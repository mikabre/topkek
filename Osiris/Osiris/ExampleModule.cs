﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Heimdall;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Globalization;
using System.Threading;

namespace Osiris
{
    public delegate void MessageHandler(string args, string source, string nick);
    public abstract class ExampleModule
    {
        public static ConnectionToRouter Connection;
        public static DateTime Start = DateTime.Now;

        public static string Name = "";

        public static Dictionary<string, MessageHandler> Commands = new Dictionary<string, MessageHandler>()
        {
        };

        public static void Init(string[] args, Action act = null)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("en-US");
            Config.Load();

            string host = "localhost";
            int port = 9933;

            if (args.Any(a => !a.StartsWith("--")))
            {
                host = args.First(a => !a.StartsWith("--"));

                if (host.Contains(':'))
                {
                    var parts = host.Split(':');
                    host = parts[0];
                    port = int.Parse(parts[1]);
                }
            }

            ConnectionInit(host, port);

            if (act != null)
                act();

            while (true)
            {
                if(args.Any() && args[0] == "--daemon")
                {
                    Thread.Sleep(int.MaxValue);
                }

                string str = Console.ReadLine();
                if (str == "quit")
                {
                    Connection.End();
                }
                else if(str == "reconnect")
                {
                    ConnectionInit(host);
                }
            }
        }

        public static void ConnectionInit(string host, int port = 9933)
        {
            Connection = new ConnectionToRouter(host, port, Name);

            Connection.AddHandler("message", CallMatcher);
            Connection.AddHandler("get_uptime", GetUptime);
            Connection.AddHandler("send_matchers", (c, m) => { SetUpMatchers(); });
            Connection.AddHandler("rehash", (c, m) => { Config.Load(); });

            SetUpMatchers();
        }

        public static void SendNotice(string message, string token, string nick)
        {
            MemoryStream ms = new MemoryStream();

            ms.WriteString(nick);
            ms.WriteString(token);
            ms.WriteString(message);

            Connection.SendMessage(ms.ToArray(), "irc_notice", "irc");

            ms.Close();
        }

        public static string MakePermanent(string token)
        {
            MemoryStream ms = new MemoryStream();
            ms.WriteString(token);

            var arr = ms.ToArray();
            
            string ret = Encoding.Unicode.GetString(Connection.WaitFor(ms.ToArray(), "get_ptoken", "irc", "ptoken"));
            ms.Close();

            return ret;
        }

        public static string GetSource(string source)
        {
            // a5cf1d34219b2c6eea4b449c88fed665
            return source.Substring(0, 16);
            //return Encoding.Unicode.GetString(Connection.WaitFor(source, "get_source", "irc", "source"));
        }

        public static void SetUpMatchers()
        {
            char[] command = new char[] { '.', '$', '!', '?', '>' };

            foreach (var pair in Commands)
            {
                if (!command.Any(c => pair.Key.StartsWith(c.ToString())))
                    AddMatcher(pair.Key, pair.Key, MatchType.Contains, false, false);
                else
                    AddMatcher(pair.Key, pair.Key, MatchType.StartsWith, pair.Key.StartsWith("$"));
            }   
        }

        public static void AddMatcher(string id, string match_str, Osiris.MatchType type, bool owner_only = false, bool last_to_execute = false)
        {
            MessageMatcher matcher = new MessageMatcher() { ID = id, MatchString = match_str, MatchType = type, Node = Name, OwnerOnly = owner_only, ExecuteIfNoMatch = last_to_execute };

            IFormatter bf = new BinaryFormatter();

            MemoryStream ms = new MemoryStream();

            bf.Serialize(ms, matcher);

            Connection.SendMessage(ms.ToArray(), "add_matcher", "irc");

            ms.Close();
        }

        public static void CallMatcher(Connection conn, Message msg)
        {
            MemoryStream ms = new MemoryStream(msg.Data);

            string id = ms.ReadString();
            string message = ms.ReadString().Trim();
            string source = ms.ReadString();
            string nick = ms.ReadString();

            if (Commands.Any(t => t.Key == id))
            {
                Commands.First(t => t.Key == id).Value(message, source, nick);
            }

            ms.Close();
        }

        public static void GetUptime(Connection conn, Message msg)
        {
            Message reply = msg.Clone(true);

            reply.MessageType = "uptime";
            reply.Data = BitConverter.GetBytes((int)(DateTime.Now - Start).TotalSeconds);

            Connection.SendMessage(reply);
        }

        public static void SendMessage(string message, string target)
        {
            MemoryStream ms = new MemoryStream();

            ms.WriteString(target);
            ms.WriteString(message);

            Connection.SendMessage(ms.ToArray(), "irc_send", "irc");

            ms.Close();
        }
    }
}

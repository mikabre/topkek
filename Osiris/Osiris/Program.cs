using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Net.Sockets;
using System.IO;
using System.Threading;

using Heimdall;

using ChatSharp;
using ChatSharp.Events;
using System.Globalization;

namespace Osiris
{
    class Program
    {
        public static ConnectionToRouter Connection;
        public static List<MessageMatcher> Matchers = new List<MessageMatcher>();
        public static IrcManager IrcManager = new IrcManager();

        public static DateTime Start = DateTime.Now;

        static void Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("en-US");

            Config.Load();

            IrcManager.OnMessage += IrcManager_OnMessage;
            IrcManager.OnNotice += IrcManager_OnNotice;
            IrcManager.OnJoin += IrcManager_OnJoin;
            IrcManager.OnModeChange += IrcManager_OnModeChange;

            var options = Directory.GetFiles("./servers");

            foreach(var file in options)
            {
                try
                {
                    var conn = ConnectionOptions.FromFile(file);
                    IrcManager.Connect(conn);

                    Console.WriteLine("Connected to {0}:{1} from file \"{2}\"", conn.Server, conn.Port, file);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception while loading connection options from \"{0}\"", file);
                    Console.WriteLine(ex);
                }
            }

            string host = "localhost";

            if (args.Length != 0)
                host = args[0];

            Connection = new ConnectionToRouter(host, 9933, "irc");

            Connection.AddHandler("add_matcher", AddMatcher);
            Connection.AddHandler("irc_send", SendMessage);
            Connection.AddHandler("irc_notice", SendNotice);
            Connection.AddHandler("clear_matchers", ClearMatchers);
            Connection.AddHandler("get_source", GetSource);
            Connection.AddHandler("set_mode", SetMode);
            Connection.AddHandler("set_mode_cs", SetModeChanserv);
            Connection.AddHandler("get_mode", GetMode);
            Connection.AddHandler("has_user", HasUser);
            Connection.AddHandler("get_users", GetUsers);
            Connection.AddHandler("get_users_noreg", GetUsersNoReg);
            Connection.AddHandler("is_registered", IsRegistered);

            foreach (string module in GetModules())
                Connection.SendMessage(new byte[0], "send_matchers", module);

            while (true)
            {
                string str = Console.ReadLine();
                if (str == "q")
                {
                    Connection.End();
                }
                else if(str == "break")
                {
                    System.Diagnostics.Debugger.Break();
                }
                //else if(str.StartsWith("say"))
                //{
                //    Client.SendMessage(string.Join(" ", str.Split(' ').Skip(2)), str.Split(' ')[1]);
                //}
                //else
                //{

                //    Client.JoinChannel(str);
                //}
            }
        }

        private static Dictionary<char, string> ModeAliases = new Dictionary<char, string>()
        {
            {'q', "OWNER"},
            {'a', "PROTECT" },
            {'h', "HALFOP" },
            {'o', "OP" },
            {'v', "VOICE" }
        };

        private static void SetModeChanserv(Connection conn, Message msg)
        {
            MemoryStream ms = new MemoryStream(msg.Data);

            var nick = ms.ReadString();
            var mode_str = ms.ReadString().ToLower();
            var token = ms.ReadString();

            bool mode_set = mode_str.StartsWith("+");
            char mode = mode_str[1];

            string command = (!mode_set ? "DE" : "") + ModeAliases[mode];

            MessageSource source = IrcManager.TokenManager.GetSource(token);
            source.Client.SendMessage(command + " " + source.Source + " " + nick, "ChanServ");
        }

        private static void IsRegistered(Connection conn, Message msg)
        {
            MemoryStream ms = new MemoryStream(msg.Data);

            var token = ms.ReadString();
            var nick = ms.ReadString();

            if (IrcManager.IsRegistered(IrcManager.TokenManager.GetSource(token).Client, nick))
                conn.SendMessage(new byte[1] { 1 }, "registered", msg.Source);
            else
                conn.SendMessage(new byte[1] { 0 }, "registered", msg.Source);
        }

        private static void IrcManager_OnModeChange(string nick, string change, Token key)
        {
            if (Matchers.Any(t => t.Mode == true))
            {
                Matchers.Where(t => t.Mode == true).ToList().ForEach(matcher =>
                {
                    var source = IrcManager.TokenManager.GetSource(key.Key);
                    string actual_source = source.Source;

                    if (IrcManager.IgnoreModules.Any(p => p.Value.Trim() == actual_source && p.Key.Trim() == matcher.Node))
                        return;

                    MemoryStream ms = new MemoryStream();

                    ms.WriteString(matcher.ID);
                    ms.WriteString(change);
                    ms.WriteString(key.Key);
                    ms.WriteString(nick);

                    Connection.SendMessage(ms.ToArray(), "message", matcher.Node);

                    ms.Close();
                });
            }
        }

        private static void GetUsers(Connection conn, Message msg)
        {
            MemoryStream ms = new MemoryStream(msg.Data);

            string token = ms.ReadString();
            
            var source = IrcManager.TokenManager.GetSource(token);

            var channel = source.Client.Channels[source.Source];

            MemoryStream ret = new MemoryStream();

            foreach(var pair in channel.UsersByMode)
            {
                foreach (var user in pair.Value)
                    ret.WriteString(pair.Key + user.Nick + (IrcManager.IsRegistered(source.Client, user.Nick) ? "+" : "-"));
            }

            var allusers = channel.UsersByMode.SelectMany(p => p.Value);

            foreach (var user in channel.Users.Where(u => !allusers.Contains(u)))
                ret.WriteString(" " + user.Nick + (IrcManager.IsRegistered(source.Client, user.Nick) ? "+" : "-"));

            conn.SendMessage(ret.ToArray(), "users", msg.Source);

            ret.Close();
            ms.Close();
        }

        private static void GetUsersNoReg(Connection conn, Message msg)
        {
            MemoryStream ms = new MemoryStream(msg.Data);

            string token = ms.ReadString();

            var source = IrcManager.TokenManager.GetSource(token);

            var channel = source.Client.Channels[source.Source];

            MemoryStream ret = new MemoryStream();

            foreach (var pair in channel.UsersByMode)
            {
                foreach (var user in pair.Value)
                    ret.WriteString(pair.Key + user.Nick);
            }

            var allusers = channel.UsersByMode.SelectMany(p => p.Value);

            foreach (var user in channel.Users.Where(u => !allusers.Contains(u)))
                ret.WriteString(" " + user.Nick);

            conn.SendMessage(ret.ToArray(), "users", msg.Source);

            ret.Close();
            ms.Close();
        }

        private static void IrcManager_OnJoin(string username, Token key)
        {
            if(Matchers.Any(t => t.Join == true))
            {
                Matchers.Where(t => t.Join == true).ToList().ForEach(matcher => 
                {
                    string actual_source = IrcManager.TokenManager.GetSource(key.Key).Source;
                    if (IrcManager.IgnoreModules.Any(p => p.Value.Trim() == actual_source && p.Key.Trim() == matcher.Node))
                        return;

                    MemoryStream ms = new MemoryStream();

                    ms.WriteString(matcher.ID);
                    ms.WriteString(username);
                    ms.WriteString(key.Key);
                    ms.WriteString(actual_source);

                    Connection.SendMessage(ms.ToArray(), "message", matcher.Node);

                    ms.Close();
                });
            }
        }

        private static void IrcManager_OnNotice(string message, Token key)
        {
            if(Matchers.Any(t => t.Matches(message)))
            {
                Matchers.Where(t => t.Matches(message)).ToList().ForEach(matcher =>
                {
                    string actual_source = IrcManager.TokenManager.GetSource(key.Key).Source;
                    if (IrcManager.IgnoreModules.Any(p => p.Value.Trim() == actual_source && p.Key.Trim() == matcher.Node) ||
                        (!string.IsNullOrWhiteSpace(matcher.Nick) && matcher.Nick != actual_source))
                        return;

                    MemoryStream ms = new MemoryStream();

                    ms.WriteString(matcher.ID);
                    ms.WriteString(message);
                    ms.WriteString(key.Key);
                    ms.WriteString(actual_source);

                    Connection.SendMessage(ms.ToArray(), "message", matcher.Node);

                    ms.Close();
                });
            }
        }

        private static void IrcManager_OnMessage(PrivateMessage message, Token key)
        {
            if (message.Message.StartsWith(".uptime"))
            {
                // H A C K
                IrcManager.SendMessage("a fuckton", key.Key);
                return;

                string uptime_str = "";

                foreach (string str in GetModules())
                {
                    int val = 0;

                    if (str == "irc")
                        val = (int)(DateTime.Now - Start).TotalSeconds;
                    else
                        val = GetUptime(str);

                    uptime_str += string.Format("{0} uptime: {1}, ", str, GetString(val));
                }

                uptime_str = new string(uptime_str.Take(uptime_str.Length - 2).ToArray());

                IrcManager.SendMessage(uptime_str, key.Key);
                return;
            }

            if (message.Message.StartsWith(".modules"))
            {
                IrcManager.SendMessage(string.Join(", ", GetModules()) + ".", key.Key);
                return;
            }

            if (Matchers.Any(t => t.Matches(message.Message)))
            {
                bool stop = false;

                var list = Matchers.Where(t => t.Matches(message.Message) && (t.OwnerOnly ? message.User.Nick == IrcManager.TokenManager.GetSource(key.Key).Client.Owner : true)).ToList();

                list.ForEach(matcher =>
                {
                    if (stop)
                        return;

                    if (!string.IsNullOrWhiteSpace(matcher.Nick) && matcher.Nick != message.User.Nick)
                        return;

                    string actual_source = IrcManager.TokenManager.GetSource(key.Key).Source;

                    if (IrcManager.IgnoreModules.Any(p => p.Value.Trim() == actual_source && p.Key.Trim() == matcher.Node))
                        return;

                    if (matcher.EndExecution)
                        stop = true;

                    if (matcher.ExecuteIfNoMatch && list.Count > 1)
                        return;

                    MemoryStream ms = new MemoryStream();

                    ms.WriteString(matcher.ID);
                    ms.WriteString(message.Message);
                    ms.WriteString(key.Key);
                    ms.WriteString(message.User.Nick);

                    Connection.SendMessage(ms.ToArray(), "message", matcher.Node);

                    ms.Close();
                });
            }
        }

        static void GetSource(Connection conn, Message msg)
        {
            string token = Encoding.Unicode.GetString(msg.Data);

            var source = IrcManager.TokenManager.GetSource(token);

            string resp = "";

            if (source == null)
                resp = "-";
            else
                resp = source.Source;

            conn.SendMessage(resp, "source", msg.Source);
        }

        static void GetMode(Connection conn, Message msg)
        {
            MemoryStream ms = new MemoryStream(msg.Data);

            string token = ms.ReadString();
            string nick = ms.ReadString();

            var source = IrcManager.TokenManager.GetSource(token);

            var channel = source.Client.Channels.First(c => c.Name == source.Source);

            if (channel.UsersByMode.All(m => !m.Value.Any(u => u.Nick == nick)))
                conn.SendMessage(" ", "mode", msg.Source);
            else
            {
                string modes = "qaohv";

                foreach (char c in modes)
                {
                    if (channel.UsersByMode.ContainsKey(c) && channel.UsersByMode[c].Any(u => u.Nick == nick))
                    {
                        conn.SendMessage(c.ToString(), "mode", msg.Source);
                        return;
                    }
                }
            }
        }
        
        static void HasUser(Connection conn, Message msg)
        {
            MemoryStream ms = new MemoryStream(msg.Data);

            string token = ms.ReadString();
            string nick = ms.ReadString();

            var source = IrcManager.TokenManager.GetSource(token);

            bool has = source.Client.Channels[source.Source].Users.Any(u => u.Nick == nick);

            conn.SendMessage(has ? "+" : "-", "has_user_resp", msg.Source);
        }

        static void SendNotice(Connection conn, Message msg)
        {
            MemoryStream ms = new MemoryStream(msg.Data);

            string nick = ms.ReadString();
            string token = ms.ReadString();
            string message = ms.ReadString();

            message = message.Replace("\n", "");
            message = message.Replace("\r", "");

            var source = IrcManager.TokenManager.GetSource(token);

            source.Client.SendRawMessage("NOTICE {0} :{1}", nick, message);
        }

        static string GetModeByLetter(char letter)
        {
            switch(letter)
            {
                case '~': return "q";
                case '&': return "a";
                case '@': return "o";
                case '%': return "h";
                case '+': return "v";
                default: return "";
            }
        }

        static void SetMode(Connection conn, Message msg)
        {
            MemoryStream ms = new MemoryStream(msg.Data);

            string token = ms.ReadString();
            string nick = ms.ReadString();
            string mode = ms.ReadString();

            var source = IrcManager.TokenManager.GetSource(token);

            //source.Client.ChangeMode(source.Source, mode + " " + nick);
            source.Client.SendRawMessage("MODE {0} {1} {2}", source.Source, mode, nick);
        }

        static void ClearMatchers(Connection conn, Message msg)
        {
            Matchers.Clear();
        }

        static void AddMatcher(Connection conn, Message msg)
        {
            IFormatter bf = new BinaryFormatter();

            MemoryStream ms = new MemoryStream(msg.Data);
            MessageMatcher matcher = (MessageMatcher)bf.Deserialize(ms);

            if (!Matchers.Any(m => m.ID == matcher.ID && m.MatchString == matcher.MatchString && m.MatchType == matcher.MatchType && m.Node == matcher.Node))
                Matchers.Add(matcher);

            ms.Close();
        }

        static void SendMessage(Connection conn, Message msg)
        {
            MemoryStream ms = new MemoryStream(msg.Data);

            string target = ms.ReadString();
            string message = ms.ReadString();

            ms.Close();

            IrcManager.SendMessage(message, target);
        }

        static string[] GetModules()
        {
            List<string> ret = new List<string>();

            byte[] modules = Connection.WaitFor("", "get_modules", "router", "modules");

            MemoryStream ms = new MemoryStream(modules);

            while (ms.Position != ms.Length)
            {
                ret.Add(ms.ReadString());
            }

            ret.Add("router");
            return ret.ToArray();
        }

        static bool IsUp(string module)
        {
            string[] up_modules = GetModules();

            return up_modules.Contains(module);
        }

        static int GetUptime(string dest)
        {
            if (!IsUp(dest))
                return -1;

            byte[] data = Connection.WaitFor("", "get_uptime", dest, "uptime");
            int ret = BitConverter.ToInt32(data, 0);
            return ret;
        }

        static string GetString(int len)
        {
            if (len == -1)
                return "down";

            TimeSpan span = new TimeSpan(0, 0, len);

            StringBuilder sb = new StringBuilder();

            if (span.Days > 0)
                sb.Append(string.Format("{0} days ", span.Days));

            if (span.Hours > 0)
                sb.Append(string.Format("{0} hours ", span.Hours));

            if (span.Minutes > 0)
                sb.Append(string.Format("{0} minutes ", span.Minutes));

            if (span.Seconds > 0)
                sb.Append(string.Format("{0} seconds ", span.Seconds));

            return sb.ToString().Trim();
        }
    }
}

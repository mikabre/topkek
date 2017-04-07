using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ChatSharp;
using ChatSharp.Events;
using System.Threading;

namespace Osiris
{
    public delegate void NoticeHandler(string message, Token key);
    public delegate void JoinHandler(string nick, Token key);
    public delegate void ModeHandler(string nick, string change, Token key);

    public class IrcManager
    {
        public delegate void MessageHandler(PrivateMessage message, Token key);

        public List<IrcClient> Clients = new List<IrcClient>();
        public TokenManager TokenManager = new TokenManager();
        public List<string> Ignore = new List<string>();
        public List<Trigger> Triggers = new List<Trigger>();
        public List<KeyValuePair<string, string>> IgnoreModules = new List<KeyValuePair<string, string>>();

        public DateTime LastBots;

        public event MessageHandler OnMessage;
        public event NoticeHandler OnNotice;
        public event JoinHandler OnJoin;
        public event ModeHandler OnModeChange;

        public IrcManager()
        {
            PingLoop();
        }

        public void Connect(ConnectionOptions options)
        {
            if (Clients.Any(c => c.ServerAddress == options.Server))
                return;

            IrcClient Client = new IrcClient(options.Server, new IrcUser(options.Nickname, options.Nickname), options.Ssl);

            if(options.ZncLogin)
            {
                Client.User = new IrcUser(options.Nickname, string.Format("{0}@bot/{1}", options.ZncUsername, options.ZncNetwork), options.ZncPassword);
            }

            Client.Options = options;

            Client.IgnoreInvalidSSL = true;

            Client.SetHandler("INVITE", new IrcClient.MessageHandler((c, msg) =>
            {
                if(msg.Prefix.StartsWith(options.Owner))
                    c.JoinChannel(msg.Parameters[1]);
            }));

            Client.SetHandler("KICK", new IrcClient.MessageHandler((c, msg) =>
            {
                if (msg.Parameters[1] == options.Nickname)
                {
                    c.PartChannel(msg.Parameters[0]);
                    System.Threading.Thread.Sleep(10000);
                    c.JoinChannel(msg.Parameters[0]);
                }
            }));
            Client.SetHandler("PONG", new IrcClient.MessageHandler((c, msg) =>
            {
                Console.WriteLine("Received PONG from {0}", c.ServerAddress);
                c.LastPong = DateTime.Now;
            }));
            Client.ConnectionComplete += (s, e) =>
            {
                Console.WriteLine("Connection complete on {0}", Client.ServerAddress);

                if (!Client.authed)
                    Client.authact();
            };
            //Client.PrivateMessageRecieved += ChannelMessage;
            Client.ChannelMessageRecieved += ChannelMessage;
            Client.NoticeRecieved += Client_NoticeRecieved;
            Client.RawMessageRecieved += Client_RawMessageRecieved;
            Client.UserJoinedChannel += Client_UserJoinedChannel;
            Client.ModeChanged += Client_ModeChanged;
            Client.NickChanged += Client_NickChanged;
            Client.UserPartedChannel += Client_UserPartedChannel;
            Client.NetworkError += Client_NetworkError;
            //Client.AddHandler

            Client.authact = delegate
            {
                if (options.NickServ)
                {
                    Client.SendMessage("identify " + options.Password, "NickServ");
                }
                Client.authed = true;
                Client.Reconnecting = false;

                Task.Factory.StartNew(delegate
                {
                    Thread.Sleep(10000);
                    foreach (string str in options.Autojoin)
                        Client.JoinChannel(str);
                });
            };

            Client.ConnectAsync();

            Client.Owner = options.Owner;
            Clients.Add(Client);
            ConnectionGuard(Client);
        }

        private void PingLoop()
        {
            Task.Factory.StartNew(delegate
            {
                while (true)
                {
                    Thread.Sleep(30000);

                    foreach (var client in Clients)
                        client.SendRawMessage("PING ayy");
                }
            });
        }

        private void ConnectionGuard(IrcClient client)
        {
            Task.Factory.StartNew(delegate
            {
                while(true)
                {
                    Thread.Sleep(1000);

                    if(!client.Reconnecting && (DateTime.Now - client.LastPong).TotalSeconds > 60)
                    {
                        Console.WriteLine("Ping timeout on {0}", client.ServerAddress);
                        Client_NetworkError(client, new SocketErrorEventArgs(System.Net.Sockets.SocketError.TimedOut));
                        return;
                    }
                }
            });
        }

        private void Client_NetworkError(object sender, SocketErrorEventArgs e)
        {
            Task.Factory.StartNew(delegate
            {
                IrcClient client = (sender as IrcClient);
                client.Reconnecting = true;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("NETWORK ERROR!");
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine(e.SocketError);
                Console.WriteLine("Attempting to reconnect...");

                Console.Write("Waiting for connection...");

                int i = 0;
                while (!Utilities.HaveConnection())
                {
                    Thread.Sleep(3000);
                    i++;

                    if (i % 20 == 0)
                        Console.Write(".");
                }

                Thread.Sleep(120000);

                if ((DateTime.Now - client.LastPong).TotalSeconds < 120)
                    return;

                Console.WriteLine("\nReconnecting...");

                ConnectionOptions options = (ConnectionOptions)client.Options;
                Clients.Remove(client);
                Thread.Sleep(120000);
                Connect(options);
                //client.authed = false;
                //client.ConnectAsync();
            });
        }

        private void Client_UserPartedChannel(object sender, ChannelUserEventArgs e)
        {
        }

        private void Client_NickChanged(IrcClient client, string prev, string now)
        {
            foreach (var channel in client.Channels.Where(c => c.Users.Any(u => u.Nick == prev || u.Nick == now)))
                Client_UserJoinedChannel(client, new ChannelUserEventArgs(channel, new IrcUser(now, "")));
        }

        private void Client_ModeChanged(object sender, ModeChangeEventArgs e)
        {
            IrcClient client = (IrcClient)sender;

            if (!e.Change.Contains(" "))
                return;

            if (!client.Channels.Any(c => c.Name == e.Target))
                return;

            var channel = client.Channels[e.Target];

            string nick = e.Change.Split(' ')[1];

            if (!channel.Users.Contains(nick))
                return;

            if (OnModeChange != null)
                OnModeChange(e.User.Nick, e.Change, TokenManager.GetToken(new MessageSource(client, channel.Name)));
        }

        private void Client_UserJoinedChannel(object sender, ChannelUserEventArgs e)
        {
            Task.Factory.StartNew(delegate
            {
                var client = (IrcClient)sender;

                if (OnJoin != null)
                    OnJoin(e.User.Nick, TokenManager.GetToken(new MessageSource(client, e.Channel.Name)));
            });
        }

        public bool IsRegistered(IrcClient client, string nick)
        {
            string query_str = "";

            if (client.ServerAddress.Contains("rizon"))
                query_str = "STATUS {0}";
            else if (client.ServerAddress.Contains("freenode"))
                query_str = "ACC {0} *";
            else
                return false;

            bool authed = false;

            ManualResetEvent reset = new ManualResetEvent(false);

            EventHandler<IrcNoticeEventArgs> handler = null;
            handler = (s, ea) =>
            {
                if (ea.Source.StartsWith("NickServ"))
                {
                    string notice = ea.Notice;
                    var words = notice.Split(' ');

                    if (words[1] != nick)
                        return;

                    if (words[2] == "3")
                        authed = true;
                    else
                        authed = false;

                    reset.Set();
                }
            };

            client.NoticeRecieved += handler;

            client.SendMessage(string.Format(query_str, nick), "NickServ");

            reset.WaitOne(10000);

            client.NoticeRecieved -= handler;

            return authed;
        }

        private void Client_NoticeRecieved(object sender, IrcNoticeEventArgs e)
        {
            IrcClient client = (IrcClient)sender;

            MessageSource source = new MessageSource(client, e.Message.Prefix.Split('!')[0]);

            Token token = TokenManager.GetToken(source);

            if (OnNotice != null)
                OnNotice(e.Notice, token);
        }

        private void Client_RawMessageRecieved(object sender, RawMessageEventArgs e)
        {
            IrcClient client = (IrcClient)sender;

            var msg = new IrcMessage(e.Message);
            string command = msg.Command.ToUpper();

            if (command != "PRIVMSG")
            {
                Triggers.ForEach(t => t.ExecuteIfMatches(new IrcMessage(e.Message), client));
            }
        }

        public void SendMessage(string message, string token)
        {
            var source = TokenManager.GetSource(token);

            if (source == null)
                return;

            source.Client.SendMessage(message, source.Source);
        }

        private void ChannelMessage(object sender, PrivateMessageEventArgs e)
        {
            IrcClient client = (IrcClient)sender;

            if (!client.authed)
                client.authact();

            if (Ignore.Contains(e.PrivateMessage.User.Nick) && e.PrivateMessage.User.Nick != client.Owner)
                return;

            //Triggers.ForEach(t => t.ExecuteIfMatches(e.IrcMessage, client));
            
            string msg = e.PrivateMessage.Message;
            bool authed = e.PrivateMessage.User.Nick == client.Owner;

            if(msg.StartsWith("$whatmatches") && authed)
            {
                string args = msg.Substring("$whatmatches".Length).Trim();

                foreach(Trigger t in Triggers)
                {
                    if(t.Matches(args))
                    {
                        client.SendMessage(string.Format("{0}: matches {1}", t.ID, t), e.PrivateMessage.Source);
                    }
                }

                return;
            }

            if (!(authed && msg.StartsWith("$")))
            {
                foreach (Trigger t in Triggers)
                {
                    string result = t.ExecuteIfMatches(e.IrcMessage, client);
                    if (result != "")
                    {
                        msg = result;
                        if (result.StartsWith("$"))
                            authed = true;

                        if (t.StopExecution)
                            break;
                    }
                }
            }
            
            if(msg.ToLower().Contains("spurdo"))
            {
                client.SendMessage(string.Format("{0}, s/spurdo/spürdo/gi", e.PrivateMessage.User.Nick), e.PrivateMessage.Source);
            }
            else if(msg.StartsWith("$ignoremodule"))
            {
                if(authed)
                {
                    IgnoreModules.Add(new KeyValuePair<string, string>(msg.Substring("$ignoremodule".Length), e.PrivateMessage.Source));
                }
                return;
            }
            else if (msg.StartsWith("$ignore"))
            {
                if (authed)
                {
                    Ignore.Add(msg.Substring(".ignore".Length).Trim());
                }
                return;
            }
            else if(msg.StartsWith("$meow"))
            {
                if (authed)
                {
                    int count = int.Parse(msg.Split(' ')[1]);

                    client.SendMessage(MacBotGen.GenerateMacBot(count, true), e.PrivateMessage.Source);
                }
                return;
            }
            else if(msg.StartsWith("$registered"))
            {
                if(authed)
                {
                    Task.Factory.StartNew(delegate
                    {
                        client.SendMessage(IsRegistered(client, msg.Substring("$registered".Length).Trim()).ToString(), e.PrivateMessage.Source);
                        return;
                    });
                }
            }
            else if(msg.StartsWith(".bang"))
            {
                client.KickUser(e.PrivateMessage.Source, e.PrivateMessage.User.Nick, "There you fucking go");
            }
            else if(msg.StartsWith(".permabang"))
            {
                Task.Factory.StartNew(delegate
                {
                    client.SendRawMessage("MODE {0} +b *!*@{1}", e.PrivateMessage.Source, e.PrivateMessage.User.Hostname);
                    client.KickUser(e.PrivateMessage.Source, e.PrivateMessage.User.Nick, "Consequences will never be the same");

                    Thread.Sleep(10000);
                    
                    client.SendRawMessage("MODE {0} -b *!*@{1}", e.PrivateMessage.Source, e.PrivateMessage.User.Hostname);
                    client.InviteUser(e.PrivateMessage.Source, e.PrivateMessage.User.Nick);
                });
            }
            else if(msg.StartsWith("$unignoremodule"))
            {
                if(authed)
                {
                    IgnoreModules.RemoveAll(p => p.Value == e.PrivateMessage.Source && p.Key == msg.Substring("$unignoremodule".Length));
                }
                return;
            }
            else if(msg.StartsWith(".prebots"))
            {
                if(authed)
                {
                    client.SendMessage("Reporting in! [C#] https://github.com/hexafluoride/topkek", msg.Split(' ')[1]);
                    LastBots = DateTime.Now;
                }
                return;
            }
            else if(msg.StartsWith("$addtrigger"))
            {
                if(authed)
                {
                    Trigger trigger = Trigger.ParseTrigger(msg.Substring("$addtrigger".Length));
                    Triggers.Add(trigger);

                    client.SendMessage(string.Format("Use 11$removetrigger {0} to remove this trigger.", trigger.ID), e.PrivateMessage.Source);
                }
                return;
            }
            else if(msg.StartsWith("$removetrigger"))
            {
                if(authed)
                {
                    string ID = msg.Substring("$removetrigger".Length).Trim();

                    Triggers.RemoveAll(t => t.ID == ID);
                    client.SendMessage(string.Format("Removed trigger 11{0}.", ID), e.PrivateMessage.Source);
                }
                return;
            }
            else if (msg.StartsWith("$unignore"))
            {
                if (authed)
                {
                    Ignore.Remove(msg.Substring(".unignore".Length).Trim());
                }
                return;
            }
            else if (msg.StartsWith(".bots"))
            {
                if ((DateTime.Now - LastBots).TotalSeconds < 3)
                    return;
                client.SendMessage("Reporting in! [C#]", e.PrivateMessage.Source);
                return;
            }
            else if (msg == "$leave")
            {
                if (authed)
                {
                    client.PartChannel(e.PrivateMessage.Source, "Bye!");
                }
                return;
            }
            else if(msg.StartsWith("$say"))
            {
                if(authed)
                {
                    client.SendMessage(msg.Substring("$say".Length).Trim(), e.PrivateMessage.Source);
                }
                return;
            }
            else if(msg.StartsWith("$raw"))
            {
                if(authed)
                {
                    client.SendRawMessage(msg.Substring("$raw".Length).Trim());
                }
                return;
            }
            else if (msg == "^")
            {
                client.SendMessage("can confirm", e.PrivateMessage.Source);
                return;
            }
            else if (msg.StartsWith("$join"))
            {
                if (authed)
                {
                    client.JoinChannel(msg.Substring(".join".Length).Trim());
                }
                return;
            }
            else if(msg.StartsWith("$macbot"))
            {
                if (authed)
                {
                    int count = int.Parse(msg.Split(' ')[1]);

                    client.SendMessage(MacBotGen.GenerateMacBot(count), e.PrivateMessage.Source);
                }
                return;
            }
            else if(msg.StartsWith("rape"))
            {
                msg = msg.Trim();
                
                if (!msg.Contains(" "))
                    return;

                if (!client.Channels[e.PrivateMessage.Source].Users.Any(user => user.Nick == msg.Split(' ')[1]))
                    return;

                string nick = msg.Split(' ')[1];

                client.SendAction(string.Format("rapes {0}", nick), e.PrivateMessage.Source);
            }
            else if (msg.ToLower() == "who's a healer slut" || msg.ToLower() == "who's the healer slut")
            {
                client.SendMessage(string.Format("I-I am, {0}~", e.PrivateMessage.User.Nick), e.PrivateMessage.Source);
            }
            else if(MatchesHealRequest(msg))
            {
                msg = msg.Trim();

                string nick = e.PrivateMessage.User.Nick;

                if(msg.Contains(" "))
                {
                    if(client.Channels[e.PrivateMessage.Source].Users.Any(user => user.Nick == msg.Split(' ')[1]))
                        nick = msg.Split(' ')[1];
                }
                double rnd = MacBotGen.Random.NextDouble();
                if (msg.ToLower().EndsWith("slut") || msg.ToLower().EndsWith("whore") || msg.ToLower().EndsWith("bıtch") || msg.ToLower().EndsWith("bitch"))
                {
                    if (rnd < 0.9 && !authed)
                        client.SendAction(string.Format("h-heals {0} (3+{1} HP~!)", nick, MacBotGen.Random.Next(90, 110)), e.PrivateMessage.Source);
                    else
                        client.SendAction(string.Format("h-heals {0} (3+{1} HP~! Critical heal~!)", nick, MacBotGen.Random.Next(250, 300)), e.PrivateMessage.Source);
                }
                else
                {
                    if (rnd < 0.9 && !authed)
                        client.SendAction(string.Format("heals {0} (3+{1} HP!)", nick, MacBotGen.Random.Next(90, 110)), e.PrivateMessage.Source);
                    else
                        client.SendAction(string.Format("heals {0} (3+{1} HP! Critical heal!)", nick, MacBotGen.Random.Next(250, 300)), e.PrivateMessage.Source);
                }

                return;
            }
            else if (msg.StartsWith("$spam"))
            {
                if (authed)
                {
                    int amount = int.Parse(msg.Split(' ')[1]);

                    Task.Factory.StartNew(delegate {
                        for(int i = 0; i < amount; i++)
                        {
                            client.SendMessage(GenerateRandom(), e.PrivateMessage.Source);
                            System.Threading.Thread.Sleep(1000);
                        }
                    });
                }
            }

            Token token = TokenManager.GetToken(new MessageSource(client, e.PrivateMessage.Source));

            e.PrivateMessage.Message = msg;

            if (OnMessage != null)
                OnMessage(e.PrivateMessage, token);
        }

        bool MatchesHealRequest(string msg)
        {
            msg = msg.ToLower();

            if (msg.Contains("heal"))
            {
                int heal_index = msg.IndexOf("heal");

                if (ContainsBefore(msg, "heal", "don't") || ContainsBefore(msg, "heal", "do not"))
                    return false;

                if (ContainsBefore(msg, "heal", "pls") ||
                    ContainsBefore(msg, "pls", "heal") ||
                    ContainsBefore(msg, "heal", "please") ||
                    ContainsBefore(msg, "me", "heal"))
                    return true;

                if (CountWords(msg) < 5 && msg.Split(' ').Any(w => w.ToLower() == "heal"))
                    return true;
            }
            else if (msg == "medic")
                return true;
            else if (msg.Contains(":(") || msg.Contains(";_;") || msg.Contains(";-;") || msg.Contains(";~;"))
                return true;

            return false;
        }

        int CountWords(string str)
        {
            return str.Trim().Count(c => c == ' ') + 1; 
        }

        bool ContainsBefore(string haystack, string needle, string second)
        {
            if (!haystack.Contains(needle) || !haystack.Contains(second))
                return false;

            return haystack.IndexOf(needle) > haystack.IndexOf(second);
        }

        string GenerateRandom()
        {
            Random rnd = new Random();

            string total = "abcdefghijklmnopqrstuvwxyz";
            string vowels = "aeiou";
            string consonants = new string(total.Where(c => !vowels.Contains(c)).ToArray());

            string ret = "";

            for(int i = 0; i < 5; i++)
            {
                string word = "";

                for(int k = 0; k < rnd.Next(7, 10); k++)
                {
                    bool cons = k % 2 == 0;

                    if (cons)
                        word += consonants[rnd.Next(consonants.Length)];
                    else
                        word += vowels[rnd.Next(vowels.Length)];
                }

                ret += word + " ";
            }

            return ret;
        }
    }
}

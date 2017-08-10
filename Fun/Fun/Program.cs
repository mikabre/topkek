using Heimdall;
using Osiris;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Web;
using System.Net;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace Fun
{
    public delegate void MessageHandler(string args, string source, string nick);
    class Program : ExampleModule
    {
        static QuoteManager Quotes;
        
        public static RuleManager RuleManager = new RuleManager();

        public static List<string> Disabled = new List<string>();
        public static List<string> LinkR9k = new List<string>();

        static void Enable(string args, string source, string nick)
        {
            string channel = source.Substring(0, 16);

            if (args.EndsWith("enable"))
            {
                if (Disabled.Contains(channel))
                    Disabled.Remove(channel);
            }
            else
            {
                if (!Disabled.Contains(channel))
                    Disabled.Add(channel);
            }

            File.WriteAllLines("./disabled", Disabled.ToArray());
        }

        static void GetRules(string args, string source, string n)
        {
            string channel = source.Substring(0, 16);

            var rules = RuleManager.GetRules(channel);

            for(int i = 0; i < rules.Length; i++)
            {
                SendMessage(string.Format("{0}. {1}", i + 1, rules[i]), source);
            }
        }

        static void Rules(string args, string source, string n)
        {
            try
            {
                string channel = source.Substring(0, 16);

                args = args.Substring("$rules".Length).Trim();

                List<string> allowed = new List<string>()
                {
                };

                if (args.StartsWith("add"))
                {
                    if (!allowed.Contains(n))
                        return;

                    args = args.Substring(3).Trim();

                    RuleManager.AddRule(channel, args);
                    return;
                }
                else if (args.StartsWith("del"))
                {
                    if (!allowed.Contains(n))
                        return;

                    args = args.Substring(3).Trim();

                    RuleManager.RemoveRule(channel, int.Parse(args) + 1);
                }
                else if (args.StartsWith("channel"))
                {
                    if (!allowed.Contains(n))
                        return;

                    var rules = RuleManager.GetRules(channel);

                    for (int i = 0; i < rules.Length; i++)
                    {
                        SendMessage(string.Format("{0}. {1}", i + 1, rules[i]), source);
                    }
                }
                else
                {
                    var rules = RuleManager.GetRules(channel);

                    for (int i = 0; i < rules.Length; i++)
                    {
                        SendNotice(string.Format("{0}. {1}", i + 1, rules[i]), source, n);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        static void YoutubeSearch(string args, string source, string n)
        {
            if (args.StartsWith(".youtube"))
                args = args.Substring(9).Trim();
            else
                args = args.Substring(4).Trim();

            if (string.IsNullOrWhiteSpace(args))
                return;

            string result = YoutubeUtils.Search(args);

            if (result == "")
                return;

            SendMessage(result, source);
        }

        static void SquareLondon(string args, string source, string n)
        {
            bool unlimited = args.StartsWith("$");

            if (!unlimited)
                return;

            args = args.Substring(8).ToUpper();
            
            if(!unlimited && args.Length > 7)
            {
                SendMessage("too long tbh", source);
                return;
            }

            string spacedargs = new string(args.Zip(new string(' ', args.Length), (f, s) => new[] { f, s })
                      .SelectMany(f => f).ToArray()).Trim();

            string reversedargs = new string(args.Reverse().ToArray());

            SendMessage(spacedargs, source);

            for(int i = 1; i < args.Length - 1; i++)
            {
                char f = args[i];
                char s = reversedargs[i];

                SendMessage(f + new string(' ', spacedargs.Length - 2) + s, source);

                System.Threading.Thread.Sleep(250);
            }

            spacedargs = spacedargs.Replace(">", "supahsekrittbh");
            spacedargs = spacedargs.Replace("<", ">");
            spacedargs = spacedargs.Replace("supahsekrittbh", "<");

            SendMessage(new string(spacedargs.Reverse().ToArray()).Replace(">", "<"), source);
        }

        static void ManageQuotes(string args, string source, string n)
        {
            try
            {
                args = args.Substring(7);

                if (args.StartsWith("add"))
                {
                    string quote = args.Substring(4);

                    if (string.IsNullOrWhiteSpace(quote))
                    {
                        SendMessage("4Quotes cannot be empty!", source);
                        return;
                    }
                    else if (Quotes.Quotes.Any() && ((DateTime.Now - Quotes.Quotes.Last().Added).TotalSeconds < 5))
                    {
                        SendMessage("4You're trying to add quotes too quickly!", source);
                        return;
                    }

                    Quotes.Add(quote);

                    SendMessage(string.Format("View your quote using 11.quote get {0}.", Quotes.Quotes.Count), source);
                }
                else if (args.StartsWith("get"))
                {
                    string IDstr = args.Substring(4);
                    int ID = -1;

                    if (string.IsNullOrWhiteSpace(IDstr) || !int.TryParse(IDstr, out ID) || (ID < 1 || ID > Quotes.Quotes.Count))
                    {
                        SendMessage("4Invalid ID!", source);
                        return;
                    }

                    SendMessage(Quotes.GetQuoteById(ID), source);
                }
                else if (args == "random")
                {
                    SendMessage(Quotes.GetRandomQuote(), source);
                }
                else if (args.StartsWith("delete"))
                {
                    string IDstr = args.Substring(7);
                    int ID = -1;

                    if (string.IsNullOrWhiteSpace(IDstr) || !int.TryParse(IDstr, out ID) || (ID < 1 || ID > Quotes.Quotes.Count))
                    {
                        SendMessage("4Invalid ID!", source);
                        return;
                    }

                    Quotes.DeleteQuoteById(ID);

                    SendMessage(string.Format("Deleted quote 11#{0}.", ID), source);
                }
                else
                {
                    SendMessage("Usage: .quote [add <quote> | get <ID> | delete <ID> | random ]", source);
                }
            }
            catch { }
        }

        static void PermaTokenTest(string args, string source, string nick)
        {
            args = args.Substring("$ptoken".Length).Trim();

            if(!string.IsNullOrWhiteSpace(args))
            {
                SendMessage("Testing token " + args, args);
                return;
            }

            SendMessage("Temporary token: " + source, source);
            string ptoken = MakePermanent(source);
            SendMessage("Permanent token: " + ptoken, source);
        }

        static void SetCache(string args, string source, string nick)
        {
            args = args.Substring("$setcache".Length).Trim();

            int length = 0;
            if (!int.TryParse(args, out length))
                return;

            TimedCache.DefaultExpiry = TimeSpan.FromSeconds(length);
        }

        static void GetCache(string args, string source, string nick)
        {
            SendMessage(TimedCache.DefaultExpiry.TotalSeconds + " seconds", source);

            foreach(var item in LinkResolver.Cache.List)
            {
                SendMessage(string.Format("{0}: \"{1}\", expires in {2}", item.ID, item.Content, ((item.Added + item.Expiry) - DateTime.Now)), source);
                System.Threading.Thread.Sleep(250);
            }
        }

        static void SearchImage(string args, string source, string n)
        {
            bool random = args[2] == 'r';

            args = args.Substring(".im".Length).Trim();
            string result = Fun.ImageSearch.SearchImages(args, random, false);

            if (result != "-")
                SendMessage(string.Format("Found {0}: {1}", args, result), source);
        }

        static void GifSearch(string args, string source, string n)
        {
            bool random = args[3] == 'r';

            args = args.Substring(".gif".Length).Trim();
            if (random)
                args = args.Substring(1).Trim();

            string result = Fun.ImageSearch.SearchImages(args, true, true);

            if (result != "-")
                SendMessage(string.Format("Found {0}: {1}", args, result), source);
        }

        static void TumblrSearch(string args, string source, string n)
        {
            bool random = args[2] == 'r';

            args = args.Substring(".tu".Length).Trim();
            string result = Fun.ImageSearch.SearchImages(args + " site:tumblr.com", random, false);

            if (result != "-")
                SendMessage(string.Format("Found {0}: {1}", args, result), source);
        }

        static void GoogleSearch(string args, string source, string n)
        {
            //bool random = args.StartsWith(".g");

            try
            {
                ManualResetEvent finished = new ManualResetEvent(false);

                Task.Factory.StartNew(delegate
                {
                    args = args.Substring(".g".Length).Trim();
                    string result = Fun.ImageSearch.SearchLinks(args, false);

                    if (result != "-")
                    {
                        SendMessage(string.Format("Found {0}: {1}", args, result), source);
                        finished.Set();
                    }
                });

                if (!finished.WaitOne(3000))
                    throw new Exception();
            }
            catch
            {
                //SendMessage("It appears that Google is currently blocking search queries. Please try again in a few hours(it usually works when the sun is over Europe)! Try using ?ddg for now.", source);
                DdgSearch("fallback " + args, source, n);
            }
        }

        static void DdgSearch(string args, string source, string n)
        {
            //bool random = args.StartsWith(".g");

            try
            {
                ManualResetEvent finished = new ManualResetEvent(false);

                Task.Factory.StartNew(delegate
                {
                    bool fallback = args.StartsWith("fallback");

                    if (fallback)
                        args = args.Substring("fallback".Length).Trim();
                    else
                        args = args.Substring(".ddg".Length).Trim();

                    string result = Fun.ImageSearch.SearchLinksDdg(args, false);

                    if (result != "-")
                    {
                        SendMessage(string.Format("Found {0}{2}: {1}", args, result, fallback ? " (duckduckgo fallback)" : ""), source);
                        finished.Set();
                    }
                });

                if (!finished.WaitOne(3000))
                    throw new Exception();
            }
            catch
            {
                SendMessage("How did this even fail?", source);
            }
        }

        static void NoU(string args, string source, string n)
        {
            //SendMessage("no u", source);
        }

        static void Remind(string args, string source, string n)
        {
            if (!args.StartsWith("!remind"))
                return;

            args = args.Substring("!remind".Length).Trim();

            StringBuilder msg = new StringBuilder();
            StringBuilder t = new StringBuilder();
            bool in_quotes = false;

            for(int i = 0; i < args.Length; i++)
            {
                if(args[i] == '"')
                {
                    in_quotes = !in_quotes;
                    continue;
                }

                if (!in_quotes)
                {
                    t.Append(args[i]);
                }
                else
                    msg.Append(args[i]);
            }

            var time = RemindManager.Get(t.ToString());

            if((DateTime.Now - time).Duration().TotalSeconds < 1)
            {
                SendMessage("What?", source);
                return;
            }

            //SendMessage("Message: " + msg.ToString(), source);
            SendMessage("I'll remind you at " + time.ToString(), source);
            //SendMessage("Token: " + MakePermanent(source), source);

            RemindManager.Add(time, msg.ToString(), n, MakePermanent(source));
        }

        static void GetComments(string args, string source, string n)
        {
            args = args.Substring("$comment".Length).Trim();
            SendMessage(YoutubeUtils.GetComment(args, source, n), source);
        }

        static void Main(string[] args)
        {
            Name = "lynx";

            Quotes = new QuoteManager();
            RuleManager = RuleManager.Load();
            TellManager.Load();
            RemindManager.Load();
            RemindManager.ReminderDone += (r) =>
            {
                SendMessage(string.Format("{0}, you asked to be reminded of \"{1}\" at {2}.", r.Nick, r.Message, r.StartDate), r.Token);
            };
            Task.Factory.StartNew(RemindManager.TimingLoop);

            Commands = new Dictionary<string, Osiris.MessageHandler>()
            {
                {"", CheckTells },
                {"$getid", GetIdentifier },
                {"http", ResolveLink },
                {"no u", NoU },
                {".quote", ManageQuotes },
                {"$london", SquareLondon },
                {"!london", SquareLondon },
                {"?youtube", YoutubeSearch },
                {"?yt", YoutubeSearch },
                {"!rules", Rules },
                {"$links", Enable },
                {"$getcache", GetCache },
                {"$setcache", SetCache },
                {"?im", SearchImage },
                {"?ir", SearchImage },
                {"?gif", GifSearch },
                {"?gifr", GifSearch },
                {"?tu", TumblrSearch },
                {"?tr", TumblrSearch },
                {"?g ", GoogleSearch },
                {"?ddg ", DdgSearch },
                {"$ptoken", PermaTokenTest },
                {"!remind", Remind },
                {".ud", UrbanSearch },
                {"$comment", GetComments },
                {">tell", Tell },
                {".wa", Wolfram },
                {">wa", Wolfram },
                {".dumptells", DumpTells }
            };
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls; // comparable to modern browsers
            
            if(File.Exists("./disabled"))
                Disabled = File.ReadAllLines("./disabled").ToList();

            Init(args, delegate {
                var ch = new FourChanResolver();

                LinkResolver.AddResolver(ch);
                LinkResolver.AddResolver(new FourChanImageResolver() { Parent = ch });
                LinkResolver.AddResolver(new SoundCloudResolver());
                LinkResolver.AddResolver(new YoutubeWrapper());
            });
        }

        public static void UrbanSearch(string args, string source, string n)
        {
            args = args.Substring(".ud".Length).Trim();
            SendMessage(GetUd(args), source);
        }

        public static void DumpTells(string args, string source, string n)
        {
            SendMessage("I'm now sending you(via NOTICEs) all >tells you've ever sent, and all >tells you've ever received.", source);

            foreach (var tell in TellManager.Tells.Where(t => t.To == n))
                SendNotice(tell.ToString(), source, n);

            foreach (var tell in TellManager.Tells.Where(t => t.From == n))
                SendNotice(tell.ToString(), source, n);
        }

        static WebClient ud_client = new WebClient();
        static WebClient wa_client = new WebClient();

        public static void Wolfram(string args, string source, string n)
        {
            string query = args.Substring(".wa".Length).Trim();
            string result = "";

            try
            {
                string response = wa_client.DownloadString(string.Format("https://api.wolframalpha.com/v2/query?appid={0}&input={1}&output=json&includepodid=Input&includepodid=Result&format=plaintext", Config.GetString("wolfram.key"), HttpUtility.UrlEncode(query)));
                var obj = JObject.Parse(response);

                if (!obj["queryresult"].Value<bool>("success"))
                {
                    throw new Exception();
                }

                var pods = obj["queryresult"]["pods"];

                var input_pod = pods.First(p => p.Value<string>("id") == "Input");
                var result_pod = pods.First(p => p.Value<string>("id") == "Result");

                result = string.Format("10{0} = 12{1}", input_pod["subpods"].First.Value<string>("plaintext"), result_pod["subpods"].First.Value<string>("plaintext"));
            }
            catch
            {
                try
                {
                    result = wa_client.DownloadString(string.Format("https://api.wolframalpha.com/v1/result?appid={0}&i={1}&units=metric", Config.GetString("wolfram.key"), HttpUtility.UrlEncode(query)));
                }
                catch
                {
                    SendMessage("[4Wolfram] 4Couldn't display answer", source);
                    return;
                }
            }

            SendMessage(string.Format("[4Wolfram] {1}", n, result), source);
        }

        public static string GetUd(string query)
        {
            string endpoint = "https://mashape-community-urban-dictionary.p.mashape.com/define?term={0}";
            string key = Config.GetString("mashape.key");

            ud_client.Headers["X-Mashape-Key"] = key;
            ud_client.Headers["Accept"] = "text/plain";

            string response = ud_client.DownloadString(string.Format(endpoint, query));
            JObject obj = JObject.Parse(response);

            if (obj["list"].Any())
                return string.Format("{0}: {1}", query, obj["list"].First()["definition"].Value<string>().Replace("\r\n", " "));

            return string.Format("No definition found for {0}.", query);
        }

        static void Tell(string args, string source, string n)
        {
            args = args.Substring(">tell".Length).Trim();

            if (Config.Contains("tell.disabled", GetSource(source)))
                return;

            string nick = args.Split(' ')[0];
            string message = string.Join(" ", args.Split(' ').Skip(1));

            string[] tell_responses = new string[] { "teehee", "rawr xD" };

            TellManager.Tell(n, nick, message);
            SendMessage(ImageSearch.Random.NextDouble() > 0.5 ? "okay buddy!!!" : tell_responses[ImageSearch.Random.Next(tell_responses.Length)], source);
        }

        static void CheckTells(string args, string source, string n)
        {
            if(RemindManager.SeenTrackedNicks.Contains(n))
                if (RemindManager.SeenTracker.Any(s => s.Key.Nick == n))
                    foreach (var p in RemindManager.SeenTracker.Where(s => s.Key.Nick == n))
                        p.Value.Set();

            if (ImageSearch.Random.NextDouble() < (1d / Config.GetDouble("skynet.chance")))
            {
                string comment = YoutubeUtils.GetComment(args, GetSource(source), n);
                if (comment.Length > 0)
                    SendMessage(comment, source);
            }

            var tells = TellManager.GetTells(n);

            if (!tells.Any())
                return;

            foreach (var tell in tells)
            {
                SendMessage(tell.ToString(), source);
                TellManager.Expire(tell);
            }

            TellManager.Save();
        }

        static async void GetIdentifier(string args, string source, string n)
        {
            try
            {
                string channel = source.Substring(0, 16);

                if (Disabled.Contains(channel))
                    return;

                Console.WriteLine(args);

                if (args.StartsWith("Reporting in!"))
                    return;

                System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

                Regex regex = new Regex(@"(?<Protocol>\w+):\/\/(?<Domain>[\w@][\w-.:@]+)\/?[\w\.~?=%&=\-@/$,]*");

                if (!regex.IsMatch(args))
                    return;

                string url = regex.Matches(args)[0].Value;

                var result = await LinkResolver.GetSummary(url);
                string summary = result.Key;

                if (summary == "-")
                    return;

                summary = HttpUtility.HtmlDecode(summary);

                sw.Stop();

                SendMessage(string.Format("{0} ({1}s)", summary, sw.Elapsed.TotalSeconds.ToString("0.00")), source);
            }
            catch
            {

            }
        }

        static async void ResolveLink(string args, string source, string n)
        {
            try
            {
                string channel = source.Substring(0, 16);

                if (Disabled.Contains(channel))
                    return;

                Console.WriteLine(args);

                if (args.StartsWith("Reporting in!"))
                    return;

                System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

                Regex regex = new Regex(@"(?<Protocol>\w+):\/\/(?<Domain>[\w@][\w-.:@]+)\/?[\w\.~?=%&=\-@/$,]*");

                if (!regex.IsMatch(args))
                    return;

                var match = regex.Matches(args)[0];
                int fragment_index = match.Index + match.Length;
                string fragment = "";

                if (args.Length > fragment_index)
                {
                    if (args[fragment_index] == '#')
                    {
                        while (args.Length > fragment_index && args[fragment_index] != ' ')
                            fragment += args[fragment_index++];
                    }
                }

                string url = match.Value + fragment;

                var result = await LinkResolver.GetSummary(url);
                string summary = result.Value;

                if (summary == "-")
                    return;

                summary = HttpUtility.HtmlDecode(summary);

                sw.Stop();

                SendMessage(string.Format("{0} ({1}s)", summary, sw.Elapsed.TotalSeconds.ToString("0.00")), source);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}

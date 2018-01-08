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
using Exchange;

namespace Fun
{
    public delegate void MessageHandler(string args, string source, string nick);
    class Program : ExampleModule
    {
        static QuoteManager Quotes;
        
        public static RuleManager RuleManager = new RuleManager();

        public static List<string> Disabled = new List<string>();
        public static List<string> LinkR9k = new List<string>();

        public static TwitterResolver Twitter = new TwitterResolver();

        public static Dictionary<string, List<string>> CommentSources = new Dictionary<string, List<string>>();

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
                    "NUL"
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

        static void LastFm(string args, string source, string n)
        {
            Console.WriteLine("GOD DARN IT");

            try
            {
                if (args.StartsWith(">fm"))
                    args = args.Substring(">fm".Length).Trim();
                else
                    args = args.Substring(">lastfm".Length).Trim();

                string username = "";

                Console.WriteLine("{0} {1}", args, n);

                if (!string.IsNullOrWhiteSpace(args))
                {
                    username = args;

                    if (Config.Contains("lastfm.users", t => t.ToString().StartsWith(n.ToLower()), out JToken token))
                        Config.Remove("lastfm.users", token.ToString());

                    Config.Add("lastfm.users", n.ToLower() + ":" + username);
                    Config.Save();
                }
                else
                {
                    if (Config.Contains("lastfm.users", t => t.ToString().StartsWith(n.ToLower()), out JToken token))
                    {
                        username = token.ToString().Split(':')[1];
                    }
                    else
                    {
                        SendMessage("You need to specify a username with >fm <username> first.", source);
                        return;
                    }
                }

                // magic happens here

                Console.WriteLine("before last.fm clal");
                string response = wa_client.DownloadString(string.Format("http://ws.audioscrobbler.com/2.0/?method=user.getrecenttracks&user={0}&format=json&api_key={1}", HttpUtility.UrlEncode(username), Config.GetString("lastfm.key")));
                Console.WriteLine("last.fm call complete");
                var resp_json = JObject.Parse(response);
                Console.WriteLine(1);
                var tracks = resp_json["recenttracks"];
                Console.WriteLine(2);
                var track_obj = tracks["track"];
                Console.WriteLine(3);
                var track = track_obj[0];
                Console.WriteLine(4);

                Console.WriteLine(track);

                string artist = track["artist"].Value<string>("#text");
                Console.WriteLine(5);
                string track_name = track.Value<string>("name");
                Console.WriteLine(6);

                //var span = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0).AddSeconds(track["date"].Value<ulong>("uts"));

                string album = "";

                try
                {
                    album = track["album"].Value<string>("#text");
                }
                catch
                {

                }

                SendMessage(string.Format("{0} is currently listening to {1} - {2}{3}.", string.IsNullOrWhiteSpace(args) ? n : username, artist, track_name, album != "" ? string.Format(" on album {0}", album) : ""), source);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Thigns are wrong");
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
                        var preview_result = LinkResolver.GetSummaryWithTimeout(result);

                        string preview = preview_result.Value == "" ? "link preview timed out" : preview_result.Value;

                        if(!finished.WaitOne(0))
                            SendMessage(string.Format("Found {0}: {1} | {2}", args, result, preview), source);
                        finished.Set();
                    }
                });

                if (!finished.WaitOne(3000))
                {
                    finished.Set();
                    throw new Exception();
                }
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
                        var preview_result = LinkResolver.GetSummaryWithTimeout(result);

                        string preview = preview_result.Value == "" ? "link preview timed out" : preview_result.Value;

                        if (!finished.WaitOne(0))
                            SendMessage(string.Format("Found {0} (duckduckgo fallback): {1} | {2}", args, result, preview), source);
                        finished.Set();
                    }
                });

                if (!finished.WaitOne(3000))
                    throw new Exception();
            }
            catch
            {
                //SendMessage("How did this even fail?", source);
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
            SendMessage(string.Format("I'll remind you at {0}, which is {1} from now.", time.ToString(), Utilities.TimeSpanToPrettyString(time - DateTime.Now)), source);
            //SendMessage("Token: " + MakePermanent(source), source);

            RemindManager.Add(time, msg.ToString(), n, MakePermanent(source));
        }

        static void GetComments(string args, string source, string n)
        {
            args = args.Substring("$comment".Length).Trim();
            var tuple = YoutubeUtils.GetComment(args, source, n);
            SendMessage(tuple.Item2, source);

            source = GetSource(source);

            if (!CommentSources.ContainsKey(source))
                CommentSources[source] = new List<string>();

            CommentSources[source].Add(string.Format("{0} | {1}", tuple.Item1, tuple.Item2));
        }

        static void Main(string[] args)
        {
            Name = "lynx";

            Quotes = new QuoteManager();
            RuleManager = RuleManager.Load();
            TellManager.Load();
            RemindManager.Load();
            CryptoHandler.Init();
            MonitorManager.Init();
            AlertManager.Init();
            RemindManager.ReminderDone += (r) =>
            {
                SendMessage(string.Format("{0}, you asked to be reminded of \"{1}\" at {2}.", r.Nick, r.Message, r.StartDate), r.Token);
            };
            Task.Factory.StartNew(RemindManager.TimingLoop);

            Commands = new Dictionary<string, Osiris.MessageHandler>()
            {
                {"", CheckTells },
                {"$getid ", GetIdentifier },
                {"http", ResolveLink },
                {"no u", NoU },
                {".quote", ManageQuotes },
                {"$london ", SquareLondon },
                {"!london ", SquareLondon },
                {"?youtube ", YoutubeSearch },
                {"?yt ", YoutubeSearch },
                {"!rules", Rules },
                {"$links", Enable },
                {"$getcache", GetCache },
                {"$setcache ", SetCache },
                {".cache", CacheStats },
                {"?im ", SearchImage },
                {"?ir ", SearchImage },
                {"?gif ", GifSearch },
                {"?gifr ", GifSearch },
                {"?tu ", TumblrSearch },
                {"?tr ", TumblrSearch },
                {"?g ", GoogleSearch },
                {"?ddg ", DdgSearch },
                {"$ptoken", PermaTokenTest },
                {"!remind ", Remind },
                {".ud ", UrbanSearch },
                //{"$comment ", GetComments },
                {">tell ", Tell },
                {">lastfm", LastFm },
                {">fm", LastFm },
                {".wa ", Wolfram },
                {">plot ", WolframPlot },
                {">waplot ", WolframPlot },
                {">wa ", Wolfram },
                {".imperial", SetUnits },
                {".metric", SetUnits },
                {".units", GetUnits },
                {".dumptells", DumpTells },
                {"?tw ", TwitterSearch },
                //{".source", Source },
                {".help", Help },
                //{".btc", BtcRate },
                {".exc", ExchangeRate },
                {".insult", Insult },
                {".strlen", StringLength },
                {".len", StringLength },
                {"$monitor", Monitor },
                {".alert", SetAlert },
                {".geocode", Geocode },
                {".weather", GetWeather }
            };
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls; // comparable to modern browsers
            
            if(File.Exists("./disabled"))
                Disabled = File.ReadAllLines("./disabled").ToList();

            Init(args, delegate {
                Twitter.Init();

                var ch = new FourChanResolver();

                LinkResolver.AddResolver(ch);
                LinkResolver.AddResolver(new FourChanImageResolver() { Parent = ch });
                LinkResolver.AddResolver(new SoundCloudResolver());
                LinkResolver.AddResolver(new YoutubeWrapper());

                Task.Factory.StartNew(delegate 
                {
                    Thread.Sleep(10000);

                    foreach(var board in Config.GetArray<string>("4chan.default_boards"))
                    {
                        Console.WriteLine("Loading board /{0}/", board);
                        ch.GetBoard(board);
                    }
                });
            });
        }

        static void CacheStats(string args, string source, string n)
        {
            SendMessage(string.Format("{0:##,#} cache requests, {1:##,#} cache hits, {2:0.00}% hit ratio, {3:##,#} items in cache",
                LinkResolver.Cache.RequestCount,
                LinkResolver.Cache.HitCount,
                ((double)LinkResolver.Cache.HitCount / (double)LinkResolver.Cache.RequestCount) * 100d,
                LinkResolver.Cache.List.Count), source);
        }

        static void GetWeather(string args, string source, string n)
        {
            try
            {
                args = args.Substring(".weather".Length).Trim();

                string human = "";
                
                if (!string.IsNullOrWhiteSpace(args))
                {
                    human = args;

                    if (Config.Contains("weather.locations", t => t.Value<string>().StartsWith(n.ToLower()), out JToken token))
                        Config.Remove("weather.locations", token);

                    Config.Add("weather.locations", n.ToLower() + ":" + human);
                    Config.Save();
                }
                else
                {
                    if (Config.Contains("weather.locations", t => t.Value<string>().StartsWith(n.ToLower()), out JToken token))
                    {
                        human = token.ToString().Split(':')[1];
                    }
                    else
                    {
                        SendMessage("You need to specify a location with .weather <location> first.", source);
                        return;
                    }
                }

                var coords = Geocoder.GetLatLong(human);
                var results = Weather.TryGetSummary(coords.Item1, coords.Item2, n);

                SendMessage(n + ": " + results, source);
            }
            catch (Exception ex)
            {
                SendMessage("Something happened: " + ex.Message, source);
            }
        }

        static void Geocode(string args, string source, string n)
        {
            var result = Geocoder.GetLatLong(args.Substring(".geocode".Length).Trim());

            if (result == null)
                SendMessage("Failed", source);
            else
                SendMessage(string.Format("{0}, {1}", result.Item1, result.Item2), source);
        }

        static List<string> Insults = new List<string>();
        static int insult_index = -1;
        static Random Random = new Random();

        static void SetAlert(string args, string source, string n)
        {
            args = args.Substring(".alert".Length).Trim();

            var parts = args.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            
            if(parts.Contains("<") || parts.Contains(">"))
            {

            }
        }

        static void Monitor(string args, string source, string n)
        {
            args = args.Substring("$monitor".Length).Trim();
            var parts = args.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Any())
            {
                if (parts[0] == "add")
                {
                    var monitor = new Monitor();
                    monitor.Ticker = new Ticker(parts[1], parts[2]);
                    monitor.Precision = int.Parse(parts[3]);
                    monitor.Unit = (TickerUnit)Enum.Parse(typeof(TickerUnit), parts[4], true);

                    MonitorManager.Monitors.Add(monitor);
                    MonitorManager.Save();

                    SendMessage(string.Format("Added monitor with ticker {0}, precision {1}, unit {2}", monitor.Ticker, monitor.Precision, monitor.Unit), source);
                }
                else if(parts[0] == "remove")
                {
                    var ticker = new Ticker(parts[1], parts[2]);

                    if(MonitorManager.Monitors.Any(m => m.Ticker == ticker))
                    {
                        MonitorManager.Monitors.RemoveAll(m => m.Ticker == ticker);
                    }

                    MonitorManager.Save();
                }
                else if(parts[0] == "start")
                {
                    MonitorManager.Start();
                }
                else if (parts[0] == "stop")
                {
                    MonitorManager.Stop();
                }
                else if (parts[0] == "reorder")
                {
                    parts = parts.Skip(1).ToArray();

                    var new_monitors = new List<Monitor>();

                    for(int i = 0; i < parts.Length; i++)
                    {
                        new_monitors.Add(MonitorManager.Monitors.First(m => string.Equals(m.Ticker.ToString(), parts[i], StringComparison.InvariantCultureIgnoreCase)));
                    }

                    MonitorManager.Monitors = new_monitors;
                    SendMessage("Done.", source);
                }
            }
            else
            {
                SendMessage("Triggering monitor update...", source);
                MonitorManager.PrintMonitorStatus();
            }
            //args = args.Substring("$monitor".Length).Trim();

            //var ptoken = args.Split(' ').Last();

            //var thread = new Thread(new ThreadStart(delegate
            //{
            //    int count = 5;

            //    for(int i = 0; i < count; i++)
            //    {
            //        ExchangeRate(".exc" + args, ptoken, n);
            //        SendMessage(string.Format("{0} of {1}", i + 1, count), ptoken);
            //        Thread.Sleep(5000);
            //    }
            //}));

            //thread.Start();
        }

        static List<string> Shuffle(IEnumerable<string> input)
        {
            var input_copy = input.ToList();
            List<string> ret = new List<string>();

            while(input_copy.Any())
            {
                int index = Random.Next(input_copy.Count);
                ret.Add(input_copy[index]);
                input_copy.RemoveAt(index);
            }

            return ret;
        }

        public static void StringLength(string args, string source, string n)
        {
            if (args.StartsWith(".len"))
                args = args.Substring(".len".Length).TrimStart();
            else if (args.StartsWith(".strlen"))
                args = args.Substring(".strlen".Length).TrimStart();

            SendMessage(n + ": " + args.Length.ToString(), source);
        }

        public static void Insult(string args, string source, string n)
        {
            if (insult_index == -1)
            {
                if (!File.Exists("./insults.txt"))
                    return;

                var insults_tmp = File.ReadAllLines("./insults.txt").Where(i => i.Trim().Any() && i.Contains("{0}"));
                insults_tmp = insults_tmp.Distinct();

                if (!insults_tmp.Any())
                    return;

                Insults = Shuffle(insults_tmp);
                insult_index = 0;
            }
            else
                insult_index = (insult_index + 1) % Insults.Count;

            SendMessage(string.Format(Insults[insult_index], n), source);
        }

        static double _btc_rate = 0;
        static DateTime _btc_last = DateTime.Now.Subtract(TimeSpan.FromDays(1));

        public static double GetBitcoinRate()
        {
            if ((DateTime.Now - _btc_last).TotalMinutes < 6)
                return _btc_rate;

            var response = JObject.Parse(wa_client.DownloadString("https://api.coindesk.com/v1/bpi/currentprice.json"));
            _btc_rate = response["bpi"]["USD"].Value<double>("rate");

            _btc_last = DateTime.Now;

            return _btc_rate;
        }

        public static void ExchangeRate(string args, string source, string n)
        {
            args = args.Substring(".exc".Length).Trim();

            var parts = args.Split(' ');

            if(!parts.Any(p => double.TryParse(p, out double tmp)))
            {
                SendMessage("Sorry, I couldn't understand that. Try something like .exc 100 usd to btc. Known currencies are: " + string.Join(", ", CryptoHandler.Tickers), source);
                return;
            }

            var amount = double.Parse(parts.First(p => double.TryParse(p, out double tmp)));

            var eligible = parts.Where(p => CryptoHandler.Tickers.Contains(p.ToUpper())).ToList();

            if(eligible.Count < 2)
            {
                SendMessage("Sorry, I couldn't understand that. Try something like .exc 100 usd to btc. Known currencies are: " + string.Join(", ", CryptoHandler.Tickers), source);
                return;
            }

            try
            {
                SendMessage(CryptoHandler.Convert(eligible[0], eligible[1], amount), source);
            }
            catch (Exception ex)
            {
                SendMessage(string.Format("Exception occurred: {0}", ex.Message), source);
            }
        }

        public static void BtcRate(string args, string source, string n)
        {
            args = args.Substring(".btc".Length).Trim();

            double rate = GetBitcoinRate();

            double minutes =
                (DateTime.Now - _btc_last).TotalMinutes;

            string last_update =
                minutes < 0.01 ? "now" :
                minutes < 1 ? "less than a minute ago" :
                minutes < 1.99 ? "1 minute ago" :
                ((int)minutes).ToString() + " minutes ago";

            if (double.TryParse(args, out double btc_amount))
            {
                SendMessage(string.Format("{0} BTC = 3${1:0.00} USD (1 BTC = 3${2:0.00} USD, updated {3}{4})", btc_amount, btc_amount * rate, rate, last_update,
                    btc_amount == 1 ? ". psst, 2.btc 1 is the same as 2.btc" : ""), source);
                return;
            }

            args = args.TrimStart('$');

            if(double.TryParse(args, out double usd_amount))
            {
                SendMessage(string.Format("3${0} USD = {1:0.0000} BTC (1 BTC = 3${2:0.00} USD, updated {3})", usd_amount, usd_amount / rate, rate, last_update), source);
                return;
            }

            SendMessage(string.Format("1 BTC = 3${0:0.00} USD, updated {1}", rate, last_update), source);
        }

        public static bool PrefersMetric(string nick)
        {
            if (!Config.Contains("wolfram.units", n => n.Value<string>().StartsWith(nick), out JToken token))
                return Config.GetValue<bool>("wolfram.metric");

            return token.Value<string>().Split(':')[1] == "metric";
        }

        public static void GetUnits(string args, string source, string n)
        {
            SendMessage(string.Format("You are currently using {0} units.", PrefersMetric(n) ? "metric" : "imperial"), source);
        }

        public static void SetUnits(string args, string source, string n)
        {
            args = args.Substring(1).Trim().ToLower();

            try
            {
                if (args == "imperial")
                {
                    if (Config.Contains("wolfram.units", t => t.Value<string>().StartsWith(n), out JToken token))
                    {
                        Console.WriteLine("Removed {0}", token);
                        Config.Remove("wolfram.units", token);
                    }

                    Config.Add("wolfram.units", n + ":imperial");
                    SendMessage(string.Format("You are currently using {0} units.", PrefersMetric(n) ? "metric" : "imperial"), source);
                }
                else if (args == "metric")
                {
                    if (Config.Contains("wolfram.units", t => t.Value<string>().StartsWith(n), out JToken token))
                    {
                        Console.WriteLine("Removed {0}", token);
                        Config.Remove("wolfram.units", token);
                    }

                    Config.Add("wolfram.units", n + ":metric");
                    SendMessage(string.Format("You are currently using {0} units.", PrefersMetric(n) ? "metric" : "imperial"), source);
                }

                Config.Save();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        public static void Help(string args, string source, string n)
        {
            SendMessage("See http://hexafluoride.dryfish.net/help for help.", source);
        }

        public static void Source(string args, string source, string n)
        {
            string real_source = source;
            source = GetSource(source);
            args = args.Substring(".source".Length);

            if (!CommentSources.ContainsKey(source) || !CommentSources[source].Any())
            {
                SendMessage("My skynet module hasn't been triggered in this channel yet. Try again later!", real_source);
                return;
            }

            var list = CommentSources[source];

            if(int.TryParse(args, out int count))
            {
                if (list.Count >= count && count > 0)
                {
                    SendMessage(list[(list.Count - count)], real_source);
                    return;
                }
            }

            SendMessage(list.Last(), real_source);
        }

        public static void TwitterSearch(string args, string source, string n)
        {
            args = args.Substring("?tw".Length).Trim();

            var tweet = Twitter.GetSearchResult(args);

            SendMessage(tweet, source);
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

        static List<string> preferred_pods = new List<string>()
            {
                "Result",
                "Values",
            };

        static List<string> preferred_scanners = new List<string>()
        {
            "Series"
        };

        static string MakeWolframQuery(string query, bool metric = true, bool images = false)
        {
            return wa_client.DownloadString(string.Format("https://api.wolframalpha.com/v2/query?appid={0}&input={1}&output=json&format={3}plaintext&units={2}", Config.GetString("wolfram.key"), HttpUtility.UrlEncode(query), metric ? "metric" : "nonmetric", images ? "image," : ""));
        }

        public static void Wolfram(string args, string source, string n)
        {
            if (args.StartsWith(">waplot"))
                return;

            string query = args.Substring(".wa".Length).Trim();
            string result = "";

            string suggestion = "";

            try
            {
                string response = MakeWolframQuery(query, PrefersMetric(n));
                var obj = JObject.Parse(response);

                var queryresult = obj["queryresult"];

                if (!queryresult.Value<bool>("success")) // check for did you means
                {
                    if (queryresult["didyoumeans"] != null)
                    {
                        if (queryresult["didyoumeans"].Type == JTokenType.Array)
                        {
                            suggestion = queryresult["didyoumeans"].OrderByDescending(p => p.Value<double>("score")).First().Value<string>("val");
                        }
                        else
                            suggestion = queryresult["didyoumeans"].Value<string>("val");

                        response = MakeWolframQuery(suggestion, PrefersMetric(n));
                        obj = JObject.Parse(response);
                        queryresult = obj["queryresult"];
                    }
                    else
                        throw new Exception();
                }

                var pods = queryresult["pods"];

                JToken result_pod = null;

                for (int i = 0; i < preferred_pods.Count; i++)
                {
                    string pod_id = preferred_pods[i];

                    if (pods.Any(p => p.Value<string>("id") == pod_id))
                    {
                        result_pod = pods.First(p => p.Value<string>("id") == pod_id);
                        break;
                    }
                }

                if (result_pod == null)
                {
                    for (int i = 0; i < preferred_scanners.Count; i++)
                    {
                        string scanner_id = preferred_scanners[i];

                        if (pods.Any(p => p.Value<string>("scanner") == scanner_id))
                        {
                            result_pod = pods.First(p => p.Value<string>("scanner") == scanner_id);
                            break;
                        }
                    }
                }

                if (result_pod == null && pods.Any(p => p.Value<string>("id") != "Input"))
                {
                    result_pod = pods.First(p => p.Value<string>("id") != "Input");
                }
                //var input_pod = preferred_pods.First(pods.Any(p => p.Value<string>("id") == "Input");
                var input_pod = pods.First(p => p.Value<string>("id") == "Input");

                result = string.Format("10{0} = 12{1}", input_pod["subpods"].First.Value<string>("plaintext"), result_pod["subpods"].First.Value<string>("plaintext").Replace("\n", " "));

                if (suggestion != "")
                    result += string.Format(" (suggested as 11did you mean \"{0}\"12)", suggestion);
            }
            catch (Exception ex)
            {
                try
                {
                    result = wa_client.DownloadString(string.Format("https://api.wolframalpha.com/v1/result?appid={0}&i={1}&units=metric", Config.GetString("wolfram.key"), HttpUtility.UrlEncode(query)));
                }
                catch (Exception ex2)
                {
                    SendMessage("[4Wolfram] 4Couldn't display answer", source);
                    Console.WriteLine(ex);
                    Console.WriteLine(ex2);
                    return;
                }
            }

            SendMessage(string.Format("[4Wolfram] {1}", n, result), source);
        }

        public static void WolframPlot(string args, string source, string n)
        {
            List<string> preferred_pods = new List<string>()
            {
                "Plot",
                "Result",
            };

            List<string> preferred_scanners = new List<string>()
            {
                "Plotter"
            };

            if (args.StartsWith(">plot"))
                args = args.Substring(">plot".Length).Trim();
            else
                args = args.Substring(".waplot".Length).Trim();

            string query = args;
            string result = "";

            string suggestion = "";

            try
            {
                string response = MakeWolframQuery(query, PrefersMetric(n), true);
                var obj = JObject.Parse(response);

                var queryresult = obj["queryresult"];

                if (!queryresult.Value<bool>("success")) // check for did you means
                {
                    if (queryresult["didyoumeans"] != null)
                    {
                        if (queryresult["didyoumeans"].Type == JTokenType.Array)
                        {
                            suggestion = queryresult["didyoumeans"].OrderByDescending(p => p.Value<double>("score")).First().Value<string>("val");
                        }
                        else
                            suggestion = queryresult["didyoumeans"].Value<string>("val");

                        response = MakeWolframQuery(suggestion, PrefersMetric(n), true);
                        obj = JObject.Parse(response);
                        queryresult = obj["queryresult"];
                    }
                    else
                        throw new Exception();
                }

                var pods = queryresult["pods"];

                JToken result_pod = null;

                for (int i = 0; i < preferred_pods.Count; i++)
                {
                    string pod_id = preferred_pods[i];

                    if (pods.Any(p => p.Value<string>("id") == pod_id))
                    {
                        result_pod = pods.First(p => p.Value<string>("id") == pod_id);
                        break;
                    }
                }

                if (result_pod == null)
                {
                    for (int i = 0; i < preferred_scanners.Count; i++)
                    {
                        string scanner_id = preferred_scanners[i];

                        if (pods.Any(p => p.Value<string>("scanner") == scanner_id))
                        {
                            result_pod = pods.First(p => p.Value<string>("scanner") == scanner_id);
                            break;
                        }
                    }
                }

                if (result_pod == null && pods.Any(p => p.Value<string>("id") != "Input"))
                {
                    result_pod = pods.First(p => p.Value<string>("id") != "Input");
                }
                //var input_pod = preferred_pods.First(pods.Any(p => p.Value<string>("id") == "Input");
                var input_pod = pods.First(p => p.Value<string>("id") == "Input");

                result = string.Format("plot(10{0}) = {1}", input_pod["subpods"].First.Value<string>("plaintext"), result_pod["subpods"].First["img"].Value<string>("src").Replace("\n", " "));

                if (suggestion != "")
                    result += string.Format(" (suggested as 11did you mean \"{0}\"12)", suggestion);
            }
            catch (Exception ex)
            {
                //try
                //{
                //    result = wa_client.DownloadString(string.Format("https://api.wolframalpha.com/v1/result?appid={0}&i={1}&units=metric", Config.GetString("wolfram.key"), HttpUtility.UrlEncode(query)));
                //}
                //catch (Exception ex2)
                //{
                    SendMessage("[4Wolfram] 4Couldn't display answer", source);
                    Console.WriteLine(ex);
                    return;
                //}
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
            string real_source = source;
            source = GetSource(source);

            if(RemindManager.SeenTrackedNicks.Contains(n))
                if (RemindManager.SeenTracker.Any(s => s.Key.Nick == n))
                    foreach (var p in RemindManager.SeenTracker.Where(s => s.Key.Nick == n))
                        p.Value.Set();

            if (false && ImageSearch.Random.NextDouble() < (1d / Config.GetDouble("skynet.chance")) && !Config.Contains("skynet.disabled", source))
            {
                var tuple = YoutubeUtils.GetComment(args, source, n);
                string comment = tuple.Item2;

                if (comment.Length > 0)
                {
                    SendMessage(comment, real_source);

                    if (!CommentSources.ContainsKey(source))
                        CommentSources[source] = new List<string>();

                    CommentSources[source].Add(string.Format("{0} | {1}", tuple.Item1, comment));
                }
            }

            if (args.Contains("spurdo"))
            {
                SendMessage(string.Format("{0}, s/spurdo/spürdo/gi", n), real_source);
            }

            if (args.StartsWith("."))
            {
                var ticker = args.Substring(1);
                var first = ticker.Split(' ')[0];

                if (CryptoHandler.Tickers.Contains(first.ToUpper()))
                {
                    var rest = ticker.Split(' ').Skip(1).Select(t => t.Trim()).ToArray();

                    //Console.WriteLine("\"{0}\"", rest[0]);

                    if (rest.Any() && CryptoHandler.LooksLikeAddress(rest[0]))
                    {
                        try
                        {
                            SendMessage(CryptoHandler.GetAddressInfo(rest[0]), real_source);
                            goto crypto_end;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                    }

                    if (rest.Any() && CryptoHandler.LooksLikeTxid(rest[0]))
                    {
                        try
                        {
                            SendMessage(CryptoHandler.GetTransactionInfo(rest[0]), real_source);
                            goto crypto_end;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                    }

                    if(rest.Any() && (rest[0] == "block" || rest[0] == "latest" || rest[0] == "latestblock" || rest[0] == "last" || rest[0] == "lastblock"))
                    {
                        try
                        {
                            SendMessage(CryptoHandler.GetLastBlockInfo(), real_source);
                            goto crypto_end;
                        }
                        catch
                        {

                        }
                    }

                    if(rest.Any() && (rest[0].ToLower() == "btc"))
                    {
                        ExchangeRate(string.Format(".exc 1 {0} to BTC", first), real_source, n);
                        goto crypto_end;
                    }

                    double amount = 1;
                    bool to_usd = true;
                    bool success = false;

                    for (int i = 0; i < rest.Length; i++)
                    {
                        if (rest[i].StartsWith("$") && (success = double.TryParse(rest[i].Substring(1), out amount)))
                        {
                            to_usd = false;
                            break;
                        }
                        else if ((success = double.TryParse(rest[i], out amount)))
                            break;
                    }

                    if (amount == 0)
                        amount = 1;

                    if (to_usd)
                        ExchangeRate(string.Format(".exc {0} {1} to USD", amount, first), real_source, n);
                    else
                        ExchangeRate(string.Format(".exc {0} USD to {1}", amount, first), real_source, n);
                }
            }

            crypto_end:

            var tells = TellManager.GetTells(n);

            if (!tells.Any())
                return;

            foreach (var tell in tells)
            {
                SendMessage(tell.ToString(), real_source);
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

                bool cache_hit = false;

                if (cache_hit = summary.EndsWith("(cache hit)"))
                {
                    summary = summary.Substring(0, summary.Length - "(cache hit)".Length);
                }

                sw.Stop();

                SendMessage(string.Format("{0} ({1}s{2})", summary, sw.Elapsed.TotalSeconds.ToString("0.00"), cache_hit ? "-cache" : ""), source);
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

                bool cache_hit = false;

                if (cache_hit = summary.EndsWith("(cache hit)"))
                {
                    summary = summary.Substring(0, summary.Length - "(cache hit)".Length);
                }

                sw.Stop();

                SendMessage(string.Format("{0} ({1}s{2})", summary, sw.Elapsed.TotalSeconds.ToString("0.00"), cache_hit ? "-cache" : ""), source);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}

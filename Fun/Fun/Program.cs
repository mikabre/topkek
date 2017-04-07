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

namespace Fun
{
    public delegate void MessageHandler(string args, string source, string nick);
    class Program : ExampleModule
    {
        static QuoteManager Quotes;

        public static YoutubeResolver YoutubeResolver = new YoutubeResolver();
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

            string result = YoutubeResolver.Search(args);

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

        static void ImageSearch(string args, string source, string n)
        {
            bool random = args.StartsWith(".ir");

            args = args.Substring(".im".Length).Trim();
            string result = Fun.ImageSearch.SearchImages(args, random, false);

            if (result != "-")
                SendMessage(string.Format("Found {0}: {1}", args, result), source);
        }

        static void GifSearch(string args, string source, string n)
        {
            //bool random = args.StartsWith(".gif");

            args = args.Substring(".gif".Length).Trim();
            string result = Fun.ImageSearch.SearchImages(args, true, true);

            if (result != "-")
                SendMessage(string.Format("Found {0}: {1}", args, result), source);
        }

        static void TumblrSearch(string args, string source, string n)
        {
            bool random = args.StartsWith(".tr");

            args = args.Substring(".tu".Length).Trim();
            string result = Fun.ImageSearch.SearchImages(args + " site:tumblr.com", random, false);

            if (result != "-")
                SendMessage(string.Format("Found {0}: {1}", args, result), source);
        }

        static void GoogleSearch(string args, string source, string n)
        {
            //bool random = args.StartsWith(".g");

            args = args.Substring(".g".Length).Trim();
            string result = Fun.ImageSearch.SearchLinks(args, false);

            if (result != "-")
                SendMessage(string.Format("Found {0}: {1}", args, result), source);
        }

        static void NoU(string args, string source, string n)
        {
            //SendMessage("no u", source);
        }

        static void Main(string[] args)
        {
            Name = "lynx";

            Quotes = new QuoteManager();
            RuleManager = RuleManager.Load();

            Commands = new Dictionary<string, Osiris.MessageHandler>()
            {
                {"$getid", GetIdentifier },
                {"http", ResolveLink },
                {"no u", NoU },
                {"!quote", ManageQuotes },
                {"$london", SquareLondon },
                {"!london", SquareLondon },
                {"!youtube", YoutubeSearch },
                {"!yt", YoutubeSearch },
                {"!rules", Rules },
                {"$links", Enable },
                {"$getcache", GetCache },
                {"$setcache", SetCache },
                {"!im", ImageSearch },
                {"!ir", ImageSearch },
                {"!gif", GifSearch },
                {"!tu", TumblrSearch },
                {"!tr", TumblrSearch },
                {"!g ", GoogleSearch }
            };
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls; // comparable to modern browsers

            Init(args);
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

                string url = regex.Matches(args)[0].Value;

                var result = await LinkResolver.GetSummary(url);
                string summary = result.Value;

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
    }
}

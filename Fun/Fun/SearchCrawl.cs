using Osiris;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Fun
{
    public class ImageSearch
    { 
        public static WebClient Client = new WebClient();
        public static Random Random = new Random();

        public static string SearchTwitter(string text)
        {
            return "";
        }

        public static string SearchImages(string text, bool random, bool gif)
        {
            string id = GetCacheString(text, gif ? "gif" : "im");
            var item = LinkResolver.Cache.Get(id);

            if (item != null)
            {
                var temp_list = item.Content.Split('\n');

                if (random)
                {
                    int rand = Random.Next(temp_list.Length);
                    Console.WriteLine("random pick: {0} out of {1}", rand, temp_list.Length);
                    return temp_list[Random.Next(temp_list.Length)];
                }
                else
                {
                    return temp_list[0];
                }
            }

            Console.WriteLine("Cache miss");
            Client.Headers["User-Agent"] = Config.GetString("search.useragent");

            string response = Client.DownloadString(BuildQuery(text, true, gif));
            var matches = Regex.Matches(response, "\"ou\":\"(http.*?)\",\"ow\"");
            var list = matches.Cast<Match>().Where(m => m.Groups.Count >= 2 && !m.Groups[1].Value.Contains("ggpht")).Select(m => Regex.Replace(
    m.Groups[1].ToString(),
    @"\\[Uu]([0-9A-Fa-f]{4})",
    k => char.ToString(
        (char)ushort.Parse(k.Groups[1].Value, NumberStyles.AllowHexSpecifier))) ).ToList();

            string res = string.Join("\n", list);

            Console.WriteLine(list.Count);

            if (list.Count != 0)
                LinkResolver.Cache.Add(id, res, TimeSpan.FromSeconds(10));
            else
                return "-";

            if (random)
            {
                return list[Random.Next(list.Count)];
            }
            else
            {
                return list[0];
            }
        }

        public static string SearchLinks(string text, bool random)
        {
            string id = GetCacheString(text, "urls");
            var item = LinkResolver.Cache.Get(id);

            if (item != null)
            {
                var temp_list = item.Content.Split('\n');

                if (random)
                {
                    return temp_list[Random.Next(temp_list.Length)];
                }
                else
                {
                    return temp_list[0];
                }
            }

            Console.WriteLine("Cache miss");
            Client.Headers["User-Agent"] = Config.GetString("search.useragent");

            string response = Client.DownloadString(BuildQuery(text, false, false));
            var matches = Regex.Matches(response, "href=\"(http.*?)\"");
            var list = matches.Cast<Match>().Where(m => m.Groups.Count >= 2 && !m.Groups[1].Value.Contains("webcache.googleusercontent.com") && !m.Groups[1].Value.Contains("google.com") && !m.Groups[1].Value.Contains("google.nl")).Select(m => m.Groups[1].ToString()).ToList();

            string res = string.Join("\n", list);

            Console.WriteLine(list.Count);
            if (list.Count != 0)
                LinkResolver.Cache.Add(id, res, TimeSpan.FromSeconds(10));
            else
                return "-";

            if (random)
            {
                return list[Random.Next(list.Count)];
            }
            else
            {
                return list[0];
            }
        }

        public static string SearchLinksDdg(string text, bool random)
        {
            string id = GetCacheString(text, "urls-ddg");
            var item = LinkResolver.Cache.Get(id);

            if (item != null)
            {
                var temp_list = item.Content.Split('\n');

                if (random)
                {
                    return temp_list[Random.Next(temp_list.Length)];
                }
                else
                {
                    return temp_list[0];
                }
            }

            Console.WriteLine("Cache miss");
            Client.Headers["User-Agent"] = Config.GetString("search.useragent");

            string response = Client.DownloadString(string.Format("https://duckduckgo.com/html?q={0}", WebUtility.UrlEncode(text)));
            var matches = Regex.Matches(response, "<a rel=\"nofollow\" href=\"(http.*?)\"");
            var list = matches.Cast<Match>().Where(m => m.Groups.Count >= 2 && !m.Groups[1].Value.Contains("/feedback.html")).Select(m => m.Groups[1].ToString()).Distinct().ToList();

            string res = string.Join("\n", list);

            Console.WriteLine(list.Count);
            if (list.Count != 0)
                LinkResolver.Cache.Add(id, res, TimeSpan.FromSeconds(10));
            else
                return "-";

            if (random)
            {
                return list[Random.Next(list.Count)];
            }
            else
            {
                return list[0];
            }
        }

        public static string BuildQuery(string text, bool im, bool gif)
        {
            if (im)
                return string.Format("http://google.com/search?hl=en&tbm=isch&q={0}{1}", WebUtility.UrlEncode(text), gif ? "&tbs=itp:animated" : "");
            else
                return string.Format("http://google.com/search?hl=en&q={0}", WebUtility.UrlEncode(text));
        }

        public static string GetCacheString(string query, string type = "im")
        {
            return string.Format("?search:{0}:{1}", query, type);
        }
    }
}

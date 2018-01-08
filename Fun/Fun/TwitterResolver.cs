using Osiris;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Tweetinvi;
using Tweetinvi.Core;
using Tweetinvi.Models;

namespace Fun
{
    public class TwitterResolver
    {
        TwitterCredentials Credentials { get; set; }
        Random Random { get; set; }

        public TwitterResolver()
        {
        }

        public void Init()
        {
            Credentials = new TwitterCredentials(Config.GetString("twitter.consumer.key"), Config.GetString("twitter.consumer.secret"), Config.GetString("twitter.user.token"), Config.GetString("twitter.user.secret"));
            Console.WriteLine("Setting up Twitter credentials as:");

            Console.WriteLine("\t{0}", Credentials.ConsumerKey);
            Console.WriteLine("\t{0}", Credentials.ConsumerSecret);
            Console.WriteLine("\t{0}", Credentials.AccessToken);
            Console.WriteLine("\t{0}", Credentials.AccessTokenSecret);

            Auth.SetCredentials(Credentials);
            Random = new Random();

            TweetinviConfig.CurrentThreadSettings.TweetMode = TweetMode.Extended;
            TweetinviConfig.ApplicationSettings.TweetMode = TweetMode.Extended;
        }

        public string GetSearchResult(string query)
        {
            try
            {
                TweetinviConfig.CurrentThreadSettings.TweetMode = TweetMode.Extended;
                TweetinviConfig.ApplicationSettings.TweetMode = TweetMode.Extended;
                string cache_id = "twitter-search:" + query.ToLower().Trim();

                var item = LinkResolver.Cache.Get(cache_id);

                if(item != null)
                {
                    var items = item.Content.Split('\n');
                    return items[Random.Next(items.Length)];
                }

                Func<ITweet, string> transform = (t) => { return string.Format("11Tweet from {0}: \"{1}\" | {2}", t.CreatedBy.Name, t.FullText.Replace("\n", " "), t.Url); };

                if (Config.GetString("twitter.output") == "classic")
                    transform = (t) => { return t.FullText.Replace("\n", " "); };

                var results = Search.SearchTweets(query).Select(transform).ToList();
                LinkResolver.Cache.Add(cache_id, string.Join("\n", results), TimedCache.DefaultExpiry);
                
                return results[Random.Next(results.Count)];
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return null;
        }
    }
}

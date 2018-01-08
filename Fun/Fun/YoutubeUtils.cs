using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using System.Xml;
using Osiris;
using System.IO;

using OpenNLP.NET.PoC;

namespace Fun
{
    public static class YoutubeUtils
    {
        public static YouTubeService Service;
        public static PosNounPhraseParser Adapter;
        public static StreamWriter MemeLog;

        public static bool IsYouTubeLink(string url)
        {
            return
                url.StartsWith("http://youtube.com") ||
                url.StartsWith("https://youtube.com") ||
                url.StartsWith("http://www.youtube.com") ||
                url.StartsWith("https://www.youtube.com") ||
                url.StartsWith("http://youtu.be") ||
                url.StartsWith("https://youtu.be");
        }

        public static string GetVideoID(string url)
        {
            if (url.Contains("youtu.be") && !url.Contains("feature"))
            {
                string[] parts = url.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length < 3)
                    return "-";

                if (parts[2].Contains('?'))
                    return parts[2].Split('?')[0];

                return parts[2];
            }
            else
            {
                string[] parts = url.Split(new[] { "v=" }, StringSplitOptions.RemoveEmptyEntries);

                if (!parts.Any())
                    return "-";

                if (parts[1].Contains("&"))
                    return parts[1].Split('&')[0];

                return parts[1];
            }
        }

        public static void LoadKeys()
        {
            Service = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = Config.GetString("youtube.key"),
                ApplicationName = Config.GetString("youtube.appname")
            });

            //Adapter = new PosNounPhraseParser("./models");
            //Adapter.WarmUpModels();

            MemeLog = new StreamWriter("./privacy-meme.log", true);
        }

        public static string Search(string search)
        {
            if (Service == null)
                LoadKeys();

            var req = Service.Search.List("snippet");
            req.Q = search;

            var response = req.Execute();

            string id = "";

            foreach (var item in response.Items)
            {
                if (item.Kind == "youtube#searchResult" && item.Id.Kind == "youtube#video")
                {
                    id = item.Id.VideoId;
                    break;
                }
            }

            if (id != "")
            {
                return GetSummary(id).Replace("You4Tube livestream:", "You4Tube video:").Replace("You4Tube video:", string.Format("https://youtu.be/{0} |", id));
            }

            return "-";
        }

        public static string GetSummary(string ID)
        {
            if (ID.StartsWith("http"))
                ID = GetVideoID(ID);

            if (Service == null)
                LoadKeys();

            try
            {
                var req = Service.Videos.List("snippet,statistics,contentDetails,liveStreamingDetails");
                req.Id = ID;

                var response = req.Execute();

                string duration = "";
                string name = "";
                string uploader = "";
                string likes = "";
                string dislikes = "";
                string views = "";
                string uploaded = "";

                bool live = false;
                bool success = false;

                foreach (var item in response.Items)
                {
                    if (item.Kind == "youtube#video")
                    {
                        try
                        {
                            duration = XmlConvert.ToTimeSpan(item.ContentDetails.Duration.Trim()).ToString("hh\\:mm\\:ss");
                            name = item.Snippet.Title;
                            uploader = item.Snippet.ChannelTitle;
                            likes = ((ulong)item.Statistics.LikeCount).ToString("N0");
                            dislikes = ((ulong)item.Statistics.DislikeCount).ToString("N0");
                            views = ((ulong)item.Statistics.ViewCount).ToString("N0");
                            uploaded = ((DateTime)item.Snippet.PublishedAt).ToShortDateString();
                            if (item.Snippet.LiveBroadcastContent != "none")
                            {
                                live = true;
                                views = ((ulong)item.LiveStreamingDetails.ConcurrentViewers).ToString("N0");
                                uploaded = NiceString(DateTime.Now - (DateTime)item.LiveStreamingDetails.ActualStartTime);
                            }
                        }
                        catch
                        {

                        }

                        success = true;
                        break;
                    }
                }

                if (!success)
                    return "-";

                if (live)
                    return string.Format(
                    "You4Tube livestream: \"{0}\" | Started by 11{1} {2} ago | {3} watching | 3{4} likes/4{5} dislikes",
                    name, uploader, uploaded, views, likes, dislikes);

                return string.Format(
                    "You4Tube video: \"{0}\" | Uploaded by 11{1} on {2} | {3} long | {4} views | 3{5} likes/4{6} dislikes",
                    name, uploader, uploaded, duration, views, likes, dislikes);
            }
            catch
            {
                return "-";
            }
        }

        public static Tuple<string, string> GetComment(string raw, string source, string nick)
        {
            if (Service == null)
                LoadKeys();

            var phrases = Adapter.GetNounPhrases(raw);

            if (!phrases.Any())
                return new Tuple<string, string>("", "");

            var longest = phrases.OrderByDescending(p => p.Length).First().Trim();

            if (longest.Length == 0)
                return new Tuple<string, string>("", "");

            MemeLog.WriteLine("{0} [{1}] <{2}> {3} | Translated into: \"{4}\"", DateTime.UtcNow.ToString(), source, nick, raw, longest);
            MemeLog.Flush();

            var request = Service.Search.List("snippet");
            request.Q = longest;

            var response = request.Execute();

            string id = "";

            foreach (var item in response.Items)
            {
                if (item.Kind == "youtube#searchResult" && item.Id.Kind == "youtube#video")
                {
                    id = item.Id.VideoId;
                    break;
                }
            }

            if (id == "")
                return new Tuple<string, string>("", "");

            var req = Service.CommentThreads.List("snippet");
            req.VideoId = id;
            req.TextFormat = CommentThreadsResource.ListRequest.TextFormatEnum.PlainText;
            
            var resp = req.Execute();
            var comment = resp.Items[ImageSearch.Random.Next(resp.Items.Count)].Snippet.TopLevelComment;

            return new Tuple<string, string>(string.Format("https://youtube.com/watch?v={0}&lc={1}", id, comment.Id), comment.Snippet.TextDisplay.ToString());
        }

        public static string ConditionalPlural(double val, string noun)
        {
            int c = (int)val;

            if (c == 1)
                return c.ToString() + " " + noun;

            return c.ToString() + " " + noun + "s";
        }

        public static string NiceString(TimeSpan span)
        {
            if (span.TotalDays > 1)
                return ConditionalPlural(span.TotalDays, "day");

            if (span.TotalHours > 1)
                return ConditionalPlural(span.TotalHours, "hour");

            if (span.TotalMinutes > 1)
                return ConditionalPlural(span.TotalMinutes, "minute");

            if (span.TotalSeconds > 1)
                return ConditionalPlural(span.TotalSeconds, "second");

            return span.ToString();
        }

        public static TimeSpan FuckingRetardedStandards(string iso)
        {
            // "PT3M44S"

            TimeSpan ret = new TimeSpan();

            iso = iso.Replace("PT", "");

            var parts = iso.Split(new[] { 'H', 'M', 'S' }, StringSplitOptions.RemoveEmptyEntries);

            parts = parts.Reverse().ToArray();

            return new TimeSpan((parts.Length > 2 ? int.Parse(parts[2]) : 0), (parts.Length > 1 ? int.Parse(parts[1]) : 0), int.Parse(parts[0]));
        }
    }
}
namespace System.Runtime.CompilerServices
{
    public class ExtensionAttribute : Attribute { }
}
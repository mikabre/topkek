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

namespace Fun
{
    public class YoutubeResolver
    {
        public YouTubeService Service = new YouTubeService(new BaseClientService.Initializer()
        {
            ApiKey = Config.GetString("youtube.key"),
            ApplicationName = Config.GetString("youtube.appname")
        });

        public YoutubeResolver()
        {

        }

        public string Search(string search)
        {
            var req = Service.Search.List("snippet");
            req.Q = search;

            var response = req.Execute();

            string id = "";

            foreach (var item in response.Items)
            {
                if (item.Kind == "youtube#searchResult")
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

        public string GetSummary(string ID)
        {
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
                            likes = ((ulong)item.Statistics.LikeCount).ToString("##,#");
                            dislikes = ((ulong)item.Statistics.DislikeCount).ToString("##,#");
                            views = ((ulong)item.Statistics.ViewCount).ToString("##,#");
                            uploaded = ((DateTime)item.Snippet.PublishedAt).ToShortDateString();
                            if (item.Snippet.LiveBroadcastContent != "none")
                            {
                                live = true;
                                views = ((ulong)item.LiveStreamingDetails.ConcurrentViewers).ToString("##,#");
                            }
                        }
                        catch
                        {

                        }

                        success = true;
                    }
                }

                if (!success)
                    return "-";

                if (live)
                    return string.Format(
                    "You4Tube livestream: \"{0}\" | Started by 11{1} on {2} | {3} watching | 3{4} likes/4{5} dislikes",
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

        public TimeSpan FuckingRetardedStandards(string iso)
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
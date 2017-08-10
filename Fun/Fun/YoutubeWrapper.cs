using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fun
{
    public class YoutubeWrapper : IResolver
    {
        public string Name
        {
            get
            {
                return "youtube";
            }
        }

        public YoutubeWrapper()
        {
            YoutubeUtils.LoadKeys();
        }

        public bool Matches(string URL)
        {
            return YoutubeUtils.IsYouTubeLink(URL);
        }

        public string GetCacheID(string URL)
        {
            return "youtube:" + YoutubeUtils.GetVideoID(URL);
        }

        public string GetSummary(string URL)
        {
            return YoutubeUtils.GetSummary(URL);
        }

        public bool Ready(string URL)
        {
            return YoutubeUtils.Service != null;
        }
    }
}

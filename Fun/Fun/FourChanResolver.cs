using SharpChannel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fun
{
    public class FourChanResolver : IResolver
    {
        public Dictionary<string, Board> Boards = new Dictionary<string, Board>();

        public string Name
        {
            get
            {
                return "4chan";
            }
        }

        public FourChanResolver()
        {
            EndpointManager.DefaultProvider = new FourEndpointProvider();
        }

        public Board GetBoard(string board_id, bool delay = true)
        {
            board_id = board_id.ToLower();

            if (Boards.ContainsKey(board_id))
                return Boards[board_id];

            Board board = new Board(board_id);

            Boards.Add(board_id, board);

            Task.Factory.StartNew(delegate
            {
                if (delay)
                    System.Threading.Thread.Sleep(1000);

                board.Update();
                //board.AutoUpdateInterval = 600000;
                //board.StartAutoUpdate();

            });

            //if (!delay)
            //    System.Threading.Thread.Sleep(100);

            return board;
        }

        public bool Ready(string url)
        {
            return true;
            //Uri uri = new Uri(url);
            //var parts = uri.AbsolutePath.Split('/');
            //var board_id = parts[1];

            //Console.Write("Testing readiness for board {0}: ", board_id);

            //var ret = !GetBoard(board_id, true).Updating;
            //Console.WriteLine(ret);
            //return ret;
        }

        public bool Matches(string url)
        {
            url = url.ToLower();

            Uri uri = new Uri(url);
            var parts = uri.AbsolutePath.Split('/');

            if (parts.Length < 4)
                return false;

            if (!int.TryParse(parts[3], out int id))
                return false;

            return url.StartsWith("http://boards.4chan.org") ||
                url.StartsWith("https://boards.4chan.org");
        }

        public string GetCacheID(string url)
        {
            url = url.ToLower();

            Uri uri = new Uri(url);
            return "4chan:" + uri.AbsolutePath + uri.Fragment;
        }

        public static string Italics(int str, int italics)
        {
            if (italics == 0)
                return str.ToString();
            return Osiris.Utilities.ITALICS + str.ToString() + Osiris.Utilities.ITALICS;
        }

        static string Layout = "034chan thread: {0} posted on 07/{1}/ | {2} | {3} | {4} | Posted {5} ago{6}";
        string post_layout = "034chan post: {0} posted on thread {1} on 07/{2}/ | {3} | Posted {4} ago{5}";

        public string GetSummary(string url)
        {
            url = url.ToLower();
            var uri = new Uri(url);
            string path = uri.AbsolutePath;
            var post_str = uri.Fragment;
            Console.WriteLine(url);
            Console.WriteLine(uri.Fragment);

            int post_id = -1;
            if(post_str.Length > 0)
                int.TryParse(post_str.Substring(2), out post_id);

            var parts = path.Split('/');
            string board_id = parts[1];
            string id_str = parts[3];

            if (!int.TryParse(id_str, out int id))
                return "-";

            var board = GetBoard(board_id);

            if(board.Threads.Count == 0 || board.Threads.All(p => p.ID != id))
            {
                var thread = new Thread(id, board);
                thread.Update();

                board.Threads.Add(thread);
            }

            for(int i = 0; i < board.Threads.Count; i++)
            {
                var thread = board.Threads[i];

                if (thread.ID == id)
                {
                    if(post_id > -1)
                    {
                        try
                        {
                            var post = thread.Posts.First(p => p.ID == post_id);
                            int replies = thread.Posts.Where(p => p.Comment.Contains(">>" + post.ID)).Count();

                            return string.Format(post_layout,
                                FourChanImageResolver.Bold(post.SmartSubject) ?? "(no subject)",
                                FourChanImageResolver.Bold(thread.OP.SmartSubject) ?? "(no subject)",
                                board_id,
                                Utilities.BetterPlural(replies, "reply", 0, 8),
                                Utilities.TimeSpanToPrettyString(DateTime.UtcNow - post.PostTime),
                                post.Filename > 0 ? string.Format(" | File: {0}", post.FileUrl) : "");
                        }
                        catch
                        {

                        }
                    }

                    return string.Format(Layout, 
                        FourChanImageResolver.Bold(thread.OP.SmartSubject) ?? "(no subject)", 
                        board_id,
                        Utilities.BetterPlural(thread.OP.Replies, "reply", thread.OP.BumpLimit, 8),
                        Utilities.BetterPlural(thread.OP.Images, "image", thread.OP.ImageLimit, 3),
                        Utilities.BetterPlural(thread.OP.Posters, "poster", 0, 11),
                        Utilities.TimeSpanToPrettyString(DateTime.UtcNow - thread.OP.PostTime),
                                thread.OP.Filename > 0 ? string.Format(" | File: {0}", thread.OP.FileUrl) : "");
                }
            }

            throw new Exception("nope");
        }
    }

    public class FourChanImageResolver : IResolver
    {
        public FourChanResolver Parent { get; set; }

        public string Name { get { return "4chan-img"; } }

        public bool Matches(string url)
        {
            url = url.ToLower();

            Uri uri = new Uri(url);
            var parts = uri.AbsolutePath.Split('/');

            if (parts.Length < 3)
                return false;

            if (!ulong.TryParse(parts[2].Split('.')[0], out ulong id))
                return false;

            return url.StartsWith("http://i.4cdn.org") ||
                url.StartsWith("https://i.4cdn.org");
        }

        public bool Ready(string url)
        {
            Uri uri = new Uri(url);
            var parts = uri.AbsolutePath.Split('/');
            var board_id = parts[1];

            Console.Write("Testing readiness for board {0}: ", board_id);

            var ret = Parent.GetBoard(board_id, false).WaitUntilUpdated(0);
            Console.WriteLine(ret);
            return ret;
        }

        public string GetCacheID(string url)
        {
            url = url.ToLower();

            Uri uri = new Uri(url);
            return "4chan-img:" + uri.AbsolutePath;
        }

        public static string Bold(string str)
        {
            if (str == null)
                return null;

            return "\"" + Osiris.Utilities.BOLD + str + Osiris.Utilities.BOLD + "\"";
        }

        public string GetSummary(string url)
        {
            string layout = "034chan post: {0} posted on thread {1} on 07/{2}/ | {3} | Posted {4} ago | {5}";
            string op_layout = "034chan thread: {0} posted on 07/{1}/ | {2} | {3} | {4} | Posted {5} ago | {6}";

            Uri uri = new Uri(url);
            string path = uri.AbsolutePath;

            string board_id = path.Split('/')[1];
            string filename = path.Split('/')[2].Split('.')[0];

            if (!ulong.TryParse(filename, out ulong tim))
                throw new Exception("Couldn't parse filename");

            var board = Parent.GetBoard(board_id, false);

            if (!board.WaitUntilUpdated(1000))
                throw new Exception("Couldn't get board");

            for(int i = 0; i < board.Threads.Count; i++)
            {
                var thread = board.Threads[i];

                if (thread.OP.Filename == tim)
                {
                    return string.Format(
                        op_layout,
                        Bold(thread.OP.SmartSubject) ?? "(no subject)",
                        board_id,
                        Utilities.BetterPlural(thread.OP.Replies, "reply", thread.OP.BumpLimit, 8),
                        Utilities.BetterPlural(thread.OP.Images, "image", thread.OP.ImageLimit, 3),
                        Utilities.BetterPlural(thread.OP.Posters, "poster", 0, 11),
                        Utilities.TimeSpanToPrettyString(DateTime.UtcNow - thread.OP.PostTime),
                        string.Format("https://boards.4chan.org/{0}/thread/{1}", board_id, thread.ID));
                }

                for (int k = 0; k < thread.Posts.Count; k++)
                {
                    var post = thread.Posts[k];

                    if(post.Filename == tim && k > 0)
                    {
                        if (k > 0)
                        {
                            int replies = thread.Posts.Where(p => p.Comment.Contains(">>" + post.ID)).Count();

                            return string.Format(
                                layout,
                                Bold(post.SmartSubject) ?? "(no subject)",
                                Bold(thread.OP.SmartSubject) ?? "(no subject)",
                                board_id,
                                Utilities.BetterPlural(replies, "reply", 0, 8),
                                Utilities.TimeSpanToPrettyString(DateTime.UtcNow - post.PostTime),
                                string.Format("https://boards.4chan.org/{0}/thread/{1}#p{2}", board_id, thread.ID, post.ID));
                        }
                        else
                        {
                            return string.Format(
                                op_layout,
                                Bold(post.SmartSubject) ?? "(no subject)",
                                board_id,
                                Utilities.BetterPlural(post.Replies, "reply", post.BumpLimit, 8),
                                Utilities.BetterPlural(post.Images, "image", post.ImageLimit, 3),
                                Utilities.BetterPlural(post.Posters, "poster", 0, 11),
                                Utilities.TimeSpanToPrettyString(DateTime.UtcNow - post.PostTime),
                                string.Format("https://boards.4chan.org/{0}/thread/{1}", board_id, thread.ID));
                        }
                    }
                    
                }
            }

            throw new Exception("Couldn't find image");
        }
    }
}

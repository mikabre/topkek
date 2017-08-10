using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Osiris
{
    public static class Utilities
    {
        public static string BOLD = "";
        public static string UNDERLINE = "";
        public static string ITALICS = "";
        public static string COLOR = "";

        public static string Sanitize(string input)
        {
            input = input.Replace("", ""); // bold
            input = input.Replace("", ""); // underline
            input = input.Replace("", ""); // italics

            input = RemoveDuplicateColor(input);

            input = Regex.Replace(input, @"[\x02\x1F\x0F\x16]|\x03(\d\d?(,\d\d?)?)?", String.Empty);

            return input;
        }

        public static string RemoveDuplicateColor(string input)
        {
            string ret = string.Join(COLOR, input.Split(new string[] { COLOR }, StringSplitOptions.RemoveEmptyEntries));
            if (input.StartsWith(COLOR))
                ret = COLOR + ret;
            if (input.EndsWith(COLOR))
                ret = ret + COLOR;

            return ret;
        }

        public static IEnumerable<int> AllIndexesOf(this string str, string searchstring)
        {
            int minIndex = str.IndexOf(searchstring);
            while (minIndex != -1)
            {
                yield return minIndex;
                minIndex = str.IndexOf(searchstring, minIndex + searchstring.Length);
            }
        }

        public static bool HaveConnection()
        {
            try
            {
                Ping ping = new Ping();
                return ping.Send("google.com").Status == IPStatus.Success;
            }
            catch
            {
                return false;
            }
        }
    }
}

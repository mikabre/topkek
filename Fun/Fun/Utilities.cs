using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fun
{
    class Utilities
    {
        public static string Italics(long str, int italics)
        {
            if (italics == 0)
                return str.ToString();
            return Osiris.Utilities.ITALICS + str.ToString() + Osiris.Utilities.ITALICS;
        }

        public static string BetterPlural(double amount, string unit, int italics = 0, int color = -1)
        {
            return BetterPlural((long)amount, unit, italics, color);
        }

        public static string BetterPlural(long amount, string unit, int italics = 0, int color = -1)
        {
            if (amount == 1 || amount == -1)
                return string.Format("{2}{0}{3} {1}", Italics(amount, italics), unit, color > -1 ? "" + color.ToString("00") : "", color > -1 ? "" : "");

            if (unit.EndsWith("y") && !unit.EndsWith("ay"))
                unit = unit.Substring(0, unit.Length - 1) + "ie";
            return string.Format("{2}{0}{3} {1}s", Italics(amount, italics), unit, color > -1 ? "" + color.ToString("00") : "", color > -1 ? "" : "");
        }

        public static string ShortHand(TimeSpan span)
        {
            if (span.TotalHours > 1)
                return string.Format("{0}h", (int)span.TotalHours);

            if (span.TotalMinutes > 1)
                return string.Format("{0}m", (int)span.TotalMinutes);
            
            return string.Format("{0}s", (int)span.TotalSeconds);
        }

        public static string TimeSpanToPrettyString(TimeSpan span)
        {
            Dictionary<string, int> lengths = new Dictionary<string, int>()
            {
                {Utilities.BetterPlural(span.TotalDays / 7, "week"), (int)(span.TotalDays / 7) },
                {Utilities.BetterPlural(span.TotalDays % 7, "day"), (int)(span.TotalDays % 7) },
                {Utilities.BetterPlural(span.TotalHours % 24, "hour"), (int)(span.TotalHours % 24) },
                {Utilities.BetterPlural(span.TotalMinutes % 60, "minute"), (int)(span.TotalMinutes % 60) },
                {Utilities.BetterPlural(span.TotalSeconds % 60, "second"), (int)(span.TotalSeconds % 60) },
            };

            var final = lengths.Where(p => p.Value > 0).Select(p => p.Key).Take(2);

            return string.Join(" ", final);
        }
    }
}

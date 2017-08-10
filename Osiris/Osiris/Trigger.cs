using ChatSharp;
using ConversionTherapy;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Osiris
{
    [Serializable]
    public class Trigger
    {
        public static HomoglyphTable Table = new HomoglyphTable("./glyphs.txt");

        public string MatchString { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public TriggerType TriggerType { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        public TriggerMatchType TriggerMatchType { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        public TriggerResult TriggerResult { get; set; }

        public string Location { get; set; }
        public string ResultString { get; set; }
        public string ID { get; set; }
        public bool Strip { get; set; }
        public bool AsciiOnly { get; set; }
        public bool Insensitive { get; set; }
        public bool StopExecution { get; set; }
        public bool FixHomoglyphs { get; set; }

        public Trigger()
        {
            byte[] rnd = new byte[16];
            Token.Random.GetBytes(rnd);

            ID = BitConverter.ToString(rnd).ToLower().Replace("-", "");
        }

        public string ExecuteIfMatches(IrcMessage msg, IrcClient client)
        {
            try
            {


                string haystack = "";

                if (TriggerType == TriggerType.Raw)
                    haystack = msg.RawMessage;
                else
                    haystack = new PrivateMessage(msg).Message;

                if (Matches(haystack))
                {
                    if (TriggerResult == TriggerResult.Raw)
                        client.SendRawMessage(ResultString);
                    else if (TriggerResult == TriggerResult.Irc)
                        client.SendMessage(ResultString, new PrivateMessage(msg).Source);
                    else if (TriggerResult == TriggerResult.Modify)
                    {
                        string temp = haystack;

                        if (FixHomoglyphs)
                            temp = Table.Purify(temp);
                        if (Strip)
                            temp = Utilities.Sanitize(temp);
                        if (AsciiOnly)
                            temp = new string(temp.Where(c => (char.IsLetterOrDigit(c) || char.IsSymbol(c) || MatchString.Contains(c))).ToArray());

                        switch (TriggerMatchType)
                        {
                            case TriggerMatchType.Contains:
                                return temp.Replace(MatchString, ResultString);
                            case TriggerMatchType.EndsWith:
                                return temp.Substring(0, temp.Length - MatchString.Length) + ResultString;
                            case TriggerMatchType.StartsWith:
                                return ResultString + temp.Substring(MatchString.Length);
                            case TriggerMatchType.Regex:
                                return Regex.Replace(temp, MatchString, m => { return ResultString; }, Insensitive ? RegexOptions.IgnoreCase : RegexOptions.None);
                        }
                    }
                    else if (TriggerResult == TriggerResult.Rewrite)
                        return ResultString;
                }
            }
            catch
            {

            }

            return "";
        }

        public bool Matches(string msg)
        {
            if (FixHomoglyphs)
                msg = Table.Purify(msg);

            if (Insensitive)
                msg = msg.ToLower();

            if (Strip)
                msg = Utilities.Sanitize(msg);

            if (AsciiOnly)
                msg = new string(msg.Where(c => (char.IsLetterOrDigit(c) || char.IsSymbol(c) || MatchString.Contains(c))).ToArray());

            bool matches = false;

            switch (TriggerMatchType)
            {
                case TriggerMatchType.Contains:
                    matches = msg.Contains(MatchString);
                    break;
                case TriggerMatchType.EndsWith:
                    matches = msg.EndsWith(MatchString);
                    break;
                case TriggerMatchType.StartsWith:
                    matches = msg.StartsWith(MatchString);
                    break;
                case TriggerMatchType.Regex:
                    matches = Regex.IsMatch(msg, MatchString);
                    break;
            }

            return matches;
        }
        // %ns - nick of the action source
        // %n

        //public string ParseResultString(string raw, string result)
        //{
        //    IrcMessage msg = new IrcMessage(raw);

        //}

        // 5 bits + 2 bytes + 2 bytes + text

        //public string SerializeTrigger()
        //{
        //    BitArray ba = new BitArray(42);

        //    ba[0] = TriggerType == TriggerType.Irc;

        //    byte match = (byte)TriggerMatchType;

        //    ba[1] = (match & )
        //}

        public static Trigger ParseTrigger(string trigger)
        {
            trigger = trigger.Trim();
            var arr = trigger.Split(';');
            Trigger ret = new Trigger();

            ret.TriggerType = (TriggerType)Enum.Parse(typeof(TriggerType), arr[0].ToLower(), true);
            
            switch(arr[1])
            {
                case "sw":
                case "startswith":
                    ret.TriggerMatchType = TriggerMatchType.StartsWith;
                    break;
                case "ew":
                case "endswith":
                    ret.TriggerMatchType = TriggerMatchType.EndsWith;
                    break;
                case "contains":
                    ret.TriggerMatchType = TriggerMatchType.Contains;
                    break;
                case "regex":
                    ret.TriggerMatchType = TriggerMatchType.Regex;
                    break;
            }

            ret.TriggerResult = (TriggerResult)Enum.Parse(typeof(TriggerResult), arr[2].ToLower(), true);
            ret.MatchString = arr[3];
            ret.ResultString = arr[4];

            if(arr.Length > 5)
            {
                string options = arr[5];
                
                ret.Insensitive = options.Contains("i");
                ret.Strip = options.Contains("s");
                ret.AsciiOnly = options.Contains("a");
                ret.StopExecution = options.Contains("e");
                ret.FixHomoglyphs = options.Contains("h");

                if (ret.Insensitive)
                    ret.MatchString = ret.MatchString.ToLower();
            }

            return ret;
        }

        public override string ToString()
        {
            string ret = "";

            if (TriggerType == TriggerType.Raw)
                ret += "raw IRC message ";
            else
                ret += "IRC message ";

            ret += "that ";

            switch(TriggerMatchType)
            {
                case TriggerMatchType.Contains:
                    ret += "contains ";
                    break;
                case TriggerMatchType.EndsWith:
                    ret += "ends with ";
                    break;
                case TriggerMatchType.StartsWith:
                    ret += "starts with ";
                    break;
                case TriggerMatchType.Regex:
                    ret += "matches regex ";
                    break;
            }

            ret += "\"" + MatchString + "\"";

            return ret;
        }
    }

    public enum TriggerType
    {
        Raw, Irc
    }
    public enum TriggerMatchType
    {
        StartsWith,
        Contains,
        EndsWith,
        Regex
    }
    public enum TriggerResult
    {
        Raw, Irc, Modify, Rewrite
    }
}

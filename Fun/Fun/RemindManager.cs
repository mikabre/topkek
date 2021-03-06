﻿using Osiris;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Fun
{
    public delegate void ReminderDone(Reminder r);

    public class RemindManager
    {
        public static List<Reminder> Reminders = new List<Reminder>();
        public static List<string> SeenTrackedNicks = new List<string>();
        public static Dictionary<Reminder, ManualResetEvent> SeenTracker = new Dictionary<Reminder, ManualResetEvent>(); 
        public static ManualResetEvent Added = new ManualResetEvent(false);
        public static event ReminderDone ReminderDone;

        public static void Load(string path = "./remind")
        {
            if (!File.Exists("./remind"))
                return;

            BinaryFormatter formatter = new BinaryFormatter();
            var stream = File.OpenRead(path);

            try
            {
                Reminders = (List<Reminder>)formatter.Deserialize(stream);
            }
            catch (Exception ex)
            {
                throw;
            }
            finally
            {
                stream.Close();
            }
        }

        public static void Save()
        {
            BinaryFormatter formatter = new BinaryFormatter();
            var stream = File.OpenWrite("./remind.tmp");

            formatter.Serialize(stream, Reminders);

            stream.Close();
            stream = File.OpenRead("./remind.tmp");

            if (formatter.Deserialize(stream) == Reminders)
            {
                throw new Exception("Inconsistent database");
            }
            else
            {
                stream.Close();
                File.Copy("./remind.tmp", "./remind", true);
            }
        }

        public static void Add(DateTime time, string message, string nick, string ptoken)
        {
            Reminder r = new Reminder()
            {
                EndDate = time,
                StartDate = DateTime.Now,
                Nick = nick,
                Token = ptoken,
                Message = message
            };

            Reminders.Add(r);
            Save();
            Added.Set();
        }

        public static void TimingLoop()
        {
            while (true)
            {
                try
                {
                    Reminders.RemoveAll(r => r.GetSpan().TotalSeconds < 0.1);

                    if (Reminders.Any())
                    {
                        var nearest = Reminders.Min(r => r.GetSpan());
                        Console.WriteLine(nearest);
                        Added.BetterWaitOne(nearest);
                    }
                    else
                        Added.WaitOne();

                    Added.Reset();

                    var eligible = Reminders.Where(r => r.GetSpan().TotalSeconds < 2);

                    foreach (var reminder in eligible)
                    {
                        ReminderDone.Invoke(reminder);

                        SeenTracker[reminder] = new ManualResetEvent(false);
                        SeenTrackedNicks.Add(reminder.Nick);

                        Task.Factory.StartNew(delegate
                        {
                            if (!SeenTracker[reminder].WaitOne(Config.GetInt("remind.telldelay")))
                                TellManager.Tell("topkek_2000", reminder.Nick, string.Format("You asked to be reminded of \"{0}\" at {1}, but missed the reminder", reminder.Message, reminder.StartDate));

                            SeenTracker.Remove(reminder);
                            SeenTrackedNicks.Remove(reminder.Nick);
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
        }

        public static DateTime Get(string str)
        {
            var days = new string[]
            {
                "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday",
                "mon", "tue", "wed", "thu", "fri", "sat", "sun"
            };

            var dayofweeks = new DayOfWeek[]
            {
                DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
            };

            str = str.Replace("tomorrow", DateTime.Now.AddDays(1).ToShortDateString());
            str = str.Replace("midnight", "00:00");

            DateTime n = DateTime.Now;
            if (DateTime.TryParse(str, out n))
            {
                return n;
            }
            n = DateTime.Now;

            DateTime base_date = DateTime.Now;

            var words = str.Split(' ').Select(s => s.ToLower()).ToArray();

            for (int i = 0; i < words.Length - 1; i++)
            {
                if (words[i] == "next")
                {
                    if (days.Contains(words[i + 1]))
                    {
                        var day = dayofweeks[(Array.IndexOf(days, words[i + 1]) % 7)];

                        DateTime orig = n;

                        n = n.AddDays(7);

                        while (n.DayOfWeek != day)
                            n = n.AddDays(1);

                        base_date = n;
                        //break;
                    }
                }
                else if (words[i] == "this")
                {
                    if (days.Contains(words[1]))
                    {
                        var day = dayofweeks[(Array.IndexOf(days, words[i + 1]) % 7)];

                        DateTime orig = n;

                        if (n.DayOfWeek == day)
                            n = n.AddDays(1);

                        while (n.DayOfWeek != day)
                            n = n.AddDays(1);

                        base_date = n;
                        //break;
                    }
                }

                int hour = -1;
                int minute = 0;

                if(words[i].EndsWith("am") || words[i].EndsWith("pm"))
                {
                    if (int.TryParse(words[i].Substring(0, words[i].Length - 2), out hour))
                    {
                        if (words[i].EndsWith("pm"))
                            hour += 12;
                    }
                    else
                    {
                        continue;
                    }
                }
                else if(i > 0 && words[i] == "am" || words[i] == "pm")
                {
                    if (int.TryParse(words[i - 1], out hour))
                    {
                        if (words[i].EndsWith("pm"))
                            hour += 12;
                    }
                    else
                        continue;
                }
                else if(words[i].Contains(":"))
                {
                    var fragments = words[i].Split(':');

                    if(fragments.Length == 2)
                    {
                        if(int.TryParse(fragments[0], out int protohour) && protohour <= 23 && protohour >= 0)
                        {
                            if (int.TryParse(fragments[1], out int protominute) && protohour <= 59 && protohour >= 0)
                            {
                                hour = protohour;
                                minute = protominute;
                            }
                        }
                    }
                }

                if(hour != -1)
                    base_date = new DateTime(base_date.Year, base_date.Month, base_date.Day, hour, minute, 0);
            }

            var amount = Parse(str);

            return base_date + amount;
        }

        static TimeSpan Parse(string str)
        {
            var b_d = DateTime.Now;
            var base_date = b_d;
            var words = str.Split(' ');

            Dictionary<string, string> mappings = new Dictionary<string, string>()
            {
                {"y", "year" },
                {"m", "minute" },
                {"s", "second" },
                {"d", "day" },
                {"w", "week" },
                {"h", "hour" }
            };

            for (int i = 0; i < words.Length - 1; i++)
            {
                double amount = 0;
                string unit = words[i + 1].ToLower().TrimEnd('s');

                if (!double.TryParse(words[i], out amount))
                {
                    if (double.TryParse(words[i].Substring(0, words[i].Length - 1), out amount) &&
                        mappings.ContainsKey(words[i][words[i].Length - 1].ToString()))
                    {
                        unit = mappings[words[i][words[i].Length - 1].ToString()];
                    }
                    else
                        continue;
                }

                switch (unit)
                {
                    case "year":
                        while (amount >= 1)
                        {
                            base_date = base_date.AddYears(1);
                            amount -= 1;
                        }

                        base_date = base_date.AddDays(amount * (DateTime.IsLeapYear(base_date.Year) ? 366 : 365));
                        break;
                    case "month":
                        while (amount >= 1)
                        {
                            base_date = base_date.AddMonths(1);
                            amount -= 1;
                        }

                        base_date = base_date.AddDays(amount * DateTime.DaysInMonth(base_date.Year, base_date.Month));
                        break;
                    case "week":
                        base_date = base_date.AddDays(amount * 7);
                        break;
                    case "day":
                        base_date = base_date.AddDays(amount);
                        break;
                    case "hour":
                        base_date = base_date.AddHours(amount);
                        break;
                    case "minute":
                        base_date = base_date.AddMinutes(amount);
                        break;
                    case "second":
                        base_date = base_date.AddSeconds(amount);
                        break;
                    case "millisecond":
                        base_date = base_date.AddMilliseconds(amount);
                        break;
                }

                i++;
            }

            return base_date - b_d;
        }
    }

    [Serializable]
    public class Reminder
    {
        public string Nick { get; set; }
        public string Token { get; set; }
        public string Message { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public Reminder()
        {

        }

        public TimeSpan GetSpan()
        {
            return EndDate - DateTime.Now;
        }
    }

    public static class Extensions
    {
        public static bool BetterWaitOne(this WaitHandle handle, TimeSpan span)
        {
            if(span.TotalMilliseconds < int.MaxValue)
            {
                return handle.WaitOne(span);
            }

            var max_span = new TimeSpan(0, 0, 0, 0, int.MaxValue);

            while(span > max_span)
            {
                span -= max_span;

                if (handle.WaitOne(max_span))
                    return true;
            }

            return handle.WaitOne(span);
        }
    }
}

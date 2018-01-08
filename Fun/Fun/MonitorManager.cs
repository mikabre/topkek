using Exchange;
using Osiris;
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
    public class MonitorManager
    {
        public static List<Monitor> Monitors = new List<Monitor>();
        public static ManualResetEvent Running = new ManualResetEvent(false);

        public static int Interval = 60000;
        public static int TickerHistoryLength = 3600000 / Interval;
        
        public static void MonitorLoop()
        {
            while (true)
            {
                while (!Running.WaitOne(Interval)) ;

                while(Running.WaitOne(Interval))
                {
                    PrintMonitorStatus();
                    Save();
                    Thread.Sleep(Interval);
                }
            }
        }

        static void Load()
        {
            if (!File.Exists("./monitors"))
                return;
            try
            {
                var formatter = new BinaryFormatter();
                using (FileStream fs = new FileStream("./monitors", FileMode.OpenOrCreate))
                {
                    Monitors = (List<Monitor>)formatter.Deserialize(fs);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to load monitors.");
                Console.WriteLine(ex);
            }
        }

        public static void Save()
        {
            try
            {
                var formatter = new BinaryFormatter();
                using (
                FileStream fs = new FileStream("./monitors", FileMode.OpenOrCreate))
                {
                    formatter.Serialize(fs, Monitors);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to save monitors.");
                Console.WriteLine(ex);
            }
        }

        public static void Init()
        {
            Load();

            var thread = new Thread(new ThreadStart(MonitorLoop));
            thread.Start();
        }

        public static void Start()
        {
            Running.Set();
        }

        public static void Stop()
        {
            Running.Reset();
        }

        static TickerData MostFitting(IEnumerable<TickerData> data, TimeSpan span)
        {
            return data.Where(d => DateTime.Now - d.Retrieved > span).OrderBy(d => d.Retrieved).Last();
        }

        public static void PrintMonitorStatus()
        {
            if (!Monitors.Any())
                return;

            List<string> statuses = new List<string>();

            string up_percentage = "03↑{0:0.##}%";
            string down_percentage = "04↓{0:0.##}%";

            string up = "03↑{0}";
            string down = "04↓{0}";

            List<TimeSpan> spans = new List<TimeSpan>()
            {
                new TimeSpan(0, 1, 0),
                new TimeSpan(0, 5, 0),
                new TimeSpan(0, 15, 0),
                new TimeSpan(0, 30, 0),
                new TimeSpan(1, 0, 0),
                new TimeSpan(6, 0, 0)
            };

            foreach (var monitor in Monitors)
            {
                var ticker_data = CryptoHandler.GetCurrentTickerData(CryptoHandler.Bitfinex, monitor.Ticker);

                string unit = monitor.Unit.ToString();

                List<string> historicals = new List<string>();

                foreach (var span in spans)
                {
                    if (monitor.PreviousTickerData.Any(p => DateTime.Now - p.Retrieved > span))
                    {
                        var last = MostFitting(monitor.PreviousTickerData, span);

                        double diff = ticker_data.LastTrade - last.LastTrade;
                        double percentage = ((ticker_data.LastTrade / last.LastTrade) - 1) * 100;

                        var span_local = DateTime.Now - last.Retrieved;

                        historicals.Add(string.Format("{0}: {1} {2}/{3}", Utilities.ShortHand(span_local),
                            monitor.Format(last.LastTrade),
                            diff > 0 ? string.Format(up, monitor.Format(diff)) :
                            string.Format(down, monitor.Format(diff)),
                            percentage > 0 ? string.Format(up_percentage, percentage) :
                            string.Format(down_percentage, percentage)));
                    }
                }

                if (historicals.Count > 2)
                    historicals = historicals.Skip(historicals.Count - 2).ToList();

                string historical_24h = string.Format(", 24h: high 03{0}, low 04{1}", monitor.Format(ticker_data.DailyHigh), monitor.Format(ticker_data.DailyLow));

                string status = string.Format("{0}: {1}{2}{3}",
                    monitor.Ticker,
                    monitor.Format(ticker_data.LastTrade),
                    historicals.Any() ? "(" + string.Join(" / ", historicals) + ")" : "",
                    DateTime.Now.Minute % 30 == 0 ? historical_24h : ""
                    );

                statuses.Add(status);

                monitor.PreviousTickerData.Add(ticker_data);

                if (monitor.PreviousTickerData.Count > TickerHistoryLength)
                    monitor.PreviousTickerData.RemoveAt(0);
            }

            List<string> messages = new List<string>();
            List<string> temp = new List<string>();

            foreach(var status in statuses)
            {
                if(temp.Sum(s => s.Length) + status.Length > 450)
                {
                    messages.Add(string.Join(" // ", temp));
                    temp.Clear();
                }

                Console.WriteLine(status);
                temp.Add(status);
            }

            if (temp.Any())
                messages.Add(string.Join(" // ", temp));

            if (messages.Count > 1)
            {
                for (int i = 0; i < messages.Count; i++)
                    messages[i] += string.Format("({0}/{1})", i + 1, messages.Count);
            }
            
            foreach(var message in messages)
            {
                Program.SendMessage(message, Config.GetString("monitor.ptoken"));
            }
        }
    }

    [Serializable]
    public class Monitor
    {
        public Ticker Ticker { get; set; }
        public TickerUnit Unit { get; set; }
        public int Precision { get; set; }

        public List<TickerData> PreviousTickerData = new List<TickerData>();
        public List<Alert> Alerts = new List<Alert>();

        public Monitor()
        {

        }

        public string Format(double price)
        {
            switch(Unit)
            {
                case TickerUnit.Bitcoin:
                    return price.ToString("0." + new string('0', Precision)) + "BTC";
                case TickerUnit.Satoshi:
                    return ((int)(price * 100000000d)).ToString() + "sat";
                case TickerUnit.Cent:
                    return "¢" + (price * 100d).ToString("0." + new string('0', Precision));
                case TickerUnit.Dollar:
                    return "$" + price.ToString("0." + new string('0', Precision));
                default:
                    return price.ToString("0." + new string('0', Precision));
            }
        }
    }

    public enum TickerUnit
    {
        Dollar,
        Cent,
        Bitcoin,
        Satoshi
    }
}

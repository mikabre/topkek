using Exchange;
using Osiris;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace Fun
{
    public class AlertManager
    {
        public static List<Alert> Alerts = new List<Alert>();
        public static Dictionary<Ticker, List<Alert>> AlertsByTicker = new Dictionary<Ticker, List<Alert>>();

        public static void Init()
        {
            Load();
            RegisterTickers();
            CryptoHandler.Bitfinex.OnConnect += (b) => { RegisterTickers(); };
            CryptoHandler.Bitfinex.OnTickerUpdateReceived += HandleTickerUpdate;
        }

        private static void HandleTickerUpdate(IExchange instance, TickerData data)
        {
            if (!AlertsByTicker.ContainsKey(data.Ticker))
                return;

            bool modified = false;

            foreach(var alert in AlertsByTicker[data.Ticker].ToList())
            {
                switch(alert.Type)
                {
                    case AlertType.Higher:
                        if (data.LastTrade > alert.Value)
                        {
                            Program.SendMessage(string.Format("{0}: ticker {1}'s current value, {2}, is higher than the value you specified, {3}.", alert.Nick, alert.TickerName, data.LastTrade, alert.Value), Config.GetString("monitor.ptoken"));
                            AlertsByTicker[data.Ticker].Remove(alert);
                            Alerts.Remove(alert);

                            modified = true;
                        }
                        break;
                    case AlertType.Lower:
                        if (data.LastTrade < alert.Value)
                        {
                            Program.SendMessage(string.Format("{0}: ticker {1}'s current value, {2}, is lower than the value you specified, {3}.", alert.Nick, alert.TickerName, data.LastTrade, alert.Value), Config.GetString("monitor.ptoken"));
                            AlertsByTicker[data.Ticker].Remove(alert);
                            Alerts.Remove(alert);

                            modified = true;
                        }
                        break;
                }
            }

            if(modified)
                Save();
        }

        static void Load()
        {
            if (!File.Exists("./alerts"))
                return;
            try
            {
                var formatter = new BinaryFormatter();
                using (FileStream fs = new FileStream("./alerts", FileMode.OpenOrCreate))
                {
                    Alerts = (List<Alert>)formatter.Deserialize(fs);
                }

                foreach(var alert in Alerts)
                {
                    Ticker ticker = alert.TickerName;

                    if (!AlertsByTicker.ContainsKey(ticker))
                        AlertsByTicker[ticker] = new List<Alert>();

                    AlertsByTicker[ticker].Add(alert);
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
                using (FileStream fs = new FileStream("./alerts", FileMode.OpenOrCreate))
                {
                    formatter.Serialize(fs, Alerts);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to save monitors.");
                Console.WriteLine(ex);
            }
        }

        public static void RegisterTickers()
        {
            var tickers = Alerts.Select(a => a.TickerName).Distinct();

            foreach (var ticker in tickers)
                CryptoHandler.Bitfinex.SubscribeToTicker(ticker);
        }

        public static void CreateAlert(Ticker ticker, string nick, double value, AlertType type)
        {
            var alert = new Alert(ticker, value, type, nick);
            Alerts.Add(alert);

            if (!AlertsByTicker.ContainsKey(ticker))
                AlertsByTicker[ticker] = new List<Alert>();

            AlertsByTicker[ticker].Add(alert);

            Save();
        }
    }

    [Serializable]
    public class Alert
    {
        public Ticker TickerName { get; set; }
        public AlertType Type { get; set; }
        public double Value { get; set; }
        public string Nick { get; set; }

        public Alert(Ticker ticker, double value, AlertType type, string nick)
        {
            TickerName = ticker;
            Value = value;
            Type = type;
            Nick = nick;
        }
    }

    public enum AlertType
    {
        Lower,
        Higher
    }
}

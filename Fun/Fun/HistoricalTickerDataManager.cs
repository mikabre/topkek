using Exchange;
using Heimdall;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace Fun
{
    public class HistoricalTickerDataManager
    {
        public static Dictionary<Ticker, List<TickerData>> Data = new Dictionary<Ticker, List<TickerData>>();

        static int MaxRecords = 86400;

        public static void Load()
        {
            if (!File.Exists("./ticker-history"))
                return;

            try
            {
                var formatter = new BinaryFormatter();
                using (FileStream fs = new FileStream("./ticker-history", FileMode.OpenOrCreate))
                {
                    Data = (Dictionary<Ticker, List<TickerData>>)formatter.Deserialize(fs);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to load historical ticker data.");
                Console.WriteLine(ex);
            }
        }

        public static void Save()
        {
            try
            {
                using (FileStream fs = new FileStream("./ticker-history", FileMode.OpenOrCreate))
                {
                    uint pair_count = (uint)Data.Count;

                    fs.WriteUInt(pair_count);

                    foreach(var pair in Data)
                    {
                        fs.WriteString(pair.Key.ToString());

                        //foreach(var data_point in pair.Value.)
                    }
                }
                //var formatter = new BinaryFormatter();
                //using (FileStream fs = new FileStream("./ticker-history", FileMode.OpenOrCreate))
                //{
                //    formatter.Serialize(fs, Data);
                //}
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to save historical ticker data.");
                Console.WriteLine(ex);
            }
        }
        

        static void Prune()
        {
            foreach(var pair in Data)
            {

            }
        }

        public static void LogTickerUpdate(IExchange exchange, TickerData data)
        {

        }
    }
}

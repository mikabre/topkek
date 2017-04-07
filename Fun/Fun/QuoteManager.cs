using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Fun
{
    public class QuoteManager
    {
        public List<Quote> Quotes;

        private string SavePath = "./quotes";

        private Random rnd = new Random();

        public QuoteManager()
        {
            if (!File.Exists(SavePath))
            {
                Quotes = new List<Quote>();
                Save(SavePath);
            }
            else
                Load(SavePath);
        }

        public void Add(string msg)
        {
            Quote quote = new Quote() { Added = DateTime.Now, Content = msg };

            Quotes.Add(quote);

            Save(SavePath);
        }

        public string GetRandomQuote()
        {
            int num = rnd.Next(Quotes.Count);

            return GetQuoteById(num + 1);
        }

        public void DeleteQuoteById(int ID)
        {
            Quotes.RemoveAt(ID - 1);

            Save(SavePath);
        }

        public string GetQuoteById(int ID)
        {
            Quote quote = Quotes[ID - 1];

            return string.Format("11#{0}: {1} || {2}", ID, quote.Added, quote.Content);
        }

        public void Load(string path)
        {
            BinaryFormatter formatter = new BinaryFormatter();

            using (FileStream fs = new FileStream(path, FileMode.Open))
            {
                Quotes = (List<Quote>)formatter.Deserialize(fs);
            }
        }

        public void Save(string path)
        {
            BinaryFormatter formatter = new BinaryFormatter();

            using (FileStream fs = new FileStream(path, FileMode.Create))
            {
                formatter.Serialize(fs, Quotes);
            }
        }
    }

    [Serializable]
    public class Quote
    {
        public string Content { get; set; }
        public DateTime Added { get; set; }
        
        public Quote()
        {

        }
    }
}

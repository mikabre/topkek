using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinanceThing
{
    [Serializable]
    public class Transaction
    {
        public string Channel { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public long Amount { get; set; }
        public string Description { get; set; }
        public DateTime Time { get; set; }

        public Transaction(string desc, string source, string from, string to, long amount, DateTime time)
        {
            Channel = source;
            From = from;
            To = to;
            Amount = amount;
            Description = desc;
            Time = time;
        }

        public override string ToString()
        {
            return string.Format("[{0} - {4}] {1} -> {2} in {5}, amount: ${3:N0}", Description, From, To, Amount, Time, Channel);
        }
    }
}

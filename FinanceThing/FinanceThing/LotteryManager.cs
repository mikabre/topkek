using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FinanceThing
{
    public class LotteryManager
    {
        public LotteryState CurrentLottery { get; set; }

        public LotteryManager()
        {

        }

        //public void Loop()
        //{
        //    while(true)
        //    {
        //        try
        //        {
        //            while (CurrentLottery == null)
        //                Thread.Sleep(1000);

        //            var span = (DateTime.Now - CurrentLottery.End);
        //            Thread.Sleep(span);
                    
        //        }
        //        catch
        //        { }
        //    }
        //}

        public void Start(long stake, TimeSpan span)
        {
            var lottery = new LotteryState(DateTime.Now + span, stake);
            CurrentLottery = lottery;
        }

        public LotteryResult End()
        {
            if (CurrentLottery == null)
                return null;

            var result = new LotteryResult(CurrentLottery.Draw(), CurrentLottery.Stake);
            Save();

            if (!Directory.Exists("./lotteries"))
                Directory.CreateDirectory("./lotteries");

            File.Move("./lottery", "./lotteries/lottery-" + DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss"));

            CurrentLottery = null;

            return result;
        }

        public void Save()
        {
            IFormatter formatter = new BinaryFormatter();
            FileStream fs = new FileStream("./lottery-temp", FileMode.OpenOrCreate);

            formatter.Serialize(fs, CurrentLottery);

            fs.Close();

            File.Copy("./lottery-temp", "./lottery", true);
        }

        public void Load()
        {
            if (!File.Exists("./lottery"))
                return;
            
            IFormatter formatter = new BinaryFormatter();
            FileStream fs = new FileStream("./lottery", FileMode.Open);

            CurrentLottery = (LotteryState)formatter.Deserialize(fs);

            fs.Close();
        }
    }

    public class LotteryResult
    {
        public string Winner { get; set; }
        public long Winnings { get; set; }

        public LotteryResult(string winner, long winnings)
        {
            Winner = winner;
            Winnings = winnings;
        }
    }

    [Serializable]
    public class LotteryState
    {
        public Dictionary<string, long> Tickets = new Dictionary<string, long>();
        //public DateTime End = DateTime.Now;
        public long Stake = 0;
        public static Random Random = new Random();

        public LotteryState(DateTime end, long stake)
        {
            Stake = stake;
            //End = end;
        }

        public string Draw()
        {
            double sum = Tickets.Sum(t => t.Value);
            sum *= Random.NextDouble();

            double counter = 0;

            foreach(var pair in Tickets)
            {
                long count = pair.Value;

                if (counter < sum && sum < (counter + count))
                    return pair.Key;

                counter += count;
            }

            return Tickets.Last().Key;
        }

        public long Get(string user)
        {
            var pair = Tickets.FirstOrDefault(p => p.Key.ToLower() == user.ToLower());

            if (pair.Equals(new KeyValuePair<string, long>()))
                return -1;
            return pair.Value;
        }

        public void Add(string user, long tickets)
        {
            var pair = Tickets.FirstOrDefault(p => p.Key.ToLower() == user.ToLower());

            if (pair.Equals(new KeyValuePair<string, long>()))
                Tickets[user] = tickets;
            else
                Tickets[pair.Key] += tickets;

            Stake += tickets;
        }
    }
}

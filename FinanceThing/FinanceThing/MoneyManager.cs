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
    [Serializable]
    public class MoneyManager
    {
        public List<User> Users = new List<User>();
        public List<Transaction> Transactions = new List<Transaction>();
        public string Error = "";
        public static TimeSpan JailTime = new TimeSpan(0, 5, 0);
        public static TimeSpan PayInterval = new TimeSpan(0, 10, 0);
        public static int PayAmount = 600;

        public MoneyManager()
        {

        }

        public void TrannyLoop()
        {
            while (true)
            {
                var added = DateTime.Now.AddDays(1);
                var dt = new DateTime(added.Year, added.Month, added.Day, 0, 0, 0);
                var wait = (dt - DateTime.Now);

                Console.WriteLine("Sleeping until {0} for {1}", dt, wait);

                Thread.Sleep(wait);

                lock (Transactions)
                {
                    Save();
                    Transactions.Clear();
                    File.Move("./transactions", "./transaction-backups/transactions-" + DateTime.Now.ToString("yyyy-dd-MM-HH-mm-ss"));
                    File.Move("./save", "./transaction-backups/save-" + DateTime.Now.ToString("yyyy-dd-MM-HH-mm-ss"));
                    Console.WriteLine("Cycled out transactions file");
                }

                Thread.Sleep(5000);
            }
        }

        public void Save()
        {
            lock (Transactions)
            {
                IFormatter formatter = new BinaryFormatter();
                FileStream temp_stream = new FileStream("./temp-save", FileMode.OpenOrCreate);
                FileStream trans = new FileStream("./transactions", FileMode.OpenOrCreate);

                formatter.Serialize(temp_stream, Users);
                formatter.Serialize(trans, Transactions);

                temp_stream.Close();
                trans.Close();

                if (new FileInfo("./temp-save").Length != 0)
                    File.Copy("./temp-save", "./save", true);
            }
        }

        public void Load()
        {
            IFormatter formatter = new BinaryFormatter();
            try
            {
                FileStream save = new FileStream("./save", FileMode.Open);

                Users = (List<User>)formatter.Deserialize(save);

                save.Close();

                if (File.Exists("./transactions"))
                {
                    FileStream trans = new FileStream("./transactions", FileMode.Open);

                    Transactions = (List<Transaction>)formatter.Deserialize(trans);

                    trans.Close();
                }
            }
            catch
            {
                Error = "Failed to load main database, attempting to load ./temp-save...";

                bool fail = false;

                try
                {
                    FileStream j = new FileStream("./temp-save", FileMode.Open);
                    Users = (List<User>)formatter.Deserialize(j);
                    j.Close();
                }
                catch
                {
                    Error += "failed to load from ./temp-save!";
                    Users = new List<User>();
                    fail = true;
                }

                if (!fail)
                    Error += "success!";
            }
        }

        public void Log(string desc, string source, string from, string to, long amount)
        {
            lock (Transactions)
            {
                Transactions.Add(new Transaction(desc, source, from, to, amount, DateTime.Now));
            }
        }

        public User GetUser(string channel, string nick, bool create = false)
        {
            nick = nick.ToLower();
            channel = channel.ToLower();

            if(!Users.Any(u => u.Name == nick && u.Channel == channel))
            {
                if (create)
                {
                    User temp = new User(nick, channel);
                    Users.Add(temp);
                }
                else
                    return null;
            }

            User ret = Users.First(u => u.Name == nick && u.Channel == channel);

            if(ret.Deleted)
            {
                ret.Deleted = false;
            }

            return ret;
        }
    }

    [Serializable]
    public class User
    {
        public string Channel { get; set; }
        public string Name { get; set; }
        public long Balance { get; set; }
        public DateTime LastPaid { get; set; }
        public DateTime Jailed { get; set; }
        public bool Bot { get; set; }
        public bool Deleted { get; set; }
        
        public bool CanGetPaid
        {
            get
            {
                return (DateTime.Now - LastPaid) > MoneyManager.PayInterval;
            }
        }
        
        public bool InJail
        {
            get
            {
                return (DateTime.Now - Jailed) < MoneyManager.JailTime;
            }
        }

        public User(string name, string channel)
        {
            Name = name;
            Channel = channel;
            LastPaid = DateTime.Now - (MoneyManager.PayInterval + TimeSpan.FromSeconds(1));
            Jailed = DateTime.Now - (MoneyManager.JailTime + TimeSpan.FromSeconds(1));
            Balance = 0;
            Bot = false;
            Deleted = false;
        }

        public void Delete()
        {
            Balance = 0;
            Deleted = true;
        }
    }
}

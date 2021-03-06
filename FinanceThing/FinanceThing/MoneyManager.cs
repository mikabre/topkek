﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;
using Osiris;
using ProbablyFair;

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
        public static DateTime LastSaved = DateTime.Now;

        public Dictionary<string, string> Translation = new Dictionary<string, string>()
        {

        };

        public MoneyManager()
        {

        }

        public void TrannyLoop()
        {
            while (true)
            {
                var added = DateTime.UtcNow.AddDays(1);
                var dt = new DateTime(added.Year, added.Month, added.Day, 0, 0, 0);
                var wait = (dt - DateTime.UtcNow);

                Console.WriteLine("Sleeping until {0} for {1}", dt, wait);

                Thread.Sleep(wait);

                lock (Transactions)
                {
                    Save(true);
                    Transactions.Clear();

                    string filename = DateTime.Now.ToString("yyyy-MM-dd");
                    string yesterday_filename = DateTime.Now.AddDays(0).ToString("yyyy-MM-dd");

                    File.Move("./transactions", "./transaction-backups/transactions-" + filename);
                    File.Move("./save", "./transaction-backups/save-" + filename);

                    File.Move("./generator", Path.Combine(Config.GetString("generator.directory"), yesterday_filename + "-" + Program.Random.HashedName));
                    File.WriteAllText(Path.Combine(Config.GetString("generator.directory"), "latest.txt"), "http://hexafluoride.dryfish.net/audit-files/" + yesterday_filename + "-" + Program.Random.HashedName);
                    
                    File.Copy(Path.Combine(Config.GetString("generator.directory"), yesterday_filename + "-" + Program.Random.HashedName), Path.Combine(Config.GetString("generator.directory"), "latest"), true);
                    File.WriteAllText(Path.Combine(Config.GetString("generator.directory"), "index.html"), IndexGenerator.FromDirectory(Config.GetString("generator.directory")));
                    Program.Random = GeneratorManager.Create();

                    Console.WriteLine("Cycled out transactions file");
                }

                Thread.Sleep(5000);
            }
        }

        public bool SaveQueued = false;
        public DateTime SaveQueueTime = DateTime.Now;

        public ManualResetEvent SaveQueueEvent = new ManualResetEvent(false);

        public void SaveQueue()
        {
            SaveQueued = true;
            SaveQueueEvent.Reset();

            while (SaveQueueEvent.WaitOne(5000))
                SaveQueueEvent.Reset();

            Save(really: true);
            SaveQueued = false;
        }

        public void Save(bool really = false)
        {
            if(!really)
            {
                if (SaveQueued)
                    SaveQueueEvent.Set();
                else
                    Task.Factory.StartNew(SaveQueue);

                return;
            }

            Console.WriteLine("Really saved");

            //if ((DateTime.Now - LastSaved).TotalSeconds < 5)
            //    return;

            //LastSaved = DateTime.Now;

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

                Program.Random.Save("./generator");
            }
        }

        public void Load()
        {
            IFormatter formatter = new BinaryFormatter();
            try
            {

                if (File.Exists("./translations.json"))
                {
                    var obj = JObject.Parse(File.ReadAllText("./translations.json"));

                    foreach (var child in obj.Children())
                    {
                        foreach (var c2 in child.Values())
                            Translation[c2.Value<string>()] = ((JProperty)child).Name.PadRight(16, '0');
                    }
                }
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
            if (Translation.ContainsKey(source.ToLower()))
                source = Translation[source.ToLower()];

            lock (Transactions)
            {
                Transactions.Add(new Transaction(desc, source, from, to, amount, DateTime.Now));
            }
        }

        public User GetUser(string channel, string nick, bool create = false)
        {
            nick = nick.ToLower();
            channel = channel.ToLower();

            if (Translation.ContainsKey(channel))
                channel = Translation[channel];

            if (!Users.Any(u => u.Name == nick && u.Channel == channel))
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

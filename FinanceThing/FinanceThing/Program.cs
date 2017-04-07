using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Osiris;
using Heimdall;
using System.IO;
using System.Threading;

namespace FinanceThing
{
    class Program : ExampleModule
    {
        public static Random Random = new Random();
        static double GambleChance = 0.5;
        static double MugChance = 0.1;
        static MoneyManager Manager = new MoneyManager();
        static LotteryManager LotteryManager = new LotteryManager();

        static Dictionary<string, Dictionary<string, char>> ModeCache = new Dictionary<string, Dictionary<string, char>>();
        static Dictionary<string, DateTime> LastStatsUse = new Dictionary<string, DateTime>();

        static void Main(string[] args)
        {
            Name = "finance";
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");

            Commands = new Dictionary<string, MessageHandler>()
            {
                //{"", CheckForExpression },
                {".give", Give },
                {".pokies", Bet },
                {".bene", GetPaid },
                {".mug", Mug },
                {".money", GetBalance },
                {".getsource", GetSource },
                {"$donate", Donate },
                {".bet", Alias },
                {"$getkey", GetKey },
                {"$setkey", SetKey },
                {"$resetstrs", ResetStrs },
                {".bot", SetBot },
                {".human", SetHuman },
                {".delete", Delete },
                {"$stats", GetStats },
                {".stats", GetStats },
                {"$writestats", WriteFile },
                {"$writebots", WriteBots },
                {"$botwealth", BotWealth },
                {"$deletebots", RedistributeBotMoney },
                {"$recalculate", RecalculateTable },
                {".rankcost", GetBalanceForRank },
                {"$draw", DrawLottery },
                {".buytickets", BuyTickets },
                {"$startlottery", StartLottery },
                {".chance", GetChance },
                {"$distribute", DistributeAmount },
                {".rigreport", RigReport },
                {"$addexpr", AddToExpression },
                {"$execute", ExecuteExpression},
                {"$startexpr", StartTyping }
            };

            Manager.Load();
            LotteryManager.Load();
            new Thread(new ThreadStart(Manager.TrannyLoop)).Start();
            StringManager.SetStrings();

            string query = "";

            //while(true)
            //{
            //    string line = Console.ReadLine();
            //    if (string.IsNullOrWhiteSpace(line))
            //    {
            //        IFilter filter = new FilterInstance<Transaction>(Manager.Transactions.ToList());

            //        object result = null;

            //        foreach (var action in query.Split('\n'))
            //        {
            //            if (string.IsNullOrWhiteSpace(action))
            //                continue;

            //            var pair = filter.Apply(new Clause(action.Split(' ')[0], string.Join(" ", action.Split(' ').Skip(1))));

            //            if (pair.Key == 0)
            //                filter = (IFilter)pair.Value;
            //            else if (pair.Key == 1)
            //                result = pair.Value;
            //        }

            //        if (result is IEnumerable<object>)
            //        {
            //            foreach (var obj in (result as IEnumerable<object>))
            //                Console.WriteLine(obj);
            //        }
            //        else
            //            Console.WriteLine(result.ToString());

            //        query = "";
            //    }
            //    else
            //        query += line + "\n";
            //}

            Init(args, delegate
            {
            });
        }

        static Dictionary<string, string> Expressions = new Dictionary<string, string>();
        static List<Tuple<string, string>> CurrentTyping = new List<Tuple<string, string>>();

        static void CheckForExpression(string args, string source, string nick)
        {
            if(CurrentTyping.Any(t => t.Item1 == GetSource(source) && t.Item2 == nick))
            {
                if (!args.StartsWith("$"))
                {
                    if (!Expressions.ContainsKey(args))
                        Expressions.Add(nick, args + "\n");
                    else
                        Expressions[nick] += args + "\n";
                }
            }
        }

        static void StartTyping(string args, string source, string nick)
        {
            if (!CurrentTyping.Any(t => t.Item1 == GetSource(source) && t.Item2 == nick))
            {
                CurrentTyping.Add(new Tuple<string, string>(GetSource(source), nick));
            }
        }

        static void AddToExpression(string args, string source, string nick)
        {
            string expr = args.Substring("$addexpr".Length).Trim();

            if (!Expressions.ContainsKey(nick))
                Expressions.Add(nick, expr + "\n");
            else
                Expressions[nick] += expr + "\n";
        }

        static void ExecuteExpression(string args, string source, string nick)
        {
            try
            {
                if (!Expressions.ContainsKey(nick))
                {
                    SendMessage("You haven't built a query! Use $addexpr <expression> to build queries.", source);
                    return;
                }

                string query = Expressions[nick];

                if (string.IsNullOrWhiteSpace(query))
                {
                    SendMessage("You haven't built a query! Use $addexpr <expression> to build queries.", source);
                    return;
                }

                IFilter filter = new FilterInstance<Transaction>(Manager.Transactions.ToList());

                object result = null;

                foreach (var action in query.Split('\n'))
                {
                    if (string.IsNullOrWhiteSpace(action))
                        continue;

                    var pair = filter.Apply(new Clause(action.Split(' ')[0], string.Join(" ", action.Split(' ').Skip(1))));

                    if (pair.Key == 0)
                        filter = (IFilter)pair.Value;
                    else if (pair.Key == 1)
                        result = pair.Value;
                    else if (pair.Key == -1)
                        result = pair.Value;
                }

                if (result is IEnumerable<object>)
                {
                    var list = (result as IEnumerable<object>);

                    int count = list.Count();
                    list = list.Skip(count - 3);
                    SendMessage(string.Format("{0} entries skipped, displaying 3...", count - 3), source);

                    foreach (var obj in list)
                        SendMessage(obj.ToString(), source);
                }
                else
                    SendMessage(result.ToString(), source);
            }
            catch (Exception ex)
            {
                SendMessage("Failed to execute query!", source);
                //ex.ToString().Split('\n').ToList().ForEach(s => SendMessage(s, source));
            }
            finally
            {
                Expressions.Remove(nick);
                if (CurrentTyping.Any(t => t.Item1 == GetSource(source) && t.Item2 == nick))
                    CurrentTyping.RemoveAll(t => t.Item1 == GetSource(source) && t.Item2 == nick);
            }
        }

        static void RigReport(string args, string source, string nick)
        {
            args = args.Substring(".rigreport".Length).Trim();
            
            var bets = Manager.Transactions.Where(t => t.Description == "bet").ToList();

            if(!string.IsNullOrWhiteSpace(args))
            {
                bets = bets.Where(t => t.From.ToLower() == args.ToLower() || t.To.ToLower() == args.ToLower()).ToList();
            }

            var lost = bets.Where(t => t.To == "*bet_source*");
            var won = bets.Where(t => t.From == "*bet_source*");

            SendMessage(string.Format("{9}{0} bets in the last {1} hour(s) and {2} minute(s): {3} win(s), 3${4:N0} won; {5} loss(es), 3${6:N0} lost ({7:0.00}% possible losing bias according to count, {8:0.00}% possible losing bias according to value)",
                bets.Count,
                DateTime.Now.Hour,
                DateTime.Now.Minute,
                won.Count(),
                won.Sum(t => t.Amount),
                lost.Count(),
                lost.Sum(t => t.Amount),
                ((double)(lost.Count() - won.Count()) / (double)(bets.Count)) * 100.0,
                ((double)(lost.Sum(t => t.Amount) - won.Sum(t => t.Amount)) / (double)(bets.Sum(t => t.Amount))) * 100.0,
                !string.IsNullOrWhiteSpace(args) ? "Rig report for " + args + ": " : ""
                ), source);
        }

        static void GetChance(string args, string source, string nick)
        {
            args = args.Substring(".chance".Length).Trim();

            string meme = "";

            if(LotteryManager.CurrentLottery.Get(args) != -1)
            {
                nick = args;
                meme = nick;
            }

            var user = Manager.GetUser(GetSource(source), nick, true);

            if (LotteryManager.CurrentLottery == null)
            {
                SendMessage("There's no lottery running right now.", source);
                return;
            }

            long user_tickets = LotteryManager.CurrentLottery.Get(nick);
            long total_tickets = LotteryManager.CurrentLottery.Tickets.Sum(p => p.Value);

            if(user_tickets == -1)
            {
                SendMessage(StringManager.GetString("chance.notickets", user), source);
                return;
            }

            SendMessage(StringManager.GetString("chance.info", user, ((double)user_tickets / (double)total_tickets) * 100d, user_tickets, total_tickets, meme == "" ? "ur" : meme + "'s"), source);
        }

        static void DrawLottery(string args, string source, string nick)
        {
            args = args.Substring(".draw".Length);
            var user = Manager.GetUser(GetSource(source), nick, true);

            if (LotteryManager.CurrentLottery == null)
            {
                SendMessage("There's no lottery running right now.", source);
                return;
            }

            var tickets = LotteryManager.CurrentLottery.Tickets.ToList();

            var result = LotteryManager.End();
            Manager.GetUser(GetSource(source), result.Winner, true).Balance += result.Winnings;

            SendMessage(StringManager.GetString("draw.winner", user, result.Winner, result.Winnings, tickets.First(p => p.Key == result.Winner).Value), source);
            Manager.Log("lottery_draw", GetSource(source), "*lottery*", result.Winner, (long)result.Winnings);
        }

        static void BuyTickets(string args, string source, string nick)
        {
            args = args.Substring(".buytickets".Length);
            var user = Manager.GetUser(GetSource(source), nick, true);

            if (LotteryManager.CurrentLottery == null)
            {
                SendMessage(StringManager.GetString("tickets.nolottery", user), source);
                return;
            }

            ulong amount = 0;

            if (!ulong.TryParse(args.Replace(",", ""), out amount))
            {
                long temp = 0;

                if (long.TryParse(args.Replace(",", ""), out temp))
                {
                    SendMessage(StringManager.GetString("tickets.negative", user), source);
                    return;
                }

                SendMessage(StringManager.GetString("tickets.invalid", user), source);
                return;
            }

            if ((ulong)user.Balance < amount)
            {
                SendMessage(StringManager.GetString("tickets.insufficient", user), source);
                return;
            }

            user.Balance -= (long)amount;
            LotteryManager.CurrentLottery.Add(nick, (long)amount);
            LotteryManager.Save();

            SendMessage(StringManager.GetString("tickets.info", user, amount, LotteryManager.CurrentLottery.Stake, ((double)LotteryManager.CurrentLottery.Get(nick) / (double)LotteryManager.CurrentLottery.Tickets.Sum(p => p.Value)) * 100d), source);
            Manager.Log("buy_tickets", GetSource(source), user.Name, "*lottery*", (long)amount);
        }

        static void StartLottery(string args, string source, string nick)
        {
            if(LotteryManager.CurrentLottery != null)
            {
                SendMessage("There's a lottery running right now.", source);
                return;
            }

            args = args.Substring("$startlottery".Length);

            LotteryManager.Start(long.Parse(args), new TimeSpan(0, 1, 0));
            LotteryManager.Save();
            SendMessage(string.Format("Created lottery with {0} stake.", LotteryManager.CurrentLottery.Stake), source);
        }

        static void GetBalanceForRank(string args, string source, string nick)
        {
            try
            {
                args = args.Substring(".rankcost".Length).Trim().ToLower();
                char rank = args.FirstOrDefault();

                if (!"qaohv".Contains(rank))
                {
                    SendMessage("That's not a rank eejit", source);
                    return;
                }

                var smallest = ModeCache[source.Substring(0, 16)].Where(p => p.Value == rank).Select(p => Manager.GetUser(source.Substring(0, 16), p.Key)).OrderBy(u => u.Balance).First();
                SendMessage(string.Format("To obtain rank +{0}, you need to edge out {1} who has a balance of 3${2:N0}.", rank, smallest.Name, smallest.Balance), source);
            }
            catch
            {

            }
        }

        static void RecalculateTable(string args, string source, string nick)
        {
            ModeCache = new Dictionary<string, Dictionary<string, char>>();
            UpdateModes(source);

            SendMessage("done", source);
        }

        static Dictionary<string, char> CalculateModes(string source)
        {
            var users = Manager.Users.Where(u => u.Channel == source).OrderByDescending(u => u.Balance).ToList();
            var ret = new Dictionary<string, char>();

            int i = 0;

            foreach (var user in users)
            {
                ret.Add(user.Name, CalculateMode(i++));
            }

            return ret;
        }

        static void UpdateModes(string source)
        {
            return;
            lock (ModeCache)
            {
                var short_source = source.Substring(0, 16);

                var prev_modes = new Dictionary<string, char>();

                if (ModeCache.ContainsKey(short_source))
                    prev_modes = ModeCache[short_source];

                var new_modes = CalculateModes(short_source);
                var difference = new_modes.Where(pair =>
                {
                    return !prev_modes.ContainsKey(pair.Key) || prev_modes[pair.Key] != pair.Value;
                });

                foreach (var diff in difference)
                {
                    ModeUpdate(diff.Key, source, diff.Value, difference.Count() > 6);
                }

                if(prev_modes.Any(u => !new_modes.ContainsKey(u.Key)))
                {
                    var removed = prev_modes.Where(u => !new_modes.ContainsKey(u.Key)).ToList();

                    Console.WriteLine("{0} removed pairs", removed.Count);

                    foreach (var pair in removed)
                        ModeUpdate(pair.Key, source, ' ', false);
                }

                ModeCache[short_source] = new_modes;
            }
        }

        static char CalculateMode(int rank)
        {
            if (rank < 5)
                return 'a';

            if (rank < 10)
                return 'o';

            if (rank < 15)
                return 'h';

            if (rank < 20)
                return 'v';

            return ' ';
            //if (balance < 10000)
            //    return ' ';
            //else if (balance < 100000)
            //    return 'v';
            //else if (balance < 1000000)
            //    return 'h';
            //else if (balance < 100000000)
            //    return 'o';
            //else
            //    return 'a';
        }

        static void RedistributeBotMoney(string args, string source, string nick)
        {
            var bots = GetBots(source).ToList();
            var balance = bots.Sum(u => u.Balance);

            foreach (var bot in bots)
                bot.Delete();

            var users = Manager.Users.Where(u => u.Channel == GetSource(source) && !u.Deleted).ToList();

            SendMessage(string.Format("Randomly distributing ${0:N0}...", balance), source);

            while(balance > 0)
            {
                var number = Math.Min(balance, Random.Next(40000, 60000));
                var user = users[Random.Next(users.Count)];
                user.Balance += number;
                balance -= number;
                Manager.Log("bot_delete", GetSource(source), "*bots*", user.Name, (long)number);
            }

            SendMessage("done", source);
        }

        static void DistributeAmount(string args, string source, string nick)
        {
            try
            {
                long amount = long.Parse(args.Substring("$distribute".Length).Trim());

                var users = Manager.Users.Where(u => u.Channel == GetSource(source) && !u.Deleted).ToList();

                SendMessage(string.Format("Randomly distributing ${0:N0}...", amount), source);

                while (amount > 0)
                {
                    var number = Math.Min(amount, Random.Next(40000, 60000));
                    var user = users[Random.Next(users.Count)];
                    user.Balance += number;
                    amount -= number;
                    Manager.Log("redistribute", GetSource(source), nick, user.Name, (long)number);
                }

                SendMessage("done", source);
            }
            catch
            {

            }
        }

        static IEnumerable<User> GetBots(string source)
        {
            List<string> bot_prefixes = new List<string>()
            {
                "ni_",
                "nig_",
                "pikey_",
                "memeshit_",
                "hex_is_gay_",
                "foobarstuffs_",
                "cpt_",
                "moneyforkrock_",

            };

            List<string> wiiaam_shit = new List<string>()
            {
                "wiiaam",
                "wiiiaam",
                "wiiasidasd",
                "safdgwregge",
                "wefwergwerg",
                "safdgwregge",
                "paybot",

            };

            var short_source = GetSource(source);
            return Manager.Users.Where(u => u.Channel == short_source && !u.Deleted && (bot_prefixes.Any(prefix => u.Name.StartsWith(prefix)) || wiiaam_shit.Any(prefix => u.Name.StartsWith(prefix) && u.Name.Replace(prefix, "").All(c => char.IsNumber(c)) && u.Name.Replace(prefix, "").Any())));
        }

        static void BotWealth(string args, string source, string nick)
        {
            SendMessage(string.Format("${0:N0}", GetBots(source).Sum(u => u.Balance)), source);
        }

        static void WriteBots(string args, string source, string nick)
        {
            var users = GetBots(source).OrderByDescending(u => u.Balance).Select(u => string.Format("{0}: ${1:N0}", u.Name, u.Balance));
            File.WriteAllLines("./output.txt", users.ToArray());
        }

        static void WriteFile(string args, string source, string nick)
        {
            var short_source = GetSource(source);

            var users = Manager.Users.Where(u => u.Channel == short_source && !u.Deleted).OrderByDescending(u => u.Balance).Select(u => string.Format("{0}: ${1:N0}", u.Name, u.Balance));
            File.WriteAllLines("./output.txt", users.ToArray());
        }

        static void GetStats(string args, string source, string nick)
        {
            Task.Factory.StartNew(delegate
            {
                var short_source = GetSource(source);

                var users = Manager.Users.Where(u => u.Channel == short_source).OrderByDescending(u => u.Balance).ToList();
                var sum = users.Sum(u => u.Balance);
                var lottery_sum = LotteryManager.CurrentLottery == null ? 0 : LotteryManager.CurrentLottery.Tickets.Sum(p => p.Value);

                if (args.StartsWith("."))
                {
                    if (LastStatsUse.ContainsKey(nick))
                    {
                        if ((DateTime.Now - LastStatsUse[nick]).TotalSeconds < 15)
                        {
                            SendNotice("You can only use .stats once every 15 seconds.", source, nick);
                            return;
                        }
                    }

                    LastStatsUse[nick] = DateTime.Now;
                }

                if (args.StartsWith("."))
                {
                    SendNotice(string.Format("Total amount of money: 3${0:N0} (+{1:N0} lottery tickets)", users.Sum(u => u.Balance), lottery_sum), source, nick);
                    SendNotice("Richest people:", source, nick);
                }
                else
                {
                    SendMessage(string.Format("Total amount of money: 3${0:N0} (+{1:N0} lottery tickets)", users.Sum(u => u.Balance), lottery_sum), source);
                    SendMessage("Richest people:", source);
                }

                for (int i = 0; i < (LotteryManager.CurrentLottery == null ? 5 : 3); i++)
                {
                    var richest = users[i];

                    if (args.StartsWith("."))
                        SendNotice(string.Format("{0}. {1}(3${2:N0}, {3:0.00}% of all money)", i + 1, richest.Name, richest.Balance, ((double)richest.Balance / (double)sum) * 100.0), source, nick);
                    else
                        SendMessage(string.Format("{0}. {1}(3${2:N0}, {3:0.00}% of all money)", i + 1, richest.Name, richest.Balance, ((double)richest.Balance / (double)sum) * 100.0), source);
                    
                }
                if (LotteryManager.CurrentLottery != null)
                {
                    users = users.OrderByDescending(u => LotteryManager.CurrentLottery.Get(u.Name)).ToList();

                    for (int i = 0; i < 3; i++)
                    {
                        var richest = users[i];
                        long bal = LotteryManager.CurrentLottery.Get(richest.Name);

                        if (args.StartsWith("."))
                            SendNotice(string.Format("{0}. {1}({2:N0}, {3:0.00}% of all tickets)", i + 1, richest.Name, bal, ((double)bal / (double)lottery_sum) * 100.0), source, nick);
                        else
                            SendMessage(string.Format("{0}. {1}({2:N0}, {3:0.00}% of all tickets)", i + 1, richest.Name, bal, ((double)bal / (double)lottery_sum) * 100.0), source);
                        
                    }
                }
            });
        }

        static void ModeUpdate(string nick, string source, char new_mode, bool lot = false)
        {
            MemoryStream ms = new MemoryStream();

            ms.WriteString(source);
            ms.WriteString(nick);
            ms.WriteString(new_mode.ToString());
            ms.WriteString(lot ? "meme" : "stale");

            Connection.SendMessage(ms.ToArray(), "mode_update", "gatekeeper");
        }

        static void Delete(string args, string source, string nick)
        {
            args = args.Substring(".delete".Length).Trim();
            var user = Manager.GetUser(GetSource(source), nick);

            if (user == null)
                return;
            
            string target_name = "hexafluoride";

            //if (!string.IsNullOrWhiteSpace(args))
            //{
                var target = string.IsNullOrWhiteSpace(args) ? Manager.GetUser(GetSource(source), "hexafluoride", true) : Manager.GetUser(GetSource(source), args, true);
                target.Balance += user.Balance;
                target_name = target.Name;
            //}

            long deleted_tickets = 0;

            if (LotteryManager.CurrentLottery != null)
            {
                if (LotteryManager.CurrentLottery.Get(user.Name) != -1)
                {
                    deleted_tickets += LotteryManager.CurrentLottery.Get(user.Name);
                    if (!string.IsNullOrWhiteSpace(args))
                    {
                        //var target = Manager.GetUser(GetSource(source), args, true);
                    }
                    else
                    {
                    }
                    target.Balance += LotteryManager.CurrentLottery.Get(user.Name);
                    LotteryManager.CurrentLottery.Add(user.Name, -LotteryManager.CurrentLottery.Get(user.Name));
                }
            }

            Manager.Log("bot_delete", GetSource(source), user.Name, target_name, (long)user.Balance + (deleted_tickets));
            user.Delete();
            //Manager.Users.Remove(user);
            Manager.Save(); UpdateModes(source);
        }

        static void SetBot(string args, string source, string nick)
        {
            args = args.Substring(".bot".Length).Trim();
            var user = Manager.GetUser(GetSource(source), nick, true);

            user.Bot = true;
        }

        static void SetHuman(string args, string source, string nick)
        {
            args = args.Substring(".human".Length).Trim();
            var user = Manager.GetUser(GetSource(source), nick, true);

            user.Bot = false;
        }

        static void ResetStrs(string args, string source, string nick)
        {
            StringManager.SetStrings();
        }

        static void GetKey(string args, string source, string nick)
        {
            args = args.Substring("$getkey".Length).Trim();
            
            string str = StringManager.GetString(args);
            SendMessage(str, source);
        }

        static void SetKey(string args, string source, string nick)
        {
            args = args.Substring("$setkey".Length).Trim();
            var parts = args.Split(' ');
            string key = parts[0];
            string rest = string.Join(" ", parts.Skip(1).ToArray());

            string str = StringManager.GetString(key);
            StringManager.Strings[key] = rest;
            SendMessage(string.Format("{0} changed from \"{1}\" to \"{2}\"", key, str, rest), source);
        }

        static void Alias(string args, string source, string nick)
        {
            Bet(args.Replace(".bet", ".pokies"), source, nick);
        }

        static void Donate(string args, string source, string nick)
        {
            try
            {
                var parts = args.Split(' ');
                var user = Manager.GetUser(GetSource(source), parts[1]);

                if (user == null)
                    return;

                user.Balance += long.Parse(parts[2]);
                Manager.Log("donate", GetSource(source), "*donation_source*", user.Name, (long)long.Parse(parts[2]));
                Manager.Save();
                UpdateModes(source);
            }
            catch
            {

            }
        }

        static void GetSource(string args, string source, string nick)
        {
            SendMessage(GetSource(source), source);
        }

        static new string GetSource(string source)
        {
            return source.Substring(0, 16);
        }

        static void Give(string args, string source, string nick)
        {
            args = args.Substring(".give".Length).Trim();
            var parts = args.Split(' ');

            var user = Manager.GetUser(GetSource(source), nick, true);
            var target = Manager.GetUser(GetSource(source), parts.First());

            ulong amount = 0;

            if (!ulong.TryParse(parts.Last().Replace(",", ""), out amount))
            {
                long temp = 0;

                if (long.TryParse(parts.Last().Replace(",", ""), out temp))
                {
                    SendMessage(StringManager.GetString("give.negative", user), source);
                    return;
                }

                SendMessage(StringManager.GetString("give.decimal", user), source);
                return;
            }

            if (target == null)
            {
                SendMessage(StringManager.GetString("give.noaccount", user), source);
                return;
            }

            if((ulong)user.Balance < amount)
            {
                SendMessage(StringManager.GetString("give.insufficient", user), source);
                return;
            }

            user.Balance -= (long)amount;
            target.Balance += (long)amount;

            SendMessage(StringManager.GetString("give.info", user, target.Name, amount), source);
            Manager.Log("give", GetSource(source), user.Name, target.Name, (long)amount);

            Manager.Save();
            UpdateModes(source);
            //BalanceUpdated(user, source);
            //BalanceUpdated(target, source);
        }

        static void Mug(string args, string source, string nick)
        {
            args = args.Substring(".mug".Length).Trim();

            var user = Manager.GetUser(GetSource(source), nick, true);
            var target = Manager.GetUser(GetSource(source), args);

            if (user.InJail)
            {
                SendMessage(StringManager.GetString("mug.jailed", user, (int)(MoneyManager.JailTime - (DateTime.Now - user.Jailed)).TotalSeconds), source);
                return;
            }

            long tickets = -1;

            if (LotteryManager.CurrentLottery != null)
                tickets = LotteryManager.CurrentLottery.Get(target.Name);

            if (target == null || (target.Balance == 0 && tickets == -1))
            {
                SendMessage(StringManager.GetString("mug.insufficient", user), source);
                return;
            }

            double local_chance = MugChance + Math.Min(0.3, ((tickets != -1 ? tickets : 0) + target.Balance) / 1000000.0);
            Console.WriteLine("{0}: {1}", target.Balance, local_chance);

            if(Random.NextDouble() < local_chance)
            {
                long amount = (long)(((double)Random.Next(10, 50) / 100d) * (double)target.Balance);
                long t_amount = (long)(((double)Random.Next(10, 50) / 100d) * (double)tickets);
                
                target.Balance -= amount;
                user.Balance += amount;

                if(tickets != -1)
                {
                    user.Balance += t_amount;
                    LotteryManager.CurrentLottery.Add(target.Name, -t_amount);
                    Manager.Log("mug_tickets", GetSource(source), target.Name, user.Name, (long)t_amount);
                }

                Manager.Log("mug", GetSource(source), target.Name, user.Name, (long)amount);
                SendMessage(StringManager.GetString("mug.successful", user, (tickets != -1 ? t_amount : 0) + amount, args), source);
            }
            else
            {
                user.Jailed = DateTime.Now;

                SendMessage(StringManager.GetString("mug.unsuccessful", user, MoneyManager.JailTime.Minutes), source);
            }

            Manager.Save();
            UpdateModes(source);
        }

        static void GetPaid(string args, string source, string nick)
        {
            var user = Manager.GetUser(GetSource(source), nick, true);

            if(user.CanGetPaid)
            {
                user.Balance += MoneyManager.PayAmount;
                user.LastPaid = DateTime.Now;
                SendMessage(StringManager.GetString("bene.info", user, MoneyManager.PayAmount, user.Balance), source);

                Manager.Log("bene", GetSource(source), "*bene_source*", user.Name, (long)MoneyManager.PayAmount);
            }
            else
            {
                var time = MoneyManager.PayInterval - (DateTime.Now - user.LastPaid);

                if(time.Minutes == 0 && time.Hours == 0)
                {
                    SendMessage(StringManager.GetString("bene.time", user, time.Seconds, "second"), source);
                }
                else
                {
                    SendMessage(StringManager.GetString("bene.time", user, (int)time.TotalMinutes, "minute"), source);
                }
            }
            Manager.Save();
            UpdateModes(source);
        }

        static void GetBalance(string args, string source, string nick)
        {
            var user = Manager.GetUser(GetSource(source), nick, true);

            args = args.Substring(".money".Length).Trim();
            var target = Manager.GetUser(GetSource(source), args);

            if (target != null)
                SendMessage(StringManager.GetString("money.other", user, args, target.Balance), source);
            else
                SendMessage(StringManager.GetString("money.info", user, user.Balance), source);
        }

        static void Bet(string args, string source, string nick)
        {
            args = args.Substring(".pokies".Length).Trim();
            ulong amount = 0;

            var user = Manager.GetUser(GetSource(source), nick);

            if(args == "coins")
            {
                SendMessage("real funny eh faggot", source);
                return;
            }

            if (!ulong.TryParse(args.Replace(",", ""), out amount))
            {
                long temp = 0;

                if (long.TryParse(args.Replace(",", ""), out temp))
                {
                    SendMessage(StringManager.GetString("bet.negative", user), source);
                    return;
                }

                SendMessage(StringManager.GetString("bet.invalid", user), source);
                return;
            }

            if(amount > (ulong)user.Balance)
            {
                SendMessage(StringManager.GetString("bet.insufficient", user), source);
                return;
            }

            if(Random.NextDouble() < GambleChance)
            {
                user.Balance += (long)amount;
                SendMessage(StringManager.GetString("bet.won", user, amount), source);

                Manager.Log("bet", GetSource(source), "*bet_source*", user.Name, (long)amount);
            }
            else
            {
                user.Balance -= (long)amount;
                SendMessage(StringManager.GetString("bet.lost", user, amount), source);

                Manager.Log("bet", GetSource(source), user.Name, "*bet_source*", (long)amount);
            }

            Manager.Save();
            UpdateModes(source);
        }
    }

    class StringManager
    {
        public static Dictionary<string, string> Strings = new Dictionary<string, string>();
    //    public static Dictionary<string, string> BotStrings = new Dictionary<string, string>()
    //    {
    //        { "bet.invalid", "{{\"result\":\"error\", \"error\":\"invalid_amount\", \"user\": \"{user}\"}}" },
    //        { "bet.insufficient", "{{\"result\":\"error\", \"error\":\"insufficient_balance\", \"user\": \"{user}\"}}" },
    //        { "bet.negative", "{{\"result\":\"error\", \"error\":\"negative_amount\", \"user\": \"{user}\"}}" },
    //        { "bet.won", "{{\"result\":\"won {0}\", \"error\": null, \"user\": \"{1}\"}}" },
    //        { "bet.lost", "{{\"result\":\"lost {0}\", \"error\": null, \"user\": \"{1}\"}}" },
    //        {"mug.insufficient", "{{\"result\":\"error\", \"error\":\"no_money\", \"user\": \"{user}\"}}" },
    //        {"mug.unsuccessful", "{{\"result\":\"jailed\", \"error\":null, \"user\": \"{user}\"}}" },
    //        {"mug.successful", "{{\"result\":\"won {0}\", \"error\":null, \"user\": \"{user}\"}}" },
    //        {"mug.jailed", "{{\"result\":\"error\", \"error\":\"in_jail\", \"user\": \"{user}\"}}" },
    //        { "money.info", "{{\"result\":\"{0}\", \"error\": null, \"user\": \"{user}\"}}" },
    //        {"money.other", "{{\"result\":\"{0}\", \"error\": null, \"user\": \"{user}\"}}" },
    //        { "bene.info", "{{\"result\":\"{0}\", \"error\": null, \"user\": \"{user}\"}}" },
    //        {"bene.time", "{{\"result\":\"error\", \"error\": \"{0}\", \"user\": \"{user}\"}}" },
    //        { "give.info", "{{\"result\":\"success\", \"error\":null, \"user\": \"{user}\"}}" },
    //        {"give.decimal", "{{\"result\":\"error\", \"error\":\"invalid_amount\", \"user\": \"{user}\"}}" },
    //        {"give.negative", "{{\"result\":\"error\", \"error\":\"negative_amount\", \"user\": \"{user}\"}}" },
    //        {"give.insufficient", "{{\"result\":\"error\", \"error\":\"insufficient_balance\", \"user\": \"{user}\"}}" },
    //        {"give.noaccount", "{{\"result\":\"error\", \"error\":\"no_account\", \"user\": \"{user}\"}}" }
    //};

        public static void SetStrings()
        {
            Strings = new Dictionary<string, string>()
            {
                {"bet.invalid", "u gotta put coins in the machine mate" },
                {"bet.insufficient", "u dont have enough money for that mate" },
                {"bet.negative", "stop being a poor cunt and put money in the machine" },
                {"bet.won", "bro you won! wow 3${0:N0}, thats heaps g! drinks on u ay" },
                {"bet.lost", "shit man, u lost 3${0:N0}. better not let the middy know" },
                {"mug.insufficient", "they dont have any money to steal" },
                {"mug.unsuccessful", "4,4 2,2 0,1POLICE4,4 2,2  Its the police! looks like u got caught. thats {0} minutes the big house for you!" },
                {"mug.successful", "u manage to steal 3${0:N0} off {1}" },
                {"mug.jailed", "ur in jail for another {0} seconds. dont drop the soap!" },
                {"money.info", "You currently have3 ${0:N0} in the bnz" },
                {"money.other", "{0} currently has 3${1:N0} in the bnz" },
                {"bene.info", "winz just gave u 3${0:N0}. u now have3 ${1:N0}" },
                {"bene.time", "bro ur next payment is in {0} {1}s" },
                {"give.info", "you gave {0} 3${1:N0}" },
                {"give.decimal", "dont be a cheap cunt" },
                {"give.negative", "dont be a cheap cunt" },
                {"give.insufficient", "u dont have enuf money bro" },
                {"give.noaccount", "sorry bro theyre with kiwibank" },
                {"tickets.invalid", "u gotta put coins in the machine mate" },
                {"tickets.insufficient", "u dont have enough money for that mate" },
                {"tickets.negative", "stop being a poor cunt and put money in the machine" },
                {"tickets.info", "u put 3${0:N0} in the pot and got {0} tickets. now theres 3${1:N0} in the pot and ur chance of winning is {2:0.00}%" },
                {"tickets.nolottery", "theres no lottery running right now mate" },
                {"chance.info", "{3} chance of winning is {0:0.00}% ({1} tickets out of {2})" },
                {"chance.notickets", "u havent bought any tickets m8" },
                {"draw.winner", "{0} won 3${1:N0} from the lottery with {2:N0} tickets" }
            };
        }

        public static string GetString(string key)
        {
            if (!Strings.ContainsKey(key))
                return key;

            return Strings[key];
        }

        public static string GetString(string key, User user)
        {
            if (!Strings.ContainsKey(key))
                return key;

            return Strings[key];
        }

        public static string GetString(string key, User user, params object[] format)
        {
            var value = GetString(key, user);

            if (key == value)
                return value;

            if(user.Bot)
            {
                value = value.Replace("{user}", user.Name);
            }

            return string.Format(value, format);
        }
    }
}

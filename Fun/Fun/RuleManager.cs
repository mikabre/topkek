using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace Fun
{
    [Serializable]
    public class RuleManager
    {
        public List<RuleList> RuleLists = new List<RuleList>();

        public RuleManager()
        {

        }

        public void Save()
        {
            IFormatter formatter = new BinaryFormatter();

            FileStream fs = new FileStream("./rules", FileMode.Create);

            formatter.Serialize(fs, this);

            fs.Close();
        }

        public static RuleManager Load()
        {
            if (!File.Exists("./rules"))
                return new RuleManager();

            try
            {
                IFormatter formatter = new BinaryFormatter();

                using (FileStream fs = new FileStream("./rules", FileMode.Open))
                {
                    var ret = (RuleManager)formatter.Deserialize(fs);
                    return ret;
                }
            }
            catch
            {
                return new RuleManager();
            }
        }

        public string[] GetRules(string channel)
        {
            if(!RuleLists.Any(rules => rules.Channel == channel))
                return new string[0];

            return RuleLists.First(rules => rules.Channel == channel).Rules.ToArray();
        }

        public void AddRule(string channel, string rule)
        {
            if (!RuleLists.Any(rules => rules.Channel == channel))
                RuleLists.Add(new RuleList(channel));

            GetRuleList(channel).AddRule(rule);

            Save();
        }

        public void RemoveRule(string channel, int rule)
        {
            if (!RuleLists.Any(rules => rules.Channel == channel))
                return;

            GetRuleList(channel).RemoveRule(rule);

            Save();
        }

        public RuleList GetRuleList(string channel)
        {
            return RuleLists.FirstOrDefault(r => r.Channel == channel);
        }
    }

    [Serializable]
    public class RuleList
    {
        public string Channel { get; set; }
        public List<string> Rules { get; set; }
        
        public RuleList(string channel)
        {
            Channel = channel;
            Rules = new List<string>();
        }

        public void AddRule(string rule)
        {
            if(!Rules.Contains(rule))
                Rules.Add(rule);
        }

        public void RemoveRule(int id)
        {
            Rules.RemoveAt(id);
        }
    }
}

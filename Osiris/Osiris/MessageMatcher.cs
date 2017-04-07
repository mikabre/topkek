using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Osiris
{
    public enum MatchType
    {
        StartsWith,
        Contains,
        EndsWith
    }

    [Serializable]
    public class MessageMatcher
    {
        public string ID { get; set; }
        public string MatchString { get; set; }
        public MatchType MatchType { get; set; }
        public string Node { get; set; }
        public bool OwnerOnly { get; set; }
        public string Nick { get; set; }
        public bool Notice { get; set; }
        public bool Join { get; set; }
        public bool Mode { get; set; }
        public bool EndExecution { get; set; }
        public bool ExecuteIfNoMatch { get; set; }

        public MessageMatcher()
        { 
        }

        public bool Matches(string target)
        {
            switch(MatchType)
            {
                case MatchType.StartsWith:
                    return target.StartsWith(MatchString);
                case MatchType.Contains:
                    return target.Contains(MatchString);
                case MatchType.EndsWith:
                    return target.EndsWith(MatchString);
                default:
                    return false;
            }
        }
    }
}

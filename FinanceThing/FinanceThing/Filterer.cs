using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Linq.Dynamic;

namespace FinanceThing
{
    public interface IFilter
    {
        KeyValuePair<int, object> Apply(Clause clause);
    }

    public class FilterInstance<T> : IFilter
    {
        public IEnumerable<T> State;

        public FilterInstance(IEnumerable<T> list)
        {
            State = list;
        }

        public KeyValuePair<int, object> Apply(Clause clause)
        {
            Dictionary<string, Func<string, IEnumerable<T>>> same_type_transforms = new Dictionary<string, Func<string, IEnumerable<T>>>()
            {
                {"where", (args) => State.Where(args) },
                {"reverse", (args) => State.Reverse() },
                {"take", (args) => State.Take(int.Parse(args)) },
                {"skip", (args) => State.Skip(int.Parse(args)) },
                {"sort", (args) => State.OrderBy(args) },
                {"sortdesc", (args) => State.OrderBy(args).Reverse() },
                {"distinct", (args) => State.Distinct() }
            };

            Dictionary<string, Func<string, object>> diff_type_transforms = new Dictionary<string, Func<string, object>>()
            {
                {"select", (args) => State.Select(args) }
            };

            Dictionary<string, Func<string, object>> result_transforms = new Dictionary<string, Func<string, object>>()
            {
                {"sum", (args) => State.Sum(i => Convert.ToDouble(i)) },
                {"average", (args) => State.Average(i => Convert.ToDouble(i)) },
                {"count", (args) => State.Count() },
                {"first", (args) => State.First() },
                {"last", (args) => State.Last() },
                {"max", (args) => State.Max() },
                {"min", (args) => State.Min() }
            };

            if(same_type_transforms.ContainsKey(clause.Method))
            {
                State = same_type_transforms[clause.Method](clause.Arguments);
            }
            else if(diff_type_transforms.ContainsKey(clause.Method))
            {
                var result = diff_type_transforms[clause.Method](clause.Arguments);
                var type = result.GetType().GetGenericArguments()[0];
                var open_type = typeof(FilterInstance<>);

                var actual_type = open_type.MakeGenericType(type);

                return new KeyValuePair<int, object>(0, Activator.CreateInstance(actual_type, result));
            }
            else if(result_transforms.ContainsKey(clause.Method))
            {
                return new KeyValuePair<int, object>(1, result_transforms[clause.Method](clause.Arguments));
            }

            return new KeyValuePair<int, object>(-1, State);
        }
    }

    public class Clause
    {
        public string Method { get; set; }
        public string Arguments { get; set; }

        public Clause(string method, string arguments)
        {
            Method = method;
            Arguments = arguments;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net;
using System.Web;

namespace Fun
{
    interface IResolver
    {
        string Name { get; }

        bool Matches(string URL);
        bool Ready(string URL);
        string GetSummary(string URL);
        string GetCacheID(string URL);
    }
}

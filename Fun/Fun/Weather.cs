using Newtonsoft.Json.Linq;
using Osiris;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Fun
{
    public class Weather
    {
        static WebClient Client = new WebClient();

        public static string TryGetSummary(double lat, double lng, string nick)
        {
            lock (Client)
            {
                string unit_code = Program.PrefersMetric(nick) ? "si" : "us";
                string cache_id = "weather:" + lat.ToString() + "," + lng.ToString() + "#" + unit_code;

                try
                {
                    var item = LinkResolver.Cache.Get(cache_id);

                    if (item != null)
                    {
                        return item.Content;
                    }

                    var raw = Client.DownloadString(string.Format("https://api.darksky.net/forecast/{0}/{1},{2}?units={3}", Config.GetString("weather.key"), lat, lng, unit_code));
                    var resp = JObject.Parse(raw);

                    //Console.WriteLine(raw);

                    string format = "{0}, current temperature: {1}, feels like {2}. {3}";

                    var currently = resp["currently"];
                    var minutely = resp["minutely"];
                    var hourly = resp["hourly"];
                    var daily = resp["daily"];

                    List<string> summaries = new List<string>();

                    if (minutely != null)
                        summaries.Add(minutely.Value<string>("summary"));

                    if (hourly != null)
                        summaries.Add(hourly.Value<string>("summary"));

                    if (daily != null)
                        summaries.Add(daily.Value<string>("summary"));

                    var ret = string.Format(format,
                        currently.Value<string>("summary"),
                        FormatTemperature(currently.Value<double>("temperature"), nick),
                        FormatTemperature(currently.Value<double>("apparentTemperature"), nick),
                        string.Join(" ", summaries));

                    LinkResolver.Cache.Add(cache_id, ret, TimeSpan.FromMinutes(10));
                    return ret;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.StackTrace);
                    throw;
                }
            }
        }

        public static string FormatTemperature(double value, string nick)
        {
            if(Program.PrefersMetric(nick))
            {
                //value = (value - 32) * (5d / 9d);
                return string.Format("{0:0.}°C", value);
            }
            else
            {
                return string.Format("{0:0.}°F", value);
            }
        }
    }
}

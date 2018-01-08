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
    public class Geocoder
    {
        static WebClient Client = new WebClient();

        public static Tuple<double, double> GetLatLong(string human)
        {
            lock (Client)
            {
                human = HttpUtility.UrlEncode(human.ToLower());

                try
                {
                    var item = LinkResolver.Cache.Get("geocoder:" + human);

                    if (item != null)
                    {
                        var parts = item.Content.Split(',').Select(double.Parse).ToArray();
                        return new Tuple<double, double>(parts[0], parts[1]);
                    }

                    var raw = Client.DownloadString(string.Format("https://maps.googleapis.com/maps/api/geocode/json?address={0}&key={1}", human, Config.GetString("geocoding.key")));
                    var resp = JObject.Parse(raw);

                    Console.WriteLine(raw);

                    if (resp.Value<string>("status") != "OK")
                        return null;

                    var location = resp["results"][0]["geometry"]["location"];

                    LinkResolver.Cache.Add("geocoder:" + human, location.Value<double>("lat").ToString() + "," + location.Value<double>("lng").ToString(), TimeSpan.FromDays(365));

                    return new Tuple<double, double>(location.Value<double>("lat"), location.Value<double>("lng"));
                }
                catch (Exception ex)
                {
                    return null;
                }
            }
        }
    }
}

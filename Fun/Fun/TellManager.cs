using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace Fun
{
    [Serializable]
    public class Tell
    {
        public string From { get; set; }
        public string To { get; set; }
        public string Message { get; set; }
        public bool Expired { get; set; }
        public DateTime Time { get; set; }

        public Tell()
        {

        }

        public override string ToString()
        {
            return string.Format("{0}: [10{2}] [05{1}] [03{3} ago]", To, From, Message, Utilities.TimeSpanToPrettyString(DateTime.UtcNow - Time));
        }
    }

    public class TellManager
    {
        public static List<Tell> Tells = new List<Tell>();

        public static void Load(string path = "./tell")
        {
            BinaryFormatter formatter = new BinaryFormatter();
            var stream = File.OpenRead(path);

            try
            {
                Tells = (List<Tell>)formatter.Deserialize(stream);
            }
            catch (Exception ex)
            {
                throw;
            }
            finally
            {
                stream.Close();
            }
        }

        public static void Expire(Tell tell)
        {
            if (!Tells.Contains(tell))
                return;

            Tells.First(t => t == tell).Expired = true;
        }

        public static void Tell(string from, string to, string message)
        {
            var tell = new Tell() { From = from, To = to, Message = message, Time = DateTime.UtcNow };
            Tells.Add(tell);
            Save();
        }

        public static IEnumerable<Tell> GetTells(string nick)
        {
            return Tells.Where(t => t.To.ToLower() == nick.ToLower() && !t.Expired);
        }

        public static void Save()
        {
            BinaryFormatter formatter = new BinaryFormatter();
            var stream = File.OpenWrite("./tell.tmp");

            formatter.Serialize(stream, Tells);

            stream.Close();
            stream = File.OpenRead("./tell.tmp");

            if(formatter.Deserialize(stream) == Tells)
            {
                throw new Exception("Inconsistent database");
            }
            else
            {
                stream.Close();
                File.Copy("./tell.tmp", "./tell", true);
            }
        }
    }
}

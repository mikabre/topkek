using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace Osiris
{
    public class Config
    {
        private static Dictionary<string, object> KeyStore = null;

        public static void Init()
        {
            KeyStore = new Dictionary<string, object>();
        }

        public static void Load(string path = "./config.json")
        {
            var stream = new StreamReader(path);
            try
            {
                var serializer = new JsonSerializer();
                

                KeyStore = (Dictionary<string, object>)serializer.Deserialize(stream, typeof(Dictionary<string, object>));
            }
            catch
            {
                KeyStore = null;
                throw new Exception("Couldn't load configuration file");
            }
            finally
            {
                stream.Close();
            }
        }

        public static T GetValue<T>(string key)
        {
            if (KeyStore == null)
                return default(T);
            
            return (T)Convert.ChangeType(KeyStore[key], typeof(T));
        }

        public static string GetString(string key)
        {
            return GetValue<string>(key);
        }

        public static int GetInt(string key)
        {
            return GetValue<int>(key);
        }

        public static double GetDouble(string key)
        {
            return GetValue<double>(key);
        }

        public static void SetValue(string key, object value)
        {
            KeyStore[key] = value;
        }

        public static void Save(string path = "./config.json")
        {
            var stream = new StreamWriter(path);
            var writer = new JsonTextWriter(stream);

            writer.Indentation = 4;
            writer.Formatting = Formatting.Indented;

            try
            {
                var serializer = new JsonSerializer();
                
                serializer.Serialize(writer, KeyStore);
            }
            catch
            {
                throw;
            }
            finally
            {
                writer.Close();
                stream.Close();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace Osiris
{
    public class ConnectionOptions
    {
        public string Server { get; set; }
        public int Port { get; set; }
        public string Nickname { get; set; }
        public string Password { get; set; }
        public string Owner { get; set; }
        public bool NickServ { get; set; }
        public bool Ssl { get; set; }
        public bool ZncLogin { get; set; }
        public string ZncPassword { get; set; }
        public string ZncUsername { get; set; }
        public string ZncNetwork { get; set; }
        public List<string> Autojoin { get; set; }

        public ConnectionOptions()
        {
            Nickname = Config.GetString("irc.default.nick");
            Port = Config.GetInt("irc.default.port");
            Password = Config.GetString("irc.default.password");
            Owner = Config.GetString("irc.default.owner");
            NickServ = true;
        }

        public ConnectionOptions(string host) : this()
        {
            Server = host;
        }

        public static ConnectionOptions FromFile(string filename)
        {
            var stream = new StreamReader(filename);
            ConnectionOptions ret = new ConnectionOptions("");

            try
            {
                var serializer = new JsonSerializer();
                ret = (ConnectionOptions)serializer.Deserialize(stream, typeof(ConnectionOptions));
            }
            catch (Exception ex)
            {
                throw new Exception("Couldn't load connection options from file \"" + filename + "\"", ex);
            }
            finally
            {
                stream.Close();
            }

            return ret;
        }

        public void Save(string file)
        {
            var serializer = new JsonSerializer();
            var stream = new StreamWriter(file);
            var writer = new JsonTextWriter(stream);

            writer.Formatting = Formatting.Indented;
            writer.Indentation = 4;

            serializer.Serialize(writer, this);

            writer.Close();
            stream.Close();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Security.Cryptography;

using System.Threading;

using ChatSharp;

namespace Osiris
{
    public class TokenManager
    {
        public Dictionary<Token, MessageSource> Dictionary = new Dictionary<Token, MessageSource>();

        public TokenManager()
        {
            Task.Factory.StartNew(PurgeLoop);
        }

        public Token GetToken(MessageSource source)
        {
            if (Dictionary.ContainsValue(source))
            {
                var tokens = Dictionary.Where(p => p.Value.Equals(source) && (p.Key.Expiration - DateTime.Now).TotalSeconds > 30);

                if (tokens.Any())
                {
                    return tokens.First().Key;
                }
            }

            var token = new Token(source);
            Dictionary.Add(token, source);

            Console.WriteLine("Added token {0} for ({1}:{2}), expires at {3}", token.Key, source.Client.ServerAddress, source.Source, token.Expiration);

            return token;
        }

        public MessageSource GetSource(string token)
        {
            var pairs = Dictionary.Where(p => p.Key.Key == token);

            if (!pairs.Any())
            {
                Console.WriteLine("Invalid token {0}", token);
                return null;
            }

            return pairs.First().Value;
        }

        private void PurgeLoop()
        {
            while (true)
            {
                try
                {
                    var list = Dictionary.Where(p => p.Key.Expiration < DateTime.Now).ToList();
                    list.ForEach(p => { Dictionary.Remove(p.Key); Console.WriteLine("Purged {0}", p.Key.Key); });
                }
                catch
                {
                    Console.WriteLine("Couldn't purge tokens!");
                }

                Thread.Sleep(1000);
            }
        }
    }

    public class Token
    {
        public static RNGCryptoServiceProvider Random = new RNGCryptoServiceProvider();

        public string Key { get; set; }
        public DateTime Expiration { get; set; }
        public MessageSource Source { get; set; }

        public Token(MessageSource source)
        {
            Expiration = DateTime.Now.AddMinutes(1);

            byte[] rnd = new byte[8];
            Random.GetBytes(rnd);

            byte[] key = new byte[16];
            source.GetHash().CopyTo(key, 0);
            rnd.CopyTo(key, 8);

            Key = BitConverter.ToString(key).ToLower().Replace("-", "");

            if (source.Notice)
                Key = Key.Substring(0, Key.Length - 1) + 'n';

            Source = source;
        }
    }

    public class MessageSource
    {
        public IrcClient Client;
        public string Source;
        public bool Notice;

        public MessageSource(IrcClient client, string source, bool notice = false)
        {
            Client = client;
            Source = source;
            Notice = notice;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is MessageSource))
                return false;

            var source = obj as MessageSource;

            if (source.Client.ServerAddress == Client.ServerAddress &&
                source.Source == Source)
                return true;

            return false;
        }

        public byte[] GetHash()
        {
            MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();
            return md5.ComputeHash(Encoding.ASCII.GetBytes(Client.ServerAddress + Source)).Take(8).ToArray();
        }

        public override int GetHashCode()
        {
            return BitConverter.ToInt32(GetHash(), 0);
        }
    }
}

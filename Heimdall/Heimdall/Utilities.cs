using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.Security.Cryptography;

namespace Heimdall
{
    public static class Utilities
    {
        public static byte[] GetChecksum(byte[] data)
        {
            SHA256CryptoServiceProvider sha = new SHA256CryptoServiceProvider();
            return sha.ComputeHash(data);
        }

        public static string ReadString(this Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream);

            int str_len = reader.ReadInt32();
            byte[] str_data = reader.ReadBytes(str_len);

            return Encoding.Unicode.GetString(str_data);
        }

        public static void WriteString(this Stream stream, string str)
        {
            BinaryWriter writer = new BinaryWriter(stream);

            byte[] str_data = Encoding.Unicode.GetBytes(str);

            writer.Write(str_data.Length);
            writer.Write(str_data);

            writer.Flush();
        }

        public static string ToString(byte[] data)
        {
            MemoryStream ms = new MemoryStream(data);

            return ms.ReadString();
        }

        public static byte[] ToBytes(string data)
        {
            MemoryStream ms = new MemoryStream();

            ms.WriteString(data);
            ms.Close();

            return ms.ToArray();
        }
    }
}

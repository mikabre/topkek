using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.Diagnostics;

namespace Heimdall
{
    public class Message
    {
        public const int CHECKSUM_SIZE = 32;
        public const uint VERSION = 1;

        public uint Version { get; set; }

        public string Source { get; set; }
        public string Destination { get; set; }

        public string MessageType { get; set; }

        public byte[] Checksum { get; set; }

        public byte[] Data { get; set; }

        public bool Valid { get; internal set; }

        public Message()
        {
            Version = VERSION;
            Data = new byte[0];
        }

        public Message(byte[] data)
            : this()
        {
            Data = data;
            Checksum = Utilities.GetChecksum(Data);
        }

        public Message Clone(bool swap_addr = false)
        {
            Message ret = new Message(this.Serialize());

            if (swap_addr)
            {
                ret.Source = this.Destination;
                ret.Destination = this.Source;
            }

            return ret;
        }

        public static Message Consume(Stream stream, bool strict = false)
        {
            try
            {
                Message msg = new Message();

                BinaryReader reader = new BinaryReader(stream);

                msg.Version = reader.ReadUInt32();

                msg.Source = reader.BaseStream.ReadString();
                msg.Destination = reader.BaseStream.ReadString();

                msg.MessageType = reader.BaseStream.ReadString();

                msg.Checksum = reader.ReadBytes(CHECKSUM_SIZE);

                int len = reader.ReadInt32();
                msg.Data = reader.ReadBytes(len);

                msg.Verify(strict);

                return msg;
            }
            catch
            {
                return new Message() { Valid = false };
            }
        }

        public static Message Parse(byte[] raw, bool strict = false)
        {
            return Consume(new MemoryStream(raw));   
        }

        public byte[] Serialize()
        {
            Checksum = Utilities.GetChecksum(Data);

            MemoryStream ms = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(ms);

            writer.Write(Version);

            writer.BaseStream.WriteString(Source);
            writer.BaseStream.WriteString(Destination);

            writer.BaseStream.WriteString(MessageType);

            writer.Write(Checksum);

            writer.Write(Data.Length);
            writer.Write(Data);

            writer.Flush();
            writer.Close();

            return ms.ToArray();
        }

        internal void Verify(bool strict)
        {
            Valid = true;

            Check(Utilities.GetChecksum(Data).SequenceEqual(Checksum), "Invalid checksum", strict);
            Check(Version == VERSION, "Protocol version mismatch", strict);
        }

        internal void Check(bool condition, string message, bool strict)
        {
            if (strict)
                Debug.Assert(condition, message);
            else
                if (!condition)
                    Valid = false;
        }
    }
}

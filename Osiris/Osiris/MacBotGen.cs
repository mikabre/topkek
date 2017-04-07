using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Osiris
{
    class MacBotGen
    {
        public static List<string> Words = new List<string>()
        {
            "beep",
            "boop",
            "boopity",
            "beepity",
            "zippity",
            "zip",
            "zap",
            "bap",
            "brrap",
            "booop",
            "beeeep",
            "booooop",
            "mark",
            "wark"
        };

        public static List<string> CatWords = new List<string>()
        {
            "mew",
            "meow",
            "mow",
            "whiskers",
            "mrowr",
            "mrew",
            "nyan",
            "marf"
        };

        public static Random Random = new Random();

        public static string GenerateMacBot(int length, bool cat = false)
        {
            string ret = "";

            while(length > 0)
            {
                int run = Random.Next(length);

                while (run > 5)
                    run = Random.Next(length);

                if (length < 5)
                    run = length;

                length -= run;
                string word = Words[Random.Next(Words.Count)];

                if(cat)
                {
                    word = CatWords[Random.Next(CatWords.Count)];
                }

                for(int i = 0; i < run; i++)
                {
                    //ret += word + (Random.Next(10) < 7 ? " " : "  ");

                    if (Random.NextDouble() > 0.5)
                        ret += word.ToUpper();
                    else
                        ret += word;

                    if (Random.NextDouble() > 0.5)
                        ret += "!";

                    if (Random.NextDouble() > 0.5)
                        ret += "~";

                    if (Random.NextDouble() > 0.7)
                        ret += " ";
                    else
                        ret += "  ";
                }
            }

            return Colorify(Symbolify(ret));
        }

        public static string Colorify(string input)
        {
            string c = GetRandomIRCColor();
            input = c + input;

            for(int i = c.Length; i < input.Length; i++)
            {
                if (Random.Next(100) < 10)
                {
                    string color = GetRandomIRCColor();
                    input = input.Insert(i, color);

                    i += color.Length;
                }
            }

            return input;
        }

        public static string Symbolify(string input)
        {
            string bold = "";
            string italics = "";
            string underline = "";

            for (int i = 0; i < input.Length; i++)
            {
                if(Random.Next(100) < 10)
                {
                    double d = Random.NextDouble();

                    if (d < 0.3)
                        input = input.Insert(i, bold);
                    else if (d < 0.7)
                        input = input.Insert(i, underline);
                    else
                        input = input.Insert(i, italics);

                    i++;
                }
            }

            return input;
        }

        public static string GetRandomIRCColor()
        {
            return string.Format("{0},{1}", Random.Next(16), Random.Next(16));
        }
    }
}

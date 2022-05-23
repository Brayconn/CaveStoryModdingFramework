using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CaveStoryModdingFramework.Compatability.CaveStoryPlus
{
    public class WaterLine
    {
        //Each of those (\d+) is one (unsigned) byte
        public static Regex LineRegex = new Regex(@"(\d+):(\d+)(?::\[(\d+),\s+(\d+),\s+(\d+),\s+(\d+)\])+");

        //TODO if someone ever wanted to use the pxw file format with 16 bit tiles this wouldn't work
        public byte StartTile { get; set; } = 0;
        public byte EndTile { get; set; } = 255;

        public List<Color> Colors = new List<Color>(3);

        public WaterLine()
        {
            Colors = new List<Color>()
            {
                Color.Blue,
                Color.DarkBlue,
                Color.Black
            };
        }
        public WaterLine(string definition)
        {
            if (LineRegex.IsMatch(definition))
            {
                var m = LineRegex.Match(definition);
                StartTile = byte.Parse(m.Groups[1].Value);
                EndTile = byte.Parse(m.Groups[2].Value);

                for (int i = 0; i < m.Groups[3].Captures.Count; i++)
                {
                    Colors.Add(Color.FromArgb(
                        //note we're converting from RGBA to ARGB with this arg order
                        byte.Parse(m.Groups[6].Captures[i].Value),
                        byte.Parse(m.Groups[3].Captures[i].Value),
                        byte.Parse(m.Groups[4].Captures[i].Value),
                        byte.Parse(m.Groups[5].Captures[i].Value)
                        ));
                }
            }
            else
                throw new ArgumentException("Invalid definition!", nameof(definition));
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.Append(StartTile + ":" + EndTile);
            foreach(var c in Colors)
                builder.Append($":[{c.R}, {c.G}, {c.B}, {c.A}]");
            return builder.ToString();
        }
    }
    public static class WaterAttributes
    {
        public const string Extension = "pxw";

        public static List<WaterLine> Read(string path)
        {
            var output = new List<WaterLine>();
            using(var sr = new StreamReader(path))
            {
                while (!sr.EndOfStream)
                {
                    var line = sr.ReadLine();
                    if(!string.IsNullOrWhiteSpace(line))
                        output.Add(new WaterLine(line));
                }
            }
            return output;
        }

        public static void Write(string path, List<WaterLine> lines)
        {
            using(var sw = new StreamWriter(path, false))
            {
                foreach(var line in lines)
                {
                    sw.WriteLine(line.ToString());
                }
            }
        }
    }
}

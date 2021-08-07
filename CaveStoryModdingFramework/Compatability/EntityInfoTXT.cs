using System.Collections.Generic;
using System.Drawing;
using System.IO;
using CaveStoryModdingFramework.Entities;

namespace CaveStoryModdingFramework
{
    public static class EntityInfoTXT
    {
        public const int NumberIndex = 0;
        public const int Short1Index = 1;
        public const int Short2Index = 2;
        public const int LongIndex   = 3;
        public const int RectIndex   = 4;
        public const int DescriptionIndex = 5;
        public const int CategoryIndex = 6;

        public static Rectangle ReadRect(string rect, int divisor = 2)
        {
            var coords = rect.Split(':');
            return Rectangle.FromLTRB(int.Parse(coords[0]) / divisor, int.Parse(coords[1]) / divisor, int.Parse(coords[2]) / divisor, int.Parse(coords[3]) / divisor);
        }
        public static string ToRect(this Rectangle rect, int multiplier = 2)
        {
            return string.Join(":", rect.Left * multiplier, rect.Top * multiplier, rect.Right * multiplier, rect.Bottom * multiplier);
        }

        public static Dictionary<int, EntityInfo> Load(string path, int divisor = 2)
        {
            var output = new Dictionary<int, EntityInfo>();
            using(var sr = new StreamReader(path))
            {
                while (!sr.EndOfStream)
                {
                    var line = sr.ReadLine().Split('\t');
                    if (line[0].StartsWith("//"))
                        continue;
                    //Substring(1) to igore the #
                    var num = int.Parse(line[NumberIndex].Substring(1));
                    var short1 = line[Short1Index];
                    var short2 = line[Short2Index];
                    var longStr = line[LongIndex];
                    var rect = ReadRect(line[RectIndex], divisor);
                    var desc = line[DescriptionIndex];
                    var category = line[CategoryIndex];

                    output.Add(num, new EntityInfo(longStr, rect, category));
                }
            }
            return output;
        }

        /*
        public static void Save<T>(string path, T dict, int multiplier = 2) where T : IDictionary<int, EntityInfo>
        {
            using(var sw = new StreamWriter(path))
            {

            }
        }
        */

        public static void Merge<T>(string path, T dict, params string[] Names) where T : IDictionary<int, EntityInfo>
        {
            Merge(path, dict, 2, Names);
        }
        public static void Merge<T>(string path, T dict, int multiplier, params string[] Names) where T : IDictionary<int, EntityInfo>
        {
            var lines = File.ReadAllLines(path);
            for(int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("//"))
                    continue;
                var split = lines[i].Split('\t');
                var num = int.Parse(split[NumberIndex].Substring(1));
                if (!dict.TryGetValue(num, out var ent))
                    continue;
                foreach (var name in Names)
                {
                    switch (name)
                    {
                        case nameof(EntityInfo.Name):
                            split[LongIndex] = ent.Name;
                            break;
                        case nameof(EntityInfo.SpriteLocation):
                            split[RectIndex] = ent.SpriteLocation.ToRect(multiplier);
                            break;
                    }
                }
                lines[i] = string.Join("\t", split);
            }
            File.WriteAllLines(path, lines);
        }
    }
}

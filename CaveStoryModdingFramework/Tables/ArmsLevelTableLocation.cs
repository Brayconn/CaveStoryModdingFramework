using System;
using System.Collections.Generic;
using System.IO;

namespace CaveStoryModdingFramework
{
    public class ArmsLevelEntry : PropertyChangedHelper
    {
        public List<int> Levels { get; set; }
        public ArmsLevelEntry(int levelCount)
        {
            Levels = new List<int>(levelCount);
        }
    }
    public class ArmsLevelTableLocation : DataLocation
    {
        public const string ARMS_LEVELTABLE = "arms_level.tbl";
        private int armsCount = 14, levelCount = 3;
        
        public int ArmsCount { get => armsCount; set => SetVal(ref armsCount, value); }
        public int LevelCount { get => levelCount; set => SetVal(ref levelCount, value); }
        public int EntrySize => LevelCount * sizeof(int);

        public ArmsLevelTableLocation() { }
        public ArmsLevelTableLocation(string filename)
        {
            Filename = filename;
        }

        public List<ArmsLevelEntry> Read()
        {
            var count = ArmsCount;
            if (DataLocationType == DataLocationTypes.External)
                count = (int)(new FileInfo(Filename).Length / EntrySize);
            var output = new List<ArmsLevelEntry>(count);
            using(var br = new BinaryReader(GetStream(FileMode.Open, FileAccess.Read)))
            {
                for(int i = 0; i < count; i++)
                {
                    var entry = new ArmsLevelEntry(LevelCount);
                    for(int j = 0; j < LevelCount; j++)
                    {
                        entry.Levels.Add(br.ReadInt32());
                    }
                }
            }
            return output;
        }

        public void Write(IList<ArmsLevelEntry> entries)
        {
            var buff = new byte[entries.Count * (sizeof(int) * LevelCount)];
            using (var bw = new BinaryWriter(new MemoryStream(buff)))
            {
                foreach (var entry in entries)
                {
                    if (entry.Levels.Count != LevelCount)
                        throw new ArgumentException("Invalid number of levels!", nameof(entry.Levels));
                    foreach (var weapon in entry.Levels)
                    {
                        bw.Write(weapon);
                    }
                }
            }
            Write(buff);
        }

        public override bool Equals(object obj)
        {
            if(obj is ArmsLevelTableLocation altl)
            {
                return base.Equals(altl)
                    && ArmsCount == altl.ArmsCount
                    && LevelCount == altl.LevelCount;
            }
            else 
                return base.Equals(obj);
        }
    }
}

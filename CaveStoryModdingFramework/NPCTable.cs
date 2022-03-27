using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Xml.Linq;

namespace CaveStoryModdingFramework.Entities
{
    public class NPCTableEntry : PropertyChangedHelper
    {
        public const int Size = 0x18;

        private EntityFlags bits;
        private ushort life;
        private byte spriteSurface, hitSound, deathSound, smokeSize;
        private int xp, damage;
        private NPCHitRect hitbox;
        private NPCViewRect viewbox;
        public EntityFlags Bits { get => bits; set => SetVal(ref bits, value); }
        public ushort Life { get => life; set => SetVal(ref life, value); }
        
        public byte SpriteSurface { get => spriteSurface; set => SetVal(ref spriteSurface, value); }
        public byte HitSound { get => hitSound; set => SetVal(ref hitSound, value); }
        public byte DeathSound { get => deathSound; set => SetVal(ref deathSound, value); }
        public byte SmokeSize { get => smokeSize; set => SetVal(ref smokeSize, value); }

        public int XP { get => xp; set => SetVal(ref xp, value); }
        public int Damage { get => damage; set => SetVal(ref damage, value); }

        [TypeConverter(typeof(ByteRectTypeConverter))]
        public NPCHitRect Hitbox { get => hitbox; set => SetVal(ref hitbox, value); }

        //despite sharing the same type as Hitbox (in the original game)
        //the values here are treated completely different, hence the seperate type
        [TypeConverter(typeof(ByteRectTypeConverter))]
        public NPCViewRect Viewbox { get => viewbox; set => SetVal(ref viewbox, value); }

        public NPCTableEntry()
        {
            Hitbox = new NPCHitRect();
            Viewbox = new NPCViewRect();
        }
    }

    public enum NPCTableFormats
    {
        ByType, //the mode used by 99.9% of all npc.tbls
        ByEntry //what DSi-ware uses becuase it's the internal representation
    }

    public class NPCTableLocation : DataLocation
    {
        NPCTableFormats npcTableFormat;
        int npcCount;
        public NPCTableFormats NpcTableFormat { get => npcTableFormat; set => SetVal(ref npcTableFormat, value); }
        public int NpcCount { get => npcCount; set => SetVal(ref npcCount, value); }

        public NPCTableLocation() { }
        public NPCTableLocation(string path)
        {
            Filename = path;
            DataLocationType = DataLocationTypes.External;
        }

        public override XElement ToXML(string elementName, string relativeBase)
        {
            var x = base.ToXML(elementName, relativeBase);
            x.Add(
                new XElement(nameof(NpcTableFormat), NpcTableFormat),
                new XElement(nameof(NpcCount), NpcCount)
                );
            return x;
        }
    }

    public static class NPCTable
    {
        public const string NPCTBL = "npc.tbl";
        public static string NPCTBLFilter = $"{Dialog.NPCTable} ({NPCTBL})|{NPCTBL}";

        public static List<NPCTableEntry> ReadNPCTableByType(this BinaryReader br, int npcCount)
        {
            //Unfortunately, initializing an array of classes like NPCTableEntry[npcCount]
            //just gives you a really long array of null
            var npcTable = new List<NPCTableEntry>(npcCount);
            while (npcTable.Count < npcCount)
                npcTable.Add(new NPCTableEntry());

            //something something saving on variable delcarations something something
            int i;
            void ReadSequence(Action<NPCTableEntry> act)
            {
                for (i = 0; i < npcCount; i++)
                    act(npcTable[i]);
            }
            ReadSequence((e) => e.Bits = (EntityFlags)br.ReadUInt16());
            ReadSequence((e) => e.Life = br.ReadUInt16());
            ReadSequence((e) => e.SpriteSurface = br.ReadByte());
            ReadSequence((e) => e.DeathSound = br.ReadByte());
            ReadSequence((e) => e.HitSound = br.ReadByte());
            ReadSequence((e) => e.SmokeSize = br.ReadByte());
            ReadSequence((e) => e.XP = br.ReadInt32());
            ReadSequence((e) => e.Damage = br.ReadInt32());
            ReadSequence((e) => e.Hitbox = br.ReadHitRect());
            ReadSequence((e) => e.Viewbox = br.ReadViewRect());

            return npcTable;
        }
        public static NPCTableEntry ReadNPCTableEntry(this BinaryReader br)
        {
            return new NPCTableEntry()
            {
                Bits = (EntityFlags)br.ReadUInt16(),
                Life = br.ReadUInt16(),
                SpriteSurface = br.ReadByte(),
                HitSound = br.ReadByte(),
                DeathSound = br.ReadByte(),
                XP = br.ReadInt32(),
                Damage = br.ReadInt32(),
                Hitbox = br.ReadHitRect(),
                Viewbox = br.ReadViewRect()
            };
        }
        public static List<NPCTableEntry> ReadNPCTableByEntry(this BinaryReader br, int npcCount)
        {
            var npcTable = new List<NPCTableEntry>(npcCount);
            for (int i = 0; i < npcCount; i++)
                npcTable.Add(br.ReadNPCTableEntry());
            return npcTable;
        }

        public static List<NPCTableEntry> Load(NPCTableLocation location)
        {
            if (!location.TryCalculateEntryCount(NPCTableEntry.Size, out int npcCount))
                npcCount = location.NpcCount;
            using (var br = new BinaryReader(location.GetStream(FileMode.Open, FileAccess.Read)))
            {
                switch(location.NpcTableFormat)
                {
                    case NPCTableFormats.ByType:
                        return br.ReadNPCTableByType(npcCount);
                    case NPCTableFormats.ByEntry:
                        return br.ReadNPCTableByEntry(npcCount);
                    default:
                        throw new ArgumentException("Invalid NPC Table format!", nameof(location.NpcTableFormat));
                }
            }
        }

        public static void Write(this BinaryWriter bw, NPCTableEntry entry)
        {
            bw.Write((ushort)entry.Bits);
            bw.Write(entry.Life);
            bw.Write(entry.SpriteSurface);
            bw.Write(entry.HitSound);
            bw.Write(entry.DeathSound);
            bw.Write(entry.SmokeSize);
            bw.Write(entry.XP);
            bw.Write(entry.Damage);
            bw.Write((int)entry.Hitbox);
            bw.Write((int)entry.Viewbox);
        }

        public static void WriteNPCTableByEntry(this BinaryWriter bw, IList<NPCTableEntry> entries)
        {
            foreach (var item in entries)
                bw.Write(item);
        }

        public static void WriteNPCTableByType(this BinaryWriter bw, IList<NPCTableEntry> table)
        {
            int i;
            void WriteSequence(Action<NPCTableEntry> act)
            {
                for (i = 0; i < table.Count; i++)
                    act(table[i]);
            }
            WriteSequence((e) => bw.Write((ushort)e.Bits));
            WriteSequence((e) => bw.Write(e.Life));
            WriteSequence((e) => bw.Write(e.SpriteSurface));
            WriteSequence((e) => bw.Write(e.DeathSound));
            WriteSequence((e) => bw.Write(e.HitSound));
            WriteSequence((e) => bw.Write(e.SmokeSize));
            WriteSequence((e) => bw.Write(e.XP));
            WriteSequence((e) => bw.Write(e.Damage));
            WriteSequence((e) => bw.Write((int)e.Hitbox));
            WriteSequence((e) => bw.Write((int)e.Viewbox));
        }

        public static void Save(IList<NPCTableEntry> table, NPCTableLocation location)
        {
            var buffer = new byte[table.Count * NPCTableEntry.Size];
            using(var bw = new BinaryWriter(new MemoryStream(buffer)))
            {
                switch(location.NpcTableFormat)
                {
                    case NPCTableFormats.ByType:
                        bw.WriteNPCTableByType(table);
                        break;
                    case NPCTableFormats.ByEntry:
                        bw.WriteNPCTableByEntry(table);
                        break;
                }
            }
            DataLocation.Write(location, buffer);
        }
    }
}

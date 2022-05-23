using CaveStoryModdingFramework.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace CaveStoryModdingFramework
{
    public enum BulletTablePresets
    {
        doukutsuexe,
        csplus
    }
    [Flags]
    public enum BulletFlags : uint
    {
        PierceTiles = 0x04,
        CollideWithTiles = 0x08,
        PierceInvincibleEntities = 0x10,
        BreakSnacks = 0x20,
        PierceSnacks = 0x40
    }

    public class BulletTableEntry : PropertyChangedHelper
    {
        public const int UnpaddedSize = 42;
        public const int PaddedSize = 44;

        sbyte damage, hits;
        int range, enemyHitboxWidth, enemyHitboxHeight, tileHitboxWidth, tileHitboxHeight;
        BulletFlags bits;
        BulletViewRect viewBox = new BulletViewRect(0,0,0,0);

        public sbyte Damage { get => damage; set => SetVal(ref damage, value); }
        //Life
        public sbyte Hits { get => hits; set => SetVal(ref hits, value); }
        //Life_count
        public int Range { get => range; set => SetVal(ref range, value); }

        public BulletFlags Bits { get => bits; set => SetVal(ref bits, value); }
        //EnemyXL
        public int EnemyHitboxWidth { get => enemyHitboxWidth; set => SetVal(ref enemyHitboxWidth, value); }
        //EnemyYL

        public int EnemyHitboxHeight { get => enemyHitboxHeight; set => SetVal(ref enemyHitboxHeight, value); }
        //BlockXL
        public int TileHitboxWidth { get => tileHitboxWidth; set => SetVal(ref tileHitboxWidth, value); }
        //BlockYL
        public int TileHitboxHeight { get => tileHitboxHeight; set => SetVal(ref tileHitboxHeight, value); }

        [TypeConverter(typeof(BullletViewRectTypeConverter))]
        public BulletViewRect ViewBox { get => viewBox; set => SetVal(ref viewBox, value); }
    }
    public class BulletTableLocation : DataLocation
    {
        public const string BULLETTABLE = "bullet.tbl";
        public static string BulletTableFilter = $"Bullet Table ({BULLETTABLE})|{BULLETTABLE}";

        public const int CSBulletTableAddress = 0x8F048;
        public const int CSBulletTableCount = 46;
        public const int CSBulletTableSize = CSBulletTableCount * BulletTableEntry.PaddedSize;

        int bulletCount;
        bool padDamageAndHits;
        public int BulletCount { get => bulletCount; set => SetVal(ref bulletCount, value); }
        public bool PadDamageAndHits { get => padDamageAndHits; set => SetVal(ref padDamageAndHits, value); }

        public int EntrySize => PadDamageAndHits ? BulletTableEntry.PaddedSize : BulletTableEntry.UnpaddedSize;

        public BulletTableLocation()
        {

        }
        public BulletTableLocation(BulletTablePresets preset)
        {
            ResetToDefault(preset);
        }
        public BulletTableLocation(string filename, BulletTablePresets preset)
        {
            Filename = filename;
            ResetToDefault(preset);
        }
        public void ResetToDefault(BulletTablePresets preset)
        {
            switch (preset)
            {
                case BulletTablePresets.doukutsuexe:
                    DataLocationType = DataLocationTypes.Internal;
                    SectionName = "";
                    Offset = CSBulletTableAddress;
                    MaximumSize = CSBulletTableSize;
                    FixedSize = true;
                    BulletCount = CSBulletTableCount;
                    PadDamageAndHits = true;
                    break;
                case BulletTablePresets.csplus:
                    DataLocationType = DataLocationTypes.External;
                    SectionName = "";
                    Offset = 0;
                    MaximumSize = 0;
                    FixedSize = false;
                    BulletCount = CSBulletTableCount;
                    PadDamageAndHits = false;
                    break;
            }
        }

        public List<BulletTableEntry> Read()
        {
            var count = BulletCount;
            if (DataLocationType == DataLocationTypes.External)
                count = (int)(new FileInfo(Filename).Length / EntrySize);
            var output = new List<BulletTableEntry>(count);
            using (var br = new BinaryReader(GetStream(FileMode.Open, FileAccess.Read)))
            {
                for (int i = 0; i < count; i++)
                {
                    var entry = new BulletTableEntry()
                    {
                        Damage = br.ReadSByte(),
                        Hits = br.ReadSByte()
                    };
                    if (PadDamageAndHits)
                        br.BaseStream.Position += 2;
                    entry.Range = br.ReadInt32();
                    entry.Bits = (BulletFlags)br.ReadUInt32();
                    entry.EnemyHitboxWidth = br.ReadInt32();
                    entry.EnemyHitboxHeight = br.ReadInt32();
                    entry.TileHitboxWidth = br.ReadInt32();
                    entry.TileHitboxHeight = br.ReadInt32();
                    entry.ViewBox = br.ReadIntRect();

                    output.Add(entry);
                }
            }
            return output;
        }

        public void Write(IList<BulletTableEntry> bullets)
        {
            var buff = new byte[bullets.Count * EntrySize];
            using (var bw = new BinaryWriter(new MemoryStream(buff)))
            {
                foreach (var bullet in bullets)
                {
                    bw.Write(bullet.Damage);
                    bw.Write(bullet.Hits);
                    if (PadDamageAndHits) //I'm so clever 😎
                        bw.Write((ushort)0);
                    bw.Write(bullet.Range);
                    bw.Write((uint)bullet.Bits);
                    bw.Write(bullet.EnemyHitboxWidth);
                    bw.Write(bullet.EnemyHitboxHeight);
                    bw.Write(bullet.TileHitboxWidth);
                    bw.Write(bullet.TileHitboxHeight);
                    bw.Write(bullet.ViewBox);
                }
            }
            Write(buff);
        }

        public override bool Equals(object obj)
        {
            if(obj is BulletTableLocation btl)
            {
                return base.Equals(btl)
                    && BulletCount == btl.BulletCount
                    && PadDamageAndHits == btl.PadDamageAndHits;
            }
            else 
                return base.Equals(obj);
        }
    }
}

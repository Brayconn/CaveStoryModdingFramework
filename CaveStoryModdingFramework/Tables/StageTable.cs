using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using PETools;
using System.Linq;
using System.ComponentModel;
using System.Globalization;
using System.Xml.Linq;
using System.Xml;
using System.Xml.Serialization;
using System.Xml.Schema;
using System.Diagnostics;
using CaveStoryModdingFramework.Utilities;

namespace CaveStoryModdingFramework.Stages
{
    public enum StageTableFormats
    {
        normal,
        swdata,
        mrmapbin,
    }
    public enum StageTablePresets
    {
        custom = -1,
        doukutsuexe = 0,
        swdata,
        csmap,
        stagetbl,
        mrmapbin
    }

    #region type converters

    public class EncodingTypeConverter : TypeConverter
    {
        static readonly HashSet<Encoding> encounteredEncodings = new HashSet<Encoding>();
        static StandardValuesCollection AvailableEncodings = null;
        public override bool GetStandardValuesSupported(ITypeDescriptorContext context) => true;
        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            if (AvailableEncodings == null)
            {
                //*
                foreach (var enc in Encoding.GetEncodings().Select(x => x.GetEncoding()))
                    encounteredEncodings.Add(enc);
                /*/
                var baseEncoding = typeof(Encoding);
                foreach (var prop in baseEncoding.GetProperties())
                {
                    if (prop.PropertyType == typeof(Encoding))
                    {
                        encounteredEncodings.Add((Encoding)prop.GetValue(baseEncoding));
                    }
                }
                */
                AvailableEncodings = new StandardValuesCollection(encounteredEncodings.ToList());
            }
            return AvailableEncodings;
        }

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string) || sourceType == typeof(int);
        }
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            Encoding enc = null;
            try
            {
                if (value is string s)
                {
                    if (int.TryParse(s, out int i))
                        enc = Encoding.GetEncoding(i);
                    else
                        enc = Encoding.GetEncoding(s);
                }
                else if (value is int i)
                {
                    enc = Encoding.GetEncoding(i);
                }
            }
            catch (ArgumentException) { }
            catch (NotSupportedException) { }

            if (enc != null && !encounteredEncodings.Contains(enc))
            {
                encounteredEncodings.Add(enc);
                AvailableEncodings = new StandardValuesCollection(encounteredEncodings.ToList());
            }
            return enc;
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return destinationType == typeof(string);
        }
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            return value is Encoding enc ? enc.WebName : null;
        }
    }
    public class EnumTypeTypeConverter : TypeConverter
    {
        public static readonly Type[] integerTypes = new[]
        {
            typeof(sbyte),
            typeof(byte),
            typeof(short),
            typeof(ushort),
            typeof(int),
            typeof(uint),
            typeof(long),
            typeof(ulong),
        };
        static readonly StandardValuesCollection standardTypes = new StandardValuesCollection(integerTypes);
        public override bool GetStandardValuesSupported(ITypeDescriptorContext context) => true;
        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context) => standardTypes;

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string);
        }
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string s)
            {
                var t = Type.GetType(s, false, true);
                if (integerTypes.Contains(t))
                    return t;
            }
            return null;
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return destinationType == typeof(string);
        }
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            return (value as Type)?.FullName;
        }
    }

    #endregion

    public class StageTableEntrySettings : IXmlSerializable
    {
        const string VariableTypes = "Variable Types";
        const string BufferSizes = "Buffer Sizes";

        [Category(VariableTypes), TypeConverter(typeof(EnumTypeTypeConverter))]
        public Type BackgroundTypeType { get; set; }
        [Category(VariableTypes), TypeConverter(typeof(EnumTypeTypeConverter))]
        public Type BossNumberType { get; set; }

        [Category(VariableTypes), TypeConverter(typeof(EncodingTypeConverter))]
        public Encoding FilenameEncoding { get; set; }
        [Category(VariableTypes), TypeConverter(typeof(EncodingTypeConverter))]
        public Encoding MapNameEncoding { get; set; }
        [Category(VariableTypes), TypeConverter(typeof(EncodingTypeConverter))]
        public Encoding JapaneseNameEncoding { get; set; }

        [Category(BufferSizes)]
        public int TilesetNameBuffer { get; set; }
        [Category(BufferSizes)]
        public int FilenameBuffer { get; set; }        
        [Category(BufferSizes)]
        public int BackgroundNameBuffer { get; set; }
        [Category(BufferSizes)]
        public int Spritesheet1Buffer { get; set; }
        [Category(BufferSizes)]
        public int Spritesheet2Buffer { get; set; }        
        [Category(BufferSizes)]
        public int JapaneseNameBuffer { get; set; }        
        [Category(BufferSizes)]
        public int MapNameBuffer { get; set; }
        [Category(BufferSizes)]
        public int Padding { get; set; }
        [Category(BufferSizes)]
        public int Size => TilesetNameBuffer
                         + FilenameBuffer
                         + Marshal.SizeOf(BackgroundTypeType)
                         + BackgroundNameBuffer
                         + Spritesheet1Buffer
                         + Spritesheet2Buffer
                         + JapaneseNameBuffer
                         + Marshal.SizeOf(BossNumberType)
                         + MapNameBuffer
                         + Padding;

        public XmlSchema GetSchema() => null;
        public void WriteXml(XmlWriter writer)
        {
            writer.WriteElementString(nameof(BackgroundTypeType), BackgroundTypeType.FullName);
            writer.WriteElementString(nameof(BossNumberType), BossNumberType.FullName);

            writer.WriteElementString(nameof(FilenameEncoding), FilenameEncoding?.WebName ?? "");
            writer.WriteElementString(nameof(MapNameEncoding), MapNameEncoding?.WebName ?? "");
            writer.WriteElementString(nameof(JapaneseNameEncoding), JapaneseNameEncoding?.WebName ?? "");

            writer.WriteElementString(nameof(TilesetNameBuffer), TilesetNameBuffer.ToString());
            writer.WriteElementString(nameof(FilenameBuffer), FilenameBuffer.ToString());
            writer.WriteElementString(nameof(BackgroundNameBuffer), BackgroundNameBuffer.ToString());
            writer.WriteElementString(nameof(Spritesheet1Buffer), Spritesheet1Buffer.ToString());
            writer.WriteElementString(nameof(Spritesheet2Buffer), Spritesheet2Buffer.ToString());
            writer.WriteElementString(nameof(JapaneseNameBuffer), JapaneseNameBuffer.ToString());
            writer.WriteElementString(nameof(MapNameBuffer), MapNameBuffer.ToString());
            writer.WriteElementString(nameof(Padding), Padding.ToString());
        }

        public void ReadXml(XmlReader reader)
        {
            reader.ReadStartElement();
            {
                Type ReadType(string name)
                {
                    var type = reader.ReadElementContentAsTypeName(name);
                    if (!EnumTypeTypeConverter.integerTypes.Contains(type))
                        throw new ArgumentException($"{type.Name} is not a valid type!");
                    return type;
                }
                BackgroundTypeType = ReadType(nameof(BackgroundTypeType));
                BossNumberType = ReadType(nameof(BossNumberType));

                FilenameEncoding = reader.ReadElementContentAsEncoding(nameof(FilenameEncoding));
                MapNameEncoding = reader.ReadElementContentAsEncoding(nameof(MapNameEncoding));
                JapaneseNameEncoding = reader.ReadElementContentAsEncoding(nameof(JapaneseNameEncoding));

                TilesetNameBuffer = reader.ReadElementContentAsInt(nameof(TilesetNameBuffer), "");
                FilenameBuffer = reader.ReadElementContentAsInt(nameof(FilenameBuffer), "");
                BackgroundNameBuffer = reader.ReadElementContentAsInt(nameof(BackgroundNameBuffer), "");
                Spritesheet1Buffer = reader.ReadElementContentAsInt(nameof(Spritesheet1Buffer), "");
                Spritesheet2Buffer = reader.ReadElementContentAsInt(nameof(Spritesheet2Buffer), "");
                JapaneseNameBuffer = reader.ReadElementContentAsInt(nameof(JapaneseNameBuffer), "");
                MapNameBuffer = reader.ReadElementContentAsInt(nameof(MapNameBuffer), "");
                Padding = reader.ReadElementContentAsInt(nameof(Padding), "");
            }
            reader.ReadEndElement();
        }

        

        public void ResetToDefault(StageTablePresets type)
        {
            switch (type)
            {
                case StageTablePresets.doukutsuexe:
                case StageTablePresets.swdata:
                case StageTablePresets.csmap:
                case StageTablePresets.stagetbl:
                    FilenameEncoding = Encoding.ASCII;
                    TilesetNameBuffer = 0x20;
                    FilenameBuffer = 0x20;
                    BackgroundTypeType = typeof(int);
                    BackgroundNameBuffer = 0x20;
                    Spritesheet1Buffer = 0x20;
                    Spritesheet2Buffer = 0x20;
                    BossNumberType = typeof(sbyte);
#if NETCOREAPP
                    //.NET Core will throw on the subsequent call to get Shift JIS if this isn't run
                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
                    JapaneseNameEncoding = Encoding.GetEncoding(932); //Shift JIS
                    //stage.tbl is the only one that has japanese names
                    JapaneseNameBuffer = type == StageTablePresets.stagetbl ? 0x20 : 0;
                    MapNameEncoding = Encoding.ASCII;
                    MapNameBuffer = 0x20;
                    Padding = type != StageTablePresets.stagetbl ? 3 : 0;
                    break;
                //Moustache Riders Map (table) .bin
                //this is emulating the stage table format used for GIRakaCHEEZER's unreleased game "Moustache Riders"
                //using it makes BL load png images instead of bmp
                //this editor doesn't care though, so use whatever you want :3
                case StageTablePresets.mrmapbin:
                    FilenameEncoding = Encoding.ASCII;
                    TilesetNameBuffer = 0x10;
                    FilenameBuffer = 0x10;
                    BackgroundTypeType = typeof(byte);
                    BackgroundNameBuffer = 0x10;
                    Spritesheet1Buffer = 0x10;
                    Spritesheet2Buffer = 0x10;
                    BossNumberType = typeof(sbyte);
                    MapNameEncoding = Encoding.ASCII;
                    MapNameBuffer = 0x22;
                    Padding = 0;
                    break;
                case StageTablePresets.custom:
                    throw new ArgumentException("No preset for custom!", nameof(type));
            }
        }
        public StageTableEntrySettings()
        { }
        public StageTableEntrySettings(StageTablePresets type)
        {
            ResetToDefault(type);
        }
        public override bool Equals(object obj)
        {
            if(obj is StageTableEntrySettings s)
            {
                return FilenameEncoding == s.FilenameEncoding &&
                    TilesetNameBuffer == s.TilesetNameBuffer &&
                    FilenameBuffer == s.FilenameBuffer &&
                    BackgroundTypeType == s.BackgroundTypeType &&
                    BackgroundNameBuffer == s.BackgroundNameBuffer &&
                    Spritesheet1Buffer == s.Spritesheet1Buffer &&
                    Spritesheet2Buffer == s.Spritesheet2Buffer &&
                    JapaneseNameEncoding == s.JapaneseNameEncoding &&
                    JapaneseNameBuffer == s.JapaneseNameBuffer &&
                    BossNumberType == s.BossNumberType &&
                    MapNameBuffer == s.MapNameBuffer &&
                    Padding == s.Padding;
            }
            else
                return base.Equals(obj);
        }
        public override int GetHashCode()
        {
            return (2 * FilenameEncoding?.GetHashCode() ?? 0) +
                (3 * TilesetNameBuffer.GetHashCode()) +
                (5 * FilenameBuffer.GetHashCode()) +
                (7 * BackgroundTypeType.GetHashCode()) +
                (11 * BackgroundNameBuffer.GetHashCode()) +
                (13 * Spritesheet1Buffer.GetHashCode()) +
                (17 * Spritesheet2Buffer.GetHashCode()) +
                (19 * JapaneseNameEncoding?.GetHashCode() ?? 0) +
                (23 * JapaneseNameBuffer.GetHashCode()) +
                (29 * BossNumberType.GetHashCode()) +
                (31 * MapNameBuffer.GetHashCode()) +
                (37 * Padding.GetHashCode());
        }
    }

    public class StageTableReferences
    {
        public List<long> TilesetReferences { get; set; } = new List<long>();
        public List<long> FilenameReferences { get; set; } = new List<long>();
        public List<long> BackgroundTypeReferences { get; set; } = new List<long>();
        public List<long> BackgroundNameReferences { get; set; } = new List<long>();
        public List<long> Spritesheet1References { get; set; } = new List<long>();
        public List<long> Spritesheet2References { get; set; } = new List<long>();
        public List<long> BossNumberReferences { get; set; } = new List<long>();
        public List<long> JapaneseNameReferences { get; set; } = new List<long>();
        public List<long> MapNameReferences { get; set; } = new List<long>();

        public override bool Equals(object obj)
        {
            if (obj is StageTableReferences r)
            {
                return TilesetReferences.SequenceEqual(r.TilesetReferences) &&
                    FilenameReferences.SequenceEqual(r.FilenameReferences) &&
                    BackgroundTypeReferences.SequenceEqual(r.BackgroundTypeReferences) &&
                    BackgroundNameReferences.SequenceEqual(r.BackgroundNameReferences) &&
                    Spritesheet1References.SequenceEqual(r.Spritesheet1References) &&
                    Spritesheet2References.SequenceEqual(r.Spritesheet2References) &&
                    BossNumberReferences.SequenceEqual(r.BossNumberReferences) &&
                    JapaneseNameReferences.SequenceEqual(r.JapaneseNameReferences) &&
                    MapNameReferences.SequenceEqual(r.MapNameReferences);
            }
            else return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return 2 * TilesetReferences.GetHashCode() +
                3 * FilenameReferences.GetHashCode() +
                5 * BackgroundTypeReferences.GetHashCode() +
                7 * BackgroundNameReferences.GetHashCode() +
                11 * Spritesheet1References.GetHashCode() +
                13 * Spritesheet2References.GetHashCode() +
                17 * BossNumberReferences.GetHashCode() +
                19 * JapaneseNameReferences.GetHashCode() +
                23 * MapNameReferences.GetHashCode();
        }
    }


    [DebuggerDisplay("{Filename} - {MapName}")]
    public class StageTableEntry : PropertyChangedHelper, ICloneable
    {
        public const int DoukutsuExeLength = 200;
        public const int CSPlusLength = 232;
        public const int CS3DLength = 268; //tested on JP

        string tilesetName, filename, backgroundName, spritesheet1, spritesheet2, japaneseName, mapName;
        long backgroundType, bossNumber;
        public string TilesetName { get => tilesetName; set => SetVal(ref tilesetName, value); }
        //SW doesn't allow "\empty" as a name, it won't show them n stuff
        public string Filename { get => filename; set => SetVal(ref filename, value); }
        public long BackgroundType { get => backgroundType; set => SetVal(ref backgroundType, value); }
        public string BackgroundName { get => backgroundName; set => SetVal(ref backgroundName, value); }
        public string Spritesheet1 { get => spritesheet1; set => SetVal(ref spritesheet1, value); }
        public string Spritesheet2 { get => spritesheet2; set => SetVal(ref spritesheet2, value); }
        public long BossNumber { get => bossNumber; set => SetVal(ref bossNumber, value); }
        public string JapaneseName { get => japaneseName; set => SetVal(ref japaneseName, value); }
        public string MapName { get => mapName; set => SetVal(ref mapName, value); }

        public byte[] Serialize(StageTableEntrySettings settings)
        {
            var data = new byte[settings.Size];
            var index = 0;

            void WriteNum(object num, Type t)
            {
                var bytes = Extensions.ConvertAndGetBytes(num, t);
                Array.Copy(bytes, 0, data, index, bytes.Length);
                index += bytes.Length;
            }
            void WriteString(string text, Encoding encoding, int max)
            {
                if (text != null)
                    Extensions.BufferCopy(encoding?.GetBytes(text) ?? Array.Empty<byte>(), data, index, max - 1);
                index += max;
            }
            WriteString(TilesetName, settings.FilenameEncoding, settings.TilesetNameBuffer);
            WriteString(Filename, settings.FilenameEncoding, settings.FilenameBuffer);
            WriteNum(BackgroundType, settings.BackgroundTypeType);
            WriteString(BackgroundName, settings.FilenameEncoding, settings.BackgroundNameBuffer);
            WriteString(Spritesheet1, settings.FilenameEncoding, settings.Spritesheet1Buffer);
            WriteString(Spritesheet2, settings.FilenameEncoding, settings.Spritesheet2Buffer);
            WriteNum(BossNumber, settings.BossNumberType);
            WriteString(JapaneseName, settings.JapaneseNameEncoding, settings.JapaneseNameBuffer);
            WriteString(MapName, settings.MapNameEncoding, settings.MapNameBuffer);

            return data;
        }

        public object Clone()
        {
            return new StageTableEntry()
            {
                TilesetName = tilesetName,
                Filename = filename,
                BackgroundType = backgroundType,
                BackgroundName = backgroundName,
                Spritesheet1 = spritesheet1,
                Spritesheet2 = spritesheet2,
                BossNumber = bossNumber,
                JapaneseName = japaneseName,
                MapName = mapName,
            };
        }
    }


    public static class StageTable
    {
        public const string EXEFilter = "Executables (*.exe)|*.exe";
        public const string CSFilter = "Cave Story (Doukutsu.exe)|Doukutsu.exe";
        public const string MRMAPFilter = "CSE2 (mrmap.bin)|mrmap.bin";
        public const string STAGETBLFilter = "CS+ (stage.tbl)|stage.tbl";

        public const int CSStageCount = 95;
        public const int CSStageTableEntrySize = 0xC8;
        public const int CSStageTableSize = CSStageCount * CSStageTableEntrySize;
        public const int CSStageTableAddress = 0x937B0;

        public const string SWDATASectionName = ".swdata";
        public const int SWDATAExpectedAddress = 0x169000;
        public const string SWDATAHeader = "Sue's Workshop01";
        //Belongs at index after .rsrc
        public static readonly IMAGE_SECTION_HEADER SWDATASectionHeader = new IMAGE_SECTION_HEADER()
        {
            Name = DataLocation.GetSectionHeaderSafeName(SWDATASectionName).ToCharArray(),
            Characteristics = IMAGE_SECTION_FLAGS.IMAGE_SCN_CNT_CODE |
                              IMAGE_SECTION_FLAGS.IMAGE_SCN_CNT_INITIALIZED_DATA |
                              IMAGE_SECTION_FLAGS.IMAGE_SCN_CNT_UNINITIALIZED_DATA |
                              IMAGE_SECTION_FLAGS.IMAGE_SCN_MEM_EXECUTE |
                              IMAGE_SECTION_FLAGS.IMAGE_SCN_MEM_READ |
                              IMAGE_SECTION_FLAGS.IMAGE_SCN_MEM_WRITE,
            VirtualAddress = 0x18E000,
        };

        public const string CSMAPSectionName = ".csmap";
        //Belongs at index before .rsrc
        public static readonly IMAGE_SECTION_HEADER CSMAPSectionHeader = new IMAGE_SECTION_HEADER()
        {
            Name = DataLocation.GetSectionHeaderSafeName(CSMAPSectionName).ToCharArray(),
            Characteristics = IMAGE_SECTION_FLAGS.IMAGE_SCN_CNT_INITIALIZED_DATA |
                              IMAGE_SECTION_FLAGS.IMAGE_SCN_MEM_READ |
                              IMAGE_SECTION_FLAGS.IMAGE_SCN_MEM_WRITE,
            VirtualAddress = 0xBF000
        };

        public const string STAGETBL = "stage.tbl";
        public const int STAGETBLEntrySize = 0xE5;
        public const int STAGETBLSize = CSStageCount * STAGETBLEntrySize;

        public const string MRMAPBIN = "mrmap.bin";


        /// <summary>
        /// Removes the specificed stage table type from the exe without updating any references
        /// </summary>
        /// <param name="path"></param>
        /// <param name="type"></param>
        public static void CleanEXE(string path, StageTablePresets type)
        {
            string sectName;
            switch (type)
            {
                case StageTablePresets.csmap:
                    sectName = CSMAPSectionName;
                    break;
                case StageTablePresets.swdata:
                    sectName = SWDATASectionName;
                    break;
                default:
                    return;
            }
            var pe = PEFile.FromFile(path);
            if (pe.ContainsSection(sectName))
            {
                pe.RemoveSection(sectName);
                pe.UpdateSectionLayout();
                pe.WriteFile(path);
            }
        }

        public static bool VerifySW(string path)
        {
            return VerifySW(PEFile.FromFile(path));
        }
        public static bool VerifySW(PEFile pe)
        {
            //SWData HAS to be at this address, otherwise SW will break the mod on opening it...
            return pe.TryGetSection(SWDATASectionName, out PESection sw) && sw.PhysicalAddress == SWDATAExpectedAddress;
        }

        public static void AddSection(string path, StageTablePresets type)
        {
            if (!(type == StageTablePresets.csmap || type == StageTablePresets.swdata))
                return;
            AddSection(PEFile.FromFile(path), type);
        }
        public static void AddSection(PEFile pe, StageTablePresets type)
        {
            switch (type)
            {
                case StageTablePresets.csmap:
                    if (!pe.ContainsSection(CSMAPSectionName))
                        pe.InsertSection(pe.sections.IndexOf(pe.GetSection(".rsrc")), new PESection(CSMAPSectionHeader));
                    break;
                case StageTablePresets.swdata:
                    if (!pe.ContainsSection(SWDATASectionName))
                        pe.AddSection(new PESection(SWDATASectionHeader));
                    break;
            }
        }

        //Determine how much space a stage table in the given format would take up
        public static int GetBufferSize(StageTableFormats format, int stageCount, StageTableEntrySettings settings)
        {
            switch (format)
            {
                case StageTableFormats.normal:
                    return stageCount * settings.Size;
                case StageTableFormats.swdata:
                    return SWDATAHeader.Length + ((stageCount + 1) * settings.Size);
                case StageTableFormats.mrmapbin:
                    return sizeof(int) + stageCount * settings.Size;
                default:
                    return -1;
            }
        }
        public static List<StageTableEntry> ReadSWData(this BinaryReader br, int stageCount, StageTableEntrySettings settings)
        {
            var header = br.ReadString(SWDATAHeader.Length, Encoding.ASCII);
            if (header != SWDATAHeader)
                throw new FileLoadException(); //TODO
            var stages = new List<StageTableEntry>(stageCount);
            while (true)
            {
                var buffer = br.ReadBytes(settings.Size);
                if (buffer.All(x => x == 0xFF))
                    break;
                using (var buff = new BinaryReader(new MemoryStream(buffer)))
                {
                    stages.Add(ReadStage(buff, settings));
                }
            }
            return stages;
        }
        public static StageTableEntry ReadStage(this BinaryReader br, StageTableEntrySettings settings)
        {
            var s = new StageTableEntry()
            {
                TilesetName = br.ReadString(settings.TilesetNameBuffer, settings.FilenameEncoding),
                Filename = br.ReadString(settings.FilenameBuffer, settings.FilenameEncoding),
                BackgroundType = (long)br.Read(settings.BackgroundTypeType),
                BackgroundName = br.ReadString(settings.BackgroundNameBuffer, settings.FilenameEncoding),
                Spritesheet1 = br.ReadString(settings.Spritesheet1Buffer, settings.FilenameEncoding),
                Spritesheet2 = br.ReadString(settings.Spritesheet2Buffer, settings.FilenameEncoding),
                JapaneseName = br.ReadString(settings.JapaneseNameBuffer, settings.JapaneseNameEncoding),
                BossNumber = (long)br.Read(settings.BossNumberType),
                MapName = br.ReadString(settings.MapNameBuffer, settings.MapNameEncoding),
            };
            br.BaseStream.Seek(settings.Padding, SeekOrigin.Current);
            return s;
        }
        public static List<StageTableEntry> ReadStages(this BinaryReader br, int stageCount, StageTableEntrySettings settings)
        {
            var stages = new List<StageTableEntry>(stageCount);
            for (int i = 0; i < stageCount; i++)
            {
                stages.Add(ReadStage(br, settings));
            }
            return stages;
        }

        public static void WriteStages(this BinaryWriter bw, IEnumerable<StageTableEntry> table, StageTableEntrySettings settings)
        {
            foreach(var stage in table)
            {
                bw.Write(stage.Serialize(settings));
            }
        }
    }

    public class StageTableLocation : DataLocation
    {
        StageTableFormats stageTableFormat;
        int stageCount;
        public StageTableFormats StageTableFormat { get => stageTableFormat; set => SetVal(ref stageTableFormat, value); }
        public int StageCount { get => stageCount; set => SetVal(ref stageCount, value); }

        public StageTableEntrySettings Settings { get; set; } = new StageTableEntrySettings();

        public StageTableReferences References { get; set; } = new StageTableReferences();

        public StageTableLocation() { }
        public StageTableLocation(string path)
        {
            Filename = path;
        }
        public StageTableLocation(StageTablePresets type)
        {
            ResetToDefault(type);
            Settings.ResetToDefault(type);
        }
        public StageTableLocation(string path, StageTablePresets type)
        {
            Filename = path;
            ResetToDefault(type);
            Settings.ResetToDefault(type);
        }
        public StageTableLocation(XmlElement xml)
        {
            //Filename = xml[nameof(Filename)].InnerText;
            DataLocationType = (DataLocationTypes)Enum.Parse(typeof(DataLocationTypes), xml[nameof(DataLocationType)].InnerText);
            Offset = int.Parse(xml[nameof(Offset)].InnerText);
            SectionName = xml[nameof(SectionName)].InnerText;
            FixedSize = bool.Parse(xml[nameof(FixedSize)].InnerText);
            MaximumSize = int.Parse(xml[nameof(MaximumSize)].InnerText);

            StageTableFormat = (StageTableFormats)Enum.Parse(typeof(StageTableFormats), xml[nameof(StageTableFormat)].InnerText);
            StageCount = int.Parse(xml[nameof(StageCount)].InnerText);
        }
        public override XElement ToXML(string elementName, string relativeBase)
        {
            var x = base.ToXML(elementName,relativeBase);
            x.Add(
                new XElement(nameof(StageTableFormat), StageTableFormat),
                new XElement(nameof(StageCount), StageCount)
                );
            return x;
        }
        public void ResetToDefault(StageTablePresets type)
        {
            switch (type)
            {
                case StageTablePresets.doukutsuexe:
                    DataLocationType = DataLocationTypes.Internal;
                    SectionName = "";
                    Offset = StageTable.CSStageTableAddress;
                    MaximumSize = StageTable.CSStageTableSize;
                    FixedSize = true;
                    StageTableFormat = StageTableFormats.normal;
                    StageCount = StageTable.CSStageCount;
                    break;
                case StageTablePresets.swdata:
                    DataLocationType = DataLocationTypes.Internal;
                    SectionName = StageTable.SWDATASectionName;
                    Offset = 0;
                    MaximumSize = 0;
                    FixedSize = false;
                    StageTableFormat = StageTableFormats.swdata;
                    StageCount = 0;
                    break;
                case StageTablePresets.csmap:
                    DataLocationType = DataLocationTypes.Internal;
                    SectionName = StageTable.CSMAPSectionName;
                    Offset = 0;
                    MaximumSize = 0;
                    FixedSize = false;
                    StageTableFormat = StageTableFormats.normal;
                    StageCount = 0;
                    break;
                case StageTablePresets.stagetbl:
                case StageTablePresets.mrmapbin:
                    DataLocationType = DataLocationTypes.External;
                    SectionName = "";
                    Offset = 0;
                    MaximumSize = type == StageTablePresets.stagetbl ? StageTable.STAGETBLSize : 0;
                    FixedSize = false; //TODO check on the real limits of stage.tbl
                    StageTableFormat = type == StageTablePresets.stagetbl ? StageTableFormats.normal : StageTableFormats.mrmapbin;
                    StageCount = 0;
                    break;
                case StageTablePresets.custom:
                    throw new ArgumentException("There is no preset for custom", nameof(type));
            }
        }

        
        public List<StageTableEntry> Read()
        {
            if (!TryCalculateEntryCount(Settings.Size, out int stageCount))
                stageCount = StageCount;
            using (var br = new BinaryReader(GetStream(FileMode.Open, FileAccess.Read)))
            {
                switch (StageTableFormat)
                {
                    case StageTableFormats.mrmapbin:
                        stageCount = br.ReadInt32();
                        goto case StageTableFormats.normal;
                    case StageTableFormats.normal:
                        return br.ReadStages(stageCount, Settings);
                    case StageTableFormats.swdata:
                        return br.ReadSWData(stageCount, Settings);
                    default:
                        throw new ArgumentException("Invalid stage table format!", nameof(StageTableFormat));
                }
            }
        }
        public void Write(IList<StageTableEntry> stages)
        {
            var size = StageTable.GetBufferSize(StageTableFormat, stages.Count, Settings);
            //stop if we're about to write too much data
            if (MaximumSize > 0 && size > MaximumSize)
                throw new Exception($"The current stage table export settings result in a stage table that is {size} bytes large, " +
                    $"which is {size - MaximumSize} over the maximum allowed size {MaximumSize}!");
            var buffer = new byte[size];

            //if we're saving to an internal file, and need to go off of a section...
            if (DataLocationType == DataLocationTypes.Internal && !string.IsNullOrEmpty(SectionName))
            {
                PEFile pe = PEFile.FromFile(Filename);
                //...but the requested section doesn't exist...
                if (!pe.ContainsSection(SectionName))
                {
                    //...we might be able to fix it!
                    switch (SectionName)
                    {
                        case StageTable.CSMAPSectionName:
                            pe.InsertSection(pe.sections.IndexOf(pe.GetSection(".rsrc")), new PESection(StageTable.CSMAPSectionHeader)
                            {
                                Data = new byte[size]
                            });
                            break;
                        case StageTable.SWDATASectionName:
                            pe.AddSection(new PESection(StageTable.SWDATASectionHeader)
                            {
                                Data = new byte[size]
                            });
                            break;
                        //...or not
                        default:
                            throw new KeyNotFoundException();
                    }
                    pe.UpdateSectionLayout();
                    pe.WriteFile(Filename);
                }
            }
            using (var bw = new BinaryWriter(new MemoryStream(buffer)))
            {
                switch (StageTableFormat)
                {
                    case StageTableFormats.mrmapbin:
                        bw.Write((int)stages.Count);
                        bw.WriteStages(stages, Settings);
                        break;
                    case StageTableFormats.normal:
                        bw.WriteStages(stages, Settings);
                        break;
                    case StageTableFormats.swdata:
                        bw.Write(Encoding.ASCII.GetBytes(StageTable.SWDATAHeader));
                        bw.WriteStages(stages, Settings);
                        bw.Write(Enumerable.Repeat<byte>(0xFF, Settings.Size).ToArray());
                        break;
                }
            }
            Write(buffer);

            if (DataLocationType == DataLocationTypes.Internal)
                UpdateStageTableReferences();
        }

        public void UpdateStageTableReferences()
        {
            if (DataLocationType != DataLocationTypes.Internal)
                throw new ArgumentException("Can only patch internal stage tables!", nameof(DataLocationType));
            var pe = PEFile.FromFile(Filename);
            uint startOfStageTable = pe.optionalHeader32.ImageBase;
            bool set = false;
            if (!string.IsNullOrEmpty(SectionName))
            {
                if (!pe.TryGetSection(SectionName, out var sect))
                    throw new KeyNotFoundException();
                startOfStageTable += sect.VirtualAddress;
                set = true;
            }

            //Opening the file the internal stage table is located in AT the stage table
            using (var bw = new BinaryWriter(GetStream(FileMode.Open, FileAccess.ReadWrite)))
            {
                //if we aren't going off of a section name, then we add the current position
                //this might not work in *every* case, but it can be circumvented by using the actual section name anyways, so...
                if (!set)
                    startOfStageTable += (uint)bw.BaseStream.Position;
                switch (StageTableFormat)
                {
                    case StageTableFormats.swdata:
                        startOfStageTable += (uint)StageTable.SWDATAHeader.Length;
                        break;
                    case StageTableFormats.mrmapbin:
                        startOfStageTable += (uint)sizeof(int);
                        break;
                }
                void UpdateAddressList(IEnumerable<long> addresses, uint value)
                {
                    foreach (var a in addresses)
                    {
                        bw.BaseStream.Seek(a, SeekOrigin.Begin);
                        bw.Write(value);
                    }
                }
                UpdateAddressList(References.TilesetReferences, startOfStageTable);
                UpdateAddressList(References.FilenameReferences, startOfStageTable += (uint)Settings.TilesetNameBuffer);
                UpdateAddressList(References.BackgroundTypeReferences, startOfStageTable += (uint)Settings.FilenameBuffer);
                UpdateAddressList(References.BackgroundNameReferences, startOfStageTable += (uint)Marshal.SizeOf(Settings.BackgroundTypeType));
                UpdateAddressList(References.Spritesheet1References, startOfStageTable += (uint)Settings.BackgroundNameBuffer);
                UpdateAddressList(References.Spritesheet2References, startOfStageTable += (uint)Settings.Spritesheet1Buffer);
                UpdateAddressList(References.BossNumberReferences, startOfStageTable += (uint)Settings.Spritesheet2Buffer);
                UpdateAddressList(References.JapaneseNameReferences, startOfStageTable += (uint)Marshal.SizeOf(Settings.BossNumberType));
                UpdateAddressList(References.MapNameReferences, startOfStageTable += (uint)Settings.JapaneseNameBuffer);
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is StageTableLocation l)
            {
                return base.Equals(l) &&
                    StageTableFormat == l.StageTableFormat &&
                    StageCount == l.StageCount &&
                    Settings.Equals(l.Settings) &&
                    References.Equals(l.References);
            }
            else return base.Equals(obj);
        }
        public override int GetHashCode()
        {
            return (2 * DataLocationType.GetHashCode()) +
                (3 * SectionName.GetHashCode()) +
                (5 * Offset.GetHashCode()) +
                (7 * MaximumSize.GetHashCode()) +
                (11 * FixedSize.GetHashCode()) +
                (13 * StageTableFormat.GetHashCode()) +
                (17 * StageCount.GetHashCode());
        }
    }
}

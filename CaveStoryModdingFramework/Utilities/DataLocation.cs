using System;
using System.Collections.Generic;
using System.IO;
using PETools;
using System.Text;
using System.Xml.Linq;

namespace CaveStoryModdingFramework
{
    public enum DataLocationTypes
    {
        Internal,
        External
    }
    /// <summary>
    /// Describes how data is layed out in file
    /// </summary>
    public class DataLocation : PropertyChangedHelper
    {
        #region Properties

        string filename, sectionName;
        DataLocationTypes dataLocationType;
        bool fixedSize;
        int offset, maximumSize;

        public string Filename { get => filename; set => SetVal(ref filename, value); }
        /// <summary>
        /// Is the data inside an exe, or in its own file?
        /// </summary>
        public DataLocationTypes DataLocationType { get => dataLocationType; set => SetVal(ref dataLocationType, value); }

        /// <summary>
        /// How many bytes away from the start of the file/section this data is
        /// </summary>
        public int Offset { get => offset; set => SetVal(ref offset, value); }

        /// <summary>
        /// What PE section this data belongs in. empty string/null = no section/start of file
        /// </summary>
        public string SectionName { get => sectionName; set => SetVal(ref sectionName, value); }

        /// <summary>
        /// If set, always write either MaximumSize bytes, or enough bytes to overwrite the desired "SectionName" completely
        /// </summary>
        public bool FixedSize { get => fixedSize; set => SetVal(ref fixedSize, value); }
        /// <summary>
        /// The maximum space this data has. Any value <= 0 means "no maximum"
        /// </summary>
        public int MaximumSize { get => maximumSize; set => SetVal(ref maximumSize, value); }

        #endregion

        public static string GetSectionHeaderSafeName(string value)
        {
            return GetSectionHeaderSafeName(value, Encoding.ASCII);
        }
        public static string GetSectionHeaderSafeName(string value, Encoding encoding)
        {
            var outBuff = new byte[IMAGE_SECTION_HEADER.IMAGE_SIZEOF_SHORT_NAME];
            var inBuff = encoding.GetBytes(value);
            var len = Math.Min(outBuff.Length, inBuff.Length);
            for (int i = 0; i < len; i++)
                outBuff[i] = inBuff[i];
            return encoding.GetString(outBuff);
        }
        public PESection GetSection()
        {
            var pe = PEFile.FromFile(Filename);
            if (!pe.TryGetSection(SectionName, out var sect))
                throw new KeyNotFoundException();
            return sect;
        }
        public byte[] GetSectionData()
        {
            return GetSection().Data;
        }
        public Stream GetStream(FileMode mode, FileAccess access)
        {
            var offset = Math.Max(0, Offset);
            var fs = new FileStream(filename, mode, access);
            switch (DataLocationType)
            {
                case DataLocationTypes.Internal:
                    //for internal data, we might need to go to the PE section and add its offset
                    if (!string.IsNullOrEmpty(SectionName))
                    {
                        try
                        {
                            var p = PEFile.FromStream(fs);
                            offset += (int)p.GetSection(SectionName).PhysicalAddress;
                        }
                        catch
                        {
                            //don't want to leave the file open if something went wrong...
                            fs.Close();
                            throw new Exception("Something went wrong while getting the section...");
                        }
                    }
                    //but other than that it's the same code to get a stream
                    goto case DataLocationTypes.External;
                case DataLocationTypes.External:
                    fs.Seek(offset, SeekOrigin.Begin);
                    break;
            }
            return fs;
        }
        public byte[] Read(int length)
        {
            if (!File.Exists(Filename))
                throw new FileNotFoundException();
            using(var br = new BinaryReader(GetStream(FileMode.Open, FileAccess.Read)))
            {
                return br.ReadBytes(length);
            }
        }
        public void Write(byte[] data)
        {
            //don't write if the data exceeds the max
            if (MaximumSize > 0 && data.Length > MaximumSize)
                throw new ArgumentOutOfRangeException();

            var offset = Math.Max(0, Offset);
            var fileMode = FileMode.Create;
            var fixedSize = FixedSize;
            var fixedSizeMakeupLength = MaximumSize - data.Length;

            switch (DataLocationType)
            {
                case DataLocationTypes.Internal:
                    //can't write to internal data if the file doesn't exist
                    if (!File.Exists(Filename))
                        throw new FileNotFoundException();
                                        
                    //grab the requested section where applicable
                    if(!string.IsNullOrEmpty(SectionName))
                    {
                        var pe = PEFile.FromFile(Filename);
                        if (!pe.TryGetSection(SectionName, out var sect))
                            throw new KeyNotFoundException();
#if COMPARE_SECTION_SIZES
                        var nextSect = pe.sections[pe.sections.IndexOf(sect) + 1];
#endif
                        offset += (int)sect.PhysicalAddress;

                        //if we were told to write at the *very* start of the section...
                        if (Offset <= 0)
                        {
                            //...we must be overwriting ALL the data
                            fixedSize = true;
                            //calculate the true size of the section for this write
                            var newSectionSize = PEUtility.AlignUp((uint)data.Length, pe.FileAlignment);
                            //use that size to update the makeupLength
                            fixedSizeMakeupLength = (int)(newSectionSize - data.Length);

                            //if this would result in a resize, use the stuff below to make the write
#if COMPARE_SECTION_SIZES
                            if (sect.PhysicalAddress + newSectionSize != nextSect.PhysicalAddress)
#else
                            if(newSectionSize != sect.RawSize)
#endif
                            {
                                //TODO I have some uncertainty about this line but I don't know why
                                pe.WriteSectionData(SectionName, data);

                                pe.UpdateSectionLayout();
                                pe.WriteFile(filename);
                                return;
                            }
                            //otherwise we can just use the function below
                        }
                    }

                    fileMode = FileMode.Open;
                    goto case DataLocationTypes.External;
                case DataLocationTypes.External:
                    using (var bw = new BinaryWriter(new FileStream(filename, fileMode, FileAccess.Write)))
                    {
                        bw.Seek(offset, SeekOrigin.Begin);
                        bw.Write(data);
                        //write 00s to fill space when needed
                        if (FixedSize && fixedSizeMakeupLength > 0)
                            bw.Write(new byte[fixedSizeMakeupLength]);
                    }
                    break;
            }
        }

        /// <summary>
        /// If your data is in distinct chunks, try to calculate how many chunks this data has
        /// </summary>
        /// <param name="filename">File to open</param>
        /// <param name="entrySize">Size of each chunk</param>
        /// <param name="entryCount">Output for number of entries</param>
        /// <returns>Whether the opporation succeeded</returns>
        /// <exception cref="ArgumentException"></exception>
        protected bool TryCalculateEntryCount(int entrySize, out int entryCount)
        {
            //an offset of 0 (or less) means we're probably using the whole section/file...
            if (Offset <= 0)
            {
                switch (DataLocationType)
                {
                    case DataLocationTypes.Internal:
                        //if it's a section, use the length of it
                        if (!string.IsNullOrEmpty(SectionName))
                            entryCount = (int)(GetSection().RawSize / entrySize);
                        //if we get here that means we just
                        //have the data right at the beginning of the entire file...?!
                        else
                            throw new ArgumentException("Your data is at the *very* start of an exe...?!",
                                $"{nameof(Offset)}, {nameof(SectionName)}");
                        break;
                    //if it's a file, use the length of that
                    case DataLocationTypes.External:
                        entryCount = (int)(new FileInfo(filename).Length / entrySize);
                        break;
                    default:
                        throw new ArgumentException($"Invalid {nameof(DataLocationType)}: {DataLocationType}", nameof(DataLocationType));
                }
                return true;
            }
            else
            {
                entryCount = -1;
                return false;
            }
        }

        public override bool Equals(object obj)
        {
            if(obj is DataLocation dl)
            {
                return DataLocationType == dl.DataLocationType
                    && SectionName == dl.SectionName
                    && Offset == dl.Offset
                    && MaximumSize == dl.MaximumSize
                    && FixedSize == dl.FixedSize;
            }
            else
                return base.Equals(obj);
        }

        public virtual XElement ToXML(string elementName, string relativeBase)
        {
            return new XElement(elementName,
                    new XElement(nameof(Filename), AssetManager.MakeRelative(relativeBase, Filename)),
                    new XElement(nameof(DataLocationType), DataLocationType),
                    new XElement(nameof(Offset), Offset),
                    new XElement(nameof(SectionName), SectionName),
                    new XElement(nameof(FixedSize), FixedSize),
                    new XElement(nameof(MaximumSize), MaximumSize)
                );
        }
    }
}

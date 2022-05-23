using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using CaveStoryModdingFramework.Entities;
using CaveStoryModdingFramework.Maps;
using CaveStoryModdingFramework.Stages;
using PETools;

namespace CaveStoryModdingFramework.AutoDetection
{
    [DebuggerDisplay("{StageTables.Count + NpcTables.Count + BulletTables.Count + ArmsLevelTables.Count} Total Tables")]
    public class ExternalTables
    {
        public List<StageTableLocation> StageTables = new List<StageTableLocation>();
        public List<NPCTableLocation> NpcTables = new List<NPCTableLocation>();
        public List<BulletTableLocation> BulletTables = new List<BulletTableLocation>();
        public List<ArmsLevelTableLocation> ArmsLevelTables = new List<ArmsLevelTableLocation>();
    }

    [DebuggerDisplay("Prefix = {TilesetPrefix} Extension = {AttributeExtension}")]
    public class AttributeInfo
    {
        public string TilesetPrefix { get; }
        public string AttributeExtension { get; }

        public AttributeInfo(string tilesetPrefix, string attributeExtension)
        {
            TilesetPrefix = tilesetPrefix;
            AttributeExtension = attributeExtension;
        }
    }

    [DebuggerDisplay("Map = {FoundMaps} Entity = {FoundEntities} Script = {FoundScripts} Encrypted = {ScriptsEncrypted}")]
    public class StageFolderSearchResults
    {
        public string FoundMaps { get; }
        public string FoundEntities { get; }
        public string FoundScripts { get; }
        public bool ScriptsEncrypted { get; }

        public StageFolderSearchResults(string foundMaps, string foundEntities, string foundScripts, bool scriptsEncrypted)
        {
            FoundMaps = foundMaps;
            FoundEntities = foundEntities;
            FoundScripts = foundScripts;
            ScriptsEncrypted = scriptsEncrypted;
        }
    }

    /* Rundown for how to use this class to detect a copy of CS
     * WARNING: this process WILL require human intervention
     *          any lines in ALL CAPS are where the user has to
     *          interviene if someting couldn't be found
     * 
     * 0. user supplies data folder AND exe path
     *        if only one is given, the other can USUALLY be autodetected
     *        but ultimately you need both and the user MUST be able to
     *        provide each individually, since modding is messy and not
     *        every version of CS keeps the exe and data folder together
     * 1. find stage table (+ other tables)
     *        everything in autodetection assumes you have a stage table
     *        but you can usually find other tables while you're looking too
     *    a. look for table(s) in exe
     *            99.9% of the time you're just going to find a stage table
     *            it's also the only one that you reasonably *search* for
     *            bullet and arms_level tables just have to be hardcoded
     *            based off the address/type of the stage table found
     *    b. search data folder for external tables (1 depth BFS)
     *            The data folder the user gave might not ACTUALLY be a data folder
     *            it could be the "data" folder from CS+, which doesn't
     *            contain any data directly, it just has more folders.
     *            Thankfully, CS+'s "base" folder (the one we want)
     *            is only one directory in, so we only have to do
     *            Breadth-first-search to a depth of 1.
     * MUST HAVE AT LEAST 1 STAGE TABLE BY THIS POINT
     * 2. use stage table(s) to determine image extension via backgrounds
     *        backgrounds are really special, since their prefix ISN'T
     *        hardcoded, it's ACTUALLY in the stage table each time.
     *        This means we actually have a list of FULL valid filenames
     *        so if we look for "<background_name>.*" we can determine
     *        the correct image extension, AND find the data folder
     *        since backgrounds live there.
     *        The image extension is super important, so this is good
     * MUST HAVE IMAGE EXTENSION AND DATA FOLDER BY THIS POINT
     * 3. find the npc/stage folders
     *        These next two steps (a and b) COULD be processed in any order
     *        but NPC is was less complicated, so I do it first
     *        all of this is handled by one function that just
     *        iterates over each folder in the found data folder
     *    a. Look for the npc folder/npc prefix
     *            since we have the image extension from before
     *            and a list of valid spritesheets from the stage table
     *            we can search for "*<spritesheet_name>.<image_extension>"
     *            and isolate the prefix/find the folder
     *    b. Look for the stage table
     *            hold on to your butts...
     *        i. search for stage files
     *                for each stage, look for "*<filename>.*" to find both
     *                prefixes and extensions, then put all the results
     *                in a big table. we SHOULD have 3 file extensions that
     *                EVERY file in the table shares
     *                then, check some sample files from each of those
     *                extensions to see what they are.
     *                We can use a header check to identify PXM/PXE
     *                TSC we just need to look for <END, but we need to
     *                check both the default input AND decrypted version
     *        ii. search for tileset files
     *                we need to look for two disparate things:
     *                the tileset prefix (attached to each tileset's IMAGE)
     *                the tileset attribute extension (these have NO prefix)
     *                so, we look for "*<tileset_name>.*", and if we find
     *                something with the image extension, we record the prefix
     *                any other extension gets added to the list
     *                if those lists each only have one item, the process worked!
     *                note: CS+ switch messes this up with .pxw files but
     *                that's easy to check for by trying to read the files as pxw
     * THE ABOVE FUNCTIONS MUST SUCCEED
     * 
     * and after all that, ALL WE HAVE is the first data folder
     * we do NOT have any CS+-style challenges
     * for that, it's a similar (but more loose) process
     * 
     * 1. perform DFS on the user-given data folder to find potential data folders
     *      annoyingly, finding data folders is a bit harder now
     *      you need to check for
     *      - external tables
     *      - hardcoded data files
     *      - backgrounds
     *      if ANY of those are found, then we can try to load it
     * 2. Find stage/npc folders
     *      this time we have to do the checks BACKWARDS
     *      instead of checking what files from the stage table are in the folder
     *      we need to check what files are in the folder that are in the stage table
     *      do that for both types and you might find something???
     * 
     * that's it... a lot more loose, but it works (barely)     
     */

    public class AutoDetector
    {
        #region Utilities

        /// <summary>
        /// Finds all items in the given list that have the largest value
        /// </summary>
        /// <typeparam name="T">The key value of the KeyValuePair</typeparam>
        /// <param name="list">The list to search</param>
        /// <returns>All items with the largest value</returns>
        public static List<T> GetMaxes<T>(IEnumerable<KeyValuePair<T, float>> list) where T : class
        {
            float max = -1;
            var maxes = new List<T>();
            foreach (var item in list)
            {
                if (item.Value > max)
                {
                    maxes.Clear();
                    max = item.Value;
                }
                if(item.Value >= max)
                {
                    maxes.Add(item.Key);
                }
            }       
            return maxes;
        }
        /// <summary>
        /// Used to find prefixes/file extensions by filtering a folder down to a set of known filenames
        /// </summary>
        /// <param name="path">The directory to search</param>
        /// <param name="filenames">The filenames to look for</param>
        /// <param name="extension">The file extension to append</param>
        /// <param name="filterFunc">The function to run on each found filename (params are {found filename, filename that was searched for})</param>
        /// <param name="percents">The filtered data, paired with the percentage of found files that mapped to that data</param>
        /// <returns>How many files were found as a percentage</returns>
        public static float FilterDirectory(string path, IList<string> filenames, string extension, Func<string, string, string> filterFunc, out List<KeyValuePair<string, float>> percents)
        {
            var filteredResults = new Dictionary<string, int>();
            int foundTotal = 0;
            foreach (var filename in filenames)
            {
                bool foundThis = false;
                foreach (var file in Extensions.EnumerateFilesCaseInsensitive(path, "*" + Path.ChangeExtension(filename, extension)))
                {
                    foundThis = true;
                    var filteredItem = filterFunc(Path.GetFileName(file), filename);
                    if (!filteredResults.ContainsKey(filteredItem))
                        filteredResults.Add(filteredItem, 1);
                    else
                        filteredResults[filteredItem]++;
                }
                if (foundThis)
                    foundTotal++;
            }

            percents = new List<KeyValuePair<string, float>>(filteredResults.Count);
            foreach (var result in filteredResults)
            {
                percents.Add(new KeyValuePair<string, float>(result.Key, (float)result.Value / foundTotal));
            }

            return (float)foundTotal / filenames.Count;
        }

        public static string FindWithShortcut(string path, Func<string, bool> func, string shortcut)
        {
            foreach (var dir in Extensions.EnumerateDirectoriesCaseInsensitive(path, shortcut))
            {
                if (func(dir))
                {
                    return dir;
                }
            }
            foreach (var dir in Extensions.EnumerateDirectoriesCaseInsensitive(path))
            {
                if (func(dir))
                    return dir;
            }
            return null;
        }

        public static void BreadthFirstSearch(IEnumerable<string> init, Func<string, bool> func)
        {
            var q = new Queue<string>(init);
            while (q.Count > 0)
            {
                var current = q.Dequeue();
                if (!func(current))
                {
                    foreach (var dir in Directory.EnumerateDirectories(current))
                        q.Enqueue(dir);
                }
            }
        }

        //TODO the fact that this takes a stage table is nice, but it means recalculating the hashes a lot
        static HashSet<string> MakeHashset(IEnumerable<string> items)
        {
            var output = new HashSet<string>();
            foreach (var item in items)
                output.Add(item.ToLower());
            return output;
        }

        /// <summary>
        /// Merges a set of loaded stage tables into one table where the last table has highest priority
        /// </summary>
        /// <param name="tables">The tables to merge</param>
        /// <returns>The merged table</returns>
        public static List<StageTableEntry> MergeStageTables(List<List<StageTableEntry>> tables)
        {
            if (tables.Count <= 0)
                return new List<StageTableEntry>();
            var output = new StageTableEntry[tables.Max(x => x.Count)];

            for(int i = 0; i < output.Length; i++)
            {
                for(int j = tables.Count - 1; j >= 0; j--)
                {
                    if (i < tables[j].Count) //please don't make me add a null check here
                    {
                        output[i] = tables[j][i];
                        break;
                    }
                }
            }
            return output.ToList();
        }

        #endregion

        #region Internal stage table detection
        public static List<StageTableLocation> FindInternalStageTables(string path)
        {
            var list = DetectStageTables(path, StageTableSearchOptions.Tileset, 2, Encoding.ASCII, "Eggs", "EggX");
            foreach (var l in list)
            {
                l.Filename = path;
            }
            return list;
        }
        public static List<StageTableLocation> DetectStageTables(Stream stream)
        {
            return DetectStageTables(stream, StageTableSearchOptions.Tileset, 2, Encoding.ASCII, "Eggs", "EggX");
        }
        public static List<StageTableLocation> DetectStageTables(string path, StageTableSearchOptions fallbackOption, int startIndex, Encoding encoding, params string[] names)
        {
            using (var StreamToUse = Extensions.OpenInMemory(path))
                return DetectStageTables(StreamToUse, fallbackOption, startIndex, encoding, names);
        }
        public static List<StageTableLocation> DetectStageTables(Stream StreamToUse, StageTableSearchOptions fallbackOption, int startIndex, Encoding encoding, params string[] names)
        {
            PEFile pe = null;
            try
            {
                pe = PEFile.FromStream(StreamToUse);
            }
            catch (FileLoadException)
            {
                //invalid PE
            }

            var tables = new List<StageTableLocation>();

            //check for/prioritize modded stage tables
            if (pe != null)
                tables.AddRange(FindSectionTables(pe));

            //if that failed, just go with the manual method
            if (tables.Count <= 0)
            {
                StreamToUse.Position = 0;
                tables.Add(FindStageTable(StreamToUse, fallbackOption, startIndex, encoding, names));
            }

            //only finding references in exes (for now?)
            if (pe != null)
            {
                foreach (var table in tables)
                    table.References = FindReferences(StreamToUse, pe, table, table.Settings);
            }

            return tables;
        }

        public static List<StageTableLocation> FindSectionTables(PEFile pe)
        {
            var l = new List<StageTableLocation>();
            if (pe.ContainsSection(StageTable.SWDATASectionName))
                l.Add(new StageTableLocation(StageTablePresets.swdata));
            if (pe.ContainsSection(StageTable.CSMAPSectionName))
                l.Add(new StageTableLocation(StageTablePresets.csmap));
            return l;
        }
        static List<Tuple<long, int>> FindSequentialData(Stream stream, params byte[][] values)
        {
            var results = new List<Tuple<long, int>>(values.Length);
            foreach (var str in values)
            {
                var pos = stream.FindBytes(str);
                if (pos < 0)
                    throw new KeyNotFoundException($"String not found: {str}");

                var len = str.Length + stream.CountZeros();
                results.Add(new Tuple<long, int>(pos, len));
            }

            if (results.Select(x => x.Item2).Distinct().Count() > 1)
                throw new ArithmeticException("Inconsistent buffer sizes!");

            return results;
        }

        static StageTableEntrySettings BufferSizeToSettings(int entryLen)
        {
            StageTableEntrySettings set;
            switch (entryLen)
            {
                case StageTableEntry.CS3DLength:
#if NETCOREAPP
                    //.NET Core will throw on the subsequent call to get Shift JIS if this isn't run
                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
                    set = new StageTableEntrySettings()
                    {
                        //TODO consider whether this should be its own stage table preset?
                        BackgroundTypeType = typeof(int),
                        BossNumberType = typeof(sbyte),
                        FilenameEncoding = Encoding.ASCII,
                        MapNameEncoding = Encoding.ASCII,
                        JapaneseNameEncoding = Encoding.GetEncoding(932),
                        TilesetNameBuffer = 32,
                        FilenameBuffer = 32,
                        BackgroundNameBuffer = 32,
                        Spritesheet1Buffer = 32,
                        Spritesheet2Buffer = 32,
                        JapaneseNameBuffer = 39,
                        MapNameBuffer = 64,
                        Padding = 0
                    };
                    break;
                case StageTableEntry.CSPlusLength:
                    set = new StageTableEntrySettings(StageTablePresets.stagetbl)
                    {
                        Padding = 3 //TODO why do I need to specify this...
                    };
                    break;
                case StageTableEntry.DoukutsuExeLength:
                    set = new StageTableEntrySettings(StageTablePresets.doukutsuexe);
                    break;
                case StageTableEntry.DoukutsuExeLength - 3: //CS mac has no padding
                    set = new StageTableEntrySettings(StageTablePresets.doukutsuexe)
                    {
                        Padding = 0
                    };
                    break;
                default:
                    set = new StageTableEntrySettings(StageTablePresets.doukutsuexe);
                    break;
            }
            return set;
        }

        static int DetermineEntryCount(Stream stream, StageTableEntrySettings set)
        {
            var found = 0;
            using (var br = new BinaryReader(stream, Encoding.Default, true))
            {
                while (br.BaseStream.Position < br.BaseStream.Length)
                {
                    StageTableEntry ent;
                    try
                    {
                        ent = br.ReadStage(set);
                        //pretty sure no copy of CS has this many bosses
                        if (ent.BossNumber < 0 || 10 < ent.BossNumber ||
                            //even CS+ Switch doesn't have this many background types
                            ent.BackgroundType < 0 || 10 < ent.BackgroundType
                            //TODO put more checks involving strings
                            )
                            break;
                    }
                    catch (Exception e)
                    {
                        break;
                    }
                    found++;
                }
            }
            return found;
        }

        public enum StageTableSearchOptions
        {
            Tileset,
            Name
        }
        //for filenames these are the best defaults:
        //2, Encoding.ASCII, "Eggs", "EggX"
        //for titles these are the best defaults:
        //9, Encoding.ASCII, "Labyrinth I", "Sand Zone"
        public static StageTableLocation FindStageTable(Stream stream, StageTableSearchOptions option, int startingIndex, Encoding encoding, params string[] names)
        {
            return FindStageTable(stream, option, startingIndex, names.Select(x => encoding.GetBytes(x)).ToArray());
        }
        public static StageTableLocation FindStageTable(Stream stream, StageTableSearchOptions option, int startingIndex, params byte[][] names)
        {
            if (names.Length < 2)
                throw new ArgumentException("Must provide at least two adjancent values!");

            var loc = new StageTableLocation(StageTablePresets.doukutsuexe);

            //find where the names are
            var results = FindSequentialData(stream, names);

            var entryLen = (int)(results[1].Item1 - results[0].Item1);

            loc.Settings = BufferSizeToSettings(entryLen);

            long start;
            switch (option)
            {
                case StageTableSearchOptions.Tileset:
                    //First result should be the first tileset, which is at the start of the first entry
                    //Just subtract the offset normally and we should be good
                    start = results[0].Item1 - (entryLen * startingIndex);
                    break;
                case StageTableSearchOptions.Name:
                    //(Address of first name + length of name buffer) = start of second entry
                    //subtract (length of each entry * (index of first item + 1)) to get to the start of the table
                    start = (results[0].Item1 + results[0].Item2) - (entryLen * (startingIndex + 1));
                    break;
                default:
                    throw new ArgumentException(nameof(option));
            }
            loc.Offset = (int)start;

            stream.Position = start;
            loc.StageCount = DetermineEntryCount(stream, loc.Settings);

            if (loc.StageCount < names.Length)
                throw new FileLoadException("Wasn't able to verify every stage supplied. Parameters are most likely wrong.");

            return loc;
        }

        //TODO consider making this function only work on pe files
        //since unless I want to make code for dealing with ELF files, as well as every other possible EXE format...
        private static StageTableReferences FindReferences(Stream file, PEFile pe, StageTableLocation location, StageTableEntrySettings settings)
        {
            if (location.DataLocationType != DataLocationTypes.Internal)
                throw new ArgumentException("Can only find references for internal stage tables!", nameof(location.DataLocationType));

            uint startOfStageTable;
            if (pe != null)
            {
                startOfStageTable = pe.optionalHeader32.ImageBase;
                if (!string.IsNullOrEmpty(location.SectionName))
                {
                    if (!pe.TryGetSection(location.SectionName, out var sect))
                        throw new KeyNotFoundException();
                    startOfStageTable += sect.VirtualAddress;
                }
                else
                {
                    startOfStageTable += (uint)location.Offset;
                }
            }
            else
                startOfStageTable = 0x400000;

            switch (location.StageTableFormat)
            {
                case StageTableFormats.swdata:
                    startOfStageTable += (uint)StageTable.SWDATAHeader.Length;
                    break;
                case StageTableFormats.mrmapbin:
                    startOfStageTable += (uint)sizeof(int);
                    break;
            }

            var r = new StageTableReferences();
            using (var br = new BinaryReader(file, Encoding.Default, true))
            {
                //TODO remake this to only loop over the file once
                //this should increase performance when reading directly off disk
                List<long> FindReferences(uint offset)
                {
                    var results = new List<long>();

                    br.BaseStream.Position = 0;
                    uint check = br.ReadUInt32();

                    while (br.BaseStream.Position < br.BaseStream.Length)
                    {
                        if (check == offset)
                            results.Add(br.BaseStream.Position - 4);
                        check >>= 8;
                        check |= (uint)br.ReadByte() << 24;
                    }

                    return results;
                }
                r.TilesetReferences = FindReferences(startOfStageTable);
                r.FilenameReferences = FindReferences(startOfStageTable += (uint)settings.TilesetNameBuffer);
                r.BackgroundTypeReferences = FindReferences(startOfStageTable += (uint)settings.FilenameBuffer);
                r.BackgroundNameReferences = FindReferences(startOfStageTable += (uint)Marshal.SizeOf(settings.BackgroundTypeType));
                r.Spritesheet1References = FindReferences(startOfStageTable += (uint)settings.BackgroundNameBuffer);
                r.Spritesheet2References = FindReferences(startOfStageTable += (uint)settings.Spritesheet1Buffer);
                r.BossNumberReferences = FindReferences(startOfStageTable += (uint)settings.Spritesheet2Buffer);

                startOfStageTable += (uint)Marshal.SizeOf(settings.BossNumberType);
                if (settings.JapaneseNameBuffer > 0)
                {
                    r.JapaneseNameReferences = FindReferences(startOfStageTable);
                    startOfStageTable += (uint)settings.JapaneseNameBuffer;
                }
                r.MapNameReferences = FindReferences(startOfStageTable);
            }

            return r;
        }

        #endregion

        #region Embedded NPC Table detection
        public static NPCTableLocation FindEmbeddedNpcTable(string file, NPCTableLocation npctblPath, int fullSize = 0)
        {
            return FindEmbeddedNpcTable(file, npctblPath.Read(), fullSize);
        }
        public static NPCTableLocation FindEmbeddedNpcTable(string path, IList<NPCTableEntry> npctbl, int fullCount = 0)
        {
            if (fullCount < 1)
                fullCount = npctbl.Count;
            else if (fullCount < npctbl.Count)
                throw new ArgumentException($"{nameof(fullCount)} must be greater than or equal to the size of {nameof(npctbl)}!",
                    nameof(fullCount));

            var buffer = new byte[npctbl.Count * NPCTableEntry.Size];
            using (var bw = new BinaryWriter(new MemoryStream(buffer, true)))
                bw.WriteNPCTableByEntry(npctbl);

            int index = 0;
            int offset;
            using (var file = new BinaryReader(Extensions.OpenInMemory(path)))
            {
                while (index < buffer.Length && file.BaseStream.Position < file.BaseStream.Length)
                {
                    if (file.ReadByte() != buffer[index++])
                        index = 0;
                }
                if (index < buffer.Length)
                    return null;

                offset = (int)(file.BaseStream.Position - buffer.Length);

                /* Unfortunately, a system like this is really inaccurate
                 * when we're dealing with such small data types
                var foundEntries = npctbl.Count;
                while (true)
                {
                    var entry = file.ReadNPCTableEntry();
                    if (entry.SpriteSurface >= 40)
                        break;
                    foundEntries++;
                }
                Debug.Assert(foundEntries == fullCount);
                */
            }

            return new NPCTableLocation()
            {
                NpcTableFormat = NPCTableFormats.ByEntry,
                NpcCount = fullCount,
                DataLocationType = DataLocationTypes.Internal,
                Offset = offset,
                MaximumSize = fullCount * NPCTableEntry.Size,
                FixedSize = true,
            };
        }
        #endregion

        #region Folder contains files
        public static float ContainsStages(string path, IList<StageTableEntry> entries)
        {
            //Not using .Distinct() here since filenames are 99.999% of the time already distinct
            var filenames = entries.Select(x => x.Filename).ToList();
            return ContainsFilenames(path, filenames);
        }
        public static float ContainsSpritesheets(string path, List<List<StageTableEntry>> tables, string imageExtension, string prefix = "*")
        {
            var spritesheets = GetSpritesheets(tables);
            return ContainsFilenames(path, spritesheets, prefix, imageExtension);
        }
        public static float ContainsSpritesheets(string path, IList<StageTableEntry> entries, string extension)
        {
            var spritesheets = entries.SelectMany(x => new[] { x.Spritesheet1, x.Spritesheet2 }).Distinct().ToList();
            return ContainsFilenames(path, spritesheets, "*", extension);
        }
        public static float ContainsBackgrounds(string path, List<List<StageTableEntry>> tables)
        {
            var backgrounds = GetBackgrounds(tables);
            return ContainsFilenames(path, backgrounds);
        }
        public static float ContainsBackgrounds(string path, IList<StageTableEntry> entries)
        {
            var backgrounds = entries.Select(x => x.BackgroundName).Distinct().ToList();
            return ContainsFilenames(path, backgrounds);
        }

        /// <summary>
        /// Check if the given directory contains the given filenames, using the given prefix/extension rules
        /// </summary>
        /// <param name="path">The directory to search</param>
        /// <param name="names">The filenames to look for</param>
        /// <param name="prefix">The prefix to attach</param>
        /// <param name="extension">The extension to attach</param>
        /// <returns>What percentage of files were found</returns>
        public static float ContainsFilenames(string path, IList<string> names, string prefix = "", string extension = ".*")
        {
            var found = 0;
            foreach (var name in names)
                if (Extensions.EnumerateFilesCaseInsensitive(path,
                    Path.ChangeExtension(prefix + name, extension)).Any())
                    found++;
            return (float)found / names.Count;
        }

        #endregion

        #region External Table Detection

        public static int ContainsExternalArmsLevelTables(string dir, out List<ArmsLevelTableLocation> foundTables)
        {
            foundTables = new List<ArmsLevelTableLocation>(1);
            foreach (var table in Directory.EnumerateFiles(dir, ArmsLevelTableLocation.ARMS_LEVELTABLE))
            {
                foundTables.Add(new ArmsLevelTableLocation(table));
            }
            return foundTables.Count;
        }
        public static int ContainsExternalBulletTables(string dir, out List<BulletTableLocation> foundTables)
        {
            foundTables = new List<BulletTableLocation>(1);
            foreach (var table in Directory.EnumerateFiles(dir, BulletTableLocation.BULLETTABLE))
            {
                foundTables.Add(new BulletTableLocation(table, BulletTablePresets.csplus));
            }
            return foundTables.Count;
        }
        public static int ContainsExternalNpcTables(string dir, out List<NPCTableLocation> foundTables)
        {
            foundTables = new List<NPCTableLocation>(1);
            foreach (var table in Directory.EnumerateFiles(dir, NPCTableLocation.NPCTBL))
            {
                foundTables.Add(new NPCTableLocation(table));
            }
            return foundTables.Count;
        }

        public static int ContainsExternalStageTables(string dir, out List<StageTableLocation> foundTables)
        {
            foundTables = new List<StageTableLocation>(2);
            foreach (var file in new[]
            {
                    (StageTable.STAGETBL, StageTablePresets.stagetbl),
                    (StageTable.MRMAPBIN, StageTablePresets.mrmapbin)
                })
            {
                foreach (var table in Directory.EnumerateFiles(dir, file.Item1))
                {
                    foundTables.Add(new StageTableLocation(table, file.Item2));
                }
            }
            return foundTables.Count;
        }
        public static bool TryFindExternalTables(string path, out ExternalTables externalTables)
        {
            externalTables = new ExternalTables();
            //using | to force all functions to run
            return ContainsExternalStageTables(path, out externalTables.StageTables) > 0
                | ContainsExternalNpcTables(path, out externalTables.NpcTables) > 0
                | ContainsExternalBulletTables(path, out externalTables.BulletTables) > 0
                | ContainsExternalArmsLevelTables(path, out externalTables.ArmsLevelTables) > 0;
        }

        //Find external tables (when NO internal tables have been found)
        public static ExternalTables FindExternalTables(string path)
        {
            if (TryFindExternalTables(path, out var ext))
            {
                return ext;
            }
            else
            {
                //if not, we might be in a CS+ meta-directory, so we need to check one layer in
                foreach (var dir in Directory.EnumerateDirectories(path))
                {
                    if (TryFindExternalTables(dir, out var externalTables))
                    {
                        return externalTables;
                    }
                }
            }
            return null;
        }

        #endregion

        #region Data folder/Image Extension Detection
        public static List<string> GetBackgrounds(List<List<StageTableEntry>> tables)
        {
            return tables.SelectMany(x => x.Select(y => y.BackgroundName)).Distinct().ToList();
        }
        public static float DetermineImageExtensionFromBackgrounds(string path, List<List<StageTableEntry>> tables, out List<KeyValuePair<string, float>> results)
        {
            return DetermineImageExtensionFromBackgrounds(path, GetBackgrounds(tables), out results);
        }
        public static float DetermineImageExtensionFromBackgrounds(string path, List<string> backgrounds, out List<KeyValuePair<string, float>> results)
        {
            return FilterDirectory(path, backgrounds, ".*", (f, i) => Path.GetExtension(f), out results);
        }

        public static string FindDataFolderAndImageExtension(string path, List<List<StageTableEntry>> tables, out List<KeyValuePair<string,float>> foundExtensions)
        {
            //check if the given folder already has the backgrounds
            //if it does, we're already looking at a data folder
            if (DetermineImageExtensionFromBackgrounds(path, tables, out foundExtensions) > 0.9)
            {
                return path;
            }
            else
            {
                //if not, we might be in a CS+ meta-directory, so we need to check one layer in
                foreach (var dir in Directory.EnumerateDirectories(path))
                {
                    if (DetermineImageExtensionFromBackgrounds(dir, tables, out foundExtensions) > 0.9)
                    {
                        return dir;
                    }
                }

            }
            return null;
        }

        public static int CountHardcodedDataFiles(string path, string imageExtension)
        {
            var items = new string[]
            {
                "Arms",
                "ArmsImage",
                "Bullet",
                "Caret",
                "casts",
                "Fade",
                "ItemImage",
                "Loading",
                "MyChar",
                "StageImage",
                "TextBox",
                "Title",
                "Face*" //* is to catch CS+ style animated face pics
            };
            var found = 0;
            foreach (var item in items)
            {
                if (Extensions.EnumerateFilesCaseInsensitive(path, Path.ChangeExtension(item, imageExtension)).Any())
                    found++;
            }
            return found;
        }

        #endregion

        /// <summary>
        /// Search the given path for an npc an stage folder.
        /// It is highly recommended that you read any info from the stage table(s) outside of the passed functions
        /// </summary>
        /// <param name="path">Directory to search</param>
        /// <param name="NpcTest">Test if the given path is an npc folder</param>
        /// <param name="StageTest">Test if the given path is a stage folder</param>
        /// <returns>NpcPath, StagePath</returns>
        public static Tuple<string, string> FindNpcAndStageFolders(string path, Func<string, bool> NpcTest, Func<string, bool> StageTest)
        {
            string npcDir = null;
            string stageDir = null;
            foreach (var dir in Extensions.EnumerateDirectoriesCaseInsensitive(path, NPC))
            {
                if (NpcTest(dir))
                {
                    npcDir = dir;
                    break;
                }
            }
            foreach (var dir in Extensions.EnumerateDirectoriesCaseInsensitive(path, STAGE))
            {
                if (StageTest(dir))
                {
                    stageDir = dir;
                    break;
                }
            }

            //boy I sure do love checking this 500000 times
            if (npcDir == null || stageDir == null)
            {
                using (var e = Directory.EnumerateDirectories(path).GetEnumerator())
                {
                    while ((npcDir == null || stageDir == null) && e.MoveNext())
                    {
                        if (npcDir == null && NpcTest(e.Current))
                        {
                            npcDir = e.Current;
                            continue;
                        }
                        if (stageDir == null && StageTest(e.Current))
                        {
                            stageDir = e.Current;
                        }
                    }
                }
            }

            return Tuple.Create(npcDir, stageDir);
        }

        #region Npc folder/prefix detection
        public const string NPC = "npc";
        public const float NPC_INIT_THRESHOLD = 0.9f;
        public static bool TryInitFromNpcFolder(string path, List<string> spritesheets, string imageExtension, ProjectFile project)
        {
            if (FindNpcPrefix(path, spritesheets, imageExtension, out var prefixes) > NPC_INIT_THRESHOLD)
            {
                var npcPrefix = GetMaxes(prefixes);
                if (npcPrefix.Count == 1)
                {
                    project.SpritesheetPrefix = npcPrefix[0];
                    return true;
                }
            }
            return false;
        }
        public static List<string> GetSpritesheets(List<StageTableEntry> table)
        {
            return table.SelectMany(y => new[] { y.Spritesheet1, y.Spritesheet2 }).Distinct().ToList();
        }
        public static List<string> GetSpritesheets(List<List<StageTableEntry>> tables)
        {
            return tables.SelectMany(x => x.SelectMany(y => new[] { y.Spritesheet1, y.Spritesheet2}))
                .Distinct().ToList();
        }
        public static float FindNpcPrefix(string path, List<List<StageTableEntry>> tables, string imageExtension, out List<KeyValuePair<string, float>> results)
        {
            return FindNpcPrefix(path, GetSpritesheets(tables), imageExtension, out results);
        }
        public static float FindNpcPrefix(string path, List<string> spritesheets, string imageExtension, out List<KeyValuePair<string, float>> results)
        {
            return FilterDirectory(path, spritesheets, imageExtension, (f,i) => f.ReplaceCaseInsensitive(Path.ChangeExtension(i, imageExtension),""), out results);
        }

        public static float ContainsNpcFiles(IEnumerable<string> spritesheets, string path, string imageExtension, string spritesheetPrefix)
        {
            var sheets = MakeHashset(spritesheets);

            var files = Directory.GetFiles(path);
            var found = 0;
            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file).ToLower();
                var ext = Path.GetExtension(file);
                if (name.StartsWith(spritesheetPrefix, StringComparison.OrdinalIgnoreCase) && ext == imageExtension
                    && sheets.Contains(name.ReplaceCaseInsensitive(spritesheetPrefix, "")))
                    found++;
            }

            return (float)found / files.Length;
        }

        #endregion

        #region Stage Folder Detection
        public const string STAGE = "stage";

        [DebuggerDisplay("({Option1} vs {Option2})")]
        class VersusCounter
        {
            public int Option1;
            public int Option2;
        }
        public static List<string> GetFilenames(List<StageTableEntry> table)
        {
            return table.Select(y => y.Filename).Distinct().ToList();
        }
        public static List<string> GetFilenames(List<List<StageTableEntry>> tables)
        {
            return tables.SelectMany(x => x.Select(y => y.Filename)).Distinct().ToList();
        }
        public static bool TryFindStageExtensions(string path, List<List<StageTableEntry>> tables, out StageFolderSearchResults stageExtensions)
        {
            return TryFindStageExtensions(path, GetFilenames(tables), out stageExtensions);
        }
        public static bool TryParseStageFolder(string path, List<List<StageTableEntry>> tables, string imageExtension, out StageFolderSearchResults stageResults, out AttributeInfo attributeResults)
        {
            var filenames = GetFilenames(tables);
            var tilesets = GetTilesets(tables);
            return TryParseStageFolder(path, filenames, tilesets, imageExtension, out stageResults, out attributeResults);
        }
        public static bool TryParseStageFolder(string path, List<string> filenames, List<string> tilesets, string imageExtension, out StageFolderSearchResults stageResults, out AttributeInfo attributeResults)
        {
            return TryFindStageExtensions(path, filenames, out stageResults)
                //using & so we return as much info as possible
                & TryFindAttributeExtension(path, tilesets, imageExtension, stageResults, out attributeResults);
        }
        public static bool TryInitFromStageFolder(string path, List<string> filenames, List<string> tilesets, string imageExtension, ProjectFile project)
        {
            if (TryParseStageFolder(path, filenames, tilesets, imageExtension, out var stage, out var attrib))
            {
                project.EntityExtension = stage.FoundEntities;
                project.MapExtension = stage.FoundMaps;
                project.ScriptExtension = stage.FoundScripts;
                project.ScriptsEncrypted = stage.ScriptsEncrypted;

                project.AttributeExtension = attrib.AttributeExtension;
                project.TilesetPrefix = attrib.TilesetPrefix;

                return true;
            }
            return false;
        }
        public static bool TryFindStageExtensions(string path, List<string> filenames, out StageFolderSearchResults stageExtensions)
        {
            stageExtensions = null;

            //Find filenames/initial candidate extensions
            var FoundExts = new HashSet<string>();
            var FoundNames = new Dictionary<string, HashSet<string>>(filenames.Count);
            foreach(var entry in filenames)
            {
                foreach(var file in Extensions.EnumerateFilesCaseInsensitive(path, entry + ".*"))
                {
                    var ext = Path.GetExtension(file);
                    if (!FoundExts.Contains(ext))
                        FoundExts.Add(ext);

                    var name = Path.GetFileNameWithoutExtension(file);
                    if (!FoundNames.ContainsKey(name))
                        FoundNames.Add(name, new HashSet<string> { ext });
                    else
                        FoundNames[name].Add(ext);
                }
            }
            if (FoundExts.Count < 3) //didn't even find enough extensions
                return false;
            
            //check if we found enough SHARED extensions
            var exts = new Dictionary<string, VersusCounter>(FoundExts.Count);
            foreach(var ext in FoundExts)
            {
                if(FoundNames.All(x => x.Value.Contains(ext)))
                    exts.Add(ext, new VersusCounter());
            }
            if (exts.Count > 3)
                throw new Exception("Found more than 3 possible extensions!");

            //Score the extensions by how many PXM/PXEs they have
            //Note: only checking the HEADER, not the one byte after
            var max = Math.Max(Map.DefaultHeader.Length, PXE.DefaultHeader.Length);
            const int Cutoff = 3;
            foreach (var ext in exts.Keys)
            {
                foreach (var file in Directory.EnumerateFiles(path, "*" + ext))
                {
                    if (exts[ext].Option1 > Cutoff || exts[ext].Option2 > Cutoff)
                        break;

                    var data = new byte[max];
                    using(var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                        fs.Read(data, 0, data.Length);
                    if (data.SequenceEqual(Map.DefaultHeader))
                        exts[ext].Option1++;
                    else if (data.SequenceEqual(PXE.DefaultHeader))
                        exts[ext].Option2++;
                }
            }
            //gather the results
            var MaxMap = 0;
            var MapExt = "";
            var MaxEnt = 0;
            var EntExt = "";
            foreach (var ext in exts)
            {
                if (ext.Value.Option1 > MaxMap)
                    MapExt = ext.Key;
                else if (ext.Value.Option2 > MaxEnt)
                    EntExt = ext.Key;
            }
            exts.Remove(MapExt);
            exts.Remove(EntExt);

            //should be left with just scripts?
            if (exts.Count > 1)
                throw new Exception("More than 1 script candidate left!");

            var searchString = Encoding.ASCII.GetBytes("<END");
            foreach (var ext in exts.Keys)
            {
                exts[ext].Option1 = 0;
                exts[ext].Option2 = 0;
                foreach (var file in Directory.EnumerateFiles(path, "*" + ext))
                {
                    using (var data = Extensions.OpenInMemory(file))
                    {
                        var text = Encoding.ASCII.GetString((data as MemoryStream).ToArray());
                        //if the file already has <END, that's one point for "unencrypted TSC"
                        if(data.FindBytes(searchString) != -1)
                        {
                            exts[ext].Option1++;
                        }
                        else
                        {
                            //if it needed to be decrypted first, that's a point for encrypted
                            TSC.Encryptor.DecryptInPlace(data);
                            text = Encoding.ASCII.GetString((data as MemoryStream).ToArray());
                            data.Position = 0;
                            if(data.FindBytes(searchString) != -1)
                                exts[ext].Option2++;
                        }
                        //otherwise it's a point for nothing...
                    }
                }
            }
            bool encrypted = true;
            int ScriptMax = 0;
            string ScriptExt = "";
            foreach(var ext in exts)
            {
                if (ext.Value.Option1 > ScriptMax)
                {
                    encrypted = false;
                    ScriptMax = ext.Value.Option1;
                    ScriptExt = ext.Key;
                }
                else if(ext.Value.Option2 > ScriptMax)
                {
                    encrypted = true;
                    ScriptMax = ext.Value.Option2;
                    ScriptExt = ext.Key;
                }
            }
            stageExtensions = new StageFolderSearchResults(MapExt,EntExt,ScriptExt,encrypted);
            return true;
        }

        public static float ContainsStageFiles(List<StageTableEntry> table, string path,
            string tilesetPrefix, string attributeExtension, string imageExtension, string mapExtension, string entityExtension, string scriptExtension)
        {
            var filenames = MakeHashset(GetFilenames(table));
            var tilesets = MakeHashset(GetTilesets(table));

            var exts = new HashSet<string>()
            {
                entityExtension,
                scriptExtension,
                mapExtension
            };

            var files = Directory.GetFiles(path);
            var found = 0;
            foreach (var file in files)
            {
                var ext = Path.GetExtension(file);
                var name = Path.GetFileNameWithoutExtension(file).ToLower();
                if (ext == imageExtension)
                {
                    if (tilesets.Contains(name.ReplaceCaseInsensitive(tilesetPrefix, "")))
                        found++;
                }
                else if (ext == attributeExtension)
                {
                    if (tilesets.Contains(name))
                        found++;
                }
                else if (exts.Contains(ext))
                {
                    if (filenames.Contains(name))
                        found++;
                }
            }

            return (float)found / files.Length;
        }

        #endregion

        #region Attribute detection
        public static List<string> GetTilesets(List<StageTableEntry> table)
        {
            return table.Select(y => y.TilesetName).Distinct().ToList();
        }
        public static List<string> GetTilesets(List<List<StageTableEntry>> tables)
        {
            return tables.SelectMany(x => x.Select(y => y.TilesetName)).Distinct().ToList();
        }
        public static bool TryFindAttributeExtension(string path, List<List<StageTableEntry>> tables, string imageExtension, StageFolderSearchResults exclude, out AttributeInfo inf)
        {
            return TryFindAttributeExtension(path, GetTilesets(tables), imageExtension, exclude, out inf);
        }
        public static bool TryFindAttributeExtension(string path, IEnumerable<string> tilesets, string imageExtension, StageFolderSearchResults exclude, out AttributeInfo inf)
        { 
            if (!imageExtension.StartsWith("."))
                imageExtension = "." + imageExtension;

            var excludeExts = new HashSet<string>()
            {
                exclude.FoundEntities,
                exclude.FoundScripts,
                exclude.FoundMaps
            };

            var foundPrefixes = new HashSet<string>();
            var foundExts = new HashSet<string>();
            foreach(var tileset in tilesets)
            {
                foreach(var file in Extensions.EnumerateFilesCaseInsensitive(path, "*" + tileset + ".*"))
                {
                    var ext = Path.GetExtension(file);
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (ext == imageExtension)
                        foundPrefixes.Add(name.ReplaceCaseInsensitive(tileset, ""));
                    else if(!excludeExts.Contains(ext))
                        foundExts.Add(ext);
                }
            }
            //if we found more than 1 candidate, we might need to check for pxw files
            if(foundExts.Count > 1)
            {
                //using ToList() because we're about to modify the collection
                foreach (var ext in foundExts.ToList())
                {
                    bool pxw = false;
                    foreach(var file in Extensions.EnumerateFilesCaseInsensitive(path, "*" + ext))
                    {
                        try
                        {
                            _ = Compatability.CaveStoryPlus.WaterAttributes.Read(file);
                            pxw = true;
                        }
                        catch (Exception e)
                        {
#if DEBUG
                            Debug.WriteLine("Threw " + e.GetType() + " on " + file + "\n" + e.Message);
#endif
                        }
                        //only need to test one file really...
                        break;
                    }
                    if (pxw)
                        foundExts.Remove(ext);
                }
            }
            //if it's not 1 by this point, fail
            if (foundPrefixes.Count == 1 && foundExts.Count == 1)
            {
                inf = new AttributeInfo(foundPrefixes.First(), foundExts.First());
                return true;
            }
            else
            {
                inf = null;
                return false;
            }
        }
        #endregion
    }
}
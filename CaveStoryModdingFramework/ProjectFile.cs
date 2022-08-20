using CaveStoryModdingFramework.AutoDetection;
using CaveStoryModdingFramework.Entities;
using CaveStoryModdingFramework.Stages;
using CaveStoryModdingFramework.TSC;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace CaveStoryModdingFramework
{
    public class CustomEnums
    {
        public SerializableDictionary<int, EntityInfo> EntityInfos { get; set; } = new SerializableDictionary<int, EntityInfo>();

        public SerializableDictionary<int, BulletInfo> BulletInfos { get; set; } = new SerializableDictionary<int, BulletInfo>();

        public SerializableDictionary<int, ISurfaceSource> SurfaceDescriptors { get; set; } = new SerializableDictionary<int, ISurfaceSource>();

        public SerializableDictionary<int, string> SoundEffects { get; set; } = new SerializableDictionary<int, string>();

        public SerializableDictionary<int, string> SmokeSizes { get; set; } = new SerializableDictionary<int, string>();

        public SerializableDictionary<long, string> BossNumbers { get; set; } = new SerializableDictionary<long, string>();

        public SerializableDictionary<long, string> BackgroundTypes { get; set; } = new SerializableDictionary<long, string>();

        public SerializableDictionary<string, SerializableDictionary<long, string>> UserEnums { get; set; } = new SerializableDictionary<string, SerializableDictionary<long, string>>();
    }
    [DebuggerDisplay("Key = {Key} Write = {Write}")]
    public class TableLoadInfo
    {
        [XmlAttribute]
        public bool Write { get; set; }

        [XmlText]
        public string Key { get; set; }

        public TableLoadInfo() { }
        public TableLoadInfo(string key, bool write)
        {
            Key = key;
            Write = write;
        }
    }
    [DebuggerDisplay("{DataPaths[DataPaths.Count-1]}")]
    public class AssetLayout
    {
        /// <summary>
        /// Search from end to start
        /// </summary>
        public List<string> DataPaths { get; set; } = new List<string>();
        /// <summary>
        /// Search from end to start
        /// </summary>
        public List<string> StagePaths { get; set; } = new List<string>();
        /// <summary>
        /// Search from end to start
        /// </summary>
        public List<string> NpcPaths { get; set; } = new List<string>();

        public List<TableLoadInfo> StageTables { get; set; } = new List<TableLoadInfo>();
        public List<TableLoadInfo> NpcTables { get; set; } = new List<TableLoadInfo>();
        public List<TableLoadInfo> BulletTables { get; set; } = new List<TableLoadInfo>();
        public List<TableLoadInfo> ArmsLevelTables { get; set; } = new List<TableLoadInfo>();

        /// <summary>
        /// Search for the given filename in the given list of directories
        /// </summary>
        /// <param name="paths">The list paths to search</param>
        /// <param name="filename">The filename to search for</param>
        /// <param name="foundFiles">The list of files found (case insensitive)</param>
        /// <param name="searchAll">Whether or not to search all directories, instead of stopping at the first found one</param>
        /// <returns>Whether or not an exact match was found (case sensitive)</returns>
        public bool SearchForFile(List<string> paths, string filename, out List<string> foundFiles, bool searchAll = false)
        {
            foundFiles = new List<string>();
            bool foundExact = false;
            for(int i = paths.Count - 1; i >= 0; i--)
            {
                foreach(var file in Extensions.EnumerateFilesCaseInsensitive(paths[i], filename))
                {
                    if(Path.GetFileName(file) == filename)
                        foundExact = true;
                    foundFiles.Add(file);
                }
                if (!searchAll && foundFiles.Count > 0)
                    break;
            }
            return foundExact;
        }

        public int TileSize { get; set; } = 16;

        public SerializableDictionary<int, EntityInfo> EntityOverrides { get; set; } = new SerializableDictionary<int, EntityInfo>();

        public AssetLayout() { }
        public AssetLayout(AssetLayout parent, bool forceReadonly = false)
        {
            void DeepCopyStrings(List<string> orig, List<string> other)
            {
                foreach(var item in other)
                {
                    orig.Add(item);
                }
            }
            DeepCopyStrings(DataPaths, parent.DataPaths);
            DeepCopyStrings(StagePaths, parent.StagePaths);
            DeepCopyStrings(NpcPaths, parent.NpcPaths);

            void DeepCopyTableLoadInfos(List<TableLoadInfo> orig, List<TableLoadInfo> other)
            {
                foreach (var item in other)
                {
                    orig.Add(new TableLoadInfo(item.Key, !forceReadonly && item.Write));
                }
            }
            DeepCopyTableLoadInfos(StageTables, parent.StageTables);
            DeepCopyTableLoadInfos(NpcTables, parent.NpcTables);
            DeepCopyTableLoadInfos(BulletTables, parent.BulletTables);
            DeepCopyTableLoadInfos(ArmsLevelTables, parent.ArmsLevelTables);
        }
        public override bool Equals(object obj)
        {
            if(obj is AssetLayout al)
            {
                return StageTables.SequenceEqual(al.StageTables) &&
                    DataPaths.SequenceEqual(al.DataPaths) &&
                    StagePaths.SequenceEqual(al.StagePaths) &&
                    NpcPaths.SequenceEqual(al.NpcPaths) &&
                    TileSize == al.TileSize;
            }
            else return base.Equals(obj);
        }
    }
    [DebuggerDisplay("{BaseDataPath}")]
    public class ProjectFile
    {
        public const string Extension = "cav";

        public string BaseDataPath { get; set; }
        public string EXEPath { get; set; }

        public SerializableDictionary<string, StageTableLocation> StageTables { get; set; } = new SerializableDictionary<string, StageTableLocation>();
        public SerializableDictionary<string, NPCTableLocation> NPCTables { get; set; } = new SerializableDictionary<string, NPCTableLocation>();
        public SerializableDictionary<string, BulletTableLocation> BulletTables { get; set; } = new SerializableDictionary<string, BulletTableLocation>();
        public SerializableDictionary<string, ArmsLevelTableLocation> ArmsLevelTables { get; set; } = new SerializableDictionary<string, ArmsLevelTableLocation>();

        public Color ImageTransparentColor { get; set; } = Color.Black;

        public bool ImagesCopyrighted { get; set; }
        public string ImageCopyrightText { get; set; }

        //this prefix is fake, but useful for user display maybe?
        public string BackgroundPrefix { get; set; } = "bk";
        public string TilesetPrefix { get; set; } = "Prt";
        public string SpritesheetPrefix { get; set; } = "Npc";

        //If a lot of people start modding GOG v1.0, this may have to be turned into a list...
        public string ImageExtension { get; set; } = "pbm";
        public string MapExtension { get; set; } = "pxm";
        public string EntityExtension { get; set; } = "pxe";
        public string AttributeExtension { get; set; } = "pxa";
        public string ScriptExtension { get; set; } = "tsc";
        

        public bool UseScriptSource { get; set; }
        public Encoding ScriptEncoding { get; set; } = Encoding.ASCII;
        public bool ScriptsEncrypted { get; set; }
        //divide the length of the tsc file by this number to find the TSC decryption key
        //there are two whole people who will find this useful :smile_eol:
        public int ScriptKeyLocation { get; set; } = 2;
        //when the key at FileSize/ScriptKeyLocation is 0, use this value instead
        public byte DefaultEncryptionKey { get; set; } = 7;

        //needs to be diffed with the global list?
        public SerializableDictionary<string, Command> ScriptCommands { get; set; }

        public int ScreenWidth { get; set; } = 320;
        public int ScreenHeight { get; set; } = 240;

        public SerializableDictionary<int, EntityInfo> EntityInfos { get; } = new SerializableDictionary<int, EntityInfo>(EntityList.EntityInfos);

        public int SelectedLayoutIndex { get; set; } = 0;
        public List<AssetLayout> Layouts { get; set; } = new List<AssetLayout>();
        public AssetLayout SelectedLayout => (0 <= SelectedLayoutIndex && SelectedLayoutIndex < Layouts.Count) ? Layouts[SelectedLayoutIndex] : null;

        #region table adding/reading/writing
        public void AddTables(ExternalTables externalTables)
        {
            void AddExternalTables<T>(List<T> tables, Dictionary<string, T> dest) where T : DataLocation
            {
                foreach (var result in tables)
                    dest.Add(AssetManager.MakeRelative(BaseDataPath, result.Filename), result);
            }
            AddExternalTables(externalTables.StageTables, StageTables);
            AddExternalTables(externalTables.NpcTables, NPCTables);
            AddExternalTables(externalTables.BulletTables, BulletTables);
            AddExternalTables(externalTables.ArmsLevelTables, ArmsLevelTables);
        }
        public void AddTables(ExternalTables externalTables, AssetLayout layout)
        {
            void AddExternalTables<T>(List<T> tables, Dictionary<string, T> dest, List<TableLoadInfo> l) where T : DataLocation
            {
                foreach (var result in tables)
                {
                    var key = AssetManager.MakeRelative(BaseDataPath, result.Filename);
                    dest.Add(key, result);
                    l.Add(new TableLoadInfo(key, true));
                }
            }
            AddExternalTables(externalTables.StageTables, StageTables, layout.StageTables);
            AddExternalTables(externalTables.NpcTables, NPCTables, layout.NpcTables);
            AddExternalTables(externalTables.BulletTables, BulletTables, layout.BulletTables);
            AddExternalTables(externalTables.ArmsLevelTables, ArmsLevelTables, layout.ArmsLevelTables);
        }
        public List<T> GetTables<T>(Dictionary<string, T> dict, List<TableLoadInfo> loadInfos) where T : DataLocation
        {
            var tables = new List<T>(loadInfos.Count);
            foreach (var loadInf in loadInfos)
                tables.Add(dict[loadInf.Key]);
            return tables;
        }
        public List<List<StageTableEntry>> ReadManyStageTables(AssetLayout layout)
        {
            var list = new List<List<StageTableEntry>>();
            foreach(var table in layout.StageTables)
            {
                list.Add(StageTables[table.Key].Read());
            }
            return list;
        }
        public List<StageTableEntry> ReadStageTables(AssetLayout layout)
        {
            if (layout.StageTables.Count <= 0)
                return new List<StageTableEntry>();

            //init with first table
            var output = StageTables[layout.StageTables[0].Key].Read();

            //overlay the rest
            for(int i = 1; i < layout.StageTables.Count; i++)
            {
                var entries = StageTables[layout.StageTables[i].Key].Read();
                while (output.Count < entries.Count)
                    output.Add(null); //these will all be overwritten in the next loop
                for (int j = 0; j < entries.Count; j++)
                    output[j] = entries[j];                
            }

            return output;
        }
        public void WriteStageTables(List<StageTableEntry> entries, AssetLayout layout)
        {
            foreach(var table in layout.StageTables)
            {
                if (table.Write)
                {
                    StageTables[table.Key].Write(entries);
                }
            }
        }
        #endregion

        #region Getting assets from the Asset Layout
        bool GetAsset(string fullname, List<string> searchPaths, out List<string> foundFiles)
        {
            if(SelectedLayout != null && searchPaths != null)
            {
                return SelectedLayout.SearchForFile(searchPaths, fullname, out foundFiles);
            }
            else
            {
                foundFiles = null;
                return false;
            }
        }
        //background -> data (value + extension)
        public bool GetBackground(string name, out List<string> foundFiles)
        {
            return GetAsset(Path.ChangeExtension(name, ImageExtension),
                SelectedLayout?.DataPaths, out foundFiles);
        }
        //tileset -> stage (prefix + value + extension)
        public bool GetTileset(string name, out List<string> foundFiles)
        {
            return GetAsset(Path.ChangeExtension(TilesetPrefix + name, ImageExtension),
                SelectedLayout?.StagePaths, out foundFiles);
        }
        //attribute -> stage (value + extension)
        public bool GetAttributes(string name, out List<string> foundFiles)
        {
            return GetAsset(Path.ChangeExtension(name, AttributeExtension),
                SelectedLayout?.StagePaths, out foundFiles);
        }
        //filename(s) -> stage (value + extension)
        public bool GetTiles(string name, out List<string> foundFiles)
        {
            return GetAsset(Path.ChangeExtension(name, MapExtension),
                SelectedLayout?.StagePaths, out foundFiles);
        }
        public bool GetEntities(string name, out List<string> foundFiles)
        {
            return GetAsset(Path.ChangeExtension(name, EntityExtension),
                SelectedLayout?.StagePaths, out foundFiles);
        }
        public bool GetScript(string name, out List<string> foundFiles)
        {
            return GetAsset(Path.ChangeExtension(name, ScriptExtension),
                SelectedLayout?.StagePaths, out foundFiles);
        }
        //spritesheet(s) -> npc (prefix + value + extension)
        public bool GetSpritesheet(string name, out List<string> foundFiles)
        {
            return GetAsset(Path.ChangeExtension(SpritesheetPrefix + name, ImageExtension),
                SelectedLayout?.NpcPaths, out foundFiles);
        }
        #endregion

        /// <summary>
        /// Creates an empty project
        /// </summary>
        public ProjectFile() { }

        #region New/Load/Save

        /// <summary>
        /// Loads a project from the given path
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static ProjectFile Load(string path)
        {
            var relativePath = Path.GetDirectoryName(path);
            ProjectFile pf;
            
            var x = new XmlSerializer(typeof(ProjectFile));
            using(var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                pf = (ProjectFile)x.Deserialize(fs);

            void MakeLocationsAbsolute<T>(Dictionary<string, T> locations) where T : DataLocation
            {
                foreach (var loc in locations)
                {
                    loc.Value.Filename = AssetManager.MakeAbsolute(pf.BaseDataPath, loc.Value.Filename);
                }
            }
            void MakeListAbsolute(List<string> list)
            {
                for(int i = 0; i < list.Count; i++)
                {
                    list[i] = AssetManager.MakeAbsolute(pf.BaseDataPath, list[i]);
                }
            }
            void MakeLayoutsAbsolute(List<AssetLayout> layouts)
            {
                foreach (var layout in layouts)
                {
                    MakeListAbsolute(layout.DataPaths);
                    MakeListAbsolute(layout.NpcPaths);
                    MakeListAbsolute(layout.StagePaths);
                }
            }

            pf.BaseDataPath = AssetManager.MakeAbsolute(relativePath, pf.BaseDataPath);
            if(!string.IsNullOrEmpty(pf.EXEPath))
                pf.EXEPath = AssetManager.MakeAbsolute(relativePath, pf.EXEPath);

            MakeLayoutsAbsolute(pf.Layouts);

            MakeLocationsAbsolute(pf.StageTables);
            MakeLocationsAbsolute(pf.NPCTables);
            MakeLocationsAbsolute(pf.BulletTables);
            MakeLocationsAbsolute(pf.ArmsLevelTables);

            return pf;
        }

        //TODO this triggers a lot of filename overwrites
        //if this breaks things, then we may need to save a COPY of the project file where we overwrite things
        public void Save(string path)
        {
            using(var ms = new MemoryStream())
            {
                Save(ms, path);
                ms.Position = 0;
                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                {
                    ms.CopyTo(fs);
                }
            }   
        }
        public void Save(Stream stream, string path)
        {
            var relativePath = Path.GetDirectoryName(path);

            //TODO maybe replace KeyValuePair with Tuple? Investigate impact first
            List<KeyValuePair<string, string>> SaveLocations<T>(Dictionary<string, T> locations) where T : DataLocation
            {
                var olds = new List<KeyValuePair<string,string>>(locations.Count);
                foreach(var loc in locations)
                {
                    olds.Add(new KeyValuePair<string,string>(loc.Key, loc.Value.Filename));
                    loc.Value.Filename = AssetManager.MakeRelative(BaseDataPath, loc.Value.Filename);
                }
                return olds;
            }
            void RestoreLocations<T>(List<KeyValuePair<string, string>> olds, Dictionary<string, T> locations) where T : DataLocation
            {
                foreach(var old in olds)
                {
                    locations[old.Key].Filename = old.Value;
                }
            }
            List<string> SaveList(List<string> list)
            {
                var olds = new List<string>(list.Count);
                for(int i = 0; i < list.Count; i++)
                {
                    olds.Add(list[i]);
                    list[i] = AssetManager.MakeRelative(BaseDataPath, list[i]);
                }
                return olds;
            }
            void RestoreList(List<string> olds, List<string> list)
            {
                for(int i = 0; i < list.Count; i++)
                {
                    list[i] = olds[i];
                }
            }
            List<Tuple<List<string>,List<string>,List<string>>> SaveLayouts(List<AssetLayout> layouts)
            {
                var olds = new List<Tuple<List<string>, List<string>, List<string>>>(layouts.Count);
                foreach(var layout in layouts)
                {
                    olds.Add(new Tuple<List<string>, List<string>, List<string>>(
                        SaveList(layout.DataPaths),
                        SaveList(layout.StagePaths),
                        SaveList(layout.NpcPaths)
                        ));
                }
                return olds;
            }
            void RestoreLayouts(List<Tuple<List<string>, List<string>, List<string>>> olds, List<AssetLayout> layouts)
            {
                for(int i = 0; i < layouts.Count; i++)
                {
                    RestoreList(olds[i].Item1, layouts[i].DataPaths);
                    RestoreList(olds[i].Item2, layouts[i].StagePaths);
                    RestoreList(olds[i].Item3, layouts[i].NpcPaths);
                }
            }

            var oldStageTableNames = SaveLocations(StageTables);
            var oldNPCTableNames = SaveLocations(NPCTables);
            var oldBulletTableNames = SaveLocations(BulletTables);
            var oldArmsLevelTableNames = SaveLocations(ArmsLevelTables);

            var oldLayouts = SaveLayouts(Layouts);

            var oldBaseDataPath = BaseDataPath;
            BaseDataPath = AssetManager.MakeRelative(relativePath, BaseDataPath);
            var oldEXEPath = EXEPath;
            if(!string.IsNullOrEmpty(EXEPath))
                EXEPath = AssetManager.MakeRelative(relativePath, EXEPath);
            try
            {
                var x = new XmlSerializer(typeof(ProjectFile));
                x.Serialize(stream, this);
            }
            finally
            {
                BaseDataPath = oldBaseDataPath;
                EXEPath = oldEXEPath;

                RestoreLayouts(oldLayouts, Layouts);

                RestoreLocations(oldStageTableNames, StageTables);
                RestoreLocations(oldNPCTableNames, NPCTables);
                RestoreLocations(oldBulletTableNames, BulletTables);
                RestoreLocations(oldArmsLevelTableNames, ArmsLevelTables);
            }
        }
        #endregion

        public override bool Equals(object obj)
        {
            if(obj is ProjectFile p)
            {
                //TODO expand
                return BackgroundPrefix == p.BackgroundPrefix &&
                    TilesetPrefix == p.TilesetPrefix &&
                    SpritesheetPrefix == p.SpritesheetPrefix &&
                    ImageExtension == p.ImageExtension &&
                    MapExtension == p.MapExtension &&
                    EntityExtension == p.EntityExtension &&
                    AttributeExtension == p.AttributeExtension &&
                    ScriptExtension == p.ScriptExtension; 
            }
            else
                return base.Equals(obj);
        }
    }
}

using CaveStoryModdingFramework.Entities;
using CaveStoryModdingFramework.TSC;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CaveStoryModdingFramework
{
    public static class Analysis
    {
        [Flags]
        public enum FlagChangeSources
        {
            TSC = 1,
            Entity = 2,
            Both = TSC | Entity
        }
        [Flags]
        public enum FlagChangeTypes
        {
            None = 0,
            Read = 1,
            Write = 2,
            //Both = Read | Write //removed this because I want ToString to actually return "Read | Write"
        }
        [Flags]
        public enum FlagEntryWarnings
        {
            SpecialEntityNoWriteFlag = 1,
            ReadFlag0 = 2,
            RunEventNoWriteFlag = 4,
        }
        //Nested type
#pragma warning disable CA1034
        public class FlagListEntry
#pragma warning restore CA1034
        {
            public int Flag { get; set; }
            public FlagChangeSources Source { get; set; }
            public string Filename { get; set; }
            public FlagChangeTypes Type { get; set; }

            public string TSCEvent { get; set; } = null;
            public string TSCCommand { get; set; } = null;

            public int EntityIndex { get; set; } = -1;
            public int EntityType { get; set; } = -1;
            public string EntityName { get; set; } = null;
            public EntityFlags EntityBits { get; set; } = 0;

            public FlagEntryWarnings EntityOversightWarning { get; set; } = 0;

            public FlagListEntry(int flag, FlagChangeSources source, string filename, FlagChangeTypes type)
            {
                Flag = flag;
                Source = source;
                Filename = filename;
                Type = type;
            }
        }

        static EntityFlags GetRealEntityFlags(Entity ent, IList<NPCTableEntry> npctbl)
        {
            var bits = ent.Bits;
            if (ent.Type < npctbl.Count)
                bits |= npctbl[ent.Type].Bits;
            return bits;
        }

        static string GetEntityName<T>(Entity ent, T infos) where T : IDictionary<int, EntityInfo>
        {
            if (infos.TryGetValue(ent.Type, out var inf))
                return inf.Name;
            else
                return "";
        }

        static string LoadTSC(string path, bool encrypted, byte defaultKey, Encoding encoding)
        {
            byte[] input = File.ReadAllBytes(path);
            if (encrypted)
                Encryptor.DecryptInPlace(input, defaultKey);
            return encoding.GetString(input);
        }

        public static void AddCreditsTSC<F>(string tscPath, F flagList, bool encrypted, byte defaultKey, Encoding encoding) where F : IList<FlagListEntry>
        {
            string filename = Path.GetFileName(tscPath);
            var text = LoadTSC(tscPath, encrypted, defaultKey, encoding);

            var curreve = "N/A";
            for (var index = 0; index < text.Length; index++)
            {
                switch (text[index])
                {
                    //skip text
                    case '[':
                        index = text.IndexOf(']', index);
                        //HACK I hate how this breaks the nice flow the rest of the code has
                        if (index == -1)
                            return;
                        break;
                    //l == # in credits
                    case 'l':
                        var l = text.Substring(index + 1, 4);
                        if (char.IsDigit(l[0]) && char.IsDigit(l[1]) && char.IsDigit(l[2]) && char.IsDigit(l[3]))
                            curreve = l;
                        index += 4;
                        break;
                    //f == FLJ in credits
                    case 'f':
                        flagList.Add(new FlagListEntry(FlagConverter.FlagToRealValue(text.Substring(index + 1, 4), 4), FlagChangeSources.TSC, filename, FlagChangeTypes.Read)
                        {
                            TSCCommand = "<FLJ",
                            TSCEvent = curreve
                        });
                        //$"<FLJ {Path.GetFileName(tscPath)} event #{curreve}");
                        index += 4;
                        break;
                }
            }
        }
        public static void AddTSC<F>(string tscPath, F flagList, bool encrypted, byte defaultKey, Encoding encoding) where F : IList<FlagListEntry>
        {
            AddTSC<F,IList<Entity>,IList<NPCTableEntry>,IDictionary<int,EntityInfo>>(tscPath, flagList, encrypted, defaultKey, encoding, null, null, null);
        }
        public static void AddTSC<F,P,N,E>(string tscPath, F flagList, bool encrypted, byte defaultKey, Encoding encoding, P entities, N npcTable, E entityInfos)
            where F : IList<FlagListEntry> where P : IList<Entity> where N : IList<NPCTableEntry> where E : IDictionary<int,EntityInfo>
        {
            string filename = Path.GetFileName(tscPath);
            var text = LoadTSC(tscPath, encrypted, defaultKey, encoding);

            for (var index = text.IndexOf('<', 0); index != -1; index = text.IndexOf('<', index + 1))
            {
                var eve = text.Substring(text.LastIndexOf('#', index), 5);
                var cmd = text.Substring(index, 4);
                int val;
                switch (cmd)
                {
                    case "<FL+":
                    case "<FL-":
                    case "<FLJ":
                        val = FlagConverter.FlagToRealValue(text.Substring(index + 4, 4), 4);
                        flagList.Add(new FlagListEntry(val, FlagChangeSources.TSC, filename,
                            cmd == "<FLJ" ? FlagChangeTypes.Read : FlagChangeTypes.Write)
                        {
                            TSCCommand = cmd,
                            TSCEvent = eve,
                        });
                        break;
                    case "<DNP" when entities != null:
                    case "<DNA" when entities != null:
                        val = FlagConverter.FlagToRealValue(text.Substring(index + 4, 4), 4);
                        for (int i = 0; i < entities.Count; i++)
                        {
                            if ((cmd == "<DNP" ? entities[i].Event : entities[i].Type) == val)
                            {
                                flagList.Add(new FlagListEntry(entities[i].Flag, FlagChangeSources.Both, filename, FlagChangeTypes.Write)
                                {
                                    TSCCommand = cmd,
                                    TSCEvent = eve,

                                    EntityIndex = i,
                                    EntityType = entities[i].Type,
                                    EntityName = GetEntityName(entities[i], entityInfos),
                                    EntityBits = GetRealEntityFlags(entities[i], npcTable),
                                });
                            }
                        }
                        break;
                }
            }
        }

        //Add flags from a PXE file
        public static List<Entity> AddPXE<F,N,E>(string pxePath, F flagList, EntityFlags readWhitelist, EntityFlags writeWhitelist, EntityFlags writeBlacklist, N npcTable, E entityInfo)
            where F : IList<FlagListEntry> where N : IList<NPCTableEntry> where E : IDictionary<int, EntityInfo>
        {
            string filename = Path.GetFileName(pxePath);
            var pxe = PXE.Read(pxePath);
            for (var i = 0; i < pxe.Count; i++)
            {
                var actualBits = GetRealEntityFlags(pxe[i], npcTable);

                FlagChangeTypes changeType = 0;
                //every entity sets its flag when killed by the player,
                //but only entities with certain bits can actually be killed
                //in addition, certain bits will prevent the entity from setting its flag on death
                //thus, the whitelist should have things like "shootable"
                //while the blacklist has things like "run event on death"
                if((actualBits & writeWhitelist) != 0 && (actualBits & writeBlacklist) == 0)
                {
                    changeType |= FlagChangeTypes.Write;
                }
                //only certain flags make the flag get read
                if ((actualBits & readWhitelist) != 0)
                {
                    changeType |= FlagChangeTypes.Read;
                }
                //if we've determined that this entity actually uses its flag add it!
                if(changeType != FlagChangeTypes.None)
                {
                    bool entityException = entityInfo.TryGetValue(pxe[i].Type, out var inf) && !inf.SetsFlagWhenKilledByPlayer;
                    FlagEntryWarnings oversights = 0;

                    //TODO hardcoded flag 0 in a bunch of places here

                    //trying to write, but the entity has an exception in the list
                    if((changeType & FlagChangeTypes.Write) != 0 && entityException)
                    {
                        //that's an oversight if the flag isn't 0
                        if(pxe[i].Flag != 0)
                            oversights |= FlagEntryWarnings.SpecialEntityNoWriteFlag;
                        //the write will fail no matter what
                        changeType &= ~FlagChangeTypes.Write;
                    }
                    //trying to read flag 0
                    if((changeType & FlagChangeTypes.Read) != 0 && pxe[i].Flag == 0)
                    {
                        oversights |= FlagEntryWarnings.ReadFlag0;
                    }
                    //run event on killed will make
                    if((changeType & FlagChangeTypes.Read) == 0 && (actualBits & EntityFlags.RunEventWhenKilled) != 0 && pxe[i].Flag != 0)
                    {
                        oversights |= FlagEntryWarnings.RunEventNoWriteFlag;
                    }

                    EntityFlags filterBits = 0;
                    if ((changeType & FlagChangeTypes.Read) != 0)
                        filterBits |= readWhitelist;
                    if ((changeType & FlagChangeTypes.Write) != 0)
                        filterBits |= writeWhitelist;

                    flagList.Add(new FlagListEntry(pxe[i].Flag, FlagChangeSources.Entity, filename, changeType)
                    {
                        EntityIndex = i,
                        EntityType = pxe[i].Type,
                        EntityName = GetEntityName(pxe[i], entityInfo),
                        EntityBits = actualBits & filterBits,
                        EntityOversightWarning = oversights,
                    });
                }
            }
            return pxe;
        }


        public static List<FlagListEntry> GenerateFlagList(Mod mod)
        {
            var flagList = new List<FlagListEntry>();

            //global tsc files
            foreach (var tsc in mod.FolderPaths.EnumerateFiles(SearchLocations.Data, Extension.Script))
            {
                if (tsc.Contains("Credit"))
                    AddCreditsTSC(tsc, flagList, mod.TSCEncrypted, mod.DefaultKey, mod.TSCEncoding);
                else
                    AddTSC(tsc, flagList, mod.TSCEncrypted, mod.DefaultKey, mod.TSCEncoding);
            }

            //stage table
            foreach (var entry in mod.StageTable)
            {
                List<Entity> ents = null;
                if(mod.FolderPaths.TryGetFile(SearchLocations.Stage, entry.Filename, Extension.EntityData, out string pxePath))
                    ents = AddPXE(pxePath, flagList, EntityFlags.AppearWhenFlagSet | EntityFlags.HideWhenFlagSet, EntityFlags.Shootable, EntityFlags.RunEventWhenKilled | EntityFlags.Invulnerable, mod.NPCTable, mod.EntityInfos);

                if(mod.FolderPaths.TryGetFile(SearchLocations.Stage, entry.Filename, Extension.Script, out string tscPath))
                    AddTSC(tscPath, flagList, mod.TSCEncrypted, mod.DefaultKey, mod.TSCEncoding, ents, mod.NPCTable, mod.EntityInfos);
            }
            return flagList;
        }

        public static void WriteFlagListToTable<T>(T flagList, string path, char deliminator = '\t') where T : IEnumerable<FlagListEntry>
        {
            string delimString = deliminator.ToString();
            using(var sw = new StreamWriter(path))
            {
                //header
                sw.WriteLine(string.Join(delimString, "Flag", "Source", "Filename", "Type", "TSC Event", "TSC Command", "Entity Index", "Entity Type", "Entity Name", "Entity Bits", "Entity Oversight Warning"));

                //body
                foreach(var item in flagList)
                    sw.WriteLine(string.Join(delimString,
                        //Base
                        item.Flag, item.Source, item.Filename, item.Type != 0 ? item.Type.ToString() : "",
                        //TSC
                        item.TSCEvent,
                        item.TSCCommand,
                        //Entity
                        item.EntityIndex >= 0 ? item.EntityIndex.ToString() : "",
                        item.EntityType >= 0 ? item.EntityType.ToString() : "",
                        item.EntityName,
                        item.EntityBits != EntityFlags.None ? item.EntityBits.ToString() : "",
                        item.EntityOversightWarning != 0 ? item.EntityOversightWarning.ToString() : ""));
            }
        }

        public static void WriteFlagListToText<T>(T flagList, string path, bool showWriteOnly = false) where T : IList<FlagListEntry>
        {
            HashSet<int> flagRead = new HashSet<int>();
            SortedDictionary<int, List<FlagListEntry>> tree = new SortedDictionary<int, List<FlagListEntry>>();

            //build the tree
            foreach (var item in flagList)
            {
                if (!tree.ContainsKey(item.Flag))
                    tree.Add(item.Flag, new List<FlagListEntry>());

                tree[item.Flag].Add(item);

                //add this item's flag to the list of flags that get read
                if ((item.EntityOversightWarning != 0 || item.Type == FlagChangeTypes.Read) && !flagRead.Contains(item.Flag))
                    flagRead.Add(item.Flag);
            }

            List<Tuple<string,int>> warnings = new List<Tuple<string,int>>();
            int lineNum = 1;
            foreach (var flag in tree)
            {
                lineNum++;
                foreach (var entry in flag.Value)
                {
                    lineNum++;
                    if(entry.EntityOversightWarning != 0)
                    {                        
                        if((entry.EntityOversightWarning & FlagEntryWarnings.SpecialEntityNoWriteFlag) != 0)
                        {
                            //trying to write a flag on an entity that can't
                            warnings.Add(new Tuple<string, int>($"Entity {entry.EntityIndex} on {entry.Filename} is trying to write a flag," +
                                $" but its entity type ({entry.EntityType} - {entry.EntityName}) won't automatically set its flag on death!", lineNum));
                        }
                        if ((entry.EntityOversightWarning & FlagEntryWarnings.ReadFlag0) != 0)
                        {
                            //trying to read flag 0
                            warnings.Add(new Tuple<string, int>($"Entity {entry.EntityIndex} on {entry.Filename} is trying to read flag 0!", lineNum));
                        }
                        if((entry.EntityOversightWarning & FlagEntryWarnings.RunEventNoWriteFlag) != 0)
                        {
                            warnings.Add(new Tuple<string, int>($"Entity {entry.EntityIndex} on {entry.Filename} has the bit \"{EntityFlags.RunEventWhenKilled}\" set," +
                                $" which will prevent it from writing its flag; moreover, the entity is not reading its flag ({entry.Flag})!", lineNum));
                        }
                        //TODO check for OOB flag write on an entity
                    }
                }
            }            
            if(!showWriteOnly)
                //remove anything that isn't being read
                foreach (var key in tree.Keys.ToArray())
                    if(!flagRead.Contains(key))
                        tree.Remove(key);

            //save the file
            using (var sw = new StreamWriter(path))
            {
                if(warnings.Count > 0)
                {
                    sw.WriteLine("Warnings");
                    foreach(var warning in warnings)
                    {
                        sw.WriteLine($"\t{warning.Item1} - Line {warning.Item2 + warnings.Count}");
                    }
                }                
                foreach (var flag in tree)
                {
                    sw.WriteLine("Flag " + flag.Key);
                    foreach (var item in flag.Value)
                    {
                        sw.Write("\t");
                        //add each entry
                        switch (item.Source)
                        {
                            case FlagChangeSources.Both:
                                sw.WriteLine($"{item.Type} - {item.TSCCommand} {item.Filename} event {item.TSCEvent} deletes entity {item.EntityIndex} (Type {item.EntityType} - {item.EntityName})");
                                break;
                            case FlagChangeSources.TSC:
                                sw.WriteLine($"{item.Type} - {item.TSCCommand} {item.Filename} event {item.TSCEvent}");
                                break;
                            case FlagChangeSources.Entity:
                                sw.WriteLine($"{item.Type} - {item.Filename} entity {item.EntityIndex} (Type {item.EntityType} - {item.EntityName}) {item.EntityBits}");
                                break;
                        }                        
                    }
                }
            }
        }
    }
}

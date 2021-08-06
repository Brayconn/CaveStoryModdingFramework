using CaveStoryModdingFramework.Entities;
using CaveStoryModdingFramework.TSC;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CaveStoryModdingFramework
{
    public static class Analysis
    {
        [Flags]
        public enum FlagChangeSources
        {
            TSC = 1,
            Entity,
            Both = TSC | Entity
        }
        [Flags]
        public enum FlagChangeTypes
        {
            Read = 1,
            Write,
            //Both = Read | Write //removed this because I want ToString to actually return "Read | Write"
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

        static string GetEntityName(Entity ent, IDictionary<int, EntityInfo> infos)
        {
            if (infos.TryGetValue(ent.Type, out var inf))
                return inf.Name;
            else
                return "";
        }

        //add flags from a TSC file
        static void AddTSC<T>(Mod mod, string tscPath, T flagList, bool credits = false, IList<Entity> entities = null) where T : IList<FlagListEntry>
        {
            string filename = Path.GetFileName(tscPath);
            byte[] input = File.ReadAllBytes(tscPath);
            if (mod.TSCEncrypted)
                Encryptor.DecryptInPlace(input, mod.DefaultKey);
            var text = mod.TSCEncoding.GetString(input);

            if (!credits)
            {
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
                                        EntityName = GetEntityName(entities[i], mod.EntityInfos),
                                        EntityBits = GetRealEntityFlags(entities[i], mod.NPCTable),
                                    });
                                }
                            }
                            break;
                    }
                }
            }
            else
            {
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
                            flagList.Add(new FlagListEntry(FlagConverter.FlagToRealValue(text.Substring(index + 1, 4), 4),FlagChangeSources.TSC, filename, FlagChangeTypes.Read)
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
        }

        //Add flags from a PXE file
        static List<Entity> AddPXE<T>(Mod mod, string pxePath, T flagList, EntityFlags readWhitelist, EntityFlags writeBlacklist) where T : IList<FlagListEntry>
        {
            string filename = Path.GetFileName(pxePath);
            var pxe = PXE.Read(pxePath);
            for (var i = 0; i < pxe.Count; i++)
            {
                var actualBits = GetRealEntityFlags(pxe[i], mod.NPCTable);

                //by default, every entity sets its flag on death...
                FlagChangeTypes changeType = FlagChangeTypes.Write;
                //...but some (such as Gaudi) are special and don't...
                if ((mod.EntityInfos.TryGetValue(pxe[i].Type, out var inf) && !inf.SetsFlagWhenKilledByPlayer)
                    //...and certain bits (such as RunEventOnDeath) make it so the flag won't be set
                    || (actualBits & writeBlacklist) != 0)
                {
                    changeType &= ~FlagChangeTypes.Write;
                }
                //plus, only certain flags make the flag get read
                if ((actualBits & readWhitelist) != 0)
                {
                    changeType |= FlagChangeTypes.Read;
                }

                flagList.Add(new FlagListEntry(pxe[i].Flag, FlagChangeSources.Entity, filename, changeType)
                {
                    EntityIndex = i,
                    EntityType = pxe[i].Type,
                    EntityName = GetEntityName(pxe[i], mod.EntityInfos),
                    EntityBits = actualBits,
                });
            }
            return pxe;
        }


        public static List<FlagListEntry> GenerateFlagList(Mod mod)
        {
            var flagList = new List<FlagListEntry>();

            //global tsc files
            foreach (var tsc in mod.FolderPaths.EnumerateFiles(SearchLocations.Data, Extension.Script))
                AddTSC(mod, tsc, flagList, tsc.Contains("Credit"));

            //stage table
            foreach (var entry in mod.StageTable)
            {
                List<Entity> ents = null;
                if(mod.FolderPaths.TryGetFile(SearchLocations.Stage, entry.Filename, Extension.EntityData, out string pxePath))
                    ents = AddPXE(mod, pxePath, flagList, EntityFlags.AppearWhenFlagSet | EntityFlags.HideWhenFlagSet, EntityFlags.RunEventWhenKilled | EntityFlags.Invulnerable);

                if(mod.FolderPaths.TryGetFile(SearchLocations.Stage, entry.Filename, Extension.Script, out string tscPath))
                    AddTSC(mod, tscPath, flagList, false, ents);
            }
            return flagList;
        }

        public static void WriteFlagListToTable<T>(T flagList, string path, char deliminator = '\t') where T : IEnumerable<FlagListEntry>
        {
            string delimString = deliminator.ToString();
            using(var sw = new StreamWriter(path))
            {
                //header
                sw.WriteLine(string.Join(delimString, "Flag", "Source", "Filename", "Type", "TSC Event", "TSC Command", "Entity Index", "Entity Type", "Entity Name", "Entity Bits"));

                //body
                foreach(var item in flagList)
                    sw.WriteLine(string.Join(delimString, item.Flag, item.Source, item.Filename, item.Type, item.TSCEvent, item.TSCCommand, item.EntityIndex, item.EntityType, item.EntityName, item.EntityBits));
            }
        }

        public static void WriteFlagListToText<T>(T flagList, string path, bool showWriteOnly = false) where T : IEnumerable<FlagListEntry>
        {
            HashSet<int> flagRead = new HashSet<int>();
            SortedDictionary<int, List<string>> tree = new SortedDictionary<int, List<string>>();
            foreach(var item in flagList)
            {
                if (!tree.ContainsKey(item.Flag))
                    tree.Add(item.Flag, new List<string>());
                switch(item.Source)
                {
                    case FlagChangeSources.Both:
                        tree[item.Flag].Add($"{item.Type} - {item.TSCCommand} {item.Filename} event {item.TSCEvent} deletes entity {item.EntityIndex} (Type {item.EntityType} - {item.EntityName})");
                        break;
                    case FlagChangeSources.TSC:
                        tree[item.Flag].Add($"{item.Type} - {item.TSCCommand} {item.Filename} event {item.TSCEvent}");
                        break;
                    case FlagChangeSources.Entity:
                        tree[item.Flag].Add($"{item.Type} - {item.EntityBits} {item.Filename} entity {item.EntityIndex} (Type {item.EntityType} - {item.EntityName})");
                        break;
                }
                if (item.Type == FlagChangeTypes.Read && !flagRead.Contains(item.Flag))
                    flagRead.Add(item.Flag);
            }
            if(!showWriteOnly)
                foreach (var key in tree.Keys.ToArray())
                    if(!flagRead.Contains(key))
                        tree.Remove(key);
            //save the file
            using (var sw = new StreamWriter(path))
            {
                foreach (var item in tree)
                {
                    sw.WriteLine("Flag " + item.Key);
                    foreach (var str in item.Value)
                    {
                        sw.WriteLine("\t" + str);
                    }
                }
            }
        }
    }
}

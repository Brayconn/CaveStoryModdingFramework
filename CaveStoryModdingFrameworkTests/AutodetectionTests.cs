using CaveStoryModdingFramework;
using CaveStoryModdingFramework.AutoDetection;
using CaveStoryModdingFramework.Stages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace CaveStoryModdingFrameworkTests
{
    public class AutodetectionTests
    {
        private readonly ITestOutputHelper output;
        public AutodetectionTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        static MethodInfo Prepare = typeof(XMLDirectorySearcherAttribute).GetMethod(nameof(Prepare), BindingFlags.NonPublic | BindingFlags.Instance);
        static FieldInfo SearchDir = typeof(XMLDirectorySearcherAttribute).GetField(nameof(SearchDir), BindingFlags.NonPublic | BindingFlags.Instance);
        [Fact]
        public void XMLDirectorySearcherWorks()
        {
            var x = new XMLDirectorySearcherAttribute("DATA", nameof(ProjectFile));
            var m = typeof(AutodetectionTests).GetMethod(nameof(XMLDirectorySearcherWorks));
            Prepare.Invoke(x, new object[] { m });
            var sd = (string)SearchDir.GetValue(x);
            int count = 0;
            foreach (var test in CaveStoryTestData.EnumerateValidTests(sd, false))
                count++;
            output.WriteLine($"Found {count} tests in {sd}");
            Assert.True(count > 0);
        }

        [Theory]
        [XMLDirectorySearcher("DATA", nameof(ProjectFile.EXEPath), nameof(ProjectFile.StageTables))]
        public void FindInternalTables(string exe, Dictionary<string, StageTableLocation> expected)
        {
            if (!string.IsNullOrEmpty(exe))
            {
                var tables = AutoDetector.FindInternalStageTables(exe);
                output.WriteLine($"Found {tables.Count} table(s)");
                foreach (var table in tables)
                    Assert.Contains(table, expected.Values);
            }
        }

        [Theory]
        [XMLDirectorySearcher("DATA", nameof(ProjectFile), nameof(ProjectFile.Layouts))]
        public void FindExternalTables(ProjectFile project, List<AssetLayout> layouts)
        {
            //TODO items can be cached to make this more efficient
            foreach (var layout in layouts)
            {
                var externalTables = new List<ExternalTables>(layout.DataPaths.Count);
                foreach (var data in layout.DataPaths)
                    if (AutoDetector.TryFindExternalTables(data, out var ext))
                        externalTables.Add(ext);
                output.WriteLine($"Found {externalTables.Count} bundles of tables");

                var expectedStageTables = project.GetTables(project.StageTables, layout.StageTables);
                var expectedNpcTables = project.GetTables(project.NPCTables, layout.NpcTables);
                var expectedBulletTables = project.GetTables(project.BulletTables, layout.BulletTables);
                var expectedArmsLevelTables = project.GetTables(project.ArmsLevelTables, layout.ArmsLevelTables);

                foreach(var table in externalTables)
                {
                    static void CheckTableList<T>(List<T> actual, List<T> expected) where T : DataLocation
                    {
                        foreach(var a in actual)
                            Assert.Contains(a, expected);
                    }
                    CheckTableList(table.StageTables, expectedStageTables);
                    CheckTableList(table.NpcTables, expectedNpcTables);
                    CheckTableList(table.BulletTables, expectedBulletTables);
                    CheckTableList(table.ArmsLevelTables, expectedArmsLevelTables);
                }
            }
        }

        [Theory]
        [XMLDirectorySearcher("DATA", nameof(ProjectFile), nameof(ProjectFile.Layouts), nameof(ProjectFile.BaseDataPath), nameof(ProjectFile.ImageExtension))]
        public void InitDataAndImageExtension(ProjectFile pf, List<AssetLayout> layouts, string data, string extension)
        {
            var stages = pf.ReadManyStageTables(layouts[0]);
            
            var predicted = AutoDetector.FindDataFolderAndImageExtension(data, stages, out var list);
            output.WriteLine($"Predicted {predicted}");
            output.WriteLine($"Found {FormatKVPList(list)}");

            var maxes = AutoDetector.GetMaxes(list);            
            output.WriteLine($"Maxes: {FormatListOfStrings(maxes)} (expected {extension})");

            Assert.Single(maxes);
            //don't really care if the extension includes the dot or not
            Assert.EndsWith(extension, maxes[0]);
        }

        [Theory]
        [XMLDirectorySearcher("DATA", nameof(ProjectFile), nameof(ProjectFile.Layouts), nameof(ProjectFile.ImageExtension))]
        public void CanInitNpcFolder(ProjectFile project, List<AssetLayout> layouts, string imgExt)
        {
            var layout = layouts[0];
            var data = layout.DataPaths[layout.DataPaths.Count - 1];

            var tables = project.ReadManyStageTables(layout);
            var spritesheets = AutoDetector.GetSpritesheets(tables);

            float retV = 0;
            List<KeyValuePair<string, float>> finalPrefixes;
            bool test(string path)
            {
                if ((retV = AutoDetector.FindNpcPrefix(path, spritesheets, imgExt, out var prefixes)) > AutoDetector.NPC_INIT_THRESHOLD)
                {
                    var npcPrefix = AutoDetector.GetMaxes(prefixes);
                    if (npcPrefix.Count == 1)
                    {
                        finalPrefixes = new List<KeyValuePair<string, float>>();
                        return true;
                    }
                }
                output.WriteLine($"Failed on {path}");
                output.WriteLine($"Extensions: {FormatKVPList(prefixes)}");
                return false;
            }

            //TODO don't use the find with shortcut thing
            foreach(var dir in Directory.EnumerateDirectories(data))
            {
                if(test(dir))
                {
                    output.WriteLine($"Found {retV * 100}% of spritesheets in {dir}");

                    Assert.Equal(layout.NpcPaths[layout.NpcPaths.Count - 1], dir);
                }
            }
        }

        [Theory]
        [XMLDirectorySearcher("DATA", nameof(ProjectFile), nameof(ProjectFile.Layouts), nameof(ProjectFile.ImageExtension),
            nameof(ProjectFile.MapExtension), nameof(ProjectFile.EntityExtension), nameof(ProjectFile.ScriptExtension), nameof(ProjectFile.ScriptsEncrypted),
            nameof(ProjectFile.TilesetPrefix), nameof(ProjectFile.AttributeExtension))]
        public void CanInitStageFolder(ProjectFile project, List<AssetLayout> layouts, string imgExt, string map, string entity, string tsc, bool encrypted, string tilesetPrefix, string attributeExtension)
        {
            var layout = layouts[0];
            var data = layout.DataPaths[layout.DataPaths.Count - 1];

            var tables = project.ReadManyStageTables(layout);
            var filenames = AutoDetector.GetFilenames(tables);
            var tilesets = AutoDetector.GetTilesets(tables);

            foreach (var dir in Directory.EnumerateDirectories(data))
            {
                //haha
                var dn = Path.GetFileName(dir);
                output.WriteLine($"Working on {dn}");

                StageFolderSearchResults stageExts = null;
                var stage = false;
                try
                {
                    stage = AutoDetector.TryFindStageExtensions(dir, filenames, out stageExts);
                    output.WriteLine($"Stage = {stage}");
                    if (stageExts == null)
                        output.WriteLine($"Didn't find enough extensions");
                }
                catch (Exception e)
                {
                    output.WriteLine($"Threw {e.GetType()}: " + e.Message);
                }

                AttributeInfo attribInf = null;
                var attrib = false;
                if(stage)
                    attrib = AutoDetector.TryFindAttributeExtension(dir, tilesets, imgExt, stageExts, out attribInf);
                output.WriteLine($"Attributes = {attrib}");

                if(stage && attrib)
                {
                    output.WriteLine($"Found {stageExts.FoundMaps}, expected {map}");
                    output.WriteLine($"Found {stageExts.FoundEntities}, expected {entity}");
                    output.WriteLine($"Found {stageExts.FoundScripts}, expected {tsc}");
                    output.WriteLine($"Found {stageExts.ScriptsEncrypted}, expected {encrypted}");

                    output.WriteLine($"Found {attribInf.TilesetPrefix}, expected {tilesetPrefix}");
                    output.WriteLine($"Found {attribInf.AttributeExtension}, expected {attributeExtension}");

                    Assert.EndsWith(map, stageExts.FoundMaps);
                    Assert.EndsWith(entity, stageExts.FoundEntities);
                    Assert.EndsWith(tsc, stageExts.FoundScripts);
                    Assert.Equal(encrypted, stageExts.ScriptsEncrypted);

                    Assert.Equal(tilesetPrefix, attribInf.TilesetPrefix);
                    Assert.EndsWith(attributeExtension, attribInf.AttributeExtension);
                }
            }
        }

        string FormatListOfFilenames(List<string> values)
        {
            if (values.Count == 0)
                return "";
            else
                return string.Join("\n", values.Select(x => Path.GetRelativePath("DATA", x)));
        }
        string FormatListOfStrings(List<string> values)
        {
            if (values.Count == 0)
                return "";
            else
                return string.Join("\n", values);
        }
        string FormatListOfStrings(List<TableLoadInfo> values)
        {
            if (values.Count == 0)
                return "";
            else
                return string.Join("\n", values.Select(x => $"{x.Key} - {x.Write}" ));
        }
        string FormatKVPList(List<KeyValuePair<string, float>> list)
        {
            return FormatListOfStrings(list.Select(x => $"({x.Key}: {x.Value})").ToList());
        }
    }
}

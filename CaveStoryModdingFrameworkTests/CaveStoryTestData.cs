using CaveStoryModdingFramework;
using CaveStoryModdingFramework.AutoDetection;
using CaveStoryModdingFramework.Stages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Xml.Serialization;
using Xunit.Sdk;

namespace CaveStoryModdingFrameworkTests
{
    public static class CaveStoryVersionDownloader
    {
        public static void DownloadAndUnpack(string url, string output)
        {
            string tempFile = "";
            try
            {
                tempFile = Path.GetTempFileName();
                using (var client = new WebClient())
                {
                    client.DownloadFile(url, tempFile);
                }
                Process cmd = new Process();
                cmd.StartInfo.FileName = @"C:/Program Files/7-zip/7z.exe"; //TODO 7-zip isn't always here?
                cmd.StartInfo.Arguments = $"x \"{tempFile}\" -o\"{output}\" -y"; //Spacing on these args is important
                cmd.StartInfo.RedirectStandardInput = true;
                cmd.StartInfo.RedirectStandardOutput = true;
                cmd.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                cmd.StartInfo.UseShellExecute = false;
                cmd.StartInfo.CreateNoWindow = true;
                cmd.Start();
                cmd.WaitForExit();
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
    }
    public enum DownloadMethods
    {
        Manual,
        DirectArchive,
    }
    public class CaveStoryTestData
    {
        //Unique name for this test
        public string Name { get; set; }
        
        //URL where the file can be downloaded (where applicable)
        public string URL { get; set; }

        //How to download this item
        public DownloadMethods DownloadMethod { get; set; }

        public static IEnumerable<CaveStoryTestData> EnumerateTests(string path, bool download = false)
        {
            var cstd = new XmlSerializer(typeof(CaveStoryTestData));
            foreach (var file in Directory.EnumerateFiles(path, "*.xml"))
            {
                //Try to unpack a potential test
                CaveStoryTestData test;
                using (var f = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    try
                    {
                        test = (CaveStoryTestData)cstd.Deserialize(f);
                    }
                    catch
                    {
                        continue;
                    }
                }

                var expectedLocation = Path.Combine(path, test.Name);
                if (!Directory.Exists(expectedLocation))
                {
                    Directory.CreateDirectory(expectedLocation);
                    if (download)
                    {
                        switch (test.DownloadMethod)
                        {
                            //Direct archive links can be downloaded/unpacked automatically
                            case DownloadMethods.DirectArchive:
                                CaveStoryVersionDownloader.DownloadAndUnpack(test.URL, expectedLocation);
                                break;
                            //Can't do anything about manual, try the next test
                            case DownloadMethods.Manual:
                                continue;
                        }
                    }
                    else continue;
                }

                yield return test;
            }
        }
        public static IEnumerable<Tuple<CaveStoryTestData, ProjectFile>> EnumerateValidTests(string path, bool download = false)
        {
            foreach(var test in EnumerateTests(path, download))
            {
                ProjectFile expected;
                var expectedPath = Path.Combine(path, Path.ChangeExtension(test.Name, ProjectFile.Extension));
                if (!File.Exists(expectedPath))
                    continue;
                using (var f = new FileStream(expectedPath, FileMode.Open, FileAccess.Read))
                {
                    try
                    {
                        expected = ProjectFile.Load(expectedPath);
                    }
                    catch
                    {
                        continue;
                    }
                }
                yield return new Tuple<CaveStoryTestData, ProjectFile>(test, expected);
            }
        }
    }
    public class XMLDirectorySearcherAttribute : DataAttribute
    {
        protected string SearchDir;
        protected readonly string[] Input;
        public XMLDirectorySearcherAttribute(string dir, params string[] input)
        {
            SearchDir = dir;
            Input = input;
        }
        protected void Prepare(MethodInfo testMethod)
        {
            if (testMethod == null)
                throw new ArgumentNullException(nameof(testMethod));

            if (!Path.IsPathRooted(SearchDir))
                SearchDir = Path.Combine(Directory.GetCurrentDirectory(), SearchDir);

            if (!Directory.Exists(SearchDir))
                throw new ArgumentException(SearchDir + " could not be found!", nameof(SearchDir));
        }
        protected object[] AssembleInput(CaveStoryTestData test, ProjectFile proj)
        {
            var o = new object[Input.Length];
            for (int i = 0; i < Input.Length; i++)
            {
                switch (Input[i])
                {
                    case nameof(ProjectFile):
                        o[i] = proj;
                        break;
                    case nameof(CaveStoryTestData):
                        o[i] = test;
                        break;
                    case nameof(CaveStoryTestData.Name):
                        o[i] = Path.Combine(SearchDir, test.Name);
                        break;
                    default:
                        var prop = typeof(ProjectFile).GetProperty(Input[i]);
                        if (prop == null)
                            throw new ArgumentException("Can't find property " + Input[i]);
                        var val = prop.GetValue(proj);
                        if (val == null)
                            throw new ArgumentNullException("Surely you didn't want a null value from " + Input[i]);
                        o[i] = val;
                        break;
                }
            }
            return o;
        }
        public override IEnumerable<object[]> GetData(MethodInfo testMethod)
        {
            Prepare(testMethod);

            foreach (var test in CaveStoryTestData.EnumerateValidTests(SearchDir, true))
            {
                yield return AssembleInput(test.Item1, test.Item2);
            }
        }
    }

    public class XMLDirectoryTESTGEN : XMLDirectorySearcherAttribute
    {
        readonly bool Clobber;
        public XMLDirectoryTESTGEN(string dir, bool clobber, params string[] input) : base(dir, input)
        {
            Clobber = clobber;
        }

        public override IEnumerable<object[]> GetData(MethodInfo testMethod)
        {
            Prepare(testMethod);

            foreach(var test in CaveStoryTestData.EnumerateTests(SearchDir, true))
            {
                //Basic check for a CS copy existing
                var csPath = Path.Combine(SearchDir, test.Name);
                if (!Directory.EnumerateFileSystemEntries(csPath).Any())
                    continue;

                //Only overwrite with clobber
                var expected = Path.Combine(SearchDir, Path.ChangeExtension(test.Name, ProjectFile.Extension));
                if (!File.Exists(expected) || Clobber)
                {
                    const string exeFilter = "*.exe";
                    //BFS to find a folder with an exe
                    bool foundEXE = false;
                    AutoDetector.BreadthFirstSearch(new[] { csPath }, (x) =>
                    {
                        foreach(var dummy in Directory.EnumerateFiles(x, exeFilter))
                        {
                            csPath = x;
                            foundEXE = true;
                            return true;
                        }
                        return false;
                    });
                    
                    var exePath = "";
                    //if we found one, use it!
                    if (foundEXE)
                    {
                        //Need to do this to distinguish from DoConfig
                        //Different languages have different names, so file size it is...
                        var biggestExe = new Tuple<string, long>("", 0);
                        foreach (var exe in Directory.EnumerateFiles(csPath, exeFilter))
                        {
                            var inf = new FileInfo(exe);
                            if (inf.Length > biggestExe.Item2)
                                biggestExe = new Tuple<string, long>(inf.Name, inf.Length);
                        }
                        exePath = Path.Combine(csPath, biggestExe.Item1);
                    }
                    
                    var baseDataPath = Path.Combine(csPath, "data");
                    if(!Directory.Exists(baseDataPath))
                        baseDataPath = Directory.EnumerateDirectories(csPath).First(); //hacky way to get around CS+ console ports not having a "data" folder

                    var pf = new ProjectFile()
                    {
                        BaseDataPath = baseDataPath,
                        EXEPath = exePath
                    };
                    //need to start the layout here, since we're about to (potentially) add a bunch of tables to it
                    var layout = new AssetLayout();

                    //Add internal stage tables
                    if (foundEXE)
                    {
                        int i = 0;
                        foreach (var result in AutoDetector.FindInternalStageTables(pf.EXEPath))
                        {
                            var key = "Internal " + (++i);
                            pf.StageTables.Add(key, result);
                            layout.StageTables.Add(new TableLoadInfo(key, true));
                        }

                        //TODO based on what internal data was found we probably need to add a bunch of other locations
                        //these need to be hardcoded
                        //stuff like if you load freeware it knows where the other tables are
                        //or dsiware, etc.
                    }

                    //external tables in general
                    var extTables = AutoDetector.FindExternalTables(baseDataPath);
                    if(extTables != null)
                        pf.Add(extTables, layout);

                    //still didn't find a single stage table... epic fail moment...
                    if (pf.StageTables.Count == 0)
                        throw new FileLoadException("Couldn't find any stage tables?!");

                    //load all stage tables into memory
                    var loadedTables = new List<List<StageTableEntry>>(pf.StageTables.Count);
                    foreach (var entry in pf.StageTables.Values)
                        loadedTables.Add(entry.Read());

                    //find the data folder
                    var firstDataPath = AutoDetector.FindDataFolderAndImageExtension(baseDataPath, loadedTables, out var foundExts);
                    
                    if (firstDataPath == null)
                        throw new DirectoryNotFoundException("Couldn't find data folder!");
                    
                    layout.DataPaths.Add(firstDataPath);

                    //find the image extension
                    {
                        var possibleExtensions = AutoDetector.GetMaxes(foundExts);
                        if (possibleExtensions.Count != 1)
                            throw new FileNotFoundException("Couldn't determine image extension");
                        pf.ImageExtension = possibleExtensions[0];
                    }

                    //Find the npc/stage folders
                    {
                        var spritesheets = AutoDetector.GetSpritesheets(loadedTables);
                        var filenames = AutoDetector.GetFilenames(loadedTables);
                        var tilesets = AutoDetector.GetTilesets(loadedTables);

                        var foundthingies = AutoDetector.FindNpcAndStageFolders(firstDataPath,
                            //note that the project file gets modified by these functions
                            x => AutoDetector.TryInitFromNpcFolder(x, spritesheets, pf.ImageExtension, pf),
                            x => AutoDetector.TryInitFromStageFolder(x, filenames, tilesets, pf.ImageExtension, pf));

                        if (foundthingies.Item1 == null)
                            throw new DirectoryNotFoundException("Unable to find a suitible npc folder! " + firstDataPath);
                        if (foundthingies.Item2 == null)
                            throw new DirectoryNotFoundException("Unable to find a suitible stage folder! " + firstDataPath);

                        layout.NpcPaths.Add(foundthingies.Item1);
                        layout.StagePaths.Add(foundthingies.Item2);
                    }

                    //first layout done!
                    pf.Layouts.Add(layout);

                    //Add other mods?
                    if(firstDataPath != baseDataPath)
                    {
                        bool SubAdd(string curr)
                        {
                            //the tables this layout will use
                            var localTables = new List<List<StageTableEntry>>();

                            var extTablesFound = false;
                            //if there were external tables, we're already done
                            if (extTablesFound = AutoDetector.TryFindExternalTables(curr, out var externalTables)
                                || AutoDetector.CountHardcodedDataFiles(curr, pf.ImageExtension) >= 2
                                //otherwise, we should check if backgrounds exist
                                || AutoDetector.ContainsBackgrounds(curr, localTables) > 0.5)
                            {
                                //we are now committed to making a layout, so make it
                                var localLayout = new AssetLayout(layout, true);
                                localLayout.DataPaths.Add(curr);
                                
                                //also time to add the previously found tables...
                                foreach(var table in loadedTables)
                                    localTables.Add(table);

                                //...the external tables if any were found...
                                if (extTablesFound)
                                {
                                    foreach (var tab in externalTables.StageTables)
                                        localTables.Add(tab.Read());
                                    pf.Add(externalTables, localLayout);
                                }

                                //...and merge them!
                                var merged = AutoDetector.MergeStageTables(localTables);

                                var localSpritesheets = AutoDetector.GetSpritesheets(merged);
                                var localFilenames = AutoDetector.GetFilenames(merged);
                                var localTilesets = AutoDetector.GetTilesets(merged);

                                var loc = AutoDetector.FindNpcAndStageFolders(curr,
                                    x => AutoDetector.ContainsNpcFiles(localSpritesheets, x, pf.ImageExtension, pf.SpritesheetPrefix) >= 0.5,
                                    x => AutoDetector.ContainsStageFiles(merged,x, pf.TilesetPrefix, pf.AttributeExtension,
                                    pf.ImageExtension, pf.MapExtension, pf.EntityExtension, pf.ScriptExtension) >= 0.5);
                                
                                if (loc.Item1 != null)
                                    localLayout.NpcPaths.Add(loc.Item1);
                                if (loc.Item2 != null)
                                    localLayout.StagePaths.Add(loc.Item2);

                                //we should have a valid layout by this point????
                                pf.Layouts.Add(localLayout);
                                return true;
                            }
                            return false;
                        }

                        AutoDetector.BreadthFirstSearch(
                            Directory.EnumerateDirectories(baseDataPath).Where(x => x != firstDataPath),
                            (x) => SubAdd(x));
                    }
                    
                    //Finishing up
                    pf.Save(expected);

                    yield return AssembleInput(test, pf);
                }
                else
                {
                    yield return AssembleInput(test, ProjectFile.Load(expected));
                }
            }
        }
    }
}

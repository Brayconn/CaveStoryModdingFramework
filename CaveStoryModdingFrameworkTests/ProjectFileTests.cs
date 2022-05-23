using CaveStoryModdingFramework;
using CaveStoryModdingFramework.Stages;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using Xunit;
using Xunit.Abstractions;

namespace CaveStoryModdingFrameworkTests
{
    class ProjectFileTestGenerator : IEnumerable<object[]>
    {
        ProjectFile make(string img = "", bool addStage = false)
        {
            var pf = new ProjectFile
            {
                BaseDataPath = @"C:\data",
                EXEPath = @"C:\Doukutsu.exe"
            };
            pf.Layouts.Add(new AssetLayout());
            if(!string.IsNullOrEmpty(img))
                pf.ImageExtension = img;
            if(addStage)
                pf.StageTables.Add("1", new StageTableLocation(pf.EXEPath, StageTablePresets.doukutsuexe));
            return pf;
        }
        public IEnumerator<object[]> GetEnumerator()
        {
            yield return new object[] { make() };
            yield return new object[] { make("bmp") };
            yield return new object[] { make(addStage:true) };
            yield return new object[] { make("bmp", addStage: true) };
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    public class ProjectFileTests
    {
        ITestOutputHelper output;

        public ProjectFileTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Theory]
        [ClassData(typeof(ProjectFileTestGenerator))]
        public void SerializeOK(ProjectFile pf)
        {
            using (var m = new MemoryStream())
            {
                var x = new XmlSerializer(typeof(ProjectFile));
                x.Serialize(m, pf);
                                
                output.WriteLine(Encoding.ASCII.GetString(m.ToArray()));

                m.Position = 0;
                var o = x.Deserialize(m);

                Assert.Equal(pf, o);
            }
        }

        [Theory]
        [ClassData(typeof (ProjectFileTestGenerator))]
        public void SaveLoadOK(ProjectFile pf)
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                pf.Save(tempFile);

                var l = ProjectFile.Load(tempFile);

                Assert.Equal(pf, l);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
    }
}

using CaveStoryModdingFramework.Compatability;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace CaveStoryModdingFrameworkTests
{
    public class TexTests
    {
        private readonly ITestOutputHelper output;
        public TexTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        public class TexFinderAttribute : XMLDirectorySearcherAttribute
        {
            string Name;
            public TexFinderAttribute(string dir, string name) : base(dir)
            {
                Name = name;
            }
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                Prepare(testMethod);
                bool foundData = false;
                //not downloading because the filter should only access one copy of the game anyways...
                foreach (var test in CaveStoryTestData.EnumerateValidTests(SearchDir, false))
                {
                    if (test.Item1.Name == Name)
                    {
                        foundData = true;
                        foreach (var f in Directory.EnumerateFiles(test.Item2.BaseDataPath, "*.tex", SearchOption.AllDirectories))
                            yield return new object[] { f };
                        break;
                    }
                }
                if (!foundData)
                    throw new Xunit.SkipException($"Didn't find data for \"{Name}\"!");
            }
        }

        void DumpDebugInfo(string file, MemoryStream fs, MemoryStream os, bool writeFiles)
        {
            output.WriteLine(string.Join(", ", fs.ToArray().Select(x => x.ToString("X2")).ToArray()));
            output.WriteLine(string.Join(", ", os.ToArray().Select(x => x.ToString("X2")).ToArray()));

            if (!writeFiles)
                return;

            fs.Position = 0;
            os.Position = 0;
            var e = new _3DSTex(fs, false, false);
            var a = new _3DSTex(os, false, false);
            var d = new Bitmap(e.Bitmap.Width, e.Bitmap.Height);
            for (int y = 0; y < e.Bitmap.Height; y++)
            {
                for (int x = 0; x < e.Bitmap.Width; x++)
                {
                    if (e.Bitmap.GetPixel(x, y) != a.Bitmap.GetPixel(x, y))
                        d.SetPixel(x, y, Color.Black);
                }
            }
            e.Bitmap.Save(file + "_expected.png", ImageFormat.Png);
            a.Bitmap.Save(file + "_actual.png", ImageFormat.Png);
            d.Save(file + "_diff.png", ImageFormat.Png);
        }

        void TestPreserve(string file, bool crop, bool flipY)
        {
            output.WriteLine($"Crop: {crop} - Flip Y: {flipY}");

            using var ms1 = new MemoryStream(File.ReadAllBytes(file));
            using var tex = new _3DSTex(ms1, crop, flipY);

            using var ms2 = new MemoryStream();
            tex.Save(ms2, flipY);

            try
            {
                Assert.Equal(ms1.Length, ms2.Length);
            }
            catch (EqualException)
            {
                output.WriteLine("Stream length was incorrect!");
                DumpDebugInfo(file, ms1, ms2, false);
                throw;
            }

            try
            {
                Assert.Equal(ms1.ToArray(), ms2.ToArray());
            }
            catch (EqualException)
            {
                output.WriteLine("Data didn't match!");
                DumpDebugInfo(file, ms1, ms2, true);
                throw;
            }
        }

        [SkippableTheory(Skip = "This test takes 2-3 minutes to run")]
        [TexFinder("DATA", "3dseshop_usa")]
        public void ReadWritePreserve(string file)
        {
            output.WriteLine(file);
            
            TestPreserve(file, false, false);

            TestPreserve(file, false, true);
            TestPreserve(file, true, false);

            TestPreserve(file, true, true);
        }
    }
}

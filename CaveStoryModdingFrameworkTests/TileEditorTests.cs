using CaveStoryModdingFramework.Editors;
using CaveStoryModdingFramework.Maps;
using CaveStoryModdingFramework.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace CaveStoryModdingFrameworkTests
{
    public class TileEditorTests
    {
        private readonly ITestOutputHelper output;
        public TileEditorTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        class FillTest
        {
            Map Input;
            byte Tile = 1;
            int[] Points;
            Map[] Outputs;
            public FillTest(Map input, params Map[] outputs) : this(input, input.Tiles.Select(x => (int)(x != 0 ? -x : x)).ToArray(), outputs)
            { }
            public FillTest(Map input, int[] points, params Map[] outputs)
            {
                Input = input;
                Points = points;
                if (outputs == null || outputs.Length <= 0)
                    throw new ArgumentException("Must provide at least one output map!", nameof(outputs));
                Outputs = outputs;
            }
            public object[] ToArray()
            {
                return new object[] { Input, Points, Outputs, Tile };
            }
        }
        public static IEnumerable<object[]> FillTests
        {
            get
            {
                //Blank 1x1
                yield return new FillTest(new Map(1, 1, 0), new Map(1, 1, 1)).ToArray();
                //Blank 2x2
                yield return new FillTest(new Map(2, 2, 0), new Map(2, 2, 1)).ToArray();
                //O shape
                yield return new FillTest(new Map(3, 3, 0)
                {
                    Tiles = new List<byte?>(){
                    0, 0, 0,
                    0, 1, 0,
                    0, 0, 0
                }
                }, new Map(3, 3, 1)).ToArray();
                //U shape
                yield return new FillTest(new Map(5, 5, 0)
                {
                    Tiles = new List<byte?>()
                {
                    0, 0, 0, 0, 0,
                    0, 1, 0, 1, 0,
                    0, 1, 0, 1, 0,
                    0, 1, 1, 1, 0,
                    0, 0, 0, 0, 0
                }
                }, new Map(5, 5, 1)).ToArray();
                //I shape
                yield return new FillTest(new Map(5, 5, 0)
                {
                    Tiles = new List<byte?>()
                {
                    0, 0, 0, 0, 0,
                    1, 1, 0, 1, 1,
                    0, 1, 0, 1, 0,
                    1, 1, 0, 1, 1,
                    0, 0, 0, 0, 0
                }
                }, new[]
                {
                    0, 0, 0, 0, 0,
                   -1,-1, 0,-1, -1,
                    1,-1, 0,-1, 2,
                   -1,-1, 0,-1, -1,
                    0, 0, 0, 0, 0
                }, new Map(5, 5, 0)
                {
                    Tiles = new List<byte?>()
                {
                    1, 1, 1, 1, 1,
                    1, 1, 1, 1, 1,
                    0, 1, 1, 1, 0,
                    1, 1, 1, 1, 1,
                    1, 1, 1, 1, 1
                }
                }, new Map(5, 5, 0)
                {
                    Tiles = new List<byte?>()
                {
                    0, 0, 0, 0, 0,
                    1, 1, 0, 1, 1,
                    1, 1, 0, 1, 0,
                    1, 1, 0, 1, 1,
                    0, 0, 0, 0, 0
                }
                }, new Map(5, 5, 0)
                {
                    Tiles = new List<byte?>()
                {
                    0, 0, 0, 0, 0,
                    1, 1, 0, 1, 1,
                    0, 1, 0, 1, 1,
                    1, 1, 0, 1, 1,
                    0, 0, 0, 0, 0
                }
                }).ToArray();
            }
        }

        [Theory]
        [MemberData(nameof(FillTests))]
        public void FillOK(Map initial, int[] expectedIndexes, Map[] expected, byte tile = 1)
        {
            ChangeTracker<object> tracker = new ChangeTracker<object>();
            var c = new Map(initial.Width, initial.Height);
            for (int i = 0; i < initial.Tiles.Count; i++)
                c.Tiles[i] = initial.Tiles[i];
            var editor = new TileEditor(c, tracker);

            editor.Selection.Contents.Tiles[0] = tile;
            int e = 0;
            for(int y = 0; y < initial.Height; y++)
            {
                for(int x = 0; x < initial.Width; x++)
                {
                    editor.BeginSelection(x, y, TileEditorActions.Fill);
                    editor.CommitSelection();

                    var expectedIndex = expectedIndexes[e++];
                    if (expectedIndex < 0)
                        Assert.Equal(initial.Tiles, editor.Tiles.Tiles);
                    else
                        Assert.Equal(expected[expectedIndex].Tiles, editor.Tiles.Tiles);

                    tracker.Undo();
                }
            }
        }

        public static IEnumerable<object[]> LargestMapTests
        {
            get
            {
                //Largest rectangles (19200x16 = 307200 tiles)
                //yield return new object[] { new Map(19200, 16, 0) };
                //yield return new object[] { new Map(16, 19200, 0) };
                
                //Second largest rectangles (complies with the default minimum map size) (9600x32 = max)
                //yield return new object[] { new Map(9600, 32, 0) };
                //yield return new object[] { new Map(32, 9600, 0) };

                //Largest square (554x554 = 306916 tiles)
                yield return new object[] { new Map(554,554, 0) };
            }
        }
        [Theory]
        [MemberData(nameof(LargestMapTests))]
        //By default, CS only supports maps up to 307200 tiles large
        //this test is intended to map sure that TKT can fill a map of that size in a reasonable time
        public void CanFillLargestMaps(Map map, byte fill = 1)
        {
            output.WriteLine($"Filling {map.Width}x{map.Height}");
            ChangeTracker<object> tracker = new ChangeTracker<object>();

            var editor = new TileEditor(map, tracker);
            editor.Selection.Contents.Tiles[0] = fill;

            editor.BeginSelection(0, 0, TileEditorActions.Fill);
            editor.CommitSelection();

            for (int i = 0; i < editor.Tiles.Tiles.Count; i++)
                Assert.Equal(fill, editor.Tiles.Tiles[i]);

        }
    }
}

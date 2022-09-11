using CaveStoryModdingFramework.Editors;
using CaveStoryModdingFramework.Maps;
using CaveStoryModdingFramework.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace CaveStoryModdingFrameworkTests
{
    public class TileEditorTests
    {
        public class FillTests : IEnumerable<object[]>
        {
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

            readonly FillTest[] maps = new FillTest[]
            {
                //Blank 1x1
                new FillTest(new Map(1,1,0), new Map(1,1,1)),
                //Blank 2x2
                new FillTest(new Map(2,2,0), new Map(2,2,1)),
                //O shape
                new FillTest(new Map(3,3,0){ Tiles = new List<byte?>(){
                    0, 0, 0,
                    0, 1, 0,
                    0, 0, 0
                } }, new Map(3,3,1)),
                //U shape
                new FillTest(new Map(5,5,0){ Tiles = new List<byte?>()
                {
                    0, 0, 0, 0, 0,
                    0, 1, 0, 1, 0,
                    0, 1, 0, 1, 0,
                    0, 1, 1, 1, 0,
                    0, 0, 0, 0, 0
                } }, new Map(5,5,1)),
                //I shape
                new FillTest(new Map(5,5,0){ Tiles = new List<byte?>()
                {
                    0, 0, 0, 0, 0,
                    1, 1, 0, 1, 1,
                    0, 1, 0, 1, 0,
                    1, 1, 0, 1, 1,
                    0, 0, 0, 0, 0
                } }, new[]
                {
                    0, 0, 0, 0, 0,
                   -1,-1, 0,-1, -1,
                    1,-1, 0,-1, 2,
                   -1,-1, 0,-1, -1,
                    0, 0, 0, 0, 0
                }, new Map(5,5,0){ Tiles = new List<byte?>()
                {
                    1, 1, 1, 1, 1,
                    1, 1, 1, 1, 1,
                    0, 1, 1, 1, 0,
                    1, 1, 1, 1, 1,
                    1, 1, 1, 1, 1
                } }, new Map(5,5,0){ Tiles = new List<byte?>()
                {
                    0, 0, 0, 0, 0,
                    1, 1, 0, 1, 1,
                    1, 1, 0, 1, 0,
                    1, 1, 0, 1, 1,
                    0, 0, 0, 0, 0
                } }, new Map(5,5,0){ Tiles = new List<byte?>()
                {
                    0, 0, 0, 0, 0,
                    1, 1, 0, 1, 1,
                    0, 1, 0, 1, 1,
                    1, 1, 0, 1, 1,
                    0, 0, 0, 0, 0
                } })
            };

            public IEnumerator<object[]> GetEnumerator()
            {
                foreach (var m in maps)
                    yield return m.ToArray();
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Theory]
        [ClassData(typeof(FillTests))]
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
    }
}

using CaveStoryModdingFramework.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace CaveStoryModdingFrameworkTests
{
    public class LinkedStreamTests
    {
        private readonly ITestOutputHelper output;
        public LinkedStreamTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void EmptyStreamOperationsOk()
        {
            //empty stream looks ok
            var s = new LinkedByteStream();
            Assert.Equal(0, s.Position);
            Assert.True(s.PositionValid);

            //reading doesn't change anything
            var b = s.ReadByte();
            Assert.Equal(-1, b);
            Assert.Equal(0, s.Position);
            Assert.True(s.PositionValid);

            //writing expands
            s.WriteByte(0xFF);
            Assert.Equal(1, s.Position);
            Assert.True(s.PositionValid);

            //reading still doesn't change anything
            b = s.ReadByte();
            Assert.Equal(-1, b);
            Assert.Equal(1, s.Position);
        }
        [Fact]
        public void CanCreateFromBytes()
        {
            var data = new byte[] { 0, 1, 2, 3, 4, 5 };
            var s = new LinkedByteStream(data);
            Assert.Equal(data.Length, s.Length);
            Assert.Equal(0, s.Position);
            Assert.True(s.PositionValid);

            var rdata = new byte[data.Length];
            var l = s.Read(rdata, 0, rdata.Length);
            Assert.Equal(s.Length, s.Position);
            Assert.Equal(data.Length, l);
            Assert.Equal(data, rdata);
        }
                
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)] //TODO why is this one so much slower than all the others
        [InlineData(6)]
        [InlineData(7)]
        [InlineData(8)]
        [InlineData(9)]
        [InlineData(10)]
        public void AllSeeksWork(byte amount)
        {
            var data = Enumerable.Range(0, amount).Select(x => (byte)x).ToArray();
            var s = new LinkedByteStream(data);

            for(int i =  0; i < data.Length; i++)
            {
                for(int j =  0; j < data.Length; j++)
                {
                    var p = s.Position;
                    try
                    {
                        s.Position = i;
                        Assert.Equal(i, s.Position);
                    }
                    catch (EqualException)
                    {
                        output.WriteLine($"{p} -> {i} != {s.Position}");
                        throw;
                    }
                    try
                    {
                        s.Position = j;
                        Assert.Equal(j, s.Position);
                    }
                    catch (EqualException)
                    {
                        output.WriteLine($"{i} -> {j} != {s.Position}");
                        throw;
                    }
                    var d = s.ReadByte();
                    try
                    {
                        Assert.Equal(data[j], d);
                    }
                    catch (EqualException)
                    {
                        output.WriteLine($"{i} -> data[{j}] != {d}");
                        throw;
                    }
                }
            }
        }

        public static IEnumerable<object[]> WriteTests
        {
            get
            {
                //do nothing
                yield return new object[]
                {
                    Array.Empty<byte>(),
                    0L,
                    Array.Empty<byte>(),
                    Array.Empty<byte>(),
                };

                //fill after create
                yield return new object[]
                {
                    Array.Empty<byte>(),
                    0L,
                    new byte[]{ 0, 1, 2, 3, 4, 5 },
                    new byte[]{ 0, 1, 2, 3, 4, 5 },
                };

                //overwrite entire thing
                yield return new object[]
                {
                    new byte[]{ 0, 1, 2, },
                    0L,
                    new byte[]{ 3, 4, 5 },
                    new byte[]{ 3, 4, 5 },
                };

                //append
                yield return new object[]
                {
                    new byte[]{ 0, 1, 2, },
                    3L,
                    new byte[]{ 3, 4, 5 },
                    new byte[]{ 0, 1, 2, 3, 4, 5 },
                };

                //partial append
                yield return new object[]
                {
                    new byte[]{ 0, 1, 2, },
                    2L,
                    new byte[]{ 3, 4, 5 },
                    new byte[]{ 0, 1, 3, 4, 5 },
                };
            }
        }

        [Theory]
        [MemberData(nameof(WriteTests))]
        public void WriteWorks(byte[] initial, long offset, byte[] data, byte[] expected)
        {
            var s = new LinkedByteStream(initial);
            Assert.True(s.PositionValid);
            Assert.Equal(0, s.Position);
            Assert.Equal(initial.Length, s.Length);

            s.Position = offset;
            Assert.Equal(offset, s.Position);
            Assert.True(s.PositionValid);

            s.Write(data, 0, data.Length);
            Assert.Equal(offset + data.Length, s.Position);

            s.Position = 0;
            Assert.Equal(0, s.Position);
            Assert.True(s.PositionValid);

            var actual = new byte[s.Length];
            var r = s.Read(actual, 0, actual.Length);
            Assert.Equal(actual.Length, r);
            Assert.Equal(expected, actual);
        }
        public static IEnumerable<object[]> InsertTests
        {
            get
            {
                //do nothing
                yield return new object[]
                {
                    Array.Empty<byte>(),
                    0L,
                    Array.Empty<byte>(),
                    Array.Empty<byte>(),
                };

                //fill after create
                yield return new object[]
                {
                    Array.Empty<byte>(),
                    0L,
                    new byte[]{ 0, 1, 2, 3, 4, 5 },
                    new byte[]{ 0, 1, 2, 3, 4, 5 },
                };

                //insert in middle
                yield return new object[]
                {
                    new byte[]{ 0, 1, 5, },
                    2L,
                    new byte[]{ 2, 3, 4, },
                    new byte[]{ 0, 1, 2, 3, 4, 5 },
                };
                yield return new object[]
                {
                    new byte[]{ 0, 4, 5, },
                    1L,
                    new byte[]{ 1, 2, 3, },
                    new byte[]{ 0, 1, 2, 3, 4, 5 },
                };

                //append
                yield return new object[]
                {
                    new byte[]{ 0, 1, 2, },
                    3L,
                    new byte[]{ 3, 4, 5 },
                    new byte[]{ 0, 1, 2, 3, 4, 5 },
                };
            }
        }

        [Theory]
        [MemberData(nameof(InsertTests))]
        public void InsertWorks(byte[] initial, long offset, byte[] data, byte[] expected)
        {
            void DoInsert(bool follow)
            {
                var s = new LinkedByteStream(initial);
                Assert.True(s.PositionValid);
                Assert.Equal(0, s.Position);
                Assert.Equal(initial.Length, s.Length);

                s.Position = offset;
                Assert.Equal(offset, s.Position);
                Assert.True(s.PositionValid);

                s.Insert(data, 0, data.Length, follow);
                if (follow)
                    Assert.Equal(offset + data.Length, s.Position);
                else
                    Assert.Equal(offset, s.Position);

                s.Position = 0;
                Assert.Equal(0, s.Position);
                Assert.True(s.PositionValid);

                var actual = new byte[s.Length];
                var r = s.Read(actual, 0, actual.Length);
                Assert.Equal(actual.Length, r);
                Assert.Equal(expected, actual);
            }
            DoInsert(false);
            DoInsert(true);
        }

        public static IEnumerable<object[]> HybridWriteTests
        {
            get
            {
                //do nothing
                yield return new object[]
                {
                    Array.Empty<byte>(),
                    0L,
                    Array.Empty<byte>(),
                    0, 0,
                    Array.Empty<byte>(),
                };

                //fill after create
                for (int i = 0; i <= 6; i++)
                {
                    yield return new object[]
                    {
                        Array.Empty<byte>(),
                        0L,
                        new byte[]{ 0, 1, 2, 3, 4, 5 },
                        i, 6-i,
                        new byte[]{ 0, 1, 2, 3, 4, 5 },
                    };
                }

                //normal usage
                yield return new object[]
                {
                    new byte[]{ 0, 1, 2, 3, 4, 5 },
                    0L,
                    new byte[]{ 7, 8, 9, 10 },
                    1, 3,
                    new byte[]{ 7, 8, 9, 10, 1, 2, 3, 4, 5 },
                };
                yield return new object[]
                {
                    new byte[]{ 0, 1, 2, 3, 4, 5 },
                    0L,
                    new byte[]{ 7, 8, 9, 10 },
                    2, 2,
                    new byte[]{ 7, 8, 9, 10, 2, 3, 4, 5 },
                };
                yield return new object[]
                {
                    new byte[]{ 0, 1, 2, 3, 4, 5 },
                    0L,
                    new byte[]{ 7, 8, 9, 10 },
                    3, 1,
                    new byte[]{ 7, 8, 9, 10, 3, 4, 5 },
                };

                //partial overwrite, partial append
                yield return new object[]
                {
                    new byte[]{ 0, 1, 2, 3, 4, 5 },
                    4L,
                    new byte[]{ 7, 8, 9, 10 },
                    1, 3,
                    new byte[]{ 0, 1, 2, 3, 7, 8, 9, 10, 5 },
                };
                yield return new object[]
                {
                    new byte[]{ 0, 1, 2, 3, 4, 5 },
                    4L,
                    new byte[]{ 7, 8, 9, 10 },
                    2, 2,
                    new byte[]{ 0, 1, 2, 3, 7, 8, 9, 10 },
                };
                yield return new object[]
                {
                    new byte[]{ 0, 1, 2, 3, 4, 5 },
                    4L,
                    new byte[]{ 7, 8, 9, 10 },
                    3, 1,
                    new byte[]{ 0, 1, 2, 3, 7, 8, 9, 10 },
                };
            }
        }

        [Theory]
        [MemberData(nameof(HybridWriteTests))]
        public void HybridWriteWorks(byte[] initial, long offset, byte[] data, int write, int insert, byte[] expected)
        {
            void DoInsert(bool follow)
            {
                var s = new LinkedByteStream(initial);
                Assert.True(s.PositionValid);
                Assert.Equal(0, s.Position);
                Assert.Equal(initial.Length, s.Length);

                s.Position = offset;
                Assert.Equal(offset, s.Position);
                Assert.True(s.PositionValid);

                s.HybridWrite(data, 0, write, insert, follow);
                if (follow)
                    Assert.Equal(offset + write + insert, s.Position);
                else
                    Assert.Equal(offset + write, s.Position);
                

                s.Position = 0;
                Assert.Equal(0, s.Position);
                Assert.True(s.PositionValid);

                var actual = new byte[s.Length];
                var r = s.Read(actual, 0, actual.Length);
                Assert.Equal(actual.Length, r);
                Assert.Equal(expected, actual);
            }
            DoInsert(false);
            DoInsert(true);
        }
    }
}

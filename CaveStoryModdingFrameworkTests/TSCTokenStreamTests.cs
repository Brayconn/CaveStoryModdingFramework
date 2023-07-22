using CaveStoryModdingFramework.Editors;
using CaveStoryModdingFramework.TSC;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace CaveStoryModdingFrameworkTests
{
    public class TSCTokenStreamTests
    {
        static FieldInfo readLine = typeof(TSCTokenStream).GetField(nameof(readLine), BindingFlags.NonPublic | BindingFlags.Instance)!;
        static FieldInfo writeLine = typeof(TSCTokenStream).GetField(nameof(writeLine), BindingFlags.NonPublic | BindingFlags.Instance)!;
        static MethodInfo SetPosition = typeof(TSCTokenStream).GetMethod(nameof(SetPosition), BindingFlags.NonPublic | BindingFlags.Instance)!;

        static Command msg = CommandList.BaseCommands.First(x => x.ShortName == "MSG");
        static Command nod = CommandList.BaseCommands.First(x => x.ShortName == "NOD");
        static Command tra = CommandList.BaseCommands.First(x => x.ShortName == "TRA");
        static Command end = CommandList.BaseCommands.First(x => x.ShortName == "END");

        public static IList<TSCTokenLine> Doc1
        {
            get => new List<TSCTokenLine>
                    {
                        new TSCTokenLine(
                            new TSCEventToken("#0200", TSCTokenValidity.Valid, 200),
                            new TSCTextToken("\r", TSCTextTypes.Ignored),
                            new TSCEventEndToken("\n")),

                        new TSCTokenLine(
                            new TSCCommandToken("<MSG", TSCTokenValidity.Valid, msg),
                            new TSCTextToken("Hello, World!", TSCTextTypes.Printed),
                            new TSCCommandToken("<NOD", TSCTokenValidity.Valid, nod),
                            new TSCTextNewLineToken("\r\n")),

                        new TSCTokenLine(
                            new TSCCommandToken("<TRA", TSCTokenValidity.Valid, tra),
                            new TSCArgumentToken("0001:", TSCTokenValidity.Valid, (Argument)tra.Arguments[0]),
                            new TSCArgumentToken("0002:", TSCTokenValidity.Valid, (Argument)tra.Arguments[1]),
                            new TSCArgumentToken("0003:", TSCTokenValidity.Valid, (Argument)tra.Arguments[2]),
                            new TSCArgumentToken("0004",  TSCTokenValidity.Valid, (Argument)tra.Arguments[3]))
                    };
        }

        public static IEnumerable<object[]> ReplaceTests
        {
            get
            {
                static object[] makeTest(IList<TSCTokenLine> lines, int offset, int length, string replaceText, string expectedText, int expectedRead, int expectedWrite)
                {
                    return new object[] { lines, offset, length, replaceText, expectedText, expectedRead, expectedWrite };
                }

                yield return makeTest(
                    new List<TSCTokenLine>
                    {

                    },
                    0, 0, "",
                    "", 0, 0
                );

                yield return makeTest(
                    new List<TSCTokenLine>
                    {
                        new TSCTokenLine(new TSCTextToken("ABCD", TSCTextTypes.Printed))
                    },
                    1, 2, "__",
                    "A__D", 0, 0
                );

                yield return makeTest(
                    new List<TSCTokenLine>
                    {
                        new TSCTokenLine(new TSCTextToken("ABCD", TSCTextTypes.Printed), new TSCTextNewLineToken("\r\n")),
                        new TSCTokenLine(new TSCTextToken("EFGH", TSCTextTypes.Printed))
                    },
                    0, 4, "____",
                    "____", 0, 0
                );

                yield return makeTest(
                    new List<TSCTokenLine>
                    {
                        new TSCTokenLine(new TSCTextToken("ABCD", TSCTextTypes.Printed), new TSCTextNewLineToken("\r\n")),
                        new TSCTokenLine(new TSCTextToken("EFGH", TSCTextTypes.Printed))
                    },
                    0, 6, "____",
                    "____", 1, 0
                );

                yield return makeTest(
                    Doc1,
                    2, 1, "4",
                    "#0400", 0, 0
                );

                yield return makeTest(
                    Doc1,
                    11, "Hello".Length, "Goodbye",
                    "Goodbye, World!", 1, 1
                );
            }
        }

        [Theory]
        [MemberData(nameof(ReplaceTests))]
        public void CanReplace(IList<TSCTokenLine> lines, int offset, int length, string replaceText, string expectedText, int expectedRead, int expectedWrite)
        {
            var ts = new TSCTokenStream(lines);
            
            var actualText = ts.Replace(offset, length, replaceText);
            var rl = (int)readLine.GetValue(ts)!;
            var wl = (int)writeLine.GetValue(ts)!;

            Assert.Equal(expectedText, actualText);
            Assert.Equal(expectedRead, rl);
            Assert.Equal(expectedWrite, wl);
        }

        public static IEnumerable<object[]> CanPushTests
        {
            get
            {
                static object[] makeTest(IList<TSCTokenLine> lines, int startLine, int startOffset, IEnumerable<ITSCToken> tokens, IList<TSCTokenLine> expectedLines)
                {
                    return new object[] { lines, startLine, startOffset, tokens, expectedLines };
                }

                yield return makeTest(new List<TSCTokenLine>(), 0, 0, new List<ITSCToken>(), new List<TSCTokenLine>());

                yield return makeTest(new List<TSCTokenLine>()
                {
                }, 0, 0,
                new List<ITSCToken>
                {
                    new TSCTextToken("AB", TSCTextTypes.Printed),
                    new TSCTextToken("CD", TSCTextTypes.Printed),
                    new TSCTextToken("EF", TSCTextTypes.Printed)
                },
                new List<TSCTokenLine>()
                {
                     new TSCTokenLine(
                         new TSCTextToken("AB", TSCTextTypes.Printed),
                         new TSCTextToken("CD", TSCTextTypes.Printed),
                         new TSCTextToken("EF", TSCTextTypes.Printed))
                });

                yield return makeTest(new List<TSCTokenLine>()
                {
                }, 0, 0,
                new List<ITSCToken>
                {
                    new TSCTextToken("AB", TSCTextTypes.Printed),
                    new TSCTextToken("CD", TSCTextTypes.Printed),
                    new TSCTextNewLineToken("\r\n"),
                    new TSCTextToken("EF", TSCTextTypes.Printed),
                    new TSCTextToken("GH", TSCTextTypes.Printed),
                },
                new List<TSCTokenLine>()
                {
                     new TSCTokenLine(
                         new TSCTextToken("AB", TSCTextTypes.Printed),
                         new TSCTextToken("CD", TSCTextTypes.Printed),
                         new TSCTextNewLineToken("\r\n")),
                     new TSCTokenLine(
                         new TSCTextToken("EF", TSCTextTypes.Printed),
                         new TSCTextToken("GH", TSCTextTypes.Printed))
                });

                yield return makeTest(new List<TSCTokenLine>()
                {
                    new TSCTokenLine(
                        new TSCTextToken("CD", TSCTextTypes.Printed),
                        new TSCTextToken("EF", TSCTextTypes.Printed))
                }, 0, 0,
                new List<ITSCToken>
                {
                    new TSCTextToken("AB", TSCTextTypes.Printed)
                },
                new List<TSCTokenLine>()
                {
                     new TSCTokenLine(
                         new TSCTextToken("AB", TSCTextTypes.Printed),
                         new TSCTextToken("CD", TSCTextTypes.Printed),
                         new TSCTextToken("EF", TSCTextTypes.Printed))
                });

                yield return makeTest(new List<TSCTokenLine>()
                {
                    new TSCTokenLine(
                        new TSCTextToken("AB", TSCTextTypes.Printed),
                        new TSCTextToken("EF", TSCTextTypes.Printed))
                }, 0, 1,
                new List<ITSCToken>
                {
                    new TSCTextToken("CD", TSCTextTypes.Printed)
                },
                new List<TSCTokenLine>()
                {
                     new TSCTokenLine(
                         new TSCTextToken("AB", TSCTextTypes.Printed),
                         new TSCTextToken("CD", TSCTextTypes.Printed),
                         new TSCTextToken("EF", TSCTextTypes.Printed))
                });

                yield return makeTest(new List<TSCTokenLine>()
                {
                    new TSCTokenLine(
                        new TSCEventToken("#0200", TSCTokenValidity.Valid, 200),
                        new TSCTextToken("\r", TSCTextTypes.Ignored),
                        new TSCEventEndToken("\n")),
                    new TSCTokenLine(
                        new TSCCommandToken("<MSG", TSCTokenValidity.Valid, msg),
                        new TSCCommandToken("<END", TSCTokenValidity.Valid, end))
                },
                1, 1,
                new List<ITSCToken>
                {
                    new TSCTextToken("Hello, World!", TSCTextTypes.Printed)
                },
                new List<TSCTokenLine>()
                {
                    new TSCTokenLine(
                        new TSCEventToken("#0200", TSCTokenValidity.Valid, 200),
                        new TSCTextToken("\r", TSCTextTypes.Ignored),
                        new TSCEventEndToken("\n")),
                    new TSCTokenLine(
                        new TSCCommandToken("<MSG", TSCTokenValidity.Valid, msg),
                        new TSCTextToken("Hello, World!", TSCTextTypes.Printed),
                        new TSCCommandToken("<END", TSCTokenValidity.Valid, end))
                });

                yield return makeTest(new List<TSCTokenLine>()
                {
                    new TSCTokenLine(
                        new TSCEventToken("#0200", TSCTokenValidity.Valid, 200),
                        new TSCTextToken("\r", TSCTextTypes.Ignored),
                        new TSCEventEndToken("\n")),
                    new TSCTokenLine(
                        new TSCCommandToken("<MSG", TSCTokenValidity.Valid, msg),
                        new TSCTextToken("Hello, World!", TSCTextTypes.Printed),
                        new TSCCommandToken("<END", TSCTokenValidity.Valid, end))
                },
                1, 3,
                new List<ITSCToken>
                {
                    new TSCTextNewLineToken("\r\n")
                },
                new List<TSCTokenLine>()
                {
                    new TSCTokenLine(
                        new TSCEventToken("#0200", TSCTokenValidity.Valid, 200),
                        new TSCTextToken("\r", TSCTextTypes.Ignored),
                        new TSCEventEndToken("\n")),
                    new TSCTokenLine(
                        new TSCCommandToken("<MSG", TSCTokenValidity.Valid, msg),
                        new TSCTextToken("Hello, World!", TSCTextTypes.Printed),
                        new TSCCommandToken("<END", TSCTokenValidity.Valid, end),
                        new TSCTextNewLineToken("\r\n"))
                });
            }
        }

        [Theory]
        [MemberData(nameof(CanPushTests))]
        public void CanPush(IList<TSCTokenLine> lines, int startLine, int startOffset, IEnumerable<ITSCToken> tokens, IList<TSCTokenLine> expectedLines)
        {
            var ts = new TSCTokenStream(lines);

            writeLine.SetValue(ts, startLine);
            SetPosition.Invoke(ts, new object[] { startLine, startOffset });

            foreach(var t in tokens)
                ts.Push(t);

            CheckLines(expectedLines, lines);
        }

        

        public static IEnumerable<object[]> CanPopTests
        {
            get
            {
                static object[] makeTest(IList<TSCTokenLine> lines, int startLine, int startOffset, int popCount, IList<TSCTokenLine> expectedLines)
                {
                    return new object[] { lines, startLine, startOffset, popCount, expectedLines };
                }

                yield return makeTest(new List<TSCTokenLine>(), 0, 0, 0, new List<TSCTokenLine>());

                yield return makeTest(new List<TSCTokenLine>()
                {
                    new TSCTokenLine(
                        new TSCTextToken("AB", TSCTextTypes.Printed),
                        new TSCTextToken("CD", TSCTextTypes.Printed),
                        new TSCTextToken("EF", TSCTextTypes.Printed))
                }, 0, 0, 1,
                new List<TSCTokenLine>()
                {
                    new TSCTokenLine(
                        new TSCTextToken("CD", TSCTextTypes.Printed),
                        new TSCTextToken("EF", TSCTextTypes.Printed))
                });

                yield return makeTest(new List<TSCTokenLine>()
                {
                    new TSCTokenLine(
                        new TSCTextToken("AB", TSCTextTypes.Printed),
                        new TSCTextToken("CD", TSCTextTypes.Printed),
                        new TSCTextToken("EF", TSCTextTypes.Printed))
                }, 0, 0, 2,
                new List<TSCTokenLine>()
                {
                    new TSCTokenLine(
                        new TSCTextToken("EF", TSCTextTypes.Printed))
                });

                yield return makeTest(new List<TSCTokenLine>()
                {
                    new TSCTokenLine(
                        new TSCTextToken("AB", TSCTextTypes.Printed),
                        new TSCTextToken("CD", TSCTextTypes.Printed),
                        new TSCTextToken("EF", TSCTextTypes.Printed))
                }, 0, 0, 3,
                new List<TSCTokenLine>()
                {
                    new TSCTokenLine()
                });

                yield return makeTest(new List<TSCTokenLine>()
                {
                    new TSCTokenLine(
                        new TSCTextToken("AB", TSCTextTypes.Printed),
                        new TSCTextToken("CD", TSCTextTypes.Printed),
                        new TSCTextToken("EF", TSCTextTypes.Printed))
                }, 0, 1, 1,
                new List<TSCTokenLine>()
                {
                    new TSCTokenLine(
                        new TSCTextToken("AB", TSCTextTypes.Printed),
                        new TSCTextToken("EF", TSCTextTypes.Printed))
                });

                yield return makeTest(new List<TSCTokenLine>()
                {
                    new TSCTokenLine(
                        new TSCTextToken("AB", TSCTextTypes.Printed),
                        new TSCTextToken("CD", TSCTextTypes.Printed),
                        new TSCTextToken("EF", TSCTextTypes.Printed))
                }, 0, 1, 2,
                new List<TSCTokenLine>()
                {
                    new TSCTokenLine(
                        new TSCTextToken("AB", TSCTextTypes.Printed))
                });

                yield return makeTest(new List<TSCTokenLine>()
                {
                    new TSCTokenLine(
                        new TSCTextToken("AB", TSCTextTypes.Printed),
                        new TSCTextToken("CD", TSCTextTypes.Printed),
                        new TSCTextToken("EF", TSCTextTypes.Printed))
                }, 0, 2, 1,
                new List<TSCTokenLine>()
                {
                    new TSCTokenLine(
                        new TSCTextToken("AB", TSCTextTypes.Printed),
                        new TSCTextToken("CD", TSCTextTypes.Printed))
                });
            }
        }

        [Theory]
        [MemberData(nameof(CanPopTests))]
        public void CanPop(IList<TSCTokenLine> lines, int startLine, int startToken, int popCount, IList<TSCTokenLine> expectedLines)
        {
            var ts = new TSCTokenStream(lines);

            readLine.SetValue(ts, startLine);
            SetPosition.Invoke(ts, new object[] { startLine, startToken });

            for (int i = 0; i < popCount; i++)
                Assert.True(ts.TryPop(out ITSCToken _));

            CheckLines(expectedLines, lines);
        }

        void CheckLines(IList<TSCTokenLine> expected, IList<TSCTokenLine> actual)
        {
            Assert.Equal(expected.Count, actual.Count);
            for(int l = 0; l < expected.Count; l++)
            {
                Assert.Equal(expected[l].TokenCount, actual[l].TokenCount);
                Assert.Equal(expected[l].TextLength, actual[l].TextLength);
                var ee = expected[l].GetEnumerator();
                var ae = actual[l].GetEnumerator();
                while(ee.Current != null && ae.Current != null)
                {
                    Assert.Equal(ee.Current.GetType(), ae.Current.GetType());
                    Assert.Equal(ee.Current.Text, ae.Current.Text);
                    Assert.Equal(ee.MoveNext(), ae.MoveNext());
                }
            }
        }
    }
}

using CaveStoryModdingFramework.Editors;
using CaveStoryModdingFramework.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace CaveStoryModdingFrameworkTests
{
    public class TSCParserStreamTests
    {
        private readonly ITestOutputHelper output;
        public TSCParserStreamTests(ITestOutputHelper output)
        {
            this.output = output;
        }
        static string ByteArrayToString(byte[] bytes)
        {
            return "[" + string.Join(", ", bytes) + "]";
        }
        static Func<ITSCToken, byte[]> MakeTokenToBytesFunc(Encoding arg, Encoding text)
        {
            return (ITSCToken x) =>
            {
                return x switch
                {
                    TSCArgumentToken or TSCEventToken or TSCCommandToken => arg.GetBytes(x.Text),
                    _ => text.GetBytes(x.Text),
                };
            };
        }
        public static object[] MakeTest(string baseInput, Encoding? argEncoding = null, Encoding? textEncoding = null, params string[] expectedStrings)
        {
            var expectedReads = new (string, int)[expectedStrings.Length];
            for (int i = 0; i < expectedStrings.Length; i++)
                expectedReads[i] = (expectedStrings[i], 0);
            return MakeTest(baseInput, null, argEncoding, textEncoding, expectedReads);
        }
        public static object[] MakeTest(string baseInput, List<TSCTokenLine>? extraData = null, Encoding? argEncoding = null, Encoding? textEncoding = null, params (string,int)[] expectedReads)
        {
            extraData ??= new List<TSCTokenLine>();
            argEncoding ??= EncodingOverrides.EscapedASCII;
            textEncoding ??= Encoding.ASCII;

            var dataB = EncodingOverrides.GetBytesWithEscapes(textEncoding, baseInput);
            
            var readsB = new List<(byte[], int)>(expectedReads.Length);
            foreach (var read in expectedReads)
                readsB.Add((EncodingOverrides.GetBytesWithEscapes(textEncoding, read.Item1), read.Item2));
            
            return new object[] { dataB, new TSCTokenStream(extraData), MakeTokenToBytesFunc(argEncoding, textEncoding), readsB };
        }
        public static IEnumerable<object[]> MakePermutations(string data, Encoding? arg = null, Encoding? text = null)
        {
            for(int perm = 0; perm <= data.Length; perm++)
            {
                int reads, extra;
                if(perm <= 0)
                {
                    reads = data.Length;
                    extra = data.Length;
                }
                else
                {
                    reads = data.Length / perm;
                    extra = data.Length % perm;
                }

                var readsS = new string[reads + (extra != 0 ? 1 : 0)];
                if(extra != 0)
                {
                    for(int i = 0; i < readsS.Length; i++)
                    {
                        for(int j = 0; j < i; j++)
                            readsS[j] = data.Substring(j * perm, perm);

                        readsS[i] = data.Substring(i * perm, extra);
                        
                        for (int j = i+1; j < readsS.Length - i; j++)
                            readsS[j] = data.Substring(extra + ((j-1) * perm), perm);

                        Debug.Assert(!readsS.Contains(null), "A null string snuck in!");

                        yield return MakeTest(data, arg, text, readsS);
                    }
                }
                else
                {
                    for (int i = 0; i < reads; i++)
                        readsS[i] = data.Substring(i * perm, perm);
                    yield return MakeTest(data, arg, text, readsS);
                }
            }
        }
        public static IEnumerable<object[]> ReadOkTests
        {
            get
            {
                yield return MakeTest("ABCD", new List<TSCTokenLine>()
                {
                    new TSCTokenLine(
                        new TSCTextToken("EFGH", TSCTextTypes.Printed)
                        )
                }, null, null,
                ("ABCD",1), ("EFGH",0));

                yield return MakeTest("ABCD", new List<TSCTokenLine>()
                {
                    new TSCTokenLine(
                        new TSCTextToken("EFGH", TSCTextTypes.Printed)
                        )
                }, null, null,
                ("AB", 1), ("CD", 1), ("EF", 0), ("GH", 0));

                yield return MakeTest("ABCD", new List<TSCTokenLine>()
                {
                    new TSCTokenLine(
                        new TSCTextToken("EFGH", TSCTextTypes.Printed)
                        )
                }, null, null,
                ("ABC", 1), ("DEF",0), ("GH", 0));
            }
        }

        [Theory]
        [MemberData(nameof(MakePermutations), "ABCD", null, null)]
        [MemberData(nameof(ReadOkTests))]
        public void ReadOk(byte[] intialData, TSCTokenStream extraData, Func<ITSCToken, byte[]> tokenToBytes, IList<(byte[], int)> reads)
        {
            var parserStream = new TSCParserStream(intialData, extraData, tokenToBytes);

            bool ok = true;
            foreach(var expected in reads)
            {
                var actual = new byte[expected.Item1.Length];
                var actualCount = parserStream.Read(actual, 0, actual.Length);
                output.WriteLine($"Expected \"{EncodingOverrides.EscapedASCII.GetString(expected.Item1)}\" ({ByteArrayToString(expected.Item1)}), " +
                        $"got {EncodingOverrides.EscapedASCII.GetString(actual)} ({ByteArrayToString(actual)})");

                //var tokensRemaining = extraData.Sum(x => x.TokenCount);
                output.WriteLine($"Expected {expected.Item2} tokens remaining");//, got {tokensRemaining}");
                if (actualCount != expected.Item1.Length
                    || !expected.Item1.SequenceEqual(actual)
                    //|| tokensRemaining != expected.Item2
                    )
                {
                    ok = false;
                }
            }
            Assert.True(ok);
        }
    }
}

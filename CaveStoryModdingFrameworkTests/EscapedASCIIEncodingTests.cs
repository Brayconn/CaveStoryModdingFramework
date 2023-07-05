using CaveStoryModdingFramework.Utilities;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using System.Reflection;
using System.Linq;
using Xunit.Sdk;
using Xunit.Abstractions;

namespace CaveStoryModdingFrameworkTests
{
    public class EscapedASCIIEncodingTests
    {
        private readonly ITestOutputHelper output;
        public EscapedASCIIEncodingTests(ITestOutputHelper output)
        {
            this.output = output;
        }
        public static IEnumerable<object[]> GenerateEncodeTests()
        {
            for(int i = 0; i < 256; i++)
            {
                byte[] expected;
                if (StolenCharMap[i].Length == 1)
                    expected = Encoding.ASCII.GetBytes(StolenCharMap[i]);
                else if (StolenCharMap[i].Length == 2)
                    expected = new byte[] { EscapedASCII.EscapeByte };
                else //if (StolenCharMap[i].Length > 2)
                    expected = new byte[] { Convert.ToByte(StolenCharMap[i].Substring(2), 16) };
                

                yield return new object[] { StolenCharMap[i], expected };
            }
        }

        [Theory]
        [MemberData(nameof(GenerateEncodeTests))]
        public void CharMapEncodeOK(string text, byte[] expected)
        {
            var encoding = new EscapedASCII();
            var output = encoding.GetBytes(text);
            Assert.Equal(expected, output);
        }

        public static IEnumerable<object[]> GenerateStandardDecodeTests()
        {
            for (int i = 0; i < 256; i++)
            {
                yield return new object[] { new byte[] { (byte)i }, StolenCharMap[i] };
            }
        }

        [Theory]
        [MemberData(nameof(GenerateStandardDecodeTests))]
        public void CharMapDecodeOK(byte[] input, string expected)
        {
            var encoding = new EscapedASCII();
            string? output;
            if (input[0] == EscapedASCII.EscapeByte)
            {
                output = encoding.GetString(input);
                Assert.Equal(expected + EscapedASCII.EscapeChar, output);
                encoding.DoubleEscapeBytes = false;
            }
            output = encoding.GetString(input);
            Assert.Equal(expected, output);
        }


        public static IEnumerable<object[]> GenerateEscapeEdgeCases(char[] input, byte[] expected)
        {
            var c = new char[input.Length + 1];
            c[0] = EscapedASCII.EscapeChar;
            input.CopyTo(c, 1);
            
            var b = new byte[expected.Length + 1];
            b[0] = EscapedASCII.EscapeByte;
            expected.CopyTo(b, 1);

            yield return new object[] { new string(c), b };
        }

        [Theory]
        [MemberData(nameof(GenerateEscapeEdgeCases), new char[0], new byte[0])]
        [MemberData(nameof(GenerateEscapeEdgeCases), new[] { 'X' }, new[] { (byte)'X' })]
        [MemberData(nameof(GenerateEscapeEdgeCases), new[] { EscapedASCII.EscapeHexChar },           new[] { EscapedASCII.EscapeHexByte })]
        [MemberData(nameof(GenerateEscapeEdgeCases), new[] { EscapedASCII.EscapeHexChar, 'X' },      new[] { EscapedASCII.EscapeHexByte, (byte)'X' })]
        [MemberData(nameof(GenerateEscapeEdgeCases), new[] { EscapedASCII.EscapeHexChar, 'X', 'X' }, new[] { EscapedASCII.EscapeHexByte, (byte)'X', (byte)'X' })]
        public void EncodeEscapeEdgeCases(string input, byte[] expected)
        {
            var encoding = new EscapedASCII();
            var output = encoding.GetBytes(input);
            Assert.Equal(expected, output);

            encoding.ThrowOnInvalidEscapeSequence = true;
            Assert.Throws<EncoderFallbackException>(() => encoding.GetBytes(input));
        }

        public static IEnumerable<object[]> GetBytesWithEscapesTests
        {
            get
            {
                yield return new object[] { Encoding.ASCII, string.Empty, Array.Empty<byte>() };

                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                var jp = Encoding.GetEncoding(932);

                object[] MakeTest(Encoding encoding, params object[] args)
                {
                    var bytes = new List<byte>();
                    var sb = new StringBuilder();
                    for(int i = 0; i < args.Length; i++)
                    {
                        if (args[i] is string s)
                        {
                            sb.Append(s);
                            bytes.AddRange(encoding.GetBytes(s));
                        }
                        else if (args[i] is char c)
                        {
                            if (c != EscapedASCII.EscapeChar)
                                throw new ArgumentException($"Argument {i} was a char, but it was {c} not {EscapedASCII.EscapeChar}");
                            sb.Append(c,2);
                            bytes.Add((byte)c);
                        }
                        else if (args[i] is byte b)
                        {
                            sb.Append("\\x");
                            sb.Append(b.ToString("X2"));
                            bytes.Add(b);
                        }
                        else
                            throw new ArgumentException($"Invalid value in position {i}: {args[i]} ({args[i].GetType()})");
                    }
                    return new object[] { encoding, sb.ToString(), bytes.ToArray() };
                }


                //誰かの通信が聞こえる…
                //
                //0x92,N, // 誰
                //0x82,0xA9, // か
                //0x82, 0xCC, //の
                //0x92, 0xCA, //通
                //0x90, 0x4D, //信
                //0x82, 0xAA, //が
                //0x95, 0xB7, //聞
                //0x82, 0xB1, //こ
                //0x82, 0xA6, //え
                //0x82, 0xE9, //る
                //0x81, 0x63  //…

                //no escaped bytes
                yield return MakeTest(jp, "誰かの通信が聞こえる…");
                
                //single escaped byte
                yield return MakeTest(jp, (byte)0xFF, "誰かの通信が聞こえる…");
                yield return MakeTest(jp, "誰かの通", (byte)0xFF, "信が聞こえる…");
                yield return MakeTest(jp, "誰かの通信が聞こえる…", (byte)0xFF);

                //multiple escaped bytes
                yield return MakeTest(jp, (byte)0xFF, "誰かの通信が聞こえる…", (byte)0xFF);
                yield return MakeTest(jp, "誰かの通", (byte)0xFF, (byte)0xFF, "信が聞こえる…");
                yield return MakeTest(jp, "誰かの通", (byte)0xFF, "信が聞こえる…", (byte)0xFF);

                //escaped escape
                yield return MakeTest(jp, '\\', "誰かの通信が聞こえる…");
                yield return MakeTest(jp, "誰かの通", '\\', "信が聞こえる…");
                yield return MakeTest(jp, "誰かの通信が聞こえる…", '\\');

                //combo
                yield return MakeTest(jp, "誰かの通", '\\', (byte)0xFF, "信が聞こえる…");
                yield return MakeTest(jp, "誰かの通", (byte)0xFF, '\\', "信が聞こえる…");

                //literals/invalids
                yield return MakeTest(jp, "誰かの通\\x信が聞こえる…");
                yield return MakeTest(jp, "誰かの通\\x1信が聞こえる…");
                yield return MakeTest(jp, "誰かの通\\信が聞こえる…");
                yield return MakeTest(jp, "誰かの通信が聞こえる…\\");
                yield return MakeTest(jp, "誰かの通信が聞こえる…\\x");
                yield return MakeTest(jp, "誰かの通信が聞こえる…\\x4");
            }
        }

        [Theory]
        [MemberData(nameof(GetBytesWithEscapesTests))]
        public void CanGetBytesWithEscapes(Encoding encoding, string input, byte[] expected)
        {
            var actual = EncodingOverrides.GetBytesWithEscapes(encoding, input);
            try
            {
                Assert.Equal(expected, actual);
            }
            catch (EqualException)
            {
                output.WriteLine(string.Join(", ", input.ToCharArray()));
                output.WriteLine(string.Join(", ", expected.Select(x => x.ToString("X2"))));
                output.WriteLine(string.Join(", ", actual.Select(x => x.ToString("X2"))));
                throw;
            }
        }


        static string[] StolenCharMap;
        static EscapedASCIIEncodingTests()
        {
            StolenCharMap = (string[])typeof(EscapedASCII)
                .GetField("CharMap", BindingFlags.Static | BindingFlags.NonPublic)
                .GetValue(null);
        }
    }
}

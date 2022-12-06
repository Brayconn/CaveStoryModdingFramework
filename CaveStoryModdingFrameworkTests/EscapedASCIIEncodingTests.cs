using CaveStoryModdingFramework.Utilities;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using System.Reflection;

namespace CaveStoryModdingFrameworkTests
{
    public class EscapedASCIIEncodingTests
    {
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


        static string[] StolenCharMap;
        static EscapedASCIIEncodingTests()
        {
            StolenCharMap = (string[])typeof(EscapedASCII)
                .GetField("CharMap", BindingFlags.Static | BindingFlags.NonPublic)
                .GetValue(null);
        }
    }
}

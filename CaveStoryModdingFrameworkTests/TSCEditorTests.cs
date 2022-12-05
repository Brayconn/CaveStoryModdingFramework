﻿using CaveStoryModdingFramework;
using CaveStoryModdingFramework.Editors;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace CaveStoryModdingFrameworkTests
{
    public class TSCEditorTests
    {
        private readonly ITestOutputHelper output;
        public TSCEditorTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        public static IEnumerable<object[]> CanCheckBytesTests()
        {
            static object[] GenTest(byte[] data, byte[] sequence, int startPos, int endPos, bool contains)
            {
                return new object[] { data, sequence, startPos, endPos, contains };
            }
            yield return GenTest(new byte[] { 0 }, new byte[] { 0 }, 0, 1, true);
            yield return GenTest(new byte[] { 0 }, new byte[] { 0 }, 1, 1, false);

            yield return GenTest(new byte[] { 0 }, new byte[] { 1 }, 0, 0, false);
            yield return GenTest(new byte[] { 0 }, new byte[] { 1 }, 1, 1, false);

            yield return GenTest(new byte[] { 0, 1, 2 }, new byte[] { 0, 1 }, 0, 2, true);
            yield return GenTest(new byte[] { 0, 1, 2 }, new byte[] { 0, 1 }, 1, 1, false);
            yield return GenTest(new byte[] { 0, 1, 2 }, new byte[] { 0, 1 }, 2, 2, false);

            yield return GenTest(new byte[] { 0, 1, 2 }, new byte[] { 1, 2 }, 0, 0, false);
            yield return GenTest(new byte[] { 0, 1, 2 }, new byte[] { 1, 2 }, 1, 3, true);
            yield return GenTest(new byte[] { 0, 1, 2 }, new byte[] { 1, 2 }, 2, 2, false);

        }

        [Theory]
        [MemberData(nameof(CanCheckBytesTests))]
        public void CanCheckBytes(byte[] data, byte[] sequence, int start, int end, bool contains)
        {
            var s = new MemoryStream(data);
            s.Position = start;

            //check peek
            var r = s.CheckBytes(sequence, true);
            Assert.Equal(contains, r);
            Assert.Equal(start, s.Position);

            //check advancing
            r = s.CheckBytes(sequence, false);
            Assert.Equal(contains, r);
            Assert.Equal(end, s.Position);
        }


        public static IEnumerable<object[]> CanReadEscapedByteDigitsTests()
        {
            static object[] GenLinkedListTest(string data, int start, int end, bool advance, bool succeed, byte value)
            {
                var ll = new LinkedList<byte>(Encoding.ASCII.GetBytes(data));
                var s = ll.First;
                for (int i = 0; i < start; i++)
                    s = s.Next;
                var e = ll.First;
                for (int i = 0; i < end; i++)
                    e = e.Next;
                return new object[] { s, e, advance, succeed, value };
            }
            yield return GenLinkedListTest("X", 0, 0, false, false, 0);
            yield return GenLinkedListTest("XX", 0, 0, true, false, 0);
            yield return GenLinkedListTest("AX", 0, 0, true, false, 0);
            yield return GenLinkedListTest("XA", 0, 0, true, false, 0);

            yield return GenLinkedListTest("12", 0, 1, false, true, 0x12);
            yield return GenLinkedListTest("34X", 0, 2, true, true, 0x34);
            yield return GenLinkedListTest("X56", 1, 2, false, true, 0x56);
            yield return GenLinkedListTest("X78X", 1, 3, true, true, 0x78);
            yield return GenLinkedListTest("XX9AX", 2, 4, true, true, 0x9A);
            yield return GenLinkedListTest("XBCXX", 1, 3, true, true, 0xBC);
            yield return GenLinkedListTest("XXDEXX", 2, 4, true, true, 0xDE);
            yield return GenLinkedListTest("XXXF0XXX", 3, 5, true, true, 0xF0);
        }

        [Theory]
        [MemberData(nameof(CanReadEscapedByteDigitsTests))]
        public void CanReadEscapedByteDigits(LinkedListNode<byte> start, LinkedListNode<byte> end, bool advanceOk, bool succeed, byte value)
        {
            var s = LocalExtensions.TryReadEscapedByteDigits(ref start, out var a, out var b);

            //did we end on the right spot?
            Assert.Equal(end, start);
            //did it recognize it's out of input?
            Assert.Equal(advanceOk, a);
            //did the actual read succeed?
            Assert.Equal(succeed, s);
            //if the read was supposed to succeed, did it read the right value
            if (succeed)
                Assert.Equal(value, b);
        }

        public static IEnumerable<object[]> CanReadEscapedByteTests()
        {
            static object[] GenLinkedListTest(string data, int start, int end, bool advance, byte value)
            {
                var ll = new LinkedList<byte>(Encoding.ASCII.GetBytes(data));
                var s = ll.First;
                for (int i = 0; i < start; i++)
                    s = s.Next;
                var e = ll.First;
                for (int i = 0; i < end; i++)
                    e = e.Next;
                return new object[] { s, e, advance, value };
            }
            yield return GenLinkedListTest("\\", 0, 0, false, (byte)'\\');

            yield return GenLinkedListTest("\\x", 0, 1, true, (byte)'\\');
            yield return GenLinkedListTest("\\x", 1, 1, false, (byte)'x');

            yield return GenLinkedListTest("\\xX", 0, 1, true, (byte)'\\');
            yield return GenLinkedListTest("\\xX", 1, 2, true, (byte)'x');
            yield return GenLinkedListTest("\\xX", 2, 2, false, (byte)'X');

            yield return GenLinkedListTest("\\xXX", 0, 1, true, (byte)'\\');
            yield return GenLinkedListTest("\\xXX", 1, 2, true, (byte)'x');
            yield return GenLinkedListTest("\\xXX", 2, 3, true, (byte)'X');
            yield return GenLinkedListTest("\\xXX", 3, 3, false, (byte)'X');

            yield return GenLinkedListTest("\\x11", 0, 3, false, 0x11);
            yield return GenLinkedListTest("\\xFA", 0, 3, false, 0xFA);
        }

        [Theory]
        [MemberData(nameof(CanReadEscapedByteTests))]
        public void CanReadEscapedByte(LinkedListNode<byte> start, LinkedListNode<byte> end, bool advanceOk, byte value)
        {
            var a = LocalExtensions.ReadProcessingEscapes(ref start, out var b);

            //end on the right spot?
            Assert.Equal(end, start);
            //recognize end of input?
            Assert.Equal(advanceOk, a);
            //read the right value?
            Assert.Equal(value, b);
        }

        class LoadParseTest
        {
            /// <summary>
            /// The text that's in this TSC file
            /// </summary>
            public string Input { get; }
            /// <summary>
            /// The raw bytes that should end up in the TSCBuffer
            /// </summary>
            public byte[] Data { get; }
            /// <summary>
            /// The tokens that should be produced when the data is parsed
            /// </summary>
            public string[] Tokens { get; }
            /// <summary>
            /// The encoding this test's text uses
            /// </summary>
            public Encoding Encoding { get; }

            public LoadParseTest(string input, params string[] tokens) : this(input, Encoding.ASCII.CodePage, tokens) { }
            public LoadParseTest(string input, int codepage, params string[] tokens)
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                Encoding = Encoding.GetEncoding(codepage);

                Input = input;
                Data = Encoding.GetBytes(this.Input);
                if (tokens.Length == 0)
                    Tokens = new string[] { input };
                else
                    Tokens = tokens;
            }
            public LoadParseTest(string input, byte[] data, int codepage, params string[] tokens)
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                Encoding = Encoding.GetEncoding(codepage);
                Encoding = Encoding.GetEncoding(Encoding.WebName);

                Input = input;
                Data = data;
                if (tokens.Length == 0)
                    Tokens = new string[] { input };
                else
                    Tokens = tokens;
            }
        }
        static LoadParseTest[] LoadParseTests = new LoadParseTest[]
        {
            new LoadParseTest("#"),
            new LoadParseTest("#0"),
            new LoadParseTest("#02"),
            new LoadParseTest("#020"),
            new LoadParseTest("#0200"),
            new LoadParseTest("#0200\r\n", "#0200", "\r", "\n"),
            new LoadParseTest("#0200\r\n<END", "#0200", "\r", "\n", "<END"),
            new LoadParseTest("#0200\r\n<MSG<END", "#0200", "\r", "\n", "<MSG", "<END"),
            new LoadParseTest("#0200\r\n<MSGHello World!<END", "#0200", "\r", "\n", "<MSG", "Hello World!", "<END"),
            new LoadParseTest("#0200\r\n<KEY<MSGHello World!<NOD<END", "#0200", "\r", "\n", "<KEY", "<MSG", "Hello World!", "<NOD", "<END"),

            new LoadParseTest("#誰かの通信が聞こえる…<NOD<END",
                Encoding.ASCII.GetBytes("#\\x92N\\x82\\xA9").Concat(new byte[]{
                    0x82, 0xCC, //の
                    0x92, 0xCA, //通
                    0x90, 0x4D, //信
                    0x82, 0xAA, //が
                    0x95, 0xB7, //聞
                    0x82, 0xB1, //こ
                    0x82, 0xA6, //え
                    0x82, 0xE9, //る
                    0x81, 0x63  //…
                }).Concat(Encoding.ASCII.GetBytes("<NOD<END")).ToArray(),
                932,
                "#\\x92N\\x82\\xA9", "の通信が聞こえる…<NOD<END")
        };

        public static IEnumerable<object[]> LoadOkSharedTests()
        {
            foreach (var test in LoadParseTests)
                yield return new object[] { test.Encoding.GetBytes(test.Input), test.Data, test.Encoding };
        }

        public static IEnumerable<object[]> LoadOkTests()
        {
            object[] GenerateTest(string data, string expected = null, Encoding encoding = null)
            {
                encoding ??= Encoding.ASCII;
                var b = encoding.GetBytes(data);
                var e = expected != null ? encoding.GetBytes(expected) : b;
                return new object[] { b, e, encoding };
            }

            //*
            yield return GenerateTest(
                "#0300\r\n" +
                "<KEY<FLJ0300:0301<FL+0300\r\n" +
                "<MSGYou open the chest...<NOD<CLR\r\n" +
                "<AM+0001:0050Got the first gun!<NOD<END\r\n\r\n" +
                "#0301\r\n" +
                "<KEY<MSGEmpty...<NOD<END");

            /*
            yield return GenerateTest(
                "#0200\r\n" +
                "<MSG<ML+誰かの通信が聞こえる…<NOD<END",
                "#0200\r\n" +
                "<MSG<ML+\\x92N\\x82\\xA9の通信が聞こえる…<NOD<END",
                Encoding.GetEncoding(932));
            */


            yield return GenerateTest(
                "#0200 #0201\r\n" +
                "<MSGTwo events<NOD<END"
                );
            yield return GenerateTest(
                "#0200 This is effectively #0201 a few comments\r\n" +
                "<KEY<MSGTwo commented events<NOD<END"
                );

            yield return GenerateTest("#04\n0" +
                "<MSGInterrupted event<NOD<END"
                );

            yield return GenerateTest("#022#0208\r\n" +
                "<MSGHello from events 207 and 208!<NOD<END");
            yield return GenerateTest(
                "#04#0271\r\n" +
                "<MSGHello from events 270 and 271!<NOD<END");
            yield return GenerateTest(
                "#2#7071\r\n" +
                "<MSGHello from events 770 and 771!<NOD<END"
                );
            yield return GenerateTest("##$000\r\n" +
                "<MSGFake Overlap<NOD<END");

            yield return GenerateTest(
                "#\n" +
                "<MSGUh oh,<NOD\r\n" +
                "Unexpected comment<NOD<END",

                "#\\x0A<MS" +
                "GUh oh,<NOD\r\n" +
                "Unexpected comment<NOD<END"
                );
            //*/
        }

        static FieldInfo TSCbuffer = typeof(TSCEditor).GetField("TSCbuffer", BindingFlags.NonPublic | BindingFlags.Instance);

        [Theory]
        [MemberData(nameof(LoadOkSharedTests))]
        [MemberData(nameof(LoadOkTests))]
        public void LoadOk(byte[] data, byte[] expected, Encoding encoding)
        {
            TSCEditor editor = new TSCEditor(data, false, encoding);
            var buf = (LinkedList<byte>)TSCbuffer.GetValue(editor);
            Assert.Equal(expected, buf.ToArray());
        }

        
        public static IEnumerable<object[]> ParseOkSharedTests()
        {
            foreach (var test in LoadParseTests)
                yield return new object[] { test.Data, test.Tokens, test.Encoding };
        }
        public static IEnumerable<object[]> ParseOkTests()
        {
            object[] GenTest(string data, params string[] tokens)
            {
                var b = Encoding.ASCII.GetBytes(data);
                if (tokens.Length == 0)
                    return new object[] { b, new string[] { data }, Encoding.ASCII };
                else
                    return new object[] { b, tokens, Encoding.ASCII };
            }

            yield return GenTest("#\\");
            yield return GenTest("#\\x");
            yield return GenTest("#\\xX");
            yield return GenTest("#\\xXX");
            yield return GenTest("#\\x0");
            yield return GenTest("#\\xX0");

            yield return GenTest("#\\000");
            yield return GenTest("#\\xOOO",   "#\\xOO", "O");
            yield return GenTest("#\\xX000",  "#\\xX0", "00");
            yield return GenTest("#\\xXX000", "#\\xXX", "000");
            yield return GenTest("#\\xX0000", "#\\xX0", "000");

            yield return GenTest("#\\000\r\n",   "#\\000", "\r", "\n");
            yield return GenTest("#\\xOOO\r\n",  "#\\xOO","O\r", "\n");
            yield return GenTest("#\\xX000\r\n", "#\\xX0","00\r", "\n");
            yield return GenTest("#\\xXX000\r\n","#\\xXX","000\r", "\n");
            yield return GenTest("#\\xX0000\r\n","#\\xX0","000\r", "\n");

        }

        //static MethodInfo AppendToBuffer = typeof(TSCEditor).GetMethod("AppendToBuffer", BindingFlags.NonPublic | BindingFlags.Instance);
        
        static MethodInfo Parse = typeof(TSCEditor).GetMethod("Parse", BindingFlags.NonPublic | BindingFlags.Instance);

        [Theory]
        [MemberData(nameof(ParseOkSharedTests))]
        [MemberData(nameof(ParseOkTests))]
        public void ParseOk(byte[] data, string[] tokens, Encoding encoding)
        {
            var editor = new TSCEditor(encoding);
            TSCbuffer.SetValue(editor, new LinkedList<byte>(data));
            //AppendToBuffer.Invoke(editor, new object[] { data });
            
            Parse.Invoke(editor, new object[] { 0 });

            checkTokens(editor.Tokens, tokens);
        }

        //TODO this test can definitely be expanded
        [Theory]
        [MemberData(nameof(LoadAndParseOkTests))]
        public void SaveOk(string data, string[] tokens, Encoding encoding)
        {
            var fd = encoding.GetBytes(data);
            var editor = new TSCEditor(fd, false, encoding);
            var ms = new MemoryStream();
            editor.Save(ms);
            Assert.Equal(fd, ms.ToArray());
        }

        public static IEnumerable<object[]> LoadAndParseOkTests()
        {
            foreach (var test in LoadParseTests)
                yield return new object[] { test.Input, test.Tokens, test.Encoding };
        }

        [Theory]
        [MemberData(nameof(LoadAndParseOkTests))]
        public void LoadAndParseOK(string data, string[] tokens, Encoding encoding)
        {
            var editor = new TSCEditor(encoding.GetBytes(data), false, encoding);
            checkTokens(editor.Tokens, tokens);
        }

        void checkTokens(List<List<TSCToken>> tokens, string[] expected)
        {
            bool Ok = true;
            int i = 0;
            foreach (var line in tokens)
            {
                foreach (var token in line)
                {
                    var s = token.GetString();
                    try
                    {
                        Assert.Equal(expected[i], s);
                    }
                    catch (Xunit.Sdk.EqualException ee)
                    {
                        Ok = false;
                        output.WriteLine($"Token {i}/{tokens.Count}:\n" + ee.Message);
                    }
                    i++;
                }
            }
            Assert.True(Ok);
        }
    }
}
using CaveStoryModdingFramework.TSC;
using CaveStoryModdingFramework.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace CaveStoryModdingFramework.Editors
{
    public static class LocalExtensions
    {
        #region Moving linked list nodes around
        public static int Backup<T>(ref LinkedListNode<T> node)
        {
            var okToMove = node.Previous != null;
            if (okToMove)
                node = node.Previous!;
            return okToMove ? 1 : 0;
        }
        public static int Backup<T>(ref LinkedListNode<T> node, int amount)
        {
            int i;
            for (i = 0; node.Previous != null && i < amount; i++)
                node = node.Previous;
            return i;
        }
        public static int Advance<T>(ref LinkedListNode<T> node)
        {
            var okToMove = node.Next != null;
            if (okToMove)
                node = node.Next!;
            return okToMove ? 1 : 0;
        }
        public static int Advance<T>(ref LinkedListNode<T> node, int amount)
        {
            int i;
            for (i = 0; node.Next != null && i < amount; i++)
                node = node.Next;
            return i;
        }
        #endregion

        #region COMPARISONS
        class ComparisonInfo
        {
            public int Sequence;
            public int Offset;
            public int Progress;

            public ComparisonInfo(int sequence, int offset)
            {
                Sequence = sequence;
                Offset = offset;
                Progress = 0;
            }
        }
        /// <summary>
        /// Update the given list of ComparisonInfos based on the latest contents of the buffer and the list of sequences to check for
        /// </summary>
        /// <param name="compares">The list of comparisons to update</param>
        /// <param name="buff">The buffer to compare agains</param>
        /// <param name="seqs">The sequenes to check for</param>
        /// <returns>The sequence that matched, or null if none matched</returns>
        static IList<byte>? Compare(List<ComparisonInfo> compares, List<byte> buff, IList<byte>[] seqs)
        {
            //find any sequences that COULD start at the latest byte added
            for (int j = 0; j < seqs.Length; j++)
                if (buff[buff.Count - 1] == seqs[j][0])
                    compares.Add(new ComparisonInfo(j, buff.Count - 1));

            //for all the active comparisons...
            for (int j = 0; j < compares.Count; /*incremementing j is done later*/ )
            {
                //if the current byte is a mismatch, this comparison is a fail, remove it
                if (buff[compares[j].Offset++] != seqs[compares[j].Sequence][compares[j].Progress++])
                {
                    compares.RemoveAt(j);
                }
                //otherwise, it matched, check if we're at the end
                else if (compares[j].Progress == seqs[j].Count)
                {
                    return seqs[j];
                }
                //we weren't at the end, continue to the next compare...
                else
                {
                    j++;
                }
            }
            return null;
        }

        public static byte[] ReadUntilLengthOrSequences(this Stream stream, int length, params IList<byte>[] seqs)
        {
            var buff = new List<byte>(length);
            var compares = new List<ComparisonInfo>(seqs.Length);

            for(int i = 0; compares.Count > 0 || i < length; i++)
            {
                var bi = stream.ReadByte();
                //stop at end of stream
                if (bi == -1)
                    break;
                //otherwise add it to the buffer
                buff.Add((byte)bi);

                var stopper = Compare(compares, buff, seqs);
                if(stopper != null)
                {
                    buff.RemoveRange(buff.Count - stopper.Count, stopper.Count);
                    stream.Position -= stopper.Count;
                    return buff.ToArray();
                }
            }
            return buff.ToArray();
        }
        public static byte[] ReadUntilSequences(this Stream stream, params IList<byte>[] seqs)
        {
            var buff = new List<byte>();
            var compares = new List<ComparisonInfo>(seqs.Length);

            while (true)
            {
                var bi = stream.ReadByte();
                if (bi == -1)
                    break;
                buff.Add((byte)bi);

                var stopper = Compare(compares, buff, seqs);
                if (stopper != null)
                {
                    buff.RemoveRange(buff.Count - stopper.Count, stopper.Count);
                    stream.Position -= stopper.Count;
                    return buff.ToArray();
                }
            }
            return buff.ToArray();
        }
        public static byte[] ReadUntilSequencesOrNewline(this Stream stream, params IList<byte>[] seqs)
        {
            var buff = new List<byte>();
            var compares = new List<ComparisonInfo>(seqs.Length);

            bool carriageReturnFound = false;
            while (true)
            {
                var bi = stream.ReadByte();
                if (bi == -1)
                    break;
                if (carriageReturnFound && bi != '\n')
                {
                    stream.Position--;
                    break;
                }
                buff.Add((byte)bi);
                if (bi == '\n')
                    break;
                if (bi == '\r')
                    carriageReturnFound = true;

                var stopper = Compare(compares, buff, seqs);
                if (stopper != null)
                {
                    buff.RemoveRange(buff.Count - stopper.Count, stopper.Count);
                    stream.Position -= stopper.Count;
                    return buff.ToArray();
                }
            }
            return buff.ToArray();
        }

        public static List<byte> ReadUntilSequences(ref LinkedListNode<byte> node, out bool advanceOk, params IList<byte>[] seqs)
        {
            var buff = new List<byte>();
            var compares = new List<ComparisonInfo>(seqs.Length);

            advanceOk = true;
            while (advanceOk)
            {
                advanceOk = ReadProcessingEscapes(ref node, out var b);
                buff.Add(b);

                var stopper = Compare(compares, buff, seqs);
                if (stopper != null)
                {
                    buff.RemoveRange(buff.Count - stopper.Count, stopper.Count);
                    Backup(ref node, stopper.Count);
                    return buff;
                }
            }
            return buff;
        }
        public static byte[] ReadUntilLengthOrSequences(ref LinkedListNode<byte> node, out bool advanceOk, int length, params IList<byte>[] seqs)
        {
            var buff = new List<byte>(length);
            var compares = new List<ComparisonInfo>(seqs.Length);
            advanceOk = true;
            for (int i = 0; advanceOk && (compares.Count > 0 || i < length); i++)
            {
                advanceOk = ReadProcessingEscapes(ref node, out var b);
                buff.Add(b);

                var stopper = Compare(compares, buff, seqs);
                if (stopper != null)
                {
                    buff.RemoveRange(buff.Count - stopper.Count, stopper.Count);
                    Backup(ref node, stopper.Count);
                    return buff.ToArray();
                }
            }
            return buff.ToArray();
        }
        public static byte[] ReadRawUntilLengthOrSequences(ref LinkedListNode<byte> node, Encoding encoding, out bool advanceOk, int length, params IList<byte>[] seqs)
        {
            var buff = new List<byte>(length);
            var compares = new List<ComparisonInfo>(seqs.Length);
            advanceOk = true;
            for (int i = 0; advanceOk && (compares.Count > 0 || i < length); i++)
            {
                advanceOk = ReadProcessingEscapes(ref node, out var b);
                var rb = Encoding.ASCII.GetBytes(encoding.GetString(new[] { b }));
                foreach (var _b in rb)
                    buff.Add(_b);

                var stopper = Compare(compares, buff, seqs);
                if (stopper != null)
                {
                    buff.RemoveRange(buff.Count - stopper.Count, stopper.Count);
                    Backup(ref node, stopper.Count);
                    return buff.ToArray();
                }
            }
            return buff.ToArray();
        }

        /// <summary>
        /// Checks if the bytes at this location match the given sequence and outputs that sequence if true
        /// </summary>
        /// <param name="node"></param>
        /// <param name="buff"></param>
        /// <param name="seq"></param>
        /// <returns>Whether or not the sequence matched</returns>
        public static bool CheckAndReadSequence(ref LinkedListNode<byte> node, out bool advanceOk, out List<byte> buff, IList<byte> seq)
        {
            if (seq.Count <= 0)
                throw new ArgumentException("Must provide a sequence of more than 0 bytes", nameof(seq));
            var start = node;
            var startAdv = start.Next != null;
            advanceOk = true;
            buff = new List<byte>(seq.Count);
            for (int i = 0; i < seq.Count; i++)
            {
                //get/add the next byte to the sequence
                advanceOk = ReadProcessingEscapes(ref node, out var b);
                buff.Add(b);
                //if the next byte is incorrect
                if (buff[i] != seq[i]
                    //or we have no more input to read, and we haven't completed the sequence
                    || (!advanceOk && i + 1 < seq.Count))
                {
                    //need to back up once more if we advanced one more
                    node = start;
                    advanceOk = startAdv;
                    return false;
                }
            }
            return true;
        }

        #endregion

        #region basic reading
        static bool GetAndAdvance<T>(ref LinkedListNode<T> node, out T value)
        {
            value = node.Value;
            return Advance(ref node) == 1;
        }
        static bool AddAndAdvance<T>(ref LinkedListNode<T> node, IList<T> list)
        {
            list.Add(node.Value);
            return Advance(ref node) == 1;
        }
        public static T[] Read<T>(this LinkedListNode<T> node, int length)
        {
            var output = new T[length];
            var n = node;
            for (int i = 0; i < length; i++)
            {
                output[i] = n.Value;
                n = n.Next;
            }
            return output;
        }
        #endregion

        public static bool StartsWith<T>(this IList<T> list1, IList<T> list2) where T : IComparable
        {
            for (int i = 0; i < list2.Count; i++)
                if (list1[i].CompareTo(list2[i]) != 0)
                    return false;
            return true;
        }

        public static void AddLast<T>(this LinkedList<T> list, IList<T> items)
        {
            for(int i = 0; i < items.Count; i++)
                list.AddLast(items[i]);
        }

        public static byte[] PeekRawLength(this LinkedListNode<byte> node, int length)
        {
            //overwriting "node" via this call doesn't affect the caller, thus, a peek
            return ReadRawLength(ref node, length);
        }
        
        /// <summary>
        /// Read the given number of bytes WITHOUT converting escape sequences into their byte values
        /// </summary>
        /// <param name="node"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static byte[] ReadRawLength(ref LinkedListNode<byte> node, int length)
        {
            var buff = new List<byte>(length); //TODO investigate performance implications of Capacity
            var advanceOk = true;
            for(int i = 0; advanceOk && i < length; i++)
            {
                advanceOk = AddAndAdvance(ref node, buff);
                if (buff[buff.Count - 1] == EscapedASCII.EscapeByte)
                {
                    advanceOk = AddAndAdvance(ref node, buff);
                    if(buff[buff.Count - 1] == EscapedASCII.EscapeHexByte)
                    {
                        buff.Capacity += 3;
                        AddAndAdvance(ref node, buff);
                        AddAndAdvance(ref node, buff);
                        
                    }
                }
            }
            
            return buff.ToArray();
        }

        #region Escaped bytes
        /// <summary>
        /// 
        /// </summary>
        /// <param name="node"></param>
        /// <param name="advanceOk"></param>
        /// <param name="output"></param>
        /// <returns>Whether the read succeeded</returns>
        public static bool TryReadEscapedByteDigits(ref LinkedListNode<byte> node, out bool advanceOk, out byte output)
        {
            output = 0;
            byte b;
            var hex = new char[2];

            advanceOk = GetAndAdvance(ref node, out b);
            //if there's only one byte here, quit out now
            if (!advanceOk)
                return false;
            hex[0] = (char)b;
            
            advanceOk = GetAndAdvance(ref node, out b);
            hex[1] = (char)b;
            
            if (Extensions.IsHexDigit(hex[0]) && Extensions.IsHexDigit(hex[1]))
            {
                output = Convert.ToByte(new string(hex), 16);
                return true;
            }
            else
            {
                //backup twice if we actually ADVANCED two bytes, otherwise we only need to go back one
                Backup(ref node, advanceOk ? 2 : 1);
                advanceOk = true; //to get here we had to read 2 bytes, so it is ok to advance...
                return false;
            }
        }
        /// <summary>
        /// Read one byte starting at the given node, converting escape sequences into their appropriate byte value.
        /// Invalid escape sequences are read byte-by-byte
        /// </summary>
        /// <param name="node">The node to read from</param>
        /// <param name="value">The byte that was read</param>
        /// <returns>Whether the byte that was read has more input after it</returns>
        public static bool ReadProcessingEscapes(ref LinkedListNode<byte> node, out byte value)
        {
            bool advanceOk = GetAndAdvance(ref node, out value);
            //all escape bytes require there to be a second character, so we can give up if there isn't
            if (value == EscapedASCII.EscapeByte && advanceOk)
            {
                var escapeAdvanceOk = GetAndAdvance(ref node, out var escapeChar);
                switch(escapeChar)
                {
                    //hex bytes require more data, so if there isn't any we're just going to backup
                    case EscapedASCII.EscapeHexByte:
                        if (escapeAdvanceOk)
                        {
                            if (TryReadEscapedByteDigits(ref node, out var dataAdvanceOk, out var data))
                            {
                                advanceOk = dataAdvanceOk;
                                value = data;
                            }
                            else
                                goto default;
                        }
                        break;
                    //escaping the escape character doesn't require there to be any further input
                    //so we preserve advanceOk and continue
                    case EscapedASCII.EscapeByte:
                        advanceOk = escapeAdvanceOk;
                        break;
                    //anything else, we go all the way back to the escape character and read that
                    default:
                        Backup(ref node);
                        break;
                }
            }
            return advanceOk;
        }
        #endregion

        public static readonly int[] PowersOfTen = new int[]
        {
            1,
            10,
            100,
            1000,
            10_000,
            100_000,
            1_000_000,
            10_000_000,
            100_000_000,
            1_000_000_000,
            //Anything over one billion will error
            //Meaning you can't read TSC numbers larger than 10 digits
        };
        public static int PeekTSCNum(this LinkedListNode<byte> node, int length = 4)
        {
            int value = 0;
            for (int i = 0; i < length; i++)
            {
                var nextOk = ReadProcessingEscapes(ref node, out var b);
          
                value += (((sbyte)b) - 0x30) * PowersOfTen[length - 1 - i];
                
                //give up if there's no more input
                if (!nextOk)
                    break;
            }
            return value;
        }
        public static int GetTSCNum(this IList<byte> arr)
        {
            int value = 0;
            for (int i = 0; i < arr.Count; i++)
            {                
                value += (((sbyte)arr[i]) - 0x30) * PowersOfTen[arr.Count - 1 - i];
            }
            return value;
        }
        public static int GetTSCNum(this IList<sbyte> arr)
        {
            int value = 0;
            for (int i = 0; i < arr.Count; i++)
            {
                value += (arr[i] - 0x30) * PowersOfTen[arr.Count - 1 - i];
            }
            return value;
        }

    }

    #region Tokens

    public enum TSCTokenValidity
    {
        /// <summary>
        /// The token is in the correct form
        /// </summary>
        Valid,
        /// <summary>
        /// The token is technically correct, but could be the result of an error
        /// </summary>
        Warning,
        /// <summary>
        /// The token will cause the game to behave unexpectedly (or crash) when encountered
        /// </summary>
        Error,
        /// <summary>
        /// The token is malformed and cannot be understood
        /// </summary>
        //Critical //pray that this never has to be uncommented
    }
    public enum TSCTextTypes
    {
        //TODO command separators are baked into the Argument type, so half of this description is redundant I guess...?
        /// <summary>
        /// Text that the parser skips over (such as command separators), or otherwise ignored (such as text within an event definition)
        /// </summary>
        Ignored,
        /// <summary>
        /// Text that will be printed to the message box
        /// </summary>
        Printed,
        /// <summary>
        /// Text that exists while editing, but will not be written to the tsc file when saving (such as Scriptsource comments)
        /// </summary>
        EditorOnly
    }
    public interface ITSCToken
    {
        string Text { get; }
        public TSCTokenValidity Validity { get; }
    }
    [DebuggerDisplay("{Text}")]
    public class TSCEventToken : ITSCToken
    {
        public string Text { get; set; }

        public TSCTokenValidity Validity { get; set; }

        public int Value { get; set; }

        public TSCEventToken(string text, TSCTokenValidity validity , int value)
        {
            Text = text;
            Validity = validity;
            Value = value;
        }
    }
    [DebuggerDisplay("{Text}")]
    public class TSCEventEndToken : ITSCToken
    {
        public string Text { get; set; }

        public TSCTokenValidity Validity => TSCTokenValidity.Valid;

        public TSCEventEndToken(string text)
        {
            Text = text;
        }
    }
    [DebuggerDisplay("{Text}")]
    public class TSCArgumentToken : ITSCToken
    {
        public string Text { get; set; }

        public TSCTokenValidity Validity { get; set; }

        public Argument Argument { get; set; }

        public TSCArgumentToken(string text, TSCTokenValidity validity, Argument argument)
        {
            Text = text;
            Validity = validity;
            Argument = argument;
        }
    }
    [DebuggerDisplay("{Text}")]
    public class TSCCommandToken : ITSCToken
    {
        public string Text { get; set; }

        public TSCTokenValidity Validity { get; set; }

        public Command? Command { get; set; }

        public TSCCommandToken(string text, TSCTokenValidity validity, Command? command)
        {
            Text = text;
            Validity = validity;
            Command = command;
        }
    }
    [DebuggerDisplay("{Text}")]
    public class TSCTextNewLineToken : ITSCToken
    {
        public string Text { get; set; }

        public TSCTokenValidity Validity => TSCTokenValidity.Valid;

        public TSCTextNewLineToken(string text)
        {
            Text = text;
        }
    }
    [DebuggerDisplay("{Text}")]
    public class TSCTextToken : ITSCToken
    {
        public string Text { get; set; }

        public TSCTokenValidity Validity => TSCTokenValidity.Valid;
        public TSCTextTypes TextType { get; }
        public TSCTextToken(string text, TSCTextTypes textType)
        {
            Text = text;
            TextType = textType;
        }
    }
    #endregion

    public class TSCTokenLine : IEnumerable<ITSCToken>
    {
        public LinkedList<ITSCToken> Tokens { get; }
        public int TokenCount => Tokens.Count;
        public int TextLength { get; private set; } = 0;
        public TSCTokenLine(params ITSCToken[] tokens)
        {
            Tokens = new LinkedList<ITSCToken>();
            foreach (var t in tokens)
                AddLast(t);
        }

        public LinkedListNode<ITSCToken> AddFirst(ITSCToken token)
        {
            TextLength += token.Text.Length;
            return Tokens.AddFirst(token);
        }
        public LinkedListNode<ITSCToken> AddAfter(LinkedListNode<ITSCToken> node, ITSCToken token)
        {
            TextLength += token.Text.Length;
            return Tokens.AddAfter(node, token);
        }
        public LinkedListNode<ITSCToken> AddLast(ITSCToken token)
        {
            TextLength += token.Text.Length;
            return Tokens.AddLast(token);
        }

        public ITSCToken Remove(ref LinkedListNode<ITSCToken>? node)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            ITSCToken token;
            token = node.Value;
            if(node.Next != null)
            {
                node = node.Next;
                Tokens.Remove(node.Previous!);
            }
            else
            {
                Tokens.RemoveLast();
                node = null;
            }
            TextLength -= token.Text.Length;
            return token;
        }

        public override string ToString()
        {
            //TODO investigate capacity
            var sb = new StringBuilder();
            foreach(var i in Tokens)
                sb.Append(i.Text);
            return sb.ToString();
        }

        public IEnumerator<ITSCToken> GetEnumerator()
        {
            return Tokens.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Tokens.GetEnumerator();
        }
    }

    //The two main problems of all TSC editors:
    //
    //1.
    //The TSC parser assumes that all commands/arguments are formatted using ASCII encoding
    //  and makes no attempt to verify that this is actually the case.
    //So you can totally just shove whatever byte values you want into an event number/command argument
    //  and the game will just... read it as if it was valid.
    //Plus, different parts of the parser look for different parts of a Windows-style newline
    //  so the BYTE REPRESENTATION of a TSC file is EXTREMELY IMPORTANT
    //
    //2.
    //In-game textboxes can use essentially ANY encoding,
    //  such as Shift-JIS, Unicode, etc.
    //This wouldn't be a problem if the game only checked for "message box text" in certain locations,
    //  but it doesn't, it checks for it EVERYWHERE THAT ISN'T A COMMAND/ARGUMENT.
    //
    //Therefore, an accurate TSC editor needs to constantly keep in mind what parts of the document use what encoding,
    //  AND automatically convert text that moves between each part into the right format for display/editing.
    //
    //Originally this editor solved this dilemma by storing the entire document as bytes
    //  and converting everything to the right text encoding LAST.
    //Unfortunately, this method quickly proved to make editing infeasible to implement,
    //  so it now stores everything as characters and converts to/from bytes for parsing
    public class TSCEditor
    {
        #region User Editable Parameters
        /// <summary>
        /// What byte sequence denotes the start of an event line
        /// </summary>
        public byte[] EventStart = new byte[] { (byte)'#' };
        /// <summary>
        /// What byte sequence denotes the end of an event line
        /// </summary>
        public byte[] EventEnd = new byte[] { (byte)'\n' };
        /// <summary>
        /// How many digits event number take
        /// </summary>
        public int EventLength = 4;
        /// <summary>
        /// How many bytes to advance after an event number is parsed successfully, but was not the desired value
        /// </summary>
        public int EventAdvance = 1;

        /// <summary>
        /// What bytes sequence denotes the start of a command
        /// </summary>
        public byte[] CommandStart = new byte[] { (byte)'<' };
        
        /// <summary>
        /// What byte sequence counts as a newline when reading text
        /// </summary>
        public byte[] TextNewline = new byte[] { (byte)'\r' };
        /// <summary>
        /// How many ADDITIONAL characters to consume AFTER reading the TextNewLine
        /// </summary>
        public int TextNewLineExtraAdvance = 1;

        /// <summary>
        /// If you put this in your file pixel will come to your house and steal your milk
        /// </summary>
        public byte[] EndOfFileMarker = new byte[] { 0x00 };

        /// <summary>
        /// Used for all TSC arguments (so that escaped bytes work)
        /// </summary>
        internal Encoding ArgumentEncoding;
        /// <summary>
        /// Used for all TSC text/comments (so languages display properly)
        /// </summary>
        public Encoding TextEncoding;

        /// <summary>
        /// The commands this parser will recognize
        /// </summary>
        public List<Command> Commands;
        #endregion
                
        public byte[] GetBytes(ITSCToken tok)
        {
            if (tok is TSCEventToken || tok is TSCCommandToken || tok is TSCArgumentToken)
                return ArgumentEncoding.GetBytes(tok.Text);
            else
                return TextEncoding.GetBytes(tok.Text);
        }

        public IList<TSCTokenLine> Lines { get; }
        internal TSCTokenStream Stream { get; }

        /// <summary>
        /// Creates a new TSC document with the given text encoding and set of commands
        /// </summary>
        /// <param name="textEncoding"></param>
        /// <param name="commands"></param>
        public TSCEditor(Encoding? textEncoding = null, List<Command>? commands = null)
        {
            ArgumentEncoding = new EscapedASCII()
            {
                ForceOneCharWidth = true,
                DoubleEscapeBytes = false
            };
            if (textEncoding != null)
                TextEncoding = EncodingOverrides.GetEncoding(textEncoding.CodePage);
            else
                TextEncoding = EncodingOverrides.EscapedASCII;

            if (commands != null)
                Commands = commands;
            else
                Commands = new List<Command>(CommandList.BaseCommands);

            Lines = new List<TSCTokenLine>();
            Stream = new TSCTokenStream(Lines);
        }
        /// <summary>
        /// Creates a new TSC document with the given input
        /// </summary>
        /// <param name="input"></param>
        /// <param name="encrypted"></param>
        /// <param name="textEncoding"></param>
        /// <param name="commands"></param>
        public TSCEditor(byte[] input, bool encrypted, Encoding? textEncoding = null, List<Command>? commands = null)
            : this(textEncoding, commands)
        {
            if (encrypted)
                Encryptor.DecryptInPlace(input);
            Parse(input);
        }

        /// <summary>
        /// Replaces the text at the specified offset with the given new text
        /// </summary>
        /// <param name="offset">The offset in the document to start replacing text</param>
        /// <param name="length">The length of the text to replace</param>
        /// <param name="text">The new text</param>
        public void Replace(int offset, int length, string text)
        {
            if (text.Length <= 0 && length <= 0)
                return;

            //get text
            var buffer = Stream.Replace(offset, length, text);
            
            //parse
            Parse(EncodingOverrides.GetBytesWithEscapes(TextEncoding, buffer));

            //trigger event?
            
        }

        protected void Parse(byte[] data)
        {
            using (var s = new TSCParserStream(data, this))
            {
                //tokenize
                foreach (var t in TokenizeStream(s))
                {
                    Stream.Push(t);
                }
            }
        }
        protected enum ParserState
        {
            /// <summary>
            /// The parser is parsing an event definition line
            /// </summary>
            EventDefinition,
            /// <summary>
            /// The parser is parsing the body of an event
            /// </summary>
            EventBody,
            /// <summary>
            /// The parser is parsing any text outside an event
            /// </summary>
            OutsideEvent,
            //TODO add state that lets head.tsc work
        }
        protected IEnumerable<ITSCToken> TokenizeStream(Stream stream, ParserState mode = ParserState.OutsideEvent)
        {
            Command? activeCommand = null;
            var args = new Stack<object>();
            var argValues = new List<int>();

            while (stream.Position < stream.Length)
            {
                if (stream.CheckBytes(EventStart))
                {
                    mode = ParserState.EventDefinition;
                    argValues.Clear();

                    //start working on the data
                    var eventData = new List<byte>(EventStart.Length + EventLength);
                    eventData.AddRange(EventStart);

                    //these bytes are free
                    for (int i = 0; i < EventAdvance; i++)
                    {
                        var b = stream.ReadByte();
                        if (b == -1)
                            goto END_OF_STREAM;
                        eventData.Add((byte)b);
                    }
                    //TODO check for end of file sequence in here
                    //add the rest if possible
                    if (stream.Position < stream.Length)
                        eventData.AddRange(stream.ReadUntilLengthOrSequences(EventLength - EventAdvance, EventStart, EventEnd));

                    END_OF_STREAM:
                    yield return new TSCEventToken(ArgumentEncoding.GetString(eventData.ToArray()),
                        eventData.Count == EventLength ? TSCTokenValidity.Valid : TSCTokenValidity.Error,
                        eventData.GetTSCNum());
                }
                else if (stream.CheckBytes(EventEnd))
                {
                    mode = ParserState.EventBody;
                    yield return new TSCEventEndToken(TextEncoding.GetString(EventEnd));
                }
                else if (args.Count > 0)
                {
                    var dequeuedArg = args.Pop();
                    if (dequeuedArg is Argument arg)
                    {
                        List<byte> argData;
                        if (arg.Length > 0)
                        {
                            argData = new List<byte>(arg.Length + arg.Separator.Length);
                            argData.AddRange(stream.ReadUntilLengthOrSequences(arg.Length, EventStart));
                        }
                        else
                        {
                            var sepBytes = ArgumentEncoding.GetBytes(arg.Separator);
                            argData = new List<byte>();
                            argData.AddRange(stream.ReadUntilSequences(sepBytes, EventStart));
                        }
                        if (activeCommand!.UsesRepeats)
                            argValues.Add(argData.GetTSCNum());
                        if (arg.Length <= 0 || args.Count > 0)
                            argData.AddRange(stream.ReadUntilLengthOrSequences(arg.Separator.Length, EventStart));


                        yield return new TSCArgumentToken(
                            ArgumentEncoding.GetString(argData.ToArray()),
                            argData.Count == arg.Length ? TSCTokenValidity.Valid : TSCTokenValidity.Warning,
                            arg);
                    }
                    else if (dequeuedArg is RepeatStructure repeat)
                    {
                        for (int i = 0; i < argValues[repeat.Value]; i++)
                        {
                            for (int j = repeat.Arguments.Count - 1; j >= 0; j--)
                                args.Push(repeat.Arguments[j]);
                        }
                    }
                    else
                        throw new ArgumentException("Invalid TSC argument type! " + dequeuedArg.GetType(), nameof(dequeuedArg));
                    if (args.Count == 0 && (activeCommand!.Properties & CommandProperties.EndsEvent) != 0)
                    {
                        mode = ParserState.OutsideEvent;
                        activeCommand = null;
                    }
                }
                //add command (in event)
                else if (mode == ParserState.EventBody && stream.CheckBytes(CommandStart))
                {
                    var cmdData = new List<byte>();
                    cmdData.AddRange(CommandStart);
                    Command? command = null;
                    for(int j = 0; j < Commands.Count; j++)
                    {
                        var cmdNameBytes = ArgumentEncoding.GetBytes(Commands[j].ShortName);
                        if (stream.CheckBytes(cmdNameBytes))
                        {
                            command = Commands[j];
                            cmdData.AddRange(cmdNameBytes);
                            break;
                        }
                    }
                    if(command != null)
                    {
                        //set the activeCommand if we need to process args
                        if (command.Arguments.Count > 0)
                        {
                            activeCommand = command;
                        }
                        else
                        {
                            //otherwise reset it...
                            activeCommand = null;
                            //...and end the event if necessary
                            if((command.Properties & CommandProperties.EndsEvent) != 0)
                                mode = ParserState.OutsideEvent;
                        }
                        for (int j = command.Arguments.Count - 1; j >= 0; j--)
                            args.Push(command.Arguments[j]);
                    }
                    yield return new TSCCommandToken(
                            ArgumentEncoding.GetString(cmdData.ToArray()),
                            command != null ? TSCTokenValidity.Valid : TSCTokenValidity.Error,
                            command);
                }
                else if (mode == ParserState.EventBody && stream.CheckBytes(TextNewline))
                {
                    var newlineData = new List<byte>(TextNewline.Length + TextNewLineExtraAdvance);
                    newlineData.AddRange(TextNewline);
                    newlineData.AddRange(stream.ReadUntilLengthOrSequences(TextNewLineExtraAdvance, EventStart));
                    yield return new TSCTextNewLineToken(TextEncoding.GetString(newlineData.ToArray()));
                }
                //add text (covers remaining cases)
                else
                {
                    byte[] text;
                    TSCTextTypes type;
                    switch (mode)
                    {
                        case ParserState.EventDefinition:
                            text = stream.ReadUntilSequences(EventStart, EventEnd);
                            type = TSCTextTypes.Ignored;
                            break;
                        case ParserState.EventBody:
                            text = stream.ReadUntilSequences(EventStart, TextNewline, CommandStart);
                            type = TSCTextTypes.Printed;
                            break;
                        case ParserState.OutsideEvent:
                            text = stream.ReadUntilSequencesOrNewline(EventStart);
                            type = TSCTextTypes.Ignored;
                            break;
                        default:
                            throw new InvalidOperationException("Invalid TSC state: " + mode);
                    }
                    yield return new TSCTextToken(TextEncoding.GetString(text), type);
                }
            }
        }

        /// <summary>
        /// Loads the file from the given path into the editor
        /// </summary>
        /// <param name="path"></param>
        /// <param name="encrypted"></param>
        protected void LoadFile(string path, bool encrypted)
        {
            throw new NotImplementedException();
        }
       

        public void Save(string path)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                Save(fs);
        }
        public void Save(Stream stream)
        {
            if(Lines.Count <= 0)
                return;
            foreach (var line in Lines)
            {
                foreach (var tok in line)
                {
                    byte[] data = GetBytes(tok);
                    stream.Write(data, 0, data.Length);
                }
            }
        }

        /// <summary>
        /// Warning: This method is slow, read the Token list directly for editor display
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return string.Join("", Lines.SelectMany(x => x.Select(y => y.Text)));
        }
    }
}

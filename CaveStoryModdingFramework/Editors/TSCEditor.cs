using CaveStoryModdingFramework.TSC;
using CaveStoryModdingFramework.Utilities;
using System;
using System.Collections.Generic;
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

            while (stream.Position < stream.Length)
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
        /// The token is technically correct, but could have been made in error
        /// </summary>
        Warning,
        /// <summary>
        /// The token will crash the game when encountered
        /// </summary>
        Error,
        /// <summary>
        /// The token is malformed and cannot be understood
        /// </summary>
        //Critical //pray that this never has to be uncommented
    }
    public class TokenList
    {
        public List<TSCToken> Tokens { get; }

        public TokenList()
        {
            Tokens = new List<TSCToken>();
        }

        public TSCToken this[int index] { get => Tokens[index]; set => Tokens[index] = value; }
        public int Count => Tokens.Count;
    }
    public abstract class TSCToken
    {
        protected LinkedListNode<byte> Content { get; }
        protected int LengthBytes { get; }
        protected int LengthChars { get; }
        protected Encoding Encoding { get; }
        public string GetString()
        {
            return Encoding.GetString(Content.Read(LengthBytes));
        }

        public TSCTokenValidity Validity { get; set; }

        protected TSCToken(LinkedListNode<byte> node, int length, Encoding encoding, TSCTokenValidity validity)
        {
            Content = node;
            LengthBytes = length;
            Encoding = encoding;
            Validity = validity;
        }
    }

    public class TSCEventToken : TSCToken
    {
        public int Value { get; }
        public TSCEventToken(LinkedListNode<byte> node, int length, Encoding encoding, int value, TSCTokenValidity validity)
            : base(node,length, encoding, validity)
        {
            Value = value;
        }
    }

    public enum TSCTextTypes
    {
        /// <summary>
        /// Text that is impossible for the parser to reach
        /// </summary>
        Comment,
        /// <summary>
        /// Text that could be seen in a message box if one was open
        /// </summary>
        Text,
        /// <summary>
        /// Text that the parser will actively ignore
        /// </summary>
        Ignore,
    }

    public class TSCTextToken : TSCToken
    {
        public TSCTextTypes TextType { get; }

        public TSCTextToken(LinkedListNode<byte> node, int length, Encoding encoding, TSCTextTypes textType, TSCTokenValidity validity = TSCTokenValidity.Valid)
            : base(node, length, encoding, validity)
        {
            TextType = textType;
        }
    }

    public class TSCCommandToken : TSCToken
    {
        public Command? Command { get; }

        public TSCCommandToken(LinkedListNode<byte> node, int length, Encoding encoding, Command? command, TSCTokenValidity validity = TSCTokenValidity.Valid)
            : base(node, length, encoding, validity)
        {
            Command = command;
        }
    }

    public class TSCArgumentToken : TSCToken
    {
        public Argument Argument { get; }

        public int GetValue()
        {
            //TODO support custom arg types
            return Content.PeekTSCNum();
        }

        public TSCArgumentToken(LinkedListNode<byte> node, int length, Encoding encoding, Argument argument, TSCTokenValidity validity = TSCTokenValidity.Valid)
            : base(node, length, encoding, validity)
        {
            Argument = argument;
        }
    }

    #endregion

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
        /// <summary>
        /// What byte sequence denotes the start of an event line
        /// </summary>
        public byte[] EventStart = new byte[] { (byte)'#' };
        /// <summary>
        /// What byte sequence denotes the end of an event line
        /// </summary>
        public byte[] EventEnd   = new byte[] { (byte)'\n' };
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
        /// Used for all TSC arguments (so that escaped bytes work)
        /// </summary>
        Encoding ArgumentEncoding;
        /// <summary>
        /// Used for all TSC text/comments (so languages display properly)
        /// </summary>
        public Encoding TextEncoding;

        /// <summary>
        /// The commands this parser will recognize
        /// </summary>
        public List<Command> Commands;

        /// <summary>
        /// The actual bytes of this TSC file
        /// </summary>
        LinkedList<byte> TSCbuffer = new LinkedList<byte>();
        /// <summary>
        /// The lines of tokens that make up this file
        /// </summary>
        public List<List<TSCToken>> Tokens { get; } = new List<List<TSCToken>>();

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
            LoadData(input, encrypted);
            Parse(0);
        }

        /// <summary>
        /// Loads the file from the given path into the editor
        /// </summary>
        /// <param name="path"></param>
        /// <param name="encrypted"></param>
        protected void LoadFile(string path, bool encrypted)
        {
            LoadData(File.ReadAllBytes(path), encrypted);
        }
        /// <summary>
        /// Loads the data from the given path into the editor
        /// </summary>
        /// <param name="input"></param>
        /// <param name="encrypted"></param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        protected void LoadData(byte[] input, bool encrypted)
        {
            int AppendToBuffer(string text)
            {
                foreach (var c in text)
                    TSCbuffer.AddLast((byte)c);
                return text.Length;
            }

            using var data = new MemoryStream();
            data.Write(input, 0, input.Length);
            data.Seek(0, SeekOrigin.Begin);
            if (encrypted)
                Encryptor.DecryptInPlace(data);

            //TODO very few command use repeats at all, but this system wouldn't support nested or local repeats
            var argValues = new List<int>();
            var storeArgValues = false;

            var args = new Stack<object>();

            ParserTextMode mode = ParserTextMode.OutsideEvent;
            while(data.Position < data.Length)
            {
                if (data.CheckBytes(EventStart))
                {
                    mode = ParserTextMode.EventDefinition;
                    argValues.Clear();
                    storeArgValues = false;
                    
                    //start working on the data
                    var eventData = new List<byte>(EventStart.Length + EventLength);
                    eventData.AddRange(EventStart);

                    //these bytes are free
                    for (int i = 0; data.Position < data.Length && i < EventAdvance; i++)
                        eventData.Add((byte)data.ReadByte());
                    
                    //add the rest if possible
                    if(data.Position < data.Length)
                        eventData.AddRange(data.ReadUntilLengthOrSequences(EventLength - EventAdvance, EventStart, EventEnd));

                    AppendToBuffer(ArgumentEncoding.GetString(eventData.ToArray()));
                }
                else if (data.CheckBytes(EventEnd))
                {
                    mode = ParserTextMode.EventBody;
                    AppendToBuffer(TextEncoding.GetString(EventEnd));
                }
                else if(args.Count > 0)
                {
                    var dequeuedArg = args.Pop();
                    if(dequeuedArg is Argument arg)
                    {
                        List<byte> argData;
                        if(arg.Length > 0)
                        {
                            argData = new List<byte>(arg.Length + arg.Separator.Length);
                            argData.AddRange(data.ReadUntilLengthOrSequences(arg.Length, EventStart));
                        }
                        else
                        {
                            var sepBytes = ArgumentEncoding.GetBytes(arg.Separator);
                            argData = new List<byte>();
                            argData.AddRange(data.ReadUntilSequences(sepBytes, EventStart));                            
                        }
                        if (storeArgValues)
                            argValues.Add(argData.GetTSCNum());
                        if (arg.Length <= 0 || args.Count > 0)
                            argData.AddRange(data.ReadUntilLengthOrSequences(arg.Separator.Length, EventStart));
                        AppendToBuffer(ArgumentEncoding.GetString(argData.ToArray()));
                    }
                    else if(dequeuedArg is RepeatStructure repeat)
                    {
                        for(int i = 0; i < argValues[repeat.Value]; i++)
                        {
                            for(int j = repeat.Arguments.Count - 1; j >= 0; j--)
                                args.Push(repeat.Arguments[j]);
                        }
                    }
                    else
                        throw new ArgumentException("Invalid TSC argument type! " + dequeuedArg.GetType(), nameof(dequeuedArg));
                }
                //add command (in event)
                else if (mode == ParserTextMode.EventBody && data.CheckBytes(CommandStart))
                {
                    var cmdData = new List<byte>();
                    cmdData.AddRange(CommandStart);
                    foreach (var command in Commands)
                    {
                        var cmdNameBytes = ArgumentEncoding.GetBytes(command.ShortName);
                        if (data.CheckBytes(cmdNameBytes))
                        {
                            cmdData.AddRange(cmdNameBytes);

                            storeArgValues = command.UsesRepeats;

                            for (int j = command.Arguments.Count - 1; j >= 0; j--)
                                args.Push(command.Arguments[j]);
                            break;
                        }
                    }
                    AppendToBuffer(ArgumentEncoding.GetString(cmdData.ToArray()));
                }
                else if (mode == ParserTextMode.EventBody && data.CheckBytes(TextNewline))
                {
                    var newlineData = new List<byte>(TextNewline.Length + TextNewLineExtraAdvance);
                    newlineData.AddRange(TextNewline);
                    newlineData.AddRange(data.ReadUntilLengthOrSequences(TextNewLineExtraAdvance, EventStart));
                    AppendToBuffer(TextEncoding.GetString(newlineData.ToArray()));
                }
                //add text (covers remaining cases)
                else
                {
                    byte[] text;
                    switch (mode)
                    {
                        case ParserTextMode.EventDefinition:
                            text = data.ReadUntilSequences(EventStart, EventEnd);
                            break;
                        case ParserTextMode.EventBody:
                            text = data.ReadUntilSequences(EventStart, TextNewline, CommandStart);
                            break;
                        case ParserTextMode.OutsideEvent:
                            text = data.ReadUntilSequences(EventStart);
                            break;
                        default:
                            throw new InvalidOperationException("Invalid TSC state: " + mode);
                    }
                    TSCbuffer.AddLast(text);
                    //AppendToBuffer(TextEncoding.GetString(text));
                }
            }
        }
        protected void AddTokenLine(ref int index, List<TSCToken> tokens)
        {
            Tokens.Insert(index, tokens);
            index++;
        }
        enum ParserTextMode
        {
            EventDefinition,
            EventBody,
            OutsideEvent
        }
        protected void Parse(int lineIndex)
        {
            if (lineIndex == Tokens.Count)
                Tokens.Add(new List<TSCToken>());
            else if(lineIndex > Tokens.Count)
                throw new IndexOutOfRangeException("Invalid line to parse!");

            var workingOffset = TSCbuffer.First;
            var args = new Stack<object>();

            ParserTextMode mode = ParserTextMode.OutsideEvent;
            var advanceOk = workingOffset != null;
            while (advanceOk)
            {
                List<byte> data;
                var currentTokenOffset = workingOffset;
                if (LocalExtensions.CheckAndReadSequence(ref workingOffset!, out advanceOk, out data, EventStart))
                {
                    mode = ParserTextMode.EventDefinition;
                    args.Clear();
                    //grab the value of this event
                    var num = workingOffset.PeekTSCNum();

                    //these bytes are free
                    for (int i = 0; advanceOk && i < EventAdvance; i++)
                    {
                        advanceOk = LocalExtensions.ReadProcessingEscapes(ref workingOffset, out var b);
                        data.Add(b);
                    }
                    if (advanceOk)
                    {
                        data.AddRange(LocalExtensions.ReadUntilLengthOrSequences(ref workingOffset, out advanceOk,
                            EventLength - EventAdvance, EventStart, EventEnd));
                    }
                    
                    Tokens[lineIndex].Add(new TSCEventToken(currentTokenOffset,
                        Encoding.ASCII.GetBytes(ArgumentEncoding.GetString(data.ToArray())).Length,
                        ArgumentEncoding, num,
                        data.Count == EventStart.Length + EventLength ? TSCTokenValidity.Valid : TSCTokenValidity.Warning));
                }
                else if (LocalExtensions.CheckAndReadSequence(ref workingOffset, out advanceOk, out data, EventEnd))
                {
                    mode = ParserTextMode.EventBody;
                    Tokens[lineIndex++].Add(new TSCTextToken(currentTokenOffset, EventEnd.Length, TextEncoding, TSCTextTypes.Ignore));
                    Tokens.Insert(lineIndex, new List<TSCToken>());
                }
                else if(args.Count > 0)
                {
                    var dequeuedArg = args.Pop();
                    if(dequeuedArg is Argument arg)
                    {
                        List<byte> argData;
                        if (arg.Length > 0)
                        {
                            argData = new List<byte>(arg.Length + arg.Separator.Length);
                            argData.AddRange(LocalExtensions.ReadUntilLengthOrSequences(ref workingOffset, out advanceOk, arg.Length, EventStart));
                        }
                        else
                        {
                            var sepBytes = ArgumentEncoding.GetBytes(arg.Separator);
                            argData = new List<byte>();
                            argData.AddRange(LocalExtensions.ReadUntilSequences(ref workingOffset, out advanceOk, sepBytes, EventStart));
                        }
                        if (advanceOk && (arg.Length <= 0 || args.Count > 0))
                            argData.AddRange(LocalExtensions.ReadUntilLengthOrSequences(ref workingOffset, out advanceOk, arg.Separator.Length, EventStart));
                        Tokens[lineIndex].Add(new TSCArgumentToken(currentTokenOffset,
                            ArgumentEncoding.GetString(argData.ToArray()).Length,
                            ArgumentEncoding, arg));
                    }
                    else if (dequeuedArg is RepeatStructure repeat)
                    {
                        int i = Tokens[lineIndex].Count - 1;
                        while (!(Tokens[lineIndex][i] is TSCCommandToken))
                            i--;
                        i += 1 + repeat.Value;
                        if (!(Tokens[lineIndex][i] is TSCArgumentToken))
                            throw new ArgumentException("Something went wrong when getting the repeat index!");
                        var repeatCount = ((TSCArgumentToken)Tokens[lineIndex][i]).GetValue();
                        for (i = 0; i < repeatCount; i++)
                        {
                            for (int j = repeat.Arguments.Count - 1; j >= 0; j--)
                                args.Push(repeat.Arguments[j]);
                        }
                    }
                    else
                        throw new ArgumentException("Invalid TSC argument type! " + dequeuedArg.GetType(), nameof(dequeuedArg));
                }
                //add command (in event)
                else if (mode == ParserTextMode.EventBody
                    && LocalExtensions.CheckAndReadSequence(ref workingOffset, out advanceOk, out data, CommandStart))
                {
                    var cmdData = new List<byte>(data);
                    Command? cmd = null;
                    foreach (var command in Commands)
                    {
                        var cmdNameBytes = ArgumentEncoding.GetBytes(command.ShortName);
                        if (LocalExtensions.CheckAndReadSequence(ref workingOffset, out advanceOk, out data, cmdNameBytes))
                        {
                            cmd = command;
                            cmdData.AddRange(cmdNameBytes);

                            for (int j = command.Arguments.Count - 1; j >= 0; j--)
                                args.Push(command.Arguments[j]);
                            break;
                        }
                    }
                    Tokens[lineIndex].Add(new TSCCommandToken(currentTokenOffset, cmdData.Count, ArgumentEncoding, cmd,
                        //TODO togglable error state
                        cmd != null ? TSCTokenValidity.Valid : TSCTokenValidity.Error));
                }
                else if(mode == ParserTextMode.EventBody
                    && LocalExtensions.CheckAndReadSequence(ref workingOffset, out advanceOk, out data, TextNewline))
                {
                    var expected = TextNewline.Length + TextNewLineExtraAdvance;

                    data.AddRange(LocalExtensions.ReadUntilLengthOrSequences(ref workingOffset, out advanceOk, TextNewLineExtraAdvance, EventStart));

                    Tokens[lineIndex++].Add(new TSCTextToken(currentTokenOffset, data.Count, TextEncoding, TSCTextTypes.Ignore,
                        data.Count == expected ? TSCTokenValidity.Valid : TSCTokenValidity.Error));
                    Tokens.Insert(lineIndex, new List<TSCToken>());
                }
                //add text (covers remaining cases)
                else
                {
                    switch (mode)
                    {
                        case ParserTextMode.EventDefinition:
                            data = LocalExtensions.ReadUntilSequences(ref workingOffset, out advanceOk, EventStart, EventEnd);
                            Tokens[lineIndex].Add(new TSCTextToken(currentTokenOffset, data.Count, TextEncoding, TSCTextTypes.Comment));
                            break;
                        case ParserTextMode.EventBody:
                            data = LocalExtensions.ReadUntilSequences(ref workingOffset, out advanceOk, EventStart, TextNewline, CommandStart);
                            Tokens[lineIndex].Add(new TSCTextToken(currentTokenOffset, data.Count, TextEncoding, TSCTextTypes.Text));
                            break;
                        case ParserTextMode.OutsideEvent:
                            data = LocalExtensions.ReadUntilSequences(ref workingOffset, out advanceOk, EventStart);
                            Tokens[lineIndex].Add(new TSCTextToken(currentTokenOffset, data.Count, TextEncoding, TSCTextTypes.Comment));
                            break;
                    }
                }
            }
        }

        public void Save(string path)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                Save(fs);
        }
        public void Save(Stream stream)
        {
            if(TSCbuffer.Count <= 0)
                return;
            bool readOk;
            var i = TSCbuffer.First;
            do
            {
                readOk = LocalExtensions.ReadProcessingEscapes(ref i, out var data);
                stream.WriteByte(data);
            } while (readOk);
        }

        //Warning: This method is slow, read the Token list directly for editor display
        public override string ToString()
        {
            return string.Join("", Tokens.SelectMany(x => x.Select(y => y.GetString())));
        }
    }
}

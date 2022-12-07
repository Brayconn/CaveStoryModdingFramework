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
                    if (advanceOk)
                        i++;
                    Backup(ref node, i);
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

        public static int PeekTSCNum(this LinkedListNode<byte> node, int length = 4)
        {
            int value = 0;
            for (int i = 0; i < length; i++)
            {
                var nextOk = ReadProcessingEscapes(ref node, out var b);
          
                value += (b - 0x30) * (int)Math.Pow(10, length - 1 - i);
                
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
                value += (arr[i] - 0x30) * (int)Math.Pow(10, arr.Count - 1 - i);
            }
            return value;
        }
        
    }
    #region Tokens

    public enum TSCTokenValidity
    {
        Valid,
        Warning,
        Error,
        //Critical //pray that this never has to be uncommented
    }
    public abstract class TSCToken
    {
        protected LinkedListNode<byte> Content { get; }
        protected int Length { get; }
        protected Encoding Encoding { get; }
        public string GetString()
        {
            return Encoding.GetString(Content.Read(Length));
        }

        public TSCTokenValidity Validity { get; set; }

        protected TSCToken(LinkedListNode<byte> node, int length, Encoding encoding, TSCTokenValidity validity)
        {
            Content = node;
            Length = length;
            Encoding = encoding;
            Validity = validity;
        }
    }

    public class TSCEventToken2 : TSCToken
    {
        public int Value { get; }
        public TSCEventToken2(LinkedListNode<byte> node, int length, Encoding encoding, int value, TSCTokenValidity validity)
            : base(node,length, encoding, validity)
        {
            Value = value;
        }
    }

    public enum TSCTextTypes
    {
        //text that could never be read
        Comment,
        //text that could be seen in a message box if one was open
        Text,
        //text that is in the firing line, but will be ignored
        Ignore,
    }

    public class TSCTextToken2 : TSCToken
    {
        public TSCTextTypes TextType { get; }

        public TSCTextToken2(LinkedListNode<byte> node, int length, Encoding encoding, TSCTextTypes textType, TSCTokenValidity validity = TSCTokenValidity.Valid)
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
    //tsc is loaded from bytes -> NORMALIZED bytes (basically escaped ascii converted back into bytes)
    //input comes in as string -> bytes
    //inserted into linked list
    //tokens around that point are destroyed/recalculated
    //tokens individually return string versions of themselves for display
    public class TSCEditor
    {
        public byte[] EventStart = new byte[] { (byte)'#' };
        public byte[] EventEnd = new byte[] { (byte)'\n' };
        public int EventLength = 4;
        public int EventAdvance = 1;

        public byte[] CommandStart = new byte[] { (byte)'<' };
        
        public byte[] TextNewline = new byte[] { (byte)'\r' };
        public int TextNewLineAdvance = 1;

        /// <summary>
        /// Used for all TSC arguments (so that escaped bytes work)
        /// </summary>
        Encoding ArgumentEncoding;
        /// <summary>
        /// Used for all TSC text/comments (so languages display properly)
        /// </summary>
        public Encoding TextEncoding = EncodingOverrides.EscapedASCII;

        public List<Command> Commands = new List<Command>(CommandList.BaseCommands);

        LinkedList<byte> TSCbuffer = new LinkedList<byte>();
        LinkedListNode<byte>? TSCoffset = null;
        int currentLine = -1;
        public List<List<TSCToken>> Tokens { get; } = new List<List<TSCToken>>();
        
        public TSCEditor(Encoding? textEncoding = null, List<Command>? commands = null)
        {
            ArgumentEncoding = new EscapedASCII()
            {
                DoubleEscapeBytes = false
            };
            if (textEncoding != null)
                TextEncoding = EncodingOverrides.GetEncoding(textEncoding.CodePage);

            if (commands != null)
                Commands = commands;
        }
        public TSCEditor(byte[] input, bool encrypted, Encoding? textEncoding = null, List<Command>? commands = null)
            : this(textEncoding, commands)
        {
            Load(input, encrypted);            
            Parse(0);
        }
        void Load(string path, bool encrypted)
        {
            Load(File.ReadAllBytes(path), encrypted);
        }
        void Load(byte[] input, bool encrypted)
        {
            MemoryStream data = new MemoryStream();
            data.Write(input, 0, input.Length);
            data.Seek(0, SeekOrigin.Begin);
            if (encrypted)
                Encryptor.DecryptInPlace(data);

            //TODO very few command use repeats at all, but this system wouldn't support nested or local repeats
            var argValues = new List<int>();
            var storeArgValues = false;

            var argumentQueue = new Queue<object>();

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

                    AppendToBuffer(EncodingOverrides.EscapedOneCharWideASCII.GetString(eventData.ToArray()));
                }
                else if (data.CheckBytes(EventEnd))
                {
                    mode = ParserTextMode.EventBody;
                    AppendToBuffer(TextEncoding.GetString(EventEnd));
                }
                else if(argumentQueue.Count > 0)
                {
                    var dequeuedArg = argumentQueue.Dequeue();
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
                        if (arg.Length <= 0 || argumentQueue.Count > 0)
                            argData.AddRange(data.ReadUntilLengthOrSequences(arg.Separator.Length, EventStart));
                        AppendToBuffer(EncodingOverrides.EscapedOneCharWideASCII.GetString(argData.ToArray()));
                    }
                    else if(dequeuedArg is RepeatStructure repeat)
                    {
                        for(int i = 0; i < argValues[repeat.Value]; i++)
                        {
                            foreach(var a in repeat.Arguments)
                                argumentQueue.Enqueue(a);
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

                            foreach (var arg in command.Arguments)
                            {
                                argumentQueue.Enqueue(arg);
                            }
                            break;
                        }
                    }
                    AppendToBuffer(ArgumentEncoding.GetString(cmdData.ToArray()));
                }
                else if (mode == ParserTextMode.EventBody && data.CheckBytes(TextNewline))
                {
                    var newlineData = new List<byte>(TextNewline.Length + TextNewLineAdvance);
                    newlineData.AddRange(TextNewline);
                    newlineData.AddRange(data.ReadUntilLengthOrSequences(TextNewLineAdvance, EventStart));
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
        int AppendToBuffer(string text)
        {
            foreach (var c in text)
                TSCbuffer.AddLast((byte)c);
            return text.Length;
        }

        
        void AddTokenLine(ref int index, List<TSCToken> tokens)
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
        private void Parse(int index)
        {
            if (index == Tokens.Count)
                Tokens.Add(new List<TSCToken>());
            else if(index > Tokens.Count)
                throw new IndexOutOfRangeException("Invalid line to parse!");

            TSCoffset = TSCbuffer.First;
            var Arguments = new Queue<object>();

            ParserTextMode mode = ParserTextMode.OutsideEvent;
            var advanceOk = TSCoffset != null;
            while (advanceOk)
            {
                List<byte> data;
                var current = TSCoffset;
                if (LocalExtensions.CheckAndReadSequence(ref TSCoffset!, out advanceOk, out data, EventStart))
                {
                    mode = ParserTextMode.EventDefinition;
                    Arguments.Clear();
                    //grab the value of this event
                    var num = TSCoffset.PeekTSCNum();

                    //these bytes are free
                    for (int i = 0; advanceOk && i < EventAdvance; i++)
                    {
                        advanceOk = LocalExtensions.ReadProcessingEscapes(ref TSCoffset, out var b);
                        data.Add(b);
                    }
                    if (advanceOk)
                    {
                        data.AddRange(LocalExtensions.ReadUntilLengthOrSequences(ref TSCoffset, out advanceOk,
                            EventLength - EventAdvance, EventStart, EventEnd));
                    }
                    
                    Tokens[index].Add(new TSCEventToken2(current,
                        Encoding.ASCII.GetBytes(ArgumentEncoding.GetString(data.ToArray())).Length,
                        ArgumentEncoding, num,
                        data.Count == EventStart.Length + EventLength ? TSCTokenValidity.Valid : TSCTokenValidity.Warning));
                }
                else if (LocalExtensions.CheckAndReadSequence(ref TSCoffset, out advanceOk, out data, EventEnd))
                {
                    mode = ParserTextMode.EventBody;
                    Tokens[index++].Add(new TSCTextToken2(current, EventEnd.Length, TextEncoding, TSCTextTypes.Ignore));
                    Tokens.Insert(index, new List<TSCToken>());
                }
                else if(Arguments.Count > 0)
                {
                    var dequeuedArg = Arguments.Dequeue();
                    if(dequeuedArg is Argument arg)
                    {
                        List<byte> argData;
                        if (arg.Length > 0)
                        {
                            argData = new List<byte>(arg.Length + arg.Separator.Length);
                            argData.AddRange(LocalExtensions.ReadUntilLengthOrSequences(ref TSCoffset, out advanceOk, arg.Length, EventStart));
                        }
                        else
                        {
                            var sepBytes = ArgumentEncoding.GetBytes(arg.Separator);
                            argData = new List<byte>();
                            argData.AddRange(LocalExtensions.ReadUntilSequences(ref TSCoffset, out advanceOk, sepBytes, EventStart));
                        }
                        if (advanceOk && (arg.Length <= 0 || Arguments.Count > 0))
                            argData.AddRange(LocalExtensions.ReadUntilLengthOrSequences(ref TSCoffset, out advanceOk, arg.Separator.Length, EventStart));
                        Tokens[index].Add(new TSCArgumentToken(current,
                            EncodingOverrides.EscapedOneCharWideASCII.GetString(argData.ToArray()).Length,
                            ArgumentEncoding, arg));
                    }
                    else if (dequeuedArg is RepeatStructure repeat)
                    {
                        int i = Tokens[index].Count - 1;
                        while (!(Tokens[index][i] is TSCCommandToken))
                            i--;
                        i += 1 + repeat.Value;
                        if (!(Tokens[index][i] is TSCArgumentToken))
                            throw new ArgumentException("Something went wrong when getting the repeat index!");
                        var repeatCount = ((TSCArgumentToken)Tokens[index][i]).GetValue();
                        for (i = 0; i < repeatCount; i++)
                        {
                            foreach (var a in repeat.Arguments)
                                Arguments.Enqueue(a);
                        }
                    }
                    else
                        throw new ArgumentException("Invalid TSC argument type! " + dequeuedArg.GetType(), nameof(dequeuedArg));
                }
                //add command (in event)
                else if (mode == ParserTextMode.EventBody
                    && LocalExtensions.CheckAndReadSequence(ref TSCoffset, out advanceOk, out data, CommandStart))
                {
                    var cmdData = new List<byte>(data);
                    Command? cmd = null;
                    foreach (var command in Commands)
                    {
                        var cmdNameBytes = ArgumentEncoding.GetBytes(command.ShortName);
                        if (LocalExtensions.CheckAndReadSequence(ref TSCoffset, out advanceOk, out data, cmdNameBytes))
                        {
                            cmdData.AddRange(cmdNameBytes);

                            foreach (var arg in command.Arguments)
                            {
                                Arguments.Enqueue(arg);
                            }
                            break;
                        }
                    }
                    Tokens[index].Add(new TSCCommandToken(current, cmdData.Count, ArgumentEncoding, cmd,
                        //TODO togglable error state
                        cmd != null ? TSCTokenValidity.Valid : TSCTokenValidity.Error));
                }
                else if(mode == ParserTextMode.EventBody
                    && LocalExtensions.CheckAndReadSequence(ref TSCoffset, out advanceOk, out data, TextNewline))
                {
                    var expected = TextNewline.Length + TextNewLineAdvance;

                    data.AddRange(LocalExtensions.ReadUntilLengthOrSequences(ref TSCoffset, out advanceOk, TextNewLineAdvance, EventStart));

                    Tokens[index++].Add(new TSCTextToken2(current, data.Count, TextEncoding, TSCTextTypes.Ignore,
                        data.Count == expected ? TSCTokenValidity.Valid : TSCTokenValidity.Error));
                    Tokens.Insert(index, new List<TSCToken>());
                }
                //add text (covers remaining cases)
                else
                {
                    switch (mode)
                    {
                        case ParserTextMode.EventDefinition:
                            data = LocalExtensions.ReadUntilSequences(ref TSCoffset, out advanceOk, EventStart, EventEnd);
                            Tokens[index].Add(new TSCTextToken2(current, data.Count, TextEncoding, TSCTextTypes.Comment));
                            break;
                        case ParserTextMode.EventBody:
                            data = LocalExtensions.ReadUntilSequences(ref TSCoffset, out advanceOk, EventStart, TextNewline, CommandStart);
                            Tokens[index].Add(new TSCTextToken2(current, data.Count, TextEncoding, TSCTextTypes.Text));
                            break;
                        case ParserTextMode.OutsideEvent:
                            data = LocalExtensions.ReadUntilSequences(ref TSCoffset, out advanceOk, EventStart);
                            Tokens[index].Add(new TSCTextToken2(current, data.Count, TextEncoding, TSCTextTypes.Comment));
                            break;
                    }
                }
            }
        }

        public int SelectionStart { get; private set; } = 0;
        public int SelectionEnd { get; private set; } = 0;
        public void StartSelection(int index)
        {
            SelectionStart = SelectionEnd = index;
        }
        public void MoveSelection(int index)
        {
            SelectionEnd = index;
        }

        public void Save(string path)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                Save(fs);
        }
        public void Save(Stream stream)
        {
            var a = true;
            var o = TSCbuffer.First;
            if (o == null)
                return;
            while (a)
            {
                a = LocalExtensions.ReadProcessingEscapes(ref o, out var b);
                stream.WriteByte(b);
            }
        }

        //Warning: do not use to display the contents of the editor
        public override string ToString()
        {
            return string.Join("", Tokens.SelectMany(x => x.Select(y => y.GetString())));
        }
    }
}

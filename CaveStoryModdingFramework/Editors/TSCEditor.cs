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
        public static byte[] ReadUntilLengthOrSequences(this Stream stream, int length, params IList<byte>[] seqs)
        {
            var buff = new List<byte>(length);
            var indexes = new int[seqs.Length];
            bool anyIndex = false;
            for(int i = 0; anyIndex || i < length; i++)
            {
                anyIndex = false;
                var bi = stream.ReadByte();
                //stop at end of stream
                if (bi == -1)
                    break;
                //otherwise add it to the buffer
                buff.Add((byte)bi);
                for (int j = 0; j < indexes.Length; j++)
                {
                    //then check if that byte contributes to the stop sequence
                    if (buff[buff.Count - 1] == seqs[j][indexes[j]++])
                        anyIndex = true;
                    else
                        indexes[j] = 0;
                    //remove the end sequence and quit
                    if (indexes[j] == seqs[j].Count)
                    {
                        buff.RemoveRange(buff.Count - seqs[j].Count, seqs[j].Count);
                        stream.Position -= seqs[j].Count;
                        return buff.ToArray();
                    }
                }
            }
            return buff.ToArray();
        }
        public static byte[] ReadUntilSequences(this Stream stream, params IList<byte>[] seqs)
        {
            var buff = new List<byte>();
            var indexes = new int[seqs.Length];
            while(stream.Position < stream.Length)
            {
                var bi = stream.ReadByte();
                if (bi == -1)
                    break;

                buff.Add((byte)bi);
                for (int j = 0; j < indexes.Length; j++)
                {
                    //then check if that byte contributes to the stop sequence
                    if (buff[buff.Count - 1] != seqs[j][indexes[j]++])
                        indexes[j] = 0;
                    //remove the end sequence and quit
                    if (indexes[j] == seqs[j].Count)
                    {
                        buff.RemoveRange(buff.Count - seqs[j].Count, seqs[j].Count);
                        stream.Position -= seqs[j].Count;
                        return buff.ToArray();
                    }
                }
            }
            return buff.ToArray();
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
        static T Getvance<T>(ref LinkedListNode<T> node)
        {
            var v = node.Value;
            node = node.Next;
            return v;
        }
        static T Addvance<T>(ref LinkedListNode<T> node, IList<T> list)
        {
            list.Add(node.Value);
            node = node.Next;
            return list[list.Count - 1];
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
            var n = node;
            for(int i = 0; i < length; i++)
            {
                if(Addvance(ref n, buff) == EscapedASCII.EscapeByte)
                {
                    var e = Addvance(ref n, buff);
                    if(e == EscapedASCII.EscapeHexByte)
                    {
                        Addvance(ref n, buff);
                        Addvance(ref n, buff);
                        buff.Capacity += 3;
                    }
                }
            }
            node = n;
            return buff.ToArray();
        }
        /// <summary>
        /// Try and read the next two nodes as a single byte value
        /// </summary>
        /// <param name="node"></param>
        /// <param name="output"></param>
        /// <returns></returns>
        public static bool TryReadEscapedByteDigits(ref LinkedListNode<byte> node, out byte output)
        {
            output = 0xFF;
            var b = new char[2]
            {
                (char)Getvance(ref node),
                (char)Getvance(ref node),
            };
            if (Extensions.IsHexDigit(b[0]) && Extensions.IsHexDigit(b[1]))
            {
                output = Convert.ToByte(new string(b), 16);
                return true;
            }
            else
            {
                Backup(ref node, 2);
                return false;
            }
        }
        /// <summary>
        /// Read one byte starting at the given node, converting escape sequences into their appropriate byte value
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public static byte ReadProcessingEscapes(ref LinkedListNode<byte> node)
        {
            var output = Getvance(ref node);
            if (output == EscapedASCII.EscapeByte)
            {
                var e = Getvance(ref node);
                if (e == EscapedASCII.EscapeHexByte)
                {
                    if (TryReadEscapedByteDigits(ref node, out var b))
                        output = b;
                    else
                        Backup(ref node, 1);
                }
                else
                    output = e;
            }
            return output;
        }

        public static int PeekTSCNum(this LinkedListNode<byte> node, int length = 4)
        {
            int value = 0;
            var n = node;
            for (int i = 0; i < length; i++)
            {
                byte c = Getvance(ref n);
                if(c == EscapedASCII.EscapeByte)
                {
                    switch(Getvance(ref n))
                    {
                        case EscapedASCII.EscapeHexByte:
                            if (TryReadEscapedByteDigits(ref n, out var b))
                                c = b;
                            break;
                        case EscapedASCII.EscapeByte:
                            //oh, it's just the escape byte, continue...
                            break;
                        default:
                            //invalid escape sequence, DON'T USE THESE TWO CHARACTERS
                            continue;
                    }
                }
                value += (c - 0x30) * (int)Math.Pow(10, length - 1 - i);
            }
            return value;
        }

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

        public static List<byte> ReadUntilSequences(ref LinkedListNode<byte> node, params IList<byte>[] seqs)
        {
            var buff = new List<byte>();
            var compares = new List<ComparisonInfo>(seqs.Length);

            for(int i = 0; node != null; i++)
            {
                buff.Add(ReadProcessingEscapes(ref node));

                var stopper = Compare(compares, buff, seqs);
                if(stopper != null)
                {
                    buff.RemoveRange(buff.Count - stopper.Count, stopper.Count);
                    Backup(ref node, stopper.Count);
                    return buff;
                }
            }
            return buff;
        }
        public static byte[] ReadUntilLengthOrSequences(ref LinkedListNode<byte> node, int length, params IList<byte>[] seqs)
        {
            var buff = new List<byte>(length);
            var compares = new List<ComparisonInfo>(seqs.Length);

            for (int i = 0; compares.Count > 0 || i < length; i++)
            {
                buff.Add(ReadProcessingEscapes(ref node));

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
        public static bool CheckAndReadSequence(ref LinkedListNode<byte> node, out List<byte> buff, IList<byte> seq)
        {
            buff = new List<byte>(seq.Count);
            for(int i = 0; i < seq.Count; i++)
            {
                buff.Add(ReadProcessingEscapes(ref node));
                if(buff[i] != seq[i])
                {
                    Backup(ref node, i + 1);
                    return false;
                }
            }
            return true;
        }

        public static bool StartsWith<T>(this IList<T> list1, IList<T> list2) where T : IComparable
        {
            for (int i = 0; i < list2.Count; i++)
                if (list1[i].CompareTo(list2[i]) != 0)
                    return false;
            return true;
        }

        public static int Backup<T>(ref LinkedListNode<T>? n, int amount)
        {
            int i;
            for (i = 0; n != null && i < amount; i++)
                n = n.Previous;
            return i;
        }
        public static int Advance<T>(ref LinkedListNode<T>? n, int amount)
        {
            int i;
            for (i = 0; n != null && i < amount; i++)
                n = n.Next;
            return i;
        }
    }
    #region TEMP
    
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

    public class TSCTextToken2 : TSCToken
    {
        public TSCTextTypes TextType { get; }

        public TSCTextToken2(LinkedListNode<byte> node, int length, Encoding encoding, TSCTextTypes textType, TSCTokenValidity validity = TSCTokenValidity.Valid)
            : base(node, length, encoding, validity)
        {
            TextType = textType;
        }
    }




    public interface ITSCToken
    {
        TSCTokenValidity Validity { get; set; }
        Encoding Encoding { get; }
        string Text { get; }
        byte[] ToBytes();
    }
    public class TSCEventToken : ITSCToken
    {
        public TSCTokenValidity Validity { get; set; }
        public int Value { get; }
        public Encoding Encoding { get; }
        public string Text { get; }

        public TSCEventToken(IList<byte> text, int value, Encoding encoding, TSCTokenValidity validity)
        {
            Encoding = encoding;
            Text = Encoding.GetString(text.ToArray());
            Value = value;
            Validity = validity;
        }

        public byte[] ToBytes()
        {
            return Encoding.GetBytes(Text);
        }
    }
    public class TSCCommandToken : ITSCToken
    {
        public TSCTokenValidity Validity { get; set; }
        Command Command { get; set; }
        public Encoding Encoding { get; }
        public string Text { get; private set; }

        public byte[] ToBytes()
        {
            throw new NotImplementedException();
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
    public class TSCTextToken : ITSCToken
    {
        public TSCTokenValidity Validity { get; set; }
        public TSCTextTypes TextType { get; }
        public Encoding Encoding { get; }
        public string Text { get; }
        public TSCTextToken(byte[] data, Encoding encoding, TSCTextTypes textType, TSCTokenValidity validity = TSCTokenValidity.Valid)
        {
            Encoding = encoding;
            Text = Encoding.GetString(data);
            TextType = textType;
            Validity = validity;
        }
        public byte[] ToBytes()
        {
            return Encoding.GetBytes(Text);
        }
    }
    public class TSCOverlapToken
    {

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
        Encoding ArgumentEncoding = EncodingOverrides.EscapedOneCharWideASCII;
        /// <summary>
        /// Used for all TSC text/comments (so languages display properly)
        /// </summary>
        public Encoding TextEncoding = EncodingOverrides.EscapedASCII;

        LinkedList<byte> TSCbuffer = new LinkedList<byte>();
        LinkedListNode<byte>? TSCoffset = null;
        int currentLine = -1;
        public List<List<TSCToken>> Tokens { get; } = new List<List<TSCToken>>();
        
        public TSCEditor(byte[] input, bool encrypted, Encoding? textEncoding = null)
        {
            if (textEncoding != null)
                TextEncoding = EncodingOverrides.GetEncoding(textEncoding.CodePage);
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

            ParserTextMode mode = ParserTextMode.OutsideEvent;
            while(data.Position < data.Length)
            {
                if (data.CheckBytes(EventStart))
                {
                    mode = ParserTextMode.EventDefinition;
                    
                    //start working on the data
                    var eventData = new List<byte>(EventStart.Length + EventLength);
                    eventData.AddRange(EventStart);

                    //these bytes are free
                    for (int i = 0; i < EventAdvance; i++)
                        eventData.Add((byte)data.ReadByte());

                    //add the rest if possible
                    eventData.AddRange(data.ReadUntilLengthOrSequences(EventLength - EventAdvance, EventStart, EventEnd));

                    AppendToBuffer(ArgumentEncoding.GetString(eventData.ToArray()));
                }
                else if (data.CheckBytes(EventEnd))
                {
                    mode = ParserTextMode.EventBody;
                    AppendToBuffer(TextEncoding.GetString(EventEnd));
                }
                //add command (in event)
                else if (mode == ParserTextMode.EventBody && data.CheckBytes(CommandStart))
                {
                    //TODO TEMP
                    var cmdData = new List<byte>();
                    cmdData.AddRange(CommandStart);
                    cmdData.AddRange(data.ReadUntilLengthOrSequences(3));
                    AppendToBuffer(TextEncoding.GetString(cmdData.ToArray()));
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
                    AppendToBuffer(TextEncoding.GetString(text));
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
            Queue<Argument> Arguments = new Queue<Argument>();

            ParserTextMode mode = ParserTextMode.OutsideEvent;
            while (TSCoffset != null)
            {
                List<byte> data;
                var current = TSCoffset;
                if (LocalExtensions.CheckAndReadSequence(ref TSCoffset, out data, EventStart))
                {
                    mode = ParserTextMode.EventDefinition;
                    //grab the value of this event
                    var num = TSCoffset.PeekTSCNum();

                    //these bytes are free
                    for (int i = 0; i < EventAdvance; i++)
                        data.Add(LocalExtensions.ReadProcessingEscapes(ref TSCoffset));
                    
                    //add the rest if possible
                    data.AddRange(LocalExtensions.ReadUntilLengthOrSequences(ref TSCoffset, EventLength - EventAdvance,
                        EventStart, EventEnd));

                    //now we FOR SURE have a valid event token
                    Tokens[index].Add(new TSCEventToken2(current, data.Count, ArgumentEncoding, num,
                        data.Count == EventStart.Length + EventLength ? TSCTokenValidity.Valid : TSCTokenValidity.Warning));
                }
                else if (LocalExtensions.CheckAndReadSequence(ref TSCoffset, out data, EventEnd))
                {
                    mode = ParserTextMode.EventBody;
                    Tokens[index++].Add(new TSCTextToken2(current, EventEnd.Length, TextEncoding, TSCTextTypes.Ignore));
                    Tokens.Insert(index, new List<TSCToken>());
                }
                //add command (in event)
                else if (mode == ParserTextMode.EventBody
                    && LocalExtensions.CheckAndReadSequence(ref TSCoffset, out data, CommandStart))
                {
                    //TODO TEMP
                    data.AddRange(LocalExtensions.ReadRawLength(ref TSCoffset, 3));
                    Tokens[index].Add(new TSCTextToken2(current, data.Count, TextEncoding, TSCTextTypes.Text));
                }
                else if(mode == ParserTextMode.EventBody
                    && LocalExtensions.CheckAndReadSequence(ref TSCoffset, out data, TextNewline))
                {
                    var expected = TextNewline.Length + TextNewLineAdvance;

                    data.AddRange(LocalExtensions.ReadUntilLengthOrSequences(ref TSCoffset, TextNewLineAdvance, EventStart));

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
                            data = LocalExtensions.ReadUntilSequences(ref TSCoffset, EventStart, EventEnd);
                            Tokens[index].Add(new TSCTextToken2(current, data.Count, TextEncoding, TSCTextTypes.Comment));
                            break;
                        case ParserTextMode.EventBody:
                            data = LocalExtensions.ReadUntilSequences(ref TSCoffset, EventStart, TextNewline, CommandStart);
                            Tokens[index].Add(new TSCTextToken2(current, data.Count, TextEncoding, TSCTextTypes.Text));
                            break;
                        case ParserTextMode.OutsideEvent:
                            data = LocalExtensions.ReadUntilSequences(ref TSCoffset, EventStart);
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
            foreach (var line in Tokens)
            {
                foreach (var token in line)
                {
                    var td = new[] { (byte)0 };
                    stream.Write(td, 0, td.Length);
                }
            }
        }

        //Warning: do not use to display the contents of the editor
        public override string ToString()
        {
            return string.Join("", Tokens.SelectMany(x => x.Select(y => y.GetString())));
        }
    }
}

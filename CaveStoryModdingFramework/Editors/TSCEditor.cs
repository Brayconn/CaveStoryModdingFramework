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
        public static int ReadTSCNum(this Stream stream, int length = 4)
        {
            int value = 0;
            for(int i = 0; i < length; i++)
            {
                value += (((byte)stream.ReadByte()) - 0x30) * (int)Math.Pow(10, length - 1 - i);
            }
            stream.Position -= length;
            return value;
        }
        public static byte[] ReadUntilLengthOrSequences(this Stream stream, int length, params byte[][] seqs)
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
                    if (indexes[j] == seqs[j].Length)
                    {
                        buff.RemoveRange(buff.Count - seqs[j].Length, seqs[j].Length);
                        stream.Position -= seqs[j].Length;
                        return buff.ToArray();
                    }
                }
            }
            return buff.ToArray();
        }
        public static byte[] ReadUntilSequences(this Stream stream, params byte[][] seqs)
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
                    if (indexes[j] == seqs[j].Length)
                    {
                        buff.RemoveRange(buff.Count - seqs[j].Length, seqs[j].Length);
                        stream.Position -= seqs[j].Length;
                        return buff.ToArray();
                    }
                }
            }
            return buff.ToArray();
        }
    }

    public enum TSCTokenValidity
    {
        Valid,
        Warning,
        Error,
        //Critical //pray that this never has to be uncommented
    }
    //each token should have
    // parsed data (#0200 should store 200 AND "0200")
    //
    public abstract class IDEA
    {
        public TSCTokenValidity Validity { get; protected set; }
        public string Text { get; protected set; }

        protected Encoding Encoding { get; set; }

        public IDEA(byte[] data, Encoding encoding, TSCTokenValidity validity)
        {
            Text = Encoding.GetString(data);
            Encoding = encoding;
            Validity = validity;
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


    //input goes in as string
    //  gets converted to bytes
    //  processed
    //  turned back into string as the parser parses
    //returns as string
    public class TSCEditor
    {
        public byte[] EventStart = new byte[] { (byte)'#' };
        public byte[] EventEnd = new byte[] { (byte)'\n' };
        public int EventLength = 4;
        public int EventAdvance = 1;

        public byte[] CommandStart = new byte[] { (byte)'<' };
        public Encoding TextEncoding = EncodingOverrides.EscapedASCII;

        public byte[] TextNewline = new byte[] { (byte)'\r' };
        public int TextNewLineAdvance = 1;

        Encoding ArgumentEncoding = EncodingOverrides.EscapedOneCharWideASCII;

        public List<List<ITSCToken>> Tokens { get; } = new List<List<ITSCToken>>();
        
        public TSCEditor(byte[] input, bool encrypted, Encoding textEncoding = null)
        {
            var data = new byte[input.Length];
            input.CopyTo(data, 0);
            if (encrypted)
                Encryptor.DecryptInPlace(data);
            WorkingBuffer.Write(data, 0, data.Length);
            WorkingBuffer.Position -= data.Length;

            if (textEncoding != null)
                TextEncoding = EncodingOverrides.GetEncoding(textEncoding.CodePage);

            Parse(0);
        }

        MemoryStream WorkingBuffer = new MemoryStream();
        /// <summary>
        /// Empties the line at the specified index and puts the byte contents into the working buffer
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        private bool DeconstructLine(int index)
        {
            if (index < 0 || index >= Tokens.Count)
                return false;
            foreach (var token in Tokens[index])
            {
                var tokenData = token.ToBytes();
                WorkingBuffer.Write(tokenData, (int)WorkingBuffer.Length, tokenData.Length);
                WorkingBuffer.Position -= tokenData.Length;
            }
            Tokens.RemoveAt(index);
            return true;
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
                Tokens.Add(new List<ITSCToken>());
            else if (index < Tokens.Count)
                DeconstructLine(index);
            else
                throw new IndexOutOfRangeException("Invalid line to parse!");
            
            Queue<Argument> Arguments = new Queue<Argument>();

            ParserTextMode mode = ParserTextMode.OutsideEvent;
            while (WorkingBuffer.Position < WorkingBuffer.Length)
            {
                if (WorkingBuffer.Position >= WorkingBuffer.Length)
                    DeconstructLine(index);

                if (WorkingBuffer.CheckBytes(EventStart))
                {
                    mode = ParserTextMode.EventDefinition;
                    //grab the value of this event
                    var num = WorkingBuffer.ReadTSCNum();

                    //start working on the data
                    var data = new List<byte>(EventLength);
                    data.AddRange(EventStart);

                    //these bytes are free
                    for (int i = 0; i < EventAdvance; i++)
                        data.Add((byte)WorkingBuffer.ReadByte());
                    
                    //add the rest if possible
                    data.AddRange(WorkingBuffer.ReadUntilLengthOrSequences(EventLength-EventAdvance, EventStart, EventEnd));

                    //now we FOR SURE have a valid event token
                    Tokens[index].Add(new TSCEventToken(data, num, ArgumentEncoding,
                        data.Count == EventStart.Length + EventLength ? TSCTokenValidity.Valid : TSCTokenValidity.Warning));
                }
                else if (WorkingBuffer.CheckBytes(EventEnd))
                {
                    mode = ParserTextMode.EventBody;
                    Tokens[index++].Add(new TSCTextToken(EventEnd, TextEncoding, TSCTextTypes.Ignore));
                    Tokens.Insert(index, new List<ITSCToken>());
                }
                //add command (in event)
                else if (mode == ParserTextMode.EventBody && WorkingBuffer.CheckBytes(CommandStart))
                {
                    //TODO TEMP
                    var data = new List<byte>();
                    data.AddRange(CommandStart);
                    data.AddRange(WorkingBuffer.ReadUntilLengthOrSequences(3));
                    Tokens[index].Add(new TSCTextToken(data.ToArray(), TextEncoding, TSCTextTypes.Text));
                }
                else if(mode == ParserTextMode.EventBody && WorkingBuffer.CheckBytes(TextNewline))
                {
                    var expected = TextNewline.Length + TextNewLineAdvance;
                    var data = new List<byte>(expected);
                    data.AddRange(TextNewline);
                    data.AddRange(WorkingBuffer.ReadUntilLengthOrSequences(TextNewLineAdvance, EventStart));
                    Tokens[index++].Add(new TSCTextToken(data.ToArray(), TextEncoding, TSCTextTypes.Ignore,
                        data.Count == expected ? TSCTokenValidity.Valid : TSCTokenValidity.Error));
                    Tokens.Insert(index, new List<ITSCToken>());
                }
                //add text (covers remaining cases)
                else
                {
                    byte[] data;
                    switch (mode)
                    {
                        case ParserTextMode.EventDefinition:
                            data = WorkingBuffer.ReadUntilSequences(EventStart, EventEnd);
                            Tokens[index].Add(new TSCTextToken(data, TextEncoding, TSCTextTypes.Comment));
                            break;
                        case ParserTextMode.EventBody:
                            data = WorkingBuffer.ReadUntilSequences(EventStart, TextNewline, CommandStart);
                            Tokens[index].Add(new TSCTextToken(data, TextEncoding, TSCTextTypes.Text));
                            break;
                        case ParserTextMode.OutsideEvent:
                            data = WorkingBuffer.ReadUntilSequences(EventStart);
                            Tokens[index].Add(new TSCTextToken(data, TextEncoding, TSCTextTypes.Comment));
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
                    var td = token.ToBytes();
                    stream.Write(td, 0, td.Length);
                }
            }
        }

        //Warning: do not use to display the contents of the editor
        public override string ToString()
        {
            return string.Join("", Tokens.SelectMany(x => x.Select(y => y.Text)));
        }
    }
}

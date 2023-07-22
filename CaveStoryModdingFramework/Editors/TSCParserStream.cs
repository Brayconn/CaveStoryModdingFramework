using CaveStoryModdingFramework.Utilities;
using System;
using System.Collections.Generic;
using System.IO;

namespace CaveStoryModdingFramework.Editors
{

    /// <summary>
    /// Provides a layer of abstraction between the data of a TSC file when parsing
    /// </summary>
    public class TSCParserStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => true;
        /// <summary>
        /// WARNING: this gives the length of the CURRENT buffer, NOT the TOTAL buffer
        /// </summary>
        public override long Length
        {
            get => Data.Length;
        }
        public override long Position
        {
            get => Data.Position;
            set
            {
                //need to fill up the data buffer
                if (value >= Data.Length)
                {
                    AddFromExtra(value);
                }
                //TODO should this throw?
                if (value >= Data.Length)
                    throw new ArgumentOutOfRangeException(nameof(value));
                
                Data.Position = value;
            }
        }

        readonly Func<ITSCToken, byte[]> TokenToBytes;
        readonly LinkedByteStream Data;
        readonly TSCTokenStream ExtraData;

        /// <summary>
        /// Tries to append data from the extra data until there are is at least "value" bytes in the buffer
        /// </summary>
        /// <param name="value"></param>
        void AddFromExtra(long value)
        {
            while (value > Data.Length && ExtraData.TryPop(out ITSCToken token))
            {
                byte[] tokData = TokenToBytes(token);
                Data.Append(tokData, 0, tokData.Length);
            }
        }

        public TSCParserStream(IEnumerable<byte> data, TSCEditor parent)
            : this(data, parent.Stream, parent.GetBytes)
        { }
        public TSCParserStream(IEnumerable<byte> data, TSCTokenStream extraData, Func<ITSCToken, byte[]> tokenToBytes)
        {
            Data = new LinkedByteStream(data);
            TokenToBytes = tokenToBytes;
            ExtraData = extraData;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (Position + count > Data.Length)
                AddFromExtra(Position + count);
            return Data.Read(buffer, offset, count);
        }
        public override int ReadByte()
        {
            if (Position >= Data.Length)
                AddFromExtra(Position);
            return Data.ReadByte();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.End:
                    Position = Data.Length - offset;
                    break;
                case SeekOrigin.Begin:
                    Position = offset;
                    break;
                case SeekOrigin.Current:
                    Position += offset;
                    break;
                default:
                    throw new ArgumentException(nameof(origin));
            }
            return Position;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override void Flush()
        {
            //this stream doesn't use buffering
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace CaveStoryModdingFramework.Utilities
{
    /// <summary>
    /// Provides a Stream interface over a LinkedList. This is an abstract class that only provides seek functionality
    /// </summary>
    /// <typeparam name="T">The type of linked list</typeparam>
    public abstract class LinkedStream<T> : Stream
    {
        public abstract override bool CanRead { get; }
        public abstract override bool CanWrite { get; }

        public override bool CanSeek => true;

        protected LinkedList<T> List;
        public override long Length => List.Count;

        public bool EndOfStream
        {
            get => currentNode == null;
            protected set
            {
                if (value)
                {
                    currentNode = null;
                    position = Length;
                }
                else
                    throw new ArgumentException(nameof(value));
            }
        }
        /// <summary>
        /// Whether or not the stream knows what position it is in the list.
        /// Manually setting CurrentNode will cause this to become false in many cases.
        /// </summary>
        public bool PositionValid
        {
            get => position != -1;
            protected set
            {
                if (!value)
                    position = -1;
                else
                    throw new ArgumentOutOfRangeException(nameof(value));
            }
        }
        protected long position = 0;
        /// <summary>
        /// The current offset into the stream.
        /// Getter is O(1) when PositionValid is true;
        ///   O(Length/2) when PositionValid is false, but CurrentNode is in the stream;
        ///   throws when CurrentNode is not part of the stream.
        /// Setter is O(1) for the start/end/previous/next nodes;
        ///   O(n/2) for most other cases.
        /// </summary>
        public override long Position
        {
            get
            {
                //Use the stored position if possible
                if(PositionValid)
                    return position;
                
                //otherwise, we need to find the current position
                //search from both the start and end
                LinkedListNode<T> start = List.First, end = List.Last;
                long i = 0, j = List.Count - 1;
                while(start != List.Last)
                {
                    if(start == CurrentNode)
                    {
                        return position = i;
                    }
                    else
                    {
                        i++;
                        start = start.Next;
                    }

                    if(end == CurrentNode)
                    {
                        return position = j;
                    }
                    else
                    {
                        j--;
                        end = end.Previous;
                    }
                }

                //the current node must not be part of the list!
                throw new InvalidOperationException();
            }
            set
            {
                //can't go before the stream
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }
                //stay put
                else if (value == position)
                {
                    return;
                }
                //start/end
                else if (value == 0)
                {
                    currentNode = List.First;
                }
                else if (value == Length - 1)
                {
                    currentNode = List.Last;
                }
                //prev/next
                else if (PositionValid && value == position - 1)
                {
                    currentNode = currentNode!.Previous;
                }
                else if (PositionValid && value == position + 1)
                {
                    currentNode = currentNode!.Next;
                }
                //TODO consider effects of this (maybe remove)
                //move to end
                else if(value == Length)
                {
                    currentNode = null;
                }
                //new end
                else if (value > Length)
                {
                    SetLength(value + 1);
                    currentNode = List.Last;
                }
                //somewhere inbetween
                else if (PositionValid && !EndOfStream)
                {
                    if (value < position)
                    {
                        SeekBetween(List.First, 0, currentNode!, position, value);
                    }
                    else //if(value > position) //always true by this point
                    {
                        SeekBetween(currentNode!, position, List.Last, Length - 1, value);
                    }
                }
                else
                {
                    SeekBetween(List.First, 0, List.Last, Length - 1, value);
                }
                
                position = value;
            }
        }
        public override long Seek(long value, SeekOrigin origin)
        {
            switch(origin)
            {
                case SeekOrigin.Begin:
                    return Position = value;

                case SeekOrigin.Current:
                    if (!PositionValid)
                        throw new ArgumentException(nameof(origin));
                    if (value == 0)
                        return Position;
                    value = Position + value;
                    goto case SeekOrigin.Begin;

                case SeekOrigin.End:
                    value -= Length;
                    goto case SeekOrigin.Begin;

                default:
                    throw new ArgumentException(nameof(origin));
            }
        }
        /// <summary>
        /// Move the CurrentNode to the given value, starting from the provided bounds
        /// </summary>
        /// <param name="start"></param>
        /// <param name="startIndex"></param>
        /// <param name="end"></param>
        /// <param name="endIndex"></param>
        /// <param name="value"></param>
        protected void SeekBetween(LinkedListNode<T> start, long startIndex, LinkedListNode<T> end, long endIndex, long value)
        {
            //closer to start
            if (value - startIndex <= endIndex - value)
            {
                currentNode = start;
                for (long i = startIndex; i < value; i++)
                {
                    currentNode = currentNode.Next;
                }
            }
            //closer to end
            else
            {
                currentNode = end;
                for (long i = endIndex; value < i; i--)
                {
                    currentNode = currentNode.Previous;
                }
            }
        }

        protected LinkedListNode<T>? currentNode;
        /// <summary>
        /// Fair warning: setting this to anything other than the first/last/next/previous node will cause the position to invalidate
        /// </summary>
        public LinkedListNode<T>? CurrentNode
        {
            get => currentNode;
            set
            {
                if (Length > 0 && value == null)
                    throw new ArgumentNullException(nameof(value));

                if (value == List.First)
                {
                    position = 0;
                }
                else if(value == List.Last)
                {
                    position = Length - 1;
                }
                else if(!EndOfStream && value == currentNode!.Previous)
                {
                    position--;
                }
                else if(!EndOfStream && value == currentNode!.Next)
                {
                    position++;
                }
                else
                {
                    PositionValid = false;
                }
                currentNode = value;
            }
        }

        protected LinkedStream()
        {
            List = new LinkedList<T>();
        }
        protected LinkedStream(LinkedList<T> list)
        {
            List = list;
        }
        protected LinkedStream(IEnumerable<T> data)
        {
            List = new LinkedList<T>(data);
            if (List.Count > 0)
                currentNode = List.First;
        }

        public override void SetLength(long newLength)
        {
            if(newLength == 0)
            {
                List.Clear();
                EndOfStream = true;
                position = 0;
            }
            //shrink
            if (newLength < Length)
            {
                while (newLength < Length)
                    List.RemoveLast();
            }
            //expand
            else if (Length < newLength)
            {
                while (Length < newLength)
                    List.AddLast(default(T));
            }
        }

        public abstract override int Read(byte[] buffer, int offset, int count);
        public abstract override void Write(byte[] buffer, int offset, int count);
        /// <summary>
        /// When overridden in a derived class, inserts a sequence of bytes to the current stream
        /// and (optionally) advances the stream by the number of bytes inserted.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="follow"></param>
        public abstract void Insert(byte[] buffer, int offset, int count, bool follow);
        /// <summary>
        /// Combines a call to Write and Insert (in that order) into one method.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="overwrite"></param>
        /// <param name="insert"></param>
        /// <param name="follow"></param>
        public void HybridWrite(byte[] buffer, int offset, int overwrite, int insert, bool follow)
        {
            if (overwrite > 0)
            {
                Write(buffer, offset, overwrite);
            }
            if (insert > 0)
            {
                Insert(buffer, offset + overwrite, insert, follow);
            }
        }
        public abstract void Append(byte[] buffer, int offset, int count);
        public override void Flush()
        {
            //This stream type doesn't need to buffer I think...?
        }
    }

    public class LinkedByteStream : LinkedStream<byte>
    {
        public override bool CanRead => true;
        public override bool CanWrite => true;

        public LinkedByteStream() : base() { }
        public LinkedByteStream(LinkedList<byte> list) : base(list) { }
        public LinkedByteStream(IEnumerable<byte> data) : base(data) { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int i;
            for(i = 0; !EndOfStream && i < count; i++)
            {
                buffer[offset + i] = currentNode!.Value;
                currentNode = currentNode.Next;
                position++;
            }
            return i;
        }
        public override int ReadByte()
        {
            if (List.Count <= 0 || EndOfStream)
                return -1;
            var b = currentNode!.Value;
            currentNode = currentNode.Next;
            if(PositionValid)
                position++;
            return b;
        }
        public override void Append(byte[] buffer, int offset, int count)
        {
            if (count <= 0)
                return;

            if(position == List.Count)
            {
                List.AddLast(buffer[offset]);
                currentNode = List.Last;
                offset++;
                count--;
            }

            for (int i = 0; i < count; i++)
                List.AddLast(buffer[offset + i]);
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (count <= 0)
                return;

            int i;
            for(i = 0; !EndOfStream && i < count; i++)
            {
                currentNode!.Value = buffer[offset + i];
                currentNode = currentNode.Next;
                if (PositionValid)
                    position++;
            }
            for(; i < count; i++)
            {
                List.AddLast(buffer[offset + i]);
            }
            if (EndOfStream)
                position = Length;
        }
        public override void WriteByte(byte value)
        {
            if (EndOfStream)
            {
                List.AddLast(value);
                position = Length;
            }
            else
            {
                currentNode!.Value = value;
                currentNode = currentNode.Next;
                if(PositionValid)
                    position++;
            }
        }
        public override void Insert(byte[] buffer, int offset, int count, bool follow)
        {
            if (count <= 0)
                return;

            if (EndOfStream)
            {
                if (!follow)
                {
                    currentNode = List.AddLast(buffer[offset]);
                    offset++;
                    count--;
                }
                for (int i = 0; i < count; i++)
                    List.AddLast(buffer[offset + i]);
                if (follow)
                {
                    position = Length;
                }
            }
            else if(follow)
            {
                for(int i = 0; i < count; i++)
                {
                    List.AddBefore(currentNode, buffer[offset + i]);
                    position++;
                }
            }
            else
            {
                for (int i = offset + count - 1; offset <= i; i--)
                {
                    currentNode = List.AddBefore(currentNode, buffer[i]);
                }
            }
        }
    }
}

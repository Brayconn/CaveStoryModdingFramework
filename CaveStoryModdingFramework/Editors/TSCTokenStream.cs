using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace CaveStoryModdingFramework.Editors
{
    public class TSCTokenStream
    {
        readonly IList<TSCTokenLine> Lines;

        public TSCTokenStream(IList<TSCTokenLine> lines)
        {
            Lines = lines;
            Reset();
        }

        /// <summary>
        /// The line that will be read on next Pop
        /// </summary>
        int readLine;
        /// <summary>
        /// The token that will be read on the next Pop (null = end of line)
        /// </summary>
        LinkedListNode<ITSCToken>? readToken;
        /// <summary>
        /// The line that will be written to on next Push
        /// </summary>
        int writeLine;
        /// <summary>
        /// The token that will be written after on the next Push (null = start of line)
        /// </summary>
        LinkedListNode<ITSCToken>? writeToken;
        public void Reset()
        {
            readLine = writeLine = 0;
            writeToken = null;
            readToken = readLine < Lines.Count ? Lines[readLine].Tokens.First : null;
        }
        internal void SetPosition(int line, int token)
        {
            readLine = writeLine = line;
            LinkedListNode<ITSCToken>? t = null;
            if (line < Lines.Count)
            {
                t = Lines[line].Tokens.First;
                for (int i = 0; i < token; i++)
                    t = t.Next;
            }
            readToken = t;
            if (line < Lines.Count) //HACK
                writeToken = t?.Previous ?? Lines[line].Tokens.Last;
            else
                writeToken = null;
        }

        static bool IsValidStartToken(ITSCToken token) =>
            token is TSCEventToken ||
            token is TSCCommandToken ||
            token is TSCTextToken ||
            token is TSCTextNewLineToken ||
            token is TSCEventEndToken;

        /// <summary>
        /// Find the first valid token on the given line before the given offset to start parsing from
        /// </summary>
        /// <param name="line"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">The chosen line must have tokens on it</exception>
        int FindFirstValidToken(int line, int offset)
        {
            if (line < 0)
                throw new ArgumentException(nameof(line));
            if (offset < 0)
                throw new ArgumentException(nameof(offset));
            if (line >= Lines.Count)
                return 0;
            if (Lines[line].TokenCount <= 0)
                throw new ArgumentException(nameof(line));

            //start will lag behind on the last valid token (event, command, text)
            //since we just can't start parsing in the middle of a command
            int startOffset = 0;
            LinkedListNode<ITSCToken>? startToken;
            int currentOffset = 0;
            LinkedListNode<ITSCToken>? currentToken;
            
            startToken = currentToken = Lines[line].Tokens.First!;

            if(offset == Lines[line].TextLength)
            {
                startOffset = Lines[line].TextLength;
                startToken = null;
            }
            else if(0 < offset)
            {
                while (currentToken != null && currentOffset + currentToken.Value.Text.Length <= offset)
                {
                    currentOffset += currentToken.Value.Text.Length;
                    currentToken = currentToken.Next;
                    if (IsValidStartToken(currentToken.Value))
                    {
                        startOffset = currentOffset;
                        startToken = currentToken;
                    }
                }
            }
            writeToken = startToken?.Previous;
            readToken = startToken;
            return startOffset;
        }

        public string Replace(int line, int offset, int length, string text)
        {
            if (line > Lines.Count)
                throw new ArgumentOutOfRangeException(nameof(line));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));
            
            //line was given
            readLine = writeLine = line;

            //find the token on that line
            var tokenOffset = FindFirstValidToken(line, offset);

            return DoReplace(tokenOffset, offset, length, text);
        }
        public string Replace(int offset, int length, string text)
        {
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            var lineOffset = 0;

            //find the right line to start on
            int currentLine;
            for (currentLine = 0; currentLine < Lines.Count && lineOffset + Lines[currentLine].TextLength < offset; /*increment is done in the loop*/)
            {
                lineOffset += Lines[currentLine].TextLength;
                currentLine++;
            }
            //the above loop will stop one line early in the event the edit takes place at the very end of a line/start of a new one
            if (currentLine < Lines.Count && lineOffset + Lines[currentLine].TextLength == offset &&
                //if the line doesn't have a new line, this is fine, but if it does, we need to go to the start of the next line
                (Lines[currentLine].Tokens.Last.Value is TSCTextNewLineToken || Lines[currentLine].Tokens.Last.Value is TSCEventEndToken))
            {
                lineOffset += Lines[currentLine].TextLength;
                currentLine++;
            }
            readLine = writeLine = currentLine;

            //find the right token on that line
            var tokenOffset = FindFirstValidToken(currentLine, offset - lineOffset);

            return DoReplace(lineOffset + tokenOffset, offset, length, text);
        }
        private string DoReplace(int currentOffset, int targetOffset, int length, string text)
        {
            if(targetOffset < 0)
                throw new ArgumentOutOfRangeException(nameof(targetOffset));
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));
            if (length == 0)
                return text;

            //TODO investigate capacity
            var sb = new StringBuilder(text.Length * 2);

            int consumedChars = 0;
            string lastReadToken = string.Empty;

            //if we're behind where we're supposed to be...
            if (currentOffset < targetOffset)
            {
                var preChars = targetOffset - currentOffset;
                //read the first token
                bool popOk = TryPop(out lastReadToken);
                Debug.Assert(popOk);
                //add the first characters we were forced to take
                sb.Append(lastReadToken, 0, preChars);
                //update position
                currentOffset += lastReadToken.Length;
                consumedChars += preChars;
            }

            //do the edit
            sb.Append(text);
            //if the loop below doesn't run, that implies that the first token contained the entire edit, so this will be valid
            //if the loop below DOES run, then this will be overwritten, so it doesn't matter if it was temporarily invalid
            consumedChars += text.Length;

            //if the first token we read wasn't enough to cover the entire edit
            var endOffset = targetOffset + length;
            while (currentOffset < endOffset && TryPop(out lastReadToken))
            {
                currentOffset += lastReadToken.Length;
                consumedChars = lastReadToken.Length - (currentOffset - endOffset);
            }

            //read any EXTRA data if needed
            if(currentOffset > endOffset)
            {
                sb.Append(lastReadToken, consumedChars, lastReadToken.Length - consumedChars);
            }

            return sb.ToString();
        }
        private bool TryAdvance(ref int line, ref LinkedListNode<ITSCToken>? token)
        {
            while(token == null && line+1 < Lines.Count)
            {
                token = Lines[++line].Tokens.First;
            }
            return token != null;
        }
        public bool TryPop(out ITSCToken token)
        {
            if (!TryAdvance(ref readLine, ref readToken))
            {
                token = null; //it's your fault if you try and read the out value after returning false...
                return false;
            }

            token = Lines[readLine].Remove(ref readToken);

            if(readToken == null)
                TryAdvance(ref readLine, ref readToken);
            
            return true;
        }
        public bool TryPop(out string text)
        {
            var success = TryPop(out ITSCToken token);
            text = success ? token.Text : string.Empty;
            return success;
        }

        public void Push(ITSCToken value)
        {
            //make sure we always have a line to write to
            while (writeLine >= Lines.Count)
                Lines.Add(new TSCTokenLine());

            //write the token in the appropriate spot
            if (writeToken == null)
            {
                writeToken = Lines[writeLine].AddFirst(value);
            }
            else
            {
                writeToken = Lines[writeLine].AddAfter(writeToken, value);
            }

            //if we just wrote a new line... move to/create a new line!
            if(value is TSCTextNewLineToken || value is TSCEventEndToken)
            {
                if(writeToken.Next != null)
                {
                    //insert new empty line if needed
                    if (writeLine+1 >= Lines.Count)
                    {
                        Lines.Add(new TSCTokenLine());
                    }
                    else if(Lines[writeLine + 1].TokenCount > 0)
                    {
                        Lines.Insert(writeLine+1, new TSCTokenLine());
                    }
                    //use empty line
                    for(var t = writeToken.Next; t != null; /*Advancing done in loop*/)
                    {
                        Lines[writeLine + 1].AddLast(t.Value);
                        if(t == readToken)
                        {
                            readToken = Lines[++readLine].Tokens.Last;
                        }
                        var prev = t;
                        t = t.Next;
                        Lines[writeLine].Remove(ref prev);
                    }
                }
                writeLine++;
                if (writeLine < Lines.Count)
                    writeToken = Lines[writeLine].Tokens.First;
                else
                    writeToken = null;
            }
        }
    }
}

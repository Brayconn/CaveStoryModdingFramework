using System;
using System.Text;

namespace CaveStoryModdingFramework.Utilities
{
    public class EscapedByteDecoderFallback : DecoderFallback
    {
        public override int MaxCharCount => 4;

        public override DecoderFallbackBuffer CreateFallbackBuffer()
        {
            return new EscapedByteDecoderFallbackBuffer();
        }
    }
    public class EscapedByteDecoderFallbackBuffer : DecoderFallbackBuffer
    {
        int remaining;
        public override int Remaining => remaining;

        string escaped;
        int max;

        public EscapedByteDecoderFallbackBuffer()
        {
            escaped = new string(EscapedASCII.EscapeChar, EscapedASCII.EscapeHexChar);
            max = escaped.Length + 2;
            remaining = max;
        }

        public override bool Fallback(byte[] bytesUnknown, int index)
        {
            escaped += Convert.ToString(bytesUnknown[0], 16) + "\0";
            return true;
        }

        public override char GetNextChar()
        {
            return escaped[max - remaining--];
        }

        public override bool MovePrevious()
        {
            if (remaining < max)
            {
                remaining++;
                return true;
            }
            else
                return false;
        }
    }

    /// <summary>
    /// Same as Encoding.ASCII, but invalid characters are escaped with the format \xNN
    /// </summary>
    public class EscapedASCII : Encoding
    {
        public const char EscapeChar = '\\';
        public const byte EscapeByte = (byte)EscapeChar;

        public const char EscapeHexChar = 'x';
        public const byte EscapeHexByte = (byte)EscapeHexChar;
        
        public override int GetByteCount(char[] chars, int index, int count)
        {
            int result = 0;
            for(int i = 0; i < count; i++)
            {
                //if the current character is a potential escape sequence...
                if (chars[index + i] == EscapeChar)
                {
                    var remaining = count - (i + 1);
                    //there should be at least ONE character it's escaping
                    if(remaining >= 1)
                    {
                        switch(chars[index + i + 1])
                        {
                            //ah, it's just escaping the escape character
                            case EscapeChar:
                                //skip past it so we don't double count
                                i++;
                                break;
                            case EscapeHexChar:
                                //escaped bytes need room to exist
                                if (remaining >= 3)
                                {
                                    if(Extensions.IsHexDigit(chars[index + i + 2]) &&
                                       Extensions.IsHexDigit(chars[index + i + 3]))
                                    {
                                        //valid escaped byte! this will turn into 1 byte of output
                                        //this plus the for loop's i++ will skip to the next correct char
                                        i += 3;
                                    }
                                    else if(ThrowOnInvalidEscapeSequence)
                                        throw new EncoderFallbackException($"Invalid escaped byte at {index + i}");
                                }
                                else if(ThrowOnInvalidEscapeSequence)
                                    throw new EncoderFallbackException($"Not enough room for escaped byte at {index + i}");
                                break;
                            default:
                                if(ThrowOnInvalidEscapeSequence)
                                    throw new EncoderFallbackException($"Unknown escape sequence at index {index + i}");
                                break;
                        }
                    }
                    else if(ThrowOnInvalidEscapeSequence)
                        throw new EncoderFallbackException($"Escape sequence at end of stream! {index + i}");
                }
                result++;
            }
            return result;
        }

        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
        {
            int i;
            for(i = 0; i < charCount; i++)
            {
                if(chars[charIndex + i] == EscapeChar)
                {
                    var remaining = charCount - (i + 1);
                    if (remaining >= 1)
                    {
                        switch (chars[charIndex + i + 1])
                        {
                            //false alarm, just need to NOT write the second EscapeChar
                            case EscapeChar:
                                i++;
                                break;
                            case EscapeHexChar:
                                //escaped bytes need room to exist
                                if (remaining >= 3)
                                {
                                    if (Extensions.IsHexDigit(chars[charIndex + i + 2]) &&
                                       Extensions.IsHexDigit(chars[charIndex + i + 3]))
                                    {
                                        //write the escaped byte
                                        var c = new char[2];
                                        Array.Copy(chars, charIndex + i + 2, c, 0, 2);
                                        bytes[byteIndex++] = System.Convert.ToByte(new string(c), 16);
                                        //this plus the for loop's i++ will skip to the next correct char
                                        i += 3;
                                        //DON'T fallthrough to the default printing code below
                                        continue;
                                    }
                                    else if (ThrowOnInvalidEscapeSequence)
                                        throw new EncoderFallbackException($"Invalid escaped byte at {charIndex + i}");
                                }
                                else if (ThrowOnInvalidEscapeSequence)
                                    throw new EncoderFallbackException($"Not enough room for escaped byte at {charIndex + i}");
                                break;
                            default:
                                if (ThrowOnInvalidEscapeSequence)
                                    throw new EncoderFallbackException($"Unknown escape sequence at index {charIndex + i}");
                                break;
                        }
                    }
                    else if (ThrowOnInvalidEscapeSequence)
                        throw new EncoderFallbackException($"Escape sequence at end of stream! {charIndex + i}");
                }
                bytes[byteIndex++] = (byte)chars[charIndex + i];
            }
            return i;
        }

        //ASCII table moment
        static readonly string[] CharMap = new string[256]
        {
            @"\x00", @"\x01", @"\x02", @"\x03", @"\x04", @"\x05", @"\x06", @"\x07", @"\x08", "\t",    "\n",    @"\x0B", @"\x0C", "\r",    @"\x0E", @"\x0F",
            @"\x10", @"\x11", @"\x12", @"\x13", @"\x14", @"\x15", @"\x16", @"\x17", @"\x18", @"\x19", @"\x1A", @"\x1B", @"\x1C", @"\x1D", @"\x1E", @"\x1F",
            " ",     "!",     "\"",    "#",     "$",     "%",     "&",     "'",     "(",     ")",     "*",     "+",     ",",     "-",     ".",     "/",
            "0",     "1",     "2",     "3",     "4",     "5",     "6",     "7",     "8",     "9",     ":",     ";",     "<",     "=",     ">",     "?",
            "@",     "A",     "B",     "C",     "D",     "E",     "F",     "G",     "H",     "I",     "J",     "K",     "L",     "M",     "N",     "O",
            "P",     "Q",     "R",     "S",     "T",     "U",     "V",     "W",     "X",     "Y",     "Z",     "[",     "\\",    "]",     "^",     "_",
            "`",     "a",     "b",     "c",     "d",     "e",     "f",     "g",     "h",     "i",     "j",     "k",     "l",     "m",     "n",     "o",
            "p",     "q",     "r",     "s",     "t",     "u",     "v",     "w",     "x",     "y",     "z",     "{",     "|",     "}",     "~",     @"\x7F",
            @"\x80", @"\x81", @"\x82", @"\x83", @"\x84", @"\x85", @"\x86", @"\x87", @"\x88", @"\x89", @"\x8A", @"\x8B", @"\x8C", @"\x8D", @"\x8E", @"\x8F",
            @"\x90", @"\x91", @"\x92", @"\x93", @"\x94", @"\x95", @"\x96", @"\x97", @"\x98", @"\x99", @"\x9A", @"\x9B", @"\x9C", @"\x9D", @"\x9E", @"\x9F",
            @"\xA0", @"\xA1", @"\xA2", @"\xA3", @"\xA4", @"\xA5", @"\xA6", @"\xA7", @"\xA8", @"\xA9", @"\xAA", @"\xAB", @"\xAC", @"\xAD", @"\xAE", @"\xAF",
            @"\xB0", @"\xB1", @"\xB2", @"\xB3", @"\xB4", @"\xB5", @"\xB6", @"\xB7", @"\xB8", @"\xB9", @"\xBA", @"\xBB", @"\xBC", @"\xBD", @"\xBE", @"\xBF",
            @"\xC0", @"\xC1", @"\xC2", @"\xC3", @"\xC4", @"\xC5", @"\xC6", @"\xC7", @"\xC8", @"\xC9", @"\xCA", @"\xCB", @"\xCC", @"\xCD", @"\xCE", @"\xCF",
            @"\xD0", @"\xD1", @"\xD2", @"\xD3", @"\xD4", @"\xD5", @"\xD6", @"\xD7", @"\xD8", @"\xD9", @"\xDA", @"\xDB", @"\xDC", @"\xDD", @"\xDE", @"\xDF",
            @"\xE0", @"\xE1", @"\xE2", @"\xE3", @"\xE4", @"\xE5", @"\xE6", @"\xE7", @"\xE8", @"\xE9", @"\xEA", @"\xEB", @"\xEC", @"\xED", @"\xEE", @"\xEF",
            @"\xF0", @"\xF1", @"\xF2", @"\xF3", @"\xF4", @"\xF5", @"\xF6", @"\xF7", @"\xF8", @"\xF9", @"\xFA", @"\xFB", @"\xFC", @"\xFD", @"\xFE", @"\xFF"
        };

        public override int GetCharCount(byte[] bytes, int index, int count)
        {
            int result = 0;
            for (int i = 0; i < count; i++)
            {
                switch(bytes[index + i])
                {
                    case (byte)'\t':
                    case (byte)'\n':
                    case (byte)'\r':
                        result += ForceOneCharWidth ? 4 : 1;
                        break;
                    case EscapeByte:
                        result += DoubleEscapeBytes ? 2 : 1;
                        break;
                    default:
                        result += CharMap[bytes[index + i]].Length;
                        break;
                }
            }
            return result;
        }

        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
        {
            int i;
            for(i = 0; i < byteCount; i++)
            {
                string charData;
                switch(bytes[byteIndex + i])
                {
                    case (byte)'\t' when ForceOneCharWidth:
                        charData = @"\x09";
                        break;
                    case (byte)'\n' when ForceOneCharWidth:
                        charData = @"\x0A";
                        break;
                    case (byte)'\r' when ForceOneCharWidth:
                        charData = @"\x0D";
                        break;
                    case EscapeByte when DoubleEscapeBytes:
                        charData = new string(new char[] { EscapeChar, EscapeChar });
                        break;
                    default:
                        charData = CharMap[bytes[byteIndex + i]];
                        break;
                }
                charData.CopyTo(0, chars, charIndex, charData.Length);
                charIndex += charData.Length;
            }
            return i;
        }

        //charCount would be if none of the input was valid escape sequences
        public override int GetMaxByteCount(int charCount) => charCount;

        //byteCount * 4 would be if every byte needed to be escaped
        public override int GetMaxCharCount(int byteCount) => byteCount * 4;

        /// <summary>
        /// When true, tabs, newlines, and carriage returns will be escaped instead of printed normally
        /// </summary>
        public bool ForceOneCharWidth { get; set; } = false;

        /// <summary>
        /// When converting a string to bytes, throw an exception when an invalid escape sequence is encountered
        /// </summary>
        public bool ThrowOnInvalidEscapeSequence { get; set; } = false;

        /// <summary>
        /// When converting bytes to a string, turn "\" into "\\" so it won't be read as an escape sequence when converting back to bytes
        /// </summary>
        public bool DoubleEscapeBytes { get; set; } = true;
    }

    public static class EncodingOverrides
    {
        public static Encoding EscapedASCII { get; } = new EscapedASCII();
        public static Encoding EscapedOneCharWideASCII { get; } = new EscapedASCII() { ForceOneCharWidth = true };
        public static Encoding GetEncoding(int codepage)
        {
            if (codepage == Encoding.ASCII.CodePage)
            {
                return new EscapedASCII();
            }
            else
            {
#if NETCOREAPP
                    //.NET Core will throw an exception if the requested encoding isn't already loaded
                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
                return Encoding.GetEncoding(codepage, new EncoderExceptionFallback(), new EscapedByteDecoderFallback());
            }
        }
        public static Encoding GetEncoding(string name)
        {
            if (name == Encoding.ASCII.EncodingName)
            {
                return new EscapedASCII();
            }
            else
            {
#if NETCOREAPP
                    //.NET Core will throw an exception if the requested encoding isn't already loaded
                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
                return Encoding.GetEncoding(name, new EncoderExceptionFallback(), new EscapedByteDecoderFallback());
            }
        }

        public static byte[] GetBytesWithEscapes(Encoding encoding, string text)
        {
            if(text.Length <= 0)
                return Array.Empty<byte>();

            var buffer = new byte[encoding.GetMaxByteCount(text.Length)];
            int byteIndex = 0;
            int start = 0, end = start;
            void AddBytes()
            {
                byteIndex += encoding.GetBytes(text, start, end - start, buffer, byteIndex);
            }

            while(end < text.Length)
            {
                start = end;
                switch(text[end])
                {
                    case Utilities.EscapedASCII.EscapeChar:
                        var remaining = text.Length - end - 1;
                        if (remaining < 1)
                        {
                            end++;
                            AddBytes();
                            break;
                        }
                        switch(text[end+1])
                        {
                            case Utilities.EscapedASCII.EscapeChar:
                                end++;
                                AddBytes();
                                end++;
                                break;
                            case Utilities.EscapedASCII.EscapeHexChar:
                                if (remaining >= 3
                                    && Extensions.IsHexDigit(text[end+2])
                                    && Extensions.IsHexDigit(text[end+3]))
                                {
                                    buffer[byteIndex++] = Convert.ToByte(text.Substring(end + 2, 2), 16);
                                    end += 4;
                                }
                                else
                                {
                                    end++;
                                    goto NORMAL_TEXT;
                                }
                                break;
                            default:
                                end++;
                                goto NORMAL_TEXT;
                        }
                        break;
                    default:
                        NORMAL_TEXT:
                        while (end < text.Length && text[end] != Utilities.EscapedASCII.EscapeChar)
                            end++;
                        AddBytes();
                        break;
                }
            }
            var finalBuff = new byte[byteIndex];
            Array.Copy(buffer, finalBuff, finalBuff.Length);
            return finalBuff;
        }
    }
}

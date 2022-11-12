using System;
using System.Collections.Generic;
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
        const int max = 4;
        int remaining = 4;
        public override int Remaining => remaining;

        string escaped = @"\x";
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
        static bool IsHexDigit(char c)
        {
            return ('0' <= c && c <= '9')
                || ('A' <= c && c <= 'F')
                || ('a' <= c && c <= 'f');
        }
        public override int GetByteCount(char[] chars, int index, int count)
        {
            int result = 0;
            for(int i = 0; i < count; i++)
            {
                //if the current character is a potential escape sequence
                //and there's room to actually check...
                if (chars[index + i] == '\\')
                {
                    //check for an escaped byte sequence
                    if (count - i >= 4 &&
                        chars[index + i + 1] == 'x' &&
                        IsHexDigit(chars[index + i + 2]) &&
                        IsHexDigit(chars[index + i + 3]))
                    {
                        //this plus the for loop's i++ will skip to the next correct char
                        i += 3;
                    }
                    //escaped slash?
                    else if (count - i >= 1 && chars[index + i + 1] == '\\')
                    {
                        result++;
                    }
                    else
                        throw new EncoderFallbackException($"Unknown escape sequence at index {index + i}");
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
                if(chars[charIndex + i] == '\\')
                {
                    //if there's enough room for an escaped byte, check for one
                    if (charCount - i >= 4 &&
                   chars[charIndex + i + 1] == 'x' &&
                   IsHexDigit(chars[charIndex + i + 2]) &&
                   IsHexDigit(chars[charIndex + i + 3]))
                    {
                        bytes[byteIndex++] = System.Convert.ToByte(new string(chars[charIndex + i + 2], chars[charIndex + i + 3]), 16);
                        i += 3;
                    }
                    //okay, maybe it's an escaped slash?
                    else if (charCount - i >= 1 && chars[charIndex + i + 1] == '\\')
                    {
                        bytes[byteIndex++] = (byte)'\\';
                        bytes[byteIndex++] = (byte)'\\';
                        i++;
                    }
                    //idk lol
                    else
                        throw new EncoderFallbackException($"Unknown escape sequence at index {charIndex + i}");
                }
                else
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
            "P",     "Q",     "R",     "S",     "T",     "U",     "V",     "W",     "X",     "Y",     "Z",     "[",     @"\\",    "]",     "^",     "_",
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

        public bool ForceOneCharWidth { get; private set; }
        public EscapedASCII(bool forceOneCharWidth = false)
        {
            ForceOneCharWidth = forceOneCharWidth;
        }
    }

    public static class EncodingOverrides
    {
        public static Encoding EscapedASCII { get; } = new EscapedASCII();
        public static Encoding EscapedOneCharWideASCII { get; } = new EscapedASCII(true);
        public static Encoding GetEncoding(int codepage)
        {
            if (codepage == Encoding.ASCII.CodePage)
                return EscapedASCII;
            else
                return Encoding.GetEncoding(codepage, new EncoderExceptionFallback(), new EscapedByteDecoderFallback());
        }
        public static Encoding GetEncoding(string name)
        {
            if (name == Encoding.ASCII.EncodingName)
                return EscapedASCII;
            else
                return Encoding.GetEncoding(name, new EncoderExceptionFallback(), new EscapedByteDecoderFallback());
        }
    }
}

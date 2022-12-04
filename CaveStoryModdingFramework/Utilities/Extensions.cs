using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;

namespace CaveStoryModdingFramework
{
    public static class Extensions
    {
        public static T Clamp<T>(this T value, T min, T max) where T : IComparable<T>
        {
            if (value.CompareTo(min) < 0)
                return min;
            if (value.CompareTo(max) > 0)
                return max;

            return value;
        }

        //TODO this should really go in filephoenix.extensions at some point
        internal static ReadOnlyDictionary<Type, Func<BinaryReader, dynamic>> BinaryReaderDict = new ReadOnlyDictionary<Type, Func<BinaryReader, dynamic>>(new Dictionary<Type, Func<BinaryReader, dynamic>>()
        {
            { typeof(byte), (BinaryReader br) => br.ReadByte() },
            { typeof(sbyte), (BinaryReader br) => br.ReadSByte() },
            { typeof(short), (BinaryReader br) => br.ReadInt16() },
            { typeof(ushort), (BinaryReader br) => br.ReadUInt16() },
            { typeof(int), (BinaryReader br) => br.ReadInt32() },
            { typeof(uint), (BinaryReader br) => br.ReadUInt32() },
            { typeof(long), (BinaryReader br) => br.ReadInt64() },
            { typeof(ulong), (BinaryReader br) => br.ReadUInt64() },
            { typeof(float), (BinaryReader br) => br.ReadSingle() },
            { typeof(double), (BinaryReader br) => br.ReadDouble() },
            { typeof(decimal), (BinaryReader br) => br.ReadDecimal() },            
        });
        public static T Read<T>(this BinaryReader br) where T : struct
        {
            return BinaryReaderDict[typeof(T)](br);
        }
        public static dynamic Read(this BinaryReader br, Type T)
        {
            return BinaryReaderDict[T](br);
        }

        public static string ReadString(this BinaryReader br, int length, Encoding encoding)
        {
            return encoding?.GetString(br.ReadBytes(length).TakeWhile(x => x != 0).ToArray());
        }

        public static void BufferCopy(Array source, Array dest, int destIndex, int maxCopy)
        {
            if(maxCopy > 0)
                Array.Copy(source, 0, dest, destIndex, Math.Min(source.Length, maxCopy));
        }

        public static byte[] ConvertAndGetBytes(object obj, Type T)
        {
            dynamic conv = Convert.ChangeType(obj, T);
            return (conv.GetType() == typeof(sbyte) || conv.GetType() == typeof(byte))
                ? new byte[] { (byte)conv }
                : BitConverter.GetBytes(conv);
        }
        public static bool IsHexDigit(char c)
        {
            return ('0' <= c && c <= '9')
                || ('A' <= c && c <= 'F')
                || ('a' <= c && c <= 'f');
        }

        /*
        public static T?[] ToNullable<T>(this T[] array) where T : struct
        {
            T?[] output = new T?[array.Length];
            for (int i = 0; i < array.Length; i++)
                output[i] = array[i];
            return output;
        }
        public static T[] ToNonNullable<T>(this T?[] array) where T : struct
        {
            T[] output = new T[array.Length];
            for (int i = 0; i < array.Length; i++)
                output[i] = array[i] ?? default;
            return output;
        }
        */

        //TODO this isn't 100% safe, since you could still add/remove namespaces from this...
        public static readonly XmlSerializerNamespaces BlankNamespace;
        static Extensions()
        {
            BlankNamespace = new XmlSerializerNamespaces();
            BlankNamespace.Add("", "");
        }

        public static T DeserializeAs<T>(this XmlReader stream, T dummy, string root)
        {
            var serializer = new XmlSerializer(typeof(T), new XmlRootAttribute(root));
            return (T)serializer.Deserialize(stream);
        }
        public static void SerializeAsRoot<T>(this XmlWriter stream, T obj, string root)
        {
            var serializer = new XmlSerializer(typeof(T), new XmlRootAttribute(root));
            serializer.Serialize(stream, obj, BlankNamespace);
        }

        public static Encoding ReadElementContentAsEncoding(this XmlReader reader, string localName, string namespaceURI = "")
        {
#if NETCOREAPP
            //.NET Core will throw on the subsequent call to get Shift JIS if this isn't run
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
            var str = reader.ReadElementContentAsString(localName, namespaceURI);
            if (!string.IsNullOrWhiteSpace(str))
                return Encoding.GetEncoding(str);
            else
                return null;
        }

        public static Type ReadElementContentAsTypeName(this XmlReader reader, string localName, string namespaceURI = "")
        {
            var typeName = reader.ReadElementContentAsString(localName, namespaceURI);
            var type = Type.GetType(typeName);
            return type;
        }

        public static Stream OpenInMemory(string path)
        {
            var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            byte[] contents;
            Stream StreamToUse;
            try
            {
                contents = new byte[fs.Length];
                fs.Read(contents, 0, (int)fs.Length);
                StreamToUse = new MemoryStream(contents);
                fs.Close();
            }
            catch (OutOfMemoryException) //file was too big to load into memory all at once I guess
            {
                StreamToUse = fs;
            }
            return StreamToUse;
        }

        public static bool CheckBytes(this Stream stream, byte[] seq, bool peek = false)
        {
            int i;
            for(i = 0; stream.Position < stream.Length && i < seq.Length; i++)
            {
                if (stream.ReadByte() != seq[i])
                    break;
            }
            if (i < seq.Length)
                stream.Position -= i + 1;
            else if (peek)
                stream.Position -= seq.Length;
            return i == seq.Length;
        }

        public static long FindBytes(this Stream stream, byte[] seq)
        {
            var counter = 0;
            while (stream.Position < stream.Length && counter < seq.Length)
            {
                if (stream.ReadByte() != seq[counter++])
                    counter = 0;
            }
            if (counter >= seq.Length)
                return stream.Position - counter;
            else
                return -1;
        }

        public static int CountZeros(this Stream stream)
        {
            var count = 0;
            while (stream.ReadByte() == 0)
                count++;
            stream.Position--;
            return count;
        }

        //These two methods rely on the fact that .NET Framework is probably only being used on Windows
        //(where NTFS is already case insensitive without the extra arg)
        //But Linux/Mac users are probably using .NET Core, so they need this
        //Just don't run this on .NET Framework on Mac/Linux and we should be good
        public static IEnumerable<string> EnumerateFilesCaseInsensitive(string path, string filter = "")
        {
            return Directory.EnumerateFiles(path, filter
                    
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
                    , new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive }
#endif
                );
        }
        public static IEnumerable<string> EnumerateDirectoriesCaseInsensitive(string path, string filter = "")
        {
            return Directory.EnumerateDirectories(path, filter
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
                    , new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive }
#endif
                );
        }

        public static string ReplaceCaseInsensitive(this string s, string old, string @new)
        {
#if NETCOREAPP2_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            return s.Replace(old, @new, StringComparison.OrdinalIgnoreCase);
#else
            return Regex.Replace(s, Regex.Escape(old), @new, RegexOptions.IgnoreCase);
#endif
        }
    }
}

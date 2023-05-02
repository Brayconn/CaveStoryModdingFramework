using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CaveStoryModdingFramework.Editors
{
    public class TSCInspector
    {
        int width = 0;
        public int Width
        {
            get => width;
            set
            {
                if (value > 0)
                    width = value;
                else
                    throw new ArgumentException(nameof(value));
            }
        }

        public int Result
        {
            get
            {
                return data.GetTSCNum();
            }
        }

        List<byte> data = new List<byte>();

        public byte GetValue(int index)
        {
            return data[index];
        }
        public void SetValue(int index, byte val)
        {
            data[index] = val;
        }
        public TSCInspector(IList<byte> data)
        {
            Width = data.Count;
            this.data = new List<byte>(data);
        }
        public TSCInspector(int width, byte init = 0)
        {
            Width = width;
            data = new List<byte>(Enumerable.Repeat(init,Width));
        }

        public char FillChar = '_';
        public override string ToString()
        {
            string SafeConvert(int index)
            {
                var p = data.Count - 1 - index;
                if (data[index] == (byte)'0')
                    return "0" + new string(FillChar, LocalExtensions.PowersOfTen[p].ToString().Length - 1);
                else
#if MATH_MODE
                    return ((((sbyte)data[index]) - '0') * LocalExtensions.PowersOfTen[p]).ToString();
#else
                    return (((sbyte)data[index]) - '0').ToString() + new string(FillChar, LocalExtensions.PowersOfTen[p].ToString().Length - 1);
#endif
            }
            var sb = new StringBuilder();
            var first = SafeConvert(0);
            sb.AppendLine(first);
            
            for(int i = 1; i < data.Count; i++)
            {
                var t = SafeConvert(i);
                sb.Append(' ', first.Length - t.Length);
                sb.AppendLine(t);
            }

            //after the data
            sb.Append('-', first.Length);
            sb.Append('\n');
            var r = Result.ToString();
            sb.Append(' ', first.Length - r.Length);
            sb.Append(r);
            return sb.ToString();
        }
    }
}

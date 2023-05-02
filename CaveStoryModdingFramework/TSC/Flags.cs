using System;
using System.Collections.Generic;
using System.Text;

namespace CaveStoryModdingFramework.TSC
{
    public static class FlagConverter
    {
        public const int NPCFlagAddress = 0x49DDA0;

        public const int SkipFlagAddress = 0x49DD98;

        public const int MapFlagAddress = 0x49E5B8;

        /// <summary>
        /// -176
        /// </summary>
        public const int MIN_DIGIT = sbyte.MinValue - '0';
        /// <summary>
        /// -1936
        /// </summary>
        public const int MIN_2_DIGIT = (10 * MIN_DIGIT) + MIN_DIGIT;
        /// <summary>
        /// -19536
        /// </summary>
        public const int MIN_3_DIGIT = (100 * MIN_DIGIT) + MIN_2_DIGIT;
        /// <summary>
        /// -195536
        /// </summary>
        public const int MIN_4_DIGIT = (1000 * MIN_DIGIT) + MIN_3_DIGIT;

        /// <summary>
        /// 79
        /// </summary>
        public const int MAX_DIGIT = sbyte.MaxValue - '0';
        /// <summary>
        /// 869
        /// </summary>
        public const int MAX_2_DIGIT = (10 * MAX_DIGIT) + MAX_DIGIT;
        /// <summary>
        /// 8769
        /// </summary>
        public const int MAX_3_DIGIT = (100 * MAX_DIGIT) + MAX_2_DIGIT;
        /// <summary>
        /// 87769
        /// </summary>
        public const int MAX_4_DIGIT = (1000 * MAX_DIGIT) + MAX_3_DIGIT;

        //It's probably obvious by the definition, but these are given by the formula
        // (|MIN_DIGIT| + 1) * (10^x) - 1 - MAX_10^(x-1)_DIGIT
        //I'm not exactly sure why this is the formula to use, and there might be an expanded form that works better too
        //but for the purpose of getting these functions working, it's enough
        /// <summary>
        /// 1690
        /// </summary>
        public const int START_2_DIGIT = (-MIN_DIGIT + 1) * 10 - 1 - MAX_DIGIT;
        /// <summary>
        /// 16830
        /// </summary>
        public const int START_3_DIGIT = (-MIN_DIGIT + 1) * 100 - 1 - MAX_2_DIGIT;
        /// <summary>
        /// 168232
        /// </summary>
        public const int START_4_DIGIT = (-MIN_DIGIT + 1) * 1000 - 1 - MAX_3_DIGIT;

        #region 2-digit iteration

        public static (int,int) Get2Bounds(int target)
        {
            return (
                Math.Max(MIN_DIGIT, ((target + START_2_DIGIT) / 10) + MIN_DIGIT),
                Math.Min(((target - MIN_2_DIGIT) / 10) + MIN_DIGIT, MAX_DIGIT)
                );
        }

        public static IEnumerable<(int,int)> Iterate2Flags(int target)
        {
            if (target < MIN_2_DIGIT || MAX_2_DIGIT < target)
                throw new ArgumentOutOfRangeException(nameof(target));

            (int start, int end) = Get2Bounds(target);
            for (int i = start; i <= end; i++)
            {
                int x = (-10 * i) + target;
                yield return (i, x);
            }
        }

        #endregion

        #region 3-digit iteration

        public static (int,int) Get3Bounds(int target)
        {
            return (
                Math.Max(MIN_DIGIT, ((target + START_3_DIGIT) / 100) + MIN_DIGIT),
                Math.Min(((target - MIN_3_DIGIT) / 100) + MIN_DIGIT, MAX_DIGIT)
                );
        }
        public static IEnumerable<(int,int,int)> Iterate3Flags(int target)
        {
            if(target < MIN_3_DIGIT || MAX_3_DIGIT < target)
                throw new ArgumentOutOfRangeException(nameof(target));

            (var start, var end) = Get3Bounds(target);
            for(int i = start; i <= end; i++)
            {
                foreach(var r in Iterate2Flags(target - (100*i)))
                {
                    yield return (i, r.Item1, r.Item2);
                }
            }
        }

        #endregion

        #region 4-digit iteration

        public static (int,int) Get4Bounds(int target)
        {
            return (
                Math.Max(MIN_DIGIT, ((target + START_4_DIGIT) / 1000) + MIN_DIGIT),
                Math.Min(((target - MIN_4_DIGIT) / 1000) + MIN_DIGIT, MAX_DIGIT)
                );
        }

        public static IEnumerable<(int,int,int,int)> Iterate4Flags(int target)
        {
            if (target < MIN_4_DIGIT || MAX_4_DIGIT < target)
                throw new ArgumentOutOfRangeException(nameof(target));

            (var start, var end) = Get4Bounds(target);
            for(int i = start; i <= end; i++)
            {
                foreach (var r in Iterate3Flags(target - (1000 * i)))
                {
                    yield return (i, r.Item1, r.Item2, r.Item3);
                }
            }
        }

        #endregion

        #region samples
        /* Extremely basic sample methods. Useful for understanding the process
        public static int[] FlagToRealValueTEST(char[] flag)
        {
            int[] number = new int[4];

            number[0] = (flag[0] - 0x30) * 1000;
            number[1] = (flag[1] - 0x30) * 100;
            number[2] = (flag[2] - 0x30) * 10;
            number[3] = (flag[3] - 0x30) * 1;

            //The sum of all numbers in "number" is the "real flag value"
            return number;
        }
        //In practice, undoing the sum to arrive at an int[] that would work in this exact method is the hard part
        public static char[] RealValueToFlagTEST(int[] number)
        {
            char[] flag = new char[4];

            flag[0] = (char)(number[0] / 1000 + 0x30);
            flag[1] = (char)(number[1] / 100 + 0x30);
            flag[2] = (char)(number[2] / 10 + 0x30);
            flag[3] = (char)(number[3] / 1 + 0x30);

            return flag;
        }
        //*/
        #endregion

        #region flag

        /// <summary>
        /// Get the address of a given flag
        /// </summary>
        /// <param name="flag">The flag number</param>
        /// <returns>The address of the </returns>
        public static int FlagToAddress(string flag, int length, out int bit, int firstFlagAddress = NPCFlagAddress)
        {
            var val = FlagToRealValue(flag, length);
            var whole = Math.DivRem(val, 8, out bit) + firstFlagAddress;
            if (val < 0 && bit != 0)
                whole--;
            bit = (8 + bit) % 8;
            return whole;
        }

        /// <summary>
        /// Get the address of a given flag
        /// </summary>
        /// <param name="flag">The flag number</param>
        /// <returns>The address of the </returns>
        public static int FlagToAddress(string flag, int length, int firstFlagAddress = NPCFlagAddress)
        {
            return (FlagToRealValue(flag, length) / 8) + firstFlagAddress;
        }
        public static int FlagToRealValue(string flag, int length)
        {
            return FlagToRealValue(flag, length, Encoding.UTF8);
        }
        /// <summary>
        /// Get the "real" value of the given TSC flag
        /// </summary>
        /// <param name="flag">The TSC flag</param>
        /// <returns>The "real" flag number</returns>
        public static int FlagToRealValue(string flag, int length, Encoding encoding)
        {
            TryFlagToRealValue(flag, length, encoding, out var output);
            return output;
        }

        public static bool TryFlagToRealValue(string flag, out int value)
        {
            return TryFlagToRealValue(flag, flag.Length, Encoding.UTF8, out value);
        }
        public static bool TryFlagToRealValue(string flag, int length, out int value)
        {
            return TryFlagToRealValue(flag, length, Encoding.UTF8, out value);
        }
        public static bool TryFlagToRealValue(string flag, int length, Encoding encoding, out int value)
        {
            //Input sanitation
            byte[] input = encoding.GetBytes(flag);

            value = 0;
            for (int i = 0; i < input.Length; i++)
                value += (input[i] - 0x30) * (int)Math.Pow(10, input.Length - 1 - i);

            return input.Length == length;
        }


        #endregion

        #region Real Value

        /// <summary>
        /// Get the TSC flag for the "real" flag number
        /// </summary>
        /// <param name="number">The "real" flag number</param>
        /// <returns>The TSC flag</returns>
        public static string RealValueToFlag(int number, int outputLength = 4, char minimum_character = ' ', char max_char = '~')
        {
            string flag = "";
            //If this number can be represented using just numbers
            if (0 <= number && number <= (int)Math.Pow(10, outputLength - 1))
            {
                //pad it and return
                flag = number.ToString($"D{outputLength}");
            }
            //if it's within the range of "single OOB character" numbers, use that
            else if (FlagToRealValue(minimum_character + new string('0', outputLength - 1), outputLength) <= number
                 && number <= FlagToRealValue(max_char + new string('9', outputLength - 1), outputLength))
            {
                for (int dec_place = outputLength - 1; dec_place >= 0; dec_place--)
                {
                    var divisor = (int)Math.Pow(10, dec_place);
                    var digit = Math.DivRem(number, divisor, out int rem);

                    if (rem != 0 && digit < 0)
                        digit--;

                    flag += (char)(byte)(digit + 0x30);

                    if (dec_place == 0)
                        break;

                    if (digit != 0)
                    {
                        if (number < 0)
                            rem = (divisor + rem) % divisor;
                        flag += rem.ToString($"D{dec_place}");
                        break;
                    }
                }
            }
            /*outside the given bounds
            else if (number < FlagToRealValue(new string(minimum_character, outputLength)) || FlagToRealValue(new string(max_char, outputLength)) < number)
            {
                //TODO ????
            }
            */
            //otherwise just use the generic multi-character one
            else //if (number < FlagToRealValue(minimum_character + new string('0', outputLength - 1)) || FlagToRealValue(max_char + new string('9', outputLength - 1)) < number)
            {
                for (int i = outputLength - 1; i >= 0; i--)
                {
                    int decimalPlace = (int)Math.Pow(10, i);
                    //This value MUST be clamped to the range of numbers that FlagToRealValue would produce
                    var thisC = (number / decimalPlace).Clamp((byte)(minimum_character - 0x30), (byte)(max_char - 0x30));
                    //Then it can be used safely
                    number -= decimalPlace * thisC;
                    flag += (char)(byte)(thisC + 0x30);
                }
            }
            return flag;
        }
        /// <summary>
        /// Get the address of a "real" flag number
        /// </summary>
        /// <param name="number">The real value of the flag</param>
        /// <returns>Address of the given flag</returns>
        public static int RealValueToAddress(int number, out int bit, int firstFlagAddress = NPCFlagAddress)
        {
            var whole = Math.DivRem(number, 8, out bit) + firstFlagAddress;
            if (number < 0 && bit != 0)
                whole--;
            bit = ((8 + bit) % 8);
            return whole;
        }
        /// <summary>
        /// Get the address of a "real" flag number
        /// </summary>
        /// <param name="number">The real value of the flag</param>
        /// <returns>Address of the given flag</returns>
        public static int RealValueToAddress(int number, int firstFlagAddress = NPCFlagAddress)
        {
            return (number / 8) + firstFlagAddress;
        }

        #endregion

        #region address
        /// <summary>
        /// Get the first flag corresponding to the given address
        /// </summary>
        /// <param name="address">Address in the exe</param>
        /// <returns>First TSC flag corresponding to this address</returns>
        public static string AddressToFlag(int address, int bit = 0, int firstFlagAddress = NPCFlagAddress)
        {
            return RealValueToFlag(((address - firstFlagAddress) * 8) + bit);
        }
        /// <summary>
        /// Get the real value of a given address
        /// </summary>
        /// <param name="address">Address in the exe</param>
        /// <returns>Real value of this address</returns>
        public static int AddressToRealValue(int address, int bit = 0, int firstFlagAddress = NPCFlagAddress)
        {
            return ((address - firstFlagAddress) * 8) + bit;
        }
        #endregion
    }
}
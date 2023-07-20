using CaveStoryModdingFramework.TSC;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace CaveStoryModdingFrameworkTests
{
    public class TSCNumberTests
    {
        private readonly ITestOutputHelper output;
        public TSCNumberTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void TSC2IteratorRangeOk()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => FlagConverter.Iterate2Flags(FlagConverter.MIN_2_DIGIT - 1).ToArray());
            Assert.Throws<ArgumentOutOfRangeException>(() => FlagConverter.Iterate2Flags(FlagConverter.MAX_2_DIGIT + 1).ToArray());
        }

        /// <summary>
        /// Uses brute force to check that the function works
        /// </summary>
        [Fact]
        public void TSC2IteratorWorks()
        {
            var h = new Dictionary<int, HashSet<(int, int)>>();
            for (int i = FlagConverter.MIN_DIGIT; i <= FlagConverter.MAX_DIGIT; i++)
            {
                for (int j = FlagConverter.MIN_DIGIT; j <= FlagConverter.MAX_DIGIT; j++)
                {
                    var v = (10 * i) + j;
                    if (!h.ContainsKey(v))
                        h.Add(v, new HashSet<(int, int)>() { (i, j) });
                    else
                        h[v].Add((i, j));
                }
            }

            foreach (var kvp in h)
            {
                var hs = new HashSet<(int, int)>(FlagConverter.Iterate2Flags(kvp.Key));
                try
                {
                    Assert.Equal(kvp.Value, hs);
                }
                catch
                {
                    output.WriteLine($"FAIL on {kvp.Key}:");
                    output.WriteLine($"Expected: {string.Join(", ", kvp.Value)}");
                    output.WriteLine($"Actual: {string.Join(", ", hs)}");
                    throw;
                }
            }
        }

        [Fact]
        public void TSC3IteratorRangeOk()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => FlagConverter.Iterate2Flags(FlagConverter.MIN_3_DIGIT - 1).ToArray());
            Assert.Throws<ArgumentOutOfRangeException>(() => FlagConverter.Iterate2Flags(FlagConverter.MAX_3_DIGIT + 1).ToArray());
        }

        /// <summary>
        /// Checks that all individual outputs are valid, and that the correct number of values is encountered.
        /// </summary>
        [Fact(Skip = "Takes around 5 seconds to run")]
        public void TSC3IteratorLooksOk()
        {
            const int total = 256 * 256 * 256;
            int actual = 0;
            for (int i = FlagConverter.MIN_3_DIGIT; i <= FlagConverter.MAX_3_DIGIT; i++)
            {
                foreach (var o in FlagConverter.Iterate3Flags(i))
                {
                    actual++;
                    try
                    {
                        Assert.InRange(o.Item1, FlagConverter.MIN_DIGIT, FlagConverter.MAX_DIGIT);
                        Assert.InRange(o.Item2, FlagConverter.MIN_DIGIT, FlagConverter.MAX_DIGIT);
                        Assert.InRange(o.Item3, FlagConverter.MIN_DIGIT, FlagConverter.MAX_DIGIT);
                        Assert.Equal(i, (o.Item1 * 100) + (o.Item2 * 10) + o.Item3);
                    }
                    catch
                    {
                        output.WriteLine($"{i} => {o} = {(o.Item1 * 100) + (o.Item2 * 10) + o.Item3}");
                        throw;
                    }
                }
            }
            Assert.Equal(total, actual);
        }

        [Fact]
        public void TSC4IteratorRangeOk()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => FlagConverter.Iterate2Flags(FlagConverter.MIN_4_DIGIT - 1).ToArray());
            Assert.Throws<ArgumentOutOfRangeException>(() => FlagConverter.Iterate2Flags(FlagConverter.MAX_4_DIGIT + 1).ToArray());
        }
        /// <summary>
        /// Checks that all individual outputs are valid, and that the correct number of values is encountered.
        /// </summary>
        //TODO consider using multithreading to reduce runtime?
        [Fact(Skip = "Takes around 30 minutes to run")]
        public void TSC4IteratorLooksOk()
        {
            //My want to be explicit requires this to be 64 bit
            const long total = 256L * 256L * 256L * 256L;
            long actual = 0;
            for (int i = FlagConverter.MIN_4_DIGIT; i <= FlagConverter.MAX_4_DIGIT; i++)
            {
                foreach (var o in FlagConverter.Iterate4Flags(i))
                {
                    actual++;
                    try
                    {
                        Assert.InRange(o.Item1, FlagConverter.MIN_DIGIT, FlagConverter.MAX_DIGIT);
                        Assert.InRange(o.Item2, FlagConverter.MIN_DIGIT, FlagConverter.MAX_DIGIT);
                        Assert.InRange(o.Item3, FlagConverter.MIN_DIGIT, FlagConverter.MAX_DIGIT);
                        Assert.InRange(o.Item4, FlagConverter.MIN_DIGIT, FlagConverter.MAX_DIGIT);
                        Assert.Equal(i, (o.Item1 * 1000) + (o.Item2 * 100) + (o.Item3 * 10) + o.Item4);
                    }
                    catch
                    {
                        output.WriteLine($"{i} => {o} = {(o.Item1 * 1000) + (o.Item2 * 100) + (o.Item3 * 10) + o.Item4}");
                        throw;
                    }
                }
            }
            Assert.Equal(total, actual);
        }
    }
}

using CaveStoryModdingFramework.Utilities;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using Xunit;
using Xunit.Abstractions;

namespace CaveStoryModdingFrameworkTests
{
    public class UserEnumTests
    {
        private readonly ITestOutputHelper output;
        public UserEnumTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        public class SerializeTests : IEnumerable<object[]>
        {
            UserEnum[] enums = new UserEnum[]
            {
            new UserEnum("Empty",null),
            new UserEnum("DefaultOnly",new UserEnumValue("Default")),
            new UserEnum("SingleOnly",new UserEnumValue("Default"), new SingleMap(0,"Zero")),
            new UserEnum("RangeOnly",new UserEnumValue("Default"), new RangeMap(0,1,"0-1")),
            new UserEnum("InfiniteOnly",new UserEnumValue("Default"), new InfiniteMap(0, Directions.Positive, "0-infinity")),
            };
            public IEnumerator<object[]> GetEnumerator()
            {
                foreach (var e in enums)
                    yield return new object[] { e };
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Theory]
        [ClassData(typeof(SerializeTests))]
        public void SerializeOK(UserEnum e)
        {
            using (var m = new MemoryStream())
            {
                var x = new XmlSerializer(typeof(UserEnum));
                
                x.Serialize(m, e);
                m.Position = 0;

                output.WriteLine(Encoding.ASCII.GetString(m.ToArray()));

                var o = (UserEnum)x.Deserialize(m);

                Assert.Equal(e, o);
            }
        }
    }
}

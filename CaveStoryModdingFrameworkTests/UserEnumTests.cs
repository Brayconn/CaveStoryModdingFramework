using CaveStoryModdingFramework.Utilities;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using Xunit;

namespace CaveStoryModdingFrameworkTests
{
    public class UserEnumTests
    {
        public class SerializeTests : IEnumerable<object[]>
        {
            UserEnum[] enums = new UserEnum[]
            {
            new UserEnum("Empty",null),
            new UserEnum("DefaultOnly","Default"),
            new UserEnum("SingleOnly","Default", new SingleMap(0,"Zero")),
            new UserEnum("RangeOnly","Default", new RangeMap(0,1,"0-1")),
            new UserEnum("InfiniteOnly","Default", new InfiniteMap(0, Directions.Positive, "0-infinity")),
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

                var o = (UserEnum)x.Deserialize(m);

                Assert.Equal(e, o);
            }
        }
    }
}

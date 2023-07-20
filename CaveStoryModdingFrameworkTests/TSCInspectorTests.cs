using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit;
using CaveStoryModdingFramework.Editors;

namespace CaveStoryModdingFrameworkTests
{
    public class TSCInspectorTests
    {
        private readonly ITestOutputHelper output;
        public TSCInspectorTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        //TODO visually inspecting the output from this is pretty bad...
        [Theory]
        [InlineData("\0\0\0\0")]
        [InlineData("\x7F\x7F\x7F\x7F")]
        [InlineData("\x80\x80\x80\x80")]
        [InlineData("\xFF\xFF\xFF\xFF")]
        [InlineData("0000")]
        [InlineData("0001")]
        [InlineData("0200")]
        [InlineData("000/")]
        [InlineData("00/:")]
        [InlineData("001&")]
        public void InspectorWorks(string data)
        {
            var i = new TSCInspector(Encoding.Latin1.GetBytes(data));
            output.WriteLine(i.ToString());
        }
    }
}

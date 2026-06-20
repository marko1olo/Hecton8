using NUnit.Framework;
using Hecton8.Core;
using Unity.Collections;

namespace Hecton8.Gameplay.Editor.Tests
{
    public class HarpoonTextSegmentTests
    {
        [Test]
        public void FormatString_WithValidTemplateAndArgument_WritesCorrectly()
        {
            var segment = HarpoonLauncherTool.HarpoonTextSegment.FormatString("Test {0}", "Value");
            var buffer = new FixedCharBuffer(256);
            var mutableBuffer = buffer; // Make a mutable copy if it's a struct
            bool result = segment.TryWrite(ref mutableBuffer);
            Assert.IsTrue(result);
            Assert.AreEqual("Test Value", mutableBuffer.ToString());
        }

        [Test]
        public void FormatString_WithNullTemplate_WritesEmpty()
        {
            var segment = HarpoonLauncherTool.HarpoonTextSegment.FormatString(null, "Value");
            var buffer = new FixedCharBuffer(256);
            var mutableBuffer = buffer;
            bool result = segment.TryWrite(ref mutableBuffer);
            Assert.IsTrue(result);
            Assert.AreEqual(string.Empty, mutableBuffer.ToString());
        }

        [Test]
        public void FormatString_WithoutPlaceholders_WritesTemplateOnly()
        {
            var segment = HarpoonLauncherTool.HarpoonTextSegment.FormatString("Test Only", "Value");
            var buffer = new FixedCharBuffer(256);
            var mutableBuffer = buffer;
            bool result = segment.TryWrite(ref mutableBuffer);
            Assert.IsTrue(result);
            Assert.AreEqual("Test Only", mutableBuffer.ToString());
        }

        [Test]
        public void FormatString_WithMultiplePlaceholders_WritesArgumentMultipleTimes()
        {
            var segment = HarpoonLauncherTool.HarpoonTextSegment.FormatString("{0} Test {0}", "Value");
            var buffer = new FixedCharBuffer(256);
            var mutableBuffer = buffer;
            bool result = segment.TryWrite(ref mutableBuffer);
            Assert.IsTrue(result);
            Assert.AreEqual("Value Test Value", mutableBuffer.ToString());
        }
    }
}

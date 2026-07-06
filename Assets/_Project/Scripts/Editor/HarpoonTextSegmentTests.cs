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

        [Test]
        public void FormatStringFloat_WithValidTemplateAndArguments_WritesCorrectly()
        {
            var segment = HarpoonLauncherTool.HarpoonTextSegment.FormatStringFloat("Test {0} {1}", "Value", 12.3f);
            var buffer = new FixedCharBuffer(256);
            var mutableBuffer = buffer;
            bool result = segment.TryWrite(ref mutableBuffer);
            Assert.IsTrue(result);
            Assert.AreEqual("Test Value 12.3", mutableBuffer.ToString());
        }

        [Test]
        public void FormatStringFloatFloat_WithValidTemplateAndArguments_WritesCorrectly()
        {
            var segment = HarpoonLauncherTool.HarpoonTextSegment.FormatStringFloatFloat("Test {0} {1} {2}", "Value", 12.3f, 45.6f);
            var buffer = new FixedCharBuffer(256);
            var mutableBuffer = buffer;
            bool result = segment.TryWrite(ref mutableBuffer);
            Assert.IsTrue(result);
            Assert.AreEqual("Test Value 12.3 45.6", mutableBuffer.ToString());
        }

        [Test]
        public void FormatStringFloatFloat_WithSwappedTokens_WritesCorrectly()
        {
            var segment = HarpoonLauncherTool.HarpoonTextSegment.FormatStringFloatFloat("Test {2} {0} {1}", "Value", 12.3f, 45.6f);
            var buffer = new FixedCharBuffer(256);
            var mutableBuffer = buffer;
            bool result = segment.TryWrite(ref mutableBuffer);
            Assert.IsTrue(result);
            Assert.AreEqual("Test 45.6 Value 12.3", mutableBuffer.ToString());
        }
    }
}

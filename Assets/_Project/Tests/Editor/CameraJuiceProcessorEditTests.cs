using System.Reflection;
using NUnit.Framework;
using Hecton8.Gameplay;

namespace Hecton8.Tests.Editor
{
    public sealed class CameraJuiceProcessorEditTests
    {
        [Test]
        public void RegisterWaterEntryFovImpulse_NegativeOrZeroDuration_NoStateChange()
        {
            var processor = new CameraJuiceProcessor();
            processor.Initialize(false);

            // Set up some initial state
            processor.RegisterWaterEntryFovImpulse(10f, 5f, 2f);

            var fieldTimer = typeof(CameraJuiceProcessor).GetField("_waterEntryFovTimer", BindingFlags.NonPublic | BindingFlags.Instance);
            var fieldDuration = typeof(CameraJuiceProcessor).GetField("_waterEntryFovDuration", BindingFlags.NonPublic | BindingFlags.Instance);
            var fieldExpand = typeof(CameraJuiceProcessor).GetField("_waterEntryFovExpandDegrees", BindingFlags.NonPublic | BindingFlags.Instance);
            var fieldCompress = typeof(CameraJuiceProcessor).GetField("_waterEntryFovCompressDegrees", BindingFlags.NonPublic | BindingFlags.Instance);

            float initialTimer = (float)fieldTimer.GetValue(processor);
            float initialDuration = (float)fieldDuration.GetValue(processor);
            float initialExpand = (float)fieldExpand.GetValue(processor);
            float initialCompress = (float)fieldCompress.GetValue(processor);

            // Negative duration
            processor.RegisterWaterEntryFovImpulse(20f, 15f, -1f);

            Assert.AreEqual(initialTimer, (float)fieldTimer.GetValue(processor));
            Assert.AreEqual(initialDuration, (float)fieldDuration.GetValue(processor));
            Assert.AreEqual(initialExpand, (float)fieldExpand.GetValue(processor));
            Assert.AreEqual(initialCompress, (float)fieldCompress.GetValue(processor));

            // Zero duration
            processor.RegisterWaterEntryFovImpulse(20f, 15f, 0f);

            Assert.AreEqual(initialTimer, (float)fieldTimer.GetValue(processor));
            Assert.AreEqual(initialDuration, (float)fieldDuration.GetValue(processor));
            Assert.AreEqual(initialExpand, (float)fieldExpand.GetValue(processor));
            Assert.AreEqual(initialCompress, (float)fieldCompress.GetValue(processor));
        }

        [Test]
        public void RegisterWaterEntryFovImpulse_ZeroExpandAndCompress_NoStateChange()
        {
            var processor = new CameraJuiceProcessor();
            processor.Initialize(false);

            // Set up some initial state
            processor.RegisterWaterEntryFovImpulse(10f, 5f, 2f);

            var fieldTimer = typeof(CameraJuiceProcessor).GetField("_waterEntryFovTimer", BindingFlags.NonPublic | BindingFlags.Instance);
            var fieldDuration = typeof(CameraJuiceProcessor).GetField("_waterEntryFovDuration", BindingFlags.NonPublic | BindingFlags.Instance);
            var fieldExpand = typeof(CameraJuiceProcessor).GetField("_waterEntryFovExpandDegrees", BindingFlags.NonPublic | BindingFlags.Instance);
            var fieldCompress = typeof(CameraJuiceProcessor).GetField("_waterEntryFovCompressDegrees", BindingFlags.NonPublic | BindingFlags.Instance);

            float initialTimer = (float)fieldTimer.GetValue(processor);
            float initialDuration = (float)fieldDuration.GetValue(processor);
            float initialExpand = (float)fieldExpand.GetValue(processor);
            float initialCompress = (float)fieldCompress.GetValue(processor);

            // Zero expand and compress, but valid duration
            processor.RegisterWaterEntryFovImpulse(0f, 0f, 5f);

            Assert.AreEqual(initialTimer, (float)fieldTimer.GetValue(processor));
            Assert.AreEqual(initialDuration, (float)fieldDuration.GetValue(processor));
            Assert.AreEqual(initialExpand, (float)fieldExpand.GetValue(processor));
            Assert.AreEqual(initialCompress, (float)fieldCompress.GetValue(processor));

            // Negative expand and compress (which get clamped to 0)
            processor.RegisterWaterEntryFovImpulse(-5f, -10f, 5f);

            Assert.AreEqual(initialTimer, (float)fieldTimer.GetValue(processor));
            Assert.AreEqual(initialDuration, (float)fieldDuration.GetValue(processor));
            Assert.AreEqual(initialExpand, (float)fieldExpand.GetValue(processor));
            Assert.AreEqual(initialCompress, (float)fieldCompress.GetValue(processor));
        }
    }
}

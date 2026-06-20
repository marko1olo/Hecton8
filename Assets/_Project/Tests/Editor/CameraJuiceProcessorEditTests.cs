using NUnit.Framework;
using System.Reflection;

namespace Hecton8.Tests.Editor
{
    public sealed class CameraJuiceProcessorEditTests
    {
        [Test]
        public void RegisterActionBob_WithZeroIntensity_DoesNotUpdateInternalFields()
        {
            var processor = new CameraJuiceProcessor();

            // Set fields to known values
            processor.RegisterActionBob(1f);

            var fieldY = typeof(CameraJuiceProcessor).GetField("_actionBobY", BindingFlags.NonPublic | BindingFlags.Instance);
            var fieldYVel = typeof(CameraJuiceProcessor).GetField("_actionBobYVel", BindingFlags.NonPublic | BindingFlags.Instance);
            var fieldX = typeof(CameraJuiceProcessor).GetField("_actionBobX", BindingFlags.NonPublic | BindingFlags.Instance);
            var fieldXVel = typeof(CameraJuiceProcessor).GetField("_actionBobXVel", BindingFlags.NonPublic | BindingFlags.Instance);
            var fieldIntensity = typeof(CameraJuiceProcessor).GetField("_actionBobIntensity", BindingFlags.NonPublic | BindingFlags.Instance);

            float expectedY = (float)fieldY.GetValue(processor);
            float expectedYVel = (float)fieldYVel.GetValue(processor);
            float expectedX = (float)fieldX.GetValue(processor);
            float expectedXVel = (float)fieldXVel.GetValue(processor);
            float expectedIntensity = (float)fieldIntensity.GetValue(processor);

            // Action
            processor.RegisterActionBob(0f);

            // Assert
            Assert.AreEqual(expectedY, (float)fieldY.GetValue(processor));
            Assert.AreEqual(expectedYVel, (float)fieldYVel.GetValue(processor));
            Assert.AreEqual(expectedX, (float)fieldX.GetValue(processor));
            Assert.AreEqual(expectedXVel, (float)fieldXVel.GetValue(processor));
            Assert.AreEqual(expectedIntensity, (float)fieldIntensity.GetValue(processor));
        }

        [Test]
        public void RegisterActionBob_WithNegativeIntensity_DoesNotUpdateInternalFields()
        {
            var processor = new CameraJuiceProcessor();

            // Set fields to known values
            processor.RegisterActionBob(1f);

            var fieldY = typeof(CameraJuiceProcessor).GetField("_actionBobY", BindingFlags.NonPublic | BindingFlags.Instance);
            var fieldYVel = typeof(CameraJuiceProcessor).GetField("_actionBobYVel", BindingFlags.NonPublic | BindingFlags.Instance);
            var fieldX = typeof(CameraJuiceProcessor).GetField("_actionBobX", BindingFlags.NonPublic | BindingFlags.Instance);
            var fieldXVel = typeof(CameraJuiceProcessor).GetField("_actionBobXVel", BindingFlags.NonPublic | BindingFlags.Instance);
            var fieldIntensity = typeof(CameraJuiceProcessor).GetField("_actionBobIntensity", BindingFlags.NonPublic | BindingFlags.Instance);

            float expectedY = (float)fieldY.GetValue(processor);
            float expectedYVel = (float)fieldYVel.GetValue(processor);
            float expectedX = (float)fieldX.GetValue(processor);
            float expectedXVel = (float)fieldXVel.GetValue(processor);
            float expectedIntensity = (float)fieldIntensity.GetValue(processor);

            // Action
            processor.RegisterActionBob(-0.5f);

            // Assert
            Assert.AreEqual(expectedY, (float)fieldY.GetValue(processor));
            Assert.AreEqual(expectedYVel, (float)fieldYVel.GetValue(processor));
            Assert.AreEqual(expectedX, (float)fieldX.GetValue(processor));
            Assert.AreEqual(expectedXVel, (float)fieldXVel.GetValue(processor));
            Assert.AreEqual(expectedIntensity, (float)fieldIntensity.GetValue(processor));
        }
    }
}

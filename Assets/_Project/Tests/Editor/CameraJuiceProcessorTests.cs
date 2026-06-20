using NUnit.Framework;
using Hecton8.Gameplay;
using System.Reflection;

namespace Hecton8.Tests.Editor
{
    public sealed class CameraJuiceProcessorTests
    {
        [Test]
        public void ClearActionBob_ResetsActionBobIntensityToZero()
        {
            // Arrange
            var processor = new CameraJuiceProcessor();

            // Set up initial state where action bob intensity is non-zero
            processor.RegisterActionBob(1.0f);

            // Get private field using reflection
            var fieldInfo = typeof(CameraJuiceProcessor).GetField("_actionBobIntensity", BindingFlags.NonPublic | BindingFlags.Instance);

            // Verify our setup worked
            float initialIntensity = (float)fieldInfo.GetValue(processor);
            Assert.AreEqual(1.0f, initialIntensity, "Setup failed: _actionBobIntensity was not set correctly.");

            // Act
            processor.ClearActionBob();

            // Assert
            float clearedIntensity = (float)fieldInfo.GetValue(processor);
            Assert.AreEqual(0f, clearedIntensity, "ClearActionBob did not reset _actionBobIntensity to 0.");
        }
    }
}

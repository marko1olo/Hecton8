using NUnit.Framework;
using UnityEngine;
using Hecton8.Gameplay;
using System.Reflection;

namespace Hecton8.Tests
{
    public class CameraJuiceProcessorEditTests
    {
        [Test]
        public void ClearActionBob_SetsActionBobIntensityToZero()
        {
            // Arrange
            var processor = new CameraJuiceProcessor();

            // Use reflection to set the private field _actionBobIntensity to a non-zero value
            var fieldInfo = typeof(CameraJuiceProcessor).GetField("_actionBobIntensity", BindingFlags.NonPublic | BindingFlags.Instance);
            fieldInfo.SetValue(processor, 5.0f);

            Assert.AreEqual(5.0f, (float)fieldInfo.GetValue(processor), "Failed to set initial value");

            // Act
            processor.ClearActionBob();

            // Assert
            Assert.AreEqual(0.0f, (float)fieldInfo.GetValue(processor), "ClearActionBob should reset _actionBobIntensity to 0");
        }
    }
}

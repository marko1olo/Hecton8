using NUnit.Framework;
using UnityEngine;
using System.Reflection;

namespace Hecton.Tests
{
    public class CameraJuiceProcessorInitializeIssueTests
    {
        [Test]
        public void Initialize_ResetsStateToNeutral()
        {
            // The codebase actually uses a sealed class `CameraJuiceProcessor` (not a MonoBehaviour)
            // and `Initialize(bool)` resets internal timers and intensities rather than transforms.
            var processor = new CameraJuiceProcessor();
            var type = typeof(CameraJuiceProcessor);

            // Dirty state
            type.GetField("_bobTimer", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(processor, 10f);
            type.GetField("_bobIntensity", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(processor, 1f);
            type.GetField("_wasInLowPhase", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(processor, true);
            type.GetField("_currentRoll", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(processor, 45f);

            // Act
            processor.Initialize(true);

            // Assert it resets the parameters to a neutral state
            Assert.AreEqual(0f, type.GetField("_bobTimer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(processor));
            Assert.AreEqual(0f, type.GetField("_bobIntensity", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(processor));
            Assert.AreEqual(false, type.GetField("_wasInLowPhase", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(processor));
            Assert.AreEqual(0f, type.GetField("_currentRoll", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(processor));
            Assert.AreEqual(-1f, type.GetField("_rollSign", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(processor));
        }
    }
}

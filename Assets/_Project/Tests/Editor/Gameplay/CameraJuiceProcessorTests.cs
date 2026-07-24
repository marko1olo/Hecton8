using NUnit.Framework;
using Hecton8.Gameplay;
using System.Reflection;

namespace Hecton8.Tests.Editor.Gameplay
{
    public class CameraJuiceProcessorTests
    {
        [Test]
        public void Initialize_ResetsAllFieldsAndSetsRollSign()
        {
            // Arrange
            var processor = new CameraJuiceProcessor();

            // Use reflection to dirty the internal state
            var type = typeof(CameraJuiceProcessor);
            type.GetField("_bobTimer", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(processor, 5f);
            type.GetField("_bobIntensity", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(processor, 1f);
            type.GetField("_wasInLowPhase", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(processor, true);
            type.GetField("_swimBobTimer", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(processor, 3f);
            type.GetField("_swimBobIntensity", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(processor, 0.5f);
            type.GetField("_swayTimer", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(processor, 10f);
            type.GetField("_swayIntensity", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(processor, 0.8f);
            type.GetField("_surfaceBobTimer", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(processor, 4f);
            type.GetField("_impactDipCurrent", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(processor, 2f);

            type.GetField("_splashThisFrame", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(processor, true);
            type.GetField("_splashIntensityThisFrame", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(processor, 0.9f);
            type.GetField("_submergeChangeThisFrame", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(processor, true);
            type.GetField("_submergedStateThisFrame", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(processor, true);
            type.GetField("_exhaleThisFrame", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(processor, true);
            type.GetField("_currentRoll", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(processor, 45f);

            // Act
            processor.Initialize(leanIntoTurn: true);

            // Assert internal fields were reset
            Assert.AreEqual(0f, type.GetField("_bobTimer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(processor));
            Assert.AreEqual(0f, type.GetField("_bobIntensity", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(processor));
            Assert.AreEqual(false, type.GetField("_wasInLowPhase", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(processor));
            Assert.AreEqual(0f, type.GetField("_swimBobTimer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(processor));
            Assert.AreEqual(0f, type.GetField("_swimBobIntensity", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(processor));
            Assert.AreEqual(0f, type.GetField("_swayTimer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(processor));
            Assert.AreEqual(0f, type.GetField("_swayIntensity", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(processor));
            Assert.AreEqual(0f, type.GetField("_surfaceBobTimer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(processor));
            Assert.AreEqual(0f, type.GetField("_impactDipCurrent", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(processor));

            // Public properties that should be reset
            Assert.AreEqual(0f, processor.SplashIntensity);
            Assert.IsFalse(processor.SplashThisFrame);
            Assert.IsFalse(processor.SubmergeChangedThisFrame);
            Assert.IsFalse(processor.IsSubmerged);
            Assert.IsFalse(processor.ExhaleThisFrame);
            Assert.AreEqual(0f, processor.CurrentRoll);

            // Check roll sign was set based on leanIntoTurn
            Assert.AreEqual(-1f, type.GetField("_rollSign", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(processor));
        }
    }
}

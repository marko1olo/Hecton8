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

        [Test]
        public void RegisterSplash_WithValidSuit_SetsDipValues()
        {
            // Arrange
            var processor = new CameraJuiceProcessor();
            var suit = UnityEngine.ScriptableObject.CreateInstance<SuitData>();
            suit.splashCameraDip = 0.05f;
            float intensity = 0.5f;

            // Act
            processor.RegisterSplash(intensity, suit);

            // Assert
            var type = typeof(CameraJuiceProcessor);
            float expectedDip = -intensity * suit.splashCameraDip;
            float expectedVelocity = -expectedDip * 2f;

            float actualDip = (float)type.GetField("_splashDipCurrent", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(processor);
            float actualVelocity = (float)type.GetField("_splashDipVelocity", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(processor);

            Assert.AreEqual(expectedDip, actualDip, 0.0001f);
            Assert.AreEqual(expectedVelocity, actualVelocity, 0.0001f);

            UnityEngine.Object.DestroyImmediate(suit);
        }

        [Test]
        public void RegisterSplash_WithNullSuit_DoesNothing()
        {
            // Arrange
            var processor = new CameraJuiceProcessor();
            float intensity = 0.5f;

            var type = typeof(CameraJuiceProcessor);
            // Set some initial values
            type.GetField("_splashDipCurrent", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(processor, 1f);
            type.GetField("_splashDipVelocity", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(processor, 2f);

            // Act
            processor.RegisterSplash(intensity, null);

            // Assert
            float actualDip = (float)type.GetField("_splashDipCurrent", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(processor);
            float actualVelocity = (float)type.GetField("_splashDipVelocity", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(processor);

            Assert.AreEqual(1f, actualDip, "Current dip should not change when suit is null");
            Assert.AreEqual(2f, actualVelocity, "Dip velocity should not change when suit is null");
        }

        [Test]
        public void RegisterSplash_WithZeroIntensity_SetsZeroDip()
        {
            // Arrange
            var processor = new CameraJuiceProcessor();
            var suit = UnityEngine.ScriptableObject.CreateInstance<SuitData>();
            suit.splashCameraDip = 0.05f;
            float intensity = 0f;

            // Act
            processor.RegisterSplash(intensity, suit);

            // Assert
            var type = typeof(CameraJuiceProcessor);
            float actualDip = (float)type.GetField("_splashDipCurrent", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(processor);
            float actualVelocity = (float)type.GetField("_splashDipVelocity", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(processor);

            Assert.AreEqual(0f, actualDip, "Dip should be 0 when intensity is 0");
            Assert.AreEqual(0f, actualVelocity, "Velocity should be 0 when intensity is 0");

            UnityEngine.Object.DestroyImmediate(suit);
        }
    }
}

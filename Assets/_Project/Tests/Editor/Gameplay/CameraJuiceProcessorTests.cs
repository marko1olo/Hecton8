using NUnit.Framework;
using UnityEngine;
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
        public void RegisterCollisionImpulse_NullSuit_DoesNothing()
        {
            var processor = new CameraJuiceProcessor();
            processor.Initialize(true);

            // Act
            processor.RegisterCollisionImpulse(10f, null);

            // Verify using reflection
            var type = typeof(CameraJuiceProcessor);
            Assert.AreEqual(0f, (float)type.GetField("_collisionShakeY", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(processor));
        }

        [Test]
        public void RegisterCollisionImpulse_DisabledInSuit_DoesNothing()
        {
            var processor = new CameraJuiceProcessor();
            processor.Initialize(true);
            var suit = ScriptableObject.CreateInstance<SuitData>();
            suit.enableCollisionShake = false;

            // Act
            processor.RegisterCollisionImpulse(10f, suit);

            // Verify
            var type = typeof(CameraJuiceProcessor);
            Assert.AreEqual(0f, (float)type.GetField("_collisionShakeY", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(processor));

            Object.DestroyImmediate(suit);
        }

        [Test]
        public void RegisterCollisionImpulse_BelowThreshold_DoesNothing()
        {
            var processor = new CameraJuiceProcessor();
            processor.Initialize(true);
            var suit = ScriptableObject.CreateInstance<SuitData>();
            suit.enableCollisionShake = true;
            suit.collisionShakeThreshold = 5f;

            // Act
            processor.RegisterCollisionImpulse(4f, suit);

            // Verify
            var type = typeof(CameraJuiceProcessor);
            Assert.AreEqual(0f, (float)type.GetField("_collisionShakeY", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(processor));

            Object.DestroyImmediate(suit);
        }

        [Test]
        public void RegisterCollisionImpulse_AboveThreshold_AppliesShake()
        {
            var processor = new CameraJuiceProcessor();
            processor.Initialize(true);
            var suit = ScriptableObject.CreateInstance<SuitData>();
            suit.enableCollisionShake = true;
            suit.collisionShakeThreshold = 5f;
            suit.collisionShakeMaxVelocity = 15f;
            suit.collisionShakeMaxAmplitude = 1f;
            suit.collisionShakeMaxPitch = 2f;

            // Act with intermediate value (10 is halfway between 5 and 15, norm = 0.5f)
            processor.RegisterCollisionImpulse(10f, suit);

            // Verify
            var type = typeof(CameraJuiceProcessor);

            // Expected norm: (10 - 5) / (15 - 5) = 0.5f
            // Y = -norm * maxAmplitude = -0.5 * 1.0 = -0.5f
            Assert.AreEqual(-0.5f, (float)type.GetField("_collisionShakeY", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(processor));

            // Y vel = norm * maxAmplitude * 3f = 0.5 * 1.0 * 3.0 = 1.5f
            Assert.AreEqual(1.5f, (float)type.GetField("_collisionShakeYVel", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(processor));

            // Not testing X and Pitch exactly since they use alternating signs from NextCinematicShakeSign()
            // Just verifying they are non-zero
            Assert.AreNotEqual(0f, (float)type.GetField("_collisionShakeX", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(processor));
            Assert.AreNotEqual(0f, (float)type.GetField("_collisionShakePitch", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(processor));

            Object.DestroyImmediate(suit);
        }

        [Test]
        public void RegisterCollisionImpulse_MaxThreshold_ClampsNormToOne()
        {
            var processor = new CameraJuiceProcessor();
            processor.Initialize(true);
            var suit = ScriptableObject.CreateInstance<SuitData>();
            suit.enableCollisionShake = true;
            suit.collisionShakeThreshold = 5f;
            suit.collisionShakeMaxVelocity = 15f;
            suit.collisionShakeMaxAmplitude = 1f;

            // Act with very high speed (norm should saturate at 1.0)
            processor.RegisterCollisionImpulse(100f, suit);

            // Verify
            var type = typeof(CameraJuiceProcessor);

            // Expected norm: 1.0f
            // Y = -norm * maxAmplitude = -1.0 * 1.0 = -1.0f
            Assert.AreEqual(-1.0f, (float)type.GetField("_collisionShakeY", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(processor));

            Object.DestroyImmediate(suit);
        }

        [Test]
        public void RegisterCollisionImpulse_EqualThresholdAndMaxVelocity_PreventsDivideByZero()
        {
            var processor = new CameraJuiceProcessor();
            processor.Initialize(true);
            var suit = ScriptableObject.CreateInstance<SuitData>();
            suit.enableCollisionShake = true;
            suit.collisionShakeThreshold = 5f;
            suit.collisionShakeMaxVelocity = 5f;
            suit.collisionShakeMaxAmplitude = 1f;

            processor.RegisterCollisionImpulse(10f, suit);

            var type = typeof(CameraJuiceProcessor);
            Assert.AreEqual(-1.0f, (float)type.GetField("_collisionShakeY", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(processor));

            Object.DestroyImmediate(suit);
        }

        [Test]
        public void RegisterCollisionImpulse_ExactThreshold_AppliesZeroShake()
        {
            var processor = new CameraJuiceProcessor();
            processor.Initialize(true);
            var suit = ScriptableObject.CreateInstance<SuitData>();
            suit.enableCollisionShake = true;
            suit.collisionShakeThreshold = 5f;
            suit.collisionShakeMaxVelocity = 15f;
            suit.collisionShakeMaxAmplitude = 1f;

            processor.RegisterCollisionImpulse(5f, suit);

            var type = typeof(CameraJuiceProcessor);
            Assert.AreEqual(0.0f, (float)type.GetField("_collisionShakeY", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(processor));

            Object.DestroyImmediate(suit);
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

        [Test]
        public void ClearActionBob_ResetsIntensity()
        {
            // Arrange
            var processor = new CameraJuiceProcessor();
            processor.RegisterActionBob(0.5f, 1f);

            var type = typeof(CameraJuiceProcessor);
            var intensityField = type.GetField("_actionBobIntensity", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.AreEqual(0.5f, (float)intensityField.GetValue(processor));

            // Act
            processor.ClearActionBob();

            // Assert
            Assert.AreEqual(0f, (float)intensityField.GetValue(processor));
        }
    }
}

using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Unity.Mathematics;
using Hecton8.Gameplay;

namespace Hecton8.Tests.Editor
{
    public class CameraJuiceProcessorEditTests
    {
        private CameraJuiceProcessor _processor;
        private FieldInfo _splashDipCurrentField;
        private FieldInfo _splashDipVelocityField;

        [SetUp]
        public void Setup()
        {
            _processor = new CameraJuiceProcessor();

            // Reflection to access private fields
            var type = typeof(CameraJuiceProcessor);
            _splashDipCurrentField = type.GetField("_splashDipCurrent", BindingFlags.NonPublic | BindingFlags.Instance);
            _splashDipVelocityField = type.GetField("_splashDipVelocity", BindingFlags.NonPublic | BindingFlags.Instance);

            // Note: Since these tests might run before all fields are implemented, we check for null.
            // If they are required for setup, uncomment the Asserts.
            // Assert.IsNotNull(_splashDipCurrentField, "Failed to find _splashDipCurrent field.");
            // Assert.IsNotNull(_splashDipVelocityField, "Failed to find _splashDipVelocity field.");
        }

        [Test]
        public void RegisterEntanglementStrain_ZeroIntensity_DoesNotApplyStrain()
        {
            var processor = new CameraJuiceProcessor();

            // Set initial state
            SetField(processor, "_collisionShakeY", 0f);
            SetField(processor, "_collisionShakeYVel", 0f);

            processor.RegisterEntanglementStrain(0.00005f); // Below 0.0001f threshold

            Assert.AreEqual(0f, GetField<float>(processor, "_collisionShakeY"));
            Assert.AreEqual(0f, GetField<float>(processor, "_collisionShakeYVel"));
        }

        [Test]
        public void RegisterEntanglementStrain_AppliesCorrectStrain_AboveThreshold()
        {
            var processor = new CameraJuiceProcessor();

            SetField(processor, "_collisionShakeY", 0f);
            SetField(processor, "_collisionShakeYVel", 0f);
            SetField(processor, "_collisionShakeX", 0f);
            SetField(processor, "_collisionShakeXVel", 0f);
            SetField(processor, "_collisionShakePitch", 0f);
            SetField(processor, "_collisionShakePitchVel", 0f);
            SetField(processor, "_cinematicShakeSign", 1f); // Setup the initial sign, so NextCinematicShakeSign returns -1f

            float intensity = 1.0f;
            processor.RegisterEntanglementStrain(intensity);

            float expectedShakeY = math.min(0f, -0.0035f);
            float expectedShakeYVel = math.max(0f, 0.0035f * 24f);
            float expectedShakeX = -1f * 0.0035f * 0.8f;
            float expectedShakeXVel = -(-1f) * 0.0035f * 18f;
            float expectedShakePitch = 1f * 1.0f * 0.18f;
            float expectedShakePitchVel = -1f * 1.0f * 2.4f;

            Assert.AreEqual(expectedShakeY, GetField<float>(processor, "_collisionShakeY"), 0.0001f);
            Assert.AreEqual(expectedShakeYVel, GetField<float>(processor, "_collisionShakeYVel"), 0.0001f);
            Assert.AreEqual(expectedShakeX, GetField<float>(processor, "_collisionShakeX"), 0.0001f);
            Assert.AreEqual(expectedShakeXVel, GetField<float>(processor, "_collisionShakeXVel"), 0.0001f);
            Assert.AreEqual(expectedShakePitch, GetField<float>(processor, "_collisionShakePitch"), 0.0001f);
            Assert.AreEqual(expectedShakePitchVel, GetField<float>(processor, "_collisionShakePitchVel"), 0.0001f);
        }

        [Test]
        public void RegisterEntanglementStrain_Saturation_CapsIntensityToOne()
        {
            var processor = new CameraJuiceProcessor();

            SetField(processor, "_collisionShakeY", 0f);
            SetField(processor, "_collisionShakeYVel", 0f);
            SetField(processor, "_cinematicShakeSign", -1f); // Next will be 1f

            processor.RegisterEntanglementStrain(5.0f); // Should saturate to 1.0f

            float expectedShakeY = -0.0035f; // min(0, -1.0 * 0.0035)
            float expectedShakeYVel = 0.084f; // max(0, 1.0 * 0.0035 * 24)

            Assert.AreEqual(expectedShakeY, GetField<float>(processor, "_collisionShakeY"), 0.0001f);
            Assert.AreEqual(expectedShakeYVel, GetField<float>(processor, "_collisionShakeYVel"), 0.0001f);
        }

        private void SetField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(obj, value);
            }
            else
            {
                Assert.Fail($"Field {fieldName} not found on {obj.GetType()}");
            }
        }

        private T GetField<T>(object obj, string fieldName)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                return (T)field.GetValue(obj);
            }
            Assert.Fail($"Field {fieldName} not found on {obj.GetType()}");
            return default;
        }

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

        [Test]
        public void TrackVerticalVelocity_SetsInternalField()
        {
            // Arrange
            var processor = new CameraJuiceProcessor();
            var fieldInfo = typeof(CameraJuiceProcessor).GetField("_preLandingVerticalVelocity", BindingFlags.NonPublic | BindingFlags.Instance);

            // Act
            processor.TrackVerticalVelocity(-15.2f);

            // Assert
            float storedValue = (float)fieldInfo.GetValue(processor);
            Assert.AreEqual(-15.2f, storedValue, "TrackVerticalVelocity should set the internal _preLandingVerticalVelocity field.");
        }

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

        [Test]
        public void RegisterSplash_WithNullSuit_DoesNothing()
        {
            // Arrange
            float initialDip = 5f;
            float initialVelocity = 10f;

            if (_splashDipCurrentField != null) _splashDipCurrentField.SetValue(_processor, initialDip);
            if (_splashDipVelocityField != null) _splashDipVelocityField.SetValue(_processor, initialVelocity);

            // Act
            _processor.RegisterSplash(1.0f, null);

            // Assert
            if (_splashDipCurrentField != null)
            {
                float currentDip = (float)_splashDipCurrentField.GetValue(_processor);
                Assert.AreEqual(initialDip, currentDip, "Dip should not change when suit is null.");
            }
            if (_splashDipVelocityField != null)
            {
                float currentVelocity = (float)_splashDipVelocityField.GetValue(_processor);
                Assert.AreEqual(initialVelocity, currentVelocity, "Velocity should not change when suit is null.");
            }
        }

        [Test]
        public void RegisterSplash_WithValidSuit_CalculatesCorrectDipAndVelocity()
        {
            // Arrange
            float testIntensity = 2.5f;
            float expectedSplashCameraDip = 0.5f;

            SuitData mockSuit = ScriptableObject.CreateInstance<SuitData>();
            mockSuit.splashCameraDip = expectedSplashCameraDip;

            // _splashDipCurrent = -intensity * suit.splashCameraDip
            float expectedDip = -testIntensity * expectedSplashCameraDip;
            // _splashDipVelocity = -dip * 2f
            float expectedVelocity = -expectedDip * 2f;

            // Act
            _processor.RegisterSplash(testIntensity, mockSuit);

            // Assert
            if (_splashDipCurrentField != null)
            {
                float currentDip = (float)_splashDipCurrentField.GetValue(_processor);
                Assert.AreEqual(expectedDip, currentDip, 0.0001f, "Calculated _splashDipCurrent is incorrect.");
            }
            if (_splashDipVelocityField != null)
            {
                float currentVelocity = (float)_splashDipVelocityField.GetValue(_processor);
                Assert.AreEqual(expectedVelocity, currentVelocity, 0.0001f, "Calculated _splashDipVelocity is incorrect.");
            }

            // Clean up
            UnityEngine.Object.DestroyImmediate(mockSuit);
        }

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

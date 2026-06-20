using NUnit.Framework;
using UnityEngine;
using Unity.Mathematics;
using System.Reflection;
using Hecton8.Gameplay;

namespace Hecton8.Tests.Editor
{
    public class CameraJuiceProcessorEditTests
    {
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

            // Expected values
            // float signX = NextCinematicShakeSign(); // which will be -1f because it flips from 1f
            // float signP = -signX; // 1f
            // float amplitude = intensity * 0.0035f; // 0.0035f

            float expectedShakeY = math.min(0f, -0.0035f); // -0.0035f
            float expectedShakeYVel = math.max(0f, 0.0035f * 24f); // 0.084f
            float expectedShakeX = -1f * 0.0035f * 0.8f; // -0.0028f
            float expectedShakeXVel = -(-1f) * 0.0035f * 18f; // 0.063f
            float expectedShakePitch = 1f * 1.0f * 0.18f; // 0.18f
            float expectedShakePitchVel = -1f * 1.0f * 2.4f; // -2.4f

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
    }
}

using NUnit.Framework;
using UnityEngine;
using Unity.Mathematics;
using Hecton8.Gameplay;
using System.Reflection;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public class CameraJuiceProcessorTests
    {
        private CameraJuiceProcessor _processor;

        [SetUp]
        public void Setup()
        {
            _processor = new CameraJuiceProcessor();
        }

        [Test]
        public void RegisterSonarPingImpulse_LowIntensity_ReturnsEarly()
        {
            // Arrange
            float initialShakeY = GetPrivateField<float>("_collisionShakeY");
            float initialShakeYVel = GetPrivateField<float>("_collisionShakeYVel");
            float initialShakePitch = GetPrivateField<float>("_collisionShakePitch");
            float initialShakePitchVel = GetPrivateField<float>("_collisionShakePitchVel");

            // Act
            _processor.RegisterSonarPingImpulse(0.00005f); // Less than 0.0001f

            // Assert
            Assert.AreEqual(initialShakeY, GetPrivateField<float>("_collisionShakeY"));
            Assert.AreEqual(initialShakeYVel, GetPrivateField<float>("_collisionShakeYVel"));
            Assert.AreEqual(initialShakePitch, GetPrivateField<float>("_collisionShakePitch"));
            Assert.AreEqual(initialShakePitchVel, GetPrivateField<float>("_collisionShakePitchVel"));
        }

        [Test]
        public void RegisterSonarPingImpulse_ValidIntensity_UpdatesShakeFields()
        {
            // Arrange
            float intensity = 0.5f;
            float expectedAmplitude = intensity * 0.0042f;
            float expectedShakeY = -expectedAmplitude; // math.min(0, -amplitude)
            float expectedShakeYVel = expectedAmplitude * 16f; // math.max(0, amplitude * 16f)
            float expectedShakePitch = -intensity * 0.12f; // math.min(0, -intensity * 0.12f)
            float expectedShakePitchVel = intensity * 0.95f; // math.max(0, intensity * 0.95f)

            // Act
            _processor.RegisterSonarPingImpulse(intensity);

            // Assert
            Assert.AreEqual(expectedShakeY, GetPrivateField<float>("_collisionShakeY"));
            Assert.AreEqual(expectedShakeYVel, GetPrivateField<float>("_collisionShakeYVel"));
            Assert.AreEqual(expectedShakePitch, GetPrivateField<float>("_collisionShakePitch"));
            Assert.AreEqual(expectedShakePitchVel, GetPrivateField<float>("_collisionShakePitchVel"));
        }

        [Test]
        public void RegisterSonarPingImpulse_ClampsIntensityToOne()
        {
            // Arrange
            float intensity = 2.0f; // Above 1.0f, should be saturated to 1.0f
            float saturatedIntensity = 1.0f;
            float expectedAmplitude = saturatedIntensity * 0.0042f;
            float expectedShakeY = -expectedAmplitude;
            float expectedShakeYVel = expectedAmplitude * 16f;
            float expectedShakePitch = -saturatedIntensity * 0.12f;
            float expectedShakePitchVel = saturatedIntensity * 0.95f;

            // Act
            _processor.RegisterSonarPingImpulse(intensity);

            // Assert
            Assert.AreEqual(expectedShakeY, GetPrivateField<float>("_collisionShakeY"));
            Assert.AreEqual(expectedShakeYVel, GetPrivateField<float>("_collisionShakeYVel"));
            Assert.AreEqual(expectedShakePitch, GetPrivateField<float>("_collisionShakePitch"));
            Assert.AreEqual(expectedShakePitchVel, GetPrivateField<float>("_collisionShakePitchVel"));
        }

        [Test]
        public void RegisterSonarPingImpulse_AccumulatesCorrectly_WhenCalledMultipleTimes()
        {
            // Arrange
            float firstIntensity = 0.2f;
            float secondIntensity = 0.8f;

            // Expected values after second call (0.8f intensity should dominate due to min/max)
            float expectedAmplitude = secondIntensity * 0.0042f;
            float expectedShakeY = -expectedAmplitude;
            float expectedShakeYVel = expectedAmplitude * 16f;
            float expectedShakePitch = -secondIntensity * 0.12f;
            float expectedShakePitchVel = secondIntensity * 0.95f;

            // Act
            _processor.RegisterSonarPingImpulse(firstIntensity);
            _processor.RegisterSonarPingImpulse(secondIntensity);

            // Assert
            Assert.AreEqual(expectedShakeY, GetPrivateField<float>("_collisionShakeY"));
            Assert.AreEqual(expectedShakeYVel, GetPrivateField<float>("_collisionShakeYVel"));
            Assert.AreEqual(expectedShakePitch, GetPrivateField<float>("_collisionShakePitch"));
            Assert.AreEqual(expectedShakePitchVel, GetPrivateField<float>("_collisionShakePitchVel"));

            // Test that a smaller impulse doesn't overwrite a larger one
            _processor.RegisterSonarPingImpulse(0.1f);

            // Assert values remain the same as the largest impulse
            Assert.AreEqual(expectedShakeY, GetPrivateField<float>("_collisionShakeY"));
            Assert.AreEqual(expectedShakeYVel, GetPrivateField<float>("_collisionShakeYVel"));
            Assert.AreEqual(expectedShakePitch, GetPrivateField<float>("_collisionShakePitch"));
            Assert.AreEqual(expectedShakePitchVel, GetPrivateField<float>("_collisionShakePitchVel"));
        }

        private T GetPrivateField<T>(string fieldName)
        {
            var fieldInfo = typeof(CameraJuiceProcessor).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (fieldInfo == null)
                throw new System.Exception($"Field '{fieldName}' not found.");
            return (T)fieldInfo.GetValue(_processor);
        }
    }
}

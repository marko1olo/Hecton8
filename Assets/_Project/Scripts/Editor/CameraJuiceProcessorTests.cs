#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Gameplay;

namespace Hecton8.Tests
{
    [TestFixture]
    public class CameraJuiceProcessorTests
    {
        private CameraJuiceProcessor _processor;
        private SuitData _suitData;

        [SetUp]
        public void Setup()
        {
            _processor = new CameraJuiceProcessor();
            _suitData = ScriptableObject.CreateInstance<SuitData>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_suitData != null)
            {
                UnityEngine.Object.DestroyImmediate(_suitData);
            }
        }

        [Test]
        public void RegisterCollisionImpulse_WhenNullSuit_DoesNothing()
        {
            _processor.RegisterCollisionImpulse(10f, null);

            var shakeYField = typeof(CameraJuiceProcessor).GetField("_collisionShakeY", BindingFlags.NonPublic | BindingFlags.Instance);
            float shakeY = (float)shakeYField.GetValue(_processor);

            Assert.AreEqual(0f, shakeY);
        }

        [Test]
        public void RegisterCollisionImpulse_WhenCollisionShakeDisabled_DoesNothing()
        {
            _suitData.enableCollisionShake = false;
            _suitData.collisionShakeThreshold = 2f;

            _processor.RegisterCollisionImpulse(10f, _suitData);

            var shakeYField = typeof(CameraJuiceProcessor).GetField("_collisionShakeY", BindingFlags.NonPublic | BindingFlags.Instance);
            float shakeY = (float)shakeYField.GetValue(_processor);

            Assert.AreEqual(0f, shakeY);
        }

        [Test]
        public void RegisterCollisionImpulse_WhenRelativeSpeedBelowThreshold_DoesNothing()
        {
            _suitData.enableCollisionShake = true;
            _suitData.collisionShakeThreshold = 5f;

            _processor.RegisterCollisionImpulse(3f, _suitData);

            var shakeYField = typeof(CameraJuiceProcessor).GetField("_collisionShakeY", BindingFlags.NonPublic | BindingFlags.Instance);
            float shakeY = (float)shakeYField.GetValue(_processor);

            Assert.AreEqual(0f, shakeY);
        }

        [Test]
        public void RegisterCollisionImpulse_WhenRelativeSpeedAboveThreshold_AppliesImpulse()
        {
            _suitData.enableCollisionShake = true;
            _suitData.collisionShakeThreshold = 2f;
            _suitData.collisionShakeMaxVelocity = 12f;
            _suitData.collisionShakeMaxAmplitude = 0.05f;

            _processor.RegisterCollisionImpulse(12f, _suitData);

            var shakeYField = typeof(CameraJuiceProcessor).GetField("_collisionShakeY", BindingFlags.NonPublic | BindingFlags.Instance);
            float shakeY = (float)shakeYField.GetValue(_processor);

            Assert.AreNotEqual(0f, shakeY);

            // Expected norm is math.saturate((12 - 2) / max(12 - 2, 0.1f)) = 1.0f
            // Expected _collisionShakeY = -norm * collisionShakeMaxAmplitude = -0.05f
            Assert.AreEqual(-0.05f, shakeY, 0.001f);

            var shakeYVelField = typeof(CameraJuiceProcessor).GetField("_collisionShakeYVel", BindingFlags.NonPublic | BindingFlags.Instance);
            float shakeYVel = (float)shakeYVelField.GetValue(_processor);

            // Expected _collisionShakeYVel = norm * collisionShakeMaxAmplitude * 3f = 0.15f
            Assert.AreEqual(0.15f, shakeYVel, 0.001f);
        }

        [Test]
        public void RegisterCollisionImpulse_PartialSpeed_ScalesImpulse()
        {
            _suitData.enableCollisionShake = true;
            _suitData.collisionShakeThreshold = 2f;
            _suitData.collisionShakeMaxVelocity = 12f;
            _suitData.collisionShakeMaxAmplitude = 0.05f;

            // Halfway between threshold and max
            _processor.RegisterCollisionImpulse(7f, _suitData);

            var shakeYField = typeof(CameraJuiceProcessor).GetField("_collisionShakeY", BindingFlags.NonPublic | BindingFlags.Instance);
            float shakeY = (float)shakeYField.GetValue(_processor);

            // Expected norm is math.saturate((7 - 2) / max(12 - 2, 0.1f)) = 5 / 10 = 0.5f
            // Expected _collisionShakeY = -0.5 * 0.05 = -0.025f
            Assert.AreEqual(-0.025f, shakeY, 0.001f);
        }

        [Test]
        public void Initialize_ResetsStateToNeutral()
        {
            // Set up initial state with some dirty values using reflection
            typeof(CameraJuiceProcessor).GetField("_bobTimer", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_processor, 10f);
            typeof(CameraJuiceProcessor).GetField("_bobIntensity", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_processor, 0.5f);
            typeof(CameraJuiceProcessor).GetField("_currentRoll", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_processor, 5f);
            typeof(CameraJuiceProcessor).GetField("_momentumPitch", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_processor, 2f);
            typeof(CameraJuiceProcessor).GetField("_wasSubmerged", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_processor, true);

            // Call Initialize
            _processor.Initialize(true);

            // Assert that the state is reset
            Assert.AreEqual(0f, (float)typeof(CameraJuiceProcessor).GetField("_bobTimer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_processor));
            Assert.AreEqual(0f, (float)typeof(CameraJuiceProcessor).GetField("_bobIntensity", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_processor));
            Assert.AreEqual(0f, (float)typeof(CameraJuiceProcessor).GetField("_currentRoll", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_processor));
            Assert.AreEqual(0f, (float)typeof(CameraJuiceProcessor).GetField("_momentumPitch", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_processor));
            Assert.IsFalse((bool)typeof(CameraJuiceProcessor).GetField("_wasSubmerged", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_processor));

            // Assert roll sign
            Assert.AreEqual(-1f, (float)typeof(CameraJuiceProcessor).GetField("_rollSign", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_processor));

            // Call Initialize with false
            _processor.Initialize(false);

            // Assert roll sign
            Assert.AreEqual(1f, (float)typeof(CameraJuiceProcessor).GetField("_rollSign", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_processor));
        }
    }
}
#endif

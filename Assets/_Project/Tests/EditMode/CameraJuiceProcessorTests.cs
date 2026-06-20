using NUnit.Framework;
using Unity.Mathematics;
using Hecton8.Gameplay;
using UnityEngine;
using System.Reflection;

namespace Hecton8.Tests.EditMode
{
    [TestFixture]
    public class CameraJuiceProcessorTests
    {
        private CameraJuiceProcessor _processor;
        private FieldInfo _externalRollImpulseField;

        [SetUp]
        public void SetUp()
        {
            _processor = new CameraJuiceProcessor();

            // Use reflection to access private fields since we need to assert their state
            _externalRollImpulseField = typeof(CameraJuiceProcessor).GetField("_externalRollImpulse", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        [Test]
        public void RegisterExternalRollImpulse_SmallInput_Ignored()
        {
            _processor.RegisterExternalRollImpulse(0.0005f);

            float impulse = (float)_externalRollImpulseField.GetValue(_processor);

            Assert.AreEqual(0f, impulse, "Expected impulse to be 0 for small input");
        }

        [Test]
        public void RegisterExternalRollImpulse_ValidInput_SetsImpulse()
        {
            float input = 10f;
            _processor.RegisterExternalRollImpulse(input);

            float impulse = (float)_externalRollImpulseField.GetValue(_processor);

            Assert.AreEqual(10f, impulse, "Expected impulse to match input");
        }

        [Test]
        public void RegisterExternalRollImpulse_ExceedsMax_Clamped()
        {
            float input = 25f;
            _processor.RegisterExternalRollImpulse(input);

            float impulse = (float)_externalRollImpulseField.GetValue(_processor);

            Assert.AreEqual(18f, impulse, "Expected impulse to be clamped to 18");
        }

        [Test]
        public void RegisterExternalRollImpulse_ExceedsMin_Clamped()
        {
            float input = -25f;
            _processor.RegisterExternalRollImpulse(input);

            float impulse = (float)_externalRollImpulseField.GetValue(_processor);

            Assert.AreEqual(-18f, impulse, "Expected impulse to be clamped to -18");
        }
    }
}

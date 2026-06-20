using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using Unity.Mathematics;

namespace Hecton8.Tests
{
    [TestFixture]
    public class HectonPlayerMovementEnvironmentalDragTests
    {
        private GameObject _playerGo;
        private HectonPlayerMovement _movement;

        [SetUp]
        public void Setup()
        {
            _playerGo = new GameObject("PlayerMovementMock");
            _movement = _playerGo.AddComponent<HectonPlayerMovement>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_playerGo != null)
                Object.DestroyImmediate(_playerGo);
        }

        [Test]
        public void ApplyEnvironmentalDrag_NonFiniteInput_DoesNotUpdateState()
        {
            // Initial state
            SetPrivateField("_externalEnvironmentalDragRequestedMultiplier", 1f);
            SetPrivateField("_externalEnvironmentalDragRequestedThisStep", false);

            _movement.ApplyEnvironmentalDrag(float.NaN);

            Assert.AreEqual(1f, GetPrivateField<float>("_externalEnvironmentalDragRequestedMultiplier"));
            Assert.IsFalse(GetPrivateField<bool>("_externalEnvironmentalDragRequestedThisStep"));

            _movement.ApplyEnvironmentalDrag(float.PositiveInfinity);

            Assert.AreEqual(1f, GetPrivateField<float>("_externalEnvironmentalDragRequestedMultiplier"));
            Assert.IsFalse(GetPrivateField<bool>("_externalEnvironmentalDragRequestedThisStep"));

            _movement.ApplyEnvironmentalDrag(float.NegativeInfinity);

            Assert.AreEqual(1f, GetPrivateField<float>("_externalEnvironmentalDragRequestedMultiplier"));
            Assert.IsFalse(GetPrivateField<bool>("_externalEnvironmentalDragRequestedThisStep"));
        }

        [Test]
        public void ApplyEnvironmentalDrag_InputBelowOne_ClampsToOne()
        {
            SetPrivateField("_externalEnvironmentalDragRequestedMultiplier", 0.5f); // Should never happen normally, but tests the clamp
            SetPrivateField("_externalEnvironmentalDragHoldTimer", 1f);

            _movement.ApplyEnvironmentalDrag(0f);

            Assert.AreEqual(1f, GetPrivateField<float>("_externalEnvironmentalDragRequestedMultiplier"));
            Assert.IsTrue(GetPrivateField<bool>("_externalEnvironmentalDragRequestedThisStep"));
            Assert.AreEqual(0f, GetPrivateField<float>("_externalEnvironmentalDragHoldTimer"));

            SetPrivateField("_externalEnvironmentalDragRequestedThisStep", false);
            SetPrivateField("_externalEnvironmentalDragHoldTimer", 1f);

            _movement.ApplyEnvironmentalDrag(-5f);

            Assert.AreEqual(1f, GetPrivateField<float>("_externalEnvironmentalDragRequestedMultiplier"));
            Assert.IsTrue(GetPrivateField<bool>("_externalEnvironmentalDragRequestedThisStep"));
            Assert.AreEqual(0f, GetPrivateField<float>("_externalEnvironmentalDragHoldTimer"));
        }

        [Test]
        public void ApplyEnvironmentalDrag_ValidHighInput_UpdatesMultiplierAndTimer()
        {
            SetPrivateField("_externalEnvironmentalDragRequestedMultiplier", 1f);
            SetPrivateField("externalEnvironmentalDragHoldTime", 0.25f);

            _movement.ApplyEnvironmentalDrag(2.5f);

            Assert.AreEqual(2.5f, GetPrivateField<float>("_externalEnvironmentalDragRequestedMultiplier"));
            Assert.IsTrue(GetPrivateField<bool>("_externalEnvironmentalDragRequestedThisStep"));

            // Should be set to externalEnvironmentalDragHoldTime (0.25f)
            float timer = GetPrivateField<float>("_externalEnvironmentalDragHoldTimer");
            Assert.AreEqual(0.25f, timer, 0.001f);
        }

        [Test]
        public void ApplyEnvironmentalDrag_SuccessiveCalls_KeepsMaximum()
        {
            SetPrivateField("_externalEnvironmentalDragRequestedMultiplier", 1f);

            _movement.ApplyEnvironmentalDrag(2f);
            Assert.AreEqual(2f, GetPrivateField<float>("_externalEnvironmentalDragRequestedMultiplier"));

            _movement.ApplyEnvironmentalDrag(1.5f);
            Assert.AreEqual(2f, GetPrivateField<float>("_externalEnvironmentalDragRequestedMultiplier")); // Retains max

            _movement.ApplyEnvironmentalDrag(3f);
            Assert.AreEqual(3f, GetPrivateField<float>("_externalEnvironmentalDragRequestedMultiplier")); // Updates to new max
        }

        private void SetPrivateField(string fieldName, object value)
        {
            var field = typeof(HectonPlayerMovement).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
                field.SetValue(_movement, value);
        }

        private T GetPrivateField<T>(string fieldName)
        {
            var field = typeof(HectonPlayerMovement).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
                return (T)field.GetValue(_movement);
            return default;
        }
    }
}

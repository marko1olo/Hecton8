using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Unity.Mathematics;

namespace Hecton8.Tests.Editor
{
    public sealed class HectonPlayerMovementLocalGravityEditTests
    {
        private GameObject _go;
        private HectonPlayerMovement _movement;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestPlayer");
            _movement = _go.AddComponent<HectonPlayerMovement>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                UnityEngine.Object.DestroyImmediate(_go);
        }

        [Test]
        public void RequestLocalGravityOverride_ValidInput_UpdatesStateAndActivates()
        {
            Vector3 testGravity = new Vector3(0f, -9.81f, 0f);
            float testHold = 5f;

            _movement.RequestLocalGravityOverride(testGravity, testHold);

            bool isActive = (bool)GetPrivateField(_movement, "_localGravityOverrideActive");
            Vector3 overrideGravity = (Vector3)GetPrivateField(_movement, "_localGravityOverride");
            float overrideTimer = (float)GetPrivateField(_movement, "_localGravityOverrideTimer");
            float blendTimer = (float)GetPrivateField(_movement, "_localGravityOverrideBlendTimer");

            Assert.IsTrue(isActive);
            Assert.AreEqual(testGravity, overrideGravity);
            Assert.AreEqual(testHold, overrideTimer);
            Assert.AreEqual(0f, blendTimer);
        }

        [Test]
        public void RequestLocalGravityOverride_ZeroGravity_IgnoresRequest()
        {
            _movement.RequestLocalGravityOverride(Vector3.zero, 5f);
            bool isActive = (bool)GetPrivateField(_movement, "_localGravityOverrideActive");
            Assert.IsFalse(isActive);
        }

        [Test]
        public void RequestLocalGravityOverride_ZeroHoldTime_IgnoresRequest()
        {
            _movement.RequestLocalGravityOverride(new Vector3(0f, -9.81f, 0f), 0f);
            bool isActive = (bool)GetPrivateField(_movement, "_localGravityOverrideActive");
            Assert.IsFalse(isActive);
        }

        [Test]
        public void RequestLocalGravityOverride_SameTarget_DoesNotResetBlendStart()
        {
            Vector3 testGravity = new Vector3(0f, -9.81f, 0f);

            // First call
            _movement.RequestLocalGravityOverride(testGravity, 5f);

            // Alter blend timer to verify it doesn't reset on same target
            SetPrivateField(_movement, "_localGravityOverrideBlendTimer", 1.5f);

            // Second call with same gravity
            _movement.RequestLocalGravityOverride(testGravity, 10f);

            float blendTimer = (float)GetPrivateField(_movement, "_localGravityOverrideBlendTimer");
            float overrideTimer = (float)GetPrivateField(_movement, "_localGravityOverrideTimer");

            Assert.AreEqual(1.5f, blendTimer); // Should not have reset
            Assert.AreEqual(10f, overrideTimer); // Max of current (5) and new (10)
        }

        private static object GetPrivateField(object obj, string fieldName)
        {
            FieldInfo field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field {fieldName} not found");
            return field.GetValue(obj);
        }

        private static void SetPrivateField(object obj, string fieldName, object value)
        {
            FieldInfo field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field {fieldName} not found");
            field.SetValue(obj, value);
        }
    }
}

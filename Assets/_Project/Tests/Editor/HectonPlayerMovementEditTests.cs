using NUnit.Framework;
using UnityEngine;
using Unity.Mathematics;
using System.Reflection;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public class HectonPlayerMovementEditTests
    {
        private HectonPlayerMovement _movement;
        private GameObject _go;

        // Use reflection to access private fields if needed.
        private FieldInfo _totalMassKgField;
        private FieldInfo _loadRatioField;
        private FieldInfo _load01Field;
        private FieldInfo _upwardSwimMultiplierField;
        private FieldInfo _loadMovementMultiplierField;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("PlayerMovement");
            _movement = _go.AddComponent<HectonPlayerMovement>();

            var type = typeof(HectonPlayerMovement);
            _totalMassKgField = type.GetField("_runtimeInventoryTotalMassKg", BindingFlags.NonPublic | BindingFlags.Instance);
            _loadRatioField = type.GetField("_runtimeInventoryLoadRatio", BindingFlags.NonPublic | BindingFlags.Instance);
            _load01Field = type.GetField("_runtimeInventoryLoad01", BindingFlags.NonPublic | BindingFlags.Instance);
            _upwardSwimMultiplierField = type.GetField("_runtimeInventoryUpwardSwimMultiplier", BindingFlags.NonPublic | BindingFlags.Instance);
            _loadMovementMultiplierField = type.GetField("_runtimeInventoryLoadMovementMultiplier", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void ApplyRuntimeInventoryMassLoad_ZeroMass_SetsNoPenalty()
        {
            _movement.ApplyRuntimeInventoryMassLoad(0f, 100f);

            Assert.AreEqual(0f, _totalMassKgField.GetValue(_movement));
            Assert.AreEqual(0f, _loadRatioField.GetValue(_movement));
            Assert.AreEqual(0f, _load01Field.GetValue(_movement));
            Assert.AreEqual(1f, _upwardSwimMultiplierField.GetValue(_movement));
            Assert.AreEqual(1f, _loadMovementMultiplierField.GetValue(_movement));
        }

        [Test]
        public void ApplyRuntimeInventoryMassLoad_NegativeMass_ClampsToZero()
        {
            _movement.ApplyRuntimeInventoryMassLoad(-50f, 100f);

            Assert.AreEqual(0f, _totalMassKgField.GetValue(_movement));
            Assert.AreEqual(0f, _loadRatioField.GetValue(_movement));
        }

        [Test]
        public void ApplyRuntimeInventoryMassLoad_PartialCapacity_CalculatesLerpedPenalties()
        {
            // 50kg / 100kg = 0.5 ratio
            _movement.ApplyRuntimeInventoryMassLoad(50f, 100f);

            Assert.AreEqual(50f, _totalMassKgField.GetValue(_movement));
            Assert.AreEqual(0.5f, _loadRatioField.GetValue(_movement));
            Assert.AreEqual(0.5f, _load01Field.GetValue(_movement));

            // Swim minimum is 0.6. Lerp(1f, 0.6f, 0.5f) = 0.8f
            Assert.AreEqual(0.8f, _upwardSwimMultiplierField.GetValue(_movement));

            // Movement minimum is 0.5. Lerp(1f, 0.5f, 0.5f) = 0.75f
            Assert.AreEqual(0.75f, _loadMovementMultiplierField.GetValue(_movement));
        }

        [Test]
        public void ApplyRuntimeInventoryMassLoad_ExactCapacity_SetsMaxPenalty()
        {
            // 100kg / 100kg = 1.0 ratio
            _movement.ApplyRuntimeInventoryMassLoad(100f, 100f);

            Assert.AreEqual(100f, _totalMassKgField.GetValue(_movement));
            Assert.AreEqual(1.0f, _loadRatioField.GetValue(_movement));
            Assert.AreEqual(1.0f, _load01Field.GetValue(_movement));

            Assert.AreEqual(0.6f, _upwardSwimMultiplierField.GetValue(_movement)); // 0.6 minimum
            Assert.AreEqual(0.5f, _loadMovementMultiplierField.GetValue(_movement)); // 0.5 minimum
        }

        [Test]
        public void ApplyRuntimeInventoryMassLoad_OverCapacity_SaturatesLoad01()
        {
            // 120kg / 100kg = 1.2 ratio
            _movement.ApplyRuntimeInventoryMassLoad(120f, 100f);

            Assert.AreEqual(120f, _totalMassKgField.GetValue(_movement));
            Assert.AreEqual(1.2f, _loadRatioField.GetValue(_movement));
            Assert.AreEqual(1.0f, _load01Field.GetValue(_movement)); // saturated to 1.0

            Assert.AreEqual(0.6f, _upwardSwimMultiplierField.GetValue(_movement));
            Assert.AreEqual(0.5f, _loadMovementMultiplierField.GetValue(_movement));
        }

        [Test]
        public void ApplyRuntimeInventoryMassLoad_CriticalCapacity_SetsCriticalEncumbrance()
        {
            // 150kg / 100kg = 1.5 ratio
            _movement.ApplyRuntimeInventoryMassLoad(150f, 100f);

            Assert.AreEqual(150f, _totalMassKgField.GetValue(_movement));
            Assert.AreEqual(1.5f, _loadRatioField.GetValue(_movement));
            Assert.AreEqual(1.0f, _load01Field.GetValue(_movement));
        }

        [Test]
        public void ApplyRuntimeInventoryMassLoad_ZeroCapacity_AvoidsDivideByZeroAndDefaultsToMax()
        {
            // Capacity 0 -> math.max(0.01f, 0f) = 0.01f. 10f / 0.01f = 1000f ratio.
            _movement.ApplyRuntimeInventoryMassLoad(10f, 0f);

            Assert.AreEqual(10f, _totalMassKgField.GetValue(_movement));
            Assert.AreEqual(1000f, _loadRatioField.GetValue(_movement));
            Assert.AreEqual(1.0f, _load01Field.GetValue(_movement));
        }

        [Test]
        public void ApplyRuntimeInventoryMassLoad_CachedVersion_RespectsCachedValues()
        {
            // 50kg / 100kg = 0.5 actual ratio.
            // But we provide cachedMovementMultiplier = 0.9f and cachedLoad01 = 0.2f
            _movement.ApplyRuntimeInventoryMassLoad(50f, 100f, 0.9f, 0.2f);

            Assert.AreEqual(50f, _totalMassKgField.GetValue(_movement));
            Assert.AreEqual(0.5f, _loadRatioField.GetValue(_movement)); // Ratio is still calculated fresh

            Assert.AreEqual(0.2f, _load01Field.GetValue(_movement)); // Load01 uses cached

            // Swim multiplier is derived from cached Load01 (0.2). Lerp(1f, 0.6f, 0.2f) = 0.92f
            Assert.IsTrue(Mathf.Abs(0.92f - (float)_upwardSwimMultiplierField.GetValue(_movement)) < 0.001f);

            // Movement multiplier uses cached explicitly (0.9f)
            Assert.AreEqual(0.9f, _loadMovementMultiplierField.GetValue(_movement));
        }

        [Test]
        public void ApplyRuntimeInventoryMassLoad_CachedVersion_ClampsCachedValues()
        {
            // cachedLoad01 > 1.0f should be saturated to 1.0f
            // cachedMovementMultiplier < minimum (0.5f) should be clamped to 0.5f
            _movement.ApplyRuntimeInventoryMassLoad(100f, 100f, 0.1f, 5.0f);

            Assert.AreEqual(1.0f, _load01Field.GetValue(_movement)); // Saturated 5.0 -> 1.0

            // Swim multiplier uses saturated load01 (1.0). Lerp(1f, 0.6f, 1.0f) = 0.6f
            Assert.AreEqual(0.6f, _upwardSwimMultiplierField.GetValue(_movement));

            // Movement multiplier uses clamped explicitly. 0.1f is less than 0.5f so clamped to 0.5f.
            Assert.AreEqual(0.5f, _loadMovementMultiplierField.GetValue(_movement));
        }
    }
}

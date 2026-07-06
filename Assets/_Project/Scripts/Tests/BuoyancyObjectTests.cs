using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using Hecton8.Physics;

namespace Hecton8.Tests
{
    [TestFixture]
    public class BuoyancyObjectTests
    {
        private GameObject _go;
        private BuoyancyObject _buoyancyObject;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestBuoyancyObject");
            // Add required Rigidbody as mentioned in comments
            _go.AddComponent<Rigidbody>();
            _buoyancyObject = _go.AddComponent<BuoyancyObject>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }
        }

        private void SetPrivateField(string fieldName, object value)
        {
            var field = typeof(BuoyancyObject).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field {fieldName} not found");
            field.SetValue(_buoyancyObject, value);
        }

        [Test]
        public void ShouldSuppressFluid_ExternallySuppressed_ReturnsTrue()
        {
            _buoyancyObject.SetExternalSuppression(true);
            Assert.IsTrue(_buoyancyObject.ShouldSuppressFluid(0f));
        }

        [Test]
        public void ShouldSuppressFluid_NotGrounded_ReturnsFalse()
        {
            _buoyancyObject.SetExternalSuppression(false);
            SetPrivateField("_isGrounded", false);
            Assert.IsFalse(_buoyancyObject.ShouldSuppressFluid(0f));
        }

        [Test]
        public void ShouldSuppressFluid_Grounded_WithoutCollider_AboveWaterLevel_ReturnsTrue()
        {
            _buoyancyObject.SetExternalSuppression(false);
            SetPrivateField("_isGrounded", true);

            // Need to set _cachedTransform since it's initialized in Awake and Awake doesn't run in Edit mode tests automatically
            SetPrivateField("_cachedTransform", _go.transform);

            // Set height
            SetPrivateField("height", 2f);

            _go.transform.position = new Vector3(0, 5f, 0); // bottomY = 5 - max(0.05, 1) = 4

            // Water level = 0. bottomY (4) >= waterLevel (0) - 0.02f -> True
            Assert.IsTrue(_buoyancyObject.ShouldSuppressFluid(0f));
        }

        [Test]
        public void ShouldSuppressFluid_Grounded_WithoutCollider_BelowWaterLevel_ReturnsFalse()
        {
            _buoyancyObject.SetExternalSuppression(false);
            SetPrivateField("_isGrounded", true);

            SetPrivateField("_cachedTransform", _go.transform);
            SetPrivateField("height", 2f);

            _go.transform.position = new Vector3(0, -5f, 0); // bottomY = -5 - 1 = -6

            // Water level = 0. bottomY (-6) >= waterLevel (0) - 0.02f -> False
            Assert.IsFalse(_buoyancyObject.ShouldSuppressFluid(0f));
        }

        [Test]
        public void ShouldSuppressFluid_Grounded_WithCollider_AboveWaterLevel_ReturnsTrue()
        {
            _buoyancyObject.SetExternalSuppression(false);
            SetPrivateField("_isGrounded", true);

            var collider = _go.AddComponent<BoxCollider>();
            collider.center = Vector3.zero;
            collider.size = new Vector3(1, 2, 1);
            _go.transform.position = new Vector3(0, 5f, 0); // bounds.min.y = 4

            SetPrivateField("_collider", collider);

            Assert.IsTrue(_buoyancyObject.ShouldSuppressFluid(0f));
        }

        [Test]
        public void ShouldSuppressFluid_Grounded_WithCollider_BelowWaterLevel_ReturnsFalse()
        {
            _buoyancyObject.SetExternalSuppression(false);
            SetPrivateField("_isGrounded", true);

            var collider = _go.AddComponent<BoxCollider>();
            collider.center = Vector3.zero;
            collider.size = new Vector3(1, 2, 1);
            _go.transform.position = new Vector3(0, -5f, 0); // bounds.min.y = -6

            SetPrivateField("_collider", collider);

            Assert.IsFalse(_buoyancyObject.ShouldSuppressFluid(0f));
        }
    }
}

#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public sealed class EntityChangeDetectorEditTests
    {
        [TearDown]
        public void TearDown()
        {
            // The tests create temporary objects, clean them up to not pollute state
            EntityChangeManager.RemoveDetector("test_entity");
            EntityChangeManager.RemoveDetector("TestEntity");
            EntityChangeManager.RemoveDetector("TestEntity1");
            EntityChangeManager.RemoveDetector("TestEntity2");
        }

        [Test]
        public void EntityChangeDetector_IsDirty_ReturnsTrue_IfExactFlagIsSet()
        {
            var detector = new EntityChangeDetector("test_entity");
            detector.MarkDirty(EntityChangeFlag.Health);

            Assert.IsTrue(detector.IsDirty(EntityChangeFlag.Health));
        }

        [Test]
        public void EntityChangeDetector_IsDirty_ReturnsTrue_IfFlagIsPartiallySet()
        {
            var detector = new EntityChangeDetector("test_entity");
            detector.MarkDirty(EntityChangeFlag.Health);

            // Checking for Position | Health, since Health is set, it should return true.
            Assert.IsTrue(detector.IsDirty(EntityChangeFlag.Position | EntityChangeFlag.Health));
        }

        [Test]
        public void EntityChangeDetector_IsDirty_ReturnsFalse_IfFlagIsNotSet()
        {
            var detector = new EntityChangeDetector("test_entity");
            detector.MarkDirty(EntityChangeFlag.Health);

            Assert.IsFalse(detector.IsDirty(EntityChangeFlag.Position));
        }

        [Test]
        public void EntityChangeDetector_IsDirty_ReturnsFalse_ForNone()
        {
            var detector = new EntityChangeDetector("test_entity");
            detector.MarkDirty(EntityChangeFlag.All);

            // Flag.None is 0, so bitwise AND will always be 0.
            Assert.IsFalse(detector.IsDirty(EntityChangeFlag.None));
        }

        [Test]
        public void EntityChangeDetector_NewInstance_HasNoDirtyFlags()
        {
            var detector = new EntityChangeDetector("test_entity");
            Assert.IsFalse(detector.IsDirty(EntityChangeFlag.All));
            Assert.IsFalse(detector.IsDirty(EntityChangeFlag.Position));
        }

        [Test]
        public void EntityChangeDetector_MarkDirty_SetsCorrectFlags()
        {
            var detector = new EntityChangeDetector("test_entity");
            detector.MarkDirty(EntityChangeFlag.Health);

            Assert.IsTrue(detector.IsDirty(EntityChangeFlag.Health));
            Assert.IsFalse(detector.IsDirty(EntityChangeFlag.Position));
            Assert.IsFalse(detector.IsDirty(EntityChangeFlag.State));

            detector.MarkDirty(EntityChangeFlag.Position | EntityChangeFlag.State);
            Assert.IsTrue(detector.IsDirty(EntityChangeFlag.Health));
            Assert.IsTrue(detector.IsDirty(EntityChangeFlag.Position));
            Assert.IsTrue(detector.IsDirty(EntityChangeFlag.State));
        }

        [Test]
        public void EntityChangeDetector_MarkDirty_WithNone_DoesNotChangeFlags()
        {
            var detector = new EntityChangeDetector("test_entity");
            detector.MarkDirty(EntityChangeFlag.Health);

            detector.MarkDirty(EntityChangeFlag.None);

            Assert.IsTrue(detector.IsDirty(EntityChangeFlag.Health));
            Assert.IsFalse(detector.IsDirty(EntityChangeFlag.Position));
            Assert.IsFalse(detector.IsDirty(EntityChangeFlag.All));
        }

        [Test]
        public void EntityChangeDetector_MarkDirty_WithAll_SetsAllFlags()
        {
            var detector = new EntityChangeDetector("test_entity");
            detector.MarkDirty(EntityChangeFlag.All);

            Assert.IsTrue(detector.IsDirty(EntityChangeFlag.Position));
            Assert.IsTrue(detector.IsDirty(EntityChangeFlag.Rotation));
            Assert.IsTrue(detector.IsDirty(EntityChangeFlag.Scale));
            Assert.IsTrue(detector.IsDirty(EntityChangeFlag.Health));
            Assert.IsTrue(detector.IsDirty(EntityChangeFlag.State));
            Assert.IsTrue(detector.IsDirty(EntityChangeFlag.Inventory));
            Assert.IsTrue(detector.IsDirty(EntityChangeFlag.Active));
            Assert.IsTrue(detector.IsDirty(EntityChangeFlag.Velocity));
            Assert.IsTrue(detector.IsDirty(EntityChangeFlag.All));
        }

        [Test]
        public void EntityChangeDetector_ClearDirty_ResetsAllFlags()
        {
            var detector = new EntityChangeDetector("test_entity");
            detector.MarkDirty(EntityChangeFlag.All);
            Assert.IsTrue(detector.IsDirty(EntityChangeFlag.All));

            detector.ClearDirty();
            Assert.IsFalse(detector.IsDirty(EntityChangeFlag.All));
        }

        [Test]
        public void EntityChangeDetector_ClearDirty_SetsDirtyFlagsToNone()
        {
            var detector = new EntityChangeDetector("test_entity");
            detector.MarkDirty(EntityChangeFlag.All);

            detector.ClearDirty();

            var fieldInfo = typeof(EntityChangeDetector).GetField("_dirtyFlags", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var actualFlags = (EntityChangeFlag)fieldInfo.GetValue(detector);

            Assert.AreEqual(EntityChangeFlag.None, actualFlags);
        }

        [Test]
        public void EntityChangeDetector_FlushChanges_InvokesCallbackAndClearsFlag_WhenValueChanges()
        {
            var detector = new EntityChangeDetector("test_entity");
            detector.MarkDirty(EntityChangeFlag.Health);

            bool callbackInvoked = false;
            float oldValue = -1f;
            float newValue = -1f;

            detector.OnHealthChanged += (oldVal, newVal) =>
            {
                callbackInvoked = true;
                oldValue = oldVal;
                newValue = newVal;
            };

            // By default _lastHealth is 1f
            detector.FlushChanges(currentHealth: 0.5f);

            Assert.IsTrue(callbackInvoked);
            Assert.AreEqual(1f, oldValue);
            Assert.AreEqual(0.5f, newValue);
            Assert.IsFalse(detector.IsDirty(EntityChangeFlag.Health), "Dirty flag should be cleared after flush");
        }

        [Test]
        public void EntityChangeDetector_FlushChanges_DoesNotInvokeCallback_WhenValueIsSame()
        {
            var detector = new EntityChangeDetector("test_entity");
            detector.MarkDirty(EntityChangeFlag.Health);

            bool callbackInvoked = false;
            detector.OnHealthChanged += (oldVal, newVal) => callbackInvoked = true;

            // By default _lastHealth is 1f
            detector.FlushChanges(currentHealth: 1f);

            Assert.IsFalse(callbackInvoked);
            Assert.IsFalse(detector.IsDirty(EntityChangeFlag.Health), "Dirty flag should be cleared even if no change occurred");
        }

        [Test]
        public void EntityChangeDetector_FlushChanges_DoesNotInvokeCallback_WhenFlagIsNotDirty()
        {
            var detector = new EntityChangeDetector("test_entity");
            // We do NOT mark Health as dirty

            bool callbackInvoked = false;
            detector.OnHealthChanged += (oldVal, newVal) => callbackInvoked = true;

            detector.FlushChanges(currentHealth: 0.5f);

            Assert.IsFalse(callbackInvoked);
        }

        [Test]
        public void EntityChangeDetector_FlushTransformChanges_InvokesExpectedCallbacks()
        {
            var detector = new EntityChangeDetector("test_entity");
            detector.MarkDirty(EntityChangeFlag.Position | EntityChangeFlag.Scale);

            bool positionChanged = false;
            detector.OnPositionChanged += (oldVal, newVal) => positionChanged = true;
            bool rotationChanged = false;
            detector.OnRotationChanged += (oldVal, newVal) => rotationChanged = true;
            bool scaleChanged = false;
            detector.OnScaleChanged += (oldVal, newVal) => scaleChanged = true;

            var go = new GameObject("TestObject");
            go.transform.position = new Vector3(1, 2, 3);
            go.transform.localScale = new Vector3(2, 2, 2);

            detector.FlushTransformChanges(go.transform);

            UnityEngine.Object.DestroyImmediate(go);

            Assert.IsTrue(positionChanged);
            Assert.IsTrue(scaleChanged);
            Assert.IsFalse(rotationChanged, "Rotation callback should not be invoked as it was not dirty");
            Assert.IsFalse(detector.IsDirty(EntityChangeFlag.All), "All dirty flags should be cleared");
        }

        [Test]
        public void EntityChangeDetector_FlushChanges_InvokesCallbacksForPosition_WhenPositionChanges()
        {
            var detector = new EntityChangeDetector("TestEntity");

            bool positionChangedCalled = false;
            Vector3 oldPos = Vector3.zero;
            Vector3 newPos = Vector3.zero;

            detector.OnPositionChanged += (oldV, newV) =>
            {
                positionChangedCalled = true;
                oldPos = oldV;
                newPos = newV;
            };

            detector.MarkDirty(EntityChangeFlag.Position);

            // Value hasn't changed from default zero, so we need to pass a non-zero to trigger
            detector.FlushChanges(currentPos: new Vector3(1, 2, 3));

            Assert.IsTrue(positionChangedCalled);
            Assert.AreEqual(Vector3.zero, oldPos);
            Assert.AreEqual(new Vector3(1, 2, 3), newPos);
            Assert.IsFalse(detector.IsDirty(EntityChangeFlag.Position));
        }

        [Test]
        public void EntityChangeDetector_GetOrCreateDetector_ReturnsSameInstanceForSameId()
        {
            var detector1 = EntityChangeManager.GetOrCreateDetector("TestEntity1");
            var detector2 = EntityChangeManager.GetOrCreateDetector("TestEntity1");
            var detector3 = EntityChangeManager.GetOrCreateDetector("TestEntity2");

            Assert.IsNotNull(detector1);
            Assert.AreSame(detector1, detector2);
            Assert.AreNotSame(detector1, detector3);
        }

        [Test]
        public void EntityChangeDetector_RemoveDetector_RemovesInstanceFromRegistry()
        {
            var detector1 = EntityChangeManager.GetOrCreateDetector("TestEntity1");
            Assert.AreEqual(1, EntityChangeManager.GetActiveDetectorCount());

            EntityChangeManager.RemoveDetector("TestEntity1");
            Assert.AreEqual(0, EntityChangeManager.GetActiveDetectorCount());

            // A new call should return a new instance
            var detector2 = EntityChangeManager.GetOrCreateDetector("TestEntity1");
            Assert.AreNotSame(detector1, detector2);
            Assert.AreEqual(1, EntityChangeManager.GetActiveDetectorCount());
        }
    }
}
#endif

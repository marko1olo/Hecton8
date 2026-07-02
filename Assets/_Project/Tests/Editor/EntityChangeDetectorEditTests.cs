#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Tests.Editor
{
    public sealed class EntityChangeDetectorEditTests
    {
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
        public void EntityChangeDetector_ClearDirty_ResetsAllFlags()
        {
            var detector = new EntityChangeDetector("test_entity");
            detector.MarkDirty(EntityChangeFlag.All);
            Assert.IsTrue(detector.IsDirty(EntityChangeFlag.All));

            detector.ClearDirty();
            Assert.IsFalse(detector.IsDirty(EntityChangeFlag.All));
            Assert.IsFalse(detector.IsDirty(EntityChangeFlag.Position));
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
    }
}
#endif

#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Tests.Editor
{
    public class EntityChangeDetectorEditTests
    {
        [TearDown]
        public void TearDown()
        {
            // The tests create temporary objects, clean them up to not pollute state
            // Let's also remove any detectors from the global manager that were created
            EntityChangeManager.RemoveDetector("TestEntity");
            EntityChangeManager.RemoveDetector("TestEntity1");
            EntityChangeManager.RemoveDetector("TestEntity2");
        }

        [Test]
        public void MarkDirty_SetsCorrectFlags()
        {
            var detector = new EntityChangeDetector("TestEntity");

            detector.MarkDirty(EntityChangeFlag.Position);
            Assert.IsTrue(detector.IsDirty(EntityChangeFlag.Position));
            Assert.IsFalse(detector.IsDirty(EntityChangeFlag.Rotation));

            detector.MarkDirty(EntityChangeFlag.Rotation | EntityChangeFlag.Health);
            Assert.IsTrue(detector.IsDirty(EntityChangeFlag.Position));
            Assert.IsTrue(detector.IsDirty(EntityChangeFlag.Rotation));
            Assert.IsTrue(detector.IsDirty(EntityChangeFlag.Health));
            Assert.IsFalse(detector.IsDirty(EntityChangeFlag.Scale));
        }

        [Test]
        public void ClearDirty_RemovesAllFlags()
        {
            var detector = new EntityChangeDetector("TestEntity");
            detector.MarkDirty(EntityChangeFlag.All);

            Assert.IsTrue(detector.IsDirty(EntityChangeFlag.Position));

            detector.ClearDirty();

            Assert.IsFalse(detector.IsDirty(EntityChangeFlag.Position));
            Assert.IsFalse(detector.IsDirty(EntityChangeFlag.Health));
            Assert.IsFalse(detector.IsDirty(EntityChangeFlag.All));
        }

        [Test]
        public void FlushChanges_InvokesCallbacksForDirtyProperties()
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

            // Verify flags are cleared after flush
            Assert.IsFalse(detector.IsDirty(EntityChangeFlag.Position));
        }

        [Test]
        public void FlushChanges_DoesNotInvokeCallbacksIfValueUnchanged()
        {
            var detector = new EntityChangeDetector("TestEntity");

            bool positionChangedCalled = false;
            detector.OnPositionChanged += (oldV, newV) => positionChangedCalled = true;

            detector.MarkDirty(EntityChangeFlag.Position);

            // Pass the default Vector3.zero, which matches _lastPosition
            detector.FlushChanges(currentPos: Vector3.zero);

            Assert.IsFalse(positionChangedCalled);

            // Flags should still be cleared even if no callback was fired
            Assert.IsFalse(detector.IsDirty(EntityChangeFlag.Position));
        }

        [Test]
        public void FlushTransformChanges_CorrectlyTriggersCallbacks()
        {
            var detector = new EntityChangeDetector("TestEntity");

            bool positionChangedCalled = false;
            detector.OnPositionChanged += (oldV, newV) => positionChangedCalled = true;

            var go = new GameObject("TestGo");
            go.transform.position = new Vector3(10, 0, 0);

            try
            {
                detector.MarkDirty(EntityChangeFlag.Position);
                detector.FlushTransformChanges(go.transform);

                Assert.IsTrue(positionChangedCalled);
                Assert.IsFalse(detector.IsDirty(EntityChangeFlag.Position));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void GetOrCreateDetector_ReturnsSameInstanceForSameId()
        {
            var detector1 = EntityChangeManager.GetOrCreateDetector("TestEntity1");
            var detector2 = EntityChangeManager.GetOrCreateDetector("TestEntity1");
            var detector3 = EntityChangeManager.GetOrCreateDetector("TestEntity2");

            Assert.IsNotNull(detector1);
            Assert.AreSame(detector1, detector2);
            Assert.AreNotSame(detector1, detector3);
        }

        [Test]
        public void RemoveDetector_RemovesInstanceFromRegistry()
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

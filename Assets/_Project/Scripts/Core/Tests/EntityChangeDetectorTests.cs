using NUnit.Framework;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Core.Tests
{
    [TestFixture]
    public class EntityChangeDetectorTests
    {
        private GameObject _tempGo;

        [TearDown]
        public void TearDown()
        {
            if (_tempGo != null)
            {
                Object.DestroyImmediate(_tempGo);
                _tempGo = null;
            }
        }

        [Test]
        public void EntityChangeDetector_MarkDirty_SetsFlagsCorrectly()
        {
            var detector = new EntityChangeDetector("test_entity");
            detector.MarkDirty(EntityChangeFlag.Position | EntityChangeFlag.Health);

            Assert.IsTrue(detector.IsDirty(EntityChangeFlag.Position));
            Assert.IsTrue(detector.IsDirty(EntityChangeFlag.Health));
            Assert.IsFalse(detector.IsDirty(EntityChangeFlag.Rotation));
        }

        [Test]
        public void EntityChangeDetector_ClearDirty_ResetsAllFlags()
        {
            var detector = new EntityChangeDetector("test_entity");
            detector.MarkDirty(EntityChangeFlag.All);
            detector.ClearDirty();

            Assert.IsFalse(detector.IsDirty(EntityChangeFlag.Position));
            Assert.IsFalse(detector.IsDirty(EntityChangeFlag.All));
        }

        [Test]
        public void EntityChangeDetector_FlushChanges_InvokesEventsOnValueChange()
        {
            var detector = new EntityChangeDetector("test_entity");

            int positionInvokes = 0;
            Vector3 lastPosOld = Vector3.zero;
            Vector3 lastPosNew = Vector3.zero;

            detector.OnPositionChanged += (oldVal, newVal) =>
            {
                positionInvokes++;
                lastPosOld = oldVal;
                lastPosNew = newVal;
            };

            detector.MarkDirty(EntityChangeFlag.Position);

            var newPos = new Vector3(1f, 2f, 3f);
            detector.FlushChanges(currentPos: newPos);

            Assert.AreEqual(1, positionInvokes, "Event should be invoked once.");
            Assert.AreEqual(Vector3.zero, lastPosOld, "Old value should be default (zero).");
            Assert.AreEqual(newPos, lastPosNew, "New value should match flushed value.");

            // It should clear the flag after flush
            Assert.IsFalse(detector.IsDirty(EntityChangeFlag.Position));
        }

        [Test]
        public void EntityChangeDetector_FlushChanges_DoesNotInvokeIfValueUnchanged()
        {
            var detector = new EntityChangeDetector("test_entity");

            int positionInvokes = 0;
            detector.OnPositionChanged += (oldVal, newVal) => positionInvokes++;

            // Set initial value
            detector.MarkDirty(EntityChangeFlag.Position);
            detector.FlushChanges(currentPos: Vector3.zero); // initial value is zero

            Assert.AreEqual(0, positionInvokes, "Event should not invoke if value is unchanged from default.");

            // Change it
            detector.MarkDirty(EntityChangeFlag.Position);
            detector.FlushChanges(currentPos: Vector3.one);
            Assert.AreEqual(1, positionInvokes, "Event should invoke on change.");

            // Flush same value again
            detector.MarkDirty(EntityChangeFlag.Position);
            detector.FlushChanges(currentPos: Vector3.one);
            Assert.AreEqual(1, positionInvokes, "Event should not invoke if value hasn't changed since last update.");
        }

        [Test]
        public void EntityChangeDetector_FlushTransformChanges_InvokesProperEvents()
        {
            var detector = new EntityChangeDetector("test_entity");

            int posInvokes = 0;
            int rotInvokes = 0;
            int scaleInvokes = 0;
            int activeInvokes = 0;

            detector.OnPositionChanged += (o, n) => posInvokes++;
            detector.OnRotationChanged += (o, n) => rotInvokes++;
            detector.OnScaleChanged += (o, n) => scaleInvokes++;
            detector.OnActiveChanged += (o, n) => activeInvokes++;

            _tempGo = new GameObject("TestTransform");
            _tempGo.transform.position = new Vector3(1, 1, 1);
            _tempGo.transform.rotation = Quaternion.Euler(0, 90, 0);
            _tempGo.transform.localScale = new Vector3(2, 2, 2);
            _tempGo.SetActive(false); // Default true -> false

            detector.MarkDirty(EntityChangeFlag.Position | EntityChangeFlag.Rotation | EntityChangeFlag.Scale | EntityChangeFlag.Active);

            detector.FlushTransformChanges(_tempGo.transform);

            Assert.AreEqual(1, posInvokes);
            Assert.AreEqual(1, rotInvokes);
            Assert.AreEqual(1, scaleInvokes);
            Assert.AreEqual(1, activeInvokes);
        }
    }
}

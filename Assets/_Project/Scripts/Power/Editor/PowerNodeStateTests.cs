#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Power;

namespace Hecton8.Tests.Editor
{
    public class PowerNodeStateTests
    {
        private GameObject _nodeObj;
        private PowerNode _node;

        [SetUp]
        public void SetUp()
        {
            _nodeObj = new GameObject("PowerNode");
            _node = _nodeObj.AddComponent<PowerNode>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_nodeObj != null)
                Object.DestroyImmediate(_nodeObj);
        }

        [Test]
        public void SetGrid_SetsGridProperty()
        {
            var grid = new PowerGrid();
            _node.SetGrid(grid);
            Assert.AreEqual(grid, _node.Grid);

            _node.SetGrid(null);
            Assert.IsNull(_node.Grid);
        }

        [Test]
        public void SetRuptured_ChangesStateAndIncrementsRevision()
        {
            var isRupturedField = typeof(PowerNode).GetField("_isRuptured", BindingFlags.NonPublic | BindingFlags.Instance);
            var topologyRevisionField = typeof(PowerNode).GetField("_topologyRevision", BindingFlags.NonPublic | BindingFlags.Instance);

            bool initialIsRuptured = (bool)isRupturedField.GetValue(_node);
            int initialRevision = (int)topologyRevisionField.GetValue(_node);

            Assert.IsFalse(initialIsRuptured, "Initial _isRuptured should be false.");

            MethodInfo setRupturedMethod = typeof(PowerNode).GetMethod("SetRuptured", BindingFlags.NonPublic | BindingFlags.Instance);
            setRupturedMethod.Invoke(_node, new object[] { true });

            bool newIsRuptured = (bool)isRupturedField.GetValue(_node);
            int newRevision = (int)topologyRevisionField.GetValue(_node);

            Assert.IsTrue(newIsRuptured, "_isRuptured should be true after SetRuptured(true).");
            Assert.AreEqual(initialRevision + 1, newRevision, "_topologyRevision should be incremented.");
        }

        [Test]
        public void SetRuptured_SameValue_DoesNotIncrementRevision()
        {
            var topologyRevisionField = typeof(PowerNode).GetField("_topologyRevision", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo setRupturedMethod = typeof(PowerNode).GetMethod("SetRuptured", BindingFlags.NonPublic | BindingFlags.Instance);

            setRupturedMethod.Invoke(_node, new object[] { true });
            int revisionAfterFirst = (int)topologyRevisionField.GetValue(_node);

            setRupturedMethod.Invoke(_node, new object[] { true });
            int revisionAfterSecond = (int)topologyRevisionField.GetValue(_node);

            Assert.AreEqual(revisionAfterFirst, revisionAfterSecond, "_topologyRevision should not increment if value is unchanged.");
        }

        [Test]
        public void SetShortCircuited_ChangesStateAndIncrementsRevision()
        {
            var isShortCircuitedField = typeof(PowerNode).GetField("_isShortCircuited", BindingFlags.NonPublic | BindingFlags.Instance);
            var topologyRevisionField = typeof(PowerNode).GetField("_topologyRevision", BindingFlags.NonPublic | BindingFlags.Instance);

            bool initialIsShortCircuited = (bool)isShortCircuitedField.GetValue(_node);
            int initialRevision = (int)topologyRevisionField.GetValue(_node);

            Assert.IsFalse(initialIsShortCircuited, "Initial _isShortCircuited should be false.");

            MethodInfo setShortCircuitedMethod = typeof(PowerNode).GetMethod("SetShortCircuited", BindingFlags.NonPublic | BindingFlags.Instance);
            setShortCircuitedMethod.Invoke(_node, new object[] { true });

            bool newIsShortCircuited = (bool)isShortCircuitedField.GetValue(_node);
            int newRevision = (int)topologyRevisionField.GetValue(_node);

            Assert.IsTrue(newIsShortCircuited, "_isShortCircuited should be true after SetShortCircuited(true).");
            Assert.AreEqual(initialRevision + 1, newRevision, "_topologyRevision should be incremented.");
        }

        [Test]
        public void SetShortCircuited_SameValue_DoesNotIncrementRevision()
        {
            var topologyRevisionField = typeof(PowerNode).GetField("_topologyRevision", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo setShortCircuitedMethod = typeof(PowerNode).GetMethod("SetShortCircuited", BindingFlags.NonPublic | BindingFlags.Instance);

            setShortCircuitedMethod.Invoke(_node, new object[] { true });
            int revisionAfterFirst = (int)topologyRevisionField.GetValue(_node);

            setShortCircuitedMethod.Invoke(_node, new object[] { true });
            int revisionAfterSecond = (int)topologyRevisionField.GetValue(_node);

            Assert.AreEqual(revisionAfterFirst, revisionAfterSecond, "_topologyRevision should not increment if value is unchanged.");
        }
    }
}
#endif

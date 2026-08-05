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

        [Test]
        public void SetRuntimeActivation01_ClampsValueBetween0And1()
        {
            // Initial activation is 1f
            Assert.IsTrue(_node.SetRuntimeActivation01(1.5f) == false, "Should return false if value is clamped to existing 1f");

            // Set to 0.5
            Assert.IsTrue(_node.SetRuntimeActivation01(0.5f));

            // Set to -1f, should clamp to 0f
            Assert.IsTrue(_node.SetRuntimeActivation01(-1f));

            var field = typeof(PowerNode).GetField("_runtimeActivation01", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.That((float)field.GetValue(_node), Is.EqualTo(0f).Within(0.0001f), "Value should be clamped to 0f");
        }

        [Test]
        public void SetRuntimeActivation01_UnchangedValue_ReturnsFalse()
        {
            // Initial is 1f
            Assert.IsFalse(_node.SetRuntimeActivation01(1f));
            Assert.IsFalse(_node.SetRuntimeActivation01(1.00005f)); // Within epsilon
        }

        [Test]
        public void SetRuntimeActivation01_ConductivityChange_IncrementsTopologyRevision()
        {
            var topologyRevisionField = typeof(PowerNode).GetField("_topologyRevision", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            int initialRevision = (int)topologyRevisionField.GetValue(_node);

            // Change from 1f to 0f (conductive -> non-conductive)
            _node.SetRuntimeActivation01(0f);

            int newRevision = (int)topologyRevisionField.GetValue(_node);
            Assert.AreEqual(initialRevision + 1, newRevision, "Topology revision should increment when conductivity changes");

            // Change from 0f to 0.00005f (still non-conductive, threshold is 0.0001f)
            _node.SetRuntimeActivation01(0.00005f);

            int nextRevision = (int)topologyRevisionField.GetValue(_node);
            Assert.AreEqual(newRevision, nextRevision, "Topology revision should NOT increment when conductivity state is unchanged");

            // Change to 0.5f (conductive)
            _node.SetRuntimeActivation01(0.5f);

            int finalRevision = (int)topologyRevisionField.GetValue(_node);
            Assert.AreEqual(nextRevision + 1, finalRevision, "Topology revision should increment when returning to conductive");
        }

        [Test]
        public void SetRuntimeActivation01_NaNAndInfinity_DefaultsToOne()
        {
            _node.SetRuntimeActivation01(0f); // Reset to 0
            Assert.IsTrue(_node.SetRuntimeActivation01(float.NaN), "NaN should be converted to 1f");

            var field = typeof(PowerNode).GetField("_runtimeActivation01", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.That((float)field.GetValue(_node), Is.EqualTo(1f).Within(0.0001f), "Value should be 1f for NaN");

            _node.SetRuntimeActivation01(0f); // Reset to 0
            Assert.IsTrue(_node.SetRuntimeActivation01(float.PositiveInfinity), "Infinity should be converted to 1f");
            Assert.That((float)field.GetValue(_node), Is.EqualTo(1f).Within(0.0001f), "Value should be 1f for Infinity");
        }
    }
}
#endif

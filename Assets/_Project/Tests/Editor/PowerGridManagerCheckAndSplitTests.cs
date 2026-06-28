using System.Reflection;
using Hecton8.Core.Memory;
using Hecton8.Power;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Power
{
    [TestFixture]
    public class PowerGridManagerCheckAndSplitTests
    {
        private GameObject _go1;
        private GameObject _go2;
        private PowerNode _node1;
        private PowerNode _node2;
        private PowerGrid _grid;

        [SetUp]
        public void SetUp()
        {
            _go1 = new GameObject("Node1");
            _go2 = new GameObject("Node2");
            _node1 = _go1.AddComponent<PowerNode>();
            _node2 = _go2.AddComponent<PowerNode>();

            _grid = new PowerGrid(16, null); // Provide explicit null IDataVault since we don't have GlobalRegistry
        }

        [TearDown]
        public void TearDown()
        {
            _grid?.Dispose();
            Object.DestroyImmediate(_go1);
            Object.DestroyImmediate(_go2);
        }

        [Test]
        public void CheckAndSplitGrid_WithNullGrid_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => PowerGridManager.CheckAndSplitGrid(null));
        }

        [Test]
        public void CheckAndSplitGrid_WithZeroNodes_DoesNotRequestSplitCheck()
        {
            PowerGridManager.CheckAndSplitGrid(_grid);

            var pendingSplitCheckField = typeof(PowerGrid).GetField("_splitCheckPending", BindingFlags.NonPublic | BindingFlags.Instance);
            bool isPending = (bool)pendingSplitCheckField.GetValue(_grid);

            Assert.IsFalse(isPending);
        }

        [Test]
        public void CheckAndSplitGrid_WithOneNode_DoesNotRequestSplitCheck()
        {
            _grid.AddNode(_node1);
            PowerGridManager.CheckAndSplitGrid(_grid);

            var pendingSplitCheckField = typeof(PowerGrid).GetField("_splitCheckPending", BindingFlags.NonPublic | BindingFlags.Instance);
            bool isPending = (bool)pendingSplitCheckField.GetValue(_grid);

            Assert.IsFalse(isPending);
        }

        [Test]
        public void CheckAndSplitGrid_WithTwoNodes_RequestsSplitCheck()
        {
            _grid.AddNode(_node1);
            _grid.AddNode(_node2);
            PowerGridManager.CheckAndSplitGrid(_grid);

            var pendingSplitCheckField = typeof(PowerGrid).GetField("_splitCheckPending", BindingFlags.NonPublic | BindingFlags.Instance);
            bool isPending = (bool)pendingSplitCheckField.GetValue(_grid);

            Assert.IsTrue(isPending);
        }
    }
}

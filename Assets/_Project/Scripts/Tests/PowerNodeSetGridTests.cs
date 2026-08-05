using NUnit.Framework;
using UnityEngine;
using Hecton8.Power;

namespace Hecton8.Tests
{
    public class PowerNodeSetGridTests
    {
        private GameObject _nodeObj;
        private PowerNode _node;

        [SetUp]
        public void SetUp()
        {
            _nodeObj = new GameObject("TestNode");
            _node = _nodeObj.AddComponent<PowerNode>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_nodeObj != null)
            {
                Object.DestroyImmediate(_nodeObj);
            }
        }

        [Test]
        public void SetGrid_AssignsGridToInternalState()
        {
            var grid = new PowerGrid();

            _node.SetGrid(grid);

            Assert.That(_node.Grid, Is.SameAs(grid), "SetGrid should correctly assign the provided PowerGrid to the node's internal state.");
        }

        [Test]
        public void SetGrid_AssignsNullCorrectly()
        {
            var grid = new PowerGrid();
            _node.SetGrid(grid); // Assign first

            _node.SetGrid(null); // Then null out

            Assert.That(_node.Grid, Is.Null, "SetGrid should handle null assignment properly.");
        }
    }
}

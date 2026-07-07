using NUnit.Framework;
using UnityEngine;
using CandiceAIforGames.AI;
using CandiceAIforGames.AI.Pathfinding;
using System.Reflection;

namespace CandiceAIforGames.AI.Tests
{
    public class CandiceAIControllerPathfindTests
    {
        private GameObject _gameObject;
        private CandiceAIController _controller;
        private GameObject _managerGo;
        private CandiceAIManager _manager;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("Controller");
            _controller = _gameObject.AddComponent<CandiceAIController>();

            _managerGo = new GameObject("Manager");
            _manager = _managerGo.AddComponent<CandiceAIManager>();

            var fieldInfo = typeof(CandiceAIController).GetField("candice", BindingFlags.NonPublic | BindingFlags.Instance);
            fieldInfo.SetValue(_controller, _manager);

            _controller.MovePoint = new Vector3(10, 0, 10);

            var path = new Path(new Vector3[] { new Vector3(5, 0, 5), new Vector3(10, 0, 10) }, _controller.transform.position, 1f);
            var pathFieldInfo = typeof(CandiceAIController).GetField("_path", BindingFlags.NonPublic | BindingFlags.Instance);
            pathFieldInfo.SetValue(_controller, path);
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null) Object.DestroyImmediate(_gameObject);
            if (_managerGo != null) Object.DestroyImmediate(_managerGo);
        }

        [Test]
        public void CandicePathfind_NotCalculatingNotFollowing_CalculatesPath()
        {
            _controller.IsCalculatingPath = false;
            _controller.IsFollowingPath = false;

            bool result = _controller.CandicePathfind();

            Assert.IsFalse(result);
            Assert.IsTrue(_controller.IsCalculatingPath, "IsCalculatingPath should be true after requesting path");
        }

        [Test]
        public void CandicePathfind_IsFollowingPath_WithinThreshold_FollowsPath()
        {
            _controller.IsFollowingPath = true;
            _controller.IsCalculatingPath = false;

            var oldPosFieldInfo = typeof(CandiceAIController).GetField("targetPosOld", BindingFlags.NonPublic | BindingFlags.Instance);
            oldPosFieldInfo.SetValue(_controller, _controller.MovePoint);

            bool result = _controller.CandicePathfind();

            Assert.IsTrue(result);
            Assert.IsFalse(_controller.IsCalculatingPath, "Should not calculate new path if within threshold");
        }

        [Test]
        public void CandicePathfind_IsFollowingPath_OutsideThreshold_CalculatesNewPath()
        {
            _controller.IsFollowingPath = true;
            _controller.IsCalculatingPath = false;

            var oldPosFieldInfo = typeof(CandiceAIController).GetField("targetPosOld", BindingFlags.NonPublic | BindingFlags.Instance);
            oldPosFieldInfo.SetValue(_controller, new Vector3(100, 100, 100));

            bool result = _controller.CandicePathfind();

            Assert.IsTrue(result);
            Assert.IsTrue(_controller.IsCalculatingPath, "Should calculate new path if outside threshold");
        }

        [Test]
        public void CandicePathfind_IsCalculatingPathButNotFollowing_ReturnsFalseAndDoesNothing()
        {
            _controller.IsCalculatingPath = true;
            _controller.IsFollowingPath = false;

            bool result = _controller.CandicePathfind();

            Assert.IsFalse(result);
            Assert.IsTrue(_controller.IsCalculatingPath);
        }
    }
}

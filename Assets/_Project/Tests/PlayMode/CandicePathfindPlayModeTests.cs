using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using System.Reflection;
using CandiceAIforGames.AI;
using CandiceAIforGames.AI.Pathfinding;

namespace Tests
{
    public class CandicePathfindPlayModeTests
    {
        private GameObject _aiGo;
        private CandiceAIController _controller;
        private GameObject _managerGo;
        private CandiceAIManager _manager;

        [SetUp]
        public void Setup()
        {
            _managerGo = new GameObject("AIManager");
            _manager = _managerGo.AddComponent<CandiceAIManager>();
            _manager.grid = _managerGo.AddComponent<CandiceGrid>();

            _aiGo = new GameObject("AI");
            _controller = _aiGo.AddComponent<CandiceAIController>();

            FieldInfo candiceField = typeof(CandiceAIController).GetField("candice", BindingFlags.NonPublic | BindingFlags.Instance);
            if (candiceField != null)
            {
                candiceField.SetValue(_controller, _manager);
            }
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            if (_aiGo != null)
            {
                Object.Destroy(_aiGo);
            }
            if (_managerGo != null)
            {
                Object.Destroy(_managerGo);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator CandicePathfind_NotCalculatingNotFollowing_CallsCalculateAStarPath()
        {
            _controller.IsCalculatingPath = false;
            _controller.IsFollowingPath = false;
            _controller.MovePoint = new Vector3(10, 0, 10);

            bool result = _controller.CandicePathfind();

            Assert.That(_controller.IsCalculatingPath, Is.True);
            Assert.That(result, Is.False);

            yield return null;
        }

        [UnityTest]
        public IEnumerator CandicePathfind_IsFollowingPathAndMovedPastThreshold_CallsCalculateAStarPath()
        {
            _controller.IsFollowingPath = true;
            _controller.IsCalculatingPath = false;

            FieldInfo targetPosOldField = typeof(CandiceAIController).GetField("targetPosOld", BindingFlags.NonPublic | BindingFlags.Instance);
            if (targetPosOldField != null)
            {
                targetPosOldField.SetValue(_controller, Vector3.zero);
            }

            _controller._pathUpdateMoveThreshold = 1f;
            _controller.MovePoint = new Vector3(5, 0, 5);

            bool result = _controller.CandicePathfind();

            Assert.That(_controller.IsCalculatingPath, Is.True);
            Assert.That(result, Is.True);

            yield return null;
        }

        [UnityTest]
        public IEnumerator CandicePathfind_IsFollowingPathAndNotMovedPastThreshold_CallsFollowAStarPath()
        {
            _controller.IsFollowingPath = true;
            _controller.IsCalculatingPath = false;

            FieldInfo targetPosOldField = typeof(CandiceAIController).GetField("targetPosOld", BindingFlags.NonPublic | BindingFlags.Instance);
            if (targetPosOldField != null)
            {
                targetPosOldField.SetValue(_controller, Vector3.zero);
            }

            _controller._pathUpdateMoveThreshold = 10f;
            _controller.MovePoint = new Vector3(5, 0, 5);

            bool result = _controller.CandicePathfind();

            Assert.That(_controller.IsCalculatingPath, Is.False);
            Assert.That(_controller.IsFollowingPath, Is.False);
            Assert.That(result, Is.False);

            yield return null;
        }

        [UnityTest]
        public IEnumerator CandicePathfind_NoManager_SetsVariablesToFalse()
        {
            FieldInfo candiceField = typeof(CandiceAIController).GetField("candice", BindingFlags.NonPublic | BindingFlags.Instance);
            if (candiceField != null)
            {
                candiceField.SetValue(_controller, null);
            }

            _controller.IsCalculatingPath = false;
            _controller.IsFollowingPath = false;

            bool result = _controller.CandicePathfind();

            Assert.That(_controller.IsCalculatingPath, Is.False);
            Assert.That(_controller.IsFollowingPath, Is.False);
            Assert.That(result, Is.False);

            yield return null;
        }
    }
}

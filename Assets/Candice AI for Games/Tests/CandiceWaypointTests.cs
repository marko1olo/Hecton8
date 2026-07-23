using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CandiceAIforGames.AI;

namespace CandiceAIforGames.Tests
{
    [TestFixture]
    public class CandiceWaypointTests
    {
        private GameObject _waypointGo;
        private CandiceWaypoint _waypoint;

        [SetUp]
        public void SetUp()
        {
            _waypointGo = new GameObject("TestWaypoint");
            _waypoint = _waypointGo.AddComponent<CandiceWaypoint>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_waypointGo != null)
            {
                Object.DestroyImmediate(_waypointGo);
            }
        }

        [Test]
        public void GetPosition_ReturnsPositionWithinBounds()
        {
            _waypoint.transform.position = new Vector3(10f, 0f, 10f);
            _waypoint.transform.rotation = Quaternion.Euler(0f, 45f, 0f); // Rotate to test local right vector
            _waypoint.width = 5f;

            Vector3 expectedMinBound = _waypoint.transform.position + _waypoint.transform.right * _waypoint.width / 2f;
            Vector3 expectedMaxBound = _waypoint.transform.position - _waypoint.transform.right * _waypoint.width / 2f;
            Vector3 diff = expectedMaxBound - expectedMinBound;
            float maxDistSqr = diff.sqrMagnitude;

            for (int i = 0; i < 100; i++)
            {
                Vector3 position = _waypoint.GetPosition();

                // The point must lie on the line segment between minBound and maxBound

                // 1. Check distance to bounds is not greater than the total distance between bounds
                float distToMinSqr = (position - expectedMinBound).sqrMagnitude;
                float distToMaxSqr = (position - expectedMaxBound).sqrMagnitude;

                Assert.That(distToMinSqr, Is.LessThanOrEqualTo(maxDistSqr).Within(0.001f));
                Assert.That(distToMaxSqr, Is.LessThanOrEqualTo(maxDistSqr).Within(0.001f));

                // 2. Check collinearity using cross product
                Vector3 v1 = position - expectedMinBound;
                Vector3 v2 = expectedMaxBound - expectedMinBound;
                Vector3 cross = Vector3.Cross(v1, v2);
                Assert.That(cross.sqrMagnitude, Is.EqualTo(0f).Within(0.001f));
            }
        }

        [Test]
        public void GetPosition_WithZeroWidth_ReturnsTransformPosition()
        {
            _waypoint.transform.position = new Vector3(5f, 5f, 5f);
            _waypoint.width = 0f;

            for (int i = 0; i < 10; i++)
            {
                Vector3 position = _waypoint.GetPosition();

                Assert.That(position.x, Is.EqualTo(5f).Within(0.001f));
                Assert.That(position.y, Is.EqualTo(5f).Within(0.001f));
                Assert.That(position.z, Is.EqualTo(5f).Within(0.001f));
            }
        }
    }
}

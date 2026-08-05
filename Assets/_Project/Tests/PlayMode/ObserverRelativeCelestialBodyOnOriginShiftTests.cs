using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using Hecton8.Celestial;
using Hecton8.Core;

namespace Hecton8.Tests.PlayMode
{
    [TestFixture]
    public class ObserverRelativeCelestialBodyOnOriginShiftTests
    {
        private GameObject _go;
        private ObserverRelativeCelestialBody _celestialBody;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestCelestialBody");
            _celestialBody = _go.AddComponent<ObserverRelativeCelestialBody>();
            _celestialBody.SetFixedDirection(Vector3.forward);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.Destroy(_go);
            }
        }

        [UnityTest]
        public IEnumerator OnOriginShift_ValidShift_QueuesVisualSyncAndUpdatesTransform()
        {
            yield return null;

            _celestialBody.transform.position = new Vector3(1234f, 5678f, 91011f);

            _celestialBody.LateFrameTick();
            Assert.AreEqual(new Vector3(1234f, 5678f, 91011f), _celestialBody.transform.position);

            var shiftData = new OriginShiftEventData(
                new Vector3(100f, 0f, 0f),
                Vector3.zero,
                new Vector3(100f, 0f, 0f),
                1u,
                Time.frameCount,
                0f,
                false
            );

            _celestialBody.OnOriginShift(in shiftData);
            _celestialBody.LateFrameTick();

            Assert.AreNotEqual(new Vector3(1234f, 5678f, 91011f), _celestialBody.transform.position);
        }

        [UnityTest]
        public IEnumerator OnOriginShift_InvalidShiftMagnitude_DoesNotQueueVisualSync()
        {
            yield return null;

            _celestialBody.transform.position = new Vector3(1234f, 5678f, 91011f);

            var shiftData = new OriginShiftEventData(
                new Vector3(0.0000001f, 0f, 0f),
                Vector3.zero,
                new Vector3(0.0000001f, 0f, 0f),
                1u,
                Time.frameCount,
                0f,
                false
            );

            _celestialBody.OnOriginShift(in shiftData);
            _celestialBody.LateFrameTick();

            Assert.AreEqual(new Vector3(1234f, 5678f, 91011f), _celestialBody.transform.position);
        }

        [UnityTest]
        public IEnumerator OnOriginShift_InfiniteShift_DoesNotQueueVisualSync()
        {
            yield return null;

            _celestialBody.transform.position = new Vector3(1234f, 5678f, 91011f);

            var shiftData = new OriginShiftEventData(
                new Vector3(float.PositiveInfinity, 0f, 0f),
                Vector3.zero,
                new Vector3(float.PositiveInfinity, 0f, 0f),
                1u,
                Time.frameCount,
                0f,
                false
            );

            _celestialBody.OnOriginShift(in shiftData);
            _celestialBody.LateFrameTick();

            Assert.AreEqual(new Vector3(1234f, 5678f, 91011f), _celestialBody.transform.position);
        }
    }
}

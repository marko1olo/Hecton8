#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using Hecton8.Celestial;

namespace Hecton8.Tests.Editor
{
    public class ObserverRelativeCelestialBodyTickTests
    {
        [Test]
        public void Tick_ExecutesWithoutException()
        {
            GameObject go = new GameObject("CelestialBody");
            var celestialBody = go.AddComponent<ObserverRelativeCelestialBody>();

            Assert.DoesNotThrow(() => celestialBody.Tick(0.1f));

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void LateFrameTick_ExecutesWithoutException()
        {
            GameObject go = new GameObject("CelestialBody");
            var celestialBody = go.AddComponent<ObserverRelativeCelestialBody>();

            celestialBody.Tick(0.1f);
            Assert.DoesNotThrow(() => celestialBody.LateFrameTick());

            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
#endif
using System.Collections;
using Hecton8.Celestial;
using Hecton8.Core;
using Hecton8.Environment;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Hecton8.Tests.PlayMode
{
    [TestFixture]
    public sealed class HectonCelestialEnginePlayModeTests
    {
        private GameObject _atmosphereObject;
        private HectonAtmosphereManager _atmosphereManager;
        private GameObject _engineObject;
        private HectonCelestialEngine _engine;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _atmosphereObject = new GameObject(nameof(HectonAtmosphereManager));
            _atmosphereManager = _atmosphereObject.AddComponent<HectonAtmosphereManager>();

            _engineObject = new GameObject(nameof(HectonCelestialEngine));
            _engine = _engineObject.AddComponent<HectonCelestialEngine>();

            Assert.That(GlobalRegistry.Atmosphere, Is.SameAs(_atmosphereManager));
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_engineObject != null)
                Object.Destroy(_engineObject);

            if (_atmosphereObject != null)
                Object.Destroy(_atmosphereObject);

            yield return null;
        }

        [UnityTest]
        public IEnumerator TryApplyRuntimeTimeOfDay01_ValidTime_UpdatesTheRuntimeAtmosphere()
        {
            const float requestedTimeOfDay = 0.73f;

            Assert.That(_engine.TryApplyRuntimeTimeOfDay01(requestedTimeOfDay), Is.True);
            Assert.That(_atmosphereManager.TimeOfDay, Is.EqualTo(requestedTimeOfDay).Within(0.0001f));

            yield return null;
        }

        [UnityTest]
        public IEnumerator TryApplyRuntimeTimeOfDay01_NonFiniteTime_RejectsInputWithoutMutatingAtmosphere()
        {
            const float knownTimeOfDay = 0.27f;
            Assert.That(_atmosphereManager.TrySetTimeOfDay(knownTimeOfDay), Is.True);
            float timeBeforeInvalidRequest = _atmosphereManager.TimeOfDay;

            Assert.That(_engine.TryApplyRuntimeTimeOfDay01(float.NaN), Is.False);
            Assert.That(_atmosphereManager.TimeOfDay, Is.EqualTo(timeBeforeInvalidRequest).Within(0.0001f));

            yield return null;
        }

        [UnityTest]
        public IEnumerator TryApplyRuntimeTimeOfDay01_OutOfRangeTime_ClampsBeforeUpdatingAtmosphere()
        {
            Assert.That(_engine.TryApplyRuntimeTimeOfDay01(-0.25f), Is.True);
            Assert.That(_atmosphereManager.TimeOfDay, Is.EqualTo(0f).Within(0.0001f));

            Assert.That(_engine.TryApplyRuntimeTimeOfDay01(1.25f), Is.True);
            Assert.That(_atmosphereManager.TimeOfDay, Is.EqualTo(1f).Within(0.0001f));

            yield return null;
        }

        [UnityTest]
        public IEnumerator TryApplyRuntimeTimeOfDay01_MissingAtmosphere_RejectsRequest()
        {
            GlobalRegistry.UnregisterAtmosphereRuntime(_atmosphereManager);
            Object.Destroy(_atmosphereObject);
            yield return null;

            Assert.That(_engine.TryApplyRuntimeTimeOfDay01(0.5f), Is.False);
        }
    }
}

using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using System.Collections;
using UnityEngine.TestTools;
using Hecton8.Physics;
using Hecton8.Gameplay;

namespace Hecton8.Physics.Tests
{
    public class ThermalUpdraftVolumeTests
    {
        private ThermalUpdraftVolume _volume;
        private GameObject _gameObject;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("TestVolume");
            _gameObject.AddComponent<CurrentVolume>();
            _volume = _gameObject.AddComponent<ThermalUpdraftVolume>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
                Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void SlowTick_WhenActiveAndValid_DoesNotThrow()
        {
            // Set up valid values for UpdateHazardRegistration
            var fieldHeatIntensity = typeof(ThermalUpdraftVolume).GetField("heatIntensity", BindingFlags.NonPublic | BindingFlags.Instance);
            if(fieldHeatIntensity != null)
                fieldHeatIntensity.SetValue(_volume, 10f); // valid > 0

            _gameObject.transform.position = Vector3.one; // valid position

            _volume.gameObject.SetActive(true); // make sure it's active and enabled

            Assert.DoesNotThrow(() => {
                _volume.SlowTick();
            });
        }

        [Test]
        public void SlowTick_WhenNotActive_DoesNotThrow()
        {
            _gameObject.SetActive(false);

            Assert.DoesNotThrow(() => {
                _volume.SlowTick();
            });
        }

        [Test]
        public void SlowTick_WhenHeatIsZero_DoesNotThrow()
        {
            var fieldHeatIntensity = typeof(ThermalUpdraftVolume).GetField("heatIntensity", BindingFlags.NonPublic | BindingFlags.Instance);
            if(fieldHeatIntensity != null)
                fieldHeatIntensity.SetValue(_volume, 0f);

            _gameObject.transform.position = Vector3.one;
            _volume.gameObject.SetActive(true);

            Assert.DoesNotThrow(() => {
                _volume.SlowTick();
            });
        }

        [Test]
        public void SlowTick_WhenPositionIsInfinite_DoesNotThrow()
        {
            var fieldHeatIntensity = typeof(ThermalUpdraftVolume).GetField("heatIntensity", BindingFlags.NonPublic | BindingFlags.Instance);
            if(fieldHeatIntensity != null)
                fieldHeatIntensity.SetValue(_volume, 10f);

            _gameObject.transform.position = new Vector3(float.PositiveInfinity, 0f, 0f);
            _volume.gameObject.SetActive(true);

            Assert.DoesNotThrow(() => {
                _volume.SlowTick();
            });
        }
    }
}

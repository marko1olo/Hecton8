using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hecton8.Physics;

namespace Hecton8.Tests.PlayMode.Physics
{
    public class ThermalUpdraftVolumeTests
    {
        private GameObject _go;
        private ThermalUpdraftVolume _volume;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("ThermalUpdraftTest");
            // Need a CurrentVolume first to bypass RequireComponent
            _go.AddComponent<CurrentVolume>();
            _volume = _go.AddComponent<ThermalUpdraftVolume>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }
        }

        [UnityTest]
        public IEnumerator ApplyPreset_NullCurrentVolume_DoesNotThrow()
        {
            // By design, ThermalUpdraftVolume gets a reference to CurrentVolume via TryGetComponent in Awake().
            // And then it uses that in ApplyPreset.
            // We can null out the private field via reflection to test the edge case without destroying the component.

            var field = typeof(ThermalUpdraftVolume).GetField("_currentVolume", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "Could not find private field _currentVolume on ThermalUpdraftVolume");
            field.SetValue(_volume, null);

            var method = typeof(ThermalUpdraftVolume).GetMethod("ApplyPreset", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "Could not find private method ApplyPreset on ThermalUpdraftVolume");

            // Execute method and verify it doesn't throw NullReferenceException
            Assert.DoesNotThrow(() =>
            {
                method.Invoke(_volume, null);
            });

            yield return null;
        }
    }
}

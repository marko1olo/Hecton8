using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using Hecton8.World;
using Hecton8.Core;

namespace Hecton8.World.Tests
{
    public class MockPlayerRuntimeContext : IPlayerRuntimeContext
    {
        public bool IsInitialized { get; set; }
        public GameObject PlayerObject { get; set; }
        public Transform PlayerTransform { get; set; }
        public HectonPlayerMovement PlayerMovement { get; set; }
    }

    [TestFixture]
    public class BiomeSamplerCacheTests
    {
        private GameObject _gameObject;
        private BiomeSamplerCache _biomeSamplerCache;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("BiomeSamplerCache");
            _biomeSamplerCache = _gameObject.AddComponent<BiomeSamplerCache>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
            {
                Object.DestroyImmediate(_gameObject);
            }
        }

        [Test]
        public void OnGlobalRegistryServiceReplaced_Dispatcher_Inactive_UnregistersOnly()
        {
            _gameObject.SetActive(false);

            FieldInfo registeredField = typeof(BiomeSamplerCache).GetField("_registeredToTickManager", BindingFlags.NonPublic | BindingFlags.Instance);
            registeredField.SetValue(_biomeSamplerCache, true);

            _biomeSamplerCache.OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot.Dispatcher, null, new object());

            Assert.IsFalse((bool)registeredField.GetValue(_biomeSamplerCache));
        }

        [Test]
        public void OnGlobalRegistryServiceReplaced_Dispatcher_ActiveNullService_UnregistersOnly()
        {
            _gameObject.SetActive(true);

            FieldInfo registeredField = typeof(BiomeSamplerCache).GetField("_registeredToTickManager", BindingFlags.NonPublic | BindingFlags.Instance);
            registeredField.SetValue(_biomeSamplerCache, true);

            _biomeSamplerCache.OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot.Dispatcher, null, null);

            Assert.IsFalse((bool)registeredField.GetValue(_biomeSamplerCache));
        }

        [Test]
        public void OnGlobalRegistryServiceReplaced_MapMagicRuntime_NullsPreviousAndSetsNew()
        {
            GameObject currentObj = new GameObject("CurrentMapMagic");
            GameObject previousObj = new GameObject("PreviousMapMagic");
            try
            {
                var currentBridge = currentObj.AddComponent<Hecton8.Core.MapMagicRuntimeBridge>();
                var previousBridge = previousObj.AddComponent<Hecton8.Core.MapMagicRuntimeBridge>();

                FieldInfo mapMagicField = typeof(BiomeSamplerCache).GetField("mapMagicBridge", BindingFlags.NonPublic | BindingFlags.Instance);
                mapMagicField.SetValue(_biomeSamplerCache, previousBridge);

                _biomeSamplerCache.OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot.MapMagicRuntime, previousBridge, currentBridge);

                MapMagicBridge result = (MapMagicBridge)mapMagicField.GetValue(_biomeSamplerCache);
                Assert.AreEqual(currentBridge, result);
                Assert.AreNotEqual(previousBridge, result);
            }
            finally
            {
                if (previousObj != null) Object.DestroyImmediate(previousObj);
                if (currentObj != null) Object.DestroyImmediate(currentObj);
            }
        }

        [Test]
        public void OnGlobalRegistryServiceReplaced_MapMagicRuntime_PreviousMatches_CurrentNull()
        {
            GameObject previousObj = new GameObject("PreviousMapMagic");
            try
            {
                var previousBridge = previousObj.AddComponent<Hecton8.Core.MapMagicRuntimeBridge>();

                FieldInfo mapMagicField = typeof(BiomeSamplerCache).GetField("mapMagicBridge", BindingFlags.NonPublic | BindingFlags.Instance);
                mapMagicField.SetValue(_biomeSamplerCache, previousBridge);

                _biomeSamplerCache.OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot.MapMagicRuntime, previousBridge, null);

                MapMagicBridge result = (MapMagicBridge)mapMagicField.GetValue(_biomeSamplerCache);
                Assert.IsNull(result);
            }
            finally
            {
                if (previousObj != null) Object.DestroyImmediate(previousObj);
            }
        }

        [Test]
        public void OnGlobalRegistryServiceReplaced_Player_NullsPreviousAndSetsNew()
        {
            GameObject previousPlayerObj = new GameObject("PreviousPlayer");
            GameObject currentPlayerObj = new GameObject("CurrentPlayer");
            try
            {
                Transform previousTransform = previousPlayerObj.transform;
                Transform currentTransform = currentPlayerObj.transform;

                MockPlayerRuntimeContext previousContext = new MockPlayerRuntimeContext { PlayerTransform = previousTransform };
                MockPlayerRuntimeContext currentContext = new MockPlayerRuntimeContext { PlayerTransform = currentTransform };

                FieldInfo playerTransformField = typeof(BiomeSamplerCache).GetField("playerTransform", BindingFlags.NonPublic | BindingFlags.Instance);
                playerTransformField.SetValue(_biomeSamplerCache, previousTransform);

                FieldInfo cachedPlayerContextField = typeof(BiomeSamplerCache).GetField("_cachedPlayerContext", BindingFlags.NonPublic | BindingFlags.Instance);

                _biomeSamplerCache.OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot.Player, previousContext, currentContext);

                Transform resultTransform = (Transform)playerTransformField.GetValue(_biomeSamplerCache);
                Assert.AreEqual(currentTransform, resultTransform);
                Assert.AreNotEqual(previousTransform, resultTransform);

                IPlayerRuntimeContext resultContext = (IPlayerRuntimeContext)cachedPlayerContextField.GetValue(_biomeSamplerCache);
                Assert.AreEqual(currentContext, resultContext);
            }
            finally
            {
                if (previousPlayerObj != null) Object.DestroyImmediate(previousPlayerObj);
                if (currentPlayerObj != null) Object.DestroyImmediate(currentPlayerObj);
            }
        }

        [Test]
        public void OnGlobalRegistryServiceReplaced_Player_PreviousMatches_CurrentNull()
        {
            GameObject previousPlayerObj = new GameObject("PreviousPlayer");
            try
            {
                Transform previousTransform = previousPlayerObj.transform;

                MockPlayerRuntimeContext previousContext = new MockPlayerRuntimeContext { PlayerTransform = previousTransform };

                FieldInfo playerTransformField = typeof(BiomeSamplerCache).GetField("playerTransform", BindingFlags.NonPublic | BindingFlags.Instance);
                playerTransformField.SetValue(_biomeSamplerCache, previousTransform);

                _biomeSamplerCache.OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot.Player, previousContext, null);

                Transform resultTransform = (Transform)playerTransformField.GetValue(_biomeSamplerCache);
                Assert.IsNull(resultTransform);
            }
            finally
            {
                if (previousPlayerObj != null) Object.DestroyImmediate(previousPlayerObj);
            }
        }

        [Test]
        public void TryGetCachedSample_SamplesNull_ReturnsFalse()
        {
            FieldInfo samplesField = typeof(BiomeSamplerCache).GetField("_samples", BindingFlags.NonPublic | BindingFlags.Instance);
            samplesField.SetValue(_biomeSamplerCache, null);

            bool result = _biomeSamplerCache.TryGetCachedSample(Vector3.zero, out BiomeSamplerCache.CachedSample sample);

            Assert.That(result, Is.False);
            Assert.That(sample, Is.EqualTo(default(BiomeSamplerCache.CachedSample)));
        }

        [Test]
        public void TryGetCachedSample_SampleCountZero_ReturnsFalse()
        {
            FieldInfo samplesField = typeof(BiomeSamplerCache).GetField("_samples", BindingFlags.NonPublic | BindingFlags.Instance);
            samplesField.SetValue(_biomeSamplerCache, new BiomeSamplerCache.CachedSample[1]);

            FieldInfo sampleCountField = typeof(BiomeSamplerCache).GetField("_sampleCount", BindingFlags.NonPublic | BindingFlags.Instance);
            sampleCountField.SetValue(_biomeSamplerCache, 0);

            bool result = _biomeSamplerCache.TryGetCachedSample(Vector3.zero, out BiomeSamplerCache.CachedSample sample);

            Assert.That(result, Is.False);
        }

        [Test]
        public void TryGetCachedSample_CacheNotReady_ReturnsFalse()
        {
            FieldInfo samplesField = typeof(BiomeSamplerCache).GetField("_samples", BindingFlags.NonPublic | BindingFlags.Instance);
            samplesField.SetValue(_biomeSamplerCache, new BiomeSamplerCache.CachedSample[1]);

            FieldInfo sampleCountField = typeof(BiomeSamplerCache).GetField("_sampleCount", BindingFlags.NonPublic | BindingFlags.Instance);
            sampleCountField.SetValue(_biomeSamplerCache, 1);

            FieldInfo debugCacheReadyField = typeof(BiomeSamplerCache).GetField("_debugCacheReady", BindingFlags.NonPublic | BindingFlags.Instance);
            debugCacheReadyField.SetValue(_biomeSamplerCache, false);

            bool result = _biomeSamplerCache.TryGetCachedSample(Vector3.zero, out BiomeSamplerCache.CachedSample sample);

            Assert.That(result, Is.False);
        }

        [Test]
        public void TryGetCachedSample_NoNearestSampleFound_ReturnsFalse()
        {
            var samples = new BiomeSamplerCache.CachedSample[1];
            samples[0] = new BiomeSamplerCache.CachedSample
            {
                position = new Vector3(10f, 0f, 10f),
                isValid = 1
            };

            FieldInfo samplesField = typeof(BiomeSamplerCache).GetField("_samples", BindingFlags.NonPublic | BindingFlags.Instance);
            samplesField.SetValue(_biomeSamplerCache, samples);

            FieldInfo sampleCountField = typeof(BiomeSamplerCache).GetField("_sampleCount", BindingFlags.NonPublic | BindingFlags.Instance);
            sampleCountField.SetValue(_biomeSamplerCache, 1);

            FieldInfo debugCacheReadyField = typeof(BiomeSamplerCache).GetField("_debugCacheReady", BindingFlags.NonPublic | BindingFlags.Instance);
            debugCacheReadyField.SetValue(_biomeSamplerCache, true);

            FieldInfo cellSizeField = typeof(BiomeSamplerCache).GetField("cellSize", BindingFlags.NonPublic | BindingFlags.Instance);
            cellSizeField.SetValue(_biomeSamplerCache, 10f); // maxDistance = 7.5f

            bool result = _biomeSamplerCache.TryGetCachedSample(Vector3.zero, out BiomeSamplerCache.CachedSample sample);

            Assert.That(result, Is.False);
        }

        [Test]
        public void TryGetCachedSample_NearestSampleFoundButInvalid_ReturnsFalse()
        {
            var samples = new BiomeSamplerCache.CachedSample[1];
            samples[0] = new BiomeSamplerCache.CachedSample
            {
                position = Vector3.zero,
                isValid = 0
            };

            FieldInfo samplesField = typeof(BiomeSamplerCache).GetField("_samples", BindingFlags.NonPublic | BindingFlags.Instance);
            samplesField.SetValue(_biomeSamplerCache, samples);

            FieldInfo sampleCountField = typeof(BiomeSamplerCache).GetField("_sampleCount", BindingFlags.NonPublic | BindingFlags.Instance);
            sampleCountField.SetValue(_biomeSamplerCache, 1);

            FieldInfo debugCacheReadyField = typeof(BiomeSamplerCache).GetField("_debugCacheReady", BindingFlags.NonPublic | BindingFlags.Instance);
            debugCacheReadyField.SetValue(_biomeSamplerCache, true);

            FieldInfo cellSizeField = typeof(BiomeSamplerCache).GetField("cellSize", BindingFlags.NonPublic | BindingFlags.Instance);
            cellSizeField.SetValue(_biomeSamplerCache, 10f);

            bool result = _biomeSamplerCache.TryGetCachedSample(Vector3.zero, out BiomeSamplerCache.CachedSample sample);

            Assert.That(result, Is.False);
        }

        [Test]
        public void TryGetCachedSample_NearestSampleFoundAndValid_ReturnsTrue()
        {
            var samples = new BiomeSamplerCache.CachedSample[1];
            samples[0] = new BiomeSamplerCache.CachedSample
            {
                position = Vector3.zero,
                isValid = 1,
                biomeIndex = 42
            };

            FieldInfo samplesField = typeof(BiomeSamplerCache).GetField("_samples", BindingFlags.NonPublic | BindingFlags.Instance);
            samplesField.SetValue(_biomeSamplerCache, samples);

            FieldInfo sampleCountField = typeof(BiomeSamplerCache).GetField("_sampleCount", BindingFlags.NonPublic | BindingFlags.Instance);
            sampleCountField.SetValue(_biomeSamplerCache, 1);

            FieldInfo debugCacheReadyField = typeof(BiomeSamplerCache).GetField("_debugCacheReady", BindingFlags.NonPublic | BindingFlags.Instance);
            debugCacheReadyField.SetValue(_biomeSamplerCache, true);

            FieldInfo cellSizeField = typeof(BiomeSamplerCache).GetField("cellSize", BindingFlags.NonPublic | BindingFlags.Instance);
            cellSizeField.SetValue(_biomeSamplerCache, 10f);

            bool result = _biomeSamplerCache.TryGetCachedSample(Vector3.zero, out BiomeSamplerCache.CachedSample sample);

            Assert.That(result, Is.True);
            Assert.That(sample.isValid, Is.EqualTo(1));
            Assert.That(sample.biomeIndex, Is.EqualTo(42));
        }
    }
}

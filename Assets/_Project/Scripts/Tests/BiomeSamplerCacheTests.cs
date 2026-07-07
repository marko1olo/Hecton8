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
        public void OnOriginShift_InactiveComponent_DoesNotApplyOffset()
        {
            _gameObject.SetActive(false);

            FieldInfo lastCenterPositionField = typeof(BiomeSamplerCache).GetField("_lastCenterPosition", BindingFlags.NonPublic | BindingFlags.Instance);
            Vector3 initialCenter = new Vector3(10f, 0f, 10f);
            lastCenterPositionField.SetValue(_biomeSamplerCache, initialCenter);
            typeof(BiomeSamplerCache).GetField("_hasLastCenterPosition", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_biomeSamplerCache, true);

            var shiftData = new OriginShiftEventData(new Vector3(5f, 0f, 5f), Vector3.zero, Vector3.zero, 0, 0);

            _biomeSamplerCache.OnOriginShift(in shiftData);

            Vector3 updatedCenter = (Vector3)lastCenterPositionField.GetValue(_biomeSamplerCache);
            Assert.That(updatedCenter, Is.EqualTo(initialCenter));
        }

        [Test]
        public void OnOriginShift_InvalidOffset_DoesNotApplyOffset()
        {
            _gameObject.SetActive(true);

            FieldInfo lastCenterPositionField = typeof(BiomeSamplerCache).GetField("_lastCenterPosition", BindingFlags.NonPublic | BindingFlags.Instance);
            Vector3 initialCenter = new Vector3(10f, 0f, 10f);
            lastCenterPositionField.SetValue(_biomeSamplerCache, initialCenter);
            typeof(BiomeSamplerCache).GetField("_hasLastCenterPosition", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_biomeSamplerCache, true);

            var shiftData = new OriginShiftEventData(new Vector3(float.NaN, 0f, 5f), Vector3.zero, Vector3.zero, 0, 0);

            _biomeSamplerCache.OnOriginShift(in shiftData);

            Vector3 updatedCenter = (Vector3)lastCenterPositionField.GetValue(_biomeSamplerCache);
            Assert.That(updatedCenter, Is.EqualTo(initialCenter));
        }

        [Test]
        public void OnOriginShift_NearZeroOffset_DoesNotApplyOffset()
        {
            _gameObject.SetActive(true);

            FieldInfo lastCenterPositionField = typeof(BiomeSamplerCache).GetField("_lastCenterPosition", BindingFlags.NonPublic | BindingFlags.Instance);
            Vector3 initialCenter = new Vector3(10f, 0f, 10f);
            lastCenterPositionField.SetValue(_biomeSamplerCache, initialCenter);
            typeof(BiomeSamplerCache).GetField("_hasLastCenterPosition", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_biomeSamplerCache, true);

            var shiftData = new OriginShiftEventData(new Vector3(0.005f, 0f, 0.005f), Vector3.zero, Vector3.zero, 0, 0); // sqrMagnitude = 0.00005 < 0.0001f

            _biomeSamplerCache.OnOriginShift(in shiftData);

            Vector3 updatedCenter = (Vector3)lastCenterPositionField.GetValue(_biomeSamplerCache);
            Assert.That(updatedCenter, Is.EqualTo(initialCenter));
        }

        [Test]
        public void OnOriginShift_ValidOffset_AppliesOffsetToCachedState()
        {
            _gameObject.SetActive(true);

            FieldInfo lastCenterPositionField = typeof(BiomeSamplerCache).GetField("_lastCenterPosition", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo debugLastCenterPositionField = typeof(BiomeSamplerCache).GetField("_debugLastCenterPosition", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo samplesField = typeof(BiomeSamplerCache).GetField("_samples", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo sampleCountField = typeof(BiomeSamplerCache).GetField("_sampleCount", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo hasLastCenterPositionField = typeof(BiomeSamplerCache).GetField("_hasLastCenterPosition", BindingFlags.NonPublic | BindingFlags.Instance);

            Vector3 initialCenter = new Vector3(10f, 0f, 10f);
            Vector3 debugInitialCenter = new Vector3(20f, 0f, 20f);

            lastCenterPositionField.SetValue(_biomeSamplerCache, initialCenter);
            debugLastCenterPositionField.SetValue(_biomeSamplerCache, debugInitialCenter);
            hasLastCenterPositionField.SetValue(_biomeSamplerCache, true);

            var samplesArray = new BiomeSamplerCache.CachedSample[1];
            samplesArray[0] = new BiomeSamplerCache.CachedSample { position = new Vector3(15f, 0f, 15f) };

            samplesField.SetValue(_biomeSamplerCache, samplesArray);
            sampleCountField.SetValue(_biomeSamplerCache, 1);

            Vector3 shiftOffset = new Vector3(100f, 0f, 100f);
            var shiftData = new OriginShiftEventData(shiftOffset, Vector3.zero, Vector3.zero, 0, 0);

            _biomeSamplerCache.OnOriginShift(in shiftData);

            Vector3 updatedCenter = (Vector3)lastCenterPositionField.GetValue(_biomeSamplerCache);
            Vector3 updatedDebugCenter = (Vector3)debugLastCenterPositionField.GetValue(_biomeSamplerCache);
            var updatedSamplesArray = (BiomeSamplerCache.CachedSample[])samplesField.GetValue(_biomeSamplerCache);
            Vector3 updatedSamplePosition = updatedSamplesArray[0].position;

            Assert.That(updatedCenter, Is.EqualTo(initialCenter - shiftOffset));
            Assert.That(updatedDebugCenter, Is.EqualTo(debugInitialCenter - shiftOffset));
            Assert.That(updatedSamplePosition, Is.EqualTo(new Vector3(15f, 0f, 15f) - shiftOffset));
        public void TryGetCachedSample_SamplesNull_ReturnsFalse()
            samplesField.SetValue(_biomeSamplerCache, null);

            bool result = _biomeSamplerCache.TryGetCachedSample(Vector3.zero, out BiomeSamplerCache.CachedSample sample);

            Assert.That(result, Is.False);
            Assert.That(sample, Is.EqualTo(default(BiomeSamplerCache.CachedSample)));

        public void TryGetCachedSample_SampleCountZero_ReturnsFalse()
            samplesField.SetValue(_biomeSamplerCache, new BiomeSamplerCache.CachedSample[1]);

            sampleCountField.SetValue(_biomeSamplerCache, 0);

            bool result = _biomeSamplerCache.TryGetCachedSample(Vector3.zero, out BiomeSamplerCache.CachedSample sample);

            Assert.That(result, Is.False);

        public void TryGetCachedSample_CacheNotReady_ReturnsFalse()
            samplesField.SetValue(_biomeSamplerCache, new BiomeSamplerCache.CachedSample[1]);


            FieldInfo debugCacheReadyField = typeof(BiomeSamplerCache).GetField("_debugCacheReady", BindingFlags.NonPublic | BindingFlags.Instance);
            debugCacheReadyField.SetValue(_biomeSamplerCache, false);

            bool result = _biomeSamplerCache.TryGetCachedSample(Vector3.zero, out BiomeSamplerCache.CachedSample sample);

            Assert.That(result, Is.False);

        public void TryGetCachedSample_NoNearestSampleFound_ReturnsFalse()
            var samples = new BiomeSamplerCache.CachedSample[1];
            samples[0] = new BiomeSamplerCache.CachedSample
                position = new Vector3(10f, 0f, 10f),
                isValid = 1
            };

            samplesField.SetValue(_biomeSamplerCache, samples);


            FieldInfo debugCacheReadyField = typeof(BiomeSamplerCache).GetField("_debugCacheReady", BindingFlags.NonPublic | BindingFlags.Instance);
            debugCacheReadyField.SetValue(_biomeSamplerCache, true);

            FieldInfo cellSizeField = typeof(BiomeSamplerCache).GetField("cellSize", BindingFlags.NonPublic | BindingFlags.Instance);
            cellSizeField.SetValue(_biomeSamplerCache, 10f); // maxDistance = 7.5f

            bool result = _biomeSamplerCache.TryGetCachedSample(Vector3.zero, out BiomeSamplerCache.CachedSample sample);

            Assert.That(result, Is.False);

        public void TryGetCachedSample_NearestSampleFoundButInvalid_ReturnsFalse()
            var samples = new BiomeSamplerCache.CachedSample[1];
            samples[0] = new BiomeSamplerCache.CachedSample
                position = Vector3.zero,
                isValid = 0
            };

            samplesField.SetValue(_biomeSamplerCache, samples);


            FieldInfo debugCacheReadyField = typeof(BiomeSamplerCache).GetField("_debugCacheReady", BindingFlags.NonPublic | BindingFlags.Instance);
            debugCacheReadyField.SetValue(_biomeSamplerCache, true);

            FieldInfo cellSizeField = typeof(BiomeSamplerCache).GetField("cellSize", BindingFlags.NonPublic | BindingFlags.Instance);
            cellSizeField.SetValue(_biomeSamplerCache, 10f);

            bool result = _biomeSamplerCache.TryGetCachedSample(Vector3.zero, out BiomeSamplerCache.CachedSample sample);

            Assert.That(result, Is.False);

        public void TryGetCachedSample_NearestSampleFoundAndValid_ReturnsTrue()
            var samples = new BiomeSamplerCache.CachedSample[1];
            samples[0] = new BiomeSamplerCache.CachedSample
                position = Vector3.zero,
                isValid = 1,
                biomeIndex = 42
            };

            samplesField.SetValue(_biomeSamplerCache, samples);


            FieldInfo debugCacheReadyField = typeof(BiomeSamplerCache).GetField("_debugCacheReady", BindingFlags.NonPublic | BindingFlags.Instance);
            debugCacheReadyField.SetValue(_biomeSamplerCache, true);

            FieldInfo cellSizeField = typeof(BiomeSamplerCache).GetField("cellSize", BindingFlags.NonPublic | BindingFlags.Instance);
            cellSizeField.SetValue(_biomeSamplerCache, 10f);

            bool result = _biomeSamplerCache.TryGetCachedSample(Vector3.zero, out BiomeSamplerCache.CachedSample sample);

            Assert.That(result, Is.True);
            Assert.That(sample.isValid, Is.EqualTo(1));
            Assert.That(sample.biomeIndex, Is.EqualTo(42));
        public void OnGlobalRegistryServiceReplaced_Dispatcher_ActiveWithService_AttemptsRegister()

            FieldInfo registeredField = typeof(BiomeSamplerCache).GetField("_registeredToTickManager", BindingFlags.NonPublic | BindingFlags.Instance);
            registeredField.SetValue(_biomeSamplerCache, true);

            // Since GlobalRegistry.Dispatcher is null in tests (SystemDispatcher is not initialized),
            // TryRegister will abort early, leaving _registeredToTickManager as false.
            // This verifies the branch is executed.
            _biomeSamplerCache.OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot.Dispatcher, null, new object());

            Assert.That((bool)registeredField.GetValue(_biomeSamplerCache), Is.False);

        public void OnGlobalRegistryServiceReplaced_MapMagicRuntime_PreviousMismatches_KeepsOldBridge()
            GameObject originalObj = new GameObject("OriginalMapMagic");
            GameObject otherObj = new GameObject("OtherMapMagic");
            try
                var originalBridge = originalObj.AddComponent<Hecton8.Core.MapMagicRuntimeBridge>();
                var otherBridge = otherObj.AddComponent<Hecton8.Core.MapMagicRuntimeBridge>();

                FieldInfo mapMagicField = typeof(BiomeSamplerCache).GetField("mapMagicBridge", BindingFlags.NonPublic | BindingFlags.Instance);
                mapMagicField.SetValue(_biomeSamplerCache, originalBridge);

                // Pass otherBridge as previousService. Since it doesn't match originalBridge, mapMagicBridge shouldn't be nulled.
                _biomeSamplerCache.OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot.MapMagicRuntime, otherBridge, null);

                MapMagicBridge result = (MapMagicBridge)mapMagicField.GetValue(_biomeSamplerCache);
                Assert.That(result, Is.EqualTo(originalBridge));
            finally
                if (originalObj != null) Object.DestroyImmediate(originalObj);
                if (otherObj != null) Object.DestroyImmediate(otherObj);

        public void OnGlobalRegistryServiceReplaced_Player_PreviousMismatches_KeepsOldTransform()
            GameObject originalPlayerObj = new GameObject("OriginalPlayer");
            GameObject otherPlayerObj = new GameObject("OtherPlayer");
            try
                Transform originalTransform = originalPlayerObj.transform;
                Transform otherTransform = otherPlayerObj.transform;

                MockPlayerRuntimeContext otherContext = new MockPlayerRuntimeContext { PlayerTransform = otherTransform };

                FieldInfo playerTransformField = typeof(BiomeSamplerCache).GetField("playerTransform", BindingFlags.NonPublic | BindingFlags.Instance);
                playerTransformField.SetValue(_biomeSamplerCache, originalTransform);

                // Pass otherContext as previousService. Since it doesn't match originalTransform, playerTransform shouldn't be nulled.
                _biomeSamplerCache.OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot.Player, otherContext, null);

                Transform resultTransform = (Transform)playerTransformField.GetValue(_biomeSamplerCache);
                Assert.That(resultTransform, Is.EqualTo(originalTransform));
            finally
                if (originalPlayerObj != null) Object.DestroyImmediate(originalPlayerObj);
                if (otherPlayerObj != null) Object.DestroyImmediate(otherPlayerObj);

        public void OnGlobalRegistryServiceReplaced_UnrelatedSlot_DoesNothing()
            FieldInfo registeredField = typeof(BiomeSamplerCache).GetField("_registeredToTickManager", BindingFlags.NonPublic | BindingFlags.Instance);
            registeredField.SetValue(_biomeSamplerCache, true);

            FieldInfo mapMagicField = typeof(BiomeSamplerCache).GetField("mapMagicBridge", BindingFlags.NonPublic | BindingFlags.Instance);
            mapMagicField.SetValue(_biomeSamplerCache, null);

            FieldInfo playerTransformField = typeof(BiomeSamplerCache).GetField("playerTransform", BindingFlags.NonPublic | BindingFlags.Instance);
            playerTransformField.SetValue(_biomeSamplerCache, null);

            // Using an unrelated slot like Input should not affect state
            _biomeSamplerCache.OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot.Input, null, null);

            Assert.That((bool)registeredField.GetValue(_biomeSamplerCache), Is.True);
            Assert.That(mapMagicField.GetValue(_biomeSamplerCache), Is.Null);
            Assert.That(playerTransformField.GetValue(_biomeSamplerCache), Is.Null);
        }
    }
}

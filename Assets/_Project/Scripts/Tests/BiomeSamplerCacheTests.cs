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
        }
    }
}

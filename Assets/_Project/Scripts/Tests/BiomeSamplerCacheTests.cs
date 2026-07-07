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
        public void OnGlobalRegistryServiceReplaced_Dispatcher_ActiveWithService_AttemptsRegister()
        {
            _gameObject.SetActive(true);

            FieldInfo registeredField = typeof(BiomeSamplerCache).GetField("_registeredToTickManager", BindingFlags.NonPublic | BindingFlags.Instance);
            registeredField.SetValue(_biomeSamplerCache, true);

            // Since GlobalRegistry.Dispatcher is null in tests (SystemDispatcher is not initialized),
            // TryRegister will abort early, leaving _registeredToTickManager as false.
            // This verifies the branch is executed.
            _biomeSamplerCache.OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot.Dispatcher, null, new object());

            Assert.That((bool)registeredField.GetValue(_biomeSamplerCache), Is.False);
        }

        [Test]
        public void OnGlobalRegistryServiceReplaced_MapMagicRuntime_PreviousMismatches_KeepsOldBridge()
        {
            GameObject originalObj = new GameObject("OriginalMapMagic");
            GameObject otherObj = new GameObject("OtherMapMagic");
            try
            {
                var originalBridge = originalObj.AddComponent<Hecton8.Core.MapMagicRuntimeBridge>();
                var otherBridge = otherObj.AddComponent<Hecton8.Core.MapMagicRuntimeBridge>();

                FieldInfo mapMagicField = typeof(BiomeSamplerCache).GetField("mapMagicBridge", BindingFlags.NonPublic | BindingFlags.Instance);
                mapMagicField.SetValue(_biomeSamplerCache, originalBridge);

                // Pass otherBridge as previousService. Since it doesn't match originalBridge, mapMagicBridge shouldn't be nulled.
                _biomeSamplerCache.OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot.MapMagicRuntime, otherBridge, null);

                MapMagicBridge result = (MapMagicBridge)mapMagicField.GetValue(_biomeSamplerCache);
                Assert.That(result, Is.EqualTo(originalBridge));
            }
            finally
            {
                if (originalObj != null) Object.DestroyImmediate(originalObj);
                if (otherObj != null) Object.DestroyImmediate(otherObj);
            }
        }

        [Test]
        public void OnGlobalRegistryServiceReplaced_Player_PreviousMismatches_KeepsOldTransform()
        {
            GameObject originalPlayerObj = new GameObject("OriginalPlayer");
            GameObject otherPlayerObj = new GameObject("OtherPlayer");
            try
            {
                Transform originalTransform = originalPlayerObj.transform;
                Transform otherTransform = otherPlayerObj.transform;

                MockPlayerRuntimeContext otherContext = new MockPlayerRuntimeContext { PlayerTransform = otherTransform };

                FieldInfo playerTransformField = typeof(BiomeSamplerCache).GetField("playerTransform", BindingFlags.NonPublic | BindingFlags.Instance);
                playerTransformField.SetValue(_biomeSamplerCache, originalTransform);

                // Pass otherContext as previousService. Since it doesn't match originalTransform, playerTransform shouldn't be nulled.
                _biomeSamplerCache.OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot.Player, otherContext, null);

                Transform resultTransform = (Transform)playerTransformField.GetValue(_biomeSamplerCache);
                Assert.That(resultTransform, Is.EqualTo(originalTransform));
            }
            finally
            {
                if (originalPlayerObj != null) Object.DestroyImmediate(originalPlayerObj);
                if (otherPlayerObj != null) Object.DestroyImmediate(otherPlayerObj);
            }
        }

        [Test]
        public void OnGlobalRegistryServiceReplaced_UnrelatedSlot_DoesNothing()
        {
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

using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hecton8.World;
using Hecton8.Core;

namespace Hecton8.Tests.PlayMode
{
    public class BiomeSamplerCacheTests
    {
        [UnityTest]
        public IEnumerator BiomeSamplerCache_TryGetCachedSample_EmptyCache_ReturnsFalse()
        {
            var go = new GameObject();
            try
            {
                var cache = go.AddComponent<BiomeSamplerCache>();
                yield return null; // Wait one frame for Awake/Start

                bool result = cache.TryGetCachedSample(Vector3.zero, out var sample);
                Assert.IsFalse(result, "TryGetCachedSample should return false when the cache is not ready or empty.");
            }
            finally
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
        }

        [UnityTest]
        public IEnumerator BiomeSamplerCache_TryGetNearestSample_EmptyCache_ReturnsFalse()
        {
            var go = new GameObject();
            try
            {
                var cache = go.AddComponent<BiomeSamplerCache>();
                yield return null; // Wait one frame for Awake/Start

                bool result = cache.TryGetNearestSample(Vector3.zero, 10f, out var sample);
                Assert.IsFalse(result, "TryGetNearestSample should return false when the cache is not ready or empty.");
            }
            finally
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
        }

        [UnityTest]
        public IEnumerator BiomeSamplerCache_RebuildCache_WithNullMapMagicBridge_HandlesGracefully()
        {
            var go = new GameObject();
            try
            {
                var cache = go.AddComponent<BiomeSamplerCache>();
                yield return null; // Wait one frame

                // Without a MapMagicBridge and PlayerTransform, RebuildCache returns early and sets _debugCacheReady = false
                // But we want to call SlowTick which triggers RebuildCache.
                cache.SlowTick();

                bool isReady = cache.IsReady;
                Assert.IsFalse(isReady, "Cache should not be ready if MapMagicBridge or PlayerTransform is null.");
            }
            finally
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
        }
    }
}

using Hecton8.Bootstrap;
using Hecton8.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Shared runtime reference helpers for world directors.
    /// Keeps player resolution aligned with SceneBootstrap and reduces duplicated
    /// scene-wide fallback searches during runtime startup.
    /// </summary>
    internal static class WorldRuntimeReferenceUtility
    {
        private static Transform _CachedPlayerTransform;
        private static MapMagicBridge _CachedMapMagicBridge;
        private static ScavengePopulator _CachedScavengePopulator;
        private static readonly Dictionary<Type, UnityEngine.Object> _SceneObjectCache = new Dictionary<Type, UnityEngine.Object>(32);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _CachedPlayerTransform = null;
            _CachedMapMagicBridge = null;
            _CachedScavengePopulator = null;
            _SceneObjectCache.Clear();
        }

        public static bool TryResolvePlayerTransform(ref Transform target)
        {
            if (target != null)
                return true;

            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform bootstrapPlayer))
            {
                _CachedPlayerTransform = bootstrapPlayer;
                target = bootstrapPlayer;
                return true;
            }

            if (_CachedPlayerTransform != null)
            {
                target = _CachedPlayerTransform;
                return true;
            }

            if (Application.isPlaying && SceneBootstrap.HasActiveInstance && !SceneBootstrap.IsGameReady)
                return false;

            GameObject player = GameObject.FindWithTag("Player");
            if (player == null)
                player = GameObject.Find("Player");

            if (player == null)
                return false;

            _CachedPlayerTransform = player.transform;
            target = _CachedPlayerTransform;
            return true;
        }

        public static bool TryResolveSceneObject<T>(ref T target) where T : UnityEngine.Object
        {
            if (target != null)
                return true;

            Type targetType = typeof(T);
            if (_SceneObjectCache.TryGetValue(targetType, out UnityEngine.Object cachedObject) && cachedObject != null)
            {
                target = cachedObject as T;
                if (target != null)
                    return true;

                _SceneObjectCache.Remove(targetType);
            }

            target = UnityEngine.Object.FindAnyObjectByType<T>();
            if (target != null)
                _SceneObjectCache[targetType] = target;
            return target != null;
        }

        public static bool TryResolveMapMagicBridge(ref MapMagicBridge target)
        {
            if (target != null)
                return true;

            if (_CachedMapMagicBridge != null)
            {
                target = _CachedMapMagicBridge;
                return true;
            }

            target = MapMagicBridge.Instance ?? UnityEngine.Object.FindAnyObjectByType<MapMagicBridge>();
            if (target != null)
                _CachedMapMagicBridge = target;
            return target != null;
        }

        public static bool TryResolveScavengePopulator(ref ScavengePopulator target)
        {
            if (target != null)
                return true;

            if (_CachedScavengePopulator != null)
            {
                target = _CachedScavengePopulator;
                return true;
            }

            target = ScavengePopulator.Instance ?? UnityEngine.Object.FindAnyObjectByType<ScavengePopulator>();
            if (target != null)
                _CachedScavengePopulator = target;
            return target != null;
        }
    }
}

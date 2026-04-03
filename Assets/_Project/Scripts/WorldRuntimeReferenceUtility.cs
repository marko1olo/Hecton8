using Hecton8.Bootstrap;
using Hecton8.Core;
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

        private static class SceneObjectCache<T> where T : Object
        {
            public static T Cached;
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

        public static bool TryResolveSceneObject<T>(ref T target) where T : Object
        {
            if (target != null)
                return true;

            if (SceneObjectCache<T>.Cached != null)
            {
                target = SceneObjectCache<T>.Cached;
                return true;
            }

            target = Object.FindAnyObjectByType<T>();
            if (target != null)
                SceneObjectCache<T>.Cached = target;
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

            target = MapMagicBridge.Instance ?? Object.FindAnyObjectByType<MapMagicBridge>();
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

            target = ScavengePopulator.Instance ?? Object.FindAnyObjectByType<ScavengePopulator>();
            if (target != null)
                _CachedScavengePopulator = target;
            return target != null;
        }
    }
}

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
        public static bool TryResolvePlayerTransform(ref Transform target)
        {
            if (target != null)
                return true;

            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform bootstrapPlayer))
            {
                target = bootstrapPlayer;
                return true;
            }

            if (Application.isPlaying && SceneBootstrap.HasActiveInstance && !SceneBootstrap.IsGameReady)
                return false;

            GameObject player = GameObject.FindWithTag("Player");
            if (player == null)
                player = GameObject.Find("Player");

            if (player == null)
                return false;

            target = player.transform;
            return true;
        }

        public static bool TryResolveSceneObject<T>(ref T target) where T : Object
        {
            if (target != null)
                return true;

            target = Object.FindAnyObjectByType<T>();
            return target != null;
        }

        public static bool TryResolveMapMagicBridge(ref MapMagicBridge target)
        {
            if (target != null)
                return true;

            target = MapMagicBridge.Instance ?? Object.FindAnyObjectByType<MapMagicBridge>();
            return target != null;
        }

        public static bool TryResolveScavengePopulator(ref ScavengePopulator target)
        {
            if (target != null)
                return true;

            target = ScavengePopulator.Instance ?? Object.FindAnyObjectByType<ScavengePopulator>();
            return target != null;
        }
    }
}

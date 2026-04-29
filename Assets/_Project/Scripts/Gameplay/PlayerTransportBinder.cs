using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Resolves ambient transport-platform ownership that lives outside explicit mounted transport lifecycles.
    /// </summary>
    internal static class PlayerTransportBinder
    {
        /// <summary>
        /// Resolves the authoritative ambient submarine platform when the player is walking inside a dry interior.
        /// </summary>
        public static bool TryResolveAmbientSubmarinePlatform(
            bool isInDryInterior,
            out ITransportPlatform platform,
            out MonoBehaviour platformBehaviour)
        {
            platform = null;
            platformBehaviour = null;

            ISubmarineRuntimeContext submarineRuntimeContext = GlobalRegistry.Submarine;
            if (submarineRuntimeContext == null || !submarineRuntimeContext.IsTransportPlatformActive || !isInDryInterior)
                return false;

            platform = submarineRuntimeContext;
            platformBehaviour = submarineRuntimeContext as MonoBehaviour;
            return platformBehaviour != null;
        }
    }
}

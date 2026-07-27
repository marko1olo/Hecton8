// ============================================================================
// HECTON-8 — InteractionProbeLayerMask.cs
// Pure resolver for serialized interaction probe masks.
//
// WHY THIS EXISTS:
//   A serialized UnityEngine.LayerMask field defaults to Nothing (0). Every
//   interaction probe in this project filters candidates with a plain
//   (mask & (1 << layer)) != 0 test, so a mask of 0 rejects every candidate
//   and the probe resolves NO target — silently, forever, with no exception
//   and no player-build log. That is the same dead-query failure the Core
//   helpers already close for terrain and resource-node probes
//   (HectonLayerMasks.ResolveTerrainSdfProbeLayerMask:287 and
//   ResolveResourceNodeHostLayerMask:302 both treat layerMask == 0 as
//   "unconfigured" and substitute the route default).
//
//   Everything (-1) is forbidden project-authored data and was already
//   handled by the owners; Nothing (0), which is the value every
//   never-touched inspector field ships with, was not. This closes that
//   asymmetry for the player interaction route.
//
// SCOPE:
//   Pure C# over Hecton8.Core layer constants. No Unity API, no allocation,
//   no state. Owner-local by design — the caller supplies its own declared
//   route default, so no new global authority surface is introduced.
// ============================================================================

namespace Hecton8.Interaction
{
    using Hecton8.Core;

    /// <summary>
    /// Resolves serialized interaction probe layer masks so an unconfigured or unusable
    /// inspector value falls back to the owner's declared route default instead of
    /// producing a query that can never match a collider.
    /// </summary>
    public static class InteractionProbeLayerMask
    {
        /// <summary>
        /// Fallback used when the caller's own route default is itself unusable. The
        /// Interactable layer is the minimum surface every interaction probe must reach.
        /// </summary>
        public static readonly int MinimumProbeLayerMask = HectonLayerMasks.InteractableLayerMask;

        /// <summary>
        /// True when a mask can actually resolve a collider on this project's layer set.
        /// </summary>
        /// <param name="layerMask">Serialized or computed Unity layer mask value.</param>
        /// <remarks>
        /// Rejected: Nothing (0), the serialized default of an unconfigured field, which
        /// matches no layer; Everything (-1) and any other negative value, which
        /// <see cref="HectonLayerMasks.IsEverythingLayerMask"/> and the authoring rules
        /// treat as forbidden project-authored data; and any positive mask that selects
        /// only layer indices TagManager never assigned, which can never match a collider.
        /// </remarks>
        public static bool IsUsableProbeMask(int layerMask)
        {
            if (layerMask <= 0)
                return false;

            return (layerMask & HectonLayerMasks.AllDefinedProjectLayersMask) != 0;
        }

        /// <summary>
        /// Returns the layer mask an interaction probe should actually query with.
        /// </summary>
        /// <param name="serializedMask">Value read from the owner's inspector field.</param>
        /// <param name="routeDefaultMask">The owner's declared route default mask.</param>
        /// <returns>
        /// <paramref name="serializedMask"/> when it is usable; otherwise
        /// <paramref name="routeDefaultMask"/> when that is usable; otherwise
        /// <see cref="MinimumProbeLayerMask"/>. The result is always a usable mask,
        /// so the probe below this hop can never be handed a filter that matches nothing.
        /// </returns>
        public static int Resolve(int serializedMask, int routeDefaultMask)
        {
            if (IsUsableProbeMask(serializedMask))
                return serializedMask;

            return IsUsableProbeMask(routeDefaultMask) ? routeDefaultMask : MinimumProbeLayerMask;
        }
    }
}

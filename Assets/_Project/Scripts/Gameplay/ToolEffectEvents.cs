// ============================================================================
// HECTON-8 — ToolEffectEvents.cs
// Zero-GC gameplay event bus for tool-driven effect signals.
// ============================================================================

using System;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Typed gameplay tool-effect channels consumed by runtime systems.
    /// </summary>
    public enum EffectType : byte
    {
        None = 0,
        Weld = 1
    }

    /// <summary>
    /// Value-type payload for gameplay tool effects. Struct dispatch avoids heap allocations in held-beam hot paths.
    /// </summary>
    public readonly struct ToolEffectSignal
    {
        /// <summary>
        /// Creates a tool-effect payload for the current gameplay frame.
        /// </summary>
        public ToolEffectSignal(
            EffectType effectType,
            BaseModule module,
            Transform sourceTransform,
            float magnitude,
            Vector3 hitPointWorld)
        {
            EffectType = effectType;
            Module = module;
            SourceTransform = sourceTransform;
            Magnitude = magnitude;
            HitPointWorld = hitPointWorld;
        }

        /// <summary>Resolved gameplay effect type.</summary>
        public EffectType EffectType { get; }

        /// <summary>Target module under the active tool beam.</summary>
        public BaseModule Module { get; }

        /// <summary>Transform that emitted the tool effect, when available.</summary>
        public Transform SourceTransform { get; }

        /// <summary>Effect magnitude in gameplay units for this frame.</summary>
        public float Magnitude { get; }

        /// <summary>World hit point resolved by the active tool query.</summary>
        public Vector3 HitPointWorld { get; }
    }

    /// <summary>
    /// Static gameplay event bus for tool-effect signals.
    /// </summary>
    public static class ToolEffectEvents
    {
        /// <summary>
        /// Raised when a gameplay tool applies a typed effect to a base module.
        /// </summary>
        public static event Action<ToolEffectSignal> OnEffectApplied;

        /// <summary>
        /// Dispatches a gameplay tool-effect signal without heap allocations.
        /// </summary>
        public static void RaiseEffectApplied(
            EffectType effectType,
            BaseModule module,
            Transform sourceTransform,
            float magnitude,
            Vector3 hitPointWorld)
        {
            Action<ToolEffectSignal> handler = OnEffectApplied;
            if (handler == null || module == null || effectType == EffectType.None || magnitude <= 0f)
                return;

            handler(new ToolEffectSignal(effectType, module, sourceTransform, magnitude, hitPointWorld));
        }
    }
}

// ============================================================================
// HECTON-8 - ToolEffectEvents.cs
// Zero-GC gameplay event bus for tool-driven effect signals.
// ============================================================================

using Hecton8.Core;
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
    /// Listener contract for immediate pre-repair tool effects.
    /// </summary>
    public interface IToolEffectListener
    {
        /// <summary>
        /// Consumes one tool-effect signal before the owning tool applies its final gameplay mutation.
        /// </summary>
        /// <param name="signal">Tool effect payload.</param>
        void OnToolEffectApplied(in ToolEffectSignal signal);
    }

    /// <summary>
    /// Immediate gameplay signal router for pre-repair tool effects.
    /// </summary>
    public static class ToolEffectEvents
    {
        private const int ListenerCapacity = 16;

        // COLD ALLOC: RegistryBucket<IToolEffectListener>[16] - immediate pre-repair tool effect listeners - owner: ToolEffectEvents
        private static readonly RegistryBucket<IToolEffectListener> _listeners = new RegistryBucket<IToolEffectListener>(ListenerCapacity);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _listeners.Clear();
        }

        /// <summary>
        /// Registers a listener for immediate tool-effect signals.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void Register(IToolEffectListener listener)
        {
            if (listener == null)
                return;

            if (!_listeners.Contains(listener))
                _listeners.Register(listener);
        }

        /// <summary>
        /// Unregisters a listener from immediate tool-effect signals.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void Unregister(IToolEffectListener listener)
        {
            if (listener == null)
                return;

            if (_listeners.Contains(listener))
                _listeners.Unregister(listener);
        }

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
            if (module == null || effectType == EffectType.None || magnitude <= 0f)
                return;

            int count = _listeners.Count;
            if (count <= 0)
                return;

            ToolEffectSignal signal = new ToolEffectSignal(effectType, module, sourceTransform, magnitude, hitPointWorld);
            IToolEffectListener[] rawArray = _listeners.RawArray;
            for (int i = count - 1; i >= 0; i--)
            {
                IToolEffectListener listener = rawArray[i];
                if (listener != null)
                    listener.OnToolEffectApplied(in signal);
            }
        }
    }
}

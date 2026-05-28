// ============================================================================
// HECTON-8 - ToolEffectEvents.cs
// Zero-GC gameplay event bus for tool-driven effect signals.
// ============================================================================

using System.Runtime.InteropServices;
using UnityEngine;
using Hecton8.Interaction;

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
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public readonly struct ToolEffectSignal
    {
        /// <summary>
        /// Creates a tool-effect payload for the current gameplay frame.
        /// </summary>
        public ToolEffectSignal(
            EffectType effectType,
            IRepairableModuleTarget moduleTarget,
            Transform sourceTransform,
            float magnitude,
            Vector3 hitPointWorld)
        {
            EffectType = effectType;
            ModuleTargetInstanceId = ResolveModuleTargetInstanceId(moduleTarget);
            SourceTransformInstanceId = ResolveComponentEntityId(sourceTransform);
            Magnitude = magnitude;
            HitPointWorld = hitPointWorld;
            SourcePositionWorld = sourceTransform != null ? sourceTransform.position : hitPointWorld;
        }

        /// <summary>Resolved gameplay effect type.</summary>
        [FieldOffset(0)]
        public readonly EffectType EffectType;

        /// <summary>Unity entity-derived id of the repairable module target under the active tool beam.</summary>
        [FieldOffset(4)]
        public readonly int ModuleTargetInstanceId;

        /// <summary>Unity entity-derived id of the transform that emitted the tool effect, when available.</summary>
        [FieldOffset(8)]
        public readonly int SourceTransformInstanceId;

        /// <summary>Effect magnitude in gameplay units for this frame.</summary>
        [FieldOffset(12)]
        public readonly float Magnitude;

        /// <summary>World hit point resolved by the active tool query.</summary>
        [FieldOffset(16)]
        public readonly Vector3 HitPointWorld;

        /// <summary>World position of the emitting transform, or hit point when no transform is available.</summary>
        [FieldOffset(28)]
        public readonly Vector3 SourcePositionWorld;

        private static int ResolveModuleTargetInstanceId(IRepairableModuleTarget moduleTarget)
        {
            return moduleTarget is Component component ? ResolveComponentEntityId(component) : 0;
        }

        private static int ResolveComponentEntityId(Component component)
        {
            return component != null ? unchecked((int)UnityEngine.EntityId.ToULong(component.GetEntityId())) : 0;
        }
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

        private struct ListenerSlot
        {
            public IToolEffectListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private struct ToolEffectListenerRegistry
        {
            private readonly ListenerSlot[] _slots;
            private int _count;

            public ToolEffectListenerRegistry(int capacity)
            {
                _slots = new ListenerSlot[capacity];
                _count = 0;
            }

            public int Count => _count;

            public void Clear()
            {
                for (int i = 0; i < _count; i++)
                    _slots[i].Clear();

                _count = 0;
            }

            public bool Contains(IToolEffectListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (ReferenceEquals(_slots[i].Listener, listener))
                        return true;
                }

                return false;
            }

            public bool TryRegister(IToolEffectListener listener)
            {
                if (listener == null || _count >= _slots.Length)
                    return false;

                _slots[_count++].Listener = listener;
                return true;
            }

            public void Unregister(IToolEffectListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (!ReferenceEquals(_slots[i].Listener, listener))
                        continue;

                    _count--;
                    _slots[i] = _slots[_count];
                    _slots[_count].Clear();
                    return;
                }
            }

            public IToolEffectListener GetAt(int index)
            {
                return (uint)index < (uint)_count ? _slots[index].Listener : null;
            }
        }

        // COLD ALLOC: ListenerSlot[16] - immediate pre-repair tool effect listeners - owner: ToolEffectEvents
        private static ToolEffectListenerRegistry _listeners = new ToolEffectListenerRegistry(ListenerCapacity);

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
                _listeners.TryRegister(listener);
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
        public static bool TryRaiseEffectApplied(
            EffectType effectType,
            IRepairableModuleTarget moduleTarget,
            Transform sourceTransform,
            float magnitude,
            Vector3 hitPointWorld)
        {
            if (moduleTarget == null || effectType == EffectType.None || magnitude <= 0f)
                return false;

            int count = _listeners.Count;
            if (count <= 0)
                return false;

            ToolEffectSignal signal = new ToolEffectSignal(effectType, moduleTarget, sourceTransform, magnitude, hitPointWorld);
            for (int i = count - 1; i >= 0; i--)
            {
                IToolEffectListener listener = _listeners.GetAt(i);
                if (listener != null)
                    listener.OnToolEffectApplied(in signal);
            }

            return true;
        }

        [System.Obsolete("Use TryRaiseEffectApplied so producer-side refusal is visible.", true)]
        public static void RaiseEffectApplied(
            EffectType effectType,
            IRepairableModuleTarget moduleTarget,
            Transform sourceTransform,
            float magnitude,
            Vector3 hitPointWorld)
        {
            TryRaiseEffectApplied(effectType, moduleTarget, sourceTransform, magnitude, hitPointWorld);
        }
    }
}

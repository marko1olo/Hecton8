// ============================================================================
// HECTON-8 — RuntimeInstanceId.cs
// 64-bit unique identifier for spawned objects. Zero GC, save-safe.
// ============================================================================
//
// ARCHITECTURE:
//   • 64-bit struct: 32-bit PrefabId + 32-bit Sequence.
//   • PrefabId: stable ID from PrefabRegistry (replaces GetInstanceID).
//   • Sequence: monotonically increasing per-prefab counter.
//   • Deterministic for save/load: (prefabId, sequence) tuple.
//
// USAGE:
//   RuntimeInstanceId id = RuntimeInstanceId.Create(prefabId);
//   long serialized = id.Value; // For save systems
//
// ZERO GC:
//   • Struct — stack allocated, no heap.
//   • No string operations.
//   • Implicit conversion to/from long for serialization.
//
// THREAD SAFETY:
//   • Sequence counter is Interlocked.Increment — thread-safe.
//   • PrefabId is immutable after creation.
// ============================================================================

using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// 64-bit unique identifier for runtime-spawned objects.
    /// Combines PrefabId (stable) + Sequence (unique per spawn).
    /// </summary>
    [System.Serializable]
    public struct RuntimeInstanceId : System.IEquatable<RuntimeInstanceId>
    {
        // ══════════════════════════════════════════════════════════
        //  BIT LAYOUT
        // ══════════════════════════════════════════════════════════

        // Bits 0-31:  PrefabId (stable, from PrefabRegistry)
        // Bits 32-63: Sequence (monotonically increasing)

        /// <summary>Raw 64-bit value for serialization.</summary>
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("runtimeId")]
        private long _value;

        /// <summary>Invalid ID (0).</summary>
        public static readonly RuntimeInstanceId Invalid = new RuntimeInstanceId(0);

        // ══════════════════════════════════════════════════════════
        //  SEQUENCE COUNTER
        // ══════════════════════════════════════════════════════════

        private static int _globalSequence;

        // ══════════════════════════════════════════════════════════
        //  CONSTRUCTORS
        // ══════════════════════════════════════════════════════════

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private RuntimeInstanceId(long value)
        {
            _value = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private RuntimeInstanceId(int prefabId, int sequence)
        {
            // Pack: prefabId in lower 32 bits, sequence in upper 32 bits
            _value = ((long)sequence << 32) | (uint)prefabId;
        }

        // ══════════════════════════════════════════════════════════
        //  FACTORY
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Creates a new unique ID for the given prefab.
        /// Thread-safe: uses Interlocked.Increment for sequence.
        /// </summary>
        /// <param name="prefabId">Stable prefab ID from PrefabRegistry.</param>
        /// <returns>New unique RuntimeInstanceId.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RuntimeInstanceId Create(int prefabId)
        {
            if (prefabId <= 0)
            {
                Hecton8.Core.H8Debug.LogError("[RuntimeInstanceId] Create: invalid prefabId (must be > 0)");
                return Invalid;
            }

            int sequence = Interlocked.Increment(ref _globalSequence);
            return new RuntimeInstanceId(prefabId, sequence);
        }

        /// <summary>
        /// Creates a new unique ID from a prefab GameObject.
        /// Registers the prefab if not already registered.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RuntimeInstanceId Create(GameObject prefab)
        {
            if (prefab == null)
            {
                Hecton8.Core.H8Debug.LogError("[RuntimeInstanceId] Create: prefab is null");
                return Invalid;
            }

            PrefabRegistry registry = null;
            if (!PrefabRegistry.TryResolveActiveRuntime(ref registry))
            {
                Hecton8.Core.H8Debug.LogError("[RuntimeInstanceId] Create: PrefabRegistry not initialized");
                return Invalid;
            }

            int prefabId = registry.GetOrRegisterPrefab(prefab);
            return Create(prefabId);
        }

        /// <summary>
        /// Reconstructs an ID from serialized value.
        /// Use for save/load systems.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RuntimeInstanceId FromValue(long value)
        {
            return new RuntimeInstanceId(value);
        }

        // ══════════════════════════════════════════════════════════
        //  PROPERTIES
        // ══════════════════════════════════════════════════════════

        /// <summary>Raw 64-bit value for serialization.</summary>
        public long Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _value;
        }

        /// <summary>Prefab ID (lower 32 bits).</summary>
        public int PrefabId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (int)(_value & 0xFFFFFFFF);
        }

        /// <summary>Sequence number (upper 32 bits).</summary>
        public int Sequence
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (int)(_value >> 32);
        }

        /// <summary>True if this is a valid (non-zero) ID.</summary>
        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _value != 0;
        }

        // ══════════════════════════════════════════════════════════
        //  CONVERSIONS
        // ══════════════════════════════════════════════════════════

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator long(RuntimeInstanceId id) => id._value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator RuntimeInstanceId(long value) => new RuntimeInstanceId(value);

        // ══════════════════════════════════════════════════════════
        //  EQUALITY
        // ══════════════════════════════════════════════════════════

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(RuntimeInstanceId other) => _value == other._value;

        public override bool Equals(object obj) => obj is RuntimeInstanceId other && Equals(other);

        public override int GetHashCode() => _value.GetHashCode();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(RuntimeInstanceId left, RuntimeInstanceId right) => left._value == right._value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(RuntimeInstanceId left, RuntimeInstanceId right) => left._value != right._value;

        // ══════════════════════════════════════════════════════════
        //  STRING (DEBUG ONLY)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Returns a debug string. NOT for hot paths — allocates string.
        /// </summary>
        public override string ToString()
        {
            return IsValid ? $"RID[{PrefabId}:{Sequence}]" : "RID[Invalid]";
        }
    }
}

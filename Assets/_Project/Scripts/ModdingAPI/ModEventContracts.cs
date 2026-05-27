using System;
using Unity.Mathematics;

namespace Hecton8.Modding
{
    /// <summary>
    /// Native first-party event streams exposed to mods as immutable byte payloads.
    /// </summary>
    public enum HectonNativeEventKind : byte
    {
        Interaction = 0,
        Crafting = 1
    }

    /// <summary>
    /// Public signal projection kinds exposed to managed mods through the native-to-managed bridge.
    /// </summary>
    public enum ModEventKind : ushort
    {
        None = 0,
        CombatDamage = 1,
        WeatherChanged = 2
    }

    /// <summary>
    /// Condensed, blittable public event metadata copied from first-party SignalBus snapshots.
    /// Coordinates are already relative to the current player runtime position.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 64)]
    public struct ModEventDto
    {
        public const uint CombatDamageEventHash = 0x43444D47u; // CDMG
        public const uint WeatherChangedEventHash = 0x57454154u; // WEAT
        public const ushort LowTierSampleFlag = 1 << 8;

        [System.Runtime.InteropServices.FieldOffset(0)] public uint EventHash;
        [System.Runtime.InteropServices.FieldOffset(4)] public uint SubjectHash;
        [System.Runtime.InteropServices.FieldOffset(8)] public uint ContextHash;
        [System.Runtime.InteropServices.FieldOffset(12)] public uint SourceHash;
        [System.Runtime.InteropServices.FieldOffset(16)] public uint Frame;
        [System.Runtime.InteropServices.FieldOffset(20)] public float3 RelativePosition;
        [System.Runtime.InteropServices.FieldOffset(32)] public float3 Direction;
        [System.Runtime.InteropServices.FieldOffset(44)] public float Scalar0;
        [System.Runtime.InteropServices.FieldOffset(48)] public float Scalar1;
        [System.Runtime.InteropServices.FieldOffset(52)] public ushort Kind;
        [System.Runtime.InteropServices.FieldOffset(54)] public ushort Flags;
        [System.Runtime.InteropServices.FieldOffset(56)] public byte QualityTier;
        [System.Runtime.InteropServices.FieldOffset(57)] public byte Reserved0;
        [System.Runtime.InteropServices.FieldOffset(58)] public ushort Sequence;
        [System.Runtime.InteropServices.FieldOffset(60)] public uint Reserved1;
    }

    /// <summary>
    /// Read-only player spawn snapshot for mod event hooks.
    /// No mutable engine arrays or Unity object references are exposed.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 24)]
    public readonly struct ModPlayerSpawnedEvent
    {
        public ModPlayerSpawnedEvent(ulong playerId, float3 absoluteUniversePosition, int biomeId)
        {
            PlayerId = playerId;
            AbsoluteUniversePosition = absoluteUniversePosition;
            BiomeId = biomeId;
        }

        [System.Runtime.InteropServices.FieldOffset(0)]
        public readonly ulong PlayerId;

        [System.Runtime.InteropServices.FieldOffset(8)]
        public readonly float3 AbsoluteUniversePosition;

        [System.Runtime.InteropServices.FieldOffset(20)]
        public readonly int BiomeId;
    }

    /// <summary>
    /// Read-only biome transition snapshot for mod event hooks.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 24)]
    public readonly struct ModBiomeChangedEvent
    {
        public ModBiomeChangedEvent(int previousBiomeId, int currentBiomeId, float3 absoluteUniversePosition)
        {
            PreviousBiomeId = previousBiomeId;
            CurrentBiomeId = currentBiomeId;
            AbsoluteUniversePosition = absoluteUniversePosition;
            _pad0 = 0u;
        }

        [System.Runtime.InteropServices.FieldOffset(0)]
        public readonly int PreviousBiomeId;

        [System.Runtime.InteropServices.FieldOffset(4)]
        public readonly int CurrentBiomeId;

        [System.Runtime.InteropServices.FieldOffset(8)]
        public readonly float3 AbsoluteUniversePosition;

        [System.Runtime.InteropServices.FieldOffset(20)]
        private readonly uint _pad0;
    }

    /// <summary>
    /// First-party bridge for safe mod event publication.
    /// </summary>
    internal static class HectonModHooks
    {
        internal static void PublishPlayerSpawned(in ModPlayerSpawnedEvent payload)
        {
            HectonEventBus.Publish(in payload);
        }

        internal static void PublishBiomeChanged(in ModBiomeChangedEvent payload)
        {
            HectonEventBus.Publish(in payload);
        }
    }

    /// <summary>
    /// Managed mod callback for read-only native event payload copies.
    /// The span is valid only for the callback duration and cannot expose native container ownership.
    /// </summary>
    /// <param name="eventKind">Native event lane that produced the payload.</param>
    /// <param name="payload">Blittable payload bytes copied from the internal native queue.</param>
    public delegate void HectonNativeEventHandler(HectonNativeEventKind eventKind, ReadOnlySpan<byte> payload);

    /// <summary>
    /// Managed callback for mod-facing unmanaged payload events.
    /// Payloads are passed by readonly reference and cannot contain managed references.
    /// </summary>
    /// <typeparam name="TPayload">Unmanaged event payload type.</typeparam>
    /// <param name="payload">Blittable event payload.</param>
    public delegate void HectonUnmanagedEventHandler<TPayload>(in TPayload payload)
        where TPayload : unmanaged;
}

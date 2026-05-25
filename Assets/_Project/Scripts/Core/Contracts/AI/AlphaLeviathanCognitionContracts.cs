using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts.AI.Cognition
{
    /// <summary>
    /// Legacy Alpha Leviathan phase byte values shared by Fauna and AI/Cognition telemetry.
    /// </summary>
    public static class AlphaLeviathanPhase
    {
        /// <summary>Predator is hidden or dormant.</summary>
        public const byte Hidden = 0;

        /// <summary>Predator circles the current anchor.</summary>
        public const byte Circling = 1;

        /// <summary>Predator commits to a false charge.</summary>
        public const byte FalseCharge = 2;

        /// <summary>Predator strike-compatible phase value retained for legacy readers.</summary>
        public const byte Strike = 3;

        /// <summary>Predator retreats using the legacy strike byte for wire compatibility.</summary>
        public const byte VeerOff = Strike;
    }

    /// <summary>
    /// Flags written into Alpha Leviathan telemetry entries.
    /// </summary>
    public static class AlphaLeviathanTelemetryFlags
    {
        /// <summary>Survival math radial fallback was used.</summary>
        public const byte SurvivalRadialFallback = 1 << 0;

        /// <summary>SDF contouring was requested and survived continuous quality pressure.</summary>
        public const byte SdfDiveRequested = 1 << 1;

        /// <summary>Player gaze crossed the predator exposure threshold.</summary>
        public const byte PlayerGazeBreak = 1 << 2;

        /// <summary>Legacy roar marker reserved for Fauna readers.</summary>
        public const byte RoarEmitted = 1 << 3;

        /// <summary>Invalid or non-finite stalk math was detected.</summary>
        public const byte Fault = 1 << 4;

        /// <summary>Legacy Fauna-side marker for Alpha rows without a player target. AI/Cognition must not write this bit.</summary>
        public const byte LegacyNoPlayerTarget = 1 << 5;

        /// <summary>Headlight exposure forced retreat behavior.</summary>
        public const byte LightRetreat = 1 << 6;

        /// <summary>AUP shift fence is active; first changed frame also reset steering history.</summary>
        public const byte ShiftFenceReset = 1 << 7;
    }

    /// <summary>
    /// Fixed-size blackbox row for Alpha Leviathan stalking state.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AlphaLeviathanTelemetryEntry
    {
        /// <summary>Simulation frame for this row.</summary>
        [FieldOffset(0)]
        public uint Frame;

        /// <summary>Distance from Leviathan to the active anchor in meters.</summary>
        [FieldOffset(4)]
        public float DistanceToPlayerMeters;

        /// <summary>Target fog-edge orbit ring in meters.</summary>
        [FieldOffset(8)]
        public float FogRingDistanceMeters;

        /// <summary>Leviathan-local telemetry reference sample.</summary>
        [FieldOffset(12)]
        public float3 Position;

        /// <summary>Active anchor delta after subtracting Leviathan AUP in double precision.</summary>
        [FieldOffset(24)]
        public float3 PlayerPosition;

        /// <summary>Sanitized desired steering direction.</summary>
        [FieldOffset(36)]
        public float3 DesiredDirection;

        /// <summary>Deterministic state hash for crash triage.</summary>
        [FieldOffset(48)]
        public uint StateHash;

        /// <summary>Reported aggression scalar clamped to 0..1.</summary>
        [FieldOffset(52)]
        public float LeviathanAgressivity01;

        /// <summary>Observed AUP shift frame ID that produced this row.</summary>
        [FieldOffset(56)]
        public uint Reserved1;

        /// <summary>Dense Alpha Leviathan slot index.</summary>
        [FieldOffset(60)]
        public ushort Slot;

        /// <summary>Current phase byte from <see cref="AlphaLeviathanPhase"/>.</summary>
        [FieldOffset(62)]
        public byte Phase;

        /// <summary>Bitmask from <see cref="AlphaLeviathanTelemetryFlags"/>.</summary>
        [FieldOffset(63)]
        public byte Flags;
    }
}

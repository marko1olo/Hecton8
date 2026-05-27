using System;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core.Contracts
{
    internal static class PrologueSequenceContractLayout
    {
        public const int OrbitalSnapshotStrideBytes = 48;
        public const int AtmosphericReentrySnapshotStrideBytes = 16;
        public const int CompleteSnapshotStrideBytes = 16;
    }

    /// <summary>
    /// Deterministic prologue pacing states. Values are persisted in black-box telemetry.
    /// </summary>
    public enum PrologueStage : byte
    {
        None = 0,
        AwaitingAtmosphericReentry = 1,
        OrbitalSilence = 2,
        ReentryBurn = 3,
        ManualOverride = 4,
        ImpactSync = 5,
        AwaitOceanHydration = 6,
        WaterTransition = 7,
        Complete = 8,
        DevSkip = 9,
        Cancelled = 10,
        Faulted = 11
    }

    [Flags]
    public enum PrologueInputLockFlags : byte
    {
        None = 0,
        Look = 1 << 0,
        Translation = 1 << 1,
        Interaction = 1 << 2
    }

    public enum PrologueHydrationMode : byte
    {
        HighResolutionSurface = 0,
        LowTierProxySurface = 1,
        DevForcedShallowWater = 2,
        StandaloneOrbitHandoffProxy = 3
    }

    public static class PrologueCancelReasons
    {
        public const byte TokenCancelled = 1;
        public const byte ExplicitCancel = 2;
        public const byte DevSkip = 3;
        public const byte NonFinite = 4;
    }

    public static class PrologueSignalSourceHashes
    {
        public const uint SequenceDirector = 0x50524C47u; // PRLG
        public const uint ManualOverrideLever = 0x4D4F5652u; // MOVR
        public const uint OrbitalRelativityDirector = 0x4F524249u; // ORBI
    }

    [StructLayout(LayoutKind.Explicit, Size = PrologueSequenceContractLayout.OrbitalSnapshotStrideBytes)]
    public readonly struct PrologueOrbitalSnapshot
    {
        public PrologueOrbitalSnapshot(
            double3 universeVelocity,
            double planetDistanceMeters,
            float reentryHeat01,
            float cloudWhiteout01,
            uint sequence,
            byte mathLod,
            byte flags)
        {
            UniverseVelocity = universeVelocity;
            PlanetDistanceMeters = planetDistanceMeters;
            ReentryHeat01 = reentryHeat01;
            CloudWhiteout01 = cloudWhiteout01;
            Sequence = sequence;
            MathLod = mathLod;
            Flags = flags;
        }

        [FieldOffset(0)] public readonly double3 UniverseVelocity;
        [FieldOffset(24)] public readonly double PlanetDistanceMeters;
        [FieldOffset(32)] public readonly float ReentryHeat01;
        [FieldOffset(36)] public readonly float CloudWhiteout01;
        [FieldOffset(40)] public readonly uint Sequence;
        [FieldOffset(44)] public readonly byte MathLod;
        [FieldOffset(45)] public readonly byte Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = PrologueSequenceContractLayout.AtmosphericReentrySnapshotStrideBytes)]
    public readonly struct PrologueAtmosphericReentrySnapshot
    {
        public PrologueAtmosphericReentrySnapshot(
            float altitudeMeters,
            float universeVelocityMetersPerSecond,
            float heat01,
            ushort sequence,
            byte phase,
            byte flags)
        {
            AltitudeMeters = altitudeMeters;
            UniverseVelocityMetersPerSecond = universeVelocityMetersPerSecond;
            Heat01 = heat01;
            Sequence = sequence;
            Phase = phase;
            Flags = flags;
        }

        [FieldOffset(0)] public readonly float AltitudeMeters;
        [FieldOffset(4)] public readonly float UniverseVelocityMetersPerSecond;
        [FieldOffset(8)] public readonly float Heat01;
        [FieldOffset(12)] public readonly ushort Sequence;
        [FieldOffset(14)] public readonly byte Phase;
        [FieldOffset(15)] public readonly byte Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = PrologueSequenceContractLayout.CompleteSnapshotStrideBytes)]
    public readonly struct PrologueCompleteSnapshot
    {
        public PrologueCompleteSnapshot(uint frame, float whiteoutHoldSeconds, ushort sequence, byte phase, byte flags)
        {
            Frame = frame;
            WhiteoutHoldSeconds = whiteoutHoldSeconds;
            Sequence = sequence;
            Phase = phase;
            Flags = flags;
        }

        [FieldOffset(0)] public readonly uint Frame;
        [FieldOffset(4)] public readonly float WhiteoutHoldSeconds;
        [FieldOffset(8)] public readonly ushort Sequence;
        [FieldOffset(10)] public readonly byte Phase;
        [FieldOffset(11)] public readonly byte Flags;
    }

    public interface IPrologueSequenceRuntime
    {
        bool IsDevelopmentBuild { get; }
        bool IsLowTier { get; }
        bool IsStandaloneOrbitHandoffProxyAllowed { get; }
        uint CurrentFrame { get; }
        bool ShouldSkipPrologue { get; }

        bool TryGetOrbitalSnapshot(out PrologueOrbitalSnapshot snapshot);
        bool TryConsumeAtmosphericReentry(out PrologueAtmosphericReentrySnapshot snapshot);
        bool TryConsumePrologueComplete(out PrologueCompleteSnapshot snapshot);
        bool IsOceanSurfaceReady(bool allowProxy);

        void PrepareSequenceRun();
        Awaitable DelayDilatedAsync(float seconds, CancellationToken cancellationToken);
        Awaitable NextFrameAsync(CancellationToken cancellationToken);

        void PublishInputLock(PrologueInputLockFlags flags, bool paused);
        void PublishMuffledBreathing(float intensity01, float durationSeconds);
        void PublishHullTempCriticalWarning(float severity01);
        void PublishHeavyRumble(float intensity01, float durationSeconds);
        void PublishManualReleasePrompt();
        void PublishMassiveImpact();
        void PublishOceanHandoff();
        void ZeroUniverseVelocity();
        void ForceShallowWaterHydration();
        void PushTelemetry(PrologueStage stage, uint stateHash, byte flags);
        void DumpBlackBox();
    }

    public interface IPrologueSequenceService : IDisposable
    {
        bool IsConfigured { get; }
        bool IsRunning { get; }
        PrologueStage CurrentStage { get; }

        void Configure(IPrologueSequenceRuntime runtime);
        Awaitable RunPrologueSequenceAsync(CancellationToken cancellationToken);
        void CancelSequence(byte reason);
    }
}

using System;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core.Contracts
{
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
        DevForcedShallowWater = 2
    }

    public static class PrologueCancelReasons
    {
        public const byte TokenCancelled = 1;
        public const byte ExplicitCancel = 2;
        public const byte DevSkip = 3;
        public const byte NonFinite = 4;
    }

    [StructLayout(LayoutKind.Sequential)]
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

        public double3 UniverseVelocity { get; }
        public double PlanetDistanceMeters { get; }
        public float ReentryHeat01 { get; }
        public float CloudWhiteout01 { get; }
        public uint Sequence { get; }
        public byte MathLod { get; }
        public byte Flags { get; }
    }

    [StructLayout(LayoutKind.Sequential)]
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

        public float AltitudeMeters { get; }
        public float UniverseVelocityMetersPerSecond { get; }
        public float Heat01 { get; }
        public ushort Sequence { get; }
        public byte Phase { get; }
        public byte Flags { get; }
    }

    [StructLayout(LayoutKind.Sequential)]
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

        public uint Frame { get; }
        public float WhiteoutHoldSeconds { get; }
        public ushort Sequence { get; }
        public byte Phase { get; }
        public byte Flags { get; }
    }

    public interface IPrologueSequenceRuntime
    {
        bool IsDevelopmentBuild { get; }
        bool IsLowTier { get; }
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

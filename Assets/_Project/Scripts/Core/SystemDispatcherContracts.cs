using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Core
{
    /// <summary>
    /// Hard phase order owned by SystemDispatcher.
    /// </summary>
    public enum DispatcherPhase : byte
    {
        None = 0,
        PreSimulation = 1,
        Simulation = 2,
        PostSimulation = 3,
        VisualSync = 4
    }

    /// <summary>
    /// Independent fence timelines owned by SystemDispatcher.
    /// </summary>
    public enum DispatcherFenceDomain : byte
    {
        Simulation = 0,
        Physics = 1,
        Audio = 2,
        Netcode = 3
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct DispatcherStateDTO
    {
        [FieldOffset(0)] public uint CurrentPhaseId;
        [FieldOffset(4)] public uint CurrentFrame;
        [FieldOffset(8)] public uint ActiveBucket;
        [FieldOffset(12)] public uint ActiveBucketMask;
        [FieldOffset(16)] public uint SortedSystemCount;
        [FieldOffset(20)] public uint DisabledSystemCount;
        [FieldOffset(24)] public uint PendingSimulationJobCount;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct DispatcherTimingDTO
    {
        [FieldOffset(0)] public float FrameDelta;
        [FieldOffset(4)] public float FixedDelta;
        [FieldOffset(8)] public float TimeScale;
        [FieldOffset(12)] public uint ActiveBucketMask;

        [FieldOffset(0)] public float PreSimMs;
        [FieldOffset(4)] public float SimWaitMs;
        [FieldOffset(8)] public float PostSimMs;
        [FieldOffset(12)] public float VisualSyncMs;
        [FieldOffset(16)] public uint FrameId;
        [FieldOffset(20)] public byte _pad0;
        [FieldOffset(21)] public byte _pad1;
        [FieldOffset(22)] public byte _pad2;
        [FieldOffset(23)] public byte _pad3;
        [FieldOffset(24)] public byte _pad4;
        [FieldOffset(25)] public byte _pad5;
        [FieldOffset(26)] public byte _pad6;
        [FieldOffset(27)] public byte _pad7;
        [FieldOffset(28)] public byte _pad8;
        [FieldOffset(29)] public byte _pad9;
        [FieldOffset(30)] public byte _pad10;
        [FieldOffset(31)] public byte _pad11;
    }

    public static class DispatcherTimingLayoutGuard
    {
        public const int SizeBytes = 32;

        public static bool ValidateLayout()
        {
            return UnsafeUtility.SizeOf<DispatcherTimingDTO>() == SizeBytes &&
                   GetOffset(nameof(DispatcherTimingDTO.PreSimMs)) == 0 &&
                   GetOffset(nameof(DispatcherTimingDTO.SimWaitMs)) == 4 &&
                   GetOffset(nameof(DispatcherTimingDTO.PostSimMs)) == 8 &&
                   GetOffset(nameof(DispatcherTimingDTO.VisualSyncMs)) == 12 &&
                   GetOffset(nameof(DispatcherTimingDTO.FrameId)) == 16 &&
                   GetOffset(nameof(DispatcherTimingDTO._pad0)) == 20 &&
                   GetOffset(nameof(DispatcherTimingDTO._pad11)) == 31;
        }

        private static int GetOffset(string fieldName)
        {
            return Marshal.OffsetOf<DispatcherTimingDTO>(fieldName).ToInt32();
        }
    }

    public static class DispatcherPresentationSuppressionFlags
    {
        public const uint None = 0u;
        public const uint VisualSyncSuppressed = 1u << 0;
        public const uint RollbackFence = 1u << 1;
        public const uint HealthPressure = 1u << 2;
        public const uint AudioSuppression = 1u << 3;
        public const uint ParticleSuppression = 1u << 4;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct DispatcherPresentationSuppressionDTO
    {
        [FieldOffset(0)] public uint FrameId;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public float GlobalQualityWeight;
        [FieldOffset(12)] public float Suppression01;
        [FieldOffset(16)] public uint RollbackFlags;
        [FieldOffset(20)] public byte _pad0;
        [FieldOffset(21)] public byte _pad1;
        [FieldOffset(22)] public byte _pad2;
        [FieldOffset(23)] public byte _pad3;
        [FieldOffset(24)] public byte _pad4;
        [FieldOffset(25)] public byte _pad5;
        [FieldOffset(26)] public byte _pad6;
        [FieldOffset(27)] public byte _pad7;
        [FieldOffset(28)] public byte _pad8;
        [FieldOffset(29)] public byte _pad9;
        [FieldOffset(30)] public byte _pad10;
        [FieldOffset(31)] public byte _pad11;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct JobDependencyDTO
    {
        [FieldOffset(0)] public ulong JobHandleBits;
        [FieldOffset(8)] public uint SystemIdHash;
        [FieldOffset(12)] public uint FrameId;
        [FieldOffset(16)] public uint DependencyHash0;
        [FieldOffset(20)] public byte PhaseId;
        [FieldOffset(21)] public byte DomainId;
        [FieldOffset(22)] public byte DependencyCount;
        [FieldOffset(23)] public byte BucketId;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct DispatcherFenceTelemetryEntry
    {
        [FieldOffset(0)] public uint FrameId;
        [FieldOffset(4)] public uint ScheduledJobCount;
        [FieldOffset(8)] public uint SafetyBypassCount;
        [FieldOffset(12)] public uint DomainMask;
        [FieldOffset(16)] public float SimulationWaitMs;
        [FieldOffset(20)] public float FixedWaitMs;
        [FieldOffset(24)] public float AupHardFenceMs;
        [FieldOffset(28)] public float GlobalQualityWeight;
        [FieldOffset(32)] public ulong MasterSimulationHandleBits;
        [FieldOffset(40)] public ulong PhysicsHandleBits;
        [FieldOffset(48)] public ulong AudioHandleBits;
        [FieldOffset(56)] public ulong NetcodeHandleBits;
    }

    public static class DispatcherFenceTelemetryLayoutGuard
    {
        public const int SizeBytes = 64;

        public static bool ValidateLayout()
        {
            return UnsafeUtility.SizeOf<DispatcherFenceTelemetryEntry>() == SizeBytes &&
                   GetOffset(nameof(DispatcherFenceTelemetryEntry.FrameId)) == 0 &&
                   GetOffset(nameof(DispatcherFenceTelemetryEntry.ScheduledJobCount)) == 4 &&
                   GetOffset(nameof(DispatcherFenceTelemetryEntry.SafetyBypassCount)) == 8 &&
                   GetOffset(nameof(DispatcherFenceTelemetryEntry.DomainMask)) == 12 &&
                   GetOffset(nameof(DispatcherFenceTelemetryEntry.SimulationWaitMs)) == 16 &&
                   GetOffset(nameof(DispatcherFenceTelemetryEntry.FixedWaitMs)) == 20 &&
                   GetOffset(nameof(DispatcherFenceTelemetryEntry.AupHardFenceMs)) == 24 &&
                   GetOffset(nameof(DispatcherFenceTelemetryEntry.GlobalQualityWeight)) == 28 &&
                   GetOffset(nameof(DispatcherFenceTelemetryEntry.MasterSimulationHandleBits)) == 32 &&
                   GetOffset(nameof(DispatcherFenceTelemetryEntry.PhysicsHandleBits)) == 40 &&
                   GetOffset(nameof(DispatcherFenceTelemetryEntry.AudioHandleBits)) == 48 &&
                   GetOffset(nameof(DispatcherFenceTelemetryEntry.NetcodeHandleBits)) == 56;
        }

        private static int GetOffset(string fieldName)
        {
            return Marshal.OffsetOf<DispatcherFenceTelemetryEntry>(fieldName).ToInt32();
        }
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 64)]
    public struct DispatcherPipelineTelemetryEntry
    {
        [System.Runtime.InteropServices.FieldOffset(0)]
        public uint Frame;
        [System.Runtime.InteropServices.FieldOffset(4)]
        public float PreSimulationTimeMs;
        [System.Runtime.InteropServices.FieldOffset(8)]
        public float SimWaitTimeMs;
        [System.Runtime.InteropServices.FieldOffset(12)]
        public float PostSimulationTimeMs;
        [System.Runtime.InteropServices.FieldOffset(16)]
        public float VisualSyncTimeMs;
        [System.Runtime.InteropServices.FieldOffset(20)]
        public uint ActiveBucket;
        [System.Runtime.InteropServices.FieldOffset(24)]
        public uint SystemCount;
        [System.Runtime.InteropServices.FieldOffset(28)]
        public uint Flags;
        [System.Runtime.InteropServices.FieldOffset(32)]
        private byte _pad0;
        [System.Runtime.InteropServices.FieldOffset(33)]
        private byte _pad1;
        [System.Runtime.InteropServices.FieldOffset(34)]
        private byte _pad2;
        [System.Runtime.InteropServices.FieldOffset(35)]
        private byte _pad3;
        [System.Runtime.InteropServices.FieldOffset(36)]
        private byte _pad4;
        [System.Runtime.InteropServices.FieldOffset(37)]
        private byte _pad5;
        [System.Runtime.InteropServices.FieldOffset(38)]
        private byte _pad6;
        [System.Runtime.InteropServices.FieldOffset(39)]
        private byte _pad7;
        [System.Runtime.InteropServices.FieldOffset(40)]
        private byte _pad8;
        [System.Runtime.InteropServices.FieldOffset(41)]
        private byte _pad9;
        [System.Runtime.InteropServices.FieldOffset(42)]
        private byte _pad10;
        [System.Runtime.InteropServices.FieldOffset(43)]
        private byte _pad11;
        [System.Runtime.InteropServices.FieldOffset(44)]
        private byte _pad12;
        [System.Runtime.InteropServices.FieldOffset(45)]
        private byte _pad13;
        [System.Runtime.InteropServices.FieldOffset(46)]
        private byte _pad14;
        [System.Runtime.InteropServices.FieldOffset(47)]
        private byte _pad15;
        [System.Runtime.InteropServices.FieldOffset(48)]
        private byte _pad16;
        [System.Runtime.InteropServices.FieldOffset(49)]
        private byte _pad17;
        [System.Runtime.InteropServices.FieldOffset(50)]
        private byte _pad18;
        [System.Runtime.InteropServices.FieldOffset(51)]
        private byte _pad19;
        [System.Runtime.InteropServices.FieldOffset(52)]
        private byte _pad20;
        [System.Runtime.InteropServices.FieldOffset(53)]
        private byte _pad21;
        [System.Runtime.InteropServices.FieldOffset(54)]
        private byte _pad22;
        [System.Runtime.InteropServices.FieldOffset(55)]
        private byte _pad23;
        [System.Runtime.InteropServices.FieldOffset(56)]
        private byte _pad24;
        [System.Runtime.InteropServices.FieldOffset(57)]
        private byte _pad25;
        [System.Runtime.InteropServices.FieldOffset(58)]
        private byte _pad26;
        [System.Runtime.InteropServices.FieldOffset(59)]
        private byte _pad27;
        [System.Runtime.InteropServices.FieldOffset(60)]
        private byte _pad28;
        [System.Runtime.InteropServices.FieldOffset(61)]
        private byte _pad29;
        [System.Runtime.InteropServices.FieldOffset(62)]
        private byte _pad30;
        [System.Runtime.InteropServices.FieldOffset(63)]
        private byte _pad31;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public partial struct MockTimeDilationSignal
    {
        [FieldOffset(0)] public float TimeScale;
        [FieldOffset(4)] public float FrameDelta;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint SourceHash;
    }

    public interface IRequire<T>
    {
    }

    public interface IDispatcherSystem
    {
        uint GetSystemIdHash();

        DispatcherPhase GetDispatcherPhase();

        byte GetBucketId();

        int GetDependencyCount();

        uint GetDependencyHash(int dependencyIndex);

        void PreSimulationTick(in DispatcherTimingDTO timing);

        JobHandle ScheduleSimulation(
            in DispatcherTimingDTO timing,
            in DispatcherJobContext context,
            JobHandle dependsOn);

        void PostSimulationTick(in DispatcherTimingDTO timing);

        void VisualSyncTick(in DispatcherTimingDTO timing);
    }

    public interface IDispatcherFixedSystem
    {
        uint GetFixedSystemIdHash();

        JobHandle ScheduleFixedSimulation(in DispatcherTimingDTO timing, JobHandle dependsOn);

        void PostFixedSimulation(in DispatcherTimingDTO timing);
    }

    public ref struct DispatcherJobContext
    {
        internal NativeArray<MockTimeDilationSignal> MockTimeDilationSignals;
        internal NativeArray<JobDependencyDTO> JobDependencyTelemetry;
        public uint Frame;
        public uint ActiveBucket;
    }

    public interface IDispatcherFenceDomainProvider
    {
        DispatcherFenceDomain GetFenceDomain();
    }

    public sealed class FatalArchitectureException : Exception
    {
        public FatalArchitectureException(string message)
            : base(message)
        {
        }
    }

    public struct MockTickableSystem : IDispatcherSystem, IRequire<MockTimeDilationSignal>
    {
        public uint SystemIdHash;
        public uint Dependency0;
        public uint Dependency1;
        public byte DependencyCount;
        public byte BucketId;
        public byte PhaseId;
        public byte SignalIndex;
        public uint CostMicroseconds;

        public uint GetSystemIdHash() => SystemIdHash;

        public DispatcherPhase GetDispatcherPhase() => (DispatcherPhase)PhaseId;

        public byte GetBucketId() => BucketId;

        public int GetDependencyCount() => DependencyCount;

        public uint GetDependencyHash(int dependencyIndex)
        {
            if (dependencyIndex == 0)
                return Dependency0;
            if (dependencyIndex == 1)
                return Dependency1;

            return 0u;
        }

        public void PreSimulationTick(in DispatcherTimingDTO timing)
        {
        }

        public JobHandle ScheduleSimulation(
            in DispatcherTimingDTO timing,
            in DispatcherJobContext context,
            JobHandle dependsOn)
        {
            if (!context.MockTimeDilationSignals.IsCreated ||
                (uint)SignalIndex >= (uint)context.MockTimeDilationSignals.Length)
            {
                return dependsOn;
            }

            MockTimeDilationSignalJob job = default;
            job.Signals = context.MockTimeDilationSignals;
            job.SignalIndex = SignalIndex;
            job.Frame = context.Frame;
            job.SystemHash = SystemIdHash;
            job.FrameDelta = timing.FrameDelta;
            return job.Schedule(dependsOn);
        }

        public void PostSimulationTick(in DispatcherTimingDTO timing)
        {
        }

        public void VisualSyncTick(in DispatcherTimingDTO timing)
        {
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct MockTimeDilationSignalJob : IJob
    {
        [NoAlias] public NativeArray<MockTimeDilationSignal> Signals;
        public int SignalIndex;
        public uint Frame;
        public uint SystemHash;
        public float FrameDelta;

        public void Execute()
        {
            uint x = Frame ^ (SystemHash * 747796405u);
            x = (x ^ (x >> 16)) * 2246822519u;
            float scale = (x & 3u) == 0u ? 0.1f : 1f;

            MockTimeDilationSignal signal = default;
            signal.TimeScale = scale;
            signal.FrameDelta = FrameDelta * scale;
            signal.Frame = Frame;
            signal.SourceHash = SystemHash;
            Signals[SignalIndex] = signal;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct DispatcherMockDependencyStressJob : IJob
    {
        [NoAlias] public NativeArray<uint> Results;
        public uint Seed;
        public int Index;
        public int Iterations;

        public void Execute()
        {
            uint x = Seed ^ unchecked((uint)(Index * 747796405));
            int count = math.max(64, Iterations);
            for (int i = 0; i < count; i++)
            {
                x ^= x >> 16;
                x *= 2246822519u;
                x ^= x >> 13;
                x *= 3266489917u;
                x ^= x >> 16;
            }

            if (Results.IsCreated && (uint)Index < (uint)Results.Length)
                Results[Index] = x;
        }
    }
}

using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;

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

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct DispatcherStateDTO
    {
        public uint CurrentPhaseId;
        public uint CurrentFrame;
        public uint ActiveBucket;
        public uint ActiveBucketMask;
        public uint SortedSystemCount;
        public uint DisabledSystemCount;
        public uint PendingSimulationJobCount;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct DispatcherTimingDTO
    {
        public float FrameDelta;
        public float FixedDelta;
        public float TimeScale;
        public uint ActiveBucketMask;
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct JobDependencyDTO
    {
        public ulong JobHandlePtr;
        public uint SystemIdHash;
        public uint _pad0;
    }

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct DispatcherPipelineTelemetryEntry
    {
        public uint Frame;
        public float PreSimulationTimeMs;
        public float SimWaitTimeMs;
        public float PostSimulationTimeMs;
        public float VisualSyncTimeMs;
        public uint ActiveBucket;
        public uint SystemCount;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public partial struct MockTimeDilationSignal
    {
        public float TimeScale;
        public float FrameDelta;
        public uint Frame;
        public uint SourceHash;
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

    public struct DispatcherJobContext
    {
        public NativeArray<MockTimeDilationSignal> MockTimeDilationSignals;
        public NativeArray<JobDependencyDTO> JobDependencyTelemetry;
        public uint Frame;
        public uint ActiveBucket;
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

    public struct MockTimeDilationSignalJob : IJob
    {
        public NativeArray<MockTimeDilationSignal> Signals;
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
}

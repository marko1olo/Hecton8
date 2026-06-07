using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Crafting
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct FabricationJobDTO
    {
        [FieldOffset(0)] public double3 TargetAUP;
        [FieldOffset(24)] public float Progress01;
        [FieldOffset(28)] public uint TargetPrefabHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct FabricationJobSnapshotDTO
    {
        [FieldOffset(0)] public double3 TargetAUP;
        [FieldOffset(24)] public float Progress01;
        [FieldOffset(28)] public uint TargetPrefabHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 96)]
    internal struct FabricationRuntimeDTO
    {
        [FieldOffset(0)] public double3 FabricatorAUP;
        [FieldOffset(24)] public float DurationSeconds;
        [FieldOffset(28)] public float BuildSpeedMultiplier;
        [FieldOffset(32)] public float PowerPotential01;
        [FieldOffset(36)] public float BoundsMinY;
        [FieldOffset(40)] public float BoundsMaxY;
        [FieldOffset(44)] public float GlobalQualityWeight;
        [FieldOffset(48)] public float ThermalThrottle01;
        [FieldOffset(52)] public uint FabricatorHash;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint FrameBegan;
        [FieldOffset(64)] public uint FrameCompleted;
        [FieldOffset(68)] public uint Sequence;
        [FieldOffset(72)] public uint RollbackHash;
        [FieldOffset(76)] public float PowerDrainWatts;
        [FieldOffset(80)] public float LastDelta01;
        [FieldOffset(84)] public uint Reserved0;
        [FieldOffset(88)] public ulong Reserved1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 96)]
    internal struct FabricationGpuPayloadDTO
    {
        [FieldOffset(0)] public float4 BoundsProgress;
        [FieldOffset(16)] public float4 LocalOffsetPause;
        [FieldOffset(32)] public float4 WorldToFabricatorRow0;
        [FieldOffset(48)] public float4 WorldToFabricatorRow1;
        [FieldOffset(64)] public float4 WorldToFabricatorRow2;
        [FieldOffset(80)] public float4 WorldToFabricatorRow3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct FabricationCompletedSignal : ISignal
    {
        [FieldOffset(0)] public double3 TargetAUP;
        [FieldOffset(24)] public uint TargetPrefabHash;
        [FieldOffset(28)] public uint FabricatorHash;
        [FieldOffset(32)] public uint Frame;
        [FieldOffset(36)] public uint RollbackHash;
        [FieldOffset(40)] public float Progress01;
        [FieldOffset(44)] public byte Flags;
        [FieldOffset(45)] public byte Slot;
        [FieldOffset(46)] public ushort Reserved0;
        [FieldOffset(48)] public ulong Sequence;
        [FieldOffset(56)] public ulong Reserved1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct FabricationTickSignal : ISignal
    {
        [FieldOffset(0)] public double3 TargetAUP;
        [FieldOffset(24)] public float Progress01;
        [FieldOffset(28)] public float EmissionMultiplier;
        [FieldOffset(32)] public float PowerPotential01;
        [FieldOffset(36)] public float GlobalQualityWeight;
        [FieldOffset(40)] public uint TargetPrefabHash;
        [FieldOffset(44)] public uint FabricatorHash;
        [FieldOffset(48)] public uint Frame;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public ulong Sequence;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct FabricationTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint ActiveJobs;
        [FieldOffset(8)] public uint CompletedJobs;
        [FieldOffset(12)] public uint FaultFlags;
        [FieldOffset(16)] public uint RollbackHash;
        [FieldOffset(20)] public float AverageProgress01;
        [FieldOffset(24)] public float GlobalQualityWeight;
        [FieldOffset(28)] public float VisualUploadMicroseconds;
        [FieldOffset(32)] public float SimulationBudgetMicroseconds;
        [FieldOffset(36)] public float PowerPotential01;
        [FieldOffset(40)] public float MinProgress01;
        [FieldOffset(44)] public float MaxProgress01;
        [FieldOffset(48)] public uint LastTargetPrefabHash;
        [FieldOffset(52)] public uint LastFabricatorHash;
        [FieldOffset(56)] public ulong Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct FabricationTuningDTO
    {
        [FieldOffset(0)] public float BaseBuildSpeedMultiplier;
        [FieldOffset(4)] public float PowerDrawMultiplier;
        [FieldOffset(8)] public float ShaderEdgeGlowIntensity;
        [FieldOffset(12)] public float ReservedFloat0;
        [FieldOffset(16)] public uint CsvTimingsVersion;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public ulong Reserved0;
        [FieldOffset(32)] public ulong Reserved1;
        [FieldOffset(40)] public ulong Reserved2;
        [FieldOffset(48)] public ulong Reserved3;
        [FieldOffset(56)] public ulong Reserved4;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct FabricationTimingDTO
    {
        [FieldOffset(0)] public uint PrefabHash;
        [FieldOffset(4)] public float DurationSeconds;
        [FieldOffset(8)] public float PowerDrawMultiplier;
        [FieldOffset(12)] public uint Flags;
    }

    public struct FabricationRuntimeSnapshot
    {
        public float Progress01;
        public float DurationSeconds;
        public uint TargetPrefabHash;
        public uint Flags;
        public uint RollbackHash;
    }

    public struct FabricationEditorStats
    {
        public int ActiveJobs;
        public int CompletedJobs;
        public float AverageProgress01;
        public float GlobalQualityWeight;
        public uint RollbackHash;
        public uint FaultFlags;
    }

    public struct FabricationEditorJobDebug
    {
        public double3 TargetAUP;
        public float Progress01;
        public float BoundsMinY;
        public float BoundsMaxY;
        public uint TargetPrefabHash;
        public uint FabricatorHash;
        public uint Flags;
    }

    internal static class FabricationAssemblerFlags
    {
        public const uint Active = 1u << 0;
        public const uint Paused = 1u << 1;
        public const uint Completed = 1u << 2;
        public const uint Deconstruct = 1u << 3;
        public const uint Fault = 1u << 4;
        public const uint Mock = 1u << 5;
        public const uint CompletionObserved = 1u << 6;
        public const uint Dirty = 1u << 7;
        public const uint SignalDrop = 1u << 8;
    }

    public sealed class FabricationAssemblerRuntime : IGlobalRegistryHotSwapListener, IColdTickable
    {
        public const int MaxFabricationJobs = 128;
        public const int TelemetryFrameCount = 300;
        public const int MockFabricationJobCount = 50;
        public const int TimingLookupCapacity = 256;
#if UNITY_EDITOR
        public const int CsvScratchByteCapacity = 65536;
#endif
        public const uint SystemHash = 0x53483142u; // SH1B

        private const SystemID OwnerSystemId = SystemID.Construction;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_FABRICATION_ASSEMBLER.bin";
        private const uint FabricationCompletedLaneHash = 0x4631434Fu; // F1CO
        private const uint FabricationTickLaneHash = 0x46315449u; // F1TI
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;
        private static readonly int AssemblyPayloadsId = Shader.PropertyToID("_H8FabricationAssemblyPayloads");
        private static readonly int AssemblyPayloadCountId = Shader.PropertyToID("_H8FabricationAssemblyPayloadCount");
        private static readonly int AssemblyQualityId = Shader.PropertyToID("_H8FabricationAssemblyQuality");
        private static readonly int AssemblyEdgeBoostId = Shader.PropertyToID("_H8FabricationAssemblyEdgeBoost");

        private static FabricationAssemblerRuntime s_active;
        private static readonly System.Threading.WaitCallback TelemetryDumpWorkerCallback = RunTelemetryDumpWorker;
#if UNITY_EDITOR
        private static byte[] s_csvManagedScratch;
        private static FabricationTimingDTO[] s_timingManagedScratch;
        private static int s_csvScratchBusy;
#endif

        private readonly PreSimulationPhaseSystem _preSimulationPhase;
        private readonly SimulationPhaseSystem _simulationPhase;
        private readonly PostSimulationPhaseSystem _postSimulationPhase;
        private readonly VisualSyncPhaseSystem _visualSyncPhase;
        private readonly FabricationTelemetryEntry[] _telemetryDumpSnapshot = new FabricationTelemetryEntry[TelemetryFrameCount];

        private IDataVault _vault;
        private VaultGenerationHandle<FabricationJobDTO> _jobsHandle;
        private VaultGenerationHandle<FabricationRuntimeDTO> _runtimeHandle;
        private VaultGenerationHandle<FabricationGpuPayloadDTO> _gpuPayloadHandle;
        private VaultGenerationHandle<FabricationTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<FabricationTuningDTO> _tuningHandle;
        private VaultGenerationHandle<FabricationTimingDTO> _timingHandle;
        private VaultGenerationHandle<ScalabilityStateDTO> _scalabilityHandle;

        private GraphicsBuffer _gpuPayloadBufferA;
        private GraphicsBuffer _gpuPayloadBufferB;
        private bool _registeredPreSimulation;
        private bool _registeredSimulation;
        private bool _registeredPostSimulation;
        private bool _registeredVisualSync;
        private bool _registeredColdTick;
        private bool _registeredHotSwap;
        private bool _vaultInitialized;
        private bool _shutdown;
        private bool _simulationScheduled;
        private bool _vaultRepairRequested;
        private bool _payloadDirty;
        private JobHandle _simulationHandle;
        private int _gpuWriteIndex;
        private int _telemetryCursor;
        private int _telemetryDumpInFlight;
        private int _telemetryDumpCount;
        private int _activeUploadCount;
        private uint _lastFrame;
        private uint _lastRollbackHash;
        private uint _lastFaultFlags;
        private uint _telemetryDumpFrame;
        private uint _telemetryDumpReasonFlags;
        private float _lastQualityWeight = 1f;
        private float _lastShaderEdgeGlowIntensity = 1f;
        private float _lastVisualUploadMicroseconds;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ShutdownActive();
            s_active = null;
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void InstallEditorLifecycleShutdownHook()
        {
            EditorApplication.playModeStateChanged -= HandleEditorPlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandleEditorPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= ShutdownActive;
            AssemblyReloadEvents.beforeAssemblyReload += ShutdownActive;
        }

        private static void HandleEditorPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode ||
                state == PlayModeStateChange.EnteredEditMode)
            {
                ShutdownActive();
            }
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapAfterSceneLoad()
        {
            if (!Application.isPlaying || s_active != null)
                return;

            EnsureRuntime();
        }

        public static bool EnsureRuntime()
        {
            if (s_active != null)
                return true;

            // COLD ALLOC: FabricationAssemblerRuntime[1] - Vault-backed zero-GC fabrication progress service - owner: SHINOBU_142
            FabricationAssemblerRuntime runtime = new FabricationAssemblerRuntime();
            s_active = runtime;
            runtime.Initialize();
            return true;
        }

        public static bool TryBeginJob(
            uint fabricatorHash,
            uint targetPrefabHash,
            double3 targetAup,
            double3 fabricatorAup,
            float durationSeconds,
            float buildSpeedMultiplier,
            float powerDrainWatts,
            float boundsMinY,
            float boundsMaxY,
            Matrix4x4 worldToFabricator,
            bool deconstruct,
            out int slot)
        {
            slot = -1;
            if (!EnsureRuntime())
                return false;

            FabricationAssemblerRuntime runtime = s_active;
            if (runtime == null || !runtime.EnsureVaultState())
                return false;

            if (!runtime.TryOpenArray(BufferID.ShinobuFabricationJobs, in runtime._jobsHandle, MaxFabricationJobs, out NativeArray<FabricationJobDTO> jobs) ||
                !runtime.TryOpenArray(BufferID.ShinobuFabricationRuntime, in runtime._runtimeHandle, MaxFabricationJobs, out NativeArray<FabricationRuntimeDTO> states) ||
                !runtime.TryOpenArray(BufferID.ShinobuFabricationGpuPayload, in runtime._gpuPayloadHandle, MaxFabricationJobs, out NativeArray<FabricationGpuPayloadDTO> payloads))
            {
                return false;
            }

            int targetSlot = -1;
            unsafe
            {
                void* statesPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(states);
                for (int i = 0; i < states.Length; i++)
                {
                    ref FabricationRuntimeDTO state = ref UnsafeUtility.ArrayElementAsRef<FabricationRuntimeDTO>(statesPtr, i);
                    if ((state.Flags & FabricationAssemblerFlags.Active) != 0u && state.FabricatorHash == fabricatorHash)
                    {
                        targetSlot = i;
                        break;
                    }

                    if (targetSlot < 0 && (state.Flags & FabricationAssemblerFlags.Active) == 0u)
                        targetSlot = i;
                }
            }

            if (targetSlot < 0 || targetSlot >= jobs.Length || targetSlot >= payloads.Length)
                return false;

            float quality = runtime.ResolveGlobalQualityWeight();
            float safeDuration = math.max(0.001f, math.isfinite(durationSeconds) ? durationSeconds : 0.001f);
            if (runtime.TryResolveTimingDuration(targetPrefabHash, out float authoredDuration))
                safeDuration = authoredDuration;

            float safeSpeed = math.max(0.0001f, math.isfinite(buildSpeedMultiplier) ? buildSpeedMultiplier : 1f);
            float minY = math.isfinite(boundsMinY) ? boundsMinY : 0f;
            float maxY = math.max(minY + 0.001f, math.isfinite(boundsMaxY) ? boundsMaxY : minY + 1f);
            FabricationTuningDTO tuning = runtime.ResolveTuning();
            uint flags = FabricationAssemblerFlags.Active | FabricationAssemblerFlags.Dirty;
            if (deconstruct)
                flags |= FabricationAssemblerFlags.Deconstruct;

            float3 localOffset = ResolveLocalAupOffset(targetAup, fabricatorAup);
            FabricationGpuPayloadDTO payload = CreateGpuPayload(minY, maxY, deconstruct ? 1f : 0f, quality, deconstruct ? 1f : 0f, localOffset, worldToFabricator);
            unsafe
            {
                ref FabricationJobDTO job = ref UnsafeUtility.ArrayElementAsRef<FabricationJobDTO>(
                    NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(jobs),
                    targetSlot);
                ref FabricationRuntimeDTO state = ref UnsafeUtility.ArrayElementAsRef<FabricationRuntimeDTO>(
                    NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(states),
                    targetSlot);
                ref FabricationGpuPayloadDTO targetPayload = ref UnsafeUtility.ArrayElementAsRef<FabricationGpuPayloadDTO>(
                    NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payloads),
                    targetSlot);
                uint nextSequence = unchecked(state.Sequence + 1u);

                job.TargetAUP = math.all(math.isfinite(targetAup)) ? targetAup : double3.zero;
                job.Progress01 = deconstruct ? 1f : 0f;
                job.TargetPrefabHash = targetPrefabHash;

                state.FabricatorAUP = math.all(math.isfinite(fabricatorAup)) ? fabricatorAup : targetAup;
                state.DurationSeconds = safeDuration;
                state.BuildSpeedMultiplier = safeSpeed;
                state.PowerPotential01 = 1f;
                state.BoundsMinY = minY;
                state.BoundsMaxY = maxY;
                state.GlobalQualityWeight = quality;
                state.ThermalThrottle01 = 1f;
                state.FabricatorHash = fabricatorHash;
                state.Flags = flags;
                state.FrameBegan = runtime._lastFrame;
                state.FrameCompleted = 0u;
                state.Sequence = nextSequence;
                state.RollbackHash = HashSlot(targetSlot, targetPrefabHash, deconstruct ? 1f : 0f);
                state.PowerDrainWatts = math.max(0f, math.isfinite(powerDrainWatts) ? powerDrainWatts : 0f) * tuning.PowerDrawMultiplier;
                state.LastDelta01 = 0f;

                targetPayload = payload;
            }
            runtime._activeUploadCount = math.max(runtime._activeUploadCount, targetSlot + 1);
            runtime._payloadDirty = true;
            slot = targetSlot;
            return true;
        }

        public static bool TryUpdateSlot(
            int slot,
            float powerPotential01,
            float thermalThrottle01,
            bool paused,
            Matrix4x4 worldToFabricator,
            float boundsMinY,
            float boundsMaxY)
        {
            FabricationAssemblerRuntime runtime = s_active;
            if (runtime == null || !runtime.EnsureVaultState())
                return false;

            if (!runtime.TryOpenArray(BufferID.ShinobuFabricationJobs, in runtime._jobsHandle, MaxFabricationJobs, out NativeArray<FabricationJobDTO> jobs) ||
                !runtime.TryOpenArray(BufferID.ShinobuFabricationRuntime, in runtime._runtimeHandle, MaxFabricationJobs, out NativeArray<FabricationRuntimeDTO> states) ||
                !runtime.TryOpenArray(BufferID.ShinobuFabricationGpuPayload, in runtime._gpuPayloadHandle, MaxFabricationJobs, out NativeArray<FabricationGpuPayloadDTO> payloads) ||
                (uint)slot >= (uint)jobs.Length || (uint)slot >= (uint)states.Length || (uint)slot >= (uint)payloads.Length)
            {
                return false;
            }

            unsafe
            {
                ref FabricationRuntimeDTO state = ref UnsafeUtility.ArrayElementAsRef<FabricationRuntimeDTO>(
                    NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(states),
                    slot);
                if ((state.Flags & FabricationAssemblerFlags.Active) == 0u)
                    return false;

                ref FabricationJobDTO job = ref UnsafeUtility.ArrayElementAsRef<FabricationJobDTO>(
                    NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(jobs),
                    slot);
                ref FabricationGpuPayloadDTO payload = ref UnsafeUtility.ArrayElementAsRef<FabricationGpuPayloadDTO>(
                    NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payloads),
                    slot);

                float minY = math.isfinite(boundsMinY) ? boundsMinY : state.BoundsMinY;
                float maxY = math.max(minY + 0.001f, math.isfinite(boundsMaxY) ? boundsMaxY : state.BoundsMaxY);
                state.PowerPotential01 = paused ? 0f : math.saturate(math.isfinite(powerPotential01) ? powerPotential01 : 0f);
                state.ThermalThrottle01 = math.saturate(math.isfinite(thermalThrottle01) ? thermalThrottle01 : 1f);
                state.BoundsMinY = minY;
                state.BoundsMaxY = maxY;
                state.GlobalQualityWeight = runtime.ResolveGlobalQualityWeight();
                state.Flags = paused
                    ? (state.Flags | FabricationAssemblerFlags.Paused | FabricationAssemblerFlags.Dirty)
                    : ((state.Flags & ~FabricationAssemblerFlags.Paused) | FabricationAssemblerFlags.Dirty);

                float progress = math.saturate(math.isfinite(job.Progress01) ? job.Progress01 : 0f);
                float pause01 = paused ? 1f : 0f;
                float3 localOffset = ResolveLocalAupOffset(job.TargetAUP, state.FabricatorAUP);
                payload = CreateGpuPayload(minY, maxY, progress, state.GlobalQualityWeight, pause01, localOffset, worldToFabricator);
            }
            runtime._activeUploadCount = math.max(runtime._activeUploadCount, slot + 1);
            runtime._payloadDirty = true;
            return true;
        }

        public static bool TryReadSnapshot(int slot, out FabricationRuntimeSnapshot snapshot)
        {
            snapshot = default;
            FabricationAssemblerRuntime runtime = s_active;
            if (runtime == null || !runtime._vaultInitialized)
                return false;

            if (!runtime.TryOpenReadArray(BufferID.ShinobuFabricationJobs, in runtime._jobsHandle, MaxFabricationJobs, out NativeArray<FabricationJobDTO> jobs) ||
                !runtime.TryOpenReadArray(BufferID.ShinobuFabricationRuntime, in runtime._runtimeHandle, MaxFabricationJobs, out NativeArray<FabricationRuntimeDTO> states) ||
                (uint)slot >= (uint)jobs.Length || (uint)slot >= (uint)states.Length)
            {
                return false;
            }

            unsafe
            {
                ref FabricationRuntimeDTO state = ref UnsafeUtility.ArrayElementAsRef<FabricationRuntimeDTO>(
                    NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(states),
                    slot);
                if ((state.Flags & FabricationAssemblerFlags.Active) == 0u)
                    return false;

                ref FabricationJobDTO job = ref UnsafeUtility.ArrayElementAsRef<FabricationJobDTO>(
                    NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(jobs),
                    slot);
                snapshot.Progress01 = math.saturate(math.isfinite(job.Progress01) ? job.Progress01 : 0f);
                snapshot.DurationSeconds = math.max(0.001f, state.DurationSeconds);
                snapshot.TargetPrefabHash = job.TargetPrefabHash;
                snapshot.Flags = state.Flags;
                snapshot.RollbackHash = state.RollbackHash;
            }
            return true;
        }

        public static void ClearSlot(int slot)
        {
            FabricationAssemblerRuntime runtime = s_active;
            if (runtime == null || !runtime.EnsureVaultState())
                return;

            if (!runtime.TryOpenArray(BufferID.ShinobuFabricationJobs, in runtime._jobsHandle, MaxFabricationJobs, out NativeArray<FabricationJobDTO> jobs) ||
                !runtime.TryOpenArray(BufferID.ShinobuFabricationRuntime, in runtime._runtimeHandle, MaxFabricationJobs, out NativeArray<FabricationRuntimeDTO> states) ||
                !runtime.TryOpenArray(BufferID.ShinobuFabricationGpuPayload, in runtime._gpuPayloadHandle, MaxFabricationJobs, out NativeArray<FabricationGpuPayloadDTO> payloads) ||
                (uint)slot >= (uint)jobs.Length || (uint)slot >= (uint)states.Length || (uint)slot >= (uint)payloads.Length)
            {
                return;
            }

            unsafe
            {
                ref FabricationJobDTO job = ref UnsafeUtility.ArrayElementAsRef<FabricationJobDTO>(
                    NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(jobs),
                    slot);
                ref FabricationRuntimeDTO state = ref UnsafeUtility.ArrayElementAsRef<FabricationRuntimeDTO>(
                    NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(states),
                    slot);
                ref FabricationGpuPayloadDTO payload = ref UnsafeUtility.ArrayElementAsRef<FabricationGpuPayloadDTO>(
                    NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payloads),
                    slot);
                job = default;
                state = default;
                payload = default;
            }
            runtime._activeUploadCount = ResolveActiveUploadCount(states);
            runtime._payloadDirty = true;
        }

        public static bool GenerateMockFabricationJobs()
        {
            if (!EnsureRuntime())
                return false;

            FabricationAssemblerRuntime runtime = s_active;
            if (runtime == null || !runtime.EnsureVaultState())
                return false;

            if (!runtime.TryOpenArray(BufferID.ShinobuFabricationJobs, in runtime._jobsHandle, MaxFabricationJobs, out NativeArray<FabricationJobDTO> jobs) ||
                !runtime.TryOpenArray(BufferID.ShinobuFabricationRuntime, in runtime._runtimeHandle, MaxFabricationJobs, out NativeArray<FabricationRuntimeDTO> states) ||
                !runtime.TryOpenArray(BufferID.ShinobuFabricationGpuPayload, in runtime._gpuPayloadHandle, MaxFabricationJobs, out NativeArray<FabricationGpuPayloadDTO> payloads))
            {
                return false;
            }

            // COLD SYNC JOB: editor/CI mock injection must be visible immediately to the tuner/profiler.
            GenerateMockFabricationJobsJob job = new GenerateMockFabricationJobsJob
            {
                Jobs = jobs,
                Runtime = states,
                GpuPayload = payloads,
                MockCount = MockFabricationJobCount,
                Frame = runtime._lastFrame,
                GlobalQualityWeight = runtime.ResolveGlobalQualityWeight()
            };

            for (int i = 0; i < MockFabricationJobCount; i++)
                job.Execute(i);

            runtime._activeUploadCount = MockFabricationJobCount;
            runtime._payloadDirty = true;
            return true;
        }

        public static bool TryGetRollbackSnapshotHash(out uint hash)
        {
            FabricationAssemblerRuntime runtime = s_active;
            hash = runtime != null ? runtime._lastRollbackHash : 0u;
            return runtime != null;
        }

        public static bool TryGetEditorStats(out FabricationEditorStats stats)
        {
            stats = default;
            FabricationAssemblerRuntime runtime = s_active;
            if (runtime == null || !runtime._vaultInitialized)
                return false;

            if (!runtime.TryOpenReadArray(BufferID.ShinobuFabricationJobs, in runtime._jobsHandle, MaxFabricationJobs, out NativeArray<FabricationJobDTO> jobs) ||
                !runtime.TryOpenReadArray(BufferID.ShinobuFabricationRuntime, in runtime._runtimeHandle, MaxFabricationJobs, out NativeArray<FabricationRuntimeDTO> states))
            {
                return false;
            }

            float sum = 0f;
            unsafe
            {
                void* jobsPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(jobs);
                void* statesPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(states);
                int count = math.min(jobs.Length, states.Length);
                for (int i = 0; i < count; i++)
                {
                    ref FabricationRuntimeDTO state = ref UnsafeUtility.ArrayElementAsRef<FabricationRuntimeDTO>(statesPtr, i);
                    if ((state.Flags & FabricationAssemblerFlags.Active) == 0u)
                        continue;

                    ref FabricationJobDTO job = ref UnsafeUtility.ArrayElementAsRef<FabricationJobDTO>(jobsPtr, i);
                    float progress = math.saturate(math.isfinite(job.Progress01) ? job.Progress01 : 0f);
                    stats.ActiveJobs++;
                    if ((state.Flags & FabricationAssemblerFlags.Completed) != 0u)
                        stats.CompletedJobs++;
                    stats.FaultFlags |= state.Flags & (FabricationAssemblerFlags.Fault | FabricationAssemblerFlags.SignalDrop);
                    sum += progress;
                }
            }

            stats.AverageProgress01 = stats.ActiveJobs > 0 ? sum / stats.ActiveJobs : 0f;
            stats.GlobalQualityWeight = runtime._lastQualityWeight;
            stats.RollbackHash = runtime._lastRollbackHash;
            return true;
        }

        public static bool TryGetEditorJobDebug(int slot, out FabricationEditorJobDebug debug)
        {
            debug = default;
            FabricationAssemblerRuntime runtime = s_active;
            if (runtime == null || !runtime._vaultInitialized)
                return false;

            if (!runtime.TryOpenReadArray(BufferID.ShinobuFabricationJobs, in runtime._jobsHandle, MaxFabricationJobs, out NativeArray<FabricationJobDTO> jobs) ||
                !runtime.TryOpenReadArray(BufferID.ShinobuFabricationRuntime, in runtime._runtimeHandle, MaxFabricationJobs, out NativeArray<FabricationRuntimeDTO> states) ||
                (uint)slot >= (uint)jobs.Length || (uint)slot >= (uint)states.Length)
            {
                return false;
            }

            unsafe
            {
                ref FabricationRuntimeDTO state = ref UnsafeUtility.ArrayElementAsRef<FabricationRuntimeDTO>(
                    NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(states),
                    slot);
                if ((state.Flags & FabricationAssemblerFlags.Active) == 0u)
                    return false;

                ref FabricationJobDTO job = ref UnsafeUtility.ArrayElementAsRef<FabricationJobDTO>(
                    NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(jobs),
                    slot);
                debug.TargetAUP = job.TargetAUP;
                debug.Progress01 = math.saturate(math.isfinite(job.Progress01) ? job.Progress01 : 0f);
                debug.BoundsMinY = state.BoundsMinY;
                debug.BoundsMaxY = state.BoundsMaxY;
                debug.TargetPrefabHash = job.TargetPrefabHash;
                debug.FabricatorHash = state.FabricatorHash;
                debug.Flags = state.Flags;
            }
            return true;
        }

        public static bool TryGetTuning(out float baseBuildSpeedMultiplier, out float powerDrawMultiplier, out float shaderEdgeGlowIntensity)
        {
            baseBuildSpeedMultiplier = 1f;
            powerDrawMultiplier = 1f;
            shaderEdgeGlowIntensity = 1f;
            FabricationAssemblerRuntime runtime = s_active;
            if (runtime == null || !runtime._vaultInitialized)
                return false;

            FabricationTuningDTO tuning = runtime.ResolveTuning();
            baseBuildSpeedMultiplier = tuning.BaseBuildSpeedMultiplier;
            powerDrawMultiplier = tuning.PowerDrawMultiplier;
            shaderEdgeGlowIntensity = tuning.ShaderEdgeGlowIntensity;
            return true;
        }

        public static bool TrySetTuning(float baseBuildSpeedMultiplier, float powerDrawMultiplier, float shaderEdgeGlowIntensity)
        {
            FabricationAssemblerRuntime runtime = s_active;
            if (runtime == null || !runtime.EnsureVaultState())
                return false;

            IDataVault vault = runtime.ResolveVault();
            if (!runtime.TryOpenWriteArray(BufferID.ShinobuFabricationTuning, in runtime._tuningHandle, SystemID.CoreDiagnostics, 1, out NativeArray<FabricationTuningDTO> tuning))
                return false;

            try
            {
                FabricationTuningDTO next = tuning[0];
                next.BaseBuildSpeedMultiplier = math.clamp(math.isfinite(baseBuildSpeedMultiplier) ? baseBuildSpeedMultiplier : 1f, 0.05f, 16f);
                next.PowerDrawMultiplier = math.clamp(math.isfinite(powerDrawMultiplier) ? powerDrawMultiplier : 1f, 0f, 8f);
                next.ShaderEdgeGlowIntensity = math.clamp(math.isfinite(shaderEdgeGlowIntensity) ? shaderEdgeGlowIntensity : 1f, 0f, 8f);
                tuning[0] = next;
                runtime._payloadDirty = true;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in runtime._tuningHandle, SystemID.CoreDiagnostics);
            }
        }

#if UNITY_EDITOR
        public static unsafe bool TryIngestFabricationTimingsCsv(string absolutePath, out int parsedRows)
        {
            parsedRows = 0;
            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
                return false;

            if (!EnsureRuntime())
                return false;

            FabricationAssemblerRuntime runtime = s_active;
            if (runtime == null || !runtime.EnsureVaultState())
                return false;

            if (System.Threading.Interlocked.CompareExchange(ref s_csvScratchBusy, 1, 0) != 0)
                return false;

            try
            {
                byte[] csvScratch = s_csvManagedScratch;
                if (csvScratch == null || csvScratch.Length < CsvScratchByteCapacity)
                {
                    // COLD EDITOR ALLOC: CSV import byte scratch; never used by dispatcher ticks.
                    csvScratch = new byte[CsvScratchByteCapacity];
                    s_csvManagedScratch = csvScratch;
                }

                FabricationTimingDTO[] timingScratch = s_timingManagedScratch;
                if (timingScratch == null || timingScratch.Length < TimingLookupCapacity)
                {
                    // COLD EDITOR ALLOC: staged timing rows before DataVault publication.
                    timingScratch = new FabricationTimingDTO[TimingLookupCapacity];
                    s_timingManagedScratch = timingScratch;
                }

                int readLength;
                using (FileStream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    long streamLength = stream.Length;
                    int cappedLength = streamLength > CsvScratchByteCapacity ? CsvScratchByteCapacity : (int)streamLength;
                    readLength = stream.Read(csvScratch, 0, cappedLength);
                }

                Span<FabricationTimingDTO> stagedTimings = timingScratch.AsSpan(0, TimingLookupCapacity);
                bool parsed = ParseTimingCsv(csvScratch.AsSpan(0, readLength), stagedTimings, out parsedRows);
                if (!parsed)
                    return false;

                if (!runtime.TryCommitTimingLookup(stagedTimings))
                    return false;
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref s_csvScratchBusy, 0);
            }

            runtime.TryIncrementCsvTimingsVersion();
            return true;
        }
#endif

        private bool TryCommitTimingLookup(ReadOnlySpan<FabricationTimingDTO> stagedTimings)
        {
            IDataVault vault = ResolveVault();
            if (vault == null ||
                !TryOpenWriteArray(BufferID.ShinobuFabricationTimingLookup, in _timingHandle, OwnerSystemId, TimingLookupCapacity, out NativeArray<FabricationTimingDTO> timings))
            {
                return false;
            }

            try
            {
                int count = math.min(timings.Length, stagedTimings.Length);
                for (int i = 0; i < count; i++)
                    timings[i] = stagedTimings[i];
                for (int i = count; i < timings.Length; i++)
                    timings[i] = default;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _timingHandle, OwnerSystemId);
            }
        }

        private bool TryIncrementCsvTimingsVersion()
        {
            IDataVault vault = ResolveVault();
            if (vault == null ||
                !TryOpenWriteArray(BufferID.ShinobuFabricationTuning, in _tuningHandle, OwnerSystemId, 1, out NativeArray<FabricationTuningDTO> tuning))
            {
                return false;
            }

            try
            {
                FabricationTuningDTO next = tuning[0];
                next.CsvTimingsVersion++;
                tuning[0] = next;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _tuningHandle, OwnerSystemId);
            }
        }

        private FabricationAssemblerRuntime()
        {
            // COLD ALLOC: IDispatcherSystem[4] - phase adapters registered into GlobalRegistry dispatcher - owner: SHINOBU_142
            _preSimulationPhase = new PreSimulationPhaseSystem(this);
            _simulationPhase = new SimulationPhaseSystem(this);
            _postSimulationPhase = new PostSimulationPhaseSystem(this);
            _visualSyncPhase = new VisualSyncPhaseSystem(this);
        }

        private void Initialize()
        {
            _shutdown = false;
            _vault = GlobalRegistry.DataVault;
            TryRegisterHotSwapListener();
            ConfigureSignalLanes();
            PrepareRuntimeStateCold();
            RegisterDispatcherPhases();
            Application.quitting -= ShutdownActive;
            Application.quitting += ShutdownActive;
        }

        private static void ShutdownActive()
        {
            FabricationAssemblerRuntime active = s_active;
            if (active != null)
                active.Shutdown();
        }

        private void Shutdown()
        {
            if (_shutdown)
                return;

            _shutdown = true;
            Application.quitting -= ShutdownActive;
            TryUnregisterHotSwapListener();
            CompleteSimulationForLifecycle();
            UnregisterDispatcherPhases();
            ReleaseBuffer(ref _gpuPayloadBufferA);
            ReleaseBuffer(ref _gpuPayloadBufferB);
            ReleaseVaultHandles(_vault);
            _vault = null;
            _vaultInitialized = false;
            if (ReferenceEquals(s_active, this))
                s_active = null;
        }

        private void RegisterDispatcherPhases()
        {
            if (GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredPreSimulation && GlobalRegistry.TryRegisterDispatcherSystem(_preSimulationPhase))
                _registeredPreSimulation = true;
            if (!_registeredSimulation && GlobalRegistry.TryRegisterDispatcherSystem(_simulationPhase))
                _registeredSimulation = true;
            if (!_registeredPostSimulation && GlobalRegistry.TryRegisterDispatcherSystem(_postSimulationPhase))
                _registeredPostSimulation = true;
            if (!_registeredVisualSync && GlobalRegistry.TryRegisterDispatcherSystem(_visualSyncPhase))
                _registeredVisualSync = true;
            if (!_registeredColdTick && GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Environment))
                _registeredColdTick = true;
        }

        private void UnregisterDispatcherPhases()
        {
            if (_registeredColdTick)
            {
                GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Environment);
                _registeredColdTick = false;
            }

            if (_registeredPreSimulation)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_preSimulationPhase);
                _registeredPreSimulation = false;
            }

            if (_registeredSimulation)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_simulationPhase);
                _registeredSimulation = false;
            }

            if (_registeredPostSimulation)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_postSimulationPhase);
                _registeredPostSimulation = false;
            }

            if (_registeredVisualSync)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_visualSyncPhase);
                _registeredVisualSync = false;
            }
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                if (currentService != null)
                    RegisterDispatcherPhases();
                else
                    UnregisterDispatcherPhases();
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            IDataVault nextVault = currentService is IDataVault currentVault ? currentVault : null;
            if (ReferenceEquals(_vault, nextVault))
                return;

            CompleteSimulationForLifecycle();
            ReleaseVaultHandles(previousService is IDataVault previousVault ? previousVault : _vault);
            _vault = nextVault;
            _vaultInitialized = false;
            _vaultRepairRequested = true;
            if (!_shutdown)
                PrepareRuntimeStateCold();
        }

        public void ColdTick()
        {
            if (_shutdown)
                return;

            if (!_vaultRepairRequested &&
                HasVaultStateReady() &&
                (Application.isBatchMode || HasGraphicsBuffersReady()))
            {
                return;
            }

            if (_simulationScheduled)
                return;

            PrepareRuntimeStateCold();
        }

        private IDataVault ResolveVault()
        {
            return _vault;
        }

        private static bool HasFabricationHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId) where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                   handle.SystemID == (uint)OwnerSystemId &&
                   handle.Generation != 0u;
        }

        private static bool HasScalabilityHandle(in VaultGenerationHandle<ScalabilityStateDTO> handle)
        {
            return handle.BufferID == (uint)BufferID.ShinobuScalabilityState &&
                   handle.SystemID == (uint)SystemID.GraphicsScalability &&
                   handle.Generation != 0u;
        }

        private bool TryOpenArray<T>(BufferID bufferId, in VaultGenerationHandle<T> handle, int requiredLength, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (!HasFabricationHandle(in handle, bufferId) || requiredLength < 0)
                return false;

            IDataVault vault = ResolveVault();
            return vault != null &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private bool TryOpenReadArray<T>(BufferID bufferId, in VaultGenerationHandle<T> handle, int requiredLength, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (!HasFabricationHandle(in handle, bufferId) || requiredLength < 0)
                return false;

            IDataVault vault = ResolveVault();
            return vault != null &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private bool TryOpenWriteArray<T>(
            BufferID bufferId,
            in VaultGenerationHandle<T> handle,
            SystemID writerSystem,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (!HasFabricationHandle(in handle, bufferId) || writerSystem == SystemID.Unknown || requiredLength < 0)
                return false;

            IDataVault vault = ResolveVault();
            if (vault == null || !vault.TryAcquireWriteLock(in handle, writerSystem, out buffer))
                return false;

            bool ownershipTransferred = false;
            try
            {
                if (buffer.IsCreated && buffer.Length >= requiredLength)
                {
                    ownershipTransferred = true;
                    return true;
                }

                buffer = default;
                return false;
            }
            finally
            {
                if (!ownershipTransferred)
                    vault.ReleaseWriteLock(in handle, writerSystem);
            }
        }

        private void ReleaseVaultHandles(IDataVault vault)
        {
            ReleaseOwnedHandle(vault, BufferID.ShinobuFabricationJobs, ref _jobsHandle);
            ReleaseOwnedHandle(vault, BufferID.ShinobuFabricationRuntime, ref _runtimeHandle);
            ReleaseOwnedHandle(vault, BufferID.ShinobuFabricationGpuPayload, ref _gpuPayloadHandle);
            ReleaseOwnedHandle(vault, BufferID.ShinobuFabricationTelemetryRing, ref _telemetryHandle);
            ReleaseOwnedHandle(vault, BufferID.ShinobuFabricationTuning, ref _tuningHandle);
            ReleaseOwnedHandle(vault, BufferID.ShinobuFabricationTimingLookup, ref _timingHandle);
            _scalabilityHandle = default;
            _vaultInitialized = false;
        }

        private static void ReleaseOwnedHandle<T>(IDataVault vault, BufferID bufferId, ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (!HasFabricationHandle(in handle, bufferId))
            {
                handle = default;
                return;
            }

            if (vault != null)
                vault.ReleaseBuffer(in handle);
            handle = default;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private bool EnsureVaultState()
        {
            if (_vaultInitialized)
                return true;

#if UNITY_EDITOR
            FabricationLayoutValidator.ThrowIfInvalid();
#endif
            IDataVault vault = ResolveVault();
            if (vault == null)
                return false;

            _jobsHandle = vault.EnsureGenerationHandle<FabricationJobDTO>(BufferID.ShinobuFabricationJobs, MaxFabricationJobs, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            _runtimeHandle = vault.EnsureGenerationHandle<FabricationRuntimeDTO>(BufferID.ShinobuFabricationRuntime, MaxFabricationJobs, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            _gpuPayloadHandle = vault.EnsureGenerationHandle<FabricationGpuPayloadDTO>(BufferID.ShinobuFabricationGpuPayload, MaxFabricationJobs, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            _telemetryHandle = vault.EnsureGenerationHandle<FabricationTelemetryEntry>(BufferID.ShinobuFabricationTelemetryRing, TelemetryFrameCount, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            _tuningHandle = vault.EnsureGenerationHandle<FabricationTuningDTO>(BufferID.ShinobuFabricationTuning, 1, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            _timingHandle = vault.EnsureGenerationHandle<FabricationTimingDTO>(BufferID.ShinobuFabricationTimingLookup, TimingLookupCapacity, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            if (vault.TryGetGenerationHandle(BufferID.ShinobuScalabilityState, out VaultGenerationHandle<ScalabilityStateDTO> scalability))
                _scalabilityHandle = scalability;

            if (!TryOpenArray(BufferID.ShinobuFabricationJobs, in _jobsHandle, MaxFabricationJobs, out NativeArray<FabricationJobDTO> jobs) ||
                !TryOpenArray(BufferID.ShinobuFabricationRuntime, in _runtimeHandle, MaxFabricationJobs, out NativeArray<FabricationRuntimeDTO> states) ||
                !TryOpenArray(BufferID.ShinobuFabricationGpuPayload, in _gpuPayloadHandle, MaxFabricationJobs, out NativeArray<FabricationGpuPayloadDTO> payloads) ||
                !TryOpenArray(BufferID.ShinobuFabricationTelemetryRing, in _telemetryHandle, TelemetryFrameCount, out NativeArray<FabricationTelemetryEntry> telemetry) ||
                !TryOpenArray(BufferID.ShinobuFabricationTuning, in _tuningHandle, 1, out NativeArray<FabricationTuningDTO> tuning) ||
                !TryOpenArray(BufferID.ShinobuFabricationTimingLookup, in _timingHandle, TimingLookupCapacity, out NativeArray<FabricationTimingDTO> timings))
            {
                return false;
            }

            // COLD SYNC JOB: first-use Vault sanitation before dispatcher systems can read fabrication slots.
            ClearFabricationJobsJob clearJob = new ClearFabricationJobsJob
            {
                Jobs = jobs,
                Runtime = states,
                GpuPayload = payloads
            };
            for (int i = 0; i < MaxFabricationJobs; i++)
                clearJob.Execute(i);

            // COLD SYNC JOB: first-use timing lookup clear before any editor/runtime CSV ingestion can mutate the table.
            ClearFabricationTimingLookupJob timingClearJob = new ClearFabricationTimingLookupJob
            {
                Timings = timings
            };
            for (int i = 0; i < timings.Length; i++)
                timingClearJob.Execute(i);

            for (int i = 0; i < telemetry.Length; i++)
                telemetry[i] = default;

            tuning[0] = CreateDefaultTuning();

            _vaultInitialized = true;
            _activeUploadCount = 1;
            _payloadDirty = true;
            return true;
        }

        private void PrepareRuntimeStateCold()
        {
            bool vaultReady = EnsureVaultState();
            bool graphicsReady = Application.isBatchMode || EnsureGraphicsBuffers();
            if (graphicsReady && !Application.isBatchMode)
                _payloadDirty = true;
            _vaultRepairRequested = !vaultReady || !graphicsReady;
        }

        private bool HasVaultStateReady()
        {
            return _vaultInitialized &&
                TryOpenArray(BufferID.ShinobuFabricationJobs, in _jobsHandle, MaxFabricationJobs, out NativeArray<FabricationJobDTO> _) &&
                TryOpenArray(BufferID.ShinobuFabricationRuntime, in _runtimeHandle, MaxFabricationJobs, out NativeArray<FabricationRuntimeDTO> _) &&
                TryOpenArray(BufferID.ShinobuFabricationGpuPayload, in _gpuPayloadHandle, MaxFabricationJobs, out NativeArray<FabricationGpuPayloadDTO> _) &&
                TryOpenArray(BufferID.ShinobuFabricationTelemetryRing, in _telemetryHandle, TelemetryFrameCount, out NativeArray<FabricationTelemetryEntry> _) &&
                TryOpenArray(BufferID.ShinobuFabricationTuning, in _tuningHandle, 1, out NativeArray<FabricationTuningDTO> _) &&
                TryOpenArray(BufferID.ShinobuFabricationTimingLookup, in _timingHandle, TimingLookupCapacity, out NativeArray<FabricationTimingDTO> _);
        }

        private void PreSimulationTick(in DispatcherTimingDTO timing)
        {
            if (!HasVaultStateReady())
            {
                _vaultRepairRequested = true;
                return;
            }

            _lastQualityWeight = ResolveGlobalQualityWeight();
        }

        private JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn)
        {
            if (_simulationScheduled)
            {
                if (!DispatcherJobFence.TryFinalizeCompleted(ref _simulationHandle))
                    return JobHandle.CombineDependencies(dependsOn, _simulationHandle);

                _simulationScheduled = false;
            }

            if (!HasVaultStateReady())
            {
                _vaultRepairRequested = true;
                return dependsOn;
            }

            if (!TryOpenArray(BufferID.ShinobuFabricationJobs, in _jobsHandle, MaxFabricationJobs, out NativeArray<FabricationJobDTO> jobs) ||
                !TryOpenArray(BufferID.ShinobuFabricationRuntime, in _runtimeHandle, MaxFabricationJobs, out NativeArray<FabricationRuntimeDTO> states) ||
                !TryOpenArray(BufferID.ShinobuFabricationGpuPayload, in _gpuPayloadHandle, MaxFabricationJobs, out NativeArray<FabricationGpuPayloadDTO> payloads))
            {
                return dependsOn;
            }

            _lastFrame = context.Frame;
            float safeDelta = math.max(0f, timing.FrameDelta);
            FabricationTuningDTO tuning = ResolveTuning();
            JobHandle progressHandle = new AdvanceFabricationProgressJob
            {
                Jobs = jobs,
                Runtime = states,
                GpuPayload = payloads,
                DeltaSeconds = safeDelta,
                Frame = context.Frame,
                GlobalQualityWeight = _lastQualityWeight,
                GlobalBuildSpeedMultiplier = tuning.BaseBuildSpeedMultiplier
            }.Schedule(MaxFabricationJobs, 32, dependsOn);

            JobHandle signalHandle = new EmitFabricationSignalsJob
            {
                Jobs = jobs,
                Runtime = states,
                Frame = context.Frame,
                GlobalQualityWeight = _lastQualityWeight,
                FabricationCompletedSignalWriter = SignalBus<FabricationCompletedSignal>.ParallelWriter,
                FabricationCompletedSignalWriterBudget = SignalBus<FabricationCompletedSignal>.ParallelWriterBudget,
                FabricationTickSignalWriter = SignalBus<FabricationTickSignal>.ParallelWriter,
                FabricationTickSignalWriterBudget = SignalBus<FabricationTickSignal>.ParallelWriterBudget,
                DeconstructResultWriter = SignalBus<DeconstructResultSignal>.ParallelWriter,
                DeconstructResultWriterBudget = SignalBus<DeconstructResultSignal>.ParallelWriterBudget
            }.Schedule(progressHandle);

            H8Memory.RegisterActiveJob(OwnerSystemId, signalHandle);
            _simulationHandle = signalHandle;
            _simulationScheduled = true;
            return signalHandle;
        }

        private void PostSimulationTick(in DispatcherTimingDTO timing)
        {
            if (_simulationScheduled)
            {
                if (!DispatcherJobFence.TryFinalizeCompleted(ref _simulationHandle))
                    return;

                _simulationScheduled = false;
            }

            if (!HasVaultStateReady())
            {
                _vaultRepairRequested = true;
                return;
            }

            if (!TryOpenReadArray(BufferID.ShinobuFabricationJobs, in _jobsHandle, MaxFabricationJobs, out NativeArray<FabricationJobDTO> jobs) ||
                !TryOpenReadArray(BufferID.ShinobuFabricationRuntime, in _runtimeHandle, MaxFabricationJobs, out NativeArray<FabricationRuntimeDTO> states))
            {
                return;
            }

            uint active = 0u;
            uint completed = 0u;
            uint faultFlags = 0u;
            uint rollback = 2166136261u;
            uint lastHash = 0u;
            uint lastFabricator = 0u;
            float sum = 0f;
            float min = 1f;
            float max = 0f;
            float power = 0f;

            unsafe
            {
                void* jobsPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(jobs);
                void* statesPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(states);
                int count = math.min(jobs.Length, states.Length);
                for (int i = 0; i < count; i++)
                {
                    ref FabricationRuntimeDTO state = ref UnsafeUtility.ArrayElementAsRef<FabricationRuntimeDTO>(statesPtr, i);
                    if ((state.Flags & FabricationAssemblerFlags.Active) == 0u)
                        continue;

                    ref FabricationJobDTO job = ref UnsafeUtility.ArrayElementAsRef<FabricationJobDTO>(jobsPtr, i);
                    float progress = math.saturate(math.isfinite(job.Progress01) ? job.Progress01 : 0f);
                    active++;
                    completed += (state.Flags & FabricationAssemblerFlags.Completed) != 0u ? 1u : 0u;
                    faultFlags |= state.Flags & (FabricationAssemblerFlags.Fault | FabricationAssemblerFlags.SignalDrop);
                    lastHash = job.TargetPrefabHash;
                    lastFabricator = state.FabricatorHash;
                    sum += progress;
                    min = math.min(min, progress);
                    max = math.max(max, progress);
                    power += state.PowerPotential01;
                    rollback = HashCombine(rollback, state.RollbackHash);
                }
            }

            _lastRollbackHash = rollback;
            _lastFaultFlags = faultFlags;
            _activeUploadCount = ResolveActiveUploadCount(states);
            _payloadDirty |= active > 0u;
            float activeF = math.max(1f, active);
            FabricationTelemetryEntry entry = default;
            entry.Frame = _lastFrame;
            entry.ActiveJobs = active;
            entry.CompletedJobs = completed;
            entry.FaultFlags = faultFlags;
            entry.RollbackHash = rollback;
            entry.AverageProgress01 = active > 0u ? sum / activeF : 0f;
            entry.GlobalQualityWeight = _lastQualityWeight;
            entry.VisualUploadMicroseconds = _lastVisualUploadMicroseconds;
            entry.SimulationBudgetMicroseconds = active * 0.42f;
            entry.PowerPotential01 = active > 0u ? power / activeF : 0f;
            entry.MinProgress01 = active > 0u ? min : 0f;
            entry.MaxProgress01 = active > 0u ? max : 0f;
            entry.LastTargetPrefabHash = lastHash;
            entry.LastFabricatorHash = lastFabricator;
            if (!TryWriteTelemetryEntry(entry, faultFlags != 0u, out bool shouldDumpTelemetry))
                return;

            if (shouldDumpTelemetry)
                QueueTelemetryDump(faultFlags);
        }

        private bool TryWriteTelemetryEntry(FabricationTelemetryEntry entry, bool shouldDump, out bool shouldDumpTelemetry)
        {
            shouldDumpTelemetry = false;
            IDataVault vault = ResolveVault();
            if (vault == null ||
                !HasFabricationHandle(in _telemetryHandle, BufferID.ShinobuFabricationTelemetryRing) ||
                !vault.TryAcquireWriteLock(in _telemetryHandle, OwnerSystemId, out NativeArray<FabricationTelemetryEntry> telemetry))
            {
                return false;
            }

            try
            {
                if (telemetry.Length == 0)
                    return false;

                int cursor = _telemetryCursor % telemetry.Length;
                unsafe
                {
                    ref FabricationTelemetryEntry telemetryEntry = ref UnsafeUtility.ArrayElementAsRef<FabricationTelemetryEntry>(
                        NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(telemetry),
                        cursor);
                    telemetryEntry = entry;
                }

                _telemetryCursor = (_telemetryCursor + 1) % telemetry.Length;
                shouldDumpTelemetry = shouldDump;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _telemetryHandle, OwnerSystemId);
            }
        }

        private void CompleteSimulationForLifecycle()
        {
            if (!_simulationScheduled)
                return;

            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                DispatcherJobFence.TryComplete(ref _simulationHandle, forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }

            _simulationScheduled = false;
        }

        private void VisualSyncTick(in DispatcherTimingDTO timing)
        {
            if (_simulationScheduled)
                return;

            if (!HasVaultStateReady())
            {
                _vaultRepairRequested = true;
                return;
            }

            if (!HasGraphicsBuffersReady())
            {
                _vaultRepairRequested = true;
                return;
            }

            if (!_payloadDirty)
                return;

            int uploadStride = ResolveVisualUploadStride(_lastQualityWeight);
            if (uploadStride > 1 && (_lastFrame % (uint)uploadStride) != 0u)
                return;

            if (!TryOpenReadArray(BufferID.ShinobuFabricationGpuPayload, in _gpuPayloadHandle, MaxFabricationJobs, out NativeArray<FabricationGpuPayloadDTO> payloads))
                return;

            FabricationTuningDTO tuning = ResolveTuning();
            _lastShaderEdgeGlowIntensity = tuning.ShaderEdgeGlowIntensity;
            int uploadCount = math.clamp(ResolveVisualUploadCount(_activeUploadCount, _lastQualityWeight), 1, math.min(payloads.Length, MaxFabricationJobs));
            GraphicsBuffer target = _gpuWriteIndex == 0 ? _gpuPayloadBufferA : _gpuPayloadBufferB;
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            GraphicsBufferUploadUtility.UploadNativeArray(target, payloads, uploadCount);
            long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - start;
            _lastVisualUploadMicroseconds = (float)((double)elapsed * 1000000.0 / System.Diagnostics.Stopwatch.Frequency);

            Shader.SetGlobalBuffer(AssemblyPayloadsId, target);
            Shader.SetGlobalInt(AssemblyPayloadCountId, uploadCount);
            Shader.SetGlobalFloat(AssemblyQualityId, _lastQualityWeight);
            Shader.SetGlobalFloat(AssemblyEdgeBoostId, _lastShaderEdgeGlowIntensity);
            _gpuWriteIndex ^= 1;
            _payloadDirty = false;
        }

        private bool EnsureGraphicsBuffers()
        {
            if (_gpuPayloadBufferA == null)
            {
                // COLD ALLOC: GraphicsBuffer[128 FabricationGpuPayloadDTO A] - double-buffered assembly shader payload - owner: SHINOBU_142
                _gpuPayloadBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<FabricationGpuPayloadDTO>(MaxFabricationJobs);
            }

            if (_gpuPayloadBufferB == null)
            {
                // COLD ALLOC: GraphicsBuffer[128 FabricationGpuPayloadDTO B] - double-buffered assembly shader payload - owner: SHINOBU_142
                _gpuPayloadBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<FabricationGpuPayloadDTO>(MaxFabricationJobs);
            }

            return _gpuPayloadBufferA != null && _gpuPayloadBufferB != null;
        }

        private bool HasGraphicsBuffersReady()
        {
            return _gpuPayloadBufferA != null &&
                   _gpuPayloadBufferA.IsValid() &&
                   _gpuPayloadBufferB != null &&
                   _gpuPayloadBufferB.IsValid();
        }

        private static void ConfigureSignalLanes()
        {
            SignalBus<FabricationCompletedSignal>.Configure(128, maxFrameSignals: 128, lowTierFrameSignals: 32, laneHash: FabricationCompletedLaneHash);
            SignalBus<FabricationCompletedSignal>.EnsureInitialized();
            SignalBus<FabricationTickSignal>.Configure(128, maxFrameSignals: 128, lowTierFrameSignals: 24, laneHash: FabricationTickLaneHash);
            SignalBus<FabricationTickSignal>.EnsureInitialized();
            SignalCorridorRuntime.EnsureInitialized();
        }

        private void QueueTelemetryDump(uint reasonFlags)
        {
            if (_shutdown ||
                System.Threading.Interlocked.CompareExchange(ref _telemetryDumpInFlight, 1, 0) != 0)
            {
                return;
            }

            if (!TryOpenReadArray(BufferID.ShinobuFabricationTelemetryRing, in _telemetryHandle, TelemetryFrameCount, out NativeArray<FabricationTelemetryEntry> telemetry) ||
                !telemetry.IsCreated)
            {
                System.Threading.Volatile.Write(ref _telemetryDumpInFlight, 0);
                return;
            }

            int count = math.min(telemetry.Length, _telemetryDumpSnapshot.Length);
            for (int i = 0; i < count; i++)
                _telemetryDumpSnapshot[i] = telemetry[i];

            _telemetryDumpFrame = _lastFrame;
            _telemetryDumpReasonFlags = reasonFlags;
            System.Threading.Volatile.Write(ref _telemetryDumpCount, count);
            if (!System.Threading.ThreadPool.QueueUserWorkItem(TelemetryDumpWorkerCallback, this))
                System.Threading.Volatile.Write(ref _telemetryDumpInFlight, 0);
        }

        private static void RunTelemetryDumpWorker(object state)
        {
            FabricationAssemblerRuntime runtime = state as FabricationAssemblerRuntime;
            runtime?.WriteTelemetryDumpWorker();
        }

        private unsafe void WriteTelemetryDumpWorker()
        {
            NativeArray<byte> payload = default;
            try
            {
                int count = math.clamp(System.Threading.Volatile.Read(ref _telemetryDumpCount), 0, TelemetryFrameCount);
                int entryBytes = UnsafeUtility.SizeOf<FabricationTelemetryEntry>();
                const int headerBytes = 16;
                int byteCount = headerBytes + count * entryBytes;
                const string dumpPayloadLabel = "FabricationAssemblerTelemetryDumpPayload";
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(FabricationAssemblerRuntime),
                    dumpPayloadLabel,
                    allocator: Allocator.TempJob);

                byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                WriteUInt32LittleEndian(target, 0, SystemHash);
                WriteUInt32LittleEndian(target, 4, _telemetryDumpFrame);
                WriteUInt32LittleEndian(target, 8, _telemetryDumpReasonFlags);
                WriteUInt32LittleEndian(target, 12, unchecked((uint)count));

                int cursor = headerBytes;
                for (int i = 0; i < count; i++)
                {
                    FabricationTelemetryEntry entry = _telemetryDumpSnapshot[i];
                    UnsafeUtility.MemCpy(target + cursor, UnsafeUtility.AddressOf(ref entry), entryBytes);
                    cursor += entryBytes;
                }

                if (!NativeFaultDumpWriter.TryWriteAll(DumpRelativePath, payload, cursor))
                    _lastFaultFlags |= FabricationAssemblerFlags.Fault;
            }
            catch (Exception)
            {
                _lastFaultFlags |= FabricationAssemblerFlags.Fault;
            }
            finally
            {
                const string dumpPayloadLabel = "FabricationAssemblerTelemetryDumpPayload";
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(FabricationAssemblerRuntime),
                    dumpPayloadLabel,
                    Allocator.TempJob);

                System.Threading.Volatile.Write(ref _telemetryDumpInFlight, 0);
            }
        }

        private static unsafe void WriteUInt32LittleEndian(byte* target, int offset, uint value)
        {
            target[offset] = (byte)value;
            target[offset + 1] = (byte)(value >> 8);
            target[offset + 2] = (byte)(value >> 16);
            target[offset + 3] = (byte)(value >> 24);
        }

        private float ResolveGlobalQualityWeight()
        {
            IDataVault vault = ResolveVault();
            if (vault != null)
            {
                if (HasScalabilityHandle(in _scalabilityHandle) &&
                    vault.TryReadHandle(in _scalabilityHandle, out NativeArray<ScalabilityStateDTO> state) &&
                    state.IsCreated &&
                    state.Length > 0 &&
                    math.isfinite(state[0].GlobalQualityWeight))
                {
                    return math.saturate(state[0].GlobalQualityWeight);
                }
            }

            float fallback = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(fallback) ? fallback : 1f);
        }

        private FabricationTuningDTO ResolveTuning()
        {
            FabricationTuningDTO tuning = CreateDefaultTuning();
            IDataVault vault = ResolveVault();
            if (HasFabricationHandle(in _tuningHandle, BufferID.ShinobuFabricationTuning) &&
                vault != null &&
                vault.TryReadHandle(in _tuningHandle, out NativeArray<FabricationTuningDTO> buffer) &&
                buffer.IsCreated &&
                buffer.Length > 0)
            {
                tuning = buffer[0];
            }

            tuning.BaseBuildSpeedMultiplier = math.clamp(math.isfinite(tuning.BaseBuildSpeedMultiplier) ? tuning.BaseBuildSpeedMultiplier : 1f, 0.05f, 16f);
            tuning.PowerDrawMultiplier = math.clamp(math.isfinite(tuning.PowerDrawMultiplier) ? tuning.PowerDrawMultiplier : 1f, 0f, 8f);
            tuning.ShaderEdgeGlowIntensity = math.clamp(math.isfinite(tuning.ShaderEdgeGlowIntensity) ? tuning.ShaderEdgeGlowIntensity : 1f, 0f, 8f);
            return tuning;
        }

        private bool TryResolveTimingDuration(uint targetPrefabHash, out float durationSeconds)
        {
            durationSeconds = 0f;
            IDataVault vault = ResolveVault();
            if (!HasFabricationHandle(in _timingHandle, BufferID.ShinobuFabricationTimingLookup) ||
                vault == null ||
                !vault.TryReadHandle(in _timingHandle, out NativeArray<FabricationTimingDTO> timings) ||
                !timings.IsCreated ||
                timings.Length == 0 ||
                targetPrefabHash == 0u)
            {
                return false;
            }

            int start = (int)(targetPrefabHash % (uint)timings.Length);
            for (int probe = 0; probe < timings.Length; probe++)
            {
                FabricationTimingDTO entry = timings[(start + probe) % timings.Length];
                if (entry.PrefabHash == 0u)
                    return false;
                if (entry.PrefabHash != targetPrefabHash)
                    continue;

                float seconds = entry.DurationSeconds;
                if (!math.isfinite(seconds) || seconds <= 0f)
                    return false;

                durationSeconds = math.max(0.001f, seconds);
                return true;
            }

            return false;
        }

        private static FabricationTuningDTO CreateDefaultTuning()
        {
            return new FabricationTuningDTO
            {
                BaseBuildSpeedMultiplier = 1f,
                PowerDrawMultiplier = 1f,
                ShaderEdgeGlowIntensity = 1f
            };
        }

#if UNITY_EDITOR
        private static bool ParseTimingCsv(
            ReadOnlySpan<byte> bytes,
            Span<FabricationTimingDTO> timings,
            out int parsedRows)
        {
            parsedRows = 0;
            if (timings.Length <= 0 || bytes.Length <= 0)
                return false;

            for (int i = 0; i < timings.Length; i++)
                timings[i] = default;

            int index = 0;
            while (index < bytes.Length)
            {
                SkipLineBreaks(bytes, ref index);
                if (index >= bytes.Length)
                    break;

                uint hash = FnvOffset;
                bool hasName = false;
                while (index < bytes.Length)
                {
                    byte b = bytes[index++];
                    if (b == (byte)',' || b == (byte)'\n' || b == (byte)'\r')
                        break;

                    if (b == (byte)' ' || b == (byte)'\t' || b == (byte)'"')
                        continue;

                    if (b >= (byte)'A' && b <= (byte)'Z')
                        b = (byte)(b + 32);

                    hash ^= b;
                    hash *= FnvPrime;
                    hasName = true;
                }

                bool hasDuration = TryParsePositiveFloat(bytes, ref index, out float duration);
                SkipToNextLine(bytes, ref index);
                if (!hasName || !hasDuration)
                    continue;

                InsertTiming(timings, hash, duration);
                parsedRows++;
            }

            return parsedRows > 0;
        }

        private static void InsertTiming(Span<FabricationTimingDTO> timings, uint prefabHash, float durationSeconds)
        {
            if (prefabHash == 0u || !math.isfinite(durationSeconds) || durationSeconds <= 0f)
                return;

            int start = (int)(prefabHash % (uint)timings.Length);
            for (int probe = 0; probe < timings.Length; probe++)
            {
                int slot = (start + probe) % timings.Length;
                FabricationTimingDTO existing = timings[slot];
                if (existing.PrefabHash != 0u && existing.PrefabHash != prefabHash)
                    continue;

                FabricationTimingDTO next = default;
                next.PrefabHash = prefabHash;
                next.DurationSeconds = math.max(0.001f, durationSeconds);
                next.PowerDrawMultiplier = 1f;
                next.Flags = 1u;
                timings[slot] = next;
                return;
            }
        }

        private static bool TryParsePositiveFloat(ReadOnlySpan<byte> bytes, ref int index, out float value)
        {
            value = 0f;
            while (index < bytes.Length && (bytes[index] == (byte)' ' || bytes[index] == (byte)'\t' || bytes[index] == (byte)','))
                index++;

            float integer = 0f;
            bool hasDigit = false;
            while (index < bytes.Length)
            {
                byte b = bytes[index];
                if (b < (byte)'0' || b > (byte)'9')
                    break;

                integer = integer * 10f + (b - (byte)'0');
                hasDigit = true;
                index++;
            }

            float fractional = 0f;
            if (index < bytes.Length && bytes[index] == (byte)'.')
            {
                index++;
                float scale = 0.1f;
                while (index < bytes.Length)
                {
                    byte b = bytes[index];
                    if (b < (byte)'0' || b > (byte)'9')
                        break;

                    fractional += (b - (byte)'0') * scale;
                    scale *= 0.1f;
                    hasDigit = true;
                    index++;
                }
            }

            value = integer + fractional;
            return hasDigit && math.isfinite(value) && value > 0f;
        }

        private static void SkipLineBreaks(ReadOnlySpan<byte> bytes, ref int index)
        {
            while (index < bytes.Length && (bytes[index] == (byte)'\n' || bytes[index] == (byte)'\r'))
                index++;
        }

        private static void SkipToNextLine(ReadOnlySpan<byte> bytes, ref int index)
        {
            while (index < bytes.Length)
            {
                byte b = bytes[index++];
                if (b == (byte)'\n')
                    break;
            }
        }
#endif

        private static FabricationGpuPayloadDTO CreateGpuPayload(
            float minY,
            float maxY,
            float progress01,
            float quality01,
            float pause01,
            float3 localOffset,
            Matrix4x4 worldToFabricator)
        {
            return new FabricationGpuPayloadDTO
            {
                BoundsProgress = new float4(minY, math.max(minY + 0.001f, maxY), math.saturate(progress01), math.saturate(quality01)),
                LocalOffsetPause = new float4(localOffset, math.saturate(pause01)),
                WorldToFabricatorRow0 = new float4(worldToFabricator.m00, worldToFabricator.m01, worldToFabricator.m02, worldToFabricator.m03),
                WorldToFabricatorRow1 = new float4(worldToFabricator.m10, worldToFabricator.m11, worldToFabricator.m12, worldToFabricator.m13),
                WorldToFabricatorRow2 = new float4(worldToFabricator.m20, worldToFabricator.m21, worldToFabricator.m22, worldToFabricator.m23),
                WorldToFabricatorRow3 = new float4(worldToFabricator.m30, worldToFabricator.m31, worldToFabricator.m32, worldToFabricator.m33)
            };
        }

        internal static float3 ResolveLocalAupOffset(double3 targetAup, double3 fabricatorAup)
        {
            double3 delta = targetAup - fabricatorAup;
            if (!math.all(math.isfinite(delta)))
                return float3.zero;

            delta = math.clamp(delta, new double3(-100000.0), new double3(100000.0));
            return new float3((float)delta.x, (float)delta.y, (float)delta.z);
        }

        internal static AbsoluteUniversePosition ToAbsoluteUniversePosition(double3 absolutePosition)
        {
            if (!math.all(math.isfinite(absolutePosition)))
                absolutePosition = double3.zero;

            double cellSize = AbsoluteUniversePosition.CellSizeMeters;
            long gridX = (long)math.floor(absolutePosition.x / cellSize);
            long gridY = (long)math.floor(absolutePosition.y / cellSize);
            long gridZ = (long)math.floor(absolutePosition.z / cellSize);
            return new AbsoluteUniversePosition
            {
                GridX = gridX,
                GridY = gridY,
                GridZ = gridZ,
                LocalX = (float)(absolutePosition.x - (gridX * cellSize)),
                LocalY = (float)(absolutePosition.y - (gridY * cellSize)),
                LocalZ = (float)(absolutePosition.z - (gridZ * cellSize))
            };
        }

        private static int ResolveActiveUploadCount(NativeArray<FabricationRuntimeDTO> states)
        {
            int count = 1;
            if (!states.IsCreated)
                return count;

            unsafe
            {
                void* statesPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(states);
                for (int i = 0; i < states.Length; i++)
                {
                    ref FabricationRuntimeDTO state = ref UnsafeUtility.ArrayElementAsRef<FabricationRuntimeDTO>(statesPtr, i);
                    if ((state.Flags & FabricationAssemblerFlags.Active) != 0u)
                        count = i + 1;
                }
            }

            return math.clamp(count, 1, MaxFabricationJobs);
        }

        private static int ResolveVisualUploadCount(int activeCount, float quality)
        {
            float q = math.saturate(math.isfinite(quality) ? quality : 1f);
            float curved = q * q * (3f - (2f * q));
            int budget = (int)math.round(math.lerp(1f, MaxFabricationJobs, curved));
            return math.clamp(math.max(1, activeCount), 1, math.max(1, budget));
        }

        private static int ResolveVisualUploadStride(float quality)
        {
            float q = math.saturate(math.isfinite(quality) ? quality : 1f);
            float curved = q * q * (3f - (2f * q));
            float stride = math.lerp(60f, 1f, curved);
            return math.clamp((int)math.round(stride), 1, 60);
        }

        internal static uint HashSlot(int slot, uint targetHash, float progress01)
        {
            uint hash = 2166136261u;
            hash = HashCombine(hash, (uint)slot);
            hash = HashCombine(hash, targetHash);
            hash = HashCombine(hash, math.asuint(math.saturate(progress01)));
            return hash;
        }

        private static uint HashCombine(uint hash, uint value)
        {
            hash ^= value;
            hash *= 16777619u;
            return hash;
        }

        private static void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Dispose();
            buffer = null;
        }

        private sealed class PreSimulationPhaseSystem : IDispatcherSystem
        {
            private readonly FabricationAssemblerRuntime _owner;
            public PreSimulationPhaseSystem(FabricationAssemblerRuntime owner) { _owner = owner; }
            public uint GetSystemIdHash() { return 0x46315052u; }
            public DispatcherPhase GetDispatcherPhase() { return DispatcherPhase.PreSimulation; }
            public byte GetBucketId() { return byte.MaxValue; }
            public int GetDependencyCount() { return 0; }
            public uint GetDependencyHash(int dependencyIndex) { return 0u; }
            public void PreSimulationTick(in DispatcherTimingDTO timing) { _owner.PreSimulationTick(in timing); }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) { return dependsOn; }
            public void PostSimulationTick(in DispatcherTimingDTO timing) { }
            public void VisualSyncTick(in DispatcherTimingDTO timing) { }
        }

        private sealed class SimulationPhaseSystem : IDispatcherSystem
        {
            private readonly FabricationAssemblerRuntime _owner;
            public SimulationPhaseSystem(FabricationAssemblerRuntime owner) { _owner = owner; }
            public uint GetSystemIdHash() { return 0x46315349u; }
            public DispatcherPhase GetDispatcherPhase() { return DispatcherPhase.Simulation; }
            public byte GetBucketId() { return byte.MaxValue; }
            public int GetDependencyCount() { return 0; }
            public uint GetDependencyHash(int dependencyIndex) { return 0u; }
            public void PreSimulationTick(in DispatcherTimingDTO timing) { }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) { return _owner.ScheduleSimulation(in timing, in context, dependsOn); }
            public void PostSimulationTick(in DispatcherTimingDTO timing) { }
            public void VisualSyncTick(in DispatcherTimingDTO timing) { }
        }

        private sealed class PostSimulationPhaseSystem : IDispatcherSystem
        {
            private readonly FabricationAssemblerRuntime _owner;
            public PostSimulationPhaseSystem(FabricationAssemblerRuntime owner) { _owner = owner; }
            public uint GetSystemIdHash() { return 0x4631504Fu; }
            public DispatcherPhase GetDispatcherPhase() { return DispatcherPhase.PostSimulation; }
            public byte GetBucketId() { return byte.MaxValue; }
            public int GetDependencyCount() { return 0; }
            public uint GetDependencyHash(int dependencyIndex) { return 0u; }
            public void PreSimulationTick(in DispatcherTimingDTO timing) { }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) { return dependsOn; }
            public void PostSimulationTick(in DispatcherTimingDTO timing) { _owner.PostSimulationTick(in timing); }
            public void VisualSyncTick(in DispatcherTimingDTO timing) { }
        }

        private sealed class VisualSyncPhaseSystem : IDispatcherSystem
        {
            private readonly FabricationAssemblerRuntime _owner;
            public VisualSyncPhaseSystem(FabricationAssemblerRuntime owner) { _owner = owner; }
            public uint GetSystemIdHash() { return 0x46315649u; }
            public DispatcherPhase GetDispatcherPhase() { return DispatcherPhase.VisualSync; }
            public byte GetBucketId() { return byte.MaxValue; }
            public int GetDependencyCount() { return 0; }
            public uint GetDependencyHash(int dependencyIndex) { return 0u; }
            public void PreSimulationTick(in DispatcherTimingDTO timing) { }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) { return dependsOn; }
            public void PostSimulationTick(in DispatcherTimingDTO timing) { }
            public void VisualSyncTick(in DispatcherTimingDTO timing) { _owner.VisualSyncTick(in timing); }
        }
    }

#if UNITY_EDITOR
    internal static class FabricationLayoutValidator
    {
        [InitializeOnLoadMethod]
        private static void ValidateOnEditorLoad()
        {
            ThrowIfInvalid();
        }

        public static void ThrowIfInvalid()
        {
            AssertSize<FabricationJobDTO>(32);
            AssertOffset<FabricationJobDTO>(nameof(FabricationJobDTO.TargetAUP), 0);
            AssertOffset<FabricationJobDTO>(nameof(FabricationJobDTO.Progress01), 24);
            AssertOffset<FabricationJobDTO>(nameof(FabricationJobDTO.TargetPrefabHash), 28);
            AssertSize<FabricationJobSnapshotDTO>(32);
            AssertSize<FabricationRuntimeDTO>(96);
            AssertSize<FabricationGpuPayloadDTO>(96);
            AssertSize<FabricationCompletedSignal>(64);
            AssertSize<FabricationTickSignal>(64);
            AssertSize<FabricationTelemetryEntry>(64);
            AssertSize<FabricationTuningDTO>(64);
            AssertSize<FabricationTimingDTO>(16);
        }

        private static void AssertSize<T>(int expectedSize) where T : struct
        {
            int actualSize = UnsafeUtility.SizeOf<T>();
            if (actualSize != expectedSize)
                throw new InvalidOperationException(typeof(T).Name + " size mismatch. Expected " + expectedSize + " bytes, got " + actualSize + ".");
        }

        private static void AssertOffset<T>(string fieldName, int expectedOffset) where T : struct
        {
            int actualOffset = Marshal.OffsetOf<T>(fieldName).ToInt32();
            if (actualOffset != expectedOffset)
                throw new InvalidOperationException(typeof(T).Name + "." + fieldName + " offset mismatch. Expected " + expectedOffset + ", got " + actualOffset + ".");
        }
    }
#endif

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct ClearFabricationJobsJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<FabricationJobDTO> Jobs;
        [NoAlias] public NativeArray<FabricationRuntimeDTO> Runtime;
        [NoAlias] public NativeArray<FabricationGpuPayloadDTO> GpuPayload;

        public unsafe void Execute(int index)
        {
            if (!Jobs.IsCreated || !Runtime.IsCreated || !GpuPayload.IsCreated)
                return;

            ref FabricationJobDTO job = ref UnsafeUtility.ArrayElementAsRef<FabricationJobDTO>(
                NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Jobs),
                index);
            job.Progress01 = 0f;
            job.TargetPrefabHash = 0u;
            ref FabricationRuntimeDTO state = ref UnsafeUtility.ArrayElementAsRef<FabricationRuntimeDTO>(
                NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Runtime),
                index);
            ref FabricationGpuPayloadDTO payload = ref UnsafeUtility.ArrayElementAsRef<FabricationGpuPayloadDTO>(
                NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(GpuPayload),
                index);
            state = default;
            payload = default;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct ClearFabricationTimingLookupJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<FabricationTimingDTO> Timings;

        public void Execute(int index)
        {
            if (!Timings.IsCreated)
                return;

            Timings[index] = default;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct GenerateMockFabricationJobsJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<FabricationJobDTO> Jobs;
        [NoAlias] public NativeArray<FabricationRuntimeDTO> Runtime;
        [NoAlias] public NativeArray<FabricationGpuPayloadDTO> GpuPayload;
        public int MockCount;
        public uint Frame;
        public float GlobalQualityWeight;

        public unsafe void Execute(int index)
        {
            if (!Jobs.IsCreated || !Runtime.IsCreated || !GpuPayload.IsCreated || index >= MockCount)
                return;

            float lane = (float)index - (float)(MockCount - 1) * 0.5f;
            float progress = math.frac((Frame * 0.00625f) + (index * 0.0375f));
            uint targetHash = unchecked(0x46414200u + (uint)index);
            float maxY = 0.5f + (index & 3) * 0.15f;
            float quality = math.saturate(GlobalQualityWeight);
            ref FabricationJobDTO job = ref UnsafeUtility.ArrayElementAsRef<FabricationJobDTO>(
                NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Jobs),
                index);
            ref FabricationRuntimeDTO state = ref UnsafeUtility.ArrayElementAsRef<FabricationRuntimeDTO>(
                NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Runtime),
                index);
            ref FabricationGpuPayloadDTO payload = ref UnsafeUtility.ArrayElementAsRef<FabricationGpuPayloadDTO>(
                NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(GpuPayload),
                index);

            job.TargetAUP = new double3(lane * 2.0f, -80.0 + index * 0.125, 12.0 + index);
            job.Progress01 = progress;
            job.TargetPrefabHash = targetHash;

            state = default;
            state.FabricatorAUP = new double3(lane * 2.0f, -80.0, 12.0);
            state.DurationSeconds = 5f + (index & 7);
            state.BuildSpeedMultiplier = 1f;
            state.PowerPotential01 = 1f;
            state.BoundsMinY = -0.5f;
            state.BoundsMaxY = maxY;
            state.GlobalQualityWeight = quality;
            state.ThermalThrottle01 = 1f;
            state.FabricatorHash = unchecked(0x53483100u + (uint)index);
            state.Flags = FabricationAssemblerFlags.Active | FabricationAssemblerFlags.Mock | FabricationAssemblerFlags.Dirty;
            state.FrameBegan = Frame;
            state.RollbackHash = unchecked(targetHash ^ (uint)index * 2654435761u);

            payload.BoundsProgress = new float4(-0.5f, maxY, progress, quality);
            payload.LocalOffsetPause = new float4(0f, (float)(index * 0.125f), (float)index, 0f);
            payload.WorldToFabricatorRow0 = new float4(1f, 0f, 0f, 0f);
            payload.WorldToFabricatorRow1 = new float4(0f, 1f, 0f, 0f);
            payload.WorldToFabricatorRow2 = new float4(0f, 0f, 1f, 0f);
            payload.WorldToFabricatorRow3 = new float4(0f, 0f, 0f, 1f);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct AdvanceFabricationProgressJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<FabricationJobDTO> Jobs;
        [NoAlias] public NativeArray<FabricationRuntimeDTO> Runtime;
        [NoAlias] public NativeArray<FabricationGpuPayloadDTO> GpuPayload;
        public float DeltaSeconds;
        public uint Frame;
        public float GlobalQualityWeight;
        public float GlobalBuildSpeedMultiplier;

        public unsafe void Execute(int index)
        {
            if (!Jobs.IsCreated || !Runtime.IsCreated || !GpuPayload.IsCreated)
                return;

            ref FabricationRuntimeDTO state = ref UnsafeUtility.ArrayElementAsRef<FabricationRuntimeDTO>(
                NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Runtime),
                index);
            if ((state.Flags & FabricationAssemblerFlags.Active) == 0u)
                return;

            ref FabricationJobDTO job = ref UnsafeUtility.ArrayElementAsRef<FabricationJobDTO>(
                NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Jobs),
                index);
            float previousProgress = math.saturate(math.isfinite(job.Progress01) ? job.Progress01 : 0f);
            float duration = math.max(0.001f, math.isfinite(state.DurationSeconds) ? state.DurationSeconds : 0.001f);
            float globalSpeed = math.max(0.0001f, math.isfinite(GlobalBuildSpeedMultiplier) ? GlobalBuildSpeedMultiplier : 1f);
            float speed = math.max(0.0001f, math.isfinite(state.BuildSpeedMultiplier) ? state.BuildSpeedMultiplier : 1f) * globalSpeed;
            float power = math.saturate(math.isfinite(state.PowerPotential01) ? state.PowerPotential01 : 0f);
            float thermal = math.saturate(math.isfinite(state.ThermalThrottle01) ? state.ThermalThrottle01 : 1f);
            bool paused = (state.Flags & FabricationAssemblerFlags.Paused) != 0u || power <= 0.0001f;
            bool deconstruct = (state.Flags & FabricationAssemblerFlags.Deconstruct) != 0u;
            float direction = deconstruct ? -1f : 1f;
            float delta = paused ? 0f : (math.max(0f, DeltaSeconds) * speed * power * thermal) / duration;
            float progress = math.saturate(previousProgress + direction * delta);
            uint flags = state.Flags & ~FabricationAssemblerFlags.Fault;

            if (!math.isfinite(progress) ||
                !math.all(math.isfinite(job.TargetAUP)) ||
                !math.all(math.isfinite(state.FabricatorAUP)))
            {
                progress = deconstruct ? 1f : 0f;
                flags |= FabricationAssemblerFlags.Fault;
            }

            bool completed = deconstruct ? progress <= 0.0001f : progress >= 0.9999f;
            bool wasCompleted = (state.Flags & FabricationAssemblerFlags.Completed) != 0u;
            if (completed)
            {
                flags |= FabricationAssemblerFlags.Completed;
                if (!wasCompleted)
                    state.FrameCompleted = Frame;
            }
            else
            {
                flags &= ~FabricationAssemblerFlags.Completed;
            }

            job.Progress01 = progress;

            float quality = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : state.GlobalQualityWeight);
            uint rollbackHash = FabricationAssemblerRuntime.HashSlot(index, job.TargetPrefabHash, progress);
            state.RollbackHash = rollbackHash;

            state.Flags = flags | FabricationAssemblerFlags.Dirty;
            state.GlobalQualityWeight = quality;
            state.LastDelta01 = progress - previousProgress;

            ref FabricationGpuPayloadDTO payload = ref UnsafeUtility.ArrayElementAsRef<FabricationGpuPayloadDTO>(
                NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(GpuPayload),
                index);
            payload.BoundsProgress.x = state.BoundsMinY;
            payload.BoundsProgress.y = math.max(state.BoundsMinY + 0.001f, state.BoundsMaxY);
            payload.BoundsProgress.z = progress;
            payload.BoundsProgress.w = quality;
            float3 localOffset = FabricationAssemblerRuntime.ResolveLocalAupOffset(job.TargetAUP, state.FabricatorAUP);
            payload.LocalOffsetPause = new float4(localOffset, paused ? 1f : 0f);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct EmitFabricationSignalsJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<FabricationJobDTO> Jobs;
        [NoAlias] public NativeArray<FabricationRuntimeDTO> Runtime;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // ParallelWriter safety is suppressed because SignalBus owns the queue lifetime and the job only appends completed fabrication events.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Rejected main-thread event emission because it would force a second scan of all fabrication jobs. Rejected managed callbacks because they allocate and break Burst isolation.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The job is scheduled once from the dispatcher SIMULATION phase and its returned handle is registered through H8Memory before any lane drain can consume the writer output.
        [WriteOnly, NoAlias, NativeDisableContainerSafetyRestriction] public global::Hecton8.Core.MpscSignalRingBuffer<FabricationCompletedSignal>.ParallelWriter FabricationCompletedSignalWriter;
        [NativeDisableParallelForRestriction] public NativeArray<int> FabricationCompletedSignalWriterBudget;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Tick signal writes are producer-only and do not read queue state; Unity's container safety cannot see the dispatcher-owned consumer phase.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Rejected splitting tick events into a NativeArray because sparse event compaction would add another job. Rejected immediate UI writes because UI belongs to VISUAL_SYNC.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // Signal consumption is phase-separated after the combined simulation handle; this field has no second producer in EmitFabricationSignalsJob.
        [WriteOnly, NoAlias, NativeDisableContainerSafetyRestriction] public global::Hecton8.Core.MpscSignalRingBuffer<FabricationTickSignal>.ParallelWriter FabricationTickSignalWriter;
        [NativeDisableParallelForRestriction] public NativeArray<int> FabricationTickSignalWriterBudget;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Deconstruct results are emitted through the legacy GlobalSignals bridge; the safety restriction is limited to write-only enqueue.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Rejected direct owner callbacks because deconstruction crosses construction/inventory/UI domains. Rejected a managed event because it violates zero-GC hot path rules.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The returned signalHandle chains after progressHandle and is registered with H8Memory; the late-frame bridge drains only after dispatcher fence resolution.
        [WriteOnly, NoAlias, NativeDisableContainerSafetyRestriction] public global::Hecton8.Core.MpscSignalRingBuffer<DeconstructResultSignal>.ParallelWriter DeconstructResultWriter;
        [NativeDisableParallelForRestriction] public NativeArray<int> DeconstructResultWriterBudget;
        public uint Frame;
        public float GlobalQualityWeight;

        public unsafe void Execute()
        {
            if (!Jobs.IsCreated || !Runtime.IsCreated)
                return;

            int count = math.min(Jobs.Length, Runtime.Length);
            void* jobsPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Jobs);
            void* runtimePtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Runtime);
            for (int index = 0; index < count; index++)
            {
                ref FabricationRuntimeDTO state = ref UnsafeUtility.ArrayElementAsRef<FabricationRuntimeDTO>(
                    runtimePtr,
                    index);
                if ((state.Flags & FabricationAssemblerFlags.Active) == 0u)
                    continue;

                ref readonly FabricationJobDTO job = ref UnsafeUtility.ArrayElementAsRef<FabricationJobDTO>(
                    jobsPtr,
                    index);
                uint flags = state.Flags;
                float progress = math.saturate(math.isfinite(job.Progress01) ? job.Progress01 : 0f);
                float quality = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : state.GlobalQualityWeight);
                float emissionMultiplier = math.lerp(0f, 1f, quality);
                bool completed = (flags & FabricationAssemblerFlags.Completed) != 0u;
                bool observed = (flags & FabricationAssemblerFlags.CompletionObserved) != 0u;
                bool deconstruct = (flags & FabricationAssemblerFlags.Deconstruct) != 0u;

                if (completed && !observed)
                {
                    if (!EmitCompletionSignals(index, in job, in state, flags, progress, state.RollbackHash, deconstruct))
                        flags |= FabricationAssemblerFlags.SignalDrop;

                    flags |= FabricationAssemblerFlags.CompletionObserved | FabricationAssemblerFlags.Dirty;
                    state.Flags = flags;
                }

                if (!EmitTickSignal(in job, in state, flags, progress, quality, emissionMultiplier))
                {
                    flags |= FabricationAssemblerFlags.SignalDrop | FabricationAssemblerFlags.Dirty;
                    state.Flags = flags;
                }
            }
        }

        private bool EmitCompletionSignals(
            int slot,
            in FabricationJobDTO job,
            in FabricationRuntimeDTO state,
            uint flags,
            float progress01,
            uint rollbackHash,
            bool deconstruct)
        {
            if (!math.all(math.isfinite(job.TargetAUP)))
                return true;

            if (deconstruct)
            {
                return SignalBus<DeconstructResultSignal>.TryEnqueueBounded(DeconstructResultWriter, DeconstructResultWriterBudget, new DeconstructResultSignal
                {
                    TargetAup = FabricationAssemblerRuntime.ToAbsoluteUniversePosition(job.TargetAUP),
                    TargetEntityId = job.TargetPrefabHash,
                    RequesterEntityId = state.FabricatorHash,
                    RefundItemCount = 0,
                    Result = 1,
                    Reason = 0,
                    Frame = Frame
                });
            }

            return SignalBus<FabricationCompletedSignal>.TryEnqueueBounded(FabricationCompletedSignalWriter, FabricationCompletedSignalWriterBudget, new FabricationCompletedSignal
            {
                TargetAUP = job.TargetAUP,
                TargetPrefabHash = job.TargetPrefabHash,
                FabricatorHash = state.FabricatorHash,
                Frame = Frame,
                RollbackHash = rollbackHash,
                Progress01 = progress01,
                Flags = (byte)(flags & 0xFFu),
                Slot = (byte)math.clamp(slot, 0, 255),
                Sequence = state.Sequence
            });
        }

        private bool EmitTickSignal(
            in FabricationJobDTO job,
            in FabricationRuntimeDTO state,
            uint flags,
            float progress01,
            float quality01,
            float emissionMultiplier)
        {
            if (!math.all(math.isfinite(job.TargetAUP)))
                return true;

            return SignalBus<FabricationTickSignal>.TryEnqueueBounded(FabricationTickSignalWriter, FabricationTickSignalWriterBudget, new FabricationTickSignal
            {
                TargetAUP = job.TargetAUP,
                Progress01 = progress01,
                EmissionMultiplier = emissionMultiplier,
                PowerPotential01 = math.saturate(state.PowerPotential01),
                GlobalQualityWeight = quality01,
                TargetPrefabHash = job.TargetPrefabHash,
                FabricatorHash = state.FabricatorHash,
                Frame = Frame,
                Flags = flags,
                Sequence = state.Sequence
            });
        }
    }
}

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Hecton8.Core;
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
        [FieldOffset(16)] public float4 FlagsPause;
        [FieldOffset(32)] public float4 WorldToFabricatorRow0;
        [FieldOffset(48)] public float4 WorldToFabricatorRow1;
        [FieldOffset(64)] public float4 WorldToFabricatorRow2;
        [FieldOffset(80)] public float4 WorldToFabricatorRow3;
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

    public struct FabricationRuntimeSnapshot
    {
        public float Progress01;
        public float DurationSeconds;
        public uint TargetPrefabHash;
        public uint Flags;
        public uint RollbackHash;
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
    }

    internal sealed class FabricationAssemblerRuntime
    {
        public const int MaxFabricationJobs = 128;
        public const int TelemetryFrameCount = 300;
        public const int MockFabricationJobCount = 50;
        public const uint SystemHash = 0x53483142u; // SH1B

        private const SystemID OwnerSystemId = SystemID.Construction;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_142.bin";
        private static readonly int AssemblyPayloadsId = Shader.PropertyToID("_H8FabricationAssemblyPayloads");
        private static readonly int AssemblyPayloadCountId = Shader.PropertyToID("_H8FabricationAssemblyPayloadCount");
        private static readonly int AssemblyQualityId = Shader.PropertyToID("_H8FabricationAssemblyQuality");

        private static FabricationAssemblerRuntime s_active;

        private readonly PreSimulationPhaseSystem _preSimulationPhase;
        private readonly SimulationPhaseSystem _simulationPhase;
        private readonly PostSimulationPhaseSystem _postSimulationPhase;
        private readonly VisualSyncPhaseSystem _visualSyncPhase;
        private readonly string _dumpPath;

        private IDataVault _vault;
        private VaultBufferHandle<FabricationJobDTO> _jobsHandle;
        private VaultBufferHandle<FabricationRuntimeDTO> _runtimeHandle;
        private VaultBufferHandle<FabricationGpuPayloadDTO> _gpuPayloadHandle;
        private VaultBufferHandle<FabricationTelemetryEntry> _telemetryHandle;
        private VaultBufferHandle<ScalabilityStateDTO> _scalabilityHandle;

        private GraphicsBuffer _gpuPayloadBufferA;
        private GraphicsBuffer _gpuPayloadBufferB;
        private bool _registeredPreSimulation;
        private bool _registeredSimulation;
        private bool _registeredPostSimulation;
        private bool _registeredVisualSync;
        private bool _vaultInitialized;
        private bool _shutdown;
        private int _gpuWriteIndex;
        private int _telemetryCursor;
        private int _activeUploadCount;
        private uint _lastFrame;
        private uint _lastRollbackHash;
        private uint _lastFaultFlags;
        private float _lastQualityWeight = 1f;
        private float _lastVisualUploadMicroseconds;

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

            IDataVault vault = runtime.ResolveVault();
            NativeArray<FabricationJobDTO> jobs = runtime._jobsHandle.Resolve(vault);
            NativeArray<FabricationRuntimeDTO> states = runtime._runtimeHandle.Resolve(vault);
            NativeArray<FabricationGpuPayloadDTO> payloads = runtime._gpuPayloadHandle.Resolve(vault);
            if (!jobs.IsCreated || !states.IsCreated || !payloads.IsCreated)
                return false;

            int targetSlot = -1;
            for (int i = 0; i < states.Length; i++)
            {
                FabricationRuntimeDTO state = states[i];
                if ((state.Flags & FabricationAssemblerFlags.Active) != 0u && state.FabricatorHash == fabricatorHash)
                {
                    targetSlot = i;
                    break;
                }

                if (targetSlot < 0 && (state.Flags & FabricationAssemblerFlags.Active) == 0u)
                    targetSlot = i;
            }

            if (targetSlot < 0 || targetSlot >= jobs.Length || targetSlot >= payloads.Length)
                return false;

            float quality = runtime.ResolveGlobalQualityWeight();
            float safeDuration = math.max(0.001f, math.isfinite(durationSeconds) ? durationSeconds : 0.001f);
            float safeSpeed = math.max(0.0001f, math.isfinite(buildSpeedMultiplier) ? buildSpeedMultiplier : 1f);
            float minY = math.isfinite(boundsMinY) ? boundsMinY : 0f;
            float maxY = math.max(minY + 0.001f, math.isfinite(boundsMaxY) ? boundsMaxY : minY + 1f);
            uint flags = FabricationAssemblerFlags.Active | FabricationAssemblerFlags.Dirty;
            if (deconstruct)
                flags |= FabricationAssemblerFlags.Deconstruct;

            jobs[targetSlot] = new FabricationJobDTO
            {
                TargetAUP = math.all(math.isfinite(targetAup)) ? targetAup : double3.zero,
                Progress01 = deconstruct ? 1f : 0f,
                TargetPrefabHash = targetPrefabHash
            };

            states[targetSlot] = new FabricationRuntimeDTO
            {
                FabricatorAUP = math.all(math.isfinite(fabricatorAup)) ? fabricatorAup : targetAup,
                DurationSeconds = safeDuration,
                BuildSpeedMultiplier = safeSpeed,
                PowerPotential01 = 1f,
                BoundsMinY = minY,
                BoundsMaxY = maxY,
                GlobalQualityWeight = quality,
                ThermalThrottle01 = 1f,
                FabricatorHash = fabricatorHash,
                Flags = flags,
                FrameBegan = runtime._lastFrame,
                FrameCompleted = 0u,
                Sequence = unchecked(states[targetSlot].Sequence + 1u),
                RollbackHash = HashSlot(targetSlot, targetPrefabHash, deconstruct ? 1f : 0f),
                PowerDrainWatts = math.max(0f, math.isfinite(powerDrainWatts) ? powerDrainWatts : 0f),
                LastDelta01 = 0f
            };

            payloads[targetSlot] = CreateGpuPayload(minY, maxY, deconstruct ? 1f : 0f, quality, deconstruct ? 1f : 0f, worldToFabricator);
            runtime._activeUploadCount = math.max(runtime._activeUploadCount, targetSlot + 1);
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

            IDataVault vault = runtime.ResolveVault();
            NativeArray<FabricationJobDTO> jobs = runtime._jobsHandle.Resolve(vault);
            NativeArray<FabricationRuntimeDTO> states = runtime._runtimeHandle.Resolve(vault);
            NativeArray<FabricationGpuPayloadDTO> payloads = runtime._gpuPayloadHandle.Resolve(vault);
            if (!jobs.IsCreated || !states.IsCreated || !payloads.IsCreated || (uint)slot >= (uint)states.Length)
                return false;

            FabricationRuntimeDTO state = states[slot];
            if ((state.Flags & FabricationAssemblerFlags.Active) == 0u)
                return false;

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
            states[slot] = state;

            FabricationJobDTO job = jobs[slot];
            float progress = math.saturate(math.isfinite(job.Progress01) ? job.Progress01 : 0f);
            float pause01 = paused ? 1f : 0f;
            payloads[slot] = CreateGpuPayload(minY, maxY, progress, state.GlobalQualityWeight, pause01, worldToFabricator);
            runtime._activeUploadCount = math.max(runtime._activeUploadCount, slot + 1);
            return true;
        }

        public static bool TryReadSnapshot(int slot, out FabricationRuntimeSnapshot snapshot)
        {
            snapshot = default;
            FabricationAssemblerRuntime runtime = s_active;
            if (runtime == null || !runtime.EnsureVaultState())
                return false;

            IDataVault vault = runtime.ResolveVault();
            NativeArray<FabricationJobDTO> jobs = runtime._jobsHandle.Resolve(vault);
            NativeArray<FabricationRuntimeDTO> states = runtime._runtimeHandle.Resolve(vault);
            if (!jobs.IsCreated || !states.IsCreated || (uint)slot >= (uint)jobs.Length || (uint)slot >= (uint)states.Length)
                return false;

            FabricationRuntimeDTO state = states[slot];
            if ((state.Flags & FabricationAssemblerFlags.Active) == 0u)
                return false;

            FabricationJobDTO job = jobs[slot];
            snapshot.Progress01 = math.saturate(math.isfinite(job.Progress01) ? job.Progress01 : 0f);
            snapshot.DurationSeconds = math.max(0.001f, state.DurationSeconds);
            snapshot.TargetPrefabHash = job.TargetPrefabHash;
            snapshot.Flags = state.Flags;
            snapshot.RollbackHash = state.RollbackHash;
            return true;
        }

        public static void ClearSlot(int slot)
        {
            FabricationAssemblerRuntime runtime = s_active;
            if (runtime == null || !runtime.EnsureVaultState())
                return;

            IDataVault vault = runtime.ResolveVault();
            NativeArray<FabricationJobDTO> jobs = runtime._jobsHandle.Resolve(vault);
            NativeArray<FabricationRuntimeDTO> states = runtime._runtimeHandle.Resolve(vault);
            NativeArray<FabricationGpuPayloadDTO> payloads = runtime._gpuPayloadHandle.Resolve(vault);
            if (!jobs.IsCreated || !states.IsCreated || !payloads.IsCreated || (uint)slot >= (uint)states.Length)
                return;

            jobs[slot] = default;
            states[slot] = default;
            payloads[slot] = default;
            runtime._activeUploadCount = ResolveActiveUploadCount(states);
        }

        public static bool GenerateMockFabricationJobs()
        {
            if (!EnsureRuntime())
                return false;

            FabricationAssemblerRuntime runtime = s_active;
            if (runtime == null || !runtime.EnsureVaultState())
                return false;

            IDataVault vault = runtime.ResolveVault();
            NativeArray<FabricationJobDTO> jobs = runtime._jobsHandle.Resolve(vault);
            NativeArray<FabricationRuntimeDTO> states = runtime._runtimeHandle.Resolve(vault);
            NativeArray<FabricationGpuPayloadDTO> payloads = runtime._gpuPayloadHandle.Resolve(vault);
            if (!jobs.IsCreated || !states.IsCreated || !payloads.IsCreated)
                return false;

            JobHandle handle = new GenerateMockFabricationJobsJob
            {
                Jobs = jobs,
                Runtime = states,
                GpuPayload = payloads,
                MockCount = MockFabricationJobCount,
                Frame = runtime._lastFrame,
                GlobalQualityWeight = runtime.ResolveGlobalQualityWeight()
            }.Schedule(MockFabricationJobCount, 16);

            H8Memory.RegisterActiveJob(OwnerSystemId, handle);
            handle.Complete();
            runtime._activeUploadCount = MockFabricationJobCount;
            return true;
        }

        public static bool TryGetRollbackSnapshotHash(out uint hash)
        {
            FabricationAssemblerRuntime runtime = s_active;
            hash = runtime != null ? runtime._lastRollbackHash : 0u;
            return runtime != null;
        }

        private FabricationAssemblerRuntime()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            _dumpPath = Path.GetFullPath(Path.Combine(projectRoot, DumpRelativePath));

            // COLD ALLOC: IDispatcherSystem[4] - phase adapters registered into GlobalRegistry dispatcher - owner: SHINOBU_142
            _preSimulationPhase = new PreSimulationPhaseSystem(this);
            _simulationPhase = new SimulationPhaseSystem(this);
            _postSimulationPhase = new PostSimulationPhaseSystem(this);
            _visualSyncPhase = new VisualSyncPhaseSystem(this);
        }

        private void Initialize()
        {
            _shutdown = false;
            _vault = ResolveVault();
            EnsureGraphicsBuffers();
            EnsureVaultState();
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
            UnregisterDispatcherPhases();
            ReleaseBuffer(ref _gpuPayloadBufferA);
            ReleaseBuffer(ref _gpuPayloadBufferB);
            _vault = null;
            _vaultInitialized = false;
            if (ReferenceEquals(s_active, this))
                s_active = null;
        }

        private void RegisterDispatcherPhases()
        {
            if (!_registeredPreSimulation && GlobalRegistry.TryRegisterDispatcherSystem(_preSimulationPhase))
                _registeredPreSimulation = true;
            if (!_registeredSimulation && GlobalRegistry.TryRegisterDispatcherSystem(_simulationPhase))
                _registeredSimulation = true;
            if (!_registeredPostSimulation && GlobalRegistry.TryRegisterDispatcherSystem(_postSimulationPhase))
                _registeredPostSimulation = true;
            if (!_registeredVisualSync && GlobalRegistry.TryRegisterDispatcherSystem(_visualSyncPhase))
                _registeredVisualSync = true;
        }

        private void UnregisterDispatcherPhases()
        {
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

        private IDataVault ResolveVault()
        {
            IDataVault vault = _vault;
            if (vault != null)
                return vault;

            vault = GlobalRegistry.DataVault;
            if (vault == null && GlobalDataVault.TryGetLatestCreated(out GlobalDataVault latest))
                vault = latest;

            _vault = vault;
            return vault;
        }

        private bool EnsureVaultState()
        {
            if (_vaultInitialized)
                return true;

            FabricationLayoutValidator.ThrowIfInvalid();
            IDataVault vault = ResolveVault();
            if (vault == null)
                return false;

            _jobsHandle = vault.GetBufferHandle<FabricationJobDTO>(BufferID.ShinobuFabricationJobs, MaxFabricationJobs, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            _runtimeHandle = vault.GetBufferHandle<FabricationRuntimeDTO>(BufferID.ShinobuFabricationRuntime, MaxFabricationJobs, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            _gpuPayloadHandle = vault.GetBufferHandle<FabricationGpuPayloadDTO>(BufferID.ShinobuFabricationGpuPayload, MaxFabricationJobs, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            _telemetryHandle = vault.GetBufferHandle<FabricationTelemetryEntry>(BufferID.ShinobuFabricationTelemetryRing, TelemetryFrameCount, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            if (vault.TryGetBufferHandle(BufferID.ShinobuScalabilityState, out VaultBufferHandle<ScalabilityStateDTO> scalability))
                _scalabilityHandle = scalability;

            NativeArray<FabricationJobDTO> jobs = _jobsHandle.Resolve(vault);
            NativeArray<FabricationRuntimeDTO> states = _runtimeHandle.Resolve(vault);
            NativeArray<FabricationGpuPayloadDTO> payloads = _gpuPayloadHandle.Resolve(vault);
            NativeArray<FabricationTelemetryEntry> telemetry = _telemetryHandle.Resolve(vault);
            if (!jobs.IsCreated || !states.IsCreated || !payloads.IsCreated || !telemetry.IsCreated)
                return false;

            JobHandle clearHandle = new ClearFabricationJobsJob
            {
                Jobs = jobs,
                Runtime = states,
                GpuPayload = payloads
            }.Schedule(MaxFabricationJobs, 32);
            H8Memory.RegisterActiveJob(OwnerSystemId, clearHandle);
            clearHandle.Complete();

            for (int i = 0; i < telemetry.Length; i++)
                telemetry[i] = default;

            _vaultInitialized = true;
            _activeUploadCount = 1;
            return true;
        }

        private void PreSimulationTick(in DispatcherTimingDTO timing)
        {
            if (!EnsureVaultState())
                return;

            _lastQualityWeight = ResolveGlobalQualityWeight();
        }

        private JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn)
        {
            if (!EnsureVaultState())
                return dependsOn;

            IDataVault vault = ResolveVault();
            NativeArray<FabricationJobDTO> jobs = _jobsHandle.Resolve(vault);
            NativeArray<FabricationRuntimeDTO> states = _runtimeHandle.Resolve(vault);
            NativeArray<FabricationGpuPayloadDTO> payloads = _gpuPayloadHandle.Resolve(vault);
            if (!jobs.IsCreated || !states.IsCreated || !payloads.IsCreated)
                return dependsOn;

            _lastFrame = context.Frame;
            float safeDelta = math.max(0f, timing.FrameDelta);
            JobHandle handle = new AdvanceFabricationProgressJob
            {
                Jobs = jobs,
                Runtime = states,
                GpuPayload = payloads,
                DeltaSeconds = safeDelta,
                Frame = context.Frame,
                GlobalQualityWeight = _lastQualityWeight
            }.Schedule(MaxFabricationJobs, 32, dependsOn);

            H8Memory.RegisterActiveJob(OwnerSystemId, handle);
            return handle;
        }

        private void PostSimulationTick(in DispatcherTimingDTO timing)
        {
            if (!EnsureVaultState())
                return;

            IDataVault vault = ResolveVault();
            NativeArray<FabricationJobDTO> jobs = _jobsHandle.Resolve(vault);
            NativeArray<FabricationRuntimeDTO> states = _runtimeHandle.Resolve(vault);
            NativeArray<FabricationTelemetryEntry> telemetry = _telemetryHandle.Resolve(vault);
            if (!jobs.IsCreated || !states.IsCreated || !telemetry.IsCreated || telemetry.Length == 0)
                return;

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

            for (int i = 0; i < states.Length; i++)
            {
                FabricationRuntimeDTO state = states[i];
                if ((state.Flags & FabricationAssemblerFlags.Active) == 0u)
                    continue;

                FabricationJobDTO job = jobs[i];
                float progress = math.saturate(math.isfinite(job.Progress01) ? job.Progress01 : 0f);
                active++;
                completed += (state.Flags & FabricationAssemblerFlags.Completed) != 0u ? 1u : 0u;
                faultFlags |= state.Flags & FabricationAssemblerFlags.Fault;
                lastHash = job.TargetPrefabHash;
                lastFabricator = state.FabricatorHash;
                sum += progress;
                min = math.min(min, progress);
                max = math.max(max, progress);
                power += state.PowerPotential01;
                rollback = HashCombine(rollback, state.RollbackHash);
            }

            _lastRollbackHash = rollback;
            _lastFaultFlags = faultFlags;
            _activeUploadCount = ResolveActiveUploadCount(states);
            float activeF = math.max(1f, active);
            telemetry[_telemetryCursor % telemetry.Length] = new FabricationTelemetryEntry
            {
                Frame = _lastFrame,
                ActiveJobs = active,
                CompletedJobs = completed,
                FaultFlags = faultFlags,
                RollbackHash = rollback,
                AverageProgress01 = active > 0u ? sum / activeF : 0f,
                GlobalQualityWeight = _lastQualityWeight,
                VisualUploadMicroseconds = _lastVisualUploadMicroseconds,
                SimulationBudgetMicroseconds = active * 0.42f,
                PowerPotential01 = active > 0u ? power / activeF : 0f,
                MinProgress01 = active > 0u ? min : 0f,
                MaxProgress01 = active > 0u ? max : 0f,
                LastTargetPrefabHash = lastHash,
                LastFabricatorHash = lastFabricator
            };
            _telemetryCursor = (_telemetryCursor + 1) % telemetry.Length;

            if (faultFlags != 0u)
                DumpTelemetry(vault, telemetry, faultFlags);
        }

        private void VisualSyncTick(in DispatcherTimingDTO timing)
        {
            if (!EnsureVaultState())
                return;

            if (!EnsureGraphicsBuffers())
                return;

            IDataVault vault = ResolveVault();
            NativeArray<FabricationGpuPayloadDTO> payloads = _gpuPayloadHandle.Resolve(vault);
            if (!payloads.IsCreated)
                return;

            int uploadCount = math.clamp(ResolveVisualUploadCount(_activeUploadCount, _lastQualityWeight), 1, math.min(payloads.Length, MaxFabricationJobs));
            GraphicsBuffer target = _gpuWriteIndex == 0 ? _gpuPayloadBufferA : _gpuPayloadBufferB;
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            GraphicsBufferUploadUtility.UploadNativeArray(target, payloads, uploadCount);
            long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - start;
            _lastVisualUploadMicroseconds = (float)((double)elapsed * 1000000.0 / System.Diagnostics.Stopwatch.Frequency);

            Shader.SetGlobalBuffer(AssemblyPayloadsId, target);
            Shader.SetGlobalInt(AssemblyPayloadCountId, uploadCount);
            Shader.SetGlobalFloat(AssemblyQualityId, _lastQualityWeight);
            _gpuWriteIndex ^= 1;
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

        private void DumpTelemetry(IDataVault vault, NativeArray<FabricationTelemetryEntry> telemetry, uint reasonFlags)
        {
            try
            {
                string directory = Path.GetDirectoryName(_dumpPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                using BinaryWriter writer = new BinaryWriter(stream);
                writer.Write(0x53483142u);
                writer.Write(_lastFrame);
                writer.Write(reasonFlags);
                writer.Write(telemetry.Length);
                for (int i = 0; i < telemetry.Length; i++)
                {
                    FabricationTelemetryEntry entry = telemetry[i];
                    writer.Write(entry.Frame);
                    writer.Write(entry.ActiveJobs);
                    writer.Write(entry.CompletedJobs);
                    writer.Write(entry.FaultFlags);
                    writer.Write(entry.RollbackHash);
                    writer.Write(entry.AverageProgress01);
                    writer.Write(entry.GlobalQualityWeight);
                    writer.Write(entry.VisualUploadMicroseconds);
                    writer.Write(entry.SimulationBudgetMicroseconds);
                    writer.Write(entry.PowerPotential01);
                    writer.Write(entry.MinProgress01);
                    writer.Write(entry.MaxProgress01);
                    writer.Write(entry.LastTargetPrefabHash);
                    writer.Write(entry.LastFabricatorHash);
                    writer.Write(entry.Reserved0);
                }
            }
            catch (Exception)
            {
                _lastFaultFlags |= FabricationAssemblerFlags.Fault;
            }
        }

        private float ResolveGlobalQualityWeight()
        {
            IDataVault vault = ResolveVault();
            if (vault != null)
            {
                if (!_scalabilityHandle.IsCreated &&
                    vault.TryGetBufferHandle(BufferID.ShinobuScalabilityState, out VaultBufferHandle<ScalabilityStateDTO> scalability))
                {
                    _scalabilityHandle = scalability;
                }

                NativeArray<ScalabilityStateDTO> state = _scalabilityHandle.IsCreated ? _scalabilityHandle.Resolve(vault) : default;
                if (state.IsCreated && state.Length > 0 && math.isfinite(state[0].GlobalQualityWeight))
                    return math.saturate(state[0].GlobalQualityWeight);
            }

            float fallback = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(fallback) ? fallback : 1f);
        }

        private static FabricationGpuPayloadDTO CreateGpuPayload(
            float minY,
            float maxY,
            float progress01,
            float quality01,
            float pause01,
            Matrix4x4 worldToFabricator)
        {
            return new FabricationGpuPayloadDTO
            {
                BoundsProgress = new float4(minY, math.max(minY + 0.001f, maxY), math.saturate(progress01), math.saturate(quality01)),
                FlagsPause = new float4(pause01, 0f, 0f, 0f),
                WorldToFabricatorRow0 = new float4(worldToFabricator.m00, worldToFabricator.m01, worldToFabricator.m02, worldToFabricator.m03),
                WorldToFabricatorRow1 = new float4(worldToFabricator.m10, worldToFabricator.m11, worldToFabricator.m12, worldToFabricator.m13),
                WorldToFabricatorRow2 = new float4(worldToFabricator.m20, worldToFabricator.m21, worldToFabricator.m22, worldToFabricator.m23),
                WorldToFabricatorRow3 = new float4(worldToFabricator.m30, worldToFabricator.m31, worldToFabricator.m32, worldToFabricator.m33)
            };
        }

        private static int ResolveActiveUploadCount(NativeArray<FabricationRuntimeDTO> states)
        {
            int count = 1;
            if (!states.IsCreated)
                return count;

            for (int i = 0; i < states.Length; i++)
            {
                if ((states[i].Flags & FabricationAssemblerFlags.Active) != 0u)
                    count = i + 1;
            }

            return math.clamp(count, 1, MaxFabricationJobs);
        }

        private static int ResolveVisualUploadCount(int activeCount, float quality)
        {
            float q = math.saturate(math.isfinite(quality) ? quality : 1f);
            int budget = (int)math.round(math.lerp(16f, MaxFabricationJobs, q));
            return math.clamp(math.max(1, activeCount), 1, math.max(1, budget));
        }

        private static uint HashSlot(int slot, uint targetHash, float progress01)
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

    internal static class FabricationLayoutValidator
    {
#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void ValidateOnEditorLoad()
        {
            ThrowIfInvalid();
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ValidateOnSubsystemRegistration()
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
            AssertSize<FabricationTelemetryEntry>(64);
        }

        private static void AssertSize<T>(int expectedSize) where T : struct
        {
            int actualSize = UnsafeUtility.SizeOf<T>();
            if (actualSize != expectedSize)
                throw new InvalidOperationException(typeof(T).Name + " size mismatch. Expected " + expectedSize + " bytes, got " + actualSize + ".");
        }

        private static void AssertOffset<T>(string fieldName, int expectedOffset) where T : struct
        {
            FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            int actualOffset = field != null ? UnsafeUtility.GetFieldOffset(field) : -1;
            if (actualOffset != expectedOffset)
                throw new InvalidOperationException(typeof(T).Name + "." + fieldName + " offset mismatch. Expected " + expectedOffset + ", got " + actualOffset + ".");
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct ClearFabricationJobsJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<FabricationJobDTO> Jobs;
        [NoAlias] public NativeArray<FabricationRuntimeDTO> Runtime;
        [NoAlias] public NativeArray<FabricationGpuPayloadDTO> GpuPayload;

        public void Execute(int index)
        {
            if (!Jobs.IsCreated || !Runtime.IsCreated || !GpuPayload.IsCreated)
                return;

            Jobs[index] = default;
            Runtime[index] = default;
            GpuPayload[index] = default;
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

        public void Execute(int index)
        {
            if (!Jobs.IsCreated || !Runtime.IsCreated || !GpuPayload.IsCreated || index >= MockCount)
                return;

            float lane = (float)index - (float)(MockCount - 1) * 0.5f;
            float progress = math.frac((Frame * 0.00625f) + (index * 0.0375f));
            uint targetHash = unchecked(0x46414200u + (uint)index);
            Jobs[index] = new FabricationJobDTO
            {
                TargetAUP = new double3(lane * 2.0f, -80.0 + index * 0.125, 12.0 + index),
                Progress01 = progress,
                TargetPrefabHash = targetHash
            };

            Runtime[index] = new FabricationRuntimeDTO
            {
                FabricatorAUP = new double3(lane * 2.0f, -80.0, 12.0),
                DurationSeconds = 5f + (index & 7),
                BuildSpeedMultiplier = 1f,
                PowerPotential01 = 1f,
                BoundsMinY = -0.5f,
                BoundsMaxY = 0.5f + (index & 3) * 0.15f,
                GlobalQualityWeight = math.saturate(GlobalQualityWeight),
                ThermalThrottle01 = 1f,
                FabricatorHash = unchecked(0x53483100u + (uint)index),
                Flags = FabricationAssemblerFlags.Active | FabricationAssemblerFlags.Mock | FabricationAssemblerFlags.Dirty,
                FrameBegan = Frame,
                RollbackHash = unchecked(targetHash ^ (uint)index * 2654435761u)
            };

            GpuPayload[index] = new FabricationGpuPayloadDTO
            {
                BoundsProgress = new float4(-0.5f, 0.5f + (index & 3) * 0.15f, progress, math.saturate(GlobalQualityWeight)),
                FlagsPause = new float4(0f, 0f, 0f, 0f),
                WorldToFabricatorRow0 = new float4(1f, 0f, 0f, 0f),
                WorldToFabricatorRow1 = new float4(0f, 1f, 0f, 0f),
                WorldToFabricatorRow2 = new float4(0f, 0f, 1f, 0f),
                WorldToFabricatorRow3 = new float4(0f, 0f, 0f, 1f)
            };
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

        public void Execute(int index)
        {
            if (!Jobs.IsCreated || !Runtime.IsCreated || !GpuPayload.IsCreated)
                return;

            FabricationRuntimeDTO state = Runtime[index];
            if ((state.Flags & FabricationAssemblerFlags.Active) == 0u)
                return;

            FabricationJobDTO job = Jobs[index];
            float previousProgress = math.saturate(math.isfinite(job.Progress01) ? job.Progress01 : 0f);
            float duration = math.max(0.001f, math.isfinite(state.DurationSeconds) ? state.DurationSeconds : 0.001f);
            float speed = math.max(0.0001f, math.isfinite(state.BuildSpeedMultiplier) ? state.BuildSpeedMultiplier : 1f);
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
            if (completed)
            {
                flags |= FabricationAssemblerFlags.Completed;
                state.FrameCompleted = Frame;
            }
            else
            {
                flags &= ~FabricationAssemblerFlags.Completed;
            }

            job.Progress01 = progress;
            Jobs[index] = job;

            float quality = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : state.GlobalQualityWeight);
            state.Flags = flags | FabricationAssemblerFlags.Dirty;
            state.GlobalQualityWeight = quality;
            state.LastDelta01 = progress - previousProgress;
            state.RollbackHash = HashSlot(index, job.TargetPrefabHash, progress);
            Runtime[index] = state;

            FabricationGpuPayloadDTO payload = GpuPayload[index];
            payload.BoundsProgress.x = state.BoundsMinY;
            payload.BoundsProgress.y = math.max(state.BoundsMinY + 0.001f, state.BoundsMaxY);
            payload.BoundsProgress.z = progress;
            payload.BoundsProgress.w = quality;
            payload.FlagsPause.x = paused ? 1f : 0f;
            payload.FlagsPause.y = deconstruct ? 1f : 0f;
            payload.FlagsPause.z = completed ? 1f : 0f;
            payload.FlagsPause.w = (flags & FabricationAssemblerFlags.Fault) != 0u ? 1f : 0f;
            GpuPayload[index] = payload;
        }

        private static uint HashSlot(int slot, uint targetHash, float progress01)
        {
            uint hash = 2166136261u;
            hash ^= (uint)slot;
            hash *= 16777619u;
            hash ^= targetHash;
            hash *= 16777619u;
            hash ^= math.asuint(math.saturate(progress01));
            hash *= 16777619u;
            return hash;
        }
    }
}

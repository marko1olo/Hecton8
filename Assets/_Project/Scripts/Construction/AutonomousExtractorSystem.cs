using System.Runtime.InteropServices;
using Hecton8.Building;
using Hecton8.Core;
using Hecton8.Items;
using Hecton8.Power;
using Hecton8.Scavenging;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Construction
{
    /// <summary>
    /// Runtime owner for autonomous deep-sea extractors.
    /// Maintains extractor SOA state, schedules the Burst extraction pass on SlowTick,
    /// and commits the results during the end-of-frame swap window.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4041)]
    public sealed class AutonomousExtractorSystem : MonoBehaviour, ISlowTickable, IGlobalRegistryHotSwapListener
    {
        private const int MaxModuleCapacity = 256;
        private const float SlowTickDeltaSeconds = 0.5f;
        private const uint ExtractorCapacityGrowthWarningHash = 0xA8754B21u;
        private const uint ExtractorCapacityGrowthContextHash = 0xE71C92D4u;
        private const uint DuplicateRuntimeWarningHash = 0xB44D12E9u;
        private const uint DuplicateRuntimeContextHash = 0xAD50966Cu;

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct ExtractorJobInput
        {
            [FieldOffset(0)] public float CycleTimerSeconds;
            [FieldOffset(4)] public float CycleSeconds;
            [FieldOffset(8)] public int BufferedUnitCount;
            [FieldOffset(12)] public int BufferedUnitCapacity;
            [FieldOffset(16)] public int ItemHashId;
            [FieldOffset(20)] public byte IsActive;
            [FieldOffset(24)] private ulong _pad0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct ExtractorJobResult
        {
            [FieldOffset(0)] public float NextCycleTimerSeconds;
            [FieldOffset(4)] public int NextBufferedUnitCount;
            [FieldOffset(8)] public int BufferedItemHashId;
            [FieldOffset(12)] public int CompletedCycleDelta;
            [FieldOffset(16)] public byte IsOperating;
            [FieldOffset(24)] private ulong _pad0;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct AdvanceExtractionJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<ExtractorJobInput> Inputs;
            [NoAlias] public NativeArray<ExtractorJobResult> Results;
            public float SlowTickDeltaSeconds;

            public void Execute(int index)
            {
                ExtractorJobInput input = Inputs[index];
                ExtractorJobResult result = new ExtractorJobResult
                {
                    NextCycleTimerSeconds = math.max(0f, input.CycleTimerSeconds),
                    NextBufferedUnitCount = math.max(0, input.BufferedUnitCount),
                    BufferedItemHashId = input.ItemHashId,
                    CompletedCycleDelta = 0,
                    IsOperating = 0
                };

                bool canOperate = input.IsActive != 0 &&
                                  input.ItemHashId != 0 &&
                                  input.BufferedUnitCapacity > 0 &&
                                  input.BufferedUnitCount < input.BufferedUnitCapacity &&
                                  input.CycleSeconds > 0f;
                if (!canOperate)
                {
                    Results[index] = result;
                    return;
                }

                result.IsOperating = 1;
                float accumulatedTime = input.CycleTimerSeconds + math.max(0f, SlowTickDeltaSeconds);
                float cycleSeconds = math.max(0.001f, input.CycleSeconds);
                int completedCycles = (int)math.floor(accumulatedTime / cycleSeconds);
                int availableCapacity = math.max(0, input.BufferedUnitCapacity - input.BufferedUnitCount);
                int producedUnits = math.min(math.max(0, completedCycles), availableCapacity);

                result.NextBufferedUnitCount = input.BufferedUnitCount + producedUnits;
                result.CompletedCycleDelta = producedUnits;
                result.NextCycleTimerSeconds = accumulatedTime - (producedUnits * cycleSeconds);

                if (completedCycles > producedUnits)
                    result.NextCycleTimerSeconds = cycleSeconds;

                if (result.NextBufferedUnitCount >= input.BufferedUnitCapacity)
                    result.IsOperating = 0;

                Results[index] = result;
            }
        }

        private struct ExtractorNativeState
        {
            public NativeArray<ExtractorJobInput> JobInputs;
            public NativeArray<ExtractorJobResult> JobResults;
            public NativeArray<float> CycleTimers;
            public NativeArray<int> BufferedItemHashIds;
            public NativeArray<int> BufferedUnitCounts;
            public NativeArray<int> CompletedCycleCounts;

            public bool IsReady(int capacity)
            {
                return JobInputs.IsCreated &&
                       JobResults.IsCreated &&
                       CycleTimers.IsCreated &&
                       BufferedItemHashIds.IsCreated &&
                       BufferedUnitCounts.IsCreated &&
                       CompletedCycleCounts.IsCreated &&
                       JobInputs.Length == capacity &&
                       JobResults.Length == capacity &&
                       CycleTimers.Length == capacity &&
                       BufferedItemHashIds.Length == capacity &&
                       BufferedUnitCounts.Length == capacity &&
                       CompletedCycleCounts.Length == capacity;
            }

            public void Ensure(int capacity)
            {
                if (IsReady(capacity))
                    return;

                Dispose();
                try
                {
                    JobInputs = new NativeArray<ExtractorJobInput>(capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                    NativeMemorySentinel.RegisterNativeArray(JobInputs, nameof(AutonomousExtractorSystem), nameof(JobInputs), NativeAllocationLifetime.Scene);
                    JobResults = new NativeArray<ExtractorJobResult>(capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                    NativeMemorySentinel.RegisterNativeArray(JobResults, nameof(AutonomousExtractorSystem), nameof(JobResults), NativeAllocationLifetime.Scene);
                    CycleTimers = new NativeArray<float>(capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                    NativeMemorySentinel.RegisterNativeArray(CycleTimers, nameof(AutonomousExtractorSystem), nameof(CycleTimers), NativeAllocationLifetime.Scene);
                    BufferedItemHashIds = new NativeArray<int>(capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                    NativeMemorySentinel.RegisterNativeArray(BufferedItemHashIds, nameof(AutonomousExtractorSystem), nameof(BufferedItemHashIds), NativeAllocationLifetime.Scene);
                    BufferedUnitCounts = new NativeArray<int>(capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                    NativeMemorySentinel.RegisterNativeArray(BufferedUnitCounts, nameof(AutonomousExtractorSystem), nameof(BufferedUnitCounts), NativeAllocationLifetime.Scene);
                    CompletedCycleCounts = new NativeArray<int>(capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                    NativeMemorySentinel.RegisterNativeArray(CompletedCycleCounts, nameof(AutonomousExtractorSystem), nameof(CompletedCycleCounts), NativeAllocationLifetime.Scene);
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            public void Dispose()
            {
                DisposeNativeArray(ref JobInputs);
                DisposeNativeArray(ref JobResults);
                DisposeNativeArray(ref CycleTimers);
                DisposeNativeArray(ref BufferedItemHashIds);
                DisposeNativeArray(ref BufferedUnitCounts);
                DisposeNativeArray(ref CompletedCycleCounts);
            }

            private static void DisposeNativeArray<T>(ref NativeArray<T> array) where T : struct
            {
                if (!array.IsCreated)
                    return;

                NativeMemorySentinel.UnregisterNativeArray(array);
                array.Dispose();
                array = default;
            }
        }

        // COLD ALLOC: AutonomousExtractorModule[MaxModuleCapacity] - fixed runtime extractor registry; no managed growth - owner: AutonomousExtractorSystem
        private readonly AutonomousExtractorModule[] _modules = new AutonomousExtractorModule[MaxModuleCapacity];
        private ExtractorNativeState _nativeState;
        private JobHandle _scheduledJobHandle;
        private bool _scheduledJobActive;
        private bool _slowTickRegistered;
        private bool _serviceRegistered;
        private bool _hotSwapRegistered;
        private IPersistentDroppedItemRegistry _persistentDroppedItems;
        private int _scheduledModuleCount;
        private int _moduleCount;
        private static AutonomousExtractorSystem s_activeRuntime;

        internal IPersistentDroppedItemRegistry PersistentDroppedItems => _persistentDroppedItems;

        internal static bool TryGetActiveRuntime(out AutonomousExtractorSystem runtime)
        {
            runtime = s_activeRuntime;
            return runtime != null;
        }

        private void OnEnable()
        {
            TryRegisterToGlobalRegistry();
            if (!_serviceRegistered)
                return;

            TryRegisterHotSwapListener();
            if (!EnsureExtractorNativeStateCold())
                return;

            TryRegisterRuntimeLoops();
        }

        private void OnDisable()
        {
            TryUnregisterRuntimeLoops();
            TryUnregisterHotSwapListener();
            TryUnregisterFromGlobalRegistry();
            CompleteScheduledJobForTeardown();
        }

        private void OnDestroy()
        {
            TryUnregisterRuntimeLoops();
            TryUnregisterHotSwapListener();
            TryUnregisterFromGlobalRegistry();
            CompleteScheduledJobForTeardown();
            DisposeExtractorNativeState();
        }

        private void TryRegisterToGlobalRegistry()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            AutonomousExtractorSystem existingRuntime = GlobalRegistry.AutonomousExtractors;
            if (existingRuntime != null && !ReferenceEquals(existingRuntime, this))
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    DuplicateRuntimeWarningHash,
                    DuplicateRuntimeContextHash,
                    1f);
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterAutonomousExtractorRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.AutonomousExtractors, this);
            if (_serviceRegistered)
            {
                s_activeRuntime = this;
                _persistentDroppedItems = GlobalRegistry.PersistentDroppedItems;
            }
        }

        private void TryUnregisterFromGlobalRegistry()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterAutonomousExtractorRuntime(this);
            _serviceRegistered = false;
            _persistentDroppedItems = null;
            if (ReferenceEquals(s_activeRuntime, this))
                s_activeRuntime = null;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.PersistentWorldRegistry)
            {
                _persistentDroppedItems = currentService as IPersistentDroppedItemRegistry;
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher || currentService == null || !isActiveAndEnabled)
                return;

            TryUnregisterRuntimeLoops();
            TryRegisterRuntimeLoops();
        }

        private void TryRegisterRuntimeLoops()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_slowTickRegistered)
                _slowTickRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);

        }

        private void TryUnregisterRuntimeLoops()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
            {
                _slowTickRegistered = false;
                return;
            }

            if (_slowTickRegistered)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _slowTickRegistered = false;
            }

        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        /// <summary>
        /// Schedules the extractor advancement Burst pass on the slow-tick lane.
        /// </summary>
        public void SlowTick()
        {
            if (_scheduledJobActive && !TryCompleteScheduledExtractorJob(forceComplete: false))
                return;

            CompactModuleList();
            int moduleCount = _moduleCount;
            if (moduleCount <= 0)
                return;

            if (!_nativeState.IsReady(MaxModuleCapacity))
                return;

            if (!TryAcquireExtractorJobBuffers(
                    moduleCount,
                    out NativeArray<ExtractorJobInput> jobInputs,
                    out NativeArray<ExtractorJobResult> jobResults,
                    out NativeArray<float> cycleTimers,
                    out _,
                    out NativeArray<int> bufferedUnitCounts,
                    out _))
            {
                return;
            }

            for (int i = 0; i < moduleCount; i++)
            {
                AutonomousExtractorModule module = _modules[i];
                if (module == null)
                {
                    jobInputs[i] = default;
                    continue;
                }

                module.RefreshBinding(this);
                ResourceNode hostNode = module.BoundNode;
                ResourceNodeTemplate template = hostNode != null ? hostNode.ResourceTemplate : null;
                int capacity = template != null ? template.ExtractorInventoryCapacity : 0;
                int itemHashId = template != null ? template.ExtractorYieldItemHashId : 0;
                bool isActive = module.HasPower &&
                                hostNode != null &&
                                hostNode.gameObject.activeInHierarchy &&
                                template != null &&
                                template.SupportsAutonomousExtraction &&
                                bufferedUnitCounts[i] < capacity;

                jobInputs[i] = new ExtractorJobInput
                {
                    CycleTimerSeconds = cycleTimers[i],
                    CycleSeconds = template != null ? template.ExtractorCycleSeconds : 1f,
                    BufferedUnitCount = bufferedUnitCounts[i],
                    BufferedUnitCapacity = capacity,
                    ItemHashId = itemHashId,
                    IsActive = isActive ? (byte)1 : (byte)0
                };
            }

            AdvanceExtractionJob job = new AdvanceExtractionJob
            {
                Inputs = jobInputs,
                Results = jobResults,
                SlowTickDeltaSeconds = SlowTickDeltaSeconds
            };

            _scheduledModuleCount = moduleCount;
            _scheduledJobHandle = job.Schedule(moduleCount, 8);
            _scheduledJobActive = true;
        }

        private bool TryCompleteScheduledExtractorJob(bool forceComplete)
        {
            if (!_scheduledJobActive)
                return true;

            if (!DispatcherJobSwap.TryComplete(ref _scheduledJobHandle, forceComplete))
                return false;

            if (!TryReadLockedExtractorBuffers(
                    out _,
                    out NativeArray<ExtractorJobResult> jobResults,
                    out NativeArray<float> cycleTimers,
                    out NativeArray<int> bufferedItemHashIds,
                    out NativeArray<int> bufferedUnitCounts,
                    out NativeArray<int> completedCycleCounts))
            {
                _scheduledJobActive = false;
                _scheduledModuleCount = 0;
                return false;
            }

            for (int i = 0; i < _scheduledModuleCount; i++)
            {
                ExtractorJobResult result = jobResults[i];
                cycleTimers[i] = result.NextCycleTimerSeconds;
                bufferedItemHashIds[i] = result.BufferedItemHashId;
                completedCycleCounts[i] += result.CompletedCycleDelta;

                AutonomousExtractorModule module = i < _moduleCount ? _modules[i] : null;
                if (module == null)
                {
                    cycleTimers[i] = 0f;
                    bufferedItemHashIds[i] = 0;
                    bufferedUnitCounts[i] = 0;
                    completedCycleCounts[i] = 0;
                    continue;
                }

                int bufferedUnitCount = result.NextBufferedUnitCount;
                ResourceNode hostNode = module.BoundNode;
                ResourceNodeTemplate template = hostNode != null ? hostNode.ResourceTemplate : null;
                ItemData routedItem = template != null ? template.ExtractorYieldItem : null;
                if (result.CompletedCycleDelta > 0)
                    module.ConsumeExtractionPower(result.CompletedCycleDelta, SlowTickDeltaSeconds);

                if (bufferedUnitCount > 0 &&
                    routedItem != null &&
                    module.TryRouteBufferedOutput(routedItem, bufferedUnitCount, out int routedCount))
                {
                    bufferedUnitCount = math.max(0, bufferedUnitCount - routedCount);
                }

                bufferedUnitCounts[i] = bufferedUnitCount;

                module.ApplyRuntimeTelemetry(
                    result.BufferedItemHashId,
                    bufferedUnitCount,
                    completedCycleCounts[i],
                    result.IsOperating != 0);
            }

            _scheduledJobActive = false;
            _scheduledModuleCount = 0;
            return true;
        }

        internal int RegisterModule(AutonomousExtractorModule module)
        {
            if (module == null)
                return -1;

            for (int i = 0; i < _moduleCount; i++)
            {
                if (ReferenceEquals(_modules[i], module))
                    return i;

                if (_modules[i] != null || (_scheduledJobActive && i < _scheduledModuleCount))
                    continue;

                _modules[i] = module;
                module.SetRuntimeIndex(i);
                return i;
            }

            if (_moduleCount >= _modules.Length)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    ExtractorCapacityGrowthWarningHash,
                    ExtractorCapacityGrowthContextHash,
                    _modules.Length);
                return -1;
            }

            int newIndex = _moduleCount;
            _modules[newIndex] = module;
            _moduleCount++;
            module.SetRuntimeIndex(newIndex);
            return newIndex;
        }

        internal void UnregisterModule(AutonomousExtractorModule module)
        {
            if (module == null)
                return;

            int index = module.RuntimeIndex;
            if (index < 0 || index >= _moduleCount || !ReferenceEquals(_modules[index], module))
            {
                index = -1;
                for (int i = 0; i < _moduleCount; i++)
                {
                    if (ReferenceEquals(_modules[i], module))
                    {
                        index = i;
                        break;
                    }
                }
            }

            if (index < 0)
                return;

            _modules[index] = null;
            module.SetRuntimeIndex(-1);
            module.ApplyRuntimeTelemetry(0, 0, 0, false);

            if (_scheduledJobActive && index < _scheduledModuleCount)
                return;

            if (!TryAcquireExtractorStateBuffers(
                    out NativeArray<float> cycleTimers,
                    out NativeArray<int> bufferedItemHashIds,
                    out NativeArray<int> bufferedUnitCounts,
                    out NativeArray<int> completedCycleCounts))
            {
                return;
            }

            if ((uint)index >= (uint)cycleTimers.Length)
                return;

            cycleTimers[index] = 0f;
            bufferedItemHashIds[index] = 0;
            bufferedUnitCounts[index] = 0;
            completedCycleCounts[index] = 0;
        }

        internal bool IsNodeClaimed(ResourceNode node, AutonomousExtractorModule requester)
        {
            if (node == null)
                return false;

            int moduleCount = _moduleCount;
            for (int i = 0; i < moduleCount; i++)
            {
                AutonomousExtractorModule module = _modules[i];
                if (module == null || ReferenceEquals(module, requester))
                    continue;

                if (ReferenceEquals(module.BoundNode, node))
                    return true;
            }

            return false;
        }

        private void CompactModuleList()
        {
            for (int i = _moduleCount - 1; i >= 0; i--)
            {
                if (_modules[i] != null)
                    continue;

                int lastIndex = _moduleCount - 1;
                while (lastIndex > i && _modules[lastIndex] == null)
                {
                    _moduleCount--;
                    lastIndex--;
                }

                if (i >= _moduleCount)
                    continue;

                if (_modules[i] == null && _moduleCount - 1 > i)
                {
                    int sourceIndex = _moduleCount - 1;
                    AutonomousExtractorModule movedModule = _modules[sourceIndex];
                    _modules[i] = movedModule;
                    _modules[sourceIndex] = null;
                    _moduleCount--;
                    if (movedModule != null)
                        movedModule.SetRuntimeIndex(i);
                }
            }

            for (int i = _moduleCount - 1; i >= 0; i--)
            {
                if (_modules[i] != null)
                    break;

                _moduleCount--;
            }
        }

        private bool TryAcquireExtractorJobBuffers(
            int requiredCount,
            out NativeArray<ExtractorJobInput> jobInputs,
            out NativeArray<ExtractorJobResult> jobResults,
            out NativeArray<float> cycleTimers,
            out NativeArray<int> bufferedItemHashIds,
            out NativeArray<int> bufferedUnitCounts,
            out NativeArray<int> completedCycleCounts)
        {
            jobInputs = default;
            jobResults = default;
            cycleTimers = default;
            bufferedItemHashIds = default;
            bufferedUnitCounts = default;
            completedCycleCounts = default;

            if (requiredCount <= 0 || !_nativeState.IsReady(MaxModuleCapacity))
                return false;

            jobInputs = _nativeState.JobInputs;
            jobResults = _nativeState.JobResults;
            cycleTimers = _nativeState.CycleTimers;
            bufferedItemHashIds = _nativeState.BufferedItemHashIds;
            bufferedUnitCounts = _nativeState.BufferedUnitCounts;
            completedCycleCounts = _nativeState.CompletedCycleCounts;
            return jobInputs.IsCreated &&
                   jobResults.IsCreated &&
                   cycleTimers.IsCreated &&
                   bufferedItemHashIds.IsCreated &&
                   bufferedUnitCounts.IsCreated &&
                   completedCycleCounts.IsCreated &&
                   jobInputs.Length >= requiredCount &&
                   jobResults.Length >= requiredCount &&
                   cycleTimers.Length >= requiredCount &&
                   bufferedItemHashIds.Length >= requiredCount &&
                   bufferedUnitCounts.Length >= requiredCount &&
                   completedCycleCounts.Length >= requiredCount;
        }

        private bool TryAcquireExtractorStateBuffers(
            out NativeArray<float> cycleTimers,
            out NativeArray<int> bufferedItemHashIds,
            out NativeArray<int> bufferedUnitCounts,
            out NativeArray<int> completedCycleCounts)
        {
            cycleTimers = default;
            bufferedItemHashIds = default;
            bufferedUnitCounts = default;
            completedCycleCounts = default;

            if (!_nativeState.IsReady(MaxModuleCapacity))
                return false;

            cycleTimers = _nativeState.CycleTimers;
            bufferedItemHashIds = _nativeState.BufferedItemHashIds;
            bufferedUnitCounts = _nativeState.BufferedUnitCounts;
            completedCycleCounts = _nativeState.CompletedCycleCounts;
            return cycleTimers.IsCreated &&
                   bufferedItemHashIds.IsCreated &&
                   bufferedUnitCounts.IsCreated &&
                   completedCycleCounts.IsCreated;
        }

        private bool TryReadLockedExtractorBuffers(
            out NativeArray<ExtractorJobInput> jobInputs,
            out NativeArray<ExtractorJobResult> jobResults,
            out NativeArray<float> cycleTimers,
            out NativeArray<int> bufferedItemHashIds,
            out NativeArray<int> bufferedUnitCounts,
            out NativeArray<int> completedCycleCounts)
        {
            jobInputs = default;
            jobResults = default;
            cycleTimers = default;
            bufferedItemHashIds = default;
            bufferedUnitCounts = default;
            completedCycleCounts = default;

            int requiredCount = _scheduledModuleCount;
            jobInputs = _nativeState.JobInputs;
            jobResults = _nativeState.JobResults;
            cycleTimers = _nativeState.CycleTimers;
            bufferedItemHashIds = _nativeState.BufferedItemHashIds;
            bufferedUnitCounts = _nativeState.BufferedUnitCounts;
            completedCycleCounts = _nativeState.CompletedCycleCounts;
            return requiredCount >= 0 &&
                   jobInputs.IsCreated &&
                   jobResults.IsCreated &&
                   cycleTimers.IsCreated &&
                   bufferedItemHashIds.IsCreated &&
                   bufferedUnitCounts.IsCreated &&
                   completedCycleCounts.IsCreated &&
                   jobInputs.Length >= requiredCount &&
                   jobResults.Length >= requiredCount &&
                   cycleTimers.Length >= requiredCount &&
                   bufferedItemHashIds.Length >= requiredCount &&
                   bufferedUnitCounts.Length >= requiredCount &&
                   completedCycleCounts.Length >= requiredCount;
        }

        private void CompleteScheduledJobForTeardown()
        {
            if (!_scheduledJobActive)
            {
                _scheduledJobHandle = default;
                _scheduledModuleCount = 0;
                return;
            }

            TryCompleteScheduledExtractorJob(forceComplete: true);
        }

        private bool EnsureExtractorNativeStateCold()
        {
            if (_nativeState.IsReady(MaxModuleCapacity))
                return true;

            _nativeState.Ensure(MaxModuleCapacity);
            return _nativeState.IsReady(MaxModuleCapacity);
        }

        private void DisposeExtractorNativeState()
        {
            _nativeState.Dispose();
        }

    }

    /// <summary>
    /// Player-placed extractor module that binds to one large autonomous resource vein and consumes grid power while the runtime system advances its inventory.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PowerNode))]
    [AddComponentMenu("Hecton8/Construction/Autonomous Extractor Module")]
    public sealed class AutonomousExtractorModule : MonoBehaviour, IPoolable, IPowerComponent
    {
        private const string DefaultPlacementBlockedReason = "INFINITE VEIN REQUIRED";
        private const string DefaultClaimBlockedReason = "VEIN ALREADY CLAIMED";
        private const string DefaultNodeScaleBlockedReason = "VEIN TOO SMALL";
        private const int PlacementOverlapCapacity = 24;
        private const int ResourceNodeLookupCacheCapacity = PlacementOverlapCapacity;
        private const uint ExtractorOverflowDropWarningHash = 0x6DAE28B7u;
        private const uint ExtractorOverflowDropContextHash = 0xD9113EF2u;
        // COLD ALLOC: Collider[24] — placement/resource-node overlap buffer — owner: AutonomousExtractorModule
        private static readonly SpatialQueryHit[] PlacementSpatialBuffer = new SpatialQueryHit[PlacementOverlapCapacity];

        [Header("Placement")]
        [SerializeField, Range(0.5f, 6f)]
        [Tooltip("Search radius used to find a valid infinite-vein host under the placed extractor.")]
        private float placementProbeRadius = 2.25f;

        [SerializeField, Range(0.5f, 8f)]
        [Tooltip("Runtime search radius used to refresh the bound host vein when the local node set changes.")]
        private float bindingRefreshRadius = 3f;

        [SerializeField, Range(0.5f, 8f)]
        [Tooltip("Minimum authored node diameter in meters required before the extractor may bind.")]
        private float minimumHostDiameterMeters = 1.5f;

        [Header("Power")]
        [SerializeField, Range(0f, 500f)]
        [Tooltip("Continuous grid draw while the extractor is actively advancing an autonomous cycle.")]
        private float activePowerDraw = 180f;

        [SerializeField, Range(0, 100)]
        [Tooltip("Brownout priority for extractor loads.")]
        private int powerPriority = 34;

        [Header("Output")]
        [SerializeField] private Transform outputSocket;
        [SerializeField] private Vector3 outputDirectionLocal = Vector3.forward;
        [SerializeField, Range(0f, 3f)] private float outputForwardOffset = 0.5f;
        [SerializeField, Range(0f, 2f)] private float outputLiftOffset = 0.15f;
        [SerializeField, Range(0f, 8f)] private float outputVelocityChange = 1.4f;
        [SerializeField, Range(0f, 4f)] private float outputUpwardVelocityChange = 0.35f;

        [Header("Diagnostics")]
        [SerializeField] private bool _debugHasPower = true;
        [SerializeField] private bool _debugIsOperating;
        [SerializeField] private int _debugBufferedItemHashId;
        [SerializeField] private int _debugBufferedUnitCount;
        [SerializeField] private int _debugCompletedCycleCount;
        [SerializeField] private string _debugBoundNodeId;

        private PowerNode _powerNode;
        private ResourceNode _boundNode;
        private bool _registered;
        private bool _hasPower = true;
        private bool _isOperating;
        private int _runtimeIndex = -1;
        // COLD ALLOC: ulong[24] — overlap collider id cache for resource-node discovery — owner: AutonomousExtractorModule
        private readonly ulong[] _resourceNodeLookupColliderIds = new ulong[ResourceNodeLookupCacheCapacity];
        // COLD ALLOC: ResourceNode[24] — overlap collider resolved resource cache — owner: AutonomousExtractorModule
        private readonly ResourceNode[] _resourceNodeLookupNodes = new ResourceNode[ResourceNodeLookupCacheCapacity];
        private int _resourceNodeLookupCount;
        private int _resourceNodeLookupWriteCursor;

        /// <summary>True while the module is currently drawing grid power for extraction.</summary>
        public bool IsOperating => _isOperating;

        /// <summary>Current bound autonomous resource host.</summary>
        public ResourceNode BoundNode => _boundNode;

        /// <summary>Runtime index inside the extractor SOA owner.</summary>
        internal int RuntimeIndex => _runtimeIndex;

        /// <inheritdoc />
        public float PowerRating => _isOperating ? -activePowerDraw : 0f;

        /// <inheritdoc />
        public int PowerPriority => powerPriority;

        /// <inheritdoc />
        public bool HasPower => _hasPower;

        private void Awake()
        {
            TryGetComponent(out _powerNode);
        }

        private void OnEnable()
        {
            ClearResourceNodeLookupCache();
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
            ClearResourceNodeLookupCache();
        }

        private void OnDestroy()
        {
            TryUnregister();
            ClearResourceNodeLookupCache();
        }

        /// <inheritdoc />
        public void OnSpawn()
        {
            _hasPower = true;
            _debugHasPower = true;
            SetBoundNode(null);
            ApplyRuntimeTelemetry(0, 0, 0, false);
            ClearResourceNodeLookupCache();
            TryRegister();
        }

        /// <inheritdoc />
        public void OnDespawn()
        {
            TryUnregister();
            _hasPower = true;
            _debugHasPower = true;
            SetBoundNode(null);
            ApplyRuntimeTelemetry(0, 0, 0, false);
            ClearResourceNodeLookupCache();
        }

        /// <inheritdoc />
        public void OnPowerStatusChanged(bool hasPower)
        {
            _hasPower = hasPower;
            _debugHasPower = hasPower;

            if (!hasPower)
                ApplyRuntimeTelemetry(_debugBufferedItemHashId, _debugBufferedUnitCount, _debugCompletedCycleCount, false);
        }

        internal bool ValidatePlacementWithRuntime(
            Vector3 position,
            Quaternion rotation,
            AutonomousExtractorSystem runtime,
            out string blockReason)
        {
            if (runtime == null)
            {
                blockReason = DefaultPlacementBlockedReason;
                return false;
            }

            if (!TryResolveNearestValidNode(position, placementProbeRadius, runtime, out ResourceNode node))
            {
                blockReason = DefaultPlacementBlockedReason;
                return false;
            }

            if (runtime.IsNodeClaimed(node, this))
            {
                blockReason = DefaultClaimBlockedReason;
                return false;
            }

            if (!MeetsSizeThreshold(node))
            {
                blockReason = DefaultNodeScaleBlockedReason;
                return false;
            }

            blockReason = string.Empty;
            return true;
        }

        internal void RefreshBinding(AutonomousExtractorSystem runtime)
        {
            if (runtime == null)
                return;

            if (IsCurrentBindingValid(runtime))
                return;

            AbsoluteUniversePosition moduleAup = default;
            Vector3 moduleVisualPosition = transform.position;
            if (TryResolveNearestValidNode(moduleVisualPosition, false, in moduleAup, bindingRefreshRadius, runtime, out ResourceNode node))
            {
                SetBoundNode(node);
                return;
            }

            SetBoundNode(null);
        }

        internal void ApplyRuntimeTelemetry(int itemHashId, int bufferedUnitCount, int completedCycleCount, bool isOperating)
        {
            bool previousOperating = _isOperating;
            _isOperating = isOperating;
            _debugIsOperating = isOperating;
            _debugBufferedItemHashId = itemHashId;
            _debugBufferedUnitCount = bufferedUnitCount;
            _debugCompletedCycleCount = completedCycleCount;

            if (previousOperating != isOperating)
                NotifyGridBalanceChanged();
        }

        internal void SetRuntimeIndex(int runtimeIndex)
        {
            _runtimeIndex = runtimeIndex;
        }

        internal void ConsumeExtractionPower(int completedCycleDelta, float tickSeconds)
        {
            if (completedCycleDelta <= 0 || _powerNode == null || _powerNode.Grid == null)
                return;

            float wattSeconds = activePowerDraw * Mathf.Max(0.001f, tickSeconds) * completedCycleDelta;
            if (wattSeconds > 0f)
                _powerNode.Grid.ConsumePower(wattSeconds);
        }

        internal bool TryRouteBufferedOutput(ItemData item, int bufferedUnitCount, out int routedCount)
        {
            routedCount = 0;
            if (item == null || bufferedUnitCount <= 0 || _powerNode == null)
                return false;

            if (BaseLogisticsNetwork.TryDepositItem(_powerNode, item, bufferedUnitCount, out int depositedCount))
                routedCount = depositedCount;

            int remainingCount = bufferedUnitCount - routedCount;
            if (remainingCount <= 0)
                return routedCount > 0;

            if (TrySpillBufferedOutput(item, remainingCount))
            {
                routedCount += remainingCount;
                return true;
            }

            return routedCount > 0;
        }

        private bool TrySpillBufferedOutput(ItemData item, int quantity)
        {
            if (item == null || quantity <= 0)
                return false;

            if (!AutonomousExtractorSystem.TryGetActiveRuntime(out AutonomousExtractorSystem runtime))
                return false;

            IPersistentDroppedItemRegistry registry = runtime.PersistentDroppedItems;
            if (registry == null)
                return false;

            ResolveOutputPose(out Vector3 spawnPosition, out Vector3 velocityChange);
            GlobalTelemetryBus.PublishPerformanceWarning(
                ExtractorOverflowDropWarningHash,
                ExtractorOverflowDropContextHash,
                quantity);
            return registry.TryRegisterDroppedItem(item, quantity, spawnPosition, Vector3.zero, velocityChange);
        }

        private void ResolveOutputPose(out Vector3 spawnPosition, out Vector3 velocityChange)
        {
            Transform origin = outputSocket != null ? outputSocket : transform;
            Vector3 localDirection = outputDirectionLocal.sqrMagnitude > 0.0001f
                ? outputDirectionLocal.normalized
                : Vector3.forward;
            Vector3 worldDirection = origin.TransformDirection(localDirection);
            if (worldDirection.sqrMagnitude <= 0.0001f)
                worldDirection = origin.forward;

            worldDirection.Normalize();
            spawnPosition = origin.position + worldDirection * outputForwardOffset + Vector3.up * outputLiftOffset;
            velocityChange = worldDirection * outputVelocityChange + Vector3.up * outputUpwardVelocityChange;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (!AutonomousExtractorSystem.TryGetActiveRuntime(out AutonomousExtractorSystem runtime))
                return;

            _registered = runtime.RegisterModule(this) >= 0;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            AutonomousExtractorSystem.TryGetActiveRuntime(out AutonomousExtractorSystem runtime);
            if (runtime != null)
                runtime.UnregisterModule(this);

            _registered = false;
            _runtimeIndex = -1;
        }

        private bool IsCurrentBindingValid(AutonomousExtractorSystem runtime)
        {
            return _boundNode != null &&
                   _boundNode.gameObject.activeInHierarchy &&
                   !_boundNode.IsDepleted &&
                   _boundNode.ResourceTemplate != null &&
                   _boundNode.ResourceTemplate.SupportsAutonomousExtraction &&
                   MeetsSizeThreshold(_boundNode) &&
                   !runtime.IsNodeClaimed(_boundNode, this);
        }

        private bool TryResolveNearestValidNode(Vector3 position, float probeRadius, AutonomousExtractorSystem runtime, out ResourceNode node)
        {
            bool hasQueryAup = TryResolveAupFromRuntimeOrigin(position, out AbsoluteUniversePosition queryAup);
            return TryResolveNearestValidNode(position, hasQueryAup, in queryAup, probeRadius, runtime, out node);
        }

        private bool TryResolveNearestValidNode(
            Vector3 position,
            bool hasQueryAup,
            in AbsoluteUniversePosition queryAup,
            float probeRadius,
            AutonomousExtractorSystem runtime,
            out ResourceNode node)
        {
            node = null;
            float safeRadius = Mathf.Max(0.5f, probeRadius);
            int overlapCount = WorldSpatialHashGrid.CollectContactsNonAlloc(
                position,
                safeRadius,
                SpatialTargetKind.Resource,
                PlacementSpatialBuffer);
            if (overlapCount <= 0)
                return false;

            float bestDistanceSqr = float.MaxValue;
            for (int i = 0; i < overlapCount; i++)
            {
                SpatialQueryHit hit = PlacementSpatialBuffer[i];
                PlacementSpatialBuffer[i] = default;
                if (!LayerMatchesMask(hit.Layer, HectonLayerMasks.StrictInteractionLayerMask))
                    continue;

                ResourceNode candidate = hit.Owner as ResourceNode;
                if (candidate == null ||
                    candidate.IsDepleted ||
                    !candidate.gameObject.activeInHierarchy ||
                    candidate.ResourceTemplate == null ||
                    !candidate.ResourceTemplate.SupportsAutonomousExtraction ||
                    !MeetsSizeThreshold(candidate) ||
                    runtime.IsNodeClaimed(candidate, this))
                {
                    continue;
                }

                float distanceSqr = ResolveCandidateDistanceSq(candidate, position, hasQueryAup, in queryAup);
                if (distanceSqr >= bestDistanceSqr)
                    continue;

                bestDistanceSqr = distanceSqr;
                node = candidate;
            }

            return node != null;
        }

        private static bool LayerMatchesMask(int layer, int mask)
        {
            return layer >= 0 && layer < 32 && (mask & (1 << layer)) != 0;
        }

        private static float ResolveCandidateDistanceSq(
            ResourceNode candidate,
            Vector3 queryRuntimePosition,
            bool hasQueryAup,
            in AbsoluteUniversePosition queryAup)
        {
            if (candidate != null &&
                hasQueryAup &&
                IsFinite(in queryAup) &&
                candidate.TryGetPersistentAup(out AbsoluteUniversePosition candidateAup))
            {
                return SaturateDistanceSq(AbsoluteUniversePosition.DistanceSq(in candidateAup, in queryAup));
            }

            return float.MaxValue;
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            float3 runtime = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(runtime)))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!IsFinite(in originAup))
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(in originAup, new double3(runtime.x, runtime.y, runtime.z));
            return IsFinite(in positionAup);
        }

        private static bool IsFinite(in AbsoluteUniversePosition position)
        {
            return math.isfinite(position.LocalX) &&
                   math.isfinite(position.LocalY) &&
                   math.isfinite(position.LocalZ);
        }

        private static float SaturateDistanceSq(double distanceSq)
        {
            if (!math.isfinite(distanceSq))
                return float.MaxValue;

            if (distanceSq <= 0d)
                return 0f;

            return distanceSq >= float.MaxValue ? float.MaxValue : (float)distanceSq;
        }

        private bool TryResolveResourceNode(Collider collider, out ResourceNode node)
        {
            node = null;
            if (collider == null)
                return false;

            ulong colliderId = ResolveColliderRuntimeId(collider);
            if (colliderId != 0UL)
            {
                for (int i = 0; i < _resourceNodeLookupCount; i++)
                {
                    if (_resourceNodeLookupColliderIds[i] != colliderId)
                        continue;

                    node = _resourceNodeLookupNodes[i];
                    if (node != null)
                        return node.gameObject.activeInHierarchy;

                    _resourceNodeLookupColliderIds[i] = 0UL;
                    break;
                }
            }

            if (!collider.TryGetComponent(out node))
                ConstructionParentLookup.TryCaptureSelfOrParent(collider, out node);

            if (colliderId != 0UL && node != null)
                CacheResourceNodeLookup(colliderId, node);

            return node != null;
        }

        private void CacheResourceNodeLookup(ulong colliderId, ResourceNode node)
        {
            if (colliderId == 0UL || node == null)
                return;

            int slot;
            if (_resourceNodeLookupCount < _resourceNodeLookupColliderIds.Length)
            {
                slot = _resourceNodeLookupCount;
                _resourceNodeLookupCount++;
            }
            else
            {
                slot = _resourceNodeLookupWriteCursor;
            }

            _resourceNodeLookupColliderIds[slot] = colliderId;
            _resourceNodeLookupNodes[slot] = node;
            _resourceNodeLookupWriteCursor = (_resourceNodeLookupWriteCursor + 1) % _resourceNodeLookupColliderIds.Length;
        }

        private void ClearResourceNodeLookupCache()
        {
            for (int i = 0; i < _resourceNodeLookupCount; i++)
            {
                _resourceNodeLookupColliderIds[i] = 0UL;
                _resourceNodeLookupNodes[i] = null;
            }

            _resourceNodeLookupCount = 0;
            _resourceNodeLookupWriteCursor = 0;
        }

        private static ulong ResolveColliderRuntimeId(Collider collider)
        {
            return collider != null
                ? EntityId.ToULong(collider.GetEntityId())
                : 0UL;
        }

        private bool MeetsSizeThreshold(ResourceNode node)
        {
            if (node == null || node.ResourceTemplate == null)
                return false;

            Vector3 size = node.ResourceTemplate.PhysicalSize;
            float diameter = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            return diameter >= Mathf.Max(0.5f, minimumHostDiameterMeters);
        }

        private void SetBoundNode(ResourceNode node)
        {
            _boundNode = node;
            _debugBoundNodeId = node != null ? node.UniqueId : string.Empty;
        }

        private void NotifyGridBalanceChanged()
        {
            PowerGrid grid = _powerNode != null ? _powerNode.Grid : null;
            if (grid != null)
                grid.MarkDirty();
        }
    }
}

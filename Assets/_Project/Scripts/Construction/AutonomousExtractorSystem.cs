using System.Runtime.InteropServices;
using Hecton8.Building;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay.Atlas6Liability;
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
    public sealed class AutonomousExtractorSystem : MonoBehaviour, ISlowTickable, IPostFixedTickable, IGlobalRegistryHotSwapListener
    {
        private const int MaxModuleCapacity = 256;
        private const float SlowTickDeltaSeconds = 0.5f;
        private const int MaxPendingCompletionFrames = 4;
        private const uint ExtractorCapacityGrowthWarningHash = 0xA8754B21u;
        private const uint ExtractorCapacityGrowthContextHash = 0xE71C92D4u;
        private const uint DuplicateRuntimeWarningHash = 0xB44D12E9u;
        private const uint DuplicateRuntimeContextHash = 0xAD50966Cu;
        private const uint ExtractorPendingJobStallWarningHash = 0x41D71703u;
        private const uint ExtractorPendingJobStallContextHash = 0x1B26C087u;
        private const uint RuntimeInstallFailedWarningHash = 0x2F63A11Du;
        private const uint RuntimeInstallFailedContextHash = 0x7C4E5B90u;
        private const string RuntimeRootName = "__HECTON_CONSTRUCTION_RUNTIME";
        private const SystemID NativeArrayOwnerSystem = SystemID.Construction;

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
                    JobInputs = H8Memory.Allocate<ExtractorJobInput>(capacity, NativeArrayOwnerSystem, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                    JobResults = H8Memory.Allocate<ExtractorJobResult>(capacity, NativeArrayOwnerSystem, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                    CycleTimers = H8Memory.Allocate<float>(capacity, NativeArrayOwnerSystem, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                    BufferedItemHashIds = H8Memory.Allocate<int>(capacity, NativeArrayOwnerSystem, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                    BufferedUnitCounts = H8Memory.Allocate<int>(capacity, NativeArrayOwnerSystem, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                    CompletedCycleCounts = H8Memory.Allocate<int>(capacity, NativeArrayOwnerSystem, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                }
                catch
                {
                    Dispose();
                    throw;
                }

                if (!IsReady(capacity))
                    Dispose();
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

                H8Memory.Release(ref array, NativeArrayOwnerSystem);
            }
        }

        // COLD ALLOC: AutonomousExtractorModule[MaxModuleCapacity] - fixed runtime extractor registry; no managed growth - owner: AutonomousExtractorSystem
        private readonly AutonomousExtractorModule[] _modules = new AutonomousExtractorModule[MaxModuleCapacity];
        private ExtractorNativeState _nativeState;
        private JobHandle _scheduledJobHandle;
        private bool _scheduledJobActive;
        private bool _slowTickRegistered;
        private bool _postFixedRegistered;
        private bool _serviceRegistered;
        private bool _hotSwapRegistered;
        private IPersistentDroppedItemRegistry _persistentDroppedItems;
        private int _scheduledModuleCount;
        private int _scheduledJobAgeFrames;
        private bool _dropScheduledJobReadback;
        private int _lastPendingJobWarningFrame = -1;
        private int _moduleCount;
        private static AutonomousExtractorSystem s_activeRuntime;
        private static int s_signalPushDropCount;

        internal IPersistentDroppedItemRegistry PersistentDroppedItems => _persistentDroppedItems;

        internal static bool TryGetActiveRuntime(out AutonomousExtractorSystem runtime)
        {
            runtime = s_activeRuntime;
            return runtime != null;
        }

        /// <summary>
        /// Owner-local factory route that brings the extractor SOA owner into existence.
        /// <para>
        /// This type had NO construction site of any kind. Nothing calls
        /// <c>AddComponent&lt;AutonomousExtractorSystem&gt;</c> anywhere under <c>Assets/</c>, and a scene
        /// census that walks the editor object model found it absent from 00_BOOTSTRAP, 01_MAIN_MENU and
        /// 02_HECTON_WORLD. GlobalRegistry.cs holds only the slot (:648 field, :2252 accessor, :4511
        /// register, :6283 unregister, :7960 slot read, :8383 type map) and nothing ever fills it, so
        /// <see cref="GlobalRegistry.AutonomousExtractors"/> is permanently null in a shipped build. Two
        /// consumers therefore fail without ever saying why: PlayerBuilder.cs:2135-2140 passes that null
        /// into <see cref="AutonomousExtractorModule.ValidatePlacementWithRuntime"/>, which rejects the
        /// placement with a reason that blames the world for missing an infinite vein, and
        /// <see cref="AutonomousExtractorModule"/> never reaches <see cref="SlowTick"/> at all.
        /// </para>
        /// <para>
        /// The route is not abandoned content. Nine authored ResourceNodeTemplate assets under
        /// Assets/_Project/Data/Scavenging/ResourceNodes/ set <c>supportsAutonomousExtraction: 1</c>
        /// (AbyssalCrystalSpire, AegiriumCrustNodule, CarbonGraphiteNodule, DeepMantleGeode,
        /// PressureDiamond, RareEarthDustBed, Silicon7BGlassVein, TitaniumBasaltMass,
        /// XenonOmegaVentCache), the vein-extraction fields those assets carry
        /// (ResourceNodeTemplate.cs:526/:551/:561/:564) have exactly one consumer in the whole project -
        /// this owner - and <see cref="Atlas6CorporateLiabilityManager.TryReportXenonOmegaExtracted"/>
        /// consumes its yield for the liability route.
        /// </para>
        /// <para>
        /// Shape follows the installer family - EcosystemRuntimeInstaller.cs:56-74 owns the identical
        /// resolve-or-create runtime root, including the two traps it documents: a resolved root can come
        /// back hidden or deactivated, and AddComponent on an inactive GameObject never runs OnEnable, so
        /// the owner would exist and never register. Registration is verified against the registry rather
        /// than assumed, because <see cref="TryRegisterToGlobalRegistry"/> can legally refuse.
        /// .agents-skills/ARCH_Project_Bootstrap_Sequence_Init_Safety.txt Section3 names "source-proven
        /// owner-local factory routes" as a legal registration source alongside 00_BOOTSTRAP bindings and
        /// installer records; its [FORBID] entries cover self-heal and lazy creation after the first
        /// gameplay frame, so this must be called from boot composition, never from a consumer that found
        /// the service missing.
        /// </para>
        /// </summary>
        /// <param name="runtime">Registered extractor owner, or null when the install could not complete.</param>
        /// <returns>True when <see cref="GlobalRegistry.AutonomousExtractors"/> resolves to a live owner.</returns>
        public static bool TryEnsureRuntimeOwner(out AutonomousExtractorSystem runtime)
        {
            runtime = GlobalRegistry.AutonomousExtractors;
            if (runtime != null)
                return true;

            if (!Application.isPlaying)
                return false;

            GameObject runtimeRoot = null;
            WorldRuntimeReferenceUtility.TryResolveScenePath(ref runtimeRoot, RuntimeRootName);
            if (runtimeRoot == null)
                runtimeRoot = new GameObject(RuntimeRootName); // COLD ALLOC: one construction runtime root per gameplay scene - owner: AutonomousExtractorSystem

            runtimeRoot.hideFlags = HideFlags.None;
            if (!runtimeRoot.activeSelf)
                runtimeRoot.SetActive(true);

            if (!runtimeRoot.TryGetComponent(out runtime) || runtime == null)
                runtime = runtimeRoot.AddComponent<AutonomousExtractorSystem>();
            else if (!runtime.enabled)
                runtime.enabled = true;

            if (runtime != null && ReferenceEquals(GlobalRegistry.AutonomousExtractors, runtime))
                return true;

            GlobalTelemetryBus.PublishPerformanceWarning(
                RuntimeInstallFailedWarningHash,
                RuntimeInstallFailedContextHash,
                1f);
            runtime = null;
            return false;
        }

        private void OnEnable()
        {
            TryRegisterToGlobalRegistry();
            if (!_serviceRegistered)
                return;

            TryRegisterHotSwapListener();
            EnsureExtractorSignalLanes();
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

            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            bool hadRuntimeLoops = _slowTickRegistered || _postFixedRegistered;
            TryUnregisterRuntimeLoops();
            if (hadRuntimeLoops && currentService != null && isActiveAndEnabled)
                TryRegisterRuntimeLoops();
        }

        private void TryRegisterRuntimeLoops()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_slowTickRegistered)
                _slowTickRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);

            if (!_postFixedRegistered)
                _postFixedRegistered = GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterRuntimeLoops()
        {
            if (_slowTickRegistered)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _slowTickRegistered = false;
            }

            if (_postFixedRegistered)
            {
                GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Environment);
                _postFixedRegistered = false;
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
            _scheduledJobAgeFrames = 0;
            _dropScheduledJobReadback = false;
        }

        public void PostFixedTick(float fixedDeltaTime)
        {
            if (!_scheduledJobActive)
                return;

            _scheduledJobAgeFrames++;
            if (TryCompleteScheduledExtractorJob(forceComplete: false))
                return;

            if (_scheduledJobAgeFrames < MaxPendingCompletionFrames)
                return;

            if (_dropScheduledJobReadback)
                return;

            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastPendingJobWarningFrame == frame)
                return;

            _lastPendingJobWarningFrame = frame;
            _dropScheduledJobReadback = true;
            GlobalTelemetryBus.PublishPerformanceWarning(
                ExtractorPendingJobStallWarningHash,
                ExtractorPendingJobStallContextHash,
                _scheduledModuleCount);
        }

        private bool TryCompleteScheduledExtractorJob(bool forceComplete)
        {
            if (!_scheduledJobActive)
                return true;

            if (!DispatcherJobSwap.TryComplete(ref _scheduledJobHandle, forceComplete))
                return false;

            if (_dropScheduledJobReadback && !forceComplete)
            {
                _scheduledJobActive = false;
                _scheduledModuleCount = 0;
                _scheduledJobAgeFrames = 0;
                _dropScheduledJobReadback = false;
                return true;
            }

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
                _scheduledJobAgeFrames = 0;
                _dropScheduledJobReadback = false;
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
                    ExtractorSlotLanes.TryClearRow(
                        cycleTimers,
                        bufferedItemHashIds,
                        bufferedUnitCounts,
                        completedCycleCounts,
                        i);
                    continue;
                }

                int bufferedUnitCount = result.NextBufferedUnitCount;
                ResourceNode hostNode = module.BoundNode;
                ResourceNodeTemplate template = hostNode != null ? hostNode.ResourceTemplate : null;
                ItemData routedItem = template != null ? template.ExtractorYieldItem : null;
                if (result.CompletedCycleDelta > 0)
                {
                    module.ConsumeExtractionPower(result.CompletedCycleDelta, SlowTickDeltaSeconds);
                    Atlas6CorporateLiabilityManager.TryReportXenonOmegaExtracted(
                        hostNode != null ? hostNode.ResourceTemplateStableHashId : 0,
                        result.CompletedCycleDelta);
                }

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
            _scheduledJobAgeFrames = 0;
            _dropScheduledJobReadback = false;
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
                ClearExtractorStateRow(i);
                module.SetRuntimeIndex(i);
                return i;
            }

            if (_moduleCount >= _modules.Length)
            {
                PublishExtractorCapacityReached(module);
                GlobalTelemetryBus.PublishPerformanceWarning(
                    ExtractorCapacityGrowthWarningHash,
                    ExtractorCapacityGrowthContextHash,
                    _modules.Length);
                return -1;
            }

            int newIndex = _moduleCount;
            _modules[newIndex] = module;
            ClearExtractorStateRow(newIndex);
            _moduleCount++;
            module.SetRuntimeIndex(newIndex);
            return newIndex;
        }

        private static void EnsureExtractorSignalLanes()
        {
            SignalBus<ExtractorCapacityReachedSignal>.Configure(
                ExtractorCapacityReachedSignal.ExpectedCapacity,
                maxFrameSignals: ExtractorCapacityReachedSignal.MaxFrameSignals,
                lowTierFrameSignals: ExtractorCapacityReachedSignal.LowTierFrameSignals,
                laneHash: ExtractorCapacityReachedSignal.LaneHash);
            SignalBus<ExtractorCapacityReachedSignal>.EnsureInitialized();
        }

        private void PublishExtractorCapacityReached(AutonomousExtractorModule module)
        {
            ExtractorCapacityReachedSignal signal = new ExtractorCapacityReachedSignal
            {
                Frame = SystemDispatcher.CurrentFrameId,
                Capacity = MaxModuleCapacity,
                ActiveCount = _moduleCount,
                ModuleInstanceId = module != null ? unchecked((int)EntityId.ToULong(module.GetEntityId())) : 0,
                Flags = 1u,
                ContextHash = ExtractorCapacityGrowthContextHash
            };
            SignalBus<ExtractorCapacityReachedSignal>.TryPushTracked(in signal, ref s_signalPushDropCount);
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

            ClearExtractorStateRow(index);
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
                    MoveExtractorStateRow(sourceIndex, i);
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

        /// <summary>
        /// Carries the accumulated cycle/buffer row with a module that compaction relocated to a lower slot.
        /// The lanes are keyed by slot index, so without this the moved extractor reads the vacated slot's
        /// zeroed row and its buffered yield is destroyed. Only reachable from the SlowTick compaction pass,
        /// which runs after the extraction job has been completed, so the lanes carry no in-flight job alias.
        /// </summary>
        private void MoveExtractorStateRow(int sourceIndex, int destinationIndex)
        {
            if (!TryAcquireExtractorStateBuffers(
                    out NativeArray<float> cycleTimers,
                    out NativeArray<int> bufferedItemHashIds,
                    out NativeArray<int> bufferedUnitCounts,
                    out NativeArray<int> completedCycleCounts))
            {
                return;
            }

            ExtractorSlotLanes.TryMoveRow(
                cycleTimers,
                bufferedItemHashIds,
                bufferedUnitCounts,
                completedCycleCounts,
                sourceIndex,
                destinationIndex);
        }

        /// <summary>
        /// Zeroes one accumulation row through the single owner of that rule, <see cref="ExtractorSlotLanes"/>.
        /// Used on slot claim (a new extractor must not inherit an abandoned buffer and deposit units that were
        /// never mined) and on module unregistration (the departing extractor's tally must not survive the slot).
        /// Adding a fifth lane therefore means editing <see cref="ExtractorSlotLanes.TryClearRow"/> only.
        /// </summary>
        private void ClearExtractorStateRow(int index)
        {
            if (!TryAcquireExtractorStateBuffers(
                    out NativeArray<float> cycleTimers,
                    out NativeArray<int> bufferedItemHashIds,
                    out NativeArray<int> bufferedUnitCounts,
                    out NativeArray<int> completedCycleCounts))
            {
                return;
            }

            ExtractorSlotLanes.TryClearRow(
                cycleTimers,
                bufferedItemHashIds,
                bufferedUnitCounts,
                completedCycleCounts,
                index);
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
                _scheduledJobAgeFrames = 0;
                _dropScheduledJobReadback = false;
                return;
            }

            TryCompleteScheduledExtractorJob(forceComplete: true);
        }

        private bool EnsureExtractorNativeStateCold()
        {
            if (!ValidateExtractorAbiLayout())
                return false;

            if (_nativeState.IsReady(MaxModuleCapacity))
                return true;

            _nativeState.Ensure(MaxModuleCapacity);
            return _nativeState.IsReady(MaxModuleCapacity);
        }

        private static bool ValidateExtractorAbiLayout()
        {
            return UnsafeUtility.SizeOf<ExtractorJobInput>() == 32 &&
                   UnsafeUtility.SizeOf<ExtractorJobResult>() == 32 &&
                   UnsafeUtility.SizeOf<ExtractorCapacityReachedSignal>() == 32 &&
                   (UnsafeUtility.SizeOf<ExtractorJobInput>() & 7) == 0 &&
                   (UnsafeUtility.SizeOf<ExtractorJobResult>() & 7) == 0 &&
                   (UnsafeUtility.SizeOf<ExtractorCapacityReachedSignal>() & 7) == 0;
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
        /// <summary>
        /// Shown when the extractor SOA owner is absent, which is a system fault and not a world condition.
        /// The other three reasons all name something the player can act on - find a bigger vein, find an
        /// unclaimed one. Reusing <see cref="DefaultPlacementBlockedReason"/> here told a player standing on
        /// a perfectly valid infinite vein to go find an infinite vein, with no action that could ever clear
        /// it. Same defect shape as commit 77bb63582: a lifetime bug reported as missing content.
        /// </summary>
        private const string OwnerMissingBlockedReason = "EXTRACTOR CONTROL OFFLINE";
        private const int PlacementOverlapCapacity = 24;
        private const uint ExtractorOverflowDropWarningHash = 0x6DAE28B7u;
        private const uint ExtractorOverflowDropContextHash = 0xD9113EF2u;
        private const uint ExtractorOwnerMissingWarningHash = 0x5B1907C4u;
        private const uint ExtractorOwnerMissingRegisterContextHash = 0x9E42D7A6u;
        private const uint ExtractorOwnerMissingPlacementContextHash = 0xC30F81B5u;
        // COLD ALLOC: Collider[24] — placement/resource-node overlap buffer — owner: AutonomousExtractorModule
        private static readonly SpatialQueryHit[] PlacementSpatialBuffer = new SpatialQueryHit[PlacementOverlapCapacity];

        private static bool s_ownerMissingRegisterReported;
        private static bool s_ownerMissingPlacementReported;

        /// <summary>
        /// Clears cross-session static state. With "Enter Play Mode Options" domain reload disabled the
        /// latches below would survive into the next session and suppress the missing-owner report that is
        /// this type's only evidence of a dead owner, and the overlap buffer would keep managed
        /// <see cref="SpatialQueryHit.Owner"/> references to destroyed nodes past the entry the last query
        /// wrote. Same hook and same reason as DeepDrillModule.cs:32-39 in this folder.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_ownerMissingRegisterReported = false;
            s_ownerMissingPlacementReported = false;

            for (int i = 0; i < PlacementSpatialBuffer.Length; i++)
                PlacementSpatialBuffer[i] = default;
        }

        /// <summary>
        /// Publishes the release-audible evidence that <see cref="AutonomousExtractorSystem"/> has no live
        /// instance, at most once per call site per session.
        /// <para>
        /// The latches, the warning hash and both context hashes were authored for this report and the
        /// session reset above was written to keep it firing, but the publish itself was never wired: the
        /// three hashes had zero references and neither latch was ever set, so both missing-owner paths
        /// returned silently and the only evidence that this type has no construction route did not exist.
        /// </para>
        /// <para>
        /// The latch is load-bearing, not merely tidy. The placement site is reached from
        /// PlayerBuilder.cs:2136 on every ghost-preview evaluation while an extractor blueprint is held, so
        /// an unlatched publish would push a telemetry entry per frame. Latched, the steady-state cost is one
        /// static bool read and no allocation.
        /// <see cref="GlobalTelemetryBus.PublishPerformanceWarning"/> (Core/GlobalTelemetryBus.cs:365) is
        /// used because it carries no <c>[Conditional]</c> attribute and is therefore audible in a shipped
        /// build, unlike the H8Debug helpers.
        /// </para>
        /// </summary>
        /// <param name="contextHash">Call-site context hash distinguishing registration from placement.</param>
        /// <param name="reportedLatch">Per-call-site once-per-session latch, cleared by <see cref="ResetStaticState"/>.</param>
        private static void ReportOwnerMissing(uint contextHash, ref bool reportedLatch)
        {
            if (reportedLatch)
                return;

            reportedLatch = true;
            GlobalTelemetryBus.PublishPerformanceWarning(
                ExtractorOwnerMissingWarningHash,
                contextHash,
                1f);
        }

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
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryUnregister();
        }

        /// <inheritdoc />
        public void OnSpawn()
        {
            _hasPower = true;
            _debugHasPower = true;
            SetBoundNode(null);
            ApplyRuntimeTelemetry(0, 0, 0, false);
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
                ReportOwnerMissing(ExtractorOwnerMissingPlacementContextHash, ref s_ownerMissingPlacementReported);
                blockReason = OwnerMissingBlockedReason;
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
            {
                ReportOwnerMissing(ExtractorOwnerMissingRegisterContextHash, ref s_ownerMissingRegisterReported);
                return;
            }

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

                float distanceSqr = ResolveCandidateDistanceSq(in hit, candidate, hasQueryAup, in queryAup);
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
            in SpatialQueryHit hit,
            ResourceNode candidate,
            bool hasQueryAup,
            in AbsoluteUniversePosition queryAup)
        {
            if (hasQueryAup && IsFinite(in queryAup))
            {
                if (candidate != null &&
                    candidate.TryGetPersistentAup(out AbsoluteUniversePosition candidateAup))
                {
                    return SaturateDistanceSq(AbsoluteUniversePosition.DistanceSq(in candidateAup, in queryAup));
                }

                AbsoluteUniversePosition hitAup = hit.AbsolutePosition;
                if (hit.HasAbsolutePosition && IsFinite(in hitAup))
                    return SaturateDistanceSq(AbsoluteUniversePosition.DistanceSq(in hitAup, in queryAup));
            }

            return math.isfinite(hit.DistanceSqr) && hit.DistanceSqr >= 0f ? hit.DistanceSqr : float.MaxValue;
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

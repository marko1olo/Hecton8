using System.Collections.Generic;
using Hecton8.Building;
using Hecton8.Core;
using Hecton8.Items;
using Hecton8.Power;
using Hecton8.Scavenging;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
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
    public sealed class AutonomousExtractorSystem : MonoBehaviour, ISlowTickable, ILateFrameTickable
    {
        private const int InitialModuleCapacity = 16;
        private const float SlowTickDeltaSeconds = 0.5f;

        private struct ExtractorJobInput
        {
            public float CycleTimerSeconds;
            public float CycleSeconds;
            public int BufferedUnitCount;
            public int BufferedUnitCapacity;
            public int ItemHashId;
            public byte IsActive;
        }

        private struct ExtractorJobResult
        {
            public float NextCycleTimerSeconds;
            public int NextBufferedUnitCount;
            public int BufferedItemHashId;
            public int CompletedCycleDelta;
            public byte IsOperating;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct AdvanceExtractionJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<ExtractorJobInput> Inputs;
            public NativeArray<ExtractorJobResult> Results;
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

        private static AutonomousExtractorSystem _instance;

        // COLD ALLOC: List<AutonomousExtractorModule>[InitialModuleCapacity] — managed runtime extractor registry — owner: AutonomousExtractorSystem
        private readonly List<AutonomousExtractorModule> _modules = new List<AutonomousExtractorModule>(InitialModuleCapacity);
        private NativeArray<ExtractorJobInput> _jobInputs;
        private NativeArray<ExtractorJobResult> _jobResults;
        private NativeArray<float> _cycleTimers;
        private NativeArray<int> _bufferedItemHashIds;
        private NativeArray<int> _bufferedUnitCounts;
        private NativeArray<int> _completedCycleCounts;
        private JobHandle _scheduledJobHandle;
        private bool _scheduledJobActive;
        private bool _slowTickRegistered;
        private bool _lateFrameRegistered;
        private bool _serviceRegistered;
        private int _scheduledModuleCount;

        /// <summary>Returns the current runtime owner when one exists.</summary>
        public static AutonomousExtractorSystem Instance => _instance;

        internal static bool TryGetActiveRuntime(out AutonomousExtractorSystem runtime)
        {
            runtime = _instance;
            return runtime != null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        /// <summary>
        /// Ensures a runtime extraction owner exists.
        /// </summary>
        public static AutonomousExtractorSystem EnsureRuntimeInstance()
        {
            AutonomousExtractorSystem registryRuntime = GlobalRegistry.AutonomousExtractors;
            if (registryRuntime != null)
                return registryRuntime;

            if (_instance != null)
                return _instance;

            GameObject runtimeRoot = new GameObject("[AutonomousExtractorSystem]"); // COLD ALLOC: GameObject[1] — runtime extractor SOA owner root — owner: AutonomousExtractorSystem
            return runtimeRoot.AddComponent<AutonomousExtractorSystem>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void OnEnable()
        {
            TryRegisterToGlobalRegistry();

            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_slowTickRegistered)
            {
                GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
                _slowTickRegistered = GlobalRegistry.SlowTickables.Contains(this);
            }

            if (!_lateFrameRegistered)
            {
                GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameRegistered = SystemDispatcher.GetLateFrameLane(PriorityLayer.Environment).Contains(this);
            }
        }

        private void OnDisable()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
            {
                _slowTickRegistered = false;
                _lateFrameRegistered = false;
                TryUnregisterFromGlobalRegistry();
                return;
            }

            if (_slowTickRegistered)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _slowTickRegistered = false;
            }

            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameRegistered = false;
            }

            TryUnregisterFromGlobalRegistry();
        }

        private void OnDestroy()
        {
            TryUnregisterFromGlobalRegistry();
            JobHandle teardownDependency = CancelScheduledJobForTeardown();
            DisposeNativeBuffers(teardownDependency);
            JobHandle.ScheduleBatchedJobs();

            if (_instance == this)
                _instance = null;
        }

        private void TryRegisterToGlobalRegistry()
        {
            if (_serviceRegistered || !Application.isPlaying || _instance != this)
                return;

            GlobalRegistry.RegisterAutonomousExtractorRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.AutonomousExtractors, this);
        }

        private void TryUnregisterFromGlobalRegistry()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterAutonomousExtractorRuntime(this);
            _serviceRegistered = false;
        }

        /// <summary>
        /// Schedules the extractor advancement Burst pass on the slow-tick lane.
        /// </summary>
        public void SlowTick()
        {
            if (_scheduledJobActive)
                return;

            CompactModuleList();
            int moduleCount = _modules.Count;
            if (moduleCount <= 0)
                return;

            EnsureNativeCapacity(moduleCount);
            for (int i = 0; i < moduleCount; i++)
            {
                AutonomousExtractorModule module = _modules[i];
                if (module == null)
                {
                    _jobInputs[i] = default;
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
                                _bufferedUnitCounts[i] < capacity;

                _jobInputs[i] = new ExtractorJobInput
                {
                    CycleTimerSeconds = _cycleTimers[i],
                    CycleSeconds = template != null ? template.ExtractorCycleSeconds : 1f,
                    BufferedUnitCount = _bufferedUnitCounts[i],
                    BufferedUnitCapacity = capacity,
                    ItemHashId = itemHashId,
                    IsActive = isActive ? (byte)1 : (byte)0
                };
            }

            AdvanceExtractionJob job = new AdvanceExtractionJob
            {
                Inputs = _jobInputs,
                Results = _jobResults,
                SlowTickDeltaSeconds = SlowTickDeltaSeconds
            };

            _scheduledModuleCount = moduleCount;
            _scheduledJobHandle = job.Schedule(moduleCount, 8);
            _scheduledJobActive = true;
        }

        /// <summary>
        /// Commits the last scheduled extractor pass during the end-of-frame swap window.
        /// </summary>
        public void LateFrameTick()
        {
            if (!_scheduledJobActive)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _scheduledJobHandle, forceComplete: false))
                return;

            for (int i = 0; i < _scheduledModuleCount; i++)
            {
                ExtractorJobResult result = _jobResults[i];
                _cycleTimers[i] = result.NextCycleTimerSeconds;
                _bufferedItemHashIds[i] = result.BufferedItemHashId;
                _completedCycleCounts[i] += result.CompletedCycleDelta;

                AutonomousExtractorModule module = i < _modules.Count ? _modules[i] : null;
                if (module == null)
                {
                    _cycleTimers[i] = 0f;
                    _bufferedItemHashIds[i] = 0;
                    _bufferedUnitCounts[i] = 0;
                    _completedCycleCounts[i] = 0;
                    continue;
                }

                int bufferedUnitCount = result.NextBufferedUnitCount;
                ResourceNode hostNode = module.BoundNode;
                ResourceNodeTemplate template = hostNode != null ? hostNode.ResourceTemplate : null;
                ItemData routedItem = template != null ? template.ExtractorYieldItem : null;
                if (bufferedUnitCount > 0 &&
                    routedItem != null &&
                    module.TryRouteBufferedOutput(routedItem, bufferedUnitCount, out int routedCount))
                {
                    bufferedUnitCount = math.max(0, bufferedUnitCount - routedCount);
                }

                _bufferedUnitCounts[i] = bufferedUnitCount;

                module.ApplyRuntimeTelemetry(
                    result.BufferedItemHashId,
                    bufferedUnitCount,
                    _completedCycleCounts[i],
                    result.IsOperating != 0);
            }

            _scheduledJobActive = false;
            _scheduledModuleCount = 0;
        }

        internal int RegisterModule(AutonomousExtractorModule module)
        {
            if (module == null)
                return -1;

            for (int i = 0; i < _modules.Count; i++)
            {
                if (ReferenceEquals(_modules[i], module))
                    return i;

                if (_modules[i] != null || (_scheduledJobActive && i < _scheduledModuleCount))
                    continue;

                _modules[i] = module;
                module.SetRuntimeIndex(i);
                return i;
            }

            int newIndex = _modules.Count;
            _modules.Add(module);
            module.SetRuntimeIndex(newIndex);
            return newIndex;
        }

        internal void UnregisterModule(AutonomousExtractorModule module)
        {
            if (module == null)
                return;

            int index = module.RuntimeIndex;
            if (index < 0 || index >= _modules.Count || !ReferenceEquals(_modules[index], module))
            {
                index = -1;
                for (int i = 0; i < _modules.Count; i++)
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

            if (_cycleTimers.IsCreated && index < _cycleTimers.Length)
            {
                if (_scheduledJobActive && index < _scheduledModuleCount)
                    return;

                _cycleTimers[index] = 0f;
                _bufferedItemHashIds[index] = 0;
                _bufferedUnitCounts[index] = 0;
                _completedCycleCounts[index] = 0;
            }
        }

        internal bool IsNodeClaimed(ResourceNode node, AutonomousExtractorModule requester)
        {
            if (node == null)
                return false;

            int moduleCount = _modules.Count;
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
            for (int i = _modules.Count - 1; i >= 0; i--)
            {
                if (_modules[i] != null)
                    continue;

                int lastIndex = _modules.Count - 1;
                while (lastIndex > i && _modules[lastIndex] == null)
                {
                    _modules.RemoveAt(lastIndex);
                    lastIndex--;
                }

                if (i >= _modules.Count)
                    continue;

                if (_modules[i] == null && _modules.Count - 1 > i)
                {
                    AutonomousExtractorModule movedModule = _modules[_modules.Count - 1];
                    _modules[i] = movedModule;
                    _modules.RemoveAt(_modules.Count - 1);
                    if (movedModule != null)
                        movedModule.SetRuntimeIndex(i);
                }
            }

            for (int i = _modules.Count - 1; i >= 0; i--)
            {
                if (_modules[i] != null)
                    break;

                _modules.RemoveAt(i);
            }
        }

        private void EnsureNativeCapacity(int requiredCount)
        {
            if (requiredCount <= 0)
                return;

            int currentCapacity = _jobInputs.IsCreated ? _jobInputs.Length : 0;
            if (currentCapacity >= requiredCount)
                return;

            int nextCapacity = math.max(requiredCount, math.max(InitialModuleCapacity, currentCapacity * 2));
            NativeArray<ExtractorJobInput> nextInputs = new NativeArray<ExtractorJobInput>(nextCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ExtractorJobInput>[capacity] — extractor Burst input lane — owner: AutonomousExtractorSystem
            NativeArray<ExtractorJobResult> nextResults = new NativeArray<ExtractorJobResult>(nextCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ExtractorJobResult>[capacity] — extractor Burst result lane — owner: AutonomousExtractorSystem
            NativeArray<float> nextCycleTimers = new NativeArray<float>(nextCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[capacity] — extractor cycle SOA timers — owner: AutonomousExtractorSystem
            NativeArray<int> nextItemHashIds = new NativeArray<int>(nextCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[capacity] — extractor SOA item hashes — owner: AutonomousExtractorSystem
            NativeArray<int> nextBufferedCounts = new NativeArray<int>(nextCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[capacity] — extractor SOA buffered counts — owner: AutonomousExtractorSystem
            NativeArray<int> nextCompletedCounts = new NativeArray<int>(nextCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[capacity] — extractor SOA completed-cycle counters — owner: AutonomousExtractorSystem

            for (int i = 0; i < currentCapacity; i++)
            {
                nextInputs[i] = _jobInputs[i];
                nextResults[i] = _jobResults[i];
                nextCycleTimers[i] = _cycleTimers[i];
                nextItemHashIds[i] = _bufferedItemHashIds[i];
                nextBufferedCounts[i] = _bufferedUnitCounts[i];
                nextCompletedCounts[i] = _completedCycleCounts[i];
            }

            DisposeNativeBuffers();
            _jobInputs = nextInputs;
            _jobResults = nextResults;
            _cycleTimers = nextCycleTimers;
            _bufferedItemHashIds = nextItemHashIds;
            _bufferedUnitCounts = nextBufferedCounts;
            _completedCycleCounts = nextCompletedCounts;
        }

        private JobHandle CancelScheduledJobForTeardown()
        {
            if (!_scheduledJobActive)
                return _scheduledJobHandle;

            JobHandle dependency = _scheduledJobHandle;
            _scheduledJobHandle = default;
            _scheduledJobActive = false;
            _scheduledModuleCount = 0;
            return dependency;
        }

        private JobHandle DisposeNativeBuffers()
        {
            return DisposeNativeBuffers(default);
        }

        private JobHandle DisposeNativeBuffers(JobHandle dependency)
        {
            JobHandle disposeHandle = dependency;

            if (_jobInputs.IsCreated)
            {
                disposeHandle = _jobInputs.Dispose(disposeHandle);
                _jobInputs = default;
            }

            if (_jobResults.IsCreated)
            {
                disposeHandle = _jobResults.Dispose(disposeHandle);
                _jobResults = default;
            }

            if (_cycleTimers.IsCreated)
            {
                disposeHandle = _cycleTimers.Dispose(disposeHandle);
                _cycleTimers = default;
            }

            if (_bufferedItemHashIds.IsCreated)
            {
                disposeHandle = _bufferedItemHashIds.Dispose(disposeHandle);
                _bufferedItemHashIds = default;
            }

            if (_bufferedUnitCounts.IsCreated)
            {
                disposeHandle = _bufferedUnitCounts.Dispose(disposeHandle);
                _bufferedUnitCounts = default;
            }

            if (_completedCycleCounts.IsCreated)
            {
                disposeHandle = _completedCycleCounts.Dispose(disposeHandle);
                _completedCycleCounts = default;
            }

            return disposeHandle;
        }
    }

    /// <summary>
    /// Player-placed extractor module that binds to one large autonomous resource vein and consumes grid power while the runtime system advances its inventory.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PowerNode))]
    [AddComponentMenu("Hecton8/Construction/Autonomous Extractor Module")]
    public sealed class AutonomousExtractorModule : MonoBehaviour, IPoolable, IPowerComponent, IBuildPlacementRule
    {
        private const string DefaultPlacementBlockedReason = "INFINITE VEIN REQUIRED";
        private const string DefaultClaimBlockedReason = "VEIN ALREADY CLAIMED";
        private const string DefaultNodeScaleBlockedReason = "VEIN TOO SMALL";
        private const int PlacementOverlapCapacity = 24;
        private const int ResourceNodeLookupCacheCapacity = PlacementOverlapCapacity;
        // COLD ALLOC: Collider[24] — placement/resource-node overlap buffer — owner: AutonomousExtractorModule
        private static readonly Collider[] PlacementOverlapBuffer = new Collider[PlacementOverlapCapacity];

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
            _powerNode = GetComponent<PowerNode>();
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

        /// <inheritdoc />
        public bool ValidatePlacement(Vector3 position, Quaternion rotation, out string blockReason)
        {
            AutonomousExtractorSystem runtime = AutonomousExtractorSystem.EnsureRuntimeInstance();
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

            if (TryResolveNearestValidNode(transform.position, bindingRefreshRadius, runtime, out ResourceNode node))
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

            PersistentWorldRegistry registry = GlobalRegistry.PersistentWorldRegistry;
            if (registry == null)
                return false;

            ResolveOutputPose(out Vector3 spawnPosition, out Vector3 velocityChange);
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

            AutonomousExtractorSystem runtime = AutonomousExtractorSystem.EnsureRuntimeInstance();
            if (runtime == null)
                return;

            runtime.RegisterModule(this);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            AutonomousExtractorSystem runtime = GlobalRegistry.AutonomousExtractors;
            if (runtime == null)
                AutonomousExtractorSystem.TryGetActiveRuntime(out runtime);
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
            node = null;
            float safeRadius = Mathf.Max(0.5f, probeRadius);
            int overlapCount = UnityEngine.Physics.OverlapSphereNonAlloc(
                position,
                safeRadius,
                PlacementOverlapBuffer,
                HectonLayerMasks.StrictInteractionLayerMask,
                QueryTriggerInteraction.Ignore);
            if (overlapCount <= 0)
                return false;

            float bestDistanceSqr = float.MaxValue;
            for (int i = 0; i < overlapCount; i++)
            {
                Collider collider = PlacementOverlapBuffer[i];
                PlacementOverlapBuffer[i] = null;
                if (collider == null)
                    continue;

                if (!TryResolveResourceNode(collider, out ResourceNode candidate))
                    continue;

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

                float distanceSqr = (candidate.transform.position - position).sqrMagnitude;
                if (distanceSqr >= bestDistanceSqr)
                    continue;

                bestDistanceSqr = distanceSqr;
                node = candidate;
            }

            return node != null;
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
                node = collider.GetComponentInParent<ResourceNode>();

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

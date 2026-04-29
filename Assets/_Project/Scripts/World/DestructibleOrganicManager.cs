using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Scavenging;
using Hecton8.SaveSystem;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Runtime owner for indirect-flora harvest health, destruction, debris, and yield routing.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-120)] // Manager order must stay ahead of gameplay consumers that read/wire destruction state.
    public sealed class DestructibleOrganicManager : MonoBehaviour, ITickable, ISlowTickable
    {
        private static DestructibleOrganicManager _activeRuntimeInstance;
        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 32)]
        private struct DestroyedOrganicEvent
        {
            public float3 Position;
            public float ToolPower;
            public uint InstanceUid;
            public int TemplateIndex;
            public int MaterialClassId;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 24)]
        internal struct ItemDropData
        {
            public float3 Position;
            public int ItemHashId;
            public ushort Quantity;
            public byte MaterialClassId;
            public byte Reserved0;
            public uint SourceInstanceUid;
        }

        [BurstCompile]
        private struct EntropyYieldJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<DestroyedOrganicEvent> Events;
            [ReadOnly] public NativeArray<HarvestableTemplate.RuntimeDescriptor> TemplateDescriptors;
            [ReadOnly] public NativeArray<HarvestableTemplate.LootRuntimeEntry> LootEntries;
            [WriteOnly] public NativeArray<ItemDropData> OutputDrops;
            public int EventCount;

            public void Execute(int index)
            {
                if (index >= EventCount ||
                    !Events.IsCreated ||
                    !TemplateDescriptors.IsCreated ||
                    !LootEntries.IsCreated ||
                    !OutputDrops.IsCreated)
                {
                    return;
                }

                DestroyedOrganicEvent organicEvent = Events[index];
                if (organicEvent.TemplateIndex < 0 || organicEvent.TemplateIndex >= TemplateDescriptors.Length)
                {
                    OutputDrops[index] = default;
                    return;
                }

                HarvestableTemplate.RuntimeDescriptor descriptor = TemplateDescriptors[organicEvent.TemplateIndex];
                if (descriptor.LootCount <= 0)
                {
                    OutputDrops[index] = default;
                    return;
                }

                int lootStart = math.max(0, descriptor.LootStartIndex);
                int lootCount = math.min(descriptor.LootCount, LootEntries.Length - lootStart);
                if (lootCount <= 0)
                {
                    OutputDrops[index] = default;
                    return;
                }

                uint rng = organicEvent.InstanceUid ^ (uint)descriptor.StableHashId ^ 0x9E3779B9u;
                int totalWeight = 0;
                for (int lootIndex = 0; lootIndex < lootCount; lootIndex++)
                    totalWeight += math.max(1, LootEntries[lootStart + lootIndex].Weight);

                if (totalWeight <= 0)
                {
                    OutputDrops[index] = default;
                    return;
                }

                int weightedPick = (int)(NextRandom01(ref rng) * totalWeight);
                int runningWeight = 0;
                HarvestableTemplate.LootRuntimeEntry resolvedLoot = LootEntries[lootStart];
                for (int lootIndex = 0; lootIndex < lootCount; lootIndex++)
                {
                    HarvestableTemplate.LootRuntimeEntry candidate = LootEntries[lootStart + lootIndex];
                    runningWeight += math.max(1, candidate.Weight);
                    if (weightedPick < runningWeight)
                    {
                        resolvedLoot = candidate;
                        break;
                    }
                }

                int minimumAmount = math.max(1, resolvedLoot.MinimumAmount);
                int maximumAmount = math.max(minimumAmount, resolvedLoot.MaximumAmount);
                int quantity = minimumAmount;
                if (maximumAmount > minimumAmount)
                {
                    float amount01 = NextRandom01(ref rng);
                    quantity = minimumAmount + (int)math.floor((maximumAmount - minimumAmount + 1) * amount01);
                    quantity = math.clamp(quantity, minimumAmount, maximumAmount);
                }

                OutputDrops[index] = new ItemDropData
                {
                    Position = organicEvent.Position,
                    ItemHashId = resolvedLoot.ItemHashId,
                    Quantity = (ushort)math.clamp(quantity, 1, ushort.MaxValue),
                    MaterialClassId = (byte)organicEvent.MaterialClassId,
                    Reserved0 = 0,
                    SourceInstanceUid = organicEvent.InstanceUid
                };
            }

            private static float NextRandom01(ref uint state)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return (state & 0x00FFFFFFu) * (1f / 16777215f);
            }
        }

        private const int DefaultTrackedDestroyedCapacity = 2048;
        private const int DefaultTrackedHealthCapacity = 4096;
        private const int DefaultPendingYieldCapacity = 128;
        private const int DefaultDropBufferCapacity = 256;
        private const float HiddenInstanceWorldY = -100000f;
        private const float MinimumSearchRadius = 0.8f;
        private const float KelpRadiusBias = 0.65f;
        private const float OrganicBurstVelocityScale = 3f;
        private const float OrganicWiltDurationSeconds = 0.85f;

        [Header("Runtime Wiring")]
        [SerializeField]
        [Tooltip("Authoritative indirect-flora bridge that owns the streamed native instance payloads.")]
        private HectonMapMagicVegetationBridge vegetationBridge;

        [SerializeField]
        [Tooltip("Optional flora interaction manager used to publish localized tool-impact bend bursts.")]
        private FloraInteractionManager floraInteractionManager;

        [Header("Templates")]
        [SerializeField]
        [Tooltip("Authored harvest templates resolved by material class.")]
        private HarvestableTemplate[] harvestTemplates;

        [Header("Debris")]
        [SerializeField]
        [Tooltip("Burst debris profile used for kelp-family destruction.")]
        private OrganicDebrisProfile kelpDebrisProfile;

        [SerializeField]
        [Tooltip("Burst debris profile used for coral-family destruction.")]
        private OrganicDebrisProfile coralDebrisProfile;

        [SerializeField]
        [Tooltip("Burst debris profile used for metallic outcrop destruction.")]
        private OrganicDebrisProfile titaniumDebrisProfile;

        [SerializeField]
        [Tooltip("Burst debris profile used for surface sargassum destruction.")]
        private OrganicDebrisProfile sargassumDebrisProfile;

        [Header("Harvest Query")]
        [SerializeField, Min(MinimumSearchRadius)]
        [Tooltip("Base world-space radius used when resolving a tool hit against the active indirect flora arrays.")]
        private float hitSearchRadius = 1.25f;

        [SerializeField, Min(0.05f)]
        [Tooltip("Extra world-space radius added when resolving tall kelp silhouettes.")]
        private float kelpHeightTolerance = 0.4f;

        [SerializeField, Min(0.05f)]
        [Tooltip("Radius of the published flora-interaction burst when a tool hits a harvestable instance.")]
        private float interactionBurstRadius = 1.4f;

        private NativeArray<uint> _surfaceInstanceUids;
        private NativeArray<uint> _underwaterInstanceUids;
        private NativeArray<byte> _surfaceMaterialClasses;
        private NativeArray<byte> _underwaterMaterialClasses;
        private NativeArray<Unity.Mathematics.half> _surfaceHealth;
        private NativeArray<Unity.Mathematics.half> _underwaterHealth;
        private NativeHashMap<uint, Unity.Mathematics.half> _healthByInstanceUid;
        private NativeHashMap<uint, byte> _destroyedByInstanceUid;
        private NativeHashMap<uint, float> _pendingWiltEndTimeByInstanceUid;
        private NativeList<PersistentWorldDeltaRecord> _destroyedFloraScratch;
        private NativeList<DestroyedOrganicEvent> _pendingYieldEvents;
        private NativeArray<DestroyedOrganicEvent> _yieldJobInput;
        private NativeArray<ItemDropData> _yieldJobOutput;
        private NativeArray<ItemDropData> _dropBuffer;
        private NativeArray<HarvestableTemplate.RuntimeDescriptor> _templateDescriptors;
        private NativeArray<HarvestableTemplate.LootRuntimeEntry> _lootEntries;
        private NativeArray<Vector3> _dropDebugScratch;
        private JobHandle _yieldJobHandle;
        private int _scheduledYieldCount;
        private int _surfaceRevision = -1;
        private int _underwaterRevision = -1;
        private int _surfaceCount;
        private int _underwaterCount;
        private int _dropHead;
        private int _dropTail;
        private int _dropCount;
        private bool _tickRegistered;
        private bool _slowTickRegistered;
        private bool _yieldScheduled;

        private NativeArray<Matrix4x4> _surfaceMatrices;
        private NativeArray<HectonVegetationInstanceData> _surfaceMetadata;
        private NativeArray<int> _surfaceTypes;
        private NativeArray<int> _surfaceSemanticTypes;
        private NativeArray<Matrix4x4> _underwaterMatrices;
        private NativeArray<HectonVegetationInstanceData> _underwaterMetadata;
        private NativeArray<int> _underwaterTypes;
        private NativeArray<int> _underwaterSemanticTypes;

        private int[] _templateIndexByMaterialClass;

        /// <summary>Currently enabled runtime organic entropy owner.</summary>
        public static DestructibleOrganicManager ActiveRuntimeInstance => _activeRuntimeInstance;

        private void Awake()
        {
            _activeRuntimeInstance = this;

            if (vegetationBridge == null)
                vegetationBridge = GetComponent<HectonMapMagicVegetationBridge>();

            if (floraInteractionManager == null)
                floraInteractionManager = GetComponent<FloraInteractionManager>();

            hitSearchRadius = Mathf.Max(MinimumSearchRadius, hitSearchRadius);
            kelpHeightTolerance = Mathf.Max(0.05f, kelpHeightTolerance);
            interactionBurstRadius = Mathf.Max(0.05f, interactionBurstRadius);

            // COLD ALLOC: NativeHashMap<uint,half>[4096] - persistent per-instance harvest health state keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _healthByInstanceUid = new NativeHashMap<uint, Unity.Mathematics.half>(DefaultTrackedHealthCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeHashMap<uint,byte>[2048] - persistent destroyed flora tombstone set keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _destroyedByInstanceUid = new NativeHashMap<uint, byte>(DefaultTrackedDestroyedCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeHashMap<uint,float>[2048] - active wilt-to-hide timers keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _pendingWiltEndTimeByInstanceUid = new NativeHashMap<uint, float>(DefaultTrackedDestroyedCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeList<PersistentWorldDeltaRecord>[2048] - destroyed flora tombstone restore scratch - owner: DestructibleOrganicManager
            _destroyedFloraScratch = new NativeList<PersistentWorldDeltaRecord>(DefaultTrackedDestroyedCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeList<DestroyedOrganicEvent>[128] - pending entropy yield event queue - owner: DestructibleOrganicManager
            _pendingYieldEvents = new NativeList<DestroyedOrganicEvent>(DefaultPendingYieldCapacity, Allocator.Persistent);
            // COLD ALLOC: ItemDropData[256] - fixed-capacity organic drop ring buffer - owner: DestructibleOrganicManager
            _dropBuffer = new NativeArray<ItemDropData>(DefaultDropBufferCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: Vector3[1] - bounded debug scratch for future runtime diagnostics - owner: DestructibleOrganicManager
            _dropDebugScratch = new NativeArray<Vector3>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            BuildTemplateCaches();
        }

        private void OnEnable()
        {
            if (!_tickRegistered)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
                _tickRegistered = true;
            }

            if (!_slowTickRegistered)
            {
                GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
                _slowTickRegistered = true;
            }

            SyncDestroyedFloraFromPersistence();
            RefreshActiveCachesIfNeeded(force: true);
        }

        private void OnDisable()
        {
            if (_tickRegistered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _tickRegistered = false;
            }

            if (_slowTickRegistered)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _slowTickRegistered = false;
            }

            CompleteYieldJobIfNeeded();
        }

        private void OnDestroy()
        {
            if (_activeRuntimeInstance == this)
                _activeRuntimeInstance = null;

            CompleteYieldJobIfNeeded();
            DisposeNativeArray(ref _surfaceInstanceUids);
            DisposeNativeArray(ref _underwaterInstanceUids);
            DisposeNativeArray(ref _surfaceMaterialClasses);
            DisposeNativeArray(ref _underwaterMaterialClasses);
            DisposeNativeArray(ref _surfaceHealth);
            DisposeNativeArray(ref _underwaterHealth);
            DisposeNativeArray(ref _yieldJobInput);
            DisposeNativeArray(ref _yieldJobOutput);
            DisposeNativeArray(ref _dropBuffer);
            DisposeNativeArray(ref _templateDescriptors);
            DisposeNativeArray(ref _lootEntries);
            DisposeNativeArray(ref _dropDebugScratch);

            if (_healthByInstanceUid.IsCreated)
                _healthByInstanceUid.Dispose();

            if (_destroyedByInstanceUid.IsCreated)
                _destroyedByInstanceUid.Dispose();

            if (_pendingWiltEndTimeByInstanceUid.IsCreated)
                _pendingWiltEndTimeByInstanceUid.Dispose();

            if (_destroyedFloraScratch.IsCreated)
                _destroyedFloraScratch.Dispose();

            if (_pendingYieldEvents.IsCreated)
                _pendingYieldEvents.Dispose();
        }

        /// <summary>
        /// Processes pending entropy jobs and drop routing.
        /// </summary>
        public void Tick(float deltaTime)
        {
            RefreshActiveCachesIfNeeded(force: false);
            UpdateWiltInstances(Time.time);
            CompleteYieldJobIfNeeded();
            DrainDropBuffer();
            ScheduleYieldJobIfNeeded();
        }

        /// <summary>
        /// Restores destroyed flora tombstones from persistence and re-applies active suppression after world paging.
        /// </summary>
        public void SlowTick()
        {
            SyncDestroyedFloraFromPersistence();
            RefreshActiveCachesIfNeeded(force: true);
        }

        /// <summary>
        /// Applies one tool hit against the nearest active harvestable indirect-flora instance.
        /// </summary>
        public bool TryApplyToolHit(
            Vector3 hitPoint,
            Vector3 hitNormal,
            Vector3 direction,
            float deliveredDamage,
            float normalizedPower)
        {
            if (deliveredDamage <= 0f || vegetationBridge == null || _templateDescriptors.Length <= 0)
                return false;

            RefreshActiveCachesIfNeeded(force: false);
            if (!TryResolveNearestHarvestTarget(
                hitPoint,
                Mathf.Max(hitSearchRadius, interactionBurstRadius),
                out bool underwater,
                out int activeIndex,
                out uint instanceUid,
                out HarvestableTemplate.MaterialClass materialClass,
                out int templateIndex,
                out Matrix4x4 instanceMatrix,
                out Vector3 instancePosition))
            {
                return false;
            }

            float toolResistance = math.max(0.01f, _templateDescriptors[templateIndex].ToolResistance);
            float nextHealth = Mathf.Max(0f, GetLaneHealth(underwater, activeIndex) - (deliveredDamage / toolResistance));
            SetLaneHealth(underwater, activeIndex, nextHealth);
            _healthByInstanceUid.Remove(instanceUid);
            _healthByInstanceUid.TryAdd(instanceUid, (Unity.Mathematics.half)nextHealth);

            PublishExternalInteraction(hitPoint, direction * Mathf.Max(0.25f, normalizedPower * OrganicBurstVelocityScale), interactionBurstRadius);
            if (nextHealth > 0.0001f)
                return true;

            DestroyResolvedInstance(
                underwater,
                activeIndex,
                instanceUid,
                materialClass,
                templateIndex,
                instanceMatrix,
                instancePosition,
                hitPoint,
                hitNormal,
                normalizedPower);
            return true;
        }

        private void BuildTemplateCaches()
        {
            int materialClassCount = System.Enum.GetValues(typeof(HarvestableTemplate.MaterialClass)).Length;
            // COLD ALLOC: int[materialClassCount] - material-class to template-index lookup table - owner: DestructibleOrganicManager
            _templateIndexByMaterialClass = new int[materialClassCount];
            for (int i = 0; i < _templateIndexByMaterialClass.Length; i++)
                _templateIndexByMaterialClass[i] = -1;

            int validTemplateCount = 0;
            int totalLootEntries = 0;
            if (harvestTemplates != null)
            {
                for (int i = 0; i < harvestTemplates.Length; i++)
                {
                    HarvestableTemplate template = harvestTemplates[i];
                    if (template == null)
                        continue;

                    validTemplateCount++;
                    int materialIndex = (int)template.TemplateMaterialClass;
                    if ((uint)materialIndex < (uint)_templateIndexByMaterialClass.Length && _templateIndexByMaterialClass[materialIndex] < 0)
                        _templateIndexByMaterialClass[materialIndex] = i;
                }
            }

            if (harvestTemplates != null)
            {
                for (int i = 0; i < harvestTemplates.Length; i++)
                {
                    HarvestableTemplate template = harvestTemplates[i];
                    if (template == null)
                        continue;

                    totalLootEntries += CountTemplateLootEntries(template);
                }
            }

            DisposeNativeArray(ref _templateDescriptors);
            DisposeNativeArray(ref _lootEntries);
            _templateDescriptors = new NativeArray<HarvestableTemplate.RuntimeDescriptor>(
                math.max(1, validTemplateCount),
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: RuntimeDescriptor[templateCount] - compact harvest template runtime table - owner: DestructibleOrganicManager
            _lootEntries = new NativeArray<HarvestableTemplate.LootRuntimeEntry>(
                math.max(1, totalLootEntries),
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: LootRuntimeEntry[totalLootEntries] - flattened harvest loot runtime table - owner: DestructibleOrganicManager

            if (harvestTemplates == null)
                return;

            int descriptorWriteIndex = 0;
            int lootWriteIndex = 0;
            NativeList<HarvestableTemplate.LootRuntimeEntry> lootScratch =
                new NativeList<HarvestableTemplate.LootRuntimeEntry>(math.max(1, totalLootEntries), Allocator.Temp);
            try
            {
                for (int i = 0; i < harvestTemplates.Length; i++)
                {
                    HarvestableTemplate template = harvestTemplates[i];
                    if (template == null)
                        continue;

                    int lootStartIndex = lootWriteIndex;
                    lootScratch.Clear();
                    template.CopyLootTableNonAlloc(lootScratch);
                    for (int lootIndex = 0; lootIndex < lootScratch.Length && lootWriteIndex < _lootEntries.Length; lootIndex++)
                    {
                        _lootEntries[lootWriteIndex] = lootScratch[lootIndex];
                        lootWriteIndex++;
                    }

                    if (descriptorWriteIndex < _templateDescriptors.Length)
                        _templateDescriptors[descriptorWriteIndex++] = template.BuildRuntimeDescriptor(lootStartIndex);

                    int materialIndex = (int)template.TemplateMaterialClass;
                    if ((uint)materialIndex < (uint)_templateIndexByMaterialClass.Length)
                        _templateIndexByMaterialClass[materialIndex] = descriptorWriteIndex - 1;
                }
            }
            finally
            {
                if (lootScratch.IsCreated)
                    lootScratch.Dispose();
            }
        }

        private static int CountTemplateLootEntries(HarvestableTemplate template)
        {
            NativeList<HarvestableTemplate.LootRuntimeEntry> scratch =
                new NativeList<HarvestableTemplate.LootRuntimeEntry>(32, Allocator.Temp);
            try
            {
                return template != null ? template.CopyLootTableNonAlloc(scratch) : 0;
            }
            finally
            {
                if (scratch.IsCreated)
                    scratch.Dispose();
            }
        }

        private void RefreshActiveCachesIfNeeded(bool force)
        {
            if (vegetationBridge == null)
                vegetationBridge = GetComponent<HectonMapMagicVegetationBridge>();

            if (vegetationBridge == null)
                return;

            if (force || _surfaceRevision != vegetationBridge.ActiveSurfaceAggregateRevision)
                SyncLane(false);

            if (force || _underwaterRevision != vegetationBridge.ActiveUnderwaterAggregateRevision)
                SyncLane(true);
        }

        private void SyncLane(bool underwater)
        {
            NativeArray<Matrix4x4> matrices;
            NativeArray<HectonVegetationInstanceData> metadata;
            NativeArray<int> types;
            NativeArray<int> semanticTypes;
            int count;
            int semanticCount;
            bool hasNativePayload;
            bool hasSemanticPayload;
            if (underwater)
            {
                hasNativePayload = vegetationBridge.TryGetActiveUnderwaterNativePayload(out matrices, out metadata, out types, out count);
                hasSemanticPayload = vegetationBridge.TryGetActiveUnderwaterSemanticPayload(out semanticTypes, out _, out semanticCount);
            }
            else
            {
                hasNativePayload = vegetationBridge.TryGetActiveSurfaceNativePayload(out matrices, out metadata, out types, out count);
                hasSemanticPayload = vegetationBridge.TryGetActiveSurfaceSemanticPayload(out semanticTypes, out _, out semanticCount);
            }

            if (!hasNativePayload || !hasSemanticPayload || count <= 0 || semanticCount < count)
            {
                if (underwater)
                {
                    _underwaterMatrices = default;
                    _underwaterMetadata = default;
                    _underwaterTypes = default;
                    _underwaterSemanticTypes = default;
                    _underwaterCount = 0;
                    _underwaterRevision = vegetationBridge.ActiveUnderwaterAggregateRevision;
                }
                else
                {
                    _surfaceMatrices = default;
                    _surfaceMetadata = default;
                    _surfaceTypes = default;
                    _surfaceSemanticTypes = default;
                    _surfaceCount = 0;
                    _surfaceRevision = vegetationBridge.ActiveSurfaceAggregateRevision;
                }

                return;
            }

            NativeArray<uint> instanceUids = underwater ? EnsureLaneCapacity(ref _underwaterInstanceUids, count) : EnsureLaneCapacity(ref _surfaceInstanceUids, count);
            NativeArray<byte> materialClasses = underwater ? EnsureLaneCapacity(ref _underwaterMaterialClasses, count) : EnsureLaneCapacity(ref _surfaceMaterialClasses, count);
            NativeArray<Unity.Mathematics.half> health = underwater ? EnsureLaneCapacity(ref _underwaterHealth, count) : EnsureLaneCapacity(ref _surfaceHealth, count);

            for (int i = 0; i < count; i++)
            {
                uint instanceUid = ComputeStableInstanceUid(matrices[i], metadata[i], types[i], semanticTypes[i]);
                HarvestableTemplate.MaterialClass materialClass = ResolveMaterialClass(types[i], semanticTypes[i]);
                instanceUids[i] = instanceUid;
                materialClasses[i] = (byte)materialClass;

                int templateIndex = ResolveTemplateIndex(materialClass);
                float defaultHealth = templateIndex >= 0 ? _templateDescriptors[templateIndex].BaseHealth : 0f;
                float resolvedHealth = defaultHealth;
                bool isDestroyed = _destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(instanceUid);
                if (_healthByInstanceUid.IsCreated && _healthByInstanceUid.TryGetValue(instanceUid, out Unity.Mathematics.half savedHealth))
                    resolvedHealth = math.max(0f, (float)savedHealth);

                if (_healthByInstanceUid.IsCreated)
                {
                    _healthByInstanceUid.Remove(instanceUid);
                    _healthByInstanceUid.TryAdd(instanceUid, (Unity.Mathematics.half)resolvedHealth);
                }

                health[i] = (Unity.Mathematics.half)resolvedHealth;
                if (isDestroyed || resolvedHealth <= 0.0001f)
                {
                    if (_pendingWiltEndTimeByInstanceUid.IsCreated &&
                        _pendingWiltEndTimeByInstanceUid.TryGetValue(instanceUid, out float wiltEndTime) &&
                        wiltEndTime > Time.time)
                    {
                        ApplyWiltMetadata(ref metadata, i, wiltEndTime - OrganicWiltDurationSeconds);
                    }
                    else
                    {
                        SuppressActiveInstance(ref matrices, ref metadata, i);
                    }
                }
            }

            if (underwater)
            {
                _underwaterMatrices = matrices;
                _underwaterMetadata = metadata;
                _underwaterTypes = types;
                _underwaterSemanticTypes = semanticTypes;
                _underwaterCount = count;
                _underwaterRevision = vegetationBridge.ActiveUnderwaterAggregateRevision;
            }
            else
            {
                _surfaceMatrices = matrices;
                _surfaceMetadata = metadata;
                _surfaceTypes = types;
                _surfaceSemanticTypes = semanticTypes;
                _surfaceCount = count;
                _surfaceRevision = vegetationBridge.ActiveSurfaceAggregateRevision;
            }
        }

        private void SyncDestroyedFloraFromPersistence()
        {
            PersistentWorldRegistry registry = PersistentWorldRegistry.Instance;
            if (registry == null || !_destroyedFloraScratch.IsCreated || !_destroyedByInstanceUid.IsCreated)
                return;

            _destroyedFloraScratch.Clear();
            registry.CopyDestroyedFloraDeltas(_destroyedFloraScratch);
            for (int i = 0; i < _destroyedFloraScratch.Length; i++)
            {
                PersistentWorldDeltaRecord record = _destroyedFloraScratch[i];
                if (record.InstanceUid == 0u)
                    continue;

                _destroyedByInstanceUid.TryAdd(record.InstanceUid, 1);
                _healthByInstanceUid.Remove(record.InstanceUid);
                _healthByInstanceUid.TryAdd(record.InstanceUid, (Unity.Mathematics.half)0f);
            }
        }

        private void CompleteYieldJobIfNeeded()
        {
            if (!_yieldScheduled)
                return;

            _yieldJobHandle.Complete();
            _yieldScheduled = false;
            for (int i = 0; i < _scheduledYieldCount && i < _yieldJobOutput.Length; i++)
            {
                ItemDropData drop = _yieldJobOutput[i];
                if (drop.ItemHashId == 0 || drop.Quantity == 0)
                    continue;

                EnqueueDrop(drop);
            }

            _scheduledYieldCount = 0;
        }

        private void ScheduleYieldJobIfNeeded()
        {
            if (_yieldScheduled || !_pendingYieldEvents.IsCreated || _pendingYieldEvents.Length <= 0)
                return;

            int eventCount = _pendingYieldEvents.Length;
            EnsureNativeCapacity(ref _yieldJobInput, eventCount);
            EnsureNativeCapacity(ref _yieldJobOutput, eventCount);
            for (int i = 0; i < eventCount; i++)
            {
                _yieldJobInput[i] = _pendingYieldEvents[i];
                _yieldJobOutput[i] = default;
            }

            _pendingYieldEvents.Clear();
            _scheduledYieldCount = eventCount;
            _yieldJobHandle = new EntropyYieldJob
            {
                Events = _yieldJobInput,
                TemplateDescriptors = _templateDescriptors,
                LootEntries = _lootEntries,
                OutputDrops = _yieldJobOutput,
                EventCount = eventCount
            }.Schedule(eventCount, 8);
            _yieldScheduled = true;
        }

        private void DrainDropBuffer()
        {
            if (_dropCount <= 0 || !_dropBuffer.IsCreated)
                return;

            PlayerInventory playerInventory = PlayerInventory.Instance;
            Hecton8.SaveSystem.ItemCatalog itemCatalog = playerInventory != null ? playerInventory.ItemCatalog : null;
            PersistentWorldRegistry registry = PersistentWorldRegistry.Instance;
            while (_dropCount > 0)
            {
                ItemDropData drop = _dropBuffer[_dropHead];
                _dropHead = (_dropHead + 1) % _dropBuffer.Length;
                _dropCount--;
                if (drop.ItemHashId == 0 || drop.Quantity == 0)
                    continue;

                int rejectedQuantity = drop.Quantity;
                if (playerInventory != null)
                {
                    PlayerInventory.ScavengeAttemptResult result =
                        playerInventory.ScavengeAttempt(drop.ItemHashId, drop.Quantity, playerInventory.transform);
                    rejectedQuantity = result.RejectedQuantity;
                }

                if (rejectedQuantity > 0 && registry != null && itemCatalog != null)
                {
                    Vector3 runtimePosition = new Vector3(drop.Position.x, drop.Position.y, drop.Position.z);
                    registry.TryRegisterDroppedItem(drop.ItemHashId, itemCatalog, rejectedQuantity, runtimePosition);
                }
            }
        }

        private bool TryResolveNearestHarvestTarget(
            Vector3 hitPoint,
            float searchRadius,
            out bool underwater,
            out int activeIndex,
            out uint instanceUid,
            out HarvestableTemplate.MaterialClass materialClass,
            out int templateIndex,
            out Matrix4x4 instanceMatrix,
            out Vector3 instancePosition)
        {
            underwater = false;
            activeIndex = -1;
            instanceUid = 0u;
            materialClass = HarvestableTemplate.MaterialClass.None;
            templateIndex = -1;
            instanceMatrix = Matrix4x4.identity;
            instancePosition = Vector3.zero;

            float bestDistanceSq = float.MaxValue;
            if (TryResolveNearestHarvestTargetInLane(
                hitPoint,
                searchRadius,
                true,
                ref bestDistanceSq,
                ref activeIndex,
                ref instanceUid,
                ref materialClass,
                ref templateIndex,
                ref instanceMatrix,
                ref instancePosition))
            {
                underwater = true;
            }

            int surfaceIndex = -1;
            uint surfaceUid = 0u;
            HarvestableTemplate.MaterialClass surfaceMaterial = HarvestableTemplate.MaterialClass.None;
            int surfaceTemplateIndex = -1;
            Matrix4x4 surfaceMatrix = Matrix4x4.identity;
            Vector3 surfacePosition = Vector3.zero;
            if (TryResolveNearestHarvestTargetInLane(
                hitPoint,
                searchRadius,
                false,
                ref bestDistanceSq,
                ref surfaceIndex,
                ref surfaceUid,
                ref surfaceMaterial,
                ref surfaceTemplateIndex,
                ref surfaceMatrix,
                ref surfacePosition))
            {
                underwater = false;
                activeIndex = surfaceIndex;
                instanceUid = surfaceUid;
                materialClass = surfaceMaterial;
                templateIndex = surfaceTemplateIndex;
                instanceMatrix = surfaceMatrix;
                instancePosition = surfacePosition;
            }

            return activeIndex >= 0 && instanceUid != 0u && templateIndex >= 0;
        }

        private bool TryResolveNearestHarvestTargetInLane(
            Vector3 hitPoint,
            float searchRadius,
            bool underwater,
            ref float bestDistanceSq,
            ref int bestIndex,
            ref uint bestUid,
            ref HarvestableTemplate.MaterialClass bestMaterialClass,
            ref int bestTemplateIndex,
            ref Matrix4x4 bestMatrix,
            ref Vector3 bestPosition)
        {
            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            NativeArray<int> types = underwater ? _underwaterTypes : _surfaceTypes;
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<byte> materialClasses = underwater ? _underwaterMaterialClasses : _surfaceMaterialClasses;
            NativeArray<Unity.Mathematics.half> health = underwater ? _underwaterHealth : _surfaceHealth;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!matrices.IsCreated || !metadata.IsCreated || !types.IsCreated || !instanceUids.IsCreated || !materialClasses.IsCreated || !health.IsCreated || count <= 0)
                return false;

            float searchRadiusSq = searchRadius * searchRadius;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                uint instanceUid = instanceUids[i];
                if (instanceUid == 0u || (float)health[i] <= 0.0001f)
                    continue;

                HarvestableTemplate.MaterialClass materialClass = (HarvestableTemplate.MaterialClass)materialClasses[i];
                int templateIndex = ResolveTemplateIndex(materialClass);
                if (materialClass == HarvestableTemplate.MaterialClass.None || templateIndex < 0)
                    continue;

                Vector3 rootPosition = ExtractTranslation(matrices[i]);
                float distanceSq = ResolveHarvestDistanceSq(hitPoint, rootPosition, metadata[i], types[i], searchRadiusSq, kelpHeightTolerance);
                if (distanceSq > searchRadiusSq || distanceSq >= bestDistanceSq)
                    continue;

                found = true;
                bestDistanceSq = distanceSq;
                bestIndex = i;
                bestUid = instanceUid;
                bestMaterialClass = materialClass;
                bestTemplateIndex = templateIndex;
                bestMatrix = matrices[i];
                bestPosition = rootPosition;
            }

            return found;
        }

        private void DestroyResolvedInstance(
            bool underwater,
            int activeIndex,
            uint instanceUid,
            HarvestableTemplate.MaterialClass materialClass,
            int templateIndex,
            Matrix4x4 instanceMatrix,
            Vector3 instancePosition,
            Vector3 hitPoint,
            Vector3 hitNormal,
            float normalizedPower)
        {
            if (!_destroyedByInstanceUid.IsCreated)
                return;

            if (_destroyedByInstanceUid.ContainsKey(instanceUid))
                return;

            _destroyedByInstanceUid.TryAdd(instanceUid, 1);
            _healthByInstanceUid.Remove(instanceUid);
            _healthByInstanceUid.TryAdd(instanceUid, (Unity.Mathematics.half)0f);
            SetLaneHealth(underwater, activeIndex, 0f);
            float wiltEndTime = Time.time + OrganicWiltDurationSeconds;
            if (_pendingWiltEndTimeByInstanceUid.IsCreated)
            {
                _pendingWiltEndTimeByInstanceUid.Remove(instanceUid);
                _pendingWiltEndTimeByInstanceUid.TryAdd(instanceUid, wiltEndTime);
            }

            ApplyWiltToLaneInstance(underwater, activeIndex, wiltEndTime);

            PublishExternalInteraction(instancePosition, hitNormal.sqrMagnitude > 0.0001f ? hitNormal.normalized * (normalizedPower * OrganicBurstVelocityScale) : Vector3.up, interactionBurstRadius * 1.25f);
            SpawnDebris(materialClass, instanceMatrix, instancePosition, hitPoint, hitNormal, normalizedPower, instanceUid);
            QueueYieldEvent(instancePosition, normalizedPower, instanceUid, templateIndex, materialClass);

            PersistentWorldRegistry registry = PersistentWorldRegistry.Instance;
            if (registry != null && templateIndex >= 0 && templateIndex < _templateDescriptors.Length)
                registry.TryRegisterDestroyedFlora((ulong)(uint)_templateDescriptors[templateIndex].StableHashId, instanceUid, instancePosition);
        }

        private void SpawnDebris(
            HarvestableTemplate.MaterialClass materialClass,
            Matrix4x4 instanceMatrix,
            Vector3 instancePosition,
            Vector3 hitPoint,
            Vector3 hitNormal,
            float normalizedPower,
            uint instanceUid)
        {
            IDebrisService debrisService = GlobalRegistry.Debris;
            if (debrisService == null || !debrisService.IsInitialized)
                return;

            OrganicDebrisProfile profile = ResolveDebrisProfile(materialClass);
            if (profile == null || !profile.IsValid)
                return;

            Vector3 fallbackNormal = hitNormal.sqrMagnitude > 0.0001f ? hitNormal.normalized : Vector3.up;
            debrisService.SpawnBurst(
                profile,
                instancePosition,
                instanceMatrix.rotation,
                hitPoint,
                fallbackNormal,
                Mathf.Max(0.1f, normalizedPower),
                instanceUid ^ 0x7F4A7C15u);
        }

        private void QueueYieldEvent(
            Vector3 instancePosition,
            float normalizedPower,
            uint instanceUid,
            int templateIndex,
            HarvestableTemplate.MaterialClass materialClass)
        {
            if (!_pendingYieldEvents.IsCreated || _pendingYieldEvents.Length >= _pendingYieldEvents.Capacity)
                return;

            _pendingYieldEvents.AddNoResize(new DestroyedOrganicEvent
            {
                Position = new float3(instancePosition.x, instancePosition.y, instancePosition.z),
                ToolPower = Mathf.Max(0.1f, normalizedPower),
                InstanceUid = instanceUid,
                TemplateIndex = templateIndex,
                MaterialClassId = (int)materialClass
            });
        }

        private void PublishExternalInteraction(Vector3 positionWS, Vector3 velocityWS, float radius)
        {
            if (floraInteractionManager == null)
                floraInteractionManager = GetComponent<FloraInteractionManager>();

            floraInteractionManager?.RegisterExternalInteraction(positionWS, velocityWS, radius);
        }

        private OrganicDebrisProfile ResolveDebrisProfile(HarvestableTemplate.MaterialClass materialClass)
        {
            return materialClass switch
            {
                HarvestableTemplate.MaterialClass.Kelp => kelpDebrisProfile,
                HarvestableTemplate.MaterialClass.Coral => coralDebrisProfile,
                HarvestableTemplate.MaterialClass.TitaniumOutcrop => titaniumDebrisProfile,
                HarvestableTemplate.MaterialClass.Sargassum => sargassumDebrisProfile,
                _ => null
            };
        }

        private int ResolveTemplateIndex(HarvestableTemplate.MaterialClass materialClass)
        {
            int materialIndex = (int)materialClass;
            if (_templateIndexByMaterialClass == null || materialIndex < 0 || materialIndex >= _templateIndexByMaterialClass.Length)
                return -1;

            return _templateIndexByMaterialClass[materialIndex];
        }

        private float GetLaneHealth(bool underwater, int activeIndex)
        {
            NativeArray<Unity.Mathematics.half> laneHealth = underwater ? _underwaterHealth : _surfaceHealth;
            return laneHealth.IsCreated && (uint)activeIndex < (uint)laneHealth.Length ? (float)laneHealth[activeIndex] : 0f;
        }

        private void SetLaneHealth(bool underwater, int activeIndex, float value)
        {
            NativeArray<Unity.Mathematics.half> laneHealth = underwater ? _underwaterHealth : _surfaceHealth;
            if (!laneHealth.IsCreated || activeIndex < 0 || activeIndex >= laneHealth.Length)
                return;

            laneHealth[activeIndex] = (Unity.Mathematics.half)Mathf.Max(0f, value);
        }

        private void SuppressActiveInstance(bool underwater, int activeIndex)
        {
            if (underwater)
                SuppressActiveInstance(ref _underwaterMatrices, ref _underwaterMetadata, activeIndex);
            else
                SuppressActiveInstance(ref _surfaceMatrices, ref _surfaceMetadata, activeIndex);
        }

        private void ApplyWiltToLaneInstance(bool underwater, int activeIndex, float wiltEndTime)
        {
            float wiltStartTime = wiltEndTime - OrganicWiltDurationSeconds;
            if (underwater)
                ApplyWiltMetadata(ref _underwaterMetadata, activeIndex, wiltStartTime);
            else
                ApplyWiltMetadata(ref _surfaceMetadata, activeIndex, wiltStartTime);
        }

        private void UpdateWiltInstances(float currentTime)
        {
            if (!_pendingWiltEndTimeByInstanceUid.IsCreated || _pendingWiltEndTimeByInstanceUid.Count <= 0)
                return;

            UpdateWiltLane(false, currentTime);
            UpdateWiltLane(true, currentTime);
        }

        private void UpdateWiltLane(bool underwater, float currentTime)
        {
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!instanceUids.IsCreated || !metadata.IsCreated || count <= 0)
                return;

            for (int i = 0; i < count; i++)
            {
                uint instanceUid = instanceUids[i];
                if (instanceUid == 0u || !_pendingWiltEndTimeByInstanceUid.TryGetValue(instanceUid, out float wiltEndTime))
                    continue;

                if (currentTime >= wiltEndTime)
                {
                    SuppressActiveInstance(underwater, i);
                    _pendingWiltEndTimeByInstanceUid.Remove(instanceUid);
                    continue;
                }

                ApplyWiltMetadata(ref metadata, i, wiltEndTime - OrganicWiltDurationSeconds);
            }
        }

        private static void SuppressActiveInstance(
            ref NativeArray<Matrix4x4> matrices,
            ref NativeArray<HectonVegetationInstanceData> metadata,
            int activeIndex)
        {
            if (!matrices.IsCreated || !metadata.IsCreated || activeIndex < 0 || activeIndex >= matrices.Length || activeIndex >= metadata.Length)
                return;

            Matrix4x4 hiddenMatrix = matrices[activeIndex];
            hiddenMatrix.m03 = 0f;
            hiddenMatrix.m13 = HiddenInstanceWorldY;
            hiddenMatrix.m23 = 0f;
            matrices[activeIndex] = hiddenMatrix;

            HectonVegetationInstanceData hiddenMetadata = metadata[activeIndex];
            hiddenMetadata.Type = 0f;
            hiddenMetadata.HeightScale = 0f;
            hiddenMetadata.WidthScale = 0f;
            metadata[activeIndex] = hiddenMetadata;
        }

        private static void ApplyWiltMetadata(
            ref NativeArray<HectonVegetationInstanceData> metadata,
            int activeIndex,
            float wiltStartTime)
        {
            if (!metadata.IsCreated || activeIndex < 0 || activeIndex >= metadata.Length)
                return;

            HectonVegetationInstanceData wiltMetadata = metadata[activeIndex];
            wiltMetadata.HeightScale = -Mathf.Max(0.05f, Mathf.Abs(wiltMetadata.HeightScale));
            wiltMetadata.WidthScale = Mathf.Max(0.001f, wiltStartTime);
            metadata[activeIndex] = wiltMetadata;
        }

        private static HarvestableTemplate.MaterialClass ResolveMaterialClass(int typeId, int semanticType)
        {
            HectonVegetationInstanceType vegetationType = (HectonVegetationInstanceType)typeId;
            HectonMapMagicVegetationBridge.VegetationSemanticType semantic = (HectonMapMagicVegetationBridge.VegetationSemanticType)semanticType;
            if (vegetationType == HectonVegetationInstanceType.GiantKelp || semantic == HectonMapMagicVegetationBridge.VegetationSemanticType.OrganicKelp)
                return HarvestableTemplate.MaterialClass.Kelp;

            if (vegetationType == HectonVegetationInstanceType.Sargassum || semantic == HectonMapMagicVegetationBridge.VegetationSemanticType.FloatingSargassum)
                return HarvestableTemplate.MaterialClass.Sargassum;

            return HarvestableTemplate.MaterialClass.None;
        }

        private static float ResolveHarvestDistanceSq(
            Vector3 hitPoint,
            Vector3 rootPosition,
            HectonVegetationInstanceData metadata,
            int typeId,
            float fallbackDistanceSq,
            float heightTolerance)
        {
            HectonVegetationInstanceType vegetationType = (HectonVegetationInstanceType)typeId;
            if (vegetationType == HectonVegetationInstanceType.GiantKelp)
            {
                float kelpHeight = Mathf.Lerp(10f, 20f, Mathf.Clamp01(metadata.HeightScale));
                Vector3 top = rootPosition + Vector3.up * Mathf.Max(0.5f, kelpHeight + KelpRadiusBias);
                Vector3 closest = ClosestPointOnSegment(rootPosition, top, hitPoint);
                return (closest - hitPoint).sqrMagnitude;
            }

            return Mathf.Min((rootPosition - hitPoint).sqrMagnitude, fallbackDistanceSq + heightTolerance);
        }

        private static Vector3 ClosestPointOnSegment(Vector3 start, Vector3 end, Vector3 point)
        {
            Vector3 segment = end - start;
            float segmentLengthSq = segment.sqrMagnitude;
            if (segmentLengthSq <= 0.0001f)
                return start;

            float t = Mathf.Clamp01(Vector3.Dot(point - start, segment) / segmentLengthSq);
            return start + segment * t;
        }

        private static Vector3 ExtractTranslation(Matrix4x4 matrix)
        {
            return new Vector3(matrix.m03, matrix.m13, matrix.m23);
        }

        private static uint ComputeStableInstanceUid(
            Matrix4x4 matrix,
            HectonVegetationInstanceData metadata,
            int typeId,
            int semanticType)
        {
            int x = Mathf.RoundToInt(matrix.m03 * 100f);
            int y = Mathf.RoundToInt(matrix.m13 * 100f);
            int z = Mathf.RoundToInt(matrix.m23 * 100f);
            uint hx = (uint)x * 73856093u;
            uint hy = (uint)y * 19349663u;
            uint hz = (uint)z * 83492791u;
            uint hv = (uint)Mathf.RoundToInt(metadata.Variation * 10000f) * 2654435761u;
            uint hs = (uint)(semanticType + 1) * 2246822519u;
            uint ht = (uint)(typeId + 1) * 3266489917u;
            uint mixed = hx ^ hy ^ hz ^ hv ^ hs ^ ht;
            return mixed == 0u ? 1u : mixed;
        }

        private void EnqueueDrop(ItemDropData drop)
        {
            if (!_dropBuffer.IsCreated || _dropCount >= _dropBuffer.Length)
                return;

            _dropBuffer[_dropTail] = drop;
            _dropTail = (_dropTail + 1) % _dropBuffer.Length;
            _dropCount++;
        }

        private static NativeArray<T> EnsureLaneCapacity<T>(ref NativeArray<T> array, int requiredCount) where T : unmanaged
        {
            EnsureNativeCapacity(ref array, requiredCount);
            return array;
        }

        private static void EnsureNativeCapacity<T>(ref NativeArray<T> array, int requiredCount) where T : unmanaged
        {
            if (requiredCount <= 0)
                return;

            if (array.IsCreated && array.Length >= requiredCount)
                return;

            if (array.IsCreated)
                array.Dispose();

            array = new NativeArray<T>(requiredCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<T>[requiredCount] - resized persistent entropy runtime lane - owner: DestructibleOrganicManager
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            array.Dispose();
            array = default;
        }
    }
}

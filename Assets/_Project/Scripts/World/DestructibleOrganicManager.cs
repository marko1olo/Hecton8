using System.Runtime.InteropServices;
using System;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Interaction;
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

        private const int DefaultTrackedDestroyedCapacity = 2048;
        private const int DefaultTrackedHealthCapacity = 4096;
        private const int DefaultPendingYieldCapacity = 128;
        private const int DefaultDropBufferCapacity = 256;
        private const float HiddenInstanceWorldY = -100000f;
        private const float MinimumSearchRadius = 0.8f;
        private const float KelpRadiusBias = 0.65f;
        private const float OrganicBurstVelocityScale = 3f;
        private const float OrganicWiltDurationSeconds = 0.85f;
        private const float OrganicDecompositionDurationSeconds = 10f * 60f;
        private const float MinimumDecomposedHeightScale = 0.05f;
        private const float MinimumDecomposedWidthScale = 0.12f;
        private const byte FloraRuntimeFlagHasParasite = (byte)HectonVegetationRuntimeFlags.Parasite;

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
        private NativeHashMap<uint, float> _damageVisualProgressByInstanceUid;
        private NativeHashMap<uint, float> _decompositionStartTimeByInstanceUid;
        private NativeHashMap<uint, float> _regrowthProgressByInstanceUid;
        private NativeHashMap<uint, float3> _regrowthPositionByInstanceUid;
        private NativeHashMap<uint, float2> _baseScaleByInstanceUid;
        private NativeHashMap<uint, byte> _runtimeFlagsByInstanceUid;
        private NativeList<PersistentWorldDeltaRecord> _destroyedFloraScratch;
        private NativeList<PersistentWorldDeltaRecord> _floraStateOverrideScratch;
        private NativeHashMap<uint, Unity.Mathematics.half> _persistedHealth01ByInstanceUid;
        private NativeHashMap<uint, Unity.Mathematics.half> _persistedHeightScale01ByInstanceUid;
        private NativeList<DestroyedOrganicEvent> _pendingYieldEvents;
        private NativeArray<DestroyedOrganicEvent> _yieldJobInput;
        private DropBuffer _dropBuffer;
        private NativeArray<HarvestableTemplate.RuntimeDescriptor> _templateDescriptors;
        private NativeArray<HarvestableTemplate.LootRuntimeEntry> _lootEntries;
        private NativeArray<EntropyYieldMaterialLutEntry> _yieldMaterialLut;
        private NativeArray<Vector3> _dropDebugScratch;
        private JobHandle _yieldJobHandle;
        private int _scheduledYieldCount;
        private int _surfaceRevision = -1;
        private int _underwaterRevision = -1;
        private int _surfaceCount;
        private int _underwaterCount;
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
        private int[] _harvestDescriptorIndexByFloraTemplateIndex = Array.Empty<int>();
        private HarvestableTemplate[] _descriptorHarvestTemplates = Array.Empty<HarvestableTemplate>();

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
            // COLD ALLOC: NativeHashMap<uint,float>[2048] - persistent partial-damage wilt progress keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _damageVisualProgressByInstanceUid = new NativeHashMap<uint, float>(DefaultTrackedDestroyedCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeHashMap<uint,float>[2048] - persistent decomposition start time keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _decompositionStartTimeByInstanceUid = new NativeHashMap<uint, float>(DefaultTrackedDestroyedCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeHashMap<uint,float>[2048] - active flora regrowth progress keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _regrowthProgressByInstanceUid = new NativeHashMap<uint, float>(DefaultTrackedDestroyedCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeHashMap<uint,float3>[2048] - flora regrowth position overrides keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _regrowthPositionByInstanceUid = new NativeHashMap<uint, float3>(DefaultTrackedDestroyedCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeHashMap<uint,float2>[4096] - baseline height/width scales keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _baseScaleByInstanceUid = new NativeHashMap<uint, float2>(DefaultTrackedHealthCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeHashMap<uint,byte>[4096] - runtime flora bit-mask flags keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _runtimeFlagsByInstanceUid = new NativeHashMap<uint, byte>(DefaultTrackedHealthCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeList<PersistentWorldDeltaRecord>[2048] - destroyed flora tombstone restore scratch - owner: DestructibleOrganicManager
            _destroyedFloraScratch = new NativeList<PersistentWorldDeltaRecord>(DefaultTrackedDestroyedCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeList<PersistentWorldDeltaRecord>[4096] - partial flora-state restore scratch - owner: DestructibleOrganicManager
            _floraStateOverrideScratch = new NativeList<PersistentWorldDeltaRecord>(DefaultTrackedHealthCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeHashMap<uint,half>[4096] - persisted normalized flora health overrides keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _persistedHealth01ByInstanceUid = new NativeHashMap<uint, Unity.Mathematics.half>(DefaultTrackedHealthCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeHashMap<uint,half>[4096] - persisted normalized flora height overrides keyed by deterministic flora uid - owner: DestructibleOrganicManager
            _persistedHeightScale01ByInstanceUid = new NativeHashMap<uint, Unity.Mathematics.half>(DefaultTrackedHealthCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeList<DestroyedOrganicEvent>[128] - pending entropy yield event queue - owner: DestructibleOrganicManager
            _pendingYieldEvents = new NativeList<DestroyedOrganicEvent>(DefaultPendingYieldCapacity, Allocator.Persistent);
            _dropBuffer = new DropBuffer(DefaultDropBufferCapacity, Allocator.Persistent);
            // COLD ALLOC: Vector3[1] - bounded debug scratch for future runtime diagnostics - owner: DestructibleOrganicManager
            _dropDebugScratch = new NativeArray<Vector3>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            BuildTemplateCaches();
            BuildYieldMaterialLut();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

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
            SyncFloraStateOverridesFromPersistence();
            BuildFloraTemplateHarvestMap();
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
            DisposeNativeArray(ref _templateDescriptors);
            DisposeNativeArray(ref _lootEntries);
            DisposeNativeArray(ref _yieldMaterialLut);
            DisposeNativeArray(ref _dropDebugScratch);

            if (_dropBuffer.IsCreated)
                _dropBuffer.Dispose();

            if (_healthByInstanceUid.IsCreated)
                _healthByInstanceUid.Dispose();

            if (_destroyedByInstanceUid.IsCreated)
                _destroyedByInstanceUid.Dispose();

            if (_persistedHealth01ByInstanceUid.IsCreated)
                _persistedHealth01ByInstanceUid.Dispose();

            if (_persistedHeightScale01ByInstanceUid.IsCreated)
                _persistedHeightScale01ByInstanceUid.Dispose();

            if (_pendingWiltEndTimeByInstanceUid.IsCreated)
                _pendingWiltEndTimeByInstanceUid.Dispose();

            if (_damageVisualProgressByInstanceUid.IsCreated)
                _damageVisualProgressByInstanceUid.Dispose();

            if (_decompositionStartTimeByInstanceUid.IsCreated)
                _decompositionStartTimeByInstanceUid.Dispose();

            if (_regrowthProgressByInstanceUid.IsCreated)
                _regrowthProgressByInstanceUid.Dispose();

            if (_regrowthPositionByInstanceUid.IsCreated)
                _regrowthPositionByInstanceUid.Dispose();

            if (_baseScaleByInstanceUid.IsCreated)
                _baseScaleByInstanceUid.Dispose();

            if (_runtimeFlagsByInstanceUid.IsCreated)
                _runtimeFlagsByInstanceUid.Dispose();

            if (_destroyedFloraScratch.IsCreated)
                _destroyedFloraScratch.Dispose();

            if (_floraStateOverrideScratch.IsCreated)
                _floraStateOverrideScratch.Dispose();

            if (_pendingYieldEvents.IsCreated)
                _pendingYieldEvents.Dispose();
        }

        /// <summary>
        /// Processes pending entropy jobs and drop routing.
        /// </summary>
        public void Tick(float deltaTime)
        {
            VoxelDynamicNavGridRuntime.CompletePendingDynamicObstacleUpdates();
            RefreshActiveCachesIfNeeded(force: false);
            UpdateDecompositionVisuals(Time.time);
            UpdateRegrowthVisuals();
            UpdateDamageVisuals(Time.time);
            UpdateWiltInstances(Time.time);
            CompleteYieldJobIfNeeded();
            DrainDropBuffer();
            VoxelDynamicNavGridRuntime.EnqueueDestroyedOrganicEvents(_pendingYieldEvents);
            ScheduleYieldJobIfNeeded();
            VoxelDynamicNavGridRuntime.SchedulePendingDynamicObstacleUpdates();
        }

        /// <summary>
        /// Restores destroyed flora tombstones from persistence and re-applies active suppression after world paging.
        /// </summary>
        public void SlowTick()
        {
            SyncDestroyedFloraFromPersistence();
            SyncFloraStateOverridesFromPersistence();
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
            float normalizedPower,
            uint toolCapabilityMask)
        {
            if (deliveredDamage <= 0f || vegetationBridge == null || _templateDescriptors.Length <= 0)
                return false;

            RefreshActiveCachesIfNeeded(force: false);
            if (!TryResolveNearestHarvestTarget(
                hitPoint,
                Mathf.Max(hitSearchRadius, interactionBurstRadius),
                toolCapabilityMask,
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

            if (_regrowthProgressByInstanceUid.IsCreated && _regrowthProgressByInstanceUid.ContainsKey(instanceUid))
                return false;

            float baseHealth = Mathf.Max(0.1f, _templateDescriptors[templateIndex].BaseHealth);
            float toolResistance = math.max(0.01f, _templateDescriptors[templateIndex].ToolResistance);
            float nextHealth = Mathf.Max(0f, GetLaneHealth(underwater, activeIndex) - (deliveredDamage / toolResistance));
            SetLaneHealth(underwater, activeIndex, nextHealth);
            _healthByInstanceUid.Remove(instanceUid);
            _healthByInstanceUid.TryAdd(instanceUid, (Unity.Mathematics.half)nextHealth);

            PublishExternalInteraction(hitPoint, direction * Mathf.Max(0.25f, normalizedPower * OrganicBurstVelocityScale), interactionBurstRadius);
            ApplyDamageVisualState(instanceUid, underwater, activeIndex, baseHealth, nextHealth, Time.time);
            if (nextHealth > 0.0001f)
            {
                PersistFloraStateOverride(instanceUid, templateIndex, instancePosition, underwater, activeIndex, baseHealth, nextHealth);
                return true;
            }

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

        /// <summary>
        /// Applies non-harvest decomposition to any active indirect flora intersecting a newly placed construction envelope.
        /// </summary>
        internal int ApplyConstructionDecomposition(Vector3 runtimePosition, float radiusMeters)
        {
            if (radiusMeters <= 0f)
                return 0;

            if (vegetationBridge == null)
                vegetationBridge = GetComponent<HectonMapMagicVegetationBridge>();

            if (vegetationBridge == null)
                return 0;

            RefreshActiveCachesIfNeeded(force: false);
            Vector3 universePosition = HectonMapMagicVegetationBridge.ToUniverseSpace(runtimePosition);
            float radiusSq = radiusMeters * radiusMeters;
            int decomposedCount = 0;
            decomposedCount += ApplyConstructionDecompositionInLane(false, universePosition, radiusSq);
            decomposedCount += ApplyConstructionDecompositionInLane(true, universePosition, radiusSq);
            return decomposedCount;
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
            _descriptorHarvestTemplates = new HarvestableTemplate[math.max(1, validTemplateCount)]; // COLD ALLOC: HarvestableTemplate[templateCount] - descriptor-to-authoring lookup for flora template harvest routing - owner: DestructibleOrganicManager

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
                    {
                        _templateDescriptors[descriptorWriteIndex] = template.BuildRuntimeDescriptor(lootStartIndex);
                        _descriptorHarvestTemplates[descriptorWriteIndex] = template;
                        descriptorWriteIndex++;
                    }

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

            BuildFloraTemplateHarvestMap();
        }

        private int ApplyConstructionDecompositionInLane(bool underwater, Vector3 centerUniversePosition, float radiusSq)
        {
            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            NativeArray<int> types = underwater ? _underwaterTypes : _surfaceTypes;
            NativeArray<int> semanticTypes = underwater ? _underwaterSemanticTypes : _surfaceSemanticTypes;
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<byte> materialClasses = underwater ? _underwaterMaterialClasses : _surfaceMaterialClasses;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!matrices.IsCreated ||
                !metadata.IsCreated ||
                !types.IsCreated ||
                !semanticTypes.IsCreated ||
                !instanceUids.IsCreated ||
                !materialClasses.IsCreated ||
                count <= 0)
            {
                return 0;
            }

            int decomposedCount = 0;
            int safeCount = math.min(
                count,
                math.min(
                    math.min(matrices.Length, metadata.Length),
                    math.min(
                        math.min(types.Length, semanticTypes.Length),
                        math.min(instanceUids.Length, materialClasses.Length))));
            for (int i = 0; i < safeCount; i++)
            {
                uint instanceUid = instanceUids[i];
                if (instanceUid == 0u)
                    continue;

                HarvestableTemplate.MaterialClass materialClass = (HarvestableTemplate.MaterialClass)materialClasses[i];
                if (materialClass == HarvestableTemplate.MaterialClass.None)
                    continue;

                if (_destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(instanceUid))
                    continue;

                Vector3 rootPosition = ExtractTranslation(matrices[i]);
                float distanceSq = ResolveConstructionDistanceSq(centerUniversePosition, rootPosition, metadata[i], types[i]);
                if (distanceSq > radiusSq)
                    continue;

                int templateIndex = ResolveTemplateIndex(metadata[i], materialClass);
                ApplyPassiveDecomposition(underwater, i, instanceUid, materialClass, templateIndex, rootPosition);
                decomposedCount++;
            }

            return decomposedCount;
        }

        private void BuildFloraTemplateHarvestMap()
        {
            if (vegetationBridge == null)
                vegetationBridge = GetComponent<HectonMapMagicVegetationBridge>();

            FloraDataTemplate[] floraTemplateAssets = vegetationBridge != null ? vegetationBridge.FloraTemplates : null;
            if (floraTemplateAssets == null || floraTemplateAssets.Length == 0)
            {
                _harvestDescriptorIndexByFloraTemplateIndex = Array.Empty<int>();
                return;
            }

            // COLD ALLOC: int[floraTemplateAssets.Length] - flora-template to harvest-descriptor lookup for instance-specific loot routing - owner: DestructibleOrganicManager
            int[] mapping = new int[floraTemplateAssets.Length];
            for (int i = 0; i < mapping.Length; i++)
                mapping[i] = -1;

            for (int i = 0; i < floraTemplateAssets.Length; i++)
            {
                FloraDataTemplate floraTemplate = floraTemplateAssets[i];
                HarvestableTemplate harvestTemplate = floraTemplate != null ? floraTemplate.HarvestTemplate : null;
                if (harvestTemplate == null || _descriptorHarvestTemplates == null)
                    continue;

                for (int descriptorIndex = 0; descriptorIndex < _descriptorHarvestTemplates.Length; descriptorIndex++)
                {
                    if (_descriptorHarvestTemplates[descriptorIndex] != harvestTemplate)
                        continue;

                    mapping[i] = descriptorIndex;
                    break;
                }
            }

            _harvestDescriptorIndexByFloraTemplateIndex = mapping;
        }

        private void BuildYieldMaterialLut()
        {
            int materialClassCount = System.Enum.GetValues(typeof(HarvestableTemplate.MaterialClass)).Length;
            DisposeNativeArray(ref _yieldMaterialLut);
            _yieldMaterialLut = new NativeArray<EntropyYieldMaterialLutEntry>(
                math.max(1, materialClassCount),
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: EntropyYieldMaterialLutEntry[materialClassCount] - deterministic density/unit-mass lookup for burst flora yield - owner: DestructibleOrganicManager

            WriteYieldMaterialLut(HarvestableTemplate.MaterialClass.None, 1000f, 1f, 0.5f, 0f);
            WriteYieldMaterialLut(HarvestableTemplate.MaterialClass.Kelp, 460f, 1.2f, 0.58f, 0.08f);
            WriteYieldMaterialLut(HarvestableTemplate.MaterialClass.Coral, 1320f, 2.5f, 0.65f, 0.16f);
            WriteYieldMaterialLut(HarvestableTemplate.MaterialClass.TitaniumOutcrop, 4480f, 4.5f, 0.78f, 0.22f);
            WriteYieldMaterialLut(HarvestableTemplate.MaterialClass.Sargassum, 310f, 1.0f, 0.52f, 0.05f);
        }

        private void WriteYieldMaterialLut(
            HarvestableTemplate.MaterialClass materialClass,
            float densityKgPerM3,
            float unitItemMassKg,
            float minimumRecovery,
            float qualityBias)
        {
            if (!_yieldMaterialLut.IsCreated)
                return;

            int materialIndex = (int)materialClass;
            if (materialIndex < 0 || materialIndex >= _yieldMaterialLut.Length)
                return;

            _yieldMaterialLut[materialIndex] = new EntropyYieldMaterialLutEntry
            {
                DensityKgPerM3 = Mathf.Max(0.01f, densityKgPerM3),
                UnitItemMassKg = Mathf.Max(0.01f, unitItemMassKg),
                MinimumRecovery = Mathf.Clamp01(minimumRecovery),
                QualityBias = Mathf.Clamp01(qualityBias)
            };
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
            float currentTime = Time.time;

            for (int i = 0; i < count; i++)
            {
                uint instanceUid = ComputeStableInstanceUid(matrices[i], metadata[i], types[i], semanticTypes[i]);
                HarvestableTemplate.MaterialClass fallbackMaterialClass = ResolveMaterialClass(types[i], semanticTypes[i]);
                int templateIndex = ResolveTemplateIndex(metadata[i], fallbackMaterialClass);
                HarvestableTemplate.MaterialClass materialClass = templateIndex >= 0 && templateIndex < _templateDescriptors.Length
                    ? (HarvestableTemplate.MaterialClass)_templateDescriptors[templateIndex].MaterialClassId
                    : fallbackMaterialClass;
                instanceUids[i] = instanceUid;
                materialClasses[i] = (byte)materialClass;
                CacheBaseScale(instanceUid, metadata[i]);
                byte runtimeFlags = ResolveRuntimeFlags(instanceUid, materialClass, semanticTypes[i], metadata[i].RuntimeFlags);
                ApplyRuntimeFlags(ref metadata, i, runtimeFlags);
                SetRuntimeState(ref metadata, i, HectonVegetationInstanceData.RuntimeStateIdle);
                float defaultHealth = templateIndex >= 0 ? _templateDescriptors[templateIndex].BaseHealth : 0f;
                float resolvedHealth = defaultHealth;
                bool hasPersistedFloraState = TryResolvePersistedFloraState(instanceUid, out float persistedHealth01, out float persistedHeightScale01);
                bool isDestroyed = _destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(instanceUid);
                float regrowthProgress = 0f;
                bool isRegrowing = _regrowthProgressByInstanceUid.IsCreated &&
                                   _regrowthProgressByInstanceUid.TryGetValue(instanceUid, out regrowthProgress);
                if (_healthByInstanceUid.IsCreated && _healthByInstanceUid.TryGetValue(instanceUid, out Unity.Mathematics.half savedHealth))
                    resolvedHealth = math.max(0f, (float)savedHealth);
                else if (hasPersistedFloraState && templateIndex >= 0)
                    resolvedHealth = Mathf.Max(0f, defaultHealth * Mathf.Clamp01(persistedHealth01));

                if (_healthByInstanceUid.IsCreated)
                {
                    _healthByInstanceUid.Remove(instanceUid);
                    _healthByInstanceUid.TryAdd(instanceUid, (Unity.Mathematics.half)resolvedHealth);
                }

                health[i] = (Unity.Mathematics.half)resolvedHealth;
                if (isRegrowing)
                {
                    if (_damageVisualProgressByInstanceUid.IsCreated)
                        _damageVisualProgressByInstanceUid.Remove(instanceUid);

                    ApplyRegrowthVisualToLaneInstance(underwater, i, instanceUid, regrowthProgress);
                }
                else if (isDestroyed || resolvedHealth <= 0.0001f)
                {
                    if (_damageVisualProgressByInstanceUid.IsCreated)
                        _damageVisualProgressByInstanceUid.Remove(instanceUid);
                    float entropy01 = ResolveOrPrimeDecompositionProgress(instanceUid, currentTime);
                    ApplyDecompositionMetadata(ref metadata, i, instanceUid, entropy01);
                }
                else if (templateIndex >= 0 && resolvedHealth < defaultHealth)
                {
                    float damage01 = ResolveDamageProgress(defaultHealth, resolvedHealth);
                    UpdateDamageProgressCache(instanceUid, damage01);
                    float normalizedHeightScale = hasPersistedFloraState
                        ? Mathf.Clamp01(persistedHeightScale01)
                        : ResolveNormalizedHeightScale(defaultHealth, resolvedHealth);
                    ApplyPersistedDamageMetadata(ref metadata, i, instanceUid, normalizedHeightScale, damage01, currentTime);
                }
                else if (_damageVisualProgressByInstanceUid.IsCreated)
                {
                    _damageVisualProgressByInstanceUid.Remove(instanceUid);
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

            _destroyedByInstanceUid.Clear();
            _destroyedFloraScratch.Clear();
            registry.CopyDestroyedFloraDeltas(_destroyedFloraScratch);
            for (int i = 0; i < _destroyedFloraScratch.Length; i++)
            {
                PersistentWorldDeltaRecord record = _destroyedFloraScratch[i];
                if (record.InstanceUid == 0u)
                    continue;

                if (_regrowthProgressByInstanceUid.IsCreated && _regrowthProgressByInstanceUid.ContainsKey(record.InstanceUid))
                    continue;

                _destroyedByInstanceUid.TryAdd(record.InstanceUid, 1);
                PrimeDecompositionState(record.InstanceUid, Time.time - OrganicDecompositionDurationSeconds);
                _healthByInstanceUid.Remove(record.InstanceUid);
                _healthByInstanceUid.TryAdd(record.InstanceUid, (Unity.Mathematics.half)0f);
            }
        }

        private void SyncFloraStateOverridesFromPersistence()
        {
            PersistentWorldRegistry registry = PersistentWorldRegistry.Instance;
            if (registry == null ||
                !_floraStateOverrideScratch.IsCreated ||
                !_persistedHealth01ByInstanceUid.IsCreated ||
                !_persistedHeightScale01ByInstanceUid.IsCreated)
            {
                return;
            }

            _floraStateOverrideScratch.Clear();
            _persistedHealth01ByInstanceUid.Clear();
            _persistedHeightScale01ByInstanceUid.Clear();
            registry.CopyFloraStateOverrideDeltas(_floraStateOverrideScratch);
            for (int i = 0; i < _floraStateOverrideScratch.Length; i++)
            {
                PersistentWorldDeltaRecord record = _floraStateOverrideScratch[i];
                if (record.InstanceUid == 0u)
                    continue;

                if ((_destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(record.InstanceUid)) ||
                    (_regrowthProgressByInstanceUid.IsCreated && _regrowthProgressByInstanceUid.ContainsKey(record.InstanceUid)))
                {
                    continue;
                }

                PersistentWorldRegistry.UnpackFloraStateOverride(record.Quantity, out float persistedHealth01, out float persistedHeightScale01);
                _persistedHealth01ByInstanceUid.TryAdd(record.InstanceUid, (Unity.Mathematics.half)Mathf.Clamp01(persistedHealth01));
                _persistedHeightScale01ByInstanceUid.TryAdd(record.InstanceUid, (Unity.Mathematics.half)Mathf.Clamp01(persistedHeightScale01));
            }
        }

        private void CompleteYieldJobIfNeeded()
        {
            if (!_yieldScheduled)
                return;

            _yieldJobHandle.Complete();
            _yieldScheduled = false;
            _scheduledYieldCount = 0;
        }

        private void ScheduleYieldJobIfNeeded()
        {
            if (_yieldScheduled ||
                !_pendingYieldEvents.IsCreated ||
                _pendingYieldEvents.Length <= 0 ||
                !_dropBuffer.IsCreated ||
                !_yieldMaterialLut.IsCreated)
            return;

            int eventCount = math.min(_pendingYieldEvents.Length, _dropBuffer.Capacity);
            EnsureNativeCapacity(ref _yieldJobInput, eventCount);
            for (int i = 0; i < eventCount; i++)
            {
                _yieldJobInput[i] = _pendingYieldEvents[i];
            }

            _pendingYieldEvents.Clear();
            _scheduledYieldCount = eventCount;
            _yieldJobHandle = new EntropyYieldJob
            {
                Events = _yieldJobInput,
                TemplateDescriptors = _templateDescriptors,
                LootEntries = _lootEntries,
                MaterialLut = _yieldMaterialLut,
                DropWriter = _dropBuffer.AsParallelWriter(),
                EventCount = eventCount
            }.Schedule(eventCount, 8);
            _yieldScheduled = true;
        }

        private void DrainDropBuffer()
        {
            if (!_dropBuffer.IsCreated)
                return;

            PlayerInventory playerInventory = PlayerInventory.Instance;
            Hecton8.SaveSystem.ItemCatalog itemCatalog = playerInventory != null ? playerInventory.ItemCatalog : null;
            PersistentWorldRegistry registry = PersistentWorldRegistry.Instance;
            while (_dropBuffer.TryDequeue(out ItemDropData drop))
            {
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
            uint toolCapabilityMask,
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
                toolCapabilityMask,
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
                toolCapabilityMask,
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
            uint toolCapabilityMask,
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

                HectonVegetationInstanceData instanceMetadata = metadata[i];
                HarvestableTemplate.MaterialClass materialClass = (HarvestableTemplate.MaterialClass)materialClasses[i];
                int templateIndex = ResolveTemplateIndex(instanceMetadata, materialClass);
                if (materialClass == HarvestableTemplate.MaterialClass.None || templateIndex < 0)
                    continue;

                if (!IsToolCompatible(instanceMetadata, toolCapabilityMask))
                    continue;

                Vector3 rootPosition = ExtractTranslation(matrices[i]);
                float distanceSq = ResolveHarvestDistanceSq(hitPoint, rootPosition, instanceMetadata, types[i], searchRadiusSq, kelpHeightTolerance);
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

            bool hasNavObstacleBounds = TryResolveNavObstacleForLaneInstance(underwater, activeIndex, out float3 navObstacleCenter, out float3 navObstacleExtents);

            _destroyedByInstanceUid.TryAdd(instanceUid, 1);
            _healthByInstanceUid.Remove(instanceUid);
            _healthByInstanceUid.TryAdd(instanceUid, (Unity.Mathematics.half)0f);
            if (_damageVisualProgressByInstanceUid.IsCreated)
                _damageVisualProgressByInstanceUid.Remove(instanceUid);
            PrimeDecompositionState(instanceUid, Time.time);
            SetLaneHealth(underwater, activeIndex, 0f);
            if (_pendingWiltEndTimeByInstanceUid.IsCreated)
                _pendingWiltEndTimeByInstanceUid.Remove(instanceUid);

            ApplyDecompositionToLaneInstance(underwater, activeIndex, instanceUid, 0f);

            PublishExternalInteraction(instancePosition, hitNormal.sqrMagnitude > 0.0001f ? hitNormal.normalized * (normalizedPower * OrganicBurstVelocityScale) : Vector3.up, interactionBurstRadius * 1.25f);
            SpawnDebris(materialClass, instanceMatrix, instancePosition, hitPoint, hitNormal, normalizedPower, instanceUid);
            QueueYieldEvent(
                instancePosition,
                normalizedPower,
                instanceUid,
                templateIndex,
                materialClass,
                ResolveParentMassKg(underwater, activeIndex, materialClass, templateIndex),
                1f,
                hasNavObstacleBounds ? navObstacleCenter : float3.zero,
                hasNavObstacleBounds ? navObstacleExtents : float3.zero);

            PersistentWorldRegistry registry = PersistentWorldRegistry.Instance;
            if (registry != null)
                registry.TryClearFloraStateOverride(instanceUid);

            ClearPersistedFloraStateOverride(instanceUid);
            if (registry != null && templateIndex >= 0 && templateIndex < _templateDescriptors.Length)
                registry.TryRegisterDestroyedFlora((ulong)(uint)_templateDescriptors[templateIndex].StableHashId, instanceUid, instancePosition);
        }

        private void ApplyPassiveDecomposition(
            bool underwater,
            int activeIndex,
            uint instanceUid,
            HarvestableTemplate.MaterialClass materialClass,
            int templateIndex,
            Vector3 instancePosition)
        {
            if (!_destroyedByInstanceUid.IsCreated || instanceUid == 0u || _destroyedByInstanceUid.ContainsKey(instanceUid))
                return;

            bool hasNavObstacleBounds = TryResolveNavObstacleForLaneInstance(underwater, activeIndex, out float3 navObstacleCenter, out float3 navObstacleExtents);

            _destroyedByInstanceUid.TryAdd(instanceUid, 1);
            _healthByInstanceUid.Remove(instanceUid);
            _healthByInstanceUid.TryAdd(instanceUid, (Unity.Mathematics.half)0f);
            if (_damageVisualProgressByInstanceUid.IsCreated)
                _damageVisualProgressByInstanceUid.Remove(instanceUid);
            if (_pendingWiltEndTimeByInstanceUid.IsCreated)
                _pendingWiltEndTimeByInstanceUid.Remove(instanceUid);
            if (_regrowthProgressByInstanceUid.IsCreated)
                _regrowthProgressByInstanceUid.Remove(instanceUid);
            if (_regrowthPositionByInstanceUid.IsCreated)
                _regrowthPositionByInstanceUid.Remove(instanceUid);

            PrimeDecompositionState(instanceUid, Time.time);
            SetLaneHealth(underwater, activeIndex, 0f);
            ApplyDecompositionToLaneInstance(underwater, activeIndex, instanceUid, 0f);

            PersistentWorldRegistry registry = PersistentWorldRegistry.Instance;
            if (registry != null)
                registry.TryClearFloraStateOverride(instanceUid);

            ClearPersistedFloraStateOverride(instanceUid);
            if (registry != null && templateIndex >= 0 && templateIndex < _templateDescriptors.Length)
                registry.TryRegisterDestroyedFlora((ulong)(uint)_templateDescriptors[templateIndex].StableHashId, instanceUid, instancePosition);

            QueueYieldEvent(
                instancePosition,
                0.1f,
                instanceUid,
                -1,
                materialClass,
                0.05f,
                0f,
                hasNavObstacleBounds ? navObstacleCenter : float3.zero,
                hasNavObstacleBounds ? navObstacleExtents : float3.zero);
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
            HarvestableTemplate.MaterialClass materialClass,
            float parentMassKg,
            float damage01,
            float3 navObstacleCenter,
            float3 navObstacleExtents)
        {
            if (!_pendingYieldEvents.IsCreated || _pendingYieldEvents.Length >= _pendingYieldEvents.Capacity)
                return;

            _pendingYieldEvents.AddNoResize(new DestroyedOrganicEvent
            {
                Position = new float3(instancePosition.x, instancePosition.y, instancePosition.z),
                NavObstacleCenter = navObstacleCenter,
                NavObstacleExtents = navObstacleExtents,
                ToolPower = Mathf.Max(0.1f, normalizedPower),
                ParentMassKg = Mathf.Max(0.05f, parentMassKg),
                Damage01 = Mathf.Clamp01(damage01),
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

        private int ResolveTemplateIndex(HectonVegetationInstanceData metadata, HarvestableTemplate.MaterialClass fallbackMaterialClass)
        {
            int floraTemplateIndex = Mathf.RoundToInt(metadata.TemplateIndex);
            if (_harvestDescriptorIndexByFloraTemplateIndex != null &&
                floraTemplateIndex >= 0 &&
                floraTemplateIndex < _harvestDescriptorIndexByFloraTemplateIndex.Length)
            {
                int mappedDescriptorIndex = _harvestDescriptorIndexByFloraTemplateIndex[floraTemplateIndex];
                if (mappedDescriptorIndex >= 0)
                    return mappedDescriptorIndex;
            }

            return ResolveTemplateIndex(fallbackMaterialClass);
        }

        private bool IsToolCompatible(HectonVegetationInstanceData metadata, uint toolCapabilityMask)
        {
            if (toolCapabilityMask == 0u || vegetationBridge == null)
                return true;

            int floraTemplateIndex = Mathf.RoundToInt(metadata.TemplateIndex);
            if (!vegetationBridge.TryGetFloraTemplateRuntimeDescriptor(floraTemplateIndex, out FloraDataTemplate.RuntimeDescriptor descriptor))
                return true;

            return descriptor.VulnerabilityMask == 0u || (descriptor.VulnerabilityMask & toolCapabilityMask) != 0u;
        }

        private void CacheBaseScale(uint instanceUid, HectonVegetationInstanceData metadata)
        {
            if (!_baseScaleByInstanceUid.IsCreated || instanceUid == 0u || _baseScaleByInstanceUid.ContainsKey(instanceUid))
                return;

            _baseScaleByInstanceUid.TryAdd(
                instanceUid,
                new float2(
                    Mathf.Max(MinimumDecomposedHeightScale, Mathf.Abs(metadata.HeightScale)),
                    Mathf.Max(MinimumDecomposedWidthScale, Mathf.Clamp01(Mathf.Abs(metadata.WidthScale)))));
        }

        private byte ResolveRuntimeFlags(uint instanceUid, HarvestableTemplate.MaterialClass materialClass, int semanticType, float existingRuntimeFlags)
        {
            if (!_runtimeFlagsByInstanceUid.IsCreated || instanceUid == 0u)
                return HectonVegetationRuntimeFlagEncoding.ExtractPackedFlags(existingRuntimeFlags);

            if (_runtimeFlagsByInstanceUid.TryGetValue(instanceUid, out byte existingFlags))
                return existingFlags;

            byte resolvedFlags = HectonVegetationRuntimeFlagEncoding.ExtractPackedFlags(existingRuntimeFlags);
            bool parasiteEligible = materialClass == HarvestableTemplate.MaterialClass.Kelp ||
                                    materialClass == HarvestableTemplate.MaterialClass.Sargassum;
            if (parasiteEligible)
            {
                uint parasiteHash = instanceUid ^ (uint)(semanticType + 17) * 2246822519u;
                if ((parasiteHash & 0x0Fu) <= 1u)
                    resolvedFlags |= FloraRuntimeFlagHasParasite;
            }

            _runtimeFlagsByInstanceUid.TryAdd(instanceUid, resolvedFlags);
            return resolvedFlags;
        }

        private static void ApplyRuntimeFlags(ref NativeArray<HectonVegetationInstanceData> metadata, int activeIndex, byte runtimeFlags)
        {
            if (!metadata.IsCreated || activeIndex < 0 || activeIndex >= metadata.Length)
                return;

            HectonVegetationInstanceData flaggedMetadata = metadata[activeIndex];
            flaggedMetadata.RuntimeFlags = runtimeFlags;
            metadata[activeIndex] = flaggedMetadata;
        }

        private bool TryResolveNavObstacleForLaneInstance(bool underwater, int activeIndex, out float3 center, out float3 extents)
        {
            center = float3.zero;
            extents = float3.zero;
            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            NativeArray<int> types = underwater ? _underwaterTypes : _surfaceTypes;
            NativeArray<int> semanticTypes = underwater ? _underwaterSemanticTypes : _surfaceSemanticTypes;
            if (!matrices.IsCreated ||
                !metadata.IsCreated ||
                !types.IsCreated ||
                !semanticTypes.IsCreated ||
                activeIndex < 0 ||
                activeIndex >= matrices.Length ||
                activeIndex >= metadata.Length ||
                activeIndex >= types.Length ||
                activeIndex >= semanticTypes.Length)
            {
                return false;
            }

            return VoxelDynamicNavGridRuntime.TryResolveMacroFloraObstacleWorldBounds(
                matrices[activeIndex],
                metadata[activeIndex],
                types[activeIndex],
                semanticTypes[activeIndex],
                out center,
                out extents);
        }

        private static void SetRuntimeState(ref NativeArray<HectonVegetationInstanceData> metadata, int activeIndex, float runtimeState)
        {
            if (!metadata.IsCreated || activeIndex < 0 || activeIndex >= metadata.Length)
                return;

            HectonVegetationInstanceData stateMetadata = metadata[activeIndex];
            stateMetadata.RuntimeState = runtimeState;
            metadata[activeIndex] = stateMetadata;
        }

        private void PrimeDecompositionState(uint instanceUid, float decompositionStartTime)
        {
            if (!_decompositionStartTimeByInstanceUid.IsCreated || instanceUid == 0u)
                return;

            _decompositionStartTimeByInstanceUid.Remove(instanceUid);
            _decompositionStartTimeByInstanceUid.TryAdd(instanceUid, decompositionStartTime);
        }

        private float ResolveOrPrimeDecompositionProgress(uint instanceUid, float currentTime)
        {
            if (!_decompositionStartTimeByInstanceUid.IsCreated || instanceUid == 0u)
                return 1f;

            if (!_decompositionStartTimeByInstanceUid.TryGetValue(instanceUid, out float startTime))
            {
                startTime = currentTime - OrganicDecompositionDurationSeconds;
                _decompositionStartTimeByInstanceUid.TryAdd(instanceUid, startTime);
            }

            return math.saturate((currentTime - startTime) / OrganicDecompositionDurationSeconds);
        }

        private void ApplyDecompositionToLaneInstance(bool underwater, int activeIndex, uint instanceUid, float entropy01)
        {
            if (underwater)
                ApplyDecompositionMetadata(ref _underwaterMetadata, activeIndex, instanceUid, entropy01);
            else
                ApplyDecompositionMetadata(ref _surfaceMetadata, activeIndex, instanceUid, entropy01);
        }

        internal bool TryEvaluateParasiteExposure(Vector3 runtimePosition, out float exposure01)
        {
            exposure01 = 0f;
            float bestExposure = 0f;
            EvaluateParasiteExposureInLane(runtimePosition, false, ref bestExposure);
            EvaluateParasiteExposureInLane(runtimePosition, true, ref bestExposure);
            exposure01 = Mathf.Clamp01(bestExposure);
            return exposure01 > 0.0001f;
        }

        private void ApplyDamageVisualState(
            uint instanceUid,
            bool underwater,
            int activeIndex,
            float baseHealth,
            float currentHealth,
            float currentTime)
        {
            float damage01 = ResolveDamageProgress(baseHealth, currentHealth);
            UpdateDamageProgressCache(instanceUid, damage01);
            if (damage01 <= 0.0001f)
                return;

            float normalizedHeightScale = ResolveNormalizedHeightScale(baseHealth, currentHealth);
            ApplyDamageToLaneInstance(underwater, activeIndex, instanceUid, damage01, normalizedHeightScale, currentTime);
        }

        private void UpdateDamageProgressCache(uint instanceUid, float damage01)
        {
            if (!_damageVisualProgressByInstanceUid.IsCreated || instanceUid == 0u)
                return;

            _damageVisualProgressByInstanceUid.Remove(instanceUid);
            if (damage01 > 0.0001f)
                _damageVisualProgressByInstanceUid.TryAdd(instanceUid, damage01);
        }

        private static float ResolveDamageProgress(float baseHealth, float currentHealth)
        {
            float normalizedHealth = currentHealth / math.max(0.0001f, baseHealth);
            return math.saturate((0.5f - normalizedHealth) * 2f);
        }

        private static float ResolveNormalizedHeightScale(float baseHealth, float currentHealth)
        {
            return math.max(MinimumDecomposedHeightScale, currentHealth / math.max(0.0001f, baseHealth));
        }

        private bool TryResolvePersistedFloraState(uint instanceUid, out float normalizedHealth, out float normalizedHeightScale)
        {
            normalizedHealth = 1f;
            normalizedHeightScale = 1f;
            if (!_persistedHealth01ByInstanceUid.IsCreated || !_persistedHeightScale01ByInstanceUid.IsCreated || instanceUid == 0u)
                return false;

            bool hasHealth = _persistedHealth01ByInstanceUid.TryGetValue(instanceUid, out Unity.Mathematics.half persistedHealth);
            bool hasHeight = _persistedHeightScale01ByInstanceUid.TryGetValue(instanceUid, out Unity.Mathematics.half persistedHeight);
            if (!hasHealth || !hasHeight)
                return false;

            normalizedHealth = Mathf.Clamp01((float)persistedHealth);
            normalizedHeightScale = Mathf.Clamp01((float)persistedHeight);
            return true;
        }

        private float ResolvePersistedNormalizedHeightScale(uint instanceUid)
        {
            if (!_persistedHeightScale01ByInstanceUid.IsCreated ||
                instanceUid == 0u ||
                !_persistedHeightScale01ByInstanceUid.TryGetValue(instanceUid, out Unity.Mathematics.half persistedHeight))
            {
                return 0f;
            }

            return Mathf.Clamp01((float)persistedHeight);
        }

        private float ResolveRuntimeNormalizedHeightScale(uint instanceUid, HectonVegetationInstanceData metadata)
        {
            if (!_baseScaleByInstanceUid.IsCreated ||
                instanceUid == 0u ||
                !_baseScaleByInstanceUid.TryGetValue(instanceUid, out float2 baseScale))
            {
                return Mathf.Clamp01(Mathf.Abs(metadata.HeightScale));
            }

            return Mathf.Clamp01(Mathf.Abs(metadata.HeightScale) / math.max(0.0001f, baseScale.x));
        }

        private void ClearPersistedFloraStateOverride(uint instanceUid)
        {
            if (_persistedHealth01ByInstanceUid.IsCreated)
                _persistedHealth01ByInstanceUid.Remove(instanceUid);

            if (_persistedHeightScale01ByInstanceUid.IsCreated)
                _persistedHeightScale01ByInstanceUid.Remove(instanceUid);
        }

        private void PersistFloraStateOverride(
            uint instanceUid,
            int templateIndex,
            Vector3 instancePosition,
            bool underwater,
            int activeIndex,
            float baseHealth,
            float currentHealth)
        {
            if (instanceUid == 0u || templateIndex < 0 || templateIndex >= _templateDescriptors.Length)
                return;

            float normalizedHealth = Mathf.Clamp01(currentHealth / math.max(0.0001f, baseHealth));
            float normalizedHeightScale = ResolveCurrentNormalizedHeightScale(underwater, activeIndex, instanceUid, normalizedHealth);
            if (normalizedHealth >= 0.9999f && normalizedHeightScale >= 0.9999f)
            {
                PersistentWorldRegistry.Instance?.TryClearFloraStateOverride(instanceUid);
                ClearPersistedFloraStateOverride(instanceUid);
                return;
            }

            if (_persistedHealth01ByInstanceUid.IsCreated)
            {
                _persistedHealth01ByInstanceUid.Remove(instanceUid);
                _persistedHealth01ByInstanceUid.TryAdd(instanceUid, (Unity.Mathematics.half)normalizedHealth);
            }

            if (_persistedHeightScale01ByInstanceUid.IsCreated)
            {
                _persistedHeightScale01ByInstanceUid.Remove(instanceUid);
                _persistedHeightScale01ByInstanceUid.TryAdd(instanceUid, (Unity.Mathematics.half)normalizedHeightScale);
            }

            PersistentWorldRegistry registry = PersistentWorldRegistry.Instance;
            if (registry == null)
                return;

            registry.TryRegisterFloraStateOverride(
                (ulong)(uint)_templateDescriptors[templateIndex].StableHashId,
                instanceUid,
                instancePosition,
                normalizedHealth,
                normalizedHeightScale);
        }

        private float ResolveCurrentNormalizedHeightScale(
            bool underwater,
            int activeIndex,
            uint instanceUid,
            float fallbackNormalizedHeightScale)
        {
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            if (!metadata.IsCreated || activeIndex < 0 || activeIndex >= metadata.Length)
                return Mathf.Clamp01(fallbackNormalizedHeightScale);

            return ResolveRuntimeNormalizedHeightScale(instanceUid, metadata[activeIndex]);
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

        private void ApplyDamageToLaneInstance(
            bool underwater,
            int activeIndex,
            uint instanceUid,
            float damage01,
            float normalizedHeightScale,
            float currentTime)
        {
            if (underwater)
                ApplyPersistedDamageMetadata(ref _underwaterMetadata, activeIndex, instanceUid, normalizedHeightScale, damage01, currentTime);
            else
                ApplyPersistedDamageMetadata(ref _surfaceMetadata, activeIndex, instanceUid, normalizedHeightScale, damage01, currentTime);
        }

        private void UpdateRegrowthVisuals()
        {
            if (!_regrowthProgressByInstanceUid.IsCreated || _regrowthProgressByInstanceUid.Count <= 0)
                return;

            UpdateRegrowthLane(false);
            UpdateRegrowthLane(true);
        }

        private void UpdateRegrowthLane(bool underwater)
        {
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!instanceUids.IsCreated || count <= 0)
                return;

            for (int i = 0; i < count; i++)
            {
                uint instanceUid = instanceUids[i];
                if (instanceUid == 0u || !_regrowthProgressByInstanceUid.TryGetValue(instanceUid, out float progress01))
                    continue;

                ApplyRegrowthVisualToLaneInstance(underwater, i, instanceUid, progress01);
            }
        }

        private void UpdateDecompositionVisuals(float currentTime)
        {
            if (!_decompositionStartTimeByInstanceUid.IsCreated || _decompositionStartTimeByInstanceUid.Count <= 0)
                return;

            UpdateDecompositionLane(false, currentTime);
            UpdateDecompositionLane(true, currentTime);
        }

        private void UpdateDecompositionLane(bool underwater, float currentTime)
        {
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!instanceUids.IsCreated || !metadata.IsCreated || count <= 0)
                return;

            for (int i = 0; i < count; i++)
            {
                uint instanceUid = instanceUids[i];
                if (instanceUid == 0u ||
                    (_regrowthProgressByInstanceUid.IsCreated && _regrowthProgressByInstanceUid.ContainsKey(instanceUid)) ||
                    !_destroyedByInstanceUid.IsCreated ||
                    !_destroyedByInstanceUid.ContainsKey(instanceUid))
                {
                    continue;
                }

                float entropy01 = ResolveOrPrimeDecompositionProgress(instanceUid, currentTime);
                ApplyDecompositionMetadata(ref metadata, i, instanceUid, entropy01);
            }
        }

        private void UpdateDamageVisuals(float currentTime)
        {
            if (!_damageVisualProgressByInstanceUid.IsCreated || _damageVisualProgressByInstanceUid.Count <= 0)
                return;

            UpdateDamageLane(false, currentTime);
            UpdateDamageLane(true, currentTime);
        }

        private void UpdateDamageLane(bool underwater, float currentTime)
        {
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!instanceUids.IsCreated || !metadata.IsCreated || count <= 0)
                return;

            for (int i = 0; i < count; i++)
            {
                uint instanceUid = instanceUids[i];
                if (instanceUid == 0u ||
                    (_regrowthProgressByInstanceUid.IsCreated && _regrowthProgressByInstanceUid.ContainsKey(instanceUid)) ||
                    (_destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(instanceUid)) ||
                    (_pendingWiltEndTimeByInstanceUid.IsCreated && _pendingWiltEndTimeByInstanceUid.ContainsKey(instanceUid)) ||
                    !_damageVisualProgressByInstanceUid.TryGetValue(instanceUid, out float damage01))
                {
                    continue;
                }

                float normalizedHeightScale = ResolvePersistedNormalizedHeightScale(instanceUid);
                if (normalizedHeightScale <= 0.0001f)
                    normalizedHeightScale = ResolveRuntimeNormalizedHeightScale(instanceUid, metadata[i]);

                ApplyPersistedDamageMetadata(ref metadata, i, instanceUid, normalizedHeightScale, damage01, currentTime);
            }
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
            hiddenMetadata.RuntimeState = HectonVegetationInstanceData.RuntimeStateIdle;
            hiddenMetadata.RuntimeFlags = 0f;
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
            wiltMetadata.RuntimeState = HectonVegetationInstanceData.RuntimeStateDying;
            metadata[activeIndex] = wiltMetadata;
        }

        private void ApplyDecompositionMetadata(
            ref NativeArray<HectonVegetationInstanceData> metadata,
            int activeIndex,
            uint instanceUid,
            float entropy01)
        {
            if (!metadata.IsCreated || activeIndex < 0 || activeIndex >= metadata.Length)
                return;

            float2 baseScale = _baseScaleByInstanceUid.IsCreated && _baseScaleByInstanceUid.TryGetValue(instanceUid, out float2 cachedBaseScale)
                ? cachedBaseScale
                : new float2(1f, 1f);
            float smoothEntropy = entropy01 * entropy01 * (3f - (2f * entropy01));
            float decompositionStartTime = 0f;
            if (_decompositionStartTimeByInstanceUid.IsCreated)
                _decompositionStartTimeByInstanceUid.TryGetValue(instanceUid, out decompositionStartTime);

            HectonVegetationInstanceData decompositionMetadata = metadata[activeIndex];
            decompositionMetadata.HeightScale = -Mathf.Lerp(baseScale.x, MinimumDecomposedHeightScale, smoothEntropy);
            decompositionMetadata.WidthScale = -Mathf.Max(0.001f, decompositionStartTime);
            decompositionMetadata.RuntimeState = HectonVegetationInstanceData.RuntimeStateDying;
            metadata[activeIndex] = decompositionMetadata;
        }

        private static void ApplyDamageMetadata(
            ref NativeArray<HectonVegetationInstanceData> metadata,
            int activeIndex,
            float damage01,
            float currentTime)
        {
            if (!metadata.IsCreated || activeIndex < 0 || activeIndex >= metadata.Length || damage01 <= 0.0001f)
                return;

            HectonVegetationInstanceData damageMetadata = metadata[activeIndex];
            damageMetadata.HeightScale = -Mathf.Max(0.05f, Mathf.Abs(damageMetadata.HeightScale));
            damageMetadata.WidthScale = currentTime - (Mathf.Clamp01(damage01) * OrganicWiltDurationSeconds);
            damageMetadata.RuntimeState = HectonVegetationInstanceData.RuntimeStateAgitated;
            metadata[activeIndex] = damageMetadata;
        }

        private void ApplyPersistedDamageMetadata(
            ref NativeArray<HectonVegetationInstanceData> metadata,
            int activeIndex,
            uint instanceUid,
            float normalizedHeightScale,
            float damage01,
            float currentTime)
        {
            if (!metadata.IsCreated || activeIndex < 0 || activeIndex >= metadata.Length)
                return;

            if (_baseScaleByInstanceUid.IsCreated &&
                _baseScaleByInstanceUid.TryGetValue(instanceUid, out float2 baseScale))
            {
                float clampedHeight01 = Mathf.Clamp01(normalizedHeightScale);
                HectonVegetationInstanceData damageMetadata = metadata[activeIndex];
                damageMetadata.HeightScale = -Mathf.Max(MinimumDecomposedHeightScale, baseScale.x * clampedHeight01);
                damageMetadata.WidthScale = currentTime - (Mathf.Clamp01(damage01) * OrganicWiltDurationSeconds);
                damageMetadata.RuntimeState = HectonVegetationInstanceData.RuntimeStateAgitated;
                metadata[activeIndex] = damageMetadata;
                return;
            }

            ApplyDamageMetadata(ref metadata, activeIndex, damage01, currentTime);
        }

        private void EvaluateParasiteExposureInLane(Vector3 runtimePosition, bool underwater, ref float bestExposure)
        {
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<Unity.Mathematics.half> laneHealth = underwater ? _underwaterHealth : _surfaceHealth;
            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!instanceUids.IsCreated || !laneHealth.IsCreated || !matrices.IsCreated || count <= 0 || !_runtimeFlagsByInstanceUid.IsCreated)
                return;

            const float parasiteRadius = 3.25f;
            float inverseRadius = 1f / parasiteRadius;
            for (int i = 0; i < count; i++)
            {
                uint instanceUid = instanceUids[i];
                if (instanceUid == 0u ||
                    (float)laneHealth[i] <= 0.0001f ||
                    !_runtimeFlagsByInstanceUid.TryGetValue(instanceUid, out byte runtimeFlags) ||
                    (runtimeFlags & FloraRuntimeFlagHasParasite) == 0)
                {
                    continue;
                }

                Vector3 delta = ExtractTranslation(matrices[i]) - runtimePosition;
                float distance = delta.magnitude;
                if (distance >= parasiteRadius)
                    continue;

                float exposure = 1f - Mathf.Clamp01(distance * inverseRadius);
                if (exposure > bestExposure)
                    bestExposure = exposure;
            }
        }

        internal bool IsMaterialClassRegrowable(ulong floraPersistentIdHash)
        {
            for (int i = 0; i < _templateDescriptors.Length; i++)
            {
                if ((ulong)(uint)_templateDescriptors[i].StableHashId != floraPersistentIdHash)
                    continue;

                HarvestableTemplate.MaterialClass materialClass = (HarvestableTemplate.MaterialClass)_templateDescriptors[i].MaterialClassId;
                return materialClass == HarvestableTemplate.MaterialClass.Kelp ||
                       materialClass == HarvestableTemplate.MaterialClass.Sargassum;
            }

            return false;
        }

        internal bool IsTemplateMaterialClass(ulong floraPersistentIdHash, HarvestableTemplate.MaterialClass materialClass)
        {
            for (int i = 0; i < _templateDescriptors.Length; i++)
            {
                if ((ulong)(uint)_templateDescriptors[i].StableHashId != floraPersistentIdHash)
                    continue;

                return _templateDescriptors[i].MaterialClassId == (byte)materialClass;
            }

            return false;
        }

        internal bool TrySetRegrowthProgress(uint instanceUid, Vector3 runtimePosition, float progress01)
        {
            if (instanceUid == 0u ||
                !_regrowthProgressByInstanceUid.IsCreated ||
                !_regrowthPositionByInstanceUid.IsCreated)
            {
                return false;
            }

            progress01 = math.saturate(progress01);
            if (_destroyedByInstanceUid.IsCreated)
                _destroyedByInstanceUid.Remove(instanceUid);

            if (_pendingWiltEndTimeByInstanceUid.IsCreated)
                _pendingWiltEndTimeByInstanceUid.Remove(instanceUid);

            if (_damageVisualProgressByInstanceUid.IsCreated)
                _damageVisualProgressByInstanceUid.Remove(instanceUid);

            if (_decompositionStartTimeByInstanceUid.IsCreated)
                _decompositionStartTimeByInstanceUid.Remove(instanceUid);

            _regrowthProgressByInstanceUid.Remove(instanceUid);
            _regrowthProgressByInstanceUid.TryAdd(instanceUid, progress01);
            _regrowthPositionByInstanceUid.Remove(instanceUid);
            _regrowthPositionByInstanceUid.TryAdd(instanceUid, new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z));

            if (TryResolveActiveInstanceByUid(instanceUid, out bool underwater, out int activeIndex, out int templateIndex))
            {
                ApplyRegrowthVisualToLaneInstance(underwater, activeIndex, instanceUid, progress01);
                float health = ResolveRegrowthHealth(progress01, templateIndex);
                SetLaneHealth(underwater, activeIndex, health);
                _healthByInstanceUid.Remove(instanceUid);
                _healthByInstanceUid.TryAdd(instanceUid, (Unity.Mathematics.half)health);
            }

            if (progress01 >= 0.9999f)
                FinalizeRegrowth(instanceUid);

            return true;
        }

        private void FinalizeRegrowth(uint instanceUid)
        {
            if (_regrowthProgressByInstanceUid.IsCreated)
                _regrowthProgressByInstanceUid.Remove(instanceUid);

            if (_regrowthPositionByInstanceUid.IsCreated)
                _regrowthPositionByInstanceUid.Remove(instanceUid);

            if (_decompositionStartTimeByInstanceUid.IsCreated)
                _decompositionStartTimeByInstanceUid.Remove(instanceUid);

            PersistentWorldRegistry registry = PersistentWorldRegistry.Instance;
            if (registry != null)
                registry.TryClearFloraStateOverride(instanceUid);

            ClearPersistedFloraStateOverride(instanceUid);

            if (TryResolveActiveInstanceByUid(instanceUid, out bool underwater, out int activeIndex, out int templateIndex))
            {
                float baseHealth = templateIndex >= 0 && templateIndex < _templateDescriptors.Length
                    ? Mathf.Max(0.1f, _templateDescriptors[templateIndex].BaseHealth)
                    : 1f;
                SetLaneHealth(underwater, activeIndex, baseHealth);
                _healthByInstanceUid.Remove(instanceUid);
                _healthByInstanceUid.TryAdd(instanceUid, (Unity.Mathematics.half)baseHealth);
                ApplyRegrowthVisualToLaneInstance(underwater, activeIndex, instanceUid, 1f);
                return;
            }

            _healthByInstanceUid.Remove(instanceUid);
        }

        private float ResolveRegrowthHealth(float progress01, int templateIndex)
        {
            float baseHealth = templateIndex >= 0 && templateIndex < _templateDescriptors.Length
                ? Mathf.Max(0.1f, _templateDescriptors[templateIndex].BaseHealth)
                : 1f;
            float smoothProgress = progress01 * progress01 * (3f - (2f * progress01));
            return Mathf.Max(0.05f, Mathf.Lerp(baseHealth * 0.1f, baseHealth, smoothProgress));
        }

        private void ApplyRegrowthVisualToLaneInstance(bool underwater, int activeIndex, uint instanceUid, float progress01)
        {
            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            NativeArray<int> types = underwater ? _underwaterTypes : _surfaceTypes;
            if (!matrices.IsCreated ||
                !metadata.IsCreated ||
                !types.IsCreated ||
                activeIndex < 0 ||
                activeIndex >= matrices.Length ||
                activeIndex >= metadata.Length ||
                activeIndex >= types.Length)
            {
                return;
            }

            if (_regrowthPositionByInstanceUid.IsCreated &&
                _regrowthPositionByInstanceUid.TryGetValue(instanceUid, out float3 regrowthPosition))
            {
                Matrix4x4 visibleMatrix = matrices[activeIndex];
                visibleMatrix.m03 = regrowthPosition.x;
                visibleMatrix.m13 = regrowthPosition.y;
                visibleMatrix.m23 = regrowthPosition.z;
                matrices[activeIndex] = visibleMatrix;
            }

            float smoothProgress = progress01 * progress01 * (3f - (2f * progress01));
            float2 baseScale = _baseScaleByInstanceUid.IsCreated && _baseScaleByInstanceUid.TryGetValue(instanceUid, out float2 cachedBaseScale)
                ? cachedBaseScale
                : new float2(1f, 1f);
            HectonVegetationInstanceData regrowthMetadata = metadata[activeIndex];
            regrowthMetadata.Type = types[activeIndex];
            regrowthMetadata.HeightScale = Mathf.Lerp(MinimumDecomposedHeightScale, baseScale.x, smoothProgress);
            regrowthMetadata.WidthScale = Mathf.Lerp(MinimumDecomposedWidthScale, baseScale.y, smoothProgress);
            regrowthMetadata.RuntimeState = progress01 >= 0.995f
                ? HectonVegetationInstanceData.RuntimeStateIdle
                : HectonVegetationInstanceData.RuntimeStateAgitated;
            metadata[activeIndex] = regrowthMetadata;
        }

        private bool TryResolveActiveInstanceByUid(uint instanceUid, out bool underwater, out int activeIndex, out int templateIndex)
        {
            if (TryResolveActiveInstanceByUid(instanceUid, _surfaceInstanceUids, _surfaceCount, _surfaceMaterialClasses, _surfaceMetadata, out activeIndex, out templateIndex))
            {
                underwater = false;
                return true;
            }

            if (TryResolveActiveInstanceByUid(instanceUid, _underwaterInstanceUids, _underwaterCount, _underwaterMaterialClasses, _underwaterMetadata, out activeIndex, out templateIndex))
            {
                underwater = true;
                return true;
            }

            underwater = false;
            activeIndex = -1;
            templateIndex = -1;
            return false;
        }

        private bool TryResolveActiveInstanceByUid(
            uint instanceUid,
            NativeArray<uint> instanceUids,
            int count,
            NativeArray<byte> materialClasses,
            NativeArray<HectonVegetationInstanceData> metadata,
            out int activeIndex,
            out int templateIndex)
        {
            activeIndex = -1;
            templateIndex = -1;
            if (!instanceUids.IsCreated || !materialClasses.IsCreated || !metadata.IsCreated || count <= 0)
                return false;

            for (int i = 0; i < count; i++)
            {
                if (instanceUids[i] != instanceUid)
                    continue;

                activeIndex = i;
                templateIndex = ResolveTemplateIndex(metadata[i], (HarvestableTemplate.MaterialClass)materialClasses[i]);
                return true;
            }

            return false;
        }

        private float ResolveParentMassKg(
            bool underwater,
            int activeIndex,
            HarvestableTemplate.MaterialClass materialClass,
            int templateIndex)
        {
            float baseHealth = templateIndex >= 0 && templateIndex < _templateDescriptors.Length
                ? Mathf.Max(0.1f, _templateDescriptors[templateIndex].BaseHealth)
                : 1f;
            float height01 = 1f;
            float width01 = 1f;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            if (metadata.IsCreated && activeIndex >= 0 && activeIndex < metadata.Length)
            {
                HectonVegetationInstanceData instanceData = metadata[activeIndex];
                height01 = Mathf.Clamp01(Mathf.Abs(instanceData.HeightScale));
                width01 = Mathf.Clamp01(instanceData.WidthScale);
            }

            return materialClass switch
            {
                HarvestableTemplate.MaterialClass.Kelp => Mathf.Max(1f, baseHealth * Mathf.Lerp(0.28f, 0.52f, height01) * Mathf.Lerp(0.9f, 1.15f, width01)),
                HarvestableTemplate.MaterialClass.Coral => Mathf.Max(2f, baseHealth * Mathf.Lerp(0.55f, 0.8f, height01)),
                HarvestableTemplate.MaterialClass.TitaniumOutcrop => Mathf.Max(4f, baseHealth * Mathf.Lerp(0.82f, 1.08f, height01)),
                HarvestableTemplate.MaterialClass.Sargassum => Mathf.Max(0.75f, baseHealth * Mathf.Lerp(0.22f, 0.38f, height01) * Mathf.Lerp(0.85f, 1.1f, width01)),
                _ => Mathf.Max(1f, baseHealth * 0.4f)
            };
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

        private static float ResolveConstructionDistanceSq(
            Vector3 centerUniversePosition,
            Vector3 rootPosition,
            HectonVegetationInstanceData metadata,
            int typeId)
        {
            HectonVegetationInstanceType vegetationType = (HectonVegetationInstanceType)typeId;
            if (vegetationType == HectonVegetationInstanceType.GiantKelp)
            {
                float kelpHeight = Mathf.Lerp(10f, 20f, Mathf.Clamp01(metadata.HeightScale));
                Vector3 top = rootPosition + Vector3.up * Mathf.Max(0.5f, kelpHeight + KelpRadiusBias);
                Vector3 closest = ClosestPointOnSegment(rootPosition, top, centerUniversePosition);
                return (closest - centerUniversePosition).sqrMagnitude;
            }

            return (rootPosition - centerUniversePosition).sqrMagnitude;
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

        private static float ResolveFractionalVariation(float encodedVariation)
        {
            return Mathf.Repeat(encodedVariation, 1f);
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
            uint hv = (uint)Mathf.RoundToInt(ResolveFractionalVariation(metadata.Variation) * 10000f) * 2654435761u;
            uint hs = (uint)(semanticType + 1) * 2246822519u;
            uint ht = (uint)(typeId + 1) * 3266489917u;
            uint mixed = hx ^ hy ^ hz ^ hv ^ hs ^ ht;
            return mixed == 0u ? 1u : mixed;
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

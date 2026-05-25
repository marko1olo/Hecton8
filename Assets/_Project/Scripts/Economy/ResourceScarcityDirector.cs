using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hecton8.AtlasSignal;
using Hecton8.Building;
using Hecton8.Core;
using Hecton8.Crafting;
using Hecton8.Inventory;
using Hecton8.Interaction;
using Hecton8.Items;
using Hecton8.Modding;
using Hecton8.Quest;
using Hecton8.SaveSystem;
using Hecton8.World;
using Hecton.Localization;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Economy
{
    /// <summary>
    /// Tracks cumulative resource extraction, issues scarcity directives, and exposes sector-local deflation scalars.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-6250)]
    [AddComponentMenu("Hecton8/Economy/Resource Scarcity Director")]
    public sealed class ResourceScarcityDirector : MonoBehaviour, ISaveable, ISlowTickable, IResourceScarcityReadModel, IInteractionEventListener, IGlobalRegistryHotSwapListener
    {
        private const int InitialTrackedCapacity = 64;
        private const int UnitsPerScarcityStep = 100;
        private const float ScarcityStepMultiplier = 0.04f;
        private const float MaxIngredientMultiplier = 1.80f;
        private const int MaxDirectiveResources = 8;
        private const int KnownClustersPerResource = 4;
        private const int MaxSectorExtractionRecords = 64;
        private const float SectorEdgeLengthMeters = 1000f;
        private const float InverseSectorEdgeLengthMeters = 1f / SectorEdgeLengthMeters;
        private const int SectorsPerAupCell = 5;
        private const string DefaultTitaniumDirectiveItemId = "Data_TitaniumScrap";
        private const int TitaniumHoardingThresholdUnits = 500;
        private const float TitaniumHoardingIngredientMultiplier = 4f;
        private const int DefaultTitaniumCriticalThreshold = 4;
        private const int DefaultTitaniumDirectiveHarvestUnits = 4;
        private const string DefaultDirectiveTitleFallback = "ATLAS-6 DIRECTIVE: RESOURCE RESTOCK";
        private const string DefaultDirectiveDescriptionFallback =
            "Recovered stock is below Atlas-6 operating threshold. Harvest additional critical stock to stabilize fabrication reserves.";
        private static readonly int _TitaniumDirectiveHashId = LocHash.Compute(DefaultTitaniumDirectiveItemId);

        [Serializable]
#pragma warning disable 0649 // Unity serializes scarcity directive definitions from authoring data.
        private struct DirectiveResourceDefinition
        {
            [Tooltip("Essential resource monitored by the Atlas scarcity directive owner.")]
            public ItemData item;

            [Tooltip("Quest activates when the player's carried quantity falls below this threshold.")]
            [Min(0)] public int criticalThreshold;

            [Tooltip("Minimum harvest quantity required to complete the directive once active.")]
            [Min(1)] public int directiveHarvestUnits;

            [Tooltip("Optional explicit directive title. Falls back to a generated Atlas-6 title when left empty.")]
            public string directiveTitle;

            [Tooltip("Optional explicit directive description. Falls back to a generated scarcity description when left empty.")]
            [TextArea(2, 4)] public string directiveDescription;

            [Tooltip("Optional stable marker target ID. Leave empty to use the nearest remembered cluster position.")]
            public string markerTargetId;

            [Tooltip("Vertical marker offset above the resolved remembered cluster.")]
            [Min(0f)] public float markerHeightOffset;

            [Tooltip("Optional narrative phase gate applied to the generated directive.")]
            public QuestPhaseGateType phaseGate;
        }
#pragma warning restore 0649

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct ResourceClusterRecord
        {
            [FieldOffset(0)] public int ItemHashId;
            [FieldOffset(4)] public int ObservationCount;
            [FieldOffset(8)] public AbsoluteUniversePosition PositionAup;
            [FieldOffset(56)] private ulong _pad0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct SectorExtractionRecord
        {
            [FieldOffset(0)] public int ItemHashId;
            [FieldOffset(4)] public int SectorKey;
            [FieldOffset(8)] public int ExtractedUnits;
            [FieldOffset(12)] private uint _pad0;
        }

        [Header("── Scarcity Curve ──────────────────")]
        [Tooltip("Optional sector-local inflation profile that lowers value and spawn-rate after repeated extraction from one sector.")]
        [SerializeField] private EconomyInflationProfile inflationProfile;

        [Header("── Atlas Directives ─────────────────")]
        [Tooltip("Essential resources that spawn procedural Atlas-6 scarcity directives when the player's stock falls too low.")]
        [SerializeField] private DirectiveResourceDefinition[] directiveResources = Array.Empty<DirectiveResourceDefinition>();

        // COLD ALLOC: Dictionary<int,int>[64] - cumulative collected raw-resource counts by stable item hash - owner: ResourceScarcityDirector
        private readonly Dictionary<int, int> _collectedByItemHash = new Dictionary<int, int>(InitialTrackedCapacity);
        // COLD ALLOC: Dictionary<int,string>[64] - stable item ID lookup for save serialization and diagnostics - owner: ResourceScarcityDirector
        private readonly Dictionary<int, string> _itemIdsByHash = new Dictionary<int, string>(InitialTrackedCapacity);
        // COLD ALLOC: ResourceClusterRecord[32] - remembered harvest clusters grouped by directive resource slice - owner: ResourceScarcityDirector
        private readonly ResourceClusterRecord[] _knownClusters = new ResourceClusterRecord[MaxDirectiveResources * KnownClustersPerResource];
        // COLD ALLOC: SectorExtractionRecord[64] - sector-local extraction totals used by inflation lookups - owner: ResourceScarcityDirector
        private readonly SectorExtractionRecord[] _sectorExtractionRecords = new SectorExtractionRecord[MaxSectorExtractionRecords];
        // COLD ALLOC: int[8] - cached directive resource item hashes - owner: ResourceScarcityDirector
        private readonly int[] _directiveItemHashes = new int[MaxDirectiveResources];
        // COLD ALLOC: uint[8] - cached scarcity directive quest hashes - owner: ResourceScarcityDirector
        private readonly uint[] _directiveQuestHashes = new uint[MaxDirectiveResources];
        // COLD ALLOC: uint[8] - cached directive marker target hashes - owner: ResourceScarcityDirector
        private readonly uint[] _directiveMarkerTargetHashes = new uint[MaxDirectiveResources];
        // COLD ALLOC: string[8] - cached directive titles - owner: ResourceScarcityDirector
        private readonly string[] _directiveTitles = new string[MaxDirectiveResources];
        // COLD ALLOC: string[8] - cached directive descriptions - owner: ResourceScarcityDirector
        private readonly string[] _directiveDescriptions = new string[MaxDirectiveResources];
        // COLD ALLOC: int[8] - cached directive critical inventory thresholds - owner: ResourceScarcityDirector
        private readonly int[] _directiveCriticalThresholds = new int[MaxDirectiveResources];
        // COLD ALLOC: int[8] - cached directive harvest completion requirements - owner: ResourceScarcityDirector
        private readonly int[] _directiveHarvestUnits = new int[MaxDirectiveResources];
        // COLD ALLOC: float[8] - cached directive marker height offsets - owner: ResourceScarcityDirector
        private readonly float[] _directiveMarkerHeightOffsets = new float[MaxDirectiveResources];
        // COLD ALLOC: QuestPhaseGateType[8] - cached directive phase gates - owner: ResourceScarcityDirector
        private readonly QuestPhaseGateType[] _directivePhaseGates = new QuestPhaseGateType[MaxDirectiveResources];

        private bool _registeredSlowTickable;
        private bool _serviceRegistered;
        private bool _hotSwapRegistered;
        private bool _saveServiceRegistered;
        private ISaveService _cachedSaveService;
        private IQuestSystem _cachedQuestManager;
        private IPlayerInventoryService _cachedInventoryService;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private int _cachedDirectiveCount;
        private int _runtimeVersion;

        /// <summary>
        /// Monotonic cache key incremented when sector extraction or scarcity totals mutate.
        /// </summary>
        public int RuntimeVersion => _runtimeVersion;

        /// <summary>
        /// Save priority keeps scarcity state in the world band before player inventory consumers.
        /// </summary>
        public int SavePriority => 40;

        /// <summary>
        /// Load priority keeps scarcity state in the world band before player inventory consumers.
        /// </summary>
        public int LoadPriority => 40;

        private void Awake()
        {
            CacheDirectiveDefinitions();
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            TryRegisterService();
            TryRegisterWithSaveManager();
            TryRegisterSlowTickable();
            InteractionEvents.Register(this);
            CacheDirectiveDefinitions();
        }

        private void OnDisable()
        {
            TryUnregisterFromSaveManager();
            TryUnregisterHotSwapListener();
            TryUnregisterSlowTickable();
            InteractionEvents.Unregister(this);
            TryUnregisterService();
        }

        private void OnDestroy()
        {
            TryUnregisterFromSaveManager();
            TryUnregisterHotSwapListener();
            TryUnregisterSlowTickable();
            InteractionEvents.Unregister(this);
            TryUnregisterService();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Save:
                    TryUnregisterFromSaveManager();
                    _cachedSaveService = currentService as ISaveService;
                    if (isActiveAndEnabled)
                        TryRegisterWithSaveManager();
                    break;
                case GlobalRegistryServiceSlot.QuestRuntime:
                case GlobalRegistryServiceSlot.QuestSystem:
                    _cachedQuestManager = currentService as IQuestSystem;
                    break;
                case GlobalRegistryServiceSlot.PlayerInventory:
                    _cachedInventoryService = currentService as IPlayerInventoryService;
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _cachedPlayerContext = currentService as IPlayerRuntimeContext;
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    if (currentService == null)
                    {
                        _registeredSlowTickable = false;
                        break;
                    }

                    if (isActiveAndEnabled)
                    {
                        TryUnregisterSlowTickable();
                        TryRegisterSlowTickable();
                    }
                    break;
            }
        }

        /// <summary>
        /// Evaluates the scarcity-driven directive table against current carried inventory.
        /// </summary>
        public void SlowTick()
        {
            EvaluateScarcityDirectives();
        }

        /// <summary>
        /// Returns the current scarcity multiplier for the specified recipe.
        /// </summary>
        public static float ResolveCraftPowerMultiplier(RecipeData recipe)
        {
            ResourceScarcityDirector runtime = GlobalRegistry.ResourceScarcity;
            return runtime != null ? runtime.GetCraftPowerMultiplier(recipe) : 1f;
        }

        /// <summary>
        /// Returns the current scarcity multiplier for the specified recipe.
        /// </summary>
        public float GetCraftPowerMultiplier(RecipeData recipe)
        {
            if (recipe == null || recipe.ingredients == null || recipe.ingredients.Count == 0)
                return 1f;

            float weightedSum = 0f;
            int totalIngredientUnits = 0;

            for (int i = 0; i < recipe.ingredients.Count; i++)
            {
                InventoryCost cost = recipe.ingredients[i];
                if (cost == null || cost.item == null || cost.amount <= 0)
                    continue;

                float ingredientMultiplier = GetIngredientMultiplier(cost.item.PersistentHashId);
                weightedSum += ingredientMultiplier * cost.amount;
                totalIngredientUnits += cost.amount;
            }

            if (totalIngredientUnits <= 0)
                return 1f;

            return weightedSum / totalIngredientUnits;
        }

        /// <summary>
        /// Returns the current scarcity multiplier for a single resource item hash.
        /// </summary>
        public float GetIngredientMultiplier(int itemHashId)
        {
            if (itemHashId == 0)
                return 1f;

            if (!_collectedByItemHash.TryGetValue(itemHashId, out int collectedCount) || collectedCount <= 0)
                return 1f;

            int scarcitySteps = collectedCount / UnitsPerScarcityStep;
            if (scarcitySteps <= 0)
                return 1f;

            return Mathf.Clamp(1f + scarcitySteps * ScarcityStepMultiplier, 1f, MaxIngredientMultiplier);
        }

        /// <summary>
        /// Returns the sector-local spawn-rate scalar for one resource.
        /// </summary>
        public float GetSectorSpawnRateScalar(int itemHashId, Vector3 worldPosition)
        {
            if (!TryResolveAupFromRuntimeOrigin(worldPosition, out AbsoluteUniversePosition positionAup))
                return 1f;

            return GetSectorSpawnRateScalar(itemHashId, in positionAup);
        }

        public float GetSectorSpawnRateScalar(int itemHashId, in AbsoluteUniversePosition worldPosition)
        {
            if (inflationProfile == null || itemHashId == 0)
                return 1f;

            return inflationProfile.EvaluateSpawnRateScalar(itemHashId, GetSectorExtractedUnits(itemHashId, in worldPosition));
        }

        /// <summary>
        /// Returns the sector-local value scalar for one resource.
        /// </summary>
        public float GetSectorValueScalar(int itemHashId, Vector3 worldPosition)
        {
            if (!TryResolveAupFromRuntimeOrigin(worldPosition, out AbsoluteUniversePosition positionAup))
                return 1f;

            return GetSectorValueScalar(itemHashId, in positionAup);
        }

        public float GetSectorValueScalar(int itemHashId, in AbsoluteUniversePosition worldPosition)
        {
            if (inflationProfile == null || itemHashId == 0)
                return 1f;

            return inflationProfile.EvaluateValueScalar(itemHashId, GetSectorExtractedUnits(itemHashId, in worldPosition));
        }

        /// <summary>
        /// Returns the sector-local crafting surcharge ratio for one resource.
        /// </summary>
        public float GetSectorCraftInflationScalar(int itemHashId, Vector3 worldPosition)
        {
            if (!TryResolveAupFromRuntimeOrigin(worldPosition, out AbsoluteUniversePosition positionAup))
                return 0f;

            return GetSectorCraftInflationScalar(itemHashId, in positionAup);
        }

        public float GetSectorCraftInflationScalar(int itemHashId, in AbsoluteUniversePosition worldPosition)
        {
            if (inflationProfile == null || itemHashId == 0)
                return 0f;

            return inflationProfile.EvaluateCraftInflationScalar(itemHashId, GetSectorExtractedUnits(itemHashId, in worldPosition));
        }

        /// <summary>
        /// Resolves the inflated ingredient amount for the supplied sector.
        /// </summary>
        public int ResolveInflatedIngredientAmount(int itemHashId, int baseAmount, Vector3 worldPosition)
        {
            return ResolveInflatedIngredientAmount(itemHashId, baseAmount, worldPosition, 0);
        }

        public int ResolveInflatedIngredientAmount(int itemHashId, int baseAmount, in AbsoluteUniversePosition worldPosition)
        {
            return ResolveInflatedIngredientAmount(itemHashId, baseAmount, in worldPosition, 0);
        }

        /// <summary>
        /// Resolves the inflated ingredient amount using sector extraction and accessible-storage hoarding pressure.
        /// </summary>
        public int ResolveInflatedIngredientAmount(int itemHashId, int baseAmount, Vector3 worldPosition, int accessibleUnits)
        {
            if (!TryResolveAupFromRuntimeOrigin(worldPosition, out AbsoluteUniversePosition positionAup))
                return ResolveInflatedIngredientAmountWithoutSector(itemHashId, baseAmount, accessibleUnits);

            return ResolveInflatedIngredientAmount(itemHashId, baseAmount, in positionAup, accessibleUnits);
        }

        public int ResolveInflatedIngredientAmount(int itemHashId, int baseAmount, in AbsoluteUniversePosition worldPosition, int accessibleUnits)
        {
            if (itemHashId == 0 || baseAmount <= 0)
                return Mathf.Max(0, baseAmount);

            float multiplier = Mathf.Max(
                1f + GetSectorCraftInflationScalar(itemHashId, in worldPosition),
                ResolveHoardingIngredientMultiplier(itemHashId, accessibleUnits));
            return Mathf.Max(baseAmount, (int)math.ceil(baseAmount * multiplier));
        }

        private static int ResolveInflatedIngredientAmountWithoutSector(int itemHashId, int baseAmount, int accessibleUnits)
        {
            if (itemHashId == 0 || baseAmount <= 0)
                return Mathf.Max(0, baseAmount);

            float multiplier = ResolveHoardingIngredientMultiplier(itemHashId, accessibleUnits);
            return Mathf.Max(baseAmount, (int)math.ceil(baseAmount * multiplier));
        }

        private static float ResolveHoardingIngredientMultiplier(int itemHashId, int accessibleUnits)
        {
            if (accessibleUnits <= TitaniumHoardingThresholdUnits)
                return 1f;

            return itemHashId == _TitaniumDirectiveHashId
                ? TitaniumHoardingIngredientMultiplier
                : 1f;
        }

        public void OnInteractionEvent(in InteractionEventPayload payload)
        {
            if ((InteractionEventType)payload.EventType != InteractionEventType.ItemCollected ||
                payload.Quantity <= 0 ||
                payload.ItemHashId == 0u)
            {
                return;
            }

            int itemHashId = unchecked((int)payload.ItemHashId);
            if (itemHashId == 0)
                return;

            if (InteractionEvents.TryResolveItem(in payload, out ItemData item) &&
                item != null &&
                !item.isRawResource &&
                item.category != ItemCategory.Material)
            {
                return;
            }

            if (_collectedByItemHash.TryGetValue(itemHashId, out int currentCount))
                _collectedByItemHash[itemHashId] = currentCount + payload.Quantity;
            else
                _collectedByItemHash[itemHashId] = payload.Quantity;

            if (TryResolvePlayerAup(out AbsoluteUniversePosition extractionAup))
            {
                TrackKnownCluster(itemHashId, in extractionAup);
                AccumulateSectorExtraction(itemHashId, in extractionAup, payload.Quantity);
            }

            unchecked
            {
                _runtimeVersion++;
            }
            EvaluateScarcityDirectives();
        }

        /// <inheritdoc />
        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            ref ResourceScarcityDTO dto = ref data.resourceScarcity;
            dto.EnsureCapacity();
            dto.entryCount = 0;

            Dictionary<int, int>.Enumerator enumerator = _collectedByItemHash.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (dto.entryCount >= ResourceScarcityDTO.MaxTrackedResources)
                    break;

                int itemHashId = enumerator.Current.Key;
                _itemIdsByHash.TryGetValue(itemHashId, out string stableItemId);
                dto.itemHashIds[dto.entryCount] = itemHashId;
                dto.itemIds[dto.entryCount] = stableItemId;
                dto.collectedCounts[dto.entryCount] = Mathf.Max(0, enumerator.Current.Value);
                dto.entryCount++;
            }
        }

        /// <inheritdoc />
        public void LoadFromSaveData(SaveData data)
        {
            _collectedByItemHash.Clear();
            _itemIdsByHash.Clear();
            Array.Clear(_knownClusters, 0, _knownClusters.Length);
            Array.Clear(_sectorExtractionRecords, 0, _sectorExtractionRecords.Length);

            if (data == null)
                return;

            ResourceScarcityDTO dto = data.resourceScarcity;
            if (dto.collectedCounts == null || dto.entryCount <= 0)
                return;

            int hashCapacity = dto.itemHashIds != null ? dto.itemHashIds.Length : 0;
            int itemIdCapacity = dto.itemIds != null ? dto.itemIds.Length : 0;
            int count = Mathf.Min(
                dto.entryCount,
                Mathf.Min(Mathf.Max(hashCapacity, itemIdCapacity), dto.collectedCounts.Length));
            for (int i = 0; i < count; i++)
            {
                string stableItemId = i < itemIdCapacity ? dto.itemIds[i] : null;
                int itemHashId = i < hashCapacity ? dto.itemHashIds[i] : 0;
                if (itemHashId == 0 && !string.IsNullOrWhiteSpace(stableItemId))
                    itemHashId = LocHash.Compute(stableItemId);
                if (itemHashId == 0)
                    continue;

                int collectedCount = Mathf.Max(0, dto.collectedCounts[i]);
                if (collectedCount <= 0)
                    continue;

                _collectedByItemHash[itemHashId] = collectedCount;
                if (!string.IsNullOrWhiteSpace(stableItemId))
                    _itemIdsByHash[itemHashId] = stableItemId;
            }

            unchecked
            {
                _runtimeVersion++;
            }
        }

        private void EvaluateScarcityDirectives()
        {
            if (_cachedDirectiveCount <= 0)
                return;

            IQuestSystem questManager = _cachedQuestManager;
            IPlayerInventoryService inventoryService = _cachedInventoryService;
            PlayerInventory inventory = inventoryService != null && inventoryService.IsInitialized
                ? inventoryService.Inventory
                : null;
            if (questManager == null || inventory == null)
                return;

            bool hasPlayerAup = TryResolvePlayerAup(out AbsoluteUniversePosition playerAup);
            for (int definitionIndex = 0; definitionIndex < _cachedDirectiveCount; definitionIndex++)
            {
                int itemHashId = _directiveItemHashes[definitionIndex];
                if (itemHashId == 0)
                    continue;

                uint questHash = _directiveQuestHashes[definitionIndex];
                uint markerTargetHash = _directiveMarkerTargetHashes[definitionIndex];
                Vector3 markerWorldPosition = default;
                if (markerTargetHash == 0u && hasPlayerAup)
                    TryResolveNearestKnownCluster(itemHashId, in playerAup, out markerWorldPosition);

                bool shouldActivate = inventory.CountTotal(itemHashId) < Mathf.Max(0, _directiveCriticalThresholds[definitionIndex]);
                if (!questManager.UpsertProceduralDirective(
                        questHash,
                        (uint)itemHashId,
                        _directiveTitles[definitionIndex],
                        _directiveDescriptions[definitionIndex],
                        markerTargetHash,
                        markerWorldPosition,
                        Mathf.Max(0f, _directiveMarkerHeightOffsets[definitionIndex]),
                        (byte)_directivePhaseGates[definitionIndex],
                        Mathf.Max(1, _directiveHarvestUnits[definitionIndex]),
                        shouldActivate,
                        out bool activatedNow))
                {
                    continue;
                }

                if (activatedNow)
                    Atlas6Events.TryRaiseScarcityDirective(questHash, unchecked((uint)itemHashId));
            }
        }

        private int ResolveDirectiveItemHash(DirectiveResourceDefinition definition)
        {
            return definition.item != null ? definition.item.PersistentHashId : 0;
        }

        private string ResolveDirectiveTitle(DirectiveResourceDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(definition.directiveTitle))
                return definition.directiveTitle;

            return DefaultDirectiveTitleFallback;
        }

        private string ResolveDirectiveDescription(DirectiveResourceDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(definition.directiveDescription))
                return definition.directiveDescription;

            return DefaultDirectiveDescriptionFallback;
        }

        private void TrackKnownCluster(int itemHashId, in AbsoluteUniversePosition worldAup)
        {
            int definitionIndex = FindDirectiveDefinitionIndex(itemHashId);
            if (definitionIndex < 0)
                return;

            int sliceStart = definitionIndex * KnownClustersPerResource;
            int bestSlot = -1;
            double bestDistanceSq = double.MaxValue;
            for (int i = 0; i < KnownClustersPerResource; i++)
            {
                int slot = sliceStart + i;
                ResourceClusterRecord record = _knownClusters[slot];
                if (record.ItemHashId == 0)
                {
                    bestSlot = slot;
                    break;
                }

                if (record.ItemHashId != itemHashId)
                    continue;

                double distanceSq = AbsoluteUniversePosition.DistanceSq(in record.PositionAup, in worldAup);
                if (distanceSq < 64d)
                {
                    record.ObservationCount++;
                    _knownClusters[slot] = record;
                    return;
                }

                if (distanceSq < bestDistanceSq)
                {
                    bestDistanceSq = distanceSq;
                    bestSlot = slot;
                }
            }

            if (bestSlot < 0)
                return;

            _knownClusters[bestSlot] = new ResourceClusterRecord
            {
                ItemHashId = itemHashId,
                ObservationCount = 1,
                PositionAup = worldAup
            };
        }

        private bool TryResolveNearestKnownCluster(int itemHashId, in AbsoluteUniversePosition originAup, out Vector3 clusterPosition)
        {
            clusterPosition = default;
            int definitionIndex = FindDirectiveDefinitionIndex(itemHashId);
            if (definitionIndex < 0)
                return false;

            int sliceStart = definitionIndex * KnownClustersPerResource;
            int bestSlot = -1;
            double bestDistanceSq = double.MaxValue;
            for (int i = 0; i < KnownClustersPerResource; i++)
            {
                int slot = sliceStart + i;
                ResourceClusterRecord record = _knownClusters[slot];
                if (record.ItemHashId != itemHashId)
                    continue;

                double distanceSq = AbsoluteUniversePosition.DistanceSq(in record.PositionAup, in originAup);
                if (distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                bestSlot = slot;
            }

            if (bestSlot < 0)
                return false;

            clusterPosition = ToRuntimePosition(in _knownClusters[bestSlot].PositionAup);
            return true;
        }

        private int FindDirectiveDefinitionIndex(int itemHashId)
        {
            if (itemHashId == 0 || _cachedDirectiveCount <= 0)
                return -1;

            for (int i = 0; i < _cachedDirectiveCount; i++)
            {
                if (_directiveItemHashes[i] == itemHashId)
                    return i;
            }

            return -1;
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext != null && playerContext.PlayerMovement != null)
            {
                playerAup = playerContext.PlayerMovement.CurrentAup;
                return IsFiniteAup(in playerAup);
            }

            playerAup = default;
            return false;
        }

        private static bool TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup)
        {
            originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            return IsFiniteAup(in originAup);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!IsFiniteRuntimePosition(runtimePosition) ||
                !TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup))
            {
                return false;
            }

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return IsFiniteAup(in positionAup);
        }

        private static bool IsFiniteRuntimePosition(Vector3 runtimePosition)
        {
            return math.isfinite(runtimePosition.x) &&
                   math.isfinite(runtimePosition.y) &&
                   math.isfinite(runtimePosition.z);
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition positionAup)
        {
            return math.isfinite(positionAup.LocalX) &&
                   math.isfinite(positionAup.LocalY) &&
                   math.isfinite(positionAup.LocalZ);
        }

        private void AccumulateSectorExtraction(int itemHashId, in AbsoluteUniversePosition worldAup, int quantity)
        {
            if (itemHashId == 0 || quantity <= 0)
                return;

            int sectorKey = PackSectorKey(in worldAup);
            int firstFreeSlot = -1;
            for (int i = 0; i < _sectorExtractionRecords.Length; i++)
            {
                SectorExtractionRecord record = _sectorExtractionRecords[i];
                if (record.ItemHashId == 0)
                {
                    if (firstFreeSlot < 0)
                        firstFreeSlot = i;
                    continue;
                }

                if (record.ItemHashId != itemHashId || record.SectorKey != sectorKey)
                    continue;

                record.ExtractedUnits += quantity;
                _sectorExtractionRecords[i] = record;
                return;
            }

            if (firstFreeSlot < 0)
                return;

            _sectorExtractionRecords[firstFreeSlot] = new SectorExtractionRecord
            {
                ItemHashId = itemHashId,
                SectorKey = sectorKey,
                ExtractedUnits = quantity
            };
        }

        private int GetSectorExtractedUnits(int itemHashId, Vector3 worldPosition)
        {
            if (!TryResolveAupFromRuntimeOrigin(worldPosition, out AbsoluteUniversePosition positionAup))
                return 0;

            return GetSectorExtractedUnits(itemHashId, in positionAup);
        }

        private int GetSectorExtractedUnits(int itemHashId, in AbsoluteUniversePosition worldPosition)
        {
            if (itemHashId == 0)
                return 0;

            int sectorKey = PackSectorKey(in worldPosition);
            for (int i = 0; i < _sectorExtractionRecords.Length; i++)
            {
                SectorExtractionRecord record = _sectorExtractionRecords[i];
                if (record.ItemHashId == itemHashId && record.SectorKey == sectorKey)
                    return record.ExtractedUnits;
            }

            return 0;
        }

        private void TryRegisterSlowTickable()
        {
            if (_registeredSlowTickable || !Application.isPlaying)
                return;

            _registeredSlowTickable = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Core);
        }

        private void TryUnregisterSlowTickable()
        {
            if (!_registeredSlowTickable)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
            _registeredSlowTickable = false;
        }

        private static uint BuildDirectiveQuestHash(int itemHashId)
        {
            unchecked
            {
                uint hash = QuestFlagHashKernel.ComputeStableHash("atlas.directive.scarcity");
                uint value = (uint)itemHashId;
                hash ^= value & 0xFFu;
                hash *= LocHash.FnvPrime;
                hash ^= (value >> 8) & 0xFFu;
                hash *= LocHash.FnvPrime;
                hash ^= (value >> 16) & 0xFFu;
                hash *= LocHash.FnvPrime;
                hash ^= (value >> 24) & 0xFFu;
                hash *= LocHash.FnvPrime;
                return hash;
            }
        }

        private static int PackSectorKey(in AbsoluteUniversePosition aup)
        {
            int localSectorX = math.clamp((int)(aup.LocalX * InverseSectorEdgeLengthMeters), 0, SectorsPerAupCell - 1);
            int localSectorZ = math.clamp((int)(aup.LocalZ * InverseSectorEdgeLengthMeters), 0, SectorsPerAupCell - 1);
            int sectorX = unchecked((int)(aup.GridX * SectorsPerAupCell + localSectorX));
            int sectorZ = unchecked((int)(aup.GridZ * SectorsPerAupCell + localSectorZ));
            unchecked
            {
                return (sectorX * 73856093) ^ (sectorZ * 19349663);
            }
        }

        private static Vector3 ToRuntimePosition(in AbsoluteUniversePosition position)
        {
            float3 runtime = position.ToRuntimeFloat3();
            return new Vector3(runtime.x, runtime.y, runtime.z);
        }

        private void CacheDirectiveDefinitions()
        {
            Array.Clear(_directiveItemHashes, 0, _directiveItemHashes.Length);
            Array.Clear(_directiveQuestHashes, 0, _directiveQuestHashes.Length);
            Array.Clear(_directiveMarkerTargetHashes, 0, _directiveMarkerTargetHashes.Length);
            Array.Clear(_directiveTitles, 0, _directiveTitles.Length);
            Array.Clear(_directiveDescriptions, 0, _directiveDescriptions.Length);
            Array.Clear(_directiveCriticalThresholds, 0, _directiveCriticalThresholds.Length);
            Array.Clear(_directiveHarvestUnits, 0, _directiveHarvestUnits.Length);
            Array.Clear(_directiveMarkerHeightOffsets, 0, _directiveMarkerHeightOffsets.Length);
            Array.Clear(_directivePhaseGates, 0, _directivePhaseGates.Length);

            int writeIndex = 0;
            bool hasTitaniumDirective = false;
            if (directiveResources != null)
            {
                int count = Mathf.Min(directiveResources.Length, MaxDirectiveResources);
                for (int i = 0; i < count && writeIndex < MaxDirectiveResources; i++)
                {
                    DirectiveResourceDefinition definition = directiveResources[i];
                    int itemHashId = ResolveDirectiveItemHash(definition);
                    if (itemHashId == 0)
                        continue;

                    CacheDirectiveDefinition(
                        writeIndex,
                        itemHashId,
                        ResolveDirectiveTitle(definition),
                        ResolveDirectiveDescription(definition),
                        QuestFlagHashKernel.ComputeStableHash(definition.markerTargetId),
                        Mathf.Max(0, definition.criticalThreshold),
                        Mathf.Max(1, definition.directiveHarvestUnits),
                        Mathf.Max(0f, definition.markerHeightOffset),
                        definition.phaseGate);

                    if (itemHashId == _TitaniumDirectiveHashId)
                        hasTitaniumDirective = true;

                    writeIndex++;
                }
            }

            if (!hasTitaniumDirective && writeIndex < MaxDirectiveResources)
            {
                int titaniumHashId = LocHash.Compute(DefaultTitaniumDirectiveItemId);
                CacheDirectiveDefinition(
                    writeIndex,
                    titaniumHashId,
                    "ATLAS-6 DIRECTIVE: RESTOCK TITANIUM SCRAP",
                    "Recovered titanium stock is below Atlas-6 operating threshold. Harvest additional Titanium Scrap to stabilize fabrication reserves.",
                    0u,
                    DefaultTitaniumCriticalThreshold,
                    DefaultTitaniumDirectiveHarvestUnits,
                    0f,
                    QuestPhaseGateType.None);
                writeIndex++;
            }

            _cachedDirectiveCount = writeIndex;
        }

        private void CacheDirectiveDefinition(
            int index,
            int itemHashId,
            string title,
            string description,
            uint markerTargetHash,
            int criticalThreshold,
            int directiveHarvestUnits,
            float markerHeightOffset,
            QuestPhaseGateType phaseGate)
        {
            if (index < 0 || index >= MaxDirectiveResources || itemHashId == 0)
                return;

            _directiveItemHashes[index] = itemHashId;
            _directiveQuestHashes[index] = BuildDirectiveQuestHash(itemHashId);
            _directiveMarkerTargetHashes[index] = markerTargetHash;
            _directiveTitles[index] = string.IsNullOrWhiteSpace(title) ? "ATLAS-6 DIRECTIVE" : title;
            _directiveDescriptions[index] = description ?? string.Empty;
            _directiveCriticalThresholds[index] = Mathf.Max(0, criticalThreshold);
            _directiveHarvestUnits[index] = Mathf.Max(1, directiveHarvestUnits);
            _directiveMarkerHeightOffsets[index] = Mathf.Max(0f, markerHeightOffset);
            _directivePhaseGates[index] = phaseGate;
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterResourceScarcityRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.ResourceScarcity, this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterResourceScarcityRuntime(this);
            _serviceRegistered = false;
        }

        private void CacheRegistryServicesCold()
        {
            _cachedSaveService = GlobalRegistry.Save;
            _cachedQuestManager = GlobalRegistry.QuestSystem;
            _cachedInventoryService = GlobalRegistry.PlayerInventory;
            _cachedPlayerContext = Hecton8.Core.GlobalRegistry.Player;
        }

        private void TryRegisterWithSaveManager()
        {
            if (_saveServiceRegistered)
                return;

            ISaveService saveService = _cachedSaveService;
            if (saveService == null)
                return;

            saveService.Register(this);
            _saveServiceRegistered = true;
        }

        private void TryUnregisterFromSaveManager()
        {
            if (!_saveServiceRegistered)
                return;

            ISaveService saveService = _cachedSaveService;
            if (saveService != null)
                saveService.Unregister(this);
            _saveServiceRegistered = false;
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
    }
}

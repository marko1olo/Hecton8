using System;
using System.Collections.Generic;
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
using UnityEngine;

namespace Hecton8.Economy
{
    /// <summary>
    /// Tracks cumulative resource extraction, issues scarcity directives, and exposes sector-local deflation scalars.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-6250)]
    [AddComponentMenu("Hecton8/Economy/Resource Scarcity Director")]
    public sealed class ResourceScarcityDirector : MonoBehaviour, ISaveable, ISlowTickable, IInteractionEventListener
    {
        private const int InitialTrackedCapacity = 64;
        private const int UnitsPerScarcityStep = 100;
        private const float ScarcityStepMultiplier = 0.04f;
        private const float MaxIngredientMultiplier = 1.80f;
        private const int MaxDirectiveResources = 8;
        private const int KnownClustersPerResource = 4;
        private const int MaxSectorExtractionRecords = 64;
        private const float SectorEdgeLengthMeters = 1000f;
        private const string DefaultTitaniumDirectiveItemId = "Data_TitaniumScrap";
        private const int TitaniumHoardingThresholdUnits = 500;
        private const float TitaniumHoardingIngredientMultiplier = 4f;
        private const int DefaultTitaniumCriticalThreshold = 4;
        private const int DefaultTitaniumDirectiveHarvestUnits = 4;
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

        private struct ResourceClusterRecord
        {
            public int ItemHashId;
            public int ObservationCount;
            public Vector3 Position;
        }

        private struct SectorExtractionRecord
        {
            public int ItemHashId;
            public int SectorKey;
            public int ExtractedUnits;
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
        private int _cachedDirectiveCount;

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
            TryRegisterService();
            Hecton8.Core.GlobalRegistry.SaveRuntime?.Register(this);
            TryRegisterSlowTickable();
            InteractionEvents.Register(this);
            CacheDirectiveDefinitions();
        }

        private void OnDisable()
        {
            Hecton8.Core.GlobalRegistry.SaveRuntime?.Unregister(this);
            TryUnregisterSlowTickable();
            InteractionEvents.Unregister(this);
            TryUnregisterService();
        }

        private void OnDestroy()
        {
            Hecton8.Core.GlobalRegistry.SaveRuntime?.Unregister(this);
            TryUnregisterSlowTickable();
            InteractionEvents.Unregister(this);
            TryUnregisterService();
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

                float ingredientMultiplier = GetIngredientMultiplier(cost.item.PersistentId);
                weightedSum += ingredientMultiplier * cost.amount;
                totalIngredientUnits += cost.amount;
            }

            if (totalIngredientUnits <= 0)
                return 1f;

            return weightedSum / totalIngredientUnits;
        }

        /// <summary>
        /// Returns the current scarcity multiplier for a single resource item.
        /// </summary>
        public float GetIngredientMultiplier(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return 1f;

            int itemHashId = LocHash.Compute(itemId);
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
            if (inflationProfile == null || itemHashId == 0)
                return 1f;

            return inflationProfile.EvaluateSpawnRateScalar(itemHashId, GetSectorExtractedUnits(itemHashId, worldPosition));
        }

        /// <summary>
        /// Returns the sector-local value scalar for one resource.
        /// </summary>
        public float GetSectorValueScalar(int itemHashId, Vector3 worldPosition)
        {
            if (inflationProfile == null || itemHashId == 0)
                return 1f;

            return inflationProfile.EvaluateValueScalar(itemHashId, GetSectorExtractedUnits(itemHashId, worldPosition));
        }

        /// <summary>
        /// Returns the sector-local crafting surcharge ratio for one resource.
        /// </summary>
        public float GetSectorCraftInflationScalar(int itemHashId, Vector3 worldPosition)
        {
            if (inflationProfile == null || itemHashId == 0)
                return 0f;

            return inflationProfile.EvaluateCraftInflationScalar(itemHashId, GetSectorExtractedUnits(itemHashId, worldPosition));
        }

        /// <summary>
        /// Resolves the inflated ingredient amount for the supplied sector.
        /// </summary>
        public int ResolveInflatedIngredientAmount(int itemHashId, int baseAmount, Vector3 worldPosition)
        {
            return ResolveInflatedIngredientAmount(itemHashId, baseAmount, worldPosition, 0);
        }

        /// <summary>
        /// Resolves the inflated ingredient amount using sector extraction and accessible-storage hoarding pressure.
        /// </summary>
        public int ResolveInflatedIngredientAmount(int itemHashId, int baseAmount, Vector3 worldPosition, int accessibleUnits)
        {
            if (itemHashId == 0 || baseAmount <= 0)
                return Mathf.Max(0, baseAmount);

            float multiplier = Mathf.Max(
                1f + GetSectorCraftInflationScalar(itemHashId, worldPosition),
                ResolveHoardingIngredientMultiplier(itemHashId, accessibleUnits));
            return Mathf.Max(baseAmount, Mathf.CeilToInt(baseAmount * multiplier));
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
                !InteractionEvents.TryResolveItem(in payload, out ItemData item) ||
                item == null)
            {
                return;
            }

            if (!item.isRawResource && item.category != ItemCategory.Material)
                return;

            int itemHashId = payload.ItemHashId != 0u
                ? unchecked((int)payload.ItemHashId)
                : LocHash.Compute(item.PersistentId);
            if (itemHashId == 0)
                return;

            if (_collectedByItemHash.TryGetValue(itemHashId, out int currentCount))
                _collectedByItemHash[itemHashId] = currentCount + payload.Quantity;
            else
                _collectedByItemHash[itemHashId] = payload.Quantity;

            _itemIdsByHash[itemHashId] = item.PersistentId;

            if (InteractionEvents.TryResolveInteractor(in payload, out Transform interactor) &&
                interactor != null)
            {
                Vector3 position = interactor.position;
                TrackKnownCluster(itemHashId, position);
                AccumulateSectorExtraction(itemHashId, position, payload.Quantity);
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
                if (!_itemIdsByHash.TryGetValue(itemHashId, out string itemId) || string.IsNullOrWhiteSpace(itemId))
                    continue;

                dto.itemIds[dto.entryCount] = itemId;
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
            if (dto.itemIds == null || dto.collectedCounts == null || dto.entryCount <= 0)
                return;

            int count = Mathf.Min(dto.entryCount, dto.itemIds.Length, dto.collectedCounts.Length);
            for (int i = 0; i < count; i++)
            {
                string itemId = dto.itemIds[i];
                if (string.IsNullOrWhiteSpace(itemId))
                    continue;

                int itemHashId = LocHash.Compute(itemId);
                if (itemHashId == 0)
                    continue;

                int collectedCount = Mathf.Max(0, dto.collectedCounts[i]);
                if (collectedCount <= 0)
                    continue;

                _collectedByItemHash[itemHashId] = collectedCount;
                _itemIdsByHash[itemHashId] = itemId;
            }
        }

        private void EvaluateScarcityDirectives()
        {
            if (_cachedDirectiveCount <= 0)
                return;

            QuestManager questManager = GlobalRegistry.Quest;
            IPlayerInventoryService inventoryService = GlobalRegistry.PlayerInventory;
            PlayerInventory inventory = inventoryService != null && inventoryService.IsInitialized
                ? inventoryService.Inventory
                : null;
            if (questManager == null || inventory == null)
                return;

            Transform playerTransform = GlobalRegistry.Player != null ? GlobalRegistry.Player.PlayerTransform : null;
            Vector3 playerPosition = playerTransform != null ? playerTransform.position : Vector3.zero;
            for (int definitionIndex = 0; definitionIndex < _cachedDirectiveCount; definitionIndex++)
            {
                int itemHashId = _directiveItemHashes[definitionIndex];
                if (itemHashId == 0)
                    continue;

                uint questHash = _directiveQuestHashes[definitionIndex];
                uint markerTargetHash = _directiveMarkerTargetHashes[definitionIndex];
                Vector3 markerWorldPosition = default;
                if (markerTargetHash == 0u)
                    TryResolveNearestKnownCluster(itemHashId, playerPosition, out markerWorldPosition);

                bool shouldActivate = inventory.CountTotal(itemHashId) < Mathf.Max(0, _directiveCriticalThresholds[definitionIndex]);
                if (!questManager.UpsertProceduralDirective(
                        questHash,
                        (uint)itemHashId,
                        _directiveTitles[definitionIndex],
                        _directiveDescriptions[definitionIndex],
                        markerTargetHash,
                        markerWorldPosition,
                        Mathf.Max(0f, _directiveMarkerHeightOffsets[definitionIndex]),
                        _directivePhaseGates[definitionIndex],
                        Mathf.Max(1, _directiveHarvestUnits[definitionIndex]),
                        shouldActivate,
                        out bool activatedNow))
                {
                    continue;
                }

                if (activatedNow)
                    Atlas6Events.RaiseScarcityDirective(questHash, unchecked((uint)itemHashId));
            }
        }

        private int ResolveDirectiveItemHash(DirectiveResourceDefinition definition)
        {
            return definition.item != null && !string.IsNullOrWhiteSpace(definition.item.PersistentId)
                ? LocHash.Compute(definition.item.PersistentId)
                : 0;
        }

        private string ResolveDirectiveTitle(DirectiveResourceDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(definition.directiveTitle))
                return definition.directiveTitle;

            string itemName = definition.item != null ? definition.item.itemName : "RESOURCE";
            return $"ATLAS-6 DIRECTIVE: RESTOCK {itemName.ToUpperInvariant()}";
        }

        private string ResolveDirectiveDescription(DirectiveResourceDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(definition.directiveDescription))
                return definition.directiveDescription;

            string itemName = definition.item != null ? definition.item.itemName : "critical structural stock";
            return $"Recovered stock is below Atlas-6 operating threshold. Harvest additional {itemName} to stabilize fabrication reserves.";
        }

        private void TrackKnownCluster(int itemHashId, Vector3 worldPosition)
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

                double distanceSq = ResolveAupDistanceSq(record.Position, worldPosition);
                if (distanceSq < 64d)
                {
                    record.ObservationCount++;
                    record.Position = Vector3.Lerp(record.Position, worldPosition, 1f / Mathf.Max(1, record.ObservationCount));
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
                Position = worldPosition
            };
        }

        private bool TryResolveNearestKnownCluster(int itemHashId, Vector3 origin, out Vector3 clusterPosition)
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

                double distanceSq = ResolveAupDistanceSq(record.Position, origin);
                if (distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                bestSlot = slot;
            }

            if (bestSlot < 0)
                return false;

            clusterPosition = _knownClusters[bestSlot].Position;
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

        private void AccumulateSectorExtraction(int itemHashId, Vector3 worldPosition, int quantity)
        {
            if (itemHashId == 0 || quantity <= 0)
                return;

            int sectorKey = PackSectorKey(worldPosition);
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
            if (itemHashId == 0)
                return 0;

            int sectorKey = PackSectorKey(worldPosition);
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

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Core);
            _registeredSlowTickable = GlobalRegistry.SlowTickables.Contains(this);
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

        private static int PackSectorKey(Vector3 runtimePosition)
        {
            AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
            var absolutePosition = aup.ToAbsoluteDouble3();
            int sectorX = (int)Math.Floor(absolutePosition.x / SectorEdgeLengthMeters);
            int sectorZ = (int)Math.Floor(absolutePosition.z / SectorEdgeLengthMeters);
            unchecked
            {
                return (sectorX * 73856093) ^ (sectorZ * 19349663);
            }
        }

        private static double ResolveAupDistanceSq(Vector3 fromRuntimePosition, Vector3 toRuntimePosition)
        {
            AbsoluteUniversePosition fromAup = AbsoluteUniversePosition.FromRuntimePosition(fromRuntimePosition);
            AbsoluteUniversePosition toAup = AbsoluteUniversePosition.FromRuntimePosition(toRuntimePosition);
            return AbsoluteUniversePosition.DistanceSq(in fromAup, in toAup);
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

                    if (string.Equals(definition.item.PersistentId, DefaultTitaniumDirectiveItemId, StringComparison.Ordinal))
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
    }
}

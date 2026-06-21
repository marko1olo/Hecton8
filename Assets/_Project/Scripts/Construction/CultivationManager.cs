using System;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Memory.Layout;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.SaveSystem;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Construction
{
    /// <summary>
    /// Fixed-slot cultivation owner that persists hybrid seed genetics and routes mature-plant side effects
    /// into the existing base-module atmosphere, power, and hazard systems.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CultivationManager : MonoBehaviour, ISlowTickable, IGlobalRegistryHotSwapListener
    {
        private const int MaxCultivationSlots = ModuleDTO.MaxCultivationSlots;
        private const float SlowTickDt = 0.5f;
        private const float GrowthDurationSeconds = 20f * 60f;
        private const float MatureThreshold = 0.999f;
        private const float MinimumOperationalSupplyRatio = 0.98f;
        private const ulong GeneBioluminescent = (ulong)GeneticTraitProfile.GeneticTraitMask.Bioluminescent;
        private const ulong GeneOxygenProducing = (ulong)GeneticTraitProfile.GeneticTraitMask.OxygenProducing;
        private const ulong GeneToxic = (ulong)GeneticTraitProfile.GeneticTraitMask.Toxic;
        private const ulong GeneRapidGrowth = (ulong)GeneticTraitProfile.GeneticTraitMask.FastGrowing;
        private const ulong DefinedCultivationGeneMask = ModuleDTO.CultivationGeneticsSupportedMask;
        private const ulong SpliceMutationGeneMask = (GeneBioluminescent | GeneOxygenProducing | GeneToxic | GeneRapidGrowth) & DefinedCultivationGeneMask;
        /// <summary>
        /// Fixed cultivation slot payload shared with atmosphere jobs without managed allocation.
        /// </summary>
        [BinaryBlittableSafe]
        [Serializable]
        [StructLayout(LayoutKind.Explicit, Size = 32)]
        public struct CultivationSlotState
        {
            [FieldOffset(0)]
            public ulong GeneticsMask;
            [FieldOffset(8)]
            public int SeedItemHashId;
            [FieldOffset(12)]
            public float Growth01;
            [FieldOffset(16)]
            public float Quality01;
            [FieldOffset(20)]
            private uint _pad0;
            [FieldOffset(24)]
            private ulong _pad1;
        }

        [StructLayout(LayoutKind.Explicit, Size = 8)]
        private struct XorShift32State
        {
            [FieldOffset(0)]
            private uint _state;
            [FieldOffset(4)]
            private uint _pad0;

            public XorShift32State(uint seed)
            {
                _state = seed != 0u ? seed : 0x6D2B79F5u;
                _pad0 = 0u;
            }

            public uint NextUInt()
            {
                uint value = _state;
                value ^= value << 13;
                value ^= value >> 17;
                value ^= value << 5;
                _state = value != 0u ? value : 0x6D2B79F5u;
                return _state;
            }
        }

        [Header("── Target ──────────────────")]
        [Tooltip("Base module whose atmosphere and power loop this cultivation rack mutates.")]
        [SerializeField] private BaseModule targetModule;

        [Tooltip("Existing planter interaction shell that should delegate into this runtime when present.")]
        [SerializeField] private BotanyPlanterModule planterBridge;

        [Header("── Genetics ─────────────────")]
        [Tooltip("Authored flora templates used to resolve default seed genetics and hybrid output seed items.")]
        [SerializeField] private FloraDataTemplate[] floraTemplates = Array.Empty<FloraDataTemplate>();

        [Tooltip("Optional authored trait profile that maps cultivation bits to runtime buffs and debuffs.")]
        [SerializeField] private GeneticTraitProfile geneticTraitProfile;

        [Header("── Atmosphere ───────────────")]
        [Tooltip("Fallback oxygen contribution per mature oxygen-producing trait when no authored trait profile is assigned.")]
        [SerializeField, Min(0f)] private float fallbackOxygenUnitsPerSlowTick = 0.45f;

        [Tooltip("Supplemental CO2 scrub amount per mature oxygen-producing trait.")]
        [SerializeField, Min(0f)] private float scrubAmountPerOxygenTrait = 0.18f;

        [Tooltip("Fallback scrubber power draw in watts per mature toxic trait before the 2x cultivation penalty is applied.")]
        [SerializeField, Min(0f)] private float fallbackToxicScrubberPowerWatts = 8f;

        [Tooltip("Fallback lighting power credit in watts per mature bioluminescent trait when no authored trait profile is assigned.")]
        [SerializeField, Min(0f)] private float fallbackBiolumLightingPowerCreditWatts = 4f;

        [Header("── Hazard ───────────────────")]
        [Tooltip("Optional authored hazard profile used when toxic cultivation overwhelms local scrubbers.")]
        [SerializeField] private HazardZoneProfile toxicHazardProfile;

        [Tooltip("Fallback normalized hazard intensity used when no authored profile or trait row supplies one.")]
        [SerializeField, Range(0f, 1f)] private float fallbackHazardIntensity = 0.72f;

        [Tooltip("Fallback hazard radius in meters used when no authored profile or trait row supplies one.")]
        [SerializeField, Min(0.25f)] private float fallbackHazardRadiusMeters = 2.6f;

        [Tooltip("Normalized toxic rot pulse emitted per dead non-aquatic plant into flooded module water.")]
        [SerializeField, Range(0f, 1f)] private float floodedRotIntensityPerDeadPlant = 0.28f;

        [Tooltip("Flooded-water rot hazard radius in meters when dead cultivated plants decay in saltwater.")]
        [SerializeField, Min(0.25f)] private float floodedRotHazardRadiusMeters = 2.4f;

        [Tooltip("CO2/flood exposure amplifier applied when dead cultivated plants rot in saltwater.")]
        [SerializeField, Min(0f)] private float floodedRotCo2Amplifier = 1.4f;

        [Tooltip("Scrubber load multiplier applied while flooded dead plants rot inside the module.")]
        [SerializeField, Min(1f)] private float floodedRotScrubberLoadMultiplier = 3f;

        [Header("── Diagnostics ──────────────")]
        [SerializeField] private int _debugOccupiedSlotCount;
        [SerializeField] private int _debugMatureSlotCount;
        [SerializeField] private uint _debugCombinedTraitMask;
        [SerializeField] private float _debugScrubberLoadWatts;
        [SerializeField] private float _debugLightingCreditWatts;
        [SerializeField] private int _debugDeadSlotCount;
        [SerializeField] private bool _debugHazardActive;

        private readonly CultivationSlotState[] _slots = new CultivationSlotState[MaxCultivationSlots];
        private bool _registered;
        private IPlayerInventoryService _cachedInventoryService;
        private HazardZoneManager _cachedHazardZones;
        private bool _hotSwapListenerRegistered;
        private uint _slowTickSequence;
        private int _hazardZoneId;
        private int _rotHazardZoneId;

        /// <summary>True when at least one cultivation slot is occupied.</summary>
        public bool HasCultivatedPlants => OccupiedSlotCount > 0;

        /// <summary>Current occupied slot count.</summary>
        public int OccupiedSlotCount
        {
            get
            {
                if (_slots == null)
                    return 0;

                int count = 0;
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (_slots[i].SeedItemHashId != 0)
                        count++;
                }

                return count;
            }
        }

        private void Awake()
        {
            if (targetModule == null)
                ConstructionParentLookup.TryCaptureSelfOrParent(this, out targetModule);

            if (planterBridge == null)
                TryGetComponent(out planterBridge);

            ClearSlots();
            _hazardZoneId = unchecked((int)EntityId.ToULong(GetEntityId()) * 397) ^ 0x43554C54;
            _rotHazardZoneId = _hazardZoneId ^ 0x524F54;
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            TryRegister();
        }

        private void OnDisable()
        {
            ClearHazardState();
            ClearRotHazardState();
            if (targetModule != null)
            {
                targetModule.SetCultivationScrubberLoad(0f);
                targetModule.SetCultivationLightingPowerCredit(0f);
            }

            TryUnregister();
            TryUnregisterHotSwapListener();
            ClearCachedRegistryServices();
        }

        private void OnDestroy()
        {
            ClearHazardState();
            ClearRotHazardState();
            if (targetModule != null)
            {
                targetModule.SetCultivationScrubberLoad(0f);
                targetModule.SetCultivationLightingPowerCredit(0f);
            }

            TryUnregister();
            TryUnregisterHotSwapListener();
            ClearCachedRegistryServices();
            ClearSlots();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.PlayerInventory)
            {
                _cachedInventoryService = currentService as IPlayerInventoryService;
            }
            else if (serviceSlot == GlobalRegistryServiceSlot.HazardZoneRuntime)
            {
                ClearHazardState(previousService as HazardZoneManager);
                ClearRotHazardState(previousService as HazardZoneManager);
                _cachedHazardZones = currentService as HazardZoneManager;
            }
        }

        /// <summary>
        /// Deterministically cross-breeds two seed items and returns a new hybrid seed item with a persisted genetics mask.
        /// </summary>
        public bool TrySpliceSeeds(PlayerInventory inventory, int seedItemHashIdA, int seedItemHashIdB, out ulong resultMask, out int outputSeedItemHashId)
        {
            resultMask = 0UL;
            outputSeedItemHashId = 0;
            if (inventory == null || seedItemHashIdA == 0 || seedItemHashIdB == 0)
                return false;

            if (!inventory.TryConsumeFirstMatchingItemByHash(seedItemHashIdA, out _, out _, out ulong geneticsMaskA))
                return false;

            if (!inventory.TryConsumeFirstMatchingItemByHash(seedItemHashIdB, out _, out _, out ulong geneticsMaskB))
            {
                inventory.TryAddItemWithGenetics(seedItemHashIdA, ResolveEffectiveGeneticsMask(seedItemHashIdA, geneticsMaskA));
                return false;
            }

            geneticsMaskA = ResolveEffectiveGeneticsMask(seedItemHashIdA, geneticsMaskA);
            geneticsMaskB = ResolveEffectiveGeneticsMask(seedItemHashIdB, geneticsMaskB);

            uint seed = (_slowTickSequence != 0u ? _slowTickSequence : 1u) ^
                unchecked((uint)seedItemHashIdA) ^
                (unchecked((uint)seedItemHashIdB) * 0x9E3779B9u);
            XorShift32State mutationRng = new XorShift32State(seed);
            ulong mutationMask = ((ulong)mutationRng.NextUInt()) & SpliceMutationGeneMask;
            resultMask = ((geneticsMaskA | geneticsMaskB) ^ mutationMask) & DefinedCultivationGeneMask;

            outputSeedItemHashId = ResolveHybridSeedItemHash(seedItemHashIdA, seedItemHashIdB, resultMask);
            if (outputSeedItemHashId == 0 || !inventory.TryAddItemWithGenetics(outputSeedItemHashId, resultMask))
            {
                inventory.TryAddItemWithGenetics(seedItemHashIdA, geneticsMaskA);
                inventory.TryAddItemWithGenetics(seedItemHashIdB, geneticsMaskB);
                resultMask = 0UL;
                outputSeedItemHashId = 0;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Plants one seed from inventory into the first free cultivation slot.
        /// </summary>
        public bool TryPlantSeedFromInventory(PlayerInventory inventory, int seedItemHashId)
        {
            if (inventory == null || seedItemHashId == 0)
                return false;

            if (!TryGetEmptySlotIndex(out int slotIndex))
                return false;

            if (!inventory.TryConsumeFirstMatchingItemByHash(seedItemHashId, out _, out _, out ulong geneticsMask))
                return false;

            _slots[slotIndex] = new CultivationSlotState
            {
                SeedItemHashId = seedItemHashId,
                GeneticsMask = ResolveEffectiveGeneticsMask(seedItemHashId, geneticsMask),
                Growth01 = 0.02f,
                Quality01 = 1f
            };

            return true;
        }

        /// <summary>
        /// Copies a lightweight UI snapshot into caller-owned buffers.
        /// </summary>
        public int CopyBufferSnapshot(ItemData[] items, int[] quantities, ItemCatalog itemCatalog = null)
        {
            if (items == null || quantities == null || _slots == null)
                return 0;

            int copyCount = math.min(items.Length, quantities.Length);
            int written = 0;
            ItemCatalog resolvedCatalog = itemCatalog != null ? itemCatalog : ResolveItemCatalog();
            for (int i = 0; i < _slots.Length && written < copyCount; i++)
            {
                CultivationSlotState slot = _slots[i];
                if (slot.SeedItemHashId == 0)
                    continue;

                ItemData item = resolvedCatalog != null ? resolvedCatalog.FindByHash(slot.SeedItemHashId) : null;
                if (item == null)
                    continue;

                items[written] = item;
                quantities[written] = 1;
                written++;
            }

            return written;
        }

        /// <summary>
        /// Copies genetics and growth data into caller-owned buffers for cultivation UI rendering.
        /// </summary>
        public int CopyTraitSnapshot(uint[] geneticsMasks, float[] growthValues)
        {
            if (geneticsMasks == null || growthValues == null || _slots == null)
                return 0;

            int copyCount = math.min(math.min(geneticsMasks.Length, growthValues.Length), _slots.Length);
            for (int i = 0; i < copyCount; i++)
            {
                geneticsMasks[i] = unchecked((uint)_slots[i].GeneticsMask);
                growthValues[i] = NormalizeGrowth01(_slots[i].Growth01);
            }

            return copyCount;
        }

        /// <summary>
        /// Copies 64-bit genetics and growth data into caller-owned buffers for cultivation UI rendering.
        /// </summary>
        public int CopyTraitSnapshot(ulong[] geneticsMasks, float[] growthValues)
        {
            if (geneticsMasks == null || growthValues == null || _slots == null)
                return 0;

            int copyCount = math.min(math.min(geneticsMasks.Length, growthValues.Length), _slots.Length);
            for (int i = 0; i < copyCount; i++)
            {
                geneticsMasks[i] = _slots[i].GeneticsMask;
                growthValues[i] = NormalizeGrowth01(_slots[i].Growth01);
            }

            return copyCount;
        }

        /// <summary>
        /// Copies genetics, growth, and quality data into caller-owned buffers for cultivation UI rendering.
        /// </summary>
        public int CopyTraitSnapshot(uint[] geneticsMasks, float[] growthValues, float[] qualityValues)
        {
            if (geneticsMasks == null || growthValues == null || qualityValues == null || _slots == null)
                return 0;

            int copyCount = math.min(math.min(math.min(geneticsMasks.Length, growthValues.Length), qualityValues.Length), _slots.Length);
            for (int i = 0; i < copyCount; i++)
            {
                geneticsMasks[i] = unchecked((uint)_slots[i].GeneticsMask);
                growthValues[i] = NormalizeGrowth01(_slots[i].Growth01);
                qualityValues[i] = NormalizeQuality01(_slots[i].Quality01);
            }

            return copyCount;
        }

        /// <summary>
        /// Copies 64-bit genetics, growth, and quality data into caller-owned buffers for cultivation UI rendering.
        /// </summary>
        public int CopyTraitSnapshot(ulong[] geneticsMasks, float[] growthValues, float[] qualityValues)
        {
            if (geneticsMasks == null || growthValues == null || qualityValues == null || _slots == null)
                return 0;

            int copyCount = math.min(math.min(math.min(geneticsMasks.Length, growthValues.Length), qualityValues.Length), _slots.Length);
            for (int i = 0; i < copyCount; i++)
            {
                geneticsMasks[i] = _slots[i].GeneticsMask;
                growthValues[i] = NormalizeGrowth01(_slots[i].Growth01);
                qualityValues[i] = NormalizeQuality01(_slots[i].Quality01);
            }

            return copyCount;
        }

        /// <summary>
        /// Sums mature oxygen-producing slots without exposing a retained native or managed collection view.
        /// </summary>
        internal float CalculateMatureOxygenUnits(float oxygenUnitsPerMaturePlant)
        {
            if (oxygenUnitsPerMaturePlant <= 0f || _slots == null)
                return 0f;

            float oxygenUnits = 0f;
            for (int i = 0; i < _slots.Length; i++)
            {
                CultivationSlotState slot = _slots[i];
                if (slot.SeedItemHashId == 0 ||
                    slot.Growth01 < MatureThreshold ||
                    slot.Quality01 <= 0f ||
                    (slot.GeneticsMask & GeneOxygenProducing) == 0UL)
                {
                    continue;
                }

                oxygenUnits += oxygenUnitsPerMaturePlant;
            }

            return oxygenUnits;
        }

        /// <summary>
        /// Persists cultivation slots into the construction module DTO.
        /// </summary>
        public void PopulateSaveData(ref ModuleDTO moduleDto, ItemCatalog itemCatalog)
        {
            moduleDto.cultivationSlotCount = 0;

            if (_slots == null || !moduleDto.HasCultivationSaveCapacity())
                return;

            string[] seedIds = moduleDto.cultivationSeedItemIds;
            int[] seedHashIds = moduleDto.cultivationSeedItemHashIds;
            ulong[] geneticsMasks = moduleDto.cultivationGeneticsMasks;
            float[] growthValues = moduleDto.cultivationGrowth01;
            float[] qualityValues = moduleDto.cultivationQuality01;
            int writeIndex = 0;

            for (int i = 0; i < _slots.Length && writeIndex < MaxCultivationSlots; i++)
            {
                CultivationSlotState slot = _slots[i];
                if (slot.SeedItemHashId == 0)
                    continue;

                ItemData item = itemCatalog != null ? itemCatalog.FindByHash(slot.SeedItemHashId) : null;
                seedIds[writeIndex] = item != null && !string.IsNullOrWhiteSpace(item.PersistentId)
                    ? item.PersistentId.Trim()
                    : string.Empty;
                seedHashIds[writeIndex] = slot.SeedItemHashId;
                geneticsMasks[writeIndex] = SanitizeGeneticsMask(slot.GeneticsMask);
                growthValues[writeIndex] = NormalizeGrowth01(slot.Growth01);
                qualityValues[writeIndex] = NormalizeQuality01(slot.Quality01);
                writeIndex++;
            }

            if (writeIndex <= 0)
                return;

            moduleDto.cultivationSlotCount = writeIndex;
        }

        /// <summary>
        /// Restores cultivation slots from the construction module DTO.
        /// </summary>
        public void RestoreFromSaveData(ModuleDTO moduleDto, ItemCatalog itemCatalog)
        {
            if (_slots == null)
                return;

            int safeCount = ResolveCultivationRestoreCount(in moduleDto);
            if (safeCount <= 0 || !HasSavedCultivationRestoreState(in moduleDto, safeCount))
            {
                ClearSlots();
                return;
            }

            if (!CanResolveCultivationRestoreState(in moduleDto, itemCatalog, safeCount))
                return;

            ClearSlots();
            for (int i = 0; i < safeCount; i++)
            {
                string persistentId = GetSavedCultivationSeedItemId(in moduleDto, i);
                int itemHashId = ResolveSavedCultivationSeedHashId(in moduleDto, itemCatalog, i, persistentId);
                if (itemHashId == 0)
                    continue;

                _slots[i] = new CultivationSlotState
                {
                    SeedItemHashId = itemHashId,
                    GeneticsMask = ResolveSavedGeneticsMask(moduleDto, i, itemHashId),
                    Growth01 = ResolveSavedGrowth01(moduleDto, i),
                    Quality01 = moduleDto.cultivationQuality01 != null && i < moduleDto.cultivationQuality01.Length
                        ? NormalizeQuality01(moduleDto.cultivationQuality01[i])
                        : 1f
                };
            }
        }

        private static int ResolveCultivationRestoreCount(in ModuleDTO moduleDto)
        {
            if (moduleDto.cultivationSlotCount <= 0)
                return 0;

            int identityCapacity = math.max(
                moduleDto.cultivationSeedItemIds != null ? moduleDto.cultivationSeedItemIds.Length : 0,
                moduleDto.cultivationSeedItemHashIds != null ? moduleDto.cultivationSeedItemHashIds.Length : 0);
            return math.min(math.min(moduleDto.cultivationSlotCount, MaxCultivationSlots), identityCapacity);
        }

        private static bool HasSavedCultivationRestoreState(in ModuleDTO moduleDto, int safeCount)
        {
            for (int i = 0; i < safeCount; i++)
            {
                if (!string.IsNullOrWhiteSpace(GetSavedCultivationSeedItemId(in moduleDto, i)) ||
                    GetSavedCultivationSeedHashId(in moduleDto, i) != 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CanResolveCultivationRestoreState(
            in ModuleDTO moduleDto,
            ItemCatalog itemCatalog,
            int safeCount)
        {
            for (int i = 0; i < safeCount; i++)
            {
                string persistentId = GetSavedCultivationSeedItemId(in moduleDto, i);
                int savedHashId = GetSavedCultivationSeedHashId(in moduleDto, i);
                if (string.IsNullOrWhiteSpace(persistentId))
                {
                    if (savedHashId == 0)
                        return false;

                    continue;
                }

                if (itemCatalog != null && itemCatalog.FindById(persistentId.Trim()) != null)
                    continue;

                if (savedHashId == 0)
                    return false;
            }

            return true;
        }

        private static string GetSavedCultivationSeedItemId(in ModuleDTO moduleDto, int slotIndex)
        {
            if (moduleDto.cultivationSeedItemIds == null ||
                slotIndex < 0 ||
                slotIndex >= moduleDto.cultivationSeedItemIds.Length)
            {
                return string.Empty;
            }

            return moduleDto.cultivationSeedItemIds[slotIndex];
        }

        private static int GetSavedCultivationSeedHashId(in ModuleDTO moduleDto, int slotIndex)
        {
            if (moduleDto.cultivationSeedItemHashIds == null ||
                slotIndex < 0 ||
                slotIndex >= moduleDto.cultivationSeedItemHashIds.Length)
            {
                return 0;
            }

            return moduleDto.cultivationSeedItemHashIds[slotIndex];
        }

        private static int ResolveSavedCultivationSeedHashId(
            in ModuleDTO moduleDto,
            ItemCatalog itemCatalog,
            int slotIndex,
            string persistentId)
        {
            if (!string.IsNullOrWhiteSpace(persistentId) && itemCatalog != null)
            {
                ItemData item = itemCatalog.FindById(persistentId.Trim());
                int resolvedHashId = ItemData.ResolvePersistentHashId(item);
                if (resolvedHashId != 0)
                    return resolvedHashId;
            }

            return GetSavedCultivationSeedHashId(in moduleDto, slotIndex);
        }

        internal bool CanEjectCultivationContents(BaseModule owner, PlayerInventory inventory, Vector3 dropPosition)
        {
            if (_slots == null || OccupiedSlotCount <= 0)
                return true;

            Span<int> itemHashIds = stackalloc int[MaxCultivationSlots];
            Span<int> quantities = stackalloc int[MaxCultivationSlots];
            Span<ulong> geneticsMasks = stackalloc ulong[MaxCultivationSlots];
            Span<ushort> qualityMillis = stackalloc ushort[MaxCultivationSlots];
            int occupiedCount = BuildCultivationEjectionBatch(itemHashIds, quantities, geneticsMasks, qualityMillis);
            if (occupiedCount <= 0)
                return true;

            if (inventory != null &&
                inventory.CanAcceptItemWithStateBatch(itemHashIds, geneticsMasks, qualityMillis, occupiedCount))
            {
                return true;
            }

            if (owner == null || !IsFiniteRuntimePosition(dropPosition))
                return false;

            ItemCatalog itemCatalog = ResolveEjectionItemCatalog(inventory);
            PersistentWorldRegistry persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;
            if (itemCatalog == null || persistentWorldRegistry == null)
                return false;

            for (int i = 0; i < occupiedCount; i++)
            {
                ItemData item = itemCatalog.FindByHash(itemHashIds[i]);
                if (!persistentWorldRegistry.CanRegisterDroppedItem(item, quantities[i], dropPosition))
                    return false;
            }

            return persistentWorldRegistry.CanRegisterDroppedItemBatch(occupiedCount);
        }

        internal bool EjectCultivationContents(BaseModule owner, PlayerInventory inventory, ref Vector3 dropPosition)
        {
            if (_slots == null || OccupiedSlotCount <= 0)
                return true;

            if (!CanEjectCultivationContents(owner, inventory, dropPosition))
                return false;

            ItemCatalog itemCatalog = ResolveEjectionItemCatalog(inventory);
            PersistentWorldRegistry persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;
            bool allDelivered = true;
            for (int i = 0; i < _slots.Length; i++)
            {
                CultivationSlotState slot = _slots[i];
                if (slot.SeedItemHashId == 0)
                    continue;

                ulong geneticsMask = SanitizeGeneticsMask(slot.GeneticsMask);
                ushort qualityMilli = ResolveCultivationQualityMilli(slot.Quality01);
                if (inventory != null &&
                    inventory.TryAddItemWithState(slot.SeedItemHashId, new PlayerInventory.ItemState(geneticsMask, qualityMilli)))
                {
                    _slots[i] = default;
                    continue;
                }

                ItemData item = itemCatalog != null ? itemCatalog.FindByHash(slot.SeedItemHashId) : null;
                if (persistentWorldRegistry != null &&
                    item != null &&
                    persistentWorldRegistry.TryRegisterDroppedItemWithState(
                        item,
                        1,
                        dropPosition,
                        geneticsMask,
                        qualityMilli))
                {
                    _slots[i] = default;
                    dropPosition.x += 0.3f;
                    continue;
                }

                allDelivered = false;
            }

            return allDelivered;
        }

        /// <summary>
        /// Advances plant growth and applies mature cultivation side effects into the owning module.
        /// </summary>
        public void SlowTick()
        {
            if (_slots == null)
                return;

            _slowTickSequence++;

            float scrubAmount = 0f;
            float toxicScrubberPowerWatts = 0f;
            float lightingPowerCreditWatts = 0f;
            float hazardIntensity = 0f;
            float hazardRadius = 0f;
            int occupiedCount = 0;
            int matureCount = 0;
            int deadCount = 0;
            int floodedRotDeathCount = 0;
            ulong combinedTraitMask = 0UL;
            bool moduleFlooded = targetModule != null && targetModule.IsFlooded;
            float growthDeltaSeconds = SlowTickDt;

            for (int i = 0; i < _slots.Length; i++)
            {
                CultivationSlotState slot = _slots[i];
                if (slot.SeedItemHashId == 0)
                    continue;

                occupiedCount++;
                slot.Quality01 = NormalizeQuality01(slot.Quality01);
                if (slot.Quality01 <= 0f)
                {
                    deadCount++;
                    _slots[i] = slot;
                    continue;
                }

                if (moduleFlooded && !IsSaltwaterTolerant(slot.GeneticsMask))
                {
                    slot.Quality01 = 0f;
                    slot.Growth01 = 0f;
                    _slots[i] = slot;
                    deadCount++;
                    floodedRotDeathCount++;
                    continue;
                }

                float growthMultiplier = ResolveGrowthRateMultiplier(slot.GeneticsMask);
                slot.Growth01 = NormalizeGrowth01(slot.Growth01 + (growthDeltaSeconds / GrowthDurationSeconds) * growthMultiplier);
                _slots[i] = slot;

                if (slot.Growth01 < MatureThreshold)
                    continue;

                matureCount++;
                combinedTraitMask |= slot.GeneticsMask;
                scrubAmount += ResolveScrubContribution(slot.GeneticsMask);
                toxicScrubberPowerWatts += ResolveToxicScrubberPower(slot.GeneticsMask);
                lightingPowerCreditWatts += ResolveLightingPowerCredit(slot.GeneticsMask);
                ResolveHazardContribution(slot.GeneticsMask, ref hazardIntensity, ref hazardRadius);
            }

            _debugOccupiedSlotCount = occupiedCount;
            _debugMatureSlotCount = matureCount;
            _debugCombinedTraitMask = unchecked((uint)combinedTraitMask);
            _debugDeadSlotCount = deadCount;

            if (targetModule == null)
            {
                ClearHazardState();
                ClearRotHazardState();
                _debugScrubberLoadWatts = 0f;
                _debugLightingCreditWatts = 0f;
                _debugHazardActive = false;
                return;
            }

            float oxygenUnitsPerMaturePlant = ResolveOxygenContribution(GeneOxygenProducing);
            float oxygenUnits = CalculateMatureOxygenUnits(oxygenUnitsPerMaturePlant);
            if (oxygenUnits > 0f)
                targetModule.ApplyCultivationOxygen(oxygenUnits);

            if (scrubAmount > 0f)
                targetModule.ApplyBotanyScrub(scrubAmount);

            float requiredScrubberLoadWatts = toxicScrubberPowerWatts * 2f;
            if (floodedRotDeathCount > 0)
            {
                float rotBaseLoadWatts = math.max(requiredScrubberLoadWatts, fallbackToxicScrubberPowerWatts * floodedRotDeathCount);
                requiredScrubberLoadWatts = rotBaseLoadWatts * math.max(1f, floodedRotScrubberLoadMultiplier);
            }

            targetModule.SetCultivationScrubberLoad(requiredScrubberLoadWatts);
            targetModule.SetCultivationLightingPowerCredit(lightingPowerCreditWatts);
            _debugScrubberLoadWatts = requiredScrubberLoadWatts;
            _debugLightingCreditWatts = lightingPowerCreditWatts;

            bool toxicHazardActive = requiredScrubberLoadWatts > 0.01f &&
                (!targetModule.HasPower || targetModule.PowerSupplyRatio < MinimumOperationalSupplyRatio);
            if (toxicHazardActive)
            {
                RegisterToxicHazard(math.max(hazardIntensity, fallbackHazardIntensity), math.max(hazardRadius, fallbackHazardRadiusMeters));
            }
            else
            {
                ClearHazardState();
            }

            if (floodedRotDeathCount > 0)
            {
                float rotIntensity = math.saturate(floodedRotDeathCount * floodedRotIntensityPerDeadPlant);
                targetModule.EmitCultivationRotIntoFloodWater(rotIntensity, floodedRotCo2Amplifier);
                RegisterRotHazard(rotIntensity, floodedRotHazardRadiusMeters);
            }
            else
            {
                ClearRotHazardState();
            }

            _debugHazardActive = toxicHazardActive;
        }

        internal bool TryInsertFromInventory(PlayerInventory inventory, ItemData item, int quantity = 1)
        {
            if (inventory == null || item == null || quantity <= 0)
                return false;

            int seedItemHashId = ItemData.ResolvePersistentHashId(item);
            if (seedItemHashId == 0)
                return false;

            int inserted = 0;
            int desired = math.max(1, quantity);
            for (int i = 0; i < desired; i++)
            {
                if (!TryPlantSeedFromInventory(inventory, seedItemHashId))
                    break;

                inserted++;
            }

            return inserted > 0;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registered = false;
        }

        private bool TryGetEmptySlotIndex(out int slotIndex)
        {
            if (_slots != null)
            {
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (_slots[i].SeedItemHashId != 0)
                        continue;

                    slotIndex = i;
                    return true;
                }
            }

            slotIndex = -1;
            return false;
        }

        private void ClearSlots()
        {
            if (_slots != null)
                Array.Clear(_slots, 0, _slots.Length);
        }

        private int BuildCultivationEjectionBatch(
            Span<int> itemHashIds,
            Span<int> quantities,
            Span<ulong> geneticsMasks,
            Span<ushort> qualityMillis)
        {
            if (_slots == null)
                return 0;

            int count = 0;
            int capacity = math.min(
                math.min(itemHashIds.Length, quantities.Length),
                math.min(geneticsMasks.Length, math.min(qualityMillis.Length, MaxCultivationSlots)));
            for (int i = 0; i < _slots.Length && count < capacity; i++)
            {
                CultivationSlotState slot = _slots[i];
                int itemHashId = slot.SeedItemHashId;
                if (itemHashId == 0)
                    continue;

                itemHashIds[count] = itemHashId;
                quantities[count] = 1;
                geneticsMasks[count] = SanitizeGeneticsMask(slot.GeneticsMask);
                qualityMillis[count] = ResolveCultivationQualityMilli(slot.Quality01);
                count++;
            }

            return count;
        }

        private void ClearHazardState()
        {
            ClearHazardState(_cachedHazardZones);
        }

        private void ClearHazardState(HazardZoneManager hazardZoneManager)
        {
            if (hazardZoneManager != null)
                hazardZoneManager.UnregisterZone(_hazardZoneId);
        }

        private void ClearRotHazardState()
        {
            ClearRotHazardState(_cachedHazardZones);
        }

        private void ClearRotHazardState(HazardZoneManager hazardZoneManager)
        {
            if (hazardZoneManager != null)
                hazardZoneManager.UnregisterZone(_rotHazardZoneId);
        }

        private void RegisterToxicHazard(float intensity, float radiusMeters)
        {
            if (targetModule == null)
                return;

            Vector3 center = targetModule.ResolveBotanyAnchorWorldPosition();
            float resolvedRadius = radiusMeters;
            if (targetModule.TryGetInteriorHazardBounds(out Vector3 worldCenter, out float interiorRadius))
            {
                center = worldCenter;
                resolvedRadius = math.max(radiusMeters, interiorRadius * 0.55f);
            }

            HazardZoneManager hazardZoneManager = GetCachedHazardZoneRuntime();
            if (hazardZoneManager == null)
                return;

            float visorGlitchBias = toxicHazardProfile != null ? toxicHazardProfile.VisorGlitchBias : 1f;
            hazardZoneManager.RegisterZone(
                _hazardZoneId,
                center,
                math.saturate(intensity),
                math.max(0.25f, resolvedRadius),
                HazardType.Toxicity,
                visorGlitchBias,
                toxicHazardProfile);
        }

        private void RegisterRotHazard(float intensity, float radiusMeters)
        {
            if (targetModule == null || intensity <= 0f)
                return;

            Vector3 center = targetModule.ResolveBotanyAnchorWorldPosition();
            float resolvedRadius = radiusMeters;
            if (targetModule.TryGetInteriorHazardBounds(out Vector3 worldCenter, out float interiorRadius))
            {
                center = worldCenter;
                resolvedRadius = math.max(radiusMeters, interiorRadius * 0.45f);
            }

            HazardZoneManager hazardZoneManager = GetCachedHazardZoneRuntime();
            if (hazardZoneManager == null)
                return;

            float visorGlitchBias = toxicHazardProfile != null ? toxicHazardProfile.VisorGlitchBias : 1f;
            hazardZoneManager.RegisterZone(
                _rotHazardZoneId,
                center,
                math.saturate(intensity),
                math.max(0.25f, resolvedRadius),
                HazardType.Biohazard,
                visorGlitchBias,
                toxicHazardProfile);
        }

        private ulong ResolveEffectiveGeneticsMask(int seedItemHashId, ulong geneticsMask)
        {
            ulong resolvedMask = geneticsMask != 0UL ? geneticsMask : ResolveDefaultGeneticsMask(seedItemHashId);
            return SanitizeGeneticsMask(resolvedMask);
        }

        private ulong ResolveDefaultGeneticsMask(int seedItemHashId)
        {
            if (seedItemHashId == 0 || floraTemplates == null)
                return 0UL;

            for (int i = 0; i < floraTemplates.Length; i++)
            {
                FloraDataTemplate template = floraTemplates[i];
                if (template == null || template.CultivationSeedHashId != seedItemHashId)
                    continue;

                return SanitizeGeneticsMask(template.GeneticsMask);
            }

            return 0UL;
        }

        private ulong ResolveSavedGeneticsMask(ModuleDTO moduleDto, int slotIndex, int seedItemHashId)
        {
            ulong savedMask = 0UL;
            if (moduleDto.cultivationGeneticsMasks != null &&
                slotIndex >= 0 &&
                slotIndex < moduleDto.cultivationGeneticsMasks.Length)
            {
                savedMask = moduleDto.cultivationGeneticsMasks[slotIndex];
            }

            return ResolveEffectiveGeneticsMask(seedItemHashId, savedMask);
        }

        private static ulong SanitizeGeneticsMask(ulong geneticsMask)
        {
            return geneticsMask & DefinedCultivationGeneMask;
        }

        private static float ResolveSavedGrowth01(ModuleDTO moduleDto, int slotIndex)
        {
            if (moduleDto.cultivationGrowth01 == null ||
                slotIndex < 0 ||
                slotIndex >= moduleDto.cultivationGrowth01.Length)
            {
                return 0f;
            }

            return NormalizeGrowth01(moduleDto.cultivationGrowth01[slotIndex]);
        }

        private static float NormalizeGrowth01(float growth01)
        {
            return math.isfinite(growth01)
                ? math.saturate(growth01)
                : 0f;
        }

        private int ResolveHybridSeedItemHash(int primarySeedHashId, int secondarySeedHashId, ulong resultMask)
        {
            int bestSeedHashId = primarySeedHashId != 0 ? primarySeedHashId : secondarySeedHashId;
            int bestScore = -1;

            if (floraTemplates != null)
            {
                for (int i = 0; i < floraTemplates.Length; i++)
                {
                    FloraDataTemplate template = floraTemplates[i];
                    if (template == null || template.CultivationSeedHashId == 0)
                        continue;

                    int score = CountBits(SanitizeGeneticsMask(template.GeneticsMask) & resultMask) * 4;
                    if (template.CultivationSeedHashId == primarySeedHashId || template.CultivationSeedHashId == secondarySeedHashId)
                        score += 3;

                    if (score <= bestScore)
                        continue;

                    bestScore = score;
                    bestSeedHashId = template.CultivationSeedHashId;
                }
            }

            return bestSeedHashId;
        }

        private float ResolveGrowthRateMultiplier(ulong geneticsMask)
        {
            float multiplier = geneticTraitProfile != null
                ? geneticTraitProfile.ResolveGrowthRateMultiplier(geneticsMask)
                : 1f;

            return (geneticsMask & (ulong)GeneticTraitProfile.GeneticTraitMask.FastGrowing) != 0UL
                ? math.max(2f, multiplier)
                : multiplier;
        }

        private float ResolveOxygenContribution(ulong geneticsMask)
        {
            if (geneticTraitProfile != null)
                return geneticTraitProfile.ResolveOxygenUnitsPerSlowTick(geneticsMask);

            return (geneticsMask & (ulong)GeneticTraitProfile.GeneticTraitMask.OxygenProducing) != 0UL
                ? fallbackOxygenUnitsPerSlowTick
                : 0f;
        }

        private float ResolveScrubContribution(ulong geneticsMask)
        {
            return (geneticsMask & (ulong)GeneticTraitProfile.GeneticTraitMask.OxygenProducing) != 0UL
                ? scrubAmountPerOxygenTrait
                : 0f;
        }

        private float ResolveToxicScrubberPower(ulong geneticsMask)
        {
            if (geneticTraitProfile != null)
                return geneticTraitProfile.ResolveScrubberPowerWatts(geneticsMask);

            return (geneticsMask & (ulong)GeneticTraitProfile.GeneticTraitMask.Toxic) != 0UL
                ? fallbackToxicScrubberPowerWatts
                : 0f;
        }

        private float ResolveLightingPowerCredit(ulong geneticsMask)
        {
            if (geneticTraitProfile != null)
                return geneticTraitProfile.ResolveLightingPowerCreditWatts(geneticsMask);

            return (geneticsMask & (ulong)GeneticTraitProfile.GeneticTraitMask.Bioluminescent) != 0UL
                ? fallbackBiolumLightingPowerCreditWatts
                : 0f;
        }

        private bool IsSaltwaterTolerant(ulong geneticsMask)
        {
            if (geneticTraitProfile != null)
                return geneticTraitProfile.IsSaltwaterTolerant(geneticsMask);

            return (geneticsMask & (ulong)GeneticTraitProfile.GeneticTraitMask.Aquatic) != 0UL;
        }

        private void ResolveHazardContribution(ulong geneticsMask, ref float intensity, ref float radiusMeters)
        {
            if (geneticTraitProfile != null)
            {
                geneticTraitProfile.ResolveHazardProfile(geneticsMask, out float profileIntensity, out float profileRadius);
                intensity = math.max(intensity, profileIntensity);
                radiusMeters = math.max(radiusMeters, profileRadius);
                return;
            }

            if ((geneticsMask & (ulong)GeneticTraitProfile.GeneticTraitMask.Toxic) == 0UL)
                return;

            intensity = math.max(intensity, fallbackHazardIntensity);
            radiusMeters = math.max(radiusMeters, fallbackHazardRadiusMeters);
        }

        private ItemCatalog ResolveItemCatalog()
        {
            IPlayerInventoryService inventoryService = _cachedInventoryService;
            PlayerInventory inventory = inventoryService != null && inventoryService.IsInitialized
                ? inventoryService.Inventory
                : null;
            return inventory != null ? inventory.ItemCatalog : null;
        }

        private ItemCatalog ResolveEjectionItemCatalog(PlayerInventory inventory)
        {
            return inventory != null && inventory.ItemCatalog != null
                ? inventory.ItemCatalog
                : ResolveItemCatalog();
        }

        private static ushort ResolveCultivationQualityMilli(float quality01)
        {
            return (ushort)math.clamp((int)math.round(NormalizeQuality01(quality01) * 1000f), 0, 1000);
        }

        private static bool IsFiniteRuntimePosition(Vector3 position)
        {
            return math.all(math.isfinite(new float3(position.x, position.y, position.z)));
        }

        private void CacheRegistryServicesCold()
        {
            _cachedInventoryService = GlobalRegistry.PlayerInventory;
            _cachedHazardZones = GlobalRegistry.HazardZones;
        }

        private void ClearCachedRegistryServices()
        {
            _cachedInventoryService = null;
            _cachedHazardZones = null;
        }

        private HazardZoneManager GetCachedHazardZoneRuntime()
        {
            return _cachedHazardZones;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private static float NormalizeQuality01(float quality01)
        {
            return math.isfinite(quality01)
                ? math.saturate(quality01)
                : 0f;
        }

        private static int CountBits(ulong value)
        {
            int count = 0;
            while (value != 0UL)
            {
                value &= value - 1UL;
                count++;
            }

            return count;
        }
    }
}

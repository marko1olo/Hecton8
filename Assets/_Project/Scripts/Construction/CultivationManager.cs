using System;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.SaveSystem;
using Hecton8.World;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Construction
{
    /// <summary>
    /// Fixed-slot cultivation owner that persists hybrid seed genetics and routes mature-plant side effects
    /// into the existing base-module atmosphere, power, and hazard systems.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CultivationManager : MonoBehaviour, ISlowTickable
    {
        private const int MaxCultivationSlots = 4;
        private const float SlowTickDt = 0.5f;
        private const float GrowthDurationSeconds = 20f * 60f;
        private const float MatureThreshold = 0.999f;
        private const float MinimumOperationalSupplyRatio = 0.98f;
        private const ulong GeneBioluminescent = (ulong)GeneticTraitProfile.GeneticTraitMask.Bioluminescent;
        private const ulong GeneOxygenProducing = (ulong)GeneticTraitProfile.GeneticTraitMask.OxygenProducing;
        private const ulong GeneToxic = (ulong)GeneticTraitProfile.GeneticTraitMask.Toxic;
        private const ulong GeneRapidGrowth = (ulong)GeneticTraitProfile.GeneticTraitMask.FastGrowing;
        private const ulong SpliceMutationGeneMask = GeneBioluminescent | GeneOxygenProducing | GeneToxic | GeneRapidGrowth;

        /// <summary>
        /// Fixed cultivation slot payload shared with atmosphere jobs without managed allocation.
        /// </summary>
        [Serializable]
        public struct CultivationSlotState
        {
            public int SeedItemHashId;
            public ulong GeneticsMask;
            public float Growth01;
            public float Quality01;
        }

        private struct XorShift32State
        {
            private uint _state;

            public XorShift32State(uint seed)
            {
                _state = seed != 0u ? seed : 0x6D2B79F5u;
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

        [Header("── Diagnostics ──────────────")]
        [SerializeField] private int _debugOccupiedSlotCount;
        [SerializeField] private int _debugMatureSlotCount;
        [SerializeField] private uint _debugCombinedTraitMask;
        [SerializeField] private float _debugScrubberLoadWatts;
        [SerializeField] private float _debugLightingCreditWatts;
        [SerializeField] private int _debugDeadSlotCount;
        [SerializeField] private bool _debugHazardActive;

        private NativeArray<CultivationSlotState> _slots;
        private bool _registered;
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
                if (!_slots.IsCreated)
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

        /// <summary>Read-only fixed cultivation slot state for atmosphere and UI consumers.</summary>
        internal NativeArray<CultivationSlotState>.ReadOnly SlotStateReadOnly => _slots.IsCreated ? _slots.AsReadOnly() : default;

        private void Awake()
        {
            if (targetModule == null)
                targetModule = GetComponentInParent<BaseModule>();

            if (planterBridge == null)
                TryGetComponent(out planterBridge);

            // COLD ALLOC: NativeArray<CultivationSlotState>[4] - fixed cultivation slot runtime state - owner: CultivationManager
            _slots = new NativeArray<CultivationSlotState>(MaxCultivationSlots, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _hazardZoneId = unchecked((int)EntityId.ToULong(GetEntityId()) * 397) ^ 0x43554C54;
            _rotHazardZoneId = _hazardZoneId ^ 0x524F54;
        }

        private void OnEnable()
        {
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
        }

        private void OnDestroy()
        {
            if (_slots.IsCreated)
                _slots.Dispose();
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
            resultMask = (geneticsMaskA | geneticsMaskB) ^ mutationMask;

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
            if (items == null || quantities == null || !_slots.IsCreated)
                return 0;

            int copyCount = Mathf.Min(items.Length, quantities.Length);
            int written = 0;
            ItemCatalog resolvedCatalog = itemCatalog != null ? itemCatalog : ResolveItemCatalog();
            for (int i = 0; i < _slots.Length && written < copyCount; i++)
            {
                CultivationSlotState slot = _slots[i];
                items[written] = resolvedCatalog != null ? resolvedCatalog.FindByHash(slot.SeedItemHashId) : null;
                quantities[written] = slot.SeedItemHashId != 0 ? 1 : 0;
                written++;
            }

            return written;
        }

        /// <summary>
        /// Copies genetics and growth data into caller-owned buffers for cultivation UI rendering.
        /// </summary>
        public int CopyTraitSnapshot(uint[] geneticsMasks, float[] growthValues)
        {
            if (geneticsMasks == null || growthValues == null || !_slots.IsCreated)
                return 0;

            int copyCount = Mathf.Min(Mathf.Min(geneticsMasks.Length, growthValues.Length), _slots.Length);
            for (int i = 0; i < copyCount; i++)
            {
                geneticsMasks[i] = unchecked((uint)_slots[i].GeneticsMask);
                growthValues[i] = _slots[i].Growth01;
            }

            return copyCount;
        }

        /// <summary>
        /// Copies 64-bit genetics and growth data into caller-owned buffers for cultivation UI rendering.
        /// </summary>
        public int CopyTraitSnapshot(ulong[] geneticsMasks, float[] growthValues)
        {
            if (geneticsMasks == null || growthValues == null || !_slots.IsCreated)
                return 0;

            int copyCount = Mathf.Min(Mathf.Min(geneticsMasks.Length, growthValues.Length), _slots.Length);
            for (int i = 0; i < copyCount; i++)
            {
                geneticsMasks[i] = _slots[i].GeneticsMask;
                growthValues[i] = _slots[i].Growth01;
            }

            return copyCount;
        }

        /// <summary>
        /// Copies genetics, growth, and quality data into caller-owned buffers for cultivation UI rendering.
        /// </summary>
        public int CopyTraitSnapshot(uint[] geneticsMasks, float[] growthValues, float[] qualityValues)
        {
            if (geneticsMasks == null || growthValues == null || qualityValues == null || !_slots.IsCreated)
                return 0;

            int copyCount = Mathf.Min(Mathf.Min(Mathf.Min(geneticsMasks.Length, growthValues.Length), qualityValues.Length), _slots.Length);
            for (int i = 0; i < copyCount; i++)
            {
                geneticsMasks[i] = unchecked((uint)_slots[i].GeneticsMask);
                growthValues[i] = _slots[i].Growth01;
                qualityValues[i] = NormalizeQuality01(_slots[i].Quality01);
            }

            return copyCount;
        }

        /// <summary>
        /// Copies 64-bit genetics, growth, and quality data into caller-owned buffers for cultivation UI rendering.
        /// </summary>
        public int CopyTraitSnapshot(ulong[] geneticsMasks, float[] growthValues, float[] qualityValues)
        {
            if (geneticsMasks == null || growthValues == null || qualityValues == null || !_slots.IsCreated)
                return 0;

            int copyCount = Mathf.Min(Mathf.Min(Mathf.Min(geneticsMasks.Length, growthValues.Length), qualityValues.Length), _slots.Length);
            for (int i = 0; i < copyCount; i++)
            {
                geneticsMasks[i] = _slots[i].GeneticsMask;
                growthValues[i] = _slots[i].Growth01;
                qualityValues[i] = NormalizeQuality01(_slots[i].Quality01);
            }

            return copyCount;
        }

        /// <summary>
        /// Persists cultivation slots into the construction module DTO.
        /// </summary>
        public void PopulateSaveData(ref ModuleDTO moduleDto, ItemCatalog itemCatalog)
        {
            moduleDto.cultivationSlotCount = 0;
            moduleDto.cultivationSeedItemIds = null;
            moduleDto.cultivationGeneticsMasks = null;
            moduleDto.cultivationGrowth01 = null;
            moduleDto.cultivationQuality01 = null;

            if (!_slots.IsCreated)
                return;

            string[] seedIds = new string[MaxCultivationSlots];
            ulong[] geneticsMasks = new ulong[MaxCultivationSlots];
            float[] growthValues = new float[MaxCultivationSlots];
            float[] qualityValues = new float[MaxCultivationSlots];
            int writeIndex = 0;

            for (int i = 0; i < _slots.Length && writeIndex < MaxCultivationSlots; i++)
            {
                CultivationSlotState slot = _slots[i];
                if (slot.SeedItemHashId == 0)
                    continue;

                ItemData item = itemCatalog != null ? itemCatalog.FindByHash(slot.SeedItemHashId) : null;
                if (item == null || string.IsNullOrWhiteSpace(item.PersistentId))
                    continue;

                seedIds[writeIndex] = item.PersistentId;
                geneticsMasks[writeIndex] = slot.GeneticsMask;
                growthValues[writeIndex] = Mathf.Clamp01(slot.Growth01);
                qualityValues[writeIndex] = NormalizeQuality01(slot.Quality01);
                writeIndex++;
            }

            if (writeIndex <= 0)
                return;

            moduleDto.cultivationSlotCount = writeIndex;
            moduleDto.cultivationSeedItemIds = seedIds;
            moduleDto.cultivationGeneticsMasks = geneticsMasks;
            moduleDto.cultivationGrowth01 = growthValues;
            moduleDto.cultivationQuality01 = qualityValues;
        }

        /// <summary>
        /// Restores cultivation slots from the construction module DTO.
        /// </summary>
        public void RestoreFromSaveData(ModuleDTO moduleDto, ItemCatalog itemCatalog)
        {
            ClearSlots();
            if (!_slots.IsCreated)
                return;

            int safeCount = Mathf.Max(0, moduleDto.cultivationSlotCount);
            safeCount = Mathf.Min(safeCount, moduleDto.cultivationSeedItemIds != null ? moduleDto.cultivationSeedItemIds.Length : 0);
            safeCount = Mathf.Min(safeCount, moduleDto.cultivationGeneticsMasks != null ? moduleDto.cultivationGeneticsMasks.Length : 0);
            safeCount = Mathf.Min(safeCount, moduleDto.cultivationGrowth01 != null ? moduleDto.cultivationGrowth01.Length : 0);
            safeCount = Mathf.Min(safeCount, MaxCultivationSlots);
            for (int i = 0; i < safeCount; i++)
            {
                string persistentId = moduleDto.cultivationSeedItemIds[i];
                if (string.IsNullOrWhiteSpace(persistentId))
                    continue;

                ItemData item = itemCatalog != null ? itemCatalog.FindById(persistentId) : null;
                int itemHashId = item != null && !string.IsNullOrWhiteSpace(item.PersistentId)
                    ? LocHash.Compute(item.PersistentId)
                    : LocHash.Compute(persistentId);
                if (itemHashId == 0)
                    continue;

                _slots[i] = new CultivationSlotState
                {
                    SeedItemHashId = itemHashId,
                    GeneticsMask = moduleDto.cultivationGeneticsMasks[i],
                    Growth01 = Mathf.Clamp01(moduleDto.cultivationGrowth01[i]),
                    Quality01 = moduleDto.cultivationQuality01 != null && i < moduleDto.cultivationQuality01.Length
                        ? NormalizeQuality01(moduleDto.cultivationQuality01[i])
                        : 1f
                };
            }
        }

        /// <summary>
        /// Advances plant growth and applies mature cultivation side effects into the owning module.
        /// </summary>
        public void SlowTick()
        {
            if (!_slots.IsCreated)
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
                slot.Growth01 = Mathf.Clamp01(slot.Growth01 + (SlowTickDt / GrowthDurationSeconds) * growthMultiplier);
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
            if (oxygenUnitsPerMaturePlant > 0f)
                targetModule.ApplyCultivationOxygen(this, oxygenUnitsPerMaturePlant);

            if (scrubAmount > 0f)
                targetModule.ApplyBotanyScrub(scrubAmount);

            float requiredScrubberLoadWatts = toxicScrubberPowerWatts * 2f;
            targetModule.SetCultivationScrubberLoad(requiredScrubberLoadWatts);
            targetModule.SetCultivationLightingPowerCredit(lightingPowerCreditWatts);
            _debugScrubberLoadWatts = requiredScrubberLoadWatts;
            _debugLightingCreditWatts = lightingPowerCreditWatts;

            bool toxicHazardActive = requiredScrubberLoadWatts > 0.01f &&
                (!targetModule.HasPower || targetModule.PowerSupplyRatio < MinimumOperationalSupplyRatio);
            if (toxicHazardActive)
            {
                RegisterToxicHazard(Mathf.Max(hazardIntensity, fallbackHazardIntensity), Mathf.Max(hazardRadius, fallbackHazardRadiusMeters));
            }
            else
            {
                ClearHazardState();
            }

            if (floodedRotDeathCount > 0)
            {
                float rotIntensity = Mathf.Clamp01(floodedRotDeathCount * floodedRotIntensityPerDeadPlant);
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

            int seedItemHashId = !string.IsNullOrWhiteSpace(item.PersistentId)
                ? LocHash.Compute(item.PersistentId)
                : 0;
            if (seedItemHashId == 0)
                return false;

            int inserted = 0;
            int desired = Mathf.Max(1, quantity);
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

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _registered = true;
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
            if (_slots.IsCreated)
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
            if (!_slots.IsCreated)
                return;

            for (int i = 0; i < _slots.Length; i++)
                _slots[i] = default;
        }

        private void ClearHazardState()
        {
            HazardZoneManager hazardZoneManager = HazardZoneManager.Instance;
            if (hazardZoneManager != null)
                hazardZoneManager.UnregisterZone(_hazardZoneId);
        }

        private void ClearRotHazardState()
        {
            HazardZoneManager hazardZoneManager = HazardZoneManager.Instance;
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
                resolvedRadius = Mathf.Max(radiusMeters, interiorRadius * 0.55f);
            }

            HazardZoneManager hazardZoneManager = HazardZoneManager.EnsureRuntimeInstance();
            if (hazardZoneManager == null)
                return;

            float visorGlitchBias = toxicHazardProfile != null ? toxicHazardProfile.VisorGlitchBias : 1f;
            hazardZoneManager.RegisterZone(
                _hazardZoneId,
                center,
                Mathf.Clamp01(intensity),
                Mathf.Max(0.25f, resolvedRadius),
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
                resolvedRadius = Mathf.Max(radiusMeters, interiorRadius * 0.45f);
            }

            HazardZoneManager hazardZoneManager = HazardZoneManager.EnsureRuntimeInstance();
            if (hazardZoneManager == null)
                return;

            float visorGlitchBias = toxicHazardProfile != null ? toxicHazardProfile.VisorGlitchBias : 1f;
            hazardZoneManager.RegisterZone(
                _rotHazardZoneId,
                center,
                Mathf.Clamp01(intensity),
                Mathf.Max(0.25f, resolvedRadius),
                HazardType.Biohazard,
                visorGlitchBias,
                toxicHazardProfile);
        }

        private ulong ResolveEffectiveGeneticsMask(int seedItemHashId, ulong geneticsMask)
        {
            return geneticsMask != 0UL ? geneticsMask : ResolveDefaultGeneticsMask(seedItemHashId);
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

                return template.GeneticsMask;
            }

            return 0UL;
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

                    int score = CountBits(template.GeneticsMask & resultMask) * 4;
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
                ? Mathf.Max(2f, multiplier)
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
                intensity = Mathf.Max(intensity, profileIntensity);
                radiusMeters = Mathf.Max(radiusMeters, profileRadius);
                return;
            }

            if ((geneticsMask & (ulong)GeneticTraitProfile.GeneticTraitMask.Toxic) == 0UL)
                return;

            intensity = Mathf.Max(intensity, fallbackHazardIntensity);
            radiusMeters = Mathf.Max(radiusMeters, fallbackHazardRadiusMeters);
        }

        private ItemCatalog ResolveItemCatalog()
        {
            return PlayerInventory.Instance != null ? PlayerInventory.Instance.ItemCatalog : null;
        }

        private static float NormalizeQuality01(float quality01)
        {
            return float.IsFinite(quality01) ? Mathf.Clamp01(quality01) : 0f;
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

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hecton8.SaveSystem
{
    /// <summary>
    /// Приводит старые или частично пустые сейвы к текущему формату.
    /// Делает только безопасные правки: дозаполняет недостающие поля,
    /// нормализует счётчики и выставляет дефолты там, где старый сейв
    /// физически не мог хранить нужные данные.
    /// </summary>
    public static class SaveDataMigration
    {
        private const float LegacyModuleIntegrityDefault = 100f;
        private const float LegacyBeaconLightRangeDefault = 4f;
        private const string DefaultBeaconLabelPrefix = "BEACON";
        private const int InvalidBiomeId = BiomeDiscoveryBitMask.InvalidBiomeId;
        private const int MaxAtlasRevealStage = 4;

        public static bool MigrateInPlace(SaveData data, out int originalVersion, out string summary)
        {
            originalVersion = data != null ? data.version : 0;
            summary = "No migration needed.";

            if (data == null)
                return false;

            int sourceVersion = data.version > 0 ? data.version : 1;
            bool changed = false;
            List<string> steps = new List<string>(8);

            if (string.IsNullOrWhiteSpace(data.timestamp))
            {
                data.timestamp = DateTime.Now.ToString("O");
                changed = true;
                steps.Add("timestamp repaired");
            }

            if (data.toolDurabilityMap == null)
            {
                data.toolDurabilityMap = new Dictionary<string, float>();
                changed = true;
                steps.Add("tool durability map created");
            }

            if (data.toolBrokenMap == null)
            {
                data.toolBrokenMap = new Dictionary<string, bool>();
                changed = true;
                steps.Add("tool broken map created");
            }

            if (data.CustomModData == null)
            {
                data.CustomModData = new Dictionary<string, string>();
                changed = true;
                steps.Add("custom mod data created");
            }

            if (data.suitBrokenUpgradeIds == null)
            {
                data.suitBrokenUpgradeIds = new List<string>();
                changed = true;
                steps.Add("suit broken upgrades created");
            }

            bool hadPackedBiomeCapacity = BiomeDiscoveryBitMask.HasExpectedCapacity(data.discoveredBiomeBitWords);
            if (!hadPackedBiomeCapacity)
            {
                BiomeDiscoveryBitMask.EnsureCapacity(ref data.discoveredBiomeBitWords);
                changed = true;
                steps.Add("discovered biome bit words created");
            }

            if (!BiomeDiscoveryBitMask.HasAnySet(data.discoveredBiomeBitWords) &&
                data.discoveredBiomeIds != null &&
                data.discoveredBiomeIds.Count > 0)
            {
                BiomeDiscoveryBitMask.Pack(data.discoveredBiomeIds, data.discoveredBiomeBitWords);
                changed = true;
                steps.Add("discovered biome set packed");
            }

            int normalizedLastDiscoveredBiomeId = NormalizeLastDiscoveredBiomeId(
                data.lastDiscoveredBiomeId,
                data.discoveredBiomeIds,
                data.discoveredBiomeBitWords);
            if (normalizedLastDiscoveredBiomeId != data.lastDiscoveredBiomeId)
            {
                data.lastDiscoveredBiomeId = normalizedLastDiscoveredBiomeId;
                changed = true;
                steps.Add("last discovered biome repaired");
            }

            changed |= EnsureNarrative(ref data, steps);

            changed |= EnsureInventory(ref data.inventory, steps);
            changed |= EnsureWorldState(ref data.worldState, steps);
            changed |= EnsureProceduralWorldState(ref data.proceduralWorldState, steps);
            changed |= EnsureConstruction(ref data.construction, sourceVersion, steps);
            changed |= EnsureScanLog(ref data.scanLog, steps);
            changed |= EnsureBarter(ref data.barter, steps);
            changed |= EnsureFieldOperations(ref data.fieldOperations, steps);
            changed |= EnsureBeaconNetwork(ref data.beaconNetwork, steps);
            changed |= EnsureExplorationMap(ref data.explorationMap, steps);
            changed |= EnsurePdaLogbook(ref data.pdaLogbook, steps);
            changed |= EnsurePdaMarkers(ref data.pdaMarkers, steps);
            changed |= EnsurePdaAdvisories(ref data.pdaAdvisories, steps);
            changed |= EnsureProceduralLore(ref data.proceduralLore, steps);
            changed |= EnsureAchievements(ref data.achievements, steps);
            changed |= EnsureRunModifiers(ref data.runModifiers, steps);
            changed |= EnsureResourceScarcity(ref data.resourceScarcity, steps);
            changed |= EnsureEnvironmentalStrain(ref data.environmentalStrain, steps);
            changed |= EnsureEcosystemState(ref data.ecosystemState, steps);
            changed |= EnsureLoreSystems(ref data, sourceVersion, steps);
            changed |= EnsurePlayerExpression(ref data, steps);
            changed |= EnsurePerformanceSettings(ref data, sourceVersion, steps);

            if (data.version != SaveData.CurrentVersion)
            {
                data.version = SaveData.CurrentVersion;
                changed = true;
                steps.Add($"version upgraded from {sourceVersion} to {SaveData.CurrentVersion}");
            }

            if (changed)
                summary = string.Join(", ", steps);

            return changed;
        }

        private static bool EnsureInventory(ref InventoryDTO dto, List<string> steps)
        {
            bool changed = false;
            if (dto.cells == null || dto.cells.Length < InventoryDTO.MaxCells)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("inventory capacity repaired");
            }

            int clamped = Mathf.Clamp(dto.cellCount, 0, dto.cells != null ? dto.cells.Length : 0);
            if (clamped != dto.cellCount)
            {
                dto.cellCount = clamped;
                changed = true;
                steps.Add("inventory count clamped");
            }

            return changed;
        }

        private static int NormalizeLastDiscoveredBiomeId(
            int lastDiscoveredBiomeId,
            HashSet<int> discoveredBiomeIds,
            long[] discoveredBiomeBitWords)
        {
            if (IsValidBiomeId(lastDiscoveredBiomeId) &&
                (BiomeDiscoveryBitMask.Contains(discoveredBiomeBitWords, lastDiscoveredBiomeId) ||
                 (discoveredBiomeIds != null && discoveredBiomeIds.Contains(lastDiscoveredBiomeId))))
            {
                return lastDiscoveredBiomeId;
            }

            if (BiomeDiscoveryBitMask.HasAnySet(discoveredBiomeBitWords))
                return BiomeDiscoveryBitMask.ResolveFallbackLastDiscoveredId(discoveredBiomeBitWords);

            if (discoveredBiomeIds != null)
            {
                for (int biomeId = BiomeDiscoveryBitMask.MinBiomeId; biomeId <= BiomeDiscoveryBitMask.MaxBiomeId; biomeId++)
                {
                    if (discoveredBiomeIds.Contains(biomeId))
                        return biomeId;
                }
            }

            return InvalidBiomeId;
        }

        private static bool IsValidBiomeId(int biomeId)
        {
            return BiomeDiscoveryBitMask.IsValidBiomeId(biomeId);
        }

        private static bool EnsureWorldState(ref WorldStateDTO dto, List<string> steps)
        {
            bool changed = false;
            if (dto.depletedNodeIds == null || dto.depletedNodeIds.Length < WorldStateDTO.MaxNodes ||
                dto.depletedPickupChunkKeys == null || dto.depletedPickupChunkKeys.Length < WorldStateDTO.MaxPickupChunks ||
                dto.depletedPickupChunkWordStarts == null || dto.depletedPickupChunkWordStarts.Length < WorldStateDTO.MaxPickupChunks ||
                dto.depletedPickupChunkWordCounts == null || dto.depletedPickupChunkWordCounts.Length < WorldStateDTO.MaxPickupChunks ||
                dto.depletedPickupWords == null || dto.depletedPickupWords.Length < WorldStateDTO.MaxPickupWords)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("world state capacity repaired");
            }

            int clamped = Mathf.Clamp(dto.depletedCount, 0, dto.depletedNodeIds != null ? dto.depletedNodeIds.Length : 0);
            if (clamped != dto.depletedCount)
            {
                dto.depletedCount = clamped;
                changed = true;
                steps.Add("world state count clamped");
            }

            int clampedPickupChunks = Mathf.Clamp(
                dto.depletedPickupChunkCount,
                0,
                dto.depletedPickupChunkKeys != null ? dto.depletedPickupChunkKeys.Length : 0);
            if (clampedPickupChunks != dto.depletedPickupChunkCount)
            {
                dto.depletedPickupChunkCount = clampedPickupChunks;
                changed = true;
                steps.Add("world pickup chunk count clamped");
            }

            int clampedPickupWords = Mathf.Clamp(
                dto.depletedPickupWordCount,
                0,
                dto.depletedPickupWords != null ? dto.depletedPickupWords.Length : 0);
            if (clampedPickupWords != dto.depletedPickupWordCount)
            {
                dto.depletedPickupWordCount = clampedPickupWords;
                changed = true;
                steps.Add("world pickup word count clamped");
            }

            return changed;
        }

        private static bool EnsureEcosystemState(ref EcosystemStateDTO dto, List<string> steps)
        {
            bool changed = false;
            int existingCount = dto.infectedChunkKeys != null ? dto.infectedChunkKeys.Length : 0;
            long[] previousKeys = null;
            float[] previousSeverities = null;

            if (existingCount > 0)
            {
                previousKeys = dto.infectedChunkKeys;
                previousSeverities = dto.infectedSeverities;
            }

            if (dto.infectedChunkKeys == null ||
                dto.infectedChunkKeys.Length < EcosystemStateDTO.MaxInfectedZones ||
                dto.infectedSeverities == null ||
                dto.infectedSeverities.Length < EcosystemStateDTO.MaxInfectedZones)
            {
                dto.EnsureCapacity();
                if (previousKeys != null)
                {
                    int copyCount = Mathf.Min(previousKeys.Length, dto.infectedChunkKeys.Length);
                    Array.Copy(previousKeys, dto.infectedChunkKeys, copyCount);
                    if (previousSeverities != null)
                    {
                        int severityCopyCount = Mathf.Min(previousSeverities.Length, dto.infectedSeverities.Length);
                        Array.Copy(previousSeverities, dto.infectedSeverities, severityCopyCount);
                    }
                }

                changed = true;
                steps.Add("ecosystem state capacity repaired");
            }

            int clampedCount = Mathf.Clamp(dto.infectedZoneCount, 0, dto.infectedChunkKeys != null ? dto.infectedChunkKeys.Length : 0);
            if (clampedCount != dto.infectedZoneCount)
            {
                dto.infectedZoneCount = clampedCount;
                changed = true;
                steps.Add("ecosystem infected-zone count clamped");
            }

            for (int i = 0; i < clampedCount; i++)
            {
                float clampedSeverity = Mathf.Clamp01(dto.infectedSeverities[i]);
                if (Mathf.Abs(clampedSeverity - dto.infectedSeverities[i]) > 0.0001f)
                {
                    dto.infectedSeverities[i] = clampedSeverity;
                    changed = true;
                }
            }

            return changed;
        }

        private static bool EnsureNarrative(ref SaveData data, List<string> steps)
        {
            bool changed = false;

            if (data.narrativeDiscoveryIds == null || data.narrativeDiscoveryIds.Length < SaveData.MaxNarrativeDiscoveries)
            {
                data.narrativeDiscoveryIds = new string[SaveData.MaxNarrativeDiscoveries];
                changed = true;
                steps.Add("narrative discovery capacity repaired");
            }

            int clampedCount = Mathf.Clamp(data.narrativeDiscoveryCount, 0, data.narrativeDiscoveryIds.Length);
            if (clampedCount != data.narrativeDiscoveryCount)
            {
                data.narrativeDiscoveryCount = clampedCount;
                changed = true;
                steps.Add("narrative discovery count clamped");
            }

            if (data.narrativeDepthTier < 0)
            {
                data.narrativeDepthTier = 0;
                changed = true;
                steps.Add("narrative depth tier repaired");
            }

            return changed;
        }

        private static bool EnsureExplorationMap(ref ExplorationMapDTO dto, List<string> steps)
        {
            bool changed = false;

            if (dto.exploredChunkKeys == null || dto.exploredChunkKeys.Length < ExplorationMapDTO.MaxExploredChunks)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("exploration map capacity repaired");
            }

            int clampedCount = Mathf.Clamp(dto.exploredChunkCount, 0, dto.exploredChunkKeys != null ? dto.exploredChunkKeys.Length : 0);
            if (clampedCount != dto.exploredChunkCount)
            {
                dto.exploredChunkCount = clampedCount;
                changed = true;
                steps.Add("exploration map count clamped");
            }

            return changed;
        }

        private static bool EnsurePdaLogbook(ref PDALogbookDTO dto, List<string> steps)
        {
            bool changed = false;

            if (dto.entries == null || dto.entries.Length < PDALogbookDTO.MaxEntries ||
                dto.seenOriginKeys == null || dto.seenOriginKeys.Length < PDALogbookDTO.MaxSeenOrigins)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("pda logbook capacity repaired");
            }

            int clampedCount = Mathf.Clamp(dto.entryCount, 0, dto.entries != null ? dto.entries.Length : 0);
            if (clampedCount != dto.entryCount)
            {
                dto.entryCount = clampedCount;
                changed = true;
                steps.Add("pda logbook count clamped");
            }

            if (dto.nextSequence < 1)
            {
                dto.nextSequence = Mathf.Max(1, clampedCount + 1);
                changed = true;
                steps.Add("pda logbook sequence repaired");
            }

            int clampedSeenOriginCount = Mathf.Clamp(dto.seenOriginCount, 0, dto.seenOriginKeys != null ? dto.seenOriginKeys.Length : 0);
            if (clampedSeenOriginCount != dto.seenOriginCount)
            {
                dto.seenOriginCount = clampedSeenOriginCount;
                changed = true;
                steps.Add("pda logbook seen-origin count clamped");
            }

            return changed;
        }

        private static bool EnsurePdaMarkers(ref PDAMarkerRegistryDTO dto, List<string> steps)
        {
            bool changed = false;

            if (dto.entries == null || dto.entries.Length < PDAMarkerRegistryDTO.MaxEntries)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("pda marker capacity repaired");
            }

            int clampedCount = Mathf.Clamp(dto.markerCount, 0, dto.entries != null ? dto.entries.Length : 0);
            if (clampedCount != dto.markerCount)
            {
                dto.markerCount = clampedCount;
                changed = true;
                steps.Add("pda marker count clamped");
            }

            if (dto.nextSequence < 1)
            {
                dto.nextSequence = Mathf.Max(1, clampedCount + 1);
                changed = true;
                steps.Add("pda marker sequence repaired");
            }

            return changed;
        }

        private static bool EnsurePdaAdvisories(ref PDAContextualAdvisoryDTO dto, List<string> steps)
        {
            bool changed = false;

            if (dto.oxygenDeathCount < 0)
            {
                dto.oxygenDeathCount = 0;
                changed = true;
                steps.Add("pda advisory oxygen-death count repaired");
            }

            if (dto.inventoryFullAttemptCount < 0)
            {
                dto.inventoryFullAttemptCount = 0;
                changed = true;
                steps.Add("pda advisory inventory-full count repaired");
            }

            if (dto.pressureDeathCount < 0)
            {
                dto.pressureDeathCount = 0;
                changed = true;
                steps.Add("pda advisory pressure-death count repaired");
            }

            if (dto.baseEmergencyCount < 0)
            {
                dto.baseEmergencyCount = 0;
                changed = true;
                steps.Add("pda advisory base-emergency count repaired");
            }

            if (dto.staleAirIncidentCount < 0)
            {
                dto.staleAirIncidentCount = 0;
                changed = true;
                steps.Add("pda advisory stale-air count repaired");
            }

            if (dto.coldStressIncidentCount < 0)
            {
                dto.coldStressIncidentCount = 0;
                changed = true;
                steps.Add("pda advisory cold-stress count repaired");
            }

            if (dto.heatStressIncidentCount < 0)
            {
                dto.heatStressIncidentCount = 0;
                changed = true;
                steps.Add("pda advisory heat-stress count repaired");
            }

            if (dto.deepExposureSeconds < 0f || float.IsNaN(dto.deepExposureSeconds))
            {
                dto.deepExposureSeconds = 0f;
                changed = true;
                steps.Add("pda advisory deep-exposure time repaired");
            }

            if (dto.coldStressExposureSeconds < 0f || float.IsNaN(dto.coldStressExposureSeconds))
            {
                dto.coldStressExposureSeconds = 0f;
                changed = true;
                steps.Add("pda advisory cold-stress exposure repaired");
            }

            if (dto.heatStressExposureSeconds < 0f || float.IsNaN(dto.heatStressExposureSeconds))
            {
                dto.heatStressExposureSeconds = 0f;
                changed = true;
                steps.Add("pda advisory heat-stress exposure repaired");
            }

            return changed;
        }

        private static bool EnsureProceduralLore(ref ProceduralLoreStateDTO dto, List<string> steps)
        {
            bool changed = false;

            if (dto.activePlacements == null || dto.activePlacements.Length < ProceduralLoreStateDTO.MaxActivePlacements)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("procedural lore capacity repaired");
            }

            int clampedCount = Mathf.Clamp(dto.activeCount, 0, dto.activePlacements != null ? dto.activePlacements.Length : 0);
            if (clampedCount != dto.activeCount)
            {
                dto.activeCount = clampedCount;
                changed = true;
                steps.Add("procedural lore count clamped");
            }

            if (dto.nextSourceIndex < 0)
            {
                dto.nextSourceIndex = 0;
                changed = true;
                steps.Add("procedural lore source index repaired");
            }

            return changed;
        }

        private static bool EnsureAchievements(ref AchievementRegistryDTO dto, List<string> steps)
        {
            bool changed = false;

            if (dto.unlockedIds == null || dto.unlockedIds.Length < AchievementRegistryDTO.MaxUnlockedAchievements)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("achievement registry capacity repaired");
            }

            int clampedUnlockedCount = Mathf.Clamp(dto.unlockedCount, 0, dto.unlockedIds != null ? dto.unlockedIds.Length : 0);
            if (clampedUnlockedCount != dto.unlockedCount)
            {
                dto.unlockedCount = clampedUnlockedCount;
                changed = true;
                steps.Add("achievement registry count clamped");
            }

            if (dto.swamDistanceMeters < 0f || float.IsNaN(dto.swamDistanceMeters))
            {
                dto.swamDistanceMeters = 0f;
                changed = true;
                steps.Add("achievement swim distance repaired");
            }

            if (dto.craftedItemCount < 0)
            {
                dto.craftedItemCount = 0;
                changed = true;
                steps.Add("achievement crafted count repaired");
            }

            if (dto.discoveredBiomeCount < 0)
            {
                dto.discoveredBiomeCount = 0;
                changed = true;
                steps.Add("achievement biome count repaired");
            }

            return changed;
        }

        private static bool EnsureRunModifiers(ref RunModifiersDTO dto, List<string> steps)
        {
            bool changed = false;

            if (!dto.isDailySeed && !string.IsNullOrEmpty(dto.dailySeedId))
            {
                dto.dailySeedId = string.Empty;
                changed = true;
                steps.Add("run modifiers daily-seed id cleared");
            }

            if (dto.isDailySeed && dto.dailySeedId == null)
            {
                dto.dailySeedId = string.Empty;
                changed = true;
                steps.Add("run modifiers daily-seed id repaired");
            }

            if (!dto.isPermadeath && dto.runMarkedDead)
            {
                dto.runMarkedDead = false;
                changed = true;
                steps.Add("run modifiers dead-run flag repaired");
            }

            return changed;
        }

        private static bool EnsureResourceScarcity(ref ResourceScarcityDTO dto, List<string> steps)
        {
            bool changed = false;
            int previousItemCapacity = dto.itemIds != null ? dto.itemIds.Length : 0;
            int previousCountCapacity = dto.collectedCounts != null ? dto.collectedCounts.Length : 0;

            dto.EnsureCapacity();
            if (dto.itemIds.Length != previousItemCapacity || dto.collectedCounts.Length != previousCountCapacity)
            {
                changed = true;
                steps.Add("resource scarcity capacity repaired");
            }

            int clampedEntryCount = Mathf.Clamp(
                dto.entryCount,
                0,
                Mathf.Min(dto.itemIds.Length, dto.collectedCounts.Length));

            if (clampedEntryCount != dto.entryCount)
            {
                dto.entryCount = clampedEntryCount;
                changed = true;
                steps.Add("resource scarcity entry count clamped");
            }

            for (int i = 0; i < dto.entryCount; i++)
            {
                if (dto.collectedCounts[i] < 0)
                {
                    dto.collectedCounts[i] = 0;
                    changed = true;
                }
            }

            return changed;
        }

        private static bool EnsureEnvironmentalStrain(ref EnvironmentalStrainDTO dto, List<string> steps)
        {
            bool changed = false;

            if (dto.microplasticStrain < 0f)
            {
                dto.microplasticStrain = 0f;
                changed = true;
            }

            if (dto.generalPollution < 0f)
            {
                dto.generalPollution = 0f;
                changed = true;
            }

            if (dto.recycledPlasticItemCount < 0)
            {
                dto.recycledPlasticItemCount = 0;
                changed = true;
            }

            if (dto.discardedItemCount < 0)
            {
                dto.discardedItemCount = 0;
                changed = true;
            }

            if (changed)
                steps.Add("environmental strain values clamped");

            return changed;
        }

        private static bool EnsurePerformanceSettings(ref SaveData data, int sourceVersion, List<string> steps)
        {
            bool changed = false;

            if (data.LODQualityPreset < 0 || data.LODQualityPreset > 2)
            {
                data.LODQualityPreset = 1;
                changed = true;
                steps.Add("lod quality preset repaired");
            }

            if (sourceVersion < SaveData.CurrentVersion && !data.DynamicResolutionEnabled)
            {
                data.DynamicResolutionEnabled = true;
                changed = true;
                steps.Add("dynamic resolution default repaired");
            }

            return changed;
        }

        private static bool EnsurePlayerExpression(ref SaveData data, List<string> steps)
        {
            if (data.playerExpressionProfileId != null)
                return false;

            data.playerExpressionProfileId = string.Empty;
            steps.Add("player expression profile initialized");
            return true;
        }

        private static bool EnsureConstruction(ref ConstructionDTO dto, int sourceVersion, List<string> steps)
        {
            bool changed = false;
            if (dto.modules == null || dto.modules.Length < ConstructionDTO.MaxModules)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("construction capacity repaired");
            }

            int clamped = Mathf.Clamp(dto.moduleCount, 0, dto.modules != null ? dto.modules.Length : 0);
            if (clamped != dto.moduleCount)
            {
                dto.moduleCount = clamped;
                changed = true;
                steps.Add("construction count clamped");
            }

            if (sourceVersion < 2 && dto.modules != null)
            {
                for (int i = 0; i < dto.moduleCount; i++)
                {
                    ModuleDTO module = dto.modules[i];
                    if (!string.IsNullOrEmpty(module.prefabId) && module.integrity <= 0f)
                    {
                        module.integrity = LegacyModuleIntegrityDefault;
                        dto.modules[i] = module;
                        changed = true;
                    }
                }

                if (changed)
                    steps.Add("legacy construction integrity restored");
            }

            return changed;
        }

        private static bool EnsureProceduralWorldState(ref ProceduralWorldStateDTO dto, List<string> steps)
        {
            bool changed = false;
            if (dto.suppressedPlacementKeys == null || dto.suppressedPlacementKeys.Length < ProceduralWorldStateDTO.MaxSuppressedPlacements ||
                dto.faunaStates == null || dto.faunaStates.Length < ProceduralWorldStateDTO.MaxFaunaStates)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("procedural world state capacity repaired");
            }

            int clampedSuppressed = Mathf.Clamp(dto.suppressedPlacementCount, 0, dto.suppressedPlacementKeys != null ? dto.suppressedPlacementKeys.Length : 0);
            if (clampedSuppressed != dto.suppressedPlacementCount)
            {
                dto.suppressedPlacementCount = clampedSuppressed;
                changed = true;
                steps.Add("procedural suppressed placement count clamped");
            }

            int clampedFauna = Mathf.Clamp(dto.faunaStateCount, 0, dto.faunaStates != null ? dto.faunaStates.Length : 0);
            if (clampedFauna != dto.faunaStateCount)
            {
                dto.faunaStateCount = clampedFauna;
                changed = true;
                steps.Add("procedural fauna state count clamped");
            }

            return changed;
        }

        private static bool EnsureScanLog(ref ScanLogDTO dto, List<string> steps)
        {
            bool changed = false;
            if (dto.entries == null || dto.entries.Length < ScanLogDTO.MaxEntries ||
                dto.recentEntryIds == null || dto.recentEntryIds.Length < ScanLogDTO.MaxRecentEntries)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("scan log capacity repaired");
            }

            int clampedEntries = Mathf.Clamp(dto.entryCount, 0, dto.entries != null ? dto.entries.Length : 0);
            if (clampedEntries != dto.entryCount)
            {
                dto.entryCount = clampedEntries;
                changed = true;
                steps.Add("scan log count clamped");
            }

            int clampedRecent = Mathf.Clamp(dto.recentCount, 0, dto.recentEntryIds != null ? dto.recentEntryIds.Length : 0);
            if (clampedRecent != dto.recentCount)
            {
                dto.recentCount = clampedRecent;
                changed = true;
                steps.Add("scan log recent count clamped");
            }

            return changed;
        }

        private static bool EnsureBarter(ref BarterDTO dto, List<string> steps)
        {
            bool changed = false;
            if (dto.offerStates == null || dto.offerStates.Length < BarterDTO.MaxOffers ||
                dto.recentTransactions == null || dto.recentTransactions.Length < BarterDTO.MaxRecentTransactions)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("barter capacity repaired");
            }

            int clampedStates = Mathf.Clamp(dto.stateCount, 0, dto.offerStates != null ? dto.offerStates.Length : 0);
            if (clampedStates != dto.stateCount)
            {
                dto.stateCount = clampedStates;
                changed = true;
                steps.Add("barter state count clamped");
            }

            int clampedTransactions = Mathf.Clamp(dto.recentTransactionCount, 0, dto.recentTransactions != null ? dto.recentTransactions.Length : 0);
            if (clampedTransactions != dto.recentTransactionCount)
            {
                dto.recentTransactionCount = clampedTransactions;
                changed = true;
                steps.Add("barter transaction count clamped");
            }

            return changed;
        }

        private static bool EnsureFieldOperations(ref FieldOperationLogDTO dto, List<string> steps)
        {
            bool changed = false;
            if (dto.recentEntries == null || dto.recentEntries.Length < FieldOperationLogDTO.MaxRecentEntries)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("field log capacity repaired");
            }

            int clamped = Mathf.Clamp(dto.recentCount, 0, dto.recentEntries != null ? dto.recentEntries.Length : 0);
            if (clamped != dto.recentCount)
            {
                dto.recentCount = clamped;
                changed = true;
                steps.Add("field log count clamped");
            }

            return changed;
        }

        private static bool EnsureBeaconNetwork(ref BeaconNetworkDTO dto, List<string> steps)
        {
            bool changed = false;
            if (dto.entries == null || dto.entries.Length < BeaconNetworkDTO.MaxEntries)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("beacon capacity repaired");
            }

            int clamped = Mathf.Clamp(dto.activeCount, 0, dto.entries != null ? dto.entries.Length : 0);
            if (clamped != dto.activeCount)
            {
                dto.activeCount = clamped;
                changed = true;
                steps.Add("beacon count clamped");
            }

            for (int i = 0; i < dto.activeCount; i++)
            {
                BeaconEntryDTO entry = dto.entries[i];

                if (string.IsNullOrWhiteSpace(entry.id))
                {
                    entry.id = Guid.NewGuid().ToString("N");
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(entry.label))
                {
                    entry.label = $"{DefaultBeaconLabelPrefix} {i + 1:00}";
                    changed = true;
                }

                if (entry.lightRange <= 0f)
                {
                    entry.lightRange = LegacyBeaconLightRangeDefault;
                    changed = true;
                }

                dto.entries[i] = entry;
            }

            if (dto.nextSequence <= 0)
            {
                dto.nextSequence = Mathf.Max(1, dto.activeCount + 1);
                changed = true;
                steps.Add("beacon sequence repaired");
            }

            return changed;
        }

        // ── v11-16: Lore Systems ──────────────────────────────────

        private static bool EnsureLoreSystems(ref SaveData data, int sourceVersion, List<string> steps)
        {
            bool changed = false;

            if (data.audioLogDiscoveredIds == null)
            {
                data.audioLogDiscoveredIds = new System.Collections.Generic.List<string>();
                changed = true;
                steps.Add("audioLog list created");
            }

            if (data.questActiveIds == null)
            {
                data.questActiveIds = new System.Collections.Generic.List<string>();
                changed = true;
                steps.Add("quest active list created");
            }

            if (data.questCompletedIds == null)
            {
                data.questCompletedIds = new System.Collections.Generic.List<string>();
                changed = true;
                steps.Add("quest completed list created");
            }

            if (data.suitInstalledUpgradeIds == null)
            {
                data.suitInstalledUpgradeIds = new System.Collections.Generic.List<string>();
                changed = true;
                steps.Add("suit upgrades list created");
            }

            if (data.suitUnlockedBlueprintIds == null)
            {
                data.suitUnlockedBlueprintIds = new System.Collections.Generic.List<string>();
                changed = true;
                steps.Add("suit blueprints list created");
            }

            if (data.corporateReceivedOrderIds == null)
            {
                data.corporateReceivedOrderIds = new System.Collections.Generic.List<string>();
                changed = true;
                steps.Add("corporate orders list created");
            }

            if (data.corporatePendingOrderIds == null)
            {
                data.corporatePendingOrderIds = new System.Collections.Generic.List<string>();
                changed = true;
                steps.Add("corporate pending orders list created");
            }

            if (data.corporatePendingOrderTimers == null)
            {
                data.corporatePendingOrderTimers = new System.Collections.Generic.List<float>();
                changed = true;
                steps.Add("corporate order timers list created");
            }

            if (data.missionActiveIds == null)
            {
                data.missionActiveIds = new System.Collections.Generic.List<string>();
                changed = true;
                steps.Add("mission active list created");
            }

            if (data.missionCompletedIds == null)
            {
                data.missionCompletedIds = new System.Collections.Generic.List<string>();
                changed = true;
                steps.Add("mission completed list created");
            }

            int inferredRevealStage = data.endingConditionMet
                ? MaxAtlasRevealStage
                : data.narrativeDepthTier >= 4
                    ? 3
                    : data.narrativeDepthTier >= 3
                        ? 2
                        : data.atlasSignalDetected
                            ? 2
                            : 0;

            int clampedRevealStage = Mathf.Clamp(data.atlasSignalRevealStage, 0, MaxAtlasRevealStage);
            if (sourceVersion < SaveData.CurrentVersion && clampedRevealStage < inferredRevealStage)
                clampedRevealStage = inferredRevealStage;

            if (clampedRevealStage != data.atlasSignalRevealStage)
            {
                data.atlasSignalRevealStage = clampedRevealStage;
                changed = true;
                steps.Add("atlas reveal stage repaired");
            }

            return changed;
        }
    }
}

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
        private const int MinBiomeId = 1;
        private const int MaxBiomeId = 108;
        private const int InvalidBiomeId = -1;
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

            if (data.discoveredBiomeIds == null)
            {
                data.discoveredBiomeIds = new HashSet<int>();
                changed = true;
                steps.Add("discovered biome set created");
            }

            int normalizedLastDiscoveredBiomeId = NormalizeLastDiscoveredBiomeId(
                data.lastDiscoveredBiomeId,
                data.discoveredBiomeIds);
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

        private static int NormalizeLastDiscoveredBiomeId(int lastDiscoveredBiomeId, HashSet<int> discoveredBiomeIds)
        {
            if (IsValidBiomeId(lastDiscoveredBiomeId) &&
                discoveredBiomeIds != null &&
                discoveredBiomeIds.Contains(lastDiscoveredBiomeId))
            {
                return lastDiscoveredBiomeId;
            }

            if (discoveredBiomeIds != null)
            {
                foreach (int biomeId in discoveredBiomeIds)
                {
                    if (IsValidBiomeId(biomeId))
                        return biomeId;
                }
            }

            return InvalidBiomeId;
        }

        private static bool IsValidBiomeId(int biomeId)
        {
            return biomeId >= MinBiomeId && biomeId <= MaxBiomeId;
        }

        private static bool EnsureWorldState(ref WorldStateDTO dto, List<string> steps)
        {
            bool changed = false;
            if (dto.depletedNodeIds == null || dto.depletedNodeIds.Length < WorldStateDTO.MaxNodes)
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

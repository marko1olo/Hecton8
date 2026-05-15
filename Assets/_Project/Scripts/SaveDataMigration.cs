using System;
using System.Collections.Generic;
using Hecton8.Core.Contracts;
using Hecton8.Gameplay;
using Hecton.Localization;
using Hecton8.Narrative;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.SaveSystem
{
    /// <summary>
    /// Privodit starye ili chastichno pustye seyvy k tekuschemu formatu.
    /// Delaet tolko bezopasnye pravki: dozapolnyaet nedostayuschie polya,
    /// normalizuet schetchiki i vystavlyaet defolty tam, gde staryy seyv
    /// fizicheski ne mog hranit nuzhnye dannye.
    /// </summary>
    public static class SaveDataMigration
    {
        private const float LegacyModuleIntegrityDefault = 100f;
        private const float LegacyBeaconLightRangeDefault = 4f;
        private const string DefaultBeaconLabelPrefix = "BEACON";
        private const int InvalidBiomeId = BiomeDiscoveryBitMask.InvalidBiomeId;
        private const int MaxAtlasRevealStage = 4;

        private static void EnsureExactArrayCapacity<T>(ref T[] values, int capacity)
        {
            if (values != null && values.Length == capacity)
                return;

            T[] replacement = new T[capacity];
            if (values != null && values.Length > 0)
            {
                int copyCount = values.Length < capacity ? values.Length : capacity;
                Array.Copy(values, replacement, copyCount);
            }

            values = replacement;
        }

        private static bool TrimListToMax<T>(List<T> values, int maxCount, string step, List<string> steps)
        {
            if (values == null)
                return false;

            int safeMax = Math.Max(maxCount, 0);
            if (values.Count <= safeMax)
                return false;

            values.RemoveRange(safeMax, values.Count - safeMax);
            steps.Add(step);
            return true;
        }

        private static bool TrimPairedListsToMax<T0, T1>(
            List<T0> values0,
            List<T1> values1,
            int maxCount,
            string step,
            List<string> steps)
        {
            if (values0 == null || values1 == null)
                return false;

            int safeCount = Math.Min(Math.Max(maxCount, 0), Math.Min(values0.Count, values1.Count));
            bool changed = false;

            if (values0.Count > safeCount)
            {
                values0.RemoveRange(safeCount, values0.Count - safeCount);
                changed = true;
            }

            if (values1.Count > safeCount)
            {
                values1.RemoveRange(safeCount, values1.Count - safeCount);
                changed = true;
            }

            if (changed)
                steps.Add(step);

            return changed;
        }

        private static bool TrimDictionaryToMax<TKey, TValue>(
            Dictionary<TKey, TValue> values,
            int maxCount,
            string step,
            List<string> steps)
        {
            if (values == null)
                return false;

            int safeMax = Math.Max(maxCount, 0);
            if (values.Count <= safeMax)
                return false;

            while (values.Count > safeMax)
            {
                TKey keyToRemove = default(TKey);
                bool hasKey = false;
                Dictionary<TKey, TValue>.Enumerator enumerator = values.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    keyToRemove = enumerator.Current.Key;
                    hasKey = true;
                }

                enumerator.Dispose();

                if (!hasKey)
                    break;

                values.Remove(keyToRemove);
            }

            steps.Add(step);
            return true;
        }

        private static bool TrimHashSetToMax<T>(HashSet<T> values, int maxCount, string step, List<string> steps)
        {
            if (values == null)
                return false;

            int safeMax = Math.Max(maxCount, 0);
            if (values.Count <= safeMax)
                return false;

            while (values.Count > safeMax)
            {
                T valueToRemove = default(T);
                bool hasValue = false;
                HashSet<T>.Enumerator enumerator = values.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    valueToRemove = enumerator.Current;
                    hasValue = true;
                }

                enumerator.Dispose();

                if (!hasValue)
                    break;

                values.Remove(valueToRemove);
            }

            steps.Add(step);
            return true;
        }

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
            changed |= TrimDictionaryToMax(
                data.toolDurabilityMap,
                SaveData.MaxToolDurabilityRecords,
                "tool durability map capped",
                steps);

            if (data.toolBrokenMap == null)
            {
                data.toolBrokenMap = new Dictionary<string, bool>();
                changed = true;
                steps.Add("tool broken map created");
            }
            changed |= TrimDictionaryToMax(
                data.toolBrokenMap,
                SaveData.MaxToolDurabilityRecords,
                "tool broken map capped",
                steps);

            if (data.CustomModData == null)
            {
                data.CustomModData = new Dictionary<string, string>();
                changed = true;
                steps.Add("custom mod data created");
            }
            changed |= TrimDictionaryToMax(
                data.CustomModData,
                SaveData.MaxCustomModDataEntries,
                "custom mod data capped",
                steps);

            if (data.suitBrokenUpgradeIds == null)
            {
                data.suitBrokenUpgradeIds = new List<string>();
                changed = true;
                steps.Add("suit broken upgrades created");
            }
            changed |= TrimListToMax(
                data.suitBrokenUpgradeIds,
                SaveData.MaxSuitUpgradeIds,
                "suit broken upgrades capped",
                steps);

            bool hadPackedBiomeCapacity = BiomeDiscoveryBitMask.HasExpectedCapacity(data.discoveredBiomeBitWords);
            if (!hadPackedBiomeCapacity)
            {
                BiomeDiscoveryBitMask.EnsureCapacity(ref data.discoveredBiomeBitWords);
                changed = true;
                steps.Add("discovered biome bit words created");
            }

            changed |= TrimHashSetToMax(
                data.discoveredBiomeIds,
                SaveData.MaxLegacyDiscoveredBiomeIds,
                "discovered biome set capped",
                steps);

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
            changed |= EnsureMetaCampaign(ref data.metaCampaign, steps);
            changed |= EnsureResourceScarcity(ref data.resourceScarcity, steps);
            changed |= EnsureEnvironmentalStrain(ref data.environmentalStrain, steps);
            changed |= EnsureEcosystemState(ref data.ecosystemState, steps);
            changed |= EnsureVoxelDeltaPersistence(ref data.voxelDeltaPersistence, steps);
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
            int cellBound = ClampArrayLength(dto.itemHashIds, InventoryDTO.MaxCells);
            cellBound = math.min(cellBound, ClampArrayLength(dto.packedCellCoordinates, InventoryDTO.MaxCells));
            cellBound = math.min(cellBound, ClampArrayLength(dto.stackCounts, InventoryDTO.MaxCells));
            int durabilityBound = ClampArrayLength(dto.itemDurabilityRle, InventoryDTO.MaxDurabilityRleBytes);

            if (dto.itemHashIds == null ||
                dto.itemHashIds.Length != InventoryDTO.MaxCells ||
                dto.packedCellCoordinates == null ||
                dto.packedCellCoordinates.Length != InventoryDTO.MaxCells ||
                dto.stackCounts == null ||
                dto.stackCounts.Length != InventoryDTO.MaxCells ||
                dto.itemStateFlags == null ||
                dto.itemStateFlags.Length != InventoryDTO.MaxCells ||
                dto.itemGeneticsWords == null ||
                dto.itemGeneticsWords.Length != InventoryDTO.MaxCells ||
                dto.qualityMilli == null ||
                dto.qualityMilli.Length != InventoryDTO.MaxCells ||
                dto.lastUpdateUnixSeconds == null ||
                dto.lastUpdateUnixSeconds.Length != InventoryDTO.MaxCells ||
                dto.itemDurabilityRle == null ||
                dto.itemDurabilityRle.Length != InventoryDTO.MaxDurabilityRleBytes)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("inventory SOA capacity repaired");
            }

            int clampedDurabilityRleLength = math.clamp(
                dto.itemDurabilityRleLength,
                0,
                durabilityBound);
            if (clampedDurabilityRleLength != dto.itemDurabilityRleLength)
            {
                dto.itemDurabilityRleLength = clampedDurabilityRleLength;
                changed = true;
                steps.Add("inventory durability RLE length clamped");
            }

            int clamped = math.clamp(dto.cellCount, 0, cellBound);
            if (clamped != dto.cellCount)
            {
                dto.cellCount = clamped;
                changed = true;
                steps.Add("inventory count clamped");
            }

            for (int i = 0; i < dto.cellCount; i++)
            {
                if (dto.stackCounts[i] <= 0)
                {
                    dto.stackCounts[i] = 1;
                    changed = true;
                }

                if (dto.qualityMilli[i] <= 0)
                {
                    dto.qualityMilli[i] = 1000;
                    changed = true;
                }
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
            int depletedBound = ClampArrayLength(dto.depletedNodeIds, WorldStateDTO.MaxNodes);
            int pickupChunkBound = ClampArrayLength(dto.depletedPickupChunkKeys, WorldStateDTO.MaxPickupChunks);
            pickupChunkBound = math.min(
                pickupChunkBound,
                ClampArrayLength(dto.depletedPickupChunkWordStarts, WorldStateDTO.MaxPickupChunks));
            pickupChunkBound = math.min(
                pickupChunkBound,
                ClampArrayLength(dto.depletedPickupChunkWordCounts, WorldStateDTO.MaxPickupChunks));
            int pickupWordBound = ClampArrayLength(dto.depletedPickupWords, WorldStateDTO.MaxPickupWords);

            if (dto.depletedNodeIds == null || dto.depletedNodeIds.Length != WorldStateDTO.MaxNodes ||
                dto.depletedPickupChunkKeys == null || dto.depletedPickupChunkKeys.Length != WorldStateDTO.MaxPickupChunks ||
                dto.depletedPickupChunkWordStarts == null || dto.depletedPickupChunkWordStarts.Length != WorldStateDTO.MaxPickupChunks ||
                dto.depletedPickupChunkWordCounts == null || dto.depletedPickupChunkWordCounts.Length != WorldStateDTO.MaxPickupChunks ||
                dto.depletedPickupWords == null || dto.depletedPickupWords.Length != WorldStateDTO.MaxPickupWords)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("world state capacity repaired");
            }

            int clamped = math.clamp(dto.depletedCount, 0, depletedBound);
            if (clamped != dto.depletedCount)
            {
                dto.depletedCount = clamped;
                changed = true;
                steps.Add("world state count clamped");
            }

            int clampedPickupChunks = math.clamp(
                dto.depletedPickupChunkCount,
                0,
                pickupChunkBound);
            if (clampedPickupChunks != dto.depletedPickupChunkCount)
            {
                dto.depletedPickupChunkCount = clampedPickupChunks;
                changed = true;
                steps.Add("world pickup chunk count clamped");
            }

            int clampedPickupWords = math.clamp(
                dto.depletedPickupWordCount,
                0,
                pickupWordBound);
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
            if (dto.worldGenerationVersionId < 0)
            {
                dto.worldGenerationVersionId = 0;
                changed = true;
                steps.Add("world generation version clamped");
            }

            int infectedBound = ClampArrayLength(dto.infectedChunkKeys, EcosystemStateDTO.MaxInfectedZones);
            infectedBound = math.min(
                infectedBound,
                ClampArrayLength(dto.infectedSeverities, EcosystemStateDTO.MaxInfectedZones));

            int existingCount = dto.infectedChunkKeys != null ? dto.infectedChunkKeys.Length : 0;
            long[] previousKeys = null;
            float[] previousSeverities = null;

            if (existingCount > 0)
            {
                previousKeys = dto.infectedChunkKeys;
                previousSeverities = dto.infectedSeverities;
            }

            if (dto.infectedChunkKeys == null ||
                dto.infectedChunkKeys.Length != EcosystemStateDTO.MaxInfectedZones ||
                dto.infectedSeverities == null ||
                dto.infectedSeverities.Length != EcosystemStateDTO.MaxInfectedZones)
            {
                dto.EnsureCapacity();
                if (previousKeys != null)
                {
                    int copyCount = math.min(previousKeys.Length, dto.infectedChunkKeys.Length);
                    Array.Copy(previousKeys, dto.infectedChunkKeys, copyCount);
                    if (previousSeverities != null)
                    {
                        int severityCopyCount = math.min(previousSeverities.Length, dto.infectedSeverities.Length);
                        Array.Copy(previousSeverities, dto.infectedSeverities, severityCopyCount);
                    }
                }

                changed = true;
                steps.Add("ecosystem state capacity repaired");
            }

            int clampedCount = math.clamp(dto.infectedZoneCount, 0, infectedBound);
            if (clampedCount != dto.infectedZoneCount)
            {
                dto.infectedZoneCount = clampedCount;
                changed = true;
                steps.Add("ecosystem infected-zone count clamped");
            }

            for (int i = 0; i < clampedCount; i++)
            {
                float clampedSeverity = math.saturate(dto.infectedSeverities[i]);
                if (math.abs(clampedSeverity - dto.infectedSeverities[i]) > 0.0001f)
                {
                    dto.infectedSeverities[i] = clampedSeverity;
                    changed = true;
                }
            }

            return changed;
        }

        private static bool EnsureVoxelDeltaPersistence(ref VoxelDeltaPersistenceDTO dto, List<string> steps)
        {
            bool changed = false;

            if (dto.chunks == null)
            {
                dto = VoxelDeltaPersistenceDTO.CreateDefault();
                changed = true;
                steps.Add("voxel delta persistence created");
            }

            int chunkCapacity = dto.chunks != null ? dto.chunks.Length : 0;
            int clampedChunkCount = math.clamp(dto.chunkCount, 0, chunkCapacity);
            if (clampedChunkCount != dto.chunkCount)
            {
                dto.chunkCount = clampedChunkCount;
                changed = true;
                steps.Add("voxel delta chunk count clamped");
            }

            int totalCellCount = 0;
            for (int i = 0; i < dto.chunkCount; i++)
            {
                VoxelDeltaChunkDTO chunk = dto.chunks[i];
                bool hasUniformSdfRleStorage = (chunk.storageFlags & VoxelDeltaChunkDTO.StorageUniformSdfRle) != 0;

                bool hasDenseStorage =
                    !hasUniformSdfRleStorage &&
                    chunk.dirtyMaskWords != null &&
                    chunk.dirtyMaskWords.Length == VoxelDeltaChunkDTO.DirtyMaskWordCount &&
                    chunk.sdfValueBits != null &&
                    chunk.sdfValueBits.Length == VoxelDeltaChunkDTO.CellCount &&
                    chunk.materialIds != null &&
                    chunk.materialIds.Length == VoxelDeltaChunkDTO.CellCount;

                if (chunk.dirtyMaskWords == null)
                {
                    chunk.dirtyMaskWords = Array.Empty<uint>();
                    changed = true;
                }

                if (chunk.sdfValueBits == null)
                {
                    chunk.sdfValueBits = Array.Empty<ushort>();
                    changed = true;
                }

                if (chunk.materialIds == null)
                {
                    chunk.materialIds = Array.Empty<byte>();
                    changed = true;
                }

                if (hasDenseStorage &&
                    (chunk.cellFlags == null || chunk.cellFlags.Length != VoxelDeltaChunkDTO.CellCount))
                {
                    chunk.cellFlags = new byte[VoxelDeltaChunkDTO.CellCount];
                    changed = true;
                }
                else if (chunk.cellFlags == null)
                {
                    chunk.cellFlags = Array.Empty<byte>();
                    changed = true;
                }

                if (hasUniformSdfRleStorage)
                {
                    if (chunk.dirtyMaskWords.Length != 0)
                    {
                        chunk.dirtyMaskWords = Array.Empty<uint>();
                        changed = true;
                    }

                    if (chunk.sdfValueBits.Length != 0)
                    {
                        chunk.sdfValueBits = Array.Empty<ushort>();
                        changed = true;
                    }

                    if (chunk.materialIds.Length != 0)
                    {
                        chunk.materialIds = Array.Empty<byte>();
                        changed = true;
                    }

                    if (chunk.cellFlags.Length != 0)
                    {
                        chunk.cellFlags = Array.Empty<byte>();
                        changed = true;
                    }
                }

                int cellCapacity = chunk.cells != null ? chunk.cells.Length : 0;
                int legacyCellCount = hasUniformSdfRleStorage ? 0 : math.clamp(chunk.cellCount, 0, cellCapacity);
                int denseCellCount = hasDenseStorage ? CountDirtyMaskBits(chunk.dirtyMaskWords) : 0;
                int clampedCellCount = hasUniformSdfRleStorage
                    ? VoxelDeltaChunkDTO.CellCount
                    : hasDenseStorage
                        ? math.max(denseCellCount, legacyCellCount)
                        : legacyCellCount;
                if (clampedCellCount != chunk.cellCount)
                {
                    chunk.cellCount = clampedCellCount;
                    changed = true;
                }

                if (chunk.cells == null)
                {
                    chunk.cells = Array.Empty<VoxelDeltaCellDTO>();
                    changed = true;
                }
                else if (hasUniformSdfRleStorage && chunk.cells.Length != 0)
                {
                    chunk.cells = Array.Empty<VoxelDeltaCellDTO>();
                    changed = true;
                }

                dto.chunks[i] = chunk;
                totalCellCount += chunk.cellCount;
            }

            if (dto.totalCellCount != totalCellCount)
            {
                dto.totalCellCount = totalCellCount;
                changed = true;
                steps.Add("voxel delta total count repaired");
            }

            return changed;
        }

        private static int CountDirtyMaskBits(uint[] dirtyMaskWords)
        {
            if (dirtyMaskWords == null)
                return 0;

            int total = 0;
            int wordCount = math.min(dirtyMaskWords.Length, VoxelDeltaChunkDTO.DirtyMaskWordCount);
            for (int i = 0; i < wordCount; i++)
            {
                uint value = dirtyMaskWords[i];
                while (value != 0u)
                {
                    value &= value - 1u;
                    total++;
                }
            }

            return total;
        }

        private static bool EnsureNarrative(ref SaveData data, List<string> steps)
        {
            bool changed = false;
            int narrativeBound = ClampArrayLength(data.narrativeDiscoveryIds, SaveData.MaxNarrativeDiscoveries);

            if (data.narrativeDiscoveryIds == null || data.narrativeDiscoveryIds.Length != SaveData.MaxNarrativeDiscoveries)
            {
                EnsureExactArrayCapacity(ref data.narrativeDiscoveryIds, SaveData.MaxNarrativeDiscoveries);
                changed = true;
                steps.Add("narrative discovery capacity repaired");
            }

            int clampedCount = math.clamp(data.narrativeDiscoveryCount, 0, narrativeBound);
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
            int exploredChunkBound = ClampArrayLength(dto.exploredChunkKeys, ExplorationMapDTO.MaxExploredChunks);
            int mortonWordBound = ClampArrayLength(dto.exploredMortonMaskWords, ExplorationMapDTO.MortonMaskWordCount);
            int mortonByteBound = ClampArrayLength(dto.exploredMortonMaskBytes, ExplorationMapDTO.MortonMaskByteCount);
            int sectorWordBound = ClampArrayLength(dto.discoveredSectorMaskWords, ExplorationMapDTO.CartographyMaskWordCount);
            int sectorByteBound = ClampArrayLength(dto.discoveredSectorMaskBytes, ExplorationMapDTO.CartographyMaskByteCount);

            if (dto.exploredChunkKeys == null || dto.exploredChunkKeys.Length != ExplorationMapDTO.MaxExploredChunks)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("exploration map capacity repaired");
            }

            if (dto.exploredMortonMaskWords == null || dto.exploredMortonMaskWords.Length != ExplorationMapDTO.MortonMaskWordCount)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("exploration morton bitmask capacity repaired");
            }

            if (dto.exploredMortonMaskBytes == null || dto.exploredMortonMaskBytes.Length != ExplorationMapDTO.MortonMaskByteCount)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("exploration morton byte mask capacity repaired");
            }

            if (dto.discoveredSectorMaskWords == null || dto.discoveredSectorMaskWords.Length != ExplorationMapDTO.CartographyMaskWordCount)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("cartography sector bitmask capacity repaired");
            }

            if (dto.discoveredSectorMaskBytes == null || dto.discoveredSectorMaskBytes.Length != ExplorationMapDTO.CartographyMaskByteCount)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("cartography sector byte mask capacity repaired");
            }

            int clampedCount = math.clamp(dto.exploredChunkCount, 0, exploredChunkBound);
            if (clampedCount != dto.exploredChunkCount)
            {
                dto.exploredChunkCount = clampedCount;
                changed = true;
                steps.Add("exploration map count clamped");
            }

            int clampedWordCount = math.clamp(dto.exploredMortonWordCount, 0, mortonWordBound);
            if (clampedWordCount != dto.exploredMortonWordCount)
            {
                dto.exploredMortonWordCount = clampedWordCount;
                changed = true;
                steps.Add("exploration morton word count clamped");
            }

            int clampedByteCount = math.clamp(dto.exploredMortonByteCount, 0, mortonByteBound);
            clampedByteCount = SaveBinaryStorage.AlignExplorationMortonByteCount(clampedByteCount);
            if (clampedByteCount != dto.exploredMortonByteCount)
            {
                dto.exploredMortonByteCount = clampedByteCount;
                changed = true;
                steps.Add("exploration morton byte count aligned");
            }

            int clampedSectorWordCount = math.clamp(dto.discoveredSectorWordCount, 0, sectorWordBound);
            if (clampedSectorWordCount != dto.discoveredSectorWordCount)
            {
                dto.discoveredSectorWordCount = clampedSectorWordCount;
                changed = true;
                steps.Add("cartography sector word count clamped");
            }

            int clampedSectorByteCount = math.clamp(dto.discoveredSectorByteCount, 0, sectorByteBound);
            clampedSectorByteCount = SaveBinaryStorage.AlignExplorationMortonByteCount(clampedSectorByteCount);
            if (clampedSectorByteCount != dto.discoveredSectorByteCount)
            {
                dto.discoveredSectorByteCount = clampedSectorByteCount;
                changed = true;
                steps.Add("cartography sector byte count aligned");
            }

            if (dto.chunkSizeMeters != ExplorationMapDTO.DenseChunkSizeMeters ||
                dto.mortonMaskAxisBits != ExplorationMapDTO.MortonMaskAxisBits ||
                dto.mortonMaskOriginOffset != ExplorationMapDTO.MortonMaskOriginOffset ||
                dto.mortonBuildSalt != SaveBinaryStorage.ExplorationMortonBuildSalt32)
            {
                dto.chunkSizeMeters = ExplorationMapDTO.DenseChunkSizeMeters;
                dto.mortonMaskAxisBits = ExplorationMapDTO.MortonMaskAxisBits;
                dto.mortonMaskOriginOffset = ExplorationMapDTO.MortonMaskOriginOffset;
                dto.mortonBuildSalt = SaveBinaryStorage.ExplorationMortonBuildSalt32;
                changed = true;
                steps.Add("exploration morton metadata repaired");
            }

            if (dto.cartographyCellSizeMeters != ExplorationMapDTO.CartographyCellSizeMeters ||
                dto.cartographyMaskAxisBits != ExplorationMapDTO.CartographyMaskAxisBits ||
                dto.cartographyMaskOriginOffset != ExplorationMapDTO.CartographyMaskOriginOffset)
            {
                dto.cartographyCellSizeMeters = ExplorationMapDTO.CartographyCellSizeMeters;
                dto.cartographyMaskAxisBits = ExplorationMapDTO.CartographyMaskAxisBits;
                dto.cartographyMaskOriginOffset = ExplorationMapDTO.CartographyMaskOriginOffset;
                changed = true;
                steps.Add("cartography sector metadata repaired");
            }

            return changed;
        }

        private static bool EnsurePdaLogbook(ref PDALogbookDTO dto, List<string> steps)
        {
            bool changed = false;
            int entryBound = ClampArrayLength(dto.entries, PDALogbookDTO.MaxEntries);
            int seenOriginBound = ClampArrayLength(dto.seenOriginHashes, PDALogbookDTO.MaxSeenOrigins);
            if (seenOriginBound == 0)
                seenOriginBound = ClampArrayLength(dto.seenOriginKeys, PDALogbookDTO.MaxSeenOrigins);

            if (dto.entries == null || dto.entries.Length != PDALogbookDTO.MaxEntries ||
                dto.seenOriginKeys == null || dto.seenOriginKeys.Length != PDALogbookDTO.MaxSeenOrigins ||
                dto.seenOriginHashes == null || dto.seenOriginHashes.Length != PDALogbookDTO.MaxSeenOrigins)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("pda logbook capacity repaired");
            }

            int clampedCount = math.clamp(dto.entryCount, 0, entryBound);
            if (clampedCount != dto.entryCount)
            {
                dto.entryCount = clampedCount;
                changed = true;
                steps.Add("pda logbook count clamped");
            }

            if (dto.nextSequence < 1)
            {
                dto.nextSequence = math.max(1, clampedCount + 1);
                changed = true;
                steps.Add("pda logbook sequence repaired");
            }

            int clampedSeenOriginCount = math.clamp(dto.seenOriginCount, 0, seenOriginBound);
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
            int markerBound = ClampArrayLength(dto.entries, PDAMarkerRegistryDTO.MaxEntries);

            if (dto.entries == null || dto.entries.Length != PDAMarkerRegistryDTO.MaxEntries)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("pda marker capacity repaired");
            }

            int clampedCount = math.clamp(dto.markerCount, 0, markerBound);
            if (clampedCount != dto.markerCount)
            {
                dto.markerCount = clampedCount;
                changed = true;
                steps.Add("pda marker count clamped");
            }

            if (dto.nextSequence < 1)
            {
                dto.nextSequence = math.max(1, clampedCount + 1);
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
            int activePlacementBound = ClampArrayLength(dto.activePlacements, ProceduralLoreStateDTO.MaxActivePlacements);

            if (dto.activePlacements == null || dto.activePlacements.Length != ProceduralLoreStateDTO.MaxActivePlacements)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("procedural lore capacity repaired");
            }

            int clampedCount = math.clamp(dto.activeCount, 0, activePlacementBound);
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
            int unlockedBound = ClampArrayLength(dto.unlockedIds, AchievementRegistryDTO.MaxUnlockedAchievements);

            if (dto.unlockedIds == null || dto.unlockedIds.Length != AchievementRegistryDTO.MaxUnlockedAchievements)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("achievement registry capacity repaired");
            }

            int clampedUnlockedCount = math.clamp(dto.unlockedCount, 0, unlockedBound);
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

        private static bool EnsureMetaCampaign(ref MetaCampaignDTO dto, List<string> steps)
        {
            bool changed = false;
            int previousHashCapacity = dto.variableHashes != null ? dto.variableHashes.Length : 0;
            int previousValueCapacity = dto.variableValues != null ? dto.variableValues.Length : 0;
            int variableBound = math.min(
                MetaCampaignDTO.MaxGlobalVariables,
                math.min(previousHashCapacity, previousValueCapacity));

            dto.EnsureCapacity();
            if (dto.variableHashes.Length != previousHashCapacity ||
                dto.variableValues.Length != previousValueCapacity)
            {
                changed = true;
                steps.Add("meta campaign capacity repaired");
            }

            int clampedCount = math.clamp(
                dto.variableCount,
                0,
                variableBound);
            if (clampedCount != dto.variableCount)
            {
                dto.variableCount = clampedCount;
                changed = true;
                steps.Add("meta campaign variable count clamped");
            }

            int clampedToxicity = math.clamp(dto.toxicityPermille, 0, 1000);
            if (clampedToxicity != dto.toxicityPermille)
            {
                dto.toxicityPermille = clampedToxicity;
                changed = true;
                steps.Add("meta campaign toxicity clamped");
            }

            return changed;
        }

        private static bool EnsureResourceScarcity(ref ResourceScarcityDTO dto, List<string> steps)
        {
            bool changed = false;
            int previousHashCapacity = dto.itemHashIds != null ? dto.itemHashIds.Length : 0;
            int previousItemCapacity = dto.itemIds != null ? dto.itemIds.Length : 0;
            int previousCountCapacity = dto.collectedCounts != null ? dto.collectedCounts.Length : 0;
            int entryBound = math.min(
                ResourceScarcityDTO.MaxTrackedResources,
                math.min(previousHashCapacity, math.min(previousItemCapacity, previousCountCapacity)));

            dto.EnsureCapacity();
            if (dto.itemHashIds.Length != previousHashCapacity ||
                dto.itemIds.Length != previousItemCapacity ||
                dto.collectedCounts.Length != previousCountCapacity)
            {
                changed = true;
                steps.Add("resource scarcity capacity repaired");
            }

            int clampedEntryCount = math.clamp(
                dto.entryCount,
                0,
                entryBound);

            if (clampedEntryCount != dto.entryCount)
            {
                dto.entryCount = clampedEntryCount;
                changed = true;
                steps.Add("resource scarcity entry count clamped");
            }

            for (int i = 0; i < dto.entryCount; i++)
            {
                if (dto.itemHashIds[i] == 0 && !string.IsNullOrWhiteSpace(dto.itemIds[i]))
                {
                    dto.itemHashIds[i] = LocHash.Compute(dto.itemIds[i]);
                    changed = true;
                    steps.Add("resource scarcity hash repaired");
                }

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
            int moduleBound = ClampArrayLength(dto.modules, ConstructionDTO.MaxModules);
            int graphNodeBound = ClampArrayLength(dto.graphNodes, ConstructionDTO.MaxModules);
            int graphEdgeBound = ClampArrayLength(dto.graphEdges, ConstructionDTO.MaxGraphEdges);
            int moduleBlitBound = ClampArrayLength(dto.moduleBlitRecords, ConstructionDTO.MaxModules);
            int habitatFloodBound = ClampArrayLength(dto.habitatFloodStates, ConstructionDTO.MaxModules);

            if (dto.modules == null || dto.modules.Length != ConstructionDTO.MaxModules ||
                dto.graphNodes == null || dto.graphNodes.Length != ConstructionDTO.MaxModules ||
                dto.graphEdges == null || dto.graphEdges.Length != ConstructionDTO.MaxGraphEdges ||
                dto.moduleBlitRecords == null || dto.moduleBlitRecords.Length != ConstructionDTO.MaxModules ||
                dto.habitatFloodStates == null || dto.habitatFloodStates.Length != ConstructionDTO.MaxModules)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("construction capacity repaired");
            }

            int clamped = math.clamp(dto.moduleCount, 0, moduleBound);
            if (clamped != dto.moduleCount)
            {
                dto.moduleCount = clamped;
                changed = true;
                steps.Add("construction count clamped");
            }

            int clampedGraphNodeCount = math.clamp(dto.graphNodeCount, 0, graphNodeBound);
            if (clampedGraphNodeCount != dto.graphNodeCount)
            {
                dto.graphNodeCount = clampedGraphNodeCount;
                changed = true;
                steps.Add("construction graph node count clamped");
            }

            int clampedGraphEdgeCount = math.clamp(dto.graphEdgeCount, 0, graphEdgeBound);
            if (clampedGraphEdgeCount != dto.graphEdgeCount)
            {
                dto.graphEdgeCount = clampedGraphEdgeCount;
                changed = true;
                steps.Add("construction graph edge count clamped");
            }

            int clampedBlitCount = math.clamp(dto.moduleBlitCount, 0, moduleBlitBound);
            if (clampedBlitCount != dto.moduleBlitCount)
            {
                dto.moduleBlitCount = clampedBlitCount;
                changed = true;
                steps.Add("construction blit count clamped");
            }

            int clampedFloodCount = math.clamp(dto.habitatFloodStateCount, 0, habitatFloodBound);
            if (clampedFloodCount != dto.habitatFloodStateCount)
            {
                dto.habitatFloodStateCount = clampedFloodCount;
                changed = true;
                steps.Add("construction flood count clamped");
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

            if (sourceVersion < 63 && dto.modules != null)
            {
                bool repairedHealth = false;
                for (int i = 0; i < dto.moduleCount; i++)
                {
                    ModuleDTO module = dto.modules[i];
                    byte health = PackLegacyConstructionHealthByte(module.integrity);
                    if (module.health != health)
                    {
                        module.health = health;
                        dto.modules[i] = module;
                        repairedHealth = true;
                        changed = true;
                    }
                }

                if (repairedHealth)
                    steps.Add("construction health mirror repaired");
            }

            return changed;
        }

        private static byte PackLegacyConstructionHealthByte(float integrity)
        {
            if (!math.isfinite(integrity) || integrity <= 0f)
                return 0;

            return (byte)math.clamp((int)math.round(math.saturate(integrity * 0.01f) * 255f), 0, 255);
        }

        private static bool EnsureProceduralWorldState(ref ProceduralWorldStateDTO dto, List<string> steps)
        {
            bool changed = false;
            int suppressedPlacementBound = ClampArrayLength(dto.suppressedPlacementKeys, ProceduralWorldStateDTO.MaxSuppressedPlacements);
            int faunaStateBound = ClampArrayLength(dto.faunaStates, ProceduralWorldStateDTO.MaxFaunaStates);
            int hibernatedFaunaBound = ClampArrayLength(dto.hibernatedFaunaStates, ProceduralWorldStateDTO.MaxHibernatedFaunaStates);
            int geologySeamBound = ClampArrayLength(dto.geologySeamStates, ProceduralWorldStateDTO.MaxGeologySeamStates);
            int geologyCaveBound = ClampArrayLength(dto.geologyCaveEntrances, ProceduralWorldStateDTO.MaxGeologyCaveEntrances);

            if (dto.suppressedPlacementKeys == null || dto.suppressedPlacementKeys.Length != ProceduralWorldStateDTO.MaxSuppressedPlacements ||
                dto.faunaStates == null || dto.faunaStates.Length != ProceduralWorldStateDTO.MaxFaunaStates ||
                dto.hibernatedFaunaStates == null || dto.hibernatedFaunaStates.Length != ProceduralWorldStateDTO.MaxHibernatedFaunaStates ||
                dto.geologySeamStates == null || dto.geologySeamStates.Length != ProceduralWorldStateDTO.MaxGeologySeamStates ||
                dto.geologyCaveEntrances == null || dto.geologyCaveEntrances.Length != ProceduralWorldStateDTO.MaxGeologyCaveEntrances)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("procedural world state capacity repaired");
            }

            int clampedSuppressed = math.clamp(dto.suppressedPlacementCount, 0, suppressedPlacementBound);
            if (clampedSuppressed != dto.suppressedPlacementCount)
            {
                dto.suppressedPlacementCount = clampedSuppressed;
                changed = true;
                steps.Add("procedural suppressed placement count clamped");
            }

            int clampedFauna = math.clamp(dto.faunaStateCount, 0, faunaStateBound);
            if (clampedFauna != dto.faunaStateCount)
            {
                dto.faunaStateCount = clampedFauna;
                changed = true;
                steps.Add("procedural fauna state count clamped");
            }

            int clampedHibernatedFauna = math.clamp(dto.hibernatedFaunaCount, 0, hibernatedFaunaBound);
            if (clampedHibernatedFauna != dto.hibernatedFaunaCount)
            {
                dto.hibernatedFaunaCount = clampedHibernatedFauna;
                changed = true;
                steps.Add("hibernated fauna state count clamped");
            }

            int clampedSeamStates = math.clamp(dto.geologySeamStateCount, 0, geologySeamBound);
            if (clampedSeamStates != dto.geologySeamStateCount)
            {
                dto.geologySeamStateCount = clampedSeamStates;
                changed = true;
                steps.Add("procedural geology seam count clamped");
            }

            int clampedCaveEntrances = math.clamp(dto.geologyCaveEntranceCount, 0, geologyCaveBound);
            if (clampedCaveEntrances != dto.geologyCaveEntranceCount)
            {
                dto.geologyCaveEntranceCount = clampedCaveEntrances;
                changed = true;
                steps.Add("procedural geology cave entrance count clamped");
            }

            return changed;
        }

        private static int ClampArrayLength<T>(T[] values, int maxCount)
        {
            if (values == null || maxCount <= 0)
                return 0;

            return math.min(values.Length, maxCount);
        }

        private static bool EnsureScanLog(ref ScanLogDTO dto, List<string> steps)
        {
            bool changed = false;
            int entryBound = ClampArrayLength(dto.entries, ScanLogDTO.MaxEntries);
            int recentEntryBound = ClampArrayLength(dto.recentEntryIds, ScanLogDTO.MaxRecentEntries);

            if (dto.entries == null || dto.entries.Length != ScanLogDTO.MaxEntries ||
                dto.recentEntryIds == null || dto.recentEntryIds.Length != ScanLogDTO.MaxRecentEntries)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("scan log capacity repaired");
            }

            int clampedEntries = math.clamp(dto.entryCount, 0, entryBound);
            if (clampedEntries != dto.entryCount)
            {
                dto.entryCount = clampedEntries;
                changed = true;
                steps.Add("scan log count clamped");
            }

            int clampedRecent = math.clamp(dto.recentCount, 0, recentEntryBound);
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
            int offerBound = ClampArrayLength(dto.offerStates, BarterDTO.MaxOffers);
            int transactionBound = ClampArrayLength(dto.recentTransactions, BarterDTO.MaxRecentTransactions);

            if (dto.offerStates == null || dto.offerStates.Length != BarterDTO.MaxOffers ||
                dto.recentTransactions == null || dto.recentTransactions.Length != BarterDTO.MaxRecentTransactions)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("barter capacity repaired");
            }

            int clampedStates = math.clamp(dto.stateCount, 0, offerBound);
            if (clampedStates != dto.stateCount)
            {
                dto.stateCount = clampedStates;
                changed = true;
                steps.Add("barter state count clamped");
            }

            int clampedTransactions = math.clamp(dto.recentTransactionCount, 0, transactionBound);
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
            int recentEntryBound = ClampArrayLength(dto.recentEntries, FieldOperationLogDTO.MaxRecentEntries);

            if (dto.recentEntries == null || dto.recentEntries.Length != FieldOperationLogDTO.MaxRecentEntries)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("field log capacity repaired");
            }

            int clamped = math.clamp(dto.recentCount, 0, recentEntryBound);
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
            int beaconBound = ClampArrayLength(dto.entries, BeaconNetworkDTO.MaxEntries);

            if (dto.entries == null || dto.entries.Length != BeaconNetworkDTO.MaxEntries)
            {
                dto.EnsureCapacity();
                changed = true;
                steps.Add("beacon capacity repaired");
            }

            int clamped = math.clamp(dto.activeCount, 0, beaconBound);
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
                dto.nextSequence = math.max(1, dto.activeCount + 1);
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
            changed |= TrimListToMax(
                data.audioLogDiscoveredIds,
                SaveData.MaxLegacyAudioLogDiscoveredIds,
                "audioLog list capped",
                steps);

            if (!AudioLogDiscoveryBitMask.HasExpectedCapacity(data.audioLogDiscoveryBitWords))
            {
                AudioLogDiscoveryBitMask.EnsureCapacity(ref data.audioLogDiscoveryBitWords);
                changed = true;
                steps.Add("audioLog discovery bit words created");
            }

            int encryptedFragmentBound = ClampArrayLength(
                data.audioLogEncryptedFragmentHashes,
                SaveData.MaxEncryptedAudioLogFragments);
            encryptedFragmentBound = math.min(
                encryptedFragmentBound,
                ClampArrayLength(data.audioLogEncryptedFragmentBits, SaveData.MaxEncryptedAudioLogFragments));

            if (data.audioLogEncryptedFragmentHashes == null ||
                data.audioLogEncryptedFragmentHashes.Length != SaveData.MaxEncryptedAudioLogFragments)
            {
                EnsureExactArrayCapacity(
                    ref data.audioLogEncryptedFragmentHashes,
                    SaveData.MaxEncryptedAudioLogFragments);
                changed = true;
                steps.Add("encrypted audio-log hash state created");
            }

            if (data.audioLogEncryptedFragmentBits == null ||
                data.audioLogEncryptedFragmentBits.Length != SaveData.MaxEncryptedAudioLogFragments)
            {
                EnsureExactArrayCapacity(
                    ref data.audioLogEncryptedFragmentBits,
                    SaveData.MaxEncryptedAudioLogFragments);
                changed = true;
                steps.Add("encrypted audio-log bit state created");
            }

            int clampedEncryptedFragmentCount = math.clamp(
                data.audioLogEncryptedFragmentCount,
                0,
                encryptedFragmentBound);
            if (clampedEncryptedFragmentCount != data.audioLogEncryptedFragmentCount)
            {
                data.audioLogEncryptedFragmentCount = clampedEncryptedFragmentCount;
                changed = true;
                steps.Add("encrypted audio-log fragment count clamped");
            }

            if (!IndustrialLoreBitMask.HasExpectedCapacity(data.industrialLoreUnlockWords))
            {
                IndustrialLoreBitMask.EnsureCapacity(ref data.industrialLoreUnlockWords);
                changed = true;
                steps.Add("industrial lore bit words created");
            }

            if (!DataArchaeologyDiscoveryBitMask.HasExpectedCapacity(data.dataArchaeologyDiscoveryBitWords))
            {
                DataArchaeologyDiscoveryBitMask.EnsureCapacity(ref data.dataArchaeologyDiscoveryBitWords);
                changed = true;
                steps.Add("data archaeology bit words created");
            }

            int archaeologyPartialBound = ClampArrayLength(
                data.dataArchaeologyPartialScanHashes,
                SaveData.MaxDataArchaeologyPartialScans);
            archaeologyPartialBound = math.min(
                archaeologyPartialBound,
                ClampArrayLength(
                    data.dataArchaeologyPartialScanProgressPermille,
                    SaveData.MaxDataArchaeologyPartialScans));

            if (data.dataArchaeologyPartialScanHashes == null ||
                data.dataArchaeologyPartialScanHashes.Length != SaveData.MaxDataArchaeologyPartialScans)
            {
                EnsureExactArrayCapacity(
                    ref data.dataArchaeologyPartialScanHashes,
                    SaveData.MaxDataArchaeologyPartialScans);
                changed = true;
                steps.Add("data archaeology partial hashes created");
            }

            if (data.dataArchaeologyPartialScanProgressPermille == null ||
                data.dataArchaeologyPartialScanProgressPermille.Length != SaveData.MaxDataArchaeologyPartialScans)
            {
                EnsureExactArrayCapacity(
                    ref data.dataArchaeologyPartialScanProgressPermille,
                    SaveData.MaxDataArchaeologyPartialScans);
                changed = true;
                steps.Add("data archaeology partial progress created");
            }

            int clampedArchaeologyPartialCount = math.clamp(
                data.dataArchaeologyPartialScanCount,
                0,
                archaeologyPartialBound);
            if (clampedArchaeologyPartialCount != data.dataArchaeologyPartialScanCount)
            {
                data.dataArchaeologyPartialScanCount = clampedArchaeologyPartialCount;
                changed = true;
                steps.Add("data archaeology partial count clamped");
            }

            int archaeologyScanStateBound = ClampArrayLength(
                data.dataArchaeologyScanStateKeys,
                SaveData.MaxDataArchaeologyScanStates);
            archaeologyScanStateBound = math.min(
                archaeologyScanStateBound,
                ClampArrayLength(data.dataArchaeologyScanStateValues, SaveData.MaxDataArchaeologyScanStates));

            if (data.dataArchaeologyScanStateKeys == null ||
                data.dataArchaeologyScanStateKeys.Length != SaveData.MaxDataArchaeologyScanStates)
            {
                EnsureExactArrayCapacity(
                    ref data.dataArchaeologyScanStateKeys,
                    SaveData.MaxDataArchaeologyScanStates);
                changed = true;
                steps.Add("data archaeology scan-state keys created");
            }

            if (data.dataArchaeologyScanStateValues == null ||
                data.dataArchaeologyScanStateValues.Length != SaveData.MaxDataArchaeologyScanStates)
            {
                EnsureExactArrayCapacity(
                    ref data.dataArchaeologyScanStateValues,
                    SaveData.MaxDataArchaeologyScanStates);
                changed = true;
                steps.Add("data archaeology scan-state values created");
            }

            int clampedArchaeologyScanStateCount = math.clamp(
                data.dataArchaeologyScanStateCount,
                0,
                archaeologyScanStateBound);
            if (clampedArchaeologyScanStateCount != data.dataArchaeologyScanStateCount)
            {
                data.dataArchaeologyScanStateCount = clampedArchaeologyScanStateCount;
                changed = true;
                steps.Add("data archaeology scan-state count clamped");
            }

            if (data.questActiveIds == null)
            {
                data.questActiveIds = new System.Collections.Generic.List<string>();
                changed = true;
                steps.Add("quest active list created");
            }
            changed |= TrimListToMax(
                data.questActiveIds,
                SaveData.MaxLegacyQuestIds,
                "quest active list capped",
                steps);

            if (data.questCompletedIds == null)
            {
                data.questCompletedIds = new System.Collections.Generic.List<string>();
                changed = true;
                steps.Add("quest completed list created");
            }
            changed |= TrimListToMax(
                data.questCompletedIds,
                SaveData.MaxLegacyQuestIds,
                "quest completed list capped",
                steps);

            if (data.suitInstalledUpgradeIds == null)
            {
                data.suitInstalledUpgradeIds = new System.Collections.Generic.List<string>();
                changed = true;
                steps.Add("suit upgrades list created");
            }
            changed |= TrimListToMax(
                data.suitInstalledUpgradeIds,
                SaveData.MaxSuitUpgradeIds,
                "suit upgrades list capped",
                steps);

            if (data.suitUnlockedBlueprintIds == null)
            {
                data.suitUnlockedBlueprintIds = new System.Collections.Generic.List<string>();
                changed = true;
                steps.Add("suit blueprints list created");
            }
            changed |= TrimListToMax(
                data.suitUnlockedBlueprintIds,
                SaveData.MaxSuitUpgradeIds,
                "suit blueprints list capped",
                steps);

            if (data.corporateReceivedOrderIds == null)
            {
                data.corporateReceivedOrderIds = new System.Collections.Generic.List<string>();
                changed = true;
                steps.Add("corporate orders list created");
            }
            changed |= TrimListToMax(
                data.corporateReceivedOrderIds,
                SaveData.MaxCorporateOrderIds,
                "corporate orders list capped",
                steps);

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
            changed |= TrimPairedListsToMax(
                data.corporatePendingOrderIds,
                data.corporatePendingOrderTimers,
                SaveData.MaxCorporateOrderIds,
                "corporate pending orders capped",
                steps);

            if (data.missionActiveIds == null)
            {
                data.missionActiveIds = new System.Collections.Generic.List<string>();
                changed = true;
                steps.Add("mission active list created");
            }
            changed |= TrimListToMax(
                data.missionActiveIds,
                SaveData.MaxMissionIds,
                "mission active list capped",
                steps);

            if (data.missionCompletedIds == null)
            {
                data.missionCompletedIds = new System.Collections.Generic.List<string>();
                changed = true;
                steps.Add("mission completed list created");
            }
            changed |= TrimListToMax(
                data.missionCompletedIds,
                SaveData.MaxMissionIds,
                "mission completed list capped",
                steps);

            int inferredRevealStage = data.endingConditionMet
                ? MaxAtlasRevealStage
                : data.narrativeDepthTier >= 4
                    ? 3
                    : data.narrativeDepthTier >= 3
                        ? 2
                        : data.atlasSignalDetected
                            ? 2
                            : 0;

            int clampedRevealStage = math.clamp(data.atlasSignalRevealStage, 0, MaxAtlasRevealStage);
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

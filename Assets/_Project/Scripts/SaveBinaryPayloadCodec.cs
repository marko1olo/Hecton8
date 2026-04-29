using System;
using System.Collections.Generic;
using Hecton.Localization;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.SaveSystem
{
    internal static unsafe class SaveBinaryPayloadCodec
    {
        private const ushort BiologicalItemStateMask = 1 << 6;
        private const ushort DefaultQualityMilli = 1000;
        private const float BiologicalReferenceTemperatureCelsius = 4f;
        private const float BiologicalDecayRatePerSecond = 0.001f;
        private const int NullCollectionCount = -1;
        private static readonly byte[] s_lz4CompressionDictionary = SaveCompressionDictionary.Bytes;

        internal static int Lz4CompressionDictionaryLength => s_lz4CompressionDictionary.Length;

        internal static void CopyLz4CompressionDictionary(byte* destinationPtr, int destinationCapacity)
        {
            if (destinationPtr == null || destinationCapacity < s_lz4CompressionDictionary.Length)
                return;

            fixed (byte* sourcePtr = s_lz4CompressionDictionary)
            {
                UnsafeUtility.MemCpy(destinationPtr, sourcePtr, s_lz4CompressionDictionary.Length);
            }
        }

        internal static bool TryWrite(SaveData data, byte* destination, int capacity, out int bytesWritten, out string error)
        {
            bytesWritten = 0;
            error = string.Empty;

            if (data == null)
            {
                error = "Save payload is null.";
                return false;
            }

            if (destination == null || capacity <= 0)
            {
                error = "Save payload target buffer is invalid.";
                return false;
            }

            BufferWriter writer = new BufferWriter(destination, capacity);
            if (!WriteSaveData(data, ref writer))
            {
                error = writer.Error;
                return false;
            }

            bytesWritten = writer.BytesWritten;
            return true;
        }

        internal static bool TryRead(byte* source, int length, out SaveData data, out int bytesRead, out string error)
        {
            bytesRead = 0;
            error = string.Empty;
            data = null;

            if (source == null || length < 0)
            {
                error = "Save payload source buffer is invalid.";
                return false;
            }

            data = SaveData.CreateNew(0d);
            data.voxelDeltaPersistence = VoxelDeltaPersistenceDTO.CreateDefault();

            BufferReader reader = new BufferReader(source, length);
            if (!ReadSaveData(ref reader, data))
            {
                error = reader.Error;
                data = null;
                return false;
            }

            bytesRead = reader.BytesRead;
            return true;
        }

        private static bool WriteSaveData(SaveData data, ref BufferWriter writer)
        {
            return writer.WriteInt(data.version)
                && writer.WriteString(data.timestamp)
                && writer.WriteDouble(data.totalPlayTime)
                && WritePlayerStats(ref writer, data.playerStats)
                && WriteInventory(ref writer, data.inventory)
                && WriteWorldState(ref writer, data.worldState)
                && WriteProceduralWorldState(ref writer, data.proceduralWorldState)
                && WriteConstruction(ref writer, data.construction)
                && WriteScanLog(ref writer, data.scanLog)
                && WriteBarter(ref writer, data.barter)
                && WriteFieldOperationLog(ref writer, data.fieldOperations)
                && WriteBeaconNetwork(ref writer, data.beaconNetwork)
                && WriteExplorationMap(ref writer, data.explorationMap)
                && WritePdaLogbook(ref writer, data.pdaLogbook)
                && WritePdaMarkers(ref writer, data.pdaMarkers)
                && writer.WriteStruct(data.pdaAdvisories)
                && WriteProceduralLore(ref writer, data.proceduralLore)
                && WriteAchievementRegistry(ref writer, data.achievements)
                && WriteRunModifiers(ref writer, data.runModifiers)
                && WriteResourceScarcity(ref writer, data.resourceScarcity)
                && writer.WriteStruct(data.environmentalStrain)
                && WriteEcosystemState(ref writer, data.ecosystemState)
                && WriteExternalScavengerSites(ref writer, data.externalScavengerSites)
                && WriteStringFloatDictionary(ref writer, data.toolDurabilityMap)
                && WriteStringBoolDictionary(ref writer, data.toolBrokenMap)
                && WriteIntHashSet(ref writer, data.discoveredBiomeIds)
                && writer.WriteStructArray(data.discoveredBiomeBitWords)
                && writer.WriteInt(data.lastDiscoveredBiomeId)
                && writer.WriteInt(data.narrativeDiscoveryCount)
                && WriteStringArray(ref writer, data.narrativeDiscoveryIds)
                && writer.WriteInt(data.narrativeDepthTier)
                && WriteStringList(ref writer, data.audioLogDiscoveredIds)
                && writer.WriteStructArray(data.industrialLoreUnlockWords)
                && WriteStringList(ref writer, data.questActiveIds)
                && WriteStringList(ref writer, data.questCompletedIds)
                && writer.WriteBool(data.atlasSignalDetected)
                && writer.WriteFloat(data.atlasSignalPulseTimer)
                && writer.WriteInt(data.atlasSignalRevealStage)
                && WriteStringList(ref writer, data.suitInstalledUpgradeIds)
                && WriteStringList(ref writer, data.suitUnlockedBlueprintIds)
                && WriteStringList(ref writer, data.suitBrokenUpgradeIds)
                && writer.WriteString(data.playerExpressionProfileId)
                && writer.WriteInt(data.atlas6PlayerStatus)
                && writer.WriteInt(data.atlas6BarterCount)
                && writer.WriteBool(data.atlas6DirectiveConflictTriggered)
                && WriteStringList(ref writer, data.corporateReceivedOrderIds)
                && WriteStringList(ref writer, data.corporatePendingOrderIds)
                && WriteFloatList(ref writer, data.corporatePendingOrderTimers)
                && writer.WriteFloat(data.firstHourSessionTime)
                && writer.WriteInt(data.firstHourMilestones)
                && writer.WriteInt(data.firstHourGuidanceFlags)
                && writer.WriteInt(data.endingChoice)
                && writer.WriteBool(data.endingComplete)
                && writer.WriteBool(data.endingConditionMet)
                && WriteStringList(ref writer, data.missionActiveIds)
                && WriteStringList(ref writer, data.missionCompletedIds)
                && writer.WriteInt(data.LODQualityPreset)
                && writer.WriteBool(data.DynamicResolutionEnabled)
                && WriteStringStringDictionary(ref writer, data.CustomModData);
        }

        private static bool ReadSaveData(ref BufferReader reader, SaveData data)
        {
            if (!reader.ReadInt(out data.version)
                || !reader.ReadString(out data.timestamp)
                || !ReadTotalPlayTime(ref reader, data.version, out data.totalPlayTime)
                || !ReadPlayerStats(ref reader, data.version, out data.playerStats)
                || !ReadInventory(ref reader, data.version, out data.inventory)
                || !ReadWorldState(ref reader, out data.worldState)
                || !ReadProceduralWorldState(ref reader, out data.proceduralWorldState)
                || !ReadConstruction(ref reader, out data.construction)
                || !ReadScanLog(ref reader, out data.scanLog)
                || !ReadBarter(ref reader, out data.barter)
                || !ReadFieldOperationLog(ref reader, out data.fieldOperations)
                || !ReadBeaconNetwork(ref reader, out data.beaconNetwork)
                || !ReadExplorationMap(ref reader, out data.explorationMap)
                || !ReadPdaLogbook(ref reader, out data.pdaLogbook)
                || !ReadPdaMarkers(ref reader, out data.pdaMarkers)
                || !reader.ReadStruct(out data.pdaAdvisories)
                || !ReadProceduralLore(ref reader, out data.proceduralLore)
                || !ReadAchievementRegistry(ref reader, out data.achievements)
                || !ReadRunModifiers(ref reader, out data.runModifiers)
                || !ReadResourceScarcity(ref reader, out data.resourceScarcity)
                || !reader.ReadStruct(out data.environmentalStrain)
                || !ReadEcosystemState(ref reader, out data.ecosystemState)
                || !ReadExternalScavengerSites(ref reader, data.version, out data.externalScavengerSites)
                || !ReadStringFloatDictionary(ref reader, out data.toolDurabilityMap)
                || !ReadStringBoolDictionary(ref reader, out data.toolBrokenMap)
                || !ReadIntHashSet(ref reader, out data.discoveredBiomeIds)
                || !reader.ReadStructArray(out data.discoveredBiomeBitWords)
                || !reader.ReadInt(out data.lastDiscoveredBiomeId)
                || !reader.ReadInt(out data.narrativeDiscoveryCount)
                || !ReadStringArray(ref reader, out data.narrativeDiscoveryIds)
                || !reader.ReadInt(out data.narrativeDepthTier)
                || !ReadStringList(ref reader, out data.audioLogDiscoveredIds)
                || !reader.ReadStructArray(out data.industrialLoreUnlockWords)
                || !ReadStringList(ref reader, out data.questActiveIds)
                || !ReadStringList(ref reader, out data.questCompletedIds)
                || !reader.ReadBool(out data.atlasSignalDetected)
                || !reader.ReadFloat(out data.atlasSignalPulseTimer)
                || !reader.ReadInt(out data.atlasSignalRevealStage)
                || !ReadStringList(ref reader, out data.suitInstalledUpgradeIds)
                || !ReadStringList(ref reader, out data.suitUnlockedBlueprintIds)
                || !ReadStringList(ref reader, out data.suitBrokenUpgradeIds)
                || !reader.ReadString(out data.playerExpressionProfileId)
                || !reader.ReadInt(out data.atlas6PlayerStatus)
                || !reader.ReadInt(out data.atlas6BarterCount)
                || !reader.ReadBool(out data.atlas6DirectiveConflictTriggered)
                || !ReadStringList(ref reader, out data.corporateReceivedOrderIds)
                || !ReadStringList(ref reader, out data.corporatePendingOrderIds)
                || !ReadFloatList(ref reader, out data.corporatePendingOrderTimers)
                || !reader.ReadFloat(out data.firstHourSessionTime)
                || !reader.ReadInt(out data.firstHourMilestones)
                || !reader.ReadInt(out data.firstHourGuidanceFlags)
                || !reader.ReadInt(out data.endingChoice)
                || !reader.ReadBool(out data.endingComplete)
                || !reader.ReadBool(out data.endingConditionMet)
                || !ReadStringList(ref reader, out data.missionActiveIds)
                || !ReadStringList(ref reader, out data.missionCompletedIds)
                || !reader.ReadInt(out data.LODQualityPreset)
                || !reader.ReadBool(out data.DynamicResolutionEnabled)
                || !ReadStringStringDictionary(ref reader, out data.CustomModData))
            {
                return false;
            }

            ApplyInventoryBiologicalDecay(ref data.inventory, data.playerStats.environmentTemperature);
            data.voxelDeltaPersistence = VoxelDeltaPersistenceDTO.CreateDefault();
            return true;
        }

        private static bool WriteInventory(ref BufferWriter writer, InventoryDTO value)
        {
            return writer.WriteInt(value.cellCount)
                && writer.WriteStructArray(value.itemHashIds)
                && writer.WriteStructArray(value.packedCellCoordinates)
                && writer.WriteStructArray(value.stackCounts)
                && writer.WriteStructArray(value.itemStateFlags)
                && writer.WriteStructArray(value.qualityMilli)
                && writer.WriteStructArray(value.lastUpdateUnixSeconds)
                && writer.WriteFloat(value.totalWeight)
                && writer.WriteInt(value.gridColumns)
                && writer.WriteInt(value.gridRows);
        }

        private static bool WriteExternalScavengerSites(ref BufferWriter writer, ExternalScavengerSiteDTO[] value)
        {
            return writer.WriteStructArray(value);
        }

        private static bool WritePlayerStats(ref BufferWriter writer, PlayerStatsDTO value)
        {
            return writer.WriteFloat(value.oxygen)
                && writer.WriteFloat(value.energy)
                && writer.WriteFloat(value.integrity)
                && writer.WriteFloat(value.weight)
                && writer.WriteFloat(value.hunger)
                && writer.WriteFloat(value.thirst)
                && writer.WriteDouble(value.currentLifeDurationSeconds)
                && writer.WriteDouble(value.currentLifePeakDepthMeters)
                && writer.WriteFloat(value.currentLifeLowestOxygenNormalized)
                && writer.WriteFloat(value.currentLifeLowestEnergyNormalized)
                && writer.WriteFloat(value.currentLifeLowestIntegrityNormalized)
                && writer.WriteByte(value.injuryFlags)
                && writer.WriteFloat(value.bleedingSecondsRemaining)
                && writer.WriteFloat(value.bleedingDamagePerSecond)
                && writer.WriteFloat(value.bleedingSeverity01)
                && writer.WriteFloat(value.fractureSecondsRemaining)
                && writer.WriteFloat(value.fracturePenalty01)
                && writer.WriteFloat(value.environmentTemperature)
                && writer.WriteFloat(value.coldStressSeverity01)
                && writer.WriteFloat(value.heatStressSeverity01)
                && writer.WriteBool(value.hasLastDeathRecord)
                && writer.WriteByte(value.lastDeathCause)
                && writer.WriteFloat(value.lastDeathPosX)
                && writer.WriteFloat(value.lastDeathPosY)
                && writer.WriteFloat(value.lastDeathPosZ)
                && writer.WriteDouble(value.lastDeathLifeDurationSeconds)
                && writer.WriteDouble(value.lastDeathPeakDepthMeters)
                && writer.WriteFloat(value.lastDeathLowestOxygenNormalized)
                && writer.WriteFloat(value.lastDeathLowestEnergyNormalized)
                && writer.WriteFloat(value.lastDeathLowestIntegrityNormalized)
                && writer.WriteFloat(value.posX)
                && writer.WriteFloat(value.posY)
                && writer.WriteFloat(value.posZ)
                && writer.WriteFloat(value.rotX)
                && writer.WriteFloat(value.rotY)
                && writer.WriteFloat(value.rotZ)
                && writer.WriteFloat(value.rotW)
                && writer.WriteFloat(value.velX)
                && writer.WriteFloat(value.velY)
                && writer.WriteFloat(value.velZ);
        }

        private static bool ReadPlayerStats(ref BufferReader reader, int version, out PlayerStatsDTO value)
        {
            value = default;

            return reader.ReadFloat(out value.oxygen)
                && reader.ReadFloat(out value.energy)
                && reader.ReadFloat(out value.integrity)
                && reader.ReadFloat(out value.weight)
                && reader.ReadFloat(out value.hunger)
                && reader.ReadFloat(out value.thirst)
                && ReadLifeTelemetryValue(ref reader, version, out value.currentLifeDurationSeconds)
                && ReadLifeTelemetryValue(ref reader, version, out value.currentLifePeakDepthMeters)
                && reader.ReadFloat(out value.currentLifeLowestOxygenNormalized)
                && reader.ReadFloat(out value.currentLifeLowestEnergyNormalized)
                && reader.ReadFloat(out value.currentLifeLowestIntegrityNormalized)
                && reader.ReadByte(out value.injuryFlags)
                && reader.ReadFloat(out value.bleedingSecondsRemaining)
                && reader.ReadFloat(out value.bleedingDamagePerSecond)
                && reader.ReadFloat(out value.bleedingSeverity01)
                && reader.ReadFloat(out value.fractureSecondsRemaining)
                && reader.ReadFloat(out value.fracturePenalty01)
                && reader.ReadFloat(out value.environmentTemperature)
                && reader.ReadFloat(out value.coldStressSeverity01)
                && reader.ReadFloat(out value.heatStressSeverity01)
                && reader.ReadBool(out value.hasLastDeathRecord)
                && reader.ReadByte(out value.lastDeathCause)
                && reader.ReadFloat(out value.lastDeathPosX)
                && reader.ReadFloat(out value.lastDeathPosY)
                && reader.ReadFloat(out value.lastDeathPosZ)
                && ReadLifeTelemetryValue(ref reader, version, out value.lastDeathLifeDurationSeconds)
                && ReadLifeTelemetryValue(ref reader, version, out value.lastDeathPeakDepthMeters)
                && reader.ReadFloat(out value.lastDeathLowestOxygenNormalized)
                && reader.ReadFloat(out value.lastDeathLowestEnergyNormalized)
                && reader.ReadFloat(out value.lastDeathLowestIntegrityNormalized)
                && reader.ReadFloat(out value.posX)
                && reader.ReadFloat(out value.posY)
                && reader.ReadFloat(out value.posZ)
                && reader.ReadFloat(out value.rotX)
                && reader.ReadFloat(out value.rotY)
                && reader.ReadFloat(out value.rotZ)
                && reader.ReadFloat(out value.rotW)
                && ReadPlayerVelocity(ref reader, version, ref value);
        }

        private static bool ReadPlayerVelocity(ref BufferReader reader, int version, ref PlayerStatsDTO value)
        {
            if (version < 41)
            {
                value.velX = 0f;
                value.velY = 0f;
                value.velZ = 0f;
                return true;
            }

            return reader.ReadFloat(out value.velX)
                && reader.ReadFloat(out value.velY)
                && reader.ReadFloat(out value.velZ);
        }

        private static bool ReadTotalPlayTime(ref BufferReader reader, int version, out double value)
        {
            value = 0d;
            if (version >= 39)
                return reader.ReadDouble(out value);

            if (!reader.ReadFloat(out float legacyValue))
                return false;

            value = legacyValue;
            return true;
        }

        private static bool ReadLifeTelemetryValue(ref BufferReader reader, int version, out double value)
        {
            value = 0d;
            if (version >= 39)
                return reader.ReadDouble(out value);

            if (!reader.ReadFloat(out float legacyValue))
                return false;

            value = legacyValue;
            return true;
        }

        private static bool ReadInventory(ref BufferReader reader, int version, out InventoryDTO value)
        {
            value = default;
            if (!reader.ReadInt(out value.cellCount))
                return false;

            if (version >= 40)
            {
                bool ok = reader.ReadStructArray(out value.itemHashIds)
                    && reader.ReadStructArray(out value.packedCellCoordinates)
                    && reader.ReadStructArray(out value.stackCounts)
                    && ReadInventoryStateArrays(ref reader, version, ref value)
                    && reader.ReadFloat(out value.totalWeight)
                    && reader.ReadInt(out value.gridColumns)
                    && reader.ReadInt(out value.gridRows);

                if (!ok)
                    return false;

                value.EnsureCapacity();
                return true;
            }

            if (!ReadInventoryCellArray(ref reader, out InventoryCellDTO[] legacyCells)
                || !reader.ReadFloat(out value.totalWeight)
                || !reader.ReadInt(out value.gridColumns)
                || !reader.ReadInt(out value.gridRows))
            {
                return false;
            }

            value.EnsureCapacity();
            int legacyCapacity = legacyCells != null ? legacyCells.Length : 0;
            int safeCount = Math.Min(Math.Min(value.cellCount, legacyCapacity), InventoryDTO.MaxCells);
            value.cellCount = safeCount;

            for (int i = 0; i < safeCount; i++)
            {
                InventoryCellDTO legacyCell = legacyCells[i];
                value.itemHashIds[i] = LocHash.Compute(legacyCell.itemId);
                value.packedCellCoordinates[i] = InventoryDTO.PackCellCoordinate(legacyCell.x, legacyCell.y);
                value.stackCounts[i] = (ushort)Math.Clamp(legacyCell.stackCount > 0 ? legacyCell.stackCount : 1, 1, ushort.MaxValue);
                value.qualityMilli[i] = DefaultQualityMilli;
            }

            return true;
        }

        private static bool ReadInventoryStateArrays(ref BufferReader reader, int version, ref InventoryDTO value)
        {
            if (version >= 43)
            {
                return reader.ReadStructArray(out value.itemStateFlags)
                    && reader.ReadStructArray(out value.qualityMilli)
                    && reader.ReadStructArray(out value.lastUpdateUnixSeconds);
            }

            value.EnsureCapacity();
            int safeCount = Math.Min(
                value.cellCount,
                Math.Min(value.itemHashIds != null ? value.itemHashIds.Length : 0,
                    Math.Min(value.packedCellCoordinates != null ? value.packedCellCoordinates.Length : 0, value.stackCounts != null ? value.stackCounts.Length : 0)));
            value.cellCount = safeCount;
            for (int i = 0; i < safeCount; i++)
                value.qualityMilli[i] = DefaultQualityMilli;

            return true;
        }

        private static void ApplyInventoryBiologicalDecay(ref InventoryDTO value, float ambientTemperature)
        {
            if (value.cellCount <= 0 ||
                value.itemStateFlags == null ||
                value.qualityMilli == null ||
                value.lastUpdateUnixSeconds == null)
            {
                return;
            }

            int safeCount = Math.Min(value.cellCount, Math.Min(value.itemStateFlags.Length, Math.Min(value.qualityMilli.Length, value.lastUpdateUnixSeconds.Length)));
            if (safeCount <= 0)
                return;

            long utcNowSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            uint now = utcNowSeconds <= 0L
                ? 0u
                : utcNowSeconds >= uint.MaxValue
                    ? uint.MaxValue
                    : (uint)utcNowSeconds;

            float tempFactor = (float)Math.Exp((ambientTemperature - BiologicalReferenceTemperatureCelsius) * 0.05f);
            for (int i = 0; i < safeCount; i++)
            {
                if ((value.itemStateFlags[i] & BiologicalItemStateMask) == 0)
                {
                    if (value.qualityMilli[i] == 0)
                        value.qualityMilli[i] = DefaultQualityMilli;

                    continue;
                }

                uint lastUpdate = value.lastUpdateUnixSeconds[i];
                if (lastUpdate == 0u)
                {
                    value.lastUpdateUnixSeconds[i] = now;
                    if (value.qualityMilli[i] == 0)
                        value.qualityMilli[i] = DefaultQualityMilli;
                    continue;
                }

                uint elapsedSeconds = now >= lastUpdate ? now - lastUpdate : 0u;
                float currentQuality = Math.Clamp((value.qualityMilli[i] > 0 ? value.qualityMilli[i] : DefaultQualityMilli) / 1000f, 0f, 1f);
                float qualityDelta = elapsedSeconds * BiologicalDecayRatePerSecond * tempFactor;
                float decayedQuality = Math.Clamp(currentQuality - qualityDelta, 0f, 1f);
                value.qualityMilli[i] = (ushort)Math.Clamp((int)Math.Round(decayedQuality * 1000f), 0, 1000);
                value.lastUpdateUnixSeconds[i] = now;
            }
        }

        private static bool ReadExternalScavengerSites(ref BufferReader reader, int version, out ExternalScavengerSiteDTO[] value)
        {
            value = null;
            if (version < 42)
                return true;

            return reader.ReadStructArray(out value);
        }

        private static bool WriteWorldState(ref BufferWriter writer, WorldStateDTO value)
        {
            return writer.WriteInt(value.depletedCount)
                && WriteStringArray(ref writer, value.depletedNodeIds)
                && writer.WriteInt(value.depletedPickupChunkCount)
                && writer.WriteStructArray(value.depletedPickupChunkKeys)
                && writer.WriteStructArray(value.depletedPickupChunkWordStarts)
                && writer.WriteStructArray(value.depletedPickupChunkWordCounts)
                && writer.WriteInt(value.depletedPickupWordCount)
                && writer.WriteStructArray(value.depletedPickupWords);
        }

        private static bool ReadWorldState(ref BufferReader reader, out WorldStateDTO value)
        {
            value = default;
            return reader.ReadInt(out value.depletedCount)
                && ReadStringArray(ref reader, out value.depletedNodeIds)
                && reader.ReadInt(out value.depletedPickupChunkCount)
                && reader.ReadStructArray(out value.depletedPickupChunkKeys)
                && reader.ReadStructArray(out value.depletedPickupChunkWordStarts)
                && reader.ReadStructArray(out value.depletedPickupChunkWordCounts)
                && reader.ReadInt(out value.depletedPickupWordCount)
                && reader.ReadStructArray(out value.depletedPickupWords);
        }

        private static bool WriteProceduralWorldState(ref BufferWriter writer, ProceduralWorldStateDTO value)
        {
            return writer.WriteInt(value.suppressedPlacementCount)
                && writer.WriteStructArray(value.suppressedPlacementKeys)
                && writer.WriteInt(value.faunaStateCount)
                && writer.WriteStructArray(value.faunaStates);
        }

        private static bool ReadProceduralWorldState(ref BufferReader reader, out ProceduralWorldStateDTO value)
        {
            value = default;
            return reader.ReadInt(out value.suppressedPlacementCount)
                && reader.ReadStructArray(out value.suppressedPlacementKeys)
                && reader.ReadInt(out value.faunaStateCount)
                && reader.ReadStructArray(out value.faunaStates);
        }

        private static bool WriteConstruction(ref BufferWriter writer, ConstructionDTO value)
        {
            return writer.WriteInt(value.moduleCount)
                && WriteModuleArray(ref writer, value.modules);
        }

        private static bool ReadConstruction(ref BufferReader reader, out ConstructionDTO value)
        {
            value = default;
            return reader.ReadInt(out value.moduleCount)
                && ReadModuleArray(ref reader, out value.modules);
        }

        private static bool WriteScanLog(ref BufferWriter writer, ScanLogDTO value)
        {
            return writer.WriteInt(value.entryCount)
                && WriteScanEntryArray(ref writer, value.entries)
                && writer.WriteInt(value.recentCount)
                && WriteStringArray(ref writer, value.recentEntryIds);
        }

        private static bool ReadScanLog(ref BufferReader reader, out ScanLogDTO value)
        {
            value = default;
            return reader.ReadInt(out value.entryCount)
                && ReadScanEntryArray(ref reader, out value.entries)
                && reader.ReadInt(out value.recentCount)
                && ReadStringArray(ref reader, out value.recentEntryIds);
        }

        private static bool WriteBarter(ref BufferWriter writer, BarterDTO value)
        {
            return writer.WriteInt(value.stateCount)
                && WriteBarterOfferStateArray(ref writer, value.offerStates)
                && writer.WriteInt(value.recentTransactionCount)
                && WriteBarterTransactionArray(ref writer, value.recentTransactions);
        }

        private static bool ReadBarter(ref BufferReader reader, out BarterDTO value)
        {
            value = default;
            return reader.ReadInt(out value.stateCount)
                && ReadBarterOfferStateArray(ref reader, out value.offerStates)
                && reader.ReadInt(out value.recentTransactionCount)
                && ReadBarterTransactionArray(ref reader, out value.recentTransactions);
        }

        private static bool WriteFieldOperationLog(ref BufferWriter writer, FieldOperationLogDTO value)
        {
            return writer.WriteInt(value.recentCount)
                && WriteFieldOperationEntryArray(ref writer, value.recentEntries);
        }

        private static bool ReadFieldOperationLog(ref BufferReader reader, out FieldOperationLogDTO value)
        {
            value = default;
            return reader.ReadInt(out value.recentCount)
                && ReadFieldOperationEntryArray(ref reader, out value.recentEntries);
        }

        private static bool WriteBeaconNetwork(ref BufferWriter writer, BeaconNetworkDTO value)
        {
            return writer.WriteInt(value.activeCount)
                && writer.WriteInt(value.nextSequence)
                && WriteBeaconEntryArray(ref writer, value.entries);
        }

        private static bool ReadBeaconNetwork(ref BufferReader reader, out BeaconNetworkDTO value)
        {
            value = default;
            return reader.ReadInt(out value.activeCount)
                && reader.ReadInt(out value.nextSequence)
                && ReadBeaconEntryArray(ref reader, out value.entries);
        }

        private static bool WriteExplorationMap(ref BufferWriter writer, ExplorationMapDTO value)
        {
            return writer.WriteInt(value.exploredChunkCount)
                && writer.WriteStructArray(value.exploredChunkKeys);
        }

        private static bool ReadExplorationMap(ref BufferReader reader, out ExplorationMapDTO value)
        {
            value = default;
            return reader.ReadInt(out value.exploredChunkCount)
                && reader.ReadStructArray(out value.exploredChunkKeys);
        }

        private static bool WritePdaLogbook(ref BufferWriter writer, PDALogbookDTO value)
        {
            return writer.WriteInt(value.entryCount)
                && writer.WriteInt(value.nextSequence)
                && WritePdaLogbookEntryArray(ref writer, value.entries)
                && writer.WriteInt(value.seenOriginCount)
                && WriteStringArray(ref writer, value.seenOriginKeys);
        }

        private static bool ReadPdaLogbook(ref BufferReader reader, out PDALogbookDTO value)
        {
            value = default;
            return reader.ReadInt(out value.entryCount)
                && reader.ReadInt(out value.nextSequence)
                && ReadPdaLogbookEntryArray(ref reader, out value.entries)
                && reader.ReadInt(out value.seenOriginCount)
                && ReadStringArray(ref reader, out value.seenOriginKeys);
        }

        private static bool WritePdaMarkers(ref BufferWriter writer, PDAMarkerRegistryDTO value)
        {
            return writer.WriteInt(value.markerCount)
                && writer.WriteInt(value.nextSequence)
                && WritePdaMarkerEntryArray(ref writer, value.entries);
        }

        private static bool ReadPdaMarkers(ref BufferReader reader, out PDAMarkerRegistryDTO value)
        {
            value = default;
            return reader.ReadInt(out value.markerCount)
                && reader.ReadInt(out value.nextSequence)
                && ReadPdaMarkerEntryArray(ref reader, out value.entries);
        }

        private static bool WriteProceduralLore(ref BufferWriter writer, ProceduralLoreStateDTO value)
        {
            return writer.WriteInt(value.activeCount)
                && writer.WriteInt(value.nextSourceIndex)
                && WriteProceduralLorePlacementArray(ref writer, value.activePlacements);
        }

        private static bool ReadProceduralLore(ref BufferReader reader, out ProceduralLoreStateDTO value)
        {
            value = default;
            return reader.ReadInt(out value.activeCount)
                && reader.ReadInt(out value.nextSourceIndex)
                && ReadProceduralLorePlacementArray(ref reader, out value.activePlacements);
        }

        private static bool WriteAchievementRegistry(ref BufferWriter writer, AchievementRegistryDTO value)
        {
            return writer.WriteFloat(value.swamDistanceMeters)
                && writer.WriteInt(value.craftedItemCount)
                && writer.WriteInt(value.discoveredBiomeCount)
                && writer.WriteInt(value.unlockedCount)
                && WriteStringArray(ref writer, value.unlockedIds);
        }

        private static bool ReadAchievementRegistry(ref BufferReader reader, out AchievementRegistryDTO value)
        {
            value = default;
            return reader.ReadFloat(out value.swamDistanceMeters)
                && reader.ReadInt(out value.craftedItemCount)
                && reader.ReadInt(out value.discoveredBiomeCount)
                && reader.ReadInt(out value.unlockedCount)
                && ReadStringArray(ref reader, out value.unlockedIds);
        }

        private static bool WriteRunModifiers(ref BufferWriter writer, RunModifiersDTO value)
        {
            return writer.WriteBool(value.isPermadeath)
                && writer.WriteBool(value.isNightmareMode)
                && writer.WriteBool(value.isDailySeed)
                && writer.WriteBool(value.runMarkedDead)
                && writer.WriteString(value.dailySeedId);
        }

        private static bool ReadRunModifiers(ref BufferReader reader, out RunModifiersDTO value)
        {
            value = default;
            return reader.ReadBool(out value.isPermadeath)
                && reader.ReadBool(out value.isNightmareMode)
                && reader.ReadBool(out value.isDailySeed)
                && reader.ReadBool(out value.runMarkedDead)
                && reader.ReadString(out value.dailySeedId);
        }

        private static bool WriteResourceScarcity(ref BufferWriter writer, ResourceScarcityDTO value)
        {
            return writer.WriteInt(value.entryCount)
                && WriteStringArray(ref writer, value.itemIds)
                && writer.WriteStructArray(value.collectedCounts);
        }

        private static bool ReadResourceScarcity(ref BufferReader reader, out ResourceScarcityDTO value)
        {
            value = default;
            return reader.ReadInt(out value.entryCount)
                && ReadStringArray(ref reader, out value.itemIds)
                && reader.ReadStructArray(out value.collectedCounts);
        }

        private static bool WriteEcosystemState(ref BufferWriter writer, EcosystemStateDTO value)
        {
            return writer.WriteInt(value.worldSeed)
                && writer.WriteInt(value.infectedZoneCount)
                && writer.WriteStructArray(value.infectedChunkKeys)
                && writer.WriteStructArray(value.infectedSeverities);
        }

        private static bool ReadEcosystemState(ref BufferReader reader, out EcosystemStateDTO value)
        {
            value = default;
            return reader.ReadInt(out value.worldSeed)
                && reader.ReadInt(out value.infectedZoneCount)
                && reader.ReadStructArray(out value.infectedChunkKeys)
                && reader.ReadStructArray(out value.infectedSeverities);
        }

        private static bool WriteInventoryCell(ref BufferWriter writer, in InventoryCellDTO value)
        {
            return writer.WriteInt(value.x)
                && writer.WriteInt(value.y)
                && writer.WriteString(value.itemId)
                && writer.WriteInt(value.stackCount);
        }

        private static bool ReadInventoryCell(ref BufferReader reader, out InventoryCellDTO value)
        {
            value = default;
            return reader.ReadInt(out value.x)
                && reader.ReadInt(out value.y)
                && reader.ReadString(out value.itemId)
                && reader.ReadInt(out value.stackCount);
        }

        private static bool WriteScanEntry(ref BufferWriter writer, in ScanEntryDTO value)
        {
            return writer.WriteString(value.id)
                && writer.WriteString(value.title)
                && writer.WriteString(value.category)
                && writer.WriteString(value.summary);
        }

        private static bool ReadScanEntry(ref BufferReader reader, out ScanEntryDTO value)
        {
            value = default;
            return reader.ReadString(out value.id)
                && reader.ReadString(out value.title)
                && reader.ReadString(out value.category)
                && reader.ReadString(out value.summary);
        }

        private static bool WriteBarterOfferState(ref BufferWriter writer, in BarterOfferStateDTO value)
        {
            return writer.WriteString(value.offerId)
                && writer.WriteInt(value.executionCount);
        }

        private static bool ReadBarterOfferState(ref BufferReader reader, out BarterOfferStateDTO value)
        {
            value = default;
            return reader.ReadString(out value.offerId)
                && reader.ReadInt(out value.executionCount);
        }

        private static bool WriteBarterTransaction(ref BufferWriter writer, in BarterTransactionDTO value)
        {
            return writer.WriteString(value.offerId)
                && writer.WriteString(value.offerName)
                && writer.WriteString(value.channelName)
                && writer.WriteString(value.costSummary)
                && writer.WriteString(value.rewardSummary);
        }

        private static bool ReadBarterTransaction(ref BufferReader reader, out BarterTransactionDTO value)
        {
            value = default;
            return reader.ReadString(out value.offerId)
                && reader.ReadString(out value.offerName)
                && reader.ReadString(out value.channelName)
                && reader.ReadString(out value.costSummary)
                && reader.ReadString(out value.rewardSummary);
        }

        private static bool WriteFieldOperationEntry(ref BufferWriter writer, in FieldOperationEntryDTO value)
        {
            return writer.WriteString(value.source)
                && writer.WriteString(value.title)
                && writer.WriteString(value.summary)
                && writer.WriteString(value.severity);
        }

        private static bool ReadFieldOperationEntry(ref BufferReader reader, out FieldOperationEntryDTO value)
        {
            value = default;
            return reader.ReadString(out value.source)
                && reader.ReadString(out value.title)
                && reader.ReadString(out value.summary)
                && reader.ReadString(out value.severity);
        }

        private static bool WriteBeaconEntry(ref BufferWriter writer, in BeaconEntryDTO value)
        {
            return writer.WriteString(value.id)
                && writer.WriteString(value.label)
                && writer.WriteFloat(value.posX)
                && writer.WriteFloat(value.posY)
                && writer.WriteFloat(value.posZ)
                && writer.WriteFloat(value.rotX)
                && writer.WriteFloat(value.rotY)
                && writer.WriteFloat(value.rotZ)
                && writer.WriteFloat(value.rotW)
                && writer.WriteFloat(value.colorR)
                && writer.WriteFloat(value.colorG)
                && writer.WriteFloat(value.colorB)
                && writer.WriteFloat(value.colorA)
                && writer.WriteFloat(value.lightRange);
        }

        private static bool ReadBeaconEntry(ref BufferReader reader, out BeaconEntryDTO value)
        {
            value = default;
            return reader.ReadString(out value.id)
                && reader.ReadString(out value.label)
                && reader.ReadFloat(out value.posX)
                && reader.ReadFloat(out value.posY)
                && reader.ReadFloat(out value.posZ)
                && reader.ReadFloat(out value.rotX)
                && reader.ReadFloat(out value.rotY)
                && reader.ReadFloat(out value.rotZ)
                && reader.ReadFloat(out value.rotW)
                && reader.ReadFloat(out value.colorR)
                && reader.ReadFloat(out value.colorG)
                && reader.ReadFloat(out value.colorB)
                && reader.ReadFloat(out value.colorA)
                && reader.ReadFloat(out value.lightRange);
        }

        private static bool WritePdaLogbookEntry(ref BufferWriter writer, in PDALogbookEntryDTO value)
        {
            return writer.WriteInt(value.sequence)
                && writer.WriteInt(value.dayIndex)
                && writer.WriteFloat(value.dayTimeHours)
                && writer.WriteFloat(value.playTimeSeconds)
                && writer.WriteString(value.title)
                && writer.WriteString(value.message)
                && writer.WriteString(value.originKey);
        }

        private static bool ReadPdaLogbookEntry(ref BufferReader reader, out PDALogbookEntryDTO value)
        {
            value = default;
            return reader.ReadInt(out value.sequence)
                && reader.ReadInt(out value.dayIndex)
                && reader.ReadFloat(out value.dayTimeHours)
                && reader.ReadFloat(out value.playTimeSeconds)
                && reader.ReadString(out value.title)
                && reader.ReadString(out value.message)
                && reader.ReadString(out value.originKey);
        }

        private static bool WritePdaMarkerEntry(ref BufferWriter writer, in PDAMarkerEntryDTO value)
        {
            return writer.WriteString(value.markerId)
                && writer.WriteString(value.title)
                && writer.WriteInt(value.iconType)
                && writer.WriteFloat(value.posX)
                && writer.WriteFloat(value.posY)
                && writer.WriteFloat(value.posZ)
                && writer.WriteBool(value.visibleOnHud);
        }

        private static bool ReadPdaMarkerEntry(ref BufferReader reader, out PDAMarkerEntryDTO value)
        {
            value = default;
            return reader.ReadString(out value.markerId)
                && reader.ReadString(out value.title)
                && reader.ReadInt(out value.iconType)
                && reader.ReadFloat(out value.posX)
                && reader.ReadFloat(out value.posY)
                && reader.ReadFloat(out value.posZ)
                && reader.ReadBool(out value.visibleOnHud);
        }

        private static bool WriteProceduralLorePlacement(ref BufferWriter writer, in ProceduralLorePlacementDTO value)
        {
            return writer.WriteString(value.discoveryId)
                && writer.WriteString(value.logId)
                && writer.WriteLong(value.chunkKey)
                && writer.WriteFloat(value.posX)
                && writer.WriteFloat(value.posY)
                && writer.WriteFloat(value.posZ);
        }

        private static bool ReadProceduralLorePlacement(ref BufferReader reader, out ProceduralLorePlacementDTO value)
        {
            value = default;
            return reader.ReadString(out value.discoveryId)
                && reader.ReadString(out value.logId)
                && reader.ReadLong(out value.chunkKey)
                && reader.ReadFloat(out value.posX)
                && reader.ReadFloat(out value.posY)
                && reader.ReadFloat(out value.posZ);
        }

        private static bool WriteModule(ref BufferWriter writer, in ModuleDTO value)
        {
            return writer.WriteString(value.prefabId)
                && writer.WriteString(value.slottedToolItemId)
                && writer.WriteString(value.pipeInFlightItemId)
                && writer.WriteInt(value.pipeInFlightAmount)
                && writer.WriteFloat(value.pipeTransitProgress)
                && writer.WriteFloat(value.pipeExportTimerSeconds)
                && writer.WriteString(value.drillBufferedItemId)
                && writer.WriteInt(value.drillBufferedAmount)
                && writer.WriteFloat(value.drillCycleTimerSeconds)
                && writer.WriteInt(value.sorterBufferedSlotCount)
                && WriteStringArray(ref writer, value.sorterBufferedItemIds)
                && writer.WriteStructArray(value.sorterBufferedQuantities)
                && writer.WriteFloat(value.posX)
                && writer.WriteFloat(value.posY)
                && writer.WriteFloat(value.posZ)
                && writer.WriteFloat(value.rotX)
                && writer.WriteFloat(value.rotY)
                && writer.WriteFloat(value.rotZ)
                && writer.WriteFloat(value.rotW)
                && writer.WriteFloat(value.integrity)
                && writer.WriteFloat(value.repairIntegrityCap)
                && writer.WriteFloat(value.airReserveNormalized)
                && writer.WriteFloat(value.co2Normalized)
                && writer.WriteBool(value.isFlooded)
                && writer.WriteByte(value.failureMode);
        }

        private static bool ReadModule(ref BufferReader reader, out ModuleDTO value)
        {
            value = default;
            return reader.ReadString(out value.prefabId)
                && reader.ReadString(out value.slottedToolItemId)
                && reader.ReadString(out value.pipeInFlightItemId)
                && reader.ReadInt(out value.pipeInFlightAmount)
                && reader.ReadFloat(out value.pipeTransitProgress)
                && reader.ReadFloat(out value.pipeExportTimerSeconds)
                && reader.ReadString(out value.drillBufferedItemId)
                && reader.ReadInt(out value.drillBufferedAmount)
                && reader.ReadFloat(out value.drillCycleTimerSeconds)
                && reader.ReadInt(out value.sorterBufferedSlotCount)
                && ReadStringArray(ref reader, out value.sorterBufferedItemIds)
                && reader.ReadStructArray(out value.sorterBufferedQuantities)
                && reader.ReadFloat(out value.posX)
                && reader.ReadFloat(out value.posY)
                && reader.ReadFloat(out value.posZ)
                && reader.ReadFloat(out value.rotX)
                && reader.ReadFloat(out value.rotY)
                && reader.ReadFloat(out value.rotZ)
                && reader.ReadFloat(out value.rotW)
                && reader.ReadFloat(out value.integrity)
                && reader.ReadFloat(out value.repairIntegrityCap)
                && reader.ReadFloat(out value.airReserveNormalized)
                && reader.ReadFloat(out value.co2Normalized)
                && reader.ReadBool(out value.isFlooded)
                && reader.ReadByte(out value.failureMode);
        }

        private static bool WriteInventoryCellArray(ref BufferWriter writer, InventoryCellDTO[] values)
        {
            return WriteCustomArray(ref writer, values, WriteInventoryCell);
        }

        private static bool ReadInventoryCellArray(ref BufferReader reader, out InventoryCellDTO[] values)
        {
            return ReadCustomArray(ref reader, out values, ReadInventoryCell);
        }

        private static bool WriteScanEntryArray(ref BufferWriter writer, ScanEntryDTO[] values)
        {
            return WriteCustomArray(ref writer, values, WriteScanEntry);
        }

        private static bool ReadScanEntryArray(ref BufferReader reader, out ScanEntryDTO[] values)
        {
            return ReadCustomArray(ref reader, out values, ReadScanEntry);
        }

        private static bool WriteBarterOfferStateArray(ref BufferWriter writer, BarterOfferStateDTO[] values)
        {
            return WriteCustomArray(ref writer, values, WriteBarterOfferState);
        }

        private static bool ReadBarterOfferStateArray(ref BufferReader reader, out BarterOfferStateDTO[] values)
        {
            return ReadCustomArray(ref reader, out values, ReadBarterOfferState);
        }

        private static bool WriteBarterTransactionArray(ref BufferWriter writer, BarterTransactionDTO[] values)
        {
            return WriteCustomArray(ref writer, values, WriteBarterTransaction);
        }

        private static bool ReadBarterTransactionArray(ref BufferReader reader, out BarterTransactionDTO[] values)
        {
            return ReadCustomArray(ref reader, out values, ReadBarterTransaction);
        }

        private static bool WriteFieldOperationEntryArray(ref BufferWriter writer, FieldOperationEntryDTO[] values)
        {
            return WriteCustomArray(ref writer, values, WriteFieldOperationEntry);
        }

        private static bool ReadFieldOperationEntryArray(ref BufferReader reader, out FieldOperationEntryDTO[] values)
        {
            return ReadCustomArray(ref reader, out values, ReadFieldOperationEntry);
        }

        private static bool WriteBeaconEntryArray(ref BufferWriter writer, BeaconEntryDTO[] values)
        {
            return WriteCustomArray(ref writer, values, WriteBeaconEntry);
        }

        private static bool ReadBeaconEntryArray(ref BufferReader reader, out BeaconEntryDTO[] values)
        {
            return ReadCustomArray(ref reader, out values, ReadBeaconEntry);
        }

        private static bool WritePdaLogbookEntryArray(ref BufferWriter writer, PDALogbookEntryDTO[] values)
        {
            return WriteCustomArray(ref writer, values, WritePdaLogbookEntry);
        }

        private static bool ReadPdaLogbookEntryArray(ref BufferReader reader, out PDALogbookEntryDTO[] values)
        {
            return ReadCustomArray(ref reader, out values, ReadPdaLogbookEntry);
        }

        private static bool WritePdaMarkerEntryArray(ref BufferWriter writer, PDAMarkerEntryDTO[] values)
        {
            return WriteCustomArray(ref writer, values, WritePdaMarkerEntry);
        }

        private static bool ReadPdaMarkerEntryArray(ref BufferReader reader, out PDAMarkerEntryDTO[] values)
        {
            return ReadCustomArray(ref reader, out values, ReadPdaMarkerEntry);
        }

        private static bool WriteProceduralLorePlacementArray(ref BufferWriter writer, ProceduralLorePlacementDTO[] values)
        {
            return WriteCustomArray(ref writer, values, WriteProceduralLorePlacement);
        }

        private static bool ReadProceduralLorePlacementArray(ref BufferReader reader, out ProceduralLorePlacementDTO[] values)
        {
            return ReadCustomArray(ref reader, out values, ReadProceduralLorePlacement);
        }

        private static bool WriteModuleArray(ref BufferWriter writer, ModuleDTO[] values)
        {
            return WriteCustomArray(ref writer, values, WriteModule);
        }

        private static bool ReadModuleArray(ref BufferReader reader, out ModuleDTO[] values)
        {
            return ReadCustomArray(ref reader, out values, ReadModule);
        }

        private static bool WriteStringArray(ref BufferWriter writer, string[] values)
        {
            int count = values != null ? values.Length : NullCollectionCount;
            if (!writer.WriteInt(count))
                return false;

            if (values == null)
                return true;

            for (int i = 0; i < values.Length; i++)
            {
                if (!writer.WriteString(values[i]))
                    return false;
            }

            return true;
        }

        private static bool ReadStringArray(ref BufferReader reader, out string[] values)
        {
            values = null;
            if (!reader.ReadInt(out int count))
                return false;

            if (count == NullCollectionCount)
                return true;

            if (count < 0)
            {
                reader.SetError("Collection length is negative.");
                return false;
            }

            values = new string[count];
            for (int i = 0; i < count; i++)
            {
                if (!reader.ReadString(out values[i]))
                    return false;
            }

            return true;
        }

        private static bool WriteStringList(ref BufferWriter writer, List<string> values)
        {
            int count = values != null ? values.Count : NullCollectionCount;
            if (!writer.WriteInt(count))
                return false;

            if (values == null)
                return true;

            for (int i = 0; i < values.Count; i++)
            {
                if (!writer.WriteString(values[i]))
                    return false;
            }

            return true;
        }

        private static bool ReadStringList(ref BufferReader reader, out List<string> values)
        {
            values = null;
            if (!reader.ReadInt(out int count))
                return false;

            if (count == NullCollectionCount)
                return true;

            if (count < 0)
            {
                reader.SetError("Collection length is negative.");
                return false;
            }

            values = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                if (!reader.ReadString(out string item))
                    return false;

                values.Add(item);
            }

            return true;
        }

        private static bool WriteFloatList(ref BufferWriter writer, List<float> values)
        {
            int count = values != null ? values.Count : NullCollectionCount;
            if (!writer.WriteInt(count))
                return false;

            if (values == null)
                return true;

            for (int i = 0; i < values.Count; i++)
            {
                if (!writer.WriteFloat(values[i]))
                    return false;
            }

            return true;
        }

        private static bool ReadFloatList(ref BufferReader reader, out List<float> values)
        {
            values = null;
            if (!reader.ReadInt(out int count))
                return false;

            if (count == NullCollectionCount)
                return true;

            if (count < 0)
            {
                reader.SetError("Collection length is negative.");
                return false;
            }

            values = new List<float>(count);
            for (int i = 0; i < count; i++)
            {
                if (!reader.ReadFloat(out float item))
                    return false;

                values.Add(item);
            }

            return true;
        }

        private static bool WriteStringFloatDictionary(ref BufferWriter writer, Dictionary<string, float> values)
        {
            int count = values != null ? values.Count : NullCollectionCount;
            if (!writer.WriteInt(count))
                return false;

            if (values == null)
                return true;

            Dictionary<string, float>.Enumerator enumerator = values.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<string, float> pair = enumerator.Current;
                if (!writer.WriteString(pair.Key) || !writer.WriteFloat(pair.Value))
                    return false;
            }

            enumerator.Dispose();
            return true;
        }

        private static bool ReadStringFloatDictionary(ref BufferReader reader, out Dictionary<string, float> values)
        {
            values = null;
            if (!reader.ReadInt(out int count))
                return false;

            if (count == NullCollectionCount)
                return true;

            if (count < 0)
            {
                reader.SetError("Collection length is negative.");
                return false;
            }

            values = new Dictionary<string, float>(count);
            for (int i = 0; i < count; i++)
            {
                if (!reader.ReadString(out string key) || !reader.ReadFloat(out float entryValue))
                    return false;

                values[key] = entryValue;
            }

            return true;
        }

        private static bool WriteStringBoolDictionary(ref BufferWriter writer, Dictionary<string, bool> values)
        {
            int count = values != null ? values.Count : NullCollectionCount;
            if (!writer.WriteInt(count))
                return false;

            if (values == null)
                return true;

            Dictionary<string, bool>.Enumerator enumerator = values.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<string, bool> pair = enumerator.Current;
                if (!writer.WriteString(pair.Key) || !writer.WriteBool(pair.Value))
                    return false;
            }

            enumerator.Dispose();
            return true;
        }

        private static bool ReadStringBoolDictionary(ref BufferReader reader, out Dictionary<string, bool> values)
        {
            values = null;
            if (!reader.ReadInt(out int count))
                return false;

            if (count == NullCollectionCount)
                return true;

            if (count < 0)
            {
                reader.SetError("Collection length is negative.");
                return false;
            }

            values = new Dictionary<string, bool>(count);
            for (int i = 0; i < count; i++)
            {
                if (!reader.ReadString(out string key) || !reader.ReadBool(out bool entryValue))
                    return false;

                values[key] = entryValue;
            }

            return true;
        }

        private static bool WriteStringStringDictionary(ref BufferWriter writer, Dictionary<string, string> values)
        {
            int count = values != null ? values.Count : NullCollectionCount;
            if (!writer.WriteInt(count))
                return false;

            if (values == null)
                return true;

            Dictionary<string, string>.Enumerator enumerator = values.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<string, string> pair = enumerator.Current;
                if (!writer.WriteString(pair.Key) || !writer.WriteString(pair.Value))
                    return false;
            }

            enumerator.Dispose();
            return true;
        }

        private static bool ReadStringStringDictionary(ref BufferReader reader, out Dictionary<string, string> values)
        {
            values = null;
            if (!reader.ReadInt(out int count))
                return false;

            if (count == NullCollectionCount)
                return true;

            if (count < 0)
            {
                reader.SetError("Collection length is negative.");
                return false;
            }

            values = new Dictionary<string, string>(count);
            for (int i = 0; i < count; i++)
            {
                if (!reader.ReadString(out string key) || !reader.ReadString(out string entryValue))
                    return false;

                values[key] = entryValue;
            }

            return true;
        }

        private static bool WriteIntHashSet(ref BufferWriter writer, HashSet<int> values)
        {
            int count = values != null ? values.Count : NullCollectionCount;
            if (!writer.WriteInt(count))
                return false;

            if (values == null)
                return true;

            HashSet<int>.Enumerator enumerator = values.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (!writer.WriteInt(enumerator.Current))
                    return false;
            }

            enumerator.Dispose();
            return true;
        }

        private static bool ReadIntHashSet(ref BufferReader reader, out HashSet<int> values)
        {
            values = null;
            if (!reader.ReadInt(out int count))
                return false;

            if (count == NullCollectionCount)
                return true;

            if (count < 0)
            {
                reader.SetError("Collection length is negative.");
                return false;
            }

            values = new HashSet<int>();
            for (int i = 0; i < count; i++)
            {
                if (!reader.ReadInt(out int entryValue))
                    return false;

                values.Add(entryValue);
            }

            return true;
        }

        private static bool WriteCustomArray<T>(ref BufferWriter writer, T[] values, WriteItemDelegate<T> writeItem)
        {
            int count = values != null ? values.Length : NullCollectionCount;
            if (!writer.WriteInt(count))
                return false;

            if (values == null)
                return true;

            for (int i = 0; i < values.Length; i++)
            {
                if (!writeItem(ref writer, values[i]))
                    return false;
            }

            return true;
        }

        private static bool ReadCustomArray<T>(ref BufferReader reader, out T[] values, ReadItemDelegate<T> readItem)
        {
            values = null;
            if (!reader.ReadInt(out int count))
                return false;

            if (count == NullCollectionCount)
                return true;

            if (count < 0)
            {
                reader.SetError("Collection length is negative.");
                return false;
            }

            values = new T[count];
            for (int i = 0; i < count; i++)
            {
                if (!readItem(ref reader, out values[i]))
                    return false;
            }

            return true;
        }

        private delegate bool WriteItemDelegate<T>(ref BufferWriter writer, in T value);
        private delegate bool ReadItemDelegate<T>(ref BufferReader reader, out T value);

        private struct BufferWriter
        {
            private readonly byte* _buffer;
            private readonly int _capacity;
            private int _cursor;

            public BufferWriter(byte* buffer, int capacity)
            {
                _buffer = buffer;
                _capacity = capacity;
                _cursor = 0;
                Error = string.Empty;
            }

            public int BytesWritten => _cursor;
            public string Error { get; private set; }

            public bool WriteStruct<T>(T value) where T : unmanaged
            {
                int size = UnsafeUtility.SizeOf<T>();
                if (!TryReserve(size))
                    return false;

                UnsafeUtility.CopyStructureToPtr(ref value, _buffer + _cursor);
                _cursor += size;
                return true;
            }

            public bool WriteStructArray<T>(T[] values) where T : unmanaged
            {
                int count = values != null ? values.Length : NullCollectionCount;
                if (!WriteInt(count))
                    return false;

                if (values == null || values.Length == 0)
                    return true;

                long byteCountLong = (long)values.Length * UnsafeUtility.SizeOf<T>();
                if (byteCountLong > int.MaxValue)
                {
                    Error = "Struct array byte count exceeds the supported range.";
                    return false;
                }

                int byteCount = (int)byteCountLong;
                if (!TryReserve(byteCount))
                    return false;

                fixed (T* sourcePtr = values)
                {
                    UnsafeUtility.MemCpy(_buffer + _cursor, sourcePtr, byteCount);
                }

                _cursor += byteCount;
                return true;
            }

            public bool WriteByte(byte value)
            {
                if (!TryReserve(sizeof(byte)))
                    return false;

                *(_buffer + _cursor) = value;
                _cursor += sizeof(byte);
                return true;
            }

            public bool WriteBool(bool value)
            {
                return WriteByte(value ? (byte)1 : (byte)0);
            }

            public bool WriteInt(int value)
            {
                return WriteStruct(value);
            }

            public bool WriteUInt(uint value)
            {
                return WriteStruct(value);
            }

            public bool WriteLong(long value)
            {
                return WriteStruct(value);
            }

            public bool WriteFloat(float value)
            {
                return WriteStruct(value);
            }

            public bool WriteDouble(double value)
            {
                return WriteStruct(value);
            }

            public bool WriteString(string value)
            {
                if (value == null)
                    return WriteInt(NullCollectionCount);

                int charCount = value.Length;
                if (!WriteInt(charCount))
                    return false;

                if (charCount == 0)
                    return true;

                long byteCountLong = (long)charCount * sizeof(char);
                if (byteCountLong > int.MaxValue)
                {
                    Error = "String payload exceeds the supported range.";
                    return false;
                }

                int byteCount = (int)byteCountLong;
                if (!TryReserve(byteCount))
                    return false;

                fixed (char* sourcePtr = value)
                {
                    UnsafeUtility.MemCpy(_buffer + _cursor, sourcePtr, byteCount);
                }

                _cursor += byteCount;
                return true;
            }

            private bool TryReserve(int byteCount)
            {
                if (byteCount < 0 || _cursor < 0 || _cursor > _capacity - byteCount)
                {
                    int attemptedLength = byteCount > 0 ? _cursor + byteCount : _cursor;
                    Error = $"Save payload exceeded the raw buffer ceiling at {attemptedLength} bytes.";
                    return false;
                }

                return true;
            }
        }

        private struct BufferReader
        {
            private readonly byte* _buffer;
            private readonly int _length;
            private int _cursor;

            public BufferReader(byte* buffer, int length)
            {
                _buffer = buffer;
                _length = length;
                _cursor = 0;
                Error = string.Empty;
            }

            public int BytesRead => _cursor;
            public string Error { get; private set; }

            public void SetError(string error)
            {
                if (string.IsNullOrEmpty(Error))
                    Error = error;
            }

            public bool ReadStruct<T>(out T value) where T : unmanaged
            {
                value = default;
                int size = UnsafeUtility.SizeOf<T>();
                if (!TryConsume(size))
                    return false;

                value = UnsafeUtility.ReadArrayElement<T>(_buffer + _cursor, 0);
                _cursor += size;
                return true;
            }

            public bool ReadStructArray<T>(out T[] values) where T : unmanaged
            {
                values = null;
                if (!ReadInt(out int count))
                    return false;

                if (count == NullCollectionCount)
                    return true;

                if (count < 0)
                {
                    SetError("Collection length is negative.");
                    return false;
                }

                if (count == 0)
                {
                    values = Array.Empty<T>();
                    return true;
                }

                long byteCountLong = (long)count * UnsafeUtility.SizeOf<T>();
                if (byteCountLong > int.MaxValue)
                {
                    SetError("Struct array byte count exceeds the supported range.");
                    return false;
                }

                int byteCount = (int)byteCountLong;
                if (!TryConsume(byteCount))
                    return false;

                values = new T[count];
                fixed (T* destinationPtr = values)
                {
                    UnsafeUtility.MemCpy(destinationPtr, _buffer + _cursor, byteCount);
                }

                _cursor += byteCount;
                return true;
            }

            public bool ReadByte(out byte value)
            {
                value = 0;
                if (!TryConsume(sizeof(byte)))
                    return false;

                value = *(_buffer + _cursor);
                _cursor += sizeof(byte);
                return true;
            }

            public bool ReadBool(out bool value)
            {
                value = false;
                if (!ReadByte(out byte byteValue))
                    return false;

                value = byteValue != 0;
                return true;
            }

            public bool ReadInt(out int value)
            {
                return ReadStruct(out value);
            }

            public bool ReadUInt(out uint value)
            {
                return ReadStruct(out value);
            }

            public bool ReadLong(out long value)
            {
                return ReadStruct(out value);
            }

            public bool ReadFloat(out float value)
            {
                return ReadStruct(out value);
            }

            public bool ReadDouble(out double value)
            {
                return ReadStruct(out value);
            }

            public bool ReadString(out string value)
            {
                value = string.Empty;
                if (!ReadInt(out int charCount))
                    return false;

                if (charCount == NullCollectionCount)
                {
                    value = null;
                    return true;
                }

                if (charCount < 0)
                {
                    SetError("String length is negative.");
                    return false;
                }

                if (charCount == 0)
                    return true;

                long byteCountLong = (long)charCount * sizeof(char);
                if (byteCountLong > int.MaxValue)
                {
                    SetError("String payload exceeds the supported range.");
                    return false;
                }

                int byteCount = (int)byteCountLong;
                if (!TryConsume(byteCount))
                    return false;

                value = new string((char*)(_buffer + _cursor), 0, charCount);
                _cursor += byteCount;
                return true;
            }

            private bool TryConsume(int byteCount)
            {
                if (byteCount < 0 || _cursor < 0 || _cursor > _length - byteCount)
                {
                    SetError("Save payload read exceeded the available byte range.");
                    return false;
                }

                return true;
            }
        }
    }
}

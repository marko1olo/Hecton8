using System;
using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Narrative;
using Hecton8.World;
using Hecton.Localization;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.SaveSystem
{
    internal static unsafe class SaveBinaryPayloadCodec
    {
        internal const int ProtectedLz4BlockSizeBytes = 16 * 1024;
        private const ushort BiologicalItemStateMask = 1 << 6;
        private const ushort DefaultQualityMilli = SaveData.InventoryDefaultQualityMilli;
        private const byte ItemGeneticsGlowFlag = 1 << 0;
        private const byte ItemGeneticsToxicFlag = 1 << 1;
        private const byte ItemGeneticsEdibleFlag = 1 << 2;
        private const byte ItemGeneticsHarvestableFlag = 1 << 3;
        private const ulong LegacyGlowGeneMask = 1UL << 0;
        private const ulong LegacyOxygenGeneMask = 1UL << 1;
        private const ulong LegacyToxicGeneMask = 1UL << 2;
        private const ulong LegacyFastGrowingGeneMask = 1UL << 3;
        private const ulong LegacyEdibleGeneMask = 1UL << 4;
        private const ulong LegacyAquaticGeneMask = 1UL << 6;
        private const ulong LegacyHarvestableGeneMask =
            LegacyOxygenGeneMask |
            LegacyFastGrowingGeneMask |
            LegacyAquaticGeneMask;
        private const float BiologicalReferenceTemperatureCelsius = 4f;
        private const float BiologicalDecayRatePerSecond = 0.001f;
        private const int NullCollectionCount = -1;
        private const int FirstHourKnownMilestoneMask = (1 << 6) - 1;
        private const int FirstHourKnownGuidanceMask = (1 << 11) - 1;
        private const int SuitUpgradeMaskSaveVersion = 65;
        private const int RadiationGridSaveVersion = SaveData.RadiationGridPersistenceVersion;
        private const int RtgDecaySaveVersion = 70;
        private const int MetaCampaignSaveVersion = 71;
        private const int FirstHourDtoLockSaveVersion = SaveData.FirstHourDtoLockPersistenceVersion;
        private const int ContractAuthoritySaveVersion = 73;
        private const int PreV73ReadRepairVersion = 73;
        private const int HazardZoneRuntimeSaveVersion = SaveData.HazardZoneRuntimePersistenceVersion;
        private const int VoxelDeltaSaveVersion = SaveData.VoxelDeltaPersistenceVersion;
        private const int VoxelDeltaDenseCellFlagsSaveVersion = SaveData.VoxelDeltaDenseCellFlagsPersistenceVersion;
        private const int ResourceRecyclerModuleSaveVersion = SaveData.ResourceRecyclerModulePersistenceVersion;
        private const int StorageCrateModuleSaveVersion = SaveData.StorageCrateModulePersistenceVersion;
        private const int FabricatorPendingOutputSaveVersion = SaveData.FabricatorPendingOutputPersistenceVersion;
        private const int CultivationSeedHashSaveVersion = SaveData.CultivationSeedHashPersistenceVersion;
        private const int ProceduralTerrainIdentitySaveVersion = SaveData.ProceduralTerrainIdentityPersistenceVersion;
        private const int CelestialLightPhaseSaveVersion = SaveData.CelestialLightPhasePersistenceVersion;
        private const int ProceduralTerrainIdentityContractSaveVersion =
            SaveData.ProceduralTerrainIdentityContractPersistenceVersion;
        private const int MaxVoxelDeltaChunks = 65536;
        private const int MaxVoxelDeltaCarvingOperations = 65536;
        private const float VoxelDeltaDefaultVoxelSize = 0.25f;
        private const byte VoxelDeltaSerializedStorageDense = 0;
        private const byte VoxelDeltaSerializedStorageUniformSdfRle = 1;
        private const byte VoxelDeltaSerializedStorageLegacyCells = 2;
        private const float RadiationGridDefaultCellSizeMeters = SaveData.RadiationGridDefaultCellSizeMeters;
        private const float RadiationGridMinCellSizeMeters = SaveData.RadiationGridMinCellSizeMeters;
        private const float RadiationGridMaxCellSizeMeters = SaveData.RadiationGridMaxCellSizeMeters;
        private const int ProceduralFaunaStateStrideBytes = 16;
        private const int HibernatedFaunaStateStrideBytes = 112;
        private const int ModuleSorterBufferSlotMax = 8;
        private const int ModuleRecyclerBufferSlotMax = 8;
        private const int ModuleRecyclerPendingYieldSlotMax = 16;
        private const int ModuleCultivationSlotMax = 4;
        private const int ModuleStorageCrateSlotMax = 32;
        private const int SerializedStringHeaderBytes = sizeof(int);
        private const int SerializedIntBytes = sizeof(int);
        private const int SerializedLongBytes = sizeof(long);
        private const int SerializedFloatBytes = sizeof(float);
        private const int SerializedBoolBytes = sizeof(byte);
        private const int MaxSerializedStringChars = ProtectedLz4BlockSizeBytes / 2;
        private const uint WfcOutpostPayloadMagic = 0x57464342u; // WFCB
        private const ushort WfcOutpostPayloadVersion = 1;
        private const byte WfcOutpostPayloadFlagRle = 1 << 0;
        private const byte WfcOutpostPayloadFlagChecksum24 = 1 << 1;
        private const uint WfcOutpostPayloadFlagMask = 0xFFu;
        private const uint WfcOutpostPayloadSupportedFlags = WfcOutpostPayloadFlagRle | WfcOutpostPayloadFlagChecksum24;
        private const int WfcOutpostPayloadChecksumShift = 8;
        private const uint WfcOutpostPayloadChecksumMask = 0x00FFFFFFu;
        private const uint WfcOutpostPayloadChecksumSeed = 2166136261u;

        private enum VoxelDeltaCellFlagsReadMode
        {
            CurrentRequired,
            PreV77AutoPreferLegacy,
            PreV77Absent,
            PreV77LegacyPresent
        }

        internal static ulong BuildSectorEntitySpatialSortKey(in AbsoluteUniversePosition position, int chunkSizeMeters)
        {
            int safeChunkSize = math.max(1, chunkSizeMeters);
            double chunkSize = safeChunkSize;
            int3 chunkId = AbsoluteUniversePosition.ResolveChunkId(in position, safeChunkSize);
            double3 absolute = position.ToAbsoluteDouble3();
            double chunkOriginX = chunkId.x * chunkSize;
            double chunkOriginY = chunkId.y * chunkSize;
            double chunkOriginZ = chunkId.z * chunkSize;

            ushort chunkKey = FoldSpatialChunkId(chunkId);
            ushort quantizedY = QuantizeSpatialAxis(absolute.y - chunkOriginY, chunkSize);
            ushort quantizedX = QuantizeSpatialAxis(absolute.x - chunkOriginX, chunkSize);
            ushort quantizedZ = QuantizeSpatialAxis(absolute.z - chunkOriginZ, chunkSize);
            return ((ulong)chunkKey << 48) |
                   ((ulong)quantizedY << 32) |
                   ((ulong)quantizedX << 16) |
                   quantizedZ;
        }

        private static ushort FoldSpatialChunkId(int3 chunkId)
        {
            uint hash = unchecked((uint)((chunkId.x * 73856093) ^ (chunkId.y * 19349663) ^ (chunkId.z * 83492791)));
            hash ^= hash >> 16;
            return (ushort)hash;
        }

        private static ushort QuantizeSpatialAxis(double localMeters, double chunkSizeMeters)
        {
            if (chunkSizeMeters <= 0d)
                return 0;

            double normalized = math.clamp(localMeters / chunkSizeMeters, 0d, 1d);
            return (ushort)math.clamp((int)math.round(normalized * ushort.MaxValue), 0, ushort.MaxValue);
        }

        internal static bool TryWriteWfcOutpostBitmaskPayload(
            NativeArray<ulong> packedWords,
            int wordCount,
            byte* destination,
            int capacity,
            out int bytesWritten)
        {
            bytesWritten = 0;
            if (!packedWords.IsCreated ||
                wordCount != WfcOutpostPersistenceConstants.PackedWordCount ||
                packedWords.Length < wordCount ||
                destination == null ||
                capacity < WfcOutpostPersistenceConstants.PayloadMaxBytes)
            {
                return false;
            }

            int rawBytes = wordCount * sizeof(ulong);
            byte* rawPtr = (byte*)packedWords.GetUnsafeReadOnlyPtr();
            byte* payloadPtr = destination + WfcOutpostPersistenceConstants.PayloadHeaderBytes;
            int payloadCapacity = capacity - WfcOutpostPersistenceConstants.PayloadHeaderBytes;
            uint payloadFlags = 0u;
            int storedBytes;

            if (TryWriteByteRle(rawPtr, rawBytes, payloadPtr, payloadCapacity, out int rleBytes))
            {
                storedBytes = rleBytes;
                payloadFlags |= WfcOutpostPayloadFlagRle;
            }
            else
            {
                UnsafeUtility.MemCpy(payloadPtr, rawPtr, rawBytes);
                storedBytes = rawBytes;
            }

            payloadFlags |= WfcOutpostPayloadFlagChecksum24;
            uint checksum24 = ComputeWfcOutpostPayloadChecksum24(payloadPtr, storedBytes, rawBytes, wordCount, payloadFlags);
            uint headerFlags = payloadFlags | (checksum24 << WfcOutpostPayloadChecksumShift);

            UnsafeUtility.MemClear(destination, WfcOutpostPersistenceConstants.PayloadHeaderBytes);
            WriteUInt(destination, 0, WfcOutpostPayloadMagic);
            WriteUShort(destination, 4, WfcOutpostPayloadVersion);
            WriteUShort(destination, 6, WfcOutpostPersistenceConstants.PayloadHeaderBytes);
            WriteUShort(destination, 8, WfcOutpostPersistenceConstants.GridSizeX);
            WriteUShort(destination, 10, WfcOutpostPersistenceConstants.GridSizeY);
            WriteUShort(destination, 12, WfcOutpostPersistenceConstants.GridSizeZ);
            WriteUShort(destination, 14, WfcOutpostPersistenceConstants.MutableBitPlaneCount);
            WriteInt(destination, 16, wordCount);
            WriteInt(destination, 20, rawBytes);
            WriteInt(destination, 24, storedBytes);
            WriteUInt(destination, 28, headerFlags);
            bytesWritten = WfcOutpostPersistenceConstants.PayloadHeaderBytes + storedBytes;
            return true;
        }

        internal static bool HasWfcOutpostBitmaskMagic(byte* source, int length)
        {
            return source != null &&
                   length >= sizeof(uint) &&
                   source[0] == unchecked((byte)WfcOutpostPayloadMagic) &&
                   source[1] == unchecked((byte)(WfcOutpostPayloadMagic >> 8)) &&
                   source[2] == unchecked((byte)(WfcOutpostPayloadMagic >> 16)) &&
                   source[3] == unchecked((byte)(WfcOutpostPayloadMagic >> 24));
        }

        internal static bool TryReadWfcOutpostBitmaskPayload(
            byte* source,
            int length,
            NativeArray<ulong> packedWords,
            int expectedWordCount,
            out int wordsRead)
        {
            wordsRead = 0;
            if (source == null ||
                length < WfcOutpostPersistenceConstants.PayloadHeaderBytes ||
                !packedWords.IsCreated ||
                expectedWordCount != WfcOutpostPersistenceConstants.PackedWordCount ||
                packedWords.Length < expectedWordCount)
            {
                return false;
            }

            if (ReadUInt(source, 0) != WfcOutpostPayloadMagic ||
                ReadUShort(source, 4) != WfcOutpostPayloadVersion ||
                ReadUShort(source, 6) != WfcOutpostPersistenceConstants.PayloadHeaderBytes ||
                ReadUShort(source, 8) != WfcOutpostPersistenceConstants.GridSizeX ||
                ReadUShort(source, 10) != WfcOutpostPersistenceConstants.GridSizeY ||
                ReadUShort(source, 12) != WfcOutpostPersistenceConstants.GridSizeZ ||
                ReadUShort(source, 14) != WfcOutpostPersistenceConstants.MutableBitPlaneCount)
            {
                return false;
            }

            int wordCount = ReadInt(source, 16);
            int rawBytes = ReadInt(source, 20);
            int storedBytes = ReadInt(source, 24);
            uint headerFlags = ReadUInt(source, 28);
            uint payloadFlags = headerFlags & WfcOutpostPayloadFlagMask;
            uint storedChecksum24 = (headerFlags >> WfcOutpostPayloadChecksumShift) & WfcOutpostPayloadChecksumMask;
            bool isRle = (payloadFlags & WfcOutpostPayloadFlagRle) != 0u;
            bool hasChecksum = (payloadFlags & WfcOutpostPayloadFlagChecksum24) != 0u;
            if (wordCount != expectedWordCount ||
                rawBytes != expectedWordCount * sizeof(ulong) ||
                storedBytes <= 0 ||
                storedBytes > rawBytes ||
                (payloadFlags & ~WfcOutpostPayloadSupportedFlags) != 0u ||
                (!hasChecksum && storedChecksum24 != 0u) ||
                (!isRle && storedBytes != rawBytes) ||
                length != WfcOutpostPersistenceConstants.PayloadHeaderBytes + storedBytes)
            {
                return false;
            }

            byte* payloadPtr = source + WfcOutpostPersistenceConstants.PayloadHeaderBytes;
            if (hasChecksum)
            {
                uint computedChecksum24 = ComputeWfcOutpostPayloadChecksum24(
                    payloadPtr,
                    storedBytes,
                    rawBytes,
                    wordCount,
                    payloadFlags);
                if (storedChecksum24 == 0u || storedChecksum24 != computedChecksum24)
                    return false;
            }

            byte* destination = (byte*)packedWords.GetUnsafePtr();
            if (isRle)
            {
                if (!TryReadByteRle(payloadPtr, storedBytes, destination, rawBytes))
                    return false;
            }
            else
            {
                UnsafeUtility.MemCpy(destination, payloadPtr, rawBytes);
            }

            wordsRead = wordCount;
            return true;
        }

        private static uint ComputeWfcOutpostPayloadChecksum24(
            byte* payload,
            int storedBytes,
            int rawBytes,
            int wordCount,
            uint payloadFlags)
        {
            uint hash = WfcOutpostPayloadChecksumSeed;
            hash = MixChecksum(hash, unchecked((uint)storedBytes));
            hash = MixChecksum(hash, unchecked((uint)rawBytes));
            hash = MixChecksum(hash, unchecked((uint)wordCount));
            hash = MixChecksum(hash, payloadFlags & WfcOutpostPayloadFlagMask);
            for (int i = 0; i < storedBytes; i++)
                hash = MixChecksumByte(hash, payload[i]);

            hash ^= hash >> 16;
            uint checksum = hash & WfcOutpostPayloadChecksumMask;
            return checksum != 0u ? checksum : 1u;
        }

        private static uint MixChecksum(uint hash, uint value)
        {
            hash ^= value & 0xFFu;
            hash *= 16777619u;
            hash ^= (value >> 8) & 0xFFu;
            hash *= 16777619u;
            hash ^= (value >> 16) & 0xFFu;
            hash *= 16777619u;
            hash ^= (value >> 24) & 0xFFu;
            hash *= 16777619u;
            return hash;
        }

        private static uint MixChecksumByte(uint hash, byte value)
        {
            hash ^= value;
            hash *= 16777619u;
            return hash;
        }

        private static bool TryWriteByteRle(byte* input, int inputBytes, byte* output, int outputCapacity, out int outputBytes)
        {
            outputBytes = 0;
            int read = 0;
            while (read < inputBytes)
            {
                byte value = input[read];
                int run = 1;
                while (read + run < inputBytes && run < ushort.MaxValue && input[read + run] == value)
                    run++;

                if (outputBytes + 3 > outputCapacity)
                    return false;

                output[outputBytes++] = value;
                ushort run16 = (ushort)run;
                output[outputBytes++] = unchecked((byte)run16);
                output[outputBytes++] = unchecked((byte)(run16 >> 8));
                read += run;
            }

            return outputBytes > 0 && outputBytes < inputBytes;
        }

        private static bool TryReadByteRle(byte* input, int inputBytes, byte* output, int expectedOutputBytes)
        {
            int read = 0;
            int write = 0;
            while (read + 2 < inputBytes)
            {
                byte value = input[read++];
                int run = input[read++] | (input[read++] << 8);
                if (run <= 0 || write + run > expectedOutputBytes)
                    return false;

                UnsafeUtility.MemSet(output + write, value, run);
                write += run;
            }

            return read == inputBytes && write == expectedOutputBytes;
        }

        private static void WriteUInt(byte* ptr, int offset, uint value)
        {
            ptr[offset] = unchecked((byte)value);
            ptr[offset + 1] = unchecked((byte)(value >> 8));
            ptr[offset + 2] = unchecked((byte)(value >> 16));
            ptr[offset + 3] = unchecked((byte)(value >> 24));
        }

        private static void WriteUShort(byte* ptr, int offset, int value)
        {
            uint value16 = unchecked((ushort)value);
            ptr[offset] = unchecked((byte)value16);
            ptr[offset + 1] = unchecked((byte)(value16 >> 8));
        }

        private static void WriteInt(byte* ptr, int offset, int value)
        {
            WriteUInt(ptr, offset, unchecked((uint)value));
        }

        private static uint ReadUInt(byte* ptr, int offset)
        {
            return (uint)ptr[offset] |
                   ((uint)ptr[offset + 1] << 8) |
                   ((uint)ptr[offset + 2] << 16) |
                   ((uint)ptr[offset + 3] << 24);
        }

        private static ushort ReadUShort(byte* ptr, int offset)
        {
            return unchecked((ushort)(ptr[offset] | (ptr[offset + 1] << 8)));
        }

        private static int ReadInt(byte* ptr, int offset)
        {
            return unchecked((int)ReadUInt(ptr, offset));
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

            if (data.version > SaveData.CurrentVersion)
            {
                error = $"Unsupported save data version {data.version}.";
                return false;
            }

            if (data.version != SaveData.CurrentVersion)
            {
                error = $"Save data version {data.version} must be migrated before writing.";
                return false;
            }

            data.contractVersionHashLo = HectonContractVersion.HashLo;
            data.contractVersionHashHi = HectonContractVersion.HashHi;

            BufferWriter writer = new BufferWriter(destination, capacity);
            if (!WriteSaveData(data, ref writer))
            {
                error = writer.Error;
                return false;
            }

            bytesWritten = writer.GetBytesWritten();
            return true;
        }

        internal static bool TryRead(byte* source, int length, out SaveData data, out int bytesRead, out string error)
        {
            if (TryRead(
                    source,
                    length,
                    VoxelDeltaCellFlagsReadMode.PreV77AutoPreferLegacy,
                    out data,
                    out bytesRead,
                    out error))
            {
                return true;
            }

            if (length >= sizeof(int) &&
                source != null &&
                UnsafeUtility.ReadArrayElement<int>(source, 0) == VoxelDeltaSaveVersion &&
                error.StartsWith("Save payload has ", StringComparison.Ordinal) &&
                error.EndsWith(" trailing unread bytes.", StringComparison.Ordinal))
            {
                return TryRead(
                    source,
                    length,
                    VoxelDeltaCellFlagsReadMode.PreV77Absent,
                    out data,
                    out bytesRead,
                    out error);
            }

            return false;
        }

        private static bool TryRead(
            byte* source,
            int length,
            VoxelDeltaCellFlagsReadMode preV77CellFlagsReadMode,
            out SaveData data,
            out int bytesRead,
            out string error)
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
            if (!ReadSaveData(ref reader, data, preV77CellFlagsReadMode))
            {
                error = reader.Error;
                data = null;
                return false;
            }

            bytesRead = reader.GetBytesRead();
            if (bytesRead != length)
            {
                error = $"Save payload has {length - bytesRead} trailing unread bytes.";
                data = null;
                return false;
            }

            return true;
        }

        private static bool WriteSaveData(SaveData data, ref BufferWriter writer)
        {
            if (data == null)
                return false;

            int narrativeDiscoverySourceCount = ClampCollectionCount(
                data.narrativeDiscoveryCount,
                data.narrativeDiscoveryIds,
                SaveData.MaxNarrativeDiscoveries);
            int narrativeDiscoveryCount = CountNonBlankStringArraySlice(
                data.narrativeDiscoveryIds,
                narrativeDiscoverySourceCount,
                SaveData.MaxNarrativeDiscoveries);
            int corporatePendingOrderSourceCount = ClampPairedListCount(
                data.corporatePendingOrderIds,
                data.corporatePendingOrderTimers,
                SaveData.MaxCorporateOrderIds);
            float firstHourSessionTime = SanitizeNonNegativeFinite(data.firstHourSessionTime);
            int endingChoice = SanitizeEndingChoice(data.endingChoice);
            bool endingComplete = data.endingComplete && endingChoice != 0;
            if (!endingComplete)
                endingChoice = 0;
            bool endingConditionMet = data.endingConditionMet || endingComplete;
            ulong suitUpgradeMask = SanitizeSuitUpgradeMask(data.suitUpgradeMask);

            return writer.WriteInt(data.version)
                && writer.WriteStruct(data.contractVersionHashLo)
                && writer.WriteStruct(data.contractVersionHashHi)
                && writer.WriteString(data.timestamp ?? string.Empty)
                && WriteSaveDataState(data, ref writer)
                && WriteSaveDataProgress(data, ref writer, narrativeDiscoveryCount, narrativeDiscoverySourceCount)
                && WriteSaveDataPlayer(data, ref writer, corporatePendingOrderSourceCount, firstHourSessionTime, endingChoice, endingComplete, endingConditionMet, suitUpgradeMask)
                && WriteSaveDataWorld(data, ref writer);
        }

        private static bool WriteSaveDataState(SaveData data, ref BufferWriter writer)
        {
            double totalPlayTime = SanitizeNonNegativeFinite(data.totalPlayTime);
            return writer.WriteDouble(totalPlayTime)
                && WritePlayerStats(ref writer, data.playerStats)
                && WriteInventory(ref writer, data)
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
                && WritePdaAdvisories(ref writer, data.pdaAdvisories)
                && WriteProceduralLore(ref writer, data.proceduralLore)
                && WriteAchievementRegistry(ref writer, data.achievements)
                && WriteRunModifiers(ref writer, data.runModifiers)
                && WriteMetaCampaign(ref writer, data.metaCampaign)
                && WriteResourceScarcity(ref writer, data.resourceScarcity)
                && WriteEnvironmentalStrain(ref writer, data.environmentalStrain)
                && WriteEcosystemState(ref writer, data.ecosystemState)
                && WriteExternalScavengerSites(ref writer, data.externalScavengerSites)
                && WriteHazardZoneRuntime(ref writer, data.hazardZones);
        }

        private static bool WriteSaveDataProgress(
            SaveData data,
            ref BufferWriter writer,
            int narrativeDiscoveryCount,
            int narrativeDiscoverySourceCount)
        {
            int lastDiscoveredBiomeId = NormalizeLastDiscoveredBiomeId(
                data.lastDiscoveredBiomeId,
                data.discoveredBiomeIds,
                data.discoveredBiomeBitWords);

            return WriteStringFloatDictionary(ref writer, data.toolDurabilityMap, SaveData.MaxToolDurabilityRecords)
                && WriteStringBoolDictionary(ref writer, data.toolBrokenMap, SaveData.MaxToolDurabilityRecords)
                && WriteDiscoveredBiomeHashSet(ref writer, data.discoveredBiomeIds)
                && WriteDiscoveredBiomeBitWords(ref writer, data.discoveredBiomeBitWords)
                && writer.WriteInt(lastDiscoveredBiomeId)
                && writer.WriteInt(narrativeDiscoveryCount)
                && WriteNonBlankStringArraySlice(
                    ref writer,
                    data.narrativeDiscoveryIds,
                    narrativeDiscoverySourceCount,
                    SaveData.MaxNarrativeDiscoveries)
                && writer.WriteInt(Math.Max(0, data.narrativeDepthTier))
                && writer.WriteStruct(data.narrativeAupTriggeredMask)
                && WriteNonBlankStringList(ref writer, data.audioLogDiscoveredIds, SaveData.MaxLegacyAudioLogDiscoveredIds)
                && WriteAudioLogDiscoveryBitWords(ref writer, data)
                && WriteEncryptedAudioLogFragments(ref writer, data)
                && WriteIndustrialLoreUnlockWords(ref writer, data.industrialLoreUnlockWords)
                && WriteDataArchaeology(ref writer, data)
                && WriteNonBlankStringList(ref writer, data.questActiveIds, SaveData.MaxLegacyQuestIds)
                && WriteNonBlankStringList(ref writer, data.questCompletedIds, SaveData.MaxLegacyQuestIds)
                && writer.WriteBool(data.atlasSignalDetected)
                && writer.WriteFloat(SanitizeNonNegativeFinite(data.atlasSignalPulseTimer))
                && writer.WriteInt(math.clamp(data.atlasSignalRevealStage, 0, 4));
        }

        private static bool WriteSaveDataPlayer(
            SaveData data,
            ref BufferWriter writer,
            int corporatePendingOrderSourceCount,
            float firstHourSessionTime,
            int endingChoice,
            bool endingComplete,
            bool endingConditionMet,
            ulong suitUpgradeMask)
        {
            return writer.WriteStruct(suitUpgradeMask)
                && WriteNonBlankStringList(ref writer, data.suitInstalledUpgradeIds, SaveData.MaxSuitUpgradeIds)
                && WriteNonBlankStringList(ref writer, data.suitUnlockedBlueprintIds, SaveData.MaxSuitUpgradeIds)
                && WriteNonBlankStringList(ref writer, data.suitBrokenUpgradeIds, SaveData.MaxSuitUpgradeIds)
                && writer.WriteString(SaveData.SanitizePersistenceString(data.playerExpressionProfileId))
                && writer.WriteInt(SanitizeAtlas6PlayerStatus(data.atlas6PlayerStatus))
                && writer.WriteInt(Math.Max(0, data.atlas6BarterCount))
                && writer.WriteBool(data.atlas6DirectiveConflictTriggered)
                && WriteAtlas6Liability(ref writer, data)
                && WriteNonBlankStringList(ref writer, data.corporateReceivedOrderIds, SaveData.MaxCorporateOrderIds)
                && WritePairedNonBlankStringList(
                    ref writer,
                    data.corporatePendingOrderIds,
                    data.corporatePendingOrderTimers,
                    corporatePendingOrderSourceCount)
                && WritePairedNonBlankFloatList(
                    ref writer,
                    data.corporatePendingOrderIds,
                    data.corporatePendingOrderTimers,
                    corporatePendingOrderSourceCount)
                && writer.WriteFloat(firstHourSessionTime)
                && writer.WriteInt(SanitizeFirstHourMilestones(data.firstHourMilestones))
                && writer.WriteInt(SanitizeFirstHourGuidanceFlags(data.firstHourGuidanceFlags))
                && writer.WriteInt(endingChoice)
                && writer.WriteBool(endingComplete)
                && writer.WriteBool(endingConditionMet)
                && WriteNonBlankStringList(ref writer, data.missionActiveIds, SaveData.MaxMissionIds)
                && WriteNonBlankStringList(ref writer, data.missionCompletedIds, SaveData.MaxMissionIds)
                && writer.WriteInt(SanitizeLodQualityPreset(data.LODQualityPreset))
                && writer.WriteBool(data.DynamicResolutionEnabled);
        }

        private static bool WriteSaveDataWorld(SaveData data, ref BufferWriter writer)
        {
            return WriteRadiationGrid(ref writer, data)
                && WriteRtgDecay(ref writer, data)
                && WriteStringStringDictionary(ref writer, data.CustomModData, SaveData.MaxCustomModDataEntries)
                && WriteFirstHourLockedDtos(ref writer, data)
                && WriteVoxelDeltaPersistence(ref writer, data.voxelDeltaPersistence)
                && WriteCelestialLightPhase(ref writer, data)
                && WriteProceduralTerrainIdentity(ref writer, data.proceduralTerrainIdentity);
        }

        private static bool ReadSaveData(
            ref BufferReader reader,
            SaveData data,
            VoxelDeltaCellFlagsReadMode preV77CellFlagsReadMode)
        {
            if (!reader.ReadInt(out data.version))
                return false;

            if (data.version > SaveData.CurrentVersion)
            {
                reader.SetError($"Unsupported save data version {data.version}.");
                return false;
            }

            if (data.version >= ContractAuthoritySaveVersion)
            {
                if (!reader.ReadStruct(out data.contractVersionHashLo) ||
                    !reader.ReadStruct(out data.contractVersionHashHi))
                {
                    return false;
                }
            }
            else
            {
                data.contractVersionHashLo = HectonContractVersion.HashLo;
                data.contractVersionHashHi = HectonContractVersion.HashHi;
            }

            if (!reader.ReadString(out data.timestamp)
                || !ReadSaveDataState(ref reader, data)
                || !ReadSaveDataProgress(ref reader, data)
                || !ReadSaveDataPlayer(ref reader, data)
                || !ReadSaveDataWorld(ref reader, data, preV77CellFlagsReadMode))
            {
                return false;
            }

            data.totalPlayTime = SanitizeNonNegativeFinite(data.totalPlayTime);
            data.atlasSignalPulseTimer = SanitizeNonNegativeFinite(data.atlasSignalPulseTimer);
            data.atlas6PlayerStatus = SanitizeAtlas6PlayerStatus(data.atlas6PlayerStatus);
            data.atlas6BarterCount = Math.Max(0, data.atlas6BarterCount);
            data.firstHourSessionTime = SanitizeNonNegativeFinite(data.firstHourSessionTime);
            data.firstHourMilestones = SanitizeFirstHourMilestones(data.firstHourMilestones);
            data.firstHourGuidanceFlags = SanitizeFirstHourGuidanceFlags(data.firstHourGuidanceFlags);
            data.endingChoice = SanitizeEndingChoice(data.endingChoice);
            data.endingComplete = data.endingComplete && data.endingChoice != 0;
            if (!data.endingComplete)
                data.endingChoice = 0;
            data.endingConditionMet = data.endingConditionMet || data.endingComplete;
            data.atlasSignalRevealStage = SanitizeAtlasSignalRevealStageAfterRead(data);
            data.suitUpgradeMask = SanitizeSuitUpgradeMask(data.suitUpgradeMask);
            data.narrativeDepthTier = Math.Max(0, data.narrativeDepthTier);
            data.LODQualityPreset = SanitizeLodQualityPreset(data.LODQualityPreset);
            SanitizeCelestialLightPhase(data);
            SanitizeRootCollectionsAfterRead(data);
            data.lastDiscoveredBiomeId = NormalizeLastDiscoveredBiomeId(
                data.lastDiscoveredBiomeId,
                data.discoveredBiomeIds,
                data.discoveredBiomeBitWords);
            SanitizeNonNegativeFiniteList(data.corporatePendingOrderTimers);
            ApplyInventoryBiologicalDecay(ref data.inventory, data.playerStats.environmentTemperature);
            RefreshInventoryShadowMirror(data);
            return true;
        }

        private static bool ReadSaveDataState(ref BufferReader reader, SaveData data)
        {
            return ReadTotalPlayTime(ref reader, data.version, out data.totalPlayTime)
                && ReadPlayerStats(ref reader, data.version, out data.playerStats)
                && ReadInventory(ref reader, data.version, out data.inventory)
                && ReadWorldState(ref reader, out data.worldState)
                && ReadProceduralWorldState(ref reader, data.version, out data.proceduralWorldState)
                && ReadConstruction(ref reader, data.version, out data.construction)
                && ReadScanLog(ref reader, out data.scanLog)
                && ReadBarter(ref reader, out data.barter)
                && ReadFieldOperationLog(ref reader, out data.fieldOperations)
                && ReadBeaconNetwork(ref reader, out data.beaconNetwork)
                && ReadExplorationMap(ref reader, data.version, out data.explorationMap)
                && ReadPdaLogbook(ref reader, data.version, out data.pdaLogbook)
                && ReadPdaMarkers(ref reader, data.version, out data.pdaMarkers)
                && ReadPdaAdvisories(ref reader, out data.pdaAdvisories)
                && ReadProceduralLore(ref reader, out data.proceduralLore)
                && ReadAchievementRegistry(ref reader, out data.achievements)
                && ReadRunModifiers(ref reader, out data.runModifiers)
                && ReadMetaCampaign(ref reader, data.version, out data.metaCampaign)
                && ReadResourceScarcity(ref reader, data.version, out data.resourceScarcity)
                && ReadEnvironmentalStrain(ref reader, out data.environmentalStrain)
                && ReadEcosystemState(ref reader, data.version, out data.ecosystemState)
                && ReadExternalScavengerSites(ref reader, data.version, out data.externalScavengerSites)
                && ReadHazardZoneRuntime(ref reader, data.version, out data.hazardZones);
        }

        private static bool ReadSaveDataProgress(ref BufferReader reader, SaveData data)
        {
            return ReadStringFloatDictionary(
                    ref reader,
                    out data.toolDurabilityMap,
                    SaveData.MaxToolDurabilityRecords,
                    nameof(data.toolDurabilityMap))
                && ReadStringBoolDictionary(
                    ref reader,
                    out data.toolBrokenMap,
                    SaveData.MaxToolDurabilityRecords,
                    nameof(data.toolBrokenMap))
                && ReadDiscoveredBiomeHashSet(
                    ref reader,
                    out data.discoveredBiomeIds,
                    nameof(data.discoveredBiomeIds))
                && reader.ReadStructArrayBounded(
                    out data.discoveredBiomeBitWords,
                    BiomeDiscoveryBitMask.WordCount,
                    nameof(data.discoveredBiomeBitWords))
                && reader.ReadInt(out data.lastDiscoveredBiomeId)
                && reader.ReadInt(out data.narrativeDiscoveryCount)
                && ReadStringArray(
                    ref reader,
                    out data.narrativeDiscoveryIds,
                    SaveData.MaxNarrativeDiscoveries,
                    nameof(data.narrativeDiscoveryIds))
                && reader.ReadInt(out data.narrativeDepthTier)
                && ReadNarrativeAupTriggeredMask(ref reader, data.version, out data.narrativeAupTriggeredMask)
                && ReadStringList(
                    ref reader,
                    out data.audioLogDiscoveredIds,
                    SaveData.MaxLegacyAudioLogDiscoveredIds,
                    nameof(data.audioLogDiscoveredIds))
                && ReadAudioLogDiscoveryBitWords(ref reader, data.version, data)
                && ReadEncryptedAudioLogFragments(ref reader, data.version, data)
                && reader.ReadStructArrayBounded(
                    out data.industrialLoreUnlockWords,
                    IndustrialLoreBitMask.WordCount,
                    nameof(data.industrialLoreUnlockWords))
                && ReadDataArchaeology(ref reader, data.version, data)
                && ReadStringList(
                    ref reader,
                    out data.questActiveIds,
                    SaveData.MaxLegacyQuestIds,
                    nameof(data.questActiveIds))
                && ReadStringList(
                    ref reader,
                    out data.questCompletedIds,
                    SaveData.MaxLegacyQuestIds,
                    nameof(data.questCompletedIds))
                && reader.ReadBool(out data.atlasSignalDetected)
                && reader.ReadFloat(out data.atlasSignalPulseTimer)
                && reader.ReadInt(out data.atlasSignalRevealStage);
        }

        private static bool ReadSaveDataPlayer(ref BufferReader reader, SaveData data)
        {
            return ReadSuitUpgradeMask(ref reader, data.version, out data.suitUpgradeMask)
                && ReadStringList(
                    ref reader,
                    out data.suitInstalledUpgradeIds,
                    SaveData.MaxSuitUpgradeIds,
                    nameof(data.suitInstalledUpgradeIds))
                && ReadStringList(
                    ref reader,
                    out data.suitUnlockedBlueprintIds,
                    SaveData.MaxSuitUpgradeIds,
                    nameof(data.suitUnlockedBlueprintIds))
                && ReadStringList(
                    ref reader,
                    out data.suitBrokenUpgradeIds,
                    SaveData.MaxSuitUpgradeIds,
                    nameof(data.suitBrokenUpgradeIds))
                && reader.ReadString(out data.playerExpressionProfileId)
                && reader.ReadInt(out data.atlas6PlayerStatus)
                && reader.ReadInt(out data.atlas6BarterCount)
                && reader.ReadBool(out data.atlas6DirectiveConflictTriggered)
                && ReadAtlas6Liability(ref reader, data.version, data)
                && ReadStringList(
                    ref reader,
                    out data.corporateReceivedOrderIds,
                    SaveData.MaxCorporateOrderIds,
                    nameof(data.corporateReceivedOrderIds))
                && ReadStringList(
                    ref reader,
                    out data.corporatePendingOrderIds,
                    SaveData.MaxCorporateOrderIds,
                    nameof(data.corporatePendingOrderIds))
                && ReadFloatList(
                    ref reader,
                    out data.corporatePendingOrderTimers,
                    SaveData.MaxCorporateOrderIds,
                    nameof(data.corporatePendingOrderTimers))
                && reader.ReadFloat(out data.firstHourSessionTime)
                && reader.ReadInt(out data.firstHourMilestones)
                && reader.ReadInt(out data.firstHourGuidanceFlags)
                && reader.ReadInt(out data.endingChoice)
                && reader.ReadBool(out data.endingComplete)
                && reader.ReadBool(out data.endingConditionMet)
                && ReadStringList(
                    ref reader,
                    out data.missionActiveIds,
                    SaveData.MaxMissionIds,
                    nameof(data.missionActiveIds))
                && ReadStringList(
                    ref reader,
                    out data.missionCompletedIds,
                    SaveData.MaxMissionIds,
                    nameof(data.missionCompletedIds))
                && reader.ReadInt(out data.LODQualityPreset)
                && reader.ReadBool(out data.DynamicResolutionEnabled);
        }

        private static bool ReadSaveDataWorld(
            ref BufferReader reader,
            SaveData data,
            VoxelDeltaCellFlagsReadMode preV77CellFlagsReadMode)
        {
            return ReadRadiationGrid(ref reader, data.version, data)
                && ReadRtgDecay(ref reader, data.version, data)
                && ReadStringStringDictionary(
                    ref reader,
                    out data.CustomModData,
                    SaveData.MaxCustomModDataEntries,
                    nameof(data.CustomModData))
                && ReadFirstHourLockedDtos(ref reader, data.version, data)
                && ReadVoxelDeltaPersistence(
                    ref reader,
                    data.version,
                    preV77CellFlagsReadMode,
                    out data.voxelDeltaPersistence)
                && ReadCelestialLightPhase(ref reader, data.version, data)
                && ReadProceduralTerrainIdentity(
                    ref reader,
                    data.version,
                    out data.proceduralTerrainIdentity);
        }

        private static void SanitizeRootCollectionsAfterRead(SaveData data)
        {
            if (data == null)
                return;

            if (string.IsNullOrWhiteSpace(data.timestamp))
                data.timestamp = DateTime.Now.ToString("O");
            data.toolDurabilityMap ??= new Dictionary<string, float>(SaveData.MaxToolDurabilityRecords);
            data.toolBrokenMap ??= new Dictionary<string, bool>(SaveData.MaxToolDurabilityRecords);
            data.CustomModData ??= new Dictionary<string, string>(SaveData.MaxCustomModDataEntries);
            data.discoveredBiomeIds ??= new HashSet<int>(SaveData.MaxLegacyDiscoveredBiomeIds);
            SanitizeDiscoveredBiomeIds(data.discoveredBiomeIds);
            SaveData.EnsureExactArrayCapacity(ref data.discoveredBiomeBitWords, BiomeDiscoveryBitMask.WordCount);
            BiomeDiscoveryBitMask.SanitizeWords(data.discoveredBiomeBitWords);
            if (!BiomeDiscoveryBitMask.HasAnySet(data.discoveredBiomeBitWords) && data.discoveredBiomeIds.Count > 0)
                BiomeDiscoveryBitMask.Pack(data.discoveredBiomeIds, data.discoveredBiomeBitWords);
            SanitizeNarrativeDiscoveriesAfterRead(data);

            data.audioLogDiscoveredIds ??= new List<string>(SaveData.MaxLegacyAudioLogDiscoveredIds);
            CompactNonBlankStringList(data.audioLogDiscoveredIds, SaveData.MaxLegacyAudioLogDiscoveredIds);
            AudioLogDiscoveryBitMask.EnsureCapacity(ref data.audioLogDiscoveryBitWords);
            SaveData.EnsureExactArrayCapacity(
                ref data.audioLogEncryptedFragmentHashes,
                SaveData.MaxEncryptedAudioLogFragments);
            SaveData.EnsureExactArrayCapacity(
                ref data.audioLogEncryptedFragmentBits,
                SaveData.MaxEncryptedAudioLogFragments);
            data.audioLogEncryptedFragmentCount = Math.Clamp(
                data.audioLogEncryptedFragmentCount,
                0,
                SaveData.MaxEncryptedAudioLogFragments);
            SaveData.EnsureExactArrayCapacity(ref data.industrialLoreUnlockWords, IndustrialLoreBitMask.WordCount);
            IndustrialLoreBitMask.SanitizeWords(data.industrialLoreUnlockWords);

            data.questActiveIds ??= new List<string>(SaveData.MaxLegacyQuestIds);
            data.questCompletedIds ??= new List<string>(SaveData.MaxLegacyQuestIds);
            data.suitInstalledUpgradeIds ??= new List<string>(SaveData.MaxSuitUpgradeIds);
            data.suitUnlockedBlueprintIds ??= new List<string>(SaveData.MaxSuitUpgradeIds);
            data.suitBrokenUpgradeIds ??= new List<string>(SaveData.MaxSuitUpgradeIds);
            CompactNonBlankStringList(data.questActiveIds, SaveData.MaxLegacyQuestIds);
            CompactNonBlankStringList(data.questCompletedIds, SaveData.MaxLegacyQuestIds);
            CompactNonBlankStringList(data.suitInstalledUpgradeIds, SaveData.MaxSuitUpgradeIds);
            CompactNonBlankStringList(data.suitUnlockedBlueprintIds, SaveData.MaxSuitUpgradeIds);
            CompactNonBlankStringList(data.suitBrokenUpgradeIds, SaveData.MaxSuitUpgradeIds);
            data.playerExpressionProfileId = SaveData.SanitizePersistenceString(data.playerExpressionProfileId);
            data.corporateReceivedOrderIds ??= new List<string>(SaveData.MaxCorporateOrderIds);
            CompactNonBlankStringList(data.corporateReceivedOrderIds, SaveData.MaxCorporateOrderIds);
            SanitizeCorporatePendingOrdersAfterRead(data);
            data.missionActiveIds ??= new List<string>(SaveData.MaxMissionIds);
            data.missionCompletedIds ??= new List<string>(SaveData.MaxMissionIds);
            CompactNonBlankStringList(data.missionActiveIds, SaveData.MaxMissionIds);
            CompactNonBlankStringList(data.missionCompletedIds, SaveData.MaxMissionIds);
            if (data.version < PreV73ReadRepairVersion)
                data.DynamicResolutionEnabled = true;
        }

        private static int SanitizeAtlasSignalRevealStageAfterRead(SaveData data)
        {
            if (data == null)
                return 0;

            int clampedRevealStage = math.clamp(data.atlasSignalRevealStage, 0, 4);
            if (data.version >= PreV73ReadRepairVersion)
                return clampedRevealStage;

            int inferredRevealStage = data.endingConditionMet
                ? 4
                : data.narrativeDepthTier >= 4
                    ? 3
                    : data.narrativeDepthTier >= 3
                        ? 2
                        : data.atlasSignalDetected
                            ? 2
                            : 0;
            return math.max(clampedRevealStage, inferredRevealStage);
        }

        private static void SanitizeNarrativeDiscoveriesAfterRead(SaveData data)
        {
            if (data == null)
                return;

            data.narrativeDiscoveryCount = ResolveDecodedCollectionCount(
                data.narrativeDiscoveryIds,
                SaveData.MaxNarrativeDiscoveries);
            SaveData.EnsureExactArrayCapacity(
                ref data.narrativeDiscoveryIds,
                SaveData.MaxNarrativeDiscoveries);
            CompactNonBlankStringArraySlice(
                data.narrativeDiscoveryIds,
                ref data.narrativeDiscoveryCount,
                SaveData.MaxNarrativeDiscoveries);
        }

        private static void SanitizeCorporatePendingOrdersAfterRead(SaveData data)
        {
            if (data == null)
                return;

            if (data.corporatePendingOrderIds == null)
                data.corporatePendingOrderIds = new List<string>();

            if (data.corporatePendingOrderTimers == null)
                data.corporatePendingOrderTimers = new List<float>();

            int safeCount = ClampPairedListCount(
                data.corporatePendingOrderIds,
                data.corporatePendingOrderTimers,
                SaveData.MaxCorporateOrderIds);
            CompactNonBlankStringFloatPairs(data.corporatePendingOrderIds, data.corporatePendingOrderTimers, safeCount);
        }

        private static bool WriteFirstHourLockedDtos(ref BufferWriter writer, SaveData data)
        {
            PlayerKinematicStateDTO playerState = data != null
                ? PlayerKinematicStateDTO.FromPlayerStats(in data.playerStats)
                : default;
            SaveDataPlayerSurvivalSanitizer.SanitizePlayerKinematicState(ref playerState);
            SaveDataInventorySanitizer.ResolveInventoryShadowPayloadMetadata(
                data,
                out int inventoryShadowPayloadLength,
                out uint inventoryShadowPayloadHash);
            InventoryShadowDTO inventoryShadow = data != null
                ? SaveDataInventorySanitizer.BuildInventoryShadow(
                    in data.inventory,
                    inventoryShadowPayloadLength,
                    inventoryShadowPayloadHash,
                    inventoryShadowPayloadLength > 0)
                : default;
            int floodCount = 0;
            ConstructionDTO construction = data != null ? data.construction : default;
            if (data != null)
            {
                floodCount = Math.Clamp(
                    construction.moduleCount,
                    0,
                    Math.Min(
                        ConstructionDTO.MaxModules,
                        construction.modules != null ? construction.modules.Length : 0));
            }
            if (!writer.WriteStruct(playerState) ||
                !writer.WriteStruct(inventoryShadow) ||
                !writer.WriteInt(floodCount))
            {
                return false;
            }

            for (int i = 0; i < floodCount; i++)
            {
                int moduleHashId = construction.ResolveHabitatFloodStateModuleHashId(i);
                HabitatFloodStateDTO floodState = HabitatFloodStateDTO.FromModule(in construction.modules[i], moduleHashId);
                if (!writer.WriteStruct(floodState))
                    return false;
            }

            return true;
        }

        private static bool ReadFirstHourLockedDtos(ref BufferReader reader, int saveDataVersion, SaveData data)
        {
            if (data == null)
                return false;

            if (saveDataVersion < FirstHourDtoLockSaveVersion)
            {
                data.RefreshFirstHourDtoMirrors();
                return true;
            }

            if (!reader.ReadStruct(out data.playerKinematicState) ||
                !reader.ReadStruct(out data.inventoryShadow) ||
                !reader.ReadInt(out int floodStateCount))
            {
                return false;
            }

            if (floodStateCount < 0 || floodStateCount > ConstructionDTO.MaxModules)
            {
                reader.SetError("Habitat flood state count exceeds the supported range.");
                return false;
            }

            int activeFloodStateCount = Math.Clamp(
                data.construction.moduleCount,
                0,
                data.construction.modules != null
                    ? Math.Min(ConstructionDTO.MaxModules, data.construction.modules.Length)
                    : 0);

            if (data.construction.habitatFloodStates == null ||
                data.construction.habitatFloodStates.Length < ConstructionDTO.MaxModules)
            {
                data.construction.habitatFloodStates = new HabitatFloodStateDTO[ConstructionDTO.MaxModules];
            }

            for (int i = 0; i < floodStateCount; i++)
            {
                if (!reader.ReadStruct(out HabitatFloodStateDTO _))
                    return false;
            }

            SaveDataPlayerSurvivalSanitizer.SanitizePlayerKinematicState(ref data.playerKinematicState);
            data.playerKinematicState.ApplyTo(ref data.playerStats);
            SaveDataPlayerSurvivalSanitizer.SanitizePlayerStats(ref data.playerStats);
            int inventoryShadowPayloadLength = SaveDataInventorySanitizer.ResolveInventoryShadowPayloadLength(
                in data.inventoryShadow,
                in data.inventory);
            uint inventoryShadowPayloadHash = inventoryShadowPayloadLength > 0 ? data.inventoryShadow.payloadHash : 0u;
            SaveDataInventorySanitizer.SanitizeInventoryShadow(
                ref data.inventoryShadow,
                in data.inventory,
                inventoryShadowPayloadLength,
                inventoryShadowPayloadHash,
                inventoryShadowPayloadLength > 0);
            // First-hour flood states are a locked mirror; construction modules remain the source of truth.
            for (int i = 0; i < activeFloodStateCount; i++)
            {
                int moduleHashId = data.construction.ResolveHabitatFloodStateModuleHashId(i);
                data.construction.habitatFloodStates[i] =
                    HabitatFloodStateDTO.FromModule(in data.construction.modules[i], moduleHashId);
            }

            data.construction.habitatFloodStateCount = activeFloodStateCount;
            return true;
        }

        private static bool WriteVoxelDeltaPersistence(ref BufferWriter writer, VoxelDeltaPersistenceDTO value)
        {
            if (value.chunkCount > MaxVoxelDeltaChunks)
            {
                writer.Error = "Voxel delta chunk count exceeds the supported range.";
                return false;
            }

            int availableChunkCount = value.chunks != null ? value.chunks.Length : 0;
            int chunkCount = Math.Clamp(
                value.chunkCount,
                0,
                Math.Min(MaxVoxelDeltaChunks, availableChunkCount));
            int totalCellCount = ResolveVoxelDeltaTotalCellCount(value.chunks, chunkCount);
            if (!writer.WriteInt(chunkCount) || !writer.WriteInt(totalCellCount))
                return false;

            for (int i = 0; i < chunkCount; i++)
            {
                if (!WriteVoxelDeltaChunk(ref writer, in value.chunks[i]))
                    return false;
            }

            if (value.carvingOperationCount > MaxVoxelDeltaCarvingOperations)
            {
                writer.Error = "Voxel carving operation count exceeds the supported range.";
                return false;
            }

            VoxelCarvingOperationDTO[] carvingOperations =
                value.carvingOperations ?? Array.Empty<VoxelCarvingOperationDTO>();
            int carvingOperationCount = ClampCollectionCount(
                value.carvingOperationCount,
                carvingOperations,
                MaxVoxelDeltaCarvingOperations);
            return WriteVoxelCarvingOperations(ref writer, carvingOperations, carvingOperationCount);
        }

        private static bool ReadVoxelDeltaPersistence(
            ref BufferReader reader,
            int saveDataVersion,
            VoxelDeltaCellFlagsReadMode preV77CellFlagsReadMode,
            out VoxelDeltaPersistenceDTO value)
        {
            value = VoxelDeltaPersistenceDTO.CreateDefault();
            if (saveDataVersion < VoxelDeltaSaveVersion)
                return true;

            if (saveDataVersion == VoxelDeltaSaveVersion)
            {
                if (preV77CellFlagsReadMode == VoxelDeltaCellFlagsReadMode.PreV77Absent ||
                    preV77CellFlagsReadMode == VoxelDeltaCellFlagsReadMode.PreV77LegacyPresent)
                {
                    return ReadVoxelDeltaPersistenceBody(
                        ref reader,
                        saveDataVersion,
                        preV77CellFlagsReadMode,
                        out value);
                }

                BufferReader preV77WithFlagsReader = reader;
                if (ReadVoxelDeltaPersistenceBody(
                        ref preV77WithFlagsReader,
                        saveDataVersion,
                        VoxelDeltaCellFlagsReadMode.PreV77LegacyPresent,
                        out value))
                {
                    reader = preV77WithFlagsReader;
                    return true;
                }

                BufferReader preV77WithoutFlagsReader = reader;
                if (ReadVoxelDeltaPersistenceBody(
                        ref preV77WithoutFlagsReader,
                        saveDataVersion,
                        VoxelDeltaCellFlagsReadMode.PreV77Absent,
                        out value))
                {
                    reader = preV77WithoutFlagsReader;
                    return true;
                }

                string error = string.IsNullOrEmpty(preV77WithoutFlagsReader.Error)
                    ? preV77WithFlagsReader.Error
                    : preV77WithoutFlagsReader.Error;
                reader.SetError(error);
                value = VoxelDeltaPersistenceDTO.CreateDefault();
                return false;
            }

            return ReadVoxelDeltaPersistenceBody(
                ref reader,
                saveDataVersion,
                VoxelDeltaCellFlagsReadMode.CurrentRequired,
                out value);
        }

        private static bool WriteProceduralTerrainIdentity(
            ref BufferWriter writer,
            ProceduralTerrainIdentityDTO value)
        {
            value = SanitizeProceduralTerrainIdentity(value);
            return writer.WriteUInt(value.authoringSeed)
                && writer.WriteInt(value.runtimeSeed)
                && writer.WriteInt(value.worldGenerationVersionId)
                && writer.WriteUInt(value.macroArtifactVersion)
                && writer.WriteFloat(value.macroChunkSizeMeters)
                && writer.WriteInt(value.chunkMinX)
                && writer.WriteInt(value.chunkMinZ)
                && writer.WriteInt(value.chunkMaxX)
                && writer.WriteInt(value.chunkMaxZ)
                && writer.WriteUInt(value.chunkArtifactRangeHash)
                && writer.WriteFloat(value.selectedWaterLevelY)
                && writer.WriteFloat(value.waterCalibrationTravelMeters)
                && writer.WriteUInt(value.waterCalibrationSourceHash)
                && writer.WriteUInt(value.flags)
                && writer.WriteUInt(value.terrainProviderFlags)
                && writer.WriteInt(value.heightCacheRevision)
                && writer.WriteUInt(value.terrainEntityHash)
                && writer.WriteUInt(value.surfaceMaterialContractVersion)
                && writer.WriteUInt(value.mesoDetailContractVersion)
                && writer.WriteUInt(value.detailEligibilityContractVersion)
                && writer.WriteUInt(value.mesoParamsHash);
        }

        private static bool WriteCelestialLightPhase(ref BufferWriter writer, SaveData data)
        {
            bool hasPhase = data != null &&
                            data.celestialLightPhaseSerialized &&
                            math.isfinite(data.celestialLightTimeOfDay01);
            float timeOfDay01 = hasPhase
                ? math.saturate(data.celestialLightTimeOfDay01)
                : SaveData.CelestialLightTimeOfDayDefault;

            return writer.WriteBool(hasPhase)
                && writer.WriteFloat(timeOfDay01);
        }

        private static bool ReadCelestialLightPhase(ref BufferReader reader, int saveDataVersion, SaveData data)
        {
            if (data == null)
                return true;

            if (saveDataVersion < CelestialLightPhaseSaveVersion)
            {
                data.celestialLightPhaseSerialized = false;
                data.celestialLightTimeOfDay01 = SaveData.CelestialLightTimeOfDayDefault;
                return true;
            }

            if (!reader.ReadBool(out data.celestialLightPhaseSerialized) ||
                !reader.ReadFloat(out data.celestialLightTimeOfDay01))
            {
                return false;
            }

            SanitizeCelestialLightPhase(data);
            return true;
        }

        private static void SanitizeCelestialLightPhase(SaveData data)
        {
            if (data == null)
                return;

            bool hasPhase = data.celestialLightPhaseSerialized &&
                            math.isfinite(data.celestialLightTimeOfDay01);
            data.celestialLightPhaseSerialized = hasPhase;
            data.celestialLightTimeOfDay01 = hasPhase
                ? math.saturate(data.celestialLightTimeOfDay01)
                : SaveData.CelestialLightTimeOfDayDefault;
        }

        private static bool ReadProceduralTerrainIdentity(
            ref BufferReader reader,
            int saveDataVersion,
            out ProceduralTerrainIdentityDTO value)
        {
            value = default;
            if (saveDataVersion < ProceduralTerrainIdentitySaveVersion)
                return true;

            if (!reader.ReadUInt(out value.authoringSeed)
                || !reader.ReadInt(out value.runtimeSeed)
                || !reader.ReadInt(out value.worldGenerationVersionId)
                || !reader.ReadUInt(out value.macroArtifactVersion)
                || !reader.ReadFloat(out value.macroChunkSizeMeters)
                || !reader.ReadInt(out value.chunkMinX)
                || !reader.ReadInt(out value.chunkMinZ)
                || !reader.ReadInt(out value.chunkMaxX)
                || !reader.ReadInt(out value.chunkMaxZ)
                || !reader.ReadUInt(out value.chunkArtifactRangeHash)
                || !reader.ReadFloat(out value.selectedWaterLevelY)
                || !reader.ReadFloat(out value.waterCalibrationTravelMeters)
                || !reader.ReadUInt(out value.waterCalibrationSourceHash)
                || !reader.ReadUInt(out value.flags))
            {
                return false;
            }

            if (saveDataVersion >= ProceduralTerrainIdentityContractSaveVersion &&
                (!reader.ReadUInt(out value.terrainProviderFlags)
                 || !reader.ReadInt(out value.heightCacheRevision)
                 || !reader.ReadUInt(out value.terrainEntityHash)
                 || !reader.ReadUInt(out value.surfaceMaterialContractVersion)
                 || !reader.ReadUInt(out value.mesoDetailContractVersion)
                 || !reader.ReadUInt(out value.detailEligibilityContractVersion)
                 || !reader.ReadUInt(out value.mesoParamsHash)))
            {
                return false;
            }

            value = SanitizeProceduralTerrainIdentity(value);
            return true;
        }

        private static ProceduralTerrainIdentityDTO SanitizeProceduralTerrainIdentity(
            ProceduralTerrainIdentityDTO value)
        {
            value.worldGenerationVersionId = math.max(0, value.worldGenerationVersionId);
            value.macroChunkSizeMeters = math.isfinite(value.macroChunkSizeMeters)
                ? math.max(0f, value.macroChunkSizeMeters)
                : 0f;
            value.selectedWaterLevelY =
                math.isfinite(value.selectedWaterLevelY) &&
                math.abs(value.selectedWaterLevelY) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY
                    ? value.selectedWaterLevelY
                    : 0f;
            value.waterCalibrationTravelMeters =
                math.isfinite(value.waterCalibrationTravelMeters) &&
                value.waterCalibrationTravelMeters > 0f
                    ? math.min(
                        value.waterCalibrationTravelMeters,
                        WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
                    : 0f;

            if (value.chunkMinX > value.chunkMaxX)
            {
                int swap = value.chunkMinX;
                value.chunkMinX = value.chunkMaxX;
                value.chunkMaxX = swap;
            }

            if (value.chunkMinZ > value.chunkMaxZ)
            {
                int swap = value.chunkMinZ;
                value.chunkMinZ = value.chunkMaxZ;
                value.chunkMaxZ = swap;
            }

            if (value.macroArtifactVersion == 0u)
                value.flags &= ~ProceduralTerrainIdentityDTO.FlagsMacroGeologyPresent;
            if (value.waterCalibrationSourceHash == 0u)
                value.flags &= ~ProceduralTerrainIdentityDTO.FlagsWaterCalibrationPresent;
            value.heightCacheRevision = math.max(0, value.heightCacheRevision);
            if (value.terrainProviderFlags == 0u && value.terrainEntityHash == 0u)
                value.flags &= ~ProceduralTerrainIdentityDTO.FlagsTerrainProviderIdentityPresent;
            if (value.heightCacheRevision == 0 &&
                value.terrainEntityHash == 0u &&
                (value.terrainProviderFlags & TerrainArtifactIdentityDTO.FlagsHeightPayloadPresent) == 0u)
            {
                value.flags &= ~ProceduralTerrainIdentityDTO.FlagsTerrainHeightPayloadPresent;
            }

            if (value.surfaceMaterialContractVersion == 0u)
                value.flags &= ~ProceduralTerrainIdentityDTO.FlagsTerrainMaterialContractsPresent;
            if (value.mesoDetailContractVersion == 0u ||
                value.detailEligibilityContractVersion == 0u ||
                value.mesoParamsHash == 0u)
            {
                value.flags &= ~ProceduralTerrainIdentityDTO.FlagsTerrainMesoContractsPresent;
            }

            return value;
        }

        private static bool ReadVoxelDeltaPersistenceBody(
            ref BufferReader reader,
            int saveDataVersion,
            VoxelDeltaCellFlagsReadMode cellFlagsReadMode,
            out VoxelDeltaPersistenceDTO value)
        {
            value = VoxelDeltaPersistenceDTO.CreateDefault();
            if (!reader.ReadInt(out int chunkCount) || !reader.ReadInt(out int serializedTotalCellCount))
                return false;

            if (chunkCount < 0 || chunkCount > MaxVoxelDeltaChunks)
            {
                reader.SetError("Voxel delta chunk count exceeds the supported range.");
                return false;
            }

            if (serializedTotalCellCount < 0)
            {
                reader.SetError("Voxel delta total cell count is negative.");
                return false;
            }

            value.EnsureCapacity(chunkCount);
            value.chunkCount = chunkCount;
            value.totalCellCount = 0;
            for (int i = 0; i < chunkCount; i++)
            {
                if (!ReadVoxelDeltaChunk(ref reader, saveDataVersion, cellFlagsReadMode, out value.chunks[i]))
                    return false;

                value.totalCellCount = AddVoxelDeltaCellCountClamped(
                    value.totalCellCount,
                    value.chunks[i].cellCount);
            }

            if (value.totalCellCount != serializedTotalCellCount)
            {
                reader.SetError("Voxel delta total cell count does not match the serialized chunk data.");
                return false;
            }

            if (reader.IsAtEnd())
            {
                if (saveDataVersion != VoxelDeltaSaveVersion)
                {
                    reader.SetError("Voxel carving operation payload is missing.");
                    return false;
                }

                value.carvingOperations = Array.Empty<VoxelCarvingOperationDTO>();
                value.carvingOperationCount = 0;
                return true;
            }

            if (!reader.ReadStructArrayBounded(
                    out value.carvingOperations,
                    MaxVoxelDeltaCarvingOperations,
                    nameof(value.carvingOperations)))
            {
                return false;
            }

            value.carvingOperations ??= Array.Empty<VoxelCarvingOperationDTO>();
            value.carvingOperationCount = Math.Clamp(
                value.carvingOperations.Length,
                0,
                MaxVoxelDeltaCarvingOperations);
            SanitizeVoxelCarvingOperations(value.carvingOperations, value.carvingOperationCount);
            return true;
        }

        private static bool WriteVoxelCarvingOperations(
            ref BufferWriter writer,
            VoxelCarvingOperationDTO[] operations,
            int count)
        {
            if (operations == null)
                return writer.WriteInt(0);

            int safeCount = Math.Clamp(count, 0, Math.Min(operations.Length, MaxVoxelDeltaCarvingOperations));
            if (!writer.WriteInt(safeCount))
                return false;

            for (int i = 0; i < safeCount; i++)
            {
                VoxelCarvingOperationDTO operation = SanitizeVoxelCarvingOperation(operations[i]);
                if (!writer.WriteStruct(operation))
                    return false;
            }

            return true;
        }

        private static void SanitizeVoxelCarvingOperations(VoxelCarvingOperationDTO[] operations, int count)
        {
            if (operations == null)
                return;

            int safeCount = Math.Clamp(count, 0, operations.Length);
            for (int i = 0; i < safeCount; i++)
                operations[i] = SanitizeVoxelCarvingOperation(operations[i]);
        }

        private static VoxelCarvingOperationDTO SanitizeVoxelCarvingOperation(VoxelCarvingOperationDTO operation)
        {
            if (!math.all(math.isfinite(operation.localPosition)))
            {
                operation.localPosition = new float3(
                    math.isfinite(operation.localPosition.x) ? operation.localPosition.x : 0f,
                    math.isfinite(operation.localPosition.y) ? operation.localPosition.y : 0f,
                    math.isfinite(operation.localPosition.z) ? operation.localPosition.z : 0f);
            }

            if (!math.isfinite(operation.radius) || operation.radius < 0f)
                operation.radius = 0f;

            if (operation.operation != VoxelCarvingOperationKind.Subtract &&
                operation.operation != VoxelCarvingOperationKind.Add)
            {
                operation.operation = VoxelCarvingOperationKind.Subtract;
            }

            return operation;
        }

        private static bool WriteVoxelDeltaChunk(ref BufferWriter writer, in VoxelDeltaChunkDTO value)
        {
            byte storageMode = ResolveVoxelDeltaSerializedStorageMode(in value);
            ushort uniformSdfValueBits = storageMode == VoxelDeltaSerializedStorageUniformSdfRle
                ? value.uniformSdfValueBits
                : (ushort)0;

            if (!writer.WriteLong(value.chunkX) ||
                !writer.WriteLong(value.chunkY) ||
                !writer.WriteLong(value.chunkZ) ||
                !writer.WriteFloat(SanitizeVoxelSize(value.voxelSize)) ||
                !writer.WriteByte(storageMode) ||
                !writer.WriteStruct(uniformSdfValueBits))
            {
                return false;
            }

            if (storageMode == VoxelDeltaSerializedStorageUniformSdfRle)
                return true;

            if (storageMode == VoxelDeltaSerializedStorageDense)
            {
                return WriteUIntArrayFixed(ref writer, value.dirtyMaskWords, VoxelDeltaChunkDTO.DirtyMaskWordCount)
                    && WriteUShortArrayFixed(ref writer, value.sdfValueBits, VoxelDeltaChunkDTO.CellCount)
                    && WriteByteArraySliceWithZeroFill(
                        ref writer,
                        value.materialIds,
                        VoxelDeltaChunkDTO.CellCount,
                        VoxelDeltaChunkDTO.CellCount)
                    && WriteMaskedByteArraySlice(
                        ref writer,
                        value.cellFlags,
                        VoxelDeltaChunkDTO.CellCount,
                        VoxelDeltaChunkDTO.CellCount,
                        VoxelDeltaChunkDTO.SupportedCellFlags);
            }

            VoxelDeltaCellDTO[] cells = value.cells ?? Array.Empty<VoxelDeltaCellDTO>();
            int cellCount = ClampCollectionCount(value.cellCount, cells, VoxelDeltaChunkDTO.CellCount);
            return WriteVoxelDeltaCellArraySlice(ref writer, cells, cellCount);
        }

        private static bool WriteUIntArrayFixed(ref BufferWriter writer, uint[] values, int count)
        {
            int safeCount = Math.Max(0, count);
            if (!writer.WriteInt(safeCount))
                return false;

            for (int i = 0; i < safeCount; i++)
            {
                uint value = values != null && i < values.Length ? values[i] : 0u;
                if (!writer.WriteUInt(value))
                    return false;
            }

            return true;
        }

        private static bool WriteUShortArrayFixed(ref BufferWriter writer, ushort[] values, int count)
        {
            int safeCount = Math.Max(0, count);
            if (!writer.WriteInt(safeCount))
                return false;

            for (int i = 0; i < safeCount; i++)
            {
                ushort value = values != null && i < values.Length ? values[i] : (ushort)0;
                if (!writer.WriteStruct(value))
                    return false;
            }

            return true;
        }

        private static bool ReadVoxelDeltaChunk(
            ref BufferReader reader,
            int saveDataVersion,
            VoxelDeltaCellFlagsReadMode cellFlagsReadMode,
            out VoxelDeltaChunkDTO value)
        {
            value = default;
            if (!reader.ReadLong(out value.chunkX) ||
                !reader.ReadLong(out value.chunkY) ||
                !reader.ReadLong(out value.chunkZ) ||
                !reader.ReadFloat(out value.voxelSize) ||
                !reader.ReadByte(out byte storageMode) ||
                !reader.ReadStruct(out value.uniformSdfValueBits))
            {
                return false;
            }

            value.voxelSize = SanitizeVoxelSize(value.voxelSize);
            value.reservedStorage = 0;

            if (storageMode == VoxelDeltaSerializedStorageUniformSdfRle)
            {
                // VoxelDeltaChunkDTO.EnsureCapacity(0) resets the storage identity fields, uniformSdfValueBits
                // included, so the value decoded above has to survive the reset - it is the whole payload of a
                // uniform chunk.
                ushort decodedUniformSdfValueBits = value.uniformSdfValueBits;
                value.EnsureCapacity(0);
                value.storageFlags = VoxelDeltaChunkDTO.StorageUniformSdfRle;
                value.uniformSdfValueBits = decodedUniformSdfValueBits;
                value.cellCount = VoxelDeltaChunkDTO.CellCount;
                return true;
            }

            if (storageMode == VoxelDeltaSerializedStorageDense)
            {
                if (!reader.ReadStructArrayBounded(
                        out value.dirtyMaskWords,
                        VoxelDeltaChunkDTO.DirtyMaskWordCount,
                        nameof(value.dirtyMaskWords)) ||
                    !reader.ReadStructArrayBounded(
                        out value.sdfValueBits,
                        VoxelDeltaChunkDTO.CellCount,
                        nameof(value.sdfValueBits)) ||
                    !reader.ReadStructArrayBounded(
                        out value.materialIds,
                        VoxelDeltaChunkDTO.CellCount,
                        nameof(value.materialIds)) ||
                    !ReadVoxelDeltaCellFlags(ref reader, saveDataVersion, cellFlagsReadMode, out value.cellFlags))
                {
                    return false;
                }

                if (!HasDenseVoxelDeltaStorage(in value))
                {
                    reader.SetError("Voxel delta dense storage arrays are incomplete.");
                    return false;
                }

                value.storageFlags = VoxelDeltaChunkDTO.StorageDense;
                value.cellCount = CountVoxelDeltaDirtyMaskBits(value.dirtyMaskWords);
                value.cells = Array.Empty<VoxelDeltaCellDTO>();
                return true;
            }

            if (storageMode != VoxelDeltaSerializedStorageLegacyCells)
            {
                reader.SetError("Voxel delta storage mode is outside the supported range.");
                return false;
            }

            // WriteVoxelDeltaCellArraySlice emits a single length prefix and then the cells; there is no
            // separate logical cell count on the wire, so the decoded array length IS the cell count.
            if (!reader.ReadStructArrayBounded(
                    out value.cells,
                    VoxelDeltaChunkDTO.CellCount,
                    nameof(value.cells)))
            {
                return false;
            }

            value.cells ??= Array.Empty<VoxelDeltaCellDTO>();
            value.cellCount = ResolveDecodedCollectionCount(value.cells, VoxelDeltaChunkDTO.CellCount);
            SanitizeVoxelDeltaCells(value.cells, value.cellCount);
            value.storageFlags = VoxelDeltaChunkDTO.StorageDense;
            value.dirtyMaskWords = Array.Empty<uint>();
            value.sdfValueBits = Array.Empty<ushort>();
            value.materialIds = Array.Empty<byte>();
            value.cellFlags = Array.Empty<byte>();
            return true;
        }

        private static bool ReadVoxelDeltaCellFlags(
            ref BufferReader reader,
            int saveDataVersion,
            VoxelDeltaCellFlagsReadMode readMode,
            out byte[] cellFlags)
        {
            if (readMode == VoxelDeltaCellFlagsReadMode.PreV77Absent)
            {
                cellFlags = new byte[VoxelDeltaChunkDTO.CellCount];
                return true;
            }

            if (readMode == VoxelDeltaCellFlagsReadMode.PreV77LegacyPresent &&
                (!reader.TryPeekInt(out int legacyCellFlagCount) ||
                 legacyCellFlagCount != VoxelDeltaChunkDTO.CellCount))
            {
                reader.SetError("Voxel delta legacy dense cell flags are absent.");
                cellFlags = null;
                return false;
            }

            if (saveDataVersion < VoxelDeltaDenseCellFlagsSaveVersion &&
                readMode == VoxelDeltaCellFlagsReadMode.CurrentRequired)
            {
                cellFlags = new byte[VoxelDeltaChunkDTO.CellCount];
                return true;
            }

            if (!reader.ReadStructArrayBounded(
                    out cellFlags,
                    VoxelDeltaChunkDTO.CellCount,
                    nameof(cellFlags)))
            {
                return false;
            }

            SanitizeVoxelDeltaCellFlags(cellFlags);
            return true;
        }

        private static void SanitizeVoxelDeltaCellFlags(byte[] cellFlags)
        {
            if (cellFlags == null)
                return;

            for (int i = 0; i < cellFlags.Length; i++)
                cellFlags[i] = SanitizeVoxelDeltaCellFlags(cellFlags[i]);
        }

        private static byte SanitizeVoxelDeltaCellFlags(byte cellFlags)
        {
            return (byte)(cellFlags & VoxelDeltaChunkDTO.SupportedCellFlags);
        }

        private static bool WriteVoxelDeltaCellArraySlice(
            ref BufferWriter writer,
            VoxelDeltaCellDTO[] values,
            int count)
        {
            if (values == null)
                return writer.WriteInt(NullCollectionCount);

            int safeCount = Math.Clamp(count, 0, values.Length);
            if (!writer.WriteInt(safeCount))
                return false;

            for (int i = 0; i < safeCount; i++)
            {
                VoxelDeltaCellDTO value = values[i];
                value.flags = SanitizeVoxelDeltaCellFlags(value.flags);
                if (!writer.WriteStruct(value))
                    return false;
            }

            return true;
        }

        private static void SanitizeVoxelDeltaCells(VoxelDeltaCellDTO[] cells, int count)
        {
            if (cells == null || count <= 0)
                return;

            int safeCount = Math.Clamp(count, 0, cells.Length);
            for (int i = 0; i < safeCount; i++)
                cells[i].flags = SanitizeVoxelDeltaCellFlags(cells[i].flags);
        }

        private static byte ResolveVoxelDeltaSerializedStorageMode(in VoxelDeltaChunkDTO value)
        {
            byte storageFlags = (byte)(value.storageFlags & VoxelDeltaChunkDTO.SupportedStorageFlags);
            if ((storageFlags & VoxelDeltaChunkDTO.StorageUniformSdfRle) != 0)
                return VoxelDeltaSerializedStorageUniformSdfRle;

            return HasDenseVoxelDeltaStorage(in value)
                ? VoxelDeltaSerializedStorageDense
                : VoxelDeltaSerializedStorageLegacyCells;
        }

        private static bool HasDenseVoxelDeltaStorage(in VoxelDeltaChunkDTO value)
        {
            return value.dirtyMaskWords != null &&
                   value.dirtyMaskWords.Length == VoxelDeltaChunkDTO.DirtyMaskWordCount &&
                   value.sdfValueBits != null &&
                   value.sdfValueBits.Length == VoxelDeltaChunkDTO.CellCount &&
                   value.materialIds != null &&
                   value.materialIds.Length == VoxelDeltaChunkDTO.CellCount;
        }

        private static int ResolveVoxelDeltaTotalCellCount(VoxelDeltaChunkDTO[] chunks, int chunkCount)
        {
            if (chunks == null || chunkCount <= 0)
                return 0;

            int total = 0;
            int safeCount = Math.Min(chunkCount, chunks.Length);
            for (int i = 0; i < safeCount; i++)
            {
                VoxelDeltaChunkDTO chunk = chunks[i];
                byte storageMode = ResolveVoxelDeltaSerializedStorageMode(in chunk);
                if (storageMode == VoxelDeltaSerializedStorageUniformSdfRle)
                    total = AddVoxelDeltaCellCountClamped(total, VoxelDeltaChunkDTO.CellCount);
                else if (storageMode == VoxelDeltaSerializedStorageDense)
                    total = AddVoxelDeltaCellCountClamped(total, CountVoxelDeltaDirtyMaskBits(chunk.dirtyMaskWords));
                else
                    total = AddVoxelDeltaCellCountClamped(
                        total,
                        ClampCollectionCount(chunk.cellCount, chunk.cells, VoxelDeltaChunkDTO.CellCount));
            }

            return total;
        }

        private static int AddVoxelDeltaCellCountClamped(int current, int add)
        {
            if (add <= 0)
                return Math.Max(0, current);

            return current > int.MaxValue - add
                ? int.MaxValue
                : current + add;
        }

        private static int CountVoxelDeltaDirtyMaskBits(uint[] dirtyMaskWords)
        {
            if (dirtyMaskWords == null)
                return 0;

            int total = 0;
            int wordCount = Math.Min(dirtyMaskWords.Length, VoxelDeltaChunkDTO.DirtyMaskWordCount);
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

        private static float SanitizeVoxelSize(float value)
        {
            return math.isfinite(value) && value > 0f
                ? value
                : VoxelDeltaDefaultVoxelSize;
        }

        private static void RefreshInventoryShadowMirror(SaveData data)
        {
            if (data == null)
                return;

            SaveDataInventorySanitizer.ResolveInventoryShadowPayloadMetadata(
                data,
                out int inventoryShadowPayloadLength,
                out uint inventoryShadowPayloadHash);
            data.inventoryShadow = SaveDataInventorySanitizer.BuildInventoryShadow(
                in data.inventory,
                inventoryShadowPayloadLength,
                inventoryShadowPayloadHash,
                inventoryShadowPayloadLength > 0);
        }

        private const int EncryptedAudioLogFragmentSaveVersion = 61;
        private const int PackedNarrativeLoreSaveVersion = 62;
        private const int DataArchaeologySaveVersion = 64;
        private const int DataArchaeologyScanStateSaveVersion = 66;
        private const ushort MaxDataArchaeologyPartialProgressPermille = 999;
        private const byte MaxDataArchaeologyScanStateValue = 2;
        private const int CartographyFogSaveVersion = 67;

        private static bool WriteRadiationGrid(ref BufferWriter writer, SaveData data)
        {
            byte[] payload = data.radiationGridRle ?? Array.Empty<byte>();
            int payloadCapacity = math.min(payload.Length, SaveData.RadiationGridRleMaxBytes);
            int safeLength = math.clamp(data.radiationGridRleLength, 0, payloadCapacity);
            float radiationDose = math.isfinite(data.radiationDose) ? math.max(0f, data.radiationDose) : 0f;
            double originX = math.isfinite(data.radiationGridOriginX) ? data.radiationGridOriginX : 0d;
            double originY = math.isfinite(data.radiationGridOriginY) ? data.radiationGridOriginY : 0d;
            double originZ = math.isfinite(data.radiationGridOriginZ) ? data.radiationGridOriginZ : 0d;
            float cellSize = math.isfinite(data.radiationGridCellSizeMeters)
                ? math.clamp(data.radiationGridCellSizeMeters, RadiationGridMinCellSizeMeters, RadiationGridMaxCellSizeMeters)
                : RadiationGridDefaultCellSizeMeters;

            return writer.WriteFloat(radiationDose)
                && writer.WriteDouble(originX)
                && writer.WriteDouble(originY)
                && writer.WriteDouble(originZ)
                && writer.WriteFloat(cellSize)
                && writer.WriteInt(safeLength)
                && writer.WriteStructArraySlice(payload, safeLength);
        }

        private static bool ReadRadiationGrid(ref BufferReader reader, int version, SaveData data)
        {
            if (version < RadiationGridSaveVersion)
            {
                data.radiationDose = 0f;
                data.radiationGridOriginX = 0d;
                data.radiationGridOriginY = 0d;
                data.radiationGridOriginZ = 0d;
                data.radiationGridCellSizeMeters = RadiationGridDefaultCellSizeMeters;
                data.radiationGridRleLength = 0;
                data.radiationGridRle = Array.Empty<byte>();
                return true;
            }

            if (!reader.ReadFloat(out data.radiationDose)
                || !reader.ReadDouble(out data.radiationGridOriginX)
                || !reader.ReadDouble(out data.radiationGridOriginY)
                || !reader.ReadDouble(out data.radiationGridOriginZ)
                || !reader.ReadFloat(out data.radiationGridCellSizeMeters)
                || !reader.ReadInt(out data.radiationGridRleLength)
                || !reader.ReadStructArrayBounded(
                    out data.radiationGridRle,
                    SaveData.RadiationGridRleMaxBytes,
                    nameof(data.radiationGridRle)))
            {
                return false;
            }

            int payloadLength = data.radiationGridRle != null
                ? math.min(data.radiationGridRle.Length, SaveData.RadiationGridRleMaxBytes)
                : 0;
            data.radiationGridRleLength = math.clamp(data.radiationGridRleLength, 0, payloadLength);
            SaveData.EnsureExactArrayCapacity(ref data.radiationGridRle, SaveData.RadiationGridRleMaxBytes);
            data.radiationGridCellSizeMeters = math.isfinite(data.radiationGridCellSizeMeters)
                ? math.clamp(data.radiationGridCellSizeMeters, RadiationGridMinCellSizeMeters, RadiationGridMaxCellSizeMeters)
                : RadiationGridDefaultCellSizeMeters;
            data.radiationDose = math.isfinite(data.radiationDose) ? math.max(0f, data.radiationDose) : 0f;
            data.radiationGridOriginX = math.isfinite(data.radiationGridOriginX) ? data.radiationGridOriginX : 0d;
            data.radiationGridOriginY = math.isfinite(data.radiationGridOriginY) ? data.radiationGridOriginY : 0d;
            data.radiationGridOriginZ = math.isfinite(data.radiationGridOriginZ) ? data.radiationGridOriginZ : 0d;
            return true;
        }

        private static bool WriteRtgDecay(ref BufferWriter writer, SaveData data)
        {
            int sourceLength = data?.rtgDecaySourceIds != null ? data.rtgDecaySourceIds.Length : 0;
            int safeCount = data != null
                ? math.clamp(data.rtgDecayCount, 0, math.min(SaveData.MaxRtgDecayRecords, sourceLength))
                : 0;
            int[] sourceIds = data != null && data.rtgDecaySourceIds != null
                ? data.rtgDecaySourceIds
                : Array.Empty<int>();
            double[] startTimes = data != null && data.rtgStartTimesSeconds != null
                ? data.rtgStartTimesSeconds
                : Array.Empty<double>();
            byte[] flags = data != null && data.rtgDecayFlags != null
                ? data.rtgDecayFlags
                : Array.Empty<byte>();

            return writer.WriteInt(safeCount)
                && WriteNonNegativeIntArraySlice(
                    ref writer,
                    sourceIds,
                    safeCount,
                    SaveData.MaxRtgDecayRecords)
                && WriteNonNegativeDoubleArraySlice(
                    ref writer,
                    startTimes,
                    safeCount,
                    SaveData.MaxRtgDecayRecords)
                && WriteMaskedByteArraySlice(
                    ref writer,
                    flags,
                    safeCount,
                    SaveData.MaxRtgDecayRecords,
                    SaveData.RtgDecayPersistedFlagMask);
        }

        private static bool ReadRtgDecay(ref BufferReader reader, int version, SaveData data)
        {
            if (version < RtgDecaySaveVersion)
            {
                data.rtgDecayCount = 0;
                data.EnsureRtgDecayCapacity();
                return true;
            }

            if (!reader.ReadInt(out data.rtgDecayCount)
                || !reader.ReadStructArrayBounded(
                    out data.rtgDecaySourceIds,
                    SaveData.MaxRtgDecayRecords,
                    nameof(data.rtgDecaySourceIds))
                || !reader.ReadStructArrayBounded(
                    out data.rtgStartTimesSeconds,
                    SaveData.MaxRtgDecayRecords,
                    nameof(data.rtgStartTimesSeconds))
                || !reader.ReadStructArrayBounded(
                    out data.rtgDecayFlags,
                    SaveData.MaxRtgDecayRecords,
                    nameof(data.rtgDecayFlags)))
            {
                return false;
            }

            int sourceLength = data.rtgDecaySourceIds != null ? data.rtgDecaySourceIds.Length : 0;
            data.rtgDecayCount = math.clamp(
                data.rtgDecayCount,
                0,
                math.min(SaveData.MaxRtgDecayRecords, sourceLength));
            data.EnsureRtgDecayCapacity();
            SanitizeRtgDecay(data);
            return true;
        }

        private static void SanitizeRtgDecay(SaveData data)
        {
            if (data == null)
                return;

            int safeCount = data.rtgDecayCount;
            for (int i = 0; i < safeCount; i++)
            {
                data.rtgDecaySourceIds[i] = Math.Max(0, data.rtgDecaySourceIds[i]);
                data.rtgStartTimesSeconds[i] = math.isfinite(data.rtgStartTimesSeconds[i])
                    ? math.max(0d, data.rtgStartTimesSeconds[i])
                    : 0d;
                data.rtgDecayFlags[i] = (byte)(data.rtgDecayFlags[i] & SaveData.RtgDecayPersistedFlagMask);
            }
        }

        private static bool ReadNarrativeAupTriggeredMask(
            ref BufferReader reader,
            int saveDataVersion,
            out ulong triggeredMask)
        {
            triggeredMask = 0UL;
            return saveDataVersion < PackedNarrativeLoreSaveVersion ||
                   reader.ReadStruct(out triggeredMask);
        }

        private static bool ReadSuitUpgradeMask(
            ref BufferReader reader,
            int saveDataVersion,
            out ulong upgradeMask)
        {
            upgradeMask = 0UL;
            if (saveDataVersion < SuitUpgradeMaskSaveVersion)
                return true;

            if (!reader.ReadStruct(out upgradeMask))
                return false;

            upgradeMask = SanitizeSuitUpgradeMask(upgradeMask);
            return true;
        }

        private static bool WriteAtlas6Liability(ref BufferWriter writer, SaveData data)
        {
            int safeWorkerTagCount = 0;
            uint[] workerTagHashes = data != null ? data.atlas6LiabilityRecoveredWorkerTagHashes : null;
            float pressureSealIntegrity = data != null ? data.atlas6LiabilityPressureSealIntegrity : 1f;
            if (data != null)
            {
                safeWorkerTagCount = Math.Clamp(
                    data.atlas6LiabilityRecoveredWorkerTagCount,
                    0,
                    Math.Min(
                        SaveData.MaxAtlas6LiabilityWorkerTags,
                        workerTagHashes != null ? workerTagHashes.Length : 0));
            }

            return writer.WriteFloat(SanitizeNonNegativeFloat(
                    data != null ? data.atlas6LiabilitySectorXenonOmegaYield : 0f,
                    SaveData.Atlas6LiabilityMaxTrackedSectorYield,
                    0f))
                && writer.WriteBool(data != null && data.atlas6LiabilityHasDisasterEvidence)
                && writer.WriteInt(safeWorkerTagCount)
                && WriteAtlas6WorkerTagHashes(ref writer, workerTagHashes, safeWorkerTagCount)
                && writer.WriteFloat(SanitizeNonNegativeFloat(
                    data != null ? data.atlas6LiabilityCorporateHostilityIndex : 0f,
                    float.MaxValue,
                    0f))
                && writer.WriteFloat(SanitizeNonNegativeFloat(
                    data != null ? data.atlas6LiabilityCorporateCreditBalance : 0f,
                    float.MaxValue,
                    0f))
                && writer.WriteInt(SanitizeAtlas6LiabilityCarrierState(
                    data != null ? data.atlas6LiabilityExtractionCarrierState : 0))
                && writer.WriteFloat(SanitizeNonNegativeFloat(
                    data != null ? data.atlas6LiabilityBiomatterExposureLevel : 0f,
                    SaveData.Atlas6LiabilityMaxBiomatterExposure,
                    0f))
                && writer.WriteBool(data != null && data.atlas6LiabilityHaldaneLockoutActive)
                && writer.WriteFloat(math.isfinite(pressureSealIntegrity)
                    ? math.saturate(pressureSealIntegrity)
                    : 1f)
                && writer.WriteBool(data != null && data.atlas6LiabilityBulkheadLocked);
        }

        private static bool ReadAtlas6Liability(ref BufferReader reader, int saveDataVersion, SaveData data)
        {
            if (data == null)
                return false;

            ResetAtlas6LiabilitySaveData(data);
            if (saveDataVersion < SaveData.Atlas6LiabilityPersistenceVersion)
                return true;

            if (!reader.ReadFloat(out data.atlas6LiabilitySectorXenonOmegaYield) ||
                !reader.ReadBool(out data.atlas6LiabilityHasDisasterEvidence) ||
                !reader.ReadInt(out int recoveredWorkerTagCount) ||
                !ReadAtlas6WorkerTagHashes(
                    ref reader,
                    data.atlas6LiabilityRecoveredWorkerTagHashes,
                    recoveredWorkerTagCount) ||
                !reader.ReadFloat(out data.atlas6LiabilityCorporateHostilityIndex) ||
                !reader.ReadFloat(out data.atlas6LiabilityCorporateCreditBalance) ||
                !reader.ReadInt(out data.atlas6LiabilityExtractionCarrierState) ||
                !reader.ReadFloat(out data.atlas6LiabilityBiomatterExposureLevel) ||
                !reader.ReadBool(out data.atlas6LiabilityHaldaneLockoutActive) ||
                !reader.ReadFloat(out data.atlas6LiabilityPressureSealIntegrity) ||
                !reader.ReadBool(out data.atlas6LiabilityBulkheadLocked))
            {
                return false;
            }

            SaveData.EnsureExactArrayCapacity(
                ref data.atlas6LiabilityRecoveredWorkerTagHashes,
                SaveData.MaxAtlas6LiabilityWorkerTags);
            data.atlas6LiabilityRecoveredWorkerTagCount = Math.Clamp(
                recoveredWorkerTagCount,
                0,
                SaveData.MaxAtlas6LiabilityWorkerTags);
            data.atlas6LiabilitySectorXenonOmegaYield = SanitizeNonNegativeFloat(
                data.atlas6LiabilitySectorXenonOmegaYield,
                SaveData.Atlas6LiabilityMaxTrackedSectorYield,
                0f);
            data.atlas6LiabilityCorporateHostilityIndex = SanitizeNonNegativeFloat(
                data.atlas6LiabilityCorporateHostilityIndex,
                float.MaxValue,
                0f);
            data.atlas6LiabilityCorporateCreditBalance = SanitizeNonNegativeFloat(
                data.atlas6LiabilityCorporateCreditBalance,
                float.MaxValue,
                0f);
            data.atlas6LiabilityExtractionCarrierState =
                SanitizeAtlas6LiabilityCarrierState(data.atlas6LiabilityExtractionCarrierState);
            data.atlas6LiabilityBiomatterExposureLevel = SanitizeNonNegativeFloat(
                data.atlas6LiabilityBiomatterExposureLevel,
                SaveData.Atlas6LiabilityMaxBiomatterExposure,
                0f);
            data.atlas6LiabilityPressureSealIntegrity = math.isfinite(data.atlas6LiabilityPressureSealIntegrity)
                ? math.saturate(data.atlas6LiabilityPressureSealIntegrity)
                : 1f;

            return true;
        }

        private static bool WriteAtlas6WorkerTagHashes(ref BufferWriter writer, uint[] workerTagHashes, int count)
        {
            if (count <= 0)
                return true;

            if (workerTagHashes == null || workerTagHashes.Length < count)
            {
                writer.Error = "Atlas6 worker-tag hash payload is shorter than its logical count.";
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                if (!writer.WriteUInt(workerTagHashes[i]))
                    return false;
            }

            return true;
        }

        private static bool ReadAtlas6WorkerTagHashes(ref BufferReader reader, uint[] destination, int count)
        {
            if (count < 0 || count > SaveData.MaxAtlas6LiabilityWorkerTags)
            {
                reader.SetError("Atlas6 worker-tag hash count exceeds the supported range.");
                return false;
            }

            if (destination == null || destination.Length < SaveData.MaxAtlas6LiabilityWorkerTags)
            {
                reader.SetError("Atlas6 worker-tag hash destination is not initialized.");
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                if (!reader.ReadUInt(out destination[i]))
                    return false;
            }

            return true;
        }

        private static void ResetAtlas6LiabilitySaveData(SaveData data)
        {
            data.atlas6LiabilitySectorXenonOmegaYield = 0f;
            data.atlas6LiabilityHasDisasterEvidence = false;
            data.atlas6LiabilityRecoveredWorkerTagCount = 0;
            SaveData.EnsureExactArrayCapacity(
                ref data.atlas6LiabilityRecoveredWorkerTagHashes,
                SaveData.MaxAtlas6LiabilityWorkerTags);
            Array.Clear(
                data.atlas6LiabilityRecoveredWorkerTagHashes,
                0,
                data.atlas6LiabilityRecoveredWorkerTagHashes.Length);
            data.atlas6LiabilityCorporateHostilityIndex = 0f;
            data.atlas6LiabilityCorporateCreditBalance = 5000f;
            data.atlas6LiabilityExtractionCarrierState = 0;
            data.atlas6LiabilityBiomatterExposureLevel = 0f;
            data.atlas6LiabilityHaldaneLockoutActive = false;
            data.atlas6LiabilityPressureSealIntegrity = 1f;
            data.atlas6LiabilityBulkheadLocked = false;
        }

        private static int SanitizeAtlas6LiabilityCarrierState(int carrierState)
        {
            return carrierState >= 0 && carrierState <= 4 ? carrierState : 0;
        }

        private static int SanitizeAtlas6PlayerStatus(int playerStatus)
        {
            return playerStatus >= 0 && playerStatus <= 5 ? playerStatus : 0;
        }

        private static int SanitizeLodQualityPreset(int preset)
        {
            return preset >= 0 && preset <= 2 ? preset : 1;
        }

        private static int SanitizeFirstHourMilestones(int milestones)
        {
            return milestones >= 0 ? milestones & FirstHourKnownMilestoneMask : 0;
        }

        private static int SanitizeFirstHourGuidanceFlags(int guidanceFlags)
        {
            return guidanceFlags >= 0 ? guidanceFlags & FirstHourKnownGuidanceMask : 0;
        }

        private static int SanitizeEndingChoice(int endingChoice)
        {
            return endingChoice >= 0 && endingChoice <= 3 ? endingChoice : 0;
        }

        private static ulong SanitizeSuitUpgradeMask(ulong upgradeMask)
        {
            return upgradeMask & SaveData.SuitUpgradeSupportedMask;
        }

        private static int NormalizeLastDiscoveredBiomeId(
            int lastDiscoveredBiomeId,
            HashSet<int> discoveredBiomeIds,
            long[] discoveredBiomeBitWords)
        {
            if (BiomeDiscoveryBitMask.IsValidBiomeId(lastDiscoveredBiomeId) &&
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

            return BiomeDiscoveryBitMask.InvalidBiomeId;
        }

        private static float SanitizeNonNegativeFloat(float value, float maxValue, float fallback)
        {
            if (!math.isfinite(value))
                return fallback;

            return math.clamp(value, 0f, maxValue);
        }

        private static bool WriteDataArchaeology(ref BufferWriter writer, SaveData data)
        {
            long[] words = data != null ? data.dataArchaeologyDiscoveryBitWords : null;
            if (!HasExpectedDataArchaeologyDiscoveryCapacity(words))
            {
                for (int i = 0; i < SaveData.MaxDataArchaeologyDiscoveryWords; i++)
                {
                    if (!writer.WriteLong(0L))
                        return false;
                }
            }
            else
            {
                for (int i = 0; i < SaveData.MaxDataArchaeologyDiscoveryWords; i++)
                {
                    if (!writer.WriteLong(words[i]))
                        return false;
                }
            }

            int safeCount = 0;
            if (data != null)
            {
                safeCount = Math.Clamp(
                    data.dataArchaeologyPartialScanCount,
                    0,
                    Math.Min(
                        SaveData.MaxDataArchaeologyPartialScans,
                        Math.Min(
                            data.dataArchaeologyPartialScanHashes != null ? data.dataArchaeologyPartialScanHashes.Length : 0,
                            data.dataArchaeologyPartialScanProgressPermille != null ? data.dataArchaeologyPartialScanProgressPermille.Length : 0)));
            }

            return writer.WriteInt(safeCount)
                && writer.WriteStructArraySlice(data != null ? data.dataArchaeologyPartialScanHashes : null, safeCount)
                && WriteDataArchaeologyPartialProgress(
                    ref writer,
                    data != null ? data.dataArchaeologyPartialScanProgressPermille : null,
                    safeCount)
                && WriteDataArchaeologyScanStates(ref writer, data);
        }

        private static bool ReadDataArchaeology(ref BufferReader reader, int saveDataVersion, SaveData data)
        {
            if (data == null)
                return false;

            EnsureDataArchaeologyDiscoveryCapacity(ref data.dataArchaeologyDiscoveryBitWords);
            ClearDataArchaeologyDiscoveryWords(data.dataArchaeologyDiscoveryBitWords);
            data.dataArchaeologyPartialScanCount = 0;
            SaveData.EnsureExactArrayCapacity(
                ref data.dataArchaeologyPartialScanHashes,
                SaveData.MaxDataArchaeologyPartialScans);
            SaveData.EnsureExactArrayCapacity(
                ref data.dataArchaeologyPartialScanProgressPermille,
                SaveData.MaxDataArchaeologyPartialScans);
            data.dataArchaeologyScanStateCount = 0;
            SaveData.EnsureExactArrayCapacity(
                ref data.dataArchaeologyScanStateKeys,
                SaveData.MaxDataArchaeologyScanStates);
            SaveData.EnsureExactArrayCapacity(
                ref data.dataArchaeologyScanStateValues,
                SaveData.MaxDataArchaeologyScanStates);

            Array.Clear(data.dataArchaeologyPartialScanHashes, 0, data.dataArchaeologyPartialScanHashes.Length);
            Array.Clear(data.dataArchaeologyPartialScanProgressPermille, 0, data.dataArchaeologyPartialScanProgressPermille.Length);
            Array.Clear(data.dataArchaeologyScanStateKeys, 0, data.dataArchaeologyScanStateKeys.Length);
            Array.Clear(data.dataArchaeologyScanStateValues, 0, data.dataArchaeologyScanStateValues.Length);

            if (saveDataVersion < DataArchaeologySaveVersion)
                return true;

            for (int i = 0; i < SaveData.MaxDataArchaeologyDiscoveryWords; i++)
            {
                if (!reader.ReadLong(out data.dataArchaeologyDiscoveryBitWords[i]))
                    return false;
            }

            if (!reader.ReadInt(out int count) ||
                !reader.ReadStructArrayBounded(
                    out uint[] partialHashes,
                    SaveData.MaxDataArchaeologyPartialScans,
                    nameof(data.dataArchaeologyPartialScanHashes)) ||
                !reader.ReadStructArrayBounded(
                    out ushort[] partialProgress,
                    SaveData.MaxDataArchaeologyPartialScans,
                    nameof(data.dataArchaeologyPartialScanProgressPermille)))
            {
                return false;
            }

            int safeCount = Math.Clamp(
                count,
                0,
                Math.Min(
                    SaveData.MaxDataArchaeologyPartialScans,
                    Math.Min(partialHashes != null ? partialHashes.Length : 0, partialProgress != null ? partialProgress.Length : 0)));

            data.dataArchaeologyPartialScanCount = safeCount;
            for (int i = 0; i < safeCount; i++)
            {
                data.dataArchaeologyPartialScanHashes[i] = partialHashes[i];
                data.dataArchaeologyPartialScanProgressPermille[i] =
                    SanitizeDataArchaeologyPartialProgress(partialProgress[i]);
            }

            return ReadDataArchaeologyScanStates(ref reader, saveDataVersion, data);
        }

        private static bool HasExpectedDataArchaeologyDiscoveryCapacity(long[] words)
        {
            return words != null && words.Length == SaveData.MaxDataArchaeologyDiscoveryWords;
        }

        private static void EnsureDataArchaeologyDiscoveryCapacity(ref long[] words)
        {
            SaveData.EnsureExactArrayCapacity(ref words, SaveData.MaxDataArchaeologyDiscoveryWords);
        }

        private static void ClearDataArchaeologyDiscoveryWords(long[] words)
        {
            if (words == null)
                return;

            for (int i = 0; i < words.Length; i++)
                words[i] = 0L;
        }

        private static bool WriteDataArchaeologyPartialProgress(
            ref BufferWriter writer,
            ushort[] values,
            int count)
        {
            if (values == null)
                return writer.WriteInt(NullCollectionCount);

            int safeCount = Math.Clamp(count, 0, Math.Min(values.Length, SaveData.MaxDataArchaeologyPartialScans));
            if (!writer.WriteInt(safeCount))
                return false;

            for (int i = 0; i < safeCount; i++)
            {
                ushort safeProgress = SanitizeDataArchaeologyPartialProgress(values[i]);
                if (!writer.WriteStruct(safeProgress))
                    return false;
            }

            return true;
        }

        private static ushort SanitizeDataArchaeologyPartialProgress(ushort value)
        {
            return (ushort)Math.Min(value, MaxDataArchaeologyPartialProgressPermille);
        }

        private static bool WriteDataArchaeologyScanStates(ref BufferWriter writer, SaveData data)
        {
            int safeCount = 0;
            if (data != null)
            {
                safeCount = Math.Clamp(
                    data.dataArchaeologyScanStateCount,
                    0,
                    Math.Min(
                        SaveData.MaxDataArchaeologyScanStates,
                        Math.Min(
                            data.dataArchaeologyScanStateKeys != null ? data.dataArchaeologyScanStateKeys.Length : 0,
                            data.dataArchaeologyScanStateValues != null ? data.dataArchaeologyScanStateValues.Length : 0)));
            }

            return writer.WriteInt(safeCount)
                && writer.WriteStructArraySlice(data != null ? data.dataArchaeologyScanStateKeys : null, safeCount)
                && WriteDataArchaeologyScanStateValues(
                    ref writer,
                    data != null ? data.dataArchaeologyScanStateValues : null,
                    safeCount);
        }

        private static bool ReadDataArchaeologyScanStates(ref BufferReader reader, int saveDataVersion, SaveData data)
        {
            if (saveDataVersion < DataArchaeologyScanStateSaveVersion)
                return true;

            if (!reader.ReadInt(out int count) ||
                !reader.ReadStructArrayBounded(
                    out int[] keys,
                    SaveData.MaxDataArchaeologyScanStates,
                    nameof(data.dataArchaeologyScanStateKeys)) ||
                !reader.ReadStructArrayBounded(
                    out byte[] values,
                    SaveData.MaxDataArchaeologyScanStates,
                    nameof(data.dataArchaeologyScanStateValues)))
            {
                return false;
            }

            int safeCount = Math.Clamp(
                count,
                0,
                Math.Min(
                    SaveData.MaxDataArchaeologyScanStates,
                    Math.Min(keys != null ? keys.Length : 0, values != null ? values.Length : 0)));

            data.dataArchaeologyScanStateCount = safeCount;
            for (int i = 0; i < safeCount; i++)
            {
                data.dataArchaeologyScanStateKeys[i] = keys[i];
                data.dataArchaeologyScanStateValues[i] = SanitizeDataArchaeologyScanStateValue(values[i]);
            }

            return true;
        }

        private static bool WriteDataArchaeologyScanStateValues(
            ref BufferWriter writer,
            byte[] values,
            int count)
        {
            if (values == null)
                return writer.WriteInt(NullCollectionCount);

            int safeCount = Math.Clamp(count, 0, Math.Min(values.Length, SaveData.MaxDataArchaeologyScanStates));
            if (!writer.WriteInt(safeCount))
                return false;

            for (int i = 0; i < safeCount; i++)
            {
                byte safeValue = SanitizeDataArchaeologyScanStateValue(values[i]);
                if (!writer.WriteByte(safeValue))
                    return false;
            }

            return true;
        }

        private static byte SanitizeDataArchaeologyScanStateValue(byte value)
        {
            return value <= MaxDataArchaeologyScanStateValue ? value : (byte)0;
        }

        private static bool WriteAudioLogDiscoveryBitWords(ref BufferWriter writer, SaveData data)
        {
            long[] words = data != null ? data.audioLogDiscoveryBitWords : null;
            if (!AudioLogDiscoveryBitMask.HasExpectedCapacity(words))
            {
                for (int i = 0; i < AudioLogDiscoveryBitMask.WordCount; i++)
                {
                    if (!writer.WriteLong(0L))
                        return false;
                }

                return true;
            }

            for (int i = 0; i < AudioLogDiscoveryBitMask.WordCount; i++)
            {
                if (!writer.WriteLong(words[i]))
                    return false;
            }

            return true;
        }

        private static bool ReadAudioLogDiscoveryBitWords(
            ref BufferReader reader,
            int saveDataVersion,
            SaveData data)
        {
            if (data == null)
                return false;

            AudioLogDiscoveryBitMask.EnsureCapacity(ref data.audioLogDiscoveryBitWords);
            AudioLogDiscoveryBitMask.Clear(data.audioLogDiscoveryBitWords);
            if (saveDataVersion < PackedNarrativeLoreSaveVersion)
                return true;

            for (int i = 0; i < AudioLogDiscoveryBitMask.WordCount; i++)
            {
                if (!reader.ReadLong(out data.audioLogDiscoveryBitWords[i]))
                    return false;
            }

            return true;
        }

        private static bool WriteEncryptedAudioLogFragments(ref BufferWriter writer, SaveData data)
        {
            uint[] emptyValues = Array.Empty<uint>();
            if (data == null)
                return writer.WriteInt(0)
                    && writer.WriteStructArraySlice(emptyValues, 0)
                    && writer.WriteStructArraySlice(emptyValues, 0);

            uint[] hashes = data.audioLogEncryptedFragmentHashes ?? emptyValues;
            uint[] bits = data.audioLogEncryptedFragmentBits ?? emptyValues;
            int safeCount = Math.Clamp(
                data.audioLogEncryptedFragmentCount,
                0,
                Math.Min(
                    SaveData.MaxEncryptedAudioLogFragments,
                    Math.Min(hashes.Length, bits.Length)));

            return writer.WriteInt(safeCount)
                && writer.WriteStructArraySlice(hashes, safeCount)
                && writer.WriteStructArraySlice(bits, safeCount);
        }

        private static bool ReadEncryptedAudioLogFragments(ref BufferReader reader, int saveDataVersion, SaveData data)
        {
            if (data == null)
                return false;

            if (saveDataVersion < EncryptedAudioLogFragmentSaveVersion)
            {
                data.audioLogEncryptedFragmentCount = 0;
                SaveData.EnsureExactArrayCapacity(
                    ref data.audioLogEncryptedFragmentHashes,
                    SaveData.MaxEncryptedAudioLogFragments);
                SaveData.EnsureExactArrayCapacity(
                    ref data.audioLogEncryptedFragmentBits,
                    SaveData.MaxEncryptedAudioLogFragments);
                Array.Clear(data.audioLogEncryptedFragmentHashes, 0, data.audioLogEncryptedFragmentHashes.Length);
                Array.Clear(data.audioLogEncryptedFragmentBits, 0, data.audioLogEncryptedFragmentBits.Length);
                return true;
            }

            if (!reader.ReadInt(out int count) ||
                !reader.ReadStructArrayBounded(
                    out data.audioLogEncryptedFragmentHashes,
                    SaveData.MaxEncryptedAudioLogFragments,
                    nameof(data.audioLogEncryptedFragmentHashes)) ||
                !reader.ReadStructArrayBounded(
                    out data.audioLogEncryptedFragmentBits,
                    SaveData.MaxEncryptedAudioLogFragments,
                    nameof(data.audioLogEncryptedFragmentBits)))
            {
                return false;
            }

            int hashLength = data.audioLogEncryptedFragmentHashes != null ? data.audioLogEncryptedFragmentHashes.Length : 0;
            int bitLength = data.audioLogEncryptedFragmentBits != null ? data.audioLogEncryptedFragmentBits.Length : 0;
            data.audioLogEncryptedFragmentCount = Math.Clamp(
                count,
                0,
                Math.Min(SaveData.MaxEncryptedAudioLogFragments, Math.Min(hashLength, bitLength)));
            ClearEncryptedAudioLogFragmentTail(data);
            return true;
        }

        private static void ClearEncryptedAudioLogFragmentTail(SaveData data)
        {
            if (data == null)
                return;

            int safeCount = Math.Clamp(
                data.audioLogEncryptedFragmentCount,
                0,
                SaveData.MaxEncryptedAudioLogFragments);
            int hashLength = data.audioLogEncryptedFragmentHashes != null
                ? Math.Min(data.audioLogEncryptedFragmentHashes.Length, SaveData.MaxEncryptedAudioLogFragments)
                : 0;
            int bitLength = data.audioLogEncryptedFragmentBits != null
                ? Math.Min(data.audioLogEncryptedFragmentBits.Length, SaveData.MaxEncryptedAudioLogFragments)
                : 0;

            if (data.audioLogEncryptedFragmentHashes != null && safeCount < hashLength)
                Array.Clear(data.audioLogEncryptedFragmentHashes, safeCount, hashLength - safeCount);
            if (data.audioLogEncryptedFragmentBits != null && safeCount < bitLength)
                Array.Clear(data.audioLogEncryptedFragmentBits, safeCount, bitLength - safeCount);
        }

        private static bool WriteInventory(ref BufferWriter writer, SaveData data)
        {
            int inventoryShadowPayloadLength = SaveDataInventorySanitizer.ResolveInventoryShadowPayloadLength(data);
            if (data != null && inventoryShadowPayloadLength > 0)
            {
                return writer.WriteManagedBytes(data.inventoryShadowPayload, inventoryShadowPayloadLength);
            }

            return WriteInventory(ref writer, data != null ? data.inventory : default);
        }

        private static bool WriteInventory(ref BufferWriter writer, InventoryDTO value)
        {
            SaveDataInventorySanitizer.SanitizeInventory(ref value);
            int logicalCellCount = Math.Clamp(value.cellCount, 0, InventoryDTO.MaxCells);
            return writer.WriteInt(logicalCellCount)
                && writer.WriteStructArraySlice(value.itemHashIds, logicalCellCount)
                && writer.WriteStructArraySlice(value.packedCellCoordinates, logicalCellCount)
                && writer.WriteStructArraySlice(value.stackCounts, logicalCellCount)
                && writer.WriteStructArraySlice(value.itemStateFlags, logicalCellCount)
                && writer.WriteStructArraySlice(value.itemGeneticsWords, logicalCellCount)
                && writer.WriteStructArraySlice(value.qualityMilli, logicalCellCount)
                && writer.WriteStructArraySlice(value.lastUpdateUnixSeconds, logicalCellCount)
                && writer.WriteStructArraySlice(
                    value.itemDurabilityRle,
                    Math.Clamp(value.itemDurabilityRleLength, 0, value.itemDurabilityRle != null ? value.itemDurabilityRle.Length : 0))
                && writer.WriteFloat(value.totalWeight)
                && writer.WriteInt(value.gridColumns)
                && writer.WriteInt(value.gridRows);
        }

        private static bool WriteExternalScavengerSites(ref BufferWriter writer, ExternalScavengerSiteDTO[] value)
        {
            int sourceCount = value != null ? Math.Min(value.Length, SaveData.MaxExternalScavengerSites) : 0;
            int safeCount = 0;
            for (int i = 0; i < sourceCount; i++)
            {
                if (ExternalScavengerSiteDTO.TrySanitizeForPersistence(in value[i], out _))
                    safeCount++;
            }

            if (!writer.WriteInt(safeCount))
                return false;

            for (int i = 0; i < sourceCount; i++)
            {
                if (!ExternalScavengerSiteDTO.TrySanitizeForPersistence(
                        in value[i],
                        out ExternalScavengerSiteDTO safeValue))
                    continue;

                if (!writer.WriteStruct(safeValue))
                    return false;
            }

            return true;
        }

        private static bool WriteHazardZoneRuntime(ref BufferWriter writer, HazardZoneRuntimeDTO value)
        {
            SanitizeHazardZoneRuntime(ref value);
            return writer.WriteFloat(value.toxicityDose)
                && writer.WriteFloat(value.toxicityPulseAccumulatorSeconds);
        }

        private static bool WritePlayerStats(ref BufferWriter writer, PlayerStatsDTO value)
        {
            SaveDataPlayerSurvivalSanitizer.SanitizePlayerStats(ref value);
            return writer.WriteFloat(value.oxygen)
                && writer.WriteFloat(value.energy)
                && writer.WriteFloat(value.integrity)
                && writer.WriteFloat(value.health)
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
                && writer.WriteFloat(value.nitrogenBuildUp)
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

            bool read = reader.ReadFloat(out value.oxygen)
                && reader.ReadFloat(out value.energy)
                && reader.ReadFloat(out value.integrity)
                && ReadPlayerHealth(ref reader, version, ref value)
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
                && ReadNitrogenBuildUp(ref reader, version, ref value)
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
            if (!read)
                return false;

            SaveDataPlayerSurvivalSanitizer.SanitizePlayerStats(ref value);
            return true;
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

        private static bool ReadPlayerHealth(ref BufferReader reader, int version, ref PlayerStatsDTO value)
        {
            if (version < SaveData.PlayerHealthPersistenceVersion)
            {
                value.health = SaveData.PlayerHealthDefault;
                return true;
            }

            return reader.ReadFloat(out value.health);
        }

        private static bool ReadNitrogenBuildUp(ref BufferReader reader, int version, ref PlayerStatsDTO value)
        {
            if (version < 57)
            {
                value.nitrogenBuildUp = 0f;
                return true;
            }

            return reader.ReadFloat(out value.nitrogenBuildUp);
        }

        private static bool ReadTotalPlayTime(ref BufferReader reader, int version, out double value)
        {
            value = 0d;
            if (version >= 39)
            {
                if (!reader.ReadDouble(out value))
                    return false;

                value = SanitizeNonNegativeFinite(value);
                return true;
            }

            if (!reader.ReadFloat(out float legacyValue))
                return false;

            value = SanitizeNonNegativeFinite(legacyValue);
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

        private static float SanitizeNonNegativeFinite(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        private static double SanitizeNonNegativeFinite(double value)
        {
            return math.isfinite(value) ? math.max(0d, value) : 0d;
        }

        private static void SanitizeNonNegativeFiniteList(List<float> values)
        {
            if (values == null)
                return;

            for (int i = 0; i < values.Count; i++)
                values[i] = SanitizeNonNegativeFinite(values[i]);
        }

        private static void TrimListToCount<T>(List<T> values, int count)
        {
            if (values == null)
                return;

            int safeCount = Math.Clamp(count, 0, values.Count);
            if (values.Count > safeCount)
                values.RemoveRange(safeCount, values.Count - safeCount);
        }

        private static void CompactNonBlankStringList(List<string> values, int maxCount)
        {
            if (values == null)
                return;

            int bound = ClampListCount(values, maxCount);
            int writeIndex = 0;
            for (int i = 0; i < bound; i++)
            {
                string value = SaveData.SanitizePersistenceString(values[i]);
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                if (writeIndex != i || !string.Equals(values[writeIndex], value, StringComparison.Ordinal))
                    values[writeIndex] = value;

                writeIndex++;
            }

            if (values.Count > writeIndex)
                values.RemoveRange(writeIndex, values.Count - writeIndex);
        }

        private static void CompactNonBlankStringFloatPairs(List<string> ids, List<float> values, int maxCount)
        {
            if (ids == null || values == null)
                return;

            int bound = ClampPairedListCount(ids, values, maxCount);
            int writeIndex = 0;
            for (int i = 0; i < bound; i++)
            {
                string id = SaveData.SanitizePersistenceString(ids[i]);
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                if (writeIndex != i ||
                    !string.Equals(ids[writeIndex], id, StringComparison.Ordinal))
                {
                    ids[writeIndex] = id;
                    values[writeIndex] = values[i];
                }

                writeIndex++;
            }

            if (ids.Count > writeIndex)
                ids.RemoveRange(writeIndex, ids.Count - writeIndex);
            if (values.Count > writeIndex)
                values.RemoveRange(writeIndex, values.Count - writeIndex);
        }

        private static bool ReadInventory(ref BufferReader reader, int version, out InventoryDTO value)
        {
            value = default;
            if (!reader.ReadInt(out value.cellCount))
                return false;

            if (version >= 40)
            {
                bool ok = reader.ReadStructArrayBounded(out value.itemHashIds, InventoryDTO.MaxCells, nameof(value.itemHashIds))
                    && reader.ReadStructArrayBounded(
                        out value.packedCellCoordinates,
                        InventoryDTO.MaxCells,
                        nameof(value.packedCellCoordinates))
                    && reader.ReadStructArrayBounded(out value.stackCounts, InventoryDTO.MaxCells, nameof(value.stackCounts))
                    && ReadInventoryStateArrays(ref reader, version, ref value)
                    && reader.ReadFloat(out value.totalWeight)
                    && reader.ReadInt(out value.gridColumns)
                    && reader.ReadInt(out value.gridRows);

                if (!ok)
                    return false;

                SaveDataInventorySanitizer.SanitizeInventory(ref value);
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

            int writeIndex = 0;
            for (int i = 0; i < safeCount; i++)
            {
                InventoryCellDTO legacyCell = legacyCells[i];
                string itemId = SaveData.SanitizePersistenceString(legacyCell.itemId);
                if (itemId.Length == 0)
                    continue;

                value.itemHashIds[writeIndex] = LocHash.Compute(itemId);
                value.packedCellCoordinates[writeIndex] = InventoryDTO.PackCellCoordinate(legacyCell.x, legacyCell.y);
                value.stackCounts[writeIndex] = (ushort)Math.Clamp(legacyCell.stackCount > 0 ? legacyCell.stackCount : 1, 1, ushort.MaxValue);
                value.qualityMilli[writeIndex] = DefaultQualityMilli;
                writeIndex++;
            }

            value.cellCount = writeIndex;
            SaveDataInventorySanitizer.SanitizeInventory(ref value);
            return true;
        }

        private static bool ReadInventoryStateArrays(ref BufferReader reader, int version, ref InventoryDTO value)
        {
            if (version >= 59)
            {
                bool ok = reader.ReadStructArrayBounded(out value.itemStateFlags, InventoryDTO.MaxCells, nameof(value.itemStateFlags))
                    && reader.ReadStructArrayBounded(
                        out value.itemGeneticsWords,
                        InventoryDTO.MaxCells,
                        nameof(value.itemGeneticsWords))
                    && reader.ReadStructArrayBounded(out value.qualityMilli, InventoryDTO.MaxCells, nameof(value.qualityMilli))
                    && reader.ReadStructArrayBounded(
                        out value.lastUpdateUnixSeconds,
                        InventoryDTO.MaxCells,
                        nameof(value.lastUpdateUnixSeconds));

                if (!ok)
                    return false;

                if (version >= 69)
                {
                    if (!reader.ReadStructArrayBounded(
                        out value.itemDurabilityRle,
                        InventoryDTO.MaxDurabilityRleBytes,
                        nameof(value.itemDurabilityRle)))
                        return false;

                    value.itemDurabilityRleLength = value.itemDurabilityRle != null
                        ? Math.Clamp(value.itemDurabilityRle.Length, 0, InventoryDTO.MaxDurabilityRleBytes)
                        : 0;
                }

                return true;
            }

            if (version >= 53)
            {
                return reader.ReadStructArrayBounded(out value.itemStateFlags, InventoryDTO.MaxCells, nameof(value.itemStateFlags))
                    && ReadLegacyUInt64ArrayAsByte(ref reader, out value.itemGeneticsWords, InventoryDTO.MaxCells)
                    && reader.ReadStructArrayBounded(out value.qualityMilli, InventoryDTO.MaxCells, nameof(value.qualityMilli))
                    && reader.ReadStructArrayBounded(
                        out value.lastUpdateUnixSeconds,
                        InventoryDTO.MaxCells,
                        nameof(value.lastUpdateUnixSeconds));
            }

            if (version >= 48)
            {
                return reader.ReadStructArrayBounded(out value.itemStateFlags, InventoryDTO.MaxCells, nameof(value.itemStateFlags))
                    && ReadLegacyUInt32ArrayAsByte(ref reader, out value.itemGeneticsWords, InventoryDTO.MaxCells)
                    && reader.ReadStructArrayBounded(out value.qualityMilli, InventoryDTO.MaxCells, nameof(value.qualityMilli))
                    && reader.ReadStructArrayBounded(
                        out value.lastUpdateUnixSeconds,
                        InventoryDTO.MaxCells,
                        nameof(value.lastUpdateUnixSeconds));
            }

            if (version >= 43)
            {
                return reader.ReadStructArrayBounded(out value.itemStateFlags, InventoryDTO.MaxCells, nameof(value.itemStateFlags))
                    && reader.ReadStructArrayBounded(out value.qualityMilli, InventoryDTO.MaxCells, nameof(value.qualityMilli))
                    && reader.ReadStructArrayBounded(
                        out value.lastUpdateUnixSeconds,
                        InventoryDTO.MaxCells,
                        nameof(value.lastUpdateUnixSeconds));
            }

            value.EnsureCapacity();
            int safeCount = Math.Min(
                value.cellCount,
                Math.Min(value.itemHashIds != null ? value.itemHashIds.Length : 0,
                    Math.Min(value.packedCellCoordinates != null ? value.packedCellCoordinates.Length : 0, value.stackCounts != null ? value.stackCounts.Length : 0)));
            value.cellCount = safeCount;
            for (int i = 0; i < safeCount; i++)
                value.qualityMilli[i] = DefaultQualityMilli;

            SaveDataInventorySanitizer.SanitizeInventory(ref value);
            return true;
        }

        private static bool ReadLegacyUInt32ArrayAsUInt64(
            ref BufferReader reader,
            out ulong[] value,
            int maxCount)
        {
            value = null;
            if (!reader.ReadStructArrayBounded(out uint[] legacyValues, maxCount, "Legacy uint array"))
                return false;

            if (legacyValues == null)
                return true;

            if (legacyValues.Length == 0)
            {
                value = Array.Empty<ulong>();
                return true;
            }

            value = new ulong[legacyValues.Length];
            for (int i = 0; i < legacyValues.Length; i++)
                value[i] = legacyValues[i];

            return true;
        }

        private static bool ReadLegacyUInt32ArrayAsByte(
            ref BufferReader reader,
            out byte[] value,
            int maxCount)
        {
            value = null;
            if (!reader.ReadStructArrayBounded(out uint[] legacyValues, maxCount, "Legacy uint array"))
                return false;

            if (legacyValues == null)
                return true;

            if (legacyValues.Length == 0)
            {
                value = Array.Empty<byte>();
                return true;
            }

            value = new byte[legacyValues.Length];
            for (int i = 0; i < legacyValues.Length; i++)
                value[i] = CompressLegacyItemGenetics(legacyValues[i]);

            return true;
        }

        private static bool ReadLegacyUInt64ArrayAsByte(
            ref BufferReader reader,
            out byte[] value,
            int maxCount)
        {
            value = null;
            if (!reader.ReadStructArrayBounded(out ulong[] legacyValues, maxCount, "Legacy ulong array"))
                return false;

            if (legacyValues == null)
                return true;

            if (legacyValues.Length == 0)
            {
                value = Array.Empty<byte>();
                return true;
            }

            value = new byte[legacyValues.Length];
            for (int i = 0; i < legacyValues.Length; i++)
                value[i] = CompressLegacyItemGenetics(legacyValues[i]);

            return true;
        }

        private static byte CompressLegacyItemGenetics(ulong geneticsMask)
        {
            byte flags = 0;
            if ((geneticsMask & LegacyGlowGeneMask) != 0UL)
                flags |= ItemGeneticsGlowFlag;
            if ((geneticsMask & LegacyToxicGeneMask) != 0UL)
                flags |= ItemGeneticsToxicFlag;
            if ((geneticsMask & LegacyEdibleGeneMask) != 0UL)
                flags |= ItemGeneticsEdibleFlag;
            if ((geneticsMask & LegacyHarvestableGeneMask) != 0UL)
                flags |= ItemGeneticsHarvestableFlag;

            return (byte)(flags & SaveData.InventoryItemGeneticsSupportedFlagsMask);
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

            float tempFactor = MathLodApproximation.ApproxExpSignedPade33Wide40((ambientTemperature - BiologicalReferenceTemperatureCelsius) * 0.05f);
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
                float currentQuality = Math.Clamp(
                    (value.qualityMilli[i] > 0 ? value.qualityMilli[i] : DefaultQualityMilli) /
                    (float)SaveData.InventoryDefaultQualityMilli,
                    0f,
                    1f);
                float qualityDelta = elapsedSeconds * BiologicalDecayRatePerSecond * tempFactor;
                float decayedQuality = Math.Clamp(currentQuality - qualityDelta, 0f, 1f);
                value.qualityMilli[i] = (ushort)Math.Clamp(
                    (int)Math.Round(decayedQuality * SaveData.InventoryDefaultQualityMilli),
                    0,
                    SaveData.InventoryDefaultQualityMilli);
                value.lastUpdateUnixSeconds[i] = now;
            }
        }

        private static bool ReadExternalScavengerSites(ref BufferReader reader, int version, out ExternalScavengerSiteDTO[] value)
        {
            value = null;
            if (version < 42)
                return true;

            if (!reader.ReadStructArrayBounded(
                out value,
                SaveData.MaxExternalScavengerSites,
                "externalScavengerSites"))
            {
                return false;
            }

            SanitizeExternalScavengerSitesAfterRead(ref value);
            return true;
        }

        private static void SanitizeExternalScavengerSitesAfterRead(ref ExternalScavengerSiteDTO[] value)
        {
            if (value == null || value.Length == 0)
                return;

            int writeIndex = 0;
            for (int i = 0; i < value.Length; i++)
            {
                if (!ExternalScavengerSiteDTO.TrySanitizeForPersistence(
                        in value[i],
                        out ExternalScavengerSiteDTO safeValue))
                    continue;

                value[writeIndex] = safeValue;
                writeIndex++;
            }

            if (writeIndex == value.Length)
                return;

            if (writeIndex == 0)
            {
                value = Array.Empty<ExternalScavengerSiteDTO>();
                return;
            }

            Array.Resize(ref value, writeIndex);
        }

        private static bool ReadHazardZoneRuntime(ref BufferReader reader, int version, out HazardZoneRuntimeDTO value)
        {
            value = default;
            if (version < HazardZoneRuntimeSaveVersion)
                return true;

            if (!reader.ReadFloat(out value.toxicityDose)
                || !reader.ReadFloat(out value.toxicityPulseAccumulatorSeconds))
            {
                return false;
            }

            SanitizeHazardZoneRuntime(ref value);
            return true;
        }

        private static void SanitizeHazardZoneRuntime(ref HazardZoneRuntimeDTO value)
        {
            value.toxicityDose = math.isfinite(value.toxicityDose)
                ? math.clamp(value.toxicityDose, 0f, SaveData.HazardZoneMaxPersistedToxicityDose)
                : 0f;
            value.toxicityPulseAccumulatorSeconds = math.isfinite(value.toxicityPulseAccumulatorSeconds)
                ? math.clamp(value.toxicityPulseAccumulatorSeconds, 0f, SaveData.HazardZoneMaxPersistedToxicityPulseSeconds)
                : 0f;
            if (value.toxicityDose <= SaveData.HazardZoneToxicityDamageDoseThreshold)
                value.toxicityPulseAccumulatorSeconds = 0f;
        }

        private static bool WriteWorldState(ref BufferWriter writer, WorldStateDTO value)
        {
            int depletedNodeSourceCount = ClampCollectionCount(
                value.depletedCount,
                value.depletedNodeIds,
                WorldStateDTO.MaxNodes);
            int depletedNodeCount = CountNonBlankStringArraySlice(
                value.depletedNodeIds,
                depletedNodeSourceCount,
                WorldStateDTO.MaxNodes);
            int pickupChunkCount = ClampPairedCollectionCount(
                value.depletedPickupChunkCount,
                WorldStateDTO.MaxPickupChunks,
                value.depletedPickupChunkKeys,
                value.depletedPickupChunkWordStarts,
                value.depletedPickupChunkWordCounts);
            int pickupWordCount = ClampCollectionCount(
                value.depletedPickupWordCount,
                value.depletedPickupWords,
                WorldStateDTO.MaxPickupWords);

            return writer.WriteInt(depletedNodeCount)
                && WriteNonBlankStringArraySlice(ref writer, value.depletedNodeIds, depletedNodeSourceCount, WorldStateDTO.MaxNodes)
                && writer.WriteInt(pickupChunkCount)
                && writer.WriteStructArraySlice(value.depletedPickupChunkKeys, pickupChunkCount)
                && writer.WriteStructArraySlice(value.depletedPickupChunkWordStarts, pickupChunkCount)
                && writer.WriteStructArraySlice(value.depletedPickupChunkWordCounts, pickupChunkCount)
                && writer.WriteInt(pickupWordCount)
                && writer.WriteStructArraySlice(value.depletedPickupWords, pickupWordCount);
        }

        private static bool ReadWorldState(ref BufferReader reader, out WorldStateDTO value)
        {
            value = default;
            bool read = reader.ReadInt(out value.depletedCount)
                && ReadStringArray(ref reader, out value.depletedNodeIds, WorldStateDTO.MaxNodes, nameof(value.depletedNodeIds))
                && reader.ReadInt(out value.depletedPickupChunkCount)
                && reader.ReadStructArrayBounded(
                    out value.depletedPickupChunkKeys,
                    WorldStateDTO.MaxPickupChunks,
                    nameof(value.depletedPickupChunkKeys))
                && reader.ReadStructArrayBounded(
                    out value.depletedPickupChunkWordStarts,
                    WorldStateDTO.MaxPickupChunks,
                    nameof(value.depletedPickupChunkWordStarts))
                && reader.ReadStructArrayBounded(
                    out value.depletedPickupChunkWordCounts,
                    WorldStateDTO.MaxPickupChunks,
                    nameof(value.depletedPickupChunkWordCounts))
                && reader.ReadInt(out value.depletedPickupWordCount)
                && reader.ReadStructArrayBounded(
                    out value.depletedPickupWords,
                    WorldStateDTO.MaxPickupWords,
                    nameof(value.depletedPickupWords));
            if (!read)
                return false;

            SanitizeWorldStateAfterRead(ref value);
            return true;
        }

        private static void SanitizeWorldStateAfterRead(ref WorldStateDTO value)
        {
            value.depletedCount = ResolveDecodedCollectionCount(value.depletedNodeIds, WorldStateDTO.MaxNodes);
            CompactNonBlankStringArraySlice(
                value.depletedNodeIds,
                ref value.depletedCount,
                WorldStateDTO.MaxNodes);
            value.depletedPickupChunkCount = ResolveDecodedPairedCollectionCount(
                WorldStateDTO.MaxPickupChunks,
                value.depletedPickupChunkKeys,
                value.depletedPickupChunkWordStarts,
                value.depletedPickupChunkWordCounts);
            value.depletedPickupWordCount = ResolveDecodedCollectionCount(
                value.depletedPickupWords,
                WorldStateDTO.MaxPickupWords);
            value.EnsureCapacity();
        }

        private static bool WriteProceduralWorldState(ref BufferWriter writer, ProceduralWorldStateDTO value)
        {
            int suppressedCount = ClampCollectionCount(
                value.suppressedPlacementCount,
                value.suppressedPlacementKeys,
                ProceduralWorldStateDTO.MaxSuppressedPlacements);
            int faunaCount = ClampCollectionCount(
                value.faunaStateCount,
                value.faunaStates,
                ProceduralWorldStateDTO.MaxFaunaStates);
            int seamStateCount = ClampCollectionCount(
                value.geologySeamStateCount,
                value.geologySeamStates,
                ProceduralWorldStateDTO.MaxGeologySeamStates);
            int caveEntranceCount = ClampCollectionCount(
                value.geologyCaveEntranceCount,
                value.geologyCaveEntrances,
                ProceduralWorldStateDTO.MaxGeologyCaveEntrances);
            int hibernatedFaunaCount = ClampCollectionCount(
                value.hibernatedFaunaCount,
                value.hibernatedFaunaStates,
                ProceduralWorldStateDTO.MaxHibernatedFaunaStates);

            return writer.WriteInt(suppressedCount)
                && writer.WriteStructArraySlice(value.suppressedPlacementKeys, suppressedCount)
                && writer.WriteInt(faunaCount)
                && WriteProceduralFaunaStateArray(ref writer, value.faunaStates, faunaCount)
                && writer.WriteInt(seamStateCount)
                && WriteProceduralGeologySeamStateArray(ref writer, value.geologySeamStates, seamStateCount)
                && writer.WriteInt(caveEntranceCount)
                && WriteProceduralGeologyCaveEntranceArray(ref writer, value.geologyCaveEntrances, caveEntranceCount)
                && writer.WriteInt(hibernatedFaunaCount)
                && WriteHibernatedFaunaStateArray(ref writer, value.hibernatedFaunaStates, hibernatedFaunaCount);
        }

        private static bool ReadProceduralWorldState(ref BufferReader reader, int version, out ProceduralWorldStateDTO value)
        {
            value = default;
            if (!reader.ReadInt(out value.suppressedPlacementCount)
                || !reader.ReadStructArrayBounded(
                    out value.suppressedPlacementKeys,
                    ProceduralWorldStateDTO.MaxSuppressedPlacements,
                    "Suppressed placement keys")
                || !reader.ReadInt(out value.faunaStateCount)
                || !ReadProceduralFaunaStateArray(ref reader, out value.faunaStates))
            {
                return false;
            }

            if (version < 44)
            {
                SanitizeProceduralWorldStateAfterRead(ref value);
                return true;
            }

            if (!reader.ReadInt(out value.geologySeamStateCount)
                || !reader.ReadStructArrayBounded(
                    out value.geologySeamStates,
                    ProceduralWorldStateDTO.MaxGeologySeamStates,
                    "Procedural geology seam states"))
            {
                return false;
            }

            SanitizeProceduralGeologySeamStateArray(value.geologySeamStates);

            if (version < 45)
            {
                SanitizeProceduralWorldStateAfterRead(ref value);
                return true;
            }

            if (!reader.ReadInt(out value.geologyCaveEntranceCount)
                || !reader.ReadStructArrayBounded(
                    out value.geologyCaveEntrances,
                    ProceduralWorldStateDTO.MaxGeologyCaveEntrances,
                    "Procedural geology cave entrances"))
            {
                return false;
            }

            SanitizeProceduralGeologyCaveEntranceArray(value.geologyCaveEntrances);

            if (version < 46)
            {
                SanitizeProceduralWorldStateAfterRead(ref value);
                return true;
            }

            bool readHibernated = reader.ReadInt(out value.hibernatedFaunaCount)
                && ReadHibernatedFaunaStateArray(ref reader, out value.hibernatedFaunaStates);
            if (!readHibernated)
                return false;

            SanitizeProceduralWorldStateAfterRead(ref value);
            return true;
        }

        private static void SanitizeProceduralWorldStateAfterRead(ref ProceduralWorldStateDTO value)
        {
            value.suppressedPlacementCount = ResolveDecodedCollectionCount(
                value.suppressedPlacementKeys,
                ProceduralWorldStateDTO.MaxSuppressedPlacements);
            value.faunaStateCount = ResolveDecodedCollectionCount(
                value.faunaStates,
                ProceduralWorldStateDTO.MaxFaunaStates);
            value.geologySeamStateCount = ResolveDecodedCollectionCount(
                value.geologySeamStates,
                ProceduralWorldStateDTO.MaxGeologySeamStates);
            value.geologyCaveEntranceCount = ResolveDecodedCollectionCount(
                value.geologyCaveEntrances,
                ProceduralWorldStateDTO.MaxGeologyCaveEntrances);
            value.hibernatedFaunaCount = ResolveDecodedCollectionCount(
                value.hibernatedFaunaStates,
                ProceduralWorldStateDTO.MaxHibernatedFaunaStates);
            value.EnsureCapacity();
        }

        private static bool WriteProceduralGeologySeamStateArray(
            ref BufferWriter writer,
            ProceduralGeologySeamStateDTO[] values,
            int count)
        {
            return WriteCustomArraySlice(
                ref writer,
                values,
                count,
                ProceduralWorldStateDTO.MaxGeologySeamStates,
                WriteProceduralGeologySeamState);
        }

        private static bool WriteProceduralGeologySeamState(ref BufferWriter writer, in ProceduralGeologySeamStateDTO value)
        {
            ProceduralGeologySeamStateDTO safeValue = ProceduralGeologySeamStateDTO.SanitizeForPersistence(in value);
            return writer.WriteStruct(safeValue);
        }

        private static bool WriteProceduralGeologyCaveEntranceArray(
            ref BufferWriter writer,
            ProceduralGeologyCaveEntranceDTO[] values,
            int count)
        {
            return WriteCustomArraySlice(
                ref writer,
                values,
                count,
                ProceduralWorldStateDTO.MaxGeologyCaveEntrances,
                WriteProceduralGeologyCaveEntrance);
        }

        private static bool WriteProceduralGeologyCaveEntrance(ref BufferWriter writer, in ProceduralGeologyCaveEntranceDTO value)
        {
            ProceduralGeologyCaveEntranceDTO safeValue = ProceduralGeologyCaveEntranceDTO.SanitizeForPersistence(in value);
            return writer.WriteStruct(safeValue);
        }

        private static void SanitizeProceduralGeologySeamStateArray(ProceduralGeologySeamStateDTO[] values)
        {
            if (values == null)
                return;

            for (int i = 0; i < values.Length; i++)
                values[i] = ProceduralGeologySeamStateDTO.SanitizeForPersistence(in values[i]);
        }

        private static void SanitizeProceduralGeologyCaveEntranceArray(ProceduralGeologyCaveEntranceDTO[] values)
        {
            if (values == null)
                return;

            for (int i = 0; i < values.Length; i++)
                values[i] = ProceduralGeologyCaveEntranceDTO.SanitizeForPersistence(in values[i]);
        }

        private static int ClampCollectionCount<T>(int count, T[] values, int maxCount)
        {
            int length = values != null ? values.Length : 0;
            int upperBound = Math.Min(Math.Max(maxCount, 0), length);
            return Math.Clamp(count, 0, upperBound);
        }

        private static int ResolveDecodedCollectionCount<T>(T[] values, int maxCount)
        {
            return values != null
                ? Math.Clamp(values.Length, 0, Math.Max(maxCount, 0))
                : 0;
        }

        private static int ResolveDecodedPairedCollectionCount(int maxCount, Array first, Array second, Array third)
        {
            int upperBound = Math.Max(maxCount, 0);
            upperBound = Math.Min(upperBound, first != null ? first.Length : 0);
            upperBound = Math.Min(upperBound, second != null ? second.Length : 0);
            upperBound = Math.Min(upperBound, third != null ? third.Length : 0);
            return upperBound;
        }

        private static int ClampPairedCollectionCount(int count, int maxCount, Array first, Array second)
        {
            int upperBound = Math.Min(Math.Max(maxCount, 0), first != null ? first.Length : 0);
            upperBound = Math.Min(upperBound, second != null ? second.Length : 0);
            return Math.Clamp(count, 0, upperBound);
        }

        private static int ClampPairedCollectionCount<T0, T1, T2>(
            int count,
            int maxCount,
            T0[] values0,
            T1[] values1,
            T2[] values2)
        {
            int upperBound = Math.Max(maxCount, 0);
            upperBound = Math.Min(upperBound, values0 != null ? values0.Length : 0);
            upperBound = Math.Min(upperBound, values1 != null ? values1.Length : 0);
            upperBound = Math.Min(upperBound, values2 != null ? values2.Length : 0);
            return Math.Clamp(count, 0, upperBound);
        }

        private static int ClampPairedCollectionCount<T0, T1, T2, T3>(
            int count,
            int maxCount,
            T0[] values0,
            T1[] values1,
            T2[] values2,
            T3[] values3)
        {
            int upperBound = Math.Max(maxCount, 0);
            upperBound = Math.Min(upperBound, values0 != null ? values0.Length : 0);
            upperBound = Math.Min(upperBound, values1 != null ? values1.Length : 0);
            upperBound = Math.Min(upperBound, values2 != null ? values2.Length : 0);
            upperBound = Math.Min(upperBound, values3 != null ? values3.Length : 0);
            return Math.Clamp(count, 0, upperBound);
        }

        private static int ClampPairedCollectionCount<T0, T1, T2, T3, T4>(
            int count,
            int maxCount,
            T0[] values0,
            T1[] values1,
            T2[] values2,
            T3[] values3,
            T4[] values4)
        {
            int upperBound = Math.Max(maxCount, 0);
            upperBound = Math.Min(upperBound, values0 != null ? values0.Length : 0);
            upperBound = Math.Min(upperBound, values1 != null ? values1.Length : 0);
            upperBound = Math.Min(upperBound, values2 != null ? values2.Length : 0);
            upperBound = Math.Min(upperBound, values3 != null ? values3.Length : 0);
            upperBound = Math.Min(upperBound, values4 != null ? values4.Length : 0);
            return Math.Clamp(count, 0, upperBound);
        }

        private static int ClampPairedListCount<T0, T1>(List<T0> values0, List<T1> values1, int maxCount)
        {
            int upperBound = Math.Max(maxCount, 0);
            upperBound = Math.Min(upperBound, values0 != null ? values0.Count : 0);
            upperBound = Math.Min(upperBound, values1 != null ? values1.Count : 0);
            return upperBound;
        }

        private static bool WriteProceduralFaunaStateArray(ref BufferWriter writer, ProceduralFaunaStateDTO[] values, int count)
        {
            if (values == null)
                return writer.WriteInt(NullCollectionCount);

            int safeCount = ClampCollectionCount(count, values, ProceduralWorldStateDTO.MaxFaunaStates);
            if (!writer.WriteInt(safeCount))
                return false;

            if (safeCount == 0)
                return true;

            for (int i = 0; i < safeCount; i++)
            {
                ProceduralFaunaStateDTO value = ProceduralFaunaStateDTO.SanitizeForPersistence(in values[i]);
                bool isLargeThreatZone = (value.flags & ProceduralFaunaStateDTO.FlagLargeThreatZone) != 0;
                bool blocked = (value.flags & ProceduralFaunaStateDTO.FlagBlocked) != 0;
                if (!writer.WriteLong(value.runtimeKey) ||
                    !writer.WriteFloat(value.cooldownUntilPlayTime) ||
                    !writer.WriteBool(isLargeThreatZone) ||
                    !writer.WriteBool(blocked) ||
                    !writer.WriteByte(0) ||
                    !writer.WriteByte(0))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ReadProceduralFaunaStateArray(ref BufferReader reader, out ProceduralFaunaStateDTO[] values)
        {
            values = null;
            if (!reader.ReadInt(out int count))
                return false;

            if (count == NullCollectionCount)
                return true;

            if (count < 0)
            {
                reader.SetError("Procedural fauna state count is negative.");
                return false;
            }

            if (count == 0)
            {
                values = Array.Empty<ProceduralFaunaStateDTO>();
                return true;
            }

            if (count > ProceduralWorldStateDTO.MaxFaunaStates)
            {
                reader.SetError("Procedural fauna state count exceeds the supported range.");
                return false;
            }

            if (count > int.MaxValue / ProceduralFaunaStateStrideBytes)
            {
                reader.SetError("Procedural fauna state payload exceeds the supported range.");
                return false;
            }

            int payloadBytes = count * ProceduralFaunaStateStrideBytes;
            if (!reader.CanConsumeBytes(payloadBytes))
                return false;

            values = new ProceduralFaunaStateDTO[count];
            for (int i = 0; i < count; i++)
            {
                bool isLargeThreatZone;
                bool blocked;
                if (!reader.ReadLong(out values[i].runtimeKey) ||
                    !reader.ReadFloat(out values[i].cooldownUntilPlayTime) ||
                    !reader.ReadBool(out isLargeThreatZone) ||
                    !reader.ReadBool(out blocked) ||
                    !reader.ReadByte(out _) ||
                    !reader.ReadByte(out _))
                {
                    return false;
                }

                byte flags = 0;
                if (isLargeThreatZone)
                    flags |= ProceduralFaunaStateDTO.FlagLargeThreatZone;
                if (blocked)
                    flags |= ProceduralFaunaStateDTO.FlagBlocked;
                values[i].flags = flags;
                values[i] = ProceduralFaunaStateDTO.SanitizeForPersistence(in values[i]);
            }

            return true;
        }

        private static bool WriteHibernatedFaunaStateArray(ref BufferWriter writer, HibernatedFaunaStateDTO[] values, int count)
        {
            if (values == null)
                return writer.WriteInt(NullCollectionCount);

            int safeCount = ClampCollectionCount(count, values, ProceduralWorldStateDTO.MaxHibernatedFaunaStates);
            if (!writer.WriteInt(safeCount))
                return false;

            if (safeCount == 0)
                return true;

            for (int i = 0; i < safeCount; i++)
            {
                HibernatedFaunaStateDTO value = HibernatedFaunaStateDTO.SanitizeForPersistence(in values[i]);
                bool isLargeThreat = (value.flags & HibernatedFaunaStateDTO.FlagLargeThreat) != 0;
                if (!writer.WriteInt(value.speciesId) ||
                    !writer.WriteInt(value.biomeIndex) ||
                    !writer.WriteInt(value.creatureTypeIndex) ||
                    !writer.WriteFloat(value.health) ||
                    !writer.WriteStruct(value.position) ||
                    !writer.WriteFloat(value.rotationX) ||
                    !writer.WriteFloat(value.rotationY) ||
                    !writer.WriteFloat(value.rotationZ) ||
                    !writer.WriteFloat(value.rotationW) ||
                    !writer.WriteFloat(value.linearVelocityX) ||
                    !writer.WriteFloat(value.linearVelocityY) ||
                    !writer.WriteFloat(value.linearVelocityZ) ||
                    !writer.WriteFloat(value.angularVelocityX) ||
                    !writer.WriteFloat(value.angularVelocityY) ||
                    !writer.WriteFloat(value.angularVelocityZ) ||
                    !writer.WriteUInt(value.uniqueInstanceUid) ||
                    !writer.WriteBool(isLargeThreat) ||
                    !writer.WriteByte(0) ||
                    !writer.WriteByte(0) ||
                    !writer.WriteByte(0))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ReadHibernatedFaunaStateArray(ref BufferReader reader, out HibernatedFaunaStateDTO[] values)
        {
            values = null;
            if (!reader.ReadInt(out int count))
                return false;

            if (count == NullCollectionCount)
                return true;

            if (count < 0)
            {
                reader.SetError("Hibernated fauna state count is negative.");
                return false;
            }

            if (count == 0)
            {
                values = Array.Empty<HibernatedFaunaStateDTO>();
                return true;
            }

            if (count > ProceduralWorldStateDTO.MaxHibernatedFaunaStates)
            {
                reader.SetError("Hibernated fauna state count exceeds the supported range.");
                return false;
            }

            if (count > int.MaxValue / HibernatedFaunaStateStrideBytes)
            {
                reader.SetError("Hibernated fauna state payload exceeds the supported range.");
                return false;
            }

            int payloadBytes = count * HibernatedFaunaStateStrideBytes;
            if (!reader.CanConsumeBytes(payloadBytes))
                return false;

            values = new HibernatedFaunaStateDTO[count];
            for (int i = 0; i < count; i++)
            {
                bool isLargeThreat;
                if (!reader.ReadInt(out values[i].speciesId) ||
                    !reader.ReadInt(out values[i].biomeIndex) ||
                    !reader.ReadInt(out values[i].creatureTypeIndex) ||
                    !reader.ReadFloat(out values[i].health) ||
                    !reader.ReadStruct(out values[i].position) ||
                    !reader.ReadFloat(out values[i].rotationX) ||
                    !reader.ReadFloat(out values[i].rotationY) ||
                    !reader.ReadFloat(out values[i].rotationZ) ||
                    !reader.ReadFloat(out values[i].rotationW) ||
                    !reader.ReadFloat(out values[i].linearVelocityX) ||
                    !reader.ReadFloat(out values[i].linearVelocityY) ||
                    !reader.ReadFloat(out values[i].linearVelocityZ) ||
                    !reader.ReadFloat(out values[i].angularVelocityX) ||
                    !reader.ReadFloat(out values[i].angularVelocityY) ||
                    !reader.ReadFloat(out values[i].angularVelocityZ) ||
                    !reader.ReadUInt(out values[i].uniqueInstanceUid) ||
                    !reader.ReadBool(out isLargeThreat) ||
                    !reader.ReadByte(out _) ||
                    !reader.ReadByte(out _) ||
                    !reader.ReadByte(out _))
                {
                    return false;
                }

                values[i].flags = isLargeThreat ? HibernatedFaunaStateDTO.FlagLargeThreat : (byte)0;
                values[i] = HibernatedFaunaStateDTO.SanitizeForPersistence(in values[i]);
            }

            return true;
        }

        private static bool WriteConstruction(ref BufferWriter writer, ConstructionDTO value)
        {
            int moduleCount = ClampCollectionCount(value.moduleCount, value.modules, ConstructionDTO.MaxModules);
            int graphNodeCount = Math.Min(
                ClampCollectionCount(value.graphNodeCount, value.graphNodes, ConstructionDTO.MaxModules),
                moduleCount);
            int graphEdgeCount = ClampCollectionCount(value.graphEdgeCount, value.graphEdges, ConstructionDTO.MaxGraphEdges);
            int moduleBlitCount = Math.Min(
                ClampCollectionCount(value.moduleBlitCount, value.moduleBlitRecords, ConstructionDTO.MaxModules),
                moduleCount);
            int uniqueGraphEdgeCount = ResolveUniqueModuleGraphEdgeCount(value.graphEdges, graphEdgeCount, graphNodeCount);

            return writer.WriteInt(moduleCount)
                && WriteModuleArray(ref writer, value.modules, moduleCount)
                && writer.WriteInt(graphNodeCount)
                && WriteModuleGraphNodeArray(ref writer, value.graphNodes, graphNodeCount)
                && writer.WriteInt(uniqueGraphEdgeCount)
                && WriteModuleGraphEdgeArray(ref writer, value.graphEdges, graphEdgeCount, graphNodeCount)
                && WriteModuleBlitArray(ref writer, value.moduleBlitRecords, moduleBlitCount);
        }

        private static bool ReadConstruction(ref BufferReader reader, int version, out ConstructionDTO value)
        {
            value = default;
            if (!reader.ReadInt(out value.moduleCount) ||
                !ReadModuleArray(ref reader, version, out value.modules))
            {
                return false;
            }

            value.moduleCount = ResolveDecodedCollectionCount(value.modules, ConstructionDTO.MaxModules);

            if (version >= 47)
            {
                if (!reader.ReadInt(out value.graphNodeCount) ||
                    !ReadModuleGraphNodeArray(ref reader, out value.graphNodes) ||
                    !reader.ReadInt(out value.graphEdgeCount) ||
                    !ReadModuleGraphEdgeArray(ref reader, out value.graphEdges))
                {
                    return false;
                }

                value.graphNodeCount = ResolveDecodedCollectionCount(value.graphNodes, ConstructionDTO.MaxModules);
                value.graphEdgeCount = ResolveDecodedCollectionCount(value.graphEdges, ConstructionDTO.MaxGraphEdges);

                if (version >= 63)
                {
                    if (!ReadModuleBlitArray(ref reader, out value.moduleBlitRecords))
                        return false;

                    value.moduleBlitCount = ResolveDecodedCollectionCount(
                        value.moduleBlitRecords,
                        ConstructionDTO.MaxModules);
                }

                SanitizeConstructionGraphEdgesAfterRead(ref value);
                return true;
            }

            value.graphNodeCount = 0;
            value.graphNodes = null;
            value.graphEdgeCount = 0;
            value.graphEdges = null;
            value.moduleBlitCount = 0;
            value.moduleBlitRecords = null;
            value.EnsureCapacity();
            return true;
        }

        private static void SanitizeConstructionGraphEdgesAfterRead(ref ConstructionDTO value)
        {
            int moduleCount = ClampCollectionCount(value.moduleCount, value.modules, ConstructionDTO.MaxModules);
            int graphNodeCount = Math.Min(
                ClampCollectionCount(value.graphNodeCount, value.graphNodes, ConstructionDTO.MaxModules),
                moduleCount);
            int graphEdgeCount = ClampCollectionCount(value.graphEdgeCount, value.graphEdges, ConstructionDTO.MaxGraphEdges);
            int moduleBlitCount = Math.Min(
                ClampCollectionCount(value.moduleBlitCount, value.moduleBlitRecords, ConstructionDTO.MaxModules),
                moduleCount);
            value.moduleCount = moduleCount;
            value.graphNodeCount = graphNodeCount;
            value.moduleBlitCount = moduleBlitCount;

            if (graphEdgeCount <= 0 || value.graphEdges == null)
            {
                value.graphEdgeCount = 0;
                value.EnsureCapacity();
                return;
            }

            int writeIndex = 0;
            for (int i = 0; i < graphEdgeCount; i++)
            {
                if (!ModuleGraphEdgeDTO.TrySanitizeForPersistence(
                        in value.graphEdges[i],
                        graphNodeCount,
                        out ModuleGraphEdgeDTO safeEdge) ||
                    ModuleGraphEdgeDTO.ContainsPersistenceEdge(value.graphEdges, writeIndex, in safeEdge))
                    continue;

                value.graphEdges[writeIndex] = safeEdge;
                writeIndex++;
            }

            value.graphEdgeCount = writeIndex;
            value.EnsureCapacity();
        }

        private static bool WriteScanLog(ref BufferWriter writer, ScanLogDTO value)
        {
            int entryCount = ClampCollectionCount(value.entryCount, value.entries, ScanLogDTO.MaxEntries);
            int recentSourceCount = ClampCollectionCount(
                value.recentCount,
                value.recentEntryIds,
                ScanLogDTO.MaxRecentEntries);
            int recentCount = CountNonBlankStringArraySlice(
                value.recentEntryIds,
                recentSourceCount,
                ScanLogDTO.MaxRecentEntries);

            return writer.WriteInt(entryCount)
                && WriteScanEntryArray(ref writer, value.entries, entryCount)
                && writer.WriteInt(recentCount)
                && WriteNonBlankStringArraySlice(
                    ref writer,
                    value.recentEntryIds,
                    recentSourceCount,
                    ScanLogDTO.MaxRecentEntries);
        }

        private static bool ReadScanLog(ref BufferReader reader, out ScanLogDTO value)
        {
            value = default;
            bool ok = reader.ReadInt(out value.entryCount)
                && ReadScanEntryArray(ref reader, out value.entries)
                && reader.ReadInt(out value.recentCount)
                && ReadStringArray(
                    ref reader,
                    out value.recentEntryIds,
                    ScanLogDTO.MaxRecentEntries,
                    nameof(value.recentEntryIds));
            if (!ok)
                return false;

            SanitizeScanLogAfterRead(ref value);
            return true;
        }

        private static void SanitizeScanLogAfterRead(ref ScanLogDTO value)
        {
            value.entryCount = ResolveDecodedCollectionCount(value.entries, ScanLogDTO.MaxEntries);
            value.recentCount = ResolveDecodedCollectionCount(
                value.recentEntryIds,
                ScanLogDTO.MaxRecentEntries);
            CompactNonBlankStringArraySlice(
                value.recentEntryIds,
                ref value.recentCount,
                ScanLogDTO.MaxRecentEntries);
            value.EnsureCapacity();
        }

        private static bool WriteBarter(ref BufferWriter writer, BarterDTO value)
        {
            int stateCount = ClampCollectionCount(value.stateCount, value.offerStates, BarterDTO.MaxOffers);
            int transactionCount = ClampCollectionCount(
                value.recentTransactionCount,
                value.recentTransactions,
                BarterDTO.MaxRecentTransactions);

            return writer.WriteInt(stateCount)
                && WriteBarterOfferStateArray(ref writer, value.offerStates, stateCount)
                && writer.WriteInt(transactionCount)
                && WriteBarterTransactionArray(ref writer, value.recentTransactions, transactionCount);
        }

        private static bool ReadBarter(ref BufferReader reader, out BarterDTO value)
        {
            value = default;
            bool ok = reader.ReadInt(out value.stateCount)
                && ReadBarterOfferStateArray(ref reader, out value.offerStates)
                && reader.ReadInt(out value.recentTransactionCount)
                && ReadBarterTransactionArray(ref reader, out value.recentTransactions);
            if (!ok)
                return false;

            SanitizeBarterAfterRead(ref value);
            return true;
        }

        private static void SanitizeBarterAfterRead(ref BarterDTO value)
        {
            value.stateCount = ResolveDecodedCollectionCount(value.offerStates, BarterDTO.MaxOffers);
            value.recentTransactionCount = ResolveDecodedCollectionCount(
                value.recentTransactions,
                BarterDTO.MaxRecentTransactions);
            value.EnsureCapacity();
        }

        private static bool WriteFieldOperationLog(ref BufferWriter writer, FieldOperationLogDTO value)
        {
            int recentCount = ClampCollectionCount(
                value.recentCount,
                value.recentEntries,
                FieldOperationLogDTO.MaxRecentEntries);

            return writer.WriteInt(recentCount)
                && WriteFieldOperationEntryArray(ref writer, value.recentEntries, recentCount);
        }

        private static bool ReadFieldOperationLog(ref BufferReader reader, out FieldOperationLogDTO value)
        {
            value = default;
            bool ok = reader.ReadInt(out value.recentCount)
                && ReadFieldOperationEntryArray(ref reader, out value.recentEntries);
            if (!ok)
                return false;

            SanitizeFieldOperationLogAfterRead(ref value);
            return true;
        }

        private static void SanitizeFieldOperationLogAfterRead(ref FieldOperationLogDTO value)
        {
            value.recentCount = ResolveDecodedCollectionCount(
                value.recentEntries,
                FieldOperationLogDTO.MaxRecentEntries);
            value.EnsureCapacity();
        }

        private static bool WriteBeaconNetwork(ref BufferWriter writer, BeaconNetworkDTO value)
        {
            int activeCount = ClampCollectionCount(value.activeCount, value.entries, BeaconNetworkDTO.MaxEntries);
            int nextSequence = Math.Max(1, value.nextSequence);

            return writer.WriteInt(activeCount)
                && writer.WriteInt(nextSequence)
                && WriteBeaconEntryArray(ref writer, value.entries, activeCount);
        }

        private static bool ReadBeaconNetwork(ref BufferReader reader, out BeaconNetworkDTO value)
        {
            value = default;
            bool ok = reader.ReadInt(out value.activeCount)
                && reader.ReadInt(out value.nextSequence)
                && ReadBeaconEntryArray(ref reader, out value.entries);
            if (!ok)
                return false;

            SanitizeBeaconNetworkAfterRead(ref value);
            return true;
        }

        private static void SanitizeBeaconNetworkAfterRead(ref BeaconNetworkDTO value)
        {
            value.activeCount = ResolveDecodedCollectionCount(value.entries, BeaconNetworkDTO.MaxEntries);
            value.nextSequence = Math.Max(1, value.nextSequence);
            value.EnsureCapacity();
        }

        private static bool WriteExplorationMap(ref BufferWriter writer, ExplorationMapDTO value)
        {
            int exploredChunkCount = math.clamp(
                value.exploredChunkCount,
                0,
                ExplorationMapDTO.MaxExploredChunks);
            int exploredMortonByteCount = ClampAlignedByteCount(
                value.exploredMortonByteCount,
                value.exploredMortonMaskBytes,
                ExplorationMapDTO.MortonMaskByteCount);
            int discoveredSectorByteCount = ClampAlignedByteCount(
                value.discoveredSectorByteCount,
                value.discoveredSectorMaskBytes,
                ExplorationMapDTO.CartographyMaskByteCount);

            return writer.WriteInt(exploredChunkCount)
                && writer.WriteInt(ExplorationMapDTO.DenseChunkSizeMeters)
                && writer.WriteInt(ExplorationMapDTO.MortonMaskAxisBits)
                && writer.WriteInt(ExplorationMapDTO.MortonMaskOriginOffset)
                && writer.WriteUInt(SaveBinaryStorage.ExplorationMortonBuildSalt32)
                && writer.WriteInt(exploredMortonByteCount)
                && WriteByteArraySliceWithZeroFill(
                    ref writer,
                    value.exploredMortonMaskBytes,
                    exploredMortonByteCount,
                    ExplorationMapDTO.MortonMaskByteCount)
                && writer.WriteInt(ExplorationMapDTO.CartographyCellSizeMeters)
                && writer.WriteInt(ExplorationMapDTO.CartographyMaskAxisBits)
                && writer.WriteInt(ExplorationMapDTO.CartographyMaskOriginOffset)
                && writer.WriteInt(discoveredSectorByteCount)
                && WriteByteArraySliceWithZeroFill(
                    ref writer,
                    value.discoveredSectorMaskBytes,
                    discoveredSectorByteCount,
                    ExplorationMapDTO.CartographyMaskByteCount);
        }

        private static bool ReadExplorationMap(ref BufferReader reader, int version, out ExplorationMapDTO value)
        {
            value = default;
            if (version >= CartographyFogSaveVersion)
            {
                bool read = reader.ReadInt(out value.exploredChunkCount)
                    && reader.ReadInt(out value.chunkSizeMeters)
                    && reader.ReadInt(out value.mortonMaskAxisBits)
                    && reader.ReadInt(out value.mortonMaskOriginOffset)
                    && reader.ReadUInt(out value.mortonBuildSalt)
                    && reader.ReadInt(out value.exploredMortonByteCount)
                    && reader.ReadStructArrayBounded(
                        out value.exploredMortonMaskBytes,
                        ExplorationMapDTO.MortonMaskByteCount,
                        nameof(value.exploredMortonMaskBytes))
                    && reader.ReadInt(out value.cartographyCellSizeMeters)
                    && reader.ReadInt(out value.cartographyMaskAxisBits)
                    && reader.ReadInt(out value.cartographyMaskOriginOffset)
                    && reader.ReadInt(out value.discoveredSectorByteCount)
                    && reader.ReadStructArrayBounded(
                        out value.discoveredSectorMaskBytes,
                        ExplorationMapDTO.CartographyMaskByteCount,
                        nameof(value.discoveredSectorMaskBytes));
                if (!read)
                    return false;

                SanitizeCurrentExplorationMapAfterRead(ref value);
                return true;
            }

            if (version >= 56)
            {
                bool read = reader.ReadInt(out value.exploredChunkCount)
                    && reader.ReadInt(out value.chunkSizeMeters)
                    && reader.ReadInt(out value.mortonMaskAxisBits)
                    && reader.ReadInt(out value.mortonMaskOriginOffset)
                    && reader.ReadUInt(out value.mortonBuildSalt)
                    && reader.ReadInt(out value.exploredMortonByteCount)
                    && reader.ReadStructArrayBounded(
                        out value.exploredMortonMaskBytes,
                        ExplorationMapDTO.MortonMaskByteCount,
                        nameof(value.exploredMortonMaskBytes));
                if (read)
                    SanitizeCurrentExplorationMapAfterRead(ref value);

                return read;
            }

            if (version >= 52)
            {
                bool read = reader.ReadInt(out value.exploredChunkCount)
                    && reader.ReadInt(out value.chunkSizeMeters)
                    && reader.ReadInt(out value.mortonMaskAxisBits)
                    && reader.ReadInt(out value.mortonMaskOriginOffset)
                    && reader.ReadInt(out value.exploredMortonByteCount)
                    && reader.ReadStructArrayBounded(
                        out value.exploredMortonMaskBytes,
                        ExplorationMapDTO.MortonMaskByteCount,
                        nameof(value.exploredMortonMaskBytes));
                value.mortonBuildSalt = SaveBinaryStorage.ExplorationMortonBuildSalt32;
                if (read)
                    SanitizeCurrentExplorationMapAfterRead(ref value);

                return read;
            }

            if (!reader.ReadInt(out value.exploredChunkCount)
                || !reader.ReadStructArrayBounded(
                    out value.exploredChunkKeys,
                    ExplorationMapDTO.MaxExploredChunks,
                    nameof(value.exploredChunkKeys)))
            {
                return false;
            }

            if (version < 50)
            {
                value.chunkSizeMeters = ExplorationMapDTO.DenseChunkSizeMeters;
                value.mortonMaskAxisBits = ExplorationMapDTO.MortonMaskAxisBits;
                value.mortonMaskOriginOffset = ExplorationMapDTO.MortonMaskOriginOffset;
                value.exploredMortonWordCount = 0;
                value.exploredMortonMaskWords = null;
                SanitizeLegacyExplorationMapAfterRead(ref value);
                return true;
            }

            bool readLegacyMorton = reader.ReadInt(out value.chunkSizeMeters)
                && reader.ReadInt(out value.mortonMaskAxisBits)
                && reader.ReadInt(out value.mortonMaskOriginOffset)
                && reader.ReadInt(out value.exploredMortonWordCount)
                && reader.ReadStructArrayBounded(
                    out value.exploredMortonMaskWords,
                    ExplorationMapDTO.MortonMaskWordCount,
                    nameof(value.exploredMortonMaskWords));
            if (readLegacyMorton)
                SanitizeLegacyExplorationMapAfterRead(ref value);

            return readLegacyMorton;
        }

        private static void SanitizeCurrentExplorationMapAfterRead(ref ExplorationMapDTO value)
        {
            value.exploredChunkCount = math.clamp(
                value.exploredChunkCount,
                0,
                ExplorationMapDTO.MaxExploredChunks);
            value.chunkSizeMeters = ExplorationMapDTO.DenseChunkSizeMeters;
            value.mortonMaskAxisBits = ExplorationMapDTO.MortonMaskAxisBits;
            value.mortonMaskOriginOffset = ExplorationMapDTO.MortonMaskOriginOffset;
            value.mortonBuildSalt = SaveBinaryStorage.ExplorationMortonBuildSalt32;
            value.exploredMortonByteCount = ClampAlignedByteCount(
                value.exploredMortonByteCount,
                value.exploredMortonMaskBytes,
                ExplorationMapDTO.MortonMaskByteCount);
            value.cartographyCellSizeMeters = ExplorationMapDTO.CartographyCellSizeMeters;
            value.cartographyMaskAxisBits = ExplorationMapDTO.CartographyMaskAxisBits;
            value.cartographyMaskOriginOffset = ExplorationMapDTO.CartographyMaskOriginOffset;
            value.discoveredSectorByteCount = ClampAlignedByteCount(
                value.discoveredSectorByteCount,
                value.discoveredSectorMaskBytes,
                ExplorationMapDTO.CartographyMaskByteCount);
            value.EnsureCapacity();
        }

        private static void SanitizeLegacyExplorationMapAfterRead(ref ExplorationMapDTO value)
        {
            value.exploredChunkCount = math.clamp(
                value.exploredChunkCount,
                0,
                ExplorationMapDTO.MaxExploredChunks);
            value.chunkSizeMeters = ExplorationMapDTO.DenseChunkSizeMeters;
            value.mortonMaskAxisBits = ExplorationMapDTO.MortonMaskAxisBits;
            value.mortonMaskOriginOffset = ExplorationMapDTO.MortonMaskOriginOffset;
            value.mortonBuildSalt = SaveBinaryStorage.ExplorationMortonBuildSalt32;
            value.exploredMortonWordCount = ClampCollectionCount(
                value.exploredMortonWordCount,
                value.exploredMortonMaskWords,
                ExplorationMapDTO.MortonMaskWordCount);
            value.exploredMortonByteCount = 0;
            value.cartographyCellSizeMeters = ExplorationMapDTO.CartographyCellSizeMeters;
            value.cartographyMaskAxisBits = ExplorationMapDTO.CartographyMaskAxisBits;
            value.cartographyMaskOriginOffset = ExplorationMapDTO.CartographyMaskOriginOffset;
            value.discoveredSectorWordCount = 0;
            value.discoveredSectorByteCount = 0;
            value.EnsureCapacity();
        }

        private static int ClampAlignedByteCount(int count, byte[] values, int maxCount)
        {
            int clamped = ClampCollectionCount(count, values, maxCount);
            return SaveBinaryStorage.AlignExplorationMortonByteCount(clamped);
        }

        private static bool WriteByteArraySliceWithZeroFill(
            ref BufferWriter writer,
            byte[] values,
            int count,
            int maxCount)
        {
            int safeCount = Math.Clamp(count, 0, Math.Max(maxCount, 0));
            if (values != null && values.Length >= safeCount)
                return writer.WriteStructArraySlice(values, safeCount);

            if (!writer.WriteInt(safeCount))
                return false;

            int sourceCount = values != null ? Math.Min(values.Length, safeCount) : 0;
            if (sourceCount > 0 && !writer.WriteManagedBytes(values, sourceCount))
                return false;

            return writer.WriteZeroBytes(safeCount - sourceCount);
        }

        private static bool WritePdaLogbook(ref BufferWriter writer, PDALogbookDTO value)
        {
            int entryCount = ClampCollectionCount(value.entryCount, value.entries, PDALogbookDTO.MaxEntries);
            int nextSequence = Math.Max(1, value.nextSequence);
            int seenOriginCount = ClampCollectionCount(
                value.seenOriginCount,
                value.seenOriginHashes,
                PDALogbookDTO.MaxSeenOrigins);

            return writer.WriteInt(entryCount)
                && writer.WriteInt(nextSequence)
                && WritePdaLogbookEntryArray(ref writer, value.entries, entryCount)
                && writer.WriteInt(seenOriginCount)
                && writer.WriteStructArraySlice(value.seenOriginHashes, seenOriginCount);
        }

        private static bool ReadPdaLogbook(ref BufferReader reader, int version, out PDALogbookDTO value)
        {
            value = default;
            if (!reader.ReadInt(out value.entryCount)
                || !reader.ReadInt(out value.nextSequence)
                || !ReadPdaLogbookEntryArray(ref reader, version, out value.entries)
                || !reader.ReadInt(out value.seenOriginCount))
            {
                return false;
            }

            bool readOrigins = version >= 54
                ? reader.ReadStructArrayBounded(
                    out value.seenOriginHashes,
                    PDALogbookDTO.MaxSeenOrigins,
                    nameof(value.seenOriginHashes))
                : ReadStringArray(
                    ref reader,
                    out value.seenOriginKeys,
                    PDALogbookDTO.MaxSeenOrigins,
                    nameof(value.seenOriginKeys));
            if (!readOrigins)
                return false;

            SanitizePdaLogbookAfterRead(ref value);
            return true;
        }

        private static void SanitizePdaLogbookAfterRead(ref PDALogbookDTO value)
        {
            value.entryCount = ResolveDecodedCollectionCount(value.entries, PDALogbookDTO.MaxEntries);
            value.nextSequence = Math.Max(1, value.nextSequence);
            if (value.seenOriginHashes != null)
            {
                value.seenOriginCount = ResolveDecodedCollectionCount(
                    value.seenOriginHashes,
                    PDALogbookDTO.MaxSeenOrigins);
            }
            else
            {
                value.seenOriginCount = ResolveDecodedCollectionCount(
                    value.seenOriginKeys,
                    PDALogbookDTO.MaxSeenOrigins);
            }

            value.EnsureCapacity();
            value.SanitizeSeenOriginsForPersistence();
        }

        private static bool WritePdaMarkers(ref BufferWriter writer, PDAMarkerRegistryDTO value)
        {
            int markerCount = ClampCollectionCount(value.markerCount, value.entries, PDAMarkerRegistryDTO.MaxEntries);
            int nextSequence = Math.Max(1, value.nextSequence);

            return writer.WriteInt(markerCount)
                && writer.WriteInt(nextSequence)
                && WritePdaMarkerEntryArray(ref writer, value.entries, markerCount);
        }

        private static bool ReadPdaMarkers(ref BufferReader reader, int version, out PDAMarkerRegistryDTO value)
        {
            value = default;
            bool read = reader.ReadInt(out value.markerCount)
                && reader.ReadInt(out value.nextSequence)
                && ReadPdaMarkerEntryArray(ref reader, version, out value.entries);
            if (!read)
                return false;

            SanitizePdaMarkersAfterRead(ref value);
            return true;
        }

        private static void SanitizePdaMarkersAfterRead(ref PDAMarkerRegistryDTO value)
        {
            value.markerCount = ResolveDecodedCollectionCount(
                value.entries,
                PDAMarkerRegistryDTO.MaxEntries);
            value.nextSequence = Math.Max(1, value.nextSequence);
            value.EnsureCapacity();
        }

        private static bool WritePdaAdvisories(ref BufferWriter writer, PDAContextualAdvisoryDTO value)
        {
            PDAContextualAdvisoryDTO safeValue = PDAContextualAdvisoryDTO.SanitizeForPersistence(in value);
            return writer.WriteStruct(safeValue);
        }

        private static bool ReadPdaAdvisories(ref BufferReader reader, out PDAContextualAdvisoryDTO value)
        {
            value = default;
            if (!reader.ReadStruct(out value))
                return false;

            value = PDAContextualAdvisoryDTO.SanitizeForPersistence(in value);
            return true;
        }

        private static bool WriteProceduralLore(ref BufferWriter writer, ProceduralLoreStateDTO value)
        {
            int activeCount = ClampCollectionCount(
                value.activeCount,
                value.activePlacements,
                ProceduralLoreStateDTO.MaxActivePlacements);
            int nextSourceIndex = Math.Max(0, value.nextSourceIndex);

            return writer.WriteInt(activeCount)
                && writer.WriteInt(nextSourceIndex)
                && WriteProceduralLorePlacementArray(ref writer, value.activePlacements, activeCount);
        }

        private static bool ReadProceduralLore(ref BufferReader reader, out ProceduralLoreStateDTO value)
        {
            value = default;
            bool read = reader.ReadInt(out value.activeCount)
                && reader.ReadInt(out value.nextSourceIndex)
                && ReadProceduralLorePlacementArray(ref reader, out value.activePlacements);
            if (!read)
                return false;

            SanitizeProceduralLoreAfterRead(ref value);
            return true;
        }

        private static void SanitizeProceduralLoreAfterRead(ref ProceduralLoreStateDTO value)
        {
            value.activeCount = ResolveDecodedCollectionCount(
                value.activePlacements,
                ProceduralLoreStateDTO.MaxActivePlacements);
            value.nextSourceIndex = Math.Max(0, value.nextSourceIndex);
            value.EnsureCapacity();
        }

        private static bool WriteAchievementRegistry(ref BufferWriter writer, AchievementRegistryDTO value)
        {
            int unlockedSourceCount = ClampCollectionCount(
                value.unlockedCount,
                value.unlockedIds,
                AchievementRegistryDTO.MaxUnlockedAchievements);
            int unlockedCount = CountNonBlankStringArraySlice(
                value.unlockedIds,
                unlockedSourceCount,
                AchievementRegistryDTO.MaxUnlockedAchievements);
            float swamDistanceMeters = SanitizeNonNegativeFinite(value.swamDistanceMeters);
            int craftedItemCount = Math.Max(0, value.craftedItemCount);
            int discoveredBiomeCount = Math.Max(0, value.discoveredBiomeCount);

            return writer.WriteFloat(swamDistanceMeters)
                && writer.WriteInt(craftedItemCount)
                && writer.WriteInt(discoveredBiomeCount)
                && writer.WriteInt(unlockedCount)
                && WriteNonBlankStringArraySlice(
                    ref writer,
                    value.unlockedIds,
                    unlockedSourceCount,
                    AchievementRegistryDTO.MaxUnlockedAchievements);
        }

        private static bool ReadAchievementRegistry(ref BufferReader reader, out AchievementRegistryDTO value)
        {
            value = default;
            if (!reader.ReadFloat(out value.swamDistanceMeters)
                || !reader.ReadInt(out value.craftedItemCount)
                || !reader.ReadInt(out value.discoveredBiomeCount)
                || !reader.ReadInt(out value.unlockedCount)
                || !ReadStringArray(
                    ref reader,
                    out value.unlockedIds,
                    AchievementRegistryDTO.MaxUnlockedAchievements,
                    nameof(value.unlockedIds)))
            {
                return false;
            }

            value.swamDistanceMeters = SanitizeNonNegativeFinite(value.swamDistanceMeters);
            value.craftedItemCount = Math.Max(0, value.craftedItemCount);
            value.discoveredBiomeCount = Math.Max(0, value.discoveredBiomeCount);
            value.unlockedCount = ResolveDecodedCollectionCount(
                value.unlockedIds,
                AchievementRegistryDTO.MaxUnlockedAchievements);
            CompactNonBlankStringArraySlice(
                value.unlockedIds,
                ref value.unlockedCount,
                AchievementRegistryDTO.MaxUnlockedAchievements);
            value.EnsureCapacity();
            return true;
        }

        private static bool WriteRunModifiers(ref BufferWriter writer, RunModifiersDTO value)
        {
            RunModifiersDTO safeValue = SanitizeRunModifiers(value);
            return writer.WriteBool(safeValue.isPermadeath)
                && writer.WriteBool(safeValue.isNightmareMode)
                && writer.WriteBool(safeValue.isDailySeed)
                && writer.WriteBool(safeValue.runMarkedDead)
                && writer.WriteString(safeValue.dailySeedId);
        }

        private static bool ReadRunModifiers(ref BufferReader reader, out RunModifiersDTO value)
        {
            value = default;
            bool read = reader.ReadBool(out value.isPermadeath)
                && reader.ReadBool(out value.isNightmareMode)
                && reader.ReadBool(out value.isDailySeed)
                && reader.ReadBool(out value.runMarkedDead)
                && reader.ReadString(out value.dailySeedId);
            if (!read)
                return false;

            value = SanitizeRunModifiers(value);
            return true;
        }

        private static RunModifiersDTO SanitizeRunModifiers(RunModifiersDTO value)
        {
            if (!value.isDailySeed)
            {
                value.dailySeedId = string.Empty;
            }
            else
            {
                value.dailySeedId = SaveData.SanitizePersistenceString(value.dailySeedId);
            }

            if (!value.isPermadeath)
                value.runMarkedDead = false;

            return value;
        }

        private static bool WriteMetaCampaign(ref BufferWriter writer, MetaCampaignDTO value)
        {
            value.EnsureCapacity();
            int safeCount = math.clamp(
                value.variableCount,
                0,
                math.min(MetaCampaignDTO.MaxGlobalVariables, math.min(value.variableHashes.Length, value.variableValues.Length)));

            return writer.WriteInt(safeCount)
                && writer.WriteStruct(value.currentStageHash)
                && writer.WriteInt(value.currentStage)
                && writer.WriteInt(math.clamp(value.toxicityPermille, 0, 1000))
                && writer.WriteStructArraySlice(value.variableHashes, safeCount)
                && writer.WriteStructArraySlice(value.variableValues, safeCount)
                && writer.WriteInt(value.flags);
        }

        private static bool ReadMetaCampaign(ref BufferReader reader, int saveDataVersion, out MetaCampaignDTO value)
        {
            value = MetaCampaignDTO.CreateDefault();
            if (saveDataVersion < MetaCampaignSaveVersion)
                return true;

            if (!reader.ReadInt(out int count)
                || !reader.ReadStruct(out value.currentStageHash)
                || !reader.ReadInt(out value.currentStage)
                || !reader.ReadInt(out value.toxicityPermille)
                || !reader.ReadStructArrayBounded(
                    out uint[] hashes,
                    MetaCampaignDTO.MaxGlobalVariables,
                    nameof(value.variableHashes))
                || !reader.ReadStructArrayBounded(
                    out int[] values,
                    MetaCampaignDTO.MaxGlobalVariables,
                    nameof(value.variableValues))
                || !reader.ReadInt(out int flags))
            {
                return false;
            }

            value.EnsureCapacity();
            int safeCount = math.clamp(
                count,
                0,
                math.min(MetaCampaignDTO.MaxGlobalVariables, math.min(hashes != null ? hashes.Length : 0, values != null ? values.Length : 0)));

            for (int i = 0; i < safeCount; i++)
            {
                value.variableHashes[i] = hashes[i];
                value.variableValues[i] = values[i];
            }

            value.variableCount = safeCount;
            value.toxicityPermille = math.clamp(value.toxicityPermille, 0, 1000);
            value.flags = (byte)math.clamp(flags, 0, byte.MaxValue);
            return true;
        }

        private static bool WriteResourceScarcity(ref BufferWriter writer, ResourceScarcityDTO value)
        {
            int entryCount = ClampResourceScarcityEntryCount(in value);

            return writer.WriteInt(entryCount)
                && WriteResourceScarcityHashIds(ref writer, in value, entryCount)
                && WriteResourceScarcityItemIds(ref writer, in value, entryCount)
                && WriteResourceScarcityCounts(ref writer, in value, entryCount);
        }

        private static int ClampResourceScarcityEntryCount(in ResourceScarcityDTO value)
        {
            int hashCapacity = value.itemHashIds != null
                ? Math.Min(value.itemHashIds.Length, ResourceScarcityDTO.MaxTrackedResources)
                : 0;
            int itemCapacity = value.itemIds != null
                ? Math.Min(value.itemIds.Length, ResourceScarcityDTO.MaxTrackedResources)
                : 0;
            int countCapacity = value.collectedCounts != null
                ? Math.Min(value.collectedCounts.Length, ResourceScarcityDTO.MaxTrackedResources)
                : 0;
            int identityCapacity = Math.Max(hashCapacity, itemCapacity);
            return Math.Clamp(
                value.entryCount,
                0,
                Math.Min(ResourceScarcityDTO.MaxTrackedResources, Math.Min(identityCapacity, countCapacity)));
        }

        private static bool WriteResourceScarcityHashIds(ref BufferWriter writer, in ResourceScarcityDTO value, int entryCount)
        {
            if (!writer.WriteInt(entryCount))
                return false;

            for (int i = 0; i < entryCount; i++)
            {
                int hash = value.itemHashIds != null && i < value.itemHashIds.Length ? value.itemHashIds[i] : 0;
                string itemId = value.itemIds != null && i < value.itemIds.Length
                    ? SanitizeResourceScarcityItemId(hash, value.itemIds[i])
                    : string.Empty;
                if (hash == 0 && itemId.Length != 0)
                    hash = LocHash.Compute(itemId);

                if (!writer.WriteInt(hash))
                    return false;
            }

            return true;
        }

        private static bool WriteResourceScarcityItemIds(ref BufferWriter writer, in ResourceScarcityDTO value, int entryCount)
        {
            if (!writer.WriteInt(entryCount))
                return false;

            for (int i = 0; i < entryCount; i++)
            {
                int hash = value.itemHashIds != null && i < value.itemHashIds.Length ? value.itemHashIds[i] : 0;
                string itemId = value.itemIds != null && i < value.itemIds.Length
                    ? SanitizeResourceScarcityItemId(hash, value.itemIds[i])
                    : string.Empty;
                if (!writer.WriteString(itemId))
                    return false;
            }

            return true;
        }

        private static bool WriteResourceScarcityCounts(ref BufferWriter writer, in ResourceScarcityDTO value, int entryCount)
        {
            if (!writer.WriteInt(entryCount))
                return false;

            for (int i = 0; i < entryCount; i++)
            {
                int count = value.collectedCounts != null && i < value.collectedCounts.Length ? value.collectedCounts[i] : 0;
                if (!writer.WriteInt(Math.Max(0, count)))
                    return false;
            }

            return true;
        }

        private static bool ReadResourceScarcity(ref BufferReader reader, int saveDataVersion, out ResourceScarcityDTO value)
        {
            value = default;
            if (!reader.ReadInt(out value.entryCount))
                return false;

            if (saveDataVersion >= 60 &&
                !reader.ReadStructArrayBounded(
                    out value.itemHashIds,
                    ResourceScarcityDTO.MaxTrackedResources,
                    nameof(value.itemHashIds)))
                return false;

            return ReadStringArray(
                    ref reader,
                    out value.itemIds,
                    ResourceScarcityDTO.MaxTrackedResources,
                    nameof(value.itemIds))
                && reader.ReadStructArrayBounded(
                    out value.collectedCounts,
                    ResourceScarcityDTO.MaxTrackedResources,
                    nameof(value.collectedCounts))
                && SanitizeResourceScarcityAfterRead(ref value);
        }

        private static bool SanitizeResourceScarcityAfterRead(ref ResourceScarcityDTO value)
        {
            int hashCapacity = value.itemHashIds != null
                ? Math.Min(value.itemHashIds.Length, ResourceScarcityDTO.MaxTrackedResources)
                : 0;
            int itemCapacity = value.itemIds != null
                ? Math.Min(value.itemIds.Length, ResourceScarcityDTO.MaxTrackedResources)
                : 0;
            int countCapacity = value.collectedCounts != null
                ? Math.Min(value.collectedCounts.Length, ResourceScarcityDTO.MaxTrackedResources)
                : 0;
            int identityCapacity = Math.Max(hashCapacity, itemCapacity);
            int activeCapacity = Math.Min(
                ResourceScarcityDTO.MaxTrackedResources,
                Math.Min(identityCapacity, countCapacity));
            value.entryCount = activeCapacity;

            value.EnsureCapacity();
            for (int i = 0; i < value.entryCount; i++)
            {
                value.itemIds[i] = SanitizeResourceScarcityItemId(value.itemHashIds[i], value.itemIds[i]);

                if (value.itemHashIds[i] == 0 && value.itemIds[i].Length != 0)
                    value.itemHashIds[i] = LocHash.Compute(value.itemIds[i]);

                if (value.collectedCounts[i] < 0)
                    value.collectedCounts[i] = 0;
            }

            for (int i = value.entryCount; i < ResourceScarcityDTO.MaxTrackedResources; i++)
            {
                value.itemHashIds[i] = 0;
                value.itemIds[i] = string.Empty;
                value.collectedCounts[i] = 0;
            }

            return true;
        }

        private static string SanitizeResourceScarcityItemId(int itemHashId, string itemId)
        {
            itemId = SaveData.SanitizePersistenceString(itemId);
            if (itemId.Length == 0 || itemHashId == 0)
                return itemId;

            return LocHash.Compute(itemId) == itemHashId ? itemId : string.Empty;
        }

        private static bool WriteEnvironmentalStrain(ref BufferWriter writer, EnvironmentalStrainDTO value)
        {
            EnvironmentalStrainDTO safeValue = EnvironmentalStrainDTO.SanitizeForPersistence(in value);
            return writer.WriteStruct(safeValue);
        }

        private static bool ReadEnvironmentalStrain(ref BufferReader reader, out EnvironmentalStrainDTO value)
        {
            value = default;
            if (!reader.ReadStruct(out value))
                return false;

            value = EnvironmentalStrainDTO.SanitizeForPersistence(in value);
            return true;
        }

        private static bool WriteEcosystemState(ref BufferWriter writer, EcosystemStateDTO value)
        {
            int infectedZoneCount = ClampPairedCollectionCount(
                value.infectedZoneCount,
                EcosystemStateDTO.MaxInfectedZones,
                value.infectedChunkKeys,
                value.infectedSeverities);
            int worldGenerationVersionId = Math.Max(0, value.worldGenerationVersionId);

            return writer.WriteInt(value.worldSeed)
                && writer.WriteInt(worldGenerationVersionId)
                && writer.WriteInt(infectedZoneCount)
                && writer.WriteStructArraySlice(value.infectedChunkKeys, infectedZoneCount)
                && WriteUnitFloatArraySlice(
                    ref writer,
                    value.infectedSeverities,
                    infectedZoneCount,
                    EcosystemStateDTO.MaxInfectedZones);
        }

        private static bool ReadEcosystemState(ref BufferReader reader, int saveDataVersion, out EcosystemStateDTO value)
        {
            value = default;
            if (!reader.ReadInt(out value.worldSeed))
                return false;

            if (saveDataVersion >= 58 && !reader.ReadInt(out value.worldGenerationVersionId))
                return false;

            bool ok = reader.ReadInt(out value.infectedZoneCount)
                && reader.ReadStructArrayBounded(
                    out value.infectedChunkKeys,
                    EcosystemStateDTO.MaxInfectedZones,
                    nameof(value.infectedChunkKeys))
                && reader.ReadStructArrayBounded(
                    out value.infectedSeverities,
                    EcosystemStateDTO.MaxInfectedZones,
                    nameof(value.infectedSeverities));
            if (!ok)
                return false;

            SanitizeEcosystemStateAfterRead(ref value);
            return true;
        }

        private static void SanitizeEcosystemStateAfterRead(ref EcosystemStateDTO value)
        {
            value.worldGenerationVersionId = Math.Max(0, value.worldGenerationVersionId);
            int infectedZoneCount = ClampPairedCollectionCount(
                value.infectedZoneCount,
                EcosystemStateDTO.MaxInfectedZones,
                value.infectedChunkKeys,
                value.infectedSeverities);
            value.infectedZoneCount = infectedZoneCount;
            value.EnsureCapacity();

            if (infectedZoneCount <= 0)
                return;

            for (int i = 0; i < infectedZoneCount; i++)
                value.infectedSeverities[i] = math.isfinite(value.infectedSeverities[i])
                    ? math.saturate(value.infectedSeverities[i])
                    : 0f;
        }

        private static bool WriteInventoryCell(ref BufferWriter writer, in InventoryCellDTO value)
        {
            return writer.WriteInt(value.x)
                && writer.WriteInt(value.y)
                && writer.WriteString(SaveData.SanitizePersistenceString(value.itemId))
                && writer.WriteInt(value.stackCount);
        }

        private static bool ReadInventoryCell(ref BufferReader reader, out InventoryCellDTO value)
        {
            value = default;
            bool read = reader.ReadInt(out value.x)
                && reader.ReadInt(out value.y)
                && reader.ReadString(out value.itemId)
                && reader.ReadInt(out value.stackCount);
            if (!read)
                return false;

            value.itemId = SaveData.SanitizePersistenceString(value.itemId);
            return true;
        }

        private static bool WriteScanEntry(ref BufferWriter writer, in ScanEntryDTO value)
        {
            ScanEntryDTO safeValue = ScanEntryDTO.SanitizeForPersistence(in value);
            return writer.WriteString(safeValue.id)
                && writer.WriteString(safeValue.title)
                && writer.WriteString(safeValue.category)
                && writer.WriteString(safeValue.summary);
        }

        private static bool ReadScanEntry(ref BufferReader reader, out ScanEntryDTO value)
        {
            value = default;
            bool read = reader.ReadString(out value.id)
                && reader.ReadString(out value.title)
                && reader.ReadString(out value.category)
                && reader.ReadString(out value.summary);
            if (!read)
                return false;

            value = ScanEntryDTO.SanitizeForPersistence(in value);
            return true;
        }

        private static bool WriteBarterOfferState(ref BufferWriter writer, in BarterOfferStateDTO value)
        {
            BarterOfferStateDTO safeValue = BarterOfferStateDTO.SanitizeForPersistence(in value);
            return writer.WriteString(safeValue.offerId)
                && writer.WriteInt(safeValue.executionCount);
        }

        private static bool ReadBarterOfferState(ref BufferReader reader, out BarterOfferStateDTO value)
        {
            value = default;
            if (!reader.ReadString(out value.offerId)
                || !reader.ReadInt(out value.executionCount))
            {
                return false;
            }

            value = BarterOfferStateDTO.SanitizeForPersistence(in value);
            return true;
        }

        private static bool WriteBarterTransaction(ref BufferWriter writer, in BarterTransactionDTO value)
        {
            BarterTransactionDTO safeValue = BarterTransactionDTO.SanitizeForPersistence(in value);
            return writer.WriteString(safeValue.offerId)
                && writer.WriteString(safeValue.offerName)
                && writer.WriteString(safeValue.channelName)
                && writer.WriteString(safeValue.costSummary)
                && writer.WriteString(safeValue.rewardSummary);
        }

        private static bool ReadBarterTransaction(ref BufferReader reader, out BarterTransactionDTO value)
        {
            value = default;
            bool read = reader.ReadString(out value.offerId)
                && reader.ReadString(out value.offerName)
                && reader.ReadString(out value.channelName)
                && reader.ReadString(out value.costSummary)
                && reader.ReadString(out value.rewardSummary);
            if (!read)
                return false;

            value = BarterTransactionDTO.SanitizeForPersistence(in value);
            return true;
        }

        private static bool WriteFieldOperationEntry(ref BufferWriter writer, in FieldOperationEntryDTO value)
        {
            FieldOperationEntryDTO safeValue = FieldOperationEntryDTO.SanitizeForPersistence(in value);
            return writer.WriteString(safeValue.source)
                && writer.WriteString(safeValue.title)
                && writer.WriteString(safeValue.summary)
                && writer.WriteString(safeValue.severity);
        }

        private static bool ReadFieldOperationEntry(ref BufferReader reader, out FieldOperationEntryDTO value)
        {
            value = default;
            bool read = reader.ReadString(out value.source)
                && reader.ReadString(out value.title)
                && reader.ReadString(out value.summary)
                && reader.ReadString(out value.severity);
            if (!read)
                return false;

            value = FieldOperationEntryDTO.SanitizeForPersistence(in value);
            return true;
        }

        private static bool WriteBeaconEntry(ref BufferWriter writer, in BeaconEntryDTO value)
        {
            BeaconEntryDTO safeValue = BeaconEntryDTO.SanitizeForPersistence(in value);
            return writer.WriteString(safeValue.id)
                && writer.WriteString(safeValue.label)
                && writer.WriteFloat(safeValue.posX)
                && writer.WriteFloat(safeValue.posY)
                && writer.WriteFloat(safeValue.posZ)
                && writer.WriteFloat(safeValue.rotX)
                && writer.WriteFloat(safeValue.rotY)
                && writer.WriteFloat(safeValue.rotZ)
                && writer.WriteFloat(safeValue.rotW)
                && writer.WriteFloat(safeValue.colorR)
                && writer.WriteFloat(safeValue.colorG)
                && writer.WriteFloat(safeValue.colorB)
                && writer.WriteFloat(safeValue.colorA)
                && writer.WriteFloat(safeValue.lightRange);
        }

        private static bool ReadBeaconEntry(ref BufferReader reader, out BeaconEntryDTO value)
        {
            value = default;
            bool ok = reader.ReadString(out value.id)
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
            if (!ok)
                return false;

            value = BeaconEntryDTO.SanitizeForPersistence(in value);
            return true;
        }

        private static bool WritePdaLogbookEntry(ref BufferWriter writer, in PDALogbookEntryDTO value)
        {
            PDALogbookEntryDTO safeValue = PDALogbookEntryDTO.SanitizeForPersistence(in value);
            return writer.WriteInt(safeValue.sequence)
                && writer.WriteInt(safeValue.dayIndex)
                && writer.WriteFloat(safeValue.dayTimeHours)
                && writer.WriteFloat(safeValue.playTimeSeconds)
                && writer.WriteInt(safeValue.titleHash)
                && writer.WriteInt(safeValue.messageHash)
                && writer.WriteInt(safeValue.originHash);
        }

        private static bool ReadPdaLogbookEntry(ref BufferReader reader, int version, out PDALogbookEntryDTO value)
        {
            value = default;
            if (!reader.ReadInt(out value.sequence)
                || !reader.ReadInt(out value.dayIndex)
                || !reader.ReadFloat(out value.dayTimeHours)
                || !reader.ReadFloat(out value.playTimeSeconds))
            {
                return false;
            }

            if (version >= 54)
            {
                bool readHashes = reader.ReadInt(out value.titleHash)
                    && reader.ReadInt(out value.messageHash)
                    && reader.ReadInt(out value.originHash);
                if (!readHashes)
                    return false;

                value = PDALogbookEntryDTO.SanitizeForPersistence(in value);
                return true;
            }

            if (!reader.ReadString(out value.title)
                || !reader.ReadString(out value.message)
                || !reader.ReadString(out value.originKey))
            {
                return false;
            }

            value = PDALogbookEntryDTO.SanitizeForPersistence(in value);
            return true;
        }

        private static bool WritePdaMarkerEntry(ref BufferWriter writer, in PDAMarkerEntryDTO value)
        {
            PDAMarkerEntryDTO safeValue = PDAMarkerEntryDTO.SanitizeForPersistence(in value);
            return writer.WriteString(safeValue.markerId)
                && writer.WriteString(safeValue.title)
                && writer.WriteInt(safeValue.iconType)
                && writer.WriteFloat(safeValue.posX)
                && writer.WriteFloat(safeValue.posY)
                && writer.WriteFloat(safeValue.posZ)
                && writer.WriteBool(safeValue.visibleOnHud)
                && writer.WriteInt(safeValue.positionEncodingVersion)
                && writer.WriteLong(safeValue.aupGridX)
                && writer.WriteLong(safeValue.aupGridY)
                && writer.WriteLong(safeValue.aupGridZ)
                && writer.WriteFloat(safeValue.aupLocalX)
                && writer.WriteFloat(safeValue.aupLocalY)
                && writer.WriteFloat(safeValue.aupLocalZ);
        }

        private static bool ReadPdaMarkerEntry(ref BufferReader reader, int version, out PDAMarkerEntryDTO value)
        {
            value = default;
            if (!(reader.ReadString(out value.markerId)
                && reader.ReadString(out value.title)
                && reader.ReadInt(out value.iconType)
                && reader.ReadFloat(out value.posX)
                && reader.ReadFloat(out value.posY)
                && reader.ReadFloat(out value.posZ)
                && reader.ReadBool(out value.visibleOnHud)))
            {
                return false;
            }

            if (version < 55)
            {
                TryResolveAupFromRuntimeOrigin(value.GetPosition(), out AbsoluteUniversePosition legacyAup);
                value.SetAup(in legacyAup);
                value = PDAMarkerEntryDTO.SanitizeForPersistence(in value);
                return true;
            }

            if (!reader.ReadInt(out value.positionEncodingVersion)
                || !reader.ReadLong(out value.aupGridX)
                || !reader.ReadLong(out value.aupGridY)
                || !reader.ReadLong(out value.aupGridZ)
                || !reader.ReadFloat(out value.aupLocalX)
                || !reader.ReadFloat(out value.aupLocalY)
                || !reader.ReadFloat(out value.aupLocalZ))
            {
                return false;
            }

            value = PDAMarkerEntryDTO.SanitizeForPersistence(in value);
            return true;
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition absoluteAup)
        {
            absoluteAup = default;
            if (!math.all(math.isfinite(new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z))))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            absoluteAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return absoluteAup.IsFinite();
        }

        private static bool WriteProceduralLorePlacement(ref BufferWriter writer, in ProceduralLorePlacementDTO value)
        {
            ProceduralLorePlacementDTO safeValue = ProceduralLorePlacementDTO.SanitizeForPersistence(in value);
            return writer.WriteString(safeValue.discoveryId)
                && writer.WriteString(safeValue.logId)
                && writer.WriteLong(safeValue.chunkKey)
                && writer.WriteFloat(safeValue.posX)
                && writer.WriteFloat(safeValue.posY)
                && writer.WriteFloat(safeValue.posZ);
        }

        private static bool ReadProceduralLorePlacement(ref BufferReader reader, out ProceduralLorePlacementDTO value)
        {
            value = default;
            if (!reader.ReadString(out value.discoveryId)
                || !reader.ReadString(out value.logId)
                || !reader.ReadLong(out value.chunkKey)
                || !reader.ReadFloat(out value.posX)
                || !reader.ReadFloat(out value.posY)
                || !reader.ReadFloat(out value.posZ))
            {
                return false;
            }

            value = ProceduralLorePlacementDTO.SanitizeForPersistence(in value);
            return true;
        }

        private static bool WriteModule(ref BufferWriter writer, in ModuleDTO value)
        {
            ModuleDTO safeValue = ModuleDTO.SanitizeForPersistence(in value);

            if (!WriteModuleBaseProperties(ref writer, in safeValue))
                return false;

            if (!WriteModuleRecyclerProperties(ref writer, in safeValue))
                return false;

            if (!WriteModuleStorageCrateProperties(ref writer, in safeValue))
                return false;

            if (!WriteModulePhysicalAndStatusProperties(ref writer, in safeValue))
                return false;

            return WriteModuleCultivationAndFabricatorProperties(ref writer, in safeValue);
        }

        private static bool WriteModuleBaseProperties(ref BufferWriter writer, in ModuleDTO safeValue)
        {
            int sorterSlotCount = ClampPairedCollectionCount(
                safeValue.sorterBufferedSlotCount,
                ModuleSorterBufferSlotMax,
                safeValue.sorterBufferedItemIds,
                safeValue.sorterBufferedQuantities);

            return writer.WriteString(safeValue.prefabId)
                && writer.WriteString(safeValue.slottedToolItemId)
                && writer.WriteString(safeValue.pipeInFlightItemId)
                && writer.WriteInt(safeValue.pipeInFlightAmount)
                && writer.WriteFloat(safeValue.pipeTransitProgress)
                && writer.WriteFloat(safeValue.pipeExportTimerSeconds)
                && writer.WriteString(safeValue.drillBufferedItemId)
                && writer.WriteInt(safeValue.drillBufferedAmount)
                && writer.WriteFloat(safeValue.drillCycleTimerSeconds)
                && writer.WriteInt(sorterSlotCount)
                && WriteRequiredStringArraySlice(
                    ref writer,
                    safeValue.sorterBufferedItemIds,
                    sorterSlotCount,
                    ModuleSorterBufferSlotMax)
                && WriteNonNegativeIntArraySlice(
                    ref writer,
                    safeValue.sorterBufferedQuantities,
                    sorterSlotCount,
                    ModuleSorterBufferSlotMax);
        }

        private static bool WriteModuleRecyclerProperties(ref BufferWriter writer, in ModuleDTO safeValue)
        {
            int recyclerBufferSlotCount = ClampPairedCollectionCount(
                safeValue.recyclerBufferedSlotCount,
                ModuleRecyclerBufferSlotMax,
                safeValue.recyclerBufferedItemIds,
                safeValue.recyclerBufferedQuantities);

            int recyclerPendingYieldSlotCount = ClampPairedCollectionCount(
                safeValue.recyclerPendingYieldSlotCount,
                ModuleRecyclerPendingYieldSlotMax,
                safeValue.recyclerPendingYieldItemIds,
                safeValue.recyclerPendingYieldQuantities);

            return writer.WriteInt(recyclerBufferSlotCount)
                && WriteRequiredStringArraySlice(
                    ref writer,
                    safeValue.recyclerBufferedItemIds,
                    recyclerBufferSlotCount,
                    ModuleRecyclerBufferSlotMax)
                && WriteNonNegativeIntArraySlice(
                    ref writer,
                    safeValue.recyclerBufferedQuantities,
                    recyclerBufferSlotCount,
                    ModuleRecyclerBufferSlotMax)
                && writer.WriteString(safeValue.recyclerActiveSourceItemId)
                && writer.WriteInt(recyclerPendingYieldSlotCount)
                && WriteRequiredStringArraySlice(
                    ref writer,
                    safeValue.recyclerPendingYieldItemIds,
                    recyclerPendingYieldSlotCount,
                    ModuleRecyclerPendingYieldSlotMax)
                && WriteNonNegativeIntArraySlice(
                    ref writer,
                    safeValue.recyclerPendingYieldQuantities,
                    recyclerPendingYieldSlotCount,
                    ModuleRecyclerPendingYieldSlotMax);
        }

        private static bool WriteModuleStorageCrateProperties(ref BufferWriter writer, in ModuleDTO safeValue)
        {
            int storageCrateSlotCount = safeValue.storageCrateContentsSerialized
                ? ClampPairedCollectionCount(
                    safeValue.storageCrateSlotCount,
                    ModuleStorageCrateSlotMax,
                    safeValue.storageCrateItemIds,
                    safeValue.storageCrateQuantities)
                : 0;

            return writer.WriteBool(safeValue.storageCrateContentsSerialized)
                && writer.WriteInt(storageCrateSlotCount)
                && WriteRequiredStringArraySlice(
                    ref writer,
                    safeValue.storageCrateItemIds,
                    storageCrateSlotCount,
                    ModuleStorageCrateSlotMax)
                && WriteNonNegativeIntArraySlice(
                    ref writer,
                    safeValue.storageCrateQuantities,
                    storageCrateSlotCount,
                    ModuleStorageCrateSlotMax);
        }

        private static bool WriteModulePhysicalAndStatusProperties(ref BufferWriter writer, in ModuleDTO safeValue)
        {
            return writer.WriteFloat(safeValue.posX)
                && writer.WriteFloat(safeValue.posY)
                && writer.WriteFloat(safeValue.posZ)
                && writer.WriteFloat(safeValue.rotX)
                && writer.WriteFloat(safeValue.rotY)
                && writer.WriteFloat(safeValue.rotZ)
                && writer.WriteFloat(safeValue.rotW)
                && writer.WriteFloat(safeValue.integrity)
                && writer.WriteFloat(safeValue.repairIntegrityCap)
                && writer.WriteFloat(safeValue.airReserveNormalized)
                && writer.WriteFloat(safeValue.co2Normalized)
                && writer.WriteBool(safeValue.isFlooded)
                && writer.WriteByte(safeValue.failureMode)
                && writer.WriteByte(safeValue.health)
                && writer.WriteFloat(safeValue.floodedReefFloodSeconds)
                && writer.WriteBool(safeValue.interiorReefInfestationActive);
        }

        private static bool WriteModuleCultivationAndFabricatorProperties(ref BufferWriter writer, in ModuleDTO safeValue)
        {
            int cultivationSlotCount = ClampPairedCollectionCount(
                safeValue.cultivationSlotCount,
                ModuleCultivationSlotMax,
                safeValue.cultivationSeedItemIds,
                safeValue.cultivationGeneticsMasks,
                safeValue.cultivationGrowth01,
                safeValue.cultivationQuality01);

            return writer.WriteInt(cultivationSlotCount)
                && WriteRequiredStringArraySlice(
                    ref writer,
                    safeValue.cultivationSeedItemIds,
                    cultivationSlotCount,
                    ModuleCultivationSlotMax)
                && writer.WriteStructArraySlice(safeValue.cultivationGeneticsMasks, cultivationSlotCount)
                && WriteUnitFloatArraySlice(
                    ref writer,
                    safeValue.cultivationGrowth01,
                    cultivationSlotCount,
                    ModuleCultivationSlotMax)
                && WriteUnitFloatArraySlice(
                    ref writer,
                    safeValue.cultivationQuality01,
                    cultivationSlotCount,
                    ModuleCultivationSlotMax)
                && WriteCultivationSeedHashArraySlice(
                    ref writer,
                    safeValue.cultivationSeedItemHashIds,
                    safeValue.cultivationSeedItemIds,
                    cultivationSlotCount,
                    ModuleCultivationSlotMax)
                && writer.WriteString(safeValue.fabricatorPendingOutputItemId)
                && writer.WriteInt(safeValue.fabricatorPendingOutputQuantity)
                && writer.WriteInt(safeValue.fabricatorPendingOutputTotalQuantity);
        }

        private static bool ReadModule(ref BufferReader reader, int version, out ModuleDTO value)
        {
            value = default;

            if (!ReadModuleBaseProperties(ref reader, ref value))
                return false;

            if (!ReadModuleRecyclerProperties(ref reader, version, ref value))
                return false;

            if (!ReadModuleStorageCrateProperties(ref reader, version, ref value))
                return false;

            if (!ReadModulePhysicalAndStatusProperties(ref reader, version, ref value))
                return false;

            return ReadModuleCultivationAndFabricatorProperties(ref reader, version, ref value);
        }

        private static bool ReadModuleBaseProperties(ref BufferReader reader, ref ModuleDTO value)
        {
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
                && ReadStringArray(
                    ref reader,
                    out value.sorterBufferedItemIds,
                    ModuleSorterBufferSlotMax,
                    nameof(value.sorterBufferedItemIds))
                && reader.ReadStructArrayBounded(
                    out value.sorterBufferedQuantities,
                    ModuleSorterBufferSlotMax,
                    nameof(value.sorterBufferedQuantities));
        }

        private static bool ReadModuleRecyclerProperties(ref BufferReader reader, int version, ref ModuleDTO value)
        {
            if (version >= ResourceRecyclerModuleSaveVersion)
            {
                return reader.ReadInt(out value.recyclerBufferedSlotCount)
                    && ReadStringArray(
                        ref reader,
                        out value.recyclerBufferedItemIds,
                        ModuleRecyclerBufferSlotMax,
                        nameof(value.recyclerBufferedItemIds))
                    && reader.ReadStructArrayBounded(
                        out value.recyclerBufferedQuantities,
                        ModuleRecyclerBufferSlotMax,
                        nameof(value.recyclerBufferedQuantities))
                    && reader.ReadString(out value.recyclerActiveSourceItemId)
                    && reader.ReadInt(out value.recyclerPendingYieldSlotCount)
                    && ReadStringArray(
                        ref reader,
                        out value.recyclerPendingYieldItemIds,
                        ModuleRecyclerPendingYieldSlotMax,
                        nameof(value.recyclerPendingYieldItemIds))
                    && reader.ReadStructArrayBounded(
                        out value.recyclerPendingYieldQuantities,
                        ModuleRecyclerPendingYieldSlotMax,
                        nameof(value.recyclerPendingYieldQuantities));
            }

            value.recyclerBufferedSlotCount = 0;
            value.recyclerBufferedItemIds = null;
            value.recyclerBufferedQuantities = null;
            value.recyclerActiveSourceItemId = string.Empty;
            value.recyclerPendingYieldSlotCount = 0;
            value.recyclerPendingYieldItemIds = null;
            value.recyclerPendingYieldQuantities = null;
            return true;
        }

        private static bool ReadModuleStorageCrateProperties(ref BufferReader reader, int version, ref ModuleDTO value)
        {
            if (version >= StorageCrateModuleSaveVersion)
            {
                return reader.ReadBool(out value.storageCrateContentsSerialized)
                    && reader.ReadInt(out value.storageCrateSlotCount)
                    && ReadStringArray(
                        ref reader,
                        out value.storageCrateItemIds,
                        ModuleStorageCrateSlotMax,
                        nameof(value.storageCrateItemIds))
                    && reader.ReadStructArrayBounded(
                        out value.storageCrateQuantities,
                        ModuleStorageCrateSlotMax,
                        nameof(value.storageCrateQuantities));
            }

            value.storageCrateContentsSerialized = false;
            value.storageCrateSlotCount = 0;
            value.storageCrateItemIds = null;
            value.storageCrateQuantities = null;
            return true;
        }

        private static bool ReadModulePhysicalAndStatusProperties(ref BufferReader reader, int version, ref ModuleDTO value)
        {
            bool ok = reader.ReadFloat(out value.posX)
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

            if (!ok)
                return false;

            if (version >= 63)
            {
                if (!reader.ReadByte(out value.health))
                    return false;
            }
            else
            {
                value.health = PackLegacyModuleHealthByte(value.integrity);
            }

            if (version >= 49)
            {
                if (!reader.ReadFloat(out value.floodedReefFloodSeconds) ||
                    !reader.ReadBool(out value.interiorReefInfestationActive))
                {
                    return false;
                }
            }
            else
            {
                value.floodedReefFloodSeconds = 0f;
                value.interiorReefInfestationActive = false;
            }

            return true;
        }

        private static bool ReadModuleCultivationAndFabricatorProperties(ref BufferReader reader, int version, ref ModuleDTO value)
        {
            if (version < 48)
            {
                value.cultivationSlotCount = 0;
                value.cultivationSeedItemIds = null;
                value.cultivationSeedItemHashIds = null;
                value.cultivationGeneticsMasks = null;
                value.cultivationGrowth01 = null;
                value.cultivationQuality01 = null;
                ModuleDTO.SanitizeForPersistenceInPlace(ref value);
                return true;
            }

            bool ok = reader.ReadInt(out value.cultivationSlotCount)
                && ReadStringArray(
                    ref reader,
                    out value.cultivationSeedItemIds,
                    ModuleCultivationSlotMax,
                    nameof(value.cultivationSeedItemIds))
                && (version >= 53
                    ? reader.ReadStructArrayBounded(
                        out value.cultivationGeneticsMasks,
                        ModuleCultivationSlotMax,
                        nameof(value.cultivationGeneticsMasks))
                    : ReadLegacyUInt32ArrayAsUInt64(
                        ref reader,
                        out value.cultivationGeneticsMasks,
                        ModuleCultivationSlotMax))
                && reader.ReadStructArrayBounded(
                    out value.cultivationGrowth01,
                    ModuleCultivationSlotMax,
                    nameof(value.cultivationGrowth01));

            if (!ok)
                return false;

            if (version >= 51)
            {
                if (!reader.ReadStructArrayBounded(
                    out value.cultivationQuality01,
                    ModuleCultivationSlotMax,
                    nameof(value.cultivationQuality01)))
                {
                    return false;
                }

                if (version >= CultivationSeedHashSaveVersion)
                {
                    if (!reader.ReadStructArrayBounded(
                        out value.cultivationSeedItemHashIds,
                        ModuleCultivationSlotMax,
                        nameof(value.cultivationSeedItemHashIds)))
                    {
                        return false;
                    }
                }
                else
                {
                    value.cultivationSeedItemHashIds = null;
                }

                if (version >= FabricatorPendingOutputSaveVersion &&
                    (!reader.ReadString(out value.fabricatorPendingOutputItemId) ||
                     !reader.ReadInt(out value.fabricatorPendingOutputQuantity) ||
                     !reader.ReadInt(out value.fabricatorPendingOutputTotalQuantity)))
                {
                    return false;
                }

                ModuleDTO.SanitizeForPersistenceInPlace(ref value);
                return true;
            }

            value.cultivationQuality01 = null;
            value.cultivationSeedItemHashIds = null;
            value.fabricatorPendingOutputItemId = string.Empty;
            value.fabricatorPendingOutputQuantity = 0;
            value.fabricatorPendingOutputTotalQuantity = 0;
            ModuleDTO.SanitizeForPersistenceInPlace(ref value);
            return true;
        }

        private static byte PackLegacyModuleHealthByte(float integrity)
        {
            if (!math.isfinite(integrity) || integrity <= 0f)
                return 0;

            return (byte)math.clamp((int)math.round(math.saturate(integrity * 0.01f) * 255f), 0, 255);
        }

        private static bool WriteModuleGraphNode(ref BufferWriter writer, in ModuleGraphNodeDTO value)
        {
            ModuleGraphNodeDTO safeValue = ModuleGraphNodeDTO.SanitizeForPersistence(in value);
            return writer.WriteString(safeValue.prefabId)
                && writer.WriteInt(safeValue.moduleHashId)
                && writer.WriteLong(safeValue.aupGridX)
                && writer.WriteLong(safeValue.aupGridY)
                && writer.WriteLong(safeValue.aupGridZ)
                && writer.WriteFloat(safeValue.aupLocalX)
                && writer.WriteFloat(safeValue.aupLocalY)
                && writer.WriteFloat(safeValue.aupLocalZ)
                && writer.WriteFloat(safeValue.rotX)
                && writer.WriteFloat(safeValue.rotY)
                && writer.WriteFloat(safeValue.rotZ)
                && writer.WriteFloat(safeValue.rotW);
        }

        private static bool ReadModuleGraphNode(ref BufferReader reader, out ModuleGraphNodeDTO value)
        {
            value = default;
            bool ok = reader.ReadString(out value.prefabId)
                && reader.ReadInt(out value.moduleHashId)
                && reader.ReadLong(out value.aupGridX)
                && reader.ReadLong(out value.aupGridY)
                && reader.ReadLong(out value.aupGridZ)
                && reader.ReadFloat(out value.aupLocalX)
                && reader.ReadFloat(out value.aupLocalY)
                && reader.ReadFloat(out value.aupLocalZ)
                && reader.ReadFloat(out value.rotX)
                && reader.ReadFloat(out value.rotY)
                && reader.ReadFloat(out value.rotZ)
                && reader.ReadFloat(out value.rotW);
            if (!ok)
                return false;

            value = ModuleGraphNodeDTO.SanitizeForPersistence(in value);
            return true;
        }

        private static bool WriteModuleGraphEdge(ref BufferWriter writer, in ModuleGraphEdgeDTO value)
        {
            return writer.WriteInt(value.sourceNodeIndex)
                && writer.WriteInt(value.destinationNodeIndex);
        }

        private static bool ReadModuleGraphEdge(ref BufferReader reader, out ModuleGraphEdgeDTO value)
        {
            value = default;
            return reader.ReadInt(out value.sourceNodeIndex)
                && reader.ReadInt(out value.destinationNodeIndex);
        }

        private static bool WriteModuleBlit(ref BufferWriter writer, in ModuleBlitDTO value)
        {
            ModuleBlitDTO safeValue = ModuleBlitDTO.SanitizeForPersistence(in value);
            return writer.WriteStruct(safeValue);
        }

        private static bool ReadModuleBlit(ref BufferReader reader, out ModuleBlitDTO value)
        {
            if (!reader.ReadStruct(out value))
                return false;

            value = ModuleBlitDTO.SanitizeForPersistence(in value);
            return true;
        }

        private static bool WriteInventoryCellArray(ref BufferWriter writer, InventoryCellDTO[] values)
        {
            int count = values != null ? values.Length : NullCollectionCount;
            return WriteCustomArraySlice(ref writer, values, count, InventoryDTO.MaxCells, WriteInventoryCell);
        }

        private static bool ReadInventoryCellArray(ref BufferReader reader, out InventoryCellDTO[] values)
        {
            return ReadCustomArray(
                ref reader,
                out values,
                ReadInventoryCell,
                SerializedIntBytes + SerializedIntBytes + SerializedStringHeaderBytes + SerializedIntBytes,
                InventoryDTO.MaxCells);
        }

        private static bool WriteScanEntryArray(ref BufferWriter writer, ScanEntryDTO[] values, int count)
        {
            return WriteCustomArraySlice(ref writer, values, count, ScanLogDTO.MaxEntries, WriteScanEntry);
        }

        private static bool ReadScanEntryArray(ref BufferReader reader, out ScanEntryDTO[] values)
        {
            return ReadCustomArray(
                ref reader,
                out values,
                ReadScanEntry,
                SerializedStringHeaderBytes * 4,
                ScanLogDTO.MaxEntries);
        }

        private static bool WriteBarterOfferStateArray(ref BufferWriter writer, BarterOfferStateDTO[] values, int count)
        {
            return WriteCustomArraySlice(ref writer, values, count, BarterDTO.MaxOffers, WriteBarterOfferState);
        }

        private static bool ReadBarterOfferStateArray(ref BufferReader reader, out BarterOfferStateDTO[] values)
        {
            return ReadCustomArray(
                ref reader,
                out values,
                ReadBarterOfferState,
                SerializedStringHeaderBytes + SerializedIntBytes,
                BarterDTO.MaxOffers);
        }

        private static bool WriteBarterTransactionArray(ref BufferWriter writer, BarterTransactionDTO[] values, int count)
        {
            return WriteCustomArraySlice(
                ref writer,
                values,
                count,
                BarterDTO.MaxRecentTransactions,
                WriteBarterTransaction);
        }

        private static bool ReadBarterTransactionArray(ref BufferReader reader, out BarterTransactionDTO[] values)
        {
            return ReadCustomArray(
                ref reader,
                out values,
                ReadBarterTransaction,
                SerializedStringHeaderBytes * 5,
                BarterDTO.MaxRecentTransactions);
        }

        private static bool WriteFieldOperationEntryArray(ref BufferWriter writer, FieldOperationEntryDTO[] values, int count)
        {
            return WriteCustomArraySlice(
                ref writer,
                values,
                count,
                FieldOperationLogDTO.MaxRecentEntries,
                WriteFieldOperationEntry);
        }

        private static bool ReadFieldOperationEntryArray(ref BufferReader reader, out FieldOperationEntryDTO[] values)
        {
            return ReadCustomArray(
                ref reader,
                out values,
                ReadFieldOperationEntry,
                SerializedStringHeaderBytes * 4,
                FieldOperationLogDTO.MaxRecentEntries);
        }

        private static bool WriteBeaconEntryArray(ref BufferWriter writer, BeaconEntryDTO[] values, int count)
        {
            return WriteCustomArraySlice(ref writer, values, count, BeaconNetworkDTO.MaxEntries, WriteBeaconEntry);
        }

        private static bool ReadBeaconEntryArray(ref BufferReader reader, out BeaconEntryDTO[] values)
        {
            return ReadCustomArray(
                ref reader,
                out values,
                ReadBeaconEntry,
                (SerializedStringHeaderBytes * 2) + (SerializedFloatBytes * 12),
                BeaconNetworkDTO.MaxEntries);
        }

        private static bool WritePdaLogbookEntryArray(ref BufferWriter writer, PDALogbookEntryDTO[] values, int count)
        {
            return WriteCustomArraySlice(ref writer, values, count, PDALogbookDTO.MaxEntries, WritePdaLogbookEntry);
        }

        private static bool ReadPdaLogbookEntryArray(ref BufferReader reader, int version, out PDALogbookEntryDTO[] values)
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

            if (count > PDALogbookDTO.MaxEntries)
            {
                reader.SetError(nameof(PDALogbookEntryDTO) + " length exceeds the supported range.");
                return false;
            }

            if (count == 0)
            {
                values = Array.Empty<PDALogbookEntryDTO>();
                return true;
            }

            int minimumBytesPerEntry = version >= 54
                ? (SerializedIntBytes * 5) + (SerializedFloatBytes * 2)
                : (SerializedIntBytes * 2) + (SerializedFloatBytes * 2) + (SerializedStringHeaderBytes * 3);
            if (!reader.CanConsumeCollectionItems(count, minimumBytesPerEntry, nameof(PDALogbookEntryDTO)))
                return false;

            values = new PDALogbookEntryDTO[count];
            for (int i = 0; i < count; i++)
            {
                if (!ReadPdaLogbookEntry(ref reader, version, out values[i]))
                    return false;
            }

            return true;
        }

        private static bool WritePdaMarkerEntryArray(ref BufferWriter writer, PDAMarkerEntryDTO[] values, int count)
        {
            return WriteCustomArraySlice(ref writer, values, count, PDAMarkerRegistryDTO.MaxEntries, WritePdaMarkerEntry);
        }

        private static bool ReadPdaMarkerEntryArray(ref BufferReader reader, int version, out PDAMarkerEntryDTO[] values)
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

            if (count > PDAMarkerRegistryDTO.MaxEntries)
            {
                reader.SetError(nameof(PDAMarkerEntryDTO) + " length exceeds the supported range.");
                return false;
            }

            if (count == 0)
            {
                values = Array.Empty<PDAMarkerEntryDTO>();
                return true;
            }

            int minimumBytesPerEntry = version >= 55
                ? (SerializedStringHeaderBytes * 2) + SerializedIntBytes + (SerializedFloatBytes * 3) + SerializedBoolBytes + SerializedIntBytes + (SerializedLongBytes * 3) + (SerializedFloatBytes * 3)
                : (SerializedStringHeaderBytes * 2) + SerializedIntBytes + (SerializedFloatBytes * 3) + SerializedBoolBytes;
            if (!reader.CanConsumeCollectionItems(count, minimumBytesPerEntry, nameof(PDAMarkerEntryDTO)))
                return false;

            values = new PDAMarkerEntryDTO[count];
            for (int i = 0; i < count; i++)
            {
                if (!ReadPdaMarkerEntry(ref reader, version, out values[i]))
                    return false;
            }

            return true;
        }

        private static bool WriteProceduralLorePlacementArray(
            ref BufferWriter writer,
            ProceduralLorePlacementDTO[] values,
            int count)
        {
            return WriteCustomArraySlice(
                ref writer,
                values,
                count,
                ProceduralLoreStateDTO.MaxActivePlacements,
                WriteProceduralLorePlacement);
        }

        private static bool ReadProceduralLorePlacementArray(ref BufferReader reader, out ProceduralLorePlacementDTO[] values)
        {
            return ReadCustomArray(
                ref reader,
                out values,
                ReadProceduralLorePlacement,
                (SerializedStringHeaderBytes * 2) + SerializedLongBytes + (SerializedFloatBytes * 3),
                ProceduralLoreStateDTO.MaxActivePlacements);
        }

        private static bool WriteModuleArray(ref BufferWriter writer, ModuleDTO[] values, int count)
        {
            return WriteCustomArraySlice(ref writer, values, count, ConstructionDTO.MaxModules, WriteModule);
        }

        private static bool ReadModuleArray(ref BufferReader reader, int version, out ModuleDTO[] values)
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

            if (count > ConstructionDTO.MaxModules)
            {
                reader.SetError(nameof(ModuleDTO) + " length exceeds the supported range.");
                return false;
            }

            if (count == 0)
            {
                values = Array.Empty<ModuleDTO>();
                return true;
            }

            int minimumBytesPerEntry = (SerializedStringHeaderBytes * 4)
                + (SerializedIntBytes * 3)
                + (SerializedFloatBytes * 14)
                + (SerializedStringHeaderBytes * 2)
                + SerializedBoolBytes
                + sizeof(byte);
            if (version >= 63)
                minimumBytesPerEntry += sizeof(byte);
            if (version >= 49)
                minimumBytesPerEntry += SerializedFloatBytes + SerializedBoolBytes;
            if (version >= 48)
                minimumBytesPerEntry += SerializedIntBytes + (SerializedStringHeaderBytes * 3);
            if (version >= 51)
                minimumBytesPerEntry += SerializedStringHeaderBytes;
            if (!reader.CanConsumeCollectionItems(count, minimumBytesPerEntry, nameof(ModuleDTO)))
                return false;

            values = new ModuleDTO[count];
            for (int i = 0; i < count; i++)
            {
                if (!ReadModule(ref reader, version, out values[i]))
                    return false;
            }

            return true;
        }

        private static bool WriteModuleGraphNodeArray(ref BufferWriter writer, ModuleGraphNodeDTO[] values, int count)
        {
            return WriteCustomArraySlice(ref writer, values, count, ConstructionDTO.MaxModules, WriteModuleGraphNode);
        }

        private static bool ReadModuleGraphNodeArray(ref BufferReader reader, out ModuleGraphNodeDTO[] values)
        {
            return ReadCustomArray(
                ref reader,
                out values,
                ReadModuleGraphNode,
                SerializedStringHeaderBytes + SerializedIntBytes + (SerializedLongBytes * 3) + (SerializedFloatBytes * 7),
                ConstructionDTO.MaxModules);
        }

        private static bool WriteModuleGraphEdgeArray(
            ref BufferWriter writer,
            ModuleGraphEdgeDTO[] values,
            int count,
            int graphNodeCount)
        {
            if (values == null)
                return writer.WriteInt(NullCollectionCount);

            int safeCount = ClampCollectionCount(count, values, ConstructionDTO.MaxGraphEdges);
            int uniqueCount = ResolveUniqueModuleGraphEdgeCount(values, safeCount, graphNodeCount);

            if (!writer.WriteInt(uniqueCount))
                return false;

            for (int i = 0; i < safeCount; i++)
            {
                if (!ModuleGraphEdgeDTO.TrySanitizeForPersistence(in values[i], graphNodeCount, out ModuleGraphEdgeDTO edge) ||
                    ContainsPriorModuleGraphEdge(values, i, graphNodeCount, in edge))
                    continue;

                if (!WriteModuleGraphEdge(ref writer, in edge))
                    return false;
            }

            return true;
        }

        private static int ResolveUniqueModuleGraphEdgeCount(
            ModuleGraphEdgeDTO[] values,
            int count,
            int graphNodeCount)
        {
            if (values == null)
                return 0;

            int safeCount = ClampCollectionCount(count, values, ConstructionDTO.MaxGraphEdges);
            int uniqueCount = 0;
            for (int i = 0; i < safeCount; i++)
            {
                if (ModuleGraphEdgeDTO.TrySanitizeForPersistence(in values[i], graphNodeCount, out ModuleGraphEdgeDTO edge) &&
                    !ContainsPriorModuleGraphEdge(values, i, graphNodeCount, in edge))
                    uniqueCount++;
            }

            return uniqueCount;
        }

        private static bool ContainsPriorModuleGraphEdge(
            ModuleGraphEdgeDTO[] values,
            int currentIndex,
            int graphNodeCount,
            in ModuleGraphEdgeDTO edge)
        {
            if (values == null || currentIndex <= 0)
                return false;

            int safeCurrentIndex = Math.Clamp(currentIndex, 0, Math.Min(values.Length, ConstructionDTO.MaxGraphEdges));
            for (int i = 0; i < safeCurrentIndex; i++)
            {
                if (!ModuleGraphEdgeDTO.TrySanitizeForPersistence(in values[i], graphNodeCount, out ModuleGraphEdgeDTO priorEdge))
                    continue;

                if (ModuleGraphEdgeDTO.PersistenceEquals(in priorEdge, in edge))
                    return true;
            }

            return false;
        }

        private static bool ReadModuleGraphEdgeArray(ref BufferReader reader, out ModuleGraphEdgeDTO[] values)
        {
            return ReadCustomArray(
                ref reader,
                out values,
                ReadModuleGraphEdge,
                SerializedIntBytes * 2,
                ConstructionDTO.MaxGraphEdges);
        }

        private static bool WriteModuleBlitArray(ref BufferWriter writer, ModuleBlitDTO[] values, int count)
        {
            return WriteCustomArraySlice(ref writer, values, count, ConstructionDTO.MaxModules, WriteModuleBlit);
        }

        private static bool ReadModuleBlitArray(ref BufferReader reader, out ModuleBlitDTO[] values)
        {
            return ReadCustomArray(
                ref reader,
                out values,
                ReadModuleBlit,
                UnsafeUtility.SizeOf<ModuleBlitDTO>(),
                ConstructionDTO.MaxModules);
        }

        private static bool WriteNonNegativeIntArraySlice(ref BufferWriter writer, int[] values, int count, int maxCount)
        {
            if (values == null)
                return writer.WriteInt(NullCollectionCount);

            int safeCount = ClampCollectionCount(count, values, maxCount);
            if (!writer.WriteInt(safeCount))
                return false;

            for (int i = 0; i < safeCount; i++)
            {
                if (!writer.WriteInt(Math.Max(0, values[i])))
                    return false;
            }

            return true;
        }

        private static bool WriteNonNegativeDoubleArraySlice(ref BufferWriter writer, double[] values, int count, int maxCount)
        {
            if (values == null)
                values = Array.Empty<double>();

            int safeCount = math.clamp(count, 0, maxCount);
            if (!writer.WriteInt(safeCount))
                return false;

            for (int i = 0; i < safeCount; i++)
            {
                double rawValue = i < values.Length ? values[i] : 0d;
                double value = math.isfinite(rawValue) ? math.max(0d, rawValue) : 0d;
                if (!writer.WriteDouble(value))
                    return false;
            }

            return true;
        }

        private static bool WriteMaskedByteArraySlice(
            ref BufferWriter writer,
            byte[] values,
            int count,
            int maxCount,
            byte mask)
        {
            if (values == null)
                values = Array.Empty<byte>();

            int safeCount = math.clamp(count, 0, maxCount);
            if (!writer.WriteInt(safeCount))
                return false;

            for (int i = 0; i < safeCount; i++)
            {
                byte rawValue = i < values.Length ? values[i] : (byte)0;
                if (!writer.WriteByte((byte)(rawValue & mask)))
                    return false;
            }

            return true;
        }

        private static bool WriteUnitFloatArraySlice(ref BufferWriter writer, float[] values, int count, int maxCount)
        {
            if (values == null)
                return writer.WriteInt(NullCollectionCount);

            int safeCount = ClampCollectionCount(count, values, maxCount);
            if (!writer.WriteInt(safeCount))
                return false;

            for (int i = 0; i < safeCount; i++)
            {
                float value = math.isfinite(values[i]) ? math.saturate(values[i]) : 0f;
                if (!writer.WriteFloat(value))
                    return false;
            }

            return true;
        }

        private static bool WriteCultivationSeedHashArraySlice(
            ref BufferWriter writer,
            int[] values,
            string[] seedItemIds,
            int count,
            int maxCount)
        {
            int safeCount = ClampCollectionCount(count, seedItemIds, maxCount);
            if (!writer.WriteInt(safeCount))
                return false;

            for (int i = 0; i < safeCount; i++)
            {
                int value = values != null && i < values.Length ? values[i] : 0;
                if (string.IsNullOrWhiteSpace(seedItemIds[i]))
                    value = 0;

                if (!writer.WriteInt(value))
                    return false;
            }

            return true;
        }

        private static bool WriteRequiredStringArraySlice(ref BufferWriter writer, string[] values, int count, int maxCount)
        {
            int safeCount = ClampCollectionCount(count, values, maxCount);
            if (!writer.WriteInt(safeCount))
                return false;

            for (int i = 0; i < safeCount; i++)
            {
                if (!writer.WriteString(values[i] ?? string.Empty))
                    return false;
            }

            return true;
        }

        private static bool WriteNonBlankStringArraySlice(ref BufferWriter writer, string[] values, int count, int maxCount)
        {
            int safeCount = CountNonBlankStringArraySlice(values, count, maxCount);
            if (!writer.WriteInt(safeCount))
                return false;

            if (safeCount == 0)
                return true;

            int bound = ClampCollectionCount(count, values, maxCount);
            int written = 0;
            for (int i = 0; i < bound && written < safeCount; i++)
            {
                string value = SaveData.SanitizePersistenceString(values[i]);
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                if (!writer.WriteString(value))
                    return false;

                written++;
            }

            return true;
        }

        private static int CountNonBlankStringArraySlice(string[] values, int count, int maxCount)
        {
            int safeCount = ClampCollectionCount(count, values, maxCount);
            int nonBlankCount = 0;
            for (int i = 0; i < safeCount; i++)
            {
                if (SaveData.SanitizePersistenceString(values[i]).Length != 0)
                    nonBlankCount++;
            }

            return nonBlankCount;
        }

        private static bool WriteCountedLongWords(ref BufferWriter writer, long[] words, int wordCount)
        {
            int safeWordCount = Math.Max(0, wordCount);
            if (!writer.WriteInt(safeWordCount))
                return false;

            for (int i = 0; i < safeWordCount; i++)
            {
                long value = words != null && i < words.Length ? words[i] : 0L;
                if (!writer.WriteLong(value))
                    return false;
            }

            return true;
        }

        private static bool WriteDiscoveredBiomeBitWords(ref BufferWriter writer, long[] words)
        {
            if (!writer.WriteInt(BiomeDiscoveryBitMask.WordCount))
                return false;

            for (int i = 0; i < BiomeDiscoveryBitMask.WordCount; i++)
            {
                long value = words != null && i < words.Length ? words[i] : 0L;
                if (!writer.WriteLong(BiomeDiscoveryBitMask.SanitizeWord(i, value)))
                    return false;
            }

            return true;
        }

        private static bool WriteIndustrialLoreUnlockWords(ref BufferWriter writer, long[] words)
        {
            if (!writer.WriteInt(IndustrialLoreBitMask.WordCount))
                return false;

            long value = words != null && words.Length > 0 ? words[0] : 0L;
            return writer.WriteLong(IndustrialLoreBitMask.SanitizeWord(value));
        }

        private static bool ReadStringArray(ref BufferReader reader, out string[] values, int maxCount, string collectionName)
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

            if (count > maxCount)
            {
                reader.SetError(collectionName + " length exceeds the supported range.");
                return false;
            }

            if (count == 0)
            {
                values = Array.Empty<string>();
                return true;
            }

            if (!reader.CanConsumeCollectionItems(count, SerializedStringHeaderBytes, collectionName))
                return false;

            values = new string[count];
            for (int i = 0; i < count; i++)
            {
                if (!reader.ReadString(out values[i]))
                    return false;
            }

            return true;
        }

        private static void CompactNonBlankStringArraySlice(string[] values, ref int count, int maxCount)
        {
            if (values == null)
            {
                count = 0;
                return;
            }

            int safeCount = Math.Clamp(count, 0, Math.Min(values.Length, Math.Max(maxCount, 0)));
            int writeIndex = 0;
            for (int i = 0; i < safeCount; i++)
            {
                string value = SaveData.SanitizePersistenceString(values[i]);
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                if (writeIndex != i || !string.Equals(values[writeIndex], value, StringComparison.Ordinal))
                    values[writeIndex] = value;

                writeIndex++;
            }

            for (int i = writeIndex; i < safeCount; i++)
                values[i] = string.Empty;

            count = writeIndex;
        }

        private static int ClampListCount<T>(List<T> values, int maxCount)
        {
            return values != null
                ? Math.Clamp(values.Count, 0, Math.Max(maxCount, 0))
                : 0;
        }

        private static int CountNonBlankStringKeyDictionaryEntries<TValue>(
            Dictionary<string, TValue> values,
            int maxCount)
        {
            if (values == null)
                return 0;

            int safeMax = Math.Max(maxCount, 0);
            if (safeMax == 0)
                return 0;

            int count = 0;
            HashSet<string> claimedKeys = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, TValue>.Enumerator enumerator = values.GetEnumerator();
            while (count < safeMax && enumerator.MoveNext())
            {
                if (!TryClaimCanonicalDictionaryKey(values, enumerator.Current.Key, claimedKeys, out _))
                    continue;

                count++;
            }

            enumerator.Dispose();
            return count;
        }

        private static bool TryClaimCanonicalDictionaryKey<TValue>(
            Dictionary<string, TValue> values,
            string key,
            HashSet<string> claimedKeys,
            out string canonicalKey)
        {
            canonicalKey = SaveData.SanitizePersistenceString(key);
            if (canonicalKey.Length == 0)
                return false;

            if (!string.Equals(key, canonicalKey, StringComparison.Ordinal) &&
                values.ContainsKey(canonicalKey))
            {
                return false;
            }

            return claimedKeys.Add(canonicalKey);
        }

        private static bool ReadCollectionCount(
            ref BufferReader reader,
            out int count,
            int maxCount,
            string collectionName)
        {
            count = 0;
            if (!reader.ReadInt(out count))
                return false;

            if (count == NullCollectionCount)
                return true;

            if (count < 0)
            {
                reader.SetError(collectionName + " length is negative.");
                return false;
            }

            if (count > Math.Max(maxCount, 0))
            {
                reader.SetError(collectionName + " length exceeds the supported range.");
                return false;
            }

            return true;
        }

        private static bool WriteNonBlankStringList(ref BufferWriter writer, List<string> values, int maxCount)
        {
            int count = CountNonBlankStringListEntries(values, maxCount);
            if (!writer.WriteInt(count))
                return false;

            if (count == 0)
                return true;

            int bound = ClampListCount(values, maxCount);
            int written = 0;
            for (int i = 0; i < bound && written < count; i++)
            {
                string value = SaveData.SanitizePersistenceString(values[i]);
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                if (!writer.WriteString(value))
                    return false;

                written++;
            }

            return true;
        }

        private static bool WritePairedNonBlankStringList(
            ref BufferWriter writer,
            List<string> ids,
            List<float> values,
            int sourceCount)
        {
            int count = CountNonBlankPairedStringListEntries(ids, values, sourceCount);
            if (!writer.WriteInt(count))
                return false;

            if (count == 0)
                return true;

            int bound = ClampPairedListCount(ids, values, sourceCount);
            int written = 0;
            for (int i = 0; i < bound && written < count; i++)
            {
                string id = SaveData.SanitizePersistenceString(ids[i]);
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                if (!writer.WriteString(id))
                    return false;

                written++;
            }

            return true;
        }

        private static bool WritePairedNonBlankFloatList(
            ref BufferWriter writer,
            List<string> ids,
            List<float> values,
            int sourceCount)
        {
            int count = CountNonBlankPairedStringListEntries(ids, values, sourceCount);
            if (!writer.WriteInt(count))
                return false;

            if (count == 0)
                return true;

            int bound = ClampPairedListCount(ids, values, sourceCount);
            int written = 0;
            for (int i = 0; i < bound && written < count; i++)
            {
                string id = SaveData.SanitizePersistenceString(ids[i]);
                if (id.Length == 0)
                    continue;

                if (!writer.WriteFloat(SanitizeNonNegativeFinite(values[i])))
                    return false;

                written++;
            }

            return true;
        }

        private static int CountNonBlankStringListEntries(List<string> values, int maxCount)
        {
            int bound = ClampListCount(values, maxCount);
            int count = 0;
            for (int i = 0; i < bound; i++)
            {
                if (SaveData.SanitizePersistenceString(values[i]).Length != 0)
                    count++;
            }

            return count;
        }

        private static int CountNonBlankPairedStringListEntries(
            List<string> ids,
            List<float> values,
            int sourceCount)
        {
            int bound = ClampPairedListCount(ids, values, sourceCount);
            int count = 0;
            for (int i = 0; i < bound; i++)
            {
                if (SaveData.SanitizePersistenceString(ids[i]).Length != 0)
                    count++;
            }

            return count;
        }

        private static bool ReadStringList(
            ref BufferReader reader,
            out List<string> values,
            int maxCount,
            string collectionName)
        {
            values = null;
            if (!ReadCollectionCount(ref reader, out int count, maxCount, collectionName))
                return false;

            if (count == NullCollectionCount)
                return true;

            if (!reader.CanConsumeCollectionItems(count, SerializedStringHeaderBytes, collectionName))
                return false;

            values = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                if (!reader.ReadString(out string item))
                    return false;

                values.Add(item ?? string.Empty);
            }

            return true;
        }

        private static bool ReadFloatList(
            ref BufferReader reader,
            out List<float> values,
            int maxCount,
            string collectionName)
        {
            values = null;
            if (!ReadCollectionCount(ref reader, out int count, maxCount, collectionName))
                return false;

            if (count == NullCollectionCount)
                return true;

            if (!reader.CanConsumeCollectionItems(count, SerializedFloatBytes, collectionName))
                return false;

            values = new List<float>(count);
            for (int i = 0; i < count; i++)
            {
                if (!reader.ReadFloat(out float item))
                    return false;

                values.Add(SanitizeNonNegativeFinite(item));
            }

            return true;
        }

        private static bool WriteStringFloatDictionary(
            ref BufferWriter writer,
            Dictionary<string, float> values,
            int maxCount)
        {
            int count = CountNonBlankStringKeyDictionaryEntries(values, maxCount);
            if (!writer.WriteInt(count))
                return false;

            if (values == null)
                return true;

            int written = 0;
            HashSet<string> writtenKeys = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, float>.Enumerator enumerator = values.GetEnumerator();
            while (written < count && enumerator.MoveNext())
            {
                KeyValuePair<string, float> pair = enumerator.Current;
                if (!TryClaimCanonicalDictionaryKey(values, pair.Key, writtenKeys, out string key))
                    continue;

                if (!writer.WriteString(key) || !writer.WriteFloat(SanitizeNonNegativeFinite(pair.Value)))
                    return false;

                written++;
            }

            enumerator.Dispose();
            return true;
        }

        private static bool ReadStringFloatDictionary(
            ref BufferReader reader,
            out Dictionary<string, float> values,
            int maxCount,
            string collectionName)
        {
            values = null;
            if (!ReadCollectionCount(ref reader, out int count, maxCount, collectionName))
                return false;

            if (count == NullCollectionCount)
                return true;

            if (!reader.CanConsumeCollectionItems(count, SerializedStringHeaderBytes + SerializedFloatBytes, collectionName))
                return false;

            values = new Dictionary<string, float>(count);
            for (int i = 0; i < count; i++)
            {
                if (!reader.ReadString(out string key) || !reader.ReadFloat(out float entryValue))
                    return false;

                string canonicalKey = SaveData.SanitizePersistenceString(key);
                if (canonicalKey.Length == 0)
                    continue;

                if (string.Equals(key, canonicalKey, StringComparison.Ordinal) ||
                    !values.ContainsKey(canonicalKey))
                {
                    values[canonicalKey] = SanitizeNonNegativeFinite(entryValue);
                }
            }

            return true;
        }

        private static bool WriteStringBoolDictionary(
            ref BufferWriter writer,
            Dictionary<string, bool> values,
            int maxCount)
        {
            int count = CountNonBlankStringKeyDictionaryEntries(values, maxCount);
            if (!writer.WriteInt(count))
                return false;

            if (values == null)
                return true;

            int written = 0;
            HashSet<string> writtenKeys = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, bool>.Enumerator enumerator = values.GetEnumerator();
            while (written < count && enumerator.MoveNext())
            {
                KeyValuePair<string, bool> pair = enumerator.Current;
                if (!TryClaimCanonicalDictionaryKey(values, pair.Key, writtenKeys, out string key))
                    continue;

                if (!writer.WriteString(key) || !writer.WriteBool(pair.Value))
                    return false;

                written++;
            }

            enumerator.Dispose();
            return true;
        }

        private static bool ReadStringBoolDictionary(
            ref BufferReader reader,
            out Dictionary<string, bool> values,
            int maxCount,
            string collectionName)
        {
            values = null;
            if (!ReadCollectionCount(ref reader, out int count, maxCount, collectionName))
                return false;

            if (count == NullCollectionCount)
                return true;

            if (!reader.CanConsumeCollectionItems(count, SerializedStringHeaderBytes + SerializedBoolBytes, collectionName))
                return false;

            values = new Dictionary<string, bool>(count);
            for (int i = 0; i < count; i++)
            {
                if (!reader.ReadString(out string key) || !reader.ReadBool(out bool entryValue))
                    return false;

                string canonicalKey = SaveData.SanitizePersistenceString(key);
                if (canonicalKey.Length == 0)
                    continue;

                if (string.Equals(key, canonicalKey, StringComparison.Ordinal) ||
                    !values.ContainsKey(canonicalKey))
                {
                    values[canonicalKey] = entryValue;
                }
            }

            return true;
        }

        private static bool WriteStringStringDictionary(
            ref BufferWriter writer,
            Dictionary<string, string> values,
            int maxCount)
        {
            int count = CountNonBlankStringKeyDictionaryEntries(values, maxCount);
            if (!writer.WriteInt(count))
                return false;

            if (values == null)
                return true;

            int written = 0;
            HashSet<string> writtenKeys = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, string>.Enumerator enumerator = values.GetEnumerator();
            while (written < count && enumerator.MoveNext())
            {
                KeyValuePair<string, string> pair = enumerator.Current;
                if (!TryClaimCanonicalDictionaryKey(values, pair.Key, writtenKeys, out string key))
                    continue;

                if (!writer.WriteString(key) || !writer.WriteString(pair.Value ?? string.Empty))
                    return false;

                written++;
            }

            enumerator.Dispose();
            return true;
        }

        private static bool ReadStringStringDictionary(
            ref BufferReader reader,
            out Dictionary<string, string> values,
            int maxCount,
            string collectionName)
        {
            values = null;
            if (!ReadCollectionCount(ref reader, out int count, maxCount, collectionName))
                return false;

            if (count == NullCollectionCount)
                return true;

            if (!reader.CanConsumeCollectionItems(count, SerializedStringHeaderBytes * 2, collectionName))
                return false;

            values = new Dictionary<string, string>(count);
            for (int i = 0; i < count; i++)
            {
                if (!reader.ReadString(out string key) || !reader.ReadString(out string entryValue))
                    return false;

                string canonicalKey = SaveData.SanitizePersistenceString(key);
                if (canonicalKey.Length == 0)
                    continue;

                if (string.Equals(key, canonicalKey, StringComparison.Ordinal) ||
                    !values.ContainsKey(canonicalKey))
                {
                    values[canonicalKey] = entryValue ?? string.Empty;
                }
            }

            return true;
        }

        private static bool WriteDiscoveredBiomeHashSet(ref BufferWriter writer, HashSet<int> values)
        {
            int count = CountValidDiscoveredBiomeIds(values);
            if (!writer.WriteInt(count))
                return false;

            if (values == null || count == 0)
                return true;

            int written = 0;
            HashSet<int>.Enumerator enumerator = values.GetEnumerator();
            while (written < count && enumerator.MoveNext())
            {
                int biomeId = enumerator.Current;
                if (!BiomeDiscoveryBitMask.IsValidBiomeId(biomeId))
                    continue;

                if (!writer.WriteInt(biomeId))
                    return false;

                written++;
            }

            enumerator.Dispose();
            return true;
        }

        private static int CountValidDiscoveredBiomeIds(HashSet<int> values)
        {
            if (values == null)
                return 0;

            int count = 0;
            HashSet<int>.Enumerator enumerator = values.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (BiomeDiscoveryBitMask.IsValidBiomeId(enumerator.Current))
                    count++;
            }

            enumerator.Dispose();
            return Math.Clamp(count, 0, SaveData.MaxLegacyDiscoveredBiomeIds);
        }

        private static bool ReadDiscoveredBiomeHashSet(
            ref BufferReader reader,
            out HashSet<int> values,
            string collectionName)
        {
            values = null;
            if (!ReadCollectionCount(ref reader, out int count, SaveData.MaxLegacyDiscoveredBiomeIds, collectionName))
                return false;

            if (count == NullCollectionCount)
                return true;

            if (!reader.CanConsumeCollectionItems(count, SerializedIntBytes, collectionName))
                return false;

            values = new HashSet<int>(count);
            for (int i = 0; i < count; i++)
            {
                if (!reader.ReadInt(out int entryValue))
                    return false;

                if (BiomeDiscoveryBitMask.IsValidBiomeId(entryValue))
                    values.Add(entryValue);
            }

            return true;
        }

        private static void SanitizeDiscoveredBiomeIds(HashSet<int> values)
        {
            if (values == null || values.Count == 0)
                return;

            List<int> valuesToRemove = null;
            HashSet<int>.Enumerator enumerator = values.GetEnumerator();
            while (enumerator.MoveNext())
            {
                int biomeId = enumerator.Current;
                if (BiomeDiscoveryBitMask.IsValidBiomeId(biomeId))
                    continue;

                valuesToRemove ??= new List<int>();
                valuesToRemove.Add(biomeId);
            }

            enumerator.Dispose();
            if (valuesToRemove == null)
                return;

            for (int i = 0; i < valuesToRemove.Count; i++)
                values.Remove(valuesToRemove[i]);
        }

        private static bool WriteCustomArraySlice<T>(
            ref BufferWriter writer,
            T[] values,
            int count,
            int maxCount,
            WriteItemDelegate<T> writeItem)
        {
            if (values == null)
                return writer.WriteInt(NullCollectionCount);

            int safeCount = ClampCollectionCount(count, values, maxCount);
            if (!writer.WriteInt(safeCount))
                return false;

            for (int i = 0; i < safeCount; i++)
            {
                if (!writeItem(ref writer, values[i]))
                    return false;
            }

            return true;
        }

        private static bool ReadCustomArray<T>(
            ref BufferReader reader,
            out T[] values,
            ReadItemDelegate<T> readItem,
            int minimumBytesPerElement,
            int maxCount)
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

            if (count > maxCount)
            {
                reader.SetError(typeof(T).Name + " length exceeds the supported range.");
                return false;
            }

            if (count == 0)
            {
                values = Array.Empty<T>();
                return true;
            }

            if (!reader.CanConsumeCollectionItems(count, minimumBytesPerElement, typeof(T).Name))
                return false;

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

        private ref struct BufferWriter
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

            public string Error;

            public int GetBytesWritten()
            {
                return _cursor;
            }

            public bool WriteStruct<T>(T value) where T : unmanaged
            {
                int size = UnsafeUtility.SizeOf<T>();
                if (!TryReserve(size))
                    return false;

                UnsafeUtility.CopyStructureToPtr(ref value, _buffer + _cursor);
                _cursor += size;
                return true;
            }

            public bool WriteStructArraySlice<T>(T[] values, int count) where T : unmanaged
            {
                if (values == null)
                    return WriteInt(NullCollectionCount);

                int safeCount = Math.Clamp(count, 0, values.Length);
                if (!WriteInt(safeCount))
                    return false;

                if (safeCount == 0)
                    return true;

                long byteCountLong = (long)safeCount * UnsafeUtility.SizeOf<T>();
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
                    if (!UnsafeMemoryCopyGuard.TryMemCpy(_buffer + _cursor, _capacity - _cursor, sourcePtr, byteCount))
                    {
                        Error = "Struct array slice copy exceeded the raw buffer ceiling.";
                        UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(SaveBinaryPayloadCodec));
                        return false;
                    }
                }

                _cursor += byteCount;
                return true;
            }

            public bool WriteNativeBytes(NativeArray<byte> source, int byteCount)
            {
                if (!source.IsCreated)
                {
                    Error = "Native source buffer is not initialized.";
                    return false;
                }

                int safeByteCount = Math.Clamp(byteCount, 0, source.Length);
                if (safeByteCount <= 0)
                    return true;

                if (!TryReserve(safeByteCount))
                    return false;

                void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source);
                if (!UnsafeMemoryCopyGuard.TryMemCpy(_buffer + _cursor, _capacity - _cursor, sourcePtr, safeByteCount))
                {
                    Error = "Native shadow payload copy exceeded the raw buffer ceiling.";
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(SaveBinaryPayloadCodec));
                    return false;
                }

                _cursor += safeByteCount;
                return true;
            }

            public bool WriteManagedBytes(byte[] source, int byteCount)
            {
                if (source == null)
                {
                    Error = "Managed source buffer is not initialized.";
                    return false;
                }

                int safeByteCount = Math.Clamp(byteCount, 0, source.Length);
                if (safeByteCount <= 0)
                    return true;

                if (!TryReserve(safeByteCount))
                    return false;

                fixed (byte* sourcePtr = source)
                {
                    if (!UnsafeMemoryCopyGuard.TryMemCpy(_buffer + _cursor, _capacity - _cursor, sourcePtr, safeByteCount))
                    {
                        Error = "Managed shadow payload copy exceeded the raw buffer ceiling.";
                        UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(SaveBinaryPayloadCodec));
                        return false;
                    }
                }

                _cursor += safeByteCount;
                return true;
            }

            public bool WriteZeroBytes(int byteCount)
            {
                if (byteCount <= 0)
                    return true;

                if (!TryReserve(byteCount))
                    return false;

                UnsafeUtility.MemClear(_buffer + _cursor, byteCount);
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
                if (charCount > MaxSerializedStringChars)
                {
                    Error = "String length exceeds the protected block range.";
                    return false;
                }

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
                    if (!UnsafeMemoryCopyGuard.TryMemCpy(_buffer + _cursor, _capacity - _cursor, sourcePtr, byteCount))
                    {
                        Error = "String payload copy exceeded the raw buffer ceiling.";
                        UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(SaveBinaryPayloadCodec));
                        return false;
                    }
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

        private ref struct BufferReader
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

            public string Error;

            public int GetBytesRead()
            {
                return _cursor;
            }

            public bool IsAtEnd()
            {
                return _cursor == _length;
            }

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

            public bool ReadStructArrayBounded<T>(out T[] values, int maxCount, string collectionName) where T : unmanaged
            {
                values = null;
                if (!ReadInt(out int count))
                    return false;

                if (count == NullCollectionCount)
                    return true;

                if (count < 0)
                {
                    SetError(collectionName + " length is negative.");
                    return false;
                }

                if (count > maxCount)
                {
                    SetError(collectionName + " length exceeds the supported range.");
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
                    if (!UnsafeMemoryCopyGuard.TryMemCpy(destinationPtr, byteCount, _buffer + _cursor, byteCount))
                    {
                        SetError("Struct array read copy exceeded the destination byte range.");
                        UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(SaveBinaryPayloadCodec));
                        return false;
                    }
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

                if (byteValue > 1)
                {
                    SetError("Boolean flag byte is outside canonical 0/1 range.");
                    return false;
                }

                value = byteValue == 1;
                return true;
            }

            public bool ReadInt(out int value)
            {
                return ReadStruct(out value);
            }

            public bool TryPeekInt(out int value)
            {
                value = 0;
                if (_cursor < 0 || _cursor > _length - sizeof(int))
                    return false;

                value = UnsafeUtility.ReadArrayElement<int>(_buffer + _cursor, 0);
                return true;
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

                if (charCount > MaxSerializedStringChars)
                {
                    SetError("String length exceeds the protected block range.");
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

            public bool CanConsumeBytes(int byteCount)
            {
                if (byteCount < 0 || _cursor < 0 || _cursor > _length - byteCount)
                {
                    SetError("Save payload read exceeded the available byte range.");
                    return false;
                }

                return true;
            }

            public bool CanConsumeCollectionItems(int count, int minimumBytesPerElement, string collectionName)
            {
                if (count <= 0)
                    return true;

                if (minimumBytesPerElement <= 0)
                {
                    SetError($"{collectionName} minimum element size is invalid.");
                    return false;
                }

                long minimumBytesLong = (long)count * minimumBytesPerElement;
                if (minimumBytesLong > int.MaxValue)
                {
                    SetError($"{collectionName} minimum payload size exceeds the supported range.");
                    return false;
                }

                return CanConsumeBytes((int)minimumBytesLong);
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

#region JulesLink_HuffmanRleSaveDataCompressorCalculator
        private static void JulesLink_HuffmanRleSaveDataCompressorCalculator() { _ = typeof(Hecton8.PureLogic.Systems.HuffmanRleSaveDataCompressorCalculator); }
        #endregion

        #region JulesLink_SaveDataBinaryChecksumCalculator
        private static void JulesLink_SaveDataBinaryChecksumCalculator() { _ = typeof(Hecton8.PureLogic.Systems.SaveDataBinaryChecksumCalculator); }
        #endregion
}

        #region JulesLink_SaveDeltaCompressDiffCalculator
        private static void JulesLink_SaveDeltaCompressDiffCalculator() { _ = typeof(Hecton8.PureLogic.Systems.SaveDeltaCompressDiffCalculator); }
        #endregion

        }
}

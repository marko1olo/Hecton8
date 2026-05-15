using System;
using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Gameplay;
using Hecton8.Narrative;
using Hecton8.World;
using Hecton.Localization;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.SaveSystem
{
    internal static unsafe class SaveBinaryPayloadCodec
    {
        internal const int ProtectedLz4BlockSizeBytes = 16 * 1024;
        private const ushort BiologicalItemStateMask = 1 << 6;
        private const ushort DefaultQualityMilli = 1000;
        private const byte ItemGeneticsGlowFlag = 1 << 0;
        private const byte ItemGeneticsToxicFlag = 1 << 1;
        private const byte ItemGeneticsEdibleFlag = 1 << 2;
        private const byte ItemGeneticsHarvestableFlag = 1 << 3;
        private const byte ItemGeneticsSupportedFlagsMask = ItemGeneticsGlowFlag |
                                                            ItemGeneticsToxicFlag |
                                                            ItemGeneticsEdibleFlag |
                                                            ItemGeneticsHarvestableFlag;
        private const ulong LegacyGlowGeneMask = (ulong)GeneticTraitProfile.GeneticTraitMask.Bioluminescent;
        private const ulong LegacyToxicGeneMask = (ulong)GeneticTraitProfile.GeneticTraitMask.Toxic;
        private const ulong LegacyEdibleGeneMask = (ulong)GeneticTraitProfile.GeneticTraitMask.Medicinal;
        private const ulong LegacyHarvestableGeneMask = (ulong)(
            GeneticTraitProfile.GeneticTraitMask.OxygenProducing |
            GeneticTraitProfile.GeneticTraitMask.FastGrowing |
            GeneticTraitProfile.GeneticTraitMask.Aquatic);
        private const float BiologicalReferenceTemperatureCelsius = 4f;
        private const float BiologicalDecayRatePerSecond = 0.001f;
        private const int NullCollectionCount = -1;
        private const int SuitUpgradeMaskSaveVersion = 65;
        private const int RadiationGridSaveVersion = 68;
        private const int RtgDecaySaveVersion = 70;
        private const int MetaCampaignSaveVersion = 71;
        private const int FirstHourDtoLockSaveVersion = 72;
        private const int ProceduralFaunaStateStrideBytes = 16;
        private const int HibernatedFaunaStateStrideBytes = 112;
        private const int ModuleSorterBufferSlotMax = 8;
        private const int ModuleCultivationSlotMax = 4;
        private const int SerializedStringHeaderBytes = sizeof(int);
        private const int SerializedIntBytes = sizeof(int);
        private const int SerializedLongBytes = sizeof(long);
        private const int SerializedFloatBytes = sizeof(float);
        private const int SerializedBoolBytes = sizeof(byte);
        private const uint WfcOutpostPayloadMagic = 0x57464342u; // WFCB
        private const ushort WfcOutpostPayloadVersion = 1;
        private const byte WfcOutpostPayloadFlagRle = 1 << 0;
        private const byte WfcOutpostPayloadFlagChecksum24 = 1 << 1;
        private const uint WfcOutpostPayloadFlagMask = 0xFFu;
        private const uint WfcOutpostPayloadSupportedFlags = WfcOutpostPayloadFlagRle | WfcOutpostPayloadFlagChecksum24;
        private const int WfcOutpostPayloadChecksumShift = 8;
        private const uint WfcOutpostPayloadChecksumMask = 0x00FFFFFFu;
        private const uint WfcOutpostPayloadChecksumSeed = 2166136261u;

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

        private static void WriteUInt(byte* ptr, int offset, uint value) => *(uint*)(ptr + offset) = value;
        private static void WriteUShort(byte* ptr, int offset, int value) => *(ushort*)(ptr + offset) = (ushort)value;
        private static void WriteInt(byte* ptr, int offset, int value) => *(int*)(ptr + offset) = value;
        private static uint ReadUInt(byte* ptr, int offset) => *(uint*)(ptr + offset);
        private static ushort ReadUShort(byte* ptr, int offset) => *(ushort*)(ptr + offset);
        private static int ReadInt(byte* ptr, int offset) => *(int*)(ptr + offset);

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

            if (data.version != SaveData.CurrentVersion)
                data.version = SaveData.CurrentVersion;

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
            int narrativeDiscoveryCount = ClampCollectionCount(
                data.narrativeDiscoveryCount,
                data.narrativeDiscoveryIds,
                SaveData.MaxNarrativeDiscoveries);
            int corporatePendingOrderCount = ClampPairedListCount(
                data.corporatePendingOrderIds,
                data.corporatePendingOrderTimers,
                SaveData.MaxCorporateOrderIds);

            return writer.WriteInt(data.version)
                && writer.WriteString(data.timestamp)
                && writer.WriteDouble(data.totalPlayTime)
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
                && writer.WriteStruct(data.pdaAdvisories)
                && WriteProceduralLore(ref writer, data.proceduralLore)
                && WriteAchievementRegistry(ref writer, data.achievements)
                && WriteRunModifiers(ref writer, data.runModifiers)
                && WriteMetaCampaign(ref writer, data.metaCampaign)
                && WriteResourceScarcity(ref writer, data.resourceScarcity)
                && writer.WriteStruct(data.environmentalStrain)
                && WriteEcosystemState(ref writer, data.ecosystemState)
                && WriteExternalScavengerSites(ref writer, data.externalScavengerSites)
                && WriteStringFloatDictionary(ref writer, data.toolDurabilityMap, SaveData.MaxToolDurabilityRecords)
                && WriteStringBoolDictionary(ref writer, data.toolBrokenMap, SaveData.MaxToolDurabilityRecords)
                && WriteIntHashSet(ref writer, data.discoveredBiomeIds, SaveData.MaxLegacyDiscoveredBiomeIds)
                && writer.WriteStructArraySlice(data.discoveredBiomeBitWords, BiomeDiscoveryBitMask.WordCount)
                && writer.WriteInt(data.lastDiscoveredBiomeId)
                && writer.WriteInt(narrativeDiscoveryCount)
                && WriteStringArraySlice(
                    ref writer,
                    data.narrativeDiscoveryIds,
                    narrativeDiscoveryCount,
                    SaveData.MaxNarrativeDiscoveries)
                && writer.WriteInt(data.narrativeDepthTier)
                && writer.WriteStruct(data.narrativeAupTriggeredMask)
                && WriteStringList(ref writer, data.audioLogDiscoveredIds, SaveData.MaxLegacyAudioLogDiscoveredIds)
                && WriteAudioLogDiscoveryBitWords(ref writer, data)
                && WriteEncryptedAudioLogFragments(ref writer, data)
                && writer.WriteStructArraySlice(data.industrialLoreUnlockWords, IndustrialLoreBitMask.WordCount)
                && WriteDataArchaeology(ref writer, data)
                && WriteStringList(ref writer, data.questActiveIds, SaveData.MaxLegacyQuestIds)
                && WriteStringList(ref writer, data.questCompletedIds, SaveData.MaxLegacyQuestIds)
                && writer.WriteBool(data.atlasSignalDetected)
                && writer.WriteFloat(data.atlasSignalPulseTimer)
                && writer.WriteInt(data.atlasSignalRevealStage)
                && writer.WriteStruct(data.suitUpgradeMask)
                && WriteStringList(ref writer, data.suitInstalledUpgradeIds, SaveData.MaxSuitUpgradeIds)
                && WriteStringList(ref writer, data.suitUnlockedBlueprintIds, SaveData.MaxSuitUpgradeIds)
                && WriteStringList(ref writer, data.suitBrokenUpgradeIds, SaveData.MaxSuitUpgradeIds)
                && writer.WriteString(data.playerExpressionProfileId)
                && writer.WriteInt(data.atlas6PlayerStatus)
                && writer.WriteInt(data.atlas6BarterCount)
                && writer.WriteBool(data.atlas6DirectiveConflictTriggered)
                && WriteStringList(ref writer, data.corporateReceivedOrderIds, SaveData.MaxCorporateOrderIds)
                && WriteStringList(ref writer, data.corporatePendingOrderIds, corporatePendingOrderCount)
                && WriteFloatList(ref writer, data.corporatePendingOrderTimers, corporatePendingOrderCount)
                && writer.WriteFloat(data.firstHourSessionTime)
                && writer.WriteInt(data.firstHourMilestones)
                && writer.WriteInt(data.firstHourGuidanceFlags)
                && writer.WriteInt(data.endingChoice)
                && writer.WriteBool(data.endingComplete)
                && writer.WriteBool(data.endingConditionMet)
                && WriteStringList(ref writer, data.missionActiveIds, SaveData.MaxMissionIds)
                && WriteStringList(ref writer, data.missionCompletedIds, SaveData.MaxMissionIds)
                && writer.WriteInt(data.LODQualityPreset)
                && writer.WriteBool(data.DynamicResolutionEnabled)
                && WriteRadiationGrid(ref writer, data)
                && WriteRtgDecay(ref writer, data)
                && WriteStringStringDictionary(ref writer, data.CustomModData, SaveData.MaxCustomModDataEntries)
                && WriteFirstHourLockedDtos(ref writer, data);
        }

        private static bool ReadSaveData(ref BufferReader reader, SaveData data)
        {
            if (!reader.ReadInt(out data.version)
                || !reader.ReadString(out data.timestamp)
                || !ReadTotalPlayTime(ref reader, data.version, out data.totalPlayTime)
                || !ReadPlayerStats(ref reader, data.version, out data.playerStats)
                || !ReadInventory(ref reader, data.version, out data.inventory)
                || !ReadWorldState(ref reader, out data.worldState)
                || !ReadProceduralWorldState(ref reader, data.version, out data.proceduralWorldState)
                || !ReadConstruction(ref reader, data.version, out data.construction)
                || !ReadScanLog(ref reader, out data.scanLog)
                || !ReadBarter(ref reader, out data.barter)
                || !ReadFieldOperationLog(ref reader, out data.fieldOperations)
                || !ReadBeaconNetwork(ref reader, out data.beaconNetwork)
                || !ReadExplorationMap(ref reader, data.version, out data.explorationMap)
                || !ReadPdaLogbook(ref reader, data.version, out data.pdaLogbook)
                || !ReadPdaMarkers(ref reader, data.version, out data.pdaMarkers)
                || !reader.ReadStruct(out data.pdaAdvisories)
                || !ReadProceduralLore(ref reader, out data.proceduralLore)
                || !ReadAchievementRegistry(ref reader, out data.achievements)
                || !ReadRunModifiers(ref reader, out data.runModifiers)
                || !ReadMetaCampaign(ref reader, data.version, out data.metaCampaign)
                || !ReadResourceScarcity(ref reader, data.version, out data.resourceScarcity)
                || !reader.ReadStruct(out data.environmentalStrain)
                || !ReadEcosystemState(ref reader, data.version, out data.ecosystemState)
                || !ReadExternalScavengerSites(ref reader, data.version, out data.externalScavengerSites)
                || !ReadStringFloatDictionary(
                    ref reader,
                    out data.toolDurabilityMap,
                    SaveData.MaxToolDurabilityRecords,
                    nameof(data.toolDurabilityMap))
                || !ReadStringBoolDictionary(
                    ref reader,
                    out data.toolBrokenMap,
                    SaveData.MaxToolDurabilityRecords,
                    nameof(data.toolBrokenMap))
                || !ReadIntHashSet(
                    ref reader,
                    out data.discoveredBiomeIds,
                    SaveData.MaxLegacyDiscoveredBiomeIds,
                    nameof(data.discoveredBiomeIds))
                || !reader.ReadStructArrayBounded(
                    out data.discoveredBiomeBitWords,
                    BiomeDiscoveryBitMask.WordCount,
                    nameof(data.discoveredBiomeBitWords))
                || !reader.ReadInt(out data.lastDiscoveredBiomeId)
                || !reader.ReadInt(out data.narrativeDiscoveryCount)
                || !ReadStringArray(
                    ref reader,
                    out data.narrativeDiscoveryIds,
                    SaveData.MaxNarrativeDiscoveries,
                    nameof(data.narrativeDiscoveryIds))
                || !reader.ReadInt(out data.narrativeDepthTier)
                || !ReadNarrativeAupTriggeredMask(ref reader, data.version, out data.narrativeAupTriggeredMask)
                || !ReadStringList(
                    ref reader,
                    out data.audioLogDiscoveredIds,
                    SaveData.MaxLegacyAudioLogDiscoveredIds,
                    nameof(data.audioLogDiscoveredIds))
                || !ReadAudioLogDiscoveryBitWords(ref reader, data.version, data)
                || !ReadEncryptedAudioLogFragments(ref reader, data.version, data)
                || !reader.ReadStructArrayBounded(
                    out data.industrialLoreUnlockWords,
                    IndustrialLoreBitMask.WordCount,
                    nameof(data.industrialLoreUnlockWords))
                || !ReadDataArchaeology(ref reader, data.version, data)
                || !ReadStringList(
                    ref reader,
                    out data.questActiveIds,
                    SaveData.MaxLegacyQuestIds,
                    nameof(data.questActiveIds))
                || !ReadStringList(
                    ref reader,
                    out data.questCompletedIds,
                    SaveData.MaxLegacyQuestIds,
                    nameof(data.questCompletedIds))
                || !reader.ReadBool(out data.atlasSignalDetected)
                || !reader.ReadFloat(out data.atlasSignalPulseTimer)
                || !reader.ReadInt(out data.atlasSignalRevealStage)
                || !ReadSuitUpgradeMask(ref reader, data.version, out data.suitUpgradeMask)
                || !ReadStringList(
                    ref reader,
                    out data.suitInstalledUpgradeIds,
                    SaveData.MaxSuitUpgradeIds,
                    nameof(data.suitInstalledUpgradeIds))
                || !ReadStringList(
                    ref reader,
                    out data.suitUnlockedBlueprintIds,
                    SaveData.MaxSuitUpgradeIds,
                    nameof(data.suitUnlockedBlueprintIds))
                || !ReadStringList(
                    ref reader,
                    out data.suitBrokenUpgradeIds,
                    SaveData.MaxSuitUpgradeIds,
                    nameof(data.suitBrokenUpgradeIds))
                || !reader.ReadString(out data.playerExpressionProfileId)
                || !reader.ReadInt(out data.atlas6PlayerStatus)
                || !reader.ReadInt(out data.atlas6BarterCount)
                || !reader.ReadBool(out data.atlas6DirectiveConflictTriggered)
                || !ReadStringList(
                    ref reader,
                    out data.corporateReceivedOrderIds,
                    SaveData.MaxCorporateOrderIds,
                    nameof(data.corporateReceivedOrderIds))
                || !ReadStringList(
                    ref reader,
                    out data.corporatePendingOrderIds,
                    SaveData.MaxCorporateOrderIds,
                    nameof(data.corporatePendingOrderIds))
                || !ReadFloatList(
                    ref reader,
                    out data.corporatePendingOrderTimers,
                    SaveData.MaxCorporateOrderIds,
                    nameof(data.corporatePendingOrderTimers))
                || !reader.ReadFloat(out data.firstHourSessionTime)
                || !reader.ReadInt(out data.firstHourMilestones)
                || !reader.ReadInt(out data.firstHourGuidanceFlags)
                || !reader.ReadInt(out data.endingChoice)
                || !reader.ReadBool(out data.endingComplete)
                || !reader.ReadBool(out data.endingConditionMet)
                || !ReadStringList(
                    ref reader,
                    out data.missionActiveIds,
                    SaveData.MaxMissionIds,
                    nameof(data.missionActiveIds))
                || !ReadStringList(
                    ref reader,
                    out data.missionCompletedIds,
                    SaveData.MaxMissionIds,
                    nameof(data.missionCompletedIds))
                || !reader.ReadInt(out data.LODQualityPreset)
                || !reader.ReadBool(out data.DynamicResolutionEnabled)
                || !ReadRadiationGrid(ref reader, data.version, data)
                || !ReadRtgDecay(ref reader, data.version, data)
                || !ReadStringStringDictionary(
                    ref reader,
                    out data.CustomModData,
                    SaveData.MaxCustomModDataEntries,
                    nameof(data.CustomModData))
                || !ReadFirstHourLockedDtos(ref reader, data.version, data))
            {
                return false;
            }

            ApplyInventoryBiologicalDecay(ref data.inventory, data.playerStats.environmentTemperature);
            data.voxelDeltaPersistence = VoxelDeltaPersistenceDTO.CreateDefault();
            return true;
        }

        private static bool WriteFirstHourLockedDtos(ref BufferWriter writer, SaveData data)
        {
            PlayerKinematicStateDTO playerState = data != null
                ? PlayerKinematicStateDTO.FromPlayerStats(in data.playerStats)
                : default;
            InventoryShadowDTO inventoryShadow = data != null
                ? InventoryShadowDTO.FromInventory(
                    in data.inventory,
                    data.inventoryShadowPayloadLength,
                    data.inventoryShadowPayloadHash,
                    data.hasInventoryShadowPayload)
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
                int moduleHashId = 0;
                if (construction.moduleBlitRecords != null && i < construction.moduleBlitRecords.Length)
                    moduleHashId = construction.moduleBlitRecords[i].moduleHashId;

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

            if (data.construction.habitatFloodStates == null ||
                data.construction.habitatFloodStates.Length < ConstructionDTO.MaxModules)
            {
                data.construction.habitatFloodStates = new HabitatFloodStateDTO[ConstructionDTO.MaxModules];
            }

            for (int i = 0; i < floodStateCount; i++)
            {
                if (!reader.ReadStruct(out data.construction.habitatFloodStates[i]))
                    return false;
            }

            data.playerKinematicState.ApplyTo(ref data.playerStats);
            data.construction.habitatFloodStateCount = floodStateCount;
            return true;
        }

        private const int EncryptedAudioLogFragmentSaveVersion = 61;
        private const int PackedNarrativeLoreSaveVersion = 62;
        private const int DataArchaeologySaveVersion = 64;
        private const int DataArchaeologyScanStateSaveVersion = 66;
        private const int CartographyFogSaveVersion = 67;

        private static bool WriteRadiationGrid(ref BufferWriter writer, SaveData data)
        {
            byte[] payload = data.radiationGridRle ?? Array.Empty<byte>();
            int safeLength = math.clamp(data.radiationGridRleLength, 0, payload.Length);

            return writer.WriteFloat(data.radiationDose)
                && writer.WriteDouble(data.radiationGridOriginX)
                && writer.WriteDouble(data.radiationGridOriginY)
                && writer.WriteDouble(data.radiationGridOriginZ)
                && writer.WriteFloat(data.radiationGridCellSizeMeters)
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
                data.radiationGridCellSizeMeters = 4f;
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

            int payloadLength = data.radiationGridRle != null ? data.radiationGridRle.Length : 0;
            data.radiationGridRleLength = math.clamp(data.radiationGridRleLength, 0, payloadLength);
            data.radiationGridCellSizeMeters = math.max(0.5f, data.radiationGridCellSizeMeters);
            data.radiationDose = math.max(0f, data.radiationDose);
            return true;
        }

        private static bool WriteRtgDecay(ref BufferWriter writer, SaveData data)
        {
            int sourceLength = data?.rtgDecaySourceIds != null ? data.rtgDecaySourceIds.Length : 0;
            int startLength = data?.rtgStartTimesSeconds != null ? data.rtgStartTimesSeconds.Length : 0;
            int flagLength = data?.rtgDecayFlags != null ? data.rtgDecayFlags.Length : 0;
            int safeCount = data != null
                ? math.clamp(data.rtgDecayCount, 0, math.min(SaveData.MaxRtgDecayRecords, math.min(sourceLength, math.min(startLength, flagLength))))
                : 0;

            return writer.WriteInt(safeCount)
                && writer.WriteStructArraySlice(data != null ? data.rtgDecaySourceIds : null, safeCount)
                && writer.WriteStructArraySlice(data != null ? data.rtgStartTimesSeconds : null, safeCount)
                && writer.WriteStructArraySlice(data != null ? data.rtgDecayFlags : null, safeCount);
        }

        private static bool ReadRtgDecay(ref BufferReader reader, int version, SaveData data)
        {
            if (version < RtgDecaySaveVersion)
            {
                data.rtgDecayCount = 0;
                data.rtgDecaySourceIds = Array.Empty<int>();
                data.rtgStartTimesSeconds = Array.Empty<double>();
                data.rtgDecayFlags = Array.Empty<byte>();
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
            int startLength = data.rtgStartTimesSeconds != null ? data.rtgStartTimesSeconds.Length : 0;
            int flagLength = data.rtgDecayFlags != null ? data.rtgDecayFlags.Length : 0;
            data.rtgDecayCount = math.clamp(
                data.rtgDecayCount,
                0,
                math.min(SaveData.MaxRtgDecayRecords, math.min(sourceLength, math.min(startLength, flagLength))));
            return true;
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
            return saveDataVersion < SuitUpgradeMaskSaveVersion ||
                   reader.ReadStruct(out upgradeMask);
        }

        private static bool WriteDataArchaeology(ref BufferWriter writer, SaveData data)
        {
            long[] words = data != null ? data.dataArchaeologyDiscoveryBitWords : null;
            if (!DataArchaeologyDiscoveryBitMask.HasExpectedCapacity(words))
            {
                for (int i = 0; i < DataArchaeologyDiscoveryBitMask.WordCount; i++)
                {
                    if (!writer.WriteLong(0L))
                        return false;
                }
            }
            else
            {
                for (int i = 0; i < DataArchaeologyDiscoveryBitMask.WordCount; i++)
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
                && writer.WriteStructArraySlice(data != null ? data.dataArchaeologyPartialScanProgressPermille : null, safeCount)
                && WriteDataArchaeologyScanStates(ref writer, data);
        }

        private static bool ReadDataArchaeology(ref BufferReader reader, int saveDataVersion, SaveData data)
        {
            if (data == null)
                return false;

            DataArchaeologyDiscoveryBitMask.EnsureCapacity(ref data.dataArchaeologyDiscoveryBitWords);
            DataArchaeologyDiscoveryBitMask.Clear(data.dataArchaeologyDiscoveryBitWords);
            data.dataArchaeologyPartialScanCount = 0;
            data.dataArchaeologyPartialScanHashes = new uint[SaveData.MaxDataArchaeologyPartialScans];
            data.dataArchaeologyPartialScanProgressPermille = new ushort[SaveData.MaxDataArchaeologyPartialScans];
            data.dataArchaeologyScanStateCount = 0;
            data.dataArchaeologyScanStateKeys = new int[SaveData.MaxDataArchaeologyScanStates];
            data.dataArchaeologyScanStateValues = new byte[SaveData.MaxDataArchaeologyScanStates];

            if (saveDataVersion < DataArchaeologySaveVersion)
                return true;

            for (int i = 0; i < DataArchaeologyDiscoveryBitMask.WordCount; i++)
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
                data.dataArchaeologyPartialScanProgressPermille[i] = partialProgress[i];
            }

            return ReadDataArchaeologyScanStates(ref reader, saveDataVersion, data);
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
                && writer.WriteStructArraySlice(data != null ? data.dataArchaeologyScanStateValues : null, safeCount);
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
                data.dataArchaeologyScanStateValues[i] = values[i];
            }

            return true;
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
            if (data == null)
                return writer.WriteInt(0)
                    && writer.WriteStructArraySlice<uint>(null, 0)
                    && writer.WriteStructArraySlice<uint>(null, 0);

            int safeCount = Math.Clamp(
                data.audioLogEncryptedFragmentCount,
                0,
                Math.Min(
                    SaveData.MaxEncryptedAudioLogFragments,
                    Math.Min(
                        data.audioLogEncryptedFragmentHashes != null ? data.audioLogEncryptedFragmentHashes.Length : 0,
                        data.audioLogEncryptedFragmentBits != null ? data.audioLogEncryptedFragmentBits.Length : 0)));

            return writer.WriteInt(safeCount)
                && writer.WriteStructArraySlice(data.audioLogEncryptedFragmentHashes, safeCount)
                && writer.WriteStructArraySlice(data.audioLogEncryptedFragmentBits, safeCount);
        }

        private static bool ReadEncryptedAudioLogFragments(ref BufferReader reader, int saveDataVersion, SaveData data)
        {
            if (data == null)
                return false;

            if (saveDataVersion < EncryptedAudioLogFragmentSaveVersion)
            {
                data.audioLogEncryptedFragmentCount = 0;
                data.audioLogEncryptedFragmentHashes = new uint[SaveData.MaxEncryptedAudioLogFragments];
                data.audioLogEncryptedFragmentBits = new uint[SaveData.MaxEncryptedAudioLogFragments];
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
            return true;
        }

        private static bool WriteInventory(ref BufferWriter writer, SaveData data)
        {
            if (data != null &&
                data.hasInventoryShadowPayload &&
                data.inventoryShadowPayload.IsCreated &&
                data.inventoryShadowPayloadLength > 0)
            {
                return writer.WriteNativeBytes(data.inventoryShadowPayload, data.inventoryShadowPayloadLength);
            }

            return WriteInventory(ref writer, data != null ? data.inventory : default);
        }

        private static bool WriteInventory(ref BufferWriter writer, InventoryDTO value)
        {
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
            int count = value != null ? Math.Min(value.Length, SaveData.MaxExternalScavengerSites) : 0;
            return writer.WriteStructArraySlice(value, count);
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

            return true;
        }

        private static bool ReadLegacyUInt32ArrayAsUInt64(
            ref BufferReader reader,
            out ulong[] value,
            int maxCount = int.MaxValue)
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
            int maxCount = int.MaxValue)
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
            int maxCount = int.MaxValue)
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

            return (byte)(flags & ItemGeneticsSupportedFlagsMask);
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

            return reader.ReadStructArrayBounded(
                out value,
                SaveData.MaxExternalScavengerSites,
                "externalScavengerSites");
        }

        private static bool WriteWorldState(ref BufferWriter writer, WorldStateDTO value)
        {
            int depletedNodeCount = ClampCollectionCount(
                value.depletedCount,
                value.depletedNodeIds,
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
                && WriteStringArraySlice(ref writer, value.depletedNodeIds, depletedNodeCount, WorldStateDTO.MaxNodes)
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
            return reader.ReadInt(out value.depletedCount)
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
                && writer.WriteStructArraySlice(value.geologySeamStates, seamStateCount)
                && writer.WriteInt(caveEntranceCount)
                && writer.WriteStructArraySlice(value.geologyCaveEntrances, caveEntranceCount)
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
                return true;

            if (!reader.ReadInt(out value.geologySeamStateCount)
                || !reader.ReadStructArrayBounded(
                    out value.geologySeamStates,
                    ProceduralWorldStateDTO.MaxGeologySeamStates,
                    "Procedural geology seam states"))
            {
                return false;
            }

            if (version < 45)
                return true;

            if (!reader.ReadInt(out value.geologyCaveEntranceCount)
                || !reader.ReadStructArrayBounded(
                    out value.geologyCaveEntrances,
                    ProceduralWorldStateDTO.MaxGeologyCaveEntrances,
                    "Procedural geology cave entrances"))
            {
                return false;
            }

            if (version < 46)
                return true;

            return reader.ReadInt(out value.hibernatedFaunaCount)
                && ReadHibernatedFaunaStateArray(ref reader, out value.hibernatedFaunaStates);
        }

        private static int ClampCollectionCount<T>(int count, T[] values, int maxCount)
        {
            int length = values != null ? values.Length : 0;
            int upperBound = Math.Min(Math.Max(maxCount, 0), length);
            return Math.Clamp(count, 0, upperBound);
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
                ProceduralFaunaStateDTO value = values[i];
                if (!writer.WriteLong(value.runtimeKey) ||
                    !writer.WriteFloat(value.cooldownUntilPlayTime) ||
                    !writer.WriteBool(value.isLargeThreatZone) ||
                    !writer.WriteBool(value.blocked) ||
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

                values[i].isLargeThreatZone = isLargeThreatZone;
                values[i].blocked = blocked;
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
                HibernatedFaunaStateDTO value = values[i];
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
                    !writer.WriteBool(value.isLargeThreat) ||
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

                values[i].isLargeThreat = isLargeThreat;
            }

            return true;
        }

        private static bool WriteConstruction(ref BufferWriter writer, ConstructionDTO value)
        {
            int moduleCount = ClampCollectionCount(value.moduleCount, value.modules, ConstructionDTO.MaxModules);
            int graphNodeCount = ClampCollectionCount(value.graphNodeCount, value.graphNodes, ConstructionDTO.MaxModules);
            int graphEdgeCount = ClampCollectionCount(value.graphEdgeCount, value.graphEdges, ConstructionDTO.MaxGraphEdges);
            int moduleBlitCount = ClampCollectionCount(value.moduleBlitCount, value.moduleBlitRecords, ConstructionDTO.MaxModules);

            return writer.WriteInt(moduleCount)
                && WriteModuleArray(ref writer, value.modules, moduleCount)
                && writer.WriteInt(graphNodeCount)
                && WriteModuleGraphNodeArray(ref writer, value.graphNodes, graphNodeCount)
                && writer.WriteInt(graphEdgeCount)
                && WriteModuleGraphEdgeArray(ref writer, value.graphEdges, graphEdgeCount)
                && writer.WriteStructArraySlice(value.moduleBlitRecords, moduleBlitCount);
        }

        private static bool ReadConstruction(ref BufferReader reader, int version, out ConstructionDTO value)
        {
            value = default;
            if (!reader.ReadInt(out value.moduleCount) ||
                !ReadModuleArray(ref reader, version, out value.modules))
            {
                return false;
            }

            if (version >= 47)
            {
                if (!reader.ReadInt(out value.graphNodeCount) ||
                    !ReadModuleGraphNodeArray(ref reader, out value.graphNodes) ||
                    !reader.ReadInt(out value.graphEdgeCount) ||
                    !ReadModuleGraphEdgeArray(ref reader, out value.graphEdges))
                {
                    return false;
                }

                if (version >= 63)
                {
                    if (!reader.ReadStructArrayBounded(
                        out value.moduleBlitRecords,
                        ConstructionDTO.MaxModules,
                        nameof(value.moduleBlitRecords)))
                        return false;

                    value.moduleBlitCount = value.moduleBlitRecords != null
                        ? Math.Clamp(value.moduleBlitRecords.Length, 0, ConstructionDTO.MaxModules)
                        : 0;
                }

                return true;
            }

            value.graphNodeCount = 0;
            value.graphNodes = null;
            value.graphEdgeCount = 0;
            value.graphEdges = null;
            return true;
        }

        private static bool WriteScanLog(ref BufferWriter writer, ScanLogDTO value)
        {
            int entryCount = ClampCollectionCount(value.entryCount, value.entries, ScanLogDTO.MaxEntries);
            int recentCount = ClampCollectionCount(value.recentCount, value.recentEntryIds, ScanLogDTO.MaxRecentEntries);

            return writer.WriteInt(entryCount)
                && WriteScanEntryArray(ref writer, value.entries, entryCount)
                && writer.WriteInt(recentCount)
                && WriteStringArraySlice(ref writer, value.recentEntryIds, recentCount, ScanLogDTO.MaxRecentEntries);
        }

        private static bool ReadScanLog(ref BufferReader reader, out ScanLogDTO value)
        {
            value = default;
            return reader.ReadInt(out value.entryCount)
                && ReadScanEntryArray(ref reader, out value.entries)
                && reader.ReadInt(out value.recentCount)
                && ReadStringArray(
                    ref reader,
                    out value.recentEntryIds,
                    ScanLogDTO.MaxRecentEntries,
                    nameof(value.recentEntryIds));
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
            return reader.ReadInt(out value.stateCount)
                && ReadBarterOfferStateArray(ref reader, out value.offerStates)
                && reader.ReadInt(out value.recentTransactionCount)
                && ReadBarterTransactionArray(ref reader, out value.recentTransactions);
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
            return reader.ReadInt(out value.recentCount)
                && ReadFieldOperationEntryArray(ref reader, out value.recentEntries);
        }

        private static bool WriteBeaconNetwork(ref BufferWriter writer, BeaconNetworkDTO value)
        {
            int activeCount = ClampCollectionCount(value.activeCount, value.entries, BeaconNetworkDTO.MaxEntries);

            return writer.WriteInt(activeCount)
                && writer.WriteInt(value.nextSequence)
                && WriteBeaconEntryArray(ref writer, value.entries, activeCount);
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
            int exploredMortonByteCount = ClampCollectionCount(
                value.exploredMortonByteCount,
                value.exploredMortonMaskBytes,
                ExplorationMapDTO.MortonMaskByteCount);
            int discoveredSectorByteCount = ClampCollectionCount(
                value.discoveredSectorByteCount,
                value.discoveredSectorMaskBytes,
                ExplorationMapDTO.CartographyMaskByteCount);

            return writer.WriteInt(value.exploredChunkCount)
                && writer.WriteInt(value.chunkSizeMeters)
                && writer.WriteInt(value.mortonMaskAxisBits)
                && writer.WriteInt(value.mortonMaskOriginOffset)
                && writer.WriteUInt(value.mortonBuildSalt != 0u ? value.mortonBuildSalt : SaveBinaryStorage.ExplorationMortonBuildSalt32)
                && writer.WriteInt(exploredMortonByteCount)
                && writer.WriteStructArraySlice(value.exploredMortonMaskBytes, exploredMortonByteCount)
                && writer.WriteInt(value.cartographyCellSizeMeters)
                && writer.WriteInt(value.cartographyMaskAxisBits)
                && writer.WriteInt(value.cartographyMaskOriginOffset)
                && writer.WriteInt(discoveredSectorByteCount)
                && writer.WriteStructArraySlice(value.discoveredSectorMaskBytes, discoveredSectorByteCount);
        }

        private static bool ReadExplorationMap(ref BufferReader reader, int version, out ExplorationMapDTO value)
        {
            value = default;
            if (version >= CartographyFogSaveVersion)
            {
                return reader.ReadInt(out value.exploredChunkCount)
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
            }

            if (version >= 56)
            {
                return reader.ReadInt(out value.exploredChunkCount)
                    && reader.ReadInt(out value.chunkSizeMeters)
                    && reader.ReadInt(out value.mortonMaskAxisBits)
                    && reader.ReadInt(out value.mortonMaskOriginOffset)
                    && reader.ReadUInt(out value.mortonBuildSalt)
                    && reader.ReadInt(out value.exploredMortonByteCount)
                    && reader.ReadStructArrayBounded(
                        out value.exploredMortonMaskBytes,
                        ExplorationMapDTO.MortonMaskByteCount,
                        nameof(value.exploredMortonMaskBytes));
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
                return true;
            }

            return reader.ReadInt(out value.chunkSizeMeters)
                && reader.ReadInt(out value.mortonMaskAxisBits)
                && reader.ReadInt(out value.mortonMaskOriginOffset)
                && reader.ReadInt(out value.exploredMortonWordCount)
                && reader.ReadStructArrayBounded(
                    out value.exploredMortonMaskWords,
                    ExplorationMapDTO.MortonMaskWordCount,
                    nameof(value.exploredMortonMaskWords));
        }

        private static bool WritePdaLogbook(ref BufferWriter writer, PDALogbookDTO value)
        {
            int entryCount = ClampCollectionCount(value.entryCount, value.entries, PDALogbookDTO.MaxEntries);
            int seenOriginCount = ClampCollectionCount(
                value.seenOriginCount,
                value.seenOriginHashes,
                PDALogbookDTO.MaxSeenOrigins);

            return writer.WriteInt(entryCount)
                && writer.WriteInt(value.nextSequence)
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

            if (version >= 54)
                return reader.ReadStructArrayBounded(
                    out value.seenOriginHashes,
                    PDALogbookDTO.MaxSeenOrigins,
                    nameof(value.seenOriginHashes));

            return ReadStringArray(
                ref reader,
                out value.seenOriginKeys,
                PDALogbookDTO.MaxSeenOrigins,
                nameof(value.seenOriginKeys));
        }

        private static bool WritePdaMarkers(ref BufferWriter writer, PDAMarkerRegistryDTO value)
        {
            int markerCount = ClampCollectionCount(value.markerCount, value.entries, PDAMarkerRegistryDTO.MaxEntries);

            return writer.WriteInt(markerCount)
                && writer.WriteInt(value.nextSequence)
                && WritePdaMarkerEntryArray(ref writer, value.entries, markerCount);
        }

        private static bool ReadPdaMarkers(ref BufferReader reader, int version, out PDAMarkerRegistryDTO value)
        {
            value = default;
            return reader.ReadInt(out value.markerCount)
                && reader.ReadInt(out value.nextSequence)
                && ReadPdaMarkerEntryArray(ref reader, version, out value.entries);
        }

        private static bool WriteProceduralLore(ref BufferWriter writer, ProceduralLoreStateDTO value)
        {
            int activeCount = ClampCollectionCount(
                value.activeCount,
                value.activePlacements,
                ProceduralLoreStateDTO.MaxActivePlacements);

            return writer.WriteInt(activeCount)
                && writer.WriteInt(value.nextSourceIndex)
                && WriteProceduralLorePlacementArray(ref writer, value.activePlacements, activeCount);
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
            int unlockedCount = ClampCollectionCount(
                value.unlockedCount,
                value.unlockedIds,
                AchievementRegistryDTO.MaxUnlockedAchievements);

            return writer.WriteFloat(value.swamDistanceMeters)
                && writer.WriteInt(value.craftedItemCount)
                && writer.WriteInt(value.discoveredBiomeCount)
                && writer.WriteInt(unlockedCount)
                && WriteStringArraySlice(
                    ref writer,
                    value.unlockedIds,
                    unlockedCount,
                    AchievementRegistryDTO.MaxUnlockedAchievements);
        }

        private static bool ReadAchievementRegistry(ref BufferReader reader, out AchievementRegistryDTO value)
        {
            value = default;
            return reader.ReadFloat(out value.swamDistanceMeters)
                && reader.ReadInt(out value.craftedItemCount)
                && reader.ReadInt(out value.discoveredBiomeCount)
                && reader.ReadInt(out value.unlockedCount)
                && ReadStringArray(
                    ref reader,
                    out value.unlockedIds,
                    AchievementRegistryDTO.MaxUnlockedAchievements,
                    nameof(value.unlockedIds));
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
            int entryCount = ClampPairedCollectionCount(
                value.entryCount,
                ResourceScarcityDTO.MaxTrackedResources,
                value.itemHashIds,
                value.itemIds,
                value.collectedCounts);

            return writer.WriteInt(entryCount)
                && writer.WriteStructArraySlice(value.itemHashIds, entryCount)
                && WriteStringArraySlice(ref writer, value.itemIds, entryCount, ResourceScarcityDTO.MaxTrackedResources)
                && writer.WriteStructArraySlice(value.collectedCounts, entryCount);
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
                    nameof(value.collectedCounts));
        }

        private static bool WriteEcosystemState(ref BufferWriter writer, EcosystemStateDTO value)
        {
            int infectedZoneCount = ClampPairedCollectionCount(
                value.infectedZoneCount,
                EcosystemStateDTO.MaxInfectedZones,
                value.infectedChunkKeys,
                value.infectedSeverities);

            return writer.WriteInt(value.worldSeed)
                && writer.WriteInt(value.worldGenerationVersionId)
                && writer.WriteInt(infectedZoneCount)
                && writer.WriteStructArraySlice(value.infectedChunkKeys, infectedZoneCount)
                && writer.WriteStructArraySlice(value.infectedSeverities, infectedZoneCount);
        }

        private static bool ReadEcosystemState(ref BufferReader reader, int saveDataVersion, out EcosystemStateDTO value)
        {
            value = default;
            if (!reader.ReadInt(out value.worldSeed))
                return false;

            if (saveDataVersion >= 58 && !reader.ReadInt(out value.worldGenerationVersionId))
                return false;

            return reader.ReadInt(out value.infectedZoneCount)
                && reader.ReadStructArrayBounded(
                    out value.infectedChunkKeys,
                    EcosystemStateDTO.MaxInfectedZones,
                    nameof(value.infectedChunkKeys))
                && reader.ReadStructArrayBounded(
                    out value.infectedSeverities,
                    EcosystemStateDTO.MaxInfectedZones,
                    nameof(value.infectedSeverities));
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
                && writer.WriteInt(value.titleHash)
                && writer.WriteInt(value.messageHash)
                && writer.WriteInt(value.originHash);
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
                return reader.ReadInt(out value.titleHash)
                    && reader.ReadInt(out value.messageHash)
                    && reader.ReadInt(out value.originHash);
            }

            if (!reader.ReadString(out value.title)
                || !reader.ReadString(out value.message)
                || !reader.ReadString(out value.originKey))
            {
                return false;
            }

            value.titleHash = LocHash.Compute(value.title);
            value.messageHash = LocHash.Compute(value.message);
            value.originHash = LocHash.Compute(value.originKey);
            return true;
        }

        private static bool WritePdaMarkerEntry(ref BufferWriter writer, in PDAMarkerEntryDTO value)
        {
            return writer.WriteString(value.markerId)
                && writer.WriteString(value.title)
                && writer.WriteInt(value.iconType)
                && writer.WriteFloat(value.posX)
                && writer.WriteFloat(value.posY)
                && writer.WriteFloat(value.posZ)
                && writer.WriteBool(value.visibleOnHud)
                && writer.WriteInt(value.positionEncodingVersion)
                && writer.WriteLong(value.aupGridX)
                && writer.WriteLong(value.aupGridY)
                && writer.WriteLong(value.aupGridZ)
                && writer.WriteFloat(value.aupLocalX)
                && writer.WriteFloat(value.aupLocalY)
                && writer.WriteFloat(value.aupLocalZ);
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
                AbsoluteUniversePosition legacyAup = AbsoluteUniversePosition.FromRuntimePosition(value.GetPosition());
                value.SetAup(in legacyAup);
                return true;
            }

            return reader.ReadInt(out value.positionEncodingVersion)
                && reader.ReadLong(out value.aupGridX)
                && reader.ReadLong(out value.aupGridY)
                && reader.ReadLong(out value.aupGridZ)
                && reader.ReadFloat(out value.aupLocalX)
                && reader.ReadFloat(out value.aupLocalY)
                && reader.ReadFloat(out value.aupLocalZ);
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
            int sorterSlotCount = ClampPairedCollectionCount(
                value.sorterBufferedSlotCount,
                ModuleSorterBufferSlotMax,
                value.sorterBufferedItemIds,
                value.sorterBufferedQuantities);
            int cultivationSlotCount = ClampPairedCollectionCount(
                value.cultivationSlotCount,
                ModuleCultivationSlotMax,
                value.cultivationSeedItemIds,
                value.cultivationGeneticsMasks,
                value.cultivationGrowth01);

            return writer.WriteString(value.prefabId)
                && writer.WriteString(value.slottedToolItemId)
                && writer.WriteString(value.pipeInFlightItemId)
                && writer.WriteInt(value.pipeInFlightAmount)
                && writer.WriteFloat(value.pipeTransitProgress)
                && writer.WriteFloat(value.pipeExportTimerSeconds)
                && writer.WriteString(value.drillBufferedItemId)
                && writer.WriteInt(value.drillBufferedAmount)
                && writer.WriteFloat(value.drillCycleTimerSeconds)
                && writer.WriteInt(sorterSlotCount)
                && WriteStringArraySlice(
                    ref writer,
                    value.sorterBufferedItemIds,
                    sorterSlotCount,
                    ModuleSorterBufferSlotMax)
                && writer.WriteStructArraySlice(value.sorterBufferedQuantities, sorterSlotCount)
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
                && writer.WriteByte(value.failureMode)
                && writer.WriteByte(value.health)
                && writer.WriteFloat(value.floodedReefFloodSeconds)
                && writer.WriteBool(value.interiorReefInfestationActive)
                && writer.WriteInt(cultivationSlotCount)
                && WriteStringArraySlice(
                    ref writer,
                    value.cultivationSeedItemIds,
                    cultivationSlotCount,
                    ModuleCultivationSlotMax)
                && writer.WriteStructArraySlice(value.cultivationGeneticsMasks, cultivationSlotCount)
                && writer.WriteStructArraySlice(value.cultivationGrowth01, cultivationSlotCount)
                && writer.WriteStructArraySlice(value.cultivationQuality01, cultivationSlotCount);
        }

        private static bool ReadModule(ref BufferReader reader, int version, out ModuleDTO value)
        {
            value = default;
            bool ok = reader.ReadString(out value.prefabId)
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
                    nameof(value.sorterBufferedQuantities))
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

            if (version < 48)
            {
                value.cultivationSlotCount = 0;
                value.cultivationSeedItemIds = null;
                value.cultivationGeneticsMasks = null;
                value.cultivationGrowth01 = null;
                value.cultivationQuality01 = null;
                return true;
            }

            ok = reader.ReadInt(out value.cultivationSlotCount)
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
                return reader.ReadStructArrayBounded(
                    out value.cultivationQuality01,
                    ModuleCultivationSlotMax,
                    nameof(value.cultivationQuality01));
            }

            value.cultivationQuality01 = null;
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
            return writer.WriteString(value.prefabId)
                && writer.WriteInt(value.moduleHashId)
                && writer.WriteLong(value.aupGridX)
                && writer.WriteLong(value.aupGridY)
                && writer.WriteLong(value.aupGridZ)
                && writer.WriteFloat(value.aupLocalX)
                && writer.WriteFloat(value.aupLocalY)
                && writer.WriteFloat(value.aupLocalZ)
                && writer.WriteFloat(value.rotX)
                && writer.WriteFloat(value.rotY)
                && writer.WriteFloat(value.rotZ)
                && writer.WriteFloat(value.rotW);
        }

        private static bool ReadModuleGraphNode(ref BufferReader reader, out ModuleGraphNodeDTO value)
        {
            value = default;
            return reader.ReadString(out value.prefabId)
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

        private static bool WriteModuleGraphEdgeArray(ref BufferWriter writer, ModuleGraphEdgeDTO[] values, int count)
        {
            return WriteCustomArraySlice(ref writer, values, count, ConstructionDTO.MaxGraphEdges, WriteModuleGraphEdge);
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

        private static bool WriteStringArraySlice(ref BufferWriter writer, string[] values, int count, int maxCount)
        {
            if (values == null)
                return writer.WriteInt(NullCollectionCount);

            int safeCount = ClampCollectionCount(count, values, maxCount);
            if (!writer.WriteInt(safeCount))
                return false;

            for (int i = 0; i < safeCount; i++)
            {
                if (!writer.WriteString(values[i]))
                    return false;
            }

            return true;
        }

        private static bool ReadStringArray(ref BufferReader reader, out string[] values)
        {
            return ReadStringArray(ref reader, out values, int.MaxValue, "String array");
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

        private static int ClampListCount<T>(List<T> values, int maxCount)
        {
            return values != null
                ? Math.Clamp(values.Count, 0, Math.Max(maxCount, 0))
                : NullCollectionCount;
        }

        private static int ClampDictionaryCount<TKey, TValue>(Dictionary<TKey, TValue> values, int maxCount)
        {
            return values != null
                ? Math.Clamp(values.Count, 0, Math.Max(maxCount, 0))
                : NullCollectionCount;
        }

        private static int ClampHashSetCount<T>(HashSet<T> values, int maxCount)
        {
            return values != null
                ? Math.Clamp(values.Count, 0, Math.Max(maxCount, 0))
                : NullCollectionCount;
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

        private static bool WriteStringList(ref BufferWriter writer, List<string> values)
        {
            return WriteStringList(ref writer, values, int.MaxValue);
        }

        private static bool WriteStringList(ref BufferWriter writer, List<string> values, int maxCount)
        {
            int count = ClampListCount(values, maxCount);
            if (!writer.WriteInt(count))
                return false;

            if (values == null)
                return true;

            for (int i = 0; i < count; i++)
            {
                if (!writer.WriteString(values[i]))
                    return false;
            }

            return true;
        }

        private static bool ReadStringList(ref BufferReader reader, out List<string> values)
        {
            return ReadStringList(ref reader, out values, int.MaxValue, "String list");
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

                values.Add(item);
            }

            return true;
        }

        private static bool WriteFloatList(ref BufferWriter writer, List<float> values)
        {
            return WriteFloatList(ref writer, values, int.MaxValue);
        }

        private static bool WriteFloatList(ref BufferWriter writer, List<float> values, int maxCount)
        {
            int count = ClampListCount(values, maxCount);
            if (!writer.WriteInt(count))
                return false;

            if (values == null)
                return true;

            for (int i = 0; i < count; i++)
            {
                if (!writer.WriteFloat(values[i]))
                    return false;
            }

            return true;
        }

        private static bool ReadFloatList(ref BufferReader reader, out List<float> values)
        {
            return ReadFloatList(ref reader, out values, int.MaxValue, "Float list");
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

                values.Add(item);
            }

            return true;
        }

        private static bool WriteStringFloatDictionary(ref BufferWriter writer, Dictionary<string, float> values)
        {
            return WriteStringFloatDictionary(ref writer, values, int.MaxValue);
        }

        private static bool WriteStringFloatDictionary(
            ref BufferWriter writer,
            Dictionary<string, float> values,
            int maxCount)
        {
            int count = ClampDictionaryCount(values, maxCount);
            if (!writer.WriteInt(count))
                return false;

            if (values == null)
                return true;

            int written = 0;
            Dictionary<string, float>.Enumerator enumerator = values.GetEnumerator();
            while (written < count && enumerator.MoveNext())
            {
                KeyValuePair<string, float> pair = enumerator.Current;
                if (!writer.WriteString(pair.Key) || !writer.WriteFloat(pair.Value))
                    return false;

                written++;
            }

            enumerator.Dispose();
            return true;
        }

        private static bool ReadStringFloatDictionary(ref BufferReader reader, out Dictionary<string, float> values)
        {
            return ReadStringFloatDictionary(ref reader, out values, int.MaxValue, "String-float dictionary");
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

                values[key] = entryValue;
            }

            return true;
        }

        private static bool WriteStringBoolDictionary(ref BufferWriter writer, Dictionary<string, bool> values)
        {
            return WriteStringBoolDictionary(ref writer, values, int.MaxValue);
        }

        private static bool WriteStringBoolDictionary(
            ref BufferWriter writer,
            Dictionary<string, bool> values,
            int maxCount)
        {
            int count = ClampDictionaryCount(values, maxCount);
            if (!writer.WriteInt(count))
                return false;

            if (values == null)
                return true;

            int written = 0;
            Dictionary<string, bool>.Enumerator enumerator = values.GetEnumerator();
            while (written < count && enumerator.MoveNext())
            {
                KeyValuePair<string, bool> pair = enumerator.Current;
                if (!writer.WriteString(pair.Key) || !writer.WriteBool(pair.Value))
                    return false;

                written++;
            }

            enumerator.Dispose();
            return true;
        }

        private static bool ReadStringBoolDictionary(ref BufferReader reader, out Dictionary<string, bool> values)
        {
            return ReadStringBoolDictionary(ref reader, out values, int.MaxValue, "String-bool dictionary");
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

                values[key] = entryValue;
            }

            return true;
        }

        private static bool WriteStringStringDictionary(ref BufferWriter writer, Dictionary<string, string> values)
        {
            return WriteStringStringDictionary(ref writer, values, int.MaxValue);
        }

        private static bool WriteStringStringDictionary(
            ref BufferWriter writer,
            Dictionary<string, string> values,
            int maxCount)
        {
            int count = ClampDictionaryCount(values, maxCount);
            if (!writer.WriteInt(count))
                return false;

            if (values == null)
                return true;

            int written = 0;
            Dictionary<string, string>.Enumerator enumerator = values.GetEnumerator();
            while (written < count && enumerator.MoveNext())
            {
                KeyValuePair<string, string> pair = enumerator.Current;
                if (!writer.WriteString(pair.Key) || !writer.WriteString(pair.Value))
                    return false;

                written++;
            }

            enumerator.Dispose();
            return true;
        }

        private static bool ReadStringStringDictionary(ref BufferReader reader, out Dictionary<string, string> values)
        {
            return ReadStringStringDictionary(ref reader, out values, int.MaxValue, "String-string dictionary");
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

                values[key] = entryValue;
            }

            return true;
        }

        private static bool WriteIntHashSet(ref BufferWriter writer, HashSet<int> values)
        {
            return WriteIntHashSet(ref writer, values, int.MaxValue);
        }

        private static bool WriteIntHashSet(ref BufferWriter writer, HashSet<int> values, int maxCount)
        {
            int count = ClampHashSetCount(values, maxCount);
            if (!writer.WriteInt(count))
                return false;

            if (values == null)
                return true;

            int written = 0;
            HashSet<int>.Enumerator enumerator = values.GetEnumerator();
            while (written < count && enumerator.MoveNext())
            {
                if (!writer.WriteInt(enumerator.Current))
                    return false;

                written++;
            }

            enumerator.Dispose();
            return true;
        }

        private static bool ReadIntHashSet(ref BufferReader reader, out HashSet<int> values)
        {
            return ReadIntHashSet(ref reader, out values, int.MaxValue, "Int hash set");
        }

        private static bool ReadIntHashSet(
            ref BufferReader reader,
            out HashSet<int> values,
            int maxCount,
            string collectionName)
        {
            values = null;
            if (!ReadCollectionCount(ref reader, out int count, maxCount, collectionName))
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

                values.Add(entryValue);
            }

            return true;
        }

        private static bool WriteCustomArray<T>(ref BufferWriter writer, T[] values, WriteItemDelegate<T> writeItem)
        {
            int count = values != null ? values.Length : NullCollectionCount;
            return WriteCustomArraySlice(ref writer, values, count, values != null ? values.Length : 0, writeItem);
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
            int minimumBytesPerElement)
        {
            return ReadCustomArray(
                ref reader,
                out values,
                readItem,
                minimumBytesPerElement,
                int.MaxValue);
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
                    if (!UnsafeMemoryCopyGuard.TryMemCpy(_buffer + _cursor, _capacity - _cursor, sourcePtr, byteCount))
                    {
                        Error = "Struct array copy exceeded the raw buffer ceiling.";
                        UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(SaveBinaryPayloadCodec));
                        return false;
                    }
                }

                _cursor += byteCount;
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
                return ReadStructArrayBounded(out values, int.MaxValue, "Collection");
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
        }
    }
}

import re

def refactor():
    with open('Assets/_Project/Scripts/SaveBinaryStorage.cs', 'r') as f:
        content = f.read()

    # Find the start and end of TryWriteSaveFileIndexedV8
    start = content.find('private static bool TryWriteSaveFileIndexedV8(')
    if start == -1:
        print("Could not find start of TryWriteSaveFileIndexedV8")
        return

    end_pattern = '        private static unsafe bool TryWriteSavePayloadMetadataV8('
    end = content.find(end_pattern, start)

    if end == -1:
        print("Could not find end pattern")
        return

    original = content[start:end]

    new_code = """private static bool TryWriteSaveFileIndexedV8(
            string absolutePath,
            SaveMetadata metadata,
            SaveData data,
            NativeArray<PersistentWorldDeltaRecord>.ReadOnly persistentWorldDeltas,
            NativeArray<EcosystemSectorSaveRecord>.ReadOnly ecosystemSectorStates,
            QuestSaveHeader packedQuestHeader,
            NativeArray<uint> packedQuestStateWords,
            ushort playerDialogueChoiceFlagsSnapshot,
            NativeArray<byte> voxelDeltaSnapshot,
            NativeArray<byte> rawBuffer,
            NativeArray<byte> compressedBuffer,
            int chunkSizeMeters,
            out ulong payloadHash64,
            out int rawPayloadLength,
            out string error)
        {
            payloadHash64 = 0UL;
            rawPayloadLength = 0;
            error = string.Empty;

            if (!TryValidateWriteSaveFileArguments(absolutePath, rawBuffer, compressedBuffer, metadata, data, chunkSizeMeters, out error))
                return false;

            if (!TryPrepareMetadataStrings(metadata, out string sceneName, out string gameVersion, out int sceneBytesLength, out int versionBytesLength, out error))
                return false;

            byte* rawPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rawBuffer);
            byte* filePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(compressedBuffer);
            UnsafeUtility.MemClear(rawPtr, rawBuffer.Length);
            UnsafeUtility.MemClear(filePtr, compressedBuffer.Length);

            ulong timestampUnixMs = ToUnixMilliseconds(metadata.Timestamp);

            if (!TryWriteSavePayloadMetadataV8(
                    metadata,
                    data,
                    packedQuestHeader,
                    packedQuestStateWords,
                    playerDialogueChoiceFlagsSnapshot,
                    ecosystemSectorStates,
                    voxelDeltaSnapshot,
                    rawPtr,
                    rawBuffer.Length,
                    sceneName,
                    gameVersion,
                    sceneBytesLength,
                    versionBytesLength,
                    timestampUnixMs,
                    out uint headerDeltaCount,
                    out int packedQuestOffsetInMetadataPayload,
                    out int metadataRawLength,
                    out ulong metadataHash64,
                    out uint metadataChecksum,
                    out error))
            {
                return false;
            }

            using RegisteredTransientNativeArray<SectorEntry> sectorEntriesOwner = CreateRegisteredTransientNativeArray<SectorEntry>(
                IndexedSectorDirectorySlotCount,
                NativeArrayOptions.ClearMemory,
                IndexedSectorDirectoryScratchLabel);
            NativeArray<SectorEntry> sectorEntries = sectorEntriesOwner.Array;

            int sectorEntrySize = ResolveIndexedSectorEntrySize(CurrentVersion);
            int directoryBytes = IndexedSectorDirectoryHeaderSize + (IndexedSectorDirectorySlotCount * sectorEntrySize);
            int metadataBlockOffset = CurrentHeaderSize + directoryBytes;
            int fileCursor = metadataBlockOffset;

            if (!TryBuildAndSerializeIndexedSectors(
                    persistentWorldDeltas,
                    chunkSizeMeters,
                    rawPtr,
                    metadataRawLength,
                    filePtr,
                    compressedBuffer.Length,
                    sectorEntries,
                    ref fileCursor,
                    out int metadataCompressedSize,
                    out bool anyTokenSubstitution,
                    out int sectorCount,
                    out int totalEntityCount,
                    out error))
            {
                return false;
            }

            return TryFinalizeAndWriteSaveFile(
                absolutePath,
                metadata,
                filePtr,
                fileCursor,
                timestampUnixMs,
                sectorCount,
                chunkSizeMeters,
                metadataCompressedSize,
                metadataRawLength,
                sectorEntries,
                sectorEntrySize,
                directoryBytes,
                metadataHash64,
                metadataChecksum,
                headerDeltaCount,
                totalEntityCount,
                metadataBlockOffset,
                packedQuestOffsetInMetadataPayload,
                anyTokenSubstitution,
                out payloadHash64,
                out rawPayloadLength,
                out error);
        }

        private static bool TryValidateWriteSaveFileArguments(
            string absolutePath,
            NativeArray<byte> rawBuffer,
            NativeArray<byte> compressedBuffer,
            SaveMetadata metadata,
            SaveData data,
            int chunkSizeMeters,
            out string error)
        {
            error = string.Empty;

            if (string.IsNullOrEmpty(absolutePath))
            {
                error = "Save path is empty.";
                return false;
            }

            if (!rawBuffer.IsCreated || !compressedBuffer.IsCreated)
            {
                error = "Native save buffers are not initialized.";
                return false;
            }

            if (metadata == null || data == null)
            {
                error = "Save payload is null.";
                return false;
            }

            if (!TryValidateCurrentBinaryLayouts(out error))
                return false;

            if (chunkSizeMeters <= 0)
            {
                error = "Indexed persistent-world chunk size is invalid.";
                return false;
            }

            return true;
        }

        private static bool TryPrepareMetadataStrings(
            SaveMetadata metadata,
            out string sceneName,
            out string gameVersion,
            out int sceneBytesLength,
            out int versionBytesLength,
            out string error)
        {
            sceneName = SaveMetadata.NormalizeSceneName(metadata.SceneName);
            gameVersion = string.IsNullOrEmpty(metadata.GameVersion) ? "Unknown" : metadata.GameVersion;
            sceneBytesLength = 0;
            versionBytesLength = 0;
            error = string.Empty;

            long sceneBytesLengthLong = (long)sceneName.Length * sizeof(char);
            long versionBytesLengthLong = (long)gameVersion.Length * sizeof(char);
            if (sceneBytesLengthLong > ushort.MaxValue || versionBytesLengthLong > ushort.MaxValue)
            {
                error = "Save metadata strings exceed the payload prefix limits.";
                return false;
            }

            sceneBytesLength = (int)sceneBytesLengthLong;
            versionBytesLength = (int)versionBytesLengthLong;
            return true;
        }

        private static unsafe bool TryBuildAndSerializeIndexedSectors(
            NativeArray<PersistentWorldDeltaRecord>.ReadOnly persistentWorldDeltas,
            int chunkSizeMeters,
            byte* rawPtr,
            int metadataRawLength,
            byte* filePtr,
            int compressedBufferLength,
            NativeArray<SectorEntry> sectorEntries,
            ref int fileCursor,
            out int metadataCompressedSize,
            out bool anyTokenSubstitution,
            out int sectorCount,
            out int totalEntityCount,
            out string error)
        {
            metadataCompressedSize = 0;
            anyTokenSubstitution = false;
            sectorCount = 0;
            totalEntityCount = 0;

            IndexedSectorGroupBuffer sectorGroups = BuildIndexedSectorGroups(persistentWorldDeltas, chunkSizeMeters);
            if (sectorGroups.InvalidRecordCount > 0)
            {
                error = "Indexed persistent world save contains invalid AUP sector records.";
                sectorGroups.Dispose();
                return false;
            }

            sectorCount = sectorGroups.Count;
            if (sectorCount > IndexedSectorDirectorySlotCount)
            {
                long overflowSectorHash = sectorGroups.Groups[IndexedSectorDirectorySlotCount].SectorHash;
                ReportIndexedSectorDirectoryCapacityExceeded(overflowSectorHash, sectorCount);
                sectorCount = IndexedSectorDirectorySlotCount;
            }

            if (!TryWriteIndexedCompressedBlock(
                    rawPtr,
                    metadataRawLength,
                    filePtr,
                    compressedBufferLength,
                    ref fileCursor,
                    out metadataCompressedSize,
                    out uint metadataBlockFlags,
                    out error))
            {
                sectorGroups.Dispose();
                return false;
            }

            anyTokenSubstitution |= (metadataBlockFlags & FlagTokenSubstitution) != 0;

            if (!TryCountIndexedSectorRecords(sectorGroups, sectorCount, out totalEntityCount))
            {
                error = "Indexed persistent-world entity count exceeds supported bounds.";
                sectorGroups.Dispose();
                return false;
            }

            try
            {
                if (!TryProcessIndexedSectorsV8(
                        ref sectorGroups,
                        sectorCount,
                        rawPtr,
                        filePtr,
                        compressedBufferLength,
                        sectorEntries,
                        ref fileCursor,
                        ref anyTokenSubstitution,
                        out error))
                {
                    return false;
                }
            }
            finally
            {
                sectorGroups.Dispose();
            }

            return true;
        }

        private static unsafe bool TryFinalizeAndWriteSaveFile(
            string absolutePath,
            SaveMetadata metadata,
            byte* filePtr,
            int fileCursor,
            ulong timestampUnixMs,
            int sectorCount,
            int chunkSizeMeters,
            int metadataCompressedSize,
            int metadataRawLength,
            NativeArray<SectorEntry> sectorEntries,
            int sectorEntrySize,
            int directoryBytes,
            ulong metadataHash64,
            uint metadataChecksum,
            uint headerDeltaCount,
            int totalEntityCount,
            int metadataBlockOffset,
            int packedQuestOffsetInMetadataPayload,
            bool anyTokenSubstitution,
            out ulong payloadHash64,
            out int rawPayloadLength,
            out string error)
        {
            IndexedSectorDirectoryHeader directoryHeader = new IndexedSectorDirectoryHeader
            {
                SectorCount = (uint)sectorCount,
                ChunkSizeMeters = chunkSizeMeters,
                MetadataCompressedSize = metadataCompressedSize,
                MetadataDecompressedSize = metadataRawLength
            };
            UnsafeUtility.CopyStructureToPtr(ref directoryHeader, filePtr + CurrentHeaderSize);

            int directoryCursor = CurrentHeaderSize + IndexedSectorDirectoryHeaderSize;
            for (int i = 0; i < sectorEntries.Length; i++)
            {
                SectorEntry entry = sectorEntries[i];
                WriteIndexedSectorEntry(filePtr + directoryCursor, sectorEntrySize, in entry);
                directoryCursor += sectorEntrySize;
            }

            ulong directoryHash64 = Hash64(filePtr + CurrentHeaderSize, directoryBytes);
            payloadHash64 = metadataHash64 ^ directoryHash64;
            uint checksumRoot = ComputeIndexedChecksumRoot(metadataChecksum, sectorEntries);
            rawPayloadLength = metadataRawLength;

            SaveFileHeader header = new SaveFileHeader
            {
                MagicValue = Magic,
                Version = CurrentVersion,
                CompatMask = CurrentCompatMask,
                Flags = (byte)(FlagLz4Blocks | FlagIndexedSectorBlocks | FlagProtectedLz4Blocks),
                TimestampUnixMs = timestampUnixMs,
                Checksum = checksumRoot,
                DeltaCount = headerDeltaCount,
                EntityCount = (uint)math.max(totalEntityCount, 0),
                PlayerOffset = (uint)metadataBlockOffset,
                DeltaOffset = (uint)(metadataBlockOffset + packedQuestOffsetInMetadataPayload),
                EntityOffset = (uint)metadataBlockOffset,
                HashPayload64 = payloadHash64,
                HashHeader64 = 0UL
            };

            if (anyTokenSubstitution)
                header.Flags |= FlagTokenSubstitution;

            header.HashHeader64 = ComputeHeaderHash(ref header);
            UnsafeUtility.CopyStructureToPtr(ref header, filePtr);

            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            if (!AsyncWriteManager.WriteAllPaged(absolutePath, filePtr, fileCursor, out error))
                return false;

            metadata.Checksum = FormatPayloadChecksum(in header);
            return true;
        }

"""

    content = content[:start] + new_code + content[end:]

    with open('Assets/_Project/Scripts/SaveBinaryStorage.cs', 'w') as f:
        f.write(content)

refactor()

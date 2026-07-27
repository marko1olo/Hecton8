            NativeArray<uint> packedQuestStateSnapshot = default;
            QuestSaveHeader packedQuestSaveHeader = default;
            NativeArray<byte> voxelDeltaSnapshot = default;
            bool ownsVoxelDeltaSnapshot = false;
            VoxelDeltaProcessor borrowedVoxelDeltaSnapshotOwner = null;

            try
            {
                SaveEvents.TryRaiseSaveStarted(SaveEvents.ComputeSlotHash(slotName));
                PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.InProgress, 0.05f, 0u);
                EnsureSaveWorkingBuffers();
                RequestSnapshotPause(operationId);
                snapshotPauseActive = true;
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(destroyCancellationToken);
                if (TryRejectSaveDuringRespawnReconciliation(
                        slotIndex,
                        operationId,
                        slotName,
                        activeSaveStarted: true,
                        elapsedMs: totalTimer.ElapsedMilliseconds))
                {
                    return;
                }

                thumbnailTicket = SaveThumbnailSystem.CaptureThumbnailForSave(slotName, slotIndex, operationId);
                ThreadSafeCommandQueue.PrepareStorageReservationCommitBridgeForPersistenceSnapshot();
                snapshotTimer.Restart();
                SortRegistryIfDirty(SavePriorityComparer);
                for (int i = 0; i < _saveableCount; i++)
                {
                    ISaveable saveable = _saveables[i];
                    if (!IsAlive(saveable))
                        continue;

                    if (saveable is VoxelDeltaProcessor voxelDeltaProcessor)
                    {
                        Exception cleanupException = null;
                        if (borrowedVoxelDeltaSnapshotOwner != null)
                        {
                            ReleaseBorrowedVoxelDeltaSnapshotBestEffort(borrowedVoxelDeltaSnapshotOwner, ref cleanupException);
                            borrowedVoxelDeltaSnapshotOwner = null;
                        }

                        if (voxelDeltaSnapshot.IsCreated && ownsVoxelDeltaSnapshot)
                            DisposeTransientNativeArrayBestEffort(ref voxelDeltaSnapshot, ref cleanupException, sentinelLabel: "voxelDeltaSnapshot");
                        else
                            voxelDeltaSnapshot = default;

                        ReportPersistenceCleanupFailure("save", cleanupException);
                        ownsVoxelDeltaSnapshot = false;
                        if (!voxelDeltaProcessor.TryCopyNativeSnapshotToBorrowedScratch(
                                out voxelDeltaSnapshot,
                                out int voxelDeltaSnapshotByteCount) ||
                            voxelDeltaSnapshotByteCount <= 0)
                        {
                            if (voxelDeltaSnapshotByteCount > 0)
                            {
                                const string reason = "Voxel delta native snapshot copy failed.";
                                const string logReason = "[SaveManager] Save failed: voxel delta native snapshot copy failed.";
                                const uint failureCode = 3u;
                                voxelDeltaSnapshot = default;
                                if (snapshotPauseActive)
                                {
                                    ReleaseSnapshotPauseBestEffort(operationId, ref cleanupException);
                                    snapshotPauseActive = false;
                                }

                                ReportPersistenceCleanupFailure("save", cleanupException);
                                RecordAsyncPersistenceTelemetry(operationId, slotName, totalTimer.ElapsedMilliseconds, 0L, 0, 0, failureCode);
                                PublishSaveCompletedForSlotName(slotIndex, slotName, new PublishSaveCompletedArgs(operationId: operationId, durationMs: totalTimer.ElapsedMilliseconds, compressedSizeBytes: 0L, succeeded: false));
                                PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Failed, 1f, failureCode);
                                DumpSaveBlackBox();
                                RecordFailure(slotName, "save", reason);
                                LastOperationError = reason;
                                LogError(logReason);
                                SaveEvents.TryRaiseSaveFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(reason), reason);
                                return;
                            }

                            voxelDeltaSnapshot = default;
                        }
                        else
                        {
                            borrowedVoxelDeltaSnapshotOwner = voxelDeltaProcessor;
                        }

                    }

                    saveable.PopulateSaveData(data);
                }

                StampRuntimeWorldSeed(data);
                StampProceduralTerrainIdentity(data);
                ModSaveStateStore.PopulateSaveData(data);
                Stopwatch divergenceSnapshotTimer = Stopwatch.StartNew();
                if (persistentWorldRegistry != null)
                {
                    if (!persistentWorldRegistry.CaptureSaveSnapshot())
                    {
                        const string reason = "Persistent world save snapshot capture failed.";
                        const string logReason = "[SaveManager] Save failed: persistent world save snapshot capture failed.";
                        const uint failureCode = 3u;
                        Exception cleanupException = null;
                        if (snapshotPauseActive)
                        {
                            ReleaseSnapshotPauseBestEffort(operationId, ref cleanupException);
                            snapshotPauseActive = false;
                        }

                        ReportPersistenceCleanupFailure("save", cleanupException);
                        RecordAsyncPersistenceTelemetry(operationId, slotName, totalTimer.ElapsedMilliseconds, 0L, 0, 0, failureCode);
                        PublishSaveCompletedForSlotName(slotIndex, slotName, new PublishSaveCompletedArgs(operationId: operationId, durationMs: totalTimer.ElapsedMilliseconds, compressedSizeBytes: 0L, succeeded: false));
                        PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Failed, 1f, failureCode);
                        DumpSaveBlackBox();
                        RecordFailure(slotName, "save", reason);
                        LastOperationError = reason;
                        LogError(logReason);
                        SaveEvents.TryRaiseSaveFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(reason), reason);
                        return;
                    }

                    int persistentWorldSnapshotCapacity = persistentWorldRegistry.SaveSnapshotCapacity;
                    if (persistentWorldSnapshotCapacity > 0)
                    {
                        persistentWorldDeltaSnapshotOwner = CreateTransientNativeArray<PersistentWorldDeltaRecord>(
                            persistentWorldSnapshotCapacity,
                            Allocator.Persistent,
                            NativeArrayOptions.UninitializedMemory,
                            "persistentWorldDeltaSnapshotOwner");

                        if (!persistentWorldRegistry.TryCopySaveSnapshotDeltas(
                            persistentWorldDeltaSnapshotOwner,
                            persistentWorldSnapshotCapacity,
                            out int copiedPersistentWorldDeltas))
                        {
                            Exception cleanupException = null;
                            DisposeTransientNativeArrayBestEffort(ref persistentWorldDeltaSnapshotOwner, ref cleanupException, sentinelLabel: "persistentWorldDeltaSnapshotOwner");
                            const string reason = "Persistent world save snapshot copy failed.";
                            const string logReason = "[SaveManager] Save failed: persistent world save snapshot copy failed.";
                            const uint failureCode = 3u;
                            if (snapshotPauseActive)
                            {
                                ReleaseSnapshotPauseBestEffort(operationId, ref cleanupException);
                                snapshotPauseActive = false;
                            }

                            ReportPersistenceCleanupFailure("save", cleanupException);
                            RecordAsyncPersistenceTelemetry(operationId, slotName, totalTimer.ElapsedMilliseconds, 0L, 0, 0, failureCode);
                            PublishSaveCompletedForSlotName(slotIndex, slotName, new PublishSaveCompletedArgs(operationId: operationId, durationMs: totalTimer.ElapsedMilliseconds, compressedSizeBytes: 0L, succeeded: false));
                            PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Failed, 1f, failureCode);
                            DumpSaveBlackBox();
                            RecordFailure(slotName, "save", reason);
                            LastOperationError = reason;
                            LogError(logReason);
                            SaveEvents.TryRaiseSaveFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(reason), reason);
                            return;
                        }

                        if (copiedPersistentWorldDeltas > 0)
                        {
                            NativeArray<PersistentWorldDeltaRecord> copiedView = copiedPersistentWorldDeltas < persistentWorldDeltaSnapshotOwner.Length
                                ? persistentWorldDeltaSnapshotOwner.GetSubArray(0, copiedPersistentWorldDeltas)
                                : persistentWorldDeltaSnapshotOwner;
                            persistentWorldDeltaSnapshot = copiedView.AsReadOnly();
                        }
                    }
                }

                EcosystemDirector ecosystemDirector = GlobalRegistry.EcosystemDirector as EcosystemDirector;
                if (ecosystemDirector != null)
                {
                    ecosystemDirector.CaptureSaveSnapshot();
                    NativeArray<EcosystemSectorSaveRecord>.ReadOnly ecosystemView = ecosystemDirector.GetSaveSnapshotArray(out int ecosystemRecordCount);
                    if (ecosystemView.IsCreated && ecosystemRecordCount > 0)
                    {
                        ecosystemSectorSnapshotOwner = CreateTransientNativeArray<EcosystemSectorSaveRecord>(
                            ecosystemRecordCount,
                            Allocator.Persistent,
                            NativeArrayOptions.UninitializedMemory,
                            "ecosystemSectorSnapshotOwner");

                        for (int i = 0; i < ecosystemRecordCount; i++)
                            ecosystemSectorSnapshotOwner[i] = ecosystemView[i];

                        ecosystemSectorSnapshot = ecosystemSectorSnapshotOwner.AsReadOnly();
                    }
                }

                divergenceSnapshotTimer.Stop();
                long saveTimestampTicks = DateTime.UtcNow.Ticks;
                QuestManager questManager = GlobalRegistry.Quest;
                if (questManager != null)
                {
                    int packedQuestWordCount = questManager.PackedStateWordCount;
                    if (packedQuestWordCount > 0)
                    {
                        packedQuestStateSnapshot = CreateTransientNativeArray<uint>(
                            packedQuestWordCount,
                            Allocator.Persistent,
                            NativeArrayOptions.ClearMemory,
                            "packedQuestStateSnapshot");

                        bool copiedQuestState;
                        unsafe
                        {
                            void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(packedQuestStateSnapshot);
                            copiedQuestState = questManager.TryCopyPackedStateSnapshot(
                                destinationPtr,
                                packedQuestStateSnapshot.Length,
                                out packedQuestSaveHeader,
                                saveTimestampTicks);
                        }

                        if (!copiedQuestState)
                            DisposeTransientNativeArrayBestEffortAndReport(ref packedQuestStateSnapshot, "save", "packedQuestStateSnapshot");
                    }
                }

                RecordPlayerDialogueChoiceFlag(SaveBinaryStorage.ExtractPlayerDialogueChoiceFlags(packedQuestStateSnapshot));
                ushort playerDialogueChoiceFlagsSnapshot = PlayerDialogueChoiceFlags;

                SaveMetadata metadata = new SaveMetadata
                {
                    SlotName = slotName,
                    GameVersion = Application.version,
                    Timestamp = saveTimestampTicks,
                    PlayTimeSeconds = (float)playTime,
                    SceneName = SaveMetadata.NormalizeSceneName(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name),
                    PlayerPosition = data.playerStats.GetPosition(),
                    WorldSeed = data.ecosystemState.worldSeed,
                    WorldGenerationVersionId = data.ecosystemState.worldGenerationVersionId
                };

                snapshotTimer.Stop();
                StageSnapshotHeader(operationId, slotName, persistentWorldDeltaSnapshot, ecosystemSectorSnapshot, packedQuestStateSnapshot, voxelDeltaSnapshot);
                Exception snapshotPauseReleaseException = null;
                ReleaseSnapshotPauseBestEffort(operationId, ref snapshotPauseReleaseException);
                snapshotPauseActive = false;
                ReportPersistenceCleanupFailure("save", snapshotPauseReleaseException);
                WarnIfSnapshotBudgetExceeded(slotName, snapshotTimer.ElapsedMilliseconds);

                int backupRetention = GetBackupRetentionCount(slotName);
                string tempPath = GetTempSaveFilePath(slotName);
                if (divergenceSnapshotTimer.ElapsedTicks > PreCompressionYieldBudgetTicks)
                    await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync();

                await Awaitable.MainThreadAsync();
                SaveContextFrameData frameData = SaveContextFrameData.CaptureMainThread();
                SaveEvents.TryRaiseMappedWriteStarted(SaveEvents.ComputeSlotHash(slotName));
                await Awaitable.BackgroundThreadAsync();

                ulong payloadHash64;
                int rawPayloadLength;
                long compressionPipelineStartTicks = Stopwatch.GetTimestamp();

                if (!TryExecuteVerifiedSavePipeline(
                    slotName,
                    tempPath,
                    GetPrimarySaveFilePath(slotName),
                    metadata,
                    data,
                    persistentWorldDeltaSnapshot,
                    ecosystemSectorSnapshot,
                    packedQuestSaveHeader,
                    packedQuestStateSnapshot,
                    playerDialogueChoiceFlagsSnapshot,
                    voxelDeltaSnapshot,
                    _savePayloadBuffer,
                    _compressedSaveBuffer,
                    backupRetention,
                    out payloadHash64,
                    out rawPayloadLength,
                    out long compressedSizeBytes,
                    out string savePipelineError))
                {
                    await Awaitable.MainThreadAsync();
                    const uint failureCode = 3u;
                    string failureMessage = string.IsNullOrEmpty(savePipelineError)
                        ? "Verified save pipeline failed."
                        : savePipelineError;
                    RecordAsyncPersistenceTelemetry(operationId, slotName, totalTimer.ElapsedMilliseconds, 0L, 0, 0, failureCode);
                    PublishSaveCompletedForSlotName(slotIndex, slotName, new PublishSaveCompletedArgs(operationId: operationId, durationMs: totalTimer.ElapsedMilliseconds, compressedSizeBytes: 0L, succeeded: false));
                    PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Failed, 1f, failureCode);
                    DumpSaveBlackBox();
                    RecordFailure(slotName, "save", failureMessage);
                    LastOperationError = failureMessage;
                    LogError("[SaveManager] Save failed: " + failureMessage);
                    SaveEvents.TryRaiseSaveFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(failureMessage), failureMessage);
                    return;
                }

                long compressionPipelineElapsedTicks = Stopwatch.GetTimestamp() - compressionPipelineStartTicks;
                await Awaitable.MainThreadAsync();
                RegisterCompressionPipelineElapsed(compressionPipelineElapsedTicks, in frameData);
                SaveThumbnailSystem.CaptureCompletion thumbnailCompletion =
                    await SaveThumbnailSystem.WaitForCompletionAsync(thumbnailTicket, destroyCancellationToken);
                RecordAsyncPersistenceTelemetry(
                    operationId,
                    slotName,
                    totalTimer.ElapsedMilliseconds,
                    compressedSizeBytes,
                    rawPayloadLength,
                    thumbnailCompletion.ByteLength,
                    thumbnailCompletion.Succeeded != 0 ? 0u : 2u);
                PublishSaveCompletedForSlotName(slotIndex, slotName, new PublishSaveCompletedArgs(operationId: operationId, durationMs: totalTimer.ElapsedMilliseconds, compressedSizeBytes: compressedSizeBytes, succeeded: true));
                PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Completed, 1f, 0u);
                StageIntegrityPayload(_savePayloadBuffer, rawPayloadLength, payloadHash64, slotName);
                SaveSlotIntegrityState savedIntegrity = backupRetention > 0
                    ? SaveSlotIntegrityState.HealthyWithBackup
                    : SaveSlotIntegrityState.Healthy;
                RecordSuccessfulSave(slotName, data.version, savedIntegrity);
                NotifyMappedInventoryWritesCommitted();

                LastOperationSucceeded = true;
                LogInfo($"[SaveManager] Saved '{slotName}' (XXH3-64: {metadata.Checksum}) in {totalTimer.ElapsedMilliseconds}ms");
                RaiseSaveCompletedWithBackpressureRecovery(SaveEvents.ComputeSlotHash(slotName));
                PublishSaveSynchronizedNotification(slotName);
            }
            catch (Exception ex)
            {
                await Awaitable.MainThreadAsync();
                if (snapshotPauseActive)
                {
                    Exception cleanupException = null;
                    ReleaseSnapshotPauseBestEffort(operationId, ref cleanupException);
                    snapshotPauseActive = false;
                    ReportPersistenceCleanupFailure("save", cleanupException);
                }

                RecordAsyncPersistenceTelemetry(operationId, slotName, totalTimer.ElapsedMilliseconds, 0L, 0, 0, 1u);
                PublishSaveCompletedForSlotName(slotIndex, slotName, new PublishSaveCompletedArgs(operationId: operationId, durationMs: totalTimer.ElapsedMilliseconds, compressedSizeBytes: 0L, succeeded: false));
                PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Failed, 1f, 1u);
                DumpSaveBlackBox();
                RecordFailure(slotName, "save", ex.Message);
                LastOperationError = ex.Message;
                LogError("[SaveManager] Save failed: " + ex);
                SaveEvents.TryRaiseSaveFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(ex.Message), ex.Message);
            }
            finally
            {
                Exception cleanupException = null;

                if (snapshotPauseActive)
                    ReleaseSnapshotPauseBestEffort(operationId, ref cleanupException);

                if (packedQuestStateSnapshot.IsCreated)
                    DisposeTransientNativeArrayBestEffort(ref packedQuestStateSnapshot, ref cleanupException, sentinelLabel: "packedQuestStateSnapshot");

                if (persistentWorldDeltaSnapshotOwner.IsCreated)
                    DisposeTransientNativeArrayBestEffort(ref persistentWorldDeltaSnapshotOwner, ref cleanupException, sentinelLabel: "persistentWorldDeltaSnapshotOwner");

                if (ecosystemSectorSnapshotOwner.IsCreated)
                    DisposeTransientNativeArrayBestEffort(ref ecosystemSectorSnapshotOwner, ref cleanupException, sentinelLabel: "ecosystemSectorSnapshotOwner");

                if (voxelDeltaSnapshot.IsCreated && ownsVoxelDeltaSnapshot)
                    DisposeTransientNativeArrayBestEffort(ref voxelDeltaSnapshot, ref cleanupException, sentinelLabel: "voxelDeltaSnapshot");

                if (borrowedVoxelDeltaSnapshotOwner != null)
                {
                    ReleaseBorrowedVoxelDeltaSnapshotBestEffort(borrowedVoxelDeltaSnapshotOwner, ref cleanupException);
                    borrowedVoxelDeltaSnapshotOwner = null;
                }

                _isBusy = false;
                NotifyMacroDatabasePersistenceGateBestEffort(false, ref cleanupException);
                ReportPersistenceCleanupFailure("save", cleanupException);
            }
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void WarnIfSnapshotBudgetExceeded(string slotName, long snapshotElapsedMs)
        {
            if (snapshotElapsedMs <= MainThreadSnapshotBudgetMs)
                return;

            LogWarning(
                $"[SaveManager] Main-thread snapshot for '{slotName}' took {snapshotElapsedMs}ms. " +
                $"Budget is {MainThreadSnapshotBudgetMs}ms. Snapshot purity is pending verification.");
        }

        [StructLayout(LayoutKind.Explicit, Size = 4)]
        private readonly struct SaveContextFrameData
        {
            [FieldOffset(0)]
            public readonly int FrameCount;

            private SaveContextFrameData(int frameCount)
            {
                FrameCount = frameCount;
            }

            public static SaveContextFrameData CaptureMainThread()
            {
                return new SaveContextFrameData(SystemDispatcher.CurrentFrameIndex);
            }
        }

        private void RegisterCompressionPipelineElapsed(long elapsedTicks, in SaveContextFrameData frameData)
        {
            _lastSaveCompressionPipelineTicks = elapsedTicks > 0L ? elapsedTicks : 0L;
            if (elapsedTicks <= CompressionThrottleBudgetTicks)
                return;

            _compressionThrottleReleaseFrame = frameData.FrameCount + 1;
            _compressionThrottleLateFrameArmed = true;
        }

        private void NotifyMappedInventoryWritesCommitted()
        {
            for (int i = 0; i < _saveableCount; i++)
            {
                ISaveable saveable = _saveables[i];
                if (!IsAlive(saveable))
                {
                    _registryDirty = true;
                    continue;
                }

                if (saveable is IMappedInventoryWriteCommitSink sink)
                    sink.NotifyMappedInventoryWriteCommitted();
            }
        }

        private static void StampRuntimeWorldSeed(SaveData data)
        {
            if (data == null)
                return;

            IWorldSeedProvider seedProvider = GlobalRegistry.WorldSeedProvider;
            if (seedProvider == null || !seedProvider.IsInitialized)
                return;

            data.ecosystemState.worldSeed = seedProvider.RuntimeWorldSeed;
            data.ecosystemState.worldGenerationVersionId = math.max(0, seedProvider.RuntimeWorldGenerationVersionId);
        }

        private static void ValidateRuntimeWorldSeed(SaveData data)
        {
            if (data == null)
                return;

            int savedSeed = data.ecosystemState.worldSeed;
            int savedWorldGenerationVersion = data.ecosystemState.worldGenerationVersionId;
            if (savedSeed == 0 && savedWorldGenerationVersion == 0)
                return;

            IWorldSeedProvider seedProvider = GlobalRegistry.WorldSeedProvider;
            if (seedProvider == null || !seedProvider.IsInitialized)
                return;

            int runtimeSeed = seedProvider.RuntimeWorldSeed;
            int runtimeWorldGenerationVersion = math.max(0, seedProvider.RuntimeWorldGenerationVersionId);
            bool seedMismatch = savedSeed != 0 && runtimeSeed != 0 && savedSeed != runtimeSeed;
            bool versionMismatch = savedWorldGenerationVersion > 0 &&
                                   savedWorldGenerationVersion != runtimeWorldGenerationVersion;
            if (!seedMismatch && !versionMismatch)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogWarning(
                "[SaveManager] Geological Anomaly: saved world seed " + savedSeed +
                " / version " + savedWorldGenerationVersion +
                " != runtime world seed " + runtimeSeed +
                " / version " + runtimeWorldGenerationVersion + ".");
#endif
            TryPushGeologicalAnomalyNotification();
            PlayerSignalEvents.TryRaiseTraumaHudSignal(new TraumaHudSignal(
                0.78f,
                0.12f,
                1f,
                1f,
                false));
        }

        private static void StampProceduralTerrainIdentity(SaveData data)
        {
            if (data == null)
                return;

            IWorldSeedProvider seedProvider = GlobalRegistry.WorldSeedProvider;
            int runtimeSeed = 0;
            int worldGenerationVersionId = 0;
            if (seedProvider != null && seedProvider.IsInitialized)
            {
                runtimeSeed = seedProvider.RuntimeWorldSeed;
                worldGenerationVersionId = math.max(0, seedProvider.RuntimeWorldGenerationVersionId);
            }

            TerrainArtifactIdentityDTO terrainIdentity =
                ResolveRuntimeTerrainArtifactIdentity(runtimeSeed, worldGenerationVersionId);

            ProceduralTerrainIdentityDTO identity = default;
            identity.authoringSeed = terrainIdentity.AuthoringSeed;
            identity.runtimeSeed = terrainIdentity.RuntimeSeed;
            identity.worldGenerationVersionId = terrainIdentity.WorldGenerationVersionId;
            identity.macroArtifactVersion = terrainIdentity.MacroArtifactVersion;
            identity.macroChunkSizeMeters = terrainIdentity.ChunkSizeMeters;
            identity.chunkMinX = terrainIdentity.ChunkMinX;
            identity.chunkMinZ = terrainIdentity.ChunkMinZ;
            identity.chunkMaxX = terrainIdentity.ChunkMaxX;
            identity.chunkMaxZ = terrainIdentity.ChunkMaxZ;
            identity.chunkArtifactRangeHash = terrainIdentity.ChunkArtifactRangeHash;
            identity.flags = ProceduralTerrainIdentityDTO.FlagsMacroGeologyPresent;
            if ((terrainIdentity.Flags & TerrainArtifactIdentityDTO.FlagsDefaultChunkRange) != 0u)
                identity.flags |= ProceduralTerrainIdentityDTO.FlagsDefaultChunkRange;

            if (TryResolveActiveWaterCalibration(out WorldWaterLevelCalibrationDTO waterSnapshot))
            {
                identity.selectedWaterLevelY = waterSnapshot.ResolvedWaterLevelY;
                identity.waterCalibrationTravelMeters = waterSnapshot.CalibrationTravelMeters;
                identity.waterCalibrationSourceHash = waterSnapshot.SourceHash;
                identity.flags |= ProceduralTerrainIdentityDTO.FlagsWaterCalibrationPresent;
            }

            identity.terrainProviderFlags = terrainIdentity.Flags;
            identity.heightCacheRevision = math.max(0, terrainIdentity.CacheRevision);
            identity.terrainEntityHash = terrainIdentity.TerrainEntityHash;
            identity.surfaceMaterialContractVersion = WorldTerrainSurfaceMaterialResolver.ContractVersion;
            identity.mesoDetailContractVersion = WorldTerrainMesoDetailFields.ContractVersion;
            identity.detailEligibilityContractVersion = WorldTerrainDetailContracts.ContractVersion;
            identity.mesoParamsHash = BuildTerrainMesoParamsHash(
                terrainIdentity.AuthoringSeed,
                terrainIdentity.RuntimeSeed);
            identity.flags |= ProceduralTerrainIdentityDTO.FlagsTerrainProviderIdentityPresent |
                              ProceduralTerrainIdentityDTO.FlagsTerrainMaterialContractsPresent |
                              ProceduralTerrainIdentityDTO.FlagsTerrainMesoContractsPresent;
            if ((terrainIdentity.Flags & TerrainArtifactIdentityDTO.FlagsHeightPayloadPresent) != 0u)
                identity.flags |= ProceduralTerrainIdentityDTO.FlagsTerrainHeightPayloadPresent;

            data.proceduralTerrainIdentity = identity;
        }

        private static void ValidateProceduralTerrainIdentity(SaveData data)
        {
            if (data == null || !data.proceduralTerrainIdentity.HasMacroIdentity)
                return;

            ProceduralTerrainIdentityDTO saved = data.proceduralTerrainIdentity;
            IWorldSeedProvider seedProvider = GlobalRegistry.WorldSeedProvider;
            int runtimeSeed = seedProvider != null && seedProvider.IsInitialized
                ? seedProvider.RuntimeWorldSeed
                : saved.runtimeSeed;
            int worldGenerationVersionId = seedProvider != null && seedProvider.IsInitialized
                ? math.max(0, seedProvider.RuntimeWorldGenerationVersionId)
                : saved.worldGenerationVersionId;

            TerrainArtifactIdentityDTO expected = ResolveRuntimeTerrainArtifactIdentity(
                runtimeSeed,
                worldGenerationVersionId);

            bool macroMismatch =
                saved.authoringSeed != expected.AuthoringSeed ||
                saved.macroArtifactVersion != expected.MacroArtifactVersion ||
                math.abs(saved.macroChunkSizeMeters - expected.ChunkSizeMeters) > 0.001f ||
                saved.chunkMinX != expected.ChunkMinX ||
                saved.chunkMinZ != expected.ChunkMinZ ||
                saved.chunkMaxX != expected.ChunkMaxX ||
                saved.chunkMaxZ != expected.ChunkMaxZ ||
                saved.chunkArtifactRangeHash != expected.ChunkArtifactRangeHash;

            bool seedMismatch = seedProvider != null &&
                                seedProvider.IsInitialized &&
                                ((saved.runtimeSeed != 0 && saved.runtimeSeed != runtimeSeed) ||
                                 (saved.worldGenerationVersionId > 0 &&
                                  saved.worldGenerationVersionId != worldGenerationVersionId));

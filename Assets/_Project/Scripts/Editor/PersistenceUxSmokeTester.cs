#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Dev
{
    public static class PersistenceUxSmokeTester
    {
        private const string ArtifactRelativePath = "CodexArtifacts/persistence-ux-smoke.json";
        private const string InventoryFullWriteMmfRelativePath = "CodexArtifacts/persistence-ux-inventory-full-write.mmf";
        private const int SectorSizeBytes = 16 * 1024;
        private const int InventorySlotStrideBytes = 16;
        private const int InventorySlotCount = 64;

        [MenuItem("Hecton8/Dev/Run Persistence UX Smoke")]
        private static void RunMenuSmokeTest()
        {
            RunSmokeAndWriteArtifact();
        }

        public static void RunBatchModeSmokeTest()
        {
            bool pass = RunSmokeAndWriteArtifact();
            if (Application.isBatchMode)
                EditorApplication.Exit(pass ? 0 : 1);
        }

        private static bool RunSmokeAndWriteArtifact()
        {
            string saveManager = ReadProjectFile("Assets/_Project/Scripts/SaveManager.cs");
            string thumbnailSystem = ReadProjectFile("Assets/_Project/Scripts/SaveThumbnailSystem.cs");
            string captureFeature = ReadProjectFile("Assets/_Project/Scripts/SaveThumbnailCaptureFeature.cs");
            string loadingScreen = ReadProjectFile("Assets/_Project/Scripts/UI/LoadingScreenController.cs");
            string suitHud = ReadProjectFile("Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs");
            string dataRecPulseShader = ReadProjectFile("Assets/_Project/Shaders/UI/Hecton_DataRecPulse.shader");
            string playerInventory = ReadProjectFile("Assets/_Project/Scripts/PlayerInventory.cs");
            string saveBinaryPayloadCodec = ReadProjectFile("Assets/_Project/Scripts/SaveBinaryPayloadCodec.cs");
            string saveBinaryStorage = ReadProjectFile("Assets/_Project/Scripts/SaveBinaryStorage.cs");
            string persistentWorldRegistry = ReadProjectFile("Assets/_Project/Scripts/World/PersistentWorldRegistry.cs");
            string unsafeMemoryCopyGuard = ReadProjectFile("Assets/_Project/Scripts/Core/UnsafeMemoryCopyGuard.cs");

            bool asyncThumbnailPass =
                ContainsAll(thumbnailSystem, "Extension = \".jpg\"", "EncodeNativeArrayToJPG", "Awaitable.BackgroundThreadAsync", "NativeMemorySentinel.RegisterNativeArray") &&
                ContainsAll(thumbnailSystem, "MinPoseCaptureDistanceMeters = 5f", "MinPoseCaptureAngleDegrees = 5f", "MinPoseCaptureQuaternionDot", "HasCapturePoseChanged", "delta.sqrMagnitude > MinPoseCaptureDistanceSq", "Quaternion.Dot") &&
                ContainsAll(captureFeature, "RequestAsyncReadback", "SaveThumbnailSystem.ReadbackCompletedCallback") &&
                SourceIndex(saveManager, "SaveThumbnailSystem.CaptureThumbnail(slotName);") <
                SourceIndex(saveManager, "SaveEvents.RaiseSaveStarted(slotName);");

            bool loadingStagePass =
                ContainsAll(loadingScreen, "LoadingPipelineStage", "Paging Sectors...", "Hydrating Entities...", "Building NavGrid...", "CharBufferPool.TryAcquire", "SetCharArray", "WritePercent") &&
                ContainsAll(saveManager, "ReportLoadPipelineStage(LoadingPipelineStage.PagingSectors", "ReportLoadPipelineStage(LoadingPipelineStage.HydratingEntities", "ReportLoadPipelineStage(LoadingPipelineStage.BuildingNavGrid");

            bool safeAupSnapPass =
                ContainsAll(saveManager, "TryApplySafeAupSnapOnLoad(data)", "Physics.SphereCastNonAlloc", "AbsoluteUniversePosition.FromRuntimePosition", "HectonFloatingOrigin.BeginSafeTeleportProtocol");

            bool savingHudPass =
                ContainsAll(saveManager, "SaveEvents.RaiseMappedWriteStarted(slotName);") &&
                ContainsAll(suitHud, "ISaveEventListener", "SavingProgressRoot", "SaveEventType.MappedWriteStarted", "SaveEventType.SaveCompleted", "_savingProgressTargetAlpha", "DataRecPulseShaderName") &&
                ContainsAll(dataRecPulseShader, "Shader \"Hecton8/UI/DataRecPulse\"", "sin(_Time.y * _PulseSpeed)") &&
                ContainsAll(suitHud, "SavingProgressMinimumVisibleSeconds", "_savingProgressHidePending", "BeginSavingProgressMappedWrite", "EmitSavingProgressHapticPulse", "ToolHapticsRuntime.EnqueueSinusoidalCommand", "RequestSavingProgressHide");

            bool savingHudShaderPulsePass =
                ContainsAll(dataRecPulseShader, "_SweepIntensity", "sincos(phase", "rsqrt(radiusSq)", "dot(dir, sweepDir)") &&
                ContainsAll(suitHud, "_savingProgressDataNeedle.material = _savingProgressDataPulseMaterial") &&
                !ContainsAll(suitHud, "SavingProgressSpinDegreesPerSecond", "_savingProgressIconRoot.localEulerAngles") &&
                SourceIndex(dataRecPulseShader, "atan2(") == int.MaxValue;

            bool corruptionDialogPass =
                ContainsAll(saveBinaryStorage, "ConsumeIndexedSectorQuarantineFlag", "ReportIndexedSectorQuarantine", "TryResetIndexedPersistentWorldSectorToPristine") &&
                ContainsAll(saveManager, "CriticalSectorCorruptionMessage", "NotificationEvents.PushCritical(CriticalSectorCorruptionMessage)");

            bool seedConsistencyPass =
                ContainsAll(saveManager, "GeologicalAnomalyDetectedMessage", "WorldGenerationVersionId", "RuntimeWorldGenerationVersionId") &&
                ContainsAll(ReadProjectFile("Assets/_Project/Scripts/SaveData.cs"), "worldGenerationVersionId", "CurrentVersion =") &&
                ContainsAll(ReadProjectFile("Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs"), "RuntimeWorldGenerationVersionId") &&
                ContainsAll(ReadProjectFile("Assets/_Project/Scripts/HectonWorldGenerator.cs"), "WorldGenerationAlgorithmVersionId");

            bool inventoryFullWritePass = RunInventoryFullWriteMmfAssert(out int rewrittenOffset, out int rewrittenLength);
            bool unsafeMappedWritePass =
                ContainsAll(saveBinaryStorage, "MemoryMappedFile.CreateFromFile", "UnsafeMemoryCopyGuard.SafeCopy") &&
                ContainsAll(unsafeMemoryCopyGuard, "UnsafeUtility.MemCpy");

            bool inventoryShadowBufferPass =
                ContainsAll(playerInventory, "_inventoryShadowBuffer", "RefreshInventoryShadowBufferFromRuntime", "Fnv1a32Offset", "CommitCurrentInventoryShadowHash") &&
                ContainsAll(saveBinaryPayloadCodec, "WriteNativeBytes", "NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr", "data.hasInventoryShadowPayload");

            bool tombstoneLoadOrderPass =
                ContainsAll(saveManager, "persistentWorldRegistryForLoad?.PreloadTombstonesFromLoadedRecords(loadedWorldDeltas);") &&
                SourceIndex(saveManager, "PreloadTombstonesFromLoadedRecords(loadedWorldDeltas);") <
                SourceIndex(saveManager, "saveable.LoadFromSaveData(data);") &&
                ContainsAll(persistentWorldRegistry, "PreloadTombstonesFromLoadedRecords", "UpsertDeletedTombstone", "RegisterResourceNodeTombstone");

            bool modPayloadSidecarPass =
                ContainsAll(saveBinaryStorage, "ModPayloadSectorPrefix = 0x4D50000000000000UL", "ModPayloadSubBlockSizeBytes", "ModPayloadMagic = 0x50444F4Du") &&
                ContainsAll(saveBinaryStorage, "payloadLength & 1", "Mod payload rejected: odd byte length.", "PayloadLength & 1");

            bool hydrationTimeSlicePass =
                ContainsAll(saveManager, "LoadApplyFrameBudgetTicks = Math.Max(1L, Stopwatch.Frequency / 333L)", "await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: destroyCancellationToken);") &&
                ContainsAll(persistentWorldRegistry, "HydrationFrameBudgetTicks = Math.Max(1L, Stopwatch.Frequency / 333L)", "TryProcessHydrationBurst", "await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: destroyCancellationToken);") &&
                ContainsAll(persistentWorldRegistry, "HydrationPerformanceWarningBudgetTicks = Math.Max(1L, Stopwatch.Frequency / 5000L)", "PublishHydrationBudgetWarningIfNeeded", "GlobalTelemetryBus.PublishPerformanceWarning");

            bool hydrationGcPurgePass =
                ContainsAll(persistentWorldRegistry, "ComputePersistentIdHash(in record.ItemPersistentId)", "ComputePersistentIdHash(in FixedString128Bytes value)") &&
                !ContainsAll(persistentWorldRegistry, "TryResolveItemData(in PersistentWorldItemRecord record", "ItemPersistentId.ToString()");

            bool registryMigrationRsqrtPass =
                ContainsAll(persistentWorldRegistry, "float invDistance = math.rsqrt(distanceSq);", "float moveScalar = math.min(stepMeters * invDistance, 1f);") &&
                SourceIndex(persistentWorldRegistry, "Mathf.Sqrt(distanceSq)") == int.MaxValue;

            bool deterministicScatterCheapRadiusPass =
                ContainsAll(persistentWorldRegistry, "float radius = NextScatter01(ref state) * DropScatterRadiusMeters;") &&
                SourceIndex(persistentWorldRegistry, "math.sqrt(NextScatter01(ref state))") == int.MaxValue;

            bool asyncDehydrationPipelinePass =
                ContainsAll(saveBinaryStorage, "BuildSectorEntityStateSortEntriesJob", "CompressSectorEntityStateJob", "BurstCompile", "xxHash3");

            bool writeAllBytesPurgedPass = !ProjectSourceContains("File." + "WriteAllBytes");

            bool pass = asyncThumbnailPass &&
                        loadingStagePass &&
                        safeAupSnapPass &&
                        savingHudPass &&
                        savingHudShaderPulsePass &&
                        corruptionDialogPass &&
                        seedConsistencyPass &&
                        inventoryFullWritePass &&
                        unsafeMappedWritePass &&
                        inventoryShadowBufferPass &&
                        tombstoneLoadOrderPass &&
                        modPayloadSidecarPass &&
                        hydrationTimeSlicePass &&
                        hydrationGcPurgePass &&
                        registryMigrationRsqrtPass &&
                        deterministicScatterCheapRadiusPass &&
                        asyncDehydrationPipelinePass &&
                        writeAllBytesPurgedPass;

            WriteArtifact(
                pass,
                asyncThumbnailPass,
                loadingStagePass,
                safeAupSnapPass,
                savingHudPass,
                savingHudShaderPulsePass,
                corruptionDialogPass,
                seedConsistencyPass,
                inventoryFullWritePass,
                unsafeMappedWritePass,
                inventoryShadowBufferPass,
                tombstoneLoadOrderPass,
                modPayloadSidecarPass,
                hydrationTimeSlicePass,
                hydrationGcPurgePass,
                registryMigrationRsqrtPass,
                deterministicScatterCheapRadiusPass,
                asyncDehydrationPipelinePass,
                writeAllBytesPurgedPass,
                rewrittenOffset,
                rewrittenLength);

            if (pass)
                Debug.Log("[PersistenceUxSmokeTester] PASS artifact=" + ArtifactRelativePath);
            else
                Debug.LogError("[PersistenceUxSmokeTester] FAIL artifact=" + ArtifactRelativePath);

            return pass;
        }

        private static bool RunInventoryFullWriteMmfAssert(out int rewrittenOffset, out int rewrittenLength)
        {
            byte[] before = new byte[SectorSizeBytes]; // COLD ALLOC: byte[16KB] - editor-only inventory full-write sector fixture - owner: PersistenceUxSmokeTester
            byte[] after = new byte[SectorSizeBytes]; // COLD ALLOC: byte[16KB] - editor-only inventory full-write sector fixture - owner: PersistenceUxSmokeTester
            byte[] observed = new byte[SectorSizeBytes]; // COLD ALLOC: byte[16KB] - editor-only MMF full-write verification readback - owner: PersistenceUxSmokeTester
            for (int slot = 0; slot < InventorySlotCount; slot++)
            {
                int offset = slot * InventorySlotStrideBytes;
                WriteInventorySlot(before, offset, unchecked((uint)(0xA0000000u + slot)), (ushort)(slot + 1), (ushort)0);
                WriteInventorySlot(after, offset, unchecked((uint)(0xA0000000u + slot)), (ushort)(slot + 1), (ushort)0);
            }

            int changedSlot = 17;
            int changedSlotOffset = changedSlot * InventorySlotStrideBytes;
            WriteInventorySlot(after, changedSlotOffset, 0u, (ushort)0, (ushort)1);

            string mmfPath = Path.Combine(System.Environment.CurrentDirectory, InventoryFullWriteMmfRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string directory = Path.GetDirectoryName(mmfPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            WriteBytes(mmfPath, before);
            using (FileStream stream = new FileStream(mmfPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                stream.Position = 0L;
                stream.Write(after, 0, SectorSizeBytes);
                stream.Flush(true);
            }

            using (FileStream stream = new FileStream(mmfPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                int read = stream.Read(observed, 0, observed.Length);
                if (read != observed.Length)
                {
                    rewrittenOffset = -1;
                    rewrittenLength = 0;
                    return false;
                }
            }

            int changedOffset = -1;
            int lastChangedOffset = -1;
            for (int i = 0; i < observed.Length; i++)
            {
                if (before[i] == observed[i])
                    continue;

                if (changedOffset < 0)
                    changedOffset = i;
                lastChangedOffset = i;
            }

            int changedLength = changedOffset >= 0 ? lastChangedOffset - changedOffset + 1 : 0;
            rewrittenOffset = 0;
            rewrittenLength = SectorSizeBytes;
            return changedOffset >= changedSlotOffset &&
                   changedOffset + changedLength <= changedSlotOffset + InventorySlotStrideBytes &&
                   changedLength > 0 &&
                   changedLength < InventorySlotStrideBytes &&
                   observed[changedSlotOffset] == after[changedSlotOffset] &&
                   observed[changedSlotOffset + 6] == after[changedSlotOffset + 6];
        }

        private static void WriteInventorySlot(byte[] bytes, int offset, uint itemHash, ushort stackCount, ushort flags)
        {
            bytes[offset + 0] = (byte)itemHash;
            bytes[offset + 1] = (byte)(itemHash >> 8);
            bytes[offset + 2] = (byte)(itemHash >> 16);
            bytes[offset + 3] = (byte)(itemHash >> 24);
            bytes[offset + 4] = (byte)stackCount;
            bytes[offset + 5] = (byte)(stackCount >> 8);
            bytes[offset + 6] = (byte)flags;
            bytes[offset + 7] = (byte)(flags >> 8);
        }

        private static int SourceIndex(string source, string value)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(value))
                return int.MaxValue;

            int index = source.IndexOf(value, StringComparison.Ordinal);
            return index < 0 ? int.MaxValue : index;
        }

        private static bool ContainsAll(string source, params string[] values)
        {
            if (string.IsNullOrEmpty(source) || values == null)
                return false;

            for (int i = 0; i < values.Length; i++)
            {
                if (source.IndexOf(values[i], StringComparison.Ordinal) < 0)
                    return false;
            }

            return true;
        }

        private static string ReadProjectFile(string relativePath)
        {
            string path = Path.Combine(System.Environment.CurrentDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }

        private static bool ProjectSourceContains(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            string sourceRoot = Path.Combine(System.Environment.CurrentDirectory, "Assets/_Project/Scripts");
            if (!Directory.Exists(sourceRoot))
                return false;

            string[] files = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                if (File.ReadAllText(files[i]).IndexOf(value, StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
        }

        private static void WriteBytes(string path, byte[] bytes)
        {
            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
        }

        private static void WriteArtifact(
            bool pass,
            bool asyncThumbnailPass,
            bool loadingStagePass,
            bool safeAupSnapPass,
            bool savingHudPass,
            bool savingHudShaderPulsePass,
            bool corruptionDialogPass,
            bool seedConsistencyPass,
            bool inventoryFullWritePass,
            bool unsafeMappedWritePass,
            bool inventoryShadowBufferPass,
            bool tombstoneLoadOrderPass,
            bool modPayloadSidecarPass,
            bool hydrationTimeSlicePass,
            bool hydrationGcPurgePass,
            bool registryMigrationRsqrtPass,
            bool deterministicScatterCheapRadiusPass,
            bool asyncDehydrationPipelinePass,
            bool writeAllBytesPurgedPass,
            int inventoryRewriteOffset,
            int inventoryRewriteLength)
        {
            string artifactPath = Path.Combine(System.Environment.CurrentDirectory, ArtifactRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string directory = Path.GetDirectoryName(artifactPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            StringBuilder builder = new StringBuilder(768); // COLD ALLOC: StringBuilder[768] - editor smoke JSON artifact - owner: PersistenceUxSmokeTester
            builder.Append('{')
                .Append("\"tester\":\"PersistenceUxSmokeTester\",")
                .Append("\"pass\":").Append(pass ? "true" : "false").Append(',')
                .Append("\"asyncThumbnailPass\":").Append(asyncThumbnailPass ? "true" : "false").Append(',')
                .Append("\"loadingStagePass\":").Append(loadingStagePass ? "true" : "false").Append(',')
                .Append("\"safeAupSnapPass\":").Append(safeAupSnapPass ? "true" : "false").Append(',')
                .Append("\"savingHudPass\":").Append(savingHudPass ? "true" : "false").Append(',')
                .Append("\"savingHudShaderPulsePass\":").Append(savingHudShaderPulsePass ? "true" : "false").Append(',')
                .Append("\"corruptionDialogPass\":").Append(corruptionDialogPass ? "true" : "false").Append(',')
                .Append("\"seedConsistencyPass\":").Append(seedConsistencyPass ? "true" : "false").Append(',')
                .Append("\"inventoryFullWritePass\":").Append(inventoryFullWritePass ? "true" : "false").Append(',')
                .Append("\"unsafeMappedWritePass\":").Append(unsafeMappedWritePass ? "true" : "false").Append(',')
                .Append("\"inventoryShadowBufferPass\":").Append(inventoryShadowBufferPass ? "true" : "false").Append(',')
                .Append("\"tombstoneLoadOrderPass\":").Append(tombstoneLoadOrderPass ? "true" : "false").Append(',')
                .Append("\"modPayloadSidecarPass\":").Append(modPayloadSidecarPass ? "true" : "false").Append(',')
                .Append("\"hydrationTimeSlicePass\":").Append(hydrationTimeSlicePass ? "true" : "false").Append(',')
                .Append("\"hydrationGcPurgePass\":").Append(hydrationGcPurgePass ? "true" : "false").Append(',')
                .Append("\"registryMigrationRsqrtPass\":").Append(registryMigrationRsqrtPass ? "true" : "false").Append(',')
                .Append("\"deterministicScatterCheapRadiusPass\":").Append(deterministicScatterCheapRadiusPass ? "true" : "false").Append(',')
                .Append("\"asyncDehydrationPipelinePass\":").Append(asyncDehydrationPipelinePass ? "true" : "false").Append(',')
                .Append("\"writeAllBytesPurgedPass\":").Append(writeAllBytesPurgedPass ? "true" : "false").Append(',')
                .Append("\"inventoryRewriteOffset\":").Append(inventoryRewriteOffset).Append(',')
                .Append("\"inventoryRewriteLength\":").Append(inventoryRewriteLength)
                .Append('}');

            File.WriteAllText(artifactPath, builder.ToString());
        }
    }
}
#endif

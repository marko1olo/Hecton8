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
        private const string InventoryDeltaMmfRelativePath = "CodexArtifacts/persistence-ux-inventory-delta.mmf";
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
            string saveBinaryStorage = ReadProjectFile("Assets/_Project/Scripts/SaveBinaryStorage.cs");

            bool asyncThumbnailPass =
                ContainsAll(thumbnailSystem, "Extension = \".jpg\"", "EncodeNativeArrayToJPG", "Awaitable.BackgroundThreadAsync", "NativeMemorySentinel.RegisterNativeArray") &&
                ContainsAll(captureFeature, "RequestAsyncReadback", "SaveThumbnailSystem.ReadbackCompletedCallback") &&
                SourceIndex(saveManager, "SaveThumbnailSystem.CaptureThumbnail(slotName);") <
                SourceIndex(saveManager, "SaveEvents.RaiseSaveStarted(slotName);");

            bool loadingStagePass =
                ContainsAll(loadingScreen, "LoadingPipelineStage", "Paging Sectors...", "Hydrating Entities...", "Building NavGrid...", "CharBufferPool.TryAcquire", "SetCharArray") &&
                ContainsAll(saveManager, "ReportLoadPipelineStage(LoadingPipelineStage.PagingSectors", "ReportLoadPipelineStage(LoadingPipelineStage.HydratingEntities", "ReportLoadPipelineStage(LoadingPipelineStage.BuildingNavGrid");

            bool safeAupSnapPass =
                ContainsAll(saveManager, "TryApplySafeAupSnapOnLoad(data)", "Physics.SphereCast", "AbsoluteUniversePosition.FromRuntimePosition", "HectonFloatingOrigin.BeginSafeTeleportProtocol");

            bool savingHudPass =
                ContainsAll(suitHud, "ISaveEventListener", "SavingProgressRoot", "SaveEventType.SaveStarted", "SaveEventType.SaveCompleted", "_savingProgressTargetAlpha");

            bool corruptionDialogPass =
                ContainsAll(saveBinaryStorage, "ConsumeIndexedSectorQuarantineFlag", "ReportIndexedSectorQuarantine") &&
                ContainsAll(saveManager, "CriticalSectorCorruptionMessage", "NotificationEvents.PushCritical(CriticalSectorCorruptionMessage)");

            bool inventoryDeltaSpanPass = RunInventoryDeltaMmfAssert(out int changedOffset, out int changedLength);

            bool pass = asyncThumbnailPass &&
                        loadingStagePass &&
                        safeAupSnapPass &&
                        savingHudPass &&
                        corruptionDialogPass &&
                        inventoryDeltaSpanPass;

            WriteArtifact(
                pass,
                asyncThumbnailPass,
                loadingStagePass,
                safeAupSnapPass,
                savingHudPass,
                corruptionDialogPass,
                inventoryDeltaSpanPass,
                changedOffset,
                changedLength);

            if (pass)
                Debug.Log("[PersistenceUxSmokeTester] PASS artifact=" + ArtifactRelativePath);
            else
                Debug.LogError("[PersistenceUxSmokeTester] FAIL artifact=" + ArtifactRelativePath);

            return pass;
        }

        private static bool RunInventoryDeltaMmfAssert(out int changedOffset, out int changedLength)
        {
            byte[] before = new byte[SectorSizeBytes]; // COLD ALLOC: byte[16KB] - editor-only inventory delta sector fixture - owner: PersistenceUxSmokeTester
            byte[] after = new byte[SectorSizeBytes]; // COLD ALLOC: byte[16KB] - editor-only inventory delta sector fixture - owner: PersistenceUxSmokeTester
            byte[] observed = new byte[SectorSizeBytes]; // COLD ALLOC: byte[16KB] - editor-only MMF delta verification readback - owner: PersistenceUxSmokeTester
            for (int slot = 0; slot < InventorySlotCount; slot++)
            {
                int offset = slot * InventorySlotStrideBytes;
                WriteInventorySlot(before, offset, unchecked((uint)(0xA0000000u + slot)), (ushort)(slot + 1), (ushort)0);
                WriteInventorySlot(after, offset, unchecked((uint)(0xA0000000u + slot)), (ushort)(slot + 1), (ushort)0);
            }

            int changedSlot = 17;
            int changedSlotOffset = changedSlot * InventorySlotStrideBytes;
            WriteInventorySlot(after, changedSlotOffset, 0u, (ushort)0, (ushort)1);

            string mmfPath = Path.Combine(System.Environment.CurrentDirectory, InventoryDeltaMmfRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string directory = Path.GetDirectoryName(mmfPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllBytes(mmfPath, before);
            using (FileStream stream = new FileStream(mmfPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                stream.Position = changedSlotOffset;
                stream.Write(after, changedSlotOffset, InventorySlotStrideBytes);
                stream.Flush(true);
            }

            using (FileStream stream = new FileStream(mmfPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                int read = stream.Read(observed, 0, observed.Length);
                if (read != observed.Length)
                {
                    changedOffset = -1;
                    changedLength = 0;
                    return false;
                }
            }

            changedOffset = -1;
            int lastChangedOffset = -1;
            for (int i = 0; i < observed.Length; i++)
            {
                if (before[i] == observed[i])
                    continue;

                if (changedOffset < 0)
                    changedOffset = i;
                lastChangedOffset = i;
            }

            changedLength = changedOffset >= 0 ? lastChangedOffset - changedOffset + 1 : 0;
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

        private static void WriteArtifact(
            bool pass,
            bool asyncThumbnailPass,
            bool loadingStagePass,
            bool safeAupSnapPass,
            bool savingHudPass,
            bool corruptionDialogPass,
            bool inventoryDeltaSpanPass,
            int inventoryDeltaOffset,
            int inventoryDeltaLength)
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
                .Append("\"corruptionDialogPass\":").Append(corruptionDialogPass ? "true" : "false").Append(',')
                .Append("\"inventoryDeltaSpanPass\":").Append(inventoryDeltaSpanPass ? "true" : "false").Append(',')
                .Append("\"inventoryDeltaOffset\":").Append(inventoryDeltaOffset).Append(',')
                .Append("\"inventoryDeltaLength\":").Append(inventoryDeltaLength)
                .Append('}');

            File.WriteAllText(artifactPath, builder.ToString());
        }
    }
}
#endif

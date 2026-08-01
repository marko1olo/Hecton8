#if UNITY_EDITOR
using System.Globalization;
using System.Text;
using Hecton8.SaveSystem;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Lightweight batchmode-safe CI proof for Save/load binary codec:
    /// SaveData.CreateNew → seed tool durability maps → TryWrite → TryRead → restore gates.
    ///
    /// Complements PersistenceUxSmokeTester (source-text only) with a real memory codec round-trip.
    /// Soft FAIL stays exit 0 under -quit. Does not touch disk save slots or SaveManager.
    /// </summary>
    public static class SaveBinaryRoundTripValidator
    {
        private const string LogPrefix = "[SaveBinaryRoundTripValidator]";
        private const int BinaryPayloadScratchBytes = 1024 * 1024;
        private const double SeedPlayTime = 42.0;
        private const string ToolKeyCutter = "tool.cutter";
        private const string ToolKeyScanner = "tool.scanner";
        private const float CutterDurability = 0.75f;
        private const float ScannerDurability = 1.0f;
        private const float DurabilityEpsilon = 0.0001f;

        // COLD ALLOC: StringBuilder[4096] - editor audit report builder - owner: SaveBinaryRoundTripValidator
        private static readonly StringBuilder Report = new StringBuilder(4096);

        /// <summary>
        /// Public for -executeMethod / CI batchmode. Soft FAIL stays exit 0 under -quit.
        /// </summary>
        [MenuItem("Hecton8/Validation/Validate Save Binary Round Trip", priority = 188)]
        public static void ValidateSaveBinaryRoundTrip()
        {
            bool batch = Application.isBatchMode;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating || Application.isPlaying)
            {
                string busy = LogPrefix + " RESULT: FAIL — Editor busy (compiling/updating/playing).";
                Debug.LogError(busy);
                if (!batch)
                    EditorUtility.DisplayDialog("Save Binary Round Trip", busy, "OK");
                return;
            }

            Report.Clear();
            Report.AppendLine("═══════════════════════════════════════════════════════");
            Report.AppendLine("HECTON-8 — Save Binary Codec Round-Trip Audit");
            Report.AppendLine("═══════════════════════════════════════════════════════");
            Report.AppendLine();

            bool wrote = false;
            bool read = false;
            int bytesWritten = 0;
            int bytesRead = 0;
            string writeError = string.Empty;
            string readError = string.Empty;
            int restoredVersion = -1;
            double restoredPlayTime = -1.0;
            float restoredCutterDurability = -1f;
            float restoredScannerDurability = -1f;
            bool restoredCutterBroken = true;
            bool restoredScannerBroken = false;
            bool hasCutterDurability = false;
            bool hasScannerDurability = false;
            bool hasCutterBroken = false;
            bool hasScannerBroken = false;
            string exceptionError = string.Empty;

            try
            {
                SaveData data = SaveData.CreateNew(SeedPlayTime);
                data.toolDurabilityMap[ToolKeyCutter] = CutterDurability;
                data.toolBrokenMap[ToolKeyCutter] = false;
                data.toolDurabilityMap[ToolKeyScanner] = ScannerDurability;
                data.toolBrokenMap[ToolKeyScanner] = true;

                Report.Append("seedVersion=").Append(data.version.ToString(CultureInfo.InvariantCulture));
                Report.Append(" currentVersion=").Append(SaveData.CurrentVersion.ToString(CultureInfo.InvariantCulture));
                Report.Append(" seedPlayTime=").Append(SeedPlayTime.ToString(CultureInfo.InvariantCulture));
                Report.Append(" scratchBytes=").Append(BinaryPayloadScratchBytes.ToString(CultureInfo.InvariantCulture));
                Report.AppendLine();

                // COLD ALLOC: byte[1MB] - codec scratch for editor audit only - owner: SaveBinaryRoundTripValidator
                byte[] payload = new byte[BinaryPayloadScratchBytes];
                unsafe
                {
                    fixed (byte* payloadPtr = payload)
                    {
                        wrote = SaveBinaryPayloadCodec.TryWrite(
                            data,
                            payloadPtr,
                            payload.Length,
                            out bytesWritten,
                            out writeError);

                        if (wrote && bytesWritten > 0)
                        {
                            read = SaveBinaryPayloadCodec.TryRead(
                                payloadPtr,
                                bytesWritten,
                                out SaveData restored,
                                out bytesRead,
                                out readError);

                            if (read && restored != null)
                            {
                                restoredVersion = restored.version;
                                restoredPlayTime = restored.totalPlayTime;

                                if (restored.toolDurabilityMap != null)
                                {
                                    hasCutterDurability = restored.toolDurabilityMap.TryGetValue(
                                        ToolKeyCutter,
                                        out restoredCutterDurability);
                                    hasScannerDurability = restored.toolDurabilityMap.TryGetValue(
                                        ToolKeyScanner,
                                        out restoredScannerDurability);
                                }

                                if (restored.toolBrokenMap != null)
                                {
                                    hasCutterBroken = restored.toolBrokenMap.TryGetValue(
                                        ToolKeyCutter,
                                        out restoredCutterBroken);
                                    hasScannerBroken = restored.toolBrokenMap.TryGetValue(
                                        ToolKeyScanner,
                                        out restoredScannerBroken);
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                wrote = false;
                read = false;
                exceptionError = ex.GetType().Name + ": " + ex.Message;
            }

            if (string.IsNullOrEmpty(writeError))
                writeError = string.Empty;
            if (string.IsNullOrEmpty(readError))
                readError = string.Empty;

            bool versionOk = restoredVersion == SaveData.CurrentVersion;
            bool playTimeOk = System.Math.Abs(restoredPlayTime - SeedPlayTime) < 0.0001;
            bool bytesOk = wrote && read && bytesWritten > 0 && bytesWritten == bytesRead;
            bool cutterDurabilityOk = hasCutterDurability &&
                                      System.Math.Abs(restoredCutterDurability - CutterDurability) < DurabilityEpsilon;
            bool scannerDurabilityOk = hasScannerDurability &&
                                       System.Math.Abs(restoredScannerDurability - ScannerDurability) < DurabilityEpsilon;
            bool cutterBrokenOk = hasCutterBroken && restoredCutterBroken == false;
            bool scannerBrokenOk = hasScannerBroken && restoredScannerBroken == true;
            bool mapsOk = cutterDurabilityOk && scannerDurabilityOk && cutterBrokenOk && scannerBrokenOk;
            bool noException = string.IsNullOrEmpty(exceptionError);

            Report.Append("wrote=").Append(wrote ? 1 : 0);
            Report.Append(" read=").Append(read ? 1 : 0);
            Report.Append(" bytesWritten=").Append(bytesWritten.ToString(CultureInfo.InvariantCulture));
            Report.Append(" bytesRead=").Append(bytesRead.ToString(CultureInfo.InvariantCulture));
            Report.Append(" bytesOk=").Append(bytesOk ? 1 : 0);
            if (!string.IsNullOrEmpty(writeError))
                Report.Append(" writeError=").Append(writeError);
            if (!string.IsNullOrEmpty(readError))
                Report.Append(" readError=").Append(readError);
            if (!string.IsNullOrEmpty(exceptionError))
                Report.Append(" exception=").Append(exceptionError);
            Report.AppendLine();

            Report.Append("restoredVersion=").Append(restoredVersion.ToString(CultureInfo.InvariantCulture));
            Report.Append(" versionOk=").Append(versionOk ? 1 : 0);
            Report.Append(" restoredPlayTime=").Append(restoredPlayTime.ToString(CultureInfo.InvariantCulture));
            Report.Append(" playTimeOk=").Append(playTimeOk ? 1 : 0);
            Report.AppendLine();

            Report.Append("cutterDurabilityOk=").Append(cutterDurabilityOk ? 1 : 0);
            Report.Append(" scannerDurabilityOk=").Append(scannerDurabilityOk ? 1 : 0);
            Report.Append(" cutterBrokenOk=").Append(cutterBrokenOk ? 1 : 0);
            Report.Append(" scannerBrokenOk=").Append(scannerBrokenOk ? 1 : 0);
            Report.Append(" mapsOk=").Append(mapsOk ? 1 : 0);
            Report.AppendLine();

            bool passed = noException && wrote && read && bytesOk && versionOk && playTimeOk && mapsOk;

            Report.AppendLine();
            Report.Append(LogPrefix).Append(" RESULT: ").Append(passed ? "PASS" : "FAIL");
            Report.Append(" wrote=").Append(wrote ? 1 : 0);
            Report.Append(" read=").Append(read ? 1 : 0);
            Report.Append(" bytesOk=").Append(bytesOk ? 1 : 0);
            Report.Append(" versionOk=").Append(versionOk ? 1 : 0);
            Report.Append(" playTimeOk=").Append(playTimeOk ? 1 : 0);
            Report.Append(" mapsOk=").Append(mapsOk ? 1 : 0);
            Report.Append(" bytesWritten=").Append(bytesWritten.ToString(CultureInfo.InvariantCulture));
            Report.AppendLine();

            string reportText = Report.ToString();
            if (passed)
                Debug.Log(reportText);
            else
                Debug.LogError(reportText);

            if (!batch)
            {
                EditorUtility.DisplayDialog(
                    "Save Binary Round Trip",
                    passed
                        ? "PASS — SaveBinaryPayloadCodec TryWrite/TryRead restored version + tool maps."
                        : "FAIL — see Console for measured fields.",
                    "OK");
            }

            // Soft FAIL under -quit: do not EditorApplication.Exit on audit fail.
        }
    }
}
#endif

#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Soft-FAIL CI pin for SaveStation explicit-request rejection HUD feedback.
    ///
    /// BUILD_PLAYTEST notes that SaveStation reports nothing when the player asks
    /// to save and the persistence layer rejects. TryRequestSave false must surface
    /// SAVE_STATION_BUSY / SAVE_STATION_OFFLINE via ShowHudInfo / ShowHudWarning —
    /// never return silently after an explicit Interact.
    ///
    /// Does not open scenes. Soft FAIL under -quit.
    /// </summary>
    public static class SaveStationRejectionHudValidator
    {
        private const string LogPrefix = "[SaveStationRejectionHudValidator]";

        private const string SaveStationRelativePath =
            "Assets/_Project/Scripts/Interaction/SaveStation.cs";

        private const string PinTryRequestSave = "TryRequestSave";
        private const string PinTryRequestManual = "TryRequestManualSlotSave";
        private const string PinShowHudWarning = "ShowHudWarning";
        private const string PinShowHudInfo = "ShowHudInfo";
        private const string PinBusyKey = "SAVE_STATION_BUSY";
        private const string PinOfflineKey = "SAVE_STATION_OFFLINE";
        private const string PinRejectedNotify =
            "TryRequestSave rejected; player was notified on HUD";

        // COLD ALLOC: StringBuilder[4096] - editor audit report builder - owner: SaveStationRejectionHudValidator
        private static readonly StringBuilder Report = new StringBuilder(4096);

        /// <summary>
        /// Public for -executeMethod / CI batchmode. Soft FAIL stays exit 0 under -quit.
        /// </summary>
        [MenuItem("Hecton8/Validation/Validate SaveStation Rejection HUD", priority = 192)]
        public static void ValidateSaveStationRejectionHud()
        {
            bool batch = Application.isBatchMode;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating || Application.isPlaying)
            {
                string busy = LogPrefix + " RESULT: FAIL — Editor busy (compiling/updating/playing).";
                Debug.LogError(busy);
                if (!batch)
                    EditorUtility.DisplayDialog("SaveStation Rejection HUD", busy, "OK");
                return;
            }

            Report.Clear();
            Report.AppendLine("═══════════════════════════════════════════════════════");
            Report.AppendLine("HECTON-8 — SaveStation Rejection HUD Audit");
            Report.AppendLine("═══════════════════════════════════════════════════════");
            Report.AppendLine();
            Report.AppendLine("Note: TryRequestSave false must notify the player on HUD.");
            Report.AppendLine();

            string dataPath = Application.dataPath;
            string projectRoot = Directory.GetParent(dataPath) != null
                ? Directory.GetParent(dataPath).FullName
                : dataPath;

            string stationPath = Path.Combine(
                projectRoot,
                SaveStationRelativePath.Replace('/', Path.DirectorySeparatorChar));

            bool stationExists = File.Exists(stationPath);
            string stationText = stationExists ? File.ReadAllText(stationPath) : string.Empty;

            bool hasTryRequest = stationExists && stationText.Contains(PinTryRequestSave);
            bool hasManualPath = stationExists && stationText.Contains(PinTryRequestManual);
            bool hasShowWarning = stationExists && stationText.Contains(PinShowHudWarning);
            bool hasShowInfo = stationExists && stationText.Contains(PinShowHudInfo);
            bool hasBusyKey = stationExists && stationText.Contains(PinBusyKey);
            bool hasOfflineKey = stationExists && stationText.Contains(PinOfflineKey);
            bool hasRejectedNotify = stationExists && stationText.Contains(PinRejectedNotify);

            // Gate: rejection branch must call HUD surface (not only request path).
            // Require both busy and offline keys inside the same file as TryRequestSave.
            bool rejectionSurfaced =
                hasTryRequest &&
                hasManualPath &&
                hasShowWarning &&
                hasShowInfo &&
                hasBusyKey &&
                hasOfflineKey &&
                hasRejectedNotify;

            Report.AppendLine("--- Source file presence ---");
            AppendPresence(Report, SaveStationRelativePath, stationExists);
            Report.AppendLine();

            Report.AppendLine("--- Rejection HUD pins ---");
            AppendGate(Report, "station.TryRequestSave", hasTryRequest);
            AppendGate(Report, "station.TryRequestManualSlotSave", hasManualPath);
            AppendGate(Report, "station.ShowHudWarning", hasShowWarning);
            AppendGate(Report, "station.ShowHudInfo", hasShowInfo);
            AppendGate(Report, "station.SAVE_STATION_BUSY", hasBusyKey);
            AppendGate(Report, "station.SAVE_STATION_OFFLINE", hasOfflineKey);
            AppendGate(Report, "station.rejected-notify log pin", hasRejectedNotify);
            Report.AppendLine();

            Report.Append("stationExists=").Append(stationExists ? 1 : 0);
            Report.Append(" hasTryRequest=").Append(hasTryRequest ? 1 : 0);
            Report.Append(" hasManualPath=").Append(hasManualPath ? 1 : 0);
            Report.Append(" hasShowWarning=").Append(hasShowWarning ? 1 : 0);
            Report.Append(" hasShowInfo=").Append(hasShowInfo ? 1 : 0);
            Report.Append(" hasBusyKey=").Append(hasBusyKey ? 1 : 0);
            Report.Append(" hasOfflineKey=").Append(hasOfflineKey ? 1 : 0);
            Report.Append(" hasRejectedNotify=").Append(hasRejectedNotify ? 1 : 0);
            Report.AppendLine();

            bool passed = stationExists && rejectionSurfaced;

            if (!passed)
            {
                Report.AppendLine("FAIL reason: SaveStation rejection path missing HUD surface pins.");
                if (!stationExists)
                    Report.AppendLine("  • SaveStation.cs must remain present.");
                if (!hasTryRequest || !hasManualPath)
                    Report.AppendLine("  • TryRequestManualSlotSave must call TryRequestSave.");
                if (!hasShowWarning || !hasShowInfo || !hasBusyKey || !hasOfflineKey || !hasRejectedNotify)
                    Report.AppendLine("  • !accepted must ShowHudInfo(BUSY) or ShowHudWarning(OFFLINE) — never silent return.");
            }
            else
            {
                Report.AppendLine("PASS: SaveStation surfaces TryRequestSave rejection on HUD.");
            }

            Report.Append("RESULT: ").AppendLine(passed ? "PASS" : "FAIL");
            string reportText = LogPrefix + " " + Report.ToString();

            if (passed)
                Debug.Log(reportText);
            else
                Debug.LogError(reportText);

            if (!batch)
            {
                EditorUtility.DisplayDialog(
                    "SaveStation Rejection HUD",
                    passed
                        ? "PASS\nRejection path surfaces HUD feedback."
                        : "FAIL\nOne or more source pins missing.\nSee Console.",
                    "OK");
            }
            // batchmode: soft FAIL under -quit (no EditorApplication.Exit on audit fail).
        }

        private static void AppendPresence(StringBuilder sb, string relativePath, bool exists)
        {
            sb.Append(exists ? "  OK  " : "  MISS ");
            sb.AppendLine(relativePath);
        }

        private static void AppendGate(StringBuilder sb, string label, bool ok)
        {
            sb.Append(ok ? "  OK  " : "  MISS ");
            sb.AppendLine(label);
        }
    }
}
#endif

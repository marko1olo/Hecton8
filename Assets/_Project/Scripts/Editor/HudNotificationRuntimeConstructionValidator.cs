#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Soft-FAIL CI pin for HUD notification / save-failure bridge runtime construction.
    ///
    /// HUDNotification and HUDSaveNotificationLink are intentionally ABSENT from every live
    /// scene/prefab — they are constructed at runtime:
    ///   - GameBootstrapper.EnsureHudNotificationRegistered → AddComponent<HUDNotification>
    ///   - SuitHUDV4CanvasOverlay.EnsureSaveFailureNotificationBridgeCold → AddComponent<HUDSaveNotificationLink>
    ///
    /// This validator pins those source paths so authoring cannot regress to "zero instances /
    /// no bridge" without a CI RESULT: FAIL line. Does not open scenes. Soft FAIL under -quit.
    /// </summary>
    public static class HudNotificationRuntimeConstructionValidator
    {
        private const string LogPrefix = "[HudNotificationRuntimeConstructionValidator]";

        private const string BootstrapRelativePath =
            "Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs";
        private const string OverlayRelativePath =
            "Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs";
        private const string LinkRelativePath =
            "Assets/_Project/Scripts/UI/HUDSaveNotificationLink.cs";
        private const string HudRelativePath =
            "Assets/_Project/Scripts/HUDNotification.cs";

        private const string PinEnsureHudRegistered = "EnsureHudNotificationRegistered";
        private const string PinAddHudNotification = "AddComponent<HUDNotification>";
        private const string PinEnsureSaveBridge = "EnsureSaveFailureNotificationBridgeCold";
        private const string PinAddSaveLink = "AddComponent<HUDSaveNotificationLink>";
        private const string PinShowCritical = "ShowCritical";
        private const string PinTryGetActive = "TryGetActive";

        // COLD ALLOC: StringBuilder[4096] - editor audit report builder - owner: HudNotificationRuntimeConstructionValidator
        private static readonly StringBuilder Report = new StringBuilder(4096);

        /// <summary>
        /// Public for -executeMethod / CI batchmode. Soft FAIL stays exit 0 under -quit.
        /// </summary>
        [MenuItem("Hecton8/Validation/Validate HUD Notification Runtime Construction", priority = 190)]
        public static void ValidateHudNotificationRuntimeConstruction()
        {
            bool batch = Application.isBatchMode;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating || Application.isPlaying)
            {
                string busy = LogPrefix + " RESULT: FAIL — Editor busy (compiling/updating/playing).";
                Debug.LogError(busy);
                if (!batch)
                    EditorUtility.DisplayDialog("HUD Notification Runtime Construction", busy, "OK");
                return;
            }

            Report.Clear();
            Report.AppendLine("═══════════════════════════════════════════════════════");
            Report.AppendLine("HECTON-8 — HUD Notification Runtime Construction Audit");
            Report.AppendLine("═══════════════════════════════════════════════════════");
            Report.AppendLine();
            Report.AppendLine("Note: HUDNotification + HUDSaveNotificationLink are runtime-only");
            Report.AppendLine("(scene/prefab GUID absence is EXPECTED; do not pin presence).");
            Report.AppendLine();

            string dataPath = Application.dataPath;
            // Application.dataPath ends in /Assets — climb one level to project root.
            string projectRoot = Directory.GetParent(dataPath) != null
                ? Directory.GetParent(dataPath).FullName
                : dataPath;

            string bootstrapPath = Path.Combine(projectRoot, BootstrapRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string overlayPath = Path.Combine(projectRoot, OverlayRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string linkPath = Path.Combine(projectRoot, LinkRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string hudPath = Path.Combine(projectRoot, HudRelativePath.Replace('/', Path.DirectorySeparatorChar));

            bool bootstrapExists = File.Exists(bootstrapPath);
            bool overlayExists = File.Exists(overlayPath);
            bool linkExists = File.Exists(linkPath);
            bool hudExists = File.Exists(hudPath);

            string bootstrapText = bootstrapExists ? File.ReadAllText(bootstrapPath) : string.Empty;
            string overlayText = overlayExists ? File.ReadAllText(overlayPath) : string.Empty;
            string linkText = linkExists ? File.ReadAllText(linkPath) : string.Empty;
            string hudText = hudExists ? File.ReadAllText(hudPath) : string.Empty;

            bool bootstrapHasEnsure = bootstrapExists && bootstrapText.Contains(PinEnsureHudRegistered);
            bool bootstrapHasAdd = bootstrapExists && bootstrapText.Contains(PinAddHudNotification);
            bool overlayHasEnsure = overlayExists && overlayText.Contains(PinEnsureSaveBridge);
            bool overlayHasAdd = overlayExists && overlayText.Contains(PinAddSaveLink);
            bool linkHasShowCritical = linkExists && linkText.Contains(PinShowCritical);
            bool hudHasTryGetActive = hudExists && hudText.Contains(PinTryGetActive);
            bool hudHasShowCritical = hudExists && hudText.Contains(PinShowCritical);

            Report.AppendLine("--- Source file presence ---");
            AppendPresence(Report, BootstrapRelativePath, bootstrapExists);
            AppendPresence(Report, OverlayRelativePath, overlayExists);
            AppendPresence(Report, LinkRelativePath, linkExists);
            AppendPresence(Report, HudRelativePath, hudExists);
            Report.AppendLine();

            Report.AppendLine("--- Construction pins ---");
            AppendGate(Report, "bootstrap.EnsureHudNotificationRegistered", bootstrapHasEnsure);
            AppendGate(Report, "bootstrap.AddComponent<HUDNotification>", bootstrapHasAdd);
            AppendGate(Report, "overlay.EnsureSaveFailureNotificationBridgeCold", overlayHasEnsure);
            AppendGate(Report, "overlay.AddComponent<HUDSaveNotificationLink>", overlayHasAdd);
            AppendGate(Report, "link.ShowCritical", linkHasShowCritical);
            AppendGate(Report, "hud.TryGetActive", hudHasTryGetActive);
            AppendGate(Report, "hud.ShowCritical", hudHasShowCritical);
            Report.AppendLine();

            Report.Append("bootstrapExists=").Append(bootstrapExists ? 1 : 0);
            Report.Append(" overlayExists=").Append(overlayExists ? 1 : 0);
            Report.Append(" linkExists=").Append(linkExists ? 1 : 0);
            Report.Append(" hudExists=").Append(hudExists ? 1 : 0);
            Report.Append(" bootstrapHasEnsure=").Append(bootstrapHasEnsure ? 1 : 0);
            Report.Append(" bootstrapHasAdd=").Append(bootstrapHasAdd ? 1 : 0);
            Report.Append(" overlayHasEnsure=").Append(overlayHasEnsure ? 1 : 0);
            Report.Append(" overlayHasAdd=").Append(overlayHasAdd ? 1 : 0);
            Report.Append(" linkHasShowCritical=").Append(linkHasShowCritical ? 1 : 0);
            Report.Append(" hudHasTryGetActive=").Append(hudHasTryGetActive ? 1 : 0);
            Report.Append(" hudHasShowCritical=").Append(hudHasShowCritical ? 1 : 0);
            Report.AppendLine();

            bool passed =
                bootstrapExists &&
                overlayExists &&
                linkExists &&
                hudExists &&
                bootstrapHasEnsure &&
                bootstrapHasAdd &&
                overlayHasEnsure &&
                overlayHasAdd &&
                linkHasShowCritical &&
                hudHasTryGetActive &&
                hudHasShowCritical;

            if (!passed)
            {
                Report.AppendLine("FAIL reason: one or more runtime construction source pins missing.");
                if (!bootstrapExists || !bootstrapHasEnsure || !bootstrapHasAdd)
                    Report.AppendLine("  • GameBootstrapper must own EnsureHudNotificationRegistered + AddComponent<HUDNotification>.");
                if (!overlayExists || !overlayHasEnsure || !overlayHasAdd)
                    Report.AppendLine("  • SuitHUDV4CanvasOverlay must own EnsureSaveFailureNotificationBridgeCold + AddComponent<HUDSaveNotificationLink>.");
                if (!linkExists || !linkHasShowCritical)
                    Report.AppendLine("  • HUDSaveNotificationLink must call ShowCritical on failure payloads.");
                if (!hudExists || !hudHasTryGetActive || !hudHasShowCritical)
                    Report.AppendLine("  • HUDNotification must expose TryGetActive + ShowCritical.");
            }
            else
            {
                Report.AppendLine("PASS: runtime construction pins present for HUD notification + save-failure bridge.");
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
                    "HUD Notification Runtime Construction",
                    passed
                        ? "PASS\nAll runtime construction source pins present."
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

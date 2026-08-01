#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Soft-FAIL CI pin for player-critical audio runtime construction.
    ///
    /// PlayerCriticalProceduralAudioRenderer (GUID d837e0b45d8800643bbc1f384302325a) and
    /// VocalWarningSystem (GUID 36c8bbdca4a5c1b4396cb80c386fba8f) had zero scene/prefab hits.
    /// AtmosphericAudioRuntimeInstaller.EnsurePlayerSystems previously only warned and returned
    /// when the components were missing, so BindToPlayer never ran and thruster/vocal audio
    /// stayed silent in a shipped build.
    ///
    /// Fix: installer now AddComponent both owners onto the live AudioListener and binds the
    /// procedural renderer to the player. Soft FAIL under -quit.
    /// </summary>
    public static class PlayerCriticalAudioRuntimeConstructionValidator
    {
        private const string LogPrefix = "[PlayerCriticalAudioRuntimeConstructionValidator]";

        private const string InstallerRelativePath =
            "Assets/_Project/Scripts/Audio/AtmosphericAudioRuntimeInstaller.cs";

        private const string PinAddProcedural =
            "AddComponent<PlayerCriticalProceduralAudioRenderer>";
        private const string PinAddVocal =
            "AddComponent<VocalWarningSystem>";
        private const string PinBindToPlayer = "BindToPlayer";
        private const string PinEnsurePlayerSystems = "EnsurePlayerSystems";

        // COLD ALLOC: StringBuilder[4096] - editor audit report builder - owner: PlayerCriticalAudioRuntimeConstructionValidator
        private static readonly StringBuilder Report = new StringBuilder(4096);

        /// <summary>
        /// Public for -executeMethod / CI batchmode. Soft FAIL stays exit 0 under -quit.
        /// </summary>
        [MenuItem("Hecton8/Validation/Player Critical Audio Runtime Construction", priority = 198)]
        public static void Run()
        {
            bool batch = Application.isBatchMode;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating || Application.isPlaying)
            {
                string busy = LogPrefix + " RESULT: FAIL - Editor busy (compiling/updating/playing).";
                Debug.LogError(busy);
                if (!batch)
                    EditorUtility.DisplayDialog("Player Critical Audio Runtime Construction", busy, "OK");
                return;
            }

            Report.Clear();
            Report.AppendLine("=======================================================");
            Report.AppendLine("HECTON-8 - Player Critical Audio Runtime Construction Audit");
            Report.AppendLine("=======================================================");
            Report.AppendLine();
            Report.AppendLine("Note: both owners are runtime-only on the live AudioListener");
            Report.AppendLine("(scene/prefab GUID absence is EXPECTED; do not pin presence).");
            Report.AppendLine();

            string dataPath = Application.dataPath;
            string projectRoot = Directory.GetParent(dataPath) != null
                ? Directory.GetParent(dataPath).FullName
                : dataPath;

            string installerPath = Path.Combine(projectRoot, InstallerRelativePath.Replace('/', Path.DirectorySeparatorChar));
            bool installerExists = File.Exists(installerPath);
            string installerText = installerExists ? File.ReadAllText(installerPath) : string.Empty;

            bool hasAddProcedural = installerExists && installerText.Contains(PinAddProcedural);
            bool hasAddVocal = installerExists && installerText.Contains(PinAddVocal);
            bool hasBind = installerExists && installerText.Contains(PinBindToPlayer);
            bool hasEnsurePlayer = installerExists && installerText.Contains(PinEnsurePlayerSystems);

            // Regression guard: the old path only warned and returned. Pin that the warn-and-return
            // shape for these two components is gone.
            bool stillWarnOnlyProcedural = installerExists &&
                installerText.Contains("Missing authored PlayerCriticalProceduralAudioRenderer") &&
                installerText.Contains("Runtime component creation is disabled");
            bool stillWarnOnlyVocal = installerExists &&
                installerText.Contains("Missing authored VocalWarningSystem") &&
                installerText.Contains("Runtime component creation is disabled") &&
                !hasAddVocal;

            Report.AppendLine("--- Source file presence ---");
            AppendPresence(Report, InstallerRelativePath, installerExists);
            Report.AppendLine();

            Report.AppendLine("--- Construction pins ---");
            AppendGate(Report, "installer.EnsurePlayerSystems", hasEnsurePlayer);
            AppendGate(Report, "installer.AddComponent<PlayerCriticalProceduralAudioRenderer>", hasAddProcedural);
            AppendGate(Report, "installer.AddComponent<VocalWarningSystem>", hasAddVocal);
            AppendGate(Report, "installer.BindToPlayer", hasBind);
            AppendGate(Report, "installer.no-warn-only-procedural", !stillWarnOnlyProcedural || hasAddProcedural);
            AppendGate(Report, "installer.no-warn-only-vocal", hasAddVocal);
            Report.AppendLine();

            bool passed =
                installerExists &&
                hasEnsurePlayer &&
                hasAddProcedural &&
                hasAddVocal &&
                hasBind;

            if (!passed)
            {
                Report.AppendLine("FAIL reason: one or more runtime construction source pins missing.");
                if (!installerExists || !hasAddProcedural || !hasAddVocal)
                    Report.AppendLine("  - AtmosphericAudioRuntimeInstaller must AddComponent both critical audio owners.");
                if (!installerExists || !hasBind)
                    Report.AppendLine("  - AtmosphericAudioRuntimeInstaller must BindToPlayer after construct.");
            }
            else
            {
                Report.AppendLine("PASS: runtime construction pins present for player critical audio.");
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
                    "Player Critical Audio Runtime Construction",
                    passed
                        ? "PASS\nAll runtime construction source pins present."
                        : "FAIL\nOne or more source pins missing.\nSee Console.",
                    "OK");
            }
            // batchmode: soft FAIL under -quit (no EditorApplication.Exit on audit fail).
        }

        private static void AppendPresence(StringBuilder sb, string path, bool present)
        {
            sb.Append(present ? "  [OK] " : "  [MISSING] ");
            sb.AppendLine(path);
        }

        private static void AppendGate(StringBuilder sb, string label, bool present)
        {
            sb.Append(present ? "  [OK] " : "  [MISSING] ");
            sb.AppendLine(label);
        }
    }
}
#endif

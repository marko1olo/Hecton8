#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Soft-FAIL CI pin for AcousticZoneController runtime construction.
    ///
    /// AcousticZoneController is the sole owner of GlobalRegistry.AcousticZone /
    /// AcousticZoneReadModel / AcousticZoneMadnessCueSink / ToolAcousticCues and had no
    /// construction site of any kind. No AddComponent, no scene/prefab GUID hit for script GUID
    /// 46c4f463f7190a04b9285cb2b4cc7f63 under Assets/ (text + nibble-swapped binary sweep of
    /// 02_HECTON_WORLD / 00_BOOTSTRAP / 01_MAIN_MENU / 010_TEST). Four live consumers cached the
    /// permanent null: HectonSurfaceWeatherDirector.cs:836, DeepPsychosisController.cs:340,
    /// HectonMusicDirector.cs:1573, MantaScooter.cs:2608.
    ///
    /// The fix is AtmosphericAudioRuntimeInstaller.EnsureRuntimeSystems, wired from
    /// GameBootstrapper.PublishPlayerRuntimeReference after EnsurePlayerSystems.
    ///
    /// Live construction cannot be exercised in edit-mode batch: AcousticZoneController.Awake /
    /// OnEnable / TryRegisterService all gate on Application.isPlaying, so Instance and the four
    /// registry slots stay null outside Play Mode. This validator therefore pins the source
    /// construction path the same way HudNotificationRuntimeConstructionValidator does.
    /// Soft FAIL under -quit (no EditorApplication.Exit on audit fail).
    /// </summary>
    public static class AcousticZoneRuntimeConstructionValidator
    {
        private const string LogPrefix = "[AcousticZoneRuntimeConstructionValidator]";

        private const string InstallerRelativePath =
            "Assets/_Project/Scripts/Audio/AtmosphericAudioRuntimeInstaller.cs";
        private const string BootstrapRelativePath =
            "Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs";
        private const string ControllerRelativePath =
            "Assets/_Project/Scripts/AcousticZoneController.cs";

        private const string PinEnsureRuntimeSystems = "EnsureRuntimeSystems";
        private const string PinAddAcousticZone =
            "AddComponent<AcousticZoneController>";
        private const string PinInstallerCall =
            "AtmosphericAudioRuntimeInstaller.EnsureRuntimeSystems";
        private const string PinRegisterAcousticZone =
            "RegisterAcousticZoneRuntime";
        private const string PinTryRegisterService = "TryRegisterService";

        // COLD ALLOC: StringBuilder[4096] - editor audit report builder - owner: AcousticZoneRuntimeConstructionValidator
        private static readonly StringBuilder Report = new StringBuilder(4096);

        /// <summary>
        /// Public for -executeMethod / CI batchmode. Soft FAIL stays exit 0 under -quit.
        /// </summary>
        [MenuItem("Hecton8/Validation/Acoustic Zone Runtime Construction", priority = 194)]
        public static void Run()
        {
            bool batch = Application.isBatchMode;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating || Application.isPlaying)
            {
                string busy = LogPrefix + " RESULT: FAIL — Editor busy (compiling/updating/playing).";
                Debug.LogError(busy);
                if (!batch)
                    EditorUtility.DisplayDialog("Acoustic Zone Runtime Construction", busy, "OK");
                return;
            }

            Report.Clear();
            Report.AppendLine("═══════════════════════════════════════════════════════");
            Report.AppendLine("HECTON-8 — Acoustic Zone Runtime Construction Audit");
            Report.AppendLine("═══════════════════════════════════════════════════════");
            Report.AppendLine();
            Report.AppendLine("Note: AcousticZoneController is runtime-only");
            Report.AppendLine("(scene/prefab GUID absence is EXPECTED; do not pin presence).");
            Report.AppendLine("Live Instance/registry assert requires Play Mode (isPlaying gate).");
            Report.AppendLine();

            string dataPath = Application.dataPath;
            // Application.dataPath ends in /Assets — climb one level to project root.
            string projectRoot = Directory.GetParent(dataPath) != null
                ? Directory.GetParent(dataPath).FullName
                : dataPath;

            string installerPath = Path.Combine(projectRoot, InstallerRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string bootstrapPath = Path.Combine(projectRoot, BootstrapRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string controllerPath = Path.Combine(projectRoot, ControllerRelativePath.Replace('/', Path.DirectorySeparatorChar));

            bool installerExists = File.Exists(installerPath);
            bool bootstrapExists = File.Exists(bootstrapPath);
            bool controllerExists = File.Exists(controllerPath);

            string installerText = installerExists ? File.ReadAllText(installerPath) : string.Empty;
            string bootstrapText = bootstrapExists ? File.ReadAllText(bootstrapPath) : string.Empty;
            string controllerText = controllerExists ? File.ReadAllText(controllerPath) : string.Empty;

            bool installerHasEnsure = installerExists && installerText.Contains(PinEnsureRuntimeSystems);
            bool installerHasAdd = installerExists && installerText.Contains(PinAddAcousticZone);
            bool bootstrapHasCall = bootstrapExists && bootstrapText.Contains(PinInstallerCall);
            bool controllerHasRegister = controllerExists && controllerText.Contains(PinRegisterAcousticZone);
            bool controllerHasTryRegister = controllerExists && controllerText.Contains(PinTryRegisterService);

            Report.AppendLine("--- Source file presence ---");
            AppendPresence(Report, InstallerRelativePath, installerExists);
            AppendPresence(Report, BootstrapRelativePath, bootstrapExists);
            AppendPresence(Report, ControllerRelativePath, controllerExists);
            Report.AppendLine();

            Report.AppendLine("--- Construction pins ---");
            AppendGate(Report, "installer.EnsureRuntimeSystems", installerHasEnsure);
            AppendGate(Report, "installer.AddComponent<AcousticZoneController>", installerHasAdd);
            AppendGate(Report, "bootstrap.AtmosphericAudioRuntimeInstaller.EnsureRuntimeSystems", bootstrapHasCall);
            AppendGate(Report, "controller.RegisterAcousticZoneRuntime", controllerHasRegister);
            AppendGate(Report, "controller.TryRegisterService", controllerHasTryRegister);
            Report.AppendLine();

            Report.Append("installerExists=").Append(installerExists ? 1 : 0);
            Report.Append(" bootstrapExists=").Append(bootstrapExists ? 1 : 0);
            Report.Append(" controllerExists=").Append(controllerExists ? 1 : 0);
            Report.Append(" installerHasEnsure=").Append(installerHasEnsure ? 1 : 0);
            Report.Append(" installerHasAdd=").Append(installerHasAdd ? 1 : 0);
            Report.Append(" bootstrapHasCall=").Append(bootstrapHasCall ? 1 : 0);
            Report.Append(" controllerHasRegister=").Append(controllerHasRegister ? 1 : 0);
            Report.Append(" controllerHasTryRegister=").Append(controllerHasTryRegister ? 1 : 0);
            Report.AppendLine();

            bool passed =
                installerExists &&
                bootstrapExists &&
                controllerExists &&
                installerHasEnsure &&
                installerHasAdd &&
                bootstrapHasCall &&
                controllerHasRegister &&
                controllerHasTryRegister;

            if (!passed)
            {
                Report.AppendLine("FAIL reason: one or more runtime construction source pins missing.");
                if (!installerExists || !installerHasEnsure || !installerHasAdd)
                    Report.AppendLine("  • AtmosphericAudioRuntimeInstaller must own EnsureRuntimeSystems + AddComponent<AcousticZoneController>.");
                if (!bootstrapExists || !bootstrapHasCall)
                    Report.AppendLine("  • GameBootstrapper must call AtmosphericAudioRuntimeInstaller.EnsureRuntimeSystems.");
                if (!controllerExists || !controllerHasRegister || !controllerHasTryRegister)
                    Report.AppendLine("  • AcousticZoneController must own TryRegisterService → RegisterAcousticZoneRuntime.");
            }
            else
            {
                Report.AppendLine("PASS: runtime construction pins present for AcousticZoneController.");
                Report.AppendLine("masterMixer remains unassigned on runtime-created owner (profile-null-safe;");
                Report.AppendLine("snapshot transitions degrade until scene-authored mixer is wired).");
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
                    "Acoustic Zone Runtime Construction",
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


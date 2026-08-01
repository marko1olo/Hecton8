#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Soft-FAIL CI pin for HectonDiscoveryManager runtime construction.
    ///
    /// HectonDiscoveryManager is the sole GlobalRegistry.Discovery owner (biome discovery +
    /// fauna bestiary progression). Script GUID 56aa89edaf4f263419dd966a1cc4c197 has ZERO
    /// scene/prefab hits. No EnsureRuntimeInstance existed; OnEnable only registers when
    /// already present. DynamicDifficultyDirector, GlobalProfileManager,
    /// PlayerExplorationTracker and PlayerAchievementRegistry hit permanent null.
    ///
    /// The fix is HectonDiscoveryManager.EnsureRuntimeInstance (resolve-or-create + AddComponent)
    /// called from GameBootstrapper.PublishPlayerRuntimeReference after MissionManager.
    /// Soft FAIL under -quit (no EditorApplication.Exit on audit fail).
    /// </summary>
    public static class HectonDiscoveryManagerRuntimeConstructionValidator
    {
        private const string LogPrefix = "[HectonDiscoveryManagerRuntimeConstructionValidator]";

        private const string BootstrapRelativePath =
            "Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs";
        private const string ManagerRelativePath =
            "Assets/_Project/Scripts/HectonDiscoveryManager.cs";

        private const string PinEnsureRuntimeInstance = "EnsureRuntimeInstance";
        private const string PinAddDiscoveryManager = "AddComponent<HectonDiscoveryManager>";
        private const string PinBootstrapCall = "HectonDiscoveryManager.EnsureRuntimeInstance";
        private const string PinRegisterDiscovery = "RegisterDiscoveryRuntime";
        private const string PinPlayerBuildPath = "Player-build construction path";

        // COLD ALLOC: StringBuilder[4096] - editor audit report builder - owner: HectonDiscoveryManagerRuntimeConstructionValidator
        private static readonly StringBuilder Report = new StringBuilder(4096);

        /// <summary>
        /// Public for -executeMethod / CI batchmode. Soft FAIL stays exit 0 under -quit.
        /// </summary>
        [MenuItem("Hecton8/Validation/Hecton Discovery Manager Runtime Construction", priority = 204)]
        public static void Run()
        {
            bool batch = Application.isBatchMode;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating || Application.isPlaying)
            {
                string busy = LogPrefix + " RESULT: FAIL - Editor busy (compiling/updating/playing).";
                Debug.LogError(busy);
                if (!batch)
                    EditorUtility.DisplayDialog("Hecton Discovery Manager Runtime Construction", busy, "OK");
                return;
            }

            Report.Clear();
            Report.AppendLine("=======================================================");
            Report.AppendLine("HECTON-8 - Hecton Discovery Manager Runtime Construction Audit");
            Report.AppendLine("=======================================================");
            Report.AppendLine();
            Report.AppendLine("Note: HectonDiscoveryManager is runtime-only");
            Report.AppendLine("(scene/prefab GUID absence is EXPECTED; do not pin presence).");
            Report.AppendLine();

            string dataPath = Application.dataPath;
            string projectRoot = Directory.GetParent(dataPath) != null
                ? Directory.GetParent(dataPath).FullName
                : dataPath;

            string bootstrapPath = Path.Combine(projectRoot, BootstrapRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string managerPath = Path.Combine(projectRoot, ManagerRelativePath.Replace('/', Path.DirectorySeparatorChar));

            bool bootstrapExists = File.Exists(bootstrapPath);
            bool managerExists = File.Exists(managerPath);

            string bootstrapText = bootstrapExists ? File.ReadAllText(bootstrapPath) : string.Empty;
            string managerText = managerExists ? File.ReadAllText(managerPath) : string.Empty;

            bool managerHasEnsure = managerExists && managerText.Contains(PinEnsureRuntimeInstance);
            bool managerHasAdd = managerExists && managerText.Contains(PinAddDiscoveryManager);
            bool managerHasRegister = managerExists && managerText.Contains(PinRegisterDiscovery);
            bool managerHasPlayerPath = managerExists && managerText.Contains(PinPlayerBuildPath);
            bool bootstrapHasCall = bootstrapExists && bootstrapText.Contains(PinBootstrapCall);

            Report.AppendLine("--- Source file presence ---");
            AppendPresence(Report, ManagerRelativePath, managerExists);
            AppendPresence(Report, BootstrapRelativePath, bootstrapExists);
            Report.AppendLine();

            Report.AppendLine("--- Construction pins ---");
            AppendGate(Report, "manager.EnsureRuntimeInstance", managerHasEnsure);
            AppendGate(Report, "manager.AddComponent<HectonDiscoveryManager>", managerHasAdd);
            AppendGate(Report, "manager.RegisterDiscoveryRuntime", managerHasRegister);
            AppendGate(Report, "manager.Player-build construction path", managerHasPlayerPath);
            AppendGate(Report, "bootstrap.HectonDiscoveryManager.EnsureRuntimeInstance", bootstrapHasCall);
            Report.AppendLine();

            Report.Append("managerExists=").Append(managerExists ? 1 : 0);
            Report.Append(" bootstrapExists=").Append(bootstrapExists ? 1 : 0);
            Report.Append(" managerHasEnsure=").Append(managerHasEnsure ? 1 : 0);
            Report.Append(" managerHasAdd=").Append(managerHasAdd ? 1 : 0);
            Report.Append(" managerHasRegister=").Append(managerHasRegister ? 1 : 0);
            Report.Append(" managerHasPlayerPath=").Append(managerHasPlayerPath ? 1 : 0);
            Report.Append(" bootstrapHasCall=").Append(bootstrapHasCall ? 1 : 0);
            Report.AppendLine();

            bool passed =
                managerExists &&
                bootstrapExists &&
                managerHasEnsure &&
                managerHasAdd &&
                managerHasRegister &&
                managerHasPlayerPath &&
                bootstrapHasCall;

            if (!passed)
            {
                Report.AppendLine("FAIL reason: one or more runtime construction source pins missing.");
                if (!managerExists || !managerHasEnsure || !managerHasAdd)
                    Report.AppendLine("  - HectonDiscoveryManager must own EnsureRuntimeInstance + AddComponent<HectonDiscoveryManager>.");
                if (!managerExists || !managerHasRegister)
                    Report.AppendLine("  - HectonDiscoveryManager must own RegisterDiscoveryRuntime (GlobalRegistry.Discovery publish).");
                if (!managerExists || !managerHasPlayerPath)
                    Report.AppendLine("  - HectonDiscoveryManager Ensure must document Player-build construction path.");
                if (!bootstrapExists || !bootstrapHasCall)
                    Report.AppendLine("  - GameBootstrapper must call HectonDiscoveryManager.EnsureRuntimeInstance.");
            }
            else
            {
                Report.AppendLine("PASS: runtime construction pins present for HectonDiscoveryManager.");
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
                    "Hecton Discovery Manager Runtime Construction",
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
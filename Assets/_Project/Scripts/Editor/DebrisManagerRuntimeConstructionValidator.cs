#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Soft-FAIL CI pin for DebrisManager runtime construction.
    ///
    /// DebrisManager is the sole IDebrisService owner and already shipped
    /// DebrisManager.EnsureRuntimeInstance (resolve-or-create + inactive-root handling +
    /// AddComponent) with ZERO callers. Bootstrap DiagnoseMissingCriticalSystems already named
    /// the empty slot ("DebrisManager.EnsureRuntimeInstance exists but is never called").
    /// No scene/prefab GUID hit for script GUID 0b66cb54cbdfba54aa2267ecb4982579.
    /// Live consumers that hit the permanent null: StructureIntegrity (SpawnBurst on collapse),
    /// StructureModule (SpawnBurst), Creature (SpawnBurst on death), HectonFluidEngine
    /// (GlobalRegistry.Debris). Collapse/death debris FX never spawn in a shipped build.
    ///
    /// The fix is calling DebrisManager.EnsureRuntimeInstance from
    /// GameBootstrapper.TryResolveBootstrapNode (DebrisManager case) and from
    /// PublishPlayerRuntimeReference (defense-in-depth after AutonomousExtractor).
    /// Soft FAIL under -quit (no EditorApplication.Exit on audit fail).
    /// </summary>
    public static class DebrisManagerRuntimeConstructionValidator
    {
        private const string LogPrefix = "[DebrisManagerRuntimeConstructionValidator]";

        private const string BootstrapRelativePath =
            "Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs";
        private const string ManagerRelativePath =
            "Assets/_Project/Scripts/Gameplay/DebrisManager.cs";

        private const string PinEnsureRuntimeInstance = "EnsureRuntimeInstance";
        private const string PinAddDebrisManager = "AddComponent<DebrisManager>";
        private const string PinBootstrapCall = "DebrisManager.EnsureRuntimeInstance";
        private const string PinRegisterDebris = "RegisterDebrisService";


        // COLD ALLOC: StringBuilder[4096] - editor audit report builder - owner: DebrisManagerRuntimeConstructionValidator
        private static readonly StringBuilder Report = new StringBuilder(4096);

        /// <summary>
        /// Public for -executeMethod / CI batchmode. Soft FAIL stays exit 0 under -quit.
        /// </summary>
        [MenuItem("Hecton8/Validation/Debris Manager Runtime Construction", priority = 195)]
        public static void Run()
        {
            bool batch = Application.isBatchMode;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating || Application.isPlaying)
            {
                string busy = LogPrefix + " RESULT: FAIL - Editor busy (compiling/updating/playing).";
                Debug.LogError(busy);
                if (!batch)
                    EditorUtility.DisplayDialog("Debris Manager Runtime Construction", busy, "OK");
                return;
            }

            Report.Clear();
            Report.AppendLine("=======================================================");
            Report.AppendLine("HECTON-8 - Debris Manager Runtime Construction Audit");
            Report.AppendLine("=======================================================");
            Report.AppendLine();
            Report.AppendLine("Note: DebrisManager is runtime-only");
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
            bool managerHasAdd = managerExists && managerText.Contains(PinAddDebrisManager);
            bool managerHasRegister = managerExists && managerText.Contains(PinRegisterDebris);
            bool bootstrapHasCall = bootstrapExists && bootstrapText.Contains(PinBootstrapCall);

            Report.AppendLine("--- Source file presence ---");
            AppendPresence(Report, ManagerRelativePath, managerExists);
            AppendPresence(Report, BootstrapRelativePath, bootstrapExists);
            Report.AppendLine();

            Report.AppendLine("--- Construction pins ---");
            AppendGate(Report, "manager.EnsureRuntimeInstance", managerHasEnsure);
            AppendGate(Report, "manager.AddComponent<DebrisManager>", managerHasAdd);
            AppendGate(Report, "manager.RegisterDebrisService", managerHasRegister);

            AppendGate(Report, "bootstrap.DebrisManager.EnsureRuntimeInstance", bootstrapHasCall);
            Report.AppendLine();

            Report.Append("managerExists=").Append(managerExists ? 1 : 0);
            Report.Append(" bootstrapExists=").Append(bootstrapExists ? 1 : 0);
            Report.Append(" managerHasEnsure=").Append(managerHasEnsure ? 1 : 0);
            Report.Append(" managerHasAdd=").Append(managerHasAdd ? 1 : 0);
            Report.Append(" managerHasRegister=").Append(managerHasRegister ? 1 : 0);
            Report.Append(" bootstrapHasCall=").Append(bootstrapHasCall ? 1 : 0);
            Report.AppendLine();

            bool passed =
                managerExists &&
                bootstrapExists &&
                managerHasEnsure &&
                managerHasAdd &&
                managerHasRegister &&
                bootstrapHasCall;

            if (!passed)
            {
                Report.AppendLine("FAIL reason: one or more runtime construction source pins missing.");
                if (!managerExists || !managerHasEnsure || !managerHasAdd)
                    Report.AppendLine("  - DebrisManager must own EnsureRuntimeInstance + AddComponent<DebrisManager>.");
                if (!managerExists || !managerHasRegister)
                    Report.AppendLine("  - DebrisManager must own RegisterDebris (GlobalRegistry.Debris publish).");
                if (!bootstrapExists || !bootstrapHasCall)
                    Report.AppendLine("  - GameBootstrapper must call DebrisManager.EnsureRuntimeInstance.");
            }
            else
            {
                Report.AppendLine("PASS: runtime construction pins present for DebrisManager.");
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
                    "Debris Manager Runtime Construction",
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

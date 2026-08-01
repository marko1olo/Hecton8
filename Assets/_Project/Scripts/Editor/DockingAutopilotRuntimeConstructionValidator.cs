#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Soft-FAIL CI pin for DockingAutopilotService runtime construction.
    ///
    /// DockingAutopilotService is the sole IDockingAutopilotService owner and had no construction
    /// site of any kind. No AddComponent, no scene/prefab GUID hit for 3d6fecc0d76140547a5275b902b63c4b.
    /// Live consumer VehicleDockingModule.cs:1845 caches GlobalRegistry.DockingAutopilot permanently
    /// null, so docking spline acquire/evaluate never arms.
    ///
    /// Fix shape matches DebrisManager: EnsureRuntimeInstance on the service + call from
    /// GameBootstrapper.PublishPlayerRuntimeReference. Soft FAIL under -quit (no EditorApplication.Exit
    /// on audit fail).
    /// </summary>
    public static class DockingAutopilotRuntimeConstructionValidator
    {
        private const string LogPrefix = "[DockingAutopilotRuntimeConstructionValidator]";

        private const string ServiceRelativePath =
            "Assets/_Project/Scripts/Physics/Vehicles/Automation/DockingAutopilotService.cs";

        private const string BootstrapRelativePath =
            "Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs";

        private const string PinEnsureRuntimeInstance = "EnsureRuntimeInstance";
        private const string PinAddComponent = "AddComponent<DockingAutopilotService>";
        private const string PinBootstrapCall = "DockingAutopilotService.EnsureRuntimeInstance";
        private const string PinRegisterDocking = "RegisterDockingAutopilotService";


        // COLD ALLOC: StringBuilder[4096] - editor audit report builder - owner: DockingAutopilotRuntimeConstructionValidator
        private static readonly StringBuilder Report = new StringBuilder(4096);

        /// <summary>
        /// Public for -executeMethod / CI batchmode. Soft FAIL stays exit 0 under -quit.
        /// </summary>
        [MenuItem("Hecton8/Validation/Docking Autopilot Runtime Construction", priority = 197)]
        public static void Run()
        {
            bool batch = Application.isBatchMode;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating || Application.isPlaying)
            {
                string busy = LogPrefix + " RESULT: FAIL - Editor busy (compiling/updating/playing).";
                Debug.LogError(busy);
                if (!batch)
                    EditorUtility.DisplayDialog("Docking Autopilot Runtime Construction", busy, "OK");
                return;
            }

            Report.Clear();
            Report.AppendLine("=======================================================");
            Report.AppendLine("HECTON-8 - Docking Autopilot Runtime Construction Audit");
            Report.AppendLine("=======================================================");
            Report.AppendLine();
            Report.AppendLine("Note: DockingAutopilotService is runtime-only");
            Report.AppendLine("(scene/prefab GUID absence is EXPECTED; do not pin presence).");
            Report.AppendLine();

            string dataPath = Application.dataPath;
            string projectRoot = Directory.GetParent(dataPath) != null
                ? Directory.GetParent(dataPath).FullName
                : dataPath;

            string servicePath = Path.Combine(projectRoot, ServiceRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string bootstrapPath = Path.Combine(projectRoot, BootstrapRelativePath.Replace('/', Path.DirectorySeparatorChar));

            bool serviceExists = File.Exists(servicePath);
            bool bootstrapExists = File.Exists(bootstrapPath);
            string serviceText = serviceExists ? File.ReadAllText(servicePath) : string.Empty;
            string bootstrapText = bootstrapExists ? File.ReadAllText(bootstrapPath) : string.Empty;

            bool hasEnsure = serviceExists && serviceText.Contains(PinEnsureRuntimeInstance);
            bool hasAdd = serviceExists && serviceText.Contains(PinAddComponent);
            bool hasBootstrapCall = bootstrapExists && bootstrapText.Contains(PinBootstrapCall);
            bool hasRegister = serviceExists && serviceText.Contains(PinRegisterDocking);

            Report.AppendLine("--- Source file presence ---");
            AppendPresence(Report, ServiceRelativePath, serviceExists);
            AppendPresence(Report, BootstrapRelativePath, bootstrapExists);
            Report.AppendLine();

            Report.AppendLine("--- Construction pins ---");
            AppendGate(Report, "service.EnsureRuntimeInstance", hasEnsure);
            AppendGate(Report, "service.AddComponent<DockingAutopilotService>", hasAdd);
            AppendGate(Report, "bootstrap.DockingAutopilotService.EnsureRuntimeInstance", hasBootstrapCall);
            AppendGate(Report, "service.RegisterDockingAutopilotService", hasRegister);

            Report.AppendLine();

            Report.Append("serviceExists=").Append(serviceExists ? 1 : 0);
            Report.Append(" bootstrapExists=").Append(bootstrapExists ? 1 : 0);
            Report.Append(" hasEnsure=").Append(hasEnsure ? 1 : 0);
            Report.Append(" hasAdd=").Append(hasAdd ? 1 : 0);
            Report.Append(" hasBootstrapCall=").Append(hasBootstrapCall ? 1 : 0);
            Report.Append(" hasRegister=").Append(hasRegister ? 1 : 0);
            Report.AppendLine();

            bool passed = serviceExists && bootstrapExists && hasEnsure && hasAdd && hasBootstrapCall && hasRegister;

            if (!passed)
            {
                Report.AppendLine("FAIL reason: one or more runtime construction source pins missing.");
                if (!serviceExists || !hasEnsure || !hasAdd)
                    Report.AppendLine("  - DockingAutopilotService must own EnsureRuntimeInstance + AddComponent.");
                if (!bootstrapExists || !hasBootstrapCall)
                    Report.AppendLine("  - GameBootstrapper must call DockingAutopilotService.EnsureRuntimeInstance.");
                if (!serviceExists || !hasRegister)
                    Report.AppendLine("  - DockingAutopilotService must own RegisterDockingAutopilot.");
            }
            else
            {
                Report.AppendLine("PASS: runtime construction pins present for DockingAutopilotService.");
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
                    "Docking Autopilot Runtime Construction",
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

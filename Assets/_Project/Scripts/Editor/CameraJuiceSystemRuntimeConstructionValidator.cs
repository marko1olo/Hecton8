#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Soft-FAIL CI pin for CameraJuiceSystem runtime construction.
    ///
    /// CameraJuiceSystem is the sole ICameraJuiceSystem / GlobalRegistry.CameraJuice owner.
    /// Script GUID 394c096b405b1e745b881283ae9a05c6 has ZERO scene/prefab hits. No
    /// EnsureRuntimeInstance existed; Awake/OnEnable only register when already present.
    /// Live consumers that hit the permanent null: SceneRuntimeService BeginInputReclaimFov,
    /// SystemDispatcher ResolveCameraJuiceSystem, HectonSystemsDebugUI.
    ///
    /// The fix is CameraJuiceSystem.EnsureRuntimeInstance (resolve-or-create + AddComponent)
    /// called from GameBootstrapper.PublishPlayerRuntimeReference after SubtitleManager
    /// (player available for TryResolveCamera). Soft FAIL under -quit (no EditorApplication.Exit
    /// on audit fail).
    /// </summary>
    public static class CameraJuiceSystemRuntimeConstructionValidator
    {
        private const string LogPrefix = "[CameraJuiceSystemRuntimeConstructionValidator]";

        private const string BootstrapRelativePath =
            "Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs";
        private const string ManagerRelativePath =
            "Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs";

        private const string PinEnsureRuntimeInstance = "EnsureRuntimeInstance";
        private const string PinAddCameraJuice = "AddComponent<CameraJuiceSystem>";
        private const string PinBootstrapCall = "CameraJuiceSystem.EnsureRuntimeInstance";
        private const string PinRegisterCameraJuice = "RegisterCameraJuiceRuntime";
        private const string PinPlayerBuildPath = "Player-build construction path";

        // COLD ALLOC: StringBuilder[4096] - editor audit report builder - owner: CameraJuiceSystemRuntimeConstructionValidator
        private static readonly StringBuilder Report = new StringBuilder(4096);

        /// <summary>
        /// Public for -executeMethod / CI batchmode. Soft FAIL stays exit 0 under -quit.
        /// </summary>
        [MenuItem("Hecton8/Validation/Camera Juice System Runtime Construction", priority = 202)]
        public static void Run()
        {
            bool batch = Application.isBatchMode;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating || Application.isPlaying)
            {
                string busy = LogPrefix + " RESULT: FAIL - Editor busy (compiling/updating/playing).";
                Debug.LogError(busy);
                if (!batch)
                    EditorUtility.DisplayDialog("Camera Juice System Runtime Construction", busy, "OK");
                return;
            }

            Report.Clear();
            Report.AppendLine("=======================================================");
            Report.AppendLine("HECTON-8 - Camera Juice System Runtime Construction Audit");
            Report.AppendLine("=======================================================");
            Report.AppendLine();
            Report.AppendLine("Note: CameraJuiceSystem is runtime-only");
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
            bool managerHasAdd = managerExists && managerText.Contains(PinAddCameraJuice);
            bool managerHasRegister = managerExists && managerText.Contains(PinRegisterCameraJuice);
            bool managerHasPlayerPath = managerExists && managerText.Contains(PinPlayerBuildPath);
            bool bootstrapHasCall = bootstrapExists && bootstrapText.Contains(PinBootstrapCall);

            Report.AppendLine("--- Source file presence ---");
            AppendPresence(Report, ManagerRelativePath, managerExists);
            AppendPresence(Report, BootstrapRelativePath, bootstrapExists);
            Report.AppendLine();

            Report.AppendLine("--- Construction pins ---");
            AppendGate(Report, "manager.EnsureRuntimeInstance", managerHasEnsure);
            AppendGate(Report, "manager.AddComponent<CameraJuiceSystem>", managerHasAdd);
            AppendGate(Report, "manager.RegisterCameraJuiceRuntime", managerHasRegister);
            AppendGate(Report, "manager.Player-build construction path", managerHasPlayerPath);
            AppendGate(Report, "bootstrap.CameraJuiceSystem.EnsureRuntimeInstance", bootstrapHasCall);
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
                    Report.AppendLine("  - CameraJuiceSystem must own EnsureRuntimeInstance + AddComponent<CameraJuiceSystem>.");
                if (!managerExists || !managerHasRegister)
                    Report.AppendLine("  - CameraJuiceSystem must own RegisterCameraJuiceRuntime (GlobalRegistry.CameraJuice publish).");
                if (!managerExists || !managerHasPlayerPath)
                    Report.AppendLine("  - CameraJuiceSystem Ensure must document Player-build construction path.");
                if (!bootstrapExists || !bootstrapHasCall)
                    Report.AppendLine("  - GameBootstrapper must call CameraJuiceSystem.EnsureRuntimeInstance.");
            }
            else
            {
                Report.AppendLine("PASS: runtime construction pins present for CameraJuiceSystem.");
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
                    "Camera Juice System Runtime Construction",
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

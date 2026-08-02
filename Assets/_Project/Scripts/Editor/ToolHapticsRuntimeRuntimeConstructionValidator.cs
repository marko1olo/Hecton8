#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Soft-FAIL CI pin for ToolHapticsRuntime runtime construction.
    ///
    /// Zero live scene/prefab GUID hits. EnsureRuntimeInstance previously only returned
    /// s_runtime without AddComponent, so OnEnable registration never ran. Fix is
    /// player-build resolve-or-create + GameBootstrapper wire. Soft FAIL under -quit.
    /// </summary>
    public static class ToolHapticsRuntimeRuntimeConstructionValidator
    {
        private const string LogPrefix = "[ToolHapticsRuntimeRuntimeConstructionValidator]";

        private const string BootstrapRelativePath =
            "Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs";
        private const string ManagerRelativePath =
            "Assets/_Project/Scripts/Tools/ToolHapticsRuntime.cs";

        private const string PinEnsureRuntimeInstance = "EnsureRuntimeInstance";
        private const string PinAddManager = "AddComponent<ToolHapticsRuntime>";
        private const string PinBootstrapCall = "ToolHapticsRuntime.EnsureRuntimeInstance";
        private const string PinPlayerBuildPath = "Player-build construction path";

        // COLD ALLOC: StringBuilder[4096] - editor audit report builder - owner: ToolHapticsRuntimeRuntimeConstructionValidator
        private static readonly StringBuilder Report = new StringBuilder(4096);

        [MenuItem("Hecton8/Validation/Tool Haptics Runtime Construction", priority = 218)]
        public static void Run()
        {
            Report.Clear();
            bool pass = true;

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            string managerPath = Path.Combine(projectRoot, ManagerRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string bootstrapPath = Path.Combine(projectRoot, BootstrapRelativePath.Replace('/', Path.DirectorySeparatorChar));

            string managerSrc = File.Exists(managerPath) ? File.ReadAllText(managerPath) : string.Empty;
            string bootstrapSrc = File.Exists(bootstrapPath) ? File.ReadAllText(bootstrapPath) : string.Empty;

            pass &= Pin(managerSrc, PinEnsureRuntimeInstance, "ToolHapticsRuntime.EnsureRuntimeInstance factory");
            pass &= Pin(managerSrc, PinAddManager, "ToolHapticsRuntime player-build AddComponent");
            pass &= Pin(managerSrc, PinPlayerBuildPath, "Player-build construction path comment");
            pass &= Pin(bootstrapSrc, PinBootstrapCall, "GameBootstrapper ToolHapticsRuntime.EnsureRuntimeInstance call");

            string result = pass ? "PASS" : "FAIL";
            Report.AppendLine($"{LogPrefix} RESULT: {result}");

            if (pass)
                Debug.Log(Report.ToString());
            else
                Debug.LogError(Report.ToString());
        }

        private static bool Pin(string source, string token, string label)
        {
            bool ok = !string.IsNullOrEmpty(source) && source.Contains(token);
            Report.AppendLine(ok
                ? $"{LogPrefix} OK  {label} ({token})"
                : $"{LogPrefix} MISSING {label} ({token})");
            return ok;
        }
    }
}
#endif

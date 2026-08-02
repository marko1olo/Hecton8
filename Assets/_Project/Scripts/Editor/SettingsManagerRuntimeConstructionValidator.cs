#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Soft-FAIL CI pin for SettingsManager runtime construction.
    ///
    /// Construction previously lived only in GameBootstrapper.EnsureSettingsRuntimeRegistered.
    /// SettingsManager.EnsureRuntimeInstance was resolve-only (returned s_runtimeInstance) so hot
    /// consumers permanently saw null when bootstrap had not yet re-registered after scene unload.
    /// Fix is SettingsManager.EnsureRuntimeInstance (static slot + GlobalRegistry.Settings + scene
    /// scan + player-build AddComponent) with bootstrap simplified to call the factory.
    /// Soft FAIL under -quit (no EditorApplication.Exit on audit fail).
    /// </summary>
    public static class SettingsManagerRuntimeConstructionValidator
    {
        private const string LogPrefix = "[SettingsManagerRuntimeConstructionValidator]";

        private const string BootstrapRelativePath =
            "Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs";
        private const string ManagerRelativePath =
            "Assets/_Project/Scripts/UI/SettingsManager.cs";

        private const string PinEnsureRuntimeInstance = "EnsureRuntimeInstance";
        private const string PinAddManager = "AddComponent<SettingsManager>";
        private const string PinBootstrapCall = "SettingsManager.EnsureRuntimeInstance";
        private const string PinPlayerBuildPath = "Player-build construction path";
        private const string PinNoDuplicateBootstrap =
            "Bootstrap no longer duplicates the construction path";

        // COLD ALLOC: StringBuilder[4096] - editor audit report builder - owner: SettingsManagerRuntimeConstructionValidator
        private static readonly StringBuilder Report = new StringBuilder(4096);

        /// <summary>
        /// Public for -executeMethod / CI batchmode. Soft FAIL stays exit 0 under -quit.
        /// </summary>
        [MenuItem("Hecton8/Validation/Settings Manager Runtime Construction", priority = 224)]
        public static void Run()
        {
            Report.Clear();
            bool pass = true;

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            string managerPath = Path.Combine(projectRoot, ManagerRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string bootstrapPath = Path.Combine(projectRoot, BootstrapRelativePath.Replace('/', Path.DirectorySeparatorChar));

            string managerSrc = File.Exists(managerPath) ? File.ReadAllText(managerPath) : string.Empty;
            string bootstrapSrc = File.Exists(bootstrapPath) ? File.ReadAllText(bootstrapPath) : string.Empty;

            pass &= Pin(managerSrc, PinEnsureRuntimeInstance, "SettingsManager.EnsureRuntimeInstance factory");
            pass &= Pin(managerSrc, PinAddManager, "SettingsManager player-build AddComponent");
            pass &= Pin(managerSrc, PinPlayerBuildPath, "Player-build construction path comment");
            pass &= Pin(bootstrapSrc, PinBootstrapCall, "GameBootstrapper SettingsManager.EnsureRuntimeInstance call");
            pass &= Pin(bootstrapSrc, PinNoDuplicateBootstrap, "Bootstrap no longer duplicates construction path");

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

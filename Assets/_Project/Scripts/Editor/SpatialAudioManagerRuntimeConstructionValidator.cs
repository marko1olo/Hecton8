#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Soft-FAIL CI pin for SpatialAudioManager runtime construction.
    ///
    /// Prefab PFB_SpatialAudioManagerRoot exists but is not parented under GameBootstrapper
    /// in player builds. Bootstrap previously only GetComponentInChildren'd an authored child,
    /// then fell through to NoOpAudio. Fix is SpatialAudioManager.EnsureRuntimeInstance
    /// (resolve-or-create + InitializeService) called from EnsureAudioServiceRegistered.
    /// Soft FAIL under -quit (no EditorApplication.Exit on audit fail).
    /// </summary>
    public static class SpatialAudioManagerRuntimeConstructionValidator
    {
        private const string LogPrefix = "[SpatialAudioManagerRuntimeConstructionValidator]";

        private const string BootstrapRelativePath =
            "Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs";
        private const string ManagerRelativePath =
            "Assets/_Project/Scripts/SpatialAudioManager.cs";

        private const string PinEnsureRuntimeInstance = "EnsureRuntimeInstance";
        private const string PinAddManager = "AddComponent<SpatialAudioManager>";
        private const string PinBootstrapCall = "SpatialAudioManager.EnsureRuntimeInstance";
        private const string PinPlayerBuildPath = "Player-build construction path";
        private const string PinInitializeService = "InitializeService";

        // COLD ALLOC: StringBuilder[4096] - editor audit report builder - owner: SpatialAudioManagerRuntimeConstructionValidator
        private static readonly StringBuilder Report = new StringBuilder(4096);

        /// <summary>
        /// Public for -executeMethod / CI batchmode. Soft FAIL stays exit 0 under -quit.
        /// </summary>
        [MenuItem("Hecton8/Validation/Spatial Audio Manager Runtime Construction", priority = 220)]
        public static void Run()
        {
            Report.Clear();
            bool pass = true;

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            string managerPath = Path.Combine(projectRoot, ManagerRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string bootstrapPath = Path.Combine(projectRoot, BootstrapRelativePath.Replace('/', Path.DirectorySeparatorChar));

            string managerSrc = File.Exists(managerPath) ? File.ReadAllText(managerPath) : string.Empty;
            string bootstrapSrc = File.Exists(bootstrapPath) ? File.ReadAllText(bootstrapPath) : string.Empty;

            pass &= Pin(managerSrc, PinEnsureRuntimeInstance, "SpatialAudioManager.EnsureRuntimeInstance factory");
            pass &= Pin(managerSrc, PinAddManager, "SpatialAudioManager player-build AddComponent");
            pass &= Pin(managerSrc, PinPlayerBuildPath, "Player-build construction path comment");
            pass &= Pin(managerSrc, PinInitializeService, "SpatialAudioManager.InitializeService");
            pass &= Pin(bootstrapSrc, PinBootstrapCall, "GameBootstrapper SpatialAudioManager.EnsureRuntimeInstance call");

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

#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Soft-FAIL CI pin for AmbientBiotaDirector runtime construction.
    ///
    /// AmbientBiotaDirector is the sole IAmbientBiotaService owner and lives in
    /// Hecton8.AI.Ambient (autoReferenced: false). Live consumers (EcosystemDirector,
    /// Creature, WorldChunkResidencyManager) cache GlobalRegistry.AmbientBiota permanently
    /// null without a create path. Fix is AmbientBiotaDirector.EnsureRuntimeInstance
    /// (scene scan + registry + player-build AddComponent) called from GameBootstrapper
    /// and RuntimeInitializeOnLoad AfterSceneLoad.
    /// Soft FAIL under -quit (no EditorApplication.Exit on audit fail).
    /// </summary>
    public static class AmbientBiotaDirectorRuntimeConstructionValidator
    {
        private const string LogPrefix = "[AmbientBiotaDirectorRuntimeConstructionValidator]";

        private const string BootstrapRelativePath =
            "Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs";
        private const string ManagerRelativePath =
            "Assets/_Project/Scripts/AI/Ambient/AmbientBiotaDirector.cs";

        private const string PinEnsureRuntimeInstance = "EnsureRuntimeInstance";
        private const string PinAddManager = "AddComponent<AmbientBiotaDirector>";
        private const string PinBootstrapCall = "AmbientBiotaDirector.EnsureRuntimeInstance";
        private const string PinPlayerBuildPath = "Player-build construction path";

        // COLD ALLOC: StringBuilder[4096] - editor audit report builder - owner: AmbientBiotaDirectorRuntimeConstructionValidator
        private static readonly StringBuilder Report = new StringBuilder(4096);

        /// <summary>
        /// Public for -executeMethod / CI batchmode. Soft FAIL stays exit 0 under -quit.
        /// </summary>
        [MenuItem("Hecton8/Validation/Ambient Biota Director Runtime Construction", priority = 226)]
        public static void Run()
        {
            Report.Clear();
            bool pass = true;

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            string managerPath = Path.Combine(projectRoot, ManagerRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string bootstrapPath = Path.Combine(projectRoot, BootstrapRelativePath.Replace('/', Path.DirectorySeparatorChar));

            string managerSrc = File.Exists(managerPath) ? File.ReadAllText(managerPath) : string.Empty;
            string bootstrapSrc = File.Exists(bootstrapPath) ? File.ReadAllText(bootstrapPath) : string.Empty;

            pass &= Pin(managerSrc, PinEnsureRuntimeInstance, "AmbientBiotaDirector.EnsureRuntimeInstance factory");
            pass &= Pin(managerSrc, PinAddManager, "AmbientBiotaDirector player-build AddComponent");
            pass &= Pin(managerSrc, PinPlayerBuildPath, "Player-build construction path comment");
            pass &= Pin(bootstrapSrc, PinBootstrapCall, "GameBootstrapper AmbientBiotaDirector.EnsureRuntimeInstance call");

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

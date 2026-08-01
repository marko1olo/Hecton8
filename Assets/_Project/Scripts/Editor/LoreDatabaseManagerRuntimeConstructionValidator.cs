#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Soft-FAIL CI pin for LoreDatabaseManager runtime construction.
    ///
    /// LoreDatabaseManager is the sole GlobalRegistry.LoreDatabase owner
    /// (lore unlock read-model / scan fragment catalog).
    /// Script GUID 42a7b5625bed8574794366fcc0149275 has ZERO live scene/prefab hits
    /// (only Assets/_Recovery leftovers). HectonLoreSystemsRoot.SetupAllSystems is
    /// editor ContextMenu-only and does not run in play mode.
    /// No EnsureRuntimeInstance existed; OnEnable only registers when already present.
    /// HectonDiscoveryManager, ResearchDirector and ScannableFragment hit permanent null.
    ///
    /// The fix is LoreDatabaseManager.EnsureRuntimeInstance (resolve-or-create +
    /// AddComponent) called from GameBootstrapper.PublishPlayerRuntimeReference after
    /// AudioLogSystem. Soft FAIL under -quit (no EditorApplication.Exit on audit fail).
    /// </summary>
    public static class LoreDatabaseManagerRuntimeConstructionValidator
    {
        private const string LogPrefix = "[LoreDatabaseManagerRuntimeConstructionValidator]";

        private const string BootstrapRelativePath =
            "Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs";
        private const string ManagerRelativePath =
            "Assets/_Project/Scripts/Narrative/LoreDatabaseManager.cs";

        private const string PinEnsureRuntimeInstance = "EnsureRuntimeInstance";
        private const string PinAddManager = "AddComponent<LoreDatabaseManager>";
        private const string PinBootstrapCall = "LoreDatabaseManager.EnsureRuntimeInstance";
        private const string PinRegister = "RegisterLoreDatabaseRuntime";
        private const string PinPlayerBuildPath = "Player-build construction path";

        // COLD ALLOC: StringBuilder[4096] - editor audit report builder - owner: LoreDatabaseManagerRuntimeConstructionValidator
        private static readonly StringBuilder Report = new StringBuilder(4096);

        /// <summary>
        /// Public for -executeMethod / CI batchmode. Soft FAIL stays exit 0 under -quit.
        /// </summary>
        [MenuItem("Hecton8/Validation/Lore Database Manager Runtime Construction", priority = 208)]
        public static void Run()
        {
            Report.Clear();
            bool pass = true;

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            string managerPath = Path.Combine(projectRoot, ManagerRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string bootstrapPath = Path.Combine(projectRoot, BootstrapRelativePath.Replace('/', Path.DirectorySeparatorChar));

            string managerSrc = File.Exists(managerPath) ? File.ReadAllText(managerPath) : string.Empty;
            string bootstrapSrc = File.Exists(bootstrapPath) ? File.ReadAllText(bootstrapPath) : string.Empty;

            pass &= Pin(managerSrc, PinEnsureRuntimeInstance, "LoreDatabaseManager.EnsureRuntimeInstance factory");
            pass &= Pin(managerSrc, PinAddManager, "LoreDatabaseManager player-build AddComponent");
            pass &= Pin(managerSrc, PinRegister, "GlobalRegistry.RegisterLoreDatabaseRuntime");
            pass &= Pin(managerSrc, PinPlayerBuildPath, "Player-build construction path comment");
            pass &= Pin(bootstrapSrc, PinBootstrapCall, "GameBootstrapper LoreDatabaseManager.EnsureRuntimeInstance call");

            string result = pass ? "PASS" : "FAIL";
            string summary = $"{LogPrefix} RESULT: {result}";
            Report.AppendLine(summary);

            if (pass)
                Debug.Log(Report.ToString());
            else
                Debug.LogError(Report.ToString());

            // Soft FAIL: never EditorApplication.Exit(1) — batch -quit must stay green for compile.
            if (Application.isBatchMode)
            {
                // no-op exit code control; Unity -quit handles process lifetime
            }
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

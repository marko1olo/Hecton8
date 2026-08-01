#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Soft-FAIL CI pin for SargassumCutManager runtime construction.
    ///
    /// SargassumCutManager is the sole GlobalRegistry.SargassumCut owner
    /// (ISargassumCutWriteService — cut mask / damage volume).
    /// Script GUID ff5d403710d1d0e4bb43e3210c59df5c has ZERO live scene/prefab hits.
    /// No EnsureRuntimeInstance existed; OnEnable only registers when already present.
    /// Cut-mask consumers hit permanent null.
    ///
    /// The fix is SargassumCutManager.EnsureRuntimeInstance (resolve-or-create +
    /// AddComponent) called from GameBootstrapper.PublishPlayerRuntimeReference after
    /// SargassumGlobalDragManager. Soft FAIL under -quit (no EditorApplication.Exit on audit fail).
    /// </summary>
    public static class SargassumCutManagerRuntimeConstructionValidator
    {
        private const string LogPrefix = "[SargassumCutManagerRuntimeConstructionValidator]";

        private const string BootstrapRelativePath =
            "Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs";
        private const string ManagerRelativePath =
            "Assets/_Project/Scripts/World/SargassumCutManager.cs";

        private const string PinEnsureRuntimeInstance = "EnsureRuntimeInstance";
        private const string PinAddManager = "AddComponent<SargassumCutManager>";
        private const string PinBootstrapCall = "SargassumCutManager.EnsureRuntimeInstance";
        private const string PinRegister = "RegisterSargassumCutRuntime";
        private const string PinPlayerBuildPath = "Player-build construction path";

        // COLD ALLOC: StringBuilder[4096] - editor audit report builder - owner: SargassumCutManagerRuntimeConstructionValidator
        private static readonly StringBuilder Report = new StringBuilder(4096);

        /// <summary>
        /// Public for -executeMethod / CI batchmode. Soft FAIL stays exit 0 under -quit.
        /// </summary>
        [MenuItem("Hecton8/Validation/Sargassum Cut Manager Runtime Construction", priority = 214)]
        public static void Run()
        {
            Report.Clear();
            bool pass = true;

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            string managerPath = Path.Combine(projectRoot, ManagerRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string bootstrapPath = Path.Combine(projectRoot, BootstrapRelativePath.Replace('/', Path.DirectorySeparatorChar));

            string managerSrc = File.Exists(managerPath) ? File.ReadAllText(managerPath) : string.Empty;
            string bootstrapSrc = File.Exists(bootstrapPath) ? File.ReadAllText(bootstrapPath) : string.Empty;

            pass &= Pin(managerSrc, PinEnsureRuntimeInstance, "SargassumCutManager.EnsureRuntimeInstance factory");
            pass &= Pin(managerSrc, PinAddManager, "SargassumCutManager player-build AddComponent");
            pass &= Pin(managerSrc, PinRegister, "GlobalRegistry.RegisterSargassumCutRuntime");
            pass &= Pin(managerSrc, PinPlayerBuildPath, "Player-build construction path comment");
            pass &= Pin(bootstrapSrc, PinBootstrapCall, "GameBootstrapper SargassumCutManager.EnsureRuntimeInstance call");

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

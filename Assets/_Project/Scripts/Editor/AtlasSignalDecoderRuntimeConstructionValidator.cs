#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Soft-FAIL CI pin for AtlasSignalDecoder runtime construction.
    ///
    /// AtlasSignalDecoder is the sole GlobalRegistry.AtlasSignalDecoder owner
    /// (Atlas-6 phase decode / quest handoff).
    /// Script GUID bca3aaf40fff8ea459345f06a7f5592b has ZERO live scene/prefab hits.
    /// HectonLoreSystemsRoot.SetupAllSystems is editor ContextMenu-only.
    /// No EnsureRuntimeInstance existed; OnEnable only registers when already present.
    /// Phase/quest consumers hit permanent null.
    ///
    /// The fix is AtlasSignalDecoder.EnsureRuntimeInstance (resolve-or-create +
    /// AddComponent) called from GameBootstrapper.PublishPlayerRuntimeReference after
    /// AtlasSignalSystem. Soft FAIL under -quit (no EditorApplication.Exit on audit fail).
    /// </summary>
    public static class AtlasSignalDecoderRuntimeConstructionValidator
    {
        private const string LogPrefix = "[AtlasSignalDecoderRuntimeConstructionValidator]";

        private const string BootstrapRelativePath =
            "Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs";
        private const string ManagerRelativePath =
            "Assets/_Project/Scripts/AtlasSignal/AtlasSignalDecoder.cs";

        private const string PinEnsureRuntimeInstance = "EnsureRuntimeInstance";
        private const string PinAddManager = "AddComponent<AtlasSignalDecoder>";
        private const string PinBootstrapCall = "AtlasSignalDecoder.EnsureRuntimeInstance";
        private const string PinRegister = "RegisterAtlasSignalDecoderRuntime";
        private const string PinPlayerBuildPath = "Player-build construction path";

        // COLD ALLOC: StringBuilder[4096] - editor audit report builder - owner: AtlasSignalDecoderRuntimeConstructionValidator
        private static readonly StringBuilder Report = new StringBuilder(4096);

        /// <summary>
        /// Public for -executeMethod / CI batchmode. Soft FAIL stays exit 0 under -quit.
        /// </summary>
        [MenuItem("Hecton8/Validation/Atlas Signal Decoder Runtime Construction", priority = 211)]
        public static void Run()
        {
            Report.Clear();
            bool pass = true;

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            string managerPath = Path.Combine(projectRoot, ManagerRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string bootstrapPath = Path.Combine(projectRoot, BootstrapRelativePath.Replace('/', Path.DirectorySeparatorChar));

            string managerSrc = File.Exists(managerPath) ? File.ReadAllText(managerPath) : string.Empty;
            string bootstrapSrc = File.Exists(bootstrapPath) ? File.ReadAllText(bootstrapPath) : string.Empty;

            pass &= Pin(managerSrc, PinEnsureRuntimeInstance, "AtlasSignalDecoder.EnsureRuntimeInstance factory");
            pass &= Pin(managerSrc, PinAddManager, "AtlasSignalDecoder player-build AddComponent");
            pass &= Pin(managerSrc, PinRegister, "GlobalRegistry.RegisterAtlasSignalDecoderRuntime");
            pass &= Pin(managerSrc, PinPlayerBuildPath, "Player-build construction path comment");
            pass &= Pin(bootstrapSrc, PinBootstrapCall, "GameBootstrapper AtlasSignalDecoder.EnsureRuntimeInstance call");

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

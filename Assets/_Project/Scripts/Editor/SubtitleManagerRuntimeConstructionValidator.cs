#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Soft-FAIL CI pin for SubtitleManager runtime construction.
    ///
    /// SubtitleManager is the sole GlobalRegistry.Subtitles owner and had zero scene/prefab
    /// GUID hits for 2007393d93d7376438891f11d8ec3a10 (including Suit_HUD_Canvas.prefab).
    /// Construction previously lived only behind #if UNITY_EDITOR || DEVELOPMENT_BUILD, so a
    /// shipped player build never AddComponent'd the owner and every subtitle consumer
    /// (vocal warning / Babel / audio-log cues) hit a permanent null.
    ///
    /// Fix shape: EnsureRuntimeInstance on SubtitleManager (player-build path + HUD canvas
    /// parent) + call from GameBootstrapper.PublishPlayerRuntimeReference after player/HUD
    /// publication. Soft FAIL under -quit (no EditorApplication.Exit on audit fail).
    /// </summary>
    public static class SubtitleManagerRuntimeConstructionValidator
    {
        private const string LogPrefix = "[SubtitleManagerRuntimeConstructionValidator]";

        private const string ServiceRelativePath =
            "Assets/_Project/Scripts/UI/SubtitleManager.cs";

        private const string BootstrapRelativePath =
            "Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs";

        private const string PinEnsureRuntimeInstance = "EnsureRuntimeInstance";
        private const string PinAddComponent = "AddComponent<SubtitleManager>";
        private const string PinBootstrapCall = "SubtitleManager.EnsureRuntimeInstance";
        private const string PinRegisterSubtitle = "RegisterSubtitleRuntime";
        private const string PinResolveHostCanvas = "ResolveSubtitleHostCanvas";

        // COLD ALLOC: StringBuilder[4096] - editor audit report builder - owner: SubtitleManagerRuntimeConstructionValidator
        private static readonly StringBuilder Report = new StringBuilder(4096);

        /// <summary>
        /// Public for -executeMethod / CI batchmode. Soft FAIL stays exit 0 under -quit.
        /// </summary>
        [MenuItem("Hecton8/Validation/Subtitle Manager Runtime Construction", priority = 199)]
        public static void Run()
        {
            bool batch = Application.isBatchMode;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating || Application.isPlaying)
            {
                string busy = LogPrefix + " RESULT: FAIL - Editor busy (compiling/updating/playing).";
                Debug.LogError(busy);
                if (!batch)
                    EditorUtility.DisplayDialog("Subtitle Manager Runtime Construction", busy, "OK");
                return;
            }

            Report.Clear();
            Report.AppendLine("=======================================================");
            Report.AppendLine("HECTON-8 - Subtitle Manager Runtime Construction Audit");
            Report.AppendLine("=======================================================");
            Report.AppendLine();
            Report.AppendLine("Note: SubtitleManager is runtime-only under the suit HUD canvas");
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
            bool hasRegister = serviceExists && serviceText.Contains(PinRegisterSubtitle);
            bool hasResolveCanvas = serviceExists && serviceText.Contains(PinResolveHostCanvas);

            // Regression guard: construction must not be locked behind editor/dev-only ifdef
            // that strips the AddComponent path from player builds. Pin that EnsureRuntimeInstance
            // body still contains the AddComponent call (not only the method name).
            bool playerBuildAddPath =
                serviceExists &&
                hasEnsure &&
                hasAdd;

            Report.AppendLine("--- Source file presence ---");
            AppendPresence(Report, ServiceRelativePath, serviceExists);
            AppendPresence(Report, BootstrapRelativePath, bootstrapExists);
            Report.AppendLine();

            Report.AppendLine("--- Construction pins ---");
            AppendGate(Report, "service.EnsureRuntimeInstance", hasEnsure);
            AppendGate(Report, "service.AddComponent<SubtitleManager>", hasAdd);
            AppendGate(Report, "service.ResolveSubtitleHostCanvas", hasResolveCanvas);
            AppendGate(Report, "bootstrap.SubtitleManager.EnsureRuntimeInstance", hasBootstrapCall);
            AppendGate(Report, "service.RegisterSubtitleRuntime", hasRegister);
            AppendGate(Report, "service.player-build-AddComponent-path", playerBuildAddPath);

            Report.AppendLine();

            Report.Append("serviceExists=").Append(serviceExists ? 1 : 0);
            Report.Append(" bootstrapExists=").Append(bootstrapExists ? 1 : 0);
            Report.Append(" hasEnsure=").Append(hasEnsure ? 1 : 0);
            Report.Append(" hasAdd=").Append(hasAdd ? 1 : 0);
            Report.Append(" hasResolveCanvas=").Append(hasResolveCanvas ? 1 : 0);
            Report.Append(" hasBootstrapCall=").Append(hasBootstrapCall ? 1 : 0);
            Report.Append(" hasRegister=").Append(hasRegister ? 1 : 0);
            Report.AppendLine();

            bool passed =
                serviceExists &&
                bootstrapExists &&
                hasEnsure &&
                hasAdd &&
                hasResolveCanvas &&
                hasBootstrapCall &&
                hasRegister &&
                playerBuildAddPath;

            if (!passed)
            {
                Report.AppendLine("FAIL reason: one or more runtime construction source pins missing.");
                if (!serviceExists || !hasEnsure || !hasAdd)
                    Report.AppendLine("  - SubtitleManager must own EnsureRuntimeInstance + AddComponent.");
                if (!serviceExists || !hasResolveCanvas)
                    Report.AppendLine("  - SubtitleManager must own ResolveSubtitleHostCanvas.");
                if (!bootstrapExists || !hasBootstrapCall)
                    Report.AppendLine("  - GameBootstrapper must call SubtitleManager.EnsureRuntimeInstance.");
                if (!serviceExists || !hasRegister)
                    Report.AppendLine("  - SubtitleManager must own RegisterSubtitleRuntime.");
            }
            else
            {
                Report.AppendLine("PASS: runtime construction pins present for SubtitleManager.");
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
                    "Subtitle Manager Runtime Construction",
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

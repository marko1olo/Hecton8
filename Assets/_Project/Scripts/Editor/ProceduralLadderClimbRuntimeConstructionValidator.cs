#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Soft-FAIL CI pin for ProceduralLadderClimbRuntime construction.
    ///
    /// ProceduralLadderClimbRuntime is the sole GlobalRegistry.ProceduralLadderClimbRuntime owner
    /// and has zero scene/prefab GUID hits for f9e433f3e8f94484094909231551de54.
    /// Construction previously lived only behind #if UNITY_EDITOR || DEVELOPMENT_BUILD, so a
    /// shipped player build returned null from EnsureRuntimeInstance and ClimbableLadder
    /// TryBeginClimb permanently failed.
    ///
    /// Fix shape: EnsureRuntimeInstance player-build AddComponent path + RuntimeInitialize
    /// AfterSceneLoad self-bootstrap (class is internal; ClimbableLadder shares assembly via
    /// TryBeginClimb lazy path). Soft FAIL under -quit (no EditorApplication.Exit on audit fail).
    /// </summary>
    public static class ProceduralLadderClimbRuntimeConstructionValidator
    {
        private const string LogPrefix = "[ProceduralLadderClimbRuntimeConstructionValidator]";

        private const string ServiceRelativePath =
            "Assets/_Project/Scripts/Animation/Locomotion/ProceduralLadderClimbRuntime.cs";

        private const string ClimbableRelativePath =
            "Assets/_Project/Scripts/Gameplay/ClimbableLadder.cs";

        private const string PinEnsureRuntimeInstance = "EnsureRuntimeInstance";
        private const string PinAddComponent = "AddComponent<ProceduralLadderClimbRuntime>";
        private const string PinRegister = "RegisterProceduralLadderClimbRuntime";
        private const string PinTryBeginClimb = "TryBeginClimb";
        private const string PinBootstrapAfterSceneLoad = "BootstrapRuntimeAfterSceneLoad";
        private const string PinRuntimeInitialize = "RuntimeInitializeOnLoadMethod";
        private const string PinPlayerBuildComment = "Player-build construction path";
        private const string DeadPlayerElseReturnNull = "#else\r\n            return null;";
        private const string DeadPlayerElseReturnNullLf = "#else\n            return null;";

        // COLD ALLOC: StringBuilder[4096] - editor audit report builder - owner: ProceduralLadderClimbRuntimeConstructionValidator
        private static readonly StringBuilder Report = new StringBuilder(4096);

        /// <summary>
        /// Public for -executeMethod / CI batchmode. Soft FAIL stays exit 0 under -quit.
        /// </summary>
        [MenuItem("Hecton8/Validation/Procedural Ladder Climb Runtime Construction", priority = 200)]
        public static void Run()
        {
            bool batch = Application.isBatchMode;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating || Application.isPlaying)
            {
                string busy = LogPrefix + " RESULT: FAIL - Editor busy (compiling/updating/playing).";
                Debug.LogError(busy);
                if (!batch)
                    EditorUtility.DisplayDialog("Procedural Ladder Climb Runtime Construction", busy, "OK");
                return;
            }

            Report.Clear();
            Report.AppendLine("=======================================================");
            Report.AppendLine("HECTON-8 - Procedural Ladder Climb Runtime Construction Audit");
            Report.AppendLine("=======================================================");
            Report.AppendLine();
            Report.AppendLine("Note: ProceduralLadderClimbRuntime is runtime-only (scene/prefab");
            Report.AppendLine("GUID absence is EXPECTED; do not pin presence).");
            Report.AppendLine();

            string dataPath = Application.dataPath;
            string projectRoot = Directory.GetParent(dataPath) != null
                ? Directory.GetParent(dataPath).FullName
                : dataPath;

            string servicePath = Path.Combine(projectRoot, ServiceRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string climbablePath = Path.Combine(projectRoot, ClimbableRelativePath.Replace('/', Path.DirectorySeparatorChar));

            bool serviceExists = File.Exists(servicePath);
            bool climbableExists = File.Exists(climbablePath);
            string serviceText = serviceExists ? File.ReadAllText(servicePath) : string.Empty;
            string climbableText = climbableExists ? File.ReadAllText(climbablePath) : string.Empty;

            bool hasEnsure = serviceExists && serviceText.Contains(PinEnsureRuntimeInstance);
            bool hasAdd = serviceExists && serviceText.Contains(PinAddComponent);
            bool hasRegister = serviceExists && serviceText.Contains(PinRegister);
            bool hasTryBegin = serviceExists && serviceText.Contains(PinTryBeginClimb);
            bool hasBootstrapMethod = serviceExists && serviceText.Contains(PinBootstrapAfterSceneLoad);
            bool hasRuntimeInit = serviceExists && serviceText.Contains(PinRuntimeInitialize);
            bool hasPlayerBuildComment = serviceExists && serviceText.Contains(PinPlayerBuildComment);
            bool climbableCallsTryBegin =
                climbableExists && climbableText.Contains("ProceduralLadderClimbRuntime.TryBeginClimb");

            // Regression guard: construction must not be locked behind editor/dev-only ifdef
            // that strips the AddComponent path from player builds (#else return null).
            bool hasDeadPlayerElse =
                serviceExists &&
                (serviceText.Contains(DeadPlayerElseReturnNull) ||
                 serviceText.Contains(DeadPlayerElseReturnNullLf));

            bool playerBuildAddPath =
                serviceExists &&
                hasEnsure &&
                hasAdd &&
                hasPlayerBuildComment &&
                !hasDeadPlayerElse;

            Report.AppendLine("--- Source file presence ---");
            AppendPresence(Report, ServiceRelativePath, serviceExists);
            AppendPresence(Report, ClimbableRelativePath, climbableExists);
            Report.AppendLine();

            Report.AppendLine("--- Construction pins ---");
            AppendGate(Report, "service.EnsureRuntimeInstance", hasEnsure);
            AppendGate(Report, "service.AddComponent<ProceduralLadderClimbRuntime>", hasAdd);
            AppendGate(Report, "service.RegisterProceduralLadderClimbRuntime", hasRegister);
            AppendGate(Report, "service.TryBeginClimb", hasTryBegin);
            AppendGate(Report, "service.BootstrapRuntimeAfterSceneLoad", hasBootstrapMethod);
            AppendGate(Report, "service.RuntimeInitializeOnLoadMethod", hasRuntimeInit);
            AppendGate(Report, "service.player-build-AddComponent-path", playerBuildAddPath);
            AppendGate(Report, "climbable.ProceduralLadderClimbRuntime.TryBeginClimb", climbableCallsTryBegin);

            Report.AppendLine();

            Report.Append("serviceExists=").Append(serviceExists ? 1 : 0);
            Report.Append(" climbableExists=").Append(climbableExists ? 1 : 0);
            Report.Append(" hasEnsure=").Append(hasEnsure ? 1 : 0);
            Report.Append(" hasAdd=").Append(hasAdd ? 1 : 0);
            Report.Append(" hasRegister=").Append(hasRegister ? 1 : 0);
            Report.Append(" hasTryBegin=").Append(hasTryBegin ? 1 : 0);
            Report.Append(" hasBootstrapMethod=").Append(hasBootstrapMethod ? 1 : 0);
            Report.Append(" hasRuntimeInit=").Append(hasRuntimeInit ? 1 : 0);
            Report.Append(" playerBuildAddPath=").Append(playerBuildAddPath ? 1 : 0);
            Report.Append(" climbableCallsTryBegin=").Append(climbableCallsTryBegin ? 1 : 0);
            Report.AppendLine();

            bool passed =
                serviceExists &&
                climbableExists &&
                hasEnsure &&
                hasAdd &&
                hasRegister &&
                hasTryBegin &&
                hasBootstrapMethod &&
                hasRuntimeInit &&
                playerBuildAddPath &&
                climbableCallsTryBegin;

            if (!passed)
            {
                Report.AppendLine("FAIL reason: one or more runtime construction source pins missing.");
                if (!serviceExists || !hasEnsure || !hasAdd)
                    Report.AppendLine("  - ProceduralLadderClimbRuntime must own EnsureRuntimeInstance + AddComponent.");
                if (!serviceExists || !hasRegister)
                    Report.AppendLine("  - ProceduralLadderClimbRuntime must own RegisterProceduralLadderClimbRuntime.");
                if (!serviceExists || !hasTryBegin)
                    Report.AppendLine("  - ProceduralLadderClimbRuntime must own TryBeginClimb.");
                if (!serviceExists || !hasBootstrapMethod || !hasRuntimeInit)
                    Report.AppendLine("  - ProceduralLadderClimbRuntime must self-bootstrap via RuntimeInitialize AfterSceneLoad.");
                if (!playerBuildAddPath)
                    Report.AppendLine("  - Player-build AddComponent path must not be stripped by editor/dev-only ifdef.");
                if (!climbableExists || !climbableCallsTryBegin)
                    Report.AppendLine("  - ClimbableLadder must call ProceduralLadderClimbRuntime.TryBeginClimb.");
            }
            else
            {
                Report.AppendLine("PASS: runtime construction pins present for ProceduralLadderClimbRuntime.");
            }

            Report.Append("RESULT: ").AppendLine(passed ? "PASS" : "FAIL");
            string reportText = LogPrefix + " " + Report.ToString();

            if (passed)
                Debug.Log(reportText);
            else
                Debug.LogError(reportText);

            try
            {
                string logsDir = Path.Combine(projectRoot, "Logs");
                if (!Directory.Exists(logsDir))
                    Directory.CreateDirectory(logsDir);
                File.WriteAllText(
                    Path.Combine(logsDir, "procedural_ladder_climb_construction_validator.log"),
                    reportText);
            }
            catch
            {
                // Soft path: log write failure must not crash batch audit.
            }

            if (!batch)
            {
                EditorUtility.DisplayDialog(
                    "Procedural Ladder Climb Runtime Construction",
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

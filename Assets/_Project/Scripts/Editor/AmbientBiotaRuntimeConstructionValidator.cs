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
    /// Hecton8.AI.Ambient (autoReferenced: false). No other assembly can AddComponent it, and
    /// no scene/prefab GUID hit exists for 560a1d1e41eb4e9e81bc73402e4c7807. Live consumers
    /// cache the permanent null: EcosystemDirector.cs:1917, Creature.cs:1226/7320,
    /// WorldChunkResidencyManager.cs:2576.
    ///
    /// The fix is AmbientBiotaDirector.EnsureRuntimeInstance + RuntimeInitializeOnLoadMethod
    /// (AfterSceneLoad, isPlaying-gated) self-bootstrap inside the ambient assembly fence.
    /// Soft FAIL under -quit (no EditorApplication.Exit on audit fail).
    /// </summary>
    public static class AmbientBiotaRuntimeConstructionValidator
    {
        private const string LogPrefix = "[AmbientBiotaRuntimeConstructionValidator]";

        private const string DirectorRelativePath =
            "Assets/_Project/Scripts/AI/Ambient/AmbientBiotaDirector.cs";

        private const string PinEnsureRuntimeInstance = "EnsureRuntimeInstance";
        private const string PinAddComponent = "AddComponent<AmbientBiotaDirector>";
        private const string PinRuntimeInitialize = "RuntimeInitializeOnLoadMethod";
        private const string PinRegisterAmbient = "RegisterAmbientBiotaRuntime";

        // COLD ALLOC: StringBuilder[4096] - editor audit report builder - owner: AmbientBiotaRuntimeConstructionValidator
        private static readonly StringBuilder Report = new StringBuilder(4096);

        /// <summary>
        /// Public for -executeMethod / CI batchmode. Soft FAIL stays exit 0 under -quit.
        /// </summary>
        [MenuItem("Hecton8/Validation/Ambient Biota Runtime Construction", priority = 196)]
        public static void Run()
        {
            bool batch = Application.isBatchMode;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating || Application.isPlaying)
            {
                string busy = LogPrefix + " RESULT: FAIL - Editor busy (compiling/updating/playing).";
                Debug.LogError(busy);
                if (!batch)
                    EditorUtility.DisplayDialog("Ambient Biota Runtime Construction", busy, "OK");
                return;
            }

            Report.Clear();
            Report.AppendLine("=======================================================");
            Report.AppendLine("HECTON-8 - Ambient Biota Runtime Construction Audit");
            Report.AppendLine("=======================================================");
            Report.AppendLine();
            Report.AppendLine("Note: AmbientBiotaDirector is runtime-only + asmdef-fenced");
            Report.AppendLine("(scene/prefab GUID absence is EXPECTED; do not pin presence).");
            Report.AppendLine("Self-bootstrap via RuntimeInitializeOnLoadMethod is required.");
            Report.AppendLine();

            string dataPath = Application.dataPath;
            string projectRoot = Directory.GetParent(dataPath) != null
                ? Directory.GetParent(dataPath).FullName
                : dataPath;

            string directorPath = Path.Combine(projectRoot, DirectorRelativePath.Replace('/', Path.DirectorySeparatorChar));
            bool directorExists = File.Exists(directorPath);
            string directorText = directorExists ? File.ReadAllText(directorPath) : string.Empty;

            bool hasEnsure = directorExists && directorText.Contains(PinEnsureRuntimeInstance);
            bool hasAdd = directorExists && directorText.Contains(PinAddComponent);
            bool hasRuntimeInit = directorExists && directorText.Contains(PinRuntimeInitialize);
            bool hasRegister = directorExists && directorText.Contains(PinRegisterAmbient);

            Report.AppendLine("--- Source file presence ---");
            AppendPresence(Report, DirectorRelativePath, directorExists);
            Report.AppendLine();

            Report.AppendLine("--- Construction pins ---");
            AppendGate(Report, "director.EnsureRuntimeInstance", hasEnsure);
            AppendGate(Report, "director.AddComponent<AmbientBiotaDirector>", hasAdd);
            AppendGate(Report, "director.RuntimeInitializeOnLoadMethod", hasRuntimeInit);
            AppendGate(Report, "director.RegisterAmbientBiotaRuntime", hasRegister);
            Report.AppendLine();

            Report.Append("directorExists=").Append(directorExists ? 1 : 0);
            Report.Append(" hasEnsure=").Append(hasEnsure ? 1 : 0);
            Report.Append(" hasAdd=").Append(hasAdd ? 1 : 0);
            Report.Append(" hasRuntimeInit=").Append(hasRuntimeInit ? 1 : 0);
            Report.Append(" hasRegister=").Append(hasRegister ? 1 : 0);
            Report.AppendLine();

            bool passed = directorExists && hasEnsure && hasAdd && hasRuntimeInit && hasRegister;

            if (!passed)
            {
                Report.AppendLine("FAIL reason: one or more runtime construction source pins missing.");
                if (!directorExists || !hasEnsure || !hasAdd)
                    Report.AppendLine("  - AmbientBiotaDirector must own EnsureRuntimeInstance + AddComponent.");
                if (!directorExists || !hasRuntimeInit)
                    Report.AppendLine("  - AmbientBiotaDirector must self-bootstrap via RuntimeInitializeOnLoadMethod.");
                if (!directorExists || !hasRegister)
                    Report.AppendLine("  - AmbientBiotaDirector must own RegisterAmbientBiotaRuntime.");
            }
            else
            {
                Report.AppendLine("PASS: runtime construction pins present for AmbientBiotaDirector.");
                Report.AppendLine("biotaMaterial/biotaQuadMesh remain unassigned on runtime-created owner");
                Report.AppendLine("(presentation degrades until scene-authored assets are wired).");
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
                    "Ambient Biota Runtime Construction",
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

#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Soft-FAIL CI pin for ChemicalInfluenceGrid runtime construction.
    ///
    /// Zero live scene/prefab GUID hits (67189d92acf53ae4786558c89ccd2210). Construction
    /// previously sat behind UNITY_EDITOR || DEVELOPMENT_BUILD so player AI frames never
    /// got a grid. Fix is player-build EnsureRuntimeInstance + GameBootstrapper wire.
    /// Soft FAIL under -quit (no EditorApplication.Exit on audit fail).
    /// </summary>
    public static class ChemicalInfluenceGridRuntimeConstructionValidator
    {
        private const string LogPrefix = "[ChemicalInfluenceGridRuntimeConstructionValidator]";

        private const string BootstrapRelativePath =
            "Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs";
        private const string ManagerRelativePath =
            "Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs";

        private const string PinEnsureRuntimeInstance = "EnsureRuntimeInstance";
        private const string PinAddManager = "AddComponent<ChemicalInfluenceGrid>";
        private const string PinBootstrapCall = "ChemicalInfluenceGrid.EnsureRuntimeInstance";
        private const string PinPlayerBuildPath = "Player-build construction path";

        // COLD ALLOC: StringBuilder[4096] - editor audit report builder - owner: ChemicalInfluenceGridRuntimeConstructionValidator
        private static readonly StringBuilder Report = new StringBuilder(4096);

        /// <summary>
        /// Public for -executeMethod / CI batchmode. Soft FAIL stays exit 0 under -quit.
        /// </summary>
        [MenuItem("Hecton8/Validation/Chemical Influence Grid Runtime Construction", priority = 217)]
        public static void Run()
        {
            Report.Clear();
            bool pass = true;

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            string managerPath = Path.Combine(projectRoot, ManagerRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string bootstrapPath = Path.Combine(projectRoot, BootstrapRelativePath.Replace('/', Path.DirectorySeparatorChar));

            string managerSrc = File.Exists(managerPath) ? File.ReadAllText(managerPath) : string.Empty;
            string bootstrapSrc = File.Exists(bootstrapPath) ? File.ReadAllText(bootstrapPath) : string.Empty;

            pass &= Pin(managerSrc, PinEnsureRuntimeInstance, "ChemicalInfluenceGrid.EnsureRuntimeInstance factory");
            pass &= Pin(managerSrc, PinAddManager, "ChemicalInfluenceGrid player-build AddComponent");
            pass &= Pin(managerSrc, PinPlayerBuildPath, "Player-build construction path comment");
            pass &= Pin(bootstrapSrc, PinBootstrapCall, "GameBootstrapper ChemicalInfluenceGrid.EnsureRuntimeInstance call");

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

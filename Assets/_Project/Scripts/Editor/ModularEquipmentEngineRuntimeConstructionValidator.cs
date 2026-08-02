#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Soft-FAIL CI pin for ModularEquipmentEngine runtime construction.
    ///
    /// Zero live scene/prefab GUID hits. Factory EnsureRuntimeInstance existed with no
    /// bootstrap construction site, so IModularEquipmentService stayed null in player builds.
    /// Soft FAIL under -quit (no EditorApplication.Exit on audit fail).
    /// </summary>
    public static class ModularEquipmentEngineRuntimeConstructionValidator
    {
        private const string LogPrefix = "[ModularEquipmentEngineRuntimeConstructionValidator]";

        private const string BootstrapRelativePath =
            "Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs";
        private const string ManagerRelativePath =
            "Assets/_Project/Scripts/ModularEquipmentEngine.cs";

        private const string PinEnsureRuntimeInstance = "EnsureRuntimeInstance";
        private const string PinAddManager = "AddComponent<ModularEquipmentEngine>";
        private const string PinBootstrapCall = "ModularEquipmentEngine.EnsureRuntimeInstance";

        // COLD ALLOC: StringBuilder[4096] - editor audit report builder - owner: ModularEquipmentEngineRuntimeConstructionValidator
        private static readonly StringBuilder Report = new StringBuilder(4096);

        [MenuItem("Hecton8/Validation/Modular Equipment Engine Runtime Construction", priority = 219)]
        public static void Run()
        {
            Report.Clear();
            bool pass = true;

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            string managerPath = Path.Combine(projectRoot, ManagerRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string bootstrapPath = Path.Combine(projectRoot, BootstrapRelativePath.Replace('/', Path.DirectorySeparatorChar));

            string managerSrc = File.Exists(managerPath) ? File.ReadAllText(managerPath) : string.Empty;
            string bootstrapSrc = File.Exists(bootstrapPath) ? File.ReadAllText(bootstrapPath) : string.Empty;

            pass &= Pin(managerSrc, PinEnsureRuntimeInstance, "ModularEquipmentEngine.EnsureRuntimeInstance factory");
            pass &= Pin(managerSrc, PinAddManager, "ModularEquipmentEngine player-build AddComponent");
            pass &= Pin(bootstrapSrc, PinBootstrapCall, "GameBootstrapper ModularEquipmentEngine.EnsureRuntimeInstance call");

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

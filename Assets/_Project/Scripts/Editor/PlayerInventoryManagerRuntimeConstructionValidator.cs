#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Soft-FAIL CI pin for PlayerInventoryManager runtime construction.
    ///
    /// Tooling/inventory hot paths resolve through GlobalRegistry.RegisteredPlayerInventory.
    /// Without a create path the slot stays null when bootstrap reorders or skips the node.
    /// Fix is PlayerInventoryManager.EnsureRuntimeInstance (usable runtime resolve + registry
    /// guard + player-build AddComponent) called from GameBootstrapper.
    /// Soft FAIL under -quit (no EditorApplication.Exit on audit fail).
    /// </summary>
    public static class PlayerInventoryManagerRuntimeConstructionValidator
    {
        private const string LogPrefix = "[PlayerInventoryManagerRuntimeConstructionValidator]";

        private const string BootstrapRelativePath =
            "Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs";
        private const string ManagerRelativePath =
            "Assets/_Project/Scripts/Core/PlayerInventoryManager.cs";

        private const string PinEnsureRuntimeInstance = "EnsureRuntimeInstance";
        private const string PinAddManager = "AddComponent<PlayerInventoryManager>";
        private const string PinBootstrapCall = "PlayerInventoryManager.EnsureRuntimeInstance";
        private const string PinPlayerBuildPath = "Player-build construction path";

        // COLD ALLOC: StringBuilder[4096] - editor audit report builder - owner: PlayerInventoryManagerRuntimeConstructionValidator
        private static readonly StringBuilder Report = new StringBuilder(4096);

        /// <summary>
        /// Public for -executeMethod / CI batchmode. Soft FAIL stays exit 0 under -quit.
        /// </summary>
        [MenuItem("Hecton8/Validation/Player Inventory Manager Runtime Construction", priority = 228)]
        public static void Run()
        {
            Report.Clear();
            bool pass = true;

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            string managerPath = Path.Combine(projectRoot, ManagerRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string bootstrapPath = Path.Combine(projectRoot, BootstrapRelativePath.Replace('/', Path.DirectorySeparatorChar));

            string managerSrc = File.Exists(managerPath) ? File.ReadAllText(managerPath) : string.Empty;
            string bootstrapSrc = File.Exists(bootstrapPath) ? File.ReadAllText(bootstrapPath) : string.Empty;

            pass &= Pin(managerSrc, PinEnsureRuntimeInstance, "PlayerInventoryManager.EnsureRuntimeInstance factory");
            pass &= Pin(managerSrc, PinAddManager, "PlayerInventoryManager player-build AddComponent");
            pass &= Pin(managerSrc, PinPlayerBuildPath, "Player-build construction path comment");
            pass &= Pin(bootstrapSrc, PinBootstrapCall, "GameBootstrapper PlayerInventoryManager.EnsureRuntimeInstance call");

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

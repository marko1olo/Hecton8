#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Soft-FAIL CI pin for HazardZoneManager runtime construction.
    ///
    /// HazardZoneManager is the sole GlobalRegistry.HazardZones / IHazardZoneReadModel owner
    /// and has zero scene/prefab GUID hits for 008e5f84c0b54c23a0b2341464541d1e.
    /// Construction is owned by EnvironmentRuntimeContextService.EnsureHazardZoneManager
    /// (called from InitializeService). Both EnvironmentRuntimeContextService.EnsureRuntimeInstance
    /// and EnsureHazardZoneManager previously lived only behind #if UNITY_EDITOR || DEVELOPMENT_BUILD,
    /// so a shipped player build returned null from bootstrap Environment node and HazardZones
    /// stayed permanently null for FaunaBrain, EcosystemDirector, HectonHazardManager,
    /// CultivationManager, ResourceDistributionDirector.
    ///
    /// EnvironmentRuntimeContextService itself also has zero scene/prefab hits
    /// (GUID cbd923421b7c8d2438eaa99d10ba0449). Bootstrap already wires
    /// EnvironmentRuntimeContextService.EnsureRuntimeInstance + InitializeService.
    ///
    /// Fix shape: player-build AddComponent paths for EnvironmentRuntimeContextService and
    /// HazardZoneManager (remove editor/dev-only ifdef). Soft FAIL under -quit
    /// (no EditorApplication.Exit on audit fail).
    /// </summary>
    public static class HazardZoneManagerRuntimeConstructionValidator
    {
        private const string LogPrefix = "[HazardZoneManagerRuntimeConstructionValidator]";

        private const string EnvironmentRelativePath =
            "Assets/_Project/Scripts/Core/EnvironmentRuntimeContextService.cs";
        private const string HazardRelativePath =
            "Assets/_Project/Scripts/Gameplay/HazardZoneManager.cs";
        private const string BootstrapRelativePath =
            "Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs";

        private const string PinEnvEnsure = "EnsureRuntimeInstance";
        private const string PinEnvAdd = "AddComponent<EnvironmentRuntimeContextService>";
        private const string PinEnsureHazard = "EnsureHazardZoneManager";
        private const string PinAddHazard = "AddComponent<HazardZoneManager>";
        private const string PinInitializeService = "InitializeService";
        private const string PinPlayerBuildComment = "Player-build construction path";
        private const string PinHazardEnsure = "EnsureRuntimeInstance";
        private const string PinRegisterHazard = "RegisterHazardZoneRuntime";
        private const string PinBootstrapEnvEnsure = "EnvironmentRuntimeContextService.EnsureRuntimeInstance";
        private const string PinBootstrapInit = "environmentContextService.InitializeService";

        private const string DeadPlayerElseReturnNull = "#else\r\n            return null;";
        private const string DeadPlayerElseReturnNullLf = "#else\n            return null;";

        // COLD ALLOC: StringBuilder[4096] - editor audit report builder - owner: HazardZoneManagerRuntimeConstructionValidator
        private static readonly StringBuilder Report = new StringBuilder(4096);

        /// <summary>
        /// Public for -executeMethod / CI batchmode. Soft FAIL stays exit 0 under -quit.
        /// </summary>
        [MenuItem("Hecton8/Validation/Hazard Zone Manager Runtime Construction", priority = 201)]
        public static void Run()
        {
            bool batch = Application.isBatchMode;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating || Application.isPlaying)
            {
                string busy = LogPrefix + " RESULT: FAIL - Editor busy (compiling/updating/playing).";
                Debug.LogError(busy);
                if (!batch)
                    EditorUtility.DisplayDialog("Hazard Zone Manager Runtime Construction", busy, "OK");
                return;
            }

            Report.Clear();
            Report.AppendLine("=======================================================");
            Report.AppendLine("HECTON-8 - Hazard Zone Manager Runtime Construction Audit");
            Report.AppendLine("=======================================================");
            Report.AppendLine();
            Report.AppendLine("Note: HazardZoneManager + EnvironmentRuntimeContextService are");
            Report.AppendLine("runtime-only (scene/prefab GUID absence is EXPECTED; do not pin presence).");
            Report.AppendLine();

            string dataPath = Application.dataPath;
            string projectRoot = Directory.GetParent(dataPath) != null
                ? Directory.GetParent(dataPath).FullName
                : dataPath;

            string envPath = Path.Combine(projectRoot, EnvironmentRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string hazardPath = Path.Combine(projectRoot, HazardRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string bootstrapPath = Path.Combine(projectRoot, BootstrapRelativePath.Replace('/', Path.DirectorySeparatorChar));

            bool envExists = File.Exists(envPath);
            bool hazardExists = File.Exists(hazardPath);
            bool bootstrapExists = File.Exists(bootstrapPath);

            string envText = envExists ? File.ReadAllText(envPath) : string.Empty;
            string hazardText = hazardExists ? File.ReadAllText(hazardPath) : string.Empty;
            string bootstrapText = bootstrapExists ? File.ReadAllText(bootstrapPath) : string.Empty;

            bool envHasEnsure = envExists && envText.Contains(PinEnvEnsure);
            bool envHasAdd = envExists && envText.Contains(PinEnvAdd);
            bool envHasEnsureHazard = envExists && envText.Contains(PinEnsureHazard);
            bool envHasAddHazard = envExists && envText.Contains(PinAddHazard);
            bool envHasInit = envExists && envText.Contains(PinInitializeService);
            bool envHasPlayerBuildComment = envExists && envText.Contains(PinPlayerBuildComment);

            bool hazardHasEnsure = hazardExists && hazardText.Contains(PinHazardEnsure);
            bool hazardHasRegister = hazardExists && hazardText.Contains(PinRegisterHazard);

            bool bootstrapHasEnvEnsure = bootstrapExists && bootstrapText.Contains(PinBootstrapEnvEnsure);
            bool bootstrapHasInit = bootstrapExists && bootstrapText.Contains(PinBootstrapInit);

            // Regression guard: construction must not be locked behind editor/dev-only ifdef
            // that strips the AddComponent path from player builds (#else return null).
            bool envHasDeadPlayerElse =
                envExists &&
                (envText.Contains(DeadPlayerElseReturnNull) ||
                 envText.Contains(DeadPlayerElseReturnNullLf));

            // EnsureHazardZoneManager previously used #if UNITY_EDITOR || DEVELOPMENT_BUILD around
            // AddComponent without an #else return null — guard that the AddComponent is not still
            // wrapped by that ifdef by requiring player-build comment adjacent to construction.
            bool playerBuildEnvPath =
                envExists &&
                envHasEnsure &&
                envHasAdd &&
                envHasPlayerBuildComment &&
                !envHasDeadPlayerElse;

            bool playerBuildHazardPath =
                envExists &&
                envHasEnsureHazard &&
                envHasAddHazard &&
                envHasPlayerBuildComment &&
                !envHasDeadPlayerElse;

            Report.AppendLine("--- Source file presence ---");
            AppendPresence(Report, EnvironmentRelativePath, envExists);
            AppendPresence(Report, HazardRelativePath, hazardExists);
            AppendPresence(Report, BootstrapRelativePath, bootstrapExists);
            Report.AppendLine();

            Report.AppendLine("--- Construction pins ---");
            AppendGate(Report, "env.EnsureRuntimeInstance", envHasEnsure);
            AppendGate(Report, "env.AddComponent<EnvironmentRuntimeContextService>", envHasAdd);
            AppendGate(Report, "env.EnsureHazardZoneManager", envHasEnsureHazard);
            AppendGate(Report, "env.AddComponent<HazardZoneManager>", envHasAddHazard);
            AppendGate(Report, "env.InitializeService", envHasInit);
            AppendGate(Report, "env.player-build-Environment-path", playerBuildEnvPath);
            AppendGate(Report, "env.player-build-HazardZone-path", playerBuildHazardPath);
            AppendGate(Report, "hazard.EnsureRuntimeInstance", hazardHasEnsure);
            AppendGate(Report, "hazard.RegisterHazardZoneRuntime", hazardHasRegister);
            AppendGate(Report, "bootstrap.EnvironmentRuntimeContextService.EnsureRuntimeInstance", bootstrapHasEnvEnsure);
            AppendGate(Report, "bootstrap.environmentContextService.InitializeService", bootstrapHasInit);

            Report.AppendLine();

            Report.Append("envExists=").Append(envExists ? 1 : 0);
            Report.Append(" hazardExists=").Append(hazardExists ? 1 : 0);
            Report.Append(" bootstrapExists=").Append(bootstrapExists ? 1 : 0);
            Report.Append(" envHasEnsure=").Append(envHasEnsure ? 1 : 0);
            Report.Append(" envHasAdd=").Append(envHasAdd ? 1 : 0);
            Report.Append(" envHasEnsureHazard=").Append(envHasEnsureHazard ? 1 : 0);
            Report.Append(" envHasAddHazard=").Append(envHasAddHazard ? 1 : 0);
            Report.Append(" playerBuildEnvPath=").Append(playerBuildEnvPath ? 1 : 0);
            Report.Append(" playerBuildHazardPath=").Append(playerBuildHazardPath ? 1 : 0);
            Report.Append(" hazardHasEnsure=").Append(hazardHasEnsure ? 1 : 0);
            Report.Append(" hazardHasRegister=").Append(hazardHasRegister ? 1 : 0);
            Report.Append(" bootstrapHasEnvEnsure=").Append(bootstrapHasEnvEnsure ? 1 : 0);
            Report.Append(" bootstrapHasInit=").Append(bootstrapHasInit ? 1 : 0);
            Report.AppendLine();

            bool passed =
                envExists &&
                hazardExists &&
                bootstrapExists &&
                envHasEnsure &&
                envHasAdd &&
                envHasEnsureHazard &&
                envHasAddHazard &&
                envHasInit &&
                playerBuildEnvPath &&
                playerBuildHazardPath &&
                hazardHasEnsure &&
                hazardHasRegister &&
                bootstrapHasEnvEnsure &&
                bootstrapHasInit;

            if (!passed)
            {
                Report.AppendLine("FAIL reason: one or more runtime construction source pins missing.");
                if (!envExists || !envHasEnsure || !envHasAdd)
                    Report.AppendLine("  - EnvironmentRuntimeContextService must own EnsureRuntimeInstance + AddComponent.");
                if (!envExists || !envHasEnsureHazard || !envHasAddHazard)
                    Report.AppendLine("  - EnvironmentRuntimeContextService must own EnsureHazardZoneManager + AddComponent<HazardZoneManager>.");
                if (!playerBuildEnvPath)
                    Report.AppendLine("  - Environment player-build AddComponent path must not be stripped by editor/dev-only ifdef.");
                if (!playerBuildHazardPath)
                    Report.AppendLine("  - HazardZoneManager player-build AddComponent path must not be stripped by editor/dev-only ifdef.");
                if (!hazardExists || !hazardHasEnsure || !hazardHasRegister)
                    Report.AppendLine("  - HazardZoneManager must own EnsureRuntimeInstance + RegisterHazardZoneRuntime.");
                if (!bootstrapExists || !bootstrapHasEnvEnsure || !bootstrapHasInit)
                    Report.AppendLine("  - GameBootstrapper must call EnvironmentRuntimeContextService.EnsureRuntimeInstance + InitializeService.");
            }
            else
            {
                Report.AppendLine("PASS: runtime construction pins present for HazardZoneManager via EnvironmentRuntimeContextService.");
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
                    Path.Combine(logsDir, "hazard_zone_manager_construction_validator.log"),
                    reportText);
            }
            catch
            {
                // Soft path: log write failure must not crash batch audit.
            }

            if (!batch)
            {
                EditorUtility.DisplayDialog(
                    "Hazard Zone Manager Runtime Construction",
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

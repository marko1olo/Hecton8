#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Soft-FAIL CI pin for 01_ORBIT prologue runtime construction.
    ///
    /// OrbitalRelativityDirector + drop sequence + registry bridge + reentry VFX +
    /// world handoff + acoustic orchestrator are constructed at runtime by
    /// PrologueOrbitSceneBootstrap.EnsurePrologueRuntime via EnsureComponent<T>
    /// (TryGetComponent else AddComponent). They are NOT required as baked scene
    /// components on every open of 01_ORBIT — authoring absence is expected until
    /// Awake runs in play mode.
    ///
    /// This validator pins those source paths so BUILD_PLAYTEST "no construction
    /// site" cannot silently regress. Does not open scenes. Soft FAIL under -quit.
    /// </summary>
    public static class PrologueOrbitRuntimeConstructionValidator
    {
        private const string LogPrefix = "[PrologueOrbitRuntimeConstructionValidator]";

        private const string BootstrapRelativePath =
            "Assets/_Project/Scripts/Prologue/Space/PrologueOrbitSceneBootstrap.cs";
        private const string OrbitalRelativePath =
            "Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs";
        private const string BridgeRelativePath =
            "Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs";

        private const string PinEnsurePrologueRuntime = "EnsurePrologueRuntime";
        private const string PinEnsureOrbital = "EnsureComponent<OrbitalRelativityDirector>";
        private const string PinEnsureSequence = "EnsureComponent<AwaitableDropSequenceDirector>";
        private const string PinEnsureBridge = "EnsureComponent<PrologueSequenceRegistryBridge>";
        private const string PinEnsureReentry = "EnsureComponent<OrbitalDropReentryVfxController>";
        private const string PinEnsureHandoff = "EnsureComponent<PrologueWorldHandoffSceneLoader>";
        private const string PinEnsureAudio = "EnsureComponent<PrologueAcousticOrchestrator>";
        private const string PinAddComponentT = "return owner.AddComponent<T>()";
        private const string PinRegisterOrbital = "RegisterOrbitalDirectorRuntime";
        private const string PinIOrbitalDirector = "IOrbitalDirector";

        // COLD ALLOC: StringBuilder[4096] - editor audit report builder - owner: PrologueOrbitRuntimeConstructionValidator
        private static readonly StringBuilder Report = new StringBuilder(4096);

        /// <summary>
        /// Public for -executeMethod / CI batchmode. Soft FAIL stays exit 0 under -quit.
        /// </summary>
        [MenuItem("Hecton8/Validation/Validate Prologue Orbit Runtime Construction", priority = 191)]
        public static void ValidatePrologueOrbitRuntimeConstruction()
        {
            bool batch = Application.isBatchMode;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating || Application.isPlaying)
            {
                string busy = LogPrefix + " RESULT: FAIL — Editor busy (compiling/updating/playing).";
                Debug.LogError(busy);
                if (!batch)
                    EditorUtility.DisplayDialog("Prologue Orbit Runtime Construction", busy, "OK");
                return;
            }

            Report.Clear();
            Report.AppendLine("═══════════════════════════════════════════════════════");
            Report.AppendLine("HECTON-8 — Prologue Orbit Runtime Construction Audit");
            Report.AppendLine("═══════════════════════════════════════════════════════");
            Report.AppendLine();
            Report.AppendLine("Note: OrbitalRelativityDirector + bridge + reentry + handoff");
            Report.AppendLine("are runtime-constructed by PrologueOrbitSceneBootstrap");
            Report.AppendLine("(baked scene absence is EXPECTED until Awake play mode).");
            Report.AppendLine();

            string dataPath = Application.dataPath;
            // Application.dataPath ends in /Assets — climb one level to project root.
            string projectRoot = Directory.GetParent(dataPath) != null
                ? Directory.GetParent(dataPath).FullName
                : dataPath;

            string bootstrapPath = Path.Combine(projectRoot, BootstrapRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string orbitalPath = Path.Combine(projectRoot, OrbitalRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string bridgePath = Path.Combine(projectRoot, BridgeRelativePath.Replace('/', Path.DirectorySeparatorChar));

            bool bootstrapExists = File.Exists(bootstrapPath);
            bool orbitalExists = File.Exists(orbitalPath);
            bool bridgeExists = File.Exists(bridgePath);

            string bootstrapText = bootstrapExists ? File.ReadAllText(bootstrapPath) : string.Empty;
            string orbitalText = orbitalExists ? File.ReadAllText(orbitalPath) : string.Empty;
            string bridgeText = bridgeExists ? File.ReadAllText(bridgePath) : string.Empty;

            bool bootstrapHasEnsureRuntime = bootstrapExists && bootstrapText.Contains(PinEnsurePrologueRuntime);
            bool bootstrapHasOrbital = bootstrapExists && bootstrapText.Contains(PinEnsureOrbital);
            bool bootstrapHasSequence = bootstrapExists && bootstrapText.Contains(PinEnsureSequence);
            bool bootstrapHasBridge = bootstrapExists && bootstrapText.Contains(PinEnsureBridge);
            bool bootstrapHasReentry = bootstrapExists && bootstrapText.Contains(PinEnsureReentry);
            bool bootstrapHasHandoff = bootstrapExists && bootstrapText.Contains(PinEnsureHandoff);
            bool bootstrapHasAudio = bootstrapExists && bootstrapText.Contains(PinEnsureAudio);
            bool bootstrapHasAddComponent = bootstrapExists && bootstrapText.Contains(PinAddComponentT);
            bool orbitalHasRegister = orbitalExists && orbitalText.Contains(PinRegisterOrbital);
            bool orbitalHasInterface = orbitalExists && orbitalText.Contains(PinIOrbitalDirector);
            // Bridge file presence is the pin; content gate stays light (class name).
            bool bridgeHasClass = bridgeExists && bridgeText.Contains("class PrologueSequenceRegistryBridge");

            Report.AppendLine("--- Source file presence ---");
            AppendPresence(Report, BootstrapRelativePath, bootstrapExists);
            AppendPresence(Report, OrbitalRelativePath, orbitalExists);
            AppendPresence(Report, BridgeRelativePath, bridgeExists);
            Report.AppendLine();

            Report.AppendLine("--- Construction pins ---");
            AppendGate(Report, "bootstrap.EnsurePrologueRuntime", bootstrapHasEnsureRuntime);
            AppendGate(Report, "bootstrap.EnsureComponent<OrbitalRelativityDirector>", bootstrapHasOrbital);
            AppendGate(Report, "bootstrap.EnsureComponent<AwaitableDropSequenceDirector>", bootstrapHasSequence);
            AppendGate(Report, "bootstrap.EnsureComponent<PrologueSequenceRegistryBridge>", bootstrapHasBridge);
            AppendGate(Report, "bootstrap.EnsureComponent<OrbitalDropReentryVfxController>", bootstrapHasReentry);
            AppendGate(Report, "bootstrap.EnsureComponent<PrologueWorldHandoffSceneLoader>", bootstrapHasHandoff);
            AppendGate(Report, "bootstrap.EnsureComponent<PrologueAcousticOrchestrator>", bootstrapHasAudio);
            AppendGate(Report, "bootstrap.EnsureComponent AddComponent<T>", bootstrapHasAddComponent);
            AppendGate(Report, "orbital.RegisterOrbitalDirectorRuntime", orbitalHasRegister);
            AppendGate(Report, "orbital.IOrbitalDirector", orbitalHasInterface);
            AppendGate(Report, "bridge.class PrologueSequenceRegistryBridge", bridgeHasClass);
            Report.AppendLine();

            Report.Append("bootstrapExists=").Append(bootstrapExists ? 1 : 0);
            Report.Append(" orbitalExists=").Append(orbitalExists ? 1 : 0);
            Report.Append(" bridgeExists=").Append(bridgeExists ? 1 : 0);
            Report.Append(" bootstrapHasEnsureRuntime=").Append(bootstrapHasEnsureRuntime ? 1 : 0);
            Report.Append(" bootstrapHasOrbital=").Append(bootstrapHasOrbital ? 1 : 0);
            Report.Append(" bootstrapHasSequence=").Append(bootstrapHasSequence ? 1 : 0);
            Report.Append(" bootstrapHasBridge=").Append(bootstrapHasBridge ? 1 : 0);
            Report.Append(" bootstrapHasReentry=").Append(bootstrapHasReentry ? 1 : 0);
            Report.Append(" bootstrapHasHandoff=").Append(bootstrapHasHandoff ? 1 : 0);
            Report.Append(" bootstrapHasAudio=").Append(bootstrapHasAudio ? 1 : 0);
            Report.Append(" bootstrapHasAddComponent=").Append(bootstrapHasAddComponent ? 1 : 0);
            Report.Append(" orbitalHasRegister=").Append(orbitalHasRegister ? 1 : 0);
            Report.Append(" orbitalHasInterface=").Append(orbitalHasInterface ? 1 : 0);
            Report.Append(" bridgeHasClass=").Append(bridgeHasClass ? 1 : 0);
            Report.AppendLine();

            bool passed =
                bootstrapExists &&
                orbitalExists &&
                bridgeExists &&
                bootstrapHasEnsureRuntime &&
                bootstrapHasOrbital &&
                bootstrapHasSequence &&
                bootstrapHasBridge &&
                bootstrapHasReentry &&
                bootstrapHasHandoff &&
                bootstrapHasAudio &&
                bootstrapHasAddComponent &&
                orbitalHasRegister &&
                orbitalHasInterface &&
                bridgeHasClass;

            if (!passed)
            {
                Report.AppendLine("FAIL reason: one or more prologue orbit runtime construction source pins missing.");
                if (!bootstrapExists || !bootstrapHasEnsureRuntime)
                    Report.AppendLine("  • PrologueOrbitSceneBootstrap must own EnsurePrologueRuntime.");
                if (!bootstrapExists || !bootstrapHasOrbital || !bootstrapHasSequence || !bootstrapHasBridge ||
                    !bootstrapHasReentry || !bootstrapHasHandoff || !bootstrapHasAudio || !bootstrapHasAddComponent)
                    Report.AppendLine("  • EnsurePrologueRuntime must EnsureComponent orbital/sequence/bridge/reentry/handoff/audio via AddComponent<T>.");
                if (!orbitalExists || !orbitalHasRegister || !orbitalHasInterface)
                    Report.AppendLine("  • OrbitalRelativityDirector must implement IOrbitalDirector and call RegisterOrbitalDirectorRuntime.");
                if (!bridgeExists || !bridgeHasClass)
                    Report.AppendLine("  • PrologueSequenceRegistryBridge source must remain present.");
            }
            else
            {
                Report.AppendLine("PASS: runtime construction pins present for prologue orbit stack.");
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
                    "Prologue Orbit Runtime Construction",
                    passed
                        ? "PASS\nAll prologue orbit runtime construction source pins present."
                        : "FAIL\nOne or more source pins missing.\nSee Console.",
                    "OK");
            }
            // batchmode: soft FAIL under -quit (no EditorApplication.Exit on audit fail).
        }

        private static void AppendPresence(StringBuilder sb, string relativePath, bool exists)
        {
            sb.Append(exists ? "  OK  " : "  MISS ");
            sb.AppendLine(relativePath);
        }

        private static void AppendGate(StringBuilder sb, string label, bool ok)
        {
            sb.Append(ok ? "  OK  " : "  MISS ");
            sb.AppendLine(label);
        }
    }
}
#endif

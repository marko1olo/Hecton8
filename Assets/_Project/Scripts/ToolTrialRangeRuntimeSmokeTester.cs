using System;
using System.Collections.Generic;
using System.Threading;
using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Tool Trial Range Runtime Smoke Tester")]
    public sealed class ToolTrialRangeRuntimeSmokeTester : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Header("References")]
        [SerializeField] private PlayerToolManager toolManager;
        [FormerlySerializedAs("beaconNetwork")]
        [SerializeField] private MonoBehaviour beaconNetworkProvider;
        [SerializeField] private Transform playerRoot;

        [Header("Execution")]
        [SerializeField] private bool runOnStart = false;
        [SerializeField] private float startupDelay = 0.75f;
        [SerializeField] private float equipTimeout = 1.5f;
        [SerializeField] private float settleDelay = 0.15f;
        [SerializeField] private bool verboseLogging = false;

        // COLD ALLOC: List<GameObject>[512] - loaded-scene root traversal scratch for trial-range smoke reference resolution - owner: ToolTrialRangeRuntimeSmokeTester
        private static readonly List<GameObject> _sceneRootScratch = new List<GameObject>(512);
        // COLD ALLOC: List<MonoBehaviour>[16] - dev-only service scan scratch for trial-range smoke reference resolution - owner: ToolTrialRangeRuntimeSmokeTester
        private static readonly List<MonoBehaviour> _componentScratch = new List<MonoBehaviour>(16);
        // COLD ALLOC: GameObject[4] - original tool loadout snapshot reused by smoke-suite restore path - owner: ToolTrialRangeRuntimeSmokeTester
        private readonly GameObject[] _originalAssignments = new GameObject[4];
        private FixedCharBuffer _summaryProbeBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] - tool summary smoke assertion probe - owner: ToolTrialRangeRuntimeSmokeTester
        private FixedCharBuffer _directiveProbeBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] - tool directive smoke assertion probe - owner: ToolTrialRangeRuntimeSmokeTester

        private IBeaconNetworkService _beaconNetwork;
        private bool _isRunning;

        private void Awake()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            AutoResolve();
#endif
        }

        private void Start()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!runOnStart || _isRunning)
                return;

            _ = RunFullSuiteAsync(destroyCancellationToken);
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            AutoResolve();
        }
#endif

        [ContextMenu("Run Tool Trial Range Smoke Suite")]
        public void RunFromContextMenu()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_isRunning)
                return;

            _ = RunFullSuiteAsync(destroyCancellationToken);
#endif
        }

        private async Awaitable RunFullSuiteAsync(CancellationToken cancellationToken)
        {
            if (_isRunning)
                return;

            AutoResolve();
            if (toolManager == null || playerRoot == null)
            {
                LogSmokeWarning("[TrialRangeSmoke] Missing required references.");
                return;
            }

            Transform rangeRoot = FindSceneTransform("Tool_TrialRange");
            if (rangeRoot == null)
            {
                LogSmokeWarning("[TrialRangeSmoke] Tool_TrialRange root not found.");
                return;
            }

            _isRunning = true;
            try
            {
                if (startupDelay > 0f)
                    await DelayRealtimeAsync(startupDelay, cancellationToken);

                Vector3 originalPosition = playerRoot.position;
                Quaternion originalRotation = playerRoot.rotation;
                CaptureAssignments(_originalAssignments);
                int originalSlot = toolManager.CurrentSlotIndex;

                bool logisticsPass = await RunLogisticsPassAsync(rangeRoot, cancellationToken);
                ReportPass(logisticsPass);
                bool reconPass = await RunReconPassAsync(rangeRoot, cancellationToken);
                ReportPass(reconPass);
                bool recoveryPass = await RunRecoveryPassAsync(rangeRoot, cancellationToken);
                ReportPass(recoveryPass);
                bool servicePass = await RunServicePassAsync(rangeRoot, cancellationToken);
                ReportPass(servicePass);
                bool powerPass = await RunPowerPassAsync(rangeRoot, cancellationToken);
                ReportPass(powerPass);
                bool combatPass = await RunCombatPassAsync(rangeRoot, cancellationToken);
                ReportPass(combatPass);
                bool constructionPass = await RunConstructionPassAsync(rangeRoot, cancellationToken);
                ReportPass(constructionPass);
                bool endgamePass = await RunEndgameFlowPassAsync(rangeRoot, cancellationToken);
                ReportPass(endgamePass);

                await RestoreLoadoutAsync(_originalAssignments, originalSlot, cancellationToken);
                playerRoot.SetPositionAndRotation(originalPosition, originalRotation);

                if (logisticsPass && reconPass && recoveryPass && servicePass && powerPass && combatPass && constructionPass && endgamePass)
                    LogSmoke("[TrialRangeSmoke] PASS logistics=True recon=True recovery=True service=True power=True combat=True construction=True endgame=True");
                else
                    LogSmokeWarning("[TrialRangeSmoke] FAIL one or more lanes failed.");
            }
            catch (OperationCanceledException)
            {
                LogVerbose("Cancelled.");
            }
            catch (Exception)
            {
                LogSmokeError("[TrialRangeSmoke] UNHANDLED EXCEPTION.");
            }
            finally
            {
                _isRunning = false;
            }
        }

        private async Awaitable<bool> RunLogisticsPassAsync(Transform rangeRoot, CancellationToken cancellationToken)
        {
            Transform cargoWork = FindRelative(rangeRoot, "Lane_Cargo/Cargo_Work");
            Transform cargoHeavy = FindRelative(rangeRoot, "Lane_Cargo/Cargo_Heavy");
            Transform routeAnchor = FindRelative(rangeRoot, "Lane_BeaconRoute/Route_Anchor");
            Transform routeRelay = FindRelative(rangeRoot, "Lane_BeaconRoute/Route_Relay");
            if (cargoWork == null || cargoHeavy == null || routeAnchor == null || routeRelay == null)
            {
                LogSmokeWarning("[TrialRangeSmoke] Logistics lane is missing key authored targets.");
                return false;
            }

            bool equipOk = await EquipToolAsync<PropulsionTool>(0, cancellationToken);
            if (!equipOk)
                return false;

            if (!(toolManager.CurrentTool is PropulsionTool propulsion))
                return false;

            PositionPlayerForTarget(cargoWork, 4.5f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ToolSummaryContainsAll(propulsion, "WORK", "CARGO"))
            {
                LogSmokeWarning("[TrialRangeSmoke] Propulsion summary did not resolve work cargo.");
                return false;
            }

            equipOk = await EquipToolAsync<HarpoonLauncherTool>(1, cancellationToken);
            if (!equipOk)
                return false;

            if (!(toolManager.CurrentTool is HarpoonLauncherTool harpoon))
                return false;

            PositionPlayerForTarget(cargoHeavy, 6f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ToolSummaryContainsAny(harpoon, "HEAVY", "CARGO"))
            {
                LogSmokeWarning("[TrialRangeSmoke] Harpoon summary did not resolve heavy cargo.");
                return false;
            }

            equipOk = await EquipToolAsync<BeaconDeployerTool>(2, cancellationToken);
            if (!equipOk)
                return false;

            if (!(toolManager.CurrentTool is BeaconDeployerTool beaconTool))
                return false;

            PositionPlayerForTarget(routeAnchor, 2.5f);
            beaconTool.UsePrimary(0f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (_beaconNetwork == null || _beaconNetwork.ActiveCount <= 0 || !ToolSummaryContainsAny(beaconTool, "ANCHOR", "BEACON"))
            {
                LogSmokeWarning("[TrialRangeSmoke] Beacon tool did not establish anchor semantics.");
                return false;
            }

            PositionPlayerForTarget(routeRelay, 2.5f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ToolDirectiveContainsAny(beaconTool, "relay", "route", "readable"))
            {
                LogSmokeWarning("[TrialRangeSmoke] Beacon directive did not resolve relay guidance.");
                return false;
            }

            LogVerbose("Logistics pass complete.");
            return true;
        }

        private async Awaitable<bool> RunReconPassAsync(Transform rangeRoot, CancellationToken cancellationToken)
        {
            Transform darkHazard = FindRelative(rangeRoot, "Lane_DarkRoute/DarkRoute_HazardProbe");
            Transform darkPickup = FindRelative(rangeRoot, "Lane_DarkRoute/DarkRoute_Salvage_Close");
            Transform expeditionProbe = FindRelative(rangeRoot, "Lane_ScanCorridor/Scan_Poi_ExpeditionContact");
            Transform resourceProbe = FindRelative(rangeRoot, "Lane_ScanCorridor/Scan_Poi_ResourceCache");
            if (darkHazard == null || darkPickup == null || expeditionProbe == null || resourceProbe == null)
            {
                LogSmokeWarning("[TrialRangeSmoke] Recon lane is missing key authored targets.");
                return false;
            }

            bool equipOk = await EquipToolAsync<FlashlightTool>(0, cancellationToken);
            if (!equipOk)
                return false;

            if (!(toolManager.CurrentTool is FlashlightTool flashlight))
                return false;

            PositionPlayerForTarget(darkHazard, 10f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ToolDirectiveContainsAny(flashlight, "FOCUS", "frontier", "route"))
            {
                LogSmokeWarning("[TrialRangeSmoke] Flashlight directive did not resolve hazard/frontier guidance.");
                return false;
            }

            PositionPlayerForTarget(darkPickup, 3.5f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ToolDirectiveContainsAny(flashlight, "FLOOD", "pickup", "salvage"))
            {
                LogSmokeWarning("[TrialRangeSmoke] Flashlight directive did not resolve close salvage guidance.");
                return false;
            }

            equipOk = await EquipToolAsync<EnvironmentalAnalyzerTool>(1, cancellationToken);
            if (!equipOk)
                return false;

            if (!(toolManager.CurrentTool is EnvironmentalAnalyzerTool analyzer))
                return false;

            PositionPlayerForTarget(resourceProbe, 4.5f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ToolSummaryContainsAny(analyzer, "RESOURCE", "CACHE"))
            {
                LogSmokeWarning("[TrialRangeSmoke] Analyzer summary did not resolve resource semantics.");
                return false;
            }

            equipOk = await EquipToolAsync<ScannerTool>(2, cancellationToken);
            if (!equipOk)
                return false;

            if (!(toolManager.CurrentTool is ScannerTool scanner))
                return false;

            PositionPlayerForTarget(expeditionProbe, 5f);
            scanner.UsePrimary(0f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ToolDirectiveContainsAny(scanner, "checkpoint", "contact", "deeper", "cargo"))
            {
                LogSmokeWarning("[TrialRangeSmoke] Scanner directive did not resolve authored sweep semantics.");
                return false;
            }

            LogVerbose("Recon pass complete.");
            return true;
        }

        private async Awaitable<bool> RunCombatPassAsync(Transform rangeRoot, CancellationToken cancellationToken)
        {
            Transform dormant = FindRelative(rangeRoot, "Lane_CombatContacts/Combat_Dormant");
            Transform aggressive = FindRelative(rangeRoot, "Lane_CombatContacts/Combat_Aggressive");
            Transform fractured = FindRelative(rangeRoot, "Lane_CombatContacts/Combat_Fractured");
            Transform down = FindRelative(rangeRoot, "Lane_CombatContacts/Combat_Down");
            if (dormant == null || aggressive == null || fractured == null || down == null)
            {
                LogSmokeWarning("[TrialRangeSmoke] Combat lane is missing key authored targets.");
                return false;
            }

            bool equipOk = await EquipToolAsync<EnvironmentalAnalyzerTool>(0, cancellationToken);
            if (!equipOk || !(toolManager.CurrentTool is EnvironmentalAnalyzerTool analyzer))
                return false;

            PositionPlayerForTarget(aggressive, 4.5f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ToolSummaryContainsAny(analyzer, "AGGRESSIVE", "BIOFORM"))
            {
                LogSmokeWarning("[TrialRangeSmoke] Analyzer combat summary mismatch.");
                return false;
            }

            equipOk = await EquipToolAsync<StunPistolTool>(1, cancellationToken);
            if (!equipOk || !(toolManager.CurrentTool is StunPistolTool stun))
                return false;

            PositionPlayerForTarget(dormant, 5f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ToolDirectiveContainsAny(stun, "wake", "quiet", "shot"))
            {
                LogSmokeWarning("[TrialRangeSmoke] Stun directive mismatch on dormant target.");
                return false;
            }

            equipOk = await EquipToolAsync<KnifeTool>(2, cancellationToken);
            if (!equipOk || !(toolManager.CurrentTool is KnifeTool knife))
                return false;

            PositionPlayerForTarget(fractured, 2.8f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ToolDirectiveContainsAny(knife, "precision", "finish", "window"))
            {
                LogSmokeWarning("[TrialRangeSmoke] Knife directive mismatch on fractured target.");
                return false;
            }

            equipOk = await EquipToolAsync<HarpoonLauncherTool>(3, cancellationToken);
            if (!equipOk || !(toolManager.CurrentTool is HarpoonLauncherTool harpoon))
                return false;

            PositionPlayerForTarget(aggressive, 5.5f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ToolDirectiveContainsAny(harpoon, "control", "spacing", "disengage"))
            {
                LogSmokeWarning("[TrialRangeSmoke] Harpoon directive mismatch on aggressive target.");
                return false;
            }

            LogVerbose("Combat pass complete.");
            return true;
        }

        private async Awaitable<bool> RunRecoveryPassAsync(Transform rangeRoot, CancellationToken cancellationToken)
        {
            Transform salvagePickup = FindRelative(rangeRoot, "Lane_Salvage/Trial_Salvage_A");
            Transform activeNode = FindRelative(rangeRoot, "Lane_Salvage/Trial_Node_Active");
            Transform depletedNode = FindRelative(rangeRoot, "Lane_Salvage/Trial_Node_Depleted");
            if (salvagePickup == null || activeNode == null || depletedNode == null)
            {
                LogSmokeWarning("[TrialRangeSmoke] Recovery lane is missing key authored targets.");
                return false;
            }

            bool equipOk = await EquipToolAsync<SalvageSamplerTool>(0, cancellationToken);
            if (!equipOk || !(toolManager.CurrentTool is SalvageSamplerTool sampler))
                return false;

            PositionPlayerForTarget(salvagePickup, 2.8f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ToolSummaryContainsAny(sampler, "RECOVERY READY", "PACKAGE", "RECOVERY"))
            {
                LogSmokeWarning("[TrialRangeSmoke] Sampler summary did not resolve salvage pickup.");
                return false;
            }

            PositionPlayerForTarget(depletedNode, 3.4f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ToolSummaryContainsAny(sampler, "DEPLETED", "NODE"))
            {
                LogSmokeWarning("[TrialRangeSmoke] Sampler summary did not resolve depleted node.");
                return false;
            }

            equipOk = await EquipToolAsync<LaserCutter>(1, cancellationToken);
            if (!equipOk || !(toolManager.CurrentTool is LaserCutter cutter))
                return false;

            PositionPlayerForTarget(activeNode, 3.6f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ToolSummaryContainsAny(cutter, "RESOURCE", "CONTACT", "NODE"))
            {
                LogSmokeWarning("[TrialRangeSmoke] Cutter summary did not resolve active node.");
                return false;
            }

            equipOk = await EquipToolAsync<KnifeTool>(2, cancellationToken);
            if (!equipOk || !(toolManager.CurrentTool is KnifeTool knife))
                return false;

            PositionPlayerForTarget(depletedNode, 2.8f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ToolSummaryContainsAny(knife, "NODE", "DEPLETED"))
            {
                LogSmokeWarning("[TrialRangeSmoke] Knife summary did not resolve depleted node.");
                return false;
            }

            LogVerbose("Recovery pass complete.");
            return true;
        }

        private async Awaitable<bool> RunServicePassAsync(Transform rangeRoot, CancellationToken cancellationToken)
        {
            Transform damaged = FindRelative(rangeRoot, "Lane_ServiceModules/Trial_Module_Foundation_Damaged");
            Transform flooded = FindRelative(rangeRoot, "Lane_ServiceModules/Trial_Module_Corridor_Flooded");
            Transform control = FindRelative(rangeRoot, "Lane_ServiceModules/Trial_Module_Foundation_Control");
            if (damaged == null || flooded == null || control == null)
            {
                LogSmokeWarning("[TrialRangeSmoke] Service lane is missing key authored targets.");
                return false;
            }

            bool equipOk = await EquipToolAsync<RepairTool>(0, cancellationToken);
            if (!equipOk || !(toolManager.CurrentTool is RepairTool repair))
                return false;

            PositionPlayerForTarget(damaged, 4.5f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ToolSummaryContainsAny(repair, "SERVICE", "RESPONSE", "IMMEDIATE", "CRITICAL", "ACTIVE"))
            {
                LogSmokeWarning("[TrialRangeSmoke] Repair summary did not resolve damaged module.");
                return false;
            }

            PositionPlayerForTarget(flooded, 4.8f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ToolDirectiveContainsAny(repair, "drain", "wait", "service", "power"))
            {
                LogSmokeWarning("[TrialRangeSmoke] Repair directive did not resolve flooded module guidance.");
                return false;
            }

            equipOk = await EquipToolAsync<LaserCutter>(1, cancellationToken);
            if (!equipOk || !(toolManager.CurrentTool is LaserCutter cutter))
                return false;

            PositionPlayerForTarget(control, 4.8f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ToolSummaryContainsAny(cutter, "MODULE", "LOCKED", "RECOVERY", "CONTACT"))
            {
                LogSmokeWarning("[TrialRangeSmoke] Cutter summary did not resolve service module.");
                return false;
            }

            equipOk = await EquipToolAsync<EnvironmentalAnalyzerTool>(2, cancellationToken);
            if (!equipOk || !(toolManager.CurrentTool is EnvironmentalAnalyzerTool analyzer))
                return false;

            PositionPlayerForTarget(flooded, 4.8f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ToolSummaryContainsAny(analyzer, "FLOODED", "SERVICE", "MODULE"))
            {
                LogSmokeWarning("[TrialRangeSmoke] Analyzer summary did not resolve flooded service semantics.");
                return false;
            }

            LogVerbose("Service pass complete.");
            return true;
        }

        private async Awaitable<bool> RunPowerPassAsync(Transform rangeRoot, CancellationToken cancellationToken)
        {
            Transform turbine = FindRelative(rangeRoot, "Lane_PowerOps/Power_CurrentTurbine");
            Transform relay = FindRelative(rangeRoot, "Lane_PowerOps/Power_RelayPylon");
            Transform pump = FindRelative(rangeRoot, "Lane_PowerOps/Power_ServicePump");
            Transform route = FindRelative(rangeRoot, "Lane_PowerOps/Power_ServiceRoute");
            if (turbine == null || relay == null || pump == null || route == null)
            {
                LogSmokeWarning("[TrialRangeSmoke] Power lane is missing key authored targets.");
                return false;
            }

            if (!VerifyRecommendedPreset(turbine, 5f, "CONSTRUCTION"))
                return false;

            await DelayRealtimeAsync(settleDelay, cancellationToken);

            if (!VerifyRecommendedPreset(relay, 5f, "CONSTRUCTION"))
                return false;

            await DelayRealtimeAsync(settleDelay, cancellationToken);

            if (!VerifyRecommendedPreset(pump, 5f, "CONSTRUCTION"))
                return false;

            bool equipOk = await EquipToolAsync<EnvironmentalAnalyzerTool>(0, cancellationToken);
            if (!equipOk || !(toolManager.CurrentTool is EnvironmentalAnalyzerTool analyzer))
                return false;

            PositionPlayerForTarget(turbine, 4.8f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ToolSummaryContainsAny(analyzer, "POWER", "GENERATION", "CURRENT"))
            {
                LogSmokeWarning("[TrialRangeSmoke] Analyzer summary did not resolve power generation semantics.");
                return false;
            }

            equipOk = await EquipToolAsync<FlashlightTool>(1, cancellationToken);
            if (!equipOk || !(toolManager.CurrentTool is FlashlightTool flashlight))
                return false;

            PositionPlayerForTarget(route, 8f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ToolDirectiveContainsAny(flashlight, "FOCUS", "service", "power", "generator"))
            {
                LogSmokeWarning("[TrialRangeSmoke] Flashlight directive did not resolve power/service guidance.");
                return false;
            }

            LogVerbose("Power pass complete.");
            return true;
        }

        private async Awaitable<bool> RunConstructionPassAsync(Transform rangeRoot, CancellationToken cancellationToken)
        {
            Transform clearLane = FindRelative(rangeRoot, "Lane_ConstructionOps/Construct_ClearLane");
            Transform blockedLane = FindRelative(rangeRoot, "Lane_ConstructionOps/Construct_Blocker");
            Transform socketGuide = FindRelative(rangeRoot, "Lane_ConstructionOps/Construct_SocketGuide");
            if (clearLane == null || blockedLane == null || socketGuide == null)
            {
                LogSmokeWarning("[TrialRangeSmoke] Construction lane is missing key authored targets.");
                return false;
            }

            if (!VerifyRecommendedPreset(clearLane, 5f, "CONSTRUCTION"))
                return false;

            await DelayRealtimeAsync(settleDelay, cancellationToken);

            if (!VerifyRecommendedPreset(blockedLane, 6f, "CONSTRUCTION"))
                return false;

            await DelayRealtimeAsync(settleDelay, cancellationToken);

            if (!VerifyRecommendedPreset(socketGuide, 4f, "CONSTRUCTION"))
                return false;

            bool equipOk = await EquipToolAsync<BuilderTool>(3, cancellationToken);
            if (!equipOk || !(toolManager.CurrentTool is BuilderTool builder))
                return false;

            await DelayRealtimeAsync(settleDelay, cancellationToken);

            if (!ToolSummaryContainsAny(builder, "READY", "BLOCKED", "MISSING", "MODULE", "SNAPPED", "NO MODULE"))
            {
                LogSmokeWarning("[TrialRangeSmoke] Builder summary did not resolve an operational state.");
                return false;
            }

            if (!ToolDirectiveContainsAny(builder, "build", "place", "module", "resources", "snap", "deployment"))
            {
                LogSmokeWarning("[TrialRangeSmoke] Builder directive did not resolve field guidance.");
                return false;
            }

            LogVerbose("Construction pass complete.");
            return true;
        }

        private async Awaitable<bool> RunEndgameFlowPassAsync(Transform rangeRoot, CancellationToken cancellationToken)
        {
            Transform cargo = FindRelative(rangeRoot, "Lane_EndgameOps/Ops_Cargo_Work");
            Transform salvage = FindRelative(rangeRoot, "Lane_EndgameOps/Ops_Salvage");
            Transform service = FindRelative(rangeRoot, "Lane_EndgameOps/Ops_Service_Flooded");
            Transform hazard = FindRelative(rangeRoot, "Lane_EndgameOps/Ops_Hazard");
            Transform combat = FindRelative(rangeRoot, "Lane_EndgameOps/Ops_Combat_Aggressive");
            Transform frontier = FindRelative(rangeRoot, "Lane_EndgameOps/Ops_Frontier");
            if (cargo == null || salvage == null || service == null || hazard == null || combat == null || frontier == null)
            {
                LogSmokeWarning("[TrialRangeSmoke] Endgame lane is missing key authored targets.");
                return false;
            }

            if (!VerifyRecommendedPreset(cargo, 5f, "FIELD RECOVERY"))
                return false;

            await DelayRealtimeAsync(settleDelay, cancellationToken);

            if (!VerifyRecommendedPreset(salvage, 3f, "FIELD RECOVERY"))
                return false;

            await DelayRealtimeAsync(settleDelay, cancellationToken);

            if (!VerifyRecommendedPreset(service, 5f, "CONSTRUCTION"))
                return false;

            await DelayRealtimeAsync(settleDelay, cancellationToken);

            if (!VerifyRecommendedPreset(hazard, 6f, "EXPLORATION"))
                return false;

            await DelayRealtimeAsync(settleDelay, cancellationToken);

            if (!VerifyRecommendedPreset(combat, 5f, "DEFENSE"))
                return false;

            await DelayRealtimeAsync(settleDelay, cancellationToken);

            if (!VerifyRecommendedPreset(frontier, 4f, "EXPLORATION"))
                return false;

            LogVerbose("Endgame flow pass complete.");
            return true;
        }

        private async Awaitable<bool> EquipToolAsync<TTool>(int slotIndex, CancellationToken cancellationToken) where TTool : PlayerTool
        {
            GameObject prefab = toolManager.GetKnownToolPrefabForToolType<TTool>();
            if (prefab == null)
            {
                LogSmokeWarning("[TrialRangeSmoke] Missing prefab registration for requested tool.");
                return false;
            }

            if (!IsToolManagerHolstered())
            {
                toolManager.Holster();
                if (!await WaitUntilHolsteredAsync(cancellationToken))
                    return false;
            }

            toolManager.SetAssignedToolPrefab(slotIndex, prefab, holsterIfCurrentInvalid: false);
            toolManager.SwitchToSlot(slotIndex);
            if (!await WaitUntilEquippedAsync<TTool>(cancellationToken))
                return false;

            return toolManager != null && !toolManager.IsSwapping && toolManager.CurrentTool is TTool;
        }

        private async Awaitable RestoreLoadoutAsync(
            GameObject[] originalAssignments,
            int originalSlot,
            CancellationToken cancellationToken)
        {
            if (toolManager == null)
                return;

            toolManager.Holster();
            await WaitUntilHolsteredAsync(cancellationToken);

            if (originalAssignments != null)
            {
                for (int i = 0; i < originalAssignments.Length; i++)
                    toolManager.SetAssignedToolPrefab(i, originalAssignments[i], holsterIfCurrentInvalid: false);
            }

            if (originalSlot >= 0)
                toolManager.SwitchToSlot(originalSlot);
        }

        private void CaptureAssignments(GameObject[] snapshot)
        {
            if (snapshot == null)
                return;

            for (int i = 0; i < snapshot.Length; i++)
                snapshot[i] = toolManager.GetAssignedToolPrefab(i);
        }

        private void PositionPlayerForTarget(Transform target, float distance)
        {
            Vector3 targetPosition = target.position;
            Vector3 playerPosition = playerRoot.position;
            Vector3 toTarget = targetPosition - playerPosition;
            Vector3 flatForward = new Vector3(toTarget.x, 0f, toTarget.z);
            float flatForwardSq = flatForward.sqrMagnitude;
            if (flatForwardSq < 0.001f)
            {
                flatForward = playerRoot.forward;
                flatForward.y = 0f;
                flatForwardSq = flatForward.sqrMagnitude;
                if (flatForwardSq < 0.001f)
                {
                    flatForward = Vector3.forward;
                    flatForwardSq = 1f;
                }
            }

            Vector3 normalizedForward = flatForward * math.rsqrt(flatForwardSq);
            Vector3 position = targetPosition - normalizedForward * distance;
            position.y = playerPosition.y;
            playerRoot.SetPositionAndRotation(position, Quaternion.LookRotation(normalizedForward, Vector3.up));
        }

        private bool VerifyRecommendedPreset(Transform target, float distance, string expectedPreset)
        {
            PositionPlayerForTarget(target, distance);

            if (!FieldLoadoutAdvisor.TryBuildForwardAdvice(playerRoot, 18f, Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask, out FieldLoadoutAdvisor.LoadoutAdvice advice))
            {
                LogSmokeWarning("[TrialRangeSmoke] Loadout advice did not resolve.");
                return false;
            }

            if (!string.Equals(advice.PresetName, expectedPreset, System.StringComparison.OrdinalIgnoreCase))
            {
                LogSmokeWarning("[TrialRangeSmoke] Loadout advice mismatch.");
                return false;
            }

            return true;
        }

        private bool ToolSummaryContainsAny(
            PlayerTool tool,
            string needle0,
            string needle1 = null,
            string needle2 = null,
            string needle3 = null,
            string needle4 = null,
            string needle5 = null)
        {
            if (tool == null)
                return false;

            _summaryProbeBuffer.Clear();
            tool.WriteOperationalSummary(ref _summaryProbeBuffer);
            return ContainsAny(_summaryProbeBuffer.AsSpan(), needle0, needle1, needle2, needle3, needle4, needle5);
        }

        private bool ToolSummaryContainsAll(
            PlayerTool tool,
            string needle0,
            string needle1 = null,
            string needle2 = null,
            string needle3 = null,
            string needle4 = null,
            string needle5 = null)
        {
            if (tool == null)
                return false;

            _summaryProbeBuffer.Clear();
            tool.WriteOperationalSummary(ref _summaryProbeBuffer);
            return ContainsAll(_summaryProbeBuffer.AsSpan(), needle0, needle1, needle2, needle3, needle4, needle5);
        }

        private bool ToolDirectiveContainsAny(
            PlayerTool tool,
            string needle0,
            string needle1 = null,
            string needle2 = null,
            string needle3 = null,
            string needle4 = null,
            string needle5 = null)
        {
            if (tool == null)
                return false;

            _directiveProbeBuffer.Clear();
            tool.WriteOperationalDirective(ref _directiveProbeBuffer);
            return ContainsAny(_directiveProbeBuffer.AsSpan(), needle0, needle1, needle2, needle3, needle4, needle5);
        }

        private static bool ContainsAny(
            ReadOnlySpan<char> source,
            string needle0,
            string needle1 = null,
            string needle2 = null,
            string needle3 = null,
            string needle4 = null,
            string needle5 = null)
        {
            if (source.IsEmpty)
                return false;

            return ContainsIgnoreCase(source, needle0) ||
                   ContainsIgnoreCase(source, needle1) ||
                   ContainsIgnoreCase(source, needle2) ||
                   ContainsIgnoreCase(source, needle3) ||
                   ContainsIgnoreCase(source, needle4) ||
                   ContainsIgnoreCase(source, needle5);
        }

        private static bool ContainsAll(
            ReadOnlySpan<char> source,
            string needle0,
            string needle1 = null,
            string needle2 = null,
            string needle3 = null,
            string needle4 = null,
            string needle5 = null)
        {
            if (source.IsEmpty)
                return false;

            return ContainsIgnoreCase(source, needle0) &&
                   ContainsOptionalIgnoreCase(source, needle1) &&
                   ContainsOptionalIgnoreCase(source, needle2) &&
                   ContainsOptionalIgnoreCase(source, needle3) &&
                   ContainsOptionalIgnoreCase(source, needle4) &&
                   ContainsOptionalIgnoreCase(source, needle5);
        }

        private static bool ContainsOptionalIgnoreCase(ReadOnlySpan<char> source, string needle)
        {
            return string.IsNullOrEmpty(needle) || ContainsIgnoreCase(source, needle);
        }

        private static bool ContainsIgnoreCase(ReadOnlySpan<char> source, string needle)
        {
            if (source.IsEmpty || string.IsNullOrEmpty(needle) || needle.Length > source.Length)
                return false;

            ReadOnlySpan<char> needleSpan = needle.AsSpan();
            int maxStartIndex = source.Length - needleSpan.Length;
            for (int startIndex = 0; startIndex <= maxStartIndex; startIndex++)
            {
                bool matched = true;
                for (int needleIndex = 0; needleIndex < needleSpan.Length; needleIndex++)
                {
                    if (char.ToUpperInvariant(source[startIndex + needleIndex]) ==
                        char.ToUpperInvariant(needleSpan[needleIndex]))
                        continue;

                    matched = false;
                    break;
                }

                if (matched)
                    return true;
            }

            return false;
        }

        private static async Awaitable DelayRealtimeAsync(float seconds, CancellationToken cancellationToken)
        {
            float deadline = Time.realtimeSinceStartup + math.max(0f, seconds);
            while (Time.realtimeSinceStartup < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: cancellationToken);
            }
        }

        private async Awaitable<bool> WaitUntilHolsteredAsync(CancellationToken cancellationToken)
        {
            float deadline = Time.realtimeSinceStartup + math.max(0.05f, equipTimeout);
            while (Time.realtimeSinceStartup < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsToolManagerHolstered())
                    return true;

                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: cancellationToken);
            }

            LogSmokeWarning("[TrialRangeSmoke] TIMEOUT waiting for holster.");
            return false;
        }

        private async Awaitable<bool> WaitUntilEquippedAsync<TTool>(CancellationToken cancellationToken) where TTool : PlayerTool
        {
            float deadline = Time.realtimeSinceStartup + math.max(0.05f, equipTimeout);
            while (Time.realtimeSinceStartup < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (toolManager != null && !toolManager.IsSwapping && toolManager.CurrentTool is TTool)
                    return true;

                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: cancellationToken);
            }

            LogSmokeWarning("[TrialRangeSmoke] TIMEOUT waiting for requested tool equip.");
            return false;
        }

        private bool IsToolManagerHolstered()
        {
            return toolManager != null &&
                   !toolManager.IsSwapping &&
                   toolManager.CurrentTool == null &&
                   toolManager.CurrentSlotIndex < 0;
        }

        private void AutoResolve()
        {
            if (toolManager == null)
                toolManager = FindSceneObjectIncludingInactive<PlayerToolManager>();
            if (_beaconNetwork == null)
                _beaconNetwork = beaconNetworkProvider as IBeaconNetworkService;
            if (_beaconNetwork == null)
                _beaconNetwork = GlobalRegistry.BeaconNetworkService;
            if (_beaconNetwork == null)
                _beaconNetwork = FindSceneServiceIncludingInactive<IBeaconNetworkService>();
            if (beaconNetworkProvider == null && _beaconNetwork is MonoBehaviour beaconNetworkBehaviour)
                beaconNetworkProvider = beaconNetworkBehaviour;
            if (playerRoot == null && toolManager != null)
                playerRoot = toolManager.transform;
        }

        private Transform FindSceneTransform(string name)
        {
            GameObject target = null;
            Hecton8.World.WorldRuntimeReferenceUtility.TryResolveScenePath(ref target, name);
            return target != null ? target.transform : null;
        }

        private Transform FindRelative(Transform root, string path)
        {
            return root != null ? root.Find(path) : null;
        }

        private void LogVerbose(string message)
        {
            if (verboseLogging)
                LogSmoke(message);
        }

        private static void ReportPass(bool result)
        {
            if (result)
                LogSmoke("[TrialRangeSmoke] PASS lane=True");
            else
                LogSmokeWarning("[TrialRangeSmoke] FAIL lane=False");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogSmoke(string message)
        {
            Hecton8.Core.H8Debug.Log(message);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogSmokeWarning(string message)
        {
            Debug.LogWarning(message);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogSmokeError(string message)
        {
            Debug.LogError(message);
        }

        private static T FindSceneObjectIncludingInactive<T>() where T : Component
        {
            _sceneRootScratch.Clear();

            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                scene.GetRootGameObjects(_sceneRootScratch);
                for (int rootIndex = 0; rootIndex < _sceneRootScratch.Count; rootIndex++)
                {
                    GameObject root = _sceneRootScratch[rootIndex];
                    if (root == null)
                        continue;

                    T candidate = FindComponentInChildrenIncludingInactive<T>(root.transform);
                    if (candidate != null)
                    {
                        _sceneRootScratch.Clear();
                        return candidate;
                    }
                }

                _sceneRootScratch.Clear();
            }

            return null;
        }

        private static T FindComponentInChildrenIncludingInactive<T>(Transform root) where T : Component
        {
            if (root == null)
                return null;

            if (root.TryGetComponent(out T candidate))
                return candidate;

            for (int i = 0; i < root.childCount; i++)
            {
                T match = FindComponentInChildrenIncludingInactive<T>(root.GetChild(i));
                if (match != null)
                    return match;
            }

            return null;
        }

        private static TService FindSceneServiceIncludingInactive<TService>() where TService : class
        {
            _sceneRootScratch.Clear();

            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                scene.GetRootGameObjects(_sceneRootScratch);
                for (int rootIndex = 0; rootIndex < _sceneRootScratch.Count; rootIndex++)
                {
                    GameObject root = _sceneRootScratch[rootIndex];
                    if (root == null)
                        continue;

                    TService candidate = FindServiceInChildrenIncludingInactive<TService>(root.transform);
                    if (candidate != null)
                    {
                        _sceneRootScratch.Clear();
                        return candidate;
                    }
                }

                _sceneRootScratch.Clear();
            }

            return null;
        }

        private static TService FindServiceInChildrenIncludingInactive<TService>(Transform root) where TService : class
        {
            if (root == null)
                return null;

            root.GetComponents(_componentScratch);
            for (int i = 0; i < _componentScratch.Count; i++)
            {
                if (_componentScratch[i] is TService service)
                {
                    _componentScratch.Clear();
                    return service;
                }
            }

            _componentScratch.Clear();

            for (int i = 0; i < root.childCount; i++)
            {
                TService match = FindServiceInChildrenIncludingInactive<TService>(root.GetChild(i));
                if (match != null)
                    return match;
            }

            return null;
        }
#endif
    }
}

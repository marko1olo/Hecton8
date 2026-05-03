using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Tool Trial Range Runtime Smoke Tester")]
    public sealed class ToolTrialRangeRuntimeSmokeTester : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerToolManager toolManager;
        [SerializeField] private BeaconNetworkSystem beaconNetwork;
        [SerializeField] private Transform playerRoot;

        [Header("Execution")]
        [SerializeField] private bool runOnStart = false;
        [SerializeField] private float startupDelay = 0.75f;
        [SerializeField] private float equipTimeout = 1.5f;
        [SerializeField] private float settleDelay = 0.15f;
        [SerializeField] private bool verboseLogging = false;

        // COLD ALLOC: List<GameObject>[512] - loaded-scene root traversal scratch for trial-range smoke reference resolution - owner: ToolTrialRangeRuntimeSmokeTester
        private static readonly List<GameObject> _sceneRootScratch = new List<GameObject>(512);
        // COLD ALLOC: GameObject[4] - original tool loadout snapshot reused by smoke-suite restore path - owner: ToolTrialRangeRuntimeSmokeTester
        private readonly GameObject[] _originalAssignments = new GameObject[4];

        private bool _isRunning;

        private void Awake()
        {
            AutoResolve();
        }

        private void Start()
        {
            if (!runOnStart || _isRunning)
                return;

            _ = RunFullSuiteAsync(destroyCancellationToken);
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
            if (_isRunning)
                return;

            _ = RunFullSuiteAsync(destroyCancellationToken);
        }

        private async Awaitable RunFullSuiteAsync(CancellationToken cancellationToken)
        {
            if (_isRunning)
                return;

            AutoResolve();
            if (toolManager == null || playerRoot == null)
            {
                Debug.LogWarning($"[TrialRangeSmoke] Missing references tools={(toolManager != null ? "Y" : "N")} player={(playerRoot != null ? "Y" : "N")}");
                return;
            }

            Transform rangeRoot = FindSceneTransform("Tool_TrialRange");
            if (rangeRoot == null)
            {
                Debug.LogWarning("[TrialRangeSmoke] Tool_TrialRange root not found.");
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
                ReportPass("logistics", logisticsPass);
                bool reconPass = await RunReconPassAsync(rangeRoot, cancellationToken);
                ReportPass("recon", reconPass);
                bool recoveryPass = await RunRecoveryPassAsync(rangeRoot, cancellationToken);
                ReportPass("recovery", recoveryPass);
                bool servicePass = await RunServicePassAsync(rangeRoot, cancellationToken);
                ReportPass("service", servicePass);
                bool powerPass = await RunPowerPassAsync(rangeRoot, cancellationToken);
                ReportPass("power", powerPass);
                bool combatPass = await RunCombatPassAsync(rangeRoot, cancellationToken);
                ReportPass("combat", combatPass);
                bool constructionPass = await RunConstructionPassAsync(rangeRoot, cancellationToken);
                ReportPass("construction", constructionPass);
                bool endgamePass = await RunEndgameFlowPassAsync(rangeRoot, cancellationToken);
                ReportPass("endgame", endgamePass);

                await RestoreLoadoutAsync(_originalAssignments, originalSlot, cancellationToken);
                playerRoot.SetPositionAndRotation(originalPosition, originalRotation);

                if (logisticsPass && reconPass && recoveryPass && servicePass && powerPass && combatPass && constructionPass && endgamePass)
                    Debug.Log("[TrialRangeSmoke] PASS logistics=True recon=True recovery=True service=True power=True combat=True construction=True endgame=True");
                else
                    Debug.LogWarning($"[TrialRangeSmoke] FAIL logistics={logisticsPass} recon={reconPass} recovery={recoveryPass} service={servicePass} power={powerPass} combat={combatPass} construction={constructionPass} endgame={endgamePass}");
            }
            catch (OperationCanceledException)
            {
                LogVerbose("Cancelled.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TrialRangeSmoke] UNHANDLED EXCEPTION: {ex}");
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
                Debug.LogWarning("[TrialRangeSmoke] Logistics lane is missing key authored targets.");
                return false;
            }

            bool equipOk = await EquipToolAsync<PropulsionTool>(0, cancellationToken);
            if (!equipOk)
                return false;

            if (!(toolManager.CurrentTool is PropulsionTool propulsion))
                return false;

            PositionPlayerForTarget(cargoWork, 4.5f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ContainsAll(propulsion.GetOperationalSummary(), "WORK", "CARGO"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Propulsion summary did not resolve work cargo. Summary={propulsion.GetOperationalSummary()}");
                return false;
            }

            equipOk = await EquipToolAsync<HarpoonLauncherTool>(1, cancellationToken);
            if (!equipOk)
                return false;

            if (!(toolManager.CurrentTool is HarpoonLauncherTool harpoon))
                return false;

            PositionPlayerForTarget(cargoHeavy, 6f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ContainsAny(harpoon.GetOperationalSummary(), "HEAVY", "CARGO"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Harpoon summary did not resolve heavy cargo. Summary={harpoon.GetOperationalSummary()}");
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
            if (beaconNetwork == null || beaconNetwork.ActiveCount <= 0 || !ContainsAny(beaconTool.GetOperationalSummary(), "ANCHOR", "BEACON"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Beacon tool did not establish anchor semantics. Summary={beaconTool.GetOperationalSummary()}");
                return false;
            }

            PositionPlayerForTarget(routeRelay, 2.5f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ContainsAny(beaconTool.GetOperationalDirective(), "relay", "route", "readable"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Beacon directive did not resolve relay guidance. Directive={beaconTool.GetOperationalDirective()}");
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
                Debug.LogWarning("[TrialRangeSmoke] Recon lane is missing key authored targets.");
                return false;
            }

            bool equipOk = await EquipToolAsync<FlashlightTool>(0, cancellationToken);
            if (!equipOk)
                return false;

            if (!(toolManager.CurrentTool is FlashlightTool flashlight))
                return false;

            PositionPlayerForTarget(darkHazard, 10f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ContainsAny(flashlight.GetOperationalDirective(), "FOCUS", "frontier", "route"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Flashlight directive did not resolve hazard/frontier guidance. Directive={flashlight.GetOperationalDirective()}");
                return false;
            }

            PositionPlayerForTarget(darkPickup, 3.5f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ContainsAny(flashlight.GetOperationalDirective(), "FLOOD", "pickup", "salvage"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Flashlight directive did not resolve close salvage guidance. Directive={flashlight.GetOperationalDirective()}");
                return false;
            }

            equipOk = await EquipToolAsync<EnvironmentalAnalyzerTool>(1, cancellationToken);
            if (!equipOk)
                return false;

            if (!(toolManager.CurrentTool is EnvironmentalAnalyzerTool analyzer))
                return false;

            PositionPlayerForTarget(resourceProbe, 4.5f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ContainsAny(analyzer.GetOperationalSummary(), "RESOURCE", "CACHE"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Analyzer summary did not resolve resource semantics. Summary={analyzer.GetOperationalSummary()}");
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
            if (!ContainsAny(scanner.GetOperationalDirective(), "checkpoint", "contact", "deeper", "cargo"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Scanner directive did not resolve authored sweep semantics. Directive={scanner.GetOperationalDirective()}");
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
                Debug.LogWarning("[TrialRangeSmoke] Combat lane is missing key authored targets.");
                return false;
            }

            bool equipOk = await EquipToolAsync<EnvironmentalAnalyzerTool>(0, cancellationToken);
            if (!equipOk || !(toolManager.CurrentTool is EnvironmentalAnalyzerTool analyzer))
                return false;

            PositionPlayerForTarget(aggressive, 4.5f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ContainsAny(analyzer.GetOperationalSummary(), "AGGRESSIVE", "BIOFORM"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Analyzer combat summary mismatch. Summary={analyzer.GetOperationalSummary()}");
                return false;
            }

            equipOk = await EquipToolAsync<StunPistolTool>(1, cancellationToken);
            if (!equipOk || !(toolManager.CurrentTool is StunPistolTool stun))
                return false;

            PositionPlayerForTarget(dormant, 5f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ContainsAny(stun.GetOperationalDirective(), "wake", "quiet", "shot"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Stun directive mismatch on dormant target. Directive={stun.GetOperationalDirective()}");
                return false;
            }

            equipOk = await EquipToolAsync<KnifeTool>(2, cancellationToken);
            if (!equipOk || !(toolManager.CurrentTool is KnifeTool knife))
                return false;

            PositionPlayerForTarget(fractured, 2.8f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ContainsAny(knife.GetOperationalDirective(), "precision", "finish", "window"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Knife directive mismatch on fractured target. Directive={knife.GetOperationalDirective()}");
                return false;
            }

            equipOk = await EquipToolAsync<HarpoonLauncherTool>(3, cancellationToken);
            if (!equipOk || !(toolManager.CurrentTool is HarpoonLauncherTool harpoon))
                return false;

            PositionPlayerForTarget(aggressive, 5.5f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ContainsAny(harpoon.GetOperationalDirective(), "control", "spacing", "disengage"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Harpoon directive mismatch on aggressive target. Directive={harpoon.GetOperationalDirective()}");
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
                Debug.LogWarning("[TrialRangeSmoke] Recovery lane is missing key authored targets.");
                return false;
            }

            bool equipOk = await EquipToolAsync<SalvageSamplerTool>(0, cancellationToken);
            if (!equipOk || !(toolManager.CurrentTool is SalvageSamplerTool sampler))
                return false;

            PositionPlayerForTarget(salvagePickup, 2.8f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ContainsAny(sampler.GetOperationalSummary(), "RECOVERY READY", "PACKAGE", "RECOVERY"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Sampler summary did not resolve salvage pickup. Summary={sampler.GetOperationalSummary()}");
                return false;
            }

            PositionPlayerForTarget(depletedNode, 3.4f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ContainsAny(sampler.GetOperationalSummary(), "DEPLETED", "NODE"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Sampler summary did not resolve depleted node. Summary={sampler.GetOperationalSummary()}");
                return false;
            }

            equipOk = await EquipToolAsync<LaserCutter>(1, cancellationToken);
            if (!equipOk || !(toolManager.CurrentTool is LaserCutter cutter))
                return false;

            PositionPlayerForTarget(activeNode, 3.6f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ContainsAny(cutter.GetOperationalSummary(), "RESOURCE", "CONTACT", "NODE"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Cutter summary did not resolve active node. Summary={cutter.GetOperationalSummary()}");
                return false;
            }

            equipOk = await EquipToolAsync<KnifeTool>(2, cancellationToken);
            if (!equipOk || !(toolManager.CurrentTool is KnifeTool knife))
                return false;

            PositionPlayerForTarget(depletedNode, 2.8f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ContainsAny(knife.GetOperationalSummary(), "NODE", "DEPLETED"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Knife summary did not resolve depleted node. Summary={knife.GetOperationalSummary()}");
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
                Debug.LogWarning("[TrialRangeSmoke] Service lane is missing key authored targets.");
                return false;
            }

            bool equipOk = await EquipToolAsync<RepairTool>(0, cancellationToken);
            if (!equipOk || !(toolManager.CurrentTool is RepairTool repair))
                return false;

            PositionPlayerForTarget(damaged, 4.5f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ContainsAny(repair.GetOperationalSummary(), "SERVICE", "RESPONSE", "IMMEDIATE", "CRITICAL", "ACTIVE"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Repair summary did not resolve damaged module. Summary={repair.GetOperationalSummary()}");
                return false;
            }

            PositionPlayerForTarget(flooded, 4.8f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ContainsAny(repair.GetOperationalDirective(), "drain", "wait", "service", "power"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Repair directive did not resolve flooded module guidance. Directive={repair.GetOperationalDirective()}");
                return false;
            }

            equipOk = await EquipToolAsync<LaserCutter>(1, cancellationToken);
            if (!equipOk || !(toolManager.CurrentTool is LaserCutter cutter))
                return false;

            PositionPlayerForTarget(control, 4.8f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ContainsAny(cutter.GetOperationalSummary(), "MODULE", "LOCKED", "RECOVERY", "CONTACT"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Cutter summary did not resolve service module. Summary={cutter.GetOperationalSummary()}");
                return false;
            }

            equipOk = await EquipToolAsync<EnvironmentalAnalyzerTool>(2, cancellationToken);
            if (!equipOk || !(toolManager.CurrentTool is EnvironmentalAnalyzerTool analyzer))
                return false;

            PositionPlayerForTarget(flooded, 4.8f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ContainsAny(analyzer.GetOperationalSummary(), "FLOODED", "SERVICE", "MODULE"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Analyzer summary did not resolve flooded service semantics. Summary={analyzer.GetOperationalSummary()}");
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
                Debug.LogWarning("[TrialRangeSmoke] Power lane is missing key authored targets.");
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
            if (!ContainsAny(analyzer.GetOperationalSummary(), "POWER", "GENERATION", "CURRENT"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Analyzer summary did not resolve power generation semantics. Summary={analyzer.GetOperationalSummary()}");
                return false;
            }

            equipOk = await EquipToolAsync<FlashlightTool>(1, cancellationToken);
            if (!equipOk || !(toolManager.CurrentTool is FlashlightTool flashlight))
                return false;

            PositionPlayerForTarget(route, 8f);
            await DelayRealtimeAsync(settleDelay, cancellationToken);
            if (!ContainsAny(flashlight.GetOperationalDirective(), "FOCUS", "service", "power", "generator"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Flashlight directive did not resolve power/service guidance. Directive={flashlight.GetOperationalDirective()}");
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
                Debug.LogWarning("[TrialRangeSmoke] Construction lane is missing key authored targets.");
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

            string summary = builder.GetOperationalSummary();
            string directive = builder.GetOperationalDirective();
            if (!ContainsAny(summary, "READY", "BLOCKED", "MISSING", "MODULE", "SNAPPED", "NO MODULE"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Builder summary did not resolve an operational state. Summary={summary}");
                return false;
            }

            if (!ContainsAny(directive, "build", "place", "module", "resources", "snap", "deployment"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Builder directive did not resolve field guidance. Directive={directive}");
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
                Debug.LogWarning("[TrialRangeSmoke] Endgame lane is missing key authored targets.");
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
                Debug.LogWarning($"[TrialRangeSmoke] Missing prefab registration for {typeof(TTool).Name}.");
                return false;
            }

            if (!IsToolManagerHolstered())
            {
                toolManager.Holster();
                await WaitUntilAsync(
                    () => IsToolManagerHolstered(),
                    equipTimeout,
                    $"Holster before {typeof(TTool).Name}",
                    cancellationToken);
            }

            toolManager.SetAssignedToolPrefab(slotIndex, prefab, holsterIfCurrentInvalid: false);
            toolManager.SwitchToSlot(slotIndex);
            await WaitUntilAsync(
                () => !toolManager.IsSwapping && toolManager.CurrentTool is TTool,
                equipTimeout,
                $"Equip {typeof(TTool).Name}",
                cancellationToken);

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
            await WaitUntilAsync(
                () => IsToolManagerHolstered(),
                equipTimeout,
                "Holster restore",
                cancellationToken);

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
            Vector3 toTarget = target.position - playerRoot.position;
            Vector3 flatForward = new Vector3(toTarget.x, 0f, toTarget.z);
            if (flatForward.sqrMagnitude < 0.001f)
                flatForward = playerRoot.forward;

            Vector3 position = target.position - flatForward.normalized * distance;
            position.y = playerRoot.position.y;
            playerRoot.SetPositionAndRotation(position, Quaternion.LookRotation(flatForward.normalized, Vector3.up));
        }

        private bool VerifyRecommendedPreset(Transform target, float distance, string expectedPreset)
        {
            PositionPlayerForTarget(target, distance);

            if (!FieldLoadoutAdvisor.TryBuildForwardAdvice(playerRoot, 18f, Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask, out FieldLoadoutAdvisor.LoadoutAdvice advice))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Loadout advice did not resolve for {target.name}.");
                return false;
            }

            if (!string.Equals(advice.PresetName, expectedPreset, System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Loadout advice mismatch on {target.name}. Expected={expectedPreset} Actual={advice.PresetName} Summary={advice.Summary}");
                return false;
            }

            return true;
        }

        private static bool ContainsAny(string source, params string[] needles)
        {
            if (string.IsNullOrWhiteSpace(source))
                return false;

            string lowered = source.ToLowerInvariant();
            for (int i = 0; i < needles.Length; i++)
            {
                if (lowered.Contains(needles[i].ToLowerInvariant()))
                    return true;
            }

            return false;
        }

        private static bool ContainsAll(string source, params string[] needles)
        {
            if (string.IsNullOrWhiteSpace(source))
                return false;

            string lowered = source.ToLowerInvariant();
            for (int i = 0; i < needles.Length; i++)
            {
                if (!lowered.Contains(needles[i].ToLowerInvariant()))
                    return false;
            }

            return true;
        }

        private static async Awaitable DelayRealtimeAsync(float seconds, CancellationToken cancellationToken)
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0f, seconds);
            while (Time.realtimeSinceStartup < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Awaitable.NextFrameAsync(cancellationToken: cancellationToken);
            }
        }

        private static async Awaitable<bool> WaitUntilAsync(
            Func<bool> predicate,
            float timeout,
            string label,
            CancellationToken cancellationToken)
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0.05f, timeout);
            while (Time.realtimeSinceStartup < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool success = false;
                try
                {
                    success = predicate();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[TrialRangeSmoke] EXCEPTION {label}: {ex}");
                    return false;
                }

                if (success)
                    return true;

                await Awaitable.NextFrameAsync(cancellationToken: cancellationToken);
            }

            Debug.LogWarning($"[TrialRangeSmoke] TIMEOUT {label} after {timeout:0.00}s");
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
            if (beaconNetwork == null)
                beaconNetwork = FindSceneObjectIncludingInactive<BeaconNetworkSystem>();
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
                Debug.Log($"[TrialRangeSmoke] {message}");
        }

        private static void ReportPass(string label, bool result)
        {
            if (result)
                Debug.Log($"[TrialRangeSmoke] PASS {label}=True");
            else
                Debug.LogWarning($"[TrialRangeSmoke] FAIL {label}=False");
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
    }
}

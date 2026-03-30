using System.Collections;
using UnityEngine;

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

        private bool _isRunning;

        private void Awake()
        {
            AutoResolve();
        }

        private void Start()
        {
            if (!runOnStart || _isRunning)
                return;

            StartCoroutine(RunFullSuite());
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

            StartCoroutine(RunFullSuite());
        }

        private IEnumerator RunFullSuite()
        {
            if (_isRunning)
                yield break;

            AutoResolve();
            if (toolManager == null || playerRoot == null)
            {
                Debug.LogWarning($"[TrialRangeSmoke] Missing references tools={(toolManager != null ? "Y" : "N")} player={(playerRoot != null ? "Y" : "N")}");
                yield break;
            }

            Transform rangeRoot = FindSceneTransform("Tool_TrialRange");
            if (rangeRoot == null)
            {
                Debug.LogWarning("[TrialRangeSmoke] Tool_TrialRange root not found.");
                yield break;
            }

            _isRunning = true;
            if (startupDelay > 0f)
                yield return new WaitForSecondsRealtime(startupDelay);

            Vector3 originalPosition = playerRoot.position;
            Quaternion originalRotation = playerRoot.rotation;
            GameObject[] originalAssignments = SnapshotAssignments();
            int originalSlot = toolManager.CurrentSlotIndex;

            bool logisticsPass = false;
            bool reconPass = false;
            bool recoveryPass = false;
            bool servicePass = false;
            bool powerPass = false;
            bool combatPass = false;
            bool constructionPass = false;
            bool endgamePass = false;

            yield return RunLogisticsPass(rangeRoot, result => logisticsPass = result);
            ReportPass("logistics", logisticsPass);
            yield return RunReconPass(rangeRoot, result => reconPass = result);
            ReportPass("recon", reconPass);
            yield return RunRecoveryPass(rangeRoot, result => recoveryPass = result);
            ReportPass("recovery", recoveryPass);
            yield return RunServicePass(rangeRoot, result => servicePass = result);
            ReportPass("service", servicePass);
            yield return RunPowerPass(rangeRoot, result => powerPass = result);
            ReportPass("power", powerPass);
            yield return RunCombatPass(rangeRoot, result => combatPass = result);
            ReportPass("combat", combatPass);
            yield return RunConstructionPass(rangeRoot, result => constructionPass = result);
            ReportPass("construction", constructionPass);
            yield return RunEndgameFlowPass(rangeRoot, result => endgamePass = result);
            ReportPass("endgame", endgamePass);

            yield return RestoreLoadout(originalAssignments, originalSlot);
            playerRoot.SetPositionAndRotation(originalPosition, originalRotation);
            _isRunning = false;

            if (logisticsPass && reconPass && recoveryPass && servicePass && powerPass && combatPass && constructionPass && endgamePass)
                Debug.Log("[TrialRangeSmoke] PASS logistics=True recon=True recovery=True service=True power=True combat=True construction=True endgame=True");
            else
                Debug.LogWarning($"[TrialRangeSmoke] FAIL logistics={logisticsPass} recon={reconPass} recovery={recoveryPass} service={servicePass} power={powerPass} combat={combatPass} construction={constructionPass} endgame={endgamePass}");
        }

        private IEnumerator RunLogisticsPass(Transform rangeRoot, System.Action<bool> complete)
        {
            complete(false);

            Transform cargoWork = FindRelative(rangeRoot, "Lane_Cargo/Cargo_Work");
            Transform cargoHeavy = FindRelative(rangeRoot, "Lane_Cargo/Cargo_Heavy");
            Transform routeAnchor = FindRelative(rangeRoot, "Lane_BeaconRoute/Route_Anchor");
            Transform routeRelay = FindRelative(rangeRoot, "Lane_BeaconRoute/Route_Relay");
            if (cargoWork == null || cargoHeavy == null || routeAnchor == null || routeRelay == null)
            {
                Debug.LogWarning("[TrialRangeSmoke] Logistics lane is missing key authored targets.");
                yield break;
            }

            bool equipOk = false;
            yield return EquipTool<PropulsionTool>(0, result => equipOk = result);
            if (!equipOk)
                yield break;

            if (!(toolManager.CurrentTool is PropulsionTool propulsion))
                yield break;

            PositionPlayerForTarget(cargoWork, 4.5f);
            yield return new WaitForSecondsRealtime(settleDelay);
            if (!ContainsAll(propulsion.GetOperationalSummary(), "WORK", "CARGO"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Propulsion summary did not resolve work cargo. Summary={propulsion.GetOperationalSummary()}");
                yield break;
            }

            equipOk = false;
            yield return EquipTool<HarpoonLauncherTool>(1, result => equipOk = result);
            if (!equipOk)
                yield break;

            if (!(toolManager.CurrentTool is HarpoonLauncherTool harpoon))
                yield break;

            PositionPlayerForTarget(cargoHeavy, 6f);
            yield return new WaitForSecondsRealtime(settleDelay);
            if (!ContainsAny(harpoon.GetOperationalSummary(), "HEAVY", "CARGO"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Harpoon summary did not resolve heavy cargo. Summary={harpoon.GetOperationalSummary()}");
                yield break;
            }

            equipOk = false;
            yield return EquipTool<BeaconDeployerTool>(2, result => equipOk = result);
            if (!equipOk)
                yield break;

            if (!(toolManager.CurrentTool is BeaconDeployerTool beaconTool))
                yield break;

            PositionPlayerForTarget(routeAnchor, 2.5f);
            beaconTool.UsePrimary(0f);
            yield return new WaitForSecondsRealtime(settleDelay);
            if (beaconNetwork == null || beaconNetwork.ActiveCount <= 0 || !ContainsAny(beaconTool.GetOperationalSummary(), "ANCHOR", "BEACON"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Beacon tool did not establish anchor semantics. Summary={beaconTool.GetOperationalSummary()}");
                yield break;
            }

            PositionPlayerForTarget(routeRelay, 2.5f);
            yield return new WaitForSecondsRealtime(settleDelay);
            if (!ContainsAny(beaconTool.GetOperationalDirective(), "relay", "route", "readable"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Beacon directive did not resolve relay guidance. Directive={beaconTool.GetOperationalDirective()}");
                yield break;
            }

            complete(true);
            LogVerbose("Logistics pass complete.");
        }

        private IEnumerator RunReconPass(Transform rangeRoot, System.Action<bool> complete)
        {
            complete(false);

            Transform darkHazard = FindRelative(rangeRoot, "Lane_DarkRoute/DarkRoute_HazardProbe");
            Transform darkPickup = FindRelative(rangeRoot, "Lane_DarkRoute/DarkRoute_Salvage_Close");
            Transform expeditionProbe = FindRelative(rangeRoot, "Lane_ScanCorridor/Scan_Poi_ExpeditionContact");
            Transform resourceProbe = FindRelative(rangeRoot, "Lane_ScanCorridor/Scan_Poi_ResourceCache");
            if (darkHazard == null || darkPickup == null || expeditionProbe == null || resourceProbe == null)
            {
                Debug.LogWarning("[TrialRangeSmoke] Recon lane is missing key authored targets.");
                yield break;
            }

            bool equipOk = false;
            yield return EquipTool<FlashlightTool>(0, result => equipOk = result);
            if (!equipOk)
                yield break;

            if (!(toolManager.CurrentTool is FlashlightTool flashlight))
                yield break;

            PositionPlayerForTarget(darkHazard, 10f);
            yield return new WaitForSecondsRealtime(settleDelay);
            if (!ContainsAny(flashlight.GetOperationalDirective(), "FOCUS", "frontier", "route"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Flashlight directive did not resolve hazard/frontier guidance. Directive={flashlight.GetOperationalDirective()}");
                yield break;
            }

            PositionPlayerForTarget(darkPickup, 3.5f);
            yield return new WaitForSecondsRealtime(settleDelay);
            if (!ContainsAny(flashlight.GetOperationalDirective(), "FLOOD", "pickup", "salvage"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Flashlight directive did not resolve close salvage guidance. Directive={flashlight.GetOperationalDirective()}");
                yield break;
            }

            equipOk = false;
            yield return EquipTool<EnvironmentalAnalyzerTool>(1, result => equipOk = result);
            if (!equipOk)
                yield break;

            if (!(toolManager.CurrentTool is EnvironmentalAnalyzerTool analyzer))
                yield break;

            PositionPlayerForTarget(resourceProbe, 4.5f);
            yield return new WaitForSecondsRealtime(settleDelay);
            if (!ContainsAny(analyzer.GetOperationalSummary(), "RESOURCE", "CACHE"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Analyzer summary did not resolve resource semantics. Summary={analyzer.GetOperationalSummary()}");
                yield break;
            }

            equipOk = false;
            yield return EquipTool<ScannerTool>(2, result => equipOk = result);
            if (!equipOk)
                yield break;

            if (!(toolManager.CurrentTool is ScannerTool scanner))
                yield break;

            PositionPlayerForTarget(expeditionProbe, 5f);
            scanner.UsePrimary(0f);
            yield return new WaitForSecondsRealtime(settleDelay);
            if (!ContainsAny(scanner.GetOperationalDirective(), "checkpoint", "contact", "deeper", "cargo"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Scanner directive did not resolve authored sweep semantics. Directive={scanner.GetOperationalDirective()}");
                yield break;
            }

            complete(true);
            LogVerbose("Recon pass complete.");
        }

        private IEnumerator RunCombatPass(Transform rangeRoot, System.Action<bool> complete)
        {
            complete(false);

            Transform dormant = FindRelative(rangeRoot, "Lane_CombatContacts/Combat_Dormant");
            Transform aggressive = FindRelative(rangeRoot, "Lane_CombatContacts/Combat_Aggressive");
            Transform fractured = FindRelative(rangeRoot, "Lane_CombatContacts/Combat_Fractured");
            Transform down = FindRelative(rangeRoot, "Lane_CombatContacts/Combat_Down");
            if (dormant == null || aggressive == null || fractured == null || down == null)
            {
                Debug.LogWarning("[TrialRangeSmoke] Combat lane is missing key authored targets.");
                yield break;
            }

            bool equipOk = false;
            yield return EquipTool<EnvironmentalAnalyzerTool>(0, result => equipOk = result);
            if (!equipOk || !(toolManager.CurrentTool is EnvironmentalAnalyzerTool analyzer))
                yield break;

            PositionPlayerForTarget(aggressive, 4.5f);
            yield return new WaitForSecondsRealtime(settleDelay);
            if (!ContainsAny(analyzer.GetOperationalSummary(), "AGGRESSIVE", "BIOFORM"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Analyzer combat summary mismatch. Summary={analyzer.GetOperationalSummary()}");
                yield break;
            }

            equipOk = false;
            yield return EquipTool<StunPistolTool>(1, result => equipOk = result);
            if (!equipOk || !(toolManager.CurrentTool is StunPistolTool stun))
                yield break;

            PositionPlayerForTarget(dormant, 5f);
            yield return new WaitForSecondsRealtime(settleDelay);
            if (!ContainsAny(stun.GetOperationalDirective(), "wake", "quiet", "shot"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Stun directive mismatch on dormant target. Directive={stun.GetOperationalDirective()}");
                yield break;
            }

            equipOk = false;
            yield return EquipTool<KnifeTool>(2, result => equipOk = result);
            if (!equipOk || !(toolManager.CurrentTool is KnifeTool knife))
                yield break;

            PositionPlayerForTarget(fractured, 2.8f);
            yield return new WaitForSecondsRealtime(settleDelay);
            if (!ContainsAny(knife.GetOperationalDirective(), "precision", "finish", "window"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Knife directive mismatch on fractured target. Directive={knife.GetOperationalDirective()}");
                yield break;
            }

            equipOk = false;
            yield return EquipTool<HarpoonLauncherTool>(3, result => equipOk = result);
            if (!equipOk || !(toolManager.CurrentTool is HarpoonLauncherTool harpoon))
                yield break;

            PositionPlayerForTarget(aggressive, 5.5f);
            yield return new WaitForSecondsRealtime(settleDelay);
            if (!ContainsAny(harpoon.GetOperationalDirective(), "control", "spacing", "disengage"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Harpoon directive mismatch on aggressive target. Directive={harpoon.GetOperationalDirective()}");
                yield break;
            }

            complete(true);
            LogVerbose("Combat pass complete.");
        }

        private IEnumerator RunRecoveryPass(Transform rangeRoot, System.Action<bool> complete)
        {
            complete(false);

            Transform salvagePickup = FindRelative(rangeRoot, "Lane_Salvage/Trial_Salvage_A");
            Transform activeNode = FindRelative(rangeRoot, "Lane_Salvage/Trial_Node_Active");
            Transform depletedNode = FindRelative(rangeRoot, "Lane_Salvage/Trial_Node_Depleted");
            if (salvagePickup == null || activeNode == null || depletedNode == null)
            {
                Debug.LogWarning("[TrialRangeSmoke] Recovery lane is missing key authored targets.");
                yield break;
            }

            bool equipOk = false;
            yield return EquipTool<SalvageSamplerTool>(0, result => equipOk = result);
            if (!equipOk || !(toolManager.CurrentTool is SalvageSamplerTool sampler))
                yield break;

            PositionPlayerForTarget(salvagePickup, 2.8f);
            yield return new WaitForSecondsRealtime(settleDelay);
            if (!ContainsAny(sampler.GetOperationalSummary(), "RECOVERY READY", "PACKAGE", "RECOVERY"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Sampler summary did not resolve salvage pickup. Summary={sampler.GetOperationalSummary()}");
                yield break;
            }

            PositionPlayerForTarget(depletedNode, 3.4f);
            yield return new WaitForSecondsRealtime(settleDelay);
            if (!ContainsAny(sampler.GetOperationalSummary(), "DEPLETED", "NODE"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Sampler summary did not resolve depleted node. Summary={sampler.GetOperationalSummary()}");
                yield break;
            }

            equipOk = false;
            yield return EquipTool<LaserCutter>(1, result => equipOk = result);
            if (!equipOk || !(toolManager.CurrentTool is LaserCutter cutter))
                yield break;

            PositionPlayerForTarget(activeNode, 3.6f);
            yield return new WaitForSecondsRealtime(settleDelay);
            if (!ContainsAny(cutter.GetOperationalSummary(), "RESOURCE", "CONTACT", "NODE"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Cutter summary did not resolve active node. Summary={cutter.GetOperationalSummary()}");
                yield break;
            }

            equipOk = false;
            yield return EquipTool<KnifeTool>(2, result => equipOk = result);
            if (!equipOk || !(toolManager.CurrentTool is KnifeTool knife))
                yield break;

            PositionPlayerForTarget(depletedNode, 2.8f);
            yield return new WaitForSecondsRealtime(settleDelay);
            if (!ContainsAny(knife.GetOperationalSummary(), "NODE", "DEPLETED"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Knife summary did not resolve depleted node. Summary={knife.GetOperationalSummary()}");
                yield break;
            }

            complete(true);
            LogVerbose("Recovery pass complete.");
        }

        private IEnumerator RunServicePass(Transform rangeRoot, System.Action<bool> complete)
        {
            complete(false);

            Transform damaged = FindRelative(rangeRoot, "Lane_ServiceModules/Trial_Module_Foundation_Damaged");
            Transform flooded = FindRelative(rangeRoot, "Lane_ServiceModules/Trial_Module_Corridor_Flooded");
            Transform control = FindRelative(rangeRoot, "Lane_ServiceModules/Trial_Module_Foundation_Control");
            if (damaged == null || flooded == null || control == null)
            {
                Debug.LogWarning("[TrialRangeSmoke] Service lane is missing key authored targets.");
                yield break;
            }

            bool equipOk = false;
            yield return EquipTool<RepairTool>(0, result => equipOk = result);
            if (!equipOk || !(toolManager.CurrentTool is RepairTool repair))
                yield break;

            PositionPlayerForTarget(damaged, 4.5f);
            yield return new WaitForSecondsRealtime(settleDelay);
            if (!ContainsAny(repair.GetOperationalSummary(), "SERVICE", "RESPONSE", "IMMEDIATE", "CRITICAL", "ACTIVE"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Repair summary did not resolve damaged module. Summary={repair.GetOperationalSummary()}");
                yield break;
            }

            PositionPlayerForTarget(flooded, 4.8f);
            yield return new WaitForSecondsRealtime(settleDelay);
            if (!ContainsAny(repair.GetOperationalDirective(), "drain", "wait", "service", "power"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Repair directive did not resolve flooded module guidance. Directive={repair.GetOperationalDirective()}");
                yield break;
            }

            equipOk = false;
            yield return EquipTool<LaserCutter>(1, result => equipOk = result);
            if (!equipOk || !(toolManager.CurrentTool is LaserCutter cutter))
                yield break;

            PositionPlayerForTarget(control, 4.8f);
            yield return new WaitForSecondsRealtime(settleDelay);
            if (!ContainsAny(cutter.GetOperationalSummary(), "MODULE", "LOCKED", "RECOVERY", "CONTACT"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Cutter summary did not resolve service module. Summary={cutter.GetOperationalSummary()}");
                yield break;
            }

            equipOk = false;
            yield return EquipTool<EnvironmentalAnalyzerTool>(2, result => equipOk = result);
            if (!equipOk || !(toolManager.CurrentTool is EnvironmentalAnalyzerTool analyzer))
                yield break;

            PositionPlayerForTarget(flooded, 4.8f);
            yield return new WaitForSecondsRealtime(settleDelay);
            if (!ContainsAny(analyzer.GetOperationalSummary(), "FLOODED", "SERVICE", "MODULE"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Analyzer summary did not resolve flooded service semantics. Summary={analyzer.GetOperationalSummary()}");
                yield break;
            }

            complete(true);
            LogVerbose("Service pass complete.");
        }

        private IEnumerator RunPowerPass(Transform rangeRoot, System.Action<bool> complete)
        {
            complete(false);

            Transform turbine = FindRelative(rangeRoot, "Lane_PowerOps/Power_CurrentTurbine");
            Transform relay = FindRelative(rangeRoot, "Lane_PowerOps/Power_RelayPylon");
            Transform pump = FindRelative(rangeRoot, "Lane_PowerOps/Power_ServicePump");
            Transform route = FindRelative(rangeRoot, "Lane_PowerOps/Power_ServiceRoute");
            if (turbine == null || relay == null || pump == null || route == null)
            {
                Debug.LogWarning("[TrialRangeSmoke] Power lane is missing key authored targets.");
                yield break;
            }

            if (!VerifyRecommendedPreset(turbine, 5f, "CONSTRUCTION"))
                yield break;

            yield return new WaitForSecondsRealtime(settleDelay);

            if (!VerifyRecommendedPreset(relay, 5f, "CONSTRUCTION"))
                yield break;

            yield return new WaitForSecondsRealtime(settleDelay);

            if (!VerifyRecommendedPreset(pump, 5f, "CONSTRUCTION"))
                yield break;

            bool equipOk = false;
            yield return EquipTool<EnvironmentalAnalyzerTool>(0, result => equipOk = result);
            if (!equipOk || !(toolManager.CurrentTool is EnvironmentalAnalyzerTool analyzer))
                yield break;

            PositionPlayerForTarget(turbine, 4.8f);
            yield return new WaitForSecondsRealtime(settleDelay);
            if (!ContainsAny(analyzer.GetOperationalSummary(), "POWER", "GENERATION", "CURRENT"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Analyzer summary did not resolve power generation semantics. Summary={analyzer.GetOperationalSummary()}");
                yield break;
            }

            equipOk = false;
            yield return EquipTool<FlashlightTool>(1, result => equipOk = result);
            if (!equipOk || !(toolManager.CurrentTool is FlashlightTool flashlight))
                yield break;

            PositionPlayerForTarget(route, 8f);
            yield return new WaitForSecondsRealtime(settleDelay);
            if (!ContainsAny(flashlight.GetOperationalDirective(), "FOCUS", "service", "power", "generator"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Flashlight directive did not resolve power/service guidance. Directive={flashlight.GetOperationalDirective()}");
                yield break;
            }

            complete(true);
            LogVerbose("Power pass complete.");
        }

        private IEnumerator RunConstructionPass(Transform rangeRoot, System.Action<bool> complete)
        {
            complete(false);

            Transform clearLane = FindRelative(rangeRoot, "Lane_ConstructionOps/Construct_ClearLane");
            Transform blockedLane = FindRelative(rangeRoot, "Lane_ConstructionOps/Construct_Blocker");
            Transform socketGuide = FindRelative(rangeRoot, "Lane_ConstructionOps/Construct_SocketGuide");
            if (clearLane == null || blockedLane == null || socketGuide == null)
            {
                Debug.LogWarning("[TrialRangeSmoke] Construction lane is missing key authored targets.");
                yield break;
            }

            if (!VerifyRecommendedPreset(clearLane, 5f, "CONSTRUCTION"))
                yield break;

            yield return new WaitForSecondsRealtime(settleDelay);

            if (!VerifyRecommendedPreset(blockedLane, 6f, "CONSTRUCTION"))
                yield break;

            yield return new WaitForSecondsRealtime(settleDelay);

            if (!VerifyRecommendedPreset(socketGuide, 4f, "CONSTRUCTION"))
                yield break;

            bool equipOk = false;
            yield return EquipTool<BuilderTool>(3, result => equipOk = result);
            if (!equipOk || !(toolManager.CurrentTool is BuilderTool builder))
                yield break;

            yield return new WaitForSecondsRealtime(settleDelay);

            string summary = builder.GetOperationalSummary();
            string directive = builder.GetOperationalDirective();
            if (!ContainsAny(summary, "READY", "BLOCKED", "MISSING", "MODULE", "SNAPPED", "NO MODULE"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Builder summary did not resolve an operational state. Summary={summary}");
                yield break;
            }

            if (!ContainsAny(directive, "build", "place", "module", "resources", "snap", "deployment"))
            {
                Debug.LogWarning($"[TrialRangeSmoke] Builder directive did not resolve field guidance. Directive={directive}");
                yield break;
            }

            complete(true);
            LogVerbose("Construction pass complete.");
        }

        private IEnumerator RunEndgameFlowPass(Transform rangeRoot, System.Action<bool> complete)
        {
            complete(false);

            Transform cargo = FindRelative(rangeRoot, "Lane_EndgameOps/Ops_Cargo_Work");
            Transform salvage = FindRelative(rangeRoot, "Lane_EndgameOps/Ops_Salvage");
            Transform service = FindRelative(rangeRoot, "Lane_EndgameOps/Ops_Service_Flooded");
            Transform hazard = FindRelative(rangeRoot, "Lane_EndgameOps/Ops_Hazard");
            Transform combat = FindRelative(rangeRoot, "Lane_EndgameOps/Ops_Combat_Aggressive");
            Transform frontier = FindRelative(rangeRoot, "Lane_EndgameOps/Ops_Frontier");
            if (cargo == null || salvage == null || service == null || hazard == null || combat == null || frontier == null)
            {
                Debug.LogWarning("[TrialRangeSmoke] Endgame lane is missing key authored targets.");
                yield break;
            }

            if (!VerifyRecommendedPreset(cargo, 5f, "FIELD RECOVERY"))
                yield break;

            yield return new WaitForSecondsRealtime(settleDelay);

            if (!VerifyRecommendedPreset(salvage, 3f, "FIELD RECOVERY"))
                yield break;

            yield return new WaitForSecondsRealtime(settleDelay);

            if (!VerifyRecommendedPreset(service, 5f, "CONSTRUCTION"))
                yield break;

            yield return new WaitForSecondsRealtime(settleDelay);

            if (!VerifyRecommendedPreset(hazard, 6f, "EXPLORATION"))
                yield break;

            yield return new WaitForSecondsRealtime(settleDelay);

            if (!VerifyRecommendedPreset(combat, 5f, "DEFENSE"))
                yield break;

            yield return new WaitForSecondsRealtime(settleDelay);

            if (!VerifyRecommendedPreset(frontier, 4f, "EXPLORATION"))
                yield break;

            complete(true);
            LogVerbose("Endgame flow pass complete.");
        }

        private IEnumerator EquipTool<TTool>(int slotIndex, System.Action<bool> complete) where TTool : PlayerTool
        {
            complete(false);
            GameObject prefab = toolManager.GetKnownToolPrefabForToolType<TTool>();
            if (prefab == null)
            {
                Debug.LogWarning($"[TrialRangeSmoke] Missing prefab registration for {typeof(TTool).Name}.");
                yield break;
            }

            if (!IsToolManagerHolstered())
            {
                toolManager.Holster();
                yield return WaitUntil(() => IsToolManagerHolstered(), equipTimeout, $"Holster before {typeof(TTool).Name}");
            }

            toolManager.SetAssignedToolPrefab(slotIndex, prefab, holsterIfCurrentInvalid: false);
            toolManager.SwitchToSlot(slotIndex);
            yield return WaitUntil(
                () => !toolManager.IsSwapping && toolManager.CurrentTool is TTool,
                equipTimeout,
                $"Equip {typeof(TTool).Name}");

            complete(toolManager != null && !toolManager.IsSwapping && toolManager.CurrentTool is TTool);
        }

        private IEnumerator RestoreLoadout(GameObject[] originalAssignments, int originalSlot)
        {
            if (toolManager == null)
                yield break;

            toolManager.Holster();
            yield return WaitUntil(() => IsToolManagerHolstered(), equipTimeout, "Holster restore");

            if (originalAssignments != null)
            {
                for (int i = 0; i < originalAssignments.Length; i++)
                    toolManager.SetAssignedToolPrefab(i, originalAssignments[i], holsterIfCurrentInvalid: false);
            }

            if (originalSlot >= 0)
                toolManager.SwitchToSlot(originalSlot);
        }

        private GameObject[] SnapshotAssignments()
        {
            GameObject[] snapshot = new GameObject[4];
            for (int i = 0; i < snapshot.Length; i++)
                snapshot[i] = toolManager.GetAssignedToolPrefab(i);
            return snapshot;
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

            if (!FieldLoadoutAdvisor.TryBuildForwardAdvice(playerRoot, 18f, ~0, out FieldLoadoutAdvisor.LoadoutAdvice advice))
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

        private IEnumerator WaitUntil(System.Func<bool> predicate, float timeout, string label)
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0.05f, timeout);
            while (Time.realtimeSinceStartup < deadline)
            {
                bool success = false;
                try
                {
                    success = predicate();
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[TrialRangeSmoke] EXCEPTION {label}: {ex}");
                    yield break;
                }

                if (success)
                    yield break;

                yield return null;
            }

            Debug.LogWarning($"[TrialRangeSmoke] TIMEOUT {label} after {timeout:0.00}s");
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
            GameObject target = GameObject.Find(name);
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

        private static T FindSceneObjectIncludingInactive<T>() where T : Object
        {
            T[] candidates = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < candidates.Length; i++)
            {
                T candidate = candidates[i];
                if (candidate == null)
                    continue;

                if (candidate is Component component)
                {
                    if (!component.gameObject.scene.IsValid())
                        continue;
                    return candidate;
                }

                if (candidate is GameObject gameObject)
                {
                    if (!gameObject.scene.IsValid())
                        continue;
                    return candidate;
                }
            }

            return null;
        }
    }
}

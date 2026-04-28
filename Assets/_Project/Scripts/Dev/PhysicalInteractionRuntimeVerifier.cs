// ============================================================================
// HECTON-8 - PhysicalInteractionRuntimeVerifier.cs
// Deterministic dev-side verifier for PhysicalInteractionHandler pickup pull
// and heavy drag behavior. Creates temporary probes, runs the handler directly,
// logs measured results, then destroys the probes.
// ============================================================================

using Hecton8.Gameplay;
using Hecton8.Interaction;
using Hecton8.Inventory;
using Hecton8.Items;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityPhysics = global::UnityEngine.Physics;
using UnitySimulationMode = global::UnityEngine.SimulationMode;

namespace Hecton8.Dev
{
    /// <summary>
    /// Runtime verifier for physical pull-to-hand pickups and heavy rigidbody dragging.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Physical Interaction Runtime Verifier")]
    public sealed class PhysicalInteractionRuntimeVerifier : MonoBehaviour
    {
        private const string ProbeItemPath = "Assets/_Project/Data/Items/Data_Titanium.asset";
        private const int MaxSimulationStepsPerRun = 32;

        [Header("References")]
        [SerializeField] private PhysicalInteractionHandler interactionHandler;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private HectonSurvivalSystem survivalSystem;
        [SerializeField] private Camera playerCamera;

        [Header("Assets")]
        [SerializeField] private ItemData pickupProbeItem;

        [Header("Execution")]
        [SerializeField] private bool runOnStart = false;
        [SerializeField] private float simulatedDeltaTime = 0.05f;
        [SerializeField] private int pickupSimulationSteps = 12;
        [SerializeField] private int heavySimulationSteps = 20;
        [SerializeField] private float pickupProbeDistance = 2f;
        [SerializeField] private float heavyProbeDistance = 4f;
        [SerializeField] private float verticalOffset = -0.8f;
        [SerializeField] private float heavyProbeMass = 60f;
        [SerializeField] private float minimumHeavyMoveDistance = 0.75f;
        [SerializeField] private float minimumEnergyDrain = 0.01f;

        [Header("Diagnostics")]
        [SerializeField] private bool _debugRunning;
        [SerializeField] private bool _debugPickupPass;
        [SerializeField] private bool _debugHeavyPass;
        [SerializeField] private float _debugPickupInventoryBefore;
        [SerializeField] private float _debugPickupInventoryAfter;
        [SerializeField] private float _debugHeavyDistanceBefore;
        [SerializeField] private float _debugHeavyDistanceAfter;
        [SerializeField] private float _debugHeavyEnergyBefore;
        [SerializeField] private float _debugHeavyEnergyAfter;
        [SerializeField] private string _debugLastIssue = string.Empty;

        private void Awake()
        {
            AutoResolveReferences();
#if UNITY_EDITOR
            AutoResolveAssets();
#endif
        }

        private void Start()
        {
#if !(UNITY_EDITOR || DEVELOPMENT_BUILD)
            return;
#endif

            if (!runOnStart || _debugRunning)
                return;

            RunVerificationImmediate();
        }

        /// <summary>
        /// Executes the verification pass immediately from the inspector context menu.
        /// </summary>
        [ContextMenu("Run Verification")]
        public void RunFromContextMenu()
        {
            if (_debugRunning)
                return;

            RunVerificationImmediate();
        }

        private void RunVerificationImmediate()
        {
            AutoResolveReferences();
            if (!ValidateSetup())
                return;

            _debugRunning = true;
            _debugPickupPass = false;
            _debugHeavyPass = false;
            _debugPickupInventoryBefore = 0f;
            _debugPickupInventoryAfter = 0f;
            _debugHeavyDistanceBefore = 0f;
            _debugHeavyDistanceAfter = 0f;
            _debugHeavyEnergyBefore = 0f;
            _debugHeavyEnergyAfter = 0f;
            _debugLastIssue = string.Empty;

            GameObject pickupProbe = null;
            GameObject heavyProbe = null;
            UnitySimulationMode originalSimulationMode = UnityPhysics.simulationMode;

            try
            {
                UnityPhysics.simulationMode = UnitySimulationMode.Script;

                pickupProbe = CreatePickupProbe();
                if (pickupProbe == null)
                {
                    FinishWithFailure("Failed to create pickup probe.");
                    return;
                }

                _debugPickupPass = VerifyPickupProbe(pickupProbe);
                if (!_debugPickupPass)
                    return;

                heavyProbe = CreateHeavyProbe();
                if (heavyProbe == null)
                {
                    FinishWithFailure("Failed to create heavy probe.");
                    return;
                }

                _debugHeavyPass = VerifyHeavyProbe(heavyProbe);
                if (!_debugHeavyPass)
                    return;

                _debugLastIssue = string.Empty;
                Debug.Log("[PhysicalVerify] PASS pickup=True heavy=True");
            }
            finally
            {
                interactionHandler.CancelActiveInteraction();
                UnityPhysics.simulationMode = originalSimulationMode;

                if (pickupProbe != null)
                    Destroy(pickupProbe);

                if (heavyProbe != null)
                    Destroy(heavyProbe);

                _debugRunning = false;
            }
        }

        private void AutoResolveReferences()
        {
            Hecton8.Core.IPlayerRuntimeContext playerContext = Hecton8.Core.GlobalRegistry.Player;
            if (interactionHandler == null)
            {
                interactionHandler = GetComponent<PhysicalInteractionHandler>();
                if (interactionHandler == null && playerContext != null && playerContext.PlayerObject != null)
                    playerContext.PlayerObject.TryGetComponent(out interactionHandler);
            }

            if (playerInventory == null)
                playerInventory = GetComponent<PlayerInventory>() ?? (Hecton8.Core.GlobalRegistry.Player != null ? Hecton8.Core.GlobalRegistry.Player.Inventory : null);

            if (survivalSystem == null)
            {
                survivalSystem = GetComponent<HectonSurvivalSystem>();
                if (survivalSystem == null && playerContext != null && playerContext.PlayerObject != null)
                    playerContext.PlayerObject.TryGetComponent(out survivalSystem);
            }

            if (playerCamera == null && interactionHandler != null)
                playerCamera = ((Hecton8.Core.GlobalRegistry.Player != null && Hecton8.Core.GlobalRegistry.Player.PlayerCamera != null) ? Hecton8.Core.GlobalRegistry.Player.PlayerCamera : interactionHandler.GetComponent<Camera>());

            if (pickupProbeItem == null)
            {
                PickupItem scenePickup = PickupItem.ActiveRuntimeInstance;
                if (scenePickup != null)
                    pickupProbeItem = scenePickup.ItemData;
            }
        }

        private bool ValidateSetup()
        {
            if (!Application.isPlaying)
            {
                FinishWithFailure("Physical interaction verifier requires play mode.");
                return false;
            }

            if (interactionHandler != null &&
                playerInventory != null &&
                playerCamera != null &&
                pickupProbeItem != null)
            {
                return true;
            }

            FinishWithFailure(
                $"Missing verifier references or pickup probe item handler={(interactionHandler != null)} " +
                $"inventory={(playerInventory != null)} survival={(survivalSystem != null)} " +
                $"camera={(playerCamera != null)} item={(pickupProbeItem != null)}");
            return false;
        }

        private bool VerifyPickupProbe(GameObject pickupProbe)
        {
            PickupItem pickupItem = pickupProbe.GetComponent<PickupItem>();
            if (pickupItem == null)
            {
                FinishWithFailure("Pickup probe is missing PickupItem.");
                return false;
            }

            _debugPickupInventoryBefore = playerInventory.CountTotal(pickupProbeItem);

            bool consumed = interactionHandler.TryHandleInteraction(pickupItem, interactionHandler.transform);
            if (!consumed)
            {
                FinishWithFailure("PhysicalInteractionHandler rejected pickup probe.");
                return false;
            }

            SimulateHandlerSteps(pickupSimulationSteps);
            _debugPickupInventoryAfter = playerInventory.CountTotal(pickupProbeItem);

            bool probeGone = !pickupProbe.activeSelf;
            bool inventoryIncreased = _debugPickupInventoryAfter > _debugPickupInventoryBefore;

            Debug.Log(
                $"[PhysicalVerify] Pickup consumed={consumed} inventoryBefore={_debugPickupInventoryBefore:F0} " +
                $"inventoryAfter={_debugPickupInventoryAfter:F0} probeActive={pickupProbe.activeSelf}");

            if (!probeGone || !inventoryIncreased)
            {
                FinishWithFailure("Pickup probe did not reach inventory completion state.");
                return false;
            }

            return true;
        }

        private bool VerifyHeavyProbe(GameObject heavyProbe)
        {
            HeavyCarryInteractable heavyCarry = heavyProbe.GetComponent<HeavyCarryInteractable>();
            Rigidbody heavyBody = heavyProbe.GetComponent<Rigidbody>();
            if (heavyCarry == null || heavyBody == null)
            {
                FinishWithFailure("Heavy probe is missing HeavyCarryInteractable or Rigidbody.");
                return false;
            }

            _debugHeavyDistanceBefore = Vector3.Distance(interactionHandler.transform.position, heavyBody.position);
            _debugHeavyEnergyBefore = survivalSystem != null ? survivalSystem.Energy : 0f;

            bool consumed = interactionHandler.TryHandleInteraction(heavyCarry, interactionHandler.transform);
            if (!consumed)
            {
                FinishWithFailure("PhysicalInteractionHandler rejected heavy probe.");
                return false;
            }

            SimulateHandlerSteps(heavySimulationSteps);

            _debugHeavyDistanceAfter = Vector3.Distance(interactionHandler.transform.position, heavyBody.position);
            _debugHeavyEnergyAfter = survivalSystem != null ? survivalSystem.Energy : 0f;

            float movedDistance = _debugHeavyDistanceBefore - _debugHeavyDistanceAfter;
            float drainedEnergy = _debugHeavyEnergyBefore - _debugHeavyEnergyAfter;

            Debug.Log(
                $"[PhysicalVerify] Heavy consumed={consumed} distanceBefore={_debugHeavyDistanceBefore:F3} " +
                $"distanceAfter={_debugHeavyDistanceAfter:F3} moved={movedDistance:F3} " +
                $"energyBefore={_debugHeavyEnergyBefore:F3} energyAfter={_debugHeavyEnergyAfter:F3} " +
                $"drained={drainedEnergy:F3}");

            if (movedDistance < minimumHeavyMoveDistance)
            {
                FinishWithFailure("Heavy probe did not move enough toward the player.");
                return false;
            }

            if (survivalSystem != null && drainedEnergy < minimumEnergyDrain)
            {
                FinishWithFailure("Heavy probe did not drain suit energy.");
                return false;
            }

            return true;
        }

        private void SimulateHandlerSteps(int stepCount)
        {
            int safeStepCount = Mathf.Clamp(stepCount, 0, MaxSimulationStepsPerRun);
            if (safeStepCount <= 0)
                return;

            float stepDelta = Mathf.Max(0.01f, simulatedDeltaTime);
            for (int i = 0; i < safeStepCount; i++)
            {
                interactionHandler.Tick(stepDelta);
                interactionHandler.FixedTick(stepDelta);
                UnityPhysics.Simulate(stepDelta);
            }
        }

        private GameObject CreatePickupProbe()
        {
            // COLD ALLOC: primitive pickup probe for one-shot runtime verification — owner: PhysicalInteractionRuntimeVerifier
            GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            probe.name = "VERIFY_PHYSICAL_PICKUP";
            probe.transform.SetPositionAndRotation(ResolveProbePosition(pickupProbeDistance), Quaternion.identity);
            probe.transform.localScale = new Vector3(0.35f, 0.2f, 0.35f);

            probe.AddComponent<InteractionHighlighter>().SetHighlight(false);

            Rigidbody body = probe.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.mass = 2f;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;

            PickupItem pickupItem = probe.AddComponent<PickupItem>();
            pickupItem.Configure(pickupProbeItem, 1);
            return probe;
        }

        private GameObject CreateHeavyProbe()
        {
            // COLD ALLOC: primitive heavy-carry probe for one-shot runtime verification — owner: PhysicalInteractionRuntimeVerifier
            GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            probe.name = "VERIFY_HEAVY_CARRY";
            probe.transform.SetPositionAndRotation(ResolveProbePosition(heavyProbeDistance), Quaternion.identity);
            probe.transform.localScale = new Vector3(1.15f, 0.9f, 1.15f);

            probe.AddComponent<InteractionHighlighter>().SetHighlight(false);

            Rigidbody body = probe.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.mass = heavyProbeMass;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;

            probe.AddComponent<HeavyCarryInteractable>();
            return probe;
        }

        private Vector3 ResolveProbePosition(float forwardDistance)
        {
            Vector3 forward = Vector3.ProjectOnPlane(playerCamera.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
                forward = interactionHandler.transform.forward;

            forward.Normalize();
            return interactionHandler.transform.position +
                   forward * forwardDistance +
                   Vector3.up * verticalOffset;
        }

        private void FinishWithFailure(string issue)
        {
            _debugLastIssue = issue;
            _debugRunning = false;
            Debug.LogWarning($"[PhysicalVerify] {issue}");
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (EditorApplication.isCompiling ||
                EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            AutoResolveReferences();
            AutoResolveAssets();
            simulatedDeltaTime = Mathf.Clamp(simulatedDeltaTime, 0.01f, 0.25f);
            pickupSimulationSteps = Mathf.Clamp(pickupSimulationSteps, 1, MaxSimulationStepsPerRun);
            heavySimulationSteps = Mathf.Clamp(heavySimulationSteps, 1, MaxSimulationStepsPerRun);
        }

        private void AutoResolveAssets()
        {
            if (pickupProbeItem == null)
                pickupProbeItem = AssetDatabase.LoadAssetAtPath<ItemData>(ProbeItemPath);
        }
#endif
    }
}

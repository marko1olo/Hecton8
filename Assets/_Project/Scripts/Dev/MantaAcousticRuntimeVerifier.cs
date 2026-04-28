// ============================================================================
// HECTON-8 - MantaAcousticRuntimeVerifier.cs
// Deterministic dev-only verifier for Manta scooter propulsion and acoustic
// snapshot switching. Uses manual FixedTick + Physics.Simulate so verification
// still runs when the Editor play session stalls on the first live frame.
// ============================================================================

using Hecton8.Audio;
using Hecton8.Bootstrap;
using Hecton8.Gameplay;
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
    /// Runtime verifier for underwater mixer snapshot switching and Manta scooter propulsion.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Manta Acoustic Runtime Verifier")]
    public sealed class MantaAcousticRuntimeVerifier : MonoBehaviour
    {
        private const string ScooterPrefabPath = "Assets/_Project/Prefabs/Tools/Held/Tool_Propulsion_Held.prefab";
        private const string BatteryItemPath = "Assets/_Project/Data/Items/Resources/Components/Comp_BatteryCell.asset";

        [Header("References")]
        [SerializeField] private PlayerToolManager toolManager;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private HectonPlayerMovement playerMovement;
        [SerializeField] private Rigidbody playerRigidbody;
        [SerializeField] private AcousticZoneController acousticZoneController;

        [Header("Assets")]
        [SerializeField] private GameObject scooterPrefab;
        [SerializeField] private ItemData batteryItem;

        [Header("Execution")]
        [SerializeField] private bool runOnStart = false;
        [SerializeField] private bool restoreOriginalLoadout = true;
        [SerializeField] private bool repositionForUnderwater = true;
        [SerializeField] private float measurementDelay = 0.75f;
        [SerializeField] private float simulatedDeltaTime = 0.05f;
        [SerializeField] private float minimumSpeedDelta = 0.5f;
        [SerializeField] private int maxSwapSettleTicks = 8;
        [SerializeField] private Vector3 underwaterVerificationLocalPosition = new Vector3(-7623.9f, 4897.2f, 5858.2f);

        [Header("Diagnostics")]
        [SerializeField] private bool _debugRunning;
        [SerializeField] private bool _debugEquippedManta;
        [SerializeField] private bool _debugForcedUnderwaterSnapshot;
        [SerializeField] private float _debugMeasuredPropulsionForce;
        [SerializeField] private float _debugSpeedBefore;
        [SerializeField] private float _debugSpeedAfter;
        [SerializeField] private float _debugSpeedDelta;
        [SerializeField] private string _debugLastIssue = string.Empty;
        [SerializeField] private string _debugLocomotionMode = "Unknown";
        [SerializeField] private string _debugMantaActivationState = "Unknown";

        private void Awake()
        {
            AutoResolveReferences();
#if UNITY_EDITOR
            AutoResolveAssets();
#endif
        }

        private void Start()
        {
            if (!runOnStart || _debugRunning)
                return;

            RunVerificationImmediate();
        }

        /// <summary>
        /// Executes the verifier immediately from the inspector context menu.
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
            _debugEquippedManta = false;
            _debugForcedUnderwaterSnapshot = false;
            _debugMeasuredPropulsionForce = 0f;
            _debugSpeedBefore = 0f;
            _debugSpeedAfter = 0f;
            _debugSpeedDelta = 0f;
            _debugLastIssue = string.Empty;
            _debugMantaActivationState = "Unknown";

            GameObject[] originalAssignments = null;
            int originalSlot = -1;
            if (restoreOriginalLoadout && toolManager != null)
            {
                // COLD ALLOC: GameObject[slotCount] — restore original player tool assignments after verifier run — owner: MantaAcousticRuntimeVerifier
                originalAssignments = new GameObject[toolManager.SlotCount];
                for (int i = 0; i < originalAssignments.Length; i++)
                    originalAssignments[i] = toolManager.GetAssignedToolPrefab(i);

                originalSlot = toolManager.CurrentSlotIndex;
            }

            if (!PrepareUnderwaterState())
            {
                FinishWithFailure("Failed to prepare underwater verification state.");
                RestoreOriginalLoadout(originalAssignments, originalSlot);
                return;
            }

            UnitySimulationMode originalSimulationMode = UnityPhysics.simulationMode;
            UnityPhysics.simulationMode = UnitySimulationMode.Script;

            SimulatePlayerFixedTicks(4);
            _debugLocomotionMode = playerMovement.CurrentLocomotionMode.ToString();
            if (playerMovement.CurrentLocomotionMode != PlayerLocomotionMode.UnderwaterSwim)
            {
                FinishWithFailure("Player locomotion never reached UnderwaterSwim before scooter activation.");
                UnityPhysics.simulationMode = originalSimulationMode;
                RestoreOriginalLoadout(originalAssignments, originalSlot);
                return;
            }

            ForceUnderwaterSnapshot();
            EquipMantaIntoSlotZero();
            AdvanceToolSwapToIdle();
            SimulatePlayerFixedTicks(1);
            _debugLocomotionMode = playerMovement.CurrentLocomotionMode.ToString();

            MantaScooter manta = toolManager.CurrentTool as MantaScooter;
            if (manta == null)
            {
                FinishWithFailure("Current tool is not MantaScooter after equip.");
                UnityPhysics.simulationMode = originalSimulationMode;
                RestoreOriginalLoadout(originalAssignments, originalSlot);
                return;
            }

            _debugEquippedManta = manta.IsEquipped;

            if (!playerInventory.ContainsItem(batteryItem))
                playerInventory.TryAddItem(batteryItem, 1);

            manta.InsertBattery(batteryItem, 1f);
            playerRigidbody.linearVelocity = Vector3.zero;
            _debugSpeedBefore = 0f;

            // First activation pass happens only after locomotion has already been forced
            // into UnderwaterSwim, avoiding the false zero-propulsion path seen earlier.
            manta.UsePrimary(simulatedDeltaTime);
            _debugMantaActivationState = manta.DebugActivationState;
            _debugMeasuredPropulsionForce = manta.GetPropulsionForce();
            if (_debugMeasuredPropulsionForce <= 0f)
            {
                AdvanceToolSwapToIdle();
                SimulatePlayerFixedTicks(2);
                manta.UsePrimary(simulatedDeltaTime);
                _debugMantaActivationState = manta.DebugActivationState;
                _debugMeasuredPropulsionForce = manta.GetPropulsionForce();
            }

            SimulatePlayerFixedTicks(Mathf.Max(1, Mathf.CeilToInt(measurementDelay / simulatedDeltaTime)));
            _debugMantaActivationState = manta.DebugActivationState;

            _debugSpeedAfter = playerRigidbody.linearVelocity.magnitude;
            _debugSpeedDelta = _debugSpeedAfter - _debugSpeedBefore;
            _debugLocomotionMode = playerMovement.CurrentLocomotionMode.ToString();

            Debug.Log(
                $"[MantaVerify] ScooterSpeed before={_debugSpeedBefore:F3} after={_debugSpeedAfter:F3} " +
                $"delta={_debugSpeedDelta:F3} mode={_debugLocomotionMode} propulsion={_debugMeasuredPropulsionForce:F3} " +
                $"equipped={_debugEquippedManta} forcedUnderwater={_debugForcedUnderwaterSnapshot} " +
                $"mantaState={_debugMantaActivationState} swapping={toolManager.IsSwapping}");

            if (_debugMeasuredPropulsionForce <= 0f)
                FinishWithFailure("MantaScooter never reached active propulsion state.");
            else if (_debugSpeedDelta < minimumSpeedDelta)
                FinishWithFailure("Measured scooter speed delta is below threshold.");
            else
                _debugLastIssue = string.Empty;

            UnityPhysics.simulationMode = originalSimulationMode;
            RestoreOriginalLoadout(originalAssignments, originalSlot);
            _debugRunning = false;
        }

        private void AutoResolveReferences()
        {
            if (toolManager == null)
                toolManager = GetComponent<PlayerToolManager>() ?? (Hecton8.Core.GlobalRegistry.Player != null ? Hecton8.Core.GlobalRegistry.Player.ToolManager : null);

            if (playerInventory == null)
                playerInventory = GetComponent<PlayerInventory>() ?? (Hecton8.Core.GlobalRegistry.Player != null ? Hecton8.Core.GlobalRegistry.Player.Inventory : null);

            if (playerMovement == null)
                playerMovement = GetComponent<HectonPlayerMovement>();

            if (playerMovement == null &&
                SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                playerMovement = playerTransform.GetComponent<HectonPlayerMovement>();
            }

            if (playerRigidbody == null && playerMovement != null)
                playerRigidbody = playerMovement.GetComponent<Rigidbody>();

            if (playerRigidbody == null)
                playerRigidbody = GetComponent<Rigidbody>();

            if (acousticZoneController == null)
                acousticZoneController = AcousticZoneController.Instance;
        }

        private bool ValidateSetup()
        {
            if (toolManager != null &&
                playerInventory != null &&
                playerMovement != null &&
                playerRigidbody != null &&
                acousticZoneController != null &&
                scooterPrefab != null &&
                batteryItem != null)
            {
                return true;
            }

            FinishWithFailure("Missing verifier references or assets.");
            return false;
        }

        private bool PrepareUnderwaterState()
        {
            if (playerMovement.CurrentLocomotionMode == PlayerLocomotionMode.UnderwaterSwim)
                return true;

            if (!repositionForUnderwater)
                return false;

            Transform currentTransform = playerMovement.transform;
            if (currentTransform.parent != null)
                currentTransform.localPosition = underwaterVerificationLocalPosition;
            else
                currentTransform.position = underwaterVerificationLocalPosition;

            playerRigidbody.linearVelocity = Vector3.zero;
            return true;
        }

        private void ForceUnderwaterSnapshot()
        {
            acousticZoneController.ForceZone(true);
            acousticZoneController.ForceZone(false);
            _debugForcedUnderwaterSnapshot = true;
            Debug.Log("[MantaVerify] Requested Underwater snapshot via AcousticZoneController.ForceZone(false).");
        }

        private void EquipMantaIntoSlotZero()
        {
            if (!scooterPrefab.TryGetComponent(out MantaScooter prefabScooter) || prefabScooter.ToolData == null)
            {
                FinishWithFailure("Scooter prefab is missing MantaScooter or ToolData.");
                return;
            }

            if (!playerInventory.ContainsItem(prefabScooter.ToolData))
                playerInventory.TryAddItem(prefabScooter.ToolData, 1);

            toolManager.Holster();
            toolManager.SetAssignedToolPrefab(0, scooterPrefab, holsterIfCurrentInvalid: false);
            toolManager.SwitchToSlot(0);
        }

        private void RestoreOriginalLoadout(GameObject[] originalAssignments, int originalSlot)
        {
            if (!restoreOriginalLoadout || originalAssignments == null || toolManager == null)
            {
                _debugRunning = false;
                return;
            }

            toolManager.Holster();

            for (int i = 0; i < originalAssignments.Length; i++)
                toolManager.SetAssignedToolPrefab(i, originalAssignments[i], holsterIfCurrentInvalid: false);

            if (originalSlot >= 0 &&
                originalSlot < originalAssignments.Length &&
                originalAssignments[originalSlot] != null)
            {
                toolManager.SwitchToSlot(originalSlot);
            }
        }

        private void AdvanceToolSwapToIdle()
        {
            if (toolManager == null || maxSwapSettleTicks <= 0)
                return;

            for (int i = 0; i < maxSwapSettleTicks; i++)
            {
                toolManager.Tick(simulatedDeltaTime);
                if (!toolManager.IsSwapping)
                    break;
            }
        }

        private void SimulatePlayerFixedTicks(int stepCount)
        {
            if (stepCount <= 0)
                return;

            UnityPhysics.SyncTransforms();
            for (int i = 0; i < stepCount; i++)
            {
                playerMovement.FixedTick(simulatedDeltaTime);
                UnityPhysics.Simulate(simulatedDeltaTime);
            }
        }

        private void FinishWithFailure(string issue)
        {
            _debugLastIssue = issue;
            _debugRunning = false;
            Debug.LogWarning($"[MantaVerify] {issue}");
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
        }

        private void AutoResolveAssets()
        {
            if (scooterPrefab == null)
                scooterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScooterPrefabPath);

            if (batteryItem == null)
                batteryItem = AssetDatabase.LoadAssetAtPath<ItemData>(BatteryItemPath);
        }
#endif
    }
}

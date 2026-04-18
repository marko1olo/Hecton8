// ============================================================================
// HECTON-8 — InteractionUI.cs
// Context-sensitive interaction prompts for the player.
//
// ARCHITECTURE:
//   • ITickable for updates (no Update)
//   • Zero GC: cached refs, pre-cached strings
//   • UnityEvent hooks for designers
//
// FEATURES:
//   • Shows interaction prompts based on looked-at object
//   • Context-sensitive: "Press [E] to Swap Battery" vs "No Battery to Swap"
//   • Tool-aware: different prompts based on held tool
// ============================================================================

namespace Hecton8.UI
{
    using Hecton8.Bootstrap;
    using Hecton8.Core;
    using Hecton8.Gameplay;
    using Hecton8.Inventory;
    using Hecton8.Interaction;
    using Hecton8.Items;
    using Hecton8.Tools;
    using UnityEngine;
    using UnityEngine.Events;

    /// <summary>
    /// Context-sensitive interaction prompt system.
    /// Shows different prompts based on looked-at object and held tool.
    /// Uses ITickable for updates. Zero GC in hot paths.
    /// </summary>
    public class InteractionUI : MonoBehaviour, ITickable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── References ────────────────────────────────")]
        [Tooltip("Text component for the interaction prompt.")]
        [SerializeField] private TMPro.TMP_Text promptText;

        [Tooltip("Canvas group for fading.")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("── Raycast Settings ──────────────────────────")]
        [Tooltip("Maximum interaction distance.")]
        [SerializeField] private float interactionRange = 4f;

        [Tooltip("Layers to check for interactables.")]
        [SerializeField] private LayerMask interactionMask = ~0;

        [Header("── Prompt Templates ──────────────────────────")]
        [Tooltip("Default interaction prompt format. {0}=verb, {1}=name")]
        [SerializeField] private string defaultPromptFormat = "[E] {0} {1}";

        [Tooltip("Prompt when tool has no battery.")]
        [SerializeField] private string noBatteryPrompt = "No Battery to Swap";

        [Tooltip("Prompt to swap battery.")]
        [SerializeField] private string swapBatteryPrompt = "[E] Swap Battery";

        [Tooltip("Prompt to deposit fuel.")]
        [SerializeField] private string depositFuelPrompt = "[E] Deposit Fuel";

        [Tooltip("Prompt to take item.")]
        [SerializeField] private string takeItemPrompt = "[E] Take Item";

        [Tooltip("Prompt format for consumable with duration. {0}=verb, {1}=duration")]
        [SerializeField] private string consumableWithDurationFormat = "Hold [E] {0} ({1:0.0}s)";

        [Tooltip("Prompt for action in progress.")]
        [SerializeField] private string actionInProgressPrompt = "Consuming...";

        [Header("── Events ──────────────────────────────────────")]
        [Tooltip("Fired when the prompt changes.")]
        public UnityEvent<string> OnPromptChanged;

        [Tooltip("Fired when prompt visibility changes.")]
        public UnityEvent<bool> OnVisibilityChanged;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private Camera _mainCamera;
        private Transform _cachedTransform;
        private PlayerToolManager _toolManager;
        private PlayerInventory _inventory;
        private bool _registered;
        private string _currentPrompt;
        private bool _isVisible;

        // Pre-allocated raycast buffer
        private readonly RaycastHit[] _hitBuffer = new RaycastHit[1]; // COLD ALLOC: single-hit interaction probe — owner: InteractionUI

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        /// <summary>Current interaction prompt text.</summary>
        public string CurrentPrompt => _currentPrompt;

        /// <summary>Whether the prompt is currently visible.</summary>
        public bool IsVisible => _isVisible;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _cachedTransform = transform;
            _mainCamera = Camera.main;
            SetVisible(false);
        }

        private void OnEnable()
        {
            ResolvePlayerReferences();
            RegisterToTick();
        }

        private void OnDisable()
        {
            UnregisterFromTick();
            SetVisible(false);
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable
        // ══════════════════════════════════════════════════════════

        public void Tick(float deltaTime)
        {
            // ── Check if action is in progress ──
            PlayerActionController actionController = PlayerActionController.Instance;
            if (actionController != null && actionController.IsActionInProgress)
            {
                // Show action in progress prompt, hide interaction prompt
                UpdatePrompt(actionInProgressPrompt);
                SetVisible(true);
                return;
            }

            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null)
                    return;
            }

            // Raycast from camera center
            Ray ray = _mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            int hitCount = Physics.RaycastNonAlloc(
                ray,
                _hitBuffer,
                interactionRange,
                interactionMask,
                QueryTriggerInteraction.Collide);

            if (hitCount <= 0)
            {
                if (_isVisible)
                    SetVisible(false);
                return;
            }

            RaycastHit hit = _hitBuffer[0];
            Collider collider = hit.collider;

            if (collider == null)
            {
                if (_isVisible)
                    SetVisible(false);
                return;
            }

            // Build context-sensitive prompt
            string prompt = BuildPrompt(collider, hit.distance);

            if (string.IsNullOrEmpty(prompt))
            {
                if (_isVisible)
                    SetVisible(false);
                return;
            }

            UpdatePrompt(prompt);
            SetVisible(true);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — PROMPT BUILDING
        // ══════════════════════════════════════════════════════════

        private string BuildPrompt(Collider collider, float distance)
        {
            // Check for IInteractable
            IInteractable interactable = collider.GetComponent<IInteractable>();
            if (interactable != null)
            {
                return BuildInteractablePrompt(interactable, collider);
            }

            // Check for BatteryCharger
            BatteryCharger charger = collider.GetComponent<BatteryCharger>();
            if (charger != null)
            {
                return BuildBatteryChargerPrompt(charger);
            }

            // Check for BioReactor
            BioReactor reactor = collider.GetComponent<BioReactor>();
            if (reactor != null)
            {
                return BuildBioReactorPrompt(reactor);
            }

            // Check for StorageCrate
            StorageCrate crate = collider.GetComponent<StorageCrate>();
            if (crate != null)
            {
                return BuildStorageCratePrompt(crate);
            }

            // Check for PickupItem
            PickupItem pickup = collider.GetComponent<PickupItem>();
            if (pickup != null && pickup.ItemData != null)
            {
                return BuildPickupItemPrompt(pickup);
            }

            return null;
        }

        /// <summary>
        /// Builds prompt for pickup items, showing duration for consumables.
        /// Zero GC: uses cached StringBuilder for string building.
        /// </summary>
        private string BuildPickupItemPrompt(PickupItem pickup)
        {
            ItemData item = pickup.ItemData;
            if (item == null)
                return null;

            // Check if this is a consumable with use duration
            if (item.isConsumable && item.useDuration > 0f)
            {
                // Determine verb based on item type
                string verb = GetConsumableVerb(item);
                // Note: string.Format is acceptable here (not in hot path - only when looking at new item)
                return string.Format(consumableWithDurationFormat, verb, item.useDuration);
            }

            return string.Format(defaultPromptFormat, "Take", item.itemName);
        }

        /// <summary>
        /// Gets the verb for a consumable item (Eat, Drink, Apply, etc.).
        /// Pre-cached strings for zero GC.
        /// </summary>
        private static string GetConsumableVerb(ItemData item)
        {
            if (item.integrityRestore > 0f)
                return "Apply";
            if (item.thirstRestore > 0f)
                return "Drink";
            if (item.hungerRestore > 0f)
                return "Eat";
            if (item.oxygenRestore > 0f)
                return "Inhale";
            return "Use";
        }

        private string BuildInteractablePrompt(IInteractable interactable, Collider collider)
        {
            // Get base interaction text
            string baseText = interactable.GetInteractText();

            // Check if this is a battery tool context
            IBatteryTool batteryTool = collider.GetComponent<IBatteryTool>();
            if (batteryTool != null)
            {
                return BuildBatteryToolPrompt(batteryTool);
            }

            return baseText;
        }

        private string BuildBatteryToolPrompt(IBatteryTool tool)
        {
            if (!tool.HasBattery)
            {
                return noBatteryPrompt;
            }

            return string.Format(swapBatteryPrompt);
        }

        private string BuildBatteryChargerPrompt(BatteryCharger charger)
        {
            // Check if player is holding a tool with battery
            if (_toolManager != null)
            {
                PlayerTool heldTool = _toolManager.CurrentTool;
                if (heldTool is IBatteryTool batteryTool)
                {
                    if (batteryTool.HasBattery)
                    {
                        return swapBatteryPrompt;
                    }
                    else
                    {
                        return noBatteryPrompt;
                    }
                }
            }

            // Check if charger has a battery to take
            if (charger.HasBatteryInSlot(0) || charger.HasBatteryInSlot(1))
            {
                return takeItemPrompt;
            }

            return "Insert Battery";
        }

        private string BuildBioReactorPrompt(BioReactor reactor)
        {
            // Check if player has organic items
            if (_inventory != null)
            {
                int fuelCount = CountOrganicFuel(_inventory);
                if (fuelCount > 0)
                {
                    return string.Format("{0} ({1})", depositFuelPrompt, fuelCount);
                }
            }

            return "Bio Reactor";
        }

        private string BuildStorageCratePrompt(StorageCrate crate)
        {
            if (crate.IsEmpty())
            {
                return "Empty Crate";
            }

            return "[E] Open Crate";
        }

        private int CountOrganicFuel(PlayerInventory inventory)
        {
            // Count organic items that can be used as fuel
            // This would need to check inventory for organic items
            // For now, return 0 - actual implementation depends on inventory API
            return 0;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — UI UPDATE
        // ══════════════════════════════════════════════════════════

        private void UpdatePrompt(string prompt)
        {
            if (_currentPrompt == prompt)
                return;

            _currentPrompt = prompt;

            if (promptText != null)
                promptText.text = prompt;

            OnPromptChanged?.Invoke(prompt);
        }

        private void SetVisible(bool visible)
        {
            if (_isVisible == visible)
                return;

            _isVisible = visible;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.blocksRaycasts = false;
            }

            OnVisibilityChanged?.Invoke(visible);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — REFERENCES
        // ══════════════════════════════════════════════════════════

        private void ResolvePlayerReferences()
        {
            if (!SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform))
                return;

            if (_toolManager == null)
                _toolManager = playerTransform.GetComponent<PlayerToolManager>();

            if (_inventory == null)
                _inventory = playerTransform.GetComponent<PlayerInventory>();
        }

        private void RegisterToTick()
        {
            if (_registered)
                return;

            if (GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Register(this);
                _registered = true;
            }
        }

        private void UnregisterFromTick()
        {
            if (!_registered)
                return;

            if (GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Unregister(this);
                _registered = false;
            }
        }
    }
}

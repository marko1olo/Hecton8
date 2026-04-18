// ============================================================================
// HECTON-8 — MantaScooter.cs
// Handheld propulsion vehicle (Seaglide equivalent).
//
// ARCHITECTURE:
//   • PlayerTool-derived for inventory/tool slot integration
//   • IBatteryTool for BatteryCharger compatibility
//   • ITickable for active propulsion logic
//   • Zero GC: cached refs, MaterialPropertyBlock, pre-allocated arrays
//
// FEATURES:
//   • Increases swim speed while active and has battery
//   • Drains battery only while moving
//   • HUD display showing depth and battery %
// ============================================================================

namespace Hecton8.Gameplay
{
    using Hecton8.Audio;
    using Hecton8.Bootstrap;
    using Hecton8.Core;
    using Hecton8.Items;
    using Hecton8.Tools;
    using Hecton8.UI;
    using UnityEngine;

    /// <summary>
    /// Handheld propulsion scooter that increases swim speed.
    /// Implements IBatteryTool for battery swapping via BatteryCharger.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Tools/Manta Scooter")]
    public sealed class MantaScooter : PlayerTool, IBatteryTool, ITickable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Propulsion ────────────────────────────────")]
        [Tooltip("Swim speed multiplier when scooter is active.")]
        [SerializeField, Range(1.5f, 4f)] private float speedMultiplier = 2.2f;

        [Tooltip("Battery drain per second while moving.")]
        [SerializeField, Range(0.5f, 10f)] private float batteryDrainRate = 2f;

        [Tooltip("Minimum battery charge to activate (0-1).")]
        [SerializeField, Range(0f, 0.3f)] private float minChargeToActivate = 0.05f;

        [Header("── Audio ──────────────────────────────────────")]
        [Tooltip("Looping motor sound while active.")]
        [SerializeField] private AudioClip motorLoopClip;

        [Tooltip("Motor volume.")]
        [SerializeField, Range(0f, 1f)] private float motorVolume = 0.6f;

        [Header("── Visuals ────────────────────────────────────")]
        [Tooltip("Mesh to hide when battery is removed.")]
        [SerializeField] private GameObject batteryMesh;

        [Tooltip("Renderer for power indicator light.")]
        [SerializeField] private Renderer powerIndicatorRenderer;

        [Tooltip("Emission color when powered.")]
        [SerializeField] private Color powerOnColor = new Color(0f, 0.9f, 1f);

        [Tooltip("Emission color when low battery.")]
        [SerializeField] private Color lowBatteryColor = new Color(1f, 0.3f, 0f);

        [Header("── HUD Display ────────────────────────────────")]
        [Tooltip("Canvas group for the HUD display.")]
        [SerializeField] private CanvasGroup hudCanvasGroup;

        [Tooltip("Text component for depth display.")]
        [SerializeField] private TMPro.TMP_Text depthText;

        [Tooltip("Text component for battery display.")]
        [SerializeField] private TMPro.TMP_Text batteryText;

        // ══════════════════════════════════════════════════════════
        //  IBatteryTool STATE
        // ══════════════════════════════════════════════════════════

        private ItemData _batteryItem;
        private float _currentCharge;
        private bool _hasBattery;

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        private HectonPlayerMovement _playerMovement;
        private HectonSurvivalSystem _mantaSurvivalSystem;
        private AudioSource _motorAudioSource;
        private Rigidbody _playerRigidbody;
        private Transform _cachedTransform;
        private Camera _mainCamera;
        private bool _isActive;
        private bool _isMoving;
        private bool _registeredTick;
        private bool _hudStateInitialized;
        private bool _lastHudVisible;
        private int _lastDepthTenths = int.MinValue;
        private int _lastBatteryPercent = int.MinValue;
        private bool _summaryStateInitialized;
        private bool _lastSummaryHasBattery;
        private bool _lastSummaryActive;
        private int _lastSummaryBatteryPercent = int.MinValue;
        private string _cachedOperationalSummary = "MANTA // NO BATTERY";

        // MaterialPropertyBlock for power indicator
        private MaterialPropertyBlock _mpb;
        private static readonly int _EmissionColorID = Shader.PropertyToID("_EmissionColor");

        // ══════════════════════════════════════════════════════════
        //  IBatteryTool IMPLEMENTATION
        // ══════════════════════════════════════════════════════════

        /// <summary>True if the tool currently has a battery installed.</summary>
        public bool HasBattery => _hasBattery;

        /// <summary>Current battery charge level (0-1). Returns 0 if no battery.</summary>
        public float BatteryCharge => _hasBattery ? _currentCharge : 0f;

        /// <summary>The battery item currently installed (null if none).</summary>
        public ItemData BatteryItem => _batteryItem;

        /// <summary>
        /// Removes the battery from the tool.
        /// </summary>
        public ItemData RemoveBattery()
        {
            if (!_hasBattery)
                return null;

            ItemData removed = _batteryItem;
            _batteryItem = null;
            _currentCharge = 0f;
            _hasBattery = false;

            UpdateBatteryVisuals();
            UpdatePowerIndicator();

            return removed;
        }

        /// <summary>
        /// Inserts a battery into the tool.
        /// </summary>
        public bool InsertBattery(ItemData battery, float charge)
        {
            if (battery == null)
                return false;

            _batteryItem = battery;
            _currentCharge = Mathf.Clamp01(charge);
            _hasBattery = true;

            UpdateBatteryVisuals();
            UpdatePowerIndicator();

            return true;
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _cachedTransform = transform;
            _mpb = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] — power indicator emission — owner: MantaScooter

            // Setup motor audio
            TryGetComponent(out _motorAudioSource);
            if (_motorAudioSource == null && motorLoopClip != null)
            {
                _motorAudioSource = gameObject.AddComponent<AudioSource>();
                _motorAudioSource.playOnAwake = false;
                _motorAudioSource.loop = true;
                _motorAudioSource.spatialBlend = 1f;
            }
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            _isActive = false;
            _registeredTick = false;
            ResolvePlayerReferences();
            ResetHudStateCache();
            UpdateBatteryVisuals();
            UpdatePowerIndicator();
        }

        public override void OnDespawn()
        {
            DeactivateScooter();
            UnregisterFromTick();
            ResetHudStateCache();
            base.OnDespawn();
        }

        public override void OnEquip()
        {
            base.OnEquip();
            ResolvePlayerReferences();
            ResetHudStateCache();
            RegisterToTick();
            UpdateBatteryVisuals();
            UpdatePowerIndicator();
        }

        public override void OnUnequip()
        {
            DeactivateScooter();
            UnregisterFromTick();
            ResetHudStateCache();
            base.OnUnequip();
        }

        // ══════════════════════════════════════════════════════════
        //  TOOL ACTIONS
        // ══════════════════════════════════════════════════════════

        public override void UsePrimary(float deltaTime)
        {
            if (!IsEquipped)
                return;

            // Check battery
            if (!_hasBattery || _currentCharge < minChargeToActivate)
            {
                if (_isActive)
                    DeactivateScooter();

                ToolHitUtility.ShowWarning("MANTA - NO BATTERY");
                return;
            }

            // Check if player is swimming
            if (_playerMovement == null || 
                _playerMovement.CurrentLocomotionMode != PlayerLocomotionMode.UnderwaterSwim)
            {
                if (_isActive)
                    DeactivateScooter();

                return;
            }

            // Activate if not already active
            if (!_isActive)
                ActivateScooter();

            // Check if player is moving
            _isMoving = IsPlayerMoving();

            if (_isMoving)
            {
                // Drain battery while moving
                _currentCharge = Mathf.Max(0f, _currentCharge - batteryDrainRate * deltaTime);
                UpdatePowerIndicator();
                UpdateHUD();

                if (_currentCharge <= 0f)
                {
                    DeactivateScooter();
                    ToolHitUtility.ShowWarning("MANTA - BATTERY DEPLETED");
                }
            }
        }

        public override void UseSecondary(float deltaTime)
        {
            // Secondary does nothing for scooter - could be used for headlight toggle
        }

        public override void ToolTick(float deltaTime)
        {
            // Called by PlayerToolManager - we use ITickable for HUD updates
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable
        // ══════════════════════════════════════════════════════════

        public void Tick(float deltaTime)
        {
            if (!IsEquipped)
                return;

            UpdateHUD();
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        public override string GetOperationalSummary()
        {
            int batteryPercent = Mathf.RoundToInt(_currentCharge * 100f);
            if (!_summaryStateInitialized ||
                _lastSummaryHasBattery != _hasBattery ||
                _lastSummaryActive != _isActive ||
                _lastSummaryBatteryPercent != batteryPercent)
            {
                _cachedOperationalSummary = !_hasBattery
                    ? "MANTA // NO BATTERY"
                    : _isActive
                        ? "MANTA // ACTIVE // BAT " + batteryPercent + "%"
                        : "MANTA // STANDBY // BAT " + batteryPercent + "%";

                _lastSummaryHasBattery = _hasBattery;
                _lastSummaryActive = _isActive;
                _lastSummaryBatteryPercent = batteryPercent;
                _summaryStateInitialized = true;
            }

            return _cachedOperationalSummary;
        }

        public override string GetOperationalDirective()
        {
            if (!_hasBattery)
                return "Insert a battery to activate propulsion.";

            if (_currentCharge < minChargeToActivate)
                return "Battery depleted. Swap or recharge.";

            if (_isActive)
                return "Hold forward to propel. Release to coast.";

            return "Hold primary to activate propulsion while swimming.";
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — ACTIVATION
        // ══════════════════════════════════════════════════════════

        private void ActivateScooter()
        {
            _isActive = true;

            // Start motor sound
            if (_motorAudioSource != null && motorLoopClip != null && !_motorAudioSource.isPlaying)
            {
                _motorAudioSource.clip = motorLoopClip;
                _motorAudioSource.volume = motorVolume;
                _motorAudioSource.Play();
            }

            UpdatePowerIndicator();
        }

        private void DeactivateScooter()
        {
            _isActive = false;
            _isMoving = false;

            // Stop motor sound
            if (_motorAudioSource != null && _motorAudioSource.isPlaying)
                _motorAudioSource.Stop();

            UpdatePowerIndicator();
        }

        private bool IsPlayerMoving()
        {
            if (_playerRigidbody == null)
                return false;

            return _playerRigidbody.linearVelocity.sqrMagnitude > 0.25f;
        }

        /// <summary>
        /// Gets the swim speed multiplier to apply when scooter is active.
        /// Called by HectonPlayerMovement.SwimPhysics.
        /// </summary>
        public float GetSpeedMultiplier()
        {
            if (!_isActive || !_hasBattery || _currentCharge < minChargeToActivate)
                return 1f;

            return speedMultiplier;
        }

        /// <summary>
        /// Gets the additional propulsion force to apply.
        /// Called by HectonPlayerMovement.SwimPhysics.
        /// </summary>
        public float GetPropulsionForce()
        {
            if (!_isActive || !_hasBattery || _currentCharge < minChargeToActivate)
                return 0f;

            // Return additional force based on battery charge
            return 800f * _currentCharge; // Scale force with battery level
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — VISUALS
        // ══════════════════════════════════════════════════════════

        private void UpdateBatteryVisuals()
        {
            if (batteryMesh != null && batteryMesh.activeSelf != _hasBattery)
                batteryMesh.SetActive(_hasBattery);
        }

        private void UpdatePowerIndicator()
        {
            if (powerIndicatorRenderer == null || _mpb == null)
                return;

            powerIndicatorRenderer.GetPropertyBlock(_mpb);

            if (!_hasBattery || _currentCharge <= 0f)
            {
                // No power - dark
                _mpb.SetColor(_EmissionColorID, Color.black);
            }
            else if (_currentCharge <= 0.2f)
            {
                // Low battery - orange
                _mpb.SetColor(_EmissionColorID, lowBatteryColor);
            }
            else if (_isActive)
            {
                // Active - bright cyan
                _mpb.SetColor(_EmissionColorID, powerOnColor * 2f);
            }
            else
            {
                // Standby - dim cyan
                _mpb.SetColor(_EmissionColorID, powerOnColor * 0.5f);
            }

            powerIndicatorRenderer.SetPropertyBlock(_mpb);
        }

        private void UpdateHUD()
        {
            if (hudCanvasGroup == null)
                return;

            // Show HUD only when equipped and active
            bool showHUD = IsEquipped && _hasBattery;
            if (!_hudStateInitialized || showHUD != _lastHudVisible)
            {
                hudCanvasGroup.alpha = showHUD ? 1f : 0f;
                hudCanvasGroup.blocksRaycasts = false;
                _lastHudVisible = showHUD;
                _hudStateInitialized = true;
            }

            if (!showHUD)
                return;

            // Update depth display
            if (depthText != null && _playerMovement != null)
            {
                int depthTenths = Mathf.RoundToInt(_playerMovement.CurrentDepth * 10f);
                if (depthTenths != _lastDepthTenths)
                {
                    depthText.SetText("{0:0.0}m", depthTenths * 0.1f);
                    _lastDepthTenths = depthTenths;
                }
            }

            // Update battery display
            if (batteryText != null)
            {
                int batteryPercent = Mathf.RoundToInt(_currentCharge * 100f);
                if (batteryPercent != _lastBatteryPercent)
                {
                    batteryText.SetText("{0:0}%", batteryPercent);
                    _lastBatteryPercent = batteryPercent;
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — REFERENCES
        // ══════════════════════════════════════════════════════════

        private void ResolvePlayerReferences()
        {
            if (_playerMovement == null)
                _playerMovement = GetComponentInParent<HectonPlayerMovement>();

            if (_mantaSurvivalSystem == null && _playerMovement != null)
                _playerMovement.TryGetComponent(out _mantaSurvivalSystem);

            if (_playerRigidbody == null && _playerMovement != null)
                _playerMovement.TryGetComponent(out _playerRigidbody);

            if ((_playerMovement == null || _mantaSurvivalSystem == null || _playerRigidbody == null) &&
                SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform))
            {
                if (_playerMovement == null)
                    _playerMovement = playerTransform.GetComponent<HectonPlayerMovement>();

                if (_mantaSurvivalSystem == null)
                    _mantaSurvivalSystem = playerTransform.GetComponent<HectonSurvivalSystem>();

                if (_playerRigidbody == null)
                    _playerRigidbody = playerTransform.GetComponent<Rigidbody>();
            }

            if (_mainCamera == null)
                _mainCamera = Camera.main;
        }

        private void ResetHudStateCache()
        {
            _hudStateInitialized = false;
            _lastHudVisible = false;
            _lastDepthTenths = int.MinValue;
            _lastBatteryPercent = int.MinValue;
            _summaryStateInitialized = false;
            _lastSummaryHasBattery = false;
            _lastSummaryActive = false;
            _lastSummaryBatteryPercent = int.MinValue;
            _cachedOperationalSummary = "MANTA // NO BATTERY";
        }

        private void RegisterToTick()
        {
            if (_registeredTick)
                return;

            if (GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Register(this);
                _registeredTick = true;
            }
        }

        private void UnregisterFromTick()
        {
            if (!_registeredTick)
                return;

            if (GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Unregister(this);
                _registeredTick = false;
            }
        }
    }
}

// ============================================================================
// HECTON-8 - FlashlightTool.cs
// Hand-tool adapter over the existing PlayerFlashlight system.
// Does not create a second flashlight pipeline.
// ============================================================================

namespace Hecton8.Gameplay
{
    using Hecton8.Bootstrap;
    using Hecton8.Core;
    using Hecton8.Input;
    using Hecton8.Interaction;
    using Hecton8.Items;
    using Hecton8.Scavenging;
    using Hecton8.Tools;
    using Hecton8.UI;
    using UnityEngine;

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Tools/Flashlight Tool")]
    public sealed class FlashlightTool : PlayerTool, IBatteryTool
    {
        private readonly struct LampAssessment
        {
            public readonly string Headline;
            public readonly string Summary;
            public readonly string Recommendation;
            public readonly string Severity;

            public LampAssessment(string headline, string summary, string recommendation, string severity)
            {
                Headline = headline;
                Summary = summary;
                Recommendation = recommendation;
                Severity = severity;
            }

            public string BuildHudMessage()
            {
                return $"{Headline} | {Summary} | {Recommendation}";
            }
        }

        [Header("Adapter")]
        [SerializeField] private bool autoTurnOffOnUnequip = true;
        [SerializeField] private bool secondaryCyclesBeamMode = true;
        [SerializeField] private float contextProbeRange = 18f;
        [SerializeField] private LayerMask contextMask = (1 << 8) | (1 << 9) | (1 << 10);

        [Header("── Battery Settings ─────────────────────────")]
        [Tooltip("Battery item type this tool uses.")]
        [SerializeField] private ItemData _batteryItemType;

        [Header("── Battery Visuals ──────────────────────────")]
        [Tooltip("Mesh to hide when battery is removed.")]
        [SerializeField] private GameObject _batteryMesh;

        [Tooltip("Renderer for power indicator light.")]
        [SerializeField] private Renderer _powerIndicatorRenderer;

        [Tooltip("Emission color when powered.")]
        [SerializeField] private Color _powerOnColor = new Color(1f, 0.9f, 0.5f);

        private ItemData _installedBattery;

        // MaterialPropertyBlock for power indicator
        private MaterialPropertyBlock _mpb; // COLD ALLOC: MaterialPropertyBlock[1] — power indicator emission — owner: FlashlightTool
        private static readonly int _EmissionColorID = Shader.PropertyToID("_EmissionColor");
        private static readonly int _ToolBatteryNormalizedID = Shader.PropertyToID("_ToolBatteryNormalized");

        // ══════════════════════════════════════════════════════════
        //  IBatteryTool IMPLEMENTATION
        // ══════════════════════════════════════════════════════════

        /// <summary>True if the tool currently has a battery installed.</summary>
        public bool HasBattery => _installedBattery != null;

        /// <summary>Current battery charge level (0-1). Returns 0 if no battery.</summary>
        public float BatteryCharge => _installedBattery != null ? GetRuntimeBatteryNormalized(0f) : 0f;

        /// <summary>The battery item currently installed (null if none).</summary>
        public ItemData BatteryItem => _installedBattery;

        /// <summary>
        /// Removes the battery from the tool.
        /// </summary>
        public ItemData RemoveBattery()
        {
            if (_installedBattery == null)
                return null;

            ItemData removed = _installedBattery;
            _installedBattery = null;

            SetRuntimeBatteryNormalized(0f);
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

            _installedBattery = battery;
            SetRuntimeBatteryNormalized(charge);
            UpdateBatteryVisuals();
            UpdatePowerIndicator();

            return true;
        }

        private void UpdateBatteryVisuals()
        {
            if (_batteryMesh != null)
                _batteryMesh.SetActive(_installedBattery != null);
        }

        private void UpdatePowerIndicator()
        {
            if (_powerIndicatorRenderer == null)
                return;

            _powerIndicatorRenderer.GetPropertyBlock(_mpb);

            float batteryCharge = BatteryCharge;
            float flickerScalar = 1f;
            if (TryGetWirelessBrownoutFlicker(out float brownoutFlicker))
                flickerScalar = Mathf.Clamp(brownoutFlicker, 0f, 1f);

            _mpb.SetFloat(_ToolBatteryNormalizedID, Mathf.Clamp01(batteryCharge));
            if (_installedBattery == null || batteryCharge <= 0f)
            {
                _mpb.SetColor(_EmissionColorID, Color.black);
            }
            else if (batteryCharge <= 0.2f)
            {
                _mpb.SetColor(_EmissionColorID, new Color(1f, 0.3f, 0f) * flickerScalar);
            }
            else
            {
                _mpb.SetColor(_EmissionColorID, _powerOnColor * flickerScalar);
            }

            _powerIndicatorRenderer.SetPropertyBlock(_mpb);
        }

        private PlayerFlashlight _flashlight;
        private HUDNotification _hudNotification;
        private bool _stateBeforeEquip;
        private bool _primaryLatched;
        private bool _secondaryLatched;
        private bool _missingFlashlightWarned;
        private int _cachedSnapshotFrame = -1;
        private string _cachedOperationalSummary;
        private string _cachedOperationalRecommendation;
        private int _cachedContextDirectiveFrame = -1;
        private bool _cachedHasContextDirective;
        private string _cachedContextDirective;

        // ══════════════════════════════════════════════════════════
        //  IBatteryTool STATE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] — power indicator emission — owner: FlashlightTool
        }

        public override void OnEquip()
        {
            base.OnEquip();

            ResolveRuntimeReferences();
            _stateBeforeEquip = _flashlight != null && _flashlight.IsOn;
            _primaryLatched = false;
            _secondaryLatched = false;
            UpdatePowerIndicator();
            InvalidateSnapshotCache();
        }

        internal override float ResolveModularBatteryNormalized()
        {
            return _installedBattery != null ? GetRuntimeBatteryNormalized(1f) : 0f;
        }

        protected override void ConfigureModularRuntimeProfile(ref ToolRuntimeProfile profile)
        {
            profile.MaxRange = Mathf.Max(0.1f, contextProbeRange);
            profile.PowerScalar = 1f;
            profile.BatteryCapacity = 1f;
            profile.BatteryDrainPerSecond = Metadata != null ? Mathf.Max(0f, Metadata.GetTotalEnergyConsumption()) : 0.02f;
        }

        public override void OnUnequip()
        {
            if (autoTurnOffOnUnequip &&
                _flashlight != null &&
                !_stateBeforeEquip &&
                _flashlight.IsOn)
            {
                _flashlight.TurnOff();
            }

            _primaryLatched = false;
            _secondaryLatched = false;
            InvalidateSnapshotCache();
            base.OnUnequip();
        }

        public override void OnDespawn()
        {
            if (_flashlight != null)
                _flashlight.UnbindExternalBatteryTool(this);

            base.OnDespawn();
        }

        public override void UsePrimary(float deltaTime)
        {
            if (_primaryLatched)
                return;

            _primaryLatched = true;

            if (!TryResolveFlashlight())
                return;

            if (!_flashlight.IsOn && !HasToolEnergyOrWirelessPath())
            {
                PublishAssessment(new LampAssessment(
                    "DIVE LAMP - CELL DEPLETED",
                    "No charged battery module is available for lamp startup.",
                    "Insert a charged cell or route power before deployment.",
                    "WARN"));
                InvalidateSnapshotCache();
                return;
            }

            if (_flashlight.IsOverheated)
            {
                InvalidateSnapshotCache();
                LampAssessment cooling = BuildAssessment();
                PublishAssessment(cooling);
                FieldOperationLogSystem.RecordOperation(
                    "FLASHLIGHT",
                    "DIVE LAMP COOLING",
                    $"{cooling.Summary} | {cooling.Recommendation}",
                    "WARN");
                return;
            }

            _flashlight.Toggle();
            FieldOperationLogSystem.RecordOperation(
                "FLASHLIGHT",
                _flashlight.IsOn ? "DIVE LAMP ACTIVATED" : "DIVE LAMP STOWED",
                _flashlight.IsOn
                    ? "Hand lamp is now contributing to the active field visibility stack."
                    : "Hand lamp returned to standby to preserve expedition power discipline.",
                "INFO");
            InvalidateSnapshotCache();
            PublishAssessment(BuildAssessment());
        }

        public override void UseSecondary(float deltaTime)
        {
            if (_secondaryLatched)
                return;

            _secondaryLatched = true;

            if (!TryResolveFlashlight())
                return;

            if (secondaryCyclesBeamMode)
            {
                _flashlight.CycleBeamMode();
                InvalidateSnapshotCache();
                string mode = _flashlight.BeamModeLabel;
                LampAssessment assessment = BuildAssessment();
                FieldOperationLogSystem.RecordOperation(
                    "FLASHLIGHT",
                    $"DIVE LAMP {mode} PROFILE",
                    $"{assessment.Summary} | {assessment.Recommendation}",
                    "INFO");
                PublishAssessment(assessment);
                return;
            }

            LampAssessment status = BuildAssessment();
            FieldOperationLogSystem.RecordOperation(
                "FLASHLIGHT",
                "DIVE LAMP STATUS QUERY",
                $"{status.Summary} | {status.Recommendation}",
                status.Severity);
            PublishAssessment(status);
        }

        public override void ToolTick(float deltaTime)
        {
            UpdatePowerIndicator();

            IInputService inputService = GlobalRegistry.Input;
            PlayerInputState inputState = inputService != null && inputService.IsPlayerInputEnabled
                ? inputService.GetState()
                : default;

            if (!inputState.HasAction(PlayerInputAction.PrimaryFire))
                _primaryLatched = false;

            if (!inputState.HasAction(PlayerInputAction.SecondaryFire))
                _secondaryLatched = false;

            if (!TryResolveFlashlight())
                return;

            if (_flashlight.IsOn && !HasToolEnergyOrWirelessPath())
            {
                _flashlight.TurnOff();
                UpdatePowerIndicator();
                InvalidateSnapshotCache();
                return;
            }

            if (_flashlight.IsOn)
            {
                bool hadEnergy = TryConsumeRuntimeEnergy(deltaTime);
                UpdatePowerIndicator();
                InvalidateSnapshotCache();

                if (!hadEnergy)
                {
                    _flashlight.TurnOff();
                    PublishAssessment(new LampAssessment(
                        "DIVE LAMP - CELL DEPLETED",
                        "Active battery module reached zero charge during lamp operation.",
                        "Swap the cell or route external power before relight.",
                        "WARN"));
                }
            }
        }

        private void ResolveRuntimeReferences()
        {
            if (_flashlight == null)
                _flashlight = GetComponentInParent<PlayerFlashlight>();

            if (_flashlight == null)
            {
                if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                    playerTransform != null)
                {
                    _flashlight = ((Hecton8.Core.GlobalRegistry.Player != null && Hecton8.Core.GlobalRegistry.Player.Flashlight != null) ? Hecton8.Core.GlobalRegistry.Player.Flashlight : playerTransform.GetComponent<PlayerFlashlight>());
                }
            }

            if (_flashlight != null)
                _flashlight.BindExternalBatteryTool(this);

            if (_hudNotification == null)
                HUDNotification.TryGetActive(out _hudNotification);
        }

        private bool TryResolveFlashlight()
        {
            ResolveRuntimeReferences();

            if (_flashlight != null)
                return true;

            if (!_missingFlashlightWarned)
            {
                Debug.LogWarning("[FlashlightTool] No PlayerFlashlight found in scene.");
                _missingFlashlightWarned = true;
            }

            return false;
        }

        private void ShowInfo(string message)
        {
            if (_hudNotification != null)
                _hudNotification.ShowInfo(message);
            else
                Debug.Log(message);
        }

        public override string GetOperationalSummary()
        {
            if (!TryResolveFlashlight())
                return "DIVE LAMP // LINK OFFLINE";

            if (TryGetOperationalSnapshot(out string summary, out _))
                return summary;

            return _flashlight.BuildOperationalSummary();
        }

        public override string GetOperationalDirective()
        {
            if (!TryResolveFlashlight())
                return "Restore the lamp link before field deployment.";

            if (TryGetOperationalSnapshot(out _, out string recommendation))
            {
                if (TryGetForwardContextDirectiveCached(out string contextDirective))
                    return contextDirective;

                return recommendation;
            }

            if (TryGetForwardContextDirective(out string directive))
                return directive;

            return _flashlight.BuildOperationalRecommendation();
        }

        private string BuildStatusSnapshot()
        {
            if (_flashlight == null)
                return "Lamp diagnostics unavailable.";

            if (TryGetOperationalSnapshot(out string summary, out _))
                return summary;

            return _flashlight.BuildOperationalSummary();
        }

        private LampAssessment BuildAssessment()
        {
            if (_flashlight == null)
            {
                return new LampAssessment(
                    "DIVE LAMP - LINK OFFLINE",
                    "Flashlight diagnostics are unavailable.",
                    "Re-establish the lamp link before field deployment.",
                    "WARN");
            }

            if (!TryGetOperationalSnapshot(out string summary, out string recommendation))
            {
                summary = _flashlight.BuildOperationalSummary();
                recommendation = _flashlight.BuildOperationalRecommendation();
            }

            if (_flashlight.IsOverheated)
            {
                return new LampAssessment(
                    $"DIVE LAMP - COOLING {Mathf.CeilToInt(_flashlight.CooldownRemaining)}S",
                    summary,
                    recommendation,
                    "WARN");
            }

            if (_flashlight.EnergyPercent <= 10f)
            {
                return new LampAssessment(
                    $"DIVE LAMP - LOW ENERGY [{_flashlight.BeamModeLabel}]",
                    summary,
                    recommendation,
                    "WARN");
            }

            if (_flashlight.HeatLevel >= 0.7f)
            {
                return new LampAssessment(
                    $"DIVE LAMP - HEAT RISING [{_flashlight.BeamModeLabel}]",
                    summary,
                    recommendation,
                    "WARN");
            }

            string contextualRecommendation = TryGetForwardContextDirectiveCached(out string contextDirective)
                ? contextDirective
                : recommendation;

            return new LampAssessment(
                _flashlight.IsOn
                    ? $"DIVE LAMP - ON [{_flashlight.BeamModeLabel}]"
                    : $"DIVE LAMP - STANDBY [{_flashlight.BeamModeLabel}]",
                summary,
                contextualRecommendation,
                "INFO");
        }

        private bool TryGetOperationalSnapshot(out string summary, out string recommendation)
        {
            summary = null;
            recommendation = null;

            if (_flashlight == null)
                return false;

            int currentFrame = Time.frameCount;
            if (_cachedSnapshotFrame == currentFrame)
            {
                summary = _cachedOperationalSummary;
                recommendation = _cachedOperationalRecommendation;
                return true;
            }

            summary = _flashlight.BuildOperationalSummary();
            recommendation = _flashlight.BuildOperationalRecommendation();
            _cachedSnapshotFrame = currentFrame;
            _cachedOperationalSummary = summary;
            _cachedOperationalRecommendation = recommendation;
            return true;
        }

        private bool TryGetForwardContextDirectiveCached(out string contextDirective)
        {
            contextDirective = null;

            if (_flashlight == null)
                return false;

            int currentFrame = Time.frameCount;
            if (_cachedContextDirectiveFrame == currentFrame)
            {
                contextDirective = _cachedContextDirective;
                return _cachedHasContextDirective;
            }

            bool hasDirective = TryGetForwardContextDirective(out contextDirective);
            _cachedContextDirectiveFrame = currentFrame;
            _cachedHasContextDirective = hasDirective;
            _cachedContextDirective = contextDirective;
            return hasDirective;
        }

        private void InvalidateSnapshotCache()
        {
            _cachedSnapshotFrame = -1;
            _cachedOperationalSummary = null;
            _cachedOperationalRecommendation = null;
            _cachedContextDirectiveFrame = -1;
            _cachedHasContextDirective = false;
            _cachedContextDirective = null;
        }

        private bool TryGetForwardContextDirective(out string directive)
        {
            directive = null;

            Transform probeOrigin = transform;
            if (_flashlight == null || probeOrigin == null)
                return false;

            var cache = Hecton8.Physics.GlobalQueryCacheManager.GetContext("PlayerLook");
            Ray ray = new Ray(probeOrigin.position, probeOrigin.forward);
            
            if (!cache.TryGet(ray, contextProbeRange, contextMask, out Hecton8.Physics.QueryResult qResult))
            {
                if (!TryResolveQueuedRaycast(ray.origin, ray.direction, contextProbeRange, contextMask.value, QueryTriggerInteraction.Collide, out RaycastHit hit))
                    return false;
                qResult = new Hecton8.Physics.QueryResult { hasHit = true, hit = hit };
                cache.Set(ray, contextProbeRange, contextMask, qResult);
            }

            if (!qResult.hasHit) 
                return false;

            RaycastHit finalHit = qResult.hit;

            Collider collider = finalHit.collider;
            if (collider == null)
                return false;

            if (FieldTargetDescriptor.TryResolve(collider, out FieldTargetDescriptor descriptor))
            {
                if (FieldTargetSemantics.TryBuildFlashlightDirective(descriptor, finalHit.distance, out directive))
                    return true;
            }

            if (collider.GetComponent<ScannableTarget>() != null || collider.GetComponentInParent<ScannableTarget>() != null)
            {
                directive = finalHit.distance >= 10f
                    ? "Use FOCUS to read distant probes and hazard points before closing in."
                    : "Use STANDARD while you classify the probe and keep route awareness.";
                return true;
            }

            PickupItem pickup = collider.GetComponent<PickupItem>() ?? collider.GetComponentInParent<PickupItem>();
            if (pickup != null)
            {
                directive = finalHit.distance <= 5f
                    ? "Use FLOOD to sweep the nearby salvage pocket without overshooting the pickup."
                    : "Use STANDARD until the pickup lane tightens, then widen to FLOOD.";
                return true;
            }

            ResourceNode node = collider.GetComponent<ResourceNode>() ?? collider.GetComponentInParent<ResourceNode>();
            if (node != null)
            {
                directive = finalHit.distance >= 9f
                    ? "Use FOCUS to probe the node edge before committing cutter or sampler."
                    : "Use STANDARD to hold visibility on the extraction face.";
                return true;
            }

            BaseModule module = collider.GetComponent<BaseModule>() ?? collider.GetComponentInParent<BaseModule>();
            if (module != null)
            {
                directive = finalHit.distance >= 9f
                    ? "Use FOCUS for distant module reads and service planning."
                    : "Use STANDARD to maintain service visibility on the module face.";
                return true;
            }

            return false;
        }

        private void PublishAssessment(LampAssessment assessment)
        {
            if (_hudNotification != null)
            {
                if (assessment.Severity == "WARN" || assessment.Severity == "CRITICAL")
                    _hudNotification.ShowWarning(assessment.BuildHudMessage());
                else
                    _hudNotification.ShowInfo(assessment.BuildHudMessage());
                return;
            }

            Debug.Log(assessment.BuildHudMessage());
        }

        // ══════════════════════════════════════════════════════════
        //  IBatteryTool IMPLEMENTATION
        // ══════════════════════════════════════════════════════════

    }
}

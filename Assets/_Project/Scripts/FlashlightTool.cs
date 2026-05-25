// ============================================================================
// HECTON-8 - FlashlightTool.cs
// Hand-tool adapter over the existing PlayerFlashlight system.
// Does not create a second flashlight pipeline.
// ============================================================================

namespace Hecton8.Gameplay
{
    using Hecton8.Core;
    using Hecton8.Interaction;
    using Hecton8.Items;
    using Hecton8.Tools;
    using Hecton8.UI;
    using Unity.Mathematics;
    using UnityEngine;

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Tools/Flashlight Tool")]
    public sealed class FlashlightTool : PlayerTool, IBatteryTool, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private readonly struct LampAssessment
        {
            public readonly string Headline;
            public readonly string Summary;
            public readonly string Recommendation;
            public readonly string Severity;
            public readonly int CooldownSeconds;
            public readonly string BeamModeLabel;
            public readonly byte Flags;

            private const byte FlagAppendCooldown = 1 << 0;
            private const byte FlagAppendBeamMode = 1 << 1;

            public LampAssessment(string headline, string summary, string recommendation, string severity, int cooldownSeconds = 0, string beamModeLabel = null, byte flags = 0)
            {
                Headline = headline;
                Summary = summary;
                Recommendation = recommendation;
                Severity = severity;
                CooldownSeconds = cooldownSeconds;
                BeamModeLabel = beamModeLabel;
                Flags = flags;
            }

            public static byte WithCooldown => FlagAppendCooldown;
            public static byte WithBeamMode => FlagAppendBeamMode;

            public bool TryWriteHudMessage(ref FixedCharBuffer buffer)
            {
                if (!AppendText(ref buffer, Headline))
                    return false;

                if ((Flags & FlagAppendCooldown) != 0)
                {
                    if (!AppendText(ref buffer, " ") || !buffer.AppendInt(CooldownSeconds) || !AppendText(ref buffer, "S"))
                        return false;
                }

                if ((Flags & FlagAppendBeamMode) != 0 && !string.IsNullOrEmpty(BeamModeLabel))
                {
                    if (!AppendText(ref buffer, " [") || !AppendText(ref buffer, BeamModeLabel) || !AppendText(ref buffer, "]"))
                        return false;
                }

                return AppendText(ref buffer, " | ") &&
                       AppendText(ref buffer, Summary) &&
                       AppendText(ref buffer, " | ") &&
                       AppendText(ref buffer, Recommendation);
            }
        }

        [Header("Adapter")]
        [SerializeField] private bool autoTurnOffOnUnequip = true;
        [SerializeField] private bool secondaryCyclesBeamMode = true;
        [SerializeField] private float contextProbeRange = 18f;
        [SerializeField] private LayerMask contextMask = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;

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
        private float _batteryCharge = 1f;

        // MaterialPropertyBlock for power indicator
        private MaterialPropertyBlock _mpb; // COLD ALLOC: MaterialPropertyBlock[1] — power indicator emission — owner: FlashlightTool
        private static readonly int _EmissionColorID = Shader.PropertyToID("_EmissionColor");
        private static readonly int _ToolBatteryNormalizedID = Shader.PropertyToID("_ToolBatteryNormalized");
        private bool _powerIndicatorDirty;
        private bool _lateFrameRegistered;

        // ══════════════════════════════════════════════════════════
        //  IBatteryTool IMPLEMENTATION
        // ══════════════════════════════════════════════════════════

        /// <summary>True if the tool currently has a battery installed.</summary>
        public bool HasBattery => _installedBattery != null;

        /// <summary>Current battery charge level (0-1). Returns 0 if no battery.</summary>
        public float BatteryCharge => _installedBattery != null ? GetRuntimeBatteryNormalized(_batteryCharge) : 0f;

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
            _batteryCharge = 0f;

            SetRuntimeBatteryNormalized(0f);
            UpdateBatteryVisuals();
            QueuePowerIndicatorUpdate();

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
            _batteryCharge = math.saturate(charge);
            SetRuntimeBatteryNormalized(_batteryCharge);
            UpdateBatteryVisuals();
            QueuePowerIndicatorUpdate();

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
                flickerScalar = math.saturate(brownoutFlicker);

            _mpb.SetFloat(_ToolBatteryNormalizedID, math.saturate(batteryCharge));
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
        private uint _snapshotEvaluationStamp;
        private uint _cachedSnapshotStamp = uint.MaxValue;
        private string _cachedOperationalSummary;
        private string _cachedOperationalRecommendation;
        private uint _contextDirectiveEvaluationStamp;
        private uint _cachedContextDirectiveStamp = uint.MaxValue;
        private bool _cachedHasContextDirective;
        private string _cachedContextDirective;
        private Hecton8.Physics.QueryCacheContext _playerLookQueryCache;
        private FixedCharBuffer _assessmentHudBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] — flashlight assessment HUD staging buffer — owner: FlashlightTool

        // ══════════════════════════════════════════════════════════
        //  IBatteryTool STATE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _playerLookQueryCache = Hecton8.Physics.GlobalQueryCacheManager.PlayerLook;
            _mpb = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] — power indicator emission — owner: FlashlightTool
        }

        public override void OnEquip()
        {
            base.OnEquip();

            ResolveRuntimeReferences();
            _stateBeforeEquip = _flashlight != null && _flashlight.IsOn;
            _primaryLatched = false;
            _secondaryLatched = false;
            AdvanceEvaluationStamps();
            QueuePowerIndicatorUpdate();
            InvalidateSnapshotCache();
        }

        internal override float ResolveModularBatteryNormalized()
        {
            return _installedBattery != null ? BatteryCharge : 0f;
        }

        protected override void ConfigureModularRuntimeProfile(ref ToolRuntimeProfile profile)
        {
            profile.MaxRange = math.max(0.1f, contextProbeRange);
            profile.PowerScalar = 1f;
            profile.BatteryCapacity = 1f;
            profile.BatteryDrainPerSecond = Metadata != null ? math.max(0f, Metadata.energyConsumptionRate) : 0.02f;
        }

        public override void OnUnequip()
        {
            SyncFlashlightChargeMirrorFromCentral();
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
            SyncFlashlightChargeMirrorFromCentral();
            if (_flashlight != null)
                _flashlight.UnbindExternalBatteryTool(this);

            _flashlight = null;
            AdvanceEvaluationStamps();
            _powerIndicatorDirty = false;
            TryUnregisterLateFrameTick();
            base.OnDespawn();
        }

        protected override void OnToolRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            base.OnToolRegistryServiceReplaced(serviceSlot, previousService, currentService);
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher || currentService == null || !isActiveAndEnabled || !_lateFrameRegistered)
                return;

            TryUnregisterLateFrameTick();
            TryRegisterLateFrameTick();
        }

        private void SyncFlashlightChargeMirrorFromCentral()
        {
            _batteryCharge = _installedBattery != null ? BatteryCharge : 0f;
        }

        public override void UsePrimary(float deltaTime)
        {
            if (_primaryLatched)
                return;

            _primaryLatched = true;

            if (!TryResolveFlashlight())
                return;

            SyncCentralFlashlightState();

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
                    "Lamp thermal guard blocked activation; HUD assessment carries live cooling data.",
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
                LampAssessment assessment = BuildAssessment();
                FieldOperationLogSystem.RecordOperation(
                    "FLASHLIGHT",
                    "DIVE LAMP PROFILE",
                    "Beam profile cycled; HUD assessment carries live profile and directive data.",
                    "INFO");
                PublishAssessment(assessment);
                return;
            }

            LampAssessment status = BuildAssessment();
            FieldOperationLogSystem.RecordOperation(
                "FLASHLIGHT",
                "DIVE LAMP STATUS QUERY",
                "Status query archived; HUD assessment carries live lamp and context data.",
                status.Severity);
            PublishAssessment(status);
        }

        public override void ToolTick(float deltaTime)
        {
            QueuePowerIndicatorUpdate();

            PlayerInputState inputState = TryGetInputService(out IInputService inputService) && inputService.IsPlayerInputEnabled
                ? inputService.GetState()
                : default;

            if (!inputState.HasAction(PlayerInputAction.PrimaryFire))
                _primaryLatched = false;

            if (!inputState.HasAction(PlayerInputAction.SecondaryFire))
                _secondaryLatched = false;

            if (!TryResolveFlashlight())
                return;

            SyncCentralFlashlightState();

            if (_flashlight.IsOn && !HasToolEnergyOrWirelessPath())
            {
                _flashlight.TurnOff();
                QueuePowerIndicatorUpdate();
                AdvanceEvaluationStamps();
                InvalidateSnapshotCache();
                return;
            }

            if (_flashlight.IsOn)
            {
                bool hadEnergy = MarkFlashlightActiveForCentralSolver();
                QueuePowerIndicatorUpdate();
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
            else
            {
                MarkFlashlightInactiveForCentralSolver();
            }

            AdvanceEvaluationStamps();
        }

        public void LateFrameTick()
        {
            if (_powerIndicatorDirty)
            {
                _powerIndicatorDirty = false;
                UpdatePowerIndicator();
            }

            if (!IsEquipped && !_powerIndicatorDirty)
                TryUnregisterLateFrameTick();
        }

        private void QueuePowerIndicatorUpdate()
        {
            _powerIndicatorDirty = true;
            TryRegisterLateFrameTick();
        }

        private void TryRegisterLateFrameTick()
        {
            if (_lateFrameRegistered || !Application.isPlaying)
                return;

            _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterLateFrameTick()
        {
            if (!_lateFrameRegistered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _lateFrameRegistered = false;
        }

        private void AdvanceEvaluationStamps()
        {
            unchecked
            {
                _snapshotEvaluationStamp++;
                _contextDirectiveEvaluationStamp++;
            }
        }

        private void SyncCentralFlashlightState()
        {
            if (!TryGetModularEquipment(out IModularEquipmentService service) || RuntimeToolId == 0u)
                return;

            if (!service.TryGetToolState(RuntimeToolId, out ToolState state))
                return;

            _flashlight.ApplyCentralThermalState(
                math.saturate(state.InternalHeat),
                (state.StatusMask & ToolRuntimeStatusMasks.Overheated) != 0u);
        }

        private bool MarkFlashlightActiveForCentralSolver()
        {
            if (!TryGetModularEquipment(out IModularEquipmentService service) || RuntimeToolId == 0u)
                return HasToolEnergyOrWirelessPath();

            service.SetToolActive(RuntimeToolId, true);
            if (!service.TryGetToolState(RuntimeToolId, out ToolState state))
                return HasToolEnergyOrWirelessPath();

            _flashlight.ApplyCentralThermalState(
                math.saturate(state.InternalHeat),
                (state.StatusMask & ToolRuntimeStatusMasks.Overheated) != 0u);
            uint hardBlock = ToolRuntimeStatusMasks.Overheated | ToolRuntimeStatusMasks.Broken | ToolRuntimeStatusMasks.DepthFailed;
            if ((state.StatusMask & hardBlock) != 0u)
                return false;

            return (state.StatusMask & ToolRuntimeStatusMasks.Disabled) == 0u || HasToolEnergyOrWirelessPath();
        }

        private void MarkFlashlightInactiveForCentralSolver()
        {
            if (TryGetModularEquipment(out IModularEquipmentService service) && RuntimeToolId != 0u)
                service.SetToolActive(RuntimeToolId, false);
        }

        private void ResolveRuntimeReferences()
        {
            if (_flashlight == null &&
                TryGetPlayerRuntimeContext(out IPlayerRuntimeContext playerContext))
            {
                _flashlight = playerContext.Flashlight;
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[FlashlightTool] No PlayerFlashlight found in scene.");
#endif
                _missingFlashlightWarned = true;
            }

            return false;
        }

        public override string BuildLegacyOperationalSummaryString()
        {
            if (!TryResolveFlashlight())
                return "DIVE LAMP // LINK OFFLINE";

            if (TryGetOperationalSnapshot(out string summary, out _))
                return summary;

            return _flashlight.BuildOperationalSummary();
        }

        public override void WriteOperationalSummary(ref FixedCharBuffer buffer)
        {
            if (!TryResolveFlashlight())
            {
                AppendText(ref buffer, "DIVE LAMP // LINK OFFLINE");
                return;
            }

            _flashlight.WriteOperationalSummary(ref buffer);
        }

        public override string BuildLegacyOperationalDirectiveString()
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

        public override void WriteOperationalDirective(ref FixedCharBuffer buffer)
        {
            if (!TryResolveFlashlight())
            {
                AppendText(ref buffer, "Restore the lamp link before field deployment.");
                return;
            }

            if (_cachedContextDirectiveStamp == _contextDirectiveEvaluationStamp &&
                _cachedHasContextDirective &&
                !string.IsNullOrEmpty(_cachedContextDirective))
            {
                AppendText(ref buffer, _cachedContextDirective);
                return;
            }

            _flashlight.WriteOperationalRecommendation(ref buffer);
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
                    "DIVE LAMP - COOLING",
                    summary,
                    recommendation,
                    "WARN",
                    (int)math.ceil(_flashlight.CooldownRemaining),
                    null,
                    LampAssessment.WithCooldown);
            }

            if (_flashlight.EnergyPercent <= 10f)
            {
                return new LampAssessment(
                    "DIVE LAMP - LOW ENERGY",
                    summary,
                    recommendation,
                    "WARN",
                    0,
                    _flashlight.BeamModeLabel,
                    LampAssessment.WithBeamMode);
            }

            if (_flashlight.HeatLevel >= 0.7f)
            {
                return new LampAssessment(
                    "DIVE LAMP - HEAT RISING",
                    summary,
                    recommendation,
                    "WARN",
                    0,
                    _flashlight.BeamModeLabel,
                    LampAssessment.WithBeamMode);
            }

            string contextualRecommendation = TryGetForwardContextDirectiveCached(out string contextDirective)
                ? contextDirective
                : recommendation;

            return new LampAssessment(
                _flashlight.IsOn
                    ? "DIVE LAMP - ON"
                    : "DIVE LAMP - STANDBY",
                summary,
                contextualRecommendation,
                "INFO",
                0,
                _flashlight.BeamModeLabel,
                LampAssessment.WithBeamMode);
        }

        private bool TryGetOperationalSnapshot(out string summary, out string recommendation)
        {
            summary = null;
            recommendation = null;

            if (_flashlight == null)
                return false;

            uint currentStamp = _snapshotEvaluationStamp;
            if (_cachedSnapshotStamp == currentStamp)
            {
                summary = _cachedOperationalSummary;
                recommendation = _cachedOperationalRecommendation;
                return true;
            }

            summary = _flashlight.BuildOperationalSummary();
            recommendation = _flashlight.BuildOperationalRecommendation();
            _cachedSnapshotStamp = currentStamp;
            _cachedOperationalSummary = summary;
            _cachedOperationalRecommendation = recommendation;
            return true;
        }

        private bool TryGetForwardContextDirectiveCached(out string contextDirective)
        {
            contextDirective = null;

            if (_flashlight == null)
                return false;

            uint currentStamp = _contextDirectiveEvaluationStamp;
            if (_cachedContextDirectiveStamp == currentStamp)
            {
                contextDirective = _cachedContextDirective;
                return _cachedHasContextDirective;
            }

            bool hasDirective = TryGetForwardContextDirective(out contextDirective);
            _cachedContextDirectiveStamp = currentStamp;
            _cachedHasContextDirective = hasDirective;
            _cachedContextDirective = contextDirective;
            return hasDirective;
        }

        private void InvalidateSnapshotCache()
        {
            _cachedSnapshotStamp = uint.MaxValue;
            _cachedOperationalSummary = null;
            _cachedOperationalRecommendation = null;
            _cachedContextDirectiveStamp = uint.MaxValue;
            _cachedHasContextDirective = false;
            _cachedContextDirective = null;
        }

        private bool TryGetForwardContextDirective(out string directive)
        {
            directive = null;

            if (_flashlight == null ||
                !TryResolveContextRay(out Vector3 origin, out Vector3 direction))
                return false;

            Hecton8.Physics.QueryCacheContext cache =
                _playerLookQueryCache ?? Hecton8.Physics.GlobalQueryCacheManager.PlayerLook;
            _playerLookQueryCache = cache;
            Ray ray = new Ray(origin, direction);
            
            const QueryTriggerInteraction triggerMode = QueryTriggerInteraction.Collide;
            if (!cache.TryGet(ray, contextProbeRange, contextMask, triggerMode, out Hecton8.Physics.QueryResult qResult))
            {
                if (!TryResolvePrimarySurfaceHit(ray.origin, ray.direction, contextProbeRange, contextMask.value, triggerMode, out InteractionSurfaceHit hit))
                    return false;
                qResult = new Hecton8.Physics.QueryResult { hasHit = true, hit = hit };
                cache.Set(ray, contextProbeRange, contextMask, triggerMode, qResult);
            }

            if (!qResult.hasHit) 
                return false;

            InteractionSurfaceHit finalHit = qResult.hit;

            Collider collider = finalHit.collider;
            if (collider == null)
                return false;

            if (FieldTargetDescriptor.TryResolve(collider, out FieldTargetDescriptor descriptor))
            {
                if (FieldTargetSemantics.TryBuildFlashlightDirective(descriptor, finalHit.distance, out directive))
                    return true;
            }

            if (InteractableRegistry.TryResolve(collider, out InteractableRegistry.TargetInfo targetInfo) &&
                TryBuildCachedContextDirective(in targetInfo, finalHit.distance, out directive))
            {
                return true;
            }

            return TryBuildDistanceContextDirective(finalHit.distance, out directive);
        }

        private bool TryResolveContextRay(out Vector3 origin, out Vector3 direction)
        {
            origin = default;
            direction = default;
            if (!TryGetPlayerRuntimeContext(out IPlayerRuntimeContext playerContext) ||
                !playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
            {
                return false;
            }

            float3 runtimePosition = snapshot.RuntimePosition;
            float3 forward = snapshot.Forward;
            float forwardLengthSq = math.lengthsq(forward);
            if (!math.all(math.isfinite(runtimePosition)) ||
                !math.all(math.isfinite(forward)) ||
                !math.isfinite(forwardLengthSq) ||
                forwardLengthSq <= 0.0001f)
            {
                return false;
            }

            float invForwardLength = math.rsqrt(math.max(forwardLengthSq, 0.0001f));
            origin = new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            direction = new Vector3(
                forward.x * invForwardLength,
                forward.y * invForwardLength,
                forward.z * invForwardLength);
            return true;
        }

        private static bool TryBuildCachedContextDirective(in InteractableRegistry.TargetInfo targetInfo, float distance, out string directive)
        {
            if (targetInfo.Scannable != null)
            {
                directive = distance >= 10f
                    ? "Use FOCUS to read distant probes and hazard points before closing in."
                    : "Use STANDARD while you classify the probe and keep route awareness.";
                return true;
            }

            if (targetInfo.Pickup != null || targetInfo.PickupSource != null)
            {
                directive = distance <= 5f
                    ? "Use FLOOD to sweep the nearby salvage pocket without overshooting the pickup."
                    : "Use STANDARD until the pickup lane tightens, then widen to FLOOD.";
                return true;
            }

            if (targetInfo.ResourceNode != null)
            {
                directive = distance >= 9f
                    ? "Use FOCUS to probe the node edge before committing cutter or sampler."
                    : "Use STANDARD to hold visibility on the extraction face.";
                return true;
            }

            if (targetInfo.BaseModule != null)
            {
                directive = distance >= 9f
                    ? "Use FOCUS for distant module reads and service planning."
                    : "Use STANDARD to maintain service visibility on the module face.";
                return true;
            }

            directive = null;
            return false;
        }

        private static bool TryBuildDistanceContextDirective(float distance, out string directive)
        {
            if (distance >= 10f)
            {
                directive = "Use FOCUS for distant reads before closing the route.";
                return true;
            }

            if (distance <= 5f)
            {
                directive = "Use FLOOD to widen near-field visibility without oversteering.";
                return true;
            }

            directive = "Use STANDARD to preserve route awareness and battery discipline.";
            return true;
        }

        private void PublishAssessment(LampAssessment assessment)
        {
            _assessmentHudBuffer.Clear();
            if (!assessment.TryWriteHudMessage(ref _assessmentHudBuffer))
                return;

            if (_hudNotification != null)
            {
                if (assessment.Severity == "WARN" || assessment.Severity == "CRITICAL")
                    _hudNotification.ShowWarning(in _assessmentHudBuffer);
                else
                    _hudNotification.ShowInfo(in _assessmentHudBuffer);
                return;
            }

            return;
        }

        private static bool AppendText(ref FixedCharBuffer buffer, string value)
        {
            return string.IsNullOrEmpty(value) || buffer.Append(value);
        }

        // ══════════════════════════════════════════════════════════
        //  IBatteryTool IMPLEMENTATION
        // ══════════════════════════════════════════════════════════

    }
}

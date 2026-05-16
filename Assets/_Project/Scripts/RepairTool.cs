// ============================================================================
// HECTON-8 — RepairTool.cs
// Remontnyy instrument igroka.
//
// NASLEDOVANIE:
//   PlayerTool → RepairTool
//
// LOGIKA:
//   • UsePrimary(dt):
//       1. Puskaet Raycast vpered.
//       2. Esli popal v BaseModule — vyzyvaet Repair(repairSpeed * dt).
//       3. Vklyuchaet vizual svarki / iskry / Bloom-friendly light.
//   • ToolTick(dt):
//       Otklyuchaet vizual, esli v kadre instrument ne ispolzovalsya.
//
// VIZUAL:
//   • sparksVFX         — iskry.
//   • repairLine        — LineRenderer lucha/dugi.
//   • weldLight         — yarkiy point light dlya Bloom v shleme.
//
// ZERO GC:
//   • RaycastHit — struct.
//   • TryGetComponent — zero GC.
//   • Net Update().
// ============================================================================

using System;
using System.Collections.Generic;
using Hecton.Localization;
using Hecton8.Audio;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Hecton8.Items;
using Hecton8.Physics;
using Hecton8.Tools;
using Hecton8.UI;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class RepairTool : PlayerTool, IBatteryTool
    {
        private const string RepairToolNoPowerHeadline = "NO POWER";
        private const string RepairToolDrainingHeadline = "DRAINING";
        private const string RepairToolFloodedHeadline = "FLOODED";
        private const string RepairToolSealedHeadline = "SEALED";
        private const string RepairToolCriticalDamageHeadline = "CRITICAL DAMAGE";
        private const string RepairToolHeavyDamageHeadline = "HEAVY DAMAGE";
        private const string RepairToolPatchingHeadline = "PATCHING";
        private const string RepairToolCategory = "REPAIR";
        private const string RepairToolModuleLabel = "BASE MODULE";
        private const uint RepairSparksSignalHash = 0x44525350u;
        private const uint HullRepairSourceHash = 0x574C4452u; // WLDR
        private const uint HullRepairTelemetryHash = 0x48445250u; // HDRP
        private const byte RepairSparkDebrisKind = 1;
        private const int HullDentVaultCapacity = 16;
        private const int HullDentRadiusQuantizationStepsPerMeter = 16;
        private const float InvHullDentRadiusQuantizationStepsPerMeter = 1f / HullDentRadiusQuantizationStepsPerMeter;
        private const float InvHullDentDepthQuantizationSteps = 1f / 255f;
        private const float HullDentRepairRadiusMeters = 2f;
        private const float HullDentRepairRadiusSq = HullDentRepairRadiusMeters * HullDentRepairRadiusMeters;
        private const float HullDentRepairDepthScale = 0.01f;
        private const float MinimumStoredHullDentDepthMeters = 0.001f;
        private const float HullRepairEpsilon = 0.0001f;
        private static readonly char[] s_integrityDiagnosticPrefixChars = "INTEGRITY ".ToCharArray();
        private static FixedCharBuffer s_hudBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] - repair tool HUD staging buffer - owner: RepairTool

        private struct ServiceDiagnosis
        {
            public string status;
            public string headline;
            public string summary;
            public string summaryKey;
            public string summaryFallback;
            public string recommendation;
            public string severity;
            public string priority;
            public int integrityPercent;
            public bool hasIntegrityPercent;
        }

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Repair Settings ───────────────────────────")]
        [Tooltip("Maksimalnaya dalnost remonta.")]
        [SerializeField] private float repairRange = 4f;

        [Tooltip("Skorost remonta (edinits tselostnosti v sekundu).")]
        [SerializeField] private float repairSpeed = 20f;

        [Tooltip("Sloi, po kotorym rabotaet remontnyy luch.")]
        [SerializeField] private LayerMask repairMask = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;

        [Header("── Visuals ───────────────────────────────────")]
        [SerializeField] private LineRenderer repairLine;
        [SerializeField] private ParticleSystem sparksVFX;
        [SerializeField] private Light weldLight;
        [SerializeField] private AudioSource repairLoopAudio;

        [Header("── Battery ───────────────────────────────────")]
        [Tooltip("Optional battery item type accepted by the repair tool.")]

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        private Transform _cachedTransform;
        private RaycastHit _hit;
        private bool _isRepairing;
        private bool _wasRepairingLastFrame;
        private bool _invalidTargetReportedThisUse;
        private bool _noTargetReportedThisUse;
        private bool _healthyTargetReportedThisUse;
        private bool _activeRepairReportedThisUse;
        private bool _secondaryLatched;
        private int _cachedDiagnosisFrame = -1;
        private bool _cachedDiagnosisValid;
        private Collider _cachedServiceTargetCollider;
        private BaseModule _cachedServiceTargetModule;
        private HectonVoxelVolume _cachedServiceTargetVoxelVolume;
        private Collider _cachedAirlockTargetCollider;
        private BaseAirlock _cachedServiceTargetAirlock;
        private Collider _cachedSubmarineDamageTargetCollider;
        private ISubmarineDamageControlTarget _cachedSubmarineDamageTarget;
        private ISubmarineRepairRoomResolver _cachedSubmarineRepairRoomResolver;
        private Transform _cachedSubmarineDamageTargetTransform;
        private IDataVault _dataVault;
        private VaultBufferHandle<float4> _hullDentsHandle;
        private readonly List<MonoBehaviour> _submarineDamageTargetSearchBuffer = new List<MonoBehaviour>(16); // COLD ALLOC: List<MonoBehaviour>(16) - interface lookup scratch for submarine damage-control targets - owner: RepairTool
        private readonly char[] _integrityDiagnosticBuffer = new char[24]; // COLD ALLOC: char[24] — repair-tool floating integrity diagnostic buffer — owner: RepairTool

        // ══════════════════════════════════════════════════════════
        //  IBatteryTool STATE
        // ══════════════════════════════════════════════════════════

        [Header("── Battery Settings ─────────────────────────")]
        [Tooltip("Battery item type this tool uses.")]
        [SerializeField] private ItemData _batteryItemType;

        [Header("── Battery Visuals ──────────────────────────")]
        [Tooltip("Mesh to hide when battery is removed.")]
        [SerializeField] private GameObject _batteryMesh;

        [Tooltip("Renderer for power indicator light.")]
        [SerializeField] private Renderer _powerIndicatorRenderer;

        [Tooltip("Emission color when powered.")]
        [SerializeField] private Color _powerOnColor = new Color(0f, 0.9f, 1f);

        private ItemData _installedBattery;
        private float _batteryCharge;
        private ServiceDiagnosis _cachedDiagnosis;

        // MaterialPropertyBlock for power indicator
        private MaterialPropertyBlock _mpb; // COLD ALLOC: MaterialPropertyBlock[1] — power indicator emission — owner: RepairTool
        private static readonly int _EmissionColorID = Shader.PropertyToID("_EmissionColor");

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
            _batteryCharge = math.saturate(charge);
            SetRuntimeBatteryNormalized(_batteryCharge);

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
            float currentCharge = BatteryCharge;
            float flickerScalar = 1f;
            if (TryGetToolBrownoutFlicker(out float brownoutFlicker))
                flickerScalar = math.saturate(brownoutFlicker);

            if (_installedBattery == null || currentCharge <= 0f)
            {
                _mpb.SetColor(_EmissionColorID, Color.black);
            }
            else if (currentCharge <= 0.2f)
            {
                _mpb.SetColor(_EmissionColorID, new Color(1f, 0.3f, 0f) * flickerScalar);
            }
            else
            {
                _mpb.SetColor(_EmissionColorID, _powerOnColor * flickerScalar);
            }

            _powerIndicatorRenderer.SetPropertyBlock(_mpb);
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] — power indicator emission — owner: RepairTool
            _cachedTransform = transform;
            TryAssignRepairAudioMixerRoute();
            SetRepairVisuals(false);
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            TryAssignRepairAudioMixerRoute();
            _isRepairing = false;
            _wasRepairingLastFrame = false;
            _invalidTargetReportedThisUse = false;
            _noTargetReportedThisUse = false;
            _healthyTargetReportedThisUse = false;
            _activeRepairReportedThisUse = false;
            _secondaryLatched = false;
            SetRepairVisuals(false);
        }

        public override void OnDespawn()
        {
            _isRepairing = false;
            _wasRepairingLastFrame = false;
            _invalidTargetReportedThisUse = false;
            _noTargetReportedThisUse = false;
            _healthyTargetReportedThisUse = false;
            _activeRepairReportedThisUse = false;
            _secondaryLatched = false;
            SetRepairVisuals(false);
            ClearDiagnosticLaserTelemetry();
            ReleaseEquippedAudio();
            base.OnDespawn();
        }

        public override void OnEquip()
        {
            base.OnEquip();
            TryAssignRepairAudioMixerRoute();
            _isRepairing = false;
            _wasRepairingLastFrame = false;
            _invalidTargetReportedThisUse = false;
            _noTargetReportedThisUse = false;
            _healthyTargetReportedThisUse = false;
            _activeRepairReportedThisUse = false;
            _secondaryLatched = false;
            SetRepairVisuals(false);
            ClearDiagnosticLaserTelemetry();
            UpdatePowerIndicator();
            PrewarmEquippedAudio();
        }

        public override void OnUnequip()
        {
            _isRepairing = false;
            _wasRepairingLastFrame = false;
            _invalidTargetReportedThisUse = false;
            _noTargetReportedThisUse = false;
            _healthyTargetReportedThisUse = false;
            _activeRepairReportedThisUse = false;
            _secondaryLatched = false;
            SetRepairVisuals(false);
            ClearDiagnosticLaserTelemetry();
            ReleaseEquippedAudio();
            base.OnUnequip();
        }

        internal override float ResolveModularBatteryNormalized()
        {
            return _installedBattery != null ? math.saturate(_batteryCharge) : 0f;
        }

        private float ResolveRuntimeRepairRange()
        {
            return GetRuntimeMaxRange(repairRange);
        }

        private void TryAssignRepairAudioMixerRoute()
        {
            if (repairLoopAudio == null || repairLoopAudio.outputAudioMixerGroup != null)
                return;

            if (GlobalRegistry.Audio is SpatialAudioManager spatialAudioManager)
                repairLoopAudio.outputAudioMixerGroup = spatialAudioManager.SfxGroup;
        }

        private void PrewarmEquippedAudio()
        {
            AudioResidencyCache.PrewarmAudioSource(repairLoopAudio, AudioResidencyDomain.Player);
        }

        private void ReleaseEquippedAudio()
        {
            AudioResidencyCache.ReleaseAudioSource(repairLoopAudio);
        }

        private float ResolveRuntimeRepairPowerPerSecond()
        {
            float runtimePower = GetRuntimePowerScalar(1f);
            if (!math.isfinite(runtimePower))
                runtimePower = 1f;

            return FiniteNonNegativeOrZero(repairSpeed) * math.max(0.1f, runtimePower);
        }

        private float ResolveRuntimeRepairPowerNormalized()
        {
            float runtimePower = GetRuntimePowerScalar(1f);
            return math.isfinite(runtimePower) ? math.saturate(runtimePower) : 0f;
        }

        // ══════════════════════════════════════════════════════════
        //  TOOL ACTIONS
        // ══════════════════════════════════════════════════════════

        public override void UsePrimary(float deltaTime)
        {
            if (!TryBeginToolUse(deltaTime, true))
            {
                _isRepairing = false;
                if (!_noTargetReportedThisUse)
                {
                    PublishWarningMessage(ResolveLocalized(LocalizationKeys.REPAIR_TOOL_HUD_NO_POWER, "REPAIR TOOL - NO POWER"));
                    _noTargetReportedThisUse = true;
                }

                UpdateBeamMiss();
                InvalidateDiagnosisCache();
                return;
            }

            _isRepairing = true;
            bool didHit = TryGetRepairHit(out _hit);

            if (!didHit)
            {
                if (!_noTargetReportedThisUse)
                {
                    PublishWarningMessage(ResolveLocalized(LocalizationKeys.REPAIR_TOOL_HUD_NO_TARGET, "REPAIR TOOL - NO TARGET"));
                    _noTargetReportedThisUse = true;
                }
                UpdateBeamMiss();
                InvalidateDiagnosisCache();
                return;
            }

            BaseAirlock airlock = ResolveRepairAirlock(_hit.collider);
            if (airlock != null && airlock.TryApplyWeldOverride(deltaTime, _hit.point))
            {
                UpdateBeamHit(_hit.point, _hit.normal);
                ClearIntegrityDiagnostic();
                InvalidateDiagnosisCache();
                return;
            }

            if (TryHandleSubmarineDamageControlHit(deltaTime))
            {
                ClearIntegrityDiagnostic();
                InvalidateDiagnosisCache();
                return;
            }

            ResolveRepairTargets(_hit.collider, out BaseModule module, out HectonVoxelVolume voxelVolume);
            if (module != null)
            {
                float beforeIntegrity = module.CurrentIntegrity;
                bool beforeFlooded = module.IsFlooded;

                if (beforeIntegrity >= module.MaxIntegrity && !beforeFlooded)
                {
                    if (!_healthyTargetReportedThisUse)
                    {
                        PublishInfoMessage(ResolveLocalized(LocalizationKeys.REPAIR_TOOL_HUD_SEALED, "REPAIR TOOL - MODULE SEALED"));
                        _healthyTargetReportedThisUse = true;
                    }

                    UpdateBeamHit(_hit.point, _hit.normal);
                    PublishIntegrityDiagnostic(module, _hit.point, _hit.normal);
                    InvalidateDiagnosisCache();
                    return;
                }

                float repairAmount = ResolveRuntimeRepairPowerPerSecond() * deltaTime;
                ToolEffectEvents.RaiseEffectApplied(
                    EffectType.Weld,
                    module,
                    _cachedTransform,
                    repairAmount,
                    _hit.point);
                module.Repair(repairAmount);
                UpdateBeamHit(_hit.point, _hit.normal);
                PublishIntegrityDiagnostic(module, _hit.point, _hit.normal);

                if (!_activeRepairReportedThisUse)
                {
                    ServiceDiagnosis diagnosis = BuildDiagnosis(module);
                    PublishActiveRepairInfo(diagnosis.headline);
                    s_hudBuffer.Clear();
                    if (TryWriteRepairStartedLogSummary(ref s_hudBuffer, diagnosis))
                    {
                        FieldOperationLogSystem.RecordOperation(
                            ResolveLocalized(LocalizationKeys.REPAIR_TOOL_CATEGORY, RepairToolCategory),
                            ResolveLocalized(LocalizationKeys.REPAIR_TOOL_LOG_STARTED_TITLE, "MODULE REPAIR STARTED"),
                            in s_hudBuffer,
                            "INFO");
                    }

                    _activeRepairReportedThisUse = true;
                }

                if ((beforeIntegrity < module.MaxIntegrity || beforeFlooded) &&
                    module.CurrentIntegrity >= module.MaxIntegrity &&
                    !module.IsFlooded)
                {
                    PublishInfoMessage(ResolveLocalized(LocalizationKeys.REPAIR_TOOL_HUD_RESTORED, "REPAIR TOOL - MODULE RESTORED"));
                    s_hudBuffer.Clear();
                    if (TryWriteRepairRestoredLogSummary(ref s_hudBuffer))
                    {
                        FieldOperationLogSystem.RecordOperation(
                            ResolveLocalized(LocalizationKeys.REPAIR_TOOL_CATEGORY, RepairToolCategory),
                            ResolveLocalized(LocalizationKeys.REPAIR_TOOL_LOG_RESTORED_TITLE, "MODULE RESTORED"),
                            in s_hudBuffer,
                            "INFO");
                    }
                }
            }
            else
            {
                if (voxelVolume != null)
                {
                    double3 absoluteHitPoint = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(_hit.point);
                    if (voxelVolume.ApplyRepairWeldDda(
                        absoluteHitPoint,
                        _cachedTransform.forward,
                        ResolveRuntimeRepairPowerNormalized(),
                        ResolveRuntimeRepairRange()))
                    {
                        UpdateBeamHit(_hit.point, _hit.normal);
                        ClearIntegrityDiagnostic();
                        QueueToolHapticFeedback(ResolveRuntimeRepairPowerPerSecond(), math.max(1f, repairSpeed));
                        InvalidateDiagnosisCache();
                        return;
                    }
                }

                if (!_invalidTargetReportedThisUse)
                {
                    PublishWarningMessage(ResolveLocalized(LocalizationKeys.REPAIR_TOOL_HUD_INVALID_TARGET, "REPAIR TOOL - INVALID TARGET"));
                    _invalidTargetReportedThisUse = true;
                }
                UpdateBeamMiss();
                ClearIntegrityDiagnostic();
            }

            InvalidateDiagnosisCache();
        }

        public override void UseSecondary(float deltaTime)
        {
            if (_secondaryLatched)
                return;

            _secondaryLatched = true;

            bool didHit = TryGetRepairHit(out _hit);

            if (!didHit)
            {
                PublishWarningMessage(ResolveLocalized(LocalizationKeys.REPAIR_TOOL_HUD_NO_MODULE, "REPAIR TOOL - NO MODULE IN RANGE"));
                InvalidateDiagnosisCache();
                return;
            }

            ResolveRepairTargets(_hit.collider, out BaseModule module, out _);
            if (module == null)
            {
                PublishWarningMessage(ResolveLocalized(LocalizationKeys.REPAIR_TOOL_HUD_NOT_SERVICEABLE, "REPAIR TOOL - TARGET NOT SERVICEABLE"));
                InvalidateDiagnosisCache();
                return;
            }

            ServiceDiagnosis diagnosis = BuildDiagnosis(module);
            PublishDiagnosis(diagnosis);
            string logTitle = GetServiceDiagnosisLogTitle(diagnosis.headline);
            s_hudBuffer.Clear();
            if (TryWriteDiagnosisLogSummary(ref s_hudBuffer, diagnosis))
            {
                FieldOperationLogSystem.RecordOperation(
                    ResolveLocalized(LocalizationKeys.REPAIR_TOOL_CATEGORY, RepairToolCategory),
                    logTitle,
                    in s_hudBuffer,
                    diagnosis.severity);
            }

            InvalidateDiagnosisCache();
        }

        public override void ToolTick(float deltaTime)
        {
            if (_wasRepairingLastFrame && !_isRepairing)
                SetRepairVisuals(false);

            if (_powerIndicatorRenderer != null)
                UpdatePowerIndicator();

            _wasRepairingLastFrame = _isRepairing;
            bool repairingThisFrame = _isRepairing;
            _isRepairing = false;

            IInputService inputService = GlobalRegistry.Input;
            PlayerInputState inputState = inputService != null && inputService.IsPlayerInputEnabled
                ? inputService.GetState()
                : default;

            if (!inputState.HasAction(PlayerInputAction.PrimaryFire))
            {
                _invalidTargetReportedThisUse = false;
                _noTargetReportedThisUse = false;
                _healthyTargetReportedThisUse = false;
                _activeRepairReportedThisUse = false;
            }

            if (!inputState.HasAction(PlayerInputAction.SecondaryFire))
                _secondaryLatched = false;

            if (!IsEquipped)
            {
                ClearDiagnosticLaserTelemetry();
                return;
            }

            if (!repairingThisFrame)
                UpdateDiagnosticLaserPreview();
        }

        public override string GetOperationalSummary()
        {
            s_hudBuffer.Clear();
            WriteOperationalSummary(ref s_hudBuffer);
            return CreateLegacyString(in s_hudBuffer);
        }

        public override void WriteOperationalSummary(ref FixedCharBuffer buffer)
        {
            if (_isRepairing)
            {
                AppendText(ref buffer, ResolveLocalized(LocalizationKeys.REPAIR_TOOL_OPERATIONAL_ACTIVE, "REPAIR TOOL // ACTIVE SERVICE"));
                return;
            }

            if (TryGetServiceDiagnosisCached(out ServiceDiagnosis diagnosis))
            {
                AppendText(ref buffer, "REPAIR TOOL // ");
                AppendText(ref buffer, diagnosis.priority);
                return;
            }

            AppendText(ref buffer, ResolveLocalized(LocalizationKeys.REPAIR_TOOL_OPERATIONAL_STANDBY, "REPAIR TOOL // STANDBY"));
        }

        public override string GetOperationalDirective()
        {
            s_hudBuffer.Clear();
            WriteOperationalDirective(ref s_hudBuffer);
            return CreateLegacyString(in s_hudBuffer);
        }

        public override void WriteOperationalDirective(ref FixedCharBuffer buffer)
        {
            if (_isRepairing)
            {
                AppendText(
                    ref buffer,
                    ResolveLocalized(
                        LocalizationKeys.REPAIR_TOOL_OPERATIONAL_ACTIVE_DIRECTIVE,
                        "Hold the beam steady until the service window closes."));
                return;
            }

            if (TryGetServiceDiagnosisCached(out ServiceDiagnosis diagnosis))
            {
                AppendText(ref buffer, diagnosis.recommendation);
                return;
            }

            AppendText(
                ref buffer,
                ResolveLocalized(
                    LocalizationKeys.REPAIR_TOOL_OPERATIONAL_STANDBY_DIRECTIVE,
                    "Sweep a damaged module to diagnose or begin repair."));
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — VISUAL STATE
        // ══════════════════════════════════════════════════════════

        private void UpdateBeamHit(Vector3 hitPoint, Vector3 hitNormal)
        {
            SetRepairVisuals(true);
            Vector3 safeNormal = ResolveFiniteDirection(hitNormal, _cachedTransform != null ? _cachedTransform.forward : Vector3.forward);

            if (repairLine != null)
            {
                if (!repairLine.enabled)
                    repairLine.enabled = true;

                repairLine.SetPosition(0, Vector3.zero);
                repairLine.SetPosition(
                    1,
                    TryResolveToolLocalPointAup(hitPoint, out Vector3 localHitPoint)
                        ? localHitPoint
                        : Vector3.forward * ResolveRuntimeRepairRange());
            }

            if (sparksVFX != null)
            {
                Transform t = sparksVFX.transform;
                t.position = hitPoint;
                t.rotation = Quaternion.LookRotation(safeNormal);
            }

            if (weldLight != null)
            {
                weldLight.transform.position = hitPoint - safeNormal * 0.05f;
            }

            if (repairLoopAudio != null && !repairLoopAudio.isPlaying)
            {
                TryAssignRepairAudioMixerRoute();
                repairLoopAudio.Play();
            }
        }

        private void UpdateBeamMiss()
        {
            SetRepairVisuals(true);

            if (repairLine != null)
            {
                if (!repairLine.enabled)
                    repairLine.enabled = true;

                repairLine.SetPosition(0, Vector3.zero);
                repairLine.SetPosition(1, Vector3.forward * ResolveRuntimeRepairRange());
            }

            if (sparksVFX != null && sparksVFX.isPlaying)
            {
                sparksVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            if (weldLight != null)
            {
                weldLight.transform.position = _cachedTransform.position + _cachedTransform.forward * ResolveRuntimeRepairRange();
            }

            if (repairLoopAudio != null && !repairLoopAudio.isPlaying)
            {
                TryAssignRepairAudioMixerRoute();
                repairLoopAudio.Play();
            }
        }

        private void SetRepairVisuals(bool active)
        {
            if (repairLine != null)
                repairLine.enabled = active;

            if (weldLight != null)
                weldLight.enabled = active;

            if (!active)
            {
                if (sparksVFX != null && sparksVFX.isPlaying)
                    sparksVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                if (repairLoopAudio != null && repairLoopAudio.isPlaying)
                    repairLoopAudio.Stop();
            }
        }

        private void UpdateDiagnosticLaserPreview()
        {
            if (!TryGetRepairHit(out _hit))
            {
                if (repairLine != null)
                    repairLine.enabled = false;
                ClearIntegrityDiagnostic();
                return;
            }

            if (repairLine != null)
            {
                repairLine.enabled = true;
                repairLine.SetPosition(0, Vector3.zero);
                repairLine.SetPosition(
                    1,
                    TryResolveToolLocalPointAup(_hit.point, out Vector3 localHitPoint)
                        ? localHitPoint
                        : Vector3.forward * ResolveRuntimeRepairRange());
            }

            ResolveRepairTargets(_hit.collider, out BaseModule module, out _);
            if (module != null)
                PublishIntegrityDiagnostic(module, _hit.point, _hit.normal);
            else
                ClearIntegrityDiagnostic();
        }

        private void ClearDiagnosticLaserTelemetry()
        {
            if (repairLine != null && !_isRepairing)
                repairLine.enabled = false;
            ClearIntegrityDiagnostic();
        }

        private void ResolveRepairTargets(Collider collider, out BaseModule module, out HectonVoxelVolume voxelVolume)
        {
            module = null;
            voxelVolume = null;
            if (collider == null)
                return;

            if (!ReferenceEquals(_cachedServiceTargetCollider, collider))
            {
                _cachedServiceTargetCollider = collider;
                _cachedServiceTargetModule = null;
                _cachedServiceTargetVoxelVolume = null;

                if (!collider.TryGetComponent(out _cachedServiceTargetModule))
                    _cachedServiceTargetModule = collider.GetComponentInParent<BaseModule>();

                if (_cachedServiceTargetModule == null)
                {
                    if (!collider.TryGetComponent(out _cachedServiceTargetVoxelVolume))
                        _cachedServiceTargetVoxelVolume = collider.GetComponentInParent<HectonVoxelVolume>();
                }
            }

            module = _cachedServiceTargetModule;
            voxelVolume = _cachedServiceTargetVoxelVolume;
        }

        private BaseAirlock ResolveRepairAirlock(Collider collider)
        {
            if (collider == null)
                return null;

            if (!ReferenceEquals(_cachedAirlockTargetCollider, collider))
            {
                _cachedAirlockTargetCollider = collider;
                _cachedServiceTargetAirlock = null;

                if (!collider.TryGetComponent(out _cachedServiceTargetAirlock))
                    _cachedServiceTargetAirlock = collider.GetComponentInParent<BaseAirlock>();
            }

            return _cachedServiceTargetAirlock;
        }

        private bool TryHandleSubmarineDamageControlHit(float deltaTime)
        {
            ISubmarineDamageControlTarget damageTarget = ResolveSubmarineDamageControlTarget(_hit.collider);
            if (damageTarget == null)
                return false;

            float repairPowerPerSecond = ResolveRuntimeRepairPowerPerSecond();
            float intensity01 = ResolveRuntimeRepairPowerNormalized();
            int repairRoomId = -1;
            ISubmarineRepairRoomResolver roomResolver = _cachedSubmarineRepairRoomResolver;
            if (roomResolver != null)
                roomResolver.TryResolveRepairRoom(_hit.point, out repairRoomId);

            bool dentChanged = TryRepairVaultHullDents(
                _hit.point,
                deltaTime,
                repairPowerPerSecond,
                intensity01,
                _cachedSubmarineDamageTargetTransform,
                repairRoomId,
                out _,
                out _);
            bool breachRepairQueued = damageTarget.TryQueueRepairHit(_hit.point, deltaTime, repairPowerPerSecond, intensity01);
            if (!dentChanged && !breachRepairQueued)
                return false;

            UpdateBeamHit(_hit.point, _hit.normal);
            PublishRepairSparkSignal(_hit.point, intensity01);
            QueueToolHapticFeedback(repairPowerPerSecond, math.max(1f, repairSpeed));

            if (!_activeRepairReportedThisUse)
            {
                PublishActiveRepairInfo(RepairToolPatchingHeadline);
                _activeRepairReportedThisUse = true;
            }

            return true;
        }

        private ISubmarineDamageControlTarget ResolveSubmarineDamageControlTarget(Collider collider)
        {
            if (collider == null)
                return null;

            if (!ReferenceEquals(_cachedSubmarineDamageTargetCollider, collider))
            {
                _cachedSubmarineDamageTargetCollider = collider;
                _cachedSubmarineDamageTarget = null;
                _cachedSubmarineRepairRoomResolver = null;
                _cachedSubmarineDamageTargetTransform = null;

                _submarineDamageTargetSearchBuffer.Clear();
                collider.GetComponentsInParent(false, _submarineDamageTargetSearchBuffer);
                for (int i = 0; i < _submarineDamageTargetSearchBuffer.Count; i++)
                {
                    MonoBehaviour component = _submarineDamageTargetSearchBuffer[i];
                    if (component is ISubmarineDamageControlTarget target)
                    {
                        _cachedSubmarineDamageTarget = target;
                        _cachedSubmarineRepairRoomResolver = component as ISubmarineRepairRoomResolver;
                        _cachedSubmarineDamageTargetTransform = component != null ? component.transform : null;
                        break;
                    }
                }
            }

            return _cachedSubmarineDamageTarget;
        }

        private void PublishRepairSparkSignal(Vector3 worldPoint, float intensity01)
        {
            float safeIntensity01 = math.isfinite(intensity01) ? math.saturate(intensity01) : 0f;
            ushort sparkQuantity = ResolveRepairSparkQuantity(safeIntensity01);
            double3 absolute = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(worldPoint);
            DebrisSpawnSignal signal = new DebrisSpawnSignal
            {
                PositionAup = AbsoluteUniversePosition.FromAbsolutePosition(absolute),
                SpeciesHash = RepairSparksSignalHash,
                SourceEntityId = unchecked((uint)EntityId.ToULong(gameObject.GetEntityId())),
                Intensity01 = safeIntensity01,
                DebrisKind = RepairSparkDebrisKind,
                Flags = DebrisSpawnSignal.FlagToolSparks | DebrisSpawnSignal.FlagComputeShard,
                Quantity = sparkQuantity
            };
            GlobalSignals.Publish(in signal);
        }

        private static ushort ResolveRepairSparkQuantity(float intensity01)
        {
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            bool lowTier = tier == HectonQualityTier.Unknown ||
                           tier == HectonQualityTier.Low ||
                           tier == HectonQualityTier.Mx350;
            int min = lowTier ? 2 : 8;
            int max = lowTier ? 6 : 32;
            return (ushort)math.clamp((int)math.round(math.lerp(min, max, math.saturate(intensity01))), 1, 64);
        }

        private bool TryRepairVaultHullDents(
            Vector3 worldPoint,
            float deltaTime,
            float repairPowerPerSecond,
            float intensity01,
            Transform submarineRoot,
            int roomId,
            out int touchedDentCount,
            out int repairedDentCount)
        {
            touchedDentCount = 0;
            repairedDentCount = 0;
            float safeDeltaTime = FiniteNonNegativeOrZero(deltaTime);
            float safeRepairPowerPerSecond = FiniteNonNegativeOrZero(repairPowerPerSecond);
            float safeIntensity01 = math.isfinite(intensity01) ? math.saturate(intensity01) : 0f;
            if (submarineRoot == null ||
                safeDeltaTime <= 0f ||
                safeRepairPowerPerSecond <= 0f ||
                !TryResolveSubmarineLocalHit(submarineRoot, worldPoint, out float3 localPoint))
            {
                return false;
            }

            IDataVault vault = ResolveDataVault();
            if (vault == null)
                return false;

            if (!EnsureHullDentsHandle(vault))
                return false;

            float repairDelta = safeDeltaTime *
                                safeRepairPowerPerSecond *
                                HullDentRepairDepthScale *
                                math.max(0.1f, safeIntensity01);
            if (repairDelta <= 0f || !math.isfinite(repairDelta))
                return false;

            if (!vault.TryLockBuffer(BufferID.HullDents))
                return false;

            bool changed = false;
            int activeDentCount = 0;
            try
            {
                var dents = _hullDentsHandle.Resolve(vault);
                if (!dents.IsCreated)
                    return false;

                int count = math.min(HullDentVaultCapacity, dents.Length);
                for (int dentIndex = 0; dentIndex < count; dentIndex++)
                {
                    float4 dent = dents[dentIndex];
                    if (!math.all(math.isfinite(dent)))
                    {
                        dents[dentIndex] = default;
                        changed = true;
                        continue;
                    }

                    float depth = UnpackHullDentDepth(dent.w);
                    if (depth <= MinimumStoredHullDentDepthMeters)
                    {
                        if (dent.w < 0f || !math.isfinite(dent.w))
                        {
                            dent.w = 0f;
                            dents[dentIndex] = dent;
                            changed = true;
                        }

                        continue;
                    }

                    activeDentCount++;
                    float3 dentPoint = new float3(dent.x, dent.y, dent.z);
                    if (math.distancesq(dentPoint, localPoint) > HullDentRepairRadiusSq)
                        continue;

                    touchedDentCount++;
                    float radius = UnpackHullDentRadius(dent.w);
                    float repairedDepth = math.max(0f, depth - repairDelta);
                    float repairedPacked = repairedDepth <= MinimumStoredHullDentDepthMeters
                        ? 0f
                        : PackHullDentRadiusDepth(radius, repairedDepth);

                    if (math.abs(repairedPacked - dent.w) <= HullRepairEpsilon)
                        continue;

                    dent.w = math.max(0f, repairedPacked);
                    dents[dentIndex] = dent;
                    changed = true;

                    if (repairedDepth <= MinimumStoredHullDentDepthMeters)
                    {
                        repairedDentCount++;
                        activeDentCount = math.max(0, activeDentCount - 1);
                        PublishHullRepairedSignal(worldPoint, roomId, dentIndex, repairedDentCount);
                    }
                }
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.HullDents);
            }

            if (changed || repairedDentCount > 0)
                CrashTelemetryBuffer.ReportHullDentState(HullRepairTelemetryHash, activeDentCount, BuildHullRepairTelemetryFlags(touchedDentCount, repairedDentCount));

            return changed;
        }

        private IDataVault ResolveDataVault()
        {
            _dataVault = GlobalRegistry.DataVault;
            return _dataVault;
        }

        private bool EnsureHullDentsHandle(IDataVault vault)
        {
            if (vault == null)
                return false;

            if (!_hullDentsHandle.IsCreated ||
                _hullDentsHandle.BufferId != BufferID.HullDents ||
                _hullDentsHandle.Length < HullDentVaultCapacity)
            {
                _hullDentsHandle = vault.GetBufferHandle<float4>(
                    BufferID.HullDents,
                    HullDentVaultCapacity,
                    SystemID.GameplayTools,
                    NativeArrayOptions.ClearMemory);
            }

            return _hullDentsHandle.IsCreated;
        }

        private static bool TryResolveSubmarineLocalHit(Transform submarineRoot, Vector3 worldPoint, out float3 localPoint)
        {
            localPoint = default;
            if (submarineRoot == null || !IsFiniteVector(worldPoint))
                return false;
            if (!IsFiniteQuaternion(submarineRoot.rotation))
                return false;

            double3 hitAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(worldPoint);
            double3 rootAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(submarineRoot.position);
            double3 relativeWorldDouble = hitAup - rootAup;
            if (!math.all(math.isfinite(relativeWorldDouble)))
                return false;

            Vector3 relativeWorld = new Vector3(
                (float)relativeWorldDouble.x,
                (float)relativeWorldDouble.y,
                (float)relativeWorldDouble.z);
            if (!IsFiniteVector(relativeWorld))
                return false;

            Vector3 localVector = Quaternion.Inverse(submarineRoot.rotation) * relativeWorld;
            Vector3 lossyScale = submarineRoot.lossyScale;
            localVector.x /= ResolveSafeScale(lossyScale.x);
            localVector.y /= ResolveSafeScale(lossyScale.y);
            localVector.z /= ResolveSafeScale(lossyScale.z);
            if (!IsFiniteVector(localVector))
                return false;

            localPoint = new float3(localVector.x, localVector.y, localVector.z);
            return math.all(math.isfinite(localPoint));
        }

        private bool TryResolveToolLocalPointAup(Vector3 worldPoint, out Vector3 localPoint)
        {
            localPoint = Vector3.zero;
            if (!IsFiniteVector(worldPoint))
                return false;

            Transform toolTransform = _cachedTransform != null ? _cachedTransform : transform;
            _cachedTransform = toolTransform;
            if (toolTransform == null ||
                !IsFiniteVector(toolTransform.position) ||
                !IsFiniteQuaternion(toolTransform.rotation))
            {
                return false;
            }

            double3 pointAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(worldPoint);
            double3 toolAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(toolTransform.position);
            double3 relativeWorldDouble = pointAup - toolAup;
            if (!math.all(math.isfinite(relativeWorldDouble)))
                return false;

            Vector3 relativeWorld = new Vector3(
                (float)relativeWorldDouble.x,
                (float)relativeWorldDouble.y,
                (float)relativeWorldDouble.z);
            if (!IsFiniteVector(relativeWorld))
                return false;

            Vector3 localVector = Quaternion.Inverse(toolTransform.rotation) * relativeWorld;
            Vector3 lossyScale = toolTransform.lossyScale;
            localVector.x /= ResolveSafeScale(lossyScale.x);
            localVector.y /= ResolveSafeScale(lossyScale.y);
            localVector.z /= ResolveSafeScale(lossyScale.z);
            if (!IsFiniteVector(localVector))
                return false;

            localPoint = localVector;
            return true;
        }

        private static void PublishHullRepairedSignal(Vector3 worldPoint, int roomId, int dentIndex, int repairedDentCount)
        {
            double3 absolute = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(worldPoint);
            byte flags = HullRepairedSignal.CompletedFlag;
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            if (tier == HectonQualityTier.Unknown || tier == HectonQualityTier.Low || tier == HectonQualityTier.Mx350)
                flags |= HullRepairedSignal.LowTierVisualOnlyFlag;

            HullRepairedSignal signal = new HullRepairedSignal
            {
                HitAup = AbsoluteUniversePosition.FromAbsolutePosition(absolute),
                RoomId = roomId,
                SourceHash = HullRepairSourceHash,
                Frame = unchecked((uint)Time.frameCount),
                DentIndex = (byte)math.clamp(dentIndex, 0, 255),
                DentsRepairedCount = (byte)math.clamp(repairedDentCount, 0, 255),
                QualityTier = GlobalRegistry.ScalabilityTierProfileByte,
                Flags = flags
            };
            GlobalSignals.Publish(in signal);
        }

        private static uint BuildHullRepairTelemetryFlags(int touchedDentCount, int repairedDentCount)
        {
            uint touched = (uint)math.clamp(touchedDentCount, 0, 255);
            uint repaired = (uint)math.clamp(repairedDentCount, 0, 255);
            return GlobalRegistry.ScalabilityTierProfileByte |
                   (touched << 8) |
                   (repaired << 16);
        }

        private static float PackHullDentRadiusDepth(float radius, float depth)
        {
            int radiusQ = Mathf.Clamp(
                Mathf.RoundToInt(math.clamp(radius, 0f, 15.9375f) * HullDentRadiusQuantizationStepsPerMeter),
                0,
                255);
            int depthQ = Mathf.Clamp(Mathf.RoundToInt(math.saturate(depth) * 255f), 0, 255);
            return (depthQ << 8) | radiusQ;
        }

        private static float UnpackHullDentRadius(float packed)
        {
            int packedInt = Mathf.Max(0, Mathf.RoundToInt(math.max(0f, packed)));
            return (packedInt & 255) * InvHullDentRadiusQuantizationStepsPerMeter;
        }

        private static float UnpackHullDentDepth(float packed)
        {
            int packedInt = Mathf.Max(0, Mathf.RoundToInt(math.max(0f, packed)));
            return ((packedInt >> 8) & 255) * InvHullDentDepthQuantizationSteps;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }

        private static Vector3 ResolveFiniteDirection(Vector3 value, Vector3 fallback)
        {
            if (IsFiniteVector(value))
            {
                float lengthSq = value.sqrMagnitude;
                if (math.isfinite(lengthSq) && lengthSq > HullRepairEpsilon * HullRepairEpsilon)
                    return value * math.rsqrt(lengthSq);
            }

            if (IsFiniteVector(fallback))
            {
                float fallbackLengthSq = fallback.sqrMagnitude;
                if (math.isfinite(fallbackLengthSq) && fallbackLengthSq > HullRepairEpsilon * HullRepairEpsilon)
                    return fallback * math.rsqrt(fallbackLengthSq);
            }

            return Vector3.forward;
        }

        private static bool IsFiniteQuaternion(Quaternion value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z) &&
                   math.isfinite(value.w);
        }

        private static float FiniteNonNegativeOrZero(float value)
        {
            return math.isfinite(value) && value > 0f ? value : 0f;
        }

        private static float ResolveSafeScale(float scale)
        {
            return math.isfinite(scale) && math.abs(scale) > HullRepairEpsilon ? scale : 1f;
        }

        private void PublishIntegrityDiagnostic(BaseModule module, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (module == null || !TryBuildIntegrityDiagnosticBuffer(module, out int length))
            {
                ClearIntegrityDiagnostic();
                return;
            }

            DiegeticTooltipSystem tooltipSystem = ResolveDiegeticTooltipSystem();
            if (tooltipSystem == null)
                return;

            Vector3 anchorPoint = hitPoint + (hitNormal * 0.035f);
            tooltipSystem.ShowDiagnostic(anchorPoint, _integrityDiagnosticBuffer.AsSpan(0, length), new Color(0.46f, 0.98f, 0.94f, 0.96f));
        }

        private void ClearIntegrityDiagnostic()
        {
            DiegeticTooltipSystem tooltipSystem = ResolveDiegeticTooltipSystem();
            if (tooltipSystem != null)
                tooltipSystem.ClearDiagnostic();
        }

        private static DiegeticTooltipSystem ResolveDiegeticTooltipSystem()
        {
            RegistryBucket<IRenderable> renderables = GlobalRegistry.Renderables;
            int count = renderables.Count;
            for (int i = 0; i < count; i++)
            {
                if (renderables.GetAt(i) is DiegeticTooltipSystem tooltipSystem)
                    return tooltipSystem;
            }

            return null;
        }

        private bool TryBuildIntegrityDiagnosticBuffer(BaseModule module, out int length)
        {
            length = 0;
            if (module == null)
                return false;

            int cursor = 0;
            s_integrityDiagnosticPrefixChars.CopyTo(_integrityDiagnosticBuffer, cursor);
            cursor += s_integrityDiagnosticPrefixChars.Length;
            int integrityPercent = module.MaxIntegrity > 0.01f
                ? (int)(math.saturate(module.CurrentIntegrity / module.MaxIntegrity) * 100f + 0.5f)
                : 0;
            if (!integrityPercent.TryFormat(_integrityDiagnosticBuffer.AsSpan(cursor), out int written))
                return false;

            cursor += written;
            if (cursor >= _integrityDiagnosticBuffer.Length)
                return false;

            _integrityDiagnosticBuffer[cursor++] = '%';
            length = cursor;
            return true;
        }

        private bool TryReadServiceDiagnosis(out ServiceDiagnosis diagnosis)
        {
            diagnosis = default;

            bool didHit = TryGetRepairHit(out _hit);

            ResolveRepairTargets(didHit ? _hit.collider : null, out BaseModule module, out _);
            if (module == null)
                return false;

            diagnosis = BuildDiagnosis(module);
            return true;
        }

        private bool TryGetServiceDiagnosisCached(out ServiceDiagnosis diagnosis)
        {
            int currentFrame = Time.frameCount;
            if (_cachedDiagnosisFrame == currentFrame)
            {
                diagnosis = _cachedDiagnosis;
                return _cachedDiagnosisValid;
            }

            bool valid = TryReadServiceDiagnosis(out diagnosis);
            _cachedDiagnosisFrame = currentFrame;
            _cachedDiagnosisValid = valid;
            _cachedDiagnosis = diagnosis;
            return valid;
        }

        private void InvalidateDiagnosisCache()
        {
            _cachedDiagnosisFrame = -1;
            _cachedDiagnosisValid = false;
            _cachedDiagnosis = default;
        }

        private bool TryGetRepairHit(out RaycastHit hit)
        {
            return TryResolveQueuedRaycast(_cachedTransform.position, _cachedTransform.forward, ResolveRuntimeRepairRange(), repairMask.value, QueryTriggerInteraction.Ignore, out hit);
        }

        private static ServiceDiagnosis BuildDiagnosis(BaseModule module)
        {
            float integrity01 = module.MaxIntegrity > 0f
                ? math.saturate(module.CurrentIntegrity / module.MaxIntegrity)
                : 0f;
            int integrityPercent = (int)(integrity01 * 100f + 0.5f);

            if (module.IsFlooded && !module.HasPower && module.CurrentIntegrity >= module.MaxIntegrity)
            {
                return new ServiceDiagnosis
                {
                    status = "FLOODED",
                    headline = RepairToolNoPowerHeadline,
                    summaryKey = LocalizationKeys.REPAIR_TOOL_SUMMARY_NO_POWER,
                    summaryFallback = "Integrity {0:0}% // compartment flooded // pumps offline.",
                    recommendation = ResolveLocalized(
                        LocalizationKeys.REPAIR_TOOL_RECOMMEND_NO_POWER,
                        "Restore power before expecting water evacuation."),
                    severity = "WARN",
                    priority = ResolveLocalized(LocalizationKeys.REPAIR_TOOL_PRIORITY_SERVICE_BLOCKED, "SERVICE BLOCKED"),
                    integrityPercent = integrityPercent,
                    hasIntegrityPercent = true
                };
            }

            if (module.IsFlooded && module.IsDraining)
            {
                return new ServiceDiagnosis
                {
                    status = "DRAINING",
                    headline = RepairToolDrainingHeadline,
                    summaryKey = LocalizationKeys.REPAIR_TOOL_SUMMARY_DRAINING,
                    summaryFallback = "Integrity {0:0}% // pumps are clearing floodwater.",
                    recommendation = ResolveLocalized(
                        LocalizationKeys.REPAIR_TOOL_RECOMMEND_DRAINING,
                        "Hold perimeter and let the compartment finish draining."),
                    severity = "INFO",
                    priority = ResolveLocalized(LocalizationKeys.REPAIR_TOOL_PRIORITY_STABILIZING, "STABILIZING"),
                    integrityPercent = integrityPercent,
                    hasIntegrityPercent = true
                };
            }

            if (module.IsFlooded)
            {
                return new ServiceDiagnosis
                {
                    status = "FLOODED",
                    headline = RepairToolFloodedHeadline,
                    summaryKey = LocalizationKeys.REPAIR_TOOL_SUMMARY_FLOODED,
                    summaryFallback = "Integrity {0:0}% // compartment breach still active.",
                    recommendation = ResolveLocalized(
                        LocalizationKeys.REPAIR_TOOL_RECOMMEND_FLOODED,
                        "Continue repair until integrity reaches 100% and pump cycle can start."),
                    severity = "WARN",
                    priority = ResolveLocalized(LocalizationKeys.REPAIR_TOOL_PRIORITY_IMMEDIATE_SERVICE, "IMMEDIATE SERVICE"),
                    integrityPercent = integrityPercent,
                    hasIntegrityPercent = true
                };
            }

            if (integrity01 >= 0.999f)
            {
                return new ServiceDiagnosis
                {
                    status = "SEALED",
                    headline = RepairToolSealedHeadline,
                    summary = ResolveLocalized(
                        LocalizationKeys.REPAIR_TOOL_SUMMARY_SEALED,
                        "Integrity 100% // hull stable // compartment dry."),
                    recommendation = ResolveLocalized(
                        LocalizationKeys.REPAIR_TOOL_RECOMMEND_SEALED,
                        "No further repair action required."),
                    severity = "INFO",
                    priority = ResolveLocalized(LocalizationKeys.REPAIR_TOOL_PRIORITY_SERVICE_COMPLETE, "SERVICE COMPLETE")
                };
            }

            if (integrity01 <= 0.25f)
            {
                return new ServiceDiagnosis
                {
                    status = "CRITICAL",
                    headline = RepairToolCriticalDamageHeadline,
                    summaryKey = LocalizationKeys.REPAIR_TOOL_SUMMARY_CRITICAL,
                    summaryFallback = "Integrity {0:0}% // hull failure risk elevated.",
                    recommendation = ResolveLocalized(
                        LocalizationKeys.REPAIR_TOOL_RECOMMEND_CRITICAL,
                        "Maintain continuous repair contact until the module exits critical range."),
                    severity = "CRITICAL",
                    priority = ResolveLocalized(LocalizationKeys.REPAIR_TOOL_PRIORITY_CRITICAL_RESPONSE, "CRITICAL RESPONSE"),
                    integrityPercent = integrityPercent,
                    hasIntegrityPercent = true
                };
            }

            if (integrity01 <= 0.65f)
            {
                return new ServiceDiagnosis
                {
                    status = "DAMAGED",
                    headline = RepairToolHeavyDamageHeadline,
                    summaryKey = LocalizationKeys.REPAIR_TOOL_SUMMARY_HEAVY,
                    summaryFallback = "Integrity {0:0}% // hull is compromised but recoverable.",
                    recommendation = ResolveLocalized(
                        LocalizationKeys.REPAIR_TOOL_RECOMMEND_HEAVY,
                        "Keep the repair beam on target and avoid leaving the module unattended."),
                    severity = "WARN",
                    priority = ResolveLocalized(LocalizationKeys.REPAIR_TOOL_PRIORITY_ACTIVE_SERVICE, "ACTIVE SERVICE"),
                    integrityPercent = integrityPercent,
                    hasIntegrityPercent = true
                };
            }

            return new ServiceDiagnosis
            {
                status = "DAMAGED",
                headline = RepairToolPatchingHeadline,
                summaryKey = LocalizationKeys.REPAIR_TOOL_SUMMARY_PATCHING,
                summaryFallback = "Integrity {0:0}% // module is nearly sealed.",
                recommendation = ResolveLocalized(
                    LocalizationKeys.REPAIR_TOOL_RECOMMEND_PATCHING,
                    "Finish the repair cycle to restore full integrity."),
                severity = "INFO",
                priority = ResolveLocalized(LocalizationKeys.REPAIR_TOOL_PRIORITY_FINAL_PASS, "FINAL PASS"),
                integrityPercent = integrityPercent,
                hasIntegrityPercent = true
            };
        }

        private static void PublishDiagnosis(ServiceDiagnosis diagnosis)
        {
            s_hudBuffer.Clear();
            if (!TryWriteDiagnosisMessage(ref s_hudBuffer, diagnosis))
                return;

            if (diagnosis.severity == "CRITICAL")
                ToolHitUtility.ShowWarning(in s_hudBuffer);
            else if (diagnosis.severity == "WARN")
                ToolHitUtility.ShowWarning(in s_hudBuffer);
            else
                ToolHitUtility.ShowInfo(in s_hudBuffer);
        }

        private static void PublishInfoMessage(string message)
        {
            s_hudBuffer.Clear();
            if (AppendText(ref s_hudBuffer, message))
                ToolHitUtility.ShowInfo(in s_hudBuffer);
        }

        private static void PublishWarningMessage(string message)
        {
            s_hudBuffer.Clear();
            if (AppendText(ref s_hudBuffer, message))
                ToolHitUtility.ShowWarning(in s_hudBuffer);
        }

        private static void PublishActiveRepairInfo(string headline)
        {
            s_hudBuffer.Clear();
            if (TryWriteActiveRepairHudMessage(ref s_hudBuffer, headline))
                ToolHitUtility.ShowInfo(in s_hudBuffer);
        }

        private static bool TryWriteDiagnosisMessage(ref FixedCharBuffer buffer, ServiceDiagnosis diagnosis)
        {
            if (!AppendText(ref buffer, "REPAIR DIAG - ") ||
                !AppendText(ref buffer, diagnosis.headline) ||
                !AppendText(ref buffer, " // ") ||
                !AppendText(ref buffer, diagnosis.priority) ||
                !AppendText(ref buffer, " // "))
                return false;

            if (!TryWriteDiagnosisSummary(ref buffer, diagnosis))
                return false;

            return AppendText(ref buffer, " // ") &&
                   AppendText(ref buffer, diagnosis.recommendation);
        }

        private static bool TryWriteActiveRepairHudMessage(ref FixedCharBuffer buffer, string headline)
        {
            switch (headline)
            {
                case RepairToolNoPowerHeadline:
                    return AppendText(ref buffer, ResolveLocalized(LocalizationKeys.REPAIR_TOOL_HUD_NO_POWER, "REPAIR TOOL - NO POWER"));
                case RepairToolDrainingHeadline:
                    return AppendText(ref buffer, ResolveLocalized(LocalizationKeys.REPAIR_TOOL_HUD_DRAINING, "REPAIR TOOL - DRAINING"));
                case RepairToolFloodedHeadline:
                    return AppendText(ref buffer, ResolveLocalized(LocalizationKeys.REPAIR_TOOL_HUD_FLOODED, "REPAIR TOOL - FLOODED"));
                case RepairToolSealedHeadline:
                    return AppendText(ref buffer, ResolveLocalized(LocalizationKeys.REPAIR_TOOL_HUD_SEALED, "REPAIR TOOL - SEALED"));
                case RepairToolCriticalDamageHeadline:
                    return AppendText(ref buffer, ResolveLocalized(LocalizationKeys.REPAIR_TOOL_HUD_CRITICAL_DAMAGE, "REPAIR TOOL - CRITICAL DAMAGE"));
                case RepairToolHeavyDamageHeadline:
                    return AppendText(ref buffer, ResolveLocalized(LocalizationKeys.REPAIR_TOOL_HUD_HEAVY_DAMAGE, "REPAIR TOOL - HEAVY DAMAGE"));
                case RepairToolPatchingHeadline:
                    return AppendText(ref buffer, ResolveLocalized(LocalizationKeys.REPAIR_TOOL_HUD_PATCHING, "REPAIR TOOL - PATCHING"));
                default:
                    return AppendText(ref buffer, "REPAIR TOOL - ") &&
                           AppendText(ref buffer, headline);
            }
        }

        private static bool TryWriteDiagnosisSummary(ref FixedCharBuffer buffer, ServiceDiagnosis diagnosis)
        {
            if (!diagnosis.hasIntegrityPercent)
                return AppendText(ref buffer, diagnosis.summary);

            string template = ResolveLocalized(diagnosis.summaryKey, diagnosis.summaryFallback);
            return TryAppendSingleIntTemplate(ref buffer, template, diagnosis.integrityPercent);
        }

        private static bool TryWriteDiagnosisLogSummary(ref FixedCharBuffer buffer, ServiceDiagnosis diagnosis)
        {
            if (!TryWriteDiagnosisSummary(ref buffer, diagnosis))
                return false;

            return AppendText(ref buffer, " ") &&
                   AppendText(ref buffer, diagnosis.recommendation);
        }

        private static bool TryWriteRepairStartedLogSummary(ref FixedCharBuffer buffer, ServiceDiagnosis diagnosis)
        {
            string template = ResolveLocalized(
                LocalizationKeys.REPAIR_TOOL_LOG_STARTED_MESSAGE,
                "{0} entered active repair service. {1} {2}");
            return TryAppendRepairStartedTemplate(ref buffer, template, RepairToolModuleLabel, diagnosis);
        }

        private static bool TryWriteRepairRestoredLogSummary(ref FixedCharBuffer buffer)
        {
            string template = ResolveLocalized(
                LocalizationKeys.REPAIR_TOOL_LOG_RESTORED_MESSAGE,
                "{0} reached full integrity and dry status.");
            return TryAppendSingleStringTemplate(ref buffer, template, RepairToolModuleLabel);
        }

        private static bool TryAppendRepairStartedTemplate(
            ref FixedCharBuffer buffer,
            string template,
            string targetLabel,
            ServiceDiagnosis diagnosis)
        {
            ReadOnlySpan<char> templateSpan = template.AsSpan();
            if (templateSpan.Length <= 0)
                return AppendText(ref buffer, targetLabel);

            int segmentStart = 0;
            for (int i = 0; i < templateSpan.Length; i++)
            {
                if (templateSpan[i] != '{' || i + 1 >= templateSpan.Length)
                    continue;

                char tokenIndex = templateSpan[i + 1];
                int tokenEnd = i + 2;
                while (tokenEnd < templateSpan.Length && templateSpan[tokenEnd] != '}')
                    tokenEnd++;

                if (tokenEnd >= templateSpan.Length)
                    continue;

                if (i > segmentStart && !buffer.Append(templateSpan.Slice(segmentStart, i - segmentStart)))
                    return false;

                bool wroteToken = false;
                switch (tokenIndex)
                {
                    case '0':
                        wroteToken = AppendText(ref buffer, targetLabel);
                        break;
                    case '1':
                        wroteToken = TryWriteDiagnosisSummary(ref buffer, diagnosis);
                        break;
                    case '2':
                        wroteToken = AppendText(ref buffer, diagnosis.recommendation);
                        break;
                }

                if (!wroteToken && !buffer.Append(templateSpan.Slice(i, tokenEnd - i + 1)))
                    return false;

                i = tokenEnd;
                segmentStart = tokenEnd + 1;
            }

            return segmentStart >= templateSpan.Length || buffer.Append(templateSpan.Slice(segmentStart));
        }

        private static bool TryAppendSingleStringTemplate(ref FixedCharBuffer buffer, string template, string value)
        {
            ReadOnlySpan<char> templateSpan = template.AsSpan();
            if (templateSpan.Length <= 0)
                return AppendText(ref buffer, value);

            bool wroteTemplateToken = false;
            int segmentStart = 0;
            for (int i = 0; i < templateSpan.Length; i++)
            {
                if (templateSpan[i] != '{' || i + 1 >= templateSpan.Length || templateSpan[i + 1] != '0')
                    continue;

                int tokenEnd = i + 2;
                while (tokenEnd < templateSpan.Length && templateSpan[tokenEnd] != '}')
                    tokenEnd++;

                if (tokenEnd >= templateSpan.Length)
                    continue;

                if (i > segmentStart && !buffer.Append(templateSpan.Slice(segmentStart, i - segmentStart)))
                    return false;

                if (!AppendText(ref buffer, value))
                    return false;

                wroteTemplateToken = true;
                i = tokenEnd;
                segmentStart = tokenEnd + 1;
            }

            if (!wroteTemplateToken)
                return buffer.Append(templateSpan);

            return segmentStart >= templateSpan.Length || buffer.Append(templateSpan.Slice(segmentStart));
        }

        private static bool TryAppendSingleIntTemplate(ref FixedCharBuffer buffer, string template, int value)
        {
            ReadOnlySpan<char> templateSpan = template.AsSpan();
            if (templateSpan.Length <= 0)
                return buffer.AppendInt(value);

            bool wroteTemplateToken = false;
            int segmentStart = 0;
            for (int i = 0; i < templateSpan.Length; i++)
            {
                if (templateSpan[i] != '{' || i + 1 >= templateSpan.Length || templateSpan[i + 1] != '0')
                    continue;

                int tokenEnd = i + 2;
                while (tokenEnd < templateSpan.Length && templateSpan[tokenEnd] != '}')
                    tokenEnd++;

                if (tokenEnd >= templateSpan.Length)
                    continue;

                if (i > segmentStart && !buffer.Append(templateSpan.Slice(segmentStart, i - segmentStart)))
                    return false;

                if (!buffer.AppendInt(value))
                    return false;

                wroteTemplateToken = true;
                i = tokenEnd;
                segmentStart = tokenEnd + 1;
            }

            if (!wroteTemplateToken)
                return buffer.Append(templateSpan);

            return segmentStart >= templateSpan.Length || buffer.Append(templateSpan.Slice(segmentStart));
        }

        private static string GetServiceDiagnosisLogTitle(string headline)
        {
            switch (headline)
            {
                case RepairToolNoPowerHeadline:
                    return ResolveLocalized(LocalizationKeys.REPAIR_TOOL_LOG_DIAG_NO_POWER, "SERVICE DIAG - NO POWER");
                case RepairToolDrainingHeadline:
                    return ResolveLocalized(LocalizationKeys.REPAIR_TOOL_LOG_DIAG_DRAINING, "SERVICE DIAG - DRAINING");
                case RepairToolFloodedHeadline:
                    return ResolveLocalized(LocalizationKeys.REPAIR_TOOL_LOG_DIAG_FLOODED, "SERVICE DIAG - FLOODED");
                case RepairToolSealedHeadline:
                    return ResolveLocalized(LocalizationKeys.REPAIR_TOOL_LOG_DIAG_SEALED, "SERVICE DIAG - SEALED");
                case RepairToolCriticalDamageHeadline:
                    return ResolveLocalized(LocalizationKeys.REPAIR_TOOL_LOG_DIAG_CRITICAL, "SERVICE DIAG - CRITICAL DAMAGE");
                case RepairToolHeavyDamageHeadline:
                    return ResolveLocalized(LocalizationKeys.REPAIR_TOOL_LOG_DIAG_HEAVY, "SERVICE DIAG - HEAVY DAMAGE");
                case RepairToolPatchingHeadline:
                    return ResolveLocalized(LocalizationKeys.REPAIR_TOOL_LOG_DIAG_PATCHING, "SERVICE DIAG - PATCHING");
                default:
                    s_hudBuffer.Clear();
                    string template = ResolveLocalized(LocalizationKeys.REPAIR_TOOL_LOG_DIAG_GENERIC, "SERVICE DIAG - {0}");
                    return TryAppendSingleStringTemplate(ref s_hudBuffer, template, headline)
                        ? CreateLegacyString(in s_hudBuffer)
                        : ResolveLocalized(LocalizationKeys.REPAIR_TOOL_LOG_DIAG_PATCHING, "SERVICE DIAG - PATCHING");
            }
        }

        private static string CreateLegacyString(in FixedCharBuffer buffer)
        {
            return buffer.Length > 0
                ? new string(buffer.Buffer, 0, buffer.Length)
                : string.Empty;
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
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

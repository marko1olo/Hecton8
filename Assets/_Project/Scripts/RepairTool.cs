// ============================================================================
// HECTON-8 — RepairTool.cs
// Ремонтный инструмент игрока.
//
// НАСЛЕДОВАНИЕ:
//   PlayerTool → RepairTool
//
// ЛОГИКА:
//   • UsePrimary(dt):
//       1. Пускает Raycast вперёд.
//       2. Если попал в BaseModule — вызывает Repair(repairSpeed * dt).
//       3. Включает визуал сварки / искры / Bloom-friendly light.
//   • ToolTick(dt):
//       Отключает визуал, если в кадре инструмент не использовался.
//
// ВИЗУАЛ:
//   • sparksVFX         — искры.
//   • repairLine        — LineRenderer луча/дуги.
//   • weldLight         — яркий point light для Bloom в шлеме.
//
// ZERO GC:
//   • RaycastHit — struct.
//   • TryGetComponent — zero GC.
//   • Нет Update().
// ============================================================================

using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Items;
using Hecton8.Tools;
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

        private struct ServiceDiagnosis
        {
            public string status;
            public string headline;
            public string summary;
            public string recommendation;
            public string severity;
            public string priority;
        }

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Repair Settings ───────────────────────────")]
        [Tooltip("Максимальная дальность ремонта.")]
        [SerializeField] private float repairRange = 4f;

        [Tooltip("Скорость ремонта (единиц целостности в секунду).")]
        [SerializeField] private float repairSpeed = 20f;

        [Tooltip("Слои, по которым работает ремонтный луч.")]
        [SerializeField] private LayerMask repairMask = ~0;

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
        public float BatteryCharge => _installedBattery != null ? _batteryCharge : 0f;

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
            _batteryCharge = Mathf.Clamp01(charge);

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

            if (_installedBattery == null || _batteryCharge <= 0f)
            {
                _mpb.SetColor(_EmissionColorID, Color.black);
            }
            else if (_batteryCharge <= 0.2f)
            {
                _mpb.SetColor(_EmissionColorID, new Color(1f, 0.3f, 0f));
            }
            else
            {
                _mpb.SetColor(_EmissionColorID, _powerOnColor);
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
            SetRepairVisuals(false);
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
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
            base.OnDespawn();
        }

        public override void OnEquip()
        {
            base.OnEquip();
            _isRepairing = false;
            _wasRepairingLastFrame = false;
            _invalidTargetReportedThisUse = false;
            _noTargetReportedThisUse = false;
            _healthyTargetReportedThisUse = false;
            _activeRepairReportedThisUse = false;
            _secondaryLatched = false;
            SetRepairVisuals(false);
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
            base.OnUnequip();
        }

        // ══════════════════════════════════════════════════════════
        //  TOOL ACTIONS
        // ══════════════════════════════════════════════════════════

        public override void UsePrimary(float deltaTime)
        {
            _isRepairing = true;

            bool didHit = TryGetRepairHit(out _hit);

            if (!didHit)
            {
                if (!_noTargetReportedThisUse)
                {
                    ToolHitUtility.ShowWarning(ResolveLocalized(LocalizationKeys.REPAIR_TOOL_HUD_NO_TARGET, "REPAIR TOOL - NO TARGET"));
                    _noTargetReportedThisUse = true;
                }
                UpdateBeamMiss();
                InvalidateDiagnosisCache();
                return;
            }

            BaseModule module =
                _hit.collider != null
                    ? _hit.collider.GetComponent<BaseModule>() ?? _hit.collider.GetComponentInParent<BaseModule>()
                    : null;
            if (module != null)
            {
                float beforeIntegrity = module.CurrentIntegrity;
                bool beforeFlooded = module.IsFlooded;

                if (beforeIntegrity >= module.MaxIntegrity && !beforeFlooded)
                {
                    if (!_healthyTargetReportedThisUse)
                    {
                        ToolHitUtility.ShowInfo(ResolveLocalized(LocalizationKeys.REPAIR_TOOL_HUD_SEALED, "REPAIR TOOL - MODULE SEALED"));
                        _healthyTargetReportedThisUse = true;
                    }

                    UpdateBeamHit(_hit.point, _hit.normal);
                    InvalidateDiagnosisCache();
                    return;
                }

                float repairAmount = repairSpeed * deltaTime;
                ToolEffectEvents.RaiseEffectApplied(
                    EffectType.Weld,
                    module,
                    _cachedTransform,
                    repairAmount,
                    _hit.point);
                module.Repair(repairAmount);
                UpdateBeamHit(_hit.point, _hit.normal);

                if (!_activeRepairReportedThisUse)
                {
                    ServiceDiagnosis diagnosis = BuildDiagnosis(module);
                    ToolHitUtility.ShowInfo(GetActiveRepairHudMessage(diagnosis.headline));
                    FieldOperationLogSystem.RecordOperation(
                        ResolveLocalized(LocalizationKeys.REPAIR_TOOL_CATEGORY, RepairToolCategory),
                        ResolveLocalized(LocalizationKeys.REPAIR_TOOL_LOG_STARTED_TITLE, "MODULE REPAIR STARTED"),
                        string.Format(
                            ResolveLocalized(
                                LocalizationKeys.REPAIR_TOOL_LOG_STARTED_MESSAGE,
                                "{0} entered active repair service. {1} {2}"),
                            module.name,
                            diagnosis.summary,
                            diagnosis.recommendation),
                        "INFO");
                    _activeRepairReportedThisUse = true;
                }

                if ((beforeIntegrity < module.MaxIntegrity || beforeFlooded) &&
                    module.CurrentIntegrity >= module.MaxIntegrity &&
                    !module.IsFlooded)
                {
                    ToolHitUtility.ShowInfo(ResolveLocalized(LocalizationKeys.REPAIR_TOOL_HUD_RESTORED, "REPAIR TOOL - MODULE RESTORED"));
                    FieldOperationLogSystem.RecordOperation(
                        ResolveLocalized(LocalizationKeys.REPAIR_TOOL_CATEGORY, RepairToolCategory),
                        ResolveLocalized(LocalizationKeys.REPAIR_TOOL_LOG_RESTORED_TITLE, "MODULE RESTORED"),
                        string.Format(
                            ResolveLocalized(
                                LocalizationKeys.REPAIR_TOOL_LOG_RESTORED_MESSAGE,
                                "{0} reached full integrity and dry status."),
                            module.name),
                        "INFO");
                }
            }
            else
            {
                if (!_invalidTargetReportedThisUse)
                {
                    ToolHitUtility.ShowWarning(ResolveLocalized(LocalizationKeys.REPAIR_TOOL_HUD_INVALID_TARGET, "REPAIR TOOL - INVALID TARGET"));
                    _invalidTargetReportedThisUse = true;
                }
                UpdateBeamMiss();
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
                ToolHitUtility.ShowWarning(ResolveLocalized(LocalizationKeys.REPAIR_TOOL_HUD_NO_MODULE, "REPAIR TOOL - NO MODULE IN RANGE"));
                InvalidateDiagnosisCache();
                return;
            }

            BaseModule module =
                _hit.collider != null
                    ? _hit.collider.GetComponent<BaseModule>() ?? _hit.collider.GetComponentInParent<BaseModule>()
                    : null;
            if (module == null)
            {
                ToolHitUtility.ShowWarning(ResolveLocalized(LocalizationKeys.REPAIR_TOOL_HUD_NOT_SERVICEABLE, "REPAIR TOOL - TARGET NOT SERVICEABLE"));
                InvalidateDiagnosisCache();
                return;
            }

            ServiceDiagnosis diagnosis = BuildDiagnosis(module);
            PublishDiagnosis(diagnosis);
            FieldOperationLogSystem.RecordOperation(
                ResolveLocalized(LocalizationKeys.REPAIR_TOOL_CATEGORY, RepairToolCategory),
                GetServiceDiagnosisLogTitle(diagnosis.headline),
                $"{diagnosis.summary} {diagnosis.recommendation}",
                diagnosis.severity);
            InvalidateDiagnosisCache();
        }

        public override void ToolTick(float deltaTime)
        {
            if (_wasRepairingLastFrame && !_isRepairing)
                SetRepairVisuals(false);

            _wasRepairingLastFrame = _isRepairing;
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
        }

        public override string GetOperationalSummary()
        {
            if (_isRepairing)
                return ResolveLocalized(LocalizationKeys.REPAIR_TOOL_OPERATIONAL_ACTIVE, "REPAIR TOOL // ACTIVE SERVICE");

            if (TryGetServiceDiagnosisCached(out ServiceDiagnosis diagnosis))
                return string.Format(
                    ResolveLocalized(LocalizationKeys.REPAIR_TOOL_OPERATIONAL_PRIORITY, "REPAIR TOOL // {0}"),
                    diagnosis.priority);

            return ResolveLocalized(LocalizationKeys.REPAIR_TOOL_OPERATIONAL_STANDBY, "REPAIR TOOL // STANDBY");
        }

        public override string GetOperationalDirective()
        {
            if (_isRepairing)
                return ResolveLocalized(
                    LocalizationKeys.REPAIR_TOOL_OPERATIONAL_ACTIVE_DIRECTIVE,
                    "Hold the beam steady until the service window closes.");

            if (TryGetServiceDiagnosisCached(out ServiceDiagnosis diagnosis))
                return diagnosis.recommendation;

            return ResolveLocalized(
                LocalizationKeys.REPAIR_TOOL_OPERATIONAL_STANDBY_DIRECTIVE,
                "Sweep a damaged module to diagnose or begin repair.");
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — VISUAL STATE
        // ══════════════════════════════════════════════════════════

        private void UpdateBeamHit(Vector3 hitPoint, Vector3 hitNormal)
        {
            SetRepairVisuals(true);

            if (repairLine != null)
            {
                if (!repairLine.enabled)
                    repairLine.enabled = true;

                repairLine.SetPosition(0, Vector3.zero);
                repairLine.SetPosition(1, _cachedTransform.InverseTransformPoint(hitPoint));
            }

            if (sparksVFX != null)
            {
                Transform t = sparksVFX.transform;
                t.position = hitPoint;
                t.rotation = Quaternion.LookRotation(hitNormal);

                if (!sparksVFX.isPlaying)
                    sparksVFX.Play();
            }

            if (weldLight != null)
            {
                weldLight.transform.position = hitPoint - hitNormal * 0.05f;
            }

            if (repairLoopAudio != null && !repairLoopAudio.isPlaying)
            {
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
                repairLine.SetPosition(1, Vector3.forward * repairRange);
            }

            if (sparksVFX != null && sparksVFX.isPlaying)
            {
                sparksVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            if (weldLight != null)
            {
                weldLight.transform.position = _cachedTransform.position + _cachedTransform.forward * repairRange;
            }

            if (repairLoopAudio != null && !repairLoopAudio.isPlaying)
            {
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

        private bool TryReadServiceDiagnosis(out ServiceDiagnosis diagnosis)
        {
            diagnosis = default;

            bool didHit = TryGetRepairHit(out _hit);

            BaseModule module =
                didHit && _hit.collider != null
                    ? _hit.collider.GetComponent<BaseModule>() ?? _hit.collider.GetComponentInParent<BaseModule>()
                    : null;
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
            return TryResolveQueuedRaycast(_cachedTransform.position, _cachedTransform.forward, repairRange, repairMask.value, QueryTriggerInteraction.Ignore, out hit);
        }

        private static ServiceDiagnosis BuildDiagnosis(BaseModule module)
        {
            float integrity01 = module.MaxIntegrity > 0f
                ? module.CurrentIntegrity / module.MaxIntegrity
                : 0f;

            if (module.IsFlooded && !module.HasPower && module.CurrentIntegrity >= module.MaxIntegrity)
            {
                return new ServiceDiagnosis
                {
                    status = "FLOODED",
                    headline = RepairToolNoPowerHeadline,
                    summary = string.Format(
                        ResolveLocalized(
                            LocalizationKeys.REPAIR_TOOL_SUMMARY_NO_POWER,
                            "Integrity {0:0}% // compartment flooded // pumps offline."),
                        integrity01 * 100f),
                    recommendation = ResolveLocalized(
                        LocalizationKeys.REPAIR_TOOL_RECOMMEND_NO_POWER,
                        "Restore power before expecting water evacuation."),
                    severity = "WARN",
                    priority = ResolveLocalized(LocalizationKeys.REPAIR_TOOL_PRIORITY_SERVICE_BLOCKED, "SERVICE BLOCKED")
                };
            }

            if (module.IsFlooded && module.IsDraining)
            {
                return new ServiceDiagnosis
                {
                    status = "DRAINING",
                    headline = RepairToolDrainingHeadline,
                    summary = string.Format(
                        ResolveLocalized(
                            LocalizationKeys.REPAIR_TOOL_SUMMARY_DRAINING,
                            "Integrity {0:0}% // pumps are clearing floodwater."),
                        integrity01 * 100f),
                    recommendation = ResolveLocalized(
                        LocalizationKeys.REPAIR_TOOL_RECOMMEND_DRAINING,
                        "Hold perimeter and let the compartment finish draining."),
                    severity = "INFO",
                    priority = ResolveLocalized(LocalizationKeys.REPAIR_TOOL_PRIORITY_STABILIZING, "STABILIZING")
                };
            }

            if (module.IsFlooded)
            {
                return new ServiceDiagnosis
                {
                    status = "FLOODED",
                    headline = RepairToolFloodedHeadline,
                    summary = string.Format(
                        ResolveLocalized(
                            LocalizationKeys.REPAIR_TOOL_SUMMARY_FLOODED,
                            "Integrity {0:0}% // compartment breach still active."),
                        integrity01 * 100f),
                    recommendation = ResolveLocalized(
                        LocalizationKeys.REPAIR_TOOL_RECOMMEND_FLOODED,
                        "Continue repair until integrity reaches 100% and pump cycle can start."),
                    severity = "WARN",
                    priority = ResolveLocalized(LocalizationKeys.REPAIR_TOOL_PRIORITY_IMMEDIATE_SERVICE, "IMMEDIATE SERVICE")
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
                    summary = string.Format(
                        ResolveLocalized(
                            LocalizationKeys.REPAIR_TOOL_SUMMARY_CRITICAL,
                            "Integrity {0:0}% // hull failure risk elevated."),
                        integrity01 * 100f),
                    recommendation = ResolveLocalized(
                        LocalizationKeys.REPAIR_TOOL_RECOMMEND_CRITICAL,
                        "Maintain continuous repair contact until the module exits critical range."),
                    severity = "CRITICAL",
                    priority = ResolveLocalized(LocalizationKeys.REPAIR_TOOL_PRIORITY_CRITICAL_RESPONSE, "CRITICAL RESPONSE")
                };
            }

            if (integrity01 <= 0.65f)
            {
                return new ServiceDiagnosis
                {
                    status = "DAMAGED",
                    headline = RepairToolHeavyDamageHeadline,
                    summary = string.Format(
                        ResolveLocalized(
                            LocalizationKeys.REPAIR_TOOL_SUMMARY_HEAVY,
                            "Integrity {0:0}% // hull is compromised but recoverable."),
                        integrity01 * 100f),
                    recommendation = ResolveLocalized(
                        LocalizationKeys.REPAIR_TOOL_RECOMMEND_HEAVY,
                        "Keep the repair beam on target and avoid leaving the module unattended."),
                    severity = "WARN",
                    priority = ResolveLocalized(LocalizationKeys.REPAIR_TOOL_PRIORITY_ACTIVE_SERVICE, "ACTIVE SERVICE")
                };
            }

            return new ServiceDiagnosis
            {
                status = "DAMAGED",
                headline = RepairToolPatchingHeadline,
                summary = string.Format(
                    ResolveLocalized(
                        LocalizationKeys.REPAIR_TOOL_SUMMARY_PATCHING,
                        "Integrity {0:0}% // module is nearly sealed."),
                    integrity01 * 100f),
                recommendation = ResolveLocalized(
                    LocalizationKeys.REPAIR_TOOL_RECOMMEND_PATCHING,
                    "Finish the repair cycle to restore full integrity."),
                severity = "INFO",
                priority = ResolveLocalized(LocalizationKeys.REPAIR_TOOL_PRIORITY_FINAL_PASS, "FINAL PASS")
            };
        }

        private static void PublishDiagnosis(ServiceDiagnosis diagnosis)
        {
            string message = string.Format(
                ResolveLocalized(
                    LocalizationKeys.REPAIR_TOOL_DIAG_MESSAGE,
                    "REPAIR DIAG - {0} // {1} // {2} // {3}"),
                diagnosis.headline,
                diagnosis.priority,
                diagnosis.summary,
                diagnosis.recommendation);
            if (diagnosis.severity == "CRITICAL")
                ToolHitUtility.ShowWarning(message);
            else if (diagnosis.severity == "WARN")
                ToolHitUtility.ShowWarning(message);
            else
                ToolHitUtility.ShowInfo(message);
        }

        private static string GetActiveRepairHudMessage(string headline)
        {
            switch (headline)
            {
                case RepairToolNoPowerHeadline:
                    return ResolveLocalized(LocalizationKeys.REPAIR_TOOL_HUD_NO_POWER, "REPAIR TOOL - NO POWER");
                case RepairToolDrainingHeadline:
                    return ResolveLocalized(LocalizationKeys.REPAIR_TOOL_HUD_DRAINING, "REPAIR TOOL - DRAINING");
                case RepairToolFloodedHeadline:
                    return ResolveLocalized(LocalizationKeys.REPAIR_TOOL_HUD_FLOODED, "REPAIR TOOL - FLOODED");
                case RepairToolSealedHeadline:
                    return ResolveLocalized(LocalizationKeys.REPAIR_TOOL_HUD_SEALED, "REPAIR TOOL - SEALED");
                case RepairToolCriticalDamageHeadline:
                    return ResolveLocalized(LocalizationKeys.REPAIR_TOOL_HUD_CRITICAL_DAMAGE, "REPAIR TOOL - CRITICAL DAMAGE");
                case RepairToolHeavyDamageHeadline:
                    return ResolveLocalized(LocalizationKeys.REPAIR_TOOL_HUD_HEAVY_DAMAGE, "REPAIR TOOL - HEAVY DAMAGE");
                case RepairToolPatchingHeadline:
                    return ResolveLocalized(LocalizationKeys.REPAIR_TOOL_HUD_PATCHING, "REPAIR TOOL - PATCHING");
                default:
                    return string.Format(
                        ResolveLocalized(LocalizationKeys.REPAIR_TOOL_HUD_GENERIC, "REPAIR TOOL - {0}"),
                        headline);
            }
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
                    return string.Format(
                        ResolveLocalized(LocalizationKeys.REPAIR_TOOL_LOG_DIAG_GENERIC, "SERVICE DIAG - {0}"),
                        headline);
            }
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = LocalizationManager.Instance;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
        }

        // ══════════════════════════════════════════════════════════
        //  IBatteryTool IMPLEMENTATION
        // ══════════════════════════════════════════════════════════

    }
}

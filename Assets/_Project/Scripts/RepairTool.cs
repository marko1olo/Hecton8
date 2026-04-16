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
using Hecton8.Gameplay;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class RepairTool : PlayerTool
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

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        private Transform _cachedTransform;
        private RaycastHit _hit;
        private readonly RaycastHit[] _repairHits = new RaycastHit[1]; // COLD ALLOC: repair tool needs only the nearest service contact.
        private bool _isRepairing;
        private bool _wasRepairingLastFrame;
        private bool _invalidTargetReportedThisUse;
        private bool _noTargetReportedThisUse;
        private bool _healthyTargetReportedThisUse;
        private bool _activeRepairReportedThisUse;
        private bool _secondaryLatched;
        private int _cachedDiagnosisFrame = -1;
        private bool _cachedDiagnosisValid;
        private ServiceDiagnosis _cachedDiagnosis;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
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

                module.Repair(repairSpeed * deltaTime);
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

            Hecton8.Input.InputManager input = Hecton8.Input.InputManager.Instance;
            if (input == null)
                return;

            if (!input.IsPrimaryActionHeld)
            {
                _invalidTargetReportedThisUse = false;
                _noTargetReportedThisUse = false;
                _healthyTargetReportedThisUse = false;
                _activeRepairReportedThisUse = false;
            }

            if (!input.IsSecondaryActionHeld)
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
            int hitCount = UnityEngine.Physics.RaycastNonAlloc(
                _cachedTransform.position,
                _cachedTransform.forward,
                _repairHits,
                repairRange,
                repairMask,
                QueryTriggerInteraction.Ignore);

            if (hitCount > 0)
            {
                hit = _repairHits[0];
                return true;
            }

            hit = default;
            return false;
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
    }
}

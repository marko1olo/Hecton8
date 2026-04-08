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
        private bool _isRepairing;
        private bool _wasRepairingLastFrame;
        private bool _invalidTargetReportedThisUse;
        private bool _noTargetReportedThisUse;
        private bool _healthyTargetReportedThisUse;
        private bool _activeRepairReportedThisUse;
        private bool _secondaryLatched;

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

            Vector3 origin = _cachedTransform.position;
            Vector3 direction = _cachedTransform.forward;

            bool didHit = UnityEngine.Physics.Raycast(
                origin,
                direction,
                out _hit,
                repairRange,
                repairMask,
                QueryTriggerInteraction.Ignore);

            if (!didHit)
            {
                if (!_noTargetReportedThisUse)
                {
                    ToolHitUtility.ShowWarning("REPAIR TOOL - NO TARGET");
                    _noTargetReportedThisUse = true;
                }
                UpdateBeamMiss();
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
                        ToolHitUtility.ShowInfo("REPAIR TOOL - MODULE SEALED");
                        _healthyTargetReportedThisUse = true;
                    }

                    UpdateBeamHit(_hit.point, _hit.normal);
                    return;
                }

                module.Repair(repairSpeed * deltaTime);
                UpdateBeamHit(_hit.point, _hit.normal);

                if (!_activeRepairReportedThisUse)
                {
                    ServiceDiagnosis diagnosis = BuildDiagnosis(module);
                    ToolHitUtility.ShowInfo(GetActiveRepairHudMessage(diagnosis.headline));
                    FieldOperationLogSystem.RecordOperation(
                        "REPAIR",
                        "MODULE REPAIR STARTED",
                        $"{module.name} entered active repair service. {diagnosis.summary} {diagnosis.recommendation}",
                        "INFO");
                    _activeRepairReportedThisUse = true;
                }

                if ((beforeIntegrity < module.MaxIntegrity || beforeFlooded) &&
                    module.CurrentIntegrity >= module.MaxIntegrity &&
                    !module.IsFlooded)
                {
                    ToolHitUtility.ShowInfo("REPAIR TOOL - MODULE RESTORED");
                    FieldOperationLogSystem.RecordOperation(
                        "REPAIR",
                        "MODULE RESTORED",
                        $"{module.name} reached full integrity and dry status.",
                        "INFO");
                }
            }
            else
            {
                if (!_invalidTargetReportedThisUse)
                {
                    ToolHitUtility.ShowWarning("REPAIR TOOL - INVALID TARGET");
                    _invalidTargetReportedThisUse = true;
                }
                UpdateBeamMiss();
            }
        }

        public override void UseSecondary(float deltaTime)
        {
            if (_secondaryLatched)
                return;

            _secondaryLatched = true;

            Vector3 origin = _cachedTransform.position;
            Vector3 direction = _cachedTransform.forward;

            bool didHit = UnityEngine.Physics.Raycast(
                origin,
                direction,
                out _hit,
                repairRange,
                repairMask,
                QueryTriggerInteraction.Ignore);

            if (!didHit)
            {
                ToolHitUtility.ShowWarning("REPAIR TOOL - NO MODULE IN RANGE");
                return;
            }

            BaseModule module =
                _hit.collider != null
                    ? _hit.collider.GetComponent<BaseModule>() ?? _hit.collider.GetComponentInParent<BaseModule>()
                    : null;
            if (module == null)
            {
                ToolHitUtility.ShowWarning("REPAIR TOOL - TARGET NOT SERVICEABLE");
                return;
            }

            ServiceDiagnosis diagnosis = BuildDiagnosis(module);
            PublishDiagnosis(diagnosis);
            FieldOperationLogSystem.RecordOperation(
                "REPAIR",
                GetServiceDiagnosisLogTitle(diagnosis.headline),
                $"{diagnosis.summary} {diagnosis.recommendation}",
                diagnosis.severity);
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
                return "REPAIR TOOL // ACTIVE SERVICE";

            if (TryReadServiceDiagnosis(out ServiceDiagnosis diagnosis))
                return $"REPAIR TOOL // {diagnosis.priority}";

            return "REPAIR TOOL // STANDBY";
        }

        public override string GetOperationalDirective()
        {
            if (_isRepairing)
                return "Hold the beam steady until the service window closes.";

            if (TryReadServiceDiagnosis(out ServiceDiagnosis diagnosis))
                return diagnosis.recommendation;

            return "Sweep a damaged module to diagnose or begin repair.";
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

            bool didHit = UnityEngine.Physics.Raycast(
                _cachedTransform.position,
                _cachedTransform.forward,
                out _hit,
                repairRange,
                repairMask,
                QueryTriggerInteraction.Ignore);

            BaseModule module =
                didHit && _hit.collider != null
                    ? _hit.collider.GetComponent<BaseModule>() ?? _hit.collider.GetComponentInParent<BaseModule>()
                    : null;
            if (module == null)
                return false;

            diagnosis = BuildDiagnosis(module);
            return true;
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
                    headline = "NO POWER",
                    summary = $"Integrity {(integrity01 * 100f):0}% // compartment flooded // pumps offline.",
                    recommendation = "Restore power before expecting water evacuation.",
                    severity = "WARN",
                    priority = "SERVICE BLOCKED"
                };
            }

            if (module.IsFlooded && module.IsDraining)
            {
                return new ServiceDiagnosis
                {
                    status = "DRAINING",
                    headline = "DRAINING",
                    summary = $"Integrity {(integrity01 * 100f):0}% // pumps are clearing floodwater.",
                    recommendation = "Hold perimeter and let the compartment finish draining.",
                    severity = "INFO",
                    priority = "STABILIZING"
                };
            }

            if (module.IsFlooded)
            {
                return new ServiceDiagnosis
                {
                    status = "FLOODED",
                    headline = "FLOODED",
                    summary = $"Integrity {(integrity01 * 100f):0}% // compartment breach still active.",
                    recommendation = "Continue repair until integrity reaches 100% and pump cycle can start.",
                    severity = "WARN",
                    priority = "IMMEDIATE SERVICE"
                };
            }

            if (integrity01 >= 0.999f)
            {
                return new ServiceDiagnosis
                {
                    status = "SEALED",
                    headline = "SEALED",
                    summary = "Integrity 100% // hull stable // compartment dry.",
                    recommendation = "No further repair action required.",
                    severity = "INFO",
                    priority = "SERVICE COMPLETE"
                };
            }

            if (integrity01 <= 0.25f)
            {
                return new ServiceDiagnosis
                {
                    status = "CRITICAL",
                    headline = "CRITICAL DAMAGE",
                    summary = $"Integrity {(integrity01 * 100f):0}% // hull failure risk elevated.",
                    recommendation = "Maintain continuous repair contact until the module exits critical range.",
                    severity = "CRITICAL",
                    priority = "CRITICAL RESPONSE"
                };
            }

            if (integrity01 <= 0.65f)
            {
                return new ServiceDiagnosis
                {
                    status = "DAMAGED",
                    headline = "HEAVY DAMAGE",
                    summary = $"Integrity {(integrity01 * 100f):0}% // hull is compromised but recoverable.",
                    recommendation = "Keep the repair beam on target and avoid leaving the module unattended.",
                    severity = "WARN",
                    priority = "ACTIVE SERVICE"
                };
            }

            return new ServiceDiagnosis
            {
                status = "DAMAGED",
                headline = "PATCHING",
                summary = $"Integrity {(integrity01 * 100f):0}% // module is nearly sealed.",
                recommendation = "Finish the repair cycle to restore full integrity.",
                severity = "INFO",
                priority = "FINAL PASS"
            };
        }

        private static void PublishDiagnosis(ServiceDiagnosis diagnosis)
        {
            string message = $"REPAIR DIAG - {diagnosis.headline} // {diagnosis.priority} // {diagnosis.summary} // {diagnosis.recommendation}";
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
                    return "REPAIR TOOL - NO POWER";
                case RepairToolDrainingHeadline:
                    return "REPAIR TOOL - DRAINING";
                case RepairToolFloodedHeadline:
                    return "REPAIR TOOL - FLOODED";
                case RepairToolSealedHeadline:
                    return "REPAIR TOOL - SEALED";
                case RepairToolCriticalDamageHeadline:
                    return "REPAIR TOOL - CRITICAL DAMAGE";
                case RepairToolHeavyDamageHeadline:
                    return "REPAIR TOOL - HEAVY DAMAGE";
                case RepairToolPatchingHeadline:
                    return "REPAIR TOOL - PATCHING";
                default:
                    return "REPAIR TOOL - " + headline;
            }
        }

        private static string GetServiceDiagnosisLogTitle(string headline)
        {
            switch (headline)
            {
                case RepairToolNoPowerHeadline:
                    return "SERVICE DIAG - NO POWER";
                case RepairToolDrainingHeadline:
                    return "SERVICE DIAG - DRAINING";
                case RepairToolFloodedHeadline:
                    return "SERVICE DIAG - FLOODED";
                case RepairToolSealedHeadline:
                    return "SERVICE DIAG - SEALED";
                case RepairToolCriticalDamageHeadline:
                    return "SERVICE DIAG - CRITICAL DAMAGE";
                case RepairToolHeavyDamageHeadline:
                    return "SERVICE DIAG - HEAVY DAMAGE";
                case RepairToolPatchingHeadline:
                    return "SERVICE DIAG - PATCHING";
                default:
                    return "SERVICE DIAG - " + headline;
            }
        }
    }
}

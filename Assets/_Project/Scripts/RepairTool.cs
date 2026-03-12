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
            SetRepairVisuals(false);
        }

        public override void OnDespawn()
        {
            _isRepairing = false;
            _wasRepairingLastFrame = false;
            SetRepairVisuals(false);
            base.OnDespawn();
        }

        public override void OnEquip()
        {
            base.OnEquip();
            _isRepairing = false;
            _wasRepairingLastFrame = false;
            SetRepairVisuals(false);
        }

        public override void OnUnequip()
        {
            _isRepairing = false;
            _wasRepairingLastFrame = false;
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
                UpdateBeamMiss();
                return;
            }

            if (_hit.collider != null &&
                _hit.collider.TryGetComponent(out BaseModule module))
            {
                module.Repair(repairSpeed * deltaTime);
                UpdateBeamHit(_hit.point, _hit.normal);
            }
            else
            {
                UpdateBeamMiss();
            }
        }

        public override void ToolTick(float deltaTime)
        {
            if (_wasRepairingLastFrame && !_isRepairing)
                SetRepairVisuals(false);

            _wasRepairingLastFrame = _isRepairing;
            _isRepairing = false;
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
    }
}
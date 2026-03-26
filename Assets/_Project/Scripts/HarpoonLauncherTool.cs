using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class HarpoonLauncherTool : PlayerTool
    {
        [Header("Harpoon")]
        [SerializeField] private float range = 36f;
        [SerializeField] private float damage = 42f;
        [SerializeField] private float impulse = 18f;
        [SerializeField] private float reelImpulse = 14f;
        [SerializeField] private float maxReelMass = 55f;
        [SerializeField] private float shotCooldown = 0.85f;
        [SerializeField] private LayerMask targetMask = ~0;

        [Header("Tracer")]
        [SerializeField] private LineRenderer tracer;
        [SerializeField] private float tracerLifetime = 0.08f;

        private Transform _cachedTransform;
        private float _cooldown;
        private float _tracerTimer;

        private void Awake()
        {
            _cachedTransform = transform;
            SetTracer(false, Vector3.zero);
        }

        public override void UsePrimary(float deltaTime)
        {
            base.UsePrimary(deltaTime);

            if (!IsEquipped || _cooldown > 0f)
                return;

            Vector3 endPoint = _cachedTransform.position + _cachedTransform.forward * range;

            if (UnityEngine.Physics.Raycast(
                _cachedTransform.position,
                _cachedTransform.forward,
                out RaycastHit hit,
                range,
                targetMask,
                QueryTriggerInteraction.Ignore))
            {
                endPoint = hit.point;
                ToolHitUtility.ApplyDamage(
                    hit.collider,
                    damage * GetEfficiency(),
                    hit.point,
                    _cachedTransform.forward,
                    impulse);
            }

            SetTracer(true, endPoint);
            _tracerTimer = tracerLifetime;
            _cooldown = shotCooldown / Mathf.Max(0.25f, GetSpeed());
        }

        public override void UseSecondary(float deltaTime)
        {
            base.UseSecondary(deltaTime);

            if (!IsEquipped || _cooldown > 0f)
                return;

            if (!UnityEngine.Physics.Raycast(
                _cachedTransform.position,
                _cachedTransform.forward,
                out RaycastHit hit,
                range,
                targetMask,
                QueryTriggerInteraction.Ignore))
            {
                return;
            }

            if (!ToolHitUtility.TryGetRigidbody(hit.collider, out Rigidbody body))
                return;

            if (body == null || body.isKinematic || body.mass > maxReelMass)
                return;

            Vector3 direction = (_cachedTransform.position - body.worldCenterOfMass).normalized;
            body.AddForce(direction * reelImpulse, ForceMode.Impulse);

            SetTracer(true, hit.point);
            _tracerTimer = tracerLifetime;
            _cooldown = shotCooldown * 0.65f;
        }

        public override void ToolTick(float deltaTime)
        {
            if (_cooldown > 0f)
                _cooldown = Mathf.Max(0f, _cooldown - deltaTime);

            if (_tracerTimer > 0f)
            {
                _tracerTimer -= deltaTime;
                if (_tracerTimer <= 0f)
                    SetTracer(false, Vector3.zero);
            }
        }

        private void SetTracer(bool active, Vector3 endPoint)
        {
            if (tracer == null)
                return;

            tracer.enabled = active;
            if (!active)
                return;

            tracer.SetPosition(0, Vector3.zero);
            tracer.SetPosition(1, _cachedTransform.InverseTransformPoint(endPoint));
        }
    }
}

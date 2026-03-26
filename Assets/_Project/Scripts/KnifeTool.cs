using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class KnifeTool : PlayerTool
    {
        [Header("Melee")]
        [SerializeField] private float range = 2.15f;
        [SerializeField] private float radius = 0.28f;
        [SerializeField] private float damage = 32f;
        [SerializeField] private float impulse = 4f;
        [SerializeField] private float swingCooldown = 0.35f;
        [SerializeField] private LayerMask hitMask = ~0;

        private static readonly RaycastHit[] HitBuffer = new RaycastHit[8];

        private Transform _cachedTransform;
        private float _cooldown;

        private void Awake()
        {
            _cachedTransform = transform;
        }

        public override void UsePrimary(float deltaTime)
        {
            base.UsePrimary(deltaTime);

            if (!IsEquipped || _cooldown > 0f)
                return;

            Vector3 origin = _cachedTransform.position;
            Vector3 direction = _cachedTransform.forward;
            int hitCount = UnityEngine.Physics.SphereCastNonAlloc(
                origin,
                radius,
                direction,
                HitBuffer,
                range,
                hitMask,
                QueryTriggerInteraction.Ignore);

            Collider bestCollider = null;
            Vector3 bestPoint = origin + direction * range;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                Collider candidate = HitBuffer[i].collider;
                if (candidate == null || candidate.transform == _cachedTransform || candidate.transform.IsChildOf(_cachedTransform))
                    continue;

                if (HitBuffer[i].distance < bestDistance)
                {
                    bestDistance = HitBuffer[i].distance;
                    bestCollider = candidate;
                    bestPoint = HitBuffer[i].point;
                }
            }

            if (bestCollider != null)
            {
                float effectiveDamage = damage * GetEfficiency();
                ToolHitUtility.ApplyDamage(bestCollider, effectiveDamage, bestPoint, direction, impulse);
            }

            for (int i = 0; i < hitCount; i++)
                HitBuffer[i] = default;

            _cooldown = swingCooldown / Mathf.Max(0.25f, GetSpeed());
        }

        public override void ToolTick(float deltaTime)
        {
            if (_cooldown > 0f)
                _cooldown = Mathf.Max(0f, _cooldown - deltaTime);
        }
    }
}

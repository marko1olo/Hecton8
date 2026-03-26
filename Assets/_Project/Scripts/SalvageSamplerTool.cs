using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class SalvageSamplerTool : PlayerTool
    {
        [Header("Sampling")]
        [SerializeField] private float samplingRange = 3.2f;
        [SerializeField] private float sampleDamage = 18f;
        [SerializeField] private float sampleImpulse = 1.5f;
        [SerializeField] private float sampleCooldown = 0.3f;
        [SerializeField] private LayerMask samplingMask = ~0;

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

            if (UnityEngine.Physics.Raycast(
                _cachedTransform.position,
                _cachedTransform.forward,
                out RaycastHit hit,
                samplingRange,
                samplingMask,
                QueryTriggerInteraction.Collide))
            {
                float effectiveDamage = sampleDamage * GetEfficiency();
                ToolHitUtility.ApplyDamage(
                    hit.collider,
                    effectiveDamage,
                    hit.point,
                    _cachedTransform.forward,
                    sampleImpulse);
            }

            _cooldown = sampleCooldown / Mathf.Max(0.25f, GetSpeed());
        }

        public override void UseSecondary(float deltaTime)
        {
            base.UseSecondary(deltaTime);

            if (!IsEquipped || _cooldown > 0f)
                return;

            if (UnityEngine.Physics.Raycast(
                _cachedTransform.position,
                _cachedTransform.forward,
                out RaycastHit hit,
                samplingRange,
                samplingMask,
                QueryTriggerInteraction.Collide))
            {
                ToolHitUtility.TryCollectItem(hit.collider, _cachedTransform.root);
            }

            _cooldown = sampleCooldown / Mathf.Max(0.25f, GetSpeed());
        }

        public override void ToolTick(float deltaTime)
        {
            if (_cooldown > 0f)
                _cooldown = Mathf.Max(0f, _cooldown - deltaTime);
        }
    }
}

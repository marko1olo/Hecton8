using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class PropulsionTool : PlayerTool
    {
        [Header("Propulsion")]
        [SerializeField] private float range = 18f;
        [SerializeField] private float pushForce = 85f;
        [SerializeField] private float pullForce = 62f;
        [SerializeField] private float maxTargetMass = 400f;
        [SerializeField] private LayerMask targetMask = ~0;

        private Transform _cachedTransform;

        private void Awake()
        {
            _cachedTransform = transform;
        }

        public override void UsePrimary(float deltaTime)
        {
            base.UsePrimary(deltaTime);
            ApplyDirectedForce(pushForce * GetEfficiency(), true);
        }

        public override void UseSecondary(float deltaTime)
        {
            base.UseSecondary(deltaTime);
            ApplyDirectedForce(pullForce * GetEfficiency(), false);
        }

        private void ApplyDirectedForce(float force, bool pushAway)
        {
            if (!IsEquipped || force <= 0f)
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

            if (body == null || body.isKinematic || body.mass > maxTargetMass)
                return;

            Vector3 direction = pushAway
                ? _cachedTransform.forward
                : (_cachedTransform.position - body.worldCenterOfMass);

            if (direction.sqrMagnitude < 0.0001f)
                return;

            body.AddForce(direction.normalized * force, ForceMode.Force);
        }
    }
}

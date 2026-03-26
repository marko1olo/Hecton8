using Hecton8.AI;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class StunPistolTool : PlayerTool
    {
        [Header("Stun Shot")]
        [SerializeField] private float range = 22f;
        [SerializeField] private float damage = 10f;
        [SerializeField] private float impulse = 9f;
        [SerializeField] private float stunDuration = 2.5f;
        [SerializeField] private float shotCooldown = 0.6f;
        [SerializeField] private LayerMask targetMask = ~0;

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
                range,
                targetMask,
                QueryTriggerInteraction.Ignore))
            {
                ToolHitUtility.ApplyDamage(
                    hit.collider,
                    damage * GetEfficiency(),
                    hit.point,
                    _cachedTransform.forward,
                    impulse);

                HectonBaseAI ai = hit.collider.GetComponent<HectonBaseAI>();
                if (ai == null)
                    ai = hit.collider.GetComponentInParent<HectonBaseAI>();

                if (ai != null)
                {
                    StunTargetRuntime stunState = ai.GetComponent<StunTargetRuntime>();
                    if (stunState == null)
                        stunState = ai.gameObject.AddComponent<StunTargetRuntime>();

                    stunState.Apply(ai, stunDuration);
                }
            }

            _cooldown = shotCooldown;
        }

        public override void ToolTick(float deltaTime)
        {
            if (_cooldown > 0f)
                _cooldown = Mathf.Max(0f, _cooldown - deltaTime);
        }
    }

    public sealed class StunTargetRuntime : MonoBehaviour
    {
        private HectonBaseAI _target;
        private float _remaining;
        private bool _armed;

        public void Apply(HectonBaseAI target, float duration)
        {
            _target = target;
            _remaining = Mathf.Max(_remaining, duration);

            if (_target != null && _target.enabled)
            {
                _target.enabled = false;
                _armed = true;
            }
        }

        private void Update()
        {
            if (!_armed)
                return;

            _remaining -= Time.deltaTime;
            if (_remaining > 0f)
                return;

            if (_target != null)
                _target.enabled = true;

            _armed = false;
            _remaining = 0f;
        }

        private void OnDisable()
        {
            if (_target != null)
                _target.enabled = true;

            _armed = false;
            _remaining = 0f;
        }
    }
}

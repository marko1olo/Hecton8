using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class HarpoonLauncherTool : PlayerTool
    {
        private static Material s_tracerMaterial;

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
            EnsureTracer();
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

        private void EnsureTracer()
        {
            if (tracer != null)
                return;

            GameObject tracerRoot = new GameObject("Tracer");
            tracerRoot.transform.SetParent(transform, false);
            tracerRoot.transform.localPosition = Vector3.zero;
            tracerRoot.transform.localRotation = Quaternion.identity;

            tracer = tracerRoot.AddComponent<LineRenderer>();
            tracer.alignment = LineAlignment.View;
            tracer.useWorldSpace = false;
            tracer.positionCount = 2;
            tracer.startWidth = 0.012f;
            tracer.endWidth = 0.005f;
            tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tracer.receiveShadows = false;
            tracer.textureMode = LineTextureMode.Stretch;
            tracer.numCapVertices = 2;
            tracer.sharedMaterial = GetTracerMaterial();
            tracer.startColor = new Color(0.46f, 0.98f, 0.94f, 0.95f);
            tracer.endColor = new Color(0.46f, 0.98f, 0.94f, 0.2f);
            tracer.enabled = false;
        }

        private static Material GetTracerMaterial()
        {
            if (s_tracerMaterial != null)
                return s_tracerMaterial;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            s_tracerMaterial = new Material(shader);
            return s_tracerMaterial;
        }
    }
}

using Hecton8.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    /// <summary>
    /// Lightweight spline-like runtime cable used by abyssal bio-cable zones.
    /// Manager-driven only: no Update, no per-frame allocations.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class BioCableIK : MonoBehaviour
    {
        private static Material s_FallbackCableMaterial;

        [Header("── Runtime Wiring ──────────────────")]
        [SerializeField]
        [Tooltip("Cached line renderer used to draw the abyssal cable.")]
        private LineRenderer lineRenderer;

        [Header("── Cable Shape ─────────────────────")]
        [SerializeField, Range(4, 24)]
        [Tooltip("Segment count used by the light IK chain.")]
        private int segmentCount = 12;

        [SerializeField, Min(0.1f)]
        [Tooltip("Rest distance preserved between cable points.")]
        private float segmentLength = 1.25f;

        [SerializeField, Range(0f, 32f)]
        [Tooltip("Spring pull toward the live attractor point near the scooter hull.")]
        private float attractorSpring = 9.5f;

        [SerializeField, Range(0f, 4f)]
        [Tooltip("Velocity damping applied to cable segments each simulation step.")]
        private float damping = 1.45f;

        [SerializeField, Range(0f, 3f)]
        [Tooltip("Additional wrap bias that coils the last third of the cable around a stuck scooter.")]
        private float wrapStrength = 1.2f;

        [SerializeField, Range(0.01f, 1f)]
        [Tooltip("Cable width at the anchor root.")]
        private float rootWidth = 0.18f;

        [SerializeField, Range(0.01f, 1f)]
        [Tooltip("Cable width at the free end.")]
        private float tipWidth = 0.06f;

        [Header("── EMP Charge Visuals ─────────────")]
        [SerializeField]
        [Tooltip("Calm cable tint used when the nest is idle.")]
        private Color baseCableColor = new Color(0.12f, 0.52f, 0.46f, 0.92f);

        [SerializeField]
        [Tooltip("Hot white tint used when an EMP nest reaches charge-up state.")]
        private Color empChargeColor = new Color(0.95f, 0.98f, 1f, 0.98f);

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Minimum charge before spark emission starts.")]
        private float sparkChargeThreshold = 0.28f;

        [SerializeField, Range(0f, 128f)]
        [Tooltip("Maximum spark emission rate used during EMP pre-fire charging.")]
        private float sparkEmissionRate = 42f;

        [SerializeField, Range(0f, 4f)]
        [Tooltip("Extra width added while the cable is fully charged.")]
        private float empWidthBoost = 0.3f;

        [Header("── Snap Recoil ───────────────────")]
        [SerializeField, Range(0f, 4f)]
        [Tooltip("Additional damping applied after the cable has been severed and is recoiling.")]
        private float snapDamping = 1.8f;

        [SerializeField, Range(0f, 8f)]
        [Tooltip("How strongly severed cable segments continue to inherit the snap velocity during recoil.")]
        private float snapVelocityCarry = 2.6f;

        [Header("── Elastic Rupture ─────────────────")]
        [SerializeField, Range(1f, 2.5f)]
        [Tooltip("Normalized chain stretch ratio where the cable starts accumulating rupture strain.")]
        private float elasticStretchLimit = 1.42f;

        [SerializeField, Range(0.02f, 1f)]
        [Tooltip("How long the cable must stay beyond the stretch limit before it ruptures.")]
        private float elasticBreakHoldTime = 0.14f;

        [SerializeField, Range(0f, 4f)]
        [Tooltip("Extra recoil multiplier injected when the cable bursts from over-tension instead of a cutter release.")]
        private float elasticBreakRecoilMultiplier = 1.35f;

        // COLD ALLOC: Vector3[24] - cable point positions for manager-driven IK simulation - owner: BioCableIK
        private Vector3[] _points;
        // COLD ALLOC: Vector3[24] - cable point velocities for manager-driven IK simulation - owner: BioCableIK
        private Vector3[] _velocities;

        private Vector3 _anchorPositionWS;
        private Vector3 _anchorUpWS = Vector3.up;
        private Vector3 _snapVelocityWS;
        private float _oscillationTime;
        private float _empCharge01;
        private float _empPulse01;
        private float _snapTimer;
        private float _snapDuration;
        private float _elasticBreakTimer;
        private float _debugStretchRatio;
        private bool _initialized;
        private bool _pendingElasticRupture;
        private Vector3 _pendingElasticRuptureVelocityWS;
        private ParticleSystem _sparkParticles;
        private ParticleSystemRenderer _sparkRenderer;

        private void Awake()
        {
            ResolveRuntimeWiring();
            EnsureStorage();
            EnsureChargeEffects();
            InitializeAt(transform.position, Vector3.up);
        }

        /// <summary>
        /// Resets the cable chain to a stable authored anchor pose.
        /// </summary>
        public void InitializeAt(Vector3 anchorPositionWS, Vector3 anchorUpWS)
        {
            ResolveRuntimeWiring();
            EnsureStorage();

            _anchorPositionWS = anchorPositionWS;
            _anchorUpWS = anchorUpWS.sqrMagnitude > 0.0001f ? anchorUpWS.normalized : Vector3.up;
            _snapVelocityWS = Vector3.zero;
            _snapTimer = 0f;
            _snapDuration = 0f;
            _elasticBreakTimer = 0f;
            _debugStretchRatio = 1f;
            _pendingElasticRupture = false;
            _pendingElasticRuptureVelocityWS = Vector3.zero;

            for (int i = 0; i < _points.Length; i++)
            {
                _points[i] = _anchorPositionWS - _anchorUpWS * (segmentLength * i);
                _velocities[i] = Vector3.zero;
            }

            ApplyVisualState();
            SyncRenderer();
            _initialized = true;
        }

        /// <summary>
        /// Advances the cable toward the live scooter hull without requiring per-segment colliders.
        /// </summary>
        public void TickCable(
            Vector3 anchorPositionWS,
            Vector3 anchorUpWS,
            Vector3 attractorPositionWS,
            Vector3 attractorVelocityWS,
            float attraction01,
            float wrap01,
            float dt)
        {
            if (!_initialized)
                InitializeAt(anchorPositionWS, anchorUpWS);

            _anchorPositionWS = anchorPositionWS;
            _anchorUpWS = anchorUpWS.sqrMagnitude > 0.0001f ? anchorUpWS.normalized : Vector3.up;
            _points[0] = _anchorPositionWS;
            _velocities[0] = Vector3.zero;
            _snapTimer = 0f;
            _snapDuration = 0f;
            _snapVelocityWS = Vector3.zero;
            _pendingElasticRupture = false;
            _pendingElasticRuptureVelocityWS = Vector3.zero;

            float deltaTime = Mathf.Max(0f, dt);
            _oscillationTime += deltaTime;
            float clampedAttraction = Mathf.Clamp01(attraction01);
            float clampedWrap = Mathf.Clamp01(wrap01);
            Vector3 velocityBias = attractorVelocityWS * Mathf.Lerp(0.08f, 0.42f, clampedWrap);
            Vector3 wrapAxis = Vector3.Cross(_anchorUpWS, attractorVelocityWS.sqrMagnitude > 0.0001f ? attractorVelocityWS.normalized : Vector3.forward);
            if (wrapAxis.sqrMagnitude <= 0.0001f)
                wrapAxis = Vector3.Cross(_anchorUpWS, Vector3.right);
            wrapAxis.Normalize();

            for (int i = 1; i < _points.Length; i++)
            {
                float tail01 = i / (float)(_points.Length - 1);
                Vector3 point = _points[i];
                Vector3 velocity = _velocities[i];

                Vector3 restPosition = _points[i - 1] - _anchorUpWS * segmentLength;
                Vector3 springForce = (restPosition - point) * Mathf.Lerp(3.6f, 6.8f, tail01);

                Vector3 toAttractor = attractorPositionWS - point;
                Vector3 attractForce = toAttractor * (attractorSpring * Mathf.Lerp(0.2f, 1f, tail01) * clampedAttraction);

                Vector3 wrapOffset = wrapAxis * Mathf.Sin((tail01 * 4.5f + _oscillationTime * 1.9f)) * segmentLength * 0.55f;
                Vector3 wrapForce = wrapOffset * (wrapStrength * clampedWrap * Mathf.SmoothStep(0f, 1f, tail01));

                velocity += (springForce + attractForce + wrapForce + velocityBias) * deltaTime;
                velocity *= Mathf.Clamp01(1f - damping * deltaTime * 0.35f);

                point += velocity * deltaTime;

                Vector3 toPrevious = point - _points[i - 1];
                float distance = toPrevious.magnitude;
                if (distance > 0.0001f)
                    point = _points[i - 1] + toPrevious * (segmentLength / distance);
                else
                    point = _points[i - 1] - _anchorUpWS * segmentLength;

                _points[i] = point;
                _velocities[i] = velocity;
            }

            UpdateElasticRupture(deltaTime, attractorPositionWS, clampedAttraction);
            UpdateSparkAnchor();
            ApplyVisualState();
            SyncRenderer();
        }

        /// <summary>
        /// Advances a severed cable through its recoil window after the cutter snaps the bio-cable.
        /// </summary>
        public void TickReleased(Vector3 anchorPositionWS, Vector3 anchorUpWS, float dt)
        {
            if (!_initialized)
                InitializeAt(anchorPositionWS, anchorUpWS);

            _anchorPositionWS = anchorPositionWS;
            _anchorUpWS = anchorUpWS.sqrMagnitude > 0.0001f ? anchorUpWS.normalized : Vector3.up;
            _points[0] = _anchorPositionWS;
            _velocities[0] = Vector3.zero;
            _pendingElasticRupture = false;
            _pendingElasticRuptureVelocityWS = Vector3.zero;

            float deltaTime = Mathf.Max(0f, dt);
            _oscillationTime += deltaTime;
            float recoilGate = _snapDuration > 0.0001f ? Mathf.Clamp01(_snapTimer / _snapDuration) : 0f;
            if (_snapTimer > 0f)
            {
                _snapTimer -= deltaTime;
                if (_snapTimer < 0f)
                    _snapTimer = 0f;
            }

            for (int i = 1; i < _points.Length; i++)
            {
                float tail01 = i / (float)(_points.Length - 1);
                Vector3 point = _points[i];
                Vector3 velocity = _velocities[i];

                Vector3 restPosition = _points[i - 1] - _anchorUpWS * segmentLength;
                Vector3 springForce = (restPosition - point) * Mathf.Lerp(2.8f, 5.4f, tail01);
                Vector3 recoilForce = _snapVelocityWS * (snapVelocityCarry * recoilGate * Mathf.Lerp(0.4f, 1f, tail01));
                velocity += (springForce + recoilForce) * deltaTime;
                velocity *= Mathf.Clamp01(1f - (damping + snapDamping * recoilGate) * deltaTime * 0.35f);

                point += velocity * deltaTime;

                Vector3 toPrevious = point - _points[i - 1];
                float distance = toPrevious.magnitude;
                if (distance > 0.0001f)
                    point = _points[i - 1] + toPrevious * (segmentLength / distance);
                else
                    point = _points[i - 1] - _anchorUpWS * segmentLength;

                _points[i] = point;
                _velocities[i] = velocity;
            }

            UpdateSparkAnchor();
            ApplyVisualState();
            SyncRenderer();
        }

        /// <summary>
        /// Returns and clears the pending elastic-rupture event generated by over-tension.
        /// </summary>
        public bool ConsumeElasticRupture(out Vector3 ruptureVelocityWS)
        {
            ruptureVelocityWS = _pendingElasticRuptureVelocityWS;
            bool hadEvent = _pendingElasticRupture;
            _pendingElasticRupture = false;
            _pendingElasticRuptureVelocityWS = Vector3.zero;
            return hadEvent;
        }

        /// <summary>
        /// Updates the deterministic EMP pre-fire visual state without allocating materials or gradients.
        /// </summary>
        public void SetEmpCharge(float charge01, float pulse01)
        {
            _empCharge01 = Mathf.Clamp01(charge01);
            _empPulse01 = Mathf.Clamp01(pulse01);
            ApplyVisualState();
        }

        /// <summary>
        /// Injects a sever snap impulse so the cable visibly recoils instead of disappearing on release.
        /// </summary>
        public void TriggerSnapRecoil(Vector3 recoilVelocityWS, float duration)
        {
            EnsureStorage();
            _snapVelocityWS = recoilVelocityWS;
            _snapDuration = Mathf.Max(0.1f, duration);
            _snapTimer = _snapDuration;

            for (int i = 1; i < _velocities.Length; i++)
            {
                float tail01 = i / (float)(_velocities.Length - 1);
                _velocities[i] += recoilVelocityWS * Mathf.Lerp(0.35f, 1f, tail01);
            }
        }

        /// <summary>
        /// Returns true while the severed cable still has active recoil motion to render.
        /// </summary>
        public bool HasTransientMotion => _snapTimer > 0.0001f;

        /// <summary>
        /// Enables or disables the cable renderer without destroying runtime storage.
        /// </summary>
        public void SetCableActive(bool isActive)
        {
            if (lineRenderer != null)
                lineRenderer.enabled = isActive;

            if (!isActive && _sparkParticles != null && _sparkParticles.isPlaying)
                _sparkParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            if (!isActive)
            {
                _pendingElasticRupture = false;
                _pendingElasticRuptureVelocityWS = Vector3.zero;
                _elasticBreakTimer = 0f;
            }
        }

        private void ResolveRuntimeWiring()
        {
            if (lineRenderer == null)
                TryGetComponent(out lineRenderer);

            if (lineRenderer != null)
            {
                lineRenderer.positionCount = Mathf.Max(2, segmentCount);
                lineRenderer.widthMultiplier = 1f;
                lineRenderer.startWidth = rootWidth;
                lineRenderer.endWidth = tipWidth;
                lineRenderer.startColor = baseCableColor;
                lineRenderer.endColor = baseCableColor;
                lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
                lineRenderer.receiveShadows = false;
                lineRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                lineRenderer.alignment = LineAlignment.View;
                lineRenderer.textureMode = LineTextureMode.Stretch;
                if (lineRenderer.sharedMaterial == null)
                    lineRenderer.sharedMaterial = ResolveFallbackCableMaterial();
            }
        }

        private void EnsureStorage()
        {
            int clampedSegmentCount = Mathf.Clamp(segmentCount, 4, 24);
            if (_points == null || _points.Length != clampedSegmentCount)
            {
                // COLD ALLOC: Vector3[24] - cable point positions for manager-driven IK simulation - owner: BioCableIK
                _points = new Vector3[clampedSegmentCount];
                // COLD ALLOC: Vector3[24] - cable point velocities for manager-driven IK simulation - owner: BioCableIK
                _velocities = new Vector3[clampedSegmentCount];
            }
        }

        private void UpdateElasticRupture(float deltaTime, Vector3 attractorPositionWS, float attraction01)
        {
            if (_points == null || _points.Length <= 1)
                return;

            float restLength = segmentLength * (_points.Length - 1);
            if (restLength <= 0.0001f)
                return;

            float tailDistance = Vector3.Distance(_points[_points.Length - 1], _anchorPositionWS);
            float attractorDistance = Vector3.Distance(attractorPositionWS, _anchorPositionWS);
            float effectiveDistance = Mathf.Max(tailDistance, attractorDistance);
            float stretchRatio = effectiveDistance / restLength;
            _debugStretchRatio = stretchRatio;
            if (stretchRatio <= elasticStretchLimit)
            {
                _elasticBreakTimer = 0f;
                return;
            }

            float overStretch01 = Mathf.Clamp01((stretchRatio - elasticStretchLimit) / Mathf.Max(elasticStretchLimit, 0.001f));
            _elasticBreakTimer += deltaTime * Mathf.Lerp(0.35f, 1.35f, Mathf.Clamp01(attraction01 + overStretch01));
            if (_elasticBreakTimer < elasticBreakHoldTime || _pendingElasticRupture)
                return;

            Vector3 ruptureDirection = _points[_points.Length - 1] - _points[_points.Length - 2];
            if (ruptureDirection.sqrMagnitude <= 0.0001f)
                ruptureDirection = attractorPositionWS - _anchorPositionWS;
            if (ruptureDirection.sqrMagnitude <= 0.0001f)
                ruptureDirection = Vector3.up;

            _pendingElasticRuptureVelocityWS =
                ruptureDirection.normalized * (segmentLength * elasticBreakRecoilMultiplier * Mathf.Lerp(1f, 3.2f, overStretch01)) +
                _velocities[_velocities.Length - 1] * elasticBreakRecoilMultiplier;
            _pendingElasticRupture = true;
            _elasticBreakTimer = 0f;
        }

        private void EnsureChargeEffects()
        {
            if (_sparkParticles != null)
                return;

            Transform existing = transform.Find("CableSparkFX");
            GameObject sparkObject;
            if (existing != null)
            {
                sparkObject = existing.gameObject;
            }
            else
            {
                // COLD ALLOC: GameObject[1] - persistent EMP spark child for abyssal cable charge-up visuals - owner: BioCableIK
                sparkObject = new GameObject("CableSparkFX");
                sparkObject.transform.SetParent(transform, false);
                sparkObject.transform.localPosition = Vector3.zero;
                sparkObject.transform.localRotation = Quaternion.identity;
                sparkObject.transform.localScale = Vector3.one;
            }

            if (!_sparkParticles && !sparkObject.TryGetComponent(out _sparkParticles))
            {
                // COLD ALLOC: Component[1] - persistent particle system used for EMP nest pre-fire sparks - owner: BioCableIK
                _sparkParticles = sparkObject.AddComponent<ParticleSystem>();
            }

            if (_sparkParticles != null)
            {
                var main = _sparkParticles.main;
                main.playOnAwake = false;
                main.loop = true;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = 0.18f;
                main.startSpeed = 0.45f;
                main.startSize = 0.08f;
                main.maxParticles = 48;
                main.startColor = new Color(0.92f, 0.96f, 1f, 0.92f);

                var emission = _sparkParticles.emission;
                emission.enabled = true;
                emission.rateOverTime = 0f;

                var shape = _sparkParticles.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.05f;

                if (!_sparkParticles.isPlaying)
                    _sparkParticles.Play();
            }

            if (_sparkRenderer == null && _sparkParticles != null)
                _sparkParticles.TryGetComponent(out _sparkRenderer);

            if (_sparkRenderer != null && _sparkRenderer.sharedMaterial == null && lineRenderer != null)
                _sparkRenderer.sharedMaterial = lineRenderer.sharedMaterial;
        }

        private void ApplyVisualState()
        {
            if (lineRenderer != null)
            {
                float chargeBlend = Mathf.Clamp01(_empCharge01 * Mathf.Lerp(0.35f, 1f, _empPulse01));
                Color drawColor = Color.Lerp(baseCableColor, empChargeColor, chargeBlend);
                lineRenderer.startColor = drawColor;
                lineRenderer.endColor = Color.Lerp(drawColor, empChargeColor, chargeBlend * 0.5f);
                lineRenderer.widthMultiplier = 1f + empWidthBoost * chargeBlend;
            }

            if (_sparkParticles != null)
            {
                float sparkGate = Mathf.Clamp01((_empCharge01 - sparkChargeThreshold) / Mathf.Max(1f - sparkChargeThreshold, 0.001f));
                var emission = _sparkParticles.emission;
                emission.rateOverTime = sparkEmissionRate * sparkGate * Mathf.Lerp(0.25f, 1f, _empPulse01);
                if (!_sparkParticles.isPlaying && sparkGate > 0f)
                    _sparkParticles.Play();
                else if (_sparkParticles.isPlaying && sparkGate <= 0f && (lineRenderer == null || !lineRenderer.enabled))
                    _sparkParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void UpdateSparkAnchor()
        {
            if (_sparkParticles == null || _points == null || _points.Length == 0)
                return;

            int sparkIndex = Mathf.Min(2, _points.Length - 1);
            Transform sparkTransform = _sparkParticles.transform;
            sparkTransform.position = _points[sparkIndex];
        }

        private void SyncRenderer()
        {
            if (lineRenderer == null || _points == null)
                return;

            lineRenderer.positionCount = _points.Length;
            lineRenderer.startWidth = rootWidth;
            lineRenderer.endWidth = tipWidth;
            lineRenderer.SetPositions(_points);
        }

        private static Material ResolveFallbackCableMaterial()
        {
            if (s_FallbackCableMaterial != null)
                return s_FallbackCableMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                return null;

            // COLD ALLOC: Material[1] - shared fallback material for abyssal cable line renderers - owner: BioCableIK
            s_FallbackCableMaterial = new Material(shader)
            {
                name = "MAT_Runtime_BioCableIK"
            };
            s_FallbackCableMaterial.SetColor("_BaseColor", new Color(0.12f, 0.52f, 0.46f, 0.92f));
            return s_FallbackCableMaterial;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            segmentCount = Mathf.Clamp(segmentCount, 4, 24);
            segmentLength = Mathf.Max(0.1f, segmentLength);
            attractorSpring = Mathf.Clamp(attractorSpring, 0f, 32f);
            damping = Mathf.Clamp(damping, 0f, 4f);
            wrapStrength = Mathf.Clamp(wrapStrength, 0f, 3f);
            rootWidth = Mathf.Clamp(rootWidth, 0.01f, 1f);
            tipWidth = Mathf.Clamp(tipWidth, 0.01f, rootWidth);
            sparkChargeThreshold = Mathf.Clamp01(sparkChargeThreshold);
            sparkEmissionRate = Mathf.Clamp(sparkEmissionRate, 0f, 128f);
            empWidthBoost = Mathf.Clamp(empWidthBoost, 0f, 4f);
            snapDamping = Mathf.Clamp(snapDamping, 0f, 4f);
            snapVelocityCarry = Mathf.Clamp(snapVelocityCarry, 0f, 8f);
            elasticStretchLimit = Mathf.Clamp(elasticStretchLimit, 1f, 2.5f);
            elasticBreakHoldTime = Mathf.Clamp(elasticBreakHoldTime, 0.02f, 1f);
            elasticBreakRecoilMultiplier = Mathf.Clamp(elasticBreakRecoilMultiplier, 0f, 4f);

            ResolveRuntimeWiring();
            EnsureStorage();
            if (!_initialized)
                InitializeAt(transform.position, Vector3.up);
            else
            {
                ApplyVisualState();
                SyncRenderer();
            }
        }
#endif
    }
}

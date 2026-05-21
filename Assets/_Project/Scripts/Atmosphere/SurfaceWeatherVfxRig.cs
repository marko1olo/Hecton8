using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Atmosphere
{
    /// <summary>
    /// Scene-local presenter for above-water lightning geometry. Rain is rendered by the screen-space weather shader.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SurfaceWeatherVfxRig : MonoBehaviour, IOriginShiftListener
    {
        private const int BoltPointCount = 6;
        private const float BoltDurationSeconds = 0.14f;
        private static readonly int _SurfaceSplashImpulseId = Shader.PropertyToID("_HectonSurfaceSplashImpulse");

        [Header("Lightning")]
        [Tooltip("Height of the lightning source point above the water surface.")]
        [SerializeField, Min(5f)] private float lightningHeight = 95f;

        [Tooltip("Pre-authored line renderer used for lightning bolts. Runtime GameObject/AddComponent creation is forbidden.")]
        [SerializeField] private LineRenderer authoredBoltRenderer;

        [Tooltip("Shared line-renderer material used by lightning bolts. Runtime fallback material creation is forbidden.")]
        [SerializeField] private Material lightningBoltMaterial;

        private LineRenderer _boltRenderer;
        private float _boltTimer;
        private float _boltIntensity;
        private bool _loggedMissingBoltMaterial;
        private bool _loggedMissingBoltRenderer;

        // COLD ALLOC: Vector3[6] - reusable bolt polyline points for world lightning - owner: SurfaceWeatherVfxRig
        private readonly Vector3[] _boltPoints = new Vector3[BoltPointCount];

        private void Awake()
        {
            EnsureRigBuilt();
            ClearState();
        }

        private void OnEnable()
        {
            HectonFloatingOrigin.RegisterListener(this);
        }

        private void OnDisable()
        {
            HectonFloatingOrigin.UnregisterListener(this);
        }

        private void OnDestroy()
        {
            HectonFloatingOrigin.UnregisterListener(this);
        }

        internal void ConfigureAuthoring(Material authoredLightningBoltMaterial)
        {
            if (authoredLightningBoltMaterial == null)
                return;

            lightningBoltMaterial = authoredLightningBoltMaterial;
            if (_boltRenderer != null)
                _boltRenderer.sharedMaterial = lightningBoltMaterial;
        }

        /// <summary>
        /// Fades active lightning geometry. Rain state is owned by shader globals.
        /// </summary>
        internal void ApplyState(
            float deltaTime,
            Vector3 followPosition,
            float surfaceY,
            Vector2 windDirection,
            float precipitationIntensity,
            float localRainAreaScale,
            float localRainDensityMultiplier,
            float surfaceImpactRadiusScale,
            float surfaceImpactDensityMultiplier,
            bool active)
        {
            EnsureRigBuilt();
            UpdateLightningState(deltaTime);
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            if (!isActiveAndEnabled || shiftOffset.sqrMagnitude <= 0.000001f)
                return;

            RebaseBoltPositions(shiftOffset);
        }

        /// <summary>
        /// Triggers one world lightning bolt. Directional-light flash is applied by the celestial lighting path.
        /// </summary>
        internal void TriggerLightningStrike(
            Vector3 impactPosition,
            Vector2 windDirection,
            float flashStrength,
            float randomA,
            float randomB,
            float boltWidthMultiplier,
            float lightRangeMultiplier)
        {
            EnsureRigBuilt();
            if (_boltRenderer == null)
                return;

            Vector3 strikeDirection = new Vector3(windDirection.x, 0f, windDirection.y);
            if (strikeDirection.sqrMagnitude < 0.0001f)
            {
                float angle = randomA * math.PI * 2f;
                strikeDirection.x = CinematicMath.FastCos(angle);
                strikeDirection.z = CinematicMath.FastSin(angle);
            }

            strikeDirection = ResolveSafeDirection(strikeDirection, Vector3.forward);

            Vector3 endPoint = impactPosition;
            Vector3 startPoint = endPoint + Vector3.up * lightningHeight;
            Vector3 side = Vector3.Cross(Vector3.up, strikeDirection);
            if (side.sqrMagnitude < 0.0001f)
                side = Vector3.right;
            side = ResolveSafeDirection(side, Vector3.right);

            Vector3 windOffset = new Vector3(windDirection.x, 0f, windDirection.y) * 3f;

            for (int i = 0; i < BoltPointCount; i++)
            {
                float t = i / (BoltPointCount - 1f);
                Vector3 point = startPoint + ((endPoint - startPoint) * t);
                float jitterAmplitude = LerpClamped(14f, 1.5f, t);
                float phase = (randomA * 13.37f) + (randomB * 7.11f) + (i * 1.618f);
                point += side * (CinematicMath.FastSin(phase) * jitterAmplitude);
                point += windOffset * (1f - t) * 0.2f;
                _boltPoints[i] = point;
            }

            _boltPoints[0] = startPoint;
            _boltPoints[BoltPointCount - 1] = endPoint;

            _boltRenderer.positionCount = BoltPointCount;
            _boltRenderer.SetPositions(_boltPoints);
            float widthScale = Mathf.Max(0.5f, boltWidthMultiplier);
            float flash01 = math.saturate(flashStrength);
            _boltRenderer.startWidth = LerpClamped(0.9f, 1.6f, flash01) * widthScale;
            _boltRenderer.endWidth = LerpClamped(0.12f, 0.24f, flash01) * widthScale;
            _boltRenderer.enabled = true;

            _boltTimer = BoltDurationSeconds;
            _boltIntensity = Mathf.Clamp01(flashStrength);
        }

        /// <summary>
        /// Publishes one player-scale splash impulse. The ocean shader expands the ripple without CPU decal registration.
        /// </summary>
        internal void TriggerSurfaceSplashBurst(
            Vector3 centerPosition,
            float surfaceY,
            Vector2 windDirection,
            float intensity)
        {
            float clampedIntensity = Mathf.Clamp01(intensity);
            if (clampedIntensity <= 0.0001f)
                return;

            Vector4 impulse;
            impulse.x = centerPosition.x;
            impulse.y = centerPosition.z;
            impulse.z = ResolveWeatherShaderClockSeconds();
            impulse.w = clampedIntensity;
            Shader.SetGlobalVector(_SurfaceSplashImpulseId, impulse);
        }

        private static float ResolveWeatherShaderClockSeconds()
        {
            return Time.timeSinceLevelLoad;
        }

        /// <summary>
        /// Hides any active lightning geometry.
        /// </summary>
        internal void ClearState()
        {
            _boltTimer = 0f;
            _boltIntensity = 0f;

            if (_boltRenderer != null)
                _boltRenderer.enabled = false;
        }

        private void EnsureRigBuilt()
        {
            if (_boltRenderer != null)
                return;

            ResolveBoltRenderer();
        }

        private void ResolveBoltRenderer()
        {
            _boltRenderer = authoredBoltRenderer;
            if (_boltRenderer == null && !TryGetComponent(out _boltRenderer))
            {
                if (!_loggedMissingBoltRenderer)
                {
                    _loggedMissingBoltRenderer = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError("[SurfaceWeatherVfxRig] Missing authored LineRenderer. Add it to this rig or assign authoredBoltRenderer; runtime renderer creation is forbidden.", this);
#endif
                }

                return;
            }

            ConfigureBoltRenderer(_boltRenderer);
        }

        private void ConfigureBoltRenderer(LineRenderer renderer)
        {
            if (renderer == null)
                return;

            _boltRenderer.alignment = LineAlignment.View;
            _boltRenderer.textureMode = LineTextureMode.Stretch;
            _boltRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _boltRenderer.receiveShadows = false;
            _boltRenderer.useWorldSpace = true;
            _boltRenderer.numCapVertices = 2;
            _boltRenderer.positionCount = BoltPointCount;
            _boltRenderer.startColor = new Color(0.82f, 0.9f, 1f, 0.92f);
            _boltRenderer.endColor = new Color(0.76f, 0.84f, 1f, 0.28f);
            _boltRenderer.sharedMaterial = ResolveBoltMaterial();
            _boltRenderer.enabled = false;
        }

        private Material ResolveBoltMaterial()
        {
            if (lightningBoltMaterial != null)
                return lightningBoltMaterial;

            if (!_loggedMissingBoltMaterial)
            {
                _loggedMissingBoltMaterial = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[SurfaceWeatherVfxRig] Missing lightningBoltMaterial asset. Runtime material creation is forbidden for lightning bolt rendering.", this);
#endif
            }

            return null;
        }

        private void RebaseBoltPositions(Vector3 shiftOffset)
        {
            if (_boltRenderer == null || !_boltRenderer.enabled || _boltRenderer.positionCount <= 0)
                return;

            int positionCount = Mathf.Min(_boltRenderer.positionCount, _boltPoints.Length);
            if (positionCount <= 0)
                return;

            _boltRenderer.GetPositions(_boltPoints);
            for (int i = 0; i < positionCount; i++)
                _boltPoints[i] -= shiftOffset;

            _boltRenderer.SetPositions(_boltPoints);
        }

        private static Vector3 ResolveSafeDirection(Vector3 value, Vector3 fallback)
        {
            float sqrMagnitude = value.sqrMagnitude;
            return sqrMagnitude > 0.000001f
                ? value * math.rsqrt(sqrMagnitude)
                : fallback;
        }

        private static float LerpClamped(float from, float to, float t)
        {
            return math.lerp(from, to, math.saturate(t));
        }

        private void UpdateLightningState(float deltaTime)
        {
            if (_boltTimer <= 0f)
                return;

            _boltTimer -= deltaTime;
            if (_boltTimer <= 0f)
            {
                _boltTimer = 0f;
                _boltIntensity = 0f;
                if (_boltRenderer != null)
                    _boltRenderer.enabled = false;
            }
        }
    }
}

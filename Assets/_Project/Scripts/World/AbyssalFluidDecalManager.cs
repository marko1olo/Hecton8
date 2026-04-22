using Hecton8.Core;
using Hecton8.Physics;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    /// <summary>
    /// Lightweight non-GameObject fluid aftermath pass for abyssal cable cuts and drone ruptures.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-103)]
    public sealed class AbyssalFluidDecalManager : MonoBehaviour, ITickable
    {
        private struct FluidDecalState
        {
            public bool Active;
            public Vector3 PositionWS;
            public Vector3 DriftVelocityWS;
            public float RotationDegrees;
            public float Radius;
            public float TargetRadius;
            public float RemainingLifetime;
            public float TotalLifetime;
            public Color Color;
        }

        private static readonly int _TintColorId = Shader.PropertyToID("_TintColor");
        private static readonly int _RadiusId = Shader.PropertyToID("_Radius");
        private static readonly int _SoftnessId = Shader.PropertyToID("_Softness");
        private static readonly int _WakeDistortionId = Shader.PropertyToID("_WakeDistortion");
        private static readonly int _WakeTearStrengthId = Shader.PropertyToID("_WakeTearStrength");
        private static readonly int _WakeThresholdId = Shader.PropertyToID("_WakeThreshold");
        private static AbyssalFluidDecalManager _instance;

        [Header("── Runtime Wiring ──────────────────")]
        [SerializeField]
        [Tooltip("Optional explicit fluid decal material. Runtime falls back to Shader.Find when left empty.")]
        private Material decalMaterial;

        [Header("── Decal Simulation ─────────────────")]
        [SerializeField, Range(1, 32)]
        [Tooltip("Hard cap for simultaneous abyssal fluid decals.")]
        private int maxDecalCount = 12;

        [SerializeField, Range(0.1f, 10f)]
        [Tooltip("How quickly the decal radius grows toward the authored target radius.")]
        private float spreadSpeed = 1.35f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("How strongly global drift offset delta advects the decal position.")]
        private float driftOffsetInfluence = 0.75f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("How strongly ambient current velocity pushes the decal while it spreads.")]
        private float ambientCurrentInfluence = 0.28f;

        [SerializeField, Range(0.1f, 24f)]
        [Tooltip("How quickly each decal drift velocity converges toward the sampled current field.")]
        private float currentAdvectionBlendSharpness = 4.5f;

        [SerializeField, Range(0.001f, 0.2f)]
        [Tooltip("Noise scale used when sampling the shared ocean current field for decal advection.")]
        private float currentNoiseScale = 0.018f;

        [SerializeField, Range(0.01f, 1f)]
        [Tooltip("Time scale used when sampling the shared ocean current field for decal advection.")]
        private float currentTimeScale = 0.12f;

        [SerializeField, Range(0f, 10f)]
        [Tooltip("Strength applied to the shared ocean current sample before authored current volumes are added.")]
        private float currentStrength = 1.05f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Vertical preservation factor applied to decal advection so blood/oil mostly drifts along the seafloor plane.")]
        private float currentVerticalFactor = 0.1f;

        [SerializeField, Range(0.05f, 2f)]
        [Tooltip("Edge softness passed into the decal shader.")]
        private float edgeSoftness = 0.28f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("How strongly active scooter wake tears the decal alpha and radial profile.")]
        private float wakeTearStrength = 0.68f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("How strongly active scooter wake distorts the decal silhouette.")]
        private float wakeDistortion = 0.22f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Minimum sampled wake intensity required before the decal starts tearing.")]
        private float wakeThreshold = 0.08f;

        [SerializeField]
        [Tooltip("Synthetic oil tint used for cable cuts.")]
        private Color cableFluidColor = new Color(0.16f, 0.38f, 0.34f, 0.74f);

        [SerializeField]
        [Tooltip("Synthetic blood/oil tint used for ruptured abyssal drone schools.")]
        private Color ruptureFluidColor = new Color(0.32f, 0.1f, 0.18f, 0.82f);

        private FluidDecalState[] _decalStates;
        private Mesh _quadMesh;
        private Material _runtimeMaterial;
        private MaterialPropertyBlock _materialPropertyBlock;
        private Vector3 _previousGlobalDriftOffset;
        private bool _ownsRuntimeMaterial;
        private bool _registeredTick;

        /// <summary>
        /// Active singleton instance used by abyssal rupture and cable-cut aftermath systems.
        /// </summary>
        public static AbyssalFluidDecalManager Instance => _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogError("[AbyssalFluidDecalManager] Duplicate instance detected. Destroying newer component.", this);
                Destroy(this);
                return;
            }

            _instance = this;
            SanitizeSettings();
            EnsureStorage();
            EnsureRenderingResources();
            _previousGlobalDriftOffset = ResolveGlobalDriftOffset();
        }

        private void OnEnable()
        {
            EnsureStorage();
            EnsureRenderingResources();
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryUnregister();
            if (_ownsRuntimeMaterial && _runtimeMaterial != null)
            {
                Destroy(_runtimeMaterial);
            }
            _runtimeMaterial = null;
            _ownsRuntimeMaterial = false;

            if (_quadMesh != null)
            {
                Destroy(_quadMesh);
                _quadMesh = null;
            }

            if (_instance == this)
                _instance = null;
        }

        /// <summary>
        /// Registers a synthetic-fluid decal at a severed bio-cable knot.
        /// </summary>
        public void RegisterCableFluid(Vector3 positionWS, float radiusScale)
        {
            RegisterDecal(positionWS, cableFluidColor, Mathf.Lerp(0.8f, 2.2f, Mathf.Clamp01(radiusScale)), Mathf.Lerp(2.4f, 4.6f, Mathf.Clamp01(radiusScale)), 10f);
        }

        /// <summary>
        /// Registers a synthetic-fluid decal at a ruptured abyssal flock event.
        /// </summary>
        public void RegisterRuptureFluid(Vector3 positionWS, float radiusScale)
        {
            RegisterDecal(positionWS, ruptureFluidColor, Mathf.Lerp(1.4f, 3.2f, Mathf.Clamp01(radiusScale)), Mathf.Lerp(3.6f, 7.5f, Mathf.Clamp01(radiusScale)), 14f);
        }

        /// <summary>
        /// Advances decal drift, spread, and draw.
        /// </summary>
        public void Tick(float dt)
        {
            if (_decalStates == null || _runtimeMaterial == null || _quadMesh == null)
                return;

            float deltaTime = Mathf.Max(0f, dt);
            Vector3 currentDriftOffset = ResolveGlobalDriftOffset();
            Vector3 driftDelta = (currentDriftOffset - _previousGlobalDriftOffset) * driftOffsetInfluence;
            _previousGlobalDriftOffset = currentDriftOffset;
            for (int i = 0; i < _decalStates.Length; i++)
            {
                if (!_decalStates[i].Active)
                    continue;

                FluidDecalState decal = _decalStates[i];
                decal.RemainingLifetime -= deltaTime;
                if (decal.RemainingLifetime <= 0f)
                {
                    decal.Active = false;
                    _decalStates[i] = decal;
                    continue;
                }

                Vector3 sampledCurrent = ResolveCurrentVelocity(decal.PositionWS);
                float blendT = 1f - Mathf.Exp(-Mathf.Max(0.1f, currentAdvectionBlendSharpness) * deltaTime);
                decal.DriftVelocityWS = Vector3.Lerp(decal.DriftVelocityWS, sampledCurrent, blendT);
                decal.PositionWS += driftDelta + decal.DriftVelocityWS * (ambientCurrentInfluence * deltaTime);
                decal.Radius = Mathf.MoveTowards(decal.Radius, decal.TargetRadius, spreadSpeed * deltaTime);
                _decalStates[i] = decal;
                DrawDecal(decal);
            }
        }

        private void RegisterDecal(Vector3 positionWS, Color color, float startRadius, float targetRadius, float lifetime)
        {
            if (_decalStates == null || _decalStates.Length == 0)
                return;

            int targetIndex = -1;
            float weakestLifetime = float.MaxValue;
            for (int i = 0; i < _decalStates.Length; i++)
            {
                if (!_decalStates[i].Active)
                {
                    targetIndex = i;
                    break;
                }

                if (_decalStates[i].RemainingLifetime < weakestLifetime)
                {
                    weakestLifetime = _decalStates[i].RemainingLifetime;
                    targetIndex = i;
                }
            }

            if (targetIndex < 0)
                targetIndex = 0;

            Vector3 currentVector = ResolveCurrentVelocity(positionWS);
            _decalStates[targetIndex] = new FluidDecalState
            {
                Active = true,
                PositionWS = positionWS,
                DriftVelocityWS = currentVector * 0.25f,
                RotationDegrees = Mathf.Repeat((targetIndex * 57.29578f) + positionWS.x * 0.37f + positionWS.z * 0.19f, 360f),
                Radius = Mathf.Max(0.1f, startRadius),
                TargetRadius = Mathf.Max(startRadius, targetRadius),
                RemainingLifetime = Mathf.Max(0.25f, lifetime),
                TotalLifetime = Mathf.Max(0.25f, lifetime),
                Color = color
            };
        }

        private void DrawDecal(in FluidDecalState decal)
        {
            float alphaT = decal.TotalLifetime > 0.0001f ? Mathf.Clamp01(decal.RemainingLifetime / decal.TotalLifetime) : 0f;
            Color drawColor = decal.Color;
            drawColor.a *= alphaT;
            if (drawColor.a <= 0.0001f)
                return;

            Quaternion rotation = Quaternion.Euler(90f, decal.RotationDegrees, 0f);
            Matrix4x4 matrix = Matrix4x4.TRS(
                decal.PositionWS + Vector3.up * 0.03f,
                rotation,
                new Vector3(decal.Radius * 2f, decal.Radius * 2f, 1f));

            _materialPropertyBlock.Clear();
            _materialPropertyBlock.SetColor(_TintColorId, drawColor);
            _materialPropertyBlock.SetFloat(_RadiusId, decal.Radius);
            _materialPropertyBlock.SetFloat(_SoftnessId, edgeSoftness);
            _materialPropertyBlock.SetFloat(_WakeDistortionId, wakeDistortion);
            _materialPropertyBlock.SetFloat(_WakeTearStrengthId, wakeTearStrength);
            _materialPropertyBlock.SetFloat(_WakeThresholdId, wakeThreshold);

            Graphics.DrawMesh(
                _quadMesh,
                matrix,
                _runtimeMaterial,
                gameObject.layer,
                null,
                0,
                _materialPropertyBlock,
                ShadowCastingMode.Off,
                false,
                null,
                LightProbeUsage.Off,
                null);
        }

        private void EnsureStorage()
        {
            if (_decalStates == null || _decalStates.Length != maxDecalCount)
            {
                // COLD ALLOC: FluidDecalState[32] - capped abyssal aftermath decal registry - owner: AbyssalFluidDecalManager
                _decalStates = new FluidDecalState[maxDecalCount];
            }

            _materialPropertyBlock ??= new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - abyssal fluid decal draw properties - owner: AbyssalFluidDecalManager
        }

        private void EnsureRenderingResources()
        {
            if (_quadMesh == null)
                _quadMesh = BuildQuadMesh();

            if (_runtimeMaterial == null)
            {
                if (decalMaterial != null)
                {
                    _runtimeMaterial = decalMaterial;
                    _ownsRuntimeMaterial = false;
                    return;
                }

                Shader shader = Shader.Find("HECTON/World/AbyssalFluidDecal");
                if (shader == null)
                    return;

                // COLD ALLOC: Material[1] - dedicated abyssal fluid decal runtime material - owner: AbyssalFluidDecalManager
                _runtimeMaterial = new Material(shader)
                {
                    name = "MAT_Runtime_AbyssalFluidDecal"
                };
                _ownsRuntimeMaterial = true;
            }
        }

        private static Mesh BuildQuadMesh()
        {
            // COLD ALLOC: Mesh[1] - reusable quad for abyssal fluid decal rendering - owner: AbyssalFluidDecalManager
            Mesh mesh = new Mesh
            {
                name = "AbyssalFluidDecalQuad"
            };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3( 0.5f, -0.5f, 0f),
                new Vector3( 0.5f,  0.5f, 0f),
                new Vector3(-0.5f,  0.5f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
            mesh.UploadMeshData(true);
            return mesh;
        }

        private Vector3 ResolveGlobalDriftOffset()
        {
            SargassumGlobalDragManager dragManager = SargassumGlobalDragManager.Instance;
            return dragManager != null ? dragManager.GlobalDriftOffset : Vector3.zero;
        }

        private Vector3 ResolveCurrentVelocity(Vector3 positionWS)
        {
            float time = Time.time;
            float3 sampledCurrent = CurrentManager.SampleCurrent(
                new float3(positionWS.x, positionWS.y, positionWS.z),
                time,
                currentNoiseScale,
                currentTimeScale,
                currentStrength,
                currentVerticalFactor);
            Vector3 authoredCurrent = CurrentVolume.SampleAt(positionWS);
            Vector3 resolvedCurrent = new Vector3(sampledCurrent.x, sampledCurrent.y, sampledCurrent.z) + authoredCurrent;
            resolvedCurrent.y *= currentVerticalFactor;
            return resolvedCurrent;
        }

        private void TryRegister()
        {
            if (_registeredTick)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            tickManager.Register((ITickable)this);
            _registeredTick = true;
        }

        private void TryUnregister()
        {
            if (!_registeredTick)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
                tickManager.Unregister((ITickable)this);

            _registeredTick = false;
        }

        private void SanitizeSettings()
        {
            maxDecalCount = Mathf.Clamp(maxDecalCount, 1, 32);
            spreadSpeed = Mathf.Clamp(spreadSpeed, 0.1f, 10f);
            driftOffsetInfluence = Mathf.Clamp(driftOffsetInfluence, 0f, 2f);
            ambientCurrentInfluence = Mathf.Clamp(ambientCurrentInfluence, 0f, 2f);
            currentAdvectionBlendSharpness = Mathf.Clamp(currentAdvectionBlendSharpness, 0.1f, 24f);
            currentNoiseScale = Mathf.Clamp(currentNoiseScale, 0.001f, 0.2f);
            currentTimeScale = Mathf.Clamp(currentTimeScale, 0.01f, 1f);
            currentStrength = Mathf.Clamp(currentStrength, 0f, 10f);
            currentVerticalFactor = Mathf.Clamp01(currentVerticalFactor);
            edgeSoftness = Mathf.Clamp(edgeSoftness, 0.05f, 2f);
            wakeTearStrength = Mathf.Clamp01(wakeTearStrength);
            wakeDistortion = Mathf.Clamp01(wakeDistortion);
            wakeThreshold = Mathf.Clamp01(wakeThreshold);
        }
    }
}

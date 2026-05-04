using Hecton8.Core;
using Hecton8.Physics;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.World
{
    /// <summary>
    /// Lightweight non-GameObject fluid aftermath pass for abyssal cable cuts and drone ruptures.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-103)]
    public sealed class AbyssalFluidDecalManager : MonoBehaviour, ITickable, IOriginShiftListener
    {
#if UNITY_EDITOR
        private const string DecalMaterialAssetPath = "Assets/_Project/Art/Materials/VFX/MAT_AbyssalFluidDecal.mat";
#endif

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
        // COLD ALLOC: Vector3[4] - shared abyssal fluid decal quad vertices - owner: AbyssalFluidDecalManager
        private static readonly Vector3[] _quadVertices =
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f)
        };

        // COLD ALLOC: Vector2[4] - shared abyssal fluid decal quad UVs - owner: AbyssalFluidDecalManager
        private static readonly Vector2[] _quadUvs =
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };

        // COLD ALLOC: int[6] - shared abyssal fluid decal quad indices - owner: AbyssalFluidDecalManager
        private static readonly int[] _quadTriangles = { 0, 1, 2, 0, 2, 3 };

        // COLD ALLOC: Vector3[4] - shared abyssal fluid decal quad normals - owner: AbyssalFluidDecalManager
        private static readonly Vector3[] _quadNormals =
        {
            Vector3.forward,
            Vector3.forward,
            Vector3.forward,
            Vector3.forward
        };

        private static AbyssalFluidDecalManager _instance;

        [Header("── Runtime Wiring ──────────────────")]
        [SerializeField]
        [Tooltip("Authored fluid decal material. Runtime material creation is forbidden for this draw path.")]
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

        [SerializeField]
        [Tooltip("Low-alpha silt tint used for biome transition seismic dust.")]
        private Color seismicDustColor = new Color(0.34f, 0.32f, 0.26f, 0.42f);

        private FluidDecalState[] _decalStates;
        private Mesh _quadMesh;
        private Material _runtimeMaterial;
        private MaterialPropertyBlock _drawPropertyBlock;
        private Vector3 _previousGlobalDriftOffset;
        private bool _serviceRegistered;
        private bool _registeredTick;
        private bool _loggedMissingDecalMaterial;

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
            EnsureRenderingResources(false);
            _drawPropertyBlock = MaterialPropertyBlockRegistry.GetOrCreateLegacyBlock(this);
            _previousGlobalDriftOffset = ResolveGlobalDriftOffset();
        }

        private void OnEnable()
        {
            EnsureStorage();
            EnsureRenderingResources(false);
            _drawPropertyBlock = MaterialPropertyBlockRegistry.GetOrCreateLegacyBlock(this);
            HectonFloatingOrigin.RegisterListener(this);
            TryRegisterService();
            TryRegister();
        }

        private void OnDisable()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregisterService();
            TryUnregister();
        }

        private void OnDestroy()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregisterService();
            TryUnregister();
            _runtimeMaterial = null;
            _drawPropertyBlock = null;
            MaterialPropertyBlockRegistry.ReleaseLegacyBlock(this);

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
            EnsureRenderingResources(true);
            RegisterDecal(positionWS, cableFluidColor, Mathf.Lerp(0.8f, 2.2f, Mathf.Clamp01(radiusScale)), Mathf.Lerp(2.4f, 4.6f, Mathf.Clamp01(radiusScale)), 10f);
        }

        /// <summary>
        /// Registers a synthetic-fluid decal at a ruptured abyssal flock event.
        /// </summary>
        public void RegisterRuptureFluid(Vector3 positionWS, float radiusScale)
        {
            EnsureRenderingResources(true);
            RegisterDecal(positionWS, ruptureFluidColor, Mathf.Lerp(1.4f, 3.2f, Mathf.Clamp01(radiusScale)), Mathf.Lerp(3.6f, 7.5f, Mathf.Clamp01(radiusScale)), 14f);
        }

        /// <summary>
        /// Registers a soft seafloor dust sheet for tectonic biome transitions.
        /// </summary>
        public void RegisterSeismicDust(Vector3 positionWS, float radiusScale)
        {
            EnsureRenderingResources(true);
            float clampedScale = Mathf.Clamp01(radiusScale);
            RegisterDecal(positionWS, seismicDustColor, Mathf.Lerp(0.6f, 1.6f, clampedScale), Mathf.Lerp(2.2f, 5.4f, clampedScale), 8f);
        }

        /// <summary>
        /// Assigns the authored decal material before runtime draw resources are used.
        /// </summary>
        /// <param name="material">Shared material asset owned by the caller.</param>
        internal void ConfigureMaterial(Material material)
        {
            if (material == null)
                return;

            decalMaterial = material;
            _runtimeMaterial = material;
            _loggedMissingDecalMaterial = false;
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (!isActiveAndEnabled || shiftData.ShiftOffset.sqrMagnitude <= 0.0001f)
                return;

            ApplyRuntimeOffsetToCachedState(-shiftData.ShiftOffset);
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
            if (_drawPropertyBlock == null)
                _drawPropertyBlock = MaterialPropertyBlockRegistry.GetOrCreateLegacyBlock(this);
            if (_drawPropertyBlock == null)
                return;

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

            _drawPropertyBlock.Clear();
            _drawPropertyBlock.SetColor(_TintColorId, drawColor);
            _drawPropertyBlock.SetFloat(_RadiusId, decal.Radius);
            _drawPropertyBlock.SetFloat(_SoftnessId, edgeSoftness);
            _drawPropertyBlock.SetFloat(_WakeDistortionId, wakeDistortion);
            _drawPropertyBlock.SetFloat(_WakeTearStrengthId, wakeTearStrength);
            _drawPropertyBlock.SetFloat(_WakeThresholdId, wakeThreshold);

            Graphics.DrawMesh(
                _quadMesh,
                matrix,
                _runtimeMaterial,
                gameObject.layer,
                null,
                0,
                _drawPropertyBlock,
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
        }

        private void EnsureRenderingResources(bool logIfMissing)
        {
            if (_quadMesh == null)
                _quadMesh = BuildQuadMesh();

            if (_runtimeMaterial == null)
            {
                if (decalMaterial != null)
                {
                    _runtimeMaterial = decalMaterial;
                    return;
                }

                if (logIfMissing && !_loggedMissingDecalMaterial)
                {
                    _loggedMissingDecalMaterial = true;
                    Debug.LogError("[AbyssalFluidDecalManager] Missing decalMaterial asset. Runtime material creation is forbidden for this draw path.", this);
                }
            }
        }

        private static Mesh BuildQuadMesh()
        {
            // COLD ALLOC: Mesh[1] - reusable quad for abyssal fluid decal rendering - owner: AbyssalFluidDecalManager
            Mesh mesh = new Mesh
            {
                name = "AbyssalFluidDecalQuad"
            };
            mesh.vertices = _quadVertices;
            mesh.uv = _quadUvs;
            mesh.triangles = _quadTriangles;
            mesh.normals = _quadNormals;
            mesh.UploadMeshData(true);
            return mesh;
        }

        private Vector3 ResolveGlobalDriftOffset()
        {
            SargassumGlobalDragManager dragManager = GlobalRegistry.SargassumDrag;
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
            if (_registeredTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _registeredTick = GlobalRegistry.Updatables.Contains(this);
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterAbyssalFluidDecalRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.AbyssalFluidDecals, this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterAbyssalFluidDecalRuntime(this);
            _serviceRegistered = false;
        }

        private void TryUnregister()
        {
            if (!_registeredTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);

            _registeredTick = false;
        }

        private void ApplyRuntimeOffsetToCachedState(Vector3 runtimeOffset)
        {
            _previousGlobalDriftOffset += runtimeOffset;
            if (_decalStates == null)
                return;

            for (int i = 0; i < _decalStates.Length; i++)
            {
                if (!_decalStates[i].Active)
                    continue;

                FluidDecalState decal = _decalStates[i];
                decal.PositionWS += runtimeOffset;
                _decalStates[i] = decal;
            }
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            SanitizeSettings();

            if (decalMaterial == null)
                decalMaterial = AssetDatabase.LoadAssetAtPath<Material>(DecalMaterialAssetPath);
        }
#endif
    }
}

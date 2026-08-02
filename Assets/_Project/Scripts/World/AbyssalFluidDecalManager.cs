using Hecton8.Core;
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
    public sealed class AbyssalFluidDecalManager : MonoBehaviour, ILateFrameTickable, IOriginShiftListener, IFluidDecalPresentationSink, IGlobalRegistryHotSwapListener
    {
#if UNITY_EDITOR
        private const string DecalMaterialAssetPath = "Assets/_Project/Art/Materials/VFX/MAT_AbyssalFluidDecal.mat";
#endif
        private const string BuiltinQuadMeshName = "Quad.fbx";
        private const byte FluidDecalDriftModeCurrent = 0;
        private const byte FluidDecalDriftModeCinematic = 1;
        private const float FluidDecalClockMaxSeconds = 16777215f;
        private const int ScreenSpaceConsumerGraceFrames = 2;
        private const float MinimumPressureSprayDrawFraction = 0.1875f;

        private struct FluidDecalState
        {
            public byte Active;
            public Vector3 PositionWS;
            public Vector3 DriftVelocityWS;
            public byte DriftMode;
            public float RotationDegrees;
            public float Radius;
            public float TargetRadius;
            public float RemainingLifetime;
            public float TotalLifetime;
            public Color Color;
        }

        private struct PressureSprayState
        {
            public byte Active;
            public Vector3 PositionWS;
            public Vector3 DirectionWS;
            public float Width;
            public float Length;
            public float Speed;
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
        private static Mesh s_sharedQuadMesh;

        [Header("в”Ђв”Ђ Runtime Wiring в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ")]
        [SerializeField]
        [Tooltip("Authored fluid decal material. Runtime material creation is forbidden for this draw path.")]
        private Material decalMaterial;

        [SerializeField]
        [Tooltip("Routes fluid aftermath decals through the fullscreen deferred decal pass; mesh draw remains only as an explicit fallback.")]
        private bool screenSpaceFluidDecals = true;

        [Header("в”Ђв”Ђ Decal Simulation в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ")]
        [SerializeField, Range(1, 32)]
        [Tooltip("Hard cap for simultaneous abyssal fluid decals.")]
        private int maxDecalCount = 12;

        [SerializeField, Range(1, 64)]
        [Tooltip("Hard cap for directional high-pressure room leak sprays.")]
        private int maxPressureSprayCount = 24;

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

        [SerializeField]
        [Tooltip("Slow brown-black silt volume tint used when voxel cave mouths collapse under laser cutting.")]
        private Color voxelCaveInDustColor = new Color(0.27f, 0.24f, 0.18f, 0.48f);

        [SerializeField]
        [Tooltip("Low-alpha disturbed-silt tint used for KCC and submarine wake trails.")]
        private Color wakeSiltColor = new Color(0.28f, 0.31f, 0.29f, 0.34f);

        [SerializeField]
        [Tooltip("White-blue foam tint used for water-entry splash decals.")]
        private Color waterSplashFoamColor = new Color(0.64f, 0.86f, 1f, 0.48f);

        [SerializeField]
        [Tooltip("White-blue foam tint used for module high-pressure leak spray ribbons.")]
        private Color pressureSprayColor = new Color(0.72f, 0.88f, 1f, 0.62f);

        private FluidDecalState[] _decalStates;
        private PressureSprayState[] _pressureSprayStates;
        private Matrix4x4[] _pressureSprayMatrices;
        private Mesh _quadMesh;
        private Material _runtimeMaterial;
        private MaterialPropertyBlock _drawPropertyBlock;
        private Vector3 _previousGlobalDriftOffset;
        private float _fluidDecalClockSeconds;
        private SargassumGlobalDragManager _sargassumDrag;
        private IPlayerRuntimeContext _playerContext;
        private IAmbientCurrentReadModel _ambientCurrentReadModel;
        private bool _serviceRegistered;
        private bool _registeredTick;
        private bool _registeredLateFrame;
        private bool _registeredHotSwapListener;
        private bool _loggedMissingDecalMaterial;
        private bool _drawDecalsDirty;
        private bool _pressureSprayDrawDirty;
        private bool _screenSpaceDecalConsumerSeen;
        private int _lastScreenSpaceDecalCopyFrame;
        private int _pressureSprayMatrixCount;

        /// <summary>
        /// Resolve-or-create the sole AbyssalFluidDecalManager / GlobalRegistry.FluidDecalPresentation owner.
        /// Script GUID 932634fcdd3b41b091f6c33d24230da6 has ZERO scene/prefab hits.
        /// OnEnable only registers when already present; without this factory BaseModule,
        /// HabitatIntegrityManager, LogisticsPipeNode, HectonPlayerMotor, VehicleMotor,
        /// BiomeMatrixDirector, ConstructionManager and SubmarineStructuralGrid hit permanent null.
        /// Profile-null-safe: missing decalMaterial logs once and mesh draw no-ops.
        /// </summary>
        public static AbyssalFluidDecalManager EnsureRuntimeInstance()
        {
            AbyssalFluidDecalManager registered = GlobalRegistry.AbyssalFluidDecals;
            if (IsFluidDecalRuntimeUsable(registered))
                return registered;

            if (!ReferenceEquals(registered, null))
            {
                GlobalRegistry.UnregisterAbyssalFluidDecalRuntime(registered);
                registered._serviceRegistered = false;
            }

            if (!Application.isPlaying)
                return null;

            // Player-build construction path: no authored/bootstrap instance reachable.
            // Sole AbyssalFluidDecals owner; must construct when bootstrap reorders.
            GameObject runtimeRoot = new GameObject("[AbyssalFluidDecalManager]"); // COLD ALLOC
            return runtimeRoot.AddComponent<AbyssalFluidDecalManager>();
        }

        private static bool IsFluidDecalRuntimeUsable(AbyssalFluidDecalManager manager)
        {
            return !ReferenceEquals(manager, null) &&
                   manager != null &&
                   manager._serviceRegistered &&
                   manager.isActiveAndEnabled;
        }

        private void Awake()
        {
            SanitizeSettings();
            CacheRegistryServicesCold();
            EnsureStorage();
            EnsureRenderingResources(false);
            _drawPropertyBlock = MaterialPropertyBlockRegistry.AcquireLegacyBlock(this);
            _previousGlobalDriftOffset = ResolveGlobalDriftOffset();
            _fluidDecalClockSeconds = 0f;
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            EnsureStorage();
            EnsureRenderingResources(false);
            _drawPropertyBlock = MaterialPropertyBlockRegistry.AcquireLegacyBlock(this);
            HectonFloatingOrigin.RegisterListener(this);
            TryRegisterHotSwapListener();
            TryRegisterService();
            TryRegister();
        }

        private void OnDisable()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregisterHotSwapListener();
            TryUnregisterService();
            TryUnregister();
            _sargassumDrag = null;
        }

        private void OnDestroy()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregisterHotSwapListener();
            TryUnregisterService();
            TryUnregister();
            _sargassumDrag = null;
            _runtimeMaterial = null;
            _drawPropertyBlock = null;
            MaterialPropertyBlockRegistry.ReleaseLegacyBlock(this);

            if (_quadMesh != null && !ReferenceEquals(_quadMesh, s_sharedQuadMesh))
            {
                Destroy(_quadMesh);
                _quadMesh = null;
            }
            else
            {
                _quadMesh = null;
            }

        }

        /// <summary>
        /// Registers a synthetic-fluid decal at a severed bio-cable knot.
        /// </summary>
        public void RegisterCableFluid(Vector3 positionWS, float radiusScale)
        {
            if (!IsPresentationReady())
                return;

            RegisterDecal(positionWS, cableFluidColor, LerpClamped(0.8f, 2.2f, radiusScale), LerpClamped(2.4f, 4.6f, radiusScale), 10f);
        }

        /// <summary>
        /// Registers a synthetic-fluid decal at a ruptured abyssal flock event.
        /// </summary>
        public void RegisterRuptureFluid(Vector3 positionWS, float radiusScale)
        {
            if (!IsPresentationReady())
                return;

            RegisterDecal(positionWS, ruptureFluidColor, LerpClamped(1.4f, 3.2f, radiusScale), LerpClamped(3.6f, 7.5f, radiusScale), 14f);
        }

        /// <summary>
        /// Registers a short-lived directional foam ribbon for high-pressure habitat leak sprays.
        /// </summary>
        public void RegisterPressureSpray(Vector3 positionWS, Vector3 inwardDirectionWS, float intensity01)
        {
            if (!IsPresentationReady())
                return;

            RegisterSpray(positionWS, inwardDirectionWS, Mathf.Clamp01(intensity01));
        }

        /// <summary>
        /// Registers a soft seafloor dust sheet for tectonic biome transitions.
        /// </summary>
        public void RegisterSeismicDust(Vector3 positionWS, float radiusScale)
        {
            if (!IsPresentationReady())
                return;

            float clampedScale = Mathf.Clamp01(radiusScale);
            RegisterDecal(positionWS, seismicDustColor, LerpClamped(0.6f, 1.6f, clampedScale), LerpClamped(2.2f, 5.4f, clampedScale), 8f);
        }

        /// <summary>
        /// Registers a slow unlit silt sheet at a voxel SDF carve AUP projected into the current runtime origin.
        /// </summary>
        public void RegisterVoxelCaveInDust(Vector3 positionWS, Vector3 impulseDirectionWS, float radiusScale)
        {
            if (!IsPresentationReady())
                return;

            float3 position3 = new float3(positionWS.x, positionWS.y, positionWS.z);
            float3 impulse3 = new float3(impulseDirectionWS.x, impulseDirectionWS.y, impulseDirectionWS.z);
            if (!math.all(math.isfinite(position3)) || !math.all(math.isfinite(impulse3)))
                return;

            float clampedScale = math.saturate(radiusScale);
            if (clampedScale <= 0.001f)
                return;

            float3 resolvedImpulse = NormalizeOrDefault(impulse3, new float3(0f, 1f, 0f));
            float downwardBias = math.saturate(-resolvedImpulse.y * 0.5f + 0.5f);
            Color color = voxelCaveInDustColor;
            color.a *= LerpClamped(0.55f, 1f, clampedScale);
            Vector3 liftedPosition = positionWS + Vector3.up * LerpClamped(0.08f, 0.22f, clampedScale);
            Vector3 cinematicDrift = ResolveVoxelCaveInDustDrift(position3, resolvedImpulse, clampedScale);
            RegisterDecal(
                liftedPosition,
                color,
                LerpClamped(0.45f, 1.1f, clampedScale),
                LerpClamped(1.8f, 4.8f, clampedScale) * LerpClamped(0.86f, 1.16f, downwardBias),
                LerpClamped(3.6f, 7.2f, clampedScale),
                cinematicDrift,
                FluidDecalDriftModeCinematic);
        }

        /// <summary>
        /// Registers voxel cave-in dust from an absolute-universe hit point.
        /// </summary>
        public void RegisterVoxelCaveInDustAup(Vector3 absoluteUniversePosition, Vector3 impulseDirectionWS, float radiusScale)
        {
            RegisterVoxelCaveInDust(HectonFloatingOrigin.ToRuntimePosition(absoluteUniversePosition), impulseDirectionWS, radiusScale);
        }

        public void RegisterVoxelCaveInDustAup(double3 absoluteUniversePosition, Vector3 impulseDirectionWS, float radiusScale)
        {
            RegisterVoxelCaveInDust(HectonFloatingOrigin.ToRuntimePosition(absoluteUniversePosition), impulseDirectionWS, radiusScale);
        }

        /// <summary>
        /// Registers a disturbed-silt sheet emitted by fast KCC or vehicle wake motion.
        /// </summary>
        public void RegisterWakeSilt(Vector3 positionWS, Vector3 sourceVelocityWS, float intensity01)
        {
            if (!IsPresentationReady())
                return;

            float3 position3 = new float3(positionWS.x, positionWS.y, positionWS.z);
            float3 velocity3 = new float3(sourceVelocityWS.x, sourceVelocityWS.y, sourceVelocityWS.z);
            if (!math.all(math.isfinite(position3)) || !math.all(math.isfinite(velocity3)))
                return;

            float clampedIntensity = math.saturate(intensity01);
            if (clampedIntensity <= 0.001f)
                return;

            float speed = ApproximateMagnitude(velocity3);
            Color color = wakeSiltColor;
            color.a *= LerpClamped(0.35f, 1f, clampedIntensity);
            float startRadius = LerpClamped(0.45f, 1.4f, clampedIntensity);
            float targetRadius = LerpClamped(1.6f, 5.2f, clampedIntensity) + speed * 0.04f;
            float lifetime = LerpClamped(2.2f, 6.5f, clampedIntensity);
            RegisterDecal(positionWS, color, startRadius, targetRadius, lifetime);
        }

        /// <summary>
        /// Registers a flat shader splash sheet for object water entry. No particle drops are spawned.
        /// </summary>
        public void RegisterWaterSplash(Vector3 positionWS, Vector3 sourceVelocityWS, float intensity01)
        {
            if (!IsPresentationReady())
                return;

            float3 position3 = new float3(positionWS.x, positionWS.y, positionWS.z);
            float3 velocity3 = new float3(sourceVelocityWS.x, sourceVelocityWS.y, sourceVelocityWS.z);
            if (!math.all(math.isfinite(position3)) || !math.all(math.isfinite(velocity3)))
                return;

            float clampedIntensity = math.saturate(intensity01);
            if (clampedIntensity <= 0.001f)
                return;

            float speed = ApproximateMagnitude(velocity3);
            Color color = waterSplashFoamColor;
            color.a *= LerpClamped(0.45f, 1f, clampedIntensity);
            float startRadius = LerpClamped(0.35f, 1.15f, clampedIntensity);
            float targetRadius = LerpClamped(1.4f, 5.4f, clampedIntensity) + speed * 0.025f;
            float lifetime = LerpClamped(0.75f, 2.2f, clampedIntensity);
            RegisterDecal(positionWS, color, startRadius, targetRadius, lifetime);
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
            Vector3 shiftOffset = shiftData.ShiftOffset;
            float shiftSqrMagnitude = shiftOffset.sqrMagnitude;
            if (!isActiveAndEnabled ||
                !MathGuard.IsFinite(shiftOffset) ||
                !MathGuard.IsFinite(shiftSqrMagnitude) ||
                shiftSqrMagnitude <= 0.0001f)
            {
                return;
            }

            ApplyRuntimeOffsetToCachedState(-shiftOffset);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _playerContext = currentService as IPlayerRuntimeContext;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.SargassumDragRuntime)
            {
                _sargassumDrag = currentService as SargassumGlobalDragManager;
                WorldRuntimeReferenceUtility.TryResolveSargassumGlobalDragManager(ref _sargassumDrag);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.FluidRuntime)
            {
                _ambientCurrentReadModel = currentService as IAmbientCurrentReadModel;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregister();
                if (currentService != null && isActiveAndEnabled)
                    TryRegister();
            }
        }

        /// <summary>
        /// Advances decal drift, spread, and draw.
        /// </summary>
        private void AdvanceFluidDecals(float dt)
        {
            if (_decalStates == null || _runtimeMaterial == null || _quadMesh == null)
                return;

            float deltaTime = math.isfinite(dt) ? math.max(0f, dt) : 0f;
            AdvanceFluidDecalClock(deltaTime);
            Vector3 currentDriftOffset = ResolveGlobalDriftOffset();
            Vector3 driftDelta = (currentDriftOffset - _previousGlobalDriftOffset) * driftOffsetInfluence;
            _previousGlobalDriftOffset = currentDriftOffset;
            for (int i = 0; i < _decalStates.Length; i++)
            {
                if (_decalStates[i].Active == 0)
                    continue;

                FluidDecalState decal = _decalStates[i];
                decal.RemainingLifetime -= deltaTime;
                if (decal.RemainingLifetime <= 0f)
                {
                    decal.Active = 0;
                    _decalStates[i] = decal;
                    continue;
                }

                if (decal.DriftMode != FluidDecalDriftModeCinematic)
                {
                    Vector3 sampledCurrent = ResolveCurrentVelocity(decal.PositionWS);
                    float blendT = FastAdvectionBlend(currentAdvectionBlendSharpness, deltaTime);
                    decal.DriftVelocityWS = new Vector3(
                        math.lerp(decal.DriftVelocityWS.x, sampledCurrent.x, blendT),
                        math.lerp(decal.DriftVelocityWS.y, sampledCurrent.y, blendT),
                        math.lerp(decal.DriftVelocityWS.z, sampledCurrent.z, blendT));
                }

                decal.PositionWS += driftDelta + decal.DriftVelocityWS * (ambientCurrentInfluence * deltaTime);
                decal.Radius = MoveTowardsFast(decal.Radius, decal.TargetRadius, spreadSpeed * deltaTime);
                _decalStates[i] = decal;
                if (ShouldDrawMeshFluidDecals())
                    _drawDecalsDirty = true;
            }

            TickPressureSprays(deltaTime, driftDelta);
        }

        public void LateFrameTick()
        {
            AdvanceFluidDecals(SystemDispatcher.CurrentFrameDeltaTime);

            if (_drawDecalsDirty)
            {
                _drawDecalsDirty = false;
                DrawActiveDecals();
            }

            if (_pressureSprayDrawDirty)
            {
                _pressureSprayDrawDirty = false;
                DrawPressureSprayBatch(_pressureSprayMatrixCount);
            }
        }

        private void RegisterDecal(Vector3 positionWS, Color color, float startRadius, float targetRadius, float lifetime)
        {
            RegisterDecal(positionWS, color, startRadius, targetRadius, lifetime, Vector3.zero, FluidDecalDriftModeCurrent);
        }

        private void RegisterDecal(Vector3 positionWS, Color color, float startRadius, float targetRadius, float lifetime, Vector3 driftVelocityWS, byte driftMode)
        {
            if (_decalStates == null || _decalStates.Length == 0)
                return;

            int targetIndex = -1;
            float weakestLifetime = float.MaxValue;
            for (int i = 0; i < _decalStates.Length; i++)
            {
                if (_decalStates[i].Active == 0)
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

            Vector3 currentVector = driftMode == FluidDecalDriftModeCinematic
                ? driftVelocityWS
                : ResolveCurrentVelocity(positionWS) * 0.25f;
            _decalStates[targetIndex] = new FluidDecalState
            {
                Active = 1,
                PositionWS = positionWS,
                DriftVelocityWS = currentVector,
                DriftMode = driftMode,
                RotationDegrees = Mathf.Repeat((targetIndex * 57.29578f) + positionWS.x * 0.37f + positionWS.z * 0.19f, 360f),
                Radius = Mathf.Max(0.1f, startRadius),
                TargetRadius = Mathf.Max(startRadius, targetRadius),
                RemainingLifetime = Mathf.Max(0.25f, lifetime),
                TotalLifetime = Mathf.Max(0.25f, lifetime),
                Color = color
            };
        }

        private static float FastAdvectionBlend(float sharpness, float deltaTime)
        {
            float x = math.max(0.1f, sharpness) * math.max(0f, deltaTime);
            return math.saturate((x * (6f + x)) / (6f + (4f * x) + (x * x)));
        }

        private static float LerpClamped(float from, float to, float t)
        {
            return from + ((to - from) * math.saturate(t));
        }

        private static float MoveTowardsFast(float current, float target, float maxDelta)
        {
            float delta = target - current;
            float safeDelta = math.max(0f, maxDelta);
            if (math.abs(delta) <= safeDelta)
                return target;

            return current + (math.sign(delta) * safeDelta);
        }

        private static Vector3 ResolveSafeDirection(Vector3 direction, Vector3 fallback)
        {
            float3 value = new float3(direction.x, direction.y, direction.z);
            float3 fallbackValue = new float3(fallback.x, fallback.y, fallback.z);
            float3 normalized = NormalizeOrDefault(value, fallbackValue);
            return new Vector3(normalized.x, normalized.y, normalized.z);
        }

        private static float3 NormalizeOrDefault(float3 value, float3 fallback)
        {
            if (!math.all(math.isfinite(value)))
                return fallback;

            float lengthSq = math.lengthsq(value);
            if (lengthSq <= 0.0001f)
                return fallback;

            return value * math.rsqrt(lengthSq);
        }

        private static float ApproximateMagnitude(float3 value)
        {
            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float az = math.abs(value.z);
            float maxAxis = math.max(ax, math.max(ay, az));
            float minAxis = math.min(ax, math.min(ay, az));
            float midAxis = ax + ay + az - maxAxis - minAxis;
            return maxAxis + (midAxis * 0.375f) + (minAxis * 0.125f);
        }

        private static Vector3 ResolveVoxelCaveInDustDrift(float3 position, float3 resolvedImpulse, float scale01)
        {
            uint seed = (uint)math.hash(new int4(
                (int)math.round(position.x * 4f),
                (int)math.round(position.y * 4f),
                (int)math.round(position.z * 4f),
                (int)math.round(scale01 * 255f)));
            float2 lateralDirection = ResolveOctantDirection((int)(seed & 7u));
            float lateral = math.lerp(0.025f, 0.12f, scale01);
            float sink = -math.lerp(0.025f, 0.06f, scale01);
            float impulseLateral = math.saturate(1f - math.abs(resolvedImpulse.y)) * 0.035f;
            return new Vector3(
                lateralDirection.x * lateral + resolvedImpulse.x * impulseLateral,
                sink,
                lateralDirection.y * lateral + resolvedImpulse.z * impulseLateral);
        }

        private static float2 ResolveOctantDirection(int sector)
        {
            switch (sector & 7)
            {
                case 0:
                    return new float2(1f, 0f);
                case 1:
                    return new float2(0.70710677f, 0.70710677f);
                case 2:
                    return new float2(0f, 1f);
                case 3:
                    return new float2(-0.70710677f, 0.70710677f);
                case 4:
                    return new float2(-1f, 0f);
                case 5:
                    return new float2(-0.70710677f, -0.70710677f);
                case 6:
                    return new float2(0f, -1f);
                default:
                    return new float2(0.70710677f, -0.70710677f);
            }
        }

        private void RegisterSpray(Vector3 positionWS, Vector3 directionWS, float intensity01)
        {
            if (_pressureSprayStates == null || _pressureSprayStates.Length == 0)
                return;

            float3 position = new float3(positionWS.x, positionWS.y, positionWS.z);
            float3 direction = new float3(directionWS.x, directionWS.y, directionWS.z);
            if (!math.all(math.isfinite(position)) || !math.all(math.isfinite(direction)) || math.lengthsq(direction) <= 0.0001f)
                return;

            int targetIndex = -1;
            float weakestLifetime = float.MaxValue;
            for (int i = 0; i < _pressureSprayStates.Length; i++)
            {
                if (_pressureSprayStates[i].Active == 0)
                {
                    targetIndex = i;
                    break;
                }

                if (_pressureSprayStates[i].RemainingLifetime < weakestLifetime)
                {
                    weakestLifetime = _pressureSprayStates[i].RemainingLifetime;
                    targetIndex = i;
                }
            }

            if (targetIndex < 0)
                targetIndex = 0;

            Vector3 axisDirection = ResolveSafeDirection(directionWS, Vector3.up);
            float clampedIntensity = Mathf.Clamp01(intensity01);
            Color color = pressureSprayColor;
            color.a *= LerpClamped(0.45f, 1f, clampedIntensity);
            _pressureSprayStates[targetIndex] = new PressureSprayState
            {
                Active = 1,
                PositionWS = positionWS,
                DirectionWS = axisDirection,
                Width = LerpClamped(0.12f, 0.42f, clampedIntensity),
                Length = LerpClamped(1.4f, 5.8f, clampedIntensity),
                Speed = LerpClamped(0.45f, 3.2f, clampedIntensity),
                RemainingLifetime = LerpClamped(0.45f, 1.4f, clampedIntensity),
                TotalLifetime = LerpClamped(0.45f, 1.4f, clampedIntensity),
                Color = color
            };
        }

        private void TickPressureSprays(float deltaTime, Vector3 driftDelta)
        {
            if (_pressureSprayStates == null)
                return;

            Transform cameraTransform = ResolvePlayerCameraTransform();
            int drawLimit = ResolvePressureSprayDrawLimit(
                _pressureSprayMatrices != null ? _pressureSprayMatrices.Length : maxPressureSprayCount,
                HomeostasisBrain.GlobalQualityWeight,
                HomeostasisBrain.PressureLevel);
            int matrixCount = 0;
            for (int i = 0; i < _pressureSprayStates.Length; i++)
            {
                if (_pressureSprayStates[i].Active == 0)
                    continue;

                PressureSprayState spray = _pressureSprayStates[i];
                spray.RemainingLifetime -= deltaTime;
                if (spray.RemainingLifetime <= 0f)
                {
                    spray.Active = 0;
                    _pressureSprayStates[i] = spray;
                    continue;
                }

                spray.PositionWS += driftDelta + spray.DirectionWS * (deltaTime * spray.Speed);
                _pressureSprayStates[i] = spray;
                AppendPressureSprayMatrix(in spray, cameraTransform, drawLimit, ref matrixCount);
            }

            _pressureSprayMatrixCount = matrixCount;
            _pressureSprayDrawDirty = matrixCount > 0;
        }

        private void DrawActiveDecals()
        {
            if (_decalStates == null || !ShouldDrawMeshFluidDecals())
                return;

            for (int i = 0; i < _decalStates.Length; i++)
            {
                FluidDecalState decal = _decalStates[i];
                if (decal.Active != 0)
                    DrawDecal(in decal);
            }
        }

        private void AppendPressureSprayMatrix(in PressureSprayState spray, Transform cameraTransform, int drawLimit, ref int matrixCount)
        {
            if (_pressureSprayMatrices == null || matrixCount >= _pressureSprayMatrices.Length || matrixCount >= drawLimit)
                return;

            float alphaT = spray.TotalLifetime > 0.0001f ? Mathf.Clamp01(spray.RemainingLifetime / spray.TotalLifetime) : 0f;
            if (spray.Color.a * alphaT <= 0.0001f)
                return;

            Vector3 center = spray.PositionWS + spray.DirectionWS * (spray.Length * 0.5f);
            Quaternion rotation;
            if (cameraTransform != null)
            {
                Vector3 toCamera = cameraTransform.position - center;
                if (toCamera.sqrMagnitude <= 0.0001f)
                    toCamera = -cameraTransform.forward;
                rotation = Quaternion.LookRotation(ResolveSafeDirection(toCamera, -cameraTransform.forward), cameraTransform.up);
            }
            else
            {
                rotation = Quaternion.LookRotation(-spray.DirectionWS, Vector3.up);
            }

            _pressureSprayMatrices[matrixCount] = Matrix4x4.TRS(
                center,
                rotation,
                new Vector3(
                    spray.Width * LerpClamped(0.55f, 1f, alphaT),
                    spray.Length * LerpClamped(0.70f, 1f, alphaT),
                    1f));
            matrixCount++;
        }

        private void DrawPressureSprayBatch(int matrixCount)
        {
            if (matrixCount <= 0 || _quadMesh == null || _runtimeMaterial == null)
                return;

            if (_drawPropertyBlock == null)
                return;

            _drawPropertyBlock.Clear();
            _drawPropertyBlock.SetColor(_TintColorId, pressureSprayColor);
            _drawPropertyBlock.SetFloat(_RadiusId, 1f);
            _drawPropertyBlock.SetFloat(_SoftnessId, edgeSoftness * 0.5f);
            _drawPropertyBlock.SetFloat(_WakeDistortionId, wakeDistortion);
            _drawPropertyBlock.SetFloat(_WakeTearStrengthId, wakeTearStrength);
            _drawPropertyBlock.SetFloat(_WakeThresholdId, wakeThreshold);

            RenderParams renderParams = new RenderParams(_runtimeMaterial)
            {
                matProps = _drawPropertyBlock,
                layer = gameObject.layer,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                lightProbeUsage = LightProbeUsage.Off
            };
            UnityEngine.Graphics.RenderMeshInstanced(renderParams, _quadMesh, 0, _pressureSprayMatrices, matrixCount);
        }

        private void DrawDecal(in FluidDecalState decal)
        {
            if (_drawPropertyBlock == null)
                return;

            if (!TryBuildFluidDecalDrawData(in decal, out Matrix4x4 matrix, out Color drawColor))
                return;

            _drawPropertyBlock.Clear();
            _drawPropertyBlock.SetColor(_TintColorId, drawColor);
            _drawPropertyBlock.SetFloat(_RadiusId, decal.Radius);
            _drawPropertyBlock.SetFloat(_SoftnessId, edgeSoftness);
            _drawPropertyBlock.SetFloat(_WakeDistortionId, wakeDistortion);
            _drawPropertyBlock.SetFloat(_WakeTearStrengthId, wakeTearStrength);
            _drawPropertyBlock.SetFloat(_WakeThresholdId, wakeThreshold);

            UnityEngine.Graphics.DrawMesh(
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
                LightProbeUsage.Off);
        }

        internal int CopyScreenSpaceDecals(Matrix4x4[] matrices, Color[] colors, int capacity)
        {
            if (!screenSpaceFluidDecals ||
                _decalStates == null ||
                matrices == null ||
                colors == null ||
                capacity <= 0)
            {
                return 0;
            }

            _screenSpaceDecalConsumerSeen = true;
            _lastScreenSpaceDecalCopyFrame = Time.frameCount;
            int safeCapacity = Mathf.Min(capacity, matrices.Length, colors.Length);
            int count = 0;
            for (int i = 0; i < _decalStates.Length && count < safeCapacity; i++)
            {
                if (_decalStates[i].Active == 0)
                    continue;

                if (!TryBuildFluidDecalDrawData(in _decalStates[i], out Matrix4x4 matrix, out Color drawColor))
                    continue;

                matrices[count] = matrix;
                colors[count] = drawColor;
                count++;
            }

            return count;
        }

        internal static int ResolvePressureSprayDrawLimit(int capacity, float globalQualityWeight, byte pressureLevel)
        {
            int safeCapacity = math.max(0, capacity);
            if (safeCapacity <= 0)
                return 0;

            float quality = Sanitize01(globalQualityWeight, 1f);
            float pressure01 = math.saturate(pressureLevel / 3f);
            float qualityScale = math.lerp(MinimumPressureSprayDrawFraction, 1f, Smooth01(quality));
            float pressureScale = math.lerp(1f, MinimumPressureSprayDrawFraction, pressure01);
            float drawFraction = math.max(MinimumPressureSprayDrawFraction, qualityScale * pressureScale);
            return math.clamp((int)math.ceil(safeCapacity * drawFraction), 1, safeCapacity);
        }

        private static bool TryBuildFluidDecalDrawData(in FluidDecalState decal, out Matrix4x4 matrix, out Color drawColor)
        {
            float alphaT = decal.TotalLifetime > 0.0001f ? Mathf.Clamp01(decal.RemainingLifetime / decal.TotalLifetime) : 0f;
            drawColor = decal.Color;
            drawColor.a *= alphaT;
            if (drawColor.a <= 0.0001f)
            {
                matrix = Matrix4x4.identity;
                return false;
            }

            Quaternion rotation = Quaternion.Euler(90f, decal.RotationDegrees, 0f);
            matrix = Matrix4x4.TRS(
                decal.PositionWS + Vector3.up * 0.03f,
                rotation,
                new Vector3(decal.Radius * 2f, decal.Radius * 2f, 1f));
            return true;
        }

        private bool ShouldDrawMeshFluidDecals()
        {
            return !screenSpaceFluidDecals || !HasActiveScreenSpaceFluidDecalConsumer();
        }

        private bool HasActiveScreenSpaceFluidDecalConsumer()
        {
            return _screenSpaceDecalConsumerSeen &&
                   Time.frameCount - _lastScreenSpaceDecalCopyFrame <= ScreenSpaceConsumerGraceFrames;
        }

        private static float Sanitize01(float value, float fallback)
        {
            return math.saturate(math.isfinite(value) ? value : fallback);
        }

        private static float Smooth01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - 2f * t);
        }

        private void EnsureStorage()
        {
            if (_decalStates == null || _decalStates.Length != maxDecalCount)
            {
                // COLD ALLOC: FluidDecalState[32] - capped abyssal aftermath decal registry - owner: AbyssalFluidDecalManager
                _decalStates = new FluidDecalState[maxDecalCount];
            }

            if (_pressureSprayStates == null || _pressureSprayStates.Length != maxPressureSprayCount)
            {
                // COLD ALLOC: PressureSprayState[24] - capped high-pressure breach spray registry - owner: AbyssalFluidDecalManager
                _pressureSprayStates = new PressureSprayState[maxPressureSprayCount];
            }

            if (_pressureSprayMatrices == null || _pressureSprayMatrices.Length != maxPressureSprayCount)
            {
                // COLD ALLOC: Matrix4x4[64] - batched billboard foam spray matrices - owner: AbyssalFluidDecalManager
                _pressureSprayMatrices = new Matrix4x4[maxPressureSprayCount];
            }
        }

        private void EnsureRenderingResources(bool logIfMissing)
        {
            if (_quadMesh == null)
                _quadMesh = ResolveSharedQuadMesh();

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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Hecton8.Core.H8Debug.LogError("[AbyssalFluidDecalManager] Missing decalMaterial asset. Runtime material creation is forbidden for this draw path.", this);
#endif
                }
            }
        }

        private bool IsPresentationReady()
        {
            return _quadMesh != null &&
                   _runtimeMaterial != null &&
                   _drawPropertyBlock != null &&
                   _decalStates != null &&
                   _decalStates.Length > 0 &&
                   _pressureSprayStates != null &&
                   _pressureSprayStates.Length > 0 &&
                   _pressureSprayMatrices != null &&
                   _pressureSprayMatrices.Length >= _pressureSprayStates.Length;
        }

        private static Mesh ResolveSharedQuadMesh()
        {
            if (s_sharedQuadMesh != null)
                return s_sharedQuadMesh;

            s_sharedQuadMesh = Resources.GetBuiltinResource<Mesh>(BuiltinQuadMeshName);
            return s_sharedQuadMesh;
        }

        private Transform ResolvePlayerCameraTransform()
        {
            IPlayerRuntimeContext playerContext = _playerContext;
            Camera playerCamera = playerContext != null ? playerContext.PlayerCamera : null;
            if (playerCamera == null)
                playerCamera = GlobalRenderContext.CurrentCamera;
            return playerCamera != null ? playerCamera.transform : null;
        }

        private Vector3 ResolveGlobalDriftOffset()
        {
            SargassumGlobalDragManager dragManager = _sargassumDrag;
            return dragManager != null ? dragManager.GlobalDriftOffset : Vector3.zero;
        }

        private Vector3 ResolveCurrentVelocity(Vector3 positionWS)
        {
            float time = ResolveFluidDecalClockSeconds();
            float3 sampledCurrent = CurrentManager.SampleCurrent(
                new float3(positionWS.x, positionWS.y, positionWS.z),
                time,
                currentNoiseScale,
                currentTimeScale,
                currentStrength,
                currentVerticalFactor);
            Vector3 authoredCurrent = Vector3.zero;
            IAmbientCurrentReadModel ambientCurrent = _ambientCurrentReadModel;
            if (ambientCurrent != null)
                ambientCurrent.TrySampleAuthoredCurrent(positionWS, out authoredCurrent);
            Vector3 resolvedCurrent = new Vector3(sampledCurrent.x, sampledCurrent.y, sampledCurrent.z) + authoredCurrent;
            resolvedCurrent.y *= currentVerticalFactor;
            return resolvedCurrent;
        }

        private void AdvanceFluidDecalClock(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            _fluidDecalClockSeconds = math.min(FluidDecalClockMaxSeconds, _fluidDecalClockSeconds + deltaTime);
        }

        private float ResolveFluidDecalClockSeconds()
        {
            return _fluidDecalClockSeconds;
        }

        private void TryRegister()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void CacheRegistryServicesCold()
        {
            if (_playerContext == null)
                _playerContext = GlobalRegistry.Player;

            if (_sargassumDrag == null)
                WorldRuntimeReferenceUtility.TryResolveSargassumGlobalDragManager(ref _sargassumDrag);

            if (_ambientCurrentReadModel == null)
                _ambientCurrentReadModel = GlobalRegistry.AmbientCurrent;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
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
            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

            _registeredTick = false;
        }

        private void ApplyRuntimeOffsetToCachedState(Vector3 runtimeOffset)
        {
            _previousGlobalDriftOffset += runtimeOffset;
            if (_decalStates == null)
                return;

            for (int i = 0; i < _decalStates.Length; i++)
            {
                if (_decalStates[i].Active == 0)
                    continue;

                FluidDecalState decal = _decalStates[i];
                decal.PositionWS += runtimeOffset;
                _decalStates[i] = decal;
            }

            if (_pressureSprayStates == null)
                return;

            for (int i = 0; i < _pressureSprayStates.Length; i++)
            {
                if (_pressureSprayStates[i].Active == 0)
                    continue;

                PressureSprayState spray = _pressureSprayStates[i];
                spray.PositionWS += runtimeOffset;
                _pressureSprayStates[i] = spray;
            }
        }

        private void SanitizeSettings()
        {
            maxDecalCount = Mathf.Clamp(maxDecalCount, 1, 32);
            maxPressureSprayCount = Mathf.Clamp(maxPressureSprayCount, 1, 64);
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
            wakeSiltColor.a = Mathf.Clamp01(wakeSiltColor.a);
            pressureSprayColor.a = Mathf.Clamp01(pressureSprayColor.a);
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

using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Environment;
using Hecton8.Gameplay;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    /// <summary>
    /// Owns abyssal hydrothermal updraft sampling, heat-hazard registration, cable entanglement metadata,
    /// and the indirect black-smoke plume simulation used by deep thermal vents.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-102)]
    public sealed class AbyssalThermalManager : MonoBehaviour, ITickable, ISlowTickable
    {
        public struct ThermalFlowSample
        {
            public bool HasFlow;
            public Vector3 FlowVelocityWS;
            public float Heat01;
            public float DragMultiplier;
            public bool IsCableZone;
            public Vector3 CableAnchorWS;
            public float CableTension01;
            public float CableCutProgress01;
            public float CableEscapeSuppression01;
        }

        private struct ThermalVentState
        {
            public Vector3 PositionWS;
            public Vector3 CableAnchorWS;
            public float RadiusWS;
            public float HeightWS;
            public float UpdraftVelocity;
            public float HeatIntensity;
            public float SmokeDensity;
            public float CableRadiusWS;
            public int HazardSourceId;
        }

        private struct ThermalVentGpuData
        {
            public Vector3 PositionWS;
            public float RadiusWS;
            public float HeightWS;
            public float UpdraftVelocity;
            public float HeatIntensity;
            public float SmokeDensity;
            public Vector2 Padding;
        }

        private struct AshParticleData
        {
            public Vector3 PositionWS;
            public float Size;
            public Vector3 VelocityWS;
            public float Alpha;
            public float Lifetime;
            public float MaxLifetime;
            public float Seed;
            public float VentIndex;
        }

        private static readonly int _ParticlesReadId = Shader.PropertyToID("_ParticlesRead");
        private static readonly int _ParticlesWriteId = Shader.PropertyToID("_ParticlesWrite");
        private static readonly int _ThermalVentsId = Shader.PropertyToID("_ThermalVents");
        private static readonly int _ParticleCountId = Shader.PropertyToID("_ParticleCount");
        private static readonly int _ActiveVentCountId = Shader.PropertyToID("_ActiveVentCount");
        private static readonly int _DeltaTimeId = Shader.PropertyToID("_DeltaTime");
        private static readonly int _SimulationTimeId = Shader.PropertyToID("_SimulationTime");
        private static readonly int _CameraPositionId = Shader.PropertyToID("_CameraPositionWS");
        private static readonly int _CameraRightId = Shader.PropertyToID("_CameraRightWS");
        private static readonly int _CameraUpId = Shader.PropertyToID("_CameraUpWS");
        private static readonly int _ParticleSizeRangeId = Shader.PropertyToID("_ParticleSizeRange");
        private static readonly int _NoiseParamsId = Shader.PropertyToID("_NoiseParams");
        private static readonly int _MaxViewDistanceId = Shader.PropertyToID("_MaxViewDistance");
        private static readonly int _AshParticlesId = Shader.PropertyToID("_AshParticles");
        private static readonly int _AshTintId = Shader.PropertyToID("_AshTint");
        private static readonly int _AshHotTintId = Shader.PropertyToID("_AshHotTint");
        private static readonly int _SoftnessId = Shader.PropertyToID("_Softness");

        private const int MaxVentCapacity = 16;
        private const int MaxAnchorScanCapacity = 32;
        private const int MaxSmokeParticleCapacity = 8192;
        private const int ParticleStride = 40;
        private const int VentStride = 32;
        private const int IndirectArgsCount = 4;
        private const uint ThermalHashSeed = 0xC6BC2796u;

        private static AbyssalThermalManager _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        [Header("── Runtime Wiring ──────────────────")]
        [SerializeField]
        [Tooltip("Compute shader that simulates the hydrothermal ash plume.")]
        private ComputeShader blackSmokeCompute;

        [SerializeField]
        [Tooltip("Transparent billboard material used by DrawProceduralIndirect for thermal ash.")]
        private Material blackSmokeMaterial;

        [SerializeField]
        [Tooltip("Optional direct biome director override. Runtime resolves the active singleton when null.")]
        private BiomeMatrixDirector biomeMatrixDirector;

        [SerializeField]
        [Tooltip("Optional direct zone director override. Runtime resolves the active singleton when null.")]
        private WorldZoneDirector worldZoneDirector;

        [SerializeField]
        [Tooltip("Optional direct cut-manager override used when abyssal cables need to be severed by the cutter.")]
        private SargassumCutManager cutManager;

        [SerializeField]
        [Tooltip("Optional direct player override used for isolated validation scenes.")]
        private Transform playerTransform;

        [SerializeField]
        [Tooltip("Optional direct camera override for procedural smoke visibility and billboarding.")]
        private Camera viewCamera;

        [Header("── Thermal Vent Field ───────────────")]
        [SerializeField, Range(900f, 6000f)]
        [Tooltip("Minimum evaluated depth in meters before hydrothermal vents are allowed to arm.")]
        private float abyssalVentStartDepthMeters = 950f;

        [SerializeField, Range(1, MaxVentCapacity)]
        [Tooltip("Hard cap for active thermal vents registered into the local abyssal field.")]
        private int maxActiveVentCount = 10;

        [SerializeField, Range(1, 4)]
        [Tooltip("Maximum deterministic vent chimneys authored around one qualifying cartographer zone.")]
        private int maxVentsPerAnchor = 2;

        [SerializeField, Range(0.1f, 0.9f)]
        [Tooltip("Normalized fraction of the zone activation radius used when placing thermal vents around an anchor.")]
        private float ventAnchorRadiusFraction = 0.32f;

        [SerializeField, Range(2f, 30f)]
        [Tooltip("Minimum world-space hydrothermal vent radius.")]
        private float ventRadiusMin = 5f;

        [SerializeField, Range(4f, 40f)]
        [Tooltip("Maximum world-space hydrothermal vent radius.")]
        private float ventRadiusMax = 13f;

        [SerializeField, Range(4f, 90f)]
        [Tooltip("Vertical plume height used for updraft influence and smoke falloff.")]
        private float ventHeight = 24f;

        [SerializeField, Range(1f, 40f)]
        [Tooltip("Peak upward flow velocity contributed by the vent core before ocean-current blending.")]
        private float ventUpdraftVelocity = 14f;

        [SerializeField, Range(0.1f, 8f)]
        [Tooltip("Additional drag multiplier applied inside the strongest updraft core.")]
        private float ventDragMultiplier = 1.55f;

        [SerializeField, Range(1f, 60f)]
        [Tooltip("Heat intensity registered into HectonHazardManager for each vent.")]
        private float ventHeatIntensity = 18f;

        [SerializeField, Range(0.25f, 2f)]
        [Tooltip("Multiplier that expands the heat-hazard radius beyond the raw updraft radius.")]
        private float ventHeatRadiusMultiplier = 1.2f;

        [Header("── Bio-Cable Zones ─────────────────")]
        [SerializeField, Range(0.5f, 1.5f)]
        [Tooltip("Multiplier applied to qualifying cartographer service/power radii when resolving cable entanglement.")]
        private float cableRadiusMultiplier = 0.58f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Additional planar offset applied toward the player when resolving the cable anchor point.")]
        private float cableAnchorPull = 0.8f;

        [SerializeField, Range(0.1f, 6f)]
        [Tooltip("Recent-cut query radius used when checking whether a cable snare has been severed by the cutter.")]
        private float cableCutQueryRadius = 1.2f;

        [SerializeField, Range(0.01f, 1f)]
        [Tooltip("Normalized recent-cut weight required before a cable snare starts to release.")]
        private float cableCutReleaseThreshold = 0.24f;

        [SerializeField, Range(0.05f, 2f)]
        [Tooltip("Cooldown between automatic cutter-driven cable cut stamps while the beam stays active.")]
        private float cableCutStampInterval = 0.12f;

        [SerializeField, Range(0.1f, 3f)]
        [Tooltip("World-space radius stamped into the shared cut mask while the laser cutter severs a cable knot.")]
        private float cableCutStampRadius = 1.05f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Strength written into the shared cut mask when a cable knot is being severed.")]
        private float cableCutStampStrength = 0.82f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Forward offset applied from the active cutter transform before stamping the cable sever cut.")]
        private float cableCutForwardOffset = 0.95f;

        [Header("── Black Smoke ─────────────────────")]
        [SerializeField, Range(256, MaxSmokeParticleCapacity)]
        [Tooltip("Maximum compute-simulated ash particles rendered by the abyssal smoke pass.")]
        private int smokeParticleCount = 4096;

        [SerializeField, Range(0.02f, 0.8f)]
        [Tooltip("Minimum particle billboard size.")]
        private float smokeParticleSizeMin = 0.06f;

        [SerializeField, Range(0.04f, 1.2f)]
        [Tooltip("Maximum particle billboard size.")]
        private float smokeParticleSizeMax = 0.18f;

        [SerializeField, Range(0f, 6f)]
        [Tooltip("Lateral noise applied to smoke particles so the plume reads as turbulent rather than linear.")]
        private float smokeLateralDrift = 1.35f;

        [SerializeField, Range(0f, 6f)]
        [Tooltip("Small upward turbulence variation layered onto the main updraft.")]
        private float smokeUpdraftJitter = 1.1f;

        [SerializeField, Range(0f, 4f)]
        [Tooltip("Noise amplitude used by the compute sim when decorrelating particles from one another.")]
        private float smokeNoiseWeight = 0.75f;

        [SerializeField, Range(10f, 300f)]
        [Tooltip("Maximum camera distance where ash keeps meaningful opacity. Beyond this the plume fades aggressively to control overdraw.")]
        private float smokeMaxViewDistance = 95f;

        [SerializeField]
        [Tooltip("Cold ash tint used by the procedural smoke shader.")]
        private Color smokeTint = new Color(0.08f, 0.08f, 0.08f, 0.28f);

        [SerializeField]
        [Tooltip("Hot-core tint used near fresh hydrothermal emission.")]
        private Color smokeHotTint = new Color(0.22f, 0.17f, 0.12f, 0.34f);

        [SerializeField, Range(0.5f, 4f)]
        [Tooltip("Radial falloff sharpness for the procedural ash billboards. Higher values tighten the plume silhouette and reduce overdraw.")]
        private float smokeSoftness = 2.2f;

        [SerializeField]
        [Tooltip("Shadow mode used by the procedural smoke draw.")]
        private ShadowCastingMode smokeShadowCastingMode = ShadowCastingMode.Off;

        [Header("── Diagnostics ─────────────────────")]
        [SerializeField]
        [Tooltip("Current active hydrothermal vent count.")]
        private int _debugActiveVentCount;

        [SerializeField]
        [Tooltip("Current active cable-zone count derived from cartographer service and power anchors.")]
        private int _debugCableZoneCount;

        [SerializeField]
        [Tooltip("Current procedural smoke bounds.")]
        private Bounds _debugSmokeBounds;

        [SerializeField]
        [Tooltip("True when the abyssal manager currently considers the local biome deep enough for thermal vent simulation.")]
        private bool _debugAbyssalContext;

        [SerializeField]
        [Tooltip("True while the laser cutter beam is actively severing an abyssal cable knot.")]
        private bool _debugCutterSeveringCable;

        [SerializeField]
        [Tooltip("Latest cut progress reported by the active cable zone sampler.")]
        private float _debugCableCutProgress01;

        // COLD ALLOC: List<WorldZoneAnchor>[32] - reusable runtime cartographer anchor scratch list for abyssal vent selection - owner: AbyssalThermalManager
        private readonly List<WorldZoneAnchor> _zoneAnchors = new List<WorldZoneAnchor>(MaxAnchorScanCapacity);
        // COLD ALLOC: Plane[6] - frustum planes for smoke visibility checks - owner: AbyssalThermalManager
        private readonly Plane[] _frustumPlanes = new Plane[6];

        private ThermalVentState[] _ventStates;
        private ThermalVentGpuData[] _ventGpuData;
        private AshParticleData[] _initialParticles;
        private ComputeBuffer _particleBufferA;
        private ComputeBuffer _particleBufferB;
        private ComputeBuffer _ventBuffer;
        private ComputeBuffer _argsBuffer;
        private MaterialPropertyBlock _materialPropertyBlock;
        private Bounds _smokeBounds;
        private bool _registeredTick;
        private bool _registeredSlowTick;
        private bool _cutterBeamActive;
        private bool _hasSmokeData;
        private int _kernelIndex = -1;
        private int _threadGroupSizeX = 64;
        private int _dispatchGroupCount = 1;
        private int _frameParity;
        private int _activeVentCount;
        private int _activeCableZoneCount;
        private int _instanceId;
        private float _simulationTime;
        private float _cableCutStampCooldown;
        private Transform _activeCutterTransform;

        public static AbyssalThermalManager Instance => _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogError("[AbyssalThermalManager] Duplicate instance detected. Destroying the newer component.", this);
                Destroy(this);
                return;
            }

            _instance = this;
            _instanceId = GetHashCode();
            SanitizeSettings();
            ResolveDependencies();
            EnsureStorage();
            EnsureBuffers();
            ConfigureIndirectArgs();
            ClearHazardSources();
            RebuildVentField();
        }

        private void OnEnable()
        {
            LaserCutter.OnBeamStateChanged += HandleCutterBeamStateChanged;
            ResolveDependencies();
            EnsureStorage();
            EnsureBuffers();
            ConfigureIndirectArgs();
            RebuildVentField();
            TryRegister();
        }

        private void OnDisable()
        {
            LaserCutter.OnBeamStateChanged -= HandleCutterBeamStateChanged;
            _cutterBeamActive = false;
            _activeCutterTransform = null;
            _debugCutterSeveringCable = false;
            ClearHazardSources();
            TryUnregister();
        }

        private void OnDestroy()
        {
            LaserCutter.OnBeamStateChanged -= HandleCutterBeamStateChanged;
            ClearHazardSources();
            ReleaseBuffers();

            if (_instance == this)
                _instance = null;
        }

        /// <summary>
        /// Advances the thermal ash simulation and renders one indirect smoke draw while the local abyssal context is active.
        /// </summary>
        /// <param name="dt">Frame delta supplied by GameTickManager.</param>
        public void Tick(float dt)
        {
            ResolveDependencies();
            float deltaTime = Mathf.Max(0f, dt);
            if (_debugCutterSeveringCable && !_cutterBeamActive)
                _debugCutterSeveringCable = false;
            UpdateCableCutting(deltaTime);

            if (!_hasSmokeData || blackSmokeCompute == null || blackSmokeMaterial == null || _activeVentCount <= 0)
                return;

            _simulationTime += deltaTime;
            BindSmokeUniforms(deltaTime);
            blackSmokeCompute.Dispatch(_kernelIndex, _dispatchGroupCount, 1, 1);
            _frameParity ^= 1;

            if (IsSmokeVisible())
                RenderSmoke();
        }

        /// <summary>
        /// Rebuilds local vent and cable metadata from the current abyssal cartographer context.
        /// </summary>
        public void SlowTick()
        {
            ResolveDependencies();
            RebuildVentField();
        }

        /// <summary>
        /// Samples hydrothermal flow and cable entanglement without allocating.
        /// </summary>
        /// <param name="positionWS">World-space sample point.</param>
        /// <param name="radiusWS">Additional sample radius.</param>
        /// <param name="sample">Resolved flow and cable payload.</param>
        /// <returns>True when any updraft or cable influence is active at the sample point.</returns>
        public bool SampleThermalFlow(Vector3 positionWS, float radiusWS, out ThermalFlowSample sample)
        {
            sample = default;
            sample.DragMultiplier = 1f;

            if (_activeVentCount <= 0)
                return false;

            float effectiveRadius = Mathf.Max(0.1f, radiusWS);
            float strongestCable = 0f;
            Vector3 strongestCableAnchor = positionWS;
            float strongestCableCut = 0f;

            for (int i = 0; i < _activeVentCount; i++)
            {
                ThermalVentState vent = _ventStates[i];
                float ventRadius = Mathf.Max(0.1f, vent.RadiusWS + effectiveRadius);
                Vector2 planarDelta = new Vector2(positionWS.x - vent.PositionWS.x, positionWS.z - vent.PositionWS.z);
                float planarDistance = planarDelta.magnitude;
                if (planarDistance <= ventRadius)
                {
                    float radialFalloff = 1f - planarDistance / Mathf.Max(ventRadius, 0.001f);
                    float baseVentY = vent.PositionWS.y;
                    float heightGate = 1f - Mathf.Clamp01((positionWS.y - baseVentY) / Mathf.Max(vent.HeightWS, 0.001f));
                    if (heightGate > 0f)
                    {
                        float ventWeight = radialFalloff * heightGate;
                        Vector3 swirlDirection = planarDistance > 0.0001f
                            ? new Vector3(-planarDelta.y / planarDistance, 0f, planarDelta.x / planarDistance)
                            : Vector3.zero;
                        sample.HasFlow = true;
                        sample.Heat01 = Mathf.Max(sample.Heat01, vent.HeatIntensity * ventWeight);
                        sample.DragMultiplier = Mathf.Max(sample.DragMultiplier, Mathf.Lerp(1f, ventDragMultiplier, ventWeight));
                        sample.FlowVelocityWS += Vector3.up * (vent.UpdraftVelocity * ventWeight);
                        sample.FlowVelocityWS += swirlDirection * (vent.UpdraftVelocity * 0.12f * ventWeight);
                    }
                }

                float cableRadius = Mathf.Max(0.1f, vent.CableRadiusWS + effectiveRadius);
                Vector2 cableDelta = new Vector2(positionWS.x - vent.CableAnchorWS.x, positionWS.z - vent.CableAnchorWS.z);
                float cableDistance = cableDelta.magnitude;
                if (cableDistance > cableRadius)
                    continue;

                float cableWeight = 1f - cableDistance / Mathf.Max(cableRadius, 0.001f);
                if (cableWeight <= strongestCable)
                    continue;

                strongestCable = cableWeight;
                strongestCableAnchor = ResolveCableAnchor(positionWS, vent.CableAnchorWS);
                strongestCableCut = ResolveCableCutProgress(positionWS, strongestCableAnchor, cableRadius);
            }

            if (strongestCable > 0f)
            {
                sample.IsCableZone = true;
                sample.CableAnchorWS = strongestCableAnchor;
                sample.CableCutProgress01 = strongestCableCut;
                sample.CableEscapeSuppression01 = 1f - strongestCableCut;
                sample.CableTension01 = strongestCable * sample.CableEscapeSuppression01;
            }

            _debugCableCutProgress01 = strongestCableCut;
            return sample.HasFlow || sample.IsCableZone;
        }

        private void ResolveDependencies()
        {
            if (biomeMatrixDirector == null)
                biomeMatrixDirector = BiomeMatrixDirector.ActiveRuntimeInstance;

            if (worldZoneDirector == null)
                worldZoneDirector = WorldZoneDirector.ActiveRuntimeInstance;

            if (cutManager == null)
                cutManager = SargassumCutManager.Instance;

            if (playerTransform == null)
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);

            if (viewCamera == null && playerTransform != null)
                viewCamera = playerTransform.GetComponentInChildren<Camera>(true);
        }

        private void SanitizeSettings()
        {
            maxActiveVentCount = Mathf.Clamp(maxActiveVentCount, 1, MaxVentCapacity);
            maxVentsPerAnchor = Mathf.Clamp(maxVentsPerAnchor, 1, 4);
            ventAnchorRadiusFraction = Mathf.Clamp(ventAnchorRadiusFraction, 0.1f, 0.9f);
            ventRadiusMin = Mathf.Clamp(ventRadiusMin, 2f, ventRadiusMax);
            ventRadiusMax = Mathf.Max(ventRadiusMin, ventRadiusMax);
            ventHeight = Mathf.Clamp(ventHeight, 4f, 90f);
            ventUpdraftVelocity = Mathf.Clamp(ventUpdraftVelocity, 1f, 40f);
            ventDragMultiplier = Mathf.Clamp(ventDragMultiplier, 1f, 8f);
            ventHeatIntensity = Mathf.Clamp(ventHeatIntensity, 1f, 60f);
            ventHeatRadiusMultiplier = Mathf.Clamp(ventHeatRadiusMultiplier, 0.25f, 2f);
            cableRadiusMultiplier = Mathf.Clamp(cableRadiusMultiplier, 0.5f, 1.5f);
            cableAnchorPull = Mathf.Clamp(cableAnchorPull, 0f, 2f);
            cableCutQueryRadius = Mathf.Clamp(cableCutQueryRadius, 0.1f, 6f);
            cableCutReleaseThreshold = Mathf.Clamp01(cableCutReleaseThreshold);
            cableCutStampInterval = Mathf.Clamp(cableCutStampInterval, 0.05f, 2f);
            cableCutStampRadius = Mathf.Clamp(cableCutStampRadius, 0.1f, 3f);
            cableCutStampStrength = Mathf.Clamp01(cableCutStampStrength);
            cableCutForwardOffset = Mathf.Clamp(cableCutForwardOffset, 0f, 2f);
            smokeParticleCount = Mathf.Clamp(smokeParticleCount, 256, MaxSmokeParticleCapacity);
            smokeParticleSizeMin = Mathf.Clamp(smokeParticleSizeMin, 0.02f, smokeParticleSizeMax);
            smokeParticleSizeMax = Mathf.Max(smokeParticleSizeMin, smokeParticleSizeMax);
            smokeLateralDrift = Mathf.Clamp(smokeLateralDrift, 0f, 6f);
            smokeUpdraftJitter = Mathf.Clamp(smokeUpdraftJitter, 0f, 6f);
            smokeNoiseWeight = Mathf.Clamp(smokeNoiseWeight, 0f, 4f);
            smokeMaxViewDistance = Mathf.Clamp(smokeMaxViewDistance, 10f, 300f);
            smokeSoftness = Mathf.Clamp(smokeSoftness, 0.5f, 4f);
        }

        private void EnsureStorage()
        {
            if (_ventStates == null || _ventStates.Length != MaxVentCapacity)
            {
                // COLD ALLOC: ThermalVentState[16] - CPU vent metadata and cable anchors for abyssal sampling - owner: AbyssalThermalManager
                _ventStates = new ThermalVentState[MaxVentCapacity];
            }

            if (_ventGpuData == null || _ventGpuData.Length != MaxVentCapacity)
            {
                // COLD ALLOC: ThermalVentGpuData[16] - CPU upload staging for hydrothermal vent compute data - owner: AbyssalThermalManager
                _ventGpuData = new ThermalVentGpuData[MaxVentCapacity];
            }

            if (_initialParticles == null || _initialParticles.Length != smokeParticleCount)
            {
                // COLD ALLOC: AshParticleData[smokeParticleCount] - deterministic initial plume state for abyssal smoke ping-pong buffers - owner: AbyssalThermalManager
                _initialParticles = new AshParticleData[smokeParticleCount];
            }

            _materialPropertyBlock ??= new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - abyssal smoke draw parameters - owner: AbyssalThermalManager
        }

        private void EnsureBuffers()
        {
            EnsureBuffer(ref _particleBufferA, smokeParticleCount, ParticleStride, ComputeBufferType.Structured);
            EnsureBuffer(ref _particleBufferB, smokeParticleCount, ParticleStride, ComputeBufferType.Structured);
            EnsureBuffer(ref _ventBuffer, MaxVentCapacity, VentStride, ComputeBufferType.Structured);
            EnsureBuffer(ref _argsBuffer, 1, sizeof(uint) * IndirectArgsCount, ComputeBufferType.IndirectArguments);

            if (blackSmokeCompute == null)
                return;

            if (_kernelIndex < 0)
            {
                _kernelIndex = blackSmokeCompute.FindKernel("CSMain");
                blackSmokeCompute.GetKernelThreadGroupSizes(_kernelIndex, out uint groupSizeX, out _, out _);
                _threadGroupSizeX = Mathf.Max(1, (int)groupSizeX);
            }

            _dispatchGroupCount = Mathf.Max(1, Mathf.CeilToInt(smokeParticleCount / (float)_threadGroupSizeX));
        }

        private static void EnsureBuffer(ref ComputeBuffer buffer, int count, int stride, ComputeBufferType type)
        {
            if (buffer != null && buffer.count == count && buffer.stride == stride)
                return;

            if (buffer != null)
            {
                buffer.Release();
                buffer = null;
            }

            // COLD ALLOC: ComputeBuffer[count] - persistent abyssal smoke or vent GPU storage - owner: AbyssalThermalManager
            buffer = new ComputeBuffer(count, stride, type);
        }

        private void ConfigureIndirectArgs()
        {
            if (_argsBuffer == null)
                return;

            uint[] args =
            {
                6u,
                (uint)smokeParticleCount,
                0u,
                0u
            };
            _argsBuffer.SetData(args);
        }

        private void ReleaseBuffers()
        {
            ReleaseBuffer(ref _particleBufferA);
            ReleaseBuffer(ref _particleBufferB);
            ReleaseBuffer(ref _ventBuffer);
            ReleaseBuffer(ref _argsBuffer);
        }

        private static void ReleaseBuffer(ref ComputeBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private void RebuildVentField()
        {
            _activeVentCount = 0;
            _activeCableZoneCount = 0;
            _debugAbyssalContext = IsAbyssalThermalContext();
            _debugActiveVentCount = 0;
            _debugCableZoneCount = 0;

            if (!_debugAbyssalContext)
            {
                _hasSmokeData = false;
                ClearHazardSources();
                UploadVentBuffer();
                return;
            }

            WorldZoneAnchor.CopyActiveAnchorsTo(_zoneAnchors);
            Vector3 playerPosition = playerTransform != null ? playerTransform.position : transform.position;
            for (int i = 0; i < _zoneAnchors.Count && _activeVentCount < maxActiveVentCount; i++)
            {
                WorldZoneAnchor anchor = _zoneAnchors[i];
                if (!IsThermalAnchor(anchor))
                    continue;

                float holdWeight = Mathf.Max(anchor.EvaluateHoldWeight(playerPosition), anchor.EvaluateActivationWeight(playerPosition));
                if (holdWeight <= 0.01f)
                    continue;

                int spawnCount = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(1f, maxVentsPerAnchor, holdWeight)), 1, maxVentsPerAnchor);
                for (int ventIndex = 0; ventIndex < spawnCount && _activeVentCount < maxActiveVentCount; ventIndex++)
                {
                    RegisterVent(anchor, ventIndex, holdWeight);
                }

                _activeCableZoneCount++;
            }

            _debugActiveVentCount = _activeVentCount;
            _debugCableZoneCount = _activeCableZoneCount;
            _hasSmokeData = _activeVentCount > 0 && blackSmokeCompute != null && blackSmokeMaterial != null;

            UpdateSmokeBounds();
            UpdateHazardSources();
            UploadVentBuffer();
            ResetParticles();
        }

        private void RegisterVent(WorldZoneAnchor anchor, int ventIndex, float anchorWeight)
        {
            Vector3 anchorPosition = anchor.transform.position;
            float anchorRadius = Mathf.Max(12f, anchor.ActivationRadius * ventAnchorRadiusFraction);
            uint hashIndex = (uint)(_activeVentCount + 1);
            float radial01 = HashToFloat01(hashIndex, (uint)(ventIndex + 1), 0x68E31DA4u);
            float angle01 = HashToFloat01(hashIndex, (uint)(ventIndex + 1), 0xB5297A4Du);
            float angle = angle01 * Mathf.PI * 2f;
            float radialDistance = Mathf.Lerp(anchorRadius * 0.15f, anchorRadius, radial01);
            Vector3 ventOffset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radialDistance;
            Vector3 ventPosition = anchorPosition + ventOffset;
            float radius = Mathf.Lerp(ventRadiusMin, ventRadiusMax, HashToFloat01(hashIndex, (uint)(ventIndex + 5), 0x1B56C4E9u));
            float updraft = ventUpdraftVelocity * Mathf.Lerp(0.85f, 1.2f, anchorWeight);
            float heat = ventHeatIntensity * Mathf.Lerp(0.85f, 1.15f, anchorWeight);
            float smokeDensity = Mathf.Lerp(0.55f, 1.25f, HashToFloat01(hashIndex, (uint)(ventIndex + 9), 0x94D049BBu));
            float cableRadius = Mathf.Max(radius * 1.4f, anchor.ActivationRadius * cableRadiusMultiplier);

            _ventStates[_activeVentCount] = new ThermalVentState
            {
                PositionWS = ventPosition,
                CableAnchorWS = anchorPosition,
                RadiusWS = radius,
                HeightWS = ventHeight,
                UpdraftVelocity = updraft,
                HeatIntensity = heat,
                SmokeDensity = smokeDensity,
                CableRadiusWS = cableRadius,
                HazardSourceId = BuildHazardSourceId(_activeVentCount)
            };

            _activeVentCount++;
        }

        private void UpdateHazardSources()
        {
            for (int i = 0; i < MaxVentCapacity; i++)
            {
                if (i < _activeVentCount)
                {
                    ThermalVentState vent = _ventStates[i];
                    float hazardRadius = Mathf.Max(vent.RadiusWS, vent.RadiusWS * ventHeatRadiusMultiplier);
                    HectonHazardManager.Register(vent.HazardSourceId, vent.PositionWS, vent.HeatIntensity, hazardRadius, HazardType.Heat);
                }
                else
                {
                    HectonHazardManager.Unregister(BuildHazardSourceId(i));
                }
            }
        }

        private void ClearHazardSources()
        {
            for (int i = 0; i < MaxVentCapacity; i++)
                HectonHazardManager.Unregister(BuildHazardSourceId(i));
        }

        private void UploadVentBuffer()
        {
            if (_ventBuffer == null)
                return;

            for (int i = 0; i < MaxVentCapacity; i++)
            {
                if (i < _activeVentCount)
                {
                    ThermalVentState vent = _ventStates[i];
                    _ventGpuData[i] = new ThermalVentGpuData
                    {
                        PositionWS = vent.PositionWS,
                        RadiusWS = vent.RadiusWS,
                        HeightWS = vent.HeightWS,
                        UpdraftVelocity = vent.UpdraftVelocity,
                        HeatIntensity = vent.HeatIntensity,
                        SmokeDensity = vent.SmokeDensity,
                        Padding = Vector2.zero
                    };
                }
                else
                {
                    _ventGpuData[i] = default;
                }
            }

            _ventBuffer.SetData(_ventGpuData);
        }

        private void ResetParticles()
        {
            if (_initialParticles == null || _particleBufferA == null || _particleBufferB == null)
                return;

            if (_activeVentCount <= 0)
            {
                System.Array.Clear(_initialParticles, 0, _initialParticles.Length);
                _particleBufferA.SetData(_initialParticles);
                _particleBufferB.SetData(_initialParticles);
                return;
            }

            for (int i = 0; i < smokeParticleCount; i++)
            {
                int ventIndex = i % _activeVentCount;
                ThermalVentState vent = _ventStates[ventIndex];
                float seed = HashToFloat01((uint)i, (uint)ventIndex, 0xA24BAEDCu);
                float angle = HashToFloat01((uint)i, (uint)ventIndex, 0xE7037ED1u) * Mathf.PI * 2f;
                float radiusT = Mathf.Sqrt(HashToFloat01((uint)i, (uint)ventIndex, 0x8EBC6AF1u));
                float radialDistance = vent.RadiusWS * 0.45f * radiusT;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radialDistance;
                Vector3 position = vent.PositionWS + offset + Vector3.up * Mathf.Lerp(0.2f, 1.6f, HashToFloat01((uint)i, (uint)ventIndex, 0x589965CDu));
                Vector3 velocity = Vector3.up * (vent.UpdraftVelocity * Mathf.Lerp(0.65f, 1.05f, seed));
                float size = Mathf.Lerp(smokeParticleSizeMin, smokeParticleSizeMax, HashToFloat01((uint)i, (uint)ventIndex, 0x1D8E4E27u));
                float maxLifetime = Mathf.Lerp(1.8f, 5.6f, HashToFloat01((uint)i, (uint)ventIndex, 0xA4093822u));

                _initialParticles[i] = new AshParticleData
                {
                    PositionWS = position,
                    Size = size,
                    VelocityWS = velocity,
                    Alpha = 0.18f,
                    Lifetime = maxLifetime * seed,
                    MaxLifetime = maxLifetime,
                    Seed = seed,
                    VentIndex = ventIndex
                };
            }

            _particleBufferA.SetData(_initialParticles);
            _particleBufferB.SetData(_initialParticles);
            _frameParity = 0;
        }

        private void BindSmokeUniforms(float dt)
        {
            ComputeBuffer readBuffer = _frameParity == 0 ? _particleBufferA : _particleBufferB;
            ComputeBuffer writeBuffer = _frameParity == 0 ? _particleBufferB : _particleBufferA;
            Camera activeCamera = viewCamera;
            Vector3 cameraPosition = activeCamera != null ? activeCamera.transform.position : Vector3.zero;
            Vector3 cameraRight = activeCamera != null ? activeCamera.transform.right : Vector3.right;
            Vector3 cameraUp = activeCamera != null ? activeCamera.transform.up : Vector3.up;

            blackSmokeCompute.SetBuffer(_kernelIndex, _ParticlesReadId, readBuffer);
            blackSmokeCompute.SetBuffer(_kernelIndex, _ParticlesWriteId, writeBuffer);
            blackSmokeCompute.SetBuffer(_kernelIndex, _ThermalVentsId, _ventBuffer);
            blackSmokeCompute.SetInt(_ParticleCountId, smokeParticleCount);
            blackSmokeCompute.SetInt(_ActiveVentCountId, _activeVentCount);
            blackSmokeCompute.SetFloat(_DeltaTimeId, dt);
            blackSmokeCompute.SetFloat(_SimulationTimeId, _simulationTime);
            blackSmokeCompute.SetVector(_CameraPositionId, cameraPosition);
            blackSmokeCompute.SetVector(_ParticleSizeRangeId, new Vector4(smokeParticleSizeMin, smokeParticleSizeMax, 0f, 0f));
            blackSmokeCompute.SetVector(_NoiseParamsId, new Vector4(smokeLateralDrift, smokeUpdraftJitter, smokeNoiseWeight, 0f));
            blackSmokeCompute.SetFloat(_MaxViewDistanceId, smokeMaxViewDistance);

            _materialPropertyBlock.Clear();
            _materialPropertyBlock.SetBuffer(_AshParticlesId, writeBuffer);
            _materialPropertyBlock.SetVector(_CameraPositionId, cameraPosition);
            _materialPropertyBlock.SetVector(_CameraRightId, cameraRight);
            _materialPropertyBlock.SetVector(_CameraUpId, cameraUp);
            _materialPropertyBlock.SetFloat(_MaxViewDistanceId, smokeMaxViewDistance);
            _materialPropertyBlock.SetColor(_AshTintId, smokeTint);
            _materialPropertyBlock.SetColor(_AshHotTintId, smokeHotTint);
            _materialPropertyBlock.SetFloat(_SoftnessId, smokeSoftness);
        }

        private bool IsSmokeVisible()
        {
            if (viewCamera == null)
                return true;

            GeometryUtility.CalculateFrustumPlanes(viewCamera, _frustumPlanes);
            return GeometryUtility.TestPlanesAABB(_frustumPlanes, _smokeBounds);
        }

        private void RenderSmoke()
        {
            Graphics.DrawProceduralIndirect(
                blackSmokeMaterial,
                _smokeBounds,
                MeshTopology.Triangles,
                _argsBuffer,
                0,
                null,
                _materialPropertyBlock,
                smokeShadowCastingMode,
                false,
                gameObject.layer);
        }

        private void UpdateSmokeBounds()
        {
            if (_activeVentCount <= 0)
            {
                _smokeBounds = new Bounds(transform.position, Vector3.one * 4f);
                _debugSmokeBounds = _smokeBounds;
                return;
            }

            Vector3 min = _ventStates[0].PositionWS;
            Vector3 max = _ventStates[0].PositionWS + Vector3.up * _ventStates[0].HeightWS;
            for (int i = 0; i < _activeVentCount; i++)
            {
                ThermalVentState vent = _ventStates[i];
                Vector3 extents = new Vector3(vent.RadiusWS * 1.6f, vent.HeightWS, vent.RadiusWS * 1.6f);
                Vector3 ventMin = vent.PositionWS - extents;
                Vector3 ventMax = vent.PositionWS + extents;
                min = Vector3.Min(min, ventMin);
                max = Vector3.Max(max, ventMax);
            }

            _smokeBounds.SetMinMax(min, max);
            _debugSmokeBounds = _smokeBounds;
        }

        private void UpdateCableCutting(float dt)
        {
            if (_cableCutStampCooldown > 0f)
            {
                _cableCutStampCooldown -= dt;
                if (_cableCutStampCooldown < 0f)
                    _cableCutStampCooldown = 0f;
            }

            _debugCutterSeveringCable = false;
            if (!_cutterBeamActive || cutManager == null)
                return;

            Transform cutterTransform = _activeCutterTransform != null ? _activeCutterTransform : playerTransform;
            if (cutterTransform == null)
                return;

            if (_cableCutStampCooldown > 0f)
                return;

            Vector3 positionWS = cutterTransform.position;
            if (!TryResolveCableZone(positionWS, out _, out float cableTension, out _))
                return;

            if (cableTension <= 0.0001f)
                return;

            Vector3 forward = cutterTransform.forward;
            if (forward.sqrMagnitude <= 0.0001f)
                forward = Vector3.forward;

            Vector3 stampPosition = positionWS + forward.normalized * cableCutForwardOffset;
            cutManager.RegisterExternalCut(stampPosition, cableCutStampRadius, cableCutStampStrength, forward, 0.18f);
            _cableCutStampCooldown = cableCutStampInterval;
            _debugCutterSeveringCable = true;
        }

        private void HandleCutterBeamStateChanged(Transform cutterTransform, bool isActive)
        {
            _activeCutterTransform = isActive ? cutterTransform : null;
            _cutterBeamActive = isActive;
            if (!isActive)
                _debugCutterSeveringCable = false;
        }

        private bool TryResolveCableZone(Vector3 positionWS, out Vector3 cableAnchorWS, out float cableTension01, out float cableCutProgress01)
        {
            cableAnchorWS = positionWS;
            cableTension01 = 0f;
            cableCutProgress01 = 0f;
            if (_activeVentCount <= 0)
                return false;

            for (int i = 0; i < _activeVentCount; i++)
            {
                ThermalVentState vent = _ventStates[i];
                float cableRadius = Mathf.Max(0.1f, vent.CableRadiusWS);
                Vector2 planarDelta = new Vector2(positionWS.x - vent.CableAnchorWS.x, positionWS.z - vent.CableAnchorWS.z);
                float planarDistance = planarDelta.magnitude;
                if (planarDistance > cableRadius)
                    continue;

                float tension = 1f - planarDistance / Mathf.Max(cableRadius, 0.001f);
                if (tension <= cableTension01)
                    continue;

                cableTension01 = tension;
                cableAnchorWS = ResolveCableAnchor(positionWS, vent.CableAnchorWS);
                cableCutProgress01 = ResolveCableCutProgress(positionWS, cableAnchorWS, cableRadius);
            }

            return cableTension01 > 0f;
        }

        private Vector3 ResolveCableAnchor(Vector3 positionWS, Vector3 cableAnchorWS)
        {
            Vector3 planarDelta = positionWS - cableAnchorWS;
            planarDelta.y = 0f;
            if (planarDelta.sqrMagnitude <= 0.0001f || cableAnchorPull <= 0f)
                return cableAnchorWS;

            return cableAnchorWS + planarDelta.normalized * cableAnchorPull;
        }

        private float ResolveCableCutProgress(Vector3 positionWS, Vector3 cableAnchorWS, float cableRadiusWS)
        {
            if (cutManager == null)
                return 0f;

            float queryRadius = Mathf.Min(cableRadiusWS * 0.35f, cableCutQueryRadius);
            if (!cutManager.SampleRecentCutArea(positionWS, queryRadius, out float accumulatedAreaWS, out float strongestCut01))
                return 0f;

            float requiredArea = Mathf.PI * queryRadius * queryRadius * cableCutReleaseThreshold;
            float areaProgress = requiredArea > 0.0001f ? Mathf.Clamp01(accumulatedAreaWS / requiredArea) : 0f;
            float strengthProgress = Mathf.Clamp01(strongestCut01 / Mathf.Max(cableCutReleaseThreshold, 0.0001f));
            return Mathf.Clamp01(Mathf.Max(areaProgress, strengthProgress));
        }

        private bool IsAbyssalThermalContext()
        {
            if (biomeMatrixDirector == null || biomeMatrixDirector.CurrentDepthMeters < abyssalVentStartDepthMeters)
                return false;

            HectonBiomeFamilyProfile family = worldZoneDirector != null && worldZoneDirector.CurrentZone != null && worldZoneDirector.CurrentZone.DominantBiomeFamily != null
                ? worldZoneDirector.CurrentZone.DominantBiomeFamily
                : biomeMatrixDirector.CurrentFamilyProfile;
            return IsThermalBiomeFamily(family);
        }

        private bool IsThermalAnchor(WorldZoneAnchor anchor)
        {
            if (anchor == null)
                return false;

            if (anchor.Kind != WorldZoneAnchor.ZoneKind.Service &&
                anchor.Kind != WorldZoneAnchor.ZoneKind.Power &&
                anchor.Kind != WorldZoneAnchor.ZoneKind.Construction)
                return false;

            HectonBiomeFamilyProfile family = anchor.DominantBiomeFamily != null
                ? anchor.DominantBiomeFamily
                : biomeMatrixDirector != null ? biomeMatrixDirector.CurrentFamilyProfile : null;
            return IsThermalBiomeFamily(family);
        }

        private static bool IsThermalBiomeFamily(HectonBiomeFamilyProfile family)
        {
            if (family == null || string.IsNullOrWhiteSpace(family.familyId))
                return false;

            string familyId = family.familyId;
            return string.Equals(familyId, "biome.family.tectonic_spine", System.StringComparison.OrdinalIgnoreCase)
                   || string.Equals(familyId, "biome.family.chemosynthetic_brine", System.StringComparison.OrdinalIgnoreCase)
                   || string.Equals(familyId, "biome.family.metallic_hadal", System.StringComparison.OrdinalIgnoreCase)
                   || string.Equals(familyId, "biome.family.rift_spine", System.StringComparison.OrdinalIgnoreCase)
                   || string.Equals(familyId, "biome.family.volcanic_hadal", System.StringComparison.OrdinalIgnoreCase);
        }

        private int BuildHazardSourceId(int ventIndex)
        {
            return (_instanceId & 0x7FFF) * 64 + ventIndex + 1;
        }

        private void TryRegister()
        {
            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            if (!_registeredTick)
            {
                tickManager.Register((ITickable)this);
                _registeredTick = true;
            }

            if (!_registeredSlowTick)
            {
                tickManager.Register((ISlowTickable)this);
                _registeredSlowTick = true;
            }
        }

        private void TryUnregister()
        {
            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            if (_registeredTick)
            {
                tickManager.Unregister((ITickable)this);
                _registeredTick = false;
            }

            if (_registeredSlowTick)
            {
                tickManager.Unregister((ISlowTickable)this);
                _registeredSlowTick = false;
            }
        }

        private static float HashToFloat01(uint a, uint b, uint salt)
        {
            uint state = a * 747796405u + b * 2891336453u + ThermalHashSeed + salt;
            state ^= state >> 16;
            state *= 2246822519u;
            state ^= state >> 13;
            state *= 3266489917u;
            state ^= state >> 16;
            return (state & 0x00FFFFFFu) / 16777215f;
        }
    }
}

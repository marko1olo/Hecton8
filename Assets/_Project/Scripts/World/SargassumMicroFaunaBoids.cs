using Hecton8.Core;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Biolum;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    /// <summary>
    /// GPU boids confined to dense sargassum walls. Density comes from <see cref="SargassumGlobalDragManager"/>
    /// and panic comes from <see cref="SargassumCutManager"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-101)]
    public sealed class SargassumMicroFaunaBoids : MonoBehaviour, ITickable, ISlowTickable
    {
        private struct BoidData
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Seed;
            public float Panic;
        }

        private struct GrazingAnchorData
        {
            public Vector3 Position;
            public float Radius;
            public float Strength;
            public float Phase;
            public Vector2 Padding;
        }

        private struct MassiveThreatData
        {
            public Vector3 Position;
            public float InnerRadius;
            public float PanicRadius;
            public float Strength;
            public float RemainingDuration;
            public Vector3 Padding;
        }

        private const int BoidStride = 32;
        private const int GrazingAnchorStride = 32;
        private const int MassiveThreatStride = 40;
        private const int IndirectArgsCount = 5;
        private const uint HashSeed = 0x9E3779B9u;

        private static readonly int _BoidsBufferId = Shader.PropertyToID("_BoidsBuffer");
        private static readonly int _BoidsBufferReadId = Shader.PropertyToID("_BoidsBufferRead");
        private static readonly int _BoidsBufferWriteId = Shader.PropertyToID("_BoidsBufferWrite");
        private static readonly int _BoidCountId = Shader.PropertyToID("_BoidCount");
        private static readonly int _DeltaTimeId = Shader.PropertyToID("_DeltaTime");
        private static readonly int _FieldCenterId = Shader.PropertyToID("_FieldCenterWS");
        private static readonly int _FieldExtentsId = Shader.PropertyToID("_FieldExtents");
        private static readonly int _WaterLevelId = Shader.PropertyToID("_WaterLevel");
        private static readonly int _MinDepthId = Shader.PropertyToID("_MinDepthBelowSurface");
        private static readonly int _MaxDepthId = Shader.PropertyToID("_MaxDepthBelowSurface");
        private static readonly int _CruiseSpeedId = Shader.PropertyToID("_CruiseSpeed");
        private static readonly int _MaxSpeedId = Shader.PropertyToID("_MaxSpeed");
        private static readonly int _PanicSpeedBoostId = Shader.PropertyToID("_PanicSpeedBoost");
        private static readonly int _PerceptionRadiusId = Shader.PropertyToID("_PerceptionRadius");
        private static readonly int _SeparationRadiusId = Shader.PropertyToID("_SeparationRadius");
        private static readonly int _SeparationWeightId = Shader.PropertyToID("_SeparationWeight");
        private static readonly int _AlignmentWeightId = Shader.PropertyToID("_AlignmentWeight");
        private static readonly int _CohesionWeightId = Shader.PropertyToID("_CohesionWeight");
        private static readonly int _ContainmentWeightId = Shader.PropertyToID("_ContainmentWeight");
        private static readonly int _PanicWeightId = Shader.PropertyToID("_PanicWeight");
        private static readonly int _NoiseWeightId = Shader.PropertyToID("_NoiseWeight");
        private static readonly int _DensityThresholdId = Shader.PropertyToID("_DensityThreshold");
        private static readonly int _WindowThresholdId = Shader.PropertyToID("_WindowThreshold");
        private static readonly int _GradientWorldStepId = Shader.PropertyToID("_GradientWorldStep");
        private static readonly int _PanicThresholdId = Shader.PropertyToID("_PanicThreshold");
        private static readonly int _PanicDecayId = Shader.PropertyToID("_PanicDecay");
        private static readonly int _GrazingAnchorsId = Shader.PropertyToID("_GrazingAnchors");
        private static readonly int _GrazingAnchorCountId = Shader.PropertyToID("_GrazingAnchorCount");
        private static readonly int _GrazingWeightId = Shader.PropertyToID("_GrazingWeight");
        private static readonly int _GrazingRadiusId = Shader.PropertyToID("_GrazingRadius");
        private static readonly int _GrazingRestSpeedScaleId = Shader.PropertyToID("_GrazingRestSpeedScale");
        private static readonly int _GrazingRestHoldThresholdId = Shader.PropertyToID("_GrazingRestHoldThreshold");
        private static readonly int _CanopyAffinityWeightId = Shader.PropertyToID("_CanopyAffinityWeight");
        private static readonly int _SimulationTimeId = Shader.PropertyToID("_SimulationTime");
        private static readonly int _PlayerPositionId = Shader.PropertyToID("_PlayerPositionWS");
        private static readonly int _PlayerVelocityId = Shader.PropertyToID("_PlayerVelocityWS");
        private static readonly int _PlayerSpeedId = Shader.PropertyToID("_PlayerSpeed");
        private static readonly int _PanicPlayerSpeedThresholdId = Shader.PropertyToID("_PanicPlayerSpeedThreshold");
        private static readonly int _PanicPlayerRadiusId = Shader.PropertyToID("_PanicPlayerRadius");
        private static readonly int _PanicPlayerRadiusScaleId = Shader.PropertyToID("_PanicPlayerRadiusScale");
        private static readonly int _CameraAvoidPositionId = Shader.PropertyToID("_CameraAvoidPositionWS");
        private static readonly int _CameraAvoidRadiusId = Shader.PropertyToID("_CameraAvoidRadius");
        private static readonly int _CameraAvoidWeightId = Shader.PropertyToID("_CameraAvoidWeight");
        private static readonly int _MassiveThreatsId = Shader.PropertyToID("_MassiveThreats");
        private static readonly int _MassiveThreatCountId = Shader.PropertyToID("_MassiveThreatCount");
        private static readonly int _MassiveThreatWeightId = Shader.PropertyToID("_MassiveThreatWeight");
        private static readonly int _DensityTexId = Shader.PropertyToID("_DensityTex");
        private static readonly int _DensityWorldRectId = Shader.PropertyToID("_DensityWorldRect");
        private static readonly int _CutMaskTexId = Shader.PropertyToID("_CutMaskTex");
        private static readonly int _CutMaskWorldRectId = Shader.PropertyToID("_CutMaskWorldRect");
        private static readonly int _CutMaskActiveId = Shader.PropertyToID("_CutMaskActive");
        private static readonly int _GlobalDriftOffsetId = Shader.PropertyToID("_GlobalDriftOffset");
        private static readonly int _GlobalDriftDeltaId = Shader.PropertyToID("_GlobalDriftDelta");
        private static readonly int _DeepModeId = Shader.PropertyToID("_DeepMode");
        private static readonly int _DeepClusterWeightId = Shader.PropertyToID("_DeepClusterWeight");
        private static readonly int _HeadlightPanicId = Shader.PropertyToID("_HeadlightPanic");
        private static readonly int _ParasiteModeId = Shader.PropertyToID("_ParasiteMode");
        private static readonly int _ParasiteAffinityWeightId = Shader.PropertyToID("_ParasiteAffinityWeight");
        private static readonly int _ParasiteAggressionId = Shader.PropertyToID("_ParasiteAggression");
        private static readonly int _ParasiteLatchRadiusId = Shader.PropertyToID("_ParasiteLatchRadius");

        [Header("── Runtime Wiring ──────────────────")]
        [SerializeField]
        [Tooltip("Compute shader that simulates the micro-fauna flock.")]
        private ComputeShader boidCompute;

        [SerializeField]
        [Tooltip("Instanced mesh rendered for each micro-fauna boid.")]
        private Mesh boidMesh;

        [SerializeField]
        [Tooltip("Instanced material used by DrawMeshInstancedIndirect.")]
        private Material boidMaterial;

        [SerializeField]
        [Tooltip("Primary density owner. If null the controller resolves the active runtime singleton.")]
        private SargassumGlobalDragManager dragManager;

        [SerializeField]
        [Tooltip("Primary cut-mask owner. If null the controller resolves the active runtime singleton.")]
        private SargassumCutManager cutManager;

        [SerializeField]
        [Tooltip("Optional deep-sea biolum owner used when the flock switches from canopy grazing into abyssal bait-ball mode.")]
        private HectonBiolumManager biolumManager;

        [SerializeField]
        [Tooltip("Optional direct gameplay camera override for frustum culling.")]
        private Camera viewCamera;

        [SerializeField]
        [Tooltip("Optional direct player override used only to resolve the gameplay camera hierarchy.")]
        private Transform playerTransform;

        [Header("── Population ──────────────────")]
        [SerializeField, Range(128, 2048)]
        [Tooltip("Total boid count rendered and simulated on the GPU.")]
        private int boidCount = 768;

        [SerializeField, Range(0.15f, 1f)]
        [Tooltip("Minimum density required for spawn and containment.")]
        private float densityThreshold = 0.42f;

        [SerializeField, Range(0f, 0.75f)]
        [Tooltip("Maximum allowed window openness for valid spawn points. Lower values keep boids inside dense walls.")]
        private float windowThreshold = 0.32f;

        [SerializeField, Range(4, 32)]
        [Tooltip("Maximum rejection-sampling attempts per boid when rebuilding the spawn set.")]
        private int maxSpawnAttempts = 18;

        [Header("── Motion ──────────────────")]
        [SerializeField, Range(0.1f, 8f)]
        [Tooltip("Steady-state swim speed before panic boosts are applied.")]
        private float cruiseSpeed = 1.8f;

        [SerializeField, Range(0.1f, 12f)]
        [Tooltip("Hard velocity clamp for the GPU simulation.")]
        private float maxSpeed = 3.8f;

        [SerializeField, Range(0f, 8f)]
        [Tooltip("Additional speed unlocked while fleeing from the cut mask.")]
        private float panicSpeedBoost = 2.4f;

        [SerializeField, Range(0.25f, 8f)]
        [Tooltip("Neighbor perception radius used for cohesion and alignment.")]
        private float perceptionRadius = 2.25f;

        [SerializeField, Range(0.1f, 4f)]
        [Tooltip("Personal-space radius used for short-range separation.")]
        private float separationRadius = 0.85f;

        [SerializeField, Range(0f, 4f)]
        [Tooltip("Separation force weight.")]
        private float separationWeight = 1.85f;

        [SerializeField, Range(0f, 4f)]
        [Tooltip("Alignment force weight.")]
        private float alignmentWeight = 0.85f;

        [SerializeField, Range(0f, 4f)]
        [Tooltip("Cohesion force weight.")]
        private float cohesionWeight = 0.7f;

        [SerializeField, Range(0f, 8f)]
        [Tooltip("Force that keeps boids inside dense sargassum walls.")]
        private float containmentWeight = 3.4f;

        [SerializeField, Range(0f, 8f)]
        [Tooltip("Force applied away from fresh cuts.")]
        private float panicWeight = 4.2f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Low-amplitude deterministic wander added to avoid rigid movement.")]
        private float noiseWeight = 0.35f;

        [SerializeField, Range(0.05f, 4f)]
        [Tooltip("World-space sampling step used when computing density and cut gradients.")]
        private float gradientWorldStep = 0.8f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Cut-mask value that upgrades the flock into panic mode.")]
        private float panicThreshold = 0.08f;

        [SerializeField, Range(0.1f, 8f)]
        [Tooltip("Seconds^-1 decay applied to the per-boid panic accumulator.")]
        private float panicDecay = 1.4f;

        [Header("── Grazing & Threat Response ──────────────────")]
        [SerializeField, Range(4, 96)]
        [Tooltip("Deterministic pneumatocyst grazing anchors sampled inside dense canopy walls.")]
        private int grazingAnchorCount = 28;

        [SerializeField, Range(0.25f, 6f)]
        [Tooltip("World-space radius around each grazing anchor that attracts nearby boids.")]
        private float grazingRadius = 2.35f;

        [SerializeField, Range(0f, 4f)]
        [Tooltip("Attraction force toward nearby pneumatocyst grazing anchors while calm.")]
        private float grazingWeight = 1.25f;

        [SerializeField, Range(0f, 4f)]
        [Tooltip("Additional calm-state pull toward the densest nearby canopy so the flock stays inside the thickest walls instead of orbiting dead centers.")]
        private float canopyAffinityWeight = 0.85f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Minimum dense-wall value required before a grazing anchor is accepted.")]
        private float grazingDensityThreshold = 0.58f;

        [SerializeField, Range(0.05f, 0.6f)]
        [Tooltip("Speed scale applied while a calm boid is in a short feeding pause near a pneumatocyst anchor.")]
        private float grazingRestSpeedScale = 0.12f;

        [SerializeField, Range(0.1f, 1f)]
        [Tooltip("Minimum grazing hold intensity required before a boid can briefly freeze to imitate feeding.")]
        private float grazingRestHoldThreshold = 0.48f;

        [SerializeField, Range(0.5f, 8f)]
        [Tooltip("Player approach speed that upgrades a nearby flock from calm grazing into panic.")]
        private float panicPlayerSpeedThreshold = 2.4f;

        [SerializeField, Range(0.5f, 12f)]
        [Tooltip("Player threat radius used when evaluating fast approach panic.")]
        private float panicPlayerRadius = 3.6f;

        [SerializeField, Range(0.25f, 3f)]
        [Tooltip("Radius around the gameplay camera that repels boids and prevents near-field clipping through the player's view volume.")]
        private float cameraAvoidRadius = 0.95f;

        [SerializeField, Range(0f, 8f)]
        [Tooltip("Strength of the camera-avoidance force applied when a boid enters the player's near clip bubble.")]
        private float cameraAvoidWeight = 4.8f;

        [SerializeField, Range(1, 8)]
        [Tooltip("Hard cap for concurrent leviathan or submarine panic threats cached on the CPU and uploaded to the compute shader.")]
        private int maxMassiveThreatCount = 4;

        [SerializeField, Range(50f, 96f)]
        [Tooltip("Minimum flee radius used when a leviathan-scale object tears through the canopy.")]
        private float massiveThreatPanicRadius = HectonVegetationConstants.BoidMassiveDisplacementPanicRadius;

        [SerializeField, Range(0f, 12f)]
        [Tooltip("Additional flee force weight applied when a leviathan-scale threat is active.")]
        private float massiveThreatWeight = 8.6f;

        [Header("── Vertical Band ──────────────────")]
        [SerializeField, Min(0f)]
        [Tooltip("Water surface level used to clamp the vertical simulation band.")]
        private float waterLevel = 4900f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Minimum depth below the surface for the boid band.")]
        private float minDepthBelowSurface = 0.8f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Maximum depth below the surface for the boid band.")]
        private float maxDepthBelowSurface = 4.5f;

        [Header("── Deep Sea Adaptation ──────────────────")]
        [SerializeField]
        [Tooltip("World-space Y threshold where the flock abandons canopy confinement and rebuilds as abyssal bait balls around biolum sources.")]
        private float deepSeaWorldYThreshold = -1000f;

        [SerializeField, Range(1, 16)]
        [Tooltip("Maximum nearby biolum zones copied into the deep-sea bait-ball anchor set without allocations.")]
        private int deepBiolumAnchorCapacity = 8;

        [SerializeField, Range(10f, 250f)]
        [Tooltip("Maximum search radius used when harvesting nearby biolum anchors for abyssal bait-ball mode.")]
        private float deepBiolumSearchRadius = 140f;

        [SerializeField, Range(0.5f, 12f)]
        [Tooltip("Horizontal radius of the dense bait-ball cluster around each deep biolum source.")]
        private float deepBaitBallRadius = 4.5f;

        [SerializeField, Range(0.25f, 8f)]
        [Tooltip("Vertical half-height used by abyssal bait-ball spawn and render bounds.")]
        private float deepBaitBallHeight = 2.1f;

        [SerializeField, Range(0f, 8f)]
        [Tooltip("Additional anchor-attraction weight applied while deep bait-ball mode is active.")]
        private float deepClusterWeight = 3.8f;

        [SerializeField, Range(0.1f, 10f)]
        [Tooltip("Seconds that abyssal boids stay in headlight panic after a sudden lamp activation while transport is active.")]
        private float deepHeadlightPanicDuration = 3.5f;

        [SerializeField, Range(1f, 6f)]
        [Tooltip("Additional player panic radius multiplier applied while abyssal headlight panic is active.")]
        private float deepHeadlightPanicRadiusScale = 2.8f;

        [SerializeField, Range(-4000f, -1000f)]
        [Tooltip("World-space Y threshold where abyssal technical zones replace calm fish behavior with parasite-drone affinity toward active transport.")]
        private float parasiteDroneWorldYThreshold = -2000f;

        [SerializeField, Range(0f, 12f)]
        [Tooltip("Base attraction weight pulling parasite drones toward an active scooter hull in abyssal technical zones.")]
        private float parasiteAffinityWeight = 4.6f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Normalized hull-stress request applied while parasite drones aggressively latch onto a lit scooter hull.")]
        private float parasiteHullStressIntensity = 0.42f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Additional hull-stress intensity unlocked when scooter lights are active and parasite drones switch into hard latch behavior.")]
        private float parasiteHullStressLightBoost = 0.34f;

        [SerializeField, Range(0.5f, 8f)]
        [Tooltip("Near-hull radius used when parasite drones clamp to the scooter body instead of orbiting at bait-ball distance.")]
        private float parasiteLatchRadius = 1.35f;

        [Header("── Rendering ──────────────────")]
        [SerializeField]
        [Tooltip("Shadow mode used for the indirect draw.")]
        private ShadowCastingMode shadowCastingMode = ShadowCastingMode.Off;

        [SerializeField]
        [Tooltip("True if the indirect draw should render into the layer of this GameObject.")]
        private bool useGameObjectLayer = true;

        [Header("── Diagnostics ──────────────────")]
        [SerializeField]
        [Tooltip("Field revision used to build the current spawn set.")]
        private int _debugFieldRevision;

        [SerializeField]
        [Tooltip("Current render bounds used by DrawMeshInstancedIndirect.")]
        private Bounds _debugRenderBounds;

        [SerializeField]
        [Tooltip("Current drift offset applied to the boid field.")]
        private Vector3 _debugDriftOffset;

        [SerializeField]
        [Tooltip("Current dispatch group count.")]
        private int _debugDispatchGroups;

        [SerializeField]
        [Tooltip("Active pneumatocyst grazing anchor count uploaded to the GPU.")]
        private int _debugGrazingAnchorCount;

        [SerializeField]
        [Tooltip("Measured player speed fed into the panic gate.")]
        private float _debugPlayerSpeed;

        [SerializeField]
        [Tooltip("Current panic-radius multiplier uploaded to the GPU. Transport spikes this to the authored scooter fear radius.")]
        private float _debugPlayerPanicRadiusScale = 1f;

        [SerializeField]
        [Tooltip("True when the boid bounds intersect the current gameplay camera frustum.")]
        private bool _debugVisible;

        [SerializeField]
        [Tooltip("Active leviathan-scale panic threat count uploaded to the compute shader.")]
        private int _debugMassiveThreatCount;

        [SerializeField]
        [Tooltip("True while the flock is running in abyssal bait-ball mode instead of canopy mode.")]
        private bool _debugDeepModeActive;

        [SerializeField]
        [Tooltip("Active abyssal headlight-panic strength uploaded to the compute shader.")]
        private float _debugHeadlightPanic01;

        [SerializeField]
        [Tooltip("True while abyssal technical zones replace calm bait-ball fish behavior with parasite-drone hull affinity.")]
        private bool _debugParasiteModeActive;

        [SerializeField]
        [Tooltip("Current parasite aggression strength uploaded to the compute shader. Lights drive this toward hard hull latch behavior.")]
        private float _debugParasiteAggression01;

        private MaterialPropertyBlock _materialPropertyBlock;
        // COLD ALLOC: Plane[6] - cached frustum plane array reused for no-alloc visibility tests - owner: SargassumMicroFaunaBoids
        private readonly Plane[] _frustumPlanes = new Plane[6];

        private BoidData[] _spawnData;
        private GrazingAnchorData[] _grazingAnchors;
        private MassiveThreatData[] _massiveThreats;
        private HectonBiolumZone[] _deepBiolumZones;
        private float[] _deepBiolumZoneScores;
        private ComputeBuffer _boidsBufferA;
        private ComputeBuffer _boidsBufferB;
        private ComputeBuffer _argsBuffer;
        private ComputeBuffer _grazingAnchorBuffer;
        private ComputeBuffer _massiveThreatBuffer;
        private Bounds _renderBounds;
        private Vector4 _densityWorldRect;
        private int _kernelIndex = -1;
        private uint _threadGroupSizeX = 64;
        private int _dispatchGroupCount = 1;
        private int _frameParity;
        private int _lastFieldRevision = -1;
        private bool _registeredTick;
        private bool _registeredSlowTick;
        private bool _hasSpawnData;
        private Vector3 _fieldCenter;
        private Vector3 _fieldExtents;
        private Vector3 _previousDriftOffset;
        private float _headlightPanicTimer;
        private bool _deepModeActive;
        private bool _lastSpawnModeDeep;
        private float _simulationTime;
        private PlayerTransportCoordinator _playerTransportCoordinator;
        private int _activeGrazingAnchorCount;
        private int _activeMassiveThreatCount;
        private Rigidbody _playerRigidbody;
        private HectonPlayerMovement _playerMovement;
        private PlayerFlashlight _playerFlashlight;
        private WorldZoneDirector _worldZoneDirector;
        private BiomeMatrixDirector _biomeMatrixDirector;
        private bool _flashlightOn;
        private bool _parasiteModeActive;

        /// <summary>
        /// Current active boid count.
        /// </summary>
        public int BoidCount => boidCount;

        private void Awake()
        {
            _materialPropertyBlock ??= new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - indirect boid render properties - owner: SargassumMicroFaunaBoids
            SanitizeSettings();
            ResolveDependencies();
            EnsureBuffers();
            ConfigureIndirectArgs();
            RefreshSpawnData(force: true);
        }

        private void OnEnable()
        {
            _materialPropertyBlock ??= new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - indirect boid render properties - owner: SargassumMicroFaunaBoids
            ResolveDependencies();
            EnsureBuffers();
            ConfigureIndirectArgs();
            RefreshSpawnData(force: true);
            SargassumGlobalDragManager.OnMassiveDisplacement += HandleMassiveDisplacement;
            FlashlightEvents.OnToggled += HandleFlashlightToggled;
            TryRegister();
        }

        private void OnDisable()
        {
            SargassumGlobalDragManager.OnMassiveDisplacement -= HandleMassiveDisplacement;
            FlashlightEvents.OnToggled -= HandleFlashlightToggled;
            _headlightPanicTimer = 0f;
            _debugHeadlightPanic01 = 0f;
            _flashlightOn = false;
            _parasiteModeActive = false;
            _debugParasiteModeActive = false;
            _debugParasiteAggression01 = 0f;
            TryUnregister();
        }

        private void OnDestroy()
        {
            SargassumGlobalDragManager.OnMassiveDisplacement -= HandleMassiveDisplacement;
            FlashlightEvents.OnToggled -= HandleFlashlightToggled;
            TryUnregister();
            ReleaseBuffers();
        }

        /// <summary>
        /// Runs GPU flocking and issues one indirect draw call when the field is valid.
        /// </summary>
        /// <param name="dt">Frame delta supplied by GameTickManager.</param>
        public void Tick(float dt)
        {
            if (!_hasSpawnData || boidCompute == null || boidMaterial == null || boidMesh == null)
                return;

            ResolveDependencies();
            _deepModeActive = IsDeepModeActive();
            _parasiteModeActive = IsParasiteModeActive();
            float deltaTime = Mathf.Max(0f, dt);
            if (_headlightPanicTimer > 0f)
            {
                _headlightPanicTimer -= deltaTime;
                if (_headlightPanicTimer < 0f)
                    _headlightPanicTimer = 0f;
            }

            Vector3 currentDriftOffset = !_deepModeActive && dragManager != null ? dragManager.GlobalDriftOffset : Vector3.zero;
            Vector3 driftDelta = currentDriftOffset - _previousDriftOffset;
            _previousDriftOffset = currentDriftOffset;
            if (driftDelta.sqrMagnitude > 0.000001f)
            {
                _fieldCenter += driftDelta;
                _renderBounds.center += driftDelta;
                _debugRenderBounds = _renderBounds;
            }

            _simulationTime += deltaTime;
            UpdateMassiveThreats(dt);
            BindSimulationUniforms(dt, currentDriftOffset, driftDelta);
            boidCompute.Dispatch(_kernelIndex, _dispatchGroupCount, 1, 1);

            ApplyParasiteHullStress();

            _frameParity ^= 1;
            _debugVisible = CheckFrustumVisibility();
            if (_debugVisible)
                RenderCurrentBuffer();

            _debugDriftOffset = currentDriftOffset;
            _debugDeepModeActive = _deepModeActive;
            _debugHeadlightPanic01 = ResolveHeadlightPanic01();
            _debugParasiteModeActive = _parasiteModeActive;
        }

        /// <summary>
        /// Rebuilds the spawn set whenever the sargassum field topology changes.
        /// </summary>
        public void SlowTick()
        {
            ResolveDependencies();
            RefreshSpawnData(force: false);
        }

        private void ResolveDependencies()
        {
            if (biolumManager == null)
                biolumManager = HectonBiolumManager.Instance;

            if (dragManager == null)
                dragManager = SargassumGlobalDragManager.Instance;

            if (cutManager == null)
                cutManager = SargassumCutManager.Instance;

            if (playerTransform == null)
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);

            if (_playerRigidbody == null && playerTransform != null)
                _playerRigidbody = playerTransform.GetComponent<Rigidbody>();

            if (_playerMovement == null && playerTransform != null)
                playerTransform.TryGetComponent(out _playerMovement);

            if (_playerTransportCoordinator == null && playerTransform != null)
                playerTransform.TryGetComponent(out _playerTransportCoordinator);

            if (_playerFlashlight == null && playerTransform != null)
                _playerFlashlight = playerTransform.GetComponentInChildren<PlayerFlashlight>(true);

            if (_worldZoneDirector == null)
                _worldZoneDirector = WorldZoneDirector.ActiveRuntimeInstance;

            if (_biomeMatrixDirector == null)
                _biomeMatrixDirector = BiomeMatrixDirector.ActiveRuntimeInstance;

            if (viewCamera == null && playerTransform != null)
                viewCamera = playerTransform.GetComponentInChildren<Camera>(true);

            if (_playerFlashlight != null)
                _flashlightOn = _playerFlashlight.IsOn;
        }

        private void SanitizeSettings()
        {
            boidCount = Mathf.Clamp(boidCount, 128, 2048);
            maxSpawnAttempts = Mathf.Clamp(maxSpawnAttempts, 4, 32);
            densityThreshold = Mathf.Clamp01(densityThreshold);
            windowThreshold = Mathf.Clamp(windowThreshold, 0f, 0.75f);
            cruiseSpeed = Mathf.Max(0.1f, cruiseSpeed);
            maxSpeed = Mathf.Max(cruiseSpeed, maxSpeed);
            panicSpeedBoost = Mathf.Max(0f, panicSpeedBoost);
            perceptionRadius = Mathf.Max(0.25f, perceptionRadius);
            separationRadius = Mathf.Clamp(separationRadius, 0.1f, perceptionRadius);
            gradientWorldStep = Mathf.Max(0.05f, gradientWorldStep);
            waterLevel = Mathf.Max(0f, waterLevel);
            minDepthBelowSurface = Mathf.Max(0.1f, minDepthBelowSurface);
            maxDepthBelowSurface = Mathf.Max(minDepthBelowSurface + 0.1f, maxDepthBelowSurface);
            panicThreshold = Mathf.Clamp01(panicThreshold);
            panicDecay = Mathf.Max(0.1f, panicDecay);
            grazingAnchorCount = Mathf.Clamp(grazingAnchorCount, 4, 96);
            grazingRadius = Mathf.Clamp(grazingRadius, 0.25f, 6f);
            grazingWeight = Mathf.Clamp(grazingWeight, 0f, 4f);
            canopyAffinityWeight = Mathf.Clamp(canopyAffinityWeight, 0f, 4f);
            grazingDensityThreshold = Mathf.Clamp01(grazingDensityThreshold);
            grazingRestSpeedScale = Mathf.Clamp(grazingRestSpeedScale, 0.05f, 0.6f);
            grazingRestHoldThreshold = Mathf.Clamp01(grazingRestHoldThreshold);
            panicPlayerSpeedThreshold = Mathf.Clamp(panicPlayerSpeedThreshold, 0.5f, 8f);
            panicPlayerRadius = Mathf.Clamp(panicPlayerRadius, 0.5f, 12f);
            cameraAvoidRadius = Mathf.Clamp(cameraAvoidRadius, 0.25f, 3f);
            cameraAvoidWeight = Mathf.Clamp(cameraAvoidWeight, 0f, 8f);
            maxMassiveThreatCount = Mathf.Clamp(maxMassiveThreatCount, 1, 8);
            massiveThreatPanicRadius = Mathf.Clamp(massiveThreatPanicRadius, 50f, 96f);
            massiveThreatWeight = Mathf.Clamp(massiveThreatWeight, 0f, 12f);
            deepBiolumAnchorCapacity = Mathf.Clamp(deepBiolumAnchorCapacity, 1, 16);
            deepBiolumSearchRadius = Mathf.Clamp(deepBiolumSearchRadius, 10f, 250f);
            deepBaitBallRadius = Mathf.Clamp(deepBaitBallRadius, 0.5f, 12f);
            deepBaitBallHeight = Mathf.Clamp(deepBaitBallHeight, 0.25f, 8f);
            deepClusterWeight = Mathf.Clamp(deepClusterWeight, 0f, 8f);
            deepHeadlightPanicDuration = Mathf.Clamp(deepHeadlightPanicDuration, 0.1f, 10f);
            deepHeadlightPanicRadiusScale = Mathf.Clamp(deepHeadlightPanicRadiusScale, 1f, 6f);
            parasiteDroneWorldYThreshold = Mathf.Clamp(parasiteDroneWorldYThreshold, -4000f, -1000f);
            parasiteAffinityWeight = Mathf.Clamp(parasiteAffinityWeight, 0f, 12f);
            parasiteHullStressIntensity = Mathf.Clamp01(parasiteHullStressIntensity);
            parasiteHullStressLightBoost = Mathf.Clamp01(parasiteHullStressLightBoost);
            parasiteLatchRadius = Mathf.Clamp(parasiteLatchRadius, 0.5f, 8f);
        }

        private void EnsureBuffers()
        {
            if (_spawnData == null || _spawnData.Length != boidCount)
            {
                // COLD ALLOC: BoidData[boidCount] - CPU staging array for deterministic spawn uploads - owner: SargassumMicroFaunaBoids
                _spawnData = new BoidData[boidCount];
            }

            if (_grazingAnchors == null || _grazingAnchors.Length != grazingAnchorCount)
            {
                // COLD ALLOC: GrazingAnchorData[grazingAnchorCount] - CPU staging array for deterministic grazing anchors - owner: SargassumMicroFaunaBoids
                _grazingAnchors = new GrazingAnchorData[grazingAnchorCount];
            }

            if (_massiveThreats == null || _massiveThreats.Length != maxMassiveThreatCount)
            {
                // COLD ALLOC: MassiveThreatData[maxMassiveThreatCount] - CPU staging array for leviathan panic threats - owner: SargassumMicroFaunaBoids
                _massiveThreats = new MassiveThreatData[maxMassiveThreatCount];
                _activeMassiveThreatCount = 0;
                _debugMassiveThreatCount = 0;
            }

            if (_deepBiolumZones == null || _deepBiolumZones.Length != deepBiolumAnchorCapacity)
            {
                // COLD ALLOC: HectonBiolumZone[deepBiolumAnchorCapacity] - deep-sea biolum anchor cache for bait-ball rebuilds - owner: SargassumMicroFaunaBoids
                _deepBiolumZones = new HectonBiolumZone[deepBiolumAnchorCapacity];
            }

            if (_deepBiolumZoneScores == null || _deepBiolumZoneScores.Length != deepBiolumAnchorCapacity)
            {
                // COLD ALLOC: float[deepBiolumAnchorCapacity] - deep-sea biolum anchor strength cache paired with zone refs - owner: SargassumMicroFaunaBoids
                _deepBiolumZoneScores = new float[deepBiolumAnchorCapacity];
            }

            EnsureBuffer(ref _boidsBufferA, boidCount, BoidStride, ComputeBufferType.Structured);
            EnsureBuffer(ref _boidsBufferB, boidCount, BoidStride, ComputeBufferType.Structured);
            EnsureBuffer(ref _argsBuffer, 1, sizeof(uint) * IndirectArgsCount, ComputeBufferType.IndirectArguments);
            EnsureBuffer(ref _grazingAnchorBuffer, grazingAnchorCount, GrazingAnchorStride, ComputeBufferType.Structured);
            EnsureBuffer(ref _massiveThreatBuffer, maxMassiveThreatCount, MassiveThreatStride, ComputeBufferType.Structured);

            if (boidCompute == null)
                return;

            if (_kernelIndex < 0)
            {
                _kernelIndex = boidCompute.FindKernel("CSMain");
                boidCompute.GetKernelThreadGroupSizes(_kernelIndex, out _threadGroupSizeX, out _, out _);
            }

            _dispatchGroupCount = Mathf.Max(1, Mathf.CeilToInt(boidCount / (float)_threadGroupSizeX));
            _debugDispatchGroups = _dispatchGroupCount;
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

            // COLD ALLOC: ComputeBuffer[count] - persistent GPU boid data or indirect args buffer - owner: SargassumMicroFaunaBoids
            buffer = new ComputeBuffer(count, stride, type);
        }

        private void ConfigureIndirectArgs()
        {
            if (_argsBuffer == null || boidMesh == null)
                return;

            uint[] args =
            {
                boidMesh != null ? boidMesh.GetIndexCount(0) : 0u,
                (uint)boidCount,
                boidMesh != null ? boidMesh.GetIndexStart(0) : 0u,
                boidMesh != null ? boidMesh.GetBaseVertex(0) : 0u,
                0u
            };
            _argsBuffer.SetData(args);
        }

        private void RefreshSpawnData(bool force)
        {
            if (boidCompute == null || boidMaterial == null || boidMesh == null)
            {
                _hasSpawnData = false;
                return;
            }

            _deepModeActive = IsDeepModeActive();
            _debugDeepModeActive = _deepModeActive;

            if (_deepModeActive)
            {
                if (!force && _lastSpawnModeDeep)
                    return;

                if (!BuildDeepSpawnData())
                {
                    _hasSpawnData = false;
                    return;
                }

                _boidsBufferA.SetData(_spawnData);
                _boidsBufferB.SetData(_spawnData);
                _grazingAnchorBuffer.SetData(_grazingAnchors);
                _frameParity = 0;
                _previousDriftOffset = Vector3.zero;
                _lastFieldRevision = -1;
                _debugFieldRevision = -1;
                _hasSpawnData = true;
                _lastSpawnModeDeep = true;
                return;
            }

            _lastSpawnModeDeep = false;
            if (dragManager == null || !dragManager.TryGetDensityFieldTexture(out _, out Vector4 densityWorldRect))
            {
                _hasSpawnData = false;
                return;
            }

            if (!force && dragManager.FieldRevision == _lastFieldRevision)
                return;

            _densityWorldRect = densityWorldRect;
            BuildSpawnSet(densityWorldRect, dragManager.GlobalDriftOffset);
            _boidsBufferA.SetData(_spawnData);
            _boidsBufferB.SetData(_spawnData);
            BuildGrazingAnchors(densityWorldRect, dragManager.GlobalDriftOffset);
            _grazingAnchorBuffer.SetData(_grazingAnchors);
            _frameParity = 0;
            _previousDriftOffset = dragManager.GlobalDriftOffset;
            _lastFieldRevision = dragManager.FieldRevision;
            _debugFieldRevision = _lastFieldRevision;
            _hasSpawnData = true;
        }

        private bool BuildDeepSpawnData()
        {
            if (biolumManager == null || playerTransform == null || _deepBiolumZones == null || _deepBiolumZoneScores == null)
                return false;

            System.Array.Clear(_deepBiolumZones, 0, _deepBiolumZones.Length);
            System.Array.Clear(_deepBiolumZoneScores, 0, _deepBiolumZoneScores.Length);
            int zoneCount = biolumManager.CopyNearbyZonesNonAlloc(
                playerTransform.position,
                deepBiolumSearchRadius,
                _deepBiolumZones,
                _deepBiolumZoneScores);
            if (zoneCount <= 0)
                return false;

            _densityWorldRect = Vector4.zero;
            BuildDeepSpawnSet(zoneCount);
            BuildDeepGrazingAnchors(zoneCount);
            return true;
        }

        private bool IsDeepModeActive()
        {
            return playerTransform != null && playerTransform.position.y <= deepSeaWorldYThreshold;
        }

        private bool IsParasiteModeActive()
        {
            if (playerTransform == null || playerTransform.position.y > parasiteDroneWorldYThreshold)
                return false;

            if (_worldZoneDirector == null)
                _worldZoneDirector = WorldZoneDirector.ActiveRuntimeInstance;

            if (_biomeMatrixDirector == null)
                _biomeMatrixDirector = BiomeMatrixDirector.ActiveRuntimeInstance;

            if (_worldZoneDirector == null || _biomeMatrixDirector == null || _biomeMatrixDirector.CurrentDepthMeters < 2000f)
                return false;

            WorldZoneAnchor primaryZone = _worldZoneDirector.CurrentZone;
            WorldZoneAnchor secondaryZone = _worldZoneDirector.SecondaryZone;
            return IsSyntheticAbyssZone(primaryZone) || IsSyntheticAbyssZone(secondaryZone);
        }

        private static bool IsSyntheticAbyssZone(WorldZoneAnchor zone)
        {
            if (zone == null)
                return false;

            return zone.Kind == WorldZoneAnchor.ZoneKind.Service ||
                   zone.Kind == WorldZoneAnchor.ZoneKind.Power ||
                   zone.Kind == WorldZoneAnchor.ZoneKind.Construction;
        }

        private float ResolveParasiteAggression01()
        {
            if (!_parasiteModeActive || _playerTransportCoordinator == null || !_playerTransportCoordinator.IsTransportActive())
                return 0f;

            return Mathf.Clamp01(Mathf.Max(_flashlightOn ? 1f : 0f, ResolveHeadlightPanic01()));
        }

        private void BuildDeepSpawnSet(int zoneCount)
        {
            HectonBiolumZone primaryZone = _deepBiolumZones[0];
            Vector3 primaryPosition = primaryZone != null ? primaryZone.GetZonePosition() : Vector3.zero;
            Vector3 boundsMin = primaryPosition;
            Vector3 boundsMax = primaryPosition;
            Vector3 weightedCenter = Vector3.zero;
            float weightSum = 0f;

            for (int i = 0; i < zoneCount; i++)
            {
                HectonBiolumZone zone = _deepBiolumZones[i];
                if (zone == null)
                    continue;

                float score = Mathf.Max(0.0001f, _deepBiolumZoneScores[i]);
                Vector3 zonePosition = zone.GetZonePosition();
                weightedCenter += zonePosition * score;
                weightSum += score;

                Vector3 extents = new Vector3(deepBaitBallRadius, deepBaitBallHeight, deepBaitBallRadius);
                boundsMin = Vector3.Min(boundsMin, zonePosition - extents);
                boundsMax = Vector3.Max(boundsMax, zonePosition + extents);
            }

            _fieldCenter = weightSum > 0.0001f ? weightedCenter / weightSum : primaryPosition;
            _fieldExtents = Vector3.Max((boundsMax - boundsMin) * 0.5f, new Vector3(2f, 1f, 2f));
            _renderBounds = new Bounds(_fieldCenter, Vector3.Max(boundsMax - boundsMin, new Vector3(4f, 2f, 4f)));
            _debugRenderBounds = _renderBounds;

            for (int i = 0; i < boidCount; i++)
            {
                int zoneIndex = i % zoneCount;
                HectonBiolumZone zone = _deepBiolumZones[zoneIndex];
                Vector3 anchorPosition = zone != null ? zone.GetZonePosition() : _fieldCenter;
                float radiusT = Mathf.Sqrt(HashToFloat01((uint)i, 0u, 0xA2F98A1Du));
                float angle = HashToFloat01((uint)i, 0u, 0x3C6EF372u) * Mathf.PI * 2f;
                float verticalT = HashToFloat01((uint)i, 0u, 0x1BF5C7D5u) * 2f - 1f;
                Vector3 spawnPosition = anchorPosition;
                spawnPosition.x += Mathf.Cos(angle) * deepBaitBallRadius * radiusT;
                spawnPosition.z += Mathf.Sin(angle) * deepBaitBallRadius * radiusT;
                spawnPosition.y += verticalT * deepBaitBallHeight;

                Vector3 toCenter = anchorPosition - spawnPosition;
                if (toCenter.sqrMagnitude <= 0.0001f)
                    toCenter = BuildInitialVelocity(i);
                else
                    toCenter.Normalize();

                _spawnData[i] = new BoidData
                {
                    Position = spawnPosition,
                    Velocity = toCenter * cruiseSpeed,
                    Seed = HashToFloat01((uint)i, 0u, 0x94D049BBu),
                    Panic = 0f
                };
            }
        }

        private void BuildDeepGrazingAnchors(int zoneCount)
        {
            _activeGrazingAnchorCount = 0;
            Vector3 fallbackPosition = _fieldCenter;
            for (int i = 0; i < zoneCount && _activeGrazingAnchorCount < grazingAnchorCount; i++)
            {
                HectonBiolumZone zone = _deepBiolumZones[i];
                if (zone == null)
                    continue;

                _grazingAnchors[_activeGrazingAnchorCount] = new GrazingAnchorData
                {
                    Position = zone.GetZonePosition(),
                    Radius = deepBaitBallRadius,
                    Strength = Mathf.Lerp(1.2f, 1.8f, Mathf.Clamp01(_deepBiolumZoneScores[i])),
                    Phase = HashToFloat01((uint)i, 0u, 0xA4093822u),
                    Padding = Vector2.zero
                };
                _activeGrazingAnchorCount++;
            }

            for (int i = _activeGrazingAnchorCount; i < grazingAnchorCount; i++)
            {
                _grazingAnchors[i] = new GrazingAnchorData
                {
                    Position = fallbackPosition,
                    Radius = deepBaitBallRadius,
                    Strength = 0f,
                    Phase = 0f,
                    Padding = Vector2.zero
                };
            }

            _debugGrazingAnchorCount = _activeGrazingAnchorCount;
        }

        private void BuildSpawnSet(Vector4 densityWorldRect, Vector3 driftOffset)
        {
            float sizeX = 1f / Mathf.Max(densityWorldRect.z, 0.0001f);
            float sizeZ = 1f / Mathf.Max(densityWorldRect.w, 0.0001f);
            float minX = densityWorldRect.x;
            float minZ = densityWorldRect.y;
            float minY = waterLevel - maxDepthBelowSurface;
            float maxY = waterLevel - minDepthBelowSurface;
            Vector3 fallbackCenter = new Vector3(minX + sizeX * 0.5f + driftOffset.x, (minY + maxY) * 0.5f, minZ + sizeZ * 0.5f + driftOffset.z);

            _fieldCenter = fallbackCenter;
            _fieldExtents = new Vector3(sizeX * 0.5f, Mathf.Max(1f, maxDepthBelowSurface), sizeZ * 0.5f);
            _renderBounds = new Bounds(_fieldCenter, new Vector3(sizeX, Mathf.Max(2f, maxDepthBelowSurface + 2f), sizeZ));
            _debugRenderBounds = _renderBounds;

            for (int i = 0; i < boidCount; i++)
            {
                Vector3 spawnPosition = fallbackCenter;
                SargassumGlobalDragManager.SargassumFieldSample fieldSample = default;
                bool found = false;

                for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
                {
                    float u = HashToFloat01((uint)i, (uint)attempt, 0xA2F98A1Du);
                    float v = HashToFloat01((uint)i, (uint)attempt, 0x3C6EF372u);
                    float w = HashToFloat01((uint)i, (uint)attempt, 0x1BF5C7D5u);

                    spawnPosition.x = minX + u * sizeX + driftOffset.x;
                    spawnPosition.y = Mathf.Lerp(minY, maxY, w);
                    spawnPosition.z = minZ + v * sizeZ + driftOffset.z;

                    if (!dragManager.SampleDetailedInfluence(spawnPosition, 0.45f, cruiseSpeed, out fieldSample))
                        continue;

                    if (fieldSample.Density01 < densityThreshold || fieldSample.Window01 > windowThreshold)
                        continue;

                    found = true;
                    break;
                }

                if (!found)
                {
                    spawnPosition = fallbackCenter;
                }

                Vector3 velocity = BuildInitialVelocity(i);
                _spawnData[i] = new BoidData
                {
                    Position = spawnPosition,
                    Velocity = velocity,
                    Seed = HashToFloat01((uint)i, 0u, 0x94D049BBu),
                    Panic = 0f
                };
            }
        }

        private void BuildGrazingAnchors(Vector4 densityWorldRect, Vector3 driftOffset)
        {
            float sizeX = 1f / Mathf.Max(densityWorldRect.z, 0.0001f);
            float sizeZ = 1f / Mathf.Max(densityWorldRect.w, 0.0001f);
            float minX = densityWorldRect.x;
            float minZ = densityWorldRect.y;
            float minY = waterLevel - maxDepthBelowSurface;
            float maxY = waterLevel - minDepthBelowSurface;
            Vector3 fallbackPosition = new Vector3(minX + sizeX * 0.5f + driftOffset.x, Mathf.Lerp(minY, maxY, 0.32f), minZ + sizeZ * 0.5f + driftOffset.z);

            _activeGrazingAnchorCount = 0;
            for (int i = 0; i < grazingAnchorCount; i++)
            {
                Vector3 anchorPosition = fallbackPosition;
                SargassumGlobalDragManager.SargassumFieldSample fieldSample = default;
                bool found = false;

                for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
                {
                    float u = HashToFloat01((uint)i, (uint)attempt, 0x1F123BB5u);
                    float v = HashToFloat01((uint)i, (uint)attempt, 0x6B8B4567u);
                    float w = HashToFloat01((uint)i, (uint)attempt, 0x327B23C6u);

                    anchorPosition.x = minX + u * sizeX + driftOffset.x;
                    anchorPosition.y = Mathf.Lerp(minY, maxY, Mathf.Lerp(0.18f, 0.58f, w));
                    anchorPosition.z = minZ + v * sizeZ + driftOffset.z;

                    if (!dragManager.SampleDetailedInfluence(anchorPosition, grazingRadius * 0.35f, cruiseSpeed, out fieldSample))
                        continue;

                    if (fieldSample.Density01 < grazingDensityThreshold || fieldSample.Window01 > windowThreshold)
                        continue;

                    anchorPosition = fieldSample.AnchorWS;
                    found = true;
                    break;
                }

                if (!found)
                    continue;

                _grazingAnchors[_activeGrazingAnchorCount] = new GrazingAnchorData
                {
                    Position = anchorPosition,
                    Radius = grazingRadius,
                    Strength = Mathf.Lerp(0.8f, 1.25f, fieldSample.Density01),
                    Phase = HashToFloat01((uint)i, 0u, 0xA4093822u),
                    Padding = Vector2.zero
                };
                _activeGrazingAnchorCount++;
            }

            for (int i = _activeGrazingAnchorCount; i < grazingAnchorCount; i++)
            {
                _grazingAnchors[i] = new GrazingAnchorData
                {
                    Position = fallbackPosition,
                    Radius = grazingRadius,
                    Strength = 0f,
                    Phase = 0f,
                    Padding = Vector2.zero
                };
            }

            _debugGrazingAnchorCount = _activeGrazingAnchorCount;
        }

        private Vector3 BuildInitialVelocity(int index)
        {
            float angle = HashToFloat01((uint)index, 0u, 0xDEADBEEFu) * Mathf.PI * 2f;
            float vertical = Mathf.Lerp(-0.15f, 0.15f, HashToFloat01((uint)index, 0u, 0x165667B1u));
            Vector3 direction = new Vector3(Mathf.Cos(angle), vertical, Mathf.Sin(angle));
            if (direction.sqrMagnitude <= 0.0001f)
                direction = Vector3.forward;

            direction.Normalize();
            return direction * cruiseSpeed;
        }

        private void BindSimulationUniforms(float dt, Vector3 driftOffset, Vector3 driftDelta)
        {
            ComputeBuffer readBuffer = _frameParity == 0 ? _boidsBufferA : _boidsBufferB;
            ComputeBuffer writeBuffer = _frameParity == 0 ? _boidsBufferB : _boidsBufferA;

            boidCompute.SetBuffer(_kernelIndex, _BoidsBufferReadId, readBuffer);
            boidCompute.SetBuffer(_kernelIndex, _BoidsBufferWriteId, writeBuffer);
            boidCompute.SetInt(_BoidCountId, boidCount);
            boidCompute.SetFloat(_DeltaTimeId, dt);
            boidCompute.SetVector(_FieldCenterId, _fieldCenter);
            boidCompute.SetVector(_FieldExtentsId, _fieldExtents);
            boidCompute.SetFloat(_WaterLevelId, waterLevel);
            boidCompute.SetFloat(_MinDepthId, minDepthBelowSurface);
            boidCompute.SetFloat(_MaxDepthId, maxDepthBelowSurface);
            boidCompute.SetFloat(_CruiseSpeedId, cruiseSpeed);
            boidCompute.SetFloat(_MaxSpeedId, maxSpeed);
            boidCompute.SetFloat(_PanicSpeedBoostId, panicSpeedBoost);
            boidCompute.SetFloat(_PerceptionRadiusId, perceptionRadius);
            boidCompute.SetFloat(_SeparationRadiusId, separationRadius);
            boidCompute.SetFloat(_SeparationWeightId, separationWeight);
            boidCompute.SetFloat(_AlignmentWeightId, alignmentWeight);
            boidCompute.SetFloat(_CohesionWeightId, cohesionWeight);
            boidCompute.SetFloat(_ContainmentWeightId, containmentWeight);
            boidCompute.SetFloat(_PanicWeightId, panicWeight);
            boidCompute.SetFloat(_NoiseWeightId, noiseWeight);
            boidCompute.SetFloat(_DensityThresholdId, densityThreshold);
            boidCompute.SetFloat(_WindowThresholdId, windowThreshold);
            boidCompute.SetFloat(_GradientWorldStepId, gradientWorldStep);
            boidCompute.SetFloat(_PanicThresholdId, panicThreshold);
            boidCompute.SetFloat(_PanicDecayId, panicDecay);
            boidCompute.SetInt(_GrazingAnchorCountId, _activeGrazingAnchorCount);
            boidCompute.SetFloat(_GrazingWeightId, grazingWeight);
            boidCompute.SetFloat(_GrazingRadiusId, grazingRadius);
            boidCompute.SetFloat(_GrazingRestSpeedScaleId, grazingRestSpeedScale);
            boidCompute.SetFloat(_GrazingRestHoldThresholdId, grazingRestHoldThreshold);
            boidCompute.SetFloat(_CanopyAffinityWeightId, canopyAffinityWeight);
            boidCompute.SetVector(_DensityWorldRectId, _densityWorldRect);
            boidCompute.SetVector(_GlobalDriftOffsetId, driftOffset);
            boidCompute.SetVector(_GlobalDriftDeltaId, driftDelta);
            boidCompute.SetBuffer(_kernelIndex, _GrazingAnchorsId, _grazingAnchorBuffer);
            boidCompute.SetFloat(_SimulationTimeId, _simulationTime);
            boidCompute.SetFloat(_DeepModeId, _deepModeActive ? 1f : 0f);
            boidCompute.SetFloat(_DeepClusterWeightId, _deepModeActive ? deepClusterWeight : 0f);

            Vector3 playerPosition = playerTransform != null ? playerTransform.position : _fieldCenter;
            Vector3 playerVelocity = _playerRigidbody != null ? _playerRigidbody.linearVelocity : Vector3.zero;
            float playerSpeed = playerVelocity.magnitude;
            float headlightPanic01 = ResolveHeadlightPanic01();
            float parasiteAggression01 = ResolveParasiteAggression01();
            float panicPlayerRadiusScale =
                _playerTransportCoordinator != null && _playerTransportCoordinator.IsTransportActive()
                    ? HectonVegetationConstants.BoidScooterPanicRadiusMultiplier
                    : 1f;
            if (headlightPanic01 > 0f)
                panicPlayerRadiusScale = Mathf.Max(panicPlayerRadiusScale, Mathf.Lerp(1f, deepHeadlightPanicRadiusScale, headlightPanic01));
            boidCompute.SetVector(_PlayerPositionId, playerPosition);
            boidCompute.SetVector(_PlayerVelocityId, playerVelocity);
            boidCompute.SetFloat(_PlayerSpeedId, playerSpeed);
            boidCompute.SetFloat(_PanicPlayerSpeedThresholdId, panicPlayerSpeedThreshold);
            boidCompute.SetFloat(_PanicPlayerRadiusId, panicPlayerRadius);
            boidCompute.SetFloat(_PanicPlayerRadiusScaleId, panicPlayerRadiusScale);
            boidCompute.SetFloat(_ParasiteModeId, _parasiteModeActive ? 1f : 0f);
            boidCompute.SetFloat(_ParasiteAffinityWeightId, _parasiteModeActive ? parasiteAffinityWeight : 0f);
            boidCompute.SetFloat(_ParasiteAggressionId, parasiteAggression01);
            boidCompute.SetFloat(_ParasiteLatchRadiusId, parasiteLatchRadius);
            boidCompute.SetFloat(_HeadlightPanicId, headlightPanic01);
            Vector3 cameraAvoidPosition = viewCamera != null ? viewCamera.transform.position : playerPosition;
            boidCompute.SetVector(_CameraAvoidPositionId, cameraAvoidPosition);
            boidCompute.SetFloat(_CameraAvoidRadiusId, cameraAvoidRadius);
            boidCompute.SetFloat(_CameraAvoidWeightId, cameraAvoidWeight);
            _debugPlayerSpeed = playerSpeed;
            _debugPlayerPanicRadiusScale = panicPlayerRadiusScale;
            _debugParasiteAggression01 = parasiteAggression01;
            boidCompute.SetInt(_MassiveThreatCountId, _activeMassiveThreatCount);
            boidCompute.SetFloat(_MassiveThreatWeightId, massiveThreatWeight);
            boidCompute.SetBuffer(_kernelIndex, _MassiveThreatsId, _massiveThreatBuffer);
            _debugMassiveThreatCount = _activeMassiveThreatCount;

            Texture densityTexture = !_deepModeActive && dragManager != null ? dragManager.DensityFieldTexture : Texture2D.blackTexture;
            boidCompute.SetTexture(_kernelIndex, _DensityTexId, densityTexture);

            if (!_deepModeActive && cutManager != null && cutManager.TryGetCutMask(out RenderTexture cutMaskTexture, out Vector4 cutMaskWorldRect))
            {
                boidCompute.SetTexture(_kernelIndex, _CutMaskTexId, cutMaskTexture);
                boidCompute.SetVector(_CutMaskWorldRectId, cutMaskWorldRect);
                boidCompute.SetFloat(_CutMaskActiveId, 1f);
            }
            else
            {
                boidCompute.SetTexture(_kernelIndex, _CutMaskTexId, Texture2D.blackTexture);
                boidCompute.SetVector(_CutMaskWorldRectId, Vector4.zero);
                boidCompute.SetFloat(_CutMaskActiveId, 0f);
            }
        }

        private void HandleMassiveDisplacement(SargassumGlobalDragManager.MassiveDisplacementSignal signal)
        {
            if (_massiveThreats == null || _massiveThreats.Length == 0)
                return;

            float panicRadius = Mathf.Max(massiveThreatPanicRadius, Mathf.Max(signal.ExtremePanicRadiusWS, signal.RadiusWS * 3f));
            int targetIndex = -1;
            float weakestRemaining = float.MaxValue;

            for (int i = 0; i < _massiveThreats.Length; i++)
            {
                MassiveThreatData threat = _massiveThreats[i];
                if (threat.RemainingDuration <= 0f)
                {
                    targetIndex = i;
                    break;
                }

                float planarDistanceSq = (new Vector2(threat.Position.x, threat.Position.z) - new Vector2(signal.PositionWS.x, signal.PositionWS.z)).sqrMagnitude;
                float mergeDistance = Mathf.Max(threat.PanicRadius, panicRadius) * 0.4f;
                if (planarDistanceSq <= mergeDistance * mergeDistance)
                {
                    targetIndex = i;
                    break;
                }

                if (threat.RemainingDuration < weakestRemaining)
                {
                    weakestRemaining = threat.RemainingDuration;
                    targetIndex = i;
                }
            }

            if (targetIndex < 0)
                targetIndex = 0;

            _massiveThreats[targetIndex] = new MassiveThreatData
            {
                Position = signal.PositionWS,
                InnerRadius = Mathf.Max(0.5f, signal.RadiusWS),
                PanicRadius = panicRadius,
                Strength = 1f,
                RemainingDuration = Mathf.Max(0.25f, signal.Duration),
                Padding = Vector3.zero
            };

            RecalculateMassiveThreatCount();
            if (_massiveThreatBuffer != null)
                _massiveThreatBuffer.SetData(_massiveThreats);
        }

        private void HandleFlashlightToggled(bool isOn)
        {
            _flashlightOn = isOn;
            if (!isOn || !IsDeepModeActive() || _playerTransportCoordinator == null || !_playerTransportCoordinator.IsTransportActive())
                return;

            _headlightPanicTimer = deepHeadlightPanicDuration;
            _debugHeadlightPanic01 = 1f;
        }

        private float ResolveHeadlightPanic01()
        {
            if (!_deepModeActive || deepHeadlightPanicDuration <= 0.0001f)
                return 0f;

            return Mathf.Clamp01(_headlightPanicTimer / deepHeadlightPanicDuration);
        }

        private void ApplyParasiteHullStress()
        {
            if (_playerMovement == null || !_parasiteModeActive)
                return;

            float aggression01 = ResolveParasiteAggression01();
            if (aggression01 <= 0f)
                return;

            float requestedStress = Mathf.Clamp01(Mathf.Lerp(parasiteHullStressIntensity, parasiteHullStressIntensity + parasiteHullStressLightBoost, aggression01));
            if (requestedStress <= 0.0001f)
                return;

            _playerMovement.RequestExternalHullStress(requestedStress);
        }

        private void UpdateMassiveThreats(float dt)
        {
            if (_massiveThreats == null || _massiveThreatBuffer == null)
                return;

            float deltaTime = Mathf.Max(0f, dt);
            bool changed = false;
            for (int i = 0; i < _massiveThreats.Length; i++)
            {
                if (_massiveThreats[i].RemainingDuration <= 0f)
                    continue;

                _massiveThreats[i].RemainingDuration = Mathf.Max(0f, _massiveThreats[i].RemainingDuration - deltaTime);
                changed = true;
            }

            if (!changed)
                return;

            RecalculateMassiveThreatCount();
            _massiveThreatBuffer.SetData(_massiveThreats);
        }

        private void RecalculateMassiveThreatCount()
        {
            _activeMassiveThreatCount = 0;
            if (_massiveThreats == null)
            {
                _debugMassiveThreatCount = 0;
                return;
            }

            for (int i = 0; i < _massiveThreats.Length; i++)
            {
                if (_massiveThreats[i].RemainingDuration > 0f)
                    _activeMassiveThreatCount++;
            }

            _debugMassiveThreatCount = _activeMassiveThreatCount;
        }

        private void RenderCurrentBuffer()
        {
            ComputeBuffer currentBuffer = _frameParity == 0 ? _boidsBufferA : _boidsBufferB;
            _materialPropertyBlock.Clear();
            _materialPropertyBlock.SetBuffer(_BoidsBufferId, currentBuffer);
            _materialPropertyBlock.SetFloat(_ParasiteModeId, _parasiteModeActive ? 1f : 0f);
            _materialPropertyBlock.SetFloat(_ParasiteAggressionId, _debugParasiteAggression01);

            int targetLayer = useGameObjectLayer ? gameObject.layer : 0;
            Graphics.DrawMeshInstancedIndirect(
                boidMesh,
                0,
                boidMaterial,
                _renderBounds,
                _argsBuffer,
                0,
                _materialPropertyBlock,
                shadowCastingMode,
                false,
                targetLayer,
                null,
                LightProbeUsage.Off,
                null);
        }

        private bool CheckFrustumVisibility()
        {
            if (viewCamera == null)
            {
                ResolveDependencies();
                if (viewCamera == null)
                    return true;
            }

            GeometryUtility.CalculateFrustumPlanes(viewCamera, _frustumPlanes);
            return GeometryUtility.TestPlanesAABB(_frustumPlanes, _renderBounds);
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

        private void ReleaseBuffers()
        {
            ReleaseBuffer(ref _boidsBufferA);
            ReleaseBuffer(ref _boidsBufferB);
            ReleaseBuffer(ref _argsBuffer);
            ReleaseBuffer(ref _grazingAnchorBuffer);
            ReleaseBuffer(ref _massiveThreatBuffer);
        }

        private static void ReleaseBuffer(ref ComputeBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private static float HashToFloat01(uint index, uint iteration, uint salt)
        {
            uint value = index * 374761393u + iteration * 668265263u + salt + HashSeed;
            value = (value ^ (value >> 13)) * 1274126177u;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777215f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            SanitizeSettings();
            _debugDispatchGroups = _dispatchGroupCount;
        }
#endif
    }
}

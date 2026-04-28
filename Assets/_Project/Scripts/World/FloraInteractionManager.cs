using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Physics;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.World
{
    /// <summary>
    /// Publishes global vegetation interaction and environment shader inputs.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-105)]
    public sealed class FloraInteractionManager : MonoBehaviour, ITickable, IOriginShiftListener
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct FloraInteractionPointGpuData
        {
            public Vector4 PositionRadius;
            public Vector4 VelocitySpeed;
        }

        private struct WakeTrailStampCommand
        {
            public Vector4 UvEllipse;
            public Vector4 DirectionStrengthVertical;
        }

        private const int MaxPublishedInteractionPoints = 12;
        private const int MaxQueryColliders = 32;
        private const int InteractionPointStride = 32;
        private const int FlowFieldStride = sizeof(float) * 2;
        private const float DefaultVegetationWaterLevel = 4900f;
        private const float FlowFieldUploadIntervalSeconds = 0.1f;
        private const float FlowFieldRecenterThresholdCells = 0.5f;
        private const int WakeTrailStampCommandCapacity = 4;
        private const int WakeTrailThreadGroupSize = 8;
#if UNITY_EDITOR
        private const string WakeTrailSimulationComputeAssetPath = "Assets/_Project/Art/Shaders/Hecton_VegetationWakeTrailSim.compute";
#endif

        private static readonly int _PropWashPosId = Shader.PropertyToID("_HectonPropWashPosition");
        private static readonly int _PropWashForceId = Shader.PropertyToID("_HectonPropWashForce");
        private static readonly int _InteractionBufferId = Shader.PropertyToID("_HectonFloraInteractionPoints");
        private static readonly int _InteractionCountId = Shader.PropertyToID("_HectonFloraInteractionCount");
        private static readonly int _MarineSnowFlowFieldId = Shader.PropertyToID("_MarineSnowFlowField");
        private static readonly int _MarineSnowFlowFieldCenterCellSizeId = Shader.PropertyToID("_MarineSnowFlowFieldCenterCellSize");
        private static readonly int _FloraFlowFieldResolutionId = Shader.PropertyToID("_HectonFloraFlowFieldResolution");
        private static readonly int _PlayerRuntimePositionId = Shader.PropertyToID("_HectonPlayerRuntimePosition");
        private static readonly int _PlayerFloraInteractionParamsId = Shader.PropertyToID("_HectonPlayerFloraInteractionParams");
        private static readonly int _VegetationFogColorId = Shader.PropertyToID("_HectonVegetationFogColor");
        private static readonly int _VegetationAmbientColorId = Shader.PropertyToID("_HectonVegetationAmbientColor");
        private static readonly int _VegetationDepthId = Shader.PropertyToID("_HectonVegetationDepth");
        private static readonly int _VegetationLightFactorId = Shader.PropertyToID("_HectonVegetationLightFactor");
        private static readonly int _VegetationTurbidityId = Shader.PropertyToID("_HectonVegetationTurbidity");
        private static readonly int _VegetationWaterLevelId = Shader.PropertyToID("_HectonVegetationWaterLevel");
        private static readonly int _VegetationCurrentVectorId = Shader.PropertyToID("_HectonVegetationCurrentVector");
        private static readonly int _GlobalOceanFlowId = Shader.PropertyToID("_GlobalOceanFlow");
        private static readonly int _VegetationCurrentStrengthId = Shader.PropertyToID("_HectonVegetationCurrentStrength");
        private static readonly int _VegetationCurrentNoiseScaleId = Shader.PropertyToID("_HectonVegetationCurrentNoiseScale");
        private static readonly int _VegetationCurrentTimeScaleId = Shader.PropertyToID("_HectonVegetationCurrentTimeScale");
        private static readonly int _VegetationCurrentVerticalFactorId = Shader.PropertyToID("_HectonVegetationCurrentVerticalFactor");
        private static readonly int _WakeTrailTextureId = Shader.PropertyToID("_HectonVegetationWakeTrailRT");
        private static readonly int _WakeTrailWorldRectId = Shader.PropertyToID("_HectonVegetationWakeTrailWorldRect");
        private static readonly int _WakeTrailActiveId = Shader.PropertyToID("_HectonVegetationWakeTrailActive");
        private static readonly int _ShallowWaterFieldTextureId = Shader.PropertyToID("_HectonShallowWaterFieldRT");
        private static readonly int _ShallowWaterFieldWorldRectId = Shader.PropertyToID("_HectonShallowWaterFieldWorldRect");
        private static readonly int _ShallowWaterFieldActiveId = Shader.PropertyToID("_HectonShallowWaterFieldActive");
        private static readonly int _ShallowWaterFieldTexelSizeId = Shader.PropertyToID("_HectonShallowWaterFieldTexelSize");
        private static readonly int _WakeTrailSourceId = Shader.PropertyToID("_HectonWakeTrailSource");
        private static readonly int _WakeTrailResultId = Shader.PropertyToID("_HectonWakeTrailResult");
        private static readonly int _WakeTrailFadeDeltaId = Shader.PropertyToID("_HectonWakeTrailFadeDelta");
        private static readonly int _WakeTrailDiffusionId = Shader.PropertyToID("_HectonWakeTrailDiffusion");
        private static readonly int _WakeTrailWaveStrengthId = Shader.PropertyToID("_HectonWakeTrailWaveStrength");
        private static readonly int _WakeTrailDampingId = Shader.PropertyToID("_HectonWakeTrailDamping");
        private static readonly int _WakeTrailCurlStrengthId = Shader.PropertyToID("_HectonWakeTrailCurlStrength");
        private static readonly int _WakeTrailSimulationTimeId = Shader.PropertyToID("_HectonWakeTrailSimulationTime");
        private static readonly int _WakeTrailTexelSizeId = Shader.PropertyToID("_HectonWakeTrailTexelSize");
        private static readonly int _WakeTrailStampCommandsId = Shader.PropertyToID("_HectonWakeTrailStampCommands");
        private static readonly int _WakeTrailStampCountId = Shader.PropertyToID("_HectonWakeTrailStampCount");
        private static readonly int _WakeTrailScrollUvOffsetId = Shader.PropertyToID("_HectonWakeTrailScrollUvOffset");

        [Header("Runtime Wiring")]
        [SerializeField]
        [Tooltip("Optional direct player override for direct scene play mode when BootstrapState has not published a runtime player yet.")]
        private Transform _playerTransformOverride;

        [SerializeField]
        [Tooltip("Optional direct scooter transform override for isolated prefab or broken-scene validation.")]
        private Transform _scooterTransformOverride;

        [SerializeField]
        [Tooltip("Optional vegetation bridge override used for dense-grass heuristics and sediment interaction bursts.")]
        private HectonMapMagicVegetationBridge _vegetationBridgeOverride;

        [Header("Interaction")]
        [SerializeField, Range(1f, 10f)]
        [Tooltip("Base radius around the player influence point for legacy prop-wash style vegetation response.")]
        private float _baseRadius = 3.5f;

        [SerializeField, Range(0f, 5f)]
        [Tooltip("How much player speed increases the published legacy interaction radius.")]
        private float _velocityRadiusMultiplier = 0.45f;

        [SerializeField, Range(0.1f, 10f)]
        [Tooltip("Maximum player interaction force pushed into legacy vegetation shader parameters.")]
        private float _maxInteractionForce = 4.2f;

        [SerializeField, Range(1f, 20f)]
        [Tooltip("Position smoothing speed for the player interaction point.")]
        private float _positionSmoothSpeed = 12f;

        [SerializeField, Range(1f, 20f)]
        [Tooltip("Radius and force smoothing speed for the player interaction point.")]
        private float _intensitySmoothSpeed = 8f;

        [Header("Velocity Bend")]
        [SerializeField, Range(1, MaxPublishedInteractionPoints)]
        [Tooltip("Maximum number of interaction points published to the global vegetation buffer, including the player.")]
        private int _maxInteractionPoints = 12;

        [SerializeField, Range(4f, 20f)]
        [Tooltip("Attention radius for collecting dynamic object interaction points around the player.")]
        private float _dynamicInteractionRadius = 15f;

        [SerializeField, Range(1.5f, 3f)]
        [Tooltip("Base true-bend radius used for the player interaction point.")]
        private float _playerBendRadius = 2.4f;

        [SerializeField, Range(1.5f, 3f)]
        [Tooltip("Base true-bend radius used for non-player dynamic objects.")]
        private float _dynamicObjectBaseRadius = 2.2f;

        [SerializeField, Range(0f, 0.5f)]
        [Tooltip("Extra true-bend radius per meter per second of velocity.")]
        private float _dynamicVelocityRadiusMultiplier = 0.08f;

        [SerializeField, Range(2f, 3f)]
        [Tooltip("Maximum true-bend radius applied to interaction points.")]
        private float _maxBendRadius = 2.9f;

        [SerializeField, Range(1.5f, 4f)]
        [Tooltip("Base bend radius published for the active Manta scooter wake point.")]
        private float _scooterBendRadius = 2.8f;

        [SerializeField, Range(0.5f, 2f)]
        [Tooltip("Velocity multiplier used for the active Manta scooter wake point.")]
        private float _scooterVelocityMultiplier = 1.35f;

        [SerializeField, Range(0f, 1.5f)]
        [Tooltip("Forward offset used to move the scooter wake point ahead of the held tool transform.")]
        private float _scooterForwardOffset = 0.4f;

        [SerializeField, Range(0.05f, 0.5f)]
        [Tooltip("Spring rise time for new or fast-changing vegetation interaction vectors.")]
        private float _interactionRiseSmoothTime = 0.12f;

        [SerializeField, Range(0.5f, 2f)]
        [Tooltip("Spring recovery time used when interaction sources stop pushing the vegetation field.")]
        private float _interactionRecoverySmoothTime = 1.25f;

        [SerializeField, Range(0.01f, 0.25f)]
        [Tooltip("Velocity threshold below which a recovered interaction point is dropped from publication.")]
        private float _interactionReleaseSpeed = 0.08f;

        [SerializeField]
        [Tooltip("Physics layers considered for dynamic vegetation interaction queries.")]
        private LayerMask _dynamicInteractionMask = ~0;

        [Header("Wake Trail")]
        [SerializeField, Range(256, 256)]
        [Tooltip("Resolution of the shallow-water interaction field around the player. MX350 mandate fixes this at 256x256.")]
        private int _wakeTrailResolution = 256;

        [SerializeField, Range(64f, 192f)]
        [Tooltip("World-space coverage of the shallow-water field centered around the player.")]
        private float _wakeTrailWorldSize = 128f;

        [SerializeField, Range(8f, 16f)]
        [Tooltip("Seconds required for the persistent wake trail to fade out back to calm water.")]
        private float _wakeTrailFadeSeconds = 12f;

        [SerializeField, Range(0.1f, 2f)]
        [Tooltip("Persistent wake intensity written by the player body when moving through vegetation.")]
        private float _wakeTrailPlayerStrength = 0.28f;

        [SerializeField, Range(0.25f, 2f)]
        [Tooltip("Persistent wake intensity written by the active Manta scooter.")]
        private float _wakeTrailScooterStrength = 0.95f;

        [SerializeField, Range(0.5f, 4f)]
        [Tooltip("Base half-width of each wake trail stamp in world meters.")]
        private float _wakeTrailBaseRadius = 1.35f;

        [SerializeField, Range(1f, 20f)]
        [Tooltip("Minimum world-space trail length written per wake stamp.")]
        private float _wakeTrailMinLength = 2.4f;

        [SerializeField, Range(4f, 30f)]
        [Tooltip("Maximum world-space trail length written per wake stamp.")]
        private float _wakeTrailMaxLength = 15f;

        [SerializeField, Range(0.05f, 0.75f)]
        [Tooltip("Extra trail length written per meter per second of source velocity.")]
        private float _wakeTrailVelocityToLength = 0.28f;

        [SerializeField, Range(0.25f, 4f)]
        [Tooltip("Minimum player speed required before persistent wake stamps start accumulating.")]
        private float _wakeTrailPlayerMinSpeed = 0.75f;

        [SerializeField, Range(0.25f, 4f)]
        [Tooltip("Minimum scooter speed required before persistent wake stamps start accumulating.")]
        private float _wakeTrailScooterMinSpeed = 0.45f;

        [SerializeField, Range(0.1f, 4f)]
        [Tooltip("Pixel stride used to quantize treadmill recentering and avoid sub-pixel wake shimmer.")]
        private float _wakeTrailCenterSnapPixelStride = 1f;

        [SerializeField]
        [Tooltip("Optional compute shader used to evolve the persistent wake trail into reactive spreading ripples.")]
        private ComputeShader _wakeTrailSimulationCompute;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Neighbor blending factor used by the wake ripple simulation.")]
        private float _wakeTrailDiffusion = 0.22f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Wave propagation strength used when the wake trail expands into surrounding water.")]
        private float _wakeTrailWaveStrength = 0.36f;

        [SerializeField, Range(0.5f, 1f)]
        [Tooltip("Per-step damping used by the wake ripple simulation.")]
        private float _wakeTrailWaveDamping = 0.94f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Curl-noise advection strength used to form micro-vortices inside the reactive wake field.")]
        private float _wakeTrailCurlStrength = 0.42f;

        [Header("Sediment Interaction")]
        [SerializeField]
        [Tooltip("Optional scene particle system used for sediment bursts kicked out of dense grass. If null, a hidden local system is created once.")]
        private ParticleSystem _sedimentBurstParticleSystem;

        [SerializeField, Range(1024, 65535)]
        [Tooltip("Minimum active surface instance count required before the manager considers the current area dense enough for grass sediment bursts.")]
        private int _denseGrassInstanceThreshold = 8192;

        [SerializeField, Range(1f, 20f)]
        [Tooltip("Minimum speed required before player movement starts emitting sediment bursts in dense grass.")]
        private float _playerSedimentMinSpeed = 4.5f;

        [SerializeField, Range(1f, 30f)]
        [Tooltip("Minimum speed required before scooter wake starts emitting sediment bursts in dense grass.")]
        private float _scooterSedimentMinSpeed = 7.5f;

        [SerializeField, Range(0.05f, 1f)]
        [Tooltip("Minimum time between player sediment burst emissions.")]
        private float _playerSedimentCooldown = 0.16f;

        [SerializeField, Range(0.05f, 1f)]
        [Tooltip("Minimum time between scooter sediment burst emissions.")]
        private float _scooterSedimentCooldown = 0.09f;

        [SerializeField, Range(0.5f, 4f)]
        [Tooltip("Base world radius of one sediment burst stamp.")]
        private float _sedimentBurstRadius = 1.3f;

        [SerializeField, Range(2, 32)]
        [Tooltip("Maximum particle count emitted by one dense-grass sediment burst.")]
        private int _sedimentMaxBurstCount = 18;

        private Vector3 _smoothPosition;
        private float _smoothRadius;
        private float _smoothForce;
        private Transform _playerTransform;
        private Rigidbody _playerRb;
        private PlayerToolManager _playerToolManager;
        private Transform _activeScooterTransform;
        private Vector3 _lastPlayerPosition;
        private Vector3 _lastPublishedPlayerVelocity;
        private Vector3 _lastPublishedScooterWakePosition;
        private Vector3 _smoothedPlayerVelocity;
        private Vector3 _smoothedPlayerVelocityDamp;
        private Vector3 _smoothedScooterVelocity;
        private Vector3 _smoothedScooterVelocityDamp;
        private Vector3 _smoothedScooterPosition;
        private Vector3 _smoothedScooterPositionDamp;
        private bool _hasLastPlayerPosition;
        private bool _hasSmoothedScooterPosition;
        private bool _hasActiveScooterWake;
        private bool _isRegistered;
        private int _lastPublishedInteractionCount;

        private FloraInteractionPointGpuData[] _interactionPoints;
        private Collider[] _interactionColliders;
        private Rigidbody[] _interactionBodies;
        private GraphicsBuffer _interactionBuffer;
        private GraphicsBuffer _flowFieldBuffer;
        private GraphicsBuffer _wakeTrailStampCommandBuffer;
        private RenderTexture _wakeTrailRead;
        private RenderTexture _wakeTrailWrite;
        private Vector4 _wakeTrailWorldRect;
        private Vector2 _wakeTrailCenterXZ;
        private Vector2 _pendingWakeTrailScrollUv;
        private NativeArray<WakeTrailStampCommand> _queuedWakeTrailStampCommands;
        private float _wakeTrailRuntimeWorldSize;
        private float _wakeTrailEnergy;
        private float _playerSedimentCooldownRemaining;
        private float _scooterSedimentCooldownRemaining;
        private bool _wakeTrailDisabled;
        private int _queuedWakeTrailStampCount;
        private int _lastWakeTrailDispatchFrame = -1;
        private int _wakeTrailRuntimeResolution;
        private int _wakeTrailQualityLevel = -1;
        private int _wakeTrailSimulationKernel = -1;
        private int _flowFieldResolution;
        private float _flowFieldCellSize;
        private float _flowFieldUploadTimer;
        private HectonMapMagicVegetationBridge _vegetationBridge;
        private IHectonOceanKinematics _oceanKinematicsProvider;
        private Vector3 _flowFieldCenterWS;
        private Vector3 _lastUploadedFlowFieldCenterWS;
        private NativeArray<Vector3> _oceanFlowSamplePositions;
        private NativeArray<Vector3> _oceanFlowSampleResults;
        private ParticleSystem.EmitParams _sedimentEmitParams;

        /// <summary>Last interaction point count pushed into the global flora buffer.</summary>
        public int PublishedInteractionCount => _lastPublishedInteractionCount;

        /// <summary>True when the active Manta scooter wake point is currently being published.</summary>
        public bool HasActiveScooterWake => _hasActiveScooterWake;

        /// <summary>Last published player velocity vector.</summary>
        public Vector3 LastPublishedPlayerVelocity => _lastPublishedPlayerVelocity;

        /// <summary>Last published scooter wake anchor position.</summary>
        public Vector3 LastPublishedScooterWakePosition => _lastPublishedScooterWakePosition;

        /// <summary>Approximate VRAM footprint in bytes for the wake-trail ping-pong textures and interaction buffer.</summary>
        public long GetVRAMEstimation()
        {
            long totalBytes = 0L;
            totalBytes += EstimateGraphicsBufferBytes(_interactionBuffer);
            totalBytes += EstimateGraphicsBufferBytes(_flowFieldBuffer);
            totalBytes += EstimateRenderTextureBytes(_wakeTrailRead);
            totalBytes += EstimateRenderTextureBytes(_wakeTrailWrite);
            return totalBytes;
        }

        private void Awake()
        {
            _maxInteractionPoints = Mathf.Clamp(_maxInteractionPoints, 1, MaxPublishedInteractionPoints);
            _wakeTrailResolution = 256;
            _wakeTrailWorldSize = Mathf.Max(32f, _wakeTrailWorldSize);
            _wakeTrailFadeSeconds = Mathf.Max(0.1f, _wakeTrailFadeSeconds);
            _wakeTrailDiffusion = Mathf.Clamp01(_wakeTrailDiffusion);
            _wakeTrailWaveStrength = Mathf.Clamp01(_wakeTrailWaveStrength);
            _wakeTrailWaveDamping = Mathf.Clamp(_wakeTrailWaveDamping, 0.5f, 1f);
            _denseGrassInstanceThreshold = Mathf.Max(1024, _denseGrassInstanceThreshold);
            _sedimentMaxBurstCount = Mathf.Clamp(_sedimentMaxBurstCount, 2, 32);
            _wakeTrailQualityLevel = QualitySettings.GetQualityLevel();
            _wakeTrailRuntimeResolution = ResolveWakeTrailResolutionForQuality(_wakeTrailQualityLevel);
            _vegetationBridge = ResolveVegetationBridge();
            TryAutoAssignWakeTrailSimulationCompute();
            if (_wakeTrailSimulationCompute != null)
                _wakeTrailSimulationKernel = _wakeTrailSimulationCompute.FindKernel("SimulateWakeTrail");

            // COLD ALLOC: FloraInteractionPointGpuData[_maxInteractionPoints] - global vegetation interaction payload - owner: FloraInteractionManager
            _interactionPoints = new FloraInteractionPointGpuData[_maxInteractionPoints];
            // COLD ALLOC: Collider[32] - NonAlloc interaction query results - owner: FloraInteractionManager
            _interactionColliders = new Collider[MaxQueryColliders];
            // COLD ALLOC: Rigidbody[32] - duplicate suppression for interaction query results - owner: FloraInteractionManager
            _interactionBodies = new Rigidbody[MaxQueryColliders];
            _interactionBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<FloraInteractionPointGpuData>(_maxInteractionPoints); // COLD ALLOC: GraphicsBuffer[_maxInteractionPoints] - global vegetation interaction StructuredBuffer - owner: FloraInteractionManager
            // COLD ALLOC: NativeArray<Vector3>[1] - caller-owned ocean provider sample positions for vegetation flow publishing - owner: FloraInteractionManager
            _oceanFlowSamplePositions = new NativeArray<Vector3>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<Vector3>[1] - caller-owned ocean provider sample results for vegetation flow publishing - owner: FloraInteractionManager
            _oceanFlowSampleResults = new NativeArray<Vector3>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            Shader.SetGlobalBuffer(_InteractionBufferId, _interactionBuffer);
            PublishFlowFieldGlobals();
            CreateWakeTrailResources();
            EnsureSedimentParticleSystem();
            ResetInteractionGlobals();
            PublishEnvironmentGlobals(Vector3.zero);
        }

        private void OnEnable()
        {
            if (_interactionBuffer != null)
                Shader.SetGlobalBuffer(_InteractionBufferId, _interactionBuffer);

            HectonFloatingOrigin.RegisterListener(this);
            PublishFlowFieldGlobals();
            PublishWakeTrailGlobals();
            TryRegister();
            PublishEnvironmentGlobals(_playerTransform != null ? _playerTransform.position : Vector3.zero);
        }

        private void OnDisable()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregister();
            ResetInteractionGlobals();
        }

        private void OnDestroy()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregister();
            ResetInteractionGlobals();

            if (_oceanFlowSamplePositions.IsCreated)
                _oceanFlowSamplePositions.Dispose();

            if (_oceanFlowSampleResults.IsCreated)
                _oceanFlowSampleResults.Dispose();

            if (_interactionBuffer != null)
            {
                _interactionBuffer.Release();
                _interactionBuffer = null;
            }

            ReleaseFlowFieldBuffer();
            ReleaseWakeTrailResources();
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (!isActiveAndEnabled || shiftData.ShiftOffset.sqrMagnitude <= 0.0001f)
                return;

            ApplyRuntimeOffsetToCachedState(-shiftData.ShiftOffset);
        }

        /// <summary>
        /// Updates published vegetation interaction and environment globals.
        /// </summary>
        /// <param name="deltaTime">Current frame delta.</param>
        public void Tick(float deltaTime)
        {
            RefreshQualityDependentResourcesIfNeeded();
            UpdateSedimentCooldowns(deltaTime);

            Transform runtimePlayerTransform = ResolveRuntimePlayerTransform();
            PublishEnvironmentGlobals(runtimePlayerTransform != null ? runtimePlayerTransform.position : Vector3.zero);
            RefreshFlowFieldGlobals(deltaTime);
            if (runtimePlayerTransform == null)
            {
                ResetInteractionGlobals();
                return;
            }

            ResolvePlayerState(runtimePlayerTransform);

            Vector3 targetPosition = runtimePlayerTransform.position;
            Vector3 playerVelocity = UpdatePlayerSpringVelocity(ResolvePlayerVelocity(targetPosition, deltaTime), deltaTime);
            float velocityMagnitude = playerVelocity.magnitude;
            _lastPublishedPlayerVelocity = playerVelocity;
            _hasActiveScooterWake = false;

            float targetRadius = _baseRadius + velocityMagnitude * _velocityRadiusMultiplier;
            float targetForce = Mathf.Clamp(velocityMagnitude * 0.85f, 0f, _maxInteractionForce);

            _smoothPosition = Vector3.Lerp(_smoothPosition, targetPosition, deltaTime * _positionSmoothSpeed);
            _smoothRadius = Mathf.Lerp(_smoothRadius, targetRadius, deltaTime * _intensitySmoothSpeed);
            _smoothForce = Mathf.Lerp(_smoothForce, targetForce, deltaTime * _intensitySmoothSpeed);

            int interactionCount = 0;
            float playerBendRadius = Mathf.Clamp(
                _playerBendRadius + velocityMagnitude * _dynamicVelocityRadiusMultiplier,
                0.5f,
                _maxBendRadius);
            PublishPlayerRuntimePosition(targetPosition, playerBendRadius, velocityMagnitude, targetForce);
            interactionCount = AppendInteractionPoint(_smoothPosition, playerVelocity, playerBendRadius, interactionCount);
            interactionCount = AppendScooterInteractionPoint(playerVelocity, interactionCount, deltaTime);
            interactionCount = CollectDynamicInteractionPoints(targetPosition, interactionCount);
            UpdateWakeTrail(runtimePlayerTransform.position, playerVelocity, deltaTime);
            TryEmitSedimentBursts(targetPosition, playerVelocity);

            Shader.SetGlobalVector(
                _PropWashPosId,
                new Vector4(_smoothPosition.x, _smoothPosition.y, _smoothPosition.z, _smoothRadius));
            Shader.SetGlobalFloat(_PropWashForceId, _smoothForce);

            if (_interactionBuffer != null && interactionCount > 0)
            {
                GraphicsBufferUploadUtility.UploadArray(_interactionBuffer, _interactionPoints, interactionCount);
                Shader.SetGlobalBuffer(_InteractionBufferId, _interactionBuffer);
                Shader.SetGlobalInt(_InteractionCountId, interactionCount);
                _lastPublishedInteractionCount = interactionCount;
                return;
            }

            Shader.SetGlobalInt(_InteractionCountId, 0);
            _lastPublishedInteractionCount = 0;
        }

        private Transform ResolveRuntimePlayerTransform()
        {
            Transform runtimePlayerTransform = BootstrapState.CurrentPlayerTransform;
            if (runtimePlayerTransform != null)
                return runtimePlayerTransform;

            return _playerTransformOverride;
        }

        private void ResolvePlayerState(Transform runtimePlayerTransform)
        {
            if (_playerTransform == runtimePlayerTransform)
            {
                ResolveScooterState();
                return;
            }

            _playerTransform = runtimePlayerTransform;
            _playerRb = runtimePlayerTransform.GetComponent<Rigidbody>();
            _playerToolManager = ResolvePlayerToolManager(runtimePlayerTransform);
            _activeScooterTransform = _scooterTransformOverride;
            _smoothPosition = runtimePlayerTransform.position;
            _lastPlayerPosition = runtimePlayerTransform.position;
            _hasLastPlayerPosition = true;
            _smoothedPlayerVelocity = Vector3.zero;
            _smoothedPlayerVelocityDamp = Vector3.zero;
            _smoothedScooterVelocity = Vector3.zero;
            _smoothedScooterVelocityDamp = Vector3.zero;
            _smoothedScooterPosition = _activeScooterTransform != null ? _activeScooterTransform.position : runtimePlayerTransform.position;
            _smoothedScooterPositionDamp = Vector3.zero;
            _hasSmoothedScooterPosition = _activeScooterTransform != null;
            ResolveScooterState();
        }

        private void RefreshQualityDependentResourcesIfNeeded()
        {
            int qualityLevel = QualitySettings.GetQualityLevel();
            if (_wakeTrailQualityLevel == qualityLevel)
                return;

            _wakeTrailQualityLevel = qualityLevel;
            int desiredResolution = ResolveWakeTrailResolutionForQuality(qualityLevel);
            if (_wakeTrailRuntimeResolution == desiredResolution)
                return;

            _wakeTrailRuntimeResolution = desiredResolution;
            ReleaseWakeTrailResources();
            CreateWakeTrailResources();
        }

        private int ResolveWakeTrailResolutionForQuality(int qualityLevel)
        {
            string[] qualityNames = QualitySettings.names;
            string qualityName = qualityLevel >= 0 && qualityLevel < qualityNames.Length ? qualityNames[qualityLevel] : string.Empty;
            return 256;
        }

        private Vector3 ResolvePlayerVelocity(Vector3 targetPosition, float deltaTime)
        {
            if (_playerRb != null)
            {
                _lastPlayerPosition = targetPosition;
                _hasLastPlayerPosition = true;
                return _playerRb.linearVelocity;
            }

            if (!_hasLastPlayerPosition || deltaTime <= 0.0001f)
            {
                _lastPlayerPosition = targetPosition;
                _hasLastPlayerPosition = true;
                return Vector3.zero;
            }

            if (!TryResolveSafeReciprocal(deltaTime, out float inverseDeltaTime))
            {
                _lastPlayerPosition = targetPosition;
                return Vector3.zero;
            }

            Vector3 velocity = HectonPlayerMotor.SafeVelocity((targetPosition - _lastPlayerPosition) * inverseDeltaTime);
            _lastPlayerPosition = targetPosition;
            return velocity;
        }

        private static bool TryResolveSafeReciprocal(float value, out float reciprocal)
        {
            if (!float.IsFinite(value) || math.abs(value) <= 0.0001f)
            {
                reciprocal = 0f;
                return false;
            }

            reciprocal = 1f / value;
            return float.IsFinite(reciprocal);
        }

        private int CollectDynamicInteractionPoints(Vector3 targetPosition, int interactionCount)
        {
            int hitCount = global::UnityEngine.Physics.OverlapSphereNonAlloc(
                targetPosition,
                _dynamicInteractionRadius,
                _interactionColliders,
                _dynamicInteractionMask,
                QueryTriggerInteraction.Ignore);

            int uniqueBodyCount = 0;
            for (int i = 0; i < hitCount && interactionCount < _maxInteractionPoints; i++)
            {
                Collider hitCollider = _interactionColliders[i];
                if (hitCollider == null)
                    continue;

                Rigidbody hitBody = hitCollider.attachedRigidbody;
                if (hitBody == null || hitBody == _playerRb || hitBody.transform == _playerTransform)
                    continue;

                bool duplicate = false;
                for (int j = 0; j < uniqueBodyCount; j++)
                {
                    if (_interactionBodies[j] == hitBody)
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (duplicate)
                    continue;

                if (uniqueBodyCount < _interactionBodies.Length)
                    _interactionBodies[uniqueBodyCount++] = hitBody;

                Vector3 velocity = hitBody.linearVelocity;
                float radius = Mathf.Clamp(
                    _dynamicObjectBaseRadius + velocity.magnitude * _dynamicVelocityRadiusMultiplier,
                    0.5f,
                    _maxBendRadius);
                interactionCount = AppendInteractionPoint(hitBody.worldCenterOfMass, velocity, radius, interactionCount);
            }

            return interactionCount;
        }

        private int AppendScooterInteractionPoint(Vector3 playerVelocity, int interactionCount, float deltaTime)
        {
            ResolveScooterState();

            if (interactionCount >= _maxInteractionPoints)
                return interactionCount;

            bool hasScooterSource = _activeScooterTransform != null;
            Vector3 targetVelocity = hasScooterSource ? playerVelocity * _scooterVelocityMultiplier : Vector3.zero;
            Vector3 smoothedVelocity = SmoothInteractionVector(
                _smoothedScooterVelocity,
                targetVelocity,
                ref _smoothedScooterVelocityDamp,
                deltaTime);
            float speed = smoothedVelocity.magnitude;
            if (speed <= _interactionReleaseSpeed)
                return interactionCount;

            Vector3 targetScooterPosition = _smoothedScooterPosition;
            if (hasScooterSource)
            {
                targetScooterPosition = _activeScooterTransform.position;
                if (_scooterForwardOffset > 0.0001f)
                    targetScooterPosition += _activeScooterTransform.forward * _scooterForwardOffset;
            }

            if (!_hasSmoothedScooterPosition)
            {
                _smoothedScooterPosition = targetScooterPosition;
                _hasSmoothedScooterPosition = true;
            }

            _smoothedScooterPosition = Vector3.SmoothDamp(
                _smoothedScooterPosition,
                targetScooterPosition,
                ref _smoothedScooterPositionDamp,
                hasScooterSource ? _interactionRiseSmoothTime : _interactionRecoverySmoothTime,
                Mathf.Infinity,
                deltaTime);

            float radius = Mathf.Clamp(
                _scooterBendRadius + speed * _dynamicVelocityRadiusMultiplier,
                0.5f,
                _maxBendRadius);
            _hasActiveScooterWake = true;
            _lastPublishedScooterWakePosition = _smoothedScooterPosition;

            return AppendInteractionPoint(_smoothedScooterPosition, smoothedVelocity, radius, interactionCount);
        }

        private Vector3 UpdatePlayerSpringVelocity(Vector3 targetVelocity, float deltaTime)
        {
            _smoothedPlayerVelocity = SmoothInteractionVector(
                _smoothedPlayerVelocity,
                targetVelocity,
                ref _smoothedPlayerVelocityDamp,
                deltaTime);
            return _smoothedPlayerVelocity;
        }

        private Vector3 SmoothInteractionVector(
            Vector3 currentVelocity,
            Vector3 targetVelocity,
            ref Vector3 smoothVelocity,
            float deltaTime)
        {
            if (deltaTime <= 0.0001f)
                return currentVelocity;

            float smoothTime = targetVelocity.sqrMagnitude > 0.0001f
                ? _interactionRiseSmoothTime
                : _interactionRecoverySmoothTime;

            return Vector3.SmoothDamp(
                currentVelocity,
                targetVelocity,
                ref smoothVelocity,
                smoothTime,
                Mathf.Infinity,
                deltaTime);
        }

        private PlayerToolManager ResolvePlayerToolManager(Transform runtimePlayerTransform)
        {
            if (runtimePlayerTransform == null)
                return null;

            if (runtimePlayerTransform.TryGetComponent(out PlayerToolManager directToolManager))
                return directToolManager;

            return ((Hecton8.Core.GlobalRegistry.Player != null && Hecton8.Core.GlobalRegistry.Player.ToolManager != null) ? Hecton8.Core.GlobalRegistry.Player.ToolManager : runtimePlayerTransform.GetComponent<PlayerToolManager>());
        }

        private void ResolveScooterState()
        {
            _activeScooterTransform = _scooterTransformOverride;

            if (_playerTransform == null)
                return;

            if (_playerToolManager == null)
                _playerToolManager = ResolvePlayerToolManager(_playerTransform);

            if (_playerToolManager == null || _playerToolManager.IsSwapping)
                return;

            if (!(_playerToolManager.CurrentTool is MantaScooter scooter) || !scooter.IsTransportActive)
                return;

            if (_activeScooterTransform == null)
                _activeScooterTransform = scooter.transform;
        }

        private HectonMapMagicVegetationBridge ResolveVegetationBridge()
        {
            if (_vegetationBridgeOverride != null)
                return _vegetationBridgeOverride;

            HectonMapMagicVegetationBridge directBridge = GetComponent<HectonMapMagicVegetationBridge>();
            if (directBridge != null)
                return directBridge;

            HectonMapMagicVegetationBridge childBridge = Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<HectonMapMagicVegetationBridge>(transform);
            if (childBridge != null)
                return childBridge;

            return GetComponentInParent<HectonMapMagicVegetationBridge>();
        }

        private void EnsureSedimentParticleSystem()
        {
            if (_sedimentBurstParticleSystem != null)
                return;

            GameObject sedimentObject = new GameObject("__VegetationSedimentBursts");
            sedimentObject.hideFlags = HideFlags.HideAndDontSave;
            sedimentObject.transform.SetParent(transform, false);
            _sedimentBurstParticleSystem = sedimentObject.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = _sedimentBurstParticleSystem.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 256;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.58f, 0.64f, 0.56f, 0.34f));
            main.gravityModifier = new ParticleSystem.MinMaxCurve(-0.02f);

            ParticleSystem.EmissionModule emission = _sedimentBurstParticleSystem.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = _sedimentBurstParticleSystem.shape;
            shape.enabled = false;

            ParticleSystem.NoiseModule noise = _sedimentBurstParticleSystem.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.18f);
            noise.frequency = 0.28f;

            ParticleSystem.VelocityOverLifetimeModule velocityOverLifetime = _sedimentBurstParticleSystem.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.06f, 0.22f);

            ParticleSystemRenderer renderer = _sedimentBurstParticleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
        }

        private void UpdateSedimentCooldowns(float deltaTime)
        {
            if (_playerSedimentCooldownRemaining > 0f)
            {
                _playerSedimentCooldownRemaining -= deltaTime;
                if (_playerSedimentCooldownRemaining < 0f)
                    _playerSedimentCooldownRemaining = 0f;
            }

            if (_scooterSedimentCooldownRemaining > 0f)
            {
                _scooterSedimentCooldownRemaining -= deltaTime;
                if (_scooterSedimentCooldownRemaining < 0f)
                    _scooterSedimentCooldownRemaining = 0f;
            }
        }

        private void TryEmitSedimentBursts(Vector3 playerPosition, Vector3 playerVelocity)
        {
            if (_sedimentBurstParticleSystem == null)
                return;

            float playerSpeed = playerVelocity.magnitude;
            if (_playerSedimentCooldownRemaining <= 0f && playerSpeed >= _playerSedimentMinSpeed && IsInsideDenseGrassZone(playerPosition))
            {
                EmitSedimentBurst(playerPosition, playerVelocity, false);
                _playerSedimentCooldownRemaining = _playerSedimentCooldown;
            }

            float scooterSpeed = _smoothedScooterVelocity.magnitude;
            if (_hasActiveScooterWake &&
                _scooterSedimentCooldownRemaining <= 0f &&
                scooterSpeed >= _scooterSedimentMinSpeed &&
                IsInsideDenseGrassZone(_lastPublishedScooterWakePosition))
            {
                EmitSedimentBurst(_lastPublishedScooterWakePosition, _smoothedScooterVelocity, true);
                _scooterSedimentCooldownRemaining = _scooterSedimentCooldown;
            }
        }

        private bool IsInsideDenseGrassZone(Vector3 positionWS)
        {
            if (_vegetationBridge == null)
                _vegetationBridge = ResolveVegetationBridge();

            if (_vegetationBridge == null || _vegetationBridge.ActiveSurfaceInstanceCount < _denseGrassInstanceThreshold)
                return false;

            Bounds surfaceBounds = _vegetationBridge.ActiveSurfaceDrawBounds;
            if (surfaceBounds.size.sqrMagnitude <= 0.0001f || !surfaceBounds.Contains(positionWS))
                return false;

            float waterLevel = HectonFluidEngine.Instance != null ? HectonFluidEngine.Instance.WaterLevel : DefaultVegetationWaterLevel;
            return positionWS.y <= waterLevel - 0.25f;
        }

        private void EmitSedimentBurst(Vector3 positionWS, Vector3 velocityWS, bool scooterBurst)
        {
            if (_sedimentBurstParticleSystem == null)
                return;

            if (!_sedimentBurstParticleSystem.isPlaying)
                _sedimentBurstParticleSystem.Play(true);

            float speed = velocityWS.magnitude;
            float burstRadiusScale = Mathf.Clamp(_sedimentBurstRadius * 0.5f, 0.4f, 2f);
            int burstCount = Mathf.Clamp(Mathf.RoundToInt(speed * (scooterBurst ? 0.9f : 0.55f)), 2, _sedimentMaxBurstCount);
            Vector3 planarVelocity = new Vector3(velocityWS.x, 0f, velocityWS.z);
            if (planarVelocity.sqrMagnitude <= 0.0001f)
                planarVelocity = Vector3.forward;
            planarVelocity.Normalize();

            _sedimentEmitParams.position = positionWS + Vector3.down * 0.18f;
            _sedimentEmitParams.velocity = planarVelocity * Mathf.Min(speed * (0.16f + _sedimentBurstRadius * 0.015f), 3.2f) + Vector3.up * (scooterBurst ? 0.38f : 0.22f);
            _sedimentEmitParams.startSize = Mathf.Lerp(0.08f, 0.24f, Mathf.InverseLerp(_playerSedimentMinSpeed, _scooterSedimentMinSpeed * 2f, speed)) * burstRadiusScale;
            _sedimentEmitParams.startLifetime = Mathf.Lerp(1.0f, 2.0f, Mathf.InverseLerp(_playerSedimentMinSpeed, _scooterSedimentMinSpeed * 2f, speed));
            _sedimentEmitParams.startColor = scooterBurst
                ? new Color(0.62f, 0.7f, 0.62f, 0.36f)
                : new Color(0.55f, 0.6f, 0.54f, 0.28f);
            _sedimentBurstParticleSystem.Emit(_sedimentEmitParams, burstCount);
        }

        private int AppendInteractionPoint(Vector3 position, Vector3 velocity, float radius, int interactionCount)
        {
            if (_interactionPoints == null || _maxInteractionPoints <= 0)
                return 0;

            if (interactionCount < 0)
                interactionCount = 0;

            int interactionCapacity = Mathf.Min(_maxInteractionPoints, _interactionPoints.Length);
            if (interactionCount >= interactionCapacity)
                return interactionCount;

            _interactionPoints[interactionCount] = new FloraInteractionPointGpuData
            {
                PositionRadius = new Vector4(
                    position.x,
                    position.y,
                    position.z,
                    Mathf.Max(0.05f, radius)),
                VelocitySpeed = new Vector4(
                    velocity.x,
                    velocity.y,
                    velocity.z,
                    velocity.magnitude)
            };
            return interactionCount + 1;
        }

        private void PublishEnvironmentGlobals(Vector3 samplePositionWS)
        {
            HectonUnderwaterVisuals underwaterVisuals = HectonUnderwaterVisuals.ActiveRuntimeInstance;
            float depth = underwaterVisuals != null ? underwaterVisuals.CurrentDepth : 0f;
            float lightFactor = underwaterVisuals != null ? underwaterVisuals.CurrentLightFactor : 1f;
            float turbidity = underwaterVisuals != null ? underwaterVisuals.CurrentTurbidity : 0f;

            HectonFluidEngine fluidEngine = HectonFluidEngine.Instance;
            float waterLevel = fluidEngine != null ? fluidEngine.WaterLevel : DefaultVegetationWaterLevel;
            Vector3 currentVector = ResolveGlobalOceanFlow(samplePositionWS, fluidEngine);
            float currentStrength = currentVector.magnitude;
            float currentNoiseScale = fluidEngine != null && fluidEngine.EnablePhantomCurrent ? fluidEngine.CurrentNoiseScale : 0f;
            float currentTimeScale = fluidEngine != null && fluidEngine.EnablePhantomCurrent ? fluidEngine.CurrentTimeScale : 0f;
            float currentVerticalFactor = fluidEngine != null && fluidEngine.EnablePhantomCurrent ? fluidEngine.CurrentVerticalFactor : 0f;

            Shader.SetGlobalColor(_VegetationFogColorId, RenderSettings.fogColor);
            Shader.SetGlobalColor(_VegetationAmbientColorId, ResolveAmbientColor());
            Shader.SetGlobalFloat(_VegetationDepthId, depth);
            Shader.SetGlobalFloat(_VegetationLightFactorId, lightFactor);
            Shader.SetGlobalFloat(_VegetationTurbidityId, turbidity);
            Shader.SetGlobalFloat(_VegetationWaterLevelId, waterLevel);
            Shader.SetGlobalVector(_GlobalOceanFlowId, new Vector4(currentVector.x, currentVector.y, currentVector.z, 0f));
            Shader.SetGlobalVector(
                _VegetationCurrentVectorId,
                new Vector4(currentVector.x, currentVector.y, currentVector.z, 0f));
            Shader.SetGlobalFloat(_VegetationCurrentStrengthId, currentStrength);
            Shader.SetGlobalFloat(_VegetationCurrentNoiseScaleId, currentNoiseScale);
            Shader.SetGlobalFloat(_VegetationCurrentTimeScaleId, currentTimeScale);
            Shader.SetGlobalFloat(_VegetationCurrentVerticalFactorId, currentVerticalFactor);
        }

        private void RefreshFlowFieldGlobals(float deltaTime)
        {
            _flowFieldUploadTimer -= deltaTime;
            if (_vegetationBridge == null)
                _vegetationBridge = ResolveVegetationBridge();

            if (_vegetationBridge == null)
            {
                _flowFieldResolution = 0;
                _flowFieldCellSize = 0f;
                _flowFieldCenterWS = Vector3.zero;
                PublishFlowFieldGlobals();
                return;
            }

            bool hasPayload = _vegetationBridge.TryGetEcosystemFlowFieldPayload(
                out NativeArray<float2> flowVectors,
                out int gridResolution,
                out Vector3 gridCenter,
                out float cellSize);
            if (!hasPayload)
            {
                _flowFieldResolution = 0;
                _flowFieldCellSize = 0f;
                _flowFieldCenterWS = Vector3.zero;
                PublishFlowFieldGlobals();
                return;
            }

            _flowFieldResolution = gridResolution;
            _flowFieldCellSize = cellSize;
            _flowFieldCenterWS = gridCenter;

            float recenterThreshold = math.max(0.01f, cellSize * FlowFieldRecenterThresholdCells);
            bool forceUpload =
                _flowFieldBuffer == null ||
                _flowFieldUploadTimer <= 0f ||
                _lastUploadedFlowFieldCenterWS == Vector3.zero ||
                (gridCenter - _lastUploadedFlowFieldCenterWS).sqrMagnitude >= recenterThreshold * recenterThreshold;

            if (forceUpload)
            {
                int requiredCount = math.max(1, flowVectors.Length);
                if (_flowFieldBuffer == null || _flowFieldBuffer.count != requiredCount)
                {
                    ReleaseFlowFieldBuffer();
                    _flowFieldBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float2>(requiredCount); // COLD ALLOC: GraphicsBuffer[flowVectors.Length] - authoritative ecosystem flow-field GPU staging for flora shading - owner: FloraInteractionManager
                }

                GraphicsBufferUploadUtility.UploadNativeArray(_flowFieldBuffer, flowVectors, requiredCount);
                _lastUploadedFlowFieldCenterWS = gridCenter;
                _flowFieldUploadTimer = FlowFieldUploadIntervalSeconds;
            }

            PublishFlowFieldGlobals();
        }

        private void PublishFlowFieldGlobals()
        {
            if (_flowFieldBuffer != null)
                Shader.SetGlobalBuffer(_MarineSnowFlowFieldId, _flowFieldBuffer);

            Shader.SetGlobalVector(
                _MarineSnowFlowFieldCenterCellSizeId,
                new Vector4(_flowFieldCenterWS.x, _flowFieldCenterWS.y, _flowFieldCenterWS.z, _flowFieldCellSize));
            Shader.SetGlobalInt(_FloraFlowFieldResolutionId, _flowFieldResolution);
        }

        private void PublishPlayerRuntimePosition(
            Vector3 playerRuntimePosition,
            float playerBendRadius,
            float playerSpeed,
            float targetForce)
        {
            float normalizedForce = _maxInteractionForce > 0.0001f
                ? Mathf.Clamp01(targetForce / _maxInteractionForce)
                : 0f;

            Shader.SetGlobalVector(
                _PlayerRuntimePositionId,
                new Vector4(
                    playerRuntimePosition.x,
                    playerRuntimePosition.y,
                    playerRuntimePosition.z,
                    Mathf.Max(0.05f, playerBendRadius)));
            Shader.SetGlobalVector(
                _PlayerFloraInteractionParamsId,
                new Vector4(
                    playerSpeed,
                    normalizedForce,
                    _hasActiveScooterWake ? 1f : 0f,
                    1f));
        }

        private Vector3 ResolveGlobalOceanFlow(Vector3 samplePositionWS, HectonFluidEngine fluidEngine)
        {
            IHectonOceanKinematics provider = HectonOceanRegistry.ActiveProvider;
            _oceanKinematicsProvider = provider;
            if (provider != null &&
                provider.IsAvailable &&
                _oceanFlowSamplePositions.IsCreated &&
                _oceanFlowSampleResults.IsCreated &&
                _oceanFlowSamplePositions.Length > 0 &&
                _oceanFlowSampleResults.Length > 0)
            {
                _oceanFlowSamplePositions[0] = samplePositionWS;
                if (provider.GetSurfaceFlow(_oceanFlowSamplePositions, 1, 1f, _oceanFlowSampleResults))
                    return _oceanFlowSampleResults[0];
            }

            return fluidEngine != null ? fluidEngine.CurrentVector : Vector3.zero;
        }

        private void CreateWakeTrailResources()
        {
            if (_wakeTrailDisabled)
                return;

            if (_wakeTrailRead == null)
                _wakeTrailRead = CreateWakeTrailTexture("__VegetationWakeTrail_A");

            if (_wakeTrailWrite == null)
                _wakeTrailWrite = CreateWakeTrailTexture("__VegetationWakeTrail_B");

            if (!_queuedWakeTrailStampCommands.IsCreated)
                _queuedWakeTrailStampCommands = new NativeArray<WakeTrailStampCommand>(WakeTrailStampCommandCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<WakeTrailStampCommand>[4] - queued vegetation wake-trail stamps for single compute dispatch - owner: FloraInteractionManager

            if (_wakeTrailStampCommandBuffer == null)
                _wakeTrailStampCommandBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<WakeTrailStampCommand>(WakeTrailStampCommandCapacity); // COLD ALLOC: GraphicsBuffer[4] - queued vegetation wake-trail stamp buffer for single compute dispatch - owner: FloraInteractionManager

            TryAutoAssignWakeTrailSimulationCompute();
            if (_wakeTrailSimulationCompute == null)
            {
                Debug.LogError("[FloraInteractionManager] Missing wake trail compute shader. Expected Hecton_VegetationWakeTrailSim.compute.", this);
                _wakeTrailDisabled = true;
                PublishWakeTrailGlobals();
                return;
            }

            if (_wakeTrailSimulationKernel < 0)
                _wakeTrailSimulationKernel = _wakeTrailSimulationCompute.FindKernel("SimulateWakeTrail");

            RefreshWakeTrailWorldRect(Vector3.zero, forceClear: true);
            PublishWakeTrailGlobals();
        }

        private void ReleaseWakeTrailResources()
        {
            ReleaseWakeTrailTexture(ref _wakeTrailRead);
            ReleaseWakeTrailTexture(ref _wakeTrailWrite);

            if (_wakeTrailStampCommandBuffer != null)
            {
                _wakeTrailStampCommandBuffer.Release();
                _wakeTrailStampCommandBuffer = null;
            }

            if (_queuedWakeTrailStampCommands.IsCreated)
                _queuedWakeTrailStampCommands.Dispose();

            _pendingWakeTrailScrollUv = Vector2.zero;
            _queuedWakeTrailStampCount = 0;
            _lastWakeTrailDispatchFrame = -1;

            Shader.SetGlobalFloat(_WakeTrailActiveId, 0f);
        }

        private void UpdateWakeTrail(Vector3 playerPosition, Vector3 playerVelocity, float deltaTime)
        {
            if (_wakeTrailDisabled)
                return;

            CreateWakeTrailResources();
            if (_wakeTrailRead == null || _wakeTrailWrite == null || _wakeTrailSimulationCompute == null || _wakeTrailStampCommandBuffer == null)
            {
                PublishWakeTrailGlobals();
                return;
            }

            RefreshWakeTrailWorldRect(playerPosition, forceClear: false);

            bool wrotePass = false;
            float fade = Mathf.Max(0f, deltaTime / _wakeTrailFadeSeconds);
            float strongestStamp = 0f;

            float playerSpeed = playerVelocity.magnitude;
            if (playerSpeed >= _wakeTrailPlayerMinSpeed)
            {
                QueueWakeTrailStamp(
                    playerPosition,
                    playerVelocity,
                    _wakeTrailBaseRadius,
                    Mathf.Clamp(_wakeTrailMinLength + playerSpeed * _wakeTrailVelocityToLength, _wakeTrailMinLength, _wakeTrailMaxLength),
                    Mathf.Clamp01(_wakeTrailPlayerStrength));
                wrotePass = true;
                strongestStamp = Mathf.Max(strongestStamp, _wakeTrailPlayerStrength);
            }

            float scooterSpeed = _smoothedScooterVelocity.magnitude;
            if (_hasActiveScooterWake && scooterSpeed >= _wakeTrailScooterMinSpeed)
            {
                QueueWakeTrailStamp(
                    _lastPublishedScooterWakePosition,
                    _smoothedScooterVelocity,
                    _wakeTrailBaseRadius * 1.15f,
                    Mathf.Clamp(_wakeTrailMinLength + scooterSpeed * (_wakeTrailVelocityToLength * 1.7f), _wakeTrailMinLength * 1.25f, _wakeTrailMaxLength),
                    Mathf.Clamp01(_wakeTrailScooterStrength));
                wrotePass = true;
                strongestStamp = Mathf.Max(strongestStamp, _wakeTrailScooterStrength);
            }

            if (wrotePass || _wakeTrailEnergy > 0.0001f || _pendingWakeTrailScrollUv.sqrMagnitude > 0.0000001f)
                ExecuteWakeTrailSimulation(fade);

            _wakeTrailEnergy = Mathf.Max(0f, wrotePass ? Mathf.Max(_wakeTrailEnergy - fade, strongestStamp) : (_wakeTrailEnergy - fade));
            PublishWakeTrailGlobals();
        }

        private void RefreshWakeTrailWorldRect(Vector3 anchorPosition, bool forceClear)
        {
            if (_wakeTrailRead == null || _wakeTrailWrite == null)
                return;

            float desiredWorldSize = Mathf.Max(64f, _wakeTrailWorldSize);
            float snapStride = ResolveWakeTrailSnapStride(desiredWorldSize);
            Vector2 desiredCenterXZ = QuantizeWakeTrailCenter(new Vector2(anchorPosition.x, anchorPosition.z), snapStride);

            bool mustClear = forceClear || _wakeTrailRuntimeWorldSize <= 0f || Mathf.Abs(desiredWorldSize - _wakeTrailRuntimeWorldSize) > 0.001f;
            Vector2 centerDelta = desiredCenterXZ - _wakeTrailCenterXZ;
            if (!mustClear && centerDelta.sqrMagnitude <= 0.000001f)
                return;

            _wakeTrailCenterXZ = desiredCenterXZ;
            _wakeTrailRuntimeWorldSize = desiredWorldSize;
            float halfSize = desiredWorldSize * 0.5f;
            _wakeTrailWorldRect = new Vector4(
                desiredCenterXZ.x - halfSize,
                desiredCenterXZ.y - halfSize,
                1f / Mathf.Max(desiredWorldSize, 0.001f),
                1f / Mathf.Max(desiredWorldSize, 0.001f));

            if (mustClear)
            {
                ClearWakeTrailTextures();
                return;
            }

            QueueWakeTrailScroll(centerDelta);
        }

        private void QueueWakeTrailStamp(
            Vector3 positionWS,
            Vector3 directionWS,
            float radiusWS,
            float lengthWS,
            float strength)
        {
            if (!_queuedWakeTrailStampCommands.IsCreated || _queuedWakeTrailStampCount >= WakeTrailStampCommandCapacity)
                return;

            Vector2 uvCenter = new Vector2(
                (positionWS.x - _wakeTrailWorldRect.x) * _wakeTrailWorldRect.z,
                (positionWS.z - _wakeTrailWorldRect.y) * _wakeTrailWorldRect.w);
            Vector2 directionXZ = new Vector2(directionWS.x, directionWS.z);
            float directionMagnitude = directionWS.magnitude;
            float verticalImpulse = directionMagnitude > 0.0001f
                ? Mathf.Clamp01(Mathf.Abs(directionWS.y) / directionMagnitude) * Mathf.Clamp01(directionMagnitude * 0.12f)
                : 0f;
            if (directionXZ.sqrMagnitude <= 0.0001f)
                directionXZ = Vector2.up;
            directionXZ.Normalize();

            float uvRadius = radiusWS * _wakeTrailWorldRect.z;
            float uvLength = lengthWS * _wakeTrailWorldRect.z;

            _queuedWakeTrailStampCommands[_queuedWakeTrailStampCount] = new WakeTrailStampCommand
            {
                UvEllipse = new Vector4(uvCenter.x, uvCenter.y, uvRadius, uvLength),
                DirectionStrengthVertical = new Vector4(directionXZ.x, directionXZ.y, Mathf.Clamp01(strength), verticalImpulse)
            };
            _queuedWakeTrailStampCount++;
        }

        private void ExecuteWakeTrailSimulation(float fade)
        {
            if (_wakeTrailSimulationCompute == null ||
                _wakeTrailSimulationKernel < 0 ||
                _wakeTrailRead == null ||
                _wakeTrailWrite == null ||
                _wakeTrailStampCommandBuffer == null ||
                _lastWakeTrailDispatchFrame == Time.frameCount)
            {
                return;
            }

            if (_queuedWakeTrailStampCount > 0 && _queuedWakeTrailStampCommands.IsCreated)
                GraphicsBufferUploadUtility.UploadNativeArray(_wakeTrailStampCommandBuffer, _queuedWakeTrailStampCommands, _queuedWakeTrailStampCount);

            _wakeTrailSimulationCompute.SetTexture(_wakeTrailSimulationKernel, _WakeTrailSourceId, _wakeTrailRead);
            _wakeTrailSimulationCompute.SetTexture(_wakeTrailSimulationKernel, _WakeTrailResultId, _wakeTrailWrite);
            _wakeTrailSimulationCompute.SetBuffer(_wakeTrailSimulationKernel, _WakeTrailStampCommandsId, _wakeTrailStampCommandBuffer);
            _wakeTrailSimulationCompute.SetInt(_WakeTrailStampCountId, _queuedWakeTrailStampCount);
            _wakeTrailSimulationCompute.SetVector(_WakeTrailScrollUvOffsetId, new Vector4(_pendingWakeTrailScrollUv.x, _pendingWakeTrailScrollUv.y, 0f, 0f));
            _wakeTrailSimulationCompute.SetFloat(_WakeTrailFadeDeltaId, Mathf.Max(0f, fade));
            _wakeTrailSimulationCompute.SetFloat(_WakeTrailDiffusionId, _wakeTrailDiffusion);
            _wakeTrailSimulationCompute.SetFloat(_WakeTrailWaveStrengthId, _wakeTrailWaveStrength);
            _wakeTrailSimulationCompute.SetFloat(_WakeTrailDampingId, _wakeTrailWaveDamping);
            _wakeTrailSimulationCompute.SetFloat(_WakeTrailCurlStrengthId, _wakeTrailCurlStrength);
            _wakeTrailSimulationCompute.SetFloat(_WakeTrailSimulationTimeId, Time.unscaledTime);
            _wakeTrailSimulationCompute.SetVector(
                _WakeTrailTexelSizeId,
                new Vector4(
                    1f / Mathf.Max(_wakeTrailRuntimeResolution, 1),
                    1f / Mathf.Max(_wakeTrailRuntimeResolution, 1),
                    _wakeTrailRuntimeResolution,
                    _wakeTrailRuntimeResolution));

            int groupCount = Mathf.CeilToInt(_wakeTrailRuntimeResolution / (float)WakeTrailThreadGroupSize);
            _wakeTrailSimulationCompute.Dispatch(_wakeTrailSimulationKernel, Mathf.Max(1, groupCount), Mathf.Max(1, groupCount), 1);

            RenderTexture temp = _wakeTrailRead;
            _wakeTrailRead = _wakeTrailWrite;
            _wakeTrailWrite = temp;
            _lastWakeTrailDispatchFrame = Time.frameCount;
            _pendingWakeTrailScrollUv = Vector2.zero;
            _queuedWakeTrailStampCount = 0;
        }

        private void QueueWakeTrailScroll(Vector2 centerDelta)
        {
            if (_wakeTrailRead == null || _wakeTrailWrite == null)
                return;

            float uvOffsetX = centerDelta.x / Mathf.Max(_wakeTrailRuntimeWorldSize, 0.001f);
            float uvOffsetY = centerDelta.y / Mathf.Max(_wakeTrailRuntimeWorldSize, 0.001f);
            if (Mathf.Abs(uvOffsetX) >= 1f || Mathf.Abs(uvOffsetY) >= 1f)
            {
                ClearWakeTrailTextures();
                return;
            }

            _pendingWakeTrailScrollUv.x += uvOffsetX;
            _pendingWakeTrailScrollUv.y += uvOffsetY;
        }

        private void ClearWakeTrailTextures()
        {
            if (_wakeTrailRead == null || _wakeTrailWrite == null)
                return;

            RenderTexture active = RenderTexture.active;
            RenderTexture.active = _wakeTrailRead;
            GL.Clear(false, true, Color.clear);
            RenderTexture.active = _wakeTrailWrite;
            GL.Clear(false, true, Color.clear);
            RenderTexture.active = active;
            _wakeTrailEnergy = 0f;
            _pendingWakeTrailScrollUv = Vector2.zero;
            _queuedWakeTrailStampCount = 0;
        }

        private void PublishWakeTrailGlobals()
        {
            if (_wakeTrailDisabled || _wakeTrailRead == null)
            {
                Shader.SetGlobalFloat(_WakeTrailActiveId, 0f);
                Shader.SetGlobalFloat(_ShallowWaterFieldActiveId, 0f);
                return;
            }

            Shader.SetGlobalTexture(_WakeTrailTextureId, _wakeTrailRead);
            Shader.SetGlobalVector(_WakeTrailWorldRectId, _wakeTrailWorldRect);
            Shader.SetGlobalFloat(_WakeTrailActiveId, _wakeTrailRuntimeWorldSize > 0f ? 1f : 0f);
            Shader.SetGlobalTexture(_ShallowWaterFieldTextureId, _wakeTrailRead);
            Shader.SetGlobalVector(_ShallowWaterFieldWorldRectId, _wakeTrailWorldRect);
            Shader.SetGlobalFloat(_ShallowWaterFieldActiveId, _wakeTrailRuntimeWorldSize > 0f ? 1f : 0f);
            Shader.SetGlobalVector(
                _ShallowWaterFieldTexelSizeId,
                new Vector4(
                    1f / Mathf.Max(_wakeTrailRuntimeResolution, 1),
                    1f / Mathf.Max(_wakeTrailRuntimeResolution, 1),
                    _wakeTrailRuntimeResolution,
                    _wakeTrailRuntimeResolution));
        }

        private RenderTexture CreateWakeTrailTexture(string textureName)
        {
            RenderTexture texture = new RenderTexture(_wakeTrailRuntimeResolution, _wakeTrailRuntimeResolution, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
            {
                name = textureName,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
                autoGenerateMips = false,
                enableRandomWrite = true,
                hideFlags = HideFlags.HideAndDontSave
            }; // COLD ALLOC: RenderTexture[1] - persistent vegetation wake trail ping-pong target - owner: FloraInteractionManager
            texture.Create();
            return texture;
        }

        private static void ReleaseWakeTrailTexture(ref RenderTexture texture)
        {
            if (texture == null)
                return;

            texture.Release();
            Destroy(texture);
            texture = null;
        }

        private float ResolveWakeTrailSnapStride(float worldSize)
        {
            float pixelWorldSize = worldSize / Mathf.Max(_wakeTrailRuntimeResolution, 1);
            return pixelWorldSize * Mathf.Max(0.1f, _wakeTrailCenterSnapPixelStride);
        }

        private static Vector2 QuantizeWakeTrailCenter(Vector2 centerXZ, float stride)
        {
            if (stride <= 0.0001f)
                return centerXZ;

            return new Vector2(
                Mathf.Round(centerXZ.x / stride) * stride,
                Mathf.Round(centerXZ.y / stride) * stride);
        }

        private static Color ResolveAmbientColor()
        {
            switch (RenderSettings.ambientMode)
            {
                case AmbientMode.Flat:
                    return RenderSettings.ambientLight;
                case AmbientMode.Trilight:
                    return RenderSettings.ambientEquatorColor;
                default:
                    return RenderSettings.ambientSkyColor;
            }
        }

        private void ApplyRuntimeOffsetToCachedState(Vector3 runtimeOffset)
        {
            _smoothPosition += runtimeOffset;
            if (_hasLastPlayerPosition)
                _lastPlayerPosition += runtimeOffset;

            if (_hasSmoothedScooterPosition)
                _smoothedScooterPosition += runtimeOffset;

            if (_hasActiveScooterWake)
                _lastPublishedScooterWakePosition += runtimeOffset;

            if (_interactionPoints != null &&
                _interactionBuffer != null &&
                _lastPublishedInteractionCount > 0)
            {
                int interactionCount = Mathf.Min(_lastPublishedInteractionCount, _interactionPoints.Length);
                for (int i = 0; i < interactionCount; i++)
                {
                    Vector4 positionRadius = _interactionPoints[i].PositionRadius;
                    positionRadius.x += runtimeOffset.x;
                    positionRadius.y += runtimeOffset.y;
                    positionRadius.z += runtimeOffset.z;
                    _interactionPoints[i].PositionRadius = positionRadius;
                }

                GraphicsBufferUploadUtility.UploadArray(_interactionBuffer, _interactionPoints, interactionCount);
            }

            if (_flowFieldResolution > 0 || _flowFieldCellSize > 0f)
            {
                _flowFieldCenterWS += runtimeOffset;
                _lastUploadedFlowFieldCenterWS += runtimeOffset;
                PublishFlowFieldGlobals();
            }

            if (_wakeTrailWorldRect.z > 0f && _wakeTrailWorldRect.w > 0f)
            {
                _wakeTrailCenterXZ += new Vector2(runtimeOffset.x, runtimeOffset.z);
                _wakeTrailWorldRect.x += runtimeOffset.x;
                _wakeTrailWorldRect.y += runtimeOffset.z;
                PublishWakeTrailGlobals();
            }

            PublishEnvironmentGlobals(_playerTransform != null ? _playerTransform.position : _smoothPosition);
        }

        private void ResetInteractionGlobals()
        {
            Shader.SetGlobalVector(_PropWashPosId, Vector4.zero);
            Shader.SetGlobalFloat(_PropWashForceId, 0f);
            Shader.SetGlobalInt(_InteractionCountId, 0);
            Shader.SetGlobalVector(_PlayerRuntimePositionId, Vector4.zero);
            Shader.SetGlobalVector(_PlayerFloraInteractionParamsId, Vector4.zero);
            Shader.SetGlobalVector(_GlobalOceanFlowId, Vector4.zero);
            Shader.SetGlobalVector(_VegetationCurrentVectorId, Vector4.zero);
            Shader.SetGlobalFloat(_VegetationCurrentStrengthId, 0f);
            Shader.SetGlobalVector(_MarineSnowFlowFieldCenterCellSizeId, Vector4.zero);
            Shader.SetGlobalInt(_FloraFlowFieldResolutionId, 0);
            _lastPublishedInteractionCount = 0;
            _lastPublishedPlayerVelocity = Vector3.zero;
            _lastPublishedScooterWakePosition = Vector3.zero;
            _smoothedPlayerVelocity = Vector3.zero;
            _smoothedPlayerVelocityDamp = Vector3.zero;
            _smoothedScooterVelocity = Vector3.zero;
            _smoothedScooterVelocityDamp = Vector3.zero;
            _smoothedScooterPositionDamp = Vector3.zero;
            _hasSmoothedScooterPosition = false;
            _hasActiveScooterWake = false;
            _wakeTrailEnergy = 0f;
            _playerSedimentCooldownRemaining = 0f;
            _scooterSedimentCooldownRemaining = 0f;

            if (_interactionBuffer != null)
                Shader.SetGlobalBuffer(_InteractionBufferId, _interactionBuffer);

            ClearWakeTrailTextures();
            PublishWakeTrailGlobals();
        }

        private void ReleaseFlowFieldBuffer()
        {
            if (_flowFieldBuffer == null)
                return;

            _flowFieldBuffer.Release();
            _flowFieldBuffer = null;
        }

        private void TryRegister()
        {
            if (_isRegistered)
                return;


            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _isRegistered = true;
        }

        private void TryUnregister()
        {
            if (!_isRegistered)
                return;

                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);

            _isRegistered = false;
        }

        private static long EstimateGraphicsBufferBytes(GraphicsBuffer buffer)
        {
            return buffer != null ? (long)buffer.count * buffer.stride : 0L;
        }

        private static long EstimateRenderTextureBytes(RenderTexture texture)
        {
            if (texture == null)
                return 0L;

            int bytesPerPixel = 4;
            return (long)texture.width * texture.height * bytesPerPixel;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            TryAutoAssignWakeTrailSimulationCompute();
        }

        private void TryAutoAssignWakeTrailSimulationCompute()
        {
            if (_wakeTrailSimulationCompute == null)
                _wakeTrailSimulationCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(WakeTrailSimulationComputeAssetPath);
        }
#else
        private void TryAutoAssignWakeTrailSimulationCompute()
        {
        }
#endif
    }
}

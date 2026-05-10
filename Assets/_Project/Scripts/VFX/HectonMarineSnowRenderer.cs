using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Optimization;
using Hecton8.Physics;
using Hecton8.VFX;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Environment
{
    /// <summary>
    /// GPU-resident camera-local marine snow renderer driven by the authoritative ecosystem flow field.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HectonMarineSnowRenderer : MonoBehaviour, ITickable, IUpdatable, IOriginShiftListener
    {
        private const float BiolumeSurgeDurationSeconds = 4f;
        private const int ThreadGroupSize = 64;
        private const int MaxMarineSnowParticleCapacity = 32768;
        private const int ParticleStride = 64;
        private const int FrameConstantsStride = 112;
        private const int ParticleFlagBubble = 1 << 0;
        private const int ParticleFlagDebris = 1 << 2;
        private const int ParticleFlagSnow = 1 << 3;
        private const float ActiveDensityEpsilon = 0.0001f;
        private const float ShaderVectorPublishEpsilon = 0.0001f;
        private const float ExternalGpuBindingColdTickSeconds = 0.1f;
        private static readonly Vector4 DepthCollisionParams = new Vector4(15f, 0.25f, 0.5f, 0f);
        private static readonly Vector4 DefaultFlowSynchronyParams = new Vector4(1f, 0.26f, 0f, 0f);
        private static readonly Vector4 DisabledTerrainHeightScale = new Vector4(0f, 0f, 0f, 0f);
        private static readonly Vector4 DefaultPropwashParams = new Vector4(2f, 0.08f, 0.025f, 1f);
        private static readonly Vector4 InvalidVector = new Vector4(float.NaN, float.NaN, float.NaN, float.NaN);
        private static readonly Matrix4x4 IdentityMatrix = Matrix4x4.identity;
        private static readonly Matrix4x4 InvalidMatrix = new Matrix4x4(InvalidVector, InvalidVector, InvalidVector, InvalidVector);
        private static readonly Vector3[] QuadMeshVertices =
        {
            new Vector3(-1f, -1f, 0f),
            new Vector3(-1f, 1f, 0f),
            new Vector3(1f, 1f, 0f),
            new Vector3(-1f, -1f, 0f),
            new Vector3(1f, 1f, 0f),
            new Vector3(1f, -1f, 0f)
        }; // COLD ALLOC: Vector3[6] - immutable marine-snow indirect quad vertices - owner: HectonMarineSnowRenderer
        private static readonly int[] QuadMeshIndices =
        {
            0, 1, 2, 3, 4, 5
        }; // COLD ALLOC: int[6] - immutable marine-snow indirect quad indices - owner: HectonMarineSnowRenderer

        [StructLayout(LayoutKind.Sequential)]
        private struct ParticleGpuData
        {
            public Vector3 PositionWS;
            public float Life;
            public Vector3 VelocityWS;
            public float Size;
            public Vector3 PreviousPositionWS;
            public uint Flags;
            public Vector2 Uv;
            public Vector2 Pad;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FrameConstantsData
        {
            public Vector4 CameraPositionTime;
            public Vector4 CameraRightDeltaTime;
            public Vector4 CameraUpDensity;
            public Vector4 FlowFieldCenterCellSize;
            public Vector4 ShellParams;
            public Vector4 MetaParams;
            public Vector4 CameraVelocityStretch;
        }

        private static class ShaderIds
        {
            internal static readonly int ParticlesReadId = Shader.PropertyToID("_MarineSnowParticlesRead");
            internal static readonly int ParticlesWriteId = Shader.PropertyToID("_MarineSnowParticlesWrite");
            internal static readonly int ParticlesRenderId = Shader.PropertyToID("_MarineSnowParticles");
            internal static readonly int VisibleParticleIndicesId = Shader.PropertyToID("_MarineSnowVisibleParticleIndices");
            internal static readonly int IndirectArgsId = Shader.PropertyToID("_MarineSnowIndirectArgs");
            internal static readonly int FlowFieldId = Shader.PropertyToID("_MarineSnowFlowField");
            internal static readonly int AbyssalFlowFieldResultId = Shader.PropertyToID("_AbyssalFlowFieldResult");
            internal static readonly int AbyssalGridResolutionId = Shader.PropertyToID("_AbyssalGridResolution");
            internal static readonly int AbyssalFlowCenterId = Shader.PropertyToID("_AbyssalFlowCenter");
            internal static readonly int AbyssalFlowSpacingId = Shader.PropertyToID("_AbyssalFlowSpacing");
            internal static readonly int FrameConstantsId = Shader.PropertyToID("_HectonMarineSnowFrame");
            internal static readonly int DriftParamsId = Shader.PropertyToID("_MarineSnowDriftParams");
            internal static readonly int FlowParamsId = Shader.PropertyToID("_MarineSnowFlowParams");
            internal static readonly int BubbleParamsId = Shader.PropertyToID("_MarineSnowBubbleParams");
            internal static readonly int TerrainHeightTextureId = Shader.PropertyToID("_MarineSnowTerrainHeightTexture");
            internal static readonly int TerrainHeightRectId = Shader.PropertyToID("_MarineSnowTerrainHeightRect");
            internal static readonly int TerrainHeightScaleId = Shader.PropertyToID("_MarineSnowTerrainHeightScale");
            internal static readonly int PropwashParamsId = Shader.PropertyToID("_MarineSnowPropwashParams");
            internal static readonly int FlowSynchronyParamsId = Shader.PropertyToID("_HectonFlowSynchronyParams");
            internal static readonly int RenderParamsId = Shader.PropertyToID("_MarineSnowRenderParams");
            internal static readonly int TintId = Shader.PropertyToID("_MarineSnowTint");
            internal static readonly int EmissionParamsId = Shader.PropertyToID("_MarineSnowEmissionParams");
            internal static readonly int ViewProjectionId = Shader.PropertyToID("_MarineSnowViewProjection");
            internal static readonly int ViewMatrixId = Shader.PropertyToID("_MarineSnowViewMatrix");
            internal static readonly int CaveVoxelSdfTexId = Shader.PropertyToID("_HectonCaveVoxelSdfTex");
            internal static readonly int CaveVoxelActiveId = Shader.PropertyToID("_HectonCaveVoxelActive");
            internal static readonly int CaveVoxelWorldToLocalId = Shader.PropertyToID("_HectonCaveVoxelWorldToLocal");
            internal static readonly int CaveVoxelHalfExtentsId = Shader.PropertyToID("_HectonCaveVoxelHalfExtents");
            internal static readonly int SubmarineWashSphereId = Shader.PropertyToID("_HectonSubmarineWashSphere");
            internal static readonly int SubmarineWashVelocityId = Shader.PropertyToID("_HectonSubmarineWashVelocity");
            internal static readonly int ZBufferParamsId = Shader.PropertyToID("_MarineSnowZBufferParams");
            internal static readonly int DepthTextureTexelSizeId = Shader.PropertyToID("_MarineSnowDepthTextureTexelSize");
            internal static readonly int DepthCollisionParamsId = Shader.PropertyToID("_MarineSnowDepthCollisionParams");
            internal static readonly int CameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture");
            internal static readonly int GlobalZBufferParamsId = Shader.PropertyToID("_ZBufferParams");
            internal static readonly int SonarGlowTextureId = Shader.PropertyToID("_HectonMarineSnowSonarGlowTex");
            internal static readonly int SonarGlowResultId = Shader.PropertyToID("_HectonMarineSnowSonarGlowResult");
            internal static readonly int SonarGlowTexelSizeId = Shader.PropertyToID("_HectonMarineSnowSonarGlowTexelSize");
            internal static readonly int SonarGlowParamsId = Shader.PropertyToID("_HectonMarineSnowSonarGlowParams");
            internal static readonly int SonarRevealExpireTimeId = Shader.PropertyToID("_SonarRevealExpireTime");
        }

        [Header("â”€â”€ References â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Camera transform that owns the marine snow shell. Bind this to the runtime main camera.")]
        [SerializeField] private Transform targetCamera;
        [Tooltip("Compute shader responsible for marine-snow simulation.")]
        [SerializeField] private ComputeShader marineSnowCompute;
        [Tooltip("Dedicated material used by the direct marine-snow billboard draw.")]
        [SerializeField] private Material marineSnowMaterial;

        [Tooltip("Optional fluid emission profile that overrides drag, buoyancy, and turbulence coefficients per particle class.")]
        [SerializeField] private VFXEmissionProfile emissionProfile;

        [Tooltip("Fluid class emitted by this GPU particle owner.")]
        [SerializeField] private VFXEmissionProfile.FluidType fluidType = VFXEmissionProfile.FluidType.Snow;

        [Header("â”€â”€ Population â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Maximum compute-simulated plankton particles. Hard-capped to the MX350 marine-snow budget.")]
        [SerializeField, Range(512, MaxMarineSnowParticleCapacity)] private int maxParticles = MaxMarineSnowParticleCapacity;
        [Tooltip("Empty safety radius around the camera to avoid particles clipping through the visor.")]
        [SerializeField, Range(0.1f, 4f)] private float innerRadius = 0.8f;
        [Tooltip("Outer shell radius. Particles respawn to the ring when they drift beyond this distance.")]
        [SerializeField, Range(4f, 32f)] private float outerRadius = 18f;
        [Tooltip("Vertical span of the marine-snow shell relative to the target camera.")]
        [SerializeField] private Vector2 verticalSpan = new Vector2(-10f, 8f);

        [Header("â”€â”€ Drift â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Minimum base descent speed for marine snow.")]
        [SerializeField, Range(0.005f, 0.08f)] private float descentMinSpeed = 0.015f;
        [Tooltip("Maximum base descent speed for marine snow.")]
        [SerializeField, Range(0.005f, 0.08f)] private float descentMaxSpeed = 0.04f;
        [Tooltip("Horizontal wander amplitude used before anisotropic drag clamps it back into the current.")]
        [SerializeField, Range(0f, 0.02f)] private float wanderStrength = 0.008f;
        [Tooltip("Base drag coefficient for the mandated anisotropic-drag attenuation.")]
        [SerializeField, Range(0.01f, 0.5f)] private float baseDragCoefficient = 0.15f;

        [Header("â”€â”€ Flow Coupling â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("How strongly particles chase the authoritative ecosystem current before anisotropic drag is applied.")]
        [SerializeField, Range(0f, 1f)] private float flowBlend = 0.18f;
        [Tooltip("Extra flow-coupling gain injected by denser water states.")]
        [SerializeField, Range(0f, 1f)] private float densityBiasFlowGain = 0.08f;
        [Tooltip("How often the CPU is allowed to upload the current flow-field snapshot to the GPU.")]
        [SerializeField, Range(0.05f, 2f)] private float flowFieldUploadInterval = 0.25f;
        [Tooltip("If the flow-field center shifts by more than this many cells, force an upload immediately.")]
        [SerializeField, Range(0.1f, 4f)] private float flowFieldRecenterThresholdCells = 0.5f;

        [Header("â”€â”€ Rendering â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Minimum world-space snow billboard size.")]
        [SerializeField, Range(0.0005f, 0.02f)] private float particleSizeMin = 0.0035f;
        [Tooltip("Maximum world-space snow billboard size.")]
        [SerializeField, Range(0.0005f, 0.03f)] private float particleSizeMax = 0.009f;
        [Tooltip("Base tint for the marine-snow quads.")]
        [SerializeField] private Color particleTint = new Color(0.54f, 0.61f, 0.58f, 0.55f);
        [Tooltip("Maximum resolved particle alpha before density scaling.")]
        [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.55f;
        [Tooltip("Softness of the particle radial falloff.")]
        [SerializeField, Range(0.5f, 8f)] private float softness = 3.2f;
        [Tooltip("Distance fade for the camera-local shell.")]
        [SerializeField, Range(4f, 48f)] private float maxViewDistance = 18f;
        [Tooltip("Shadow-casting mode for the marine-snow particle draw.")]
        [SerializeField] private ShadowCastingMode shadowCastingMode = ShadowCastingMode.Off;

        [Header("Sprint Speed Lines")]
        [Tooltip("Camera/player velocity where marine snow stretches into full sprint speed lines.")]
        [SerializeField, Range(1f, 18f)] private float speedLineFullVelocity = 8f;
        [Tooltip("Maximum billboard elongation applied to plankton at full sprint velocity.")]
        [SerializeField, Range(1f, 18f)] private float speedLineMaxStretch = 7.5f;
        [Tooltip("Blend sharpness for speed-line stretch so brief frame spikes do not flash the whole shell.")]
        [SerializeField, Range(0.1f, 16f)] private float speedLineResponseSharpness = 7f;

        [Header("Ã¢â€â‚¬Ã¢â€â‚¬ Biolume Surge Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬")]
        [Tooltip("Temporary particle-population multiplier applied while the global biolume surge bit remains active.")]
        [SerializeField, Range(1f, 3f)] private float biolumeSurgeParticleMultiplier = 1.75f;
        [Tooltip("Temporary turbulence multiplier applied while the global biolume surge bit remains active.")]
        [SerializeField, Range(1f, 4f)] private float biolumeSurgeTurbulenceMultiplier = 2f;

        [Header("Sonar Glow")]
        [Tooltip("Screen-space render-scale used by the low-resolution sonar-reactive plankton glow splatmap.")]
        [SerializeField, Range(0.1f, 0.75f)] private float sonarGlowRenderScale = 0.35f;
        [Tooltip("Simulation-side intensity scale used when particles intersect the active sonar pulse.")]
        [SerializeField, Range(0f, 8f)] private float sonarGlowIntensity = 2.2f;
        [Tooltip("Final underwater composite strength for sonar-reactive plankton glow.")]
        [SerializeField, Range(0f, 4f)] private float sonarGlowCompositeStrength = 1.15f;

        private readonly FrameConstantsData[] _frameConstantsUpload = new FrameConstantsData[1]; // COLD ALLOC: FrameConstantsData[1] â€” reusable per-frame constant-buffer upload cache â€” owner: HectonMarineSnowRenderer

        private ParticleGpuData[] _bootstrapParticles;
        private GraphicsBuffer _particleBufferA;
        private GraphicsBuffer _particleBufferB;
        private GraphicsBuffer _flowFieldBuffer;
        private GraphicsBuffer _emptyFlowFieldBuffer;
        private GraphicsBuffer _frameConstantsBuffer;
        private GraphicsBuffer _visibleParticleIndexBuffer;
        private GraphicsBuffer _indirectArgsBuffer;
        private GraphicsBuffer _emptyAbyssalFlowBuffer;
        private GraphicsBuffer _boundAbyssalFlowBuffer;
        private Camera _targetCameraComponent;
        private Mesh _quadMesh;
        private Bounds _drawBounds;
        private int _kernelIndex = -1;
        private int _clearVisibleKernel = -1;
        private int _sonarGlowClearKernel = -1;
        private int _sonarGlowAccumulateKernel = -1;
        private int _frameParity;
        private int _flowFieldResolution;
        private float _flowFieldCellSize;
        private float _flowFieldUploadTimer;
        private float _simulationTime;
        private int _activeParticleCount;
        private bool _registeredTick;
        private bool _buffersReady;
        private bool _staticBindingsDirty = true;
        private bool _underwaterActive;
        private float _biolumeSurgeTimer;
        private float _visualDensityScale;
        private float _lastDepth;
        private float _lastLightFactor = 1f;
        private float _lastSubmergeImpulse;
        private float _bubbleTrailMovement01;
        private float _bubbleTrailExhale01;
        private float _speedLineIntensity;
        private bool _hasLastCameraPositionWS;
        private Vector3 _flowFieldCenterWS;
        private Vector3 _lastUploadedFlowFieldCenterWS;
        private Vector3 _lastCameraPositionWS;
        private RenderTexture _sonarGlowTexture;
        private int _sonarGlowWidth;
        private int _sonarGlowHeight;
        private Vector4 _lastPublishedSonarGlowTexelSize;
        private Vector4 _lastPublishedSonarGlowParams;
        private Texture _lastPublishedSonarGlowTexture;
        private Texture _boundCameraDepthTexture;
        private Texture _boundTerrainHeightTexture;
        private Texture _boundCaveSdfTexture;
        private Texture3D _emptyCaveSdfTexture;
        private Vector4 _boundAbyssalGridResolution;
        private Vector4 _boundAbyssalFlowCenter;
        private Vector4 _boundAbyssalFlowSpacing;
        private Vector4 _boundCaveVoxelHalfExtents;
        private Vector4 _boundTerrainHeightRect;
        private Vector4 _boundTerrainHeightScale;
        private Vector4 _boundSubmarineWashSphere;
        private Vector4 _boundSubmarineWashVelocity;
        private Vector4 _boundPropwashParams;
        private GraphicsBuffer _boundSimulationReadBuffer;
        private GraphicsBuffer _boundSimulationWriteBuffer;
        private GraphicsBuffer _boundSimulationFlowFieldBuffer;
        private GraphicsBuffer _boundSimulationVisibleParticleIndexBuffer;
        private GraphicsBuffer _boundSimulationIndirectArgsBuffer;
        private GraphicsBuffer _boundMaterialParticlesBuffer;
        private GraphicsBuffer _boundMaterialVisibleParticleIndexBuffer;
        private GraphicsBuffer _boundSonarGlowParticlesWriteBuffer;
        private Texture _boundSonarGlowClearTexture;
        private Texture _boundSonarGlowAccumulateTexture;
        private Vector4 _boundEmissionParams = InvalidVector;
        private Vector4 _boundBubbleParams = InvalidVector;
        private Vector4 _boundFlowSynchronyParams = InvalidVector;
        private Vector4 _boundZBufferParams = InvalidVector;
        private Vector4 _boundDepthTextureTexelSize = InvalidVector;
        private Vector4 _boundDepthCollisionParams = InvalidVector;
        private Vector4 _boundSonarGlowTexelSize = InvalidVector;
        private Vector4 _boundSonarGlowParams = InvalidVector;
        private Matrix4x4 _boundViewProjection = InvalidMatrix;
        private Matrix4x4 _boundViewMatrix = InvalidMatrix;
        private Matrix4x4 _boundCaveVoxelWorldToLocal = IdentityMatrix;
        private float _boundCaveVoxelActive = -1f;
        private float _externalGpuBindingColdTickTimer;
        private bool _externalGpuBindingsDirty = true;
        private bool _sonarGlowGlobalsDirty = true;
        [SerializeField] private int _debugActiveParticleCount;
        [SerializeField] private float _debugAdaptiveRenderScale = 1f;
        [SerializeField] private float _debugAdaptiveBudgetScale = 1f;
        [SerializeField] private VRAMMonitor.VRAMPressureState _debugAdaptiveVramPressureState;
        [SerializeField] private float _debugBiolumeSurgeBlend;

        private readonly Vector4[] _emptyAbyssalFlowUpload = new Vector4[1]; // COLD ALLOC: Vector4[1] - zeroed fallback abyssal-flow buffer upload cache - owner: HectonMarineSnowRenderer
        private readonly float2[] _emptyFlowFieldUpload = new float2[1]; // COLD ALLOC: float2[1] - zeroed fallback ecosystem flow-field buffer upload cache - owner: HectonMarineSnowRenderer
        /// <summary>
        /// True when the compute path has all required resources and can replace the fallback particle system.
        /// </summary>
        public bool IsOperational => _buffersReady && marineSnowCompute != null && marineSnowMaterial != null && _kernelIndex >= 0;

        private void OnEnable()
        {
            ResolveTargetCamera();
            HectonFloatingOrigin.RegisterListener(this);
            TryRegisterTick();
        }

        private void OnDisable()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            SetUnderwaterState(false, 0f, 0f, 1f, 0f);
            SetBubbleTrailState(0f, 0f);
            if (_registeredTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredTick = false;
            }

            ReleaseBuffers();
        }

        private void OnDestroy()
        {
            HectonFloatingOrigin.UnregisterListener(this);
        }

        /// <summary>
        /// Binds the camera transform that owns the marine-snow shell.
        /// </summary>
        /// <param name="cameraTransform">Runtime main-camera transform.</param>
        public void BindTargetCamera(Transform cameraTransform)
        {
            targetCamera = cameraTransform;
            _targetCameraComponent = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null;
            ResetSpeedLineHistory();
        }

        /// <summary>
        /// Updates the underwater state pushed by <see cref="HectonUnderwaterVisuals"/>.
        /// </summary>
        /// <param name="active">True when underwater visuals are active.</param>
        /// <param name="densityScale">Normalized density scale derived from the underwater owner.</param>
        /// <param name="depth">Current camera depth.</param>
        /// <param name="lightFactor">Current underwater light factor.</param>
        /// <param name="submergeImpulse">Current submerge impulse amount.</param>
        public void SetUnderwaterState(bool active, float densityScale, float depth, float lightFactor, float submergeImpulse)
        {
            _underwaterActive = active;
            _visualDensityScale = math.saturate(densityScale);
            _lastDepth = math.max(0f, depth);
            _lastLightFactor = math.saturate(lightFactor);
            _lastSubmergeImpulse = math.saturate(submergeImpulse);
            if (!active)
                ResetSpeedLineHistory();
        }

        public void SetBubbleTrailState(float movement01, float exhale01)
        {
            _bubbleTrailMovement01 = math.saturate(movement01);
            _bubbleTrailExhale01 = math.saturate(exhale01);
        }

        private void ResetSpeedLineHistory()
        {
            _speedLineIntensity = 0f;
            _hasLastCameraPositionWS = false;
            _lastCameraPositionWS = Vector3.zero;
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (!isActiveAndEnabled || shiftData.ShiftOffset.sqrMagnitude <= 0.0001f)
                return;

            Vector3 runtimeOffset = -shiftData.ShiftOffset;
            _flowFieldCenterWS += runtimeOffset;
            if (_lastUploadedFlowFieldCenterWS != Vector3.zero)
                _lastUploadedFlowFieldCenterWS += runtimeOffset;

            _flowFieldUploadTimer = 0f;
            ResetSpeedLineHistory();

            if (!_buffersReady || _bootstrapParticles == null)
                return;

            ResolveTargetCamera();
            if (targetCamera == null)
                return;

            int particleCount = ResolveConfiguredCapacity();
            BootstrapParticles(particleCount);
            GraphicsBufferUploadUtility.UploadArray(_particleBufferA, _bootstrapParticles, particleCount);
            GraphicsBufferUploadUtility.UploadArray(_particleBufferB, _bootstrapParticles, particleCount);
            _frameParity = 0;
        }

        public void Tick(float dt)
        {
            if (!enabled || marineSnowCompute == null || marineSnowMaterial == null)
                return;

            ResolveTargetCamera();
            if (targetCamera == null || _targetCameraComponent == null)
                return;

            float effectiveDensityScale = ResolveEffectiveDensityScale();
            if (effectiveDensityScale <= ActiveDensityEpsilon)
            {
                _activeParticleCount = 0;
                _debugActiveParticleCount = 0;
                PublishSonarGlowGlobals(Vector4.zero, Vector4.zero, null);
                return;
            }

            EnsureBuffers();
            if (!_buffersReady)
                return;

            UpdateBiolumeSurgeState(dt);
            _activeParticleCount = ResolveActiveParticleCount(effectiveDensityScale);
            if (_activeParticleCount <= 0)
            {
                PublishSonarGlowGlobals(Vector4.zero, Vector4.zero, null);
                return;
            }

            RefreshFlowFieldUpload(dt);
            ApplyStaticBindingsIfNeeded();
            UpdateFrameConstants(math.max(0f, dt), effectiveDensityScale);
            RefreshExternalGpuBindings(dt);
            DispatchVisibleClear();
            DispatchSimulation();
            DispatchSonarGlow();
            RenderMarineSnow();
            _frameParity ^= 1;
        }

        private float ResolveEffectiveDensityScale()
        {
            if (!_underwaterActive)
                return 0f;

            return math.saturate(
                _visualDensityScale +
                (_lastSubmergeImpulse * 0.35f) +
                (_bubbleTrailMovement01 * 0.08f) +
                (_bubbleTrailExhale01 * 0.12f));
        }

        private void UpdateBiolumeSurgeState(float dt)
        {
            IWeatherService weatherService = GlobalRegistry.Weather;
            if (weatherService != null &&
                weatherService.IsInitialized &&
                (weatherService.CurrentWeatherState & WeatherState.BiolumeSurge) != 0)
            {
                _biolumeSurgeTimer = math.max(_biolumeSurgeTimer, BiolumeSurgeDurationSeconds);
            }
            else
            {
                _biolumeSurgeTimer = math.max(0f, _biolumeSurgeTimer - math.max(0f, dt));
            }

            _debugBiolumeSurgeBlend = ResolveBiolumeSurgeBlend();
        }

        private void ResolveTargetCamera()
        {
            if (targetCamera == null)
            {
                _targetCameraComponent = GetComponentInParent<Camera>();
                targetCamera = _targetCameraComponent != null ? _targetCameraComponent.transform : null;
            }
            else if (_targetCameraComponent == null || _targetCameraComponent.transform != targetCamera)
            {
                _targetCameraComponent = targetCamera.GetComponent<Camera>();
            }
        }

        private void TryRegisterTick()
        {
            if (_registeredTick)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _registeredTick = GlobalRegistry.Updatables.Contains(this);
        }

        private void EnsureBuffers()
        {
            if (_buffersReady)
                return;

            int clampedParticleCount = ResolveConfiguredCapacity();
            if (marineSnowCompute == null || marineSnowMaterial == null)
                return;

            _kernelIndex = marineSnowCompute.FindKernel("CSMain");
            if (_kernelIndex < 0)
            {
                LogMissingMainKernel();
                enabled = false;
                return;
            }

            _clearVisibleKernel = marineSnowCompute.FindKernel("ClearVisibleParticles");
            if (_clearVisibleKernel < 0)
            {
                LogMissingVisibleKernel();
                enabled = false;
                return;
            }

            _sonarGlowClearKernel = marineSnowCompute.FindKernel("ClearSonarGlow");
            _sonarGlowAccumulateKernel = marineSnowCompute.FindKernel("AccumulateSonarGlow");
            if (_sonarGlowClearKernel < 0 || _sonarGlowAccumulateKernel < 0)
            {
                LogMissingSonarGlowKernels();
                enabled = false;
                return;
            }

            // COLD ALLOC: ParticleGpuData[maxParticles] â€” maxParticles * 64B bootstrap upload cache, required to seed both GPU ping-pong buffers without runtime allocations â€” owner: HectonMarineSnowRenderer
            _bootstrapParticles = new ParticleGpuData[clampedParticleCount];
            // COLD ALLOC: GraphicsBuffer[maxParticles] â€” persistent marine-snow particle state ping-pong buffer A â€” owner: HectonMarineSnowRenderer
            _particleBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<ParticleGpuData>(clampedParticleCount);
            // COLD ALLOC: GraphicsBuffer[maxParticles] â€” persistent marine-snow particle state ping-pong buffer B â€” owner: HectonMarineSnowRenderer
            _particleBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<ParticleGpuData>(clampedParticleCount);
            // COLD ALLOC: GraphicsBuffer[1] â€” per-frame marine-snow constant buffer â€” owner: HectonMarineSnowRenderer
            _frameConstantsBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<FrameConstantsData>(1);
            _emptyFlowFieldBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float2>(1); // COLD ALLOC: GraphicsBuffer[1] - zero fallback ecosystem flow-vector buffer - owner: HectonMarineSnowRenderer
            GraphicsBufferUploadUtility.UploadArray(_emptyFlowFieldBuffer, _emptyFlowFieldUpload, 1);
            _visibleParticleIndexBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<uint>(clampedParticleCount); // COLD ALLOC: GraphicsBuffer[maxParticles] - GPU-written visible-particle index list - owner: HectonMarineSnowRenderer
            _indirectArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - GPU-written culled indirect indexed draw arguments - owner: HectonMarineSnowRenderer
            _emptyAbyssalFlowBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Vector4>(1); // COLD ALLOC: GraphicsBuffer[1] - zero fallback abyssal-flow vector buffer - owner: HectonMarineSnowRenderer
            GraphicsBufferUploadUtility.UploadArray(_emptyAbyssalFlowBuffer, _emptyAbyssalFlowUpload, 1);
            BootstrapParticles(clampedParticleCount);
            GraphicsBufferUploadUtility.UploadArray(_particleBufferA, _bootstrapParticles, clampedParticleCount);
            GraphicsBufferUploadUtility.UploadArray(_particleBufferB, _bootstrapParticles, clampedParticleCount);
            _frameParity = 0;
            EnsureEmptyCaveSdfTexture();
            EnsureQuadMesh();
            EnsureSonarGlowTexture();
            _buffersReady = true;
            ResetGpuBindingCaches();
            _staticBindingsDirty = true;
            _externalGpuBindingsDirty = true;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMissingMainKernel()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("HectonMarineSnowRenderer: compute kernel CSMain not found. Disabling compute marine snow.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMissingVisibleKernel()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("HectonMarineSnowRenderer: compute kernel ClearVisibleParticles not found. Disabling compute marine snow.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMissingSonarGlowKernels()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("HectonMarineSnowRenderer: sonar glow kernels not found. Disabling compute marine snow.");
#endif
        }

        private void BootstrapParticles(int particleCount)
        {
            Vector3 cameraPosition = targetCamera != null ? targetCamera.position : transform.position;
            float minVertical = math.min(verticalSpan.x, verticalSpan.y);
            float maxVertical = math.max(verticalSpan.x, verticalSpan.y);
            float respawnMinRadius = math.max(innerRadius + 0.1f, outerRadius - 4f);
            float respawnMaxRadius = math.max(respawnMinRadius + 0.1f, outerRadius);

            for (int index = 0; index < particleCount; index++)
            {
                float seed0 = HashToFloat01((uint)index, 0x3C6EF372u);
                float seed1 = HashToFloat01((uint)index, 0xBB67AE85u);
                float seed2 = HashToFloat01((uint)index, 0xA54FF53Au);
                float seed3 = HashToFloat01((uint)index, 0x510E527Fu);
                float2 lateralSeed = new float2(seed0 * 2f - 1f, seed1 * 2f - 1f);
                float lateralMajorAxis = math.max(math.abs(lateralSeed.x), math.abs(lateralSeed.y));
                float2 lateralDirection = lateralMajorAxis > 0.0001f ? lateralSeed / lateralMajorAxis : new float2(1f, 0f);
                float radius = math.lerp(respawnMinRadius, respawnMaxRadius, seed1);
                float height = math.lerp(minVertical, maxVertical, seed2);
                Vector3 position = cameraPosition + new Vector3(lateralDirection.x, 0f, lateralDirection.y) * radius;
                position.y = cameraPosition.y + height;
                float baseSpeed = math.lerp(descentMinSpeed, descentMaxSpeed, seed3);
                float size = math.lerp(particleSizeMin, particleSizeMax, HashToFloat01((uint)index, 0x9B05688Cu));

                _bootstrapParticles[index] = new ParticleGpuData
                {
                    PositionWS = position,
                    Life = 1f,
                    VelocityWS = ResolveBootstrapVelocity(baseSpeed),
                    Size = size,
                    PreviousPositionWS = position,
                    Flags = ResolveBootstrapFlags(),
                    Uv = new Vector2(seed0, seed1),
                    Pad = new Vector2(seed2, seed3)
                };
            }
        }

        private uint ResolveBootstrapFlags()
        {
            switch (fluidType)
            {
                case VFXEmissionProfile.FluidType.Bubble:
                    return ParticleFlagBubble;
                case VFXEmissionProfile.FluidType.Debris:
                    return ParticleFlagDebris;
                default:
                    return ParticleFlagSnow;
            }
        }

        private Vector3 ResolveBootstrapVelocity(float baseSpeed)
        {
            if (fluidType == VFXEmissionProfile.FluidType.Bubble)
                return new Vector3(0f, math.max(0.02f, baseSpeed), 0f);

            return new Vector3(0f, -baseSpeed, 0f);
        }

        private void RefreshFlowFieldUpload(float dt)
        {
            _flowFieldUploadTimer -= dt;
            HectonMapMagicVegetationBridge bridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (bridge == null)
            {
                _flowFieldResolution = 0;
                _flowFieldCellSize = 0f;
                _flowFieldCenterWS = Vector3.zero;
                return;
            }

            bool hasPayload = bridge.TryGetEcosystemFlowFieldPayload(
                out NativeArray<float2> flowVectors,
                out int gridResolution,
                out Vector3 gridCenter,
                out float cellSize);
            if (!hasPayload)
            {
                _flowFieldResolution = 0;
                _flowFieldCellSize = 0f;
                _flowFieldCenterWS = Vector3.zero;
                return;
            }

            _flowFieldCenterWS = gridCenter;
            _flowFieldResolution = gridResolution;
            _flowFieldCellSize = cellSize;

            float recenterThreshold = math.max(0.01f, cellSize * flowFieldRecenterThresholdCells);
            bool forceUpload =
                _flowFieldBuffer == null ||
                _flowFieldUploadTimer <= 0f ||
                _lastUploadedFlowFieldCenterWS == Vector3.zero ||
                (gridCenter - _lastUploadedFlowFieldCenterWS).sqrMagnitude >= recenterThreshold * recenterThreshold;

            if (!forceUpload)
                return;

            int requiredCount = math.max(1, flowVectors.Length);
            if (_flowFieldBuffer == null || _flowFieldBuffer.count != requiredCount)
            {
                ReleaseBuffer(ref _flowFieldBuffer);
                // COLD ALLOC: GraphicsBuffer[flowVectors.Length] â€” ecosystem flow-field snapshot staging on GPU, sized to the authoritative bridge payload â€” owner: HectonMarineSnowRenderer
                _flowFieldBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float2>(requiredCount);
                _boundSimulationFlowFieldBuffer = null;
                _staticBindingsDirty = true;
            }

            GraphicsBufferUploadUtility.UploadNativeArray(_flowFieldBuffer, flowVectors, requiredCount);
            _lastUploadedFlowFieldCenterWS = gridCenter;
            _flowFieldUploadTimer = math.max(0.05f, flowFieldUploadInterval);
        }

        private void ApplyStaticBindingsIfNeeded()
        {
            if (!_staticBindingsDirty)
                return;

            if (_particleBufferA == null ||
                _particleBufferB == null ||
                _frameConstantsBuffer == null ||
                _emptyFlowFieldBuffer == null ||
                _visibleParticleIndexBuffer == null ||
                _indirectArgsBuffer == null ||
                _emptyAbyssalFlowBuffer == null)
                return;

            GraphicsBuffer flowFieldBuffer = _flowFieldBuffer != null ? _flowFieldBuffer : _emptyFlowFieldBuffer;
            marineSnowCompute.SetBuffer(_kernelIndex, ShaderIds.ParticlesReadId, _particleBufferA);
            _boundSimulationReadBuffer = _particleBufferA;
            marineSnowCompute.SetBuffer(_kernelIndex, ShaderIds.ParticlesWriteId, _particleBufferB);
            _boundSimulationWriteBuffer = _particleBufferB;
            marineSnowCompute.SetBuffer(_kernelIndex, ShaderIds.FlowFieldId, flowFieldBuffer);
            _boundSimulationFlowFieldBuffer = flowFieldBuffer;
            marineSnowCompute.SetBuffer(_kernelIndex, ShaderIds.VisibleParticleIndicesId, _visibleParticleIndexBuffer);
            _boundSimulationVisibleParticleIndexBuffer = _visibleParticleIndexBuffer;
            marineSnowCompute.SetBuffer(_kernelIndex, ShaderIds.IndirectArgsId, _indirectArgsBuffer);
            _boundSimulationIndirectArgsBuffer = _indirectArgsBuffer;
            marineSnowCompute.SetBuffer(_clearVisibleKernel, ShaderIds.IndirectArgsId, _indirectArgsBuffer);
            marineSnowCompute.SetBuffer(_kernelIndex, ShaderIds.AbyssalFlowFieldResultId, _emptyAbyssalFlowBuffer);
            _boundAbyssalFlowBuffer = _emptyAbyssalFlowBuffer;
            marineSnowCompute.SetBuffer(_kernelIndex, ShaderIds.FrameConstantsId, _frameConstantsBuffer);
            VFXEmissionProfile.FluidSettings emissionSettings = ResolveEmissionSettings();
            marineSnowCompute.SetVector(
                ShaderIds.DriftParamsId,
                new Vector4(
                    math.min(descentMinSpeed, descentMaxSpeed),
                    math.max(descentMinSpeed, descentMaxSpeed),
                    wanderStrength,
                    emissionSettings.baseDragCoeff > 0f ? emissionSettings.baseDragCoeff : baseDragCoefficient));
            marineSnowCompute.SetVector(
                ShaderIds.FlowParamsId,
                new Vector4(
                    flowBlend,
                    densityBiasFlowGain,
                    0.15f,
                    0f));
            marineSnowCompute.SetVector(ShaderIds.BubbleParamsId, Vector4.zero);
            _boundBubbleParams = Vector4.zero;
            marineSnowCompute.SetVector(ShaderIds.DepthCollisionParamsId, DepthCollisionParams);
            _boundDepthCollisionParams = DepthCollisionParams;

            marineSnowMaterial.SetBuffer(ShaderIds.FrameConstantsId, _frameConstantsBuffer);
            marineSnowMaterial.SetBuffer(ShaderIds.VisibleParticleIndicesId, _visibleParticleIndexBuffer);
            _boundMaterialVisibleParticleIndexBuffer = _visibleParticleIndexBuffer;
            marineSnowMaterial.SetVector(
                ShaderIds.RenderParamsId,
                new Vector4(
                    maxAlpha,
                    softness,
                    math.max(0.25f, maxViewDistance),
                    0f));
            marineSnowMaterial.SetColor(ShaderIds.TintId, particleTint);

            _staticBindingsDirty = false;
        }

        private VFXEmissionProfile.FluidSettings ResolveEmissionSettings()
        {
            if (emissionProfile != null)
                return emissionProfile.GetSettings(fluidType);

            return new VFXEmissionProfile.FluidSettings
            {
                baseDragCoeff = baseDragCoefficient,
                buoyancyModifier = fluidType == VFXEmissionProfile.FluidType.Bubble ? 1f : -0.02f,
                turbulenceScale = 1f,
                wobbleScale = fluidType == VFXEmissionProfile.FluidType.Bubble ? 1f : 0f
            };
        }

        private void UpdateFrameConstants(float dt, float effectiveDensityScale)
        {
            _simulationTime += dt;
            if (_simulationTime >= 60f)
                _simulationTime -= 60f;

            Vector3 cameraPosition = targetCamera.position;
            Vector3 cameraRight = targetCamera.right;
            Vector3 cameraUp = targetCamera.up;
            Vector3 cameraVelocity = ResolveCameraVelocity(cameraPosition, dt);
            float speedLineStretch = ResolveSpeedLineStretch(cameraVelocity, dt);
            float densityScale = math.saturate(effectiveDensityScale);
            float activeFlag = densityScale > ActiveDensityEpsilon ? 1f : 0f;

            _frameConstantsUpload[0] = new FrameConstantsData
            {
                CameraPositionTime = new Vector4(cameraPosition.x, cameraPosition.y, cameraPosition.z, _simulationTime),
                CameraRightDeltaTime = new Vector4(cameraRight.x, cameraRight.y, cameraRight.z, dt),
                CameraUpDensity = new Vector4(cameraUp.x, cameraUp.y, cameraUp.z, densityScale),
                FlowFieldCenterCellSize = new Vector4(_flowFieldCenterWS.x, _flowFieldCenterWS.y, _flowFieldCenterWS.z, _flowFieldCellSize),
                ShellParams = new Vector4(
                    math.max(0.05f, innerRadius),
                    math.max(innerRadius + 0.1f, outerRadius),
                    math.min(verticalSpan.x, verticalSpan.y),
                    math.max(verticalSpan.x, verticalSpan.y)),
                MetaParams = new Vector4(
                    _activeParticleCount,
                    _flowFieldResolution,
                    Time.frameCount & 1023,
                    activeFlag),
                CameraVelocityStretch = new Vector4(
                    cameraVelocity.x,
                    cameraVelocity.y,
                    cameraVelocity.z,
                    speedLineStretch)
            };

            GraphicsBufferUploadUtility.UploadArray(_frameConstantsBuffer, _frameConstantsUpload, 1);
            VFXEmissionProfile.FluidSettings emissionSettings = ResolveEmissionSettings();
            float biolumeSurgeBlend = ResolveBiolumeSurgeBlend();
            float surgeTurbulenceScale = math.lerp(1f, biolumeSurgeTurbulenceMultiplier, biolumeSurgeBlend);
            Vector4 emissionParams = new Vector4(
                emissionSettings.buoyancyModifier,
                emissionSettings.turbulenceScale * surgeTurbulenceScale,
                emissionSettings.wobbleScale * surgeTurbulenceScale,
                (float)fluidType);
            Vector4 bubbleParams = new Vector4(
                _underwaterActive ? _bubbleTrailMovement01 : 0f,
                _underwaterActive ? _bubbleTrailExhale01 : 0f,
                _lastDepth,
                activeFlag);
            SetComputeVectorHotIfChanged(ShaderIds.EmissionParamsId, emissionParams, ref _boundEmissionParams);
            SetComputeVectorHotIfChanged(ShaderIds.BubbleParamsId, bubbleParams, ref _boundBubbleParams);
            if (_targetCameraComponent != null)
            {
                Texture depthTexture = Shader.GetGlobalTexture(ShaderIds.CameraDepthTextureId);
                if (depthTexture != null)
                    SetKernelTextureIfChanged(_kernelIndex, ShaderIds.CameraDepthTextureId, depthTexture, ref _boundCameraDepthTexture);

                Matrix4x4 viewProjection = GL.GetGPUProjectionMatrix(_targetCameraComponent.projectionMatrix, false) * _targetCameraComponent.worldToCameraMatrix;
                Vector4 depthTextureTexelSize = new Vector4(
                    _targetCameraComponent.pixelWidth > 0 ? 1f / _targetCameraComponent.pixelWidth : 0f,
                    _targetCameraComponent.pixelHeight > 0 ? 1f / _targetCameraComponent.pixelHeight : 0f,
                    _targetCameraComponent.pixelWidth,
                    _targetCameraComponent.pixelHeight);
                SetComputeMatrixHotIfChanged(ShaderIds.ViewProjectionId, viewProjection, ref _boundViewProjection);
                SetComputeMatrixHotIfChanged(ShaderIds.ViewMatrixId, _targetCameraComponent.worldToCameraMatrix, ref _boundViewMatrix);
                SetComputeVectorHotIfChanged(ShaderIds.ZBufferParamsId, Shader.GetGlobalVector(ShaderIds.GlobalZBufferParamsId), ref _boundZBufferParams);
                SetComputeVectorHotIfChanged(ShaderIds.DepthTextureTexelSizeId, depthTextureTexelSize, ref _boundDepthTextureTexelSize);
            }
        }

        private void RefreshExternalGpuBindings(float dt)
        {
            SetComputeVectorIfChanged(ShaderIds.SubmarineWashSphereId, Shader.GetGlobalVector(ShaderIds.SubmarineWashSphereId), ref _boundSubmarineWashSphere);
            SetComputeVectorIfChanged(ShaderIds.SubmarineWashVelocityId, Shader.GetGlobalVector(ShaderIds.SubmarineWashVelocityId), ref _boundSubmarineWashVelocity);

            _externalGpuBindingColdTickTimer -= math.max(0f, dt);
            if (!_externalGpuBindingsDirty && _externalGpuBindingColdTickTimer > 0f)
                return;

            RefreshAbyssalFlowBinding();
            RefreshCaveSdfBinding();
            RefreshTerrainHeightBinding();
            SetComputeVectorIfChanged(ShaderIds.PropwashParamsId, DefaultPropwashParams, ref _boundPropwashParams);
            SetComputeVectorIfChanged(ShaderIds.FlowSynchronyParamsId, ResolveFlowSynchronyParams(), ref _boundFlowSynchronyParams);
            _externalGpuBindingColdTickTimer = ExternalGpuBindingColdTickSeconds;
            _externalGpuBindingsDirty = false;
        }

        private void RefreshAbyssalFlowBinding()
        {
            GraphicsBuffer flowFieldBuffer = _emptyAbyssalFlowBuffer;
            Vector4 gridResolution = Vector4.zero;
            Vector4 flowCenter = Vector4.zero;
            Vector4 flowSpacing = Vector4.zero;

            HectonFluidEngine fluidEngine = GlobalRegistry.Fluid;
            if (fluidEngine != null &&
                fluidEngine.TryGetGpuAbyssalFlowFieldBuffer(
                    out GraphicsBuffer publishedFlowFieldBuffer,
                    out Vector4 publishedGridResolution,
                    out Vector4 publishedFlowCenter,
                    out Vector4 publishedFlowSpacing))
            {
                flowFieldBuffer = publishedFlowFieldBuffer;
                gridResolution = publishedGridResolution;
                flowCenter = publishedFlowCenter;
                flowSpacing = publishedFlowSpacing;
            }

            if (flowFieldBuffer != null && flowFieldBuffer != _boundAbyssalFlowBuffer)
            {
                marineSnowCompute.SetBuffer(_kernelIndex, ShaderIds.AbyssalFlowFieldResultId, flowFieldBuffer);
                _boundAbyssalFlowBuffer = flowFieldBuffer;
            }

            SetComputeVectorIfChanged(ShaderIds.AbyssalGridResolutionId, gridResolution, ref _boundAbyssalGridResolution);
            SetComputeVectorIfChanged(ShaderIds.AbyssalFlowCenterId, flowCenter, ref _boundAbyssalFlowCenter);
            SetComputeVectorIfChanged(ShaderIds.AbyssalFlowSpacingId, flowSpacing, ref _boundAbyssalFlowSpacing);
        }

        private void RefreshCaveSdfBinding()
        {
            Texture sdfTexture = _emptyCaveSdfTexture;
            Matrix4x4 worldToLocal = IdentityMatrix;
            Vector4 halfExtentsAndRange = Vector4.zero;
            float active = 0f;

            HectonCaveVoxelLightingVolume caveVolume = HectonCaveVoxelLightingVolume.ActiveRuntimeInstance;
            if (caveVolume != null &&
                caveVolume.TryGetPublishedGpuSdfPayload(
                    out Texture3D publishedSdfTexture,
                    out Matrix4x4 publishedWorldToLocal,
                    out Vector4 publishedHalfExtentsAndRange))
            {
                sdfTexture = publishedSdfTexture;
                worldToLocal = publishedWorldToLocal;
                halfExtentsAndRange = publishedHalfExtentsAndRange;
                active = 1f;
            }

            if (sdfTexture != null && sdfTexture != _boundCaveSdfTexture)
            {
                marineSnowCompute.SetTexture(_kernelIndex, ShaderIds.CaveVoxelSdfTexId, sdfTexture);
                _boundCaveSdfTexture = sdfTexture;
            }

            SetComputeFloatIfChanged(ShaderIds.CaveVoxelActiveId, active, ref _boundCaveVoxelActive);
            SetComputeMatrixIfChanged(ShaderIds.CaveVoxelWorldToLocalId, worldToLocal, ref _boundCaveVoxelWorldToLocal);
            SetComputeVectorIfChanged(ShaderIds.CaveVoxelHalfExtentsId, halfExtentsAndRange, ref _boundCaveVoxelHalfExtents);
        }

        private void RefreshTerrainHeightBinding()
        {
            Texture heightTexture = Texture2D.blackTexture;
            Vector4 heightRect = Vector4.zero;
            Vector4 heightScale = DisabledTerrainHeightScale;

            HectonMapMagicVegetationBridge bridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (bridge != null &&
                bridge.TryGetActiveHeightTexturePayload(out HectonMapMagicVegetationBridge.TerrainHeightTexturePayload heightPayload) &&
                heightPayload.HeightTexture != null &&
                heightPayload.TerrainSize.x > 0f &&
                heightPayload.TerrainSize.z > 0f)
            {
                heightTexture = heightPayload.HeightTexture;
                heightRect = new Vector4(
                    heightPayload.TerrainPosition.x,
                    heightPayload.TerrainPosition.z,
                    1f / heightPayload.TerrainSize.x,
                    1f / heightPayload.TerrainSize.z);
                heightScale = new Vector4(
                    heightPayload.TerrainPosition.y,
                    heightPayload.TerrainSize.y,
                    1f,
                    heightPayload.HeightmapResolution);
            }

            if (heightTexture != _boundTerrainHeightTexture)
            {
                marineSnowCompute.SetTexture(_kernelIndex, ShaderIds.TerrainHeightTextureId, heightTexture);
                _boundTerrainHeightTexture = heightTexture;
            }

            SetComputeVectorIfChanged(ShaderIds.TerrainHeightRectId, heightRect, ref _boundTerrainHeightRect);
            SetComputeVectorIfChanged(ShaderIds.TerrainHeightScaleId, heightScale, ref _boundTerrainHeightScale);
        }

        private void DispatchVisibleClear()
        {
            if (_clearVisibleKernel < 0 || _indirectArgsBuffer == null)
                return;

            marineSnowCompute.Dispatch(_clearVisibleKernel, 1, 1, 1);
        }

        private Vector3 ResolveCameraVelocity(Vector3 cameraPosition, float dt)
        {
            Vector3 velocity = Vector3.zero;
            if (_hasLastCameraPositionWS && dt > 0.0001f)
                velocity = (cameraPosition - _lastCameraPositionWS) * (1f / dt);

            _lastCameraPositionWS = cameraPosition;
            _hasLastCameraPositionWS = true;
            return velocity;
        }

        private float ResolveSpeedLineStretch(Vector3 cameraVelocity, float dt)
        {
            float targetIntensity = 0f;
            if (_underwaterActive && dt > 0f)
            {
                float speedSq = math.lengthsq((float3)cameraVelocity);
                float fullVelocity = math.max(1f, speedLineFullVelocity);
                float startVelocity = fullVelocity * 0.72f;
                float fullVelocitySq = fullVelocity * fullVelocity;
                float startVelocitySq = startVelocity * startVelocity;
                float speed01 = math.saturate((speedSq - startVelocitySq) / math.max(0.01f, fullVelocitySq - startVelocitySq));
                targetIntensity = speed01 * speed01 * (3f - 2f * speed01);
            }

            float blendT = FastDecayBlend(math.max(0.1f, speedLineResponseSharpness), math.max(0f, dt));
            _speedLineIntensity = math.lerp(_speedLineIntensity, targetIntensity, blendT);
            return math.lerp(1f, math.max(1f, speedLineMaxStretch), _speedLineIntensity);
        }

        private static float FastDecayBlend(float speed, float deltaTime)
        {
            float x = math.max(0f, speed) * math.max(0f, deltaTime);
            if (x >= 3.5f)
                return 1f;

            return math.saturate((12f * x) / (12f + (6f * x) + (x * x)));
        }

        private static Vector4 ResolveFlowSynchronyParams()
        {
            Vector4 synchronyParams = Shader.GetGlobalVector(ShaderIds.FlowSynchronyParamsId);
            if (synchronyParams.x <= 0f)
                return DefaultFlowSynchronyParams;

            return synchronyParams;
        }

        private void DispatchSimulation()
        {
            GraphicsBuffer readBuffer = _frameParity == 0 ? _particleBufferA : _particleBufferB;
            GraphicsBuffer writeBuffer = _frameParity == 0 ? _particleBufferB : _particleBufferA;
            GraphicsBuffer flowFieldBuffer = _flowFieldBuffer != null ? _flowFieldBuffer : _emptyFlowFieldBuffer;
            SetKernelBufferIfChanged(_kernelIndex, ShaderIds.ParticlesReadId, readBuffer, ref _boundSimulationReadBuffer);
            SetKernelBufferIfChanged(_kernelIndex, ShaderIds.ParticlesWriteId, writeBuffer, ref _boundSimulationWriteBuffer);
            SetKernelBufferIfChanged(_kernelIndex, ShaderIds.FlowFieldId, flowFieldBuffer, ref _boundSimulationFlowFieldBuffer);
            SetKernelBufferIfChanged(_kernelIndex, ShaderIds.VisibleParticleIndicesId, _visibleParticleIndexBuffer, ref _boundSimulationVisibleParticleIndexBuffer);
            SetKernelBufferIfChanged(_kernelIndex, ShaderIds.IndirectArgsId, _indirectArgsBuffer, ref _boundSimulationIndirectArgsBuffer);

            int groupCount = (_activeParticleCount + ThreadGroupSize - 1) / ThreadGroupSize;
            marineSnowCompute.Dispatch(_kernelIndex, groupCount, 1, 1);

            SetMaterialBufferIfChanged(ShaderIds.ParticlesRenderId, writeBuffer, ref _boundMaterialParticlesBuffer);
            SetMaterialBufferIfChanged(ShaderIds.VisibleParticleIndicesId, _visibleParticleIndexBuffer, ref _boundMaterialVisibleParticleIndexBuffer);
        }

        private void DispatchSonarGlow()
        {
            if (!IsSonarGlowActive())
            {
                PublishSonarGlowGlobals(Vector4.zero, Vector4.zero, null);
                return;
            }

            EnsureSonarGlowTexture();
            if (_sonarGlowTexture == null)
            {
                PublishSonarGlowGlobals(Vector4.zero, Vector4.zero, null);
                return;
            }

            Vector4 sonarGlowTexelSize = ResolveSonarGlowTexelSize();
            Vector4 sonarGlowParams = ResolveSonarGlowParams();
            SetComputeVectorHotIfChanged(ShaderIds.SonarGlowTexelSizeId, sonarGlowTexelSize, ref _boundSonarGlowTexelSize);
            SetComputeVectorHotIfChanged(ShaderIds.SonarGlowParamsId, sonarGlowParams, ref _boundSonarGlowParams);
            PublishSonarGlowGlobals(sonarGlowTexelSize, sonarGlowParams, _sonarGlowTexture);

            SetKernelTextureIfChanged(_sonarGlowClearKernel, ShaderIds.SonarGlowResultId, _sonarGlowTexture, ref _boundSonarGlowClearTexture);
            SetKernelTextureIfChanged(_sonarGlowAccumulateKernel, ShaderIds.SonarGlowResultId, _sonarGlowTexture, ref _boundSonarGlowAccumulateTexture);
            SetKernelBufferIfChanged(
                _sonarGlowAccumulateKernel,
                ShaderIds.ParticlesWriteId,
                _frameParity == 0 ? _particleBufferB : _particleBufferA,
                ref _boundSonarGlowParticlesWriteBuffer);

            int clearGroupsX = (_sonarGlowWidth + 7) / 8;
            int clearGroupsY = (_sonarGlowHeight + 7) / 8;
            marineSnowCompute.Dispatch(_sonarGlowClearKernel, clearGroupsX, clearGroupsY, 1);

            int particleGroups = (_activeParticleCount + ThreadGroupSize - 1) / ThreadGroupSize;
            marineSnowCompute.Dispatch(_sonarGlowAccumulateKernel, particleGroups, 1, 1);
        }

        private bool IsSonarGlowActive()
        {
            if (_activeParticleCount <= 0 ||
                sonarGlowIntensity <= 0f ||
                sonarGlowCompositeStrength <= 0f)
            {
                return false;
            }

            return Time.time <= Shader.GetGlobalFloat(ShaderIds.SonarRevealExpireTimeId);
        }

        private Vector4 ResolveSonarGlowTexelSize()
        {
            Vector4 texelSize;
            texelSize.x = 1f / math.max(1, _sonarGlowWidth);
            texelSize.y = 1f / math.max(1, _sonarGlowHeight);
            texelSize.z = _sonarGlowWidth;
            texelSize.w = _sonarGlowHeight;
            return texelSize;
        }

        private Vector4 ResolveSonarGlowParams()
        {
            Vector4 parameters;
            parameters.x = math.max(0f, sonarGlowIntensity);
            parameters.y = math.max(0f, sonarGlowCompositeStrength);
            parameters.z = 65535f;
            parameters.w = 1f;
            return parameters;
        }

        private void PublishSonarGlowGlobals(Vector4 texelSize, Vector4 parameters, Texture texture)
        {
            if (_sonarGlowGlobalsDirty ||
                !NearlyEqual(_lastPublishedSonarGlowTexelSize, texelSize, ShaderVectorPublishEpsilon))
            {
                Shader.SetGlobalVector(ShaderIds.SonarGlowTexelSizeId, texelSize);
                _lastPublishedSonarGlowTexelSize = texelSize;
            }

            if (_sonarGlowGlobalsDirty ||
                !NearlyEqual(_lastPublishedSonarGlowParams, parameters, ShaderVectorPublishEpsilon))
            {
                Shader.SetGlobalVector(ShaderIds.SonarGlowParamsId, parameters);
                _lastPublishedSonarGlowParams = parameters;
            }

            if (texture != null && (_sonarGlowGlobalsDirty || _lastPublishedSonarGlowTexture != texture))
            {
                Shader.SetGlobalTexture(ShaderIds.SonarGlowTextureId, texture);
                _lastPublishedSonarGlowTexture = texture;
            }
            else if (texture == null)
            {
                _lastPublishedSonarGlowTexture = null;
            }

            _sonarGlowGlobalsDirty = false;
        }

        private void EnsureEmptyCaveSdfTexture()
        {
            if (_emptyCaveSdfTexture != null)
                return;

            TextureFormat textureFormat = SystemInfo.SupportsTextureFormat(TextureFormat.R8)
                ? TextureFormat.R8
                : TextureFormat.Alpha8;
            _emptyCaveSdfTexture = new Texture3D(1, 1, 1, textureFormat, false)
            {
                name = "__HectonMarineSnowEmptySdf",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
                anisoLevel = 0
            }; // COLD ALLOC: Texture3D[1] - fallback SDF binding for marine-snow compute when cave volume is inactive - owner: HectonMarineSnowRenderer
            _emptyCaveSdfTexture.SetPixel(0, 0, 0, new Color(0.5f, 0f, 0f, 0f));
            _emptyCaveSdfTexture.Apply(false, true);
        }

        private void EnsureQuadMesh()
        {
            if (_quadMesh != null)
                return;

            _quadMesh = new Mesh
            {
                name = "__HectonMarineSnowIndirectQuad",
                bounds = new Bounds(Vector3.zero, Vector3.one * 2f)
            }; // COLD ALLOC: Mesh[1] - six-vertex quad mesh for DrawMeshInstancedIndirect marine-snow draw - owner: HectonMarineSnowRenderer
            _quadMesh.vertices = QuadMeshVertices;
            _quadMesh.SetIndices(QuadMeshIndices, MeshTopology.Triangles, 0, false);
            _quadMesh.UploadMeshData(true);
        }

        private static bool NearlyEqual(Vector4 left, Vector4 right, float epsilon)
        {
            return math.abs(left.x - right.x) <= epsilon &&
                   math.abs(left.y - right.y) <= epsilon &&
                   math.abs(left.z - right.z) <= epsilon &&
                   math.abs(left.w - right.w) <= epsilon;
        }

        private static bool NearlyEqual(Matrix4x4 left, Matrix4x4 right, float epsilon)
        {
            return math.abs(left.m00 - right.m00) <= epsilon &&
                   math.abs(left.m01 - right.m01) <= epsilon &&
                   math.abs(left.m02 - right.m02) <= epsilon &&
                   math.abs(left.m03 - right.m03) <= epsilon &&
                   math.abs(left.m10 - right.m10) <= epsilon &&
                   math.abs(left.m11 - right.m11) <= epsilon &&
                   math.abs(left.m12 - right.m12) <= epsilon &&
                   math.abs(left.m13 - right.m13) <= epsilon &&
                   math.abs(left.m20 - right.m20) <= epsilon &&
                   math.abs(left.m21 - right.m21) <= epsilon &&
                   math.abs(left.m22 - right.m22) <= epsilon &&
                   math.abs(left.m23 - right.m23) <= epsilon &&
                   math.abs(left.m30 - right.m30) <= epsilon &&
                   math.abs(left.m31 - right.m31) <= epsilon &&
                   math.abs(left.m32 - right.m32) <= epsilon &&
                   math.abs(left.m33 - right.m33) <= epsilon;
        }

        private void SetKernelBufferIfChanged(int kernelIndex, int shaderId, GraphicsBuffer buffer, ref GraphicsBuffer cachedBuffer)
        {
            if (buffer == null || buffer == cachedBuffer)
                return;

            marineSnowCompute.SetBuffer(kernelIndex, shaderId, buffer);
            cachedBuffer = buffer;
        }

        private void SetMaterialBufferIfChanged(int shaderId, GraphicsBuffer buffer, ref GraphicsBuffer cachedBuffer)
        {
            if (buffer == null || buffer == cachedBuffer)
                return;

            marineSnowMaterial.SetBuffer(shaderId, buffer);
            cachedBuffer = buffer;
        }

        private void SetKernelTextureIfChanged(int kernelIndex, int shaderId, Texture texture, ref Texture cachedTexture)
        {
            if (texture == null || texture == cachedTexture)
                return;

            marineSnowCompute.SetTexture(kernelIndex, shaderId, texture);
            cachedTexture = texture;
        }

        private void SetComputeVectorHotIfChanged(int shaderId, Vector4 value, ref Vector4 cachedValue)
        {
            if (NearlyEqual(value, cachedValue, ShaderVectorPublishEpsilon))
                return;

            marineSnowCompute.SetVector(shaderId, value);
            cachedValue = value;
        }

        private void SetComputeMatrixHotIfChanged(int shaderId, Matrix4x4 value, ref Matrix4x4 cachedValue)
        {
            if (NearlyEqual(value, cachedValue, ShaderVectorPublishEpsilon))
                return;

            marineSnowCompute.SetMatrix(shaderId, value);
            cachedValue = value;
        }

        private void SetComputeVectorIfChanged(int shaderId, Vector4 value, ref Vector4 cachedValue)
        {
            if (!_externalGpuBindingsDirty && NearlyEqual(value, cachedValue, ShaderVectorPublishEpsilon))
                return;

            marineSnowCompute.SetVector(shaderId, value);
            cachedValue = value;
        }

        private void SetComputeFloatIfChanged(int shaderId, float value, ref float cachedValue)
        {
            if (!_externalGpuBindingsDirty && math.abs(value - cachedValue) <= ShaderVectorPublishEpsilon)
                return;

            marineSnowCompute.SetFloat(shaderId, value);
            cachedValue = value;
        }

        private void SetComputeMatrixIfChanged(int shaderId, Matrix4x4 value, ref Matrix4x4 cachedValue)
        {
            if (!_externalGpuBindingsDirty && NearlyEqual(value, cachedValue, ShaderVectorPublishEpsilon))
                return;

            marineSnowCompute.SetMatrix(shaderId, value);
            cachedValue = value;
        }

        private void EnsureSonarGlowTexture()
        {
            if (_targetCameraComponent == null)
                return;

            float renderScale = math.clamp(sonarGlowRenderScale, 0.1f, 1f);
            int targetWidth = math.max(8, (int)(_targetCameraComponent.pixelWidth * renderScale + 0.999f));
            int targetHeight = math.max(8, (int)(_targetCameraComponent.pixelHeight * renderScale + 0.999f));
            if (_sonarGlowTexture != null && _sonarGlowWidth == targetWidth && _sonarGlowHeight == targetHeight)
                return;

            ReleaseSonarGlowTexture();

            // COLD ALLOC: RenderTexture[sonarGlowWidth*sonarGlowHeight] — persistent sonar-reactive plankton splatmap — owner: HectonMarineSnowRenderer
            _sonarGlowTexture = new RenderTexture(targetWidth, targetHeight, 0, RenderTextureFormat.RInt, RenderTextureReadWrite.Linear)
            {
                name = "HectonMarineSnowSonarGlow",
                enableRandomWrite = true,
                useMipMap = false,
                autoGenerateMips = false,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            _sonarGlowTexture.Create();
            _sonarGlowWidth = targetWidth;
            _sonarGlowHeight = targetHeight;
            _sonarGlowGlobalsDirty = true;
            _boundSonarGlowClearTexture = null;
            _boundSonarGlowAccumulateTexture = null;
            _boundSonarGlowTexelSize = InvalidVector;
        }

        private void ReleaseSonarGlowTexture()
        {
            if (_sonarGlowTexture == null)
                return;

            _sonarGlowTexture.Release();
            Destroy(_sonarGlowTexture);
            _sonarGlowTexture = null;
            _sonarGlowWidth = 0;
            _sonarGlowHeight = 0;
            _sonarGlowGlobalsDirty = true;
            _boundSonarGlowClearTexture = null;
            _boundSonarGlowAccumulateTexture = null;
            _boundSonarGlowTexelSize = InvalidVector;
        }

        private void RenderMarineSnow()
        {
            if (_targetCameraComponent == null ||
                marineSnowMaterial == null ||
                _quadMesh == null ||
                _indirectArgsBuffer == null)
                return;

            Vector3 cameraPosition = targetCamera.position;
            float verticalSize = math.max(1f, math.abs(verticalSpan.y - verticalSpan.x));
            _drawBounds = new Bounds(
                cameraPosition + new Vector3(0f, (verticalSpan.x + verticalSpan.y) * 0.5f, 0f),
                new Vector3(outerRadius * 2f, verticalSize, outerRadius * 2f));

            Graphics.DrawMeshInstancedIndirect(
                _quadMesh,
                0,
                marineSnowMaterial,
                _drawBounds,
                _indirectArgsBuffer,
                0,
                null,
                shadowCastingMode,
                false,
                gameObject.layer,
                _targetCameraComponent);
        }

        private void ReleaseBuffers()
        {
            ReleaseBuffer(ref _particleBufferA);
            ReleaseBuffer(ref _particleBufferB);
            ReleaseBuffer(ref _flowFieldBuffer);
            ReleaseBuffer(ref _emptyFlowFieldBuffer);
            ReleaseBuffer(ref _frameConstantsBuffer);
            ReleaseBuffer(ref _visibleParticleIndexBuffer);
            ReleaseBuffer(ref _indirectArgsBuffer);
            ReleaseBuffer(ref _emptyAbyssalFlowBuffer);
            ReleaseEmptyCaveSdfTexture();
            ReleaseQuadMesh();
            ReleaseSonarGlowTexture();
            PublishSonarGlowGlobals(Vector4.zero, Vector4.zero, null);
            _buffersReady = false;
            _kernelIndex = -1;
            _clearVisibleKernel = -1;
            _sonarGlowClearKernel = -1;
            _sonarGlowAccumulateKernel = -1;
            ResetGpuBindingCaches();
            _externalGpuBindingsDirty = true;
            _bootstrapParticles = null;
            ResetSpeedLineHistory();
        }

        private void ResetGpuBindingCaches()
        {
            _boundCameraDepthTexture = null;
            _boundTerrainHeightTexture = null;
            _boundCaveSdfTexture = null;
            _boundAbyssalFlowBuffer = null;
            _boundSimulationReadBuffer = null;
            _boundSimulationWriteBuffer = null;
            _boundSimulationFlowFieldBuffer = null;
            _boundSimulationVisibleParticleIndexBuffer = null;
            _boundSimulationIndirectArgsBuffer = null;
            _boundMaterialParticlesBuffer = null;
            _boundMaterialVisibleParticleIndexBuffer = null;
            _boundSonarGlowParticlesWriteBuffer = null;
            _boundSonarGlowClearTexture = null;
            _boundSonarGlowAccumulateTexture = null;
            _boundAbyssalGridResolution = Vector4.zero;
            _boundAbyssalFlowCenter = Vector4.zero;
            _boundAbyssalFlowSpacing = Vector4.zero;
            _boundCaveVoxelHalfExtents = Vector4.zero;
            _boundTerrainHeightRect = Vector4.zero;
            _boundTerrainHeightScale = Vector4.zero;
            _boundSubmarineWashSphere = Vector4.zero;
            _boundSubmarineWashVelocity = Vector4.zero;
            _boundPropwashParams = Vector4.zero;
            _boundEmissionParams = InvalidVector;
            _boundBubbleParams = InvalidVector;
            _boundFlowSynchronyParams = InvalidVector;
            _boundZBufferParams = InvalidVector;
            _boundDepthTextureTexelSize = InvalidVector;
            _boundDepthCollisionParams = InvalidVector;
            _boundSonarGlowTexelSize = InvalidVector;
            _boundSonarGlowParams = InvalidVector;
            _boundViewProjection = InvalidMatrix;
            _boundViewMatrix = InvalidMatrix;
            _boundCaveVoxelWorldToLocal = IdentityMatrix;
            _boundCaveVoxelActive = -1f;
        }

        private void ReleaseEmptyCaveSdfTexture()
        {
            if (_emptyCaveSdfTexture == null)
                return;

            Destroy(_emptyCaveSdfTexture);
            _emptyCaveSdfTexture = null;
        }

        private void ReleaseQuadMesh()
        {
            if (_quadMesh == null)
                return;

            Destroy(_quadMesh);
            _quadMesh = null;
        }

        private static void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private static float HashToFloat01(uint index, uint salt)
        {
            uint value = index ^ salt;
            value ^= value >> 17;
            value *= 0xED5AD4BBu;
            value ^= value >> 11;
            value *= 0xAC4C1B51u;
            value ^= value >> 15;
            value *= 0x31848BABu;
            value ^= value >> 14;
            return (value & 0x00FFFFFFu) * (1f / 16777215f);
        }

        private int ResolveActiveParticleCount(float effectiveDensityScale)
        {
            int capacity = ResolveConfiguredCapacity();
            float densityScale = math.saturate(effectiveDensityScale);
            if (densityScale <= ActiveDensityEpsilon)
            {
                _debugActiveParticleCount = 0;
                return 0;
            }

            float budgetScale = 1f;

            DynamicResolutionScaler scaler = GlobalRegistry.DynamicResolution;
            float renderScale = scaler != null ? math.saturate(scaler.CurrentRenderScale) : 1f;
            budgetScale *= math.clamp(renderScale, 0.45f, 1f);
            _debugAdaptiveRenderScale = renderScale;

            VRAMMonitor vramMonitor = Hecton8.Core.GlobalRegistry.VRAMMonitor;
            VRAMMonitor.VRAMPressureState pressureState = vramMonitor != null
                ? vramMonitor.PressureState
                : VRAMMonitor.VRAMPressureState.Stable;

            switch (pressureState)
            {
                case VRAMMonitor.VRAMPressureState.Critical:
                    budgetScale *= 0.45f;
                    break;
                case VRAMMonitor.VRAMPressureState.Warning:
                    budgetScale *= 0.7f;
                    break;
            }

            _debugAdaptiveVramPressureState = pressureState;
            budgetScale *= math.lerp(0.35f, 1f, densityScale);
            budgetScale *= math.lerp(1f, biolumeSurgeParticleMultiplier, ResolveBiolumeSurgeBlend());
            budgetScale *= math.min(1.12f, 1f + _bubbleTrailMovement01 * 0.08f + _bubbleTrailExhale01 * 0.06f);
            _debugAdaptiveBudgetScale = budgetScale;

            int resolvedCount = math.clamp((int)(capacity * budgetScale + 0.5f), 64, capacity);
            _debugActiveParticleCount = resolvedCount;
            return resolvedCount;
        }

        private float ResolveBiolumeSurgeBlend()
        {
            return math.saturate(_biolumeSurgeTimer / BiolumeSurgeDurationSeconds);
        }

        private int ResolveConfiguredCapacity()
        {
            return math.clamp(maxParticles, 64, MaxMarineSnowParticleCapacity);
        }
    }
}



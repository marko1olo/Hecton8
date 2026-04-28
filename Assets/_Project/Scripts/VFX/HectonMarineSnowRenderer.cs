using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Optimization;
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
        private const int ThreadGroupSize = 64;
        private const int ParticleStride = 64;
        private const int FrameConstantsStride = 96;
        private const int ParticleFlagSnow = 1 << 3;

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
        }

        private static class ShaderIds
        {
            internal static readonly int ParticlesReadId = Shader.PropertyToID("_MarineSnowParticlesRead");
            internal static readonly int ParticlesWriteId = Shader.PropertyToID("_MarineSnowParticlesWrite");
            internal static readonly int ParticlesRenderId = Shader.PropertyToID("_MarineSnowParticles");
            internal static readonly int FlowFieldId = Shader.PropertyToID("_MarineSnowFlowField");
            internal static readonly int FrameConstantsId = Shader.PropertyToID("_HectonMarineSnowFrame");
            internal static readonly int DriftParamsId = Shader.PropertyToID("_MarineSnowDriftParams");
            internal static readonly int FlowParamsId = Shader.PropertyToID("_MarineSnowFlowParams");
            internal static readonly int FlowSynchronyParamsId = Shader.PropertyToID("_HectonFlowSynchronyParams");
            internal static readonly int RenderParamsId = Shader.PropertyToID("_MarineSnowRenderParams");
            internal static readonly int TintId = Shader.PropertyToID("_MarineSnowTint");
        }

        [Header("â”€â”€ References â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Camera transform that owns the marine snow shell. Bind this to the runtime main camera.")]
        [SerializeField] private Transform targetCamera;
        [Tooltip("Compute shader responsible for marine-snow simulation.")]
        [SerializeField] private ComputeShader marineSnowCompute;
        [Tooltip("Dedicated material used by the direct marine-snow billboard draw.")]
        [SerializeField] private Material marineSnowMaterial;

        [Header("â”€â”€ Population â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Maximum compute-simulated plankton particles. Runtime buffer sizing supports up to 100000 particles; tune lower on MX350 scenes that cannot afford the overdraw.")]
        [SerializeField, Range(512, 100000)] private int maxParticles = 100000;
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

        private readonly FrameConstantsData[] _frameConstantsUpload = new FrameConstantsData[1]; // COLD ALLOC: FrameConstantsData[1] â€” reusable per-frame constant-buffer upload cache â€” owner: HectonMarineSnowRenderer

        private ParticleGpuData[] _bootstrapParticles;
        private GraphicsBuffer _particleBufferA;
        private GraphicsBuffer _particleBufferB;
        private GraphicsBuffer _flowFieldBuffer;
        private GraphicsBuffer _frameConstantsBuffer;
        private Camera _targetCameraComponent;
        private Bounds _drawBounds;
        private int _kernelIndex = -1;
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
        private float _visualDensityScale;
        private float _lastDepth;
        private float _lastLightFactor = 1f;
        private float _lastSubmergeImpulse;
        private Vector3 _flowFieldCenterWS;
        private Vector3 _lastUploadedFlowFieldCenterWS;
        [SerializeField] private int _debugActiveParticleCount;
        [SerializeField] private float _debugAdaptiveRenderScale = 1f;
        [SerializeField] private float _debugAdaptiveBudgetScale = 1f;
        [SerializeField] private VRAMMonitor.VRAMPressureState _debugAdaptiveVramPressureState;

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

            if (!_buffersReady || _bootstrapParticles == null)
                return;

            ResolveTargetCamera();
            if (targetCamera == null)
                return;

            int particleCount = math.max(64, maxParticles);
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

            if (!_underwaterActive && _visualDensityScale <= 0.0001f)
                return;

            EnsureBuffers();
            if (!_buffersReady)
                return;

            _activeParticleCount = ResolveActiveParticleCount();
            RefreshFlowFieldUpload(dt);
            ApplyStaticBindingsIfNeeded();
            UpdateFrameConstants(math.max(0f, dt));
            DispatchSimulation();
            RenderMarineSnow();
            _frameParity ^= 1;
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

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _registeredTick = true;
        }

        private void EnsureBuffers()
        {
            if (_buffersReady)
                return;

            int clampedParticleCount = math.max(64, maxParticles);
            if (marineSnowCompute == null || marineSnowMaterial == null)
                return;

            _kernelIndex = marineSnowCompute.FindKernel("CSMain");
            if (_kernelIndex < 0)
            {
                Debug.LogError("HectonMarineSnowRenderer: compute kernel CSMain not found. Disabling compute marine snow.");
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
            // COLD ALLOC: GraphicsBuffer[1] â€” indirect draw arguments for the marine-snow billboard pass â€” owner: HectonMarineSnowRenderer
            BootstrapParticles(clampedParticleCount);
            GraphicsBufferUploadUtility.UploadArray(_particleBufferA, _bootstrapParticles, clampedParticleCount);
            GraphicsBufferUploadUtility.UploadArray(_particleBufferB, _bootstrapParticles, clampedParticleCount);
            _frameParity = 0;
            _buffersReady = true;
            _staticBindingsDirty = true;
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
                float angle = seed0 * math.PI * 2f;
                float radius = math.lerp(respawnMinRadius, respawnMaxRadius, math.sqrt(seed1));
                float height = math.lerp(minVertical, maxVertical, seed2);
                Vector3 position = cameraPosition + new Vector3(math.cos(angle), 0f, math.sin(angle)) * radius;
                position.y = cameraPosition.y + height;
                float baseSpeed = math.lerp(descentMinSpeed, descentMaxSpeed, seed3);
                float size = math.lerp(particleSizeMin, particleSizeMax, HashToFloat01((uint)index, 0x9B05688Cu));

                _bootstrapParticles[index] = new ParticleGpuData
                {
                    PositionWS = position,
                    Life = 1f,
                    VelocityWS = new Vector3(0f, -baseSpeed, 0f),
                    Size = size,
                    PreviousPositionWS = position,
                    Flags = ParticleFlagSnow,
                    Uv = new Vector2(seed0, seed1),
                    Pad = new Vector2(seed2, seed3)
                };
            }
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

            if (_particleBufferA == null || _particleBufferB == null || _frameConstantsBuffer == null)
                return;

            marineSnowCompute.SetBuffer(_kernelIndex, ShaderIds.ParticlesReadId, _particleBufferA);
            marineSnowCompute.SetBuffer(_kernelIndex, ShaderIds.ParticlesWriteId, _particleBufferB);
            if (_flowFieldBuffer != null)
                marineSnowCompute.SetBuffer(_kernelIndex, ShaderIds.FlowFieldId, _flowFieldBuffer);
            marineSnowCompute.SetBuffer(_kernelIndex, ShaderIds.FrameConstantsId, _frameConstantsBuffer);
            marineSnowCompute.SetVector(
                ShaderIds.DriftParamsId,
                new Vector4(
                    math.min(descentMinSpeed, descentMaxSpeed),
                    math.max(descentMinSpeed, descentMaxSpeed),
                    wanderStrength,
                    baseDragCoefficient));
            marineSnowCompute.SetVector(
                ShaderIds.FlowParamsId,
                new Vector4(
                    flowBlend,
                    densityBiasFlowGain,
                    0.15f,
                    0f));

            marineSnowMaterial.SetBuffer(ShaderIds.FrameConstantsId, _frameConstantsBuffer);
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

        private void UpdateFrameConstants(float dt)
        {
            _simulationTime += dt;
            if (_simulationTime >= 60f)
                _simulationTime -= 60f;

            Vector3 cameraPosition = targetCamera.position;
            Vector3 cameraRight = targetCamera.right;
            Vector3 cameraUp = targetCamera.up;
            float densityScale = _underwaterActive
                ? math.saturate(_visualDensityScale + (_lastSubmergeImpulse * 0.35f))
                : 0f;
            float activeFlag = _underwaterActive && densityScale > 0.0001f ? 1f : 0f;

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
                    activeFlag)
            };

            GraphicsBufferUploadUtility.UploadArray(_frameConstantsBuffer, _frameConstantsUpload, 1);
            marineSnowCompute.SetVector(ShaderIds.FlowSynchronyParamsId, ResolveFlowSynchronyParams());
        }

        private static Vector4 ResolveFlowSynchronyParams()
        {
            Vector4 synchronyParams = Shader.GetGlobalVector(ShaderIds.FlowSynchronyParamsId);
            if (synchronyParams.x <= 0f)
                return new Vector4(1f, 0.26f, 0f, 0f);

            return synchronyParams;
        }

        private void DispatchSimulation()
        {
            GraphicsBuffer readBuffer = _frameParity == 0 ? _particleBufferA : _particleBufferB;
            GraphicsBuffer writeBuffer = _frameParity == 0 ? _particleBufferB : _particleBufferA;
            marineSnowCompute.SetBuffer(_kernelIndex, ShaderIds.ParticlesReadId, readBuffer);
            marineSnowCompute.SetBuffer(_kernelIndex, ShaderIds.ParticlesWriteId, writeBuffer);
            if (_flowFieldBuffer != null)
                marineSnowCompute.SetBuffer(_kernelIndex, ShaderIds.FlowFieldId, _flowFieldBuffer);

            int groupCount = (_activeParticleCount + ThreadGroupSize - 1) / ThreadGroupSize;
            marineSnowCompute.Dispatch(_kernelIndex, groupCount, 1, 1);

            marineSnowMaterial.SetBuffer(ShaderIds.ParticlesRenderId, writeBuffer);
        }

        private void RenderMarineSnow()
        {
            if (_targetCameraComponent == null || marineSnowMaterial == null)
                return;

            Vector3 cameraPosition = targetCamera.position;
            float verticalSize = math.max(1f, math.abs(verticalSpan.y - verticalSpan.x));
            _drawBounds = new Bounds(
                cameraPosition + new Vector3(0f, (verticalSpan.x + verticalSpan.y) * 0.5f, 0f),
                new Vector3(outerRadius * 2f, verticalSize, outerRadius * 2f));

            RenderParams renderParams = new RenderParams(marineSnowMaterial)
            {
                worldBounds = _drawBounds,
                shadowCastingMode = shadowCastingMode,
                receiveShadows = false,
                layer = gameObject.layer,
                camera = _targetCameraComponent,
                lightProbeUsage = LightProbeUsage.Off
            };
            Graphics.RenderPrimitives(renderParams, MeshTopology.Triangles, 6, _activeParticleCount);
        }

        private void ReleaseBuffers()
        {
            ReleaseBuffer(ref _particleBufferA);
            ReleaseBuffer(ref _particleBufferB);
            ReleaseBuffer(ref _flowFieldBuffer);
            ReleaseBuffer(ref _frameConstantsBuffer);
            _buffersReady = false;
            _kernelIndex = -1;
            _bootstrapParticles = null;
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

        private int ResolveActiveParticleCount()
        {
            int capacity = math.max(64, maxParticles);
            float budgetScale = 1f;

            DynamicResolutionScaler scaler = DynamicResolutionScaler.Instance;
            float renderScale = scaler != null ? math.saturate(scaler.CurrentRenderScale) : 1f;
            budgetScale *= math.clamp(renderScale, 0.45f, 1f);
            _debugAdaptiveRenderScale = renderScale;

            VRAMMonitor vramMonitor = VRAMMonitor.Instance;
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
            _debugAdaptiveBudgetScale = budgetScale;

            int resolvedCount = math.clamp((int)math.round(capacity * budgetScale), 64, capacity);
            _debugActiveParticleCount = resolvedCount;
            return resolvedCount;
        }
    }
}



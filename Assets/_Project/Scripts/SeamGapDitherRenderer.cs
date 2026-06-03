using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.SaveSystem;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    /// <summary>
    /// GPU-instanced seam-dither renderer that masks residual terrain/voxel microgaps.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4027)]
    public sealed class SeamGapDitherRenderer : MonoBehaviour, IUpdatable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private static readonly int _MatrixBufferId = Shader.PropertyToID("_HectonSeamDitherMatrices");
        private static readonly int _ColorBufferId = Shader.PropertyToID("_HectonSeamDitherColors");
        private static readonly int _CameraPositionId = Shader.PropertyToID("_SeamDitherCameraPositionWS");
        private static readonly int _MaxCameraDistanceId = Shader.PropertyToID("_MaxCameraDistance");
        private static readonly int _BaseTintId = Shader.PropertyToID("_BaseTint");
        private const string LegacyGapDitherName = "__SEAM_DITHER";
        private const int MaxMotesPerChunk = 256;
        private const float CurrentFadeInvSpeedSq = 0.16f;
        [Header("References")]
        [SerializeField] private SeamRegistry seamRegistry;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Material seamDitherMaterial;
        [SerializeField] private Mesh seamDitherQuadMesh;
        [SerializeField] private CaveBiomeTemplate defaultBiomeTemplate;
        [SerializeField] private CaveBiomeTemplate[] biomeTemplates;
        [SerializeField] private WorldGenerativeGeologyIntegrationDirector integrationDirector;

        [Header("Rendering")]
        [SerializeField, Min(8)] private int maxInstances = 512;
        [SerializeField, Min(1)] private int minInstancesPerSeam = 4;
        [SerializeField, Min(1)] private int maxInstancesPerSeam = 24;
        [SerializeField, Min(0.1f)] private float instanceSpacing = 0.65f;
        [SerializeField, Min(0.01f)] private float moteSize = 0.18f;
        [SerializeField, Min(0.01f)] private float lateralJitter = 0.18f;
        [SerializeField, Min(0.01f)] private float verticalJitter = 0.12f;
        [SerializeField, Min(0.5f)] private float maxCameraDistance = 15f;
        [SerializeField, Min(0.5f)] private float segmentLengthBias = 2.5f;

        [Header("Flora Root Gap Motes")]
        [Tooltip("When enabled, the indirect dither pass also emits capped bioluminescent motes around underwater macro-flora root contacts.")]
        [SerializeField] private bool includeFloraRootMotes = true;

        [Tooltip("Maximum flora-root motes appended to the existing seam dither indirect draw.")]
        [SerializeField, Min(1)] private int maxFloraRootMotes = 128;

        [Tooltip("Stride through active underwater vegetation instances. Higher values lower CPU upload cost.")]
        [SerializeField, Min(1)] private int floraRootInstanceStride = 4;

        [Tooltip("Number of contact-ring motes emitted per accepted macro-flora instance.")]
        [SerializeField, Range(1, 4)] private int floraRootMotesPerPlant = 4;

        [Tooltip("Base footprint radius used when placing root-contact motes around a vegetation pivot.")]
        [SerializeField, Min(0.01f)] private float floraRootFootprintRadiusMeters = 0.35f;

        [Tooltip("Small upward offset to keep root motes from z-fighting with seabed geometry.")]
        [SerializeField, Min(0f)] private float floraRootSurfaceLiftMeters = 0.035f;

        [Tooltip("Tint used for flora-root contact dust motes.")]
        [SerializeField] private Color floraRootDustColor = new Color(0.24f, 0.95f, 0.78f, 0.65f);

        [Header("Diagnostics")]
        [SerializeField] private bool _debugReady;
        [SerializeField] private int _debugRenderedInstances;
        [SerializeField] private int _debugSourceSeams;
        [SerializeField] private Bounds _debugDrawBounds;

        // COLD ALLOC: List<ProceduralGeologySeamStateDTO>[512] - caller-owned seam snapshot staging for indirect dither generation - owner: SeamGapDitherRenderer
        private readonly List<ProceduralGeologySeamStateDTO> _stateScratch = new List<ProceduralGeologySeamStateDTO>(ProceduralWorldStateDTO.MaxGeologySeamStates);
        // COLD ALLOC: List<WorldGenerativeGeologySeamRuntime>[128] - seam runtime traversal buffer used to disable legacy particle gap dithers - owner: SeamGapDitherRenderer
        private readonly List<WorldGenerativeGeologySeamRuntime> _legacyRuntimeScratch = new List<WorldGenerativeGeologySeamRuntime>(128);
        // COLD ALLOC: IndirectDrawIndexedArgs[1] - indirect draw argument upload cache for seam dither - owner: SeamGapDitherRenderer
        private readonly GraphicsBuffer.IndirectDrawIndexedArgs[] _argsUpload = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
        private Matrix4x4[] _matrixUpload;
        private Vector4[] _colorUpload;
        private GraphicsBuffer _matrixBufferA;
        private GraphicsBuffer _matrixBufferB;
        private GraphicsBuffer _activeMatrixBuffer;
        private GraphicsBuffer _colorBufferA;
        private GraphicsBuffer _colorBufferB;
        private GraphicsBuffer _activeColorBuffer;
        private GraphicsBuffer _argsBufferA;
        private GraphicsBuffer _argsBufferB;
        private GraphicsBuffer _activeArgsBuffer;
        private Mesh _quadMesh;
        // COLD ALLOC: MaterialPropertyBlock - per-draw seam dither parameters without mutating the shared/runtime material.
        private MaterialPropertyBlock _drawPropertyBlock;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private IAmbientCurrentReadModel _ambientCurrentReadModel;
        private HectonMapMagicVegetationBridge _vegetationBridge;
        private bool _registeredToDispatcher;
        private bool _registeredLateFrame;
        private int _pendingVisualInstanceCount;
        private bool _pendingVisualDrawDirty;
        private bool _hotSwapRegistered;
        private int _visualUploadBufferIndex;
        private float _nextLegacyVfxDisableTime = float.NegativeInfinity;
        private bool _loggedMissingSeamDitherMaterial;

        /// <summary>
        /// Injects the active seam registry owner.
        /// </summary>
        public void SetSeamRegistry(SeamRegistry registry)
        {
            seamRegistry = registry;
        }

        private void Awake()
        {
            ResolveReferencesCold();
            EnsureRenderingResourcesCold();
        }

        private void OnEnable()
        {
            ResolveReferencesCold();
            EnsureRenderingResourcesCold();
            TryRegisterHotSwapListener();
            TryRegister();
        }

        private void Start()
        {
            ResolveReferencesCold();
            EnsureRenderingResourcesCold();
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
            ReleaseBuffers();
            ReleaseRuntimeMaterial();
            ReleaseQuadMesh();
        }

        public void Tick(float deltaTime)
        {
        }

        private void RunSeamDitherVisualSync()
        {
            ResolveReferencesFromCache();
            DisableLegacyGapDitherIfNeeded();
            if (!EnsureRenderingResources())
            {
                _debugReady = false;
                _debugRenderedInstances = 0;
                _pendingVisualDrawDirty = false;
                _pendingVisualInstanceCount = 0;
                return;
            }

            int instanceCount = BuildInstances();
            _debugRenderedInstances = instanceCount;
            _debugSourceSeams = _stateScratch.Count;
            _debugReady = instanceCount > 0;
            if (instanceCount <= 0)
            {
                _pendingVisualDrawDirty = false;
                _pendingVisualInstanceCount = 0;
                return;
            }

            _pendingVisualInstanceCount = instanceCount;
            _pendingVisualDrawDirty = true;
        }

        private void FlushQueuedSeamDitherVisuals()
        {
            if (!_pendingVisualDrawDirty)
                return;

            int instanceCount = _pendingVisualInstanceCount;
            _pendingVisualDrawDirty = false;
            _pendingVisualInstanceCount = 0;
            if (instanceCount <= 0 || !EnsureRenderingResources())
                return;

            GraphicsBuffer matrixWriteBuffer = ResolveMatrixWriteBuffer();
            GraphicsBuffer colorWriteBuffer = ResolveColorWriteBuffer();
            GraphicsBuffer argsWriteBuffer = ResolveArgsWriteBuffer();
            if (matrixWriteBuffer == null || colorWriteBuffer == null || argsWriteBuffer == null)
                return;

            GraphicsBufferUploadUtility.UploadArray(matrixWriteBuffer, _matrixUpload, instanceCount);
            GraphicsBufferUploadUtility.UploadArray(colorWriteBuffer, _colorUpload, instanceCount);

            _argsUpload[0].indexCountPerInstance = _quadMesh != null ? _quadMesh.GetIndexCount(0) : 0u;
            _argsUpload[0].instanceCount = (uint)instanceCount;
            _argsUpload[0].startIndex = _quadMesh != null ? _quadMesh.GetIndexStart(0) : 0u;
            _argsUpload[0].baseVertexIndex = _quadMesh != null ? _quadMesh.GetBaseVertex(0) : 0u;
            _argsUpload[0].startInstance = 0u;
            GraphicsBufferUploadUtility.UploadArray(argsWriteBuffer, _argsUpload, 1);
            _activeMatrixBuffer = matrixWriteBuffer;
            _activeColorBuffer = colorWriteBuffer;
            _activeArgsBuffer = argsWriteBuffer;
            _visualUploadBufferIndex ^= 1;

            Material drawMaterial = ResolveMaterial();
            EnsureDrawPropertyBlockCold();
            _drawPropertyBlock.Clear();
            _drawPropertyBlock.SetBuffer(_MatrixBufferId, _activeMatrixBuffer);
            _drawPropertyBlock.SetBuffer(_ColorBufferId, _activeColorBuffer);
            _drawPropertyBlock.SetVector(_CameraPositionId, ResolveCameraRuntimePosition(targetCamera));
            _drawPropertyBlock.SetFloat(_MaxCameraDistanceId, Mathf.Max(0.5f, maxCameraDistance));

            UnityEngine.Graphics.DrawMeshInstancedIndirect(
                _quadMesh,
                0,
                drawMaterial,
                _debugDrawBounds,
                _activeArgsBuffer,
                0,
                _drawPropertyBlock,
                ShadowCastingMode.Off,
                false,
                gameObject.layer,
                targetCamera);
        }

        private static Vector3 ResolveCameraRuntimePosition(Camera targetCamera)
        {
            return targetCamera != null && targetCamera.transform != null
                ? targetCamera.transform.position
                : Vector3.zero;
        }

        public void LateFrameTick()
        {
            RunSeamDitherVisualSync();
            FlushQueuedSeamDitherVisuals();
        }

        private void ResolveReferencesCold()
        {
            if (seamRegistry == null)
                seamRegistry = SeamRegistry.ActiveRuntimeInstance;

            CachePlayerRuntimeContext(GlobalRegistry.Player);
            _ambientCurrentReadModel = GlobalRegistry.AmbientCurrent;
            _vegetationBridge = GlobalRegistry.MapMagicVegetation;

            if (playerTransform == null)
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);

            if (integrationDirector == null)
                WorldRuntimeReferenceUtility.TryResolveWorldGenerativeGeologyIntegrationDirector(ref integrationDirector);
        }

        private void ResolveReferencesFromCache()
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext == null)
                return;

            if (playerTransform == null)
                playerTransform = playerContext.PlayerTransform;

            if (targetCamera == null)
                targetCamera = playerContext.PlayerCamera;
        }

        private void TryRegister()
        {
            if (!Application.isPlaying)
                return;

            if (!_registeredToDispatcher)
                _registeredToDispatcher = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);

            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

            if (_registeredToDispatcher)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredToDispatcher = false;
            }
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Player:
                    ClearPlayerRuntimeContext(previousService as IPlayerRuntimeContext);
                    CachePlayerRuntimeContext(currentService as IPlayerRuntimeContext);
                    break;
                case GlobalRegistryServiceSlot.FluidRuntime:
                    _ambientCurrentReadModel = currentService as IAmbientCurrentReadModel;
                    break;
                case GlobalRegistryServiceSlot.MapMagicVegetationRuntime:
                    _vegetationBridge = currentService as HectonMapMagicVegetationBridge;
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    _registeredToDispatcher = false;
                    _registeredLateFrame = false;
                    if (currentService != null && isActiveAndEnabled)
                        TryRegister();
                    break;
            }
        }

        private void CachePlayerRuntimeContext(IPlayerRuntimeContext playerContext)
        {
            _playerRuntimeContext = playerContext;
            if (playerContext == null)
                return;

            if (playerTransform == null)
                playerTransform = playerContext.PlayerTransform;

            if (targetCamera == null)
                targetCamera = playerContext.PlayerCamera;
        }

        private void ClearPlayerRuntimeContext(IPlayerRuntimeContext previousContext)
        {
            if (previousContext == null)
                return;

            if (ReferenceEquals(playerTransform, previousContext.PlayerTransform))
                playerTransform = null;

            if (ReferenceEquals(targetCamera, previousContext.PlayerCamera))
                targetCamera = null;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private bool EnsureRenderingResources()
        {
            if (seamRegistry == null || targetCamera == null)
                return false;

            return AreRenderingResourcesResident();
        }

        private void EnsureRenderingResourcesCold()
        {
            EnsureDrawPropertyBlockCold();
            EnsureCpuCapacity();
            EnsureQuadMesh();
            ResolveMaterial();
            if (Application.isPlaying)
                EnsureBuffers();
        }

        private void EnsureDrawPropertyBlockCold()
        {
            if (_drawPropertyBlock != null)
                return;

            // COLD ALLOC: MaterialPropertyBlock[1] - per-draw seam dither parameters - owner: SeamGapDitherRenderer.
            _drawPropertyBlock = new MaterialPropertyBlock();
        }

        private bool AreRenderingResourcesResident()
        {
            int requiredCapacity = Mathf.Clamp(maxInstances, 8, MaxMotesPerChunk);
            return _quadMesh != null &&
                   seamDitherMaterial != null &&
                   _matrixUpload != null &&
                   _matrixUpload.Length == requiredCapacity &&
                   _colorUpload != null &&
                   _colorUpload.Length == requiredCapacity &&
                   _activeMatrixBuffer != null &&
                   _activeMatrixBuffer.count == requiredCapacity &&
                   _activeColorBuffer != null &&
                   _activeColorBuffer.count == requiredCapacity &&
                   _activeArgsBuffer != null &&
                   _matrixBufferA != null &&
                   _matrixBufferA.count == requiredCapacity &&
                   _matrixBufferB != null &&
                   _matrixBufferB.count == requiredCapacity &&
                   _colorBufferA != null &&
                   _colorBufferA.count == requiredCapacity &&
                   _colorBufferB != null &&
                   _colorBufferB.count == requiredCapacity &&
                   _argsBufferA != null &&
                   _argsBufferB != null;
        }

        private void EnsureCpuCapacity()
        {
            int clampedCapacity = Mathf.Clamp(maxInstances, 8, MaxMotesPerChunk);
            if (_matrixUpload == null || _matrixUpload.Length != clampedCapacity)
            {
                // COLD ALLOC: Matrix4x4[MaxMotesPerChunk] - capped per-frame seam dither transform upload cache - owner: SeamGapDitherRenderer
                _matrixUpload = new Matrix4x4[clampedCapacity];
            }

            if (_colorUpload == null || _colorUpload.Length != clampedCapacity)
            {
                // COLD ALLOC: Vector4[MaxMotesPerChunk] - capped per-frame seam dither tint upload cache - owner: SeamGapDitherRenderer
                _colorUpload = new Vector4[clampedCapacity];
            }
        }

        private void EnsureQuadMesh()
        {
            _quadMesh = seamDitherQuadMesh;
        }

        private void EnsureBuffers()
        {
            int requiredCapacity = _matrixUpload != null ? _matrixUpload.Length : Mathf.Clamp(maxInstances, 8, MaxMotesPerChunk);
            if (_matrixBufferA == null || _matrixBufferA.count != requiredCapacity ||
                _matrixBufferB == null || _matrixBufferB.count != requiredCapacity)
            {
                ReleaseBuffer(ref _matrixBufferA);
                ReleaseBuffer(ref _matrixBufferB);
                _matrixBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Matrix4x4>(requiredCapacity); // COLD ALLOC: GraphicsBuffer[MaxMotesPerChunk] - seam dither matrix upload buffer A - owner: SeamGapDitherRenderer
                _matrixBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Matrix4x4>(requiredCapacity); // COLD ALLOC: GraphicsBuffer[MaxMotesPerChunk] - seam dither matrix upload buffer B - owner: SeamGapDitherRenderer
                _activeMatrixBuffer = _matrixBufferA;
                _visualUploadBufferIndex = 0;
            }

            if (_colorBufferA == null || _colorBufferA.count != requiredCapacity ||
                _colorBufferB == null || _colorBufferB.count != requiredCapacity)
            {
                ReleaseBuffer(ref _colorBufferA);
                ReleaseBuffer(ref _colorBufferB);
                _colorBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Vector4>(requiredCapacity); // COLD ALLOC: GraphicsBuffer[MaxMotesPerChunk] - seam dither tint upload buffer A - owner: SeamGapDitherRenderer
                _colorBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Vector4>(requiredCapacity); // COLD ALLOC: GraphicsBuffer[MaxMotesPerChunk] - seam dither tint upload buffer B - owner: SeamGapDitherRenderer
                _activeColorBuffer = _colorBufferA;
                _visualUploadBufferIndex = 0;
            }

            if (_argsBufferA == null || _argsBufferB == null)
            {
                ReleaseBuffer(ref _argsBufferA);
                ReleaseBuffer(ref _argsBufferB);
                _argsBufferA = new GraphicsBuffer(
                    GraphicsBuffer.Target.IndirectArguments,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - seam dither indirect indexed draw arguments A - owner: SeamGapDitherRenderer
                _argsBufferB = new GraphicsBuffer(
                    GraphicsBuffer.Target.IndirectArguments,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - seam dither indirect indexed draw arguments B - owner: SeamGapDitherRenderer
                _activeArgsBuffer = _argsBufferA;
                _visualUploadBufferIndex = 0;
            }
        }

        private GraphicsBuffer ResolveMatrixWriteBuffer()
        {
            return (_visualUploadBufferIndex & 1) == 0 ? _matrixBufferB : _matrixBufferA;
        }

        private GraphicsBuffer ResolveColorWriteBuffer()
        {
            return (_visualUploadBufferIndex & 1) == 0 ? _colorBufferB : _colorBufferA;
        }

        private GraphicsBuffer ResolveArgsWriteBuffer()
        {
            return (_visualUploadBufferIndex & 1) == 0 ? _argsBufferB : _argsBufferA;
        }

        private Material ResolveMaterial()
        {
            if (seamDitherMaterial != null)
                return seamDitherMaterial;

            if (!_loggedMissingSeamDitherMaterial)
            {
                _loggedMissingSeamDitherMaterial = true;
                LogMissingSeamDitherMaterial(this);
            }

            return null;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMissingSeamDitherMaterial(UnityEngine.Object context)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogError("[SeamGapDitherRenderer] Missing seamDitherMaterial asset. Runtime material creation is forbidden for seam gap indirect draws.", context);
#endif
        }

        private int BuildInstances()
        {
            seamRegistry.CopyStatesTo(_stateScratch);

            Quaternion billboardRotation = targetCamera.transform.rotation;
            Vector3 cameraPosition = targetCamera.transform.position;
            float maxDistanceSq = Mathf.Max(0.5f, maxCameraDistance);
            maxDistanceSq *= maxDistanceSq;
            int maxCount = _matrixUpload.Length;
            int instanceCount = 0;
            Vector3 boundsMin = default;
            Vector3 boundsMax = default;
            bool hasBounds = false;

            for (int seamIndex = 0; seamIndex < _stateScratch.Count && instanceCount < maxCount; seamIndex++)
            {
                ProceduralGeologySeamStateDTO state = _stateScratch[seamIndex];
                Vector3 surfaceAbsolute = new Vector3(
                    state.absolutePositionX,
                    state.absoluteSeamHeight,
                    state.absolutePositionZ);
                Vector3 centerAbsolute = new Vector3(
                    state.absoluteVoxelCenterX,
                    state.absoluteSeamHeight,
                    state.absoluteVoxelCenterZ);

                Vector3 segment = centerAbsolute - surfaceAbsolute;
                segment.y = 0f;
                float segmentSqr = LengthSq(segment);
                float segmentLength = ApproximateVectorMagnitude(segment);
                Vector3 forward = segmentSqr > 0.000001f ? segment * math.rsqrt(segmentSqr) : Vector3.forward;
                Vector3 right = new Vector3(-forward.z, 0f, forward.x);
                Color seamColor = ResolveSeamColor(state.runtimeKey);
                float densityScale = ResolveDensityScale(state.runtimeKey);
                int seamInstances = Mathf.Clamp(
                    Mathf.CeilToInt((Mathf.Max(state.seamBlendRadius, segmentLength + segmentLengthBias) / Mathf.Max(0.1f, instanceSpacing)) * densityScale),
                    Mathf.Max(1, minInstancesPerSeam),
                    Mathf.Max(minInstancesPerSeam, maxInstancesPerSeam));

                for (int pointIndex = 0; pointIndex < seamInstances && instanceCount < maxCount; pointIndex++)
                {
                    float t = seamInstances <= 1 ? 0.5f : pointIndex / (float)(seamInstances - 1);
                    float jitterSeed = Hash01(state.runtimeKey, pointIndex, 17);
                    float verticalSeed = Hash01(state.runtimeKey, pointIndex, 29);
                    float scaleSeed = Hash01(state.runtimeKey, pointIndex, 41);

                    Vector3 absolutePoint = surfaceAbsolute + ((centerAbsolute - surfaceAbsolute) * t);
                    absolutePoint += right * (-lateralJitter + (lateralJitter * 2f * jitterSeed));
                    absolutePoint.y += -verticalJitter + (verticalJitter * 2f * verticalSeed);

                    Vector3 runtimePoint = HectonFloatingOrigin.ToRuntimePosition(absolutePoint);
                    if ((runtimePoint - cameraPosition).sqrMagnitude > maxDistanceSq)
                        continue;

                    float scale = moteSize * (0.75f + (0.6f * scaleSeed));
                    Vector3 sampledCurrent = Vector3.zero;
                    IAmbientCurrentReadModel ambientCurrent = _ambientCurrentReadModel;
                    if (ambientCurrent != null)
                        ambientCurrent.TrySampleCombinedCurrent(runtimePoint, out sampledCurrent);
                    float currentSpeedSq = sampledCurrent.x * sampledCurrent.x
                        + sampledCurrent.y * sampledCurrent.y
                        + sampledCurrent.z * sampledCurrent.z;
                    float currentFadeT = math.saturate(currentSpeedSq * CurrentFadeInvSpeedSq);
                    float currentFade = 1f - (0.65f * currentFadeT);
                    Color resolvedColor = seamColor;
                    resolvedColor.a *= currentFade;
                    if (resolvedColor.a <= 0.01f)
                        continue;

                    Vector3 scaleVector = new Vector3(scale, scale, scale);
                    _matrixUpload[instanceCount] = Matrix4x4.TRS(runtimePoint, billboardRotation, scaleVector);
                    _colorUpload[instanceCount] = (Vector4)resolvedColor;

                    Vector3 padding = Vector3.one * scale;
                    Vector3 pointMin = runtimePoint - padding;
                    Vector3 pointMax = runtimePoint + padding;
                    if (!hasBounds)
                    {
                        boundsMin = pointMin;
                        boundsMax = pointMax;
                        hasBounds = true;
                    }
                    else
                    {
                        boundsMin = Vector3.Min(boundsMin, pointMin);
                        boundsMax = Vector3.Max(boundsMax, pointMax);
                    }

                    instanceCount++;
                }
            }

            instanceCount = AppendFloraRootInstances(
                instanceCount,
                maxCount,
                cameraPosition,
                billboardRotation,
                maxDistanceSq,
                ref boundsMin,
                ref boundsMax,
                ref hasBounds);

            if (!hasBounds)
            {
                _debugDrawBounds = new Bounds(cameraPosition, Vector3.zero);
                return 0;
            }

            Vector3 center = (boundsMin + boundsMax) * 0.5f;
            Vector3 size = Vector3.Max(boundsMax - boundsMin, Vector3.one * 0.5f);
            _debugDrawBounds = new Bounds(center, size);
            return instanceCount;
        }

        private int AppendFloraRootInstances(
            int instanceCount,
            int maxCount,
            Vector3 cameraPosition,
            Quaternion billboardRotation,
            float maxDistanceSq,
            ref Vector3 boundsMin,
            ref Vector3 boundsMax,
            ref bool hasBounds)
        {
            if (!includeFloraRootMotes || maxFloraRootMotes <= 0 || instanceCount >= maxCount)
                return instanceCount;

            HectonMapMagicVegetationBridge vegetationBridge = _vegetationBridge;
            if (vegetationBridge == null ||
                !vegetationBridge.TryGetActiveUnderwaterNativePayload(
                    out NativeArray<Matrix4x4> matrices,
                    out NativeArray<HectonVegetationInstanceData> metadata,
                    out _,
                    out int count))
            {
                return instanceCount;
            }

            int limit = Mathf.Min(count, Mathf.Min(matrices.Length, metadata.Length));
            if (limit <= 0)
                return instanceCount;

            int stride = Mathf.Max(1, floraRootInstanceStride);
            int motesPerPlant = Mathf.Clamp(floraRootMotesPerPlant, 1, 4);
            int appendLimit = Mathf.Min(Mathf.Max(0, maxFloraRootMotes), maxCount - instanceCount);
            int appendedCount = 0;
            float footprintRadius = Mathf.Max(0.01f, floraRootFootprintRadiusMeters);
            float surfaceLift = Mathf.Max(0f, floraRootSurfaceLiftMeters);
            Color color = floraRootDustColor;
            if (color.a <= 0.01f)
                return instanceCount;

            for (int sourceIndex = 0; sourceIndex < limit && appendedCount < appendLimit; sourceIndex += stride)
            {
                HectonVegetationInstanceData instanceData = metadata[sourceIndex];
                HectonVegetationInstanceType type = (HectonVegetationInstanceType)Mathf.RoundToInt(instanceData.Type);
                if (type != HectonVegetationInstanceType.GiantKelp && type != HectonVegetationInstanceType.Sargassum)
                    continue;

                Matrix4x4 matrix = matrices[sourceIndex];
                Vector3 root = new Vector3(matrix.m03, matrix.m13, matrix.m23);
                Vector3 right = new Vector3(matrix.m00, matrix.m10, matrix.m20);
                Vector3 forward = new Vector3(matrix.m02, matrix.m12, matrix.m22);
                float rightSqr = LengthSq(right);
                float forwardSqr = LengthSq(forward);
                right = rightSqr > 0.000001f ? right * math.rsqrt(rightSqr) : Vector3.right;
                forward = forwardSqr > 0.000001f ? forward * math.rsqrt(forwardSqr) : Vector3.forward;

                float baseRadius = footprintRadius * Mathf.Max(0.35f, Mathf.Abs(instanceData.WidthScale));
                for (int moteIndex = 0; moteIndex < motesPerPlant && appendedCount < appendLimit && instanceCount < maxCount; moteIndex++)
                {
                    float angle01 = (moteIndex + Hash01(sourceIndex, moteIndex, 83) * 0.22f) / motesPerPlant;
                    float angle = angle01 * Mathf.PI * 2f;
                    Vector3 runtimePoint =
                        root +
                        ((right * MathLodApproximation.ApproxCosBhaskara(angle)) + (forward * MathLodApproximation.ApproxSinBhaskara(angle))) * baseRadius +
                        (Vector3.up * surfaceLift);
                    if ((runtimePoint - cameraPosition).sqrMagnitude > maxDistanceSq)
                        continue;

                    float scale = moteSize * (0.45f + (0.45f * Hash01(sourceIndex, moteIndex, 97)));
                    _matrixUpload[instanceCount] = Matrix4x4.TRS(runtimePoint, billboardRotation, Vector3.one * scale);
                    _colorUpload[instanceCount] = (Vector4)color;
                    IncludeInstanceBounds(runtimePoint, scale, ref boundsMin, ref boundsMax, ref hasBounds);
                    instanceCount++;
                    appendedCount++;
                }
            }

            return instanceCount;
        }

        private static void IncludeInstanceBounds(
            Vector3 runtimePoint,
            float scale,
            ref Vector3 boundsMin,
            ref Vector3 boundsMax,
            ref bool hasBounds)
        {
            Vector3 padding = Vector3.one * scale;
            Vector3 pointMin = runtimePoint - padding;
            Vector3 pointMax = runtimePoint + padding;
            if (!hasBounds)
            {
                boundsMin = pointMin;
                boundsMax = pointMax;
                hasBounds = true;
                return;
            }

            boundsMin = Vector3.Min(boundsMin, pointMin);
            boundsMax = Vector3.Max(boundsMax, pointMax);
        }

        private static float LengthSq(Vector3 value)
        {
            return value.x * value.x + value.y * value.y + value.z * value.z;
        }

        private static float ApproximateVectorMagnitude(Vector3 value)
        {
            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float az = math.abs(value.z);
            float max = math.max(ax, math.max(ay, az));
            float min = math.min(ax, math.min(ay, az));
            float mid = ax + ay + az - max - min;
            return max + (mid * 0.375f) + (min * 0.125f);
        }

        private Color ResolveSeamColor(long runtimeKey)
        {
            CaveBiomeTemplate biomeTemplate = ResolveBiomeTemplate(runtimeKey);
            if (biomeTemplate == null)
                return new Color(0.28f, 0.92f, 1f, 0.8f);

            Color dust = biomeTemplate.SeamDitherDustColor;
            Color emissive = biomeTemplate.EmissiveRockColor;
            return new Color(
                dust.r + ((emissive.r - dust.r) * 0.18f),
                dust.g + ((emissive.g - dust.g) * 0.18f),
                dust.b + ((emissive.b - dust.b) * 0.18f),
                dust.a + ((emissive.a - dust.a) * 0.18f));
        }

        private float ResolveDensityScale(long runtimeKey)
        {
            CaveBiomeTemplate biomeTemplate = ResolveBiomeTemplate(runtimeKey);
            if (biomeTemplate == null)
                return 1f;

            return Mathf.Clamp(biomeTemplate.StalactiteDensity, 0.5f, 2f);
        }

        private CaveBiomeTemplate ResolveBiomeTemplate(long runtimeKey)
        {
            WorldGenerativeGeologyIntegrationDirector integrationDirector = this.integrationDirector;
            if (integrationDirector != null && integrationDirector.TryGetPlan(runtimeKey, out WorldGenerativeGeologySeamPlan plan))
            {
                string geologyProfileId = plan.geologyProfileId;
                if (!string.IsNullOrEmpty(geologyProfileId) && biomeTemplates != null)
                {
                    for (int i = 0; i < biomeTemplates.Length; i++)
                    {
                        CaveBiomeTemplate candidate = biomeTemplates[i];
                        if (candidate == null || candidate.GeologyProfileId != geologyProfileId)
                            continue;

                        return candidate;
                    }
                }
            }

            return defaultBiomeTemplate;
        }

        private void DisableLegacyGapDitherIfNeeded()
        {
            float now = Application.isPlaying ? (float)SystemDispatcher.CurrentUnscaledTimeSeconds : 0f;
            if (now < _nextLegacyVfxDisableTime)
                return;

            _nextLegacyVfxDisableTime = now + 1f;
            WorldGenerativeGeologySeamRuntime.CopyActiveRuntimesTo(_legacyRuntimeScratch);
            for (int i = 0; i < _legacyRuntimeScratch.Count; i++)
            {
                WorldGenerativeGeologySeamRuntime runtime = _legacyRuntimeScratch[i];
                if (runtime == null)
                    continue;

                Transform legacy = runtime.transform.Find(LegacyGapDitherName);
                if (legacy != null && legacy.gameObject.activeSelf)
                    legacy.gameObject.SetActive(false);
            }
        }

        private void ReleaseBuffers()
        {
            ReleaseBuffer(ref _matrixBufferA);
            ReleaseBuffer(ref _matrixBufferB);
            ReleaseBuffer(ref _colorBufferA);
            ReleaseBuffer(ref _colorBufferB);
            ReleaseBuffer(ref _argsBufferA);
            ReleaseBuffer(ref _argsBufferB);
            _activeMatrixBuffer = null;
            _activeColorBuffer = null;
            _activeArgsBuffer = null;
            _visualUploadBufferIndex = 0;
            if (_drawPropertyBlock != null)
                _drawPropertyBlock.Clear();
        }

        private void ReleaseRuntimeMaterial()
        {
        }

        private void ReleaseQuadMesh()
        {
            _quadMesh = null;
        }

        private static void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private static float Hash01(long runtimeKey, int index, int salt)
        {
            unchecked
            {
                uint value = (uint)(runtimeKey * 1103515245L + index * 92821L + salt * 12345L);
                value ^= value >> 16;
                value *= 2246822519u;
                value ^= value >> 13;
                return (value & 0x00FFFFFFu) * (1f / 16777215f);
            }
        }
    }
}

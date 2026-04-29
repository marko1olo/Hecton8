using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Physics;
using Hecton8.SaveSystem;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    /// <summary>
    /// GPU-instanced seam-dither renderer that masks residual terrain/voxel microgaps.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4027)]
    public sealed class SeamGapDitherRenderer : MonoBehaviour, IUpdatable
    {
        private static readonly int _MatrixBufferId = Shader.PropertyToID("_HectonSeamDitherMatrices");
        private static readonly int _ColorBufferId = Shader.PropertyToID("_HectonSeamDitherColors");
        private static readonly int _CameraPositionId = Shader.PropertyToID("_SeamDitherCameraPositionWS");
        private static readonly int _MaxCameraDistanceId = Shader.PropertyToID("_MaxCameraDistance");
        private static readonly int _BaseTintId = Shader.PropertyToID("_BaseTint");
        private const string LegacyGapDitherName = "__SEAM_DITHER";
        [Header("References")]
        [SerializeField] private SeamRegistry seamRegistry;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Material seamDitherMaterial;
        [SerializeField] private CaveBiomeTemplate defaultBiomeTemplate;
        [SerializeField] private CaveBiomeTemplate[] biomeTemplates;

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
        private GraphicsBuffer _matrixBuffer;
        private GraphicsBuffer _colorBuffer;
        private GraphicsBuffer _argsBuffer;
        private Mesh _quadMesh;
        private bool _registeredToDispatcher;
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
            ResolveReferences();
            EnsureCpuCapacity();
            EnsureQuadMesh();
        }

        private void OnEnable()
        {
            ResolveReferences();
            EnsureCpuCapacity();
            EnsureQuadMesh();
            TryRegister();
        }

        private void Start()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryUnregister();
            ReleaseBuffers();
            ReleaseRuntimeMaterial();
            ReleaseQuadMesh();
        }

        public void Tick(float deltaTime)
        {
            ResolveReferences();
            DisableLegacyGapDitherIfNeeded();
            if (!EnsureRenderingResources())
            {
                _debugReady = false;
                _debugRenderedInstances = 0;
                return;
            }

            int instanceCount = BuildInstances();
            _debugRenderedInstances = instanceCount;
            _debugSourceSeams = _stateScratch.Count;
            _debugReady = instanceCount > 0;
            if (instanceCount <= 0)
                return;

            GraphicsBufferUploadUtility.UploadArray(_matrixBuffer, _matrixUpload, instanceCount);
            GraphicsBufferUploadUtility.UploadArray(_colorBuffer, _colorUpload, instanceCount);

            _argsUpload[0].indexCountPerInstance = _quadMesh != null ? _quadMesh.GetIndexCount(0) : 0u;
            _argsUpload[0].instanceCount = (uint)instanceCount;
            _argsUpload[0].startIndex = _quadMesh != null ? _quadMesh.GetIndexStart(0) : 0u;
            _argsUpload[0].baseVertexIndex = _quadMesh != null ? _quadMesh.GetBaseVertex(0) : 0u;
            _argsUpload[0].startInstance = 0u;
            _argsBuffer.SetData(_argsUpload);

            Material drawMaterial = ResolveMaterial();
            drawMaterial.SetBuffer(_MatrixBufferId, _matrixBuffer);
            drawMaterial.SetBuffer(_ColorBufferId, _colorBuffer);
            drawMaterial.SetVector(_CameraPositionId, targetCamera.transform.position);
            drawMaterial.SetFloat(_MaxCameraDistanceId, Mathf.Max(0.5f, maxCameraDistance));

            Graphics.DrawMeshInstancedIndirect(
                _quadMesh,
                0,
                drawMaterial,
                _debugDrawBounds,
                _argsBuffer,
                0,
                null,
                ShadowCastingMode.Off,
                false,
                gameObject.layer,
                targetCamera);
        }

        private void ResolveReferences()
        {
            if (seamRegistry == null)
                seamRegistry = SeamRegistry.ActiveRuntimeInstance;

            if (playerTransform == null)
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);

            if (targetCamera == null && GlobalRegistry.Player != null)
                targetCamera = GlobalRegistry.Player.PlayerCamera;
        }

        private void TryRegister()
        {
            if (_registeredToDispatcher)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _registeredToDispatcher = true;
        }

        private void TryUnregister()
        {
            if (!_registeredToDispatcher)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registeredToDispatcher = false;
        }

        private bool EnsureRenderingResources()
        {
            if (seamRegistry == null || targetCamera == null)
                return false;

            EnsureCpuCapacity();
            EnsureQuadMesh();
            EnsureBuffers();
            return _quadMesh != null && ResolveMaterial() != null && _matrixBuffer != null && _colorBuffer != null && _argsBuffer != null;
        }

        private void EnsureCpuCapacity()
        {
            int clampedCapacity = Mathf.Clamp(maxInstances, 8, 4096);
            if (_matrixUpload == null || _matrixUpload.Length != clampedCapacity)
            {
                // COLD ALLOC: Matrix4x4[maxInstances] - per-frame seam dither transform upload cache - owner: SeamGapDitherRenderer
                _matrixUpload = new Matrix4x4[clampedCapacity];
            }

            if (_colorUpload == null || _colorUpload.Length != clampedCapacity)
            {
                // COLD ALLOC: Vector4[maxInstances] - per-frame seam dither tint upload cache - owner: SeamGapDitherRenderer
                _colorUpload = new Vector4[clampedCapacity];
            }
        }

        private void EnsureQuadMesh()
        {
            if (_quadMesh != null)
                return;

            _quadMesh = new Mesh
            {
                name = "GEN_SeamGapDitherQuad"
            }; // COLD ALLOC: Mesh[1] - billboard quad used by seam dither indirect draw - owner: SeamGapDitherRenderer
            _quadMesh.SetVertices(new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f)
            });
            _quadMesh.SetUVs(0, new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f)
            });
            _quadMesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0, true);
            _quadMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            _quadMesh.UploadMeshData(false);
        }

        private void EnsureBuffers()
        {
            int requiredCapacity = _matrixUpload != null ? _matrixUpload.Length : Mathf.Clamp(maxInstances, 8, 4096);
            if (_matrixBuffer == null || _matrixBuffer.count != requiredCapacity)
            {
                ReleaseBuffer(ref _matrixBuffer);
                _matrixBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Matrix4x4>(requiredCapacity); // COLD ALLOC: GraphicsBuffer[maxInstances] - seam dither matrix upload buffer - owner: SeamGapDitherRenderer
            }

            if (_colorBuffer == null || _colorBuffer.count != requiredCapacity)
            {
                ReleaseBuffer(ref _colorBuffer);
                _colorBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Vector4>(requiredCapacity); // COLD ALLOC: GraphicsBuffer[maxInstances] - seam dither tint upload buffer - owner: SeamGapDitherRenderer
            }

            if (_argsBuffer == null)
            {
                _argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - seam dither indirect indexed draw arguments - owner: SeamGapDitherRenderer
            }
        }

        private Material ResolveMaterial()
        {
            if (seamDitherMaterial != null)
                return seamDitherMaterial;

            if (!_loggedMissingSeamDitherMaterial)
            {
                _loggedMissingSeamDitherMaterial = true;
                Debug.LogError("[SeamGapDitherRenderer] Missing seamDitherMaterial asset. Runtime material creation is forbidden for seam gap indirect draws.", this);
            }

            return null;
        }

        private int BuildInstances()
        {
            seamRegistry.CopyStatesTo(_stateScratch);
            if (_stateScratch.Count == 0)
            {
                _debugDrawBounds = new Bounds(Vector3.zero, Vector3.zero);
                return 0;
            }

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
                float segmentLength = segment.magnitude;
                Vector3 forward = segmentLength > 0.001f ? segment / segmentLength : Vector3.forward;
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

                    Vector3 absolutePoint = Vector3.Lerp(surfaceAbsolute, centerAbsolute, t);
                    absolutePoint += right * Mathf.Lerp(-lateralJitter, lateralJitter, jitterSeed);
                    absolutePoint.y += Mathf.Lerp(-verticalJitter, verticalJitter, verticalSeed);

                    Vector3 runtimePoint = HectonFloatingOrigin.ToRuntimePosition(absolutePoint);
                    if ((runtimePoint - cameraPosition).sqrMagnitude > maxDistanceSq)
                        continue;

                    float scale = moteSize * Mathf.Lerp(0.75f, 1.35f, scaleSeed);
                    float currentSpeed = CurrentVolume.SampleCombinedCurrent(runtimePoint).magnitude;
                    float currentFade = Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(currentSpeed / 2.5f));
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

        private Color ResolveSeamColor(long runtimeKey)
        {
            CaveBiomeTemplate biomeTemplate = ResolveBiomeTemplate(runtimeKey);
            if (biomeTemplate == null)
                return new Color(0.28f, 0.92f, 1f, 0.8f);

            return Color.Lerp(biomeTemplate.SeamDitherDustColor, biomeTemplate.EmissiveRockColor, 0.18f);
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
            WorldGenerativeGeologyIntegrationDirector integrationDirector = WorldGenerativeGeologyIntegrationDirector.ActiveRuntimeInstance;
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
            float now = Application.isPlaying ? Time.unscaledTime : 0f;
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
            ReleaseBuffer(ref _matrixBuffer);
            ReleaseBuffer(ref _colorBuffer);
            ReleaseBuffer(ref _argsBuffer);
        }

        private void ReleaseRuntimeMaterial()
        {
        }

        private void ReleaseQuadMesh()
        {
            if (_quadMesh == null)
                return;

            if (Application.isPlaying)
                Destroy(_quadMesh);
            else
                DestroyImmediate(_quadMesh);

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

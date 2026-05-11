using System;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Optimization;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.World
{
    /// <summary>
    /// Indirect renderer for dense procedural vegetation driven by external GPU buffers.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-90)]
    public class HectonIndirectVegetationRenderer : MonoBehaviour, ITickable
    {
        /// <summary>Stride of one Matrix4x4 entry expected in the external instance matrix buffer.</summary>
        public const int InstanceMatrixStride = 64;

        /// <summary>Stride of one <see cref="HectonVegetationInstanceData"/> entry expected in the instance metadata buffer.</summary>
        public const int InstanceDataStride = HectonVegetationInstanceData.Stride;

        private const int IndirectArgsCount = 5;
        private const int VisibleIndexStride = sizeof(uint);
        private const int ThreadsPerGroup = 64;
        private const int FrustumPlaneCount = 6;
        private const int BrgMetadataPlaceholderCount = 1;
        private const int MaxVegetationVisibilityPasses = 3;
        private const int MaxVegetationDrawCommands = 7;
        private const float LodTransitionRangeMeters = 2f;
        private const string GpuIndirectKeyword = "HECTON_GPU_INDIRECT";
        private const byte VisibilityMaskNear = 1 << 0;
        private const byte VisibilityMaskFar = 1 << 1;
        private const byte VisibilityMaskShadow = 1 << 2;
#if UNITY_EDITOR
        private const string ComputeShaderAssetPath = "Assets/_Project/Art/Shaders/FloraCulling.compute";
        private const string AbyssalFlowFieldComputeAssetPath = "Assets/_Project/Art/Shaders/AbyssalFlowField.compute";
        private const string DepthPyramidComputeAssetPath = "Assets/_Project/Art/Shaders/Hecton_DepthPyramid.compute";
        private const string VegetationShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_IndirectVegetation.shader";
        private const string DepthShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_IndirectVegetationDepthOnly.shader";
        private const string ShadowShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_IndirectVegetationShadow.shader";
        private const string MotionShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_IndirectVegetationMotionVectors.shader";
#endif
        private const string VegetationShaderName = "Hecton8/Vegetation/IndirectStrip";

        private static readonly int _InstanceMatricesId = Shader.PropertyToID("_HectonInstanceMatrices");
        private static readonly int _InstanceDataId = Shader.PropertyToID("_HectonVegetationInstanceData");
        private static readonly int _FloraPhaseSeedsId = Shader.PropertyToID("_HectonFloraPhaseSeeds");
        private static readonly int _FloraSnapFlagsId = Shader.PropertyToID("_HectonFloraSnapFlags");
        private static readonly int _FloraSnapFlagsEnabledId = Shader.PropertyToID("_HectonFloraSnapFlagsEnabled");
        private static readonly int _VisibleInstanceIndicesId = Shader.PropertyToID("_HectonVisibleInstanceIndices");
        private static readonly int _ChunkWorldOffsetId = Shader.PropertyToID("_ChunkWorldOffset");
        private static readonly int _GlobalFloatingOffsetId = Shader.PropertyToID("_GlobalFloatingOffset");
        private static readonly int _LodPassModeId = Shader.PropertyToID("_HectonLodPassMode");
        private static readonly int _LodNearDistanceId = Shader.PropertyToID("_HectonLodNearDistance");
        private static readonly int _LodFarDistanceId = Shader.PropertyToID("_HectonLodFarDistance");
        private static readonly int _LodTransitionRangeId = Shader.PropertyToID("_HectonLodTransitionRange");
        private static readonly int _ImpostorWidthId = Shader.PropertyToID("_HectonImpostorWidth");
        private static readonly int _ImpostorHeightId = Shader.PropertyToID("_HectonImpostorHeight");
        private static readonly int _SourceInstanceCountId = Shader.PropertyToID("_HectonSourceInstanceCount");
        private static readonly int _ViewProjectionId = Shader.PropertyToID("_HectonViewProjection");
        private static readonly int _ViewMatrixId = Shader.PropertyToID("_HectonViewMatrix");
        private static readonly int _CameraPositionId = Shader.PropertyToID("_HectonCameraPosition");
        private static readonly int _CameraForwardId = Shader.PropertyToID("_HectonCameraForward");
        private static readonly int _CameraDepthTextureId = Shader.PropertyToID("_HectonCameraDepthTexture");
        private static readonly int _DepthPyramidTextureId = Shader.PropertyToID("_HectonDepthPyramid");
        private static readonly int _DepthPyramidMipCountId = Shader.PropertyToID("_HectonDepthPyramidMipCount");
        private static readonly int _DepthPyramidTexelSizeId = Shader.PropertyToID("_HectonDepthPyramidTexelSize");
        private static readonly int _FrustumPlanesId = Shader.PropertyToID("_HectonFrustumPlanes");
        private static readonly int _OcclusionEnabledId = Shader.PropertyToID("_HectonOcclusionEnabled");
        private static readonly int _OcclusionDepthBiasId = Shader.PropertyToID("_HectonOcclusionDepthBias");
        private static readonly int _OcclusionZBufferParamsId = Shader.PropertyToID("_HectonZBufferParams");
        private static readonly int _GlobalCameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture");
        private static readonly int _GlobalZBufferParamsId = Shader.PropertyToID("_ZBufferParams");
        private static readonly int _DarknessCullEnabledId = Shader.PropertyToID("_HectonDarknessCullEnabled");
        private static readonly int _DarknessBiolumThresholdId = Shader.PropertyToID("_HectonDarknessBiolumThreshold");
        private static readonly int _ScooterHeadlightCountId = Shader.PropertyToID("_HectonScooterHeadlightCount");
        private static readonly int _ScooterHeadlightPositionsWsId = Shader.PropertyToID("_HectonScooterHeadlightPositionsWS");
        private static readonly int _ScooterHeadlightDirectionsWsId = Shader.PropertyToID("_HectonScooterHeadlightDirectionsWS");
        private static readonly int _ScooterHeadlightColorsId = Shader.PropertyToID("_HectonScooterHeadlightColors");
        private static readonly int _ScooterHeadlightConeDataId = Shader.PropertyToID("_HectonScooterHeadlightConeData");
        private static readonly int _FloorBiolumStrengthId = Shader.PropertyToID("_HectonFloorBiolumStrength");
        private static readonly int _OceanBiolumStrengthId = Shader.PropertyToID("_HectonOceanBiolumStrength");
        private static readonly int _GlobalBiolumIntensityId = Shader.PropertyToID("_BiolumIntensity");
        private static readonly int _PeripheralCullDotId = Shader.PropertyToID("_HectonPeripheralCullDot");
        private static readonly int _PeripheralCullDistanceSqId = Shader.PropertyToID("_HectonPeripheralCullDistanceSq");
        private static readonly int _SourceMatricesId = Shader.PropertyToID("_HectonSourceInstanceMatrices");
        private static readonly int _SourceDataId = Shader.PropertyToID("_HectonSourceVegetationInstanceData");
        private static readonly int _VisibleIndicesLod0Id = Shader.PropertyToID("_HectonVisibleInstanceIndicesLOD0");
        private static readonly int _VisibleIndicesLod1Id = Shader.PropertyToID("_HectonVisibleInstanceIndicesLOD1");
        private static readonly int _VisibleIndicesShadowId = Shader.PropertyToID("_HectonVisibleInstanceIndicesShadow");
        private static readonly int _FarLodAppendEnabledId = Shader.PropertyToID("_HectonFarLodAppendEnabled");
        private static readonly int _IndirectArgsBufferId = Shader.PropertyToID("_HectonIndirectArgsBuffer");
        private static readonly int _IndirectIndexCountPerInstanceId = Shader.PropertyToID("_HectonIndirectIndexCountPerInstance");
        private static readonly int _IndirectStartIndexId = Shader.PropertyToID("_HectonIndirectStartIndex");
        private static readonly int _IndirectBaseVertexIndexId = Shader.PropertyToID("_HectonIndirectBaseVertexIndex");
        private static readonly int _PreviousCameraPositionId = Shader.PropertyToID("_HectonPreviousCameraPosition");
        private static readonly int _DepthPyramidSourceDepthId = Shader.PropertyToID("_HectonDepthPyramidSourceDepth");
        private static readonly int _DepthPyramidSourceId = Shader.PropertyToID("_HectonDepthPyramidSource");
        private static readonly int _DepthPyramidTargetId = Shader.PropertyToID("_HectonDepthPyramidTarget");
        private static readonly int _SubmarineWashSphereId = Shader.PropertyToID("_HectonSubmarineWashSphere");
        private static readonly int _SubmarineWashVelocityId = Shader.PropertyToID("_HectonSubmarineWashVelocity");
        private const int MaxScooterHeadlights = 2;

        [Header("Rendering")]
        [SerializeField]
        [Tooltip("Material that consumes the indirect vegetation matrix and metadata buffers in the shader.")]
        private Material _material;

        [SerializeField]
        [Tooltip("Optional first-party vegetation shader fallback used to build a hidden runtime material when the shared material is missing.")]
        private Shader _vegetationShader;

        [SerializeField]
        [Tooltip("Compute shader that performs GPU frustum culling and per-instance LOD classification.")]
        private ComputeShader _cullingCompute;

        [SerializeField]
        [Tooltip("Abyssal flow compute shader kernel used to persist GPU-only snapped flora flags.")]
        private ComputeShader _abyssalFlowFieldCompute;

        [SerializeField]
        [Tooltip("Compute shader that builds the vegetation Hi-Z depth pyramid consumed by the culling kernel.")]
        private ComputeShader _depthPyramidCompute;

        [SerializeField]
        [Tooltip("Hidden depth-only shader used to prime the Z buffer before the expensive forward vegetation pass.")]
        private Shader _depthOnlyShader;

        [SerializeField]
        [Tooltip("Hidden shadow-only shader used for shadow-caster draws with a dedicated GPU shadow culling buffer.")]
        private Shader _shadowCasterShader;

        [SerializeField]
        [Tooltip("Hidden motion-vector shader used to write stable motion vectors for indirect vegetation instances.")]
        private Shader _motionVectorShader;

        [SerializeField]
        [Tooltip("Optional authored near mesh. If empty, a strip mesh is generated once at runtime.")]
        private Mesh _mesh;

        [SerializeField]
        [Tooltip("Submesh index rendered through the indirect draw calls.")]
        private int _subMeshIndex;

        [SerializeField]
        [Tooltip("Optional camera override. Leave null to render in all cameras.")]
        private Camera _cameraOverride;

        #pragma warning disable 0414
        [SerializeField]
        [Tooltip("Legacy inspector field retained for serialized data compatibility after BRG migration.")]
        private ShadowCastingMode _shadowCastingMode = ShadowCastingMode.Off;

        [SerializeField]
        [Tooltip("Whether the near indirect vegetation draw call should receive shadows.")]
        private bool _receiveShadows;

        [SerializeField]
        [Tooltip("Legacy inspector field retained for serialized data compatibility after BRG migration.")]
        private ShadowCastingMode _impostorShadowCastingMode = ShadowCastingMode.Off;
        #pragma warning restore 0414

        [SerializeField]
        [Tooltip("Whether the far impostor draw call should receive shadows.")]
        private bool _impostorReceiveShadows;

        [SerializeField]
        [Tooltip("When enabled, a dedicated depth-only indirect draw primes the Z buffer before forward lighting to reduce alpha-tested overdraw.")]
        private bool _enableDepthPrepass = true;

        [SerializeField]
        [Tooltip("When enabled, a dedicated shadow-only indirect draw uses its own GPU culling buffer instead of letting the forward draw populate shadow maps.")]
        private bool _enableShadowCasterDraw = true;

        [SerializeField]
        [Tooltip("Enables a dedicated motion-vector draw for indirect vegetation to reduce TAA and motion-blur artifacts.")]
        private bool _enableMotionVectorDraw = true;

        [Header("Runtime Mesh")]
        [SerializeField]
        [Tooltip("Generates a single strip mesh once at runtime when no authored near mesh is assigned.")]
        private bool _generateMeshAtRuntime = true;

        [SerializeField, Range(4, 6)]
        [Tooltip("Strip segment count. User task requires 4-6 segments.")]
        private int _segmentCount = 5;

        [SerializeField, Min(0.05f)]
        [Tooltip("Generated strip height.")]
        private float _stripHeight = 1.8f;

        [SerializeField, Min(0.005f)]
        [Tooltip("Generated strip width at the base.")]
        private float _stripBaseWidth = 0.12f;

        [SerializeField, Min(0.001f)]
        [Tooltip("Generated strip width at the tip.")]
        private float _stripTipWidth = 0.015f;

        [Header("Impostor Cards")]
        [SerializeField]
        [Tooltip("Optional authored far impostor card mesh. If empty, a quad is generated once at runtime.")]
        private Mesh _impostorMesh;

        [SerializeField]
        [Tooltip("Generates a unit vertical card once at runtime when no authored impostor mesh is assigned.")]
        private bool _generateImpostorMeshAtRuntime = true;

        [SerializeField, Min(0.25f)]
        [Tooltip("Billboard card width multiplier passed into the shader.")]
        private float _impostorWidth = 1.1f;

        [SerializeField, Min(0.25f)]
        [Tooltip("Billboard card height multiplier passed into the shader.")]
        private float _impostorHeight = 1f;

        [Header("LOD")]
        [SerializeField, Range(10f, 80f)]
        [Tooltip("Near band end distance in meters. Real strip geometry renders only inside this radius.")]
        private float _nearLodDistance = 50f;

        [SerializeField, Range(60f, 180f)]
        [Tooltip("Far band end distance in meters. Billboard cards render only up to this radius.")]
        private float _farLodDistance = 150f;

        [SerializeField, Range(0.5f, 20f)]
        [Tooltip("Cross-fade range around the near/far band thresholds. Runtime is locked to the 2m flora dither mandate.")]
        private float _lodTransitionRange = LodTransitionRangeMeters;

        [SerializeField, Range(1, 8)]
        [Tooltip("Far LOD GPU culling cadence. 4 means distant vegetation visibility refreshes at 15Hz on a 60Hz frame budget.")]
        private int _farCullingFrameStride = 4;

        [SerializeField, Min(0f)]
        [Tooltip("Far LOD cadence only engages when the far vegetation band extends beyond this distance in meters.")]
        private float _farCullingCadenceDistance = 50f;

        [Header("GPU Occlusion")]
        [SerializeField]
        [Tooltip("Uses a GPU indirect render path with append-buffer visibility lists and indirect argument buffers when the compute kernels are available.")]
        private bool _preferGpuIndirectRendering = true;

        #pragma warning disable 0414
        [SerializeField]
        [Tooltip("Legacy inspector field retained for serialized data compatibility after BRG migration.")]
        private bool _enableDepthOcclusion = true;

        [SerializeField, Range(0.05f, 2f)]
        [Tooltip("Legacy inspector field retained for serialized data compatibility after BRG migration.")]
        private float _occlusionDepthBias = 0.35f;
        #pragma warning restore 0414

        [Header("Darkness Culling")]
        [SerializeField]
        [Tooltip("Rejects flora instances that are outside the published scooter headlights and below the global biolum threshold.")]
        private bool _enableDarknessCulling = true;

        [SerializeField, Range(0.001f, 0.25f)]
        [Tooltip("Minimum combined global biolum scalar required to keep completely unlit instances alive.")]
        private float _darknessBiolumThreshold = 0.05f;

        [Header("Peripheral Cull")]
        [SerializeField, Range(-1f, 1f)]
        [Tooltip("When an instance falls below this camera-forward dot product and is beyond the peripheral distance, the GPU culling kernel rejects it.")]
        private float _peripheralCullDot = 0.5f;

        [SerializeField, Min(0f)]
        [Tooltip("Distance in meters after which peripheral instances become eligible for the dot-product cone cull.")]
        private float _peripheralCullDistance = 30f;

        [Header("Legacy Fallback")]
        [SerializeField]
        [Tooltip("Fallback vegetation type used when no external instance metadata buffer is bound.")]
        private HectonVegetationInstanceType _legacyFallbackType = HectonVegetationInstanceType.Grass;

        [Header("Draw Bounds")]
        [SerializeField]
        [Tooltip("Local center offset used when no explicit bounds override is supplied.")]
        private Vector3 _boundsCenterOffset = Vector3.zero;

        [SerializeField]
        [Tooltip("Fallback draw bounds size used when no explicit bounds override is supplied.")]
        private Vector3 _boundsSize = new Vector3(128f, 32f, 128f);

        private Mesh _generatedMesh;
        private Mesh _generatedImpostorMesh;
        private GraphicsBuffer _instanceMatrixBuffer;
        private GraphicsBuffer _instanceDataBuffer;
        private GraphicsBuffer _floraPhaseSeedBuffer;
        private GraphicsBuffer _legacyInstanceDataBuffer;
        private GraphicsBuffer _uploadedInstanceMatrixBuffer;
        private GraphicsBuffer _uploadedInstanceDataBuffer;
        private IHectonIndirectVegetationBufferSource _bufferSource;
        private Bounds _explicitBounds;
        private bool _hasBoundsOverride;
        private bool _isRegistered;
        private bool _legacyDataDirty = true;
        private int _instanceCount;
        private Camera _cachedCullCamera;
        private Material _depthOnlyMaterial;
        private Material _shadowCasterMaterial;
        private Material _motionVectorMaterial;
        private Material _runtimeMaterial;
        private bool _ownsRuntimeMaterial;
        private Vector3 _previousMotionCameraPosition;
        private Camera _previousMotionCamera;
        private bool _hasPreviousMotionCameraPosition;
        private Vector3 _cachedCullCameraPosition;
        private Vector3 _cachedCullCameraForward = Vector3.forward;
        private PlayerToolManager _playerToolManager;
        private float _nextToolManagerResolveTime;
        private BatchRendererGroup _batchRendererGroup;
        private NativeArray<MetadataValue> _batchMetadata;
        private GraphicsBuffer _batchHandleBuffer;
        private BatchID _batchId;
        private GraphicsBuffer _registeredBatchBuffer;
        private BatchMeshID _nearBatchMeshId;
        private BatchMeshID _farBatchMeshId;
        private Mesh _registeredNearMesh;
        private Mesh _registeredFarMesh;
        private BatchMaterialID _nearBatchMaterialId;
        private BatchMaterialID _farBatchMaterialId;
        private BatchMaterialID _depthNearBatchMaterialId;
        private BatchMaterialID _depthFarBatchMaterialId;
        private BatchMaterialID _shadowBatchMaterialId;
        private BatchMaterialID _motionNearBatchMaterialId;
        private BatchMaterialID _motionFarBatchMaterialId;
        private Material _registeredNearBrgMaterial;
        private Material _registeredFarBrgMaterial;
        private Material _registeredDepthNearBrgMaterial;
        private Material _registeredDepthFarBrgMaterial;
        private Material _registeredShadowBrgMaterial;
        private Material _registeredMotionNearBrgMaterial;
        private Material _registeredMotionFarBrgMaterial;
        private Material _nearBrgMaterial;
        private Material _farBrgMaterial;
        private Material _depthNearBrgMaterial;
        private Material _depthFarBrgMaterial;
        private Material _shadowBrgMaterial;
        private Material _motionNearBrgMaterial;
        private Material _motionFarBrgMaterial;
        private NativeArray<Matrix4x4> _cpuCullingMatrices;
        private NativeArray<HectonVegetationInstanceData> _cpuCullingData;
        private bool _hasCpuCullingData;

        private Vector4[] _scooterHeadlightPositionsWs;
        private Vector4[] _scooterHeadlightDirectionsWs;
        private Vector4[] _scooterHeadlightColors;
        private Vector4[] _scooterHeadlightConeData;

        // COLD ALLOC: Camera[8] - camera discovery cache for GPU culling dispatch - owner: HectonIndirectVegetationRenderer
        private readonly Camera[] _cameraSearchCache = new Camera[8];
        private Plane[] _frustumPlaneCache;
        private Vector4[] _frustumPlaneVectors;
        private GraphicsBuffer _visibleIndicesLod0Buffer;
        private GraphicsBuffer _visibleIndicesLod1Buffer;
        private GraphicsBuffer _visibleIndicesShadowBuffer;
        private GraphicsBuffer _floraSnapFlagBuffer;
        private GraphicsBuffer _indirectArgsLod0Buffer;
        private GraphicsBuffer _indirectArgsLod1Buffer;
        private GraphicsBuffer _indirectArgsShadowBuffer;
        private int _gpuVisibleIndexCapacity;
        private int _floraSnapFlagCapacity;
        private bool _floraSnapFlagBufferRequiresClear;
        private int _gpuCullingFrameIndex;
        private bool _hasFarCullingSnapshot;
        private RenderTexture _depthPyramidTexture;
        private int _depthPyramidWidth;
        private int _depthPyramidHeight;
        private int _depthPyramidMipCount;
        private int _cullFloraKernel = -1;
        private int _cullFloraShadowKernel = -1;
        private int _clearIndirectArgsKernel = -1;
        private int _clearFloraSnapFlagsKernel = -1;
        private int _flagSnappedFloraKernel = -1;
        private int _depthPyramidCopyKernel = -1;
        private int _depthPyramidDownsampleKernel = -1;

        private HectonVegetationInstanceData[] _legacyInstanceData;

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct BuildVegetationVisibilityMaskJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Matrix4x4> Matrices;
            [ReadOnly] public NativeArray<HectonVegetationInstanceData> InstanceData;
            [ReadOnly] public NativeArray<float4> CullingPlanes;
            [ReadOnly] public NativeArray<float4> HeadlightPositionsWs;
            [ReadOnly] public NativeArray<float4> HeadlightDirectionsWs;
            [ReadOnly] public NativeArray<float4> HeadlightColors;
            [ReadOnly] public NativeArray<float4> HeadlightConeData;
            public NativeArray<byte> VisibilityMask;
            public int InstanceCount;
            public int CullingPlaneCount;
            public int HeadlightCount;
            public bool EnableCpuCulling;
            public bool UseFarPass;
            public bool UseShadowPass;
            public bool BypassDarknessCulling;
            public float3 ViewPosition;
            public float3 GlobalOffset;
            public float Lod0MaxDistanceSq;
            public float Lod1MinDistanceSq;
            public float Lod1MaxDistanceSq;

            public void Execute(int index)
            {
                if (index >= InstanceCount)
                    return;

                byte instanceVisibility = 0;
                if (EnableCpuCulling)
                {
                    Matrix4x4 instanceMatrix = Matrices[index];
                    HectonVegetationInstanceData instanceData = InstanceData[index];
                    ResolveInstanceShape(instanceData, out float instanceHeight, out float instanceWidth);

                    float3 rootWs = TransformPoint(instanceMatrix, 0f, 0f, 0f) + GlobalOffset;
                    float3 centerWs = TransformPoint(instanceMatrix, 0f, instanceHeight * 0.5f, 0f) + GlobalOffset;
                    float3 topWs = TransformPoint(instanceMatrix, 0f, instanceHeight, 0f) + GlobalOffset;
                    float3 sideAWs = TransformPoint(instanceMatrix, instanceWidth, instanceHeight * 0.5f, 0f) + GlobalOffset;
                    float3 sideBWs = TransformPoint(instanceMatrix, -instanceWidth, instanceHeight * 0.5f, 0f) + GlobalOffset;

                    float radiusSq = math.max(
                        math.lengthsq(centerWs - rootWs),
                        math.max(
                            math.lengthsq(centerWs - topWs),
                            math.max(math.lengthsq(centerWs - sideAWs), math.lengthsq(centerWs - sideBWs))));
                    if (!IsSphereVisibleSq(centerWs, math.max(0.0625f, radiusSq)))
                    {
                        VisibilityMask[index] = 0;
                        return;
                    }

                    if (!IsVisibleInDarkness(centerWs))
                    {
                        VisibilityMask[index] = 0;
                        return;
                    }

                    float distanceSq = math.lengthsq(rootWs - ViewPosition);
                    if (distanceSq <= Lod0MaxDistanceSq)
                        instanceVisibility |= VisibilityMaskNear;

                    if (UseFarPass && distanceSq >= Lod1MinDistanceSq && distanceSq <= Lod1MaxDistanceSq)
                        instanceVisibility |= VisibilityMaskFar;

                    if (UseShadowPass)
                        instanceVisibility |= VisibilityMaskShadow;
                }
                else
                {
                    instanceVisibility |= VisibilityMaskNear;
                    if (UseFarPass)
                        instanceVisibility |= VisibilityMaskFar;
                    if (UseShadowPass)
                        instanceVisibility |= VisibilityMaskShadow;
                }

                VisibilityMask[index] = instanceVisibility;
            }

            private bool IsVisibleInDarkness(float3 samplePositionWs)
            {
                if (BypassDarknessCulling)
                    return true;

                for (int headlightIndex = 0; headlightIndex < HeadlightCount; headlightIndex++)
                {
                    float4 lightPosition = HeadlightPositionsWs[headlightIndex];
                    float lightRange = math.max(0.1f, lightPosition.w);
                    float3 toSample = samplePositionWs - lightPosition.xyz;
                    float sampleDistanceSq = math.lengthsq(toSample);
                    float lightRangeSq = lightRange * lightRange;
                    if (sampleDistanceSq >= lightRangeSq || sampleDistanceSq <= 0.00000001f)
                        continue;

                    float4 directionData = HeadlightDirectionsWs[headlightIndex];
                    float3 lightDirection = directionData.xyz;
                    float lightDirectionLenSq = math.lengthsq(lightDirection);
                    if (!math.isfinite(lightDirectionLenSq) || lightDirectionLenSq <= 0.00000001f)
                        continue;

                    float outerCos = HeadlightConeData[headlightIndex].x;
                    float dotLight = math.dot(lightDirection, toSample);
                    if (!PassesDotThresholdSq(dotLight, outerCos, sampleDistanceSq * lightDirectionLenSq))
                        continue;

                    float invRange = HeadlightConeData[headlightIndex].z;
                    float rangeAttenuation = math.saturate(1f - sampleDistanceSq * invRange * invRange);
                    rangeAttenuation *= rangeAttenuation;
                    float intensity = HeadlightColors[headlightIndex].w * HeadlightConeData[headlightIndex].y;
                    if (rangeAttenuation * intensity >= 0.02f)
                        return true;
                }

                return false;
            }

            private bool IsSphereVisibleSq(float3 center, float radiusSq)
            {
                for (int planeIndex = 0; planeIndex < CullingPlaneCount; planeIndex++)
                {
                    float4 plane = CullingPlanes[planeIndex];
                    float signedDistance = math.dot(plane.xyz, center) + plane.w;
                    if (signedDistance < 0f && signedDistance * signedDistance > radiusSq)
                        return false;
                }

                return true;
            }

            private static bool PassesDotThresholdSq(float dotValue, float threshold, float lengthProductSq)
            {
                if (!math.isfinite(dotValue) || !math.isfinite(threshold) || !math.isfinite(lengthProductSq) || lengthProductSq <= 0.00000001f)
                    return true;

                float thresholdSq = threshold * threshold;
                float dotSq = dotValue * dotValue;
                return threshold >= 0f
                    ? dotValue >= 0f && dotSq >= thresholdSq * lengthProductSq
                    : dotValue >= 0f || dotSq <= thresholdSq * lengthProductSq;
            }

            private static void ResolveInstanceShape(HectonVegetationInstanceData instanceData, out float instanceHeight, out float instanceWidth)
            {
                float instanceType = math.clamp(math.round(instanceData.Type), 0f, 2f);
                float encodedHeightScale = math.saturate(math.abs(instanceData.HeightScale));
                float encodedWidthScale = instanceData.WidthScale < 0f ? 1f : math.saturate(instanceData.WidthScale);
                if (instanceType < 0.5f)
                {
                    instanceHeight = math.lerp(0.35f, 1.4f, encodedHeightScale);
                    instanceWidth = math.lerp(0.65f, 1.25f, encodedWidthScale);
                    return;
                }

                if (instanceType < 1.5f)
                {
                    instanceHeight = math.lerp(10f, 20f, encodedHeightScale);
                    instanceWidth = math.lerp(0.55f, 1.6f, encodedWidthScale);
                    return;
                }

                instanceHeight = math.lerp(0.75f, 2.4f, encodedHeightScale);
                instanceWidth = math.lerp(0.75f, 1.35f, encodedWidthScale);
            }

            private static float3 TransformPoint(Matrix4x4 matrixValue, float x, float y, float z)
            {
                return new float3(
                    matrixValue.m00 * x + matrixValue.m01 * y + matrixValue.m02 * z + matrixValue.m03,
                    matrixValue.m10 * x + matrixValue.m11 * y + matrixValue.m12 * z + matrixValue.m13,
                    matrixValue.m20 * x + matrixValue.m21 * y + matrixValue.m22 * z + matrixValue.m23);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct FinalizeVegetationDrawOutputJob : IJob
        {
            [ReadOnly] public NativeArray<byte> VisibilityMask;
            public int InstanceCount;
            public int Layer;
            public int SubMeshIndex;
            public bool UseFarPass;
            public bool UseDepthPass;
            public bool UseDepthFarPass;
            public bool UseShadowPass;
            public bool UseMotionPass;
            public bool UseMotionFarPass;
            public BatchID BatchId;
            public BatchMeshID NearMeshId;
            public BatchMeshID FarMeshId;
            public BatchMaterialID NearMaterialId;
            public BatchMaterialID FarMaterialId;
            public BatchMaterialID DepthNearMaterialId;
            public BatchMaterialID DepthFarMaterialId;
            public BatchMaterialID ShadowMaterialId;
            public BatchMaterialID MotionNearMaterialId;
            public BatchMaterialID MotionFarMaterialId;
            [NativeDisableUnsafePtrRestriction] public int* VisibleInstances;
            [NativeDisableUnsafePtrRestriction] public BatchDrawCommand* DrawCommands;
            [NativeDisableUnsafePtrRestriction] public BatchDrawRange* DrawRanges;
            [NativeDisableUnsafePtrRestriction] public BatchCullingOutputDrawCommands* OutputCommands;

            public void Execute()
            {
                int nearCount = 0;
                int farCount = 0;
                int shadowCount = 0;
                for (int instanceIndex = 0; instanceIndex < InstanceCount; instanceIndex++)
                {
                    byte instanceVisibility = VisibilityMask[instanceIndex];
                    if ((instanceVisibility & VisibilityMaskNear) != 0)
                        nearCount++;
                    if ((instanceVisibility & VisibilityMaskFar) != 0)
                        farCount++;
                    if ((instanceVisibility & VisibilityMaskShadow) != 0)
                        shadowCount++;
                }

                int nearOffset = 0;
                int farOffset = nearCount;
                int shadowOffset = nearCount + farCount;
                int nearWrite = 0;
                int farWrite = 0;
                int shadowWrite = 0;

                for (int instanceIndex = 0; instanceIndex < InstanceCount; instanceIndex++)
                {
                    byte instanceVisibility = VisibilityMask[instanceIndex];
                    if ((instanceVisibility & VisibilityMaskNear) != 0)
                    {
                        VisibleInstances[nearOffset + nearWrite] = instanceIndex;
                        nearWrite++;
                    }

                    if ((instanceVisibility & VisibilityMaskFar) != 0)
                    {
                        VisibleInstances[farOffset + farWrite] = instanceIndex;
                        farWrite++;
                    }

                    if ((instanceVisibility & VisibilityMaskShadow) != 0)
                    {
                        VisibleInstances[shadowOffset + shadowWrite] = instanceIndex;
                        shadowWrite++;
                    }
                }

                int commandIndex = 0;
                commandIndex = WriteVegetationDrawCommand(
                    commandIndex,
                    nearOffset,
                    nearWrite,
                    NearMaterialId,
                    NearMeshId,
                    ShadowCastingMode.Off,
                    false,
                    MotionVectorGenerationMode.Camera);

                if (UseFarPass)
                {
                    commandIndex = WriteVegetationDrawCommand(
                        commandIndex,
                        farOffset,
                        farWrite,
                        FarMaterialId,
                        FarMeshId,
                        ShadowCastingMode.Off,
                        false,
                        MotionVectorGenerationMode.Camera);
                }

                if (UseDepthPass)
                {
                    commandIndex = WriteVegetationDrawCommand(
                        commandIndex,
                        nearOffset,
                        nearWrite,
                        DepthNearMaterialId,
                        NearMeshId,
                        ShadowCastingMode.Off,
                        false,
                        MotionVectorGenerationMode.Camera);

                    if (UseDepthFarPass)
                    {
                        commandIndex = WriteVegetationDrawCommand(
                            commandIndex,
                            farOffset,
                            farWrite,
                            DepthFarMaterialId,
                            FarMeshId,
                            ShadowCastingMode.Off,
                            false,
                            MotionVectorGenerationMode.Camera);
                    }
                }

                if (UseShadowPass)
                {
                    commandIndex = WriteVegetationDrawCommand(
                        commandIndex,
                        shadowOffset,
                        shadowWrite,
                        ShadowMaterialId,
                        NearMeshId,
                        ShadowCastingMode.On,
                        false,
                        MotionVectorGenerationMode.Camera);
                }

                if (UseMotionPass)
                {
                    commandIndex = WriteVegetationDrawCommand(
                        commandIndex,
                        nearOffset,
                        nearWrite,
                        MotionNearMaterialId,
                        NearMeshId,
                        ShadowCastingMode.Off,
                        false,
                        MotionVectorGenerationMode.Object);

                    if (UseMotionFarPass)
                    {
                        commandIndex = WriteVegetationDrawCommand(
                            commandIndex,
                            farOffset,
                            farWrite,
                            MotionFarMaterialId,
                            FarMeshId,
                            ShadowCastingMode.Off,
                            false,
                            MotionVectorGenerationMode.Object);
                    }
                }

                *OutputCommands = new BatchCullingOutputDrawCommands
                {
                    visibleInstances = VisibleInstances,
                    visibleInstanceCount = nearWrite + farWrite + shadowWrite,
                    drawCommands = DrawCommands,
                    drawCommandCount = commandIndex,
                    drawRanges = DrawRanges,
                    drawRangeCount = commandIndex
                };
            }

            private int WriteVegetationDrawCommand(
                int commandIndex,
                int visibleOffset,
                int visibleCount,
                BatchMaterialID materialId,
                BatchMeshID meshId,
                ShadowCastingMode shadowCastingMode,
                bool receiveShadows,
                MotionVectorGenerationMode motionMode)
            {
                if (visibleCount <= 0 || materialId.value == 0u || meshId.value == 0u)
                    return commandIndex;

                DrawCommands[commandIndex] = new BatchDrawCommand
                {
                    flags = BatchDrawCommandFlags.None,
                    visibleOffset = (uint)visibleOffset,
                    visibleCount = (uint)visibleCount,
                    batchID = BatchId,
                    materialID = materialId,
                    splitVisibilityMask = ushort.MaxValue,
                    lightmapIndex = ushort.MaxValue,
                    sortingPosition = 0,
                    meshID = meshId,
                    submeshIndex = (ushort)math.max(0, SubMeshIndex)
                };
                DrawRanges[commandIndex] = new BatchDrawRange
                {
                    drawCommandsBegin = (uint)commandIndex,
                    drawCommandsCount = 1u,
                    drawCommandsType = BatchDrawCommandType.Direct,
                    filterSettings = new BatchFilterSettings
                    {
                        renderingLayerMask = HectonLayerMasks.AllDefinedProjectRenderingLayerMaskValue,
                        rendererPriority = 0,
                        layer = (byte)math.clamp(Layer, byte.MinValue, byte.MaxValue),
                        shadowCastingMode = shadowCastingMode,
                        receiveShadows = receiveShadows,
                        motionMode = motionMode,
                        staticShadowCaster = false,
                        allDepthSorted = false
                    }
                };
                return commandIndex + 1;
            }
        }

        /// <summary>True when an external matrix buffer is currently bound.</summary>
        public bool HasMatrixBuffer => _instanceMatrixBuffer != null;

        /// <summary>True when either an external or fallback instance metadata buffer is currently bound.</summary>
        public bool HasInstanceDataBuffer => _instanceDataBuffer != null || _legacyInstanceDataBuffer != null;

        /// <summary>Current active instance count published into the indirect args payload.</summary>
        public int BoundInstanceCount => _instanceCount;

        /// <summary>Configured distance where full strip geometry stops rendering.</summary>
        public float NearLodDistance => _nearLodDistance;

        /// <summary>Configured distance where impostor rendering ends and the pass culls completely.</summary>
        public float FarLodDistance => _farLodDistance;

        /// <summary>True when the far impostor pass is currently enabled.</summary>
        public bool UsesImpostorPass => _farLodDistance > _nearLodDistance;

        /// <summary>True when this renderer is currently consuming caller-provided array uploads staged into owned GPU buffers.</summary>
        public bool UsesOwnedUploadBuffers => _instanceMatrixBuffer == _uploadedInstanceMatrixBuffer;

        /// <summary>Approximate VRAM footprint in bytes for the renderer-owned graphics buffers.</summary>
        public long GetVRAMEstimation()
        {
            long totalBytes = 0L;
            totalBytes += EstimateGraphicsBufferBytes(_legacyInstanceDataBuffer);
            totalBytes += EstimateGraphicsBufferBytes(_uploadedInstanceMatrixBuffer);
            totalBytes += EstimateGraphicsBufferBytes(_uploadedInstanceDataBuffer);
            totalBytes += EstimateGraphicsBufferBytes(_batchHandleBuffer);
            totalBytes += EstimateGraphicsBufferBytes(_floraSnapFlagBuffer);
            return totalBytes;
        }

        private void Awake()
        {
            _nearLodDistance = Mathf.Max(1f, _nearLodDistance);
            _farLodDistance = Mathf.Max(_nearLodDistance, _farLodDistance);
            _lodTransitionRange = LodTransitionRangeMeters;
            _farCullingFrameStride = Mathf.Clamp(_farCullingFrameStride, 1, 8);
            _farCullingCadenceDistance = Mathf.Max(0f, _farCullingCadenceDistance);
            TryAutoAssignAssets();
            if (_cullingCompute != null)
            {
                _cullFloraKernel = _cullingCompute.FindKernel("CullFloraInstances");
                _cullFloraShadowKernel = _cullingCompute.FindKernel("CullFloraShadowInstances");
                _clearIndirectArgsKernel = _cullingCompute.FindKernel("ClearIndirectArgs");
            }
            if (_abyssalFlowFieldCompute != null)
            {
                _clearFloraSnapFlagsKernel = _abyssalFlowFieldCompute.FindKernel("ClearFloraSnapFlags");
                _flagSnappedFloraKernel = _abyssalFlowFieldCompute.FindKernel("FlagSnappedFlora");
            }
            if (_depthPyramidCompute != null)
            {
                _depthPyramidCopyKernel = _depthPyramidCompute.FindKernel("CopyDepthPyramidMip0");
                _depthPyramidDownsampleKernel = _depthPyramidCompute.FindKernel("DownsampleDepthPyramidMip");
            }

            if (!EnsureRenderMaterialResolved())
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[HectonIndirectVegetationRenderer] Material is required and fallback shader resolution failed.", this);
#endif
                enabled = false;
                return;
            }

            if (_generateMeshAtRuntime || _mesh == null)
            {
                _generatedMesh = HectonProceduralVegetationStripBuilder.Build(
                    $"{nameof(HectonIndirectVegetationRenderer)}_Strip",
                    _segmentCount,
                    _stripHeight,
                    _stripBaseWidth,
                    _stripTipWidth);
            }

            if ((_generateImpostorMeshAtRuntime || _impostorMesh == null) && _farLodDistance > _nearLodDistance)
                _generatedImpostorMesh = BuildImpostorCardMesh();

            if (ResolveNearRenderMesh() == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[HectonIndirectVegetationRenderer] No near render mesh resolved.", this);
#endif
                enabled = false;
                return;
            }

            // COLD ALLOC: Vector4[2] - scooter headlight world-position payload cache for BRG darkness culling - owner: HectonIndirectVegetationRenderer
            _scooterHeadlightPositionsWs = new Vector4[MaxScooterHeadlights];
            // COLD ALLOC: Vector4[2] - scooter headlight direction payload cache for BRG darkness culling - owner: HectonIndirectVegetationRenderer
            _scooterHeadlightDirectionsWs = new Vector4[MaxScooterHeadlights];
            // COLD ALLOC: Vector4[2] - scooter headlight color/intensity payload cache for BRG darkness culling - owner: HectonIndirectVegetationRenderer
            _scooterHeadlightColors = new Vector4[MaxScooterHeadlights];
            // COLD ALLOC: Vector4[2] - scooter headlight cone payload cache for BRG darkness culling - owner: HectonIndirectVegetationRenderer
            _scooterHeadlightConeData = new Vector4[MaxScooterHeadlights];
            _frustumPlaneCache = new Plane[FrustumPlaneCount]; // COLD ALLOC: Plane[6] - cached frustum planes for GPU vegetation culling upload - owner: HectonIndirectVegetationRenderer
            _frustumPlaneVectors = new Vector4[FrustumPlaneCount]; // COLD ALLOC: Vector4[6] - packed frustum planes for compute upload - owner: HectonIndirectVegetationRenderer
            CreateAuxiliaryMaterials();
        }

        private void OnEnable()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
            _hasPreviousMotionCameraPosition = false;
            _previousMotionCamera = null;
            ReleaseBatchRendererGroupResources();
            ReleaseGpuIndirectResources();
        }

        private void OnDestroy()
        {
            TryUnregister();
            ReleaseBatchRendererGroupResources();
            ReleaseGpuIndirectResources();
            ReleaseLegacyInstanceDataBuffer();
            ReleaseUploadedInstanceBuffers();
            ReleaseAuxiliaryMaterials();
            ReleaseRuntimeMaterial();
            ReleaseCpuCullingData();

            if (_generatedMesh != null)
            {
                Destroy(_generatedMesh);
                _generatedMesh = null;
            }

            if (_generatedImpostorMesh != null)
            {
                Destroy(_generatedImpostorMesh);
                _generatedImpostorMesh = null;
            }
        }

        /// <summary>
        /// Binds an external source that owns both instance buffers and optional explicit bounds.
        /// </summary>
        /// <param name="bufferSource">External source that owns the GPU buffers.</param>
        public void BindSource(IHectonIndirectVegetationBufferSource bufferSource)
        {
            _bufferSource = bufferSource;
            SyncSourceBinding();
        }

        /// <summary>
        /// Clears the current external source binding.
        /// </summary>
        public void ClearSource()
        {
            _bufferSource = null;
            ClearInstanceBuffer();
            ClearDrawBoundsOverride();
        }

        /// <summary>
        /// Binds the external per-instance matrix buffer populated by another system.
        /// </summary>
        /// <param name="instanceMatrixBuffer">Structured buffer of Matrix4x4 transforms.</param>
        /// <param name="instanceCount">Active instance count contained in the buffer.</param>
        public void BindInstanceBuffer(GraphicsBuffer instanceMatrixBuffer, int instanceCount)
        {
            _bufferSource = null;

            if (instanceMatrixBuffer == null || instanceCount <= 0 || instanceMatrixBuffer.count <= 0)
            {
                ClearInstanceBuffer();
                return;
            }

            InvalidateRenderStateForBufferIdentityChange(instanceMatrixBuffer, _instanceDataBuffer, _floraPhaseSeedBuffer);
            _instanceMatrixBuffer = instanceMatrixBuffer;
            _legacyDataDirty = true;
            _hasCpuCullingData = false;
            SetInstanceCount(instanceCount);
        }

        /// <summary>
        /// Binds the external per-instance metadata buffer populated by another system.
        /// </summary>
        /// <param name="instanceDataBuffer">Structured buffer of <see cref="HectonVegetationInstanceData"/> payloads.</param>
        public void BindInstanceDataBuffer(GraphicsBuffer instanceDataBuffer)
        {
            _bufferSource = null;

            if (instanceDataBuffer == null || instanceDataBuffer.count <= 0)
            {
                ClearInstanceDataBuffer();
                return;
            }

            InvalidateRenderStateForBufferIdentityChange(_instanceMatrixBuffer, instanceDataBuffer, _floraPhaseSeedBuffer);
            _instanceDataBuffer = instanceDataBuffer;
        }

        /// <summary>
        /// Binds the parallel per-instance cascade phase-seed buffer consumed by reactive flora shaders.
        /// </summary>
        /// <param name="floraPhaseSeedBuffer">Structured buffer containing one phase seed per active vegetation instance.</param>
        public void BindFloraPhaseSeedBuffer(GraphicsBuffer floraPhaseSeedBuffer)
        {
            GraphicsBuffer resolvedPhaseSeedBuffer = floraPhaseSeedBuffer != null && floraPhaseSeedBuffer.count > 0
                ? floraPhaseSeedBuffer
                : null;
            InvalidateRenderStateForBufferIdentityChange(_instanceMatrixBuffer, _instanceDataBuffer, resolvedPhaseSeedBuffer);
            _floraPhaseSeedBuffer = resolvedPhaseSeedBuffer;
        }

        /// <summary>
        /// Uploads caller-owned arrays into renderer-owned GPU staging buffers and binds them for indirect rendering.
        /// </summary>
        /// <param name="instanceMatrices">Caller-owned instance matrix array.</param>
        /// <param name="instanceData">Caller-owned vegetation metadata array. Pass null to use the fallback metadata path.</param>
        /// <param name="instanceCount">Number of valid entries contained in the caller arrays.</param>
        public void BindInstanceArrays(
            Matrix4x4[] instanceMatrices,
            HectonVegetationInstanceData[] instanceData,
            int instanceCount)
        {
            _bufferSource = null;

            if (instanceMatrices == null || instanceCount <= 0 || instanceMatrices.Length < instanceCount)
            {
                ClearInstanceBuffer();
                return;
            }

            EnsureUploadedInstanceBufferCapacity(instanceCount, instanceData != null);
            if (_uploadedInstanceMatrixBuffer == null)
            {
                ClearInstanceBuffer();
                return;
            }

            GraphicsBufferUploadUtility.UploadArray(_uploadedInstanceMatrixBuffer, instanceMatrices, instanceCount);
            _instanceMatrixBuffer = _uploadedInstanceMatrixBuffer;
            CopyCpuCullingPayload(instanceMatrices, instanceData, instanceCount);

            if (instanceData != null)
            {
                if (instanceData.Length < instanceCount || _uploadedInstanceDataBuffer == null)
                {
                    ClearInstanceBuffer();
                    return;
                }

                GraphicsBufferUploadUtility.UploadArray(_uploadedInstanceDataBuffer, instanceData, instanceCount);
                _instanceDataBuffer = _uploadedInstanceDataBuffer;
                _legacyDataDirty = false;
            }
            else
            {
                _instanceDataBuffer = null;
                _legacyDataDirty = true;
            }

            SetInstanceCount(instanceCount);
        }

        /// <summary>
        /// Uploads caller-owned instance matrices into renderer-owned GPU staging buffers and uses legacy metadata fallback.
        /// </summary>
        /// <param name="instanceMatrices">Caller-owned instance matrix array.</param>
        /// <param name="instanceCount">Number of valid entries contained in the caller array.</param>
        public void BindInstanceArrays(Matrix4x4[] instanceMatrices, int instanceCount)
        {
            BindInstanceArrays(instanceMatrices, null, instanceCount);
        }

        private bool BindInstanceNativeArrays(
            NativeArray<Matrix4x4> instanceMatrices,
            NativeArray<HectonVegetationInstanceData> instanceData,
            int instanceCount)
        {
            if (!instanceMatrices.IsCreated || !instanceData.IsCreated || instanceCount <= 0)
                return false;

            if (instanceMatrices.Length < instanceCount || instanceData.Length < instanceCount)
                return false;

            EnsureUploadedInstanceBufferCapacity(instanceCount, true);
            if (_uploadedInstanceMatrixBuffer == null || _uploadedInstanceDataBuffer == null)
                return false;

            InvalidateRenderStateForBufferIdentityChange(_uploadedInstanceMatrixBuffer, _uploadedInstanceDataBuffer, _floraPhaseSeedBuffer);
            GraphicsBufferUploadUtility.UploadNativeArray(_uploadedInstanceMatrixBuffer, instanceMatrices, instanceCount);
            GraphicsBufferUploadUtility.UploadNativeArray(_uploadedInstanceDataBuffer, instanceData, instanceCount);
            _instanceMatrixBuffer = _uploadedInstanceMatrixBuffer;
            _instanceDataBuffer = _uploadedInstanceDataBuffer;
            _legacyDataDirty = false;
            CopyCpuCullingPayload(instanceMatrices, instanceData, instanceCount);
            SetInstanceCount(instanceCount);
            return true;
        }

        /// <summary>
        /// Clears the current external instance buffer binding.
        /// </summary>
        public void ClearInstanceBuffer()
        {
            _bufferSource = null;
            ClearBoundInstanceState();
        }

        /// <summary>
        /// Clears the current external instance metadata buffer binding.
        /// </summary>
        public void ClearInstanceDataBuffer()
        {
            _bufferSource = null;
            _instanceDataBuffer = null;
            _floraPhaseSeedBuffer = null;
            _legacyDataDirty = true;
            _floraSnapFlagBufferRequiresClear = true;
        }

        /// <summary>
        /// Updates the active instance count used by the indirect args buffers.
        /// </summary>
        /// <param name="instanceCount">Number of instances to draw.</param>
        public void SetInstanceCount(int instanceCount)
        {
            int clampedCount = Mathf.Max(0, instanceCount);
            if (_instanceCount == clampedCount)
                return;

            _instanceCount = clampedCount;
            _legacyDataDirty = true;
            _floraSnapFlagBufferRequiresClear = true;
            _hasFarCullingSnapshot = false;
        }

        /// <summary>
        /// Overrides the world-space draw bounds used by the indirect draw calls.
        /// </summary>
        /// <param name="drawBounds">Explicit world-space bounds.</param>
        public void SetDrawBounds(Bounds drawBounds)
        {
            _explicitBounds = drawBounds;
            _hasBoundsOverride = true;
        }

        /// <summary>
        /// Returns to transform-relative fallback draw bounds.
        /// </summary>
        public void ClearDrawBoundsOverride()
        {
            _hasBoundsOverride = false;
        }

        /// <summary>
        /// Executes the BRG-backed vegetation submission.
        /// </summary>
        /// <param name="deltaTime">Unused current frame delta required by ITickable.</param>
        public void Tick(float deltaTime)
        {
            SyncSourceBinding();

            Material renderMaterial = ResolveRenderMaterial();
            if (_instanceMatrixBuffer == null || _instanceCount <= 0 || renderMaterial == null)
                return;

            Mesh nearMesh = ResolveNearRenderMesh();
            if (nearMesh == null)
                return;

            Camera cullCamera = _cameraOverride != null ? _cameraOverride : ResolveCullCamera();
            Vector3 cullCameraPosition = _cachedCullCameraPosition;
            Vector3 cullCameraForward = _cachedCullCameraForward;
            if (cullCamera != null)
            {
                Transform cullTransform = cullCamera.transform;
                cullCameraPosition = cullTransform.position;
                cullCameraForward = cullTransform.forward;
                _cachedCullCameraPosition = cullCameraPosition;
                _cachedCullCameraForward = cullCameraForward;
            }

            CreateAuxiliaryMaterials();
            Mesh farMesh = FrameTimeWatchdog.IsDistantFloraRenderingEnabled && _farLodDistance > _nearLodDistance
                ? ResolveImpostorRenderMesh()
                : null;
            Vector3 rendererPosition = transform.position;
            Bounds drawBounds = ResolveDrawBounds(rendererPosition);
            if (TryRenderGpuIndirect(cullCamera, nearMesh, farMesh, cullCameraPosition, cullCameraForward, drawBounds))
                return;

            EnsureBatchRendererGroupResources();
            if (_batchRendererGroup == null || _batchId.Equals(default))
                return;

            if (!TryBindBrgMaterials())
                return;

            SyncBatchRegistration(nearMesh, farMesh);
            SyncBatchBuffer(_instanceMatrixBuffer);
            _batchRendererGroup.SetGlobalBounds(drawBounds);
            UpdateMotionVectorHistory(cullCamera, cullCameraPosition);
        }

        private Bounds ResolveDrawBounds(Vector3 rendererPosition)
        {
            return _hasBoundsOverride
                ? _explicitBounds
                : new Bounds(rendererPosition + _boundsCenterOffset, _boundsSize);
        }

        private Mesh ResolveNearRenderMesh()
        {
            return _generatedMesh != null ? _generatedMesh : _mesh;
        }

        private Mesh ResolveImpostorRenderMesh()
        {
            if (_generatedImpostorMesh != null)
                return _generatedImpostorMesh;

            if (_impostorMesh != null)
                return _impostorMesh;

            return ResolveNearRenderMesh();
        }

        private Material ResolveRenderMaterial()
        {
            if (!EnsureRenderMaterialResolved())
                return null;

            if (_material != null)
            {
                ReleaseRuntimeMaterial();
                return _material;
            }

            return _runtimeMaterial;
        }

        private bool EnsureRenderMaterialResolved()
        {
            if (_material != null)
                return true;

#if UNITY_EDITOR
            TryAutoAssignAssets();
#endif

            EnsureRuntimeMaterial();
            if (_runtimeMaterial != null)
                return true;

            EnsureRuntimeMaterial();
            return _runtimeMaterial != null;
        }

        private void EnsureBatchRendererGroupResources()
        {
            if (_batchRendererGroup != null)
                return;

            _batchRendererGroup = new BatchRendererGroup(new BatchRendererGroupCreateInfo
            {
                cullingCallback = OnPerformCulling,
                userContext = IntPtr.Zero
            });

            _batchMetadata = new NativeArray<MetadataValue>(BrgMetadataPlaceholderCount, Allocator.Persistent); // COLD ALLOC: NativeArray<MetadataValue>[1] - BRG metadata placeholder for vegetation renderer - owner: HectonIndirectVegetationRenderer
            NativeMemorySentinel.RegisterNativeArray(_batchMetadata, nameof(HectonIndirectVegetationRenderer), nameof(_batchMetadata), NativeAllocationLifetime.Session);
            _batchHandleBuffer = HectonBatchRendererGroupUtility.CreateBatchHandleBuffer(); // COLD ALLOC: GraphicsBuffer[1] - BRG registration handle buffer for vegetation renderer - owner: HectonIndirectVegetationRenderer
            _batchId = _batchRendererGroup.AddBatch(_batchMetadata, _batchHandleBuffer.bufferHandle);
        }

        private bool TryBindBrgMaterials()
        {
            GraphicsBuffer activeInstanceDataBuffer = ResolveActiveInstanceDataBuffer();
            if (activeInstanceDataBuffer == null)
                return false;

            Material sourceMaterial = ResolveRenderMaterial();
            if (sourceMaterial == null)
                return false;

            EnsureBrgMaterialClone(ref _nearBrgMaterial, sourceMaterial, "__HectonVegetationNearBrgMaterial");
            if (_nearBrgMaterial == null)
                return false;

            Mesh farMesh = FrameTimeWatchdog.IsDistantFloraRenderingEnabled && _farLodDistance > _nearLodDistance
                ? ResolveImpostorRenderMesh()
                : null;
            if (farMesh != null)
                EnsureBrgMaterialClone(ref _farBrgMaterial, sourceMaterial, "__HectonVegetationFarBrgMaterial");
            else
                ReleaseMaterialClone(ref _farBrgMaterial);

            if (_enableDepthPrepass && _depthOnlyMaterial != null)
            {
                EnsureBrgMaterialClone(ref _depthNearBrgMaterial, _depthOnlyMaterial, "__HectonVegetationDepthNearBrgMaterial");
                if (farMesh != null)
                    EnsureBrgMaterialClone(ref _depthFarBrgMaterial, _depthOnlyMaterial, "__HectonVegetationDepthFarBrgMaterial");
                else
                    ReleaseMaterialClone(ref _depthFarBrgMaterial);
            }
            else
            {
                ReleaseMaterialClone(ref _depthNearBrgMaterial);
                ReleaseMaterialClone(ref _depthFarBrgMaterial);
            }

            if (_enableShadowCasterDraw && _shadowCasterMaterial != null)
                EnsureBrgMaterialClone(ref _shadowBrgMaterial, _shadowCasterMaterial, "__HectonVegetationShadowBrgMaterial");
            else
                ReleaseMaterialClone(ref _shadowBrgMaterial);

            if (_enableMotionVectorDraw && _motionVectorMaterial != null)
            {
                EnsureBrgMaterialClone(ref _motionNearBrgMaterial, _motionVectorMaterial, "__HectonVegetationMotionNearBrgMaterial");
                if (farMesh != null)
                    EnsureBrgMaterialClone(ref _motionFarBrgMaterial, _motionVectorMaterial, "__HectonVegetationMotionFarBrgMaterial");
                else
                    ReleaseMaterialClone(ref _motionFarBrgMaterial);
            }
            else
            {
                ReleaseMaterialClone(ref _motionNearBrgMaterial);
                ReleaseMaterialClone(ref _motionFarBrgMaterial);
            }

            Vector4 globalFloatingOffset = ResolveVegetationFloatingOffset();
            EnsureAndDispatchFloraSnapFlags(activeInstanceDataBuffer, globalFloatingOffset);
            ApplyMaterialBindings(_nearBrgMaterial, activeInstanceDataBuffer, globalFloatingOffset, 0f, null, false);
            ApplyMaterialBindings(_farBrgMaterial, activeInstanceDataBuffer, globalFloatingOffset, 1f, null, false);
            ApplyMaterialBindings(_depthNearBrgMaterial, activeInstanceDataBuffer, globalFloatingOffset, 0f, null, false);
            ApplyMaterialBindings(_depthFarBrgMaterial, activeInstanceDataBuffer, globalFloatingOffset, 1f, null, false);
            ApplyMaterialBindings(_shadowBrgMaterial, activeInstanceDataBuffer, globalFloatingOffset, 0f, null, false);
            ApplyMaterialBindings(_motionNearBrgMaterial, activeInstanceDataBuffer, globalFloatingOffset, 0f, null, false);
            ApplyMaterialBindings(_motionFarBrgMaterial, activeInstanceDataBuffer, globalFloatingOffset, 1f, null, false);
            return true;
        }

        private bool TryBindGpuIndirectMaterials(GraphicsBuffer activeInstanceDataBuffer, Mesh farMesh)
        {
            if (activeInstanceDataBuffer == null ||
                _visibleIndicesLod0Buffer == null ||
                (_enableShadowCasterDraw && _visibleIndicesShadowBuffer == null))
            {
                return false;
            }

            Material sourceMaterial = ResolveRenderMaterial();
            if (sourceMaterial == null)
                return false;

            EnsureBrgMaterialClone(ref _nearBrgMaterial, sourceMaterial, "__HectonVegetationNearIndirectMaterial");
            if (_nearBrgMaterial == null)
                return false;

            if (farMesh != null)
                EnsureBrgMaterialClone(ref _farBrgMaterial, sourceMaterial, "__HectonVegetationFarIndirectMaterial");
            else
                ReleaseMaterialClone(ref _farBrgMaterial);

            if (_enableDepthPrepass && _depthOnlyMaterial != null)
            {
                EnsureBrgMaterialClone(ref _depthNearBrgMaterial, _depthOnlyMaterial, "__HectonVegetationDepthNearIndirectMaterial");
                if (farMesh != null)
                    EnsureBrgMaterialClone(ref _depthFarBrgMaterial, _depthOnlyMaterial, "__HectonVegetationDepthFarIndirectMaterial");
                else
                    ReleaseMaterialClone(ref _depthFarBrgMaterial);
            }
            else
            {
                ReleaseMaterialClone(ref _depthNearBrgMaterial);
                ReleaseMaterialClone(ref _depthFarBrgMaterial);
            }

            if (_enableShadowCasterDraw && _shadowCasterMaterial != null)
                EnsureBrgMaterialClone(ref _shadowBrgMaterial, _shadowCasterMaterial, "__HectonVegetationShadowIndirectMaterial");
            else
                ReleaseMaterialClone(ref _shadowBrgMaterial);

            if (_enableMotionVectorDraw && _motionVectorMaterial != null)
            {
                EnsureBrgMaterialClone(ref _motionNearBrgMaterial, _motionVectorMaterial, "__HectonVegetationMotionNearIndirectMaterial");
                if (farMesh != null)
                    EnsureBrgMaterialClone(ref _motionFarBrgMaterial, _motionVectorMaterial, "__HectonVegetationMotionFarIndirectMaterial");
                else
                    ReleaseMaterialClone(ref _motionFarBrgMaterial);
            }
            else
            {
                ReleaseMaterialClone(ref _motionNearBrgMaterial);
                ReleaseMaterialClone(ref _motionFarBrgMaterial);
            }

            Vector4 globalFloatingOffset = ResolveVegetationFloatingOffset();
            ApplyMaterialBindings(_nearBrgMaterial, activeInstanceDataBuffer, globalFloatingOffset, 0f, _visibleIndicesLod0Buffer, true);
            ApplyMaterialBindings(_farBrgMaterial, activeInstanceDataBuffer, globalFloatingOffset, 1f, _visibleIndicesLod1Buffer, true);
            ApplyMaterialBindings(_depthNearBrgMaterial, activeInstanceDataBuffer, globalFloatingOffset, 0f, _visibleIndicesLod0Buffer, true);
            ApplyMaterialBindings(_depthFarBrgMaterial, activeInstanceDataBuffer, globalFloatingOffset, 1f, _visibleIndicesLod1Buffer, true);
            ApplyMaterialBindings(_shadowBrgMaterial, activeInstanceDataBuffer, globalFloatingOffset, 0f, _visibleIndicesShadowBuffer, true);
            ApplyMaterialBindings(_motionNearBrgMaterial, activeInstanceDataBuffer, globalFloatingOffset, 0f, _visibleIndicesLod0Buffer, true);
            ApplyMaterialBindings(_motionFarBrgMaterial, activeInstanceDataBuffer, globalFloatingOffset, 1f, _visibleIndicesLod1Buffer, true);
            return true;
        }

        private void ApplyMaterialBindings(
            Material material,
            GraphicsBuffer activeInstanceDataBuffer,
            Vector4 globalFloatingOffset,
            float passMode,
            GraphicsBuffer visibleIndicesBuffer,
            bool useGpuIndirect)
        {
            if (material == null || _instanceMatrixBuffer == null || activeInstanceDataBuffer == null)
                return;

            material.enableInstancing = true;
            material.SetBuffer(_InstanceMatricesId, _instanceMatrixBuffer);
            material.SetBuffer(_InstanceDataId, activeInstanceDataBuffer);
            if (_floraPhaseSeedBuffer != null)
                material.SetBuffer(_FloraPhaseSeedsId, _floraPhaseSeedBuffer);
            if (_floraSnapFlagBuffer != null)
            {
                material.SetBuffer(_FloraSnapFlagsId, _floraSnapFlagBuffer);
                material.SetFloat(_FloraSnapFlagsEnabledId, 1f);
            }
            else
            {
                material.SetFloat(_FloraSnapFlagsEnabledId, 0f);
            }
            material.SetVector(_GlobalFloatingOffsetId, globalFloatingOffset);
            material.SetVector(_ChunkWorldOffsetId, globalFloatingOffset);
            material.SetFloat(_LodPassModeId, passMode);
            material.SetFloat(_LodNearDistanceId, _nearLodDistance);
            material.SetFloat(_LodFarDistanceId, _farLodDistance);
            material.SetFloat(_LodTransitionRangeId, _lodTransitionRange);
            material.SetFloat(_ImpostorWidthId, _impostorWidth);
            material.SetFloat(_ImpostorHeightId, _impostorHeight);
            if (useGpuIndirect && visibleIndicesBuffer != null)
            {
                material.EnableKeyword(GpuIndirectKeyword);
                material.SetBuffer(_VisibleInstanceIndicesId, visibleIndicesBuffer);
            }
            else
            {
                material.DisableKeyword(GpuIndirectKeyword);
            }
        }

        private bool TryRenderGpuIndirect(
            Camera cullCamera,
            Mesh nearMesh,
            Mesh farMesh,
            Vector3 cameraPosition,
            Vector3 cameraForward,
            Bounds drawBounds)
        {
            if (!_preferGpuIndirectRendering ||
                !SystemInfo.supportsComputeShaders ||
                cullCamera == null ||
                nearMesh == null ||
                _cullingCompute == null ||
                _clearIndirectArgsKernel < 0 ||
                _instanceMatrixBuffer == null ||
                _instanceCount <= 0)
            {
                return false;
            }

            GraphicsBuffer activeInstanceDataBuffer = ResolveActiveInstanceDataBuffer();
            if (activeInstanceDataBuffer == null)
                return false;

            if (_frustumPlaneCache == null || _frustumPlaneCache.Length != FrustumPlaneCount)
                return false;

            GeometryUtility.CalculateFrustumPlanes(cullCamera, _frustumPlaneCache);
            if (!GeometryUtility.TestPlanesAABB(_frustumPlaneCache, drawBounds))
                return true;
            PopulateFrustumPlaneUpload();

            if (_instanceCount <= 0)
                return true;

            EnsureGpuIndirectResources(_instanceCount, nearMesh, farMesh);
            if (_visibleIndicesLod0Buffer == null || _indirectArgsLod0Buffer == null)
                return false;

            if (!TryBindGpuIndirectMaterials(activeInstanceDataBuffer, farMesh))
                return false;

            UpdateMotionVectorHistory(cullCamera, cameraPosition);
            bool depthPyramidReady = BuildDepthPyramid(cullCamera);
            DispatchGpuCulling(cullCamera, activeInstanceDataBuffer, depthPyramidReady, cameraPosition, cameraForward);

            RenderIndirectPass(_nearBrgMaterial, nearMesh, _indirectArgsLod0Buffer, drawBounds, ShadowCastingMode.Off, _receiveShadows, MotionVectorGenerationMode.Camera, cullCamera);
            if (farMesh != null && _farBrgMaterial != null && _indirectArgsLod1Buffer != null)
                RenderIndirectPass(_farBrgMaterial, farMesh, _indirectArgsLod1Buffer, drawBounds, ShadowCastingMode.Off, _impostorReceiveShadows, MotionVectorGenerationMode.Camera, cullCamera);

            if (_enableDepthPrepass)
            {
                RenderIndirectPass(_depthNearBrgMaterial, nearMesh, _indirectArgsLod0Buffer, drawBounds, ShadowCastingMode.Off, false, MotionVectorGenerationMode.Camera, cullCamera);
                if (farMesh != null && _depthFarBrgMaterial != null && _indirectArgsLod1Buffer != null)
                    RenderIndirectPass(_depthFarBrgMaterial, farMesh, _indirectArgsLod1Buffer, drawBounds, ShadowCastingMode.Off, false, MotionVectorGenerationMode.Camera, cullCamera);
            }

            if (_enableShadowCasterDraw && _shadowBrgMaterial != null && _indirectArgsShadowBuffer != null && HasMainDirectionalShadowLight())
                RenderIndirectPass(_shadowBrgMaterial, nearMesh, _indirectArgsShadowBuffer, drawBounds, ShadowCastingMode.On, false, MotionVectorGenerationMode.Camera, cullCamera);

            if (_enableMotionVectorDraw)
            {
                RenderIndirectPass(_motionNearBrgMaterial, nearMesh, _indirectArgsLod0Buffer, drawBounds, ShadowCastingMode.Off, false, MotionVectorGenerationMode.Object, cullCamera);
                if (farMesh != null && _motionFarBrgMaterial != null && _indirectArgsLod1Buffer != null)
                    RenderIndirectPass(_motionFarBrgMaterial, farMesh, _indirectArgsLod1Buffer, drawBounds, ShadowCastingMode.Off, false, MotionVectorGenerationMode.Object, cullCamera);
            }

            return true;
        }

        private void RenderIndirectPass(
            Material material,
            Mesh mesh,
            GraphicsBuffer argsBuffer,
            Bounds drawBounds,
            ShadowCastingMode shadowCastingMode,
            bool receiveShadows,
            MotionVectorGenerationMode motionVectorMode,
            Camera cullCamera)
        {
            if (material == null || mesh == null || argsBuffer == null)
                return;

            RenderParams renderParams = new RenderParams(material)
            {
                worldBounds = drawBounds,
                layer = gameObject.layer,
                shadowCastingMode = shadowCastingMode,
                receiveShadows = receiveShadows,
                motionVectorMode = motionVectorMode,
                camera = _cameraOverride != null ? _cameraOverride : cullCamera
            };
            Graphics.RenderMeshIndirect(renderParams, mesh, argsBuffer, 1, 0);
        }

        private void DispatchGpuCulling(
            Camera cullCamera,
            GraphicsBuffer activeInstanceDataBuffer,
            bool depthPyramidReady,
            Vector3 cameraPosition,
            Vector3 cameraForward)
        {
            if (_cullingCompute == null ||
                _cullFloraKernel < 0 ||
                _visibleIndicesLod0Buffer == null ||
                _indirectArgsLod0Buffer == null ||
                _instanceCount <= 0)
            {
                return;
            }

            Vector4 globalFloatingOffset = ResolveVegetationFloatingOffset();
            Matrix4x4 viewProjection = GL.GetGPUProjectionMatrix(cullCamera.projectionMatrix, false) * cullCamera.worldToCameraMatrix;
            Matrix4x4 viewMatrix = cullCamera.worldToCameraMatrix;

            Mesh nearMesh = ResolveNearRenderMesh();
            Mesh farMesh = FrameTimeWatchdog.IsDistantFloraRenderingEnabled && _farLodDistance > _nearLodDistance
                ? ResolveImpostorRenderMesh()
                : null;
            float brgLodDistanceScalar = VRAMPressureMonitor.BrgLodDistanceScalar;
            float brgNearLodDistance = Mathf.Max(0.01f, _nearLodDistance * brgLodDistanceScalar);
            float brgFarLodDistance = Mathf.Max(brgNearLodDistance, _farLodDistance * brgLodDistanceScalar);
            float brgLodTransitionRange = Mathf.Max(0.01f, _lodTransitionRange * brgLodDistanceScalar);
            bool hasFarLod = farMesh != null && _visibleIndicesLod1Buffer != null && _indirectArgsLod1Buffer != null;
            bool farCadenceEligible = hasFarLod &&
                                      _farCullingFrameStride > 1 &&
                                      brgFarLodDistance > _farCullingCadenceDistance;
            bool updateFarLodThisFrame = hasFarLod &&
                                         (!_hasFarCullingSnapshot ||
                                          !farCadenceEligible ||
                                          (_gpuCullingFrameIndex % _farCullingFrameStride) == 0);
            _gpuCullingFrameIndex = (_gpuCullingFrameIndex + 1) & 0x3fffffff;
            if (!hasFarLod)
                _hasFarCullingSnapshot = false;

            _visibleIndicesLod0Buffer.SetCounterValue(0u);
            if (updateFarLodThisFrame)
                _visibleIndicesLod1Buffer.SetCounterValue(0u);
            _visibleIndicesShadowBuffer?.SetCounterValue(0u);

            if (!ClearIndirectArgsBuffer(_indirectArgsLod0Buffer, nearMesh) ||
                (hasFarLod && updateFarLodThisFrame && !ClearIndirectArgsBuffer(_indirectArgsLod1Buffer, farMesh)) ||
                (!hasFarLod && _indirectArgsLod1Buffer != null && !ClearIndirectArgsBuffer(_indirectArgsLod1Buffer, nearMesh)) ||
                !ClearIndirectArgsBuffer(_indirectArgsShadowBuffer, nearMesh))
            {
                return;
            }

            _cullingCompute.SetBuffer(_cullFloraKernel, _SourceMatricesId, _instanceMatrixBuffer);
            _cullingCompute.SetBuffer(_cullFloraKernel, _SourceDataId, activeInstanceDataBuffer);
            _cullingCompute.SetBuffer(_cullFloraKernel, _VisibleIndicesLod0Id, _visibleIndicesLod0Buffer);
            if (_visibleIndicesLod1Buffer != null)
                _cullingCompute.SetBuffer(_cullFloraKernel, _VisibleIndicesLod1Id, _visibleIndicesLod1Buffer);
            _cullingCompute.SetInt(_FarLodAppendEnabledId, updateFarLodThisFrame ? 1 : 0);
            _cullingCompute.SetInt(_SourceInstanceCountId, _instanceCount);
            _cullingCompute.SetMatrix(_ViewProjectionId, viewProjection);
            _cullingCompute.SetMatrix(_ViewMatrixId, viewMatrix);
            _cullingCompute.SetVector(_CameraPositionId, cameraPosition);
            _cullingCompute.SetVector(_CameraForwardId, cameraForward);
            _cullingCompute.SetVector(_GlobalFloatingOffsetId, globalFloatingOffset);
            _cullingCompute.SetFloat(_LodNearDistanceId, brgNearLodDistance);
            _cullingCompute.SetFloat(_LodFarDistanceId, brgFarLodDistance);
            _cullingCompute.SetFloat(_LodTransitionRangeId, brgLodTransitionRange);
            float peripheralCullDot = Mathf.Clamp(_peripheralCullDot, -1f, 1f);
            float peripheralCullDistance = Mathf.Max(0f, _peripheralCullDistance);
            float peripheralCullDistanceSq = peripheralCullDistance * peripheralCullDistance;
            _cullingCompute.SetFloat(_PeripheralCullDotId, peripheralCullDot);
            _cullingCompute.SetFloat(_PeripheralCullDistanceSqId, peripheralCullDistanceSq);
            _cullingCompute.SetFloat(_OcclusionDepthBiasId, _occlusionDepthBias);
            _cullingCompute.SetInt(_OcclusionEnabledId, depthPyramidReady && _enableDepthOcclusion ? 1 : 0);
            _cullingCompute.SetVector(_OcclusionZBufferParamsId, Shader.GetGlobalVector(_GlobalZBufferParamsId));
            _cullingCompute.SetInt(_DarknessCullEnabledId, _enableDarknessCulling ? 1 : 0);
            _cullingCompute.SetFloat(_DarknessBiolumThresholdId, _darknessBiolumThreshold);
            _cullingCompute.SetVectorArray(_FrustumPlanesId, _frustumPlaneVectors);
            if (_depthPyramidTexture != null)
                _cullingCompute.SetTexture(_cullFloraKernel, _DepthPyramidTextureId, _depthPyramidTexture);
            _cullingCompute.SetInt(_DepthPyramidMipCountId, _depthPyramidMipCount);
            _cullingCompute.SetVector(_DepthPyramidTexelSizeId, new Vector4(
                _depthPyramidWidth > 0 ? 1f / _depthPyramidWidth : 0f,
                _depthPyramidHeight > 0 ? 1f / _depthPyramidHeight : 0f,
                _depthPyramidWidth,
                _depthPyramidHeight));

            int headlightCount = CopyScooterHeadlightPayload();
            _cullingCompute.SetInt(_ScooterHeadlightCountId, headlightCount);
            _cullingCompute.SetVectorArray(_ScooterHeadlightPositionsWsId, _scooterHeadlightPositionsWs);
            _cullingCompute.SetVectorArray(_ScooterHeadlightDirectionsWsId, _scooterHeadlightDirectionsWs);
            _cullingCompute.SetVectorArray(_ScooterHeadlightColorsId, _scooterHeadlightColors);
            _cullingCompute.SetVectorArray(_ScooterHeadlightConeDataId, _scooterHeadlightConeData);
            _cullingCompute.SetFloat(_FloorBiolumStrengthId, Shader.GetGlobalFloat(_FloorBiolumStrengthId));
            _cullingCompute.SetFloat(_OceanBiolumStrengthId, Shader.GetGlobalFloat(_OceanBiolumStrengthId));
            _cullingCompute.SetFloat(_GlobalBiolumIntensityId, Shader.GetGlobalFloat(_GlobalBiolumIntensityId));

            int dispatchGroups = Mathf.Max(1, (_instanceCount + ThreadsPerGroup - 1) / ThreadsPerGroup);
            DispatchFloraSnapFlagUpdate(activeInstanceDataBuffer, globalFloatingOffset, dispatchGroups);
            _cullingCompute.Dispatch(_cullFloraKernel, dispatchGroups, 1, 1);

            if (_visibleIndicesShadowBuffer != null && _cullFloraShadowKernel >= 0)
            {
                _cullingCompute.SetBuffer(_cullFloraShadowKernel, _SourceMatricesId, _instanceMatrixBuffer);
                _cullingCompute.SetBuffer(_cullFloraShadowKernel, _SourceDataId, activeInstanceDataBuffer);
                _cullingCompute.SetBuffer(_cullFloraShadowKernel, _VisibleIndicesShadowId, _visibleIndicesShadowBuffer);
                _cullingCompute.SetInt(_SourceInstanceCountId, _instanceCount);
                _cullingCompute.SetMatrix(_ViewProjectionId, viewProjection);
                _cullingCompute.SetMatrix(_ViewMatrixId, viewMatrix);
                _cullingCompute.SetVector(_CameraPositionId, cameraPosition);
                _cullingCompute.SetVector(_CameraForwardId, cameraForward);
                _cullingCompute.SetVector(_GlobalFloatingOffsetId, globalFloatingOffset);
                _cullingCompute.SetFloat(_LodNearDistanceId, brgNearLodDistance);
                _cullingCompute.SetFloat(_LodFarDistanceId, brgFarLodDistance);
                _cullingCompute.SetFloat(_LodTransitionRangeId, brgLodTransitionRange);
                _cullingCompute.SetFloat(_PeripheralCullDotId, peripheralCullDot);
                _cullingCompute.SetFloat(_PeripheralCullDistanceSqId, peripheralCullDistanceSq);
                _cullingCompute.SetInt(_DarknessCullEnabledId, _enableDarknessCulling ? 1 : 0);
                _cullingCompute.SetFloat(_DarknessBiolumThresholdId, _darknessBiolumThreshold);
                _cullingCompute.SetVectorArray(_ScooterHeadlightPositionsWsId, _scooterHeadlightPositionsWs);
                _cullingCompute.SetVectorArray(_ScooterHeadlightDirectionsWsId, _scooterHeadlightDirectionsWs);
                _cullingCompute.SetVectorArray(_ScooterHeadlightColorsId, _scooterHeadlightColors);
                _cullingCompute.SetVectorArray(_ScooterHeadlightConeDataId, _scooterHeadlightConeData);
                _cullingCompute.SetVectorArray(_FrustumPlanesId, _frustumPlaneVectors);
                _cullingCompute.Dispatch(_cullFloraShadowKernel, dispatchGroups, 1, 1);
            }

            GraphicsBuffer.CopyCount(_visibleIndicesLod0Buffer, _indirectArgsLod0Buffer, sizeof(uint));
            if (updateFarLodThisFrame && _visibleIndicesLod1Buffer != null && _indirectArgsLod1Buffer != null)
            {
                GraphicsBuffer.CopyCount(_visibleIndicesLod1Buffer, _indirectArgsLod1Buffer, sizeof(uint));
                _hasFarCullingSnapshot = true;
            }
            if (_visibleIndicesShadowBuffer != null && _indirectArgsShadowBuffer != null)
                GraphicsBuffer.CopyCount(_visibleIndicesShadowBuffer, _indirectArgsShadowBuffer, sizeof(uint));
        }

        private void DispatchFloraSnapFlagUpdate(GraphicsBuffer activeInstanceDataBuffer, Vector4 globalFloatingOffset, int dispatchGroups)
        {
            if (_abyssalFlowFieldCompute == null ||
                _flagSnappedFloraKernel < 0 ||
                _floraSnapFlagBuffer == null ||
                _instanceMatrixBuffer == null ||
                activeInstanceDataBuffer == null ||
                _instanceCount <= 0)
            {
                return;
            }

            if (_floraSnapFlagBufferRequiresClear && _clearFloraSnapFlagsKernel >= 0)
            {
                _abyssalFlowFieldCompute.SetBuffer(_clearFloraSnapFlagsKernel, _FloraSnapFlagsId, _floraSnapFlagBuffer);
                _abyssalFlowFieldCompute.SetInt(_SourceInstanceCountId, _instanceCount);
                _abyssalFlowFieldCompute.Dispatch(_clearFloraSnapFlagsKernel, dispatchGroups, 1, 1);
                _floraSnapFlagBufferRequiresClear = false;
            }

            Vector4 washVelocity = Shader.GetGlobalVector(_SubmarineWashVelocityId);
            Vector4 washSphere = Shader.GetGlobalVector(_SubmarineWashSphereId);
            if (washVelocity.w <= 10f || washSphere.w <= 0f)
                return;

            _abyssalFlowFieldCompute.SetBuffer(_flagSnappedFloraKernel, _SourceMatricesId, _instanceMatrixBuffer);
            _abyssalFlowFieldCompute.SetBuffer(_flagSnappedFloraKernel, _SourceDataId, activeInstanceDataBuffer);
            _abyssalFlowFieldCompute.SetBuffer(_flagSnappedFloraKernel, _FloraSnapFlagsId, _floraSnapFlagBuffer);
            _abyssalFlowFieldCompute.SetInt(_SourceInstanceCountId, _instanceCount);
            _abyssalFlowFieldCompute.SetVector(_GlobalFloatingOffsetId, globalFloatingOffset);
            _abyssalFlowFieldCompute.SetVector(_SubmarineWashSphereId, washSphere);
            _abyssalFlowFieldCompute.SetVector(_SubmarineWashVelocityId, washVelocity);
            _abyssalFlowFieldCompute.Dispatch(_flagSnappedFloraKernel, dispatchGroups, 1, 1);
        }

        private void EnsureAndDispatchFloraSnapFlags(GraphicsBuffer activeInstanceDataBuffer, Vector4 globalFloatingOffset)
        {
            if (!SystemInfo.supportsComputeShaders ||
                _abyssalFlowFieldCompute == null ||
                _clearFloraSnapFlagsKernel < 0 ||
                _flagSnappedFloraKernel < 0 ||
                _instanceMatrixBuffer == null ||
                activeInstanceDataBuffer == null ||
                _instanceCount <= 0)
            {
                ReleaseFloraSnapFlagBuffer();
                return;
            }

            EnsureFloraSnapFlagBufferCapacity(Mathf.NextPowerOfTwo(Mathf.Max(1, _instanceCount)));
            if (_floraSnapFlagBuffer == null)
                return;

            int dispatchGroups = Mathf.Max(1, (_instanceCount + ThreadsPerGroup - 1) / ThreadsPerGroup);
            DispatchFloraSnapFlagUpdate(activeInstanceDataBuffer, globalFloatingOffset, dispatchGroups);
        }

        private bool BuildDepthPyramid(Camera cullCamera)
        {
            if (!_enableDepthOcclusion || _depthPyramidCompute == null || cullCamera == null)
                return false;

            Texture depthTexture = Shader.GetGlobalTexture(_GlobalCameraDepthTextureId);
            if (depthTexture == null)
                return false;

            int targetWidth = Mathf.Max(1, cullCamera.pixelWidth);
            int targetHeight = Mathf.Max(1, cullCamera.pixelHeight);
            EnsureDepthPyramidResources(targetWidth, targetHeight);
            if (_depthPyramidTexture == null || _depthPyramidCopyKernel < 0 || _depthPyramidDownsampleKernel < 0)
                return false;

            _depthPyramidCompute.SetTexture(_depthPyramidCopyKernel, _DepthPyramidSourceDepthId, depthTexture);
            _depthPyramidCompute.SetTexture(_depthPyramidCopyKernel, _DepthPyramidTargetId, _depthPyramidTexture, 0);
            _depthPyramidCompute.Dispatch(
                _depthPyramidCopyKernel,
                Mathf.Max(1, (_depthPyramidWidth + 7) / 8),
                Mathf.Max(1, (_depthPyramidHeight + 7) / 8),
                1);

            for (int mipIndex = 1; mipIndex < _depthPyramidMipCount; mipIndex++)
            {
                int mipWidth = Mathf.Max(1, _depthPyramidWidth >> mipIndex);
                int mipHeight = Mathf.Max(1, _depthPyramidHeight >> mipIndex);
                _depthPyramidCompute.SetTexture(_depthPyramidDownsampleKernel, _DepthPyramidSourceId, _depthPyramidTexture, mipIndex - 1);
                _depthPyramidCompute.SetTexture(_depthPyramidDownsampleKernel, _DepthPyramidTargetId, _depthPyramidTexture, mipIndex);
                _depthPyramidCompute.Dispatch(
                    _depthPyramidDownsampleKernel,
                    Mathf.Max(1, (mipWidth + 7) / 8),
                    Mathf.Max(1, (mipHeight + 7) / 8),
                    1);
            }

            return true;
        }

        private void EnsureDepthPyramidResources(int targetWidth, int targetHeight)
        {
            if (targetWidth <= 0 || targetHeight <= 0)
                return;

            if (_depthPyramidTexture != null && _depthPyramidWidth == targetWidth && _depthPyramidHeight == targetHeight)
                return;

            ReleaseDepthPyramidTexture();
            _depthPyramidWidth = targetWidth;
            _depthPyramidHeight = targetHeight;
            _depthPyramidMipCount = ResolveMipCountNoLog(targetWidth, targetHeight);

            _depthPyramidTexture = new RenderTexture(targetWidth, targetHeight, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear)
            {
                name = "__HectonVegetationDepthPyramid",
                hideFlags = HideFlags.HideAndDontSave,
                enableRandomWrite = true,
                useMipMap = true,
                autoGenerateMips = false,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            }; // COLD ALLOC: RenderTexture[targetWidth x targetHeight] - vegetation Hi-Z depth pyramid for compute occlusion - owner: HectonIndirectVegetationRenderer
            _depthPyramidTexture.Create();
        }

        private static int ResolveMipCountNoLog(int width, int height)
        {
            int size = math.max(1, math.max(width, height));
            int count = 1;
            count += size >= 2 ? 1 : 0;
            count += size >= 4 ? 1 : 0;
            count += size >= 8 ? 1 : 0;
            count += size >= 16 ? 1 : 0;
            count += size >= 32 ? 1 : 0;
            count += size >= 64 ? 1 : 0;
            count += size >= 128 ? 1 : 0;
            count += size >= 256 ? 1 : 0;
            count += size >= 512 ? 1 : 0;
            count += size >= 1024 ? 1 : 0;
            count += size >= 2048 ? 1 : 0;
            count += size >= 4096 ? 1 : 0;
            count += size >= 8192 ? 1 : 0;
            count += size >= 16384 ? 1 : 0;
            count += size >= 32768 ? 1 : 0;
            return count;
        }

        private void EnsureGpuIndirectResources(int instanceCount, Mesh nearMesh, Mesh farMesh)
        {
            int requiredCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, instanceCount));
            if (requiredCapacity != _gpuVisibleIndexCapacity)
            {
                ReleaseVisibleIndexBuffer(ref _visibleIndicesLod0Buffer);
                ReleaseVisibleIndexBuffer(ref _visibleIndicesLod1Buffer);
                ReleaseVisibleIndexBuffer(ref _visibleIndicesShadowBuffer);
                _visibleIndicesLod0Buffer = new GraphicsBuffer(GraphicsBuffer.Target.Append, requiredCapacity, VisibleIndexStride); // COLD ALLOC: GraphicsBuffer[visibleCapacity] - near vegetation visible-instance append buffer - owner: HectonIndirectVegetationRenderer
                _visibleIndicesLod1Buffer = new GraphicsBuffer(GraphicsBuffer.Target.Append, requiredCapacity, VisibleIndexStride); // COLD ALLOC: GraphicsBuffer[visibleCapacity] - far vegetation visible-instance append buffer - owner: HectonIndirectVegetationRenderer
                _visibleIndicesShadowBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Append, requiredCapacity, VisibleIndexStride); // COLD ALLOC: GraphicsBuffer[visibleCapacity] - shadow vegetation visible-instance append buffer - owner: HectonIndirectVegetationRenderer
                _gpuVisibleIndexCapacity = requiredCapacity;
                _hasFarCullingSnapshot = false;
            }

            EnsureIndirectArgsBuffer(ref _indirectArgsLod0Buffer);
            EnsureIndirectArgsBuffer(ref _indirectArgsLod1Buffer);
            EnsureIndirectArgsBuffer(ref _indirectArgsShadowBuffer);
            if (_abyssalFlowFieldCompute != null && _clearFloraSnapFlagsKernel >= 0 && _flagSnappedFloraKernel >= 0)
                EnsureFloraSnapFlagBufferCapacity(requiredCapacity);
            else
                ReleaseFloraSnapFlagBuffer();
        }

        private void EnsureIndirectArgsBuffer(ref GraphicsBuffer argsBuffer)
        {
            if (argsBuffer == null)
                argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - GPU-cleared indirect indexed draw arguments for vegetation pass - owner: HectonIndirectVegetationRenderer
        }

        private void EnsureFloraSnapFlagBufferCapacity(int requiredCapacity)
        {
            if (requiredCapacity <= 0)
                return;

            if (_floraSnapFlagBuffer != null &&
                _floraSnapFlagBuffer.IsValid() &&
                _floraSnapFlagCapacity >= requiredCapacity)
            {
                return;
            }

            ReleaseGraphicsBuffer(ref _floraSnapFlagBuffer);
            _floraSnapFlagBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, requiredCapacity, sizeof(uint)); // COLD ALLOC: GraphicsBuffer[visibleCapacity] — persistent GPU-only snapped flora flags — owner: HectonIndirectVegetationRenderer
            _floraSnapFlagCapacity = requiredCapacity;
            _floraSnapFlagBufferRequiresClear = true;
        }

        private void ReleaseFloraSnapFlagBuffer()
        {
            ReleaseGraphicsBuffer(ref _floraSnapFlagBuffer);
            _floraSnapFlagCapacity = 0;
            _floraSnapFlagBufferRequiresClear = false;
        }

        private bool ClearIndirectArgsBuffer(GraphicsBuffer argsBuffer, Mesh mesh)
        {
            if (_cullingCompute == null ||
                _clearIndirectArgsKernel < 0 ||
                argsBuffer == null ||
                mesh == null)
            {
                return false;
            }

            _cullingCompute.SetBuffer(_clearIndirectArgsKernel, _IndirectArgsBufferId, argsBuffer);
            uint indexCount = mesh.GetIndexCount(_subMeshIndex);
            uint startIndex = mesh.GetIndexStart(_subMeshIndex);
            _cullingCompute.SetInt(_IndirectIndexCountPerInstanceId, indexCount > int.MaxValue ? int.MaxValue : (int)indexCount);
            _cullingCompute.SetInt(_IndirectStartIndexId, startIndex > int.MaxValue ? int.MaxValue : (int)startIndex);
            uint baseVertexIndex = (uint)mesh.GetBaseVertex(_subMeshIndex);
            _cullingCompute.SetInt(_IndirectBaseVertexIndexId, baseVertexIndex > int.MaxValue ? int.MaxValue : (int)baseVertexIndex);
            _cullingCompute.Dispatch(_clearIndirectArgsKernel, 1, 1, 1);
            return true;
        }

        private void PopulateFrustumPlaneUpload()
        {
            if (_frustumPlaneCache == null || _frustumPlaneVectors == null)
                return;

            for (int planeIndex = 0; planeIndex < FrustumPlaneCount; planeIndex++)
            {
                Plane plane = _frustumPlaneCache[planeIndex];
                _frustumPlaneVectors[planeIndex] = new Vector4(plane.normal.x, plane.normal.y, plane.normal.z, plane.distance);
            }
        }

        private void ReleaseGpuIndirectResources()
        {
            ReleaseVisibleIndexBuffer(ref _visibleIndicesLod0Buffer);
            ReleaseVisibleIndexBuffer(ref _visibleIndicesLod1Buffer);
            ReleaseVisibleIndexBuffer(ref _visibleIndicesShadowBuffer);
            ReleaseFloraSnapFlagBuffer();
            ReleaseGraphicsBuffer(ref _indirectArgsLod0Buffer);
            ReleaseGraphicsBuffer(ref _indirectArgsLod1Buffer);
            ReleaseGraphicsBuffer(ref _indirectArgsShadowBuffer);
            ReleaseDepthPyramidTexture();
            _gpuVisibleIndexCapacity = 0;
            _gpuCullingFrameIndex = 0;
            _hasFarCullingSnapshot = false;
            _depthPyramidWidth = 0;
            _depthPyramidHeight = 0;
            _depthPyramidMipCount = 0;
        }

        private static void ReleaseVisibleIndexBuffer(ref GraphicsBuffer buffer)
        {
            ReleaseGraphicsBuffer(ref buffer);
        }

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private void ReleaseDepthPyramidTexture()
        {
            if (_depthPyramidTexture == null)
                return;

            _depthPyramidTexture.Release();
            if (Application.isPlaying)
                Destroy(_depthPyramidTexture);
            else
                DestroyImmediate(_depthPyramidTexture);

            _depthPyramidTexture = null;
        }

        private void EnsureBrgMaterialClone(ref Material target, Material source, string materialName)
        {
            if (source == null)
            {
                ReleaseMaterialClone(ref target);
                return;
            }

            if (target != null && target.shader == source.shader)
                return;

            ReleaseMaterialClone(ref target);
            target = new Material(source)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = materialName,
                enableInstancing = true
            }; // COLD ALLOC: Material[1] - BRG-local vegetation pass material clone for per-renderer buffer binding - owner: HectonIndirectVegetationRenderer
        }

        private void ReleaseMaterialClone(ref Material target)
        {
            if (target == null)
                return;

            ReleaseRegisteredBrgMaterial(target);

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);

            target = null;
        }

        private void ReleaseRegisteredBrgMaterial(Material material)
        {
            if (material == null)
                return;

            if (ReferenceEquals(_registeredNearBrgMaterial, material))
            {
                UnregisterBatchMaterial(ref _nearBatchMaterialId);
                _registeredNearBrgMaterial = null;
            }

            if (ReferenceEquals(_registeredFarBrgMaterial, material))
            {
                UnregisterBatchMaterial(ref _farBatchMaterialId);
                _registeredFarBrgMaterial = null;
            }

            if (ReferenceEquals(_registeredDepthNearBrgMaterial, material))
            {
                UnregisterBatchMaterial(ref _depthNearBatchMaterialId);
                _registeredDepthNearBrgMaterial = null;
            }

            if (ReferenceEquals(_registeredDepthFarBrgMaterial, material))
            {
                UnregisterBatchMaterial(ref _depthFarBatchMaterialId);
                _registeredDepthFarBrgMaterial = null;
            }

            if (ReferenceEquals(_registeredShadowBrgMaterial, material))
            {
                UnregisterBatchMaterial(ref _shadowBatchMaterialId);
                _registeredShadowBrgMaterial = null;
            }

            if (ReferenceEquals(_registeredMotionNearBrgMaterial, material))
            {
                UnregisterBatchMaterial(ref _motionNearBatchMaterialId);
                _registeredMotionNearBrgMaterial = null;
            }

            if (ReferenceEquals(_registeredMotionFarBrgMaterial, material))
            {
                UnregisterBatchMaterial(ref _motionFarBatchMaterialId);
                _registeredMotionFarBrgMaterial = null;
            }
        }

        private void SyncBatchRegistration(Mesh nearMesh, Mesh farMesh)
        {
            if (_batchRendererGroup == null)
                return;

            SyncBatchMesh(ref _nearBatchMeshId, ref _registeredNearMesh, nearMesh);
            SyncBatchMesh(ref _farBatchMeshId, ref _registeredFarMesh, farMesh);
            SyncBatchMaterial(ref _nearBatchMaterialId, ref _registeredNearBrgMaterial, _nearBrgMaterial);
            SyncBatchMaterial(ref _farBatchMaterialId, ref _registeredFarBrgMaterial, _farBrgMaterial);
            SyncBatchMaterial(ref _depthNearBatchMaterialId, ref _registeredDepthNearBrgMaterial, _depthNearBrgMaterial);
            SyncBatchMaterial(ref _depthFarBatchMaterialId, ref _registeredDepthFarBrgMaterial, _depthFarBrgMaterial);
            SyncBatchMaterial(ref _shadowBatchMaterialId, ref _registeredShadowBrgMaterial, _shadowBrgMaterial);
            SyncBatchMaterial(ref _motionNearBatchMaterialId, ref _registeredMotionNearBrgMaterial, _motionNearBrgMaterial);
            SyncBatchMaterial(ref _motionFarBatchMaterialId, ref _registeredMotionFarBrgMaterial, _motionFarBrgMaterial);
        }

        private void SyncBatchMesh(ref BatchMeshID batchMeshId, ref Mesh registeredMesh, Mesh mesh)
        {
            if (_batchRendererGroup == null)
                return;

            if (registeredMesh == mesh)
                return;

            if (!batchMeshId.Equals(default))
                _batchRendererGroup.UnregisterMesh(batchMeshId);

            batchMeshId = mesh != null ? _batchRendererGroup.RegisterMesh(mesh) : default;
            registeredMesh = mesh;
        }

        private void SyncBatchMaterial(ref BatchMaterialID batchMaterialId, ref Material registeredMaterial, Material material)
        {
            if (_batchRendererGroup == null)
                return;

            if (registeredMaterial == material)
                return;

            if (!batchMaterialId.Equals(default))
                _batchRendererGroup.UnregisterMaterial(batchMaterialId);

            batchMaterialId = material != null ? _batchRendererGroup.RegisterMaterial(material) : default;
            registeredMaterial = material;
        }

        private void SyncBatchBuffer(GraphicsBuffer matrixBuffer)
        {
            if (_batchRendererGroup == null || _batchId.Equals(default) || matrixBuffer == null)
                return;

            if (ReferenceEquals(_registeredBatchBuffer, matrixBuffer))
                return;

            _batchRendererGroup.SetBatchBuffer(_batchId, matrixBuffer.bufferHandle);
            _registeredBatchBuffer = matrixBuffer;
        }

        private void UpdateMotionVectorHistory(Camera renderCamera, Vector3 currentCameraPosition)
        {
            if (_motionNearBrgMaterial == null && _motionFarBrgMaterial == null)
                return;

            if (renderCamera == null)
                return;

            Vector3 previousCameraPosition = _hasPreviousMotionCameraPosition && _previousMotionCamera == renderCamera
                ? _previousMotionCameraPosition
                : currentCameraPosition;

            _motionNearBrgMaterial?.SetVector(_PreviousCameraPositionId, previousCameraPosition);
            _motionFarBrgMaterial?.SetVector(_PreviousCameraPositionId, previousCameraPosition);

            _previousMotionCameraPosition = currentCameraPosition;
            _previousMotionCamera = renderCamera;
            _hasPreviousMotionCameraPosition = true;
        }

        private void EnsureCpuCullingCapacity(int instanceCount)
        {
            if (instanceCount <= 0)
                return;

            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(16, instanceCount));
            if (_cpuCullingMatrices.IsCreated &&
                _cpuCullingMatrices.Length >= nextCapacity &&
                _cpuCullingData.IsCreated &&
                _cpuCullingData.Length >= nextCapacity)
            {
                return;
            }

            ReleaseCpuCullingData();
            _cpuCullingMatrices = new NativeArray<Matrix4x4>(nextCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<Matrix4x4>[NextPowerOfTwo(requiredCount)] - CPU BRG vegetation culling matrices - owner: HectonIndirectVegetationRenderer
            _cpuCullingData = new NativeArray<HectonVegetationInstanceData>(nextCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<HectonVegetationInstanceData>[NextPowerOfTwo(requiredCount)] - CPU BRG vegetation culling metadata - owner: HectonIndirectVegetationRenderer
            NativeMemorySentinel.RegisterNativeArray(_cpuCullingMatrices, nameof(HectonIndirectVegetationRenderer), nameof(_cpuCullingMatrices), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_cpuCullingData, nameof(HectonIndirectVegetationRenderer), nameof(_cpuCullingData), NativeAllocationLifetime.Session);
        }

        private void ReleaseCpuCullingData()
        {
            if (_cpuCullingMatrices.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_cpuCullingMatrices);
                _cpuCullingMatrices.Dispose();
            }

            if (_cpuCullingData.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_cpuCullingData);
                _cpuCullingData.Dispose();
            }

            _hasCpuCullingData = false;
        }

        private void CopyCpuCullingPayload(
            Matrix4x4[] instanceMatrices,
            HectonVegetationInstanceData[] instanceData,
            int instanceCount)
        {
            if (instanceMatrices == null || instanceCount <= 0 || instanceMatrices.Length < instanceCount)
            {
                _hasCpuCullingData = false;
                return;
            }

            EnsureCpuCullingCapacity(instanceCount);
            HectonVegetationInstanceData fallbackPayload = CreateLegacyDefaultPayload();
            for (int instanceIndex = 0; instanceIndex < instanceCount; instanceIndex++)
            {
                _cpuCullingMatrices[instanceIndex] = instanceMatrices[instanceIndex];
                _cpuCullingData[instanceIndex] = instanceData != null && instanceData.Length > instanceIndex
                    ? instanceData[instanceIndex]
                    : fallbackPayload;
            }

            _hasCpuCullingData = true;
        }

        private void CopyCpuCullingPayload(
            NativeArray<Matrix4x4> instanceMatrices,
            NativeArray<HectonVegetationInstanceData> instanceData,
            int instanceCount)
        {
            if (!instanceMatrices.IsCreated || !instanceData.IsCreated || instanceCount <= 0)
            {
                _hasCpuCullingData = false;
                return;
            }

            EnsureCpuCullingCapacity(instanceCount);
            NativeArray<Matrix4x4>.Copy(instanceMatrices, _cpuCullingMatrices, instanceCount);
            NativeArray<HectonVegetationInstanceData>.Copy(instanceData, _cpuCullingData, instanceCount);
            _hasCpuCullingData = true;
        }

        private static void ResolveInstanceShape(
            in HectonVegetationInstanceData instanceData,
            out float instanceHeight,
            out float instanceWidth)
        {
            float instanceType = Mathf.Clamp(Mathf.Round(instanceData.Type), 0f, 2f);
            float encodedHeightScale = Mathf.Clamp01(Mathf.Abs(instanceData.HeightScale));
            float encodedWidthScale = instanceData.WidthScale < 0f ? 1f : Mathf.Clamp01(instanceData.WidthScale);
            if (instanceType < 0.5f)
            {
                instanceHeight = math.lerp(0.35f, 1.4f, encodedHeightScale);
                instanceWidth = math.lerp(0.65f, 1.25f, encodedWidthScale);
                return;
            }

            if (instanceType < 1.5f)
            {
                instanceHeight = math.lerp(10f, 20f, encodedHeightScale);
                instanceWidth = math.lerp(0.55f, 1.6f, encodedWidthScale);
                return;
            }

            instanceHeight = math.lerp(0.75f, 2.4f, encodedHeightScale);
            instanceWidth = math.lerp(0.75f, 1.35f, encodedWidthScale);
        }

        private static Vector3 TransformPoint(Matrix4x4 matrixValue, float x, float y, float z)
        {
            return matrixValue.MultiplyPoint3x4(new Vector3(x, y, z));
        }

        private bool IsVisibleInDarkness(Vector3 samplePositionWS)
        {
            if (!_enableDarknessCulling)
                return true;

            float globalBiolum = Mathf.Max(
                Shader.GetGlobalFloat(_GlobalBiolumIntensityId),
                Mathf.Max(
                    Shader.GetGlobalFloat(_FloorBiolumStrengthId),
                    Shader.GetGlobalFloat(_OceanBiolumStrengthId)));
            if (globalBiolum >= _darknessBiolumThreshold)
                return true;

            int headlightCount = CopyScooterHeadlightPayload();
            for (int headlightIndex = 0; headlightIndex < headlightCount; headlightIndex++)
            {
                Vector4 lightPosition = _scooterHeadlightPositionsWs[headlightIndex];
                float lightRange = Mathf.Max(0.1f, lightPosition.w);
                float3 toSample = new float3(
                    samplePositionWS.x - lightPosition.x,
                    samplePositionWS.y - lightPosition.y,
                    samplePositionWS.z - lightPosition.z);
                float sampleDistanceSq = math.lengthsq(toSample);
                float lightRangeSq = lightRange * lightRange;
                if (sampleDistanceSq >= lightRangeSq || sampleDistanceSq <= 0.00000001f)
                    continue;

                Vector4 directionData = _scooterHeadlightDirectionsWs[headlightIndex];
                float3 lightDirection = new float3(directionData.x, directionData.y, directionData.z);
                float lightDirectionLenSq = math.lengthsq(lightDirection);
                if (!math.isfinite(lightDirectionLenSq) || lightDirectionLenSq <= 0.00000001f)
                    continue;

                float outerCos = _scooterHeadlightConeData[headlightIndex].x;
                float dotLight = math.dot(lightDirection, toSample);
                if (!PassesDotThresholdSq(dotLight, outerCos, sampleDistanceSq * lightDirectionLenSq))
                    continue;

                float invRange = _scooterHeadlightConeData[headlightIndex].z;
                float rangeAttenuation = math.saturate(1f - sampleDistanceSq * invRange * invRange);
                rangeAttenuation *= rangeAttenuation;
                float intensity = _scooterHeadlightColors[headlightIndex].w * _scooterHeadlightConeData[headlightIndex].y;
                if (rangeAttenuation * intensity >= 0.02f)
                    return true;
            }

            return false;
        }

        private static bool PassesDotThresholdSq(float dotValue, float threshold, float lengthProductSq)
        {
            if (!math.isfinite(dotValue) || !math.isfinite(threshold) || !math.isfinite(lengthProductSq) || lengthProductSq <= 0.00000001f)
                return true;

            float thresholdSq = threshold * threshold;
            float dotSq = dotValue * dotValue;
            return threshold >= 0f
                ? dotValue >= 0f && dotSq >= thresholdSq * lengthProductSq
                : dotValue >= 0f || dotSq <= thresholdSq * lengthProductSq;
        }

        private JobHandle OnPerformCulling(
            BatchRendererGroup rendererGroup,
            BatchCullingContext cullingContext,
            BatchCullingOutput cullingOutput,
            IntPtr userContext)
        {
            Mesh nearMesh = ResolveNearRenderMesh();
            Mesh farMesh = FrameTimeWatchdog.IsDistantFloraRenderingEnabled && _farLodDistance > _nearLodDistance
                ? ResolveImpostorRenderMesh()
                : null;
            bool useFarPass = farMesh != null && !_farBatchMeshId.Equals(default) && !_farBatchMaterialId.Equals(default);
            bool useDepthPass = _enableDepthPrepass && _depthNearBrgMaterial != null && !_depthNearBatchMaterialId.Equals(default);
            bool useShadowPass = _enableShadowCasterDraw && _shadowBrgMaterial != null && !_shadowBatchMaterialId.Equals(default) && HasMainDirectionalShadowLight();
            bool useMotionPass = _enableMotionVectorDraw && _motionNearBrgMaterial != null && !_motionNearBatchMaterialId.Equals(default);

            if (_instanceCount <= 0 ||
                _batchId.Equals(default) ||
                nearMesh == null ||
                _nearBatchMeshId.Equals(default) ||
                _nearBatchMaterialId.Equals(default))
            {
                HectonBatchRendererGroupUtility.WriteDirectDrawOutput(
                    cullingOutput,
                    HectonBatchRendererGroupUtility.AllocateDirectDrawOutput(0, 0, 0));
                return default;
            }

            Bounds drawBounds = ResolveDrawBounds(transform.position);
            if (!HectonBatchRendererGroupUtility.IsBoundsVisible(cullingContext.cullingPlanes, drawBounds))
            {
                HectonBatchRendererGroupUtility.WriteDirectDrawOutput(
                    cullingOutput,
                    HectonBatchRendererGroupUtility.AllocateDirectDrawOutput(0, 0, 0));
                return default;
            }

            bool useDepthFarPass = useDepthPass && useFarPass && _depthFarBrgMaterial != null && !_depthFarBatchMaterialId.Equals(default);
            bool useMotionFarPass = useMotionPass && useFarPass && _motionFarBrgMaterial != null && !_motionFarBatchMaterialId.Equals(default);
            bool enableCpuCulling = _hasCpuCullingData &&
                                    _cpuCullingMatrices.IsCreated &&
                                    _cpuCullingData.IsCreated &&
                                    _cpuCullingMatrices.Length >= _instanceCount &&
                                    _cpuCullingData.Length >= _instanceCount;
            float brgLodDistanceScalar = VRAMPressureMonitor.BrgLodDistanceScalar;
            float lodTransition = Mathf.Max(_lodTransitionRange * brgLodDistanceScalar, 0.01f);
            float nearLodDistance = Mathf.Max(_nearLodDistance * brgLodDistanceScalar, 0.01f);
            float farLodDistance = Mathf.Max(nearLodDistance, _farLodDistance * brgLodDistanceScalar);
            float lod0MaxDistance = nearLodDistance + lodTransition;
            float lod1MinDistance = Mathf.Max(0f, nearLodDistance - lodTransition);
            float lod1MaxDistance = farLodDistance + lodTransition;
            Vector4 floatingOffset = ResolveVegetationFloatingOffset();

            if (!enableCpuCulling)
            {
                WriteAllVisibleVegetationOutput(
                    cullingOutput,
                    useFarPass,
                    useDepthPass,
                    useDepthFarPass,
                    useShadowPass,
                    useMotionPass,
                    useMotionFarPass);
                return default;
            }

            NativeArray<byte> visibilityMask = new NativeArray<byte>(_instanceCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<float4> cullingPlanes = default;
            NativeArray<float4> headlightPositionsWs = default;
            NativeArray<float4> headlightDirectionsWs = default;
            NativeArray<float4> headlightColors = default;
            NativeArray<float4> headlightConeData = default;
            bool bypassDarknessCulling = !_enableDarknessCulling;
            int headlightCount = 0;

            if (enableCpuCulling)
            {
                int planeCount = cullingContext.cullingPlanes.IsCreated ? cullingContext.cullingPlanes.Length : 0;
                if (planeCount > 0)
                {
                    cullingPlanes = new NativeArray<float4>(planeCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                    for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
                    {
                        Plane plane = cullingContext.cullingPlanes[planeIndex];
                        cullingPlanes[planeIndex] = new float4(plane.normal.x, plane.normal.y, plane.normal.z, plane.distance);
                    }
                }

                if (_enableDarknessCulling)
                {
                    float globalBiolum = Mathf.Max(
                        Shader.GetGlobalFloat(_GlobalBiolumIntensityId),
                        Mathf.Max(
                            Shader.GetGlobalFloat(_FloorBiolumStrengthId),
                            Shader.GetGlobalFloat(_OceanBiolumStrengthId)));
                    if (globalBiolum >= _darknessBiolumThreshold)
                    {
                        bypassDarknessCulling = true;
                    }
                    else
                    {
                        bypassDarknessCulling = false;
                        headlightCount = CopyScooterHeadlightPayload();
                        if (headlightCount > 0)
                        {
                            headlightPositionsWs = new NativeArray<float4>(MaxScooterHeadlights, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                            headlightDirectionsWs = new NativeArray<float4>(MaxScooterHeadlights, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                            headlightColors = new NativeArray<float4>(MaxScooterHeadlights, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                            headlightConeData = new NativeArray<float4>(MaxScooterHeadlights, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                            for (int headlightIndex = 0; headlightIndex < MaxScooterHeadlights; headlightIndex++)
                            {
                                Vector4 lightPosition = _scooterHeadlightPositionsWs[headlightIndex];
                                Vector4 lightDirection = _scooterHeadlightDirectionsWs[headlightIndex];
                                Vector4 lightColor = _scooterHeadlightColors[headlightIndex];
                                Vector4 coneData = _scooterHeadlightConeData[headlightIndex];
                                headlightPositionsWs[headlightIndex] = new float4(lightPosition.x, lightPosition.y, lightPosition.z, lightPosition.w);
                                headlightDirectionsWs[headlightIndex] = new float4(lightDirection.x, lightDirection.y, lightDirection.z, lightDirection.w);
                                headlightColors[headlightIndex] = new float4(lightColor.x, lightColor.y, lightColor.z, lightColor.w);
                                headlightConeData[headlightIndex] = new float4(coneData.x, coneData.y, coneData.z, coneData.w);
                            }
                        }
                    }
                }
            }

            unsafe
            {
                int visibleInstanceCapacity = CalculateVegetationVisibleInstanceCapacity(
                    _instanceCount,
                    useFarPass,
                    useShadowPass);
                int drawCommandCapacity = CalculateVegetationDrawCommandCapacity(
                    useFarPass,
                    useDepthPass,
                    useDepthFarPass,
                    useShadowPass,
                    useMotionPass,
                    useMotionFarPass);
                FrameTimeWatchdog.ReportBatchRendererGroupBatchCount(drawCommandCapacity);
                BatchCullingOutputDrawCommands output = HectonBatchRendererGroupUtility.AllocateDirectDrawOutput(
                    visibleInstanceCapacity,
                    drawCommandCapacity,
                    drawCommandCapacity);

                JobHandle visibilityHandle = new BuildVegetationVisibilityMaskJob
                {
                    Matrices = _cpuCullingMatrices,
                    InstanceData = _cpuCullingData,
                    CullingPlanes = cullingPlanes,
                    HeadlightPositionsWs = headlightPositionsWs,
                    HeadlightDirectionsWs = headlightDirectionsWs,
                    HeadlightColors = headlightColors,
                    HeadlightConeData = headlightConeData,
                    VisibilityMask = visibilityMask,
                    InstanceCount = _instanceCount,
                    CullingPlaneCount = cullingPlanes.IsCreated ? cullingPlanes.Length : 0,
                    HeadlightCount = headlightCount,
                    EnableCpuCulling = enableCpuCulling,
                    UseFarPass = useFarPass,
                    UseShadowPass = useShadowPass,
                    BypassDarknessCulling = bypassDarknessCulling,
                    ViewPosition = _cachedCullCameraPosition,
                    GlobalOffset = new float3(floatingOffset.x, floatingOffset.y, floatingOffset.z),
                    Lod0MaxDistanceSq = lod0MaxDistance * lod0MaxDistance,
                    Lod1MinDistanceSq = lod1MinDistance * lod1MinDistance,
                    Lod1MaxDistanceSq = lod1MaxDistance * lod1MaxDistance
                }.Schedule(_instanceCount, 64);

                JobHandle finalizeHandle = new FinalizeVegetationDrawOutputJob
                {
                    VisibilityMask = visibilityMask,
                    InstanceCount = _instanceCount,
                    Layer = gameObject.layer,
                    SubMeshIndex = _subMeshIndex,
                    UseFarPass = useFarPass,
                    UseDepthPass = useDepthPass,
                    UseDepthFarPass = useDepthFarPass,
                    UseShadowPass = useShadowPass,
                    UseMotionPass = useMotionPass,
                    UseMotionFarPass = useMotionFarPass,
                    BatchId = _batchId,
                    NearMeshId = _nearBatchMeshId,
                    FarMeshId = _farBatchMeshId,
                    NearMaterialId = _nearBatchMaterialId,
                    FarMaterialId = _farBatchMaterialId,
                    DepthNearMaterialId = _depthNearBatchMaterialId,
                    DepthFarMaterialId = _depthFarBatchMaterialId,
                    ShadowMaterialId = _shadowBatchMaterialId,
                    MotionNearMaterialId = _motionNearBatchMaterialId,
                    MotionFarMaterialId = _motionFarBatchMaterialId,
                    VisibleInstances = output.visibleInstances,
                    DrawCommands = output.drawCommands,
                    DrawRanges = output.drawRanges,
                    OutputCommands = (BatchCullingOutputDrawCommands*)NativeArrayUnsafeUtility.GetUnsafePtr(cullingOutput.drawCommands)
                }.Schedule(visibilityHandle);

                JobHandle disposeHandle = visibilityMask.Dispose(finalizeHandle);
                if (cullingPlanes.IsCreated)
                    disposeHandle = cullingPlanes.Dispose(disposeHandle);
                if (headlightPositionsWs.IsCreated)
                    disposeHandle = headlightPositionsWs.Dispose(disposeHandle);
                if (headlightDirectionsWs.IsCreated)
                    disposeHandle = headlightDirectionsWs.Dispose(disposeHandle);
                if (headlightColors.IsCreated)
                    disposeHandle = headlightColors.Dispose(disposeHandle);
                if (headlightConeData.IsCreated)
                    disposeHandle = headlightConeData.Dispose(disposeHandle);
                return disposeHandle;
            }
        }

        private unsafe void WriteAllVisibleVegetationOutput(
            BatchCullingOutput cullingOutput,
            bool useFarPass,
            bool useDepthPass,
            bool useDepthFarPass,
            bool useShadowPass,
            bool useMotionPass,
            bool useMotionFarPass)
        {
            int nearOffset = 0;
            int farOffset = _instanceCount;
            int shadowOffset = _instanceCount + (useFarPass ? _instanceCount : 0);
            int visibleInstanceCount = CalculateVegetationVisibleInstanceCapacity(
                _instanceCount,
                useFarPass,
                useShadowPass);
            int drawCommandCapacity = CalculateVegetationDrawCommandCapacity(
                useFarPass,
                useDepthPass,
                useDepthFarPass,
                useShadowPass,
                useMotionPass,
                useMotionFarPass);

            BatchCullingOutputDrawCommands output = HectonBatchRendererGroupUtility.AllocateDirectDrawOutput(
                visibleInstanceCount,
                drawCommandCapacity,
                drawCommandCapacity);

            for (int instanceIndex = 0; instanceIndex < _instanceCount; instanceIndex++)
                output.visibleInstances[nearOffset + instanceIndex] = instanceIndex;

            if (useFarPass)
            {
                for (int instanceIndex = 0; instanceIndex < _instanceCount; instanceIndex++)
                    output.visibleInstances[farOffset + instanceIndex] = instanceIndex;
            }

            if (useShadowPass)
            {
                for (int instanceIndex = 0; instanceIndex < _instanceCount; instanceIndex++)
                    output.visibleInstances[shadowOffset + instanceIndex] = instanceIndex;
            }

            int commandIndex = 0;
            commandIndex = WriteVegetationDrawCommand(
                output,
                commandIndex,
                nearOffset,
                _instanceCount,
                _nearBatchMaterialId,
                _nearBatchMeshId,
                shadowCasting: false,
                receiveShadows: false,
                MotionVectorGenerationMode.Camera);

            if (useFarPass)
            {
                commandIndex = WriteVegetationDrawCommand(
                    output,
                    commandIndex,
                    farOffset,
                    _instanceCount,
                    _farBatchMaterialId,
                    _farBatchMeshId,
                    shadowCasting: false,
                    receiveShadows: false,
                    MotionVectorGenerationMode.Camera);
            }

            if (useDepthPass)
            {
                commandIndex = WriteVegetationDrawCommand(
                    output,
                    commandIndex,
                    nearOffset,
                    _instanceCount,
                    _depthNearBatchMaterialId,
                    _nearBatchMeshId,
                    shadowCasting: false,
                    receiveShadows: false,
                    MotionVectorGenerationMode.Camera);

                if (useDepthFarPass)
                {
                    commandIndex = WriteVegetationDrawCommand(
                        output,
                        commandIndex,
                        farOffset,
                        _instanceCount,
                        _depthFarBatchMaterialId,
                        _farBatchMeshId,
                        shadowCasting: false,
                        receiveShadows: false,
                        MotionVectorGenerationMode.Camera);
                }
            }

            if (useShadowPass)
            {
                commandIndex = WriteVegetationDrawCommand(
                    output,
                    commandIndex,
                    shadowOffset,
                    _instanceCount,
                    _shadowBatchMaterialId,
                    _nearBatchMeshId,
                    shadowCasting: true,
                    receiveShadows: false,
                    MotionVectorGenerationMode.Camera);
            }

            if (useMotionPass)
            {
                commandIndex = WriteVegetationDrawCommand(
                    output,
                    commandIndex,
                    nearOffset,
                    _instanceCount,
                    _motionNearBatchMaterialId,
                    _nearBatchMeshId,
                    shadowCasting: false,
                    receiveShadows: false,
                    MotionVectorGenerationMode.Object);

                if (useMotionFarPass)
                {
                    commandIndex = WriteVegetationDrawCommand(
                        output,
                        commandIndex,
                        farOffset,
                        _instanceCount,
                        _motionFarBatchMaterialId,
                        _farBatchMeshId,
                        shadowCasting: false,
                        receiveShadows: false,
                        MotionVectorGenerationMode.Object);
                }
            }

            output.visibleInstanceCount = visibleInstanceCount;
            output.drawCommandCount = commandIndex;
            output.drawRangeCount = commandIndex;
            FrameTimeWatchdog.ReportBatchRendererGroupBatchCount(commandIndex);
            HectonBatchRendererGroupUtility.WriteDirectDrawOutput(cullingOutput, output);
        }

        private static int CalculateVegetationVisibleInstanceCapacity(
            int instanceCount,
            bool useFarPass,
            bool useShadowPass)
        {
            int visibleInstanceCount = instanceCount;
            if (useFarPass)
                visibleInstanceCount += instanceCount;
            if (useShadowPass)
                visibleInstanceCount += instanceCount;

            return visibleInstanceCount;
        }

        private static int CalculateVegetationDrawCommandCapacity(
            bool useFarPass,
            bool useDepthPass,
            bool useDepthFarPass,
            bool useShadowPass,
            bool useMotionPass,
            bool useMotionFarPass)
        {
            int drawCommandCapacity = 1;
            if (useFarPass)
                drawCommandCapacity++;
            if (useDepthPass)
            {
                drawCommandCapacity++;
                if (useDepthFarPass)
                    drawCommandCapacity++;
            }
            if (useShadowPass)
                drawCommandCapacity++;
            if (useMotionPass)
            {
                drawCommandCapacity++;
                if (useMotionFarPass)
                    drawCommandCapacity++;
            }

            return drawCommandCapacity;
        }

        private unsafe int WriteVegetationDrawCommand(
            BatchCullingOutputDrawCommands output,
            int commandIndex,
            int visibleOffset,
            int visibleCount,
            BatchMaterialID materialId,
            BatchMeshID meshId,
            bool shadowCasting,
            bool receiveShadows,
            MotionVectorGenerationMode motionMode)
        {
            if (visibleCount <= 0 || materialId.Equals(default) || meshId.Equals(default))
                return commandIndex;

            output.drawCommands[commandIndex] = new BatchDrawCommand
            {
                flags = BatchDrawCommandFlags.None,
                visibleOffset = (uint)visibleOffset,
                visibleCount = (uint)visibleCount,
                batchID = _batchId,
                materialID = materialId,
                splitVisibilityMask = ushort.MaxValue,
                lightmapIndex = ushort.MaxValue,
                sortingPosition = 0,
                meshID = meshId,
                submeshIndex = (ushort)Mathf.Max(0, _subMeshIndex)
            };
            output.drawRanges[commandIndex] = HectonBatchRendererGroupUtility.CreateDirectDrawRange(
                (uint)commandIndex,
                gameObject.layer,
                shadowCasting ? ShadowCastingMode.On : ShadowCastingMode.Off,
                receiveShadows,
                motionMode);
            return commandIndex + 1;
        }

        private void ReleaseBatchRendererGroupResources()
        {
            if (_batchRendererGroup != null)
            {
                if (!_batchId.Equals(default))
                    _batchRendererGroup.RemoveBatch(_batchId);

                UnregisterBatchMesh(ref _nearBatchMeshId);
                UnregisterBatchMesh(ref _farBatchMeshId);
                UnregisterBatchMaterial(ref _nearBatchMaterialId);
                UnregisterBatchMaterial(ref _farBatchMaterialId);
                UnregisterBatchMaterial(ref _depthNearBatchMaterialId);
                UnregisterBatchMaterial(ref _depthFarBatchMaterialId);
                UnregisterBatchMaterial(ref _shadowBatchMaterialId);
                UnregisterBatchMaterial(ref _motionNearBatchMaterialId);
                UnregisterBatchMaterial(ref _motionFarBatchMaterialId);
                _batchRendererGroup.Dispose();
                _batchRendererGroup = null;
            }

            _batchId = default;
            _registeredBatchBuffer = null;
            _registeredNearMesh = null;
            _registeredFarMesh = null;
            _registeredNearBrgMaterial = null;
            _registeredFarBrgMaterial = null;
            _registeredDepthNearBrgMaterial = null;
            _registeredDepthFarBrgMaterial = null;
            _registeredShadowBrgMaterial = null;
            _registeredMotionNearBrgMaterial = null;
            _registeredMotionFarBrgMaterial = null;

            if (_batchHandleBuffer != null)
            {
                _batchHandleBuffer.Release();
                _batchHandleBuffer = null;
            }

            if (_batchMetadata.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_batchMetadata);
                _batchMetadata.Dispose();
            }

            ReleaseMaterialClone(ref _nearBrgMaterial);
            ReleaseMaterialClone(ref _farBrgMaterial);
            ReleaseMaterialClone(ref _depthNearBrgMaterial);
            ReleaseMaterialClone(ref _depthFarBrgMaterial);
            ReleaseMaterialClone(ref _shadowBrgMaterial);
            ReleaseMaterialClone(ref _motionNearBrgMaterial);
            ReleaseMaterialClone(ref _motionFarBrgMaterial);
        }

        private void UnregisterBatchMesh(ref BatchMeshID batchMeshId)
        {
            if (_batchRendererGroup != null && !batchMeshId.Equals(default))
                _batchRendererGroup.UnregisterMesh(batchMeshId);

            batchMeshId = default;
        }

        private void UnregisterBatchMaterial(ref BatchMaterialID batchMaterialId)
        {
            if (_batchRendererGroup != null && !batchMaterialId.Equals(default))
                _batchRendererGroup.UnregisterMaterial(batchMaterialId);

            batchMaterialId = default;
        }

        private void SyncSourceBinding()
        {
            if (_bufferSource == null)
                return;

            if (_bufferSource is IHectonIndirectVegetationNativeBufferSource nativeBufferSource)
            {
                if (!nativeBufferSource.TryAcquireNativeReadBuffer(out HectonIndirectVegetationNativeReadBuffer readBuffer) ||
                    !readBuffer.IsValid)
                {
                    ClearBoundInstanceState();
                    if (_bufferSource.HasExplicitBounds)
                        SetDrawBounds(_bufferSource.DrawBounds);
                    else
                        ClearDrawBoundsOverride();
                    return;
                }

                JobHandle producerHandle = readBuffer.ProducerHandle;
                if (!producerHandle.Equals(default) && !producerHandle.IsCompleted)
                {
                    nativeBufferSource.ReleaseNativeReadBuffer(readBuffer, default);
                    return;
                }

                bool uploadSucceeded = BindInstanceNativeArrays(
                    readBuffer.InstanceMatrices,
                    readBuffer.InstanceData,
                    readBuffer.InstanceCount);

                nativeBufferSource.ReleaseNativeReadBuffer(readBuffer, default);

                if (!uploadSucceeded)
                {
                    ClearBoundInstanceState();
                    if (readBuffer.HasExplicitBounds)
                        SetDrawBounds(readBuffer.DrawBounds);
                    else
                        ClearDrawBoundsOverride();
                    return;
                }

                if (readBuffer.HasExplicitBounds)
                    SetDrawBounds(readBuffer.DrawBounds);
                else
                    ClearDrawBoundsOverride();

                return;
            }

            GraphicsBuffer sourceMatrixBuffer = _bufferSource.InstanceMatrixBuffer;
            GraphicsBuffer sourceDataBuffer = _bufferSource.InstanceDataBuffer;
            int sourceInstanceCount = _bufferSource.InstanceCount;

            if (sourceMatrixBuffer == null || sourceInstanceCount <= 0 || sourceMatrixBuffer.count <= 0)
            {
                ClearBoundInstanceState();
                if (_bufferSource.HasExplicitBounds)
                    SetDrawBounds(_bufferSource.DrawBounds);
                else
                    ClearDrawBoundsOverride();
                return;
            }

            if (_instanceMatrixBuffer != sourceMatrixBuffer)
            {
                InvalidateRenderStateForBufferIdentityChange(sourceMatrixBuffer, _instanceDataBuffer, _floraPhaseSeedBuffer);
                _instanceMatrixBuffer = sourceMatrixBuffer;
                _hasCpuCullingData = false;
            }

            if (_instanceDataBuffer != sourceDataBuffer)
            {
                InvalidateRenderStateForBufferIdentityChange(_instanceMatrixBuffer, sourceDataBuffer != null && sourceDataBuffer.count > 0 ? sourceDataBuffer : null, _floraPhaseSeedBuffer);
                _instanceDataBuffer = sourceDataBuffer != null && sourceDataBuffer.count > 0 ? sourceDataBuffer : null;
                _hasCpuCullingData = false;
            }

            SetInstanceCount(sourceInstanceCount);

            if (_bufferSource.HasExplicitBounds)
                SetDrawBounds(_bufferSource.DrawBounds);
            else
                ClearDrawBoundsOverride();
        }

        private void ClearBoundInstanceState()
        {
            InvalidateRenderStateForBufferIdentityChange(null, null, null);
            _instanceMatrixBuffer = null;
            _instanceDataBuffer = null;
            _floraPhaseSeedBuffer = null;
            _instanceCount = 0;
            _legacyDataDirty = true;
            _hasCpuCullingData = false;
        }

        private void InvalidateRenderStateForBufferIdentityChange(
            GraphicsBuffer nextMatrixBuffer,
            GraphicsBuffer nextDataBuffer,
            GraphicsBuffer nextPhaseSeedBuffer)
        {
            if (_instanceMatrixBuffer == nextMatrixBuffer &&
                _instanceDataBuffer == nextDataBuffer &&
                _floraPhaseSeedBuffer == nextPhaseSeedBuffer)
            {
                return;
            }

            bool hadActiveBinding = _instanceMatrixBuffer != null ||
                                    _instanceDataBuffer != null ||
                                    _floraPhaseSeedBuffer != null ||
                                    _batchRendererGroup != null;
            if (!hadActiveBinding)
                return;

            _hasPreviousMotionCameraPosition = false;
            _previousMotionCamera = null;
            ReleaseBatchRendererGroupResources();
            ReleaseGpuIndirectResources();
        }

        private int CopyScooterHeadlightPayload()
        {
            ClearScooterHeadlightPayload();

            if (!_enableDarknessCulling)
                return 0;

            if (_playerToolManager == null)
                ResolvePlayerToolManager();

            if (_playerToolManager == null || _playerToolManager.IsSwapping)
                return 0;

            if (!(_playerToolManager.CurrentTool is MantaScooter scooter) || !scooter.IsTransportActive)
                return 0;

            return scooter.CopyHeadlightPayloadNonAlloc(
                _scooterHeadlightPositionsWs,
                _scooterHeadlightDirectionsWs,
                _scooterHeadlightColors,
                _scooterHeadlightConeData);
        }

        private void ResolvePlayerToolManager()
        {
            if (_playerToolManager != null)
                return;

            float currentTime = Time.unscaledTime;
            if (currentTime < _nextToolManagerResolveTime)
                return;

            _nextToolManagerResolveTime = currentTime + 2f;
            if (!BootstrapState.TryGetCurrentPlayerTransform(out Transform playerTransform) || playerTransform == null)
                return;

            IPlayerRuntimeContext playerContext = Hecton8.Core.GlobalRegistry.Player;
            if (playerContext != null && playerContext.ToolManager != null)
            {
                _playerToolManager = playerContext.ToolManager;
                return;
            }

            playerTransform.TryGetComponent(out _playerToolManager);
        }

        private void ClearScooterHeadlightPayload()
        {
            if (_scooterHeadlightPositionsWs == null ||
                _scooterHeadlightDirectionsWs == null ||
                _scooterHeadlightColors == null ||
                _scooterHeadlightConeData == null)
            {
                return;
            }

            for (int headlightIndex = 0; headlightIndex < MaxScooterHeadlights; headlightIndex++)
            {
                _scooterHeadlightPositionsWs[headlightIndex] = Vector4.zero;
                _scooterHeadlightDirectionsWs[headlightIndex] = Vector4.zero;
                _scooterHeadlightColors[headlightIndex] = Vector4.zero;
                _scooterHeadlightConeData[headlightIndex] = Vector4.zero;
            }
        }

        private static Vector4 ResolveVegetationFloatingOffset()
        {
            Vector3 totalOffset = HectonMapMagicVegetationBridge.GlobalTotalUniverseOffset;
            return new Vector4(totalOffset.x, totalOffset.y, totalOffset.z, 0f);
        }

        private GraphicsBuffer ResolveActiveInstanceDataBuffer()
        {
            if (_instanceDataBuffer != null)
                return _instanceDataBuffer;

            if (_instanceCount <= 0)
                return null;

            EnsureLegacyInstanceDataCapacity(_instanceCount);
            if (_legacyInstanceDataBuffer == null || _legacyInstanceData == null)
                return null;

            if (_legacyDataDirty)
            {
                FillLegacyInstanceData(_instanceCount);
                GraphicsBufferUploadUtility.UploadArray(_legacyInstanceDataBuffer, _legacyInstanceData, _instanceCount);
                _legacyDataDirty = false;
            }

            return _legacyInstanceDataBuffer;
        }

        private void EnsureLegacyInstanceDataCapacity(int instanceCount)
        {
            if (instanceCount <= 0)
                return;

            if (_legacyInstanceData != null &&
                _legacyInstanceData.Length >= instanceCount &&
                _legacyInstanceDataBuffer != null &&
                _legacyInstanceDataBuffer.count >= instanceCount)
            {
                return;
            }

            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(16, instanceCount));

            if (_legacyInstanceDataBuffer != null && _instanceDataBuffer == null && _instanceCount > 0)
                InvalidateRenderStateForBufferIdentityChange(_instanceMatrixBuffer, null, _floraPhaseSeedBuffer);

            ReleaseLegacyInstanceDataBuffer();

            // COLD ALLOC: HectonVegetationInstanceData[nextCapacity] - legacy metadata fallback staging - owner: HectonIndirectVegetationRenderer
            _legacyInstanceData = new HectonVegetationInstanceData[nextCapacity];
            // COLD ALLOC: GraphicsBuffer[nextCapacity] - legacy instance metadata fallback buffer - owner: HectonIndirectVegetationRenderer
            _legacyInstanceDataBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<HectonVegetationInstanceData>(nextCapacity);
            _legacyDataDirty = true;
        }

        private void EnsureUploadedInstanceBufferCapacity(int instanceCount, bool requiresInstanceDataBuffer)
        {
            if (instanceCount <= 0)
                return;

            if (_uploadedInstanceMatrixBuffer == null || _uploadedInstanceMatrixBuffer.count < instanceCount)
            {
                if (_uploadedInstanceMatrixBuffer != null && _instanceMatrixBuffer == _uploadedInstanceMatrixBuffer)
                    InvalidateRenderStateForBufferIdentityChange(null, _instanceDataBuffer == _uploadedInstanceDataBuffer ? null : _instanceDataBuffer, _floraPhaseSeedBuffer);

                if (_uploadedInstanceMatrixBuffer != null)
                {
                    _uploadedInstanceMatrixBuffer.Release();
                    _uploadedInstanceMatrixBuffer = null;
                }

                int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(16, instanceCount));
                // COLD ALLOC: GraphicsBuffer[nextCapacity] - owned matrix upload staging buffer - owner: HectonIndirectVegetationRenderer
                _uploadedInstanceMatrixBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Matrix4x4>(nextCapacity);
            }

            if (!requiresInstanceDataBuffer)
                return;

            if (_uploadedInstanceDataBuffer == null || _uploadedInstanceDataBuffer.count < instanceCount)
            {
                if (_uploadedInstanceDataBuffer != null && _instanceDataBuffer == _uploadedInstanceDataBuffer)
                    InvalidateRenderStateForBufferIdentityChange(_instanceMatrixBuffer == _uploadedInstanceMatrixBuffer ? null : _instanceMatrixBuffer, null, _floraPhaseSeedBuffer);

                if (_uploadedInstanceDataBuffer != null)
                {
                    _uploadedInstanceDataBuffer.Release();
                    _uploadedInstanceDataBuffer = null;
                }

                int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(16, instanceCount));
                // COLD ALLOC: GraphicsBuffer[nextCapacity] - owned metadata upload staging buffer - owner: HectonIndirectVegetationRenderer
                _uploadedInstanceDataBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<HectonVegetationInstanceData>(nextCapacity);
            }
        }

        private void FillLegacyInstanceData(int instanceCount)
        {
            HectonVegetationInstanceData defaultPayload = CreateLegacyDefaultPayload();
            for (int i = 0; i < instanceCount; i++)
                _legacyInstanceData[i] = defaultPayload;
        }

        private HectonVegetationInstanceData CreateLegacyDefaultPayload()
        {
            switch (_legacyFallbackType)
            {
                case HectonVegetationInstanceType.GiantKelp:
                    return new HectonVegetationInstanceData(HectonVegetationInstanceType.GiantKelp, 0.55f, 0.8f, 0.5f, -1f, HectonVegetationInstanceData.RuntimeStateIdle, 0f, 0.55f, new Vector4(0.11f, 0.52f, 0.47f, 0.42f), 0.62f, 1.18f, 1f, 0f);
                case HectonVegetationInstanceType.Sargassum:
                    return new HectonVegetationInstanceData(HectonVegetationInstanceType.Sargassum, 0.4f, 0.9f, 0.5f, -1f, HectonVegetationInstanceData.RuntimeStateIdle, 0f, 0.45f, new Vector4(0.08f, 0.42f, 0.38f, 0.26f), 0.78f, 0.94f, 1f, 0f);
                default:
                    return new HectonVegetationInstanceData(HectonVegetationInstanceType.Grass, 0.55f, 1f, 0.5f, -1f, HectonVegetationInstanceData.RuntimeStateIdle, 0f, 0.35f, new Vector4(0.10f, 0.48f, 0.34f, 0.22f), 1.35f, 0.72f, 1f, 0f);
            }
        }

        private void ReleaseLegacyInstanceDataBuffer()
        {
            if (_legacyInstanceDataBuffer != null)
            {
                _legacyInstanceDataBuffer.Release();
                _legacyInstanceDataBuffer = null;
            }

            _legacyInstanceData = null;
        }

        private void ReleaseUploadedInstanceBuffers()
        {
            if (_uploadedInstanceMatrixBuffer != null)
            {
                _uploadedInstanceMatrixBuffer.Release();
                _uploadedInstanceMatrixBuffer = null;
            }

            if (_uploadedInstanceDataBuffer != null)
            {
                _uploadedInstanceDataBuffer.Release();
                _uploadedInstanceDataBuffer = null;
            }
        }

        private Camera ResolveCullCamera()
        {
            if (_cameraOverride != null && _cameraOverride.isActiveAndEnabled)
            {
                _cachedCullCamera = _cameraOverride;
                return _cachedCullCamera;
            }

            if (_cachedCullCamera != null && _cachedCullCamera.isActiveAndEnabled)
                return _cachedCullCamera;

            int cameraCount = Mathf.Min(Camera.allCamerasCount, _cameraSearchCache.Length);
            if (cameraCount <= 0)
                return null;

            Camera.GetAllCameras(_cameraSearchCache);

            Camera fallbackCamera = null;
            for (int i = 0; i < cameraCount; i++)
            {
                Camera candidate = _cameraSearchCache[i];
                if (candidate == null || !candidate.isActiveAndEnabled)
                    continue;

                if (fallbackCamera == null)
                    fallbackCamera = candidate;

                if (candidate.CompareTag("MainCamera"))
                {
                    _cachedCullCamera = candidate;
                    return _cachedCullCamera;
                }
            }

            _cachedCullCamera = fallbackCamera;
            return _cachedCullCamera;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            TryAutoAssignAssets();
        }

        private void TryAutoAssignAssets()
        {
            if (_vegetationShader == null)
                _vegetationShader = AssetDatabase.LoadAssetAtPath<Shader>(VegetationShaderAssetPath);

            if (_cullingCompute == null)
                _cullingCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputeShaderAssetPath);

            if (_abyssalFlowFieldCompute == null)
                _abyssalFlowFieldCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(AbyssalFlowFieldComputeAssetPath);

            if (_depthPyramidCompute == null)
                _depthPyramidCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(DepthPyramidComputeAssetPath);

            if (_depthOnlyShader == null)
                _depthOnlyShader = AssetDatabase.LoadAssetAtPath<Shader>(DepthShaderAssetPath);

            if (_shadowCasterShader == null)
                _shadowCasterShader = AssetDatabase.LoadAssetAtPath<Shader>(ShadowShaderAssetPath);

            if (_motionVectorShader == null)
                _motionVectorShader = AssetDatabase.LoadAssetAtPath<Shader>(MotionShaderAssetPath);

            _cullFloraKernel = _cullingCompute != null ? _cullingCompute.FindKernel("CullFloraInstances") : -1;
            _cullFloraShadowKernel = _cullingCompute != null ? _cullingCompute.FindKernel("CullFloraShadowInstances") : -1;
            _clearIndirectArgsKernel = _cullingCompute != null ? _cullingCompute.FindKernel("ClearIndirectArgs") : -1;
            _clearFloraSnapFlagsKernel = _abyssalFlowFieldCompute != null ? _abyssalFlowFieldCompute.FindKernel("ClearFloraSnapFlags") : -1;
            _flagSnappedFloraKernel = _abyssalFlowFieldCompute != null ? _abyssalFlowFieldCompute.FindKernel("FlagSnappedFlora") : -1;
            _depthPyramidCopyKernel = _depthPyramidCompute != null ? _depthPyramidCompute.FindKernel("CopyDepthPyramidMip0") : -1;
            _depthPyramidDownsampleKernel = _depthPyramidCompute != null ? _depthPyramidCompute.FindKernel("DownsampleDepthPyramidMip") : -1;
        }
#endif

        private void EnsureRuntimeMaterial()
        {
            if (_runtimeMaterial != null)
                return;

            Shader vegetationShader = _vegetationShader != null
                ? _vegetationShader
                : (_material != null ? _material.shader : null);
            if (vegetationShader == null)
                return;

            _runtimeMaterial = new Material(vegetationShader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "__HectonIndirectVegetationRuntimeMaterial",
                enableInstancing = true
            }; // COLD ALLOC: Material[1] - hidden fallback vegetation material when the shared authoring material is missing - owner: HectonIndirectVegetationRenderer
            _ownsRuntimeMaterial = true;
        }

        private void CreateAuxiliaryMaterials()
        {
            if (_enableDepthPrepass && _depthOnlyMaterial == null && _depthOnlyShader != null)
            {
                _depthOnlyMaterial = new Material(_depthOnlyShader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                }; // COLD ALLOC: Material[1] - dedicated depth-only indirect vegetation material - owner: HectonIndirectVegetationRenderer
            }

            if (_enableShadowCasterDraw && _shadowCasterMaterial == null && _shadowCasterShader != null)
            {
                _shadowCasterMaterial = new Material(_shadowCasterShader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                }; // COLD ALLOC: Material[1] - dedicated shadow-only indirect vegetation material - owner: HectonIndirectVegetationRenderer
            }

            if (_enableMotionVectorDraw && _motionVectorMaterial == null && _motionVectorShader != null)
            {
                _motionVectorMaterial = new Material(_motionVectorShader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                }; // COLD ALLOC: Material[1] - dedicated motion-vector indirect vegetation material - owner: HectonIndirectVegetationRenderer
            }
        }

        private void ReleaseRuntimeMaterial()
        {
            if (!_ownsRuntimeMaterial || _runtimeMaterial == null)
                return;

            if (Application.isPlaying)
                Destroy(_runtimeMaterial);
            else
                DestroyImmediate(_runtimeMaterial);

            _runtimeMaterial = null;
            _ownsRuntimeMaterial = false;
        }

        private void ReleaseAuxiliaryMaterials()
        {
            if (_depthOnlyMaterial != null)
            {
                Destroy(_depthOnlyMaterial);
                _depthOnlyMaterial = null;
            }

            if (_shadowCasterMaterial != null)
            {
                Destroy(_shadowCasterMaterial);
                _shadowCasterMaterial = null;
            }

            if (_motionVectorMaterial != null)
            {
                Destroy(_motionVectorMaterial);
                _motionVectorMaterial = null;
            }
        }

        private static bool HasMainDirectionalShadowLight()
        {
            Light sun = RenderSettings.sun;
            return sun != null && sun.enabled && sun.type == LightType.Directional && sun.shadows != LightShadows.None;
        }

        private static long EstimateGraphicsBufferBytes(GraphicsBuffer buffer)
        {
            return buffer != null ? (long)buffer.count * buffer.stride : 0L;
        }

        private static Mesh BuildImpostorCardMesh()
        {
            Mesh mesh = new Mesh
            {
                name = $"{nameof(HectonIndirectVegetationRenderer)}_ImpostorCard"
            };

            // COLD ALLOC: Vector3[4] - unit impostor card vertices - owner: HectonIndirectVegetationRenderer
            Vector3[] vertices =
            {
                new Vector3(-0.5f, 0f, 0f),
                new Vector3(-0.5f, 1f, 0f),
                new Vector3(0.5f, 1f, 0f),
                new Vector3(0.5f, 0f, 0f)
            };
            // COLD ALLOC: Vector3[4] - unit impostor card normals - owner: HectonIndirectVegetationRenderer
            Vector3[] normals =
            {
                Vector3.forward,
                Vector3.forward,
                Vector3.forward,
                Vector3.forward
            };
            // COLD ALLOC: Vector4[4] - unit impostor card tangents - owner: HectonIndirectVegetationRenderer
            Vector4[] tangents =
            {
                new Vector4(1f, 0f, 0f, 1f),
                new Vector4(1f, 0f, 0f, 1f),
                new Vector4(1f, 0f, 0f, 1f),
                new Vector4(1f, 0f, 0f, 1f)
            };
            // COLD ALLOC: Vector2[4] - unit impostor card UVs - owner: HectonIndirectVegetationRenderer
            Vector2[] uvs =
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f)
            };
            // COLD ALLOC: int[6] - unit impostor card indices - owner: HectonIndirectVegetationRenderer
            int[] triangles = { 0, 1, 2, 0, 2, 3 };

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.bounds = new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 0.01f));
            return mesh;
        }

        private void TryRegister()
        {
            if (_isRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _isRegistered = GlobalRegistry.Updatables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_isRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _isRegistered = false;
        }
    }
}

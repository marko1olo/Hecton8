using Hecton8.Bootstrap;
using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Hecton8.Biolum
{
    /// <summary>
    /// Publishes a player-centered 3D bioluminescence radiance volume for flora shading.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HectonBiolumDiffusionVolume : MonoBehaviour, ITickable, IUpdatable, IOriginShiftListener
    {
        private const int DefaultResolution = 64;
        private const int ThreadGroupSize = 4;
        private const int MaxTrackedZones = 32;
        private static readonly int _VolumeOutputId = Shader.PropertyToID("_HectonBiolumVolumeOutput");
        private static readonly int _VolumeInputId = Shader.PropertyToID("_HectonBiolumVolumeInput");
        private static readonly int _PointBufferId = Shader.PropertyToID("_HectonBiolumPoints");
        private static readonly int _PointCountId = Shader.PropertyToID("_HectonBiolumPointCount");
        private static readonly int _HalfExtentsId = Shader.PropertyToID("_HectonBiolumVolumeHalfExtents");
        private static readonly int _WorldToLocalId = Shader.PropertyToID("_HectonBiolumVolumeWorldToLocal");
        private static readonly int _VolumeParamsId = Shader.PropertyToID("_HectonBiolumVolumeParams");
        private static readonly int _CascadeParamsId = Shader.PropertyToID("_HectonBiolumCascadeParams");
        private static readonly int _TexelSizeId = Shader.PropertyToID("_HectonBiolumVolumeTexelSize");
        private static readonly int _GlobalTextureId = Shader.PropertyToID("_HectonBiolumVolumeTex");
        private static readonly int _GlobalActiveId = Shader.PropertyToID("_HectonBiolumVolumeActive");

        private struct BiolumPointGpuData
        {
            public Vector4 PositionRange;
            public Vector4 ColorIntensity;
        }

        [Header("Compute")]
        [SerializeField]
        [Tooltip("Compute shader used to diffuse nearby biolum zones into a persistent 3D radiance volume.")]
        private ComputeShader biolumDiffusionCompute;

        [SerializeField, Range(32, 64)]
        [Tooltip("Resolution of the player-centered 3D radiance volume.")]
        private int volumeResolution = DefaultResolution;

        [SerializeField, Range(24f, 128f)]
        [Tooltip("World-space coverage size of the biolum diffusion volume around the player.")]
        private float volumeWorldSize = 72f;

        [SerializeField, Range(0.1f, 4f)]
        [Tooltip("Strength multiplier applied to injected zone radiance.")]
        private float injectionStrength = 1.2f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Diffusion blend applied per tick.")]
        private float diffusionStrength = 0.24f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Per-second volume decay applied before each reinjection pass.")]
        private float decayRate = 0.08f;

        [SerializeField, Range(0.05f, 0.95f)]
        [Tooltip("Radiance threshold above which a biolum voxel propagates a local cascade wave into adjacent voxels.")]
        private float cascadeSpikeThreshold = 0.32f;

        [SerializeField, Range(0f, 3f)]
        [Tooltip("Propagation gain applied when adjacent biolum voxels spike above the cascade threshold.")]
        private float cascadePropagationGain = 0.75f;

        [SerializeField, Range(0f, 12f)]
        [Tooltip("Wave speed used to phase-offset the biolum cascade through the player-centered volume.")]
        private float cascadeWaveSpeed = 4.4f;

        [SerializeField, Range(8f, 160f)]
        [Tooltip("Maximum radius used when gathering nearby biolum zone emitters.")]
        private float zoneGatherRadius = 88f;

        [Header("Diagnostics")]
        [SerializeField] private int _debugPointCount;
        [SerializeField] private Vector3 _debugVolumeCenter;

        private bool _registered;
        private bool _needsClear = true;
        private bool _hasLastVolumeCenter;
        private int _clearKernel = -1;
        private int _diffuseKernel = -1;
        private int _injectKernel = -1;
        private Transform _playerTransform;
        private HectonBiolumManager _biolumManager;
        private Vector3 _lastVolumeCenter;
        private RenderTexture _volumeA;
        private RenderTexture _volumeB;
        private GraphicsBuffer _pointBuffer;
        private readonly BiolumPointGpuData[] _pointUpload = new BiolumPointGpuData[MaxTrackedZones]; // COLD ALLOC: BiolumPointGpuData[32] — persistent GPU upload staging for biolum diffusion emitters — owner: HectonBiolumDiffusionVolume
        private readonly HectonBiolumZone[] _nearbyZones = new HectonBiolumZone[MaxTrackedZones]; // COLD ALLOC: HectonBiolumZone[32] — nearby biolum zone cache for diffusion volume injection — owner: HectonBiolumDiffusionVolume
        private readonly float[] _nearbyZoneWeights = new float[MaxTrackedZones]; // COLD ALLOC: float[32] — zone-weight scratch paired with nearby biolum zone cache — owner: HectonBiolumDiffusionVolume

        private void Awake()
        {
            ResolveDependencies();
            EnsureResources();
            PublishGlobals();
        }

        private void OnEnable()
        {
            HectonFloatingOrigin.RegisterListener(this);
            TryRegister();
            ResolveDependencies();
            EnsureResources();
            PublishGlobals();
        }

        private void OnDisable()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregister();
            ReleaseResources();
            Shader.SetGlobalFloat(_GlobalActiveId, 0f);
        }

        private void OnDestroy()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregister();
            ReleaseResources();
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (!isActiveAndEnabled || shiftData.ShiftOffset.sqrMagnitude <= 0.0001f)
                return;

            _needsClear = true;
            _hasLastVolumeCenter = false;
            _lastVolumeCenter = default;
            _debugPointCount = 0;
            Shader.SetGlobalFloat(_GlobalActiveId, 0f);
        }

        /// <summary>
        /// Updates the persistent 3D radiance volume from nearby biolum zone data.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f)
                return;

            ResolveDependencies();
            EnsureResources();
            if (_playerTransform == null || _biolumManager == null || _volumeA == null || _volumeB == null || _pointBuffer == null)
            {
                Shader.SetGlobalFloat(_GlobalActiveId, 0f);
                return;
            }

            Vector3 volumeCenter = _playerTransform.position;
            _debugVolumeCenter = volumeCenter;

            int pointCount = CollectNearbyPoints(volumeCenter);
            _debugPointCount = pointCount;

            float worldTexelSize = volumeWorldSize / math.max(1, volumeResolution);
            float centerDelta = _hasLastVolumeCenter ? Vector3.Distance(_lastVolumeCenter, volumeCenter) : 0f;
            if (_hasLastVolumeCenter && centerDelta >= volumeWorldSize * 0.5f)
                _needsClear = true;

            float motionDecayBoost = math.saturate(centerDelta / math.max(worldTexelSize * 4f, 0.001f));
            float resolvedDecayRate = math.saturate(decayRate + motionDecayBoost * 0.45f);

            Matrix4x4 worldToLocal = Matrix4x4.Translate(-volumeCenter);
            Vector4 halfExtents = new Vector4(volumeWorldSize * 0.5f, volumeWorldSize * 0.5f, volumeWorldSize * 0.5f, 0f);
            Vector4 volumeParams = new Vector4(
                math.max(0f, injectionStrength),
                math.saturate(diffusionStrength),
                resolvedDecayRate,
                math.max(0f, deltaTime));
            Vector4 cascadeParams = new Vector4(
                math.saturate(cascadeSpikeThreshold),
                math.max(0f, cascadePropagationGain),
                math.max(0f, cascadeWaveSpeed),
                Time.time);
            Vector4 texelSize = new Vector4(
                1f / volumeResolution,
                1f / volumeResolution,
                1f / volumeResolution,
                volumeResolution);

            if (_needsClear)
            {
                BindSharedParameters(_clearKernel, halfExtents, worldToLocal, volumeParams, cascadeParams, texelSize, 0);
                biolumDiffusionCompute.SetTexture(_clearKernel, _VolumeOutputId, _volumeA);
                DispatchVolumeKernel(_clearKernel);
                biolumDiffusionCompute.SetTexture(_clearKernel, _VolumeOutputId, _volumeB);
                DispatchVolumeKernel(_clearKernel);
                _needsClear = false;
            }

            if (pointCount > 0)
                GraphicsBufferUploadUtility.UploadArray(_pointBuffer, _pointUpload, pointCount);

            BindSharedParameters(_diffuseKernel, halfExtents, worldToLocal, volumeParams, cascadeParams, texelSize, pointCount);
            biolumDiffusionCompute.SetTexture(_diffuseKernel, _VolumeInputId, _volumeA);
            biolumDiffusionCompute.SetTexture(_diffuseKernel, _VolumeOutputId, _volumeB);
            DispatchVolumeKernel(_diffuseKernel);

            BindSharedParameters(_injectKernel, halfExtents, worldToLocal, volumeParams, cascadeParams, texelSize, pointCount);
            biolumDiffusionCompute.SetTexture(_injectKernel, _VolumeInputId, _volumeB);
            biolumDiffusionCompute.SetTexture(_injectKernel, _VolumeOutputId, _volumeA);
            DispatchVolumeKernel(_injectKernel);

            PublishGlobals();
            Shader.SetGlobalTexture(_GlobalTextureId, _volumeA);
            Shader.SetGlobalMatrix(_WorldToLocalId, worldToLocal);
            Shader.SetGlobalVector(_HalfExtentsId, halfExtents);
            Shader.SetGlobalVector(_VolumeParamsId, volumeParams);
            Shader.SetGlobalFloat(_GlobalActiveId, pointCount > 0 ? 1f : 0f);
            _lastVolumeCenter = volumeCenter;
            _hasLastVolumeCenter = true;
        }

        private void ResolveDependencies()
        {
            if (_playerTransform == null && SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform))
                _playerTransform = playerTransform;

            if (_biolumManager == null)
                _biolumManager = GlobalRegistry.BiolumManager;
        }

        private void EnsureResources()
        {
            if (biolumDiffusionCompute == null)
                return;

            if (_clearKernel < 0)
                _clearKernel = biolumDiffusionCompute.FindKernel("ClearBiolumVolume");

            if (_diffuseKernel < 0)
                _diffuseKernel = biolumDiffusionCompute.FindKernel("DiffuseBiolumVolume");

            if (_injectKernel < 0)
                _injectKernel = biolumDiffusionCompute.FindKernel("InjectBiolumPoints");

            int clampedResolution = Mathf.Clamp(volumeResolution, 32, 64);
            if (_volumeA == null || _volumeA.width != clampedResolution)
            {
                ReleaseVolumeTextures();
                _volumeA = CreateVolumeTexture(clampedResolution, "__HectonBiolumVolumeA");
                _volumeB = CreateVolumeTexture(clampedResolution, "__HectonBiolumVolumeB");
                volumeResolution = clampedResolution;
                _needsClear = true;
            }

            if (_pointBuffer == null)
            {
                _pointBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<BiolumPointGpuData>(MaxTrackedZones); // COLD ALLOC: GraphicsBuffer[32] — persistent biolum emitter upload buffer for 3D diffusion volume — owner: HectonBiolumDiffusionVolume
            }
        }

        private RenderTexture CreateVolumeTexture(int resolution, string textureName)
        {
            RenderTextureDescriptor descriptor = new RenderTextureDescriptor(resolution, resolution)
            {
                dimension = TextureDimension.Tex3D,
                volumeDepth = resolution,
                graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat,
                depthBufferBits = 0,
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false,
                enableRandomWrite = true,
                sRGB = false
            };

            RenderTexture texture = new RenderTexture(descriptor)
            {
                name = textureName,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            }; // COLD ALLOC: RenderTexture[1] — persistent player-centered 3D biolum diffusion volume — owner: HectonBiolumDiffusionVolume
            texture.Create();
            return texture;
        }

        private int CollectNearbyPoints(Vector3 volumeCenter)
        {
            if (_biolumManager == null)
                return 0;

            int count = _biolumManager.CopyNearbyZonesNonAlloc(volumeCenter, zoneGatherRadius, _nearbyZones, _nearbyZoneWeights, includeOcean: true, includeFloor: true);
            int safeCount = math.min(count, MaxTrackedZones);
            for (int i = 0; i < safeCount; i++)
            {
                HectonBiolumZone zone = _nearbyZones[i];
                if (zone == null)
                    continue;

                Color zoneColor = zone.SampleZoneColor().linear;
                float zoneIntensity = math.max(0f, zone.SampleZoneIntensity());
                float zoneRange = math.max(0.5f, zone.SampleZoneRange());
                float weight = math.max(0f, _nearbyZoneWeights[i]);
                _pointUpload[i] = new BiolumPointGpuData
                {
                    PositionRange = new Vector4(zone.GetZonePosition().x, zone.GetZonePosition().y, zone.GetZonePosition().z, zoneRange),
                    ColorIntensity = new Vector4(zoneColor.r, zoneColor.g, zoneColor.b, zoneIntensity * weight)
                };
            }

            return safeCount;
        }

        private void BindSharedParameters(
            int kernelIndex,
            Vector4 halfExtents,
            Matrix4x4 worldToLocal,
            Vector4 volumeParams,
            Vector4 cascadeParams,
            Vector4 texelSize,
            int pointCount)
        {
            biolumDiffusionCompute.SetVector(_HalfExtentsId, halfExtents);
            biolumDiffusionCompute.SetMatrix(_WorldToLocalId, worldToLocal);
            biolumDiffusionCompute.SetVector(_VolumeParamsId, volumeParams);
            biolumDiffusionCompute.SetVector(_CascadeParamsId, cascadeParams);
            biolumDiffusionCompute.SetVector(_TexelSizeId, texelSize);
            biolumDiffusionCompute.SetInt(_PointCountId, pointCount);
            biolumDiffusionCompute.SetBuffer(kernelIndex, _PointBufferId, _pointBuffer);
        }

        private void DispatchVolumeKernel(int kernelIndex)
        {
            int dispatchCount = Mathf.CeilToInt(volumeResolution / (float)ThreadGroupSize);
            biolumDiffusionCompute.Dispatch(kernelIndex, dispatchCount, dispatchCount, dispatchCount);
        }

        private void PublishGlobals()
        {
            if (_volumeA != null)
                Shader.SetGlobalTexture(_GlobalTextureId, _volumeA);
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _registered = GlobalRegistry.Updatables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registered = false;
        }

        private void ReleaseResources()
        {
            ReleaseVolumeTextures();
            if (_pointBuffer != null)
            {
                _pointBuffer.Release();
                _pointBuffer = null;
            }

            _hasLastVolumeCenter = false;
            _lastVolumeCenter = default;
        }

        private void ReleaseVolumeTextures()
        {
            if (_volumeA != null)
            {
                _volumeA.Release();
                if (Application.isPlaying)
                    Destroy(_volumeA);
                else
                    DestroyImmediate(_volumeA);
                _volumeA = null;
            }

            if (_volumeB != null)
            {
                _volumeB.Release();
                if (Application.isPlaying)
                    Destroy(_volumeB);
                else
                    DestroyImmediate(_volumeB);
                _volumeB = null;
            }
        }
    }
}

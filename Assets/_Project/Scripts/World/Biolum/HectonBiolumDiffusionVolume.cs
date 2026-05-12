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
        private const int MaxGlowShaderPoints = 16;
        private const float GlowPositionHashScale = 20f;
        private const float GlowRangeHashScale = 16f;
        private const float GlowColorHashScale = 255f;
        private const float GlowIntensityHashScale = 128f;
        private const float GlowPointSonarPulseGain = 2.5f;
        private const float HashClampMin = -2147483000f;
        private const float HashClampMax = 2147483000f;
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;
        private const int GlowPointInvalidNumberHash = unchecked((int)0x474C4F57); // "GLOW"
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
        private static readonly int _GlowPointPositionRangeId = Shader.PropertyToID("_HectonGlowPointPositionRange");
        private static readonly int _GlowPointColorIntensityId = Shader.PropertyToID("_HectonGlowPointColorIntensity");
        private static readonly int _GlowPointParamsId = Shader.PropertyToID("_HectonGlowPointParams");

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
        private int _lastUploadedPointCount = -1;
        private int _lastPublishedGlowCount = -1;
        private int _lastInvalidGlowTelemetryFrame = -1;
        private uint _pendingPointUploadHash;
        private uint _lastUploadedPointHash;
        private uint _pendingGlowHash;
        private uint _lastPublishedGlowHash;
        private RenderTexture _volumeA;
        private RenderTexture _volumeB;
        private GraphicsBuffer _pointBuffer;
        private readonly BiolumPointGpuData[] _pointUpload = new BiolumPointGpuData[MaxTrackedZones]; // COLD ALLOC: BiolumPointGpuData[32] — persistent GPU upload staging for biolum diffusion emitters — owner: HectonBiolumDiffusionVolume
        private readonly Vector4[] _glowPointPositionRangeUpload = new Vector4[MaxGlowShaderPoints]; // COLD ALLOC: Vector4[16] - shader-global glow point positions/ranges - owner: HectonBiolumDiffusionVolume
        private readonly Vector4[] _glowPointColorIntensityUpload = new Vector4[MaxGlowShaderPoints]; // COLD ALLOC: Vector4[16] - shader-global glow point colors/intensities - owner: HectonBiolumDiffusionVolume
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
            PublishGlowPointGlobals(0, force: true);
        }

        private void OnDestroy()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregister();
            ReleaseResources();
            PublishGlowPointGlobals(0, force: true);
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
            PublishGlowPointGlobals(0, force: true);
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
                PublishGlowPointGlobals(0, force: true);
                return;
            }

            Vector3 volumeCenter = _playerTransform.position;
            _debugVolumeCenter = volumeCenter;

            int pointCount = CollectNearbyPoints(volumeCenter);
            _debugPointCount = pointCount;
            PublishGlowPointGlobals(pointCount);

            float worldTexelSize = volumeWorldSize / math.max(1, volumeResolution);
            Vector3 centerOffset = _hasLastVolumeCenter ? volumeCenter - _lastVolumeCenter : Vector3.zero;
            float centerDeltaSq = _hasLastVolumeCenter ? centerOffset.sqrMagnitude : 0f;
            float clearDistance = volumeWorldSize * 0.5f;
            if (_hasLastVolumeCenter && centerDeltaSq >= clearDistance * clearDistance)
                _needsClear = true;

            float centerDelta = centerDeltaSq > 0.000001f ? EstimateLength3D(centerOffset) : 0f;
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

            if (pointCount > 0 && ShouldUploadPointBuffer(pointCount))
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
            if (_playerTransform == null && GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform))
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
            {
                _pendingPointUploadHash = 0u;
                _pendingGlowHash = 0u;
                return 0;
            }

            int count = _biolumManager.CopyNearbyZonesNonAlloc(volumeCenter, zoneGatherRadius, _nearbyZones, _nearbyZoneWeights, includeOcean: true, includeFloor: true);
            int safeCount = math.min(count, MaxTrackedZones);
            int writeCount = 0;
            uint pointHash = FnvOffsetBasis;
            uint glowHash = FnvOffsetBasis;
            for (int i = 0; i < safeCount; i++)
            {
                HectonBiolumZone zone = _nearbyZones[i];
                if (zone == null)
                    continue;

                Vector3 zonePosition = zone.GetZonePosition();
                if (!MathGuard.IsFinite(zonePosition))
                {
                    ReportInvalidGlowInput();
                    continue;
                }

                Color zoneColor = zone.SampleZoneColor().linear;
                float zoneIntensity = SanitizeGlowNonNegative(zone.SampleZoneIntensity());
                float zoneRange = math.max(0.5f, SanitizeGlowNonNegative(zone.SampleZoneRange(), 0.5f));
                float weight = SanitizeGlowNonNegative(_nearbyZoneWeights[i]);
                float weightedIntensity = zoneIntensity * weight;
                Vector4 positionRange = new Vector4(zonePosition.x, zonePosition.y, zonePosition.z, zoneRange);
                Vector4 colorIntensity = new Vector4(
                    SanitizeGlowNonNegative(zoneColor.r),
                    SanitizeGlowNonNegative(zoneColor.g),
                    SanitizeGlowNonNegative(zoneColor.b),
                    weightedIntensity);

                _pointUpload[writeCount] = new BiolumPointGpuData
                {
                    PositionRange = positionRange,
                    ColorIntensity = colorIntensity
                };
                pointHash = MixGlowPointHash(pointHash, positionRange, colorIntensity);

                if (writeCount < MaxGlowShaderPoints)
                {
                    _glowPointPositionRangeUpload[writeCount] = positionRange;
                    _glowPointColorIntensityUpload[writeCount] = colorIntensity;
                    glowHash = MixGlowPointHash(glowHash, positionRange, colorIntensity);
                }

                writeCount++;
            }

            _pendingPointUploadHash = writeCount > 0 ? pointHash : 0u;
            _pendingGlowHash = writeCount > 0 ? glowHash : 0u;
            return writeCount;
        }

        private bool ShouldUploadPointBuffer(int pointCount)
        {
            int safeCount = math.min(math.max(pointCount, 0), MaxTrackedZones);
            uint pointHash = safeCount > 0 ? _pendingPointUploadHash : 0u;
            if (_lastUploadedPointCount == safeCount && _lastUploadedPointHash == pointHash)
                return false;

            _lastUploadedPointCount = safeCount;
            _lastUploadedPointHash = pointHash;
            return safeCount > 0;
        }

        private void PublishGlowPointGlobals(int pointCount, bool force = false)
        {
            int glowCount = Mathf.Clamp(pointCount, 0, MaxGlowShaderPoints);
            uint glowHash = glowCount > 0 ? _pendingGlowHash : 0u;
            if (!force && _lastPublishedGlowCount == glowCount && _lastPublishedGlowHash == glowHash)
                return;

            if (glowCount > 0 && (force || _lastPublishedGlowHash != glowHash))
            {
                Shader.SetGlobalVectorArray(_GlowPointPositionRangeId, _glowPointPositionRangeUpload);
                Shader.SetGlobalVectorArray(_GlowPointColorIntensityId, _glowPointColorIntensityUpload);
            }

            Shader.SetGlobalVector(_GlowPointParamsId, new Vector4(glowCount, GlowPointSonarPulseGain, 0f, 0f));
            _lastPublishedGlowCount = glowCount;
            _lastPublishedGlowHash = glowHash;
        }

        private void ReportInvalidGlowInput()
        {
            int frame = Time.frameCount;
            if (_lastInvalidGlowTelemetryFrame == frame)
                return;

            _lastInvalidGlowTelemetryFrame = frame;
            GlobalTelemetryBus.PublishMathGuardInvalidNumber(GlowPointInvalidNumberHash);
        }

        private float SanitizeGlowNonNegative(float value, float fallback = 0f)
        {
            if (math.isfinite(value))
                return math.max(0f, value);

            ReportInvalidGlowInput();
            return math.max(0f, fallback);
        }

        private static uint MixGlowPointHash(uint hash, Vector4 positionRange, Vector4 colorIntensity)
        {
            hash = MixHash(hash, QuantizeHashComponent(positionRange.x, GlowPositionHashScale));
            hash = MixHash(hash, QuantizeHashComponent(positionRange.y, GlowPositionHashScale));
            hash = MixHash(hash, QuantizeHashComponent(positionRange.z, GlowPositionHashScale));
            hash = MixHash(hash, QuantizeHashComponent(positionRange.w, GlowRangeHashScale));
            hash = MixHash(hash, QuantizeHashComponent(colorIntensity.x, GlowColorHashScale));
            hash = MixHash(hash, QuantizeHashComponent(colorIntensity.y, GlowColorHashScale));
            hash = MixHash(hash, QuantizeHashComponent(colorIntensity.z, GlowColorHashScale));
            hash = MixHash(hash, QuantizeHashComponent(colorIntensity.w, GlowIntensityHashScale));
            return hash;
        }

        private static int QuantizeHashComponent(float value, float scale)
        {
            if (!math.isfinite(value))
                return 0;

            float scaled = math.clamp(value * scale, HashClampMin, HashClampMax);
            return (int)math.round(scaled);
        }

        private static uint MixHash(uint hash, int value)
        {
            unchecked
            {
                hash ^= (uint)value;
                return hash * FnvPrime;
            }
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
            int dispatchCount = math.max(1, (volumeResolution + ThreadGroupSize - 1) / ThreadGroupSize);
            biolumDiffusionCompute.Dispatch(kernelIndex, dispatchCount, dispatchCount, dispatchCount);
        }

        private static float EstimateLength3D(Vector3 value)
        {
            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float az = math.abs(value.z);
            float maxAxis = math.max(ax, math.max(ay, az));
            float minAxis = math.min(ax, math.min(ay, az));
            float midAxis = ax + ay + az - maxAxis - minAxis;
            return maxAxis + (midAxis * 0.375f) + (minAxis * 0.25f);
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

            _lastUploadedPointCount = -1;
            _lastUploadedPointHash = 0u;
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

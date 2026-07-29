using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.UI
{
    /// <summary>
    /// Draws physical acoustic contacts as a fixed-size instanced voxel sphere.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Acoustic Radar Sphere Renderer")]
    public sealed class AcousticRadarSphereRenderer : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const int MaxBlips = 64;
        private const int MinimumQualityBlipCapacity = 16;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int PulseIntensityId = Shader.PropertyToID("_PulseIntensity");

        [Header("Anchors")]
        [SerializeField] private Transform radarAnchor = null;
        [SerializeField] private Transform listenerOrigin = null;
        [SerializeField, Tooltip("Forward reference for rear-hemisphere culling. Defaults to the listener transform.")]
        private Transform submarineForwardReference = null;

        [Header("Rendering")]
        [SerializeField] private Mesh voxelMesh = null;
        [SerializeField] private Material voxelMaterial = null;
        [SerializeField] private Color voxelColor = new Color(0.38f, 0.98f, 0.88f, 0.72f);
        [SerializeField, Range(0.05f, 1.5f)] private float sphereRadius = 0.32f;
        [SerializeField, Range(0.002f, 0.08f)] private float voxelSizeMeters = 0.014f;
        [SerializeField, Range(0f, 4f)] private float pulseIntensity = 1.15f;
        [SerializeField] private int renderLayer = 0;

        [Header("Acoustics")]
        [SerializeField, Min(1f)] private float maxContactDistanceMeters = 80f;
        [SerializeField, Range(0f, 0.1f)] private float minimumAmplitude = 0.001f;
        [SerializeField, Range(0f, 1f)] private float minimumRadiusFraction = 0.08f;

        // COLD ALLOC: SpatialAudioImpactEmitterSample[64] -- fixed acoustic impact copy buffer with cached AUP -- owner: AcousticRadarSphereRenderer
        private readonly SpatialAudioImpactEmitterSample[] _samples =
            new SpatialAudioImpactEmitterSample[MaxBlips];
        // COLD ALLOC: Matrix4x4[64] -- DrawMeshInstanced payload -- owner: AcousticRadarSphereRenderer
        private readonly Matrix4x4[] _matrices = new Matrix4x4[MaxBlips];

        private bool _registeredLateFrame;
        private int _matrixCount;
        private MaterialPropertyBlock _materialProperties;
        private Material _resolvedMaterial;
        private Mesh _resolvedVoxelMesh;
        private Camera _viewCamera;
        private object _cachedAudioRuntime;
        private ISpatialAudioImpactEmitterReadModel _cachedAudioManager;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private Color _appliedVoxelColor;
        private float _appliedPulseIntensity;
        private bool _hotSwapListenerRegistered;
        private bool _materialPropertiesDirty = true;
        private bool _materialHasBaseColor;
        private bool _materialHasPulseIntensity;
        private int _qualityMatrixCapacity = MaxBlips;
        private bool _missingDrawAssetsAnnounced;

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            EnsureResources();
            TryRegisterTickManager();
        }

        private void Start()
        {
            CacheRegistryServicesCold();
            EnsureResources();
            TryRegisterTickManager();
        }

        private void OnDisable()
        {
            _matrixCount = 0;
            TryUnregisterTickManager();
            TryUnregisterHotSwapListener();
            _cachedAudioRuntime = null;
            _cachedAudioManager = null;
        }

        private void OnDestroy()
        {
            TryUnregisterTickManager();
            TryUnregisterHotSwapListener();
            _cachedAudioRuntime = null;
            _cachedAudioManager = null;
            _resolvedMaterial = null;
            _resolvedVoxelMesh = null;
        }

        private void RefreshMatricesForLateFrame()
        {
            RefreshQualityPolicy();
            ApplyMaterialPropertiesIfNeeded();
            _matrixCount = 0;
            if (_resolvedMaterial == null || _resolvedVoxelMesh == null)
                return;

            ISpatialAudioImpactEmitterReadModel audioManager = ResolveImpactEmitterReadModel();
            if (audioManager == null)
                return;

            Transform anchor = radarAnchor != null ? radarAnchor : transform;
            Transform listener = ResolveListenerTransform();
            if (anchor == null || listener == null)
                return;

            int sampleCount = audioManager.CopyActiveImpactEmitterSamples(_samples);
            if (sampleCount <= 0)
                return;

            Vector3 listenerPosition = listener.position;
            Quaternion listenerRotation = listener.rotation;
            float3 listenerRight = (float3)(listenerRotation * Vector3.right);
            float3 listenerUp = (float3)(listenerRotation * Vector3.up);
            float3 listenerForward = (float3)(listenerRotation * Vector3.forward);
            Quaternion anchorRotation = anchor.rotation;
            Vector3 anchorPosition = anchor.position;
            if (!TryResolveListenerAup(listenerPosition, out AbsoluteUniversePosition listenerAup))
                return;

            Transform forwardReference = submarineForwardReference != null ? submarineForwardReference : listener;
            float3 submarineForward = object.ReferenceEquals(forwardReference, listener)
                ? ResolveForwardUnitVector(listenerForward)
                : ResolveForwardUnitVector(forwardReference);
            float safeMaxDistance = math.max(1f, maxContactDistanceMeters);
            float safeMaxDistanceSq = safeMaxDistance * safeMaxDistance;
            float inverseMaxDistanceSq = math.rcp(safeMaxDistanceSq);
            float radius = math.max(0.01f, sphereRadius);
            float baseVoxelSize = math.max(0.001f, voxelSizeMeters);
            float minimumRadius = math.saturate(minimumRadiusFraction);
            int matrixCapacity = math.clamp(_qualityMatrixCapacity, MinimumQualityBlipCapacity, MaxBlips);

            for (int i = 0; i < sampleCount && _matrixCount < matrixCapacity; i++)
            {
                SpatialAudioImpactEmitterSample sample = _samples[i];
                float amplitude = math.saturate(sample.Amplitude);
                if (amplitude <= minimumAmplitude)
                    continue;

                AbsoluteUniversePosition sampleAup = sample.PositionAup;
                float3 deltaAup = AupPrecisionMath.LocalDeltaFloat3Clamped(
                    sampleAup.ToAbsoluteDouble3(),
                    listenerAup.ToAbsoluteDouble3(),
                    AupPrecisionMath.DefaultMaxLocalCastMeters,
                    float3.zero);
                if (!math.all(math.isfinite(deltaAup)) || math.dot(submarineForward, deltaAup) <= 0f)
                    continue;

                float distanceSq = math.lengthsq(deltaAup);
                if (distanceSq <= 0.0001f || distanceSq > safeMaxDistanceSq)
                    continue;

                float approximateDistance = ApproximateMagnitude(deltaAup);
                if (approximateDistance <= 0.0001f)
                    continue;

                float inverseApproximateDistance = math.rcp(math.max(approximateDistance, 0.0001f));
                float distance01 = math.saturate(distanceSq * inverseMaxDistanceSq);
                float localX = math.dot(deltaAup, listenerRight) * inverseApproximateDistance;
                float localY = math.dot(deltaAup, listenerUp) * inverseApproximateDistance;
                float localZ = math.dot(deltaAup, listenerForward) * inverseApproximateDistance;
                Vector3 directionOnRadar = anchorRotation * new Vector3(localX, localY, localZ);
                float radial01 = math.lerp(minimumRadius, 1f, distance01);
                Vector3 position = anchorPosition + directionOnRadar * (radius * radial01);
                float scaleMeters = baseVoxelSize * math.lerp(0.7f, 2.4f, amplitude);
                Vector3 scale = new Vector3(scaleMeters, scaleMeters, scaleMeters);
                _matrices[_matrixCount] = Matrix4x4.TRS(position, anchorRotation, scale);
                _matrixCount++;
            }
        }

        private static float3 ResolveForwardUnitVector(Transform reference)
        {
            if (reference == null)
                return new float3(0f, 0f, 1f);

            Quaternion rotation = reference.rotation;
            float3 forward = (float3)(rotation * Vector3.forward);
            return ResolveForwardUnitVector(forward);
        }

        private static float3 ResolveForwardUnitVector(float3 forward)
        {
            return math.all(math.isfinite(forward)) && math.lengthsq(forward) > 0.25f
                ? forward
                : new float3(0f, 0f, 1f);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Audio:
                    CacheImpactEmitterReadModel(currentService);
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _cachedPlayerContext = currentService as IPlayerRuntimeContext;
                    _viewCamera = null;
                    break;
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private void CacheRegistryServicesCold()
        {
            RefreshQualityPolicy();
            CacheImpactEmitterReadModel(GlobalRegistry.Audio);
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (!ReferenceEquals(_cachedPlayerContext, playerContext))
            {
                _cachedPlayerContext = playerContext;
                _viewCamera = null;
            }
        }

        private void CacheImpactEmitterReadModel(object audioRuntime)
        {
            if (!IsAudioRuntimeObjectUsable(audioRuntime))
            {
                _cachedAudioRuntime = null;
                _cachedAudioManager = null;
                return;
            }

            _cachedAudioRuntime = audioRuntime;
            _cachedAudioManager = audioRuntime as ISpatialAudioImpactEmitterReadModel;
        }

        private ISpatialAudioImpactEmitterReadModel ResolveImpactEmitterReadModel()
        {
            object audioRuntime = _cachedAudioRuntime;
            if (!IsAudioRuntimeObjectUsable(audioRuntime))
            {
                _cachedAudioRuntime = null;
                _cachedAudioManager = null;
                return null;
            }

            ISpatialAudioImpactEmitterReadModel audioManager = _cachedAudioManager;
            if (ReferenceEquals(audioManager, audioRuntime) && IsAudioRuntimeObjectUsable(audioManager))
                return audioManager;

            audioManager = audioRuntime as ISpatialAudioImpactEmitterReadModel;
            _cachedAudioManager = audioManager;
            return IsAudioRuntimeObjectUsable(audioManager) ? audioManager : null;
        }

        private static bool IsAudioRuntimeObjectUsable(object runtime)
        {
            if (runtime == null)
                return false;

            if (runtime is IAudioService audioService && !audioService.IsAudioRuntimeReady)
                return false;

            if (runtime is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private void RefreshQualityPolicy()
        {
            float qualityWeight01 = SanitizeQualityWeight01(HomeostasisBrain.GlobalQualityWeight);
            _qualityMatrixCapacity = ResolveQualityCapacity(qualityWeight01, MinimumQualityBlipCapacity, MaxBlips);
        }

        private bool TryResolveListenerAup(Vector3 listenerPosition, out AbsoluteUniversePosition listenerAup)
        {
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext != null &&
                playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState cachedMovementState) &&
                (cachedMovementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
            {
                listenerAup = OffsetAupLocal(
                    in cachedMovementState.PredictedAup,
                    (Vector3)((float3)listenerPosition - cachedMovementState.PredictedWorldPosition));
                return listenerAup.IsFinite();
            }

            listenerAup = default;
            return false;
        }

        private static AbsoluteUniversePosition OffsetAupLocal(in AbsoluteUniversePosition anchorAup, Vector3 runtimeOffset)
        {
            AbsoluteUniversePosition result = anchorAup;
            result.LocalX += runtimeOffset.x;
            result.LocalY += runtimeOffset.y;
            result.LocalZ += runtimeOffset.z;
            NormalizeAupLocalAxis(ref result.GridX, ref result.LocalX);
            NormalizeAupLocalAxis(ref result.GridY, ref result.LocalY);
            NormalizeAupLocalAxis(ref result.GridZ, ref result.LocalZ);
            return result;
        }

        private static void NormalizeAupLocalAxis(ref long grid, ref float local)
        {
            const float cellSize = AbsoluteUniversePosition.CellSizeMeters;
            if (local >= 0f && local < cellSize)
                return;

            long gridDelta = (long)math.floor(local / cellSize);
            grid += gridDelta;
            local -= gridDelta * cellSize;
            if (local < 0f)
            {
                local += cellSize;
                grid--;
                return;
            }

            if (local >= cellSize)
            {
                local -= cellSize;
                grid++;
            }
        }

        private static float ApproximateMagnitude(float3 value)
        {
            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float az = math.abs(value.z);
            float maxAxis = math.max(ax, math.max(ay, az));
            float minAxis = math.min(ax, math.min(ay, az));
            float midAxis = ax + ay + az - maxAxis - minAxis;
            return maxAxis + midAxis * 0.375f + minAxis * 0.125f;
        }

        private static float SmoothStep01(float value)
        {
            float t = SanitizeQualityWeight01(value);
            return t * t * (3f - 2f * t);
        }

        private static float SanitizeQualityWeight01(float value)
        {
            return math.saturate(math.isfinite(value) ? value : 0f);
        }

        private static int ResolveQualityCapacity(float qualityWeight01, int minimum, int maximum)
        {
            int safeMinimum = math.max(0, minimum);
            int safeMaximum = math.max(safeMinimum, maximum);
            float qualityCurve = SmoothStep01(qualityWeight01);
            return math.clamp(
                (int)math.round(math.lerp(safeMinimum, safeMaximum, qualityCurve)),
                safeMinimum,
                safeMaximum);
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            RefreshMatricesForLateFrame();
            if (_matrixCount <= 0 || _resolvedMaterial == null || _resolvedVoxelMesh == null)
                return;

            Camera renderCamera = ResolveRenderCamera();
            if (renderCamera == null)
                return;

            UnityEngine.Graphics.DrawMeshInstanced(
                _resolvedVoxelMesh,
                0,
                _resolvedMaterial,
                _matrices,
                _matrixCount,
                _materialProperties,
                ShadowCastingMode.Off,
                false,
                renderLayer,
                renderCamera,
                LightProbeUsage.Off);
        }

        private Camera ResolveRenderCamera()
        {
            Camera renderCamera = GlobalRenderContext.CurrentCamera;
            if (IsGameplayRenderCamera(renderCamera))
                return renderCamera;

            if (_viewCamera == null)
            {
                IPlayerRuntimeContext playerContext = _cachedPlayerContext;
                _viewCamera = playerContext != null ? playerContext.PlayerCamera : null;
            }

            return IsGameplayRenderCamera(_viewCamera) ? _viewCamera : null;
        }

        private static bool IsGameplayRenderCamera(Camera camera)
        {
            return camera != null &&
                   camera.isActiveAndEnabled &&
                   camera.cameraType != CameraType.Preview &&
                   camera.cameraType != CameraType.Reflection;
        }

        private Transform ResolveListenerTransform()
        {
            if (listenerOrigin != null)
                return listenerOrigin;

            if (_viewCamera == null)
            {
                IPlayerRuntimeContext playerContext = _cachedPlayerContext;
                _viewCamera = playerContext != null ? playerContext.PlayerCamera : null;
            }

            return _viewCamera != null ? _viewCamera.transform : transform;
        }

        /// <summary>
        /// Resolves the authored draw pair, or reports the authoring gap once without throwing.
        /// </summary>
        /// <remarks>
        /// The four <c>UnityEngine.Assertions.Assert</c> calls that used to open this method THREW - nothing
        /// under Assets sets <c>Assert.raiseExceptions = false</c> - and both callers reach this method BEFORE
        /// they register the component: <see cref="OnEnable"/> at line 71 and <see cref="Start"/> at line 78
        /// each call <c>EnsureResources()</c> and only then <see cref="TryRegisterTickManager"/>. So an
        /// unassigned inspector slot did not merely skip a draw - it threw out of the Unity message, skipped
        /// <c>SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI)</c>, and left the radar
        /// permanently off the UI late-frame lane. Assigning the material later in play-mode could not recover
        /// it, because nothing calls EnsureResources again.
        ///
        /// The asserts guarded nothing. The <c>return</c> below them was already the complete degradation
        /// path, and it is triple-covered downstream: <see cref="RefreshMatricesForLateFrame"/> returns early
        /// on a null resolved pair (:106) and <see cref="LateFrameTick"/> re-checks both before
        /// <c>DrawMeshInstanced</c> (:387). An unregistered tick lane, by contrast, had no recovery at all.
        /// </remarks>
        private void EnsureResources()
        {
            EnsureMaterialPropertiesCold();
            bool materialAssigned = voxelMaterial != null;
            bool meshAssigned = voxelMesh != null;
            bool authoredMaterialValid = materialAssigned && voxelMaterial.enableInstancing;
            bool authoredMeshValid = meshAssigned && voxelMesh.subMeshCount > 0 && voxelMesh.GetIndexCount(0) > 0u;
            if (!authoredMaterialValid || !authoredMeshValid)
            {
                _resolvedMaterial = null;
                _resolvedVoxelMesh = null;

                // Report LAST and once. The caller continues to TryRegisterTickManager after this returns, so
                // a future re-introduced throw here can no longer strand the UI late-frame registration.
                if (!_missingDrawAssetsAnnounced)
                {
                    _missingDrawAssetsAnnounced = true;
                    LogInvalidAcousticRadarDrawAssets(
                        materialAssigned,
                        authoredMaterialValid,
                        meshAssigned,
                        authoredMeshValid);
                }

                return;
            }

            if (!ReferenceEquals(_resolvedMaterial, voxelMaterial))
            {
                _resolvedMaterial = voxelMaterial;
                _materialHasBaseColor = _resolvedMaterial.HasProperty(BaseColorId);
                _materialHasPulseIntensity = _resolvedMaterial.HasProperty(PulseIntensityId);
                _materialPropertiesDirty = true;
            }

            _resolvedVoxelMesh = voxelMesh;

            ApplyMaterialPropertiesIfNeeded();
        }

        /// <summary>
        /// One-shot report of an unusable authored draw pair. The latch guarantees single emission and every
        /// parameter is a primitive, so no string work and no allocation reaches any tick cadence.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogInvalidAcousticRadarDrawAssets(
            bool materialAssigned,
            bool authoredMaterialValid,
            bool meshAssigned,
            bool authoredMeshValid)
        {
            if (!materialAssigned)
            {
                Hecton8.Core.H8Debug.LogError("AcousticRadarSphereRenderer: serialized field 'voxelMaterial' is unassigned. The acoustic contact sphere draws no blips this session - LateFrameTick null-guards the pair and skips DrawMeshInstanced. Every registration stays live. Runtime material generation is forbidden: assign the authored acoustic radar voxel material in the inspector.");
            }
            else if (!authoredMaterialValid)
            {
                Hecton8.Core.H8Debug.LogError("AcousticRadarSphereRenderer: the material assigned to 'voxelMaterial' has Enable GPU Instancing OFF. Graphics.DrawMeshInstanced requires it, so the acoustic contact sphere draws no blips this session. Tick 'Enable GPU Instancing' on that material asset.");
            }

            if (!meshAssigned)
            {
                Hecton8.Core.H8Debug.LogError("AcousticRadarSphereRenderer: serialized field 'voxelMesh' is unassigned. The acoustic contact sphere draws no blips this session - LateFrameTick null-guards the pair and skips DrawMeshInstanced. Every registration stays live. Assign the authored voxel mesh in the inspector.");
                return;
            }

            if (!authoredMeshValid)
            {
                Hecton8.Core.H8Debug.LogError("AcousticRadarSphereRenderer: the mesh assigned to 'voxelMesh' has no indexed submesh 0 (subMeshCount is 0 or GetIndexCount(0) is 0), so DrawMeshInstanced would submit an empty draw. The acoustic contact sphere shows no blips this session. Reimport or replace that mesh asset with one that carries an index buffer.");
            }
        }

        private void EnsureMaterialPropertiesCold()
        {
            if (_materialProperties != null)
                return;

            // COLD ALLOC: MaterialPropertyBlock[1] -- acoustic radar voxel per-draw payload -- owner: AcousticRadarSphereRenderer.
            _materialProperties = new MaterialPropertyBlock();
            _materialPropertiesDirty = true;
        }

        private void ApplyMaterialPropertiesIfNeeded()
        {
            if (_resolvedMaterial == null)
                return;

            EnsureMaterialPropertiesCold();

            if (!_materialPropertiesDirty &&
                SameColor(_appliedVoxelColor, voxelColor) &&
                math.abs(_appliedPulseIntensity - pulseIntensity) <= 0.0001f)
            {
                return;
            }

            if (_materialHasBaseColor)
                _materialProperties.SetColor(BaseColorId, voxelColor);
            if (_materialHasPulseIntensity)
                _materialProperties.SetFloat(PulseIntensityId, pulseIntensity);

            _appliedVoxelColor = voxelColor;
            _appliedPulseIntensity = pulseIntensity;
            _materialPropertiesDirty = false;
        }

        private static bool SameColor(Color lhs, Color rhs)
        {
            return math.abs(lhs.r - rhs.r) <= 0.0001f &&
                   math.abs(lhs.g - rhs.g) <= 0.0001f &&
                   math.abs(lhs.b - rhs.b) <= 0.0001f &&
                   math.abs(lhs.a - rhs.a) <= 0.0001f;
        }

        private void TryRegisterTickManager()
        {
            if (_registeredLateFrame || !Application.isPlaying)
                return;

            _registeredLateFrame = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
        }

        private void TryUnregisterTickManager()
        {
            if (_registeredLateFrame)
            {
                SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
                _registeredLateFrame = false;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            sphereRadius = math.clamp(sphereRadius, 0.05f, 1.5f);
            voxelSizeMeters = math.clamp(voxelSizeMeters, 0.002f, 0.08f);
            pulseIntensity = math.clamp(pulseIntensity, 0f, 4f);
            maxContactDistanceMeters = math.max(1f, maxContactDistanceMeters);
            minimumAmplitude = math.clamp(minimumAmplitude, 0f, 0.1f);
            minimumRadiusFraction = math.saturate(minimumRadiusFraction);
            _materialPropertiesDirty = true;
        }
#endif
    }
}

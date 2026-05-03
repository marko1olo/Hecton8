using System.Collections.Generic;
using Hecton8.Caves;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Cheap cave ambient/reflection darkening driven by authored voxel-volume bounds.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-6750)]
    public sealed class HectonCaveVoxelAmbientOcclusionController : MonoBehaviour, ITickable, IUpdatable, ISlowTickable
    {
        private const float BaselineEpsilon = 0.0005f;
        private const float ViewerFallbackRetryIntervalSeconds = 2f;
        private const float VolumeFallbackRefreshIntervalSeconds = 2f;

        [Header("-- References ----------------")]
        [Tooltip("Optional explicit viewer transform. Falls back to the player transform and main camera.")]
        [SerializeField] private Transform viewerTransform;
        [Tooltip("Optional explicit viewer camera. Falls back to the first active camera when left empty.")]
        [SerializeField] private Camera viewerCamera;

        [Header("-- Cave AO -------------------")]
        [Tooltip("Meters from the nearest cave wall required to reach full ambient darkening.")]
        [SerializeField, Min(0.5f)] private float fullOcclusionInteriorDepth = 8f;
        [Tooltip("Ambient-intensity multiplier at full cave occlusion.")]
        [SerializeField, Range(0.2f, 1f)] private float caveAmbientIntensityScale = 0.58f;
        [Tooltip("Reflection-intensity multiplier at full cave occlusion.")]
        [SerializeField, Range(0.2f, 1f)] private float caveReflectionIntensityScale = 0.62f;
        [Tooltip("How quickly the applied cave occlusion converges to the target value.")]
        [SerializeField, Range(0.5f, 8f)] private float occlusionBlendRate = 3f;

        [Header("-- Diagnostics ---------------")]
        [SerializeField] private bool _debugHasViewer;
        [SerializeField] private int _debugVolumeCount;
        [SerializeField] private float _debugTargetOcclusion;
        [SerializeField] private float _debugAppliedOcclusion;
        [SerializeField] private Vector3 _debugViewerPositionWS;

        private bool _registeredUpdatable;
        private bool _registeredSlowTickable;
        private WorldCaveDirector _worldCaveDirector;
        // COLD ALLOC: List<HectonVoxelVolume>[32] - active cave-volume cache pulled from WorldCaveDirector without scene scans - owner: HectonCaveVoxelAmbientOcclusionController
        private readonly List<HectonVoxelVolume> _volumeBuffer = new List<HectonVoxelVolume>(32);
        private float _targetOcclusion;
        private float _appliedOcclusion;
        private float _sourceAmbientIntensity = 1f;
        private float _sourceReflectionIntensity = 1f;
        private float _lastAppliedAmbientIntensity = -1f;
        private float _lastAppliedReflectionIntensity = -1f;
        private Camera _fallbackViewerCamera;
        private float _nextViewerFallbackResolveTime;
        private float _nextVolumeFallbackRefreshTime;

        private void Awake()
        {
            RefreshVolumeCache();
            CaptureLiveBaselines();
        }

        private void OnEnable()
        {
            RenderSettingsLifecycleGuard.Acquire(this);
            TryRegister();
            TryResolveViewerReferences();
            RefreshVolumeCache();
            CaptureLiveBaselines();
            ResolveTargetOcclusion();
            ApplyOcclusionImmediate(_targetOcclusion);
        }

        private void OnDisable()
        {
            TryUnregister();
            RestoreBaselines();
            RenderSettingsLifecycleGuard.Release(this);
        }

        private void OnDestroy()
        {
            TryUnregister();
            RestoreBaselines();
            RenderSettingsLifecycleGuard.Release(this);
        }

        /// <summary>
        /// Blends the cave ambient/reflection occlusion against the current upstream render-settings owner.
        /// </summary>
        public void Tick(float deltaTime)
        {
            RebaseIfUpstreamChanged();

            float nextOcclusion = Mathf.MoveTowards(
                _appliedOcclusion,
                _targetOcclusion,
                Mathf.Max(0.01f, occlusionBlendRate) * Mathf.Max(0f, deltaTime));

            if (Mathf.Abs(nextOcclusion - _appliedOcclusion) > BaselineEpsilon)
            {
                _appliedOcclusion = nextOcclusion;
                ApplyCurrentOcclusion();
            }

            _debugAppliedOcclusion = _appliedOcclusion;
        }

        /// <summary>
        /// Re-evaluates the cave volumes and viewer ownership at SlowTick cadence.
        /// </summary>
        public void SlowTick()
        {
            TryResolveViewerReferences();
            RefreshVolumeCache();
            ResolveTargetOcclusion();
        }

        private void TryRegister()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredUpdatable)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
                _registeredUpdatable = GlobalRegistry.Updatables.Contains(this);
            }

            if (_registeredSlowTickable)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _registeredSlowTickable = GlobalRegistry.SlowTickables.Contains(this);
        }

        private void TryUnregister()
        {
            if (_registeredUpdatable)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredUpdatable = false;
            }

            if (!_registeredSlowTickable)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registeredSlowTickable = false;
        }

        private void TryResolveViewerReferences()
        {
            if (viewerTransform == null)
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref viewerTransform);

            if (viewerCamera == null)
            {
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                if (playerContext != null && playerContext.PlayerCamera != null)
                    viewerCamera = playerContext.PlayerCamera;
            }

            if (viewerCamera == null && viewerTransform != null)
                viewerCamera = viewerTransform.GetComponent<Camera>();

            if (viewerTransform == null && viewerCamera != null)
                viewerTransform = viewerCamera.transform;

            _debugHasViewer = viewerTransform != null || viewerCamera != null;
        }

        private void RefreshVolumeCache()
        {
            WorldRuntimeReferenceUtility.TryResolveWorldCaveDirector(ref _worldCaveDirector);
            if (_worldCaveDirector != null)
            {
                _worldCaveDirector.CollectActiveVolumes(_volumeBuffer);
                _debugVolumeCount = _volumeBuffer.Count;
                return;
            }

            _volumeBuffer.Clear();
            if (Time.unscaledTime < _nextVolumeFallbackRefreshTime)
            {
                _debugVolumeCount = 0;
                return;
            }

            _nextVolumeFallbackRefreshTime = Time.unscaledTime + VolumeFallbackRefreshIntervalSeconds;
            _debugVolumeCount = 0;
        }

        private void ResolveTargetOcclusion()
        {
            Vector3 viewerPositionWS;
            if (!TryResolveViewerPosition(out viewerPositionWS))
            {
                _targetOcclusion = 0f;
                _debugTargetOcclusion = 0f;
                _debugViewerPositionWS = Vector3.zero;
                return;
            }

            _debugViewerPositionWS = viewerPositionWS;
            float strongestOcclusion = 0f;
            int volumeCount = _volumeBuffer.Count;
            for (int volumeIndex = 0; volumeIndex < volumeCount; volumeIndex++)
            {
                HectonVoxelVolume volume = _volumeBuffer[volumeIndex];
                if (volume == null || !volume.isActiveAndEnabled)
                    continue;

                float candidateOcclusion = ResolveVolumeOcclusion(volume, viewerPositionWS);
                if (candidateOcclusion > strongestOcclusion)
                    strongestOcclusion = candidateOcclusion;
            }

            _targetOcclusion = Mathf.Clamp01(strongestOcclusion);
            _debugTargetOcclusion = _targetOcclusion;
        }

        private bool TryResolveViewerPosition(out Vector3 viewerPositionWS)
        {
            if (viewerCamera != null)
            {
                viewerPositionWS = viewerCamera.transform.position;
                return true;
            }

            if (viewerTransform != null)
            {
                viewerPositionWS = viewerTransform.position;
                return true;
            }

            viewerPositionWS = Vector3.zero;
            return false;
        }

        private float ResolveVolumeOcclusion(HectonVoxelVolume volume, Vector3 viewerPositionWS)
        {
            if (!CaveRuntimeBoundsUtility.TryResolveLocalVolumeBounds(volume, volume.preset, out Bounds localBounds))
                return 0f;

            Transform volumeTransform = volume.transform;
            Vector3 localViewerPosition = volumeTransform.InverseTransformPoint(viewerPositionWS);
            if (!localBounds.Contains(localViewerPosition))
                return 0f;

            Vector3 min = localBounds.min;
            Vector3 max = localBounds.max;
            float distanceToWall = Mathf.Min(
                Mathf.Min(localViewerPosition.x - min.x, max.x - localViewerPosition.x),
                Mathf.Min(
                    Mathf.Min(localViewerPosition.y - min.y, max.y - localViewerPosition.y),
                    Mathf.Min(localViewerPosition.z - min.z, max.z - localViewerPosition.z)));

            if (!float.IsFinite(distanceToWall) || distanceToWall <= 0f)
                return 0f;

            return Mathf.Clamp01(distanceToWall / Mathf.Max(0.5f, fullOcclusionInteriorDepth));
        }

        private void CaptureLiveBaselines()
        {
            _sourceAmbientIntensity = RenderSettings.ambientIntensity;
            _sourceReflectionIntensity = RenderSettings.reflectionIntensity;
            _lastAppliedAmbientIntensity = _sourceAmbientIntensity;
            _lastAppliedReflectionIntensity = _sourceReflectionIntensity;
        }

        private void RebaseIfUpstreamChanged()
        {
            float liveAmbientIntensity = RenderSettings.ambientIntensity;
            if (Mathf.Abs(liveAmbientIntensity - _lastAppliedAmbientIntensity) > BaselineEpsilon)
                _sourceAmbientIntensity = liveAmbientIntensity;

            float liveReflectionIntensity = RenderSettings.reflectionIntensity;
            if (Mathf.Abs(liveReflectionIntensity - _lastAppliedReflectionIntensity) > BaselineEpsilon)
                _sourceReflectionIntensity = liveReflectionIntensity;
        }

        private void ApplyOcclusionImmediate(float occlusion)
        {
            _appliedOcclusion = Mathf.Clamp01(occlusion);
            ApplyCurrentOcclusion();
            _debugAppliedOcclusion = _appliedOcclusion;
        }

        private void ApplyCurrentOcclusion()
        {
            float ambientScale = Mathf.Lerp(1f, caveAmbientIntensityScale, _appliedOcclusion);
            float reflectionScale = Mathf.Lerp(1f, caveReflectionIntensityScale, _appliedOcclusion);

            float ambientIntensity = Mathf.Max(0f, _sourceAmbientIntensity * ambientScale);
            float reflectionIntensity = Mathf.Max(0f, _sourceReflectionIntensity * reflectionScale);

            if (Mathf.Abs(RenderSettings.ambientIntensity - ambientIntensity) > BaselineEpsilon)
                RenderSettings.ambientIntensity = ambientIntensity;

            if (Mathf.Abs(RenderSettings.reflectionIntensity - reflectionIntensity) > BaselineEpsilon)
                RenderSettings.reflectionIntensity = reflectionIntensity;

            _lastAppliedAmbientIntensity = ambientIntensity;
            _lastAppliedReflectionIntensity = reflectionIntensity;
        }

        private void RestoreBaselines()
        {
            if (Mathf.Abs(RenderSettings.ambientIntensity - _sourceAmbientIntensity) > BaselineEpsilon)
                RenderSettings.ambientIntensity = _sourceAmbientIntensity;

            if (Mathf.Abs(RenderSettings.reflectionIntensity - _sourceReflectionIntensity) > BaselineEpsilon)
                RenderSettings.reflectionIntensity = _sourceReflectionIntensity;

            _appliedOcclusion = 0f;
            _targetOcclusion = 0f;
        }
    }
}

using Hecton8.Core;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Maintains a per-label TMP material instance and sharpness profile for world-space HUD text.
    /// CanvasRenderer does not expose MaterialPropertyBlock, so SDF tuning must occur on a dedicated material instance.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldSpaceTMPSharpnessController : MonoBehaviour, ILateFrameTickable
    {
        private static readonly int FaceDilateId = Shader.PropertyToID("_FaceDilate");
        private static readonly int OutlineSoftnessId = Shader.PropertyToID("_OutlineSoftness");

        [Header("── Sharpness ──────────────────")]
        [SerializeField, Tooltip("Distance where the near-field SDF profile is fully applied.")]
        private float nearDistance = 0.06f;

        [SerializeField, Tooltip("Distance where the far-field SDF profile is fully applied.")]
        private float farDistance = 3.5f;

        [SerializeField, Tooltip("Near-field face dilate used when the visor text is close to the camera.")]
        private float nearFaceDilate = 0.18f;

        [SerializeField, Tooltip("Far-field face dilate used when the text plane moves deeper into the frustum.")]
        private float farFaceDilate = 0.06f;

        [SerializeField, Tooltip("Near-field outline softness used for visor-sharp text.")]
        private float nearOutlineSoftness = 0.02f;

        [SerializeField, Tooltip("Far-field outline softness used when the text drifts away from the eye.")]
        private float farOutlineSoftness = 0.12f;

        [SerializeField, Range(0.02f, 0.5f), Tooltip("Seconds between SDF sharpness updates. Material padding writes are intentionally not per-frame.")]
        private float updateIntervalSeconds = 0.1f;

        private TMP_Text _target;
        private Transform _targetTransform;
        private Camera _camera;
        private Transform _cameraTransform;
        private Material _materialInstance;
        private Material _sourceMaterial;
        private bool _registered;
        private float _lastFaceDilate = float.MinValue;
        private float _lastOutlineSoftness = float.MinValue;
        private float _nearDistanceSq = 0.0036f;
        private float _farDistanceSq = 12.25f;
        private float _sharpnessUpdateRemaining;
        private bool _distanceCacheDirty = true;

        /// <summary>
        /// Binds the sharpness owner to a world-space TMP label and optional camera.
        /// </summary>
        public void Bind(TMP_Text target, Camera camera)
        {
            if (ReferenceEquals(_target, target) && ReferenceEquals(_camera, camera))
                return;

            if (!ReferenceEquals(_target, target))
                ReleaseMaterialInstance();

            _target = target;
            _targetTransform = target != null ? target.transform : null;
            _camera = camera;
            _cameraTransform = camera != null ? camera.transform : null;
            _distanceCacheDirty = true;

            if (_target == null)
            {
                UnregisterFromTickManager();
                return;
            }

            RegisterToTickManager();
            EnsureMaterialInstance();
            ApplySharpness(force: true);
        }

        private void OnEnable()
        {
            if (_target == null)
                return;

            RegisterToTickManager();
            _targetTransform = _target != null ? _target.transform : _targetTransform;
            _cameraTransform = _camera != null ? _camera.transform : _cameraTransform;
            _distanceCacheDirty = true;
            EnsureMaterialInstance();
            ApplySharpness(force: true);
        }

        private void OnDisable()
        {
            UnregisterFromTickManager();
            ReleaseMaterialInstance();
            _sharpnessUpdateRemaining = 0f;
        }

        private void OnDestroy()
        {
            UnregisterFromTickManager();
            ReleaseMaterialInstance();
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            ApplySharpness(force: false, deltaTime: SystemDispatcher.CurrentFrameDeltaTime);
        }

        private void ApplySharpness(bool force, float deltaTime = 0f)
        {
            if (_target == null)
                return;

            if (!force && Application.isPlaying)
            {
                float safeDeltaTime = SanitizeDeltaTime(deltaTime);
                if (_sharpnessUpdateRemaining > 0f)
                {
                    _sharpnessUpdateRemaining = math.max(0f, _sharpnessUpdateRemaining - safeDeltaTime);
                    if (_sharpnessUpdateRemaining > 0f)
                        return;
                }

                _sharpnessUpdateRemaining = ResolveUpdateInterval();
            }

            EnsureMaterialInstance();
            if (_materialInstance == null)
                return;

            Camera resolvedCamera = ResolveCamera();
            if (resolvedCamera == null)
                return;

            _targetTransform = _targetTransform != null ? _targetTransform : _target.transform;
            _cameraTransform = _cameraTransform != null ? _cameraTransform : resolvedCamera.transform;
            if (_targetTransform == null || _cameraTransform == null)
                return;

            RefreshDistanceCacheIfDirty();
            float3 cameraToTarget = (float3)(_targetTransform.position - _cameraTransform.position);
            float distanceSq = math.lengthsq(cameraToTarget);
            if (!math.isfinite(distanceSq))
                return;

            float distanceT = math.saturate((distanceSq - _nearDistanceSq) / math.max(0.001f, _farDistanceSq - _nearDistanceSq));
            float faceDilate = math.lerp(nearFaceDilate, farFaceDilate, distanceT);
            float outlineSoftness = math.lerp(nearOutlineSoftness, farOutlineSoftness, distanceT);
            if (!force &&
                math.abs(faceDilate - _lastFaceDilate) <= 0.0001f &&
                math.abs(outlineSoftness - _lastOutlineSoftness) <= 0.0001f)
            {
                return;
            }

            _materialInstance.SetFloat(FaceDilateId, faceDilate);
            _materialInstance.SetFloat(OutlineSoftnessId, outlineSoftness);
            _target.UpdateMeshPadding();
            _lastFaceDilate = faceDilate;
            _lastOutlineSoftness = outlineSoftness;
        }

        private Camera ResolveCamera()
        {
            if (_camera != null && _camera.isActiveAndEnabled)
                return _camera;

            if (_camera != null && !_camera.isActiveAndEnabled)
            {
                _camera = null;
                _cameraTransform = null;
            }

            SuitHUDV4CanvasOverlay overlay = SuitHUDV4CanvasOverlay.ActiveRuntimeInstance;
            Camera projectionCamera = overlay != null ? overlay.ProjectionCamera : null;
            if (projectionCamera != null && projectionCamera.isActiveAndEnabled)
            {
                _camera = projectionCamera;
                _cameraTransform = _camera.transform;
                return _camera;
            }

            return null;
        }

        private void EnsureMaterialInstance()
        {
            if (_target == null)
                return;

            Material currentMaterial = _target.fontSharedMaterial;
            Material baseMaterial =
                ReferenceEquals(currentMaterial, _materialInstance) && _sourceMaterial != null
                    ? _sourceMaterial
                    : currentMaterial;
            if (baseMaterial == null)
                return;

            if (_materialInstance != null && ReferenceEquals(_sourceMaterial, baseMaterial))
            {
                if (!ReferenceEquals(_target.fontSharedMaterial, _materialInstance))
                    _target.fontSharedMaterial = _materialInstance;
                return;
            }

            Material previousSourceMaterial = _sourceMaterial;
            _sourceMaterial = baseMaterial;

            if (_materialInstance != null)
            {
                if (_target != null &&
                    previousSourceMaterial != null &&
                    ReferenceEquals(_target.fontSharedMaterial, _materialInstance))
                {
                    _target.fontSharedMaterial = previousSourceMaterial;
                }

                Destroy(_materialInstance);
            }

            _materialInstance = new Material(baseMaterial); // COLD ALLOC: Material[1] — per-label TMP SDF sharpness material — owner: WorldSpaceTMPSharpnessController
            _materialInstance.hideFlags = HideFlags.DontSave;
            _target.fontSharedMaterial = _materialInstance;
            _targetTransform = _target.transform;
            _lastFaceDilate = float.MinValue;
            _lastOutlineSoftness = float.MinValue;
        }

        private void ReleaseMaterialInstance()
        {
            if (_materialInstance == null)
                return;

            if (_target != null &&
                _sourceMaterial != null &&
                ReferenceEquals(_target.fontSharedMaterial, _materialInstance))
            {
                _target.fontSharedMaterial = _sourceMaterial;
            }

            Destroy(_materialInstance);
            _materialInstance = null;
            _sourceMaterial = null;
            _lastFaceDilate = float.MinValue;
            _lastOutlineSoftness = float.MinValue;
        }

        private void RefreshDistanceCacheIfDirty()
        {
            if (!_distanceCacheDirty)
                return;

            float near = math.max(0.001f, nearDistance);
            float far = math.max(near + 0.001f, farDistance);
            _nearDistanceSq = near * near;
            _farDistanceSq = far * far;
            _distanceCacheDirty = false;
        }

        private float ResolveUpdateInterval()
        {
            return math.isfinite(updateIntervalSeconds)
                ? math.clamp(updateIntervalSeconds, 0.02f, 0.5f)
                : 0.1f;
        }

        private static float SanitizeDeltaTime(float deltaTime)
        {
            return math.isfinite(deltaTime) ? math.clamp(deltaTime, 0f, 0.5f) : 0f;
        }

        private void RegisterToTickManager()
        {
            if (_registered || _target == null || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void UnregisterFromTickManager()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
            _registered = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            updateIntervalSeconds = ResolveUpdateInterval();
            _distanceCacheDirty = true;
        }
#endif
    }
}

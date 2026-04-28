using Hecton8.Core;
using TMPro;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Maintains a per-label TMP material instance and sharpness profile for world-space HUD text.
    /// CanvasRenderer does not expose MaterialPropertyBlock, so SDF tuning must occur on a dedicated material instance.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldSpaceTMPSharpnessController : MonoBehaviour, ITickable, IUpdatable
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

        private TMP_Text _target;
        private Camera _camera;
        private Material _materialInstance;
        private Material _sourceMaterial;
        private bool _registered;
        private float _lastFaceDilate = float.MinValue;
        private float _lastOutlineSoftness = float.MinValue;

        /// <summary>
        /// Binds the sharpness owner to a world-space TMP label and optional camera.
        /// </summary>
        public void Bind(TMP_Text target, Camera camera)
        {
            if (ReferenceEquals(_target, target) && ReferenceEquals(_camera, camera))
                return;

            _target = target;
            _camera = camera;
            EnsureMaterialInstance();
            ApplySharpness(force: true);
        }

        private void OnEnable()
        {
            RegisterToTickManager();
            EnsureMaterialInstance();
            ApplySharpness(force: true);
        }

        private void OnDisable()
        {
            UnregisterFromTickManager();
            ReleaseMaterialInstance();
        }

        private void OnDestroy()
        {
            UnregisterFromTickManager();
            ReleaseMaterialInstance();
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            ApplySharpness(force: false);
        }

        private void ApplySharpness(bool force)
        {
            if (_target == null)
                return;

            EnsureMaterialInstance();
            if (_materialInstance == null)
                return;

            Camera resolvedCamera = ResolveCamera();
            if (resolvedCamera == null)
                return;

            RectTransform rectTransform = _target.transform as RectTransform;
            if (rectTransform == null)
                return;

            float distance = Vector3.Distance(resolvedCamera.transform.position, rectTransform.position);
            float distanceT = Mathf.InverseLerp(Mathf.Max(0.001f, nearDistance), Mathf.Max(nearDistance + 0.001f, farDistance), distance);
            float faceDilate = Mathf.Lerp(nearFaceDilate, farFaceDilate, distanceT);
            float outlineSoftness = Mathf.Lerp(nearOutlineSoftness, farOutlineSoftness, distanceT);
            if (!force &&
                Mathf.Approximately(faceDilate, _lastFaceDilate) &&
                Mathf.Approximately(outlineSoftness, _lastOutlineSoftness))
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
            if (_camera != null)
                return _camera;

            SuitHUDV4CanvasOverlay overlay = SuitHUDV4CanvasOverlay.ActiveRuntimeInstance;
            if (overlay != null && overlay.ProjectionCamera != null)
            {
                _camera = overlay.ProjectionCamera;
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
            _materialInstance.name = string.Concat(baseMaterial.name, " (WorldSpaceSharpness)");
            _target.fontSharedMaterial = _materialInstance;
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

        private void RegisterToTickManager()
        {
            if (_registered || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registered = true;
        }

        private void UnregisterFromTickManager()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registered = false;
        }
    }
}

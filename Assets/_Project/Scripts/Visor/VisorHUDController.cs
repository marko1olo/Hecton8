using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NASAPunk.Visor
{
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class VisorHUDController : MonoBehaviour
    {
        public enum ProjectionMode
        {
            Disabled,
            SharedRenderTexture,
            RuntimeRenderTexture
        }

        [Header("References")]
        [SerializeField] private Renderer _visorRenderer;
        [SerializeField] private Camera _hudCamera;
        [SerializeField] private Camera _baseStackCamera;
        [SerializeField] private Camera _referenceCamera;
        [SerializeField] private RenderTexture _sharedRenderTexture;

        [Header("Projection")]
        [SerializeField] private ProjectionMode _projectionMode = ProjectionMode.Disabled;

        [Header("Runtime Render Texture Settings")]
        [SerializeField] private int _rtWidth = 1920;
        [SerializeField] private int _rtHeight = 1080;
        [SerializeField] private FilterMode _filterMode = FilterMode.Bilinear;

        [Header("Runtime Tuning")]
        [SerializeField, Range(0f, 5f)] private float _hudIntensity = 2.5f;
        [SerializeField] private Color _hudTint = new Color(0.2f, 1f, 0.3f, 1f);
        [SerializeField, Range(0f, 2f)] private float _scratchBleed = 0.8f;
        [SerializeField, Range(0f, 0.1f)] private float _distortion = 0.02f;
        [SerializeField] private bool _manualProjectionRender = true;
        [SerializeField] private bool _previewInEditMode = true;

        [Header("Pose Lock")]
        [SerializeField] private bool _syncToReferenceCamera = true;
        [SerializeField] private bool _syncPoseInEditMode = false;
        [SerializeField] private Vector3 _visorLocalOffset = new Vector3(0f, 0f, 0.3f);
        [SerializeField] private Vector3 _visorLocalEulerOffset = Vector3.zero;
        [SerializeField] private Vector3 _visorLocalScale = new Vector3(1f, 1f, 0.6f);
        [SerializeField] private Vector3 _hudCameraLocalOffset = Vector3.zero;
        [SerializeField] private Vector3 _hudCameraLocalEulerOffset = Vector3.zero;
        [SerializeField] private float _minimumVisorForwardOffset = 0.02f;
        [SerializeField] private bool _enforceNearClipSafeOffset = false;

        private RenderTexture _hudRT;
        private MaterialPropertyBlock _mpb;
        private bool _ownsRuntimeTexture;
        private bool _isRenderingProjection;

        private static readonly int ID_HUDTex = Shader.PropertyToID("_HUD_RenderTexture");
        private static readonly int ID_HUDIntensity = Shader.PropertyToID("_HUD_Intensity");
        private static readonly int ID_HUDColor = Shader.PropertyToID("_HUD_Color");
        private static readonly int ID_ScratchBleed = Shader.PropertyToID("_HUD_ScratchBleed");
        private static readonly int ID_Distortion = Shader.PropertyToID("_DistortionStrength");

        private void OnEnable()
        {
            EnsurePropertyBlock();
            AutoResolveReferences();
            SyncProjectionPose();
            RebuildProjection();
        }

        private void OnDisable()
        {
            ReleaseRT();
        }

        private void Update()
        {
            AutoResolveReferences();
            SyncProjectionPose();

            if (_visorRenderer == null) return;

            _visorRenderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(ID_HUDIntensity, _hudIntensity);
            _mpb.SetColor(ID_HUDColor, _hudTint);
            _mpb.SetFloat(ID_ScratchBleed, _scratchBleed);
            _mpb.SetFloat(ID_Distortion, _distortion);
            _visorRenderer.SetPropertyBlock(_mpb);
        }

        private void LateUpdate()
        {
            if (_manualProjectionRender &&
                _projectionMode != ProjectionMode.Disabled &&
                _hudCamera != null &&
                _hudRT != null &&
                (Application.isPlaying || _previewInEditMode))
            {
                RenderProjectionCamera();
            }
        }

        private void OnValidate()
        {
            EnsurePropertyBlock();
            AutoResolveReferences();
            SyncProjectionPose();

            if (!isActiveAndEnabled)
                return;

            RebuildProjection();
        }

        private void AutoResolveReferences()
        {
            if (_visorRenderer == null)
                _visorRenderer = GetComponent<Renderer>();

            if (_hudCamera == null)
            {
                Transform parent = transform.parent;
                if (parent != null)
                {
                    Transform cameraTransform = parent.Find("HUD_Render_Camera");
                    if (cameraTransform != null)
                        _hudCamera = cameraTransform.GetComponent<Camera>();
                }
            }

            if (_baseStackCamera == null)
            {
                Transform parent = transform.parent;
                if (parent != null)
                {
                    Transform mainCameraTransform = parent.Find("Main Camera");
                    if (mainCameraTransform != null)
                    {
                        Transform spaceCameraTransform = mainCameraTransform.Find("SpaceCamera");
                        if (spaceCameraTransform != null)
                            _baseStackCamera = spaceCameraTransform.GetComponent<Camera>();
                    }
                }
            }

            if (_referenceCamera == null)
            {
                Transform parent = transform.parent;
                if (parent != null)
                {
                    Transform mainCameraTransform = parent.Find("Main Camera");
                    if (mainCameraTransform != null)
                        _referenceCamera = mainCameraTransform.GetComponent<Camera>();
                    else
                        _referenceCamera = parent.GetComponent<Camera>();
                }

                if (_referenceCamera == null && _baseStackCamera != null)
                {
                    Transform baseParent = _baseStackCamera.transform.parent;
                    if (baseParent != null)
                        _referenceCamera = baseParent.GetComponent<Camera>();
                }
            }
        }

        private void EnsurePropertyBlock()
        {
            if (_mpb == null)
                _mpb = new MaterialPropertyBlock();
        }

        private void PrepareProjectionTexture()
        {
            _ownsRuntimeTexture = false;
            _hudRT = null;

            if (_projectionMode == ProjectionMode.Disabled)
                return;

            if (_projectionMode == ProjectionMode.SharedRenderTexture && _sharedRenderTexture != null)
            {
                _hudRT = _sharedRenderTexture;
                return;
            }

            _hudRT = new RenderTexture(_rtWidth, _rtHeight, 0, RenderTextureFormat.ARGB32)
            {
                filterMode = _filterMode,
                useMipMap = false,
                name = "VisorHUD_RT"
            };
            _hudRT.Create();
            _ownsRuntimeTexture = true;
        }

        private void RebuildProjection()
        {
            ReleaseRT();
            PrepareProjectionTexture();
            SyncCameraRole();
            BindRT();
        }

        private void BindRT()
        {
            EnsurePropertyBlock();

            if (_hudCamera != null)
            {
                _hudCamera.targetTexture = _projectionMode == ProjectionMode.Disabled ? null : _hudRT;
            }

            if (_visorRenderer != null)
            {
                _visorRenderer.GetPropertyBlock(_mpb);
                if (_hudRT != null)
                    _mpb.SetTexture(ID_HUDTex, _hudRT);
                else
                    _mpb.SetTexture(ID_HUDTex, Texture2D.blackTexture);
                _visorRenderer.SetPropertyBlock(_mpb);
            }
        }

        private void ReleaseRT()
        {
            if (_hudCamera != null)
            {
                _hudCamera.targetTexture = null;
                _hudCamera.enabled = true;
            }

            if (_ownsRuntimeTexture && _hudRT != null)
            {
                _hudRT.Release();
                DestroyImmediate(_hudRT);
            }

            _hudRT = null;
            _ownsRuntimeTexture = false;
        }

        private void SyncCameraRole()
        {
            if (_hudCamera == null)
                return;

            UniversalAdditionalCameraData hudCameraData = _hudCamera.GetComponent<UniversalAdditionalCameraData>();
            if (hudCameraData == null)
                return;

            UniversalAdditionalCameraData baseCameraData = _baseStackCamera != null
                ? _baseStackCamera.GetComponent<UniversalAdditionalCameraData>()
                : null;

            bool projected = _projectionMode != ProjectionMode.Disabled;

            if (projected)
            {
                hudCameraData.renderType = CameraRenderType.Base;

                if (baseCameraData != null && baseCameraData.cameraStack.Contains(_hudCamera))
                    baseCameraData.cameraStack.Remove(_hudCamera);

                _hudCamera.clearFlags = CameraClearFlags.SolidColor;
                _hudCamera.enabled = !_manualProjectionRender;
                Color color = _hudCamera.backgroundColor;
                color.a = 0f;
                _hudCamera.backgroundColor = color;
                return;
            }

            hudCameraData.renderType = CameraRenderType.Overlay;

            if (baseCameraData != null && !baseCameraData.cameraStack.Contains(_hudCamera))
                baseCameraData.cameraStack.Add(_hudCamera);

            _hudCamera.clearFlags = CameraClearFlags.Depth;
            _hudCamera.enabled = true;
        }

        private void SyncProjectionPose()
        {
            if (!_syncToReferenceCamera || _referenceCamera == null)
                return;
            if (!Application.isPlaying && !_syncPoseInEditMode)
                return;

            Transform referenceTransform = _referenceCamera.transform;
            Vector3 visorOffset = _visorLocalOffset;
            visorOffset.z = Mathf.Max(visorOffset.z, _minimumVisorForwardOffset);
            if (_enforceNearClipSafeOffset)
            {
                float nearClipSafeOffset = _referenceCamera.nearClipPlane + 0.12f;
                visorOffset.z = Mathf.Max(visorOffset.z, nearClipSafeOffset);
            }
            Quaternion visorRotation = referenceTransform.rotation * Quaternion.Euler(_visorLocalEulerOffset);

            transform.SetPositionAndRotation(
                referenceTransform.TransformPoint(visorOffset),
                visorRotation);
            transform.localScale = _visorLocalScale;

            if (_hudCamera != null)
            {
                Transform hudTransform = _hudCamera.transform;
                Quaternion hudRotation = referenceTransform.rotation * Quaternion.Euler(_hudCameraLocalEulerOffset);
                hudTransform.SetPositionAndRotation(
                    referenceTransform.TransformPoint(_hudCameraLocalOffset),
                    hudRotation);
            }
        }

        private void RenderProjectionCamera()
        {
            if (_isRenderingProjection || _hudCamera == null)
                return;

            try
            {
                _isRenderingProjection = true;
                ClearProjectionTarget();
                _hudCamera.Render();
            }
            finally
            {
                _isRenderingProjection = false;
            }
        }

        private void ClearProjectionTarget()
        {
            if (_hudRT == null)
                return;

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = _hudRT;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = previous;
        }

        public void SetHUDIntensity(float intensity)
        {
            _hudIntensity = Mathf.Clamp(intensity, 0f, 5f);
        }

        public void SetProjectionMode(ProjectionMode projectionMode)
        {
            if (_projectionMode == projectionMode)
                return;

            _projectionMode = projectionMode;
            RebuildProjection();
        }

        public void SetSharedRenderTexture(RenderTexture sharedRenderTexture)
        {
            if (_sharedRenderTexture == sharedRenderTexture)
                return;

            _sharedRenderTexture = sharedRenderTexture;

            if (_projectionMode == ProjectionMode.SharedRenderTexture)
                RebuildProjection();
        }

        public void GlitchPulse(float duration = 0.3f)
        {
            StartCoroutine(GlitchCoroutine(duration));
        }

        private System.Collections.IEnumerator GlitchCoroutine(float dur)
        {
            float original = _hudIntensity;
            float elapsed = 0;
            while (elapsed < dur)
            {
                _hudIntensity = original * Random.Range(0.1f, 2f);
                elapsed += Time.deltaTime;
                yield return null;
            }
            _hudIntensity = original;
        }
    }
}

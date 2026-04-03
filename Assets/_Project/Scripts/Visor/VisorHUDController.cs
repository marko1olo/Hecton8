using System.Collections.Generic;
using Hecton8.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NASAPunk.Visor
{
    /// <summary>
    /// Drives the visor HUD projection material and optional runtime render texture.
    /// Runtime refresh runs through <see cref="GameTickManager"/> while edit-mode preview
    /// stays on <see cref="Update"/> so the inspector workflow remains intact.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class VisorHUDController : MonoBehaviour, ITickable
    {
        private static readonly List<VisorHUDController> s_activeControllers = new List<VisorHUDController>(2);

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

        private const float AutoResolveRetryInterval = 1f;

        private RenderTexture _hudRT;
        private MaterialPropertyBlock _mpb;
        private bool _ownsRuntimeTexture;
        private float _nextAutoResolveAt;

        private bool _glitchActive;
        private float _glitchTimer;
        private float _glitchDuration;
        private float _glitchOriginalIntensity;
        private bool _runtimeTickRegistered;

        private uint _glitchRngState = 1u;

        private static readonly int ID_HUDTex = Shader.PropertyToID("_HUD_RenderTexture");
        private static readonly int ID_HUDIntensity = Shader.PropertyToID("_HUD_Intensity");
        private static readonly int ID_HUDColor = Shader.PropertyToID("_HUD_Color");
        private static readonly int ID_ScratchBleed = Shader.PropertyToID("_HUD_ScratchBleed");
        private static readonly int ID_Distortion = Shader.PropertyToID("_DistortionStrength");

        public Camera HudCamera => _hudCamera;
        public RenderTexture SharedRenderTexture => _sharedRenderTexture;

        public static void CopyActiveControllersTo(List<VisorHUDController> results)
        {
            if (results == null)
                return;

            results.Clear();
            for (int i = 0; i < s_activeControllers.Count; i++)
            {
                VisorHUDController controller = s_activeControllers[i];
                if (controller != null && controller.isActiveAndEnabled)
                    results.Add(controller);
            }
        }

        private void OnEnable()
        {
            RegisterActiveController();
            EnsurePropertyBlock();
            AutoResolveReferences(force: true);
            SyncProjectionPose();
            RebuildProjection();
            TryRegisterRuntimeTick();
        }

        private void OnDisable()
        {
            UnregisterActiveController();

            if (_glitchActive)
            {
                _hudIntensity = _glitchOriginalIntensity;
                _glitchActive = false;
            }

            UnregisterRuntimeTick();
            ReleaseRT();
        }

        private void Update()
        {
            if (Application.isPlaying)
            {
                TryRegisterRuntimeTick();
                return;
            }

            if (!_previewInEditMode)
                return;

            RefreshRuntimeState(forceResolve: false);
        }

        private void OnValidate()
        {
            EnsurePropertyBlock();
            AutoResolveReferences(force: true);
            SyncProjectionPose();

            if (!isActiveAndEnabled)
                return;

            RebuildProjection();
        }

        public void Tick(float deltaTime)
        {
            AutoResolveReferences(force: false);
            SyncProjectionPose();
            UpdateGlitchState(deltaTime);
            ApplyMaterialProperties();
        }

        /// <summary>
        /// xorshift32 based zero-GC pseudo-random in [0, 1).
        /// </summary>
        private float XorShift01()
        {
            _glitchRngState ^= _glitchRngState << 13;
            _glitchRngState ^= _glitchRngState >> 17;
            _glitchRngState ^= _glitchRngState << 5;
            return (_glitchRngState & 0x7FFFFF) / (float)0x800000;
        }

        private void TryRegisterRuntimeTick()
        {
            if (!Application.isPlaying || _runtimeTickRegistered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            tickManager.Register(this);
            _runtimeTickRegistered = true;
        }

        private void UnregisterRuntimeTick()
        {
            if (!_runtimeTickRegistered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
                tickManager.Unregister(this);

            _runtimeTickRegistered = false;
        }

        private void RegisterActiveController()
        {
            if (s_activeControllers.Contains(this))
                return;

            s_activeControllers.Add(this);
        }

        private void UnregisterActiveController()
        {
            s_activeControllers.Remove(this);
        }

        private void AutoResolveReferences(bool force)
        {
            if (!force && !NeedsAutoResolve())
                return;

            float now = GetAutoResolveNow();
            if (!force && now < _nextAutoResolveAt)
                return;

            _nextAutoResolveAt = now + AutoResolveRetryInterval;

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

        private bool NeedsAutoResolve()
        {
            bool needsBaseStackCamera = _projectionMode != ProjectionMode.Disabled && _baseStackCamera == null;
            bool needsReferenceCamera = _syncToReferenceCamera && _referenceCamera == null;
            bool needsHudCamera = _projectionMode != ProjectionMode.Disabled && _hudCamera == null;

            return _visorRenderer == null
                || needsHudCamera
                || needsBaseStackCamera
                || needsReferenceCamera;
        }

        private static float GetAutoResolveNow()
        {
            return Application.isPlaying ? Time.unscaledTime : Time.realtimeSinceStartup;
        }

        private void EnsurePropertyBlock()
        {
            if (_mpb == null)
                _mpb = new MaterialPropertyBlock();
        }

        private void RefreshRuntimeState(bool forceResolve)
        {
            AutoResolveReferences(forceResolve);
            SyncProjectionPose();
            ApplyMaterialProperties();
        }

        private void ApplyMaterialProperties()
        {
            if (_visorRenderer == null)
                return;

            EnsurePropertyBlock();

            _visorRenderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(ID_HUDIntensity, _hudIntensity);
            _mpb.SetColor(ID_HUDColor, _hudTint);
            _mpb.SetFloat(ID_ScratchBleed, _scratchBleed);
            _mpb.SetFloat(ID_Distortion, _distortion);
            _visorRenderer.SetPropertyBlock(_mpb);
        }

        private void UpdateGlitchState(float deltaTime)
        {
            if (!_glitchActive)
                return;

            _glitchTimer += deltaTime;

            if (_glitchTimer >= _glitchDuration)
            {
                _hudIntensity = _glitchOriginalIntensity;
                _glitchActive = false;
                return;
            }

            float rand01 = XorShift01();
            _hudIntensity = _glitchOriginalIntensity * (0.1f + rand01 * 1.9f);
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
                _hudCamera.targetTexture = _projectionMode == ProjectionMode.Disabled ? null : _hudRT;

            if (_visorRenderer == null)
                return;

            _visorRenderer.GetPropertyBlock(_mpb);
            _mpb.SetTexture(ID_HUDTex, _hudRT != null ? _hudRT : Texture2D.blackTexture);
            _visorRenderer.SetPropertyBlock(_mpb);
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
                if (Application.isPlaying)
                    Destroy(_hudRT);
                else
                    DestroyImmediate(_hudRT);
            }

            _hudRT = null;
            _ownsRuntimeTexture = false;
        }

        /// <summary>
        /// Configures the HUD camera so projection rendering stays inside the URP pipeline.
        /// </summary>
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
                Color color = _hudCamera.backgroundColor;
                color.a = 0f;
                _hudCamera.backgroundColor = color;
                _hudCamera.enabled = true;
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

            if (_hudCamera == null)
                return;

            Transform hudTransform = _hudCamera.transform;
            Quaternion hudRotation = referenceTransform.rotation * Quaternion.Euler(_hudCameraLocalEulerOffset);
            hudTransform.SetPositionAndRotation(
                referenceTransform.TransformPoint(_hudCameraLocalOffset),
                hudRotation);
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

        /// <summary>
        /// Starts a deterministic glitch pulse without coroutines or heap allocations.
        /// </summary>
        public void GlitchPulse(float duration = 0.3f)
        {
            if (!_glitchActive)
                _glitchOriginalIntensity = _hudIntensity;

            _glitchActive = true;
            _glitchTimer = 0f;
            _glitchDuration = duration;
            _glitchRngState = (uint)(Time.unscaledTime * 1000f) | 1u;
        }
    }
}

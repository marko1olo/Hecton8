using System.Collections.Generic;
using Hecton8.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NASAPunk.Visor
{
    /// <summary>
    /// Drives the visor HUD projection material and optional runtime render texture.
    /// Runtime refresh runs through <see cref="GameTickManager"/> while edit-mode preview
    /// stays on an editor callback so play mode avoids MonoBehaviour Update polling.
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
        [SerializeField] private int _rtWidth = 1280;
        [SerializeField] private int _rtHeight = 720;
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
        private int _cachedRTWidth = -1;
        private int _cachedRTHeight = -1;
        private float _nextAutoResolveAt;
        private bool _materialPropertiesDirty = true;
        private UniversalAdditionalCameraData _cachedHudCameraData;
        private UniversalAdditionalCameraData _cachedBaseCameraData;
        private bool _poseApplied;
        private Vector3 _appliedVisorPosition;
        private Quaternion _appliedVisorRotation;
        private Vector3 _appliedVisorScale;
        private bool _hudPoseApplied;
        private Vector3 _appliedHudPosition;
        private Quaternion _appliedHudRotation;
        private Vector3 _cachedVisorEulerOffset;
        private Quaternion _cachedVisorOffsetRotation = Quaternion.identity;
        private Vector3 _cachedHudEulerOffset;
        private Quaternion _cachedHudOffsetRotation = Quaternion.identity;

        private bool _glitchActive;
        private float _glitchTimer;
        private float _glitchDuration;
        private float _glitchOriginalIntensity;
        private bool _runtimeTickRegistered;
        private bool _editorPreviewSuspended;

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
            _materialPropertiesDirty = true;
            AutoResolveReferences(force: true);
            SyncProjectionPose();
            RebuildProjection();
            TryRegisterRuntimeTick();
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (!IsEditorPreviewActive())
                    SuspendEditModeProjection();

                EvaluateEditorTickRegistration();
            }
#endif
        }

        private void Start()
        {
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
            InvalidatePoseCache();
#if UNITY_EDITOR
            EditorApplication.update -= EditorTick;
#endif
        }

#if UNITY_EDITOR
        private void EditorTick()
        {
            if (!IsEditorPreviewActive())
            {
                SuspendEditModeProjection();
                return;
            }

            if (_editorPreviewSuspended)
                ResumeEditModeProjection();

            if (!ShouldTickInEditMode())
            {
                UnregisterEditorTick();
                return;
            }

            RefreshRuntimeState(forceResolve: false);
        }
#endif

        private void OnValidate()
        {
            EnsurePropertyBlock();
            _materialPropertiesDirty = true;
            AutoResolveReferences(force: true);
            SyncProjectionPose();

            if (!isActiveAndEnabled)
                return;

            RebuildProjection();
#if UNITY_EDITOR
            if (!Application.isPlaying)
                EvaluateEditorTickRegistration();
#endif
        }

        public void Tick(float deltaTime)
        {
            AutoResolveReferences(force: false);
            SyncProjectionPose();
            UpdateGlitchState(deltaTime);
            if (_materialPropertiesDirty)
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
            _materialPropertiesDirty = false;
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
                _materialPropertiesDirty = true;
                return;
            }

            float rand01 = XorShift01();
            _hudIntensity = _glitchOriginalIntensity * (0.1f + rand01 * 1.9f);
            _materialPropertiesDirty = true;
        }

        private void PrepareProjectionTexture()
        {
            if (_projectionMode == ProjectionMode.Disabled)
            {
                ReleaseOwnedRuntimeTexture();
                _hudRT = null;
                _ownsRuntimeTexture = false;
                _cachedRTWidth = -1;
                _cachedRTHeight = -1;
                return;
            }

            if (_projectionMode == ProjectionMode.SharedRenderTexture && _sharedRenderTexture != null)
            {
                ReleaseOwnedRuntimeTexture();
                _hudRT = _sharedRenderTexture;
                _ownsRuntimeTexture = false;
                _cachedRTWidth = -1;
                _cachedRTHeight = -1;
                return;
            }

            if (!_ownsRuntimeTexture)
                _hudRT = null;

            // Reuse RT if size matches
            if (_hudRT != null && _hudRT.width == _rtWidth && _hudRT.height == _rtHeight && _hudRT.format == RenderTextureFormat.ARGB32)
            {
                _hudRT.filterMode = _filterMode;
                if (!_hudRT.IsCreated())
                    _hudRT.Create();
                _ownsRuntimeTexture = true;
                _cachedRTWidth = _rtWidth;
                _cachedRTHeight = _rtHeight;
                return;
            }

            // Release old RT if size changed
            ReleaseOwnedRuntimeTexture();

            _hudRT = new RenderTexture(_rtWidth, _rtHeight, 0, RenderTextureFormat.ARGB32)
            {
                filterMode = _filterMode,
                useMipMap = false,
                name = "VisorHUD_RT"
            };
            _hudRT.Create();
            _ownsRuntimeTexture = true;
            _cachedRTWidth = _rtWidth;
            _cachedRTHeight = _rtHeight;
        }

        private void RebuildProjection()
        {
            InvalidatePoseCache();
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
            _materialPropertiesDirty = true;
        }

        private void ReleaseRT()
        {
            if (_hudCamera != null)
            {
                _hudCamera.targetTexture = null;
                _hudCamera.enabled = true;
            }

            ReleaseOwnedRuntimeTexture();

            _hudRT = null;
            _ownsRuntimeTexture = false;
            _cachedRTWidth = -1;
            _cachedRTHeight = -1;
        }

        private void ReleaseOwnedRuntimeTexture()
        {
            if (!_ownsRuntimeTexture || _hudRT == null)
                return;

            _hudRT.Release();
            if (Application.isPlaying)
                Destroy(_hudRT);
            else
                DestroyImmediate(_hudRT);
        }

        private void SuspendEditModeProjection()
        {
            if (Application.isPlaying || _editorPreviewSuspended)
                return;

            if (_hudCamera != null)
            {
                _hudCamera.targetTexture = null;
                _hudCamera.enabled = false;
            }

            ReleaseOwnedRuntimeTexture();
            _hudRT = null;
            _ownsRuntimeTexture = false;
            _cachedRTWidth = -1;
            _cachedRTHeight = -1;

            if (_visorRenderer != null)
            {
                EnsurePropertyBlock();
                _visorRenderer.GetPropertyBlock(_mpb);
                _mpb.SetTexture(ID_HUDTex, Texture2D.blackTexture);
                _visorRenderer.SetPropertyBlock(_mpb);
            }

            _editorPreviewSuspended = true;
        }

        private void ResumeEditModeProjection()
        {
            if (Application.isPlaying || !_editorPreviewSuspended)
                return;

            _editorPreviewSuspended = false;
            _materialPropertiesDirty = true;
            RebuildProjection();
        }

        /// <summary>
        /// Configures the HUD camera so projection rendering stays inside the URP pipeline.
        /// </summary>
        private void SyncCameraRole()
        {
            if (_hudCamera == null)
                return;

            UniversalAdditionalCameraData hudCameraData = GetCachedHudCameraData();
            if (hudCameraData == null)
                return;

            UniversalAdditionalCameraData baseCameraData = EnsureValidBaseStackCamera()
                ? GetCachedBaseCameraData()
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

        private bool EnsureValidBaseStackCamera()
        {
            if (HasValidBaseStackCamera())
                return true;

            Camera resolvedCamera = TryResolveBaseStackCameraFromHierarchy();
            if (resolvedCamera == null)
                return false;

            if (_baseStackCamera != resolvedCamera)
            {
                _baseStackCamera = resolvedCamera;
                _cachedBaseCameraData = null;
            }

            return HasValidBaseStackCamera();
        }

        private bool HasValidBaseStackCamera()
        {
            if (_baseStackCamera == null)
                return false;

            UniversalAdditionalCameraData baseCameraData = GetCachedBaseCameraData();
            return baseCameraData != null && baseCameraData.renderType == CameraRenderType.Base;
        }

        private Camera TryResolveBaseStackCameraFromHierarchy()
        {
            Camera resolvedCamera = TryResolveBaseStackCameraFromTransform(
                _referenceCamera != null ? _referenceCamera.transform : null);
            if (resolvedCamera != null)
                return resolvedCamera;

            resolvedCamera = TryResolveBaseStackCameraFromTransform(
                _baseStackCamera != null ? _baseStackCamera.transform : null);
            if (resolvedCamera != null)
                return resolvedCamera;

            Transform parent = transform.parent;
            if (parent == null)
                return null;

            Transform mainCameraTransform = parent.Find("Main Camera");
            if (mainCameraTransform == null)
                return null;

            Transform spaceCameraTransform = mainCameraTransform.Find("SpaceCamera");
            return spaceCameraTransform != null ? spaceCameraTransform.GetComponent<Camera>() : null;
        }

        private static Camera TryResolveBaseStackCameraFromTransform(Transform sourceTransform)
        {
            if (sourceTransform == null)
                return null;

            Transform spaceCameraTransform = sourceTransform.Find("SpaceCamera");
            if (spaceCameraTransform != null)
            {
                Camera directCamera = spaceCameraTransform.GetComponent<Camera>();
                if (directCamera != null)
                    return directCamera;
            }

            Transform parent = sourceTransform.parent;
            if (parent == null)
                return null;

            Transform siblingSpaceCameraTransform = parent.Find("SpaceCamera");
            if (siblingSpaceCameraTransform == null)
                return null;

            return siblingSpaceCameraTransform.GetComponent<Camera>();
        }

        private void SyncProjectionPose()
        {
            if (!_syncToReferenceCamera || _referenceCamera == null)
                return;

            if (!Application.isPlaying && !_syncPoseInEditMode)
                return;

            Transform referenceTransform = _referenceCamera.transform;
            Vector3 referencePosition = referenceTransform.position;
            Quaternion referenceRotation = referenceTransform.rotation;
            Vector3 visorOffset = _visorLocalOffset;
            visorOffset.z = Mathf.Max(visorOffset.z, _minimumVisorForwardOffset);

            if (_enforceNearClipSafeOffset)
            {
                float nearClipSafeOffset = _referenceCamera.nearClipPlane + 0.12f;
                visorOffset.z = Mathf.Max(visorOffset.z, nearClipSafeOffset);
            }

            Quaternion visorRotation = referenceRotation * GetCachedVisorOffsetRotation();
            Vector3 visorPosition = referencePosition + referenceRotation * visorOffset;
            if (!_poseApplied || _appliedVisorPosition != visorPosition || _appliedVisorRotation != visorRotation)
            {
                transform.SetPositionAndRotation(visorPosition, visorRotation);
                _appliedVisorPosition = visorPosition;
                _appliedVisorRotation = visorRotation;
                _poseApplied = true;
            }

            if (_appliedVisorScale != _visorLocalScale)
            {
                transform.localScale = _visorLocalScale;
                _appliedVisorScale = _visorLocalScale;
            }

            if (_hudCamera == null)
                return;

            Transform hudTransform = _hudCamera.transform;
            Quaternion hudRotation = referenceRotation * GetCachedHudOffsetRotation();
            Vector3 hudPosition = referencePosition + referenceRotation * _hudCameraLocalOffset;
            if (!_hudPoseApplied || _appliedHudPosition != hudPosition || _appliedHudRotation != hudRotation)
            {
                hudTransform.SetPositionAndRotation(hudPosition, hudRotation);
                _appliedHudPosition = hudPosition;
                _appliedHudRotation = hudRotation;
                _hudPoseApplied = true;
            }
        }

        private UniversalAdditionalCameraData GetCachedHudCameraData()
        {
            if (_hudCamera == null)
                return null;

            if (_cachedHudCameraData == null || _cachedHudCameraData.gameObject != _hudCamera.gameObject)
                _cachedHudCameraData = _hudCamera.GetComponent<UniversalAdditionalCameraData>();

            return _cachedHudCameraData;
        }

        private UniversalAdditionalCameraData GetCachedBaseCameraData()
        {
            if (_baseStackCamera == null)
                return null;

            if (_cachedBaseCameraData == null || _cachedBaseCameraData.gameObject != _baseStackCamera.gameObject)
                _cachedBaseCameraData = _baseStackCamera.GetComponent<UniversalAdditionalCameraData>();

            return _cachedBaseCameraData;
        }

        private void InvalidatePoseCache()
        {
            _cachedHudCameraData = null;
            _cachedBaseCameraData = null;
            _poseApplied = false;
            _appliedVisorPosition = default;
            _appliedVisorRotation = default;
            _appliedVisorScale = default;
            _hudPoseApplied = false;
            _appliedHudPosition = default;
            _appliedHudRotation = default;
            _cachedVisorEulerOffset = default;
            _cachedVisorOffsetRotation = Quaternion.identity;
            _cachedHudEulerOffset = default;
            _cachedHudOffsetRotation = Quaternion.identity;
        }

        private Quaternion GetCachedVisorOffsetRotation()
        {
            if (_cachedVisorEulerOffset != _visorLocalEulerOffset)
            {
                _cachedVisorEulerOffset = _visorLocalEulerOffset;
                _cachedVisorOffsetRotation = Quaternion.Euler(_visorLocalEulerOffset);
            }

            return _cachedVisorOffsetRotation;
        }

        private Quaternion GetCachedHudOffsetRotation()
        {
            if (_cachedHudEulerOffset != _hudCameraLocalEulerOffset)
            {
                _cachedHudEulerOffset = _hudCameraLocalEulerOffset;
                _cachedHudOffsetRotation = Quaternion.Euler(_hudCameraLocalEulerOffset);
            }

            return _cachedHudOffsetRotation;
        }

        public void SetHUDIntensity(float intensity)
        {
            float clampedIntensity = Mathf.Clamp(intensity, 0f, 5f);
            if (Mathf.Approximately(_hudIntensity, clampedIntensity))
                return;

            _hudIntensity = clampedIntensity;
            _materialPropertiesDirty = true;
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
            InvalidatePoseCache();
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

#if UNITY_EDITOR
        private static bool IsEditorPreviewActive()
        {
            return !EditorApplication.isCompiling &&
                   !EditorApplication.isUpdating;
        }

        private bool ShouldTickInEditMode()
        {
            if (Application.isPlaying || !isActiveAndEnabled || !_previewInEditMode)
                return false;

            if (_materialPropertiesDirty)
                return true;

            if (_syncToReferenceCamera && _syncPoseInEditMode)
                return true;

            return NeedsAutoResolve();
        }

        private void EvaluateEditorTickRegistration()
        {
            if (ShouldTickInEditMode())
            {
                RegisterEditorTick();
                return;
            }

            UnregisterEditorTick();
        }

        private void RegisterEditorTick()
        {
            EditorApplication.update -= EditorTick;
            EditorApplication.update += EditorTick;
        }

        private void UnregisterEditorTick()
        {
            EditorApplication.update -= EditorTick;
        }
#endif
    }
}

using Hecton8.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NASAPunk.Visor
{
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class VisorHUDController : MonoBehaviour, ITickable
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

        // ── Glitch state machine (replaces coroutine) ────────────
        private bool  _glitchActive;
        private float _glitchTimer;
        private float _glitchDuration;
        private float _glitchOriginalIntensity;
        private bool  _isTickRegistered;

        // ── Glitch deterministic RNG (zero GC) ──────────────────
        private uint _glitchRngState = 1;

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
            // ── Остановить glitch если активен ──
            if (_glitchActive)
            {
                _hudIntensity = _glitchOriginalIntensity;
                _glitchActive = false;
            }
            StopTicking();

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

        // ══════════════════════════════════════════════════════════
        //  LateUpdate — REMOVED Camera.Render()
        // ══════════════════════════════════════════════════════════
        //
        //  Ранее здесь вызывался _hudCamera.Render() — синхронный рендер
        //  вне URP pipeline. Это заставляло GPU полностью флашить пайплайн,
        //  что на слабых GPU (MX350) приводило к 2-3x падению FPS.
        //
        //  Решение: HUD камера настроена как Base camera в URP с enabled=true.
        //  Она рендерит в свой targetTexture автоматически через URP pipeline,
        //  синхронно с остальными камерами, без дополнительного flush.
        //
        //  SyncCameraRole() обновлён: убрана логика _manualProjectionRender.
        //  Камера всегда enabled когда projection активна.
        // ══════════════════════════════════════════════════════════

        private void OnValidate()
        {
            EnsurePropertyBlock();
            AutoResolveReferences();
            SyncProjectionPose();

            if (!isActiveAndEnabled)
                return;

            RebuildProjection();
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable — GLITCH STATE MACHINE (replaces coroutine)
        // ══════════════════════════════════════════════════════════

        public void Tick(float deltaTime)
        {
            if (!_glitchActive)
            {
                StopTicking();
                return;
            }

            _glitchTimer += deltaTime;

            if (_glitchTimer >= _glitchDuration)
            {
                // ── Glitch завершён ──
                _hudIntensity = _glitchOriginalIntensity;
                _glitchActive = false;
                StopTicking();
                return;
            }

            // ── Случайная модуляция интенсивности (zero GC) ──
            // xorshift32 вместо UnityEngine.Random (deterministic, no static state pollution)
            float rand01 = XorShift01();
            _hudIntensity = _glitchOriginalIntensity * (0.1f + rand01 * 1.9f); // range [0.1, 2.0] × original
        }

        /// <summary>
        /// xorshift32 — детерминированный, zero GC, zero boxing.
        /// Возвращает float в [0, 1).
        /// </summary>
        private float XorShift01()
        {
            _glitchRngState ^= _glitchRngState << 13;
            _glitchRngState ^= _glitchRngState >> 17;
            _glitchRngState ^= _glitchRngState << 5;
            return (_glitchRngState & 0x7FFFFF) / (float)0x800000; // 23-bit mantissa
        }

        // ══════════════════════════════════════════════════════════
        //  TICK REGISTRATION
        // ══════════════════════════════════════════════════════════

        private void StartTicking()
        {
            if (_isTickRegistered) return;

            // В Edit mode GameTickManager может не существовать
            if (!Application.isPlaying) return;

            GameTickManager gtm = GameTickManager.Instance;
            if (gtm != null)
            {
                gtm.Register(this);
                _isTickRegistered = true;
            }
        }

        private void StopTicking()
        {
            if (!_isTickRegistered) return;

            GameTickManager gtm = GameTickManager.Instance;
            if (gtm != null)
            {
                gtm.Unregister(this);
            }

            _isTickRegistered = false;
        }

        // ══════════════════════════════════════════════════════════
        //  AUTO-RESOLVE
        // ══════════════════════════════════════════════════════════

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

        /// <summary>
        /// Настраивает роль HUD камеры в URP.
        /// 
        /// v2.0 ИЗМЕНЕНИЕ: Убран _manualProjectionRender.
        /// В projection mode камера ВСЕГДА enabled=true и рендерит
        /// через URP pipeline в свой targetTexture автоматически.
        /// Это устраняет синхронный Camera.Render() flush.
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
                // ── Base camera, рендерит в RT через URP pipeline ──
                hudCameraData.renderType = CameraRenderType.Base;

                if (baseCameraData != null && baseCameraData.cameraStack.Contains(_hudCamera))
                    baseCameraData.cameraStack.Remove(_hudCamera);

                _hudCamera.clearFlags = CameraClearFlags.SolidColor;
                Color color = _hudCamera.backgroundColor;
                color.a = 0f;
                _hudCamera.backgroundColor = color;

                // Камера enabled — URP рендерит её автоматически в targetTexture
                _hudCamera.enabled = true;
                return;
            }

            // ── Overlay mode — стандартный camera stacking ──
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

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

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
        /// Запускает glitch-эффект. Zero GC — без корутин.
        /// Использует ITickable стейт-машину с таймером.
        /// 
        /// Безопасен при повторном вызове: перезапускает таймер.
        /// </summary>
        public void GlitchPulse(float duration = 0.3f)
        {
            if (!_glitchActive)
            {
                _glitchOriginalIntensity = _hudIntensity;
            }
            // Если glitch уже активен — перезапускаем таймер,
            // но сохраняем оригинальную интенсивность от первого вызова.

            _glitchActive = true;
            _glitchTimer = 0f;
            _glitchDuration = duration;

            // Seed RNG с текущим временем для вариативности
            _glitchRngState = (uint)(Time.unscaledTime * 1000f) | 1u; // |1 гарантирует ненулевой state

            StartTicking();
        }
    }
}
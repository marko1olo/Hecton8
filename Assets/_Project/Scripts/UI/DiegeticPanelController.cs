using Hecton8.Bootstrap;
using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Hecton8.UI
{
    /// <summary>
    /// Supplies runtime power state for a diegetic panel surface.
    /// </summary>
    public interface IPanelPowerSource
    {
        /// <summary>
        /// Returns the normalized power level for the requested panel.
        /// </summary>
        /// <param name="panelId">Stable panel identifier.</param>
        /// <returns>Power level in the inclusive 0..1 range.</returns>
        float GetPowerLevel(int panelId);
    }

    /// <summary>
    /// Receives zero-allocation canvas hit events emitted by <see cref="DiegeticPanelController"/>.
    /// </summary>
    public interface IPanelInteractable
    {
        /// <summary>
        /// Receives one projected panel input event.
        /// </summary>
        /// <param name="inputEvent">Blittable panel event payload.</param>
        void ReceiveCanvasInput(in DiegeticPanelInputEvent inputEvent);
    }

    /// <summary>
    /// Provides the latest clamped cursor position resolved by a diegetic panel.
    /// </summary>
    public interface ICursorHost
    {
        /// <summary>
        /// Returns the latest clamped canvas position in reference-resolution pixels.
        /// </summary>
        /// <param name="panelId">Stable panel identifier.</param>
        /// <returns>Latest clamped position.</returns>
        float2 GetClampedCanvasPos(int panelId);
    }

    /// <summary>
    /// Applies or clears depth-based occlusion fading on a diegetic panel surface.
    /// </summary>
    public interface IDepthOcclusionReceiver
    {
        /// <summary>
        /// Applies new occlusion settings.
        /// </summary>
        /// <param name="fadeRange">World-space fade band thickness.</param>
        /// <param name="active">True when depth occlusion should remain enabled.</param>
        void SetOcclusionParams(float fadeRange, bool active);
    }

    /// <summary>
    /// Event payload emitted by diegetic panel hit processing.
    /// </summary>
    public struct DiegeticPanelInputEvent
    {
        /// <summary>
        /// Stable panel identifier.
        /// </summary>
        public int PanelId;

        /// <summary>
        /// Canvas-space hit position in reference-resolution pixels.
        /// </summary>
        public float2 CanvasHitPoint;

        /// <summary>
        /// Event-type bitmask.
        /// </summary>
        public DiegeticPanelInputEventType EventType;

        /// <summary>
        /// Monotonic unscaled timestamp in seconds.
        /// </summary>
        public float Timestamp;
    }

    /// <summary>
    /// Bitmask describing one diegetic panel input state transition.
    /// </summary>
    [System.Flags]
    public enum DiegeticPanelInputEventType : byte
    {
        /// <summary>No event.</summary>
        None = 0,

        /// <summary>Initial press on the panel.</summary>
        Down = 1 << 0,

        /// <summary>Button released while the cursor is on the panel.</summary>
        Up = 1 << 1,

        /// <summary>Button held while the cursor remains on the panel.</summary>
        Hold = 1 << 2,

        /// <summary>Cursor moved across the panel without a press transition.</summary>
        Hover = 1 << 3,
    }

    /// <summary>
    /// Owns one world-space diegetic UI panel, its cursor projection math, and optional RT-backed panel surface.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Diegetic Panel Controller")]
    [RequireComponent(typeof(Canvas))]
    public sealed class DiegeticPanelController : MonoBehaviour, ITickable, IUpdatable, ICursorHost, IDepthOcclusionReceiver
    {
        private const string WorldGeometrySortingLayer = "WorldGeometry";
        private const float MinCanvasExtent = 0.0001f;
        private const float MatrixRefreshInterval = 0.25f;
        private const int MaxInputEventsPerTick = 4;
        private const int InputEventCapacity = 16;
        private const int InputEventMask = InputEventCapacity - 1;

        private static readonly int _MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int _BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int _DepthFadeRangeId = Shader.PropertyToID("_DepthFadeRange");
        private static readonly int _OcclusionActiveId = Shader.PropertyToID("_OcclusionActive");
        private static readonly int _PanelPowerLevelId = Shader.PropertyToID("_PanelPowerLevel");

        [System.Flags]
        private enum PanelStateFlags : ushort
        {
            None = 0,
            Active = 1 << 0,
            Powered = 1 << 1,
            Sleeping = 1 << 2,
            CursorOver = 1 << 3,
            DepthOcclusionActive = 1 << 5,
            PlayerInRange = 1 << 7,
        }

        private struct PanelData
        {
            public float4x4 LocalToWorld;
            public float4x4 WorldToLocal;
            public float2 CanvasSize;
            public float2 HalfSize;
            public int ReferenceWidth;
            public int ReferenceHeight;
            public PanelStateFlags StateFlags;
            public float DistToCamera;
            public float LastInteractTime;
        }

        [Header("── References ─────────────────────────────")]
        [SerializeField, Tooltip("World-space canvas driven by this panel controller.")]
        private Canvas targetCanvas;

        [SerializeField, Tooltip("RectTransform whose rect defines the panel hit area. Defaults to the canvas root.")]
        private RectTransform panelRect;

        [SerializeField, Tooltip("Physical collider used for panel ray hits.")]
        private Collider panelCollider;

        [SerializeField, Tooltip("Optional camera that renders the panel into a render texture.")]
        private Camera panelCamera;

        [SerializeField, Tooltip("Optional unique material assigned to the physical panel mesh. When present, the panel RT is written into _BaseMap/_MainTex.")]
        private Material panelOutputMaterial;

        [SerializeField, Tooltip("Optional renderer that displays the panel output material on physical geometry.")]
        private Renderer panelSurfaceRenderer;

        [SerializeField, Tooltip("Optional physical cursor transform that floats above the panel surface.")]
        private Transform cursorTransform;

        [SerializeField, Tooltip("Optional explicit ray origin. Falls back to the resolved interaction camera.")]
        private Transform rayOrigin;

        [SerializeField, Tooltip("Optional explicit forward source. Falls back to the resolved interaction camera.")]
        private Transform rayDirectionSource;

        [SerializeField, Tooltip("Optional explicit interaction camera. Falls back to the current player camera.")]
        private Camera interactionCamera;

        [SerializeField, Tooltip("Optional component implementing IPanelInteractable to receive panel events.")]
        private MonoBehaviour panelInteractable;

        [SerializeField, Tooltip("Optional component implementing IPanelPowerSource to drive panel power visuals.")]
        private MonoBehaviour panelPowerSource;

        [Header("── Identity ───────────────────────────────")]
        [SerializeField, Tooltip("Stable panel identifier forwarded to receivers and power sources.")]
        private int panelId = 1;

        [Header("── Interaction ────────────────────────────")]
        [SerializeField, Tooltip("Layer mask used for the panel collider hit query.")]
        private LayerMask interactionMask = ~0;

        [SerializeField, Tooltip("Maximum world-space distance for cursor interaction.")]
        private float maxInteractionDistance = 2.75f;

        [SerializeField, Tooltip("Reference-resolution used for projected canvas coordinates.")]
        private Vector2Int referenceResolution = new Vector2Int(512, 256);

        [SerializeField, Tooltip("Panel-local edge inset used to keep the physical cursor inside the panel bounds.")]
        private Vector2 cursorMargin = new Vector2(0.01f, 0.01f);

        [SerializeField, Tooltip("World-space hover offset applied along the panel normal.")]
        private float cursorHoverOffset = 0.002f;

        [SerializeField, Tooltip("Exponential smoothing speed for the physical cursor.")]
        private float cursorSmoothingSpeed = 20f;

        [SerializeField, Tooltip("Seconds between panel distance and RT-tier refresh passes.")]
        private float distanceRefreshInterval = MatrixRefreshInterval;

        [Header("── Render Texture ────────────────────────")]
        [SerializeField, Tooltip("Enables the RT-backed panel path for physical screen meshes.")]
        private bool enableRenderTexturePresentation = true;

        [SerializeField, Tooltip("Treat this panel as an MX350-tier target and keep the RT in RGB565 + D16.")]
        private bool forceMx350Tier = true;

        [SerializeField, Tooltip("RenderTexture filter mode applied to the panel surface.")]
        private FilterMode renderTextureFilterMode = FilterMode.Bilinear;

        [Header("── Occlusion ─────────────────────────────")]
        [SerializeField, Tooltip("Enables depth-fade integration on the panel output material.")]
        private bool enableDepthOcclusion = true;

        [SerializeField, Tooltip("World-space depth fade band used by the panel surface shader.")]
        private float depthFadeRange = 0.05f;

        [SerializeField, Tooltip("Layer assigned to the world-space canvas hierarchy when RT presentation is active.")]
        private int panelCanvasLayer = 5;

        // COLD ALLOC: RaycastHit[1] — bounded panel hit buffer — owner: DiegeticPanelController
        // COLD ALLOC: DiegeticPanelInputEvent[16] — fixed panel input ring buffer — owner: DiegeticPanelController
        private readonly DiegeticPanelInputEvent[] _inputEvents = new DiegeticPanelInputEvent[InputEventCapacity];
        // COLD ALLOC: List[4] - cached panel collider set for zero-GC hit validation - owner: DiegeticPanelController
        private readonly System.Collections.Generic.List<Collider> _panelColliderCache = new System.Collections.Generic.List<Collider>(4);

        private PanelData _panelData;
        private RectTransform _resolvedPanelRect;
        private Transform _resolvedPanelTransform;
        private Camera _resolvedInteractionCamera;
        private GraphicRaycaster _cachedGraphicRaycaster;
        private IPanelInteractable _panelInteractable;
        private IPanelPowerSource _panelPowerSource;
        private IInteractionSignalService _interactionSignals;
        private IInputService _input;
        private RenderTexture _panelRenderTexture;
        private int2 _activeRenderResolution;
        private float _refreshTimer;
        private float _cameraRetryTimer;
        private float _appliedDepthFadeRange = -1f;
        private float _appliedPowerLevel = -1f;
        private bool _tickRegistered;
        private bool _wasPressedLastFrame;
        private bool _cursorVisible;
        private bool _cursorStateInitialized;
        private bool _matrixStateInitialized;
        private bool _canvasSettingsApplied;
        private bool _isMx350Tier;
        private int _inputEventHead;
        private int _inputEventTail;
        private int _inputEventCount;
        private int _appliedCanvasLayer = int.MinValue;
        private ulong _raycastRequesterId;
        private float2 _clampedCanvasPosition;
        private float3 _smoothedCursorWorld;
        private Collider _cachedPanelColliderRoot;

        /// <summary>
        /// Returns the current RT assigned to the panel camera, if any.
        /// </summary>
        public RenderTexture ActiveRenderTexture => _panelRenderTexture;

        /// <summary>
        /// Returns the current clamped canvas position in reference-resolution pixels.
        /// </summary>
        public float2 CurrentCanvasPosition => _clampedCanvasPosition;

        private void Awake()
        {
            _raycastRequesterId = EntityId.ToULong(gameObject.GetEntityId());
            ResolveSerializedReferences();
            ResolveInterfaces();
            DetermineTargetHardwareTier();
            RefreshServices();
            ApplyCanvasWorldSpaceSettings();
            ApplyRendererBindings();
            RefreshPanelData(forceRefresh: true);
            EnsureRenderTexture(forceRefresh: true);
            SetCursorVisible(false);
        }

        private void OnEnable()
        {
            ResolveSerializedReferences();
            RefreshServices();
            TryRegisterTick();
            ApplyCanvasWorldSpaceSettings();
            RefreshPanelData(forceRefresh: true);
            EnsureRenderTexture(forceRefresh: true);
        }

        private void OnDisable()
        {
            UnregisterTick();
            _wasPressedLastFrame = false;
            _cursorStateInitialized = false;
            _canvasSettingsApplied = false;
            SetCursorVisible(false);
            ReleaseRenderTexture();
        }

        private void OnDestroy()
        {
            ReleaseRenderTexture();
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (!EnsureRuntimeState())
                return;

            RefreshPanelData(forceRefresh: false);
            RefreshDistanceAndRenderTexture(deltaTime);
            ApplyPowerLevel();

            if (!TryResolveRay(out float3 rayOriginWs, out float3 rayDirectionWs))
            {
                ClearHoverState();
                return;
            }

            if (!TryResolveHit(rayOriginWs, rayDirectionWs, out RaycastHit hit) || !IsPanelCollider(hit.collider))
            {
                ClearHoverState();
                return;
            }

            if (!TryProjectHitToCanvas(hit.point, out float2 canvasPos, out float3 localHit))
            {
                ClearHoverState();
                return;
            }

            _panelData.StateFlags |= PanelStateFlags.CursorOver | PanelStateFlags.PlayerInRange;
            _panelData.LastInteractTime = Time.unscaledTime;
            _clampedCanvasPosition = canvasPos;

            UpdateCursor(localHit, deltaTime);
            QueueInputEvents(canvasPos);
            DispatchInputEvents();
        }

        /// <inheritdoc />
        public float2 GetClampedCanvasPos(int requestedPanelId)
        {
            return requestedPanelId == panelId ? _clampedCanvasPosition : float2.zero;
        }

        /// <inheritdoc />
        public void SetOcclusionParams(float fadeRange, bool active)
        {
            depthFadeRange = Mathf.Max(0.001f, fadeRange);
            enableDepthOcclusion = active;
            ApplyMaterialState(forceTextureRefresh: false, forceDepthRefresh: true);
        }

        /// <summary>
        /// Forces one immediate RT rebuild using the current panel distance.
        /// </summary>
        public void ForceRefreshRenderTexture()
        {
            EnsureRenderTexture(forceRefresh: true);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            maxInteractionDistance = Mathf.Max(0.1f, maxInteractionDistance);
            referenceResolution.x = Mathf.Max(1, referenceResolution.x);
            referenceResolution.y = Mathf.Max(1, referenceResolution.y);
            cursorMargin.x = Mathf.Max(0f, cursorMargin.x);
            cursorMargin.y = Mathf.Max(0f, cursorMargin.y);
            cursorHoverOffset = Mathf.Max(0f, cursorHoverOffset);
            cursorSmoothingSpeed = Mathf.Max(0.1f, cursorSmoothingSpeed);
            distanceRefreshInterval = Mathf.Max(0.1f, distanceRefreshInterval);
            depthFadeRange = Mathf.Max(0.001f, depthFadeRange);

            ResolveSerializedReferences();
            ResolveInterfaces();
            DetermineTargetHardwareTier();
            ApplyCanvasWorldSpaceSettings();
            RefreshPanelData(forceRefresh: true);
        }
#endif

        private void ResolveSerializedReferences()
        {
            if (targetCanvas == null)
                targetCanvas = GetComponent<Canvas>();

            if (panelRect == null && targetCanvas != null)
                panelRect = targetCanvas.transform as RectTransform;

            if (panelCollider == null)
                panelCollider = GetComponent<Collider>();

            if (!ReferenceEquals(_cachedPanelColliderRoot, panelCollider))
                RebuildPanelColliderCache();

            _resolvedPanelRect = panelRect;
            _resolvedPanelTransform = _resolvedPanelRect != null ? _resolvedPanelRect.transform : transform;

            if (targetCanvas != null)
                targetCanvas.TryGetComponent(out _cachedGraphicRaycaster);
        }

        private void ResolveInterfaces()
        {
            _panelInteractable = panelInteractable as IPanelInteractable;
            _panelPowerSource = panelPowerSource as IPanelPowerSource;
        }

        private void DetermineTargetHardwareTier()
        {
            string gpuName = SystemInfo.graphicsDeviceName;
            _isMx350Tier = forceMx350Tier ||
                           SystemInfo.graphicsMemorySize <= 2048 ||
                           (!string.IsNullOrEmpty(gpuName) &&
                            gpuName.IndexOf("MX350", System.StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void RefreshServices()
        {
            _interactionSignals = GlobalRegistry.InteractionSignals;
            _input = GlobalRegistry.Input;
        }

        private bool EnsureRuntimeState()
        {
            if (!isActiveAndEnabled)
                return false;

            ResolveSerializedReferences();
            ResolveInterfaces();
            RefreshServices();
            ApplyCanvasWorldSpaceSettings();
            return targetCanvas != null && _resolvedPanelRect != null && panelCollider != null;
        }

        private void ApplyCanvasWorldSpaceSettings()
        {
            if (targetCanvas == null)
                return;

            Camera resolvedCamera = ResolveInteractionCamera();
            bool requiresLayerApply = enableRenderTexturePresentation &&
                                      panelCanvasLayer >= 0 &&
                                      (!_canvasSettingsApplied || _appliedCanvasLayer != panelCanvasLayer);

            if (_canvasSettingsApplied &&
                targetCanvas.renderMode == RenderMode.WorldSpace &&
                !targetCanvas.pixelPerfect &&
                !targetCanvas.overrideSorting &&
                targetCanvas.additionalShaderChannels == AdditionalCanvasShaderChannels.None &&
                targetCanvas.sortingLayerName == WorldGeometrySortingLayer &&
                ReferenceEquals(targetCanvas.worldCamera, resolvedCamera) &&
                (_cachedGraphicRaycaster == null || !_cachedGraphicRaycaster.enabled) &&
                !requiresLayerApply)
            {
                return;
            }

            targetCanvas.renderMode = RenderMode.WorldSpace;
            targetCanvas.pixelPerfect = false;
            targetCanvas.overrideSorting = false;
            targetCanvas.sortingLayerName = WorldGeometrySortingLayer;
            targetCanvas.additionalShaderChannels = AdditionalCanvasShaderChannels.None;
            targetCanvas.worldCamera = resolvedCamera;

            if (_cachedGraphicRaycaster != null)
                _cachedGraphicRaycaster.enabled = false;

            if (requiresLayerApply)
            {
                SetLayerRecursive(targetCanvas.transform, panelCanvasLayer);
                _appliedCanvasLayer = panelCanvasLayer;
            }

            _canvasSettingsApplied = true;
        }

        private void ApplyRendererBindings()
        {
            if (panelSurfaceRenderer != null && panelOutputMaterial != null && panelSurfaceRenderer.sharedMaterial != panelOutputMaterial)
                panelSurfaceRenderer.sharedMaterial = panelOutputMaterial;

            ApplyMaterialState(forceTextureRefresh: true, forceDepthRefresh: true);
        }

        private void RefreshPanelData(bool forceRefresh)
        {
            if (_resolvedPanelRect == null)
                return;

            if (!forceRefresh &&
                _matrixStateInitialized &&
                !_resolvedPanelTransform.hasChanged &&
                !_resolvedPanelRect.hasChanged)
            {
                return;
            }

            Matrix4x4 localToWorldMatrix = _resolvedPanelTransform.localToWorldMatrix;
            Matrix4x4 worldToLocalMatrix = _resolvedPanelTransform.worldToLocalMatrix;
            Rect rect = _resolvedPanelRect.rect;

            _panelData.LocalToWorld = ToFloat4x4(localToWorldMatrix);
            _panelData.WorldToLocal = ToFloat4x4(worldToLocalMatrix);
            _panelData.CanvasSize = math.max(new float2(rect.width, rect.height), new float2(MinCanvasExtent, MinCanvasExtent));
            _panelData.HalfSize = _panelData.CanvasSize * 0.5f;
            _panelData.ReferenceWidth = math.max(1, referenceResolution.x);
            _panelData.ReferenceHeight = math.max(1, referenceResolution.y);
            _panelData.StateFlags |= PanelStateFlags.Active;

            _resolvedPanelTransform.hasChanged = false;
            _resolvedPanelRect.hasChanged = false;
            _matrixStateInitialized = true;
        }

        private void RefreshDistanceAndRenderTexture(float deltaTime)
        {
            _refreshTimer -= deltaTime;
            if (_refreshTimer > 0f)
                return;

            _refreshTimer = distanceRefreshInterval;

            Camera resolvedCamera = ResolveInteractionCamera();
            if (resolvedCamera == null)
                return;

            float3 panelOrigin = _panelData.LocalToWorld.c3.xyz;
            _panelData.DistToCamera = math.distance(panelOrigin, resolvedCamera.transform.position);
            EnsureRenderTexture(forceRefresh: false);
        }

        private void EnsureRenderTexture(bool forceRefresh)
        {
            if (!enableRenderTexturePresentation || panelCamera == null)
                return;

            int2 requiredResolution = DetermineRenderResolution(_panelData.DistToCamera);
            if (!forceRefresh &&
                _panelRenderTexture != null &&
                requiredResolution.x == _activeRenderResolution.x &&
                requiredResolution.y == _activeRenderResolution.y)
            {
                return;
            }

            ReleaseRenderTexture();

            RenderTextureDescriptor descriptor = new RenderTextureDescriptor(requiredResolution.x, requiredResolution.y)
            {
                msaaSamples = 1,
                volumeDepth = 1,
                dimension = TextureDimension.Tex2D,
                useMipMap = false,
                autoGenerateMips = false,
                sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear,
                graphicsFormat = _isMx350Tier
                    ? GraphicsFormat.B5G6R5_UNormPack16
                    : GraphicsFormat.R8G8B8A8_UNorm,
                depthStencilFormat = GraphicsFormat.D16_UNorm
            };

            _panelRenderTexture = new RenderTexture(descriptor)
            {
                name = "DiegeticPanel_RT",
                filterMode = renderTextureFilterMode
            };
            _panelRenderTexture.Create();
            panelCamera.targetTexture = _panelRenderTexture;
            _activeRenderResolution = requiredResolution;

            ApplyMaterialState(forceTextureRefresh: true, forceDepthRefresh: true);
        }

        private int2 DetermineRenderResolution(float distanceToCamera)
        {
            if (_isMx350Tier)
            {
                if (distanceToCamera > 5f)
                    return new int2(128, 64);
                if (distanceToCamera > 2f)
                    return new int2(256, 128);
                return new int2(512, 256);
            }

            if (distanceToCamera > 5f)
                return new int2(256, 128);
            if (distanceToCamera > 2f)
                return new int2(512, 256);
            if (distanceToCamera > 0.8f)
                return new int2(1024, 512);
            return new int2(2048, 1024);
        }

        private void ReleaseRenderTexture()
        {
            if (panelCamera != null && panelCamera.targetTexture == _panelRenderTexture)
                panelCamera.targetTexture = null;

            if (_panelRenderTexture == null)
                return;

            _panelRenderTexture.Release();
            Destroy(_panelRenderTexture);
            _panelRenderTexture = null;
            _activeRenderResolution = int2.zero;
        }

        private void ApplyPowerLevel()
        {
            float powerLevel = 1f;
            if (_panelPowerSource != null)
                powerLevel = math.clamp(_panelPowerSource.GetPowerLevel(panelId), 0f, 1f);

            if (powerLevel > 0.0001f)
                _panelData.StateFlags |= PanelStateFlags.Powered;
            else
                _panelData.StateFlags &= ~PanelStateFlags.Powered;

            if (Mathf.Approximately(_appliedPowerLevel, powerLevel))
                return;

            _appliedPowerLevel = powerLevel;
            ApplyMaterialState(forceTextureRefresh: false, forceDepthRefresh: false);
        }

        private void ApplyMaterialState(bool forceTextureRefresh, bool forceDepthRefresh)
        {
            if (panelOutputMaterial == null)
                return;

            if (forceTextureRefresh && _panelRenderTexture != null)
            {
                if (panelOutputMaterial.HasProperty(_BaseMapId))
                    panelOutputMaterial.SetTexture(_BaseMapId, _panelRenderTexture);
                if (panelOutputMaterial.HasProperty(_MainTexId))
                    panelOutputMaterial.SetTexture(_MainTexId, _panelRenderTexture);
            }

            float resolvedFadeRange = enableDepthOcclusion ? depthFadeRange : 0f;
            bool shouldWriteDepthState = forceDepthRefresh || !Mathf.Approximately(_appliedDepthFadeRange, resolvedFadeRange);
            if (shouldWriteDepthState)
            {
                _appliedDepthFadeRange = resolvedFadeRange;
                if (panelOutputMaterial.HasProperty(_DepthFadeRangeId))
                    panelOutputMaterial.SetFloat(_DepthFadeRangeId, resolvedFadeRange);
                if (panelOutputMaterial.HasProperty(_OcclusionActiveId))
                    panelOutputMaterial.SetFloat(_OcclusionActiveId, enableDepthOcclusion ? 1f : 0f);
            }

            if (panelOutputMaterial.HasProperty(_PanelPowerLevelId))
                panelOutputMaterial.SetFloat(_PanelPowerLevelId, math.max(0f, _appliedPowerLevel));
        }

        private bool TryResolveRay(out float3 rayOriginWs, out float3 rayDirectionWs)
        {
            rayOriginWs = float3.zero;
            rayDirectionWs = float3.zero;

            Camera resolvedCamera = ResolveInteractionCamera();
            if (resolvedCamera == null)
                return false;

            Transform originTransform = rayOrigin != null ? rayOrigin : resolvedCamera.transform;
            Transform directionTransform = rayDirectionSource != null ? rayDirectionSource : resolvedCamera.transform;
            rayOriginWs = originTransform.position;
            rayDirectionWs = math.normalizesafe(directionTransform.forward);
            return math.lengthsq(rayDirectionWs) > 0.0001f;
        }

        private bool TryResolveHit(float3 rayOriginWs, float3 rayDirectionWs, out RaycastHit hit)
        {
            hit = default;

            if (_interactionSignals == null || !_interactionSignals.IsInitialized)
                RefreshServices();

            if (_interactionSignals == null || !_interactionSignals.IsInitialized)
                return false;

            return _interactionSignals.TryRaycastPrimary(
                _raycastRequesterId,
                rayOriginWs,
                rayDirectionWs,
                maxInteractionDistance,
                interactionMask.value,
                QueryTriggerInteraction.Collide,
                out hit);
        }

        private bool TryProjectHitToCanvas(Vector3 worldHitPoint, out float2 canvasPos, out float3 localHit)
        {
            localHit = math.transform(_panelData.WorldToLocal, worldHitPoint);

            float2 safeCanvasSize = math.max(_panelData.CanvasSize, new float2(MinCanvasExtent, MinCanvasExtent));
            float2 uv = new float2(
                (localHit.x + _panelData.HalfSize.x) / safeCanvasSize.x,
                (localHit.y + _panelData.HalfSize.y) / safeCanvasSize.y);

            if (uv.x < 0f || uv.x > 1f || uv.y < 0f || uv.y > 1f)
            {
                canvasPos = float2.zero;
                return false;
            }

            uv = math.clamp(uv, 0f, 1f);
            canvasPos = new float2(
                uv.x * _panelData.ReferenceWidth,
                uv.y * _panelData.ReferenceHeight);
            return true;
        }

        private void UpdateCursor(float3 localHit, float deltaTime)
        {
            if (cursorTransform == null)
                return;

            float2 clampedLocalXY = math.clamp(
                localHit.xy,
                -_panelData.HalfSize + new float2(cursorMargin.x, cursorMargin.y),
                _panelData.HalfSize - new float2(cursorMargin.x, cursorMargin.y));

            float3 cursorLocal = new float3(clampedLocalXY.x, clampedLocalXY.y, cursorHoverOffset);
            float3 cursorTargetWorld = math.transform(_panelData.LocalToWorld, cursorLocal);

            if (!_cursorStateInitialized)
            {
                _smoothedCursorWorld = cursorTargetWorld;
                _cursorStateInitialized = true;
            }
            else
            {
                float alpha = 1f - math.exp(-cursorSmoothingSpeed * math.max(0.0001f, deltaTime));
                _smoothedCursorWorld = math.lerp(_smoothedCursorWorld, cursorTargetWorld, alpha);
            }

            float3 panelNormal = math.normalizesafe(_panelData.LocalToWorld.c2.xyz, new float3(0f, 0f, 1f));
            float3 panelUp = math.normalizesafe(_panelData.LocalToWorld.c1.xyz, new float3(0f, 1f, 0f));
            quaternion rotation = quaternion.LookRotationSafe(-panelNormal, panelUp);

            cursorTransform.SetPositionAndRotation(
                _smoothedCursorWorld,
                new Quaternion(rotation.value.x, rotation.value.y, rotation.value.z, rotation.value.w));
            SetCursorVisible(true);
        }

        private void QueueInputEvents(float2 canvasPos)
        {
            DiegeticPanelInputEventType eventType = DiegeticPanelInputEventType.Hover;

            if (_input != null && _input.IsInitialized)
            {
                PlayerInputState state = _input.GetState();
                bool isPressed = state.HasAction(PlayerInputAction.Interact) || state.HasAction(PlayerInputAction.PrimaryFire);

                if (isPressed && !_wasPressedLastFrame)
                    eventType = DiegeticPanelInputEventType.Down;
                else if (isPressed)
                    eventType = DiegeticPanelInputEventType.Hold;
                else if (_wasPressedLastFrame)
                    eventType = DiegeticPanelInputEventType.Up;

                _wasPressedLastFrame = isPressed;
            }
            else
            {
                _wasPressedLastFrame = false;
            }

            EnqueueInputEvent(new DiegeticPanelInputEvent
            {
                PanelId = panelId,
                CanvasHitPoint = canvasPos,
                EventType = eventType,
                Timestamp = Time.unscaledTime
            });
        }

        private void EnqueueInputEvent(DiegeticPanelInputEvent inputEvent)
        {
            if (_inputEventCount >= InputEventCapacity)
                return;

            _inputEvents[_inputEventTail] = inputEvent;
            _inputEventTail = (_inputEventTail + 1) & InputEventMask;
            _inputEventCount++;
        }

        private void DispatchInputEvents()
        {
            if (_panelInteractable == null)
            {
                _inputEventHead = 0;
                _inputEventTail = 0;
                _inputEventCount = 0;
                return;
            }

            int dispatchCount = math.min(_inputEventCount, MaxInputEventsPerTick);
            for (int i = 0; i < dispatchCount; i++)
            {
                DiegeticPanelInputEvent inputEvent = _inputEvents[_inputEventHead];
                _inputEventHead = (_inputEventHead + 1) & InputEventMask;
                _inputEventCount--;
                _panelInteractable.ReceiveCanvasInput(in inputEvent);
            }
        }

        private void ClearHoverState()
        {
            _panelData.StateFlags &= ~(PanelStateFlags.CursorOver | PanelStateFlags.PlayerInRange);
            _wasPressedLastFrame = false;
            _inputEventHead = 0;
            _inputEventTail = 0;
            _inputEventCount = 0;
            _cursorStateInitialized = false;
            SetCursorVisible(false);
        }

        private bool IsPanelCollider(Collider collider)
        {
            if (collider == null || panelCollider == null)
                return false;

            for (int i = 0; i < _panelColliderCache.Count; i++)
            {
                if (ReferenceEquals(_panelColliderCache[i], collider))
                    return true;
            }

            return false;
        }

        private void RebuildPanelColliderCache()
        {
            _cachedPanelColliderRoot = panelCollider;
            _panelColliderCache.Clear();

            if (panelCollider == null)
                return;

            panelCollider.GetComponentsInChildren(true, _panelColliderCache);
        }

        private Camera ResolveInteractionCamera()
        {
            if (interactionCamera != null && interactionCamera.isActiveAndEnabled)
            {
                _resolvedInteractionCamera = interactionCamera;
                return _resolvedInteractionCamera;
            }

            if (_resolvedInteractionCamera != null && _resolvedInteractionCamera.isActiveAndEnabled)
                return _resolvedInteractionCamera;

            float now = Application.isPlaying ? Time.unscaledTime : Time.realtimeSinceStartup;
            if (now < _cameraRetryTimer)
                return null;

            _cameraRetryTimer = now + 1f;

            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null &&
                playerTransform.TryGetComponent(out Camera playerCamera))
            {
                _resolvedInteractionCamera = playerCamera;
                return _resolvedInteractionCamera;
            }

            if (SceneBootstrap.TryGetCurrentPlayerTransform(out playerTransform) && playerTransform != null)
                _resolvedInteractionCamera = ((Hecton8.Core.GlobalRegistry.Player != null && Hecton8.Core.GlobalRegistry.Player.PlayerCamera != null) ? Hecton8.Core.GlobalRegistry.Player.PlayerCamera : playerTransform.GetComponent<Camera>());

            return _resolvedInteractionCamera;
        }

        private void SetCursorVisible(bool visible)
        {
            if (cursorTransform == null || _cursorVisible == visible)
                return;

            cursorTransform.gameObject.SetActive(visible);
            _cursorVisible = visible;
        }

        private void TryRegisterTick()
        {
            if (_tickRegistered || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _tickRegistered = true;
        }

        private void UnregisterTick()
        {
            if (!_tickRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _tickRegistered = false;
        }

        private static void SetLayerRecursive(Transform root, int layer)
        {
            if (root == null)
                return;

            root.gameObject.layer = layer;
            int childCount = root.childCount;
            for (int i = 0; i < childCount; i++)
                SetLayerRecursive(root.GetChild(i), layer);
        }

        private static float4x4 ToFloat4x4(Matrix4x4 matrix)
        {
            return new float4x4(
                new float4(matrix.m00, matrix.m10, matrix.m20, matrix.m30),
                new float4(matrix.m01, matrix.m11, matrix.m21, matrix.m31),
                new float4(matrix.m02, matrix.m12, matrix.m22, matrix.m32),
                new float4(matrix.m03, matrix.m13, matrix.m23, matrix.m33));
        }
    }
}

using Hecton8.Core;
using Hecton8.World;
using System.Runtime.InteropServices;
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
    [StructLayout(LayoutKind.Sequential)]
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
        /// Analog delta forwarded for terminal dials and lever drags.
        /// </summary>
        public float2 AnalogDelta;

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

        /// <summary>Analog scroll or drag delta resolved through the platform input snapshot.</summary>
        Scroll = 1 << 4,
    }

    /// <summary>
    /// Owns one world-space diegetic UI panel, its cursor projection math, and optional RT-backed panel surface.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Diegetic Panel Controller")]
    [RequireComponent(typeof(Canvas))]
    public sealed class DiegeticPanelController : MonoBehaviour, ITickable, IUpdatable, ILateFrameTickable, ICursorHost, IDepthOcclusionReceiver, IDamageReceiver, IGlobalRegistryHotSwapListener
    {
        private const string WorldGeometrySortingLayer = "WorldGeometry";
        private const string DefaultPhosphorDecayMaterialResourcePath = "UI/MAT_DiegeticPanelPhosphorDecay";
        private const float MinCanvasExtent = 0.0001f;
        private const float MaximumInteractionReachMeters = 2f;
        private const float DamageGlitchDecaySharpness = 16f;
        private const float MatrixRefreshInterval = 0.25f;
        private const float FarPanelDistanceSq = 25f;
        private const float MediumPanelDistanceSq = 4f;
        private const float NearPanelDistanceSq = 0.64f;
        private const float InvTwoPi = 0.159154943f;
        private const float InvByteMax = 0.00392156862f;
        private const int MaxInputEventsPerTick = 4;
        private const int InputEventCapacity = 16;
        private const int InputEventMask = InputEventCapacity - 1;
        private const int MaxFingerSlots = 32;

        /// <summary>
        /// Physical panel interaction policy.
        /// </summary>
        public enum PanelInteractionMode : byte
        {
            /// <summary>Use only the central interaction ray.</summary>
            RaycastOnly = 0,

            /// <summary>Use only fingertip-to-surface distance checks.</summary>
            PhysicalFingerOnly = 1,

            /// <summary>Use fingertips when present, with desktop ray fallback when no fingertips are bound.</summary>
            HybridPreferFinger = 2
        }

        private static readonly int _MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int _BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int _DepthFadeRangeId = Shader.PropertyToID("_DepthFadeRange");
        private static readonly int _OcclusionActiveId = Shader.PropertyToID("_OcclusionActive");
        private static readonly int _PanelPowerLevelId = Shader.PropertyToID("_PanelPowerLevel");
        private static readonly int _TerminalDamageGlitchId = Shader.PropertyToID("_TerminalDamageGlitch");
        private static readonly int _FlashlightGlareId = Shader.PropertyToID("_FlashlightGlare");
        private static readonly int _PreviousTexId = Shader.PropertyToID("_PreviousTex");
        private static readonly int _CurrentTexId = Shader.PropertyToID("_CurrentTex");
        private static readonly int _DecayId = Shader.PropertyToID("_Decay");

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

        [StructLayout(LayoutKind.Sequential)]
        private struct PanelData
        {
            public float4x4 LocalToWorld;
            public float4x4 WorldToLocal;
            public float3 PanelNormal;
            public float3 PanelUp;
            public float2 CanvasSize;
            public float2 InvCanvasSize;
            public float2 HalfSize;
            public float2 InvReferenceSize;
            public int ReferenceWidth;
            public int ReferenceHeight;
            public PanelStateFlags StateFlags;
            public float DistToCameraSq;
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

        [Header("Physical Finger Input")]
        [SerializeField, Tooltip("VR interaction mode. PhysicalFingerOnly disables gaze/ray input entirely.")]
        private PanelInteractionMode interactionMode = PanelInteractionMode.HybridPreferFinger;

        [SerializeField, Tooltip("Tracked fingertip transforms used for physical finger-to-surface PDA input. Index 0/1 are normally left/right index tips.")]
        private Transform[] fingertipTransforms = new Transform[2];

        [SerializeField, Tooltip("Allows desktop ray fallback when no fingertip transforms are bound and XR is not active.")]
        private bool allowDesktopRayFallbackWithoutFingers = true;

        [SerializeField, Min(0.001f), Tooltip("Maximum absolute panel-local Z distance that counts as a finger press.")]
        private float fingerPressDistance = 0.025f;

        [SerializeField, Min(0.001f), Tooltip("Release hysteresis distance. Must be equal or larger than press distance.")]
        private float fingerReleaseDistance = 0.045f;

        [SerializeField, Min(0.001f), Tooltip("Maximum panel-local Z distance that keeps the physical cursor hovering over the panel.")]
        private float fingerHoverDistance = 0.08f;

        [Header("── Render Texture ────────────────────────")]
        [SerializeField, Tooltip("Enables the RT-backed panel path for physical screen meshes.")]
        private bool enableRenderTexturePresentation = true;

        [SerializeField, Tooltip("Treat this panel as an MX350-tier target and keep the RT in RGB565 + D16.")]
        private bool forceMx350Tier = true;

        [SerializeField, Tooltip("RenderTexture filter mode applied to the panel surface.")]
        private FilterMode renderTextureFilterMode = FilterMode.Bilinear;

        [SerializeField, Tooltip("Render Lead-owned texture assigned to the panel surface. This path never enables a panel camera.")]
        private RenderTexture externallyOwnedRenderTexture;

        [SerializeField, Tooltip("Legacy compatibility only. Keep disabled for production submarine terminals; secondary panel cameras are forbidden.")]
        private bool allowLegacyPanelCameraRenderTexture;

        [SerializeField, Tooltip("Maintains a persistent PDA/panel phosphor history RT: previous frame * decay + current frame.")]
        private bool enablePhosphorDecay;

        [SerializeField, Tooltip("Hidden full-screen material shader used to accumulate phosphor decay.")]
        private Shader phosphorDecayShader;

        [SerializeField, Tooltip("Authored full-screen material used to accumulate phosphor decay. Runtime fallback loads Resources/UI/MAT_DiegeticPanelPhosphorDecay.")]
        private Material phosphorDecayMaterial;

        [SerializeField, Range(0.1f, 0.98f), Tooltip("Multiplier applied to the previous PDA frame before adding the current panel frame.")]
        private float phosphorDecay = 0.85f;

        [Header("── Occlusion ─────────────────────────────")]
        [SerializeField, Tooltip("Enables depth-fade integration on the panel output material.")]
        private bool enableDepthOcclusion = true;

        [SerializeField, Tooltip("World-space depth fade band used by the panel surface shader.")]
        private float depthFadeRange = 0.05f;

        [Header("Terminal Effects")]
        [SerializeField, Range(0f, 1f), Tooltip("Normalized glare applied when an external flashlight hits terminal glass.")]
        private float flashlightGlare;

        [SerializeField, Range(0.02f, 1f), Tooltip("Seconds a received damage packet keeps the CRT glitch active.")]
        private float damageGlitchDurationSeconds = 0.22f;

        [SerializeField, Tooltip("Layer assigned to the world-space canvas hierarchy when RT presentation is active.")]
        private int panelCanvasLayer = 5;

        [Header("── Proxy Light ─────────────────────────")]
        [SerializeField, Tooltip("Registers a lightweight diegetic proxy light while the panel is powered.")]
        private bool enableProxyLight = true;

        [SerializeField, Min(0.01f), Tooltip("Meters covered by the panel proxy light.")]
        private float proxyLightRangeMeters = 1.35f;

        [SerializeField, Range(0f, 1f), Tooltip("Maximum normalized intensity written into the proxy light registry.")]
        private float proxyLightIntensity = 0.22f;

        [SerializeField, Tooltip("Linearized panel proxy light color. Use low values; this is a lighting hint, not a real Light component.")]
        private Color proxyLightColor = new Color(0.58f, 0.92f, 1f, 1f);

        [SerializeField, Range(0f, 0.3f), Tooltip("Small unscaled flicker amount synchronized with panel power.")]
        private float proxyLightFlicker = 0.06f;

        // COLD ALLOC: DiegeticPanelInputEvent[16] — fixed panel input ring buffer — owner: DiegeticPanelController
        private readonly DiegeticPanelInputEvent[] _inputEvents = new DiegeticPanelInputEvent[InputEventCapacity];

        private PanelData _panelData;
        private RectTransform _resolvedPanelRect;
        private Transform _resolvedPanelTransform;
        private Transform _cachedCursorTransform;
        private CanvasGroup _cursorCanvasGroup;
        private Graphic _cursorGraphic;
        private Renderer _cursorRenderer;
        private Collider _cursorCollider;
        private Camera _resolvedInteractionCamera;
        private Transform _resolvedInteractionCameraTransform;
        private GraphicRaycaster _cachedGraphicRaycaster;
        private Canvas _cachedGraphicRaycasterCanvas;
        private IPanelInteractable _panelInteractable;
        private IPanelPowerSource _panelPowerSource;
        private IInputService _input;
        private RenderTexture _panelRenderTexture;
        private RenderTexture _phosphorFrontTexture;
        private RenderTexture _phosphorBackTexture;
        private Material _phosphorDecayMaterial;
        private Material _cachedPanelOutputMaterial;
        private Texture _appliedPanelOutputTexture;
        private Texture _appliedPhosphorPreviousTexture;
        private Texture _appliedPhosphorCurrentTexture;
        private MonoBehaviour _cachedPanelInteractableSource;
        private MonoBehaviour _cachedPanelPowerSourceSource;
        private int2 _activeRenderResolution;
        private int2 _fixedRenderResolution;
        private bool _retainRenderTextureOnDisable;
        private bool _presentationPausedByOwner;
        private float _refreshTimer;
        private float _appliedDepthFadeRange = -1f;
        private float _appliedPowerLevel = -1f;
        private float _appliedPanelMaterialPowerLevel = -1f;
        private float _terminalDamageGlitch;
        private float _terminalDamageGlitchPeak;
        private float _terminalDamageGlitchRemaining;
        private float _terminalDamageGlitchDuration = 0.22f;
        private float _tickUnscaledTime;
        private float _appliedTerminalDamageGlitch = -1f;
        private float _appliedFlashlightGlare = -1f;
        private float _appliedPhosphorDecay = -1f;
        private bool _tickRegistered;
        private bool _lateFrameRegistered;
        private bool _renderPipelineHookRegistered;
        private bool _hotSwapListenerRegistered;
        private bool _inputAwaitingRegistration;
        private bool _phosphorMaterialResolveAttempted;
        private bool _phosphorMaterialResolveFailed;
        private bool _panelOutputHasBaseMap;
        private bool _panelOutputHasMainTex;
        private bool _panelOutputHasDepthFadeRange;
        private bool _panelOutputHasOcclusionActive;
        private bool _panelOutputHasPanelPowerLevel;
        private bool _panelOutputHasTerminalDamageGlitch;
        private bool _panelOutputHasFlashlightGlare;
        private bool _resolvedInteractionCameraFromExplicit;
        private bool _wasPressedLastFrame;
        private bool _fingerPressedLastFrame;
        private bool _cursorVisible;
        private bool _cursorVisibilityInitialized;
        private bool _cursorStateInitialized;
        private bool _matrixStateInitialized;
        private bool _canvasSettingsApplied;
        private bool _isMx350Tier;
        private bool _ownsPanelRenderTexture;
        private int _inputEventHead;
        private int _inputEventTail;
        private int _inputEventCount;
        private int _appliedCanvasLayer = int.MinValue;
        private int _proxyLightKey;
        private bool _proxyLightRegistered;
        private float2 _clampedCanvasPosition;
        private float2 _lastFingerCanvasPosition;
        private float3 _smoothedCursorWorld;
        private float3 _lastFingerLocalHit;
        private Transform[] _cachedFingertipTransforms;
        private uint _fingertipBindingMask;
        private int _activeFingerIndex = -1;

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
            _proxyLightKey = unchecked((int)EntityId.ToULong(gameObject.GetEntityId()));
            ResolveSerializedReferences(resolveGraphicRaycaster: true);
            ResolveInterfaces();
            DetermineTargetHardwareTier();
            RefreshServices();
            TryRegisterHotSwapListener();
            RegisterRenderPipelineHook();
            ApplyCanvasWorldSpaceSettings();
            ApplyRendererBindings();
            RefreshPanelData(forceRefresh: true);
            EnsureRenderTexture(forceRefresh: true);
            SetCursorVisible(false);
        }

        private void OnEnable()
        {
            ResolveSerializedReferences(resolveGraphicRaycaster: true);
            RefreshServices();
            TryRegisterHotSwapListener();
            TryRegisterTick();
            RegisterRenderPipelineHook();
            ApplyCanvasWorldSpaceSettings();
            RefreshPanelData(forceRefresh: true);
            EnsureRenderTexture(forceRefresh: true);
        }

        private void OnDisable()
        {
            UnregisterTick();
            TryUnregisterHotSwapListener();
            UnregisterRenderPipelineHook();
            UnregisterProxyLight();
            ClearHoverState();
            CacheInteractionCamera(null, fromExplicit: false);
            _input = null;
            _inputAwaitingRegistration = false;
            _cursorStateInitialized = false;
            _canvasSettingsApplied = false;
            if (panelCamera != null && panelCamera.enabled)
                panelCamera.enabled = false;
            if (!_retainRenderTextureOnDisable)
                ReleaseRenderTexture();
        }

        private void OnDestroy()
        {
            UnregisterRenderPipelineHook();
            TryUnregisterHotSwapListener();
            UnregisterProxyLight();
            ReleaseRenderTexture();
            ReleasePhosphorResources();
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (!EnsureRuntimeState())
                return;

            if (_presentationPausedByOwner)
            {
                SetCursorVisible(false);
                ClearHoverState();
                if (panelCamera != null && panelCamera.enabled)
                    panelCamera.enabled = false;
                return;
            }

            _tickUnscaledTime = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            RefreshPanelData(forceRefresh: false);
            RefreshDistanceAndRenderTexture(deltaTime);
            ApplyPowerLevel();
            UpdateProxyLightRegistration();
            UpdateTerminalEffectState(deltaTime);

            if (TryResolveFingerInteraction(
                    out float2 fingerCanvasPos,
                    out float3 fingerLocalHit,
                    out DiegeticPanelInputEventType fingerEventType,
                    out bool showFingerCursor))
            {
                _panelData.StateFlags |= PanelStateFlags.CursorOver | PanelStateFlags.PlayerInRange;
                _panelData.LastInteractTime = _tickUnscaledTime;
                _clampedCanvasPosition = fingerCanvasPos;

                if (showFingerCursor)
                    UpdateCursor(fingerLocalHit, deltaTime);
                else
                    SetCursorVisible(false);

                QueueInputEvent(fingerCanvasPos, fingerEventType);
                DispatchInputEvents();
                return;
            }

            if (!CanUseDesktopPlaneFallback())
            {
                ClearHoverState();
                return;
            }

            if (!TryResolveRay(out float3 rayOriginWs, out float3 rayDirectionWs))
            {
                ClearHoverState();
                return;
            }

            float effectiveInteractionDistance = ResolveEffectiveInteractionDistance();
            if (!IsRayOriginWithinAupInteractionRange(rayOriginWs, effectiveInteractionDistance))
            {
                ClearHoverState();
                return;
            }

            if (!TryProjectRayToPanel(
                    rayOriginWs,
                    rayDirectionWs,
                    effectiveInteractionDistance,
                    rayDirectionIsNormalized: true,
                    out float2 canvasPos,
                    out float3 localHit,
                    out _))
            {
                ClearHoverState();
                return;
            }

            _panelData.StateFlags |= PanelStateFlags.CursorOver | PanelStateFlags.PlayerInRange;
            _panelData.LastInteractTime = _tickUnscaledTime;
            _clampedCanvasPosition = canvasPos;

            UpdateCursor(localHit, deltaTime);
            QueueInputEventsFromInputState(canvasPos);
            DispatchInputEvents();
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            if (_presentationPausedByOwner)
                return;

            if (enablePhosphorDecay && _panelRenderTexture != null)
                ApplyMaterialState(forceTextureRefresh: true, forceDepthRefresh: false);
        }

        /// <inheritdoc />
        public float2 GetClampedCanvasPos(int requestedPanelId)
        {
            return requestedPanelId == panelId ? _clampedCanvasPosition : float2.zero;
        }

        /// <inheritdoc />
        public void SetOcclusionParams(float fadeRange, bool active)
        {
            depthFadeRange = math.max(0.001f, fadeRange);
            enableDepthOcclusion = active;
            ApplyMaterialState(forceTextureRefresh: false, forceDepthRefresh: true);
        }

        /// <inheritdoc />
        public void ReceiveDamage(in DamagePacket packet)
        {
            float channelDelta = math.abs(packet.NextValue - packet.PreviousValue);
            float integrityDelta = packet.IntegrityDelta * InvByteMax;
            float magnitude = math.max(math.abs(packet.Magnitude), math.max(channelDelta, integrityDelta));
            TriggerDamageGlitch(magnitude, damageGlitchDurationSeconds);
        }

        /// <summary>
        /// Drives the CRT damage-glitch shader channel without depending on combat concrete types.
        /// </summary>
        public void TriggerDamageGlitch(float magnitude01, float durationSeconds)
        {
            float safeMagnitude = math.saturate(math.isfinite(magnitude01) ? magnitude01 : 0f);
            if (safeMagnitude <= 0.0001f)
                return;

            _terminalDamageGlitchPeak = math.max(_terminalDamageGlitchPeak, safeMagnitude);
            _terminalDamageGlitchRemaining = math.max(_terminalDamageGlitchRemaining, math.max(0.02f, durationSeconds));
            _terminalDamageGlitchDuration = math.max(0.02f, _terminalDamageGlitchRemaining);
            _terminalDamageGlitch = math.max(_terminalDamageGlitch, safeMagnitude);
            ApplyMaterialState(forceTextureRefresh: false, forceDepthRefresh: false);
        }

        /// <summary>
        /// Applies normalized flashlight glare from an external light-probe owner.
        /// </summary>
        public void SetFlashlightGlare(float glare01)
        {
            float safeGlare = math.saturate(math.isfinite(glare01) ? glare01 : 0f);
            if (math.abs(flashlightGlare - safeGlare) <= 0.0001f)
                return;

            flashlightGlare = safeGlare;
            ApplyMaterialState(forceTextureRefresh: false, forceDepthRefresh: false);
        }

        /// <summary>
        /// Forces one immediate RT rebuild using the current panel distance.
        /// </summary>
        public void ForceRefreshRenderTexture()
        {
            if (_presentationPausedByOwner)
                return;

            EnsureRenderTexture(forceRefresh: true);
        }

        /// <summary>
        /// Projects one canvas-space reference-resolution pixel coordinate onto the physical panel plane.
        /// </summary>
        /// <param name="canvasPosition">Canvas-space pixel coordinate in reference-resolution units.</param>
        /// <param name="surfaceOffset">Positive offset applied along the panel normal in meters.</param>
        /// <param name="worldPosition">Projected world-space position.</param>
        /// <returns>True when the panel basis is valid.</returns>
        public bool TryProjectCanvasPointToWorld(float2 canvasPosition, float surfaceOffset, out Vector3 worldPosition)
        {
            RefreshPanelData(forceRefresh: false);

            float2 uv = math.clamp(canvasPosition * _panelData.InvReferenceSize, 0f, 1f);

            float2 localXY = (uv * _panelData.CanvasSize) - _panelData.HalfSize;

            float3 localPoint = new float3(localXY.x, localXY.y, surfaceOffset);
            worldPosition = math.transform(_panelData.LocalToWorld, localPoint);
            return true;
        }

        /// <summary>
        /// Projects a world-space selection ray onto the physical panel without using a physics raycast.
        /// </summary>
        /// <param name="worldRayOrigin">Ray origin in world space.</param>
        /// <param name="worldRayDirection">Ray direction in world space.</param>
        /// <param name="maxDistance">Maximum accepted plane-intersection distance in meters.</param>
        /// <param name="canvasPosition">Resolved canvas-space pixel coordinate.</param>
        /// <param name="worldHitPosition">Resolved world-space panel hit position.</param>
        /// <returns>True when the ray intersects the panel bounds.</returns>
        public bool TryProjectRayToCanvas(
            Vector3 worldRayOrigin,
            Vector3 worldRayDirection,
            float maxDistance,
            out float2 canvasPosition,
            out Vector3 worldHitPosition)
        {
            RefreshPanelData(forceRefresh: false);

            canvasPosition = float2.zero;
            worldHitPosition = default;

            if (!TryProjectRayToPanel(
                    (float3)worldRayOrigin,
                    (float3)worldRayDirection,
                    maxDistance,
                    rayDirectionIsNormalized: false,
                    out canvasPosition,
                    out _,
                    out float3 worldHit))
                return false;

            worldHitPosition = worldHit;
            return true;
        }

        /// <summary>
        /// Resolves the current physical panel orientation used by diegetic projections.
        /// </summary>
        /// <param name="rotation">Panel-space rotation with forward along the panel normal and up along the panel vertical axis.</param>
        /// <returns>True when the panel basis is valid.</returns>
        public bool TryGetPanelRotation(out Quaternion rotation)
        {
            RefreshPanelData(forceRefresh: false);

            float3 panelNormal = _panelData.PanelNormal;
            float3 panelUp = _panelData.PanelUp;
            if (math.lengthsq(panelNormal) <= 0.0001f || math.lengthsq(panelUp) <= 0.0001f)
            {
                rotation = Quaternion.identity;
                return false;
            }

            quaternion panelRotation = quaternion.LookRotationSafe(panelNormal, panelUp);
            rotation = new Quaternion(panelRotation.value.x, panelRotation.value.y, panelRotation.value.z, panelRotation.value.w);
            return true;
        }

        /// <summary>
        /// Resolves the world-space basis vectors that correspond to one canvas pixel along the panel axes.
        /// </summary>
        /// <param name="worldRightPerPixel">World-space delta for +1 canvas pixel on the panel X axis.</param>
        /// <param name="worldUpPerPixel">World-space delta for +1 canvas pixel on the panel Y axis.</param>
        /// <returns>True when the panel basis is valid.</returns>
        public bool TryGetCanvasPixelBasis(out Vector3 worldRightPerPixel, out Vector3 worldUpPerPixel)
        {
            RefreshPanelData(forceRefresh: false);

            float xStep = _panelData.CanvasSize.x * _panelData.InvReferenceSize.x;
            float yStep = _panelData.CanvasSize.y * _panelData.InvReferenceSize.y;
            worldRightPerPixel = (Vector3)(_panelData.LocalToWorld.c0.xyz * xStep);
            worldUpPerPixel = (Vector3)(_panelData.LocalToWorld.c1.xyz * yStep);
            return xStep > 0f && yStep > 0f;
        }

        /// <summary>
        /// Returns the active reference resolution used by the panel projection path.
        /// </summary>
        public Vector2Int ReferenceResolutionPixels => new Vector2Int(
            math.max(1, _panelData.ReferenceWidth),
            math.max(1, _panelData.ReferenceHeight));

        internal bool TryGetFocusGateData(out Vector3 panelOrigin, out Vector3 panelNormal)
        {
            RefreshPanelData(forceRefresh: false);

            panelOrigin = (Vector3)_panelData.LocalToWorld.c3.xyz;
            panelNormal = (Vector3)_panelData.PanelNormal;
            return math.lengthsq(_panelData.PanelNormal) > 0.0001f &&
                   math.all(math.isfinite(_panelData.LocalToWorld.c3.xyz));
        }

        internal void OverrideFixedRenderTextureResolution(Vector2Int resolution, bool retainOnDisable)
        {
            int2 sanitizedResolution = new int2(math.max(1, resolution.x), math.max(1, resolution.y));
            bool resolutionChanged = sanitizedResolution.x != _fixedRenderResolution.x || sanitizedResolution.y != _fixedRenderResolution.y;
            bool retentionChanged = _retainRenderTextureOnDisable != retainOnDisable;
            _fixedRenderResolution = sanitizedResolution;
            _retainRenderTextureOnDisable = retainOnDisable;

            if (resolutionChanged || retentionChanged)
                EnsureRenderTexture(forceRefresh: true);
        }

        internal void ClearFixedRenderTextureResolutionOverride()
        {
            bool hadOverride = _fixedRenderResolution.x > 0 && _fixedRenderResolution.y > 0;
            _fixedRenderResolution = int2.zero;
            _retainRenderTextureOnDisable = false;

            if (hadOverride)
                EnsureRenderTexture(forceRefresh: true);
        }

        internal void ReleasePresentationRenderTexture()
        {
            _retainRenderTextureOnDisable = false;
            ReleaseRenderTexture();
        }

        internal void SetPresentationPaused(bool paused)
        {
            if (_presentationPausedByOwner == paused)
                return;

            _presentationPausedByOwner = paused;
            if (paused && panelCamera != null && panelCamera.enabled)
                panelCamera.enabled = false;

            if (paused)
            {
                SetCursorVisible(false);
                ClearHoverState();
                RefreshLateFrameRegistration();
                return;
            }

            if (isActiveAndEnabled)
                EnsureRenderTexture(forceRefresh: true);
            else
                RefreshLateFrameRegistration();
        }

        internal Canvas TargetCanvas => targetCanvas;

        internal Camera PanelCamera => panelCamera;

        internal void OverridePanelPresentation(Material outputMaterial, Renderer surfaceRenderer)
        {
            if (outputMaterial != null)
                panelOutputMaterial = outputMaterial;

            if (surfaceRenderer != null)
                panelSurfaceRenderer = surfaceRenderer;

            ApplyRendererBindings();
        }

        internal void OverridePhosphorDecay(bool enabled, float decay)
        {
            enablePhosphorDecay = enabled;
            phosphorDecay = math.clamp(decay, 0.1f, 0.98f);
            if (enabled)
                EnsurePhosphorResources();
            else
                ReleasePhosphorTextures();

            ApplyMaterialState(forceTextureRefresh: true, forceDepthRefresh: false);
        }

        internal void OverridePanelInteractable(MonoBehaviour interactable)
        {
            panelInteractable = interactable;
            ResolveInterfaces();
        }

        internal void OverrideInteractionMode(PanelInteractionMode mode)
        {
            interactionMode = mode;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            maxInteractionDistance = math.min(MaximumInteractionReachMeters, math.max(0.1f, maxInteractionDistance));
            referenceResolution.x = math.max(1, referenceResolution.x);
            referenceResolution.y = math.max(1, referenceResolution.y);
            cursorMargin.x = math.max(0f, cursorMargin.x);
            cursorMargin.y = math.max(0f, cursorMargin.y);
            cursorHoverOffset = math.max(0f, cursorHoverOffset);
            cursorSmoothingSpeed = math.max(0.1f, cursorSmoothingSpeed);
            distanceRefreshInterval = math.max(0.1f, distanceRefreshInterval);
            fingerPressDistance = math.max(0.001f, fingerPressDistance);
            fingerReleaseDistance = math.max(fingerPressDistance, fingerReleaseDistance);
            fingerHoverDistance = math.max(fingerReleaseDistance, fingerHoverDistance);
            depthFadeRange = math.max(0.001f, depthFadeRange);
            flashlightGlare = math.saturate(flashlightGlare);
            damageGlitchDurationSeconds = math.clamp(damageGlitchDurationSeconds, 0.02f, 1f);
            _phosphorMaterialResolveAttempted = false;
            _phosphorMaterialResolveFailed = false;
            _appliedPhosphorPreviousTexture = null;
            _appliedPhosphorCurrentTexture = null;
            _appliedPhosphorDecay = -1f;

            RefreshFingertipBindingMask();
            ResolveSerializedReferences(resolveGraphicRaycaster: true);
            ResolveInterfaces();
            DetermineTargetHardwareTier();
            ApplyCanvasWorldSpaceSettings();
            RefreshPanelData(forceRefresh: true);
        }
#endif

        private void ResolveSerializedReferences(bool resolveGraphicRaycaster)
        {
            if (targetCanvas == null)
                TryGetComponent(out targetCanvas);

            if (panelRect == null && targetCanvas != null)
                panelRect = targetCanvas.transform as RectTransform;

            if (panelCollider == null)
                TryGetComponent(out panelCollider);

            _resolvedPanelRect = panelRect;
            _resolvedPanelTransform = _resolvedPanelRect != null ? _resolvedPanelRect.transform : transform;

            if (!ReferenceEquals(_cachedCursorTransform, cursorTransform))
                ResolveCursorVisibilityTargets();

            if (!ReferenceEquals(_cachedFingertipTransforms, fingertipTransforms))
                RefreshFingertipBindingMask();

            if (resolveGraphicRaycaster &&
                targetCanvas != null &&
                !ReferenceEquals(_cachedGraphicRaycasterCanvas, targetCanvas))
            {
                targetCanvas.TryGetComponent(out _cachedGraphicRaycaster);
                _cachedGraphicRaycasterCanvas = targetCanvas;
            }
        }

        private void ResolveCursorVisibilityTargets()
        {
            _cachedCursorTransform = cursorTransform;
            _cursorCanvasGroup = null;
            _cursorGraphic = null;
            _cursorRenderer = null;
            _cursorCollider = null;
            _cursorVisibilityInitialized = false;

            if (cursorTransform == null)
                return;

            cursorTransform.TryGetComponent(out _cursorCanvasGroup);
            cursorTransform.TryGetComponent(out _cursorGraphic);
            cursorTransform.TryGetComponent(out _cursorRenderer);
            cursorTransform.TryGetComponent(out _cursorCollider);

            if (_cursorGraphic == null)
                _cursorGraphic = ComponentReferenceUtility.ResolveOwnedComponent<Graphic>(cursorTransform);

            if (_cursorRenderer == null)
                _cursorRenderer = ComponentReferenceUtility.ResolveOwnedComponent<Renderer>(cursorTransform);

            if (_cursorCollider == null)
                _cursorCollider = ComponentReferenceUtility.ResolveOwnedComponent<Collider>(cursorTransform);
        }

        private void ResolveInterfaces()
        {
            if (!ReferenceEquals(_cachedPanelInteractableSource, panelInteractable))
            {
                _cachedPanelInteractableSource = panelInteractable;
                _panelInteractable = panelInteractable as IPanelInteractable;
            }

            if (!ReferenceEquals(_cachedPanelPowerSourceSource, panelPowerSource))
            {
                _cachedPanelPowerSourceSource = panelPowerSource;
                _panelPowerSource = panelPowerSource as IPanelPowerSource;
            }
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
            IInputService registeredInput = GlobalRegistry.RegisteredInput;
            if (registeredInput != null)
            {
                _input = registeredInput;
                _inputAwaitingRegistration = false;
                return;
            }

            _input = GlobalRegistry.Input;
            _inputAwaitingRegistration = true;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Input)
            {
                _input = currentService as IInputService;
                if (_input == null)
                {
                    RefreshServices();
                    return;
                }

                _inputAwaitingRegistration = false;
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.Player || interactionCamera != null)
                return;

            Camera playerCamera = currentService is IPlayerRuntimeContext playerContext
                ? playerContext.PlayerCamera
                : null;
            CacheInteractionCamera(playerCamera != null && playerCamera.isActiveAndEnabled ? playerCamera : null, fromExplicit: false);
            _canvasSettingsApplied = false;
        }

        private bool EnsureRuntimeState()
        {
            if (!isActiveAndEnabled)
                return false;

            ResolveSerializedReferences(resolveGraphicRaycaster: false);
            ResolveInterfaces();
            if (_input == null || _inputAwaitingRegistration)
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
            _panelData.PanelNormal = ResolveSafePanelAxis(_panelData.LocalToWorld.c2.xyz, new float3(0f, 0f, 1f));
            _panelData.PanelUp = ResolveSafePanelAxis(_panelData.LocalToWorld.c1.xyz, new float3(0f, 1f, 0f));
            _panelData.CanvasSize = math.max(new float2(rect.width, rect.height), new float2(MinCanvasExtent, MinCanvasExtent));
            _panelData.InvCanvasSize = math.rcp(_panelData.CanvasSize);
            _panelData.HalfSize = _panelData.CanvasSize * 0.5f;
            _panelData.ReferenceWidth = math.max(1, referenceResolution.x);
            _panelData.ReferenceHeight = math.max(1, referenceResolution.y);
            _panelData.InvReferenceSize = math.rcp(new float2(_panelData.ReferenceWidth, _panelData.ReferenceHeight));
            _panelData.StateFlags |= PanelStateFlags.Active;

            _resolvedPanelTransform.hasChanged = false;
            _resolvedPanelRect.hasChanged = false;
            _matrixStateInitialized = true;
        }

        private void RefreshDistanceAndRenderTexture(float deltaTime)
        {
            if (_presentationPausedByOwner)
            {
                if (panelCamera != null && panelCamera.enabled)
                    panelCamera.enabled = false;
                return;
            }

            _refreshTimer -= deltaTime;
            if (_refreshTimer > 0f)
                return;

            _refreshTimer = distanceRefreshInterval;

            Camera resolvedCamera = ResolveInteractionCamera();
            if (resolvedCamera == null)
                return;
            Transform cameraTransform = _resolvedInteractionCameraTransform;
            if (cameraTransform == null)
                return;

            Vector3 panelOrigin = (Vector3)_panelData.LocalToWorld.c3.xyz;
            Vector3 cameraPosition = cameraTransform.position;
            _panelData.DistToCameraSq = ResolveAupDistanceSqClamped(panelOrigin, cameraPosition);
            EnsureRenderTexture(forceRefresh: false);
        }

        private void EnsureRenderTexture(bool forceRefresh)
        {
            if (!enableRenderTexturePresentation || _presentationPausedByOwner)
            {
                if (panelCamera != null && panelCamera.enabled)
                    panelCamera.enabled = false;
                if (!enableRenderTexturePresentation && _panelRenderTexture != null)
                    ReleaseRenderTexture();
                RefreshLateFrameRegistration();
                return;
            }

            if (externallyOwnedRenderTexture != null)
            {
                if (panelCamera != null && panelCamera.enabled)
                    panelCamera.enabled = false;

                if (_panelRenderTexture != externallyOwnedRenderTexture)
                {
                    ReleaseRenderTexture();
                    _panelRenderTexture = externallyOwnedRenderTexture;
                    _ownsPanelRenderTexture = false;
                    _activeRenderResolution = new int2(
                        math.max(1, externallyOwnedRenderTexture.width),
                        math.max(1, externallyOwnedRenderTexture.height));
                    ApplyMaterialState(forceTextureRefresh: true, forceDepthRefresh: true);
                }
                else if (forceRefresh)
                {
                    ApplyMaterialState(forceTextureRefresh: true, forceDepthRefresh: true);
                }

                EnsurePhosphorResources();
                RefreshLateFrameRegistration();
                return;
            }

            if (!allowLegacyPanelCameraRenderTexture || panelCamera == null)
            {
                if (panelCamera != null && panelCamera.enabled)
                    panelCamera.enabled = false;
                if (_panelRenderTexture != null)
                    ReleaseRenderTexture();
                RefreshLateFrameRegistration();
                return;
            }

            int2 requiredResolution = DetermineRenderResolutionFromDistanceSq(_panelData.DistToCameraSq);
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
            _ownsPanelRenderTexture = true;
            panelCamera.targetTexture = _panelRenderTexture;
            panelCamera.enabled = true;
            _activeRenderResolution = requiredResolution;
            EnsurePhosphorResources();

            ApplyMaterialState(forceTextureRefresh: true, forceDepthRefresh: true);
            RefreshLateFrameRegistration();
        }

        private int2 DetermineRenderResolutionFromDistanceSq(float distanceToCameraSq)
        {
            if (_fixedRenderResolution.x > 0 && _fixedRenderResolution.y > 0)
                return _fixedRenderResolution;

            if (_isMx350Tier)
            {
                if (distanceToCameraSq > FarPanelDistanceSq)
                    return new int2(128, 64);
                if (distanceToCameraSq > MediumPanelDistanceSq)
                    return new int2(256, 128);
                return new int2(512, 256);
            }

            if (distanceToCameraSq > FarPanelDistanceSq)
                return new int2(256, 128);
            if (distanceToCameraSq > MediumPanelDistanceSq)
                return new int2(512, 256);
            if (distanceToCameraSq > NearPanelDistanceSq)
                return new int2(1024, 512);
            return new int2(2048, 1024);
        }

        private void ReleaseRenderTexture()
        {
            ReleasePhosphorTextures();
            _appliedPanelOutputTexture = null;

            if (panelCamera != null && panelCamera.targetTexture == _panelRenderTexture)
                panelCamera.targetTexture = null;

            if (panelCamera != null && panelCamera.enabled)
                panelCamera.enabled = false;

            if (_panelRenderTexture == null)
            {
                RefreshLateFrameRegistration();
                return;
            }

            if (_ownsPanelRenderTexture)
            {
                _panelRenderTexture.Release();
                Destroy(_panelRenderTexture);
            }

            _panelRenderTexture = null;
            _ownsPanelRenderTexture = false;
            _activeRenderResolution = int2.zero;
            RefreshLateFrameRegistration();
        }

        private void EnsurePhosphorResources()
        {
            if (!enablePhosphorDecay || _panelRenderTexture == null)
                return;

            if (!EnsurePhosphorMaterial())
                return;

            if (_phosphorFrontTexture != null &&
                _phosphorFrontTexture.width == _panelRenderTexture.width &&
                _phosphorFrontTexture.height == _panelRenderTexture.height)
            {
                return;
            }

            ReleasePhosphorTextures();
            RenderTextureDescriptor descriptor = _panelRenderTexture.descriptor;
            descriptor.msaaSamples = 1;
            descriptor.depthStencilFormat = GraphicsFormat.None;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;

            _phosphorFrontTexture = CreatePhosphorTexture(descriptor, "DiegeticPanel_PhosphorFront");
            _phosphorBackTexture = CreatePhosphorTexture(descriptor, "DiegeticPanel_PhosphorBack");
            ApplyMaterialState(forceTextureRefresh: true, forceDepthRefresh: false);
        }

        private bool EnsurePhosphorMaterial()
        {
            if (_phosphorDecayMaterial != null)
                return true;

            if (_phosphorMaterialResolveFailed)
                return false;

            if (!_phosphorMaterialResolveAttempted)
            {
                _phosphorDecayMaterial = phosphorDecayMaterial != null
                    ? phosphorDecayMaterial
                    : Resources.Load<Material>(DefaultPhosphorDecayMaterialResourcePath);
                _phosphorMaterialResolveAttempted = true;
                _appliedPhosphorPreviousTexture = null;
                _appliedPhosphorCurrentTexture = null;
                _appliedPhosphorDecay = -1f;
            }

            if (_phosphorDecayMaterial == null)
            {
                _phosphorMaterialResolveFailed = true;
                return false;
            }

            Shader resolvedShader = phosphorDecayShader != null ? phosphorDecayShader : _phosphorDecayMaterial.shader;
            if (resolvedShader == null || _phosphorDecayMaterial.shader != resolvedShader)
            {
                _phosphorDecayMaterial = null;
                _phosphorMaterialResolveFailed = true;
                return false;
            }

            phosphorDecayShader = resolvedShader;
            return true;
        }

        private static RenderTexture CreatePhosphorTexture(RenderTextureDescriptor descriptor, string textureName)
        {
            RenderTexture texture = new RenderTexture(descriptor)
            {
                name = textureName,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            }; // COLD ALLOC: RenderTexture[panel resolution] - persistent PDA phosphor history buffer - owner: DiegeticPanelController
            texture.Create();
            return texture;
        }

        private void CompositePhosphorFrame()
        {
            if (!enablePhosphorDecay ||
                _panelRenderTexture == null ||
                _phosphorDecayMaterial == null)
            {
                return;
            }

            EnsurePhosphorResources();
            if (_phosphorFrontTexture == null || _phosphorBackTexture == null)
                return;

            if (!ReferenceEquals(_appliedPhosphorPreviousTexture, _phosphorFrontTexture))
            {
                _appliedPhosphorPreviousTexture = _phosphorFrontTexture;
                _phosphorDecayMaterial.SetTexture(_PreviousTexId, _phosphorFrontTexture);
            }

            if (!ReferenceEquals(_appliedPhosphorCurrentTexture, _panelRenderTexture))
            {
                _appliedPhosphorCurrentTexture = _panelRenderTexture;
                _phosphorDecayMaterial.SetTexture(_CurrentTexId, _panelRenderTexture);
            }

            if (math.abs(_appliedPhosphorDecay - phosphorDecay) > 0.0001f)
            {
                _appliedPhosphorDecay = phosphorDecay;
                _phosphorDecayMaterial.SetFloat(_DecayId, phosphorDecay);
            }
            Graphics.Blit(null, _phosphorBackTexture, _phosphorDecayMaterial, 0);

            RenderTexture swap = _phosphorFrontTexture;
            _phosphorFrontTexture = _phosphorBackTexture;
            _phosphorBackTexture = swap;
            ApplyMaterialState(forceTextureRefresh: true, forceDepthRefresh: false);
        }

        private void ReleasePhosphorResources()
        {
            ReleasePhosphorTextures();
            _phosphorDecayMaterial = null;
            _phosphorMaterialResolveAttempted = false;
            _phosphorMaterialResolveFailed = false;
            _appliedPhosphorPreviousTexture = null;
            _appliedPhosphorCurrentTexture = null;
            _appliedPhosphorDecay = -1f;
        }

        private void ReleasePhosphorTextures()
        {
            if (ReferenceEquals(_appliedPanelOutputTexture, _phosphorFrontTexture) ||
                ReferenceEquals(_appliedPanelOutputTexture, _phosphorBackTexture))
            {
                _appliedPanelOutputTexture = null;
            }

            _appliedPhosphorPreviousTexture = null;
            _appliedPhosphorCurrentTexture = null;

            if (_phosphorFrontTexture != null)
            {
                _phosphorFrontTexture.Release();
                Destroy(_phosphorFrontTexture);
                _phosphorFrontTexture = null;
            }

            if (_phosphorBackTexture != null)
            {
                _phosphorBackTexture.Release();
                Destroy(_phosphorBackTexture);
                _phosphorBackTexture = null;
            }
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

            bool materialChanged = !ReferenceEquals(_cachedPanelOutputMaterial, panelOutputMaterial);
            if (!materialChanged && math.abs(_appliedPowerLevel - powerLevel) <= 0.0001f)
                return;

            _appliedPowerLevel = powerLevel;
            ApplyMaterialState(forceTextureRefresh: materialChanged, forceDepthRefresh: materialChanged);
        }

        private void RefreshPanelOutputMaterialPropertyCache()
        {
            Material outputMaterial = panelOutputMaterial;
            if (ReferenceEquals(_cachedPanelOutputMaterial, outputMaterial))
                return;

            _cachedPanelOutputMaterial = outputMaterial;
            _panelOutputHasBaseMap = outputMaterial != null && outputMaterial.HasProperty(_BaseMapId);
            _panelOutputHasMainTex = outputMaterial != null && outputMaterial.HasProperty(_MainTexId);
            _panelOutputHasDepthFadeRange = outputMaterial != null && outputMaterial.HasProperty(_DepthFadeRangeId);
            _panelOutputHasOcclusionActive = outputMaterial != null && outputMaterial.HasProperty(_OcclusionActiveId);
            _panelOutputHasPanelPowerLevel = outputMaterial != null && outputMaterial.HasProperty(_PanelPowerLevelId);
            _panelOutputHasTerminalDamageGlitch = outputMaterial != null && outputMaterial.HasProperty(_TerminalDamageGlitchId);
            _panelOutputHasFlashlightGlare = outputMaterial != null && outputMaterial.HasProperty(_FlashlightGlareId);
            _appliedPanelOutputTexture = null;
            _appliedDepthFadeRange = -1f;
            _appliedPanelMaterialPowerLevel = -1f;
            _appliedTerminalDamageGlitch = -1f;
            _appliedFlashlightGlare = -1f;
        }

        private void ApplyMaterialState(bool forceTextureRefresh, bool forceDepthRefresh)
        {
            RefreshPanelOutputMaterialPropertyCache();

            Material outputMaterial = _cachedPanelOutputMaterial;
            if (outputMaterial == null)
                return;

            if (forceTextureRefresh && _panelRenderTexture != null)
            {
                Texture outputTexture = enablePhosphorDecay && _phosphorFrontTexture != null
                    ? _phosphorFrontTexture
                    : _panelRenderTexture;
                if (!ReferenceEquals(_appliedPanelOutputTexture, outputTexture))
                {
                    _appliedPanelOutputTexture = outputTexture;
                    if (_panelOutputHasBaseMap)
                        outputMaterial.SetTexture(_BaseMapId, outputTexture);
                    if (_panelOutputHasMainTex)
                        outputMaterial.SetTexture(_MainTexId, outputTexture);
                }
            }

            float resolvedFadeRange = enableDepthOcclusion ? depthFadeRange : 0f;
            bool shouldWriteDepthState = forceDepthRefresh || math.abs(_appliedDepthFadeRange - resolvedFadeRange) > 0.0001f;
            if (shouldWriteDepthState)
            {
                _appliedDepthFadeRange = resolvedFadeRange;
                if (_panelOutputHasDepthFadeRange)
                    outputMaterial.SetFloat(_DepthFadeRangeId, resolvedFadeRange);
                if (_panelOutputHasOcclusionActive)
                    outputMaterial.SetFloat(_OcclusionActiveId, enableDepthOcclusion ? 1f : 0f);
            }

            if (math.abs(_appliedPanelMaterialPowerLevel - _appliedPowerLevel) > 0.0001f)
            {
                _appliedPanelMaterialPowerLevel = _appliedPowerLevel;
                if (_panelOutputHasPanelPowerLevel)
                    outputMaterial.SetFloat(_PanelPowerLevelId, math.max(0f, _appliedPowerLevel));
            }

            if (math.abs(_appliedTerminalDamageGlitch - _terminalDamageGlitch) > 0.0001f)
            {
                _appliedTerminalDamageGlitch = _terminalDamageGlitch;
                if (_panelOutputHasTerminalDamageGlitch)
                    outputMaterial.SetFloat(_TerminalDamageGlitchId, math.saturate(_terminalDamageGlitch));
            }

            if (math.abs(_appliedFlashlightGlare - flashlightGlare) > 0.0001f)
            {
                _appliedFlashlightGlare = flashlightGlare;
                if (_panelOutputHasFlashlightGlare)
                    outputMaterial.SetFloat(_FlashlightGlareId, math.saturate(flashlightGlare));
            }
        }

        private void UpdateTerminalEffectState(float deltaTime)
        {
            float previousGlitch = _terminalDamageGlitch;
            if (_terminalDamageGlitchRemaining > 0f)
            {
                _terminalDamageGlitchRemaining = math.max(0f, _terminalDamageGlitchRemaining - math.max(0f, deltaTime));
                float life01 = _terminalDamageGlitchRemaining * math.rcp(math.max(0.02f, _terminalDamageGlitchDuration));
                _terminalDamageGlitch = _terminalDamageGlitchPeak * math.saturate(life01);
                if (_terminalDamageGlitchRemaining <= 0f)
                    _terminalDamageGlitchPeak = 0f;
            }
            else if (_terminalDamageGlitch > 0f)
            {
                float decay = FastDecayBlend(DamageGlitchDecaySharpness, deltaTime);
                _terminalDamageGlitch = math.lerp(_terminalDamageGlitch, 0f, decay);
                if (_terminalDamageGlitch <= 0.0001f)
                    _terminalDamageGlitch = 0f;
            }

            if (math.abs(previousGlitch - _terminalDamageGlitch) > 0.0001f)
                ApplyMaterialState(forceTextureRefresh: false, forceDepthRefresh: false);
        }

        private void UpdateProxyLightRegistration()
        {
            if (!enableProxyLight ||
                (_panelData.StateFlags & PanelStateFlags.Powered) == 0 ||
                _appliedPowerLevel <= 0.0001f)
            {
                UnregisterProxyLight();
                return;
            }

            float3 panelNormal = _panelData.PanelNormal;
            float3 runtimePosition = _panelData.LocalToWorld.c3.xyz + (panelNormal * 0.025f);
            if (!math.all(math.isfinite(runtimePosition)) || !math.all(math.isfinite(panelNormal)))
            {
                UnregisterProxyLight();
                return;
            }

            float now = _tickUnscaledTime;
            float flickerWave = EvaluateCheapFlicker01((now * 23.0f) + (panelId * 0.37f));
            float flicker01 = math.saturate(1f - proxyLightFlicker + (flickerWave * proxyLightFlicker));
            float intensity = math.saturate(proxyLightIntensity * _appliedPowerLevel * flicker01);
            if (intensity <= 0.0001f)
            {
                UnregisterProxyLight();
                return;
            }

            AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromRuntimePosition((Vector3)runtimePosition);
            ProxyLightData lightData = ProxyLightData.CreateUiPanel(
                in aup,
                runtimePosition,
                panelNormal,
                proxyLightColor.linear,
                proxyLightRangeMeters,
                intensity,
                flickerWave,
                flicker01,
                0f,
                now);

            if (ProxyLightRegistry.RegisterOrUpdate(_proxyLightKey, in lightData))
                _proxyLightRegistered = true;
        }

        private static float EvaluateCheapFlicker01(float phaseRadians)
        {
            float phase01 = math.frac((phaseRadians * InvTwoPi) + 0.25f);
            float triangle = 1f - math.abs(phase01 * 2f - 1f);
            return triangle * triangle;
        }

        private void UnregisterProxyLight()
        {
            if (!_proxyLightRegistered)
                return;

            ProxyLightRegistry.Unregister(_proxyLightKey);
            _proxyLightRegistered = false;
        }

        private bool TryResolveRay(out float3 rayOriginWs, out float3 rayDirectionWs)
        {
            rayOriginWs = float3.zero;
            rayDirectionWs = float3.zero;

            Camera resolvedCamera = ResolveInteractionCamera();
            if (resolvedCamera == null)
                return false;
            Transform cameraTransform = _resolvedInteractionCameraTransform;
            if (cameraTransform == null)
                return false;

            Transform originTransform = rayOrigin != null ? rayOrigin : cameraTransform;
            Transform directionTransform = rayDirectionSource != null ? rayDirectionSource : cameraTransform;
            rayOriginWs = originTransform.position;
            rayDirectionWs = directionTransform.forward;
            return math.lengthsq(rayDirectionWs) > 0.0001f;
        }

        private bool IsRayOriginWithinAupInteractionRange(float3 rayOriginWs, float maxDistance)
        {
            double maxDistanceSq = (double)maxDistance * maxDistance;
            Vector3 panelOrigin = (Vector3)_panelData.LocalToWorld.c3.xyz;
            return ResolveAupDistanceSq((Vector3)rayOriginWs, panelOrigin) <= maxDistanceSq;
        }

        private float ResolveEffectiveInteractionDistance()
        {
            return math.min(MaximumInteractionReachMeters, math.max(0.001f, maxInteractionDistance));
        }

        private static float ResolveAupDistanceSqClamped(Vector3 runtimePositionA, Vector3 runtimePositionB)
        {
            double distanceSq = ResolveAupDistanceSq(runtimePositionA, runtimePositionB);
            return distanceSq >= float.MaxValue ? float.MaxValue : (float)distanceSq;
        }

        private static double ResolveAupDistanceSq(Vector3 runtimePositionA, Vector3 runtimePositionB)
        {
            AbsoluteUniversePosition a = AbsoluteUniversePosition.FromRuntimePosition(runtimePositionA);
            AbsoluteUniversePosition b = AbsoluteUniversePosition.FromRuntimePosition(runtimePositionB);
            return AbsoluteUniversePosition.DistanceSq(in a, in b);
        }

        private bool TryProjectRayToPanel(
            float3 rayOriginWs,
            float3 rayDirectionWs,
            float maxDistance,
            bool rayDirectionIsNormalized,
            out float2 canvasPos,
            out float3 localHit,
            out float3 worldHit)
        {
            canvasPos = float2.zero;
            localHit = float3.zero;
            worldHit = float3.zero;

            float3 rayDirection = rayDirectionWs;
            float directionLengthSq = 1f;
            if (!rayDirectionIsNormalized)
            {
                directionLengthSq = math.lengthsq(rayDirection);
                if (directionLengthSq <= 0.0001f)
                    return false;
            }

            float3 panelNormal = _panelData.PanelNormal;
            float3 panelOrigin = _panelData.LocalToWorld.c3.xyz;
            float denom = math.dot(rayDirection, panelNormal);
            if (math.abs(denom) < 0.01f)
                return false;

            float planeDistance = math.dot(panelOrigin - rayOriginWs, panelNormal) * math.rcp(denom);
            float maxDistanceSafe = math.max(0.001f, maxDistance);
            float maxDistanceSq = maxDistanceSafe * maxDistanceSafe;
            float planeDistanceSq = planeDistance * planeDistance;
            float travelDistanceSq = rayDirectionIsNormalized ? planeDistanceSq : planeDistanceSq * directionLengthSq;
            if (planeDistance < 0f || travelDistanceSq > maxDistanceSq)
                return false;

            worldHit = rayOriginWs + rayDirection * planeDistance;
            localHit = math.transform(_panelData.WorldToLocal, worldHit);
            return TryProjectLocalHitToCanvas(localHit, out canvasPos);
        }

        private bool TryProjectLocalHitToCanvas(float3 localHit, out float2 canvasPos)
        {
            float2 uv = new float2(
                (localHit.x + _panelData.HalfSize.x) * _panelData.InvCanvasSize.x,
                (localHit.y + _panelData.HalfSize.y) * _panelData.InvCanvasSize.y);

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

        private bool TryResolveFingerInteraction(
            out float2 canvasPos,
            out float3 localHit,
            out DiegeticPanelInputEventType eventType,
            out bool showCursor)
        {
            canvasPos = float2.zero;
            localHit = float3.zero;
            eventType = DiegeticPanelInputEventType.None;
            showCursor = false;

            if (interactionMode == PanelInteractionMode.RaycastOnly)
            {
                if (_fingerPressedLastFrame)
                    return ResolveFingerRelease(out canvasPos, out localHit, out eventType);

                _activeFingerIndex = -1;
                return false;
            }

            if (_fingertipBindingMask == 0u)
            {
                if (_fingerPressedLastFrame)
                    return ResolveFingerRelease(out canvasPos, out localHit, out eventType);

                return false;
            }

            float pressDistance = math.max(0.001f, fingerPressDistance);
            float releaseDistance = math.max(pressDistance, fingerReleaseDistance);
            float hoverDistance = math.max(releaseDistance, fingerHoverDistance);
            int bestIndex = -1;
            float bestDistance = float.MaxValue;
            float3 bestLocalHit = float3.zero;
            int fingertipCount = fingertipTransforms == null
                ? 0
                : math.min(fingertipTransforms.Length, MaxFingerSlots);

            for (int i = 0; i < fingertipCount; i++)
            {
                if ((_fingertipBindingMask & (1u << i)) == 0u)
                    continue;

                Transform fingertip = fingertipTransforms[i];
                if (fingertip == null)
                    continue;

                float3 candidateLocalHit = math.transform(_panelData.WorldToLocal, fingertip.position);
                if (!IsInsidePanelXY(candidateLocalHit.xy))
                    continue;

                float surfaceDistance = math.abs(candidateLocalHit.z);
                if (surfaceDistance <= hoverDistance && surfaceDistance < bestDistance)
                {
                    bestDistance = surfaceDistance;
                    bestIndex = i;
                    bestLocalHit = candidateLocalHit;
                }
            }

            if (bestIndex < 0)
            {
                if (_fingerPressedLastFrame)
                    return ResolveFingerRelease(out canvasPos, out localHit, out eventType);

                _activeFingerIndex = -1;
                return false;
            }

            bool heldFinger = _fingerPressedLastFrame && (_activeFingerIndex < 0 || _activeFingerIndex == bestIndex);
            bool pressedNow = bestDistance <= pressDistance || (heldFinger && bestDistance <= releaseDistance);
            if (!TryProjectLocalHitToCanvas(bestLocalHit, out canvasPos))
            {
                if (_fingerPressedLastFrame)
                    return ResolveFingerRelease(out canvasPos, out localHit, out eventType);

                return false;
            }

            localHit = bestLocalHit;
            showCursor = true;
            _activeFingerIndex = bestIndex;
            _lastFingerCanvasPosition = canvasPos;
            _lastFingerLocalHit = localHit;

            eventType = pressedNow
                ? (_fingerPressedLastFrame ? DiegeticPanelInputEventType.Hold : DiegeticPanelInputEventType.Down)
                : DiegeticPanelInputEventType.Hover;

            _fingerPressedLastFrame = pressedNow;
            return true;
        }

        private bool ResolveFingerRelease(out float2 canvasPos, out float3 localHit, out DiegeticPanelInputEventType eventType)
        {
            canvasPos = _lastFingerCanvasPosition;
            localHit = _lastFingerLocalHit;
            eventType = DiegeticPanelInputEventType.Up;
            _fingerPressedLastFrame = false;
            _activeFingerIndex = -1;
            return true;
        }

        private bool IsInsidePanelXY(float2 localXY)
        {
            return localXY.x >= -_panelData.HalfSize.x &&
                   localXY.x <= _panelData.HalfSize.x &&
                   localXY.y >= -_panelData.HalfSize.y &&
                   localXY.y <= _panelData.HalfSize.y;
        }

        private void UpdateCursor(float3 localHit, float deltaTime)
        {
            if (cursorTransform == null)
                return;

            float2 cursorMarginLocal = math.min(math.max(new float2(cursorMargin.x, cursorMargin.y), float2.zero), _panelData.HalfSize);
            float2 clampedLocalXY = math.clamp(
                localHit.xy,
                -_panelData.HalfSize + cursorMarginLocal,
                _panelData.HalfSize - cursorMarginLocal);

            float3 cursorLocal = new float3(clampedLocalXY.x, clampedLocalXY.y, cursorHoverOffset);
            float3 cursorTargetWorld = math.transform(_panelData.LocalToWorld, cursorLocal);

            if (!_cursorStateInitialized)
            {
                _smoothedCursorWorld = cursorTargetWorld;
                _cursorStateInitialized = true;
            }
            else
            {
                float alpha = FastDecayBlend(cursorSmoothingSpeed, math.max(0.0001f, deltaTime));
                _smoothedCursorWorld = math.lerp(_smoothedCursorWorld, cursorTargetWorld, alpha);
            }

            float3 panelNormal = _panelData.PanelNormal;
            float3 panelUp = _panelData.PanelUp;
            quaternion rotation = quaternion.LookRotationSafe(-panelNormal, panelUp);

            cursorTransform.SetPositionAndRotation(
                _smoothedCursorWorld,
                new Quaternion(rotation.value.x, rotation.value.y, rotation.value.z, rotation.value.w));
            SetCursorVisible(true);
        }

        private static float FastDecayBlend(float sharpness, float deltaTime)
        {
            if (deltaTime <= 0f)
                return 0f;

            float x = math.max(0f, sharpness) * deltaTime;
            if (x >= 3.5f)
                return 1f;

            return math.saturate((12f * x) * math.rcp(12f + (6f * x) + (x * x)));
        }

        private void QueueInputEventsFromInputState(float2 canvasPos)
        {
            DiegeticPanelInputEventType eventType = DiegeticPanelInputEventType.Hover;
            float2 analogDelta = float2.zero;

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
                analogDelta = new float2(state.ScrollDelta.x, state.ScrollDelta.y);
                if (math.lengthsq(analogDelta) > 0.000001f)
                    eventType |= DiegeticPanelInputEventType.Scroll;
            }
            else
            {
                _wasPressedLastFrame = false;
            }

            QueueInputEvent(canvasPos, eventType, analogDelta);
        }

        private void QueueInputEvent(float2 canvasPos, DiegeticPanelInputEventType eventType)
        {
            QueueInputEvent(canvasPos, eventType, float2.zero);
        }

        private void QueueInputEvent(float2 canvasPos, DiegeticPanelInputEventType eventType, float2 analogDelta)
        {
            if (eventType == DiegeticPanelInputEventType.None)
                return;

            EnqueueInputEvent(new DiegeticPanelInputEvent
            {
                PanelId = panelId,
                CanvasHitPoint = canvasPos,
                AnalogDelta = analogDelta,
                EventType = eventType,
                Timestamp = _tickUnscaledTime
            });
        }

        private void EnqueueInputEvent(DiegeticPanelInputEvent inputEvent)
        {
            if (_inputEventCount >= InputEventCapacity)
            {
                _inputEventHead = (_inputEventHead + 1) & InputEventMask;
                _inputEventCount--;
            }

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
            _fingerPressedLastFrame = false;
            _activeFingerIndex = -1;
            _inputEventHead = 0;
            _inputEventTail = 0;
            _inputEventCount = 0;
            _cursorStateInitialized = false;
            SetCursorVisible(false);
        }

        private bool CanUseDesktopPlaneFallback()
        {
            if (interactionMode == PanelInteractionMode.RaycastOnly)
                return true;

            if (interactionMode == PanelInteractionMode.PhysicalFingerOnly)
                return false;

            if (_fingertipBindingMask != 0u)
                return false;

            if (HectonXRRuntimeState.IsXRActive)
                return false;

            return allowDesktopRayFallbackWithoutFingers;
        }

        private void RefreshFingertipBindingMask()
        {
            _cachedFingertipTransforms = fingertipTransforms;
            _fingertipBindingMask = 0u;
            if (fingertipTransforms == null)
                return;

            int count = math.min(fingertipTransforms.Length, MaxFingerSlots);
            for (int i = 0; i < count; i++)
            {
                if (fingertipTransforms[i] != null)
                    _fingertipBindingMask |= 1u << i;
            }
        }

        private Camera ResolveInteractionCamera()
        {
            if (interactionCamera != null && interactionCamera.isActiveAndEnabled)
            {
                if (!_resolvedInteractionCameraFromExplicit || !ReferenceEquals(_resolvedInteractionCamera, interactionCamera))
                    CacheInteractionCamera(interactionCamera, fromExplicit: true);

                return _resolvedInteractionCamera;
            }

            if (_resolvedInteractionCameraFromExplicit)
                CacheInteractionCamera(null, fromExplicit: false);

            if (_resolvedInteractionCamera != null && _resolvedInteractionCamera.isActiveAndEnabled)
                return _resolvedInteractionCamera;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            Camera playerCamera = playerContext != null ? playerContext.PlayerCamera : null;
            CacheInteractionCamera(playerCamera != null && playerCamera.isActiveAndEnabled ? playerCamera : null, fromExplicit: false);

            return _resolvedInteractionCamera;
        }

        private void CacheInteractionCamera(Camera camera, bool fromExplicit)
        {
            _resolvedInteractionCamera = camera;
            _resolvedInteractionCameraTransform = camera != null ? camera.transform : null;
            _resolvedInteractionCameraFromExplicit = fromExplicit;
        }

        private void SetCursorVisible(bool visible)
        {
            if (cursorTransform == null || (_cursorVisibilityInitialized && _cursorVisible == visible))
                return;

            if (_cursorCanvasGroup != null)
            {
                _cursorCanvasGroup.alpha = visible ? 1f : 0f;
                _cursorCanvasGroup.blocksRaycasts = false;
                _cursorCanvasGroup.interactable = false;
            }

            if (_cursorGraphic != null)
                _cursorGraphic.enabled = visible;

            if (_cursorRenderer != null)
                _cursorRenderer.enabled = visible;

            if (_cursorCollider != null)
                _cursorCollider.enabled = visible;

            _cursorVisible = visible;
            _cursorVisibilityInitialized = true;
        }

        private void TryRegisterTick()
        {
            if (_tickRegistered || !Application.isPlaying)
            {
                RefreshLateFrameRegistration();
                return;
            }

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _tickRegistered = GlobalRegistry.Updatables.Contains(this);
            RefreshLateFrameRegistration();
        }

        private void RefreshLateFrameRegistration()
        {
            bool shouldRegisterLateFrame =
                enablePhosphorDecay &&
                _panelRenderTexture != null &&
                _ownsPanelRenderTexture &&
                panelCamera != null &&
                !_presentationPausedByOwner &&
                isActiveAndEnabled &&
                Application.isPlaying &&
                GlobalRegistry.Dispatcher != null;

            if (shouldRegisterLateFrame)
            {
                if (_lateFrameRegistered)
                    return;

                GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.UI);
                _lateFrameRegistered = SystemDispatcher.GetLateFrameLane(PriorityLayer.UI).Contains(this);
                return;
            }

            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _lateFrameRegistered = false;
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

        private void UnregisterTick()
        {
            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _lateFrameRegistered = false;
            }

            if (_tickRegistered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
                _tickRegistered = false;
            }
        }

        private void RegisterRenderPipelineHook()
        {
            if (_renderPipelineHookRegistered)
                return;

            RenderPipelineManager.endCameraRendering += HandleEndCameraRendering;
            _renderPipelineHookRegistered = true;
        }

        private void UnregisterRenderPipelineHook()
        {
            if (!_renderPipelineHookRegistered)
                return;

            RenderPipelineManager.endCameraRendering -= HandleEndCameraRendering;
            _renderPipelineHookRegistered = false;
        }

        private void HandleEndCameraRendering(ScriptableRenderContext context, Camera renderedCamera)
        {
            if (renderedCamera != panelCamera)
                return;

            CompositePhosphorFrame();
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

        private static float3 ResolveSafePanelAxis(float3 axis, float3 fallback)
        {
            float lengthSq = math.lengthsq(axis);
            if (lengthSq <= 0.0001f || !math.all(math.isfinite(axis)))
                return fallback;

            return axis * math.rsqrt(lengthSq);
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

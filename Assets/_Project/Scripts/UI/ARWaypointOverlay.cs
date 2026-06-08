using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.World;
using TMPro;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    /// <summary>
    /// Projects pooled waypoint markers onto the diegetic HUD plane using explicit camera-plane math.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/AR Waypoint Overlay")]
    public sealed class ARWaypointOverlay : MonoBehaviour, ILateFrameTickable, ISlowTickable, IOriginShiftListener, IARWaypointService, IGlobalRegistryHotSwapListener
    {
        private const int MaxExternalWaypoints = 16;
        private const int MaxWaypoints = MaxExternalWaypoints;
        private const int MaximumLabelCharacters = 48;
        private const float ScreenMargin = 54f;
        private const float HiddenAlpha = 0f;
        private const float VisibleAlpha = 0.96f;
        private const float OccludedAlpha = 0.32f;
        private const float EdgeAlpha = 0.74f;
        private const float MarkerSize = 18f;
        private const float OutlineSize = 26f;
        private const float EdgeMarkerWidth = 22f;
        private const float EdgeMarkerHeight = 10f;
        private const float EdgeOutlineWidth = 30f;
        private const float EdgeOutlineHeight = 16f;
        private const float ProjectionDepthEpsilon = 0.0001f;
        private const float CinematicOcclusionNearDistanceMeters = 42f;
        private const float CinematicOcclusionFarDistanceMeters = 128f;
        private const float CinematicOcclusionSideWeight = 0.62f;
        private const float CinematicOcclusionBehindDot = -0.05f;
        private const double WaypointSolveBudgetWarningMilliseconds = 0.2d;
        private const int WaypointPerformanceWarningCooldownFrames = 90;
        private const int WaypointSolveTelemetryCadenceFrames = 16;
        private const string RootName = "ARWaypointOverlay";
        private const string SlotFillName = "Fill";
        private const string SlotOutlineName = "Outline";
        private const string SlotLabelName = "Label";
        private const string DefaultExternalLabel = "WAYPOINT";
        private const int EdgeRotationUp = 0;
        private const int EdgeRotationRight = 1;
        private const int EdgeRotationLeft = 2;
        private const int EdgeRotationDown = 3;
        private const int EdgeRotationUpRight = 4;
        private const int EdgeRotationDownRight = 5;
        private const int EdgeRotationUpLeft = 6;
        private const int EdgeRotationDownLeft = 7;
        private const uint WaypointLabelHashSeed = 2166136261u;
        private const uint WaypointLabelHashPrime = 16777619u;

        private static readonly uint _WaypointSolveBudgetWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("HUD_AR_WAYPOINT_SOLVE_OVER_BUDGET"));
        private static readonly uint _WaypointSolveBudgetContextHash = unchecked((uint)Hecton.Localization.LocHash.Compute("ARWaypointOverlay.Solve"));
        private static readonly uint DefaultExternalLabelHash = ComputeWaypointLabelHash(DefaultExternalLabel.AsSpan());
        private static readonly Color RelayColor = new Color(0.64f, 0.94f, 0.98f, 0.96f);
        private static readonly Color OccludedColor = new Color(0.94f, 0.94f, 0.94f, 0.62f);
        private static ARWaypointOverlay s_activeRuntimeInstance;
        private static IARWaypointService s_cachedWaypointService;
        private static bool s_stencilRenderGraphActive;
        // COLD ALLOC: Quaternion[8] - precomputed edge-marker rotations, replaces Tick-path rotation construction - owner: ARWaypointOverlay
        private static readonly Quaternion[] s_edgeRotationLut =
        {
            Quaternion.identity,
            new Quaternion(0f, 0f, -0.70710677f, 0.70710677f),
            new Quaternion(0f, 0f, 0.70710677f, 0.70710677f),
            new Quaternion(0f, 0f, 1f, 0f),
            new Quaternion(0f, 0f, -0.38268343f, 0.9238795f),
            new Quaternion(0f, 0f, -0.9238795f, 0.38268343f),
            new Quaternion(0f, 0f, 0.38268343f, 0.9238795f),
            new Quaternion(0f, 0f, 0.9238795f, 0.38268343f)
        };
        // COLD ALLOC: string[16] - pre-baked waypoint slot names, avoids runtime interpolation - owner: ARWaypointOverlay
        private static readonly string[] s_waypointSlotNames =
        {
            "Waypoint_0",
            "Waypoint_1",
            "Waypoint_2",
            "Waypoint_3",
            "Waypoint_4",
            "Waypoint_5",
            "Waypoint_6",
            "Waypoint_7",
            "Waypoint_8",
            "Waypoint_9",
            "Waypoint_10",
            "Waypoint_11",
            "Waypoint_12",
            "Waypoint_13",
            "Waypoint_14",
            "Waypoint_15"
        };

        private struct ExternalWaypoint
        {
            public int Id;
            public Transform Target;
            public AbsoluteUniversePosition PositionAup;
            public Vector3 PresentationPosition;
            public uint LabelHash;
            public int LabelOffset;
            public int LabelLength;
            public uint LabelRevision;
            public Color Color;
            public bool Active;
            public bool HasLabel;
            public bool UseTransform;
            public bool HasPositionAup;
        }

        private struct RuntimeWaypoint
        {
            public AbsoluteUniversePosition PositionAup;
            public uint LabelHash;
            public int LabelOffset;
            public int LabelLength;
            public int LabelSlotIndex;
            public uint LabelRevision;
            public Color Color;
            public bool Active;
            public bool HasLabel;
            public bool Occluded;
        }

        private struct WaypointSlot
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public RectTransform FillRect;
            public RectTransform OutlineRect;
            public Image Fill;
            public Image Outline;
            public TextMeshProUGUI Label;
            public uint CachedLabelHash;
            public int CachedLabelLength;
            public int CachedLabelSlotIndex;
            public uint CachedLabelRevision;
            public int CachedAnchoredX;
            public int CachedAnchoredY;
            public int CachedRotationIndex;
            public byte CachedAlphaByte;
            public bool CachedEdgeState;
            public bool CachedFillEnabled;
            public bool CachedOutlineEnabled;
            public bool HasTransformState;
            public bool HasAlphaState;
            public bool HasImageState;
            public Color CachedFillColor;
            public Color CachedOutlineColor;
        }

        [StructLayout(LayoutKind.Explicit, Size = 112)]
        private struct WaypointProjectionFrame
        {
            [FieldOffset(0)]
            public AbsoluteUniversePosition CameraAup;

            [FieldOffset(48)]
            public float3 CameraRight;

            [FieldOffset(60)]
            public float3 CameraUp;

            [FieldOffset(72)]
            public float3 CameraForward;

            [FieldOffset(84)]
            public float PlaneDistance;

            [FieldOffset(88)]
            public float ScaleX;

            [FieldOffset(92)]
            public float ScaleY;

            [FieldOffset(96)]
            public float HalfWidth;

            [FieldOffset(100)]
            public float HalfHeight;

            [FieldOffset(104)]
            public uint IsValid;

            [FieldOffset(108)]
            private uint _pad0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 80)]
        public struct StencilTargetSourceDTO
        {
            [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
            [FieldOffset(48)] public float4 Color;
            [FieldOffset(64)] public uint Flags;
            [FieldOffset(68)] public uint StableId;
            [FieldOffset(72)] public uint Reserved0;
            [FieldOffset(76)] public uint Reserved1;
        }

        // COLD ALLOC: ExternalWaypoint[8] - external AR waypoint registry - owner: ARWaypointOverlay
        private readonly ExternalWaypoint[] _externalWaypoints = new ExternalWaypoint[MaxExternalWaypoints];
        // COLD ALLOC: RuntimeWaypoint[16] - projected waypoint payloads - owner: ARWaypointOverlay
        private readonly RuntimeWaypoint[] _runtimeWaypoints = new RuntimeWaypoint[MaxWaypoints];
        // COLD ALLOC: WaypointSlot[16] - pooled waypoint UI markers - owner: ARWaypointOverlay
        private readonly WaypointSlot[] _slots = new WaypointSlot[MaxWaypoints];
        // COLD ALLOC: char[48] - transient zero-GC waypoint label formatter buffer - owner: ARWaypointOverlay
        private readonly char[] _labelCharBuffer = new char[MaximumLabelCharacters];
        // COLD ALLOC: char[768] - fixed external waypoint label bank, 16 slots * 48 chars - owner: ARWaypointOverlay
        private readonly char[] _externalWaypointLabelBuffer = new char[MaxExternalWaypoints * MaximumLabelCharacters];

        private bool _registeredWaypointService;
        private bool _registeredTick;
        private bool _registeredSlowTick;
        private bool _hotSwapListenerRegistered;
        private bool _uiBuilt;
        private int _waypointCount;
        private int _renderedSlotCount;
        private int _nextWaypointPerformanceWarningFrame;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private Canvas _targetCanvas;
        private RectTransform _targetCanvasRect;
        private RectTransform _root;
        private Camera _viewCamera;
        private Transform _playerTransform;
        [SerializeField]
        private RectTransform _authoredRoot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_activeRuntimeInstance = null;
            s_cachedWaypointService = null;
            s_stencilRenderGraphActive = false;
        }

        public static void SetStencilRenderGraphActive(bool active)
        {
            if (s_stencilRenderGraphActive == active)
                return;

            s_stencilRenderGraphActive = active;
            if (active && s_activeRuntimeInstance != null)
            {
                s_activeRuntimeInstance.CaptureExternalWaypointAupsCold();
                s_activeRuntimeInstance.HideRenderedSlots();
            }
        }

        public static int CopyStencilTargetSources(NativeArray<StencilTargetSourceDTO> destination, int maxCount)
        {
            if (!destination.IsCreated || maxCount <= 0)
                return 0;

            ARWaypointOverlay instance = s_activeRuntimeInstance;
            if (instance == null || !instance.isActiveAndEnabled)
                return 0;

            int capacity = math.min(destination.Length, maxCount);
            if (capacity <= 0)
                return 0;

            return instance.CopyRuntimeTargetsForStencil(destination, capacity);
        }

        public static int CopyStencilTargetSources(Span<StencilTargetSourceDTO> destination, int maxCount)
        {
            if (destination.Length <= 0 || maxCount <= 0)
                return 0;

            ARWaypointOverlay instance = s_activeRuntimeInstance;
            if (instance == null || !instance.isActiveAndEnabled)
                return 0;

            int capacity = math.min(destination.Length, maxCount);
            if (capacity <= 0)
                return 0;

            return instance.CopyRuntimeTargetsForStencil(destination, capacity);
        }

        /// <summary>
        /// Register or refresh an external waypoint bound to a transform target.
        /// </summary>
        public static void SetWaypoint(int id, Transform target, string label, Color color)
        {
            IARWaypointService service = CacheWaypointServiceCold();
            if (service == null)
                return;

            service.SetWaypoint(id, target, label, color);
        }

        /// <summary>
        /// Register or refresh an external waypoint bound to a transform target using caller-owned text identity.
        /// </summary>
        public static void SetWaypoint(int id, Transform target, uint labelHash, ReadOnlySpan<char> label, Color color)
        {
            IARWaypointService service = CacheWaypointServiceCold();
            if (service == null)
                return;

            service.SetWaypoint(id, target, labelHash, label, color);
        }

        /// <summary>
        /// Register or refresh an external waypoint bound to a runtime-space position.
        /// </summary>
        public static void SetWaypoint(int id, Vector3 worldPosition, string label, Color color)
        {
            IARWaypointService service = CacheWaypointServiceCold();
            if (service == null)
                return;

            service.SetWaypoint(id, worldPosition, label, color);
        }

        /// <summary>
        /// Register or refresh an external waypoint bound to a runtime-space position using caller-owned text identity.
        /// </summary>
        public static void SetWaypoint(int id, Vector3 worldPosition, uint labelHash, ReadOnlySpan<char> label, Color color)
        {
            IARWaypointService service = CacheWaypointServiceCold();
            if (service == null)
                return;

            service.SetWaypoint(id, worldPosition, labelHash, label, color);
        }

        /// <summary>
        /// Remove a previously registered external waypoint.
        /// </summary>
        public static void ClearWaypoint(int id)
        {
            IARWaypointService service = CacheWaypointServiceCold();
            if (service == null)
                return;

            service.ClearWaypoint(id);
        }

        private static IARWaypointService CacheWaypointServiceCold()
        {
            IARWaypointService service = s_cachedWaypointService;
            if (service != null)
                return service;

            service = GlobalRegistry.ARWaypoints;
            s_cachedWaypointService = service;
            return service;
        }

        private void OnEnable()
        {
            s_activeRuntimeInstance = this;
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            TryRegisterWaypointService();
            ResolveOwners(allowHierarchySearch: true);
            if (!s_stencilRenderGraphActive)
                EnsureUiBuilt(allowCreate: true);
            else
                HideRenderedSlots();
            HectonFloatingOrigin.RegisterListener(this);
            RegisterToTickManager();
            RegisterToSlowTickManager();
        }

        private void Start()
        {
            ResolveOwners(allowHierarchySearch: true);
            if (!s_stencilRenderGraphActive)
                EnsureUiBuilt(allowCreate: true);
            else
                HideRenderedSlots();
            RegisterToTickManager();
            RegisterToSlowTickManager();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            UnregisterWaypointService();
            HectonFloatingOrigin.UnregisterListener(this);
            UnregisterFromTickManager();
            UnregisterFromSlowTickManager();
            HideAllSlots();
            if (ReferenceEquals(s_activeRuntimeInstance, this))
                s_activeRuntimeInstance = null;
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            UnregisterWaypointService();
            HectonFloatingOrigin.UnregisterListener(this);
            UnregisterFromTickManager();
            UnregisterFromSlowTickManager();
            if (ReferenceEquals(s_activeRuntimeInstance, this))
                s_activeRuntimeInstance = null;
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            bool sampleSolveCost = ShouldSampleWaypointSolveCost();
            long solveStartTimestamp = sampleSolveCost ? Stopwatch.GetTimestamp() : 0L;
            if (s_stencilRenderGraphActive)
            {
                CollectRuntimeWaypoints();
                HideRenderedSlots();
                PublishWaypointSolveWarningIfNeeded(sampleSolveCost, solveStartTimestamp);
                return;
            }

            if (!_uiBuilt || _root == null || _targetCanvas == null)
            {
                HideRenderedSlots();
                return;
            }

            CollectRuntimeWaypoints();
            RenderWaypoints();
            PublishWaypointSolveWarningIfNeeded(sampleSolveCost, solveStartTimestamp);
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            long solveStartTimestamp = Stopwatch.GetTimestamp();
            CollectRuntimeWaypoints();
            RefreshOcclusionStates();
            PublishWaypointSolveWarningIfNeeded(true, solveStartTimestamp);
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            float shiftSqrMagnitude = shiftOffset.sqrMagnitude;
            if (!IsFiniteRuntimeVector(shiftOffset) || !math.isfinite(shiftSqrMagnitude))
            {
                HideRenderedSlots();
                return;
            }

            if (shiftSqrMagnitude <= 0.000001f)
                return;

            RebaseExternalRuntimeWaypointPresentation(-shiftOffset);
            _targetCanvas = null;
            _targetCanvasRect = null;
            _viewCamera = null;
            _uiBuilt = false;
            _root = null;
            ResolveOwners(allowHierarchySearch: true);
            if (!s_stencilRenderGraphActive)
                EnsureUiBuilt(allowCreate: true);
        }

        bool IARWaypointService.IsInitialized => s_stencilRenderGraphActive || (_uiBuilt && _root != null && _targetCanvas != null);

        void IARWaypointService.SetWaypoint(int id, Transform target, string label, Color color)
        {
            ReadOnlySpan<char> labelSpan = ReadOnlySpan<char>.Empty;
            if (label != null && label.Length > 0)
                labelSpan = label.AsSpan();
            SetExternalWaypointInternal(id, target, default, useTransform: true, ResolveLabelHash(labelSpan), labelSpan, color);
        }

        void IARWaypointService.SetWaypoint(int id, Transform target, uint labelHash, ReadOnlySpan<char> label, Color color)
        {
            SetExternalWaypointInternal(id, target, default, useTransform: true, labelHash, label, color);
        }

        void IARWaypointService.SetWaypoint(int id, Vector3 worldPosition, string label, Color color)
        {
            ReadOnlySpan<char> labelSpan = ReadOnlySpan<char>.Empty;
            if (label != null && label.Length > 0)
                labelSpan = label.AsSpan();
            SetExternalWaypointInternal(id, null, worldPosition, useTransform: false, ResolveLabelHash(labelSpan), labelSpan, color);
        }

        void IARWaypointService.SetWaypoint(int id, Vector3 worldPosition, uint labelHash, ReadOnlySpan<char> label, Color color)
        {
            SetExternalWaypointInternal(id, null, worldPosition, useTransform: false, labelHash, label, color);
        }

        void IARWaypointService.ClearWaypoint(int id)
        {
            ClearExternalWaypointInternal(id);
        }

        private void ResolveOwners(bool allowHierarchySearch)
        {
            if (s_stencilRenderGraphActive)
            {
                IPlayerRuntimeContext playerContext = _cachedPlayerContext;
                if (_viewCamera == null && playerContext != null)
                    _viewCamera = playerContext.PlayerCamera;
                if (_playerTransform == null && playerContext != null)
                    _playerTransform = playerContext.PlayerTransform;
                return;
            }

            if (allowHierarchySearch || _targetCanvas == null || _viewCamera == null)
            {
                SuitHUDV4CanvasOverlay overlay = ResolveProjectionOverlay();
                if (overlay != null)
                {
                    Canvas overlayCanvas = overlay.TargetCanvas;
                    if (!ReferenceEquals(_targetCanvas, overlayCanvas))
                    {
                        _targetCanvas = overlayCanvas;
                        _targetCanvasRect = _targetCanvas != null ? _targetCanvas.transform as RectTransform : null;
                        _uiBuilt = false;
                        _root = null;
                    }

                    Camera overlayCamera = overlay.ProjectionCamera != null ? overlay.ProjectionCamera : (_targetCanvas != null ? _targetCanvas.worldCamera : null);
                    if (overlayCamera != null)
                        _viewCamera = overlayCamera;
                }
            }

            if (_targetCanvas == null)
            {
                _targetCanvas = ResolveTargetCanvas();
                _targetCanvasRect = _targetCanvas != null ? _targetCanvas.transform as RectTransform : null;
            }

            if (_viewCamera == null)
            {
                IPlayerRuntimeContext playerContext = _cachedPlayerContext;
                if (_targetCanvas != null && _targetCanvas.worldCamera != null)
                    _viewCamera = _targetCanvas.worldCamera;
                else if (playerContext != null && playerContext.PlayerCamera != null)
                    _viewCamera = playerContext.PlayerCamera;
                else if (allowHierarchySearch && TryGetComponent(out Camera localCamera))
                    _viewCamera = localCamera;
                else if (allowHierarchySearch &&
                         GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                         playerTransform != null)
                {
                    if (playerContext != null && playerContext.PlayerCamera != null)
                        _viewCamera = playerContext.PlayerCamera;
                    else
                        playerTransform.TryGetComponent(out _viewCamera);
                }
            }

            if (_playerTransform == null && allowHierarchySearch)
                GameBootstrapper.TryGetCurrentPlayerTransform(out _playerTransform);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _cachedPlayerContext = currentService as IPlayerRuntimeContext;
                _playerTransform = _cachedPlayerContext != null ? _cachedPlayerContext.PlayerTransform : null;
                _viewCamera = _cachedPlayerContext != null ? _cachedPlayerContext.PlayerCamera : null;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.ARWaypointRuntime)
                s_cachedWaypointService = currentService as IARWaypointService;
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
            _cachedPlayerContext = GlobalRegistry.Player;
            if (_cachedPlayerContext != null && _playerTransform == null)
                _playerTransform = _cachedPlayerContext.PlayerTransform;
        }

        private bool TryResolveCameraAup(out AbsoluteUniversePosition cameraAup)
        {
            cameraAup = default;

            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext == null)
                return false;

            if (playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
            {
                cameraAup = snapshot.Aup;
                return cameraAup.IsFinite();
            }

            if (playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                movementState.PredictedAup.IsFinite())
            {
                cameraAup = movementState.PredictedAup;
                return true;
            }

            return false;
        }

        private bool TryResolvePresentationWaypointAup(Transform target, out AbsoluteUniversePosition waypointAup)
        {
            waypointAup = default;
            if (target == null)
                return false;

            return TryResolvePresentationWaypointAup(target.position, out waypointAup);
        }

        private bool TryResolvePresentationWaypointAup(Vector3 presentationPosition, out AbsoluteUniversePosition waypointAup)
        {
            waypointAup = default;
            if (_viewCamera == null ||
                !TryResolveCameraAup(out AbsoluteUniversePosition cameraAup))
            {
                return false;
            }

            Vector3 cameraPosition = _viewCamera.transform.position;
            double3 cameraLocalDelta = new double3(
                (double)presentationPosition.x - cameraPosition.x,
                (double)presentationPosition.y - cameraPosition.y,
                (double)presentationPosition.z - cameraPosition.z);
            if (!math.all(math.isfinite(cameraLocalDelta)))
                return false;

            waypointAup = AbsoluteUniversePosition.OffsetMeters(in cameraAup, cameraLocalDelta);
            return waypointAup.IsFinite();
        }

        private bool EnsureUiBuilt(bool allowCreate)
        {
            if (_uiBuilt)
                return true;

            if (!allowCreate || _targetCanvas == null)
                return false;

            RectTransform canvasRoot = HectonUIScaler.ResolveContentRoot(_targetCanvas);
            if (canvasRoot == null)
                return false;

            _root = _authoredRoot != null ? _authoredRoot : FindExistingChild(canvasRoot, RootName);
            if (_root == null)
                return false;

            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.SetAsLastSibling();

            for (int i = 0; i < _slots.Length; i++)
            {
                if (!TryBindSlot(i, _root, _viewCamera, out _slots[i]))
                {
                    ResetBoundSlots();
                    return false;
                }
            }

            _uiBuilt = true;
            HideAllSlots();
            return true;
        }

        private void CollectRuntimeWaypoints()
        {
            int count = 0;

            for (int i = 0; i < _externalWaypoints.Length && count < _runtimeWaypoints.Length; i++)
            {
                ExternalWaypoint externalWaypoint = _externalWaypoints[i];
                if (!externalWaypoint.Active)
                    continue;

                bool hasWaypointAup;
                if (externalWaypoint.UseTransform)
                {
                    if (externalWaypoint.Target == null)
                    {
                        externalWaypoint.Active = false;
                        externalWaypoint.HasPositionAup = false;
                        _externalWaypoints[i] = externalWaypoint;
                        continue;
                    }

                    if (s_stencilRenderGraphActive)
                    {
                        hasWaypointAup = externalWaypoint.HasPositionAup &&
                                         externalWaypoint.PositionAup.IsFinite();
                    }
                    else
                    {
                        hasWaypointAup = TryResolvePresentationWaypointAup(externalWaypoint.Target, out externalWaypoint.PositionAup);
                        externalWaypoint.HasPositionAup = hasWaypointAup;
                    }
                }
                else
                {
                    if (s_stencilRenderGraphActive)
                    {
                        hasWaypointAup = externalWaypoint.HasPositionAup &&
                                         externalWaypoint.PositionAup.IsFinite();
                    }
                    else
                    {
                        hasWaypointAup = TryResolvePresentationWaypointAup(externalWaypoint.PresentationPosition, out externalWaypoint.PositionAup);
                        externalWaypoint.HasPositionAup = hasWaypointAup;
                    }
                }

                _externalWaypoints[i] = externalWaypoint;
                if (!hasWaypointAup)
                    continue;

                RuntimeWaypoint runtimeWaypoint = _runtimeWaypoints[count];
                runtimeWaypoint.PositionAup = externalWaypoint.PositionAup;
                runtimeWaypoint.LabelHash = externalWaypoint.HasLabel ? externalWaypoint.LabelHash : DefaultExternalLabelHash;
                runtimeWaypoint.LabelOffset = externalWaypoint.LabelOffset;
                runtimeWaypoint.LabelLength = externalWaypoint.HasLabel ? externalWaypoint.LabelLength : DefaultExternalLabel.Length;
                runtimeWaypoint.LabelSlotIndex = i;
                runtimeWaypoint.LabelRevision = externalWaypoint.LabelRevision;
                runtimeWaypoint.Color = externalWaypoint.Color.a <= 0f ? RelayColor : externalWaypoint.Color;
                runtimeWaypoint.Active = true;
                runtimeWaypoint.HasLabel = externalWaypoint.HasLabel;
                runtimeWaypoint.Occluded = count < _waypointCount && _runtimeWaypoints[count].Occluded;
                _runtimeWaypoints[count] = runtimeWaypoint;
                count++;
            }

            for (int i = count; i < _runtimeWaypoints.Length; i++)
                _runtimeWaypoints[i].Active = false;

            _waypointCount = count;
        }

        private int CopyRuntimeTargetsForStencil(NativeArray<StencilTargetSourceDTO> destination, int capacity)
        {
            int count = math.min(math.min(_waypointCount, _runtimeWaypoints.Length), capacity);
            for (int i = 0; i < count; i++)
            {
                RuntimeWaypoint waypoint = _runtimeWaypoints[i];
                if (!waypoint.Active)
                {
                    destination[i] = default;
                    continue;
                }

                Color color = waypoint.Color;
                destination[i] = new StencilTargetSourceDTO
                {
                    PositionAup = waypoint.PositionAup,
                    Color = new float4(color.r, color.g, color.b, color.a),
                    Flags = waypoint.Occluded ? 3u : 1u,
                    StableId = unchecked((uint)(i + 1))
                };
            }

            for (int i = count; i < capacity; i++)
                destination[i] = default;

            return count;
        }

        private int CopyRuntimeTargetsForStencil(Span<StencilTargetSourceDTO> destination, int capacity)
        {
            int count = math.min(math.min(_waypointCount, _runtimeWaypoints.Length), capacity);
            for (int i = 0; i < count; i++)
            {
                RuntimeWaypoint waypoint = _runtimeWaypoints[i];
                if (!waypoint.Active)
                {
                    destination[i] = default;
                    continue;
                }

                Color color = waypoint.Color;
                destination[i] = new StencilTargetSourceDTO
                {
                    PositionAup = waypoint.PositionAup,
                    Color = new float4(color.r, color.g, color.b, color.a),
                    Flags = waypoint.Occluded ? 3u : 1u,
                    StableId = unchecked((uint)(i + 1))
                };
            }

            for (int i = count; i < capacity; i++)
                destination[i] = default;

            return count;
        }

        private void RenderWaypoints()
        {
            if (_root == null || _viewCamera == null || _waypointCount <= 0)
            {
                HideRenderedSlots();
                return;
            }

            WaypointProjectionFrame projectionFrame = ResolveWaypointProjectionFrame();
            if (projectionFrame.IsValid == 0u)
            {
                HideRenderedSlots();
                return;
            }

            for (int i = 0; i < _waypointCount; i++)
            {
                RuntimeWaypoint waypoint = _runtimeWaypoints[i];
                if (!waypoint.Active)
                {
                    HideSlot(i);
                    continue;
                }

                if (!TryProjectWaypointOntoHudPlane(
                        in waypoint.PositionAup,
                        in projectionFrame,
                        out Vector2 anchoredPosition,
                        out Vector2 clampDirection,
                        out bool clampedToEdge,
                        out float visibility01))
                {
                    HideSlot(i);
                    continue;
                }

                if (visibility01 <= 0.0001f)
                {
                    HideSlot(i);
                    continue;
                }

                WaypointSlot slot = _slots[i];
                if (slot.Root == null || slot.Group == null || slot.Fill == null || slot.Outline == null || slot.Label == null)
                    continue;

                ApplySlotTransform(ref slot, anchoredPosition, clampedToEdge, clampDirection);

                if (slot.CachedEdgeState != clampedToEdge)
                {
                    ApplySlotIconState(ref slot, clampedToEdge);
                    slot.CachedEdgeState = clampedToEdge;
                }

                bool useOutlineOnly = waypoint.Occluded;
                float alpha = waypoint.Occluded
                    ? visibility01 * OccludedAlpha
                    : visibility01 * (clampedToEdge ? EdgeAlpha : VisibleAlpha);

                Color outlineColor = waypoint.Color;
                outlineColor.a = 0.22f;
                ApplySlotAlpha(ref slot, alpha);
                ApplySlotImageState(ref slot, !useOutlineOnly, true, waypoint.Color, useOutlineOnly ? OccludedColor : outlineColor);

                if (slot.CachedLabelHash != waypoint.LabelHash ||
                    slot.CachedLabelLength != waypoint.LabelLength ||
                    slot.CachedLabelSlotIndex != waypoint.LabelSlotIndex ||
                    slot.CachedLabelRevision != waypoint.LabelRevision)
                {
                    ReadOnlySpan<char> labelSpan = DefaultExternalLabel.AsSpan();
                    if (waypoint.HasLabel &&
                        waypoint.LabelLength > 0 &&
                        waypoint.LabelOffset >= 0 &&
                        waypoint.LabelOffset <= _externalWaypointLabelBuffer.Length - waypoint.LabelLength)
                    {
                        labelSpan = new ReadOnlySpan<char>(_externalWaypointLabelBuffer, waypoint.LabelOffset, waypoint.LabelLength);
                    }

                    ApplyLabelText(slot.Label, labelSpan);
                    slot.CachedLabelHash = waypoint.LabelHash;
                    slot.CachedLabelLength = waypoint.LabelLength;
                    slot.CachedLabelSlotIndex = waypoint.LabelSlotIndex;
                    slot.CachedLabelRevision = waypoint.LabelRevision;
                }

                _slots[i] = slot;
            }

            int previousRenderedSlotCount = _renderedSlotCount;
            for (int i = _waypointCount; i < previousRenderedSlotCount; i++)
                HideSlot(i);

            _renderedSlotCount = _waypointCount;
        }

        private void RefreshOcclusionStates()
        {
            if (_viewCamera == null)
                return;

            Transform cameraTransform = _viewCamera.transform;
            if (!TryResolveCameraAup(out AbsoluteUniversePosition cameraAup))
                return;

            Vector3 cameraForwardVector = cameraTransform.forward;
            float3 cameraForward = math.float3(cameraForwardVector.x, cameraForwardVector.y, cameraForwardVector.z);
            float nearDistanceSq = CinematicOcclusionNearDistanceMeters * CinematicOcclusionNearDistanceMeters;
            float farDistanceSq = CinematicOcclusionFarDistanceMeters * CinematicOcclusionFarDistanceMeters;
            float behindDotSq = CinematicOcclusionBehindDot * CinematicOcclusionBehindDot;
            float sideDotThreshold = 1f - CinematicOcclusionSideWeight;
            float sideDotThresholdSq = sideDotThreshold * sideDotThreshold;
            for (int i = 0; i < _waypointCount; i++)
            {
                RuntimeWaypoint waypoint = _runtimeWaypoints[i];
                if (!waypoint.Active)
                    continue;

                float3 delta = AupPrecisionMath.LocalDeltaFloat3Clamped(
                    waypoint.PositionAup.ToAbsoluteDouble3(),
                    cameraAup.ToAbsoluteDouble3(),
                    AupPrecisionMath.DefaultMaxLocalCastMeters,
                    float3.zero);
                float distanceSq = math.lengthsq(delta);
                if (distanceSq <= 0.01f)
                {
                    waypoint.Occluded = false;
                    _runtimeWaypoints[i] = waypoint;
                    continue;
                }

                float forwardDot = math.dot(cameraForward, delta);
                float forwardDotSq = forwardDot * forwardDot;
                bool behindCone = forwardDot < 0f && forwardDotSq >= distanceSq * behindDotSq;
                bool sideCone = forwardDotSq <= distanceSq * sideDotThresholdSq;
                waypoint.Occluded =
                    behindCone ||
                    distanceSq >= farDistanceSq ||
                    (distanceSq >= nearDistanceSq && sideCone);
                _runtimeWaypoints[i] = waypoint;
            }
        }

        private static bool ShouldSampleWaypointSolveCost()
        {
            return WaypointSolveTelemetryCadenceFrames <= 1 ||
                   (Hecton8.Core.SystemDispatcher.CurrentFrameIndex & (WaypointSolveTelemetryCadenceFrames - 1)) == 0;
        }

        private void PublishWaypointSolveWarningIfNeeded(bool hasSample, long startTimestamp)
        {
            if (!hasSample)
                return;

            long elapsedTicks = Stopwatch.GetTimestamp() - startTimestamp;
            double elapsedMilliseconds = elapsedTicks * 1000.0d / Stopwatch.Frequency;
            if (elapsedMilliseconds <= WaypointSolveBudgetWarningMilliseconds ||
                Hecton8.Core.SystemDispatcher.CurrentFrameIndex < _nextWaypointPerformanceWarningFrame)
                return;

            GlobalTelemetryBus.PublishPerformanceWarning(
                _WaypointSolveBudgetWarningHash,
                _WaypointSolveBudgetContextHash,
                (float)elapsedMilliseconds);
            _nextWaypointPerformanceWarningFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex + WaypointPerformanceWarningCooldownFrames;
        }

        private bool TryProjectWaypointOntoHudPlane(
            in AbsoluteUniversePosition waypointAup,
            in WaypointProjectionFrame projectionFrame,
            out Vector2 anchoredPosition,
            out Vector2 clampDirection,
            out bool clampedToEdge,
            out float visibility01)
        {
            anchoredPosition = Vector2.zero;
            clampDirection = Vector2.up;
            clampedToEdge = false;
            visibility01 = 0f;

            if (projectionFrame.IsValid == 0u)
                return false;

            float3 deltaAup = AupPrecisionMath.LocalDeltaFloat3Clamped(
                waypointAup.ToAbsoluteDouble3(),
                projectionFrame.CameraAup.ToAbsoluteDouble3(),
                AupPrecisionMath.DefaultMaxLocalCastMeters,
                float3.zero);
            float viewDepth = math.dot(projectionFrame.CameraForward, deltaAup);
            if (projectionFrame.PlaneDistance <= ProjectionDepthEpsilon)
                return false;

            float depthForProjection = math.abs(viewDepth) > ProjectionDepthEpsilon
                ? viewDepth
                : (viewDepth >= 0f ? ProjectionDepthEpsilon : -ProjectionDepthEpsilon);

            float projectedWorldX = math.dot(projectionFrame.CameraRight, deltaAup) * (projectionFrame.PlaneDistance / depthForProjection);
            float projectedWorldY = math.dot(projectionFrame.CameraUp, deltaAup) * (projectionFrame.PlaneDistance / depthForProjection);

            Vector2 projectedCanvasPosition;
            projectedCanvasPosition.x = projectedWorldX / projectionFrame.ScaleX;
            projectedCanvasPosition.y = projectedWorldY / projectionFrame.ScaleY;

            bool behindPlayer = viewDepth <= ProjectionDepthEpsilon;
            if (behindPlayer)
                projectedCanvasPosition = -projectedCanvasPosition;

            bool insideFrustum =
                !behindPlayer &&
                projectedCanvasPosition.x >= -projectionFrame.HalfWidth &&
                projectedCanvasPosition.x <= projectionFrame.HalfWidth &&
                projectedCanvasPosition.y >= -projectionFrame.HalfHeight &&
                projectedCanvasPosition.y <= projectionFrame.HalfHeight;

            if (insideFrustum)
            {
                anchoredPosition = projectedCanvasPosition;
                clampDirection = Vector2.up;
                visibility01 = 1f;
                return true;
            }

            clampDirection = ResolveApproxDirection(projectedCanvasPosition);

            float tx = math.abs(clampDirection.x) > ProjectionDepthEpsilon
                ? projectionFrame.HalfWidth / math.abs(clampDirection.x)
                : float.MaxValue;
            float ty = math.abs(clampDirection.y) > ProjectionDepthEpsilon
                ? projectionFrame.HalfHeight / math.abs(clampDirection.y)
                : float.MaxValue;

            anchoredPosition = clampDirection * math.min(tx, ty);
            clampedToEdge = true;
            visibility01 = behindPlayer ? 0f : 1f;
            return true;
        }

        private WaypointProjectionFrame ResolveWaypointProjectionFrame()
        {
            if (_viewCamera == null || _root == null || _targetCanvasRect == null)
                return default;

            Transform cameraTransform = _viewCamera.transform;
            Vector3 cameraPosition = cameraTransform.position;
            Vector3 cameraRight = cameraTransform.right;
            Vector3 cameraUp = cameraTransform.up;
            Vector3 cameraForward = cameraTransform.forward;
            if (!TryResolveCameraAup(out AbsoluteUniversePosition cameraAup))
                return default;

            float3 cameraForward3 = math.float3(cameraForward.x, cameraForward.y, cameraForward.z);
            float planeDistance = ResolveHudPlaneDistance(cameraForward3, cameraPosition, _targetCanvasRect);
            if (planeDistance <= ProjectionDepthEpsilon)
                return default;

            Vector3 lossyScale = _root.lossyScale;
            Rect rootRect = _root.rect;
            return new WaypointProjectionFrame
            {
                CameraAup = cameraAup,
                CameraRight = math.float3(cameraRight.x, cameraRight.y, cameraRight.z),
                CameraUp = math.float3(cameraUp.x, cameraUp.y, cameraUp.z),
                CameraForward = cameraForward3,
                PlaneDistance = planeDistance,
                ScaleX = math.max(ProjectionDepthEpsilon, math.abs(lossyScale.x)),
                ScaleY = math.max(ProjectionDepthEpsilon, math.abs(lossyScale.y)),
                HalfWidth = math.max(1f, (rootRect.width * 0.5f) - ScreenMargin),
                HalfHeight = math.max(1f, (rootRect.height * 0.5f) - ScreenMargin),
                IsValid = 1u
            };
        }

        private static Vector2 ResolveApproxDirection(Vector2 value)
        {
            float lengthSq = value.x * value.x + value.y * value.y;
            if (lengthSq <= ProjectionDepthEpsilon)
                return Vector2.up;

            float absX = math.abs(value.x);
            float absY = math.abs(value.y);
            float approxLength = math.max(absX, absY) + math.min(absX, absY) * 0.375f;
            float invLength = math.rcp(math.max(ProjectionDepthEpsilon, approxLength));
            value.x *= invLength;
            value.y *= invLength;
            return value;
        }

        private static int ResolveApproxEdgeRotationIndex(Vector2 direction)
        {
            float absX = math.abs(direction.x);
            float absY = math.abs(direction.y);
            if (absX <= ProjectionDepthEpsilon && absY <= ProjectionDepthEpsilon)
                return EdgeRotationUp;

            if (absX > absY * 2.41421356f)
                return direction.x >= 0f ? EdgeRotationRight : EdgeRotationLeft;

            if (absY > absX * 2.41421356f)
                return direction.y >= 0f ? EdgeRotationUp : EdgeRotationDown;

            if (direction.x >= 0f)
                return direction.y >= 0f ? EdgeRotationUpRight : EdgeRotationDownRight;

            return direction.y >= 0f ? EdgeRotationUpLeft : EdgeRotationDownLeft;
        }

        private void SetExternalWaypointInternal(
            int id,
            Transform target,
            Vector3 worldPosition,
            bool useTransform,
            uint labelHash,
            ReadOnlySpan<char> label,
            Color color)
        {
            int freeIndex = -1;
            for (int i = 0; i < _externalWaypoints.Length; i++)
            {
                if (_externalWaypoints[i].Active && _externalWaypoints[i].Id == id)
                {
                    freeIndex = i;
                    break;
                }

                if (freeIndex < 0 && !_externalWaypoints[i].Active)
                    freeIndex = i;
            }

            if (freeIndex < 0)
                return;

            ExternalWaypoint externalWaypoint = _externalWaypoints[freeIndex];
            AbsoluteUniversePosition cachedAup = externalWaypoint.PositionAup;
            bool canReuseTransformAup =
                useTransform &&
                externalWaypoint.Active &&
                externalWaypoint.UseTransform &&
                ReferenceEquals(externalWaypoint.Target, target);
            bool canReusePositionAup =
                !useTransform &&
                externalWaypoint.Active &&
                !externalWaypoint.UseTransform &&
                externalWaypoint.PresentationPosition == worldPosition;
            bool hasCachedAup =
                (canReuseTransformAup || canReusePositionAup) &&
                externalWaypoint.HasPositionAup &&
                cachedAup.IsFinite();
            externalWaypoint.Id = id;
            externalWaypoint.Target = target;
            externalWaypoint.PositionAup = hasCachedAup ? cachedAup : default;
            externalWaypoint.PresentationPosition = worldPosition;
            bool hasLabel = label.Length > 0;
            int labelOffset = freeIndex * MaximumLabelCharacters;
            int labelLength = hasLabel ? CopyExternalLabelToBank(freeIndex, label) : 0;
            uint labelRevision = externalWaypoint.LabelRevision + 1u;
            if (labelRevision == 0u)
                labelRevision = 1u;
            externalWaypoint.LabelHash = hasLabel ? labelHash : DefaultExternalLabelHash;
            externalWaypoint.LabelOffset = labelOffset;
            externalWaypoint.LabelLength = labelLength;
            externalWaypoint.LabelRevision = labelRevision;
            externalWaypoint.Color = color;
            externalWaypoint.Active = true;
            externalWaypoint.HasLabel = hasLabel;
            externalWaypoint.UseTransform = useTransform;
            externalWaypoint.HasPositionAup = hasCachedAup;
            if (TryCaptureExternalWaypointAup(ref externalWaypoint))
                externalWaypoint.HasPositionAup = true;
            _externalWaypoints[freeIndex] = externalWaypoint;
        }

        private void CaptureExternalWaypointAupsCold()
        {
            for (int i = 0; i < _externalWaypoints.Length; i++)
            {
                ExternalWaypoint externalWaypoint = _externalWaypoints[i];
                if (!externalWaypoint.Active)
                    continue;

                if (externalWaypoint.UseTransform && externalWaypoint.Target == null)
                {
                    externalWaypoint.Active = false;
                    externalWaypoint.HasPositionAup = false;
                    _externalWaypoints[i] = externalWaypoint;
                    continue;
                }

                externalWaypoint.HasPositionAup = TryCaptureExternalWaypointAup(ref externalWaypoint);
                _externalWaypoints[i] = externalWaypoint;
            }
        }

        private bool TryCaptureExternalWaypointAup(ref ExternalWaypoint externalWaypoint)
        {
            if (externalWaypoint.UseTransform)
            {
                Transform target = externalWaypoint.Target;
                if (target == null)
                    return false;

                if (!TryResolvePresentationWaypointAup(target, out AbsoluteUniversePosition capturedAup))
                    return false;

                externalWaypoint.PositionAup = capturedAup;
                externalWaypoint.HasPositionAup = true;
                return true;
            }

            if (!TryResolvePresentationWaypointAup(externalWaypoint.PresentationPosition, out AbsoluteUniversePosition positionAup))
                return false;

            externalWaypoint.PositionAup = positionAup;
            externalWaypoint.HasPositionAup = true;
            return true;
        }

        private void RebaseExternalRuntimeWaypointPresentation(Vector3 runtimeOffset)
        {
            for (int i = 0; i < _externalWaypoints.Length; i++)
            {
                ExternalWaypoint externalWaypoint = _externalWaypoints[i];
                if (!externalWaypoint.Active || externalWaypoint.UseTransform)
                    continue;

                if (externalWaypoint.HasPositionAup &&
                    externalWaypoint.PositionAup.TryToRuntimeFloat3(out float3 resolvedRuntimePosition) &&
                    math.all(math.isfinite(resolvedRuntimePosition)))
                {
                    externalWaypoint.PresentationPosition = new Vector3(
                        resolvedRuntimePosition.x,
                        resolvedRuntimePosition.y,
                        resolvedRuntimePosition.z);
                    _externalWaypoints[i] = externalWaypoint;
                    continue;
                }

                Vector3 rebasedPosition = externalWaypoint.PresentationPosition + runtimeOffset;
                if (!IsFiniteRuntimeVector(rebasedPosition))
                {
                    externalWaypoint.HasPositionAup = false;
                    _externalWaypoints[i] = externalWaypoint;
                    continue;
                }

                externalWaypoint.PresentationPosition = rebasedPosition;
                _externalWaypoints[i] = externalWaypoint;
            }
        }

        private static bool IsFiniteRuntimeVector(Vector3 value)
        {
            return math.all(math.isfinite(new float3(value.x, value.y, value.z)));
        }

        private void ClearExternalWaypointInternal(int id)
        {
            for (int i = 0; i < _externalWaypoints.Length; i++)
            {
                if (_externalWaypoints[i].Active && _externalWaypoints[i].Id == id)
                {
                    _externalWaypoints[i].Active = false;
                    _externalWaypoints[i].HasPositionAup = false;
                    _externalWaypoints[i].LabelHash = 0u;
                    _externalWaypoints[i].LabelOffset = i * MaximumLabelCharacters;
                    _externalWaypoints[i].LabelLength = 0;
                    _externalWaypoints[i].HasLabel = false;
                    break;
                }
            }
        }

        private void HideAllSlots()
        {
            for (int i = 0; i < _slots.Length; i++)
                HideSlot(i);

            _renderedSlotCount = 0;
        }

        private void HideRenderedSlots()
        {
            int count = math.min(_renderedSlotCount, _slots.Length);
            for (int i = 0; i < count; i++)
                HideSlot(i);

            _renderedSlotCount = 0;
        }

        private void HideSlot(int index)
        {
            if (index < 0 || index >= _slots.Length)
                return;

            WaypointSlot slot = _slots[index];
            if (slot.Group == null)
                return;

            if (slot.Group.alpha > HiddenAlpha)
                slot.Group.alpha = HiddenAlpha;
            slot.HasAlphaState = true;
            slot.CachedAlphaByte = 0;
            slot.HasTransformState = false;
            slot.HasImageState = false;
            _slots[index] = slot;
        }

        private void RegisterToTickManager()
        {
            if (_registeredTick || !Application.isPlaying)
                return;

            _registeredTick = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
        }

        private void RegisterToSlowTickManager()
        {
            if (_registeredSlowTick || !Application.isPlaying)
                return;

            _registeredSlowTick = SystemDispatcher.Register((ISlowTickable)this, PriorityLayer.UI);
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredTick)
                return;

            SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
            _registeredTick = false;
        }

        private void UnregisterFromSlowTickManager()
        {
            if (!_registeredSlowTick)
                return;

            SystemDispatcher.Unregister((ISlowTickable)this, PriorityLayer.UI);
            _registeredSlowTick = false;
        }

        private void TryRegisterWaypointService()
        {
            if (_registeredWaypointService || !Application.isPlaying)
                return;

            IARWaypointService current = GlobalRegistry.ARWaypoints;
            if (current != null && !ReferenceEquals(current, this))
                return;

            GlobalRegistry.RegisterARWaypointService(this);
            _registeredWaypointService = ReferenceEquals(GlobalRegistry.ARWaypoints, this);
            if (_registeredWaypointService)
                s_cachedWaypointService = this;
        }

        private void UnregisterWaypointService()
        {
            if (!_registeredWaypointService)
                return;

            GlobalRegistry.UnregisterARWaypointService(this);
            if (ReferenceEquals(s_cachedWaypointService, this))
                s_cachedWaypointService = null;
            _registeredWaypointService = false;
        }

        private static SuitHUDV4CanvasOverlay ResolveProjectionOverlay()
        {
            for (int i = 0; i < SuitHUDV4CanvasOverlay.ActiveOverlayCount; i++)
            {
                SuitHUDV4CanvasOverlay overlay = SuitHUDV4CanvasOverlay.GetActiveOverlay(i);
                if (overlay == null || overlay.TargetCanvas == null)
                    continue;

                Canvas targetCanvas = overlay.TargetCanvas;
                if (targetCanvas.renderMode == RenderMode.WorldSpace && overlay.ProjectionCamera != null)
                    return overlay;
            }

            return null;
        }

        private static Canvas ResolveTargetCanvas()
        {
            SuitHUDV4CanvasOverlay overlay = ResolveProjectionOverlay();
            if (overlay != null)
                return overlay.TargetCanvas;

            SuitHUDV4CanvasOverlay activeOverlay = null;
            return SuitHUDV4CanvasOverlay.TryResolveActiveRuntime(ref activeOverlay)
                ? activeOverlay.TargetCanvas
                : null;
        }

        private static bool TryBindSlot(int index, RectTransform parent, Camera camera, out WaypointSlot slot)
        {
            slot = default;
            if (parent == null || (uint)index >= MaxWaypoints)
                return false;

            RectTransform root = FindExistingChild(parent, s_waypointSlotNames[index]);
            if (root == null || !root.TryGetComponent(out CanvasGroup group))
                return false;

            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(120f, 42f);

            group.alpha = HiddenAlpha;
            group.blocksRaycasts = false;
            group.interactable = false;

            if (!TryBindImage(root, SlotFillName, MarkerSize, MarkerSize, RelayColor, out Image fill, out RectTransform fillRect))
                return false;

            if (!TryBindImage(root, SlotOutlineName, OutlineSize, OutlineSize, OccludedColor, out Image outline, out RectTransform outlineRect))
                return false;

            outline.enabled = true;

            if (!TryBindLabel(root, camera, out TextMeshProUGUI label))
                return false;

            slot = new WaypointSlot
            {
                Root = root,
                Group = group,
                FillRect = fillRect,
                OutlineRect = outlineRect,
                Fill = fill,
                Outline = outline,
                Label = label,
                CachedLabelHash = 0u,
                CachedLabelLength = 0,
                CachedLabelSlotIndex = -1,
                CachedLabelRevision = 0u,
                CachedEdgeState = false,
                CachedFillEnabled = true,
                CachedOutlineEnabled = true,
                CachedFillColor = RelayColor,
                CachedOutlineColor = OccludedColor
            };
            return true;
        }

        private static bool TryBindImage(
            RectTransform parent,
            string name,
            float width,
            float height,
            Color color,
            out Image image,
            out RectTransform rect)
        {
            image = null;
            rect = null;
            if (parent == null)
                return false;

            rect = FindExistingChild(parent, name);
            if (rect == null || !rect.TryGetComponent(out image))
                return false;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);

            image.color = color;
            image.raycastTarget = false;
            return true;
        }

        private static bool TryBindLabel(RectTransform parent, Camera camera, out TextMeshProUGUI label)
        {
            label = null;
            if (parent == null)
                return false;

            RectTransform rect = FindExistingChild(parent, SlotLabelName);
            if (rect == null || !rect.TryGetComponent(out label))
                return false;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 14f);
            rect.sizeDelta = new Vector2(176f, 20f);

            if (label.font == null)
                label.font = LocalizedFontResolver.ResolveReadableFont(null);
            label.fontSize = 11f;
            label.alignment = TextAlignmentOptions.Bottom;
            label.color = new Color(0.90f, 0.96f, 0.94f, 0.92f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            TMP_TextRegistry.EnsureRegistered(label);

            if (rect.TryGetComponent(out WorldSpaceTMPSharpnessController sharpnessController))
                sharpnessController.Bind(label, camera);

            return true;
        }

        private void ApplyLabelText(TextMeshProUGUI label, ReadOnlySpan<char> value)
        {
            if (label == null)
                return;

            int length = CopyLabelToBuffer(value, _labelCharBuffer);
            label.SetCharArray(_labelCharBuffer, 0, length);
        }

        private int CopyExternalLabelToBank(int waypointIndex, ReadOnlySpan<char> value)
        {
            if ((uint)waypointIndex >= MaxExternalWaypoints)
                return 0;

            int offset = waypointIndex * MaximumLabelCharacters;
            return CopyLabelToBuffer(value, _externalWaypointLabelBuffer, offset, MaximumLabelCharacters);
        }

        private static int CopyLabelToBuffer(ReadOnlySpan<char> value, char[] destination)
        {
            return destination == null ? 0 : CopyLabelToBuffer(value, destination, 0, destination.Length);
        }

        private static int CopyLabelToBuffer(ReadOnlySpan<char> value, char[] destination, int destinationOffset, int capacity)
        {
            if (destination == null || destination.Length == 0)
                return 0;

            if (destinationOffset < 0 || destinationOffset >= destination.Length || capacity <= 0)
                return 0;

            int max = math.min(capacity, destination.Length - destinationOffset);
            if (max <= 0)
                return 0;

            if (value.Length <= 0)
            {
                destination[destinationOffset] = '\0';
                return 0;
            }

            int length = math.min(value.Length, max);
            if (value.Length > max && max >= 4)
                length = max - 3;
            for (int i = 0; i < length; i++)
                destination[destinationOffset + i] = value[i];
            if (value.Length > max && max >= 4)
            {
                destination[destinationOffset + length++] = '.';
                destination[destinationOffset + length++] = '.';
                destination[destinationOffset + length++] = '.';
            }

            return length;
        }

        private static uint ResolveLabelHash(ReadOnlySpan<char> label)
        {
            return label.Length <= 0
                ? DefaultExternalLabelHash
                : ComputeWaypointLabelHash(label);
        }

        private static uint ComputeWaypointLabelHash(ReadOnlySpan<char> label)
        {
            uint hash = WaypointLabelHashSeed;
            for (int i = 0; i < label.Length; i++)
            {
                hash ^= label[i];
                hash *= WaypointLabelHashPrime;
            }

            return hash;
        }

        private static int ResolveRenderedLabelLength(int sourceLength)
        {
            if (sourceLength <= 0)
                return DefaultExternalLabel.Length;

            return sourceLength > MaximumLabelCharacters && MaximumLabelCharacters >= 4
                ? MaximumLabelCharacters
                : math.min(sourceLength, MaximumLabelCharacters);
        }

        private static void ApplySlotIconState(ref WaypointSlot slot, bool edgeState)
        {
            if (slot.FillRect != null)
            {
                slot.FillRect.localScale = edgeState
                    ? new Vector3(EdgeMarkerWidth / MarkerSize, EdgeMarkerHeight / MarkerSize, 1f)
                    : Vector3.one;
            }

            if (slot.OutlineRect != null)
            {
                slot.OutlineRect.localScale = edgeState
                    ? new Vector3(EdgeOutlineWidth / OutlineSize, EdgeOutlineHeight / OutlineSize, 1f)
                    : Vector3.one;
            }
        }

        private static void ApplySlotTransform(ref WaypointSlot slot, Vector2 anchoredPosition, bool clampedToEdge, Vector2 clampDirection)
        {
            if (slot.Root == null)
                return;

            int pixelX = (int)math.round(anchoredPosition.x);
            int pixelY = (int)math.round(anchoredPosition.y);
            int rotationIndex = clampedToEdge ? ResolveApproxEdgeRotationIndex(clampDirection) : EdgeRotationUp;
            if (slot.HasTransformState &&
                slot.CachedAnchoredX == pixelX &&
                slot.CachedAnchoredY == pixelY &&
                slot.CachedRotationIndex == rotationIndex)
            {
                return;
            }

            slot.HasTransformState = true;
            slot.CachedAnchoredX = pixelX;
            slot.CachedAnchoredY = pixelY;
            slot.CachedRotationIndex = rotationIndex;
            slot.Root.anchoredPosition = new Vector2(pixelX, pixelY);
            slot.Root.localRotation = s_edgeRotationLut[rotationIndex];
        }

        private static void ApplySlotAlpha(ref WaypointSlot slot, float alpha)
        {
            if (slot.Group == null)
                return;

            byte alphaByte = QuantizeAlphaByte(alpha);
            if (slot.HasAlphaState && slot.CachedAlphaByte == alphaByte)
                return;

            slot.HasAlphaState = true;
            slot.CachedAlphaByte = alphaByte;
            slot.Group.alpha = alphaByte * (1f / 255f);
        }

        private static void ApplySlotImageState(
            ref WaypointSlot slot,
            bool fillEnabled,
            bool outlineEnabled,
            Color fillColor,
            Color outlineColor)
        {
            if (slot.Fill == null || slot.Outline == null)
                return;

            if (slot.HasImageState &&
                slot.CachedFillEnabled == fillEnabled &&
                slot.CachedOutlineEnabled == outlineEnabled &&
                ColorsMatch(slot.CachedFillColor, fillColor) &&
                ColorsMatch(slot.CachedOutlineColor, outlineColor))
            {
                return;
            }

            slot.HasImageState = true;
            slot.CachedFillEnabled = fillEnabled;
            slot.CachedOutlineEnabled = outlineEnabled;
            slot.CachedFillColor = fillColor;
            slot.CachedOutlineColor = outlineColor;
            if (slot.Fill.enabled != fillEnabled)
                slot.Fill.enabled = fillEnabled;
            if (slot.Outline.enabled != outlineEnabled)
                slot.Outline.enabled = outlineEnabled;
            if (!ColorsMatch(slot.Fill.color, fillColor))
                slot.Fill.color = fillColor;
            if (!ColorsMatch(slot.Outline.color, outlineColor))
                slot.Outline.color = outlineColor;
        }

        private static byte QuantizeAlphaByte(float alpha)
        {
            int alphaInt = (int)math.round(math.saturate(alpha) * 255f);
            return (byte)math.clamp(alphaInt, 0, 255);
        }

        private static bool ColorsMatch(Color lhs, Color rhs)
        {
            return lhs.r == rhs.r && lhs.g == rhs.g && lhs.b == rhs.b && lhs.a == rhs.a;
        }

        private static float ResolveHudPlaneDistance(float3 cameraForward, Vector3 cameraPosition, RectTransform canvasRect)
        {
            if (canvasRect == null)
                return 0f;

            return math.max(
                ProjectionDepthEpsilon,
                math.dot(cameraForward, (float3)(canvasRect.position - cameraPosition)));
        }

        private static RectTransform FindExistingChild(Transform parent, string childName)
        {
            if (parent == null)
                return null;

            int childCount = parent.childCount;
            for (int i = 0; i < childCount; i++)
            {
                RectTransform child = parent.GetChild(i) as RectTransform;
                if (child == null)
                    continue;

                if (string.Equals(child.name, childName, StringComparison.Ordinal))
                    return child;
            }

            return null;
        }

        private void ResetBoundSlots()
        {
            HideAllSlots();
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i] = default;
            }

            _renderedSlotCount = 0;
            _uiBuilt = false;
        }

    }
}

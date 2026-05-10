using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Hecton8.Bootstrap;
using Hecton8.Core;
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
    public sealed class ARWaypointOverlay : MonoBehaviour, ITickable, ISlowTickable, IOriginShiftListener, IARWaypointService
    {
        private const int MaxAnchorWaypoints = 7;
        private const int MaxExternalWaypoints = 8;
        private const int MaxWaypoints = 1 + MaxAnchorWaypoints + MaxExternalWaypoints;
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
        private const string DefaultRelayLabel = "SERVICE RELAY";
        private const string DefaultAnchorLabel = "ABYSSAL ANCHOR";
        private const string DefaultExternalLabel = "WAYPOINT";

        private static readonly uint _WaypointSolveBudgetWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("HUD_AR_WAYPOINT_SOLVE_OVER_BUDGET"));
        private static readonly uint _WaypointSolveBudgetContextHash = unchecked((uint)Hecton.Localization.LocHash.Compute("ARWaypointOverlay.Solve"));
        private static readonly Color RelayColor = new Color(0.64f, 0.94f, 0.98f, 0.96f);
        private static readonly Color AnchorColor = new Color(0.98f, 0.74f, 0.22f, 0.96f);
        private static readonly Color OccludedColor = new Color(0.94f, 0.94f, 0.94f, 0.62f);
        private static readonly List<SuitHUDV4CanvasOverlay> s_overlayResolveBuffer =
            new List<SuitHUDV4CanvasOverlay>(4);
        private static readonly List<RectTransform> s_directChildBuffer =
            new List<RectTransform>(32);

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

        [StructLayout(LayoutKind.Sequential)]
        private struct ExternalWaypoint
        {
            public int Id;
            public Transform Target;
            public AbsoluteUniversePosition PositionAup;
            public string Label;
            public Color Color;
            public bool Active;
            public bool UseTransform;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RuntimeWaypoint
        {
            public AbsoluteUniversePosition PositionAup;
            public string Label;
            public Color Color;
            public bool Active;
            public bool Occluded;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WaypointSlot
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public RectTransform FillRect;
            public RectTransform OutlineRect;
            public Image Fill;
            public Image Outline;
            public TextMeshProUGUI Label;
            public string CachedLabel;
            public int CachedAnchoredX;
            public int CachedAnchoredY;
            public int CachedRotationDegrees;
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

        [StructLayout(LayoutKind.Sequential)]
        private struct WaypointProjectionFrame
        {
            public AbsoluteUniversePosition CameraAup;
            public float3 CameraRight;
            public float3 CameraUp;
            public float3 CameraForward;
            public float PlaneDistance;
            public float ScaleX;
            public float ScaleY;
            public float HalfWidth;
            public float HalfHeight;
            public bool IsValid;
        }

        // COLD ALLOC: ExternalWaypoint[8] - external AR waypoint registry - owner: ARWaypointOverlay
        private readonly ExternalWaypoint[] _externalWaypoints = new ExternalWaypoint[MaxExternalWaypoints];
        // COLD ALLOC: RuntimeWaypoint[16] - projected waypoint payloads - owner: ARWaypointOverlay
        private readonly RuntimeWaypoint[] _runtimeWaypoints = new RuntimeWaypoint[MaxWaypoints];
        // COLD ALLOC: WaypointSlot[16] - pooled waypoint UI markers - owner: ARWaypointOverlay
        private readonly WaypointSlot[] _slots = new WaypointSlot[MaxWaypoints];
        // COLD ALLOC: char[48] - transient zero-GC waypoint label formatter buffer - owner: ARWaypointOverlay
        private readonly char[] _labelCharBuffer = new char[MaximumLabelCharacters];

        private bool _registeredWaypointService;
        private bool _registeredTick;
        private bool _registeredSlowTick;
        private bool _uiBuilt;
        private int _waypointCount;
        private int _renderedSlotCount;
        private int _nextWaypointPerformanceWarningFrame;
        private Canvas _targetCanvas;
        private RectTransform _targetCanvasRect;
        private RectTransform _root;
        private Camera _viewCamera;
        private Transform _playerTransform;
        private HectonMapMagicVegetationBridge _vegetationBridge;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_overlayResolveBuffer.Clear();
            s_directChildBuffer.Clear();
        }

        /// <summary>
        /// Register or refresh an external waypoint bound to a transform target.
        /// </summary>
        public static void SetWaypoint(int id, Transform target, string label, Color color)
        {
            IARWaypointService service = GlobalRegistry.ARWaypoints;
            if (service == null)
                return;

            service.SetWaypoint(id, target, label, color);
        }

        /// <summary>
        /// Register or refresh an external waypoint bound to a runtime-space position.
        /// </summary>
        public static void SetWaypoint(int id, Vector3 worldPosition, string label, Color color)
        {
            IARWaypointService service = GlobalRegistry.ARWaypoints;
            if (service == null)
                return;

            service.SetWaypoint(id, worldPosition, label, color);
        }

        /// <summary>
        /// Remove a previously registered external waypoint.
        /// </summary>
        public static void ClearWaypoint(int id)
        {
            IARWaypointService service = GlobalRegistry.ARWaypoints;
            if (service == null)
                return;

            service.ClearWaypoint(id);
        }

        private void OnEnable()
        {
            TryRegisterWaypointService();
            ResolveOwners(allowHierarchySearch: true);
            EnsureUiBuilt(allowCreate: true);
            HectonFloatingOrigin.RegisterListener(this);
            RegisterToTickManager();
            RegisterToSlowTickManager();
        }

        private void Start()
        {
            ResolveOwners(allowHierarchySearch: true);
            EnsureUiBuilt(allowCreate: true);
            RegisterToTickManager();
            RegisterToSlowTickManager();
        }

        private void OnDisable()
        {
            UnregisterWaypointService();
            HectonFloatingOrigin.UnregisterListener(this);
            UnregisterFromTickManager();
            UnregisterFromSlowTickManager();
            HideAllSlots();
        }

        private void OnDestroy()
        {
            UnregisterWaypointService();
            HectonFloatingOrigin.UnregisterListener(this);
            UnregisterFromTickManager();
            UnregisterFromSlowTickManager();
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            bool sampleSolveCost = ShouldSampleWaypointSolveCost();
            long solveStartTimestamp = sampleSolveCost ? Stopwatch.GetTimestamp() : 0L;
            ResolveOwners(allowHierarchySearch: false);
            if (!EnsureUiBuilt(allowCreate: false))
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
            ResolveOwners(allowHierarchySearch: false);
            CollectRuntimeWaypoints();
            RefreshOcclusionStates();
            PublishWaypointSolveWarningIfNeeded(true, solveStartTimestamp);
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            _targetCanvas = null;
            _targetCanvasRect = null;
            _viewCamera = null;
            _uiBuilt = false;
            _root = null;
            ResolveOwners(allowHierarchySearch: true);
            EnsureUiBuilt(allowCreate: true);
        }

        bool IARWaypointService.IsInitialized => _uiBuilt && _root != null && _targetCanvas != null;

        void IARWaypointService.SetWaypoint(int id, Transform target, string label, Color color)
        {
            SetExternalWaypointInternal(id, target, default, useTransform: true, label, color);
        }

        void IARWaypointService.SetWaypoint(int id, Vector3 worldPosition, string label, Color color)
        {
            SetExternalWaypointInternal(id, null, worldPosition, useTransform: false, label, color);
        }

        void IARWaypointService.ClearWaypoint(int id)
        {
            ClearExternalWaypointInternal(id);
        }

        private void ResolveOwners(bool allowHierarchySearch)
        {
            if (_vegetationBridge == null)
                _vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;

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
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
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

            if (_playerTransform == null)
                GameBootstrapper.TryGetCurrentPlayerTransform(out _playerTransform);
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

            _root = FindExistingChild(canvasRoot, RootName);
            if (_root == null)
            {
                // COLD ALLOC: GameObject[1] - AR waypoint root canvas-space owner - owner: ARWaypointOverlay
                GameObject rootObject = new GameObject(RootName, typeof(RectTransform));
                rootObject.layer = canvasRoot.gameObject.layer;
                rootObject.TryGetComponent(out _root);
                _root.SetParent(canvasRoot, false);
            }

            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.SetAsLastSibling();

            ClearChildren(_root);
            for (int i = 0; i < _slots.Length; i++)
                _slots[i] = CreateSlot(i, _root, _viewCamera);

            _uiBuilt = true;
            return true;
        }

        private void CollectRuntimeWaypoints()
        {
            int count = 0;

            EmergencyServiceRelayDirector relayDirector = Hecton8.Core.GlobalRegistry.EmergencyRelay;
            EmergencyServiceRelay relayTarget = relayDirector != null ? relayDirector.GetActiveRouteTarget() : null;
            if (relayTarget != null && relayTarget.isActiveAndEnabled && count < _runtimeWaypoints.Length)
            {
                RuntimeWaypoint waypoint = _runtimeWaypoints[count];
                waypoint.PositionAup = relayTarget.RelayAup;
                waypoint.Label = DefaultRelayLabel;
                waypoint.Color = RelayColor;
                waypoint.Active = true;
                waypoint.Occluded = count < _waypointCount && _runtimeWaypoints[count].Occluded;
                _runtimeWaypoints[count] = waypoint;
                count++;
            }

            if (_vegetationBridge != null &&
                _vegetationBridge.TryGetActiveAbyssalAnchorPayload(out NativeArray<Vector3> anchors, out int anchorCount) &&
                anchors.IsCreated &&
                anchorCount > 0)
            {
                int visibleAnchors = math.min(MaxAnchorWaypoints, anchorCount);
                for (int i = 0; i < visibleAnchors && count < _runtimeWaypoints.Length; i++)
                {
                    RuntimeWaypoint waypoint = _runtimeWaypoints[count];
                    waypoint.PositionAup = AbsoluteUniversePosition.FromRuntimePosition(anchors[i]);
                    waypoint.Label = DefaultAnchorLabel;
                    waypoint.Color = AnchorColor;
                    waypoint.Active = true;
                    waypoint.Occluded = count < _waypointCount && _runtimeWaypoints[count].Occluded;
                    _runtimeWaypoints[count] = waypoint;
                    count++;
                }
            }

            for (int i = 0; i < _externalWaypoints.Length && count < _runtimeWaypoints.Length; i++)
            {
                ExternalWaypoint externalWaypoint = _externalWaypoints[i];
                if (!externalWaypoint.Active)
                    continue;

                if (externalWaypoint.UseTransform)
                {
                    if (externalWaypoint.Target == null)
                    {
                        externalWaypoint.Active = false;
                        _externalWaypoints[i] = externalWaypoint;
                        continue;
                    }

                    externalWaypoint.PositionAup = AbsoluteUniversePosition.FromRuntimePosition(externalWaypoint.Target.position);
                    _externalWaypoints[i] = externalWaypoint;
                }

                RuntimeWaypoint runtimeWaypoint = _runtimeWaypoints[count];
                runtimeWaypoint.PositionAup = externalWaypoint.PositionAup;
                runtimeWaypoint.Label = string.IsNullOrEmpty(externalWaypoint.Label) ? DefaultExternalLabel : externalWaypoint.Label;
                runtimeWaypoint.Color = externalWaypoint.Color.a <= 0f ? RelayColor : externalWaypoint.Color;
                runtimeWaypoint.Active = true;
                runtimeWaypoint.Occluded = count < _waypointCount && _runtimeWaypoints[count].Occluded;
                _runtimeWaypoints[count] = runtimeWaypoint;
                count++;
            }

            for (int i = count; i < _runtimeWaypoints.Length; i++)
                _runtimeWaypoints[i].Active = false;

            _waypointCount = count;
        }

        private void RenderWaypoints()
        {
            if (_root == null || _viewCamera == null || _waypointCount <= 0)
            {
                HideRenderedSlots();
                return;
            }

            WaypointProjectionFrame projectionFrame = ResolveWaypointProjectionFrame();
            if (!projectionFrame.IsValid)
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

                if (!string.Equals(slot.CachedLabel, waypoint.Label, StringComparison.Ordinal))
                {
                    ApplyLabelText(slot.Label, waypoint.Label);
                    slot.CachedLabel = waypoint.Label;
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
            AbsoluteUniversePosition cameraAup = AbsoluteUniversePosition.FromRuntimePosition(cameraTransform.position);
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

                float3 delta = AbsoluteUniversePosition.ToCameraRelativeFloat3(in waypoint.PositionAup, in cameraAup);
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
                   (Time.frameCount & (WaypointSolveTelemetryCadenceFrames - 1)) == 0;
        }

        private void PublishWaypointSolveWarningIfNeeded(bool hasSample, long startTimestamp)
        {
            if (!hasSample)
                return;

            long elapsedTicks = Stopwatch.GetTimestamp() - startTimestamp;
            double elapsedMilliseconds = elapsedTicks * 1000.0d / Stopwatch.Frequency;
            if (elapsedMilliseconds <= WaypointSolveBudgetWarningMilliseconds ||
                Time.frameCount < _nextWaypointPerformanceWarningFrame)
                return;

            GlobalTelemetryBus.PublishPerformanceWarning(
                _WaypointSolveBudgetWarningHash,
                _WaypointSolveBudgetContextHash,
                (float)elapsedMilliseconds);
            _nextWaypointPerformanceWarningFrame = Time.frameCount + WaypointPerformanceWarningCooldownFrames;
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

            if (!projectionFrame.IsValid)
                return false;

            float3 deltaAup = AbsoluteUniversePosition.ToCameraRelativeFloat3(in waypointAup, in projectionFrame.CameraAup);
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
            float3 cameraForward3 = math.float3(cameraForward.x, cameraForward.y, cameraForward.z);
            float planeDistance = ResolveHudPlaneDistance(cameraForward3, cameraPosition, _targetCanvasRect);
            if (planeDistance <= ProjectionDepthEpsilon)
                return default;

            Vector3 lossyScale = _root.lossyScale;
            Rect rootRect = _root.rect;
            return new WaypointProjectionFrame
            {
                CameraAup = AbsoluteUniversePosition.FromRuntimePosition(cameraPosition),
                CameraRight = math.float3(cameraRight.x, cameraRight.y, cameraRight.z),
                CameraUp = math.float3(cameraUp.x, cameraUp.y, cameraUp.z),
                CameraForward = cameraForward3,
                PlaneDistance = planeDistance,
                ScaleX = math.max(ProjectionDepthEpsilon, math.abs(lossyScale.x)),
                ScaleY = math.max(ProjectionDepthEpsilon, math.abs(lossyScale.y)),
                HalfWidth = math.max(1f, (rootRect.width * 0.5f) - ScreenMargin),
                HalfHeight = math.max(1f, (rootRect.height * 0.5f) - ScreenMargin),
                IsValid = true
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

        private static float ResolveApproxEdgeRotationDegrees(Vector2 direction)
        {
            float absX = math.abs(direction.x);
            float absY = math.abs(direction.y);
            if (absX <= ProjectionDepthEpsilon && absY <= ProjectionDepthEpsilon)
                return 0f;

            if (absX > absY * 2.41421356f)
                return direction.x >= 0f ? -90f : 90f;

            if (absY > absX * 2.41421356f)
                return direction.y >= 0f ? 0f : 180f;

            if (direction.x >= 0f)
                return direction.y >= 0f ? -45f : -135f;

            return direction.y >= 0f ? 45f : 135f;
        }

        private void SetExternalWaypointInternal(
            int id,
            Transform target,
            Vector3 worldPosition,
            bool useTransform,
            string label,
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
            externalWaypoint.Id = id;
            externalWaypoint.Target = target;
            externalWaypoint.PositionAup = useTransform && target != null
                ? AbsoluteUniversePosition.FromRuntimePosition(target.position)
                : AbsoluteUniversePosition.FromRuntimePosition(worldPosition);
            externalWaypoint.Label = label;
            externalWaypoint.Color = color;
            externalWaypoint.Active = true;
            externalWaypoint.UseTransform = useTransform;
            _externalWaypoints[freeIndex] = externalWaypoint;
        }

        private void ClearExternalWaypointInternal(int id)
        {
            for (int i = 0; i < _externalWaypoints.Length; i++)
            {
                if (_externalWaypoints[i].Active && _externalWaypoints[i].Id == id)
                {
                    _externalWaypoints[i].Active = false;
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

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registeredTick = GlobalRegistry.Updatables.Contains(this);
        }

        private void RegisterToSlowTickManager()
        {
            if (_registeredSlowTick || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.UI);
            _registeredSlowTick = GlobalRegistry.SlowTickables.Contains(this);
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registeredTick = false;
        }

        private void UnregisterFromSlowTickManager()
        {
            if (!_registeredSlowTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.UI);
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
        }

        private void UnregisterWaypointService()
        {
            if (!_registeredWaypointService)
                return;

            GlobalRegistry.UnregisterARWaypointService(this);
            _registeredWaypointService = false;
        }

        private static SuitHUDV4CanvasOverlay ResolveProjectionOverlay()
        {
            SuitHUDV4CanvasOverlay.CopyActiveOverlaysTo(s_overlayResolveBuffer);
            for (int i = 0; i < s_overlayResolveBuffer.Count; i++)
            {
                SuitHUDV4CanvasOverlay overlay = s_overlayResolveBuffer[i];
                if (overlay == null || overlay.TargetCanvas == null)
                    continue;

                Canvas targetCanvas = overlay.TargetCanvas;
                if (targetCanvas.renderMode == RenderMode.WorldSpace && overlay.ProjectionCamera != null)
                {
                    s_overlayResolveBuffer.Clear();
                    return overlay;
                }
            }

            s_overlayResolveBuffer.Clear();
            return null;
        }

        private static Canvas ResolveTargetCanvas()
        {
            SuitHUDV4CanvasOverlay overlay = ResolveProjectionOverlay();
            if (overlay != null)
                return overlay.TargetCanvas;

            return SuitHUDV4CanvasOverlay.ActiveRuntimeInstance != null
                ? SuitHUDV4CanvasOverlay.ActiveRuntimeInstance.TargetCanvas
                : null;
        }

        private static WaypointSlot CreateSlot(int index, RectTransform parent, Camera camera)
        {
            GameObject rootObject = new GameObject(s_waypointSlotNames[index], typeof(RectTransform), typeof(CanvasGroup));
            rootObject.layer = parent.gameObject.layer;
            rootObject.TryGetComponent(out RectTransform root);
            root.SetParent(parent, false);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(120f, 42f);

            rootObject.TryGetComponent(out CanvasGroup group);
            group.alpha = HiddenAlpha;
            group.blocksRaycasts = false;
            group.interactable = false;

            Image fill = CreateImage(root, "Fill", MarkerSize, MarkerSize, RelayColor, out RectTransform fillRect);
            Image outline = CreateImage(root, "Outline", OutlineSize, OutlineSize, OccludedColor, out RectTransform outlineRect);
            outline.enabled = true;

            TextMeshProUGUI label = CreateLabel(root, camera);
            return new WaypointSlot
            {
                Root = root,
                Group = group,
                FillRect = fillRect,
                OutlineRect = outlineRect,
                Fill = fill,
                Outline = outline,
                Label = label,
                CachedLabel = string.Empty,
                CachedEdgeState = false,
                CachedFillEnabled = true,
                CachedOutlineEnabled = true,
                CachedFillColor = RelayColor,
                CachedOutlineColor = OccludedColor
            };
        }

        private static Image CreateImage(
            RectTransform parent,
            string name,
            float width,
            float height,
            Color color,
            out RectTransform rect)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.layer = parent.gameObject.layer;
            go.TryGetComponent(out rect);
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);

            go.TryGetComponent(out Image image);
            image.sprite = null;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI CreateLabel(RectTransform parent, Camera camera)
        {
            GameObject go = new GameObject("Label", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.TryGetComponent(out RectTransform rect);
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 14f);
            rect.sizeDelta = new Vector2(132f, 20f);

            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>(); // COLD ALLOC: TextMeshProUGUI[1] - AR waypoint label owner - owner: ARWaypointOverlay
            label.font = LocalizedFontResolver.ResolveReadableFont(null);
            label.fontSize = 11f;
            label.alignment = TextAlignmentOptions.Bottom;
            label.color = new Color(0.90f, 0.96f, 0.94f, 0.92f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            TMP_TextRegistry.EnsureRegistered(label);

            WorldSpaceTMPSharpnessController sharpnessController = go.AddComponent<WorldSpaceTMPSharpnessController>(); // COLD ALLOC: WorldSpaceTMPSharpnessController[1] - per-label world-space SDF sharpness owner - owner: ARWaypointOverlay
            sharpnessController.Bind(label, camera);
            return label;
        }

        private void ApplyLabelText(TextMeshProUGUI label, string value)
        {
            if (label == null)
                return;

            int length = CopyLabelToBuffer(value, _labelCharBuffer);
            label.SetCharArray(_labelCharBuffer, 0, length);
        }

        private static int CopyLabelToBuffer(string value, char[] destination)
        {
            if (destination == null || destination.Length == 0)
                return 0;

            if (string.IsNullOrEmpty(value))
            {
                destination[0] = '\0';
                return 0;
            }

            int length = math.min(value.Length, destination.Length);
            for (int i = 0; i < length; i++)
                destination[i] = value[i];

            return length;
        }

        private static void ApplySlotIconState(ref WaypointSlot slot, bool edgeState)
        {
            if (slot.FillRect != null)
            {
                slot.FillRect.sizeDelta = edgeState
                    ? new Vector2(EdgeMarkerWidth, EdgeMarkerHeight)
                    : new Vector2(MarkerSize, MarkerSize);
            }

            if (slot.OutlineRect != null)
            {
                slot.OutlineRect.sizeDelta = edgeState
                    ? new Vector2(EdgeOutlineWidth, EdgeOutlineHeight)
                    : new Vector2(OutlineSize, OutlineSize);
            }
        }

        private static void ApplySlotTransform(ref WaypointSlot slot, Vector2 anchoredPosition, bool clampedToEdge, Vector2 clampDirection)
        {
            if (slot.Root == null)
                return;

            int pixelX = (int)math.round(anchoredPosition.x);
            int pixelY = (int)math.round(anchoredPosition.y);
            int rotationDegrees = clampedToEdge ? (int)ResolveApproxEdgeRotationDegrees(clampDirection) : 0;
            if (slot.HasTransformState &&
                slot.CachedAnchoredX == pixelX &&
                slot.CachedAnchoredY == pixelY &&
                slot.CachedRotationDegrees == rotationDegrees)
            {
                return;
            }

            slot.HasTransformState = true;
            slot.CachedAnchoredX = pixelX;
            slot.CachedAnchoredY = pixelY;
            slot.CachedRotationDegrees = rotationDegrees;
            slot.Root.anchoredPosition = new Vector2(pixelX, pixelY);
            slot.Root.localRotation = rotationDegrees != 0
                ? Quaternion.Euler(0f, 0f, rotationDegrees)
                : Quaternion.identity;
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

            s_directChildBuffer.Clear();
            parent.GetComponentsInChildren(includeInactive: true, result: s_directChildBuffer);
            for (int i = 0; i < s_directChildBuffer.Count; i++)
            {
                RectTransform child = s_directChildBuffer[i];
                if (child == null || !ReferenceEquals(child.parent, parent))
                    continue;

                if (string.Equals(child.name, childName, StringComparison.Ordinal))
                {
                    s_directChildBuffer.Clear();
                    return child;
                }
            }

            s_directChildBuffer.Clear();
            return null;
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null)
                return;

            s_directChildBuffer.Clear();
            parent.GetComponentsInChildren(includeInactive: true, result: s_directChildBuffer);
            for (int i = s_directChildBuffer.Count - 1; i >= 0; i--)
            {
                RectTransform child = s_directChildBuffer[i];
                if (child == null || !ReferenceEquals(child.parent, parent))
                    continue;

                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }

            s_directChildBuffer.Clear();
        }

    }
}

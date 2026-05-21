using Hecton8.Core;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Interaction
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Interaction/VR Cable Drag Plug")]
    public sealed class VRCableDragPlug : MonoBehaviour, IInteractable, ILateFrameTickable, IOriginShiftListener
    {
        private const long CableLinkSalt = 0x5643524300000000L;
        private const int MaxParentResolveDepth = 32;
        private const float MaximumCableLengthMeters = 128f;
        private const float MaximumCableRadiusMeters = 0.25f;
        private const float MaximumSlackDepthMeters = 16f;

        [Header("Sockets")]
        [SerializeField] private Transform sourceSocket;
        [SerializeField] private Transform destinationSocket;
        [SerializeField] private Transform plugVisual;
        [SerializeField] private PhysicalInteractionHandler releaseHandler;

        [Header("Cable")]
        [SerializeField, Min(0.001f)] private float cableRadiusMeters = 0.028f;
        [SerializeField, Min(0f)] private float slackDepthMeters = 0.45f;
        [SerializeField, Min(0.25f)] private float maxCableLengthMeters = 8f;
        [SerializeField] private bool hasPower;
        [SerializeField] private bool renderDisconnectedPreview = true;
        [SerializeField] private Color poweredColor = new Color(0.25f, 0.95f, 1f, 0.95f);
        [SerializeField] private Color unpoweredColor = new Color(0.35f, 0.42f, 0.48f, 0.55f);
        [SerializeField] private string grabPrompt = "Grab Cable Plug";
        [SerializeField] private string dropPrompt = "Drop Cable Plug";
        [SerializeField] private string disconnectPrompt = "Disconnect Cable Plug";

        private float3 _p0;
        private float3 _p1;
        private float3 _p2;
        private float3 _p3;
        private Vector3 _manualPlugPosition;
        private Vector3 _manualPlugForward = Vector3.forward;
        private Transform _dragAnchor;
        private long _linkId;
        private bool _dragging;
        private bool _connected;
        private bool _registeredLateFrame;
        private bool _registeredOriginShift;

        public bool IsDragging => _dragging;
        public bool IsConnected => _connected;
        public float MaxCableLengthSq
        {
            get { return ResolveSafeMaxCableLengthSq(); }
        }
        public bool HasPower
        {
            get => hasPower;
            set => hasPower = value;
        }

        public void OnHoverStart()
        {
        }

        public void OnHoverEnd()
        {
        }

        public void Interact(Transform interactor)
        {
            if (_dragging)
            {
                EndDrag();
                return;
            }

            BeginDrag(interactor);
        }

        public string GetInteractText()
        {
            if (_dragging)
                return dropPrompt;

            return _connected ? disconnectPrompt : grabPrompt;
        }

        private void Awake()
        {
            _linkId = CableLinkSalt ^ unchecked((long)EntityId.ToULong(GetEntityId()));
            _manualPlugPosition = ResolveAupRuntimePosition(plugVisual != null ? plugVisual : transform);
            Vector3 initialForward = plugVisual != null ? plugVisual.forward : transform.forward;
            _manualPlugForward = SafeNormalize(initialForward, Vector3.forward);
        }

        private void OnEnable()
        {
            TryRegisterOriginShiftListener();
            RefreshLateFrameRegistration();
        }

        private void OnDisable()
        {
            AbortRuntimeDragState(clearConnection: false);
            InteractableRegistry.InvalidateTree(this);
            TryUnregisterOriginShiftListener();
            TryUnregisterLateFrameTickable();
            ConnectionSplineBatchRenderer.RemoveRelayLink(_linkId);
        }

        public void LateFrameTick()
        {
            if (!TryResolveAupRuntimePosition(sourceSocket, out _, out Vector3 sourceRuntimePosition))
            {
                AbortRuntimeDragState(clearConnection: true);
                ConnectionSplineBatchRenderer.RemoveRelayLink(_linkId);
                RefreshLateFrameRegistration();
                return;
            }

            if (_dragging && IsCableOverstretched())
            {
                ForceCableTensionSnapRelease();
                return;
            }

            if (!_connected && !_dragging && !renderDisconnectedPreview)
            {
                ConnectionSplineBatchRenderer.RemoveRelayLink(_linkId);
                RefreshLateFrameRegistration();
                return;
            }

            BuildControlPoints(sourceRuntimePosition);
            float3 startForward = LogisticsPipeBuilder.SafeNormalize(_p1 - _p0, new float3(0f, 0f, 1f));
            float3 endForward = LogisticsPipeBuilder.SafeNormalize(_p3 - _p2, new float3(0f, 0f, -1f));
            SplineDescriptor descriptor = LogisticsPipeBuilder.CreateSocketDescriptor(
                _p0,
                _p3,
                startForward,
                endForward,
                ResolveSafeCableRadiusMeters(),
                PipeRenderFlags.None);
            ConnectionSplineBatchRenderer.SubmitRelaySpline(_linkId, descriptor, hasPower, poweredColor, unpoweredColor);
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 offset = shiftData.ShiftOffset;
            if (!IsFiniteVector(offset))
                return;

            _manualPlugPosition -= offset;
        }

        public void BeginDrag(Transform handAnchor)
        {
            if (sourceSocket == null)
                return;

            _dragAnchor = handAnchor;
            ResolveReleaseHandler(handAnchor);
            _dragging = true;
            _connected = false;
            RefreshLateFrameRegistration();
        }

        public void BeginDrag(Transform handAnchor, PhysicalInteractionHandler handler)
        {
            if (sourceSocket == null)
                return;

            _dragAnchor = handAnchor;
            if (handler != null)
                releaseHandler = handler;
            else
                ResolveReleaseHandler(handAnchor);

            _dragging = true;
            _connected = false;
            RefreshLateFrameRegistration();
        }

        public void SetManualDragPose(Vector3 runtimePosition, Vector3 forward)
        {
            _dragAnchor = null;
            if (IsFiniteVector(runtimePosition))
                _manualPlugPosition = runtimePosition;

            if (IsFiniteVector(forward) && forward.sqrMagnitude > 0.000001f)
                _manualPlugForward = SafeNormalize(forward, Vector3.forward);
            _dragging = true;
            _connected = false;
            RefreshLateFrameRegistration();
        }

        public void ConnectToDestination(Transform socket, bool powered)
        {
            if (!CanConnectToSocket(socket))
            {
                destinationSocket = null;
                hasPower = false;
                _connected = false;
                RefreshLateFrameRegistration();
                return;
            }

            destinationSocket = socket;
            hasPower = powered;
            _dragAnchor = null;
            _dragging = false;
            _connected = destinationSocket != null;
            RefreshLateFrameRegistration();
        }

        public void Disconnect()
        {
            EndDrag();
        }

        public void EndDrag()
        {
            ResolveRawEndPose(out _manualPlugPosition, out _manualPlugForward);
            SanitizeEndPose(ref _manualPlugPosition, ref _manualPlugForward);
            ClampEndToCableLength(ref _manualPlugPosition);
            if (plugVisual != null)
            {
                plugVisual.position = _manualPlugPosition;
                plugVisual.forward = SafeNormalize(_manualPlugForward, plugVisual.forward);
            }

            _connected = false;
            _dragging = false;
            _dragAnchor = null;
            RefreshLateFrameRegistration();
        }

        public Vector3 EvaluateCablePoint(float t)
        {
            float clamped = math.saturate(t);
            float3 point = EvaluateCubic(_p0, _p1, _p2, _p3, clamped);
            return new Vector3(point.x, point.y, point.z);
        }

        public void GetCableControlPoints(out Vector3 p0, out Vector3 p1, out Vector3 p2, out Vector3 p3)
        {
            p0 = new Vector3(_p0.x, _p0.y, _p0.z);
            p1 = new Vector3(_p1.x, _p1.y, _p1.z);
            p2 = new Vector3(_p2.x, _p2.y, _p2.z);
            p3 = new Vector3(_p3.x, _p3.y, _p3.z);
        }

        private void BuildControlPoints(Vector3 start)
        {
            if (!IsFiniteVector(start))
                start = Vector3.zero;

            Vector3 startForward = IsFiniteVector(sourceSocket.forward) ? sourceSocket.forward : Vector3.forward;
            ResolveEndPose(out Vector3 end, out Vector3 endForward);
            if (!IsFiniteVector(end))
                end = start;
            if (!IsFiniteVector(endForward))
                endForward = Vector3.back;

            Vector3 chord = end - start;
            float spanSq = chord.sqrMagnitude;
            float spanApprox = math.isfinite(spanSq) && spanSq > 0.000001f
                ? ApproximateMagnitudeNoSqrt(chord)
                : 0f;
            float handle = math.clamp(spanApprox * 0.35f, 0.05f, 1.75f);
            float sagAmount = math.isfinite(spanApprox) ? math.min(ResolveSafeSlackDepthMeters(), spanApprox * 0.35f) : 0f;

            _p0 = start;
            Vector3 p1 = start + SafeNormalize(startForward, Vector3.forward) * handle;
            Vector3 p2 = end - SafeNormalize(endForward, Vector3.back) * handle;
            ApplyQuadraticCatenarySag(ref p1, 1f / 3f, sagAmount);
            ApplyQuadraticCatenarySag(ref p2, 2f / 3f, sagAmount);
            _p1 = p1;
            _p2 = p2;
            _p3 = end;
        }

        private bool IsCableOverstretched()
        {
            ResolveRawEndPose(out Vector3 end, out _);
            if (!IsFiniteVector(end) ||
                !TryResolveAupRuntimePosition(sourceSocket, out AbsoluteUniversePosition sourceAup, out Vector3 sourceRuntimePosition) ||
                !TryResolveAupFromRuntimeDelta(end, sourceRuntimePosition, in sourceAup, out AbsoluteUniversePosition endAup))
            {
                return true;
            }

            return AbsoluteUniversePosition.DistanceSq(in sourceAup, in endAup) > ResolveSafeMaxCableLengthSq();
        }

        private bool CanConnectToSocket(Transform socket)
        {
            if (sourceSocket == null || socket == null)
                return false;

            if (!TryResolveAup(sourceSocket, out AbsoluteUniversePosition sourceAup) ||
                !TryResolveAup(socket, out AbsoluteUniversePosition socketAup))
            {
                return false;
            }

            return AbsoluteUniversePosition.DistanceSq(in sourceAup, in socketAup) <= ResolveSafeMaxCableLengthSq();
        }

        private void ForceCableTensionSnapRelease()
        {
            ResolveRawEndPose(out _manualPlugPosition, out _manualPlugForward);
            SanitizeEndPose(ref _manualPlugPosition, ref _manualPlugForward);
            ClampEndToCableLength(ref _manualPlugPosition);
            if (plugVisual != null)
            {
                plugVisual.position = _manualPlugPosition;
                plugVisual.forward = SafeNormalize(_manualPlugForward, plugVisual.forward);
            }

            if (releaseHandler != null)
                releaseHandler.ForceRelease();

            AbortRuntimeDragState(clearConnection: true);
            ConnectionSplineBatchRenderer.RemoveRelayLink(_linkId);
        }

        private void AbortRuntimeDragState(bool clearConnection)
        {
            _dragAnchor = null;
            _dragging = false;
            if (clearConnection)
                _connected = false;
            RefreshLateFrameRegistration();
        }

        private void ResolveReleaseHandler(Transform handAnchor)
        {
            if (handAnchor == null)
                return;

            if (TryResolveParentComponent(handAnchor, out PhysicalInteractionHandler resolvedHandler))
                releaseHandler = resolvedHandler;
        }

        private void ResolveEndPose(out Vector3 end, out Vector3 endForward)
        {
            ResolveRawEndPose(out end, out endForward);
            SanitizeEndPose(ref end, ref endForward);
            ClampEndToCableLength(ref end);
        }

        private void ResolveRawEndPose(out Vector3 end, out Vector3 endForward)
        {
            if (_connected && destinationSocket != null)
            {
                end = ResolveAupRuntimePosition(destinationSocket);
                endForward = IsFiniteVector(destinationSocket.forward) ? -destinationSocket.forward : Vector3.back;
                return;
            }

            Transform anchor = _dragAnchor != null ? _dragAnchor : plugVisual;
            if (anchor != null)
            {
                end = ResolveAupRuntimePosition(anchor);
                endForward = IsFiniteVector(anchor.forward) ? anchor.forward : _manualPlugForward;
                return;
            }

            end = _manualPlugPosition;
            endForward = _manualPlugForward;
        }

        private void SanitizeEndPose(ref Vector3 end, ref Vector3 endForward)
        {
            if (!IsFiniteVector(end))
            {
                if (TryResolveAup(sourceSocket, out AbsoluteUniversePosition sourceAup))
                    end = (Vector3)sourceAup.ToRuntimeFloat3();
                else
                    end = Vector3.zero;
            }

            endForward = SafeNormalize(endForward, Vector3.forward);
        }

        private void ClampEndToCableLength(ref Vector3 end)
        {
            if (sourceSocket == null)
                return;

            if (!TryResolveAupRuntimePosition(sourceSocket, out AbsoluteUniversePosition sourceAup, out Vector3 sourceRuntimePosition) ||
                !IsFiniteVector(end) ||
                !TryResolveAupFromRuntimeDelta(end, sourceRuntimePosition, in sourceAup, out AbsoluteUniversePosition endAup))
            {
                return;
            }

            double lengthSq = AbsoluteUniversePosition.DistanceSq(in sourceAup, in endAup);
            if (double.IsNaN(lengthSq) || double.IsInfinity(lengthSq))
            {
                end = sourceRuntimePosition;
                return;
            }

            float safeMaxCableLength = ResolveSafeMaxCableLengthMeters();
            float maxLengthSq = safeMaxCableLength * safeMaxCableLength;
            if (lengthSq <= maxLengthSq || lengthSq <= 0.000001f)
                return;

            float3 aupDelta = AbsoluteUniversePosition.ToCameraRelativeFloat3(in endAup, in sourceAup);
            float deltaLengthSq = math.lengthsq(aupDelta);
            if (!math.isfinite(deltaLengthSq) || deltaLengthSq <= 0.000001f || !math.all(math.isfinite(aupDelta)))
            {
                end = sourceRuntimePosition;
                return;
            }

            float approximateLength = ApproximateMagnitudeNoSqrt(aupDelta);
            if (!math.isfinite(approximateLength) || approximateLength <= 0.000001f)
            {
                end = sourceRuntimePosition;
                return;
            }

            float inverseLength = math.rcp(approximateLength);
            float3 clampedDelta = aupDelta * inverseLength * safeMaxCableLength;
            if (!math.all(math.isfinite(clampedDelta)))
            {
                end = sourceRuntimePosition;
                return;
            }

            end = sourceRuntimePosition + new Vector3(clampedDelta.x, clampedDelta.y, clampedDelta.z);
        }

        private float ResolveSafeMaxCableLengthMeters()
        {
            return math.isfinite(maxCableLengthMeters)
                ? math.clamp(maxCableLengthMeters, 0.25f, MaximumCableLengthMeters)
                : 8f;
        }

        private float ResolveSafeMaxCableLengthSq()
        {
            float safeMaxCableLength = ResolveSafeMaxCableLengthMeters();
            return safeMaxCableLength * safeMaxCableLength;
        }

        private float ResolveSafeCableRadiusMeters()
        {
            return math.isfinite(cableRadiusMeters)
                ? math.clamp(cableRadiusMeters, 0.001f, MaximumCableRadiusMeters)
                : 0.028f;
        }

        private float ResolveSafeSlackDepthMeters()
        {
            return math.isfinite(slackDepthMeters)
                ? math.clamp(slackDepthMeters, 0f, MaximumSlackDepthMeters)
                : 0.45f;
        }

        private static AbsoluteUniversePosition ResolveAup(Transform source)
        {
            return TryResolveAup(source, out AbsoluteUniversePosition aup)
                ? aup
                : GlobalSignals.CurrentRuntimeOriginAup();
        }

        private static bool TryResolveAup(Transform source, out AbsoluteUniversePosition aup)
        {
            if (source == null)
            {
                aup = default;
                return false;
            }

            Vector3 position = source.position;
            if (!IsFiniteVector(position))
            {
                aup = default;
                return false;
            }

            return TryResolveAupFromRuntimeOrigin(position, out aup);
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition aup)
        {
            return math.isfinite(aup.LocalX) &&
                   math.isfinite(aup.LocalY) &&
                   math.isfinite(aup.LocalZ);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition aup)
        {
            aup = default;
            if (!IsFiniteVector(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
            if (!IsFiniteAup(in originAup))
                return false;

            aup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return IsFiniteAup(in aup);
        }

        private static bool TryResolveAupFromRuntimeDelta(
            Vector3 runtimePosition,
            Vector3 originRuntimePosition,
            in AbsoluteUniversePosition originAup,
            out AbsoluteUniversePosition aup)
        {
            aup = default;
            if (!IsFiniteVector(runtimePosition) ||
                !IsFiniteVector(originRuntimePosition) ||
                !IsFiniteAup(in originAup))
            {
                return false;
            }

            double3 deltaMeters = new double3(
                (double)runtimePosition.x - originRuntimePosition.x,
                (double)runtimePosition.y - originRuntimePosition.y,
                (double)runtimePosition.z - originRuntimePosition.z);
            aup = AbsoluteUniversePosition.OffsetMeters(in originAup, deltaMeters);
            return IsFiniteAup(in aup);
        }

        private static Vector3 ResolveAupRuntimePosition(Transform source)
        {
            return TryResolveAupRuntimePosition(source, out _, out Vector3 runtimePosition)
                ? runtimePosition
                : Vector3.zero;
        }

        private static bool TryResolveAupRuntimePosition(
            Transform source,
            out AbsoluteUniversePosition aup,
            out Vector3 runtimePosition)
        {
            if (!TryResolveAup(source, out aup))
            {
                runtimePosition = Vector3.zero;
                return false;
            }

            runtimePosition = (Vector3)aup.ToRuntimeFloat3();
            return IsFiniteVector(runtimePosition);
        }

        private static float3 EvaluateCubic(float3 p0, float3 p1, float3 p2, float3 p3, float t)
        {
            float omt = 1f - t;
            float omt2 = omt * omt;
            float omt3 = omt2 * omt;
            float t2 = t * t;
            float t3 = t2 * t;
            return p0 * omt3 +
                   p1 * (3f * omt2 * t) +
                   p2 * (3f * omt * t2) +
                   p3 * t3;
        }

        private static Vector3 SafeNormalize(Vector3 value, Vector3 fallback)
        {
            Vector3 safeFallback = IsFiniteVector(fallback) ? fallback : Vector3.forward;
            float lengthSq = value.sqrMagnitude;
            if (!math.isfinite(lengthSq) || lengthSq <= 0.000001f || !math.all(math.isfinite(new float3(value.x, value.y, value.z))))
                return safeFallback;

            float approximateLength = ApproximateMagnitudeNoSqrt(value);
            if (!math.isfinite(approximateLength) || approximateLength <= 0.000001f)
                return safeFallback;

            Vector3 normalized = value * math.rcp(approximateLength);
            return IsFiniteVector(normalized) ? normalized : safeFallback;
        }

        private static float ApproximateMagnitudeNoSqrt(Vector3 value)
        {
            return ApproximateMagnitudeNoSqrt(new float3(value.x, value.y, value.z));
        }

        private static float ApproximateMagnitudeNoSqrt(float3 value)
        {
            float3 absValue = math.abs(value);
            if (!math.all(math.isfinite(absValue)))
                return 0f;

            float largest = math.cmax(absValue);
            if (!math.isfinite(largest) || largest <= 0f)
                return 0f;

            float3 normalized = absValue * math.rcp(largest);
            float smallest = math.cmin(normalized);
            float middle = normalized.x + normalized.y + normalized.z - 1f - smallest;
            float estimate = largest * (1f + (middle * 0.375f) + (smallest * 0.125f));
            if (math.isfinite(estimate))
                return estimate;

            return largest;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return math.all(math.isfinite(new float3(value.x, value.y, value.z)));
        }

        private static void ApplyQuadraticCatenarySag(ref Vector3 point, float t, float sagAmount)
        {
            if (!math.isfinite(sagAmount) || sagAmount <= 0f)
                return;

            float centered = (t * 2f) - 1f;
            point.y -= sagAmount * (1f - (centered * centered));
        }

        private static bool TryResolveParentComponent<T>(Transform start, out T component) where T : Component
        {
            component = null;
            Transform current = start;
            int depth = 0;
            while (current != null && depth++ < MaxParentResolveDepth)
            {
                if (current.TryGetComponent(out component))
                    return true;

                current = current.parent;
            }

            return false;
        }

        private void TryRegisterLateFrameTickable()
        {
            if (_registeredLateFrame || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void RefreshLateFrameRegistration()
        {
            if (!isActiveAndEnabled)
            {
                TryUnregisterLateFrameTickable();
                return;
            }

            if (ShouldRunLateFrame())
                TryRegisterLateFrameTickable();
            else
                TryUnregisterLateFrameTickable();
        }

        private bool ShouldRunLateFrame()
        {
            return sourceSocket != null && (_dragging || _connected || renderDisconnectedPreview);
        }

        private void TryUnregisterLateFrameTickable()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _registeredLateFrame = false;
        }

        private void TryRegisterOriginShiftListener()
        {
            if (_registeredOriginShift || !Application.isPlaying)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _registeredOriginShift = HectonFloatingOrigin.IsListenerRegistered(this);
        }

        private void TryUnregisterOriginShiftListener()
        {
            if (!_registeredOriginShift)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            _registeredOriginShift = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!math.isfinite(cableRadiusMeters) || cableRadiusMeters < 0.001f)
                cableRadiusMeters = 0.001f;
            cableRadiusMeters = math.min(cableRadiusMeters, MaximumCableRadiusMeters);
            if (!math.isfinite(slackDepthMeters) || slackDepthMeters < 0f)
                slackDepthMeters = 0f;
            slackDepthMeters = math.min(slackDepthMeters, MaximumSlackDepthMeters);
            if (!math.isfinite(maxCableLengthMeters) || maxCableLengthMeters < 0.25f)
                maxCableLengthMeters = 0.25f;
            maxCableLengthMeters = math.min(maxCableLengthMeters, MaximumCableLengthMeters);
        }
#endif
    }
}

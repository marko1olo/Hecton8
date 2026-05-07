using Hecton8.Core;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Interaction
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Interaction/VR Cable Drag Plug")]
    public sealed class VRCableDragPlug : MonoBehaviour, ILateFrameTickable, IOriginShiftListener
    {
        private const long CableLinkSalt = 0x5643524300000000L;

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
        public float MaxCableLengthSq => maxCableLengthMeters * maxCableLengthMeters;
        public bool HasPower
        {
            get => hasPower;
            set => hasPower = value;
        }

        private void Awake()
        {
            _linkId = CableLinkSalt ^ unchecked((long)EntityId.ToULong(GetEntityId()));
            _manualPlugPosition = plugVisual != null ? plugVisual.position : transform.position;
            _manualPlugForward = plugVisual != null ? plugVisual.forward : transform.forward;
        }

        private void OnEnable()
        {
            TryRegisterLateFrameTickable();
            TryRegisterOriginShiftListener();
        }

        private void OnDisable()
        {
            TryUnregisterOriginShiftListener();
            TryUnregisterLateFrameTickable();
            ConnectionSplineBatchRenderer.RemoveRelayLink(_linkId);
        }

        public void LateFrameTick()
        {
            if (sourceSocket == null)
                return;

            if (_dragging && IsCableOverstretched())
            {
                ForceCableTensionSnapRelease();
                return;
            }

            if (!_connected && !_dragging && !renderDisconnectedPreview)
            {
                ConnectionSplineBatchRenderer.RemoveRelayLink(_linkId);
                return;
            }

            BuildControlPoints();
            float3 startForward = LogisticsPipeBuilder.SafeNormalize(_p1 - _p0, new float3(0f, 0f, 1f));
            float3 endForward = LogisticsPipeBuilder.SafeNormalize(_p3 - _p2, new float3(0f, 0f, -1f));
            SplineDescriptor descriptor = LogisticsPipeBuilder.CreateSocketDescriptor(
                _p0,
                _p3,
                startForward,
                endForward,
                cableRadiusMeters,
                PipeRenderFlags.None);
            ConnectionSplineBatchRenderer.SubmitRelaySpline(_linkId, descriptor, hasPower, poweredColor, unpoweredColor);
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 offset = shiftData.ShiftOffset;
            _manualPlugPosition -= offset;
        }

        public void BeginDrag(Transform handAnchor)
        {
            _dragAnchor = handAnchor;
            ResolveReleaseHandler(handAnchor);
            _dragging = true;
            _connected = false;
        }

        public void SetManualDragPose(Vector3 runtimePosition, Vector3 forward)
        {
            _manualPlugPosition = runtimePosition;
            if (forward.sqrMagnitude > 0.000001f)
                _manualPlugForward = SafeNormalize(forward, Vector3.forward);
            _dragging = true;
            _connected = false;
        }

        public void ConnectToDestination(Transform socket, bool powered)
        {
            destinationSocket = socket;
            hasPower = powered;
            _dragging = false;
            _connected = destinationSocket != null;
        }

        public void Disconnect()
        {
            _connected = false;
            _dragging = false;
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

        private void BuildControlPoints()
        {
            Vector3 start = sourceSocket.position;
            Vector3 startForward = sourceSocket.forward;
            ResolveEndPose(out Vector3 end, out Vector3 endForward);

            Vector3 chord = end - start;
            float spanSq = chord.sqrMagnitude;
            float spanApprox = spanSq > 0.000001f ? spanSq / math.max(maxCableLengthMeters, 0.001f) : 0f;
            float handle = math.clamp(spanApprox * 0.35f, 0.05f, 1.75f);
            float sagAmount = math.min(slackDepthMeters, spanApprox * 0.35f);

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
            AbsoluteUniversePosition sourceAup = AbsoluteUniversePosition.FromRuntimePosition(sourceSocket.position);
            AbsoluteUniversePosition endAup = AbsoluteUniversePosition.FromRuntimePosition(end);
            return AbsoluteUniversePosition.DistanceSq(in sourceAup, in endAup) > MaxCableLengthSq;
        }

        private void ForceCableTensionSnapRelease()
        {
            ResolveRawEndPose(out _manualPlugPosition, out _manualPlugForward);
            ClampEndToCableLength(ref _manualPlugPosition);
            if (plugVisual != null)
                plugVisual.position = _manualPlugPosition;

            if (releaseHandler != null)
                releaseHandler.ForceRelease();

            _dragAnchor = null;
            _dragging = false;
            _connected = false;
            ConnectionSplineBatchRenderer.RemoveRelayLink(_linkId);
        }

        private void ResolveReleaseHandler(Transform handAnchor)
        {
            if (handAnchor == null)
                return;

            PhysicalInteractionHandler resolvedHandler = handAnchor.GetComponentInParent<PhysicalInteractionHandler>();
            if (resolvedHandler != null)
                releaseHandler = resolvedHandler;
        }

        private void ResolveEndPose(out Vector3 end, out Vector3 endForward)
        {
            ResolveRawEndPose(out end, out endForward);
            ClampEndToCableLength(ref end);
        }

        private void ResolveRawEndPose(out Vector3 end, out Vector3 endForward)
        {
            if (_connected && destinationSocket != null)
            {
                end = destinationSocket.position;
                endForward = -destinationSocket.forward;
                return;
            }

            Transform anchor = _dragAnchor != null ? _dragAnchor : plugVisual;
            if (anchor != null)
            {
                end = anchor.position;
                endForward = anchor.forward;
                return;
            }

            end = _manualPlugPosition;
            endForward = _manualPlugForward;
        }

        private void ClampEndToCableLength(ref Vector3 end)
        {
            if (sourceSocket == null)
                return;

            Vector3 sourceRuntimePosition = sourceSocket.position;
            AbsoluteUniversePosition sourceAup = AbsoluteUniversePosition.FromRuntimePosition(sourceRuntimePosition);
            AbsoluteUniversePosition endAup = AbsoluteUniversePosition.FromRuntimePosition(end);
            double lengthSq = AbsoluteUniversePosition.DistanceSq(in sourceAup, in endAup);
            float maxLengthSq = MaxCableLengthSq;
            if (lengthSq <= maxLengthSq || lengthSq <= 0.000001f)
                return;

            float3 aupDelta = AbsoluteUniversePosition.ToCameraRelativeFloat3(in endAup, in sourceAup);
            float deltaLengthSq = math.lengthsq(aupDelta);
            if (deltaLengthSq <= 0.000001f || !math.all(math.isfinite(aupDelta)))
            {
                end = sourceRuntimePosition;
                return;
            }

            float3 clampedDelta = aupDelta * math.rsqrt(deltaLengthSq) * maxCableLengthMeters;
            end = sourceRuntimePosition + new Vector3(clampedDelta.x, clampedDelta.y, clampedDelta.z);
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
            float lengthSq = value.sqrMagnitude;
            if (lengthSq <= 0.000001f || !math.all(math.isfinite(new float3(value.x, value.y, value.z))))
                return fallback;

            return value * math.rsqrt(lengthSq);
        }

        private static void ApplyQuadraticCatenarySag(ref Vector3 point, float t, float sagAmount)
        {
            float centered = (t * 2f) - 1f;
            point.y -= sagAmount * (1f - (centered * centered));
        }

        private void TryRegisterLateFrameTickable()
        {
            if (_registeredLateFrame || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Player);
            _registeredLateFrame = SystemDispatcher.GetLateFrameLane(PriorityLayer.Player).Contains(this);
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
            if (_registeredOriginShift)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _registeredOriginShift = true;
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
            if (cableRadiusMeters < 0.001f)
                cableRadiusMeters = 0.001f;
            if (slackDepthMeters < 0f)
                slackDepthMeters = 0f;
            if (maxCableLengthMeters < 0.25f)
                maxCableLengthMeters = 0.25f;
        }
#endif
    }
}

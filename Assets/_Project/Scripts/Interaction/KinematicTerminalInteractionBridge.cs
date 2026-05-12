using Hecton8.Core;
using Hecton8.Tools;
using Hecton8.UI;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Interaction
{
    [System.Flags]
    public enum TerminalActionFlags : byte
    {
        None = 0,
        Hover = 1 << 0,
        Press = 1 << 1,
        Hold = 1 << 2,
        Release = 1 << 3,
        Scroll = 1 << 4,
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public readonly struct KinematicTerminalPointerState
    {
        public KinematicTerminalPointerState(
            int panelId,
            float2 canvasPosition,
            Vector3 worldPosition,
            Quaternion worldRotation,
            TerminalActionFlags actionFlags)
        {
            PanelId = panelId;
            CanvasPosition = canvasPosition;
            WorldPosition = worldPosition;
            WorldRotation = worldRotation;
            ActionFlags = actionFlags;
        }

        public readonly int PanelId;
        public readonly float2 CanvasPosition;
        public readonly Vector3 WorldPosition;
        public readonly Quaternion WorldRotation;
        public readonly TerminalActionFlags ActionFlags;
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public readonly struct PhysicalHandIkTarget
    {
        public PhysicalHandIkTarget(
            int sourceId,
            PhysicalHandSide handSide,
            Vector3 worldPosition,
            Quaternion worldRotation,
            float holdSeconds,
            float blend)
        {
            SourceId = sourceId;
            HandSide = handSide;
            WorldPosition = worldPosition;
            WorldRotation = worldRotation;
            HoldSeconds = holdSeconds;
            Blend = blend;
        }

        public readonly int SourceId;
        public readonly PhysicalHandSide HandSide;
        public readonly Vector3 WorldPosition;
        public readonly Quaternion WorldRotation;
        public readonly float HoldSeconds;
        public readonly float Blend;
    }

    public interface IPhysicalHandIkTargetSink
    {
        void SetTerminalHandTarget(in PhysicalHandIkTarget target);

        void ClearTerminalHandTarget(int sourceId);
    }

    public interface IKinematicTerminalButtonResolver
    {
        bool TryResolveButtonSnap(in KinematicTerminalPointerState pointer, out float2 canvasSnapPosition);
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Interaction/Kinematic Terminal Interaction Bridge")]
    public sealed class KinematicTerminalInteractionBridge : MonoBehaviour, ITickable, IUpdatable
    {
        private const float MinimumTickIntervalSeconds = 0.033333335f;
        private const float MaximumTickIntervalSeconds = 0.5f;
        private const float DefaultTerminalTickIntervalSeconds = 0.1f;
        private const float LowTierTerminalTickIntervalSeconds = 0.2f;
        private const float MinimumReachMeters = 0.25f;
        private const float MaximumReachMeters = 2f;
        private const float MinimumSnapDurationSeconds = 0.033333335f;
        private const float MaximumSnapDurationSeconds = 0.35f;
        private const byte TerminalHapticPriority = 1;
        private const byte LeftMotorMask = 0b0001;
        private const byte RightMotorMask = 0b0010;
        private const uint LowTierQualityMask =
            (1u << (int)HectonQualityTier.Unknown) |
            (1u << (int)HectonQualityTier.Low) |
            (1u << (int)HectonQualityTier.Mx350);

        [Header("Panel")]
        [SerializeField] private DiegeticPanelController panel;
        [SerializeField] private MonoBehaviour panelReceiver;
        [SerializeField] private MonoBehaviour buttonResolver;
        [SerializeField] private int panelId = 1;
        [SerializeField, Range(MinimumReachMeters, MaximumReachMeters)] private float maxInteractionDistance = 2f;

        [Header("Ray Source")]
        [SerializeField] private Transform rayOrigin;
        [SerializeField] private Transform rayDirectionSource;
        [SerializeField] private Camera interactionCamera;

        [Header("Hand IK")]
        [SerializeField] private PhysicalHandSide handSide = PhysicalHandSide.Right;
        [SerializeField] private PhysicalHandController physicalHandController;
        [SerializeField] private MonoBehaviour handIkTargetSink;
        [SerializeField] private Transform handSnapTarget;
        [SerializeField, Range(0.001f, 0.08f)] private float handSurfaceOffsetMeters = 0.025f;
        [SerializeField, Range(MinimumSnapDurationSeconds, MaximumSnapDurationSeconds)] private float snapHoldSeconds = 0.12f;

        [Header("Cadence")]
        [SerializeField, Range(MinimumTickIntervalSeconds, MaximumTickIntervalSeconds)] private float terminalTickIntervalSeconds = DefaultTerminalTickIntervalSeconds;
        [SerializeField] private bool dispatchPanelInputEvents = true;
        [SerializeField] private bool emitPressHaptics = true;

        private IPanelInteractable _panelReceiver;
        private IKinematicTerminalButtonResolver _buttonResolver;
        private IPhysicalHandIkTargetSink _handIkTargetSink;
        private IInputService _input;
        private bool _registered;
        private bool _pressedLastTick;
        private bool _handTargetActive;
        private float _tickAccumulator;
        private int _sourceId;

        private void Awake()
        {
            ResolveInterfaces();
            _sourceId = panelId != 0 ? panelId : unchecked((int)EntityId.ToULong(gameObject.GetEntityId()));
        }

        private void OnEnable()
        {
            ResolveInterfaces();
            TryRegister();
        }

        private void OnDisable()
        {
            ClearHandTarget();
            TryUnregister();
            _pressedLastTick = false;
            _tickAccumulator = 0f;
        }

        public void Tick(float deltaTime)
        {
            float safeDeltaTime = math.clamp(deltaTime, 0f, MaximumTickIntervalSeconds);
            _tickAccumulator += safeDeltaTime;
            float interval = ResolveTickInterval();
            if (_tickAccumulator < interval)
                return;

            _tickAccumulator = math.min(_tickAccumulator - interval, interval);
            RunTerminalTick();
        }

        private void RunTerminalTick()
        {
            if (panel == null || !ResolveRay(out Vector3 origin, out Vector3 direction))
            {
                HandleProjectionLost();
                return;
            }

            float reach = math.clamp(maxInteractionDistance, MinimumReachMeters, MaximumReachMeters);
            if (!panel.TryProjectRayToCanvas(origin, direction, reach, out float2 canvasPosition, out Vector3 worldHitPosition))
            {
                HandleProjectionLost();
                return;
            }

            if (!panel.TryGetPanelRotation(out Quaternion panelRotation) || !IsFinite(panelRotation))
                panelRotation = Quaternion.identity;

            TerminalActionFlags actionFlags = ResolveTerminalActionFlags(out float2 analogDelta);
            DiegeticPanelInputEventType panelEventType = ResolvePanelEventType(actionFlags);
            KinematicTerminalPointerState pointer = new KinematicTerminalPointerState(
                panelId,
                canvasPosition,
                worldHitPosition,
                panelRotation,
                actionFlags);

            float2 snapCanvasPosition = canvasPosition;
            if (_buttonResolver != null &&
                _buttonResolver.TryResolveButtonSnap(in pointer, out float2 resolvedSnapCanvasPosition))
            {
                snapCanvasPosition = resolvedSnapCanvasPosition;
            }

            if (panel.TryProjectCanvasPointToWorld(snapCanvasPosition, handSurfaceOffsetMeters, out Vector3 snapWorldPosition))
                DispatchHandIkTarget(snapWorldPosition, panelRotation);
            else
                ClearHandTarget();

            if (dispatchPanelInputEvents && _panelReceiver != null && panelEventType != DiegeticPanelInputEventType.None)
            {
                DiegeticPanelInputEvent inputEvent = new DiegeticPanelInputEvent
                {
                    PanelId = panelId,
                    CanvasHitPoint = canvasPosition,
                    AnalogDelta = analogDelta,
                    EventType = panelEventType,
                    Timestamp = Time.unscaledTime
                };
                _panelReceiver.ReceiveCanvasInput(in inputEvent);
            }

            if (emitPressHaptics && (actionFlags & TerminalActionFlags.Press) != 0)
            {
                ToolHapticsRuntime.EnqueueSinusoidalCommand(
                    0.04f,
                    0.22f,
                    0.05f,
                    54f,
                    TerminalHapticPriority,
                    ResolveMotorMask(handSide));
            }
        }

        private TerminalActionFlags ResolveTerminalActionFlags(out float2 analogDelta)
        {
            analogDelta = float2.zero;
            if (_input == null || !_input.IsInitialized)
                _input = GlobalRegistry.Input;

            bool pressed = false;
            if (_input != null && _input.IsInitialized)
            {
                PlayerInputState state = _input.GetState();
                pressed = state.HasAction(PlayerInputAction.Interact) || state.HasAction(PlayerInputAction.PrimaryFire);
                analogDelta = new float2(state.ScrollDelta.x, state.ScrollDelta.y);
            }

            TerminalActionFlags flags = TerminalActionFlags.Hover;
            flags |= (TerminalActionFlags)math.select(0, (int)TerminalActionFlags.Press, pressed && !_pressedLastTick);
            flags |= (TerminalActionFlags)math.select(0, (int)TerminalActionFlags.Hold, pressed);
            flags |= (TerminalActionFlags)math.select(0, (int)TerminalActionFlags.Release, !pressed && _pressedLastTick);
            flags |= (TerminalActionFlags)math.select(0, (int)TerminalActionFlags.Scroll, math.lengthsq(analogDelta) > 0.000001f);
            _pressedLastTick = pressed;
            return flags;
        }

        private static DiegeticPanelInputEventType ResolvePanelEventType(TerminalActionFlags actionFlags)
        {
            DiegeticPanelInputEventType eventType;
            if ((actionFlags & TerminalActionFlags.Press) != 0)
                eventType = DiegeticPanelInputEventType.Down;
            else if ((actionFlags & TerminalActionFlags.Release) != 0)
                eventType = DiegeticPanelInputEventType.Up;
            else if ((actionFlags & TerminalActionFlags.Hold) != 0)
                eventType = DiegeticPanelInputEventType.Hold;
            else
                eventType = DiegeticPanelInputEventType.Hover;

            if ((actionFlags & TerminalActionFlags.Scroll) != 0)
                eventType |= DiegeticPanelInputEventType.Scroll;

            return eventType;
        }

        private void DispatchHandIkTarget(Vector3 worldPosition, Quaternion worldRotation)
        {
            if (!IsFinite(worldPosition) || !IsFinite(worldRotation))
            {
                ClearHandTarget();
                return;
            }

            if (handSnapTarget != null)
                handSnapTarget.SetPositionAndRotation(worldPosition, worldRotation);

            PhysicalHandIkTarget target = new PhysicalHandIkTarget(
                _sourceId,
                handSide,
                worldPosition,
                worldRotation,
                math.clamp(snapHoldSeconds, MinimumSnapDurationSeconds, MaximumSnapDurationSeconds),
                1f);

            if (_handIkTargetSink != null)
                _handIkTargetSink.SetTerminalHandTarget(in target);
            else if (physicalHandController != null)
                physicalHandController.SetTerminalHandTarget(in target);

            _handTargetActive = true;
        }

        private void HandleProjectionLost()
        {
            if (_pressedLastTick && dispatchPanelInputEvents && _panelReceiver != null)
            {
                DiegeticPanelInputEvent inputEvent = new DiegeticPanelInputEvent
                {
                    PanelId = panelId,
                    CanvasHitPoint = float2.zero,
                    EventType = DiegeticPanelInputEventType.Up,
                    Timestamp = Time.unscaledTime
                };
                _panelReceiver.ReceiveCanvasInput(in inputEvent);
            }

            _pressedLastTick = false;
            ClearHandTarget();
        }

        private void ClearHandTarget()
        {
            if (!_handTargetActive)
                return;

            if (_handIkTargetSink != null)
                _handIkTargetSink.ClearTerminalHandTarget(_sourceId);
            else if (physicalHandController != null)
                physicalHandController.ClearTerminalHandTarget(_sourceId);

            _handTargetActive = false;
        }

        private bool ResolveRay(out Vector3 origin, out Vector3 direction)
        {
            Transform originTransform = rayOrigin;
            Transform directionTransform = rayDirectionSource;
            if (originTransform == null || directionTransform == null)
            {
                Camera camera = interactionCamera;
                if (camera == null && GlobalRegistry.Player != null)
                    camera = GlobalRegistry.Player.PlayerCamera;
                if (camera == null)
                {
                    origin = default;
                    direction = default;
                    return false;
                }

                Transform cameraTransform = camera.transform;
                if (originTransform == null)
                    originTransform = cameraTransform;
                if (directionTransform == null)
                    directionTransform = cameraTransform;
            }

            origin = originTransform.position;
            direction = directionTransform.forward;
            return IsFinite(origin) && IsFinite(direction) && math.lengthsq((float3)direction) > 0.0001f;
        }

        private void ResolveInterfaces()
        {
            _panelReceiver = panelReceiver as IPanelInteractable;
            _buttonResolver = buttonResolver as IKinematicTerminalButtonResolver;
            _handIkTargetSink = handIkTargetSink as IPhysicalHandIkTargetSink;
            _input = GlobalRegistry.Input;
        }

        private float ResolveTickInterval()
        {
            float configuredInterval = math.clamp(
                math.isfinite(terminalTickIntervalSeconds) ? terminalTickIntervalSeconds : DefaultTerminalTickIntervalSeconds,
                MinimumTickIntervalSeconds,
                MaximumTickIntervalSeconds);

            int tierIndex = math.min((int)GlobalRegistry.ScalabilityTier, 31);
            bool lowTier = ((LowTierQualityMask >> tierIndex) & 1u) != 0u;
            float tierFloor = math.select(MinimumTickIntervalSeconds, LowTierTerminalTickIntervalSeconds, lowTier);
            return math.max(configuredInterval, tierFloor);
        }

        private static byte ResolveMotorMask(PhysicalHandSide side)
        {
            return side == PhysicalHandSide.Left ? LeftMotorMask : RightMotorMask;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registered = GlobalRegistry.Updatables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registered = false;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !(float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z) ||
                     float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z));
        }

        private static bool IsFinite(Quaternion value)
        {
            return !(float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z) || float.IsNaN(value.w) ||
                     float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z) || float.IsInfinity(value.w));
        }
    }

}

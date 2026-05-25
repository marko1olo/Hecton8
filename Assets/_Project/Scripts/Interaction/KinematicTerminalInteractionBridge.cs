using Hecton8.Core;
using Hecton8.Tools;
using Hecton8.UI;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Interaction
{
    internal static class KinematicTerminalInteractionLayout
    {
        public const int PointerStateStrideBytes = 64;
        public const int PhysicalHandIkTargetStrideBytes = 64;
    }

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

    [StructLayout(LayoutKind.Explicit, Size = KinematicTerminalInteractionLayout.PointerStateStrideBytes)]
    public readonly struct KinematicTerminalPointerState
    {
        public KinematicTerminalPointerState(
            int panelId,
            float2 canvasPosition,
            Vector3 worldPosition,
            Quaternion worldRotation,
            TerminalActionFlags actionFlags)
        {
            this = default;
            PanelId = panelId;
            CanvasPosition = canvasPosition;
            WorldPosition = worldPosition;
            WorldRotation = worldRotation;
            ActionFlags = actionFlags;
        }

        [FieldOffset(0)]
        public readonly int PanelId;
        [FieldOffset(4)]
        public readonly float2 CanvasPosition;
        [FieldOffset(12)]
        public readonly Vector3 WorldPosition;
        [FieldOffset(24)]
        public readonly Quaternion WorldRotation;
        [FieldOffset(40)]
        public readonly TerminalActionFlags ActionFlags;
        [FieldOffset(41)]
        private readonly byte _pad0;
        [FieldOffset(42)]
        private readonly byte _pad1;
        [FieldOffset(43)]
        private readonly byte _pad2;
        [FieldOffset(44)]
        private readonly uint _pad3;
        [FieldOffset(48)]
        private readonly ulong _pad4;
        [FieldOffset(56)]
        private readonly ulong _pad5;
    }

    [StructLayout(LayoutKind.Explicit, Size = KinematicTerminalInteractionLayout.PhysicalHandIkTargetStrideBytes)]
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
            this = default;
            SourceId = sourceId;
            HandSide = handSide;
            WorldPosition = worldPosition;
            WorldRotation = worldRotation;
            HoldSeconds = holdSeconds;
            Blend = blend;
        }

        [FieldOffset(0)]
        public readonly int SourceId;
        [FieldOffset(4)]
        public readonly PhysicalHandSide HandSide;
        [FieldOffset(8)]
        public readonly Vector3 WorldPosition;
        [FieldOffset(20)]
        public readonly Quaternion WorldRotation;
        [FieldOffset(36)]
        public readonly float HoldSeconds;
        [FieldOffset(40)]
        public readonly float Blend;
        [FieldOffset(44)]
        private readonly uint _pad0;
        [FieldOffset(48)]
        private readonly ulong _pad1;
        [FieldOffset(56)]
        private readonly ulong _pad2;
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
    public sealed class KinematicTerminalInteractionBridge : MonoBehaviour, ITickable, IUpdatable, ILateFrameTickable, IGlobalRegistryHotSwapListener
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
        private IPlayerRuntimeContext _playerRuntimeContext;
        private bool _registered;
        private bool _registeredLateFrame;
        private bool _registeredHotSwapListener;
        private bool _pressedLastTick;
        private bool _handTargetActive;
        private bool _pendingPressHaptic;
        private byte _pendingPressHapticMotorMask;
        private float _tickAccumulator;
        private int _sourceId;

        private void Awake()
        {
            ResolveInterfaces();
            RefreshColdRegistryReferences();
            _sourceId = panelId != 0 ? panelId : unchecked((int)EntityId.ToULong(gameObject.GetEntityId()));
        }

        private void OnEnable()
        {
            ResolveInterfaces();
            RefreshColdRegistryReferences();
            TryRegisterHotSwapListener();
            TryRegister();
        }

        private void OnDisable()
        {
            ClearHandTarget();
            TryUnregister();
            TryUnregisterHotSwapListener();
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
                _pendingPressHapticMotorMask = ResolveMotorMask(handSide);
                _pendingPressHaptic = true;
            }
        }

        public void LateFrameTick()
        {
            if (!_pendingPressHaptic)
                return;

            _pendingPressHaptic = false;
            ToolHapticsRuntime.TryEnqueueSinusoidalCommand(
                0.04f,
                0.22f,
                0.05f,
                54f,
                TerminalHapticPriority,
                _pendingPressHapticMotorMask);
        }

        private TerminalActionFlags ResolveTerminalActionFlags(out float2 analogDelta)
        {
            analogDelta = float2.zero;
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
                IPlayerRuntimeContext playerRuntimeContext = _playerRuntimeContext;
                if (camera == null && playerRuntimeContext != null)
                    camera = playerRuntimeContext.PlayerCamera;
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
        }

        private float ResolveTickInterval()
        {
            float configuredInterval = math.clamp(
                math.isfinite(terminalTickIntervalSeconds) ? terminalTickIntervalSeconds : DefaultTerminalTickIntervalSeconds,
                MinimumTickIntervalSeconds,
                MaximumTickIntervalSeconds);

            float quality = HomeostasisBrain.GlobalQualityWeight;
            quality = math.saturate(math.select(1f, quality, math.isfinite(quality)));
            float qualityCurve = math.smoothstep(0f, 1f, quality);
            float tierFloor = math.lerp(LowTierTerminalTickIntervalSeconds, MinimumTickIntervalSeconds, qualityCurve);
            return math.max(configuredInterval, tierFloor);
        }

        private static byte ResolveMotorMask(PhysicalHandSide side)
        {
            return side == PhysicalHandSide.Left ? LeftMotorMask : RightMotorMask;
        }

        private void TryRegister()
        {
            if ((_registered && _registeredLateFrame) || !Application.isPlaying)
                return;

            if (!_registered)
                _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void TryUnregister()
        {
            if (_registered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
                _registered = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _registeredLateFrame = false;
            }
        }

        private void RefreshColdRegistryReferences()
        {
            _input = GlobalRegistry.Input;
            _playerRuntimeContext = Hecton8.Core.GlobalRegistry.Player;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Input:
                    _input = currentService as IInputService;
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryRegister();
                    break;
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
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

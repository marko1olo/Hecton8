namespace Hecton8.Interaction
{
    using Hecton8.Core;
    using Hecton8.Tools;
    using Hecton8.World;
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    /// Collider-driven cockpit toggle that snaps to authored angles and emits a short haptic click.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    [AddComponentMenu("Hecton8/Interaction/Physical Snap Switch")]
    public sealed class PhysicalSnapSwitch : MonoBehaviour, IUpdatable, IPhysicalPanelButtonReceiver
    {
        private const uint PhysicalSwitchToolId = 0x53574954u;
        private const float MinimumDeltaTime = 0.0001f;
        private const byte BothMotorMask = 0b0011;
        private const byte CriticalPriority = 3;

        private enum SnapAxis : byte
        {
            X = 0,
            Y = 1,
            Z = 2
        }

        [Header("References")]
        [SerializeField] private BoxCollider activationVolume;
        [SerializeField] private Transform leverTransform;

        [Header("Snap")]
        [SerializeField] private SnapAxis snapAxis = SnapAxis.X;
        [SerializeField, Range(-90f, 90f)] private float offAngleDegrees = -28f;
        [SerializeField, Range(-90f, 90f)] private float onAngleDegrees = 28f;
        [SerializeField, Range(4f, 80f)] private float snapSpeed = 36f;
        [SerializeField, Range(0.02f, 0.5f)] private float snapCooldownSeconds = 0.08f;
        [SerializeField] private bool initialOn;

        private Quaternion _baseLocalRotation;
        private float _currentAngle;
        private float _targetAngle;
        private float _nextSnapTime;
        private bool _isOn;
        private bool _registered;
        private int _lastHandInsideFrame = -1;

        public bool IsOn => _isOn;
        public Collider ActivationCollider => activationVolume;

        public bool TryQueueHandPress(
            Vector3 handPosition,
            Vector3 handForward,
            IInteractionSignalService interactionSignals,
            Collider handSourceCollider,
            PhysicalHandSide fallbackHandSide)
        {
            if (activationVolume == null)
                return false;

            _lastHandInsideFrame = Time.frameCount;
            Vector3 localPoint = activationVolume.transform.InverseTransformPoint(handPosition) - activationVolume.center;
            bool desiredOn = ResolveDesiredState(localPoint);
            if (desiredOn == _isOn || Time.unscaledTime < _nextSnapTime)
                return true;

            _isOn = desiredOn;
            _targetAngle = _isOn ? onAngleDegrees : offAngleDegrees;
            _nextSnapTime = Time.unscaledTime + snapCooldownSeconds;
            PublishSwitchSignal(handPosition, handForward, interactionSignals);
            EnqueueClickHaptic();
            return true;
        }

        bool IPhysicalPanelButtonReceiver.TryQueueHandPress(
            Vector3 handPosition,
            Vector3 handForward,
            IInteractionSignalService interactionSignals,
            Collider handSourceCollider,
            PhysicalHandSide fallbackHandSide)
        {
            return TryQueueHandPress(handPosition, handForward, interactionSignals, handSourceCollider, fallbackHandSide);
        }

        private void Awake()
        {
            ResolveReferences();
            _isOn = initialOn;
            _currentAngle = _isOn ? onAngleDegrees : offAngleDegrees;
            _targetAngle = _currentAngle;
            if (leverTransform != null)
                _baseLocalRotation = leverTransform.localRotation;
            ApplyAngle(_currentAngle);
        }

        private void OnEnable()
        {
            ResolveReferences();
            RegisterCollider();
            TryRegister();
        }

        private void OnDisable()
        {
            Unregister();
            UnregisterCollider();
            _lastHandInsideFrame = -1;
        }

        private void OnDestroy()
        {
            UnregisterCollider();
        }

        public void Tick(float dt)
        {
            bool handInside = _lastHandInsideFrame == Time.frameCount;
            if (!handInside && math.abs(_targetAngle - _currentAngle) < 0.001f)
                return;

            float alpha = 1f - math.exp(-snapSpeed * math.max(dt, MinimumDeltaTime));
            _currentAngle = math.lerp(_currentAngle, _targetAngle, alpha);
            ApplyAngle(_currentAngle);
        }

        private void ResolveReferences()
        {
            if (activationVolume == null)
                TryGetComponent(out activationVolume);
            if (leverTransform == null)
                leverTransform = transform;
            if (activationVolume != null)
                activationVolume.isTrigger = true;
            if (_baseLocalRotation == default && leverTransform != null)
                _baseLocalRotation = leverTransform.localRotation;
        }

        private void RegisterCollider()
        {
            if (activationVolume != null)
                PhysicalHandReceiverRegistry.Register(activationVolume, this);
        }

        private void UnregisterCollider()
        {
            if (activationVolume != null)
                PhysicalHandReceiverRegistry.Unregister(activationVolume, this);
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registered = GlobalRegistry.Updatables.Contains(this);
        }

        private void Unregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registered = false;
        }

        private bool ResolveDesiredState(Vector3 localPoint)
        {
            switch (snapAxis)
            {
                case SnapAxis.Y:
                    return localPoint.y >= 0f;
                case SnapAxis.Z:
                    return localPoint.z >= 0f;
                default:
                    return localPoint.x >= 0f;
            }
        }

        private void ApplyAngle(float angleDegrees)
        {
            if (leverTransform == null)
                return;

            leverTransform.localRotation = _baseLocalRotation * Quaternion.AngleAxis(angleDegrees, ResolveAxisVector());
        }

        private Vector3 ResolveAxisVector()
        {
            switch (snapAxis)
            {
                case SnapAxis.Y:
                    return Vector3.up;
                case SnapAxis.Z:
                    return Vector3.forward;
                default:
                    return Vector3.right;
            }
        }

        private void PublishSwitchSignal(Vector3 handPosition, Vector3 handForward, IInteractionSignalService interactionSignals)
        {
            if (interactionSignals == null || !interactionSignals.IsInitialized)
                return;

            Vector3 absoluteHitPoint = HectonFloatingOrigin.ToAbsoluteUniversePosition(handPosition);
            Vector3 safeDirection = (Vector3)math.normalizesafe((float3)handForward, (float3)transform.forward);
            InteractionPacket packet = new InteractionPacket(
                PhysicalSwitchToolId,
                (float3)absoluteHitPoint,
                (float3)safeDirection,
                _isOn ? 1f : 0.5f,
                math.abs(onAngleDegrees - offAngleDegrees),
                (byte)ToolActionMode.Primary,
                (byte)ToolStateBits.Active,
                unchecked((uint)Time.frameCount));
            InteractionSignal signal = new InteractionSignal(
                packet,
                0,
                (float3)absoluteHitPoint,
                (float3)(-safeDirection),
                _isOn ? 1f : 0.5f,
                (byte)InteractionEffectType.Drill,
                0);

            interactionSignals.Publish(in signal, activationVolume);
        }

        private static void EnqueueClickHaptic()
        {
            ToolHapticsRuntime.EnqueueSinusoidalCommand(
                0.16f,
                0.42f,
                0.045f,
                92f,
                CriticalPriority,
                BothMotorMask);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (activationVolume == null)
                TryGetComponent(out activationVolume);
            if (activationVolume != null)
                activationVolume.isTrigger = true;
        }
#endif
    }
}

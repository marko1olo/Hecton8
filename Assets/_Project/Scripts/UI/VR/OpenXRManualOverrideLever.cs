using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Interaction;
using Hecton8.Tools;
using Hecton8.UI.VR.Contracts;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace Hecton8.UI.VR
{
    /// <summary>
    /// OpenXR-ready kinematic cockpit lever that consumes agnostic grip input, accepts physical hand proximity, and emits the manual override signal on latch.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    [AddComponentMenu("Hecton8/UI/VR/OpenXR Manual Override Lever")]
    public sealed class OpenXRManualOverrideLever : MonoBehaviour, IUpdatable, ILateFrameTickable, IPhysicalPanelButtonReceiver, IManualOverrideLeverReadModel, IGlobalRegistryHotSwapListener
    {
        private static int s_x001OpenXRManualOverrideLeverSignalPushDropCount;
        private const int LeverCount = 1;
        private const int BlackBoxFrameCount = 300;
        private const int BlackBoxDumpEntryBytes = 41;
        private const float MaxDeltaSeconds = 0.05f;
        private const float MinAxisLengthSq = 0.000001f;
        private const float DegreesToRadians = 0.01745329252f;
        private const float DegreesPerRadian = 57.29578f;
        private const float TwoPi = 6.28318530718f;
        private const float HalfPi = 1.57079632679f;
        private const float Pi = 3.14159265359f;
        private const uint SourceHash = PrologueSignalSourceHashes.ManualOverrideLever;
        private const uint GripActionMask = (uint)PlayerInputAction.Interact | (uint)PlayerInputAction.SecondaryFire;
        private const byte HapticPriorityCritical = 3;
        private const byte HapticLeftHandMask = 0b0001;
        private const byte HapticRightHandMask = 0b0010;
        private const byte HapticBothHandsMask = HapticLeftHandMask | HapticRightHandMask;
        private const int MaxStaleGrabFrames = 3;
        private const SystemID VaultOwnerSystemId = SystemID.UI;
        private const BufferID BlackBoxBufferId = BufferID.OpenXrManualOverrideLeverBlackBox;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_1335_OpenXRManualOverrideLever.bin";

        [Header("References")]
        [SerializeField] private BoxCollider activationVolume;
        [SerializeField] private Transform leverVisual;
        [SerializeField] private Transform handleAnchor;
        [SerializeField] private Transform handIkTarget;

        [Header("Lever")]
        [SerializeField] private Vector3 localRotationAxis = Vector3.right;
        [SerializeField] private Vector3 pivotLocalPosition;
        [SerializeField] private float minAngleDegrees;
        [SerializeField] private float maxAngleDegrees = 90f;
        [SerializeField] private float latchAngleDegrees = 85f;
        [SerializeField, Min(0.01f)] private float grabRadiusMeters = 0.15f;
        [SerializeField, Min(0f)] private float springStiffness = 42f;
        [SerializeField, Min(0f)] private float springDamping = 13f;
        [SerializeField, Min(1f)] private float maxVelocityDegreesPerSecond = 360f;

        [Header("Fallback")]
        [SerializeField, Min(0.05f)] private float nonVrPullSeconds = 1.5f;
        [FormerlySerializedAs("lowTierIkBlend")]
        [SerializeField, Range(0f, 1f)] private float minimumQualityIkBlend = 0.35f;
        [FormerlySerializedAs("highTierIkBlend")]
        [SerializeField, Range(0f, 1f)] private float maximumQualityIkBlend = 0.85f;
        [SerializeField] private bool emitPrologueComplete = true;

        private VaultGenerationHandle<ManualOverrideLeverTelemetryEntry> _blackBoxHandle;
        private IDataVault _dataVault;

        private Transform _cachedTransform;
        private Transform _resolvedVisual;
        private Quaternion _closedLocalRotation = Quaternion.identity;
        private Vector3 _resolvedLocalAxis = Vector3.right;
        private Vector3 _referenceLocalVector = Vector3.forward;
        private float3 _axisLocalFloat = new float3(1f, 0f, 0f);
        private float3 _referenceLocalFloat = new float3(0f, 0f, 1f);
        private float3 _leverPivot;
        private float3 _lastHandLocalPosition;
        private PhysicalHandSide _lastHandSide;
        private UniversalInputStateSignal _lastInputSignal;
        private IInputService _inputService;
        private Collider _registeredActivationVolume;
        private float _leverAngle;
        private float _leverVelocity;
        private float _leverTarget;
        private float _grabRadiusSq;
        private float _nonVrHold01;
        private int _frameThisTick;
        private int _blackBoxWriteIndex;
        private int _lastHandFrame = -1000;
        private int _lastRatchetStep = -1;
        private uint _inputSequence;
        private ushort _signalSequence;
        private bool _registeredTick;
        private bool _registeredLateFrame;
        private bool _registeredHotSwapListener;
        private bool _receiverRegistered;
        private bool _dispatcherAvailable;
        private bool _grabbed;
        private bool _latched;
        private float _ikQualityWeight01 = 1f;
        private float _activeIkBlend = 0.85f;
        private bool _nativeAllocated;
        private bool _projectionSingular;
        private bool _xrActiveThisFrame;
        private bool _blackBoxDumped;
        private bool _leverVisualDirty;
        private bool _ratchetHapticDirty;
        private bool _latchHapticDirty;
        private bool _pendingLatchShutdown;
        private float _pendingLeverVisualAngle;
        private float _pendingRatchetHapticAngle;
        private byte _latchedHandSide;

        /// <inheritdoc />
        public float AngleDegrees => _nativeAllocated ? _leverAngle : minAngleDegrees;

        /// <inheritdoc />
        public float Normalized01 => _nativeAllocated ? ResolveNormalized01(_leverAngle) : 0f;

        /// <inheritdoc />
        public float VelocityDegreesPerSecond => _nativeAllocated ? _leverVelocity : 0f;

        /// <inheritdoc />
        public bool IsGrabbed => _grabbed;

        /// <inheritdoc />
        public bool IsLatched => _latched;

        /// <inheritdoc />
        public byte ExecutionPhase => ManualOverrideLeverContractConstants.ExecutionPhaseSimulation;

        private void Awake()
        {
            EnsureReferences();
            CacheConfiguration();
            AllocateNativeState();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!VerifyProjectionMath())
                Hecton8.Core.H8Debug.LogError("[OpenXRManualOverrideLever] Projection dot/cross verification failed.", this);
#endif
            _closedLocalRotation = IsFiniteQuaternion(_resolvedVisual.localRotation)
                ? _resolvedVisual.localRotation
                : Quaternion.identity;
            InitializeLeverStateAfterAllocation();
        }

        private void OnEnable()
        {
            EnsureReferences();
            CacheConfiguration();
            EnsureNativeStateForLifecycle();
            _blackBoxDumped = false;
            _inputService = GlobalRegistry.Input;
            _dispatcherAvailable = GlobalRegistry.Dispatcher != null;
            RefreshQualityPolicy();
            TryRegisterHotSwapListener();
            TryRegisterTick();
            TryRegisterReceiver();
        }

        private void OnDisable()
        {
            _grabbed = false;
            _dispatcherAvailable = false;
            TryUnregisterTick();
            TryUnregisterReceiver();
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            _dispatcherAvailable = false;
            TryUnregisterTick();
            TryUnregisterReceiver();
            TryUnregisterHotSwapListener();
            DisposeNativeState();
        }

        /// <summary>
        /// Advances the lever through the dispatcher simulation lane without allocating or polling Unity input directly.
        /// </summary>
        /// <param name="deltaTime">Dispatcher-provided frame delta in seconds.</param>
        public void Tick(float deltaTime)
        {
            if (!_nativeAllocated)
                return;

            _frameThisTick = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            float dt = SanitizeDeltaSeconds(deltaTime);
            RefreshQualityPolicy();
            _lastInputSignal = BuildUniversalInputSignal();
            bool gripHeld = (_lastInputSignal.ActionsBitmask & GripActionMask) != 0u;
            _xrActiveThisFrame = HectonXRRuntimeState.IsXRActive;
            int handAgeFrames = _frameThisTick - _lastHandFrame;

            if (_latched)
            {
                _grabbed = false;
                _projectionSingular = false;
                _leverTarget = maxAngleDegrees;
            }
            else if (_xrActiveThisFrame)
            {
                UpdateVrGrab(gripHeld, handAgeFrames);
            }
            else
            {
                UpdateNonVrFallback(gripHeld, dt);
            }

            IntegrateSpring(dt);
            float currentAngle = _leverAngle;
            QueueLeverPresentation(currentAngle);
            TryLatch(currentAngle);
            currentAngle = _leverAngle;
            WriteBlackBoxFrame(currentAngle);
        }

        public void LateFrameTick()
        {
            float dt = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            if (_leverVisualDirty)
            {
                _leverVisualDirty = false;
                ApplyLeverVisual(_pendingLeverVisualAngle);
            }

            UpdateIkTarget(dt);

            if (_ratchetHapticDirty)
            {
                _ratchetHapticDirty = false;
                EmitRatchetHaptic(_pendingRatchetHapticAngle);
            }

            if (_latchHapticDirty)
            {
                _latchHapticDirty = false;
                PublishLatchHaptic();
            }

            if (_pendingLatchShutdown)
            {
                _pendingLatchShutdown = false;
            }
        }

        /// <summary>
        /// Queues a physical hand sample when it is close enough to the lever pivot or handle for the next simulation tick to consume.
        /// </summary>
        /// <param name="handPosition">World-space hand/controller position.</param>
        /// <param name="handForward">World-space hand forward vector supplied by the interaction bridge.</param>
        /// <param name="interactionSignals">Interaction signal service supplied by the physical hand bridge.</param>
        /// <param name="handSourceCollider">Collider that supplied the physical hand sample.</param>
        /// <param name="fallbackHandSide">Hand side reported by the physical hand bridge.</param>
        /// <param name="sampleFrame">Frame stamp captured once by the physical hand probe.</param>
        /// <returns>True when the hand sample is accepted into the lever's zero-GC state cache.</returns>
        public bool TryQueueHandPress(
            Vector3 handPosition,
            Vector3 handForward,
            IInteractionSignalService interactionSignals,
            Collider handSourceCollider,
            PhysicalHandSide fallbackHandSide,
            int sampleFrame = -1)
        {
            if (!_nativeAllocated || _latched || !IsFiniteVector(handPosition))
                return false;

            int currentFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            int resolvedSampleFrame = sampleFrame >= 0 ? sampleFrame : currentFrame;
            if (resolvedSampleFrame > currentFrame || resolvedSampleFrame < _lastHandFrame)
                return false;

            float3 localHand = WorldToLocal(handPosition);
            if (!IsFiniteFloat3(localHand))
                return false;

            float pivotDistanceSq = math.lengthsq(localHand - _leverPivot);
            if (pivotDistanceSq > _grabRadiusSq)
            {
                float handleDistanceSq = math.lengthsq(localHand - ResolveHandleLocalPosition());
                if (handleDistanceSq > _grabRadiusSq)
                    return false;
            }

            _lastHandLocalPosition = localHand;
            _lastHandSide = fallbackHandSide;
            _lastHandFrame = resolvedSampleFrame;
            return true;
        }

        private void UpdateVrGrab(bool gripHeld, int handAgeFrames)
        {
            if (!gripHeld)
            {
                _grabbed = false;
                _projectionSingular = false;
                _leverTarget = minAngleDegrees;
                return;
            }

            if (handAgeFrames > MaxStaleGrabFrames)
            {
                _grabbed = false;
                _projectionSingular = false;
                _leverTarget = minAngleDegrees;
                return;
            }

            if (handAgeFrames > 1)
            {
                _projectionSingular = false;
                if (_grabbed)
                    _leverTarget = _leverAngle;

                return;
            }

            _grabbed = true;
            _latchedHandSide = ResolveSignalHandSide(_lastHandSide);
            if (TrySolveAngleFromHand(_lastHandLocalPosition, _leverPivot, _axisLocalFloat, _referenceLocalFloat, minAngleDegrees, maxAngleDegrees, out float targetAngle))
            {
                _projectionSingular = false;
                _leverTarget = targetAngle;
            }
            else
            {
                _projectionSingular = true;
                _leverTarget = _leverAngle;
            }
        }

        private void UpdateNonVrFallback(bool gripHeld, float dt)
        {
            _grabbed = false;
            _projectionSingular = false;
            if (gripHeld)
            {
                _nonVrHold01 = math.saturate(_nonVrHold01 + dt * math.rcp(math.max(0.05f, nonVrPullSeconds)));
                _leverTarget = math.lerp(minAngleDegrees, maxAngleDegrees, _nonVrHold01);
                _latchedHandSide = ManualOverridePulledSignal.HandUnknown;
            }
            else if (!_latched)
            {
                _nonVrHold01 = math.saturate(_nonVrHold01 - dt);
                _leverTarget = math.lerp(minAngleDegrees, maxAngleDegrees, _nonVrHold01);
            }
        }

        private void IntegrateSpring(float dt)
        {
            float current = _leverAngle;
            float target = math.clamp(_leverTarget, minAngleDegrees, maxAngleDegrees);
            float velocity = _leverVelocity;
            velocity += (target - current) * math.max(0f, springStiffness) * dt;
            velocity *= math.rcp(1f + math.max(0f, springDamping) * dt);
            velocity = math.clamp(velocity, -maxVelocityDegreesPerSecond, maxVelocityDegreesPerSecond);
            current = math.clamp(current + velocity * dt, minAngleDegrees, maxAngleDegrees);

            if (!math.isfinite(current) || !math.isfinite(velocity))
            {
                DumpBlackBox();
                current = minAngleDegrees;
                velocity = 0f;
                target = minAngleDegrees;
            }

            _leverAngle = current;
            _leverVelocity = velocity;
            _leverTarget = target;
        }

        private void ApplyLeverVisual(float angleDegrees)
        {
            if (_resolvedVisual == null || !math.isfinite(angleDegrees))
                return;

            Quaternion targetRotation = _closedLocalRotation * ApproximateRotationDegreesNoTrig(angleDegrees, _resolvedLocalAxis);
            if (IsFiniteQuaternion(targetRotation))
                _resolvedVisual.localRotation = targetRotation;
        }

        private void UpdateIkTarget(float dt)
        {
            if (!_grabbed || handIkTarget == null || handleAnchor == null)
                return;

            float blend = _activeIkBlend;
            float step = math.saturate(blend * math.saturate(dt * 60f));
            if (step <= 0f)
                return;

            Vector3 handlePosition = handleAnchor.position;
            Quaternion handleRotation = handleAnchor.rotation;
            if (!IsFiniteVector(handlePosition) || !IsFiniteQuaternion(handleRotation))
                return;

            Vector3 currentPosition = handIkTarget.position;
            Quaternion currentRotation = handIkTarget.rotation;
            if (step >= 0.999f || !IsFiniteVector(currentPosition) || !IsFiniteQuaternion(currentRotation))
            {
                handIkTarget.SetPositionAndRotation(handlePosition, handleRotation);
                return;
            }

            Vector3 nextPosition = (Vector3)math.lerp((float3)currentPosition, (float3)handlePosition, step);
            Quaternion nextRotation = ApproximateNlerp(currentRotation, handleRotation, step);
            if (IsFiniteVector(nextPosition) && IsFiniteQuaternion(nextRotation))
                handIkTarget.SetPositionAndRotation(nextPosition, nextRotation);
        }

        private void EmitRatchetHaptic(float angleDegrees)
        {
            int step = (int)math.floor(math.abs(angleDegrees - minAngleDegrees) * 0.1f);
            if (step == _lastRatchetStep || step < 0)
                return;

            if (_lastRatchetStep < 0)
            {
                _lastRatchetStep = step;
                return;
            }

            if (!_grabbed && _nonVrHold01 <= 0f)
            {
                _lastRatchetStep = -1;
                return;
            }

            _lastRatchetStep = step;
            HapticRequest request = default;
            request.Intensity01 = 0.32f;
            request.DurationSeconds = 0.025f;
            request.Frequency01 = 0.72f;
            request.SourceHash = SourceHash;
            request.Frame = unchecked((uint)_frameThisTick);
            request.Channel = HapticRequest.ChannelGearScrape;
            SignalBus<HapticRequest>.TryPushTracked(in request, ref s_x001OpenXRManualOverrideLeverSignalPushDropCount);
            byte motorMask = _grabbed ? ResolveHapticMotorMask(_lastHandSide) : HapticBothHandsMask;
            ToolHapticsRuntime.TryEnqueueSinusoidalCommand(0.18f, 0.28f, 0.025f, 28f, HapticPriorityCritical, motorMask);
        }

        private void TryLatch(float currentAngle)
        {
            if (_latched || currentAngle < latchAngleDegrees)
                return;

            float latchVelocityDegreesPerSecond = _leverVelocity;
            _latched = true;
            _grabbed = false;
            _leverAngle = maxAngleDegrees;
            _leverTarget = maxAngleDegrees;
            _leverVelocity = 0f;
            QueueLeverPresentation(maxAngleDegrees);
            PublishManualOverrideSignal(latchVelocityDegreesPerSecond);
            _latchHapticDirty = true;

            if (emitPrologueComplete)
                PublishPrologueCompleteSignal();

            _pendingLatchShutdown = true;
        }

        private void PublishManualOverrideSignal(float latchVelocityDegreesPerSecond)
        {
            ManualOverridePulledSignal signal = default;
            signal.LeverLocalPosition = ResolveHandleLocalPosition();
            signal.PivotLocalPosition = _leverPivot;
            signal.AngleDegrees = _leverAngle;
            signal.GripStrength01 = (_lastInputSignal.ActionsBitmask & GripActionMask) != 0u ? 1f : 0f;
            signal.SourceHash = SourceHash;
            signal.Frame = unchecked((uint)_frameThisTick);
            signal.Sequence = ++_signalSequence;
            signal.HandSide = _latchedHandSide;
            signal.VelocityDegreesPerSecond = latchVelocityDegreesPerSecond;
            signal.Flags = ManualOverridePulledSignal.FlagLatched;
            signal.Flags |= _xrActiveThisFrame
                ? ManualOverridePulledSignal.FlagVrGrip
                : ManualOverridePulledSignal.FlagNonVrFallback;
            SignalBus<ManualOverridePulledSignal>.TryPushTracked(in signal, ref s_x001OpenXRManualOverrideLeverSignalPushDropCount);
        }

        private void QueueLeverPresentation(float angleDegrees)
        {
            _pendingLeverVisualAngle = angleDegrees;
            _pendingRatchetHapticAngle = angleDegrees;
            _leverVisualDirty = true;
            _ratchetHapticDirty = true;
        }

        private float3 ResolveHandleLocalPosition()
        {
            if (handleAnchor == null || _cachedTransform == null)
                return _leverPivot;

            Vector3 local = _cachedTransform.InverseTransformPoint(handleAnchor.position);
            float3 handleLocal = new float3(local.x, local.y, local.z);
            return IsFiniteFloat3(handleLocal) ? handleLocal : _leverPivot;
        }

        private void PublishPrologueCompleteSignal()
        {
            PrologueCompleteSignal signal = default;
            signal.CapsuleAup = default;
            signal.Frame = unchecked((uint)_frameThisTick);
            signal.WhiteoutHoldSeconds = 0.4f;
            signal.SourceHash = SourceHash;
            signal.Sequence = _signalSequence;
            signal.Flags = PrologueCompleteSignal.FlagForceWhiteout;
            signal.Phase = PrologueCompleteSignal.PhaseOceanHandoff;
            SignalBus<PrologueCompleteSignal>.TryPushTracked(in signal, ref s_x001OpenXRManualOverrideLeverSignalPushDropCount);
        }

        private void PublishLatchHaptic()
        {
            HapticRequest request = default;
            request.Intensity01 = 0.9f;
            request.DurationSeconds = 0.11f;
            request.Frequency01 = 0.95f;
            request.SourceHash = SourceHash;
            request.Frame = unchecked((uint)_frameThisTick);
            request.Channel = HapticRequest.ChannelVehicleCritical;
            SignalBus<HapticRequest>.TryPushTracked(in request, ref s_x001OpenXRManualOverrideLeverSignalPushDropCount);
            byte motorMask = ResolveHapticMotorMask(_latchedHandSide);
            ToolHapticsRuntime.TryEnqueueSinusoidalCommand(0.75f, 0.95f, 0.11f, 42f, HapticPriorityCritical, motorMask);
        }

        private UniversalInputStateSignal BuildUniversalInputSignal()
        {
            PlayerInputState state = _inputService != null ? _inputService.GetState() : default;
            UniversalInputStateSignal signal = default;
            signal.Move = SanitizeSignedAxis2(new float2(state.MoveDelta.x, state.MoveDelta.y));
            signal.Look = SanitizeSignedAxis2(new float2(state.LookDelta.x, state.LookDelta.y));
            signal.Vertical = SanitizeSignedAxis(state.VerticalDelta);
            signal.ActionsBitmask = state.ActionsBitmask;
            signal.CurrentInputSchemeHash = state.CurrentInputSchemeHash;
            signal.Frame = unchecked((uint)_frameThisTick);
            signal.Sequence = ++_inputSequence;
            signal.Flags = (byte)((state.ActionsBitmask & GripActionMask) != 0u ? 1 : 0);
            return signal;
        }

        /// <summary>
        /// Rebinds cached registry services on cold hot-swap events without adding per-frame registry polling.
        /// </summary>
        /// <param name="serviceSlot">Registry slot that changed.</param>
        /// <param name="previousService">Previous service instance, unused by this lever.</param>
        /// <param name="currentService">Current service instance, if one is registered.</param>
        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            if (!isActiveAndEnabled)
                return;

            if (serviceSlot == GlobalRegistryServiceSlot.Input)
            {
                _inputService = currentService as IInputService;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregisterReceiver();
                TryUnregisterTick();
                _dispatcherAvailable = currentService != null;
                if (!_dispatcherAvailable)
                    return;

                EnsureNativeStateForLifecycle();
                TryRegisterTick();
                TryRegisterReceiver();
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                IDataVault previousVault = previousService is IDataVault oldVault ? oldVault : null;
                IDataVault nextVault = currentService is IDataVault vault ? vault : null;
                RebindDataVaultForLifecycle(nextVault, previousVault);
                EnsureNativeStateForLifecycle();
            }
        }

        private void WriteBlackBoxFrame(float angleDegrees)
        {
            float velocity = _leverVelocity;
            float target = _leverTarget;
            float3 handLocal = _lastHandLocalPosition;
            float3 pivotLocal = _leverPivot;
            if (!math.isfinite(angleDegrees) || !math.isfinite(velocity) || !math.isfinite(target) || !IsFiniteFloat3(handLocal) || !IsFiniteFloat3(pivotLocal))
            {
                DumpBlackBox();
                return;
            }

            ManualOverrideLeverTelemetryEntry entry = default;
            entry.HandLocalPosition = handLocal;
            entry.PivotLocalPosition = pivotLocal;
            entry.AngleDegrees = angleDegrees;
            entry.TargetAngleDegrees = target;
            entry.VelocityDegreesPerSecond = velocity;
            entry.Frame = unchecked((uint)_frameThisTick);
            entry.Flags = BuildTelemetryFlags();

            if (!TryAcquireBlackBoxWriteBuffer(out NativeArray<ManualOverrideLeverTelemetryEntry> blackBox))
                return;

            try
            {
                blackBox[_blackBoxWriteIndex] = entry;
            }
            finally
            {
                ReleaseBlackBoxWriteBuffer();
            }

            _blackBoxWriteIndex++;
            if (_blackBoxWriteIndex >= BlackBoxFrameCount)
                _blackBoxWriteIndex = 0;
        }

        private byte BuildTelemetryFlags()
        {
            byte flags = 0;
            if (_grabbed)
                flags |= 1 << 0;
            if (_latched)
                flags |= 1 << 1;
            if (ResolveIkQualityPressure01() > 0.001f)
                flags |= 1 << 2;
            if (_xrActiveThisFrame)
                flags |= 1 << 3;
            if (_projectionSingular)
                flags |= 1 << 4;
            if (_blackBoxDumped)
                flags |= 1 << 5;
            return flags;
        }

        private void DumpBlackBox()
        {
            if (_blackBoxDumped)
                return;

            _blackBoxDumped = true;

            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string path = Path.Combine(projectRoot, DumpRelativePath);
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    Span<byte> header = stackalloc byte[8];
                    BinaryPrimitives.WriteInt32LittleEndian(header.Slice(0, 4), BlackBoxFrameCount);
                    BinaryPrimitives.WriteInt32LittleEndian(header.Slice(4, 4), _blackBoxWriteIndex);
                    stream.Write(header);

                    Span<byte> entryBytes = stackalloc byte[BlackBoxDumpEntryBytes];
                    for (int i = 0; i < BlackBoxFrameCount; i++)
                    {
                        if (!TryReadBlackBoxEntry(i, out ManualOverrideLeverTelemetryEntry entry))
                            return;

                        WriteBlackBoxEntry(entryBytes, in entry);
                        stream.Write(entryBytes);
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
            catch (ArgumentException)
            {
            }
            catch (NotSupportedException)
            {
            }
        }

        private static void WriteBlackBoxEntry(Span<byte> destination, in ManualOverrideLeverTelemetryEntry entry)
        {
            WriteFloatLittleEndian(destination.Slice(0, 4), entry.HandLocalPosition.x);
            WriteFloatLittleEndian(destination.Slice(4, 4), entry.HandLocalPosition.y);
            WriteFloatLittleEndian(destination.Slice(8, 4), entry.HandLocalPosition.z);
            WriteFloatLittleEndian(destination.Slice(12, 4), entry.PivotLocalPosition.x);
            WriteFloatLittleEndian(destination.Slice(16, 4), entry.PivotLocalPosition.y);
            WriteFloatLittleEndian(destination.Slice(20, 4), entry.PivotLocalPosition.z);
            WriteFloatLittleEndian(destination.Slice(24, 4), entry.AngleDegrees);
            WriteFloatLittleEndian(destination.Slice(28, 4), entry.TargetAngleDegrees);
            WriteFloatLittleEndian(destination.Slice(32, 4), entry.VelocityDegreesPerSecond);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(36, 4), entry.Frame);
            destination[40] = entry.Flags;
        }

        private static void WriteFloatLittleEndian(Span<byte> destination, float value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination, BitConverter.SingleToInt32Bits(value));
        }

        private void EnsureReferences()
        {
            _cachedTransform = transform;
            if (activationVolume == null)
                TryGetComponent(out activationVolume);
            if (leverVisual == null)
                leverVisual = transform;

            _resolvedVisual = leverVisual != null ? leverVisual : transform;
            if (activationVolume != null)
                activationVolume.isTrigger = true;
        }

        private void CacheConfiguration()
        {
            _resolvedLocalAxis = NormalizeOr(localRotationAxis, Vector3.right);
            _axisLocalFloat = ToFloat3(_resolvedLocalAxis);
            if (!IsFiniteVector(pivotLocalPosition))
                pivotLocalPosition = Vector3.zero;

            minAngleDegrees = math.clamp(SanitizeFloat(minAngleDegrees, 0f), -180f, 180f);
            maxAngleDegrees = math.clamp(SanitizeFloat(maxAngleDegrees, 90f), minAngleDegrees + 1f, 180f);
            latchAngleDegrees = math.clamp(SanitizeFloat(latchAngleDegrees, 85f), minAngleDegrees, maxAngleDegrees);
            grabRadiusMeters = math.clamp(SanitizeFloat(grabRadiusMeters, 0.15f), 0.01f, 1f);
            _grabRadiusSq = grabRadiusMeters * grabRadiusMeters;
            springStiffness = math.clamp(SanitizeFloat(springStiffness, 42f), 0f, 240f);
            springDamping = math.clamp(SanitizeFloat(springDamping, 13f), 0f, 80f);
            maxVelocityDegreesPerSecond = math.clamp(SanitizeFloat(maxVelocityDegreesPerSecond, 360f), 1f, 1440f);
            nonVrPullSeconds = math.clamp(SanitizeFloat(nonVrPullSeconds, 1.5f), 0.05f, 10f);
            minimumQualityIkBlend = math.saturate(SanitizeFloat(minimumQualityIkBlend, 0.35f));
            maximumQualityIkBlend = math.saturate(SanitizeFloat(maximumQualityIkBlend, 0.85f));
            _referenceLocalVector = ResolveReferenceVector();
            _referenceLocalFloat = ToFloat3(_referenceLocalVector);
            if (_nativeAllocated)
                _leverPivot = ToFloat3(pivotLocalPosition);
        }

        private Vector3 ResolveReferenceVector()
        {
            Vector3 raw = Vector3.forward;
            if (handleAnchor != null)
            {
                Vector3 handleLocal = _cachedTransform != null
                    ? _cachedTransform.InverseTransformPoint(handleAnchor.position)
                    : handleAnchor.localPosition;
                raw = handleLocal - pivotLocalPosition;
            }

            Vector3 axisScaled = _resolvedLocalAxis * Vector3.Dot(raw, _resolvedLocalAxis);
            Vector3 projected = raw - axisScaled;
            if (projected.sqrMagnitude < MinAxisLengthSq)
            {
                Vector3 fallback = math.abs(_resolvedLocalAxis.y) < 0.9f ? Vector3.up : Vector3.forward;
                projected = fallback - _resolvedLocalAxis * Vector3.Dot(fallback, _resolvedLocalAxis);
            }

            return NormalizeOr(projected, Vector3.forward);
        }

        private void AllocateNativeState()
        {
            if (_nativeAllocated)
                return;

            if (!EnsureBlackBoxVaultBuffer())
                return;

            _leverPivot = ToFloat3(pivotLocalPosition);
            _nativeAllocated = true;
        }

        private void EnsureNativeStateForLifecycle()
        {
            if (_nativeAllocated)
                return;

            AllocateNativeState();
            if (!_nativeAllocated)
                return;

            CacheConfiguration();
            InitializeLeverStateAfterAllocation();
        }

        private void InitializeLeverStateAfterAllocation()
        {
            if (!_nativeAllocated)
                return;

            float initialAngle = math.clamp(minAngleDegrees, math.min(minAngleDegrees, maxAngleDegrees), math.max(minAngleDegrees, maxAngleDegrees));
            _leverAngle = initialAngle;
            _leverVelocity = 0f;
            _leverTarget = initialAngle;
            _blackBoxWriteIndex = 0;
            _lastRatchetStep = -1;
            _blackBoxDumped = false;
            ApplyLeverVisual(initialAngle);
        }

        private void DisposeNativeState()
        {
            if (!_nativeAllocated && !IsExactBlackBoxHandle())
                return;

            ReleaseBlackBoxHandle(_dataVault);
            ResetNativeStateAfterRelease();
        }

        private void ResetBlackBoxNativeEpochState()
        {
            _blackBoxHandle = default;
            _blackBoxWriteIndex = 0;
            _blackBoxDumped = false;
        }

        private void ResetNativeStateAfterRelease()
        {
            ResetBlackBoxNativeEpochState();
            _leverAngle = minAngleDegrees;
            _leverVelocity = 0f;
            _leverTarget = minAngleDegrees;
            _leverPivot = default;
            _nativeAllocated = false;
        }

        private void RebindDataVaultForLifecycle(IDataVault nextVault, IDataVault fallbackReleaseVault = null)
        {
            if (ReferenceEquals(_dataVault, nextVault))
                return;

            ReleaseBlackBoxHandle(_dataVault ?? fallbackReleaseVault);
            _dataVault = nextVault;
            ResetNativeStateAfterRelease();
        }

        private void ReleaseBlackBoxHandle(IDataVault vault)
        {
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsExactBlackBoxHandle() ||
                !vault.TryGetGenerationHandle(BlackBoxBufferId, out VaultGenerationHandle<ManualOverrideLeverTelemetryEntry> currentHandle) ||
                !IsExactBlackBoxHandle(in currentHandle) ||
                currentHandle.Generation != _blackBoxHandle.Generation)
            {
                return;
            }

            vault.ReleaseBuffer(in _blackBoxHandle);
        }

        private IDataVault CacheDataVaultCold()
        {
            IDataVault registryVault = GlobalRegistry.DataVault;
            if (!ReferenceEquals(_dataVault, registryVault))
                RebindDataVaultForLifecycle(registryVault);

            return _dataVault;
        }

        private bool EnsureBlackBoxVaultBuffer()
        {
            IDataVault vault = CacheDataVaultCold();
            if (vault == null || vault.IsCompactionFenceActive || vault.IsAllocationLocked)
                return false;

            if (IsExactBlackBoxHandle() &&
                vault.TryReadOnlyHandle(in _blackBoxHandle, out NativeArray<ManualOverrideLeverTelemetryEntry>.ReadOnly existing) &&
                existing.IsCreated &&
                existing.Length >= BlackBoxFrameCount &&
                !vault.IsCompactionFenceActive)
            {
                return true;
            }

            if (vault.IsCompactionFenceActive || vault.IsAllocationLocked)
                return false;

            ReleaseBlackBoxHandle(vault);

            if (vault.IsCompactionFenceActive || vault.IsAllocationLocked)
                return false;

            _blackBoxHandle = vault.EnsureGenerationHandle<ManualOverrideLeverTelemetryEntry>(
                BlackBoxBufferId,
                BlackBoxFrameCount,
                VaultOwnerSystemId,
                NativeArrayOptions.ClearMemory);

            bool ready = !vault.IsCompactionFenceActive &&
                         IsExactBlackBoxHandle() &&
                         vault.TryReadOnlyHandle(in _blackBoxHandle, out NativeArray<ManualOverrideLeverTelemetryEntry>.ReadOnly resolved) &&
                         resolved.IsCreated &&
                         resolved.Length >= BlackBoxFrameCount &&
                         !vault.IsCompactionFenceActive;
            if (!ready)
                ResetBlackBoxNativeEpochState();

            return ready;
        }

        private bool TryAcquireBlackBoxWriteBuffer(out NativeArray<ManualOverrideLeverTelemetryEntry> blackBox)
        {
            blackBox = default;
            IDataVault vault = _dataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsExactBlackBoxHandle() ||
                !vault.TryAcquireWriteLock(in _blackBoxHandle, VaultOwnerSystemId, out blackBox))
            {
                return false;
            }

            bool releaseOnExit = true;
            try
            {
                if (!vault.IsCompactionFenceActive && blackBox.IsCreated && blackBox.Length >= BlackBoxFrameCount)
                {
                    releaseOnExit = false;
                    return true;
                }

                return false;
            }
            finally
            {
                if (releaseOnExit)
                {
                    vault.ReleaseWriteLock(in _blackBoxHandle, VaultOwnerSystemId);
                    blackBox = default;
                }
            }
        }

        private void ReleaseBlackBoxWriteBuffer()
        {
            IDataVault vault = _dataVault;
            if (vault != null && IsExactBlackBoxHandle())
                vault.ReleaseWriteLock(in _blackBoxHandle, VaultOwnerSystemId);
        }

        private bool TryReadBlackBoxEntry(int index, out ManualOverrideLeverTelemetryEntry entry)
        {
            entry = default;
            if ((uint)index >= BlackBoxFrameCount)
                return false;

            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive || !IsExactBlackBoxHandle())
                return false;

            if (!vault.TryReadOnlyHandle(in _blackBoxHandle, out NativeArray<ManualOverrideLeverTelemetryEntry>.ReadOnly blackBox) ||
                vault.IsCompactionFenceActive ||
                !blackBox.IsCreated ||
                blackBox.Length <= index)
            {
                entry = default;
                return false;
            }

            entry = blackBox[index];
            if (!vault.IsCompactionFenceActive)
                return true;

            entry = default;
            return false;
        }

        private bool IsExactBlackBoxHandle()
        {
            return IsExactBlackBoxHandle(in _blackBoxHandle);
        }

        private static bool IsExactBlackBoxHandle(in VaultGenerationHandle<ManualOverrideLeverTelemetryEntry> handle)
        {
            return handle.BufferID == (uint)BlackBoxBufferId &&
                   handle.SystemID == (uint)VaultOwnerSystemId &&
                   handle.Generation != 0u;
        }

        private void TryRegisterReceiver()
        {
            if (_latched || !_nativeAllocated || !_registeredTick || activationVolume == null || !Application.isPlaying || !_dispatcherAvailable)
                return;

            Collider registeredVolume = _registeredActivationVolume;
            if (_receiverRegistered || registeredVolume != null)
            {
                if (_receiverRegistered && ReferenceEquals(registeredVolume, activationVolume))
                    return;

                TryUnregisterReceiver();
            }

            if (!PhysicalHandReceiverRegistry.TryRegister(activationVolume, this))
                return;

            _registeredActivationVolume = activationVolume;
            _receiverRegistered = true;
        }

        private void TryUnregisterReceiver()
        {
            Collider registeredVolume = _registeredActivationVolume;
            if (!_receiverRegistered && registeredVolume == null)
                return;

            if (registeredVolume != null)
                PhysicalHandReceiverRegistry.Unregister(registeredVolume, this);

            _registeredActivationVolume = null;
            _receiverRegistered = false;
        }

        private void TryRegisterTick()
        {
            if ((_registeredTick && _registeredLateFrame) || _latched || !_nativeAllocated || !Application.isPlaying || !_dispatcherAvailable)
                return;

            if (!_registeredTick)
                _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterTick()
        {
            if (_registeredTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
                _registeredTick = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
                _registeredLateFrame = false;
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || _latched || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        private void RefreshQualityPolicy()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            if (!math.isfinite(quality))
                quality = _ikQualityWeight01;

            _ikQualityWeight01 = math.saturate(quality);
            float curve = SmoothStep01(_ikQualityWeight01);
            _activeIkBlend = math.saturate(math.lerp(minimumQualityIkBlend, maximumQualityIkBlend, curve));
        }

        private float ResolveIkQualityPressure01()
        {
            return 1f - SmoothStep01(_ikQualityWeight01);
        }

        private float ResolveNormalized01(float angleDegrees)
        {
            return math.saturate((angleDegrees - minAngleDegrees) * math.rcp(math.max(0.001f, maxAngleDegrees - minAngleDegrees)));
        }

        private static float SmoothStep01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - 2f * t);
        }

        private float3 WorldToLocal(Vector3 world)
        {
            Vector3 local = _cachedTransform != null ? _cachedTransform.InverseTransformPoint(world) : world;
            return new float3(local.x, local.y, local.z);
        }

        private static float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
        }

        private static float SolveAngleFromHand(float3 handLocal, float3 pivotLocal, float3 axisLocal, float3 referenceLocal, float minimum, float maximum)
        {
            return TrySolveAngleFromHand(handLocal, pivotLocal, axisLocal, referenceLocal, minimum, maximum, out float angle)
                ? angle
                : minimum;
        }

        private static bool TrySolveAngleFromHand(float3 handLocal, float3 pivotLocal, float3 axisLocal, float3 referenceLocal, float minimum, float maximum, out float angle)
        {
            if (!IsFiniteFloat3(handLocal) || !IsFiniteFloat3(pivotLocal) || !IsFiniteFloat3(axisLocal) || !IsFiniteFloat3(referenceLocal))
            {
                angle = minimum;
                return false;
            }

            float3 fromPivot = handLocal - pivotLocal;
            float3 projected = fromPivot - axisLocal * math.dot(fromPivot, axisLocal);
            float projectedLengthSq = math.lengthsq(projected);
            if (projectedLengthSq < MinAxisLengthSq)
            {
                angle = minimum;
                return false;
            }

            projected *= math.rsqrt(projectedLengthSq);
            float signed = MathLodApproximation.ApproxAtan2Fast(math.dot(axisLocal, math.cross(referenceLocal, projected)), math.dot(referenceLocal, projected)) * DegreesPerRadian;
            angle = math.clamp(signed, minimum, maximum);
            return math.isfinite(angle);
        }

        private static float SanitizeDeltaSeconds(float value)
        {
            return math.isfinite(value) ? math.clamp(value, 0f, MaxDeltaSeconds) : 0f;
        }

        private static float SanitizeFloat(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        private static float SanitizeSignedAxis(float value)
        {
            return math.clamp(SanitizeFloat(value, 0f), -1f, 1f);
        }

        private static float2 SanitizeSignedAxis2(float2 value)
        {
            return new float2(SanitizeSignedAxis(value.x), SanitizeSignedAxis(value.y));
        }

        private static Vector3 NormalizeOr(Vector3 value, Vector3 fallback)
        {
            float lengthSq = value.sqrMagnitude;
            if (math.isfinite(lengthSq) && lengthSq >= MinAxisLengthSq)
                return value * math.rsqrt(lengthSq);

            float fallbackLengthSq = fallback.sqrMagnitude;
            if (math.isfinite(fallbackLengthSq) && fallbackLengthSq >= MinAxisLengthSq)
                return fallback * math.rsqrt(fallbackLengthSq);

            return Vector3.forward;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return IsFiniteFloat3(new float3(value.x, value.y, value.z));
        }

        private static bool IsFiniteFloat3(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        private static bool IsFiniteQuaternion(Quaternion value)
        {
            float4 q = new float4(value.x, value.y, value.z, value.w);
            return math.all(math.isfinite(q)) && math.lengthsq(q) > MinAxisLengthSq;
        }

        private static Quaternion ApproximateRotationDegreesNoTrig(float angleDegrees, Vector3 normalizedAxis)
        {
            ApproximateSinCosFullNoTrig(math.select(0f, angleDegrees, math.isfinite(angleDegrees)) * DegreesToRadians * 0.5f, out float sinHalf, out float cosHalf);
            Quaternion rotation = new Quaternion(
                normalizedAxis.x * sinHalf,
                normalizedAxis.y * sinHalf,
                normalizedAxis.z * sinHalf,
                cosHalf);
            return NormalizeQuaternion(rotation);
        }

        private static void ApproximateSinCosFullNoTrig(float radians, out float sin, out float cos)
        {
            float x = radians - (TwoPi * math.round(radians / TwoPi));
            float cosSign = 1f;
            if (x > HalfPi)
            {
                x = Pi - x;
                cosSign = -1f;
            }
            else if (x < -HalfPi)
            {
                x = -Pi - x;
                cosSign = -1f;
            }

            float x2 = x * x;
            sin = x * (1f - (x2 * (0.16666667f - (x2 * 0.008333333f))));
            cos = cosSign * (1f - (x2 * (0.5f - (x2 * 0.041666667f))));
        }

        private static Quaternion NormalizeQuaternion(Quaternion value)
        {
            float4 q = new float4(value.x, value.y, value.z, value.w);
            float lengthSq = math.lengthsq(q);
            if (!math.isfinite(lengthSq) || lengthSq < MinAxisLengthSq)
                return Quaternion.identity;

            q *= math.rsqrt(lengthSq);
            return new Quaternion(q.x, q.y, q.z, q.w);
        }

        private static Quaternion ApproximateNlerp(Quaternion from, Quaternion to, float t)
        {
            quaternion fromQ = new quaternion(from.x, from.y, from.z, from.w);
            quaternion toQ = new quaternion(to.x, to.y, to.z, to.w);
            if (math.dot(fromQ.value, toQ.value) < 0f)
                toQ.value = -toQ.value;

            quaternion blended = new quaternion(math.lerp(fromQ.value, toQ.value, math.saturate(t)));
            float lengthSq = math.dot(blended.value, blended.value);
            if (!math.isfinite(lengthSq) || lengthSq < MinAxisLengthSq)
                return to;

            blended.value *= math.rsqrt(lengthSq);
            return new Quaternion(blended.value.x, blended.value.y, blended.value.z, blended.value.w);
        }

        private static byte ResolveSignalHandSide(PhysicalHandSide side)
        {
            if (side == PhysicalHandSide.Left)
                return ManualOverridePulledSignal.HandLeft;

            if (side == PhysicalHandSide.Right)
                return ManualOverridePulledSignal.HandRight;

            return ManualOverridePulledSignal.HandUnknown;
        }

        private static byte ResolveHapticMotorMask(PhysicalHandSide side)
        {
            if (side == PhysicalHandSide.Left)
                return HapticLeftHandMask;

            if (side == PhysicalHandSide.Right)
                return HapticRightHandMask;

            return HapticBothHandsMask;
        }

        private static byte ResolveHapticMotorMask(byte signalHandSide)
        {
            if (signalHandSide == ManualOverridePulledSignal.HandLeft)
                return HapticLeftHandMask;

            if (signalHandSide == ManualOverridePulledSignal.HandRight)
                return HapticRightHandMask;

            return HapticBothHandsMask;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static bool VerifyProjectionMath()
        {
            float3 axis = new float3(1f, 0f, 0f);
            float3 reference = new float3(0f, 0f, 1f);
            float zero = SolveAngleFromHand(reference, float3.zero, axis, reference, 0f, 90f);
            float rightAngle = SolveAngleFromHand(new float3(0f, -1f, 0f), float3.zero, axis, reference, 0f, 90f);
            bool singularRejected = !TrySolveAngleFromHand(float3.zero, float3.zero, axis, reference, 0f, 90f, out _);
            return math.abs(zero) <= 0.01f && math.abs(rightAngle - 90f) <= 0.01f && singularRejected;
        }
#endif

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureReferences();
            CacheConfiguration();
        }
#endif

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 64)]
        private struct ManualOverrideLeverTelemetryEntry
        {
            [System.Runtime.InteropServices.FieldOffset(0)]
            public float3 HandLocalPosition;
            [System.Runtime.InteropServices.FieldOffset(12)]
            public float3 PivotLocalPosition;
            [System.Runtime.InteropServices.FieldOffset(24)]
            public float AngleDegrees;
            [System.Runtime.InteropServices.FieldOffset(28)]
            public float TargetAngleDegrees;
            [System.Runtime.InteropServices.FieldOffset(32)]
            public float VelocityDegreesPerSecond;
            [System.Runtime.InteropServices.FieldOffset(36)]
            public uint Frame;
            [System.Runtime.InteropServices.FieldOffset(40)]
            public byte Flags;
            [System.Runtime.InteropServices.FieldOffset(41)]
            private byte _pad0;
            [System.Runtime.InteropServices.FieldOffset(42)]
            private byte _pad1;
            [System.Runtime.InteropServices.FieldOffset(43)]
            private byte _pad2;
            [System.Runtime.InteropServices.FieldOffset(44)]
            private byte _pad3;
            [System.Runtime.InteropServices.FieldOffset(45)]
            private byte _pad4;
            [System.Runtime.InteropServices.FieldOffset(46)]
            private byte _pad5;
            [System.Runtime.InteropServices.FieldOffset(47)]
            private byte _pad6;
            [System.Runtime.InteropServices.FieldOffset(48)]
            private byte _pad7;
            [System.Runtime.InteropServices.FieldOffset(49)]
            private byte _pad8;
            [System.Runtime.InteropServices.FieldOffset(50)]
            private byte _pad9;
            [System.Runtime.InteropServices.FieldOffset(51)]
            private byte _pad10;
            [System.Runtime.InteropServices.FieldOffset(52)]
            private byte _pad11;
            [System.Runtime.InteropServices.FieldOffset(53)]
            private byte _pad12;
            [System.Runtime.InteropServices.FieldOffset(54)]
            private byte _pad13;
            [System.Runtime.InteropServices.FieldOffset(55)]
            private byte _pad14;
            [System.Runtime.InteropServices.FieldOffset(56)]
            private byte _pad15;
            [System.Runtime.InteropServices.FieldOffset(57)]
            private byte _pad16;
            [System.Runtime.InteropServices.FieldOffset(58)]
            private byte _pad17;
            [System.Runtime.InteropServices.FieldOffset(59)]
            private byte _pad18;
            [System.Runtime.InteropServices.FieldOffset(60)]
            private byte _pad19;
            [System.Runtime.InteropServices.FieldOffset(61)]
            private byte _pad20;
            [System.Runtime.InteropServices.FieldOffset(62)]
            private byte _pad21;
            [System.Runtime.InteropServices.FieldOffset(63)]
            private byte _pad22;
        }

    }
}

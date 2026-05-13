using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Signals;
using Hecton8.Input.Universal;
using Hecton8.Interaction;
using Hecton8.Tools;
using Hecton8.UI.VR.Contracts;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.XR;

namespace Hecton8.UI.VR
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    [AddComponentMenu("Hecton8/UI/VR/OpenXR Manual Override Lever")]
    public sealed class OpenXRManualOverrideLever : MonoBehaviour, IUpdatable, IPhysicalPanelButtonReceiver, IManualOverrideLeverReadModel
    {
        private const int LeverCount = 1;
        private const int BlackBoxFrameCount = 300;
        private const float DefaultDeltaSeconds = 0.016666668f;
        private const float MaxDeltaSeconds = 0.05f;
        private const float MinAxisLengthSq = 0.000001f;
        private const float DegreesPerRadian = 57.29578f;
        private const uint SourceHash = 0x4D4F5652u;
        private const uint GripActionMask = (uint)PlayerInputAction.Interact | (uint)PlayerInputAction.SecondaryFire;
        private const byte HapticPriorityCritical = 3;
        private const byte HapticBothHandsMask = 0b0011;
        private const string NativeMemoryOwner = nameof(OpenXRManualOverrideLever);
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_VR_COCKPIT_MANUAL_OVERRIDE.bin";

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
        [SerializeField, Range(0f, 1f)] private float lowTierIkBlend = 0.35f;
        [SerializeField, Range(0f, 1f)] private float highTierIkBlend = 0.85f;
        [SerializeField] private bool emitPrologueComplete = true;

        private NativeArray<float> _leverAngles;
        private NativeArray<float> _leverVelocities;
        private NativeArray<float> _leverTargets;
        private NativeArray<float3> _leverPivots;
        private NativeArray<ManualOverrideLeverTelemetryEntry> _blackBox;

        private Transform _cachedTransform;
        private Transform _resolvedVisual;
        private Quaternion _closedLocalRotation = Quaternion.identity;
        private Vector3 _resolvedLocalAxis = Vector3.right;
        private Vector3 _referenceLocalVector = Vector3.forward;
        private Vector3 _lastHandWorldPosition;
        private Vector3 _lastHandForward = Vector3.forward;
        private PhysicalHandSide _lastHandSide;
        private UniversalInputStateSignal _lastInputSignal;
        private IInputService _inputService;
        private float _grabRadiusSq;
        private float _nonVrHold01;
        private int _blackBoxWriteIndex;
        private int _lastHandFrame = -1000;
        private int _lastRatchetStep = -1;
        private uint _inputSequence;
        private ushort _signalSequence;
        private bool _registeredTick;
        private bool _receiverRegistered;
        private bool _grabbed;
        private bool _latched;
        private bool _lowTierMath;
        private bool _nativeAllocated;
        private byte _latchedHandSide;

        public float AngleDegrees => _nativeAllocated ? _leverAngles[0] : minAngleDegrees;
        public float Normalized01 => _nativeAllocated ? ResolveNormalized01(_leverAngles[0]) : 0f;
        public float VelocityDegreesPerSecond => _nativeAllocated ? _leverVelocities[0] : 0f;
        public bool IsGrabbed => _grabbed;
        public bool IsLatched => _latched;
        public byte ExecutionPhase => ManualOverrideLeverContractConstants.ExecutionPhaseSimulation;

        private void Awake()
        {
            EnsureReferences();
            CacheConfiguration();
            AllocateNativeState();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!VerifyProjectionMath())
                Debug.LogError("[OpenXRManualOverrideLever] Projection dot/cross verification failed.", this);
#endif
            _closedLocalRotation = IsFiniteQuaternion(_resolvedVisual.localRotation)
                ? _resolvedVisual.localRotation
                : Quaternion.identity;
            _leverAngles[0] = math.clamp(minAngleDegrees, math.min(minAngleDegrees, maxAngleDegrees), math.max(minAngleDegrees, maxAngleDegrees));
            _leverTargets[0] = _leverAngles[0];
            ApplyLeverVisual(_leverAngles[0]);
        }

        private void OnEnable()
        {
            EnsureReferences();
            CacheConfiguration();
            _inputService = GlobalRegistry.Input;
            _lowTierMath = ResolveLowTierMath();
            TryRegisterReceiver();
            TryRegisterTick();
        }

        private void OnDisable()
        {
            _grabbed = false;
            TryUnregisterTick();
            TryUnregisterReceiver();
        }

        private void OnDestroy()
        {
            TryUnregisterTick();
            TryUnregisterReceiver();
            DisposeNativeState();
        }

        public void Tick(float deltaTime)
        {
            if (!_nativeAllocated)
                return;

            float dt = SanitizeDeltaSeconds(deltaTime);
            _lastInputSignal = BuildUniversalInputSignal();
            bool gripHeld = (_lastInputSignal.ActionsBitmask & GripActionMask) != 0u;
            bool xrActive = XRSettings.enabled && XRSettings.isDeviceActive;
            bool hasFreshHand = (Time.frameCount - _lastHandFrame) <= 1;

            if (_latched)
            {
                _grabbed = false;
                _leverTargets[0] = maxAngleDegrees;
            }
            else if (xrActive)
            {
                UpdateVrGrab(gripHeld, hasFreshHand);
            }
            else
            {
                UpdateNonVrFallback(gripHeld, dt);
            }

            IntegrateSpring(dt);
            float currentAngle = _leverAngles[0];
            ApplyLeverVisual(currentAngle);
            UpdateIkTarget(dt);
            EmitRatchetHaptic(currentAngle);
            TryLatch(currentAngle);
            WriteBlackBoxFrame(currentAngle);
        }

        public bool TryQueueHandPress(
            Vector3 handPosition,
            Vector3 handForward,
            IInteractionSignalService interactionSignals,
            Collider handSourceCollider,
            PhysicalHandSide fallbackHandSide)
        {
            if (!_nativeAllocated || _latched || !IsFiniteVector(handPosition))
                return false;

            float3 localHand = WorldToLocal(handPosition);
            float3 delta = localHand - _leverPivots[0];
            if (math.lengthsq(delta) > _grabRadiusSq)
                return false;

            _lastHandWorldPosition = handPosition;
            _lastHandForward = IsFiniteVector(handForward) ? handForward : Vector3.forward;
            _lastHandSide = fallbackHandSide;
            _lastHandFrame = Time.frameCount;
            return true;
        }

        private void UpdateVrGrab(bool gripHeld, bool hasFreshHand)
        {
            if (!gripHeld)
            {
                _grabbed = false;
                return;
            }

            if (!hasFreshHand && !_grabbed)
                return;

            _grabbed = true;
            _latchedHandSide = ResolveSignalHandSide(_lastHandSide);
            float targetAngle = SolveAngleFromHand(WorldToLocal(_lastHandWorldPosition), _leverPivots[0], ToFloat3(_resolvedLocalAxis), ToFloat3(_referenceLocalVector), minAngleDegrees, maxAngleDegrees);
            _leverTargets[0] = targetAngle;
        }

        private void UpdateNonVrFallback(bool gripHeld, float dt)
        {
            _grabbed = false;
            if (gripHeld)
            {
                _nonVrHold01 = math.saturate(_nonVrHold01 + dt * math.rcp(math.max(0.05f, nonVrPullSeconds)));
                _leverTargets[0] = math.lerp(minAngleDegrees, maxAngleDegrees, _nonVrHold01);
                _latchedHandSide = ManualOverridePulledSignal.HandUnknown;
            }
            else if (!_latched)
            {
                _nonVrHold01 = math.saturate(_nonVrHold01 - dt);
                _leverTargets[0] = math.lerp(minAngleDegrees, maxAngleDegrees, _nonVrHold01);
            }
        }

        private void IntegrateSpring(float dt)
        {
            float current = _leverAngles[0];
            float target = math.clamp(_leverTargets[0], minAngleDegrees, maxAngleDegrees);
            float velocity = _leverVelocities[0];
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

            _leverAngles[0] = current;
            _leverVelocities[0] = velocity;
            _leverTargets[0] = target;
        }

        private void ApplyLeverVisual(float angleDegrees)
        {
            _resolvedVisual.localRotation = _closedLocalRotation * Quaternion.AngleAxis(angleDegrees, _resolvedLocalAxis);
        }

        private void UpdateIkTarget(float dt)
        {
            if (!_grabbed || handIkTarget == null || handleAnchor == null)
                return;

            float blend = _lowTierMath ? lowTierIkBlend : highTierIkBlend;
            float step = math.saturate(blend * math.max(1f, dt * 90f));
            if (step >= 0.999f)
            {
                handIkTarget.SetPositionAndRotation(handleAnchor.position, handleAnchor.rotation);
                return;
            }

            handIkTarget.position = Vector3.Lerp(handIkTarget.position, handleAnchor.position, step);
            handIkTarget.rotation = Quaternion.Slerp(handIkTarget.rotation, handleAnchor.rotation, step);
        }

        private void EmitRatchetHaptic(float angleDegrees)
        {
            int step = (int)math.floor(math.abs(angleDegrees - minAngleDegrees) * 0.1f);
            if (step == _lastRatchetStep || step < 0)
                return;

            if (!_grabbed && _nonVrHold01 <= 0f)
                return;

            _lastRatchetStep = step;
            HapticRequest request = default;
            request.Intensity01 = 0.32f;
            request.DurationSeconds = 0.025f;
            request.Frequency01 = 0.72f;
            request.SourceHash = SourceHash;
            request.Frame = unchecked((uint)Time.frameCount);
            request.Channel = HapticRequest.ChannelGearScrape;
            GlobalSignals.Publish(in request);
            ToolHapticsRuntime.EnqueueSinusoidalCommand(0.18f, 0.28f, 0.025f, 28f, HapticPriorityCritical, HapticBothHandsMask);
        }

        private void TryLatch(float currentAngle)
        {
            if (_latched || currentAngle < latchAngleDegrees)
                return;

            _latched = true;
            _grabbed = false;
            _leverAngles[0] = maxAngleDegrees;
            _leverTargets[0] = maxAngleDegrees;
            _leverVelocities[0] = 0f;
            ApplyLeverVisual(maxAngleDegrees);
            PublishManualOverrideSignal();
            PublishLatchHaptic();

            if (emitPrologueComplete)
                PublishPrologueCompleteSignal();
        }

        private void PublishManualOverrideSignal()
        {
            ManualOverridePulledSignal signal = default;
            signal.LeverLocalPosition = _leverPivots[0];
            signal.PivotLocalPosition = _leverPivots[0];
            signal.AngleDegrees = _leverAngles[0];
            signal.GripStrength01 = (_lastInputSignal.ActionsBitmask & GripActionMask) != 0u ? 1f : 0f;
            signal.SourceHash = SourceHash;
            signal.Frame = unchecked((uint)Time.frameCount);
            signal.Sequence = ++_signalSequence;
            signal.HandSide = _latchedHandSide;
            signal.VelocityDegreesPerSecond = _leverVelocities[0];
            signal.Flags = ManualOverridePulledSignal.FlagLatched;
            signal.Flags |= XRSettings.enabled && XRSettings.isDeviceActive
                ? ManualOverridePulledSignal.FlagVrGrip
                : ManualOverridePulledSignal.FlagNonVrFallback;
            GlobalSignals.Publish(in signal);
        }

        private void PublishPrologueCompleteSignal()
        {
            PrologueCompleteSignal signal = default;
            signal.CapsuleAup = default;
            signal.Frame = unchecked((uint)Time.frameCount);
            signal.WhiteoutHoldSeconds = 0.4f;
            signal.SourceHash = SourceHash;
            signal.Sequence = _signalSequence;
            signal.Flags = PrologueCompleteSignal.FlagForceWhiteout;
            signal.Phase = PrologueCompleteSignal.PhaseOceanHandoff;
            GlobalSignals.Publish(in signal);
        }

        private void PublishLatchHaptic()
        {
            HapticRequest request = default;
            request.Intensity01 = 0.9f;
            request.DurationSeconds = 0.11f;
            request.Frequency01 = 0.95f;
            request.SourceHash = SourceHash;
            request.Frame = unchecked((uint)Time.frameCount);
            request.Channel = HapticRequest.ChannelVehicleCritical;
            GlobalSignals.Publish(in request);
            ToolHapticsRuntime.EnqueueSinusoidalCommand(0.75f, 0.95f, 0.11f, 42f, HapticPriorityCritical, HapticBothHandsMask);
        }

        private UniversalInputStateSignal BuildUniversalInputSignal()
        {
            PlayerInputState state = _inputService != null ? _inputService.GetState() : default;
            UniversalInputStateSignal signal = default;
            signal.Move = new float2(state.MoveDelta.x, state.MoveDelta.y);
            signal.Look = new float2(state.LookDelta.x, state.LookDelta.y);
            signal.Vertical = state.VerticalDelta;
            signal.ActionsBitmask = state.ActionsBitmask;
            signal.CurrentInputSchemeHash = state.CurrentInputSchemeHash;
            signal.Frame = unchecked((uint)Time.frameCount);
            signal.Sequence = ++_inputSequence;
            signal.Flags = (byte)((state.ActionsBitmask & GripActionMask) != 0u ? 1 : 0);
            return signal;
        }

        private void WriteBlackBoxFrame(float angleDegrees)
        {
            float velocity = _leverVelocities[0];
            float target = _leverTargets[0];
            if (!math.isfinite(angleDegrees) || !math.isfinite(velocity) || !math.isfinite(target))
            {
                DumpBlackBox();
                return;
            }

            ManualOverrideLeverTelemetryEntry entry = default;
            entry.HandLocalPosition = WorldToLocal(_lastHandWorldPosition);
            entry.PivotLocalPosition = _leverPivots[0];
            entry.AngleDegrees = angleDegrees;
            entry.TargetAngleDegrees = target;
            entry.VelocityDegreesPerSecond = velocity;
            entry.Frame = unchecked((uint)Time.frameCount);
            entry.Flags = BuildTelemetryFlags();
            _blackBox[_blackBoxWriteIndex] = entry;
            _blackBoxWriteIndex = (_blackBoxWriteIndex + 1) % BlackBoxFrameCount;
        }

        private byte BuildTelemetryFlags()
        {
            byte flags = 0;
            if (_grabbed)
                flags |= 1 << 0;
            if (_latched)
                flags |= 1 << 1;
            if (_lowTierMath)
                flags |= 1 << 2;
            if (XRSettings.enabled && XRSettings.isDeviceActive)
                flags |= 1 << 3;
            return flags;
        }

        private void DumpBlackBox()
        {
            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string path = Path.Combine(projectRoot, DumpRelativePath);
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(BlackBoxFrameCount);
                    writer.Write(_blackBoxWriteIndex);
                    for (int i = 0; i < BlackBoxFrameCount; i++)
                    {
                        ManualOverrideLeverTelemetryEntry entry = _blackBox[i];
                        writer.Write(entry.HandLocalPosition.x);
                        writer.Write(entry.HandLocalPosition.y);
                        writer.Write(entry.HandLocalPosition.z);
                        writer.Write(entry.PivotLocalPosition.x);
                        writer.Write(entry.PivotLocalPosition.y);
                        writer.Write(entry.PivotLocalPosition.z);
                        writer.Write(entry.AngleDegrees);
                        writer.Write(entry.TargetAngleDegrees);
                        writer.Write(entry.VelocityDegreesPerSecond);
                        writer.Write(entry.Frame);
                        writer.Write(entry.Flags);
                    }
                }
            }
            catch (IOException)
            {
            }
        }

        private void EnsureReferences()
        {
            _cachedTransform = transform;
            if (activationVolume == null)
                activationVolume = GetComponent<BoxCollider>();
            if (leverVisual == null)
                leverVisual = transform;

            _resolvedVisual = leverVisual != null ? leverVisual : transform;
            if (activationVolume != null)
                activationVolume.isTrigger = true;
        }

        private void CacheConfiguration()
        {
            _resolvedLocalAxis = NormalizeOr(localRotationAxis, Vector3.right);
            minAngleDegrees = math.clamp(SanitizeFloat(minAngleDegrees, 0f), -180f, 180f);
            maxAngleDegrees = math.clamp(SanitizeFloat(maxAngleDegrees, 90f), minAngleDegrees + 1f, 180f);
            latchAngleDegrees = math.clamp(SanitizeFloat(latchAngleDegrees, 85f), minAngleDegrees, maxAngleDegrees);
            grabRadiusMeters = math.clamp(SanitizeFloat(grabRadiusMeters, 0.15f), 0.01f, 1f);
            _grabRadiusSq = grabRadiusMeters * grabRadiusMeters;
            springStiffness = math.clamp(SanitizeFloat(springStiffness, 42f), 0f, 240f);
            springDamping = math.clamp(SanitizeFloat(springDamping, 13f), 0f, 80f);
            maxVelocityDegreesPerSecond = math.clamp(SanitizeFloat(maxVelocityDegreesPerSecond, 360f), 1f, 1440f);
            nonVrPullSeconds = math.clamp(SanitizeFloat(nonVrPullSeconds, 1.5f), 0.05f, 10f);
            lowTierIkBlend = math.saturate(SanitizeFloat(lowTierIkBlend, 0.35f));
            highTierIkBlend = math.saturate(SanitizeFloat(highTierIkBlend, 0.85f));
            _referenceLocalVector = ResolveReferenceVector();
            if (_nativeAllocated)
                _leverPivots[0] = ToFloat3(pivotLocalPosition);
        }

        private Vector3 ResolveReferenceVector()
        {
            Vector3 raw = handleAnchor != null ? handleAnchor.localPosition - pivotLocalPosition : Vector3.forward;
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

            _leverAngles = new NativeArray<float>(LeverCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _leverVelocities = new NativeArray<float>(LeverCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _leverTargets = new NativeArray<float>(LeverCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _leverPivots = new NativeArray<float3>(LeverCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _blackBox = new NativeArray<ManualOverrideLeverTelemetryEntry>(BlackBoxFrameCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(_leverAngles, NativeMemoryOwner, nameof(_leverAngles), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_leverVelocities, NativeMemoryOwner, nameof(_leverVelocities), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_leverTargets, NativeMemoryOwner, nameof(_leverTargets), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_leverPivots, NativeMemoryOwner, nameof(_leverPivots), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_blackBox, NativeMemoryOwner, nameof(_blackBox), NativeAllocationLifetime.Scene);
            _leverPivots[0] = ToFloat3(pivotLocalPosition);
            _nativeAllocated = true;
        }

        private void DisposeNativeState()
        {
            if (!_nativeAllocated)
                return;

            UnregisterNativeArray(_leverAngles);
            UnregisterNativeArray(_leverVelocities);
            UnregisterNativeArray(_leverTargets);
            UnregisterNativeArray(_leverPivots);
            UnregisterNativeArray(_blackBox);
            JobHandle disposeHandle = default;
            disposeHandle = _leverAngles.Dispose(disposeHandle);
            disposeHandle = _leverVelocities.Dispose(disposeHandle);
            disposeHandle = _leverTargets.Dispose(disposeHandle);
            disposeHandle = _leverPivots.Dispose(disposeHandle);
            disposeHandle = _blackBox.Dispose(disposeHandle);
            JobHandle.ScheduleBatchedJobs();
            _nativeAllocated = false;
        }

        private static void UnregisterNativeArray<T>(NativeArray<T> array) where T : struct
        {
            if (array.IsCreated)
                NativeMemorySentinel.UnregisterNativeArray(array);
        }

        private void TryRegisterReceiver()
        {
            if (_receiverRegistered || activationVolume == null)
                return;

            PhysicalHandReceiverRegistry.Register(activationVolume, this);
            _receiverRegistered = true;
        }

        private void TryUnregisterReceiver()
        {
            if (!_receiverRegistered)
                return;

            PhysicalHandReceiverRegistry.Unregister(activationVolume, this);
            _receiverRegistered = false;
        }

        private void TryRegisterTick()
        {
            if (_registeredTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);
        }

        private void TryUnregisterTick()
        {
            if (!_registeredTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
            _registeredTick = false;
        }

        private bool ResolveLowTierMath()
        {
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            return tier == HectonQualityTier.Unknown || tier == HectonQualityTier.Low || tier == HectonQualityTier.Mx350;
        }

        private float ResolveNormalized01(float angleDegrees)
        {
            return math.saturate((angleDegrees - minAngleDegrees) * math.rcp(math.max(0.001f, maxAngleDegrees - minAngleDegrees)));
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
            float3 fromPivot = handLocal - pivotLocal;
            float3 projected = fromPivot - axisLocal * math.dot(fromPivot, axisLocal);
            float projectedLengthSq = math.lengthsq(projected);
            if (projectedLengthSq < MinAxisLengthSq)
                return minimum;

            projected *= math.rsqrt(projectedLengthSq);
            float signed = math.atan2(math.dot(axisLocal, math.cross(referenceLocal, projected)), math.dot(referenceLocal, projected)) * DegreesPerRadian;
            return math.clamp(signed, minimum, maximum);
        }

        private static float SanitizeDeltaSeconds(float value)
        {
            float resolved = math.isfinite(value) ? value : DefaultDeltaSeconds;
            return math.clamp(resolved, 0f, MaxDeltaSeconds);
        }

        private static float SanitizeFloat(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        private static Vector3 NormalizeOr(Vector3 value, Vector3 fallback)
        {
            float lengthSq = value.sqrMagnitude;
            if (!math.isfinite(lengthSq) || lengthSq < MinAxisLengthSq)
                return fallback.normalized;

            return value * math.rsqrt(lengthSq);
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return math.all(math.isfinite(new float3(value.x, value.y, value.z)));
        }

        private static bool IsFiniteQuaternion(Quaternion value)
        {
            float4 q = new float4(value.x, value.y, value.z, value.w);
            return math.all(math.isfinite(q)) && math.lengthsq(q) > MinAxisLengthSq;
        }

        private static byte ResolveSignalHandSide(PhysicalHandSide side)
        {
            return side == PhysicalHandSide.Left
                ? ManualOverridePulledSignal.HandLeft
                : ManualOverridePulledSignal.HandRight;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static bool VerifyProjectionMath()
        {
            float3 axis = new float3(1f, 0f, 0f);
            float3 reference = new float3(0f, 0f, 1f);
            float zero = SolveAngleFromHand(reference, float3.zero, axis, reference, 0f, 90f);
            float rightAngle = SolveAngleFromHand(new float3(0f, -1f, 0f), float3.zero, axis, reference, 0f, 90f);
            return math.abs(zero) <= 0.01f && math.abs(rightAngle - 90f) <= 0.01f;
        }
#endif

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureReferences();
            CacheConfiguration();
        }
#endif

        [StructLayout(LayoutKind.Sequential)]
        private struct ManualOverrideLeverTelemetryEntry
        {
            public float3 HandLocalPosition;
            public float3 PivotLocalPosition;
            public float AngleDegrees;
            public float TargetAngleDegrees;
            public float VelocityDegreesPerSecond;
            public uint Frame;
            public byte Flags;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct LeverAngularSolveJob : IJob
        {
            public float3 HandLocal;
            public float3 AxisLocal;
            public float3 ReferenceLocal;
            public float MinAngleDegrees;
            public float MaxAngleDegrees;

            [ReadOnly] public NativeArray<float3> LeverPivots;
            public NativeArray<float> LeverTargets;

            public void Execute()
            {
                LeverTargets[0] = SolveAngleFromHand(
                    HandLocal,
                    LeverPivots[0],
                    AxisLocal,
                    ReferenceLocal,
                    MinAngleDegrees,
                    MaxAngleDegrees);
            }
        }
    }
}

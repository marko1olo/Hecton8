using System;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Interaction
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Interaction/VR Leak Patch Weld Target")]
    public sealed class VRLeakPatchWeldTarget : MonoBehaviour, IInteractionSignalConsumer, IUpdatable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const float DefaultInteractionStepSeconds = 0.02f;
        private const float MaximumPatchContactRadiusMeters = 3f;
        private const float MaximumRequiredPatchHoldSeconds = 10f;
        private const float MaximumRequiredWeldSeconds = 15f;
        private const float MaximumRepairAmountPerSecond = 100f;
        private const float MaximumPatchHoldDecayPerSecond = 10f;
        private const float MaximumAcousticGuideRadiusMeters = 5000f;
        private const uint LeakPatchMaintenanceSourceHash = 0x4c505743u; // LPWC
        private const byte MaximumMissedPatchContactTicks = 3;

        [Header("Leak")]
        [SerializeField] private BaseModule targetModule;
        [SerializeField] private Transform leakAnchor;
        [SerializeField, Min(0.01f)] private float patchContactRadiusMeters = 0.32f;
        [SerializeField, Range(-1f, 1f)] private float patchFlushDotThreshold = 0.9659258f;

        [Header("Weld")]
        [SerializeField, Min(0.05f)] private float requiredPatchHoldSeconds = 1.5f;
        [SerializeField, Min(0.05f)] private float requiredWeldSeconds = 2.0f;
        [SerializeField, Min(0f)] private float repairAmountPerSecond = 18f;
        [SerializeField, Min(0f)] private float patchHoldDecayPerSecond = 0.65f;
        [SerializeField, Range(0, 63)] private int vesselMaintenancePanelBitIndex = 3;

        private Vector3 _lastPatchContactPoint;
        private Vector3 _lastPatchContactNormal;
        private Vector3 _lastAcousticGuideDirection;
        private float _lastPatchFlushDot;
        private float _patchHoldSeconds;
        private float _weldSeconds;
        private Transform _cachedTransform;
        private Transform _targetModuleTransform;
        private SubmarineCoreDirector _submarineCore;
        private bool _patchInContact;
        private bool _patchFlushAligned;
        private bool _sealed;
        private bool _hasAcousticGuide;
        private bool _registeredPatchHoldDecayTick;
        private bool _registeredPhysicsPayloadReader;
        private bool _registeredHotSwap;
        private int _lastPhysicsEventSnapshotGeneration;
        private byte _missedPatchContactTicks;

        public bool PatchInContact => _patchInContact;
        public bool IsSealed => _sealed;
        public bool PatchFlushAligned => _patchFlushAligned;
        public float LastPatchFlushDot => _lastPatchFlushDot;
        public float PatchHoldProgress01 => math.saturate(_patchHoldSeconds / ResolveSafeRequiredPatchHoldSeconds());
        public float WeldProgress01 => math.saturate(_weldSeconds / ResolveSafeRequiredWeldSeconds());
        public bool HasAcousticGuide => _hasAcousticGuide;
        public Vector3 LastAcousticGuideDirection => _lastAcousticGuideDirection;

        private void Awake()
        {
            RefreshCachedTransforms();
        }

        private void OnEnable()
        {
            RefreshCachedTransforms();
            RefreshColdRegistryReferences();
            TryRegisterHotSwapListener();

            InteractableRegistry.RegisterTree(this);
            TryRegisterPhysicsPayloadReader();
        }

        private void OnDisable()
        {
            InteractableRegistry.InvalidateTree(this);
            TryUnregisterPhysicsPayloadReader();
            TryUnregisterHotSwapListener();
            ClearPatchContactImmediate();
            ClearAcousticGuide();
            _submarineCore = null;
            _patchHoldSeconds = 0f;
            _missedPatchContactTicks = 0;
            TryUnregisterPatchHoldDecayTick();
        }

        private void OnDestroy()
        {
            InteractableRegistry.InvalidateTree(this);
            TryUnregisterPhysicsPayloadReader();
            TryUnregisterHotSwapListener();
            TryUnregisterPatchHoldDecayTick();
        }

        public void LateFrameTick()
        {
            DrainPhysicsEventPayloads();
        }

        public bool SetPatchContact(Vector3 contactPoint, Vector3 contactNormal, float deltaSeconds)
        {
            float safeDeltaSeconds = ResolveSafeDeltaSeconds(deltaSeconds);
            if (_sealed || safeDeltaSeconds <= 0f || !IsFiniteVector(contactPoint))
            {
                _patchInContact = false;
                _patchFlushAligned = false;
                _missedPatchContactTicks = MaximumMissedPatchContactTicks;
                DecayPatchHold(safeDeltaSeconds);
                RefreshPatchHoldDecayRegistration();
                return false;
            }

            if (!IsPointWithinPatchRadius(contactPoint))
            {
                _patchInContact = false;
                _patchFlushAligned = false;
                _lastPatchFlushDot = 0f;
                _missedPatchContactTicks = MaximumMissedPatchContactTicks;
                DecayPatchHold(safeDeltaSeconds);
                RefreshPatchHoldDecayRegistration();
                return false;
            }

            _patchInContact = true;
            _missedPatchContactTicks = 0;
            _lastPatchContactPoint = contactPoint;
            _lastPatchContactNormal = SafeNormalize(contactNormal, Vector3.forward);
            _lastPatchFlushDot = ResolvePatchFlushDot(_lastPatchContactNormal);
            _patchFlushAligned = _lastPatchFlushDot >= ResolveSafePatchFlushDotThreshold();
            if (_patchFlushAligned)
                _patchHoldSeconds = math.min(ResolveSafeRequiredPatchHoldSeconds(), _patchHoldSeconds + safeDeltaSeconds);
            else
                DecayPatchHold(safeDeltaSeconds);

            RefreshPatchHoldDecayRegistration();
            return _patchFlushAligned;
        }

        public void ClearPatchContact()
        {
            ClearPatchContactImmediate();
            RefreshPatchHoldDecayRegistration();
        }

        public void Tick(float deltaTime)
        {
            if (_sealed)
            {
                RefreshPatchHoldDecayRegistration();
                return;
            }

            if (_patchInContact)
            {
                if (_missedPatchContactTicks < MaximumMissedPatchContactTicks)
                {
                    _missedPatchContactTicks++;
                    return;
                }

                ClearPatchContactImmediate();
            }

            DecayPatchHold(deltaTime);
            RefreshPatchHoldDecayRegistration();
        }

        private void ClearPatchContactImmediate()
        {
            _patchInContact = false;
            _patchFlushAligned = false;
            _lastPatchFlushDot = 0f;
            _missedPatchContactTicks = 0;
        }

        public void ApplyInteractionSignal(in InteractionSignal signal, Vector3 runtimeHitPoint)
        {
            if (signal.PowerDelivered <= 0f)
                return;

            byte effect = signal.EffectType;
            if (effect != (byte)InteractionEffectType.Weld &&
                effect != (byte)InteractionEffectType.Torch &&
                effect != (byte)InteractionEffectType.PlasmaCut)
            {
                return;
            }

            ApplyWeldAtPoint(runtimeHitPoint, signal.PowerDelivered, DefaultInteractionStepSeconds);
        }

        public bool ApplyWeldAtPoint(Vector3 runtimeHitPoint, float deliveredPower, float deltaSeconds)
        {
            float safeDeltaSeconds = ResolveSafeDeltaSeconds(deltaSeconds);
            float safeDeliveredPower = math.isfinite(deliveredPower) ? math.max(0f, deliveredPower) : 0f;
            if (_sealed || safeDeliveredPower <= 0f || safeDeltaSeconds <= 0f || !_patchInContact || !_patchFlushAligned || PatchHoldProgress01 < 1f)
                return false;

            if (!IsFiniteVector(runtimeHitPoint) || !IsPointWithinPatchRadius(runtimeHitPoint))
                return false;

            float safeRequiredWeldSeconds = ResolveSafeRequiredWeldSeconds();
            _weldSeconds = math.min(safeRequiredWeldSeconds, _weldSeconds + safeDeltaSeconds * safeDeliveredPower);
            if (targetModule != null)
                targetModule.Repair(ResolveSafeRepairAmountPerSecond() * safeDeltaSeconds * safeDeliveredPower);

            if (_weldSeconds >= safeRequiredWeldSeconds)
            {
                _sealed = true;
                _patchHoldSeconds = 0f;
                ClearAcousticGuide();
                TryRecordVesselMaintenanceSeal();
                TryUnregisterPatchHoldDecayTick();
            }

            return true;
        }

        private void DrainPhysicsEventPayloads()
        {
            if (!_registeredPhysicsPayloadReader)
                return;

            int snapshotGeneration = SignalBus<PhysicsEventPayload>.SnapshotGeneration;
            if (snapshotGeneration == _lastPhysicsEventSnapshotGeneration)
                return;

            _lastPhysicsEventSnapshotGeneration = snapshotGeneration;
            ReadOnlySpan<PhysicsEventPayload> signals = SignalBus<PhysicsEventPayload>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PhysicsEventPayload payload = signals[i];
                if (payload.EventType == (ushort)PhysicsEventType.AcousticImpulse)
                    HandleAcousticImpulsePayload(in payload);
            }
        }

        private void HandleAcousticImpulsePayload(in PhysicsEventPayload impulseEvent)
        {
            float safeRadiusMeters = ResolveSafeAcousticGuideRadiusMeters(impulseEvent.RadiusMeters);
            if (safeRadiusMeters <= 0f || !IsFiniteVector(impulseEvent.RuntimePosition))
                return;

            if (!TryResolveLeakRuntimePosition(out Vector3 leakPosition))
                return;

            double radiusSq = (double)safeRadiusMeters * safeRadiusMeters;
            double3 runtimeDelta = new double3(
                (double)leakPosition.x - impulseEvent.RuntimePosition.x,
                (double)leakPosition.y - impulseEvent.RuntimePosition.y,
                (double)leakPosition.z - impulseEvent.RuntimePosition.z);
            double lengthSq = math.lengthsq(runtimeDelta);
            if (lengthSq <= 0.000001d || lengthSq > radiusSq || !math.all(math.isfinite(runtimeDelta)))
                return;

            float3 toLeak = new float3((float)runtimeDelta.x, (float)runtimeDelta.y, (float)runtimeDelta.z);

            float directionLengthSq = math.lengthsq(toLeak);
            if (directionLengthSq <= 0.000001f || !math.all(math.isfinite(toLeak)))
                return;

            toLeak *= ApproximateInverseMagnitudeNoSqrt(toLeak);
            _lastAcousticGuideDirection = new Vector3(toLeak.x, toLeak.y, toLeak.z);
            _hasAcousticGuide = true;
        }

        private void TryRegisterPhysicsPayloadReader()
        {
            if (_registeredPhysicsPayloadReader || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredPhysicsPayloadReader = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterPhysicsPayloadReader()
        {
            if (!_registeredPhysicsPayloadReader)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _registeredPhysicsPayloadReader = false;
            _lastPhysicsEventSnapshotGeneration = 0;
        }

        private void DecayPatchHold(float deltaSeconds)
        {
            float safeDeltaSeconds = ResolveSafeDeltaSeconds(deltaSeconds);
            float decay = ResolveSafePatchHoldDecayPerSecond() * safeDeltaSeconds;
            _patchHoldSeconds = math.max(0f, _patchHoldSeconds - decay);
        }

        private void RefreshPatchHoldDecayRegistration()
        {
            if (_sealed || (!_patchInContact && _patchHoldSeconds <= 0f))
                TryUnregisterPatchHoldDecayTick();
            else
                TryRegisterPatchHoldDecayTick();
        }

        private void TryRegisterPatchHoldDecayTick()
        {
            if (_registeredPatchHoldDecayTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredPatchHoldDecayTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);
        }

        private void TryUnregisterPatchHoldDecayTick()
        {
            if (!_registeredPatchHoldDecayTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
            _registeredPatchHoldDecayTick = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Submarine)
                _submarineCore = currentService as SubmarineCoreDirector;

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                _registeredPhysicsPayloadReader = false;
                _registeredPatchHoldDecayTick = false;
                if (currentService != null && isActiveAndEnabled)
                {
                    TryRegisterPhysicsPayloadReader();
                    RefreshPatchHoldDecayRegistration();
                }
            }
        }

        private void RefreshColdRegistryReferences()
        {
            _submarineCore = GlobalRegistry.Submarine as SubmarineCoreDirector;
        }

        private void TryRecordVesselMaintenanceSeal()
        {
            SubmarineCoreDirector core = _submarineCore;
            if (core == null)
            {
                RefreshColdRegistryReferences();
                core = _submarineCore;
            }

            if (core == null)
                return;

            uint panelBit = (uint)math.clamp(vesselMaintenancePanelBitIndex, 0, 63);
            core.TryRecordVesselMaintenanceAction(panelBit, LeakPatchMaintenanceSourceHash);
        }

        private void ClearAcousticGuide()
        {
            _hasAcousticGuide = false;
            _lastAcousticGuideDirection = Vector3.zero;
        }

        private void RefreshCachedTransforms()
        {
            _cachedTransform = transform;
            _targetModuleTransform = targetModule != null ? targetModule.transform : null;
        }

        private bool IsPointWithinPatchRadius(Vector3 runtimePoint)
        {
            if (!IsFiniteVector(runtimePoint))
                return false;

            float safeRadius = ResolveSafePatchContactRadiusMeters();
            double radiusSq = (double)safeRadius * safeRadius;
            if (!TryResolveLeakRuntimePosition(out Vector3 leakPosition))
                return false;

            return RuntimeDistanceSq(runtimePoint, leakPosition) <= radiusSq;
        }

        private bool TryResolveLeakRuntimePosition(out Vector3 leakPosition)
        {
            if (leakAnchor != null && IsFiniteVector(leakAnchor.position))
            {
                leakPosition = leakAnchor.position;
                return true;
            }

            if (_targetModuleTransform == null && targetModule != null)
                _targetModuleTransform = targetModule.transform;

            if (_targetModuleTransform != null && IsFiniteVector(_targetModuleTransform.position))
            {
                leakPosition = _targetModuleTransform.position;
                return true;
            }

            if (_cachedTransform != null && IsFiniteVector(_cachedTransform.position))
            {
                leakPosition = _cachedTransform.position;
                return true;
            }

            leakPosition = default;
            return false;
        }

        private static double RuntimeDistanceSq(Vector3 a, Vector3 b)
        {
            double dx = (double)a.x - b.x;
            double dy = (double)a.y - b.y;
            double dz = (double)a.z - b.z;
            return (dx * dx) + (dy * dy) + (dz * dz);
        }

        private float ResolvePatchFlushDot(Vector3 patchNormal)
        {
            Vector3 hullNormal = ResolveHullNormal();
            return math.dot(
                new float3(patchNormal.x, patchNormal.y, patchNormal.z),
                new float3(hullNormal.x, hullNormal.y, hullNormal.z));
        }

        private Vector3 ResolveHullNormal()
        {
            Transform normalSource = leakAnchor != null ? leakAnchor : _cachedTransform;
            return SafeNormalize(normalSource != null ? normalSource.forward : Vector3.forward, Vector3.forward);
        }

        private static Vector3 SafeNormalize(Vector3 value, Vector3 fallback)
        {
            float lengthSq = value.sqrMagnitude;
            if (lengthSq <= 0.000001f || !math.all(math.isfinite(new float3(value.x, value.y, value.z))))
                return fallback;

            return value * ApproximateInverseMagnitudeNoSqrt(value);
        }

        private static float ApproximateInverseMagnitudeNoSqrt(Vector3 value)
        {
            return ApproximateInverseMagnitudeNoSqrt(new float3(value.x, value.y, value.z));
        }

        private static float ApproximateInverseMagnitudeNoSqrt(float3 value)
        {
            float3 absValue = math.abs(value);
            float largest = math.cmax(absValue);
            float smallest = math.cmin(absValue);
            float middle = absValue.x + absValue.y + absValue.z - largest - smallest;
            float magnitude = largest + (middle * 0.375f) + (smallest * 0.125f);
            return math.rcp(math.max(magnitude, 0.000001f));
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return math.all(math.isfinite(new float3(value.x, value.y, value.z)));
        }

        private static float ResolveSafeDeltaSeconds(float value)
        {
            return math.isfinite(value) ? math.clamp(value, 0f, 0.05f) : 0f;
        }

        private float ResolveSafePatchContactRadiusMeters()
        {
            return math.isfinite(patchContactRadiusMeters)
                ? math.clamp(patchContactRadiusMeters, 0.01f, MaximumPatchContactRadiusMeters)
                : 0.32f;
        }

        private float ResolveSafePatchFlushDotThreshold()
        {
            return math.isfinite(patchFlushDotThreshold) ? math.clamp(patchFlushDotThreshold, -1f, 1f) : 0.9659258f;
        }

        private float ResolveSafeRequiredPatchHoldSeconds()
        {
            return math.isfinite(requiredPatchHoldSeconds)
                ? math.clamp(requiredPatchHoldSeconds, 0.001f, MaximumRequiredPatchHoldSeconds)
                : 1.5f;
        }

        private float ResolveSafeRequiredWeldSeconds()
        {
            return math.isfinite(requiredWeldSeconds)
                ? math.clamp(requiredWeldSeconds, 0.001f, MaximumRequiredWeldSeconds)
                : 2f;
        }

        private float ResolveSafeRepairAmountPerSecond()
        {
            return math.isfinite(repairAmountPerSecond)
                ? math.clamp(repairAmountPerSecond, 0f, MaximumRepairAmountPerSecond)
                : 18f;
        }

        private float ResolveSafePatchHoldDecayPerSecond()
        {
            return math.isfinite(patchHoldDecayPerSecond)
                ? math.clamp(patchHoldDecayPerSecond, 0f, MaximumPatchHoldDecayPerSecond)
                : 0.65f;
        }

        private static float ResolveSafeAcousticGuideRadiusMeters(float value)
        {
            return math.isfinite(value)
                ? math.clamp(value, 0f, MaximumAcousticGuideRadiusMeters)
                : 0f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!math.isfinite(patchContactRadiusMeters) || patchContactRadiusMeters < 0.01f)
                patchContactRadiusMeters = 0.01f;
            patchContactRadiusMeters = math.min(patchContactRadiusMeters, MaximumPatchContactRadiusMeters);
            patchFlushDotThreshold = math.isfinite(patchFlushDotThreshold) ? math.clamp(patchFlushDotThreshold, -1f, 1f) : 0.9659258f;
            if (!math.isfinite(requiredPatchHoldSeconds) || requiredPatchHoldSeconds < 0.05f)
                requiredPatchHoldSeconds = 0.05f;
            requiredPatchHoldSeconds = math.min(requiredPatchHoldSeconds, MaximumRequiredPatchHoldSeconds);
            if (!math.isfinite(requiredWeldSeconds) || requiredWeldSeconds < 0.05f)
                requiredWeldSeconds = 0.05f;
            requiredWeldSeconds = math.min(requiredWeldSeconds, MaximumRequiredWeldSeconds);
            if (!math.isfinite(repairAmountPerSecond) || repairAmountPerSecond < 0f)
                repairAmountPerSecond = 0f;
            repairAmountPerSecond = math.min(repairAmountPerSecond, MaximumRepairAmountPerSecond);
            if (!math.isfinite(patchHoldDecayPerSecond) || patchHoldDecayPerSecond < 0f)
                patchHoldDecayPerSecond = 0f;
            patchHoldDecayPerSecond = math.min(patchHoldDecayPerSecond, MaximumPatchHoldDecayPerSecond);
            vesselMaintenancePanelBitIndex = math.clamp(vesselMaintenancePanelBitIndex, 0, 63);
        }
#endif
    }
}

using Hecton8.Gameplay;
using Hecton8.Physics;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Interaction
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Interaction/VR Leak Patch Weld Target")]
    public sealed class VRLeakPatchWeldTarget : MonoBehaviour, IInteractionSignalConsumer, IPhysicsAcousticImpulseEventListener
    {
        private const float DefaultInteractionStepSeconds = 0.02f;

        [Header("Leak")]
        [SerializeField] private BaseModule targetModule;
        [SerializeField] private Transform leakAnchor;
        [SerializeField, Min(0.01f)] private float patchContactRadiusMeters = 0.32f;
        [SerializeField, Range(-1f, 1f)] private float patchFlushDotThreshold = 0.9659258f;

        [Header("Weld")]
        [SerializeField, Min(0.05f)] private float requiredPatchHoldSeconds = 1.5f;
        [SerializeField, Min(0.05f)] private float requiredWeldSeconds = 2.0f;
        [SerializeField, Min(0f)] private float repairAmountPerSecond = 18f;

        private Vector3 _lastPatchContactPoint;
        private Vector3 _lastPatchContactNormal;
        private Vector3 _lastAcousticGuideDirection;
        private float _lastPatchFlushDot;
        private float _patchHoldSeconds;
        private float _weldSeconds;
        private bool _patchInContact;
        private bool _patchFlushAligned;
        private bool _sealed;
        private bool _hasAcousticGuide;

        public bool PatchInContact => _patchInContact;
        public bool IsSealed => _sealed;
        public bool PatchFlushAligned => _patchFlushAligned;
        public float LastPatchFlushDot => _lastPatchFlushDot;
        public float PatchHoldProgress01 => math.saturate(_patchHoldSeconds / math.max(0.001f, requiredPatchHoldSeconds));
        public float WeldProgress01 => math.saturate(_weldSeconds / math.max(0.001f, requiredWeldSeconds));
        public bool HasAcousticGuide => _hasAcousticGuide;
        public Vector3 LastAcousticGuideDirection => _lastAcousticGuideDirection;

        private void OnEnable()
        {
            PhysicsEventBus.Register(this);
        }

        private void OnDisable()
        {
            PhysicsEventBus.Unregister(this);
        }

        public bool SetPatchContact(Vector3 contactPoint, Vector3 contactNormal, float deltaSeconds)
        {
            if (_sealed || deltaSeconds <= 0f)
                return false;

            Vector3 anchor = ResolveLeakPosition();
            Vector3 delta = contactPoint - anchor;
            if (delta.sqrMagnitude > patchContactRadiusMeters * patchContactRadiusMeters)
            {
                _patchInContact = false;
                _patchFlushAligned = false;
                _lastPatchFlushDot = 0f;
                return false;
            }

            _patchInContact = true;
            _lastPatchContactPoint = contactPoint;
            _lastPatchContactNormal = SafeNormalize(contactNormal, Vector3.forward);
            _lastPatchFlushDot = ResolvePatchFlushDot(_lastPatchContactNormal);
            _patchFlushAligned = _lastPatchFlushDot >= patchFlushDotThreshold;
            if (_patchFlushAligned)
                _patchHoldSeconds = math.min(requiredPatchHoldSeconds, _patchHoldSeconds + deltaSeconds);

            return _patchFlushAligned;
        }

        public void ClearPatchContact()
        {
            _patchInContact = false;
            _patchFlushAligned = false;
            _lastPatchFlushDot = 0f;
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
            if (_sealed || deliveredPower <= 0f || deltaSeconds <= 0f || !_patchInContact || !_patchFlushAligned || PatchHoldProgress01 < 1f)
                return false;

            Vector3 delta = runtimeHitPoint - ResolveLeakPosition();
            if (delta.sqrMagnitude > patchContactRadiusMeters * patchContactRadiusMeters)
                return false;

            _weldSeconds = math.min(requiredWeldSeconds, _weldSeconds + deltaSeconds * deliveredPower);
            if (targetModule != null)
                targetModule.Repair(repairAmountPerSecond * deltaSeconds * deliveredPower);

            if (_weldSeconds >= requiredWeldSeconds)
                _sealed = true;

            return true;
        }

        public void OnAcousticImpulse(in AcousticImpulseEvent impulseEvent)
        {
            Vector3 leakPosition = ResolveLeakPosition();
            float radiusSq = impulseEvent.RadiusMeters * impulseEvent.RadiusMeters;
            float3 toLeak;
            if (impulseEvent.RadiusMeters > 50f)
            {
                AbsoluteUniversePosition leakAup = AbsoluteUniversePosition.FromRuntimePosition(leakPosition);
                AbsoluteUniversePosition impulseAup = AbsoluteUniversePosition.FromRuntimePosition(impulseEvent.RuntimePosition);
                double distanceSq = AbsoluteUniversePosition.DistanceSq(in leakAup, in impulseAup);
                if (distanceSq <= 0.000001d || distanceSq > radiusSq)
                    return;

                toLeak = AbsoluteUniversePosition.ToCameraRelativeFloat3(in leakAup, in impulseAup);
            }
            else
            {
                Vector3 runtimeDelta = leakPosition - impulseEvent.RuntimePosition;
                float lengthSq = runtimeDelta.sqrMagnitude;
                if (lengthSq <= 0.000001f || lengthSq > radiusSq)
                    return;

                toLeak = new float3(runtimeDelta.x, runtimeDelta.y, runtimeDelta.z);
            }

            float directionLengthSq = math.lengthsq(toLeak);
            if (directionLengthSq <= 0.000001f || !math.all(math.isfinite(toLeak)))
                return;

            toLeak *= math.rsqrt(directionLengthSq);
            _lastAcousticGuideDirection = new Vector3(toLeak.x, toLeak.y, toLeak.z);
            _hasAcousticGuide = true;
        }

        private Vector3 ResolveLeakPosition()
        {
            if (leakAnchor != null)
                return leakAnchor.position;

            return targetModule != null ? targetModule.transform.position : transform.position;
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
            Transform normalSource = leakAnchor != null ? leakAnchor : transform;
            return SafeNormalize(normalSource.forward, Vector3.forward);
        }

        private static Vector3 SafeNormalize(Vector3 value, Vector3 fallback)
        {
            float lengthSq = value.sqrMagnitude;
            if (lengthSq <= 0.000001f || !math.all(math.isfinite(new float3(value.x, value.y, value.z))))
                return fallback;

            return value * math.rsqrt(lengthSq);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            patchFlushDotThreshold = math.clamp(patchFlushDotThreshold, -1f, 1f);
        }
#endif
    }
}

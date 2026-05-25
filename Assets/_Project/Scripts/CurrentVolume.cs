// ============================================================================
// HECTON-8 — CurrentVolume.cs
// Local authored current field. Cheap additive influence on top of the global
// phantom current. Used by player, buoyancy, and ambient motion.
// ============================================================================

using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Physics
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Physics/Current Volume")]
    public sealed class CurrentVolume : MonoBehaviour
    {
        private const float SharedAmbientPatternScale = 0.0135f;
        private const float SharedAmbientTimeScale = 0.11f;
        private const float SharedAmbientStrength = 0.9f;
        private const float SharedAmbientVerticalFactor = 0.08f;
        private const int ActiveVolumeCapacity = 32;
        private const float CurrentSampleClockMaxSeconds = 16777215f;
        private const float LargeVolumeAupCullThresholdMeters = 50f;
        private const float LargeVolumeAupCullThresholdSq = LargeVolumeAupCullThresholdMeters * LargeVolumeAupCullThresholdMeters;
        private const float TwoPi = 6.28318530718f;

        public enum VolumeShape
        {
            Box = 0,
            Sphere = 1
        }

        public enum FlowPattern
        {
            Directional = 0,
            RadialInward = 1,
            RadialOutward = 2,
            VortexClockwise = 3,
            VortexCounterClockwise = 4,
            Updraft = 5,
            Downdraft = 6
        }

        private static readonly List<CurrentVolume> ActiveVolumes =
            new List<CurrentVolume>(ActiveVolumeCapacity); // COLD ALLOC: List<CurrentVolume>[32] — active authored-current registry — owner: CurrentVolume
        private static readonly HashSet<CurrentVolume> ActiveVolumesSet =
            new HashSet<CurrentVolume>(ActiveVolumeCapacity); // COLD ALLOC: HashSet<CurrentVolume>[32] — duplicate guard for authored-current registry — owner: CurrentVolume
        private static int _sharedSampleTimeFrame = -1;
        private static float _sharedSampleTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ActiveVolumes.Clear();
            ActiveVolumesSet.Clear();
            _sharedSampleTimeFrame = -1;
            _sharedSampleTime = 0f;
        }

        [Header("Flow")]
        [SerializeField] private VolumeShape shape = VolumeShape.Box;
        [SerializeField] private FlowPattern flowPattern = FlowPattern.Directional;
        [SerializeField] private Vector3 localDirection = Vector3.forward;
        [SerializeField] private float strength = 1.5f;
        [SerializeField, Range(-1f, 1f)] private float verticalFactor = 0.1f;
        [SerializeField, Range(0.01f, 1f)] private float edgeSoftness = 0.2f;
        [SerializeField, Range(0f, 1f)] private float pulseAmplitude = 0.15f;
        [SerializeField] private float pulseFrequency = 0.18f;
        [SerializeField] private float phaseOffset = 0f;
        [SerializeField] private float turbulenceStrength = 0f;
        [SerializeField] private float turbulenceScale = 0.08f;
        [SerializeField] private float turbulenceTimeScale = 0.16f;
        [SerializeField, Range(-1f, 1f)] private float vortexRadialPull = 0.25f;

        public VolumeShape Shape => shape;
        public FlowPattern Pattern => flowPattern;
        public Vector3 LocalDirection => localDirection;
        public float Strength => strength;
        public float VerticalFactor => verticalFactor;
        public float EdgeSoftness => edgeSoftness;
        public float PulseAmplitude => pulseAmplitude;
        public float PulseFrequency => pulseFrequency;
        public float PhaseOffset => phaseOffset;
        public float TurbulenceStrength => turbulenceStrength;
        public float TurbulenceScale => turbulenceScale;
        public float TurbulenceTimeScale => turbulenceTimeScale;
        public float VortexRadialPull => vortexRadialPull;

        [Header("Bounds")]
        [SerializeField] private Vector3 boxSize = new Vector3(10f, 6f, 10f);
        [SerializeField] private float sphereRadius = 8f;

        public Vector3 BoxSize => boxSize;
        public float SphereRadius => sphereRadius;

        private int _sampleCacheFrame = -1;
        private Vector3 _cachedPosition;
        private Vector3 _cachedUp = Vector3.up;
        private Vector3 _cachedForward = Vector3.forward;
        private Vector3 _cachedDirectionalFlow = Vector3.forward;
        private Matrix4x4 _cachedWorldToLocalMatrix = Matrix4x4.identity;
        private AbsoluteUniversePosition _cachedAup;
        private bool _cachedAupValid;
        private float _cachedSampleTime;
        private float _cachedInfluenceRadiusSq = 64f;
        private uint _sampleCacheShiftSequence;

        public static int ActiveCount => ActiveVolumes.Count;

        /// <summary>
        /// Returns an active current volume by dense registry index.
        /// </summary>
        public static CurrentVolume GetActiveVolumeAt(int index)
        {
            return ActiveVolumes[index];
        }

        public static Vector3 SampleAt(Vector3 worldPos)
        {
            if (HectonFloatingOrigin.IsShiftInProgress)
                return Vector3.zero;

            int count = ActiveVolumes.Count;
            if (count == 0)
                return Vector3.zero;

            Vector3 total = Vector3.zero;
            AbsoluteUniversePosition sampleAup = default;
            bool sampleAupValid = false;
            for (int i = 0; i < count; i++)
            {
                CurrentVolume volume = ActiveVolumes[i];
                if (volume == null || !volume.isActiveAndEnabled)
                    continue;
                if (!volume.MayAffectRuntimePoint(worldPos, ref sampleAup, ref sampleAupValid))
                    continue;

                total += volume.SampleInternal(worldPos);
            }

            return total;
        }

        internal static Vector3 SampleCombinedCurrent(Vector3 worldPos)
        {
            if (HectonFloatingOrigin.IsShiftInProgress)
                return Vector3.zero;

            float sampleTime = ResolveFrameSampleTime();
            Unity.Mathematics.float3 ambient = CurrentManager.SampleCurrent(
                new Unity.Mathematics.float3(worldPos.x, worldPos.y, worldPos.z),
                sampleTime,
                SharedAmbientPatternScale,
                SharedAmbientTimeScale,
                SharedAmbientStrength,
                SharedAmbientVerticalFactor);
            Vector3 ambientCurrent = new Vector3(ambient.x, ambient.y, ambient.z);
            return ambientCurrent + SampleAt(worldPos);
        }

        /// <summary>
        /// Samples this volume only at the specified world position.
        /// </summary>
        /// <param name="worldPos">World-space position to evaluate.</param>
        public Vector3 Sample(Vector3 worldPos)
        {
            if (HectonFloatingOrigin.IsShiftInProgress)
                return Vector3.zero;

            AbsoluteUniversePosition sampleAup = default;
            bool sampleAupValid = false;
            if (!MayAffectRuntimePoint(worldPos, ref sampleAup, ref sampleAupValid))
                return Vector3.zero;

            return SampleInternal(worldPos);
        }

        internal void ApplySemanticFlowPreset(
            FlowPattern targetPattern,
            Vector3 targetLocalDirection,
            float targetStrength,
            float targetVerticalFactor,
            float targetVortexRadialPull)
        {
            flowPattern = targetPattern;
            localDirection = targetLocalDirection.sqrMagnitude > 0.0001f
                ? DominantAxisOrDefault(targetLocalDirection, Vector3.forward)
                : Vector3.forward;
            strength = math.max(0f, targetStrength);
            verticalFactor = math.clamp(targetVerticalFactor, -1f, 1f);
            vortexRadialPull = math.clamp(targetVortexRadialPull, -1f, 1f);
            _sampleCacheFrame = -1;
        }

        internal void ApplySemanticBoundsPreset(
            VolumeShape targetShape,
            Vector3 targetBoxSize,
            float targetSphereRadius)
        {
            shape = targetShape;
            boxSize = new Vector3(
                math.max(0.01f, targetBoxSize.x),
                math.max(0.01f, targetBoxSize.y),
                math.max(0.01f, targetBoxSize.z));
            sphereRadius = math.max(0.01f, targetSphereRadius);
            _sampleCacheFrame = -1;
        }

        internal float GetApproximateInfluenceRadius()
        {
            if (shape == VolumeShape.Sphere)
                return math.max(0.01f, sphereRadius);

            Vector3 halfExtents = boxSize * 0.5f;
            return math.max(0.01f, ResolveL1MagnitudeUpperBound(halfExtents));
        }

        private void OnEnable()
        {
            if (ActiveVolumesSet.Add(this))   // O(1)
                ActiveVolumes.Add(this);
        }

        private void OnDisable()
        {
            if (ActiveVolumesSet.Remove(this))
                ActiveVolumes.Remove(this);
        }

        private Vector3 SampleInternal(Vector3 worldPos)
        {
            RefreshSampleCache();

            float weight = shape == VolumeShape.Box
                ? ComputeBoxWeight(worldPos)
                : ComputeSphereWeight(worldPos);

            if (weight <= 0.0001f)
                return Vector3.zero;

            Vector3 dir = ComputeFlowDirection(worldPos);
            if (dir.sqrMagnitude <= 0.0001f)
                return Vector3.zero;

            dir = DominantAxisOrDefault(dir, Vector3.zero);

            float pulse = 1f;
            if (pulseAmplitude > 0.0001f && pulseFrequency > 0.0001f)
            {
                float phase = (_cachedSampleTime * pulseFrequency + phaseOffset) * TwoPi;
                pulse += FastTriangleSigned(phase) * pulseAmplitude;
            }

            Vector3 result = dir * (strength * pulse * weight);

            if (turbulenceStrength > 0.0001f && turbulenceScale > 0.0001f)
            {
                var turbulenceSample = CurrentManager.SampleCurrent(
                    new Unity.Mathematics.float3(worldPos.x, worldPos.y, worldPos.z),
                    _cachedSampleTime + phaseOffset,
                    turbulenceScale,
                    turbulenceTimeScale,
                    turbulenceStrength * weight,
                    verticalFactor * 0.5f);

                result += new Vector3(turbulenceSample.x, turbulenceSample.y, turbulenceSample.z);
            }

            return result;
        }

        private bool MayAffectRuntimePoint(
            Vector3 worldPos,
            ref AbsoluteUniversePosition sampleAup,
            ref bool sampleAupValid)
        {
            RefreshSampleCache();
            if (_cachedInfluenceRadiusSq > LargeVolumeAupCullThresholdSq)
            {
                if (!_cachedAupValid)
                    return false;

                if (!sampleAupValid)
                {
                    if (!TryResolveAupFromRuntimeOrigin(worldPos, out sampleAup))
                        return false;

                    sampleAupValid = true;
                }

                return AbsoluteUniversePosition.DistanceSq(in sampleAup, in _cachedAup) <= (double)_cachedInfluenceRadiusSq;
            }

            Vector3 delta = worldPos - _cachedPosition;
            float distanceSq = delta.x * delta.x + delta.y * delta.y + delta.z * delta.z;
            return distanceSq <= _cachedInfluenceRadiusSq;
        }

        private Vector3 ComputeFlowDirection(Vector3 worldPos)
        {
            switch (flowPattern)
            {
                case FlowPattern.RadialInward:
                    return ComputeRadialDirection(worldPos, true);

                case FlowPattern.RadialOutward:
                    return ComputeRadialDirection(worldPos, false);

                case FlowPattern.VortexClockwise:
                    return ComputeVortexDirection(worldPos, _cachedUp, true);

                case FlowPattern.VortexCounterClockwise:
                    return ComputeVortexDirection(worldPos, _cachedUp, false);

                case FlowPattern.Updraft:
                    return _cachedUp;

                case FlowPattern.Downdraft:
                    return -_cachedUp;

                default:
                    return _cachedDirectionalFlow;
            }
        }

        private Vector3 ComputeRadialDirection(Vector3 worldPos, bool inward)
        {
            Vector3 delta = worldPos - _cachedPosition;
            float vertical = delta.y;
            delta.y = 0f;
            if (delta.sqrMagnitude <= 0.0001f)
            {
                return inward ? -_cachedForward : _cachedForward;
            }

            Vector3 radial = DominantAxisOrDefault(delta, _cachedForward);
            if (inward)
                radial = -radial;
            radial.y = math.clamp(verticalFactor * vertical * 0.1f, -1f, 1f);
            return radial;
        }

        private Vector3 ComputeVortexDirection(Vector3 worldPos, Vector3 axis, bool clockwise)
        {
            Vector3 delta = worldPos - _cachedPosition;
            Vector3 radial = ProjectOnPlaneUnit(delta, axis);
            if (radial.sqrMagnitude <= 0.0001f)
            {
                return axis * verticalFactor;
            }

            Vector3 tangent = clockwise
                ? CrossVector(axis, radial)
                : CrossVector(radial, axis);

            tangent = DominantAxisOrDefault(tangent, Vector3.zero);
            radial = DominantAxisOrDefault(radial, Vector3.zero);

            Vector3 dir = tangent + (-radial * vortexRadialPull) + (axis * verticalFactor);
            return dir;
        }

        private float ComputeBoxWeight(Vector3 worldPos)
        {
            Vector3 local = _cachedWorldToLocalMatrix.MultiplyPoint3x4(worldPos);
            Vector3 half = boxSize * 0.5f;

            if (math.abs(local.x) > half.x || math.abs(local.y) > half.y || math.abs(local.z) > half.z)
                return 0f;

            float softness = math.saturate(edgeSoftness);
            float safeX = half.x > 0.001f ? 1f - math.abs(local.x) / half.x : 1f;
            float safeY = half.y > 0.001f ? 1f - math.abs(local.y) / half.y : 1f;
            float safeZ = half.z > 0.001f ? 1f - math.abs(local.z) / half.z : 1f;
            float edge = math.min(safeX, math.min(safeY, safeZ));
            return math.saturate(edge / math.max(0.01f, softness));
        }

        private float ComputeSphereWeight(Vector3 worldPos)
        {
            float radius = math.max(0.01f, sphereRadius);
            float distanceSq = (_cachedPosition - worldPos).sqrMagnitude;
            float radiusSq = radius * radius;
            if (distanceSq >= radiusSq)
                return 0f;

            float edge = 1f - distanceSq / math.max(radiusSq, 0.0001f);
            return math.saturate(edge / math.max(0.01f, edgeSoftness));
        }

        private void RefreshSampleCache()
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            uint shiftSequence = HectonFloatingOrigin.CurrentShiftSequence;
            if (_sampleCacheFrame == frame && _sampleCacheShiftSequence == shiftSequence)
                return;

            Transform cachedTransform = transform;
            _cachedPosition = cachedTransform.position;
            _cachedUp = DominantAxisOrDefault(cachedTransform.up, Vector3.up);
            _cachedForward = DominantAxisOrDefault(cachedTransform.forward, Vector3.forward);
            _cachedWorldToLocalMatrix = cachedTransform.worldToLocalMatrix;
            Vector3 safeLocalDirection = DominantAxisOrDefault(localDirection, Vector3.forward);
            _cachedDirectionalFlow = cachedTransform.TransformDirection(safeLocalDirection);
            _cachedDirectionalFlow.y *= verticalFactor;
            float influenceRadius = GetApproximateInfluenceRadius();
            _cachedInfluenceRadiusSq = influenceRadius * influenceRadius;
            if (_cachedInfluenceRadiusSq > LargeVolumeAupCullThresholdSq)
            {
                _cachedAupValid = TryResolveAupFromRuntimeOrigin(_cachedPosition, out _cachedAup);
            }
            else
            {
                _cachedAup = default;
                _cachedAupValid = false;
            }

            _cachedSampleTime = ResolveFrameSampleTime();
            _sampleCacheFrame = frame;
            _sampleCacheShiftSequence = shiftSequence;
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!math.isfinite(runtimePosition.x) ||
                !math.isfinite(runtimePosition.y) ||
                !math.isfinite(runtimePosition.z))
            {
                return false;
            }

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return positionAup.IsFinite();
        }

        private static float ResolveFrameSampleTime()
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_sharedSampleTimeFrame != frame)
            {
                _sharedSampleTime = ResolveCurrentSampleClockSeconds();
                _sharedSampleTimeFrame = frame;
            }

            return _sharedSampleTime;
        }

        private static float ResolveCurrentSampleClockSeconds()
        {
            SystemDispatcher dispatcher = SystemDispatcher.ActiveRuntimeInstance;
            if (dispatcher == null)
                return 0f;

            double timeSeconds = dispatcher.DilatedTimeSeconds;
            if (!math.isfinite(timeSeconds) || timeSeconds <= 0d)
                return 0f;

            return (float)math.min(CurrentSampleClockMaxSeconds, timeSeconds);
        }

        private static float FastTriangleSigned(float phase)
        {
            float triangle01 = 1f - math.abs(math.frac(phase * 0.15915494f + 0.25f) * 2f - 1f);
            return triangle01 * 2f - 1f;
        }

        private static Vector3 DominantAxisOrDefault(Vector3 value, Vector3 fallback)
        {
            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float az = math.abs(value.z);
            float maxComponent = math.max(ax, math.max(ay, az));
            if (maxComponent <= 0.000001f)
                return fallback;

            if (ax >= ay && ax >= az)
                return new Vector3(value.x >= 0f ? 1f : -1f, 0f, 0f);

            if (ay >= az)
                return new Vector3(0f, value.y >= 0f ? 1f : -1f, 0f);

            return new Vector3(0f, 0f, value.z >= 0f ? 1f : -1f);
        }

        private static Vector3 ProjectOnPlaneUnit(Vector3 value, Vector3 unitNormal)
        {
            return value - unitNormal * DotVector(value, unitNormal);
        }

        private static float DotVector(Vector3 a, Vector3 b)
        {
            return a.x * b.x + a.y * b.y + a.z * b.z;
        }

        private static Vector3 CrossVector(Vector3 a, Vector3 b)
        {
            return new Vector3(
                a.y * b.z - a.z * b.y,
                a.z * b.x - a.x * b.z,
                a.x * b.y - a.y * b.x);
        }

        private static float ResolveL1MagnitudeUpperBound(Vector3 value)
        {
            return math.abs(value.x) + math.abs(value.y) + math.abs(value.z);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (strength < 0f) strength = 0f;
            if (pulseFrequency < 0f) pulseFrequency = 0f;
            if (turbulenceStrength < 0f) turbulenceStrength = 0f;
            if (turbulenceScale < 0f) turbulenceScale = 0f;
            if (turbulenceTimeScale < 0f) turbulenceTimeScale = 0f;
            if (sphereRadius < 0.01f) sphereRadius = 0.01f;
            vortexRadialPull = math.clamp(vortexRadialPull, -1f, 1f);
            boxSize.x = math.max(0.01f, boxSize.x);
            boxSize.y = math.max(0.01f, boxSize.y);
            boxSize.z = math.max(0.01f, boxSize.z);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.1f, 0.85f, 0.95f, 0.2f);
            Matrix4x4 old = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            if (shape == VolumeShape.Box)
                Gizmos.DrawCube(Vector3.zero, boxSize);
            else
                Gizmos.DrawSphere(Vector3.zero, sphereRadius);

            Gizmos.color = new Color(0.1f, 0.95f, 1f, 0.85f);
            Gizmos.DrawRay(Vector3.zero, DominantAxisOrDefault(localDirection, Vector3.forward) * 2f);
            Gizmos.matrix = old;
        }
#endif
    }
}

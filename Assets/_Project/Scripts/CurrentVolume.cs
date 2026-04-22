// ============================================================================
// HECTON-8 — CurrentVolume.cs
// Local authored current field. Cheap additive influence on top of the global
// phantom current. Used by player, buoyancy, and ambient motion.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace Hecton8.Physics
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Physics/Current Volume")]
    public sealed class CurrentVolume : MonoBehaviour
    {
        private const float SharedAmbientNoiseScale = 0.0135f;
        private const float SharedAmbientTimeScale = 0.11f;
        private const float SharedAmbientStrength = 0.9f;
        private const float SharedAmbientVerticalFactor = 0.08f;

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

        private static readonly List<CurrentVolume>    ActiveVolumes    = new List<CurrentVolume>(32);
        private static readonly HashSet<CurrentVolume> ActiveVolumesSet = new HashSet<CurrentVolume>();

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

        public static int ActiveCount => ActiveVolumes.Count;

        public static IReadOnlyList<CurrentVolume> ActiveVolumeList => ActiveVolumes;

        public static Vector3 SampleAt(Vector3 worldPos)
        {
            Vector3 total = Vector3.zero;
            int count = ActiveVolumes.Count;
            for (int i = 0; i < count; i++)
            {
                CurrentVolume volume = ActiveVolumes[i];
                if (volume == null || !volume.isActiveAndEnabled)
                    continue;

                total += volume.SampleInternal(worldPos);
            }

            return total;
        }

        internal static Vector3 SampleCombinedCurrent(Vector3 worldPos)
        {
            Unity.Mathematics.float3 ambient = CurrentManager.SampleCurrent(
                new Unity.Mathematics.float3(worldPos.x, worldPos.y, worldPos.z),
                Time.time,
                SharedAmbientNoiseScale,
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
                ? targetLocalDirection.normalized
                : Vector3.forward;
            strength = Mathf.Max(0f, targetStrength);
            verticalFactor = Mathf.Clamp(targetVerticalFactor, -1f, 1f);
            vortexRadialPull = Mathf.Clamp(targetVortexRadialPull, -1f, 1f);
        }

        internal void ApplySemanticBoundsPreset(
            VolumeShape targetShape,
            Vector3 targetBoxSize,
            float targetSphereRadius)
        {
            shape = targetShape;
            boxSize = new Vector3(
                Mathf.Max(0.01f, targetBoxSize.x),
                Mathf.Max(0.01f, targetBoxSize.y),
                Mathf.Max(0.01f, targetBoxSize.z));
            sphereRadius = Mathf.Max(0.01f, targetSphereRadius);
        }

        internal float GetApproximateInfluenceRadius()
        {
            if (shape == VolumeShape.Sphere)
                return Mathf.Max(0.01f, sphereRadius);

            Vector3 halfExtents = boxSize * 0.5f;
            return Mathf.Max(0.01f, halfExtents.magnitude);
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
            float weight = shape == VolumeShape.Box
                ? ComputeBoxWeight(worldPos)
                : ComputeSphereWeight(worldPos);

            if (weight <= 0.0001f)
                return Vector3.zero;

            Vector3 dir = ComputeFlowDirection(worldPos);
            if (dir.sqrMagnitude <= 0.0001f)
                return Vector3.zero;

            dir.Normalize();

            float pulse = 1f;
            if (pulseAmplitude > 0.0001f && pulseFrequency > 0.0001f)
            {
                float t = Time.time * pulseFrequency + phaseOffset;
                pulse += Mathf.Sin(t * Mathf.PI * 2f) * pulseAmplitude;
            }

            Vector3 result = dir * (strength * pulse * weight);

            if (turbulenceStrength > 0.0001f && turbulenceScale > 0.0001f)
            {
                var noise = CurrentManager.SampleCurrent(
                    new Unity.Mathematics.float3(worldPos.x, worldPos.y, worldPos.z),
                    Time.time + phaseOffset,
                    turbulenceScale,
                    turbulenceTimeScale,
                    turbulenceStrength * weight,
                    verticalFactor * 0.5f);

                result += new Vector3(noise.x, noise.y, noise.z);
            }

            return result;
        }

        private Vector3 ComputeFlowDirection(Vector3 worldPos)
        {
            Vector3 up = transform.up;
            switch (flowPattern)
            {
                case FlowPattern.RadialInward:
                    return ComputeRadialDirection(worldPos, true);

                case FlowPattern.RadialOutward:
                    return ComputeRadialDirection(worldPos, false);

                case FlowPattern.VortexClockwise:
                    return ComputeVortexDirection(worldPos, up, true);

                case FlowPattern.VortexCounterClockwise:
                    return ComputeVortexDirection(worldPos, up, false);

                case FlowPattern.Updraft:
                    return up;

                case FlowPattern.Downdraft:
                    return -up;

                default:
                    Vector3 directional = transform.TransformDirection(localDirection.normalized);
                    directional.y *= verticalFactor;
                    return directional;
            }
        }

        private Vector3 ComputeRadialDirection(Vector3 worldPos, bool inward)
        {
            Vector3 delta = worldPos - transform.position;
            float vertical = delta.y;
            delta.y = 0f;
            if (delta.sqrMagnitude <= 0.0001f)
            {
                return inward ? -transform.forward : transform.forward;
            }

            Vector3 radial = inward ? -delta.normalized : delta.normalized;
            radial.y = Mathf.Clamp(verticalFactor * vertical * 0.1f, -1f, 1f);
            return radial;
        }

        private Vector3 ComputeVortexDirection(Vector3 worldPos, Vector3 axis, bool clockwise)
        {
            Vector3 delta = worldPos - transform.position;
            Vector3 radial = Vector3.ProjectOnPlane(delta, axis);
            if (radial.sqrMagnitude <= 0.0001f)
            {
                return axis * verticalFactor;
            }

            Vector3 tangent = clockwise
                ? Vector3.Cross(axis, radial)
                : Vector3.Cross(radial, axis);

            tangent.Normalize();
            radial.Normalize();

            Vector3 dir = tangent + (-radial * vortexRadialPull) + (axis * verticalFactor);
            return dir;
        }

        private float ComputeBoxWeight(Vector3 worldPos)
        {
            Vector3 local = transform.InverseTransformPoint(worldPos);
            Vector3 half = boxSize * 0.5f;

            if (Mathf.Abs(local.x) > half.x || Mathf.Abs(local.y) > half.y || Mathf.Abs(local.z) > half.z)
                return 0f;

            float softness = Mathf.Clamp01(edgeSoftness);
            float safeX = half.x > 0.001f ? 1f - Mathf.Abs(local.x) / half.x : 1f;
            float safeY = half.y > 0.001f ? 1f - Mathf.Abs(local.y) / half.y : 1f;
            float safeZ = half.z > 0.001f ? 1f - Mathf.Abs(local.z) / half.z : 1f;
            float edge = Mathf.Min(safeX, Mathf.Min(safeY, safeZ));
            return Mathf.Clamp01(edge / Mathf.Max(0.01f, softness));
        }

        private float ComputeSphereWeight(Vector3 worldPos)
        {
            float radius = Mathf.Max(0.01f, sphereRadius);
            float distance = Vector3.Distance(transform.position, worldPos);
            if (distance >= radius)
                return 0f;

            float edge = 1f - distance / radius;
            return Mathf.Clamp01(edge / Mathf.Max(0.01f, edgeSoftness));
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
            vortexRadialPull = Mathf.Clamp(vortexRadialPull, -1f, 1f);
            boxSize.x = Mathf.Max(0.01f, boxSize.x);
            boxSize.y = Mathf.Max(0.01f, boxSize.y);
            boxSize.z = Mathf.Max(0.01f, boxSize.z);
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
            Gizmos.DrawRay(Vector3.zero, localDirection.normalized * 2f);
            Gizmos.matrix = old;
        }
#endif
    }
}

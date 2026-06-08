using UnityEngine;

namespace Hecton8.Physics
{
    [CreateAssetMenu(
        fileName = "AmbientWaterMotionProfile",
        menuName = "Hecton/Physics/Ambient Water Motion Profile",
        order = 41)]
    public sealed class AmbientWaterMotionProfile : ScriptableObject
    {
        private const float MaxAmplitudeMeters = 2f;
        private const float MaxAngularDegrees = 24f;
        private const float MaxFrequency = 8f;
        private const float MaxCurrentCoupling = 2f;
        private const float MaxLodBias = 8f;

        [Header("Offsets")]
        [Min(0f)] public float verticalAmplitude = 0.06f;
        public Vector3 positionalAmplitude = new Vector3(0.04f, 0f, 0.04f);

        [Header("Rotation")]
        public Vector3 angularAmplitude = new Vector3(3f, 1.5f, 4f);

        [Header("Timing")]
        [Min(0f)] public float baseFrequency = 0.45f;
        [Range(0f, 2f)] public float currentCoupling = 0.6f;
        public bool allowDistanceLod = true;
        [Min(0.1f)] public float lodBias = 1f;

        internal static float ResolveAmplitude(float amplitude)
        {
            return Mathf.Clamp(float.IsFinite(amplitude) ? amplitude : 0f, 0f, MaxAmplitudeMeters);
        }

        internal static Vector3 ResolvePositionalAmplitude(Vector3 amplitude)
        {
            return ClampFiniteVector(amplitude, MaxAmplitudeMeters);
        }

        internal static Vector3 ResolveAngularAmplitude(Vector3 amplitude)
        {
            return ClampFiniteVector(amplitude, MaxAngularDegrees);
        }

        internal static float ResolveFrequency(float frequency)
        {
            return Mathf.Clamp(float.IsFinite(frequency) ? frequency : 0f, 0f, MaxFrequency);
        }

        internal static float ResolveCurrentCoupling(float coupling)
        {
            return Mathf.Clamp(float.IsFinite(coupling) ? coupling : 0f, 0f, MaxCurrentCoupling);
        }

        internal static float ResolveLodBias(float bias)
        {
            return Mathf.Clamp(float.IsFinite(bias) ? bias : 1f, 0.1f, MaxLodBias);
        }

        private static Vector3 ClampFiniteVector(Vector3 value, float maxMagnitude)
        {
            if (!IsFinite(value))
                return Vector3.zero;

            float safeMax = Mathf.Max(0f, float.IsFinite(maxMagnitude) ? maxMagnitude : 0f);
            if (safeMax <= 0f)
                return Vector3.zero;

            float sqrMagnitude = value.x * value.x + value.y * value.y + value.z * value.z;
            float maxSqr = safeMax * safeMax;
            if (!float.IsFinite(sqrMagnitude))
                return Vector3.zero;
            if (sqrMagnitude <= maxSqr)
                return value;

            return value * (safeMax / Mathf.Sqrt(sqrMagnitude));
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            verticalAmplitude = ResolveAmplitude(verticalAmplitude);
            positionalAmplitude = ResolvePositionalAmplitude(positionalAmplitude);
            angularAmplitude = ResolveAngularAmplitude(angularAmplitude);
            baseFrequency = ResolveFrequency(baseFrequency);
            currentCoupling = ResolveCurrentCoupling(currentCoupling);
            lodBias = ResolveLodBias(lodBias);
        }
#endif
    }
}

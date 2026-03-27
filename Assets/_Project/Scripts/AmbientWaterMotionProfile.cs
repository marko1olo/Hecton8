using UnityEngine;

namespace Hecton8.Physics
{
    [CreateAssetMenu(
        fileName = "AmbientWaterMotionProfile",
        menuName = "Hecton/Physics/Ambient Water Motion Profile",
        order = 41)]
    public sealed class AmbientWaterMotionProfile : ScriptableObject
    {
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
    }
}
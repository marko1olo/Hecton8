using UnityEngine;

namespace Hecton8.Physics
{
    [CreateAssetMenu(
        fileName = "BuoyancyProfile",
        menuName = "Hecton/Physics/Buoyancy Profile",
        order = 40)]
    public sealed class BuoyancyProfile : ScriptableObject
    {
        [Header("Physical Properties")]
        [Min(0.01f)] public float density = 500f;
        [Min(0.0001f)] public float volume = 0.01f;
        [Min(0.01f)] public float height = 0.3f;

        [Header("Behavior")]
        [Min(0f)] public float currentResponse = 1f;
        [Min(0f)] public float surfaceStability = 0.75f;
        [Min(0.1f)] public float lodBias = 1f;
        public bool allowDistanceLod = true;
    }
}
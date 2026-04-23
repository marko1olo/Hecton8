using UnityEngine;
#if UNITY_EDITOR
using Sirenix.OdinInspector;
#endif

namespace Hecton8.Physics
{
    [CreateAssetMenu(
        fileName = "BuoyancyProfile",
        menuName = "Hecton/Physics/Buoyancy Profile",
        order = 40)]
    public sealed class BuoyancyProfile : ScriptableObject
    {
        [Header("Physical Properties")]
#if UNITY_EDITOR
        [MinValue(0.01d)]
        [ValidateInput(nameof(IsFinitePositive), "Density must be finite and greater than zero.")]
#endif
        [Min(0.01f)] public float density = 500f;
#if UNITY_EDITOR
        [MinValue(0.0001d)]
        [ValidateInput(nameof(IsFinitePositive), "Volume must be finite and greater than zero.")]
#endif
        [Min(0.0001f)] public float volume = 0.01f;
#if UNITY_EDITOR
        [MinValue(0.01d)]
        [ValidateInput(nameof(IsFinitePositive), "Height must be finite and greater than zero.")]
#endif
        [Min(0.01f)] public float height = 0.3f;

        [Header("Behavior")]
#if UNITY_EDITOR
        [MinValue(0d)]
        [ValidateInput(nameof(IsFiniteNonNegative), "Current Response must be finite and non-negative.")]
#endif
        [Min(0f)] public float currentResponse = 1f;
#if UNITY_EDITOR
        [MinValue(0d)]
        [ValidateInput(nameof(IsFiniteNonNegative), "Surface Stability must be finite and non-negative.")]
#endif
        [Min(0f)] public float surfaceStability = 0.75f;
#if UNITY_EDITOR
        [MinValue(0.1d)]
        [ValidateInput(nameof(IsFinitePositive), "LOD Bias must be finite and greater than zero.")]
#endif
        [Min(0.1f)] public float lodBias = 1f;
        public bool allowDistanceLod = true;

#if UNITY_EDITOR
        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }
#endif
    }
}

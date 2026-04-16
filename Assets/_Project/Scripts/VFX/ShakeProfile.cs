// ============================================================================
// HECTON-8 — ShakeProfile.cs
// Configuration data for camera shake effects.
// ============================================================================

using UnityEngine;

namespace Hecton8.VFX
{
    /// <summary>
    /// ScriptableObject defining camera shake parameters.
    /// </summary>
    [CreateAssetMenu(fileName = "ShakeProfile_", menuName = "HECTON-8/VFX/Shake Profile")]
    public sealed class ShakeProfile : ScriptableObject
    {
        [Header("── Intensity ──────────────────")]
        [Tooltip("Maximum displacement in world units")]
        [Range(0f, 0.5f)]
        public float MaxDisplacement = 0.1f;

        [Header("── Frequency ──────────────────")]
        [Tooltip("Shake oscillation frequency (Hz)")]
        [Range(1f, 30f)]
        public float Frequency = 15f;

        [Header("── Duration ──────────────────")]
        [Tooltip("Total shake duration (seconds)")]
        [Range(0.1f, 3f)]
        public float Duration = 0.5f;

        [Header("── Falloff ──────────────────")]
        [Tooltip("Intensity falloff curve over duration")]
        public AnimationCurve FalloffCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        [Header("── Axes ──────────────────")]
        [Tooltip("Shake contribution per axis (normalized)")]
        public Vector3 AxisWeights = new Vector3(1f, 1f, 0.5f);

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Clamp values to valid ranges
            MaxDisplacement = Mathf.Clamp(MaxDisplacement, 0f, 1f);
            Frequency = Mathf.Clamp(Frequency, 1f, 30f);
            Duration = Mathf.Clamp(Duration, 0.1f, 3f);

            // Validate duration
            if (Duration <= 0f)
            {
                Debug.LogWarning($"[ShakeProfile] Invalid Duration {Duration}. Clamping to 0.5s.");
                Duration = 0.5f;
            }
        }
#endif
    }
}

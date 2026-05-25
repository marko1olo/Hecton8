// ============================================================================
// HECTON-8 — ShakeProfile.cs
// Legacy authoring data for presentation-only camera impulse severity.
// ============================================================================

using UnityEngine;

namespace Hecton8.VFX
{
    /// <summary>
    /// ScriptableObject defining cold authoring scalars for procedural camera impulses.
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
        [Tooltip("Authoring scalar for polynomial falloff. Runtime shake decay is evaluated by the Burst tuning DTO, not Unity curve assets.")]
        [Range(0.5f, 4f)]
        public float FalloffExponent = 2f;

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
            FalloffExponent = Mathf.Clamp(FalloffExponent, 0.5f, 4f);

            // Validate duration
            if (Duration <= 0f)
            {
                Hecton8.Core.H8Debug.LogWarning("[ShakeProfile] Invalid Duration. Clamping to 0.5s.");
                Duration = 0.5f;
            }
        }
#endif
    }
}

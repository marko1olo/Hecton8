// ============================================================================
// HECTON-8 — BiomeProfile.cs
// Post-processing configuration per biome.
// ============================================================================

using UnityEngine;

namespace Hecton8.VFX
{
    /// <summary>
    /// ScriptableObject defining biome-specific post-processing parameters.
    /// </summary>
    [CreateAssetMenu(fileName = "BiomeProfile_", menuName = "HECTON-8/VFX/Biome Profile")]
    public sealed class BiomeProfile : ScriptableObject
    {
        [Header("── Color Grading ──────────────────")]
        public Color ColorFilter = Color.white;

        [Range(-100f, 100f)]
        [Tooltip("Color temperature adjustment")]
        public float Temperature = 0f;

        [Range(-100f, 100f)]
        [Tooltip("Color tint adjustment")]
        public float Tint = 0f;

        [Header("── Ambient Occlusion ──────────────────")]
        [Range(0f, 4f)]
        [Tooltip("Ambient occlusion intensity")]
        public float AOIntensity = 1f;

        [Range(0f, 2f)]
        [Tooltip("Ambient occlusion radius")]
        public float AORadius = 1f;

        [Header("── Bloom ──────────────────")]
        [Range(0f, 1f)]
        [Tooltip("Bloom intensity")]
        public float BloomIntensity = 0.3f;

        [Range(0f, 10f)]
        [Tooltip("Bloom threshold")]
        public float BloomThreshold = 0.9f;

        [Header("── Fog ──────────────────")]
        [Tooltip("Fog color")]
        public Color FogColor = new Color(0.5f, 0.6f, 0.7f);

        [Range(0f, 1f)]
        [Tooltip("Fog density")]
        public float FogDensity = 0.01f;

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Clamp all Range fields to valid bounds
            Temperature = Mathf.Clamp(Temperature, -100f, 100f);
            Tint = Mathf.Clamp(Tint, -100f, 100f);
            AOIntensity = Mathf.Clamp(AOIntensity, 0f, 4f);
            AORadius = Mathf.Clamp(AORadius, 0f, 2f);
            BloomIntensity = Mathf.Clamp(BloomIntensity, 0f, 1f);
            BloomThreshold = Mathf.Clamp(BloomThreshold, 0f, 10f);
            FogDensity = Mathf.Clamp(FogDensity, 0f, 1f);

            // Log warnings for invalid configurations
            if (AOIntensity > 3f)
            {
                Hecton8.Core.H8Debug.LogWarning("[BiomeProfile] High AOIntensity may impact performance.");
            }
        }
#endif
    }
}

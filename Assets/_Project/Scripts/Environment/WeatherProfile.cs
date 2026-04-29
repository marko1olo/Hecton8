using UnityEngine;

namespace Hecton8.Environment
{
    /// <summary>
    /// Authoring profile for macro-scale weather modulation that bridges the surface state into deep-ocean systems.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WeatherProfile",
        menuName = "Hecton8/Environment/Weather Profile",
        order = 112)]
    public sealed class WeatherProfile : ScriptableObject
    {
        [Header("Atmospherics")]
        [Tooltip("Authoritative world-space wind vector for this macro-weather profile.")]
        [SerializeField] private Vector3 windVector = new Vector3(0.5f, 0f, 1f);

        [Tooltip("Maximum aggregate wave height in meters targeted by the fallback weather spectrum.")]
        [SerializeField, Min(0f)] private float waveHeightMax = 1f;

        [Header("Abyssal Response")]
        [Tooltip("Multiplier applied to abyssal turbulence response when this profile is selected by the global weather owner.")]
        [SerializeField, Min(0f)] private float abyssalTurbulenceMultiplier = 1f;

        [Tooltip("Optional fog-color lookup texture reserved for downstream sky and volume systems.")]
        [SerializeField] private Texture2D fogColorLut;

        /// <summary>
        /// World-space wind vector for this authored weather state.
        /// </summary>
        public Vector3 WindVector => windVector;

        /// <summary>
        /// Maximum aggregate wave height in meters.
        /// </summary>
        public float WaveHeightMax => waveHeightMax;

        /// <summary>
        /// Multiplier applied to abyssal turbulence response.
        /// </summary>
        public float AbyssalTurbulenceMultiplier => abyssalTurbulenceMultiplier;

        /// <summary>
        /// Optional fog-color lookup texture for downstream presentation systems.
        /// </summary>
        public Texture2D FogColorLut => fogColorLut;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (windVector.sqrMagnitude <= 0.0001f)
                windVector = Vector3.forward;

            waveHeightMax = Mathf.Max(0f, waveHeightMax);
            abyssalTurbulenceMultiplier = Mathf.Max(0f, abyssalTurbulenceMultiplier);
        }
#endif
    }
}

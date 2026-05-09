using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Visor
{
    /// <summary>
    /// Static LifePod BIOS diagnostic visibility state consumed by visor presentation shaders.
    /// </summary>
    public static class HectonBiosDiagnosticState
    {
        private static bool s_active;
        private static float s_intensity = 1f;

        /// <summary>
        /// True while the BIOS diagnostic overlay is active.
        /// </summary>
        public static bool IsActive => s_active;

        /// <summary>
        /// Current normalized BIOS diagnostic intensity.
        /// </summary>
        public static float Intensity => s_active ? s_intensity : 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_active = false;
            s_intensity = 1f;
        }

        /// <summary>
        /// Publishes normalized BIOS diagnostic overlay state without heap allocation.
        /// </summary>
        /// <param name="active">True to show the overlay.</param>
        /// <param name="intensity">Normalized overlay intensity.</param>
        public static void SetActive(bool active, float intensity = 1f)
        {
            s_active = active;
            s_intensity = math.saturate(intensity);
        }
    }
}

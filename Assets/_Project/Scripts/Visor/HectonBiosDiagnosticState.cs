using Unity.Mathematics;

namespace Hecton8.Visor
{
    public static class HectonBiosDiagnosticState
    {
        private static bool s_active;
        private static float s_intensity = 1f;

        public static bool IsActive => s_active;
        public static float Intensity => s_active ? s_intensity : 0f;

        public static void SetActive(bool active, float intensity = 1f)
        {
            s_active = active;
            s_intensity = math.saturate(intensity);
        }
    }
}

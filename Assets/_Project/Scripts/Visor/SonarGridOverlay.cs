using UnityEngine;

namespace Hecton8.Visor
{
    /// <summary>
    /// Shared shader-global owner for active-sonar noir grid presentation.
    /// </summary>
    internal static class SonarGridOverlay
    {
        private static readonly int ShaderSonarGridParams =
            Shader.PropertyToID("_SonarGridParams0");
        private static readonly int ShaderSonarGridHardColor =
            Shader.PropertyToID("_SonarGridHardColor");
        private static readonly int ShaderSonarGridOrganicColor =
            Shader.PropertyToID("_SonarGridOrganicColor");
        private static readonly int ShaderSonarGridAbyssalColor =
            Shader.PropertyToID("_SonarGridAbyssalColor");

        internal static void ApplyGlobals(
            float intensity,
            float lineScale,
            float lineWidth,
            float contourBoost,
            Color hardColor,
            Color organicColor,
            Color abyssalColor)
        {
            Shader.SetGlobalVector(
                ShaderSonarGridParams,
                new Vector4(
                    Mathf.Max(0f, intensity),
                    Mathf.Max(0.01f, lineScale),
                    Mathf.Max(0.001f, lineWidth),
                    Mathf.Max(0f, contourBoost)));
            Shader.SetGlobalColor(ShaderSonarGridHardColor, hardColor);
            Shader.SetGlobalColor(ShaderSonarGridOrganicColor, organicColor);
            Shader.SetGlobalColor(ShaderSonarGridAbyssalColor, abyssalColor);
        }

        internal static void ClearGlobals()
        {
            Shader.SetGlobalVector(ShaderSonarGridParams, Vector4.zero);
            Shader.SetGlobalColor(ShaderSonarGridHardColor, Color.black);
            Shader.SetGlobalColor(ShaderSonarGridOrganicColor, Color.black);
            Shader.SetGlobalColor(ShaderSonarGridAbyssalColor, Color.black);
        }
    }
}

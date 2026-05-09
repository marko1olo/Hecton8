using Unity.Mathematics;
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
        private const float PublishEpsilon = 0.0001f;
        private static bool s_hasPublished;
        private static Vector4 s_lastParams;
        private static Color s_lastHardColor;
        private static Color s_lastOrganicColor;
        private static Color s_lastAbyssalColor;

        internal static void ApplyGlobals(
            float intensity,
            float lineScale,
            float lineWidth,
            float contourBoost,
            Color hardColor,
            Color organicColor,
            Color abyssalColor)
        {
            Vector4 gridParams = new Vector4(
                math.max(0f, intensity),
                math.max(0.01f, lineScale),
                math.max(0.001f, lineWidth),
                math.max(0f, contourBoost));

            if (!s_hasPublished || !NearlyEqual(s_lastParams, gridParams))
            {
                Shader.SetGlobalVector(ShaderSonarGridParams, gridParams);
                s_lastParams = gridParams;
            }

            if (!s_hasPublished || !NearlyEqual(s_lastHardColor, hardColor))
            {
                Shader.SetGlobalColor(ShaderSonarGridHardColor, hardColor);
                s_lastHardColor = hardColor;
            }

            if (!s_hasPublished || !NearlyEqual(s_lastOrganicColor, organicColor))
            {
                Shader.SetGlobalColor(ShaderSonarGridOrganicColor, organicColor);
                s_lastOrganicColor = organicColor;
            }

            if (!s_hasPublished || !NearlyEqual(s_lastAbyssalColor, abyssalColor))
            {
                Shader.SetGlobalColor(ShaderSonarGridAbyssalColor, abyssalColor);
                s_lastAbyssalColor = abyssalColor;
            }

            s_hasPublished = true;
        }

        internal static void ClearGlobals()
        {
            if (!s_hasPublished)
                return;

            Shader.SetGlobalVector(ShaderSonarGridParams, Vector4.zero);
            Shader.SetGlobalColor(ShaderSonarGridHardColor, Color.black);
            Shader.SetGlobalColor(ShaderSonarGridOrganicColor, Color.black);
            Shader.SetGlobalColor(ShaderSonarGridAbyssalColor, Color.black);
            s_lastParams = Vector4.zero;
            s_lastHardColor = Color.black;
            s_lastOrganicColor = Color.black;
            s_lastAbyssalColor = Color.black;
            s_hasPublished = false;
        }

        private static bool NearlyEqual(Vector4 lhs, Vector4 rhs)
        {
            return math.abs(lhs.x - rhs.x) <= PublishEpsilon &&
                   math.abs(lhs.y - rhs.y) <= PublishEpsilon &&
                   math.abs(lhs.z - rhs.z) <= PublishEpsilon &&
                   math.abs(lhs.w - rhs.w) <= PublishEpsilon;
        }

        private static bool NearlyEqual(Color lhs, Color rhs)
        {
            return math.abs(lhs.r - rhs.r) <= PublishEpsilon &&
                   math.abs(lhs.g - rhs.g) <= PublishEpsilon &&
                   math.abs(lhs.b - rhs.b) <= PublishEpsilon &&
                   math.abs(lhs.a - rhs.a) <= PublishEpsilon;
        }
    }
}

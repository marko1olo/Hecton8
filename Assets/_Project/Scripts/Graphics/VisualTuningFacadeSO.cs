#if UNITY_EDITOR
using UnityEngine;
using Unity.Mathematics;
using System.IO;

namespace Hecton8.Graphics.Authoring
{
    [CreateAssetMenu(fileName = "VisualTuningFacade", menuName = "Hecton-8/Visuals/Tuning Facade")]
    public class VisualTuningFacadeSO : ScriptableObject
    {
        [Header("Ocean Scattering")]
        public Color oceanScatterBase = new Color(0.05f, 0.45f, 0.45f, 1f);
        public Color oceanScatterShallow = new Color(0.15f, 0.75f, 0.7f, 1f);
        [Range(0f, 50f)]
        public float oceanScatterShallowDepthMax = 10f;

        [Header("Celestial")]
        public Color sunColor = new Color(1f, 0.95f, 0.9f, 1f);
        [Range(0f, 10f)]
        public float sunIntensity = 1.2f;
        [Range(1f, 100f)]
        public float planetCenterRadius = 15f;

        [Header("Post Processing")]
        [Range(0.1f, 5f)]
        public float exposure = 1.0f;

        [Header("Baking Metadata")]
        [ReadOnly]
        public string lastBakedHash;
        [ReadOnly]
        public string lastBakedTime;

        // Custom PropertyAttribute for ReadOnly field in inspector
        public class ReadOnlyAttribute : PropertyAttribute { }

        public VisualTuningState BakeToUnmanaged()
        {
            return new VisualTuningState
            {
                OceanScatterBase = new float4(oceanScatterBase.r, oceanScatterBase.g, oceanScatterBase.b, oceanScatterBase.a),
                OceanScatterShallow = new float4(oceanScatterShallow.r, oceanScatterShallow.g, oceanScatterShallow.b, oceanScatterShallow.a),
                SunColor = new float4(sunColor.r, sunColor.g, sunColor.b, sunColor.a),
                OceanScatterShallowDepthMax = oceanScatterShallowDepthMax,
                PlanetCenterRadius = planetCenterRadius,
                SunIntensity = sunIntensity,
                Exposure = exposure
            };
        }
    }
}
#endif

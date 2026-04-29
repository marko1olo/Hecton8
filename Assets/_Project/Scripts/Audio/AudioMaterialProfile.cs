using UnityEngine;

namespace Hecton8.Audio
{
    /// <summary>
    /// Per-layer acoustic material template used by environment authors.
    /// </summary>
    [System.Serializable]
    public struct AcousticLayerTemplate
    {
        [Tooltip("Author-facing semantic label for this acoustic layer entry.")]
        public string Label;

        [Tooltip("Unity Layer ID that should resolve to this acoustic material template.")]
        public int LayerId;

        [Tooltip("Per-path transmission multiplier after direct absorption is applied.")]
        [Range(0f, 1f)] public float Transmission01;

        [Tooltip("Low-band absorption coefficient.")]
        [Range(0f, 1f)] public float LowBandAbsorption01;

        [Tooltip("Mid-band absorption coefficient.")]
        [Range(0f, 1f)] public float MidBandAbsorption01;

        [Tooltip("High-band absorption coefficient.")]
        [Range(0f, 1f)] public float HighBandAbsorption01;

        [Tooltip("Additional reflected-energy low-pass cutoff in hertz.")]
        [Min(120f)] public float ReflectionLowPassCutoffHertz;

        [Tooltip("Semantic echo class consumed by procedural sonar and impact synthesis.")]
        public AcousticEchoSemanticClass EchoSemanticClass;
    }

    /// <summary>
    /// Semantic echo families used by the procedural sonar and impact renderer.
    /// </summary>
    public enum AcousticEchoSemanticClass : byte
    {
        HardSpecular = 0,
        Metallic = 1,
        Biological = 2,
        Porous = 3,
        SoftDiffuse = 4
    }

    /// <summary>
    /// Authored acoustic material profile for enclosure, echo, and occlusion tuning.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioMaterialProfile_", menuName = "Hecton8/Audio/Audio Material Profile")]
    public sealed class AudioMaterialProfile : ScriptableObject
    {
        [Header("── Decay ──────────────────")]
        [Tooltip("Reference RT60 decay in seconds for this acoustic material family.")]
        [SerializeField, Min(0.05f)] private float _rt60Seconds = 0.8f;

        [Tooltip("Per-path transmission multiplier after direct absorption is applied.")]
        [SerializeField, Range(0f, 1f)] private float _transmission01 = 0.75f;

        [Header("── Absorption ──────────────────")]
        [Tooltip("Low-band absorption coefficient.")]
        [SerializeField, Range(0f, 1f)] private float _lowBandAbsorption01 = 0.12f;

        [Tooltip("Mid-band absorption coefficient.")]
        [SerializeField, Range(0f, 1f)] private float _midBandAbsorption01 = 0.24f;

        [Tooltip("High-band absorption coefficient.")]
        [SerializeField, Range(0f, 1f)] private float _highBandAbsorption01 = 0.52f;

        [Tooltip("Additional low-pass bias applied to reflected energy in hertz.")]
        [SerializeField, Min(120f)] private float _reflectionLowPassCutoffHertz = 4800f;

        [Header("── Semantics ──────────────────")]
        [Tooltip("Semantic echo family consumed by procedural sonar and impact synthesis.")]
        [SerializeField] private AcousticEchoSemanticClass _echoSemanticClass = AcousticEchoSemanticClass.HardSpecular;

        [Header("── Layer Templates ──────────────────")]
        [Tooltip("Authorable layer-to-acoustic mappings for environment semantic tuning.")]
        [SerializeField] private AcousticLayerTemplate[] _layerTemplates =
        {
            new AcousticLayerTemplate
            {
                Label = "ROCK",
                LayerId = 0,
                Transmission01 = 0.98f,
                LowBandAbsorption01 = 0.08f,
                MidBandAbsorption01 = 0.16f,
                HighBandAbsorption01 = 0.26f,
                ReflectionLowPassCutoffHertz = 6800f,
                EchoSemanticClass = AcousticEchoSemanticClass.HardSpecular
            },
            new AcousticLayerTemplate
            {
                Label = "METAL",
                LayerId = 0,
                Transmission01 = 0.96f,
                LowBandAbsorption01 = 0.06f,
                MidBandAbsorption01 = 0.12f,
                HighBandAbsorption01 = 0.18f,
                ReflectionLowPassCutoffHertz = 8200f,
                EchoSemanticClass = AcousticEchoSemanticClass.Metallic
            },
            new AcousticLayerTemplate
            {
                Label = "CORAL",
                LayerId = 0,
                Transmission01 = 0.54f,
                LowBandAbsorption01 = 0.18f,
                MidBandAbsorption01 = 0.42f,
                HighBandAbsorption01 = 0.68f,
                ReflectionLowPassCutoffHertz = 2400f,
                EchoSemanticClass = AcousticEchoSemanticClass.Biological
            },
            new AcousticLayerTemplate
            {
                Label = "KELP",
                LayerId = 0,
                Transmission01 = 0.30f,
                LowBandAbsorption01 = 0.22f,
                MidBandAbsorption01 = 0.48f,
                HighBandAbsorption01 = 0.82f,
                ReflectionLowPassCutoffHertz = 950f,
                EchoSemanticClass = AcousticEchoSemanticClass.Biological
            }
        };

        /// <summary>Reference RT60 decay in seconds for this acoustic material family.</summary>
        public float Rt60Seconds => _rt60Seconds;

        /// <summary>Per-path transmission multiplier after direct absorption is applied.</summary>
        public float Transmission01 => _transmission01;

        /// <summary>Low-band absorption coefficient.</summary>
        public float LowBandAbsorption01 => _lowBandAbsorption01;

        /// <summary>Mid-band absorption coefficient.</summary>
        public float MidBandAbsorption01 => _midBandAbsorption01;

        /// <summary>High-band absorption coefficient.</summary>
        public float HighBandAbsorption01 => _highBandAbsorption01;

        /// <summary>Additional low-pass bias applied to reflected energy in hertz.</summary>
        public float ReflectionLowPassCutoffHertz => _reflectionLowPassCutoffHertz;

        /// <summary>Semantic echo family consumed by procedural sonar and impact synthesis.</summary>
        public AcousticEchoSemanticClass EchoSemanticClass => _echoSemanticClass;

        /// <summary>Authorable layer-to-acoustic mappings for environment semantic tuning.</summary>
        public AcousticLayerTemplate[] LayerTemplates => _layerTemplates;
    }
}

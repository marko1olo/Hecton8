using System;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Authored mutation thresholds unlocked by sustained hazard exposure.
    /// </summary>
    [CreateAssetMenu(fileName = "HazardMutationProfile_", menuName = "Hecton8/Gameplay/Hazard Mutation Profile")]
    public sealed class HazardMutationProfile : ScriptableObject
    {
        [Serializable]
        public struct MutationThreshold
        {
            [Tooltip("UI-facing mutation label pushed when the threshold is crossed.")]
            public string DisplayName;

            [Tooltip("Exposure time in seconds required before the mutation bit is enabled.")]
            public float ExposureThresholdSeconds;

            [Tooltip("Single-bit mutation flag (1, 2, 4, 8, ...).")]
            public uint MutationBit;
        }

        public const uint BioluminescentSkinBit = 1u << 0;
        public const uint GillsBit = 1u << 1;
        public const uint RadiationLatticeBit = 1u << 2;
        public const uint NeuralEchoBit = 1u << 3;

        [Header("Mutation Thresholds")]
        [SerializeField] private MutationThreshold[] mutations =
        {
            new MutationThreshold { DisplayName = "BIOLUMINESCENT SKIN", ExposureThresholdSeconds = 120f, MutationBit = BioluminescentSkinBit },
            new MutationThreshold { DisplayName = "GILLS", ExposureThresholdSeconds = 180f, MutationBit = GillsBit },
            new MutationThreshold { DisplayName = "RADIATION LATTICE", ExposureThresholdSeconds = 240f, MutationBit = RadiationLatticeBit },
            new MutationThreshold { DisplayName = "NEURAL ECHO", ExposureThresholdSeconds = 300f, MutationBit = NeuralEchoBit }
        };

        public MutationThreshold[] Mutations => mutations;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (mutations == null || mutations.Length == 0)
                return;

            for (int i = 0; i < mutations.Length; i++)
            {
                MutationThreshold threshold = mutations[i];
                threshold.ExposureThresholdSeconds = Mathf.Max(0f, threshold.ExposureThresholdSeconds);
                mutations[i] = threshold;
            }
        }
#endif
    }
}

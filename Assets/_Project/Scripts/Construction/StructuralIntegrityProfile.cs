using System;
using UnityEngine;

namespace Hecton8.Construction
{
    /// <summary>
    /// Authoring profile for structural rupture thresholds across supported habitat pipe materials.
    /// </summary>
    [CreateAssetMenu(fileName = "StructuralIntegrityProfile_", menuName = "Hecton8/Construction/Structural Integrity Profile", order = 22)]
    public sealed class StructuralIntegrityProfile : ScriptableObject
    {
        public const int DefaultRuptureDecalAtlasIndex = 1;

        public enum PipeMaterialVariant : byte
        {
            Glass = 0,
            Titanium = 1,
            Plasteel = 2
        }

        [Serializable]
        public struct VariantIntegrity
        {
            [SerializeField] private PipeMaterialVariant variant;
            [SerializeField, Min(0.1f)] private float maxUnsupportedSpan;
            [SerializeField, Min(1f)] private float baseHP;
            [SerializeField, Min(0)] private int ruptureDecalAtlasIndex;

            public VariantIntegrity(PipeMaterialVariant variant, float maxUnsupportedSpan, float baseHP, int ruptureDecalAtlasIndex)
            {
                this.variant = variant;
                this.maxUnsupportedSpan = maxUnsupportedSpan;
                this.baseHP = baseHP;
                this.ruptureDecalAtlasIndex = ruptureDecalAtlasIndex;
            }

            public PipeMaterialVariant Variant => variant;
            public float MaxUnsupportedSpan => maxUnsupportedSpan;
            public float BaseHP => baseHP;
            public int RuptureDecalAtlasIndex => ruptureDecalAtlasIndex;
        }

        [Header("Material Variants")]
        [Tooltip("Per-material structural thresholds and rupture decal atlas entries.")]
        [SerializeField] private VariantIntegrity[] variants =
        {
            new VariantIntegrity(PipeMaterialVariant.Glass, 8f, 45f, 0),
            new VariantIntegrity(PipeMaterialVariant.Titanium, 15f, 120f, 1),
            new VariantIntegrity(PipeMaterialVariant.Plasteel, 22f, 240f, 2)
        };

        public VariantIntegrity[] Variants => variants;
    }
}

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
        public enum PipeMaterialVariant : byte
        {
            Glass = 0,
            Titanium = 1,
            Plasteel = 2
        }

        [Serializable]
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct VariantIntegrity
        {
            [SerializeField] private PipeMaterialVariant variant;
            [SerializeField, Min(0.1f)] private float maxUnsupportedSpan;
            [SerializeField, Min(1f)] private float baseHP;

            public VariantIntegrity(PipeMaterialVariant variant, float maxUnsupportedSpan, float baseHP)
            {
                this.variant = variant;
                this.maxUnsupportedSpan = maxUnsupportedSpan;
                this.baseHP = baseHP;
            }

            public PipeMaterialVariant Variant => variant;
            public float MaxUnsupportedSpan => maxUnsupportedSpan;
            public float BaseHP => baseHP;
        }

        [Header("Material Variants")]
        [Tooltip("Per-material structural thresholds. Visual pressure aging is procedural in UberNoir, not decal-atlas driven.")]
        [SerializeField] private VariantIntegrity[] variants =
        {
            new VariantIntegrity(PipeMaterialVariant.Glass, 8f, 45f),
            new VariantIntegrity(PipeMaterialVariant.Titanium, 15f, 120f),
            new VariantIntegrity(PipeMaterialVariant.Plasteel, 22f, 240f)
        };

        public VariantIntegrity[] Variants => variants;
    }
}

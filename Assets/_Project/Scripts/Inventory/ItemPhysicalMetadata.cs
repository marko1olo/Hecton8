namespace Hecton8.Inventory
{
    using System;
    using Hecton8.Items;
    using UnityEngine;

    /// <summary>
    /// Stable item vulnerability bits consumed by first-party tool interaction gates.
    /// </summary>
    [Flags]
    public enum ItemVulnerabilityMask : uint
    {
        None = 0u,
        Cut = 1u << 0,
        Drill = 1u << 1,
        Grab = 1u << 2,
        Stun = 1u << 3,
        Burn = 1u << 4,
        Laser = 1u << 5,
        Bash = 1u << 6,
    }

    /// <summary>
    /// Compact impact-audio material family consumed by DSP collision synthesis.
    /// </summary>
    public enum ItemAudioMaterialId : byte
    {
        Organic = 0,
        Metal = 1,
        Glass = 2,
    }

    /// <summary>
    /// Stable world-physics material family used to bind shared PhysicMaterial assets on dropped items.
    /// </summary>
    public enum ItemPhysicsMaterialTag : byte
    {
        Default = 0,
        Organic = 1,
        Metal = 2,
        Glass = 3,
    }

    /// <summary>
    /// Cold-path physical metadata heuristics for item assets that have not been explicitly authored yet.
    /// </summary>
    public static class ItemPhysicalMetadataUtility
    {
        public static uint ResolveDefaultVulnerabilityMask(ItemCategory category, ResourceFamily resourceFamily, string persistentId)
        {
            string safeId = persistentId ?? string.Empty;
            if (IsGlassLikeId(safeId))
                return (uint)(ItemVulnerabilityMask.Grab | ItemVulnerabilityMask.Drill | ItemVulnerabilityMask.Laser | ItemVulnerabilityMask.Bash);

            switch (category)
            {
                case ItemCategory.Tool:
                case ItemCategory.Equipment:
                    return (uint)(ItemVulnerabilityMask.Grab | ItemVulnerabilityMask.Bash);

                case ItemCategory.Consumable:
                case ItemCategory.Organic:
                    return (uint)(ItemVulnerabilityMask.Grab | ItemVulnerabilityMask.Cut | ItemVulnerabilityMask.Burn | ItemVulnerabilityMask.Stun | ItemVulnerabilityMask.Laser | ItemVulnerabilityMask.Bash);

                case ItemCategory.Component:
                    return (uint)(ItemVulnerabilityMask.Grab | ItemVulnerabilityMask.Drill | ItemVulnerabilityMask.Laser | ItemVulnerabilityMask.Bash);
            }

            switch (resourceFamily)
            {
                case ResourceFamily.Organic:
                    return (uint)(ItemVulnerabilityMask.Grab | ItemVulnerabilityMask.Cut | ItemVulnerabilityMask.Burn | ItemVulnerabilityMask.Stun | ItemVulnerabilityMask.Laser | ItemVulnerabilityMask.Bash);

                case ResourceFamily.Chemical:
                    return (uint)(ItemVulnerabilityMask.Grab | ItemVulnerabilityMask.Burn | ItemVulnerabilityMask.Laser);

                case ResourceFamily.Crystal:
                case ResourceFamily.DeepMaterial:
                case ResourceFamily.StructuralMetal:
                case ResourceFamily.ElectronicsMetal:
                case ResourceFamily.Component:
                case ResourceFamily.Power:
                    return (uint)(ItemVulnerabilityMask.Grab | ItemVulnerabilityMask.Drill | ItemVulnerabilityMask.Laser | ItemVulnerabilityMask.Bash);
            }

            return (uint)(ItemVulnerabilityMask.Grab | ItemVulnerabilityMask.Bash);
        }

        public static ItemAudioMaterialId ResolveDefaultAudioMaterialId(
            ItemCategory category,
            ResourceFamily resourceFamily,
            string persistentId)
        {
            string safeId = persistentId ?? string.Empty;
            if (IsGlassLikeId(safeId))
                return ItemAudioMaterialId.Glass;

            switch (category)
            {
                case ItemCategory.Consumable:
                case ItemCategory.Organic:
                    return ItemAudioMaterialId.Organic;

                case ItemCategory.Tool:
                case ItemCategory.Equipment:
                case ItemCategory.Component:
                    return ItemAudioMaterialId.Metal;
            }

            switch (resourceFamily)
            {
                case ResourceFamily.Organic:
                case ResourceFamily.Chemical:
                    return ItemAudioMaterialId.Organic;

                case ResourceFamily.Crystal:
                    return ItemAudioMaterialId.Glass;

                case ResourceFamily.StructuralMetal:
                case ResourceFamily.ElectronicsMetal:
                case ResourceFamily.DeepMaterial:
                case ResourceFamily.Component:
                case ResourceFamily.Power:
                    return ItemAudioMaterialId.Metal;
            }

            return ItemAudioMaterialId.Metal;
        }

        public static float ResolveDefaultMassKg(float authoredWeight, int width, int height, ItemCategory category)
        {
            float footprintBias = Mathf.Max(1, width * height) * 0.15f;
            float resolvedMass = authoredWeight > 0f ? authoredWeight : footprintBias;
            if (category == ItemCategory.Tool || category == ItemCategory.Equipment)
                resolvedMass = Mathf.Max(resolvedMass, 0.35f);

            return Mathf.Max(0.05f, resolvedMass);
        }

        public static float ResolveDefaultVolumeM3(float resolvedMassKg, int width, int height, ItemCategory category)
        {
            float footprintCells = Mathf.Max(1, width * height);
            float baselineVolume = footprintCells * 0.0025f;
            float densityBias = category == ItemCategory.Tool || category == ItemCategory.Component
                ? 0.0014f
                : 0.0022f;
            return Mathf.Max(0.0005f, baselineVolume + Mathf.Max(0.05f, resolvedMassKg) * densityBias);
        }

        public static ItemPhysicsMaterialTag ResolveDefaultPhysicsMaterialTag(
            ItemCategory category,
            ResourceFamily resourceFamily,
            string persistentId)
        {
            switch (ResolveDefaultAudioMaterialId(category, resourceFamily, persistentId))
            {
                case ItemAudioMaterialId.Glass:
                    return ItemPhysicsMaterialTag.Glass;

                case ItemAudioMaterialId.Metal:
                    return ItemPhysicsMaterialTag.Metal;

                default:
                    return ItemPhysicsMaterialTag.Organic;
            }
        }

        public static bool IsOrganic(byte materialId)
        {
            return materialId == (byte)ItemAudioMaterialId.Organic;
        }

        public static bool IsMetal(byte materialId)
        {
            return materialId == (byte)ItemAudioMaterialId.Metal;
        }

        private static bool IsGlassLikeId(string persistentId)
        {
            return persistentId.IndexOf("Glass", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   persistentId.IndexOf("Crystal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   persistentId.IndexOf("Silica", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   persistentId.IndexOf("Lens", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}

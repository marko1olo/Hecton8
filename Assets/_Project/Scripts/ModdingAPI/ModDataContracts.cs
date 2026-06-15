using System;
using UnityEngine;

namespace Hecton8.Modding
{
    /// <summary>
    /// Base wrapper for JSON data overrides provided by mods.
    /// Mods can include a file named "overrides.json" or similar inside their "Data" folder.
    /// </summary>
    [Serializable]
    public class ModDataOverrideFile
    {
        public ModItemTemplateOverride[] ItemOverrides;
        // Expandable in the future for other systems:
        // public ModFaunaOverride[] FaunaOverrides;
        // public ModCraftingRecipeOverride[] RecipeOverrides;
    }

    [Serializable]
    public class ModItemTemplateOverride
    {
        public string TargetItemHashOrName;
        
        [Tooltip("If true, overrides the base durability.")]
        public bool OverrideDurability;
        public float BaseDurability;
        
        [Tooltip("If true, overrides the mass.")]
        public bool OverrideMassKg;
        public float MassKg;
        
        [Tooltip("If true, overrides the volume.")]
        public bool OverrideVolumeM3;
        public float VolumeM3;
        
        [Tooltip("If true, overrides max stack size.")]
        public bool OverrideMaxStackSize;
        public ushort MaxStackSize;
    }
}

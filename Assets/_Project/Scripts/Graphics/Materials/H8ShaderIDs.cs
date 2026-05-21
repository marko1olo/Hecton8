using UnityEngine;

namespace Hecton8.Graphics.Materials
{
    /// <summary>
    /// Cached shader property identifiers for Hecton-8 noir material paths.
    /// </summary>
    public static class H8ShaderIDs
    {
        /// <summary>Base albedo map.</summary>
        public static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        /// <summary>Packed metallic/AO/smoothness/emission mask.</summary>
        public static readonly int MaskMap = Shader.PropertyToID("_MaskMap");
        /// <summary>Tangent-space normal map.</summary>
        public static readonly int BumpMap = Shader.PropertyToID("_BumpMap");
        /// <summary>Runtime/global rust detail atlas.</summary>
        public static readonly int RustDetailMap = Shader.PropertyToID("_RustDetailMap");
        /// <summary>Blue-noise dither texture.</summary>
        public static readonly int BlueNoiseTex = Shader.PropertyToID("_BlueNoiseTex");
        /// <summary>SRP batcher base-map scale/offset.</summary>
        public static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");
        /// <summary>Rust detail atlas scale/offset.</summary>
        public static readonly int RustDetailMapSt = Shader.PropertyToID("_RustDetailMap_ST");
        /// <summary>Per-material base color.</summary>
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        /// <summary>Per-material emission color.</summary>
        public static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        /// <summary>Rust tint color.</summary>
        public static readonly int RustTint = Shader.PropertyToID("_RustTint");
        /// <summary>Deep corrosion pit tint color.</summary>
        public static readonly int RustPitTint = Shader.PropertyToID("_RustPitTint");
        /// <summary>Lower spectral bioluminescence color.</summary>
        public static readonly int BiolumLowColor = Shader.PropertyToID("_BiolumLowColor");
        /// <summary>Upper spectral bioluminescence color.</summary>
        public static readonly int BiolumHighColor = Shader.PropertyToID("_BiolumHighColor");
        /// <summary>Noir abyss floor minimum luminance color.</summary>
        public static readonly int NoirAbyssFloorColor = Shader.PropertyToID("_NoirAbyssFloorColor");
        /// <summary>Noir fog floor color.</summary>
        public static readonly int NoirFogColor = Shader.PropertyToID("_NoirFogColor");
        /// <summary>Feature enable vector for POM, reserved, bending, and dither.</summary>
        public static readonly int UberNoirFeatureFlags = Shader.PropertyToID("_UberNoirFeatureFlags");
        /// <summary>GraphicsBuffer instance offset/count/use/seed settings.</summary>
        public static readonly int UberNoirInstanceParams = Shader.PropertyToID("_UberNoirInstanceParams");
        /// <summary>Parallax occlusion mapping settings.</summary>
        public static readonly int UberNoirParallaxParams = Shader.PropertyToID("_UberNoirParallaxParams");
        /// <summary>Rust/corrosion settings.</summary>
        public static readonly int UberNoirRustParams = Shader.PropertyToID("_UberNoirRustParams");
        /// <summary>Hull and habitat bending settings.</summary>
        public static readonly int UberNoirBendParams = Shader.PropertyToID("_UberNoirBendParams");
        /// <summary>Bioluminescent pulse settings.</summary>
        public static readonly int UberNoirBiolumParams = Shader.PropertyToID("_UberNoirBiolumParams");
        /// <summary>Dithered transparency settings.</summary>
        public static readonly int UberNoirDitherParams = Shader.PropertyToID("_UberNoirDitherParams");
        /// <summary>Lighting scalar settings.</summary>
        public static readonly int UberNoirLightingParams = Shader.PropertyToID("_UberNoirLightingParams");
        /// <summary>Material metallic scalar.</summary>
        public static readonly int Metallic = Shader.PropertyToID("_Metallic");
        /// <summary>Material smoothness scalar.</summary>
        public static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        /// <summary>Material occlusion scalar.</summary>
        public static readonly int OcclusionStrength = Shader.PropertyToID("_OcclusionStrength");
        /// <summary>Normal strength scalar.</summary>
        public static readonly int BumpScale = Shader.PropertyToID("_BumpScale");
        /// <summary>Alpha cutout scalar.</summary>
        public static readonly int Cutoff = Shader.PropertyToID("_Cutoff");
        /// <summary>Noir fog alpha scalar.</summary>
        public static readonly int NoirFogAlpha = Shader.PropertyToID("_NoirFogAlpha");
        /// <summary>GraphicsBuffer containing H8 Uber Noir instance matrices and seeds.</summary>
        public static readonly int H8UberNoirInstanceData = Shader.PropertyToID("_H8UberNoirInstanceData");
        /// <summary>Global packed Beer-Lambert extinction LUT.</summary>
        public static readonly int ExtinctionLut = Shader.PropertyToID("_ExtinctionLUT");
        /// <summary>Global Beer-Lambert extinction LUT axis and enable parameters.</summary>
        public static readonly int ExtinctionLutParams = Shader.PropertyToID("_ExtinctionLUTParams");
        /// <summary>Global Beer-Lambert extinction runtime water state.</summary>
        public static readonly int ExtinctionLutRuntime = Shader.PropertyToID("_ExtinctionLUTRuntime");
        /// <summary>Global Beer-Lambert weather-driven turbidity shift.</summary>
        public static readonly int ExtinctionLutWeatherParams = Shader.PropertyToID("_ExtinctionLUTWeatherParams");
        /// <summary>AUP runtime-to-absolute offset.</summary>
        public static readonly int TotalUniverseOffset = Shader.PropertyToID("_TotalUniverseOffset");
        /// <summary>Global bioluminescence phase vector.</summary>
        public static readonly int BiolumMasterPhase = Shader.PropertyToID("_BiolumMasterPhase");
        /// <summary>Global base power brownout vector: supply, severity, phase, quality.</summary>
        public static readonly int HectonPowerBrownoutParams = Shader.PropertyToID("_HectonPowerBrownoutParams");
        /// <summary>Submarine crush center/radius vector.</summary>
        public static readonly int HectonSubmarineCrushCenterRadius = Shader.PropertyToID("_HectonSubmarineCrushCenterRadius");
        /// <summary>Submarine crush depth parameters.</summary>
        public static readonly int HectonSubmarineCrushDepthParams = Shader.PropertyToID("_HectonSubmarineCrushDepthParams");
        /// <summary>Habitat stress center/radius vector.</summary>
        public static readonly int HectonHabitatStressCenterRadius = Shader.PropertyToID("_HectonHabitatStressCenterRadius");
        /// <summary>Habitat stress scalar parameters.</summary>
        public static readonly int HectonHabitatStressParams = Shader.PropertyToID("_HectonHabitatStressParams");
        /// <summary>Runtime material decay vector.</summary>
        public static readonly int HectonMaterialDecayRuntime = Shader.PropertyToID("_HectonMaterialDecayRuntime");
        /// <summary>Global equipment rust scalar.</summary>
        public static readonly int HectonEquipmentRust01 = Shader.PropertyToID("_HectonEquipmentRust01");
        /// <summary>Player blood/stress splatter vector.</summary>
        public static readonly int HectonPlayerBloodSplatter = Shader.PropertyToID("_HectonPlayerBloodSplatter");
    }
}

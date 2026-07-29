// ============================================================================================
//  ForgeGeneratedMaterialAuthoring
//
//  WHAT THIS FIXES. Every package the offline Blender forge has produced ships with
//  `productionReady: false` and `manifestGaps` naming `materials` and `textures`
//  (Tools/Blender/h8forge/export_unity.py:1966-1973). `fd -g 'MAT_*Forge*'` over
//  Assets/_Project/Art returns zero: the forge emits geometry plus a manifest and NOTHING in
//  Unity ever created the shared `MAT_*` assets its own manifest names, so every forge FBX
//  renders with Unity's default material. The geometry is certified, the vertex-colour wear
//  channels are baked and measured, and the consuming shader exists - only the binding was
//  missing. This script is that binding.
//
//  WHAT IT DELIBERATELY DOES NOT DO.
//    * It does not and cannot flip `productionReady` to true. That field is computed inside
//      `h8forge.export_unity.write_manifest` (export_unity.py:2130) at Blender export time from
//      the `materials=`/`textures=` arguments the GENERATOR passes. A Unity-side asset has no
//      vote. Closing that gap is a change to Tools/Blender/generators/*.py, not to this file.
//    * It creates no texture. The bibles' required map stack
//      (`3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md:45-56`) is authored/AI-sourced tileable art
//      that does not exist in this project for rock, industrial steel, rubber or instrument
//      glass. `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md:27` rejects "flat procedural noise" as
//      material identity and `shaders.md:114` rejects "random noise as material identity", so
//      synthesising a placeholder set here would turn a red gate green while changing nothing
//      a player can see. The `textures` gap is left open on purpose.
//    * It writes no `.mat`, `.prefab`, `.unity` or `.asset` as text. `AGENTS.md` Evidence Law
//      ("YAML Serialization & Asset Integrity") makes Unity's serialiser their only legal
//      writer, so every mutation goes through AssetDatabase / PrefabUtility.
//
//  WHY NO TEXTURES IS STILL NOT GREY CLAY. `Hecton8/Construction/ModuleHardSurfaceLit` was
//  built as the consumer for the four hard-surface vertex-colour channels and gates every map
//  read behind `_ModuleSurfaceParams` (Hecton_ModuleHardSurfaceLit.shader:81, and the sibling
//  authoring script ModuleHardSurfaceWearMaterialAuthoring.cs:358-364 which proves the
//  zero-weight fallback is the intended path). With all four weights at 0 the surface response
//  comes from the baked channels plus scalars plus the existing procedural rust/silt breakup at
//  Hecton_ModuleHardSurfaceLit.shader:504-521. That is a real pressure-aged material read, not
//  a flat colour - but it is a floor, not the shipped look, and the missing map stack is
//  reported on every run.
//
//  CHANNEL CONTRACT THIS RELIES ON (`3dmodel.md:123-126`, ruled 2026-07-29):
//    hard surface / geologic: R = edge wear, G = oxidation, B = baked AO, A = emission/decal.
//    organic:                 R = sway amplitude, G = biolum mask/phase, B = baked AO,
//                             A = family-specific.
//  `Tools/Blender/h8forge/law.py:288-292` maps SurfaceClass.GEOLOGIC onto the HARD_SURFACE
//  contract, which is why geology and the small-prop tool share one master shader.
//
//  Every entry point is idempotent. Running Apply twice reports "already" and writes nothing.
// ============================================================================================
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Editor.Authoring
{
    /// <summary>
    /// Creates the shared <c>MAT_*</c> material assets the offline Blender forge manifests name
    /// but never produced, binds them to the correct HECTON-8 master shader, and (as a separate
    /// step) binds them in slot order onto <c>GEN_*</c> prefabs assembled from the forge FBX
    /// children.
    /// </summary>
    /// <remarks>
    /// Reachable from the menu and from <c>-executeMethod</c>. All three verbs are
    /// <c>public static void</c> with no parameters for that reason.
    /// </remarks>
    public static class ForgeGeneratedMaterialAuthoring
    {
        // ══════════════════════════════════════════════════════════
        //  ASCII REPORT TOKENS
        //  Build output on this host is localised to Russian, so "error"/"warning" greps miss.
        //  Every line this script emits starts with LogPrefix and every verdict uses one of the
        //  literals below, all plain ASCII, so the lead can grep them unambiguously.
        // ══════════════════════════════════════════════════════════

        private const string LogPrefix = "H8FORGEMAT";
        private const string TokenPass = "PASS";
        private const string TokenFail = "FAIL";
        private const string TokenBlocked = "BLOCKED";

        // ══════════════════════════════════════════════════════════
        //  PATHS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Forge package root written by the generators; every MANIFEST_*.json and MESH_*.fbx
        /// pair lives in a per-family subfolder of this.
        /// </summary>
        private const string ForgePackageRoot = "Assets/_Project/Art/Generated/Forge";

        /// <summary>
        /// First-party generated-material folder. `3DMODEL_TEXTURES_MATERIALS.md:157` requires
        /// generated materials to "live under first-party material folders", and
        /// Assets/_Project/Art/Materials/Generated already exists for exactly that.
        /// </summary>
        private const string ForgeMaterialRoot = "Assets/_Project/Art/Materials/Generated/Forge";

        private const string HardSurfaceShaderName = "Hecton8/Construction/ModuleHardSurfaceLit";
        private const string HardSurfaceShaderPath =
            "Assets/_Project/Art/Shaders/Hecton_ModuleHardSurfaceLit.shader";

        /// <summary>Organic master shader considered for the two Flora packages.</summary>
        private const string CoralShaderName = "Hecton8/Flora/CoralMaster";
        private const string CoralShaderPath =
            "Assets/_Project/Art/Shaders/Hecton_CoralMaster.shader";

        /// <summary>
        /// FAIL-CLOSED OPT-IN MARKER, and the direction of the failure is the whole point.
        ///
        /// Measured 2026-07-29 in Hecton_CoralMaster.shader: `:441` reads
        /// <c>saturate(input.color.r) * _VertexTintStrength</c> - channel R, which the forge
        /// writes as SWAY AMPLITUDE, consumed as a colour tint - and `:442` reads
        /// <c>moisture = saturate(input.color.g)</c> - channel G, which the forge writes as the
        /// BIOLUMINESCENCE mask, consumed as wetness. `:516` does read B as AO correctly, so the
        /// shader is half-migrated, not untouched. Binding the two organic forge packages to it
        /// today would produce a plausible-looking render driven by two swapped channels, which
        /// is the exact silent-degeneracy failure this project keeps paying for.
        ///
        /// A negative probe ("does the bad line still exist?") fails OPEN the moment somebody
        /// renames a local, so this gate is positive instead: the organic binding stays refused
        /// until the shader source contains this literal token, placed by whoever lands the
        /// channel fix. It cannot pass by accident and it clears itself when the fix lands.
        /// </summary>
        private const string OrganicContractOptInToken = "H8_ORGANIC_VCOL_CONTRACT_OK";

        /// <summary>
        /// The two non-compliant reads, quoted so the report is actionable rather than merely
        /// negative. Informational only - the opt-in token above is the gate.
        /// </summary>
        private static readonly string[] OrganicKnownBadReads =
        {
            "saturate(input.color.r) * _VertexTintStrength",
            "moisture = saturate(input.color.g)"
        };

        // ══════════════════════════════════════════════════════════
        //  LOD GROUP CONSTANTS
        //  Reused verbatim from HectonFBXPostprocessor.cs:31-34 rather than invented here, so the
        //  explicitly authored chain and the importer's own fallback cannot disagree.
        //  fadeMode/animateCrossFading come from the manifest's unityImport.lodGroup block
        //  (export_unity.py:1672-1673): CrossFade with animateCrossFading=false is the DITHERED
        //  path, which is what 3dmodel.md section 7 permits for dense flora/coral. Alpha blend is
        //  banned there.
        // ══════════════════════════════════════════════════════════

        private const float Lod0ScreenRelativeHeight = 0.6f;
        private const float Lod1ScreenRelativeHeight = 0.15f;
        private const float Lod2ScreenRelativeHeight = 0.04f;
        private const float LodFadeWidth = 0.12f;

        // ══════════════════════════════════════════════════════════
        //  SURFACE CLASS
        // ══════════════════════════════════════════════════════════

        private enum ForgeSurfaceClass
        {
            /// <summary>law.py:289/291 - SMALL_PROP and GEOLOGY both take HARD_SURFACE_VCOL.</summary>
            HardSurfaceOrGeologic,

            /// <summary>law.py:290 - FLORA takes ORGANIC_VCOL.</summary>
            Organic
        }

        // ══════════════════════════════════════════════════════════
        //  SLOT ROLE TABLE
        //
        //  WHY A TABLE AND NOT A MANIFEST PARSE. Measured across the eight schema-1 manifests:
        //  the slot declaration lives in THREE mutually incompatible shapes -
        //    geology  -> extra.materialFamily, a string ARRAY of MAT_ names
        //                (MANIFEST_Geology_boulder_*.json "materialFamily")
        //    coral    -> uvSummary.materialSlots, a string ARRAY, plus uvSummary.materialSlotRoles
        //    capstem  -> the top-level `materials` array of {name} objects
        //    drill    -> nowhere; the manifest declares 4 submeshes and NO slot names at all
        //  and geology additionally carries extra.materialSlots as a JSON OBJECT keyed by the
        //  numeric strings "0".."3", which UnityEngine.JsonUtility cannot deserialise at all.
        //  Hand-rolling a JSON reader inside an authoring script to paper over that is worse than
        //  a cited table, so the roles are literals here with the owning generator line beside
        //  them, and the SUBMESH COUNT of the imported mesh is asserted against the table on every
        //  run. A drift between generator and table therefore fails loudly instead of silently
        //  binding the wrong number of slots. Same discipline as
        //  ModuleHardSurfaceWearMaterialAuthoring.cs:93-99.
        // ══════════════════════════════════════════════════════════

        private sealed class ForgePackage
        {
            public string ManifestFileName;
            public string FbxAssetPath;
            /// <summary>identity.family, which is also the {family} token of law.NAME_MATERIAL.</summary>
            public string Family;
            public ForgeSurfaceClass Surface;
            /// <summary>Slot roles in submesh order. Length must equal the mesh submesh count.</summary>
            public string[] SlotRoles;
            /// <summary>Source of truth for SlotRoles, quoted in the report.</summary>
            public string SlotRoleSource;
            /// <summary>True when the FBX carries a COL_ convex proxy child.</summary>
            public bool HasColliderProxy;
            /// <summary>
            /// manifest identity.scaleMeters. Used only to report the real metres-per-tile this
            /// asset ends up with under the family's shared tiling, so a texel-density mismatch
            /// across a shared material is a printed number rather than a surprise.
            /// </summary>
            public float AssetScaleMetres;

            public ForgePackage(
                string manifestFileName,
                string fbxAssetPath,
                string family,
                ForgeSurfaceClass surface,
                string[] slotRoles,
                string slotRoleSource,
                bool hasColliderProxy,
                float assetScaleMetres)
            {
                AssetScaleMetres = assetScaleMetres;
                ManifestFileName = manifestFileName;
                FbxAssetPath = fbxAssetPath;
                Family = family;
                Surface = surface;
                SlotRoles = slotRoles;
                SlotRoleSource = slotRoleSource;
                HasColliderProxy = hasColliderProxy;
            }
        }

        private static readonly string[] GeologySlotRoles = { "Primary", "FractureFace", "MineralVein" };
        private static readonly string[] DrillSlotRoles =
        {
            "PaintedCasing", "BareMetalEdge", "RubberGasket", "InstrumentGlass"
        };
        private static readonly string[] CapStemSlotRoles = { "CapTissue", "TornEdge", "StemHoldfast" };
        private static readonly string[] CoralSlotRoles = { "Tissue", "ExposedTipSkeleton", "EncrustingBase" };

        // ══════════════════════════════════════════════════════════
        //  TEXTURE SETS - existing project art, reused, not generated
        //
        //  MEASURED 2026-07-29. `3DMODEL_TEXTURES_MATERIALS.md:17` orders it: "Generated meshes
        //  must use existing high-quality human-authored or AI-assisted texture assets WHEN
        //  AVAILABLE. Synthetic flat colors are allowed only as validator/debug placeholders."
        //  They are available. A sweep of Assets/_Project/Art/TEXTURES found 703 TX_* image files,
        //  and the project already ships a consistent five-map set per material:
        //      *_BaseColor.jpg|png            -> albedo, sRGB
        //      *_NormalGL.jpg|png             -> tangent normal, OpenGL Y+ (Unity's convention)
        //      *_MaskMap_UnityURP.png         -> URP packing R=Metallic G=Occlusion A=Smoothness
        //      *_ARM_AO_Rough_Metal.jpg|png   -> the alternative ARM packing, NOT used here
        //      *_Height.jpg|png               -> greyscale height, .g is the channel URP reads
        //  Sixty-nine materials already consume `_MaskMap_UnityURP`, so that packing is the de
        //  facto project standard and it is EXACTLY what Hecton_ModuleHardSurfaceLit decodes -
        //  its own property label reads "Packed Mask (R Metallic G Occlusion A Smoothness)"
        //  (Hecton_ModuleHardSurfaceLit.shader:71) and it samples .r/.g/.a in that order at
        //  :349-353. No repacking, no conversion, no new art.
        //
        //  DECLARED DEVIATION, because the bible requires it to be declared rather than guessed:
        //  `3DMODEL_TEXTURES_MATERIALS.md:44-49` gives the DEFAULT packed mask as
        //  R=Metallic G=Roughness-or-Smoothness B=AO A=Emission. This set is URP's
        //  R=Metallic G=Occlusion A=Smoothness instead - AO in G, smoothness in A, B unused.
        //  Section 3 line 46 permits it: "G = Roughness or smoothness according to shader
        //  contract. The manifest must state which one." The shader contract states it, this
        //  comment states it, and the report prints it on every run.
        //
        //  Sets are chosen by SURFACE TRUTH, not by convenience:
        //    Geology Primary      -> photic limestone rubble shelf. The forge rock's own manifest
        //                            says geologicalProcessTag "sedimentary" and biomeDepthRoute
        //                            "photic shallows to medium depth"; limestone rubble shelf is
        //                            the only set that is both sedimentary AND photic. Basalt
        //                            (also present) is igneous and would contradict the manifest.
        //    Geology FractureFace -> serpentinite FAULT rock. A fault surface is a fracture face.
        //    Geology MineralVein  -> hydrothermal vent mineral crust. Mineral banding, oxidised
        //                            rims - `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md:101`.
        //    SmallProp Casing     -> "clean nasa punk tool housing metal". Named for this job.
        //    SmallProp BareMetal  -> brushed titanium, the bare machined finish a chamfer reveals.
        //    SmallProp Gasket     -> "rubber gasket ring trim sheet". Named for this job.
        //    SmallProp Glass      -> NOTHING. No instrument-glass or dark-pane source exists in
        //                            the project. Binding a tiling metal albedo to a readout pane
        //                            would be a lie that a gate could not see, so that role stays
        //                            textureless with its map weights at 0 and is reported as the
        //                            one genuine art gap.
        // ══════════════════════════════════════════════════════════

        private const string TexRootB34 =
            "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases/Batch20260608_TextureExpansion/Tiles/";
        private const string TexRootMicroPanel =
            "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases/Batch20260607_MicroPanel/Tiles/";
        private const string TexRootBiome =
            "Assets/_Project/Art/TEXTURES/Generated/GeminiBiomeMaterialIntake_20260607/Materials/";

        /// <summary>
        /// One existing five-map material set. <see cref="TileMetres"/> is the real-world size of
        /// one tile of this source art, which is what turns a normalised UV0 unwrap into a
        /// calibrated texel density.
        /// </summary>
        private sealed class RoleTextureSet
        {
            public readonly string BaseColor;
            public readonly string NormalGL;
            public readonly string MaskUnityUrp;
            public readonly string Height;
            public readonly float TileMetres;
            public readonly string SourceNote;

            public RoleTextureSet(
                string baseColor, string normalGl, string maskUnityUrp, string height,
                float tileMetres, string sourceNote)
            {
                BaseColor = baseColor;
                NormalGL = normalGl;
                MaskUnityUrp = maskUnityUrp;
                Height = height;
                TileMetres = tileMetres;
                SourceNote = sourceNote;
            }
        }

        private static RoleTextureSet GeminiBiomeSet(string material, float tileMetres, string note)
        {
            string folder = TexRootBiome + material + "/";
            // Note the prefix split in this batch: BaseColor is TX_GB_, the other four are TX_GM_.
            return new RoleTextureSet(
                folder + "TX_GB_" + material + "_BaseColor.jpg",
                folder + "TX_GM_" + material + "_NormalGL.jpg",
                folder + "TX_GM_" + material + "_MaskMap_UnityURP.png",
                folder + "TX_GM_" + material + "_Height.jpg",
                tileMetres, note);
        }

        private static RoleTextureSet Batch34Set(string tile, float tileMetres, string note)
        {
            string material = "gemini_Batch20260608_TextureExpansion_" + tile;
            string folder = TexRootB34 + material + "/";
            return new RoleTextureSet(
                folder + "TX_B34_" + material + "_BaseColor.jpg",
                folder + "TX_B34_" + material + "_NormalGL.jpg",
                folder + "TX_B34_" + material + "_MaskMap_UnityURP.png",
                folder + "TX_B34_" + material + "_Height.jpg",
                tileMetres, note);
        }

        private static RoleTextureSet MicroPanelSet(string tile, float tileMetres, string note)
        {
            string material = "gemini_Batch20260607_MicroPanel_" + tile;
            string folder = TexRootMicroPanel + material + "/";
            // This batch is PNG throughout, unlike the two JPG batches above.
            return new RoleTextureSet(
                folder + "TX_GM_" + material + "_BaseColor.png",
                folder + "TX_GM_" + material + "_NormalGL.png",
                folder + "TX_GM_" + material + "_MaskMap_UnityURP.png",
                folder + "TX_GM_" + material + "_Height.png",
                tileMetres, note);
        }

        /// <summary>
        /// Resolves the source set for one family/role, or null when the role is deliberately
        /// textureless. Keyed on the same literals the slot-role table uses.
        /// </summary>
        private static RoleTextureSet ResolveTextureSet(string family, string role)
        {
            if (string.Equals(family, "Geology", StringComparison.Ordinal))
            {
                // 1.25 m per tile is the forge's OWN declared figure for this family
                // (manifest uvSummary.triplanarMetresPerTile), so the source art is calibrated to
                // the number the generator already published rather than to a fresh guess.
                switch (role)
                {
                    case "Primary":
                        return Batch34Set("b34_3401_photic_limestone_rubble_shelf", 1.25f,
                            "sedimentary + photic, matches manifest geologicalProcessTag and biomeDepthRoute");
                    case "FractureFace":
                        return Batch34Set("b34_3406_serpentinite_fault_rock", 1.25f,
                            "fault rock = a fracture surface");
                    case "MineralVein":
                        return GeminiBiomeSet("gemini_biome_20260607_hydrothermal_vent_mineral_crust", 1.25f,
                            "mineral banding and oxidised rims, playbook section 4 geology row");
                }

                return null;
            }

            if (string.Equals(family, "SmallProp", StringComparison.Ordinal))
            {
                // 2.0 m per tile: `3DMODEL_EQUIPMENT_PROPS.md:59` sets handheld hero tools at
                // 1024 px/m, and these sources are 2048 px, so 2048 / 1024 = 2.0 m of surface per
                // tile is the density the bible asks for. Derived, not picked.
                switch (role)
                {
                    case "PaintedCasing":
                        return new RoleTextureSet(
                            "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialIntake_20260607/Singles/gemini_20260607_clean_nasa_punk_tool_housing_metal/TX_GM_gemini_20260607_clean_nasa_punk_tool_housing_metal_BaseColor.jpg",
                            "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialIntake_20260607/Singles/gemini_20260607_clean_nasa_punk_tool_housing_metal/TX_GM_gemini_20260607_clean_nasa_punk_tool_housing_metal_NormalGL.jpg",
                            "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialIntake_20260607/Singles/gemini_20260607_clean_nasa_punk_tool_housing_metal/TX_GM_gemini_20260607_clean_nasa_punk_tool_housing_metal_MaskMap_UnityURP.png",
                            "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialIntake_20260607/Singles/gemini_20260607_clean_nasa_punk_tool_housing_metal/TX_GM_gemini_20260607_clean_nasa_punk_tool_housing_metal_Height.jpg",
                            2.0f, "NASA-punk tool housing, authored for exactly this surface");
                    case "BareMetalEdge":
                        return MicroPanelSet("brushed_titanium", 2.0f,
                            "the bare machined finish a chamfer reveals under stripped paint");
                    case "RubberGasket":
                        return Batch34Set("b34_3414_rubber_gasket_ring_trim_sheet", 2.0f,
                            "rubber gasket ring trim sheet, authored for exactly this surface");
                    case "InstrumentGlass":
                        // Deliberately null. See the block comment above.
                        return null;
                }

                return null;
            }

            return null;
        }

        private static readonly ForgePackage[] Packages =
        {
            new ForgePackage(
                "MANIFEST_Geology_boulder_sedimentary_s1713_q100.json",
                ForgePackageRoot + "/Geology/MESH_Geology_boulder_sedimentary_s1713_q100.fbx",
                "Geology", ForgeSurfaceClass.HardSurfaceOrGeologic, GeologySlotRoles,
                "manifest extra.materialFamily; roles match Tools/Blender/generators/rock.py",
                true, 0.8f),
            new ForgePackage(
                "MANIFEST_Geology_cliffchunk_sedimentary_s1713_q100.json",
                ForgePackageRoot + "/Geology/MESH_Geology_cliffchunk_sedimentary_s1713_q100.fbx",
                "Geology", ForgeSurfaceClass.HardSurfaceOrGeologic, GeologySlotRoles,
                "manifest extra.materialFamily; roles match Tools/Blender/generators/rock.py",
                true, 7.6f),
            new ForgePackage(
                "MANIFEST_Geology_outcrop_sedimentary_s1713_q100.json",
                ForgePackageRoot + "/Geology/MESH_Geology_outcrop_sedimentary_s1713_q100.fbx",
                "Geology", ForgeSurfaceClass.HardSurfaceOrGeologic, GeologySlotRoles,
                "manifest extra.materialFamily; roles match Tools/Blender/generators/rock.py",
                true, 2.9f),
            new ForgePackage(
                "MANIFEST_SmallProp_Tool_SeafloorDrill_1712.json",
                ForgePackageRoot + "/SmallProp/MESH_SmallProp_Tool_SeafloorDrill_1712.fbx",
                "SmallProp", ForgeSurfaceClass.HardSurfaceOrGeologic, DrillSlotRoles,
                "Tools/Blender/generators/prop_handtool.py:602-607 MATERIAL_ROLES (the manifest declares none)",
                true, 0.255f),
            new ForgePackage(
                "MANIFEST_SmallProp_Tool_SeafloorDrill_2611.json",
                ForgePackageRoot + "/SmallProp/MESH_SmallProp_Tool_SeafloorDrill_2611.fbx",
                "SmallProp", ForgeSurfaceClass.HardSurfaceOrGeologic, DrillSlotRoles,
                "Tools/Blender/generators/prop_handtool.py:602-607 MATERIAL_ROLES (the manifest declares none)",
                true, 0.255f),
            new ForgePackage(
                "MANIFEST_Flora_CapStem_1811_00.json",
                ForgePackageRoot + "/Flora/MESH_Flora_CapStem_1811_00.fbx",
                "Flora", ForgeSurfaceClass.Organic, CapStemSlotRoles,
                "Tools/Blender/generators/flora_capstem.py:283 MATERIAL_ROLES",
                false, 0.50172f),
            new ForgePackage(
                "MANIFEST_Flora_Coral_Branching_1712.json",
                ForgePackageRoot + "/Flora/MESH_Flora_Coral_Branching_1712.fbx",
                "Flora", ForgeSurfaceClass.Organic, CoralSlotRoles,
                "manifest uvSummary.materialSlots + materialSlotRoles",
                false, 0.55f)
        };

        // ══════════════════════════════════════════════════════════
        //  ENTRY POINTS - the Verify / Apply / Verify trio
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Step 1 of 3. Read-only. Reports, per forge package: whether the FBX imported, how many
        /// submeshes each LOD mesh actually has against the declared slot count, whether the
        /// vertex-colour stream is present and non-degenerate, which master shader is the correct
        /// consumer, and which <c>MAT_*</c> assets are missing. Writes nothing.
        /// </summary>
        [MenuItem("Hecton8/Authoring/Forge Materials/1 - Verify Forge Material Gaps", priority = 230)]
        public static void VerifyForgeMaterialGaps()
        {
            StringBuilder report = NewReport("VERIFY-GAPS");
            AuditResult audit = Audit(report);
            report.Append(LogPrefix).Append(" RESULT ")
                  .Append(audit.PackagesReady > 0 ? TokenPass : TokenFail)
                  .Append(" phase=VERIFY-GAPS packages=").Append(audit.PackagesTotal)
                  .Append(" importable=").Append(audit.PackagesReady)
                  .Append(" materialsExpected=").Append(audit.MaterialsExpected)
                  .Append(" materialsPresent=").Append(audit.MaterialsPresent)
                  .Append(" materialsMissing=").Append(audit.MaterialsExpected - audit.MaterialsPresent)
                  .Append(" blockedPackages=").Append(audit.PackagesBlocked)
                  .Append(" texturesBound=").Append(audit.TexturesBound)
                  .Append(" textureSetsAvailable=6 textureSetsMissing=1(InstrumentGlass)");
            Emit(report, audit.PackagesReady > 0);
        }

        /// <summary>
        /// Step 2 of 3. Creates every missing <c>MAT_&lt;Family&gt;_&lt;Role&gt;</c> asset on the
        /// correct master shader and re-asserts the tuned property block on the ones that already
        /// exist. Touches no prefab, no scene and no FBX importer. Refuses any package whose
        /// surface class has no compliant consumer shader.
        /// </summary>
        [MenuItem("Hecton8/Authoring/Forge Materials/2 - Apply Forge Master Materials", priority = 231)]
        public static void ApplyForgeMasterMaterials()
        {
            StringBuilder report = NewReport("APPLY-MATERIALS");

            Shader hardSurface = ResolveShader(HardSurfaceShaderName, HardSurfaceShaderPath);
            if (hardSurface == null)
            {
                report.Append("  ").Append(TokenFail).Append(" master shader not found by name '")
                      .Append(HardSurfaceShaderName).Append("' nor at ").Append(HardSurfaceShaderPath)
                      .AppendLine();
                report.Append(LogPrefix).Append(" RESULT ").Append(TokenFail)
                      .Append(" phase=APPLY-MATERIALS created=0 updated=0 reason=SHADER_MISSING");
                Emit(report, false);
                return;
            }

            OrganicGate organic = EvaluateOrganicGate();
            AppendOrganicGate(report, organic);

            if (!EnsureFolder(ForgeMaterialRoot))
            {
                report.Append("  ").Append(TokenFail).Append(" could not create folder ")
                      .Append(ForgeMaterialRoot).AppendLine();
                report.Append(LogPrefix).Append(" RESULT ").Append(TokenFail)
                      .Append(" phase=APPLY-MATERIALS created=0 updated=0 reason=FOLDER_CREATE_FAILED");
                Emit(report, false);
                return;
            }

            int created = 0;
            int updated = 0;
            int refused = 0;
            // COLD ALLOC: HashSet<string>[32] - dedupes the three geology packages that share one
            // material set - owner: ForgeGeneratedMaterialAuthoring
            HashSet<string> handled = new HashSet<string>(32, StringComparer.Ordinal);

            for (int i = 0; i < Packages.Length; i++)
            {
                ForgePackage package = Packages[i];

                if (package.Surface == ForgeSurfaceClass.Organic && !organic.Allowed)
                {
                    refused += package.SlotRoles.Length;
                    report.Append("  ").Append(TokenBlocked).Append(' ').Append(package.Family)
                          .Append(' ').Append(System.IO.Path.GetFileName(package.FbxAssetPath))
                          .Append(" slots=").Append(package.SlotRoles.Length)
                          .Append(" reason=ORGANIC_SHADER_CHANNEL_CONTRACT_UNPROVEN")
                          .AppendLine();
                    continue;
                }

                string familyFolder = ForgeMaterialRoot + "/" + package.Family;
                if (!EnsureFolder(familyFolder))
                {
                    report.Append("  ").Append(TokenFail).Append(" folder ").Append(familyFolder)
                          .AppendLine();
                    continue;
                }

                for (int slot = 0; slot < package.SlotRoles.Length; slot++)
                {
                    string materialName = MaterialName(package.Family, package.SlotRoles[slot]);
                    if (!handled.Add(materialName))
                        continue;

                    string materialPath = familyFolder + "/" + materialName + ".mat";
                    Material existing = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

                    if (existing == null)
                    {
                        Material fresh = new Material(hardSurface) { name = materialName };
                        ApplyRoleProperties(fresh, package.Family, package.SlotRoles[slot]);
                        AssetDatabase.CreateAsset(fresh, materialPath);
                        created++;
                        report.Append("  CREATE   ").Append(materialPath)
                              .Append(" shader=").Append(hardSurface.name).AppendLine();
                        continue;
                    }

                    if (existing.shader != hardSurface)
                    {
                        // Assigning material.shader drops every property the new shader does not
                        // declare, so it is done before the property write, never after. Same
                        // ordering constraint ModuleHardSurfaceWearMaterialAuthoring.cs:260-264
                        // documents on the module migrator.
                        existing.shader = hardSurface;
                    }

                    ApplyRoleProperties(existing, package.Family, package.SlotRoles[slot]);
                    EditorUtility.SetDirty(existing);
                    updated++;
                    report.Append("  UPDATE   ").Append(materialPath)
                          .Append(" shader=").Append(hardSurface.name).AppendLine();
                }
            }

            if (created > 0 || updated > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            AppendTextureTruth(report);

            report.Append(LogPrefix).Append(" RESULT ")
                  .Append(created + updated > 0 ? TokenPass : TokenFail)
                  .Append(" phase=APPLY-MATERIALS created=").Append(created)
                  .Append(" updated=").Append(updated)
                  .Append(" refusedSlots=").Append(refused)
                  .Append(" root=").Append(ForgeMaterialRoot);
            Emit(report, created + updated > 0);
        }

        /// <summary>
        /// Optional step 2b. Assembles or refreshes one <c>GEN_*</c> prefab per forge package from
        /// the imported FBX children: an explicit LODGroup over the <c>_LOD&lt;n&gt;</c> renderers,
        /// the shared <c>MAT_*</c> assets in submesh order, and the <c>COL_</c> proxy as a convex
        /// MeshCollider on its own child. Split out from step 2 so the lead can stage the
        /// materials without also staging prefabs.
        /// </summary>
        [MenuItem("Hecton8/Authoring/Forge Materials/2b - Bind Forge Prefab Slots", priority = 232)]
        public static void ApplyForgeMaterialPrefabBinding()
        {
            StringBuilder report = NewReport("APPLY-PREFABS");
            OrganicGate organic = EvaluateOrganicGate();
            AppendOrganicGate(report, organic);

            int written = 0;
            int skipped = 0;

            for (int i = 0; i < Packages.Length; i++)
            {
                ForgePackage package = Packages[i];
                if (package.Surface == ForgeSurfaceClass.Organic && !organic.Allowed)
                {
                    skipped++;
                    report.Append("  ").Append(TokenBlocked).Append(' ')
                          .Append(System.IO.Path.GetFileName(package.FbxAssetPath))
                          .Append(" reason=ORGANIC_SHADER_CHANNEL_CONTRACT_UNPROVEN").AppendLine();
                    continue;
                }

                GameObject imported = AssetDatabase.LoadAssetAtPath<GameObject>(package.FbxAssetPath);
                if (imported == null)
                {
                    skipped++;
                    report.Append("  ").Append(TokenFail).Append(" FBX not imported: ")
                          .Append(package.FbxAssetPath).AppendLine();
                    continue;
                }

                Material[] slotMaterials = LoadSlotMaterials(package);
                if (slotMaterials == null)
                {
                    skipped++;
                    report.Append("  ").Append(TokenBlocked).Append(' ')
                          .Append(System.IO.Path.GetFileName(package.FbxAssetPath))
                          .Append(" reason=MATERIALS_MISSING_RUN_STEP_2").AppendLine();
                    continue;
                }

                if (BuildPackagePrefab(package, imported, slotMaterials, report))
                    written++;
                else
                    skipped++;
            }

            if (written > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            report.Append(LogPrefix).Append(" RESULT ").Append(written > 0 ? TokenPass : TokenFail)
                  .Append(" phase=APPLY-PREFABS written=").Append(written)
                  .Append(" skipped=").Append(skipped);
            Emit(report, written > 0);
        }

        /// <summary>
        /// Step 3 of 3. Strict post-flight gate. Re-runs the same audit as step 1 and additionally
        /// asserts that every expected material exists, sits on the expected master shader, has all
        /// four map weights consistent with the textures actually bound, and - where a
        /// <c>GEN_*</c> prefab exists - that every renderer slot resolves to a shared
        /// <c>MAT_*</c> asset. Emits exactly one greppable RESULT line.
        /// </summary>
        [MenuItem("Hecton8/Authoring/Forge Materials/3 - Verify Forge Material Bindings", priority = 233)]
        public static void VerifyForgeMaterialBindings()
        {
            StringBuilder report = NewReport("VERIFY-BINDINGS");
            AuditResult audit = Audit(report);

            int expectedBindable = 0;
            for (int i = 0; i < Packages.Length; i++)
            {
                if (Packages[i].Surface != ForgeSurfaceClass.Organic)
                    expectedBindable += Packages[i].SlotRoles.Length;
            }

            bool pass = audit.MaterialsPresent >= audit.UniqueMaterialsBindable &&
                        audit.MaterialsOnWrongShader == 0 &&
                        audit.SubmeshMismatches == 0 &&
                        audit.PrefabSlotFailures == 0 &&
                        audit.TextureSetResolveFailures == 0;

            AppendTextureTruth(report);

            report.Append(LogPrefix).Append(" RESULT ").Append(pass ? TokenPass : TokenFail)
                  .Append(" phase=VERIFY-BINDINGS packages=").Append(audit.PackagesTotal)
                  .Append(" bindableSlots=").Append(expectedBindable)
                  .Append(" uniqueMaterialsBindable=").Append(audit.UniqueMaterialsBindable)
                  .Append(" materialsPresent=").Append(audit.MaterialsPresent)
                  .Append(" wrongShader=").Append(audit.MaterialsOnWrongShader)
                  .Append(" submeshMismatch=").Append(audit.SubmeshMismatches)
                  .Append(" prefabSlotFailures=").Append(audit.PrefabSlotFailures)
                  .Append(" organicBlocked=").Append(audit.PackagesBlocked)
                  .Append(" textureSetResolveFailures=").Append(audit.TextureSetResolveFailures)
                  .Append(" texturesBound=").Append(audit.TexturesBound)
                  .Append(" productionReadyFlippable=NO-OWNED-BY-BLENDER-EXPORTER");
            Emit(report, pass);
        }

        // ══════════════════════════════════════════════════════════
        //  AUDIT
        // ══════════════════════════════════════════════════════════

        private struct AuditResult
        {
            public int PackagesTotal;
            public int PackagesReady;
            public int PackagesBlocked;
            public int MaterialsExpected;
            public int UniqueMaterialsBindable;
            public int MaterialsPresent;
            public int MaterialsOnWrongShader;
            public int SubmeshMismatches;
            public int PrefabSlotFailures;
            public int TexturesBound;
            public int TextureSetResolveFailures;
        }

        private static AuditResult Audit(StringBuilder report)
        {
            AuditResult result = default;
            OrganicGate organic = EvaluateOrganicGate();
            AppendOrganicGate(report, organic);

            Shader hardSurface = ResolveShader(HardSurfaceShaderName, HardSurfaceShaderPath);
            report.Append("  master hardSurface shader=")
                  .Append(hardSurface != null ? hardSurface.name : "MISSING").AppendLine();

            // COLD ALLOC: HashSet<string>[32] - material-name dedupe across shared families -
            // owner: ForgeGeneratedMaterialAuthoring
            HashSet<string> seenMaterials = new HashSet<string>(32, StringComparer.Ordinal);

            for (int i = 0; i < Packages.Length; i++)
            {
                ForgePackage package = Packages[i];
                result.PackagesTotal++;

                string fbxName = System.IO.Path.GetFileName(package.FbxAssetPath);
                GameObject imported = AssetDatabase.LoadAssetAtPath<GameObject>(package.FbxAssetPath);

                report.Append("  PKG ").Append(package.Family).Append(' ').Append(fbxName)
                      .Append(" surface=").Append(package.Surface == ForgeSurfaceClass.Organic
                          ? "Organic" : "HardSurface/Geologic")
                      .Append(" declaredSlots=").Append(package.SlotRoles.Length)
                      .Append(" imported=").Append(imported != null ? "YES" : "NO")
                      .AppendLine();
                report.Append("      slotRoleSource: ").Append(package.SlotRoleSource).AppendLine();

                if (imported == null)
                {
                    report.Append("      ").Append(TokenFail)
                          .Append(" FBX absent from the AssetDatabase; nothing can bind to it.")
                          .AppendLine();
                }
                else
                {
                    result.PackagesReady++;
                    AuditImportedMesh(package, imported, report, ref result);
                    AuditExistingPrefab(package, report, ref result);
                }

                if (package.Surface == ForgeSurfaceClass.Organic && !organic.Allowed)
                    result.PackagesBlocked++;

                for (int slot = 0; slot < package.SlotRoles.Length; slot++)
                {
                    string materialName = MaterialName(package.Family, package.SlotRoles[slot]);
                    if (!seenMaterials.Add(materialName))
                        continue;

                    result.MaterialsExpected++;
                    bool bindable = package.Surface != ForgeSurfaceClass.Organic;
                    if (bindable)
                        result.UniqueMaterialsBindable++;

                    string materialPath =
                        ForgeMaterialRoot + "/" + package.Family + "/" + materialName + ".mat";
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

                    if (material == null)
                    {
                        report.Append("      slot").Append(slot).Append(' ').Append(materialName)
                              .Append(" MISSING at ").Append(materialPath).AppendLine();
                        continue;
                    }

                    result.MaterialsPresent++;
                    string shaderName = material.shader != null ? material.shader.name : "<null>";
                    bool wrongShader = bindable &&
                        !string.Equals(shaderName, HardSurfaceShaderName, StringComparison.Ordinal);
                    if (wrongShader)
                        result.MaterialsOnWrongShader++;

                    int bound = CountBoundTextures(material);
                    result.TexturesBound += bound;

                    report.Append("      slot").Append(slot).Append(' ').Append(materialName)
                          .Append(" shader=").Append(shaderName)
                          .Append(wrongShader ? " " + TokenFail + "-WRONG-SHADER" : string.Empty)
                          .Append(" boundTextures=").Append(bound).Append("/4")
                          .Append(" instancing=").Append(material.enableInstancing ? "on" : "off")
                          .AppendLine();

                    AuditRoleTextureSet(package, slot, report, ref result);
                }
            }

            return result;
        }

        /// <summary>
        /// Proves the hardcoded source-set paths still resolve, and reports the import state of each
        /// map. The path table is the single most fragile thing in this file - a typo or a moved
        /// folder yields a material that loads, binds nothing, and looks plausibly flat - so a set
        /// that is declared but does not fully resolve is a hard failure, not a warning.
        ///
        /// Import settings are REPORTED, never mutated. These textures are shared: 69 live materials
        /// already reference the MaskMap family and Mat_Module_* consumes the trim sheets, so
        /// flipping sRGB or textureType from here would silently change assets this script does not
        /// own. A wrong colour space is reported so the owner can fix it deliberately.
        /// </summary>
        private static void AuditRoleTextureSet(
            ForgePackage package,
            int slot,
            StringBuilder report,
            ref AuditResult result)
        {
            RoleTextureSet set = ResolveTextureSet(package.Family, package.SlotRoles[slot]);
            if (set == null)
            {
                report.Append("        sourceSet=NONE (role is deliberately textureless)").AppendLine();
                return;
            }

            int resolved = 0;
            resolved += AuditOneMap(set.BaseColor, "BaseColor", true, false, report);
            resolved += AuditOneMap(set.NormalGL, "NormalGL", false, true, report);
            resolved += AuditOneMap(set.MaskUnityUrp, "MaskMap_UnityURP", false, false, report);
            resolved += AuditOneMap(set.Height, "Height", false, false, report);

            float calibration = string.Equals(package.Family, "Geology", StringComparison.Ordinal)
                ? GeologyCalibrationMetres
                : SmallPropCalibrationMetres;
            float tiling = set.TileMetres > 0.0001f ? calibration / set.TileMetres : 1f;
            float actualMetresPerTile = tiling > 0.0001f ? package.AssetScaleMetres / tiling : 0f;

            report.Append("        sourceSet resolved=").Append(resolved).Append("/4")
                  .Append(resolved == 4 ? string.Empty : " " + TokenFail + "-SOURCE-SET-UNRESOLVED")
                  .Append(" tileMetresTarget=").Append(F(set.TileMetres))
                  .Append(" tiling=").Append(F(tiling))
                  .Append(" thisAssetMetresPerTile=").Append(F(actualMetresPerTile))
                  .Append(" densityErrorX=").Append(F(set.TileMetres > 0.0001f
                      ? actualMetresPerTile / set.TileMetres : 0f))
                  .Append(" note=").Append(set.SourceNote)
                  .AppendLine();

            if (resolved != 4)
                result.TextureSetResolveFailures += 4 - resolved;
        }

        private static int AuditOneMap(
            string assetPath,
            string role,
            bool expectSrgb,
            bool expectNormalMap,
            StringBuilder report)
        {
            Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(assetPath);
            if (texture == null)
            {
                report.Append("        ").Append(TokenFail).Append(" map ").Append(role)
                      .Append(" MISSING ").Append(assetPath).AppendLine();
                return 0;
            }

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            string srgb = importer != null ? (importer.sRGBTexture ? "sRGB" : "linear") : "unknown";
            string type = importer != null ? importer.textureType.ToString() : "unknown";
            bool srgbWrong = importer != null && importer.sRGBTexture != expectSrgb;
            bool typeWrong = importer != null && expectNormalMap &&
                             importer.textureType != TextureImporterType.NormalMap;

            report.Append("        map ").Append(role)
                  .Append(' ').Append(texture.width).Append('x').Append(texture.height)
                  .Append(' ').Append(srgb)
                  .Append(srgbWrong ? "(EXPECTED-" + (expectSrgb ? "sRGB" : "linear") + ")" : string.Empty)
                  .Append(" type=").Append(type)
                  .Append(typeWrong ? "(EXPECTED-NormalMap)" : string.Empty)
                  .Append(" mips=").Append(importer != null && importer.mipmapEnabled ? "on" : "off")
                  .AppendLine();
            return 1;
        }

        private static void AuditImportedMesh(
            ForgePackage package,
            GameObject imported,
            StringBuilder report,
            ref AuditResult result)
        {
            MeshFilter[] filters = imported.GetComponentsInChildren<MeshFilter>(true);
            int lodMeshes = 0;
            bool colliderProxySeen = false;

            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null)
                    continue;

                string name = filter.gameObject.name;
                if (name.StartsWith("COL_", StringComparison.Ordinal))
                {
                    colliderProxySeen = true;
                    report.Append("      COL  ").Append(name)
                          .Append(" tris=").Append(TriangleCount(mesh)).AppendLine();
                    continue;
                }

                lodMeshes++;
                bool submeshMismatch = mesh.subMeshCount != package.SlotRoles.Length;
                if (submeshMismatch)
                    result.SubmeshMismatches++;

                report.Append("      MESH ").Append(name)
                      .Append(" submeshes=").Append(mesh.subMeshCount)
                      .Append('/').Append(package.SlotRoles.Length)
                      .Append(submeshMismatch ? " " + TokenFail + "-SUBMESH-SLOT-MISMATCH" : string.Empty)
                      .Append(" tris=").Append(TriangleCount(mesh))
                      .Append(" uvChannels=").Append(CountUvChannels(mesh))
                      .Append(' ').Append(DescribeVertexColours(mesh))
                      .AppendLine();
            }

            if (package.HasColliderProxy && !colliderProxySeen)
            {
                report.Append("      ").Append(TokenFail)
                      .Append(" manifest declares a COL_ proxy but no COL_ child imported.")
                      .AppendLine();
            }

            if (lodMeshes < 3)
            {
                report.Append("      ").Append(TokenFail)
                      .Append(" expected 3 _LOD children, found ").Append(lodMeshes).AppendLine();
            }
        }

        private static void AuditExistingPrefab(
            ForgePackage package,
            StringBuilder report,
            ref AuditResult result)
        {
            string prefabPath = PrefabPath(package);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                report.Append("      PREFAB none at ").Append(prefabPath)
                      .Append(" (run step 2b to assemble)").AppendLine();
                return;
            }

            MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            int nullSlots = 0;
            int nonForgeSlots = 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] materials = renderers[i].sharedMaterials;
                for (int slot = 0; slot < materials.Length; slot++)
                {
                    Material material = materials[slot];
                    if (material == null)
                    {
                        nullSlots++;
                        continue;
                    }

                    string path = AssetDatabase.GetAssetPath(material);
                    if (string.IsNullOrEmpty(path) ||
                        path.IndexOf(ForgeMaterialRoot, StringComparison.Ordinal) < 0)
                    {
                        nonForgeSlots++;
                    }
                }
            }

            result.PrefabSlotFailures += nullSlots + nonForgeSlots;
            report.Append("      PREFAB ").Append(prefabPath)
                  .Append(" renderers=").Append(renderers.Length)
                  .Append(" nullSlots=").Append(nullSlots)
                  .Append(" nonForgeSlots=").Append(nonForgeSlots)
                  .Append(" lodGroup=").Append(prefab.GetComponentInChildren<LODGroup>(true) != null ? "YES" : "NO")
                  .AppendLine();
        }

        // ══════════════════════════════════════════════════════════
        //  ORGANIC GATE
        // ══════════════════════════════════════════════════════════

        private struct OrganicGate
        {
            public bool Allowed;
            public bool ShaderPresent;
            public string[] BadReadsFound;
        }

        private static OrganicGate EvaluateOrganicGate()
        {
            OrganicGate gate = default;
            gate.BadReadsFound = Array.Empty<string>();

            string absolute = ResolveProjectAbsolutePath(CoralShaderPath);
            if (!System.IO.File.Exists(absolute))
                return gate;

            gate.ShaderPresent = true;
            string source;
            try
            {
                source = System.IO.File.ReadAllText(absolute);
            }
            catch (System.IO.IOException)
            {
                return gate;
            }
            catch (UnauthorizedAccessException)
            {
                return gate;
            }

            // COLD ALLOC: List<string>[2] - one entry per known non-compliant read -
            // owner: ForgeGeneratedMaterialAuthoring
            List<string> bad = new List<string>(2);
            for (int i = 0; i < OrganicKnownBadReads.Length; i++)
            {
                if (source.IndexOf(OrganicKnownBadReads[i], StringComparison.Ordinal) >= 0)
                    bad.Add(OrganicKnownBadReads[i]);
            }

            gate.BadReadsFound = bad.ToArray();
            gate.Allowed = source.IndexOf(OrganicContractOptInToken, StringComparison.Ordinal) >= 0;
            return gate;
        }

        private static void AppendOrganicGate(StringBuilder report, in OrganicGate gate)
        {
            report.Append("  organic gate: shader=").Append(CoralShaderName)
                  .Append(" present=").Append(gate.ShaderPresent ? "YES" : "NO")
                  .Append(" optInToken=").Append(gate.Allowed ? "FOUND" : "ABSENT")
                  .Append(" verdict=").Append(gate.Allowed ? "ALLOW" : TokenBlocked)
                  .AppendLine();

            for (int i = 0; i < gate.BadReadsFound.Length; i++)
            {
                report.Append("      non-compliant read still present in ").Append(CoralShaderPath)
                      .Append(": ").Append(gate.BadReadsFound[i]).AppendLine();
            }

            if (!gate.Allowed)
            {
                report.Append("      Organic binding stays refused. 3dmodel.md:132-137 fixes ")
                      .Append("R=sway G=biolum B=AO A=family; the reads above consume R as tint ")
                      .Append("and G as moisture. Add the literal token ")
                      .Append(OrganicContractOptInToken)
                      .Append(" to the shader when the channel fix lands and this gate clears ")
                      .Append("itself.").AppendLine();
            }
        }

        // ══════════════════════════════════════════════════════════
        //  TEXTURE TRUTH - printed on every run so the open gap never goes quiet
        // ══════════════════════════════════════════════════════════

        private static void AppendTextureTruth(StringBuilder report)
        {
            report.Append("  TEXTURE TRUTH: no texture is GENERATED here. Every map bound above is an ")
                  .Append("existing project asset, reused as 3DMODEL_TEXTURES_MATERIALS.md:17 ")
                  .Append("requires - 'Generated meshes must use existing high-quality ")
                  .Append("human-authored or AI-assisted texture assets when available.'")
                  .AppendLine();
            report.Append("  Packing DECLARED, not guessed: these sets use URP's ")
                  .Append("R=Metallic G=Occlusion A=Smoothness, which is what this shader decodes ")
                  .Append("(Hecton_ModuleHardSurfaceLit.shader:71 label, :349-353 decode). That is ")
                  .Append("NOT the bible's default R=Metallic G=Roughness B=AO A=Emission ")
                  .Append("(3DMODEL_TEXTURES_MATERIALS.md:44-49); section 3 line 46 permits the ")
                  .Append("deviation only if the contract states which channel is which, so this ")
                  .Append("line is that statement. B is unused.").AppendLine();
            report.Append("  NAMING DEVIATION, open: the bound files are TX_GM_*/TX_B34_*/TX_GB_* ")
                  .Append("with _BaseColor/_NormalGL/_MaskMap_UnityURP/_Height suffixes. Neither ")
                  .Append("3DMODEL_TEXTURES_MATERIALS.md:27-32 (TX_[Family]_[Variant]_Albedo, ")
                  .Append("_Normal, _MRAO) nor law.py:500 (TX_{family}_{set}_{role}) matches that. ")
                  .Append("Renaming 703 files that 69 live materials already reference is a ")
                  .Append("project-wide decision, not a side effect of this script, so the ")
                  .Append("existing GUIDs are referenced as they are and the drift is reported.")
                  .AppendLine();
            report.Append("  GENUINE ART GAP, one role only: MAT_SmallProp_InstrumentGlass has no ")
                  .Append("source. No instrument-glass, readout-pane or dark-glass tileable set ")
                  .Append("exists in Assets/_Project/Art/TEXTURES. Its map weights stay 0 and it ")
                  .Append("renders from scalars plus the vertex channels. Binding a tiling metal ")
                  .Append("albedo to a readout window would be a lie no gate could see, so it is ")
                  .Append("left visibly empty instead.").AppendLine();
            report.Append("  Height maps are bound but POM is OFF (_ModulePomParams.y=0, ")
                  .Append("shader :269-271 early-returns). Enabling a per-pixel loop is a GPU cost ")
                  .Append("decision and no profiler capture exists for it.").AppendLine();
            report.Append("  manifestGaps 'textures' CANNOT be closed from Unity either way: the ")
                  .Append("field is computed in Tools/Blender/h8forge/export_unity.py:1969-1973 ")
                  .Append("from the textures= argument the GENERATOR passes at Blender export time. ")
                  .Append("A .mat asset has no vote. See the class comment.").AppendLine();
        }

        // ══════════════════════════════════════════════════════════
        //  MATERIAL PROPERTY AUTHORING
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Writes the per-role response for <c>Hecton8/Construction/ModuleHardSurfaceLit</c>.
        /// Every value is guarded by <c>HasProperty</c> so a shader edit that removes a property
        /// degrades to "not written" instead of throwing mid-batch.
        ///
        /// Metallic truth follows `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md:70` and `:102`: paint,
        /// rust, grime, ceramic, rubber, algae, sediment and whole rock are non-metallic; bare
        /// steel and a localised ore inclusion may be metallic. Nothing here lifts a whole rock to
        /// metal.
        /// </summary>
        private static void ApplyRoleProperties(Material material, string family, string role)
        {
            // Inherited keywords from a previous shader are dead weight and cause variant churn.
            material.shaderKeywords = Array.Empty<string>();

            // ---- texture stack -------------------------------------------------------------
            // THE MAP WEIGHTS ARE DERIVED FROM WHAT ACTUALLY LOADED, never asserted. A weight of 1
            // over an unbound slot samples the shader's "white" default and reads as fully smooth
            // chrome; a weight of 0 over a bound map wastes the art. Both are silent. So the
            // weights below are computed from the resolved Texture references, which is the same
            // gate ModuleHardSurfaceWearMaterialAuthoring.cs:358-364 applies to the module set.
            BoundMaps maps = BindTextureSet(material, family, role);

            float mapWeight = maps.HasMask ? 1f : 0f;
            float normalScale = maps.HasNormal ? 1f : 0f;
            SetVector(material, "_ModuleSurfaceParams",
                new Vector4(mapWeight, mapWeight, mapWeight, normalScale));
            SetFloat(material, "_BumpScale", normalScale);
            SetFloat(material, "_OcclusionStrength", 1f);

            // Height is BOUND but parallax occlusion mapping is left OFF: steps 0 makes the shader
            // early-return out of the POM loop entirely (Hecton_ModuleHardSurfaceLit.shader:269-271),
            // so the map costs nothing until somebody switches it on WITH a profiler capture. I have
            // no GPU measurement on the compact lane and this is a per-pixel loop, so enabling it
            // here would be an unproven cost decision dressed up as a default.
            SetVector(material, "_ModulePomParams", new Vector4(0f, 0f, 0f, 1f));
            SetFloat(material, "_Parallax", 0f);

            // Full trust in all four baked channels: the forge measures them per asset and every
            // schema-1 manifest reports "every stored channel varies; flat channels: none".
            SetVector(material, "_ModuleWearParams", new Vector4(1f, 1f, 1f, 1f));

            switch (family)
            {
                case "Geology":
                    ApplyGeologyRole(material, role);
                    break;
                case "SmallProp":
                    ApplySmallPropRole(material, role);
                    break;
                default:
                    // No compliant consumer is bound for other families; leave shader defaults.
                    break;
            }

            material.SetOverrideTag("RenderType", "Opaque");
            material.renderQueue = (int)RenderQueue.Geometry;

            // Shared material on MeshRenderer GameObjects with instancing on is the GPU Resident
            // Drawer path; MaterialPropertyBlock is banned on this geometry by `AGENTS.md` Runtime
            // Hot-Path Law.
            material.enableInstancing = true;
        }

        // ══════════════════════════════════════════════════════════
        //  TEXTURE BINDING AND TEXEL CALIBRATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Asset extent in metres that each family's SHARED material is calibrated to.
        ///
        /// Geology is the honest compromise and it is worth reading. One material set serves three
        /// asset sizes - boulder 0.8 m, outcrop 2.9 m, cliffchunk 7.6 m (manifest
        /// identity.scaleMeters) - and Hecton_ModuleHardSurfaceLit samples UV0 through
        /// TRANSFORM_TEX, while geology UV0 is a normalised smart_project atlas unwrap rather than a
        /// metre-proportional one. So a single tiling value cannot give all three the same texel
        /// density. Calibrating on the MID asset bounds the error in both directions instead of
        /// letting it run one way: at 2.32 tiling the outcrop lands on 1.25 m/tile exactly, the
        /// boulder on 0.34 m/tile (3.6x too fine) and the cliffchunk on 3.28 m/tile (2.6x too
        /// coarse). VerifyForgeMaterialBindings prints those three numbers so the deviation is on
        /// the record and not discovered later in a screenshot.
        ///
        /// The real fix already exists in the asset and is a shader change, not a material one: the
        /// geology FBX carries a SECOND UV set, UV1_Strata, which the manifest declares as
        /// cylindrical-in-bedding-frame at metresPerTile 1.25 - already metre-calibrated and
        /// therefore already size-independent across all three assets. The master shader reads
        /// TEXCOORD0 only. Reading UV1 there would give correct, size-independent density from ONE
        /// shared material with no per-size variants. That belongs to whoever owns the shader.
        /// </summary>
        private const float GeologyCalibrationMetres = 2.9f;

        /// <summary>Both drill seeds are identity.scaleMeters 0.255, so this one is exact.</summary>
        private const float SmallPropCalibrationMetres = 0.255f;

        private struct BoundMaps
        {
            public bool HasBase;
            public bool HasNormal;
            public bool HasMask;
            public bool HasHeight;
            public float Tiling;
            public string SetNote;
        }

        /// <summary>
        /// Loads the role's source set and binds it with a calibrated tiling. Returns what actually
        /// resolved so the caller can derive the map weights from reality rather than from intent.
        /// </summary>
        private static BoundMaps BindTextureSet(Material material, string family, string role)
        {
            BoundMaps bound = default;
            bound.SetNote = "none";

            RoleTextureSet set = ResolveTextureSet(family, role);
            if (set == null)
            {
                SetTexture(material, "_BaseMap", null, Vector2.one);
                SetTexture(material, "_BumpMap", null, Vector2.one);
                SetTexture(material, "_MaskMap", null, Vector2.one);
                SetTexture(material, "_ParallaxMap", null, Vector2.one);
                return bound;
            }

            float calibration = string.Equals(family, "Geology", StringComparison.Ordinal)
                ? GeologyCalibrationMetres
                : SmallPropCalibrationMetres;
            float tiling = set.TileMetres > 0.0001f ? calibration / set.TileMetres : 1f;
            Vector2 scale = new Vector2(tiling, tiling);

            Texture baseColor = AssetDatabase.LoadAssetAtPath<Texture>(set.BaseColor);
            Texture normalGl = AssetDatabase.LoadAssetAtPath<Texture>(set.NormalGL);
            Texture mask = AssetDatabase.LoadAssetAtPath<Texture>(set.MaskUnityUrp);
            Texture height = AssetDatabase.LoadAssetAtPath<Texture>(set.Height);

            SetTexture(material, "_BaseMap", baseColor, scale);
            SetTexture(material, "_BumpMap", normalGl, scale);
            SetTexture(material, "_MaskMap", mask, scale);
            SetTexture(material, "_ParallaxMap", height, scale);

            // `!= null` and not `??`: UnityEngine.Object overloads == to test the native pointer
            // while ?? tests the managed reference, so a fake-null asset would slip through
            // (`COMMON_SENSE.md` The Unity Object Fake Null).
            bound.HasBase = baseColor != null;
            bound.HasNormal = normalGl != null;
            bound.HasMask = mask != null;
            bound.HasHeight = height != null;
            bound.Tiling = tiling;
            bound.SetNote = set.SourceNote;
            return bound;
        }

        private static void SetTexture(Material material, string name, Texture texture, Vector2 scale)
        {
            if (!material.HasProperty(name))
                return;

            material.SetTexture(name, texture);
            material.SetTextureScale(name, scale);
            material.SetTextureOffset(name, Vector2.zero);
        }

        /// <summary>
        /// Sedimentary rock, photic shallows to medium depth. Channel R is an exposed chip that
        /// reveals FRESH PALE MINERAL, not bare metal, so the edge response lifts albedo and adds a
        /// little sheen while leaving metallic at zero. Channel G is mineral stain / oxide / algae,
        /// split up-facing vs down-facing by the shader's own upness term
        /// (Hecton_ModuleHardSurfaceLit.shader:492-494).
        /// </summary>
        private static void ApplyGeologyRole(Material material, string role)
        {
            SetColor(material, "_ModuleOxideColor", new Color(0.46f, 0.30f, 0.16f, 1f));
            SetColor(material, "_ModuleBiofilmColor", new Color(0.24f, 0.34f, 0.26f, 1f));
            SetColor(material, "_ModuleSiltTint", new Color(0.26f, 0.28f, 0.25f, 1f));
            SetVector(material, "_ModuleNoirParams", new Vector4(0.30f, 0.34f, 0.40f, 0.06f));

            switch (role)
            {
                // _BaseColor MULTIPLIES the albedo sample (shader :456
                // `SAMPLE_TEXTURE2D(_BaseMap, ...) * _BaseColor`) and _Smoothness MULTIPLIES the
                // mask's A channel when the map weight is 1 (:350). So with a real set bound these
                // are GRADING TINTS and GAINS near 1, not absolute surface values. Writing the
                // absolute colour here would double-darken the authored albedo.
                case "Primary":
                    SetColor(material, "_BaseColor", new Color(0.820f, 0.820f, 0.800f, 1f));
                    SetFloat(material, "_Metallic", 0f);
                    SetFloat(material, "_Smoothness", 0.85f);
                    SetColor(material, "_ModuleEdgeMetalColor", new Color(0.62f, 0.60f, 0.55f, 1f));
                    SetVector(material, "_ModuleEdgeResponse", new Vector4(0f, 0.30f, 0.75f, 0.30f));
                    SetVector(material, "_ModuleOxideResponse", new Vector4(0.55f, 0f, 0f, 0.72f));
                    SetVector(material, "_ModuleRustSiltParams", new Vector4(0.42f, 0.18f, 0f, 0f));
                    // A is the ore/emission mask, measured mean 0.09 / max 0.96 on the boulder.
                    // Only the vein role emits; host rock does not glow.
                    SetVector(material, "_ModuleSeamParams", new Vector4(0.98f, 0.02f, 0f, 0f));
                    SetColor(material, "_ModuleSeamEmissionColor", new Color(0f, 0f, 0f, 1f));
                    break;

                case "FractureFace":
                    // A fresh cut is brighter, crisper and cleaner than the weathered field.
                    SetColor(material, "_BaseColor", new Color(0.960f, 0.950f, 0.920f, 1f));
                    SetFloat(material, "_Metallic", 0f);
                    SetFloat(material, "_Smoothness", 0.95f);
                    SetColor(material, "_ModuleEdgeMetalColor", new Color(0.70f, 0.68f, 0.63f, 1f));
                    SetVector(material, "_ModuleEdgeResponse", new Vector4(0f, 0.38f, 0.85f, 0.40f));
                    SetVector(material, "_ModuleOxideResponse", new Vector4(0.42f, 0f, 0f, 0.40f));
                    SetVector(material, "_ModuleRustSiltParams", new Vector4(0.16f, 0.12f, 0f, 0f));
                    SetVector(material, "_ModuleSeamParams", new Vector4(0.98f, 0.02f, 0f, 0f));
                    SetColor(material, "_ModuleSeamEmissionColor", new Color(0f, 0f, 0f, 1f));
                    break;

                case "MineralVein":
                    // The one place a localised metallic inclusion is legal
                    // (3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md:102 - "never full-rock metallic
                    // unless it is a deliberate ore node"). Emission is a cold mineral shimmer at
                    // the vein core, gated well above the 0.09 mean so the halo stays dark.
                    SetColor(material, "_BaseColor", new Color(0.800f, 0.880f, 0.880f, 1f));
                    SetFloat(material, "_Metallic", 0.25f);
                    SetFloat(material, "_Smoothness", 1.0f);
                    SetColor(material, "_ModuleEdgeMetalColor", new Color(0.66f, 0.70f, 0.72f, 1f));
                    SetVector(material, "_ModuleEdgeResponse", new Vector4(0.35f, 0.50f, 0.60f, 0.35f));
                    SetVector(material, "_ModuleOxideResponse", new Vector4(0.38f, 0.30f, 0f, 0.50f));
                    SetVector(material, "_ModuleRustSiltParams", new Vector4(0.22f, 0.26f, 0f, 0f));
                    SetVector(material, "_ModuleSeamParams", new Vector4(0.62f, 0.28f, 0f, 0.55f));
                    SetColor(material, "_ModuleSeamEmissionColor", new Color(0.10f, 0.34f, 0.38f, 1f));
                    break;
            }
        }

        /// <summary>
        /// NASA-punk pressure tool. Alpha here is the generator's <c>emission_decal_mask</c>, which
        /// shares the module family's 0.94 seam convention, so the seam threshold matches
        /// ModuleHardSurfaceDetail1712's EmissiveAlphaThreshold on the casing and is lowered only
        /// on the readout face, where the emissive island is the point of the surface.
        /// </summary>
        private static void ApplySmallPropRole(Material material, string role)
        {
            SetColor(material, "_ModuleOxideColor", new Color(0.66f, 0.36f, 0.16f, 1f));
            SetColor(material, "_ModuleBiofilmColor", new Color(0.28f, 0.38f, 0.32f, 1f));
            SetColor(material, "_ModuleSiltTint", new Color(0.23f, 0.28f, 0.26f, 1f));
            SetVector(material, "_ModuleNoirParams", new Vector4(0.34f, 0.42f, 0.35f, 0.06f));

            switch (role)
            {
                case "PaintedCasing":
                    // Painted industrial coating: non-metallic paint that strips to bright steel on
                    // the chamfers. Paint adhesion 0.55 keeps protected fields painted, matching
                    // the tuned module response.
                    SetColor(material, "_BaseColor", new Color(0.880f, 0.900f, 0.860f, 1f));
                    SetFloat(material, "_Metallic", 0f);
                    SetFloat(material, "_Smoothness", 0.90f);
                    SetColor(material, "_ModuleEdgeMetalColor", new Color(0.55f, 0.57f, 0.60f, 1f));
                    SetVector(material, "_ModuleEdgeResponse", new Vector4(0.90f, 0.60f, 0.80f, 0.40f));
                    SetVector(material, "_ModuleOxideResponse", new Vector4(0.62f, 0.85f, 0f, 0.68f));
                    SetVector(material, "_ModuleRustSiltParams", new Vector4(0.20f, 0.48f, 0f, 0f));
                    SetVector(material, "_ModuleSeamParams", new Vector4(0.94f, 0.04f, 0.55f, 0.85f));
                    SetColor(material, "_ModuleSeamEmissionColor", new Color(0.55f, 0.28f, 0.04f, 1f));
                    break;

                case "BareMetalEdge":
                    // Bare machined steel. No paint to hold, so adhesion is 0 and the edge reveal
                    // is pure gain rather than an albedo lift.
                    SetColor(material, "_BaseColor", new Color(0.950f, 0.960f, 0.980f, 1f));
                    SetFloat(material, "_Metallic", 0.85f);
                    SetFloat(material, "_Smoothness", 1.0f);
                    SetColor(material, "_ModuleEdgeMetalColor", new Color(0.72f, 0.74f, 0.77f, 1f));
                    SetVector(material, "_ModuleEdgeResponse", new Vector4(1f, 0.70f, 0.35f, 0.45f));
                    SetVector(material, "_ModuleOxideResponse", new Vector4(0.70f, 0.55f, 0f, 0.72f));
                    SetVector(material, "_ModuleRustSiltParams", new Vector4(0.14f, 0.55f, 0f, 0f));
                    SetVector(material, "_ModuleSeamParams", new Vector4(0.94f, 0.04f, 0f, 0.60f));
                    SetColor(material, "_ModuleSeamEmissionColor", new Color(0.55f, 0.28f, 0.04f, 1f));
                    break;

                case "RubberGasket":
                    // Aged black rubber: non-metallic, low smoothness, scuffs grey rather than
                    // revealing metal. Silt collects in the contact zone; rust does not form on it.
                    SetColor(material, "_BaseColor", new Color(0.550f, 0.550f, 0.580f, 1f));
                    SetFloat(material, "_Metallic", 0f);
                    SetFloat(material, "_Smoothness", 0.70f);
                    SetColor(material, "_ModuleEdgeMetalColor", new Color(0.20f, 0.20f, 0.21f, 1f));
                    SetVector(material, "_ModuleEdgeResponse", new Vector4(0f, 0.12f, 0.25f, 0.25f));
                    SetVector(material, "_ModuleOxideResponse", new Vector4(0.20f, 0f, 0f, 0.22f));
                    SetVector(material, "_ModuleRustSiltParams", new Vector4(0.30f, 0.05f, 0f, 0f));
                    SetVector(material, "_ModuleSeamParams", new Vector4(0.94f, 0.04f, 0f, 0f));
                    SetColor(material, "_ModuleSeamEmissionColor", new Color(0f, 0f, 0f, 1f));
                    SetVector(material, "_ModuleNoirParams", new Vector4(0.34f, 0.22f, 0.45f, 0.08f));
                    break;

                case "InstrumentGlass":
                    // Depth/torque readout. NOTE, and this is the honest limitation: this master
                    // shader is Opaque/Geometry, so the pane reads as a dark polished face with an
                    // emissive readout island, not as refractive glass. True transparent glass
                    // needs a transparent master shader and a render-state decision that belongs
                    // with the lead, not with a material default.
                    SetColor(material, "_BaseColor", new Color(0.045f, 0.050f, 0.055f, 1f));
                    SetFloat(material, "_Metallic", 0f);
                    SetFloat(material, "_Smoothness", 0.92f);
                    SetColor(material, "_ModuleEdgeMetalColor", new Color(0.30f, 0.32f, 0.34f, 1f));
                    SetVector(material, "_ModuleEdgeResponse", new Vector4(0f, 0.20f, 0.10f, 0.20f));
                    SetVector(material, "_ModuleOxideResponse", new Vector4(0.10f, 0f, 0f, 0.12f));
                    SetVector(material, "_ModuleRustSiltParams", new Vector4(0.10f, 0f, 0f, 0f));
                    SetVector(material, "_ModuleSeamParams", new Vector4(0.55f, 0.20f, 0f, 1.20f));
                    SetColor(material, "_ModuleSeamEmissionColor", new Color(0.62f, 0.34f, 0.06f, 1f));
                    SetVector(material, "_ModuleNoirParams", new Vector4(0.30f, 0.60f, 0.20f, 0.04f));
                    break;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PREFAB ASSEMBLY
        // ══════════════════════════════════════════════════════════

        private static bool BuildPackagePrefab(
            ForgePackage package,
            GameObject imported,
            Material[] slotMaterials,
            StringBuilder report)
        {
            string prefabPath = PrefabPath(package);
            GameObject root = null;

            try
            {
                root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(prefabPath));

                // COLD ALLOC: List<Renderer>[4] x3 - one per LOD level - owner: ForgeGeneratedMaterialAuthoring
                List<Renderer> lod0 = new List<Renderer>(4);
                List<Renderer> lod1 = new List<Renderer>(4);
                List<Renderer> lod2 = new List<Renderer>(4);
                int colliderChildren = 0;

                MeshFilter[] filters = imported.GetComponentsInChildren<MeshFilter>(true);
                for (int i = 0; i < filters.Length; i++)
                {
                    MeshFilter filter = filters[i];
                    Mesh mesh = filter != null ? filter.sharedMesh : null;
                    if (mesh == null)
                        continue;

                    string name = filter.gameObject.name;

                    if (name.StartsWith("COL_", StringComparison.Ordinal))
                    {
                        // 3dmodel.md section 9: the convex proxy, never an LOD visual mesh.
                        GameObject colliderChild = new GameObject(name);
                        colliderChild.transform.SetParent(root.transform, false);
                        MeshCollider collider = colliderChild.AddComponent<MeshCollider>();
                        collider.sharedMesh = mesh;
                        collider.convex = true;
                        colliderChildren++;
                        continue;
                    }

                    int level = ResolveLodLevel(name);
                    if (level < 0)
                        continue;

                    GameObject visual = new GameObject(name);
                    visual.transform.SetParent(root.transform, false);
                    visual.AddComponent<MeshFilter>().sharedMesh = mesh;

                    MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = BuildSlotArray(slotMaterials, mesh.subMeshCount);
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;

                    if (level == 0) lod0.Add(renderer);
                    else if (level == 1) lod1.Add(renderer);
                    else lod2.Add(renderer);
                }

                if (lod0.Count == 0 || lod1.Count == 0 || lod2.Count == 0)
                {
                    report.Append("  ").Append(TokenFail).Append(' ').Append(prefabPath)
                          .Append(" incomplete LOD chain lod0=").Append(lod0.Count)
                          .Append(" lod1=").Append(lod1.Count)
                          .Append(" lod2=").Append(lod2.Count).AppendLine();
                    return false;
                }

                LODGroup lodGroup = root.AddComponent<LODGroup>();
                LOD[] levels =
                {
                    new LOD(Lod0ScreenRelativeHeight, lod0.ToArray()) { fadeTransitionWidth = LodFadeWidth },
                    new LOD(Lod1ScreenRelativeHeight, lod1.ToArray()) { fadeTransitionWidth = LodFadeWidth },
                    new LOD(Lod2ScreenRelativeHeight, lod2.ToArray()) { fadeTransitionWidth = LodFadeWidth }
                };
                lodGroup.fadeMode = LODFadeMode.CrossFade;
                lodGroup.animateCrossFading = false;
                lodGroup.SetLODs(levels);
                lodGroup.RecalculateBounds();

                if (!EnsureFolder(System.IO.Path.GetDirectoryName(prefabPath).Replace('\\', '/')))
                {
                    report.Append("  ").Append(TokenFail).Append(" folder for ").Append(prefabPath)
                          .AppendLine();
                    return false;
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool saved);
                report.Append(saved ? "  WRITE    " : "  " + TokenFail + "    ").Append(prefabPath)
                      .Append(" lod0=").Append(lod0.Count)
                      .Append(" lod1=").Append(lod1.Count)
                      .Append(" lod2=").Append(lod2.Count)
                      .Append(" colliderChildren=").Append(colliderChildren)
                      .Append(" slots=").Append(slotMaterials.Length)
                      .AppendLine();
                return saved;
            }
            finally
            {
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Slot array sized to the mesh's own submesh count. When the mesh disagrees with the
        /// declared slot table, the surplus slots repeat slot 0 (primary) rather than leaving a
        /// null, and the mismatch is already counted as a failure by the audit - so the asset never
        /// renders with Unity's default material and the disagreement is still visible.
        /// </summary>
        private static Material[] BuildSlotArray(Material[] slotMaterials, int subMeshCount)
        {
            int count = subMeshCount > 0 ? subMeshCount : 1;
            Material[] result = new Material[count];
            for (int i = 0; i < count; i++)
                result[i] = i < slotMaterials.Length ? slotMaterials[i] : slotMaterials[0];
            return result;
        }

        private static Material[] LoadSlotMaterials(ForgePackage package)
        {
            Material[] materials = new Material[package.SlotRoles.Length];
            for (int slot = 0; slot < package.SlotRoles.Length; slot++)
            {
                string name = MaterialName(package.Family, package.SlotRoles[slot]);
                string path = ForgeMaterialRoot + "/" + package.Family + "/" + name + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                    return null;

                materials[slot] = material;
            }

            return materials;
        }

        private static int ResolveLodLevel(string objectName)
        {
            if (objectName.EndsWith("_LOD0", StringComparison.Ordinal)) return 0;
            if (objectName.EndsWith("_LOD1", StringComparison.Ordinal)) return 1;
            if (objectName.EndsWith("_LOD2", StringComparison.Ordinal)) return 2;
            return -1;
        }

        // ══════════════════════════════════════════════════════════
        //  SMALL HELPERS
        // ══════════════════════════════════════════════════════════

        /// <summary>law.NAME_MATERIAL, "MAT_{family}_{role}" (law.py:499).</summary>
        private static string MaterialName(string family, string role)
        {
            return "MAT_" + family + "_" + role;
        }

        /// <summary>law.NAME_PREFAB_GENERATED, "GEN_{family}_{name}" (law.py:502).</summary>
        private static string PrefabPath(ForgePackage package)
        {
            string directory = package.FbxAssetPath.Substring(
                0, package.FbxAssetPath.LastIndexOf('/') + 1);
            string stem = System.IO.Path.GetFileNameWithoutExtension(package.FbxAssetPath);
            if (stem.StartsWith("MESH_", StringComparison.Ordinal))
                stem = stem.Substring("MESH_".Length);
            return directory + "GEN_" + stem + ".prefab";
        }

        private static StringBuilder NewReport(string phase)
        {
            StringBuilder report = new StringBuilder(8192);
            report.Append(LogPrefix).Append(' ').Append(phase).Append(" begin root=")
                  .Append(ForgePackageRoot).AppendLine();
            return report;
        }

        private static void Emit(StringBuilder report, bool pass)
        {
            if (pass)
                Debug.Log(report.ToString());
            else
                Debug.LogError(report.ToString());
        }

        private static Shader ResolveShader(string shaderName, string shaderPath)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader != null)
                return shader;

            return AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
        }

        private static bool EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
                return true;

            int lastSlash = folder.LastIndexOf('/');
            if (lastSlash <= 0)
                return false;

            string parent = folder.Substring(0, lastSlash);
            string leaf = folder.Substring(lastSlash + 1);
            if (!EnsureFolder(parent))
                return false;

            return !string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, leaf));
        }

        private static int TriangleCount(Mesh mesh)
        {
            int total = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
                total += (int)(mesh.GetIndexCount(i) / 3);
            return total;
        }

        private static int CountUvChannels(Mesh mesh)
        {
            int channels = 0;
            if (mesh.HasVertexAttribute(VertexAttribute.TexCoord0)) channels++;
            if (mesh.HasVertexAttribute(VertexAttribute.TexCoord1)) channels++;
            if (mesh.HasVertexAttribute(VertexAttribute.TexCoord2)) channels++;
            return channels;
        }

        /// <summary>
        /// Vertex-colour presence plus real per-channel min/max. Deliberately defensive about
        /// <c>isReadable</c>: the forge import contract sets Read/Write off
        /// (manifest unityImport.modelImporter.isReadable=false), so this reports UNREADABLE rather
        /// than failing a gate on an inaccessible buffer. An unreadable stream is not evidence of
        /// an absent stream and must not be reported as one.
        /// </summary>
        private static string DescribeVertexColours(Mesh mesh)
        {
            if (!mesh.HasVertexAttribute(VertexAttribute.Color))
                return "vcol=ABSENT";

            if (!mesh.isReadable)
                return "vcol=PRESENT-UNREADABLE(isReadable=false)";

            // COLD ALLOC: List<Color32>[0] - editor audit scratch - owner: ForgeGeneratedMaterialAuthoring
            List<Color32> colours = new List<Color32>(mesh.vertexCount);
            mesh.GetColors(colours);
            if (colours.Count == 0)
                return "vcol=PRESENT-EMPTY";

            byte minR = 255, maxR = 0, minG = 255, maxG = 0, minB = 255, maxB = 0, minA = 255, maxA = 0;
            for (int i = 0; i < colours.Count; i++)
            {
                Color32 c = colours[i];
                if (c.r < minR) minR = c.r; if (c.r > maxR) maxR = c.r;
                if (c.g < minG) minG = c.g; if (c.g > maxG) maxG = c.g;
                if (c.b < minB) minB = c.b; if (c.b > maxB) maxB = c.b;
                if (c.a < minA) minA = c.a; if (c.a > maxA) maxA = c.a;
            }

            bool flat = minR == maxR && minG == maxG && minB == maxB && minA == maxA;
            return "vcol=" + (flat ? "DEGENERATE" : "OK") +
                   " R[" + minR + ".." + maxR + "]" +
                   " G[" + minG + ".." + maxG + "]" +
                   " B[" + minB + ".." + maxB + "]" +
                   " A[" + minA + ".." + maxA + "]";
        }

        private static int CountBoundTextures(Material material)
        {
            int bound = 0;
            // `??` and `!= null` are NOT interchangeable on UnityEngine.Object: `??` tests the
            // managed reference while the overloaded `==` tests the native pointer, so a destroyed
            // texture would slip through `??` (`COMMON_SENSE.md` The Unity Object Fake Null).
            if (HasTexture(material, "_BaseMap")) bound++;
            if (HasTexture(material, "_BumpMap")) bound++;
            if (HasTexture(material, "_MaskMap")) bound++;
            if (HasTexture(material, "_ParallaxMap")) bound++;
            return bound;
        }

        private static bool HasTexture(Material material, string name)
        {
            return material.HasProperty(name) && material.GetTexture(name) != null;
        }

        private static void SetVector(Material material, string name, Vector4 value)
        {
            if (material.HasProperty(name))
                material.SetVector(name, value);
        }

        private static void SetColor(Material material, string name, Color value)
        {
            if (material.HasProperty(name))
                material.SetColor(name, value);
        }

        private static void SetFloat(Material material, string name, float value)
        {
            if (material.HasProperty(name))
                material.SetFloat(name, value);
        }

        private static string ResolveProjectAbsolutePath(string projectRelativePath)
        {
            // AGENTS.md Relative Path Requirement: resolve from the project root through
            // Application.dataPath, never from the process working directory and never from a
            // hardcoded developer path. Same helper shape as
            // HectonFBXPostprocessor.cs:934-943.
            string assetsFolder = Application.dataPath.Replace('\\', '/');
            int lastSlash = assetsFolder.LastIndexOf('/');
            string projectRoot = lastSlash > 0 ? assetsFolder.Substring(0, lastSlash) : assetsFolder;
            return projectRoot + "/" + projectRelativePath;
        }

        private static string F(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
#endif

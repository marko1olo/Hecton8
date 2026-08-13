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

        // ══════════════════════════════════════════════════════════
        //  ORGANIC MASTERS - PER PACKAGE, NOT ONE CONSTANT
        //
        //  This was a single hardcoded CoralMaster resolve, and that was a STRUCTURAL defect rather
        //  than a missing table row: with one constant, Hecton_KelpMaster.shader was UNREACHABLE no
        //  matter what roles or texture sets were added, because every organic package - coral AND
        //  CapStem - resolved to CoralMaster. Kelp was absent from the original brief for the same
        //  reason; its geometry and vertex channels were ready and the RESOLVER was not. Each
        //  package now names its own master, and the contract gate is evaluated against that
        //  shader's own source instead of against CoralMaster's on kelp's behalf.
        // ══════════════════════════════════════════════════════════

        private const string CoralShaderName = "Hecton8/Flora/CoralMaster";
        private const string CoralShaderPath =
            "Assets/_Project/Art/Shaders/Hecton_CoralMaster.shader";

        private const string KelpShaderName = "Hecton8/Flora/KelpMaster";
        private const string KelpShaderPath =
            "Assets/_Project/Art/Shaders/Hecton_KelpMaster.shader";

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
        /// CoralMaster's two historical non-compliant reads, quoted so the report is actionable
        /// rather than merely negative. Informational plus the AND-half of the gate.
        /// </summary>
        private static readonly string[] CoralKnownBadReads =
        {
            "saturate(input.color.r) * _VertexTintStrength",
            "moisture = saturate(input.color.g)"
        };

        /// <summary>
        /// KelpMaster's equivalents. These are DIFFERENT STRINGS because the two shaders were never
        /// the same source, and reusing CoralMaster's list for kelp would have produced a
        /// vacuously-passing negative probe - a check that can never fire, which this project treats
        /// as the same defect as no check.
        ///
        /// Measured 2026-07-29: BOTH are already absent from Hecton_KelpMaster.shader. Its tint now
        /// reads the biome hash (`:643 tintMask = saturate(biomeHash * _VertexTintStrength)`) and its
        /// wetness the authored mask (`:614 maskSample.g * (1 + _MoistureBoost)`), while
        /// <c>vertexColor.r</c> survives at `:261` only as <c>swayMask</c> - which is R consumed for
        /// exactly what the contract assigns it, sway amplitude, and is therefore CORRECT and must
        /// NOT appear in this list. G goes to <c>bakedBiolumMask</c> at `:525` and B to
        /// <c>bakedVertexAo</c> at `:526`.
        /// </summary>
        private static readonly string[] KelpKnownBadReads =
        {
            "saturate(vertexColor.r) * _VertexTintStrength",
            "moisture = saturate(vertexColor.g)"
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
            /// <summary>
            /// The organic master this package binds to, or null for a hard-surface package. THE
            /// POINT OF THIS FIELD: it is what makes KelpMaster reachable at all. While the resolver
            /// was one hardcoded CoralMaster constant, no role table, texture set or gate change
            /// could route a package anywhere else.
            /// </summary>
            public string OrganicMasterName;
            public string OrganicMasterPath;

            public ForgePackage(
                string manifestFileName,
                string fbxAssetPath,
                string family,
                ForgeSurfaceClass surface,
                string[] slotRoles,
                string slotRoleSource,
                bool hasColliderProxy,
                float assetScaleMetres,
                string organicMasterName = null,
                string organicMasterPath = null)
            {
                AssetScaleMetres = assetScaleMetres;
                ManifestFileName = manifestFileName;
                FbxAssetPath = fbxAssetPath;
                Family = family;
                Surface = surface;
                SlotRoles = slotRoles;
                SlotRoleSource = slotRoleSource;
                HasColliderProxy = hasColliderProxy;
                OrganicMasterName = organicMasterName;
                OrganicMasterPath = organicMasterPath;
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
        //  ROLE-TOKEN CONVENTION - SETTLED HERE, AND IT AVERTS A DESTRUCTIVE COLLISION
        //
        //  Kelp's manifest emits its roles in lower_snake_case:
        //      MANIFEST_Flora_Kelp_s4021_q100.json materialSlots ->
        //          slot 0 "tissue"             material "MAT_Flora_tissue"
        //          slot 1 "basal_collar_scar"   material "MAT_Flora_basal_collar_scar"
        //          slot 2 "holdfast"            material "MAT_Flora_holdfast"
        //  while rock.py, prop_handtool.py, flora_capstem.py and coral_branching all emit PascalCase.
        //  Adding a lowercase entry beside the PascalCase ones would not just be untidy - it is
        //  actively unsafe, and the reason is the filesystem, not the switch:
        //
        //      coral slot 0 role "Tissue"  ->  MAT_Flora_Tissue.mat
        //      kelp  slot 0 role "tissue"  ->  MAT_Flora_tissue.mat
        //
        //  Both land in the SAME folder, and NTFS is case-insensitive, so those are ONE FILE. Two
        //  materials that must carry DIFFERENT master shaders - CoralMaster and KelpMaster, which
        //  share almost no property name - would silently overwrite each other, and whichever ran
        //  last would win with the other family's property block half-applied. Nothing would throw.
        //  A case-sensitive C# switch would additionally miss `tissue` while matching `Tissue`,
        //  which is the failure the lead flagged, but it is the milder of the two.
        //
        //  SETTLED: role tokens are PascalCase everywhere, and where the bare role is ambiguous
        //  ACROSS the Flora family the organism prefixes it. That is not a new invention - CapStem
        //  already does exactly this (`CapTissue`, `StemHoldfast` rather than `Tissue`, `Holdfast`),
        //  so kelp follows the established sibling rather than introducing a third style. law.py's
        //  `NAME_MATERIAL = "MAT_{family}_{role}"` template is preserved unchanged; only the {role}
        //  token is normalised.
        //
        //  RESIDUAL RISK, FOR THE LEAD, NOT FIXED HERE: `MAT_{family}_{role}` is NOT a unique key.
        //  Three different organisms share family "Flora", so the template can only stay unique as
        //  long as no two of them choose the same role word. Coral's bare `Tissue` and
        //  `EncrustingBase` are the exposed ones - the next Flora generator that emits `tissue` in
        //  any casing collides with coral on a case-insensitive filesystem. The durable fix is a
        //  three-token template or a per-organism subfolder in law.py, which is generator territory.
        //  Nothing is renamed here: no forge Flora material exists on disk yet (the only
        //  MAT_Flora_* asset in the project is MAT_Flora_ImpostorAtlas.mat), so the convention is
        //  being set before anything can be orphaned by it.
        // ══════════════════════════════════════════════════════════

        private static readonly string[] KelpSlotRoles =
        {
            "KelpTissue", "KelpBasalCollarScar", "KelpHoldfast"
        };

        /// <summary>
        /// Manifest role token -> the PascalCase token this binder uses, for the one generator whose
        /// casing differs. Exists so the mapping is EXPLICIT and greppable rather than implied by a
        /// hardcoded array somebody later compares against a manifest and finds mismatched.
        /// Kept as a flat pair list rather than a Dictionary: three entries, editor-cold, and the
        /// pairing is easier to read beside the manifest it mirrors.
        /// </summary>
        private static readonly string[] KelpManifestRoleToBinderRole =
        {
            "tissue", "KelpTissue",
            "basal_collar_scar", "KelpBasalCollarScar",
            "holdfast", "KelpHoldfast"
        };

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

            if (string.Equals(family, "Flora", StringComparison.Ordinal))
            {
                // 4.0 m per tile: `3DMODEL_FLORA_CORAL.md:77` sets hero harvestable flora at
                // 512 px/m, and these sources are 2048 px, so 2048 / 512 = 4.0 m of surface per
                // tile. Both organic packages are harvestable and near-camera - coral's alpha is
                // harvest_yield_mask at cameraDistanceClass "near", CapStem's is harvest_mask at
                // "near_interaction" - so both take the hero row, not the 256 px/m instanced row.
                // AuditOneMap prints the real pixel width, so if a source is not 2048 the
                // derivation is visibly wrong rather than quietly wrong.
                switch (role)
                {
                    // ---- Coral_Branching: no authored pigment exists, so the art drives colour --
                    case "Tissue":
                        return GeminiBiomeSet("gemini_biome_20260607_bioluminescent_coral_flesh", 4.0f,
                            "living colony tissue over stems/forks/shafts; coral's G channel carries a real biolum signal (measured max 0.7305 mean 0.1747)");
                    case "ExposedTipSkeleton":
                        return GeminiBiomeSet("gemini_biome_20260607_pale_tube_coral_calcium", 4.0f,
                            "bare pale axial skeleton at the branch ends - calcium, not flesh");
                    case "EncrustingBase":
                        return Batch34Set("b34_3401_photic_limestone_rubble_shelf", 4.0f,
                            "encrusting foot is calcified crust meeting rock; same source as Geology Primary at a different tile scale");

                    // ---- CapStem: pigment is AUTHORED and load-bearing, so NO albedo is bound ---
                    // flora_capstem.py:1500-1522 is explicit: "Colour is the deliverable here, not
                    // decoration... the amber cap against teal water is the whole reason the frame
                    // reads", with linear base colours chosen against nice_biome.webp and pushed
                    // toward saturated orange specifically to survive a teal fog volume. Multiplying
                    // a tiling jelly/coral albedo over that destroys the one thing the generator
                    // author measured. These sets are bound for NORMAL and MASK only - structure and
                    // wetness, which do not carry hue - and BindOrganicTextureSet suppresses the
                    // albedo for these three roles. That asymmetry with coral is deliberate.
                    case "CapTissue":
                    case "TornEdge":
                        return GeminiBiomeSet("gemini_biome_20260607_soft_jelly_membrane", 4.0f,
                            "membrane folds and pore structure for a thin translucent plate; NORMAL/MASK only, authored amber pigment is preserved");
                    case "StemHoldfast":
                        return GeminiBiomeSet("gemini_biome_20260607_living_kelp_frond_surface", 4.0f,
                            "fibrous anchoring tissue; NORMAL/MASK only, authored cream-ochre pigment is preserved");

                    // ---- Kelp: no authored pigment, so the art drives colour (as with coral) ----
                    // kelp.py passes only material NAMES to its manifest, no colour spec, unlike
                    // flora_capstem.py which carries reference-derived linear pigment per role. So
                    // kelp is in the "texture drives colour" camp and its _BaseColor/_TipColor become
                    // grading tints - see ApplyOrganicRole.
                    case "KelpTissue":
                        return GeminiBiomeSet("gemini_biome_20260607_living_kelp_frond_surface", 4.0f,
                            "living blade surface; the one source authored for exactly this organism");
                    case "KelpBasalCollarScar":
                        // HONEST REUSE: no scar/abscission source exists in the project. The collar
                        // scar IS kelp tissue, just abraded, so the frond set is the least wrong
                        // option and the role is separated by its property block rather than by a
                        // distinct map. Named here so the reuse is visible instead of looking like a
                        // dedicated set.
                        return GeminiBiomeSet("gemini_biome_20260607_living_kelp_frond_surface", 4.0f,
                            "REUSED frond set - no scar/abscission source exists; role differs by property block only");
                    case "KelpHoldfast":
                        return Batch34Set("b34_3402_shallow_seagrass_root_mat_substrate", 4.0f,
                            "root-mat substrate: a holdfast is an anchoring root mass, which is what this set depicts");
                }

                return null;
            }

            return null;
        }

        /// <summary>
        /// True for the roles whose base colour is authored art that a tiling albedo would destroy.
        /// Only the three CapStem roles qualify: <c>flora_capstem.py:1514-1534</c> carries
        /// reference-derived linear pigment per role (amber cap 0.855/0.360/0.070, rust-brown torn
        /// edge 0.330/0.115/0.040, cream-ochre stem 0.640/0.545/0.375). Coral_Branching passed NO
        /// materials to its manifest at all, so it has no pigment to protect and takes the texture.
        /// </summary>
        private static bool OrganicPigmentIsAuthored(string role)
        {
            return string.Equals(role, "CapTissue", StringComparison.Ordinal) ||
                   string.Equals(role, "TornEdge", StringComparison.Ordinal) ||
                   string.Equals(role, "StemHoldfast", StringComparison.Ordinal);
        }

        /// <summary>
        /// True for the three kelp roles. Kept as an explicit predicate rather than a
        /// <c>StartsWith("Kelp")</c> test: a prefix test would silently capture any future role that
        /// merely begins with those letters, and this file's whole failure mode is silent capture.
        /// </summary>
        private static bool RoleIsKelp(string role)
        {
            for (int i = 0; i < KelpSlotRoles.Length; i++)
            {
                if (string.Equals(role, KelpSlotRoles[i], StringComparison.Ordinal))
                    return true;
            }

            return false;
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
                false, 0.50172f, CoralShaderName, CoralShaderPath),
            new ForgePackage(
                "MANIFEST_Flora_Coral_Branching_1712.json",
                ForgePackageRoot + "/Flora/MESH_Flora_Coral_Branching_1712.fbx",
                "Flora", ForgeSurfaceClass.Organic, CoralSlotRoles,
                "manifest uvSummary.materialSlots + materialSlotRoles",
                false, 0.55f, CoralShaderName, CoralShaderPath),

            // ---- KELP, previously unreachable ------------------------------------------------
            // Three seeds of one organism sharing one material set, exactly like the three geology
            // rocks. These manifests are the TIER-2 generator-local shape (no `schema`, no
            // `productionReady`, `files.fbx` + `validation.allPassed` instead), which is why they
            // never appeared in the productionReady gap table - but this binder does not read
            // manifests, it reads the imported FBX and asserts the submesh count against the table,
            // so the manifest tier is irrelevant to whether kelp can bind.
            // No COL_ proxy: kelp is Flora, and 3DMODEL_FLORA_CORAL.md section 7 makes default flora
            // collision none (the manifest states `collision.kind: "none"` for the same reason).
            new ForgePackage(
                "MANIFEST_Flora_Kelp_s4021_q100.json",
                ForgePackageRoot + "/Flora/MESH_Flora_Kelp_s4021_q100.fbx",
                "Flora", ForgeSurfaceClass.Organic, KelpSlotRoles,
                "manifest materialSlots (lower_snake_case), normalised by KelpManifestRoleToBinderRole",
                false, 9.25863f, KelpShaderName, KelpShaderPath),
            new ForgePackage(
                "MANIFEST_Flora_Kelp_s4023_q100.json",
                ForgePackageRoot + "/Flora/MESH_Flora_Kelp_s4023_q100.fbx",
                "Flora", ForgeSurfaceClass.Organic, KelpSlotRoles,
                "manifest materialSlots (lower_snake_case), normalised by KelpManifestRoleToBinderRole",
                false, 9.25863f, KelpShaderName, KelpShaderPath),
            new ForgePackage(
                "MANIFEST_Flora_Kelp_s4025_q100.json",
                ForgePackageRoot + "/Flora/MESH_Flora_Kelp_s4025_q100.fbx",
                "Flora", ForgeSurfaceClass.Organic, KelpSlotRoles,
                "manifest materialSlots (lower_snake_case), normalised by KelpManifestRoleToBinderRole",
                false, 9.25863f, KelpShaderName, KelpShaderPath)
        };

        // ══════════════════════════════════════════════════════════
        //  COMPILE PROOF - how to prove this file actually built
        //
        //  This file has no Unity proof of its own and cannot get one from the lock-free dotnet
        //  gate: `CONTRIBUTING.md` records that the gate emits FALSE CS0433/CS0656 against
        //  Hecton8.Editor.csproj, so for Editor-assembly code only a Unity batchmode/editor build
        //  counts. The cheap substitute is to probe the built assembly for a symbol this file
        //  introduces, with controls in both directions:
        //
        //    D=Library/ScriptAssemblies/Hecton8.Editor.dll
        //    grep -ac ForgeGeneratedMaterialAuthoring        $D   # this type            expect >0
        //    grep -ac ApplyOrganicRole                       $D   # organic branch       expect >0
        //    grep -ac BindOrganicTextureSet                  $D   # organic branch       expect >0
        //    grep -ac ModuleHardSurfaceWearMaterialAuthoring  $D   # KNOWN-PRESENT control expect >0
        //    grep -ac H8FORGEMAT_CONTROL_MUST_BE_ABSENT      $D   # KNOWN-ABSENT control expect 0
        //
        //  The known-absent control appears in THIS COMMENT and nowhere else, which does not
        //  weaken it: C# discards comments in the lexer, so only identifiers, string literals and
        //  metadata reach the assembly. Same evidence proves it - the identifier
        //  ApplyForgeMaterialPrefabBinding reached the DLL from this file while none of the prose
        //  around it did. If this control ever returns non-zero, the probe method is broken and
        //  every positive result above it is suspect.
        //
        //  ENCODING TRAP, measured on this DLL 2026-07-29 and worth more than the result itself.
        //  `grep -ac H8FORGEMAT` returns 0 while `grep -ac 'H.8.F.O.R.G.E.M.A.T'` returns 2 on the
        //  same file. Type, method and field names live in the UTF-8 #Strings metadata heap and
        //  grep directly; STRING LITERALS live in the UTF-16 #US heap, so every ASCII char is
        //  followed by a NUL and a plain grep misses them. A probe aimed at a `const string`
        //  therefore reports 0 on an assembly that definitely contains it - a false negative that
        //  reads exactly like "my code did not compile". Probe IDENTIFIERS, not literals, or use
        //  the dotted pattern.
        //
        //  MEASURED STATE at Hecton8.Editor.dll mtime 2026-07-29 11:08:
        //    ForgeGeneratedMaterialAuthoring 3, ApplyForgeMaterialPrefabBinding 1  -> the
        //      pre-organic revision of this file COMPILED CLEAN into the Editor assembly.
        //    ApplyOrganicRole 0, BindOrganicTextureSet 0, OrganicPigmentIsAuthored 0,
        //      CoralMeasuredSwayMean 0, CapStemMeasuredSwayMean 0, BiomeHashAssumedMean 0
        //      -> the ORGANIC BRANCH IS NOT IN ANY BUILD YET. It is static review only.
        //    Controls behaved: known-present 3, known-absent 0, so the method has no false
        //      positives on this assembly.
        // ══════════════════════════════════════════════════════════

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
                  .Append(" roles=13 sourceSetsResolved=12 sourceSetsMissing=1(InstrumentGlass)")
                  .Append(" albedoSuppressedRoles=3(CapStem-authored-pigment)");
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

            OrganicGateSet organic = EvaluateOrganicGates();
            AppendOrganicGates(report, organic);

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

                bool organicPackage = package.Surface == ForgeSurfaceClass.Organic;
                OrganicGate gate = SelectGate(organic, package);

                if (organicPackage && !gate.Allowed)
                {
                    refused += package.SlotRoles.Length;
                    report.Append("  ").Append(TokenBlocked).Append(' ').Append(package.Family)
                          .Append(' ').Append(System.IO.Path.GetFileName(package.FbxAssetPath))
                          .Append(" slots=").Append(package.SlotRoles.Length)
                          .Append(" master=").Append(gate.ShaderName)
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

                // PER-PACKAGE master resolve. This line is the fix: it used to be one unconditional
                // CoralMaster, which made KelpMaster unreachable regardless of any other table.
                Shader master = organicPackage
                    ? ResolveShader(package.OrganicMasterName, package.OrganicMasterPath)
                    : hardSurface;
                if (master == null)
                {
                    refused += package.SlotRoles.Length;
                    report.Append("  ").Append(TokenFail).Append(' ').Append(package.Family)
                          .Append(" master shader '")
                          .Append(organicPackage ? package.OrganicMasterName : HardSurfaceShaderName)
                          .Append("' unresolved; nothing written for this package.")
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
                        Material fresh = new Material(master) { name = materialName };
                        ApplyRoleProperties(fresh, package.Family, package.SlotRoles[slot]);
                        AssetDatabase.CreateAsset(fresh, materialPath);
                        created++;
                        report.Append("  CREATE   ").Append(materialPath)
                              .Append(" shader=").Append(master.name).AppendLine();
                        continue;
                    }

                    if (existing.shader != master)
                    {
                        // Assigning material.shader drops every property the new shader does not
                        // declare, so it is done before the property write, never after. Same
                        // ordering constraint ModuleHardSurfaceWearMaterialAuthoring.cs:260-264
                        // documents on the module migrator.
                        existing.shader = master;
                    }

                    ApplyRoleProperties(existing, package.Family, package.SlotRoles[slot]);
                    EditorUtility.SetDirty(existing);
                    updated++;
                    report.Append("  UPDATE   ").Append(materialPath)
                          .Append(" shader=").Append(master.name).AppendLine();
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
            OrganicGateSet organic = EvaluateOrganicGates();
            AppendOrganicGates(report, organic);

            int written = 0;
            int skipped = 0;

            for (int i = 0; i < Packages.Length; i++)
            {
                ForgePackage package = Packages[i];
                OrganicGate gate = SelectGate(organic, package);
                if (package.Surface == ForgeSurfaceClass.Organic && !gate.Allowed)
                {
                    skipped++;
                    report.Append("  ").Append(TokenBlocked).Append(' ')
                          .Append(System.IO.Path.GetFileName(package.FbxAssetPath))
                          .Append(" master=").Append(gate.ShaderName)
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
                        audit.TextureSetResolveFailures == 0 &&
                        audit.KelpUv1Failures == 0;

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
                  .Append(" kelpUv1Failures=").Append(audit.KelpUv1Failures)
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
            public int KelpUv1Failures;
        }

        private static AuditResult Audit(StringBuilder report)
        {
            AuditResult result = default;
            OrganicGateSet organic = EvaluateOrganicGates();
            AppendOrganicGates(report, organic);

            Shader hardSurface = ResolveShader(HardSurfaceShaderName, HardSurfaceShaderPath);
            Shader coralMaster = ResolveShader(CoralShaderName, CoralShaderPath);
            Shader kelpMaster = ResolveShader(KelpShaderName, KelpShaderPath);
            report.Append("  master hardSurface=")
                  .Append(hardSurface != null ? hardSurface.name : "MISSING")
                  .Append(" | organic coral=")
                  .Append(coralMaster != null ? coralMaster.name : "MISSING")
                  .Append(" | organic kelp=")
                  .Append(kelpMaster != null ? kelpMaster.name : "MISSING")
                  .AppendLine();

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

                OrganicGate packageGate = SelectGate(organic, package);
                if (package.Surface == ForgeSurfaceClass.Organic && !packageGate.Allowed)
                    result.PackagesBlocked++;

                for (int slot = 0; slot < package.SlotRoles.Length; slot++)
                {
                    string materialName = MaterialName(package.Family, package.SlotRoles[slot]);
                    if (!seenMaterials.Add(materialName))
                        continue;

                    result.MaterialsExpected++;
                    // Organic becomes bindable the moment ITS OWN master's two-part gate clears. Both
                    // the bindable flag and the expected shader now come from the package rather than
                    // from a CoralMaster constant, so kelp is judged against KelpMaster and neither
                    // this line nor the table needs editing when a token lands.
                    bool organicRole = package.Surface == ForgeSurfaceClass.Organic;
                    bool bindable = !organicRole || packageGate.Allowed;
                    string expectedShader = organicRole
                        ? package.OrganicMasterName
                        : HardSurfaceShaderName;
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
                        !string.Equals(shaderName, expectedShader, StringComparison.Ordinal);
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

                // KELP-ONLY STRUCTURAL PRECONDITION, and it is not cosmetic.
                // Hecton_KelpMaster.shader:326 declares `float2 uvMask : TEXCOORD1` and :545-546
                // derive heightMask/widthMask from it - the root-to-tip and blade-margin
                // parameterisation that drives sway, the midrib, the edge band and the
                // _BaseColor->_TipColor gradient. On a mesh without TEXCOORD1 every one of those
                // reads 0: sway collapses to the root value, midribMask goes to 0, edgeMask
                // saturates to 1 so the whole surface reads as blade EDGE, and the tip lightening
                // disappears. Nothing errors. The shader's own comment at :537-543 records 472
                // existing kelp meshes with TexCoord1 dimension 0 for exactly this reason, so this
                // is a measured failure mode and not a hypothetical.
                bool kelpPackage = string.Equals(
                    package.OrganicMasterName, KelpShaderName, StringComparison.Ordinal);
                bool missingUv1 = kelpPackage &&
                                  !mesh.HasVertexAttribute(VertexAttribute.TexCoord1);
                if (missingUv1)
                    result.KelpUv1Failures++;

                report.Append("      MESH ").Append(name)
                      .Append(" submeshes=").Append(mesh.subMeshCount)
                      .Append('/').Append(package.SlotRoles.Length)
                      .Append(submeshMismatch ? " " + TokenFail + "-SUBMESH-SLOT-MISMATCH" : string.Empty)
                      .Append(" tris=").Append(TriangleCount(mesh))
                      .Append(" uvChannels=").Append(CountUvChannels(mesh))
                      .Append(missingUv1
                          ? " " + TokenFail + "-KELP-NEEDS-TEXCOORD1(UVMask); every mask reads 0"
                          : string.Empty)
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
            public bool TokenFound;
            public string[] BadReadsFound;
            /// <summary>Which shader this verdict is about. Never assume, always print.</summary>
            public string ShaderName;
            public string ShaderPath;
        }

        /// <summary>
        /// Evaluates the channel-contract gate against the shader it is ACTUALLY ABOUT.
        ///
        /// This used to read <c>CoralShaderPath</c> unconditionally, which made it global-to-organic
        /// and keyed entirely on CoralMaster's source. The consequence was worse than an incomplete
        /// check: it would have reported a verdict about CoralMaster while binding kelp, and adding
        /// the opt-in token to Hecton_KelpMaster.shader would have done NOTHING because the gate
        /// never opened that file - falsely implying kelp was gated when it was merely unreachable.
        /// A sibling correctly declined to add the token for exactly that reason. The gate now takes
        /// its subject as a parameter and carries the subject's identity in the result.
        /// </summary>
        private static OrganicGate EvaluateOrganicGate(
            string shaderName,
            string shaderPath,
            string[] knownBadReads)
        {
            OrganicGate gate = default;
            gate.BadReadsFound = Array.Empty<string>();
            gate.ShaderName = shaderName;
            gate.ShaderPath = shaderPath;

            string absolute = ResolveProjectAbsolutePath(shaderPath);
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
            for (int i = 0; i < knownBadReads.Length; i++)
            {
                if (source.IndexOf(knownBadReads[i], StringComparison.Ordinal) >= 0)
                    bad.Add(knownBadReads[i]);
            }

            gate.BadReadsFound = bad.ToArray();
            gate.TokenFound = source.IndexOf(OrganicContractOptInToken, StringComparison.Ordinal) >= 0;

            // TWO-PART GATE, and the second half is what makes it evidence instead of assertion.
            // The token search is a plain substring match, so a COMMENT containing the token would
            // satisfy it on its own - somebody could write "// H8_ORGANIC_VCOL_CONTRACT_OK"
            // and unlock six materials by accident. ANDing on "and none of the known-bad reads is
            // still in the file" costs one comparison and means the claim and the evidence have to
            // agree. It is still not a parse: a THIRD wrong read nobody has catalogued would pass
            // both halves. This gate proves the two failures that were measured are gone, not that
            // the shader is correct.
            gate.Allowed = gate.TokenFound && bad.Count == 0;
            return gate;
        }

        private static void AppendOrganicGate(StringBuilder report, in OrganicGate gate)
        {
            report.Append("  organic gate: shader=").Append(gate.ShaderName)
                  .Append(" present=").Append(gate.ShaderPresent ? "YES" : "NO")
                  .Append(" optInToken=").Append(gate.TokenFound ? "FOUND" : "ABSENT")
                  .Append(" knownBadReadsStillPresent=").Append(gate.BadReadsFound.Length)
                  .Append(" verdict=").Append(gate.Allowed ? "ALLOW" : TokenBlocked)
                  .AppendLine();

            for (int i = 0; i < gate.BadReadsFound.Length; i++)
            {
                report.Append("      non-compliant read still present in ").Append(gate.ShaderPath)
                      .Append(": ").Append(gate.BadReadsFound[i]).AppendLine();
            }

            if (!gate.Allowed)
            {
                report.Append("      Organic binding stays refused. 3dmodel.md:132-137 fixes ")
                      .Append("R=sway G=biolum B=AO A=family. Both halves must hold: the literal ")
                      .Append("token ").Append(OrganicContractOptInToken)
                      .Append(" present AND zero known-bad reads. Token alone is not enough - a ")
                      .Append("comment would satisfy a substring search.").AppendLine();
            }
        }

        /// <summary>
        /// One verdict per organic master. Held together so a run reports the state of BOTH shaders
        /// even when only one family is being bound - a per-family gate that only printed the family
        /// in hand would hide the other's status, which is how kelp stayed invisible before.
        /// </summary>
        private struct OrganicGateSet
        {
            public OrganicGate Coral;
            public OrganicGate Kelp;
        }

        private static OrganicGateSet EvaluateOrganicGates()
        {
            OrganicGateSet set = default;
            set.Coral = EvaluateOrganicGate(CoralShaderName, CoralShaderPath, CoralKnownBadReads);
            set.Kelp = EvaluateOrganicGate(KelpShaderName, KelpShaderPath, KelpKnownBadReads);
            return set;
        }

        private static void AppendOrganicGates(StringBuilder report, in OrganicGateSet set)
        {
            AppendOrganicGate(report, set.Coral);
            AppendOrganicGate(report, set.Kelp);
        }

        /// <summary>
        /// Selects the verdict for one package by the master it declares. Hard-surface packages have
        /// no organic master and get a permanently-disallowed default, which is correct: they never
        /// consult this gate.
        /// </summary>
        private static OrganicGate SelectGate(in OrganicGateSet set, ForgePackage package)
        {
            if (string.Equals(package.OrganicMasterName, KelpShaderName, StringComparison.Ordinal))
                return set.Kelp;
            if (string.Equals(package.OrganicMasterName, CoralShaderName, StringComparison.Ordinal))
                return set.Coral;

            OrganicGate none = default;
            none.BadReadsFound = Array.Empty<string>();
            none.ShaderName = "<none>";
            none.ShaderPath = "<none>";
            return none;
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

            if (string.Equals(family, "Flora", StringComparison.Ordinal))
            {
                ApplyOrganicRole(material, role);
                material.SetOverrideTag("RenderType", "Opaque");
                material.renderQueue = (int)RenderQueue.Geometry;
                material.enableInstancing = true;
                return;
            }

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
        //  ORGANIC ROLE AUTHORING  --  Hecton8/Flora/CoralMaster
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Coral's measured sway-channel mean, from the forge manifest's own render-tile statistics
        /// (<c>MANIFEST_Flora_Coral_Branching_1712.json</c> extra.channelMeasurements,
        /// <c>chan0_sway_amplitude</c>: min 0.0, max 0.12743773, mean 0.03983368). The max lands on
        /// 32.5/255, which is <c>law.py:298 SWAY_RIGID_MINERAL_MAX = 32/255</c> - the contract cap
        /// for mineralised coral. So R on this asset is structurally tiny, and that is what makes the
        /// tint calibration below necessary rather than cosmetic.
        /// </summary>
        private const float CoralMeasuredSwayMean = 0.03983368f;

        /// <summary>
        /// CapStem's measured sway-channel mean (<c>MANIFEST_Flora_CapStem_1811_00.json</c>
        /// extra.channelMeasurements <c>sway_amplitude</c>: min 0.0, max 1.0, mean 0.53846). A cap on
        /// a flexible stalk is NOT mineral-capped - it uses the full flexible-tip band - so its mean
        /// is 13.5x coral's. This is why one shared tint constant is wrong.
        /// </summary>
        private const float CapStemMeasuredSwayMean = 0.53846f;

        /// <summary>
        /// Kelp's measured sway-channel mean, read directly from the generator's own attribute dump:
        /// <c>MANIFEST_Flora_Kelp_s4021_q100.json</c>
        /// <c>vertexColour.directAttributeRead.sway_amplitude</c> = min 0.0, max 1.0, mean 0.45855
        /// over 17418 CORNER elements, with <c>swayUniform: false</c> and
        /// <c>swayStiffnessExponent: 1.25</c> (law.py's FLEXIBLE_BLADE exponent).
        ///
        /// Kelp uses the FULL 0..1 band. It is not rigid-capped the way coral is - coral is confined
        /// to 0..0.1274 by <c>law.py:298 SWAY_RIGID_MINERAL_MAX = 32/255</c> - so coral's arithmetic
        /// cannot transfer, and this is the third independently-derived value from one formula:
        ///     coral   0.03983 -> 0.0590      (mineral-capped, 12.6x correction)
        ///     capstem 0.53846 -> 0.7969
        ///     kelp    0.45855 -> 0.7337
        /// Kelp's correction is the mildest of the three, and worth noticing WHY rather than treating
        /// it as luck: kelp's measured mean 0.4586 happens to sit close to biomeHash's 0.5, so kelp's
        /// migration off the vertex stream was nearly appearance-neutral ALREADY. Coral's was 12.6x
        /// off for the same reason in reverse. The formula is what makes both visible.
        /// </summary>
        private const float KelpMeasuredSwayMean = 0.45855f;

        /// <summary>
        /// <c>_VertexTintStrength</c> as each master shader SHIPS it. These are not the same number,
        /// and using one for both would silently mis-derive the other family: CoralMaster.shader:31
        /// declares 0.74, KelpMaster.shader:28 declares 0.8. The old value is half the derivation, so
        /// it has to come from the shader the material will actually carry.
        /// </summary>
        private const float CoralShaderDefaultTintStrength = 0.74f;
        private const float KelpShaderDefaultTintStrength = 0.8f;

        /// <summary>
        /// Mean of <c>biomeHash</c>, the replacement driver. Hecton_CoralMaster.shader:483 computes
        /// <c>HectonCoreLitHash12(floor(biomeAup.xz * 0.03125))</c> - a hash of the AUP cell index at
        /// 1/32, i.e. one value per 32-metre cell. ASSUMED uniform on [0,1] with mean 0.5, which is
        /// the standard contract for a frac-based hash. NOT MEASURED: I have not sampled
        /// HectonCoreLitHash12's actual distribution, and if it is biased the calibration below
        /// shifts by exactly that bias.
        /// </summary>
        private const float BiomeHashAssumedMean = 0.5f;

        /// <summary>
        /// Writes the per-role response for <c>Hecton8/Flora/CoralMaster</c>.
        ///
        /// ═══ WHY THIS METHOD EXISTS SEPARATELY ═══
        /// CoralMaster shares NO property with Hecton_ModuleHardSurfaceLit. Its normal slot is
        /// <c>_NormalMap</c> not <c>_BumpMap</c>, it has no <c>_ParallaxMap</c>, no
        /// <c>_Module*</c> vectors, and - decisively - it samples every map TRIPLANAR from world
        /// position through <c>ResolveFloraDominantAxisProjection</c>
        /// (Hecton_CoralMaster.shader:176-196, <c>uv = positionWS.zy * _TriplanarScale</c>). So
        /// <c>Material.SetTextureScale</c> is INERT on this shader and the scale knob is
        /// <c>_TriplanarScale</c> in tiles-per-metre. One upside falls out of that: world-space
        /// triplanar is size-independent, so the organic pair gets correct texel density from a
        /// single shared material - which is exactly the problem geology has and cannot solve
        /// without a shader change.
        ///
        /// ═══ THE _MaskMap BINDING IS LOAD-BEARING, NOT DECORATION ═══
        /// Leaving <c>_MaskMap</c> at its <c>"white"</c> default is not neutral, it is actively
        /// destructive, and it silently voids the tint calibration this method exists to apply:
        ///   * <c>:542 accent = lerp(_BaseColor, _AccentColor, saturate(maskSample.r + tintMask*0.48))</c>
        ///     - with maskSample.r = 1 the saturate pins to 1 for ANY tintMask, so
        ///     <c>_VertexTintStrength</c> would have LITERALLY NO EFFECT on the render and the
        ///     surface would sit permanently at full <c>_AccentColor</c>.
        ///   * <c>:536 wetness = saturate(maskSample.g * (1 + _MoistureBoost) + ...)</c> - with
        ///     maskSample.g = 1 wetness saturates regardless, and <c>:539
        ///     roughness = lerp(0.7, 0.2, wetness)</c> pins roughness to 0.2. Every organic surface
        ///     would read as uniformly soaking-wet gloss. No material knob can scale maskSample.g
        ///     down, so this is unfixable from a .mat.
        /// So the mask must be bound.
        ///
        /// ═══ DECLARED CHANNEL REMAP, because it is NOT a match ═══
        /// CoralMaster's mask semantic is its own: R = pigment/tint variation, G = wetness,
        /// B and A = thickness via <c>_ThicknessStrength</c>, A also feeds the caustic mask. NO
        /// texture in this project packs that. The project's 138-file standard
        /// <c>_MaskMap_UnityURP</c> packs R = Metallic, G = Occlusion, A = Smoothness. Bound here it
        /// reads as:
        ///   R metallic  -> tint variation. Organic sources are non-metallic, so R is near 0, which
        ///                  leaves the vertex/biome tint term as the ONLY thing moving base->accent.
        ///                  That is the reason this packing is chosen over
        ///                  _ARM_AO_Rough_Metal: ARM's R is AO, which is high over most of the
        ///                  surface and would re-saturate the lerp and void the calibration again.
        ///   G occlusion -> wetness. Exposed surfaces wetter, silted crevices matter. Defensible for
        ///                  a current-swept reef; NOT the authored intent of either channel.
        ///   A smoothness-> thickness. Weakest of the three. Thin translucent tissue is smoother than
        ///                  thick calcified mass, so the correlation has the right sign.
        /// This is a REMAP, not a contract match. The correct fix is an offline bake that packs
        /// CoralMaster's real semantic - derivable from the mesh plus these maps - which is a
        /// generator job, not a material job. Logged, not hidden.
        /// </summary>
        private static void ApplyOrganicRole(Material material, string role)
        {
            BoundMaps maps = BindOrganicTextureSet(material, role);

            // ═══════════════════════════════════════════════════════════════════════════════
            //  _VertexTintStrength  --  MEASURED, NOT CHOSEN
            //
            //  Hecton_CoralMaster.shader:484 previously read the tint driver off the vertex
            //  stream and now reads it off the biome hash:
            //      was:  tintMask = saturate(input.color.r) * _VertexTintStrength
            //      now:  tintMask = saturate(biomeHash * _VertexTintStrength)
            //  The DRIVER's magnitude changed by an order of magnitude, so keeping the material
            //  value would silently change the look. Derivation, for coral:
            //      today's mean tintMask  = swayMean * 0.74      = 0.03983 * 0.74 = 0.02948
            //      new mean at strength S = biomeHashMean * S    = 0.5 * S
            //      appearance-neutral S   = 0.02948 / 0.5        = 0.0590
            //  Cross-check against the downstream term, which carries a further *0.48 at :542:
            //      before: mean 0.03983*0.74*0.48 = 0.01414,  max 0.12744*0.74*0.48 = 0.04527
            //      at 0.74 unchanged: mean 0.5*0.74*0.48 = 0.1776  ->  12.6x the old mean, a
            //      visible warm lift across the whole photic reef.
            //      at 0.059:          mean 0.5*0.059*0.48 = 0.01416  ->  matches 0.01414.
            //  So 0.059 is appearance-neutral IN THE MEAN by construction, and it is checkable
            //  from the manifest without rerunning anything.
            //
            //  RESIDUAL RISK, WRITTEN DOWN RATHER THAN CLAIMED AWAY. Appearance-neutral in the
            //  mean is NOT appearance-neutral:
            //    1. The DISTRIBUTION is new. The old driver was a smooth per-vertex field rising
            //       root-to-tip; the new one is a piecewise-constant hash over 32-metre AUP cells
            //       (:483 floor(xz * 0.03125)). Within one cell every colony now shares one tint
            //       and neighbouring cells step discontinuously. Variance moved from within-asset
            //       to between-cell. That is a genuinely new visual behaviour and NOTHING has
            //       rendered it.
            //    2. The old spread was 0..0.0453; the new spread is 0..0.0283 at S=0.059
            //       (max biomeHash 1.0 * 0.059 * 0.48). Peak tint drops ~38 percent even though
            //       the mean holds, so the brightest tips lose contrast against their own stems.
            //    3. BiomeHashAssumedMean = 0.5 is an ASSUMPTION about HectonCoreLitHash12, not a
            //       measurement. A biased hash scales every number above by that bias.
            //    4. These materials land in a binary production scene that cannot be opened or
            //       diffed outside Unity, so the first real look at this is a running editor.
            //  I would rather ship a change whose residual is written down than one that claims
            //  to be safe.
            // ═══════════════════════════════════════════════════════════════════════════════
            bool isKelp = RoleIsKelp(role);
            float measuredSwayMean = isKelp
                ? KelpMeasuredSwayMean
                : OrganicPigmentIsAuthored(role)
                    ? CapStemMeasuredSwayMean
                    : CoralMeasuredSwayMean;

            // The OLD multiplier must also come from the shader the material will carry:
            // CoralMaster ships _VertexTintStrength at 0.74, KelpMaster at 0.8. Using one for both
            // mis-derives the other by 8 percent - small, but it is exactly the kind of silent
            // arithmetic slip this whole derivation exists to prevent.
            float shaderDefaultTintStrength = isKelp
                ? KelpShaderDefaultTintStrength
                : CoralShaderDefaultTintStrength;

            // PER-FAMILY, and this is a correction to the single shared constant. 0.059 is
            // calibrated to coral's mineral-CAPPED R (mean 0.0398, cap 32/255). CapStem is a
            // flexible-tip organism with measured R mean 0.53846 - 13.5x coral - so applying
            // coral's 0.059 to it would cut its tint contribution from 0.1912 to 0.0142, a 13.5x
            // REDUCTION that deletes pigment variation the generator author deliberately authored
            // against nice_biome.webp. Same formula, different measured input, different answer:
            //   coral   0.03983 * 0.74 / 0.5 -> 0.0590
            //   capstem 0.53846 * 0.74 / 0.5 -> 0.7969
            //   kelp    0.45855 * 0.80 / 0.5 -> 0.7337   (KelpMaster.shader:643 tintMask, :28 default)
            // CapStem's and kelp's values land near or above their shader defaults purely because
            // their measured means sit near biomeHash's 0.5. _VertexTintStrength is Range(0, 2) on
            // both shaders, so all three fit.
            //
            // KELP-SPECIFIC NOTE ON WHY THIS ONE IS ALREADY LIVE. Coral's tint feeds
            // `saturate(maskSample.r + tintMask * 0.48)` (CoralMaster:542), so an unbound white mask
            // pins that term to 1 and voids the calibration entirely - which is why coral MUST have
            // its mask bound. Kelp's feeds `tintMask + maskSample.b * 0.08` (KelpMaster:644), an ADD
            // whose mask contribution caps at 0.08, so kelp's _VertexTintStrength is live even with
            // an unbound mask, and there is no *0.48 attenuation either. Same formula, different
            // downstream sensitivity; kelp's mask is still bound below for the wetness term.
            float vertexTintStrength = Mathf.Clamp(
                measuredSwayMean * shaderDefaultTintStrength / BiomeHashAssumedMean, 0f, 2f);
            SetFloat(material, "_VertexTintStrength", vertexTintStrength);

            // _MoistureBoost is deliberately NOT written on either master. CoralMaster ships 0.14 and
            // KelpMaster 0.22; both shift wetness by a few percent and both are the value the shader
            // author chose. Writing a number equal to the default would only create the illusion that
            // it was calibrated.

            // Triplanar scale, in TILES PER METRE: 1 / metresPerTile, so a 4.0 m tile is 0.25.
            // Derived from the bible's texel density, not tuned by eye. Range(0.05, 4).
            if (maps.Tiling > 0.0001f)
                SetFloat(material, "_TriplanarScale", Mathf.Clamp(maps.Tiling, 0.05f, 4f));

            switch (role)
            {
                // ---- Coral_Branching --------------------------------------------------------
                // Albedo is bound, and :544 computes albedo = accent * baseTex * moistureTint *
                // ageTint, so _BaseColor and _AccentColor MULTIPLY the sampled art. They must be
                // grading tints near white here; the shader's own defaults (0.54/0.32/0.28 and
                // 0.82/0.58/0.42) are absolute coral colours meant for a textureless material and
                // would double-darken a real albedo.
                case "Tissue":
                    SetColor(material, "_BaseColor", new Color(0.880f, 0.855f, 0.840f, 1f));
                    SetColor(material, "_AccentColor", new Color(1.000f, 0.960f, 0.910f, 1f));
                    SetColor(material, "_SubsurfaceColor", new Color(0.94f, 0.62f, 0.48f, 1f));
                    SetFloat(material, "_SubsurfaceStrength", 0.52f);
                    SetFloat(material, "_Smoothness", 0.34f);
                    SetFloat(material, "_CavityStrength", 0.62f);
                    // G is a REAL signal on this asset - measured max 0.7305, mean 0.1747 - and
                    // _BiolumStrength ships at 0, so the baked biolum mask renders as nothing
                    // unless it is raised. That is a second dead channel next to the tint one.
                    // 1.1 is a deliberately conservative first value: enough for the organ to read
                    // in a dark photic reef, low enough that a 0.73 peak does not blow out. It is a
                    // JUDGEMENT, not a measurement, and no render exists for it.
                    SetFloat(material, "_BiolumStrength", 1.1f);
                    SetFloat(material, "_BiolumMaskStrength", 1f);
                    SetColor(material, "_BiolumColor", new Color(0.26f, 0.95f, 0.84f, 1f));
                    break;

                case "ExposedTipSkeleton":
                    // Bare calcium: paler, drier, no organ. Skeleton does not glow.
                    SetColor(material, "_BaseColor", new Color(0.930f, 0.925f, 0.900f, 1f));
                    SetColor(material, "_AccentColor", new Color(1.000f, 0.995f, 0.975f, 1f));
                    SetFloat(material, "_SubsurfaceStrength", 0.14f);
                    SetFloat(material, "_Smoothness", 0.26f);
                    SetFloat(material, "_CavityStrength", 0.45f);
                    SetFloat(material, "_BiolumStrength", 0f);
                    break;

                case "EncrustingBase":
                    // Calcified crust against rock: darker, matter, silted.
                    SetColor(material, "_BaseColor", new Color(0.760f, 0.755f, 0.730f, 1f));
                    SetColor(material, "_AccentColor", new Color(0.880f, 0.865f, 0.820f, 1f));
                    SetFloat(material, "_SubsurfaceStrength", 0.08f);
                    SetFloat(material, "_Smoothness", 0.20f);
                    SetFloat(material, "_CavityStrength", 0.75f);
                    SetFloat(material, "_BiolumStrength", 0f);
                    break;

                // ---- CapStem: authored pigment, NO albedo bound -----------------------------
                // _BaseMap stays unbound, so it samples "white" and albedo = accent * 1. That makes
                // _BaseColor/_AccentColor ABSOLUTE again, which is the only reason the linear values
                // from flora_capstem.py can be carried across verbatim. _AccentColor is a lighter,
                // warmer excursion of the SAME pigment rather than a different hue, so the
                // per-32-m-cell biomeHash lerp varies within the authored family instead of drifting
                // toward an unrelated colour.
                case "CapTissue":
                    // flora_capstem.py:1515-1521: base_color (0.855, 0.360, 0.070) linear,
                    // roughness 0.31, subsurface 0.22, subsurface_radius (0.020, 0.009, 0.004),
                    // ior 1.38. "a saturated warm amber top", pushed toward orange because water
                    // absorbs long wavelengths first and ochre would go grey-green in metres.
                    SetColor(material, "_BaseColor", new Color(0.855f, 0.360f, 0.070f, 1f));
                    SetColor(material, "_AccentColor", new Color(0.940f, 0.470f, 0.120f, 1f));
                    SetColor(material, "_SubsurfaceColor", new Color(0.960f, 0.520f, 0.180f, 1f));
                    // Blender roughness 0.31 -> smoothness 0.69. Converted, not re-guessed.
                    SetFloat(material, "_Smoothness", 0.69f);
                    SetFloat(material, "_SubsurfaceStrength", 0.62f);
                    SetFloat(material, "_ThicknessStrength", 0.78f);
                    SetFloat(material, "_CavityStrength", 0.48f);
                    // Measured G max 0.0003, mean 0.00002 - CapStem has NO bioluminescent organ.
                    // Raising this would invent light the generator never baked.
                    SetFloat(material, "_BiolumStrength", 0f);
                    break;

                case "TornEdge":
                    // flora_capstem.py:1523-1529: (0.330, 0.115, 0.040), roughness 0.52
                    // ("a torn edge is fibrous, not glossy"), subsurface 0.10.
                    SetColor(material, "_BaseColor", new Color(0.330f, 0.115f, 0.040f, 1f));
                    SetColor(material, "_AccentColor", new Color(0.420f, 0.170f, 0.065f, 1f));
                    SetColor(material, "_SubsurfaceColor", new Color(0.560f, 0.230f, 0.090f, 1f));
                    SetFloat(material, "_Smoothness", 0.48f);
                    SetFloat(material, "_SubsurfaceStrength", 0.28f);
                    SetFloat(material, "_CavityStrength", 0.70f);
                    SetFloat(material, "_BiolumStrength", 0f);
                    break;

                // ---- Kelp: Hecton8/Flora/KelpMaster, a DIFFERENT property set ----------------
                // The accent slot is _TipColor, not _AccentColor, and KelpMaster:636 composes
                // `gradient = lerp(_BaseColor, _TipColor, heightMask)` then :642
                // `albedo = gradient * baseTex * moistureTint * ageTint * detailMask`. So both
                // colours MULTIPLY the sampled art and must be near-white grading tints, exactly as
                // on coral - the shader's own defaults (0.16/0.46/0.24 and 0.34/0.74/0.42) are
                // absolute greens meant for a textureless material.
                //
                // heightMask is `input.uvMask.y`, i.e. TEXCOORD1 - the root-to-tip parameterisation.
                // On the forge kelp FBX that channel exists (manifest maskUv.layer "UVMask",
                // texcoordIndex 1). On a mesh without it, heightMask reads 0, the gradient collapses
                // to _BaseColor everywhere and the tip lightening silently vanishes. The audit
                // asserts TEXCOORD1 presence for exactly this reason.
                //
                // _BiolumStrength STAYS 0 for every kelp role - not an oversight, and deliberately
                // not copied from coral tissue's 1.1. Kelp's G channel is measured min 0.0 / max 0.0
                // / mean 0.0 with manifest biolumPolicy "authored 0 everywhere; photic-zone kelp has
                // no emissive organ (3DMODEL_FLORA_CORAL.md section 2)". Raising it would scale a
                // genuinely empty channel and could only invent light the generator never baked.
                case "KelpTissue":
                    SetColor(material, "_BaseColor", new Color(0.820f, 0.855f, 0.800f, 1f));
                    SetColor(material, "_TipColor", new Color(0.960f, 0.985f, 0.930f, 1f));
                    SetColor(material, "_TransmissionColor", new Color(0.26f, 0.68f, 0.34f, 1f));
                    SetColor(material, "_SSSColor", new Color(0.45f, 0.82f, 0.38f, 1f));
                    // A blade is a thin translucent sheet, so transmission and SSS carry it; both are
                    // lifted above the shader default because the forge blade is genuinely thin.
                    SetFloat(material, "_TransmissionStrength", 0.78f);
                    SetFloat(material, "_SSSStrength", 1.6f);
                    SetFloat(material, "_ThicknessStrength", 0.55f);
                    SetFloat(material, "_Smoothness", 0.90f);
                    SetFloat(material, "_MidribDarkening", 0.22f);
                    SetFloat(material, "_BiolumStrength", 0f);
                    break;

                case "KelpBasalCollarScar":
                    // Abraded, thickened, non-translucent: an old wound, not living blade. Darker and
                    // matter than the tissue, with transmission close to off.
                    SetColor(material, "_BaseColor", new Color(0.620f, 0.605f, 0.545f, 1f));
                    SetColor(material, "_TipColor", new Color(0.720f, 0.700f, 0.630f, 1f));
                    SetFloat(material, "_TransmissionStrength", 0.14f);
                    SetFloat(material, "_SSSStrength", 0.35f);
                    SetFloat(material, "_ThicknessStrength", 1.10f);
                    SetFloat(material, "_Smoothness", 0.62f);
                    SetFloat(material, "_EdgeWearDarkening", 0.30f);
                    SetFloat(material, "_BiolumStrength", 0f);
                    break;

                case "KelpHoldfast":
                    // Root mass gripping rock: opaque, rough, silted, no transmission at all.
                    SetColor(material, "_BaseColor", new Color(0.560f, 0.545f, 0.500f, 1f));
                    SetColor(material, "_TipColor", new Color(0.640f, 0.620f, 0.565f, 1f));
                    SetFloat(material, "_TransmissionStrength", 0.05f);
                    SetFloat(material, "_SSSStrength", 0.18f);
                    SetFloat(material, "_ThicknessStrength", 1.30f);
                    SetFloat(material, "_Smoothness", 0.48f);
                    SetFloat(material, "_EdgeWearDarkening", 0.22f);
                    SetFloat(material, "_AgeDarkening", 0.34f);
                    SetFloat(material, "_BiolumStrength", 0f);
                    break;

                case "StemHoldfast":
                    // flora_capstem.py:1531-1537: (0.640, 0.545, 0.375), roughness 0.38,
                    // subsurface 0.14. The reference stems are markedly LIGHTER than their caps,
                    // which is what makes the cap read as a separate organ at distance - so this
                    // must stay pale even though a darker stem would look more "grounded".
                    SetColor(material, "_BaseColor", new Color(0.640f, 0.545f, 0.375f, 1f));
                    SetColor(material, "_AccentColor", new Color(0.730f, 0.635f, 0.460f, 1f));
                    SetColor(material, "_SubsurfaceColor", new Color(0.780f, 0.680f, 0.500f, 1f));
                    SetFloat(material, "_Smoothness", 0.62f);
                    SetFloat(material, "_SubsurfaceStrength", 0.38f);
                    SetFloat(material, "_CavityStrength", 0.55f);
                    SetFloat(material, "_BiolumStrength", 0f);
                    break;
            }
        }

        /// <summary>
        /// Binds the organic map stack. Separate from <see cref="BindTextureSet"/> because
        /// CoralMaster's slot names differ (<c>_NormalMap</c>, no <c>_ParallaxMap</c>) and because
        /// the three CapStem roles must receive normal and mask WITHOUT an albedo.
        /// </summary>
        private static BoundMaps BindOrganicTextureSet(Material material, string role)
        {
            BoundMaps bound = default;
            bound.SetNote = "none";

            RoleTextureSet set = ResolveTextureSet("Flora", role);
            if (set == null)
            {
                SetTexture(material, "_BaseMap", null, Vector2.one);
                SetTexture(material, "_NormalMap", null, Vector2.one);
                SetTexture(material, "_MaskMap", null, Vector2.one);
                return bound;
            }

            bool suppressAlbedo = OrganicPigmentIsAuthored(role);
            Texture baseColor = suppressAlbedo
                ? null
                : AssetDatabase.LoadAssetAtPath<Texture>(set.BaseColor);
            Texture normalGl = AssetDatabase.LoadAssetAtPath<Texture>(set.NormalGL);
            Texture mask = AssetDatabase.LoadAssetAtPath<Texture>(set.MaskUnityUrp);

            // Scale is passed for completeness but is INERT on this shader - every map is sampled
            // from world position, not from the mesh UV (shader :176-196). _TriplanarScale is the
            // real knob and ApplyOrganicRole writes it from bound.Tiling.
            SetTexture(material, "_BaseMap", baseColor, Vector2.one);
            SetTexture(material, "_NormalMap", normalGl, Vector2.one);
            SetTexture(material, "_MaskMap", mask, Vector2.one);

            bound.HasBase = baseColor != null;
            bound.HasNormal = normalGl != null;
            bound.HasMask = mask != null;
            bound.HasHeight = false;
            bound.Tiling = set.TileMetres > 0.0001f ? 1f / set.TileMetres : 1f;
            bound.SetNote = suppressAlbedo
                ? set.SourceNote + " [albedo SUPPRESSED: authored pigment]"
                : set.SourceNote;
            return bound;
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
            // The two masters name the normal slot differently: _BumpMap on
            // Hecton_ModuleHardSurfaceLit.shader:70, _NormalMap on Hecton_CoralMaster.shader:6.
            // HasProperty makes the wrong one a no-op, so probing both is what keeps the organic
            // materials from silently reporting one fewer bound map than they carry.
            if (HasTexture(material, "_BumpMap")) bound++;
            if (HasTexture(material, "_NormalMap")) bound++;
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

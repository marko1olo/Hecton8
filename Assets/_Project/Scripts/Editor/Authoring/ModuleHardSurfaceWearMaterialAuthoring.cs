// ============================================================================================
//  ModuleHardSurfaceWearMaterialAuthoring
//
//  Binds the six construction module materials to Hecton8/Construction/ModuleHardSurfaceLit so
//  the four hard-surface vertex-colour wear channels required by `3dmodel.md` section 4 and
//  `3DMODEL_HARD_SURFACE_MODULES.md` section 5 are actually consumed at render time.
//
//  Why this script exists at all: `Universal Render Pipeline/Lit` has no COLOR semantic in its
//  Attributes struct, so every wear byte ModuleArchitect1712 bakes
//  (ModuleArchitect1712.cs:1379-1405) was inert. Swapping the shader is the whole fix, but the
//  swap is only safe on meshes that provably carry a non-degenerate colour stream - B = 0 means
//  "fully occluded" in the contract, and a colourless mesh would read as solid black without a
//  gate. That gate is the audit below, and it is why this is an authoring script and not a
//  hand-edited `.mat`: `AGENTS.md` Evidence Law
//  ("YAML Serialization & Asset Integrity (No Textual Edits)") bans text-editing material,
//  prefab and scene assets, so mutation goes through AssetDatabase / SerializedObject.
//
//  Both entry points are idempotent. Running twice reports "already bound" and writes nothing.
// ============================================================================================
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.Authoring
{
    /// <summary>
    /// Migrates <c>Assets/_Project/Art/Materials/Construction/Mat_Module_*.mat</c> from
    /// <c>Universal Render Pipeline/Lit</c> to <c>Hecton8/Construction/ModuleHardSurfaceLit</c>,
    /// preserving the existing PBR read exactly and switching on the four vertex-colour wear
    /// layers. Refuses any material whose consuming meshes do not carry usable vertex colours.
    /// </summary>
    public static class ModuleHardSurfaceWearMaterialAuthoring
    {
        // ══════════════════════════════════════════════════════════
        //  PATHS AND NAMES
        // ══════════════════════════════════════════════════════════

        private const string LogPrefix = "[ModuleHardSurfaceWear]";

        /// <summary>Same folder literal ConstructionBootstrapAuthoring.cs:60-77 creates into.</summary>
        private const string ConstructionMaterialFolder = "Assets/_Project/Art/Materials/Construction";

        /// <summary>Declared by Assets/_Project/Art/Shaders/Hecton_ModuleHardSurfaceLit.shader:1.</summary>
        private const string WearShaderName = "Hecton8/Construction/ModuleHardSurfaceLit";

        private const string WearShaderPath = "Assets/_Project/Art/Shaders/Hecton_ModuleHardSurfaceLit.shader";

        /// <summary>Resolved from Library/PackageCache/com.unity.render-pipelines.universal@17.5.0/Shaders/Lit.shader:1.</summary>
        private const string UrpLitShaderName = "Universal Render Pipeline/Lit";

        /// <summary>
        /// ModuleArchitect1712Settings.Default.OutputFolder (ModuleArchitect1712.cs:27). The six
        /// generated module prefabs live here and are the meshes that carry wear colours.
        /// </summary>
        private const string Agent1712OutputFolder = "Assets/_Project/Art/Baked/Structures/Agent1712";

        /// <summary>
        /// The six construction module materials, in the order
        /// ConstructionGeminiMaterialApplier.cs:17-73 and
        /// ConstructionInsulationBackingIntegrator.cs:15 author them.
        /// </summary>
        private static readonly string[] ModuleMaterialPaths =
        {
            ConstructionMaterialFolder + "/Mat_Module_Foundation.mat",
            ConstructionMaterialFolder + "/Mat_Module_Corridor.mat",
            ConstructionMaterialFolder + "/Mat_Module_Pylon.mat",
            ConstructionMaterialFolder + "/Mat_Module_ServicePump.mat",
            ConstructionMaterialFolder + "/Mat_Module_CurrentTurbine.mat",
            ConstructionMaterialFolder + "/Mat_Module_InsulationBacking.mat"
        };

        // URP Lit source property names read before the shader swap.
        private const string UrpBaseMap = "_BaseMap";
        private const string UrpBumpMap = "_BumpMap";
        private const string UrpMetallicGlossMap = "_MetallicGlossMap";
        private const string UrpOcclusionMap = "_OcclusionMap";
        private const string UrpParallaxMap = "_ParallaxMap";

        // ══════════════════════════════════════════════════════════
        //  AUDIT THRESHOLDS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// A mesh qualifies only if its colour stream exists, is the right length, and is not
        /// degenerate. Degenerate means every vertex shares one colour, which is what a stream
        /// filled by a default constructor looks like and carries no wear information.
        /// </summary>
        private const float DegenerateChannelSpread = 0.004f;

        /// <summary>
        /// ModuleHardSurfaceDetail1712.EmissiveAlphaThreshold (ModuleHardSurfaceDetail1712.cs:358).
        /// Kept as a literal here because that constant lives in the Hecton8.Project.Editor
        /// assembly and this file is in Hecton8.Editor; the value is asserted against the source
        /// line in the report so drift is visible rather than silent.
        /// </summary>
        private const float EmissiveAlphaThreshold = 0.94f;

        // ══════════════════════════════════════════════════════════
        //  ENTRY POINTS
        // ══════════════════════════════════════════════════════════

        [MenuItem("Hecton8/Authoring/Bind Module Hard Surface Wear Shader", priority = 221)]
        public static void BindModuleHardSurfaceWearShader()
        {
            Run(applyChanges: true);
        }

        [MenuItem("Hecton8/Validation/Verify Module Hard Surface Wear Channels", priority = 244)]
        public static void VerifyModuleHardSurfaceWearChannels()
        {
            Run(applyChanges: false);
        }

        // ══════════════════════════════════════════════════════════
        //  DRIVER
        // ══════════════════════════════════════════════════════════

        private static void Run(bool applyChanges)
        {
            Shader wearShader = ResolveWearShader();
            MeshWearAudit audit = AuditAgent1712Meshes();
            StringBuilder report = new StringBuilder(4096);

            report.Append(LogPrefix)
                  .Append(applyChanges ? " BIND" : " VERIFY")
                  .Append(" shader=")
                  .Append(wearShader != null ? wearShader.name : "MISSING")
                  .AppendLine();

            report.Append("  mesh audit: assets=").Append(audit.MeshCount)
                  .Append(" withColour=").Append(audit.MeshesWithColour)
                  .Append(" degenerate=").Append(audit.DegenerateMeshes)
                  .Append(" vertices=").Append(audit.VertexCount)
                  .AppendLine();

            if (audit.VertexCount > 0)
            {
                report.Append("  channel R edgeWear    min=").Append(F(audit.MinR)).Append(" max=").Append(F(audit.MaxR)).Append(" mean=").Append(F(audit.MeanR)).AppendLine();
                report.Append("  channel G oxide/grime min=").Append(F(audit.MinG)).Append(" max=").Append(F(audit.MaxG)).Append(" mean=").Append(F(audit.MeanG)).AppendLine();
                report.Append("  channel B cavityAO    min=").Append(F(audit.MinB)).Append(" max=").Append(F(audit.MaxB)).Append(" mean=").Append(F(audit.MeanB)).AppendLine();
                report.Append("  channel A seam/paint  min=").Append(F(audit.MinA)).Append(" max=").Append(F(audit.MaxA)).Append(" mean=").Append(F(audit.MeanA))
                      .Append(" verticesAtOrAbove").Append(F(EmissiveAlphaThreshold)).Append('=').Append(audit.SeamVertices)
                      .AppendLine();
                report.Append("  all-zero colour vertices (shader treats as absent)=").Append(audit.AllZeroVertices).AppendLine();
            }

            if (wearShader == null)
            {
                report.Append("  BLOCKED: shader not found by name '").Append(WearShaderName)
                      .Append("' nor at ").Append(WearShaderPath)
                      .Append(". Reimport the shader before binding.");
                Debug.LogError(report.ToString());
                return;
            }

            bool meshGatePassed = audit.MeshesWithColour > 0 && audit.DegenerateMeshes == 0 && audit.SeamVertices > 0;
            if (!meshGatePassed)
            {
                report.Append("  BLOCKED: mesh wear gate failed. ");
                if (audit.MeshesWithColour <= 0)
                    report.Append("No mesh under ").Append(Agent1712OutputFolder).Append(" carries a vertex colour stream - run Hecton8/Structures/Agent 1712/Fabricate Default Module Set Now first. ");
                if (audit.DegenerateMeshes > 0)
                    report.Append(audit.DegenerateMeshes).Append(" mesh(es) have a flat colour stream, which carries no wear information. ");
                if (audit.MeshesWithColour > 0 && audit.SeamVertices <= 0)
                    report.Append("No vertex reaches the emissive alpha threshold, so the gasket seam band is empty. ");
                report.Append("Nothing was written.");
                Debug.LogError(report.ToString());
                return;
            }

            int migrated = 0;
            int alreadyBound = 0;
            int missing = 0;

            for (int i = 0; i < ModuleMaterialPaths.Length; i++)
            {
                string path = ModuleMaterialPaths[i];
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    missing++;
                    report.Append("  MISSING ").Append(path).AppendLine();
                    continue;
                }

                string currentShaderName = material.shader != null ? material.shader.name : string.Empty;
                if (string.Equals(currentShaderName, WearShaderName, StringComparison.Ordinal))
                {
                    alreadyBound++;
                    if (applyChanges)
                    {
                        // Re-assert the tuned vectors so a partially authored material converges.
                        ApplyWearVectors(material);
                        EditorUtility.SetDirty(material);
                    }

                    report.Append("  BOUND    ").Append(path).AppendLine();
                    continue;
                }

                if (!string.Equals(currentShaderName, UrpLitShaderName, StringComparison.Ordinal))
                {
                    report.Append("  SKIP     ").Append(path)
                          .Append(" - unexpected source shader '").Append(currentShaderName)
                          .Append("'. Only ").Append(UrpLitShaderName).Append(" is migrated automatically.")
                          .AppendLine();
                    continue;
                }

                LitSourceState source = ReadLitSourceState(material);
                report.Append(applyChanges ? "  MIGRATE  " : "  WOULD    ").Append(path)
                      .Append(" metallic=").Append(F(source.Metallic))
                      .Append(" smoothness=").Append(F(source.Smoothness))
                      .Append(" occlusion=").Append(F(source.OcclusionStrength))
                      .Append(" bump=").Append(F(source.BumpScale))
                      .Append(" parallax=").Append(F(source.Parallax))
                      .Append(" tiling=").Append(F(source.BaseScale.x))
                      .AppendLine();

                if (!applyChanges)
                    continue;

                WriteWearMaterial(material, wearShader, source);
                EditorUtility.SetDirty(material);
                migrated++;
            }

            report.Append("  result: migrated=").Append(migrated)
                  .Append(" alreadyBound=").Append(alreadyBound)
                  .Append(" missing=").Append(missing);

            if (applyChanges && (migrated > 0 || alreadyBound > 0))
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            if (missing > 0)
                Debug.LogWarning(report.ToString());
            else
                Debug.Log(report.ToString());
        }

        private static Shader ResolveWearShader()
        {
            Shader shader = Shader.Find(WearShaderName);
            if (shader != null)
                return shader;

            return AssetDatabase.LoadAssetAtPath<Shader>(WearShaderPath);
        }

        // ══════════════════════════════════════════════════════════
        //  URP LIT SOURCE STATE - read BEFORE the shader swap
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Assigning <c>material.shader</c> drops any property the target shader does not declare,
        /// so every source value and texture scale/offset is captured first. Same ordering
        /// constraint HectonMasterShaderAudit1615.cs:500-505 enforces on the master migrator.
        /// </summary>
        private struct LitSourceState
        {
            public Texture BaseMap;
            public Texture BumpMap;
            public Texture MaskMap;
            public Texture ParallaxMap;
            public Vector2 BaseScale;
            public Vector2 BaseOffset;
            public Vector2 BumpScaleUv;
            public Vector2 BumpOffset;
            public Vector2 MaskScale;
            public Vector2 MaskOffset;
            public Vector2 ParallaxScale;
            public Vector2 ParallaxOffset;
            public Color BaseColor;
            public float Metallic;
            public float Smoothness;
            public float OcclusionStrength;
            public float BumpScale;
            public float Parallax;
            public bool HasMaskMap;
            public bool HasBumpMap;
            public bool HasParallaxMap;
        }

        private static LitSourceState ReadLitSourceState(Material material)
        {
            LitSourceState state = default;
            state.BaseMap = GetTexture(material, UrpBaseMap);
            state.BumpMap = GetTexture(material, UrpBumpMap);

            // Both slots are fed the same Gemini MaskMap_UnityURP texture by
            // ConstructionGeminiMaterialApplier.cs:127-128. Prefer _MetallicGlossMap and fall back
            // to _OcclusionMap so a material authored with only one of them still migrates.
            // `??` is deliberately NOT used here: UnityEngine.Object overloads `==` to check the
            // native pointer while `??` tests the managed reference, so a fake-null texture would
            // slip through (`COMMON_SENSE.md` 7, The Unity Object Fake Null).
            Texture metallicGloss = GetTexture(material, UrpMetallicGlossMap);
            state.MaskMap = metallicGloss != null ? metallicGloss : GetTexture(material, UrpOcclusionMap);
            state.ParallaxMap = GetTexture(material, UrpParallaxMap);

            state.BaseScale = GetTextureScale(material, UrpBaseMap);
            state.BaseOffset = GetTextureOffset(material, UrpBaseMap);
            state.BumpScaleUv = GetTextureScale(material, UrpBumpMap);
            state.BumpOffset = GetTextureOffset(material, UrpBumpMap);
            state.MaskScale = material.HasProperty(UrpMetallicGlossMap)
                ? GetTextureScale(material, UrpMetallicGlossMap)
                : GetTextureScale(material, UrpOcclusionMap);
            state.MaskOffset = material.HasProperty(UrpMetallicGlossMap)
                ? GetTextureOffset(material, UrpMetallicGlossMap)
                : GetTextureOffset(material, UrpOcclusionMap);
            state.ParallaxScale = GetTextureScale(material, UrpParallaxMap);
            state.ParallaxOffset = GetTextureOffset(material, UrpParallaxMap);

            state.BaseColor = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
            state.Metallic = GetFloat(material, "_Metallic", 0f);
            state.Smoothness = GetFloat(material, "_Smoothness", 0.42f);
            state.OcclusionStrength = GetFloat(material, "_OcclusionStrength", 1f);
            state.BumpScale = GetFloat(material, "_BumpScale", 1f);
            state.Parallax = GetFloat(material, "_Parallax", 0f);

            state.HasMaskMap = state.MaskMap != null;
            state.HasBumpMap = state.BumpMap != null;
            state.HasParallaxMap = state.ParallaxMap != null;
            return state;
        }

        // ══════════════════════════════════════════════════════════
        //  WRITE
        // ══════════════════════════════════════════════════════════

        private static void WriteWearMaterial(Material material, Shader wearShader, LitSourceState source)
        {
            material.shader = wearShader;

            // The new shader declares no shader_feature, so every inherited URP Lit keyword
            // (_NORMALMAP, _METALLICSPECGLOSSMAP, _OCCLUSIONMAP, _PARALLAXMAP) is dead weight in
            // m_ValidKeywords. Clearing the set rather than toggling individual keywords avoids
            // the variant churn `COMMON_SENSE.md` 16 warns about.
            material.shaderKeywords = Array.Empty<string>();

            SetTexture(material, "_BaseMap", source.BaseMap, source.BaseScale, source.BaseOffset);
            SetTexture(material, "_BumpMap", source.BumpMap, source.BumpScaleUv, source.BumpOffset);
            SetTexture(material, "_MaskMap", source.MaskMap, source.MaskScale, source.MaskOffset);
            SetTexture(material, "_ParallaxMap", source.ParallaxMap, source.ParallaxScale, source.ParallaxOffset);

            material.SetColor("_BaseColor", source.BaseColor);
            material.SetFloat("_Metallic", Mathf.Clamp01(source.Metallic));
            material.SetFloat("_Smoothness", Mathf.Clamp01(source.Smoothness));
            material.SetFloat("_OcclusionStrength", Mathf.Clamp01(source.OcclusionStrength));
            material.SetFloat("_BumpScale", Mathf.Clamp(source.BumpScale, 0f, 2f));
            material.SetFloat("_Parallax", Mathf.Clamp(source.Parallax, 0f, 0.08f));

            // Map-weight gates. A material with no mask texture must fall back to its scalars
            // instead of sampling white and reading as fully smooth metal.
            float metallicMapWeight = source.HasMaskMap ? 1f : 0f;
            float smoothnessMapWeight = source.HasMaskMap ? 1f : 0f;
            float occlusionMapWeight = source.HasMaskMap ? 1f : 0f;
            float normalScale = source.HasBumpMap ? Mathf.Clamp(source.BumpScale, 0f, 2f) : 0f;
            material.SetVector("_ModuleSurfaceParams", new Vector4(metallicMapWeight, smoothnessMapWeight, occlusionMapWeight, normalScale));

            // POM: x is the height amplitude, y the step ceiling, w the per-material quality cap.
            // Amplitude is inherited from _Parallax so the migration does not change silhouette
            // depth; the step ceiling is 0 without a height map so the loop is skipped entirely.
            float pomSteps = source.HasParallaxMap ? 4f : 0f;
            material.SetVector("_ModulePomParams", new Vector4(Mathf.Clamp(source.Parallax, 0f, 0.08f), pomSteps, 0f, 1f));

            ApplyWearVectors(material);

            material.SetOverrideTag("RenderType", "Opaque");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;

            // Shared material on MeshRenderer GameObjects with instancing on is the GPU Resident
            // Drawer path `REND_GPU_Sovereignty.txt:27` requires; MaterialPropertyBlock is banned
            // here by `AGENTS.md` Runtime Hot-Path Law and `REND_GPU_Sovereignty.txt:29`.
            material.enableInstancing = true;
        }

        /// <summary>
        /// The tuned wear response. Values chosen against the mandatory reference images
        /// (base.webp, nice_biome.webp): the canopy arch in nice_biome reads its chamfer as a
        /// brighter, tighter grazing highlight rather than as rust, so channel R raises metallic
        /// and smoothness and lifts albedo toward bare metal; oxidation must move the opposite way.
        /// Seam emission uses DECAY_AMBER from
        /// `REND_Shader_Noir_Aesthetics_Dithering_Fog.txt:25` ("corroded metal emissive").
        /// </summary>
        private static void ApplyWearVectors(Material material)
        {
            // x=edge R weight, y=oxide G weight, z=cavity B weight, w=vertex channel trust.
            material.SetVector("_ModuleWearParams", new Vector4(1f, 1f, 1f, 1f));

            // x=metallic gain, y=smoothness gain, z=albedo lift, w=edge contrast.
            material.SetVector("_ModuleEdgeResponse", new Vector4(0.85f, 0.55f, 0.72f, 0.35f));

            // x=roughness gain, y=metallic loss, z=reserved, w=albedo blend.
            material.SetVector("_ModuleOxideResponse", new Vector4(0.62f, 0.85f, 0f, 0.68f));

            // x=emissive alpha threshold, y=ramp band, z=paint adhesion, w=emission scale.
            // Threshold is ModuleHardSurfaceDetail1712.EmissiveAlphaThreshold; the 0.04 band keeps
            // PlateAttributes at 0.85 (ModuleHardSurfaceDetail1712.cs:388) outside the seam.
            material.SetVector("_ModuleSeamParams", new Vector4(EmissiveAlphaThreshold, 0.04f, 0.55f, 1f));

            // x=ambient, y=specular, z=cavity micro-contrast, w=occlusion floor. The floor exists
            // because `REND_Shader_Noir_Aesthetics_Dithering_Fog.txt` Section0 forbids pure black
            // on scene geometry.
            material.SetVector("_ModuleNoirParams", new Vector4(0.34f, 0.42f, 0.35f, 0.06f));

            // x=silt strength, y=rust strength. Feeds the existing
            // HectonCoreLitApplyProceduralRustSilt breakup (Hecton_CoreLit.hlsl:1105) so the
            // vertex tint is not a flat wash.
            material.SetVector("_ModuleRustSiltParams", new Vector4(0.22f, 0.46f, 0f, 0f));

            material.SetColor("_ModuleEdgeMetalColor", new Color(0.52f, 0.55f, 0.58f, 1f));
            material.SetColor("_ModuleOxideColor", new Color(0.72f, 0.40f, 0.19f, 1f));
            material.SetColor("_ModuleBiofilmColor", new Color(0.30f, 0.42f, 0.34f, 1f));
            material.SetColor("_ModuleSiltTint", new Color(0.23f, 0.28f, 0.26f, 1f));
            material.SetColor("_ModuleSeamEmissionColor", new Color(0.55f, 0.28f, 0.04f, 1f));
        }

        // ══════════════════════════════════════════════════════════
        //  MESH WEAR AUDIT
        // ══════════════════════════════════════════════════════════

        private struct MeshWearAudit
        {
            public int MeshCount;
            public int MeshesWithColour;
            public int DegenerateMeshes;
            public int VertexCount;
            public int SeamVertices;
            public int AllZeroVertices;
            public float MinR, MaxR, MeanR;
            public float MinG, MaxG, MeanG;
            public float MinB, MaxB, MeanB;
            public float MinA, MaxA, MeanA;
        }

        /// <summary>
        /// Reads every mesh asset under the ModuleArchitect1712 output folder and reports real
        /// per-channel statistics. This is the only thing that distinguishes "the channels are
        /// baked" from "the channels are baked AND non-degenerate", and it is why the binding is
        /// gated on it. Editor-only cold path; <c>Mesh.colors32</c> is banned in hot paths by
        /// `AGENTS.md` Runtime Hot-Path Law but is the correct read here.
        /// </summary>
        private static MeshWearAudit AuditAgent1712Meshes()
        {
            MeshWearAudit audit = default;
            audit.MinR = 1f; audit.MinG = 1f; audit.MinB = 1f; audit.MinA = 1f;

            if (!AssetDatabase.IsValidFolder(Agent1712OutputFolder))
                return audit;

            string[] guids = AssetDatabase.FindAssets("t:Mesh", new[] { Agent1712OutputFolder });
            double sumR = 0d;
            double sumG = 0d;
            double sumB = 0d;
            double sumA = 0d;

            // COLD ALLOC: List<Color32>[0] - editor audit scratch, reused across every mesh so the
            // per-mesh colors32 copy is the only allocation - owner: ModuleHardSurfaceWearMaterialAuthoring
            List<Color32> colourScratch = new List<Color32>(4096);

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null)
                    continue;

                audit.MeshCount++;
                colourScratch.Clear();
                mesh.GetColors(colourScratch);
                if (colourScratch.Count == 0 || colourScratch.Count != mesh.vertexCount)
                    continue;

                audit.MeshesWithColour++;
                byte firstR = colourScratch[0].r;
                byte firstG = colourScratch[0].g;
                byte firstB = colourScratch[0].b;
                byte firstA = colourScratch[0].a;
                bool flat = true;

                for (int v = 0; v < colourScratch.Count; v++)
                {
                    Color32 packed = colourScratch[v];
                    float r = packed.r * (1f / 255f);
                    float g = packed.g * (1f / 255f);
                    float b = packed.b * (1f / 255f);
                    float a = packed.a * (1f / 255f);

                    audit.VertexCount++;
                    sumR += r; sumG += g; sumB += b; sumA += a;
                    audit.MinR = Mathf.Min(audit.MinR, r); audit.MaxR = Mathf.Max(audit.MaxR, r);
                    audit.MinG = Mathf.Min(audit.MinG, g); audit.MaxG = Mathf.Max(audit.MaxG, g);
                    audit.MinB = Mathf.Min(audit.MinB, b); audit.MaxB = Mathf.Max(audit.MaxB, b);
                    audit.MinA = Mathf.Min(audit.MinA, a); audit.MaxA = Mathf.Max(audit.MaxA, a);

                    if (a >= EmissiveAlphaThreshold)
                        audit.SeamVertices++;
                    if (packed.r == 0 && packed.g == 0 && packed.b == 0 && packed.a == 0)
                        audit.AllZeroVertices++;
                    if (flat && (packed.r != firstR || packed.g != firstG || packed.b != firstB || packed.a != firstA))
                        flat = false;
                }

                if (flat)
                    audit.DegenerateMeshes++;
            }

            if (audit.VertexCount > 0)
            {
                double inv = 1d / audit.VertexCount;
                audit.MeanR = (float)(sumR * inv);
                audit.MeanG = (float)(sumG * inv);
                audit.MeanB = (float)(sumB * inv);
                audit.MeanA = (float)(sumA * inv);
            }
            else
            {
                audit.MinR = 0f; audit.MinG = 0f; audit.MinB = 0f; audit.MinA = 0f;
            }

            // A spread below the degenerate epsilon on every channel means the bake produced no
            // usable variation even though the stream exists.
            if (audit.MeshesWithColour > 0 &&
                (audit.MaxR - audit.MinR) < DegenerateChannelSpread &&
                (audit.MaxG - audit.MinG) < DegenerateChannelSpread &&
                (audit.MaxB - audit.MinB) < DegenerateChannelSpread &&
                (audit.MaxA - audit.MinA) < DegenerateChannelSpread)
            {
                audit.DegenerateMeshes = Mathf.Max(audit.DegenerateMeshes, audit.MeshesWithColour);
            }

            return audit;
        }

        // ══════════════════════════════════════════════════════════
        //  SMALL HELPERS
        // ══════════════════════════════════════════════════════════

        private static string F(float value)
        {
            return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static Texture GetTexture(Material material, string name)
        {
            return material.HasProperty(name) ? material.GetTexture(name) : null;
        }

        private static Vector2 GetTextureScale(Material material, string name)
        {
            return material.HasProperty(name) ? material.GetTextureScale(name) : Vector2.one;
        }

        private static Vector2 GetTextureOffset(Material material, string name)
        {
            return material.HasProperty(name) ? material.GetTextureOffset(name) : Vector2.zero;
        }

        private static float GetFloat(Material material, string name, float fallback)
        {
            return material.HasProperty(name) ? material.GetFloat(name) : fallback;
        }

        private static void SetTexture(Material material, string name, Texture texture, Vector2 scale, Vector2 offset)
        {
            if (!material.HasProperty(name))
                return;

            material.SetTexture(name, texture);
            material.SetTextureScale(name, scale);
            material.SetTextureOffset(name, offset);
        }
    }
}
#endif

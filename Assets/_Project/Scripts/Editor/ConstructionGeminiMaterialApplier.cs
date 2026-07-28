using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Applies accepted Gemini single-material PBR maps to first-party construction module materials.
    /// <para>
    /// MASK CHANNEL CONTRACT - read before touching <c>_Metallic</c> or <c>_Smoothness</c> below.
    /// One texture, <c>MaskMap_UnityURP</c>, is bound to BOTH <c>_MetallicGlossMap</c> and
    /// <c>_OcclusionMap</c>, which is how URP Lit consumes a Unity-convention packed mask. The
    /// packing is declared by the source manifests themselves
    /// (<c>GeminiSingleMaterials_Manifest.json</c> and
    /// <c>GeminiMaterialAtlas_Manifest.json</c>, key <c>mapPacking.unityMaskMap</c>:
    /// "RGBA = Metallic, Ambient Occlusion, unused zero, Smoothness") and by this project's own
    /// binding gate (<c>Tools/ValidateExternalPbrImporterBindings.py:63</c>,
    /// "_OcclusionMap must bind MaskMap_UnityURP/maskMap because AO is in G").
    /// </para>
    /// <para>
    /// Consequence in URP 17.5.0, which is NOT the same as the URP inspector's labels suggest:
    /// with <c>_METALLICSPECGLOSSMAP</c> enabled, <c>SampleMetallicSpecGloss</c> takes the whole
    /// RGBA from the map (<c>Shaders/LitInput.hlsl:137-138</c>) and
    /// <c>InitializeStandardLitSurfaceData</c> assigns <c>metallic = specGloss.r</c>
    /// (<c>LitInput.hlsl:261</c>). The <c>_Metallic</c> scalar is read ONLY in the no-map else
    /// branch (<c>LitInput.hlsl:148</c>), so it does not reach the renderer here.
    /// <c>_Smoothness</c> is not an absolute value either: <c>LitInput.hlsl:142</c> applies
    /// <c>specGloss.a *= _Smoothness</c>, so it is a MULTIPLIER on the map's authored smoothness
    /// and can only reduce it. Occlusion comes from <c>.g</c> of <c>_OcclusionMap</c>
    /// (<c>LitInput.hlsl:164</c>). <c>.b</c> is unused.
    /// </para>
    /// <para>
    /// The keyword cannot simply be turned off to hand authority back to the scalars: URP derives
    /// it from texture presence in <c>Editor/ShaderGUI/ShadingModels/LitGUI.cs:463-465</c>
    /// (<c>SetKeyword(material, "_METALLICSPECGLOSSMAP", hasGlossMap)</c>), so any
    /// <c>ValidateMaterial</c> round-trip re-enables it while the mask stays bound - and the mask
    /// must stay bound, because <c>_OcclusionMap</c> needs the same texture for AO.
    /// The map therefore OWNS metallic and the smoothness ceiling. Accordingly:
    /// <c>Assignment.Metallic</c> declares the value the mask's R channel carries and is asserted
    /// against the manifest so the table can never drift from the texture again, and
    /// <c>Assignment.Smoothness</c> is the authored TARGET, converted to the multiplier URP
    /// actually wants by <see cref="ResolveSmoothnessTrim"/>.
    /// </para>
    /// </summary>
    public static class ConstructionGeminiMaterialApplier
    {
        private const string GeminiSingleManifestPath = "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialIntake_20260607/GeminiSingleMaterials_Manifest.json";
        private const string GeminiAtlasRoot = "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases";

        /// <summary>
        /// Declared by Assets/_Project/Art/Shaders/Hecton_ModuleHardSurfaceLit.shader:65. The six
        /// module materials migrate onto it through
        /// ModuleHardSurfaceWearMaterialAuthoring.BindModuleHardSurfaceWearShader, which is a
        /// FIRST-PARTY shader this applier must not stomp back to URP Lit.
        /// </summary>
        private const string ModuleWearShaderName = "Hecton8/Construction/ModuleHardSurfaceLit";

        private const string UrpLitShaderName = "Universal Render Pipeline/Lit";

        /// <summary>
        /// The mask PNGs store each constant channel as <c>floor(value * 255)</c>, so a manifest
        /// scalar and the byte actually encoded differ by up to 1/255. Measured 2026-07-29 on all
        /// six bound masks: manifest smoothness 0.502 encodes as 127/255 = 0.498, manifest metallic
        /// 0.78 encodes as 198/255 = 0.7765. One quantization step is the whole tolerance budget;
        /// anything larger is a real table-versus-texture drift and must fail.
        /// </summary>
        private const float MaskChannelQuantizationTolerance = 0.005f;

        // Argument 5 (`metallic`) is NOT a free art dial. It declares the constant the bound mask's
        // R channel carries, and ValidateAssignments throws if it disagrees with the manifest. The
        // previous values - 0.34 for the corridor and 0.0 for the other five - were never rendered
        // (see the mask channel contract above), so the four zeros were a silent lie about a surface
        // that actually renders at up to metallic 0.78.
        //
        // Argument 6 (`smoothness`) IS an art dial, but it is a TARGET, not the value written to the
        // material: URP multiplies it into the mask alpha, so ResolveSmoothnessTrim converts it. The
        // target is reachable only while it stays at or below the mask's authored smoothness; the
        // report line prints CAPPED when it does not, instead of silently rendering the product.
        private static readonly Assignment[] Assignments =
        {
            new Assignment(
                "Assets/_Project/Art/Materials/Construction/Mat_Module_Corridor.mat",
                "gemini_Batch20260608_TextureExpansion_b34_3412_pressure_base_interior_wall_trim_sheet",
                0.72f,
                0.92f,
                0.3176f,
                0.48f,
                0.004f,
                "Corridor shell: pressure-base interior trim sheet gives readable wall panels, gaskets, and damp lower-wall wear. Mask R carries metallic 0.3176 - a mixed trim sheet with metal fittings, so a partial metal field is intended."),
            new Assignment(
                "Assets/_Project/Art/Materials/Construction/Mat_Module_Foundation.mat",
                "gemini_20260607_salvage_worn_repair_metal",
                0.75f,
                1.05f,
                0.48f,
                0.34f,
                0.014f,
                "Foundation: salvage-worn repair metal for heavy base plates. Mask R carries metallic 0.48. ART REVIEW OPEN: a half-metal field flattens the bare-metal reveal the wear shader drives from vertex channel R (Hecton_ModuleHardSurfaceLit.shader:484), so the first capture must decide between repacking mask R toward a coated field and keeping the plate frankly metallic."),
            new Assignment(
                "Assets/_Project/Art/Materials/Construction/Mat_Module_CurrentTurbine.mat",
                "gemini_20260607_dark_anodized_tool_metal",
                0.65f,
                0.9f,
                0.78f,
                0.38f,
                0.010f,
                "Current turbine: dark anodized machinery metal, wet but not black void. Mask R carries metallic 0.78, the most metallic surface in the set; anodizing is a dielectric coat over metal, so this is the second ART REVIEW OPEN entry alongside Foundation."),
            new Assignment(
                "Assets/_Project/Art/Materials/Construction/Mat_Module_ServicePump.mat",
                "gemini_20260607_wet_service_panel_biofilm",
                0.55f,
                0.95f,
                0.44f,
                0.36f,
                0.012f,
                "Service pump: wet service panel with biofilm; constrained tiling hides high-seam source. Mask R carries metallic 0.44."),
            new Assignment(
                "Assets/_Project/Art/Materials/Construction/Mat_Module_Pylon.mat",
                "gemini_20260607_orange_safety_composite_panel",
                0.75f,
                0.85f,
                0.0f,
                0.32f,
                0.008f,
                "Pylon: orange safety composite as construction route/readability accent. Mask R carries metallic 0.0, correct for a painted composite panel."),
            new Assignment(
                "Assets/_Project/Art/Materials/Construction/MAT_Equipment_Atlas.mat",
                "gemini_Batch20260608_TextureExpansion_b34_3416_ribbed_flexible_hose_material",
                0.50f,
                0.85f,
                0.0f,
                0.30f,
                0.003f,
                "Equipment atlas: ribbed flexible hose material covers pipe/cable/equipment backing without pretending to be a full device texture. Mask R carries metallic 0.0, correct for rubber.")
        };

        [MenuItem("Hecton8/Art/Apply Gemini PBR To Construction Materials")]
        public static void ExecuteMenu()
        {
            Apply();
        }

        public static void Apply()
        {
            Apply(true);
        }

        public static void Apply(bool importFirst)
        {
            if (importFirst)
                ExternalPbrTexturePackImporter.ImportExternalPbrTexturePacks();

            Dictionary<string, ExternalPbrAsset> assets = LoadAllManifestAssets();
            ValidateAssignments(assets);
            int applied = 0;

            // COLD ALLOC: StringBuilder[4096] - one editor apply report, so the per-material
            // metallic/smoothness numbers land in the Unity Console as measured evidence instead of
            // a bare count that proves nothing - owner: ConstructionGeminiMaterialApplier
            StringBuilder report = new StringBuilder(4096);

            for (int i = 0; i < Assignments.Length; i++)
            {
                Assignment assignment = Assignments[i];
                ExternalPbrAsset asset = RequireAsset(assets, assignment);
                Material target = RequireTargetMaterial(assignment);
                ApplyAsset(target, asset, assignment, report);
                EditorUtility.SetDirty(target);
                applied++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ConstructionGeminiMaterialApplier] Applied={applied}\n{report}");
        }

        private static void ApplyAsset(Material target, ExternalPbrAsset asset, Assignment assignment, StringBuilder report)
        {
            if (asset.maps == null)
                throw new InvalidOperationException($"[ConstructionGeminiMaterialApplier] Missing map payload for {asset.id}: {assignment.MaterialPath}");

            Texture2D baseColor = RequireTexture(asset.maps.BaseColor, asset.id, "BaseColor", assignment.MaterialPath);
            Texture2D normal = RequireTexture(asset.maps.NormalGL, asset.id, "NormalGL", assignment.MaterialPath);
            Texture2D maskMap = RequireTexture(asset.maps.MaskMap_UnityURP, asset.id, "MaskMap_UnityURP", assignment.MaterialPath);
            Texture2D height = RequireTexture(asset.maps.Height, asset.id, "Height", assignment.MaterialPath);

            // Do NOT stomp a first-party module shader back to URP Lit. Six of these materials are
            // migrated onto Hecton8/Construction/ModuleHardSurfaceLit by
            // ModuleHardSurfaceWearMaterialAuthoring so the baked hard-surface wear channels are
            // consumed at all; that shader declares no COLOR-less URP Lit property set, and
            // assigning a shader drops every property the target does not declare
            // (the same ordering constraint that tool documents at
            // ModuleHardSurfaceWearMaterialAuthoring.cs:260-264). This applier is a stage of
            // GeminiMaterialIntegrationApplier.ApplyAll (GeminiMaterialIntegrationApplier.cs:36),
            // which a human runs on its own schedule, so without this guard an apply-all after the
            // migration would silently revert all six modules to the shader that cannot read wear.
            string currentShaderName = target.shader != null ? target.shader.name : string.Empty;
            bool onWearShader = string.Equals(currentShaderName, ModuleWearShaderName, StringComparison.Ordinal);
            if (!onWearShader)
            {
                Shader shader = Shader.Find(UrpLitShaderName);
                if (shader != null)
                {
                    target.shader = shader;
                    currentShaderName = shader.name;
                }
            }

            SetTextureIfPresent(target, "_BaseMap", baseColor);
            SetTextureIfPresent(target, "_MainTex", baseColor);
            SetTextureIfPresent(target, "_BumpMap", normal);
            SetTextureIfPresent(target, "_MetallicGlossMap", maskMap);
            SetTextureIfPresent(target, "_OcclusionMap", maskMap);
            // Third slot for the same packed mask. URP Lit needs it twice because metallic/smoothness
            // and occlusion arrive through two different properties; the wear shader reads one
            // `_MaskMap` (Hecton_ModuleHardSurfaceLit.shader:71) with the identical channel order.
            // HasProperty gates the write, so this is inert on URP Lit and required after migration.
            SetTextureIfPresent(target, "_MaskMap", maskMap);
            SetTextureIfPresent(target, "_ParallaxMap", height);
            float tilingScale = TilingScale(asset, assignment);
            SetTextureScaleIfPresent(target, "_BaseMap", tilingScale);
            SetTextureScaleIfPresent(target, "_MainTex", tilingScale);
            SetTextureScaleIfPresent(target, "_BumpMap", tilingScale);
            SetTextureScaleIfPresent(target, "_MetallicGlossMap", tilingScale);
            SetTextureScaleIfPresent(target, "_OcclusionMap", tilingScale);
            SetTextureScaleIfPresent(target, "_MaskMap", tilingScale);
            SetTextureScaleIfPresent(target, "_ParallaxMap", tilingScale);
            SetFloatIfPresent(target, "_BumpScale", assignment.NormalScale);

            // `_Metallic` is the mask's R constant, asserted equal to it by ValidateAssignments. On
            // URP Lit it is dead weight the renderer never reads (LitInput.hlsl:148 is the only read
            // and it is in the no-map branch), but writing the true value is what stops the source
            // from lying, and it is a LIVE fallback on the wear shader, whose decode is
            // `lerp(_Metallic, packedMask.r, _ModuleSurfaceParams.x)`
            // (Hecton_ModuleHardSurfaceLit.shader:349). Because both sides of that lerp now hold the
            // same number, the migration can no longer change the metallic read in either direction.
            SetFloatIfPresent(target, "_Metallic", assignment.Metallic);

            // `_Smoothness` is a multiplier on the mask alpha (LitInput.hlsl:142), not the smoothness.
            // Writing the authored target here squared it: the corridor asked for 0.48, the mask
            // carries 0.498, and the renderer produced 0.239 - and the foundation collapsed from an
            // authored 0.34 to 0.053, which is a perfectly diffuse surface with no grazing highlight
            // at all. `TASTE.md` Beauty Is Controlled Damage asks for "wet metal ... grazing
            // highlights"; base.webp and nice_biome.webp both carry a readable specular sheen on
            // painted structure, so the squared value was a visual defect, not a taste choice.
            float smoothnessTrim = ResolveSmoothnessTrim(asset, assignment);
            SetFloatIfPresent(target, "_Smoothness", smoothnessTrim);
            SetFloatIfPresent(target, "_Parallax", assignment.HeightScale);
            SetFloatIfPresent(target, "_OcclusionStrength", 1f);
            SetFloatIfPresent(target, "_SmoothnessTextureChannel", 0f);
            SetKeyword(target, "_NORMALMAP", normal != null);
            SetKeyword(target, "_METALLICSPECGLOSSMAP", maskMap != null);
            SetKeyword(target, "_OCCLUSIONMAP", maskMap != null);
            SetKeyword(target, "_PARALLAXMAP", height != null);

            // Wear-shader surface authority: x metallic map weight, y smoothness map weight, z AO map
            // weight, w normal scale (Hecton_ModuleHardSurfaceLit.shader:139). The mask owns all
            // three channels on these materials, so all three weights are 1 - the same state
            // ModuleHardSurfaceWearMaterialAuthoring.cs:360-364 writes. Re-asserting it here makes an
            // apply-all after the migration converge instead of drifting. Inert on URP Lit.
            SetVectorIfPresent(
                target,
                "_ModuleSurfaceParams",
                new Vector4(1f, 1f, 1f, Mathf.Clamp(assignment.NormalScale, 0f, 2f)));
            target.enableInstancing = true;

            float mapSmoothness = Mathf.Clamp01(asset.smoothness);
            float achievedSmoothness = Mathf.Clamp01(mapSmoothness * smoothnessTrim);
            report.Append("  ").Append(assignment.MaterialPath)
                  .Append(" shader=").Append(currentShaderName)
                  .Append(" tiling=").Append(F(tilingScale))
                  .Append(" metallic=").Append(F(assignment.Metallic)).Append("(maskR)")
                  .Append(" smoothnessTarget=").Append(F(assignment.Smoothness))
                  // Manifest reference value, not the encoded texel: the PNG stores floor(v * 255),
                  // so the shipped alpha is up to one quantization step below this figure.
                  .Append(" maskA(manifest)=").Append(F(mapSmoothness))
                  .Append(" trim=").Append(F(smoothnessTrim))
                  .Append(" achieved=").Append(F(achievedSmoothness));
            if (achievedSmoothness + MaskChannelQuantizationTolerance < assignment.Smoothness)
                report.Append(" CAPPED_BY_MASK_ALPHA");
            report.AppendLine();
        }

        /// <summary>
        /// Converts the authored absolute smoothness target into the multiplier URP Lit and the wear
        /// shader both apply to the mask alpha. The mask alpha is a hard ceiling in both shaders -
        /// `specGloss.a *= _Smoothness` (LitInput.hlsl:142) and
        /// `packedMask.a * saturate(_Smoothness)` (Hecton_ModuleHardSurfaceLit.shader:350) can only
        /// reduce - so a target above it clamps to 1 and the mask value ships. That is reported, not
        /// hidden. A mask that declares zero smoothness keeps its own value rather than being scaled
        /// by a divide-by-zero.
        /// </summary>
        private static float ResolveSmoothnessTrim(ExternalPbrAsset asset, Assignment assignment)
        {
            float mapSmoothness = Mathf.Clamp01(asset.smoothness);
            if (mapSmoothness <= MaskChannelQuantizationTolerance)
                return 1f;

            return Mathf.Clamp01(assignment.Smoothness / mapSmoothness);
        }

        private static string F(float value)
        {
            return value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static Dictionary<string, ExternalPbrAsset> LoadManifestAssets(string manifestPath)
        {
            Dictionary<string, ExternalPbrAsset> assets = new Dictionary<string, ExternalPbrAsset>(StringComparer.Ordinal);
            string resolvedManifestPath = ResolveProjectFilePath(manifestPath);
            if (!File.Exists(resolvedManifestPath))
                throw new InvalidOperationException($"[ConstructionGeminiMaterialApplier] Missing manifest: {manifestPath}");

            ExternalPbrManifest manifest = JsonUtility.FromJson<ExternalPbrManifest>(File.ReadAllText(resolvedManifestPath));
            if (manifest == null || manifest.assets == null)
                throw new InvalidOperationException($"[ConstructionGeminiMaterialApplier] Invalid manifest payload: {manifestPath}");

            for (int i = 0; i < manifest.assets.Length; i++)
            {
                ExternalPbrAsset asset = manifest.assets[i];
                if (asset == null || string.IsNullOrWhiteSpace(asset.id))
                    throw new InvalidOperationException($"[ConstructionGeminiMaterialApplier] Invalid material asset entry in {manifestPath} at index {i}");
                assets[asset.id] = asset;
            }

            return assets;
        }

        private static Dictionary<string, ExternalPbrAsset> LoadAllManifestAssets()
        {
            Dictionary<string, ExternalPbrAsset> assets = new Dictionary<string, ExternalPbrAsset>(StringComparer.Ordinal);
            MergeManifestAssets(assets, GeminiSingleManifestPath);

            string resolvedAtlasRoot = ResolveProjectFilePath(GeminiAtlasRoot);
            if (Directory.Exists(resolvedAtlasRoot))
            {
                string[] manifests = Directory.GetFiles(resolvedAtlasRoot, "GeminiMaterialAtlas_Manifest.json", SearchOption.AllDirectories);
                Array.Sort(manifests, StringComparer.Ordinal);
                for (int i = 0; i < manifests.Length; i++)
                    MergeManifestAssets(assets, manifests[i]);
            }

            return assets;
        }

        private static void MergeManifestAssets(Dictionary<string, ExternalPbrAsset> assets, string manifestPath)
        {
            Dictionary<string, ExternalPbrAsset> manifestAssets = LoadManifestAssets(manifestPath);
            foreach (KeyValuePair<string, ExternalPbrAsset> pair in manifestAssets)
            {
                if (assets.ContainsKey(pair.Key))
                    throw new InvalidOperationException($"[ConstructionGeminiMaterialApplier] Duplicate Gemini material id: {pair.Key}");

                assets.Add(pair.Key, pair.Value);
            }
        }

        private static void ValidateAssignments(Dictionary<string, ExternalPbrAsset> assets)
        {
            for (int i = 0; i < Assignments.Length; i++)
            {
                Assignment assignment = Assignments[i];
                ExternalPbrAsset asset = RequireAsset(assets, assignment);

                if (asset.maps == null)
                    throw new InvalidOperationException($"[ConstructionGeminiMaterialApplier] Missing map payload for {asset.id}: {assignment.MaterialPath}");

                RequireTargetMaterial(assignment);
                RequireTexture(asset.maps.BaseColor, asset.id, "BaseColor", assignment.MaterialPath);
                RequireTexture(asset.maps.NormalGL, asset.id, "NormalGL", assignment.MaterialPath);
                RequireTexture(asset.maps.MaskMap_UnityURP, asset.id, "MaskMap_UnityURP", assignment.MaterialPath);
                RequireTexture(asset.maps.Height, asset.id, "Height", assignment.MaterialPath);
                RequireMetallicMatchesMask(asset, assignment);
            }
        }

        /// <summary>
        /// The bound mask's R channel is the metallic the renderer uses (LitInput.hlsl:261), and the
        /// manifest's own <c>metallic</c> field is the constant that was baked into it - verified
        /// 2026-07-29 by measuring all six bound mask PNGs, where R is a flat fill equal to that
        /// field within one quantization step and carries zero spatial variation. Asserting the
        /// equality here is what keeps <see cref="Assignment.Metallic"/> honest: if the texture pack
        /// is regenerated with a different metallic, or the table is edited by hand, the apply stage
        /// fails at the exact material instead of shipping a source file that documents a value the
        /// GPU never sees. Failing loudly is required by `AGENTS.md` Absolute Standards; the previous
        /// silent divergence is exactly the quiet-degradation class this project treats as the
        /// dominant failure mode.
        /// </summary>
        private static void RequireMetallicMatchesMask(ExternalPbrAsset asset, Assignment assignment)
        {
            float maskMetallic = Mathf.Clamp01(asset.metallic);
            if (Mathf.Abs(maskMetallic - assignment.Metallic) <= MaskChannelQuantizationTolerance)
                return;

            throw new InvalidOperationException(
                $"[ConstructionGeminiMaterialApplier] Metallic contract broken for {asset.id}: {assignment.MaterialPath} declares {assignment.Metallic} but MaskMap_UnityURP channel R carries {maskMetallic}. URP Lit reads metallic from that channel (LitInput.hlsl:261), so the declared value would never render. Update the assignment to the mask value or repack the mask.");
        }

        private static ExternalPbrAsset RequireAsset(Dictionary<string, ExternalPbrAsset> assets, Assignment assignment)
        {
            if (!assets.TryGetValue(assignment.MaterialId, out ExternalPbrAsset asset))
                throw new InvalidOperationException($"[ConstructionGeminiMaterialApplier] Missing Gemini material id={assignment.MaterialId} for {assignment.MaterialPath}");

            return asset;
        }

        private static Material RequireTargetMaterial(Assignment assignment)
        {
            Material target = AssetDatabase.LoadAssetAtPath<Material>(assignment.MaterialPath);
            if (target == null)
                throw new InvalidOperationException($"[ConstructionGeminiMaterialApplier] Missing construction material: {assignment.MaterialPath}");

            return target;
        }

        private static Texture2D RequireTexture(string assetPath, string materialId, string mapKey, string materialPath)
        {
            Texture2D texture = LoadTexture(assetPath);
            if (texture == null)
                throw new InvalidOperationException($"[ConstructionGeminiMaterialApplier] Missing required map {mapKey} for {materialId}: {materialPath} source={NormalizeAssetPath(assetPath)}");

            return texture;
        }

        private static Texture2D LoadTexture(string assetPath)
        {
            assetPath = NormalizeAssetPath(assetPath);
            return string.IsNullOrWhiteSpace(assetPath) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace("\\", "/").Trim();
        }

        private static string ResolveProjectFilePath(string assetOrFilePath)
        {
            string normalized = NormalizeAssetPath(assetOrFilePath);
            if (string.IsNullOrWhiteSpace(normalized) || Path.IsPathRooted(normalized))
                return normalized;

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, normalized);
        }

        private static void SetTextureIfPresent(Material material, string property, Texture texture)
        {
            if (texture != null && material.HasProperty(property))
                material.SetTexture(property, texture);
        }

        private static void SetFloatIfPresent(Material material, string property, float value)
        {
            if (material.HasProperty(property))
                material.SetFloat(property, value);
        }

        private static void SetTextureScaleIfPresent(Material material, string property, float scale)
        {
            if (material.HasProperty(property))
                material.SetTextureScale(property, new Vector2(scale, scale));
        }

        private static void SetVectorIfPresent(Material material, string property, Vector4 value)
        {
            if (material.HasProperty(property))
                material.SetVector(property, value);
        }

        private static float TilingScale(ExternalPbrAsset asset, Assignment assignment)
        {
            float sourceScale = asset.catalogVersion > 0 ? Mathf.Clamp(asset.tilingScale, 0.25f, 16f) : 1f;
            return Mathf.Clamp(sourceScale * assignment.TilingMultiplier, 0.25f, 16f);
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);
        }

        private sealed class Assignment
        {
            public readonly string MaterialPath;
            public readonly string MaterialId;
            public readonly float TilingMultiplier;
            public readonly float NormalScale;
            public readonly float Metallic;
            public readonly float Smoothness;
            public readonly float HeightScale;
            public readonly string Reason;

            public Assignment(
                string materialPath,
                string materialId,
                float tilingMultiplier,
                float normalScale,
                float metallic,
                float smoothness,
                float heightScale,
                string reason)
            {
                MaterialPath = materialPath;
                MaterialId = materialId;
                TilingMultiplier = tilingMultiplier;
                NormalScale = normalScale;
                Metallic = metallic;
                Smoothness = smoothness;
                HeightScale = heightScale;
                Reason = reason;
            }
        }

        [Serializable]
        private sealed class ExternalPbrManifest
        {
            public ExternalPbrAsset[] assets;
        }

        [Serializable]
        private sealed class ExternalPbrAsset
        {
            public string id;
            public int catalogVersion;
            public float tilingScale;

            /// <summary>
            /// The constant baked into MaskMap_UnityURP channel R, per the manifests' own
            /// <c>mapPacking.unityMaskMap</c> declaration. Field name must stay lowercase to match the
            /// JSON key JsonUtility binds by.
            /// </summary>
            public float metallic;

            /// <summary>
            /// The constant baked into MaskMap_UnityURP channel A. Acts as the smoothness ceiling in
            /// both URP Lit and the wear shader, because both only multiply into it.
            /// </summary>
            public float smoothness;

            public ExternalPbrMaps maps;
        }

        [Serializable]
        private sealed class ExternalPbrMaps
        {
            public string BaseColor;
            public string NormalGL;
            public string MaskMap_UnityURP;
            public string ARM_AO_Rough_Metal;
            public string Height;
        }
    }
}

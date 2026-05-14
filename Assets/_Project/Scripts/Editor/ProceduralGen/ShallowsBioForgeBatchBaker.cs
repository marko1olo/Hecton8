using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Editor.ProceduralGen
{
    /// <summary>
    /// Owns the Safe Shallows Bio-Forge batch bake requested by PROCEDURAL_BIOME_BAKER_SHALLOWS.
    /// </summary>
    public static class ShallowsBioForgeBatchBaker
    {
        private const string RuleFolder = "Assets/_Project/Data/ProceduralGen/Shallows";
        private const string MeshRoot = "Assets/_Project/Art/Generated/Flora/BioForge/Shallows";
        private const string PrefabRoot = "Assets/_Project/Prefabs/Nature/Flora/BioForge/Shallows";
        private const string MaterialPath = "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_ProceduralBio_Shallows.mat";
        private const string TextureRoot = "Assets/_Project/Art/TEXTURES/WorldProceduralFlora";
        private const string AlbedoAtlasPath = TextureRoot + "/TX_ProceduralBio_Shallows_AlbedoAtlas.png";
        private const string NormalAtlasPath = TextureRoot + "/TX_ProceduralBio_Shallows_NormalAtlas.png";
        private const string OrmAtlasPath = TextureRoot + "/TX_ProceduralBio_Shallows_ORMAtlas.png";
        private const string MatCapPath = TextureRoot + "/TX_ProceduralBio_Shallows_MatCap.png";
        private const int AtlasSize = 1024;
        private const int CoralCount = 50;
        private const int KelpCount = 100;
        private const int RockCount = 50;
        private const int MaxAllowedLod2Triangles = 149;
        private const float Lod0ScreenHeight = 0.6f;
        private const float Lod1ScreenHeight = 0.15f;
        private const float Lod2ScreenHeight = 0.04f;
        private const float Lod0FadeWidth = 0.08f;
        private const float Lod1FadeWidth = 0.08f;
        private const float Lod2FadeWidth = 0.04f;
        private const float TransformEpsilonSq = 0.000001f;

        [MenuItem("HECTON-8/Bio-Forge/Bake Safe Shallows Assets", false, 172)]
        public static void BakeSafeShallowsAssets()
        {
            EnsureFolder(RuleFolder);
            EnsureFolder(MeshRoot);
            EnsureFolder(PrefabRoot);
            EnsureFolder("Assets/_Project/Art/Materials/WorldProceduralProxy");
            EnsureFolder(TextureRoot);

            BuildSharedAtlases();
            Material material = CreateOrUpdateMaterial();
            BioRuleData coralRule = CreateOrUpdateTubeCoralRule(material);
            BioRuleData kelpRule = CreateOrUpdateKelpRule(material);
            BioRuleData rockRule = CreateOrUpdatePorousRockRule(material);

            EnsureFolder($"{MeshRoot}/TubeCoral");
            EnsureFolder($"{MeshRoot}/Kelp");
            EnsureFolder($"{MeshRoot}/PorousRock");
            EnsureFolder($"{PrefabRoot}/TubeCoral");
            EnsureFolder($"{PrefabRoot}/Kelp");
            EnsureFolder($"{PrefabRoot}/PorousRock");

            BioForgeGenerator.GenerateFloraBatch(coralRule, unchecked((int)0x5A110001u), "GEN_Shallows_TubeCoral", CoralCount);
            BioForgeGenerator.GenerateFloraBatch(kelpRule, unchecked((int)0x5A110101u), "GEN_Shallows_Kelp", KelpCount);
            BioForgeGenerator.GenerateRockBatch(rockRule, unchecked((int)0x5A110201u), "GEN_Shallows_PorousRock", RockCount);

            ValidateSafeShallowsAssets();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("HECTON-8/Bio-Forge/Validate Safe Shallows Assets", false, 173)]
        public static void ValidateSafeShallowsAssets()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Texture albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoAtlasPath);
            Texture normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalAtlasPath);
            Texture orm = AssetDatabase.LoadAssetAtPath<Texture2D>(OrmAtlasPath);
            Texture matCap = AssetDatabase.LoadAssetAtPath<Texture2D>(MatCapPath);

            int failures = 0;
            ValidateRequiredFolders(ref failures);
            if (material == null || albedo == null || normal == null || orm == null || matCap == null)
            {
                failures++;
                Debug.LogError("[ShallowsBioForgeBatchBaker] Missing shared material or atlas texture dependency.");
            }

            ValidateRuleAssets(material, ref failures);
            ValidateSharedMaterial(material, albedo, normal, orm, matCap, ref failures);
            ValidateAtlasImporter(AlbedoAtlasPath, AtlasKind.Albedo, ref failures);
            ValidateAtlasImporter(NormalAtlasPath, AtlasKind.Normal, ref failures);
            ValidateAtlasImporter(OrmAtlasPath, AtlasKind.Orm, ref failures);
            ValidateAtlasImporter(MatCapPath, AtlasKind.MatCap, ref failures);

            int coral = ValidateFamily("TubeCoral", CoralCount, material, false, ref failures);
            int kelp = ValidateFamily("Kelp", KelpCount, material, false, ref failures);
            int rocks = ValidateFamily("PorousRock", RockCount, material, true, ref failures);
            if (failures > 0)
            {
                Debug.LogError($"[ShallowsBioForgeBatchBaker] Validation failed. Failures={failures}, Coral={coral}, Kelp={kelp}, Rocks={rocks}.");
                return;
            }

            Debug.Log($"[ShallowsBioForgeBatchBaker] Validation passed. Coral={coral}, Kelp={kelp}, Rocks={rocks}, Total={coral + kelp + rocks}, LOD2<{MaxAllowedLod2Triangles + 1}, SharedMaterial={MaterialPath}.");
        }

        private static BioRuleData CreateOrUpdateTubeCoralRule(Material material)
        {
            BioRuleData rule = LoadOrCreateRule($"{RuleFolder}/Rule_Shallows_TubeCoral.asset");
            SerializedObject serialized = new SerializedObject(rule);
            SetString(serialized, "_assetPrefix", "GEN_Shallows_TubeCoral");
            SetObject(serialized, "_material", material);
            SetString(serialized, "_axiom", "F[+F][-F][^F][&F][/F][\\F]");
            SetRules(serialized, new[] { new RuleSpec("F", "F[+F][-F][^F][&F][/F][\\F]") });
            SetInt(serialized, "_iterations", 2);
            SetInt(serialized, "_maxBranches", 1800);
            SetFloat(serialized, "_angleDegrees", 42f);
            SetFloat(serialized, "_stepLength", 0.24f);
            SetFloat(serialized, "_lengthTaper", 0.76f);
            SetFloat(serialized, "_rootRadius", 0.24f);
            SetFloat(serialized, "_radiusTaper", 0.78f);
            SetFloat(serialized, "_minimumRadius", 0.055f);
            SetInt(serialized, "_sdfResolution", 40);
            SetFloat(serialized, "_boundsPadding", 0.32f);
            SetFloat(serialized, "_smoothMinK", 5.5f);
            SetEnum(serialized, "_sdfProfile", BioForgeSdfProfile.BranchCapsules);
            SetInt(serialized, "_lod0TriangleBudget", 2600);
            SetInt(serialized, "_lod1TriangleBudget", 620);
            SetInt(serialized, "_lod2TriangleBudget", 120);
            SetString(serialized, "_meshOutputFolder", $"{MeshRoot}/TubeCoral");
            SetString(serialized, "_prefabOutputFolder", $"{PrefabRoot}/TubeCoral");
            Apply(serialized, rule);
            return rule;
        }

        private static BioRuleData CreateOrUpdateKelpRule(Material material)
        {
            BioRuleData rule = LoadOrCreateRule($"{RuleFolder}/Rule_Shallows_Kelp.asset");
            SerializedObject serialized = new SerializedObject(rule);
            SetString(serialized, "_assetPrefix", "GEN_Shallows_Kelp");
            SetObject(serialized, "_material", material);
            SetString(serialized, "_axiom", "F[+F][-F]F");
            SetRules(serialized, new[] { new RuleSpec("F", "F[+F]F[-F]F") });
            SetInt(serialized, "_iterations", 3);
            SetInt(serialized, "_maxBranches", 2400);
            SetFloat(serialized, "_angleDegrees", 7.5f);
            SetFloat(serialized, "_stepLength", 0.34f);
            SetFloat(serialized, "_lengthTaper", 0.91f);
            SetFloat(serialized, "_rootRadius", 0.055f);
            SetFloat(serialized, "_radiusTaper", 0.83f);
            SetFloat(serialized, "_minimumRadius", 0.018f);
            SetInt(serialized, "_sdfResolution", 36);
            SetFloat(serialized, "_boundsPadding", 0.18f);
            SetFloat(serialized, "_smoothMinK", 13f);
            SetEnum(serialized, "_sdfProfile", BioForgeSdfProfile.RibbonFlora);
            SetFloat(serialized, "_ribbonThicknessScale", 0.12f);
            SetFloat(serialized, "_ribbonWidthScale", 3.3f);
            SetInt(serialized, "_lod0TriangleBudget", 2200);
            SetInt(serialized, "_lod1TriangleBudget", 520);
            SetInt(serialized, "_lod2TriangleBudget", 96);
            SetString(serialized, "_meshOutputFolder", $"{MeshRoot}/Kelp");
            SetString(serialized, "_prefabOutputFolder", $"{PrefabRoot}/Kelp");
            Apply(serialized, rule);
            return rule;
        }

        private static BioRuleData CreateOrUpdatePorousRockRule(Material material)
        {
            BioRuleData rule = LoadOrCreateRule($"{RuleFolder}/Rule_Shallows_PorousRock.asset");
            SerializedObject serialized = new SerializedObject(rule);
            SetString(serialized, "_assetPrefix", "GEN_Shallows_PorousRock");
            SetObject(serialized, "_material", material);
            SetString(serialized, "_axiom", "F");
            SetRules(serialized, new[] { new RuleSpec("F", "F") });
            SetInt(serialized, "_iterations", 0);
            SetInt(serialized, "_maxBranches", 32);
            SetInt(serialized, "_sdfResolution", 38);
            SetFloat(serialized, "_boundsPadding", 0.18f);
            SetFloat(serialized, "_smoothMinK", 8f);
            SetEnum(serialized, "_sdfProfile", BioForgeSdfProfile.PorousRock);
            SetInt(serialized, "_lod0TriangleBudget", 3200);
            SetInt(serialized, "_lod1TriangleBudget", 720);
            SetInt(serialized, "_lod2TriangleBudget", 128);
            SetFloat(serialized, "_rockRadius", 1.15f);
            SetFloat(serialized, "_rockNoiseAmplitude", 0.24f);
            SetFloat(serialized, "_rockNoiseFrequency", 4.2f);
            SetInt(serialized, "_rockPoreCount", 18);
            SetFloat(serialized, "_rockPoreRadius", 0.34f);
            SetFloat(serialized, "_rockPoreSurfaceBias", 0.82f);
            SetString(serialized, "_meshOutputFolder", $"{MeshRoot}/PorousRock");
            SetString(serialized, "_prefabOutputFolder", $"{PrefabRoot}/PorousRock");
            Apply(serialized, rule);
            return rule;
        }

        private static Material CreateOrUpdateMaterial()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Project/Art/Shaders/Hecton_ProceduralBio.shader");
            if (shader == null)
                shader = Shader.Find("Hecton8/Flora/ProceduralBio");
            if (shader == null)
            {
                Debug.LogError("[ShallowsBioForgeBatchBaker] Missing Hecton8/Flora/ProceduralBio shader.");
                return null;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "MAT_ProceduralBio_Shallows"
                };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            material.shader = shader;
            material.enableInstancing = true;
            material.doubleSidedGI = false;
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            material.SetTexture("_AlbedoAtlas", AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoAtlasPath));
            material.SetTexture("_NormalAtlas", AssetDatabase.LoadAssetAtPath<Texture2D>(NormalAtlasPath));
            material.SetTexture("_ORMAtlas", AssetDatabase.LoadAssetAtPath<Texture2D>(OrmAtlasPath));
            material.SetTexture("_MatCap", AssetDatabase.LoadAssetAtPath<Texture2D>(MatCapPath));
            material.SetColor("_BaseColor", new Color(0.64f, 0.82f, 0.62f, 1f));
            material.SetColor("_RootTint", new Color(0.10f, 0.22f, 0.14f, 1f));
            material.SetColor("_TipTint", new Color(0.28f, 0.92f, 0.84f, 1f));
            material.SetColor("_EmissionColor", new Color(0.14f, 0.68f, 0.62f, 1f));
            material.SetFloat("_TriplanarScale", 0.46f);
            material.SetFloat("_TriplanarSharpness", 4.3f);
            material.SetFloat("_SeedOffsetScale", 1.4f);
            material.SetFloat("_NormalScale", 0.84f);
            material.SetFloat("_AmbientStrength", 0.48f);
            material.SetFloat("_SubsurfaceStrength", 0.32f);
            material.SetFloat("_RimStrength", 0.22f);
            material.SetFloat("_SmoothnessBoost", 0.88f);
            material.SetFloat("_MetallicBoost", 0f);
            material.SetFloat("_BiomeTintStrength", 0.34f);
            material.SetFloat("_EmissionStrength", 0.72f);
            material.SetFloat("_BiolumPulseSharpness", 2.4f);
            material.SetFloat("_MatCapStrength", 0.42f);
            material.SetFloat("_Cull", 0f);
            material.DisableKeyword("_QUALITY_HIGH");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void BuildSharedAtlases()
        {
            CreateOrUpdateAtlas(AlbedoAtlasPath, AtlasKind.Albedo);
            CreateOrUpdateAtlas(NormalAtlasPath, AtlasKind.Normal);
            CreateOrUpdateAtlas(OrmAtlasPath, AtlasKind.Orm);
            CreateOrUpdateAtlas(MatCapPath, AtlasKind.MatCap);
        }

        private static void CreateOrUpdateAtlas(string path, AtlasKind kind)
        {
            Texture2D texture = new Texture2D(AtlasSize, AtlasSize, TextureFormat.RGBA32, true, kind == AtlasKind.Normal || kind == AtlasKind.Orm)
            {
                name = Path.GetFileNameWithoutExtension(path)
            };
            Color32[] pixels = new Color32[AtlasSize * AtlasSize];
            for (int y = 0; y < AtlasSize; y++)
            {
                for (int x = 0; x < AtlasSize; x++)
                {
                    pixels[x + y * AtlasSize] = SampleAtlas(kind, x, y);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ConfigureAtlasImporter(path, kind);
        }

        private static Color32 SampleAtlas(AtlasKind kind, int x, int y)
        {
            float u = x / (float)(AtlasSize - 1);
            float v = y / (float)(AtlasSize - 1);
            int quadrant = (u >= 0.5f ? 1 : 0) + (v >= 0.5f ? 2 : 0);
            float n;
            unchecked
            {
                n = Hash01(x * 73856093 ^ y * 19349663 ^ ((int)kind * 83492791));
            }
            float ridges = Mathf.Abs(Mathf.Sin((u * 37f + v * 29f + n * 0.2f) * Mathf.PI));

            if (kind == AtlasKind.Normal)
            {
                byte nx = (byte)Mathf.Clamp(Mathf.RoundToInt(128f + (n - 0.5f) * 28f), 0, 255);
                byte ny = (byte)Mathf.Clamp(Mathf.RoundToInt(128f + (ridges - 0.5f) * 22f), 0, 255);
                return new Color32(nx, ny, 255, 255);
            }

            if (kind == AtlasKind.Orm)
            {
                byte ao = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(145f, 235f, 1f - ridges * 0.55f)), 0, 255);
                byte roughness = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(128f, 212f, ridges)), 0, 255);
                byte emission = (byte)Mathf.Clamp(Mathf.RoundToInt(quadrant == 3 ? 54f + n * 38f : 18f + n * 18f), 0, 255);
                return new Color32(ao, roughness, 0, emission);
            }

            if (kind == AtlasKind.MatCap)
            {
                float cx = u * 2f - 1f;
                float cy = v * 2f - 1f;
                float falloff = Mathf.Clamp01(1f - Mathf.Sqrt(cx * cx + cy * cy));
                byte value = (byte)Mathf.Clamp(Mathf.RoundToInt(74f + falloff * 128f), 0, 255);
                return new Color32(value, value, value, 255);
            }

            Color a;
            Color b;
            switch (quadrant)
            {
                case 0:
                    a = new Color(0.36f, 0.52f, 0.32f, 1f);
                    b = new Color(0.68f, 0.82f, 0.52f, 1f);
                    break;
                case 1:
                    a = new Color(0.72f, 0.58f, 0.46f, 1f);
                    b = new Color(0.94f, 0.74f, 0.58f, 1f);
                    break;
                case 2:
                    a = new Color(0.34f, 0.42f, 0.38f, 1f);
                    b = new Color(0.58f, 0.66f, 0.56f, 1f);
                    break;
                default:
                    a = new Color(0.10f, 0.46f, 0.46f, 1f);
                    b = new Color(0.22f, 0.86f, 0.78f, 1f);
                    break;
            }

            Color color = Color.Lerp(a, b, Mathf.Clamp01(ridges * 0.62f + n * 0.28f));
            return new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255),
                255);
        }

        private static void ConfigureAtlasImporter(string path, AtlasKind kind)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return;

            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.crunchedCompression = false;
            importer.sRGBTexture = kind == AtlasKind.Albedo || kind == AtlasKind.MatCap;
            importer.textureType = kind == AtlasKind.Normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.maxTextureSize = AtlasSize;

            TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
            standalone.overridden = true;
            standalone.maxTextureSize = AtlasSize;
            standalone.textureCompression = TextureImporterCompression.Compressed;
            standalone.crunchedCompression = false;
            standalone.format = kind == AtlasKind.Normal ? TextureImporterFormat.BC5 : TextureImporterFormat.BC7;
            importer.SetPlatformTextureSettings(standalone);
            importer.SaveAndReimport();
        }

        private static void ValidateSharedMaterial(Material material, Texture albedo, Texture normal, Texture orm, Texture matCap, ref int failures)
        {
            if (material == null)
                return;

            if (material.shader == null || material.shader.name != "Hecton8/Flora/ProceduralBio")
            {
                failures++;
                Debug.LogError("[ShallowsBioForgeBatchBaker] Shared material shader contract failed.");
            }

            if (!material.enableInstancing || material.doubleSidedGI || material.globalIlluminationFlags != MaterialGlobalIlluminationFlags.None)
            {
                failures++;
                Debug.LogError("[ShallowsBioForgeBatchBaker] Shared material batching/GI contract failed.");
            }

            if (material.GetTexture("_AlbedoAtlas") != albedo || material.GetTexture("_NormalAtlas") != normal || material.GetTexture("_ORMAtlas") != orm || material.GetTexture("_MatCap") != matCap)
            {
                failures++;
                Debug.LogError("[ShallowsBioForgeBatchBaker] Shared atlas binding mismatch.");
            }

            if (material.IsKeywordEnabled("_QUALITY_HIGH"))
            {
                failures++;
                Debug.LogError("[ShallowsBioForgeBatchBaker] Shared material has forbidden high-quality keyword enabled.");
            }

            ValidateMaterialColor(material, "_BaseColor", new Color(0.64f, 0.82f, 0.62f, 1f), ref failures);
            ValidateMaterialColor(material, "_RootTint", new Color(0.10f, 0.22f, 0.14f, 1f), ref failures);
            ValidateMaterialColor(material, "_TipTint", new Color(0.28f, 0.92f, 0.84f, 1f), ref failures);
            ValidateMaterialColor(material, "_EmissionColor", new Color(0.14f, 0.68f, 0.62f, 1f), ref failures);
            ValidateMaterialFloat(material, "_TriplanarScale", 0.46f, ref failures);
            ValidateMaterialFloat(material, "_TriplanarSharpness", 4.3f, ref failures);
            ValidateMaterialFloat(material, "_SeedOffsetScale", 1.4f, ref failures);
            ValidateMaterialFloat(material, "_NormalScale", 0.84f, ref failures);
            ValidateMaterialFloat(material, "_AmbientStrength", 0.48f, ref failures);
            ValidateMaterialFloat(material, "_SubsurfaceStrength", 0.32f, ref failures);
            ValidateMaterialFloat(material, "_RimStrength", 0.22f, ref failures);
            ValidateMaterialFloat(material, "_SmoothnessBoost", 0.88f, ref failures);
            ValidateMaterialFloat(material, "_MetallicBoost", 0f, ref failures);
            ValidateMaterialFloat(material, "_BiomeTintStrength", 0.34f, ref failures);
            ValidateMaterialFloat(material, "_EmissionStrength", 0.72f, ref failures);
            ValidateMaterialFloat(material, "_BiolumPulseSharpness", 2.4f, ref failures);
            ValidateMaterialFloat(material, "_MatCapStrength", 0.42f, ref failures);
            ValidateMaterialFloat(material, "_Cull", 0f, ref failures);
        }

        private static void ValidateMaterialColor(Material material, string propertyName, Color expected, ref int failures)
        {
            if (!material.HasProperty(propertyName) || !Approximately(material.GetColor(propertyName), expected))
            {
                failures++;
                Debug.LogError($"[ShallowsBioForgeBatchBaker] Shared material color contract failed for {propertyName}.");
            }
        }

        private static void ValidateMaterialFloat(Material material, string propertyName, float expected, ref int failures)
        {
            if (!material.HasProperty(propertyName) || !Approximately(material.GetFloat(propertyName), expected))
            {
                failures++;
                Debug.LogError($"[ShallowsBioForgeBatchBaker] Shared material float contract failed for {propertyName}.");
            }
        }

        private static void ValidateRequiredFolders(ref int failures)
        {
            ValidateFolderExists(RuleFolder, ref failures);
            ValidateFolderExists(MeshRoot, ref failures);
            ValidateFolderExists(PrefabRoot, ref failures);
            ValidateFolderExists(TextureRoot, ref failures);
            ValidateFolderExists($"{MeshRoot}/TubeCoral", ref failures);
            ValidateFolderExists($"{MeshRoot}/Kelp", ref failures);
            ValidateFolderExists($"{MeshRoot}/PorousRock", ref failures);
            ValidateFolderExists($"{PrefabRoot}/TubeCoral", ref failures);
            ValidateFolderExists($"{PrefabRoot}/Kelp", ref failures);
            ValidateFolderExists($"{PrefabRoot}/PorousRock", ref failures);
        }

        private static void ValidateFolderExists(string folder, ref int failures)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            failures++;
            Debug.LogError($"[ShallowsBioForgeBatchBaker] Missing required folder: {folder}.");
        }

        private static void ValidateRuleAssets(Material material, ref int failures)
        {
            ValidateRuleAsset(new RuleExpectation
            {
                Path = $"{RuleFolder}/Rule_Shallows_TubeCoral.asset",
                AssetPrefix = "GEN_Shallows_TubeCoral",
                Axiom = "F[+F][-F][^F][&F][/F][\\F]",
                Replacement = "F[+F][-F][^F][&F][/F][\\F]",
                Profile = BioForgeSdfProfile.BranchCapsules,
                MeshFolder = $"{MeshRoot}/TubeCoral",
                PrefabFolder = $"{PrefabRoot}/TubeCoral",
                Iterations = 2,
                MaxBranches = 1800,
                SdfResolution = 40,
                Lod0 = 2600,
                Lod1 = 620,
                Lod2 = 120,
                AngleDegrees = 42f,
                StepLength = 0.24f,
                LengthTaper = 0.76f,
                RootRadius = 0.24f,
                RadiusTaper = 0.78f,
                MinimumRadius = 0.055f,
                BoundsPadding = 0.32f,
                SmoothMinK = 5.5f,
                RibbonThicknessScale = 0.18f,
                RibbonWidthScale = 2.4f,
                RockRadius = 1.4f,
                RockNoiseAmplitude = 0.22f,
                RockNoiseFrequency = 3.5f,
                RockPoreCount = 0,
                RockPoreRadius = 0.35f,
                RockPoreSurfaceBias = 0.72f
            }, material, ref failures);

            ValidateRuleAsset(new RuleExpectation
            {
                Path = $"{RuleFolder}/Rule_Shallows_Kelp.asset",
                AssetPrefix = "GEN_Shallows_Kelp",
                Axiom = "F[+F][-F]F",
                Replacement = "F[+F]F[-F]F",
                Profile = BioForgeSdfProfile.RibbonFlora,
                MeshFolder = $"{MeshRoot}/Kelp",
                PrefabFolder = $"{PrefabRoot}/Kelp",
                Iterations = 3,
                MaxBranches = 2400,
                SdfResolution = 36,
                Lod0 = 2200,
                Lod1 = 520,
                Lod2 = 96,
                AngleDegrees = 7.5f,
                StepLength = 0.34f,
                LengthTaper = 0.91f,
                RootRadius = 0.055f,
                RadiusTaper = 0.83f,
                MinimumRadius = 0.018f,
                BoundsPadding = 0.18f,
                SmoothMinK = 13f,
                RibbonThicknessScale = 0.12f,
                RibbonWidthScale = 3.3f,
                RockRadius = 1.4f,
                RockNoiseAmplitude = 0.22f,
                RockNoiseFrequency = 3.5f,
                RockPoreCount = 0,
                RockPoreRadius = 0.35f,
                RockPoreSurfaceBias = 0.72f
            }, material, ref failures);

            ValidateRuleAsset(new RuleExpectation
            {
                Path = $"{RuleFolder}/Rule_Shallows_PorousRock.asset",
                AssetPrefix = "GEN_Shallows_PorousRock",
                Axiom = "F",
                Replacement = "F",
                Profile = BioForgeSdfProfile.PorousRock,
                MeshFolder = $"{MeshRoot}/PorousRock",
                PrefabFolder = $"{PrefabRoot}/PorousRock",
                Iterations = 0,
                MaxBranches = 32,
                SdfResolution = 38,
                Lod0 = 3200,
                Lod1 = 720,
                Lod2 = 128,
                AngleDegrees = 24f,
                StepLength = 0.35f,
                LengthTaper = 0.82f,
                RootRadius = 0.13f,
                RadiusTaper = 0.72f,
                MinimumRadius = 0.025f,
                BoundsPadding = 0.18f,
                SmoothMinK = 8f,
                RibbonThicknessScale = 0.18f,
                RibbonWidthScale = 2.4f,
                RockRadius = 1.15f,
                RockNoiseAmplitude = 0.24f,
                RockNoiseFrequency = 4.2f,
                RockPoreCount = 18,
                RockPoreRadius = 0.34f,
                RockPoreSurfaceBias = 0.82f
            }, material, ref failures);
        }

        private static void ValidateRuleAsset(RuleExpectation expected, Material material, ref int failures)
        {
            BioRuleData rule = AssetDatabase.LoadAssetAtPath<BioRuleData>(expected.Path);
            if (rule == null)
            {
                failures++;
                Debug.LogError($"[ShallowsBioForgeBatchBaker] Missing BioRuleData at {expected.Path}.");
                return;
            }

            bool failed = false;
            failed |= rule.AssetPrefix != expected.AssetPrefix;
            failed |= rule.Material != material;
            failed |= rule.Axiom != expected.Axiom;
            failed |= rule.Iterations != expected.Iterations;
            failed |= rule.MaxBranches != expected.MaxBranches;
            failed |= !Approximately(rule.AngleDegrees, expected.AngleDegrees);
            failed |= !Approximately(rule.StepLength, expected.StepLength);
            failed |= !Approximately(rule.LengthTaper, expected.LengthTaper);
            failed |= !Approximately(rule.RootRadius, expected.RootRadius);
            failed |= !Approximately(rule.RadiusTaper, expected.RadiusTaper);
            failed |= !Approximately(rule.MinimumRadius, expected.MinimumRadius);
            failed |= rule.SdfResolution != expected.SdfResolution;
            failed |= !Approximately(rule.BoundsPadding, expected.BoundsPadding);
            failed |= !Approximately(rule.SmoothMinK, expected.SmoothMinK);
            failed |= rule.SdfProfile != expected.Profile;
            failed |= !Approximately(rule.RibbonThicknessScale, expected.RibbonThicknessScale);
            failed |= !Approximately(rule.RibbonWidthScale, expected.RibbonWidthScale);
            failed |= rule.Lod0TriangleBudget != expected.Lod0;
            failed |= rule.Lod1TriangleBudget != expected.Lod1;
            failed |= rule.Lod2TriangleBudget != expected.Lod2;
            failed |= !Approximately(rule.RockRadius, expected.RockRadius);
            failed |= !Approximately(rule.RockNoiseAmplitude, expected.RockNoiseAmplitude);
            failed |= !Approximately(rule.RockNoiseFrequency, expected.RockNoiseFrequency);
            failed |= rule.RockPoreCount != expected.RockPoreCount;
            failed |= !Approximately(rule.RockPoreRadius, expected.RockPoreRadius);
            failed |= !Approximately(rule.RockPoreSurfaceBias, expected.RockPoreSurfaceBias);
            failed |= rule.MeshOutputFolder != expected.MeshFolder;
            failed |= rule.PrefabOutputFolder != expected.PrefabFolder;

            if (!rule.TryGetReplacement('F', out string replacement) || replacement != expected.Replacement)
                failed = true;

            SerializedObject serialized = new SerializedObject(rule);
            SerializedProperty rules = serialized.FindProperty("_rules");
            if (rules == null || !rules.isArray || rules.arraySize != 1)
            {
                failed = true;
            }
            else
            {
                SerializedProperty element = rules.GetArrayElementAtIndex(0);
                failed |= element.FindPropertyRelative("_symbol").stringValue != "F";
                failed |= element.FindPropertyRelative("_replacement").stringValue != expected.Replacement;
            }

            if (failed)
            {
                failures++;
                Debug.LogError($"[ShallowsBioForgeBatchBaker] BioRuleData contract drift at {expected.Path}.");
            }
        }

        private static bool Approximately(float actual, float expected)
        {
            return Mathf.Abs(actual - expected) <= 0.0001f;
        }

        private static bool Approximately(Color actual, Color expected)
        {
            return Approximately(actual.r, expected.r) &&
                   Approximately(actual.g, expected.g) &&
                   Approximately(actual.b, expected.b) &&
                   Approximately(actual.a, expected.a);
        }

        private static bool Approximately(Vector3 actual, Vector3 expected)
        {
            return (actual - expected).sqrMagnitude <= TransformEpsilonSq;
        }

        private static bool Approximately(Quaternion actual, Quaternion expected)
        {
            return Quaternion.Angle(actual, expected) <= 0.01f;
        }

        private static void ValidateAtlasImporter(string path, AtlasKind kind, ref int failures)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                failures++;
                Debug.LogError($"[ShallowsBioForgeBatchBaker] Missing atlas importer at {path}.");
                return;
            }

            bool expectedSrgb = kind == AtlasKind.Albedo || kind == AtlasKind.MatCap;
            TextureImporterType expectedType = kind == AtlasKind.Normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.wrapMode != TextureWrapMode.Repeat || !importer.mipmapEnabled || importer.isReadable || importer.textureCompression != TextureImporterCompression.Compressed || importer.crunchedCompression || importer.sRGBTexture != expectedSrgb || importer.textureType != expectedType || importer.maxTextureSize != AtlasSize)
            {
                failures++;
                Debug.LogError($"[ShallowsBioForgeBatchBaker] Atlas importer contract failed at {path}.");
            }

            TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
            TextureImporterFormat expectedFormat = kind == AtlasKind.Normal ? TextureImporterFormat.BC5 : TextureImporterFormat.BC7;
            if (!standalone.overridden || standalone.maxTextureSize != AtlasSize || standalone.textureCompression != TextureImporterCompression.Compressed || standalone.crunchedCompression || standalone.format != expectedFormat)
            {
                failures++;
                Debug.LogError($"[ShallowsBioForgeBatchBaker] Atlas Standalone platform contract failed at {path}.");
            }
        }

        private static int ValidateFamily(string familyFolder, int expectedCount, Material material, bool rocks, ref int failures)
        {
            string folder = $"{PrefabRoot}/{familyFolder}";
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            int count = guids.Length;
            if (count != expectedCount)
            {
                Debug.LogError($"[ShallowsBioForgeBatchBaker] {familyFolder} count mismatch. Expected={expectedCount}, Actual={count}.");
                failures++;
            }

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                ValidatePrefab(path, familyFolder, prefab, material, rocks, ref failures);
            }

            ValidateMeshFamily(familyFolder, expectedCount, ref failures);
            return count;
        }

        private static void ValidateMeshFamily(string familyFolder, int expectedCount, ref int failures)
        {
            string folder = $"{MeshRoot}/{familyFolder}";
            string[] meshGuids = AssetDatabase.FindAssets("t:Mesh", new[] { folder });
            int expectedMeshCount = expectedCount * 3;
            if (meshGuids.Length != expectedMeshCount)
            {
                failures++;
                Debug.LogError($"[ShallowsBioForgeBatchBaker] {familyFolder} mesh count mismatch. Expected={expectedMeshCount}, Actual={meshGuids.Length}.");
            }

            int lod0 = 0;
            int lod1 = 0;
            int lod2 = 0;
            int unexpected = 0;
            for (int i = 0; i < meshGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(meshGuids[i]);
                if (path.EndsWith("_LOD0.asset", StringComparison.Ordinal)) lod0++;
                else if (path.EndsWith("_LOD1.asset", StringComparison.Ordinal)) lod1++;
                else if (path.EndsWith("_LOD2.asset", StringComparison.Ordinal)) lod2++;
                else unexpected++;
            }

            if (lod0 != expectedCount || lod1 != expectedCount || lod2 != expectedCount || unexpected != 0)
            {
                failures++;
                Debug.LogError($"[ShallowsBioForgeBatchBaker] {familyFolder} LOD mesh distribution mismatch. LOD0={lod0}, LOD1={lod1}, LOD2={lod2}, Unexpected={unexpected}.");
            }
        }

        private static void ValidatePrefab(string path, string familyFolder, GameObject prefab, Material material, bool rock, ref int failures)
        {
            if (prefab == null)
            {
                failures++;
                Debug.LogError($"[ShallowsBioForgeBatchBaker] Missing prefab at {path}.");
                return;
            }

            LODGroup lodGroup = prefab.GetComponent<LODGroup>();
            if (lodGroup == null || lodGroup.GetLODs().Length != 3)
            {
                failures++;
                Debug.LogError($"[ShallowsBioForgeBatchBaker] Invalid LODGroup at {path}.");
                return;
            }

            LOD[] lods = lodGroup.GetLODs();
            ValidatePrefabTransformContract(path, prefab.transform, ref failures);
            ValidateStaticFlagsContract(path, prefab, ref failures);
            ValidateLodGroupContract(path, lodGroup, lods, ref failures);
            ValidateLodContract(path, lods, ref failures);
            ValidatePrefabMeshReferences(path, familyFolder, lods, ref failures);

            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length != 3)
            {
                failures++;
                Debug.LogError($"[ShallowsBioForgeBatchBaker] Renderer count mismatch at {path}. Expected=3, Actual={renderers.Length}.");
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer.sharedMaterial != material)
                {
                    failures++;
                    Debug.LogError($"[ShallowsBioForgeBatchBaker] Material mismatch at {path}.");
                }

                if (renderer.shadowCastingMode != ShadowCastingMode.Off)
                {
                    failures++;
                    Debug.LogError($"[ShallowsBioForgeBatchBaker] Shadow caster enabled at {path}.");
                }

                if (renderer.receiveShadows || renderer.motionVectorGenerationMode != MotionVectorGenerationMode.ForceNoMotion || renderer.lightProbeUsage != LightProbeUsage.Off || renderer.reflectionProbeUsage != ReflectionProbeUsage.Off || renderer.allowOcclusionWhenDynamic)
                {
                    failures++;
                    Debug.LogError($"[ShallowsBioForgeBatchBaker] Renderer hot-path flags invalid at {path}.");
                }

                ValidateRendererTransformContract(path, renderer.transform, ref failures);
            }

            Mesh lod2Mesh = ResolveFirstMesh(lods[2].renderers);
            int lod2Triangles = ResolveTriangleCount(lod2Mesh);
            if (lod2Triangles > MaxAllowedLod2Triangles)
            {
                failures++;
                Debug.LogError($"[ShallowsBioForgeBatchBaker] LOD2 triangle overflow at {path}. Triangles={lod2Triangles}.");
            }

            Mesh lod0Mesh = ResolveFirstMesh(lods[0].renderers);
            ValidateVertexColorGradient(path, lod0Mesh, ref failures);

            MeshCollider[] colliders = prefab.GetComponentsInChildren<MeshCollider>(true);
            if (rock)
            {
                ValidateRockCollider(path, colliders, lods, ref failures);
            }
            else if (colliders.Length != 0)
            {
                failures++;
                Debug.LogError($"[ShallowsBioForgeBatchBaker] Flora has forbidden MeshCollider at {path}.");
            }

            MonoBehaviour[] behaviours = prefab.GetComponentsInChildren<MonoBehaviour>(true);
            if (behaviours.Length != 0)
            {
                failures++;
                Debug.LogError($"[ShallowsBioForgeBatchBaker] Runtime script component found at {path}.");
            }

        }

        private static void ValidatePrefabTransformContract(string path, Transform root, ref int failures)
        {
            if (!Approximately(root.localPosition, Vector3.zero) || !Approximately(root.localRotation, Quaternion.identity) || !Approximately(root.localScale, Vector3.one))
            {
                failures++;
                Debug.LogError($"[ShallowsBioForgeBatchBaker] Root transform contract failed at {path}.");
            }
        }

        private static void ValidateStaticFlagsContract(string path, GameObject prefab, ref int failures)
        {
            Transform[] transforms = prefab.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                int flags = (int)GameObjectUtility.GetStaticEditorFlags(transforms[i].gameObject);
                if (flags == 0)
                    continue;

                failures++;
                Debug.LogError($"[ShallowsBioForgeBatchBaker] Static batching/editor flags are forbidden at {path}. Child={transforms[i].name}, Flags={flags}.");
            }
        }

        private static void ValidateLodGroupContract(string path, LODGroup lodGroup, LOD[] lods, ref int failures)
        {
            if (lodGroup.fadeMode != LODFadeMode.CrossFade || !lodGroup.animateCrossFading)
            {
                failures++;
                Debug.LogError($"[ShallowsBioForgeBatchBaker] LOD crossfade mode contract failed at {path}.");
            }

            if (!Approximately(lods[0].screenRelativeTransitionHeight, Lod0ScreenHeight) ||
                !Approximately(lods[1].screenRelativeTransitionHeight, Lod1ScreenHeight) ||
                !Approximately(lods[2].screenRelativeTransitionHeight, Lod2ScreenHeight) ||
                !Approximately(lods[0].fadeTransitionWidth, Lod0FadeWidth) ||
                !Approximately(lods[1].fadeTransitionWidth, Lod1FadeWidth) ||
                !Approximately(lods[2].fadeTransitionWidth, Lod2FadeWidth))
            {
                failures++;
                Debug.LogError($"[ShallowsBioForgeBatchBaker] LOD distance/fade contract failed at {path}.");
            }
        }

        private static void ValidateRendererTransformContract(string path, Transform rendererTransform, ref int failures)
        {
            if (!string.Equals(rendererTransform.name, "LOD0", StringComparison.Ordinal) &&
                !string.Equals(rendererTransform.name, "LOD1", StringComparison.Ordinal) &&
                !string.Equals(rendererTransform.name, "LOD2", StringComparison.Ordinal))
            {
                failures++;
                Debug.LogError($"[ShallowsBioForgeBatchBaker] LOD child name contract failed at {path}. Child={rendererTransform.name}.");
            }

            if (!Approximately(rendererTransform.localRotation, Quaternion.identity) || !Approximately(rendererTransform.localScale, Vector3.one))
            {
                failures++;
                Debug.LogError($"[ShallowsBioForgeBatchBaker] LOD child transform contract failed at {path}.");
            }
        }

        private static void ValidatePrefabMeshReferences(string path, string familyFolder, LOD[] lods, ref int failures)
        {
            string assetStem = Path.GetFileNameWithoutExtension(path);
            for (int i = 0; i < lods.Length; i++)
            {
                Mesh mesh = ResolveFirstMesh(lods[i].renderers);
                string actualPath = mesh != null ? AssetDatabase.GetAssetPath(mesh) : null;
                string expectedPath = $"{MeshRoot}/{familyFolder}/{assetStem}_LOD{i}.asset";
                if (!string.Equals(actualPath, expectedPath, StringComparison.Ordinal))
                {
                    failures++;
                    Debug.LogError($"[ShallowsBioForgeBatchBaker] LOD{i} mesh reference mismatch at {path}. Expected={expectedPath}, Actual={actualPath}.");
                }
            }
        }

        private static void ValidateLodContract(string path, LOD[] lods, ref int failures)
        {
            for (int i = 0; i < lods.Length; i++)
            {
                Renderer[] renderers = lods[i].renderers;
                if (renderers == null || renderers.Length != 1)
                {
                    failures++;
                    Debug.LogError($"[ShallowsBioForgeBatchBaker] LOD{i} renderer contract failed at {path}.");
                    continue;
                }

                Renderer renderer = renderers[0];
                MeshFilter meshFilter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
                if (renderer == null || meshFilter == null || meshFilter.sharedMesh == null)
                {
                    failures++;
                    Debug.LogError($"[ShallowsBioForgeBatchBaker] LOD{i} mesh contract failed at {path}.");
                }
            }
        }

        private static void ValidateRockCollider(string path, MeshCollider[] colliders, LOD[] lods, ref int failures)
        {
            if (colliders.Length != 1 || !colliders[0].convex || colliders[0].sharedMesh == null)
            {
                failures++;
                Debug.LogError($"[ShallowsBioForgeBatchBaker] Rock collider contract failed at {path}.");
                return;
            }

            Renderer anchor = ResolveFirstRenderer(lods[0].renderers);
            if (anchor == null)
            {
                failures++;
                Debug.LogError($"[ShallowsBioForgeBatchBaker] Rock collider anchor missing at {path}.");
                return;
            }

            Vector3 delta = colliders[0].transform.localPosition - anchor.transform.localPosition;
            if (delta.sqrMagnitude > 0.0001f)
            {
                failures++;
                Debug.LogError($"[ShallowsBioForgeBatchBaker] Rock collider offset mismatch at {path}. DeltaSq={delta.sqrMagnitude:0.000000}.");
            }

            if (!string.Equals(colliders[0].transform.name, "Collision_LOD2", StringComparison.Ordinal) ||
                !Approximately(colliders[0].transform.localRotation, Quaternion.identity) ||
                !Approximately(colliders[0].transform.localScale, Vector3.one))
            {
                failures++;
                Debug.LogError($"[ShallowsBioForgeBatchBaker] Rock collider transform contract failed at {path}.");
            }
        }

        private static void ValidateVertexColorGradient(string path, Mesh mesh, ref int failures)
        {
            if (mesh == null || mesh.vertexCount == 0)
            {
                failures++;
                Debug.LogError($"[ShallowsBioForgeBatchBaker] Missing LOD0 mesh for vertex color gradient at {path}.");
                return;
            }

            Color[] colors = mesh.colors;
            if (colors == null || colors.Length != mesh.vertexCount)
            {
                failures++;
                Debug.LogError($"[ShallowsBioForgeBatchBaker] Missing vertex colors at {path}.");
                return;
            }

            float min = 1f;
            float max = 0f;
            for (int i = 0; i < colors.Length; i++)
            {
                float value = colors[i].r;
                min = Mathf.Min(min, value);
                max = Mathf.Max(max, value);
            }

            if (min > 0.08f || max < 0.82f)
            {
                failures++;
                Debug.LogError($"[ShallowsBioForgeBatchBaker] Vertex color R gradient weak at {path}. Min={min:0.000}, Max={max:0.000}.");
            }
        }

        private static Mesh ResolveFirstMesh(Renderer[] renderers)
        {
            Renderer renderer = ResolveFirstRenderer(renderers);
            if (renderer == null)
                return null;

            MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
            return meshFilter != null ? meshFilter.sharedMesh : null;
        }

        private static Renderer ResolveFirstRenderer(Renderer[] renderers)
        {
            if (renderers == null)
                return null;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                    return renderer;
            }

            return null;
        }

        private static int ResolveTriangleCount(Mesh mesh)
        {
            return mesh != null && mesh.subMeshCount > 0 ? (int)(mesh.GetIndexCount(0) / 3) : 0;
        }

        private static BioRuleData LoadOrCreateRule(string path)
        {
            BioRuleData rule = AssetDatabase.LoadAssetAtPath<BioRuleData>(path);
            if (rule != null)
                return rule;

            rule = ScriptableObject.CreateInstance<BioRuleData>();
            AssetDatabase.CreateAsset(rule, path);
            return rule;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void Apply(SerializedObject serialized, UnityEngine.Object target)
        {
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetString(SerializedObject serialized, string propertyName, string value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
                property.stringValue = value;
        }

        private static void SetInt(SerializedObject serialized, string propertyName, int value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
                property.intValue = value;
        }

        private static void SetFloat(SerializedObject serialized, string propertyName, float value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
                property.floatValue = value;
        }

        private static void SetEnum(SerializedObject serialized, string propertyName, BioForgeSdfProfile value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
                property.enumValueIndex = (int)value;
        }

        private static void SetObject(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
                property.objectReferenceValue = value;
        }

        private static void SetRules(SerializedObject serialized, RuleSpec[] rules)
        {
            SerializedProperty property = serialized.FindProperty("_rules");
            if (property == null || !property.isArray)
                return;

            property.arraySize = rules.Length;
            for (int i = 0; i < rules.Length; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_symbol").stringValue = rules[i].Symbol;
                element.FindPropertyRelative("_replacement").stringValue = rules[i].Replacement;
            }
        }

        private static float Hash01(int value)
        {
            unchecked
            {
                uint h = (uint)value;
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return h * 2.3283064e-10f;
            }
        }

        private readonly struct RuleSpec
        {
            public RuleSpec(string symbol, string replacement)
            {
                Symbol = symbol;
                Replacement = replacement;
            }

            public string Symbol { get; }
            public string Replacement { get; }
        }

        private struct RuleExpectation
        {
            public string Path;
            public string AssetPrefix;
            public string Axiom;
            public string Replacement;
            public string MeshFolder;
            public string PrefabFolder;
            public BioForgeSdfProfile Profile;
            public int Iterations;
            public int MaxBranches;
            public int SdfResolution;
            public int Lod0;
            public int Lod1;
            public int Lod2;
            public int RockPoreCount;
            public float AngleDegrees;
            public float StepLength;
            public float LengthTaper;
            public float RootRadius;
            public float RadiusTaper;
            public float MinimumRadius;
            public float BoundsPadding;
            public float SmoothMinK;
            public float RibbonThicknessScale;
            public float RibbonWidthScale;
            public float RockRadius;
            public float RockNoiseAmplitude;
            public float RockNoiseFrequency;
            public float RockPoreRadius;
            public float RockPoreSurfaceBias;
        }

        private enum AtlasKind : byte
        {
            Albedo = 0,
            Normal = 1,
            Orm = 2,
            MatCap = 3
        }
    }
}

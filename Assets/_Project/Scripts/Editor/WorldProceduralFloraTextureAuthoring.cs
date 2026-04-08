using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Owns editor-only procedural texture generation for flora starter and baked-final materials.
    /// </summary>
    public static class WorldProceduralFloraTextureAuthoring
    {
        private const string TextureRoot = "Assets/_Project/Art/Textures/WorldProceduralFlora";
        private const string FamilyKelpTall = "family.kelp.tall";
        private const string FamilyKelpPatchDense = "family.kelp.patch.dense";
        private const string FamilyKelpCanopy = "family.kelp.canopy";
        private const string FamilyCoralLow = "family.coral.low";
        private const string FamilyCoralBranching = "family.coral.branching";
        private const string FamilyCoralMassive = "family.coral.massive";
        private const string FamilyCoralPlate = "family.coral.plate";

        [MenuItem("Hecton/Authoring/Generate Procedural Flora Textures", priority = 175)]
        public static void Apply()
        {
            EnsureFolder("Assets/_Project/Art");
            EnsureFolder("Assets/_Project/Art/Textures");
            EnsureFolder(TextureRoot);

            int touchedTextures = 0;
            touchedTextures += CreateOrUpdateBaseTexture(TextureRoot + "/TX_KelpTall_Base.asset", new Color(0.18f, 0.44f, 0.21f), new Color(0.22f, 0.58f, 0.28f), new Color(0.36f, 0.72f, 0.42f), 0.18f) ? 1 : 0;
            touchedTextures += CreateOrUpdateBaseTexture(TextureRoot + "/TX_KelpPatch_Base.asset", new Color(0.12f, 0.34f, 0.18f), new Color(0.18f, 0.46f, 0.24f), new Color(0.28f, 0.60f, 0.32f), 0.14f) ? 1 : 0;
            touchedTextures += CreateOrUpdateBaseTexture(TextureRoot + "/TX_KelpCanopy_Base.asset", new Color(0.22f, 0.50f, 0.24f), new Color(0.28f, 0.66f, 0.32f), new Color(0.44f, 0.82f, 0.48f), 0.24f) ? 1 : 0;
            touchedTextures += CreateOrUpdateDetailTexture(TextureRoot + "/TX_KelpTall_Detail.asset", 11) ? 1 : 0;
            touchedTextures += CreateOrUpdateDetailTexture(TextureRoot + "/TX_KelpPatch_Detail.asset", 23) ? 1 : 0;
            touchedTextures += CreateOrUpdateDetailTexture(TextureRoot + "/TX_KelpCanopy_Detail.asset", 37) ? 1 : 0;
            touchedTextures += CreateOrUpdateNormalTexture(TextureRoot + "/TX_KelpTall_Normal.asset", 11, 0.72f) ? 1 : 0;
            touchedTextures += CreateOrUpdateNormalTexture(TextureRoot + "/TX_KelpPatch_Normal.asset", 23, 0.58f) ? 1 : 0;
            touchedTextures += CreateOrUpdateNormalTexture(TextureRoot + "/TX_KelpCanopy_Normal.asset", 37, 0.86f) ? 1 : 0;
            touchedTextures += CreateOrUpdateMaskTexture(TextureRoot + "/TX_KelpTall_Mask.asset", 11, 0.62f, 0.94f) ? 1 : 0;
            touchedTextures += CreateOrUpdateMaskTexture(TextureRoot + "/TX_KelpPatch_Mask.asset", 23, 0.54f, 0.88f) ? 1 : 0;
            touchedTextures += CreateOrUpdateMaskTexture(TextureRoot + "/TX_KelpCanopy_Mask.asset", 37, 0.68f, 0.98f) ? 1 : 0;
            touchedTextures += CreateOrUpdateBaseTexture(TextureRoot + "/TX_CoralLow_Base.asset", new Color(0.48f, 0.28f, 0.26f), new Color(0.70f, 0.42f, 0.34f), new Color(0.88f, 0.64f, 0.48f), 0.12f) ? 1 : 0;
            touchedTextures += CreateOrUpdateBaseTexture(TextureRoot + "/TX_CoralBranching_Base.asset", new Color(0.42f, 0.24f, 0.30f), new Color(0.68f, 0.40f, 0.48f), new Color(0.90f, 0.72f, 0.52f), 0.16f) ? 1 : 0;
            touchedTextures += CreateOrUpdateBaseTexture(TextureRoot + "/TX_CoralMassive_Base.asset", new Color(0.54f, 0.30f, 0.22f), new Color(0.78f, 0.48f, 0.34f), new Color(0.94f, 0.72f, 0.56f), 0.10f) ? 1 : 0;
            touchedTextures += CreateOrUpdateBaseTexture(TextureRoot + "/TX_CoralPlate_Base.asset", new Color(0.30f, 0.34f, 0.40f), new Color(0.50f, 0.54f, 0.62f), new Color(0.82f, 0.78f, 0.62f), 0.14f) ? 1 : 0;
            touchedTextures += CreateOrUpdateDetailTexture(TextureRoot + "/TX_CoralLow_Detail.asset", 41) ? 1 : 0;
            touchedTextures += CreateOrUpdateDetailTexture(TextureRoot + "/TX_CoralBranching_Detail.asset", 53) ? 1 : 0;
            touchedTextures += CreateOrUpdateDetailTexture(TextureRoot + "/TX_CoralMassive_Detail.asset", 67) ? 1 : 0;
            touchedTextures += CreateOrUpdateDetailTexture(TextureRoot + "/TX_CoralPlate_Detail.asset", 79) ? 1 : 0;
            touchedTextures += CreateOrUpdateCoralNormalTexture(TextureRoot + "/TX_CoralLow_Normal.asset", 41, 0.62f) ? 1 : 0;
            touchedTextures += CreateOrUpdateCoralNormalTexture(TextureRoot + "/TX_CoralBranching_Normal.asset", 53, 0.84f) ? 1 : 0;
            touchedTextures += CreateOrUpdateCoralNormalTexture(TextureRoot + "/TX_CoralMassive_Normal.asset", 67, 0.70f) ? 1 : 0;
            touchedTextures += CreateOrUpdateCoralNormalTexture(TextureRoot + "/TX_CoralPlate_Normal.asset", 79, 0.58f) ? 1 : 0;
            touchedTextures += CreateOrUpdateCoralMaskTexture(TextureRoot + "/TX_CoralLow_Mask.asset", 41, 0.44f, 0.78f) ? 1 : 0;
            touchedTextures += CreateOrUpdateCoralMaskTexture(TextureRoot + "/TX_CoralBranching_Mask.asset", 53, 0.36f, 0.86f) ? 1 : 0;
            touchedTextures += CreateOrUpdateCoralMaskTexture(TextureRoot + "/TX_CoralMassive_Mask.asset", 67, 0.52f, 0.74f) ? 1 : 0;
            touchedTextures += CreateOrUpdateCoralMaskTexture(TextureRoot + "/TX_CoralPlate_Mask.asset", 79, 0.34f, 0.92f) ? 1 : 0;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[WorldProceduralFloraTextureAuthoring] Applied flora textures. TouchedTextures={touchedTextures}.");
        }

        public static Texture2D LoadKelpBaseTexture(string familyId)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(ResolveBaseTexturePath(familyId));
        }

        public static Texture2D LoadKelpDetailTexture(string familyId)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(ResolveDetailTexturePath(familyId));
        }

        public static Texture2D LoadKelpNormalTexture(string familyId)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(ResolveNormalTexturePath(familyId));
        }

        public static Texture2D LoadKelpMaskTexture(string familyId)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(ResolveMaskTexturePath(familyId));
        }

        public static Texture2D LoadCoralBaseTexture(string familyId)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(ResolveCoralBaseTexturePath(familyId));
        }

        public static Texture2D LoadCoralDetailTexture(string familyId)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(ResolveCoralDetailTexturePath(familyId));
        }

        public static Texture2D LoadCoralNormalTexture(string familyId)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(ResolveCoralNormalTexturePath(familyId));
        }

        public static Texture2D LoadCoralMaskTexture(string familyId)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(ResolveCoralMaskTexturePath(familyId));
        }

        private static bool CreateOrUpdateBaseTexture(string path, Color lowColor, Color midColor, Color highColor, float bandStrength)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                texture = new Texture2D(64, 256, TextureFormat.RGBA32, false, true)
                {
                    name = System.IO.Path.GetFileNameWithoutExtension(path),
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear,
                    anisoLevel = 1
                };
                AssetDatabase.CreateAsset(texture, path);
            }

            int width = texture.width;
            int height = texture.height;
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = y / (float)(height - 1);
                Color gradient = v < 0.55f
                    ? Color.Lerp(lowColor, midColor, v / 0.55f)
                    : Color.Lerp(midColor, highColor, (v - 0.55f) / 0.45f);

                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)(width - 1);
                    float centerRib = 1.0f - Mathf.Abs(u * 2.0f - 1.0f);
                    float edgeMask = Mathf.Pow(Mathf.Abs(u * 2.0f - 1.0f), 1.25f);
                    float stripe = Mathf.Sin((u * 8.0f + v * 5.5f) * Mathf.PI);
                    float veinA = Mathf.Sin((u * 34.0f - v * 16.0f) * Mathf.PI);
                    float veinB = Mathf.Sin((u * 18.0f + v * 24.0f) * Mathf.PI);
                    float mottled = Mathf.Sin((u * 23.0f + v * 13.0f) * Mathf.PI) * 0.5f + 0.5f;
                    float band = 1.0f + stripe * bandStrength + (mottled - 0.5f) * 0.08f;
                    Color baseTint = gradient * band;
                    Color ribTint = Color.Lerp(baseTint, highColor * 1.08f, centerRib * 0.24f);
                    Color edgeTint = Color.Lerp(ribTint, lowColor * 0.88f + new Color(0.08f, 0.06f, 0.02f), edgeMask * 0.22f);
                    float veinMask = Mathf.Clamp01(0.5f + veinA * 0.16f + veinB * 0.08f);
                    pixels[y * width + x] = Color.Lerp(edgeTint, edgeTint * (0.92f + veinMask * 0.16f), centerRib * 0.42f);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            EditorUtility.SetDirty(texture);
            return true;
        }

        private static bool CreateOrUpdateDetailTexture(string path, int seed)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                texture = new Texture2D(128, 128, TextureFormat.RGBA32, false, true)
                {
                    name = System.IO.Path.GetFileNameWithoutExtension(path),
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear,
                    anisoLevel = 1
                };
                AssetDatabase.CreateAsset(texture, path);
            }

            int width = texture.width;
            int height = texture.height;
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)(width - 1);
                    float a = Mathf.Sin((u * (9 + seed * 0.1f) + v * 5.1f) * Mathf.PI);
                    float b = Mathf.Sin((u * 17.0f - v * (7 + seed * 0.05f)) * Mathf.PI);
                    float c = Mathf.Sin(((u + v) * (11 + seed * 0.07f)) * Mathf.PI);
                    float centerRib = 1.0f - Mathf.Abs(u * 2.0f - 1.0f);
                    float longitudinal = Mathf.Sin((v * (26.0f + seed * 0.03f) + u * 3.5f) * Mathf.PI);
                    float edgeWear = Mathf.Pow(Mathf.Abs(u * 2.0f - 1.0f), 1.45f);
                    float value = Mathf.Clamp01(0.5f + a * 0.24f + b * 0.18f + c * 0.12f + longitudinal * 0.08f + centerRib * 0.12f - edgeWear * 0.08f);
                    pixels[y * width + x] = new Color(value, value, value, 1f);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            EditorUtility.SetDirty(texture);
            return true;
        }

        private static bool CreateOrUpdateNormalTexture(string path, int seed, float normalScale)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                texture = new Texture2D(128, 128, TextureFormat.RGBA32, false, true)
                {
                    name = System.IO.Path.GetFileNameWithoutExtension(path),
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear,
                    anisoLevel = 1
                };
                AssetDatabase.CreateAsset(texture, path);
            }

            int width = texture.width;
            int height = texture.height;
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)(width - 1);
                    float center = SampleLeafHeight(u, v, seed);
                    float sampleX = SampleLeafHeight(Mathf.Repeat(u + 1.0f / width, 1.0f), v, seed);
                    float sampleY = SampleLeafHeight(u, Mathf.Repeat(v + 1.0f / height, 1.0f), seed);
                    Vector3 tangent = new Vector3(1f, 0f, (sampleX - center) * normalScale);
                    Vector3 bitangent = new Vector3(0f, 1f, (sampleY - center) * normalScale);
                    Vector3 normal = Vector3.Cross(tangent, bitangent).normalized;
                    pixels[y * width + x] = new Color(normal.x * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, normal.z * 0.5f + 0.5f, 1f);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            EditorUtility.SetDirty(texture);
            return true;
        }

        private static bool CreateOrUpdateMaskTexture(string path, int seed, float thicknessBase, float thicknessTip)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                texture = new Texture2D(128, 256, TextureFormat.RGBA32, false, true)
                {
                    name = System.IO.Path.GetFileNameWithoutExtension(path),
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear,
                    anisoLevel = 1
                };
                AssetDatabase.CreateAsset(texture, path);
            }

            int width = texture.width;
            int height = texture.height;
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = y / (float)(height - 1);
                float thickness = Mathf.Lerp(thicknessBase, thicknessTip, Mathf.Pow(v, 0.72f));
                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)(width - 1);
                    float centerRib = 1.0f - Mathf.Abs(u * 2.0f - 1.0f);
                    float edgeMask = Mathf.Pow(Mathf.Abs(u * 2.0f - 1.0f), 1.28f);
                    float gloss = Mathf.Clamp01(0.44f + Mathf.Sin((u * (7.0f + seed * 0.08f) + v * 3.1f) * Mathf.PI) * 0.20f + centerRib * 0.22f - edgeMask * 0.10f);
                    float ambientLift = Mathf.Clamp01(0.40f + centerRib * 0.38f + Mathf.Sin((u + v) * (5.0f + seed * 0.04f) * Mathf.PI) * 0.08f);
                    float causticBias = Mathf.Clamp01(0.46f + Mathf.Sin((u * 13.0f - v * (9.0f + seed * 0.03f)) * Mathf.PI) * 0.22f + edgeMask * 0.06f);
                    float thicknessValue = Mathf.Clamp01(thickness + centerRib * 0.12f - edgeMask * 0.14f);
                    pixels[y * width + x] = new Color(thicknessValue, gloss, ambientLift, causticBias);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            EditorUtility.SetDirty(texture);
            return true;
        }

        private static bool CreateOrUpdateCoralNormalTexture(string path, int seed, float normalScale)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                texture = new Texture2D(128, 128, TextureFormat.RGBA32, false, true)
                {
                    name = System.IO.Path.GetFileNameWithoutExtension(path),
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear,
                    anisoLevel = 1
                };
                AssetDatabase.CreateAsset(texture, path);
            }

            int width = texture.width;
            int height = texture.height;
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)(width - 1);
                    float center = SampleCoralHeight(u, v, seed);
                    float sampleX = SampleCoralHeight(Mathf.Repeat(u + 1.0f / width, 1.0f), v, seed);
                    float sampleY = SampleCoralHeight(u, Mathf.Repeat(v + 1.0f / height, 1.0f), seed);
                    Vector3 tangent = new Vector3(1f, 0f, (sampleX - center) * normalScale);
                    Vector3 bitangent = new Vector3(0f, 1f, (sampleY - center) * normalScale);
                    Vector3 normal = Vector3.Cross(tangent, bitangent).normalized;
                    pixels[y * width + x] = new Color(normal.x * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, normal.z * 0.5f + 0.5f, 1f);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            EditorUtility.SetDirty(texture);
            return true;
        }

        private static bool CreateOrUpdateCoralMaskTexture(string path, int seed, float cavityBase, float thicknessBase)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                texture = new Texture2D(128, 128, TextureFormat.RGBA32, false, true)
                {
                    name = System.IO.Path.GetFileNameWithoutExtension(path),
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear,
                    anisoLevel = 1
                };
                AssetDatabase.CreateAsset(texture, path);
            }

            int width = texture.width;
            int height = texture.height;
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)(width - 1);
                    float ridge = Mathf.Clamp01(0.5f + Mathf.Sin((u * (8.0f + seed * 0.05f) + v * 5.2f) * Mathf.PI) * 0.34f);
                    float cavity = Mathf.Clamp01(cavityBase + Mathf.Sin((u * 17.0f - v * (9.0f + seed * 0.03f)) * Mathf.PI) * 0.22f);
                    float gloss = Mathf.Clamp01(0.42f + ridge * 0.34f + Mathf.Sin((u + v) * (7.0f + seed * 0.02f) * Mathf.PI) * 0.12f);
                    float thickness = Mathf.Clamp01(thicknessBase + ridge * 0.18f + Mathf.Sin((u * 5.0f + v * 11.0f) * Mathf.PI) * 0.08f);
                    pixels[y * width + x] = new Color(ridge, gloss, cavity, thickness);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            EditorUtility.SetDirty(texture);
            return true;
        }

        private static string ResolveBaseTexturePath(string familyId)
        {
            switch (familyId)
            {
                case FamilyKelpTall:
                    return TextureRoot + "/TX_KelpTall_Base.asset";
                case FamilyKelpPatchDense:
                    return TextureRoot + "/TX_KelpPatch_Base.asset";
                case FamilyKelpCanopy:
                    return TextureRoot + "/TX_KelpCanopy_Base.asset";
                default:
                    return string.Empty;
            }
        }

        private static string ResolveDetailTexturePath(string familyId)
        {
            switch (familyId)
            {
                case FamilyKelpTall:
                    return TextureRoot + "/TX_KelpTall_Detail.asset";
                case FamilyKelpPatchDense:
                    return TextureRoot + "/TX_KelpPatch_Detail.asset";
                case FamilyKelpCanopy:
                    return TextureRoot + "/TX_KelpCanopy_Detail.asset";
                default:
                    return string.Empty;
            }
        }

        private static string ResolveNormalTexturePath(string familyId)
        {
            switch (familyId)
            {
                case FamilyKelpTall:
                    return TextureRoot + "/TX_KelpTall_Normal.asset";
                case FamilyKelpPatchDense:
                    return TextureRoot + "/TX_KelpPatch_Normal.asset";
                case FamilyKelpCanopy:
                    return TextureRoot + "/TX_KelpCanopy_Normal.asset";
                default:
                    return string.Empty;
            }
        }

        private static string ResolveMaskTexturePath(string familyId)
        {
            switch (familyId)
            {
                case FamilyKelpTall:
                    return TextureRoot + "/TX_KelpTall_Mask.asset";
                case FamilyKelpPatchDense:
                    return TextureRoot + "/TX_KelpPatch_Mask.asset";
                case FamilyKelpCanopy:
                    return TextureRoot + "/TX_KelpCanopy_Mask.asset";
                default:
                    return string.Empty;
            }
        }

        private static string ResolveCoralBaseTexturePath(string familyId)
        {
            switch (familyId)
            {
                case FamilyCoralLow:
                    return TextureRoot + "/TX_CoralLow_Base.asset";
                case FamilyCoralBranching:
                    return TextureRoot + "/TX_CoralBranching_Base.asset";
                case FamilyCoralMassive:
                    return TextureRoot + "/TX_CoralMassive_Base.asset";
                case FamilyCoralPlate:
                    return TextureRoot + "/TX_CoralPlate_Base.asset";
                default:
                    return string.Empty;
            }
        }

        private static string ResolveCoralDetailTexturePath(string familyId)
        {
            switch (familyId)
            {
                case FamilyCoralLow:
                    return TextureRoot + "/TX_CoralLow_Detail.asset";
                case FamilyCoralBranching:
                    return TextureRoot + "/TX_CoralBranching_Detail.asset";
                case FamilyCoralMassive:
                    return TextureRoot + "/TX_CoralMassive_Detail.asset";
                case FamilyCoralPlate:
                    return TextureRoot + "/TX_CoralPlate_Detail.asset";
                default:
                    return string.Empty;
            }
        }

        private static string ResolveCoralNormalTexturePath(string familyId)
        {
            switch (familyId)
            {
                case FamilyCoralLow:
                    return TextureRoot + "/TX_CoralLow_Normal.asset";
                case FamilyCoralBranching:
                    return TextureRoot + "/TX_CoralBranching_Normal.asset";
                case FamilyCoralMassive:
                    return TextureRoot + "/TX_CoralMassive_Normal.asset";
                case FamilyCoralPlate:
                    return TextureRoot + "/TX_CoralPlate_Normal.asset";
                default:
                    return string.Empty;
            }
        }

        private static string ResolveCoralMaskTexturePath(string familyId)
        {
            switch (familyId)
            {
                case FamilyCoralLow:
                    return TextureRoot + "/TX_CoralLow_Mask.asset";
                case FamilyCoralBranching:
                    return TextureRoot + "/TX_CoralBranching_Mask.asset";
                case FamilyCoralMassive:
                    return TextureRoot + "/TX_CoralMassive_Mask.asset";
                case FamilyCoralPlate:
                    return TextureRoot + "/TX_CoralPlate_Mask.asset";
                default:
                    return string.Empty;
            }
        }

        private static float SampleLeafHeight(float u, float v, int seed)
        {
            float stripeA = Mathf.Sin((u * (8.0f + seed * 0.05f) + v * 4.8f) * Mathf.PI);
            float stripeB = Mathf.Sin((u * 21.0f - v * (6.0f + seed * 0.03f)) * Mathf.PI);
            float curl = Mathf.Sin(((u * 0.75f + v) * (12.0f + seed * 0.02f)) * Mathf.PI);
            float centerRib = 1.0f - Mathf.Abs(u * 2.0f - 1.0f);
            float edgeWear = Mathf.Pow(Mathf.Abs(u * 2.0f - 1.0f), 1.35f);
            float microVein = Mathf.Sin((u * 31.0f + v * (17.0f + seed * 0.03f)) * Mathf.PI);
            return stripeA * 0.18f + stripeB * 0.10f + curl * 0.08f + centerRib * 0.18f + microVein * 0.05f - edgeWear * 0.04f;
        }

        private static float SampleCoralHeight(float u, float v, int seed)
        {
            float cells = Mathf.Sin((u * (10.0f + seed * 0.06f) + v * 7.0f) * Mathf.PI);
            float ridges = Mathf.Sin((u * 19.0f - v * (11.0f + seed * 0.04f)) * Mathf.PI);
            float pores = Mathf.Sin(((u + v * 0.85f) * (15.0f + seed * 0.03f)) * Mathf.PI);
            return cells * 0.16f + ridges * 0.12f + pores * 0.10f;
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return;

            int lastSeparator = assetPath.LastIndexOf('/');
            if (lastSeparator <= 0)
                return;

            string parentPath = assetPath.Substring(0, lastSeparator);
            string folderName = assetPath.Substring(lastSeparator + 1);
            EnsureFolder(parentPath);

            if (!AssetDatabase.IsValidFolder(assetPath))
                AssetDatabase.CreateFolder(parentPath, folderName);
        }
    }
}

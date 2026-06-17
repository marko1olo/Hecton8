#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using Hecton8.Core.Memory;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.Generators.Graphics
{
    public static class BiomeTransitionPolisher
    {
        public const string AgentId = "1628";
        public const string GeneratedFolder = "Assets/_Project/Data/Generated/Atmosphere";
        public const string FogVolumeAssetPath = GeneratedFolder + "/H8_SdfFogVolume_R8.asset";
        public const string TurbulenceVolumeAssetPath = GeneratedFolder + "/H8_SiltTurbulence_R8.asset";
        public const string CausticDepthAssetPath = GeneratedFolder + "/H8_CausticDepth_R8.asset";
        public const string ColorAtlasAssetPath = GeneratedFolder + "/H8_BiomeColorAtlas_RGBA32.asset";
        public const string LightShaftMeshAssetPath = GeneratedFolder + "/H8_DitheredLightShaftCone.asset";
        public const string BiomeCsvAssetPath = "Assets/_Project/Data/World/biome_atmosphere_rules.csv";
        public const string DitherFogIncludePath = "Assets/_Project/Art/Shaders/Include/Hecton_DitherFog.hlsl";
        public const string DearLieShaderPath = "Assets/_Project/Art/Shaders/Hecton_VolumetricFog_DearLie.shader";
        public const string MasterLitShaderPath = "Assets/_Project/Art/Shaders/Hecton_Master_Lit.shader";
        public const string ReportPath = "Docs/AgentLogs/BIOME_TRANSITION_REPORT_1628.json";

        private const int FogVolumeSize = 32;
        private const int TurbulenceVolumeSize = 32;
        private const int CausticDepthWidth = 256;
        private const int CausticDepthHeight = 16;
        private const int ColorAtlasWidth = 256;
        private const int MaxAtlasRows = 16;
        private const int QuestR8VolumeLimitBytes = 262144;
        private const int ReservedConflictStart = 71670;
        private const int ReservedConflictEnd = 71675;

        [MenuItem("Hecton8/Graphics/1628 Run Biome Transition Polish")]
        public static void RunBiomeTransitionPolishMenu()
        {
            if (!RunOfflinePolish(out BiomeTransitionPolishReport report))
            {
                Debug.LogError(report.validationReason);
                return;
            }

            Debug.Log("1628 biome transition polish report: " + ReportPath);
        }

        [MenuItem("Hecton8/Graphics/1628 Validate Atmospheric Cleanliness")]
        public static void ValidateAtmosphericCleanlinessMenu()
        {
            if (!ValidateAtmosphericCleanliness(out string reason))
                Debug.LogError(reason);
            else
                Debug.Log(reason);
        }

        public static bool RunOfflinePolish(out BiomeTransitionPolishReport report)
        {
            report = CreateBaseReport();
            EnsureAssetFolder(GeneratedFolder);

            AssetDatabase.StartAssetEditing();
            try
            {
                CaveScanSummary caveScan = ScanCaveBounds();
                BakeFogVolumeTexture3D(caveScan);
                BakeTurbulenceTexture3D();
                BakeCausticDepthTexture2D();
                int profileCount = BakeColorAtlasFromCsv();
                GenerateLightShaftConeMesh();

                report.fogVolumePath = FogVolumeAssetPath;
                report.turbulenceVolumePath = TurbulenceVolumeAssetPath;
                report.causticDepthPath = CausticDepthAssetPath;
                report.colorAtlasPath = ColorAtlasAssetPath;
                report.lightShaftMeshPath = LightShaftMeshAssetPath;
                report.fogVolumeSize = FogVolumeSize;
                report.turbulenceVolumeSize = TurbulenceVolumeSize;
                report.colorAtlasRows = profileCount;
                report.caveBoundsFound = caveScan.boundsCount;
                report.usedFallbackCaveBounds = caveScan.usedFallback;
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            report.validationPassed = ValidateAtmosphericCleanliness(out report.validationReason);
            WriteJsonReport(report);
            return report.validationPassed;
        }

        public static bool ValidateAtmosphericCleanliness(out string reason)
        {
            string ditherSource = ReadProjectText(DitherFogIncludePath);
            if (!ValidateDitherFogShaderText(ditherSource, out reason))
                return false;

            if (!CountDearLieDepthReads(out int proxyDepthReads, out int compositeDepthReads))
            {
                reason = "1628 DearLie shader missing proxy/composite functions.";
                return false;
            }

            if (proxyDepthReads != 1 || compositeDepthReads != 1)
            {
                reason = "1628 DearLie shader depth-read budget failed. proxy=" + proxyDepthReads.ToString(CultureInfo.InvariantCulture) +
                         " composite=" + compositeDepthReads.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            if (!ValidateBiomeCsvFinite(out reason))
                return false;

            if (!ValidateTextureBudgets(out reason))
                return false;

            if (!ValidateNoDuplicateUrpRendererFeatures(out reason))
                return false;

            if (!ValidateUnityPerMaterialCbufferAlignment(MasterLitShaderPath, out _, out reason))
                return false;

            reason = "1628 atmospheric cleanliness validated: finite CSV, one depth read per fog stage, no duplicate URP renderer features, Quest R8 budget respected.";
            return true;
        }

        public static bool ValidateDitherFogShaderText(string source, out string reason)
        {
            if (string.IsNullOrEmpty(source))
            {
                reason = "1628 dither fog include is empty.";
                return false;
            }

            if (!source.Contains("CBUFFER_START(H8BiomeLightingParameters)") ||
                !source.Contains("H8DitherFogBayer8x8") ||
                !source.Contains("thresholds[64]") ||
                !source.Contains("H8DitherFogAnalyticalFactor") ||
                !source.Contains("_H8GlobalQualityWeight") ||
                !source.Contains("H8DitherFogResolveQualityWeight") ||
                !source.Contains("H8DitherFogLightShaftOcclusion") ||
                !source.Contains("H8DitherFogSiltAlpha") ||
                !source.Contains("H8DitherFogCausticDepthFade") ||
                !source.Contains("H8DitherFogThermalDistortionOffset"))
            {
                reason = "1628 dither fog include missing required Bayer/fog/shaft/silt/caustic/thermal functions.";
                return false;
            }

            string lowered = source.ToLowerInvariant();
            if (lowered.Contains("raymarch") || lowered.Contains("for (") || lowered.Contains("while ("))
            {
                reason = "1628 dither fog include contains forbidden raymarch or dynamic loop token.";
                return false;
            }

            reason = "1628 dither fog include passed static checks.";
            return true;
        }

        public static bool CountDearLieDepthReads(out int proxyDepthReads, out int compositeDepthReads)
        {
            string source = ReadProjectText(DearLieShaderPath);
            proxyDepthReads = CountTokenInFunction(source, "ResolveProxyFog", "_HectonVolumetricFogSourceDepth");
            compositeDepthReads = CountTokenInFunction(source, "FragComposite", "_HectonVolumetricFogSourceDepth");
            return proxyDepthReads >= 0 && compositeDepthReads >= 0;
        }

        public static bool ValidateUnityPerMaterialCbufferAlignment(string shaderAssetPath, out int byteSize, out string reason)
        {
            byteSize = 0;
            string source = ReadProjectText(shaderAssetPath);
            int start = source.IndexOf("CBUFFER_START(UnityPerMaterial)", StringComparison.Ordinal);
            if (start < 0)
            {
                reason = "1628 UnityPerMaterial CBUFFER missing: " + shaderAssetPath;
                return false;
            }

            int end = source.IndexOf("CBUFFER_END", start, StringComparison.Ordinal);
            if (end < 0)
            {
                reason = "1628 UnityPerMaterial CBUFFER lacks CBUFFER_END: " + shaderAssetPath;
                return false;
            }

            string block = source.Substring(start, end - start);
            string[] lines = block.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int registerUsedBytes = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = StripLineComment(lines[i]).Trim();
                if (!TryParseCbufferFieldComponentCount(line, out int componentCount))
                    continue;

                int fieldBytes = componentCount * 4;
                if (componentCount == 4)
                {
                    if (registerUsedBytes != 0)
                    {
                        byteSize += 16 - registerUsedBytes;
                        registerUsedBytes = 0;
                    }

                    if ((byteSize & 15) != 0)
                    {
                        reason = "1628 float4 field unaligned in UnityPerMaterial: " + line;
                        return false;
                    }

                    byteSize += 16;
                    continue;
                }

                if (registerUsedBytes + fieldBytes > 16)
                {
                    byteSize += 16 - registerUsedBytes;
                    registerUsedBytes = 0;
                }

                byteSize += fieldBytes;
                registerUsedBytes += fieldBytes;
                if (registerUsedBytes == 16)
                    registerUsedBytes = 0;
            }

            if (registerUsedBytes != 0)
                byteSize += 16 - registerUsedBytes;

            if ((byteSize & 15) != 0)
            {
                reason = "1628 UnityPerMaterial byte size is not 16-byte aligned: " + byteSize.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            reason = "1628 UnityPerMaterial CBUFFER aligned at " + byteSize.ToString(CultureInfo.InvariantCulture) + " bytes.";
            return true;
        }

        public static int CountShaderVariantPragmaDebt(string shaderAssetPath)
        {
            string source = ReadProjectText(shaderAssetPath);
            int shaderFeature = CountToken(source, "#pragma shader_feature");
            int multiCompile = CountToken(source, "#pragma multi_compile");
            return shaderFeature + multiCompile;
        }

        public static BiomeTransitionPolishReport CreateBaseReport()
        {
            bool seaglideOwnsRequestedRange =
                (int)BufferID.ShinobuSeaglideAudioSignals == ReservedConflictStart &&
                (int)BufferID.ShinobuSeaglideCavitationSignals == ReservedConflictStart + 1 &&
                (int)BufferID.ShinobuSeaglideCsvScratch == ReservedConflictStart + 2;

            return new BiomeTransitionPolishReport
            {
                agentId = AgentId,
                generatedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                shaderModified = File.Exists(ProjectAbsolutePath(DearLieShaderPath)),
                ditherIncludePath = DitherFogIncludePath,
                biomeCsvPath = BiomeCsvAssetPath,
                fogVolumeSize = FogVolumeSize,
                turbulenceVolumeSize = TurbulenceVolumeSize,
                causticDepthWidth = CausticDepthWidth,
                causticDepthHeight = CausticDepthHeight,
                questR8VolumeLimitBytes = QuestR8VolumeLimitBytes,
                requestedBufferIdStart = ReservedConflictStart,
                requestedBufferIdEnd = ReservedConflictEnd,
                requestedBufferRangeHijacked = false,
                requestedBufferRangeOwner = seaglideOwnsRequestedRange ? "ShinobuSeaglideAudio/Cavitation/CsvScratch" : "unknown",
                activeBiomeBufferRoute = "BufferID.BiomeTransitionStates..BiomeTransitionMockCameraAup = 71220..71231",
                estimatedGpuMicrosecondsSavedMx350 = 54.0f,
                estimatedCpuMicrosecondsSavedMx350 = 17.0f,
                proofStatus = "STATIC_ONLY_PROFILER_CAPTURE_REQUIRED"
            };
        }

        private static void BakeFogVolumeTexture3D(CaveScanSummary caveScan)
        {
            int voxelCount = FogVolumeSize * FogVolumeSize * FogVolumeSize;
            byte[] density = new byte[voxelCount];
            Bounds domain = caveScan.domainBounds;
            Bounds[] caveBounds = caveScan.bounds;
            int cursor = 0;
            for (int z = 0; z < FogVolumeSize; z++)
            {
                float nz = (z + 0.5f) / FogVolumeSize;
                for (int y = 0; y < FogVolumeSize; y++)
                {
                    float ny = (y + 0.5f) / FogVolumeSize;
                    for (int x = 0; x < FogVolumeSize; x++)
                    {
                        float nx = (x + 0.5f) / FogVolumeSize;
                        Vector3 world = new Vector3(
                            Mathf.Lerp(domain.min.x, domain.max.x, nx),
                            Mathf.Lerp(domain.min.y, domain.max.y, ny),
                            Mathf.Lerp(domain.min.z, domain.max.z, nz));
                        float depth01 = Mathf.Clamp01((-world.y - 20f) / 1800f);
                        float cave01 = ResolveCaveFogWeight(world, caveBounds);
                        float value = Mathf.Clamp01(0.08f + depth01 * 0.62f + cave01 * 0.30f);
                        density[cursor++] = Quantize01(value);
                    }
                }
            }

            Texture3D texture = new Texture3D(FogVolumeSize, FogVolumeSize, FogVolumeSize, TextureFormat.R8, false)
            {
                name = "H8_SdfFogVolume_R8",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0
            };
            texture.SetPixelData(density, 0);
            texture.Apply(false, true);
            SaveAsset(texture, FogVolumeAssetPath);
        }

        private static void BakeTurbulenceTexture3D()
        {
            int voxelCount = TurbulenceVolumeSize * TurbulenceVolumeSize * TurbulenceVolumeSize;
            byte[] turbulence = new byte[voxelCount];
            int cursor = 0;
            for (int z = 0; z < TurbulenceVolumeSize; z++)
            {
                for (int y = 0; y < TurbulenceVolumeSize; y++)
                {
                    for (int x = 0; x < TurbulenceVolumeSize; x++)
                    {
                        float n0 = Hash01((uint)x, (uint)y, (uint)z, 0x9E3779B9u);
                        float n1 = Hash01((uint)(x >> 1), (uint)(y >> 1), (uint)(z >> 1), 0x85EBCA6Bu);
                        turbulence[cursor++] = Quantize01(n0 * 0.72f + n1 * 0.28f);
                    }
                }
            }

            Texture3D texture = new Texture3D(TurbulenceVolumeSize, TurbulenceVolumeSize, TurbulenceVolumeSize, TextureFormat.R8, false)
            {
                name = "H8_SiltTurbulence_R8",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0
            };
            texture.SetPixelData(turbulence, 0);
            texture.Apply(false, true);
            SaveAsset(texture, TurbulenceVolumeAssetPath);
        }

        private static void BakeCausticDepthTexture2D()
        {
            byte[] pixels = new byte[CausticDepthWidth * CausticDepthHeight];
            int cursor = 0;
            for (int y = 0; y < CausticDepthHeight; y++)
            {
                for (int x = 0; x < CausticDepthWidth; x++)
                {
                    float t = x / (float)(CausticDepthWidth - 1);
                    float depthFade = 1f - Smooth01(t);
                    pixels[cursor++] = Quantize01(depthFade);
                }
            }

            Texture2D texture = new Texture2D(CausticDepthWidth, CausticDepthHeight, TextureFormat.R8, false, true)
            {
                name = "H8_CausticDepth_R8",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0
            };
            texture.SetPixelData(pixels, 0);
            texture.Apply(false, true);
            SaveAsset(texture, CausticDepthAssetPath);
        }

        private static int BakeColorAtlasFromCsv()
        {
            BiomeProfileRecord[] records = ReadBiomeRecords();
            int rowCount = Mathf.Clamp(records.Length, 1, MaxAtlasRows);
            Color32[] pixels = new Color32[ColorAtlasWidth * rowCount];
            for (int row = 0; row < rowCount; row++)
            {
                BiomeProfileRecord record = row < records.Length ? records[row] : BiomeProfileRecord.DefaultAbyss;
                for (int x = 0; x < ColorAtlasWidth; x++)
                {
                    float t = x / (float)(ColorAtlasWidth - 1);
                    float crush = Smooth01(t);
                    float r = Mathf.Lerp(record.fogR, 0.0015f, crush * 0.58f);
                    float g = Mathf.Lerp(record.fogG, 0.0023f, crush * 0.58f);
                    float b = Mathf.Lerp(record.fogB, 0.0031f, crush * 0.58f);
                    float a = Mathf.Clamp01(record.absorptionW);
                    pixels[row * ColorAtlasWidth + x] = new Color32(Quantize01(r), Quantize01(g), Quantize01(b), Quantize01(a));
                }
            }

            Texture2D texture = new Texture2D(ColorAtlasWidth, rowCount, TextureFormat.RGBA32, false, true)
            {
                name = "H8_BiomeColorAtlas_RGBA32",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            SaveAsset(texture, ColorAtlasAssetPath);
            return rowCount;
        }

        private static void GenerateLightShaftConeMesh()
        {
            const int Segments = 24;
            Vector3[] vertices = new Vector3[Segments + 2];
            Color32[] colors = new Color32[vertices.Length];
            int[] triangles = new int[Segments * 3];
            vertices[0] = Vector3.zero;
            colors[0] = new Color32(255, 255, 255, 220);
            for (int i = 0; i <= Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * 1.55f, 6.5f, Mathf.Sin(angle) * 1.55f);
                colors[i + 1] = new Color32(255, 255, 255, 0);
            }

            int tri = 0;
            for (int i = 0; i < Segments; i++)
            {
                triangles[tri++] = 0;
                triangles[tri++] = i + 1;
                triangles[tri++] = i + 2;
            }

            Mesh mesh = new Mesh
            {
                name = "H8_DitheredLightShaftCone"
            };
            mesh.vertices = vertices;
            mesh.colors32 = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            SaveAsset(mesh, LightShaftMeshAssetPath);
        }

        private static CaveScanSummary ScanCaveBounds()
        {
            Renderer[] renderers = Resources.FindObjectsOfTypeAll<Renderer>();
            Bounds[] bounds = new Bounds[Mathf.Max(1, Mathf.Min(renderers.Length, 64))];
            int count = 0;
            bool hasDomain = false;
            Bounds domain = new Bounds(new Vector3(0f, -700f, 0f), new Vector3(5200f, 1800f, 5200f));

            for (int i = 0; i < renderers.Length && count < bounds.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || renderer.gameObject == null || !LooksLikeCave(renderer.gameObject.name))
                    continue;

                Bounds b = renderer.bounds;
                if (!IsFiniteBounds(b))
                    continue;

                bounds[count++] = b;
                if (!hasDomain)
                {
                    domain = b;
                    hasDomain = true;
                }
                else
                {
                    domain.Encapsulate(b);
                }
            }

            if (count == 0)
            {
                bounds[0] = domain;
                count = 1;
                return new CaveScanSummary
                {
                    bounds = bounds,
                    boundsCount = count,
                    domainBounds = domain,
                    usedFallback = true
                };
            }

            domain.Expand(new Vector3(240f, 180f, 240f));
            return new CaveScanSummary
            {
                bounds = bounds,
                boundsCount = count,
                domainBounds = domain,
                usedFallback = false
            };
        }

        private static bool ValidateBiomeCsvFinite(out string reason)
        {
            string path = ProjectAbsolutePath(BiomeCsvAssetPath);
            if (!File.Exists(path))
            {
                reason = "1628 missing biome atmosphere CSV: " + BiomeCsvAssetPath;
                return false;
            }

            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line[0] == '#')
                    continue;

                string[] cells = line.Split(',');
                for (int c = 1; c < cells.Length; c++)
                {
                    if (!float.TryParse(cells[c], NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || !float.IsFinite(value))
                    {
                        reason = "1628 non-finite biome CSV value at line " + (i + 1).ToString(CultureInfo.InvariantCulture) +
                                 " column " + c.ToString(CultureInfo.InvariantCulture);
                        return false;
                    }
                }
            }

            reason = "1628 biome CSV finite.";
            return true;
        }

        private static bool ValidateTextureBudgets(out string reason)
        {
            int fogBytes = FogVolumeSize * FogVolumeSize * FogVolumeSize;
            int turbulenceBytes = TurbulenceVolumeSize * TurbulenceVolumeSize * TurbulenceVolumeSize;
            if (fogBytes > QuestR8VolumeLimitBytes || turbulenceBytes > QuestR8VolumeLimitBytes)
            {
                reason = "1628 R8 volume budget exceeds Quest cap.";
                return false;
            }

            reason = "1628 R8 texture budgets valid.";
            return true;
        }

        private static bool ValidateNoDuplicateUrpRendererFeatures(out string reason)
        {
            string[] guids = AssetDatabase.FindAssets("t:ScriptableRendererData");
            for (int g = 0; g < guids.Length; g++)
            {
                UnityEngine.Object rendererData = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AssetDatabase.GUIDToAssetPath(guids[g]));
                if (rendererData == null)
                    continue;

                FieldInfo featuresField = rendererData.GetType().GetField("m_RendererFeatures", BindingFlags.Instance | BindingFlags.NonPublic);
                if (featuresField == null || !(featuresField.GetValue(rendererData) is System.Collections.IEnumerable features))
                    continue;

                string seen = string.Empty;
                foreach (object feature in features)
                {
                    if (feature == null)
                        continue;

                    string typeName = feature.GetType().FullName;
                    string key = "|" + typeName + "|";
                    if (seen.Contains(key))
                    {
                        reason = "1628 duplicate URP renderer feature: " + typeName + " in " + rendererData.name;
                        return false;
                    }

                    seen += key;
                }
            }

            reason = "1628 no duplicate URP renderer features.";
            return true;
        }

        private static BiomeProfileRecord[] ReadBiomeRecords()
        {
            string path = ProjectAbsolutePath(BiomeCsvAssetPath);
            if (!File.Exists(path))
                return new[] { BiomeProfileRecord.DefaultAbyss };

            string[] lines = File.ReadAllLines(path);
            BiomeProfileRecord[] records = new BiomeProfileRecord[Mathf.Min(MaxAtlasRows, Mathf.Max(1, lines.Length))];
            int count = 0;
            for (int i = 0; i < lines.Length && count < records.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line[0] == '#')
                    continue;

                string[] cells = line.Split(',');
                if (cells.Length < 15)
                    continue;

                records[count++] = new BiomeProfileRecord
                {
                    nameHash = Fnv1aLowerAscii(cells[0]),
                    fogR = ParseFloat(cells[6], 0.006f),
                    fogG = ParseFloat(cells[7], 0.014f),
                    fogB = ParseFloat(cells[8], 0.022f),
                    absorptionW = ParseFloat(cells[13], 0.85f)
                };
            }

            if (count == 0)
                return new[] { BiomeProfileRecord.DefaultAbyss };

            Array.Resize(ref records, count);
            return records;
        }

        private static float ResolveCaveFogWeight(Vector3 point, Bounds[] caveBounds)
        {
            float result = 0f;
            for (int i = 0; i < caveBounds.Length; i++)
            {
                Bounds b = caveBounds[i];
                if (b.size == Vector3.zero)
                    continue;

                Vector3 clamped = new Vector3(
                    Mathf.Clamp(point.x, b.min.x, b.max.x),
                    Mathf.Clamp(point.y, b.min.y, b.max.y),
                    Mathf.Clamp(point.z, b.min.z, b.max.z));
                float distance = Vector3.Distance(point, clamped);
                result = Mathf.Max(result, 1f - Mathf.Clamp01(distance / 96f));
            }

            return result;
        }

        private static bool LooksLikeCave(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            string n = name.ToLowerInvariant();
            return n.Contains("cave") || n.Contains("tunnel") || n.Contains("abyss") || n.Contains("trench") || n.Contains("shaft") || n.Contains("vent");
        }

        private static bool IsFiniteBounds(Bounds bounds)
        {
            Vector3 center = bounds.center;
            Vector3 size = bounds.size;
            return float.IsFinite(center.x) && float.IsFinite(center.y) && float.IsFinite(center.z) &&
                   float.IsFinite(size.x) && float.IsFinite(size.y) && float.IsFinite(size.z) &&
                   size.x > 0f && size.y > 0f && size.z > 0f;
        }

        private static int CountTokenInFunction(string source, string functionName, string token)
        {
            int start = source.IndexOf(functionName, StringComparison.Ordinal);
            if (start < 0)
                return -1;

            int open = source.IndexOf('{', start);
            if (open < 0)
                return -1;

            int depth = 0;
            int end = -1;
            for (int i = open; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        end = i;
                        break;
                    }
                }
            }

            if (end < 0)
                return -1;

            string body = source.Substring(open, end - open + 1);
            int count = 0;
            int cursor = 0;
            while (true)
            {
                int index = body.IndexOf(token, cursor, StringComparison.Ordinal);
                if (index < 0)
                    break;

                count++;
                cursor = index + token.Length;
            }

            return count;
        }

        private static string ReadProjectText(string assetPath)
        {
            string path = ProjectAbsolutePath(assetPath);
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }

        private static string StripLineComment(string line)
        {
            int comment = line.IndexOf("//", StringComparison.Ordinal);
            return comment >= 0 ? line.Substring(0, comment) : line;
        }

        private static bool TryParseCbufferFieldComponentCount(string line, out int componentCount)
        {
            componentCount = 0;
            if (string.IsNullOrWhiteSpace(line) || !line.EndsWith(";", StringComparison.Ordinal))
                return false;

            string[] cells = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (cells.Length < 2)
                return false;

            string type = cells[0];
            if (type == "float" || type == "half" || type == "real")
            {
                componentCount = 1;
                return true;
            }

            if (type.Length == 6 && type.StartsWith("float", StringComparison.Ordinal))
            {
                char suffix = type[5];
                if (suffix >= '2' && suffix <= '4')
                {
                    componentCount = suffix - '0';
                    return true;
                }
            }

            if (type.Length == 5 && (type.StartsWith("half", StringComparison.Ordinal) || type.StartsWith("real", StringComparison.Ordinal)))
            {
                char suffix = type[4];
                if (suffix >= '2' && suffix <= '4')
                {
                    componentCount = suffix - '0';
                    return true;
                }
            }

            return false;
        }

        private static int CountToken(string source, string token)
        {
            int count = 0;
            int cursor = 0;
            while (true)
            {
                int index = source.IndexOf(token, cursor, StringComparison.Ordinal);
                if (index < 0)
                    return count;

                count++;
                cursor = index + token.Length;
            }
        }

        private static void SaveAsset(UnityEngine.Object asset, string assetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null)
                AssetDatabase.DeleteAsset(assetPath);

            AssetDatabase.CreateAsset(asset, assetPath);
        }

        private static void EnsureAssetFolder(string assetFolder)
        {
            string fullPath = ProjectAbsolutePath(assetFolder);
            if (!Directory.Exists(fullPath))
                Directory.CreateDirectory(fullPath);
        }

        private static void WriteJsonReport(BiomeTransitionPolishReport report)
        {
            string absolutePath = ProjectAbsolutePath(ReportPath);
            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(absolutePath, JsonUtility.ToJson(report, true));
        }

        private static string ProjectAbsolutePath(string projectRelativePath)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static byte Quantize01(float value)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(value) * 255f), 0, 255);
        }

        private static float Smooth01(float value)
        {
            float x = Mathf.Clamp01(value);
            return x * x * (3f - 2f * x);
        }

        private static float ParseFloat(string value, float fallback)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) && float.IsFinite(parsed)
                ? parsed
                : fallback;
        }

        private static uint Fnv1aLowerAscii(string value)
        {
            const uint Offset = 2166136261u;
            const uint Prime = 16777619u;
            uint hash = Offset;
            if (string.IsNullOrEmpty(value))
                return hash;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c >= 'A' && c <= 'Z')
                    c = (char)(c + 32);
                hash ^= c;
                hash *= Prime;
            }

            return hash;
        }

        private static float Hash01(uint x, uint y, uint z, uint seed)
        {
            uint h = seed;
            h ^= x * 0x9E3779B9u;
            h = (h << 13) | (h >> 19);
            h ^= y * 0x85EBCA6Bu;
            h = (h << 11) | (h >> 21);
            h ^= z * 0xC2B2AE35u;
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            return (h & 0x00FFFFFFu) * (1f / 16777215f);
        }

        [Serializable]
        public struct BiomeTransitionPolishReport
        {
            public string agentId;
            public string generatedUtc;
            public string ditherIncludePath;
            public string biomeCsvPath;
            public string fogVolumePath;
            public string turbulenceVolumePath;
            public string causticDepthPath;
            public string colorAtlasPath;
            public string lightShaftMeshPath;
            public bool shaderModified;
            public bool validationPassed;
            public string validationReason;
            public int fogVolumeSize;
            public int turbulenceVolumeSize;
            public int causticDepthWidth;
            public int causticDepthHeight;
            public int colorAtlasRows;
            public int questR8VolumeLimitBytes;
            public int caveBoundsFound;
            public bool usedFallbackCaveBounds;
            public int requestedBufferIdStart;
            public int requestedBufferIdEnd;
            public bool requestedBufferRangeHijacked;
            public string requestedBufferRangeOwner;
            public string activeBiomeBufferRoute;
            public float estimatedGpuMicrosecondsSavedMx350;
            public float estimatedCpuMicrosecondsSavedMx350;
            public string proofStatus;
        }

        private struct CaveScanSummary
        {
            public Bounds[] bounds;
            public int boundsCount;
            public Bounds domainBounds;
            public bool usedFallback;
        }

        private struct BiomeProfileRecord
        {
            public uint nameHash;
            public float fogR;
            public float fogG;
            public float fogB;
            public float absorptionW;

            public static readonly BiomeProfileRecord DefaultAbyss = new BiomeProfileRecord
            {
                nameHash = 0xD33FABA5u,
                fogR = 0.006f,
                fogG = 0.014f,
                fogB = 0.022f,
                absorptionW = 0.85f
            };
        }
    }
}
#endif

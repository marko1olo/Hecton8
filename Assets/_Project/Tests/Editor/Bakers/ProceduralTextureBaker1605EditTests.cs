using System.IO;
using System.Reflection;
using Hecton8.Core;
using Hecton8.Editor.Bakers;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Tests.Editor.Bakers
{
    public sealed class ProceduralTextureBaker1605EditTests
    {
        private const string ComputePath = "Assets/_Project/Art/Shaders/Bakers/Hecton_ProceduralBaker.compute";
        private const string MraoAtlasShaderPath = "Assets/_Project/Art/Shaders/Bakers/Hecton_MraoAtlasLit.shader";
        private const string BakerPath = "Assets/_Project/Editor/Bakers/ProceduralTextureBaker.cs";
        private const string PackerPath = "Assets/_Project/Editor/Bakers/TextureAtlasPacker.cs";
        private const string ProfilePath = "Assets/_Project/Editor/Bakers/BakeProfileDTO.cs";
        private const string ParticleFlipbookPath = "Assets/_Project/Editor/Bakers/ParticleFlipbookBaker1718.cs";
        private const string ParticulateFlipbookPath1728 = "Assets/_Project/Editor/Bakers/ParticulateFlipbookBaker.cs";
        private const string MarineSnowShaderPath = "Assets/_Project/Art/Shaders/Hecton_MarineSnow.shader";
        private const string FlashlightConeSiltShaderPath = "Assets/_Project/Art/Shaders/Hecton_FlashlightConeSilt.shader";
        private const string MarineSnowRendererPath = "Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs";

        [Test]
        public void BakeProfileSchema_ContainsRequiredPbrControls()
        {
            string schema = BakeProfileSchema1605.JsonSchema;

            Assert.That(schema, Does.Contain("ProfileName"));
            Assert.That(schema, Does.Contain("GlobalQualityWeight"));
            Assert.That(schema, Does.Contain("NoiseScale"));
            Assert.That(schema, Does.Contain("PoreDensity"));
            Assert.That(schema, Does.Contain("RustSpread"));
            Assert.That(schema, Does.Contain("EdgeWearIntensity"));
            Assert.That(schema, Does.Contain("Metallic"));
            Assert.That(schema, Does.Contain("Roughness"));
            Assert.That(schema, Does.Contain("Emissive"));
        }

        [Test]
        public void ComputeShader_DeclaresThreeEditorBakeKernelsAndMraoPacking()
        {
            string shader = File.ReadAllText(ComputePath);

            Assert.That(shader, Does.Contain("#pragma kernel GenerateAlbedo"));
            Assert.That(shader, Does.Contain("#pragma kernel GenerateNormal"));
            Assert.That(shader, Does.Contain("#pragma kernel GenerateMask"));
            Assert.That(shader, Does.Contain("RWTexture2D<float4> _BakerOutput"));
            Assert.That(shader, Does.Contain("uint _BakerSeed"));
            Assert.That(shader, Does.Contain("HectonSeedFloat"));
            Assert.That(shader, Does.Contain("_BakerQualityParams"));
            Assert.That(shader, Does.Contain("[numthreads(8, 8, 1)]"));
            Assert.That(shader, Does.Contain("HectonVoronoi3D"));
            Assert.That(shader, Does.Contain("HectonSimplexFbm"));
            Assert.That(shader, Does.Contain("HectonHeightClamped"));
            Assert.That(shader, Does.Contain("HectonSafeNormalize"));
            Assert.That(shader, Does.Contain("isfinite(lenSq) && lenSq > HECTON_EPSILON"));
            Assert.That(shader, Does.Contain("HectonSafeNormalize(float3((left - right) * strength"));
            Assert.That(shader, Does.Contain("HectonCurvature"));
            Assert.That(shader, Does.Contain("HectonRustMask"));
            Assert.That(shader, Does.Contain("clamp(floor(_BakerTextureSize.z + 0.5), 0.0, 2.0)"));
            Assert.That(shader, Does.Contain("max(spread, 0.001)"));
            Assert.That(shader, Does.Contain("step(0.001, spread)"));
            Assert.That(shader, Does.Contain("curvature * 1.35 + wear * 0.65"));
            Assert.That(shader, Does.Contain("M.R.A.O. packing: R=Metallic, G=Roughness, B=Ambient Occlusion, A=Emissive"));
            Assert.That(shader, Does.Not.Contain("Texture2D<float4> _SeparateAO"));
        }

        [Test]
        public void MraoAtlasShader_DecodesBakerChannelContract()
        {
            string shader = File.ReadAllText(MraoAtlasShaderPath);

            Assert.That(shader, Does.Contain("Shader \"Hecton8/Bakers/MraoAtlasLit\""));
            Assert.That(shader, Does.Contain("_MraoMap"));
            Assert.That(shader, Does.Contain("_MetallicScale(\"Metallic Scale\", Range(0, 1)) = 0"));
            Assert.That(shader, Does.Contain("_EmissionStrength(\"Emission Strength\", Range(0, 4)) = 0"));
            Assert.That(shader, Does.Contain("ShaderLibrary/Shadows.hlsl"));
            Assert.That(shader, Does.Contain("UNITY_TRANSFER_INSTANCE_ID(input, output)"));
            Assert.That(shader, Does.Contain("1605 M.R.A.O. packing: R=Metallic, G=Roughness, B=Ambient Occlusion, A=Emissive"));
            Assert.That(shader, Does.Contain("BuildMraoTangentToWorld"));
            Assert.That(shader, Does.Contain("half smoothness = saturate(1.0h - surface.roughness)"));
            Assert.That(shader, Does.Not.Contain("HectonCoreLitDecodePackedMaskV1"));
            Assert.That(shader, Does.Not.Contain("BuildTangentToWorld(input.normalWS"));
        }

        [Test]
        public void EditorBaker_EnforcesCompressionAndNeverUsesCpuPerlin()
        {
            string baker = File.ReadAllText(BakerPath);

            Assert.That(baker, Does.Contain("[MenuItem(\"HECTON-8/Bakers/1605/Bake Default PBR Seed Pack\""));
            Assert.That(baker, Does.Contain("SystemInfo.supportsComputeShaders"));
            Assert.That(baker, Does.Contain("ResolveSafeTextureSize(profile.TextureSize, profile.GlobalQualityWeight)"));
            Assert.That(baker, Does.Contain("ResolveSafeVariant(profile.Variant)"));
            Assert.That(baker, Does.Contain("int safeVariant = ResolveSafeVariant"));
            Assert.That(baker, Does.Contain("Mathf.FloorToInt(Mathf.Lerp"));
            Assert.That(baker, Does.Contain("s_qualityParamsId"));
            Assert.That(baker, Does.Contain("s_seedId"));
            Assert.That(baker, Does.Contain("compute.SetInt(s_seedId"));
            Assert.That(baker, Does.Contain("sRGB = role == TextureRole.Albedo"));
            Assert.That(baker, Does.Contain("for (int i = 0; i < pixels.Length; i++)"));
            Assert.That(baker, Does.Not.Contain("pixels.Length / 64"));
            Assert.That(baker, Does.Contain("new Texture2D(size, size, TextureFormat.RGBA32, true, role != TextureRole.Albedo)"));
            Assert.That(baker, Does.Contain("if (!rt.Create())"));
            Assert.That(baker, Does.Contain("RenderTexture allocation failed"));
            Assert.That(baker, Does.Contain("TryResolveComputeKernel"));
            Assert.That(baker, Does.Contain("compute shader is null for"));
            Assert.That(baker, Does.Contain("missing kernel "));
            Assert.That(baker, Does.Contain("invalid kernel index for"));
            Assert.That(baker, Does.Contain("compute kernel resolve failed"));
            Assert.That(baker, Does.Contain("GetKernelThreadGroupSizes"));
            Assert.That(baker, Does.Contain("TryResolveDispatchGroups"));
            Assert.That(baker, Does.Contain("groupSizeX == 0u || groupSizeY == 0u"));
            Assert.That(baker, Does.Contain("kernel thread group size is invalid"));
            Assert.That(baker, Does.Not.Contain("CeilDivide(size, (int)groupSizeX)"));
            Assert.That(baker, Does.Contain("Bake dispatch failed"));
            Assert.That(baker, Does.Contain("TryFinalizeAssetDatabase"));
            Assert.That(baker, Does.Contain("default seed pack bake"));
            Assert.That(baker, Does.Contain("finalize failed"));
            Assert.That(baker, Does.Contain("mask texture pixel read failed"));
            Assert.That(baker, Does.Contain("AssetPathExistsNoThrow"));
            Assert.That(baker, Does.Contain("TryDeleteCreatedBakeOutputs"));
            Assert.That(baker, Does.Contain("TryDeleteCreatedAssetNoThrow"));
            Assert.That(baker, Does.Contain("Unknown bake output prior state"));
            Assert.That(baker, Does.Contain("Failed to clean newly-created bake output"));
            Assert.That(baker, Does.Contain("TextureImporterFormat.BC7"));
            Assert.That(baker, Does.Contain("TextureImporterFormat.BC5"));
            Assert.That(baker, Does.Contain("TextureImporterFormat.ASTC_6x6"));
            Assert.That(baker, Does.Contain("name = \"iPhone\""));
            Assert.That(baker, Does.Contain("importer.textureCompression = TextureImporterCompression.CompressedHQ"));
            Assert.That(baker, Does.Not.Contain("textureCompression = TextureImporterCompression.CompressedHQ,"));
            Assert.That(baker, Does.Contain("importer.sRGBTexture = role == TextureRole.Albedo"));
            Assert.That(baker, Does.Contain("importer.alphaIsTransparency = role == TextureRole.Mask"));
            Assert.That(baker, Does.Contain("importer.wrapMode = TextureWrapMode.Clamp"));
            Assert.That(baker, Does.Contain("importer.filterMode = FilterMode.Bilinear"));
            Assert.That(baker, Does.Contain("importer.anisoLevel = role == TextureRole.Normal ? 2 : 1"));
            Assert.That(baker, Does.Contain("TryEnforceTextureImportSettings"));
            Assert.That(baker, Does.Contain("texture import settings failed"));
            Assert.That(baker, Does.Contain("AuditTextureImporterSettings"));
            Assert.That(baker, Does.Contain("texture importer audit failed"));
            Assert.That(baker, Does.Contain("alphaTransparencyCorrect"));
            Assert.That(baker, Does.Contain("wrapCorrect"));
            Assert.That(baker, Does.Contain("filterCorrect"));
            Assert.That(baker, Does.Contain("anisoCorrect"));
            Assert.That(baker, Does.Contain("GetPlatformTextureSettings(\"Standalone\")"));
            Assert.That(baker, Does.Contain("GetPlatformTextureSettings(\"Android\")"));
            Assert.That(baker, Does.Contain("GetPlatformTextureSettings(\"iPhone\")"));
            Assert.That(baker, Does.Contain("standalone.format == standaloneFormat"));
            Assert.That(baker, Does.Contain("android.format == TextureImporterFormat.ASTC_6x6"));
            Assert.That(baker, Does.Contain("iPhone.format == TextureImporterFormat.ASTC_6x6"));
            Assert.That(baker, Does.Contain("iPhoneCorrect"));
            Assert.That(baker, Does.Contain("TryWriteBytesAtomic"));
            Assert.That(baker, Does.Contain("MaxEncodedPngBytes"));
            Assert.That(baker, Does.Contain("PNG byte ceiling exceeded"));
            Assert.That(baker, Does.Contain("AssetFileRollbackSnapshot"));
            Assert.That(baker, Does.Contain("MaxRollbackAssetBytes"));
            Assert.That(baker, Does.Contain("MaxRollbackMetaBytes"));
            Assert.That(baker, Does.Contain("ReadRollbackFileBytes"));
            Assert.That(baker, Does.Contain("rollback file exceeds byte ceiling"));
            Assert.That(baker, Does.Contain("TryCaptureAssetFileRollbackSnapshots"));
            Assert.That(baker, Does.Contain("TryRestoreAssetFileRollbackSnapshots(outputRollback)"));
            Assert.That(baker, Does.Contain("asset file rollback capture failed"));
            Assert.That(baker, Does.Contain("TryWriteBytesAtomicAbsolute"));
            Assert.That(baker, Does.Contain("asset rollback restore write failed"));
            Assert.That(baker, Does.Contain("meta rollback restore write failed"));
            Assert.That(baker, Does.Contain("Failed to restore asset rollback snapshot"));
            Assert.That(baker, Does.Contain("TryEnsureAssetFolder"));
            Assert.That(baker, Does.Contain("SanitizeAssetNameForPath"));
            Assert.That(baker, Does.Contain("File.Replace"));
            Assert.That(baker, Does.Contain("Path.GetFullPath"));
            Assert.That(baker, Does.Contain("asset path escapes Assets"));
            Assert.That(baker, Does.Contain("asset folder must stay under Assets"));
            Assert.That(baker, Does.Contain("asset folder contains invalid segment"));
            Assert.That(baker, Does.Contain("asset folder creation failed"));
            Assert.That(baker, Does.Contain("hasValidAssetNameChar"));
            Assert.That(baker, Does.Contain("s_highCompressionRegex"));
            Assert.That(baker, Does.Contain("s_linearTextureRegex"));
            Assert.That(baker, Does.Contain("s_androidPlatformRegex"));
            Assert.That(baker, Does.Contain("s_iPhonePlatformRegex"));
            Assert.That(baker, Does.Contain(@"textureFormat:\s*(25|50)"));
            Assert.That(baker, Does.Contain(@"textureFormat:\s*(27|48)"));
            Assert.That(baker, Does.Contain(@"buildTarget:\s*Android"));
            Assert.That(baker, Does.Contain(@"buildTarget:\s*iPhone"));
            Assert.That(baker, Does.Contain("mobileOverrides"));
            Assert.That(baker, Does.Contain("meta audit read failed"));
            Assert.That(baker, Does.Contain("meta audit byte ceiling exceeded"));
            Assert.That(baker, Does.Contain("string.IsNullOrWhiteSpace(assetPath)"));
            Assert.That(baker, Does.Not.Contain("Mathf.PerlinNoise"));
            Assert.That(baker, Does.Not.Contain("Texture.Compress("));
        }

        [Test]
        public void ParticleFlipbookBaker1718_ExtendsSharedBakerAndAvoidsRuntimeDependencies()
        {
            string particle = File.ReadAllText(ParticleFlipbookPath);
            string particulate = File.ReadAllText(ParticulateFlipbookPath1728);
            string baker = File.ReadAllText(BakerPath);

            Assert.That(particle, Does.Contain("public static partial class ProceduralTextureBaker"));
            Assert.That(particle, Does.Not.Contain("class ParticleFlipbookBaker1718"));
            Assert.That(particle, Does.Contain("[MenuItem(\"HECTON-8/Bakers/1718/Bake Default Silt And Marine Snow Flipbooks\""));
            Assert.That(particle, Does.Contain("PeriodicSimplex"));
            Assert.That(particle, Does.Contain("EvaluateWorley"));
            Assert.That(particle, Does.Contain("new float4("));
            Assert.That(particle, Does.Contain("math.cos(phase)"));
            Assert.That(particle, Does.Contain("math.sin(phase)"));
            Assert.That(particle, Does.Contain("math.normalizesafe"));
            Assert.That(particle, Does.Contain("float dx = FiniteOrZero(EvaluateDensity"));
            Assert.That(particle, Does.Contain("float dy = FiniteOrZero(EvaluateDensity"));
            Assert.That(particle, Does.Contain("normal = math.normalizesafe(normal"));
            Assert.That(particle, Does.Contain("float highFreq = FiniteOrZero(PeriodicSimplex"));
            Assert.That(particle, Does.Contain("float flow = math.saturate(FiniteOrZero(PeriodicSimplex"));
            Assert.That(particle, Does.Contain("return FiniteOrZero(density)"));
            Assert.That(particle, Does.Contain("RequiredFrameGridSize = 8"));
            Assert.That(particle, Does.Contain("FrameCount = frameGridSize * frameGridSize"));
            Assert.That(particle, Does.Contain("forceRequiredFrameGrid"));
            Assert.That(particle, Does.Contain("ResolveFrameGridSize"));
            Assert.That(particle, Does.Contain("globalQualityWeight >= 0.5f ? RequiredFrameGridSize : 4"));
            Assert.That(particle, Does.Contain("ValidatePadding(in settings, packedMask, normalMap"));
            Assert.That(particle, Does.Contain("Color32 n = normalMap[index]"));
            Assert.That(particle, Does.Contain("n.r != 128 || n.g != 128 || n.b != 255 || n.a != 0"));
            Assert.That(particle, Does.Contain("TryCaptureAssetFileRollbackSnapshots(transactionalPaths"));
            Assert.That(particle, Does.Contain("TryResolveParticleBakeAssetPaths1718"));
            Assert.That(particle, Does.Contain("siltPaths.MaterialPath"));
            Assert.That(particle, Does.Contain("snowPaths.MaterialPath"));
            Assert.That(particle, Does.Contain("using Unity.Collections.LowLevel.Unsafe"));
            Assert.That(particle, Does.Contain("ValidateUnmanagedLayouts1718"));
            Assert.That(particle, Does.Contain("UnsafeUtility.SizeOf<ResolvedBakeSettings>()"));
            Assert.That(particle, Does.Contain("UnsafeUtility.SizeOf<ParticleFlipbookBakeJob>()"));
            Assert.That(particle, Does.Contain("TryEnforceTextureImportSettings(paths.MaskPath, ProceduralTextureBaker.TextureRole.Mask"));
            Assert.That(particle, Does.Contain("TryEnforceTextureImportSettings(paths.NormalPath, ProceduralTextureBaker.TextureRole.Normal"));
            Assert.That(particle, Does.Contain("TryCreateOrUpdateParticleMaterial1718"));
            Assert.That(particle, Does.Contain("MAT_Flipbook_"));
            Assert.That(particle, Does.Contain("Hecton8/VFX/MarineSnow"));
            Assert.That(particle, Does.Contain("Hecton8/VFX/FlashlightConeSilt"));
            Assert.That(particle, Does.Contain("ResolveSafeTextureSize(MaximumAtlasSize, q"));
            Assert.That(particulate, Does.Contain("TryBakeNeutralVolumeTexture1728"));
            Assert.That(particulate, Does.Contain("TX_MarineSnow_EmptyCaveSdf_1x1x1.asset"));
            Assert.That(particulate, Does.Contain("TX_MarineSnow_EmptyAbyssalFlow_1x1x1.asset"));
            Assert.That(particulate, Does.Contain("new Texture3D(1, 1, 1, TextureFormat.RGBA32, false)"));
            Assert.AreEqual(3, CountSourceOccurrences(particulate, "forceRequiredFrameGrid: true"));
            Assert.That(baker, Does.Contain("string[] assetPaths"));
            Assert.That(baker, Does.Contain("new AssetFileRollbackSnapshot[assetPaths.Length]"));
            Assert.That(baker, Does.Contain("BuildSeedFallbackName(profile.Seed)"));
            Assert.That(baker, Does.Not.Contain("profile.Seed.ToString"));
            Assert.That(particle, Does.Not.Contain("GlobalRegistry.Get<"));
            Assert.That(particle, Does.Not.Contain("GetComponent("));
            Assert.That(particle, Does.Not.Contain("System.Linq"));
            Assert.That(particle, Does.Not.Contain("WaitForCompletion"));
            Assert.That(particle, Does.Not.Contain("WriteStaticReport"));
            Assert.That(particle, Does.Not.Contain("File.WriteAllText"));
            Assert.That(particle, Does.Not.Contain("Stopwatch"));
        }

        [Test]
        public void ParticleFlipbookConsumers_UseBakedAtlasContractWithoutHotDependencies()
        {
            string marineSnowShader = File.ReadAllText(MarineSnowShaderPath);
            string flashlightShader = File.ReadAllText(FlashlightConeSiltShaderPath);
            string renderer = File.ReadAllText(MarineSnowRendererPath);

            Assert.That(marineSnowShader, Does.Contain("_MarineSnowMaskAtlas"));
            Assert.That(marineSnowShader, Does.Contain("_MarineSnowNormalAtlas"));
            Assert.That(marineSnowShader, Does.Contain("ResolveMarineSnowFlipbookUV"));
            Assert.That(marineSnowShader, Does.Contain("SAMPLE_TEXTURE2D(_MarineSnowMaskAtlas"));
            Assert.That(marineSnowShader, Does.Contain("SAMPLE_TEXTURE2D(_MarineSnowNormalAtlas"));
            Assert.That(marineSnowShader, Does.Contain("_MarineSnowMaskAtlas_TexelSize"));
            Assert.That(marineSnowShader, Does.Contain("ResolveMarineSnowFrameLocalUv"));
            Assert.That(marineSnowShader, Does.Contain("maskPacked.b * 2.0 - 1.0"));
            Assert.That(marineSnowShader, Does.Contain("UnpackNormal(normalPacked)"));
            Assert.That(marineSnowShader, Does.Contain("lerp(radialShape, saturate(maskPacked.r), maskWeight)"));
            Assert.That(marineSnowShader, Does.Contain("maskPacked.g * maskWeight * saturate(input.headlightBoost)"));

            Assert.That(flashlightShader, Does.Contain("_SiltMaskAtlas"));
            Assert.That(flashlightShader, Does.Contain("_SiltNormalAtlas"));
            Assert.That(flashlightShader, Does.Contain("ResolveSiltFlipbookUV"));
            Assert.That(flashlightShader, Does.Contain("_SiltMaskAtlas_TexelSize"));
            Assert.That(flashlightShader, Does.Contain("ResolveSiltFrameLocalUv"));
            Assert.That(flashlightShader, Does.Contain("maskPacked.b * 2.0 - 1.0"));
            Assert.That(flashlightShader, Does.Contain("UnpackNormal(normalPacked)"));
            Assert.That(flashlightShader, Does.Contain("lerp(hashSilt, saturate(maskPacked.r), maskWeight)"));

            Assert.That(renderer, Does.Contain("MaskAtlasId = Shader.PropertyToID(\"_MarineSnowMaskAtlas\")"));
            Assert.That(renderer, Does.Contain("NormalAtlasId = Shader.PropertyToID(\"_MarineSnowNormalAtlas\")"));
            Assert.That(renderer, Does.Contain("BindMaterialFlipbookAtlasIfNeeded"));
            Assert.That(renderer, Does.Contain("SetMaterialTextureHotIfChanged"));
            Assert.That(renderer, Does.Contain("RefreshMaterialFlipbookAtlasFallbackCold"));
            Assert.That(renderer, Does.Contain("marineSnowMaterial.GetTexture(ShaderIds.MaskAtlasId) as Texture2D"));
            Assert.That(renderer, Does.Contain("marineSnowMaterial.GetTexture(ShaderIds.NormalAtlasId) as Texture2D"));
            Assert.That(renderer, Does.Contain("RefreshAuthoredNeutralVolumeFallbacksColdEditor"));
            Assert.That(renderer, Does.Contain("[System.Diagnostics.Conditional(\"UNITY_EDITOR\")]"));
            Assert.That(renderer, Does.Contain("AssetDatabase.LoadAssetAtPath<Texture3D>(DefaultEmptyCaveSdfTexturePath1728)"));
            Assert.That(renderer, Does.Contain("AssetDatabase.LoadAssetAtPath<Texture3D>(DefaultEmptyAbyssalFlowTexturePath1728)"));
            Assert.That(renderer, Does.Not.Contain("new Texture3D(1, 1, 1"));
            Assert.That(renderer, Does.Contain("marineSnowMaskAtlas != null ? math.saturate(marineSnowMaskAtlasWeight) : 0f"));
            Assert.AreEqual(1, CountSourceOccurrences(renderer, "private void EnsureCsvProfileBackgroundReader("));
            Assert.AreEqual(1, CountSourceOccurrences(renderer, "private void StopCsvProfileBackgroundReader("));
            Assert.AreEqual(1, CountSourceOccurrences(renderer, "private static bool TryJoinCsvProfileThreadNoThrow(Thread reader)"));
            Assert.That(renderer, Does.Contain("private const int CsvProfileThreadJoinTimeoutMilliseconds = CsvProfilePollSliceMilliseconds + 10;"));
            Assert.That(renderer, Does.Contain("catch (Exception)"));
            Assert.That(renderer, Does.Contain("_csvProfileThreadStopRequested = true;"));
            Assert.That(renderer, Does.Contain("TryJoinCsvProfileThreadNoThrow(reader)"));
            Assert.That(renderer, Does.Contain("ReferenceEquals(Thread.CurrentThread, reader)"));
            Assert.That(renderer, Does.Contain("reader.Join(CsvProfileThreadJoinTimeoutMilliseconds);"));
            Assert.That(renderer, Does.Contain("return !reader.IsAlive;"));
            Assert.That(renderer, Does.Not.Contain("reader.Join(CsvProfilePollSliceMilliseconds + 10);"));
            Assert.AreEqual(1, CountSourceOccurrences(renderer, "private void RefreshSiltProfileCsv("));
            Assert.AreEqual(1, CountSourceOccurrences(renderer, "private void RefreshPropwashWakeProfileCsv("));
            Assert.That(renderer, Does.Not.Contain("GlobalRegistry.Get<"));
            Assert.That(renderer, Does.Not.Contain("WaitForCompletion"));
            Assert.That(renderer, Does.Not.Contain("System.Linq"));
        }

        [Test]
        public void GlobalQualityWeight_ScalesBakeResolutionContinuously()
        {
            int low = ProceduralTextureBaker.ResolveSafeTextureSize(4096, 0f);
            int mid = ProceduralTextureBaker.ResolveSafeTextureSize(4096, 0.5f);
            int high = ProceduralTextureBaker.ResolveSafeTextureSize(4096, 1f);

            Assert.AreEqual(512, low);
            Assert.AreEqual(2048, mid);
            Assert.GreaterOrEqual(mid, low);
            Assert.GreaterOrEqual(high, mid);
        }

        private static int CountSourceOccurrences(string source, string needle)
        {
            int count = 0;
            int offset = 0;
            while (offset < source.Length)
            {
                int index = source.IndexOf(needle, offset, System.StringComparison.Ordinal);
                if (index < 0)
                    break;

                count++;
                offset = index + needle.Length;
            }

            return count;
        }

        [Test]
        public void GlobalQualityWeight_ScalesAtlasResolutionContinuously()
        {
            int low = TextureAtlasPacker.ResolveSafeAtlasSize(TextureAtlasPacker.DefaultAtlasSize, 0f);
            int mid = TextureAtlasPacker.ResolveSafeAtlasSize(TextureAtlasPacker.DefaultAtlasSize, 0.5f);
            int high = TextureAtlasPacker.ResolveSafeAtlasSize(TextureAtlasPacker.DefaultAtlasSize, 1f);
            int nonPower = TextureAtlasPacker.ResolveSafeAtlasSize(3333, 1f);
            int oversize = TextureAtlasPacker.ResolveSafeAtlasSize(8192, 1f);

            Assert.AreEqual(TextureAtlasPacker.MinimumAtlasSize, low);
            Assert.GreaterOrEqual(mid, low);
            Assert.GreaterOrEqual(high, mid);
            Assert.LessOrEqual(high, TextureAtlasPacker.DefaultAtlasSize);
            Assert.AreEqual(TextureAtlasPacker.DefaultAtlasSize, nonPower);
            Assert.AreEqual(TextureAtlasPacker.DefaultAtlasSize, oversize);
        }

        [Test]
        public void BakeVariantClamp_RejectsCorruptedEnumWithoutBlackOutput()
        {
            Assert.AreEqual(0, ProceduralTextureBaker.ResolveSafeVariant(HectonBakeVariant.Organic));
            Assert.AreEqual(2, ProceduralTextureBaker.ResolveSafeVariant((HectonBakeVariant)99));
            Assert.AreEqual(0, ProceduralTextureBaker.ResolveSafeVariant((HectonBakeVariant)(-8)));
        }

        [Test]
        public void AssetNameSanitizer_RejectsNamesWithNoValidFilenameCharacters()
        {
            MethodInfo method = typeof(ProceduralTextureBaker).GetMethod("SanitizeAssetNameForPath", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);

            Assert.AreEqual(string.Empty, method.Invoke(null, new object[] { "!!!" }) as string);
            Assert.AreEqual("Rock_01", method.Invoke(null, new object[] { "Rock 01" }) as string);
        }

        [Test]
        public void AtlasPacker_UsesMaxRectsPaddingAndMeshVertexBufferUvOverwrite()
        {
            string packer = File.ReadAllText(PackerPath);

            Assert.That(packer, Does.Contain("DefaultPadding = 4"));
            Assert.That(packer, Does.Contain("MaxTextureSetsPerAtlas = 256"));
            Assert.That(packer, Does.Contain("too many texture sets for one atlas"));
            Assert.That(packer, Does.Contain("MinimumAtlasSize = 512"));
            Assert.That(packer, Does.Contain("IsSupportedAtlasSize"));
            Assert.That(packer, Does.Contain("ResolveSafeAtlasSize(DefaultAtlasSize)"));
            Assert.That(packer, Does.Contain("ResolveSafeAtlasSize(int requestedAtlasSize, float globalQualityWeight)"));
            Assert.That(packer, Does.Contain("TryPackTextureSets(inputs, outputFolder, atlasName, atlasSize, padding, 1f"));
            Assert.That(packer, Does.Contain("return ResolveSafeAtlasSize(requestedAtlasSize, 1f)"));
            Assert.That(packer, Does.Contain("ProceduralTextureBaker.ResolveSafeTextureSize(requestedAtlasSize, globalQualityWeight)"));
            Assert.That(packer, Does.Contain("atlas size must be positive"));
            Assert.That(packer, Does.Contain("int safeAtlasSize = ResolveSafeAtlasSize(atlasSize, globalQualityWeight)"));
            Assert.That(packer, Does.Contain("atlasSize = safeAtlasSize"));
            Assert.That(packer, Does.Contain("resolved atlas size is unsupported"));
            Assert.That(packer, Does.Not.Contain("atlas size must be a power-of-two between"));
            Assert.That(packer, Does.Not.Contain("atlas size exceeds current VRAM-safe limit"));
            Assert.That(packer, Does.Contain("TryPackRectangles"));
            Assert.That(packer, Does.Contain("SplitFreeRectangles"));
            Assert.That(packer, Does.Contain("totalPaddingLong"));
            Assert.That(packer, Does.Contain("maxSourceDimension"));
            Assert.That(packer, Does.Contain("TryBuildTextureSetsFromSelection"));
            Assert.That(packer, Does.Contain("TryResolveAssetObjectName"));
            Assert.That(packer, Does.Contain("selection asset name lookup failed"));
            Assert.That(packer, Does.Contain("TryParseTextureRoleSuffix"));
            Assert.That(packer, Does.Contain("TryParseMeshSetKey"));
            Assert.That(packer, Does.Contain("TryAssignMesh"));
            Assert.That(packer, Does.Contain("TryValidateAllMeshUvsBeforeAssetWrites"));
            Assert.That(packer, Does.Contain("TryValidateMeshUv0ReadableForRemap"));
            Assert.That(packer, Does.Contain("TryComputeVertexBufferByteLength"));
            Assert.That(packer, Does.Contain("long byteLengthLong = (long)vertexCount * stride"));
            Assert.That(packer, Does.Contain("mesh vertex buffer byte length is invalid"));
            Assert.That(packer, Does.Contain("MaxMeshUvRollbackBytes"));
            Assert.That(packer, Does.Contain("mesh vertex buffer byte length exceeds rollback ceiling"));
            Assert.That(packer, Does.Contain("RequiredUv0ByteWidth = 8"));
            Assert.That(packer, Does.Contain("offset + RequiredUv0ByteWidth <= stride"));
            Assert.That(packer, Does.Contain("TryCreateAtlasScratch"));
            Assert.That(packer, Does.Contain("long pixelCountLong = (long)atlasSize * atlasSize"));
            Assert.That(packer, Does.Contain("AtlasScratchBytesPerPixel = 4L"));
            Assert.That(packer, Does.Contain("MaxAtlasScratchBytes"));
            Assert.That(packer, Does.Contain("atlas scratch byte ceiling exceeded"));
            Assert.That(packer, Does.Contain("MaxAtlasEncodedPngBytes"));
            Assert.That(packer, Does.Contain("atlas PNG byte ceiling exceeded"));
            Assert.That(packer, Does.Contain("atlas scratch buffer is too small"));
            Assert.That(packer, Does.Contain("albedoPath, atlasPixels"));
            Assert.That(packer, Does.Contain("normalPath, atlasPixels"));
            Assert.That(packer, Does.Contain("maskPath, atlasPixels"));
            Assert.That(packer, Does.Not.Contain("new Color32[atlasSize * atlasSize]"));
            Assert.That(packer, Does.Contain("CopyVertexBufferFromMesh(mesh, stream, vertexBytes)"));
            Assert.That(packer, Does.Contain("vertexBytes.Dispose()"));
            Assert.That(packer, Does.Contain("mesh preflight failed"));
            Assert.That(packer, Does.Contain("texture name must end with _Albedo, _Normal, _MRAO, or _Mask"));
            Assert.That(packer, Does.Not.Contain("selected[i * 3]"));
            Assert.That(packer, Does.Contain("FillAtlasBackground"));
            Assert.That(packer, Does.Contain("new Color32(128, 128, 128, 255)"));
            Assert.That(packer, Does.Contain("new Color32(128, 128, 255, 255)"));
            Assert.That(packer, Does.Contain("new Color32(0, 255, 255, 0)"));
            Assert.That(packer, Does.Contain("BlitWithEdgePadding"));
            Assert.That(packer, Does.Contain("TryEnsureAssetFolder(outputFolder"));
            Assert.That(packer, Does.Contain("safeAtlasName"));
            Assert.That(packer, Does.Contain("atlas name has no valid asset filename characters"));
            Assert.That(packer, Does.Contain("MeshUtility.AcquireReadOnlyMeshData"));
            Assert.That(packer, Does.Contain("GetVertexData<byte>"));
            Assert.That(packer, Does.Contain("throw new InvalidOperationException(\"Mesh vertex buffer shorter than expected for UV remap."));
            Assert.That(packer, Does.Not.Contain("throw new FatalArchitectureException(\"Mesh vertex buffer shorter than expected for UV remap."));
            Assert.That(packer, Does.Contain("SetVertexBufferData"));
            Assert.That(packer, Does.Contain("Persist once in TryFinalizeAtlasTransaction after every mesh remap succeeds."));
            Assert.That(packer, Does.Contain("RemapMeshUVsJob"));
            Assert.That(packer, Does.Contain("TryReadTexturePixels"));
            Assert.That(packer, Does.Contain("MaxAtlasSourcePixels"));
            Assert.That(packer, Does.Contain("TryValidateSourceTextureReadDimensions"));
            Assert.That(packer, Does.Contain("source texture pixel count exceeds atlas read ceiling"));
            Assert.That(packer, Does.Contain("direct texture read pixel count mismatch"));
            Assert.That(packer, Does.Contain("texture pixel count mismatch after readable import"));
            Assert.That(packer, Does.Contain("importer.isReadable = true"));
            Assert.That(packer, Does.Contain("source = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath)"));
            Assert.That(packer, Does.Contain("texture readable bridge produced null asset"));
            Assert.That(packer, Does.Contain("directReadFailure = ex.GetType().Name + \": \" + ex.Message"));
            Assert.That(packer, Does.Contain("TryResolveTextureImporterForReadableBridge"));
            Assert.That(packer, Does.Contain("texture importer lookup failed for atlas packing"));
            Assert.That(packer, Does.Contain("TryRestoreTextureReadableState"));
            Assert.That(packer, Does.Contain("restoreReadableState && !TryRestoreTextureReadableState"));
            Assert.That(packer, Does.Contain("readable restore failed"));
            Assert.That(packer, Does.Contain("TryValidateSourcePixelBuffer"));
            Assert.That(packer, Does.Contain("source texture pixel buffer mismatch"));
            Assert.That(packer, Does.Not.Contain("RestoreTextureReadableState(importer, false, assetPath);"));
            Assert.That(packer, Does.Contain("new Texture2D(atlasSize, atlasSize, TextureFormat.RGBA32, true, role != ProceduralTextureBaker.TextureRole.Albedo)"));
            Assert.That(packer, Does.Contain("catch (Exception ex) when"));
            Assert.That(packer, Does.Contain("IsRecoverableEditorException"));
            Assert.That(packer, Does.Contain("catch (Exception ex) when (IsRecoverableEditorException(ex))"));
            Assert.That(packer, Does.Not.Contain("when (!(ex is FatalArchitectureException))"));
            Assert.That(packer, Does.Contain("MraoAtlasShaderName"));
            Assert.That(packer, Does.Contain("TryCreateOrUpdateMaterial"));
            Assert.That(packer, Does.Contain("MaterialRollbackSnapshot"));
            Assert.That(packer, Does.Contain("TryCaptureMaterialRollbackSnapshot"));
            Assert.That(packer, Does.Contain("TryRestoreMaterialRollbackSnapshot"));
            Assert.That(packer, Does.Contain("TryCaptureAssetFileRollbackSnapshots"));
            Assert.That(packer, Does.Contain("TryCaptureAssetFileRollbackSnapshots(albedoPath, normalPath, maskPath, materialPath"));
            Assert.That(packer, Does.Contain("TryRestoreAssetFileRollbackSnapshots(assetRollback)"));
            Assert.That(packer, Does.Not.Contain("TryRestoreAssetFileRollbackSnapshots(textureRollback)"));
            Assert.That(packer, Does.Contain("material rollback capture failed"));
            Assert.That(packer, Does.Contain("existing material path is not an imported Material asset"));
            Assert.That(packer, Does.Contain("RestoreTextureProperty"));
            Assert.That(packer, Does.Contain("RestoreFloatProperty"));
            Assert.That(packer, Does.Contain("NormalMapKeyword"));
            Assert.That(packer, Does.Contain("NormalMapKeywordEnabled"));
            Assert.That(packer, Does.Contain("RestoreKeyword"));
            Assert.That(packer, Does.Contain("TrySaveMaterialRollbackSnapshot"));
            Assert.That(packer, Does.Contain("Failed to save restored material rollback snapshot"));
            Assert.That(packer, Does.Contain("missing required shader"));
            Assert.That(packer, Does.Contain("atlas texture import missing after write"));
            Assert.That(packer, Does.Contain("atlas build failed for"));
            Assert.That(packer, Does.Contain("material creation failed for"));
            Assert.That(packer, Does.Contain("ex is NotSupportedException"));
            Assert.That(packer, Does.Contain("TryDeleteNewMaterialAsset"));
            Assert.That(packer, Does.Contain("TryDestroyTransientMaterial"));
            Assert.That(packer, Does.Contain("TryDeleteCreatedAtlasOutputs"));
            Assert.That(packer, Does.Contain("TryDeleteCreatedAsset"));
            Assert.That(packer, Does.Contain("if (!deletedByAssetDatabase && TryResolveAbsoluteAssetPathNoThrow(assetPath, out string absolutePath))"));
            Assert.That(packer, Does.Contain("if (File.Exists(absolutePath))"));
            Assert.That(packer, Does.Contain("absolutePath + \".meta\""));
            Assert.That(packer, Does.Contain("if (File.Exists(metaPath))"));
            Assert.That(packer, Does.Contain("AssetPathExists"));
            Assert.That(packer, Does.Contain("TryResolveAbsoluteAssetPathNoThrow"));
            Assert.That(packer, Does.Contain("TryRemapMeshesAndFinalizeWithRollback"));
            Assert.That(packer, Does.Contain("TryCaptureMeshUvRollbackSnapshot"));
            Assert.That(packer, Does.Contain("TryRestoreMeshUvRollbackSnapshots"));
            Assert.That(packer, Does.Contain("TryFindPackedRectForSource"));
            Assert.That(packer, Does.Contain("packed rect array is null"));
            Assert.That(packer, Does.Contain("missing packed rect for source"));
            Assert.That(packer, Does.Not.Contain("throw new FatalArchitectureException(\"Missing packed rect for source"));
            Assert.That(packer, Does.Contain("TryFinalizeAtlasTransaction"));
            Assert.That(packer, Does.Contain("TryFinalizeAssetDatabase(\"atlas transaction\""));
            Assert.That(packer, Does.Contain("GetObjectNameNoThrow"));
            Assert.That(packer, Does.Contain("mesh assigned to multiple atlas sets"));
            Assert.That(packer, Does.Contain("TryEnforceTextureImportSettings(assetPath, role, atlasSize"));
            Assert.That(packer, Does.Contain("\"_MraoMap\""));
            Assert.That(packer, Does.Not.Contain("\"_MaskMap\""));
            Assert.That(packer, Does.Not.Contain("\"_MetallicGlossMap\""));
            Assert.That(packer, Does.Not.Contain("Universal Render Pipeline/Lit"));
            Assert.That(packer, Does.Not.Contain("Shader.Find(\"Standard\")"));
            Assert.That(packer, Does.Contain("[BurstCompile"));
        }

        [Test]
        public void AtlasPacker_RejectsUnsupportedAtlasSizes()
        {
            TextureAtlasPacker.TextureRect[] inputs =
            {
                new TextureAtlasPacker.TextureRect(64, 64)
            };
            TextureAtlasPacker.PackedRect[] output = new TextureAtlasPacker.PackedRect[1];

            Assert.IsFalse(TextureAtlasPacker.TryPackRectangles(new TextureAtlasPacker.TextureRect[0], TextureAtlasPacker.DefaultAtlasSize, TextureAtlasPacker.DefaultPadding, new TextureAtlasPacker.PackedRect[0], out _));
            Assert.IsFalse(TextureAtlasPacker.TryPackRectangles(inputs, 3333, TextureAtlasPacker.DefaultPadding, output, out _));
            Assert.IsFalse(TextureAtlasPacker.TryPackRectangles(inputs, 8192, TextureAtlasPacker.DefaultPadding, output, out _));
            Assert.IsFalse(TextureAtlasPacker.TryPackRectangles(inputs, TextureAtlasPacker.DefaultAtlasSize, int.MaxValue, output, out _));
            Assert.IsFalse(TextureAtlasPacker.TryPackRectangles(inputs, TextureAtlasPacker.DefaultAtlasSize, TextureAtlasPacker.DefaultAtlasSize / 2, output, out _));
            Assert.IsTrue(TextureAtlasPacker.TryPackRectangles(inputs, TextureAtlasPacker.MinimumAtlasSize, TextureAtlasPacker.DefaultPadding, output, out _));

            TextureAtlasPacker.TextureRect[] overflowInput =
            {
                new TextureAtlasPacker.TextureRect(int.MaxValue, 64)
            };
            Assert.IsFalse(TextureAtlasPacker.TryPackRectangles(overflowInput, TextureAtlasPacker.DefaultAtlasSize, TextureAtlasPacker.DefaultPadding, output, out _));

            TextureAtlasPacker.TextureRect[] tooManyInputs = new TextureAtlasPacker.TextureRect[TextureAtlasPacker.MaxTextureSetsPerAtlas + 1];
            TextureAtlasPacker.PackedRect[] tooManyOutput = new TextureAtlasPacker.PackedRect[tooManyInputs.Length];
            for (int i = 0; i < tooManyInputs.Length; i++)
                tooManyInputs[i] = new TextureAtlasPacker.TextureRect(1, 1);

            Assert.IsFalse(TextureAtlasPacker.TryPackRectangles(tooManyInputs, TextureAtlasPacker.DefaultAtlasSize, TextureAtlasPacker.DefaultPadding, tooManyOutput, out _));
        }

        [Test]
        public void AtlasPacker_GroupsSelectedTexturesBySuffixNotSelectionOrder()
        {
            Texture2D albedo = new Texture2D(1, 1) { name = "TX_Rock_Albedo" };
            Texture2D normal = new Texture2D(1, 1) { name = "TX_Rock_Normal" };
            Texture2D mask = new Texture2D(1, 1) { name = "TX_Rock_MRAO" };
            Mesh mesh = new Mesh { name = "TX_Rock_Mesh" };
            try
            {
                MethodInfo method = typeof(TextureAtlasPacker).GetMethod("TryBuildTextureSetsFromSelection", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.IsNotNull(method);

                object[] args =
                {
                    new UnityEngine.Object[] { normal, mesh, mask, albedo },
                    null,
                    null
                };

                bool ok = (bool)method.Invoke(null, args);
                Assert.IsTrue(ok, args[2] as string);

                TextureAtlasPacker.TextureSetInput[] grouped = (TextureAtlasPacker.TextureSetInput[])args[1];
                Assert.AreEqual(1, grouped.Length);
                Assert.AreSame(albedo, grouped[0].Albedo);
                Assert.AreSame(normal, grouped[0].Normal);
                Assert.AreSame(mask, grouped[0].Mask);
                Assert.AreSame(mesh, grouped[0].Mesh);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(albedo);
                UnityEngine.Object.DestroyImmediate(normal);
                UnityEngine.Object.DestroyImmediate(mask);
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void AtlasPacker_RejectsMismatchedRoleTextureDimensionsBeforeAssetWrites()
        {
            Texture2D albedo = new Texture2D(8, 8) { name = "TX_Mismatch_Albedo" };
            Texture2D normal = new Texture2D(4, 8) { name = "TX_Mismatch_Normal" };
            Texture2D mask = new Texture2D(8, 8) { name = "TX_Mismatch_MRAO" };
            try
            {
                TextureAtlasPacker.TextureSetInput[] inputs =
                {
                    new TextureAtlasPacker.TextureSetInput("TX_Mismatch", albedo, normal, mask, null)
                };

                bool ok = TextureAtlasPacker.TryPackTextureSets(
                    inputs,
                    "Assets/_Project/Art/Generated/TextureBaker1605/DimensionRejectProbe",
                    "TXA_dimension_reject_1605",
                    TextureAtlasPacker.MinimumAtlasSize,
                    TextureAtlasPacker.DefaultPadding,
                    out TextureAtlasPacker.AtlasBuildResult _,
                    out string failure);

                Assert.IsFalse(ok);
                Assert.That(failure, Does.Contain("texture dimensions mismatch"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(albedo);
                UnityEngine.Object.DestroyImmediate(normal);
                UnityEngine.Object.DestroyImmediate(mask);
            }
        }

        [Test]
        public void AtlasPacker_RejectsMeshWithoutUv0BeforeAssetWrites()
        {
            const string outputFolder = "Assets/_Project/Art/Generated/TextureBaker1605/MeshPreflightRejectProbe";
            Texture2D albedo = new Texture2D(8, 8) { name = "TX_NoUv_Albedo" };
            Texture2D normal = new Texture2D(8, 8) { name = "TX_NoUv_Normal" };
            Texture2D mask = new Texture2D(8, 8) { name = "TX_NoUv_MRAO" };
            Mesh mesh = new Mesh { name = "TX_NoUv_Mesh" };
            try
            {
                mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };

                TextureAtlasPacker.TextureSetInput[] inputs =
                {
                    new TextureAtlasPacker.TextureSetInput("TX_NoUv", albedo, normal, mask, mesh)
                };

                bool ok = TextureAtlasPacker.TryPackTextureSets(
                    inputs,
                    outputFolder,
                    "TXA_mesh_preflight_reject_1605",
                    TextureAtlasPacker.MinimumAtlasSize,
                    TextureAtlasPacker.DefaultPadding,
                    out TextureAtlasPacker.AtlasBuildResult _,
                    out string failure);

                string absoluteProbe = Path.Combine(Directory.GetCurrentDirectory(), outputFolder.Replace('/', Path.DirectorySeparatorChar));
                Assert.IsFalse(ok);
                Assert.That(failure, Does.Contain("mesh preflight failed"));
                Assert.That(failure, Does.Contain("mesh UV0 is missing"));
                Assert.IsFalse(Directory.Exists(absoluteProbe));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(albedo);
                UnityEngine.Object.DestroyImmediate(normal);
                UnityEngine.Object.DestroyImmediate(mask);
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void MaxRectsFuzzer_PacksOneHundredRectsWithoutOverlap()
        {
            const int count = 100;
            TextureAtlasPacker.TextureRect[] inputs = new TextureAtlasPacker.TextureRect[count];
            TextureAtlasPacker.PackedRect[] output = new TextureAtlasPacker.PackedRect[count];

            uint state = 1605u;
            for (int i = 0; i < count; i++)
            {
                state = state * 1664525u + 1013904223u;
                int width = 64 + (int)(state & 255u);
                state = state * 1664525u + 1013904223u;
                int height = 64 + (int)(state & 255u);
                inputs[i] = new TextureAtlasPacker.TextureRect(width, height);
            }

            bool packed = TextureAtlasPacker.TryPackRectangles(inputs, TextureAtlasPacker.DefaultAtlasSize, TextureAtlasPacker.DefaultPadding, output, out float efficiency);
            Assert.IsTrue(packed);
            Assert.Greater(efficiency, 0.20f);

            for (int i = 0; i < output.Length; i++)
            {
                for (int j = i + 1; j < output.Length; j++)
                {
                    if (Intersects(output[i].PaddedRect, output[j].PaddedRect))
                        throw new FatalArchitectureException("Atlas overlap between rect " + i + " and " + j);
                }
            }
        }

        [Test]
        public void RemapMeshUvsJob_MatchesScaleOffsetToFiveDecimals()
        {
            NativeArray<float2> uvs = new NativeArray<float2>(4, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            try
            {
                uvs[0] = new float2(0f, 0f);
                uvs[1] = new float2(1f, 0f);
                uvs[2] = new float2(0f, 1f);
                uvs[3] = new float2(1f, 1f);

                TextureAtlasPacker.RemapMeshUVsJob job = new TextureAtlasPacker.RemapMeshUVsJob
                {
                    Uvs = uvs,
                    Scale = new float2(0.5f, 0.5f),
                    Offset = new float2(0.25f, 0.25f)
                };
                job.Run(uvs.Length);

                AssertUv(uvs[0], new float2(0.25f, 0.25f), 0);
                AssertUv(uvs[1], new float2(0.75f, 0.25f), 1);
                AssertUv(uvs[2], new float2(0.25f, 0.75f), 2);
                AssertUv(uvs[3], new float2(0.75f, 0.75f), 3);
            }
            finally
            {
                if (uvs.IsCreated)
                    uvs.Dispose();
            }
        }

        [Test]
        public void BakerFiles_AreEditorOnlyAndDoNotMutateRuntimeDomains()
        {
            Assert.IsTrue(File.Exists(BakerPath));
            Assert.IsTrue(File.Exists(PackerPath));
            Assert.IsTrue(File.Exists(ProfilePath));
            Assert.That(BakerPath, Does.StartWith("Assets/_Project/Editor/"));
            Assert.That(PackerPath, Does.StartWith("Assets/_Project/Editor/"));
            Assert.That(ProfilePath, Does.StartWith("Assets/_Project/Editor/"));
        }

        private static bool Intersects(RectInt a, RectInt b)
        {
            return a.x < b.xMax && a.xMax > b.x && a.y < b.yMax && a.yMax > b.y;
        }

        private static void AssertUv(float2 actual, float2 expected, int index)
        {
            if (math.abs(actual.x - expected.x) > 0.00001f || math.abs(actual.y - expected.y) > 0.00001f)
                throw new FatalArchitectureException("UV remap mismatch at " + index + " expected=" + expected + " actual=" + actual);
        }
    }
}

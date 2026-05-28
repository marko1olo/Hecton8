using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class KelpShaderScalability1427EditTests
    {
        [TestCase("Hecton_KelpMaster.shader")]
        [TestCase("Hecton_KelpMaster_GPUI.shader")]
        [TestCase("Hecton_CoralMaster.shader")]
        [TestCase("Hecton_CoralMaster_GPUI.shader")]
        [TestCase("Hecton_ProceduralBio.shader")]
        [TestCase("Hecton_RetinaDistortion.shader")]
        public void PresentationShaders_RemoveBinaryQualityVariants(string shaderFile)
        {
            string source = ReadShader(shaderFile);

            Assert.That(source, Does.Not.Contain("_QUALITY_MX350"));
            Assert.That(source, Does.Not.Contain("_QUALITY_HIGH"));
            Assert.That(source, Does.Not.Contain("shader_feature_local _QUALITY"));
        }

        [TestCase("Hecton_KelpMaster.shader")]
        [TestCase("Hecton_KelpMaster_GPUI.shader")]
        public void KelpShaders_ConsumeGlobalQualityContinuously(string shaderFile)
        {
            string source = ReadShader(shaderFile);

            Assert.That(source, Does.Contain("_H8GlobalQualityWeight"));
            Assert.That(source, Does.Contain("HectonKelpSmoothRange01"));
            Assert.That(source, Does.Contain("lerp(0.58h, 1.0h, HectonKelpSmoothRange01(0.0h, 0.85h, qualityWeight))"));
        }

        [TestCase("Hecton_KelpMaster.shader")]
        [TestCase("Hecton_KelpMaster_GPUI.shader")]
        public void KelpShaders_UseArithmeticDearLieWaveInsteadOfTrig(string shaderFile)
        {
            string source = ReadShader(shaderFile);

            Assert.That(source, Does.Not.Contain("sin("));
            Assert.That(source, Does.Not.Contain("cos("));
            Assert.That(source, Does.Not.Contain("HectonKelpBoundedSin"));
            Assert.That(source, Does.Contain("HectonKelpDearLieWave"));
            Assert.That(source, Does.Contain("wave * (1.5 - 0.5 * wave * wave)"));
        }

        [TestCase("Hecton_KelpMaster.shader")]
        [TestCase("Hecton_KelpMaster_GPUI.shader")]
        [TestCase("Hecton_CoralMaster.shader")]
        [TestCase("Hecton_CoralMaster_GPUI.shader")]
        public void FloraShaders_KeepParallaxContinuousWithoutHighTierResample(string shaderFile)
        {
            string source = ReadShader(shaderFile);

            Assert.That(source, Does.Contain("parallaxQualityWeight"));
            Assert.That(source, Does.Contain("samplePositionWS -= viewDirWS * ((maskSample.b - 0.5h) * _HeightScale * parallaxQualityWeight)"));
            Assert.That(source, Does.Not.Contain("                maskSample = SampleFloraTriplanar"));
            Assert.That(source, Does.Not.Contain("                maskSample = SampleFloraDominantAxis"));
        }

        [Test]
        public void ProceduralBio_TriplanarSharpenConsumesContinuousQuality()
        {
            string source = ReadShader("Hecton_ProceduralBio.shader");

            Assert.That(source, Does.Contain("sharpen *= HectonProceduralBioSmoothRange01(0.35h, 0.95h, HectonProceduralBioGlobalQualityWeight())"));
            Assert.That(source, Does.Not.Contain("#if defined(_QUALITY_HIGH)"));
        }

        [Test]
        public void RetinaDistortion_UsesContinuousQualityWithoutRuntimeKeyword()
        {
            string shader = ReadShader("Hecton_RetinaDistortion.shader");
            string feature = ReadProjectFile("Assets", "_Project", "Scripts", "Visor", "HectonRetinaDistortionFeature.cs");

            Assert.That(shader, Does.Contain("_HectonRetinaQualityWeight"));
            Assert.That(shader, Does.Contain("HectonRetinaSmoothRange01(0.45, 0.95, retinaQuality)"));
            Assert.That(feature, Does.Contain("ResolveRetinaVisualQualityWeight"));
            Assert.That(feature, Does.Not.Contain("EnableKeyword"));
            Assert.That(feature, Does.Not.Contain("DisableKeyword"));
            Assert.That(feature, Does.Not.Contain("Mx350Keyword"));
        }

        [Test]
        public void GpuScatterLodManager_UsesContinuousMaterialScalabilityWithoutQualityKeywords()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Rendering", "Scatter", "GpuScatterLodManager.cs");

            Assert.That(source, Does.Not.Contain("_QUALITY_MX350"));
            Assert.That(source, Does.Not.Contain("_QUALITY_HIGH"));
            Assert.That(source, Does.Not.Contain("IsHighQuality"));
            Assert.That(source, Does.Not.Contain("IsKeywordEnabled(Quality"));
            Assert.That(source, Does.Contain("float quality = Smooth01(_cachedQualityWeight01)"));
            Assert.That(source, Does.Contain("math.lerp(SanitizeNonNegativeFinite(lowTierAnisotropicSssStrength)"));
        }

        [Test]
        public void EncounterDirector_CandidateBudgetUsesContinuousQuality()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "EncounterDirector.cs");

            Assert.That(source, Does.Contain("ResolveCandidateCount(_candidateHardwareWeight01)"));
            Assert.That(source, Does.Contain("HomeostasisBrain.GlobalQualityWeight"));
            Assert.That(source, Does.Contain("PlatformAdaptiveBudgetGovernor.RecommendedQualityWeight"));
            Assert.That(source, Does.Contain("math.lerp(BaseCandidateCount, HighCandidateCount, combinedWeight01)"));
            Assert.That(source, Does.Not.Contain("bool highTier"));
            Assert.That(source, Does.Not.Contain("return highTier ?"));
        }

        [Test]
        public void TetherVisuals_BlendDearLieLineContinuously()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "TetherInstance.cs");

            Assert.That(source, Does.Contain("ResolveTautLineVisualCurveWeight"));
            Assert.That(source, Does.Contain("math.lerp(straightPoint, _verletPositions[i], curveWeight01)"));
            Assert.That(source, Does.Not.Contain("ShouldUseLowTierTautLineVisualFake"));
            Assert.That(source, Does.Not.Contain("return collapseWeight > 0f"));
            Assert.That(source, Does.Not.Contain("switch (TetherManager.SanitizeQualityTier"));
        }

        [Test]
        public void AudioVirtualization_ExposesContinuousQualityWeightContract()
        {
            string contracts = ReadProjectFile("Assets", "_Project", "Scripts", "Audio", "Virtualization", "Contracts", "AudioVirtualizationContracts.cs");
            string manager = ReadProjectFile("Assets", "_Project", "Scripts", "SpatialAudioManager.cs");

            Assert.That(contracts, Does.Contain("SetVirtualizationQualityWeight(float qualityWeight01)"));
            Assert.That(manager, Does.Contain("SetVirtualizationQualityWeight(float qualityWeight01)"));
            Assert.That(contracts, Does.Not.Contain("SetLowTierVirtualization"));
            Assert.That(manager, Does.Not.Contain("lowTier ? 0f : 1f"));
        }

        [Test]
        public void FaunaAndGeologyTierNames_DoNotEncodeHardwareQualityForks()
        {
            string cognition = ReadProjectFile("Assets", "_Project", "Scripts", "Fauna", "PredatorCognitionDomain.cs");
            string compatibility = ReadProjectFile("Assets", "_Project", "Scripts", "Fauna", "FaunaBrain.Compatibility.cs");
            string geology = ReadProjectFile("Assets", "_Project", "Scripts", "WorldGenerativeGeologyTerrainSeamApplier.cs");

            Assert.That(cognition, Does.Contain("ApexSmoothSteering"));
            Assert.That(compatibility, Does.Contain("ApexSmoothSteering"));
            Assert.That(cognition, Does.Not.Contain("HighTierSmoothSteering"));
            Assert.That(compatibility, Does.Not.Contain("HighTierSmoothSteering"));
            Assert.That(geology, Does.Contain("maskDetailActive"));
            Assert.That(geology, Does.Not.Contain("highTierMaskDetail"));
        }

        [Test]
        public void CoreMathLod_UsesContinuousShaderWeightWithoutKeywordToggles()
        {
            string distanceMath = ReadProjectFile("Assets", "_Project", "Scripts", "Core", "DistanceMath.cs");
            string registry = ReadProjectFile("Assets", "_Project", "Scripts", "Core", "GlobalRegistry.cs");
            string coreLit = ReadProjectFile("Assets", "_Project", "Art", "Shaders", "Hecton_CoreLit.hlsl");

            Assert.That(distanceMath, Does.Not.Contain("EnableKeyword"));
            Assert.That(distanceMath, Does.Not.Contain("DisableKeyword"));
            Assert.That(registry, Does.Not.Contain("MathLodLowKeyword"));
            Assert.That(registry, Does.Not.Contain("MathLodHighKeyword"));
            Assert.That(coreLit, Does.Not.Contain("_MATH_LOD_LOW"));
            Assert.That(coreLit, Does.Not.Contain("_MATH_LOD_HIGH"));
            Assert.That(coreLit, Does.Contain("_HectonMathLodWeight"));
            Assert.That(coreLit, Does.Contain("HectonCoreLitMathLodWeight"));
            Assert.That(distanceMath, Does.Contain("Shader.SetGlobalFloat(_mathLodWeightPropertyId, quality)"));
        }

        [Test]
        public void ContentTieredGroupPolicy_UsesContinuousRuntimeBudgetInsteadOfVramFork()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Core", "Content", "ContentRuntimeServices.cs");

            Assert.That(source, Does.Contain("ResolveRuntimeVisualBudgetWeight01"));
            Assert.That(source, Does.Contain("ResolveHardwareVisualCapacity01"));
            Assert.That(source, Does.Contain("HomeostasisBrain.GlobalQualityWeight"));
            Assert.That(source, Does.Contain("SmoothRange01(0.28f, 1f, weight)"));
            Assert.That(source, Does.Not.Contain("SystemInfo.graphicsMemorySize <= 2048)\r\n                return ResolveLowVisualBudget"));
            Assert.That(source, Does.Not.Contain("SystemInfo.graphicsMemorySize > 4096 ? ContentTier.Overkill : ContentTier.HighRes"));
        }

        [Test]
        public void ProjectVisualShaders_RemoveBinaryQualityAndMathLodKeywords()
        {
            string shaderRoot = ResolveProjectPath("Assets", "_Project", "Art", "Shaders");
            string[] files = Directory.GetFiles(shaderRoot, "*.*", SearchOption.AllDirectories);

            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                string extension = Path.GetExtension(file);
                if (extension != ".shader" && extension != ".hlsl")
                    continue;

                string source = File.ReadAllText(file);
                Assert.That(source, Does.Not.Contain("_QUALITY_MX350"), file);
                Assert.That(source, Does.Not.Contain("_QUALITY_HIGH"), file);
                Assert.That(source, Does.Not.Contain("_MATH_LOD_LOW"), file);
                Assert.That(source, Does.Not.Contain("_MATH_LOD_HIGH"), file);
            }
        }

        [Test]
        public void KelpContinuousScalarSweep_IsMonotonicFiniteAndPopBounded()
        {
            float previousSway = -1f;
            float previousParallax = -1f;

            for (int i = 0; i <= 16; i++)
            {
                float quality = i * (1f / 16f);
                float motion = Lerp(0.42f, 1f, SmoothRange01(0.05f, 0.85f, quality));
                float survivalScale = Lerp(0.58f, 1f, SmoothRange01(0f, 0.85f, quality));
                float sway = motion * survivalScale;
                float parallax = SmoothRange01(0.55f, 0.95f, quality);

                Assert.IsFalse(float.IsNaN(sway));
                Assert.IsFalse(float.IsInfinity(sway));
                Assert.IsFalse(float.IsNaN(parallax));
                Assert.IsFalse(float.IsInfinity(parallax));
                Assert.That(sway, Is.GreaterThanOrEqualTo(previousSway));
                Assert.That(parallax, Is.GreaterThanOrEqualTo(previousParallax));

                if (i > 0)
                    Assert.That(sway - previousSway, Is.LessThan(0.16f));

                previousSway = sway;
                previousParallax = parallax;
            }
        }

        private static string ReadShader(string shaderFile)
        {
            return ReadProjectFile("Assets", "_Project", "Art", "Shaders", shaderFile);
        }

        private static string ReadProjectFile(params string[] pathParts)
        {
            string filePath = ResolveProjectPath(pathParts);
            return File.ReadAllText(filePath);
        }

        private static string ResolveProjectPath(params string[] pathParts)
        {
            string filePath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            for (int i = 0; i < pathParts.Length; i++)
                filePath = Path.Combine(filePath, pathParts[i]);

            return filePath;
        }

        private static float SmoothRange01(float start, float end, float value)
        {
            float denom = Mathf.Max(end - start, 0.0001f);
            return Mathf.Clamp01((value - start) / denom);
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * Mathf.Clamp01(t);
        }
    }
}

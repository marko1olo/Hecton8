#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Hecton8.Gameplay;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Hecton8.EditorTests
{
    public sealed class VRSomaticComfortEvaluatorEditTests
    {
        [Test]
        public void SomaticComfortDtos_AreExplicitArm64Layouts()
        {
            Assert.AreEqual(32, UnsafeUtility.SizeOf<SomaticComfortStateDTO>());
            Assert.AreEqual(0, OffsetOf<SomaticComfortStateDTO>(nameof(SomaticComfortStateDTO.FovTunnelingIntensity)));
            Assert.AreEqual(4, OffsetOf<SomaticComfortStateDTO>(nameof(SomaticComfortStateDTO.HorizonLockBlend)));
            Assert.AreEqual(8, OffsetOf<SomaticComfortStateDTO>(nameof(SomaticComfortStateDTO.FoveatedScaleMultiplier)));
            Assert.AreEqual(12, OffsetOf<SomaticComfortStateDTO>(nameof(SomaticComfortStateDTO.ActiveComfortFlags)));
            Assert.AreEqual(16, OffsetOf<SomaticComfortStateDTO>(nameof(SomaticComfortStateDTO.ReservedParameters)));

            Assert.AreEqual(64, UnsafeUtility.SizeOf<ComfortTelemetryEntry>());
            Assert.AreEqual(44, OffsetOf<ComfortTelemetryEntry>(nameof(ComfortTelemetryEntry.Pressure01)));
            Assert.AreEqual(48, OffsetOf<ComfortTelemetryEntry>(nameof(ComfortTelemetryEntry.LockContentionCount)));
            Assert.AreEqual(52, OffsetOf<ComfortTelemetryEntry>(nameof(ComfortTelemetryEntry.StateHash)));
            Assert.AreEqual(56, OffsetOf<ComfortTelemetryEntry>(nameof(ComfortTelemetryEntry.Sequence)));
            Assert.AreEqual(60, OffsetOf<ComfortTelemetryEntry>(nameof(ComfortTelemetryEntry.AupHash)));
        }

        [Test]
        public void SomaticComfortFuzzer_ExtremeAngularVelocity_StaysFiniteAndSmoothed()
        {
            Assert.IsTrue(VRSomaticProvider.RunSomaticComfortFuzzerForTests(
                out float peakIntensity01,
                out float finalIntensity01,
                out uint finalFlags));
            Assert.Greater(peakIntensity01, 0f);
            Assert.GreaterOrEqual(finalIntensity01, 0f);
            Assert.LessOrEqual(finalIntensity01, 1f);
            Assert.AreNotEqual(0u, finalFlags);
        }

        [Test]
        public void BrownoutShader_TunnelingMask_UsesDitherAndNoExpensiveGradientCalls()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string shaderPath = Path.Combine(projectRoot, "Assets/_Project/Art/Shaders/Hidden_Hecton_VRBrownout.shader");
            string shader = File.ReadAllText(shaderPath);
            const string BlockStart = "if (vrComfortTunnel > 0.0001 || vrComfortBlackout > 0.0001)";
            int start = shader.IndexOf(BlockStart, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0);
            int end = shader.IndexOf("return color;", start, StringComparison.Ordinal);
            Assert.Greater(end, start);
            string block = shader.Substring(start, end - start);

            Assert.IsFalse(ContainsOrdinal(block, "smoothstep"));
            Assert.IsFalse(ContainsOrdinal(block, "length("));
            Assert.IsFalse(ContainsOrdinal(block, "pow("));
            Assert.IsFalse(ContainsOrdinal(block, "sin("));
            Assert.IsFalse(ContainsOrdinal(block, "cos("));
            Assert.IsTrue(ContainsOrdinal(block, "frac("));
            Assert.IsTrue(ContainsOrdinal(block, "step("));
            Assert.IsTrue(ContainsOrdinal(shader, "FoveatedRemapLinearToNonUniform"));
            Assert.IsTrue(ContainsOrdinal(shader, "ResolveFoveatedSourceUV(sampleUv)"));
            Assert.IsTrue(ContainsOrdinal(shader, "ResolveFoveatedSourceUV(saturate(sampleUv + blurStep))"));
            Assert.IsFalse(ContainsOrdinal(shader, "sampler_LinearClamp, uv)"));
            Assert.IsFalse(ContainsOrdinal(shader, "saturate(uv +"));
        }

        [Test]
        public void VisorGlitchAcesShader_UsesFoveatedTriangleFake()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string shaderPath = Path.Combine(projectRoot, "Assets/_Project/Art/Shaders/Hecton_VisorGlitchACES.shader");
            string shader = File.ReadAllText(shaderPath);

            Assert.IsTrue(ContainsOrdinal(shader, "#pragma target 3.5"));
            Assert.IsFalse(ContainsOrdinal(shader, "#pragma target 4.5"));
            Assert.IsTrue(ContainsOrdinal(shader, "FoveatedRemapLinearToNonUniform"));
            Assert.IsTrue(ContainsOrdinal(shader, "ResolveFoveatedSourceUV(sampleUv)"));
            Assert.IsTrue(ContainsOrdinal(shader, "TriangleWave"));
            Assert.IsTrue(ContainsOrdinal(shader, "ResolveLinearRamp01"));
            Assert.IsFalse(ContainsOrdinal(shader, "smoothstep"));
            Assert.IsFalse(ContainsOrdinal(shader, "length("));
            Assert.IsFalse(ContainsOrdinal(shader, "pow("));
            Assert.IsFalse(ContainsOrdinal(shader, "sin("));
            Assert.IsFalse(ContainsOrdinal(shader, "cos("));
        }

        [Test]
        public void VisorUberShader_ComfortMask_UsesLinearDitheredRoute()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string shaderPath = Path.Combine(projectRoot, "Assets/_Project/Art/Shaders/HectonVisorUberPost.shader");
            string shader = File.ReadAllText(shaderPath);

            Assert.IsTrue(ContainsOrdinal(shader, "ResolveLinearRamp01"));
            Assert.IsTrue(ContainsOrdinal(shader, "ResolveLinearComfortRamp"));
            Assert.IsTrue(ContainsOrdinal(shader, "CheapSignedTriangle"));
            Assert.IsTrue(ContainsOrdinal(shader, "ditheredComfortDrive"));
            Assert.IsTrue(ContainsOrdinal(shader, "if (textureMaskWeight > 0.001)"));
            Assert.IsFalse(ContainsOrdinal(shader, "smoothstep"));
            Assert.IsFalse(ContainsOrdinal(shader, "sin("));
            Assert.IsFalse(ContainsOrdinal(shader, "cos("));
            Assert.IsTrue(ContainsOrdinal(shader, "ResolveFoveatedSourceUV"));
            Assert.IsTrue(ContainsOrdinal(shader, "FoveatedRemapLinearToNonUniform"));
            Assert.IsTrue(ContainsOrdinal(shader, "SampleSceneDepth(ResolveFoveatedSourceUV(sampleUv))"));
            Assert.IsTrue(ContainsOrdinal(shader, "SampleSceneDepth(ResolveFoveatedSourceUV(safeUv))"));
            Assert.IsFalse(ContainsOrdinal(shader, "SampleSceneDepth(sampleUv)"));
            Assert.IsFalse(ContainsOrdinal(shader, "SampleSceneDepth(safeUv)"));
        }

        [Test]
        public void BilateralUpsample_UsesFoveatedCameraColorAndDepth()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string shader = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Art/Shaders/Hecton_BilateralUpsample.shader"));

            Assert.IsTrue(ContainsOrdinal(shader, "FoveatedRemapLinearToNonUniform"));
            Assert.IsTrue(ContainsOrdinal(shader, "ResolveFoveatedSourceUV"));
            Assert.IsTrue(ContainsOrdinal(shader, "SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, ResolveFoveatedSourceUV(uv))"));
            Assert.IsTrue(ContainsOrdinal(shader, "SampleSceneDepth(ResolveFoveatedSourceUV(uv))"));
            Assert.IsTrue(ContainsOrdinal(shader, "float2 currentSourceUv = ResolveFoveatedSourceUV(uv)"));
            Assert.IsTrue(ContainsOrdinal(shader, "float2 motion = -SAMPLE_TEXTURE2D_X(_MotionVectorTexture, sampler_LinearClamp, currentSourceUv).rg"));
            Assert.IsTrue(ContainsOrdinal(shader, "float2 historyUv = saturate(currentSourceUv + motion * motionScale)"));
            Assert.IsFalse(ContainsOrdinal(shader, "SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(uv))"));
            Assert.IsFalse(ContainsOrdinal(shader, "SampleSceneDepth(saturate(uv))"));
            Assert.IsFalse(ContainsOrdinal(shader, "SAMPLE_TEXTURE2D_X(_MotionVectorTexture, sampler_LinearClamp, saturate(uv))"));
        }

        [Test]
        public void BrownoutFeature_BindsCbufferInsideRenderGraphCommandBuffer()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string featurePath = Path.Combine(projectRoot, "Assets/_Project/Scripts/Visor/HectonVRBrownoutFeature.cs");
            string feature = File.ReadAllText(featurePath);

            Assert.IsTrue(ContainsOrdinal(feature, "AddRasterRenderPass<BrownoutPassData>"));
            Assert.IsTrue(ContainsOrdinal(feature, "context.cmd.SetGlobalConstantBuffer"));
            Assert.IsFalse(ContainsOrdinal(feature, "AddBlitPass"));
            Assert.IsFalse(ContainsOrdinal(feature, "Shader.SetGlobalConstantBuffer"));
        }

        [Test]
        public void VisorSecondaryFullscreenShaders_AreFoveatedAndMobileTargeted()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string[] shaderPaths =
            {
                "Assets/_Project/Art/Shaders/Hecton_RetinaDistortion.shader",
                "Assets/_Project/Art/Shaders/Hecton_NoirDepthFog.shader",
                "Assets/_Project/Art/Shaders/Hecton_HalfResParticleComposite.shader",
            };

            for (int i = 0; i < shaderPaths.Length; i++)
            {
                string shader = File.ReadAllText(Path.Combine(projectRoot, shaderPaths[i]));
                Assert.IsTrue(ContainsOrdinal(shader, "#pragma target 3.5"), shaderPaths[i]);
                Assert.IsFalse(ContainsOrdinal(shader, "#pragma target 4.5"), shaderPaths[i]);
                Assert.IsTrue(ContainsOrdinal(shader, "FoveatedRemapLinearToNonUniform"), shaderPaths[i]);
                Assert.IsFalse(ContainsOrdinal(shader, "smoothstep"), shaderPaths[i]);
                Assert.IsFalse(ContainsOrdinal(shader, "length("), shaderPaths[i]);
                Assert.IsFalse(ContainsOrdinal(shader, "pow("), shaderPaths[i]);
                Assert.IsFalse(ContainsOrdinal(shader, "sin("), shaderPaths[i]);
                Assert.IsFalse(ContainsOrdinal(shader, "cos("), shaderPaths[i]);
            }
        }

        [Test]
        public void VisorSecondaryFullscreenFeatures_BindCbuffersInsideRenderGraphCommandBuffer()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string[] featurePaths =
            {
                "Assets/_Project/Scripts/Visor/HectonRetinaDistortionFeature.cs",
                "Assets/_Project/Scripts/Visor/HectonNoirDepthFogFeature.cs",
                "Assets/_Project/Scripts/Visor/HectonHalfResParticlesFeature.cs",
            };

            for (int i = 0; i < featurePaths.Length; i++)
            {
                string feature = File.ReadAllText(Path.Combine(projectRoot, featurePaths[i]));
                Assert.IsTrue(ContainsOrdinal(feature, "AddRasterRenderPass<"), featurePaths[i]);
                Assert.IsTrue(ContainsOrdinal(feature, "context.cmd.SetGlobalConstantBuffer"), featurePaths[i]);
                Assert.IsFalse(ContainsOrdinal(feature, "AddBlitPass"), featurePaths[i]);
                Assert.IsFalse(ContainsOrdinal(feature, "Shader.SetGlobalConstantBuffer"), featurePaths[i]);
                Assert.IsFalse(ContainsOrdinal(feature, "B10G11R11_UFloatPack32"), featurePaths[i]);
            }
        }

        [Test]
        public void AbyssalSsdo_UsesGraphOwnedTexturesAndFoveatedDepth()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string feature = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Scripts/Visor/HectonAbyssalSsdoFeature.cs"));
            string shader = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Art/Shaders/Hecton_AbyssalSSDO.shader"));

            Assert.IsTrue(ContainsOrdinal(feature, "AddRasterRenderPass<SsdoFullscreenPassData>"));
            Assert.IsTrue(ContainsOrdinal(feature, "builder.UseTexture(ssdo, AccessFlags.Read)"));
            Assert.IsTrue(ContainsOrdinal(feature, "context.cmd.SetGlobalTexture(ShaderConstants.SsdoTextureId, data.Ssdo)"));
            Assert.IsFalse(ContainsOrdinal(feature, "AddBlitPass"));
            Assert.IsFalse(ContainsOrdinal(feature, "SetGlobalTextureAfterPass"));
            Assert.IsFalse(ContainsOrdinal(feature, "B10G11R11_UFloatPack32"));

            Assert.IsTrue(ContainsOrdinal(shader, "#pragma target 3.5"));
            Assert.IsFalse(ContainsOrdinal(shader, "#pragma target 4.5"));
            Assert.IsTrue(ContainsOrdinal(shader, "FoveatedRemapLinearToNonUniform"));
            Assert.IsTrue(ContainsOrdinal(shader, "SampleSceneDepth(ResolveFoveatedSourceUV(screenUV))"));
            Assert.IsTrue(ContainsOrdinal(shader, "ResolveFoveatedSourceUV(screenUV))"));
            Assert.IsTrue(ContainsOrdinal(shader, "SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uvA)"));
            Assert.IsFalse(ContainsOrdinal(shader, "smoothstep"));
            Assert.IsFalse(ContainsOrdinal(shader, "length("));
            Assert.IsFalse(ContainsOrdinal(shader, "pow("));
            Assert.IsFalse(ContainsOrdinal(shader, "sin("));
            Assert.IsFalse(ContainsOrdinal(shader, "cos("));
        }

        [Test]
        public void StochasticSsr_UsesGraphOwnedMaskAndFoveatedSource()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string feature = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Scripts/Visor/HectonStochasticSsrFeature.cs"));
            string shader = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Art/Shaders/Hecton_StochasticSSR.shader"));

            Assert.IsTrue(ContainsOrdinal(feature, "AddRasterRenderPass<ReflectionSheenPassData>"));
            Assert.IsTrue(ContainsOrdinal(feature, "context.cmd.SetGlobalConstantBuffer"));
            Assert.IsTrue(ContainsOrdinal(feature, "context.cmd.SetGlobalTexture(ShaderConstants.MaskTextureId"));
            Assert.IsFalse(ContainsOrdinal(feature, "AddBlitPass"));
            Assert.IsFalse(ContainsOrdinal(feature, "SetGlobalTextureAfterPass"));
            Assert.IsFalse(ContainsOrdinal(feature, "Shader.SetGlobalConstantBuffer"));

            Assert.IsTrue(ContainsOrdinal(shader, "#pragma target 3.5"));
            Assert.IsFalse(ContainsOrdinal(shader, "#pragma target 4.5"));
            Assert.IsTrue(ContainsOrdinal(shader, "FoveatedRemapLinearToNonUniform"));
            Assert.IsTrue(ContainsOrdinal(shader, "SampleSceneDepth(ResolveCameraSourceUV(input.screenUV))"));
            Assert.IsTrue(ContainsOrdinal(shader, "ResolveCameraSourceUV(reflectionUV)"));
            Assert.IsFalse(ContainsOrdinal(shader, "smoothstep"));
            Assert.IsFalse(ContainsOrdinal(shader, "length("));
            Assert.IsFalse(ContainsOrdinal(shader, "pow("));
            Assert.IsFalse(ContainsOrdinal(shader, "sin("));
            Assert.IsFalse(ContainsOrdinal(shader, "cos("));
        }

        [Test]
        public void ScooterVolumetricShafts_UsesGraphOwnedTexturesAndFoveatedCameraInputs()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string feature = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Scripts/Visor/HectonScooterVolumetricShaftsFeature.cs"));
            string shader = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Art/Shaders/Hecton_ScooterVolumetricShafts.shader"));

            Assert.IsTrue(ContainsOrdinal(feature, "AddRasterRenderPass<ShaftFullscreenPassData>"));
            Assert.IsTrue(ContainsOrdinal(feature, "context.cmd.SetGlobalConstantBuffer"));
            Assert.IsTrue(ContainsOrdinal(feature, "context.cmd.SetGlobalBuffer(ShaderConstants.ExposureStateBufferId"));
            Assert.IsFalse(ContainsOrdinal(feature, "AddBlitPass"));
            Assert.IsFalse(ContainsOrdinal(feature, "SetGlobalTextureAfterPass"));
            Assert.IsFalse(ContainsOrdinal(feature, "Shader.SetGlobalConstantBuffer"));
            Assert.IsFalse(ContainsOrdinal(feature, "Shader.SetGlobalBuffer"));
            Assert.IsFalse(ContainsOrdinal(feature, "B10G11R11_UFloatPack32"));

            Assert.IsTrue(ContainsOrdinal(shader, "#pragma target 4.5"));
            Assert.IsTrue(ContainsOrdinal(shader, "StructuredBuffer<float4> _HectonNoirExposureState"));
            Assert.IsTrue(ContainsOrdinal(shader, "FoveatedRemapLinearToNonUniform"));
            Assert.IsTrue(ContainsOrdinal(shader, "SampleSceneDepth(ResolveFoveatedCameraUV(screenUV))"));
            Assert.IsTrue(ContainsOrdinal(shader, "ResolveFoveatedCameraUV(sourceUV)"));
            Assert.IsFalse(ContainsOrdinal(shader, "smoothstep"));
            Assert.IsFalse(ContainsOrdinal(shader, "length("));
            Assert.IsFalse(ContainsOrdinal(shader, "pow("));
            Assert.IsFalse(ContainsOrdinal(shader, "sin("));
            Assert.IsFalse(ContainsOrdinal(shader, "cos("));
        }

        [Test]
        public void VisorDiagnosticScannerAndSootShaders_AreFoveatedMobileFakes()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string[] shaderPaths =
            {
                "Assets/_Project/Art/Shaders/Hidden_Hecton_AtmosphereSootOverlay.shader",
                "Assets/_Project/Art/Shaders/Hidden_Hecton_BiosDiagnostic.shader",
                "Assets/_Project/Art/Shaders/Hidden_Hecton_ScannerDepthProjection.shader",
            };

            for (int i = 0; i < shaderPaths.Length; i++)
            {
                string shader = File.ReadAllText(Path.Combine(projectRoot, shaderPaths[i]));
                Assert.IsTrue(ContainsOrdinal(shader, "#pragma target 3.5"), shaderPaths[i]);
                Assert.IsFalse(ContainsOrdinal(shader, "#pragma target 4.5"), shaderPaths[i]);
                Assert.IsTrue(ContainsOrdinal(shader, "FoveatedRemapLinearToNonUniform"), shaderPaths[i]);
                Assert.IsFalse(ContainsOrdinal(shader, "smoothstep"), shaderPaths[i]);
                Assert.IsFalse(ContainsOrdinal(shader, "sin("), shaderPaths[i]);
                Assert.IsFalse(ContainsOrdinal(shader, "cos("), shaderPaths[i]);
            }
        }

        [Test]
        public void VisorScannerAndBiosFeatures_PreserveSourceFormatAndClampAup()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string biosFeature = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Scripts/Visor/HectonBiosDiagnosticFeature.cs"));
            string scannerFeature = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Scripts/Visor/HectonScannerProjectionFeature.cs"));
            string sootFeature = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Scripts/Visor/HectonAtmosphereSootFeature.cs"));

            Assert.IsFalse(ContainsOrdinal(biosFeature, "B10G11R11_UFloatPack32"));
            Assert.IsFalse(ContainsOrdinal(biosFeature, "AddBlitPass"));
            Assert.IsTrue(ContainsOrdinal(biosFeature, "AddRasterRenderPass<DiagnosticPassData>"));
            Assert.IsFalse(ContainsOrdinal(scannerFeature, "B10G11R11_UFloatPack32"));
            Assert.IsFalse(ContainsOrdinal(scannerFeature, "AddBlitPass"));
            Assert.IsTrue(ContainsOrdinal(scannerFeature, "AddRasterRenderPass<ProjectionPassData>"));
            Assert.IsTrue(ContainsOrdinal(scannerFeature, "math.clamp(local, new double3(-1000000.0), new double3(1000000.0))"));
            Assert.IsTrue(ContainsOrdinal(sootFeature, "AddRasterRenderPass<SootPassData>"));
            Assert.IsTrue(ContainsOrdinal(sootFeature, "context.cmd.SetGlobalConstantBuffer"));
            Assert.IsFalse(ContainsOrdinal(sootFeature, "AddBlitPass"));
            Assert.IsFalse(ContainsOrdinal(sootFeature, "Shader.SetGlobalConstantBuffer"));
        }

        [Test]
        public void VisorFluidShader_UsesFoveatedLinearRampRoute()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string shader = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Art/Shaders/Hecton_VisorFluidDistortion.shader"));

            Assert.IsTrue(ContainsOrdinal(shader, "#pragma target 3.5"));
            Assert.IsTrue(ContainsOrdinal(shader, "ResolveLinearRamp01"));
            Assert.IsTrue(ContainsOrdinal(shader, "ResolveFoveatedSourceUV"));
            Assert.IsTrue(ContainsOrdinal(shader, "FoveatedRemapLinearToNonUniform"));
            Assert.IsFalse(ContainsOrdinal(shader, "smoothstep"));
            Assert.IsFalse(ContainsOrdinal(shader, "length("));
            Assert.IsFalse(ContainsOrdinal(shader, "sin("));
            Assert.IsFalse(ContainsOrdinal(shader, "cos("));
        }

        [Test]
        public void VisorFluidFeature_BindsResourcesInsideGraphAndFailsClosed()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string feature = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Scripts/Visor/HectonVisorFluidDistortionFeature.cs"));

            Assert.IsTrue(ContainsOrdinal(feature, "AddRasterRenderPass<PassData>"));
            Assert.IsTrue(ContainsOrdinal(feature, "builder.UseBuffer(globalsBuffer, AccessFlags.Read)"));
            Assert.IsTrue(ContainsOrdinal(feature, "context.cmd.SetGlobalConstantBuffer"));
            Assert.IsTrue(ContainsOrdinal(feature, "catch (NotSupportedException)"));
            Assert.IsFalse(ContainsOrdinal(feature, "catch (" + "Exception)"));
            Assert.IsFalse(ContainsOrdinal(feature, "Shader.SetGlobalConstantBuffer"));
            Assert.IsFalse(ContainsOrdinal(feature, "B10G11R11_UFloatPack32"));
        }

        [Test]
        public void DiegeticVisorLensCompute_UsesLinearRampMasks()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string compute = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Art/Shaders/Hecton_DiegeticVisorLens.compute"));

            Assert.IsTrue(ContainsOrdinal(compute, "ResolveLinearRamp01"));
            Assert.IsFalse(ContainsOrdinal(compute, "smoothstep"));
            Assert.IsFalse(ContainsOrdinal(compute, "length("));
            Assert.IsFalse(ContainsOrdinal(compute, "pow("));
            Assert.IsFalse(ContainsOrdinal(compute, "sin("));
            Assert.IsFalse(ContainsOrdinal(compute, "cos("));
        }

        [Test]
        public void SonarAndTraumaVisorShaders_AreFoveatedCheapFakes()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string sonar = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Art/Shaders/SonarGridOverlay.shader"));
            Assert.IsTrue(ContainsOrdinal(sonar, "#pragma target 3.5"));
            Assert.IsFalse(ContainsOrdinal(sonar, "#pragma target 4.5"));
            Assert.IsTrue(ContainsOrdinal(sonar, "FoveatedRemapLinearToNonUniform"));
            Assert.IsTrue(ContainsOrdinal(sonar, "ResolveFoveatedSourceUV(screenUV)"));
            Assert.IsTrue(ContainsOrdinal(sonar, "SampleSceneDepth(ResolveFoveatedSourceUV(screenUV))"));
            Assert.IsTrue(ContainsOrdinal(sonar, "SampleSceneDepth(ResolveFoveatedSourceUV(leftUv))"));
            Assert.IsFalse(ContainsOrdinal(sonar, "smoothstep"));
            Assert.IsFalse(ContainsOrdinal(sonar, "length("));
            Assert.IsFalse(ContainsOrdinal(sonar, "pow("));
            Assert.IsFalse(ContainsOrdinal(sonar, "sin("));
            Assert.IsFalse(ContainsOrdinal(sonar, "cos("));

            string pdaSonarMap = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Art/Shaders/Hecton_PDA_SonarMap.shader"));
            Assert.IsTrue(ContainsOrdinal(pdaSonarMap, "ResolveLinearRamp01"));
            Assert.IsFalse(ContainsOrdinal(pdaSonarMap, "smoothstep"));
            Assert.IsFalse(ContainsOrdinal(pdaSonarMap, "length("));
            Assert.IsFalse(ContainsOrdinal(pdaSonarMap, "pow("));
            Assert.IsFalse(ContainsOrdinal(pdaSonarMap, "sin("));
            Assert.IsFalse(ContainsOrdinal(pdaSonarMap, "cos("));

            string[] traumaShaderPaths =
            {
                "Assets/_Project/Art/Shaders/Hecton_VisorWounds.shader",
                "Assets/_Project/Art/Shaders/Hecton_VisorTrauma.shader",
                "Assets/_Project/Art/Shaders/Hecton_DeferredDecal.shader",
            };

            for (int i = 0; i < traumaShaderPaths.Length; i++)
            {
                string shader = File.ReadAllText(Path.Combine(projectRoot, traumaShaderPaths[i]));
                Assert.IsTrue(ContainsOrdinal(shader, "#pragma target 4.5"), traumaShaderPaths[i]);
                Assert.IsTrue(ContainsOrdinal(shader, "StructuredBuffer<TraumaDecalData>"), traumaShaderPaths[i]);
                Assert.IsTrue(ContainsOrdinal(shader, "FoveatedRemapLinearToNonUniform"), traumaShaderPaths[i]);
                Assert.IsTrue(ContainsOrdinal(shader, "CheapSignedTriangle"), traumaShaderPaths[i]);
                Assert.IsTrue(ContainsOrdinal(shader, "ApproximateMagnitude2D"), traumaShaderPaths[i]);
                Assert.IsFalse(ContainsOrdinal(shader, "smoothstep"), traumaShaderPaths[i]);
                Assert.IsFalse(ContainsOrdinal(shader, "length("), traumaShaderPaths[i]);
                Assert.IsFalse(ContainsOrdinal(shader, "pow("), traumaShaderPaths[i]);
                Assert.IsFalse(ContainsOrdinal(shader, "sin("), traumaShaderPaths[i]);
                Assert.IsFalse(ContainsOrdinal(shader, "cos("), traumaShaderPaths[i]);
            }
        }

        [Test]
        public void BiolumSsgi_UsesGraphOwnedCompositeAndFoveatedSource()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string feature = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Scripts/Visor/HectonBiolumSSGIFeature.cs"));
            string shader = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Art/Shaders/Hecton_BiolumSSGIComposite.shader"));
            string compute = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Art/Shaders/Hecton_BiolumSSGI.compute"));

            Assert.IsTrue(ContainsOrdinal(feature, "AddRasterRenderPass<CompositePassData>"));
            Assert.IsTrue(ContainsOrdinal(feature, "context.cmd.SetGlobalTexture(ShaderConstants.GiTextureId"));
            Assert.IsFalse(ContainsOrdinal(feature, "AddBlitPass"));
            Assert.IsFalse(ContainsOrdinal(feature, "SetGlobalTextureAfterPass"));
            Assert.IsFalse(ContainsOrdinal(feature, "RenderGraphModule.Util"));

            Assert.IsTrue(ContainsOrdinal(shader, "#pragma target 3.5"));
            Assert.IsFalse(ContainsOrdinal(shader, "#pragma target 4.5"));
            Assert.IsTrue(ContainsOrdinal(shader, "FoveatedRemapLinearToNonUniform"));
            Assert.IsTrue(ContainsOrdinal(shader, "ResolveFoveatedSourceUV(screenUV)"));
            Assert.IsTrue(ContainsOrdinal(shader, "SAMPLE_TEXTURE2D_X(_HectonBiolumSSGITexture, sampler_LinearClamp, screenUV)"));
            Assert.IsFalse(ContainsOrdinal(shader, "smoothstep"));
            Assert.IsFalse(ContainsOrdinal(shader, "length("));
            Assert.IsFalse(ContainsOrdinal(shader, "pow("));
            Assert.IsFalse(ContainsOrdinal(shader, "sin("));
            Assert.IsFalse(ContainsOrdinal(shader, "cos("));

            Assert.IsTrue(ContainsOrdinal(compute, "FoveatedRemapLinearToNonUniform"));
            Assert.IsTrue(ContainsOrdinal(compute, "ResolveFoveatedSourcePixel"));
            Assert.IsFalse(ContainsOrdinal(compute, "break;"));
            Assert.IsFalse(ContainsOrdinal(compute, "continue;"));
        }

        [Test]
        public void SonarPointCloudFeature_UsesGraphOwnedHistoryTextures()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string feature = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs"));

            Assert.IsTrue(ContainsOrdinal(feature, "AddRasterRenderPass<SonarFullscreenPassData>"));
            Assert.IsTrue(ContainsOrdinal(feature, "context.cmd.SetGlobalTexture(ShaderConstants.HistoryTextureId"));
            Assert.IsTrue(ContainsOrdinal(feature, "context.cmd.SetGlobalTexture(ShaderConstants.WorldPointCloudTextureId"));
            Assert.IsFalse(ContainsOrdinal(feature, "AddBlitPass"));
            Assert.IsFalse(ContainsOrdinal(feature, "SetGlobalTextureAfterPass"));
            Assert.IsFalse(ContainsOrdinal(feature, "RenderGraphModule.Util"));
        }

        [Test]
        public void VoxelSsaoFeature_DoesNotPublishUnusedGlobalTexture()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string feature = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Scripts/Visor/HectonVoxelSsaoFeature.cs"));

            Assert.IsTrue(ContainsOrdinal(feature, "AddComputePass(\"Hecton Voxel SSAO\""));
            Assert.IsTrue(ContainsOrdinal(feature, "private const bool HasRuntimeConsumer = false"));
            Assert.IsTrue(ContainsOrdinal(feature, "builder.UseTexture(aoTexture, AccessFlags.Write)"));
            Assert.IsFalse(ContainsOrdinal(feature, "SetGlobalTextureAfterPass"));
            Assert.IsFalse(ContainsOrdinal(feature, "_HectonVoxelSSAOTex"));
        }

        [Test]
        public void PresentationHudShaders_UseCheapLinearFakeMath()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string compass = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Art/Shaders/Hecton_UI_CompassRibbon.shader"));
            string wrist = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Art/Shaders/Hecton_WristHudSDF.shader"));
            string terminal = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Art/Shaders/Hecton_DiegeticTerminal.shader"));
            string[] shaders = { compass, wrist, terminal };
            string[] names = { "CompassRibbon", "WristHudSDF", "DiegeticTerminal" };

            for (int i = 0; i < shaders.Length; i++)
            {
                Assert.IsFalse(ContainsOrdinal(shaders[i], "smoothstep"), names[i]);
                Assert.IsFalse(ContainsOrdinal(shaders[i], "sin("), names[i]);
                Assert.IsFalse(ContainsOrdinal(shaders[i], "cos("), names[i]);
                Assert.IsFalse(ContainsOrdinal(shaders[i], "length("), names[i]);
                Assert.IsFalse(ContainsOrdinal(shaders[i], "distance("), names[i]);
            }

            Assert.IsTrue(ContainsOrdinal(compass, "ResolveLinearRamp01"));
            Assert.IsTrue(ContainsOrdinal(wrist, "ResolveLinearRamp01"));
            Assert.IsTrue(ContainsOrdinal(wrist, "DistanceSq2"));
            Assert.IsTrue(ContainsOrdinal(terminal, "H8LinearRamp01"));
            Assert.IsTrue(ContainsOrdinal(terminal, "H8TerminalTriangleSigned"));
        }

        [Test]
        public void DepthAwarePresentationShaders_UseFoveatedCameraDepth()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string[] shaderPaths =
            {
                "Assets/_Project/Art/Shaders/Hecton_DiegeticPanelUnlit.shader",
                "Assets/_Project/Shaders/UI/Hecton_DiegeticPanelDepthFade.shader",
                "Assets/_Project/Art/Shaders/Hecton_PdaScreen.shader",
                "Assets/_Project/Art/Shaders/Hecton_PDA_SonarPointCloud.shader",
                "Assets/_Project/Art/Shaders/Hecton_SonarPoint.shader",
            };

            for (int i = 0; i < shaderPaths.Length; i++)
            {
                string shader = File.ReadAllText(Path.Combine(projectRoot, shaderPaths[i]));
                Assert.IsTrue(ContainsOrdinal(shader, "FoveatedRemapLinearToNonUniform"), shaderPaths[i]);
                Assert.IsTrue(ContainsOrdinal(shader, "SampleSceneDepth(ResolveFoveatedSourceUV("), shaderPaths[i]);
                Assert.IsFalse(ContainsOrdinal(shader, "SampleSceneDepth(screenUV)"), shaderPaths[i]);
                Assert.IsFalse(ContainsOrdinal(shader, "SampleSceneDepth(screenUv)"), shaderPaths[i]);
                Assert.IsFalse(ContainsOrdinal(shader, "SampleSceneDepth(input.screenUV)"), shaderPaths[i]);
            }
        }

        [Test]
        public void PdaScreenProjection_UsesLinearFoveatedPresentationMath()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string shader = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Art/Shaders/Hecton_PdaScreen.shader"));

            Assert.IsTrue(ContainsOrdinal(shader, "ResolveLinearRamp01"));
            Assert.IsTrue(ContainsOrdinal(shader, "SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, ResolveFoveatedSourceUV(screenUV))"));
            Assert.IsTrue(ContainsOrdinal(shader, "SampleSceneDepth(ResolveFoveatedSourceUV(screenUV))"));
            Assert.IsFalse(ContainsOrdinal(shader, "smoothstep"));
            Assert.IsFalse(ContainsOrdinal(shader, "SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, screenUV)"));
        }

        [Test]
        public void SuitVisorDepthAndDiagnosticProjection_UseFoveatedDepthWithLinearWorldUv()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string suit = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Art/Shaders/SuitVisor.shader"));

            Assert.IsTrue(ContainsOrdinal(suit, "SampleSceneDepth(ResolveFoveatedOpaqueUV(safeScreenUV))"));
            Assert.IsTrue(ContainsOrdinal(suit, "SampleSceneDepth(ResolveFoveatedOpaqueUV(saturate(safeScreenUV + float2(texel.x, 0.0))))"));
            Assert.IsTrue(ContainsOrdinal(suit, "SampleSceneDepth(ResolveFoveatedOpaqueUV(saturate(safeScreenUV + float2(0.0, texel.y))))"));
            Assert.IsTrue(ContainsOrdinal(suit, "SampleSceneDepth(ResolveFoveatedOpaqueUV(screenUV))"));
            Assert.IsFalse(ContainsOrdinal(suit, "SampleSceneDepth(safeScreenUV)"));
            Assert.IsFalse(ContainsOrdinal(suit, "SampleSceneDepth(saturate(safeScreenUV"));
            Assert.IsFalse(ContainsOrdinal(suit, "SampleSceneDepth(screenUV)"));

            string bios = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Art/Shaders/Hidden_Hecton_BiosDiagnostic.shader"));
            string scanner = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Art/Shaders/Hidden_Hecton_ScannerDepthProjection.shader"));
            Assert.IsTrue(ContainsOrdinal(bios, "SampleSceneDepth(cameraTextureUv)"));
            Assert.IsTrue(ContainsOrdinal(scanner, "SampleSceneDepth(cameraTextureUv)"));
            Assert.IsTrue(ContainsOrdinal(bios, "ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP)"));
            Assert.IsTrue(ContainsOrdinal(scanner, "ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP)"));
            Assert.IsFalse(ContainsOrdinal(bios, "ComputeWorldSpacePosition(cameraTextureUv, depth, UNITY_MATRIX_I_VP)"));
            Assert.IsFalse(ContainsOrdinal(scanner, "ComputeWorldSpacePosition(cameraTextureUv, depth, UNITY_MATRIX_I_VP)"));
        }

        [Test]
        public void DryVolumeRestore_UsesFoveatedCameraSourceAndDepth()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string shader = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Art/Shaders/Hecton_DryVolumeRestore.shader"));

            Assert.IsTrue(ContainsOrdinal(shader, "FoveatedRemapLinearToNonUniform"));
            Assert.IsTrue(ContainsOrdinal(shader, "UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input)"));
            Assert.IsTrue(ContainsOrdinal(shader, "float2 screenUV = UnityStereoTransformScreenSpaceTex(input.screenUV)"));
            Assert.IsTrue(ContainsOrdinal(shader, "rawDepth = SampleSceneDepth(ResolveFoveatedSourceUV(screenUV))"));
            Assert.IsTrue(ContainsOrdinal(shader, "SAMPLE_TEXTURE2D_X(_OceanCameraColorTexture, sampler_LinearClamp, sourceUV)"));
            Assert.IsTrue(ContainsOrdinal(shader, "SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, sourceUV)"));
            Assert.IsFalse(ContainsOrdinal(shader, "rawDepth = SampleSceneDepth(screenUV)"));
            Assert.IsFalse(ContainsOrdinal(shader, "SAMPLE_TEXTURE2D_X(_OceanCameraColorTexture, sampler_LinearClamp, input.screenUV)"));
            Assert.IsFalse(ContainsOrdinal(shader, "SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.screenUV)"));
        }

        [Test]
        public void FlashlightConeSilt_UsesFoveatedDepthAndLinearBeamFades()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string shader = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Art/Shaders/Hecton_FlashlightConeSilt.shader"));

            Assert.IsTrue(ContainsOrdinal(shader, "#pragma target 3.5"));
            Assert.IsTrue(ContainsOrdinal(shader, "FoveatedRemapLinearToNonUniform"));
            Assert.IsTrue(ContainsOrdinal(shader, "UnityStereoTransformScreenSpaceTex(input.screenPos.xy"));
            Assert.IsTrue(ContainsOrdinal(shader, "SampleSceneDepth(ResolveFoveatedSourceUV(screenUV))"));
            Assert.IsTrue(ContainsOrdinal(shader, "HectonLinearRamp01"));
            Assert.IsFalse(ContainsOrdinal(shader, "SampleSceneDepth(screenUV)"));
            Assert.IsFalse(ContainsOrdinal(shader, "smoothstep"));
        }

        [Test]
        public void AtmosphericCutoutVfx_UsesStereoFoveatedDepth()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string shader = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Art/Shaders/AbyssalBlackSmoke.shader"));

            Assert.IsTrue(ContainsOrdinal(shader, "FoveatedRemapLinearToNonUniform"));
            Assert.IsTrue(ContainsOrdinal(shader, "screenUV = UnityStereoTransformScreenSpaceTex(screenUV)"));
            Assert.IsTrue(ContainsOrdinal(shader, "SampleSceneDepth(ResolveFoveatedSourceUV(screenUV))"));
            Assert.IsFalse(ContainsOrdinal(shader, "SampleSceneDepth(screenUV)"));
        }

        [Test]
        public void FluidAndLeakCutoutVfx_UseStereoFoveatedDepthAndLinearMasks()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string fluid = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Art/Shaders/AbyssalFluidDecal.shader"));
            string leak = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Art/Shaders/Hecton_LeakPlume.shader"));
            string coreLit = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl"));

            Assert.IsTrue(ContainsOrdinal(fluid, "FoveatedRemapLinearToNonUniform"));
            Assert.IsTrue(ContainsOrdinal(fluid, "screenUV = UnityStereoTransformScreenSpaceTex(screenUV)"));
            Assert.IsTrue(ContainsOrdinal(fluid, "SampleSceneDepth(ResolveFoveatedSourceUV(screenUV))"));
            Assert.IsTrue(ContainsOrdinal(fluid, "ResolveLinearRamp01"));
            Assert.IsFalse(ContainsOrdinal(fluid, "SampleSceneDepth(screenUV)"));
            Assert.IsFalse(ContainsOrdinal(fluid, "smoothstep"));

            Assert.IsTrue(ContainsOrdinal(leak, "FoveatedRemapLinearToNonUniform"));
            Assert.IsTrue(ContainsOrdinal(leak, "screenUV = UnityStereoTransformScreenSpaceTex(screenUV)"));
            Assert.IsTrue(ContainsOrdinal(leak, "SampleSceneDepth(ResolveFoveatedSourceUV(screenUV))"));
            Assert.IsTrue(ContainsOrdinal(leak, "float rawFragmentDepth = saturate(positionCS.z)"));
            Assert.IsFalse(ContainsOrdinal(leak, "SampleSceneDepth(screenUV)"));
            Assert.IsFalse(ContainsOrdinal(leak, "positionCS.xy * rcp(positionCS.w)"));

            Assert.IsTrue(ContainsOrdinal(coreLit, "FoveatedRemapLinearToNonUniform"));
            Assert.IsTrue(ContainsOrdinal(coreLit, "HectonCoreLitResolveFoveatedSourceUV"));
            Assert.IsTrue(ContainsOrdinal(coreLit, "SampleSceneDepth(HectonCoreLitResolveFoveatedSourceUV(screenUV))"));
            Assert.IsTrue(ContainsOrdinal(coreLit, "float rawFragmentDepth = saturate(positionCS.z)"));
            Assert.IsFalse(ContainsOrdinal(coreLit, "SampleSceneDepth(screenUV)"));
        }

        [Test]
        public void HudScannerAndHologramShaders_UseCheapPresentationFakes()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string[] shaderPaths =
            {
                "Assets/_Project/Art/Shaders/Hecton_HUD_AcousticRadarOverlay.shader",
                "Assets/_Project/Art/Shaders/Hecton_HologramMap.shader",
                "Assets/_Project/Art/Shaders/Hecton_RadarBlipInstanced.shader",
                "Assets/_Project/Art/Shaders/Hecton_ScannerPulseInstanced.shader",
                "Assets/_Project/Art/Shaders/Hecton_ScannerMarkerInstanced.shader",
                "Assets/_Project/Art/Shaders/Hecton_HUD_DiegeticProjectionUnlit.shader",
                "Assets/_Project/Art/Shaders/Hecton_ToolScreenDiegetic.shader",
                "Assets/_Project/Art/Shaders/Hecton_HologramAssembly.shader",
            };

            for (int i = 0; i < shaderPaths.Length; i++)
            {
                string shader = File.ReadAllText(Path.Combine(projectRoot, shaderPaths[i]));
                Assert.IsFalse(ContainsOrdinal(shader, "smoothstep"), shaderPaths[i]);
                Assert.IsFalse(ContainsOrdinal(shader, "sin("), shaderPaths[i]);
                Assert.IsFalse(ContainsOrdinal(shader, "cos("), shaderPaths[i]);
                Assert.IsFalse(ContainsOrdinal(shader, "length("), shaderPaths[i]);
                Assert.IsFalse(ContainsOrdinal(shader, "distance("), shaderPaths[i]);
                Assert.IsFalse(ContainsOrdinal(shader, "pow("), shaderPaths[i]);
            }
        }

        [Test]
        public void PdaSonarAndUberNoirPresentationShaders_StayCheapForMobileVr()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string pdaWave = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Art/Shaders/Hecton_PDA_FrequencyTuningWave.shader"));
            string sonarStencil = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Art/Shaders/Hecton_SubmarineSonarHoloMapStencil.shader"));
            string uberNoir = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Art/Shaders/Hecton8_UberNoir.hlsl"));

            Assert.IsTrue(ContainsOrdinal(pdaWave, "#pragma target 3.5"));
            Assert.IsFalse(ContainsOrdinal(pdaWave, "round("));
            Assert.IsTrue(ContainsOrdinal(sonarStencil, "ApproxRadialLength"));
            Assert.IsFalse(ContainsOrdinal(sonarStencil, "length("));
            Assert.IsTrue(ContainsOrdinal(uberNoir, "float H8UberNoirHash13"));
            Assert.IsFalse(ContainsOrdinal(uberNoir, "frac(sin(dot(value"));
            Assert.IsFalse(ContainsOrdinal(uberNoir, "length(frac(stablePosition.xz"));
            Assert.IsFalse(ContainsOrdinal(uberNoir, "richCurl = sin("));
            Assert.IsFalse(ContainsOrdinal(uberNoir, "pow("));
        }

        [Test]
        public void PdaH8lrLoreStore_UsesChunkedVaultMirrorAndFenceCheckedReads()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string store = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Scripts/UI/PdaH8lrLoreStore.cs"));

            Assert.IsTrue(ContainsOrdinal(store, "VaultMirrorCopyChunkBytes"));
            Assert.IsTrue(ContainsOrdinal(store, "TryCopyVaultMirrorChunk"));
            Assert.IsTrue(ContainsOrdinal(store, "TryCommitVaultMirror"));
            Assert.IsTrue(ContainsOrdinal(store, "vault.IsCompactionFenceActive"));
            Assert.IsTrue(ContainsOrdinal(store, "vault.TryReadOnlyHandle"));
            Assert.IsFalse(ContainsOrdinal(store, "vault.TryReadHandle"));
            Assert.IsTrue(ContainsOrdinal(store, "vault.ReleaseWriteLock"));
            Assert.IsFalse(ContainsOrdinal(store, "catch (" + "Exception"));
            Assert.IsFalse(ContainsOrdinal(store, "stream.Read(" + "mirrorSpan.Slice"));
            Assert.IsFalse(ContainsOrdinal(store, "TryResolveHandle(in _vaultMirrorHandle"));
        }

        [Test]
        public void DiegeticTerminalShader_AvoidsRoundForPresentationIndexes()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string shader = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Art/Shaders/Hecton_DiegeticTerminal.shader"));

            Assert.IsTrue(ContainsOrdinal(shader, "(uint)(saturate(_TerminalSlice * (1.0 / 63.0)) * 63.0 + 0.5)"));
            Assert.IsTrue(ContainsOrdinal(shader, "(uint)(saturate(slice * (1.0 / 63.0)) * 63.0 + 0.5)"));
            Assert.IsFalse(ContainsOrdinal(shader, "round("));
        }

        [Test]
        public void TerminalBlackBoxDumps_UseSnapshotRowsAndSpecificCatches()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string runtime = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs"));
            string projection = File.ReadAllText(Path.Combine(projectRoot, "Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime_TerminalProjection.cs"));

            Assert.IsTrue(ContainsOrdinal(runtime, "TryReadTerminalTelemetryDumpShape"));
            Assert.IsTrue(ContainsOrdinal(runtime, "TryReadTerminalTelemetryDumpEntry"));
            Assert.IsTrue(ContainsOrdinal(runtime, "!_vault.IsCompactionFenceActive"));
            Assert.IsTrue(ContainsOrdinal(projection, "TryReadTerminalInputTelemetryDumpShape"));
            Assert.IsTrue(ContainsOrdinal(projection, "TryReadTerminalInputTelemetryDumpEntry"));
            Assert.IsFalse(ContainsOrdinal(runtime, "catch (" + "Exception"));
            Assert.IsFalse(ContainsOrdinal(projection, "catch (" + "Exception"));
            Assert.IsFalse(ContainsOrdinal(runtime, "WriteBlackBoxDump(_dumpFullPath, faultFlags, " + "telemetryRing)"));
            Assert.IsFalse(ContainsOrdinal(projection, "WriteTerminalInputBlackBoxDump(_terminalProjectionDumpFullPath, faultFlags, " + "telemetryRing)"));
        }

        private static bool ContainsOrdinal(string text, string value)
        {
            return text.IndexOf(value, StringComparison.Ordinal) >= 0;
        }

        private static int OffsetOf<T>(string fieldName) where T : struct
        {
            FieldInfo field = typeof(T).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
                throw new InvalidOperationException("Missing field: " + typeof(T).FullName + "." + fieldName);

            return (int)Marshal.OffsetOf<T>(fieldName);
        }
    }
}
#endif

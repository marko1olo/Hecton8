using System.IO;
using System.Reflection;
using Hecton8.Core.Memory;
using Hecton8.Editor.Generators.Graphics;
using Hecton8.World;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class BiomeTransitionPolisher1628EditTests
    {
        [Test]
        public void BiomeLightingParametersDtoPinsCbufferLayout()
        {
            Assert.AreEqual(64, UnsafeUtility.SizeOf<BiomeLightingParametersDTO>());
            Assert.AreEqual(0, OffsetOf<BiomeLightingParametersDTO>(nameof(BiomeLightingParametersDTO.PrimaryFogColor)));
            Assert.AreEqual(16, OffsetOf<BiomeLightingParametersDTO>(nameof(BiomeLightingParametersDTO.SecondaryFogColor)));
            Assert.AreEqual(32, OffsetOf<BiomeLightingParametersDTO>(nameof(BiomeLightingParametersDTO.FogDensity)));
            Assert.AreEqual(36, OffsetOf<BiomeLightingParametersDTO>(nameof(BiomeLightingParametersDTO.BlendFactor)));
            Assert.AreEqual(40, OffsetOf<BiomeLightingParametersDTO>(nameof(BiomeLightingParametersDTO.LightShaftIntensity)));
            Assert.IsTrue(BiomeTransitionNativeLayout.Validate());
        }

        [Test]
        public void DitherFogIncludeUsesBayer8x8AndNoRaymarchLoop()
        {
            string source = ReadProjectFile(BiomeTransitionPolisher.DitherFogIncludePath);
            Assert.IsTrue(BiomeTransitionPolisher.ValidateDitherFogShaderText(source, out string reason), reason);
            StringAssert.Contains("CBUFFER_START(H8BiomeLightingParameters)", source);
            StringAssert.Contains("H8DitherFogBayer8x8", source);
            StringAssert.Contains("thresholds[64]", source);
            StringAssert.Contains("H8DitherFogAnalyticalFactor", source);
            StringAssert.Contains("H8DitherFogLightShaftOcclusion", source);
            StringAssert.Contains("H8DitherFogThermalDistortionOffset", source);
            StringAssert.Contains("_H8GlobalQualityWeight", source);
            StringAssert.Contains("H8DitherFogResolveQualityWeight", source);
            StringAssert.Contains("step(0.5, abs(_H8BiomePad1.x))", source);
            StringAssert.Contains("float resolved = lerp(fallback, payload, payloadValid);", source);
            StringAssert.Contains("return lerp(resolved, global, globalValid);", source);
            Assert.That(source.ToLowerInvariant(), Does.Not.Contain("raymarch"));
        }

        [Test]
        public void DearLieFogReadsDepthOncePerStage()
        {
            Assert.IsTrue(BiomeTransitionPolisher.CountDearLieDepthReads(out int proxyReads, out int compositeReads));
            Assert.AreEqual(1, proxyReads, "Proxy path must read depth once through ResolveProxyFog.");
            Assert.AreEqual(1, compositeReads, "Composite path must read depth once on TBDR hardware.");
        }

        [Test]
        public void Requested71670LaneIsNotHijacked()
        {
            Assert.AreEqual(71670, (int)BufferID.ShinobuSeaglideAudioSignals);
            Assert.AreEqual(71671, (int)BufferID.ShinobuSeaglideCavitationSignals);
            Assert.AreEqual(71672, (int)BufferID.ShinobuSeaglideCsvScratch);
            BiomeTransitionPolisher.BiomeTransitionPolishReport report = BiomeTransitionPolisher.CreateBaseReport();
            Assert.IsFalse(report.requestedBufferRangeHijacked);
            StringAssert.Contains("71220..71231", report.activeBiomeBufferRoute);
        }

        [Test]
        public void BiomeRuntimeBridgeUsesGlobalCbufferNotMaterialMutation()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/World/Biomes/BiomeTransitionManagerRuntime.cs");
            string uploadBlock = ExtractMethodBlock(source, "private void TryUploadBiomeLightingParametersFromPayload");
            string compactBlock = ExtractMethodBlock(source, "private unsafe void TryUploadBiomeLightingParametersCBuffer");
            string compactResolveBlock = ExtractMethodBlock(source, "private static BiomeLightingParametersDTO ResolveBiomeLightingParameters");
            string publishBlock = ExtractMethodBlock(source, "private void PublishShaderPayloadToUnityGlobals");
            string releaseBlock = ExtractMethodBlock(source, "private void ReleaseShaderPayloadBuffers");

            StringAssert.Contains("TryUploadBiomeLightingParametersCBuffer", uploadBlock);
            StringAssert.Contains("LockBufferForWrite<BiomeLightingParametersDTO>", compactBlock);
            StringAssert.Contains("BiomeLightingParametersCBufferId", compactBlock);
            StringAssert.Contains("BiomeTransitionConstants.BiomeLightingParametersStrideBytes", compactBlock);
            StringAssert.Contains("HashBiomeLightingParameters", compactBlock);
            StringAssert.Contains("payloadHash == _lastBiomeLightingParametersHash", compactBlock);
            StringAssert.Contains("try", compactBlock);
            StringAssert.Contains("finally", compactBlock);
            StringAssert.Contains("UnsafeUtility.MemCpy", compactBlock);
            StringAssert.Contains("UnlockBufferAfterWrite<BiomeLightingParametersDTO>", compactBlock);
            Assert.That(compactBlock, Does.Not.Contain("mapped[0] ="));
            StringAssert.Contains("_lastBiomeLightingParametersHash = 0u", releaseBlock);
            StringAssert.Contains("_hasUploadedBiomeLightingParameters = false", releaseBlock);
            Assert.AreEqual(1, CountOrdinal(source, "Shader.SetGlobalConstantBuffer"));
            Assert.That(source, Does.Not.Contain("LockBufferForWrite<BiomeTransitionShaderPayloadCBufferDTO>"));
            StringAssert.Contains("_pad0 = qualityWeight", compactResolveBlock);
            StringAssert.Contains("_pad1 = 1f", compactResolveBlock);
            StringAssert.Contains("HashShaderGlobalPayload", publishBlock);
            StringAssert.Contains("globalPayloadHash == _lastShaderGlobalPayloadHash", publishBlock);
            StringAssert.Contains("Shader.SetGlobalVector", publishBlock);
            StringAssert.Contains("H8GlobalQualityWeightId", publishBlock);
            StringAssert.Contains("_lastShaderGlobalPayloadHash = 0u", releaseBlock);
            StringAssert.Contains("_hasUploadedShaderGlobalPayload = false", releaseBlock);
            Assert.That(publishBlock, Does.Not.Contain("new Material"));
            Assert.That(publishBlock, Does.Not.Contain(".material"));
        }

        [Test]
        public void ApexHotPathsDoNotUseColdLookupRoutes()
        {
            string runtime = ReadProjectFile("Assets/_Project/Scripts/World/Biomes/BiomeTransitionManagerRuntime.cs");
            string jobs = ReadProjectFile("Assets/_Project/Scripts/World/BiomeTransitionFogBlendJobs.cs");
            string fastTick = ExtractMethodBlock(runtime, "public void FastTick(float deltaTime)");
            string lateFrameTick = ExtractMethodBlock(runtime, "public void LateFrameTick()");

            AssertNoHotLookup(fastTick);
            AssertNoHotLookup(lateFrameTick);
            AssertNoHotLookup(jobs);
        }

        [Test]
        public void ApexComponentLookupIsColdBootstrapOnly()
        {
            string bootstrap = ReadProjectFile("Assets/_Project/Scripts/World/Biomes/BiomeBoundarySdfRuntimeBootstrap.cs");
            string ensureRuntime = ExtractMethodBlock(bootstrap, "private static void EnsureRuntimeInstance()");

            StringAssert.Contains("[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]", bootstrap);
            StringAssert.Contains("TryGetComponent", ensureRuntime);
            Assert.That(ensureRuntime, Does.Not.Contain("FastTick"));
            Assert.That(ensureRuntime, Does.Not.Contain("LateFrameTick"));
            Assert.That(ensureRuntime, Does.Not.Contain("FixedUpdate"));
            Assert.That(ensureRuntime, Does.Not.Contain("Update("));
            Assert.That(ensureRuntime, Does.Not.Contain("Execute("));
        }

        [Test]
        public void ApexPresentationUploadIsLateFrameOnly()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/World/Biomes/BiomeTransitionManagerRuntime.cs");
            string fastTick = ExtractMethodBlock(source, "public void FastTick(float deltaTime)");
            string lateFrameTick = ExtractMethodBlock(source, "public void LateFrameTick()");
            string finalizeBlock = ExtractMethodBlock(source, "private bool TryFinalizeCompletedPipeline");

            StringAssert.Contains("SchedulePipeline", fastTick);
            StringAssert.Contains("_pendingShaderPayloadUpload", fastTick);
            Assert.That(fastTick, Does.Not.Contain("PublishShaderPayloadToUnityGlobals"));
            Assert.That(fastTick, Does.Not.Contain("Shader.SetGlobal"));

            StringAssert.Contains("_pendingShaderPayloadUpload", lateFrameTick);
            StringAssert.Contains("PublishShaderPayloadToUnityGlobals", lateFrameTick);
            StringAssert.Contains("_pendingShaderPayloadUpload = true", finalizeBlock);
        }

        [Test]
        public void ApexPipelineDoesNotCompleteJobsInFastTick()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/World/Biomes/BiomeTransitionManagerRuntime.cs");
            string fastTick = ExtractMethodBlock(source, "public void FastTick(float deltaTime)");
            string scheduleBlock = ExtractMethodBlock(source, "private void SchedulePipeline");
            string finalizeBlock = ExtractMethodBlock(source, "private bool TryFinalizeCompletedPipeline");

            Assert.That(fastTick, Does.Not.Contain("TryComplete"));
            Assert.That(fastTick, Does.Not.Contain(".Complete("));
            StringAssert.Contains("Schedule(", scheduleBlock);
            StringAssert.Contains("_pipelineHandle.IsCompleted", finalizeBlock);
            StringAssert.Contains("TryFinalizeCompleted", finalizeBlock);
        }

        [Test]
        public void ApexWriteLocksAreSingleLaneAndFinallyReleased()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/World/Biomes/BiomeTransitionManagerRuntime.cs");
            string helper = ExtractMethodBlock(source, "private static bool TryWriteSingleBiomeVaultValue");
            string fastTick = ExtractMethodBlock(source, "public void FastTick(float deltaTime)");
            string lateFrameTick = ExtractMethodBlock(source, "public void LateFrameTick()");

            Assert.AreEqual(1, CountOrdinal(source, "TryAcquireWriteLock"));
            Assert.AreEqual(1, CountOrdinal(source, "ReleaseWriteLock"));
            StringAssert.Contains("TryAcquireWriteLock", helper);
            StringAssert.Contains("try", helper);
            StringAssert.Contains("finally", helper);
            StringAssert.Contains("ReleaseWriteLock", helper);
            Assert.That(fastTick, Does.Not.Contain("TryAcquireWriteLock"));
            Assert.That(lateFrameTick, Does.Not.Contain("TryAcquireWriteLock"));
        }

        [Test]
        public void ApexTuningWritesUseCachedRuntimeAndWriteLock()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/World/Biomes/BiomeTransitionManagerRuntime.cs");
            string tuningWrite = ExtractMethodBlock(source, "public static bool TryWriteTuning");
            string defaultWrite = ExtractMethodBlock(source, "private void EnsureTuningDefaultNoRead");

            StringAssert.Contains("ActiveRuntimeInstance", tuningWrite);
            StringAssert.Contains("TryWriteSingleBiomeVaultValue", tuningWrite);
            StringAssert.Contains("TryWriteSingleBiomeVaultValue", defaultWrite);
            Assert.That(tuningWrite, Does.Not.Contain("GlobalRegistry.DataVault"));
            Assert.That(tuningWrite, Does.Not.Contain("TryResolveBiomeVaultBuffer"));
            Assert.That(tuningWrite, Does.Not.Contain("TryOpenExistingBiomeVaultBuffer"));
            Assert.That(source, Does.Not.Contain("TryOpenExistingBiomeVaultBuffer"));
        }

        [Test]
        public void AtmosphericCleanlinessStaticValidatorPasses()
        {
            Assert.IsTrue(BiomeTransitionPolisher.ValidateAtmosphericCleanliness(out string reason), reason);
        }

        [Test]
        public void MasterLitUnityPerMaterialCbufferIsRegisterAligned()
        {
            Assert.IsTrue(
                BiomeTransitionPolisher.ValidateUnityPerMaterialCbufferAlignment(
                    BiomeTransitionPolisher.MasterLitShaderPath,
                    out int byteSize,
                    out string reason),
                reason);
            Assert.AreEqual(192, byteSize);
        }

        [Test]
        public void MasterLitVariantPragmaDebtRemainsBounded()
        {
            int debt = BiomeTransitionPolisher.CountShaderVariantPragmaDebt(BiomeTransitionPolisher.MasterLitShaderPath);
            Assert.LessOrEqual(debt, 4);
        }

        private static int OffsetOf<T>(string fieldName) where T : struct
        {
            FieldInfo field = typeof(T).GetField(fieldName);
            Assert.NotNull(field, fieldName);
            return UnsafeUtility.GetFieldOffset(field);
        }

        private static string ReadProjectFile(string assetPath)
        {
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(fullPath), assetPath);
            return File.ReadAllText(fullPath);
        }

        private static string ExtractMethodBlock(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(signatureIndex, 0, signature);
            int openBrace = source.IndexOf('{', signatureIndex);
            Assert.GreaterOrEqual(openBrace, 0, signature);
            int depth = 0;
            for (int i = openBrace; i < source.Length; i++)
            {
                if (source[i] == '{')
                    depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(openBrace, i - openBrace + 1);
                }
            }

            Assert.Fail("Unclosed method block: " + signature);
            return string.Empty;
        }

        private static int CountOrdinal(string source, string token)
        {
            int count = 0;
            int index = 0;
            while ((index = source.IndexOf(token, index, System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += token.Length;
            }

            return count;
        }

        private static void AssertNoHotLookup(string source)
        {
            Assert.That(source, Does.Not.Contain("GlobalRegistry."));
            Assert.That(source, Does.Not.Contain("GetComponent"));
            Assert.That(source, Does.Not.Contain("TryGetComponent"));
            Assert.That(source, Does.Not.Contain("FindObject"));
            Assert.That(source, Does.Not.Contain("GameObject.Find"));
            Assert.That(source, Does.Not.Contain("TryGetLatestCreated"));
            Assert.That(source, Does.Not.Contain("Camera.main"));
        }
    }
}

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.Generators.World
{
    internal readonly struct AbyssalScatterApexVerificationResult1614
    {
        public readonly int SourceFileCount;
        public readonly int HotMethodCount;
        public readonly int DataVaultAcquireCount;
        public readonly int HelperAcquireCount;
        public readonly int PhaseRouteCount;
        public readonly int ViolationCount;
        public readonly string FirstViolation;

        public AbyssalScatterApexVerificationResult1614(
            int sourceFileCount,
            int hotMethodCount,
            int dataVaultAcquireCount,
            int helperAcquireCount,
            int phaseRouteCount,
            int violationCount,
            string firstViolation)
        {
            SourceFileCount = sourceFileCount;
            HotMethodCount = hotMethodCount;
            DataVaultAcquireCount = dataVaultAcquireCount;
            HelperAcquireCount = helperAcquireCount;
            PhaseRouteCount = phaseRouteCount;
            ViolationCount = violationCount;
            FirstViolation = firstViolation ?? string.Empty;
        }
    }

    internal static class AbyssalScatterApexIntegratorVerifier1614
    {
        private static readonly string[] SourceFiles =
        {
            "Assets/_Project/Editor/Generators/World/AbyssalScatterPolisherJobs.cs",
            "Assets/_Project/Editor/Generators/World/AbyssalScatterPolisherPipeline.cs",
            "Assets/_Project/Editor/Generators/World/AbyssalScatterPolisherWindow.cs",
            "Assets/_Project/Editor/Generators/World/AbyssalScatterPolisherSelfTests.cs",
            "Assets/_Project/Editor/Generators/World/AbyssalScatterApexIntegratorVerifier1614.cs",
            "Assets/_Project/Scripts/Rendering/Scatter/AbyssalScatterBrgDataVaultBootstrap.cs",
            "Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs",
            "Assets/_Project/Scripts/World/GPUScatterDirector.cs",
            "Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs",
            "Assets/_Project/Scripts/World/ScatterGPUIBackend.cs"
        };

        private const string AssemblyDefinitionFile = "Assets/_Project/Editor/Generators/World/Hecton8.AbyssalScatter1614.Editor.asmdef";
        private const string RuntimeMetadataContractFile = "Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs";
        private const string RuntimeBrgBootstrapFile = "Assets/_Project/Scripts/Rendering/Scatter/AbyssalScatterBrgDataVaultBootstrap.cs";
        private const string ScatterPipelineFile = "Assets/_Project/Editor/Generators/World/AbyssalScatterPolisherPipeline.cs";
        private const string ScatterJobsFile = "Assets/_Project/Editor/Generators/World/AbyssalScatterPolisherJobs.cs";
        private const string ScatterWindowFile = "Assets/_Project/Editor/Generators/World/AbyssalScatterPolisherWindow.cs";

        private static readonly string[] HotMethodNames =
        {
            "Tick",
            "FixedTick",
            "LateFrameTick",
            "Update",
            "FixedUpdate",
            "LateUpdate",
            "Execute",
            "RunVisualTick",
            "RunScatterVisualTick"
        };

        private static readonly string[] NonVisualHotMethodNames =
        {
            "Tick",
            "FixedTick",
            "Update",
            "FixedUpdate",
            "LateUpdate",
            "Execute"
        };

        private static readonly string[] HotForbiddenDependencyTokens =
        {
            "GlobalRegistry.Get<",
            "GlobalRegistry.Get(",
            "GlobalRegistry.Get ",
            ".GetComponent<",
            ".GetComponent(",
            "GetComponent<",
            "GetComponent(",
            ".GetComponentInChildren<",
            ".GetComponentInChildren(",
            "GetComponentInChildren<",
            "GetComponentInChildren(",
            ".GetComponentInParent<",
            ".GetComponentInParent(",
            "GetComponentInParent<",
            "GetComponentInParent(",
            ".TryGetComponent<",
            ".TryGetComponent(",
            "TryGetComponent<",
            "TryGetComponent(",
            "GlobalDataVault.TryGetLatestCreated",
            "TryGetLatestCreated(",
            "FindObjectOfType",
            "FindObjectsOfType",
            "GameObject.Find",
            "Resources.Load"
        };

        private static readonly string[] HotManagedAllocationTokens =
        {
            "new List<",
            "new Dictionary<",
            "new HashSet<",
            "new Queue<",
            "new Stack<",
            "new StringBuilder",
            ".ToArray(",
            ".ToList(",
            ".Select(",
            ".Where("
        };

        private static readonly string[] PresentationTokens =
        {
            ".SetBuffer(",
            ".SetTexture(",
            ".SetVector(",
            ".SetFloat(",
            ".SetInt(",
            ".SetMatrix(",
            ".SetConstantBuffer(",
            ".Dispatch(",
            "Graphics.Draw",
            "RenderMesh",
            "SetBatchBuffer(",
            "RegisterMesh(",
            "RegisterMaterial(",
            "UploadNativeArray(",
            "UploadArray("
        };

        private static readonly string[] BuildSpawnTokens =
        {
            "dotnet build",
            "Process.Start(",
            "System.Diagnostics.Process.Start",
            "ProcessStartInfo",
            "BuildPipeline.BuildPlayer"
        };

        private static readonly string[] ForbiddenReportTokens =
        {
            "WriteSourceLedger",
            "File.WriteAllText(",
            "JsonUtility.ToJson(",
            "Newtonsoft.Json",
            ".json"
        };

        private static readonly string[] UnsafeBypassTokens =
        {
            "NativeDisableParallelForRestriction",
            "NativeDisableContainerSafetyRestriction",
            "GetUnsafeBufferPointerWithoutChecks",
            "unsafe struct",
            "unsafe class",
            "unsafe void",
            "unsafe static",
            "unsafe "
        };

        [MenuItem("HECTON-8/World Scatter/1614 APEX Integrator Verify Source")]
        public static void RunMenuVerification()
        {
            if (!RunSourceVerification(out AbyssalScatterApexVerificationResult1614 result))
            {
                Debug.LogError("[1614/APEX] Source verification failed: " + result.FirstViolation);
                return;
            }

            Debug.Log(
                "[1614/APEX] Source verification passed. files=" + result.SourceFileCount +
                " hotMethods=" + result.HotMethodCount +
                " directDataVaultAcquires=" + result.DataVaultAcquireCount +
                " helperAcquires=" + result.HelperAcquireCount +
                " phaseRoutes=" + result.PhaseRouteCount);
        }

        public static bool RunSourceVerification(out AbyssalScatterApexVerificationResult1614 result)
        {
            int fileCount = 0;
            int hotMethodCount = 0;
            int dataVaultAcquireCount = 0;
            int helperAcquireCount = 0;
            int phaseRouteCount = 0;

            for (int i = 0; i < SourceFiles.Length; i++)
            {
                string path = SourceFiles[i];
                if (!File.Exists(path))
                {
                    result = Fail(fileCount, hotMethodCount, dataVaultAcquireCount, helperAcquireCount, phaseRouteCount, path + " missing");
                    return false;
                }

                fileCount++;
                string source = File.ReadAllText(path);
                string stripped = StripCommentsAndStrings(source);

                string buildFailure = string.Empty;
                string reportFailure = string.Empty;
                string hotFailure = string.Empty;
                string phaseFailure = string.Empty;
                string lockFailure = string.Empty;
                string unsafeFailure = string.Empty;
                if (!VerifyNoBuildSpawn(path, stripped, out buildFailure) ||
                    !VerifyNoAutomaticReportWrites(path, stripped, out reportFailure) ||
                    !VerifyNoUnsafeBypass(path, stripped, out unsafeFailure) ||
                    !VerifyHotMethods(path, stripped, ref hotMethodCount, out hotFailure) ||
                    !VerifyPresentationPhase(path, stripped, ref phaseRouteCount, out phaseFailure) ||
                    !VerifyDataVaultLockFlattening(path, stripped, ref dataVaultAcquireCount, ref helperAcquireCount, out lockFailure))
                {
                    string failure = FirstNonEmpty(buildFailure, reportFailure, unsafeFailure, hotFailure, phaseFailure, lockFailure);
                    result = Fail(fileCount, hotMethodCount, dataVaultAcquireCount, helperAcquireCount, phaseRouteCount, failure);
                    return false;
                }
            }

            if (!VerifyAssemblyUnsafeDisabled(out string asmdefFailure))
            {
                result = Fail(fileCount, hotMethodCount, dataVaultAcquireCount, helperAcquireCount, phaseRouteCount, asmdefFailure);
                return false;
            }

            string gpuScatterRouteFailure = string.Empty;
            string vegetationRouteFailure = string.Empty;
            if (!VerifyRequiredPhaseRoute(
                    "Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs",
                    "LateFrameTick",
                    "RunScatterVisualTick",
                    out gpuScatterRouteFailure) ||
                !VerifyRequiredPhaseRoute(
                    "Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs",
                    "LateFrameTick",
                    "RunVisualTick",
                    out vegetationRouteFailure))
            {
                string failure = FirstNonEmpty(gpuScatterRouteFailure, vegetationRouteFailure);
                result = Fail(fileCount, hotMethodCount, dataVaultAcquireCount, helperAcquireCount, phaseRouteCount, failure);
                return false;
            }

            if (!VerifyRuntimeMetadataContract(out string metadataContractFailure))
            {
                result = Fail(fileCount, hotMethodCount, dataVaultAcquireCount, helperAcquireCount, phaseRouteCount, metadataContractFailure);
                return false;
            }

            if (!VerifyQualityDeductionMapContract(out string qualityMapFailure))
            {
                result = Fail(fileCount, hotMethodCount, dataVaultAcquireCount, helperAcquireCount, phaseRouteCount, qualityMapFailure);
                return false;
            }

            if (!VerifyBrgPayloadBindingContract(out string brgPayloadFailure))
            {
                result = Fail(fileCount, hotMethodCount, dataVaultAcquireCount, helperAcquireCount, phaseRouteCount, brgPayloadFailure);
                return false;
            }

            result = new AbyssalScatterApexVerificationResult1614(
                fileCount,
                hotMethodCount,
                dataVaultAcquireCount,
                helperAcquireCount,
                phaseRouteCount,
                0,
                string.Empty);
            return true;
        }

        private static AbyssalScatterApexVerificationResult1614 Fail(
            int fileCount,
            int hotMethodCount,
            int dataVaultAcquireCount,
            int helperAcquireCount,
            int phaseRouteCount,
            string failure)
        {
            return new AbyssalScatterApexVerificationResult1614(
                fileCount,
                hotMethodCount,
                dataVaultAcquireCount,
                helperAcquireCount,
                phaseRouteCount,
                1,
                failure);
        }

        private static bool VerifyNoBuildSpawn(string path, string stripped, out string failure)
        {
            failure = string.Empty;
            for (int i = 0; i < BuildSpawnTokens.Length; i++)
            {
                if (stripped.IndexOf(BuildSpawnTokens[i], StringComparison.Ordinal) >= 0)
                {
                    failure = path + " contains build-spawn token " + BuildSpawnTokens[i];
                    return false;
                }
            }

            return true;
        }

        private static bool VerifyNoAutomaticReportWrites(string path, string stripped, out string failure)
        {
            failure = string.Empty;
            for (int i = 0; i < ForbiddenReportTokens.Length; i++)
            {
                string token = ForbiddenReportTokens[i];
                if (stripped.IndexOf(token, StringComparison.Ordinal) >= 0)
                {
                    failure = path + " contains forbidden report token " + token;
                    return false;
                }
            }

            return true;
        }

        private static bool VerifyNoUnsafeBypass(string path, string stripped, out string failure)
        {
            failure = string.Empty;
            if (!path.StartsWith("Assets/_Project/Editor/Generators/World/", StringComparison.Ordinal))
                return true;

            for (int i = 0; i < UnsafeBypassTokens.Length; i++)
            {
                string token = UnsafeBypassTokens[i];
                if (stripped.IndexOf(token, StringComparison.Ordinal) >= 0)
                {
                    failure = path + " contains unsafe bypass token " + token;
                    return false;
                }
            }

            return true;
        }

        private static bool VerifyAssemblyUnsafeDisabled(out string failure)
        {
            failure = string.Empty;
            if (!File.Exists(AssemblyDefinitionFile))
            {
                failure = AssemblyDefinitionFile + " missing";
                return false;
            }

            string asmdef = File.ReadAllText(AssemblyDefinitionFile);
            if (asmdef.IndexOf("\"allowUnsafeCode\": true", StringComparison.Ordinal) >= 0)
            {
                failure = AssemblyDefinitionFile + " keeps allowUnsafeCode enabled after jobs no longer need unsafe writes.";
                return false;
            }

            return true;
        }

        private static bool VerifyRuntimeMetadataContract(out string failure)
        {
            failure = string.Empty;
            if (!File.Exists(RuntimeMetadataContractFile))
            {
                failure = RuntimeMetadataContractFile + " missing for BRG metadata ABI proof";
                return false;
            }

            string source = StripCommentsAndStrings(File.ReadAllText(RuntimeMetadataContractFile));
            if (source.IndexOf("public const int Stride = 64", StringComparison.Ordinal) < 0)
            {
                failure = RuntimeMetadataContractFile + "::GpuScatterFloraInstanceData stride is not 64 bytes";
                return false;
            }

            return VerifyRuntimeMetadataField(source, "Type", "float", 0, out failure) &&
                   VerifyRuntimeMetadataField(source, "HeightScale", "float", 4, out failure) &&
                   VerifyRuntimeMetadataField(source, "WidthScale", "float", 8, out failure) &&
                   VerifyRuntimeMetadataField(source, "Variation", "float", 12, out failure) &&
                   VerifyRuntimeMetadataField(source, "TemplateIndex", "float", 16, out failure) &&
                   VerifyRuntimeMetadataField(source, "RuntimeState", "float", 20, out failure) &&
                   VerifyRuntimeMetadataField(source, "RuntimeFlags", "float", 24, out failure) &&
                   VerifyRuntimeMetadataField(source, "PulseFrequency", "float", 28, out failure) &&
                   VerifyRuntimeMetadataField(source, "BioluminescenceColor", "Vector4", 32, out failure) &&
                   VerifyRuntimeMetadataField(source, "SwaySpeed", "float", 48, out failure) &&
                   VerifyRuntimeMetadataField(source, "BendAmplitude", "float", 52, out failure) &&
                   VerifyRuntimeMetadataField(source, "HealthNormalized", "float", 56, out failure) &&
                   VerifyRuntimeMetadataField(source, "Reserved0", "float", 60, out failure);
        }

        private static bool VerifyQualityDeductionMapContract(out string failure)
        {
            failure = string.Empty;
            if (!File.Exists(ScatterPipelineFile))
            {
                failure = ScatterPipelineFile + " missing for quality deduction map proof";
                return false;
            }

            if (!File.Exists(ScatterJobsFile))
            {
                failure = ScatterJobsFile + " missing for quality deduction map proof";
                return false;
            }

            string pipeline = StripCommentsAndStrings(File.ReadAllText(ScatterPipelineFile));
            string jobs = StripCommentsAndStrings(File.ReadAllText(ScatterJobsFile));
            if (pipeline.IndexOf("BuildQualityDeductionMap(config, instances, qualityIndices)", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("ValidateQualityDeductionMap(qualityIndices, config.InstanceCount)", StringComparison.Ordinal) < 0)
            {
                failure = ScatterPipelineFile + " does not build and validate the quality deduction map on the cold bake path";
                return false;
            }

            if (pipeline.IndexOf("ResolvePermutationStride(count)", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("QualityBucketCount", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("seen[instanceIndex]", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("GreatestCommonDivisor(stride, instanceCount) != 1", StringComparison.Ordinal) < 0)
            {
                failure = ScatterPipelineFile + " quality map is not proven as a bucketed coprime permutation";
                return false;
            }

            if (jobs.IndexOf("secondImportance", StringComparison.Ordinal) >= 0 ||
                jobs.IndexOf("thirdImportance", StringComparison.Ordinal) >= 0 ||
                jobs.IndexOf("QualityIndices[index] = candidate", StringComparison.Ordinal) >= 0)
            {
                failure = ScatterJobsFile + " contains obsolete duplicate-prone quality selection";
                return false;
            }

            if (jobs.IndexOf("((long)index * stride", StringComparison.Ordinal) < 0 ||
                jobs.IndexOf("% count", StringComparison.Ordinal) < 0)
            {
                failure = ScatterJobsFile + " fallback quality map job is not an overflow-safe permutation";
                return false;
            }

            return true;
        }

        private static bool VerifyBrgPayloadBindingContract(out string failure)
        {
            failure = string.Empty;
            if (!File.Exists(ScatterPipelineFile))
            {
                failure = ScatterPipelineFile + " missing for BRG payload binding proof";
                return false;
            }

            string pipeline = StripCommentsAndStrings(File.ReadAllText(ScatterPipelineFile));
            if (pipeline.IndexOf("ValidateBakedBinaryPayloadOrThrow(outputPath, header)", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("ComputeExpectedBrgByteLength(expected)", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("ReadHeader(reader)", StringComparison.Ordinal) < 0)
            {
                failure = ScatterPipelineFile + " does not verify written .brgdata header and byte length before metadata/prefab binding";
                return false;
            }

            if (pipeline.IndexOf("ValidatePayloadArrayLengthsOrThrow(matrices.Length, metadata.Length, qualityIndices.Length)", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("metadataCount != matrixCount", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("qualityIndexCount != matrixCount", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("(long)matrices.Length * AbyssalScatterPolisherConstants.MatrixStrideBytes", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("(long)metadata.Length * AbyssalScatterPolisherConstants.MetadataStrideBytes", StringComparison.Ordinal) < 0)
            {
                failure = ScatterPipelineFile + " does not fail closed on mismatched .brgdata payload block lengths before serialization";
                return false;
            }

            if (pipeline.IndexOf("MaxCullingBounds = 4096", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("IsCullingBoundsCountWithinBakeCap(boundsCount)", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("boundsCount <= MaxCullingBounds", StringComparison.Ordinal) < 0)
            {
                failure = ScatterPipelineFile + " does not fail closed on oversized culling-bound batches before grid construction";
                return false;
            }

            if (pipeline.IndexOf("MaxCullingGridReferences = 1048576", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("long totalRefs = 0L", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("totalRefs > MaxCullingGridReferences", StringComparison.Ordinal) < 0)
            {
                failure = ScatterPipelineFile + " does not fail closed on oversized culling-grid reference lists before allocation";
                return false;
            }

            if (pipeline.IndexOf("ValidateCullingBoundOrThrow(bound, i)", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("math.isfinite(bound.CenterAup)", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("bound.Extents.x < 0f", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("bound.Extents.z < 0f", StringComparison.Ordinal) < 0)
            {
                failure = ScatterPipelineFile + " does not reject non-finite or negative culling bounds before grid cell conversion";
                return false;
            }

            if (pipeline.IndexOf("ResolveMetadataAssetName(result.OutputPath)", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("ResolvePrefabAssetName(binaryPath)", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("AbyssalScatterBrgDataVaultBootstrap", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("bootstrap.ConfigureCold", StringComparison.Ordinal) < 0)
            {
                failure = ScatterPipelineFile + " does not bind per-binary metadata, prefab name, and runtime BRG bootstrap";
                return false;
            }

            if (pipeline.IndexOf("DefaultMetadataAssetName", StringComparison.Ordinal) >= 0 ||
                pipeline.IndexOf("DefaultPrefabName", StringComparison.Ordinal) >= 0 ||
                pipeline.IndexOf("PFB_WorldScatterChunk_1614", StringComparison.Ordinal) >= 0)
            {
                failure = ScatterPipelineFile + " uses fixed default metadata/prefab output name in the bake path";
                return false;
            }

            if (pipeline.IndexOf("Marshal.SizeOf<T>()", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("Marshal.OffsetOf<T>(fieldName)", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("UnsafeUtility", StringComparison.Ordinal) >= 0)
            {
                failure = ScatterPipelineFile + " does not keep ABI validation on the cold managed Marshal route";
                return false;
            }

            if (pipeline.IndexOf("Path.GetFileName(value)", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("IsAsciiAssetStemChar", StringComparison.Ordinal) < 0)
            {
                failure = ScatterPipelineFile + " does not sanitize BRG output names as one ASCII asset-path segment";
                return false;
            }

            if (pipeline.IndexOf("DefaultMapMagicOutputFolder", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("DefaultCullingDatasetFolder", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("ScanScatterSources(ref result, mapMagicOutputFolder, cullingDatasetFolder)", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("ScanScatterSourcesForFolders", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("TryResolveAssetFolderOrDefault", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("BuildValidSearchFolders", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("SourceFoldersValid", StringComparison.Ordinal) < 0)
            {
                failure = ScatterPipelineFile + " does not route user-selected source folders into scan and bake source discovery";
                return false;
            }

            if (pipeline.IndexOf("WritePrefabCullingBounds(result.CullingDatasetFolder, config, importedBounds", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("PrefabUtility.LoadPrefabContents(prefabPath)", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("PrefabUtility.UnloadPrefabContents(root)", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("root.GetComponentsInChildren<Collider>(true)", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("root.GetComponentsInChildren<Renderer>(true)", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("result.ImportedCullingBoundsCount", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("result.MockCullingBoundsCount", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("GenerateMockCullingBoundsJob", StringComparison.Ordinal) < 0)
            {
                failure = ScatterPipelineFile + " does not bind culling dataset prefab bounds into the spatial exclusion job with mock fallback";
                return false;
            }

            if (!File.Exists(ScatterWindowFile))
            {
                failure = ScatterWindowFile + " missing for source-folder UI binding proof";
                return false;
            }

            string window = StripCommentsAndStrings(File.ReadAllText(ScatterWindowFile));
            if (window.IndexOf("ScanScatterSourcesForFolders(_mapMagicOutputFolder, _cullingDatasetFolder, out _lastResult)", StringComparison.Ordinal) < 0 ||
                window.IndexOf("_mapMagicOutputFolder,", StringComparison.Ordinal) < 0 ||
                window.IndexOf("_cullingDatasetFolder,", StringComparison.Ordinal) < 0 ||
                window.IndexOf("_hasScan = true", StringComparison.Ordinal) < 0 ||
                window.IndexOf("_lastResult.ImportedCullingBoundsCount", StringComparison.Ordinal) < 0 ||
                window.IndexOf("_lastResult.MockCullingBoundsCount", StringComparison.Ordinal) < 0)
            {
                failure = ScatterWindowFile + " exposes source folders but does not bind them to scan and bake execution";
                return false;
            }

            if (pipeline.IndexOf("RequireSerializedProperty(serialized, propertyName)", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("throw new MissingFieldException(typeName, propertyName)", StringComparison.Ordinal) < 0 ||
                pipeline.IndexOf("if (property != null)", StringComparison.Ordinal) >= 0)
            {
                failure = ScatterPipelineFile + " does not fail closed on missing runtime serialized fields";
                return false;
            }

            if (!File.Exists(RuntimeBrgBootstrapFile))
            {
                failure = RuntimeBrgBootstrapFile + " missing for runtime .brgdata DataVault bridge proof";
                return false;
            }

            string bootstrap = StripCommentsAndStrings(File.ReadAllText(RuntimeBrgBootstrapFile));
            if (bootstrap.IndexOf("IGlobalRegistryHotSwapListener", StringComparison.Ordinal) < 0 ||
                bootstrap.IndexOf("ISlowTickable", StringComparison.Ordinal) < 0 ||
                bootstrap.IndexOf("GlobalRegistry.DataVault", StringComparison.Ordinal) < 0 ||
                bootstrap.IndexOf("ResolveStreamingAssetPath", StringComparison.Ordinal) < 0 ||
                bootstrap.IndexOf("UnityWebRequest.Get(uri)", StringComparison.Ordinal) < 0)
            {
                failure = RuntimeBrgBootstrapFile + " does not load .brgdata through cold registry/StreamingAssets routes";
                return false;
            }

            if (bootstrap.IndexOf("StartCoroutine", StringComparison.Ordinal) >= 0 ||
                bootstrap.IndexOf("StopCoroutine", StringComparison.Ordinal) >= 0 ||
                bootstrap.IndexOf("IEnumerator", StringComparison.Ordinal) >= 0 ||
                bootstrap.IndexOf("TryRegisterSlowTickable(this, PriorityLayer.Environment)", StringComparison.Ordinal) < 0 ||
                bootstrap.IndexOf("UnregisterSlowTickable(this, PriorityLayer.Environment)", StringComparison.Ordinal) < 0 ||
                bootstrap.IndexOf("System.Diagnostics.Conditional", StringComparison.Ordinal) < 0 ||
                bootstrap.IndexOf("LogWarningCold", StringComparison.Ordinal) < 0)
            {
                failure = RuntimeBrgBootstrapFile + " does not keep URI StreamingAssets load on coroutine-free temporary slow tick polling with conditional diagnostics";
                return false;
            }

            if (bootstrap.IndexOf("RequiredFileFlags", StringComparison.Ordinal) < 0 ||
                bootstrap.IndexOf("header.Flags != RequiredFileFlags", StringComparison.Ordinal) < 0)
            {
                failure = RuntimeBrgBootstrapFile + " does not fail closed on required .brgdata metadata/quality flags";
                return false;
            }

            if (bootstrap.IndexOf("MaxRuntimeInstanceCount = 1048576", StringComparison.Ordinal) < 0 ||
                bootstrap.IndexOf("header.MatrixCount > MaxRuntimeInstanceCount", StringComparison.Ordinal) < 0)
            {
                failure = RuntimeBrgBootstrapFile + " does not cap runtime .brgdata allocations before NativeArray payload reads";
                return false;
            }

            if (bootstrap.IndexOf("TryWriteMatricesCold", StringComparison.Ordinal) < 0 ||
                bootstrap.IndexOf("TryWriteMetadataCold", StringComparison.Ordinal) < 0 ||
                bootstrap.IndexOf("vault.TryAcquireWriteLock(in handle, SystemID.Vfx", StringComparison.Ordinal) < 0 ||
                bootstrap.IndexOf("finally", StringComparison.Ordinal) < 0 ||
                bootstrap.IndexOf("vault.ReleaseWriteLock(in handle, SystemID.Vfx)", StringComparison.Ordinal) < 0)
            {
                failure = RuntimeBrgBootstrapFile + " does not publish DataVault buffers through flattened try/finally write locks";
                return false;
            }

            if (bootstrap.IndexOf("buffer[dst] = payload.Matrices[payload.QualityIndices[dst]]", StringComparison.Ordinal) < 0 ||
                bootstrap.IndexOf("buffer[dst] = payload.Metadata[payload.QualityIndices[dst]]", StringComparison.Ordinal) < 0 ||
                bootstrap.IndexOf("ValidateQualityMap", StringComparison.Ordinal) < 0)
            {
                failure = RuntimeBrgBootstrapFile + " does not apply the continuous quality index map as the runtime draw prefix order";
                return false;
            }

            string runtimeRenderer = StripCommentsAndStrings(File.ReadAllText(RuntimeMetadataContractFile));
            if (runtimeRenderer.IndexOf("MinimumQualityDrawFraction", StringComparison.Ordinal) < 0 ||
                runtimeRenderer.IndexOf("safeCount * drawFraction", StringComparison.Ordinal) < 0)
            {
                failure = RuntimeMetadataContractFile + " does not scale active scatter count by continuous GlobalQualityWeight";
                return false;
            }

            return true;
        }

        private static bool VerifyRuntimeMetadataField(
            string source,
            string fieldName,
            string fieldType,
            int offset,
            out string failure)
        {
            failure = string.Empty;
            string fieldToken = "public " + fieldType + " " + fieldName;
            int fieldIndex = source.IndexOf(fieldToken, StringComparison.Ordinal);
            if (fieldIndex < 0)
            {
                failure = RuntimeMetadataContractFile + "::GpuScatterFloraInstanceData missing " + fieldToken;
                return false;
            }

            int windowStart = Math.Max(0, fieldIndex - 160);
            string window = source.Substring(windowStart, fieldIndex - windowStart);
            string offsetToken = "[FieldOffset(" + offset + ")]";
            if (window.IndexOf(offsetToken, StringComparison.Ordinal) < 0)
            {
                failure = RuntimeMetadataContractFile + "::" + fieldName + " missing " + offsetToken;
                return false;
            }

            return true;
        }

        private static bool VerifyHotMethods(string path, string stripped, ref int hotMethodCount, out string failure)
        {
            failure = string.Empty;
            for (int i = 0; i < HotMethodNames.Length; i++)
            {
                string methodName = HotMethodNames[i];
                int searchIndex = 0;
                while (TryFindMethodBody(stripped, methodName, searchIndex, out int bodyStart, out int bodyEnd, out int nextIndex))
                {
                    hotMethodCount++;
                    string body = stripped.Substring(bodyStart, bodyEnd - bodyStart);
                    if (ContainsAny(body, HotForbiddenDependencyTokens, out string dependencyToken))
                    {
                        failure = path + "::" + methodName + " contains hot dependency lookup " + dependencyToken;
                        return false;
                    }

                    if (ContainsAny(body, HotManagedAllocationTokens, out string allocationToken))
                    {
                        failure = path + "::" + methodName + " contains hot managed allocation " + allocationToken;
                        return false;
                    }

                    searchIndex = nextIndex;
                }
            }

            return true;
        }

        private static bool VerifyPresentationPhase(string path, string stripped, ref int phaseRouteCount, out string failure)
        {
            failure = string.Empty;
            for (int i = 0; i < NonVisualHotMethodNames.Length; i++)
            {
                string methodName = NonVisualHotMethodNames[i];
                int searchIndex = 0;
                while (TryFindMethodBody(stripped, methodName, searchIndex, out int bodyStart, out int bodyEnd, out int nextIndex))
                {
                    string body = stripped.Substring(bodyStart, bodyEnd - bodyStart);
                    if (ContainsAny(body, PresentationTokens, out string presentationToken))
                    {
                        failure = path + "::" + methodName + " writes presentation state outside LateFrameTick/VISUAL_SYNC route via " + presentationToken;
                        return false;
                    }

                    searchIndex = nextIndex;
                }
            }

            if (TryFindMethodBody(stripped, "LateFrameTick", 0, out int lateStart, out int lateEnd, out _) ||
                TryFindMethodBody(stripped, "RunVisualTick", 0, out lateStart, out lateEnd, out _) ||
                TryFindMethodBody(stripped, "RunScatterVisualTick", 0, out lateStart, out lateEnd, out _))
            {
                string body = stripped.Substring(lateStart, lateEnd - lateStart);
                if (ContainsAny(body, PresentationTokens, out _))
                    phaseRouteCount++;
            }

            return true;
        }

        private static bool VerifyRequiredPhaseRoute(
            string path,
            string entryMethod,
            string requiredCall,
            out string failure)
        {
            failure = string.Empty;
            if (!File.Exists(path))
            {
                failure = path + " missing for phase route proof";
                return false;
            }

            string stripped = StripCommentsAndStrings(File.ReadAllText(path));
            if (!TryFindMethodBody(stripped, entryMethod, 0, out int bodyStart, out int bodyEnd, out _))
            {
                failure = path + "::" + entryMethod + " missing";
                return false;
            }

            string body = stripped.Substring(bodyStart, bodyEnd - bodyStart);
            if (body.IndexOf(requiredCall + "(", StringComparison.Ordinal) < 0)
            {
                failure = path + "::" + entryMethod + " does not route through " + requiredCall;
                return false;
            }

            return true;
        }

        private static bool VerifyDataVaultLockFlattening(
            string path,
            string stripped,
            ref int dataVaultAcquireCount,
            ref int helperAcquireCount,
            out string failure)
        {
            failure = string.Empty;
            int searchIndex = 0;
            while (TryFindAnyMethodBody(stripped, searchIndex, out string methodName, out int bodyStart, out int bodyEnd, out int nextIndex))
            {
                string body = stripped.Substring(bodyStart, bodyEnd - bodyStart);
                int directAcquireCount = CountToken(body, "TryAcquireWriteLock(");
                if (directAcquireCount > 0)
                {
                    dataVaultAcquireCount += directAcquireCount;
                    if (directAcquireCount > 1)
                    {
                        failure = path + "::" + methodName + " directly acquires more than one DataVault write lock.";
                        return false;
                    }

                    if (body.IndexOf("finally", StringComparison.Ordinal) < 0 ||
                        body.IndexOf("ReleaseWriteLock(", StringComparison.Ordinal) < 0)
                    {
                        failure = path + "::" + methodName + " directly acquires DataVault write lock without try/finally ReleaseWriteLock.";
                        return false;
                    }
                }

                List<int> helperAcquirePositions = CollectHelperAcquirePositions(body);
                if (helperAcquirePositions.Count > 0)
                {
                    helperAcquireCount += helperAcquirePositions.Count;
                    if (directAcquireCount == 0 && methodName.StartsWith("TryAcquire", StringComparison.Ordinal))
                    {
                        searchIndex = nextIndex;
                        continue;
                    }

                    if (!VerifyHelperAcquireReleaseShape(path, methodName, body, helperAcquirePositions, out failure))
                        return false;
                }

                searchIndex = nextIndex;
            }

            return true;
        }

        private static bool VerifyHelperAcquireReleaseShape(
            string path,
            string methodName,
            string body,
            List<int> helperAcquirePositions,
            out string failure)
        {
            failure = string.Empty;
            for (int i = 0; i < helperAcquirePositions.Count; i++)
            {
                int acquirePosition = helperAcquirePositions[i];
                int segmentEnd = i + 1 < helperAcquirePositions.Count ? helperAcquirePositions[i + 1] : body.Length;
                string segment = body.Substring(acquirePosition, segmentEnd - acquirePosition);
                if (segment.IndexOf("finally", StringComparison.Ordinal) < 0 ||
                    (segment.IndexOf("ReleaseWriteLock(", StringComparison.Ordinal) < 0 &&
                     segment.IndexOf("ReleaseScatterTelemetryRingWrite(", StringComparison.Ordinal) < 0))
                {
                    failure = path + "::" + methodName + " helper-acquires a write buffer without release before the next write acquire.";
                    return false;
                }
            }

            return true;
        }

        private static List<int> CollectHelperAcquirePositions(string body)
        {
            List<int> positions = new List<int>(4);
            int index = 0;
            while (index < body.Length)
            {
                index = body.IndexOf("TryAcquire", index, StringComparison.Ordinal);
                if (index < 0)
                    break;

                int paren = body.IndexOf('(', index);
                if (paren < 0)
                    break;

                string invocation = body.Substring(index, paren - index);
                if ((invocation.IndexOf("ForWrite", StringComparison.Ordinal) >= 0 ||
                     invocation.EndsWith("Write", StringComparison.Ordinal)) &&
                    invocation.IndexOf("TryAcquireWriteLock", StringComparison.Ordinal) < 0)
                {
                    positions.Add(index);
                }

                index = paren + 1;
            }

            return positions;
        }

        private static bool TryFindMethodBody(
            string source,
            string methodName,
            int startIndex,
            out int bodyStart,
            out int bodyEnd,
            out int nextIndex)
        {
            bodyStart = 0;
            bodyEnd = 0;
            nextIndex = source.Length;

            int index = startIndex;
            while (index < source.Length)
            {
                index = source.IndexOf(methodName, index, StringComparison.Ordinal);
                if (index < 0)
                    return false;

                int before = index - 1;
                int after = index + methodName.Length;
                if ((before < 0 || !IsIdentifierChar(source[before])) &&
                    (after >= source.Length || !IsIdentifierChar(source[after])) &&
                    TryFindBodyAfterIdentifier(source, after, out bodyStart, out bodyEnd))
                {
                    nextIndex = bodyEnd + 1;
                    return true;
                }

                index = after;
            }

            return false;
        }

        private static bool TryFindAnyMethodBody(
            string source,
            int startIndex,
            out string methodName,
            out int bodyStart,
            out int bodyEnd,
            out int nextIndex)
        {
            methodName = string.Empty;
            bodyStart = 0;
            bodyEnd = 0;
            nextIndex = source.Length;

            for (int i = startIndex; i < source.Length; i++)
            {
                if (!IsIdentifierStart(source[i]))
                    continue;

                int identifierEnd = i + 1;
                while (identifierEnd < source.Length && IsIdentifierChar(source[identifierEnd]))
                    identifierEnd++;

                string candidateName = source.Substring(i, identifierEnd - i);
                if (IsControlFlowKeyword(candidateName))
                {
                    i = identifierEnd;
                    continue;
                }

                if (TryFindBodyAfterIdentifier(source, identifierEnd, out bodyStart, out bodyEnd))
                {
                    methodName = candidateName;
                    nextIndex = bodyEnd + 1;
                    return true;
                }

                i = identifierEnd;
            }

            return false;
        }

        private static bool TryFindBodyAfterIdentifier(string source, int index, out int bodyStart, out int bodyEnd)
        {
            bodyStart = 0;
            bodyEnd = 0;

            int cursor = SkipWhitespace(source, index);
            if (cursor >= source.Length || source[cursor] != '(')
                return false;

            int parenEnd = FindMatching(source, cursor, '(', ')');
            if (parenEnd < 0)
                return false;

            cursor = SkipWhitespace(source, parenEnd + 1);
            if (cursor >= source.Length || source[cursor] != '{')
                return false;

            int braceEnd = FindMatching(source, cursor, '{', '}');
            if (braceEnd < 0)
                return false;

            bodyStart = cursor + 1;
            bodyEnd = braceEnd;
            return true;
        }

        private static int FindMatching(string source, int openIndex, char open, char close)
        {
            int depth = 0;
            for (int i = openIndex; i < source.Length; i++)
            {
                char c = source[i];
                if (c == open)
                    depth++;
                else if (c == close)
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        private static int SkipWhitespace(string source, int index)
        {
            while (index < source.Length && char.IsWhiteSpace(source[index]))
                index++;
            return index;
        }

        private static bool IsControlFlowKeyword(string identifier)
        {
            return identifier == "if" ||
                   identifier == "else" ||
                   identifier == "for" ||
                   identifier == "foreach" ||
                   identifier == "while" ||
                   identifier == "switch" ||
                   identifier == "using" ||
                   identifier == "lock" ||
                   identifier == "fixed" ||
                   identifier == "catch" ||
                   identifier == "checked" ||
                   identifier == "unchecked";
        }

        private static bool ContainsAny(string source, string[] tokens, out string matchedToken)
        {
            for (int i = 0; i < tokens.Length; i++)
            {
                if (source.IndexOf(tokens[i], StringComparison.Ordinal) >= 0)
                {
                    matchedToken = tokens[i];
                    return true;
                }
            }

            matchedToken = string.Empty;
            return false;
        }

        private static int CountToken(string source, string token)
        {
            int count = 0;
            int index = 0;
            while (index < source.Length)
            {
                index = source.IndexOf(token, index, StringComparison.Ordinal);
                if (index < 0)
                    return count;

                count++;
                index += token.Length;
            }

            return count;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrEmpty(values[i]))
                    return values[i];
            }

            return "unknown source verification failure";
        }

        private static string StripCommentsAndStrings(string source)
        {
            char[] chars = source.ToCharArray();
            bool lineComment = false;
            bool blockComment = false;
            bool stringLiteral = false;
            bool verbatimString = false;
            bool charLiteral = false;

            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                char next = i + 1 < chars.Length ? chars[i + 1] : '\0';

                if (lineComment)
                {
                    if (c == '\r' || c == '\n')
                        lineComment = false;
                    else
                        chars[i] = ' ';
                    continue;
                }

                if (blockComment)
                {
                    if (c == '*' && next == '/')
                    {
                        chars[i] = ' ';
                        chars[i + 1] = ' ';
                        i++;
                        blockComment = false;
                    }
                    else if (c != '\r' && c != '\n')
                    {
                        chars[i] = ' ';
                    }
                    continue;
                }

                if (stringLiteral)
                {
                    if (verbatimString && c == '"' && next == '"')
                    {
                        chars[i] = ' ';
                        chars[i + 1] = ' ';
                        i++;
                        continue;
                    }

                    bool close = c == '"' && (verbatimString || !IsEscaped(source, i));
                    if (c != '\r' && c != '\n')
                        chars[i] = ' ';
                    if (close)
                    {
                        stringLiteral = false;
                        verbatimString = false;
                    }
                    continue;
                }

                if (charLiteral)
                {
                    bool close = c == '\'' && !IsEscaped(source, i);
                    if (c != '\r' && c != '\n')
                        chars[i] = ' ';
                    if (close)
                        charLiteral = false;
                    continue;
                }

                if (c == '/' && next == '/')
                {
                    chars[i] = ' ';
                    chars[i + 1] = ' ';
                    i++;
                    lineComment = true;
                    continue;
                }

                if (c == '/' && next == '*')
                {
                    chars[i] = ' ';
                    chars[i + 1] = ' ';
                    i++;
                    blockComment = true;
                    continue;
                }

                if (c == '@' && next == '"')
                {
                    chars[i] = ' ';
                    chars[i + 1] = ' ';
                    i++;
                    stringLiteral = true;
                    verbatimString = true;
                    continue;
                }

                if (c == '"')
                {
                    chars[i] = ' ';
                    stringLiteral = true;
                    verbatimString = false;
                    continue;
                }

                if (c == '\'')
                {
                    chars[i] = ' ';
                    charLiteral = true;
                }
            }

            return new string(chars);
        }

        private static bool IsEscaped(string source, int index)
        {
            int slashCount = 0;
            for (int i = index - 1; i >= 0 && source[i] == '\\'; i--)
                slashCount++;
            return (slashCount & 1) == 1;
        }

        private static bool IsIdentifierStart(char c)
        {
            return c == '_' || char.IsLetter(c);
        }

        private static bool IsIdentifierChar(char c)
        {
            return c == '_' || char.IsLetterOrDigit(c);
        }
    }
}
#endif

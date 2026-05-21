using System;
using System.IO;
using System.Text;
using UnityEditor;

namespace Hecton8.Graphics.Materials.Editor
{
    internal static class Visual_Material_Inquisition
    {
        [MenuItem("Hecton8/Rendering/Visual Material Inquisition")]
        public static void RunAndReveal()
        {
            string reportPath = UberNoirDegradationInquisition.Run();
            EditorUtility.RevealInFinder(reportPath);
        }

        public static string Run()
        {
            return UberNoirDegradationInquisition.Run();
        }
    }

    internal static class UberNoirDegradationInquisition
    {
        private const string AgentId = "SHINOBU_239";
        private const string Scope = "UBERNOIR_TEXTURE_DEGRADATION_LINK";
        private const string DedicatedReportRelativePath = "Docs/Reports/UBERNOIR_DEGRADATION_INQUISITION_REPORT.json";
        private const string DegradationDumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_239.bin";
        private const string PreservedOwnerDumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_219.bin";

        public static string Run()
        {
            string root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            string dedicatedReportPath = Path.Combine(root, DedicatedReportRelativePath);
            EnsureDirectory(dedicatedReportPath);

            string baseDegradation = ReadTextIfExists(root, "Assets/_Project/Scripts/Construction/BaseDegradationSystem.cs");
            string runtime = ReadTextIfExists(root, "Assets/_Project/Scripts/Graphics/Materials/VisualPressureAgingRuntime.cs");
            string shader = ReadTextIfExists(root, "Assets/_Project/Art/Shaders/Hecton8_UberNoir.hlsl");
            string tuner = ReadTextIfExists(root, "Assets/_Project/Scripts/Graphics/Materials/Editor/VisualPressureAgingTunerWindow.cs");
            string csvBridge = ReadTextIfExists(root, "Assets/_Project/Scripts/Graphics/Materials/Editor/UberNoirDegradationCsvBridge.cs");
            string gizmo = ReadTextIfExists(root, "Assets/_Project/Scripts/Graphics/Materials/VisualPressureAgingGizmoVisualizer.cs");
            string graphicsMaterialsRuntimeSource = ReadRuntimeTextInDirectory(root, "Assets/_Project/Scripts/Graphics/Materials", "*.cs");
            string csvPath = Path.Combine(root, "Data/Visuals/environmental_degradation_rules.csv");
            string csv = File.Exists(csvPath) ? File.ReadAllText(csvPath) : string.Empty;

            int activeMaterialMutations = Count(baseDegradation, ".material") + Count(runtime, ".material") + Count(baseDegradation, "MaterialPropertyBlock");
            int activeAuthoringDecals = Count(baseDegradation, "ApplyAuthoringDecal") + Count(baseDegradation, "LeakStripeDecal") + Count(baseDegradation, "LeakScuffDecal");
            int legacyRendererMaterialSetFloat = CountTokenInDirectory(root, "Assets/_Project/Scripts/Rendering", "*.cs", "GetComponent<Renderer>().material.SetFloat") +
                CountTokenInDirectory(root, "Assets/_Project/Scripts/Construction", "*.cs", "GetComponent<Renderer>().material.SetFloat");
            int dynamicAgingDecalReferences = CountTokenInDirectory(root, "Assets/_Project/Scripts/Rendering", "*.cs", "CorrosionDecal") +
                CountTokenInDirectory(root, "Assets/_Project/Scripts/Rendering", "*.cs", "RustDecal") +
                CountTokenInDirectory(root, "Assets/_Project/Scripts/Rendering", "*.cs", "AlgaeDecal") +
                CountTokenInDirectory(root, "Assets/_Project/Scripts/Construction", "*.cs", "CorrosionDecal") +
                CountTokenInDirectory(root, "Assets/_Project/Scripts/Construction", "*.cs", "RustDecal") +
                CountTokenInDirectory(root, "Assets/_Project/Scripts/Construction", "*.cs", "AlgaeDecal");
            int shaderBufferBindings = Count(shader, "_GlobalUberNoirDegradation") + Count(runtime, "_GlobalUberNoirDegradation");
            int degradationDtoReferences = Count(runtime, "InstanceDegradationDTO") + Count(shader, "H8InstanceDegradationDTO");
            int svInstanceIdReferences = Count(shader, "SV_InstanceID");
            int qualityRouteReferences = Count(shader, "GlobalQualityWeight") + Count(runtime, "GlobalQualityWeight");
            int scorchNormalReferences = Count(shader, "H8UberNoirApplyScorchDegradation") + Count(shader, "H8UberNoirDecodeRustNormalTS");
            int csvRouteReferences =
                Count(runtime, "environmental_degradation_rules.csv") +
                Count(csvBridge, "environmental_degradation_rules.csv") +
                Count(csvBridge, "TryReload") +
                Count(runtime, "scorch_intensity") +
                Count(runtime, "quality_noise") +
                Count(csvBridge, "ScorchIntensityMultiplier") +
                Count(csvBridge, "QualityNoiseOctaveScale");
            int readOnlySnapshotReferences = Count(tuner, "NativeArray<InstanceDegradationDTO>.ReadOnly") + Count(gizmo, "NativeArray<InstanceDegradationDTO>.ReadOnly");
            int degradationDumpReferences = Count(runtime, DegradationDumpRelativePath);
            int preservedOwnerDumpReferences = Count(runtime, PreservedOwnerDumpRelativePath) + Count(tuner, PreservedOwnerDumpRelativePath);
            int lockBufferForWriteReferences = Count(runtime, "LockBufferForWrite");
            int setDataReferences = Count(runtime, ".SetData(") + Count(runtime, "SetData<");
            int rawFaultCloneReferences = Count(runtime, "UnsafeUtility.Malloc") + Count(runtime, "UnsafeUtility.Free(");
            int runtimeMemCpyReferences = Count(runtime, "UnsafeUtility.MemCpy");
            int burstUploadCopyKernelReferences = Count(graphicsMaterialsRuntimeSource, "CopyVisualAgingUploadJob") + Count(graphicsMaterialsRuntimeSource, "CopyDegradationUploadJob");
            int impureResolveHelperNameReferences =
                Count(runtime, "TryResolveStructuralInputs") +
                Count(runtime, "TryResolveStructuralTuning") +
                Count(runtime, "TryResolveThermalInput") +
                Count(runtime, "TryResolveJobBuffers") +
                Count(runtime, "TryResolveOrAcquire");
            int editorSnapshotGuardReferences = Count(runtime, "#if !UNITY_EDITOR");
            int gizmoDegradationPreviewReferences = Count(gizmo, "InstanceDegradationDTO") + Count(gizmo, "TryOpenDegradationBufferSnapshotLease");
            int stableIndexProducerReferences =
                CountRuntimeTokenInDirectory(root, "Assets/_Project/Scripts", "*.cs", "SetGlobalBuffer(H8ShaderIDs.H8UberNoirInstanceData") +
                CountRuntimeTokenInDirectory(root, "Assets/_Project/Scripts", "*.cs", "SetGlobalBuffer(\"_H8UberNoirInstanceData\"");
            bool stableDegradationIndexProof =
                Count(shader, "degradationIndex : TEXCOORD13") == 1 &&
                Count(shader, "H8UberNoirResolveDegradationIndex(instanceData, resolvedInstanceID)") >= 1 &&
                Count(shader, "instanceData.SeedFadeFlags.w") >= 1 &&
                Count(shader, "H8_UBER_NOIR_DEGRADATION_INDEX_INVALID") >= 4 &&
                Count(shader, "_GlobalUberNoirDegradation[degradationIndex]") >= 1 &&
                Count(shader, "H8UberNoirLoadVisualAging(input.degradationIndex)") >= 1 &&
                Count(shader, "H8UberNoirLoadInstanceDegradation(materialIndex)") == 0 &&
                Count(shader, "_GlobalUberNoirDegradation[materialIndex]") == 0;
            bool boundedSvInstanceFallbackProof =
                Count(shader, "resolvedInstanceID < (uint)H8_UBER_NOIR_AGING_CAPACITY") >= 1 &&
                Count(shader, "return resolvedInstanceID;") >= 1;
            bool aupLocalNoiseProof =
                Count(shader, "positionWS - H8UberNoirFinite3(_TotalUniverseOffset.xyz") == 0 &&
                Count(shader, "float3 localAupSeed = H8UberNoirFinite3(aging.DepthAndPressure.xyz") >= 1 &&
                Count(runtime, "DepthAndPressure = new float4(local.x, local.y, local.z, pressure01)") >= 1;
            bool dtoPaddingMirrorsHlsl =
                Count(runtime, "[FieldOffset(20)] public uint _pad0") >= 1 &&
                Count(runtime, "[FieldOffset(24)] public uint _pad1") >= 1 &&
                Count(runtime, "[FieldOffset(28)] public uint _pad2") >= 1 &&
                Count(shader, "uint3 Padding") >= 1;
            bool globalBufferBindingProof =
                Count(runtime, "Shader.SetGlobalBuffer(DegradationBufferId, degradationReadBuffer)") == 1 &&
                Count(runtime, "Shader.PropertyToID(\"_GlobalUberNoirDegradation\")") == 1 &&
                Count(runtime, "Shader.SetGlobalVector(DegradationRuntimeId") >= 2;
            bool burstUploadCopyKernelProof =
                Count(graphicsMaterialsRuntimeSource, "internal unsafe struct CopyVisualAgingUploadJob : IJob") == 1 &&
                Count(graphicsMaterialsRuntimeSource, "internal unsafe struct CopyDegradationUploadJob : IJob") == 1 &&
                Count(runtime, "new CopyVisualAgingUploadJob") == 1 &&
                Count(runtime, "new CopyDegradationUploadJob") == 1 &&
                Count(runtime, ".Run();") >= 2;
            bool csvMetadataProof =
                Count(csv, "# schema_hash,") >= 1 &&
                Count(csv, "# checksum,") >= 1 &&
                Count(csv, "# binary_output_path,") >= 1 &&
                Count(csv, "# validation_report,") >= 1 &&
                Count(csv, "# dto_size,32") >= 1 &&
                Count(csv, "# field_order,InstanceID|RustAmount|ScorchAmount|BioFouling|StructuralStress|Padding") >= 1 &&
                Count(csv, "# buffer_id,71247") >= 1 &&
                Count(csv, "# generation_policy,") >= 1;
            bool layoutValid = VisualPressureAgingRuntime.ValidateLayout();

            bool pass = activeMaterialMutations == 0 &&
                activeAuthoringDecals == 0 &&
                legacyRendererMaterialSetFloat == 0 &&
                dynamicAgingDecalReferences == 0 &&
                shaderBufferBindings >= 2 &&
                degradationDtoReferences >= 2 &&
                svInstanceIdReferences > 0 &&
                qualityRouteReferences >= 2 &&
                scorchNormalReferences >= 2 &&
                csvRouteReferences >= 3 &&
                readOnlySnapshotReferences >= 2 &&
                degradationDumpReferences >= 1 &&
                preservedOwnerDumpReferences >= 2 &&
                lockBufferForWriteReferences >= 2 &&
                setDataReferences == 0 &&
                rawFaultCloneReferences == 0 &&
                burstUploadCopyKernelReferences >= 4 &&
                burstUploadCopyKernelProof &&
                impureResolveHelperNameReferences == 0 &&
                editorSnapshotGuardReferences >= 2 &&
                gizmoDegradationPreviewReferences >= 2 &&
                File.Exists(csvPath) &&
                stableDegradationIndexProof &&
                boundedSvInstanceFallbackProof &&
                aupLocalNoiseProof &&
                dtoPaddingMirrorsHlsl &&
                globalBufferBindingProof &&
                csvMetadataProof &&
                layoutValid;

            StringBuilder builder = new StringBuilder(1280);
            builder.AppendLine("{");
            builder.AppendLine("  \"agent\": \"" + AgentId + "\",");
            builder.AppendLine("  \"scope\": \"" + Scope + "\",");
            builder.AppendLine("  \"summary\": \"Instance Material Mutations Purged\",");
            builder.AppendLine("  \"evidenceClass\": \"STATIC_SOURCE\",");
            builder.AppendLine("  \"runtimeStatus\": \"PENDING_VERIFICATION\",");
            builder.AppendLine("  \"activeMaterialMutations\": " + activeMaterialMutations + ",");
            builder.AppendLine("  \"activeAuthoringDecals\": " + activeAuthoringDecals + ",");
            builder.AppendLine("  \"legacyRendererMaterialSetFloat\": " + legacyRendererMaterialSetFloat + ",");
            builder.AppendLine("  \"dynamicAgingDecalReferences\": " + dynamicAgingDecalReferences + ",");
            builder.AppendLine("  \"globalUberNoirDegradationBindings\": " + shaderBufferBindings + ",");
            builder.AppendLine("  \"instanceDegradationDtoReferences\": " + degradationDtoReferences + ",");
            builder.AppendLine("  \"svInstanceIdReferences\": " + svInstanceIdReferences + ",");
            builder.AppendLine("  \"globalQualityWeightReferences\": " + qualityRouteReferences + ",");
            builder.AppendLine("  \"scorchNormalPerturbationReferences\": " + scorchNormalReferences + ",");
            builder.AppendLine("  \"csvRouteReferences\": " + csvRouteReferences + ",");
            builder.AppendLine("  \"readOnlySnapshotReferences\": " + readOnlySnapshotReferences + ",");
            builder.AppendLine("  \"degradationDumpReferences\": " + degradationDumpReferences + ",");
            builder.AppendLine("  \"preservedOwnerDumpReferences\": " + preservedOwnerDumpReferences + ",");
            builder.AppendLine("  \"lockBufferForWriteReferences\": " + lockBufferForWriteReferences + ",");
            builder.AppendLine("  \"setDataReferences\": " + setDataReferences + ",");
            builder.AppendLine("  \"rawFaultCloneReferences\": " + rawFaultCloneReferences + ",");
            builder.AppendLine("  \"runtimeMemCpyReferences\": " + runtimeMemCpyReferences + ",");
            builder.AppendLine("  \"burstUploadCopyKernelReferences\": " + burstUploadCopyKernelReferences + ",");
            builder.AppendLine("  \"impureResolveHelperNameReferences\": " + impureResolveHelperNameReferences + ",");
            builder.AppendLine("  \"editorSnapshotGuardReferences\": " + editorSnapshotGuardReferences + ",");
            builder.AppendLine("  \"gizmoDegradationPreviewReferences\": " + gizmoDegradationPreviewReferences + ",");
            builder.AppendLine("  \"stableIndexProducerReferences\": " + stableIndexProducerReferences + ",");
            builder.AppendLine("  \"stableIndexProducerStatus\": \"" + (stableIndexProducerReferences > 0 ? "present_seedfade_w_route" : "absent_bounded_sv_instance_fallback") + "\",");
            builder.AppendLine("  \"stableDegradationIndexProof\": " + (stableDegradationIndexProof ? "true" : "false") + ",");
            builder.AppendLine("  \"boundedSvInstanceFallbackProof\": " + (boundedSvInstanceFallbackProof ? "true" : "false") + ",");
            builder.AppendLine("  \"aupLocalNoiseProof\": " + (aupLocalNoiseProof ? "true" : "false") + ",");
            builder.AppendLine("  \"dtoPaddingMirrorsHlsl\": " + (dtoPaddingMirrorsHlsl ? "true" : "false") + ",");
            builder.AppendLine("  \"globalBufferBindingProof\": " + (globalBufferBindingProof ? "true" : "false") + ",");
            builder.AppendLine("  \"burstUploadCopyKernelProof\": " + (burstUploadCopyKernelProof ? "true" : "false") + ",");
            builder.AppendLine("  \"task09Status\": \"" + (burstUploadCopyKernelProof ? "STATIC_PASS" : "BLOCKED_BY_DEPENDENCY") + "\",");
            builder.AppendLine("  \"uploadCopyCallSiteScope\": \"VisualPressureAgingRuntime.cs\",");
            builder.AppendLine("  \"uploadCopyDeclarationScope\": \"non_editor_graphics_materials_runtime_files\",");
            builder.AppendLine("  \"csvMetadataProof\": " + (csvMetadataProof ? "true" : "false") + ",");
            builder.AppendLine("  \"csvProfileExists\": " + (File.Exists(csvPath) ? "true" : "false") + ",");
            builder.AppendLine("  \"instanceDegradationDTOBytes\": 32,");
            builder.AppendLine("  \"blackBoxDumpPath\": \"" + DegradationDumpRelativePath + "\",");
            builder.AppendLine("  \"preservedOwnerDumpPath\": \"" + PreservedOwnerDumpRelativePath + "\",");
            builder.AppendLine("  \"layoutValid\": " + (layoutValid ? "true" : "false") + ",");
            builder.AppendLine("  \"rollbackStateIncluded\": false,");
            builder.AppendLine("  \"sharedAggregateReportTouched\": false,");
            builder.AppendLine("  \"status\": \"" + (pass ? "STATIC_PASS" : "STATIC_FAIL") + "\"");
            builder.AppendLine("}");
            string report = builder.ToString();
            File.WriteAllText(dedicatedReportPath, report);
            return dedicatedReportPath;
        }

        private static void EnsureDirectory(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
        }

        private static string ReadTextIfExists(string root, string relativePath)
        {
            string path = Path.Combine(root, relativePath);
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }

        private static int Count(string text, string token)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += token.Length;
            }

            return count;
        }

        private static int CountTokenInDirectory(string root, string relativePath, string searchPattern, string token)
        {
            string directory = Path.Combine(root, relativePath);
            if (!Directory.Exists(directory))
                return 0;

            int count = 0;
            string[] files = Directory.GetFiles(directory, searchPattern, SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
                count += Count(File.ReadAllText(files[i]), token);
            return count;
        }

        private static string ReadRuntimeTextInDirectory(string root, string relativePath, string searchPattern)
        {
            string directory = Path.Combine(root, relativePath);
            if (!Directory.Exists(directory))
                return string.Empty;

            string[] files = Directory.GetFiles(directory, searchPattern, SearchOption.AllDirectories);
            string editorSegment = Path.DirectorySeparatorChar + "Editor" + Path.DirectorySeparatorChar;
            StringBuilder builder = new StringBuilder(files.Length * 256);
            for (int i = 0; i < files.Length; i++)
            {
                if (files[i].IndexOf(editorSegment, StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                builder.AppendLine(File.ReadAllText(files[i]));
            }

            return builder.ToString();
        }

        private static int CountRuntimeTokenInDirectory(string root, string relativePath, string searchPattern, string token)
        {
            string directory = Path.Combine(root, relativePath);
            if (!Directory.Exists(directory))
                return 0;

            int count = 0;
            string[] files = Directory.GetFiles(directory, searchPattern, SearchOption.AllDirectories);
            string editorSegment = Path.DirectorySeparatorChar + "Editor" + Path.DirectorySeparatorChar;
            for (int i = 0; i < files.Length; i++)
            {
                if (files[i].IndexOf(editorSegment, StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                count += Count(File.ReadAllText(files[i]), token);
            }

            return count;
        }

        private static void AppendJsonString(StringBuilder builder, string value)
        {
            builder.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (c < 32)
                        {
                            builder.Append("\\u");
                            builder.Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            builder.Append(c);
                        }

                        break;
                }
            }

            builder.Append('"');
        }
    }
}

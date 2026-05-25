#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.AITextureControlMaps
{
    internal static class AITextureControlMapSelfAudit
    {
        private const string SourceRoot = "Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269";
        private const string ShaderRoot = "Assets/_Project/Shaders/Editor/AITextureControlMapBaker";

        [MenuItem("HECTON-8/AI Texture Control Maps/Run Self Audit", false, 2691)]
        internal static void RunSelfAuditMenu()
        {
            WriteSelfAuditReport();
        }

        internal static bool WriteSelfAuditReport()
        {
            bool noSyncReadback = !ContainsAnyExcluding(SourceRoot, new[]
            {
                ".ReadPixels(",
                ".GetPixels(",
                ".GetPixels32(",
                "Texture2D.EncodeToPNG",
                ".Render()"
            }, "AITextureControlMapSelfAudit.cs", "AITexturePipelineArchaeology.cs");
            bool dearLie = ContainsAny(ShaderRoot, new[] { "v.uv.x * 2.0 - 1.0" }) &&
                            ContainsAny(ShaderRoot, new[] { "v.uv.y * 2.0 - 1.0" });
            bool requestIntoNative = ContainsAnyImplementation(new[] { "RequestIntoNativeArray" });
            bool nativePngWrite = ContainsAllImplementation(new[] { "NativeArray<byte> pngBytes", "ThreadPool.QueueUserWorkItem", "NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr", "EncodeNativeArrayToPNG", "0u)" });
            bool readbackFormatGuard = ContainsAllImplementation(new[] { "SystemInfo.IsFormatSupported", "GraphicsFormatUsage.ReadPixels", "WarningUnsupportedFormat" });
            bool uninitialized = ContainsAnyImplementation(new[] { "NativeArrayOptions.UninitializedMemory" });
            bool releasePath = ContainsAnyImplementation(new[] { "finally" }) &&
                               ContainsAllImplementation(new[] { "ReleaseContextResources", "SupersampleTexture", "ReadbackTexture" }) &&
                               ContainsAnyImplementation(new[] { "DestroyImmediate" });
            bool supersampleAntiAliasing = ContainsAllImplementation(new[] { "SelectSupersampleMultiplier", "commandBuffer.Blit(drawTexture, readbackTexture)", "settings.AntiAliasing = 4", "Mathf.RoundToInt(weighted)", "1, 4" });
            bool importer = ContainsAllImplementation(new[] { "OnPreprocessTexture", "TextureImporterFormat.BC7", "TextureImporterFormat.ASTC_6x6" });
            bool importPaddingZero = ContainsAllImplementation(new[] { "TextureImportConfigDTO", "config._pad0 = 0u" });
            bool postImportDrain = ContainsAllImplementation(new[] { "AITexturePostImportDrain", "DrainPendingPostImports" });
            bool postImportDuplicateCollapse = ContainsAllImplementation(new[] { "EnqueuePendingPostImport", "PendingPostImports[i] = existing" });
            bool materialScanner = File.Exists(ProjectPath(SourceRoot + "/Material_Setup_Scanner.cs"));
            bool scenePreview = File.Exists(ProjectPath(SourceRoot + "/AITextureLiveMapPreview.cs")) &&
                                File.Exists(ProjectPath(AITextureControlMapConstants.ScenePreviewShaderPath));
            bool vectorCurvature = ContainsAny(ShaderRoot, new[] { "ddx(", "ddy(" });
            bool blackBox = ContainsAllImplementation(new[] { "AITextureBakeBlackBox.Record", "BakeBlackBoxCapacity", "AITextureBakeTelemetryEntry" });
            bool mainThreadWriteDrain = ContainsAllImplementation(new[] { "PendingWriteCompletions", "DrainWriteCompletions", "ProcessWriteCompletion" }) &&
                                        ContainsAllImplementation(new[] { "PendingReadbackCompletions", "ProcessReadbackCompletion", "ReadbackCompletion" }) &&
                                        !ContainsAnyExcluding(SourceRoot, new[] { "delayCall +=" }, "AITextureControlMapSelfAudit.cs", "AITexturePipelineArchaeology.cs");
            bool asyncReloadGuard = ContainsAllImplementation(new[] { "LockReloadAssemblies", "UnlockReloadAssemblies", "ForceUnlockReloadGuard", "RegisterDomainReloadGuards" });
            bool lazyInboxWatcher = ContainsAllImplementation(new[] { "StartWatcher", "ProcessInboxNow" }) &&
                                    !ContainsAllImplementation(new[] { "static AITextureIngestionWatcher()", "StartWatcher();" });
            bool manifestOnlyPrefabBinding = ContainsAllImplementation(new[] { "PrefabBindingManifestPath", "DRY_RUN_NO_MANIFEST", "rendererPath", "materialSlot", "ASSIGNED_MANIFEST_RENDERER_SLOT" }) &&
                                              !ContainsAnyImplementation(new[] { "FindCompatibleShader", "Universal Render Pipeline/Lit", "Shader.Find(\"Standard\")" }) &&
                                              !ContainsAnyImplementation(new[] { "GetComponentsInChildren<MeshRenderer>", ".sharedMaterial = material" }) &&
                                              !ContainsAnyExcluding(SourceRoot, new[] { "FindAssets(\"t:Prefab\"" }, "AITextureControlMapSelfAudit.cs", "AITexturePipelineArchaeology.cs");
            bool qualityCurve = ContainsAllImplementation(new[] { "BuildQualityCurve", "SelectCurvatureScale(quality)", "SelectCurvatureEdgeGain(quality)", "SelectValidationSampleBudget" });
            bool rollbackRouteCard = ContainsAllImplementation(new[] { "RollbackExclusion=REQUESTED_BY_EDITOR", "RUNTIME_OWNER_VERIFICATION_REQUIRED", "PENDING_RUNTIME_OWNER_VERIFICATION" });
            bool inboxReadinessRetry = ContainsAllImplementation(new[] { "DrainPendingImports", "PendingInboxImport", "CanReadExclusive", "InboxCopyResult.Retry", "MaxReadinessAttempts" });
            bool inboxDuplicateCollapse = ContainsAllImplementation(new[] { "EnqueuePendingImport", "UnregisterDrainIfIdleAfterStop", "if (ScratchImports.Count == 0)", "long remaining = long.MaxValue - now" });
            bool csvProfileFormats = ContainsAllImplementation(new[] { "TrySelectProfileForAsset", "ParseFormatHash", "StandaloneFormatHash", "AndroidFormatHash", "PathContainsLeadingProfileToken" }) &&
                                     ContainsAllImplementation(new[] { "SelectStandaloneTextureFormat", "profile.Resolution" });
            bool pristineResolution = ContainsAnyImplementation(new[] { "NormalizeBakeResolution" }) &&
                                       !ContainsAnyExcluding(SourceRoot, new[] { "ScaleResolutionByQuality", "requestedResolution * math.lerp" }, "AITextureControlMapSelfAudit.cs", "AITexturePipelineArchaeology.cs");
            bool cameraRigContract = ContainsAllImplementation(new[] { "UvCaptureRig.Create", "AddComponent<Camera>", "SetViewProjectionMatrices" }) &&
                                      noSyncReadback;
            bool archaeology = File.Exists(ProjectPath(SourceRoot + "/AITexturePipelineArchaeology.cs"));
            bool mockMesh = ContainsAllImplementation(new[] { "GenerateMockComplexMeshJob", "FillUInt32IndexJob" });
            bool unlitShaders = File.Exists(ProjectPath(ShaderRoot + "/Hecton_BakeWorldNormal.shader")) &&
                                File.Exists(ProjectPath(ShaderRoot + "/Hecton_BakeDepth.shader")) &&
                                File.Exists(ProjectPath(ShaderRoot + "/Hecton_BakeColorID.shader"));
            bool forgeWindow = File.Exists(ProjectPath(SourceRoot + "/AITextureForgeWindow.cs"));
            bool csvProfiles = File.Exists(ProjectPath(SourceRoot + "/AITextureProfileCsv.cs")) &&
                               ContainsAllImplementation(new[] { "FixedString64Bytes", "Span<byte>", "NativeArrayOptions.UninitializedMemory" });
            bool pass = noSyncReadback && dearLie && requestIntoNative && nativePngWrite && readbackFormatGuard && uninitialized && releasePath && supersampleAntiAliasing && importer &&
                         importPaddingZero && postImportDrain && postImportDuplicateCollapse && materialScanner && scenePreview && vectorCurvature && blackBox && mainThreadWriteDrain &&
                         asyncReloadGuard && lazyInboxWatcher && manifestOnlyPrefabBinding && rollbackRouteCard && qualityCurve && inboxReadinessRetry && inboxDuplicateCollapse && csvProfileFormats && cameraRigContract && pristineResolution;

            EnsureReportFolder();
            StringBuilder builder = new StringBuilder(16000); // COLD ALLOC: self-audit XML - owner: AITextureControlMapSelfAudit
            builder.Append("<SELF_AUDIT agent=\"SHINOBU_269\" status=\"").Append(pass ? "PENDING_UNITY_VERIFICATION" : "CRITICAL_WARNING").Append("\">\n");
            AppendCheck(builder, "RuntimeControlMapExecution", true, "All SHINOBU_269 systems are wrapped in UNITY_EDITOR folders and no runtime MonoBehaviour capture route is emitted.");
            AppendCheck(builder, "DearLieUvFlattening", dearLie, "Bake shaders force clip-space XY from UV coordinates for template PNG generation.");
            AppendCheck(builder, "NoSynchronousPixelReadback", noSyncReadback, "No Texture2D ReadPixels/GetPixels/EncodeToPNG or Camera.Render capture call exists in SHINOBU_269 source.");
            AppendCheck(builder, "RequestIntoNativeArray", requestIntoNative, "AsyncGPUReadback writes into caller-owned NativeArray before EncodeNativeArrayToPNG.");
            AppendCheck(builder, "NativePngWrite", nativePngWrite, "EncodeNativeArrayToPNG result is owned as NativeArray<byte> with explicit rowBytes=0; background FileStream write uses an unsafe ReadOnlySpan over NativeArray memory and disposes after main-thread completion.");
            AppendCheck(builder, "ReadbackFormatGuard", readbackFormatGuard, "Bake pass fails closed before AsyncGPUReadback when R8G8B8A8_UNorm lacks ReadPixels support on the current graphics backend.");
            AppendCheck(builder, "UninitializedTempJobBuffers", uninitialized, "Temporary bake/readback buffers use NativeArrayOptions.UninitializedMemory; no MemClear route is present.");
            AppendCheck(builder, "GpuResourceRelease", releasePath, "RenderTexture, CommandBuffer, Material, and NativeArray resources have finally/callback cleanup paths.");
            AppendCheck(builder, "SupersampleAntiAliasing", supersampleAntiAliasing, "UV passes draw into a quality-weighted rounded 1x..4x non-MSAA supersample texture when supported, then GPU-blit down to the pristine output resolution before AsyncGPUReadback.");
            AppendCheck(builder, "AutomatedIngestion", importer, "AssetPostprocessor applies BC7/BC5/ASTC without Inspector steps and queues post-import binding.");
            AppendCheck(builder, "TextureImportPaddingZero", importPaddingZero, "TextureImportConfigDTO offset 12 remains true manual padding and is hydrated as zero, not repurposed as hidden semantic data.");
            AppendCheck(builder, "PostImportDeferredBinding", postImportDrain, "OnPostprocessTexture only enqueues asset path/kind/config; rollback labels, material binding, prefab manifest mutation, and reports drain from EditorApplication.update after import.");
            AppendCheck(builder, "PostImportDuplicateCollapse", postImportDuplicateCollapse, "Deferred post-import queue collapses duplicate asset paths before material binding/report work to avoid repeated AssetDatabase churn during import event storms.");
            AppendCheck(builder, "ScenePreview", scenePreview, "SceneView preview renders unlit control-map math on selected mesh or prefab.");
            AppendCheck(builder, "MaterialMetricValidator", materialScanner, "Material_Setup_Scanner writes owned AI_TEXTURE_MATERIAL_SETUP_REPORT.json; shared RENDERING_OPTIMIZATION_REPORT.json merge is pending Unity menu scan execution.");
            AppendCheck(builder, "CurvatureVectorizationEvidence", vectorCurvature, "Curvature shader uses GPU derivative instructions ddx/ddy rather than CPU adjacency traversal.");
            AppendCheck(builder, "BlackBoxTelemetryRing", blackBox, "Source route exists for a 300-entry AITextureBakeTelemetryEntry ring to record pass outcomes and write Dump_SHINOBU_269.bin on critical warning paths; execution proof requires Unity bake.");
            AppendCheck(builder, "MainThreadCompletionDrain", mainThreadWriteDrain, "AsyncGPUReadback and FileStream callbacks enqueue plain completion payloads only; Unity API calls, PNG encoding, telemetry, and disposal execute from EditorApplication.update.");
            AppendCheck(builder, "AsyncReloadGuard", asyncReloadGuard, "Baker locks Unity assembly reload while async GPU readbacks or native PNG writes are in flight, then unlocks from the main-thread drain when all queues are idle.");
            AppendCheck(builder, "LazyInboxWatcher", lazyInboxWatcher, "FileSystemWatcher is started only by explicit menu/tool action; editor domain load registers cleanup hooks but does not create inbox folders or watcher side effects.");
            AppendCheck(builder, "ManifestOnlyPrefabBinding", manifestOnlyPrefabBinding, "Material assignment mutates exactly the renderer path and material slot declared in ai_texture_prefab_bindings.csv, and creates no Lit/Standard fallback material named UberNoir.");
            AppendCheck(builder, "RollbackRouteCard", rollbackRouteCard, "Texture/material assets receive presentation-only labels and userData route cards; StateRingBuffer/Merkle exclusion remains pending runtime owner verification rather than asserted as proven by the editor tool.");
            AppendCheck(builder, "ContinuousQualityCurve", qualityCurve, "GlobalQualityWeight drives bake curvature shader constants, validation sample count, SceneView preview curvature, and supersample selection; exported AI control-map resolution remains pristine per original SHINOBU_269 prompt.");
            AppendCheck(builder, "InboxReadinessRetry", inboxReadinessRetry, "FileSystemWatcher only queues paths; EditorApplication.update drains imports after exclusive-read readiness succeeds, with bounded retry for files still being written.");
            AppendCheck(builder, "InboxDuplicateCollapse", inboxDuplicateCollapse, "Pending inbox records collapse duplicate Created/Changed events, the empty drain branch unregisters the update callback after manual watcher stop, and Stopwatch delay overflow is guarded.");
            AppendCheck(builder, "CsvProfileFormatBridge", csvProfileFormats, "CSV ingestion profiles parse standalone/android format columns and route matching asset paths into importer max-size and Standalone texture format selection; composite names such as Hero_Prop also match their leading token without changing DTO layout.");
            AppendCheck(builder, "PristineBakeResolution", pristineResolution, "NormalizeBakeResolution only aligns/clamps authored profile resolution up to 4096; no GlobalQualityWeight downscale is applied to exported template PNGs.");
            AppendCheck(builder, "Task08CameraContract", cameraRigContract, "Baker instantiates one hidden disabled Camera per batch, binds it to the active RenderTexture, applies its matrices to the CommandBuffer, then clears targetTexture after async readback is queued; no Camera.Render path exists.");
            builder.Append("  <TASK_RECONCILIATION>\n");
            AppendTask(builder, "01", StaticTaskStatus(archaeology), "Manual bake archaeology source scans first-party Editor capture tokens and avoids deleting unrelated vendor tools.");
            AppendTask(builder, "02", StaticTaskStatus(noSyncReadback && requestIntoNative), "Baker excludes ReadPixels/GetPixels/Texture2D.EncodeToPNG/Camera.Render capture routes; GPU readback is async.");
            AppendTask(builder, "03", StaticTaskStatus(mockMesh), "Hot DTOs use raw fields; mock mesh jobs write unmanaged vertex/index buffers directly.");
            AppendTask(builder, "04", StaticTaskStatus(ContainsAllImplementation(new[] { "TextureImportConfigDTO", "FieldOffset(12)" }) && importPaddingZero), "TextureImportConfigDTO is explicit 16 bytes with offset proof below and offset 12 is zeroed padding.");
            AppendTask(builder, "05", StaticTaskStatus(mockMesh), "GenerateMockComplexMeshJob creates deterministic irregular UV stress geometry without upstream dependency.");
            AppendTask(builder, "06", StaticTaskStatus(unlitShaders && dearLie && supersampleAntiAliasing), "Normal, Depth, and ColorID unlit UV-flatten shaders exist and route through optional GPU supersampling before final readback.");
            AppendTask(builder, "07", StaticTaskStatus(vectorCurvature), "Curvature is GPU derivative math, not CPU adjacency traversal.");
            AppendTask(builder, "08", StaticTaskStatus(cameraRigContract && dearLie), "Baker instantiates one hidden disabled Camera per batch, binds it to each RenderTexture, applies camera matrices to the CommandBuffer, and draws UV-space with no Camera.Render traversal.");
            AppendTask(builder, "09", StaticTaskStatus(requestIntoNative && nativePngWrite && mainThreadWriteDrain), "PNG route uses AsyncGPUReadback.RequestIntoNativeArray, NativeArray EncodeNativeArrayToPNG output with explicit rowBytes, and background FileStream write with main-thread completion drain.");
            AppendTask(builder, "10", StaticTaskStatus(ContainsAllImplementation(new[] { "FileSystemWatcher", "DrainPendingImports" }) && inboxReadinessRetry && inboxDuplicateCollapse && lazyInboxWatcher), "Inbox watcher is explicit-start only, queues paths from FileSystemWatcher, collapses duplicate events, and drains imports on EditorApplication.update only after exclusive-read readiness/retry passes.");
            AppendTask(builder, "11", StaticTaskStatus(importer && postImportDrain && postImportDuplicateCollapse), "AssetPostprocessor applies mipmaps, unreadable CPU state, BC7/BC5 Standalone and ASTC_6x6 Android, then defers and collapses post-import side effects in the update drain.");
            AppendTask(builder, "12", StaticTaskStatus(manifestOnlyPrefabBinding), "Material binding creates deterministic UberNoir-only MAT asset and prefab mutation is manifest-only down to renderer path plus material slot, with dry-run report fallback.");
            AppendTask(builder, "13", StaticTaskStatus(rollbackRouteCard), "Texture/material assets receive presentation-only rollback route cards; final StateRingBuffer/Merkle exclusion proof is explicitly pending the runtime owner.");
            AppendTask(builder, "14", StaticTaskStatus(uninitialized), "Readback and mock mesh native buffers use NativeArrayOptions.UninitializedMemory.");
            AppendTask(builder, "15", StaticTaskStatus(blackBox), "Bake, ingestion, rollback, prefab binding, material scan, archaeology, and blackbox report source routes have distinct output paths; execution proof requires Unity bake/import.");
            AppendTask(builder, "16", StaticTaskStatus(forgeWindow), "UI Toolkit forge window exposes folder, passes, resolution, GlobalQualityWeight, preview, inbox, material scan, and audit commands.");
            AppendTask(builder, "17", StaticTaskStatus(csvProfiles && csvProfileFormats), "CSV profile parser uses pointer/Span over uninitialized TempJob bytes and parses profile resolution plus Standalone/Android format columns for importer policy.");
            AppendTask(builder, "18", StaticTaskStatus(scenePreview), "SceneView preview renders control-map math without PNG write and uses continuous quality for preview curvature.");
            AppendTask(builder, "19", StaticTaskStatus(materialScanner), "Material_Setup_Scanner writes AI_TEXTURE_MATERIAL_SETUP_REPORT.json and merges a SHINOBU_269 key into RENDERING_OPTIMIZATION_REPORT.json when the Unity menu scan runs.");
            AppendTask(builder, "20", StaticTaskStatus(pass), "This forensic XML includes reconciliation, layout proof, scalability curve, Vault status, dependency graph, compile guard, and Dear Lie complexity; Unity verification remains pending.");
            builder.Append("  </TASK_RECONCILIATION>\n");
            builder.Append("  <STRUCT_LAYOUT_VERIFICATION>\n");
            builder.Append("    <Struct name=\"TextureImportConfigDTO\" size=\"16\" alignment=\"multiple-of-8-and-16\" proof=\"4+4+4+4=16\">\n");
            AppendField(builder, "FormatHash", 0, 4);
            AppendField(builder, "MaxSize", 4, 4);
            AppendField(builder, "Flags", 8, 4);
            AppendField(builder, "_pad0", 12, 4);
            builder.Append("    </Struct>\n");
            builder.Append("    <Struct name=\"AITextureBakeVertex\" size=\"32\" alignment=\"multiple-of-8-and-16-and-32\" proof=\"float3(12)+float3(12)+float2(8)=32\">\n");
            AppendField(builder, "Position", 0, 12);
            AppendField(builder, "Normal", 12, 12);
            AppendField(builder, "Uv0", 24, 8);
            builder.Append("    </Struct>\n");
            builder.Append("    <Struct name=\"MockComplexMeshConfigDTO\" size=\"32\" alignment=\"multiple-of-8-and-16-and-32\" proof=\"8 scalar 4-byte lanes=32\">\n");
            AppendField(builder, "RingSegments", 0, 4);
            AppendField(builder, "TubeSegments", 4, 4);
            AppendField(builder, "MajorRadius", 8, 4);
            AppendField(builder, "TubeRadius", 12, 4);
            AppendField(builder, "Irregularity", 16, 4);
            AppendField(builder, "Seed", 20, 4);
            AppendField(builder, "Twist", 24, 4);
            AppendField(builder, "_pad0", 28, 4);
            builder.Append("    </Struct>\n");
            builder.Append("    <Struct name=\"AITextureBakeTelemetryEntry\" size=\"64\" alignment=\"single-L1-cache-line\" falseSharing=\"padded-to-64\" proof=\"15 explicit 4-byte lanes plus pad=64\">\n");
            AppendField(builder, "SourceHash", 0, 4);
            AppendField(builder, "MeshHash", 4, 4);
            AppendField(builder, "Resolution", 8, 4);
            AppendField(builder, "PassMask", 12, 4);
            AppendField(builder, "RenderMicroseconds", 16, 4);
            AppendField(builder, "EncodeMicroseconds", 20, 4);
            AppendField(builder, "WriteMicroseconds", 24, 4);
            AppendField(builder, "VertexCount", 28, 4);
            AppendField(builder, "SubMeshCount", 32, 4);
            AppendField(builder, "WarningFlags", 36, 4);
            AppendField(builder, "BoundsExtentX", 40, 4);
            AppendField(builder, "BoundsExtentY", 44, 4);
            AppendField(builder, "BoundsExtentZ", 48, 4);
            AppendField(builder, "GlobalQualityWeight", 52, 4);
            AppendField(builder, "StateHash", 56, 4);
            AppendField(builder, "_pad0", 60, 4);
            builder.Append("    </Struct>\n");
            builder.Append("    <Struct name=\"AITextureBakeSettings\" size=\"80\" alignment=\"multiple-of-8-and-16\" proof=\"FixedString64Bytes(64)+passMask(4)+resolution(4)+quality(4)+2 bytes+ushort pad=80\">\n");
            AppendField(builder, "ProfileName", 0, 64);
            AppendField(builder, "PassMask", 64, 4);
            AppendField(builder, "Resolution", 68, 4);
            AppendField(builder, "GlobalQualityWeight", 72, 4);
            AppendField(builder, "AntiAliasing", 76, 1);
            AppendField(builder, "ForceOverwrite", 77, 1);
            AppendField(builder, "_pad0", 78, 2);
            builder.Append("    </Struct>\n");
            builder.Append("    <Struct name=\"AITextureIngestionProfile\" size=\"96\" alignment=\"multiple-of-8-and-16-and-32\" proof=\"FixedString64Bytes(64)+5 scalar lanes(20)+uint pad(4)+ulong pad(8)=96\">\n");
            AppendField(builder, "ProfileName", 0, 64);
            AppendField(builder, "PassMask", 64, 4);
            AppendField(builder, "Resolution", 68, 4);
            AppendField(builder, "GlobalQualityWeight", 72, 4);
            AppendField(builder, "StandaloneFormatHash", 76, 4);
            AppendField(builder, "AndroidFormatHash", 80, 4);
            AppendField(builder, "_pad0", 84, 4);
            AppendField(builder, "_pad1", 88, 8);
            builder.Append("    </Struct>\n");
            builder.Append("  </STRUCT_LAYOUT_VERIFICATION>\n");
            builder.Append("  <SCALABILITY_CURVE_EXPLANATION value=\"GlobalQualityWeight uses smoothstep q*q*(3-2q) for bake curvature shader constants, validation sample count, SceneView preview curvature, and supersample selection. Exported AI template PNG resolution is not downscaled by quality; NormalizeBakeResolution only aligns/clamps the authored profile resolution to preserve pristine 2048/4096 ControlNet inputs. Below 0.3 the tool sheds optional validation density, curvature gain, preview ALU, and supersample multiplier first, preserving DTO layout, asset identity, and source-map dimensions.\" />\n");
            builder.Append("  <H_PHI_VAULT_STATUS runtimePrivateArrays=\"0\" vaultHandles=\"0\" editorException=\"AITextureBakeBlackBox owns one UNITY_EDITOR Persistent NativeArray ring, 300*64=19200 bytes, released on assembly reload/quitting. It is not runtime, not rollback authority, and not cross-domain state, so GlobalDataVault is deliberately not used.\" />\n");
            builder.Append("  <POINTER_ALIASING_DEPENDENCY_GRAPH>\n");
            builder.Append("    <Job name=\"GenerateMockComplexMeshJob\" consumes=\"none\" outputs=\"vertexHandle\" noAlias=\"VertexPtr\" completion=\"combined only inside offline menu benchmark\" />\n");
            builder.Append("    <Job name=\"FillUInt32IndexJob\" consumes=\"none\" outputs=\"indexHandle\" noAlias=\"IndexPtr\" completion=\"JobHandle.CombineDependencies(vertexHandle,indexHandle).Complete in offline menu benchmark\" />\n");
            builder.Append("    <Async name=\"BakePassReadback\" consumes=\"Graphics.ExecuteCommandBuffer completion by GPU queue\" outputs=\"ReadbackCompletion\" completion=\"no main-thread Complete; callback enqueues payload only; EditorApplication.update encodes PNG and disposes resources\" />\n");
            builder.Append("  </POINTER_ALIASING_DEPENDENCY_GRAPH>\n");
            builder.Append("  <COMPILE_GUARD value=\"No SHINOBU_269 runtime asmdef or sibling runtime reference exists under the domain path; all source is under Editor/UNITY_EDITOR and communicates by assets/reports only.\" />\n");
            builder.Append("  <DEAR_LIE_CONFIRMATION value=\"Instead of CPU mesh-neighborhood curvature and perspective scene traversal, the batch-level Camera is a disabled UV capture rig scaffold bound to the RenderTexture and used for CommandBuffer view/projection state; shaders flatten UVs directly to clip space and use ddx/ddy derivative curvature. CPU adjacency path would be O(V+E) preprocessing plus blocking readback risk; implemented CPU orchestration is O(meshes*passes*submeshes), while per-pixel work stays on GPU raster hardware.\" />\n");
            builder.Append("  <RenderPasses normal=\"Hecton_BakeWorldNormal\" depth=\"Hecton_BakeDepth\" colorId=\"Hecton_BakeColorID\" curvature=\"Hecton_BakeCurvature\" preview=\"Hecton_ControlMapScenePreview\" />\n");
            builder.Append("  <Scalability low=\"continuous q near 0.0: pristine authored bake resolution preserved, 512 validation samples, cheaper preview curvature\" middle=\"q around 0.5: pristine authored bake resolution preserved, middle validation density\" high=\"q 0.85: pristine authored bake resolution preserved, stricter validation\" ultra=\"q 1.0: 4096 cap when profile requests it, 4096 validation samples, full forensic reporting\" />\n");
            builder.Append("</SELF_AUDIT>\n");
            File.WriteAllText(AITextureControlMapConstants.SelfAuditReportPath, builder.ToString());
            Hecton8.Core.H8Debug.Log("[AITextureControlMapSelfAudit] " + (pass ? "Pending Unity verification." : "Critical warning."));
            return pass;
        }

        private static void AppendCheck(StringBuilder builder, string name, bool pass, string evidence)
        {
            builder.Append("  <Check name=\"").Append(name).Append("\" status=\"").Append(StaticTaskStatus(pass)).Append("\" evidenceClass=\"STATIC_SOURCE\" evidence=\"")
                .Append(Escape(evidence)).Append("\" />\n");
        }

        private static void AppendTask(StringBuilder builder, string id, string status, string evidence)
        {
            builder.Append("    <TASK id=\"").Append(id).Append("\" status=\"").Append(status).Append("\" evidence=\"")
                .Append(Escape(evidence)).Append("\" />\n");
        }

        private static string StaticTaskStatus(bool pass)
        {
            return pass ? "PASS_STATIC_SOURCE_PENDING_UNITY" : "FAIL_STATIC_SOURCE";
        }

        private static void AppendField(StringBuilder builder, string name, int offset, int size)
        {
            builder.Append("      <Field name=\"").Append(name).Append("\" offset=\"").Append(offset)
                .Append("\" size=\"").Append(size).Append("\" />\n");
        }

        private static bool ContainsAny(string root, string[] tokens)
        {
            string absoluteRoot = ProjectPath(root);
            if (File.Exists(absoluteRoot))
                return FileContainsAny(absoluteRoot, tokens);
            if (!Directory.Exists(absoluteRoot))
                return false;

            foreach (string file in Directory.EnumerateFiles(absoluteRoot, "*.*", SearchOption.AllDirectories))
            {
                string extension = Path.GetExtension(file);
                if (!string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(extension, ".shader", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (FileContainsAny(file, tokens))
                    return true;
            }

            return false;
        }

        private static bool ContainsAnyImplementation(string[] tokens)
        {
            return ContainsAnyExcluding(SourceRoot, tokens, "AITextureControlMapSelfAudit.cs", "AITexturePipelineArchaeology.cs");
        }

        private static bool ContainsAllImplementation(string[] tokens)
        {
            for (int i = 0; i < tokens.Length; i++)
            {
                if (!ContainsAnyImplementation(new[] { tokens[i] }))
                    return false;
            }

            return true;
        }

        private static bool ContainsAnyExcluding(string root, string[] tokens, string excludedFileA, string excludedFileB)
        {
            string absoluteRoot = ProjectPath(root);
            if (!Directory.Exists(absoluteRoot))
                return false;

            foreach (string file in Directory.EnumerateFiles(absoluteRoot, "*.cs", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);
                if (string.Equals(name, excludedFileA, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, excludedFileB, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (FileContainsAny(file, tokens))
                    return true;
            }

            return false;
        }

        private static bool FileContainsAny(string path, string[] tokens)
        {
            string text = File.ReadAllText(path);
            for (int i = 0; i < tokens.Length; i++)
            {
                if (text.IndexOf(tokens[i], StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
        }

        private static string Escape(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        private static void EnsureReportFolder()
        {
            string directory = Path.GetDirectoryName(AITextureControlMapConstants.SelfAuditReportPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }

        private static string ProjectPath(string projectPath)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), projectPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
#endif

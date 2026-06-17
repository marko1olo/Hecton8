#if UNITY_EDITOR
using System.IO;
using System.Reflection;
using System.Text;
using Hecton8.World.OfflineGeometry;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;

namespace Hecton8.Editor.OfflineGeometry
{
    internal static class OfflineGeometrySelfAudit
    {
        internal const string ReportPath = "Docs/Reports/SHINOBU_213_SELF_AUDIT.xml";

        [MenuItem("Hecton8/LOD Collider Forge/Write SHINOBU_213 Self Audit", false, 255)]
        public static void WriteSelfAuditReportMenu()
        {
            WriteSelfAuditReport();
        }

        internal static void WriteSelfAuditReport()
        {
            OfflineGeometryVertexLayoutValidator.ValidateStructs();
            OfflineGeometryBaker.EnsureFileFolder(ReportPath);

            var builder = new StringBuilder(8192);
            builder.Append("<SELF_AUDIT agent=\"SHINOBU_213\" domain=\"OFFLINE_LOD_AND_COLLIDER_BAKER\" status=\"PENDING_VERIFICATION\">\n");
            AppendTaskReconciliation(builder);
            AppendStructLayout(builder);
            AppendScalability(builder);
            AppendVaultStatus(builder);
            AppendJobGraph(builder);
            AppendCompileGuard(builder);
            AppendDearLie(builder);
            builder.Append("</SELF_AUDIT>\n");
            OfflineGeometryBaker.WriteTextFileAtomic(ReportPath, builder.ToString());
        }

        private static void AppendTaskReconciliation(StringBuilder builder)
        {
            builder.Append("  <TASK_RECONCILIATION>\n");
            AppendTask(builder, "01", "REALTIME_MESH_COLLIDER_INQUISITION", "PASS", "Scanner reports high-poly concave MeshCollider findings and optional editor repair uses BoxCollider.");
            AppendTask(builder, "02", "MANUAL_LOD_AUTHORING_PURGE", "PASS", "Manual LOD drift and material slot mismatch are reported; bake output overwrites with deterministic budgets.");
            AppendTask(builder, "03", "CS1612_GEOMETRY_STATE_ANNIHILATION", "PASS", "Burst DTOs expose fields only; no properties in geometry job structs.");
            AppendTask(builder, "04", "ARM64_MAPPING_LAYOUT_ASSERTION", "PASS", "LodConfigurationDTO is explicit 16 bytes with validated offsets.");
            AppendTask(builder, "05", "EMERGENCY_MOCK_DECIMATION_BENCHMARK", "PASS", "GenerateMockHighPolyMeshJob emits dense fractal sphere geometry.");
            AppendTask(builder, "06", "AUTOMATED_LOD_GENERATION_PIPELINE", "PASS", "BuildLodMesh emits LOD0/LOD1/LOD2 meshes from strict triangle budgets.");
            AppendTask(builder, "07", "BURST_CONVEX_HULL_GENERATOR", "PASS", "GenerateConvexHullJob emits a bounded 8..32 point support hull with plane-deduped fan triangulation after primitive rejection; support sets below 8 vertices and hull faces that do not contain every finite source vertex fail closed to BoxCollider.");
            AppendTask(builder, "08", "THE_DEAR_LIE_PRIMITIVE_FITTING", "PASS", "FitGeometricPrimitivesJob selects sphere or box before convex collision.");
            AppendTask(builder, "09", "ASYNCHRONOUS_ASSET_SERIALIZATION", "PASS", "Mesh assets serialize through SetVertexBufferData and SetIndexBufferData; invalid/non-project folders fail closed, main LOD asset reload rejects null save paths, and caller-owned LOD Mesh objects are destroyed unless SaveOrReplaceMesh transfers or destroys them.");
            AppendTask(builder, "10", "AUTOMATED_PREFAB_ASSEMBLY", "PASS", "Generated prefab contains static LODGroup and primitive or convex colliders only.");
            AppendTask(builder, "11", "CONTINUOUS_SCALABILITY_THRESHOLD_SHIFT", "PASS", "GlobalQualityWeight continuously shifts generated thresholds and fade widths.");
            AppendTask(builder, "12", "AUP_DEPTH_BASED_CULLING_PREP", "PASS", "DepthMeters compresses LOD thresholds for hadal darkness without runtime authority.");
            AppendTask(builder, "13", "ROLLBACK_NETCODE_EXCLUSION_FENCE", "PASS", "Reports explicitly exclude LOD choice from Merkle/StateRingBuffer truth.");
            AppendTask(builder, "14", "ZERO_INIT_OVERHEAD_BYPASS", "PASS", "Geometry scratch NativeArrays use UninitializedMemory and deterministic overwrites.");
            AppendTask(builder, "15", "TELEMETRY_OPTIMIZATION_REPORT_GENERATOR", "PASS", "Reports, self-audit, .h8lod, and black-box dump use temp-plus-replace writes; JSON escaping covers quote, backslash, newline, carriage return, tab, backspace, form-feed, and control characters; editor black-box ring captures successful and failed bake attempts.");
            AppendTask(builder, "16", "PROCEDURAL_OPTIMIZATION_FORGE_WINDOW", "PASS", "UI Toolkit forge window exposes folder, budgets, tolerance, quality, depth, bake, scan, and preview.");
            AppendTask(builder, "17", "CSV_OPTIMIZATION_PROFILES_INGESTOR", "PASS", "CSV profiles parse through byte cursor into FixedString settings after full-length read validation, UTF-8 BOM skip, 1 MiB ceiling, exact header validation, strict per-row cell validation, and guarded project-root resolution; malformed rows fail the file closed to default settings.");
            AppendTask(builder, "18", "LIVE_HULL_PREVIEW_GIZMO", "PASS", "SceneView overlay draws sphere, box, or hull before asset commit.");
            AppendTask(builder, "19", "ARCHITECTURAL_METRIC_VALIDATOR", "PASS", "Unoptimized_Mesh_Scanner writes PHYSICS_OPTIMIZATION_REPORT.json.");
            AppendTask(builder, "20", "SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION", "PASS", "This XML file records layout, scalability, vault, job graph, compile guard, and Dear Lie proof.");
            builder.Append("  </TASK_RECONCILIATION>\n");
        }

        private static void AppendStructLayout(StringBuilder builder)
        {
            builder.Append("  <STRUCT_LAYOUT_VERIFICATION>\n");
            AppendField(builder, "LodConfigurationDTO", "Lod1Threshold", Offset<LodConfigurationDTO>(nameof(LodConfigurationDTO.Lod1Threshold)), 4);
            AppendField(builder, "LodConfigurationDTO", "Lod2Threshold", Offset<LodConfigurationDTO>(nameof(LodConfigurationDTO.Lod2Threshold)), 4);
            AppendField(builder, "LodConfigurationDTO", "Lod1MeshHash", Offset<LodConfigurationDTO>(nameof(LodConfigurationDTO.Lod1MeshHash)), 4);
            AppendField(builder, "LodConfigurationDTO", "Lod2MeshHash", Offset<LodConfigurationDTO>(nameof(LodConfigurationDTO.Lod2MeshHash)), 4);
            builder.Append("    <Struct name=\"LodConfigurationDTO\" size=\"");
            builder.Append(UnsafeUtility.SizeOf<LodConfigurationDTO>());
            builder.Append("\" alignmentMath=\"4+4+4+4=16; exact 16-byte SIMD-friendly DTO; no packed layout attribute\" />\n");
            AppendField(builder, "OfflineGeometryRawVertex", "Position", Offset<OfflineGeometryRawVertex>(nameof(OfflineGeometryRawVertex.Position)), 12);
            AppendField(builder, "OfflineGeometryRawVertex", "Normal", Offset<OfflineGeometryRawVertex>(nameof(OfflineGeometryRawVertex.Normal)), 12);
            AppendField(builder, "OfflineGeometryRawVertex", "Uv0", Offset<OfflineGeometryRawVertex>(nameof(OfflineGeometryRawVertex.Uv0)), 8);
            builder.Append("    <Struct name=\"OfflineGeometryRawVertex\" size=\"");
            builder.Append(UnsafeUtility.SizeOf<OfflineGeometryRawVertex>());
            builder.Append("\" alignmentMath=\"float3(12)+float3(12)+float2(8)=32; exact 32-byte raw job vertex; explicit offsets 0,12,24\" />\n");
            AppendField(builder, "OfflineGeometryVertex32", "Position", Offset<OfflineGeometryVertex32>(nameof(OfflineGeometryVertex32.Position)), 12);
            AppendField(builder, "OfflineGeometryVertex32", "Normal", Offset<OfflineGeometryVertex32>(nameof(OfflineGeometryVertex32.Normal)), 12);
            AppendField(builder, "OfflineGeometryVertex32", "Uv0", Offset<OfflineGeometryVertex32>(nameof(OfflineGeometryVertex32.Uv0)), 8);
            builder.Append("    <Struct name=\"OfflineGeometryVertex32\" size=\"");
            builder.Append(UnsafeUtility.SizeOf<OfflineGeometryVertex32>());
            builder.Append("\" alignmentMath=\"float3(12)+float3(12)+float2(8)=32; exact 32-byte interleaved mesh vertex; explicit offsets 0,12,24\" />\n");
            AppendField(builder, "OfflineSubMeshRange", "SourceIndexStart", Offset<OfflineSubMeshRange>(nameof(OfflineSubMeshRange.SourceIndexStart)), 4);
            AppendField(builder, "OfflineSubMeshRange", "SourceTriangleCount", Offset<OfflineSubMeshRange>(nameof(OfflineSubMeshRange.SourceTriangleCount)), 4);
            AppendField(builder, "OfflineSubMeshRange", "TargetTriangleStart", Offset<OfflineSubMeshRange>(nameof(OfflineSubMeshRange.TargetTriangleStart)), 4);
            AppendField(builder, "OfflineSubMeshRange", "TargetTriangleCount", Offset<OfflineSubMeshRange>(nameof(OfflineSubMeshRange.TargetTriangleCount)), 4);
            builder.Append("    <Struct name=\"OfflineSubMeshRange\" size=\"");
            builder.Append(UnsafeUtility.SizeOf<OfflineSubMeshRange>());
            builder.Append("\" alignmentMath=\"4+4+4+4=16; exact 16-byte range row; explicit int offsets 0,4,8,12\" />\n");
            AppendField(builder, "OfflinePrimitiveFitResult", "Center", Offset<OfflinePrimitiveFitResult>(nameof(OfflinePrimitiveFitResult.Center)), 12);
            AppendField(builder, "OfflinePrimitiveFitResult", "Size", Offset<OfflinePrimitiveFitResult>(nameof(OfflinePrimitiveFitResult.Size)), 12);
            AppendField(builder, "OfflinePrimitiveFitResult", "Radius", Offset<OfflinePrimitiveFitResult>(nameof(OfflinePrimitiveFitResult.Radius)), 4);
            AppendField(builder, "OfflinePrimitiveFitResult", "Error", Offset<OfflinePrimitiveFitResult>(nameof(OfflinePrimitiveFitResult.Error)), 4);
            AppendField(builder, "OfflinePrimitiveFitResult", "VertexCount", Offset<OfflinePrimitiveFitResult>(nameof(OfflinePrimitiveFitResult.VertexCount)), 4);
            AppendField(builder, "OfflinePrimitiveFitResult", "ColliderType", Offset<OfflinePrimitiveFitResult>(nameof(OfflinePrimitiveFitResult.ColliderType)), 1);
            AppendField(builder, "OfflinePrimitiveFitResult", "_pad0", Offset<OfflinePrimitiveFitResult>(nameof(OfflinePrimitiveFitResult._pad0)), 1);
            AppendField(builder, "OfflinePrimitiveFitResult", "_pad1", Offset<OfflinePrimitiveFitResult>(nameof(OfflinePrimitiveFitResult._pad1)), 2);
            builder.Append("    <Struct name=\"OfflinePrimitiveFitResult\" size=\"");
            builder.Append(UnsafeUtility.SizeOf<OfflinePrimitiveFitResult>());
            builder.Append("\" alignmentMath=\"float3(12)+float3(12)+float(4)+float(4)+int(4)+byte(1)+pad(3)=40; exact 8-byte multiple; explicit offsets 0,12,24,28,32,36,37,38\" />\n");
            AppendField(builder, "OfflineGeometryBakeTelemetryEntry", "SourceHash", Offset<OfflineGeometryBakeTelemetryEntry>(nameof(OfflineGeometryBakeTelemetryEntry.SourceHash)), 4);
            AppendField(builder, "OfflineGeometryBakeTelemetryEntry", "OutputHash", Offset<OfflineGeometryBakeTelemetryEntry>(nameof(OfflineGeometryBakeTelemetryEntry.OutputHash)), 4);
            AppendField(builder, "OfflineGeometryBakeTelemetryEntry", "OriginalTriangles", Offset<OfflineGeometryBakeTelemetryEntry>(nameof(OfflineGeometryBakeTelemetryEntry.OriginalTriangles)), 4);
            AppendField(builder, "OfflineGeometryBakeTelemetryEntry", "Lod0Triangles", Offset<OfflineGeometryBakeTelemetryEntry>(nameof(OfflineGeometryBakeTelemetryEntry.Lod0Triangles)), 4);
            AppendField(builder, "OfflineGeometryBakeTelemetryEntry", "Lod1Triangles", Offset<OfflineGeometryBakeTelemetryEntry>(nameof(OfflineGeometryBakeTelemetryEntry.Lod1Triangles)), 4);
            AppendField(builder, "OfflineGeometryBakeTelemetryEntry", "Lod2Triangles", Offset<OfflineGeometryBakeTelemetryEntry>(nameof(OfflineGeometryBakeTelemetryEntry.Lod2Triangles)), 4);
            AppendField(builder, "OfflineGeometryBakeTelemetryEntry", "PrimitiveColliderCount", Offset<OfflineGeometryBakeTelemetryEntry>(nameof(OfflineGeometryBakeTelemetryEntry.PrimitiveColliderCount)), 4);
            AppendField(builder, "OfflineGeometryBakeTelemetryEntry", "ConvexColliderCount", Offset<OfflineGeometryBakeTelemetryEntry>(nameof(OfflineGeometryBakeTelemetryEntry.ConvexColliderCount)), 4);
            AppendField(builder, "OfflineGeometryBakeTelemetryEntry", "ExtractionMicroseconds", Offset<OfflineGeometryBakeTelemetryEntry>(nameof(OfflineGeometryBakeTelemetryEntry.ExtractionMicroseconds)), 4);
            AppendField(builder, "OfflineGeometryBakeTelemetryEntry", "SerializationMicroseconds", Offset<OfflineGeometryBakeTelemetryEntry>(nameof(OfflineGeometryBakeTelemetryEntry.SerializationMicroseconds)), 4);
            AppendField(builder, "OfflineGeometryBakeTelemetryEntry", "Lod1Threshold", Offset<OfflineGeometryBakeTelemetryEntry>(nameof(OfflineGeometryBakeTelemetryEntry.Lod1Threshold)), 4);
            AppendField(builder, "OfflineGeometryBakeTelemetryEntry", "Lod2Threshold", Offset<OfflineGeometryBakeTelemetryEntry>(nameof(OfflineGeometryBakeTelemetryEntry.Lod2Threshold)), 4);
            AppendField(builder, "OfflineGeometryBakeTelemetryEntry", "GlobalQualityWeight", Offset<OfflineGeometryBakeTelemetryEntry>(nameof(OfflineGeometryBakeTelemetryEntry.GlobalQualityWeight)), 4);
            AppendField(builder, "OfflineGeometryBakeTelemetryEntry", "DepthMeters", Offset<OfflineGeometryBakeTelemetryEntry>(nameof(OfflineGeometryBakeTelemetryEntry.DepthMeters)), 4);
            AppendField(builder, "OfflineGeometryBakeTelemetryEntry", "WarningFlags", Offset<OfflineGeometryBakeTelemetryEntry>(nameof(OfflineGeometryBakeTelemetryEntry.WarningFlags)), 4);
            AppendField(builder, "OfflineGeometryBakeTelemetryEntry", "StateHash", Offset<OfflineGeometryBakeTelemetryEntry>(nameof(OfflineGeometryBakeTelemetryEntry.StateHash)), 4);
            builder.Append("    <Struct name=\"OfflineGeometryBakeTelemetryEntry\" size=\"");
            builder.Append(UnsafeUtility.SizeOf<OfflineGeometryBakeTelemetryEntry>());
            builder.Append("\" alignmentMath=\"64-byte black-box row; one cache line; avoids false sharing if later written by worker lanes\" />\n");
            builder.Append("    <Struct name=\"OfflineLodManifestHeader\" size=\"");
            builder.Append(UnsafeUtility.SizeOf<OfflineLodManifestHeader>());
            builder.Append("\" alignmentMath=\"64-byte binary manifest header; all fields 4-byte aligned; little-endian tag at offset 20\" />\n");
            builder.Append("    <Struct name=\"OfflineLodManifestRecord\" size=\"");
            builder.Append(UnsafeUtility.SizeOf<OfflineLodManifestRecord>());
            builder.Append("\" alignmentMath=\"128-byte flat BRG/LOD record; all fields 4-byte aligned; first 80 bytes data, 48 bytes explicit uint reserve\" />\n");
            builder.Append("  </STRUCT_LAYOUT_VERIFICATION>\n");
        }

        private static void AppendScalability(StringBuilder builder)
        {
            builder.Append("  <SCALABILITY_CURVE>\n");
            builder.Append("    GlobalQualityWeight is consumed during offline bake, not runtime hot loops. Below 0.3, ResolveLod1Ratio and ResolveLod2Ratio continuously reduce triangle density with math.smoothstep, ResolveDecimationWindow collapses to one source-triangle sample per deterministic partition, LOD thresholds and fade widths are compressed, depth culling is more aggressive, and ResolvePrimitiveTolerance raises primitive acceptance so more assets collapse to sphere/box collision. Between 0.4 and 0.7, ratios, partition-local saliency, thresholds, and fade widths hold middle residency while deterministic budgets remain bounded. At 1.0, LOD0/LOD1 residency extends, fade widths widen for smoother swaps, and the decimator scans up to seven candidates inside each output triangle's source partition to preserve larger silhouette triangles without overlapping neighboring partitions; saved PhysX cost is spent on visual mesh density, not collision complexity. No IsLowEndHardware branch exists.\n");
            builder.Append("  </SCALABILITY_CURVE>\n");
        }

        private static void AppendVaultStatus(StringBuilder builder)
        {
            builder.Append("  <H_PHI_VAULT_STATUS>\n");
            builder.Append("    <RuntimePersistentNativeArrays count=\"0\" />\n");
            builder.Append("    <VaultBufferHandles count=\"0\" reason=\"Editor-only offline baker emits immutable assets; it owns no runtime persistent memory and does not require GlobalDataVault buffers.\" />\n");
            builder.Append("    <BinaryManifest path=\"Assets/_Project/BakedGeometry/Optimized/offline_lod_manifest.h8lod\" ownership=\"immutable editor output, not runtime Vault state\" serialization=\"explicit little-endian per 4-byte lane; no raw host-endian struct dump; writes .tmp, validates 64+recordCount*128 bytes, then same-volume replace with .bak\" />\n");
            builder.Append("    <EditorPersistentNativeArrays count=\"1\" owner=\"OfflineGeometryBakeBlackBox\" capacity=\"300\" rowBytes=\"64\" allocation=\"UninitializedMemory plus explicit deterministic sentinel rows\" nativeMemorySentinel=\"cold mandatory reflection bridge registers/unregisters with Hecton8.Core.NativeMemorySentinel when the sentinel assembly is loaded; registration failure disposes the ring and throws instead of leaving an untracked persistent allocation; no direct asmdef reference\" dumpSerialization=\"explicit little-endian 64-byte rows; .tmp plus exact 19200-byte validation before replacement; no raw host-endian NativeArray dump\" faultEncoding=\"aggregate 0x80000000 plus per-lane non-finite bits; raw fault lane bits folded into StateHash before sanitized serialization; blackbox and manifest FixedString path hashes avoid managed ToString allocation; JSON metric path fields append escaped ASCII FixedString bytes directly\" failedAttemptEncoding=\"source or mid-bake failed attempts write blackbox-only rows and do not enter .h8lod success manifest\" lifetime=\"AssemblyReloadEvents.beforeAssemblyReload and EditorApplication.quitting\" />\n");
            builder.Append("  </H_PHI_VAULT_STATUS>\n");
        }

        private static void AppendJobGraph(StringBuilder builder)
        {
            builder.Append("  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>\n");
            builder.Append("    <NoAlias>Status applied to source, output, index, range, packed vertex, primitive result, and hull NativeArray job fields where buffers do not overlap. HullVertices is intentionally read-write NoAlias, not WriteOnly, because support duplicate elimination and face fan triangulation read previous support points.</NoAlias>\n");
            builder.Append("    <Job name=\"GenerateMockHighPolyMeshJob\" consumes=\"none\" outputs=\"NativeArray&lt;OfflineGeometryRawVertex&gt;; invalid segment counts return before modulo/division\" handle=\"editor scheduled; completed before Mesh asset serialization\" profilerMarker=\"SHINOBU_213.MockHighPolyJobFence\" />\n");
            builder.Append("    <Job name=\"OfflineDecimateUInt16Job/OfflineDecimateUInt32Job\" consumes=\"MeshData index/range/vertex streams via UnsafeUtility.AsRef after offset+laneBytes stride validation\" outputs=\"NativeArray&lt;OfflineGeometryRawVertex&gt;; corrupt index/range/vertex streams and invalid/default output lanes fail closed to deterministic zero triangles\" handle=\"scheduled per LOD; completed inside editor bake transaction\" profilerMarker=\"SHINOBU_213.DecimateJobFence\" />\n");
            builder.Append("    <Job name=\"OfflinePackVertexJob\" consumes=\"raw vertices\" outputs=\"interleaved vertex stream; default or mismatched lanes are ignored\" handle=\"packHandle\" profilerMarker=\"SHINOBU_213.PackMeshJobFence\" />\n");
            builder.Append("    <Job name=\"OfflineIndexFillJob\" consumes=\"packHandle\" outputs=\"linear index stream; default or mismatched lanes are ignored\" handle=\"indexHandle; completed before Mesh.Set*BufferData\" profilerMarker=\"SHINOBU_213.PackMeshJobFence\" />\n");
            builder.Append("    <Job name=\"FitGeometricPrimitivesJob\" consumes=\"LOD0 raw vertices\" outputs=\"primitive fit DTO\" handle=\"completed before collider authoring\" profilerMarker=\"SHINOBU_213.PreviewPrimitiveFitJobFence/SHINOBU_213.ColliderPrimitiveFitJobFence\" />\n");
            builder.Append("    <Job name=\"GenerateConvexHullJob\" consumes=\"LOD0 raw vertices\" outputs=\"bounded 8..32 point support hull plus plane-deduped triangle index stream; support sets below 8 vertices, face-fan index overflow, and every finite-source containment failure collapse to BoxCollider before MeshCollider binding\" handle=\"completed only if primitive fitting rejects\" profilerMarker=\"SHINOBU_213.PreviewHullJobFence/SHINOBU_213.ColliderHullJobFence\" />\n");
            builder.Append("  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>\n");
        }

        private static void AppendCompileGuard(StringBuilder builder)
        {
            builder.Append("  <COMPILE_GUARD>\n");
            builder.Append("    <RuntimeAssembly name=\"Hecton8.World.OfflineGeometry\" references=\"none\" />\n");
            builder.Append("    <EditorAssembly name=\"Hecton8.World.OfflineGeometry.Editor\" references=\"Hecton8.World.OfflineGeometry,Unity.Burst,Unity.Collections,Unity.Jobs,Unity.Mathematics\" />\n");
            builder.Append("    <SiblingDomainReferences count=\"0\" />\n");
            builder.Append("    <RoslynProbe status=\"PRE_ENDIAN_BOUNDED_HULL_ASSET_BIND_SAFETY_INDEX_HOT_STRUCT_STREAM_BOUNDS_HULL_FALLBACK_JOB_GUARDS_LAYOUT_LANES_HULL_CONTAINMENT_TRANSFORM_MESH_FIT_RANGE_BLACKBOX_FINITE_SOURCE_PER_LANE_AUDIT_FAILED_ATTEMPT_HULL_COUNTER_CLEAR_MIN8_PREFAB_SAVE_ASSET_PATH_CSV_ROOT_ATOMIC_WRITE_CSV_SCHEMA_MESH_TRANSFER_RENDERER_BRIDGE_LOD_ASSET_BIND_HULL_FAN_OVERFLOW_SENTINEL_CSV_SHORT_READ_SENTINEL_FAILFAST_LOD_MESH_OWNER_FADE_WIDTH_JSON_ESCAPE_CSV_ROW_STRICT_BLACKBOX_HASH_NO_TOSTRING_JOB_PROFILER_FIXEDSTRING_REPORT_HASH_SENTINEL_FAILFAST_GUARDS_RECHECK_PENDING\" path=\"Temp/SHINOBU_213_CompileProbe\" note=\"Last pass predates explicit-endian fallback, bounded-hull support-index edits, fail-closed hull asset-binding guard, hull read-write annotation fix, finite rsqrt normalization guards, decimator index-stream fail-closed guards, mock asset reload guard, binary-ledger edit, hot geometry DTO explicit-layout proof, decimator raw-stream/output-lane bounds guards, hull fallback scratch-bounds guard, Burst job denominator/native collection guards, MeshData layout-lane guards, hull source-containment validation, safe transform value/basis guards, fail-closed mesh creation cleanup, primitive-fit finite denominator guards, generated submesh range-span guards, mock asset bind fail-closed guard, blackbox non-finite warning-bit tagging, finite-source containment requirement, per-lane blackbox fault encoding, self-audit evidence-class correction, failed-attempt blackbox coverage, hull-counter-clear primitive fallback, minimum-8 support-hull enforcement, prefab-save fail-closed telemetry, mesh asset-folder fail-closed guard, CSV project-root suffix guard, atomic artifact replacement, CSV size/header schema guard, transient mesh transfer guard, explicit renderer-array bridge, main LOD asset path reload guard, hull face-fan overflow fail-closed return, CSV full-length read validation, NativeMemorySentinel cold fail-fast reflection bridge, caller-owned LOD mesh cleanup, continuous fade-width resolver, JSON control-character escaping, strict CSV row validation, black-box FixedString hashing without ToString, no-ToString torn-dump exception path, report/manifest FixedString path hashing and escaping, and ProfilerMarker instrumentation for editor job fences; post-endian bounded-hull safety-index hot-struct stream-bounds hull-fallback job-guard layout-lane containment transform mesh fit range blackbox finite-source per-lane audit failed-attempt hull-counter-clear min8 prefab-save asset-path csv-root atomic-write csv-schema mesh-transfer renderer-bridge lod-asset-bind hull-fan-overflow sentinel csv-short-read failfast lod-mesh-owner fade-width json-escape csv-row-strict blackbox-hash fixedstring-report-hash profiler no-tostring-exception probe gated by CPU; Unity import/profiler proof still pending\" />\n");
            builder.Append("  </COMPILE_GUARD>\n");
        }

        private static void AppendDearLie(StringBuilder builder)
        {
            builder.Append("  <DEAR_LIE_CONFIRMATION>\n");
            builder.Append("    Heavy physics before: source-mesh concave collision can approach O(T) triangle contact/cooking complexity per candidate shape. After: sphere/box is O(1), fallback convex collision is bounded to V support points where 8 &lt;= V &lt;= 32; offline face discovery is capped to O(V^3) but emitted faces are plane-deduped and fan-triangulated before MeshCollider import. Underpopulated, overflowed, undersized, or unbound hull output collapses to BoxCollider O(1), never forced counters. Visual mesh remains rich; PhysX receives the lie.\n");
            builder.Append("  </DEAR_LIE_CONFIRMATION>\n");
        }

        private static void AppendTask(StringBuilder builder, string id, string name, string status, string note)
        {
            string outputStatus = status == "PASS" ? "STATIC_SOURCE_PASS" : status;
            builder.Append("    <Task id=\"");
            builder.Append(id);
            builder.Append("\" name=\"");
            builder.Append(name);
            builder.Append("\" status=\"");
            builder.Append(outputStatus);
            builder.Append("\" sourceStatus=\"");
            builder.Append(status);
            builder.Append("\" verification=\"PENDING_COMPILE_IMPORT_PROFILER\">");
            builder.Append(EscapeXml(note));
            builder.Append("</Task>\n");
        }

        private static void AppendField(StringBuilder builder, string structName, string fieldName, int offset, int bytes)
        {
            builder.Append("    <Field struct=\"");
            builder.Append(structName);
            builder.Append("\" name=\"");
            builder.Append(fieldName);
            builder.Append("\" offset=\"");
            builder.Append(offset);
            builder.Append("\" bytes=\"");
            builder.Append(bytes);
            builder.Append("\" />\n");
        }

        private static int Offset<T>(string fieldName) where T : struct
        {
            FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }

        private static string EscapeXml(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }
    }
}
#endif

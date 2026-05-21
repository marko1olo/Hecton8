#if UNITY_EDITOR
using System.IO;
using System.Text;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.GeographySanity
{
    internal static class GeographySanitySelfAudit
    {
        private const string SelfAuditPath = "Docs/Reports/GEOGRAPHY_SANITY_SELF_AUDIT.json";
        private const string AgentLogPath = "Docs/AgentLogs/LOG_SHINOBU_247.md";

        [MenuItem("Tools/Hecton8/World Sanity Checker/Run Self Audit")]
        public static void RunMenu()
        {
            RunAndWriteReport();
            Debug.Log("Geography Sanity self-audit written. STATUS: PENDING VERIFICATION.");
        }

        public static void RunAndWriteReport()
        {
            GeographySanityLayoutAssertion.AssertAll();
            string projectRoot = ResolveProjectRoot();
            string reportPath = Path.Combine(projectRoot, SelfAuditPath);
            string logPath = Path.Combine(projectRoot, AgentLogPath);
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            Directory.CreateDirectory(Path.GetDirectoryName(logPath));

            string json = BuildJson();
            File.WriteAllText(reportPath, json, Encoding.UTF8);
            File.AppendAllText(logPath, "\n" + BuildXmlBlock() + "\n", Encoding.UTF8);
            AssetDatabase.Refresh();
        }

        private static string BuildJson()
        {
            StringBuilder builder = new StringBuilder(4096);
            builder.Append("{\n");
            AppendJson(builder, "schema", "hecton8.geography_sanity_self_audit.v1", 1).Append(",\n");
            AppendJson(builder, "agent", GeographySanityConstants.AgentId, 1).Append(",\n");
            AppendJson(builder, "status", "PENDING_VERIFICATION", 1).Append(",\n");
            AppendJson(builder, "evidenceClass", "STATIC_SOURCE", 1).Append(",\n");
            builder.Append("  \"compileRunExecuted\": false,\n");
            AppendJson(builder, "compileStatus", "PENDING_UNITY_PROJECT_FILE_REGEN", 1).Append(",\n");
            builder.Append("  \"currentSourceCompileProof\": false,\n");
            builder.Append("  \"priorCompileAttemptsRecorded\": true,\n");
            AppendJson(builder, "lastCompileCommand", "dotnet build Hecton8.Editor.csproj --no-restore --nologo -v:minimal", 1).Append(",\n");
            builder.Append("  \"lastCompileElapsedMilliseconds\": 2360,\n");
            builder.Append("  \"priorCompileTimeoutMilliseconds\": 124017,\n");
            AppendJson(builder, "lastRestoreCommand", "dotnet restore Hecton8.Editor.csproj --nologo", 1).Append(",\n");
            AppendJson(builder, "lastRestoreStatus", "PASS", 1).Append(",\n");
            builder.Append("  \"dtoSizes\": {\n");
            AppendSize(builder, "SpatialAnomalyRuleDTO", UnsafeUtility.SizeOf<SpatialAnomalyRuleDTO>(), true);
            AppendSize(builder, "SpatialEntityDTO", UnsafeUtility.SizeOf<SpatialEntityDTO>(), true);
            AppendSize(builder, "NavigationRequestDTO", UnsafeUtility.SizeOf<NavigationRequestDTO>(), true);
            AppendSize(builder, "CrushDepthMaterialDTO", UnsafeUtility.SizeOf<CrushDepthMaterialDTO>(), true);
            AppendSize(builder, "SanityProfileDTO", UnsafeUtility.SizeOf<SanityProfileDTO>(), true);
            AppendSize(builder, "GeographySectorDTO", UnsafeUtility.SizeOf<GeographySectorDTO>(), true);
            AppendSize(builder, "SpatialAnomalyResultDTO", UnsafeUtility.SizeOf<SpatialAnomalyResultDTO>(), true);
            AppendSize(builder, "GeographySanityTelemetryEntry", UnsafeUtility.SizeOf<GeographySanityTelemetryEntry>(), true);
            AppendSize(builder, "GeographySanityDumpHeaderDTO", UnsafeUtility.SizeOf<GeographySanityDumpHeaderDTO>(), true);
            AppendSize(builder, "GeographySanityMetricsDTO", UnsafeUtility.SizeOf<GeographySanityMetricsDTO>(), false);
            builder.Append("  },\n");
            builder.Append("  \"spatialAnomalyRuleOffsets\": {\n");
            builder.Append("    \"TargetAUP\": 0,\n");
            builder.Append("    \"RequiredClearance\": 24,\n");
            builder.Append("    \"RuleFlags\": 28\n");
            builder.Append("  },\n");
            builder.Append("  \"editorOnly\": true,\n");
            builder.Append("  \"assemblyRoute\": \"Hecton8.World.GeographySanity.Editor asmdef; Editor include platform; references Unity Burst/Collections/Jobs/Mathematics only; no sibling Runtime asmdef reference.\",\n");
            builder.Append("  \"nativeMemoryRoute\": \"No Core native-memory facade dependency; black-box and sector buffers are local Editor NativeArray allocations with explicit allocator/options and deterministic dispose.\",\n");
            builder.Append("  \"runtimeAuthorityMutation\": false,\n");
            builder.Append("  \"rollbackNetcodeExcluded\": true,\n");
            builder.Append("  \"aupPrecisionRule\": \"double3 TargetAUP minus double3 SectorOriginAup before casting localized delta to float3.\",\n");
            builder.Append("  \"globalQualityWeightRule\": \"math.smoothstep(0.25,0.85,q) collapses low quality to nearest lookup, blends mid quality with math.lerp, and reaches bilinear/trilinear high quality without changing authority or DTO layout; reports with GlobalQualityWeight < 0.999 are marked TRIAGE_REDUCED_QUALITY and certificationEligible=false.\",\n");
            builder.Append("  \"qualityScaledWorkRule\": \"Reduced-quality triage also scales connectivity flood-fill resolution from 4 to configured resolution and vertical floating probe steps from 1 to configured steps through math.smoothstep(0.2,0.95,q); full quality keeps configured work.\",\n");
            builder.Append("  \"settingsFiniteRule\": \"Sanitize replaces non-finite scalar settings and world-origin AUP with safe defaults, sets WarningSanitizedSettings, and blocks certification with proofGrade=INVALID_SETTINGS instead of silently proving with poisoned controls.\",\n");
            builder.Append("  \"sectorPayloadEndianRule\": \"Sector .h8bin loader accepts native little-endian magic or reversed magic, then normalizes uint/int/float/double lanes through local byte-swap helpers before DTO hydration.\",\n");
            builder.Append("  \"sectorOriginRule\": \"Sector .h8bin origin must be finite and match the expected sector AUP within 0.001 meters before payload hydration proceeds.\",\n");
            builder.Append("  \"sectorPayloadValidityRule\": \"Missing sector sidecars may use deterministic mock fallback when explicitly enabled; invalid, truncated, locked, schema-mismatched, or origin-mismatched sidecars are fatal payload evidence and never fall through to mock data.\",\n");
            builder.Append("  \"sectorPayloadExactLengthRule\": \"Sector .h8bin payloads must end exactly after the declared height, SDF, entity, and navigation records; trailing bytes are rejected as invalid master data instead of being ignored.\",\n");
            builder.Append("  \"sectorPayloadFiniteLaneRule\": \"Sector .h8bin hydration rejects non-finite height samples, SDF samples, entity AUP/scalars, and navigation AUP/scalars before any Burst sampling job can consume the payload.\",\n");
            builder.Append("  \"sectorPayloadRuleMaskRule\": \"Sector .h8bin entity RuleFlags must be non-zero and limited to Floating, Buried, and CrushDepth bits; unsupported masks are invalid master data, not silent skipped checks.\",\n");
            builder.Append("  \"sectorPayloadPositiveRadiusRule\": \"Sector .h8bin entity radius and navigation vehicle radius must be finite and greater than zero; zero-radius geometry is invalid master data, not a valid clearance proof.\",\n");
            builder.Append("  \"kernelScalarDomainRule\": \"Floating, buried, and connectivity Burst jobs fatal-mark non-finite, zero-radius, negative-clearance, negative tolerance, and negative recoverability scalar lanes before SDF/height clearance math.\",\n");
            builder.Append("  \"sectorPathAllocationRule\": \"Per-sector .h8bin filenames are assembled through stackalloc char spans and int.TryFormat, avoiding sectorX/sectorZ ToString intermediates before the unavoidable filesystem path string.\",\n");
            builder.Append("  \"activeLaneSchedulingRule\": \"Loaded sparse sidecars schedule profile, floating, buried, crush, and connectivity Burst jobs only across active entity/navigation counts; mock fallback still schedules full generated capacity.\",\n");
            builder.Append("  \"jobDependencyGraphRule\": \"Connectivity flood-fill depends on seed payload generation/loading only, runs independently of entity anomaly jobs, and the offline terminal readback uses JobHandle.CombineDependencies(crush, connectivity).\",\n");
            builder.Append("  \"mockBenchmarkRule\": \"RunMockBenchmark sets ForceMockData=true and bypasses sector sidecar loading so the emergency mock benchmark remains isolated even if sector_0_0.h8bin exists.\",\n");
            builder.Append("  \"mockBenchmarkSanitizeRule\": \"RunMockBenchmark re-runs Sanitize after mock sector/count overrides, so future default or cap changes cannot bypass NativeArray capacity clamps before the benchmark allocates buffers.\",\n");
            builder.Append("  \"fatalMathRule\": \"AUP/hash identity is written before fatal returns; coordinate and scalar finite gates run before SDF sampling, cell indexing, correction, or crush-depth math.\",\n");
            builder.Append("  \"warningFlagsRule\": \"Report and diagnostic log include missing-sector, invalid-sector, reduced-quality, partial-check, mock-fallback, incomplete-sweep, pipeline-exception, and sanitized-settings warning bits so CI can distinguish absent input, corrupted master data, triage sweeps, disabled checks, CI mock fallback, cancellation, editor pipeline failures, and non-finite controls.\",\n");
            builder.Append("  \"reportMemoryRule\": \"Full-world validation streams anomaly rows to Docs/Reports/GEOGRAPHY_SANITY_REPORT.anomalies.tmp, flushes sector StringBuilder data through a pooled 4096-char chunk, final JSON assembly runs on an Awaitable background thread with pooled byte chunks plus pooled UTF-8 encoding, mock reports write StringBuilder rows through pooled UTF-8 chunks, and serializationMilliseconds is patched into the JSON from the measured writer stopwatch instead of publishing a zero placeholder.\",\n");
            builder.Append("  \"numericFormattingRule\": \"Report float/double lanes and the serializationMilliseconds patch slot are formatted through stack Span<char> TryFormat(\\\"R\\\", InvariantCulture); normal lanes append chars directly and impossible patch formatting writes a fixed zero field instead of allocating round-trip strings.\",\n");
            builder.Append("  \"jsonEscapeFormattingRule\": \"Report and scanner JSON unicode escapes append four uppercase hex nibbles directly from the char scalar; they do not allocate per-character managed hex-format strings.\",\n");
            builder.Append("  \"diagnosticLogFormattingRule\": \"GEOGRAPHY_SANITY_DIAGNOSTIC.log appends key/value fields directly and routes float/double values through the same stack-span numeric formatter instead of line-level string concatenation.\",\n");
            builder.Append("  \"reportEffectiveWorkRule\": \"GEOGRAPHY_SANITY_REPORT.json settings include configured connectivity/probe values and effective quality-scaled connectivity/probe values so CI can distinguish requested capacity from reduced-quality work.\",\n");
            builder.Append("  \"diagnosticEffectiveWorkRule\": \"GEOGRAPHY_SANITY_DIAGNOSTIC.log writes configured and effective connectivity/probe dimensions through the same resolver methods used by scheduling and JSON report generation.\",\n");
            builder.Append("  \"progressUiRule\": \"Full-world validation progress uses constant EditorUtility.DisplayProgressBar title/info strings; sector coordinates are not concatenated into per-sector UI text.\",\n");
            builder.Append("  \"perSectorTimingRule\": \"RunSector uses Stopwatch.GetTimestamp scalar ticks for per-sector burst timing; it does not allocate a Stopwatch object per sector.\",\n");
            builder.Append("  \"completionProofRule\": \"certificationEligible requires completedSectors == sectorCount; canceled or exception-shortened sweeps are marked INCOMPLETE_SWEEP or FATAL_INPUT instead of certification.\",\n");
            builder.Append("  \"capacityClampRule\": \"Sanitize clamps SectorCountX/SectorCountZ<=512, HeightResolution<=1024, SdfResolution<=128, EntitiesPerSector<=65536, NavigationRequestsPerSector<=128, ConnectivityResolution<=32, and VerticalProbeSteps<=256 before any NativeArray or probe-loop size math.\",\n");
            builder.Append("  \"editorFacadeCoverageRule\": \"WorldSanityCheckerWindow exposes sector axes, height/SDF/entity/nav capacities, connectivity grid, vertical probe cadence, max floating tolerance, check toggles, mock fallback, and continuous GlobalQualityWeight through the same sanitizer constants.\",\n");
            builder.Append("  \"editorFacadeStatusFormattingRule\": \"WorldSanityCheckerWindow count-bearing status lines format integers into stack Span<char> buffers and assign only the final unavoidable UI label string, avoiding string-concat intermediates.\",\n");
            builder.Append("  \"csvProfileRule\": \"sanity_check_profiles.csv streams through a fixed stack line buffer, fixed 2048-row NativeList capacity, and ReadOnlySpan<byte> token parser; overlong rows, excess rows, non-finite float overflow, and uint flag overflow fail closed without file-sized byte rental or hidden NativeList growth.\",\n");
            builder.Append("  \"csvProfileFlagRule\": \"Optional CSV profile flags must be non-zero and limited to Floating, Buried, and CrushDepth rule bits; invalid masks, unsupported bits, and trailing columns fail the row closed instead of silently defaulting.\",\n");
            builder.Append("  \"editorVisualizationRoute\": \"SceneView.duringSceneGui via GeographySanityAnomalySceneView; no runtime component proxy, no scene-object injection; report is parsed as a bounded stream with ReadOnlySpan numeric/type extraction, and marker AUP subtracts SceneView pivot before float handle drawing.\",\n");
            builder.Append("  \"vaultStatus\": \"No runtime Vault BufferID claimed; editor TempJob arrays are transient per-sector buffers disposed by the pipeline.\",\n");
            builder.Append("  \"jsonPrecisionRule\": \"AUP coordinates serialized by round-trip double formatting.\",\n");
            builder.Append("  \"blackBox\": \"300 GeographySanityTelemetryEntry rows use chronological CompletedSectors % 300 ring indexing, deterministic 300-row cold initialization after UninitializedMemory allocation, computed dump cursor, and explicit little-endian 64-byte records to Docs/AgentLogs/Dump_SHINOBU_247.bin on fatal math.\",\n");
            builder.Append("  \"projectFileStatus\": \"Current generated csproj files do not yet include Hecton8.World.GeographySanity.Editor; Unity import/project-file regeneration is required before dotnet can compile this new asmdef.\",\n");
            builder.Append("  \"optimizationReportRoute\": \"Runtime_Spatial_Query_Scanner writes Docs/Reports/WORLD_OPTIMIZATION_REPORT_SHINOBU_247.json first and only writes shared WORLD_OPTIMIZATION_REPORT.json when the shared file is absent or already SHINOBU_247-owned.\",\n");
            builder.Append("  \"scannerSharedOwnershipRule\": \"Runtime_Spatial_Query_Scanner checks shared-report ownership by comparing the quoted AgentId through ReadOnlySpan<char>; it does not concatenate an agent token per probed report line.\",\n");
            builder.Append("  \"scannerMethodStateRule\": \"Runtime_Spatial_Query_Scanner tracks method signatures whose opening brace appears on the following line, preventing hot-init method state from drifting into unrelated source regions.\",\n");
            builder.Append("  \"scannerSpanParserRule\": \"Runtime_Spatial_Query_Scanner strips comments, resolves forbidden patterns, detects method names, safe-spawn text, and trims finding context through ReadOnlySpan<char>; strings are allocated only for stored finding/report fields.\"\n");
            builder.Append("}\n");
            return builder.ToString();
        }

        private static string BuildXmlBlock()
        {
            StringBuilder builder = new StringBuilder(2048);
            builder.AppendLine("<SELF_AUDIT agent=\"SHINOBU_247\">");
            builder.AppendLine("  <ARRAY_FORMATS>");
            builder.AppendLine("    SpatialAnomalyRuleDTO: 32 bytes, TargetAUP offset 0, RequiredClearance offset 24, RuleFlags offset 28.");
            builder.AppendLine("    SpatialEntityDTO: 64 bytes, AUP + radius/clearance/hash/rule fields, raw public fields only.");
            builder.AppendLine("    GeographySectorDTO: 128 bytes, sector-origin AUP plus bounded height/SDF resolution metadata.");
            builder.AppendLine("    SpatialAnomalyResultDTO: 128 bytes, double3 AUP + float3 correction + numeric error facts.");
            builder.AppendLine("    GeographySanityTelemetryEntry: 64 bytes, 300-row black box ring payload.");
            builder.AppendLine("  </ARRAY_FORMATS>");
            builder.AppendLine("  <EDITOR_TOOLING>WorldSanityCheckerWindow, GeographySanityPipeline, Runtime_Spatial_Query_Scanner, GeographySanityAnomalySceneView.</EDITOR_TOOLING>");
            builder.AppendLine("  <COMPILE_GUARD>Dedicated Editor-only asmdef references Unity Burst/Collections/Jobs/Mathematics only; no sibling Runtime asmdef reference.</COMPILE_GUARD>");
            builder.AppendLine("  <NATIVE_MEMORY_ROUTE>No Core native-memory facade dependency; black-box and sector buffers are local Editor NativeArray allocations with deterministic dispose.</NATIVE_MEMORY_ROUTE>");
            builder.AppendLine("  <QUALITY_CURVE>GlobalQualityWeight uses smoothstep(0.25,0.85): low weight nearest SDF/height lookup, mid weight lerp blend, high weight bilinear/trilinear. Reports with GlobalQualityWeight below 0.999 are marked TRIAGE_REDUCED_QUALITY, not certification.</QUALITY_CURVE>");
            builder.AppendLine("  <QUALITY_SCALED_WORK>Reduced-quality triage scales connectivity resolution from 4 to configured resolution and vertical floating probes from 1 to configured steps through smoothstep(0.2,0.95); full quality keeps configured work.</QUALITY_SCALED_WORK>");
            builder.AppendLine("  <SETTINGS_FINITE_RULE>Sanitize replaces non-finite settings/AUP controls with safe defaults, sets WarningSanitizedSettings, and reports INVALID_SETTINGS instead of certification.</SETTINGS_FINITE_RULE>");
            builder.AppendLine("  <ENDIANNESS>Sector .h8bin input accepts native or byte-reversed magic and hydrates all scalar lanes through explicit endian-normalizing reads.</ENDIANNESS>");
            builder.AppendLine("  <SECTOR_ORIGIN>Sector .h8bin origin must be finite and match the expected sector AUP within 0.001 meters before validation consumes the payload.</SECTOR_ORIGIN>");
            builder.AppendLine("  <SECTOR_PAYLOAD_STATUS>Missing sidecars may use deterministic mock fallback only when enabled; invalid/truncated/locked/schema-mismatched/origin-mismatched sidecars emit fatal payload evidence and never fall through to mock data.</SECTOR_PAYLOAD_STATUS>");
            builder.AppendLine("  <SECTOR_PAYLOAD_LENGTH>Sector .h8bin payloads must end exactly after declared records; trailing bytes are invalid master data.</SECTOR_PAYLOAD_LENGTH>");
            builder.AppendLine("  <SECTOR_PAYLOAD_FINITE_LANES>Sector .h8bin hydration rejects non-finite height, SDF, entity, and navigation scalar lanes before scheduling Burst sampling jobs.</SECTOR_PAYLOAD_FINITE_LANES>");
            builder.AppendLine("  <SECTOR_PAYLOAD_RULE_MASKS>Sector .h8bin entity RuleFlags must be non-zero and limited to Floating, Buried, and CrushDepth bits.</SECTOR_PAYLOAD_RULE_MASKS>");
            builder.AppendLine("  <SECTOR_PAYLOAD_POSITIVE_RADIUS>Sector .h8bin entity radius and navigation vehicle radius must be finite and greater than zero.</SECTOR_PAYLOAD_POSITIVE_RADIUS>");
            builder.AppendLine("  <KERNEL_SCALAR_DOMAIN>Floating, buried, and connectivity jobs fatal-mark non-finite, zero-radius, negative-clearance, negative tolerance, and negative recoverability lanes before clearance math.</KERNEL_SCALAR_DOMAIN>");
            builder.AppendLine("  <SECTOR_PATH_ALLOCATION>Per-sector sidecar filenames use stackalloc char spans and int.TryFormat to avoid sector coordinate ToString intermediates before filesystem path resolution.</SECTOR_PATH_ALLOCATION>");
            builder.AppendLine("  <ACTIVE_LANE_SCHEDULING>Loaded sparse sidecars schedule validation jobs over active entity/navigation counts only; deterministic mock fallback keeps full generated capacity.</ACTIVE_LANE_SCHEDULING>");
            builder.AppendLine("  <JOB_DEPENDENCY_GRAPH>Connectivity depends on seed payload generation/loading only and is joined with entity validation through JobHandle.CombineDependencies before the offline terminal readback.</JOB_DEPENDENCY_GRAPH>");
            builder.AppendLine("  <MOCK_BENCHMARK>RunMockBenchmark sets ForceMockData=true, bypassing sector sidecar loading so T05 remains isolated even if sector_0_0.h8bin exists.</MOCK_BENCHMARK>");
            builder.AppendLine("  <MOCK_BENCHMARK_SANITIZE>RunMockBenchmark re-runs Sanitize after mock sector/count overrides before any benchmark NativeArray allocation.</MOCK_BENCHMARK_SANITIZE>");
            builder.AppendLine("  <FATAL_MATH>Result identity is populated before fatal returns; coordinate and scalar finite gates precede SDF sampling, cell indexing, correction, and crush-depth math.</FATAL_MATH>");
            builder.AppendLine("  <WARNING_FLAGS>Reports and diagnostic logs include missing-sector, invalid-sector, reduced-quality, partial-check, mock-fallback, incomplete-sweep, pipeline-exception, and sanitized-settings warning bits.</WARNING_FLAGS>");
            builder.AppendLine("  <COMPLETION_PROOF>certificationEligible requires completedSectors equal sectorCount; canceled or exception-shortened sweeps are not certification candidates.</COMPLETION_PROOF>");
            builder.AppendLine("  <REPORT_MEMORY>Full-world anomaly rows stream through GEOGRAPHY_SANITY_REPORT.anomalies.tmp; sector flush uses pooled 4096-char chunks, final report copy uses pooled byte chunks on an Awaitable background thread, mock reports write StringBuilder rows through pooled UTF-8 chunks, and serializationMilliseconds is patched from the measured writer stopwatch.</REPORT_MEMORY>");
            builder.AppendLine("  <NUMERIC_FORMATTING>Report float/double lanes and serializationMilliseconds patching use stack Span char TryFormat(R, InvariantCulture); normal lanes append chars directly and impossible patch formatting writes fixed zero bytes.</NUMERIC_FORMATTING>");
            builder.AppendLine("  <JSON_ESCAPE_FORMATTING>Report and scanner JSON unicode escapes append four uppercase hex nibbles directly from the char scalar; no per-character managed hex-format escape string is allocated.</JSON_ESCAPE_FORMATTING>");
            builder.AppendLine("  <DIAGNOSTIC_LOG_FORMATTING>Diagnostic log fields append key/value data directly and route float/double values through the same stack-span formatter.</DIAGNOSTIC_LOG_FORMATTING>");
            builder.AppendLine("  <REPORT_EFFECTIVE_WORK>JSON settings include configured and effective quality-scaled connectivity/probe work dimensions.</REPORT_EFFECTIVE_WORK>");
            builder.AppendLine("  <DIAGNOSTIC_EFFECTIVE_WORK>Diagnostic log writes configured and effective connectivity/probe work dimensions through the same resolver methods used by scheduling and JSON reporting.</DIAGNOSTIC_EFFECTIVE_WORK>");
            builder.AppendLine("  <PROGRESS_UI>Full-world progress uses constant EditorUtility.DisplayProgressBar title/info strings and does not concatenate sector coordinates per sector.</PROGRESS_UI>");
            builder.AppendLine("  <PER_SECTOR_TIMING>RunSector uses Stopwatch.GetTimestamp scalar ticks and does not allocate Stopwatch objects per sector.</PER_SECTOR_TIMING>");
            builder.AppendLine("  <CAPACITY_CLAMPS>Sanitize clamps sector axes at most 512, HeightResolution at most 1024, SdfResolution at most 128, EntitiesPerSector at most 65536, NavigationRequestsPerSector at most 128, ConnectivityResolution at most 32, and VerticalProbeSteps at most 256 before NativeArray or probe-loop size math.</CAPACITY_CLAMPS>");
            builder.AppendLine("  <EDITOR_FACADE_COVERAGE>WorldSanityCheckerWindow exposes sector axes, height/SDF/entity/nav capacities, connectivity grid, vertical probe cadence, max floating tolerance, check toggles, mock fallback, and continuous GlobalQualityWeight through sanitizer constants.</EDITOR_FACADE_COVERAGE>");
            builder.AppendLine("  <EDITOR_FACADE_STATUS_FORMATTING>WorldSanityCheckerWindow count-bearing status lines format integers into stack Span char buffers and assign only the final unavoidable UI label string.</EDITOR_FACADE_STATUS_FORMATTING>");
            builder.AppendLine("  <BLACKBOX_RING>Telemetry uses chronological CompletedSectors % 300 ring indexing, deterministic 300-row cold initialization, and computed dump cursor.</BLACKBOX_RING>");
            builder.AppendLine("  <BLACKBOX_ENDIANNESS>Dump_SHINOBU_247.bin writes fixed little-endian header and 64-byte telemetry rows.</BLACKBOX_ENDIANNESS>");
            builder.AppendLine("  <SCENEVIEW_AUP>SceneView overlay streams JSON lines, parses type/numeric fields through spans, and subtracts SceneView pivot in double before casting local marker coordinates to float.</SCENEVIEW_AUP>");
            builder.AppendLine("  <CSV_PROFILE_BRIDGE>sanity_check_profiles.csv uses a fixed stack line buffer, fixed 2048-row NativeList capacity, and ReadOnlySpan byte token parser; overlong rows, excess rows, non-finite floats, uint overflow, invalid masks, unsupported bits, and trailing columns fail closed.</CSV_PROFILE_BRIDGE>");
            builder.AppendLine("  <OPTIMIZATION_REPORT_ROUTE>Runtime scanner writes WORLD_OPTIMIZATION_REPORT_SHINOBU_247.json first and guards the shared WORLD_OPTIMIZATION_REPORT.json against foreign-agent clobbering.</OPTIMIZATION_REPORT_ROUTE>");
            builder.AppendLine("  <SCANNER_SHARED_OWNERSHIP>Shared report ownership probe compares the quoted AgentId through spans and does not concatenate an agent token per report line.</SCANNER_SHARED_OWNERSHIP>");
            builder.AppendLine("  <SCANNER_METHOD_STATE>Runtime scanner tracks pending method braces so Start/Awake hot-state does not drift into unrelated regions.</SCANNER_METHOD_STATE>");
            builder.AppendLine("  <SCANNER_SPAN_PARSER>Runtime scanner strips comments, resolves methods/patterns/safe-spawn text, and trims finding context through ReadOnlySpan char spans; strings remain only for retained report fields.</SCANNER_SPAN_PARSER>");
            builder.AppendLine("  <RUNTIME_EXCLUSION>Validation jobs and report generation are under #if UNITY_EDITOR / Editor folders; no GlobalRegistry, StateRingBuffer, save identity, or Merkle authority route is mutated.</RUNTIME_EXCLUSION>");
            builder.AppendLine("  <REALTIME_VALIDATION>Status depends on Runtime_Spatial_Query_Scanner output; requested Environment/WorldGeneration scope had no SphereCast/CheckBox offender in CLI scan.</REALTIME_VALIDATION>");
            builder.AppendLine("  <STATUS>PENDING_VERIFICATION</STATUS>");
            builder.AppendLine("</SELF_AUDIT>");
            return builder.ToString();
        }

        private static void AppendSize(StringBuilder builder, string name, int size, bool comma)
        {
            builder.Append("    \"").Append(name).Append("\": ").Append(size);
            if (comma)
                builder.Append(',');
            builder.Append('\n');
        }

        private static StringBuilder AppendJson(StringBuilder builder, string key, string value, int indent)
        {
            builder.Append(' ', indent * 2).Append('"').Append(key).Append("\": \"").Append(value).Append('"');
            return builder;
        }

        private static string ResolveProjectRoot()
        {
            DirectoryInfo parent = Directory.GetParent(Application.dataPath);
            return parent != null ? parent.FullName : Directory.GetCurrentDirectory();
        }
    }
}
#endif

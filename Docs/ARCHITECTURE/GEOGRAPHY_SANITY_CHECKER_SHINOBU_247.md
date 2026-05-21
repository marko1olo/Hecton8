# SHINOBU_247 Geography Sanity Checker Route Card

Status: STATIC_SOURCE / PENDING UNITY VERIFICATION
Owner: SHINOBU_247 / GEOGRAPHY_SANITY_CHECKER
Date: 2026-05-21

## Boundary

The system is an Editor-only offline validator for the 100 km world. It reads sector height/SDF payloads or deterministic mock sector data, evaluates object AUP against master geometry, and writes reports. It does not publish runtime signals, mutate save state, enter rollback state, or own gameplay truth.

Source boundary:

- `Assets/_Project/Scripts/Editor/GeographySanity/`
- `Assets/_Project/Scripts/Editor/GeographySanity/Hecton8.World.GeographySanity.Editor.asmdef`

Assembly route:

- Include platform: Editor only.
- References: Unity Burst, Collections, Jobs, Mathematics only.
- No direct sibling Runtime assembly reference is introduced.
- No GlobalRegistry, HectonEventBus, GlobalSignals, StateRingBuffer, or GlobalDataVault hot route is introduced.

## Payloads And Reports

Input route:

- Optional sector sidecars: `Assets/StreamingAssets/Hecton8/WorldSectors/sector_x_z.h8bin`.
- Missing sectors can be filled by `GenerateMockSpatialAnomaliesJob` for CI/offline fallback.
- Invalid, truncated, locked, schema-mismatched, endian-incoherent, origin-mismatched, exact-length-mismatched, non-finite, zero-radius, or unsupported-rule-mask sector sidecars are fatal payload evidence. They set `WarningInvalidSectorPayload`, emit a `FATAL_MATH_ERROR` anomaly at the sector AUP, write black-box telemetry, and never fall through to mock data.
- Per-sector sidecar filename construction uses stackalloc char spans plus `int.TryFormat`, avoiding coordinate `ToString` intermediates before the unavoidable filesystem path string.
- Data Monolith readiness is not claimed. This does not prove `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`.

Human tuning route:

- `Assets/StreamingAssets/Hecton8/WorldSanity/sanity_check_profiles.csv`.
- CSV rows are streamed through a fixed stack line buffer, parsed as `ReadOnlySpan<byte>` tokens, and stored in a fixed `2048` row `NativeList` capacity. Overlong rows, excess rows, non-finite float overflow, overflowing uint flag tokens, zero/unsupported rule masks, and trailing columns fail closed; no full-file byte rental, `string.Split`, `float.Parse`, or managed dictionary route is used.

Editor facade route:

- `WorldSanityCheckerWindow` exposes check toggles, mock fallback, continuous `GlobalQualityWeight`, sector axes, height/SDF/entity/nav capacities, connectivity grid, vertical probe cadence, and max floating tolerance.
- The facade clamps through the same constants as the pipeline sanitizer. The pipeline remains the authoritative gate for programmatic callers.
- Count-bearing status lines format integers into stack `Span<char>` buffers and assign only the final unavoidable UI label string; mock benchmark, CSV load, and scanner result status paths no longer build concat intermediates.

Output route:

- `Docs/Reports/GEOGRAPHY_SANITY_REPORT.json`
- `Docs/Reports/GEOGRAPHY_SANITY_REPORT.anomalies.tmp` during full-world serialization only; deleted after final report write.
- `Docs/Reports/GEOGRAPHY_SANITY_SELF_AUDIT.json`
- `Docs/Reports/WORLD_OPTIMIZATION_REPORT_SHINOBU_247.json`
- `Docs/Reports/WORLD_OPTIMIZATION_REPORT.json`
- `Docs/AgentLogs/GEOGRAPHY_SANITY_REPORT.log`
- `Docs/AgentLogs/Dump_SHINOBU_247.bin`

The JSON report and diagnostic log include `warningFlags`. `WarningMissingSectorPayload` marks absent sidecars when mock fallback is disabled; `WarningInvalidSectorPayload` marks present-but-invalid master data. CI can therefore distinguish missing upstream data from corrupted upstream data.

Full-world anomaly output streams sector rows into the temporary anomaly file and then copies those rows into the final JSON report. The pipeline does not retain one world-sized anomaly `StringBuilder`.

The SceneView anomaly overlay reloads the report through a bounded stream, caps records at `4096`, resolves type codes from `ReadOnlySpan<char>`, parses AUP doubles from spans, and subtracts the SceneView pivot in double before any float handle drawing. It does not allocate substring tokens per anomaly record.

Sector anomaly flushing writes the current `StringBuilder` through a pooled 4096-character chunk using `StringBuilder.CopyTo` and `StreamWriter.Write(char[], int, int)`, avoiding one sector-sized string allocation per flush.

Final report assembly runs on an `Awaitable.BackgroundThreadAsync` lane. It does not await `CopyToAsync`/`WriteAsync`; it copies the temporary anomaly stream through a pooled byte chunk and writes header/tail UTF-8 bytes through a pooled encoder buffer before returning to the main thread. `serializationMilliseconds` is patched into a fixed-width JSON slot from the measured writer stopwatch, so the JSON header and diagnostic log share the same non-placeholder timing value.

JSON string unicode escaping in the main report and runtime spatial-query scanner appends `\u` plus four direct uppercase hex nibbles from the source `char`. It does not allocate per-character managed hex-format escape strings.

The mock benchmark report path no longer builds one full report string. It writes the header, pooled UTF-8 chunks from the anomaly `StringBuilder`, and the tail directly to the output `FileStream`, then patches the same measured `serializationMilliseconds` slot.

`GEOGRAPHY_SANITY_REPORT.json` writes both configured and effective quality-scaled connectivity/probe settings. `GEOGRAPHY_SANITY_DIAGNOSTIC.log` mirrors those effective values through the same resolver methods used by scheduling. CI can therefore distinguish requested sweep capacity from reduced-quality triage work in both report and run log artifacts.

Full-world progress UI uses constant `EditorUtility.DisplayProgressBar` title/info strings. Sector coordinates are retained in reports and logs, not concatenated into per-sector progress text.

Per-sector burst timing uses `Stopwatch.GetTimestamp()` scalar ticks and a static elapsed-millisecond conversion. The sector loop does not allocate a `Stopwatch` object for every sector.

Sector `.h8bin` input accepts native little-endian magic or reversed magic and normalizes `uint`, `int`, `float`, and `double` scalar lanes before DTO hydration. The local reverse-byte helpers are kept inside the Editor route to avoid adding a runtime binary dependency.

Sector `.h8bin` origin is not advisory. It must be finite and match the expected sector AUP within `0.001` meters before payload hydration continues. Mismatched payload frames fail closed to mock/fallback or warning behavior instead of validating against the wrong AUP anchor.

Sector `.h8bin` v1 payloads are exact-length records. After the declared height, SDF, entity, and navigation rows are consumed, any trailing byte makes the sidecar invalid master data.

Sector `.h8bin` scalar lanes are validated before Burst jobs consume them. Height samples, SDF samples, entity AUP/scalars, and navigation AUP/scalars must be finite. Entity radius and navigation vehicle radius must be strictly positive. Entity rule masks must be non-zero and limited to `RuleCheckFloating`, `RuleCheckBuried`, and `RuleCheckCrushDepth`; unsupported masks are not silently masked or defaulted.

Floating, buried, and connectivity Burst kernels repeat the scalar-domain fence at execution time: non-finite, zero-radius, negative-clearance, negative tolerance, and negative recoverability lanes fatal-mark the row before SDF/height clearance math runs.

The loader uses a three-state result: `Missing`, `Loaded`, or `Invalid`. Only `Missing` may use deterministic mock fallback, and only when the window setting allows it. `Invalid` covers truncated streams, IO/permission denial, schema/count mismatch, unsupported version, origin mismatch, trailing bytes, non-finite scalar lanes, zero entity/navigation radii, and unsupported entity rule masks; it is treated as fatal payload evidence.

The mock benchmark route sets `ForceMockData=true`; it bypasses `.h8bin` sidecar loading so Task 05 remains isolated even if `sector_0_0.h8bin` exists on disk.

## DTO Layouts

Primary rule DTO:

- `SpatialAnomalyRuleDTO = 32` bytes.
- `TargetAUP @0`: `double3`, 24 bytes, 8-byte aligned.
- `RequiredClearance @24`: `float`, 4 bytes.
- `RuleFlags @28`: `uint`, 4 bytes.
- Total: `24 + 4 + 4 = 32`, exact 32-byte lane, no `Pack=1`.

Other native rows:

- `SpatialEntityDTO = 64`
- `NavigationRequestDTO = 64`
- `CrushDepthMaterialDTO = 32`
- `SanityProfileDTO = 32`
- `GeographySectorDTO = 128`
- `SpatialAnomalyResultDTO = 128`
- `GeographySanityTelemetryEntry = 64`
- `GeographySanityDumpHeaderDTO = 32`
- `GeographySanityMetricsDTO = 128`

`GeographySanityLayoutAssertion` verifies sizes and the primary DTO offsets with `UnsafeUtility.SizeOf<T>()` and `Marshal.OffsetOf`.

## Job Graph

The editor pipeline schedules pure Burst/data-local jobs and completes only at the offline terminal readback point:

- `GenerateMockSpatialAnomaliesJob`
- `ApplySanityProfilesJob`
- `EvaluateFloatingAnomaliesJob`
- `EvaluateBuriedAnomaliesJob`
- `ValidateCrushDepthLimitsJob`
- `EvaluateNavigationalConnectivityJob`

Every math job uses:

- `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]`
- `[NoAlias]` on non-overlapping pointer fields.
- Raw unmanaged DTO fields, no hot-path DTO properties.

Loaded sparse sidecars schedule profile, floating, buried, crush-depth, and connectivity jobs over active entity/navigation counts only. Deterministic mock fallback still schedules full generated capacity because the mock producer fills the entire staging range.

Connectivity flood-fill depends on seed payload generation/loading only. It runs independently of the entity anomaly chain and is joined with crush-depth/entity validation through `JobHandle.CombineDependencies` before the single offline terminal readback.

## AUP And Quality

AUP rule:

- `double3 TargetAUP - double3 SectorOriginAup` is computed before casting the localized delta to `float3`.
- Height/SDF sampling never casts absolute 100 km coordinates directly to `float3`.
- Fatal rows receive AUP/hash identity before returning; non-finite coordinates and scalar payloads are blocked before SDF sampling, cell indexing, correction vector math, or crush-depth math.

Quality rule:

- `GlobalQualityWeight` does not change DTO layout, save identity, report schema, or authority route.
- `math.smoothstep(0.25, 0.85, q)` drives sampling fidelity.
- Low weight collapses height/SDF to nearest lookup.
- Middle weight blends nearest and bilinear/trilinear through `math.lerp`.
- High weight uses bilinear height and trilinear SDF.
- Reduced-quality triage scales connectivity flood-fill resolution from `4` to configured resolution and vertical floating probes from `1` to configured steps through `math.smoothstep(0.2, 0.95, q)`.
- Reports with `GlobalQualityWeight < 0.999`, disabled check families, or mock fallback are explicitly marked as triage (`certificationEligible=false`) and cannot be consumed as final geography certification.

Capacity rule:

- `Sanitize` clamps sector axes to `512`, height resolution to `1024`, SDF resolution to `128`, entities per sector to `65536`, navigation requests per sector to `128`, connectivity resolution to `32`, and vertical probe steps to `256` before NativeArray sizing or probe-loop work.

## Dear Lie

Rejected heavy route:

- Runtime `Physics.Raycast`, `SphereCast`, `MeshCollider.ClosestPoint`, full navmesh, and manual submarine flythrough.

Implemented fake:

- Direct SDF/height sample kernels and coarse SDF flood-fill over bounded sector grids.
- Crush-depth failure is predicted from material limit data instead of runtime destruction.

Complexity:

- Runtime validation cost: `0 us` by construction.
- Offline geometry check: `O(sectors * (entities + navRequests * resolution^3))`.
- The rejected scene/physics path depends on loaded scene broadphase, collider mesh complexity, and manual traversal variance.

## Vault And Black Box

Vault status:

- No persistent private `NativeArray`, `NativeList`, or `NativeHashMap` ownership is introduced for gameplay.
- Editor TempJob arrays are per-sector transient and disposed in pipeline scope.
- No Vault BufferID is claimed because SHINOBU_247 is offline/editor validation, not runtime state ownership.

Black box:

- `GeographySanityTelemetryEntry = 64` bytes.
- Fixed `300` rows.
- The ring write slot is chronological: `CompletedSectors % 300`; it is not a sector-coordinate hash bucket.
- The fixed ring is deterministically initialized by a 300-row cold for-loop after `UninitializedMemory` allocation, so unwritten dump rows do not contain stale bytes and the source still contains no `ClearMemory` or `MemClear`.
- The dump header cursor is computed from the highest recorded telemetry frame.
- Fatal math dumps header + telemetry ring to `Docs/AgentLogs/Dump_SHINOBU_247.bin`.
- Dump bytes are written explicitly little-endian: 32-byte header plus fixed 64-byte telemetry records. No host-endian raw struct write is claimed.

Numeric report formatting:

- Float and double report lanes use stack `Span<char>` with `TryFormat("R", InvariantCulture)` and append characters directly into the report builder.
- Non-finite values and impossible formatting failures write JSON `null`; they do not allocate round-trip numeric strings.
- The fixed-width `serializationMilliseconds` patch slot also uses stack `TryFormat("R", InvariantCulture)` and direct ASCII byte fill. Impossible formatting writes a fixed zero field; it does not call `ToString` or `Encoding.GetBytes`.
- `GEOGRAPHY_SANITY_DIAGNOSTIC.log` appends key/value fields directly and routes float/double values through the same stack-span numeric formatter instead of line-level string concatenation.

## Deviation Register

Task 18 originally requested `OnDrawGizmos`. The implementation now uses `SceneView.duringSceneGui` in `GeographySanityAnomalySceneView` inside the dedicated Editor assembly. This is a deliberate stronger-route deviation: no scene-injected `MonoBehaviour`, no `GameObject` proxy, no runtime folder churn, same red anomaly visualization from the JSON report.

The SceneView overlay parses the report with a bounded line stream and caps loaded records at `4096`. It does not call `File.ReadAllText` on the full report. Marker rendering subtracts the SceneView pivot in double-space before casting the local delta to `Vector3`, avoiding direct absolute-100km AUP-to-float conversion in the debug facade.

`Runtime_Spatial_Query_Scanner` enumerates C# files through `Directory.EnumerateFiles(...).GetEnumerator()` and scans each file with `StreamReader.ReadLine()` plus a bounded safe-spawn context ring. It does not retain a project-wide `Directory.GetFiles` path array or per-file `File.ReadAllLines` arrays. The safe-spawn ring and pending-finding buffer are reused per scanner run, so the scanner no longer allocates those scratch arrays per scanned file.

The scanner line classifier strips comments, resolves forbidden spatial-query patterns, detects method names, detects safe-spawn context, and trims report context through `ReadOnlySpan<char>`. It no longer allocates substring tokens for ordinary source lines; strings remain only for retained finding/report fields.

The scanner writes the SHINOBU-owned report `WORLD_OPTIMIZATION_REPORT_SHINOBU_247.json` first. The shared `WORLD_OPTIMIZATION_REPORT.json` path is guarded: it is written only if absent or already SHINOBU_247-owned. That ownership probe compares the quoted `AgentId` through spans and does not concatenate a token per report line. The current shared report is SHINOBU_245-owned and must not be clobbered by this tool.

## Verification Caveat

This route is static-source evidence only. A prior narrow `dotnet build Hecton8.Editor.csproj --no-restore --nologo -v:minimal` attempt timed out after 124017 ms with no compiler diagnostics. A later guarded no-restore attempt failed before C# compilation with `NETSDK1004` because `Temp/obj/Hecton8.Editor/project.assets.json` was missing. A guarded `dotnet restore Hecton8.Editor.csproj --nologo` later passed, but the follow-up build was not launched because active `dotnet` workers remained and then CPU returned to 100%. Unity import, Console, Burst Inspector, menu execution, profiler, and device proof remain pending.

The current generated `.csproj` set does not yet include `Hecton8.World.GeographySanity.Editor`; Unity project-file regeneration/import is required before any local dotnet build can prove this new asmdef.

Full-world report execution remains pending. The streaming report path is source-audited but not Unity-run in this workspace state.

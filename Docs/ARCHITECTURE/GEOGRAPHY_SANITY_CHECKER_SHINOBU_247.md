# SHINOBU_247 Geography Sanity Checker Route Card

Status: STATIC_SOURCE / PENDING UNITY VERIFICATION
Owner: SHINOBU_247 / GEOGRAPHY_SANITY_CHECKER
Date: 2026-05-21

## Boundary

Editor-only offline validator for the 100 km world.

Inputs: sector height/SDF payloads or deterministic mock sector data.

Work: evaluate object AUP against master geometry and write reports. It does not publish signals, mutate save state, enter rollback, or own gameplay truth.

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
- Invalid sector sidecars are fatal payload evidence.
- Fatal cases: truncated, locked, schema/endian/origin/length mismatch, non-finite, zero-radius, unsupported rule mask.
- They set `WarningInvalidSectorPayload`, emit `FATAL_MATH_ERROR`, write black-box telemetry, and never fall through to mock data.
- Per-sector sidecar filename construction uses stackalloc char spans plus `int.TryFormat`, avoiding coordinate `ToString` intermediates before the unavoidable filesystem path string.
- Data Monolith readiness is not claimed. This does not prove `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`.

Human tuning route:

- `Assets/StreamingAssets/Hecton8/WorldSanity/sanity_check_profiles.csv`.
- CSV route:
  - fixed stack line buffer;
  - `ReadOnlySpan<byte>` tokens;
  - fixed `2048` row `NativeList` capacity;
  - fail closed on overlong rows, excess rows, non-finite float overflow, overflowing uint flags, zero/unsupported rule masks, trailing columns;
  - no full-file byte rental, `string.Split`, `float.Parse`, or managed dictionary.

Editor facade route:

- `WorldSanityCheckerWindow` exposes check toggles, mock fallback, continuous `GlobalQualityWeight`, sector axes, height/SDF/entity/nav capacities, connectivity grid, vertical probe cadence, and max floating tolerance.
- The facade clamps through the same constants as the pipeline sanitizer. The pipeline remains the authoritative gate for programmatic callers.
- Count-bearing status lines format integers into stack `Span<char>` buffers.
- They assign only the final unavoidable UI label string.
- Mock benchmark, CSV load, and scanner result status paths no longer build concat intermediates.

Output route:

- `Docs/Reports/GEOGRAPHY_SANITY_REPORT.json`
- `Docs/Reports/GEOGRAPHY_SANITY_REPORT.anomalies.tmp` during full-world serialization only; deleted after final report write.
- `Docs/Reports/GEOGRAPHY_SANITY_SELF_AUDIT.json`
- `Docs/Reports/WORLD_OPTIMIZATION_REPORT_SHINOBU_247.json`
- `Docs/Reports/WORLD_OPTIMIZATION_REPORT.json`
- `Docs/AgentLogs/GEOGRAPHY_SANITY_REPORT.log`
- `Docs/AgentLogs/Dump_SHINOBU_247.bin`

- JSON report and diagnostic log include `warningFlags`.
- `WarningMissingSectorPayload` marks absent sidecars when mock fallback is disabled.
- `WarningInvalidSectorPayload` marks present-but-invalid master data.
- CI can distinguish missing upstream data from corrupted upstream data.

Full-world anomaly output streams sector rows into the temporary anomaly file, then copies rows into final JSON report.

The pipeline does not retain one world-sized anomaly `StringBuilder`.

- SceneView anomaly overlay reloads the report through a bounded stream.
- Record cap: `4096`.
- Type codes resolve from `ReadOnlySpan<char>`.
- AUP doubles parse from spans.
- SceneView pivot subtraction happens in double before float handle drawing.
- It does not allocate substring tokens per anomaly record.

Sector anomaly flushing writes the current `StringBuilder` through a pooled 4096-character chunk using `StringBuilder.CopyTo` and `StreamWriter.Write(char[], int, int)`, avoiding one sector-sized string allocation per flush.

- Final report assembly runs on an `Awaitable.BackgroundThreadAsync` lane.
- It does not await `CopyToAsync`/`WriteAsync`.
- It copies the temporary anomaly stream through a pooled byte chunk.
- It writes header/tail UTF-8 bytes through a pooled encoder buffer.
- Then it returns to the main thread.
- `serializationMilliseconds` is patched into a fixed-width JSON slot from the measured writer stopwatch, so the JSON header and diagnostic log share the same non-placeholder timing value.

- JSON unicode escaping appends `\u` plus four direct uppercase hex nibbles from source `char`.
- Applies to the main report and runtime spatial-query scanner.
- It allocates no per-character managed hex-format escape strings.

The mock benchmark report path no longer builds one full report string.

It writes header, pooled UTF-8 chunks, and tail directly to `FileStream`, then patches the measured `serializationMilliseconds` slot.

`GEOGRAPHY_SANITY_REPORT.json` writes configured and effective quality-scaled connectivity/probe settings.

`GEOGRAPHY_SANITY_DIAGNOSTIC.log` mirrors effective values through the same resolver methods used by scheduling. CI can separate requested sweep capacity from reduced-quality triage work.

Full-world progress UI uses constant `EditorUtility.DisplayProgressBar` title/info strings. Sector coordinates are retained in reports and logs, not concatenated into per-sector progress text.

Per-sector burst timing uses `Stopwatch.GetTimestamp()` scalar ticks and a static elapsed-millisecond conversion. The sector loop does not allocate a `Stopwatch` object for every sector.

Sector `.h8bin` input accepts native little-endian magic or reversed magic.

It normalizes `uint`, `int`, `float`, and `double` lanes before DTO hydration. Reverse-byte helpers stay inside Editor route to avoid runtime binary dependency.

Sector `.h8bin` origin is not advisory.

It must be finite and match expected sector AUP within `0.001` meters before payload hydration. Mismatches fail closed to mock/fallback or warning behavior.

Sector `.h8bin` v1 payloads are exact-length records. After the declared height, SDF, entity, and navigation rows are consumed, any trailing byte makes the sidecar invalid master data.

Sector `.h8bin` scalar lanes are validated before Burst jobs consume them.

- Height samples: finite.
- SDF samples: finite.
- Entity AUP/scalars: finite.
- Navigation AUP/scalars: finite.
- Entity radius and navigation vehicle radius: strictly positive.
- Entity rule masks: non-zero and limited to `RuleCheckFloating`, `RuleCheckBuried`, `RuleCheckCrushDepth`.
- Unsupported masks fail closed.

Floating, buried, and connectivity Burst kernels repeat the scalar-domain fence at execution time.

Non-finite, zero-radius, negative-clearance, negative-tolerance, and negative-recoverability lanes fatal-mark the row before SDF/height clearance math runs.

- The loader uses a three-state result: `Missing`, `Loaded`, or `Invalid`.
- Only `Missing` may use deterministic mock fallback, and only when the window setting allows it.
- `Invalid` covers truncated streams, IO/permission denial, schema/count mismatch, unsupported version, origin mismatch.
- It also covers trailing bytes, non-finite scalar lanes, zero entity/navigation radii, unsupported entity rule masks.
- It is fatal payload evidence.

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

- Loaded sparse sidecars schedule profile, floating, buried, crush-depth, and connectivity jobs.
- Work range: active entity/navigation counts only.
- Deterministic mock fallback schedules full generated capacity.
- Reason: mock producer fills entire staging range.

Connectivity flood-fill depends only on seed payload generation/loading.

It runs independently of entity anomaly chain and joins crush-depth/entity validation through `JobHandle.CombineDependencies` before single offline terminal readback.

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

- `Sanitize` clamps before NativeArray sizing or probe-loop work.
- Limits: sector axes `512`, height resolution `1024`, SDF resolution `128`, entities/sector `65536`.
- More limits: navigation requests/sector `128`, connectivity resolution `32`, vertical probe steps `256`.

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
- Fixed ring initializes through a 300-row cold for-loop after `UninitializedMemory` allocation.
- Unwritten dump rows do not contain stale bytes.
- Source still contains no `ClearMemory` or `MemClear`.
- The dump header cursor is computed from the highest recorded telemetry frame.
- Fatal math dumps header + telemetry ring to `Docs/AgentLogs/Dump_SHINOBU_247.bin`.
- Dump bytes are written explicitly little-endian: 32-byte header plus fixed 64-byte telemetry records. No host-endian raw struct write is claimed.

Numeric report formatting:

- Float and double report lanes use stack `Span<char>` with `TryFormat("R", InvariantCulture)` and append characters directly into the report builder.
- Non-finite values and impossible formatting failures write JSON `null`; they do not allocate round-trip numeric strings.
- Fixed-width `serializationMilliseconds` patch slot uses stack `TryFormat("R", InvariantCulture)` and direct ASCII byte fill.
- Impossible formatting writes fixed zero field; it does not call `ToString` or `Encoding.GetBytes`.
- `GEOGRAPHY_SANITY_DIAGNOSTIC.log` appends key/value fields directly and routes float/double values through the same stack-span numeric formatter instead of line-level string concatenation.

## Deviation Register

Task 18 deviation:

- Requested: `OnDrawGizmos`.
- Implemented: `SceneView.duringSceneGui`.
- Owner: `GeographySanityAnomalySceneView` in the dedicated Editor assembly.
- Rejected routes: scene-injected `MonoBehaviour`, `GameObject` proxy, runtime folder churn.
- Preserved output: red anomaly visualization from the JSON report.

SceneView overlay:

- Parser: bounded line stream.
- Loaded record cap: `4096`.
- Rejected read route: full-report `File.ReadAllText`.
- Render origin: subtract SceneView pivot in double-space.
- Cast route: local delta to `Vector3`.
- Rejected cast: absolute 100km AUP directly to `float`.

`Runtime_Spatial_Query_Scanner` IO:

- File enumeration: `Directory.EnumerateFiles(...).GetEnumerator()`.
- File scan: `StreamReader.ReadLine()`.
- Context: bounded safe-spawn ring.
- Rejected arrays: project-wide `Directory.GetFiles`, per-file `File.ReadAllLines`.
- Reuse: safe-spawn ring plus pending-finding buffer per scanner run.

Scanner line classifier:

- Strips comments.
- Resolves forbidden spatial-query patterns.
- Detects method names and safe-spawn context.
- Trims report context through `ReadOnlySpan<char>`.
- Rejected allocation: substring tokens for ordinary source lines.
- Remaining strings: retained finding/report fields only.

Report write route:

- First output: SHINOBU-owned `WORLD_OPTIMIZATION_REPORT_SHINOBU_247.json`.
- Shared path: `WORLD_OPTIMIZATION_REPORT.json`.
- Shared write gate: absent or already SHINOBU_247-owned.
- Ownership probe: quoted `AgentId` compared through spans.
- Rejected allocation: token concatenation per report line.
- Current shared owner: SHINOBU_245; do not clobber.

## Verification Caveat

- This route is static-source evidence only.
- A prior narrow `dotnet build Hecton8.Editor.csproj --no-restore --nologo -v:minimal` attempt timed out after 124017 ms with no compiler diagnostics.
- A later guarded no-restore attempt failed before C# compilation with `NETSDK1004` because `Temp/obj/Hecton8.Editor/project.assets.json` was missing.
- A guarded `dotnet restore Hecton8.Editor.csproj --nologo` later passed, but the follow-up build was not launched because active `dotnet` workers remained and then CPU returned to 100%.
- Unity import, Console, Burst Inspector, menu execution, profiler, and device proof remain pending.

The current generated `.csproj` set does not yet include `Hecton8.World.GeographySanity.Editor`; Unity project-file regeneration/import is required before any local dotnet build can prove this new asmdef.

Full-world report execution remains pending. The streaming report path is source-audited but not Unity-run in this workspace state.

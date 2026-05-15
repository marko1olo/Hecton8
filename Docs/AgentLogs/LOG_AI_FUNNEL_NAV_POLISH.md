# AI_FUNNEL_NAV_POLISH Log

## 2026-05-14 - Funnel Rsqrt / Pack=1 Polish

What was wrong:
- Prompt named `FunnelModifierJob`, but no first-party symbol with that name exists.
- Vendor A* funnel code is managed third-party code and was not the domain target.
- Live first-party funnel-like path smoothing is `HectonMapMagicVegetationBridge.StringPullPathJob` in `VegetationFlowFieldIntegrator.cs`.
- Hot path contained normalization/division-form math candidates and no packed portal value.

What was done:
- Added `NavPortal` as `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`.
- Reworked portal construction through `BuildNavPortal` with finite and zero-width clamps.
- Replaced normalization-form math with `NormalizeRsqrtOrFallback` using `math.rsqrt`.
- Replaced hot divisions with `math.rcp` in obstacle weighting, threat grid indexing, DDA, and voxel local coordinate conversion.
- Expanded scalar triple product arithmetic for 3D winding instead of flattening to 2D.
- Preserved existing `NativeArray<Vector3>`/`NativeList<Vector3>` boundary contract; internal math uses `float3`.
- Kept `[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]`.

Cinematic Cheats used:
- Predictable cheap 3D string-pull math instead of physical path relaxation.
- Rsqrt/rcp approximation path where exact sqrt/divide precision is not visually meaningful.
- 32-byte packed portal value for cache discipline.
- Rejected 2D projection cheat because underwater 6DOF vertical corridors can invert wedge logic.

Exact Microseconds saved:
- PENDING VERIFICATION. No Unity/Burst profiler session is available, and `dotnet build` is blocked by unrelated global dependencies. Fake microsecond values rejected.

Verification:
- Static scan of `StringPullPathJob` found no `math.normalize`, `math.length(`, `math.distance(`, or raw `/` matches in the edited job range.
- `dotnet build Hecton8.Core.csproj --no-restore /m:1 /nr:false` failed with 131 unrelated missing namespace/type errors; no reported error referenced `VegetationFlowFieldIntegrator.cs`.
- Unity MCP `validate_script` failed with `no_unity_session`.

Integrator notes:
- Assembly/data-vault/telemetry items are blocked by missing or unstable contracts and should be owned by the navigation/integration layer, not patched through globals from this job.

## 2026-05-14 - Patient Recheck / LOD / Black Box Upgrade

What was wrong:
- Math LOD was documented as blocked, but the scheduler had a safe managed access point for `GlobalRegistry.ScalabilityTier`.
- `MaxSamplesPerSegment` existed but did not constrain the LOS/DDA cost.
- Exhausted DDA checks returned visible, allowing over-smoothing through unverified voxel space.
- Black Box telemetry was absent for the funnel owner.

What was done:
- Added tiered `MaxPortalLookAhead`: Low/Unknown/MX350 = 4, Mid = 8, High/Ultra = 16.
- Passed the budget into `StringPullPathJob` as a primitive field, no global access from Burst.
- Capped DDA steps with `MaxSamplesPerSegment`.
- Changed DDA exhaustion to fail closed.
- Added `AbyssalPathTelemetryEntry` as a fixed 64-byte packed native telemetry record.
- Added persistent 300-entry telemetry ring, schedule/complete timing, over-budget telemetry, finite-point scan, and NaN dump to `Docs/AgentLogs/Dump_AI_FUNNEL_NAV_POLISH.bin`.

Cinematic Cheats used:
- Low-tier route smoothing keeps more raw waypoints instead of paying for long corridor beautification.
- High-tier spends budget on 16-portal line-of-sight compaction for cleaner motion silhouettes.
- Long unverified LOS now rejects compaction instead of simulating a heavier physics proof.

Exact Microseconds saved:
- PENDING RUNTIME PROFILER DATA. The owner now records real `FunnelMs`; no fabricated value written.

Verification:
- Static scan remains clean inside `StringPullPathJob`: no `math.normalize`, `math.length(`, `math.distance(`, or raw `/`.
- `dotnet build Hecton8.Core.csproj --no-restore /m:1 /nr:false` still fails due unrelated global dependency errors. Latest run: 128 errors, none reported in the edited funnel files.
- Unity MCP `validate_script` failed due editor transport error at `127.0.0.1:8088/mcp`.

## 2026-05-14 - Timing Semantics / Conservative LOS Pass

What was wrong:
- `FunnelMs` timing used schedule-to-completion latency, which can include normal async frame delay and produce false over-budget telemetry.
- Finite waypoint detection walked the completed path after the copy loop, duplicating O(n) work.
- Missing voxel coverage and out-of-grid DDA movement still returned visible, which could over-smooth through unknown space.

What was done:
- Moved stopwatch measurement to the `DispatcherJobSwap.TryComplete` call.
- Fused NaN/Infinity detection into the existing result-copy loop.
- Changed missing voxel conversion and out-of-grid DDA steps to fail closed.

Cinematic Cheats used:
- Conservative waypoint preservation when spatial proof is missing.
- Main-thread completion timing instead of trying to instrument Burst internals.

Exact Microseconds saved:
- PENDING RUNTIME PROFILER DATA. The redundant finite scan was removed, so savings scale with completed waypoint count, but no measured value is available.

Verification:
- Static scan remains clean inside `StringPullPathJob`: no `math.normalize`, `math.length(`, `math.distance(`, or raw `/`.
- `dotnet build Hecton8.Core.csproj --no-restore /m:1 /nr:false` still fails due unrelated global dependency errors. Latest run: 132 errors, none reported in the edited funnel files.
- Unity MCP `validate_script` still fails due editor transport error at `127.0.0.1:8088/mcp`.

## 2026-05-14 - DDA Tier Cap / Attribute-Safe Recheck

What was wrong:
- The prompt re-extraction command used an exact opening-tag regex, but the live batch tag includes `role` and `chat_name` attributes.
- The DDA LOS sample budget still inherited the authored cap on every tier.

What was done:
- Re-extracted the full `AI_FUNNEL_NAV_POLISH` XML block with an attribute-aware CLI regex.
- Added `ResolveAbyssalPathDdaSampleCap`: Low/Unknown/MX350 <= 32, Mid <= 64, High/Ultra = authored cap bounded by `MaxThreatDdaSteps`.
- Passed the resolved primitive sample cap into `StringPullPathJob` and telemetry.

Cinematic Cheats used:
- Low-tier keeps more raw waypoints instead of paying for long LOS proof.
- High/Ultra retains the expensive smoothing budget where visual polish can justify it.

Exact Microseconds saved:
- PENDING RUNTIME PROFILER DATA. Worst-case low-tier DDA steps per LOS segment are now bounded lower, but no Unity profiler capture is available in this session.

Verification:
- Attribute-aware prompt extraction succeeded.
- Static scan remains clean inside `StringPullPathJob`: no `math.normalize`, `math.length(`, `math.distance(`, or raw `/`.
- Latest parsed `dotnet build Hecton8.Core.csproj --no-restore /m:1 /nr:false` summary still fails with 128 global dependency errors; parsed edited-file error count = 0.
- Unity MCP `validate_script` still fails due editor transport error at `127.0.0.1:8088/mcp`.

## 2026-05-14 - Tail Safety / Black Box Readability Pass

What was wrong:
- If the LOS compaction loop exhausted `MaxPathCompactionIterations` before reaching the final waypoint, it could append only the final point and drop the unverified tail.
- The telemetry dump wrote the circular array in raw memory order, adding friction to NaN postmortem analysis.

What was done:
- Added fail-closed tail copying when compaction budget is exhausted.
- Changed `Dump_AI_FUNNEL_NAV_POLISH.bin` writer to include valid entry count and write entries oldest-to-newest.

Cinematic Cheats used:
- Bounded low-tier compaction remains cheap; route polish is abandoned before safety is abandoned.

Exact Microseconds saved:
- PENDING RUNTIME PROFILER DATA. This pass is correctness-first; it prevents invalid tail collapse without increasing normal compaction budget.

Verification:
- Static scan remains clean inside `StringPullPathJob`: no `math.normalize`, `math.length(`, `math.distance(`, or raw `/`.
- `dotnet build Hecton8.Core.csproj --no-restore /m:1 /nr:false` succeeded with 0 warnings and 0 errors.
- `dotnet build Hecton8.PlayModeTests.csproj --no-restore /m:1 /nr:false` succeeded with 0 warnings and 0 errors.
- Unity MCP `validate_script` remains unavailable in this session due prior `127.0.0.1:8088/mcp` transport failure.

## 2026-05-14 - Voxel Transform Contract Hardening Pass

What was wrong:
- LOS compaction still accepted passability/threat voxel payloads with finite length proof missing in the live file, and cell sizes were only masked with epsilon during world-to-voxel conversion.
- Non-finite world positions or origins could reach `math.floor` and produce undefined voxel candidates.
- Invalid flat voxel samples returned open water in the live file, allowing corrupt payload holes to authorize smoothing.
- DDA grid-step cap summed dimensions in `int`.

What was done:
- Restored complete native-grid length validation using 64-bit expected length.
- Added finite positive cell-size guards for uniform passability grids and anisotropic threat grids.
- Added fail-closed `TryWorldToVoxel` checks for non-finite world positions, origins, and cell sizes.
- Changed invalid flat samples to `SolidThreatVoxel`.
- Summed DDA dimension traversal cap in `long` before clamping to `MaxThreatDdaSteps`.

Cinematic Cheats used:
- Preserve raw waypoints when voxel proof is incomplete or corrupt; do not pay for heavier simulation to guess.
- High/Ultra smoothing is still allowed only through verified voxel space, so visual polish never outranks path authority.

Exact Microseconds saved:
- PENDING RUNTIME PROFILER DATA. Normal valid grids pay a few scalar checks; the intended saving is avoiding invalid compaction and downstream steering correction on corrupt payloads.

Verification:
- Static scan remains clean inside `StringPullPathJob`: no `math.normalize`, `math.length(`, `math.distance(`, or raw `/`.
- `git diff --check` passed for edited funnel/status/log files; only LF-to-CRLF warnings were emitted.
- `dotnet build Hecton8.Core.csproj --no-restore /m:1 /nr:false` is currently blocked by 48 unrelated global errors in `VRAMEnforcer`, `BinaryLayoutManifest`, `HardwareTierDetector`, and `HectonFluidEngine`; no errors were reported in `VegetationFlowFieldIntegrator.cs`.
- Unity MCP `validate_script` remains unavailable in this session due prior `127.0.0.1:8088/mcp` transport failure.

## 2026-05-14 - Fallback Direction Sanitation Pass

What was wrong:
- `NormalizeRsqrtOrFallback` returned fallback vectors raw in the live file.
- A non-unit or non-finite fallback axis could propagate into portal construction, winding selection, or DDA ray direction fallback.

What was done:
- Kept the valid primary-vector path as `value * math.rsqrt(lengthSq)`.
- Added finite fallback validation and `fallback * math.rsqrt(fallbackLengthSq)`.
- Added deterministic +Z fallback only when both primary and fallback vectors are unusable.

Cinematic Cheats used:
- Degenerate path corners get a cheap deterministic axis instead of paying for a heavier geometric recovery pass.

Exact Microseconds saved:
- PENDING RUNTIME PROFILER DATA. Normal path remains one rsqrt multiply; extra work exists only on degenerate or non-finite fallback cases.

Verification:
- Static scan remains clean inside `StringPullPathJob`: no `math.normalize`, `math.length(`, `math.distance(`, or raw `/`.
- Post-fallback `dotnet build Hecton8.Core.csproj --no-restore /m:1 /nr:false` timed out after 120s under overlapping Unity/MSBuild load.
- Prior completed Core build remains blocked by 48 unrelated global compile errors; edited funnel file reported no compiler errors in that failed Core build.

## 2026-05-14 - Live Drift Reconciliation Pass

What was wrong:
- The live black-box ring used `_abyssalPathTelemetrySequence` as valid-entry count for dumps.
- The live conduit scorer still used raw divisions and could include non-finite flow vectors in conduit weighting.
- `NormalizeVector3Fast` returned fallback vectors raw.
- `BuildNavPortal` component-spliced invalid endpoint components instead of rejecting whole corrupt endpoints.

What was done:
- Added `_abyssalPathTelemetryWrittenCount` and used it for dump valid-entry count.
- Ignored non-finite flow vectors for conduit strength while preserving obstacle/deep-affinity accumulation.
- Replaced conduit average/current strength divisions with `math.rcp` multiplies.
- Hardened `NormalizeVector3Fast` fallback normalization with finite checks and `math.rsqrt`.
- Hardened `BuildNavPortal` endpoint and width-squared sanitation.

Cinematic Cheats used:
- Corrupt voxel/flow/portal proof preserves deterministic conservative behavior instead of spending frame budget on speculative recovery.
- High-tier smoothing still runs through valid payloads; invalid data is not allowed to buy visual smoothness.

Exact Microseconds saved:
- PENDING RUNTIME PROFILER DATA. Expected low-end savings are from removing two scalar divisions per conduit-qualified candidate and avoiding invalid compaction/steering correction paths.

Verification:
- Static scan passed for `StringPullPathJob`: no `math.normalize`, `math.length(`, `math.distance(`, or raw `/`.
- Static scan passed for `TryResolveAbyssalNavNodeCandidate`: no `math.normalize`, `math.length(`, `math.distance(`, `.normalized`, or raw `/`.
- `git diff --check` passed for edited funnel/scheduler/status/log files; only LF-to-CRLF warnings were emitted.
- Bounded no-reference `dotnet build Hecton8.Core.csproj --no-restore /m:1 /nr:false /p:BuildProjectReferences=false` completed with 63 unrelated errors in `VRAMEnforcer`, `VoxelDeltaProcessor`, `SealedDoor`, `BinaryLayoutManifest`, and `HardwareTierDetector`; no errors were reported in edited funnel/navigation files.

## 2026-05-15 - H-Phi Feeder Hardening Pass

What was wrong:
- `NativeAStarJob` still had raw divisions in conduit alignment, 2D threat-grid lookup, predator falloff, and threat-voxel decode.
- Non-finite conduit vectors/strengths, threat grid transforms, predator snapshots, or threat voxel transforms could poison path costs before funnel smoothing.
- Undersized surface/voxel threat payloads were not proven complete before indexed sampling.
- Predator fear was discarded when a waypoint fell outside the 2D surface threat grid.

What was done:
- Replaced A* feeder hot divisions with `math.rcp` multiplies.
- Normalized conduit edge direction with `math.rsqrt(math.lengthsq(delta))` instead of the approximate route-cost distance.
- Added finite guards to conduit, threat-grid, predator-fear, and threat-voxel paths.
- Added 64-bit complete-length proof for surface threat and voxel threat grids.
- Changed corrupt threat voxel payloads to fail as max threat while preserving missing-grid and out-of-coverage behavior as zero threat.
- Preserved predator fear outside the surface heatmap by returning `max(voxelThreat, predatorFearThreat)`.
- Cached `GlobalRegistry.ScalabilityTier` once per abyssal path schedule before resolving lookahead and DDA Math LOD.
- Rejected non-finite path request endpoints before voxel/terrain sampling and nearest-node lookup.
- Replaced abyssal chunk node sample-step divisions with reciprocal math and finite chunk-bound guards.
- Hardened cached terrain height sampling with finite transform checks and complete heightmap length proof.
- Hardened abyssal candidate and deep-biome pool slicing with complete matrix/biome/semantic array proof and `long` slice-end clamping.
- Clamped abyssal nav payload counting/iteration to the actual native node buffer length.
- Rejected non-finite abyssal nav payload nodes before snapshot/hash insertion.
- Sanitized non-finite conduit vectors/strengths at nav graph ingress.
- Replaced abyssal spatial-hash and flow-support divisions with reciprocal precomputes plus finite transform guards.
- Replaced raw stopwatch-frequency division in abyssal funnel timing with `math.rcp`.
- Added `NativeAStarJob` workspace completeness checks and finite/cost guards before heap and score writes.
- Clamped A* threat weighting and vertical allowance to non-negative ranges.

Cinematic Cheats used:
- Corrupt feeder proof becomes conservative route pressure instead of running expensive recovery or pretending the path is clear.
- Low tier pays fixed cheap guards and reciprocal math; High/Ultra keep route fidelity for valid payloads and can spend saved cycles on smoothing budgets.

Exact Microseconds saved:
- PENDING RUNTIME PROFILER DATA. Static improvement is removal of four raw divide sites from `NativeAStarJob`, exact rsqrt normalization for conduit alignment, reciprocal chunk sampling/nav hash/support/timing math, guarded A* workspace/cost writes, and one fewer registry property read per abyssal path schedule; predator-fear retention adds no loop because the sample was already computed.

Verification:
- Static scan passed for `NativeAStarJob`: no `math.normalize`, `math.length(`, `math.distance(`, `.normalized`, or raw `/`.
- Static scan passed for abyssal chunk sampling, terrain sampling, candidate pool, nav support/hash, graph ingress, and funnel telemetry conversion regions: no hot-code raw `/`, `math.normalize`, `math.length(`, `math.distance(`, or `.normalized`.
- `git diff --check` on touched files passed; LF/CRLF warnings only.
- `Tools/Architecture/HectonPhiAudit.ps1 -Json` was attempted and timed out after 120 seconds; no global H-Phi score is claimed from this pass.
- Dotnet rebuilds were not run after this pass because the user explicitly prohibited dotnet rebuilds.

## 2026-05-15 - A* Reconstruction And Raw Path Fail-Closed Pass

What was wrong:
- `NativeAStarJob` guarded score arrays but still relied on owner-side path-list capacity before `AddNoResize`.
- A* reconstruction could append the requested start position even if the parent chain was broken, cyclic, or exhausted `MaxPathReconstructionIterations`.
- Heuristic/F-score overflow from extreme but finite payloads could still enter heap ordering.
- `StringPullPathJob` did not reject non-finite raw waypoints before writing smoothed output.
- Empty-output telemetry inspected raw endpoints but not interior raw waypoint corruption.

What was done:
- Added path-list capacity proof inside `NativeAStarJob`.
- Added finite checks for start heuristic, neighbor heuristic, resolved F-score, distance estimate, and current G-score.
- Cleared partial A* output unless reconstruction reaches `StartNode` through bounded valid parents; reconstruction is capped by `min(Nodes.Length, MaxPathReconstructionIterations)`.
- Added a finite raw-waypoint scan and output-capacity guard before string-pull emits any path.
- Added full raw-path finite telemetry scan when smoothed output is empty.
- Recorded that current `Docs/Tasks/CURRENT_BATCH.md` no longer contains `AI_FUNNEL_NAV_POLISH`; this pass continued from persisted status/rationale rather than a neighboring prompt.

Cinematic Cheats used:
- Broken route proof now produces no path instead of a visually plausible teleporting tail.
- Valid low-tier paths keep cheap bounded math; high-tier visual smoothing is reserved for routes with proven finite input and reconstruction.

Exact Microseconds saved:
- PENDING RUNTIME PROFILER DATA. Static improvement is mostly fault avoidance: no extra allocations, one path-capacity read, finite scalar gates, and a bounded raw waypoint scan that prevents invalid smoothing and steering correction.

Verification:
- Static scan passed for `NativeAStarJob` and `StringPullPathJob`: no `math.normalize`, `math.length(`, `math.distance(`, `.normalized`, or raw `/`.
- Static scan passed for abyssal nav graph ingress, telemetry conversion, nav support/hash, terrain sampling, and candidate resolver regions.
- `git diff --check` on touched files passed; LF/CRLF warnings only.
- Dotnet rebuilds were not run because the user explicitly prohibited dotnet rebuilds.

## 2026-05-15 - H-Phi Reciprocal Sweep

What was wrong:
- Shared direction helpers could derive axis signs from non-finite vectors.
- Adjacent vegetation-navigation support jobs still used scalar floating division in speed gates, retention, flow sampling, structure-grid mapping, threat propagation, obstacle gating, thermal/depth bands, wake falloff, and HLOD fade.
- Artificial-structure grid helpers did not fully reject non-finite transforms before cell/hash mapping.

What was done:
- Hardened `DominantAxisOrDefault(float2/float3)` against non-finite input and fallback vectors.
- Replaced float divisions with `math.rcp` multiplies or literal reciprocal constants in the edited navigation-support code.
- Added finite guards to structure cell range/index helpers before spatial hash lookup.
- Re-ran broad division scan; remaining `/` matches in the two edited world navigation files are integer index decomposition only.

Cinematic Cheats used:
- Bad transforms now fail out of structure/navigation influence instead of creating plausible but false cells.
- Low-tier math uses reciprocal gates and conservative fallbacks; high-tier visual density keeps the same valid-data behavior without paying avoidable scalar divides.

Exact Microseconds saved:
- PENDING RUNTIME PROFILER DATA. Static gain is removal of repeated scalar floating divisions across Burst jobs and one managed HLOD fade helper; no allocations or new containers added.

Verification:
- Broad scan of edited world navigation files reports only integer index decomposition divisions plus no `math.normalize`, `math.length(`, `math.distance(`, `.normalized`, `Mathf.Sqrt`, or `Math.Sqrt`.
- `git diff --check` on touched source files passed; LF/CRLF warnings only.
- Dotnet rebuilds were not run because the user explicitly prohibited dotnet rebuilds.

## 2026-05-15 - Bridge Sampler And Hash Payload Proof

What was wrong:
- Bridge threat/flow sampling could trust initialized native arrays without proving that the current resolution metadata fit the backing buffers.
- Threat chunk and artificial-structure hash feeders could map corrupt grid centers, cell sizes, or bounds into plausible buckets.
- Abyssal flow sampling could emit non-finite trilinear output if the volume payload contained corrupt values.
- Surface threat interpolation could propagate NaN into route pressure.

What was done:
- Added finite guards and reciprocal mapping to threat-sampling chunk hash rebuild/estimate/stamp paths.
- Added finite guards and reciprocal mapping to artificial-structure hash estimate/stamp paths.
- Added water/depth finite checks, 64-bit complete-volume length proof, finite half-extent proof, and finite sampled-output proof to abyssal flow sampling.
- Added 64-bit complete-grid length proof and finite half-extent proof to surface threat and echo samplers.
- Changed non-finite interpolated threat to return zero influence instead of propagating NaN.

Cinematic Cheats used:
- Bad feeder payloads fail out of influence rather than simulating repair or fabricating a smooth path through corrupt data.
- Low tier gets conservative cheap proof; High/Ultra keep richer visuals only when native payloads are complete and finite.

Exact Microseconds saved:
- PENDING RUNTIME PROFILER DATA. Static gain is removal of target-domain scalar divisions in bridge hash/sampler paths and prevention of NaN/undersized-grid recovery churn.

Verification:
- Targeted raw-division scan passed for bridge flow volume, threat metadata, threat hashes, artificial-structure hashes, threat sampler, and echo sampler ranges.
- Targeted forbidden hot-math scan passed for those same ranges: no `math.normalize`, `math.length(`, `math.distance(`, `.normalized`, `Mathf.Sqrt`, `Math.Sqrt`, `new List<`, `.ToList(`, or `foreach`.
- `git diff --check` on target source/status/rationale/log files passed; only LF-to-CRLF working-copy warnings were emitted.
- Dotnet rebuilds were not run because the user explicitly prohibited dotnet rebuilds.

## 2026-05-15 - Threat Service And Nearest-Node Fail-Closed Pass

What was wrong:
- External threat pulses could write non-finite route-pressure inputs.
- Artificial-structure registration could store corrupt bounds before later hash guards rejected them.
- Public flow fallback and abyssal conduit queries could return NaN vectors or index stale managed conduit snapshots.
- Threat hotspot scanning trusted grid resolution without proving native grid length.
- Nearest abyssal-node lookup used `_abyssalNavNodeCount` directly against managed snapshots.

What was done:
- Added finite guards to external threat pulse ingress.
- Added finite and positive-volume guards to artificial-structure registration.
- Added finite input/output and managed/native length guards to flow/conduit query APIs.
- Added complete-grid length, finite metadata, finite distance-band, and finite threat-sample proof to threat hotspot scans.
- Clamped nearest-node linear/hash lookup count to both managed snapshot length and native snapshot length before indexing.

Cinematic Cheats used:
- Corrupt route pressure now disappears instead of mutating steering state or attempting expensive repair.
- Valid Low-tier navigation remains cheap; High/Ultra can still spend route budget on richer threat/conduit visuals after payload proof.

Exact Microseconds saved:
- PENDING RUNTIME PROFILER DATA. Static gain is fault avoidance: fewer invalid O(N) hotspot scans, no stale-array conduit indexing, and no NaN steering recovery.

Verification:
- Targeted raw-division scan passed for `VegetationThreatAndStructureService.cs` and nearest-node lookup ranges.
- Targeted forbidden hot-math/allocation scan passed for those same ranges: no `math.normalize`, `math.length(`, `math.distance(`, `.normalized`, `Mathf.Sqrt`, `Math.Sqrt`, `new List<`, `.ToList(`, or `foreach`.
- `git diff --check` on touched source files passed; only LF-to-CRLF working-copy warnings were emitted.
- Dotnet rebuilds were not run because the user explicitly prohibited dotnet rebuilds.

## 2026-05-15 - Flow Sampler Payload Proof

What was wrong:
- `SampleFlowFieldAtPosition` guarded finite position and cell size, but did not prove the native flow buffer had `resolution * resolution` entries before bilinear reads.
- Corrupt resolution/cell-size metadata could produce non-finite half extents and plausible-looking local indices.

What was done:
- Added 64-bit complete-grid length proof before reading the four flow samples.
- Added finite half-extent proof before local coordinate mapping.
- Kept the existing dominant-axis output sanitizer so non-finite sampled flow still collapses to zero.

Cinematic Cheats used:
- Corrupt flow payloads produce no steering influence instead of recovery simulation or fabricated current vectors.
- Low tier stays O(1); High/Ultra can spend valid flow data on richer current visuals without changing the sampler.

Exact Microseconds saved:
- PENDING RUNTIME PROFILER DATA. Static gain is fault avoidance before four indexed native reads; no allocations or new containers.

Verification:
- Targeted raw-division scan passed for `SampleFlowFieldAtPosition`.
- Targeted forbidden hot-math/allocation scan passed for `SampleFlowFieldAtPosition`.
- `git diff --check` on touched source/status/rationale/log files passed; only LF-to-CRLF working-copy warnings were emitted.
- Dotnet rebuilds were not run because the user explicitly prohibited dotnet rebuilds.

## 2026-05-15 - Threat/Flow Payload Boundary Proof

What was wrong:
- Public threat/flow payload getters could expose created native arrays while resolution/cell-count metadata was stale or incomplete.
- `UpdateThreatHotspot` scanned `_ecosystemThreatGridCellCount` directly and could accept non-finite threat samples or non-finite player Y fallback.
- Swarm wake and local external pulse merge paths still needed local finite gates, independent of service-level ingress sanitation.

What was done:
- Added shared square-grid and voxel-grid cell-count proof helpers for boundary payload validation.
- Hardened `TryGetEcosystemFlowFieldPayload`, float/compressed/echo threat payload getters, and voxel threat payload getter with complete native length, declared cell-count coherence, and finite metadata checks.
- Routed direct compressed/echo threat properties through the same byte-grid proof helper.
- Hardened swarm wake registration and external pulse merge against non-finite positions, vectors, radii, strengths, lifetimes, and timers.
- Hardened hotspot scan with complete-grid proof, finite grid metadata, finite threat sample filtering, and finite player-position fallback.

Cinematic Cheats used:
- Corrupt threat/flow payloads now disappear as influence instead of being repaired or converted into plausible steering pressure.
- Low tier pays fixed scalar proof and keeps routes conservative; High/Ultra retain richer threat/flow navigation visuals only on complete finite payloads.

Exact Microseconds saved:
- PENDING RUNTIME PROFILER DATA. Static gain is fault avoidance before native payload exposure and O(N) hotspot scans; no allocations or new native containers were added.

Verification:
- Targeted changed-range scan passed for forbidden hot math/allocation; the only `/` hit is existing integer grid-index decomposition in hotspot decode.
- `git diff --check` on touched source files passed; only LF-to-CRLF working-copy warnings were emitted.
- Dotnet rebuilds were not run because the user explicitly prohibited dotnet rebuilds.

## 2026-05-15 - Direct Native View Clamp

What was wrong:
- Direct native properties for flow fields, abyssal nav nodes, and completed paths could bypass the safer payload getter contracts.
- Public counts could expose stale `_abyssalNavNodeCount` or `_abyssalPathCount` values larger than the available managed/native buffers.

What was done:
- Routed `EcosystemFlowField` through the same complete square-grid and finite metadata proof used by the safe payload getter.
- Routed `ActiveAbyssalNavNodesNative` through a native-view helper and clamped `ActiveAbyssalNavNodeCount` to managed snapshot length and native snapshot length.
- Routed `ActiveAbyssalPathNative` through a native-view helper and clamped `ActiveAbyssalPathCount` to the native path buffer length.

Cinematic Cheats used:
- Missing or stale native views now vanish instead of being repaired or copied.
- Low tier keeps zero-copy direct readback only when proof is complete; High/Ultra retain direct native throughput without managed allocations.

Exact Microseconds saved:
- PENDING RUNTIME PROFILER DATA. Static gain is avoided invalid native reads and steering recovery; no new allocations, no new containers, and no managed copies.

Verification:
- Targeted direct-view scan reported no raw division, forbidden hot math, managed allocation, or `foreach`.
- `git diff --check` on touched source/status/rationale/log files passed; only LF-to-CRLF working-copy warnings were emitted.
- Dotnet rebuilds were not run because the user explicitly prohibited dotnet rebuilds.

## 2026-05-15 - Nav Graph Payload Count Proof

What was wrong:
- `TryGetActiveAbyssalNavNodeTypePayload` could still export raw `_abyssalNavNodeCount`.
- The type payload needed its own count proof: using the full graph/conduit clamp would make node classifications disappear when optional conduit metadata is unavailable.

What was done:
- Added `ResolveAbyssalNavNodeTypeViewCount`, clamping type payload count to the proven node snapshot count and node-type native array length.
- Updated `TryGetActiveAbyssalNavNodeTypePayload` to use the new count proof.
- Rechecked thermal/flow and anchor/native graph boundary helpers already present in source.

Cinematic Cheats used:
- Stale type payloads now vanish by count proof instead of being repaired or copied.
- Low tier keeps cheap native classification reads; High/Ultra keep node classifications independent from conduit polish data.

Exact Microseconds saved:
- PENDING RUNTIME PROFILER DATA. Static gain is invalid-read avoidance; no allocations, no new containers, no managed copies.

Verification:
- Targeted nav graph payload scan reported no raw division, forbidden hot math, managed allocation, or `foreach`.
- `git diff --check` on touched source/status/rationale/log files passed; only LF-to-CRLF working-copy warnings were emitted.
- Dotnet rebuilds were not run because the user explicitly prohibited dotnet rebuilds.

## 2026-05-15 - Conduit Payload Count Decoupling

What was wrong:
- `TryGetAbyssalCurrentConduitPayload` reused the full native nav-graph count proof.
- That made current-conductor steering depend on node-type metadata even though the conduit payload only needs nodes, conduit vectors, and conduit strengths.

What was done:
- Added `ResolveAbyssalConduitViewCount`, clamping conduit payload count to the proven node snapshot, conduit-vector native length, and conduit-strength native length.
- Updated `TryGetAbyssalCurrentConduitPayload` to use the conduit-specific count proof.
- Reworked `ResolveAbyssalNavGraphViewCount` to compose conduit proof plus node-type proof, preserving strict full-graph export while preventing duplicated clamp logic.

Cinematic Cheats used:
- Missing optional node-type metadata no longer kills current steering; corrupt or partial conduit payloads still vanish by count proof.
- Low tier keeps cheap fail-closed current reads. High/Ultra keep full graph payload strictness for richer navigation overlays and diagnostics.

Exact Microseconds saved:
- PENDING RUNTIME PROFILER DATA. Static gain is avoided fallback steering/recovery when only classification metadata is stale; no allocations, no new containers, no managed copies.

Verification:
- Targeted conduit/view-count range scan reported no raw division, forbidden hot math, managed allocation, or `foreach`.
- `git diff --check` on touched source/status/rationale/log files was rerun without invoking dotnet rebuilds.
- Dotnet rebuilds were not run because the user explicitly prohibited dotnet rebuilds.

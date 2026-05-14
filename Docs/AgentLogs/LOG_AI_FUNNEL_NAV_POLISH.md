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

Cinematic Cheats used:
- Corrupt feeder proof becomes conservative route pressure instead of running expensive recovery or pretending the path is clear.
- Low tier pays fixed cheap guards and reciprocal math; High/Ultra keep route fidelity for valid payloads and can spend saved cycles on smoothing budgets.

Exact Microseconds saved:
- PENDING RUNTIME PROFILER DATA. Static improvement is removal of four raw divide sites from `NativeAStarJob`, exact rsqrt normalization for conduit alignment, reciprocal chunk sampling/nav hash/support/timing math, and one fewer registry property read per abyssal path schedule; predator-fear retention adds no loop because the sample was already computed.

Verification:
- Static scan passed for `NativeAStarJob`: no `math.normalize`, `math.length(`, `math.distance(`, `.normalized`, or raw `/`.
- Static scan passed for abyssal chunk sampling, terrain sampling, candidate pool, nav support/hash, graph ingress, and funnel telemetry conversion regions: no hot-code raw `/`, `math.normalize`, `math.length(`, `math.distance(`, or `.normalized`.
- `git diff --check` on touched files passed; LF/CRLF warnings only.
- `Tools/Architecture/HectonPhiAudit.ps1 -Json` was attempted and timed out after 120 seconds; no global H-Phi score is claimed from this pass.
- Dotnet rebuilds were not run after this pass because the user explicitly prohibited dotnet rebuilds.

# AI_FUNNEL_NAV_POLISH Rationale

Status: PENDING VERIFICATION

Problem: Prompt names `FunnelModifierJob`, but current first-party source does not contain that symbol.
Solution: Treat `HectonMapMagicVegetationBridge.StringPullPathJob` as the live funnel/string-pull implementation because it is Burst, scheduled after abyssal path solve, and processes voxel/MapMagic path corridors.
Rejected Alternatives: Editing `Assets/AstarPathfindingProject/Modifiers/FunnelModifier.cs` was rejected because it is third-party vendor code, managed `List<Vector3>` code, and AGENTS forbids custom drift in complex third-party assets without explicit cleanup authority. Creating a brand-new AI navigation subsystem was rejected because no direct dependency may be invented during parallel batch work.
Scalability potential: Low uses existing capped path buffers and no extra allocations. Middle keeps Burst auto-vectorized scalar math. High can spend saved CPU on wider route lookahead or richer fauna steering after profiling. Ultra can raise visual navigation readability without changing gameplay authority.
Hardware Impact: Expected i3/MX350 gain is from replacing division/normalization-form math in the funnel-like job with `math.rcp`/`math.rsqrt` and avoiding vendor managed funnel paths; measured proof absent.

Problem: Full Vector3 purge would require changing `VegetationMemoryPool` path buffers and every route consumer.
Solution: Keep the public native buffer contract intact, convert boundary values to `float3` inside `StringPullPathJob`, and optimize the internal funnel math only.
Rejected Alternatives: Replacing `NativeArray<Vector3>`/`NativeList<Vector3>` with `float3` globally was rejected because it would cut across world, vegetation, and route snapshot consumers during active compile churn.
Scalability potential: Low/Middle keep compatibility and gain cheaper math in the hot job. High/Ultra can later move the whole path contract to `float3` once the owning systems are compiled and profiled together.
Hardware Impact: Avoids a risky cross-domain migration on i3/MX350 while still removing hot normalization/division cost from the executed string-pull path.

Problem: The prompt requested rsqrt and Pack=1, but the local implementation used loose portal endpoints and normalization-like math.
Solution: Added a 32-byte `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)] NavPortal`, clamped zero-width portals, and normalized with `math.rsqrt` through a finite fallback helper.
Rejected Alternatives: Default struct packing and `math.normalize` were rejected because they hide layout and generate avoidable sqrt/divide paths.
Scalability potential: Low uses the same 32-byte portal value without allocations. Middle/High/Ultra can batch or cache portals later without a layout rewrite.
Hardware Impact: Expected low-end gain is fewer scalar divides/sqrt paths and better packed stack/local portal layout; exact microseconds require Burst profiler data.

Problem: 2D funnel simplification would be cheaper but wrong for underwater 6DOF corridors.
Solution: Kept 3D winding through a scalar triple product and expanded the dot/cross expression into scalar arithmetic.
Rejected Alternatives: Flattening to XZ and using 2D cross was rejected because vertical swim routes and voxel clearance can invert portal winding.
Scalability potential: Low keeps deterministic 3D string pulling. High/Ultra can spend saved cost on wider lookahead if a tier service is exposed.
Hardware Impact: Reduces helper-call overhead without corrupting 3D path authority on low-end silicon.

Problem: The job had raw division candidates in obstacle weighting, threat grid indexing, DDA reciprocals, and voxel local coordinates.
Solution: Replaced hot divisions with `math.rcp` precomputes and multiplication inside the job.
Rejected Alternatives: Leaving C# `/` and trusting Burst to optimize every case was rejected because the mandate requires explicit division purge.
Scalability potential: Low benefits immediately from reciprocal reuse. High/Ultra can reuse the same reciprocal pattern if path sampling density increases.
Hardware Impact: Expected gain on i3/MX350 comes from removing repeated divide latency in DDA and grid sampling; exact microseconds remain pending profiler verification.

Problem: Data sovereignty, hardware-tier LOD, and blackbox dumping are not safely injectable into this Burst job from the current file.
Solution: Marked those items blocked with dependency notes instead of inventing globals or managed telemetry from inside Burst.
Rejected Alternatives: Polling a singleton tier service, writing files from a Burst path job, or moving source data into GlobalDataVault without an owner were rejected as architecture damage.
Scalability potential: Low remains stable. Middle/High/Ultra need a navigation contract owner to expose tiered lookahead and telemetry buffers without breaking Burst.
Hardware Impact: Avoids managed calls, synchronization, and file IO on low-end hardware; preserves future telemetry hook points for the owner.

Problem: Math LOD was initially blocked because the Burst job had no safe hardware-tier service.
Solution: Resolve `GlobalRegistry.ScalabilityTier` in the managed scheduler and pass a primitive `MaxPortalLookAhead` into `StringPullPathJob`. Low/Unknown/MX350 use 4 portals, Mid uses 8, High/Ultra use 16.
Rejected Alternatives: Polling `GlobalRegistry` from inside Burst or creating a new AI navigation singleton was rejected. A fixed universal cap was rejected because it wastes high-tier visual navigation budget.
Scalability potential: Low stays cheap and conservative. Middle keeps modest smoothing. High/Ultra spend saved cycles on longer line-of-sight compaction for cleaner fauna routes.
Hardware Impact: MX350/i3 avoids all-corridor compaction scans; high-end machines get visibly smoother route simplification without changing path authority.

Problem: LOS compaction could scan too far and previously returned visible when the DDA step budget exhausted.
Solution: `MaxSamplesPerSegment` now caps DDA work, and exhausted checks fail closed by returning false.
Rejected Alternatives: Leaving exhausted DDA as visible was rejected because it can over-smooth through unverified voxels. Raising the global DDA cap was rejected because it buys CPU spikes, not immersion.
Scalability potential: Low keeps more raw waypoints instead of risking geometry violation. High/Ultra can raise the inspector sample cap to buy smoother lines through sparse spaces.
Hardware Impact: Low-end silicon gets bounded DDA traversal per segment and avoids pathological long voxel walks.

Problem: The LOS DDA sample cap was still driven by one authored inspector value, so low-tier hardware could pay high-tier traversal cost.
Solution: Resolve the DDA sample cap beside the portal lookahead budget: Low/Unknown/MX350 clamps to 32, Mid clamps to 64, High/Ultra uses the authored cap bounded by `MaxThreatDdaSteps`.
Rejected Alternatives: A fixed universal cap was rejected because it either wastes low-end frame time or under-delivers high-end smoothing. Reading scalability globals inside the Burst job was rejected.
Scalability potential: Low keeps short, predictable LOS probes. Middle gets moderate smoothing. High/Ultra can spend the authored budget on cleaner motion silhouettes through verified voxel space.
Hardware Impact: MX350/i3 worst-case DDA work per LOS segment is capped lower before the job runs; exact microseconds remain pending runtime telemetry.

Problem: Black Box telemetry could not be written from inside Burst, but the owner can safely record completed path states.
Solution: Added a 300-entry persistent `NativeArray<AbyssalPathTelemetryEntry>` on `HectonMapMagicVegetationBridge`, schedule/complete timing via `Stopwatch.GetTimestamp`, over-budget telemetry through `GlobalTelemetryBus`, and `Dump_AI_FUNNEL_NAV_POLISH.bin` on NaN.
Rejected Alternatives: `Stopwatch` and file IO inside the Burst job were rejected. Managed per-frame lists were rejected. Chat-only failure notes were rejected.
Scalability potential: Low records compact fixed telemetry. High/Ultra get the same diagnostic coverage while spending the variable budget on smoothing, not logging.
Hardware Impact: Runtime hot path adds fixed native writes at completion only; no managed allocation during normal path solves after the initial persistent ring allocation.

Problem: Schedule-to-completion timing measures async latency, not funnel completion overhead, and can false-alarm if a path completes on a later frame.
Solution: Move `Stopwatch.GetTimestamp` to the `DispatcherJobSwap.TryComplete` call and record that wall time as `FunnelMs`.
Rejected Alternatives: Keeping schedule latency was rejected because it corrupts performance telemetry. Timing inside Burst was rejected because managed timers are invalid there.
Scalability potential: Low-tier telemetry now reports actual completion pressure instead of frame-delay noise. High/Ultra can use the same signal to decide if extra lookahead is affordable.
Hardware Impact: MX350/i3 avoids false 16 ms warnings from normal async delay; recorded overhead now reflects actual main-thread completion/blocking cost.

Problem: Telemetry finite checking performed a second pass over the smoothed path after the result was already copied.
Solution: Fuse NaN/Infinity detection into the existing result-copy loop and pass endpoints/finite state into the telemetry writer.
Rejected Alternatives: A second full traversal was rejected because long path results are exactly the spike case this task is reducing.
Scalability potential: Low avoids duplicate O(n) work. High/Ultra retain full finite coverage without extra traversal.
Hardware Impact: Saves one completed-path scan on low-end silicon, proportional to waypoint count.

Problem: LOS compaction still trusted missing voxel coverage and out-of-grid traversal as visible.
Solution: Missing voxel conversion and out-of-grid DDA movement now fail closed, preserving waypoints rather than smoothing through unknown space.
Rejected Alternatives: Treating unknown space as visible was rejected because it buys visual smoothness by risking geometry violation.
Scalability potential: Low keeps more conservative paths. High/Ultra still smooth aggressively inside verified voxel coverage.
Hardware Impact: Reduces invalid long compaction attempts and avoids route artifacts that cause downstream steering correction.

Problem: The in-place LOS compaction guard could hit `MaxPathCompactionIterations` before reaching the final waypoint, then append only the final point and silently drop the unverified tail.
Solution: When the iteration cap is exhausted, copy the remaining original path tail in order and stop compaction.
Rejected Alternatives: Raising `MaxPathCompactionIterations` was rejected because it preserves a spike path. Appending only the final point was rejected because it trades performance for invalid navigation proof.
Scalability potential: Low keeps deterministic bounded work without route corruption. Middle/High/Ultra still compact normally when the proof completes inside budget.
Hardware Impact: MX350/i3 avoids unbounded compaction work while preserving waypoint safety; exact microseconds unchanged until profiler capture.

Problem: The black-box dump wrote the circular telemetry array in raw memory order, forcing postmortem readers to reconstruct the last-frame sequence manually.
Solution: Dump valid entries oldest-to-newest while retaining capacity, cursor, and sequence metadata.
Rejected Alternatives: Dumping raw array order was rejected because it slows crash analysis. Allocating a managed sorted list was rejected.
Scalability potential: No hot-path cost; dump-only readability improves on every tier.
Hardware Impact: Runtime frame impact remains zero outside fault dump; dump path writes fewer cold entries before the ring is full.

Problem: Compile verification is blocked by global dependency errors outside the funnel domain.
Solution: Ran a bounded `dotnet build` and Unity MCP script validation; recorded the compile wall and tool session failure without claiming success.
Rejected Alternatives: Fixing Core/Audio/AI/Physics contracts from this task was rejected because the Integrator owns assembly surgery.
Scalability potential: The funnel patch remains narrow and reviewable when the global build is repaired.
Hardware Impact: No runtime hardware claim can be finalized until Burst compilation/profiling runs in Unity.

Problem: The prior global compile wall cleared after neighboring dependency fixes, so the funnel patch needed a fresh evidence pass.
Solution: Re-ran bounded `dotnet build Hecton8.Core.csproj --no-restore /m:1 /nr:false`; result is 0 warnings and 0 errors.
Rejected Alternatives: Keeping the stale blocked status was rejected because current objective data supersedes the old dependency wall.
Scalability potential: Build-valid code can now enter Unity/Burst profiler verification without assembly noise.
Hardware Impact: Runtime hardware claims still require Unity profiler capture; compile proof only verifies C# integration.

Problem: LOS smoothing could still trust voxel transforms with non-finite origins, non-finite world positions, zero/NaN cell sizes, undersized payloads, or hostile dimensions that overflow an int DDA cap.
Solution: Added complete-grid and finite positive cell-size guards, fail-closed `TryWorldToVoxel` checks, `long` dimension summing for DDA caps, and `SolidThreatVoxel` fallback for invalid flat samples.
Rejected Alternatives: Letting `math.max(cellSize, epsilon)` hide corrupted cell sizes was rejected because it converts invalid payloads into plausible motion proof. Editing upstream voxel payload ownership was rejected because it is outside this agent domain.
Scalability potential: Low/MX350 preserves raw waypoints when voxel proof is corrupt instead of spending cycles on invalid compaction. Middle keeps bounded DDA. High/Ultra still get aggressive smoothing only inside complete, finite voxel payloads.
Hardware Impact: Normal valid payload cost is a few scalar finite/positive checks before DDA. Low-end gain is indirect but important: avoids downstream steering correction and repeated failed smoothing on corrupted grid payloads. Exact microseconds remain pending Unity profiler data.

Problem: Latest Core compile verification is blocked again by unrelated global changes outside the funnel domain.
Solution: Recorded the dependency wall and kept this patch scoped. The latest bounded no-reference Core build reports 63 unrelated errors in `VRAMEnforcer`, `VoxelDeltaProcessor`, `SealedDoor`, `BinaryLayoutManifest`, and `HardwareTierDetector`; no errors are reported in `VegetationFlowFieldIntegrator.cs`, `VegetationNavGridSynchronizer.cs`, or `HectonMapMagicVegetationBridge.cs`.
Rejected Alternatives: Editing optimization, core save-layout, hardware-tier, or fluid-engine files from this AI navigation polish pass was rejected as cross-domain damage.
Scalability potential: Funnel changes stay reviewable and can be Burst-verified once global compile owners clear their dependency breaks.
Hardware Impact: Runtime hardware claims remain unfinalized until Unity/Burst validation and profiler capture are available.

Problem: The live `NormalizeRsqrtOrFallback` returned fallback vectors raw, so a non-unit or non-finite fallback could leak into portal axes, winding axes, or DDA ray directions.
Solution: Keep the common valid-vector path as `value * math.rsqrt(lengthSq)`, normalize finite fallback vectors with `math.rsqrt`, and return +Z only when both inputs are unusable.
Rejected Alternatives: Returning raw fallbacks was rejected because secondary axes are still path authority. Branchlessly normalizing both primary and fallback every call was rejected because it burns math on the common valid path.
Scalability potential: Low/MX350 pays no extra work on valid vectors. Middle/High/Ultra get deterministic axis sanitation in degenerate corridor corners without changing visual smoothing budgets.
Hardware Impact: Normal path remains one rsqrt multiply. Extra fallback work only occurs on degenerate or non-finite inputs; exact microseconds remain pending Unity profiler data.

Problem: The live black-box dump still derived valid telemetry count from `_abyssalPathTelemetrySequence`, which can wrap and is semantically an event ID, not a count.
Solution: Added `_abyssalPathTelemetryWrittenCount`, reset it with the ring, increment it up to `AbyssalPathTelemetryFrameCount`, and use it for dump valid-entry count.
Rejected Alternatives: Widening sequence to 64-bit was rejected because the dump needs valid occupancy, not only identity. Leaving sequence-as-count was rejected as long-soak postmortem drift.
Scalability potential: No hot-path allocation. Low and High tiers get the same 300-frame diagnostic ring with stable oldest-to-newest dump semantics.
Hardware Impact: One capped integer increment per completed path; negligible on i3/MX350 and no extra Burst-job work.

Problem: The live conduit scorer still used raw managed divisions and allowed non-finite flow vectors to poison average-current and conduit-strength calculations.
Solution: Ignore non-finite flow vectors for conduit strength, replace average and strength divisions with `math.rcp` multiplies, and keep obstacle/deep-affinity accumulation intact.
Rejected Alternatives: Trusting managed compiler division lowering was rejected by the math gate. Treating non-finite flow as zero-length but still counting it was rejected because it biases conduit strength.
Scalability potential: Low/MX350 avoids divide latency and corrupt flow amplification. Middle/High/Ultra retain visual conduit fidelity when flow payloads are valid.
Hardware Impact: Two scalar divisions become reciprocal multiplies per conduit-qualified candidate; exact microseconds remain pending Unity profiler data.

Problem: `NormalizeVector3Fast` and `BuildNavPortal` still had weak fallback/finite handling in live source.
Solution: `NormalizeVector3Fast` now finite-checks and rsqrt-normalizes both primary and fallback vectors. `BuildNavPortal` rejects whole non-finite endpoints and clamps non-finite width squared to `FunnelEpsilon`.
Rejected Alternatives: Component-splicing invalid portal endpoints with `math.select` was rejected because it can create artificial portal geometry. Raw fallback vectors were rejected because secondary axes are still path authority.
Scalability potential: Valid-path cost remains the same for normal vectors and finite portals. Degenerate/corrupt inputs fail into deterministic cheap axes rather than heavier recovery logic.
Hardware Impact: Normal paths remain one rsqrt or simple finite checks; extra branches only execute on corrupt/degenerate payloads.

## OMEGA POLISH CHANGES

Problem: Final anti-bloat pass required checking for honest math, divisions, managed strings, and allocation paths after task completion/blocking.
Solution: Static scan confirmed no `math.normalize`, `math.length(`, `math.distance(`, or raw `/` remained in the edited `StringPullPathJob` region. No managed strings, `foreach`, or native container allocation were introduced inside the Burst job.
Rejected Alternatives: Adding a LUT was rejected because the portal is built from dynamic obstacle/threat/voxel geometry and the current rsqrt/rcp path is cheaper than maintaining cache state in this context.
Scalability potential: Low/Middle run the same deterministic cheap path. High/Ultra can later increase visual navigation polish through a tier-owned lookahead budget.
Hardware Impact: Exact microseconds saved are pending Unity/Burst profiler verification; the new owner-side telemetry will collect real funnel completion timing at runtime.

Problem: H-Phi in the navigation domain was still capped by upstream A* feeder cost quality: `NativeAStarJob` had raw divisions in conduit direction, threat-grid sampling, predator falloff, and threat-voxel decode.
Solution: Replaced those divisions with `math.rcp` multiplies and normalized conduit edge direction with `math.rsqrt(math.lengthsq(delta))` instead of the approximate path-cost distance.
Rejected Alternatives: Trusting Burst or the C# compiler to lower `/` was rejected because the rsqrt/reciprocal mandate requires explicit math. Replacing the A* cost model was rejected because this pass owns polish, not route-authority redesign.
Scalability potential: Low/MX350 removes divide latency from every feeder candidate and threat lookup. Middle/High/Ultra can spend the saved scalar budget on higher authored smoothing caps without changing behavior.
Hardware Impact: Expected gain is small per candidate but broad across A* expansion. Exact microseconds remain pending Unity profiler data because dotnet rebuilds are prohibited by current user instruction.

Problem: Non-finite or incomplete feeder payloads could poison path costs before the funnel ever saw the route.
Solution: Added finite guards for conduit nodes/vectors/strengths, threat grid center/cell size, predator fear nodes, and threat voxel origin/cell size. Surface threat and voxel threat grids now require complete native lengths using 64-bit expected-size checks.
Rejected Alternatives: Treating malformed payloads as zero threat/open water was rejected because it hides corrupt data as cheap navigation. Managed repair/rebuild of payloads was rejected as outside this domain and too expensive for the hot path.
Scalability potential: Low keeps conservative, predictable navigation when payloads are corrupt. Middle/High/Ultra preserve high-quality smoothing only when upstream data has enough proof.
Hardware Impact: Adds cheap finite/length checks before indexed reads; on low-end hardware the expected win is avoiding invalid route expansion and downstream steering correction.

Problem: Predator fear was dropped when a point was outside the 2D surface threat grid, even though species-specific fear is independent route pressure.
Solution: Outside-surface-grid sampling now returns `max(voxelThreat, predatorFearThreat)` instead of only voxel threat.
Rejected Alternatives: Forcing predator fear into the 2D grid was rejected because it creates a coupling to heatmap coverage. A second fear field resample was rejected as waste.
Scalability potential: Low gets correct cheap fear avoidance without more containers. High/Ultra keep stronger route intent while still using the same fixed snapshots.
Hardware Impact: No extra loop was added; predator fear was already sampled before the branch. The change prevents bad routes rather than claiming a measurable CPU save.

Problem: The project-level static H-Phi audit was requested by context but current user instruction prohibits rebuilds and the PowerShell audit exceeded the 120 second tool window under repo load.
Solution: Record the audit timeout as attempted evidence and do not claim a project-wide H-Phi score from this navigation pass. Use domain-local static scans and code deltas as the valid evidence.
Rejected Alternatives: Running dotnet rebuilds was rejected by explicit user instruction. Editing the central H-Phi report without a completed audit was rejected because it would create fake evidence.
Scalability potential: Domain-local navigation hardening remains valid even without a fresh global score; project-wide H-Phi measurement belongs to a successful audit run or the H-Phi monitor owner.
Hardware Impact: None; the timed-out audit was offline tooling only.

Problem: The abyssal path scheduler read `GlobalRegistry.ScalabilityTier` twice in the same schedule pass, adding redundant registry surface to the navigation H-Phi pressure.
Solution: Cache the tier once in a local `HectonQualityTier` and pass that primitive to both Math LOD resolvers.
Rejected Alternatives: Moving tier ownership into the Burst job was rejected. Adding a new service dependency for one cached primitive was rejected as overengineering.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged; the scheduler now has one authoritative tier sample for both lookahead and DDA budgets.
Hardware Impact: Removes one global property read per abyssal path schedule. Exact microseconds are below meaningful profiler resolution, but the H-Phi registry surface is cleaner.

Problem: Abyssal nav graph ingress could write non-finite nodes, conduit vectors, or conduit strengths into persistent snapshots and the spatial hash before later path guards ran.
Solution: Clamp payload iteration to the actual native node buffer length, reject non-finite nodes at snapshot ingress, sanitize conduit vectors/strengths, and keep corrupt payloads out of the searchable hash.
Rejected Alternatives: Trusting serialized `Count` over native buffer length was rejected. Hashing corrupt nodes into a default bucket was rejected because it turns invalid payloads into discoverable route candidates. Repairing payload producers was rejected as cross-domain for this pass.
Scalability potential: Low avoids wasted nearest-node scans and invalid A* expansions. Middle/High/Ultra keep richer route smoothing only on valid graph payloads.
Hardware Impact: Adds cheap finite checks during snapshot rebuild; avoids downstream path expansion and steering correction against corrupt nodes.

Problem: Abyssal spatial hash lookup and flow-field nav support still used raw divisions and weak finite guards in the navigation hot/warm path.
Solution: Precompute reciprocal cell/radius terms with `math.rcp`, reject invalid grid centers/origins/cell sizes, and skip non-finite support nodes.
Rejected Alternatives: Leaving `/` in managed route support was rejected by the math gate. Masking bad cell sizes with epsilon only was rejected because it fabricates plausible coordinates from invalid transforms.
Scalability potential: Low/MX350 gets fewer scalar divisions and safer hash lookups. High/Ultra can reuse the same clean support grid for denser visual navigation without corrupting path authority.
Hardware Impact: Removes repeated divisions from nav support and hash lookup paths. Exact microseconds remain pending Unity profiler data because dotnet rebuilds are prohibited.

Problem: Abyssal chunk node generation still divided by sample step/count and trusted terrain cache dimensions before sampling height.
Solution: Validate finite chunk bounds and positive finite node step, use `math.rcp` for sample counts and per-sample step, and require finite terrain transforms plus complete heightmap length before sampling.
Rejected Alternatives: Letting `math.max` hide invalid terrain dimensions was rejected because it creates route nodes from corrupt tile transforms. Rebuilding terrain caches here was rejected as outside the AI navigation domain.
Scalability potential: Low avoids wasting nodes on invalid chunks and removes warm-path divides. Middle/High/Ultra get cleaner source nodes for smoother pathing without extra containers.
Hardware Impact: Removes four graph-generation divisions and prevents corrupt heightmap reads. Exact microseconds remain pending Unity profiler data because dotnet rebuilds are prohibited.

Problem: The node candidate resolver indexed biome, semantic, and flow arrays using only matrix length and computed slice end with `payload.UnderwaterOffset + payload.UnderwaterCount` in `int`.
Solution: Require complete matrix/biome/semantic arrays, clamp candidate and deep-biome slice bounds with a `long` requested end, and treat flow vectors as optional zero vectors when the flow array is absent or shorter.
Rejected Alternatives: Indexing parallel arrays by matrix length alone was rejected because mismatched native payloads can crash the route snapshot pass. Int `offset + count` slice math was rejected because corrupted chunk metadata can overflow. Requiring flow vectors for obstacle/deep-affinity evaluation was rejected because flow is conduit polish, not baseline passability.
Scalability potential: Low keeps route graph rebuilds from crashing on partial payloads. High/Ultra still get conduit quality when flow data exists.
Hardware Impact: Adds fixed scalar bounds checks per snapshot rebuild and avoids invalid memory reads; exact microseconds remain pending Unity profiler data.

Problem: Abyssal funnel completion timing still used a raw division by `Stopwatch.Frequency`.
Solution: Convert ticks to milliseconds with `math.rcp((double)Stopwatch.Frequency)` inside `ResolveAbyssalPathElapsedMs`.
Rejected Alternatives: Leaving `/` was rejected by the reciprocal math gate. Caching a mutable managed timing service was rejected as unnecessary registry surface.
Scalability potential: All tiers keep identical telemetry semantics with cleaner scalar math.
Hardware Impact: Removes one scalar division per completed abyssal path timing sample; exact microseconds are below practical profiler resolution.

Problem: `NativeAStarJob` trusted scheduler-provided workspace arrays and finite node data before writing scores, parents, closed flags, and heap positions.
Solution: Add a complete-workspace guard for all native arrays, reject non-finite start/end payloads, skip non-finite current/neighbor nodes, reject non-finite edge distance/cost, clamp threat weighting non-negative, and clamp vertical allowance to non-negative.
Rejected Alternatives: Relying on `EnsureAbyssalPathBuffers` alone was rejected because Burst jobs must fail closed when called with corrupted or stale native state. Letting negative threat weights invert edge costs was rejected by the pathfinding mandate.
Scalability potential: Low avoids invalid heap churn and route crashes from corrupt graph payloads. High/Ultra keep the same A* behavior on valid data and spend smoothing budget only after route authority is proven.
Hardware Impact: Adds scalar guards inside A* expansion; expected savings come from early rejection of corrupt candidates and prevention of downstream steering/telemetry faults. Exact microseconds remain pending profiler data.

Problem: `NativeAStarJob` still trusted raw path-list capacity and could append the requested start position after a broken or over-budget parent chain.
Solution: Require native path capacity before `AddNoResize`, sanitize heuristic/F-score math before heap writes, and clear the path unless reconstruction proves a bounded parent chain back to `StartNode`; reconstruction is capped by `min(Nodes.Length, MaxPathReconstructionIterations)` so a cyclic parent chain cannot outgrow the proven path-list capacity.
Rejected Alternatives: Increasing `MaxPathReconstructionIterations` was rejected because it hides corrupt parent state with more work. Emitting partial paths was rejected because it creates visually smooth but unproven navigation authority.
Scalability potential: Low/MX350 fails closed without invalid steering corrections. Middle/High/Ultra keep identical valid-path behavior and spend smoothing budget only after A* proof is complete.
Hardware Impact: Adds cheap scalar checks and one capacity read; expected win is prevention of invalid path tails and downstream correction churn. Exact microseconds remain pending profiler data because dotnet rebuilds are prohibited.

Problem: `StringPullPathJob` could consume non-finite raw waypoints from a macro route or corrupted upstream path and the black-box telemetry only inspected raw endpoints when smoothing emitted no output.
Solution: Gate string-pull execution on output capacity plus full raw-waypoint finite proof, and scan all raw waypoints for finite telemetry when output is empty.
Rejected Alternatives: Clamping individual waypoint components was rejected because it fabricates path geometry. Waiting for post-copy NaN detection was rejected because the funnel should not emit invalid output in the first place.
Scalability potential: Low pays a bounded O(n) finite scan on the raw path and avoids expensive recovery. High/Ultra keep the same route fidelity for valid payloads and get cleaner crash evidence for invalid payloads.
Hardware Impact: The scan is linear in waypoint count and allocation-free; expected low-end gain is from avoiding invalid smoothing/steering paths rather than from a pure CPU micro-optimization. Exact microseconds remain pending profiler data.

Problem: The live `Docs/Tasks/CURRENT_BATCH.md` rotated and no longer contains `AI_FUNNEL_NAV_POLISH`.
Solution: Treat the persisted status, rationale, and log files as the authoritative anti-amnesia record for this resumed task, and record the missing prompt instead of extracting a neighboring block.
Rejected Alternatives: Borrowing the active fauna/noise/mission prompts was rejected because that would violate strict prompt isolation. Fabricating a new XML extraction was rejected as false evidence.
Scalability potential: No runtime effect; keeps parallel-agent documentation coherent under batch rotation.
Hardware Impact: None; documentation integrity only.

Problem: Adjacent vegetation-navigation support code still had floating divisions and weak finite handling in shared direction and structure-grid helpers.
Solution: Replace float divisions in speed inverse-lerp, retention, flow-field sampling, structure-grid mapping, threat propagation, flow obstacle gating, thermal/depth bands, wake falloff, and HLOD fade with reciprocal/multiply or literal reciprocal constants. `DominantAxisOrDefault` now rejects non-finite input/fallback vectors, and structure-grid range/index helpers reject corrupt transforms before hash lookup.
Rejected Alternatives: Rewriting integer index decomposition divisions was rejected because those are exact grid-coordinate operations, not scalar normalization. Adding new caches or containers was rejected because this is hot native code and the reciprocal sweep removes enough scalar cost without state.
Scalability potential: Low/MX350 removes recurring divide latency and avoids corrupt structure-grid probes. Middle/High/Ultra keep identical valid-data behavior while retaining budget for denser flow/navigation visuals.
Hardware Impact: Expected low-end gain is from replacing repeated scalar divides in Burst jobs with reciprocal multiplies and avoiding invalid hash probes. Exact microseconds remain pending profiler data because dotnet rebuilds are prohibited.

Problem: Bridge-side threat/flow feeder code could still bypass the hardened funnel/A* path by sampling incomplete native grids or non-finite environmental extents.
Solution: Threat chunk hashes, artificial-structure hashes, abyssal flow sampling, surface threat sampling, and echo sampling now reject corrupt transforms and require 64-bit complete native lengths before indexed reads. Flow output is finite-checked after trilinear interpolation, and non-finite interpolated threat resolves to zero influence instead of a NaN path-cost contaminant.
Rejected Alternatives: Trusting initialization flags or native array creation alone was rejected because stale resolution metadata can outlive payload resize failures. Broadly rewriting canopy/terrain samplers was rejected because those are outside the AI funnel/navigation domain for this prompt.
Scalability potential: Low/MX350 avoids invalid threat/flow probes and the steering correction they cause. Middle keeps the same cheap reciprocal mapping. High/Ultra can spend saved cycles on richer threat/flow visuals only when the payload has complete proof.
Hardware Impact: Adds a few scalar finite and length checks before sampled reads, while removing divide pressure from target hash/sampler paths. Expected low-end win is fewer corrupt-grid expansions and no NaN recovery churn; exact microseconds remain pending Unity profiler data because dotnet rebuilds are prohibited.

Problem: Threat service APIs could still accept corrupt inputs and stale snapshot counts after the lower-level samplers were hardened.
Solution: External threat pulses now fail closed on non-finite inputs; artificial structure registration rejects non-finite or non-positive bounds; flow fallback and conduit APIs reject non-finite inputs/outputs and stale managed arrays; threat hotspot scanning proves complete grid length before O(N) reads; nearest-node lookup clamps search count to both managed and native snapshot lengths.
Rejected Alternatives: Clamping NaN pulse data into zero-radius state was rejected because it still mutates route pressure. Relying on later hash/sampler guards was rejected because public service APIs are the domain boundary for fauna steering. Rebuilding or reallocating snapshots was rejected because this pass is hot-path fail-closed hardening, not ownership migration.
Scalability potential: Low/MX350 gets deterministic cheap rejection of corrupt route pressure and stale node snapshots. Middle retains existing behavior on valid data. High/Ultra can keep stronger conduit/threat visuals without increasing recovery logic because invalid inputs never enter the route consumers.
Hardware Impact: Adds scalar guards and count clamps; expected low-end win is avoiding invalid nearest-node scans, NaN steering vectors, and corrupt hotspot loops. Exact microseconds remain pending Unity profiler data because dotnet rebuilds are prohibited.

Problem: The shared flow-field sampler guarded finite coordinates but still indexed by `resolution` without proving the native flow buffer length matched that metadata.
Solution: Add 64-bit complete-grid length proof and finite half-extent proof before bilinear flow-field reads. Existing `DominantAxisOrDefault` remains the cheap Low-tier output sanitizer for non-finite sampled vectors.
Rejected Alternatives: Relying on flow-field initialization was rejected because stale resolution metadata can survive partial resize failures. Rebuilding the flow field inside the sampler was rejected because sampling must stay O(1), zero-GC, and side-effect-free.
Scalability potential: Low/MX350 gets cheap rejection of corrupt flow payloads instead of invalid steering. Middle/High/Ultra retain the same valid-data flow fidelity and can spend cycles on denser visual current fields without changing the sampler contract.
Hardware Impact: Adds one 64-bit multiply and length compare before sampled reads; expected win is fault avoidance, not a pure throughput claim. Exact microseconds remain pending Unity profiler data because dotnet rebuilds are prohibited.

Problem: Public threat/flow payload exports and local hotspot updates still trusted initialized native arrays and resolution metadata without proving declared cell-count coherence at the API boundary.
Solution: Add shared square-grid and voxel-grid cell-count proof helpers, route public float/compressed/echo threat views through complete-grid guards, require finite metadata in payload getters, and harden hotspot scans against stale native lengths and non-finite threat samples.
Rejected Alternatives: Trusting `_threatGridInitialized` or `NativeArray.IsCreated` was rejected because partial resize failures can leave created arrays with stale metadata. Repairing or reallocating payloads inside getters was rejected because these APIs must remain zero-GC, side-effect-light, and safe for hot fauna/path consumers.
Scalability potential: Low/MX350 gets cheap fail-closed rejection of corrupt threat/flow payloads and avoids invalid steering correction. Middle keeps identical valid-data behavior. High/Ultra can spend navigation budget on richer threat/flow visuals only when exported payloads have complete proof.
Hardware Impact: Adds fixed scalar length/finite checks before exposing native arrays and before O(N) hotspot scans. Expected low-end gain is avoided invalid route pressure and stale-grid scanning; exact microseconds remain pending Unity profiler data because dotnet rebuilds are prohibited.

Problem: Swarm wake and external threat pulse ingress could still retain corrupt route-pressure state even after service-level ingress guards, due to local cached state and public wake registration.
Solution: Reject non-finite wake positions/vectors/radius/lifetime before allocating/using flow buffers, and reject non-finite external pulse position/radius/strength/timer before merging pulse state into emission snapshots.
Rejected Alternatives: Clamping NaN to zero was rejected because it mutates route-pressure state into plausible but false steering. Allocating diagnostic containers was rejected because the hot route-pressure path must stay zero-GC.
Scalability potential: Low tier drops invalid impulses cheaply. Middle/High/Ultra keep stronger valid wake/threat visuals without carrying corrupt cached pressure.
Hardware Impact: Avoids invalid flow/threat writes and later steering recovery. Valid wake registration pays only a few finite checks before the existing native write.

Problem: Direct native view properties could bypass the safer payload getters and expose stale flow, nav-node, or completed-path buffers with counts that no longer matched backing native memory.
Solution: Route `EcosystemFlowField` through complete square-grid and finite-metadata proof, clamp `ActiveAbyssalNavNodeCount` to both managed and native snapshots, and clamp `ActiveAbyssalPathCount` to the native path buffer before returning direct views.
Rejected Alternatives: Removing direct native properties was rejected because existing consumers may use them as low-overhead read-only views. Returning raw buffers with separate unclamped counts was rejected because it exports stale state across the navigation boundary.
Scalability potential: Low/MX350 avoids invalid native reads and steering work from stale snapshots. Middle keeps current valid-data behavior. High/Ultra can keep direct native readback without adding managed copies.
Hardware Impact: Adds only scalar count clamps and created checks at property access. Expected gain is fault avoidance and lower recovery pressure rather than measurable throughput until runtime profiling is available.

Problem: The node-type payload getter could export `_abyssalNavNodeCount` directly and either overrun a shorter node-type array or become over-strict if reused through the full nav-graph conduit count clamp.
Solution: Add `ResolveAbyssalNavNodeTypeViewCount`, clamping node-type payload count to the proven node snapshot count and the node-type native length, while keeping conduit-vector/strength requirements only in the full nav-graph/conduit payload getters.
Rejected Alternatives: Reusing the full graph clamp for node types was rejected because node classifications should not fail just because conduit metadata is unavailable. Keeping `_abyssalNavNodeCount` was rejected because stale counts are exactly the boundary fault this pass is removing.
Scalability potential: Low/MX350 gets cheap fail-closed payload counts without extra copies. Middle/High/Ultra keep direct native readback while preserving node classification availability independent from conduit polish data.
Hardware Impact: Adds one native length clamp at payload access; expected gain is avoidance of invalid indexed reads and recovery churn, not measurable throughput until profiler capture.

Problem: The conduit-only payload getter still reused the full nav-graph count proof, so current steering could disappear when node-type metadata was unavailable even though conduit vectors and strengths were complete.
Solution: Add `ResolveAbyssalConduitViewCount` for node/conduit-vector/conduit-strength proof, use it in `TryGetAbyssalCurrentConduitPayload`, and make the full graph proof compose conduit proof plus node-type proof.
Rejected Alternatives: Keeping `ResolveAbyssalNavGraphViewCount` in the conduit getter was rejected because node classifications are not required to expose current conductor metadata. Duplicating all conduit clamps inside the full graph helper was rejected because two count proofs can drift under parallel edits.
Scalability potential: Low/MX350 keeps cheap current steering available when optional classification metadata is missing. Middle/High/Ultra preserve stricter full-graph export while keeping high-fidelity conduit visuals independent from node-type payload churn.
Hardware Impact: Adds no allocation and no extra native containers. Static gain is fault isolation at payload boundaries; expected low-end benefit is avoided fallback steering/recovery when only node-type metadata is stale.

Problem: Voxel macro-obstacle snapshot allocation counted flora obstacles from metadata only, while writing skipped entries whose matrix-derived world bounds were invalid. That can leave uninitialized native snapshot tail entries consumed by macro portal routing before funnel smoothing.
Solution: Count macro flora obstacles through `TryResolveMacroFloraObstacleWorldBounds`, add finite/positive bounds proof to that resolver, and clamp the writer to remaining snapshot capacity.
Rejected Alternatives: Zero-filling skipped snapshot entries was rejected because it creates false obstacles at plausible coordinates. Broad voxel navgrid reciprocal rewrites were rejected because this cross-domain touch is justified only at the vegetation-to-voxel route-obstacle interface.
Scalability potential: Low/MX350 avoids route rebuilds and steering corrections from corrupt obstacle primitives. Middle/High/Ultra can keep richer flora obstacle density without letting invalid vegetation transforms poison portal routing.
Hardware Impact: Adds a second bounds proof during cold snapshot counting, not per-frame funnel math. Expected gain is fault avoidance and removal of invalid-route recovery churn; exact microseconds remain pending Unity profiler data because dotnet rebuilds are prohibited.

Problem: Macro route record lookup and passability sampling still accepted stale voxel records with non-finite bounds and used raw cell-size division in safe-node/passability/dynamic-obstacle conversion paths.
Solution: Add `HasValidRecordBounds`, fail closed on non-finite route inputs, use `math.rcp(record.CellSize)` after proof, and convert dynamic obstacle chunk sizing/timing to reciprocal math.
Rejected Alternatives: `math.max(cellSize, epsilon)` was rejected because it hides corrupt record metadata and fabricates plausible voxel coordinates. Rewriting unrelated portal graph internals was rejected because this pass only owns the route handoff feeding funnel smoothing.
Scalability potential: Low/MX350 avoids bad nearest-node probes and dynamic obstacle updates from corrupt voxel records. Middle/High/Ultra can keep larger route volumes and obstacle density without invalid record metadata poisoning macro routing.
Hardware Impact: Removes several scalar float divisions from route sampling/update paths and adds cheap finite checks. Exact microseconds remain pending Unity profiler data because dotnet rebuilds are prohibited.

Problem: Macro portal A* could seed route scratch state from invalid portal centroids/radii, accept non-finite edge costs, and void-reconstruct a partial or cyclic parent chain into the path scratch used before funnel smoothing.
Solution: Add portal-node finite validation, skip invalid portals at graph rebuild, compute portal centroid with reciprocal math, finite-gate G/F/edge scores, make open-set pop reject non-finite priorities, and make route reconstruction bounded and boolean.
Rejected Alternatives: Clamping NaN centroids to zero was rejected because it creates fake portals. Leaving reconstruction void was rejected because path scratch is route authority for the funnel feeder. Replacing the list-based macro A* with new native containers was rejected because the existing capped cold scratch lists are already zero-GC and outside the Burst funnel job.
Scalability potential: Low/MX350 fails closed on corrupt portal records instead of spending steering/funnel work on invalid macro routes. Middle/High/Ultra can keep dense portal graphs and richer obstacle geometry without letting corrupt portal geometry poison route smoothing.
Hardware Impact: Adds scalar finite checks and removes one centroid division via `math.rcp`; expected gain is invalid-route fault avoidance and fewer downstream corrections. Exact microseconds remain pending Unity profiler data because dotnet rebuilds are prohibited.

Problem: Shared voxel route-record validation still proved positive dimensions but not that the native passability buffer covered the declared `x*y*z` volume, and inverted finite bounds could pass containment math.
Solution: Extend `HasValidRecordBounds` with a 64-bit expected-cell-count proof against `record.Current.Length` and require finite `record.Max >= record.Origin`.
Rejected Alternatives: Relying on every caller to re-check flat indices was rejected because route record validity is the shared authority boundary. Clamping inverted bounds was rejected because it fabricates route volume geometry.
Scalability potential: Low/MX350 rejects corrupt voxel records before nearest-node, portal, and passability probes. Middle/High/Ultra keep larger route volumes without risking stale native length reads.
Hardware Impact: Adds one 64-bit multiply chain and length compare per record candidate; expected benefit is avoided invalid memory reads and downstream route recovery. Exact microseconds remain pending Unity profiler data because dotnet rebuilds are prohibited.

Problem: Direct passability payload getters could still expose a created native array from a stale voxel record without reusing the shared complete-record proof.
Solution: Route `TryGetPassabilityPayload(HectonVoxelVolume)` and `TryGetContainingPassabilityPayload` through `HasValidRecordBounds` before exporting passability, dimensions, origin, and cell size.
Rejected Alternatives: Keeping local `record.Current.IsCreated` checks was rejected because created native memory says nothing about dimensions, bounds ordering, cell size, or complete length. Copying passability into a repaired buffer was rejected because this boundary must stay zero-GC and side-effect-light.
Scalability potential: Low/MX350 rejects corrupt direct LoS payloads before funnel smoothing spends DDA work. Middle/High/Ultra keep direct native readback only when backing voxel records are complete.
Hardware Impact: Adds a shared scalar proof before direct payload export; expected benefit is avoided invalid LoS sampling and downstream route repair. Exact microseconds remain pending Unity profiler data because dotnet rebuilds are prohibited.

Problem: Hybrid navigation mode sampling could accept non-finite probe positions, non-finite cached terrain heights, or weakly validated voxel records before selecting cave/open-water route modes.
Solution: Reject non-finite world positions, accept terrain fallback only when finite, reuse `HasValidRecordBounds`, and finite-check the resolved voxel cell origin before returning cave/solid mode.
Rejected Alternatives: Falling back to open water on NaN was rejected because it hides corrupt route probes and can route through geometry. Clamping terrain height or cell origin was rejected because it fabricates navigable floor data.
Scalability potential: Low/MX350 avoids wasted macro route attempts from corrupt probes. Middle/High/Ultra keep hybrid terrain/voxel routing only when the selected mode is backed by finite data.
Hardware Impact: Adds scalar finite checks before mode selection; expected benefit is avoided failed route scheduling and invalid funnel input. Exact microseconds remain pending Unity profiler data because dotnet rebuilds are prohibited.

Problem: Macro portal route emitters still trusted `_routePathScratch` after solve when writing managed-array and `NativeList` waypoint outputs.
Solution: Add `CanEmitPortalRoutePath` as a shared zero-GC emit gate that proves non-empty bounded scratch count, output capacity, portal graph index bounds, and portal-node validity before any waypoint write.
Rejected Alternatives: Duplicating index checks in both emitters was rejected because the two output paths would drift. Trusting `ReconstructRoute` alone was rejected because output emission is the last authority boundary before funnel consumers.
Scalability potential: Low/MX350 fails closed before writing corrupt macro waypoints. Middle/High/Ultra keep dense portal route output only when the path scratch and graph are coherent, preserving budget for richer route visuals and steering polish.
Hardware Impact: Adds one bounded linear scratch validation before route output; expected gain is avoided invalid waypoint emission and downstream funnel recovery. Exact microseconds remain pending Unity profiler data because dotnet rebuilds are prohibited.

Problem: Portal rebuild and reconstruction still had weaker local proofs than the shared route-record and portal-node authority.
Solution: Recheck nearest/fallback records with `HasValidRecordBounds`, require the shared record proof before portal rebuild, validate current graph nodes during matching, validate neighbor-relax indices before graph access, and validate portal graph indices/nodes during reconstruction before adding scratch path entries.
Rejected Alternatives: Trusting dictionary traversal, `Current.IsCreated`, or the final emit guard alone was rejected because route authority should be proven at every boundary where stale state can enter or persist. Adding new route containers was rejected because the existing capped scratch lists remain allocation-free.
Scalability potential: Low/MX350 avoids corrupt portal rebuilds and invalid scratch paths before funnel smoothing. Middle/High/Ultra preserve dense portal routing only when record, graph, and scratch state stay coherent.
Hardware Impact: Adds scalar guards on cold/warm route boundaries; expected gain is avoided route rebuild faults and downstream funnel recovery. Exact microseconds remain pending Unity profiler data because dotnet rebuilds are prohibited.

Problem: Full H-Phi source audit timing was unstable under current repo load, and claiming a domain score from route-boundary edits would be false evidence.
Solution: Run the fast `-CoreGraphOnly -Summary` audit as static graph evidence and record exact debt counts without claiming score movement from source-only changes.
Rejected Alternatives: Rerunning the timed-out full JSON audit was rejected because it already exceeded 120 seconds. Claiming H-Phi improvement from local code hardening without the metric completing was rejected as fake reporting.
Scalability potential: No runtime effect; keeps architecture evidence honest while runtime route hardening improves domain reliability.
Hardware Impact: None at runtime; audit-only documentation evidence.

Problem: Nav-grid build metadata and chunk-id generation could still accept non-finite origin/cell-size state or use raw scalar division before portal graph identity was established.
Solution: Add finite/positive build metadata proof, 64-bit expected point-count coverage, SDF patch finite fallback to dirty rebuild, shared record proof for dynamic-update scheduling, and reciprocal chunk coordinate mapping via `math.rcp(chunkSpan)`.
Rejected Alternatives: Keeping `math.max(cellSize, epsilon)` and raw division in `ComputeChunkId` was rejected because it masks corrupt metadata and still violates the reciprocal math gate. Scheduling dynamic clears from non-finite patch extents was rejected because it can poison portal route updates.
Scalability potential: Low/MX350 avoids corrupt chunk IDs and failed dynamic obstacle clears before funnel smoothing. Middle/High/Ultra keep larger route volumes and richer obstacle updates only when build metadata is finite and buffer coverage is proven.
Hardware Impact: Removes three scalar divisions from chunk-id mapping and adds cheap scalar validation at build/update ingress; exact microseconds remain pending Unity profiler data because dotnet rebuilds are prohibited.

Problem: Portal scratch capacity proof still trusted constructor-created arrays and used int face-area products before scratch-length validation.
Solution: `EnsurePortalWorkCapacity` now rejects null scratch/portal arrays, and `TryResolveMaxFaceCells` computes face areas in 64-bit with explicit int-cap proof before comparing against scratch lengths.
Rejected Alternatives: Trusting constructor invariants was rejected because this file is being edited by parallel agents and scratch arrays are the route authority for portal flood fill. Keeping int multiplication was rejected because overflow can produce a false small face count.
Scalability potential: Low/MX350 avoids corrupt portal flood-fill setup from stale arrays or overflowed dimensions. Middle/High/Ultra keep larger voxel chunks only when scratch capacity is explicitly proven.
Hardware Impact: Adds cold-path scalar checks before portal rebuild; expected benefit is invalid portal rebuild avoidance, not measurable throughput. Dotnet rebuilds remain prohibited.

Problem: Pure-void scan block count used int addition plus `/ 64`, so corrupt large point counts could overflow before sizing the route-record pure-void block flags.
Solution: Add `PureVoidScanBlockShift`, compute the ceiling block count in 64-bit, shift by six for the fixed 64-cell block size, and clamp impossible overflows before returning.
Rejected Alternatives: Keeping the raw division was rejected by the reciprocal/no-raw-division gate. Leaving int addition was rejected because a false-small block count can under-prove pure-void metadata.
Scalability potential: Low/MX350 keeps pure-void route records fail-closed under corrupt counts. Middle/High/Ultra can keep larger voxel records with explicit sizing proof.
Hardware Impact: Removes one integer division from pure-void block sizing and avoids overflow-driven undersized metadata; exact microseconds remain pending Unity profiler data because dotnet rebuilds are prohibited.

Problem: `SchedulePureVoidScan` scheduled by `blockFlags.Length` and only prechecked the output buffer, leaving input length proof to the Burst job and letting stale spare block capacity execute.
Solution: Require created passability/distance/block buffers, require passability and distance lengths cover `pointCount`, require block flags cover `ResolvePureVoidBlockCount(pointCount)`, and schedule exactly the required block count.
Rejected Alternatives: Relying on job-side guards was rejected because the scheduler is the route-record authority boundary. Scheduling full flag capacity was rejected because spare capacity can carry stale metadata semantics even when ignored later.
Scalability potential: Low/MX350 avoids extra pure-void scan work and stale flag exposure. Middle/High/Ultra keep larger records with exact block scheduling.
Hardware Impact: Removes unnecessary block iterations when capacity exceeds required count and avoids invalid job dispatch; exact microseconds remain pending Unity profiler data because dotnet rebuilds are prohibited.

Problem: `PureVoidBlockScanJob.Execute` had an out-of-range block-index guard but still wrote `BlockFlags[blockIndex]` after the guarded branch.
Solution: Return before writing when the flag buffer is missing or the block index is outside bounds, compute block start/end in 64-bit, and write a bounded zero flag for starts beyond `PointCount`.
Rejected Alternatives: Trusting scheduler correctness alone was rejected because Burst jobs must fail closed when invoked with stale or corrupt native state.
Scalability potential: Low/MX350 avoids rare invalid pure-void metadata writes. Middle/High/Ultra keep exact pure-void scheduling without unsafe job fallback behavior.
Hardware Impact: Adds cheap branch proof inside one Burst job; expected gain is fault avoidance, not throughput. Dotnet rebuilds remain prohibited.

Problem: Dynamic obstacle scheduling proved record `Current` bounds but only checked the other update buffers for creation, so stale shorter `Next`, base, distance, or pure-void flag buffers could enter route-record jobs.
Solution: Add `HasCompleteDynamicUpdateBuffers` and `TryResolveVoxelCellCount`; dynamic update scheduling now requires every buffer to cover the proven voxel cell count and pure-void block count before scheduling reset/dilation/scan jobs.
Rejected Alternatives: Relying on downstream job index guards was rejected because the scheduler is the route update authority boundary. Duplicating dimension products in each caller was rejected because integer overflow proof must remain single-source.
Scalability potential: Low/MX350 fails closed before corrupt dynamic obstacle jobs. Middle/High/Ultra keep larger voxel records and richer dynamic obstacle updates only when buffer coverage is explicit.
Hardware Impact: Adds scalar length checks before job scheduling; expected gain is avoided invalid dispatch and route recovery. Dotnet rebuilds remain prohibited.

Problem: Pure-void snapshot acceptance could still compare `Current.Length` to `CurrentDistance.Length` and trust `PureVoidBlockCount` without proving that the scan count matched the declared voxel dimensions.
Solution: Reuse `HasValidRecordBounds`, recompute the required voxel cell count with `TryResolveVoxelCellCount`, recompute the required pure-void block count, and require exact `PureVoidBlockCount` plus flag coverage before releasing voxel buffers.
Rejected Alternatives: Keeping length equality was rejected because two stale equal buffers do not prove dimensional coverage. Accepting `PureVoidBlockCount <= flags.Length` was rejected because a stale shorter count can mark only part of a record as pure and release route authority buffers.
Scalability potential: Low/MX350 fails closed on stale pure-void metadata instead of routing through partially scanned volumes. Middle/High/Ultra keep pure-void fast paths only when the full declared voxel record is proven clean.
Hardware Impact: Adds scalar proof before a cold pure-void release path; expected gain is avoided false-pure route records and downstream funnel/steering recovery. Dotnet rebuilds remain prohibited.

Problem: Build scheduling compared existing record origin/cell size with normal float comparisons, so stored NaN or inverted metadata could under-report a change and preserve a stale pure-void record.
Solution: Add an existing-record metadata gate in `TryPrepareBuild` for finite origin/max, ordered bounds, finite positive cell size, and force change detection when the stored metadata is invalid.
Rejected Alternatives: Refreshing the metadata fields without forcing a rebuild was rejected because a stale pure-void flag would still claim route authority without a current scan. Clamping corrupt metadata was rejected because it fabricates a navigable volume.
Scalability potential: Low/MX350 rebuilds only when existing route-record metadata is corrupt instead of routing through unproven pure void. Middle/High/Ultra preserve pure-void fast paths when metadata is stable and finite.
Hardware Impact: Adds scalar finite/order checks during build scheduling; expected gain is avoided stale pure-void route records and downstream funnel correction. Dotnet rebuilds remain prohibited.

Problem: Dynamic obstacle scheduling copied and rescanned by backing-buffer length, so spare native capacity could overrun paired buffers or make pure-void scan authority differ from declared voxel dimensions.
Solution: Recompute the declared voxel cell count, copy passability/distance buffers by that exact count, pass that count into `SchedulePureVoidScan`, and compute partial-update region point count in 64-bit before scheduling.
Rejected Alternatives: Copying `record.Current.Length` was rejected because current/next buffers can have stale capacity mismatch. Passing `record.Next.Length` into the pure-void scan was rejected because scan authority must be dimension-derived, not capacity-derived.
Scalability potential: Low/MX350 avoids spare-capacity scan work and stale pure-void flags after dynamic obstacle updates. Middle/High/Ultra keep richer dynamic obstacle updates only when declared record dimensions remain the single source of truth.
Hardware Impact: Avoids invalid copy lengths and unnecessary pure-void scan cells when native capacity exceeds declared cell count. Dotnet rebuilds remain prohibited.

Problem: After exact dynamic rescans were restored, stale `PureVoidBlockCount` metadata could still prevent valid pure-void updates from taking the buffer-release fast path and force unnecessary portal rebuilds.
Solution: Derive `requiredBlockCount` from the declared voxel cell count, prove flag coverage, and write `record.PureVoidBlockCount = requiredBlockCount` immediately before scheduling the exact pure-void rescan.
Rejected Alternatives: Leaving stale block metadata was rejected because it wastes the pure-void shortcut after safe updates. Blindly setting the count without flag coverage was rejected because it would make metadata claim more blocks than the native buffer can hold.
Scalability potential: Low/MX350 avoids avoidable portal rebuilds after clean dynamic updates. Middle/High/Ultra preserve the high-density obstacle update path while still releasing buffers for fully pure records.
Hardware Impact: Adds a scalar block-count proof and preserves the cold pure-void fast path; expected gain is avoided portal rebuild churn. Dotnet rebuilds remain prohibited.

Problem: Dynamic obstacle ingress could accept non-finite centers, non-finite extents, or overflowed expansion results into the persistent obstacle list before later region guards rejected the clear request.
Solution: Add shared finite/positive obstacle bounds proof across growth ingress, destroyed-organic ingress, clear enqueue/dequeue, persistent list registration/removal, and snapshot export; reuse invalid persistent slots and branch-wrap the overwrite cursor instead of modulo.
Rejected Alternatives: Relying on `TryResolveDynamicUpdateRegion` was rejected because persistent obstacles can poison future macro snapshots before a clear request is processed. Clearing the whole list on one invalid entry was rejected because it would drop valid long-lived route obstacles.
Scalability potential: Low/MX350 avoids route snapshot corruption and unnecessary rebuilds from bad obstacle primitives. Middle/High/Ultra keep dense persistent obstacle polish while invalid slots self-heal under bounded registration.
Hardware Impact: Adds scalar finite checks and removes one integer modulo from capped overwrite maintenance; expected gain is avoided macro-route recovery and stale obstacle churn. Dotnet rebuilds remain prohibited.

Problem: Finite obstacle centers/extents could still overflow when converted into min/max request bounds or when two persistent obstacle centers were averaged during merge.
Solution: Reject non-finite request min/max bounds before voxel conversion, compute merged centers with `center + delta * 0.5f`, and replace the slot with the new valid obstacle if the merged primitive is not finite and positive.
Rejected Alternatives: Clamping infinities back into the record bounds was rejected because it fabricates a route obstacle. Dropping the new obstacle on merge overflow was rejected because the new primitive is already proven valid and should keep route authority.
Scalability potential: Low/MX350 avoids invalid giant route update regions. Middle/High/Ultra can retain dense persistent obstacles without overflowed merge data poisoning macro routing.
Hardware Impact: Adds scalar finite checks on cold dynamic-obstacle maintenance; expected gain is avoided invalid voxel conversion and route rebuild churn. Dotnet rebuilds remain prohibited.

Problem: Obstacle snapshot allocation could count collider or persistent obstacle entries that later write-side finite guards skipped, leaving uninitialized native snapshot slots that the Burst stamp job would still scan.
Solution: Share collider finite/positive bounds proof between count and write, count only valid persistent dynamic obstacles, and add a Burst-side obstacle primitive guard before min/max stamping.
Rejected Alternatives: Zero-filling the snapshot was rejected because a zero-size primitive can still carry ambiguous route meaning near origin. Allocating a second compacted snapshot was rejected because it adds cold allocation churn where count/write parity solves the authority gap.
Scalability potential: Low/MX350 avoids invalid obstacle stamping and route recovery from uninitialized snapshot tails. Middle/High/Ultra keep dense obstacle snapshots with exact count/write parity and job-local fail-closed proof.
Hardware Impact: Adds scalar finite checks during cold snapshot creation and obstacle scan; expected gain is avoided false solid cells and macro route rebuild churn. Dotnet rebuilds remain prohibited.

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

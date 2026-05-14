# BIOME_TRANSITION_BLENDER Rationale

Agent: ENVIRONMENT_ENGINEER
Domain: World Generation & Terrain / Biome Transition Manager
Status: PENDING VERIFICATION

## Initial Scope Decision

Problem: Biome boundaries must stop depending on trigger volumes and become deterministic mathematical blend zones.
Solution: Build a NativeArray-backed heatmap sampler and publish a compact gradient signal instead of direct subsystem calls.
Rejected Alternatives: Standard Unity trigger colliders and per-frame MonoBehaviour Update would create order dependence, hidden allocations, and poor AUP behavior.
Scalability potential: Low uses a 3x3 kernel; Middle/High use a 5x5 kernel; Ultra can spend saved cycles on denser visual transition consumers without changing the biome contract.
Hardware Impact: Low-end i3/MX350 avoids trigger broadphase cost and managed event churn; target sample path is tens of microseconds on slow tick, not frame-critical.

## State Tracking Decision

Problem: Multiple agents are modifying the same project and chat context can be compressed.
Solution: Keep status and rationale on disk, update after each task loop, and use signal contracts to avoid invented direct dependencies.
Rejected Alternatives: Chat-only progress reports and direct cross-domain references to systems that may be edited by other agents.
Scalability potential: Disk-led audit trail lets integration verify blocked work and exact code ownership.
Hardware Impact: No runtime impact; avoids integration churn that would risk broken builds.

## Signal Contract Decision

Problem: Lighting, fauna, and audio need biome transition data without hard-calling each other or resurrecting singleton routing.
Solution: Added `BiomeGradientSignal` with byte biome IDs, source hashes, AUP, blend, boundary distance, cell size, frame, and flags. Delivery uses `SignalBus<BiomeGradientSignal>` so snapshots are flushed at PRE_SIMULATION.
Rejected Alternatives: `BiomeManager.Instance`, direct references to GI/audio/ecology, and UnityEvents. Those create hidden order dependencies and managed dispatch cost.
Scalability potential: Low consumes one compact signal; Middle/High can add richer shader/audio responses using the same payload; Ultra can overdrive visual fog/SH/audio layers without touching the sampler.
Hardware Impact: i3/MX350 path avoids object lookups and managed multicast; expected signal push/read cost is sub-microsecond relative to slow tick.

## SDF/IDW Sampling Decision

Problem: The biome heatmap is discrete, so naive sampling causes amateur hard cuts at biome boundaries.
Solution: Hydrate DataMonolith biome hashes into persistent `NativeArray<byte> GlobalBiomeMap` plus hash mirror, then run a Burst `IJob` using 5x5 IDW with boundary-distance boost. Exact center distance clamps to `0.0001f`.
Rejected Alternatives: Trigger colliders, `Dictionary<byte,float>` aggregation, and per-frame texture reads. They are order-dependent or allocation-prone.
Scalability potential: Low uses 3x3; Middle/High use 5x5; Ultra can layer additional consumer-side visual overkill without increasing producer kernel size.
Hardware Impact: Low-end i3/MX350 does 9 weighted samples instead of 25; no GC in the hot sampling path; cold 256x256 heatmap hydration only runs on DataMonolith checksum change.

## Consumer Consequence Decision

Problem: Transition data must visibly and audibly affect the world, not just exist as a number.
Solution: GI relay lerps fog/SH tint, ecosystem spawn pressure reads blend for biomass/spawn rates, and music atmosphere layer crossfades using the signal.
Rejected Alternatives: New parallel render/audio/ecology managers or per-biome AudioSource spawning. Those add orchestration debt and runtime object churn.
Scalability potential: Low gets cheap scalar biasing; Middle/High get smoother existing-layer blends; Ultra can bind additional shader state from `_HectonBiomeGradientState`.
Hardware Impact: i3/MX350 pays only a span read and scalar math in existing ticks; no new GameObjects or per-frame allocations.

## Verification Decision

Problem: The full project compile is currently polluted by unrelated missing assembly/type errors from other domains.
Solution: Ran targeted `dotnet build Hecton8.Core.csproj` to expose the dependency wall, then ran a direct Roslyn probe on the new contracts/job with Unity references.
Rejected Alternatives: Claiming a clean compile or reverting other agents' files.
Scalability potential: Integrator can verify this slice once global assembly references are repaired; the local job contract already compiles in isolation.
Hardware Impact: No runtime impact; prevents a false green report from hiding integration risk.

## OMEGA POLISH CHANGES

Problem: The first Burst sampler version used direct divisions and an unconditional distance helper path.
Solution: Converted blend/weight math to `math.rcp` multiplications and replaced helper distance with `distanceSq * math.rsqrt(max(distanceSq, epsilon))`.
Rejected Alternatives: Leaving divisions in the kernel because it is "only SlowTick". SlowTick still runs on weak CPUs and should stay cheap.
Scalability potential: Low tier now gets the 3x3 kernel plus reciprocal math; High/Ultra keep 5x5 and can spend saved cycles in GI/audio/ecology consumers.
Hardware Impact: Estimated MX350/i3 gain is 1-4 us per biome sample job versus division-heavy 5x5 math; no change in memory footprint.

Problem: The job wrote to `Result[0]` even when a caller supplied no result array.
Solution: Guarded the output array before all map checks.
Rejected Alternatives: Assuming only the production runtime will ever schedule the job.
Scalability potential: Safer test harness and integrator use without adding branches to the valid hot path after the initial guard.
Hardware Impact: No meaningful runtime cost; prevents invalid memory access in bad harnesses.

Problem: Cross-domain edits were required for visible consequences.
Solution: Justified them as signal consumers only: GI relay, ecosystem, and music read `SignalBus<BiomeGradientSignal>` without owning biome data.
Rejected Alternatives: Direct dependencies from biome runtime into render/fauna/audio managers.
Scalability potential: Consumers can independently increase visual/audio overkill on higher tiers.
Hardware Impact: Existing tick paths pay only span read plus scalar math; no new GameObjects, AudioSources, or allocations.

## SECOND PASS QUALITY UPGRADE

Problem: Out-of-map AUP samples clamped the macro-cell index but still used unclamped sample coordinates, which could inflate distance math and weaken edge-biome determinism.
Solution: Clamp the sample coordinate to the heatmap domain and add an `OutOfBounds` flag to the contract/signal.
Rejected Alternatives: Letting huge outside-map distances bleed into the IDW math or failing the sample as missing data.
Scalability potential: Low tier gets deterministic edge results with 3x3; High/Ultra can still render a deliberate boundary effect from the flag.
Hardware Impact: i3/MX350 avoids wasted large-distance math and branchy failure recovery; estimated 1-3 us saved on out-of-domain slow-tick samples.

Problem: `byte` biome IDs can collide when record indices exceed 255 or fallback hash folding lands on the same byte.
Solution: Keep byte IDs for compact signal/UI lanes but aggregate IDW weights with hash-aware biome keys.
Rejected Alternatives: Widening the hot map to `uint` only, which would quadruple map memory, or accepting visually wrong blends between colliding biomes.
Scalability potential: Low keeps the 64 KB byte map; High/Ultra preserve hash fidelity for richer material/audio responses.
Hardware Impact: Maintains the 65,536-byte map footprint instead of moving to 262,144 bytes; collision check is a cheap integer comparison inside an already bounded 9/25-sample kernel.

Problem: The runtime scheduled an `IJob` and immediately completed it, paying worker scheduling overhead for a slow-tick scalar result.
Solution: Switch to `job.Run()` for synchronous Burst execution.
Rejected Alternatives: Leaving schedule/complete because the job is small, or moving sampling into `Update`.
Scalability potential: Low saves scheduler overhead; High/Ultra retain the same Burst math and can spend saved time in visual consumers.
Hardware Impact: Estimated 2-6 us saved per slow-tick sample on low-end CPUs by avoiding schedule/sync overhead.

Problem: No dev-only deterministic probe existed for edge cases.
Solution: Added `BiomeBoundarySdfSmokeTester` covering normal boundary blend, low-tier 3x3, out-of-bounds clamping, and same-byte/different-hash biome collisions.
Rejected Alternatives: Manual scene-only verification. Scene verification is still required, but it is not enough for deterministic math.
Scalability potential: The smoke catches low-tier and collision regressions before expensive scene testing.
Hardware Impact: No runtime impact; code compiles only under editor/development defines and allocates only during explicit smoke execution.

Problem: A neighboring `EcosystemDirector` brine sample path had double/float conversion compile errors after an AUP precision change.
Solution: Preserve the double shift math locally and compute the runtime brine height without calling float-only compiled APIs.
Rejected Alternatives: Reverting the AUP precision change or broad ecology/genome refactors.
Scalability potential: Keeps AUP precision stable for large worlds without pulling biome code into ecology internals.
Hardware Impact: No measurable frame cost; removes a compile blocker in the biome consumer file.

## THIRD PASS RUNTIME PRESENCE UPGRADE

Problem: The SDF sampler and signal were source-complete but not guaranteed to exist in every authored or cold-start scene, which would silently remove all biome transition consequences.
Solution: Add a guarded `ActiveRuntimeInstance` to `BiomeBoundarySdfRuntime`, suppress duplicate runtimes, author the component onto `[MANAGERS]`, and add a cold `BiomeBoundarySdfRuntimeBootstrap` fail-safe that attaches the producer only when no active runtime exists.
Rejected Alternatives: Relying on manual scene edits, adding a per-frame scene search, or modifying generated `.csproj` files. Generated project files explicitly drift from Unity source import and are not the ownership point.
Scalability potential: Low/Middle/High/Ultra all share one producer; authored scenes pay 0 us extra, while mis-authored scenes pay one cold GameObject/component attach and then use the same SlowTick/Burst path.
Hardware Impact: i3/MX350 steady-state impact is 0 us versus the previous sampler path; cold fail-safe allocation occurs once only in broken authoring and prevents the much larger visual failure of missing lighting/fauna/audio blending.

Problem: Editor authoring initially referenced `BiomeBoundarySdfRuntime` directly, but the current generated `Hecton8.Editor.csproj` saw the editor file before the generated core project listed the new runtime source.
Solution: Resolve the component by assembly-qualified type name inside authoring. Unity import still authors the real component when the source type exists, while stale generated projects compile without requiring direct csproj surgery.
Rejected Alternatives: Editing generated project files or removing the authoring integration. The former is overwritten; the latter leaves scene setup brittle.
Scalability potential: Authoring stays deterministic without expanding runtime dependencies; high-tier visual overkill still comes through the same signal.
Hardware Impact: No runtime impact; editor-only reflection runs during authoring rebuild, not gameplay.

Problem: Verification was previously polluted by parallel build invocations contending for the same `Temp/obj` outputs.
Solution: Shut down build servers, reran builds sequentially with `/p:UseSharedCompilation=false`, then reran the direct Roslyn sampler probe and source scans.
Rejected Alternatives: Accepting a file-lock failure as a real compile wall or reporting stale blocked status.
Scalability potential: Clean generated-project verification gives integration a better baseline while runtime Unity validation remains pending.
Hardware Impact: No runtime impact; build hygiene only.

## FOURTH PASS HOT-PATH HARDENING

Problem: `BiomeBoundarySdfRuntime.SlowTick` still risked periodic service polling for player and scalability state.
Solution: Cache the player context and scalability state, listen to `GlobalRegistry` hot-swap and scalability event lanes, use a 2-second missing-player cold rebind fallback, and apply 3-second hysteresis before changing low-tier kernel mode.
Rejected Alternatives: Polling `GlobalRegistry.Player` every `SlowTick`, polling scalability properties every sample, or flipping 3x3/5x5 kernels immediately on transient tier changes.
Scalability potential: Low keeps a stable 3x3 kernel on weak or low-memory devices; Middle/High keep 5x5 visual smoothness; Ultra can spend the saved registry/polling cost in consumers without changing the producer contract.
Hardware Impact: i3/MX350 avoids repeated registry property reads and avoids visual thrash from immediate math-LOD changes; hot path remains 0 B GC.

Problem: Edge sampling could duplicate cells because offsets were clamped after the offset loop.
Solution: Compute the clamped min/max cell window once, iterate only real heatmap cells, keep hash-aware boundary checks, and flag all-zero maps as `MissingMap`.
Rejected Alternatives: Letting clamp duplication over-weight map edges, treating an all-zero unhydrated map as valid biome 0, or widening the primary heatmap from 64 KB byte storage to a 256 KB uint map.
Scalability potential: Low tier gets cheaper and more deterministic 3x3 edge behavior; High/Ultra retain hash fidelity for richer material, fog, and audio response.
Hardware Impact: Estimated 2-4 us saved on map-edge low-tier samples on i3/MX350 by removing duplicate clamped sample work and inner-loop clamp branches.

Problem: The black-box dump was readable only if the decoder already knew the current struct layout and cursor semantics.
Solution: Prefix dumps with magic, version, entry count, struct size, capacity, cursor, origin-shift sequence, and sample sequence before writing the 300 telemetry entries.
Rejected Alternatives: Raw entry stream with no decoder metadata or managed JSON dump on fault.
Scalability potential: No gameplay cost; postmortem tools can decode Low/Middle/High/Ultra telemetry consistently even after struct changes.
Hardware Impact: No hot-path cost. Fault-path binary write stays outside the gameplay cadence.

Problem: The active `CURRENT_BATCH.md` no longer contains this agent XML block, but the agent protocol still requires disk-backed identity and task state.
Solution: Preserve authority from `Status_BIOME_TRANSITION_BLENDER.md` and `Rationale_BIOME_TRANSITION_BLENDER.md`, record the current-batch miss explicitly, and keep final state at `PENDING VERIFICATION` until Unity runtime proof exists.
Rejected Alternatives: Hallucinating the missing XML from memory or reporting runtime verification without Unity Play Mode/Profiler data.
Scalability potential: Integration gets a factual audit trail instead of stale chat-only claims.
Hardware Impact: No runtime impact.

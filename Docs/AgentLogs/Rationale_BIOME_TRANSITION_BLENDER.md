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

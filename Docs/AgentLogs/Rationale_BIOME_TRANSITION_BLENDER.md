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

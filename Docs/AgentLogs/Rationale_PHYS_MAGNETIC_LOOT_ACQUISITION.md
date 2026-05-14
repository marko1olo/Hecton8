# PHYS_MAGNETIC_LOOT_ACQUISITION Rationale

Status: PENDING VERIFICATION

## Decision 0 - Execution Boundary

Problem: Existing loot magnet implementation is unknown. Prompt demands Burst spatial pull, but AGENTS.md forbids guessing APIs and direct dependencies across parallel agents.

Solution: Scan current loot, inventory, AUP, vault, signal, audio, and VFX contracts before code. Implement only against existing contracts or create narrow local contracts under gameplay loot isolation where no contract exists.

Rejected Alternatives: Directly inventing `GlobalDataVault`, `ItemAcquiredSignal`, or `WAKE_TURBULENCE_COMPUTE` shapes would compile-risk the batch and create false dependencies.

Scalability potential: Low tier will prefer 10Hz snap or coarse pull; Middle uses scheduled spatial pull; High and Ultra can use stronger VFX/audio event density while preserving same gameplay truth.

Hardware Impact: Expected gain on i3/MX350 comes from removing PhysX trigger stay work and replacing it with contiguous SoA iteration. Exact microsecond proof is pending profiler/compile evidence.

## Decision 1 - Mandate Selection

Problem: Loot magnet touches physics avoidance, AUP math, Native jobs, inventory state, signal dispatch, debug telemetry, and presentation fakes.

Solution: Read the eight task-relevant mandate files recorded in Status before code.

Rejected Alternatives: Reading the full registry would create context noise; reading only physics would miss inventory/vault sovereignty and AUP shift safety.

Scalability potential: The selected mandates define low/middle/high/ultra behavior, especially Math LOD and fake-first VFX/audio.

Hardware Impact: Keeps MX350 path bounded by cadence and flat NativeArray access instead of Unity trigger overhead.

## Decision 2 - No Singleton / No Trigger Patch

Problem: Prompt references `LootMagnet.Instance` and loot `OnTriggerStay`, but current scans found no loot magnet singleton or loot-prefab trigger stay implementation.

Solution: Treat the purge tasks as scan-verified no-ops and add the new spatial pull path without creating replacement singleton dependencies.

Rejected Alternatives: Adding a new global singleton would preserve the architectural fault the task is trying to remove. Disabling project-wide trigger stay settings would affect construction/fluid trigger systems outside the assigned domain.

Scalability potential: Low/Middle/High/Ultra all avoid PhysX trigger fan-out for loot acquisition; higher tiers can spend saved CPU on VFX/audio density.

Hardware Impact: On i3/MX350 the expected gain is removal of per-collider trigger callbacks. Exact microseconds are pending profiler evidence.

## Decision 3 - Loot Module Boundary

Problem: Loot magnet needs new contracts but must not absorb unrelated inventory/core code or create dependencies on parallel agents.

Solution: Add `Hecton8.Gameplay.Loot.Contracts` for Burst structs/constants and `Hecton8.Gameplay.Loot` for the MonoBehaviour runtime bridge. Core expansion is limited to `SystemID.GameplayLoot`, five vault buffer IDs, and `AcousticPingSignal.ChannelLootZip`.

Rejected Alternatives: Editing `PickupItem` hot paths or adding loot contracts directly into `Hecton8.Core` would increase compile blast radius and tie gameplay to object identity.

Scalability potential: Low uses SlowTick snap; Middle uses normal pull; High/Ultra can increase authored radius/strength and add denser consumers on the same signal contracts.

Hardware Impact: Flat arrays reduce cache misses versus component walks. MX350 path only scans registry on SlowTick and never runs FastTick integration.

## Decision 4 - AUP-Space Burst Pull

Problem: Runtime float positions are unsafe across floating-origin shifts, and Rigidbody forces would reintroduce PhysX cost.

Solution: Burst job reads `AbsoluteUniversePosition` from `EntityAUPs`, computes AUP-space deltas, updates `EntityVelocities`, and writes the integrated AUP back to vault buffers. Division uses `math.rcp(math.max(distSq, 0.01f))`.

Rejected Alternatives: `Rigidbody.AddForce`, `Physics.OverlapSphere`, and transform-space distance checks all violate deterministic AUP/state ownership requirements.

Scalability potential: Low snaps instantly on radius entry; Middle integrates; High can raise radius; Ultra can pair the same AUP stream with heavier presentation.

Hardware Impact: Estimated 12-45 us for 4096 entities under Burst on desktop; MX350 avoids the 60Hz job entirely. Verification still required.

## Decision 5 - Presentation Cheats

Problem: Prompt asks for wake turbulence, audio, and GPU particle feedback without a direct `WAKE_TURBULENCE_COMPUTE` contract in the scanned code.

Solution: Emit existing `WakeGeneratedSignal` and `AcousticPingSignal` lanes from the Burst job. Presentation pings are stride-limited to 64 slots so prewarmed native signal queues do not grow under mass loot fields.

Rejected Alternatives: Direct compute-buffer writes would invent a dependency and collide with VFX ownership. Per-item AudioSource or ParticleSystem calls would allocate and scale poorly.

Scalability potential: Low gets acquisition snap with minimal presentation; Middle gets sparse zip/wake cues; High/Ultra VFX can subscribe to the same wake/acoustic lanes and overdraw deliberately.

Hardware Impact: MX350 path stays inside prewarmed native queues. Saved CPU is converted to visual/audio fake density only on higher tiers.

## Decision 6 - Black Box And Compile Wall

Problem: Critical runtime needs postmortem state, but Unity compilation could not be confirmed because MCP lost session and the generated core project already fails on unrelated assembly references.

Solution: Added a 300-entry fixed NativeArray telemetry ring and binary dump path `Docs/AgentLogs/Dump_PHYS_MAGNETIC_LOOT_ACQUISITION.bin` on non-finite detection. Status remains `PENDING VERIFICATION`; compile task is not claimed.

Rejected Alternatives: Logging strings every frame would allocate and produce noise. Claiming compile success from static inspection would be a fake report.

Scalability potential: Telemetry is constant-size across Low/Middle/High/Ultra. High-end visual overkill does not change crash evidence format.

Hardware Impact: One 300-entry persistent NativeArray is cold memory. Per-frame telemetry writes are fixed and below measurable gameplay cost; dump I/O only occurs on fault.

## OMEGA POLISH CHANGES

Problem: Polish mandate required removal of honest math and hot-path bloat after core tasks were checked/blocked.

Solution: Replaced per-signal `math.sqrt(PullRadiusSq)` with a precomputed `PullRadiusMeters` job field. Confirmed no `foreach`, `string.Format`, interpolated strings, `.ToString()`, `math.sqrt`, or `math.normalize` remain under `Assets/_Project/Scripts/Gameplay/Loot`.

Rejected Alternatives: Leaving the square root in signal emission would be acceptable for correctness but not for the frame-time dictatorship. A lookup table is unnecessary because radius is already authored once per schedule.

Cinematic Cheats used: Wake uses `WakeGeneratedSignal` as the marine-snow fake instead of compute-buffer coupling; LootZip audio uses sparse stride-limited acoustic pings; low tier snaps/acquires instead of integrating.

Scalability potential: Low = 10Hz snap/acquire. Middle = Burst pull and sparse VFX/audio. High = larger authored magnet radius. Ultra = denser downstream wake/audio consumers on the same signals without changing truth state.

Hardware Impact: Removed two square roots per emitted acoustic signal. At the 64-signal stride cap, worst-case saved work is roughly 64 sqrt operations/frame during dense pull fields. Exact microseconds remain pending profiler/Burst evidence.

Final Git Diff: `git diff --stat -- Assets/_Project/Scripts/Gameplay/Loot Assets/_Project/Scripts/Core/Memory/H8Memory.cs Assets/_Project/Scripts/Core/GlobalSignals.cs Docs/Tasks/Status_PHYS_MAGNETIC_LOOT_ACQUISITION.md Docs/AgentLogs/Rationale_PHYS_MAGNETIC_LOOT_ACQUISITION.md` reported 6 files changed, 138 insertions, 18 deletions. Current dirty `GlobalSignals.cs` and `H8Memory.cs` also contain unrelated concurrent/integrator edits; this task-owned gameplay polish diff is the `PullRadiusMeters` field/pass-through plus the status/rationale records.

Build Status: `dotnet build Hecton8.Core.csproj --no-restore -v:q -clp:ErrorsOnly` still fails before loot verification on missing cross-assembly references such as `Hecton8.Environment.Fluids`, `Hecton8.Core.Scheduling`, `Hecton8.Audio.Virtualization`, `Hecton8.Physics.CCD`, and related service types. Unity MCP console remained unavailable after refresh timeout. Status remains `PENDING VERIFICATION`.

## Decision 7 - SignalBus Correctness

Problem: Burst direct writes to `GlobalSignals.ItemAcquiredSignalWriter` only enqueue into the raw NativeQueue. Several systems, including inventory repair-side effects, read `SignalBus<ItemAcquiredSignal>.GetFrameSnapshot()` and would miss loot magnet acquisitions.

Solution: The Burst job now writes compact `LootMagnetSignalEvent` records into a persistent NativeArray. Late-frame commit publishes acquisition, acoustic, and wake signals through `GlobalSignals.Publish`, after actual inventory-added quantity is known.

Rejected Alternatives: Keeping raw queue writes would be faster on paper but semantically incomplete. Publishing inventory success from inside Burst is impossible because inventory is managed and must remain off the job thread.

Scalability potential: Low/Middle/High/Ultra all use the same event lane. Higher tiers can consume SignalBus audio/wake data without adding new dependencies.

Hardware Impact: Adds one fixed NativeArray write per active slot and keeps global signal queues prewarmed. It removes false acquisition pings when inventory is full and avoids downstream repair/inventory systems missing the event.

## Decision 8 - Dense Field Budget And AUP Math Upgrade

Problem: A dense loot cloud could try to stow hundreds of items in one late-frame pass, overrunning the 128-item signal lane and spiking inventory work. The job also used two absolute AUP conversions per entity.

Solution: Added `MaxAcquisitionsPerFrame = 64`, restoring deferred vault flags for the rest. Replaced double absolute conversions with direct AUP sector-delta math using the 5000 m cell size. Added a 50 ms integration delta clamp and required `PullEnabled` in the job mask.

Rejected Alternatives: Letting NativeQueue growth handle overload violates zero-GC intent. Full spatial hashing was rejected for this pass because the prompt's concrete task list demands linear SoA iteration over `EntityAUPs` and `EntityFlags`; the acquisition budget buys predictable behavior without new shared dependencies.

Scalability potential: Low remains 10Hz snap/acquire; Middle drains 64/frame; High/Ultra can raise presentation density downstream without changing the acquisition cap.

Hardware Impact: MX350 avoids bursty inventory work. Sector-delta math saves two AUP absolute conversions per candidate and reduces per-entity math pressure before Burst verification.

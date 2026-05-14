# PHYS_MAGNETIC_LOOT_ACQUISITION Status

Agent: GAMEPLAY_PROGRAMMER
Domain: Echelon 4 Gameplay / Scavenging & Inventory / AUP Kinematics
Prompt Source: Docs/Tasks/CURRENT_BATCH.md
Status: PENDING VERIFICATION

## Mandates Read

- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- DATA_Inventory_Resources_Items_SOA_Layout.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- PHYS_Physics_Integrity_Determinism_ForceMode.txt
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt

## Task Checklist

- [x] 01. SINGLETON ERADICATION | Justification: `rg` found no `LootMagnet.Instance` or `LootMagnet` implementation to purge; DOD used scan-before-edit | Alternative rejected: adding a replacement singleton facade | Estimate: 0 us/frame
- [x] 02. SIGNAL MIGRATION | Justification: Burst job writes compact acquisition events; late-frame commit publishes `ItemAcquiredSignal` through `GlobalSignals.Publish` after inventory quantity is confirmed | Alternative rejected: Burst writing only the raw native queue, which bypasses `SignalBus<ItemAcquiredSignal>` consumers | Estimate: 3-6 us/event under prewarmed queue
- [x] 03. ASMDEF ISOLATION | Justification: created `Hecton8.Gameplay.Loot` and `Hecton8.Gameplay.Loot.Contracts` with runtime -> contracts dependency | Alternative rejected: dumping code into `Hecton8.Core` | Estimate: 0 us/frame
- [x] 04. DEAD CODE HUNT | Justification: `rg` across gameplay scripts/prefabs found no loot `OnTriggerStay`; only non-loot trigger stay scripts exist | Alternative rejected: disabling project-wide trigger stay physics | Estimate: unknown saved until profiler, expected PhysX trigger cost removed for magnet path
- [x] 05. S.O.A. QUERY | Justification: runtime resolves `EntityAUPs`, `EntityFlags`, `EntityVelocities`, `EntityItemHashes`, and `EntityQuantities` from `GlobalDataVault` | Alternative rejected: per-pickup component scanning in `FastTick` | Estimate: 7-15 us SlowTick refresh at 256 pickups, 0 GC
- [x] 06. RADIUS CHECK | Justification: `LootMagnetPullJob` iterates vault entities flagged `Active|IsLoot` and computes AUP-space distance squared | Alternative rejected: PhysX overlap/sphere trigger query | Estimate: 12-35 us per 4096 entities on desktop Burst pending verification
- [x] 07. KINETIC PULL | Justification: velocity update uses normalized AUP delta and `math.rcp(math.max(distSq, 0.01f))` with max-speed clamp | Alternative rejected: Rigidbody force mode / direct PhysX force | Estimate: 18-45 us per 4096 entities pending Burst proof
- [x] 08. WAKE OVERRIDE | Justification: pulled/acquired loot emits `WakeGeneratedSignal` carrying AUP and velocity into existing procedural wake lane | Alternative rejected: direct marine snow compute-buffer dependency | Estimate: 1-2 us per emitted presentation signal, stride-limited
- [x] 09. AUDIO SYNC | Justification: `AcousticPingSignal.ChannelLootZip` uses `GlobalSignals.Publish`; intensity rises as `distSq` decreases and is visible to native queue and SignalBus consumers | Alternative rejected: one managed AudioSource per item | Estimate: 1-2 us per emitted ping, stride-limited to prewarmed lane
- [x] 10. AUTO-STOW | Justification: `distSq <= 0.25f` clears vault active flag, emits acquisition, and mirrors commit to legacy pickup inventory path | Alternative rejected: inventory mutation inside Burst | Estimate: event-bound, no per-frame cost when idle
- [x] 11. AUP SHIFT SAFETY | Justification: job operates on absolute AUPs, not runtime floats; runtime proxy conversion happens after late-frame job completion | Alternative rejected: transform-space distance during origin shifts | Estimate: avoids shift repair pass, 0 additional us/frame
- [x] 12. MATH LOD | Justification: low tier uses SlowTick scheduling and instant acquisition/snap when loot enters radius | Alternative rejected: same integration cadence on MX350 | Estimate: saves ~50 FastTick jobs/sec on low tier
- [x] 13. ZERO-GC | Justification: job uses only NativeArray vault buffers plus a persistent NativeArray event lane; managed sidecar allocation is OnEnable/capacity-change only | Alternative rejected: LINQ/list allocations, trigger callbacks, or unbounded native signal queue writes from Burst | Estimate: 0 B GC/frame by static inspection
- [x] 14. H-PHI DATA SOVEREIGNTY | Justification: loot active/pull/acquired state is modified through vault buffers; managed pickup proxy only mirrors vault result after completion | Alternative rejected: component fields as simulation truth | Estimate: state mutation remains contiguous, cache-friendly
- [ ] 15. OMEGA COMPILE CHECK [BLOCKED BY ENVIRONMENT] | Justification: Unity refresh timed out, MCP console unavailable, and `dotnet build Hecton8.Core.csproj` fails on pre-existing missing assembly references outside this task | Alternative rejected: claiming compile success without Unity/Burst evidence | Estimate: blocked

## Iteration Log

- Loop 0: Prompt extracted, domain read, eight mandates read. Codebase scan pending.
- Loop 1: Tasks 1-5 implemented/scanned. Unity compile request timed out after 60s; local core project fails before new loot asmdefs on unrelated missing references.
- Loop 2: Tasks 6-10 implemented. Static review fixed low-tier immediate acquisition and limited presentation signal stride to preserve prewarmed native lanes.
- Loop 3: Tasks 11-14 implemented. AUP math remains absolute until late-frame proxy mirror; vault state is authority. Task 15 remains pending verification.
- Loop 4: Polish mandate read after tasks checked/blocked. Removed per-signal square root by passing precomputed `PullRadiusMeters`.
- Loop 5: Anti-bloat scan completed: no `foreach`, `string.Format`, interpolated strings, `.ToString()`, `math.sqrt`, or `math.normalize` remain in loot module. `git diff --check` passed with line-ending warnings only.
- Loop 6: Rechecked signal consumers. Replaced Burst direct global queue writes with NativeArray event records and late-frame `GlobalSignals.Publish` so SignalBus consumers receive loot acquisition/audio.
- Loop 7: Rechecked AUP math and dense-field scaling. Replaced double absolute conversions with direct sector-delta math, added 50 ms integration clamp, required `PullEnabled`, and capped acquisition attempts to 64/frame.
- Loop 8: Rechecked asmdef bloat and static hygiene. Removed unused runtime `Unity.Burst`/`Hecton8.Core.Contracts` references; asmdef JSON valid; anti-bloat scan still clean.

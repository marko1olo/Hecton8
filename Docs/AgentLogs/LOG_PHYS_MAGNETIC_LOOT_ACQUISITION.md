# PHYS_MAGNETIC_LOOT_ACQUISITION Log

## 2026-05-14 - Zero-GC Spatial Loot Magnet

What was wrong:
- Prompted legacy failure mode was PhysX trigger-driven loot suction and/or singleton-owned `LootMagnet.Instance`.
- Repository scan found no current `LootMagnet.Instance` and no loot-prefab `OnTriggerStay` implementation. Non-loot trigger stay scripts remain outside domain.
- Existing pickup acquisition stayed component/inventory driven; no Burst SoA pull lane was available to own the magnet state.

What was done:
- Implemented isolated loot module shape under `Assets/_Project/Scripts/Gameplay/Loot` with contracts/runtime split.
- Added `LootMagnetPullJob` Burst path over vault-backed `EntityAUPs`, `EntityFlags`, `EntityVelocities`, `EntityItemHashes`, and `EntityQuantities`.
- Job checks `Active|IsLoot`, resolves AUP-space `distSq`, applies velocity with `math.rcp(math.max(distSq, 0.01f))`, clamps velocity, integrates AUP, and clears active state on acquisition.
- `LootMagnetSystem` registers FastTick/SlowTick/LateFrameTick, schedules jobs outside managed trigger paths, completes in late-frame swap window, and mirrors vault result to legacy pickup/inventory proxy only after vault truth changes.
- Low tier uses SlowTick and immediate snap/acquire on radius entry.
- Added fixed 300-entry NativeArray telemetry ring and non-finite dump path: `Docs/AgentLogs/Dump_PHYS_MAGNETIC_LOOT_ACQUISITION.bin`.
- Updated task status and rationale files with mandate compliance, rejected alternatives, scalability, and compile wall evidence.

Cinematic Cheats used:
- Wake turbulence is faked through existing `WakeGeneratedSignal`, not a direct marine-snow compute dependency.
- LootZip audio uses `AcousticPingSignal.ChannelLootZip` with intensity rising as distance squared falls.
- Presentation pings are stride-limited to preserve prewarmed native signal lanes.
- Low tier skips integration and uses snap/acquire.
- Omega polish removed per-signal `math.sqrt` by passing precomputed `PullRadiusMeters`.

Exact Microseconds saved:
- Verified exact profiler numbers: unavailable. Unity MCP compile/console was unavailable and local generated `Hecton8.Core.csproj` fails on pre-existing missing cross-assembly references.
- Static estimate: removes PhysX trigger callback cost from magnet path, expected 100+ us/frame in dense loot fields on i3/MX350 depending on collider count.
- Static estimate: Burst SoA pass over 4096 entities expected 12-45 us on desktop-class CPU, pending Burst proof.
- Low tier: saves roughly 50 FastTick jobs/sec by running acquisition at SlowTick cadence.
- Omega polish: removes up to 64 square-root operations/frame during dense presentation emission due stride cap.

Verification:
- `git diff --check` over touched task files: passed; line-ending warnings only.
- Anti-bloat scan under `Assets/_Project/Scripts/Gameplay/Loot`: no `foreach`, `string.Format`, interpolated strings, `.ToString()`, `math.sqrt`, or `math.normalize` matches.
- `dotnet build Hecton8.Core.csproj --no-restore -v:q -clp:ErrorsOnly`: failed on unrelated/global missing references before loot verification.
- Unity MCP `refresh_unity`: timed out after 60s; `read_console`: no Unity session available.

Final Status:
- PENDING VERIFICATION.

## 2026-05-14 - Professional Recheck Pass

What was wrong:
- Burst direct global signal writers only hit raw NativeQueues. `SignalBus<ItemAcquiredSignal>` and `SignalBus<AcousticPingSignal>` consumers would miss loot magnet signals.
- Dense loot clouds could stow too many items in one frame, risking native signal lane growth and inventory hitches.
- AUP distance math converted both player and loot to absolute double positions per entity.
- Runtime asmdef carried unused references.

What was done:
- Replaced direct Burst signal writes with `LootMagnetSignalEvent` records in a persistent NativeArray.
- Late-frame commit now publishes `ItemAcquiredSignal`, `AcousticPingSignal`, and `WakeGeneratedSignal` via `GlobalSignals.Publish`.
- Acquisition signal quantity is now based on actual inventory delta after `PickupItem.TryHandleInventoryPickup`; full-inventory failures restore active vault flags instead of reporting false acquisition.
- Added `MaxAcquisitionsPerFrame = 64` to bound late-frame inventory work and signal pressure.
- Replaced per-entity double absolute conversions with direct AUP sector-delta math.
- Added 50 ms integration delta clamp and required `PullEnabled` in the Burst job mask.
- Removed unused runtime asmdef references to `Unity.Burst` and `Hecton8.Core.Contracts`.

Cinematic Cheats used:
- Same visual fake path as before: sparse LootZip acoustic + wake signals; no direct compute dependency.
- Dense-field behavior drains over frames instead of simulating a physically honest pile collapse.

Exact Microseconds saved:
- Removed two AUP absolute conversions per checked entity.
- Capped inventory commit work to 64 attempts/frame.
- Exact profiler numbers remain unavailable due Unity MCP transport failure and global project build instability.

Verification:
- `rg` anti-bloat scan: no `NativeQueue`, direct signal writers, `math.sqrt`, `math.normalize`, `ToAbsoluteDouble3`, `foreach`, `string.Format`, or `.ToString()` in loot module.
- Interpolated-string scan: clean.
- Asmdef JSON parse: passed.
- `git diff --check`: passed with line-ending warnings only.
- Unity MCP: transport to `127.0.0.1:8088` failed.
- `dotnet build Hecton8.Core.csproj`: timed out / global build remains unsafe to use as proof; spawned build processes were stopped.

Final Status:
- PENDING VERIFICATION.

## 2026-05-14 - Bootstrap / Slot Identity / C# Verification Pass

What was wrong:
- The previous implementation could compile and still remain inert if no scene object owned `LootMagnetSystem`.
- Pickup registry order can shift after despawns or activation changes; preserving velocity by slot alone can push a newly occupying pickup with another pickup's old velocity.
- Unity MCP console remained unavailable, but C# verification against the loot asmdef needed evidence stronger than a DLL timestamp.

What was done:
- Added cold `AfterSceneLoad` runtime installation for `LootMagnetSystem`; it adopts an existing scene component or creates one bootstrap-owned root without exposing `LootMagnet.Instance`.
- Added a managed `int[]` pickup instance-id sidecar so per-slot velocity is retained only while the same `PickupItem` occupies that slot.
- Seeded the vault immediately on `OnEnable` after buffers resolve, removing the first SlowTick blind window.
- Compiled `Hecton8.Gameplay.Loot.Contracts` and `Hecton8.Gameplay.Loot` with Unity-generated Bee response files into `Temp/LootMagnetCompileCheck`; both compiler invocations exited 0.

Cinematic Cheats used:
- No change to truth state: vault AUP/flags remain authoritative.
- Presentation remains fake-first: sparse LootZip acoustic and wake signals, with low tier snapping instead of integrating.

Exact Microseconds saved:
- Prevented cross-slot velocity carry without adding FastTick work; cost is one `GetInstanceID()` per SlowTick pickup scan.
- Removed one-frame cold-start latency after component enable; no steady-state frame cost.
- C# compile verification has no runtime cost.

Verification:
- Unity Bee response-file C# compile: passed for contracts and runtime assemblies.
- Anti-bloat scan remains clean for direct native signal writers, `math.sqrt`, `math.normalize`, `ToAbsoluteDouble3`, `foreach`, `string.Format`, `.ToString()`, and LINQ markers under loot.
- `git diff --check` for loot module: passed with line-ending warnings only.
- Unity MCP console/Burst AOT proof: still blocked by transport failure to `127.0.0.1:8088`.

Final Status:
- PENDING VERIFICATION.
- Compile/Burst proof remains blocked by Unity session/global generated project state, not claimed.

## 2026-05-14 - Black-Box Fault Ordering Pass

What was wrong:
- The fault dump path could write `Dump_PHYS_MAGNETIC_LOOT_ACQUISITION.bin` before the fault frame itself entered the telemetry ring.
- LateFrame would then record after the dump, leaving the binary evidence one frame behind the detected non-finite state.

What was done:
- Moved committed acquisition/hash/fault counters before the dump branch.
- Records the fault frame immediately before binary dump.
- Added `_lastTelemetryRecordedFrame` so the normal late-frame path does not write the same frame twice.

Cinematic Cheats used:
- None. This is diagnostic correctness only.

Exact Microseconds saved:
- No meaningful frame-time save. Cost is one uint field and one late-frame branch.
- Debug value increased: the binary dump now contains the fault frame that caused the dump.

Verification:
- Split Unity Bee response-file C# compile: contracts exit 0, runtime exit 0.
- `git diff --check` on loot/status/rationale/log: line-ending warnings only.

Final Status:
- PENDING VERIFICATION.

## 2026-05-14 - Burst Dependency Surface Pass

What was wrong:
- `LootMagnetPullJob` still called `AbsoluteUniversePosition.FromAbsolutePosition` from another assembly while rebuilding integrated AUPs.
- The method is numeric, but the hot Burst path did not need that cross-assembly dependency.

What was done:
- Inlined AUP rebuild math inside `LootMagnetPullJob` using the same 5000 m sector size and local float offsets.
- Removed the last `FromAbsolutePosition` marker from the loot module anti-bloat scan.

Cinematic Cheats used:
- None. This is Burst surface reduction and AUP determinism hygiene.

Exact Microseconds saved:
- Removes one cross-assembly static call from each integrated loot update.
- Exact profiler numbers remain unavailable until Burst AOT/profiler can run.

Verification:
- Split Unity Bee response-file C# compile: contracts exit 0, runtime exit 0.
- Anti-bloat scan: no `FromAbsolutePosition`, `ToAbsoluteDouble3`, `math.sqrt`, `math.normalize`, direct native signal writers, `foreach`, `string.Format`, `.ToString()`, or LINQ markers in loot.
- Unity MCP console/Burst AOT proof: still blocked by transport failure to `127.0.0.1:8088`.

Final Status:
- PENDING VERIFICATION.

## 2026-05-14 - AUP Sector Broadphase Pass

What was wrong:
- The Burst job still resolved full local AUP delta for far-sector loot even when normal magnet radii make acquisition impossible.
- A full shared spatial hash would add state ownership and allocation risk beyond this prompt's SoA-vault path.

What was done:
- Added `AupCellSizeSq` and a guarded adjacent-cell reject before float delta math.
- The reject only applies when `PullRadiusSq <= AupCellSizeSq`; oversized debug radii keep exact behavior.

Cinematic Cheats used:
- None. This is deterministic broadphase pruning.

Exact Microseconds saved:
- Saves float3/double sector-delta math for any loot more than one 5 km AUP cell from the player.
- Exact profiler numbers remain unavailable until Burst/profiler can run.

Verification:
- Split Unity Bee response-file C# compile: contracts exit 0, runtime exit 0.
- `git diff --check` on loot module: line-ending warnings only.

Final Status:
- PENDING VERIFICATION.

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

## 2026-05-15 - H-Phi Continuation Pass

What was wrong:
- Active `Docs/Tasks/CURRENT_BATCH.md` no longer contains `PHYS_MAGNETIC_LOOT_ACQUISITION`, so prompt re-extraction cannot succeed from the current batch file.
- Fault dumps could be emitted before the fault frame entered the 300-frame telemetry ring.
- Pickup sidecar identity was stored as a truncated `int`, creating avoidable collision risk.
- `LootMagnetPullJob` still exposed a cross-assembly AUP rebuild call and did full delta math for far-sector loot.

What was done:
- Recorded the missing prompt extraction and continued from persisted task/rationale state plus the user's direct continuation.
- Recorded fault-frame telemetry before binary dump and suppressed duplicate same-frame telemetry writes.
- Changed the managed pickup sidecar to full `ulong` entity ids.
- Added guarded AUP-cell broadphase for radii within the 5 km cell size.
- Inlined AUP rebuild math inside the Burst job.

Cinematic Cheats used:
- No new visual dependency. Wake and acoustic presentation remain sparse signal fakes; low tier still snaps instead of integrating.

Exact Microseconds saved:
- Far-cell loot now avoids double/float sector-delta math before radius rejection.
- One cross-assembly static AUP rebuild call is removed from each integrated loot update.
- Sidecar identity hardening has no FastTick cost; cold memory rises by 16 KB at 4096 slots.

Verification:
- User forbade dotnet rebuilds; none were run.
- Unity MCP console tool is unavailable in this session.
- Static anti-bloat scan under `Assets/_Project/Scripts/Gameplay/Loot`: clean for direct native signal writers, `FromAbsolutePosition`, `ToAbsoluteDouble3`, `math.sqrt`, `math.normalize`, `foreach`, `string.Format`, `.ToString()`, and LINQ markers.
- `git diff --check` on loot code: passed with line-ending warnings only.

Final Status:
- PENDING VERIFICATION.
- Compile/Burst proof remains blocked by Unity session/global generated project state, not claimed.

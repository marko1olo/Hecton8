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
- Omega polish removed per-signal `math.sqrt` by keeping scheduled pull radius data out of presentation emission.

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

## 2026-05-15 - Idle Black-Box Continuity Pass

What was wrong:
- The telemetry ring only advanced after completed pull jobs, so idle frames were missing from the last-300-frame evidence.
- Recording while a job is still running would risk reading NativeArrays that Burst owns.

What was done:
- Added a telemetry frame counter independent from pull job frame ids.
- LateFrame records idle high-level state when no job is scheduled.
- Running jobs skip idle recording until completion; completed jobs and fault dumps still record exactly once for that frame.

Cinematic Cheats used:
- None. This is black-box correctness.

Exact Microseconds saved:
- No CPU saving claimed. This spends one fixed NativeArray write per idle late frame to buy better crash evidence.

Verification:
- User forbade dotnet rebuilds; none were run.
- Static loot anti-bloat scan remains clean.
- `git diff --check` on loot/status/rationale/log passed with line-ending warnings only.

Final Status:
- PENDING VERIFICATION.

## 2026-05-15 - Commit Accuracy And H-Phi Scale Pass

What was wrong:
- `ItemAcquiredSignal.Quantity` could be derived from a stale vault quantity if a pickup changed between SlowTick refresh and LateFrame commit.
- Consumed slots kept stale acquired state until the next registry refresh.
- Full-inventory acquisition rejection could keep PullEnabled and retry managed inventory work every FastTick.
- Presentation radius was recomputed per signal instead of using the scheduled pull radius.

What was done:
- Acquisition reporting now measures live pickup quantity before and after `TryHandleInventoryPickup`.
- Fully consumed slots clear pickup ref, entity id, AUP, flags, velocity, item hash, quantity, and signal event immediately.
- Zero-add rejections keep `Active|IsLoot` but drop `PullEnabled` until the next SlowTick registry refresh.
- Scheduled pull radius/radiusSq are cached once per job and reused for acoustic intensity.
- Default entity capacity stays 4096; authored high/ultra fields may scale to 8192 via `MaxEntitiesHardCap`.
- Idle telemetry now reports active slot samples and clears per-frame acquired/fault counters when no job ran.

Cinematic Cheats used:
- No new presentation dependency. LootZip remains sparse acoustic/wake signal fakes; low tier still snaps instead of integrating.

Exact Microseconds saved:
- Avoids repeated managed inventory/drop-overflow attempts every FastTick when inventory is full; exact number depends on rejected pickups.
- Removes one radius multiply and one max operation per emitted presentation signal.
- Clears consumed slots immediately so stale acquired slots do not survive into later FastTick job scans before SlowTick compaction.

Verification:
- User forbade dotnet rebuilds; none were run.
- Static loot anti-bloat scan remains clean for direct native signal writers, scene searches, `math.sqrt`, `math.normalize`, `ToAbsoluteDouble3`, `FromAbsolutePosition`, `foreach`, string formatting, `.ToString()`, and LINQ markers.
- `git diff --check` on loot code passed with line-ending warnings only.

Final Status:
- PENDING VERIFICATION.

## 2026-05-15 - Assembly Surface Hygiene Pass

What was wrong:
- Runtime loot asmdef still referenced `Hecton8.Core.Contracts` after the code path no longer directly used contract-only symbols.

What was done:
- Removed `Hecton8.Core.Contracts` from `Hecton8.Gameplay.Loot.asmdef`.
- Kept `Unity.Burst` in `Hecton8.Gameplay.Loot.Contracts.asmdef` because `LootMagnetPullJob` has `[BurstCompile]`.

Cinematic Cheats used:
- None. This is assembly isolation cleanup.

Exact Microseconds saved:
- No frame-time saving claimed. This reduces compile graph coupling only.

Verification:
- Asmdef JSON parse passed after the edit.
- User forbade dotnet rebuilds; none were run.

Final Status:
- PENDING VERIFICATION.

## 2026-05-15 - Presentation Budget And Shutdown Handoff Pass

What was wrong:
- Authored capacity can scale to 8192 loot slots, but the shared acoustic lane is prewarmed for 64 packets and wake for 128.
- Dense fields could publish more presentation signals than the loot system should claim from shared global lanes.
- `OnDisable` completed pending jobs but did not commit completed vault results before disposing the event lane.

What was done:
- Added Low/Mid/High/Ultra acoustic budgets: 16/48/56/64 packets per commit pass.
- Added Low/Mid/High/Ultra wake budgets: 32/96/112/128 packets per commit pass.
- Presentation publish now decrements budgets by reference and skips excess acoustic/wake packets.
- Acoustic intensity math is skipped once the acoustic budget is exhausted or when the event only emits wake.
- `OnDisable` force-completes a scheduled job and commits it when the vault and event arrays are still valid.

Cinematic Cheats used:
- Presentation remains a controlled acoustic/wake fake. Dense loot fields degrade by dropping surplus cosmetic pings, not by slowing acquisition truth.

Exact Microseconds saved:
- Prevents NativeQueue growth work from loot presentation above the shared lane ceilings.
- Avoids radius/intensity math for surplus cosmetic events after the acoustic budget is exhausted.
- Shutdown handoff is not a frame-time saving; it prevents lost completed job state before native event disposal.

Verification:
- User forbade dotnet rebuilds; none were run.
- `git diff --check` on loot code passed with line-ending warnings only.
- Static loot anti-bloat scan remains clean for direct native signal writers, scene searches, `math.sqrt`, `math.normalize`, `ToAbsoluteDouble3`, `FromAbsolutePosition`, `foreach`, string formatting, `.ToString()`, and LINQ markers.

Final Status:
- PENDING VERIFICATION.

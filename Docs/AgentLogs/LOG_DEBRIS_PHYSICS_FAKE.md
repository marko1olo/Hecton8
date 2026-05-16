# LOG_DEBRIS_PHYSICS_FAKE

## 2026-05-16 - Prompt Extraction Blocker

What was wrong:
- `DEBRIS_PHYSICS_FAKE` is present in the companion instruction list but absent from `Docs/Tasks/CURRENT_BATCH.md`.
- CLI extraction for `<AGENT_PROMPT id="DEBRIS_PHYSICS_FAKE">` returned no match.
- `Docs/Tasks/CURRENT_BATCH_AUDIT_20260516.md` independently records `DEBRIS_PHYSICS_FAKE` as an instruction ID missing from the current batch.

What was done:
- Read `AGENTS.md`.
- Read `Docs/Actual Domains of Project.txt`.
- Searched `Docs/Tasks/CURRENT_BATCH.md` for the required XML tag.
- Created `Docs/Tasks/Status_DEBRIS_PHYSICS_FAKE.md`.
- Created `Docs/AgentLogs/Rationale_DEBRIS_PHYSICS_FAKE.md`.
- Stopped before source edits because the authoritative task block is missing.

Cinematic Cheats used:
- None implemented. The expected future direction is a visual fake: GPU-only debris chips driven by signal buffers, not Rigidbody shards.

Exact Microseconds saved:
- 0 us measured. No runtime path changed.

Verification:
- Code compile not run because no source code changed.
- Status: BLOCKED BY DEPENDENCY - missing active batch XML prompt.

## 2026-05-16 - Phase 1 The Great Purge

What was wrong:
- Voxel carve aftermath still contained a CPU dropped-item debris loop and a legacy laser `IDebrisService.SpawnBurst` path.
- Bootstrap still treated the old `DebrisManager` service as the debris runtime dependency, which could create or preserve a GameObject-based debris owner.
- `DebrisSpawnSignal` had a destructive queue path only, so a GPU VFX listener could steal events from other consumers if it drained the queue directly.
- Voxel carve, outcrop, drill, player CCD impact, and vehicle CCD impact debris producers were not consistently marked for the GPU compute shard lane.

What was done:
- Removed the CPU dropped-item debris aftermath and legacy laser `SpawnBurst` emission from `VoxelDeltaProcessor`.
- Added `DebrisSpawnSignal.FlagComputeShard` and mirrored published debris signals into `SignalBus<DebrisSpawnSignal>` for non-destructive frame snapshot reads.
- Added `IDebrisComputeService` and `GlobalRegistryServiceSlot.DebrisComputeRuntime`.
- Registered `CarveDebrisComputeRenderer` as the compute debris service and exposed `ClearGpuDebris`, active count, capacity, and low-tier state through the registry contract.
- Routed scene cleanup to `GlobalRegistry.DebrisCompute.ClearGpuDebris()`.
- Changed bootstrap `DebrisManager` readiness/resolution to use `GlobalRegistry.DebrisCompute` and stopped bootstrap from calling `DebrisManager.EnsureRuntimeInstance()`.
- Converted voxel carve, outcrop, drill, player impact, vehicle impact, and existing repair sparks to publish compute-shard debris signals.
- Kept loot drops intact; `HarvestableOutcrop.DispatchYield` still registers actual loot items, not debris aftermath.

Cinematic Cheats used:
- Replaced CPU debris entities with one unmanaged signal per event and GPU-side shard request injection.
- Used deterministic hash axes from signal seed instead of CPU Rigidbody impulse simulation.
- Bounded the debris signal scan to 64 per frame so burst spam cannot create an unbounded VFX cost.
- Preserved existing DataVault/indirect-renderer path rather than introducing GameObject shards or pooled prefabs.

Exact Microseconds saved:
- Measured: blocked; `dotnet build` does not currently pass because of external fauna/docking/wake/ecosystem compile drift.
- Static estimate for Phase 1: 40-250 us saved on heavy mining frames by deleting the old dropped-item loop and legacy SpawnBurst branch.
- Static estimate for registry/bootstrap purge: 5-30 us saved around startup/teardown and avoided service-object traffic during debris bursts.
- Static estimate for DataVault cleanup path: 20-120 us saved during carve bursts by keeping shard state in fixed buffers instead of producer-owned CPU objects.

Verification:
- XML prompt extracted from `Docs/Tasks/CURRENT_BATCH.md`; task count = 18.
- Status updated in `Docs/Tasks/Status_DEBRIS_PHYSICS_FAKE.md`.
- Rationale updated in `Docs/AgentLogs/Rationale_DEBRIS_PHYSICS_FAKE.md`.
- Static search found no `DebrisManager.EnsureRuntimeInstance` caller outside the legacy `DebrisManager` type itself.
- Static search found no mining/impact debris `Instantiate` loop; remaining `Instantiate(baseStats)` is `SuitUpgradeManager` ScriptableObject stats, not debris.
- Static search confirms compute shard flags on voxel carve, outcrop, drill, player impact, vehicle impact, and repair sparks.
- `git diff --check` on touched debris/producer/doc files reports no whitespace errors; only line-ending warnings from the existing worktree configuration.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` failed externally. First run failed in fauna bite IK symbols; second run failed in docking autopilot, VFX wakes, and ecosystem macro swarm contract drift.

Status:
- Phase 1 source purge complete.
- Final validation blocked by external dependencies, not by the debris purge.

## 2026-05-16 - Multiplatform Inquisition And Kernel Polish

What was wrong:
- Debris request/telemetry layout needed explicit ABI proof for ARM64/Quest.
- The renderer needed the XML-visible `_DebrisBuffer` and `_DebrisPhysicsBuffer` contract, not only internal carve names.
- A balanced 4096-particle ceiling was not enough for High/Ultra tier.
- STP/upscalers could see procedural shimmer because debris had no motion-vector evidence.
- Stress recycling was implicit, not tied to `SystemStress01`.

What was done:
- Locked `CarveDebrisRequest` and `CarveDebrisTelemetryEntry` to `Pack=1, Size=64`.
- Bound DataVault-backed position/life and velocity buffers as `_DebrisBuffer` and `_DebrisPhysicsBuffer`.
- Kept low tier at 1024 shards, introduced mid at 4096, and high/ultra at 16,384.
- Scaled injected particles per carve to 16/48/128 for low/mid/high.
- Added shader-only deterministic tumble using particle ID plus time.
- Added a debris MotionVectors pass and stopped forcing no motion vectors in render params.
- Wired `SignalBusRegistry.SystemStress01` and `SystemHealthIndexSignal` pressure to 4x lifetime decay when stress exceeds 0.9.
- Replaced the compute velocity clamp branch with `step`/`lerp` plus `rsqrt(max(...))`.
- Confirmed the hot upload path uses `LockBufferForWrite` and no `GraphicsBuffer.SetData`.

Cinematic Cheats used:
- Toaster mode: 1024 shards, no SDF/flow heavy collision, short-lived visible chips.
- Mid mode: 4096 chips with bounded injection and GPU advection.
- God-mode: 16,384 chips, SDF/flow enabled, shader tumble, motion-vector history.
- Expiry dust remains a shader life-scale and dither clip, not a secondary emitter.

Exact Microseconds saved:
- Measured: blocked by external compile failures.
- Static low-tier GPU saving: 0.3-1.2 ms during debris storms by capping to 1024 and skipping SDF/flow.
- Static CPU saving: 20-90 us by keeping tumble in the vertex shader instead of CPU rotations.
- Static stall prevention: SetData sync spikes avoided by `LockBufferForWrite`; exact spike size pending profiling.
- Static stress behavior: slots recycle 4x faster when `SystemStress01 > 0.9`.

Verification:
- Original XML prompt re-read from `Docs/Tasks/CURRENT_BATCH.md`.
- `rg` confirmed no `ForceNoMotion`, no `Update()`, no `string.Format`, no `Instantiate`, and no `GraphicsBuffer.SetData` in debris domain.
- `rg` confirmed `_DebrisBuffer`, `_DebrisPhysicsBuffer`, MotionVectors pass, `Graphics.RenderMeshIndirect`, `LockBufferForWrite`, and AUP shift path.
- `git diff --check` on touched debris shader/compute/status/rationale files reports no whitespace errors; only line-ending warnings from the existing worktree configuration.
- `dotnet build` still fails externally. Current run reports 39 errors in XR, VaultProbeUtility, item signal, submarine structural grid, and bioluminescence domains.

Status:
- Tasks 1-17 complete by source/static verification.
- Final validation remains `[BLOCKED BY DEPENDENCY]`.

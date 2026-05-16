# LOG_ITEM_MAGNET_SOLVER

## 2026-05-16 Phase 1-4 Loot Magnet Pass

What was wrong:
- Loot magnet objective required killing trigger-style acquisition pressure. Local scan found no live loot `OnTriggerEnter` / `OnTriggerStay` magnet code and no `OverlapSphere`; the risk was semantic drift back into PhysX.
- Existing Burst path used a misleading low-tier snap mode that acquired every item in radius instead of cheap movement plus 0.3m snap.
- Existing telemetry lacked `ActiveLootPullsCount` and `PeakMagnetVelocity`.
- Origin-shift safety was implicit through AUP math but had no listener to force a stable runtime pose after a committed shift.

What was done:
- Added explicit `LootEntityFlags.Bit_IsMagnetic` and `Flag_Acquired` aliases in vault flags.
- Renamed the job type to `LootMagnetJob : IJobParallelFor`.
- Implemented inverse-square pull with `math.rcp(math.max(distSq, 0.1f))`, separate `math.rsqrt(math.max(distSq, 0.0001f))`, velocity clamp, finite guards, and 0.3m acquisition.
- Converted low tier to 10Hz `SlowTick` lerp instead of radius-wide snap-acquire.
- Added snap spark `DebrisSpawnSignal`, kept `ItemAcquiredSignal` on the existing SPSC/SignalBus lane, and retained AUP velocity wake emission for fluid/marine-snow consumers.
- Added root renderer motion-vector forcing above 10m/s in `PickupItem.ApplyLootMagnetPose`.
- Added 300-frame blackbox fields: `ActiveLootPullsCount` and `PeakMagnetVelocity`.
- Added stress culling: `HomeostasisBrain.SystemHealthIndex01 > 0.8` halves pull radius.
- Added `IOriginShiftListener` handling to force-complete pending pull jobs and reapply pulled proxy poses from AUP after floating-origin shifts.

Cinematic cheats used:
- Low tier: 10Hz clamped linear lerp, no PhysX force, no trigger volume.
- High/Ultra: same deterministic acquisition truth, higher wake/acoustic budgets for visual overkill.
- STP cheat: renderer motion vectors only when velocity exceeds 10m/s, restored when below threshold.

Exact microseconds saved:
- Trigger/OverlapSphere replacement: measured exact value unavailable because the project build is dependency-blocked; deterministic budget model is 40-80 us/frame saved in a 500-loot dense scene by avoiding trigger contact churn.
- Low-tier cadence reduction: 60Hz to 10Hz saves 83.333% of magnet scheduling frequency; model target 0.05 us/item job cost becomes 0.0083 us/item average.
- Bitmask filter vs component/tag checks: model target 0.03 us/item.
- Reciprocal/squared-distance math vs `Vector3.Distance`: model target 0.01-0.02 us/item.
- Telemetry peak velocity piggyback on commit pass: model target 0.01 us/item, no extra allocation.

Validation:
- Prompt re-extracted from `Docs/Tasks/CURRENT_BATCH.md` multiple times.
- Mandates read: physics determinism, zero-GC, native memory/job system, inventory/item SoA.
- `rg` scan found no `OnTriggerEnter`, `OnTriggerStay`, `OverlapSphere`, `LootMagnetPullJob`, `LowTierSnap`, `foreach`, or `Vector3.Distance` in loot magnet scope.
- `git diff --check` clean except existing Git CRLF warnings.
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1` failed outside this task in `PlayerKinematicsRuntime.cs`: missing `ResolveAupMaxDriftErrorMeters`, `_lastSyncFenceHash`, `_lastSyncFenceFrame`.
- `dotnet build .\Hecton8.slnx --no-restore` and a retry build hung beyond timeout and left dotnet workers; workers were killed. Final build green is blocked by external dependency wall, not reported as green.

## 2026-05-16 Multiplatform / H-PHI Rework

What was wrong:
- `LootMagnetSystem` still owned private `H8Memory` buffers for job signal events and blackbox telemetry.
- Loot magnet structs used sequential layout with desktop-friendly packing, not explicit ARM-safe offsets.
- Magnetic acquisition called the same pickup method as manual interaction, which published legacy managed interaction/EventBus events.
- High tier wake coupling existed, but there was no stronger typed lane for volumetric fluid impulse overkill.

What was done:
- Added `BufferID.EntityLootMagnetSignalEvents = 34`.
- Moved signal events and blackbox telemetry to `GlobalDataVault.GetBuffer` under `SystemID.GameplayLoot`.
- Converted `LootMagnetSignalEvent` and `LootMagnetTelemetryEntry` to `StructLayout(LayoutKind.Explicit, Pack=1)` with fixed field offsets.
- Added `PickupItem.TryHandleInventoryPickup(..., bool publishLegacyEvents)` and made the magnet path call it with `false`.
- Added High/Ultra `FluidImpulseSignal` publication for flying loot wake/silt coupling.
- Verified no shader/compute code exists in the loot magnet scope, so there are no DirectX-only thread-group assumptions in this domain.

Cinematic cheats used:
- Toaster mode remains 10Hz lerp plus no fluid impulse.
- High mode emits wake plus short-lived fluid impulses.
- Ultra mode emits wake plus larger/longer fluid impulses for dense silt swirl.

Exact microseconds saved:
- Private NativeArray ownership removal: runtime microseconds unchanged; fragmentation/ownership risk reduced, exact gain not measured.
- Managed EventBus suppression on magnet pickup: exact GC/microseconds not measured; avoids managed event object construction on magnetic acquisitions.
- High/Ultra fluid impulse: spends extra signal enqueue cost by design; Low/MX350 cost is 0 because the lane is gated off.

Validation:
- `rg` found no `H8Memory.Allocate`, `H8Memory.Release`, local `new NativeArray`, trigger fallback, `OverlapSphere`, `GameObject.Find`, `foreach`, `Vector3.Distance`, stale `LootMagnetPullJob`, or `LowTierSnap` in the loot magnet scope.
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 -v:normal` failed outside this domain in `ProceduralLadderClimbRuntime.cs`: unresolved `Hecton8.Input.Universal` and `UniversalInputStateSignal`.

## 2026-05-16 H-PHI Data Sovereignty Pass 2

What was wrong:
- `LootMagnetSystem` still cached seven `NativeArray<T>` fields as DataVault views. They were non-owning, but they still violated the stateless-system rule and created stale-alias risk across DataVault growth/fences.
- Scheduled jobs had no explicit DataVault buffer lock, so a future capacity grow could invalidate a pointer while Burst owned it.

What was done:
- Added `LootMagnetVaultViews` as a transient contract DTO for vault aliases.
- Removed every `NativeArray<T>` field from `LootMagnetSystem`.
- Changed enable, slow tick, fast tick, commit, origin-shift, telemetry, and dump paths to resolve DataVault views only for the current operation.
- Changed post-job commit/dump to use existing DataVault aliases through `TryGetBuffer` instead of allocating or growing buffers after the job.
- Added `TryLockScheduledVaultBuffers` / `UnlockScheduledVaultBuffers` around the job-owned entity/signal buffers.
- Renamed the blackbox dump target to `Docs/AgentLogs/Dump_ITEM_MAGNET_SOLVER.bin`.

Cinematic cheats used:
- Toaster mode remains the 10Hz linear fake with no fluid impulse.
- High/Ultra retain typed wake/fluid impulse overkill without contaminating the gameplay kernel.
- No shader or DirectX-only compute path exists in this loot domain.

Exact microseconds saved:
- Persistent system-local NativeArray removal: no honest hot-loop microsecond saving; this is a stability and ownership repair.
- DataVault lock/unlock: cold per scheduled job, expected below 1 us total in normal frames; exact measurement unavailable.
- Avoided stale alias crash risk on Quest/Android/Steam Deck under vault growth; not a frame-time metric.

Validation:
- `rg` confirms `LootMagnetSystem.cs` contains no `NativeArray<T>` declarations, `new NativeArray`, `Allocator.Persistent`, `H8Memory.Allocate`, or `H8Memory.Release`.
- `rg` confirms no trigger/OverlapSphere/GameObject.Find/foreach/Vector3.Distance/string.Format/Update debt in the loot magnet scope.
- `git diff --check` passed except Git CRLF normalization warnings.
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 -v:minimal` failed outside this domain: missing `Hecton8.VFX.Wakes` contracts, missing docking/autopilot contracts, duplicate `LockstepStateValidator.SanitizeFinite`, missing light shaft contracts, and `EcosystemDirector` interface drift. No local loot magnet error was emitted.

## 2026-05-16 Signal / Physics Integrity Pass

What was wrong:
- Manual pickup paths still emitted managed collection events through `InteractionEvents` and `HectonEventBus`, while magnet acquisition used typed lanes.
- `HectonItem.cs` duplicated the same legacy pickup publisher outside the nominal item folder.
- `PickupItem.ApplyLootMagnetPose` mutated `transform.position` even when the pickup had an active Rigidbody.
- `ItemAcquiredSignal`, `WakeGeneratedSignal`, and `FluidImpulseSignal` lacked explicit `Pack=1`.

What was done:
- Removed item-domain `ItemCollectedEvent` publication from `PickupItem` and `HectonItem`.
- Added shared `InventoryPickupSignalConstants` and routed manual pickup acquisition through `ItemAcquiredSignal`.
- Kept magnet acquisition from double-publishing by calling `TryHandleInventoryPickup(..., publishAcquiredSignal:false)`.
- Added Rigidbody suppression/restoration around math-owned magnet pose writes.
- Added `Pack=1` to magnet-emitted public signal structs in `GlobalSignals`.

Cinematic cheats used:
- Gameplay truth remains one typed item-acquired lane.
- Visual overkill remains bounded to `DebrisSpawnSignal`, `WakeGeneratedSignal`, and High/Ultra `FluidImpulseSignal`; Low/MX350 still pays no fluid impulse cost.
- Physics remains a deterministic fake: no trigger, no Rigidbody force, no active-body transform mutation.

Exact microseconds saved:
- Legacy managed event removal avoids managed `ItemCollectedEvent` allocation per manual pickup; exact microseconds not measured.
- Rigidbody suppression adds cold state flips when a pickup enters/leaves magnet ownership; no honest hot-loop saving claimed.
- `Pack=1` signal layout hardening has no runtime cost.

Validation:
- `rg` found no `HectonEventBus`, `InteractionEvents.RaiseItemCollected`, `ItemCollectedEvent`, or `using Hecton8.Modding` in `Gameplay/Loot`, `Items/PickupItem.cs`, or `HectonItem.cs`.
- `rg` found no `Update`, `FixedUpdate`, `LateUpdate`, `StartCoroutine`, `yield return`, `Vector3.Distance`, `OverlapSphere`, trigger callbacks, local `new NativeArray`, `Allocator.Persistent`, `H8Memory.Allocate`, or `H8Memory.Release` in the item magnet scope.
- `git diff --check` passed except Git CRLF normalization warnings.
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 -v:minimal` first exposed and then cleared one local missing using in `HectonItem`.
- Current build wall is external: `HectonXRRuntimeState`, `SubmarineStructuralGrid`, `VaultProbeUtility`, `BiolumPulseSyncRuntime`, and `SpatialAudioManager`.
## 2026-05-16 Shutdown Integrity Pass

What was wrong:
- Magnet pose ownership disabled pickup Rigidbody collisions/kinematics correctly during pull, but scheduler shutdown could clear counters without restoring active proxies if no job was pending.

What was done:
- Added scheduler-level restoration for all managed pickup sidecars before `LootMagnetSystem` clears runtime state.
- Kept the fix cold-path only: disable/dependency-loss cleanup, no added hot Burst work.

Cinematic Cheats used:
- None. This is survival plumbing: release physics ownership cleanly so visual magnet fakes do not poison authored pickup collisions.

Exact Microseconds saved:
- 0 us hot-frame savings claimed. Cost is cold O(active pickups) on disable/clear; benefit is preventing collision-suppression leaks after scheduler shutdown.

Validation:
- Forbidden item-magnet scope scan remains clean: no trigger callbacks, overlap sphere calls, `LootManager.Instance`, `Vector3.Distance`, `foreach`, `string.Format`, standard `Update` loops, or legacy item-collected publishers.
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 -v:minimal` fails outside this domain in `SargassumMicroFaunaBoids`, `HectonMarineSnowRenderer`, and `VehicleDockingModule`.
- No loot/item magnet compile errors were emitted.

## 2026-05-16 Sidecar Resize Integrity Pass

What was wrong:
- Live `maxLootEntities` capacity changes could replace the managed pickup sidecar arrays while those arrays still held magnet-owned pickup proxies.

What was done:
- `EnsureManagedSidecars` now clears runtime ownership before allocating replacement sidecars, restoring pickup physics/render state while the old references still exist.

Cinematic Cheats used:
- None. This is ownership hygiene for the visual fake pipeline.

Exact Microseconds saved:
- 0 us hot-frame savings claimed. The added work is cold-path only on sidecar capacity changes.

## 2026-05-16 Authoring Sanitizer Pass

What was wrong:
- Runtime sanitizers still allowed a 5000m magnet radius and 100000m/s max velocity, which contradicted the anti-tunneling clamp.

What was done:
- Reduced hard ceilings to 64m radius, 256 pull strength, and 48m/s max velocity. Defaults are unchanged.

Cinematic Cheats used:
- Low tier still uses 10Hz lerp. High/Ultra keep visual overkill in typed wake/fluid/debris lanes instead of buying it with unsafe item speeds.

Exact Microseconds saved:
- 0 us hot-frame savings claimed. This is a stability clamp and worst-case work limiter, not a measured arithmetic reduction.

## 2026-05-16 Duplicate Item Hygiene Pass

What was wrong:
- `HectonItem` still used `GetComponent<T>()` cache fills and a development-build interpolated error string after the magnet acquisition path had moved to typed lanes.

What was done:
- Replaced cold component cache fills in `Awake`, buoyancy setup, and editor validation with `TryGetComponent`.
- Replaced the interpolated missing-ItemData error with a static message and Unity context object.

Cinematic Cheats used:
- None. This pass keeps item pickup behavior unchanged and removes cold-path residue from the duplicate pickup path.

Exact Microseconds saved:
- 0 us hot-frame savings claimed. Cold initialization/editor validation only; exact microseconds not measured.

## 2026-05-16 Live Registry Churn Pass

What was wrong:
- `RefreshPickupVaultFromRegistry` could overwrite a sidecar slot with a different pickup or clear trailing slots without restoring the previous pickup's magnet-suppressed Rigidbody state.

What was done:
- Restored previous pickup runtime state before slot entity-id replacement.
- Restored stale pickup runtime state before clearing trailing sidecar slots.

Cinematic Cheats used:
- None. This preserves the math-driven magnet fake without leaking physics ownership after registry churn.

Exact Microseconds saved:
- 0 us Burst hot-loop savings claimed. SlowTick pays only for changed or stale slots; exact cold-path microseconds not measured.

## 2026-05-16 AUP ABI Packing Pass

What was wrong:
- Loot magnet packets embed `AbsoluteUniversePosition`, but that core AUP payload was explicit-size without `Pack=1`.

What was done:
- Added `Pack=1` to `AbsoluteUniversePosition`.
- Added `Pack=1` to `AbsoluteUniversePositionBlit128`.

Cinematic Cheats used:
- None. This is ARM64/Quest binary-layout hygiene required by the magnet packet ABI.

Exact Microseconds saved:
- 0 us runtime savings claimed. Field offsets and size remain explicit; the gain is layout certainty, not speed.

## 2026-05-16 Final Build-Green Gate

What was wrong:
- Earlier compile gates were blocked by unrelated domains and concurrent build workers, so the status file still carried an external dependency wall.

What was done:
- Ran `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -v:minimal`.
- Build completed with 0 warnings and 0 errors.

Cinematic Cheats used:
- None. Compile validation only.

Exact Microseconds saved:
- 0 us runtime savings claimed. Build elapsed time: 40.43 seconds.

## 2026-05-16 Scheduled Vault Lock Fail-Safe Pass

What was wrong:
- Scheduled DataVault buffers were unlocked on the normal completed-job path, but exceptional commit paths or schedule failures before `_pullScheduled` was set could leave lock state stale.

What was done:
- Added a shared force-complete/commit helper that always unlocks scheduled vault buffers.
- Wrapped late-frame commit in `finally`.
- Added schedule-failure cleanup that clears scheduled counters and unlocks buffers if `job.Schedule` fails before the job is owned.
- Moved origin-shift job draining before the non-finite shift-payload guard.

Cinematic Cheats used:
- None. This is survival plumbing for the Burst math pipeline.

Exact Microseconds saved:
- 0 us hot-loop savings claimed. Control-path only; prevents locked-buffer stalls rather than reducing arithmetic.

Validation:
- `rg` forbidden-pattern scan remains clean for item magnet scope.
- `git diff --check` reports only CRLF normalization warnings for the touched files.
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -v:minimal` currently fails outside item magnet in `SpatialAudioManager.cs`: missing `ClearVaultBackedTelemetryAliases` and `EnsureVaultBackedArray`.
- No loot/item magnet compile errors were emitted before the external wall.

## 2026-05-16 Kernel ABI And Vault Scrub Pass

What was wrong:
- The Burst job still trusted scheduler-sanitized dt/radius/strength/max-velocity before local speed-square and reciprocal math.
- `ItemAcquiredSignal` lacked explicit `Pack=1` while magnet acquisition publishes that packet on the typed lane.
- Runtime clear paths restored pickup physics state but could leave known item-magnet slots stale in shared DataVault buffers.

What was done:
- Added `LootMagnetJob.TryResolveKernelParameters` to validate finite kernel parameters inside Burst, clamp stable bounds, and fail closed with `LootEntityFlags.NonFinite`.
- Added `Pack=1` to `ItemAcquiredSignal`; verified all magnet-emitted public signals now use explicit `Pack=1`.
- Added `ClearKnownRuntimeVaultSlots` to scrub only sidecar-owned item-magnet slots during shutdown, dependency clear, and sidecar replacement.

Cinematic Cheats used:
- Low tier still uses the Dear Lie: 10 Hz scan plus bounded lerp.
- High/Ultra still spend saved cycles through wake/fluid/debris typed lanes; no shader or GPU-domain edits were made from this item task.

Exact Microseconds saved:
- 0 us hot-loop savings claimed. The new kernel guard is a safety branch, not an arithmetic win.
- Vault scrub is cold-path O(known sidecar slots), 0 us hot Burst cost.

Validation:
- Forbidden-pattern scan is clean for `Gameplay/Loot`, `Items/PickupItem.cs`, and `HectonItem.cs`.
- `LootMagnetSystem.cs` local NativeArray ownership scan is clean: no `NativeArray<T>` fields, `new NativeArray`, `Allocator.Persistent`, `H8Memory.Allocate`, or `H8Memory.Release`.
- `git diff --check` is clean for the current touched item-magnet and signal files.
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -v:minimal` currently fails outside item magnet in `SubmarineFluidDynamics.cs`: missing `TryRegisterHotSwapListener`, `TryUnregisterHotSwapListener`, and `RefreshVaultNativeStateAfterRelocation`.
- No loot/item magnet compile errors were emitted before the external wall.

## 2026-05-16 Kernel Delta-Time Clamp Pass

What was wrong:
- The new Burst kernel parameter guard rejected non-finite and non-positive dt, but a tiny positive dt could still reach `math.rcp(safeDeltaTime)` in the low-tier path.

What was done:
- Changed `LootMagnetJob.TryResolveKernelParameters` to clamp dt to `0.0001f..MaxIntegrationDeltaTimeSeconds`, matching the managed scheduler contract inside Burst.

Cinematic Cheats used:
- Low tier keeps the 10 Hz lerp fake; this patch prevents pathological reciprocal spikes inside that cheap path.

Exact Microseconds saved:
- 0 us claimed. This is NaN/reciprocal survival hardening.

Validation:
- `rg` confirmed no old `math.min(DeltaTimeSeconds, ...)` or `math.max(DeltaTimeSeconds, ...)` dt-only clamp remains in `LootMagnetPullJob.cs`.
- `git diff --check` is clean for `LootMagnetPullJob.cs` except Git CRLF normalization warnings.

## 2026-05-16 Post-Clamp Validation Refresh

What was wrong:
- The previous validation note named `SubmarineFluidDynamics.cs` as the active external wall, but concurrent project edits shifted the current compile wall.

What was done:
- Re-extracted the `ITEM_MAGNET_SOLVER` XML prompt.
- Re-ran forbidden-pattern scan across item-magnet scope.
- Re-ran `git diff --check` on current touched files.
- Re-ran `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -v:minimal`.

Cinematic Cheats used:
- None. Validation refresh only.

Exact Microseconds saved:
- 0 us runtime savings claimed.

Validation:
- Forbidden-pattern scan is clean for `Gameplay/Loot`, `Items/PickupItem.cs`, and `HectonItem.cs`.
- `git diff --check` is clean except Git CRLF normalization warnings.
- Current build wall is external to item magnet in `LockstepStateValidator.cs`: missing `LockstepSnapshotSignalCapacity`, `LockstepSnapshotLaneHash`, `SystemGlitchSignalCapacity`, and `SystemGlitchLaneHash`.
- No loot/item magnet compile errors were emitted before the external wall.

## 2026-05-16 Runtime Pose NaN Vaccination Pass

What was wrong:
- AUP local values were guarded, but `ToRuntimeFloat3()` could still return non-finite coordinates if floating-origin state was poisoned.

What was done:
- Added `IsFiniteFloat3` and gated both scheduler transform-write sites before `PickupItem.ApplyLootMagnetPose`.
- Normal commit now flags the fault, marks the slot `NonFinite`, restores pickup runtime state, and avoids transform mutation.
- Origin-shift reapply now restores pickup runtime state and skips the transform write when runtime conversion is non-finite.

Cinematic Cheats used:
- None. This preserves the existing mathematical magnet fake by preventing presentation NaNs from reaching Unity transforms.

Exact Microseconds saved:
- 0 us claimed. One finite branch per visually applied pulled pickup; this is crash-prevention, not a speed win.

Validation:
- `rg` confirmed both `ToRuntimeFloat3()` call sites are followed by `IsFiniteFloat3` checks before `ApplyLootMagnetPose`.
- `git diff --check` is clean for `LootMagnetSystem.cs` except Git CRLF normalization warnings.

## 2026-05-16 Final Green Validation Pass

What was wrong:
- Status still reported an external compile wall after the runtime-pose vaccination patch.

What was done:
- Re-ran the scoped forbidden-pattern scan for item magnet files.
- Re-ran the local `NativeArray` ownership scan for `LootMagnetSystem.cs`.
- Re-ran `git diff --check` for current touched source and log files.
- Re-ran `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -v:minimal`.

Cinematic Cheats used:
- None. Validation only.

Exact Microseconds saved:
- 0 us runtime savings claimed. Build elapsed time: 1.23 seconds.

Validation:
- Forbidden-pattern scan is clean for `Gameplay/Loot`, `Items/PickupItem.cs`, and `HectonItem.cs`.
- `LootMagnetSystem.cs` local ownership scan is clean: no `NativeArray<T>` declarations, `new NativeArray`, `Allocator.Persistent`, `H8Memory.Allocate`, or `H8Memory.Release`.
- `git diff --check` is clean except Git CRLF normalization warnings.
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -v:minimal` succeeded with 0 errors and 1 warning.
- Warning is external to item magnet: `AI/Ecosystem/EcosystemPopulationBalancer.cs` is specified multiple times in `Hecton8.Core.csproj`.

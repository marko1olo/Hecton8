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

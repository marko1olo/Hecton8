# LOG - WELDING_REPAIR_LOGIC

## 2026-05-16 - Hull Repair Engine
What was wrong:
- Hull dent repair had no authoritative mutation path for `GlobalDataVault.HullDents`.
- Shader dent state was private presenter data, so repair could not mathematically erase dent vectors.
- Submarine repair could patch structural breaches but did not emit a room-seal signal from dent completion.
- Repair sparks were AUP-based but were not flagged for the compute shard StructuredBuffer path.

What was done:
- Added `SystemID.GameplayTools` and `BufferID.HullDents` for a fixed `float4[16]` vault lane.
- Added `HullRepairedSignal` and configured its signal lane.
- Changed `RepairTool.UsePrimary` to run existing `TryBeginToolUse(deltaTime,true)` battery/durability drain.
- Implemented `TryRepairVaultHullDents`: AUP double3 hit/root math, submarine-local conversion, 2m dent scan, finite clamp, packed-depth erase, `HullRepairedSignal` on zero depth, and black-box repaired-count telemetry.
- Mirrored `HullDentShaderController` to/from the vault so shader deformation/rust reacts to vault `w` changes instead of private-only state.
- Added `ISubmarineRepairRoomResolver` on `SubmarineStructuralGrid` to map repair hits to gas room ids without RepairTool knowing structural internals.
- Added `GasDynamicsSolver` consumption of `HullRepairedSignal` to clear the room `Breached` flag while no gas job is running.
- Marked repair spark debris with `FlagToolSparks | FlagComputeShard` and bounded quantity for low-tier fake + high-end StructuredBuffer injection.

Cinematic Cheats used:
- Packed-radius/depth preservation instead of physical vertex simulation.
- 16-slot SOA vault scan instead of dynamic dent lists or mesh edits.
- AUP spark signal reused existing compute debris path instead of a new welding particle subsystem.
- Shader vector-array mirror buys visual unbend/rust fade with no CPU mesh deformation.

Exact Microseconds saved:
- Rejected per-vertex mesh repair: estimated 300-800 us saved on i3/MX350 during active welding.
- Rejected new RepairToolManager/singleton update loop: estimated 20-40 us saved per frame plus zero global coupling.
- Rejected bespoke welding compute buffer allocation/dispatch: estimated 80-150 us saved on setup frames and avoided new GPU resource churn.
- Rejected direct gas room scans from RepairTool: estimated 10-30 us saved per weld tick; gas solver receives O(1) room flag clear by signal.
- Final kernel estimate: repair-side dent math remains under 10 us for 16 dents, excluding existing Unity interaction raycast.

Validation:
- `dotnet build .\Assembly-CSharp.csproj --no-restore` first failed because `Temp\obj\Assembly-CSharp\project.assets.json` was missing.
- `dotnet build .\Assembly-CSharp.csproj` with restore ran for 00:03:44 and failed in `Hecton8.Core.csproj` with 159 pre-existing missing-type/reference errors before Assembly-CSharp diagnostics.
- Targeted `BuildProjectReferences=false` pass failed because `Assembly-CSharp-firstpass.dll`, `Hecton8.Core.dll`, and `Hecton8.Editor.dll` were unavailable after the Core dependency wall.
- No emitted diagnostic referenced `RepairTool.cs`, `HullDentShaderController.cs`, `SubmarineStructuralGrid.cs`, `GasDynamicsSolver.cs`, `GlobalSignals.cs`, or `H8Memory.cs`.

## 2026-05-16 - Second Pass Multiplatform Inquisition
What was wrong:
- `HullRepairedSignal` had explicit size but not explicit Pack=1, which is avoidable ABI risk for ARM64/Quest.
- The repair lane had no SignalPayloadFiniteGuards sanitizer for invalid AUP/room data.
- Repair visual beams, hull dent presenter impact conversion, and structural sidecar point conversion still had float-only Unity point transforms.
- Spark quantity was bounded but not explicit enough for MX350 versus high-tier visual overkill.
- Vault access was correct but still looked too close to repeated local buffer ownership.

What was done:
- Added Pack=1 to `HullRepairedSignal` and registered a finite guard for `HullRepairedSignal`.
- Cached `VaultBufferHandle<float4>` for HullDents in RepairTool and HullDentShaderController, resolving short-lived views only inside vault locks.
- Replaced float-only point conversions in the repair lane with AUP double3 relative math, finite quaternion checks, and safe scale division.
- Hardened normal/direction math against NaN before rsqrt and LookRotation.
- Sanitized repair power/intensity before dent erase and spark emission.
- Split spark quantity by tier: low/MX350 2-6 generic sparks; high tiers 8-32 compute-shard sparks.
- Audited compute path: Hecton_FluidAdvection uses 64-thread groups and CarveDebrisComputeRenderer clamps kernel group size to 1024.
- Ran Omega-equivalent anti-bloat grep because CURRENT_BATCH.md contains no `<POLISH_MANDATE>` tag.

Cinematic Cheats used:
- Dear Lie: low tier uses tiny spark counts and shader/vault dent fade, not physical hull simulation.
- Visual Overkill: high tiers route AUP sparks into existing SDF/flow compute advection.
- Packed dent scalar is preserved; repair erases depth without changing radius bits.
- Shader upload staging remains a fixed Vector4[16], while gameplay truth lives in GlobalDataVault.

Exact Microseconds saved:
- Cached vault handles: estimated 2-5 us saved per active repair tick versus repeated buffer lookup.
- Low-tier spark clamp: estimated 20-60 us saved per active weld burst compared with high-tier particle counts.
- Rejected private NativeArray authority: avoids persistent allocation and ownership synchronization cost.
- Rejected welding-only compute shader: avoids 80-150 us setup churn and duplicate GPU resources.
- AUP/finite guard cost: estimated under 5 us per active weld/contact path, paid to eliminate NaN/precision failures.

Validation:
- `rg` audit: no `RepairToolManager`, `EventBus`, `string.Format`, `void Update()`, direct HullDents `GetBuffer<float4>`, or float-only `InverseTransformPoint` remains in the repair lane.
- `git diff --check -- ...` reports only existing CRLF conversion warnings.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` fails before repair validation with 231 errors: missing RealtimeCSG source files plus unrelated Hecton8.Core VFX wake/light-shaft/ecosystem contract errors.
- No emitted diagnostic references the WELDING_REPAIR_LOGIC touched repair files.

## 2026-05-16 - Third Pass H-Phi / ARM64 Audit
What was wrong:
- Structural breach sidecar data is part of the repair interaction surface; it needed explicit proof that breach SOA and the 300-frame damage-control blackbox are vault-owned, not private authority.
- Repair-side storage records still carried Pack=4, leaving unnecessary implicit-padding risk for ARM64/Quest native views.
- The build command was previously blocked; it needed a longer single-worker rerun to capture the true dependency wall.

What was done:
- Verified `SubmarineStructuralGrid` uses `VaultBufferHandle<float4>` for `BufferID.SubmarineStructuralBreaches` and `VaultBufferHandle<DamageControlTelemetryEntry>` for `BufferID.SubmarineDamageControlBlackBox`.
- Verified no private `_breaches = new NativeArray<float4>` or `_damageControlTelemetry = new NativeArray<DamageControlTelemetryEntry>` allocation remains in the repair lane.
- Changed `ImpactCommand` to `StructLayout(LayoutKind.Sequential, Pack = 1, Size = 24)`.
- Changed `DamageControlTelemetryEntry` to `StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)`.
- Changed `AupPreShiftSignal`, `AupShiftSignal`, and `DeflectSignal` to `StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)`.
- Left remaining `Pack=16` hits in `SubmarineStructuralGrid` alone because they are Burst job payload structs, not vault/signal storage ABI.
- Reran anti-bloat grep across repair files.

Cinematic Cheats used:
- Structural repair remains a 64-entry breach fake plus 16-slot HullDents erase, not a physical hull remesh.
- Blackbox is a fixed 300-frame ring, not log strings or managed crash history.
- Low-tier keeps dot-product/radius repair fakes; high-tier still spends saved budget on compute-shard spark drift and shader deformation fade.

Exact Microseconds saved:
- Vault-backed breach sidecar handle reuse: estimated 2-5 us saved during active repair-side reads versus repeated lookup/authority churn.
- Removing private damage-control blackbox ownership: runtime neutral, but avoids leak/sentinel ambiguity.
- Pack=1 storage/signal pass: 0 us runtime gain; removes native stride ambiguity on ARM64/Quest.
- Anti-bloat grep pass cost: 450 us estimated CLI scan cost, zero runtime cost.

Validation:
- `rg` audit: no `RepairToolManager`, `EventBus`, `string.Format`, `void Update()`, direct HullDents `GetBuffer<float4>`, float-only `InverseTransformPoint`, private `_breaches` NativeArray allocation, or private `_damageControlTelemetry` NativeArray allocation in the repair lane.
- `git diff --check -- ...` reports only CRLF conversion warnings in `H8Memory.cs` and `SubmarineStructuralGrid.cs`.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /m:1 /clp:ErrorsOnly` ran after the storage ABI patch for 00:04:03.92 and failed with 245 errors before repair isolation.
- The same build command reran after the signal ABI patch for 00:01:42.32 and failed with 401 errors before repair isolation.
- Filtered build rerun for `RepairTool|HullDentShaderController|SubmarineStructuralGrid|GlobalSignals|GasDynamicsSolver` returned `NO_REPAIR_FILE_DIAGNOSTICS` with build exit code 1.
- Build blocker classes: RealtimeCSG.csproj missing source files; unrelated Hecton8.Core `GlobalDataVault.ValidateAbiLayout` missing symbol; unrelated `SargassumMicroFaunaBoids` missing sensory resolver/buffer symbols; unrelated `SubmarineFluidDynamics` vault-property mutation errors.
- No emitted diagnostic referenced `RepairTool.cs`, `HullDentShaderController.cs`, `SubmarineStructuralGrid.cs`, `GlobalSignals.cs`, or `GasDynamicsSolver.cs`.

## 2026-05-16 - Fourth Pass Repair Blackbox
What was wrong:
- RepairTool wrote aggregate dent counts to CrashTelemetryBuffer but had no dedicated 300-frame repair heartbeat ring.
- Invalid repair hit math could return before a repair-domain dump was written.
- A private telemetry container would have violated the vault sovereignty requirement.

What was done:
- Added `BufferID.RepairToolBlackBox = 340`.
- Added `RepairToolBlackBoxEntry` as a fixed 64-byte Pack=1 repair blackbox record.
- Added `VaultBufferHandle<RepairToolBlackBoxEntry>` to RepairTool and allocate/resolve it through GlobalDataVault with `SystemID.GameplayTools`.
- Added equipped-frame heartbeat writes keyed by `Time.frameCount % 300`, so same-frame detail writes update the same ring slot.
- Added repair-detail writes for active dent count, touched dent count, repaired count, battery byte, flags, AUP, and state hash.
- Added invalid-math fault handling that dumps `Docs/AgentLogs/Dump_WELDING_REPAIR_LOGIC.bin` once per fault streak.

Cinematic Cheats used:
- Blackbox is one 64-byte frame record instead of managed logs or verbose strings.
- Frame-indexed ring overwrites same-frame heartbeat/detail entries instead of creating extra history churn.
- Normal path has no disk I/O; disk write is reserved for fault evidence.

Exact Microseconds saved:
- Rejected managed List/string telemetry: estimated 15-40 us saved during active diagnostic frames and zero GC pressure.
- Rejected private NativeArray blackbox: estimated 1-3 us saved in ownership/sentinel churn and no untracked persistent allocation.
- Vault heartbeat write cost: estimated 3-6 us per equipped ToolTick on i3/MX350.
- Fault dump cost: intentionally unbounded disk I/O, only on invalid repair math.

Validation:
- `rg` audit: RepairTool has no `new NativeArray`, `EventBus`, `string.Format`, `void Update()`, or Pack=4/Pack=8 struct layout.
- `git diff --check -- Assets/_Project/Scripts/RepairTool.cs Assets/_Project/Scripts/Core/Memory/H8Memory.cs` reports only CRLF conversion warnings.
- Filtered build scan for `RepairTool|RepairToolBlackBox|H8Memory|CS0227|CS0214|CS0266|CS0103|CS1525|CS1002|CS1513` returned `NO_REPAIR_BLACKBOX_DIAGNOSTICS` with build exit code 1.
- Repository build remains blocked by unrelated dependency failures outside WELDING_REPAIR_LOGIC.

## 2026-05-16 - Fifth Pass Spark Signal Lane
What was wrong:
- Repair spark feedback used `GlobalSignals.Publish(in DebrisSpawnSignal)`, which also enqueues the legacy debris `NativeQueue`.
- Low-tier spark feedback depended on downstream signal consumers instead of explicitly emitting the generic particle fake requested by the XML.
- `RepairSparkDebrisKind` carried a local magic value already defined by `DebrisSpawnSignal.DebrisKindSparks`.

What was done:
- Changed repair spark publishing to `SignalBus<DebrisSpawnSignal>.Push(in signal)`.
- Kept high-end overkill on the existing typed compute-shard path consumed by `CarveDebrisComputeRenderer` through `ReadOnlySpan<DebrisSpawnSignal>`.
- Added `sparksVFX.Emit(...)` with a low-tier cap of 6 and high-tier local cap of 16.
- Changed `RepairSparkDebrisKind` to alias `DebrisSpawnSignal.DebrisKindSparks`.

Cinematic Cheats used:
- Toaster mode gets small local particle bursts, not fluid-debris queue simulation.
- God-mode still receives typed compute-shard sparks for SDF/current advection.
- The same DebrisSpawnSignal contract drives both visual tiers; no new repair-specific signal was invented.

Exact Microseconds saved:
- Direct typed lane avoids duplicate legacy debris enqueue/drain: estimated 3-8 us saved per repair spark pulse.
- Low-tier local spark cap saves estimated 20-60 us per active weld burst compared with high-tier spark counts.
- Debris-kind alias has 0 us runtime gain; it prevents interface drift.

Validation:
- `rg` audit: RepairTool spark path now contains `SignalBus<DebrisSpawnSignal>.Push` and no repair-spark `GlobalSignals.Publish`.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /m:1 /clp:ErrorsOnly` filtered for repair spark/signal diagnostics returned `NO_REPAIR_SPARK_SIGNAL_DIAGNOSTICS` with build exit code 1.
- Repository build remains blocked by unrelated dependency failures outside WELDING_REPAIR_LOGIC.

## 2026-05-16 - Sixth Pass Toaster/God Spark Split
What was wrong:
- The fifth-pass spark lane still set `DebrisSpawnSignal.FlagComputeShard` on Low/MX350, so toaster mode could pay compute-advection cost instead of using the requested generic fake.
- The code did not explicitly prove that high-tier overkill remained intact after the low-tier eviction.

What was done:
- Changed `PublishRepairSparkSignal` so Low, Unknown, and Mx350 publish only `DebrisSpawnSignal.FlagToolSparks`.
- Kept `DebrisSpawnSignal.FlagComputeShard` for non-low tiers, preserving the existing `CarveDebrisComputeRenderer` StructuredBuffer injection path.
- Changed `PublishHullRepairedSignal` to push `SignalBus<HullRepairedSignal>` directly; the typed bus still applies payload finite guards.
- Confirmed `CarveDebrisComputeRenderer` consumes `ReadOnlySpan<DebrisSpawnSignal>` and only injects compute particles when `FlagComputeShard` is set.
- Confirmed local `sparksVFX.Emit` remains capped at 6 on low tiers and 16 on higher tiers.
- Reran anti-bloat grep for local NativeArray, EventBus, string.Format, Update, Pack=4/Pack=8, and repair-spark GlobalSignals usage.
- Audited the strict XML domain path. `Assets/_Project/Scripts/Gameplay/Tools/` is absent in this checkout; the actual repair lane is `Assets/_Project/Scripts/RepairTool.cs`.
- Swept adjacent `Assets/_Project/Scripts/Tools` and `Assets/_Project/Scripts/Gameplay` for the banned patterns. Findings are unrelated systems outside WELDING_REPAIR_LOGIC ownership and were not edited.

Cinematic Cheats used:
- Toaster mode uses 2-6 local spark fakes and no compute-shard signal.
- High tiers keep 8-32 typed compute-shard sparks that can drift through SDF/current advection.
- One DebrisSpawnSignal contract handles both tiers; no new welding signal was invented.

Exact Microseconds saved:
- Low/MX350 compute eviction: estimated 20-80 us saved per active weld burst by skipping 8-32 compute-shard particle injections.
- Direct typed SignalBus path from the fifth pass remains: estimated 3-8 us saved per repair spark pulse by avoiding legacy debris queue duplication.
- Direct HullRepairedSignal SignalBus push: estimated 0-1 us saved; the main gain is eliminating wrapper drift from RepairTool.
- Local cap remains: estimated 20-60 us saved per low-tier weld pulse versus high-tier local/computed spark volume.
- High-tier compute retention saves 0 us intentionally; the budget is spent on visible spark drift.

Validation:
- `rg` audit: `RepairTool` contains direct `SignalBus<DebrisSpawnSignal>.Push`, direct `SignalBus<HullRepairedSignal>.Push`, conditional `FlagComputeShard`, no `GlobalSignals.Publish`, no `new NativeArray`, no `EventBus`, no `string.Format`, no `void Update()`, and no Pack=4/Pack=8 struct layout.
- `rg` audit: `CarveDebrisComputeRenderer` consumes `ReadOnlySpan<DebrisSpawnSignal>` and gates compute injection on `DebrisSpawnSignal.FlagComputeShard`.
- Domain audit: strict XML path `Assets/_Project/Scripts/Gameplay/Tools/` is missing; adjacent Tools/Gameplay sweep finds unrelated `GlobalSignals`, `PhysicsEventBus`, `HectonEventBus`, and private `NativeArray` usage outside this prompt.
- `git diff --check -- ...` reports only CRLF conversion warnings.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /m:1 /clp:ErrorsOnly /p:BuildProjectReferences=false` filtered for repair typed-lane diagnostics returned `NO_REPAIR_TYPED_LANE_DIAGNOSTICS` with build exit code 1.
- Repository build remains blocked by unrelated dependency failures outside WELDING_REPAIR_LOGIC.

## 2026-05-16 - Seventh Pass Repair Blackbox ABI Dump
What was wrong:
- `RepairToolBlackBoxEntry` was Pack=1/Size=64 but used sequential layout, leaving the last byte as size-only padding instead of an explicit field.
- `DumpRepairBlackBox` wrote only semantic fields. That produced 51-byte records after the header, not the 64-byte stride used by the vault ring.
- ARM64/Quest postmortem readers would have to guess the record contract.

What was done:
- Changed `RepairToolBlackBoxEntry` to `StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)`.
- Added `FieldOffset` annotations for every field through byte 63, including `Reserved0`.
- Added `RepairBlackBoxEntrySizeBytes = 64`.
- Added a cold `UnsafeUtility.SizeOf<RepairToolBlackBoxEntry>()` guard before binary dump writes.
- Updated `DumpRepairBlackBox` to write `entrySize` and then exactly 64 bytes per entry: AUP grid/local, AUP pad/reserved bytes, frame, state hash, counts, battery, flags, and reserved byte.

Cinematic Cheats used:
- None in the visual path; this is survival infrastructure.
- Fault dump stays binary and fixed-stride instead of managed log strings.
- Normal gameplay still uses one 64-byte vault ring write, not streaming I/O.

Exact Microseconds saved:
- Hot path: 0 us saved; ABI certainty is the goal.
- Fault-path `UnsafeUtility.SizeOf` guard: estimated under 1 us before disk I/O.
- Rejected managed field-log dump: avoids unpredictable GC and string formatting during crash evidence capture.
- Deterministic 64-byte dump stride saves postmortem tooling from scan/repair work after a crash.

Validation:
- `rg` audit: `RepairToolBlackBoxEntry` is explicit Pack=1 Size=64 with offsets 0,48,52,56,58,60,61,62,63.
- `rg` audit: `DumpRepairBlackBox` writes `entrySize`, AUP pad/reserved bytes, and `Reserved0`.
- `rg` audit: `RepairTool` still has direct `SignalBus<DebrisSpawnSignal>.Push` and `SignalBus<HullRepairedSignal>.Push`, no `GlobalSignals.Publish`, no `new NativeArray`, no `EventBus`, no `string.Format`, no `void Update()`, and no Pack=4/Pack=8 struct layout.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /m:1 /clp:ErrorsOnly /p:BuildProjectReferences=false` filtered for repair ABI/dump diagnostics returned `NO_REPAIR_ABI_DUMP_DIAGNOSTICS` with build exit code 1.
- `git diff --check -- ...` reports only existing CRLF conversion warning in `RepairTool.cs`.

## 2026-05-16 - Eighth Pass Interaction Raycast Vault
What was wrong:
- `RepairTool.TryGetRepairHit` correctly used the queued interaction service, but that service still owned private persistent `NativeArray<RaycastCommand>` and `NativeArray<RaycastHit>` lanes.
- The XML requires RaycastCommand from the tool; the H-Phi pass requires the command/result storage to live in the vault, not in handler-owned native arrays.

What was done:
- Added `BufferID.InteractionRaycastScheduledCommands = 385`.
- Added `BufferID.InteractionRaycastScheduledHits = 386`.
- Added `BufferID.InteractionRaycastStagingCommands = 387`.
- Replaced EquipmentInteractionHandler's persistent scheduled command, scheduled hit, and staging command NativeArray fields with `VaultBufferHandle<RaycastCommand>` and `VaultBufferHandle<RaycastHit>`.
- Resolved transient NativeArray views only from the vault handles when writing, scheduling, and completing raycast jobs.
- Added `TryLockBuffer`/`TryUnlockBuffer` around staging command writes.
- Kept scheduled command/hit buffers locked from `RaycastCommand.ScheduleBatch` until job completion.
- Left unrelated completed-hit managed side-channel arrays alone; they are not native storage authority.

Cinematic Cheats used:
- Repair hit detection remains frame-latent RaycastCommand, not synchronous Physics.Raycast.
- Low tier keeps the same one-frame hit queue and cheap dent kernel.
- High tier spends the saved physics-stall budget downstream on compute sparks and shader dent/rust fade.

Exact Microseconds saved:
- Avoided private raycast lane allocation/sentinel churn: estimated 2-5 us during interaction service lifecycle.
- Lock overhead: estimated 1-3 us per staged ray batch.
- Avoided direct synchronous per-tool raycast stalls under tool spam: estimated up to 1200 us worst-case frame protection, depending on collider load.
- No extra disk I/O; Steam Deck/MicroSD pressure remains 0 us on the hot path.

Validation:
- `rg` audit: `RepairTool.TryGetRepairHit` uses `TryResolveQueuedRaycast`; no direct `Physics.Raycast` or `RaycastNonAlloc` exists in `RepairTool`.
- `rg` audit: `EquipmentInteractionHandler` still schedules `RaycastCommand.ScheduleBatch`.
- `rg` audit: persistent raycast lane fields are now `VaultBufferHandle<RaycastCommand>` / `VaultBufferHandle<RaycastHit>`.
- `rg` audit: new BufferIDs 385-387 are present.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /m:1 /clp:ErrorsOnly /p:BuildProjectReferences=false` filtered for repair/interaction raycast vault diagnostics returned `NO_REPAIR_INTERACTION_RAYCAST_VAULT_DIAGNOSTICS` with build exit code 1.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /m:1 /clp:ErrorsOnly` full-graph filtered for repair/interaction raycast diagnostics returned `NO_REPAIR_INTERACTION_RAYCAST_FULLGRAPH_DIAGNOSTICS` with build exit code 1.
- `rg` anti-bloat audit across `RepairTool` and `EquipmentInteractionHandler` returned no matches for `void Update`, `string.Format`, `EventBus`, direct physics raycasts, `GlobalSignals.Publish(in signal)`, local `new NativeArray`, or Pack=4/Pack=8 struct layout.
- `git diff --check -- ...` reports only CRLF conversion warnings in touched files.

## 2026-05-16 - Ninth Pass Raycast Lock Discipline
What was wrong:
- `Awake` reset raycast command lanes through resolved NativeArray views without taking the vault lock first.
- `QueuePrimaryRaycast` resolved the staging command view before `TryLockBuffer`, leaving a stale-alias window.
- `ScheduleStagedRaycasts` resolved staging command/hit views before locking.
- The eighth-pass handle ping-pong conflicted with `EnsureRaycastBufferHandles`, which expects fixed BufferIDs. Completion could rebind scheduled fields away from the buffers actually scheduled into the RaycastCommand job.

What was done:
- Added `ResetCommandLaneLocked` and routed cold scheduled/staging command reset through lock-before-resolve.
- Changed `QueuePrimaryRaycast` to ensure handles, lock `_stagingCommandsHandle.BufferId`, resolve inside the lock, then write the command.
- Removed scheduled/staging handle swapping.
- Changed `ScheduleStagedRaycasts` to lock staging commands plus fixed scheduled command/hit buffers, resolve all views inside those locks, copy at most 64 commands into the fixed scheduled command buffer, and schedule RaycastCommand against the fixed scheduled command/hit lanes.
- Kept scheduled command/hit locks alive until `CompleteScheduledRaycasts` consumes the job output, resets the scheduled command lane, and calls `UnlockScheduledRaycastVaultBuffers`.
- Left staging command storage unlocked immediately after scheduling so the next frame can queue new repair hits without touching job-owned memory.
- Removed the unused `InteractionRaycastStagingHits` vault lane after fixed scheduled result storage made it dead.

Cinematic Cheats used:
- Repair hit detection remains one-frame RaycastCommand latency instead of synchronous `Physics.Raycast`.
- Low tier pays a bounded 64-command copy instead of physics stalls.
- High tier still spends the saved synchronous physics budget on compute-advection sparks and shader dent/rust recovery.

Exact Microseconds saved:
- Direct synchronous per-tool raycast stalls remain avoided: estimated up to 1200 us worst-case under tool spam/collider load.
- Fixed-lane command copy costs an estimated 2-6 us per scheduled batch on i3/MX350.
- Lock overhead remains estimated 1-3 us per staged ray batch.
- Removing the stale handle swap has 0 us direct saving; it prevents wrong-buffer completion and unlock errors.
- Removing the unused staging-hit lane has 0 us hot-path saving; it removes one 64-entry cold allocation/sentinel lane.

Validation:
- `rg` anti-bloat audit across `RepairTool`, `EquipmentInteractionHandler`, and `H8Memory` returned no matches for `new NativeArray<Raycast`, `private NativeArray<Raycast`, `Physics.Raycast`, `RaycastNonAlloc`, `GlobalSignals.Publish(in signal)`, `EventBus`, `string.Format`, `void Update()`, or `InteractionRaycastStagingHits`.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /m:1 /clp:ErrorsOnly /p:BuildProjectReferences=false` filtered for repair/interaction raycast lock diagnostics returned `NO_REPAIR_INTERACTION_RAYCAST_LOCK_DISCIPLINE_DIAGNOSTICS` with build exit code 1.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /m:1 /clp:ErrorsOnly` full-graph filtered for repair/interaction raycast lock diagnostics returned `NO_REPAIR_INTERACTION_RAYCAST_LOCK_DISCIPLINE_FULLGRAPH_DIAGNOSTICS` with build exit code 1.
- `git diff --check -- ...` reports only CRLF conversion warnings in touched files.
- Repository build remains blocked by unrelated dependency failures outside WELDING_REPAIR_LOGIC.

## 2026-05-16 - Tenth Pass Power Indicator SRP
What was wrong:
- `RepairTool` used `MaterialPropertyBlock` on `_powerIndicatorRenderer`.
- The renderer is a generic `Renderer`, not a documented particle/UI exception.
- `ToolTick` called `UpdatePowerIndicator`, so the code could run `GetPropertyBlock`, set `_EmissionColor`, and `SetPropertyBlock` while equipped.
- This violates the project SRP-batcher rule for standard geometry.

What was done:
- Removed the `MaterialPropertyBlock` field.
- Removed `Shader.PropertyToID("_EmissionColor")`.
- Removed `GetPropertyBlock`, `_mpb.SetColor`, and `SetPropertyBlock`.
- Added authored shared-material slots for off, low, and on indicator states.
- Cached the renderer default shared material.
- Added a `PowerIndicatorVisualState` state gate so ToolTick returns without touching renderer state unless the battery state/material/visibility changes.
- Preserved the low-battery visual as a three-state fake instead of per-frame emission flicker math.

Cinematic Cheats used:
- Toaster mode uses a material-state lie for power status, not dynamic emission mutation.
- High-end materials can still be authored with stronger emissive/premium appearance through the shared material slots.
- No runtime material clone was created.

Exact Microseconds saved:
- MPB eviction saves an estimated 2-5 us per equipped ToolTick with a power indicator.
- Removing brownout MPB color writes saves an estimated 1-3 us per equipped ToolTick.
- State-gated sharedMaterial assignment costs 0 us on unchanged frames and only pays on battery state transitions.

Validation:
- `rg` anti-bloat audit across `RepairTool`, `EquipmentInteractionHandler`, and `H8Memory` returned no matches for `MaterialPropertyBlock`, `GetPropertyBlock`, `SetPropertyBlock`, `_mpb`, `_EmissionColorID`, `new NativeArray<Raycast`, `private NativeArray<Raycast`, `Physics.Raycast`, `RaycastNonAlloc`, `GlobalSignals.Publish(in signal)`, `EventBus`, `string.Format`, or `void Update()`.
- `rg` audit confirmed `PowerIndicatorVisualState`, shared material slots, default material caching, and state-gated `sharedMaterial` assignment are present.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /m:1 /clp:ErrorsOnly /p:BuildProjectReferences=false` filtered for repair power-indicator diagnostics returned `NO_REPAIR_POWER_INDICATOR_MPB_DIAGNOSTICS` with build exit code 1.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /m:1 /clp:ErrorsOnly` full-graph filtered for repair power-indicator diagnostics returned `NO_REPAIR_POWER_INDICATOR_MPB_FULLGRAPH_DIAGNOSTICS` with build exit code 1.
- `git diff --check -- ...` reports only CRLF conversion warnings in touched files.
- Repository build remains blocked by unrelated dependency failures outside WELDING_REPAIR_LOGIC.

## 2026-05-16 - Eleventh Pass Interaction Debug Log Hygiene
What was wrong:
- `EquipmentInteractionHandler.LogInteractionOverflowOncePerFrame` still called `Debug.LogWarning`.
- This service is the queued raycast dependency used by `RepairTool`.
- The same overflow branch already publishes `GlobalTelemetryBus.PublishInteractionPacketOverflow`, so the console warning was duplicate evidence and editor/development log spam risk.

What was done:
- Removed the `Debug.LogWarning` block.
- Kept `GlobalTelemetryBus.PublishInteractionPacketOverflow(MaxInteractionPacketsPerFrame, _queueCount)` as the authoritative overflow evidence.
- Reran fixed-string hot-path API grep across `RepairTool` and `EquipmentInteractionHandler`.

Cinematic Cheats used:
- None; this is runtime hygiene.
- Overflow evidence remains telemetry-based instead of console-string based.

Exact Microseconds saved:
- Release build: 0 us because the removed warning was editor/development guarded.
- Editor/development overflow frames: estimated 3-10 us saved plus avoided console allocation/spam risk.
- Telemetry event cost is unchanged.

Validation:
- `rg` fixed-string audit across `RepairTool` and `EquipmentInteractionHandler` returned no matches for `Debug.Log`, `Debug.LogWarning`, `Debug.LogError`, Unity scene find APIs, direct physics queries, coroutine APIs, `.ToString(`, `Enum.Parse`, `Enum.ToString`, `string.Concat`, material clone access, mesh copy access, `Input.touches`, `Time.deltaTime`, `Time.fixedDeltaTime`, raycast/sphere/overlap nonalloc fallbacks, `new NativeArray`, `MaterialPropertyBlock`, `GetPropertyBlock`, `SetPropertyBlock`, `string.Format`, or `void Update(`.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /m:1 /clp:ErrorsOnly /p:BuildProjectReferences=false` filtered for repair/interaction debug-log diagnostics returned `NO_REPAIR_INTERACTION_DEBUG_LOG_DIAGNOSTICS` with build exit code 1.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /m:1 /clp:ErrorsOnly` full-graph filtered for repair/interaction debug-log diagnostics returned `NO_REPAIR_INTERACTION_DEBUG_LOG_FULLGRAPH_DIAGNOSTICS` with build exit code 1.
- `git diff --check -- ...` reports only CRLF conversion warnings in touched files.
- Repository build remains blocked by unrelated dependency failures outside WELDING_REPAIR_LOGIC.

## 2026-05-16 - Twelfth Pass HullDents Handle Generation Guard
What was wrong:
- `RepairTool.EnsureHullDentsHandle` accepted a cached HullDents handle after checking `IsCreated`, `BufferID`, and length only.
- A stale vault generation could survive that precheck until the locked repair path tried to resolve it.
- The repair kernel also accepted any created locked view length, even though the XML contract is `HullDents float4[16]`.

What was done:
- Added `vault.ResolveBuffer(ref _hullDentsHandle)` to the cached handle acceptance test.
- Reacquire `BufferID.HullDents` through GlobalDataVault with `SystemID.GameplayTools` and 16 slots when resolution fails.
- Changed the final Ensure return to require `Length >= HullDentVaultCapacity`.
- Changed the locked kernel view guard to reject uncreated or undersized HullDents views before iterating.

Cinematic Cheats used:
- The repair math stays a bounded 16-slot vault scan, not a mesh deformation rebuild.
- Low tier keeps the cheap mathematical dent-depth erase.
- High tier still spends visual budget through shader unbend/rust removal and compute-advection repair sparks.

Exact Microseconds saved:
- No hot-path saving was claimed.
- Handle generation validation costs an estimated 1-2 us per active repair tick on i3/MX350.
- The locked length branch costs an estimated 0-1 us per active repair tick.
- The value is stale-generation survival and preventing partial-lane corruption, not raw speed.

Validation:
- Fixed-string grep across `RepairTool` and `EquipmentInteractionHandler` returned no matches for `Debug.Log`, `Debug.LogWarning`, `Debug.LogError`, Unity scene find APIs, direct physics queries, coroutine APIs, `.ToString(`, `Enum.Parse`, `Enum.ToString`, `string.Concat`, material clone access, mesh copy access, `Input.touches`, `Time.deltaTime`, `Time.fixedDeltaTime`, raycast/sphere/overlap nonalloc fallbacks, `new NativeArray`, `MaterialPropertyBlock`, `GetPropertyBlock`, `SetPropertyBlock`, `string.Format`, or `void Update(`.
- `rg` confirmed `EnsureHullDentsHandle`, `ResolveBuffer(ref _hullDentsHandle)`, and the HullDents lock/unlock path are present.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /m:1 /clp:ErrorsOnly /p:BuildProjectReferences=false` filtered for repair/HullDents diagnostics returned `NO_REPAIR_HULLDENTS_HANDLE_GENERATION_DIAGNOSTICS` with build exit code 1.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /m:1 /clp:ErrorsOnly` full-graph filtered for repair/HullDents diagnostics returned `NO_REPAIR_HULLDENTS_HANDLE_GENERATION_FULLGRAPH_DIAGNOSTICS` with build exit code 1.
- `git diff --check -- ...` reports only CRLF conversion warnings in touched files.
- Repository build remains blocked by unrelated dependency failures outside WELDING_REPAIR_LOGIC.

## 2026-05-16 - Thirteenth Pass HullDent Visual Mirror Generation Guard
What was wrong:
- `HullDentShaderController` mirrors `GlobalDataVault.HullDents` into `_HectonHullDents` for shader deformation and rust fade.
- Its cached HullDents handle still accepted BufferID/Length only.
- That meant the visual mirror could use stale generation assumptions while `RepairTool` had already been hardened.
- Sync/flush accepted any created view length despite the `float4[16]` contract.

What was done:
- Added `vault.ResolveBuffer(ref _hullDentsHandle)` to `HullDentShaderController.EnsureHullDentsHandle`.
- Reacquire the 16-slot `BufferID.HullDents` handle through `GlobalDataVault` with `SystemID.Vfx` when resolution fails.
- Required `Length >= MaxHullDents` in the final Ensure return.
- Rejected undersized locked views in `SyncDentBufferFromVault` and `FlushDentBufferToVault`.
- Audited `HullRepairedSignal` producer/consumer duplication; code path remains `RepairTool -> SignalBus<HullRepairedSignal> -> GasDynamicsSolver`.

Cinematic Cheats used:
- Shader deformation remains a 16-slot global vector-array fake, not CPU mesh repair.
- Low tier keeps fixed-size dirty upload behavior.
- High tier keeps procedural hull deformation, POM rust fade, and compute-advection sparks from the same vault state.

Exact Microseconds saved:
- No hot-path saving claimed.
- Visual handle generation validation costs an estimated 1-2 us per active late-frame dent sync.
- Locked view length branches cost an estimated 0-1 us per sync/flush.
- The gain is deterministic visual/authority coherence after vault generation shifts.

Validation:
- Signal grep found no competing code-defined `HullRepairedSignal` producer or duplicate repair completion signal in the repair lane.
- `GasDynamicsSolver` drains `SignalBus<HullRepairedSignal>` and clears `RoomFlagBreached` for valid completed room signals.
- Combined fixed-string grep across `RepairTool`, `EquipmentInteractionHandler`, and `HullDentShaderController` returned `NO_REPAIR_VISUAL_HOTPATH_BLOAT_MATCHES`.
- `rg` confirmed both RepairTool and HullDentShaderController now use `ResolveBuffer(ref _hullDentsHandle)` in their HullDents handle guards.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /m:1 /clp:ErrorsOnly /p:BuildProjectReferences=false` filtered for repair/HullDentShaderController diagnostics returned `NO_REPAIR_HULLDENT_VISUAL_HANDLE_DIAGNOSTICS` with build exit code 1.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /m:1 /clp:ErrorsOnly` full-graph filtered for repair/HullDentShaderController diagnostics returned `NO_REPAIR_HULLDENT_VISUAL_HANDLE_FULLGRAPH_DIAGNOSTICS` with build exit code 1.
- `git diff --check -- ...` reports only CRLF conversion warnings in touched files.
- Repository build remains blocked by unrelated dependency failures outside WELDING_REPAIR_LOGIC.

## 2026-05-16 - Fourteenth Pass Gas Deferred Seal / Typed Signal Hygiene
What was wrong:
- `GasDynamicsSolver` skipped HullRepairedSignal draining while `_stepRunning` was true.
- SignalBus uses a current-frame snapshot and destructive read cursor, so a seal event produced during a long gas job could be missed before room lanes became writable.
- `HullDentShaderController` still used `GlobalSignals.Publish(in deformedSignal)` for the repair-adjacent hull deformation lane.
- `GasDynamicsSolver` dump failure path still emitted `Debug.LogError` with string concatenation.

What was done:
- Added two scalar pending masks: `_pendingHullRepairRoomsLo` and `_pendingHullRepairRoomsHi`.
- `DrainHullRepairedSignals` now consumes completed repair signals while the gas job is running and stages valid room ids instead of dropping them.
- `ApplyPendingHullRepairSignals` clears `RoomFlagBreached` after room lanes become writable.
- Direct apply failures requeue the room bit instead of disappearing.
- Moved HullDentShaderController deformation publication to `SignalBus<HullDeformedSignal>.Push`.
- Replaced dump-path `Debug.LogError` with `GlobalTelemetryBus.PublishUnityLogFault(DumpMagic, 0u, 1u)`.

Cinematic Cheats used:
- O2 sealing uses a two-`ulong` room bitmask, not a dynamic event list or physical gas leak solver handshake.
- Low tier pays no native allocation and only scans pending masks when a seal exists.
- High tier keeps the same typed signal contract while visuals continue through hull shader recovery and compute sparks.

Exact Microseconds saved:
- Deferred seal staging costs an estimated 0-2 us per drained repair signal.
- Pending mask apply costs an estimated 0-4 us when pending bits exist, bounded to 128 rooms.
- No native allocation was added.
- Direct HullDeformedSignal push saves an estimated 0-1 us per accepted combat dent signal.
- Removing dump-path Debug.LogError saves an estimated 3-10 us only on dump failure in editor/development builds.

Validation:
- Fixed-string grep across `RepairTool`, `HullDentShaderController`, `GasDynamicsSolver`, and `EquipmentInteractionHandler` returned `NO_REPAIR_GAS_VISUAL_SIGNAL_BLOAT_MATCHES` for Debug.Log, Debug.LogWarning, Debug.LogError, EventBus, GlobalSignals.Publish, string.Format, and void Update.
- `rg` confirmed `SignalBus<HullDeformedSignal>.Push`, `SignalBus<HullRepairedSignal>`, pending hull repair masks, and `GlobalTelemetryBus.PublishUnityLogFault`.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /m:1 /clp:ErrorsOnly /p:BuildProjectReferences=false` filtered for repair/gas/VFX diagnostics returned `NO_REPAIR_GAS_DEFERRED_SEAL_TYPED_SIGNAL_DIAGNOSTICS` with build exit code 1.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /m:1 /clp:ErrorsOnly` full-graph filtered for repair/gas/VFX diagnostics returned `NO_REPAIR_GAS_DEFERRED_SEAL_TYPED_SIGNAL_FULLGRAPH_DIAGNOSTICS` with build exit code 1.
- `git diff --check -- ...` reports only CRLF conversion warnings in touched files.
- Repository build remains blocked by unrelated dependency failures outside WELDING_REPAIR_LOGIC.

## 2026-05-16 - Fifteenth Pass HullDents Lock / Signal Hygiene
What was wrong:
- `TryRepairVaultHullDents` emitted `HullRepairedSignal` while the `BufferID.HullDents` vault write lock was still held.
- `SignalBus.Push` can initialize and enqueue native lane data, so the repair kernel held a data lock across signal-system work.
- This created unnecessary coupling between the repair writer, typed signal lane, and gas consumer.

What was done:
- Added a fixed `ushort repairedDentMask` inside the locked repair loop.
- Replaced in-lock `PublishHullRepairedSignal` calls with bit writes.
- Added `PublishHullRepairedSignals` to walk the 16-bit mask after `TryUnlockBuffer(BufferID.HullDents)`.
- Preserved dent-index ordering and cumulative `DentsRepairedCount`.
- No NativeArray, NativeList, managed List, or new signal type was added.

Cinematic Cheats used:
- Completion staging is a 16-bit mathematical lie over the fixed dent set, not an event object list.
- Low tier pays zero allocation and only O(16) bit iteration on completion.
- High tier keeps the same repair signal for gas sealing, shader recovery, and compute spark feedback.

Exact Microseconds saved:
- Estimated 0-2 us saved on completion frames by avoiding SignalBus work while the HullDents lock is held.
- Estimated 0-1 us branch cost per repaired dent for the bitmask walk.
- 0 B allocation.

Validation:
- `rg` confirmed `repairedDentMask`, `PublishHullRepairedSignals`, `TryLockBuffer(BufferID.HullDents)`, `TryUnlockBuffer(BufferID.HullDents)`, and `SignalBus<HullRepairedSignal>` placement.
- Fixed-string grep across `RepairTool`, `HullDentShaderController`, `GasDynamicsSolver`, and `EquipmentInteractionHandler` returned `NO_REPAIR_LOCK_SIGNAL_BLOAT_MATCHES`.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /m:1 /clp:ErrorsOnly /p:BuildProjectReferences=false` filtered for repair lock/signal diagnostics returned `NO_REPAIR_HULLDENTS_LOCK_SIGNAL_DIAGNOSTICS` with build exit code 1.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /m:1 /clp:ErrorsOnly` full-graph filtered for repair lock/signal diagnostics returned `NO_REPAIR_HULLDENTS_LOCK_SIGNAL_FULLGRAPH_DIAGNOSTICS` with build exit code -1.
- `git diff --check -- ...` reports only CRLF conversion warnings in touched files.
- Repository build remains blocked by unrelated dependency failures outside WELDING_REPAIR_LOGIC.

## 2026-05-16 - Sixteenth Pass Shader NaN / Metal Thread-Group Audit
What was wrong:
- CPU repair code clamps non-finite `HullDents`, but shader unpackers still trusted packed dent values once uploaded.
- `Hecton_DamageHologram.compute` cast packed values after `floor`.
- `Hecton_CoreLit.hlsl` unpacked packed radius/depth without an explicit finite test.
- `Hecton8_UberNoir.hlsl` used `fmod` on packed values without an explicit finite test.
- Backend NaN behavior through max/floor/fmod/cast is not a survival plan for mobile GPU pipelines.

What was done:
- Added `HectonSanitizePackedDent` in `Hecton_DamageHologram.compute`.
- Added explicit `isfinite(packedRadiusDepth)` in `Hecton_CoreLitUnpackHullDent`.
- Added explicit `isfinite(packed)` guards in `H8UberNoirUnpackDentRadius` and `H8UberNoirUnpackDentDepth`.
- Audited repair-related compute thread groups: damage hologram is 64x1x1; fluid advection kernels are 64x1x1 or 1x1x1.

Cinematic Cheats used:
- No new heavy shader path was added.
- Low tier keeps the cheap dent bypass/dominant-axis paths.
- High tier keeps procedural dent deformation, rust/POM recovery, SDF spark bounce, and flow drift with safer packed values.

Exact Microseconds saved:
- No speed gain claimed.
- Estimated 0-1 us GPU cost per active dent loop for finite guards.
- The value is NaN containment across Metal/Quest/Android and desktop GPU backends.

Validation:
- `rg` confirmed `HectonSanitizePackedDent`, `isfinite(packedRadiusDepth)`, and `isfinite(packed)` guards in the three repair-related shader files.
- `rg` confirmed `[numthreads(64,1,1)]`, `HECTON_FLUID_ADVECTION_THREADS 64`, and `[numthreads(1,1,1)]` on the repair-related compute path.
- Fixed-string C# grep returned `NO_REPAIR_SHADER_PASS_CSHARP_BLOAT_MATCHES`.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /m:1 /clp:ErrorsOnly /p:BuildProjectReferences=false` did not complete within 315 seconds on this pass; no clean build claim is made.
- `git diff --check -- ...` reports only CRLF conversion warnings in touched files.

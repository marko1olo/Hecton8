# LOG_VERLET_TOW_WINCH

## 2026-05-16 - Verlet Tow Winch Implementation

What was wrong:
- Tow cable authority was still exposed as a procedural tether path without the full VERLET_TOW_WINCH contract: no published 10-segment SOA cable state, no explicit tension signal, no local-offset AUP-safe Verlet authority, no High/Ultra indirect render path, and no `PeakCableTension` blackbox field.
- Full compile validation is currently blocked by unrelated cross-domain errors in player kinematics, visor, spatial audio, AI/fauna contracts, and registry contract types.

What was done:
- Added fixed DataVault BufferID lanes for canonical cable positions, previous positions, velocities, masses, segment tensions, and blackbox ownership.
- Implemented `VerletCableSolverJob` with pinned endpoints, segment stretch correction, `math.rsqrt` normalization, finite guards, and per-segment tension output.
- Converted active Verlet authority to local offset space relative to the tow anchor, with visual upload converting back to runtime world coordinates.
- Added DataVault/fallback SOA publication for 11 canonical cable points and 10 canonical cable segments.
- Added `TetherTensionSignal` publication with AUP endpoints, force, snap threshold, normalized tension, reactive VFX scalar, and node count.
- Added Low/MX350 3-segment authority and high-tension straight-line visual fake.
- Added High/Ultra `Graphics.RenderMeshIndirect` rendering through a persistent six-vertex cylindrical impostor segment mesh and shader `SV_InstanceID` segment mapping.
- Added reactive stress shader scalar path plus high-threshold creak/snap impact signals.
- Added velocity clamping to `MaxCableVelocity`, `PeakCableTension` telemetry, and `Docs/AgentLogs/Dump_VERLET_TOW_WINCH.bin` dump paths.
- Moved tether fixed tick registration to `PriorityLayer.Environment`, ahead of `PlayerKinematicsRuntime` in `PriorityLayer.Player`.
- Routed equal/opposite endpoint force packets through `PhysicsForceRouter`, scaled by `MassSub / (MassSub + MassWreck)`.
- Snap now clears the cable DataVault slot and publishes an `ImpactSignal` with snap material hash.

Cinematic Cheats used:
- Low tier uses 3 authority segments and a taut straight-line visual fake above high tension instead of spending 10-segment solve cost on MX350.
- High/Ultra spend saved CPU on indirect cylindrical impostor rendering and stress pulse instead of increasing physics realism.
- Motion vectors use camera-valid render mode to avoid invalid per-object history from procedural cable deformation.

Exact Microseconds saved:
- Singleton/joint purge: 0 us direct runtime, avoids unbounded PhysX spring-solver spikes.
- Low tier 3-segment solve vs 10-segment high path: estimated 6-12 us saved per active tether step on i3/MX350.
- Local-offset rebase: estimated <1 us for 11 nodes; prevents precision-failure recovery cost.
- Velocity clamp: estimated <1 us per 11-node integration pass.
- DataVault SOA publish: estimated +3-6 us per active tether; accepted to buy zero-GC cross-system visibility.
- Tension signal publish: estimated <2 us when active.
- Snap cleanup: estimated <3 us for the fixed DataVault slot.
- High/Ultra indirect draw: estimated 0-5 us CPU saved versus repeated procedural segment submission; profiler evidence is pending.

Validation:
- `rg` verified no first-party tether singleton/joint path in touched tether files.
- `rg` verified `math.rsqrt` in `VerletCableSolverJob`.
- `git diff --check` returned no whitespace errors for touched files; line-ending warnings are repository-normal CRLF conversion warnings.
- Compile attempt 1: `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /p:UseSharedCompilation=false` failed on unrelated cross-domain errors, with no tether errors in the reported set.
- Compile attempt 2: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false` failed before tether validation on missing AI/animation/registry contract types.
- Compile attempt 3: full `Assembly-CSharp.csproj` errors-only build timed out after 306 seconds with no final compiler result.
- Unity runtime and profiler validation were not executed.

## 2026-05-16 - Multiplatform / H-Phi Inquisition Pass

What was wrong:
- Snap and fire paths still had private tether signal ownership. Fire used a private `NativeQueue<TetherFiredSignal>`.
- Public cable DataVault lanes still had a private fallback allocation path.
- `TetherFiredSignal` was in an unstable contract location for the generated project; directed compile caught it.
- Full statelessness is not achieved: per-instance solver/visual working arrays still exist, now memory-sentinel tracked through `H8Memory`.

What was done:
- Converted fire notification to `SignalBus<TetherFiredSignal>` and `ReadOnlySpan<TetherFiredSignal>` snapshot reads.
- Kept only a bounded 16-entry managed Unity-object resolver sidecar for immediate same-frame attach. No delegate/EventBus/native queue remains in the tether fire path.
- Kept snap and tension on typed signal lanes.
- Locked tether signal/telemetry payloads to `Pack=1` explicit sizes.
- Added an epsilon floor before the solver correction-weight reciprocal.
- Removed the private fallback allocation for public DataVault cable SOA lanes.
- Routed remaining tether-owned runtime `NativeArray` allocation/release through `H8Memory` with `SystemID.Physics`.
- Moved the real compiled `TetherFiredSignal` payload into `TetherSignals.cs` and restored `TetherSignalContracts.cs` as an empty generated-project anchor.

Cinematic Cheats used:
- Low tier remains 3 authority segments plus taut-line fake.
- High/Ultra still spend CPU savings on indirect cable impostor stress rendering, not extra physics realism.

Exact Microseconds saved:
- Fire private queue purge: no measured frame-time claim; removed one persistent native queue and one private queue drain.
- Public DataVault fallback removal: no frame-time claim; prevents hidden fallback allocation and split-state cost.
- Pack=1/contract placement: 0 us runtime; build and binary-layout stability fix.
- Correction-weight reciprocal guard: estimated <1 us per 11-node pass; prevents near-zero divide amplification.
- Remaining estimates are unchanged: Low tier saves roughly 6-12 us versus the 10-segment high path; DataVault publish remains estimated +3-6 us.

Validation:
- Static scan found no `NativeQueue<TetherFiredSignal>`, `NativeQueue<TetherSnappedSignal>`, EventBus/delegate path, Unity Joint, `Update` family, `string.Format`, `TetherManager.Instance`, or distance helper hit in touched tether files.
- `git diff --check` returned only repository-normal CRLF conversion warnings.
- Compile attempt 10: `dotnet build Hecton8.Core.csproj -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false` failed on unrelated XR refresh-rate API, item signal import, submarine structural breach buffers, biolum buffers, and vault probe generic inference errors. No tether compiler errors appeared in the reported set.
- Unity runtime and profiler validation were not executed.

## 2026-05-16 - Vault-Owned Verlet Blackbox

What was wrong:
- The 300-frame Verlet telemetry blackbox was still a per-instance persistent `NativeArray`, so the crash recorder had private storage after the public cable lanes were evicted.

What was done:
- Moved the telemetry ring to `GlobalDataVault` under `BufferID.TetherCableBlackBox`.
- Added `BufferID.TetherCableBlackBoxHead` for one cursor per fixed tether slot.
- Changed `TetherVerletTelemetryJob` and dump export to write/read by fixed slot offsets instead of treating the telemetry ring as instance-owned.

Cinematic Cheats used:
- None in this pass; this is data sovereignty and crash forensics. The low-tier physics cheat remains the 3-segment authority solve with taut-line visual fallback.

Exact Microseconds saved:
- Not measured. Expected runtime delta is one int cursor lane write and offset clamp per active tether frame; no runtime win is claimed.

Validation:
- Corrected hot-path scan found no `Update`, `FixedUpdate`, `LateUpdate`, `string.Format`, banned Unity joints, `TetherManager.Instance`, or legacy tether queues in the touched path.
- `dotnet build Hecton8.Core.csproj -v:minimal /p:UseSharedCompilation=false` still fails on unrelated Sargassum, MarineSnow, and VehicleDocking errors. No tether compiler errors appeared in the reported set.
- Unity runtime and profiler validation were not executed.

## 2026-05-16 - Vault-Owned Manager Blackbox

What was wrong:
- `TetherManager` still owned a separate 300-entry `NativeArray<TetherManagerTelemetryEntry>` blackbox and local cursor after the instance-level Verlet blackbox was evicted.

What was done:
- Moved the manager heartbeat ring to `GlobalDataVault` under `BufferID.TetherManagerBlackBox`.
- Moved the manager cursor to `BufferID.TetherManagerBlackBoxHead`.
- Corrected BufferID assignments after detecting collisions with MarineSnow and AcousticEcho lanes; final manager IDs are 232/233 after the current enum tail.

Cinematic Cheats used:
- None in this pass. This is memory ownership cleanup.

Exact Microseconds saved:
- Not measured. The path replaces local H8Memory allocation ownership with vault views; per-sample work remains one 16-byte write plus one cursor int update.

Validation:
- `rg` found no remaining `H8Memory.Allocate<TetherManagerTelemetryEntry>`, `H8Memory.Release(ref _telemetryRing)`, or manager telemetry sentinel registration.
- `dotnet build Hecton8.Core.csproj -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false` still fails on unrelated Lockstep, Ecosystem, and SubmarineFluid dependency errors. No tether compiler errors appeared in the reported set.
- Unity runtime and profiler validation were not executed.

## 2026-05-16 - Vault-Owned Solver/Visual Scratch

What was wrong:
- `TetherInstance` still owned visual staging and Verlet solver scratch as local persistent `NativeArray` state. That violated the DataVault sovereignty rule even after public cable lanes and blackbox lanes moved to `GlobalDataVault`.
- Slot release could not safely keep a local slice alias; a later activation could acquire a different slot while retaining an old slice view if aliases were not dropped.

What was done:
- Added tether scratch BufferIDs after the current `H8Memory.BufferID` enum tail: visual point positions, visual anchors, visual segment lengths, Verlet positions/previous/velocities/pins/masks, rest lengths, tensions, corrections, correction weights, solver stats, solver flags, and node fault flags.
- Reworked `TetherInstance` to request full vault buffers once and store per-slot `NativeArray.GetSubArray` views for the active tether slot.
- Removed all `H8Memory.Allocate`, `H8Memory.Release`, and `NativeMemorySentinel` native-array ownership calls from `TetherInstance`.
- Deactivation now disposes vault aliases before releasing the fixed slot, preventing stale slice reuse.

Cinematic Cheats used:
- Low tier remains the 3-segment authority solve with taut-line fake at high tension.
- High/Ultra retain the indirect cylindrical impostor cable path and stress pulse; no extra physics truth was added.

Exact Microseconds saved:
- No measured microsecond win is claimed. This is ownership cleanup, not a profiler-proven speedup.
- Per-frame allocation remains 0 B by design; slice acquisition is activation/reconfiguration work.
- Low tier estimate remains 6-12 us saved versus full 10-segment solve; DataVault public publish remains estimated +3-6 us per active tether.

Validation:
- Duplicate BufferID scan returned `NO_DUPLICATE_BUFFER_IDS`.
- Static scan found no `H8Memory.Allocate`, `H8Memory.Release`, native-array sentinel registration/unregistration, or old native-array helper methods in `TetherInstance.cs`.
- Hot-path scan found no tether `Update`, `FixedUpdate`, `LateUpdate`, `string.Format`, legacy EventBus/delegate path, Unity Joint type, `TetherManager.Instance`, `math.distance`, or `distance(` hit in the touched tether path.
- `dotnet build Hecton8.Core.csproj -v:minimal /p:UseSharedCompilation=false` still fails on unrelated `GameBootstrapper.Initialize` arity and `ToolDurabilitySystem` missing-field/member errors. No tether compiler errors appeared in the reported set.
- Unity runtime and profiler validation were not executed.

## 2026-05-16 - Fire Sidecar Purge / Fixed-Step Clock

What was wrong:
- `TetherSignals` still carried Unity object references through a managed fire resolver sidecar after the typed lane migration.
- Payload current sampling read `Time.fixedTime` inside the fixed-step tether solve path.

What was done:
- Removed the fire resolver sidecar completely.
- `TetherSignals.PublishFire` now publishes only the unmanaged `TetherFiredSignal` typed lane payload.
- `HeavyTowWinch` executes same-owner attach directly through `TetherManager.ExecuteFireRequest` after the fire signal is published.
- Added a finite wrapped tether fixed-step clock in `TetherManager`, advanced only from dispatcher `fixedDeltaTime`, and passed it into `TetherInstance.Simulate`.
- Replaced `Time.fixedTime` in payload-current sampling with the passed fixed-step clock.

Cinematic Cheats used:
- Low tier keeps the 3-segment authority solve and taut-line visual fake under high tension.
- High/Ultra keep indirect cylindrical cable impostors and stress pulses; no extra physical truth was added.

Exact Microseconds saved:
- Fire resolver sidecar purge: no measured frame-time claim. Removed the fixed resolver queue and scan path.
- `Time.fixedTime` removal: no measured frame-time claim. Determinism/clock ownership fix.
- Existing estimates remain unchanged: Low tier saves roughly 6-12 us versus full 10-segment authority; DataVault publish remains estimated +3-6 us per active tether.

Validation:
- Static scan found no `TetherFireRequest`, fire resolver array, `TryConsumeFireForManager`, private fire queue, `Time.fixedTime`, `Time.deltaTime`, `Time.fixedDeltaTime`, EventBus/delegate path, Unity Joint type, `Update` family, `string.Format`, `TetherManager.Instance`, `math.distance`, or `distance(` hit in touched tether files.
- Duplicate BufferID value scan returned `NO_DUPLICATE_BUFFER_ID_VALUES`.
- Struct layout scan confirmed tether signal/telemetry payloads remain `Pack=1`.
- `dotnet build Hecton8.Core.csproj -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- `dotnet build Assembly-CSharp.csproj -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false` failed in unrelated `RealtimeCSG.csproj` missing source-file references. No tether compiler errors appeared in the reported set.
- Unity runtime and profiler validation were not executed.

## 2026-05-16 - GPU Double Buffer / High-Tier Cable Surface Pass

What was wrong:
- Tether visuals still uploaded position and tension data into the same public GPU buffers that the render pass consumed.
- High/Ultra cable visuals had the indirect cylindrical impostor route but not enough quality-tier surface detail to justify the saved CPU path.

What was done:
- Replaced the single visual position/tension `GraphicsBuffer` fields with explicit A/B buffers.
- Added buffer-size/stride guards so both A/B lanes are released and rebuilt together when visual point count or tension segment count changes.
- Upload now writes to the non-current buffer with `GraphicsBuffer.UsageFlags.LockBufferForWrite`, then flips the read index after upload.
- Added shader quality controls: `_TetherVisualTier`, `_TetherCrystalDensity`, and `_TetherSiltIntensity`.
- High tier now uses a 16-tap procedural cable-fiber occlusion pass plus salt glints and silt tint in `Hecton_TetherLineStrip.shader`.
- Ultra adds a stress rim on the same impostor path. Low/MX350 remains visual tier 0.

Cinematic Cheats used:
- Salt crystals, silt wake tint, and cable fiber breakup are fragment-stage procedural fakes, not new physics, particles, textures, or file reads.
- Low tier still uses 3 authority segments and the taut-line fake under high tension.

Exact Microseconds saved:
- No new measured microsecond win is claimed.
- Existing estimate remains: Low/MX350 saves roughly 6-12 us versus full 10-segment authority; High/Ultra indirect repetition remains estimated CPU neutral to -5 us versus repeated primitive submission.
- The 16-tap shader path is quality-gated and unmeasured without Unity/Profiler evidence.

Validation:
- Static scan found no direct upload to `VisualSegmentBuffer` or `VisualSegmentTensionBuffer`, no property assignments to read-only buffer accessors, and no single-buffer release sites.
- Hot-path scan found no `Time.fixedTime`, `Time.deltaTime`, `Time.fixedDeltaTime`, `H8Memory.Allocate`, `H8Memory.Release`, `NativeQueue<TetherFiredSignal>`, `NativeQueue<TetherSnappedSignal>`, EventBus/delegate path, `Update` family, `string.Format`, Unity Joint type, `TetherManager.Instance`, `math.distance`, or `distance(` hit in touched tether files.
- Shader scan found no `numthreads`, `RWTexture`, `ByteAddressBuffer`, `SV_Group`, or `groupshared` token in the tether shader.
- `git diff --check` reported only line-ending normalization warnings.
- `dotnet build Hecton8.Core.csproj -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false` succeeded once after the initial GPU double-buffer pass, then later failed after concurrent out-of-domain edits in `DiegeticGyroCompassRuntime`, `ArchitectEyeVisualizer`, `GlobalSignals`, and `SystemDispatcher`. No tether compiler errors appeared in those reported sets.
- `dotnet build Assembly-CSharp.csproj -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false` remains blocked by unrelated `RealtimeCSG.csproj` missing source-file references.
- Unity runtime and profiler validation were not executed.

## 2026-05-16 - Deterministic Visual Clock / Time Purge

What was wrong:
- The tether shader still used Unity `_Time` for cable stress pulse and salt glint animation.
- The High/Ultra procedural hash used `sin`, which is unnecessary ALU for a deterministic surface fake.
- Tether fire/snap/tension/blackbox frame stamps still read `Time.frameCount`.

What was done:
- Added `_TetherVisualClock` to the tether shader and property block.
- Fed `_TetherVisualClock` from `TetherManager`'s wrapped fixed-step clock.
- Replaced shader `_Time` usage with `_TetherVisualClock`.
- Replaced the sine hash with multiply/frac hash math.
- Added a manager-owned fixed simulation frame index and passed it into `TetherInstance.Simulate`.
- Replaced tether telemetry, tension, snap, fire, and cooldown frame stamps with the fixed simulation frame index.
- Reworked snap snapshot cursor reset to use the snapshot's own `FrameIndex`, not Unity frame count.

Cinematic Cheats used:
- Salt/silt/fiber breakup now uses deterministic multiply/frac and triangle fake math.
- Low tier remains the 3-segment authority solve with taut-line visual fake; no high-tier shader branch is forced onto MX350.

Exact Microseconds saved:
- No measured microsecond win is claimed.
- Expected benefit: removed shader trig from the High/Ultra salt/silt hash path; exact GPU time requires Unity/Profiler capture.
- Existing estimates remain: Low/MX350 saves roughly 6-12 us versus full 10-segment authority; High/Ultra indirect repetition remains estimated CPU neutral to -5 us versus repeated primitive submission.

Validation:
- Static scan found no `Time.frameCount`, `Time.fixedTime`, `Time.deltaTime`, `Time.fixedDeltaTime`, `_Time`, or `sin(` hit in touched tether scripts/shader.
- Static scan found no `H8Memory.Allocate`, `H8Memory.Release`, private fire/snap `NativeQueue`, EventBus/delegate path, `Update` family, `string.Format`, Unity Joint type, `TetherManager.Instance`, `math.distance`, or `distance(` hit in touched tether files.
- `git diff --check` reported only line-ending normalization warnings.
- `dotnet build Hecton8.Core.csproj -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false` failed first on out-of-domain `BiolumPulseSyncRuntime.ResolveDataVault`, then on out-of-domain `LockstepStateValidator` missing signal capacity/hash constants. No tether compiler errors appeared in the reported sets.
- Unity runtime and profiler validation were not executed.

## 2026-05-16 - NaN Blackbox Hardening Pass

What was wrong:
- The Burst jobs still had edge paths where non-finite pinned endpoint data, origin-shift offsets, acceleration, solver stats, or blackbox anchor/payload positions could be written into vault-backed NativeArrays.
- The Verlet telemetry dump was compiled only for editor/development builds.
- The manager blackbox dump and Verlet blackbox dump both wrote `Docs/AgentLogs/Dump_VERLET_TOW_WINCH.bin` with `FileMode.Create`, so the later fault could erase the earlier fault section.

What was done:
- Sanitized pinned endpoint writes in `TetherVerletIntegrationJob` and `VerletCableSolverJob`.
- Sanitized integration acceleration and `DeltaTimeSq` before advancing nodes.
- Sanitized origin-shift writes for positions, previous positions, and pinned positions.
- Sanitized solver stats and blackbox anchor/payload/tension fields before committing telemetry.
- Guarded tether-managed Rigidbody angular velocity assignment with finite fallback.
- Removed the dev-build-only gate from Verlet dump generation.
- Changed manager and Verlet dump writes to append binary sections into the same mandated dump file instead of overwriting each other.

Cinematic Cheats used:
- No new simulation truth was added. This pass keeps the existing MX350 3-segment/taut-line fake and High/Ultra procedural cable surface fakes intact.

Exact Microseconds saved:
- No measured microsecond saving is claimed.
- Hot-path cost added is bounded finite checks inside existing fixed-size jobs; profiler proof is absent.
- The only disk I/O remains fault-path append output, not per-frame logging.

Validation:
- Static scan found no `Time.frameCount`, `Time.fixedTime`, `Time.deltaTime`, `Time.fixedDeltaTime`, `_Time`, `sin(`, `H8Memory.Allocate`, `H8Memory.Release`, private tether `NativeQueue`, EventBus/delegate path, `Update` family, `string.Format`, Unity Joint type, `TetherManager.Instance`, `math.distance`, or `distance(` hit in touched tether files/shader.
- `dotnet build Hecton8.Core.csproj -v:minimal /p:UseSharedCompilation=false` failed on out-of-domain `SubmarineFluidDynamics` missing exterior thermal anomaly/hazard fields. No tether compiler errors appeared in the reported set.
- A concurrent quiet summary build failed on out-of-domain `SubmarineFluidDynamics` ambiguous `float3`/`Vector3` subtraction. No tether compiler errors appeared in that reported set.
- After final rebase/angular-velocity finite guards and build-server restart, `dotnet build Hecton8.Core.csproj -v:minimal /clp:Summary /p:UseSharedCompilation=false` failed on out-of-domain `Hecton8.Core.Memory.Defrag` / `MemoryDefragPhase` missing references in `SystemDispatcher` and `GlobalDataVault`. No tether compiler errors appeared in the reported set.
- Unity runtime and profiler validation were not executed.

## 2026-05-16 - Cold Dependency Cache Pass

What was wrong:
- `TetherInstance` still read `GlobalRegistry.ScalabilityTier`, `GlobalRegistry.MapMagicVegetation`, `GlobalRegistry.Fluid`, `GlobalRegistry.Weather`, and `GlobalRegistry.DataVault` from fixed/visual-adjacent code paths.
- Quality-tier decisions were duplicated inside instance helpers instead of flowing from a manager-owned dependency cache.

What was done:
- Added `ISlowTickable` to `TetherManager` and cached quality tier, DataVault, vegetation flow, fluid flow, and weather service references in `RefreshColdDependencyCache`.
- Passed cached quality into `TetherInstance.Simulate` and `TetherInstance.UpdateVisuals`.
- Preserved existing public `Configure` and `UpdateVisuals` wrappers while adding internal overloads for manager-owned cached inputs.
- Replaced all direct `GlobalRegistry` reads in `TetherInstance` with manager-cached handles.

Cinematic Cheats used:
- No new simulation truth was added. Low/MX350 still uses 3-segment authority plus taut-line visual fake; High/Ultra still spend the saved authority budget on indirect cable impostors with procedural salt, silt, and cable fiber fakes.

Exact Microseconds saved:
- No measured microsecond saving is claimed.
- Expected saving is below profiler-proof threshold: removes per-tether registry/service polling from fixed and visual paths.
- Existing estimate remains: Low/MX350 saves roughly 6-12 us versus full 10-segment authority; High/Ultra indirect repetition remains estimated CPU neutral to -5 us versus repeated primitive submission.

Validation:
- `rg -n "GlobalRegistry\." Assets\_Project\Scripts\TetherInstance.cs` returned no hits.
- `rg` found remaining tether `GlobalRegistry` hits only in `TetherManager` registration/unregistration and `RefreshColdDependencyCache`.
- Static scan still found no `Time.frameCount`, `Time.fixedTime`, `Time.deltaTime`, `Time.fixedDeltaTime`, `_Time`, `sin(`, `string.Format`, Unity Joint type, `TetherManager.Instance`, `math.distance`, or `distance(` hit in the touched tether files/shader.
- Broad domain-adjacent scan across `*Tether*`, `*Tow*`, `*Winch*`, and `*Cable*` scripts returned no `Update` family, `string.Format`, Unity Joint, legacy EventBus/delegate, private `NativeQueue`, or Unity time-global hits.
- Struct/shader scans found tether runtime contracts at `Pack=1` and no tether shader `numthreads`, `RWTexture`, `ByteAddressBuffer`, `groupshared`, `_Time`, or `sin(` hits.
- `dotnet build Hecton8.Core.csproj -v:minimal /clp:Summary /p:UseSharedCompilation=false` first failed on out-of-domain `HectonPlayerMovement.cs(6499)` missing `System.Runtime.CompilerServices.MethodImpl` references. After the final tether call-site correction, the same core build succeeded with 0 warnings and 0 errors.
- `dotnet build Assembly-CSharp.csproj -v:minimal /clp:Summary /p:UseSharedCompilation=false` failed in out-of-domain `RealtimeCSG.csproj` missing source files plus generated editor/package metadata errors. No tether compiler errors appeared before the dependency wall.
- Unity runtime and profiler validation were not executed.

## 2026-05-17 - Tow Facade NaN Vaccination Pass

What was wrong:
- `HeavyTowWinch` normalized payload mass and tow drag with raw serialized values.
- Transport handoff blended raw Rigidbody masses and velocities.
- Snap recoil used raw segment directions, active tow mass, severity, velocity, and torque vectors before calling `PhysicsForceRouter` or `ITowSnapReceiver`.

What was done:
- Added finite tow mass bounds and replaced raw divisions with `math.rcp`-based normalization.
- Sanitized serialized scalar resolver outputs for tow length, break distance, force caps, current drag, angular clamps, bio-cable influence, and snap duration.
- Sanitized transport handoff mass/velocity before computing the shared velocity change.
- Normalized snap directions with `math.rsqrt`, with finite fallback axes for player and payload snap response.
- Guarded queued player/payload force and torque packets so zero or non-finite vectors do not enter the force router.
- Sanitized `TowSnapEventData` direction, velocity, torque, and severity before notifying snap receivers.

Cinematic Cheats used:
- No new physical simulation was added. This is authority-path hardening only.
- Low/MX350 keeps the 3-segment authority and taut-line visual fake; High/Ultra keep the indirect impostor cable with procedural fiber, salt, and silt fake detail.

Exact Microseconds saved:
- No measured microsecond saving is claimed.
- Added cost is limited to scalar finite checks and `math.rsqrt` direction normalization on attach/snap/transport handoff paths, not per-node cable solve.
- Existing estimates remain: Low/MX350 saves roughly 6-12 us versus full 10-segment authority; High/Ultra indirect repetition remains estimated CPU neutral to -5 us versus repeated primitive submission.

Validation:
- Static scan found no `Update` family, `string.Format`, Unity Joint, legacy EventBus/delegate, private `NativeQueue`, Unity time-global, singleton, `math.distance`, or `distance(` hits in touched tow/tether files.
- Struct scan confirmed tether signal and telemetry contracts remain `Pack=1`.
- Shader scan found no tether shader `numthreads`, `RWTexture`, `ByteAddressBuffer`, `groupshared`, `_Time`, or `sin(` hits.
- `dotnet build Hecton8.Core.csproj -v:minimal /clp:Summary /p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- `dotnet build Assembly-CSharp.csproj -v:minimal /clp:Summary /p:UseSharedCompilation=false` failed in out-of-domain `RealtimeCSG.csproj` missing source-file references: 9 warnings, 216 errors. No tether/tow compiler errors appeared before the dependency wall.
- Unity runtime and profiler validation were not executed.

## 2026-05-17 - Fire/Snare Ingress Hardening Pass

What was wrong:
- `TryAttach` passed raw `initialDistance` to both `TetherFiredSignal` and `ExecuteFireRequest`.
- `TetherSignals.PublishFire` trusted any caller-provided attach distance.
- `HeavyTowWinch` exposed raw transform axes and raw anchor position to downstream tether response paths.
- External cable-snare samples accepted raw anchor, tension, cut progress, and payload sample bounds.

What was done:
- Rejected non-finite or negative attach distance before fire signal publish and manager execution.
- Added a second finite attach-distance guard inside `TetherSignals.PublishFire`.
- Safe-normalized `PlayerRight`, `PlayerForward`, and `PlayerUp`.
- Added finite anchor-position fallback to the cached owner transform before returning zero.
- Sanitized external snare anchor/tension/cut progress in `TetherInstance.QueueExternalCableSnare`.
- Failed payload samples closed when payload COM is non-finite and fell back to a fixed radius when collider extents are non-finite.

Cinematic Cheats used:
- No visual or simulation embellishment added. This pass protects existing fakery: MX350 3-segment authority plus taut-line visual, and High/Ultra indirect cable impostor with procedural salt/silt/fiber fakes.

Exact Microseconds saved:
- No measured microsecond saving is claimed.
- Added cost is ingress-only finite checks and `math.rsqrt` axis normalization. It does not touch the per-node Verlet loop.

Validation:
- Static scan found no `Update` family, `string.Format`, Unity Joint, legacy EventBus/delegate, private `NativeQueue`, Unity time-global, singleton, `math.distance`, or `distance(` hits in touched tow/tether files.
- `git diff --check` reported only line-ending normalization warnings.
- `dotnet build Hecton8.Core.csproj -v:minimal /clp:Summary /p:UseSharedCompilation=false` failed on out-of-domain `SargassumMicroFaunaBoids` missing `_grazingAnchors`, `_formationBeacons`, `_formationObstacles`, and `_massiveThreats`: 0 warnings, 40 errors. No tether/tow compiler errors appeared in the reported set.
- Full `Assembly-CSharp.csproj` was not rerun after this pass because core is already dependency-blocked; the same-turn full probe remains blocked by out-of-domain `RealtimeCSG.csproj` missing source-file references.
- Unity runtime and profiler validation were not executed.

## 2026-05-17 - Rsqrt Finite-Length Hardening Pass

What was wrong:
- `TetherInstance` helper math still trusted squared magnitudes after `sqrMagnitude` / `math.lengthsq`.
- A finite-but-huge vector can overflow its squared magnitude to infinity; `infinity * math.rsqrt(infinity)` can become NaN.
- The Burst segment constraint loop used `math.max(lengthSq, epsilon)` before proving `lengthSq` finite.
- PID derivative clamp and cable velocity clamp trusted non-finite damping, force, and velocity caps.

What was done:
- Hardened `ClampVector`, `ResolveSafeDirection`, `ResolveMagnitude`, `ResolveLengthAndInvLength`, `ClampPdDerivativeVelocity`, and `ClampCableVelocity`.
- Hardened `TetherVerletIntegrationJob` velocity cap handling for non-finite `MaxCableVelocity` and overflowed velocity length.
- Hardened `VerletCableSolverJob` segment length normalization: non-finite length sets segment tension to zero and records a constraint fault.
- Sanitized segment rest length before stretch calculation.
- Hardened endpoint force, payload current, and angular damping direct `math.rsqrt` call sites against non-finite squared magnitudes and bad serialized caps.
- Hardened `TetherManager` profile scalar resolution so corrupt `TetherProfileSO` spring, overdamping, or snap threshold values fail to deterministic fallbacks.

Cinematic Cheats used:
- No new simulation truth was added. This pass protects the existing fake stack: MX350 3-segment authority plus taut-line visual and High/Ultra indirect impostor cable with procedural salt/silt/fiber detail.

Exact Microseconds saved:
- No measured microsecond saving is claimed.
- Added work is finite checks in fixed-size helpers and existing Burst loops. It is defensive stability work, not a performance claim.
- Existing estimates remain: Low/MX350 saves roughly 6-12 us versus full 10-segment authority; High/Ultra indirect repetition remains estimated CPU neutral to -5 us versus repeated primitive submission.

Validation:
- XML assignment re-read from `Docs/Tasks/CURRENT_BATCH.md`.
- Unity MCP workflow guidance re-read; no Unity MCP tools are exposed in this session.
- Targeted `math.rsqrt` / `math.rcp` scan completed and the remaining direct `rsqrt` call sites are now covered by finite guards or helper guards.
- Profile scalar scan confirmed `towCableProfile` values now pass through `SanitizeProfileScalar` before solver use.
- Static scan found no `Update` family, `string.Format`, Unity Joint, legacy EventBus/delegate, private `NativeQueue`, Unity time-global, singleton, `math.distance`, or `distance(` hits in touched tow/tether files.
- `git diff --check` reported only line-ending normalization warnings.
- Dotnet was not run for this loop by explicit user instruction not to rebuild every pass.
- Unity runtime and profiler validation were not executed.

## 2026-05-17 - Adjacent Cable/Tool NaN Debt Pass

What was wrong:
- `GravityTetherTool` trusted raw transform origin/forward, runtime range/radius, pickup distance, candidate centers, chest position, and pull scalar before overlap filtering and `PhysicsForceRouter`.
- `BioCableIK` could push non-finite anchor/recoil/rupture data into `_points`, `LineRenderer.SetPositions`, particle spark anchors, and `math.rsqrt` direction/constraint helpers.
- `VRCableDragPlug` used a cheap magnitude approximation that could overflow to infinity, then multiply `maxfloat * 0` during normalization or AUP cable-length clamping.

What was done:
- Added finite origin/direction/range/radius/pickup/candidate/chest guards to `GravityTetherTool`.
- Added safe dt, segment length, scalar, color, velocity, anchor, rupture, recoil, and renderer-position sanitation to `BioCableIK`.
- Hardened `VRCableDragPlug` control-point span, AUP clamp, `SafeNormalize`, and `ApproximateMagnitudeNoSqrt` against overflowed squared magnitudes and non-finite approximate lengths.

Cinematic Cheats used:
- Kept the cheap math path. No new physical cable truth was added outside the Verlet owner.
- MX350 keeps approximated cable spline/IK and cheap overlap filtering; High/Ultra can keep richer line/plug visuals with cleaner inputs instead of spending cycles recovering from NaNs.

Exact Microseconds saved:
- No measured microsecond saving is claimed.
- Added work is bounded finite checks in small fixed arrays or interaction handoff paths.
- Expected value is stability: avoids NaN propagation into physics force queues, line renderers, plug spline control points, and mobile/Quest GPU state.

Validation:
- Confirmed `GravityTetherTool`, `BioCableIK`, and `VRCableDragPlug` were not already dirty before adjacent edits.
- Static scan found no `Update` family, `string.Format`, Unity Joint, legacy EventBus/delegate, private `NativeQueue`, Unity time-global, singleton, `math.distance`, or `distance(` hits in the three touched adjacent files.
- Targeted `math.rsqrt` / `math.rcp` scan reviewed remaining direct normalization sites; remaining calls are guarded by finite length or approximate-length checks.
- `git diff --check` reported only line-ending normalization warnings.
- Dotnet was not run by explicit user instruction not to rebuild every pass.
- Unity runtime and profiler validation were not executed.

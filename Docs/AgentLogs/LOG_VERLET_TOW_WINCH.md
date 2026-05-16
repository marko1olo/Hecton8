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

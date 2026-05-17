# LOG - SIGNAL_AUTHORITY_VALIDATOR

## Surgical Record - 2026-05-16

What was wrong:
- Signal authority was split across `Hecton8.Core.Signals` references and feature-owned payload namespaces.
- Legacy `DamageSignal` still existed as a named lane surface alongside `CombatDamageSignal`.
- Feature systems configured `SignalBus<T>` lanes locally, allowing capacity/hash drift after allocation.
- Optional VFX lanes had no central stress governor.
- Lane overflow could silently degrade frame time without producer shutdown feedback.

What was done:
- Standardized all first-party signal references on `Hecton8.Core.Contracts.Signals`.
- Removed legacy `DamageSignal` payload, overloads, and generated hash constants.
- Rewired damage producers/readers to use `CombatDamageSignal`.
- Moved feature-owned `ISignal` payload structs into the contract namespace.
- Centralized all `SignalBus<T>.Configure` calls in `GlobalSignals.InitializeAllQueues()`.
- Added `SystemStress01` lane pressure input and VFX lane load shedding at stress >= 0.8.
- Preserved full optional VFX/audio propagation when stress < 0.2.
- Added overflow fault handling for lanes above 1024 queued packets: clear queue, publish LOVF degradation, set kill switch bit, emit `[LANE_OVERFLOW_FAULT]`.
- Added `KineticEnergy` to `HighSpeedImpactSignal` while preserving the legacy `LostKineticEnergy` alias.
- Verified `PlayerInteraction.cs` has no `UnityEvent`, `Action<T>`, or event field signal ghosts.
- Folded late-added compass, biolum, ambient AI, path funnel, and GPU scatter files into the contract signal namespace after final drift scan.
- Wrote `Docs/Tasks/Signal_Audit_Matrix.md`.

Cinematic cheats used:
- Optional VFX lanes are treated as expendable presentation traffic under high system stress.
- High-end machines keep full audio/VFX signal propagation while low-end machines shed non-critical VFX first.
- Overflow response disables non-critical producer families instead of simulating every packet.

Exact microseconds saved:
- Canonical damage lane: estimated 3-8 us in damage-heavy frames by removing duplicate damage payload conversion and legacy dequeue surface.
- Contract namespace/lane registry centralization: estimated 2-4 us during bootstrap-heavy scene loads by eliminating late local lane reconfiguration.
- Stress-based VFX shedding: estimated 20-80 us during overload spikes on i3/MX350 by dropping non-critical VFX snapshots before they drain into consumers.
- Overflow clear/kill switch: estimated 100-400 us during lane storms by replacing repeated drain/write work with queue clear and producer suppression.

Verification:
- `rg "\b(CombatEvent|HitSignal|DamageSignal)\b"`: 0 matches.
- Old namespace scan for `Hecton8.Core.Signals`: 0 matches.
- `ISignal` namespace audit: 0 structs outside `Hecton8.Core.Contracts.Signals`.
- Decentralized `SignalBus<T>.Configure` scan outside `GlobalSignals.cs`: 0 matches.
- Managed format-string scan over signal authority files: 0 interpolated strings, 0 `string.Format`.
- `dotnet build Hecton8.Core.csproj`: initial pass failed on unrelated AI/animation/VFX dependencies; final pass failed on unrelated `ProceduralLadderClimbRuntime` references in `GlobalRegistry.cs`, not signal-lane errors.
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal`: timed out after 300 seconds; dotnet workers stopped.

Blocked:
- Full sequential Pack=1 migration of 130 legacy explicit signal layouts is ABI-blocked by union fields and validated offsets. Newly evicted external signal payloads were converted to sequential Pack=1 fixed-size structs.
- Final integration build remains dependency-blocked by unrelated missing `ProceduralLadderClimbRuntime` references in `GlobalRegistry.cs` on the latest core build pass.

## Surgical Record - 2026-05-16 Late Inquisition Pass

What was wrong:
- Late parallel-agent drift reintroduced lane authority outside the central registry in lockstep/glitch, compass/anomaly, tether-fire, and thermal scalability signal call sites.
- Overflow storm cleanup still had a per-packet drain shape after fault detection.
- Several late payloads carried finite-sensitive vectors/AUP data without central Push-time NaN vaccination.
- The prior build-wall note was stale and no longer matched the latest compiler output.

What was done:
- Re-read status, rationale, original XML prompt, AGENTS.md, domain map, and task-relevant mandates from disk.
- Centralized `LockstepSnapshotSignal`, `SystemGlitchSignal`, `TetherFiredSignal`, compass, and anomaly lane policy in `GlobalSignals.InitializeAllQueues()`.
- Removed stale `Hecton8.Core.Signals` import drift and kept all live `ISignal` structs under `Hecton8.Core.Contracts.Signals`.
- Expanded non-critical VFX shedding to late VisualFlare and StreamingTurbulence lanes.
- Replaced overflow storm drain with `NativeQueue<T>.Clear()` while preserving LOVF degradation and kill-switch feedback.
- Added central finite guards for tether, visual flare, voxel carve, docking, anomaly, compass, and glitch payloads.
- Updated `Status_SIGNAL_AUTHORITY_VALIDATOR.md`, `Rationale_SIGNAL_AUTHORITY_VALIDATOR.md`, and `Signal_Audit_Matrix.md` with current evidence.

Cinematic cheats used:
- Low/Quest/MX350 cut non-critical flare/turbulence first under stress, preserving gameplay truth while faking presentation density.
- High/Ultra keep full visual/audio signal propagation until actual stress or overflow telemetry demands shedding.
- Invalid visual payloads degrade to zero/safe vectors rather than trying to render corrupted math.

Exact microseconds saved:
- Overflow storm clear: estimated 50-300 us saved on frames with >1024 queued packets by using `NativeQueue<T>.Clear()` instead of per-packet drain.
- Late VFX stress expansion: estimated 10-40 us saved in visual overload bursts by dropping flare/turbulence packet snapshots under high stress.
- Central Configure drift removal: estimated 2-6 us saved during bootstrap/scene-load reconfiguration by eliminating local capacity/hash mutation.
- Finite guard expansion: not a savings. Estimated <=1-5 us cost per normal burst; value is preventing mobile GPU NaN collapse and preserving blackbox attribution.

Verification:
- `rg -n "namespace Hecton8\.Core\.Signals|Hecton8\.Core\.Signals" Assets/_Project/Scripts -g "*.cs"`: 0 matches.
- `ISignal` namespace audit: `ISIGNAL_NAMESPACE_OK`.
- Decentralized `SignalBus<T>.Configure` audit: `CONFIGURE_CENTRALIZED`.
- `rg -n "\b(CombatEvent|HitSignal|DamageSignal)\b" Assets/_Project/Scripts -g "*.cs"`: 0 matches.
- `rg -n "UnityEvent|Action<|\bevent\b" Assets/_Project/Scripts/Interaction/PlayerInteraction.cs`: 0 matches.
- `rg --fixed-strings "string.Format"` over signal authority files: 0 matches.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal`: failed on unrelated `HectonXRRuntimeState.cs`, `SubmarineStructuralGrid.cs`, and `SubmarineFluidDynamics.cs` dependency damage. No SignalBus, signal namespace, tether, or lockstep errors remained.

Blocked:
- Full DataVault migration of `SignalBus<T>` storage is architecture-blocked until `GlobalDataVault` exposes a typed lane queue API. Current transport remains central, native, bounded, sentinel-registered, and explicitly disposed.
- Full sequential Pack=1 conversion of 130 legacy explicit signal layouts remains ABI-blocked by union overlays and validated offsets.
- Final integration build remains dependency-blocked outside CORE/SIGNALS.

## Surgical Record - 2026-05-16 Determinism Lane Closure

What was wrong:
- `PhysicsDeterminismSignals` still owned a generic local `ConfigureLane<T>` helper for five typed lanes.
- Deterministic input/fence/KCC payload structs still lived in `Hecton8.Physics` instead of the contract namespace.
- Local disposal of those lanes risked fighting `GlobalSignals` ownership during subsystem reset.

What was done:
- Moved `InputSignal`, `StateCorrectionSignal`, `DesyncDetectedSignal`, `SyncFenceSignal`, and `KccVelocitySignal` to `Hecton8.Core.Contracts.Signals`.
- Added their lane policies, exact hashes, and payload size validation to `GlobalSignals.InitializeAllQueues()`.
- Removed local Configure/dispose authority from `PhysicsDeterminismSignals`; it now initializes through `GlobalSignals` and only publishes/reads typed lanes.
- Added Push-time finite guards for deterministic input, state correction, sync fence, and KCC velocity payloads.

Cinematic cheats used:
- Low-tier deterministic input and velocity lanes stay bounded, while presentation systems can fake smoothness from clean snapshots instead of increasing authority cost.
- High/Ultra retain full replay/KCC telemetry and can spend clean signal flow on richer IK, visor feedback, and motion presentation.

Exact microseconds saved:
- Determinism lane centralization: estimated 2-5 us saved during deterministic bootstrap by removing local reconfiguration/disposal churn.
- Determinism finite guards: not a savings; estimated <=1-4 us per burst and prevents corrupted replay/fence vectors from reaching physics or presentation.

Verification:
- Decentralized `SignalBus<T>.Configure` audit: `CONFIGURE_CENTRALIZED`.
- Line-tracked `ISignal` namespace audit: `ISIGNAL_NAMESPACE_OK`.
- Old namespace scan for `Hecton8.Core.Signals`: 0 matches.
- Duplicate combat signal scan: 0 matches.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal`: succeeded with 0 warnings and 0 errors.
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal`: failed outside CORE/SIGNALS with `MSB4166` child-node shutdown and `MSB4242` SDK resolver failures in third-party project graph entries; dotnet worker stopped.

Blocked:
- Full `Assembly-CSharp` graph is outside this domain and currently blocked by MSBuild/third-party project graph instability, not by SignalBus lane code.

## Surgical Record - 2026-05-16 Tool Lane Drift Closure

What was wrong:
- `LaserCutterEventPayload` was introduced in the gameplay namespace while implementing `ISignal`.
- `LaserCutterEvents` configured its own `SignalBus<LaserCutterEventPayload>` lane outside `GlobalSignals`.
- The current build wall moved again due to concurrent out-of-domain edits, now in submarine fluid vault relocation.

What was done:
- Moved cutter event enum and payload into `Hecton8.Core.Contracts.Signals`.
- Added central `LaserCutterEventPayload` lane policy, 16-byte validation, and Push-time heat saturation in `GlobalSignals`.
- Removed local cutter lane Configure; the gameplay bridge now initializes through `GlobalSignals` and uses typed Push/snapshot reads.
- Re-applied lockstep centralization after concurrent drift returned local Configure calls.

Cinematic cheats used:
- Cutter heat/beam packets stay bounded on low tier while high tier can spend the clean event stream on richer beam shimmer, visor heat bloom, and salt-glow feedback.

Exact microseconds saved:
- Cutter lane centralization: estimated 1-3 us saved during cutter event bootstrap.
- Cutter heat guard: not a savings; below 1 us for normal event counts, prevents invalid heat from driving presentation.

Verification:
- Decentralized `SignalBus<T>.Configure` audit: `CONFIGURE_CENTRALIZED`.
- Line-tracked `ISignal` namespace audit: `ISIGNAL_NAMESPACE_OK`.
- Old namespace scan for `Hecton8.Core.Signals`: 0 matches.
- Duplicate combat signal scan: 0 matches.
- Managed event scan in `PlayerInteraction.cs`: 0 matches.
- Signal authority `string.Format` / `$"` / `Update()` scans on touched domain files: 0 matches.
- Latest `dotnet build Hecton8.Core.csproj --no-restore -v:minimal`: blocked by `SubmarineFluidDynamics.cs(1250)` missing `RefreshVaultNativeStateAfterRelocation`, outside CORE/SIGNALS. No signal lane errors.

Blocked:
- Latest core build is dependency-blocked by submarine fluid/vault relocation churn outside this domain.

## Surgical Record - 2026-05-16 Final Core Compile Recovery

What was wrong:
- The active core compile reached bridge DTOs and failed because `[BinaryBlittableSafe]` was used in `H8BridgeContracts.cs` without importing `Hecton8.Core.Memory.Layout`.
- Subsequent full project verification failed in RealtimeCSG because the generated third-party project references missing source files.

What was done:
- Added the missing bridge namespace import only; no DTO layout, signal payload field order, or lane runtime code changed.
- Re-ran centralization, namespace, duplicate, stale namespace, hot-path string/update scans, core build, full project build, and diff whitespace checks.
- Recorded the full-project RealtimeCSG wall as outside CORE/SIGNALS.

Cinematic cheats used:
- None in the compile repair. Existing low-tier stress shedding and high-tier full propagation remain the signal scalability strategy.

Exact microseconds saved:
- Bridge import repair: 0 us runtime; compile-only recovery.
- Preserved binary-safe markers avoid future ABI drift rather than saving frame time.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /nr:false /p:UseSharedCompilation=false`: PASS, 0 warnings, 0 errors.
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /nr:false /p:UseSharedCompilation=false`: FAIL outside domain in `RealtimeCSG.csproj` with 216 missing third-party source file errors and 131 third-party warnings.
- Decentralized `SignalBus<T>.Configure` audit: `CONFIGURE_CENTRALIZED`.
- Line-tracked `ISignal` namespace audit: `ISIGNAL_NAMESPACE_OK`.
- Duplicate combat signal and old namespace scan: 0 matches.
- Signal authority `string.Format` / `$"` / `Update()` scans on touched domain files: 0 matches.
- `git diff --check` on touched signal/doc files: whitespace-clean; only CRLF conversion warning reported for `H8BridgeContracts.cs`.

Blocked:
- Full Unity project graph remains blocked by RealtimeCSG third-party project/source inventory, not by CORE/SIGNALS.

## Surgical Record - 2026-05-16 Tether ABI Padding Recovery

What was wrong:
- Fresh strict layout scan found `TetherSnappedSignal` at 72 bytes and `TetherFiredSignal` at 40 bytes.
- Both were sequential Pack=1, but neither was a 16-byte multiple.
- `GlobalSignals` runtime size validators still encoded the old 72/40 byte sizes.

What was done:
- Padded `TetherSnappedSignal` to 80 bytes with a reserved field.
- Padded `TetherFiredSignal` to 48 bytes with a reserved field.
- Updated `ValidateSignalSize<TetherSnappedSignal>` and `ValidateSignalSize<TetherFiredSignal>` to 80/48.
- Re-ran layout, centralization, duplicate, stale namespace, managed event/delegate, managed string, and core build checks.

Cinematic cheats used:
- No visual math change in this pass. The stable tether event ABI preserves low-tier bounded truth and lets high-tier consumers spend the signal on snap sparks, cable recoil, visor warnings, and audio overkill without increasing physics cost.

Exact microseconds saved:
- Tether ABI padding: 0 us runtime saved. This prevents layout drift and stricter ARM64/mobile read hazards rather than reducing frame time.

Verification:
- `ISIGNAL_TOTAL=163`.
- `ISIGNAL_NO_SIZE_OR_NON16=0`.
- `CONFIGURE_OUTSIDE_GLOBALSIGNALS=0`.
- Duplicate combat signal, old namespace, `string.Format`, `$"`, `.ToString()`, `UnityEvent`, `Action<`, and `EventBus` scans over the signal authority files: 0 hits.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /nr:false /p:UseSharedCompilation=false`: PASS, 0 warnings, 0 errors.
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /nr:false /p:UseSharedCompilation=false`: FAIL outside domain in `RealtimeCSG.csproj` with 216 missing third-party source file errors and 131 third-party warnings.

Blocked:
- 105 legacy explicit signal layouts still omit `Pack=1`; they are ABI-union layouts and remain staged-migration work, not a safe mechanical edit.
- Full Unity project graph remains blocked by RealtimeCSG third-party source inventory outside CORE/SIGNALS.

## Surgical Record - 2026-05-16 Multiplatform Lane Drift Reclosure

What was wrong:
- `SignalLaneTelemetry` crossed the Architect Eye/DataVault telemetry boundary without explicit Pack=1/Size.
- Local `SignalBus<T>.Configure` calls had returned in lockstep/glitch, laser cutter, and compass/anomaly code.
- The current core build is no longer green because unrelated Fauna/Construction edits introduced 17 compile errors.

What was done:
- Hardened `SignalLaneTelemetry` to `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]` with explicit reserved fields.
- Removed local Configure authority from `LockstepStateValidator`, `LaserCutterEvents`, and `DiegeticGyroCompassRuntime`; these surfaces now use central `GlobalSignals.InitializeAllQueues()` and typed `EnsureInitialized()`/Push/snapshot calls.
- Re-ran lane centralization, namespace, payload layout, managed event, and managed formatting scans.

Cinematic cheats used:
- No new simulation was added. The clean typed-lane surface preserves cheap low-tier truth and leaves high-tier presentation free to spend cycles on compass glass, cutter heat shimmer, glitch feedback, visor salt, silt wake, and hull-response consumers.

Exact microseconds saved:
- `SignalLaneTelemetry` ABI hardening: 0 us runtime; prevents platform layout drift.
- Lane drift reclosure: estimated 2-6 us saved during cold bootstrap/reinitialization by removing feature-owned policy mutation.

Verification:
- `CONFIGURE_OUTSIDE_GLOBALSIGNALS=0`.
- `OLD_SIGNAL_NAMESPACE_HITS=0`.
- `ISIGNAL_TOTAL=163`.
- `ISIGNAL_NO_SIZE_OR_NON16=0`.
- `PLAYER_INTERACTION_EVENT_HITS=0`.
- Targeted managed format scan over touched runtime files: 0 hits.
- `SignalLaneTelemetry`: Pack=1 Size=32 with explicit reserved fields.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /nr:false /p:UseSharedCompilation=false`: FAIL outside CORE/SIGNALS with 17 errors in `PredatorCognitionDomain.cs` and `DroneFleetManager.cs`; no signal lane errors were emitted.

Blocked:
- Current core build is dependency-blocked by Fauna native-container drift and Construction double3/float3 conversion drift outside CORE/SIGNALS.

## Surgical Record - 2026-05-16 Splash And Physics Event Lane Closure

What was wrong:
- `SplashEvent`, `PhysicsEventPayload`, and `DeferredSubmarineImpactSignal` existed as feature-owned `ISignal` payloads.
- `FluidFeedbackEvents`, `PhysicsEventBus`, and `PhysicsApplySystem` owned local `SignalBus<T>.Configure` calls.
- Concurrent drift repeatedly reintroduced lockstep and compass local Configure authority.

What was done:
- Moved the active splash, physics event, and deferred submarine impact payload contracts into `Hecton8.Core.Contracts.Signals`.
- Added central lane policies, size validators, and Push-time finite guards for the three lanes in `GlobalSignals`.
- Reduced fluid/physics/compass/lockstep bridges to central `GlobalSignals.InitializeAllQueues()` plus typed `EnsureInitialized()`/Push/snapshot calls.
- Added the required contract import for `RandomEventSystem` splash publishing.

Cinematic cheats used:
- Low tier can bound splash and physics feedback lanes while the presentation layer fakes richness from stable packets.
- High/Ultra can spend the clean event stream on denser splash, pressure, acoustic, trauma, visor, silt, and hull feedback without increasing simulation truth cost.

Exact microseconds saved:
- Splash/physics/deferred impact centralization: estimated 2-6 us saved during cold bootstrap/reinitialization.
- ABI/finite guard additions: 0 us saved; expected guard overhead below 1-4 us per normal burst, buying mobile NaN containment.

Verification:
- `CONFIGURE_OUTSIDE_GLOBALSIGNALS=0`.
- `ISIGNAL_NAMESPACE_VIOLATIONS=0`.
- `OLD_SIGNAL_NAMESPACE_HITS=0`.
- Targeted managed format scan over touched runtime files: 0 hits.
- `git diff --check` on touched signal/runtime/doc files: whitespace-clean; CRLF conversion warnings only.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /nr:false /p:UseSharedCompilation=false`: FAIL outside CORE/SIGNALS with 41 errors in `EcosystemDirector.cs`, `DiegeticGyroCompassRuntime.cs`, and `InputDispatcher.cs`; no SignalBus registry or signal contract namespace errors were emitted.

Blocked:
- Current core build is dependency-blocked by World/UI/Input compile drift outside CORE/SIGNALS.

## Surgical Record - 2026-05-16 ARM64 ABI Polish Pass

What was wrong:
- Signal authority scans were clean, but adjacent physics event DTOs used Pack=1 without explicit final `Size`.
- `FloodMassPropertiesResult` used an explicit 44-byte stride, which is not a 16-byte multiple.
- The AUP snapshot transformer carried one `float3` without an explicit 16-byte stride.

What was done:
- Set `PressureImpulseEvent` to Size=80, `ElectromagneticPulseEvent` to Size=32, `AcousticPingEvent` to Size=48, `AcousticImpulseEvent` to Size=48, and `LargeAcousticImpulseEvent` to Size=48.
- Set `CombatDamageSignalAupShiftTransformer` to Size=16.
- Padded `FloodMassPropertiesResult` to Size=48 with an explicit reserved field.

Cinematic cheats used:
- No simulation cost was added. Stable compact packets keep low-tier dispatch cheap and let high-tier consumers spend the same event stream on pressure bloom, acoustic feedback, silt wake, hull deformation, and visor detail.

Exact microseconds saved:
- ABI polish: 0 us runtime saved. This is mobile/Burst/IL2CPP stability work, not a frame-time shortcut.

Verification:
- `CONFIGURE_OUTSIDE_GLOBALSIGNALS=0`.
- `ISIGNAL_NAMESPACE_VIOLATIONS=0`.
- `TARGET_MANAGED_FORMAT_HITS=0`.
- `git diff --check` on touched files: whitespace-clean; CRLF conversion warnings only.

Blocked:
- Current core build remains dependency-blocked outside CORE/SIGNALS by World/UI/Input compile drift.

## Surgical Record - 2026-05-16 Concurrent Configure Drift Reclosure

What was wrong:
- While the build was running, local `SignalBus<T>.Configure` authority reappeared in compass/anomaly and lockstep/glitch code.
- The current core build wall moved again; it now fails in `World/SargassumMicroFaunaBoids.cs` on missing `SaturateFinite01`.

What was done:
- Removed the reintroduced compass/anomaly Configure calls and restored central `GlobalSignals.InitializeAllQueues()` ownership.
- Removed the reintroduced lockstep/glitch Configure calls and deleted their stale local capacity/hash constants.
- Re-ran centralization scan after the repair.

Cinematic cheats used:
- No new simulation cost was added. Centralized lanes keep low-tier packet flow bounded and leave high-tier consumers free to spend stable events on richer compass, glitch, tether, acoustic, and hull feedback.

Exact microseconds saved:
- Reclosing local Configure authority: estimated 2-6 us saved during cold bootstrap/reinitialization. Runtime hot path is unchanged.

Verification:
- `CONFIGURE_OUTSIDE_GLOBALSIGNALS=0`.
- Current `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /nr:false /p:UseSharedCompilation=false`: FAIL outside CORE/SIGNALS with 9 errors in `World/SargassumMicroFaunaBoids.cs`; no SignalBus registry or payload namespace errors were emitted.

Blocked:
- World/Sargassum owner must restore or replace `SaturateFinite01`; this is outside the signal authority domain.

## Surgical Record - 2026-05-16 Architect Eye Debug Lane Closure

What was wrong:
- `DebugSignal : ISignal` was declared in `Hecton8.Core.Diagnostics.Visuals` instead of the contract namespace.
- The debug visual lane had no central `GlobalSignals.InitializeAllQueues()` policy.

What was done:
- Moved `DebugSignal` and `DebugSignalKind` to `Hecton8.Core.Contracts.Signals`.
- Registered the `DebugSignal` lane centrally with a 64-packet cap and low-tier cap of 8.
- Marked the lane non-critical for stress shedding and added Push-time finite guards for position, vector, and scalar values.
- Routed `ArchitectEyeDebugBus.EnsureInitialized()` through central signal initialization.

Cinematic cheats used:
- Low tier can drop debug visual packet flood under stress. High/Ultra can keep dense Architect Eye overlays without mutating gameplay lanes.

Exact microseconds saved:
- Debug lane centralization: estimated 1-3 us saved during diagnostic bootstrap. Runtime hot path is unchanged except stress-based packet shedding.

Verification:
- `ISIGNAL_NAMESPACE_VIOLATIONS=0`.
- `CONFIGURE_OUTSIDE_GLOBALSIGNALS=0`.
- `TARGET_MANAGED_FORMAT_HITS=0`.
- Current `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /nr:false /p:UseSharedCompilation=false`: FAIL outside signal authority with 46 errors in UI compass DTO and SystemDispatcher blackbox/raycast fields; no SignalBus registry or signal payload namespace errors were emitted.

Blocked:
- UI compass and SystemDispatcher owners must repair their presentation DTO and dispatcher blackbox/raycast state drift.

## Surgical Record - 2026-05-16 Final Core Build Recovery

What was wrong:
- A final build attempt initially failed because the generated `Temp/obj/Hecton8.Core/Hecton8.Core.csproj.nuget.g.targets` file had been removed.
- Concurrent local Configure drift was reintroduced several times during validation.

What was done:
- Re-closed compass/anomaly and lockstep/glitch Configure drift again, leaving central `GlobalSignals.InitializeAllQueues()` as the only lane policy owner.
- Ran `dotnet restore Hecton8.Core.csproj /nr:false` to regenerate the missing target.
- Ran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /nr:false /p:UseSharedCompilation=false`.

Cinematic cheats used:
- No new physical simulation was added. Low tier keeps bounded lanes and stress shedding; High/Ultra can spend the clean signal surface on dense debug overlays, splash/acoustic feedback, visor detail, and hull response.

Exact microseconds saved:
- Final drift closure: estimated 2-6 us saved during cold bootstrap/reinitialization.
- Restore/build recovery: 0 us runtime; verification-only.

Verification:
- `dotnet restore Hecton8.Core.csproj /nr:false`: PASS.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /nr:false /p:UseSharedCompilation=false`: PASS, 0 warnings, 0 errors.
- `CONFIGURE_OUTSIDE_GLOBALSIGNALS_AFTER_WAIT=0`.

Blocked:
- Full `Assembly-CSharp.csproj` Unity graph was not re-run after the core pass.

## Surgical Record - 2026-05-17 Recurrent Drift Closure And Core Build Green

What was wrong:
- Local `SignalBus<T>.Configure` calls had returned in `LockstepStateValidator`, `DiegeticGyroCompassRuntime`, and `ArchitectEyeDebugSignal`.
- This reintroduced feature-owned lane capacity/hash authority after prior closure.
- The first build attempt in this pass failed outside CORE/SIGNALS on `SubmarineFluidDynamics.ResolveExteriorThermalAnomalyCenter`, so a current post-reclosure build was required before claiming green.

What was done:
- Re-read the live XML assignment and relevant mandates from disk.
- Removed lockstep/glitch, compass/anomaly, and Architect Eye debug local Configure calls.
- Removed stale local lane capacity/hash constants from the touched surfaces.
- Left central lane capacities in `GlobalSignals.InitializeAllQueues()`.
- Re-ran static scans and core build verification.

Cinematic cheats used:
- No new simulation was added. Low tier keeps bounded central lanes and stress shedding; High/Ultra can spend the same stable packets on richer compass glass, glitch overlays, dense Architect Eye lines, visor salt, wake/silt feedback, and hull response.

Exact microseconds saved:
- Reclosing repeated local Configure authority: estimated 2-6 us during cold bootstrap/reinitialization.
- Managed format purge: 0 B hot-path allocation retained; no new measured frame-time delta.
- Core build recovery: 0 us runtime; verification-only.

Verification:
- `CONFIGURE_OUTSIDE_GLOBALSIGNALS=0`.
- `TARGET_STRING_FORMAT_OR_FIXEDSTRING_HITS=0`.
- `ISIGNAL_CONTRACT_LAYOUT_VIOLATIONS=0`.
- Duplicate/old namespace scan returned 0 matches.
- `PLAYER_INTERACTION_EVENT_HITS=0`.
- `git diff --check` returned no whitespace errors; CRLF warnings only.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /nr:false /p:UseSharedCompilation=false`: PASS, 0 warnings, 0 errors.

Blocked:
- Unity PlayMode, player build, profiler, and platform runtime validation were not executed in this pass. Those remain unclaimed.

## Surgical Record Addendum - 2026-05-17 Final Post-Churn Build Wall

What was wrong:
- After the reclosure and one green core build, concurrent churn changed the build wall again.
- The final `Hecton8.Core.csproj` build now fails outside CORE/SIGNALS at `SubmarineFluidDynamics.cs(729,43)` with duplicate `_exteriorBuoyancySampleLocalPoints`.

What was done:
- Reclosed lockstep/glitch and compass/anomaly local Configure drift again after it reappeared.
- Re-ran the centralization scan; `CONFIGURE_OUTSIDE_GLOBALSIGNALS=0`.
- Corrected status and audit files to record the current final build as dependency-blocked, not green.

Cinematic cheats used:
- No new simulation cost was added. Low-tier signal shedding and high-tier visual packet propagation remain unchanged.

Exact microseconds saved:
- Same as the preceding reclosure: estimated 2-6 us during cold bootstrap/reinitialization by removing repeated local lane policy mutation.
- Compile wall recording: 0 us runtime.

Verification:
- `CONFIGURE_OUTSIDE_GLOBALSIGNALS=0` after the last patch.
- Earlier bounded scans in this pass: `TARGET_STRING_FORMAT_OR_FIXEDSTRING_HITS=0`, `ISIGNAL_CONTRACT_LAYOUT_VIOLATIONS=0`, duplicate/old namespace scan 0 matches, `PLAYER_INTERACTION_EVENT_HITS=0`.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /nr:false /p:UseSharedCompilation=false`: CURRENT BLOCKED outside CORE/SIGNALS by `SubmarineFluidDynamics` duplicate field.

Blocked:
- Submarine fluid dynamics owner must remove or merge the duplicate `_exteriorBuoyancySampleLocalPoints` field before current core build green can be re-certified.

## Surgical Record - 2026-05-17 Acoustic Zone Signal Closure

What was wrong:
- `AcousticZoneChangedEvent` lived in `Hecton8.Audio`, not `Hecton8.Core.Contracts.Signals`.
- The payload was 1 byte wide, not a 16-byte-multiple ABI contract.
- `AcousticZoneEvents` configured its own typed lane outside `GlobalSignals`.

What was done:
- Moved the acoustic-zone payload into the contract namespace.
- Padded the payload to Pack=1 Size=16 with explicit reserved fields.
- Added central validation and central lane registration in `GlobalSignals.InitializeAllQueues()`.
- Removed the local acoustic-zone lane Configure/hash authority.

Cinematic cheats used:
- Low tier keeps a tiny bounded acoustic transition lane; high tier can spend the stable packet on richer wet/dry mix transitions and acoustic presentation.

Exact microseconds saved:
- Estimated 1-2 us during cold bootstrap/reinitialization by removing local lane policy mutation.
- Runtime hot path remains one typed Push; no measured frame-time delta.

Verification:
- Acoustic-zone contract is registered centrally in `GlobalSignals`.
- Latest acoustic-zone follow-up build returned `-1` with an empty log and build workers were stopped; no green build is claimed after this closure.

Blocked:
- Current compile certification remains blocked by unstable external build churn and prior out-of-domain `SubmarineFluidDynamics` duplicate field failure.

## Surgical Record - 2026-05-16 Re-Inquisition SPSC And Drift Closure

What was wrong:
- Fresh scans found four reintroduced local lane Configure calls in `DiegeticGyroCompassRuntime.cs` and `LockstepStateValidator.cs`.
- `SpscSignalRingBuffer<T>` still allocated a backing `NativeArray<T>` directly instead of routing through the memory sentinel.
- The stale PlayerInteraction scan path was wrong; the live file is `Assets/_Project/Scripts/Interaction/PlayerInteraction.cs`.

What was done:
- Removed compass/anomaly and lockstep/glitch local Configure calls and stale capacity/hash constants.
- Reduced those call sites to `GlobalSignals.InitializeAllQueues()` plus typed `EnsureInitialized()`.
- Replaced direct SPSC fallback `NativeArray<T>` allocation with `H8Memory.Allocate/Release` and an explicit `SystemID` owner path.
- Re-ran signal centralization, namespace, layout, managed format, PlayerInteraction, and native allocation scans.

Cinematic cheats used:
- No new simulation cost was added. Low tier keeps bounded central lanes and stress shedding; High/Ultra can spend stable packets on richer compass glass, glitch overlays, replay diagnostics, visor salt, wake/silt, and hull feedback in presentation consumers.

Exact microseconds saved:
- Reclosing local Configure authority: estimated 2-6 us saved during cold bootstrap/reinitialization.
- SPSC memory-sentinel routing: 0 us while unused; improves leak attribution if activated.

Verification:
- `CONFIGURE_OUTSIDE_GLOBALSIGNALS=0`.
- `DUPLICATE_SIGNAL_NAME_HITS=0`.
- `OLD_SIGNAL_NAMESPACE_HITS=0`.
- `ISIGNAL_NAMESPACE_VIOLATIONS=0`.
- `ISIGNAL_LAYOUT_VIOLATIONS=0`.
- `TARGET_STRING_FORMAT_OR_FIXEDSTRING_HITS=0`.
- `PLAYER_INTERACTION_EVENT_HITS=0`.
- Direct native allocation scan in `GlobalSignals.cs` now shows only central `SignalBus<T>` `NativeQueue`/`NativeList` transport allocations; direct SPSC `NativeArray<T>` allocation is gone.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /nr:false /p:UseSharedCompilation=false`: BLOCKED. Completed attempts moved through unrelated World/Ecosystem and PlayerKinematics compile errors; a later attempt timed out after 304 seconds and dotnet/VBCSCompiler workers were stopped. No current green build exists after the final signal drift reclosure.

Blocked:
- Owning World/Gameplay/Integrator agents must repair their compile walls before current core build green can be re-certified. Final static signal scans are clean.

## Surgical Record - 2026-05-16 Warning Truth Recheck

What was wrong:
- An intermediate core build emitted 2 `CS2002` warnings while Unity-generated project files were changing under concurrent agent work.
- That intermediate state was not stable enough to claim as final.

What was done:
- Re-read the XML assignment and current status/rationale before reporting.
- Re-ran signal authority scans and layout scans.
- Re-ran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /nr:false /p:UseSharedCompilation=false`.
- Forced `dotnet build Hecton8.Core.csproj -t:Rebuild --no-restore -v:minimal /nr:false /p:UseSharedCompilation=false`.
- Updated status, rationale, audit matrix, and this log to match the forced rebuild evidence.

Cinematic cheats used:
- No physical simulation was added. Signal lanes remain bounded on low tier and fully propagated for high-tier visual/audio overkill when stress allows.

Exact microseconds saved:
- Warning recheck: 0 us runtime. This is verification truth maintenance.
- Central lane state remains at the prior estimated 2-6 us bootstrap/reinitialization savings by eliminating decentralized Configure drift.

Verification:
- `CONFIGURE_OUTSIDE_GLOBALSIGNALS=0`.
- `OLD_SIGNAL_NAMESPACE_HITS=0`.
- `PLAYER_INTERACTION_EVENT_HITS=0`.
- `ISIGNAL_NAMESPACE_VIOLATIONS=0`.
- `ISIGNAL_LAYOUT_VIOLATIONS=0`.
- `STRING_FORMAT_OR_FIXEDSTRING_HITS=0` across targeted signal/runtime files.
- `dotnet build Hecton8.Core.csproj -t:Rebuild --no-restore -v:minimal /nr:false /p:UseSharedCompilation=false`: PASS, 0 warnings, 0 errors.

Blocked:
- Full `Assembly-CSharp.csproj` Unity graph was not re-run after the core pass.

## Surgical Record - 2026-05-17 Used SignalBus Lane Closure

What was wrong:
- Alias-aware lane audit found default-policy drift for `DataVaultUpdateSignal`, `PrefabAcousticSignatureSignal`, `PrefabLoreLinkSignal`, and `ScalabilityChangedEvent`.
- `ScalabilityChangedEvent` lived in `Hecton8.Core` at Size=2, outside the signal contract namespace and below the 16-byte mobile ABI stride.
- `DirectorAIMusicSignal` had typed Push/snapshot consumers but no central registry policy.
- Concurrent churn reintroduced compass/anomaly and lockstep/glitch helper-level `SignalBus<T>.Configure` calls.

What was done:
- Added central `GlobalSignals` policies and validators for data-vault updates, prefab acoustic/lore links, scalability changes, and DirectorAI music cues.
- Moved `ScalabilityChangedEvent` to `Hecton8.Core.Contracts.Signals`, padded it to Pack=1 Size=16, and routed `ScalabilityEvents` through central initialization.
- Stripped compass and lockstep helpers down to `GlobalSignals.InitializeAllQueues()` plus typed `EnsureInitialized()`.
- Re-ran centralization, used-lane, layout, duplicate-name, managed-format, managed-event/delegate, target `Update()`, and whitespace scans.

Cinematic cheats used:
- Low tier keeps bridge/scalability/music lanes bounded and cheap; high tier can use the same clean packets for richer acoustic lore presentation, music pressure, compass glass, glitch overlays, visor salt, wake/silt, and hull feedback.

Exact microseconds saved:
- Estimated 2-8 us during cold bootstrap/reinitialization by removing default `SignalBus<T>` policy fallback and recurrent local Configure mutation.
- Struct padding and namespace eviction save 0 us runtime; they prevent mobile/Burst ABI drift.

Verification:
- `CONFIGURE_OUTSIDE_GLOBALSIGNALS=0`.
- `SIGNALBUS_USED_TYPES_WITHOUT_GLOBAL_CONFIG=0`.
- `ISIGNAL_CONTRACT_LAYOUT_VIOLATIONS=0`.
- Duplicate/old namespace scan returned 0 matches.
- `TARGET_STRING_FORMAT_OR_FIXEDSTRING_HITS=0`.
- Target managed event/delegate scan returned 0 matches.
- Target `Update()` scan returned 0 matches.
- `git diff --check` reported only LF-to-CRLF working-copy warnings, no whitespace errors.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /nr:false /p:UseSharedCompilation=false /m:1`: PASS, 0 warnings, 0 errors, 46.07 seconds.

Blocked:
- Unity PlayMode, player builds, Quest/Android, Metal/Mac, Steam Deck, and full `Assembly-CSharp.csproj` runtime validation were not run in this signal pass.

## Surgical Record - 2026-05-17 Physical Contract And Residual Lane Policy Closure

What was wrong:
- Signal contracts were namespace-correct but still physically scattered through feature files, creating duplicate authority outside `Assets/_Project/Scripts/Core/GlobalSignals.cs`.
- Reintroduced compass/anomaly and lockstep/glitch helpers configured lanes locally again.
- Alias-aware `SignalBus<T>` usage scan found default-policy drift for `BrownoutSignal`, `DebrisSpawnSignal`, `HUDNotificationSignal`, `ToolAcousticSignal`, `SeismicSignal`, `SubmarineLightsChangedSignal`, `PhysiologyStateSignal`, and `PlayerStressSignal`.

What was done:
- Centralized the remaining external payload declarations in `GlobalSignals.cs` and removed duplicate definitions from physics determinism, tether, docking, voxel, movement/prologue, diagnostics, homeostasis, bridge, acoustic, scalability, and DirectorAI surfaces.
- Replaced local compass and lockstep Configure calls with `GlobalSignals.InitializeAllQueues()` plus typed `EnsureInitialized()`.
- Added explicit central capacities, stable hashes, and low-tier frame limits for brownout, debris, HUD notification, tool acoustic, seismic, submarine lights, physiology state, and player stress lanes.
- Removed stale unused signal-contract usings from the docking/tether shells touched by the eviction.

Cinematic cheats used:
- Low tier clamps debris, seismic, tool acoustic, light, stress, and HUD traffic through central caps instead of per-feature policy drift.
- High and Ultra keep full bounded propagation so the same packets can drive dense debris, camera shake, hull-light response, material decay, visor salt, wake/silt, and acoustic overkill.

Exact microseconds saved:
- Estimated 3-10 us during cold bootstrap/reinitialization by eliminating residual default-policy fallback and repeated helper-level Configure mutation.
- Payload relocation and ABI centralization save 0 us directly; they reduce IL2CPP/Burst/mobile integration risk and prevent duplicate contract churn.

Verification:
- `ISIGNAL_STRUCT_FILES_OUTSIDE_GLOBALSIGNALS=0`.
- `CONFIGURE_OUTSIDE_GLOBALSIGNALS=0`.
- `SIGNALBUS_USED_TYPES_WITHOUT_GLOBAL_CONFIG=0`.
- `ISIGNAL_CONTRACT_LAYOUT_VIOLATIONS=0`.
- Duplicate/old namespace scan returned 0 matches.
- `TARGET_STRING_FORMAT_OR_FIXEDSTRING_HITS=0`.
- `Interaction/PlayerInteraction.cs` managed event scan returned 0 matches.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /nr:false /p:UseSharedCompilation=false /m:1`: PASS, 0 warnings, 0 errors, 25.62 seconds.

Blocked:
- Unity PlayMode, Quest/Android, Metal/Mac, Steam Deck, and full player-build runtime validation were not run in this signal pass.

## Surgical Record - 2026-05-17 Warning Sweep And Late Signal Drift Closure

What was wrong:
- `AudioEvent` and `CameraJuiceImpactSignal` were still feature-file `ISignal` payloads.
- Procedural audio, camera juice, ambient biota, lockstep, gyro-compass, and scalability helpers had recurrent local `SignalBus<T>.Configure` authority.
- `Hecton8.Core.csproj` emitted `CS2002` because `HectonSignalLaneContract.cs` was included twice through generated project items plus `Directory.Build.targets`.

What was done:
- Moved the procedural audio and camera impact payloads into `Core/GlobalSignals.cs`.
- Added central lane policy/validation for procedural audio and camera impact traffic.
- Reduced feature helpers to `GlobalSignals.InitializeAllQueues()` plus typed `EnsureInitialized()`/Push/snapshot APIs.
- Added `Compile Remove` before the `Directory.Build.targets` contract re-add so `HectonSignalLaneContract.cs` compiles once.
- Updated the audio smoke assertion to verify the central signal contract source.

Cinematic cheats used:
- Low tier keeps audio, camera impact, biome, spawn, debris, compass, glitch, and scalability lanes centrally capped.
- High and Ultra can spend the same packets on procedural audio intensity, camera impact response, dense ambient debris, compass glass, glitch overlays, visor salt, silt wake, and hull lighting without new broadcast authority.

Exact microseconds saved:
- Estimated 4-12 us during cold bootstrap/reinitialization by removing repeated helper-level lane mutation.
- Project include cleanup saves 0 us runtime; it removes a real compiler warning instead of hiding it.

Verification:
- `ISIGNAL_STRUCT_FILES_OUTSIDE_GLOBALSIGNALS=0`.
- `CONFIGURE_OUTSIDE_GLOBALSIGNALS=0`.
- `SIGNALBUS_USED_TYPES_WITHOUT_GLOBAL_CONFIG=0`.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /nr:false /p:UseSharedCompilation=false /m:1`: PASS, 0 warnings, 0 errors, 2:36.30.
- No `dotnet rebuild` was run.

Blocked:
- Full `Assembly-CSharp.csproj`, Unity PlayMode, Quest/Android, Metal/Mac, Steam Deck, and player-build runtime validation were not run in this warning sweep.

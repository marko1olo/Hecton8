# LOG_17-D

## 2026-06-03 Input/Haptic Arbitration Pass

What was wrong:
- `InputDispatcher.ApplyHapticRequestContribution` treated `HapticRequest.Channel` as priority. `ChannelMicroVibration = 6` could outrank collision and critical hull feedback.
- Queued `HapticCommandDTO` replacement used magnitude only. A loud tool/laser pulse could evict a lower-amplitude but higher-importance hull/collision pulse.
- `HapticCommandDTO` is a 16-byte deterministic ABI mirrored in two contract files, so adding priority fields would break layout.

What was done:
- Added semantic haptic priorities in `Assets/_Project/Scripts/Core/InputDispatcher.cs`: Micro=0, Tool=1, Collision=2, Critical=3.
- Packed haptic priority and blend mode into high bits of existing `MotorMask`; low 8 motor-mask bits remain the physical motor lane.
- Changed queued haptic eviction to drop lower priority first, then weaker magnitude only inside the same priority.
- Routed collision, vehicle-critical, crush, and light-thud channels through max blend so impact pulses are not diluted by additive tool noise.
- Routed synthesized haptic pulses in `Assets/_Project/Scripts/Core/HectonInputRuntime_HapticSynth.cs` through the same priority/blend mapping.
- Added `Assets/_Project/Tests/Editor/InputHapticPriority17DEditTests.cs` to lock the priority contract and the 16-byte DTO layout.

Cinematic cheats used:
- Semantic rumble arbitration instead of physical haptic simulation. Critical hull impact is preserved through priority, not through expensive collision waveform synthesis.
- Max blend for impact channels, additive blend for tool/micro channels. Cheap deterministic sensory layering, no managed queue.

Verification:
- Static scan confirmed removed bad pattern: `byte priority = request.Channel`.
- Static scan confirmed removed bad blend probe: `request.Flags & HapticBlendAdditive`.
- Static scan found no `Input.Get*`, `Gamepad.current`, or hot-path `.performed +=` pattern in `InputDispatcher.cs` or `PhysicalHandController.cs`.
- CPU guard blocked compiler/test execution. Samples: 100 percent, then 93 percent. No `dotnet`, `csc`, MSBuild, or Unity process was running, but total CPU exceeded the explicit 50 percent threshold.

Scope gates:
- `PhysicalHandController.cs` already had a large pre-existing dirty diff before this pass. It was audited but not edited to avoid overwriting concurrent work.
- First-party prefab scan exposed HUD/player prefabs, not a concrete helmet/menu prefab target safe for direct navigation edits. Existing UI source routes already expose submit/cancel/navigation through InputManager and pause/PDA panels.

Exact microseconds saved:
- Measured saved time: 0 us. No profiler or compiler run was allowed under CPU guard.
- Runtime overhead estimate for priority packing/comparison: +1-2 us worst-case haptic frame on i3/MX350.
- Avoided rejected managed-priority-queue cost estimate: 5-15 us and heap risk per busy haptic frame.

## 2026-06-03 Input Profile And Mouse-Free UI Guard Pass

What was wrong:
- Root `input_profiles.csv` was absent even though `InputDispatcher` already watches it in Editor and applies it through `InputProfileDTO`.
- No 17-D guard locked the no-mouse UI traversal contract across the action asset and runtime `InputManager` backfill.
- Compile/test execution remained illegal under the local CPU guard: CPU was 100 percent and both `dotnet` and `Unity` were active.

What was done:
- Added root `input_profiles.csv` with deterministic tuning:
  - `inner_deadzone,0.14`
  - `outer_deadzone,0.96`
  - `move_exponent,1.55`
  - `mouse_acceleration,0.06`
  - `haptic_thermal_amplitude_scale,0.62`
  - `haptic_dispatch_interval_seconds,0.0333333`
  - `mock_collision,0`
- Expanded `Assets/_Project/Tests/Editor/InputHapticPriority17DEditTests.cs` with:
  - root profile existence/value guard
  - keyboard/gamepad UI Navigate/Submit/Cancel route guard
  - `InputManager` runtime backfill guard for dpad, left stick, buttonSouth, buttonEast/start, and shoulder tab movement.

Cinematic cheats used:
- Tuning through a cold profile file instead of per-device runtime branches.
- Haptic thermal scale and 30 Hz dispatch cadence buy cleaner critical feedback without simulating hardware-specific rumble waveforms.

Verification:
- Static scan found no `Input.Get*`, `Gamepad.current`, `.performed +=`, `byte priority = request.Channel`, or `request.Flags & HapticBlendAdditive` in the target input/hand files.
- `git diff --check` passed for the touched source/test/profile/log files.
- Build/EditMode test run was not launched because CPU guard failed and existing `dotnet`/`Unity` processes were active.

Exact microseconds saved:
- Measured saved time: 0 us. No profiler run allowed.
- Runtime steady-state cost of `input_profiles.csv`: 0 us disk I/O; existing profile read is already a Vault DTO read.
- Estimated low-end benefit: fewer stick drift false positives and lower haptic spam under thermal throttle, with no new managed allocation.

## 2026-06-03 Steam Deck Gyro And Trackpad Filter Pass

What was wrong:
- `SteamDeckInputPal` used fixed `GyroEwmaAlpha = 0.22f`, making gyro smoothing frame-rate dependent and slower than the one-frame response target.
- Steam Deck trackpad proxy accepted raw bound gamepad stick vectors above the deadzone, so small drift outside the threshold could leak as non-zero proxy input.
- Compile/test execution remained blocked: CPU was 100 percent with active `dotnet` and `Unity` processes.

What was done:
- Replaced fixed gyro EWMA with deltaTime-derived low-pass alpha: `1 - exp(-2*pi*cutoff*dt)`.
- Set active gyro cutoff to 12 Hz and idle decay cutoff to 18 Hz.
- Added radial deadzone remap for Steam Deck left/right trackpad proxy before writing `SteamDeckLeftTrackpad` and `SteamDeckRightTrackpad`.
- Expanded `InputHapticPriority17DEditTests.cs` to guard the Steam Deck PAL route.

Cinematic cheats used:
- First-order low-pass filter instead of Kalman/sensor-fusion simulation. Predictable, bounded, and cheap.
- Radial remap instead of per-device calibration tables. Drift is suppressed without inventing a device-specific truth path.

Verification:
- Static scan found no `Input.Get*`, `Gamepad.current`, `.performed +=`, managed collections, or LINQ in `SteamDeckInputPal.cs`.
- `git diff --check` passed for the touched PAL/test/profile/log files.
- No compile/test run was launched because the CPU/build guard failed.

Exact microseconds saved:
- Measured saved time: 0 us. No profiler run allowed.
- Runtime estimate: +0.5-1.0 us on active Steam Deck gyro frames for `math.exp`; 0 B GC.
- Rejected Kalman/sensor-fusion estimate avoided: 10-30 us plus state complexity without current proof.
## 2026-06-03 17-D Follow-Up

Wrong: Gamepad UI stick bindings had no deadzone processors in action assets; `InputManager` fallback repair could rebuild UI/player gamepad bindings without stick filtering; `HectonRuntimeInputActions` mapped Player PDA and Pause to `<Gamepad>/start`.
Done: Added stick/axis deadzone processors to both runtime action assets, extended existing cold `InputManager.EnsureRequiredRuntimeActions` repair path for Player gamepad bindings, and moved Player PDA to `<Gamepad>/dpad/up` so `<Gamepad>/start` remains Pause/Cancel-owned.
Cinematic Cheats used: binding-level filtering instead of per-frame UI drift simulation; semantic haptic priority packed into existing `MotorMask` instead of growing DTOs.
Exact Microseconds saved: 0 us steady-state for binding fixes; haptic priority arbitration remains fixed-ring integer compare, estimated +1-2 us worst-case haptic queue frame on i3/MX350, 0 B GC.
Verification: JSON parse passed for both `.inputactions`; forbidden hot lookup/sync wait scan clean on changed runtime files; diff check clean except CRLF warnings; build/test blocked by CPU 97% with active `dotnet`/Unity.

## 2026-06-03 17-D Follow-Up 2

Wrong: `HapticPulseSignal.PriorityFlags` mixed priority bits with source hash bits, so tool hash values could impersonate collision/explosion priority. Rebind persistence could also reintroduce Player PDA/Start conflict from stale `controls.json`.
Done: Split haptic priority/source-hash packing inside the existing 16-byte signal, masked dispatcher priority reads, reserved Start for Pause in `RebindingManager`, and rejected stale non-Pause Start overrides in `ControlRemapper`.
Cinematic Cheats used: bit packing instead of DTO expansion; fail-closed override rejection instead of UI-only warning.
Exact Microseconds saved: 0 us measurable; rebind changes are cold path only; haptic pack/read is fixed bit ops.
Verification: `.inputactions` JSON parse passed; forbidden hot token scan clean; targeted changed-directory `.meta` orphan scan clean; `git diff --check` clean except CRLF warnings; build/test blocked by CPU 100% with Unity active.

## 2026-06-03 17-D Follow-Up 3

Wrong: Local Input System source proves `WithProcessor` appends processors. Existing fallback bindings with stale stick processors could receive stacked deadzones instead of a single canonical one.
Done: `InputManager.EnsureBindingProcessor` now rewrites the existing `InputBinding.processors` field and applies it with `ChangeBinding(...).To(binding)`. New bindings still use `WithProcessor` because they start empty.
Cinematic Cheats used: one binding-level filter string in cold init instead of hot UI/gameplay drift compensation.
Exact Microseconds saved: 0 us steady-state; cold init only.
Verification: `git diff --check` clean except CRLF warnings; forbidden hot token scan clean on changed runtime files; both `.inputactions` files parsed as JSON; full repository `.meta` orphan scan returned `ORPHAN_META_OK`; build/test blocked by CPU 100% with active `dotnet` and Unity processes.

## 2026-06-03 17-D Follow-Up 4

Wrong: XR Player PDA/Pause still shared left menuButton; keyboard Tab closed UI through Cancel instead of moving focus; persisted `controls.json` could load a path colliding with another default binding before clearing current overrides; optional hand suit collision shell never produced contact.
Done: Moved XR PDA to left secondaryButton, made Tab a UI TabNext route, added a pre-clear persisted binding conflict gate, restored fixed-buffer suit shell overlap/haptics, made FABRIK bend use closest collider distance, and made physical receiver queries keep nearest entries under saturation.
Cinematic Cheats used: binding ownership instead of runtime arbitration, fail-closed load scan instead of apply/rollback mutation, sphere overlap plus nearest bounds selection instead of managed overlap allocations or per-prop scene searches.
Exact Microseconds saved: 0 us measured. Binding/remap changes are cold or asset-only. Optional shell costs estimated +2-6 us while enabled, 0 B GC; it replaces a dead feature path with bounded contact feedback.
Verification: `.inputactions` JSON parse passed; regression scans found no XR menuButton/PDA, Tab/Cancel, BendAngle=1f, haptic channel priority, or source-hash priority bleed regressions; full `.meta` orphan scan returned `ORPHAN_META_OK`; `git diff --check` clean except CRLF warnings; compile/test still blocked by CPU 78.3-88.8% with active Unity, ILPP, and shader compiler processes.

## 2026-06-03 17-D Follow-Up 5

Wrong: `ControlRemapper.TryLoadOverrides` rejected persisted conflicts, but `TrySaveOverrides` could still write duplicate override paths or an override that stole another live default binding. That leaves a bad `controls.json` for the next boot.
Done: Added a cold save-side conflict scan in existing `ControlRemapper`: reject duplicate override paths in the same map, reject override-vs-live-default collisions, and reject non-Pause Player Start before JSON buffer allocation or disk write. Added EditMode tests for save rejection and kept the load rejection test by mutating a valid saved payload.
Cinematic Cheats used: fail-closed persistence guard instead of runtime input arbitration or UI warning text.
Exact Microseconds saved: 0 us steady-state. Save scan is cold only and uses existing action/binding lists with no new collections.
Verification: `git diff --check` clean except CRLF warnings; forbidden hot token scan clean for `ControlRemapper.cs`; targeted source scans found the new save guard/tests; compile/test blocked by CPU 100% then 76.6% with active `dotnet`, Unity, and shader compiler processes.

## 2026-06-03 17-D Follow-Up 6

Wrong: `RebindingManager` presented a one-victim conflict confirmation even when a new binding path collided with multiple actions. The persistence layer would later reject that state, but the UI confirmation was incomplete.
Done: Extended `TryDetectConflict` with `multipleConflicts`, rejected unresolved multi-conflict paths before confirmation/save, restored the previous override immediately, and added an EditMode reflection test for the private cold-path detector.
Cinematic Cheats used: fail-closed interactive rejection instead of dynamic victim-list UI and multi-binding rollback storage.
Exact Microseconds saved: 0 us steady-state. Extra scan is cold rebind-completion only and reuses existing action/binding lists.
Verification: `git diff --check` clean except CRLF warnings; forbidden hot token scan clean for `RebindingManager.cs`; targeted scans found `multipleConflicts` guard and functional test; compile/test blocked by CPU 99.4% then 98.1% with active `dotnet`, Unity, and shader compiler processes.

## 2026-06-03 17-D Follow-Up 7

Wrong: Keyboard Escape was protected mostly by the default interactive cancel path, not by the same persistence-level ownership rule as Gamepad Start. Custom cancel paths or stale `controls.json` could steal Escape from Pause/Cancel.
Done: Reserved `<Keyboard>/escape` for Player/Pause and UI/Cancel in `RebindingManager` and `ControlRemapper`; interactive completion, save, and load now reject non-owner Escape. Added an EditMode save rejection test.
Cinematic Cheats used: reserved-route ownership instead of runtime action arbitration.
Exact Microseconds saved: 0 us steady-state. Rebind/save/load are cold paths only.
Verification: `git diff --check` clean except CRLF warnings; forbidden hot token scan clean for `RebindingManager.cs` and `ControlRemapper.cs`; targeted scans found Keyboard Escape guard and test; compile/test blocked by CPU 48.1% then 50.6% with active `dotnet`, Unity, and shader compiler processes.

## 2026-06-03 17-D Follow-Up 8

Wrong: Escape persistence protection had a save-side functional test and source guards, but stale or manually mutated `controls.json` load rejection lacked direct behavioral coverage.
Done: Added `ControlsJsonRejectsLoadedReservedEscapeWithoutClearingOverrides`, which saves a valid Interact override, mutates the file to `<Keyboard>/escape`, and verifies load fails before clearing current overrides.
Cinematic Cheats used: fail-closed disk payload validation instead of runtime action arbitration or post-apply rollback.
Exact Microseconds saved: 0 us steady-state. Test is editor-only; runtime load path was already cold.
Verification: `git diff --check` clean except CRLF warning; targeted scan found the stale Escape load test; forbidden hot token scan clean for `InputBindingContractsEditTests.cs`, `ControlRemapper.cs`, and `RebindingManager.cs`. Compile/test blocked by CPU 98.5% then 86.4% with active `dotnet`, Unity, and shader compiler processes.

## 2026-06-03 17-D Follow-Up 9

Wrong: Player `PDA` and UI `TabNext` both owned `<Keyboard>/tab` in the runtime action asset. Public `EnableUIInput()` does not guarantee Player is disabled, so one key could both toggle/open PDA and advance UI tabs.
Done: Moved Player `PDA` to `<Keyboard>/p`, added cold `InputManager` repair that rewrites stale Player PDA Tab bindings to P, kept Tab owned by UI `TabNext`, updated the loading tip, and extended the 17-D source guard.
Cinematic Cheats used: binding ownership and cold repair instead of hot callback arbitration.
Exact Microseconds saved: 0 us steady-state. Serialized binding/cold init only; no new hot path allocations.
Verification: `.inputactions` JSON parse passed; targeted `rg` confirmed Tab remains UI-only except the legacy rewrite constant/test assertions; forbidden hot token scan found only the existing cold `Dictionary<int, InputDisplayStyle>(32)` cache; `git diff --check` clean except CRLF warnings. Compile/test blocked by CPU 76.0% then 73.7% with active `dotnet`, Unity, ILPP, and shader compiler processes.

## 2026-06-03 17-D Follow-Up 10

Wrong: Default Player PDA no longer used Tab, but interactive rebind and stale `controls.json` could still put `<Keyboard>/tab` on a non-UI action.
Done: Reserved Tab for `UI/TabNext` in `RebindingManager` and `ControlRemapper`; non-owner Tab is excluded/rejected during interactive rebind and rejected during save/load before disk write or runtime override clear. Added functional save/load tests.
Cinematic Cheats used: narrow reserved-route contract instead of broad cross-map conflict rejection, preserving intentional WASD/E contextual overlaps.
Exact Microseconds saved: 0 us steady-state. Rebind/save/load are cold paths only.
Verification: targeted scans found `ShouldReserveKeyboardTab`, `IsProtectedKeyboardTabOverride`, and the two functional tests; forbidden hot token scan found only the existing cold `Dictionary<int, InputDisplayStyle>(32)` cache; `git diff --check` clean except CRLF warnings. Compile/test guard still blocked by active Unity/dotnet state unless CPU drops below threshold.

## 2026-06-03 17-D Follow-Up 11

Wrong: `ApplyStagedInputProfileCsvToVault` nested `_inputProfileCsvStageGate` inside the DataVault input mutation guard. That is a lock-order stall vector on CSV profile reload.
Done: Flattened the route: copy staged profile/version under the managed stage gate, acquire DataVault mutation guard only for profile buffer resolve plus `profiles[0] = stagedProfile`, release DataVault, then record applied version under the stage gate. Added an EditMode source guard proving no stage gate lock exists inside that DataVault guarded window.
Cinematic Cheats used: direct DTO copy instead of parsing or math inside the DataVault guard; existing CSV profile route reused instead of adding a duplicate profile manager.
Exact Microseconds saved: 0 us steady-state. Cold profile reload now holds the DataVault mutation guard for direct assignment only.
Verification: `.inputactions` JSON parse passed; profile lock static guard returned `PROFILE_STAGE_LOCK_OK`; Assets orphan-meta scan returned `ASSETS_ORPHAN_META_OK`; targeted `git diff --check` clean except CRLF warnings; hot-token scan found only cold allocations/caches and two cold `TryGetComponent` calls in `PhysicalHandController`. Build/tests not launched: CPU 94.4% with active Unity, ILPP, PackageManager, and ShaderCompiler processes.

## 2026-06-03 17-D Follow-Up 12

Wrong: `PublishDeterministicInputState` published `SignalBus` events, discrete input commands, crash telemetry, and deterministic black-box dumps while the input DataVault mutation guard was still held. The black-box dump path can allocate transient native payload memory and write to disk.
Done: Split the phase. The guarded block now writes deterministic native buffers and stores stack-local publication flags/snapshots. After `ReleaseInputMutationGuard`, it pushes `InputStateSignal`, publishes discrete commands, reports crash telemetry, and performs black-box dump I/O through a read-only telemetry handle. Added an EditMode source guard proving those calls are after release.
Cinematic Cheats used: stack-local state transfer instead of a new managed queue; read-only dump snapshot instead of resolving mutable telemetry for fault I/O.
Exact Microseconds saved: 0 us steady-state. Fault path avoids holding DataVault during file I/O; normal path pays fixed stack-local copies only.
Verification: `INPUT_PUBLISH_GUARD_OK`; `.inputactions` JSON parse passed; targeted `git diff --check` clean except CRLF warnings; hot-token scan found only cold caches/arrays and cold `TryGetComponent` calls. Build/tests not launched: CPU 84.2% with active `dotnet`, Unity, ILPP, PackageManager, and ShaderCompiler processes.

## 2026-06-03 17-D Follow-Up 13

Wrong: Three haptic producers still used manual `PriorityTool | (hash & 0x00FFFFFFu)` packing after the signal contract moved source hash bits out of the priority lane. Source hash low bits could still impersonate collision/explosion priority.
Done: `QuestManager`, `QuestDagResolverRuntime`, and `ToolKinematicsRuntime` now use `HapticPulseSignal.PackPriorityAndSourceHash`; `PackPriorityAndSourceHash` also preserves `FlagNanSanitized` and `FlagFaultDumpRequested`. The 17-D source guard now checks the producers and the flag-preserving mask.
Cinematic Cheats used: bit packing in the existing 16-byte signal instead of DTO expansion or managed arbitration.
Exact Microseconds saved: 0 us measurable. Runtime cost is fixed inline masks/shifts, 0 B GC.
Verification: legacy priority/hash OR scan over runtime scripts returned no matches; `.inputactions` JSON parse passed; full `.meta` orphan scan returned `ASSETS_ORPHAN_META_OK`; targeted `git diff --check` clean except CRLF warnings. Build/tests not launched: CPU samples were 100/100/100 with active `dotnet`, Unity, ILPP, PackageManager, and ShaderCompiler processes.

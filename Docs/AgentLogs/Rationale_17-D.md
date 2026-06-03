# Rationale_17-D

Status: STATIC VERIFIED, COMPILE GATED BY CPU/RUNNING UNITY DOTNET

## Preflight

Problem: Active batch file does not contain `<AGENT_PROMPT id="17-D">`; direct XML extraction returned no match.
Solution: Use user-supplied 17-D assignment as active directive and bound edits to source-present input, haptics, UI navigation, and physical hand interaction files.
Rejected Alternatives: Reading neighboring batch prompts; rejected because strict parsing forbids cross-agent task bleed. Treating archived logs as authority; rejected because batch hygiene forbids stale authority.
Scalability potential: Low/Middle/High/Ultra remains continuous through GlobalQualityWeight only; no binary device quality switch is accepted.
Hardware Impact: No runtime impact from preflight. i3/MX350 impact 0 us.

## Relevant Mandate Set

Problem: Input/haptic/UI/hand work crosses hot-path and presentation boundaries.
Solution: Apply these mandates: CTRL_Device_Abstraction_Haptics, PHYS_Kinematic_Interaction_Hands, ANIM_IK_FABRIK_GroundSnapping_Procedural, UI_Diegetic_Physical_Interfaces, OPT_Zero_GC, DBG_Telemetry, ARCH_Execution_Phases, DATA_Runtime_Struct_Layout_ARM64.
Rejected Alternatives: Reading unrelated AI/render/voxel mandates; rejected because it increases context noise and risks neighboring-domain bleed.
Scalability potential: Low uses clean snapshots/core rumble/readable focus. Middle adds richer haptic priority blending. High adds adaptive trigger/device polish. Ultra adds sensory layers without changing action IDs, DTO layout, or gameplay truth.
Hardware Impact: Mandate selection only. i3/MX350 impact 0 us.

## Haptic Priority Arbitration

Problem: `InputDispatcher.ApplyHapticRequestContribution` used `request.Channel` as priority. `HapticRequest.ChannelMicroVibration = 6` could outrank collision or hull critical requests if routed through the haptic request path. The queued `HapticCommandDTO` path also evicted by magnitude only, so a high-amplitude laser/tool pulse could displace a lower-amplitude critical hull hit.
Solution: Keep `HapticCommandDTO` at 16 bytes and pack semantic priority plus blend mode into unused high bits of `MotorMask`. Resolve haptic request and synthesized pulse priorities through explicit semantic mapping: Micro=0, Tool=1, Collision=2, Critical=3. Queue replacement now evicts the lowest priority first, then the lowest magnitude inside the same priority. Critical/collision/light thud use max blend; micro/tool use additive blend.
Rejected Alternatives: Expanding `HapticCommandDTO`; rejected because DTO layout is deterministic ABI and mirrored in two contracts. Adding a managed priority queue; rejected because hot haptic dispatch must stay zero-GC. Treating channel enum order as priority; rejected because existing channel values are not ordered by gameplay importance.
Scalability potential: Low keeps one clean critical rumble lane and suppresses noisy micro vibration. Middle preserves collision/tool layering. High and Ultra can stack richer sensory pulses without stealing the critical hull-impact channel because priority is continuous arbitration, not a binary quality switch.
Hardware Impact: No heap allocation and no DTO stride growth. Extra per-command work is fixed small integer/float comparisons across the existing command ring. Estimated i3/MX350 impact: +1-2 us worst-case haptic frame, with avoided false eviction of critical pulses.

## UI And Hand Scope Gate

Problem: User prompt requested helmet/menu prefab and FABRIK cockpit prop polishing, but source-present evidence showed only first-party HUD/player prefabs in the searched prefab set, while `PhysicalHandController.cs` already carried a large pre-existing dirty diff from another worker.
Solution: Audit source routes only. Verified existing UI submit/cancel/navigation routes in `InputManager`, `PauseMenuController`, `PauseControlsPanel`, and `PDAControlsRebindUI`. Verified physical hand code already contains FABRIK finger job, kinematic bridge telemetry, cold allocation markings, and SignalBus haptic output. Avoided overwriting the already-dirty hand controller.
Rejected Alternatives: Editing third-party/demo prefabs; rejected as out of domain. Rewriting `PhysicalHandController.cs` despite pre-existing changes; rejected as concurrent-agent damage. Inventing 50 cockpit prop prefabs that are not present in the discovered first-party prefab set; rejected as fake implementation.
Scalability potential: Low/Middle/High/Ultra UI scaling stays with current navigation and visual systems. Hand scalability remains existing virtual mass, SDF bridge, and telemetry path; no binary quality branch added.
Hardware Impact: No runtime code added in hand/UI scope. Estimated i3/MX350 impact: 0 us.

## Verification Gate

Problem: Local rules require compile verification, but also forbid launching dotnet/build while CPU is above 50 percent or compiler processes are active.
Solution: Ran static source scans and added an EditMode source-audit test locking the haptic priority contract and 16-byte DTO layout. CPU samples were 100 percent then 93 percent, so Unity/dotnet compilation was not launched under the guard.
Rejected Alternatives: Running a build anyway; rejected because it violates the explicit CPU guard. Reporting compile pass without a compiler run; rejected as fake proof.
Scalability potential: Test locks the ABI and priority route so future device/haptic scaling cannot reintroduce micro-vibration priority inversion.
Hardware Impact: Editor-only test. Runtime impact 0 us.

## Root Input Profile Tuning

Problem: The repeated 17-D pass requires direct disk tuning for stick deadzones, movement response, and haptic cadence. `InputDispatcher` already owns a cold `FileSystemWatcher` route for root `input_profiles.csv`, but the file was absent, so runtime fell back to hardcoded defaults only.
Solution: Added root `input_profiles.csv` with supported ASCII `key,value` rows: `inner_deadzone=0.14`, `outer_deadzone=0.96`, `move_exponent=1.55`, `mouse_acceleration=0.06`, `haptic_thermal_amplitude_scale=0.62`, `haptic_dispatch_interval_seconds=0.0333333`, `mock_collision=0`. This uses the existing watcher -> staged DTO -> GlobalDataVault route.
Rejected Alternatives: Adding new hot-path per-device branching; rejected because one profile DTO already exists and avoids control-flow spread. Editing Unity legacy `ProjectSettings/InputManager.asset`; rejected because runtime uses the new InputSystem action asset and `InputDispatcher` cached actions. Editing `SteamDeckInputPal` constants without profiler proof; rejected because current EWMA path exists and root profile gives lower-risk tuning now.
Scalability potential: Low uses higher deadzone and thermal haptic amplitude reduction to hide drift and reduce rumble spam. Middle uses the same curve with stable analog range. High and Ultra can still add richer priority haptics because the haptic queue now preserves critical pulses.
Hardware Impact: Existing steady-state path already reads a Vault `InputProfileDTO`; added file causes no per-frame disk I/O. i3/MX350 steady-state impact 0 us. On edit/import, watcher parse is cold and bounded by a 512-byte read buffer.

## Mouse-Free UI Guard

Problem: The prompt requires menus to be traversable without a mouse. The asset already had non-pointer routes, but no 17-D guard locked them.
Solution: Expanded the 17-D EditMode source-audit test to assert `Assets/InputSystem_Actions.inputactions` contains keyboard/gamepad Navigate plus wildcard Submit/Cancel, and `InputManager` runtime backfills gamepad dpad/leftStick, buttonSouth submit, buttonEast/start cancel, and shoulder tab navigation.
Rejected Alternatives: Editing prefab YAML blindly; rejected because discovered first-party prefab set did not expose a concrete menu prefab target and runtime backfill is the authoritative route. Relying on mouse Point/Click actions as proof; rejected because pointer paths do not satisfy no-mouse traversal.
Scalability potential: Low/Middle/High/Ultra device traversal remains input-scheme agnostic. Steam Deck uses the Gamepad scheme path; DualSense/Xbox share the same action contract.
Hardware Impact: Editor-only guard plus existing runtime bindings. Runtime impact 0 us.

## Steam Deck Gyro And Trackpad Filter

Problem: `SteamDeckInputPal` used a fixed `GyroEwmaAlpha = 0.22f`. That smooths noise but makes gyro response frame-rate dependent and sluggish relative to the one-frame input target. Trackpad proxy also passed raw stick vectors above threshold without radial remap, so small drift just outside the deadzone could enter UI/gameplay as a non-zero proxy.
Solution: Replaced fixed alpha with `ResolveLowPassAlpha(deltaTime, cutoffHz) = 1 - exp(-2*pi*cutoff*dt)`. Active gyro cutoff is 12 Hz for responsive aim, idle decay cutoff is 18 Hz for quick return to zero. Trackpad proxy now uses `TryApplyRadialDeadzone`, remapping magnitude from deadzone edge to full range.
Rejected Alternatives: Kalman filtering or sensor fusion; rejected because no sensor covariance proof and too much work for a hot input path. Per-frame device search; rejected because `BindGamepad` already owns device binding. Adding new DTO fields for gyro tuning; rejected because input DTO ABI must remain stable.
Scalability potential: Low gets stronger drift suppression and quick zero decay. Middle keeps stable Deck gyro aim. High and Ultra can layer richer device haptics without changing gameplay truth or control bindings.
Hardware Impact: No allocation, no collections, no scene search. Adds one `math.exp` on Steam Deck gyro frames and one square-root only when a bound trackpad proxy is above threshold. Estimated i3/MX350/Steam Deck impact: +0.5-1.0 us on active Deck gyro frames, 0 B GC.

## Gamepad UI Deadzone Binding

Problem: `Assets/InputSystem_Actions.inputactions` routed gamepad stick directions into UI `Navigate` with empty processors. That allowed analog drift to move menus without mouse or explicit input. `InputManager.EnsureUiActionMap` also repaired missing UI maps with a raw `<Gamepad>/leftStick` binding.
Solution: Added `stickDeadzone(min=0.14,max=0.96)` to gamepad Move/Look vector bindings in both action assets, `axisDeadzone(min=0.14,max=0.96)` to root gamepad UI stick composite parts, and the same stick processor to `InputManager` cold repair path.
Rejected Alternatives: Filtering UI navigation in `OnNavigatePerformed`; rejected because callbacks are hot and should receive already-normalized action values. Adding a second UI input abstraction; rejected as duplicate topology.
Scalability potential: Low devices suppress worn-stick drift. Middle/High/Ultra devices retain full analog range after the outer threshold without changing action identity or menu contracts.
Hardware Impact: No new runtime allocations or scene lookups. Existing InputSystem processor runs inside the existing input read path. Estimated i3/MX350 impact: below measurement noise, 0 B GC.

## InputManager Generated Fallback Repair

Problem: `InputManager` can fall back to a source-generated action asset when `_inputActionAsset` is null. That fallback may not contain the full gamepad binding set from `Assets/Resources/HectonRuntimeInputActions.inputactions`, so controller play could silently degrade on misconfigured prefabs/scenes.
Solution: Extended existing cold `EnsureRequiredRuntimeActions` with `EnsurePlayerGamepadBindings`, adding missing Movement/Look stick bindings with deadzone processors and canonical gamepad buttons only when absent.
Rejected Alternatives: Assuming serialized asset assignment is always valid; rejected because prefab/scene drift is exactly the failure mode this repair path exists to cover. Adding hot per-frame device fallback; rejected because binding topology belongs in initialization.
Scalability potential: Low/Middle/High/Ultra devices share the same repaired action identity; no device-specific gameplay branch is introduced.
Hardware Impact: Cold initialization only. No steady-state allocations, searches, or per-frame cost.

## PDA Pause Gamepad Conflict

Problem: `HectonRuntimeInputActions` mapped Player `PDA` and `Pause` to `<Gamepad>/start`, producing an unresolvable same-map conflict: one physical press could dispatch both `OnPDA` and `OnPause`.
Solution: Kept `<Gamepad>/start` for Pause and moved Player `PDA` to `<Gamepad>/dpad/up` in the runtime action asset and fallback repair path.
Rejected Alternatives: Keeping dual dispatch and hoping consumers arbitrate; rejected because conflict resolution belongs in binding ownership. Mapping PDA to select; rejected because Inventory already owns select.
Scalability potential: Xbox, DualSense, and Steam Deck share Start=Pause, D-pad Up=PDA. No platform fork.
Hardware Impact: Binding-only change. Runtime cost 0 us.

## Haptic Pulse Hash Bleed

Problem: `HapticPulseSignal.PriorityFlags` previously stored `priorityFlags | (sourceHash & 0x00FFFFFFu)`. After semantic haptic arbitration, source hash low bits could falsely set collision/explosion priority.
Solution: Kept `HapticPulseSignal` at 16 bytes and split the bit field: bits 0-2 are priority, bits 3-27 are shifted source hash, bits 28+ remain fault flags. `InputDispatcher` resolves pulse priority through `ExtractPriorityFlags`.
Rejected Alternatives: Adding a second field; rejected because signal stride is fixed ABI. Late heuristic filtering; rejected because producer owns packing truth.
Scalability potential: Low suppresses noise; Middle/High/Ultra can keep source identity without corrupting haptic priority.
Hardware Impact: Bit shift/mask only. Estimated i3/MX350 impact: 0 us measurable, 0 B GC.

## Rebind Start Reservation

Problem: Asset PDA/Pause fix did not protect against stale `controls.json` overrides or interactive conflict confirmation assigning Start to a non-Pause Player action.
Solution: `RebindingManager` excludes and rejects Start for non-Pause Player rebinds. `ControlRemapper` rejects stale persisted Start overrides for Gamepad, XInput, DualShock, DualSense, and Steam Deck exact paths unless action is Pause.
Rejected Alternatives: UI-only warning text; rejected because persistence/load must fail closed. Allowing conflict confirm to steal Pause; rejected because Pause is an escape lane.
Scalability potential: Xbox, DualSense, Steam Deck, and generic gamepads retain a stable pause/cancel lane without gameplay platform forks.
Hardware Impact: Cold rebind/load path only. Runtime steady-state cost 0 us, 0 B GC.

## Existing Binding Processor Replacement

Problem: Local package source shows `InputActionSetupExtensions.BindingSyntax.WithProcessor` appends to existing `InputBinding.processors`. The first `InputManager` repair patch would add a deadzone to raw bindings, but could stack duplicate deadzone processors if a stale binding already carried a mismatched `stickDeadzone`.
Solution: Existing binding repair now copies the `InputBinding`, sets `binding.processors` to the canonical processor string, and applies it through `action.ChangeBinding(bindingIndex).To(binding)`. New bindings still use `WithProcessor` because there is no existing processor string to preserve or duplicate.
Rejected Alternatives: Additive `WithProcessor` on existing bindings; rejected because doubled deadzones are input drift bugs. Manually editing generated action code; rejected because `InputManager` cold repair is the active owner of runtime fallback binding topology.
Scalability potential: Low devices with worn sticks get deterministic drift suppression. Middle/High/Ultra devices keep full usable stick range after the outer threshold without a platform fork.
Hardware Impact: Cold initialization only. Runtime steady-state impact 0 us, 0 B GC.

## XR And Keyboard Menu Escape Lanes

Problem: Runtime actions still mapped XR Player `PDA` and `Pause` to `<XRController>{LeftHand}/menuButton`, and UI `Cancel` used `<Keyboard>/tab`. The first makes one XR menu press dispatch two Player actions; the second makes a normal keyboard traversal key back out of menus.
Solution: Moved XR Player `PDA` to `<XRController>{LeftHand}/secondaryButton`, left menuButton owned by Pause, and made `<Keyboard>/tab` a UI `TabNext` binding in both the runtime action asset and `InputManager` cold repair.
Rejected Alternatives: Keeping menuButton dual-use and relying on consumer order; rejected because action ownership must be unambiguous. Removing Tab entirely; rejected because keyboard users need forward menu traversal without mouse.
Scalability potential: Low/Middle devices keep simple deterministic escape lanes. High/Ultra XR controllers can add richer physical UI without changing Pause ownership.
Hardware Impact: Binding-only change. Runtime steady-state impact 0 us.

## Controls Json Load Conflict Gate

Problem: Interactive rebind conflict checks did not fully protect persisted `controls.json`. A saved override could collide with another action's default binding after restart, and load would clear current overrides before discovering the conflict.
Solution: Added a cold pre-clear conflict scan in `ControlRemapper.TryLoadOverrides`. It rejects duplicate loaded paths in the same action map and rejects a loaded path that matches another live default binding unless that target binding is also owned by a loaded record.
Rejected Alternatives: Apply first then rollback; rejected because it mutates live input state during a failed load. New telemetry enum; rejected to avoid public API churn.
Scalability potential: All devices share one fail-closed persistence route. Future Xbox/DualSense/SteamDeck bindings can be added without weakening the escape-lane rule.
Hardware Impact: Cold load only. Runtime steady-state impact 0 us, 0 B GC.

## Physical Hand Shell And Receiver Saturation

Problem: `StepSuitCollisionShell` disabled its own collider, cleared the overlap buffer, and forced `_suitContactActive = false`, so the opt-in non-SDF suit contact path could never report contact or haptics. FABRIK fingers always wrote `BendAngle = 1f`, and receiver sphere queries stopped at the first filled result buffer, allowing dense cockpit panels to miss closer controls later in the fixed table.
Solution: The shell now uses `Physics.OverlapSphereNonAlloc` into the existing fixed collider buffer, computes strongest penetration, queues hand-side haptics through `ToolHapticsRuntime`, and applies bounded wall recoil. FABRIK now targets `_activeBodyCollider.ClosestPoint` when available and derives bend from contact distance. Receiver queries scan all fixed receiver slots and replace the farthest stored result when a closer collider appears after saturation.
Rejected Alternatives: `Physics.OverlapSphere` managed allocation; rejected. Constant full finger curl; rejected because near-surface and deep grip need different presentation. Increasing receiver buffer size; rejected because it hides ordering bugs and increases cold memory without solving nearest-selection correctness.
Scalability potential: Low uses cheap sphere overlap and nearest-control selection. Middle gets cleaner cockpit press haptics. High/Ultra can layer richer hand visuals because contact truth is now stable and side-specific.
Hardware Impact: Optional shell only. Estimated i3/MX350 impact +2-6 us while enabled, 0 B GC; receiver scan remains fixed 128 slots.

## Controls Json Save Conflict Gate

Problem: The load path rejected persisted binding conflicts, but save could still write a `controls.json` where one action override stole another action's live default binding or where two overrides shared one path. That made the next boot fail closed instead of preventing the bad file at source.
Solution: Added a cold save-side scan inside existing `ControlRemapper.TrySaveOverrides`. It rejects duplicate non-empty override paths in the same action map, override paths matching another binding's live default path, and non-Pause Player Start overrides before allocating the JSON buffer or touching disk.
Rejected Alternatives: Let save succeed and rely on load failure; rejected because it creates avoidable bad state on disk. Adding a separate remap validator class; rejected because `ControlRemapper` already owns controls.json serialization and conflict ownership.
Scalability potential: Low/Middle/High/Ultra devices share one persistence contract. Xbox, DualSense, Steam Deck, keyboard, and XR remaps fail closed without device forks or runtime arbitration.
Hardware Impact: Cold save only. Runtime steady-state impact 0 us, 0 B GC. On save, fixed nested scans over existing action/binding lists; no new collections or scene searches.

## Interactive Rebind Multi-Conflict Gate

Problem: `RebindingManager` conflict confirmation stored only one victim binding. If a new binding path collided with two or more actions, the UI could present a single-victim confirmation, then `ControlRemapper` would reject the saved state later. That is correct persistence behavior but bad interactive ownership.
Solution: Extended the existing cold `TryDetectConflict` path with a `multipleConflicts` out flag. One conflict still follows the existing confirmation flow. More than one conflict restores the previous binding, cancels the rebind, and logs a deterministic rejection before save.
Rejected Alternatives: Disable every victim binding; rejected because the current rollback state stores one victim and expanding that into a new dynamic conflict list would add topology for an edge case. Let save fail after confirmation; rejected because it lies to the UI about what is being confirmed.
Scalability potential: Keyboard, Xbox, DualSense, Steam Deck, and XR rebinds now share one user-visible fail-closed rule. Low devices avoid extra dialog churn; high-end devices can add richer UI later without changing binding truth ownership.
Hardware Impact: Cold rebind completion only. Runtime steady-state impact 0 us, 0 B GC. The extra scan continues over existing action/binding lists only when an interactive rebind completes.

## Keyboard Escape Reservation

Problem: Gamepad Start was protected as a pause/cancel lane, but Keyboard Escape relied mostly on the default interactive cancel path. A custom cancel path or stale `controls.json` could still assign Escape to a non-Pause/non-Cancel action.
Solution: Added the same ownership rule to `RebindingManager` and `ControlRemapper`: `<Keyboard>/escape` is legal only for Player/Pause and UI/Cancel. Interactive rebind completion rejects protected Escape, and save/load reject stale protected Escape records before persistence mutation.
Rejected Alternatives: Trust `WithCancelingThrough("<Keyboard>/escape")`; rejected because callers can pass a different cancel path and persistence must defend itself independently. Add a new escape-lane service; rejected because binding ownership already lives in `RebindingManager`/`ControlRemapper`.
Scalability potential: Keyboard/mouse, Steam Deck keyboard overlays, and controller-plus-keyboard users retain a reliable escape lane on every tier. High/Ultra UI polish can layer prompts later without changing the reserved-route truth.
Hardware Impact: Cold rebind/save/load only. Runtime steady-state impact 0 us, 0 B GC.

## Stale Controls Json Escape Load Proof

Problem: Escape load-side protection existed in source, but the functional test only proved save-side rejection. A future edit could weaken the `TryLoadOverrides` pre-clear path while source-string guards still passed.
Solution: Added an EditMode test that writes a valid `controls.json`, mutates the saved path from `<Keyboard>/enter` to `<Keyboard>/escape`, then verifies `TryLoadOverrides` returns `UnsupportedPath` without calling runtime clear and without losing the current override.
Rejected Alternatives: Source-string-only proof; rejected because input persistence needs behavioral coverage for stale/mutated disk payloads. Applying then rolling back; rejected because failed load must not mutate live input state.
Scalability potential: Low/Middle/High/Ultra device routes retain the same keyboard escape lane even when users share or hand-edit config files. Higher-tier UI can add richer conflict presentation later without changing persistence truth.
Hardware Impact: Editor-only test. Runtime steady-state impact 0 us, 0 B GC.

## Keyboard PDA And UI Tab Ownership

Problem: `HectonRuntimeInputActions` still mapped Player `PDA` to `<Keyboard>/tab` while UI `TabNext` also owned `<Keyboard>/tab`. `SwitchToUIInput()` disables Player, but public `EnableUIInput()` can enable UI without disabling Player, so Tab remained an input ownership collision.
Solution: Moved Player `PDA` keyboard binding to `<Keyboard>/p`, kept Tab as UI `TabNext`, added cold `InputManager.EnsurePlayerKeyboardBindings` to rewrite stale Player PDA Tab bindings to P, and updated the loading tip text.
Rejected Alternatives: Runtime arbitration inside `OnPDAPerformed` or `OnTabNextPerformed`; rejected because callbacks are hot and binding ownership must be resolved before dispatch. Removing Tab from UI; rejected because keyboard menu traversal without mouse needs a dedicated forward-tab lane.
Scalability potential: Low/Middle keyboard users get deterministic menu traversal. Steam Deck keyboard overlay and full keyboard users keep P as PDA while gamepad uses D-pad Up. High/Ultra UI can add richer prompts without changing action ownership.
Hardware Impact: Serialized binding change plus cold action-map repair only. Runtime steady-state impact 0 us, 0 B GC.

## Reserved Keyboard Tab Rebind And Persistence

Problem: Moving the default PDA key was not enough; interactive rebinding or stale `controls.json` could assign `<Keyboard>/tab` back to any non-UI action. A broad cross-map conflict ban is not valid because existing default WASD/E overlaps are intentional between Player and UI contexts.
Solution: Reserved `<Keyboard>/tab` specifically for `UI/TabNext` in `RebindingManager` and `ControlRemapper`. Interactive completion excludes/rejects Tab for non-owners; save/load reject non-owner Tab before writing JSON or clearing current overrides. Added save and stale-load functional tests.
Rejected Alternatives: Rejecting every cross-map path collision; rejected because it would invalidate existing contextual bindings like Player movement vs UI navigation. Callback arbitration; rejected because it adds hot-path branch ownership after dispatch.
Scalability potential: Low/Middle keyboard users retain predictable menu traversal. High/Ultra UI can add richer tab visuals or controller prompts without changing the route contract.
Hardware Impact: Cold rebind/save/load only. Runtime steady-state impact 0 us, 0 B GC.

## Input Profile Lock Flattening

Problem: `InputDispatcher.ApplyStagedInputProfileCsvToVault` acquired the DataVault input mutation guard and then entered `_inputProfileCsvStageGate`. That nested DataVault + managed lock pattern violates the lock-flattening rule and can stall the owner thread if the file watcher is holding the stage gate.
Solution: Copy `InputProfileDTO` and staged version under `_inputProfileCsvStageGate` before acquiring the DataVault mutation guard. The DataVault guarded block now only resolves the input profile buffer and assigns `profiles[0] = stagedProfile`. Applied-version bookkeeping happens after DataVault release under the stage gate.
Rejected Alternatives: Keeping the nested lock because the code is cold; rejected because cold paths still execute on profile reload and can deadlock under file watcher timing. Marking the watcher volatile-only; rejected because the existing stage gate already owns the staged DTO route.
Scalability potential: Low/Middle devices can reload conservative deadzone/haptic CSV tuning without risking a frame hitch from lock contention. High/Ultra can use richer profile edits with the same single-owner route and no gameplay authority split.
Hardware Impact: 0 us steady-state. Cold profile apply avoids a potential owner-thread stall; DataVault lock duration is now direct resolve plus DTO assignment only.

## Input Publish And Fault Dump Phase Split

Problem: `PublishDeterministicInputState` held the DataVault input mutation guard while pushing `SignalBus<InputStateSignal>`, publishing discrete input commands, reporting crash telemetry, and potentially writing the deterministic black-box dump. The dump path allocates transient native payload memory and writes a file, so a rare NaN or polling spike could turn an input buffer lock into a main-thread stall.
Solution: Keep DataVault guard ownership around deterministic snapshot math and native buffer writes only. Store the resolved `InputStateSignal`, previous/current button masks, packed axis bits, and dump/report flags in stack locals; release the DataVault mutation guard; then publish signals, discrete commands, crash telemetry, and dump I/O. The dump now uses `TryReadInputBuffer` with a read-only handle and `telemetry.GetUnsafeReadOnlyPtr()`.
Rejected Alternatives: Keeping publish/dump under guard because it is the same frame; rejected because phase safety requires state transfer via stack locals, not work under lock. Deferring to another managed queue; rejected because it adds topology and allocation risk when stack locals are enough.
Scalability potential: Low/Middle devices avoid rare fault-path stalls during input frames. High/Ultra can add richer telemetry consumers behind `SignalBus` without extending the DataVault lock duration or changing gameplay truth ownership.
Hardware Impact: 0 us steady-state. Fault path moves file I/O and transient native payload allocation outside the mutation guard; normal path adds fixed local copies only.

## Cross-Producer Haptic Priority Packing

Problem: `HapticPulseSignal` had a safe `PackPriorityAndSourceHash` route, but `QuestManager`, `QuestDagResolverRuntime`, and `ToolKinematicsRuntime` still packed tool haptics manually as `PriorityTool | (hash & 0x00FFFFFFu)`. Low source hash bits could still set collision/explosion priority before dispatcher extraction.
Solution: Replace all remaining manual tool-source packing with `HapticPulseSignal.PackPriorityAndSourceHash`. Extend the existing 17-D source guard to inspect those producers directly and reject the old `0x00FFFFFFu` OR pattern.
Rejected Alternatives: Rely only on `InputDispatcher.ExtractPriorityFlags`; rejected because extraction cannot distinguish a real low priority bit from a malformed producer payload. Add a second source-hash field; rejected because `HapticPulseSignal` is a 16-byte signal contract.
Scalability potential: Low devices keep micro/tool haptics from drowning critical feedback. Middle/High/Ultra can add richer quest/tool rumble without corrupting critical hull-impact arbitration.
Hardware Impact: Fixed bit shifts/masks only. Runtime impact 0 us measurable, 0 B GC.

## Haptic Pulse Fault Flag Preservation

Problem: `PackPriorityAndSourceHash` preserved priority bits and source hash but dropped fault flags if a future producer passed `PriorityTool | FlagFaultDumpRequested` into the packer.
Solution: Add `HapticPulseSignal.FlagMask` and preserve `PriorityMask | FlagMask` during packing while source hash remains confined to bits 3-27.
Rejected Alternatives: Require every caller to OR flags after packing; rejected because it splits one payload contract across call sites and invites another drift bug. Expand signal layout; rejected because 16-byte ABI is already validated.
Scalability potential: Low/Middle devices retain black-box fault signaling without extra payloads. High/Ultra haptic producers can attach richer fault markers without changing source hash or priority extraction.
Hardware Impact: One extra constant mask in an inline method. Runtime impact 0 us measurable, 0 B GC.

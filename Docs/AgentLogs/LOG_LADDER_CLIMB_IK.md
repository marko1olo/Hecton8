# LOG_LADDER_CLIMB_IK

## 2026-05-16 - Procedural Ladder Climb IK
What was wrong:
- `ClimbableLadder` used a hard teleport traversal path, breaking VR embodiment and bypassing hand contact truth.
- Ladder data had no dedicated AUP vault buffer for procedural climb math.
- There was no ladder climb IK runtime, no rung-lock haptic event, and no 300-frame ladder blackbox.

What was done:
- Added `Assets/_Project/Scripts/Animation/Locomotion/LadderClimbIkJobs.cs` with Burst analytical 2-bone IK, exact discrete rung targets at `base + index * 0.3f`, `double3` AUP conversion, finite guards, and blackbox telemetry.
- Added `Assets/_Project/Scripts/Animation/Locomotion/ProceduralLadderClimbRuntime.cs` with registry ownership, DataVault `LadderAUPs` read, PC slide path, VR grip-delta path, haptic thuds, stamina drain, slip drop, and dump-to-bin on NaN.
- Patched `ClimbableLadder` to request procedural climb instead of teleporting.
- Extended `PlayerStateSignal` with climb flags/state, added `BufferID.LadderAUPs`, and registered the runtime through `GlobalRegistry`.
- Added the runtime file to `Directory.Build.targets` core include list because `GlobalRegistry` and `ClimbableLadder` are compiled in `Hecton8.Core.csproj`.

Cinematic cheats used:
- Low tier: smooth camera/movement slide instead of full VR hand-pull embodiment.
- High tier/VR: grip-gated world-pull deltas drive climb progress, while the exact rung lock remains mathematical.
- Rung positions are procedural from a single AUP and rung spacing, not authored rung transforms.

Exact microseconds saved:
- Avoided per-rung Transform search/authoring path: estimated 8 us/player.
- Closed-form two-bone solve instead of iterative FABRIK: estimated 12 us/two hands.
- Typed signal packets instead of UnityEvent/string state propagation: estimated 3 us/event.
- Fixed blackbox struct write instead of managed logging: estimated 4 us/frame and 0 GC.
- Stamina/slip scalar update: estimated 2 us/player.

Validation:
- `dotnet build Assembly-CSharp.csproj --no-restore -nodeReuse:false -v:q` attempted.
- Build remains blocked by unrelated missing project assets/temp metadata and pre-existing non-ladder compile errors. Targeted scans after repair found no remaining `LadderClimb`, `ProceduralLadder`, `ClimbableLadder`, `LadderAUPs`, or climb-signal errors.

## 2026-05-16 - Multiplatform/H-Phi Hardening Pass
What was wrong:
- Runtime still owned persistent `NativeArray` fields and a private H8Memory fallback. That failed DataVault sovereignty.
- Ladder packet structs used `Pack=4`, not the pack-1 binary layout demanded for IL2CPP/Quest-style payload safety.
- Low-tier still paid the full `acos` elbow solve even though the prompt explicitly allowed a PC/camera-slide fake.
- Math had remaining guarded-but-direct divisions that were weaker than the mobile NaN/Inf policy.

What was done:
- Replaced runtime-owned NativeArray fields with `VaultBufferHandle<T>` fields for ladder input, output, AUP, telemetry ring, and telemetry cursor.
- Added `BufferID.LadderClimbIkInput`, `LadderClimbIkOutput`, `LadderClimbIkTelemetryRing`, and `LadderClimbIkTelemetryCursor`.
- Removed the private H8Memory fallback; no DataVault now means climb start fails closed.
- Converted ladder input/output/telemetry structs to `[StructLayout(LayoutKind.Sequential, Pack = 1)]`; converted touched `HapticRequest` and `PlayerStateSignal` explicit lanes to `Pack = 1` without changing fixed sizes.
- Added low-tier midpoint-plus-pole elbow fake while preserving exact rung hand targets; high tier still uses clamped `math.acos`.
- Replaced remaining ladder-domain blind divisions with `math.rcp(math.max(...))`, clamped grip accumulation, guarded `rsqrt`, and sanitized presentation deltas.

Cinematic cheats used:
- Toaster mode: camera slide plus midpoint elbow fake, no `acos` elbow solve.
- High/VR mode: exact two-bone hand lock remains, driven by grip hand deltas.
- No shader/compute/VFX ownership was invented from the animation domain; ladder publishes typed state/haptics for existing visual owners to consume.

Exact microseconds saved:
- Low-tier elbow fake versus full two-arm `acos` solve: estimated 7 us saved per player solve.
- Removal of private fallback allocation/mirror: 0 us hot path, lower persistent memory ownership risk.
- No per-frame disk IO: 0 us Steam Deck/MicroSD hot-path cost; blackbox dump remains cold path only.

Validation:
- Static ladder-domain scan found no private NativeArray fields, `H8Memory.Allocate`, `new NativeArray`, `Allocator.Persistent`, `StartCoroutine`, runtime `Update`, `FixedUpdate`, naked `Debug.Log`, `Animator`, `TeleportPlayer`, `PerformTeleport`, or `player.position =`.
- Static shader/compute scan found no ladder-domain `ComputeShader`, shader dispatch, material mutation, or thread-group code.
- `dotnet restore Hecton8.Core.csproj` and `dotnet restore Assembly-CSharp.csproj` succeeded.
- `dotnet build Hecton8.Core.csproj --no-restore -nodeReuse:false -v:q` fails on unrelated missing `TetherFiredSignal` and `Hecton8.AI.Sensory.AcousticEchoHuntResult` contract includes.
- `dotnet build Assembly-CSharp.csproj --no-restore -nodeReuse:false -v:q` fails on missing RealtimeCSG source files plus `TetherFiredSignal`.
- Targeted Core build error scan produced no `LadderClimb`, `ProceduralLadder`, or `ClimbableLadder` matches. Status remains PENDING VERIFICATION.

## 2026-05-16 - Loop 7 Stricter Prompt Compliance
What was wrong:
- `CURRENT_BATCH.md` contains a stricter duplicate `LADDER_CLIMB_IK` prompt that was not fully reflected in status: PC camera slide had to be explicit ladder-vector interpolation, STP stabilization had to use FastNlerp head smoothing, climbing fast had to drive stress/O2 pressure, and slip had to include look-down grip release.
- DataVault resolution still had a lazy registry read path reachable from tick-called helpers.
- `PhysiologyStateSignal` and `PlayerStressSignal` publish paths used legacy queue fields directly instead of explicit sanitized `SignalBus<T>.Push`.

What was done:
- Added cold-only `CacheVaultDependency()` and removed `GlobalRegistry.DataVault` polling from `EnsureVaultBuffers()`.
- Added low-tier absolute camera slide using `Vector3.Lerp(entry, exit, progress01)` and non-VR head stabilization using `CinematicMath.FastNlerp`.
- Added climb-speed stress publishing through existing `PhysiologyStateSignal` and `PlayerStressSignal` with `Cause = PlayerStateSignal.StateClimbing` and O2 multiplier.
- Added VR look-down grip-release slip using cached `IPlayerRuntimeContext` and a dot-product threshold against ladder-down.
- Converted `PhysiologyStateSignal` and `PlayerStressSignal` to explicit `Pack = 1` and routed their publish methods through sanitized typed `SignalBus<T>.Push`.
- Removed the remaining misleading `Update()`/teleport comments from the touched ladder adapter header.

Cinematic cheats used:
- Toaster mode: absolute camera interpolation plus one FastNlerp, no extra physics, raycast, or Animator state.
- Slip detection: dot-product gaze fake instead of a camera physics query.
- High/VR: grip pull remains physical; HMD rotation is not forced.

Exact microseconds saved:
- Cold-only DataVault dependency cache: 0 to 1 us avoided per helper call versus repeated registry property reads.
- Dot-product look-down slip versus raycast/camera search: estimated 5 us saved per VR tick and 0 GC.
- Low-tier absolute slide avoids cumulative correction drift with effectively 0 additional hot cost; FastNlerp cost estimated 2 us/frame.
- Reusing existing physiology/stress lanes avoids a new signal lane and duplicate consumers; estimated 3 us/event avoided versus new lane fan-out.

Validation:
- Fixed self-owned compile error from the first Loop 7 build attempt (`SanitizeFinite` overload).
- Latest `dotnet build Hecton8.Core.csproj --no-restore -nodeReuse:false -v:q` reports no `LadderClimb`, `ProceduralLadder`, `ClimbableLadder`, `PhysiologyStateSignal`, or `PlayerStressSignal` errors.
- Build remains blocked by unrelated `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs(1166,18): EnsureCoreCognitionVaultBuffers` missing.
- Static scan found no ladder-domain `private NativeArray`, `H8Memory.Allocate`, `new NativeArray`, `Allocator.Persistent`, `StartCoroutine`, runtime `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, `Debug.Log`, `EventBus`, `Animator`, `TeleportPlayer`, `PerformTeleport`, `player.position =`, or `Player.transform.position +=`.
- Pack-layout scan found no non-pack-1 `StructLayout` in `Assets/_Project/Scripts/Animation/Locomotion`.
- Status remains PENDING VERIFICATION because Unity/Profiler/GCMonitor evidence is absent and Core build is blocked outside this domain.
## Loop 8 Registry Hygiene and Delegate Purge

What was wrong:
- `ProceduralLadderClimbRuntime` still created a hidden persistent root with `DontDestroyOnLoad` and self-registered through `Awake()`.
- `ClimbableLadder` still exposed `UnityEvent` climb hooks and invoked `OnClimbStart`, creating a duplicate managed delegate path beside the typed climb signal lanes.

What was done:
- Removed `DontDestroyOnLoad`; the generated ladder runtime is now scene-local.
- Deleted `Awake()` self-registration and moved registry ownership to `OnEnable`/`OnDisable`.
- Added a cold-order justification comment for the runtime `DefaultExecutionOrder`.
- Removed ladder adapter UnityEvent fields/import/invocation plus obsolete transition/player-tag serialized fields.
- Re-ran static scans for teleport markers, DDOL, UnityEvent, string.Format, Debug.Log, Animator, private/native allocations, EventBus, and H8Memory allocation in the ladder-owned path.

Cinematic cheats used:
- No new simulation. The low-tier Dear Lie remains midpoint elbow placement plus absolute camera lerp; high tier keeps the exact `math.acos` two-bone solve.
- Visual overkill remains delegated to typed-lane consumers; ladder IK owns embodiment, not visor salt/silt/hull shaders.

Exact microseconds saved:
- Runtime steady-state: 0 us. This pass removes lifecycle and delegate risk, not per-frame math.
- Interaction start: estimated 0-2 us saved by deleting `UnityEvent` invocation; profiler proof absent.
- Build validation: `dotnet restore Hecton8.Core.csproj` succeeded. Latest Core build fails on unrelated `RepairTool.cs(1036,52): CS0165` and `World/SargassumMicroFaunaBoids.cs` CS0103 vault/native-field errors; no ladder symbols reported. Assembly restore/build attempt timed out after 306 seconds.

## Loop 9 Teleport API Name Purge

What was wrong:
- `ClimbableLadder` still exposed public `TeleportToExit` and `TeleportToEntry` methods even though the implementation had become procedural.
- That preserved the old teleport contract in the source surface and kept a false-positive debt marker in the ladder-owned path.

What was done:
- Replaced `TeleportToExit` with `RequestClimbToExit`.
- Replaced `TeleportToEntry` with `RequestClimbToEntry`.

## Loop 10 - Adapter Bloat Cleanup + Core Build Green
What was wrong:
- `ClimbableLadder` still contained corrupted non-ASCII banner comments, empty hover comments, and an unused `Hecton8.Audio` namespace after the teleport/delegate purge.
- A platform-layout scan of touched signal infrastructure still found two `[StructLayout(LayoutKind.Sequential)]` declarations without `Pack = 1`.
- First `--no-restore` Core build attempt failed with NETSDK1004 because `Temp/obj/Hecton8.Core/project.assets.json` was missing.

What was done:
- Rewrote `Assets/_Project/Scripts/Gameplay/ClimbableLadder.cs` as a compact ASCII-only adapter with the same procedural climb request, localization cache, collider setup, audio call, editor gizmos, and request API.
- Added `Pack = 1` to `SpscSignalRingBuffer<T>` and `CombatDamageSignalAupShiftTransformer` in `GlobalSignals.cs`.
- Re-ran debt, ASCII, layout, and diff whitespace scans. Debt/layout/ASCII scans returned no hits; `git diff --check` reported only LF-to-CRLF warnings.
- Ran `dotnet restore Hecton8.Core.csproj`, then `dotnet build Hecton8.Core.csproj --no-restore -nodeReuse:false -v:minimal`.

Cinematic Cheats used:
- No new visual fake was added. Existing ladder tiering remains: low-tier camera slide + midpoint elbow Dear Lie; high-tier exact rung hand lock and VR grip pull.

Exact Microseconds saved:
- Runtime: 0 us from the adapter cleanup and layout annotation.
- Existing low-tier IK saving remains the prior estimate: roughly 7 us saved versus the high-tier two-elbow `math.acos` path. No profiler capture was run, so this remains an estimate, not measured proof.

Validation:
- `Hecton8.Core -> C:\hades\Hecton8\Temp\bin\Debug\Hecton8.Core.dll`
- Build succeeded: 0 warnings, 0 errors, 4.21 s.
- Runtime status remains pending Unity/editor/profiler verification.

## Loop 11 - Owner Sentinel + Reentry Hardening
What was wrong:
- Ladder DataVault buffers and the scheduled IK job used `SystemID.GameplayPlayer`, which made memory/job attribution less precise than the Animation/Locomotion ownership implied by the task.
- Active climb requests could re-enter `TryBeginClimbInstance` and reset state while a climb or solve job was in progress.
- Low-tier camera slide was not applied when the player movement force sink existed, even though the stricter XML requires linear camera interpolation along the ladder vector.

What was done:
- Added `SystemID.AnimationLocomotion = 150`.
- Routed ladder DataVault handles and `H8Memory.RegisterActiveJob` through `OwnerSystemId = SystemID.AnimationLocomotion`.
- Added an active/pending/scheduled reject guard before mutating a climb request.
- Applied the low-tier camera slide after queueing movement-sink velocity, preserving FastNlerp stabilization.
- Re-ran ladder debt scans, `Pack = 1` layout scan, shader/compute/IO scan, diff whitespace check, and filtered Core build output.

Cinematic Cheats used:
- Preserved the existing low-tier camera-slide Dear Lie and midpoint elbow fake; no expensive visual-system ownership was added from Animation/Locomotion.

Exact Microseconds saved:
- Owner sentinel and reentry guard: 0 us steady-state.
- Low-tier slide branch: same prior estimated cost, roughly 2 us/frame only while low-tier slide is active. No measured profiler evidence.

Validation:
- Ladder debt/layout scans: no forbidden `Update`, coroutine, `string.Format`, `Debug.Log`, `UnityEvent`, `EventBus`, private/native persistent allocation, teleport marker, or missing `Pack = 1` marker in the ladder-owned path.
- Latest `dotnet build Hecton8.Core.csproj --no-restore -nodeReuse:false -v:q` is blocked by external `World/EcosystemDirector.cs` CS1612 errors. Filtered output contains no `LadderClimb`, `ProceduralLadder`, `ClimbableLadder`, `AnimationLocomotion`, or `OwnerSystemId` errors.

## Loop 12 - Explicit Registry Slot
What was wrong:
- `ProceduralLadderClimbRuntime` registered into `GlobalRegistry`, but its type still resolved to `GlobalRegistryServiceSlot.Unknown`.
- Unknown service-slot ownership undermined the `SystemID.AnimationLocomotion` owner attribution added in Loop 11.

What was done:
- Added `GlobalRegistryServiceSlot.ProceduralLadderClimbRuntime = 172`.
- Appended the service slot name in `GlobalRegistry`.
- Mapped that slot to `SystemID.AnimationLocomotion` in `ResolveMemoryOwner`.
- Added `ProceduralLadderClimbRuntime` to `ResolveServiceSlotCold`.

Cinematic Cheats used:
- None added in this registry patch. Existing low-tier camera slide and midpoint elbow fake remain the ladder Dear Lie path.

Exact Microseconds saved:
- 0 us runtime. This is cold registry diagnostics/leak-attribution correctness.

Validation:
- Debt/layout scans remain clean for ladder-owned files.
- `dotnet restore Hecton8.Core.csproj` succeeded.
- Latest filtered Core build wall is external `SubmarineFluidDynamics.cs` syntax errors; no ladder/runtime/registry-owner symbols are present.

## Loop 13 - Runtime NativeArray View Eviction
What was wrong:
- `ProceduralLadderClimbRuntime` did not own native arrays, but its helper signatures still exposed `out NativeArray<T>` views.
- That made static audit output ambiguous under the DataVault sovereignty mandate.

What was done:
- Added packed `LadderClimbIkVaultViews` in `LadderClimbIkJobs.cs`.
- Refactored output read, ladder AUP write/read, solve scheduling, and blackbox dump to use the vault-view packet.
- Removed all `NativeArray<T>` declarations from `ProceduralLadderClimbRuntime.cs`.
- Re-ran debt/layout/build-wall scans.

Cinematic Cheats used:
- No new visual fake. The existing low-tier midpoint elbow and absolute camera slide remain the toaster path.

Exact Microseconds saved:
- 0 us intended; this is data-ownership clarity over the same DataVault views.

Validation:
- `ProceduralLadderClimbRuntime.cs` has zero `NativeArray<T>` matches.
- `NativeArray<T>` remains only in `LadderClimbIkJobs.cs` vault-view and Burst job fields.
- Missing `Pack = 1` scan returned no hits for the ladder/touched signal path.
- Latest filtered Core build wall is external UI compass and World ecosystem debt; no ladder/runtime/registry-owner symbols are present.
- Confirmed there are no live source references to the old method names outside a deprecated external description bundle.
- Re-ran source scans for teleport, UnityEvent, DDOL, string formatting, Debug logging, coroutine, Animator, EventBus, private/native allocations, and H8Memory allocation markers in the ladder-owned path.

Cinematic cheats used:
- No new simulation. This pass only removes an API lie; low-tier still uses the Dear Lie midpoint elbow and camera slide, high tier keeps exact rung hand lock and `math.acos`.

Exact microseconds saved:
- Runtime: 0 us. Method rename does not change the execution path.
- Maintenance/debug savings only: prevents future agents from binding against a teleport-named climb API.
- Build validation: latest Core build fails on unrelated `Assets/_Project/Scripts/Ecosystem/EcosystemRuntimeInstaller.cs(1,18): CS0234` missing `Hecton8.AI.Ecosystem`; no ladder symbols reported.

## Loop 14 - Signal Semantics and Ordered Blackbox
What was wrong:
- Finished ladder climbs published an inactive packet with `StateClimbing`, which made latest-state consumers cache a climb state after the climb ended.
- Stationary ladder frames published neutral `PlayerStressSignal`/`PhysiologyStateSignal` packets, which could overwrite a more meaningful latest stress producer.
- Blackbox dump order followed raw ring indices instead of cursor-ordered oldest-to-newest telemetry after wrap.

What was done:
- Added `PlayerStateSignal.StateNone = 0` and changed finished climb shutdown packets to publish `StateNone`.
- Kept slip terminal packets as `StateClimbing + FlagClimbing + FlagLadderSlip` so downstream systems can distinguish a drop from a clean finish.
- Added active/climbing flags to non-neutral climb physiology and stress packets.
- Suppressed neutral climb stress spam unless a slip is pending.
- Changed telemetry cursor wrap and dump export to use the actual capped ring capacity and write oldest-to-newest entries.

Cinematic Cheats used:
- Existing low-tier Dear Lie remains: camera slide plus midpoint elbow fake instead of full `math.acos`.
- No new physical simulation was added. Saved lane traffic is reserved for richer high-tier haptics/heartbeat response, not extra gameplay truth.

Exact Microseconds saved:
- Stationary ladder frames avoid up to two neutral typed signal publishes, estimated 2-4 us when the player is hanging without movement.
- State packet branch cost is estimated 0-1 us on publication only.
- Ordered blackbox export is cold path only, 0 us hot-path claim.

Validation:
- Debt scan found no forbidden `Update`, coroutine, `string.Format`, `Debug.Log`, `UnityEvent`, `EventBus`, private/native persistent allocation, teleport marker, DDOL marker, or `position +=` in the ladder-owned path.
- Missing `Pack = 1` scan returned no hits for ladder-owned files, `ClimbableLadder`, or touched `GlobalSignals`.
- `ProceduralLadderClimbRuntime.cs` still has zero `NativeArray<T>` declarations; `NativeArray<T>` remains only in `LadderClimbIkJobs.cs` vault-view/job fields.
- `git diff --check` reports only existing LF-to-CRLF warnings on touched files.
- Latest `dotnet build Hecton8.Core.csproj --no-restore -nodeReuse:false -v:q` is blocked by external `Core/Determinism/LockstepStateValidator.cs` missing lockstep/glitch constants. Filtered output contains no ladder/runtime/signal-owner symbols.

## Loop 15 - VR Embodiment Sign and Rotation Polish
What was wrong:
- VR hand-delta climb semantics were directionally ambiguous. Embodied ladder climbing requires the player to pull hands down to move the body/world up, not feed the controller delta as same-direction player progress.
- No-grip universal input packets could leave stale pending grip pull until the next resolver pass.
- Clean VR climb finish still shared the non-VR endpoint root-rotation snap path.

What was done:
- `SubmitUniversalInputState` now clears pending grip pull and grip mask when `UniversalInputStateSignal(Grip)` is absent.
- `ResolveProgressDelta` now inverts the consumed grip pull so controller movement along the ladder maps to opposite world/player progress.
- `StopClimb` now skips endpoint root-rotation snaps for VR grip mode, leaving headset/controller orientation authority intact.
- Re-ran targeted debt/layout scans and filtered Core build isolation.

Cinematic Cheats used:
- Low/toaster path remains the Dear Lie: camera slide plus midpoint elbow fake instead of full two-elbow `math.acos`.
- High/VR path keeps exact rung hand lock and grip-pull embodiment; no new physical simulation was added.

Exact Microseconds saved:
- 0 us intended steady-state change. This is sign/branch correctness over existing scalar work.
- Existing low-tier saving remains estimated at roughly 7 us versus high-tier `math.acos` elbows. No profiler capture was run, so this is still estimate, not measured proof.

Validation:
- Debt scan found no forbidden `Update`, coroutine, `string.Format`, `Debug.Log`, `UnityEvent`, `EventBus`, private/native persistent allocation, teleport marker, DDOL marker, or `position +=` in the ladder-owned path.
- Missing `Pack = 1` scan returned no hits for ladder-owned files, `ClimbableLadder`, or touched `GlobalSignals`.
- Latest filtered `dotnet build Hecton8.Core.csproj --no-restore -nodeReuse:false -v:q` is blocked by external `UI/Navigation/DiegeticGyroCompassRuntime.cs` DTO drift and `Core/SystemDispatcher.cs` missing dispatcher-blackbox helpers. Filtered output contains no ladder/runtime/signal-owner symbols.
- Unity editor, Play Mode, profiler, GC monitor, and platform builds were not run.

## Loop 16 - Typed Input Lane Grip Mask Hardening
What was wrong:
- The VR grip path still had a stale mask hazard. Core defines `PlayerInputAction.Interact = 1 << 1` and `SecondaryFire = 1 << 3`, while the old ladder default was `1 << 6`.
- XR grip in `InputDispatcher.ResolveXRToolActionBitsAndPublishSignal` publishes `SecondaryFire | Interact`, so scenes serialized with the old mask could miss real grip packets.
- Pending external hand deltas needed to be cleared by the authoritative typed input snapshot, not only by callers that explicitly submit hand deltas.

What was done:
- Added `LegacySerializedGripActionMask = 1u << 6`.
- Kept the serialized `universalGripActionMask`, but `ResolveGripActionMask()` now maps zero or legacy values to `PlayerInputAction.Interact | PlayerInputAction.SecondaryFire` and ORs custom masks with those Core grip bits.
- Added `ConsumeInputStateSignals()` on the VR hot tick. It reads `SignalBus<InputStateSignal>.GetFrameSnapshot()` as `ReadOnlySpan<InputStateSignal>`, tracks `InputState.Sequence`, and clears pending grip pull when the latest input packet has no grip.
- Re-ran source proof against `PlayerInputState.cs` and `InputDispatcher.cs`, ladder debt scans, layout scans, runtime NativeArray scan, diff whitespace check, and filtered Core build.

Cinematic Cheats used:
- Low/toaster path remains unchanged: camera slide plus midpoint elbow fake instead of full two-elbow `math.acos`.
- High/VR path now spends a small bounded typed-lane scan to preserve physical pull truth instead of inventing another simulation or signal.

Exact Microseconds saved:
- No new savings claimed. The typed input scan costs an estimated 1-2 us/frame only in VR grip mode over the bounded 64-entry lane snapshot.
- Legacy mask normalization is scalar branch work, estimated below 1 us. No profiler capture was run, so these remain estimates.

Validation:
- Forbidden ladder-domain scan returned no matches for `Update`, coroutine, `string.Format`, `Debug.Log`, `UnityEvent`, `EventBus`, private/native persistent allocation, teleport marker, DDOL marker, or `position +=`.
- Missing `Pack = 1` scan returned no hits for ladder-owned files, `ClimbableLadder`, or touched `GlobalSignals`.
- `ProceduralLadderClimbRuntime.cs` still has zero `NativeArray<T>` declarations; `NativeArray<T>` remains only in `LadderClimbIkJobs.cs` vault-view/job fields.
- `git diff --check` reports only existing LF-to-CRLF warnings on touched files.
- Latest filtered `dotnet build Hecton8.Core.csproj --no-restore -nodeReuse:false -v:q` is blocked by external `Assets/_Project/Scripts/TetherInstance.cs` missing `IsFrameCooldownActive`. Filtered output contains no ladder/runtime/input-lane symbols.
- Unity editor, Play Mode, profiler, GC monitor, and platform builds were not run.

## Loop 17 - Grip Truth and Blackbox Retained Count
What was wrong:
- Grip truth and pull distance were coupled. If `SignalBus<InputStateSignal>` showed grip held but no hand delta was submitted that frame, `_lastResolvedGripMask` became zero and the look-down release-slip branch could fire while the player was still holding grip.
- Direct `SubmitGripPullDelta` callers could report a grip mask without updating the typed grip-held truth, or pass zero mask without clearing stale pull state.
- Short-session blackbox dumps wrote the full 300-entry ring even when fewer real samples existed, which could put cleared/uninitialized entries ahead of useful crash evidence.

What was done:
- Added `_currentInputGripHeld` to track held/released truth separately from `_pendingGripPullMeters`.
- Updated `SubmitUniversalInputState`, `SubmitGripPullDelta`, and `ConsumeInputStateSignals` so held zero-delta grip blocks release-slip, while zero-mask input clears grip and pull state.
- Added two vault cursor indices: next-write and retained-count.
- Changed `LadderClimbIkSolveJob.WriteTelemetry()` to advance both cursor values in DataVault.
- Changed `DumpBlackBox()` to write only retained samples and preserve oldest-to-newest order after wrap.
- Hardened `EnsureVaultBuffers()` so an already-created one-int telemetry cursor handle is grown to the two-int lane instead of blocking solve capacity.
- Re-ran forbidden pattern scans, layout scan, NativeArray placement scan, diff whitespace check, and filtered Core build.

Cinematic Cheats used:
- Low/toaster path remains unchanged: camera slide plus midpoint elbow fake instead of full two-elbow `math.acos`.
- High/VR path remains physical: grip-held truth only prevents false slip; climb progress still requires real hand pull delta.

Exact Microseconds saved:
- No savings claimed. Grip-held truth adds one bool branch in VR mode, estimated below 1 us/frame.
- Retained blackbox count adds one int read/write in the telemetry job, estimated below 1 us/frame.
- The existing low-tier midpoint elbow path still avoids the high-tier `math.acos` cost; no profiler capture was run.

Validation:
- Forbidden ladder-domain scan returned no matches for `Update`, coroutine, `string.Format`, `Debug.Log`, `UnityEvent`, `EventBus`, private/native persistent allocation, teleport marker, DDOL marker, or `position +=`.
- Missing `Pack = 1` scan returned no hits for ladder-owned files, `ClimbableLadder`, or touched `GlobalSignals`.
- `ProceduralLadderClimbRuntime.cs` still has zero `NativeArray<T>` declarations; `NativeArray<T>` remains only in `LadderClimbIkJobs.cs` vault-view/job fields.
- `git diff --check` reports only existing LF-to-CRLF warnings on touched files.
- Latest `dotnet build Hecton8.Core.csproj --no-restore -nodeReuse:false -v:q` returned `CORE_BUILD_EXIT:0`.
- Unity editor, Play Mode, profiler, GC monitor, and platform builds were not run.
## Loop 18 - Player State AUP Truth
What was wrong:
- `PlayerStateSignal.PositionAup` published the ladder base AUP, not the current player climb AUP.
- Downstream HUD, physiology, diagnostics, or haptic consumers could anchor effects at the ladder entry while `Intensity01` advanced up the rungs.

What was done:
- Added `ResolveCurrentClimbAup(in ladderAup)` in `ProceduralLadderClimbRuntime`.
- `PublishClimbState` now derives current climb AUP from the vault ladder base plus normalized ladder-up progress using `double3` and `AbsoluteUniversePosition.OffsetMeters`.
- Re-ran focused debt, layout, NativeArray, and Core build validation after the patch.

Cinematic Cheats used:
- Low/toaster path remains the camera-slide and midpoint elbow Dear Lie; no new expensive simulation was added.
- High/VR path keeps real grip-pull progress and exact rung signal truth without adding a duplicate lane.

Exact Microseconds saved:
- No savings claimed. The fix adds one `double3` multiply and one AUP offset conversion per climb-state publish, estimated below 1 us/event. No profiler capture was run.

Validation:
- Forbidden ladder-domain scan clean: no `Update`, coroutine, managed delegate/event, `Debug.Log`, teleport marker, transform-position increment, or local runtime NativeArray owner pattern in the checked ladder path.
- Missing `[StructLayout(Pack=1)]` scan clean for ladder-owned structs.
- Runtime NativeArray scan clean: `ProceduralLadderClimbRuntime` has zero `NativeArray<T>` declarations; view/job fields remain in `LadderClimbIkJobs.cs`.
- `dotnet build Hecton8.Core.csproj --no-restore -nodeReuse:false -v:q` returned `CORE_BUILD_EXIT:0`.
- `git diff --check` reported LF-to-CRLF warnings only.
- Unity editor, Play Mode, profiler, Quest/Android, Metal/Mac, and Steam Deck runtime validation were not run in this shell session.

## Loop 19 - Burst Job Layout Closure
What was wrong:
- The ARM64 layout audit showed `LadderClimbIkInput`, `LadderClimbIkOutput`, `LadderClimbTelemetryEntry`, and `LadderClimbIkVaultViews` were packed, but `LadderClimbIkSolveJob` itself still had implicit struct layout.
- That is a static compliance gap for the Quest/Android instruction, even though the job wrapper is not a persisted save/network packet.

What was done:
- Added `[StructLayout(LayoutKind.Sequential, Pack = 1)]` to `LadderClimbIkSolveJob`.
- Re-ran the focused debt scan against the actual `Assets/_Project/Scripts/Gameplay/ClimbableLadder.cs` path instead of the stale traversal path.

Cinematic Cheats used:
- None added. Low-tier midpoint IK and camera-slide Dear Lie remain unchanged.

Exact Microseconds saved:
- 0 us claimed. This is explicit layout metadata only; no hot-path math or allocation behavior changed.

Validation:
- Forbidden ladder-domain scan returned no matches against `Assets/_Project/Scripts/Animation/Locomotion` and `Assets/_Project/Scripts/Gameplay/ClimbableLadder.cs`.
- `LadderClimbIkSolveJob` now has explicit `StructLayout(LayoutKind.Sequential, Pack = 1)`.
- `dotnet build Hecton8.Core.csproj --no-restore -nodeReuse:false -v:q` returned `CORE_BUILD_EXIT:0`.
- Unity editor, Play Mode, profiler, Quest/Android, Metal/Mac, and Steam Deck runtime validation were not run in this shell session.

## Loop 20 - Non-Blocking Ladder Job Drain
What was wrong:
- `LateFrameTick()` called `_solveHandle.Complete()` immediately whenever a solve was scheduled.
- If the worker job was not finished yet, the player lane could block on a same-frame drain. That is a hitch vector on i3/MX350 and Steam Deck under worker-thread pressure.

What was done:
- Added `_solveHandle.IsCompleted` gating before `Complete()`.
- Finished solves are still drained and applied in late frame; unfinished solves stay scheduled and are sampled on the next late-frame pass.
- Cold teardown and new climb setup still force completion because those are ownership-boundary synchronization points.

Cinematic Cheats used:
- None added. The change preserves the existing low-tier camera-slide/midpoint-elbow Dear Lie and the high-tier VR grip-pull exact rung solve.

Exact Microseconds saved:
- No measured microseconds claimed. Steady-state cost is 0 us when the job is already complete; under load this avoids waiting for the remaining worker time, but no profiler capture was run.

Validation:
- Forbidden ladder-domain scan returned no matches against `Assets/_Project/Scripts/Animation/Locomotion` and `Assets/_Project/Scripts/Gameplay/ClimbableLadder.cs`.
- Layout scan still shows packed ladder structs and packed `LadderClimbIkSolveJob`.
- `dotnet build Hecton8.Core.csproj --no-restore -nodeReuse:false -v:q` returned `CORE_BUILD_EXIT:1` because dirty external `Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs` does not implement dirty external `IScalabilityChangedEventListener.OnScalabilityChanged(in ScalabilityChangedEvent)`.
- Filtered build output contains no ladder symbols.
- Unity editor, Play Mode, profiler, Quest/Android, Metal/Mac, and Steam Deck runtime validation were not run in this shell session.

## Loop 21 - Signal and Haptic Coalescing
What was wrong:
- `PlayerStateSignal` could be published twice in the same frame with identical climb state, flags, and progress.
- When both hands locked to new rung indices in one solve, the runtime emitted two haptic packets in the same frame.

What was done:
- Added `_hasPublishedClimbState`, `_lastPublishedClimbFrame`, `_lastPublishedClimbState`, `_lastPublishedClimbFlags`, and `_lastPublishedClimbProgressMillimeters`.
- `PublishClimbState` now coalesces identical same-frame packets while still allowing slip, finish, flag, and progress changes through.
- `EmitRungContactHaptics` now emits one coalesced `HapticRequest`, with a stronger pulse when both hands lock simultaneously.

Cinematic Cheats used:
- No new simulation. This preserves the low-tier camera-slide/midpoint-elbow Dear Lie and spends less signal bandwidth on repeated presentation packets.

Exact Microseconds saved:
- No profiler-backed microseconds claimed. Static reduction can avoid one duplicate player-state publish on same-frame solve drain and one duplicate haptic publish when both hands lock in the same output.

Validation:
- Forbidden ladder-domain scan returned no matches against `Assets/_Project/Scripts/Animation/Locomotion` and `Assets/_Project/Scripts/Gameplay/ClimbableLadder.cs`.
- Layout scan still shows packed ladder structs and packed `LadderClimbIkSolveJob`.
- `dotnet build Hecton8.Core.csproj --no-restore -nodeReuse:false -v:q` returned `CORE_BUILD_EXIT:1` because external `HectonPlayerMotor` call sites need `IDataVault`, `EquipmentInteractionContracts` has a uint-to-ushort mismatch, and Tether call sites no longer match Verlet APIs.
- Filtered build output contains no ladder symbols.
- Unity editor, Play Mode, profiler, Quest/Android, Metal/Mac, and Steam Deck runtime validation were not run in this shell session.

## Loop 22 - Cold Blackbox Span Writer
What was wrong:
- `DumpBlackBox()` still built a project-root path and wrapped the crash dump stream with `BinaryWriter`.
- A NaN/crash dump should preserve evidence with minimal extra managed work and must not throw back into the ladder runtime while the system is already degraded.

What was done:
- Added `BlackBoxDumpDirectory`, `BlackBoxDumpPath`, and `BlackBoxDumpEntryBytes` constants.
- Pre-created `Docs/AgentLogs` during `OnEnable` through `PrepareBlackBoxDumpDirectoryCold`.
- Replaced `BinaryWriter` with `BinaryPrimitives` plus a fixed 8-byte header and 85-byte stackalloc telemetry record.
- Preserved the existing payload order: capacity, retained count, then retained entries oldest-to-newest.
- Wrapped the dump write in a cold catch block so export failure does not become a second runtime fault.

Cinematic Cheats used:
- No new visual simulation. Low tier still uses camera slide and midpoint elbow Dear Lie; high/VR keeps exact rung targets, grip-pull embodiment, and haptic rung locks.

Exact Microseconds saved:
- No measured microseconds claimed. Hot path remains unchanged. Cold fault-path pressure is reduced by removing `BinaryWriter`, project-root `DirectoryInfo`, and per-dump `Path.Combine`.

Validation:
- `<POLISH_MANDATE>` tag scan returned no batch-level tag.
- `BinaryWriter` / `File.Open` / `ResolveProjectRoot` scan now has no ladder-runtime matches; only the constant dump path remains.
- Forbidden ladder-domain scan returned no matches against `Assets/_Project/Scripts/Animation/Locomotion` and `Assets/_Project/Scripts/Gameplay/ClimbableLadder.cs`.
- `dotnet build Hecton8.Core.csproj --no-restore -nodeReuse:false -v:q` returned `CORE_BUILD_EXIT:0`.
- Unity editor, Play Mode, profiler, Quest/Android, Metal/Mac, and Steam Deck runtime validation were not run in this shell session.

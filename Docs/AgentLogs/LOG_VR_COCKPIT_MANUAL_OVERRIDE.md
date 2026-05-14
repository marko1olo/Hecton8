# LOG_VR_COCKPIT_MANUAL_OVERRIDE

## 2026-05-14 - OpenXR Physical Manual Override Lever

What was wrong: Space prologue manual release was represented as a button-style interaction. VR cockpit affordance needed a physical OpenXR lever that can be grabbed, pulled, latched, haptically ratcheted, and used to trigger prologue completion without a Unity physics joint.

What was done: implemented `OpenXRManualOverrideLever` in `Hecton8.UI.VR`, isolated read contracts in `Hecton8.UI.VR.Contracts`, added `ManualOverridePulledSignal` to the typed signal lane, exposed the fixed physical hand receiver registry for the isolated assembly, and wired latch to `PrologueCompleteSignal`.

Cinematic Cheats used: kinematic lever rotation; scalar damped spring resistance; 10-degree haptic ratchet pulses; local-space AUP-safe projection; low-tier IK smoothing reduction; blackbox circular telemetry dump only on NaN/crash.

Exact microseconds saved: no `HingeJoint` physics island estimated 10-80 us avoided in cockpit frames; registry lookup avoids 3-12 us component traversal; reciprocal/rsqrt polish saves about 0.15 us on fallback/projection frames; zero-GC projection avoids managed allocation spikes entirely.

Verification: prompt re-read after task completion; no `HingeJoint` found; no `math.normalize`, managed `foreach`, `.ToArray()`, or `FindObject` in task files. Dot/cross projection self-check verifies 0-degree reference and 90-degree perpendicular pull in editor/development.

Compile status: Unity MCP compile refresh timed out and later reported `no_unity_session`, so `Hecton8.UI.VR.csproj` was not generated and the new UI assembly still needs Unity compile verification. `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` is blocked by unrelated missing assembly references (`Hecton8.Environment.Fluids`, `Hecton8.Audio.Virtualization`, `Hecton8.Physics.CCD`, `Hecton8.Core.Scheduling`, etc.). First build exposed one local signal placement error; fixed by moving `ManualOverridePulledSignal` into `GlobalSignals.cs`. Filtered rebuild reports no remaining manual-override Core signal/registry errors.

## 2026-05-14 - Patience Pass Upgrade

What was wrong: pivot-only grab detection was brittle for a real lever; the latch packet lost pull velocity before publishing; `Hecton8.UI.VR` lacked an explicit `Unity.Jobs` asmdef reference; the first ratchet could fire at 0 degrees.

What was done: grab now accepts pivot or handle proximity while preserving pivot-plane math; latch velocity is captured before zeroing; latch signal uses actual handle local position; first ratchet step seeds without haptic; asmdef now references `Unity.Jobs`.

Cinematic Cheats used: same kinematic lever and scalar spring, now with cleaner tactile gating and better diagnostics.

Exact microseconds saved/spent: +0.2 us only during receiver candidate checks for handle-or-pivot distance; 0 us steady-frame cost; avoided false 0-degree haptic dispatch; preserved latch velocity without extra allocation.

Verification: static task scan remains clean for `HingeJoint`, `math.normalize`, managed `foreach`, `.ToArray()`, `FindObject`, and `GetComponentInParent` except the required interface parameter name `handForward`. Unity-generated `Hecton8.UI.VR.rsp` compile probe reports only stale Core reference errors: missing `ManualOverridePulledSignal` and internal `PhysicalHandReceiverRegistry` in `Hecton8.Core.ref.dll`.

## 2026-05-14 - Stale Sample / Dispose Hardening

What was wrong: the lever could remain grabbed against stale hand pose data if the grip stayed held but the physical receiver stopped providing hand samples. Native cleanup briefly regressed to synchronous array disposal, which violates the active native-memory teardown rule.

What was done: VR grab now releases after more than 3 frames without a fresh hand sample and holds the current target during short 2-3 frame gaps. Persistent native arrays now use tracked deferred `Dispose(JobHandle)` after sentinel unregister, store `_disposeHandle`, and clear array fields immediately.

Cinematic Cheats used: still no joint or physics solve; stale tracking uses a deterministic hold/release gate instead of attempting to simulate hand inertia.

Exact microseconds saved/spent: +0.05 us branch per tick for stale-sample safety; 0 us steady cleanup cost; teardown avoids main-thread native free and keeps disposal ownership visible.

Verification: `git diff --check` passed. Static scan remains clean for `HingeJoint`, `math.normalize`, managed `foreach`, `.ToArray()`, `FindObject`, `GetComponentInParent`, `new List`, and `new Dictionary`. Direct Core rsp compile fails before task code on unrelated missing `Audio.Virtualization`, `AI.Cognition`, `IOutpostGenerationService`, `IPrologueSequenceService`, and `WorldOreTypeIds` symbols, so Unity/Core verification remains dependency-blocked.

## 2026-05-14 - Registry Rebind Hardening

What was wrong: the lever cached Input and dispatcher registration only during `OnEnable`. A bootstrap or hot-swap rebound could leave the VR lever reading a no-op input service or believing it was registered to a dispatcher that had been replaced.

What was done: implemented `IGlobalRegistryHotSwapListener`; Input rebound now refreshes the cached `IInputService`, and Dispatcher rebound clears the local registration flag before re-registering against the new dispatcher.

Cinematic Cheats used: no new simulation; this is service-binding hardening for the existing kinematic lever fake.

Exact microseconds saved/spent: 0 us steady-state cost; avoids per-frame registry polling; cold rebound only.

Verification: source diff reviewed; pending compiler proof remains blocked by stale Core references and unrelated global compile wall.

## 2026-05-14 - Haptic Channel / Cold Math Hardening

What was wrong: ratchet and latch tool-haptic commands were broadcast to both controllers, which weakens VR tactile localization. `OnDestroy` depended on `OnDisable` for hot-swap listener cleanup, and `NormalizeOr` still had a cold `.normalized` fallback.

What was done: added left/right haptic motor masks; ratchet commands now use the grabbed hand, latch commands use the latched signal hand, and non-VR/unknown keeps both motors. Added idempotent hot-swap unregister in `OnDestroy`. Replaced `.normalized` fallback with guarded `math.rsqrt`.

Cinematic Cheats used: localized gear-click pulses instead of continuous mechanical vibration; scalar kinematic lever remains unchanged.

Exact microseconds saved/spent: +0.02 us branch only on haptic dispatch frames; 0 us steady frame; removed hidden cold normalization sqrt/division path.

Verification: `git diff --check` passed with only CRLF warning. Static scan found no `HingeJoint`, `math.normalize`, `.normalized`, managed `foreach`, `.ToArray()`, `FindObject`, `GetComponentInParent`, `new List`, `new Dictionary`, or `.Dispose()`. Direct Unity Roslyn probe for `Hecton8.UI.VR.rsp` still reports only stale Core reference symptoms: missing `ManualOverridePulledSignal` and inaccessible `PhysicalHandReceiverRegistry`. Direct Core rsp probe still timed out behind the unrelated global Core/MSBuild wall; active parallel build processes were left alone.

## 2026-05-14 - Nested Anchor / Ratchet Reacquire Hardening

What was wrong: handle reference math assumed the handle anchor lived in lever-root local space, which breaks with nested cockpit art rigs. Ratchet state could survive release and produce a false click on the next grab. Blackbox telemetry used a modulo wrap in the 60Hz path. Hot-swap listener registration was not locally tracked.

What was done: angular reference now converts the handle world position into lever-root local space. Idle unheld lever resets `_lastRatchetStep`. Blackbox index wraps with increment/compare. Hot-swap registration now has a local boolean. Receiver registration is play-mode-only. Native allocation can recover from a deferred-disposal gap before ticking.

Cinematic Cheats used: still kinematic scalar lever, no `HingeJoint`, no force solver; hierarchy-safe handle basis allows richer visible lever rigs without more simulation.

Exact microseconds saved/spent: blackbox modulo removal saves about 0.01 us per tick on weak CPUs; idle ratchet reset is one branch only while unheld; no new managed allocations.

Verification: `git diff --check` passed with only CRLF warnings. Static scan found no `HingeJoint`, `math.normalize`, `.normalized`, managed `foreach`, `.ToArray()`, `FindObject`, `GetComponentInParent`, `new List`, `new Dictionary`, `.Dispose()`, `Time.deltaTime`, `StartCoroutine`, or `Update(` in the lever source. Direct Unity Roslyn probe for `Hecton8.UI.VR.rsp` still reports only stale Core reference symptoms: missing `ManualOverridePulledSignal` and inaccessible `PhysicalHandReceiverRegistry`.

## 2026-05-14 - Projection Singularity Hardening

What was wrong: a hand exactly on the lever pivot/rotation axis collapses the projected vector. The old solver treated that invalid sample as `minAngleDegrees`, so a tracking edge case could snap the manual override toward closed.

What was done: added `TrySolveAngleFromHand`; valid samples still use the same dot/cross `atan2` math, while degenerate samples hold the current lever angle and set a blackbox telemetry bit. The editor/development verifier now checks both 0/90-degree pulls and explicit singular rejection.

Cinematic Cheats used: deterministic kinematic hold instead of a physics joint, force solver, or secondary hand simulation. The player sees stable metal, not math noise.

Exact microseconds saved/spent: +0.03 us branch on valid VR solve frames; avoided false spring recovery and unnecessary visual correction on singular frames; 0 B/frame added.

Verification: `CURRENT_BATCH.md` no longer contains `VR_COCKPIT_MANUAL_OVERRIDE`, so the batch drift is documented in status/rationale. `git diff --check` passed with only CRLF warning. Static scan found no `HingeJoint`, `math.normalize`, `.normalized`, managed `foreach`, `.ToArray`, `FindObject`, `GetComponentInParent`, `StartCoroutine`, or Unity `Update`. Direct Unity Roslyn probe for `Hecton8.UI.VR.rsp` exited 0 after this pass. Direct Core rsp probe timed out after 120s without an error stream; active shared MSBuild jobs were left alone. Full Unity/player compile still remains blocked by the unrelated global Core dependency wall. STATUS remains `PENDING VERIFICATION`.

## 2026-05-14 - Hand-Side Fallback Hardening

What was wrong: hand-side helper logic treated any non-left enum byte as right. Current `PhysicalHandSide` only declares left/right, but collider-driven interaction data should not convert future or corrupted values into false right-hand telemetry.

What was done: `ResolveSignalHandSide` now returns `HandUnknown` for invalid values, and `ResolveHapticMotorMask` falls back to both motors instead of right-only.

Cinematic Cheats used: fail-soft haptic broadening; bad hand identity becomes centered tactile feedback instead of a misleading one-sided event.

Exact microseconds saved/spent: +0.01 us only on haptic/latch helper calls; 0 B/frame; no logging or exception path.

Verification: `git diff --check` passed with CRLF warning only. Static ban scan returned no forbidden lever patterns. The first direct UI rsp probe hit transient `CS0006` while `Hecton8.Core.ref.dll` was being regenerated by shared builds; the file reappeared, and the repeated Unity Roslyn probe returned `EXIT=0`. STATUS remains `PENDING VERIFICATION`.

## 2026-05-14 - Hot-Path Allocation Closure

What was wrong: the defensive native recovery helper made `new NativeArray` reachable from `Tick` when `_nativeAllocated` was false. Rare is still a defect under the zero-GC mandate.

What was done: `Tick` now only gates on `_nativeAllocated`. Native recovery is lifecycle-only through `EnsureNativeStateForLifecycle()`, and dispatcher registration refuses to tick a lever without native state.

Cinematic Cheats used: none; this is allocator hygiene for the existing kinematic lever fake.

Exact microseconds saved/spent: removes a rare native allocation/reinit spike from the hot-path call graph; steady cost unchanged at one branch; 0 B/frame proof strengthened.

Verification: source scan shows `AllocateNativeState()` is now reachable from `Awake` and lifecycle helper only, not from `Tick`. `git diff --check` passed with CRLF warning only. Static ban scan returned no forbidden lever patterns. Direct Unity Roslyn probe for `Hecton8.UI.VR.rsp` returned `EXIT=0`. STATUS remains `PENDING VERIFICATION`.

## 2026-05-14 - Dispatcher Rebind Allocation Recovery

What was wrong: after closing the hot-path allocation path, dispatcher replacement could attempt registration while native state was still absent after a deferred-disposal gap. `TryRegisterTick()` would correctly refuse, but the active lever could stay inert until another lifecycle event.

What was done: dispatcher hotswap now calls `EnsureNativeStateForLifecycle()` before `TryRegisterTick()`. The recovery remains cold-path service rebinding; `Tick` still returns immediately when native state is missing and never allocates.

Cinematic Cheats used: none; this is lifecycle hardening for the existing kinematic cockpit lever fake.

Exact microseconds saved/spent: 0 us steady-state; cold hotswap only. Preserves 0 B/frame and avoids a dead override lever during service replacement.

Verification: direct Unity Roslyn probe for `Hecton8.UI.VR.rsp` returned `EXIT=0`. Static ban scan returned no forbidden lever patterns. `git diff --check` passed with CRLF warnings only. Source call-graph scan shows allocation remains `Awake`/lifecycle-only, not `Tick`. STATUS remains `PENDING VERIFICATION` because full Unity/player compile is still blocked by the unrelated global Core dependency wall.

## 2026-05-14 - Public Contract Documentation

What was wrong: the lever had a public integration surface but no XML docs for dispatcher timing, physical hand sample acceptance, hotswap behavior, or read-model semantics.

What was done: added public XML documentation to `OpenXRManualOverrideLever`, read-model properties, `Tick`, `TryQueueHandPress`, `OnGlobalRegistryServiceReplaced`, `IManualOverrideLeverReadModel`, and `ManualOverrideLeverContractConstants`.

Cinematic Cheats used: none; documentation only. The runtime remains the same kinematic lever and haptic fake.

Exact microseconds saved/spent: 0 us runtime, 0 B/frame. Compile-time documentation only.

Verification: direct Unity Roslyn probe for `Hecton8.UI.VR.rsp` returned `EXIT=0`. Static ban scan returned no forbidden lever patterns. `git diff --check` passed with CRLF warnings only. STATUS remains `PENDING VERIFICATION` because full Unity/player compile is still blocked by the unrelated global Core dependency wall.

## 2026-05-14 - Local Hand Sample Cache

What was wrong: accepted hand samples were converted to local space for grab distance, then converted again from world space in the solver and blackbox telemetry.

What was done: replaced the stored world hand position with `_lastHandLocalPosition`. VR solve and telemetry now consume the cached local `float3`; `WorldToLocal()` remains only on the physical hand sample acceptance path.

Cinematic Cheats used: no new simulation; this strengthens the existing kinematic local-space fake.

Exact microseconds saved/spent: saves up to two native transform crossings on fresh VR grab frames and one transform crossing on telemetry frames; 0 B/frame.

Verification: direct Unity Roslyn probe for `Hecton8.UI.VR.rsp` returned `EXIT=0`. Static ban scan returned no forbidden lever patterns. Source scan confirms `_lastHandWorldPosition` is gone and `WorldToLocal()` is no longer called by solver or blackbox telemetry. STATUS remains `PENDING VERIFICATION` because full Unity/player compile is still blocked by the unrelated global Core dependency wall.

## 2026-05-14 - XR State Cache

What was wrong: the lever sampled XR active state for branch selection, then sampled it again for telemetry and latch signal flags.

What was done: added `_xrActiveThisFrame`; `Tick` samples XR state once, and VR branch selection, `ManualOverridePulledSignal` flags, and blackbox flags reuse the same value.

Cinematic Cheats used: none; this is hot-path state sampling cleanup for the existing kinematic lever fake.

Exact microseconds saved/spent: saves one XR active-state read per lever frame and one extra read on latch frames; 0 B/frame.

Verification: direct Unity Roslyn probe for `Hecton8.UI.VR.rsp` returned `EXIT=0`. Source scan shows `XRSettings` only at the frame sample and all downstream code uses `_xrActiveThisFrame`. `git diff --check` passed with CRLF warnings only. STATUS remains `PENDING VERIFICATION` because full Unity/player compile is still blocked by the unrelated global Core dependency wall.

## 2026-05-14 - Solver Basis Float Cache

What was wrong: valid VR solve frames converted the same configured axis/reference vectors from `Vector3` to `float3` every time.

What was done: cached `_axisLocalFloat` and `_referenceLocalFloat` in `CacheConfiguration()` and passed them directly into `TrySolveAngleFromHand`.

Cinematic Cheats used: none; this is data-shape cleanup for the existing scalar kinematic lever.

Exact microseconds saved/spent: saves two small struct conversions on fresh VR solve frames; 0 B/frame.

Verification: direct Unity Roslyn probe for `Hecton8.UI.VR.rsp` returned `EXIT=0`. Source scan shows no `ToFloat3(_resolved...)` in the solver path and cached basis fields are assigned only from configuration. STATUS remains `PENDING VERIFICATION` because full Unity/player compile is still blocked by the unrelated global Core dependency wall.

## 2026-05-14 - Frame Stamp Cache

What was wrong: the lever sampled `Time.frameCount` repeatedly while producing same-tick input, haptic, signal, and telemetry payloads.

What was done: added `_frameThisTick`; `Tick` samples frame count once, and all tick-owned payloads use that cached frame. Physical hand receiver acceptance still samples the live frame because it is not owned by the lever tick.

Cinematic Cheats used: none; this is state-sampling cleanup for the existing kinematic lever.

Exact microseconds saved/spent: saves five frame-count reads on ordinary frames and more on latch/ratchet frames; 0 B/frame.

Verification: direct Unity Roslyn probe for `Hecton8.UI.VR.rsp` returned `EXIT=0`. Source scan shows tick-side payloads use `_frameThisTick`; receiver callback still uses `Time.frameCount` by design. STATUS remains `PENDING VERIFICATION` because full Unity/player compile is still blocked by the unrelated global Core dependency wall.

## 2026-05-14 - IK Handle Pose Cache

What was wrong: grabbed-hand IK smoothing read `handleAnchor.position` and `handleAnchor.rotation` through transform properties more than needed.

What was done: `UpdateIkTarget()` now reads handle position/rotation once into local value types and reuses them for snap and smoothing branches.

Cinematic Cheats used: visual-only hand IK smoothing remains a presentation fake; no physics joint or hand force simulation was added.

Exact microseconds saved/spent: saves two transform property reads on smoothed IK frames; 0 B/frame.

Verification: direct Unity Roslyn probe for `Hecton8.UI.VR.rsp` returned `EXIT=0`. Static ban scan returned no forbidden lever patterns. `git diff --check` passed with CRLF warnings only. STATUS remains `PENDING VERIFICATION` because full Unity/player compile is still blocked by the unrelated global Core dependency wall.

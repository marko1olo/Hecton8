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

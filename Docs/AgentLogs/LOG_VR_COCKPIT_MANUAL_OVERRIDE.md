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

## 2026-05-14 - Source Drift Recovery / Re-Hardening

What was wrong: `OpenXRManualOverrideLever.cs` was back at older world-hand solve and tick-side native recovery code while task evidence files described the hardened implementation.

What was done: restored task-owned safeguards by forward patch only: local hand cache, singular projection hold, lifecycle-only native allocation, hotswap allocation recovery, cached XR/frame/basis state, invalid hand-side fallback, public XML docs, and IK handle pose cache. Also made `TryQueueHandPress()` skip handle-anchor transform resolution when pivot distance already accepts the hand.

Cinematic Cheats used: preserved the kinematic local-space lever fake; no joint, no force solver, no direct OpenXR polling.

Exact microseconds saved/spent: restores prior savings from local hand, XR, frame, basis, and IK caches; adds one saved handle transform conversion on pivot-close receiver callbacks; 0 B/frame.

Verification: direct Unity Roslyn probe for `Hecton8.UI.VR.rsp` returned `EXIT=0`. Forbidden-pattern scan returned no matches. Source scan confirms no `_lastHandWorldPosition`, no tick-side `TryEnsureNativeState`, cached frame/XR/basis fields restored, and singular rejection present. STATUS remains `PENDING VERIFICATION` because full Unity/player compile is still blocked by the unrelated global Core dependency wall.

## 2026-05-14 - Finite Containment / Blackbox Guard

What was wrong: the receiver rejected non-finite world hand positions, but local Transform conversion, nested handle anchors, or inspector pivot corruption could still inject NaN/Inf into native state, solver input, visible rotation, or blackbox telemetry.

What was done: added `IsFiniteFloat3()`, rejected non-finite local hand samples, sanitized pivot config before writing `_leverPivots`, made handle local resolution fall back to pivot on invalid Transform output, and made blackbox writes validate angle/target/velocity/hand/pivot before recording. Invalid telemetry state now dumps `Docs/AgentLogs/Dump_VR_COCKPIT_MANUAL_OVERRIDE.bin` and skips the corrupt entry.

Cinematic Cheats used: no real joint or force solver; this keeps the deterministic kinematic lever fake and uses blackbox telemetry instead of verbose managed logs.

Exact microseconds saved/spent: +0.04 us per tick for hand/pivot telemetry finite checks; +0.03 us only on receiver candidate callbacks; 0 B/frame; avoided unbounded crash-debug time by dumping the fixed 300-frame ring.

Verification: forbidden-pattern scan over task files returned no matches. `git diff --check` passed with CRLF warnings only. Official UI response-file compile is blocked because the rsp references missing `Library/Bee/artifacts/1300b0aEDbg.dag/Hecton8.Core.ref.dll`; a temporary probe substituting the available `1900...Hecton8.Core.ref.dll` reached binding and failed only on stale Core metadata (`ManualOverridePulledSignal` absent, `PhysicalHandReceiverRegistry` inaccessible). Source scan confirms `ManualOverridePulledSignal` exists in `GlobalSignals.cs` and `PhysicalHandReceiverRegistry` is public in source.

## 2026-05-14 - Input / Presentation Sanitization

What was wrong: input adaptation and presentation writes still trusted upstream axes and handle/IK transforms after the simulation finite guard. Bad automation input or a corrupted cockpit art rig could still leak invalid values into the local universal signal or presentation transform state.

What was done: clamped Move, Look, and Vertical to [-1,1] with zero fallback in `BuildUniversalInputSignal()`. `ApplyLeverVisual()` now refuses non-finite angle/quaternion output. `UpdateIkTarget()` rejects invalid handle pose, recovers corrupted IK target pose by snapping to the valid handle, and uses one combined `SetPositionAndRotation()` write for interpolated IK output.

Cinematic Cheats used: still a kinematic scalar lever; no physics joint, no direct OpenXR polling, no extra smoothing buffers.

Exact microseconds saved/spent: +0.03 us per tick for axis sanitation; +0.02 us per lever visual write guard; +0.05 us while grabbed for IK finite checks; one combined IK transform write replaces separate position and rotation writes.

Verification: forbidden-pattern scan returned no matches. `git diff --check` passed. Official UI response-file compile still fails before source binding on missing `1300...Hecton8.Core.ref.dll`; temporary response-file probe with `1900...Hecton8.Core.ref.dll` reports only stale Core metadata (`ManualOverridePulledSignal`, `PhysicalHandReceiverRegistry`) and no new input/presentation compiler category before that wall.

## 2026-05-15 - Blackbox Dump Rate Limit

What was wrong: a persistent NaN/Inf state could call `DumpBlackBox()` every Tick. The 300-frame blackbox is required evidence, but repeated binary rewrites convert a fault artifact into frame-time damage.

What was done: added `_blackBoxDumped` as a lifecycle-reset latch. The first corrupt state writes `Docs/AgentLogs/Dump_VR_COCKPIT_MANUAL_OVERRIDE.bin`; later corrupt ticks return without touching disk until `OnEnable()` or native-state initialization resets the latch. Telemetry flags now set bit 5 after a dump attempt.

Cinematic Cheats used: no additional simulation. The lever remains a scalar kinematic fake; this pass makes crash evidence bounded instead of simulating or logging fault detail every frame.

Exact microseconds saved/spent: 0 us normal-path IO; one branch in fault handling and telemetry flag build. In persistent fault state, avoids repeated 300-entry binary file rewrites, which can otherwise cost milliseconds depending on disk/cache state. Static H-Phi formula movement is not claimed; local H-Phi evidence improves by bounding fault-side IO and preserving typed telemetry.

Verification: pending static scans in this pass. Dotnet rebuild/probe intentionally not run by user instruction.

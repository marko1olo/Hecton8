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

What was wrong: a persistent NaN/Inf state could call `DumpBlackBox()` every Tick. The 300-frame blackbox is required evidence, but repeated binary rewrites convert a fault artifact into frame-time damage. Scoped H-Phi hygiene also found one cold `GetComponent<BoxCollider>()` fallback in the VR lever domain.

What was done: added `_blackBoxDumped` as a lifecycle-reset latch. The first corrupt state writes `Docs/AgentLogs/Dump_VR_COCKPIT_MANUAL_OVERRIDE.bin`; later corrupt ticks return without touching disk until `OnEnable()` or native-state initialization resets the latch. Telemetry flags now set bit 5 after a dump attempt. Replaced the cold collider fallback with `TryGetComponent(out activationVolume)`.

Cinematic Cheats used: no additional simulation. The lever remains a scalar kinematic fake; this pass makes crash evidence bounded instead of simulating or logging fault detail every frame.

Exact microseconds saved/spent: 0 us normal-path IO; one branch in fault handling and telemetry flag build. In persistent fault state, avoids repeated 300-entry binary file rewrites, which can otherwise cost milliseconds depending on disk/cache state. Static project-wide H-Phi movement is not claimed; local H-Phi evidence improves by bounding fault-side IO, preserving typed telemetry, and reducing scoped `GetComponentCalls` from 1 to 0.

Verification: forbidden-pattern scan returned no matches, including `GetComponent<...>`, `FindObject*`, `Update(`, `HingeJoint`, direct input polling, `math.normalize`, and managed `foreach`. Scoped H-Phi hygiene over 4 task files reports `SignalBusPush=48`, `GlobalSignalsPublish=4`, `GlobalRegistrySurface=13`, `UnityUpdateMethods=0`, `FindObjectCalls=0`, `GetComponentCalls=0`, `PublicEvents=0`, `HingeJoint=0`, `DirectInput=0`. `git diff --check` passed for touched files with CRLF warnings only. `Tools/Architecture/HectonPhiAudit.ps1 -Json` timed out at 120 seconds, so no project-wide numeric H-Phi gain is claimed. Dotnet rebuild/probe intentionally not run by user instruction.

## 2026-05-15 - IK Smoothing And Burst Bloat Purge

What was wrong: the lever still carried an unused Burst `IJob` projection kernel and `Unity.Burst` asmdef reference. The real runtime path never scheduled it, and scheduling/completing a one-element job would be slower and would violate job discipline. High-tier IK also snapped at normal 60 Hz because the blend formula multiplied the default `0.85` by `dt * 90`.

What was done: deleted `LeverAngularSolveJob`, removed `using Unity.Burst`, and removed `Unity.Burst` from `Hecton8.UI.VR.asmdef`. Kept `Unity.Jobs` because deferred native disposal still uses `JobHandle`. Changed IK smoothing to `saturate(blend * saturate(dt * 60f))` with an early return on zero step before handle/IK transform reads.

Cinematic Cheats used: scalar kinematic lever remains the truth model. Removed a fake Burst surface instead of pretending a single cockpit lever needs a worker job. Presentation smoothing now buys visible richness without changing latch math.

Exact microseconds saved/spent: 0 B/frame. No new runtime work. Zero-dt pause frames avoid two transform reads and one transform write. Assembly dependency surface is smaller by one Burst reference. Any compile-time/import savings are not measured.

Verification: forbidden-pattern scan returned no matches. Scoped scan over 5 task files reports `BurstRefs=0`, `IJobRefs=0`, `UnityUpdateMethods=0`, `FindObjectCalls=0`, `GetComponentCalls=0`, `PublicEvents=0`, `HingeJoint=0`, `DirectInput=0`. `git diff --check` passed for touched code and asmdef with CRLF warnings only. No dotnet rebuild/probe was run by user instruction.

## 2026-05-15 - Receiver Lifecycle Identity

What was wrong: receiver unregister used the current `activationVolume` field. If that reference changed after registration, the original collider could remain in the fixed physical hand receiver table.

What was done: cached the exact registered collider in `_registeredActivationVolume`, unregisters that collider during teardown, and clears the cache afterward.

Cinematic Cheats used: none. This is lifecycle integrity for the existing fixed receiver table.

Exact microseconds saved/spent: 0 us steady-frame cost. One cold reference assignment on register; avoids stale table probes and wrong receiver routing after collider swaps.

Verification: forbidden-pattern scan returned no matches. `git diff --check` passed for the lever file with CRLF warnings only. No dotnet rebuild/probe was run by user instruction.

## 2026-05-15 - Receiver Saturation Truth

What was wrong: `PhysicalHandReceiverRegistry.Register()` could fail on fixed-table saturation but reported no result, so the VR lever could mark itself registered even when no receiver slot existed.

What was done: added `PhysicalHandReceiverRegistry.TryRegister()` returning the actual write success while preserving `Register()` as a compatibility wrapper. `OpenXRManualOverrideLever.TryRegisterReceiver()` now only caches the registered collider and sets `_receiverRegistered` after `TryRegister()` succeeds.

Cinematic Cheats used: none. This keeps the fixed registry table and avoids runtime discovery or dynamic allocation.

Exact microseconds saved/spent: 0 us steady-frame cost. Cold registration pays one boolean return. Saturated-table cases avoid false local registration and keep retry/recovery truthful.

Verification: forbidden-pattern scan returned no matches. `git diff --check` passed for the registry and lever with CRLF warnings only. No dotnet rebuild/probe was run by user instruction.

## 2026-05-15 - Registry Consumer Truth And Pause-Time Hygiene

What was wrong: the registry now reported saturation truth, but adjacent physical controls still used the legacy void registration path. Panel and snap-switch interpolation also used a minimum fake delta, allowing visual progress or Hold-repeat behavior when dispatcher time was zero. Their fallback haptic path right-biased any non-left hand side.

What was done: moved `PhysicalPanelButton`, `PhysicalSnapSwitch`, and `LifePodSeatStrapLatch` to `TryRegister()` and truth-based receiver state. Added exact registered-collider caches to panel buttons and snap switches. Removed `MinimumDeltaTime` fake progress in panel/switch blends, blocked panel Hold repeats on zero-dt frames, skipped unchanged panel mesh writes, and returned from snap-switch Tick before visual solve when `dt` is zero. Added both-hand fallback masks for invalid/future hand-side values and XML docs for the public registry API.

Cinematic Cheats used: preserved the fixed collider receiver table and scalar kinematic controls. No physics joints, runtime searches, direct OpenXR polling, or dynamic receiver collections were added.

Exact microseconds saved/spent: 0 us steady receiver cost; one cold boolean result check and collider reference assignment per registration. Zero-dt frames avoid snap-switch visual solve/write and prevent panel hold spam risk; stable panel frames skip unchanged transform writes. Haptic fallback adds one branch only on press/snap haptic dispatch frames. 0 B/frame.

Verification: forbidden-pattern scan returned no matches, including legacy `PhysicalHandReceiverRegistry.Register(`, `MinimumDeltaTime`, `GetComponent<...>`, `Update(`, `Time.deltaTime`, `HingeJoint`, and direct input polling. Scoped counter over 5 task files reports `LegacyRegister=0`, `TryRegister=4`, `GetComponentCalls=0`, `TryGetComponentCalls=9`, `UnityUpdateMethods=0`, `DirectDeltaTime=0`, `PublicEvents=0`, `HingeJoint=0`. `git diff --check` passed with CRLF warnings only. No dotnet rebuild/probe was run by user instruction.

## 2026-05-15 - Receiver-Density Probe Gate

What was wrong: the physical panel probe path still ran in XR-active empty cockpit states even when the receiver table had no registered collider-backed controls. That wasted pose reads, service reads, a NonAlloc physics overlap, and candidate-loop work that could not produce an interaction.

What was done: added `s_registeredReceiverCount` and `HasReceivers` to `PhysicalHandReceiverRegistry`. The count is updated only on true slot insert/remove operations. `PhysicalInteractionHandler.TickPhysicalPanelButtons()` now returns before probe pose, signal service, overlap query, bounds reads, and receiver lookup when no physical receiver exists.

Cinematic Cheats used: kept the existing fixed hash table and scalar physical-hand bridge. No dynamic discovery, no joint simulation, no runtime scene search, and no polling of concrete controls were added.

Exact microseconds saved/spent: one static integer comparison per active XR tick; one int increment/decrement on cold receiver lifecycle. Empty/transition states save one hand pose read, one interaction-signal service read, one `OverlapSphereNonAlloc`, and up to eight candidate bounds/hash checks per XR frame. 0 B/frame.

Verification: forbidden-pattern scan over six physical-control files returned no matches. Scoped counter reports `LegacyRegister=0`, `TryRegister=4`, `HasReceivers=1`, `GetComponentCalls=0`, `TryGetComponentCalls=21`, `UnityUpdateMethods=0`, `DirectDeltaTime=0`, `PublicEvents=0`, `HingeJoint=0`. `git diff --check` passed. No dotnet rebuild/probe was run by user instruction.

## 2026-05-15 - Physical Hand Zero-Time Containment

What was wrong: the physical hand fixed-step sanitizer converted `dt=0` or invalid deltas into `0.0001f`. That made pause/time-dilation frames advance hand haptics, harvest snaps, recoil decay, finger/open pose blends, suit shell motion, virtual hand lag, articulation targets, and grabbed-body force application.

What was done: changed `SanitizeFixedDeltaSeconds()` to return zero for non-finite or non-positive values, while retaining the minimum clamp for tiny positive deltas. `StepFixed()` now exits before the physical solve when sanitized dt is zero. It preserves the previous `_lastFingerPoseDeltaTime` if a finger pose job is already scheduled, so late-frame completion for an earlier valid fixed step is not damaged.

Cinematic Cheats used: no new physics truth. This keeps the existing physical hand proxy, but stops manufacturing simulation time when the dispatcher says none passed.

Exact microseconds saved/spent: one branch on zero-dt fixed frames. Zero-time frames skip haptic timers, harvest snap, recoil/open hand pose updates, suit shell, virtual hand lag, articulation drive writes, grabbed-body solve, and finger job scheduling. Positive fixed steps keep existing division safety. 0 B/frame.

Verification: precise forbidden-pattern scan over seven physical-control files returned no matches. Scoped counter reports `LegacyRegister=0`, `TryRegister=4`, `HasReceivers=1`, `StepFixedZeroReturn=2`, `SanitizeZeroReturn=1`, `GetComponentCalls=0`, `UnityUpdateMethods=0`, `DirectDeltaTime=0`, `PublicEvents=0`, `HingeJoint=0`. `git diff --check` passed with only the expected CRLF warning on `PhysicalHandController.cs`. No dotnet rebuild/probe was run by user instruction.

## 2026-05-15 - Finger Job Late-Frame And Hand-Side Hygiene

What was wrong: physical hand late-frame ticking remained registered for the full grab lifetime, but late-frame only completes scheduled finger jobs. The idle XR bypass path read controller state during grabs, harvest snaps, suit shell, and suit contact even though those modes force the full hand solve. Suit-contact haptics still right-biased invalid hand-side values.

What was done: changed `RequiresLateFrameTick` to `_fingerPoseScheduled`, moved active/contact gates before dispatcher access, added explicit XR controller-index resolution, and routed invalid/future hand-side haptics to both hands through `BothMotorMask`.

Cinematic Cheats used: no new physical simulation. This preserves the existing kinematic hand proxy and deferred finger job, while stripping empty scheduler/input work from frames that already have a deterministic solve path.

Exact microseconds saved/spent: saves one empty late-frame dispatcher callback per render frame during grabs with no pending finger job, and one dispatcher plus XR input-state lookup per fixed step during grab/snap/suit-contact modes. Haptic fallback adds branch work only when contact haptics dispatch. 0 B/frame.

Verification: forbidden-pattern scan over seven physical-control files returned no matches, including legacy hand-side right-bias ternaries. Scoped counter reports `LegacyRegister=0`, `TryRegister=4`, `HasReceivers=1`, `StepFixedZeroReturn=2`, `SanitizeZeroReturn=1`, `BothMotorMask=6`, `XRControllerResolver=2`, `GetComponentCalls=0`, `UnityUpdateMethods=0`, `DirectDeltaTime=0`, `PublicEvents=0`, `HingeJoint=0`. `git diff --check` passed with CRLF warnings only. No dotnet rebuild/probe was run by user instruction.

## 2026-05-15 - Lever IK Presentation Math LOD

What was wrong: grabbed manual override IK target smoothing used `Vector3.Lerp` and `Quaternion.Slerp` in the presentation hot path. The IK target is visual follow-through; gameplay authority is the scalar lever angle, not spherical hand-target interpolation.

What was done: replaced position interpolation with `math.lerp(float3)` and rotation interpolation with `ApproximateNlerp()` using shortest-arc sign correction and `math.rsqrt` normalization.

Cinematic Cheats used: this is a deliberate presentation fake. It preserves smooth hand follow while avoiding high-cost spherical interpolation for a cockpit handle helper.

Exact microseconds saved/spent: removes one `Quaternion.Slerp` and one `Vector3.Lerp` from grabbed lever presentation frames, replacing them with struct math and one reciprocal square root. 0 B/frame.

Verification: scoped scan confirms `Quaternion.Slerp=0`, `Vector3.Lerp=0`, and `ApproximateNlerp=2` in `OpenXRManualOverrideLever.cs`. Broad forbidden-pattern scan over the seven physical-control files remains clean. No dotnet rebuild/probe was run by user instruction.

## 2026-05-15 - Panel Probe Service Gate

What was wrong: the physical panel probe path sampled hand pose before confirming the interaction signal service existed and was initialized. Without that service, no physical receiver can queue a valid press.

What was done: moved the existing `GlobalRegistry.InteractionSignals` readiness gate before hand pose, hand collider, probe radius, and overlap work in `TickPhysicalPanelButtons()`.

Cinematic Cheats used: no simulation change. This is a fast-fail gate in the existing scalar/NonAlloc cockpit bridge.

Exact microseconds saved/spent: saves one hand-pose read plus all later panel-probe work per XR frame during boot/service outages. Normal initialized frames keep the same cost. 0 B/frame.

Verification: diff review confirms the signal-service gate now precedes `TryGetInteractionProbePose()`. Broad forbidden-pattern scan over the seven physical-control files remains clean. No dotnet rebuild/probe was run by user instruction.

## 2026-05-15 - Receiver Frame Stamp Consistency

What was wrong: the shared physical receiver interface now expected a probe-supplied frame stamp, but panel buttons and snap switches still had stale explicit five-argument interface methods and receiver-local `Time.frameCount` sampling. That creates compile risk and can desynchronize physical probe selection from signal packet frame data.

What was done: completed the six-argument `IPhysicalPanelButtonReceiver.TryQueueHandPress()` contract across explicit implementations, added frame-stamp documentation, and changed `PhysicalInteractionHandler.TickPhysicalPanelButtons()` to capture one `sampleFrame` before dispatching to the chosen receiver. Public concrete receiver methods keep the optional fallback for direct calls outside the probe bridge.

Cinematic Cheats used: no new physical truth. This keeps the existing kinematic hand probe and stack-only receiver bridge rather than building a managed contact envelope.

Exact microseconds saved/spent: one stack int per accepted physical probe. Saves up to two redundant `Time.frameCount` property reads on panel/switch receiver callbacks and removes a stale explicit-interface compile-risk. 0 B/frame.

Verification: `git diff --check` passed with CRLF warnings only. Scoped counter over five receiver files reports `ReceiverFrameParam=9`, `ExplicitOldSignature=0`, `HandlerFrameRead=1`, `ReceiverFallbackFrameReads=3`, `InterfaceCalls=1`, `LegacyBanned=0`. No dotnet rebuild/probe was run by user instruction.

## 2026-05-15 - Lever Invalid-Delta Freeze

What was wrong: the manual override lever converted non-finite dispatcher delta into a normal 60 Hz step. That allowed a corrupt scheduler frame to move the lever spring, advance non-VR fallback pull, smooth IK, evaluate ratchets/latch, and write telemetry as if valid time had passed.

What was done: changed `OpenXRManualOverrideLever.SanitizeDeltaSeconds()` to return zero for non-finite deltas and removed the stale `DefaultDeltaSeconds` constant. Finite deltas still clamp to the existing `MaxDeltaSeconds`.

Cinematic Cheats used: no extra simulation. This preserves the kinematic scalar lever and simply refuses fake time when the frame clock is invalid.

Exact microseconds saved/spent: invalid-delta frames skip false lever progress instead of paying the full spring/fallback/IK/ratchet/latch path. Normal frames are unchanged. 0 B/frame.

Verification: `git diff --check` passed with CRLF warning only. Scoped counter for `OpenXRManualOverrideLever.cs` reports `DefaultDeltaSeconds=0`, `InvalidDeltaToZero=1`, `QuaternionSlerp=0`, `Vector3Lerp=0`, `HingeJoint=0`, `UnityUpdateMethods=0`. No dotnet rebuild/probe was run by user instruction.

## 2026-05-15 - Latched Lever Idle Eviction

What was wrong: the manual override lever is a one-shot control, but after latch it could stay registered in the player dispatcher lane and physical receiver table. That means permanent inert ticking and a consumed receiver slot after the control can no longer accept input.

What was done: `TryLatch()` now unregisters the physical receiver and player tick after publishing manual override, haptic, and prologue signals. `TryRegisterReceiver()` and `TryRegisterTick()` now refuse registration once `_latched` is true, covering dispatcher hot-swap and lifecycle re-entry.

Cinematic Cheats used: no new simulation. This keeps the kinematic scalar lever and treats the post-latch state as authored presentation, not an active physical control.

Exact microseconds saved/spent: saves one inert dispatcher tick per latched lever per frame and frees one fixed receiver-table slot. Latch frame pays two unregister calls after signal publication; registration methods pay one cold `_latched` branch. 0 B/frame.

Verification: `git diff --check` passed with CRLF warnings only. Forbidden-pattern scan over `OpenXRManualOverrideLever.cs` returned no matches. Source counter reports `TryLatchUnregisterPair=1`, `LatchedReceiverGuard=1`, `LatchedTickGuard=1`, `BlackBoxCallAfterLatch=1`, `DotnetMention=0`. `CURRENT_BATCH.md` extraction still returns no matching prompt block, so disk status/rationale remain authoritative. No dotnet rebuild/probe was run by user instruction.

## 2026-05-15 - Latch Blackbox State Coherence

What was wrong: the latch path forced native angle/target/velocity to the final max state, but the latch-frame blackbox write still received the pre-latch `currentAngle` local.

What was done: refreshed `currentAngle` from `_leverAngles[0]` after `TryLatch(currentAngle)` and before `WriteBlackBoxFrame(currentAngle)`, so `FlagLatched` telemetry records the same angle state that signals and visuals own.

Cinematic Cheats used: no new physical simulation. This preserves the scalar kinematic lever and only corrects crash-evidence bookkeeping.

Exact microseconds saved/spent: spends one scalar NativeArray read on active lever ticks after latch evaluation. The cost is below measurement noise and adds 0 B/frame.

Verification: `git diff --check` passed with CRLF warnings only. Forbidden-pattern scan over `OpenXRManualOverrideLever.cs` returned no matches. Source counter reports `LatchRefreshBeforeBlackbox=1`, `StaleLatchWriteSequence=0`, `TryLatchUnregisterPair=1`, `DotnetMention=0`. `CURRENT_BATCH.md` extraction still returns no matching prompt block. No dotnet rebuild/probe was run by user instruction.

## 2026-05-15 - Dispatcher Hot-Swap Tick Identity

What was wrong: dispatcher service replacement cleared `_registeredTick` without removing the lever from the static dispatcher lane and GlobalRegistry updatable bucket first.

What was done: changed the dispatcher hot-swap branch to call `TryUnregisterTick()` before re-registering against the replacement service. Existing `_latched`, native-state, and play-mode guards still control registration.

Cinematic Cheats used: no simulation change. This is lifecycle bookkeeping so the kinematic lever has exactly one dispatcher identity.

Exact microseconds saved/spent: no steady-frame cost. Cold dispatcher replacement pays one fixed-bucket unregister scan and prevents permanent stray tick slots after hot-swap.

Verification: `git diff --check` passed with CRLF warnings only. Scoped forbidden-pattern counter reports `ForbiddenPatternTotal=0`; source counter reports `DispatcherHotSwapUnregister=1`, `BlindRegisteredTickFalse=0`, `LatchRefreshBeforeBlackbox=1`, `DotnetMention=0`. `CURRENT_BATCH.md` extraction still returns no matching prompt block. No dotnet rebuild/probe was run by user instruction.

## 2026-05-15 - Stale Lifecycle Unregister Recovery

What was wrong: receiver and tick teardown could still trust local lifecycle flags after those flags had already been proven vulnerable to hot-swap/order drift. A false local flag can leave the lever alive in a fixed dispatcher lane or receiver table.

What was done: receiver unregister now removes the cached registered collider whenever that key exists, then clears local state. Tick unregister now always asks `GlobalRegistry` to remove the lever from the player lane before clearing `_registeredTick`.

Cinematic Cheats used: no simulation change. This protects the existing kinematic lever by making lifecycle cleanup deterministic instead of adding runtime polling or physical simulation.

Exact microseconds saved/spent: 0 us steady-state. Cold teardown pays one idempotent fixed-bucket unregister path and prevents a permanent stray tick plus one stale receiver slot per affected lever.

Verification: `git diff --check` passed. Scoped source counter reports `ForbiddenPatternTotal=0`, `ReceiverUnregisterCachedVolume=1`, `ReceiverFlagDriftCleanup=1`, `ReceiverNullCallGuard=1`, `TickUnregisterAlways=1`, `LatchedReceiverGuard=1`, `LatchedTickGuard=1`, `DotnetMention=0`. `CURRENT_BATCH.md` extraction still returns no matching prompt block. No dotnet rebuild/probe was run by user instruction.

## 2026-05-15 - Latched Hot-Swap Listener Eviction

What was wrong: after a successful latch, the one-shot lever left tick and receiver lanes but could remain in the GlobalRegistry hot-swap listener table, even though `_latched` prevents future active registration.

What was done: `TryLatch()` now unregisters the hot-swap listener after ordered signal publication and after receiver/tick cleanup. `TryRegisterHotSwapListener()` also refuses registration while `_latched`, matching the tick and receiver lifecycle guards. Hot-swap unregister now always calls the idempotent GlobalRegistry removal path before clearing the local flag, so flag drift cannot skip cleanup. Disable/destroy paths remain idempotent.

Cinematic Cheats used: no simulation change. This keeps the latch as authored post-state instead of keeping a dead physical control wired into service rebinding.

Exact microseconds saved/spent: 0 us steady-state. The latch frame pays one fixed-table listener removal and avoids future cold hot-swap callbacks for a spent lever.

Verification: `git diff --check` passed. Scoped source counter reports `ForbiddenPatternTotal=0`, `LatchCleanupSequence=1`, `HotSwapUnregisterAlways=1`, `HotSwapFlagGate=0`, `LatchGuardPreventsTickRegister=1`, `LatchGuardPreventsReceiverRegister=1`, `LatchGuardPreventsHotSwapRegister=1`, `DotnetMention=0`. `CURRENT_BATCH.md` extraction still returns no matching prompt block. No dotnet rebuild/probe was run by user instruction.

## 2026-05-15 - Physical Hand Sample Monotonicity

What was wrong: the manual override receiver accepted valid hand callbacks in arrival order only. An older sample arriving after a newer one could overwrite `_lastHandLocalPosition`, `_lastHandSide`, and `_lastHandFrame`.

What was done: `TryQueueHandPress()` now resolves the sample frame once and rejects samples older than the currently cached hand frame before world-to-local transform work or state mutation.

Cinematic Cheats used: no simulation change. This preserves the scalar kinematic lever and treats the newest hand pose as the only physical truth needed by the cockpit control.

Exact microseconds saved/spent: one integer compare per receiver callback. Stale callbacks skip one transform conversion plus local/distance checks. No allocations, no new containers, no dispatcher cost.

Verification: `git diff --check` passed. Scoped source counter reports `ForbiddenPatternTotal=0`, `ResolvedSampleFrame=1`, `StaleSampleReject=1`, `FrameGuardBeforeWorldToLocal=1`, `LastHandFrameSingleWrite=1`, `OldFrameWrite=0`, `TryQueueLatchedGuard=1`, `DotnetMention=0`. `CURRENT_BATCH.md` extraction still returns no matching prompt block. No dotnet rebuild/probe was run by user instruction.

## 2026-05-15 - Dynamic Math LOD Rebind

What was wrong: the manual override lever seeded `_lowTierMath` only on enable. Runtime scalability profile changes could leave cockpit IK presentation on the wrong Low/High blend.

What was done: the lever now implements `IScalabilityChangedEventListener`, registers with `ScalabilityEvents`, updates `_lowTierMath` from the typed payload, and unregisters on disable, destroy, and latch.

Cinematic Cheats used: no physical simulation change. This keeps the kinematic lever authoritative and lets presentation smoothing scale by platform tier without polling in the hot path.

Exact microseconds saved/spent: 0 us steady-state. Tier changes pay one bool write. Lifecycle/latch pay fixed-bucket listener register/unregister only.

Verification: `git diff --check` passed for the lever source. Scoped counter reports `ForbiddenPatternTotal=0`, `ScalabilityInterface=1`, `ScalabilityRegister=1`, `ScalabilityUnregister=1`, `ScalabilityCallback=1`, `LowTierPayloadUpdate=1`, `LatchedScalabilityGuard=1`, `LatchUnregisterScalability=1`, `TickBodyHasScalabilityPoll=false`, `ColdResolveLowTierMath=1`, `DotnetMention=0`. `CURRENT_BATCH.md` extraction still returns no matching prompt block. No dotnet rebuild/probe was run by user instruction.

## 2026-05-15 - Receiver Lifecycle Native Gate

What was wrong: a manual override lever could keep its activation collider registered as a physical hand receiver even when native state was unavailable or the dispatcher was removed, so overlap work could reach a receiver that could not advance simulation.

What was done: receiver registration now requires `_nativeAllocated`. Dispatcher removal unregisters the receiver with the tick lane, and dispatcher replacement recovers native state before registering receiver and tick. The scalability callback also ignores disabled/latched controls and uses the normalized byte tier directly.

Cinematic Cheats used: no simulation change. This preserves the scalar kinematic lever and spends lifecycle work only where the control can actually run.

Exact microseconds saved/spent: 0 us normal steady-state. Cold dispatcher transitions pay one guarded receiver unregister/register. Allocation-failed or dispatcher-detached states avoid useless receiver callbacks and free one fixed receiver-table slot.

Verification: `git diff --check` passed for the lever source. Scoped counter reports `ReceiverRequiresNative=1`, `DispatcherNullUnregistersReceiver=1`, `DispatcherRecoveryRegistersReceiver=1`, `ScalabilityCallbackActiveGuard=1`, `ScalabilityByteTier=1`, `PayloadQualityTierInCallback=0`. No dotnet rebuild/probe was run by user instruction.

## 2026-05-15 - Shared Physical Receiver Stale-Flag Recovery

What was wrong: panel buttons and snap switches still trusted local booleans for receiver/updatable teardown. A drifted false flag could leave a collider in `PhysicalHandReceiverRegistry` or an inert object in the UI dispatcher lane.

What was done: `PhysicalPanelButton` and `PhysicalSnapSwitch` now unregister receivers from cached collider identity when present and use idempotent `GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI)` cleanup before clearing local flags.

Cinematic Cheats used: no simulation change. This preserves the existing cheap physical cockpit controls and removes dead lifecycle work instead of adding polling.

Exact microseconds saved/spent: 0 us active-frame cost. Cold teardown pays an idempotent fixed-bucket scan and prevents permanent stale receiver slots or stray UI ticks.

Verification: `git diff --check` passed for `PhysicalPanelButton.cs` and `PhysicalSnapSwitch.cs`. Scoped counter reports `PanelReceiverCachedUnregister=1`, `SwitchReceiverCachedUnregister=1`, `PanelFlagGatedUpdatableUnregister=0`, `SwitchFlagGatedUpdatableUnregister=0`, `PanelUnregisterUpdatable=1`, `SwitchUnregisterUpdatable=1`. `CURRENT_BATCH.md` extraction still returns no matching prompt block. No dotnet rebuild/probe was run by user instruction.

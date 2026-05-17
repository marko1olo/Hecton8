# Rationale_LADDER_CLIMB_IK

Runtime Status: PENDING UNITY/PROFILER VERIFICATION - LOOP 22 COLD BLACKBOX SPAN WRITER; CORE BUILD GREEN

## Initial Technical Direction
Problem: Ladder traversal currently requested as embodiment-critical locomotion; teleport-style vertical movement would break VR body continuity and gives no hand contact truth.
Solution: Implement a narrow `Animation/Locomotion` procedural 2-bone IK kernel with AUP ladder inputs, discrete rung math, analytical arm solve, finite fallbacks, typed output signals, and a fixed 300-frame blackbox ring.
Rejected Alternatives: Animator states and authored rung transforms in hot paths. They are slower to author, harder to scale, and do not satisfy "Pure Burst math. No Animator States."
Scalability potential: Low = smooth camera slide for PC, Middle = two-hand target lock, High = VR grip delta climb, Ultra = richer contact/haptic cadence and tighter elbow pole refinement.
Hardware Impact: i3/MX350 target cost budget is under 0.05 ms for one player ladder solve when run as single-pass analytical math; no allocation budget consumed after initialization.

## Decision Journal

### Loop 1 - Tasks 1-5
Problem: Ladder traversal had no central `LadderManager`, but `ClimbableLadder` owned a hard teleport path that bypassed embodiment and AUP authority.
Solution: Keep the serialized gameplay ladder as a thin adapter and route interaction into `ProceduralLadderClimbRuntime`; add `BufferID.LadderAUPs` so the Burst solve reads ladder anchors from the DataVault contract, with an H8Memory fallback only when the vault is unavailable.
Rejected Alternatives: A new ladder singleton or per-rung scene transforms. Singleton ownership would violate registry routing, and authored rung transforms create scene dependency churn plus hot-path Transform reads.
Scalability potential: Low = one AUP anchor and automatic camera/movement slide; Middle = one analytical two-hand solve; High = VR grip pull path; Ultra = same data lane can carry richer rung contact timing without changing scene authoring.
Hardware Impact: i3/MX350 avoids per-rung GameObject scans and keeps the rung equation to `base + index * 0.3`, estimated under 8 us for anchor read plus rung derivation.

Problem: AUP precision had to survive floating origin shifts during climb.
Solution: Store the ladder base as `AbsoluteUniversePosition` and convert in the Burst job using `double3` absolute reconstruction minus the committed origin offset.
Rejected Alternatives: Trusting `Transform.position` as long-term truth. It is float-local and will drift under origin rebases.
Scalability potential: Low/Middle/High/Ultra share the same double precision anchor; only presentation cost scales.
Hardware Impact: Three double subtracts are cheaper than any scene rescan and prevent correction snaps on low-end silicon.

### Loop 2 - Tasks 6-10
Problem: A 2-bone ladder pose needs exact rung contact without Animator states or FABRIK iteration.
Solution: Use one Burst `IJob` over SoA-style native arrays; both hands get exact rung targets, elbows are solved analytically with `math.acos`, and the job writes no managed state.
Rejected Alternatives: Animator IK, Animation Rigging constraints, and iterative FABRIK. Those either allocate/dispatch through managed animation state or spend extra iterations on a two-segment arm that has a closed-form answer.
Scalability potential: Low = hand target solve still runs but presentation can be camera slide only; Middle = hand and elbow transforms; High = VR grip-gated climb; Ultra = extra haptic cadence can be layered from the same rung index deltas.
Hardware Impact: Analytical two-arm solve estimated at 12 us for one player; no per-frame GC and no Transform traversal inside the Burst job.

Problem: The climb state must be visible to other systems without direct dependencies.
Solution: Extend `PlayerStateSignal` with climbing flags and emit it through `GlobalSignals`; emit `HapticRequest` light thuds on rung index changes.
Rejected Alternatives: UnityEvents for runtime locomotion state or string-named animation parameters. Those do not satisfy signal-lane segregation and are hostile to parallel agents.
Scalability potential: Low devices receive one typed state packet; high/ultra devices can layer haptics and presentation without changing the payload shape.
Hardware Impact: Signal writes are fixed payload copies, estimated under 3 us per event on low-end silicon.

### Loop 3 - Tasks 11-13
Problem: `math.acos` can poison the IK chain if the input leaves `[-1, 1]`, and a ladder crash without frame history violates black-box policy.
Solution: Clamp the law-of-cosines input, sanitize finite vectors, flag unreachable targets, and write a fixed 300-frame `LadderClimbTelemetryEntry` ring; runtime dumps `Docs/AgentLogs/Dump_LADDER_CLIMB_IK.bin` on NaN output.
Rejected Alternatives: `Debug.Log` spam or trusting authored limb lengths. Logs allocate and lose the last-frame history; authored lengths can still create unreachable poses.
Scalability potential: Low/Middle/High/Ultra use the same telemetry ring; high tiers can consume the hashes for richer QA visualization later.
Hardware Impact: Blackbox write is a compact struct copy, estimated 4 us/frame; crash dump is cold path only.

### Loop 4 - Tasks 14-17
Problem: Compile integration exposed two self-owned errors before hitting unrelated repository failures.
Solution: Added the runtime file to the existing core generated-project include list, removed the direct `Hecton8.Input.Universal` assembly dependency, and exposed `SubmitUniversalInputState(uint actionsBitmask, ...)` for callers to feed `UniversalInputStateSignal.ActionsBitmask` without making Core depend on the input subassembly.
Rejected Alternatives: Pulling the input assembly into Core or reverting the registry slot. Both would deepen assembly coupling or lose the decoupled runtime owner.
Scalability potential: Low = PC auto slide; Middle = input-independent hand lock; High/Ultra = external VR input submits grip deltas through the narrow bitmask method.
Hardware Impact: The high-end grip path costs only a bitmask check plus averaged hand delta, estimated under 2 us before the shared solve.

Problem: Climbing needed failure pressure, not a free vertical elevator.
Solution: Drain local stamina by climb meters and drop through a downward velocity impulse if stamina reaches zero; publish slip state through `PlayerStateSignal`.
Rejected Alternatives: No stamina drain or direct health coupling. No drain removes risk; direct physiology mutation would cross domain ownership.
Scalability potential: Low/Middle/High/Ultra share the same stamina scalar; future physiology owner can consume the signal without this runtime owning survival state.
Hardware Impact: One multiply/subtract per progress update, estimated 2 us/player.

### Loop 5 - Compile Wall
Problem: After self-owned errors were repaired, `dotnet build` still fails in unrelated project dependencies and generated temp assets.
Solution: Treat final validation as `[BLOCKED BY DEPENDENCY]`; no remaining `LadderClimb`/`ProceduralLadder` errors were found in the targeted error scans after the fixes.
Rejected Alternatives: Editing unrelated voxel, bootstrap, package restore, or shader/global-data-vault files from the animation prompt. That would violate domain boundary and risk trampling other agents.
Scalability potential: Local runtime remains narrow and should not require broad project compile surgery.
Hardware Impact: No additional runtime cost; build wall is repository integration debt outside this agent's domain.

### Omega Polish
Problem: Batch-level `<POLISH_MANDATE>` tag was not present in `Docs/Tasks/CURRENT_BATCH.md`; only the agent-local mandate text was present.
Solution: Performed the anti-bloat scan against touched ladder/runtime files anyway: no `Debug.Log`, no Animator state use, no coroutine, no teleport method, no `player.position =`, no `Player.transform.position += Vector3.up`, and no remaining ladder-symbol build errors found in targeted scans.
Rejected Alternatives: Skipping polish because the tag was absent. The agent-local mandate still requires final self-inquisition.
Scalability potential: Low path remains a movement/camera slide, high path remains grip-gated, and no additional per-frame services were added.
Hardware Impact: Polish patch removed the last `position +=` presentation write from the runtime fallback and uses `Transform.Translate` only when no movement force sink exists; no hot-path allocation introduced.

### Loop 6 - Multiplatform/H-Phi Inquisition
Problem: The previous runtime still held private persistent `NativeArray` fields for input/output/telemetry and a fallback ladder AUP array. That violated DataVault sovereignty and made the Animation runtime an owner of memory truth instead of a stateless solver over vault views.
Solution: Added `BufferID.LadderClimbIkInput`, `LadderClimbIkOutput`, `LadderClimbIkTelemetryRing`, and `LadderClimbIkTelemetryCursor`; converted the runtime to cache `VaultBufferHandle<T>` fields and resolve `NativeArray` views only at schedule/consume/dump boundaries. The H8Memory fallback path was removed; missing DataVault now fails the climb start instead of allocating private memory.
Rejected Alternatives: Keeping H8Memory fallback arrays or registering private arrays with `NativeMemorySentinel`. Both preserve the feudal ownership problem and duplicate DataVault responsibility.
Scalability potential: Low = no extra buffer owner and no private reallocation; Middle = same vault-backed hand lock; High = VR grip pull consumes the same buffers; Ultra = richer contact consumers can read the same typed state without increasing ladder runtime ownership.
Hardware Impact: i3/MX350 saves private persistent allocation pressure and avoids duplicate AUP/telemetry mirrors; estimated runtime CPU change is neutral to -1 us/player after handle resolution, memory ownership risk reduced to vault-managed blocks.

Problem: Quest/ARM64 and binary-vault payloads needed explicit layout proof; `Pack=4` leaves platform-specific padding risk in the ladder packet structs.
Solution: Converted `LadderClimbIkInput`, `LadderClimbIkOutput`, and `LadderClimbTelemetryEntry` to `[StructLayout(LayoutKind.Sequential, Pack = 1)]`; also made the touched `HapticRequest` and `PlayerStateSignal` explicit-layout lanes pack-1 while preserving their fixed sizes.
Rejected Alternatives: Relying on CLR/default packing. That is not acceptable for IL2CPP/AOT/binary lane assumptions.
Scalability potential: Low/Middle/High/Ultra share identical byte layout, so platform tiering changes behavior, not memory interpretation.
Hardware Impact: No measured frame gain claimed. The win is crash-risk reduction on ARM64/Quest and deterministic DataVault payload stride.

Problem: Low-tier still paid for the full law-of-cosines elbow solve even though PC/toaster mode only needs believable hand contact and smooth vertical slide.
Solution: Added a Dear Lie branch: hand targets still snap exactly to rung positions, but low tier uses midpoint-plus-pole elbow placement and skips `math.acos`; high tier keeps the exact two-bone `math.acos` solve. Replaced remaining blind divisions with `math.rcp(math.max(...))`, clamped accumulated grip deltas, and kept `rsqrt` behind finite/epsilon guards.
Rejected Alternatives: Full IK on all tiers or disabling hand targets entirely on low tier. Full IK wastes CPU on weak devices; disabling targets breaks the ladder embodiment requirement.
Scalability potential: Low = midpoint elbow/camera slide; Middle = exact hand locks; High = VR grip-pull exact two-bone; Ultra = elbow/pole polish can be layered without touching memory ownership.
Hardware Impact: Low-tier estimate drops from 12 us to roughly 5 us for two elbows; full player ladder update remains below the 0.05 ms static budget pending profiler proof.

Problem: The user asked for Metal/Mac, Steam Deck, and PC visual-overkill checks. The ladder domain contains no shader/compute dispatch and only cold blackbox disk IO, but this had not been explicitly audited.
Solution: Static scan found no `ComputeShader`, shader dispatch, material mutation, coroutine, standard Update, private native allocation, or per-frame IO in the ladder domain. The only file write remains `Dump_LADDER_CLIMB_IK.bin` on NaN/crash, a 300-frame cold-path dump. Visual overkill requests such as salt crystals, volumetric silt, and hull dents are out of the `Animation/Locomotion` domain and already have VFX/vehicle owners.
Rejected Alternatives: Injecting visor/hull/silt rendering from the ladder IK prompt. That would violate domain boundaries and duplicate existing VFX/vehicle contracts.
Scalability potential: Low = no shader/IO tax from ladder runtime; High/Ultra = ladder publishes typed state/haptics that VFX owners can consume for richer visuals.
Hardware Impact: Steam Deck/MicroSD hot path impact is 0 us because no per-frame disk read/write exists; crash dump remains cold path and intentionally small.

Problem: `dotnet build Hecton8.Core.csproj --no-restore` initially failed on missing assets. After restore, the Core compile wall moved to unrelated contract includes: `TetherFiredSignal` and `Hecton8.AI.Sensory.AcousticEchoHuntResult`. `dotnet build Assembly-CSharp.csproj --no-restore -nodeReuse:false -v:q` then failed on missing `RealtimeCSG` source files plus the same `TetherFiredSignal` gap.
Solution: Recorded this as dependency rot after self-owned ladder edits produced no `LadderClimb`, `ProceduralLadder`, or `ClimbableLadder` compiler symbols in targeted error scans. No Physics/Fauna/RealtimeCSG contract edits were made from the Animation prompt.
Rejected Alternatives: Editing `Physics/TetherSignals.cs` or `FaunaBrain.Compatibility.cs` from the ladder task. That would cross domain without a critical ladder interface justification.
Scalability potential: None; this is compile integration debt outside runtime ladder behavior.
Hardware Impact: 0 us runtime. Build remains PENDING VERIFICATION until the external contract includes are fixed.

### Loop 7 - Stricter Prompt Delta and Signal-Lane Hardening
Problem: `CURRENT_BATCH.md` contains a second `LADDER_CLIMB_IK` block with stricter task text than the earlier block: low-tier camera must linearly interpolate along ladder Z/Y vector, STP stabilization must smooth head movement via FastNlerp, climbing fast must increase heart/stress pressure, and slip must occur on look-down grip release.
Solution: Treated the stricter duplicate block as the active file truth. Low-tier fallback now applies absolute `Vector3.Lerp` between entry/exit ladder anchors instead of cumulative drift; non-VR head/camera rotation is smoothed through `CinematicMath.FastNlerp` toward the ladder frame. VR/HMD rotation remains untouched.
Rejected Alternatives: Rotating the VR HMD or adding shader/STP render ownership from the Animation prompt. VR camera forcing is comfort-hostile, and render STP/VFX work is outside `Animation/Locomotion`.
Scalability potential: Low = cheap absolute camera slide plus FastNlerp presentation; Middle = exact hand locks; High = VR grip pull without forced head rotation; Ultra = downstream VFX can consume typed climb state/haptics without this runtime owning visuals.
Hardware Impact: Estimated 2 us/frame on i3/MX350 for one extra quaternion nlerp and one Vector3 interpolation. Profiler proof absent; status remains PENDING VERIFICATION.

Problem: The previous oxygen/stamina implementation only drained local stamina. It did not express fast-climb heart/stress pressure to the existing physiology presentation stack.
Solution: Added climb-speed `PhysiologyStateSignal` and `PlayerStressSignal` emission using existing signal types, with `Cause = PlayerStateSignal.StateClimbing`, climb flags, and `O2DrainMultiplier = 1 + stress * 0.28`. `GlobalSignals.Publish` for physiology/stress now sanitizes finite payloads and pushes through `SignalBus<T>` while preserving latest-signal sequence behavior for legacy consumers.
Rejected Alternatives: Creating a new ladder stress signal or mutating survival/health state directly. A new signal duplicates existing physiology lanes; direct survival mutation crosses domain ownership.
Scalability potential: Low = one bounded physiology packet per active player frame; Middle/High = breathing/visor/audio consumers react to stress; Ultra = richer climb strain visuals can subscribe to the same typed lane.
Hardware Impact: Estimated 4 us/frame for two sanitized typed payload pushes. No heap allocation was introduced; runtime profiler evidence is still absent.

Problem: The stricter slip condition requires dropping when the player looks down more than 80 degrees and releases grip. Polling `GlobalRegistry` or searching for a camera in the hot path would violate registry and zero-GC mandates.
Solution: Cached `IPlayerRuntimeContext` at climb start and used `TryGetPlayerPoseSnapshot` to read the camera-facing vector. If the cached context is unavailable, the runtime falls back to already-cached `cameraSlideTarget` or `_playerRoot` transforms. The slip test is a normalized dot against `-_ladderUp` with threshold `0.9848077`, equivalent to within 10 degrees of straight down.
Rejected Alternatives: Per-frame `GlobalRegistry.Player` reads, `Camera.main`, or scene searches. Those are hot-path dependency violations and can allocate or hitch.
Scalability potential: Low/Middle/High/Ultra share the same dot-product Dear Lie; no physics query or raycast is needed.
Hardware Impact: Estimated 3 us/tick worst case for one pose read, one normalization, and one dot product; no GC.

Problem: DataVault handle resolution still had a helper path that could read `GlobalRegistry.DataVault` if called after cold setup, which violates the hot-path service-cache mandate even when rare.
Solution: Added `CacheVaultDependency()` for Awake/OnEnable/TryBegin only. `EnsureVaultBuffers()` now fails closed when `_dataVault` is null and no longer polls `GlobalRegistry` from helper paths used by schedule/consume/dump boundaries.
Rejected Alternatives: Keeping lazy registry polling in `TryResolve*` helpers. The property is cheap but forbidden by the mandate when the helper is reachable from tick.
Scalability potential: Low = no hidden registry polling; High/Ultra = same DataVault handles and typed outputs.
Hardware Impact: Runtime CPU impact is neutral to slightly lower. The main gain is architectural compliance, not measurable microseconds.

Problem: Loop 7 introduced one self-owned compile error: `SanitizeFinite(speed, 0f)` resolved to the float3 overload because the runtime lacked a float overload at that point.
Solution: Added the float `SanitizeFinite(float, float)` helper and removed the duplicate accidental definition. Latest `dotnet build Hecton8.Core.csproj --no-restore -nodeReuse:false -v:q` reports no ladder, `ProceduralLadder`, `ClimbableLadder`, `PhysiologyStateSignal`, or `PlayerStressSignal` errors.
Rejected Alternatives: Stopping at the first compile failure or editing the unrelated Fauna wall. The current build wall is `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs(1166,18): EnsureCoreCognitionVaultBuffers` missing, outside this prompt's domain.
Scalability potential: None; this is validation evidence.
Hardware Impact: 0 us runtime. Build remains PENDING VERIFICATION until the external Fauna dependency is repaired.

### Loop 8 - Registry Hygiene and Managed Delegate Purge
Problem: `ProceduralLadderClimbRuntime` still created a persistent runtime root with `DontDestroyOnLoad` and registered itself from `Awake()`. That is singleton-shaped persistence and violates the explicit `OnEnable`/`OnDisable` registry lifecycle mandate.
Solution: Removed `DontDestroyOnLoad`, made the generated runtime root scene-local, added a justification comment for `DefaultExecutionOrder`, and moved registry slot ownership into `OnEnable`/`OnDisable`. `Awake()` self-registration was deleted.
Rejected Alternatives: Keeping a permanent hidden runtime root or relying on `Awake()` order. Both hide ownership and are not required by the ladder XML.
Scalability potential: Low/Middle/High/Ultra use the same scene-local runtime recreation path; no scene-persistent manager survives after unload.
Hardware Impact: 0 us steady-state. Cold scene-load memory lifetime is cleaner; no profiler timing claim is made.

Problem: `ClimbableLadder` still exposed and invoked `UnityEvent` climb hooks, duplicating the typed climb state lane with managed delegate signaling.
Solution: Removed the UnityEvent import, serialized climb event fields, obsolete screen-fade field, stale player tag, and `OnClimbStart?.Invoke()` call. The authoritative cross-domain path is now `PlayerStateSignal`/`HapticRequest` from the procedural runtime.
Rejected Alternatives: Leaving optional designer delegates in place. Serialized convenience does not justify a second climb event surface when the prompt requires typed lanes.
Scalability potential: Low = no delegate fan-out on interaction; High/Ultra = VFX/audio/haptics subscribe to typed lanes without this adapter owning presentation callbacks.
Hardware Impact: Estimated 0-2 us saved on interaction-only climb start by avoiding UnityEvent invocation; hot path remains unchanged and zero-GC.

Problem: Current validation shifted again because other domains now fail before a full Core compile can finish.
Solution: Re-ran focused debt scans and `dotnet restore Hecton8.Core.csproj`; restore succeeded. Latest Core build reports `RepairTool.cs(1036,52): CS0165 localPoint` and many `World/SargassumMicroFaunaBoids.cs` CS0103 vault/native-field failures, with no ladder, procedural ladder, climb adapter, physiology, or stress-signal compiler symbols in the reported wall. Assembly restore/build was attempted and timed out after 306 seconds.
Rejected Alternatives: Editing Tools or World boid runtime from this prompt. They are outside `Animation/Locomotion` and not ladder cross-domain contracts.
Scalability potential: None; validation remains blocked by external compile debt.
Hardware Impact: 0 us runtime.

### Loop 9 - Teleport API Name Purge
Problem: `ClimbableLadder` still exposed public `TeleportToExit` and `TeleportToEntry` compatibility wrappers. They routed to the procedural climb runtime, but the method names preserved the old teleport contract and kept false debt markers in the ladder adapter.
Solution: Repository search found no live source references outside a deprecated documentation bundle, so the wrappers were replaced with `RequestClimbToExit` and `RequestClimbToEntry`. Static ladder-source scan now reports no teleport marker in `Assets/_Project/Scripts/Animation/Locomotion` or `Assets/_Project/Scripts/Gameplay/ClimbableLadder.cs`.
Rejected Alternatives: Keeping obsolete teleport names for compatibility. That would preserve the exact conceptual debt the prompt ordered removed.
Scalability potential: Low/Middle/High/Ultra unchanged; this is API truth cleanup, not a runtime path change.
Hardware Impact: 0 us runtime. The call still enters the same procedural climb request path.

Problem: Current Core validation wall shifted again after the Loop 9 patch.
Solution: Re-ran `dotnet build Hecton8.Core.csproj --no-restore -nodeReuse:false -v:q` with ladder-focused filtering. Latest wall is `Assets/_Project/Scripts/Ecosystem/EcosystemRuntimeInstaller.cs(1,18): CS0234` for missing `Hecton8.AI.Ecosystem`; no ladder, procedural ladder, climb adapter, physiology, or stress-signal symbols are present.
Rejected Alternatives: Editing Ecosystem/AI assembly contracts from the Animation/IK prompt. That is not a ladder cross-domain interface.
Scalability potential: None; validation remains blocked by external compile debt.
Hardware Impact: 0 us runtime.

### Loop 10 - Adapter Bloat Cleanup and Core Build Green
Problem: `ClimbableLadder` had no live teleport/delegate path left, but it still carried corrupted non-ASCII banner comments, an unused `Hecton8.Audio` namespace, and empty explanatory hover comments. The adapter was also one of the user-visible files in the ladder path, so leaving mojibake in it was unprofessional debt.
Solution: Rewrote `ClimbableLadder` as a compact ASCII interaction adapter while preserving the procedural climb request, localization cache, collider trigger setup, editor gizmos, and public `RequestClimbToExit`/`RequestClimbToEntry` API.
Rejected Alternatives: Leaving the banners because they are comments. Comments still damage maintainability and obscure the small interaction surface this adapter should expose.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged; this removes editor/source bloat without adding frame work.
Hardware Impact: 0 us runtime. The removed code was comments and an unused namespace.

Problem: The platform-layout scan still found two sequential structs in touched signal infrastructure without `Pack = 1`: `SpscSignalRingBuffer<T>` and `CombatDamageSignalAupShiftTransformer`.
Solution: Added `Pack = 1` to both sequential layouts and re-ran the layout scan across `Animation/Locomotion`, `GlobalSignals.cs`, and `ClimbableLadder.cs`; it returned no hits.
Rejected Alternatives: Treating them as harmless because they are not ladder payloads. They live in the same signal infrastructure and were inside a file already touched by this task.
Scalability potential: Quest/ARM64 and desktop use the same declared layout assumptions; tiering remains behavioral, not memory-layout dependent.
Hardware Impact: 0 us runtime. This is crash-risk reduction, not a frame-time optimization.

Problem: Previous Core validation was blocked by external project state. After the cleanup patch, a new `--no-restore` build first failed because `Temp/obj/Hecton8.Core/project.assets.json` was missing.
Solution: Ran `dotnet restore Hecton8.Core.csproj` successfully, then ran `dotnet build Hecton8.Core.csproj --no-restore -nodeReuse:false -v:minimal`. Result: `Hecton8.Core -> C:\hades\Hecton8\Temp\bin\Debug\Hecton8.Core.dll`, build succeeded, 0 warnings, 0 errors, 4.21 s.
Rejected Alternatives: Reporting the initial NETSDK1004 as a dependency wall. Restore was permitted and resolved that wall.
Scalability potential: None; this is compile validation evidence.
Hardware Impact: 0 us runtime. Status remains pending Unity/editor/profiler verification because no runtime capture was executed.

### Loop 11 - Owner Sentinel and Reentry Hardening
Problem: Ladder DataVault buffers and the scheduled IK job were still registered under `SystemID.GameplayPlayer`. The runtime is player-facing, but memory ownership belongs to the Animation/Locomotion solver, and the user explicitly called out SystemID correctness.
Solution: Added `SystemID.AnimationLocomotion = 150` and routed ladder input/output/AUP/telemetry/cursor DataVault handles plus `H8Memory.RegisterActiveJob` through a local `OwnerSystemId` constant.
Rejected Alternatives: Leaving the owner as `GameplayPlayer`. That hides animation memory under gameplay and makes sentinel attribution weaker during leak/postmortem review.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged; memory accountability is tighter across all tiers.
Hardware Impact: 0 us runtime. This is ownership correctness, not an optimization.

Problem: `TryBeginClimbInstance` could be called while a climb was already active or a solve job was scheduled. Interaction spam could reset state mid-climb.
Solution: Added an early reject for `_active`, `_pendingFinish`, or `_solveScheduled` before mutating climb state or touching DataVault views.
Rejected Alternatives: Letting `CompleteOutstandingJob()` reset an active climb. That creates nondeterministic embodiment state under repeated interactions.
Scalability potential: Low/Middle/High/Ultra all get deterministic single-owner climb sessions.
Hardware Impact: One boolean branch on cold climb request, 0 us steady-state.

Problem: Low-tier camera slide was skipped when `IPlayerMovementForceSink` existed; the runtime only queued movement velocity and applied head stabilization. The stricter XML requires linear camera Z/Y interpolation along the ladder vector in PC mode.
Solution: When the movement sink exists and low-tier slide is active, the runtime now still applies the absolute low-tier camera slide and FastNlerp stabilization after queueing the external velocity change.
Rejected Alternatives: Assuming the player movement sink always makes the camera motion visible. That is an integration assumption, not prompt-compliant presentation.
Scalability potential: Low = explicit camera slide; Middle/High/Ultra unchanged except for the same deterministic presentation target if low-tier mode is forced.
Hardware Impact: Existing estimate remains roughly 2 us/frame for one absolute `Vector3.Lerp` plus one FastNlerp when low-tier slide is active; no new heap allocation.

Problem: The latest Core build no longer reproduces the prior green state because unrelated repository files changed again.
Solution: Re-ran focused debt/layout/shader/IO scans and a filtered Core build. The debt/layout scans are clean for the ladder domain. Latest build wall is external `World/EcosystemDirector.cs` CS1612 native-view mutation errors; the filtered wall contains no `LadderClimb`, `ProceduralLadder`, `ClimbableLadder`, `AnimationLocomotion`, or `OwnerSystemId` failures.
Rejected Alternatives: Editing `World/EcosystemDirector.cs` from the Animation/IK prompt. That is outside the domain boundary.
Scalability potential: None; this is compile-wall isolation.
Hardware Impact: 0 us runtime.

### Loop 12 - Explicit Registry Slot
Problem: `ProceduralLadderClimbRuntime` had a concrete GlobalRegistry field and register/clear methods, but `ResolveServiceSlotCold(typeof(ProceduralLadderClimbRuntime))` still fell through to `GlobalRegistryServiceSlot.Unknown`. That weakens ghost-service diagnostics and makes unregister-time memory reaping unable to associate the service slot with the new animation owner.
Solution: Added `GlobalRegistryServiceSlot.ProceduralLadderClimbRuntime = 172`, appended the slot name, mapped `ResolveMemoryOwner` to `SystemID.AnimationLocomotion`, and added the concrete type to `ResolveServiceSlotCold`.
Rejected Alternatives: Leaving the runtime as an unknown registry service. That preserves exactly the attribution hole the owner-sentinel patch was meant to close.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged; registry diagnostics and leak attribution are now deterministic for the ladder runtime.
Hardware Impact: 0 us runtime. This is cold-path registry metadata only.

Problem: After restore, the latest Core build wall moved again to an unrelated syntax failure in `SubmarineFluidDynamics.cs`.
Solution: Re-ran filtered `dotnet build Hecton8.Core.csproj --no-restore -nodeReuse:false -v:q`. The wall is `SubmarineFluidDynamics.cs` CS1001/CS1003/CS8124 syntax errors around lines 2051-2075. Filtered output contains no `LadderClimb`, `ProceduralLadder`, `ClimbableLadder`, `AnimationLocomotion`, `OwnerSystemId`, or `GlobalRegistryServiceSlot` errors.
Rejected Alternatives: Editing submarine fluid syntax from the Animation/IK prompt. That is outside the domain boundary.
Scalability potential: None; this is compile-wall isolation.
Hardware Impact: 0 us runtime.

### Loop 13 - Runtime NativeArray View Eviction
Problem: `ProceduralLadderClimbRuntime` no longer owned native arrays, but its helper signatures still exposed local `NativeArray<T>` out parameters. That kept the system looking like a data owner during static audit even though the arrays were DataVault views.
Solution: Added a packed `LadderClimbIkVaultViews` packet in `LadderClimbIkJobs.cs` and refactored the runtime to read/write through that packet. `ProceduralLadderClimbRuntime.cs` now has zero `NativeArray<T>` declarations; `NativeArray<T>` appears only in the vault-view packet and Burst job fields.
Rejected Alternatives: Leaving `out NativeArray<T>` helper signatures and explaining them in documentation. The code shape should make DataVault ownership obvious without relying on a report.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged; data ownership is clearer and still vault-backed.
Hardware Impact: 0 us runtime intended. The packet wraps the same resolved native views and does not allocate.

Problem: Adding the vault-view packet introduced another struct in the IK file that needed explicit layout.
Solution: Annotated `LadderClimbIkVaultViews` with `[StructLayout(LayoutKind.Sequential, Pack = 1)]` and re-ran the missing-pack scan; it returned no hits for the ladder path and touched signal path.
Rejected Alternatives: Leaving the view packet unannotated because it is not persisted. The project mandate is stricter than pure persistence needs.
Scalability potential: Quest/ARM64 and desktop use identical declared layout for the packet.
Hardware Impact: 0 us runtime.

Problem: The latest Core build wall moved again while validating the refactor.
Solution: Re-ran filtered Core build. Latest wall is external `UI/Navigation/DiegeticGyroCompassRuntime.cs` missing members/overload mismatches plus `World/EcosystemDirector.cs` generic native pointer inference errors. Filtered output contains no `LadderClimb`, `ProceduralLadder`, `ClimbableLadder`, `AnimationLocomotion`, `OwnerSystemId`, or `GlobalRegistryServiceSlot` errors.
Rejected Alternatives: Editing UI compass or World ecosystem code from the Animation/IK prompt.
Scalability potential: None; this is compile-wall isolation.
Hardware Impact: 0 us runtime.

### Loop 14 - Signal Semantics and Ordered Blackbox
Problem: Climb shutdown packets still used `PlayerStateSignal.StateClimbing` even after `_active` had been cleared. That made latest-state consumers store an inactive climb packet as a climb state and hid the difference between finished, active, and slip terminal states.
Solution: Added explicit `PlayerStateSignal.StateNone = 0`; active climb now publishes `StateClimbing + FlagActive + FlagClimbing`, terminal slip publishes `StateClimbing + FlagClimbing + FlagLadderSlip`, and finished climb publishes `StateNone` with only AUP-safe metadata.
Rejected Alternatives: Leaving state clearing implicit in missing flags. Consumers already cache `State`, so the packet should carry a literal inactive state instead of relying on readers to reverse-engineer inactive flags.
Scalability potential: Low/Middle/High/Ultra all get deterministic state clear semantics with the same 64-byte signal payload; no new lane was invented.
Hardware Impact: One scalar branch in the signal builder, estimated 0-1 us only on state publication.

Problem: Climb physiology was publishing neutral zero-stress packets during frames with no climb movement. Because `PlayerStressSignal` is consumed as a latest snapshot by audio, visor, and IK consumers, neutral climb spam could overwrite a more meaningful stress producer.
Solution: Suppressed neutral climb stress publication unless a slip is pending, added `FlagActive | FlagClimbing` to non-neutral physiology/stress packets, and emits a minimum slip stress impulse when the player drops.
Rejected Alternatives: Continuing to publish `Stress01 = 0` every active ladder tick. That preserves a false latest signal and makes cross-domain stress composition cache-hostile.
Scalability potential: Low = fewer neutral lane writes; High/Ultra = richer stress/heartbeat reaction only when movement or slip justifies it.
Hardware Impact: Saves up to two signal publishes on stationary ladder frames. Estimated 2-4 us avoided per idle climb tick; no profiler proof claimed.

Problem: The blackbox ring recorded the last 300 frames but the cold dump wrote raw array order, not chronological ring order after wrap.
Solution: Bounded telemetry cursor wrap to the actual dump capacity and exported entries oldest-to-newest from `TelemetryCursor[0]`. The runtime dump writes exactly the active dump count, capped at `BlackBoxFrameCapacity`.
Rejected Alternatives: Raw index dump with an external parser guessing cursor order. Postmortem data must be self-evident.
Scalability potential: Low/Middle/High/Ultra unchanged; cold postmortem quality improves without frame cost.
Hardware Impact: 0 us hot path beyond replacing one modulo capacity constant. Dump ordering is cold NaN/crash path only.

Problem: Latest validation is again blocked outside the ladder domain.
Solution: Re-ran filtered `dotnet build Hecton8.Core.csproj --no-restore -nodeReuse:false -v:q`. The wall is `Core/Determinism/LockstepStateValidator.cs` missing `LockstepSnapshotSignalCapacity`, `LockstepSnapshotLaneHash`, `SystemGlitchSignalCapacity`, and `SystemGlitchLaneHash`. Filtered output contains no `LadderClimb`, `ProceduralLadder`, `ClimbableLadder`, `AnimationLocomotion`, `PlayerStateSignal`, or registry owner errors.
Rejected Alternatives: Editing Determinism constants from the Animation/IK prompt. That is outside the domain boundary.
Scalability potential: None; this is compile-wall isolation.
Hardware Impact: 0 us runtime.

### Loop 15 - VR Embodiment Sign and Rotation Polish
Problem: VR grip input accepted hand deltas, but the progress resolver treated the submitted delta as player progress in the same direction. For embodied ladder climbing, moving the gripped hands down should pull the world/player up. A no-grip packet also needed to clear stale pending grip state immediately, and clean VR finish should not snap the root rotation to a ladder endpoint.
Solution: `SubmitUniversalInputState` now clears `_pendingGripPullMeters` and `_pendingGripMask` whenever the grip bit is absent. `ResolveProgressDelta` consumes grip motion with inverted sign, then clears the pending packet after one frame. `StopClimb` now applies the endpoint root-rotation snap only for non-VR clean finishes.
Rejected Alternatives: Treating controller deltas as same-direction climb progress or leaving rotation snapping enabled for VR. Same-direction deltas create wrong body mechanics, and forced root snapping can fight HMD/controller authority at ladder exit.
Scalability potential: Low = unchanged camera-slide Dear Lie; Middle = deterministic non-VR movement with FastNlerp head stabilization; High = grip-gated hand-pull climb with correct inverse motion; Ultra = richer haptic/heartbeat consumers can build on the same rung-lock and stress signals without changing the gameplay truth lane.
Hardware Impact: 0 us intended steady-state change. The patch changes sign/branch semantics on existing scalar work; no new allocation or per-frame service lookup was added.

Problem: Latest validation moved again after unrelated repository changes.
Solution: Re-ran filtered Core build. The wall is external `UI/Navigation/DiegeticGyroCompassRuntime.cs` DTO/presentation drift plus `Core/SystemDispatcher.cs` missing `DisposeDispatcherBlackBox` and `EnsureDispatcherBlackBox`. Filtered output contains no `LadderClimb`, `ProceduralLadder`, `ClimbableLadder`, `AnimationLocomotion`, `PlayerStateSignal`, or registry owner errors.
Rejected Alternatives: Editing UI compass or Core dispatcher blackbox helpers from the Animation/IK prompt. Those files are outside the ladder domain and not required cross-domain contracts for procedural ladder climb.
Scalability potential: None; this is compile-wall isolation.
Hardware Impact: 0 us runtime.

### Loop 16 - Typed Input Lane Grip Mask Hardening
Problem: VR grip semantics still had a contract defect. The serialized default grip mask was formerly `1 << 6`, but the authoritative Core input map exposes `Interact = 1 << 1` and `SecondaryFire = 1 << 3`; XR grip publishes both through `InputDispatcher.ResolveXRToolActionBitsAndPublishSignal`. A scene carrying the old serialized mask could fail to clear or accept grip correctly.
Solution: Default grip resolution now uses `PlayerInputAction.Interact | PlayerInputAction.SecondaryFire`, treats legacy serialized `1 << 6` as stale data, and ORs designer-configured masks with the Core grip bits. `ProceduralLadderClimbRuntime` also consumes `SignalBus<InputStateSignal>.GetFrameSnapshot()` as `ReadOnlySpan<InputStateSignal>` each hot tick while VR grip mode is active, using the sequence field to skip already-consumed packets and clear pending grip pull when the latest typed packet has no grip.
Rejected Alternatives: Adding a new `UniversalInputStateSignal` lane, taking a direct dependency on the input assembly, or polling an input service through the registry every frame. Those options either duplicate an existing typed lane, deepen assembly coupling, or add a per-frame service lookup.
Scalability potential: Low = unchanged PC camera-slide Dear Lie; Middle = deterministic typed input clear without custom input ownership; High = VR grip pull now follows the actual Core XR grip bits; Ultra = richer haptic and stress consumers can trust the same typed input and rung-lock truth without a duplicate signal.
Hardware Impact: Estimated 1-2 us/frame only while VR grip mode is active for a bounded 64-entry signal snapshot scan; zero allocation. The legacy mask guard is scalar cold/hot branch work with no measurable claim. Static validation found no forbidden ladder-domain patterns, no missing `Pack = 1`, and no runtime `NativeArray<T>` declarations. Latest Core build is blocked outside the domain by `Assets/_Project/Scripts/TetherInstance.cs` missing `IsFrameCooldownActive`; no ladder/runtime/input-lane symbols appeared in the filtered wall.

### Loop 17 - Grip Truth and Blackbox Retained Count
Problem: Grip-held state and hand-pull distance were still coupled. A VR input packet could say grip is held while no hand delta arrives that frame; the runtime would then resolve `_lastResolvedGripMask = 0`, and looking down could trigger the release-slip branch even though the player was still holding grip.
Solution: Added `_currentInputGripHeld` as scalar runtime state separate from `_pendingGripPullMeters`. `SubmitUniversalInputState`, `SubmitGripPullDelta`, and `ConsumeInputStateSignals` now update held/released truth independently from pull distance; a zero-mask direct hand-delta packet clears grip state, while a held zero-delta packet blocks release-slip but produces no movement.
Rejected Alternatives: Treating lack of hand delta as release, or forcing every caller to submit fake zero hand deltas each frame. Both make embodiment depend on caller behavior instead of typed grip truth.
Scalability potential: Low = unchanged PC camera-slide Dear Lie; Middle = deterministic no-motion held grip; High = VR pull still needs real hand delta for progress; Ultra = look-down slip becomes a real release-gated fail state instead of an input-bridge artifact.
Hardware Impact: One bool branch in VR grip mode, estimated below 1 us/frame; no allocation and no new signal lane.

Problem: The 300-frame blackbox ring wrote full capacity on dump even when fewer than 300 samples had been recorded, so a short-session NaN dump could serialize uninitialized cleared entries before the real frames.
Solution: Expanded the vault-owned telemetry cursor lane to two ints: next-write index and retained-sample count. The Burst job updates both in the DataVault, `EnsureVaultBuffers()` grows stale one-int handles to the required two-int lane, and `DumpBlackBox()` writes only the retained sample count, oldest-to-newest after wrap.
Rejected Alternatives: Keeping a private managed retained-count field, parsing zero hashes during dump, or trusting old one-int cursor handles after the layout change. A private field violates data sovereignty, zero-hash filtering is ambiguous post-crash, and stale handles can silently block solve capacity.
Scalability potential: Low/Middle/High/Ultra all get a deterministic postmortem payload without extra hot-path allocations. The retained count improves crash evidence quality on short test sessions and still caps at exactly 300 frames.
Hardware Impact: One additional int read/write in the Burst telemetry path, estimated below 1 us/frame. Static validation found no forbidden ladder-domain patterns, no missing `Pack = 1`, and no runtime `NativeArray<T>` declarations. Latest `dotnet build Hecton8.Core.csproj --no-restore -nodeReuse:false -v:q` returned `CORE_BUILD_EXIT:0`. Unity editor/profiler verification remains unrun.

### Loop 18 - Player State AUP Truth
Problem: `PlayerStateSignal.PositionAup` still published the ladder base AUP. That made the typed climb lane truthful about state and progress intensity, but false about position; HUD, physiology, or downstream consumers reading the latest player state could anchor effects or diagnostics at the ladder entry instead of the player's current rung position.
Solution: Added `ResolveCurrentClimbAup(in ladderAup)` in `ProceduralLadderClimbRuntime`. The signal now computes current climb AUP by clamping sanitized progress, normalizing `_ladderUp`, converting the offset to `double3`, and calling `AbsoluteUniversePosition.OffsetMeters(in ladderAup, offsetMeters)`.
Rejected Alternatives: Publishing `_playerRoot.position` with `FromRuntimePosition` or leaving `Intensity01` to imply position. Transform authority would contradict the AUP mandate, and intensity-only consumers cannot recover an absolute position without duplicating ladder state.
Scalability potential: Low = PC camera-slide signal now reports the correct rung-space AUP; Middle/High/Ultra = VR grip, haptics, HUD, and physiology consumers can share one position truth without new lanes or transform reads.
Hardware Impact: One double3 multiply plus one AUP offset conversion per climb-state publish, estimated below 1 us/event. Static validation found no forbidden ladder-domain patterns, no missing `Pack = 1`, and no runtime `NativeArray<T>` declarations. Latest `dotnet build Hecton8.Core.csproj --no-restore -nodeReuse:false -v:q` returned `CORE_BUILD_EXIT:0`. Unity editor/profiler verification remains unrun.

### Loop 19 - Burst Job Layout Closure
Problem: The ARM64 layout audit confirmed the ladder data packets were packed, but the Burst job wrapper `LadderClimbIkSolveJob` still relied on implicit struct layout. Even if the job is not persisted, the mandate requires explicit layout for structs in the ladder-owned path.
Solution: Added `[StructLayout(LayoutKind.Sequential, Pack = 1)]` to `LadderClimbIkSolveJob` while leaving the vault-owned NativeArray views in `LadderClimbIkVaultViews`.
Rejected Alternatives: Treating job wrappers as exempt from the layout rule. That leaves a static audit hole on Quest/Android and makes future field changes ambiguous.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged; the explicit layout removes platform ambiguity without changing math LOD, VR grip, or low-tier Dear Lie paths.
Hardware Impact: 0 us intended runtime change. Layout metadata only; latest `dotnet build Hecton8.Core.csproj --no-restore -nodeReuse:false -v:q` returned `CORE_BUILD_EXIT:0`. Unity editor/profiler verification remains unrun.

### Loop 20 - Non-Blocking Ladder Job Drain
Problem: `LateFrameTick()` forced `_solveHandle.Complete()` every time a ladder solve was scheduled. In the common case the tiny IK job should already be complete, but under worker-thread pressure this can serialize the main thread and create the exact Steam Deck/MX350 hitch the job system is meant to avoid.
Solution: Added an `_solveHandle.IsCompleted` gate before `Complete()`. The runtime now drains finished solves only; if the job is still running, the climb holds the current frame state and waits for the next late-frame pass. Cold teardown and new-climb setup still force completion because they are ownership boundary points.
Rejected Alternatives: Forcing same-frame freshness or adding a local double buffer outside the vault. Same-frame freshness risks stalls, and local native state would violate the DataVault ownership rule.
Scalability potential: Low/MX350 and Steam Deck avoid an unbounded wait under worker pressure; Middle/High/Ultra retain the same exact rung solve, haptics, VR grip semantics, and telemetry once the job is ready.
Hardware Impact: 0 us steady-state change when the job is already complete. Under load this avoids waiting for the remaining worker time, but no profiler capture was run and no microsecond saving is claimed. Latest `dotnet build Hecton8.Core.csproj --no-restore -nodeReuse:false -v:q` is blocked outside the ladder domain by dirty `PlayerKinematicsRuntime` missing `IScalabilityChangedEventListener.OnScalabilityChanged(in ScalabilityChangedEvent)`; filtered output contains no ladder symbols.

### Loop 21 - Signal and Haptic Coalescing
Problem: The runtime could publish identical `PlayerStateSignal` packets twice in the same frame, once before scheduling and again after the IK solve drained. It could also emit two haptic packets in the same solve when both hands changed rung index.
Solution: Added a same-frame climb-state publish cache keyed by frame, state, flags, and millimeter-quantized progress. Added one coalesced rung-lock haptic request per solve, using a stronger pulse when both hands lock in the same output.
Rejected Alternatives: Leaving consumers to deduplicate latest-state packets or keeping one haptic packet per hand. Consumers should not pay for producer spam, and dual haptic packets in the same frame add lane pressure without materially improving feedback.
Scalability potential: Low/MX350 and Steam Deck get fewer typed lane writes during active climb; Middle/High/Ultra keep the same exact rung truth, AUP truth, VR grip semantics, and blackbox telemetry.
Hardware Impact: No measured microsecond claim. Static code path reduction can avoid up to one duplicate `PlayerStateSignal` publish on a no-change same-frame drain and one duplicate `HapticRequest` publish when both hands lock simultaneously. Latest `dotnet build Hecton8.Core.csproj --no-restore -nodeReuse:false -v:q` is blocked outside the ladder domain by Gameplay motor, Interaction contract, and Tether API drift; filtered output contains no ladder symbols.

### Loop 22 - Cold Blackbox Span Writer
Problem: `DumpBlackBox()` still performed project-root probing, path combining, and `BinaryWriter` wrapping inside the NaN/crash dump path. That work is cold, but it is still the worst moment to allocate extra managed helpers or let dump I/O throw back into the animation runtime.
Solution: Pre-create the relative `Docs/AgentLogs` dump directory during `OnEnable`. The fault path now uses the constant `Docs/AgentLogs/Dump_LADDER_CLIMB_IK.bin`, writes the existing capacity/retained-count header with `BinaryPrimitives`, and serializes each telemetry sample into a fixed 85-byte `stackalloc` span before a single stream write per entry. The dump is wrapped in a catch block so postmortem export cannot become a second crash.
Rejected Alternatives: Adding a new Core crash-dump service, switching to a memory-mapped writer from the Animation/IK prompt, or leaving `BinaryWriter` in place. Core service design is outside the ladder domain, memory-mapped dump ownership needs a shared diagnostics contract, and `BinaryWriter` keeps unnecessary managed wrapper work in the fault path.
Scalability potential: Low/MX350 and Steam Deck keep normal climb hot paths unchanged and avoid directory/path/writer churn when a fault dump occurs. Middle/High/Ultra keep exact retained chronological telemetry with no change to rung solve, VR grip embodiment, or haptic signal truth.
Hardware Impact: 0 us hot-path change. Cold fault-path allocation pressure is reduced by removing `BinaryWriter`, `File.Open`, project-root `DirectoryInfo`, and per-dump `Path.Combine`; no profiler or microsecond measurement is claimed. Latest `dotnet build Hecton8.Core.csproj --no-restore -nodeReuse:false -v:q` returned `CORE_BUILD_EXIT:0`. Unity editor/profiler/platform verification remains unrun.

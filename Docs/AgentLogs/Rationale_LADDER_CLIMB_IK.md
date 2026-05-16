# Rationale_LADDER_CLIMB_IK

Runtime Status: PENDING VERIFICATION - LOOP 9 HARDENED; CORE BUILD BLOCKED BY DEPENDENCY

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

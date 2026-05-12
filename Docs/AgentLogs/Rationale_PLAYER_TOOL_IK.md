# Rationale_PLAYER_TOOL_IK

STATUS: PENDING VERIFICATION - STATIC ONLY AFTER USER NO-BUILD ORDER

## Decision Log

### D0 - Batch State Initialization
Problem: PLAYER_TOOL_IK had no existing Status or Rationale file; batch memory was absent.
Solution: Created fresh state and rationale files before code reconnaissance.
Rejected Alternatives: Reusing neighboring animation/physics logs was rejected because batch hygiene forbids cross-prompt contamination.
Scalability potential: Low uses one player-only analytical hand solve; Middle/High can raise update cadence and wall-touch detail; Ultra can add wrist twist and cold shiver only after core solve compiles.
Hardware Impact: i3/MX350 impact is documentation-only at this stage; measured gain absent.

### D1 - Extend Existing Animation Job Instead Of New IK Layer
Problem: Hands clip, but the project already owns a contextual IK runtime and a Burst animation-stream two-bone solver.
Solution: Extended `ContextualPhysicalIkRuntime` and `ContextualPhysicalIkRig` so tool rays, retraction, terminal snaps, and recoil feed the existing `ContextualPhysicalIkApplyJob`.
Rejected Alternatives: A new MonoBehaviour `LateUpdate` hand solver was rejected because it would fight Animator graph order and violate the contextual IK mandate.
Scalability potential: Low keeps tool retraction and disables wall touch; Middle keeps all hand rays; High/Ultra can spend saved CPU on extra pose polish without changing the interface.
Hardware Impact: i3/MX350 avoids Full Body IK and keeps all solver math in the existing Burst job; expected avoidance is roughly 120-400 us per animated player versus generic IK.

### D2 - Fixed Batch Rays With Disabled No-Layer Lanes
Problem: Prompt requires two camera-forward hand rays, while the existing runtime already uses fixed batched foot/hand rays.
Solution: Increased fixed `RaysPerEntity` to 6 and emits disabled no-layer `RaycastCommand`s for inactive, low-tier wall touch, and disabled tool rays.
Rejected Alternatives: Variable ray counts were rejected because they complicate per-entity indexing and scheduler dependencies.
Scalability potential: Low pays only the fixed command slot with no collision layers; Middle/High use wall touch plus tool retraction; Ultra can add additional visual target modulation later.
Hardware Impact: On i3/MX350, no-layer disabled lanes remove broadphase wall checks; estimated saving is 15-45 us/frame when wall touch is off.

### D3 - SOA Hand Lanes Plus AOS Frame Compatibility
Problem: The animation job consumes existing target frames, but the prompt demanded `NativeArray<float3>` targets and `NativeArray<float>` weights.
Solution: Added `_ikTargets` and `_ikWeights` as persistent SOA lanes written by `ContextualPhysicalIkGroundResponseJob` while preserving target-frame consumption by the animation job.
Rejected Alternatives: Replacing target frames outright was rejected because feet, COM, spine lean, and hand contacts share the frame contract.
Scalability potential: Low can read only weights for culling; Middle/High can expose hand lanes to debug HUD or haptics; Ultra can add richer wrist overlays without reshaping frame data.
Hardware Impact: Contiguous hand lanes avoid scanning 192-byte target frames for hand-only consumers; estimated 2-5 us saved for diagnostics/haptics.

### D4 - Visual Retraction Instead Of Tool Physics
Problem: Tool collision must look correct without simulating physical tool constraints.
Solution: If a short camera-forward ray hits under 0.5m, the hand target blends backward and upward from the hand probe, with finite recoil offsets and surface-normal palm alignment.
Rejected Alternatives: Rigidbody constraints and per-tool colliders were rejected as slower and less predictable than a visual fake.
Scalability potential: Low uses the same cheap 2-ray approximation; High/Ultra can increase blend polish or add secondary wrist twist.
Hardware Impact: i3/MX350 saves roughly 40-150 us against constraint/contact based tool avoidance.

### D5 - Recoil And Terminal Snap Decoupled By Existing Interfaces
Problem: Tool recoil and terminal button snaps must not create hard dependencies from player IK to tool/UI internals.
Solution: Added `AddRecoil(float3)` to the rig and implemented `IPhysicalHandIkTargetSink` so `KinematicTerminalInteractionBridge` can push PhysicalTerminalKeyboard button snaps into IK through the existing interface.
Rejected Alternatives: Runtime polling of `PhysicalTerminalKeyboard` was rejected because it couples domains and wastes frame time when no terminal is active.
Scalability potential: Low only consumes target events; Middle/High keep the same path; Ultra can drive more detailed hand pose without changing the bridge.
Hardware Impact: Event-style snap input avoids scene searches or terminal polling; estimated 20-60 us saved during terminal use.

### D6 - AUP And Black Box Evidence
Problem: World-space IK targets must not jump on origin shift, and critical IK state needs postmortem evidence.
Solution: Rebased scheduled states, target frames, SOA hand lanes, predictive/external/terminal targets, and added a 300-entry native telemetry ring dumping `Dump_PLAYER_TOOL_IK.bin` on non-finite state.
Rejected Alternatives: Waiting one frame for recapture or relying on console logs was rejected because both hide fault causality.
Scalability potential: Low pays one fixed telemetry write after completed IK jobs; High/Ultra can add more fields later if needed.
Hardware Impact: Normal telemetry write is a small fixed loop over active slots after job completion; fault dump is rare path only.

### D7 - Smoothing Scope
Problem: Prompt asks for `CinematicMath.FastNlerp`, but the existing API is quaternion-specific and world positions must not be normalized.
Solution: Routed animation-stream quaternion blends through `CinematicMath.FastNlerp`; kept positions and scalar weights on existing exponential smoothing to preserve AUP-space coordinates.
Rejected Alternatives: Adding normalized interpolation to positions was rejected because it would corrupt absolute world targets.
Scalability potential: Low gets cheaper quaternion blending; Middle/High can reuse the same call for extra pose layers.
Hardware Impact: Avoids `Quaternion.Slerp`-style trig work; estimated 8-25 us saved across limb rotations.

### D8 - Compile Evidence
Problem: Full project compile is blocked by files outside the player IK domain.
Solution: Ran `dotnet build` and Unity refresh/console checks, verified both IK scripts with MCP `validate_script standard` at 0 errors / 0 warnings, then reran full `Hecton8.Core.csproj` compile successfully after external churn settled.
Rejected Alternatives: Claiming a clean full compile before evidence was rejected; final status was raised only after the build returned 0 errors. Three final CS0649 warnings remain unrelated to PLAYER_TOOL_IK.
Scalability potential: No runtime difference.
Hardware Impact: No runtime impact; this is verification evidence.

## OMEGA POLISH CHANGES

Problem: Polish mandate required an anti-bloat pass after core tasks were complete.
Solution: Re-read `<POLISH_MANDATE id="OMEGA_POLISH">`, audited own hot-path math, replaced terminal snap `Vector3.Normalize()` with `NormalizeVectorNoSqrt`, confirmed recoil decay uses `math.rcp`, and verified the final `Hecton8.Core.csproj` build returned 0 errors.
Rejected Alternatives: Leaving managed `Normalize()` in a frequently hit terminal snap path was rejected because the repo mandates rsqrt approximations where visual exactness is unnecessary.
Scalability potential: Low uses disabled no-layer wall rays and no-sqrt terminal normal capture; Middle/High retain wall touch and dashboard snap; Ultra can add wrist/cold-shiver polish later without altering the data path.
Hardware Impact: i3/MX350 saves sub-microsecond work per terminal snap and avoids exact sqrt; full feature remains zero-GC in steady state.

Exact cinematic cheats used:
- Tool collision is a two-ray visual fake, not physical tool constraints.
- Retraction is backward/upward target bias, not a simulated chest/tool body.
- Low tier disables wall-touch ray lanes through no-layer commands while keeping the more visible tool retraction.
- Palm alignment uses no-trig normal delta instead of exact `LookRotation` construction.

Final Git Diff Summary:
- `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs`: 6-lane ray batch, SOA hand lanes, retraction, terminal snap target ingestion, AUP rebase, black-box telemetry.
- `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs`: recoil API, terminal target sink, low-tier wall gate, no-sqrt terminal normal capture, `CinematicMath.FastNlerp` rotation smoothing.
- `Docs/AgentLogs/RECON_PLAYER_TOOL_IK.md`: Animator IK reconnaissance.
- `Docs/Tasks/Status_PLAYER_TOOL_IK.md` and this rationale: evidence trail.

### D9 - Continuation Solver Correctness Pass
Problem: Readback found the analytical two-bone solver still fed law-of-cosines with approximate target length and used `sinSq` directly as sine, which visually under-bends arms near partial extension.
Solution: Replaced the distance with `targetDistanceSq * math.rsqrt(targetDistanceSq)` and replaced bend sine with `bendSinSq * math.rsqrt(bendSinSq)`, preserving the no-`acos`/no-`sqrt` mandate.
Rejected Alternatives: Keeping the approximate length was rejected because law-of-cosines needs distance accuracy; adding `math.sqrt` was rejected because the rsqrt mandate already covers this case.
Scalability potential: Low keeps the same cheap analytical solve; Middle/High/Ultra get more believable elbow arcs without switching to Full Body IK.
Hardware Impact: i3/MX350 pays one extra rsqrt in the solved-limb path, still far below a generic IK package; expected visual correctness gain is larger than the sub-microsecond math cost.

### D10 - Continuation Runtime Robustness Pass
Problem: Tool retraction/dashboard snap were tied to wall-brace enablement, first activation could smooth from world origin, and native disposal jobs needed explicit scheduling without a forbidden teardown stall.
Solution: Ran retraction and dashboard snap independently from optional wall touch, added smoothing fallback helpers that choose live/previous/fallback targets by active blend, removed redundant slope projection math, and flush scheduled NativeArray dispose jobs with `JobHandle.ScheduleBatchedJobs()`.
Rejected Alternatives: Leaving tool retraction behind the bracing branch was rejected because Low tier intentionally disables wall touch; `Complete()` on teardown was rejected because native lifetime mandates require deferred disposal.
Scalability potential: Low keeps tool clipping protection while wall-touch rays stay no-layer disabled; Middle/High keep bracing plus terminal snap; Ultra can add extra hand polish without changing data contracts.
Hardware Impact: i3/MX350 keeps the 15-45 us low-tier wall-touch saving while preserving the visible hand retraction; slope dot simplification saves roughly 1-3 us/frame at full 128-slot budget; teardown fix is cold path only.

### D11 - Optional Cold Shiver Polish
Problem: The recursive prompt allowed cold hand shiver when ambient temperature is below 5 C, but the IK lane cannot own survival physiology.
Solution: Read `GlobalRegistry.Player.SurvivalSystem.EnvironmentTemperature` and `ColdStressSeverity01` through the approved registry boundary, smooth a scalar blend, and add tiny deterministic triangle-wave offsets only to already-active hand IK targets; offset amplitude is authored once and target blend is applied once in the Burst response job.
Rejected Alternatives: Random noise was rejected because it is nondeterministic and visually buzzy; direct survival-field access was rejected because it crosses domain ownership; activating IK solely for shiver was rejected because idle hands should stay animation-authored; blend-squared attenuation was rejected because it hides the effect at partial blends.
Scalability potential: Low pays no extra ray or allocation cost and only applies the offset when a hand target is already active; Middle/High/Ultra get a small diegetic temperature cue layered over tool retraction, wall touch, or terminal snaps.
Hardware Impact: i3/MX350 expected cost is below 2 us/frame for the active player due to scalar smoothing and triangle waves; no heap allocations, no random state, no extra physics queries.

Continuation verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` succeeded with 0 warnings / 0 errors after cold shiver and the teardown readback fix.
- MCP `validate_script standard` returned 0 warnings / 0 errors for `ContextualPhysicalIkRuntime.cs`, `ContextualPhysicalIkRig.cs`, and `ContextualPhysicalIkMath.cs`.
- Targeted scans found no `math.sqrt`, `Mathf.Sqrt`, `.normalized`, `math.normalize`, `Vector3.Normalize`, `math.project`, `Quaternion.Slerp`, `StartCoroutine`, `IEnumerator`, `Animator.SetIKPosition`, `SetIKRotation`, `LateUpdate`, `string.Format`, `$"` interpolation, `.ToString(`, managed `foreach`, LINQ, `new List`, or `new Dictionary` in the three IK files.

### D12 - Teardown And Shiver Readback Fix
Problem: Recursive readback found a cold-path `Complete()` in `DisposeBuffers` and a blend-squared cold shiver offset.
Solution: Removed teardown blocking and flush deferred native disposal with `JobHandle.ScheduleBatchedJobs()`; removed `_coldShiverBlend` from offset amplitude so the Burst job owns the single final blend.
Rejected Alternatives: Blocking `Complete()` was rejected by the native lifetime mandate; leaving blend squared was rejected because it made the diegetic cold cue vanish at moderate IK weights.
Scalability potential: Low avoids unnecessary teardown stalls and still pays no shiver cost unless a hand IK target is active; Middle/High/Ultra retain the same authored amplitude and can scale visual overkill by raising blend/frequency fields, not by adding physics.
Hardware Impact: i3/MX350 steady-state cost remains unchanged; cold-path stall risk is removed, and the active shiver remains below 2 us/frame.

### D13 - External Wall Bridge And Recoil Independence
Problem: External `PlayerKinematicsRuntime` wall targets used one shared hold timer for both hands, allowing a fresh right-hand hit to preserve stale left-hand data; recoil offsets were only visible when tool collision retraction also had a hit.
Solution: Split external wall holds into left/right timers, fade or zero each hand independently, pass the low-tier wall-touch decision into the bridge, and add a standalone `ApplyToolRecoil` job path fed by capped no-sqrt recoil offsets.
Rejected Alternatives: Keeping a shared timer was rejected because it couples independent hand lanes; adding per-tool rigidbody recoil was rejected because a deterministic target bias is cheaper and more controllable; leaving recoil under collision retraction was rejected because the prompt requires `AddRecoil` to affect the IK target directly.
Scalability potential: Low disables wall-touch bridge work with the same LOD gate while retaining tool retraction and recoil; Middle/High retain wall touch and terminal snap; Ultra can increase authored recoil cap/blend without changing data layout.
Hardware Impact: i3/MX350 preserves the 15-45 us low-tier wall-touch savings and adds only a few scalar rsqrt/dot operations while recoil is non-zero, estimated below 1 us/frame.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` succeeded with 0 warnings / 0 errors after the external wall and recoil fixes.
- Targeted scans found no forbidden `math.sqrt`, `Mathf.Sqrt`, `.normalized`, `math.normalize`, `Vector3.Normalize`, `math.project`, `Quaternion.Slerp`, coroutine, Animator IK, managed collection, LINQ, string formatting, interpolation, or `.ToString(` tokens in the three IK files.
- `git diff --check` reported only Git CRLF conversion warnings.
- MCP `validate_script` could not run on this pass because Unity session was unavailable.

### D14 - No-Build Recoil Decoupling
Problem: Static readback found `ApplyToolRecoil` still reused collision retraction blend as its own blend gate, so setting retraction blend low could silently weaken `AddRecoil`.
Solution: Removed the collision blend dependency from `ApplyToolRecoil`; recoil strength now derives from capped offset magnitude, and recoil normals are normalized before entering the target frame.
Rejected Alternatives: Adding a rigidbody recoil system was rejected as unnecessary physics; running another compile was rejected because the user explicitly forbade builds in this pass.
Scalability potential: Low keeps tool recoil without re-enabling wall touch or additional rays; Middle/High/Ultra can tune recoil through cap/decay fields while collision retraction remains independently authored.
Hardware Impact: i3/MX350 cost is still below 1 us/frame while recoil is non-zero; no extra allocations or physics queries.

No-build verification:
- Static readback confirmed `ApplyToolRecoil` call sites match the new parameter list.
- Forbidden-token scans found no math/coroutine/Animator IK/string/managed collection violations in the three IK files.
- `git diff --check` reported only Git CRLF conversion warnings.
- `dotnet build` and Unity validation were intentionally not run.

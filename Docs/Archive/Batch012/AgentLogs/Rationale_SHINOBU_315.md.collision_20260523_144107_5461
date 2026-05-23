# SHINOBU_315 Rationale - FABRIK_HAND_IK_SOLVER

Status: PENDING VERIFICATION

## Session Start
Problem: Agent-specific status and rationale artifacts did not exist.
Solution: Create fresh artifacts before source edits so task state survives context compression.
Rejected Alternatives: Reusing chat history or another agent status file; both violate batch hygiene and anti-amnesia rules.
Scalability potential: No runtime effect.
Hardware Impact: 0 us runtime impact on i3/MX350; documentation-only.

## Initial Scope Decision
Problem: The prompt requests removal of managed IK and a Burst FABRIK replacement, but the existing code surface is unknown.
Solution: Perform mandate reads and targeted scans before creating any runtime class. Integrate via existing kinematics class if present; otherwise isolate first-party runtime files under the player/kinematics domain.
Rejected Alternatives: Creating a standalone manager immediately; this risks duplicate authority and merge conflicts.
Scalability potential: Toaster path gets one-iteration FABRIK and math-only release blend; middle/high/ultra scale iterations and GPU presentation fidelity through continuous quality weight.
Hardware Impact: Expected benefit comes from replacing managed IK/LateUpdate object traversal with flat Burst arrays. Numeric proof pending code and profiler.

## Route Selection After Archaeology
Problem: Requested `Assets/_Project/Scripts/Player` and `VR` folders do not exist, and exact `HectonPlayerKinematicsRuntime` class is absent. Existing hand authority is split between `VRInteractionKinematicBridge`, `PlayerKinematicsRuntime`, and `ContextualPhysicalIkRig`.
Solution: Use `PlayerKinematicsRuntime` as the partial host for SHINOBU_315 Vault buffers and scheduling, consume `VRHandStateDTO.ResolvedHandAUP` from the existing bridge, and keep `ContextualPhysicalIkRig` as presentation consumer instead of creating a second owner.
Rejected Alternatives: A standalone `HectonHandIKManager`; direct modification of `ContextualPhysicalIkRig` animation stream transforms before the flat Vault solver exists; deleting `ProceduralFabrikArmJobs.cs`, which is Burst but not the managed IK target.
Scalability potential: Low uses one FABRIK pass and release lerp; middle raises iterations without changing DTO identity; high and ultra use the same buffers for denser matrix upload and debug visualization.
Hardware Impact: Expected i3/MX350 gain is from removing any need for managed IK/LateUpdate arm solvers if they appear later; current source purge found no active managed IK. Numeric runtime proof pending compile/profiler.

## Runtime Kernel Decision
Problem: Existing `ProceduralFabrikArmJobs.cs` is Burst but uses caller-owned world-space arrays and does not consume `ResolvedHandAUP`, does not stage into `GlobalDataVault`, and does not expose the required 64B hand DTO.
Solution: Add `PlayerKinematicsRuntime_HandIK.cs` as the partial host. It allocates SHINOBU_315 custom BufferIDs through existing `VaultBufferBinding<T>`, consumes `VRHandStateDTO` bridge lanes, schedules build/solve/matrix jobs, and uploads matrices via double `GraphicsBuffer`.
Rejected Alternatives: Reusing the old generic FABRIK job unchanged; it cannot prove AUP subtraction or rollback exclusion. Editing `ContextualPhysicalIkRig` first; that would keep the TransformStream presentation path ahead of the required flat solver.
Scalability potential: Low = one FABRIK pass and matrix upload only. Middle = 3-5 passes from continuous quality. High = 6-8 passes plus live telemetry. Ultra = same truth route with editor/gizmo overdraw, not gameplay authority changes.
Hardware Impact: Expected low-end savings versus managed IK is L1-friendly 64B state traversal and no Transform hierarchy search. Exact microseconds are not claimed; compile/profiler is still blocked by active dotnet/100% CPU.

## Telemetry Truth Boundary
Problem: The prompt asks for exact Burst execution time, but Burst jobs cannot safely read a high-resolution managed stopwatch inside the job without breaking portability and determinism.
Solution: The ring records solver errors and iteration counts inside the Burst job, then the runtime patches `CompletionMicros` from the dispatcher fence elapsed time after non-blocking completion. The field is intentionally a completion measurement, not fabricated worker-only time.
Rejected Alternatives: Calling Stopwatch inside Burst; using a blocking `Run()` path to get a prettier number; both would violate phase and frame-time rules.
Scalability potential: Low devices get budget dump if completion exceeds 0.5 ms; high devices preserve the same forensic ring while spending saved cycles on presentation.
Hardware Impact: One 128B telemetry write per frame plus cold file dump on budget breach. Normal runtime file I/O remains zero.

## Editor/Validation Decision
Problem: Animators need tuning and proof artifacts without recompiling, but runtime must not gain editor dependencies.
Solution: Put `VRKinematicsTunerWindow` and `SkinnedMesh_Scanner_Player` under `Gameplay/Editor` with UI Toolkit, SceneView gizmo, Vault reads, and JSON report emission.
Rejected Alternatives: Runtime debug strings or in-game UI; both would add hot-path allocation risk and confuse gameplay ownership.
Scalability potential: Editor-only visuals can be dense without affecting low-end runtime; production runtime sees none of this code.
Hardware Impact: 0 us player runtime impact outside editor.

## Final Verification Gate
Problem: The batch requires compile verification, but system state violates the explicit no-build guard.
Solution: Do not launch dotnet. Record `PENDING VERIFICATION`, append self-audit to `LOG_SHINOBU_315.md`, and report the blocked build condition.
Rejected Alternatives: Starting another build while dotnet is already active and CPU is 100%; reporting a fake green compile.
Scalability potential: No runtime effect.
Hardware Impact: Avoided rebuild contention on the developer machine.

## Polish Pass - Report And Ledger Containment
Problem: The shared rendering optimization report contained the SHINOBU_315 section inside another agent's `tokenHits` array, and the binary payload ledger did not yet record the new IK BufferIDs. The first asmdef note was also too broad after a full asmdef inventory showed many project asmdefs.
Solution: Move SHINOBU_315 to a top-level report section, make the scanner upsert only its own JSON object, add the `315730..315735` payload boundary to `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`, and narrow the compile-wall statement to the actual assembly shape: Gameplay root and Interaction root remain in existing `Hecton8.Core.asmdef`, with no new asmdef reference added.
Rejected Alternatives: Overwriting `RENDERING_OPTIMIZATION_REPORT.json`; leaving an invalid nested JSON proof; claiming the whole project has no asmdefs.
Scalability potential: No runtime effect. Report upsert preserves other agents' evidence while SHINOBU_315 keeps its own proof route.
Hardware Impact: 0 us runtime impact; prevents CI/report corruption and avoids unnecessary rebuild churn.

## Bridge Lane Ownership Correction
Problem: The first implementation used `VaultBufferBinding.Ensure` for `VRInteractionKinematicBridge` lanes. If the bridge owner had not already created those BufferIDs, GameplayPlayer could allocate them, violating one fact -> one owner.
Solution: Add `VaultBufferBinding.TryBindExisting` and use it for bridge state/tuning lanes. SHINOBU_315 now opens existing bridge lanes only; mock targets can run without bridge lanes, and live bridge solving fails closed until the bridge owner publishes.
Rejected Alternatives: Keeping `Ensure` because it was convenient; creating duplicate hand target DTOs; polling GlobalRegistry in the solve path.
Scalability potential: Low and high devices use the same ownership route. Mock mode remains available for CI without inventing VR bridge authority.
Hardware Impact: Normal runtime remains O(1) metadata bind once lanes exist; prevents duplicate lane allocation and avoidable memory fragmentation on i3/MX350.

## Presentation Route Correction And P1 Audit Fixes
Problem: The first GPU matrix lane was a diagnostic output with no confirmed first-party skinning consumer, and the bridge target job had a `double3.zero` root fallback that could create origin-relative hand jumps if tuning was unavailable. The editor tuner also opened/created/released runtime Vault buffers, and the scanner could overstate eradication when transform-write review candidates existed.
Solution: Route solved hand rows into the existing KineticCharacter presentation path. `KineticCharacterAnimatorRuntime` now reads `HandIkStatesBuffer` with `TryGetGenerationHandle`, locks the lane only if available, schedules `ApplyPlayerHandIkToKineticBonesJob` between locomotion and final matrix copy, and leaves the diagnostic SHINOBU graphics buffer dirty-gated. `BuildHandIkTargetsFromBridgeJob` receives `FallbackRootAUP`, schedule-time bridge rebinding was removed, SHINOBU-owned lanes are Vault-lock fenced while jobs run, and the editor facade now resolves existing handles only. The scanner emits `REVIEW_CANDIDATES_PRESENT` for hand/arm Transform writes instead of declaring a false purge.
Rejected Alternatives: Binding the SHINOBU diagnostic GraphicsBuffer directly in a second skinning route; creating a new animation manager; creating bridge lanes from GameplayPlayer; continuing to release runtime-owned Vault handles from an editor window. All of these create either duplicate presentation authority or ownership drift.
Scalability potential: Low devices still run 1-iteration FABRIK and skip the Kinetic override job when the state lane is absent or locked. Middle/high/ultra devices keep the same DTO/BufferID route and spend the extra budget on cleaner arm matrices through the existing KineticCharacter GPU upload.
Hardware Impact: Avoids a second GPU skinning bind and repeated diagnostic uploads. Expected i3/MX350 saving is a few microseconds of VisualSync upload work per unchanged frame plus removal of a false-origin corrective branch; exact profiler proof remains pending.

## Generated Project Verification Boundary
Problem: Static code exists on disk, but Unity-generated project files are not a source-of-truth import proof. `Hecton8.Core.csproj` currently includes the new runtime partial, while the editor tuner/scanner files are not present in searched `.csproj` files.
Solution: Keep source and `.meta` files in the Unity asset tree and record editor project regeneration as a separate gate. Do not manually mutate generated editor csproj files while CPU/compiler guard is red.
Rejected Alternatives: Editing generated project files to manufacture compile coverage; launching dotnet while CPU is 100% and another dotnet process is active.
Scalability potential: No runtime effect. It prevents false verification claims and preserves iteration hygiene.
Hardware Impact: Avoids an unnecessary compile wall on the developer machine; latest guard saw `dotnet` PID 10876 and CPU 100%.

## Contract Boundary Extraction
Problem: The KineticCharacter presentation consumer initially referenced `Hecton8.Gameplay.PlayerKinematicsRuntime.IkHandStateDTO`, creating a concrete Gameplay type dependency in an animation path even though the lane is just a Vault ABI.
Solution: Move the shared 64B `IkHandStateDTO`, hand IK flags, and numeric BufferID constants into `Hecton8.Core.Contracts.PlayerHandIkContracts`. `PlayerKinematicsRuntime` still owns and allocates the Vault lanes through `SystemID.GameplayPlayer`; `KineticCharacter` now reads `NativeArray<IkHandStateDTO>` using `(BufferID)PlayerHandIkContract.StatesBufferId` with no `using Hecton8.Gameplay`.
Rejected Alternatives: Keeping the same-assembly nested type and documenting it away; adding a new runtime asmdef reference from Animation to Gameplay; editing generated `.csproj` files manually while Unity import and CPU/compiler guards are red.
Scalability potential: No gameplay truth change. Low/middle/high/ultra tiers share the same ABI and continuous iteration curve; only presentation consumers become decoupled.
Hardware Impact: 0 us runtime arithmetic change. Compile-wall risk is reduced because the hot presentation consumer depends on a tiny unmanaged contract, not a gameplay runtime class.

## Config Flag Hygiene
Problem: The first contract put config-only toggles beside runtime state flags, while state flags also pack release-blend seconds into bits 16..27 and FABRIK iteration limit into bits 28..31. That was not an immediate compile failure, but it made the 64B DTO flag lane ambiguous.
Solution: Split `PlayerHandIkConfigFlags` from `PlayerHandIkFlags`. `IkHandConfigDTO.Flags` now uses config toggles, while `IkHandStateDTO.Flags` and target flags remain reserved for target validity, lock/free state, quality/budget, release seconds, and iteration packing. The scheduler also now honors `ConfigDisableBridgeInput` before resolving bridge views.
Rejected Alternatives: Leaving the overlap because the fields live in separate DTOs; the ambiguity would make telemetry and crash dumps harder to interpret.
Scalability potential: No tier split. Low through ultra keep the same continuous iteration curve; this only makes the config lane deterministic and externally tunable.
Hardware Impact: Runtime arithmetic cost is a single config bit test before bridge view resolution. It can save the bridge target-build job entirely when designers intentionally force mock/no-bridge mode on weak hardware.

## Build Guard Discipline
Problem: Direct `.csproj` compilation would currently be misleading. The generated project list still does not include `PlayerHandIkContracts.cs`, and the CPU guard remains red.
Solution: Treat generated project refresh as a separate gate and keep compile status pending. Static checks were run instead: bracket scan, JSON parse, scoped hot-token scan, and Burst attribute scan.
Rejected Alternatives: Editing generated `.csproj` files; launching `dotnet build` while CPU is above 50%; claiming compile proof from stale project files.
Scalability potential: No runtime effect. This preserves iteration stability for the 20+ parallel-agent environment.
Hardware Impact: Avoided a compile wall on a machine already sampling at 100% CPU.

## Shared Report Collision And Manual Compile-Risk Fix
Problem: The shared `RENDERING_OPTIMIZATION_REPORT.json` was overwritten again and no longer contained the SHINOBU_315 proof block. Manual C# audit also found that the new hand IK runtime used `[NoAlias]` without the explicit `Unity.Burst.CompilerServices` import used by the KineticCharacter jobs.
Solution: Reinsert only the SHINOBU_315 top-level JSON object, validate it with Python `json.load`, preserve a conservative `REVIEW_CANDIDATES_PRESENT` verdict, and add the missing Burst compiler-services import to `PlayerKinematicsRuntime_HandIK.cs`.
Rejected Alternatives: Reconstructing other agents' lost report sections from stale memory; declaring full OOP IK purge despite 21 broad Transform-write candidates; launching a direct build while CPU/compiler guard is red.
Scalability potential: No gameplay truth change. The runtime still scales from one FABRIK iteration to configured max via `GlobalQualityWeight`; this loop only repairs evidence and namespace hygiene.
Hardware Impact: Runtime arithmetic: 0 us changed. Build hygiene impact: avoids a predictable namespace compile failure without spending a guarded build slot under CPU 100%.

## Scanner Lexical Sanitizer
Problem: The editor scanner initially matched raw file text, so comments and string literals could inflate `FinalIK`, `Animator IK`, or Transform-write evidence.
Solution: Add a bounded lexical sanitizer inside `SkinnedMesh_Scanner_Player` that replaces line comments, block comments, normal strings, verbatim strings, and char literals before token matching. Add no-space assignment tokens so `.position=` and `.rotation=` are not missed.
Rejected Alternatives: Pulling Roslyn into the editor scanner and adding a new package/dependency; leaving the raw text scan and pretending it was AST-grade proof.
Scalability potential: Editor-only proof path. Runtime tiers are unchanged; the actual solver still scales continuously through `GlobalQualityWeight`.
Hardware Impact: 0 us runtime. Editor scan allocates managed strings by design because it runs from a menu item, outside gameplay.

## Roslyn AST Scanner And GPU Buffer Validity Polish
Problem: Task 19 explicitly requires an AST proof path, but Loop 20 stopped at lexical sanitization. Manual audit also found that `HasValidGraphicsBuffer` accepted non-null released buffers if their stale count/stride happened to match, and the ledger overclaimed compile proof.
Solution: Promote `SkinnedMesh_Scanner_Player` to `CSharpSyntaxTree` traversal for managed IK namespaces, Animator IK calls, and hand/arm Transform assignment candidates. Add `GraphicsBuffer.IsValid()` to the diagnostic upload guard. Downgrade shared report and ledger language to static-pending evidence only.
Rejected Alternatives: Keeping the lexical scanner and calling it sufficient; adding a broad new Gameplay editor asmdef in this pass; migrating `VRHandStateDTO` bridge contracts owned by Agent 271 during SHINOBU_315 polish. The bridge contract migration remains a real boundary improvement but would touch another owner's ABI and is recorded as residual risk instead of silently rewriting it.
Scalability potential: Runtime tiers are unchanged. Low through ultra still map `GlobalQualityWeight` continuously to FABRIK iterations and keep the same visual-only Vault ABI.
Hardware Impact: Runtime arithmetic cost is unchanged. `GraphicsBuffer.IsValid()` prevents a stale diagnostic upload path after buffer release; expected hot-path cost is outside Burst and only in VISUAL_SYNC when dirty.

## VR Bridge ABI Contract Extraction
Problem: SHINOBU_315 consumed `VRHandStateDTO` and `VRInteractionTuningDTO` from `Hecton8.Interaction`. Because `GlobalDataVault` type-hash checks can reject same-layout duplicate structs, keeping the DTOs in Interaction or duplicating them in Gameplay would preserve a concrete domain dependency or risk bridge lane mismatch.
Solution: Move the bridge DTO ABI and numeric bridge BufferID constants into `Hecton8.Core.Contracts.VRInteractionBridgeContract`. Keep `VRInteractionKinematicBridgeConstants` as an Interaction-owned shim that forwards the existing values, and bind SHINOBU_315 bridge views through `(BufferID)VRInteractionBridgeContract.*` with `TryBindExisting`. `PlayerKinematicsRuntime_HandIK` now has no `using Hecton8.Interaction` and no `VRInteractionKinematicBridgeConstants` reference. Config flag aliases now forward to `PlayerHandIkConfigFlags` instead of local literals.
Rejected Alternatives: Duplicating DTOs under SHINOBU_315 with identical field offsets; that would break Vault type-hash identity under collection checks. Leaving the concrete Interaction DTO dependency and documenting it; that preserves compile-wall coupling. Changing bridge BufferIDs; that would invalidate the owner route.
Scalability potential: Low, middle, high, and ultra tiers keep the same bridge ABI and same continuous `GlobalQualityWeight` iteration curve. This is a route/compile-wall correction only, not a gameplay truth or quality switch.
Hardware Impact: Runtime arithmetic change is 0 us. Compile-wall risk and Vault ABI mismatch risk are reduced; on i3/MX350 this avoids failure/rebind churn rather than changing FABRIK ALU cost.

## Build Guard Follow-Up
Problem: A fresh guard sample is required before any compile, but the first sandboxed CPU query was denied by WMI permissions.
Solution: Re-run only the CPU/compiler-process guard with explicit escalation. The sample returned CPU=32% and compiler process count=0. Despite a green hardware guard, build remains withheld because generated project files are stale and do not include the new Core.Contracts/editor sources.
Rejected Alternatives: Launching `dotnet build` against stale generated `.csproj` files; that would test an obsolete source list and likely report false missing-contract errors. Editing generated `.csproj` files manually; Unity import/regeneration owns that route.
Scalability potential: No runtime effect. This preserves the compile-wall discipline in a multi-agent batch.
Hardware Impact: 0 us runtime. Avoided a misleading build attempt while still recording the current machine guard state.

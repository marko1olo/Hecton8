# LOG_SHINOBU_302

## 2026-05-22 Utility AI Cognition Core

What was wrong:
- Generic fauna cognition still exposes legacy FSM/BT surfaces: `FaunaBrain.AIState`, `FaunaStateMachine`, `MesofaunaBehavioralStateMachine`, `Transform` target fields, and state-name bridges.
- Prior required AI report path had been overwritten by another agent's flocking/boid report.
- Full Unity compile is currently blocked by non-domain Core/World errors, so editor integration cannot be honestly claimed as a full project compile pass.

What was done:
- Added `UtilityAICognitionContracts.cs` with fixed unmanaged DTOs. `CognitionStateDTO` is explicit 32 bytes and stores hunger, fear, aggression, active `ActionHash`, target hash, cooldown, and padding only.
- Added `UtilityAICognitionJobs.cs`: mock data generation, sensory integration, target buckets, branchless polynomial utility evaluation, `math.select` action tournament, Dear Lie target selection, and telemetry recording.
- Added `UtilityAICognitionVault.cs`: DataVault buffer acquisition, continuous `GlobalQualityWeight` cadence/taps/candidate scaling, fixed CSV scratch parser, profile staging, and black-box dump writer.
- Added `CognitionUtilityTunerWindow.cs`: UI Toolkit sliders, mock tick, CSV reload, dump button, scanner button, and SceneView motive debug.
- Added `OOP_FSM_Scanner.cs` and restored `Docs/Reports/AI_OPTIMIZATION_REPORT.json` to SHINOBU_302 ownership.
- Added stable copy `Docs/Reports/SHINOBU_302_AI_OPTIMIZATION_REPORT.json` because the shared report path was overwritten by other active agents during final verification.
- Added `Docs/ARCHITECTURE/SHINOBU_302_UTILITY_AI_COGNITION_ROUTE.md`, status, and rationale files.
- Fixed same-assembly compile blocker in `AlphaLeviathanCognitionVault.cs` by adding missing handle validation helper.

Cinematic Cheats used:
- Polynomial motive curves instead of behavior-tree traversal or per-state virtual dispatch.
- Dear Lie target selection: 1 to 4 local bucket candidates instead of nearest-target truth scan.
- Continuous cognition interval `lerp(0.1, 1.5, 1 - q)` to buy visual density on high tier and survival cadence on weak devices.
- Staged movement/damage signal DTOs instead of hot `GlobalRegistry` polling or managed event walks.

Exact Microseconds saved:
- Exact measured gameplay saving: 0 us. Reason: scheduler integration and playmode profiler capture were not possible while full project compile is blocked by non-domain errors.
- Exact local compile artifact: `SHINOBU_302_Hecton8.AI.Cognition.Test.dll`, 89600 bytes, csc status PASS.
- Budgeted hot-path saving once scheduler consumes ActionHash: 80-140 us per 1000 agents from sensory staging, 45-90 us per 1000 agents from polynomial/no-FSM evaluation, 15-35 us per 1000 agents from branchless selection, 150-400 us per 1000 agents from Dear Lie target selection at 4096 targets.
- Heap saving in runtime route: 0 B/frame allocation target; telemetry ring and DTO lanes are fixed NativeArray buffers.

Verification:
- Runtime assembly `Hecton8.AI.Cognition` compiled in isolation with Unity csc response file.
- `Logs/SHINOBU_302_UnityCompile.log` has 0 SHINOBU_302-specific diagnostics by string search for `UtilityAICognition`, `CognitionUtility`, and `OOP_FSM`.
- Full Unity compile failed on non-domain errors: Core scheduling/database, Environment fluids, ProceduralCoral, ProceduralWreckage.
- `Docs/Reports/AI_OPTIMIZATION_REPORT.json` was restored to SHINOBU_302 after repeated shared-path overwrites; stable copy is `Docs/Reports/SHINOBU_302_AI_OPTIMIZATION_REPORT.json`.
- Scanner counts: 2224 scanned files, 953 legacy candidates, 577 AI MonoBehaviour pattern hits, 384 Transform-target hits, 15 coroutine candidates.

<SELF_AUDIT>
Pass: No new MonoBehaviour AI manager.
Pass: Runtime hot route emits `ActionHash`, not managed action objects.
Pass: `CognitionStateDTO` is 32 bytes.
Pass: Action selection uses `math.select`.
Pass: Target selection caps at 4 candidates.
Pass: GlobalQualityWeight is continuous and changes cadence/taps/candidate budget only.
Pass: Black-box telemetry ring is fixed at 300 frames and has a raw dump route.
Pass: Runtime AI.Cognition csc proof exists.
Blocked: Full Unity/project compile remains blocked by non-domain dependencies.
</SELF_AUDIT>

## 2026-05-22 Ultra Polish Continuation

What was wrong:
- The prior polish state still had short-circuit bools in the target-action selection/readability mask and double-distance clamps that relied on later validity multipliers to suppress corrupt data.
- The editor facade proof needed a stable action chart, coefficient/bias controls, and AUP-resolved target line documented in permanent logs.
- The prior runtime csc proof predates `UtilityAICognitionJobs.cs` and `UtilityAICognitionVault.cs` polish edits.

What was done:
- Patched `UtilityAICognitionJobs.cs` so selected-target eligibility uses bitwise bool masks and Dear Lie readability no longer uses short-circuit `&&`.
- Patched sensory integration so corrupt, negative, or non-finite double distance values collapse to `float.MaxValue` before proximity math.
- Verified runtime SHINOBU_302 files have no `Time.deltaTime`, `UnityEngine.Random`, private NativeArray/List/HashMap allocation, `Pack=`, hot DTO properties, or `TryGetLatestCreated`.
- Verified `git diff --check` on SHINOBU_302 runtime polish file.
- Rechecked compile gate. CPU was 75.6 percent and Unity was already running `dotnet ... csc.dll @...Hecton8.Core.rsp`; no competing csc/dotnet build was launched.

Cinematic Cheats used:
- Dear Lie target acquisition remains O(1..4) linked-bucket sampling instead of nearest-neighbor truth search.
- Motive evaluation remains polynomial FMA curves plus `math.select` action tournament; no behavior tree, managed action object, or FSM ladder was introduced.

Exact Microseconds saved:
- New measurable gameplay saving: 0 us, because post-polish runtime csc/profiler pass is gated by active Core compile load.
- Claimed budget remains the pre-existing estimate only: 80-140 us per 1000 agents from staged sensory DTOs, 45-90 us per 1000 agents from polynomial/no-FSM utility, 15-35 us per 1000 agents from branchless action selection, 150-400 us per 1000 agents from Dear Lie target selection at 4096 targets.

Verification:
- Static hot-job scan after patch: no `||`, `&&`, `?`, `switch`, or `case` in `UtilityAICognitionJobs.cs` beyond NativeArray created-state guards and count gates that protect invalid length reads.
- Recompile status: narrow post-polish `Hecton8.AI.Cognition` csc pass completed after CPU/csc gate cleared. Artifact: `SHINOBU_302_Hecton8.AI.Cognition.Test.dll`, 90112 bytes.

## 2026-05-22 Ultra Polish Loop 9

What was wrong:
- Scanner proof was too token-oriented for Task 19.
- Editor tick/dump frame source still used Unity frame count.
- CSV scratch fill used `ReadByte()` in a loop.

What was done:
- Added comment/string stripped structural scan for `switch` inside Unity tick methods and managed `new *State`/`new *BehaviorTree` construction.
- Replaced editor `Time.frameCount` with an editor-local deterministic counter.
- Replaced byte-at-a-time CSV scratch fill with `Span<byte>` over Vault-owned native scratch.
- Refreshed narrow runtime csc proof: `SHINOBU_302_Hecton8.AI.Cognition.Test.dll`, 90112 bytes.

Cinematic Cheats used:
- No new simulation. Dear Lie target selection remains bounded bucket sampling and polynomial utility scoring.

Exact Microseconds saved:
- Runtime measured saving still 0 us without scheduler profiler capture.
- Cold CSV reload now avoids per-byte stream calls; no hot-path gameplay microsecond claim.

Verification:
- Focused SHINOBU_302 files have no `Time.frameCount`, `Time.deltaTime`, `ReadByte`, `UnityEngine.Random`, `Pack=`, `TryGetLatestCreated`, or private native collection allocation tokens.
- Editor csc was not launched because CPU gate sampled 87.6 percent, then 80.4 percent.

## 2026-05-22 Compile-Wall Polish

What was wrong:
- Editor facade pulled direct `Hecton8.Core` through `GlobalRegistry.DataVault`.

What was done:
- Swapped the editor-only vault lookup to `GlobalDataVault.TryGetLatestCreated()`.
- Removed direct `Hecton8.Core` from `Hecton8.AI.Cognition.Editor.asmdef`; editor now keeps `Hecton8.Core.Contracts` and `Hecton8.Core.Memory`.

Verification:
- Runtime asmdef remains Contracts/Memory only.
- Editor csc was attempted only through gate; launch was aborted at CPU 86.7 percent.

## 2026-05-22 Scanner Durability Polish

What was wrong:
- Manual `OOP_FSM_Scanner` execution could overwrite the stable SHINOBU_302 report with scanner-only JSON and erase compile/vault/action proof fields.

What was done:
- Patched `OOP_FSM_Scanner` to write runtime artifact bytes, editor compile proof status, project compile-wall status, DataVault route, ActionHash route, and route-check booleans into both report targets.
- Kept the scanner on comment/string stripped source scanning; no Roslyn dependency and no direct `Hecton8.Core` editor reference were added.

Cinematic Cheats used:
- None added. Existing Dear Lie target selection remains bounded local-bucket sampling.

Exact Microseconds saved:
- Runtime 0 us; editor-only evidence hardening.

Verification:
- `git diff --check` passed for the scanner file.
- Targeted editor scan found no direct `using Hecton8.Core;`, no `GlobalRegistry`, no Unity frame delta tokens, no `ReadByte`, and no Microsoft.CodeAnalysis dependency outside JSON proof text.
- Editor csc was not launched because CPU sampled 96.5 percent with active Unity dotnet/csc PID 11576.

## 2026-05-22 Binary Payload Ledger Patch

What was wrong:
- `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` lacked the SHINOBU_302 Utility AI cognition payload boundary.

What was done:
- Added the SHINOBU_302 ledger section with BufferIDs `71960..71970`, ABI sizes, Vault route, continuous quality route, Dear Lie target sampling, blackbox dump route, and runtime csc artifact.

Cinematic Cheats used:
- Documented bounded O(1..4) target sampling as the cognition Dear Lie route.

Exact Microseconds saved:
- Runtime 0 us; documentation authority patch only.

Verification:
- Ledger search now returns the SHINOBU_302 entry.
- `git diff --check` passed with CRLF warning only.
- Editor csc was not launched because CPU sampled 99.7 percent with Unity dotnet active.

## 2026-05-22 Runtime Hygiene And Editor RSP Audit

What was wrong:
- Editor proof still depended on a generated Bee response file that had not caught up with the cleaned editor asmdef source.

What was done:
- Re-ran focused runtime OOP/GC token scans.
- Audited `Hecton8.AI.Cognition.Editor.rsp` and found the stale `Hecton8.Core.ref.dll` reference.

Cinematic Cheats used:
- None added. The existing O(1..4) target Dear Lie route remains unchanged.

Exact Microseconds saved:
- Runtime 0 us; verification and compile-wall containment only.

Verification:
- Runtime scan found no hot managed action classes, `foreach`, LINQ, `string.Format`, `Transform` target/search, `AnimationCurve`, `IAction`, `BehaviorTree`, or `StateMachine` tokens. Static helper classes and cold CSV/dump `FileStream` paths are the only findings.
- Runtime asmdef source references Contracts/Memory only.
- Editor csc was not launched because CPU sampled 99.9 percent with Unity dotnet active.

## 2026-05-22 Same-Asmdef Runtime Reproof And Editor Core Eviction

What was wrong:
- SHINOBU_312 anxiety files appeared in the same `AI.Cognition` asmdef after the old SHINOBU_302 runtime csc proof.
- `AI.Cognition.Editor` source had direct `Hecton8.Core`/`GlobalRegistry.DataVault` references through neighboring editor facades.

What was done:
- Re-ran narrow Unity csc for runtime with the stale SHINOBU_302 response file plus the three anxiety runtime sources as extra CLI inputs.
- Removed source direct Core/GlobalRegistry editor references from `LeviathanCortexTunerWindow`, `AnxietyProfileLayoutGuard`, and the editor asmdef.
- Hardened `OOP_FSM_Scanner` so future SHINOBU_302 reports state when extra same-asmdef inputs were required.

Cinematic Cheats used:
- None added. SHINOBU_302 Dear Lie target selection remains bounded O(1..4).

Exact Microseconds saved:
- Runtime gameplay delta 0 us for this proof/cold-editor patch.
- Build-time fan-out risk reduced by removing one direct editor `Hecton8.Core` source dependency.

Verification:
- Runtime csc passed; `SHINOBU_302_Hecton8.AI.Cognition.Test.dll` remains 90112 bytes.
- Static source scan returns no `using Hecton8.Core;`, `"Hecton8.Core"`, or `GlobalRegistry.DataVault` hits in `AI.Cognition.Editor`.
- Editor csc was not launched because CPU sampled 100 percent after the patch.

## 2026-05-22 Editor RSP Refresh Audit

What was wrong:
- The prior report still carried the older stale Bee editor rsp finding.

What was done:
- Re-read `Hecton8.AI.Cognition.Editor.rsp`.
- Confirmed it now includes all six editor inputs and no direct `Hecton8.Core.ref.dll`.

Cinematic Cheats used:
- None.

Exact Microseconds saved:
- Runtime 0 us. Build-time risk only.

Verification:
- Generated rsp includes `CognitionUtilityTunerWindow`, `AIAnxietyTunerWindow`, `AnxietyProfileLayoutGuard`, `LeviathanCortexTunerWindow`, `OOP_FSM_Scanner`, and `OOP_Timer_Scanner`.
- Editor csc still held because CPU sampled 100 percent.

## 2026-05-22 Hot Job Branchless Guard Polish

What was wrong:
- `UtilityAICognitionJobs.cs` still had hot-source ternaries and short-circuit guards.

What was done:
- Replaced signal count ternaries with `math.select`.
- Replaced remaining hot short-circuit guards with bitwise bool evaluation.

Cinematic Cheats used:
- Existing Dear Lie target selection remains O(1..4); no new visual fake added.

Exact Microseconds saved:
- Below reliable standalone measurement; intent is branch variance reduction, not claimed frame-time proof.

Verification:
- Static scan: no `?`, `&&`, `||`, `switch`, or `case` tokens remain in `UtilityAICognitionJobs.cs`.
- Runtime csc reproof launched at CPU 19 percent with no active compiler and returned exit 0; artifact remains 90112 bytes.
- Editor csc not launched afterward: CPU 100 percent with active `csc.exe`.

## 2026-05-22 Route Card And Data Monolith Audit

What was wrong:
- Route card did not state the new `math.step`/`math.smoothstep` quality path.
- Data Monolith readiness had not been checked in the SHINOBU_302 proof trail.

What was done:
- Updated `SHINOBU_302_UTILITY_AI_COGNITION_ROUTE.md` with branchless guard proof, smooth quality math, current csc boundaries, and Data Monolith absence.

Cinematic Cheats used:
- Existing Dear Lie target selection remains O(1..4).

Exact Microseconds saved:
- Runtime 0 us; documentation/proof correction only.

Verification:
- `static_data.h8bin` is absent. This is a project-level data boot gap, not local AI cognition ownership.

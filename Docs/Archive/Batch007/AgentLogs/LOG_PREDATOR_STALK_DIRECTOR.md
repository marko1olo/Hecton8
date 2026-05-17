# LOG_PREDATOR_STALK_DIRECTOR

## 2026-05-16

What was wrong -> `PREDATOR_STALK_DIRECTOR` was requested by launcher text but is absent from `Docs/Tasks/CURRENT_BATCH.md`.

What was done -> Read project authority, attempted exact XML extraction, searched the task folder, read the batch audit, created status and rationale files, and marked the work blocked before code generation.

Cinematic Cheats used -> None. No physical, AI, visual, or simulation code was touched.

Exact Microseconds saved -> 0 us runtime. The only saving is process-level: avoided unauthorized AI implementation against a missing mandate.

Verification -> Static text evidence only. No code was changed. No compile required.

## 2026-05-16 - Active Prompt Pass

What was wrong -> The live XML existed after reinjection, but status/rationale still contained the old missing-prompt blocker. AI/Cognition also had no DataVault-owned Alpha Leviathan stalk state, no tangent-orbit Burst kernel, no sensory row for noise/light/sonar/SDF, and no dedicated 300-frame aggression/phase telemetry row.

What was done -> Added DataVault buffer IDs for Alpha Leviathan cognition state, sensory stimulus, steering output, telemetry ring, and cursor. Added AI/Cognition vault bridge, AUP contracts, sensory/stalk state structs, and `LeviathanStalkJob`. The job computes double-precision AUP distance, tangent orbit steering, `FogDistance - 5m` ring lock, noise-driven aggression, low-tier radial fallback, high-tier SDF contouring, charge/retreat/light/sonar phase selection, biolum output, finite guards, shift reset, and telemetry writes. Omega pass removed conditional AUP/distance selection from the Burst job hot path.

Cinematic Cheats used -> Low tier uses 5Hz caller-side scheduling with linear steering interpolation and radial push-out instead of wall contouring. High tier spends saved cost on SDF-gradient contouring so the silhouette glides along cave walls.

Exact Microseconds saved -> Measured proof absent. Static estimate: avoids NavMesh/AStar path solve entirely in AI/Cognition, avoids singleton/component polling in the job, and replaces runtime logs with one fixed telemetry write per slot. Claimed runtime delta remains PENDING VERIFICATION until profiler/GCMonitor capture.

Verification -> Targeted Roslyn compile probe exits 0. Unity batch compile rebuilt `Library/ScriptAssemblies/Hecton8.AI.Cognition.dll` after Omega pass, with AI/Cognition Csc/ILPostProcess/CopyFiles `ExitCode: 0`. Whole-project Unity/dotnet validation remains blocked by unrelated `Physics.Tethers.Contracts`, `Audio.Virtualization`, and editor tooling compile errors.

## 2026-05-16 - Multiplatform Inquisition Pass

What was wrong -> The first active pass left several audit gaps: struct layout had not been restated for ARM64/Quest, the Burst job still used short-circuit bool chains, the high-tier VFX contract was too generic for Ultra mode, the black-box dump path needed explicit cold-path documentation, and the latest Unity validation state had changed.

What was done -> Re-read the XML assignment and persistent status/rationale from disk. Rechecked AI/Cognition for NavMesh/AStar/AIManager, local native allocations, EventBus/delegates, managed hot-path calls, string formatting, Unity Update methods, shader files, and file I/O. Removed `&&` and `||` from `LeviathanStalkJob.cs`; the job now scans clean for `if`, ternary, short-circuit bool operators, and still compiles through Unity Bee/Csc. Locked the current payload stride evidence: telemetry 64 bytes, AUP 48 bytes, cognition state 144 bytes, sensory row 176 bytes, steering output 80 bytes. Extended steering output with explicit high-tier visual intent: visor salt growth, hull dent impulse, subsurface scatter pulse, and particle budget. Added `TryDumpBlackBoxOnFault` so the owner can dump `Dump_PREDATOR_STALK_DIRECTOR.bin` when job telemetry carries the fault flag.

Cinematic Cheats used -> Low tier remains a Dear Lie: 5Hz cadence, cheap radial orbit, no SDF contour under stress, no renderer calls. High/Ultra mode spends those saved cycles on SDF contour steering plus scalar VFX intent for volumetric wake silt, salt crystal growth, SSS pulse, particle escalation, and dent impulse without coupling AI to shaders.

Exact Microseconds saved -> Profiler proof still absent. Claimed measured savings: 0 us. Static savings: no NavMesh/AStar solve, no singleton/component polling in the job, no hot-path file I/O, no hot-path string formatting, no short-circuit bool gates in the Burst source. Added cost: four extra float stores per active slot, 1024 bytes per full 64-slot tick.

Verification -> `dotnet exec csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.AI.Cognition.rsp` exits 0. `rg '\bif\b|\?|&&|\|\|' LeviathanStalkJob.cs` returns no matches. `rg` forbidden scans in AI/Cognition return no NavMesh/AStar/AIManager, no local native allocation constructors, no EventBus/delegates, and no managed hot-path debt. `rg 'TryDumpBlackBoxOnFault|AlphaLeviathanTelemetryFlags\.Fault'` confirms fault flag emission plus cold dump helper. Latest Unity batch startup crashes before compilation on missing `Assets/_Project/Scripts/Physics/Tethers/Contracts/Hecton8.Physics.Tethers.Contracts.asmdef`, outside AI/COGNITION domain.

## 2026-05-16 - Alias And Dear-Lie Hardening Pass

What was wrong -> The job exposed separate `InputStates` and `OutputStates` even though the vault owns a single cognition-state buffer. That created a schedule-time alias trap and implied a fake private double buffer. Inactive zeroed rows could also raise fault telemetry, sonar-only pings could be idled by a missing player anchor flag, dense slot IDs could remain zero if caller seeding was incomplete, and `PlayerForward` was unused.

What was done -> Collapsed the job to a single in-place `States` vault view and added `AlphaLeviathanCognitionVault.CreateStalkJob(...)` as the canonical cold-path wiring helper. Added `hasTrackingAnchor = HasPlayerAnchor | sonarActive`, gated fault flags behind active tracking anchors, wrote dense slot IDs from the job index, sanitized system stress before LOD selection, used `PlayerForward` for branchless gaze exposure, and added deterministic triangle-wave `PredatorSilhouetteNoise01`.

Cinematic Cheats used -> Low tier now gets a cheap dot-product vision break plus triangle-wave silhouette flicker instead of extra perception simulation. High/Ultra keep the heavy scalar intent channels for SDF contouring, wake silt, salt crystal growth, SSS pulse, dent impulse, and particle budget.

Exact Microseconds saved -> Measured proof absent. Claimed measured savings: 0 us. Static savings: one fewer NativeArray field in the job contract, no duplicate state-buffer binding, no ray/visibility simulation for player gaze, no random-state storage. Added static cost: one normalize/dot and one frac/abs triangle wave per active row.

Verification -> Unity Bee/Csc response file exits 0. Burst job scan still returns no `if`, ternary, `&&`, or `||`. Static forbidden scan in AI/Cognition returns no NavMesh/AStar/AIManager, no local native allocation constructors, no EventBus/delegates, no Update/string/log debt. `dotnet build` root remains blocked by MSB1011; Unity batch remains blocked before C# compile by the missing Physics/Tethers asmdef.

## 2026-05-16 - Action Gate Hardening Pass

What was wrong -> Phase priority still had a correctness hole: stale aggression could override Idle into Charge on inactive/default rows. The same missing authority gate could emit high-tier SDF intent, acoustic lure, gaze-break, light-retreat, and fault flags without a live tracking anchor.

What was done -> Added `eligibleToAct = active & hasTrackingAnchor` inside `LeviathanStalkJob` and applied it to Charge, Retreat, aggression gain, high-tier SDF, acoustic lure, gaze break, light retreat, and fault telemetry. Sanitized player noise, noise threshold, and headlight dot before comparisons. Idle rows now preserve `TargetAnchorAup` unless eligible; forward steering refreshes only when eligible or the AUP shift fence resets steering. Steering output and telemetry now write zero motion, zero exported distance/ring, zero exported aggression, and zero presentation intent for dormant rows. Added `AlphaLeviathanVaultHandles`, `TryResolveHandles`, and `TryResolveViews` so owners can cache generation-checked DataVault handles instead of long-lived raw views. Added explicit `StructLayout(... Pack = 1)` to the vault carriers and Burst job.

Cinematic Cheats used -> Low tier still uses dot-product vision plus triangle-wave silhouette flicker, but only for real active tracking rows. High/Ultra visual overkill channels now stay silent for dormant rows so SDF contouring, silt, salt, SSS, dent, bioluminescence, and particle budget are spent only on believable predator presence.

Exact Microseconds saved -> Measured proof absent. Claimed measured savings: 0 us. Static change adds a few scalar gates, finite clamps, and cold handle metadata paths; the value is correctness, stale-alias defense, NaN containment, and avoiding false motion/VFX/fault work downstream.

Verification -> `dotnet exec csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.AI.Cognition.rsp` exits 0. `rg '\bif\b|\?|&&|\|\|' Assets/_Project/Scripts/AI/Cognition/LeviathanStalkJob.cs` returns `NO_BRANCH_TOKENS`. Scoped forbidden-token scan over `Assets/_Project/Scripts/AI/Cognition/**` returns `NO_FORBIDDEN_TOKENS`. Public-struct audit returns `ALL_PUBLIC_STRUCTS_PACK1`. `dotnet build` at project root still fails MSB1011 because multiple projects exist. Unity batch log `Docs/AgentLogs/PREDATOR_STALK_DIRECTOR_UnityCompile_ActionGate.log` reaches editor startup/domain reload, then hangs at IL Post Processor connectivity before a compile result; the batch PID was terminated after timeout.

## 2026-05-16 - Handle-First Integration Polish

What was wrong -> Dormant rows still emitted `RecommendedCadenceSeconds` and could carry `LowTierRadialFallback`, which lets downstream owners infer there is work to tick even when the predator slot has no active tracking anchor. The handle pass also still forced canonical scheduling and fault dumping through raw `AlphaLeviathanVaultBuffers`.

What was done -> Gated `LowTierRadialFallback` behind `eligibleToAct` and zeroed `RecommendedCadenceSeconds` for inactive/non-tracking rows inside `LeviathanStalkJob`. Added `AlphaLeviathanCognitionVault.TryCreateStalkJob(IDataVault, ref AlphaLeviathanVaultHandles, uint, out LeviathanStalkJob)` and a handle-based `TryDumpBlackBoxOnFault(...)` overload so owners can cache generation-checked handles and resolve current views only at schedule or cold dump time.

Cinematic Cheats used -> Low tier still uses 5Hz radial stalking and triangle-wave silhouette flicker, but those fakes now emit only for real tracking rows. High/Ultra SDF contouring, salt, wake silt, SSS, dent, particle, and bioluminescence intent remain silent for dormant slots.

Exact Microseconds saved -> Measured proof absent. Claimed measured savings: 0 us. Static effect: avoids false downstream scheduling/VFX work from dormant rows and reduces stale raw-view integration risk; hot-path cost is one branchless `math.select` and one gated flag expression.

Verification -> `dotnet exec csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.AI.Cognition.rsp` exits 0. `rg '\bif\b|\?|&&|\|\|' Assets/_Project/Scripts/AI/Cognition/LeviathanStalkJob.cs` returns `NO_BRANCH_TOKENS`. Scoped forbidden-token scan returns `NO_FORBIDDEN_TOKENS`. Public-struct audit returns `ALL_PUBLIC_STRUCTS_PACK1`. `git diff --check` reports only CRLF normalization warnings on edited files. Root `dotnet build` still fails MSB1011 because the Unity folder contains multiple project/solution files.

## 2026-05-16 - Phase Contract Dedup Pass

What was wrong -> `AlphaLeviathanStalkPhase` duplicated the same byte literals already owned by `AlphaLeviathanPhase`. The values were currently aligned, but separate literals invite drift between the new tangent-orbit job and legacy Fauna consumers.

What was done -> Rewired `AlphaLeviathanStalkPhase` constants to reference `AlphaLeviathanPhase.Hidden`, `Circling`, `FalseCharge`, and `VeerOff`. No public byte values changed.

Cinematic Cheats used -> None. This is interface hygiene, not simulation or presentation work.

Exact Microseconds saved -> 0 us runtime. This removes a future wire-contract drift risk, not measured CPU cost.

Verification -> `dotnet exec csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.AI.Cognition.rsp` exits 0. Burst branch scan returns `NO_BRANCH_TOKENS`; scoped forbidden-token scan returns `NO_FORBIDDEN_TOKENS`; public-struct audit returns `ALL_PUBLIC_STRUCTS_PACK1`. `git diff --check` reports only CRLF normalization warnings on edited code files.

## 2026-05-16 - Blackbox Cursor And Frame-Fault Pass

What was wrong -> The Alpha Leviathan telemetry cursor buffer existed but had no owner-facing heartbeat write path. The existing fault helper scanned the full 19,200-entry telemetry ring, which is unnecessary for normal post-job fault checks and can react to stale historical fault flags.

What was done -> Added `TryRecordTelemetryHeartbeat(...)` overloads that write the latest `frame % 300` cursor into the DataVault-owned cursor buffer after the stalk job completes. Added `TryDumpBlackBoxOnFrameFault(...)` overloads that scan only the current 64-slot telemetry frame and dump the full black box only when that frame contains a `Fault`. Fixed the compiler-caught readonly carrier issue by passing the transient vault view carrier by value for the cursor write.

Cinematic Cheats used -> None. This pass is stability and dump-path pressure reduction.

Exact Microseconds saved -> Measured proof absent. Claimed measured savings: 0 us. Static cold-scan bound for normal post-job fault checks drops from 19,200 telemetry rows to 64 rows; hot Burst job cost is unchanged.

Verification -> First Bee/Csc pass caught CS8332 on the readonly heartbeat carrier; after the by-value fix, `dotnet exec csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.AI.Cognition.rsp` exits 0. Burst job branch scan returns `NO_BRANCH_TOKENS`; scoped AI/Cognition forbidden-token scan returns `NO_FORBIDDEN_TOKENS`; public-struct audit returns `ALL_PUBLIC_STRUCTS_PACK1`; `git diff --check` reports only CRLF normalization warnings. Root `dotnet build` still fails MSB1011 because the Unity folder contains multiple project/solution files.

## 2026-05-16 - AUP Fence Blackbox Polish

What was wrong -> Telemetry flagged `ShiftFenceReset`, but the dump did not carry the `ObservedShiftFrameId` that caused the reset. That weakens post-crash diagnosis for the "teleporting beast" class of AUP bugs.

What was done -> `LeviathanStalkJob` now writes `stimulus.ObservedShiftFrameId` into the existing telemetry `Reserved1` field. The dump writer already serializes that field, so no stride expansion, extra buffer, or public method signature change was required.

Cinematic Cheats used -> None. This pass is blackbox/AUP stability work.

Exact Microseconds saved -> Measured proof absent. Claimed measured savings: 0 us. Static hot-path delta is one uint store replacing a zero literal inside the existing telemetry entry write.

Verification -> `dotnet exec csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.AI.Cognition.rsp` exits 0. Burst job branch scan returns `NO_BRANCH_TOKENS`; scoped AI/Cognition forbidden-token scan returns `NO_FORBIDDEN_TOKENS`; corrected public-struct audit returns `ALL_PUBLIC_STRUCTS_PACK1`; `git diff --check` reports only CRLF normalization warnings. Root `dotnet build` still fails MSB1011 because the Unity folder contains multiple project/solution files.

## 2026-05-16 - Telemetry Contract Hygiene

What was wrong -> The telemetry contract carried public phase bytes, flags, and row fields without enough XML documentation. After assigning `Reserved1` to the observed AUP shift frame, that field needed an explicit contract without changing its binary offset.

What was done -> Added XML summaries to `AlphaLeviathanPhase`, `AlphaLeviathanTelemetryFlags`, and every `AlphaLeviathanTelemetryEntry` field. `Reserved1` is now documented as the observed AUP shift frame ID while preserving the 64-byte row layout.

Cinematic Cheats used -> None. This is public contract hygiene.

Exact Microseconds saved -> 0 us runtime. XML documentation does not change hot-path behavior.

Verification -> `dotnet exec csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.AI.Cognition.rsp` exits 0. Burst job branch scan returns `NO_BRANCH_TOKENS`; scoped AI/Cognition forbidden-token scan returns `NO_FORBIDDEN_TOKENS`; public-struct audit returns `ALL_PUBLIC_STRUCTS_PACK1`; `git diff --check` reports only CRLF normalization warnings. Root `dotnet build` still fails MSB1011 because the Unity folder contains multiple project/solution files.

## 2026-05-16 - Schedule Length Guard Pass

What was wrong -> The vault bridge could create a valid `LeviathanStalkJob`, but it did not return a canonical schedule length. That left owners responsible for manually picking a row count, which is how mismatched DataVault view lengths turn into out-of-bounds Burst execution.

What was done -> Added `GetScheduleLength(...)`, `TryGetScheduleLength(...)`, and guarded `TryCreateStalkJob(...)` overloads for both raw transient views and generation-checked handles. The old handle-first job factory overload remains source-compatible. `TryRecordTelemetryHeartbeat(...)` now refuses to write when the resolved views cannot schedule at least one row.

Cinematic Cheats used -> None. This is integration safety around the existing Dear Lie and high-tier overkill paths.

Exact Microseconds saved -> Measured proof absent. Claimed measured savings: 0 us. Static runtime impact is cold schedule-path integer min checks; the Burst job hot path is unchanged.

Verification -> `dotnet exec csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.AI.Cognition.rsp` exits 0 after the schedule guard and raw-view overload patches. Burst job branch scan returns `NO_BRANCH_TOKENS`; scoped AI/Cognition forbidden-token scan returns `NO_FORBIDDEN_TOKENS`; public-struct audit returns `ALL_PUBLIC_STRUCTS_PACK1`; `git diff --check` reports only CRLF normalization warnings. Root `dotnet build` still fails MSB1011 because the Unity folder contains multiple project/solution files.

## 2026-05-16 - Atomic Blackbox Dump Pass

What was wrong -> The black-box writer streamed directly into `Dump_PREDATOR_STALK_DIRECTOR.bin`. A fault during write or a slow MicroSD stall could leave a partial final dump that still looked authoritative.

What was done -> `TryDumpBlackBox(...)` now writes to `Dump_PREDATOR_STALK_DIRECTOR.bin.tmp` with `FileShare.None`, closes the binary writer, and promotes only the completed payload. Existing final dumps are replaced with `File.Replace(...)`; first-time dumps use `File.Move(...)`. Recoverable file/path failures delete the temp file and return false.

Cinematic Cheats used -> None. This is fault-path survival work, not simulation or rendering.

Exact Microseconds saved -> Measured proof absent. Claimed measured savings: 0 us. Hot Burst path is unchanged; the extra work is cold-path dump integrity only.

Verification -> `dotnet exec csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.AI.Cognition.rsp` exits 0. Burst job branch scan returns `NO_BRANCH_TOKENS`; scoped AI/Cognition forbidden-token scan returns `NO_FORBIDDEN_TOKENS`; public-struct audit returns `ALL_PUBLIC_STRUCTS_PACK1`; `git diff --check` reports only CRLF normalization warnings.

## 2026-05-16 - Crash Dump Handle Path Pass

What was wrong -> Handle-first owners could dump only through fault-scanning helpers. A hard crash can happen before the current frame is marked with `Fault`, and forcing the crash path to keep raw `NativeArray` views would violate the handle-first DataVault pattern.

What was done -> Added `TryDumpBlackBox(IDataVault, ref AlphaLeviathanVaultHandles, string)`. It resolves current DataVault views from handles and dumps the full 300-frame ring through the same temp-file promotion writer. Corrected the BufferID audit to parse only the `BufferID` enum and verified no duplicate DataVault lane IDs.

Cinematic Cheats used -> None. This is crash-path evidence and DataVault sovereignty work.

Exact Microseconds saved -> Measured proof absent. Claimed measured savings: 0 us. Hot Burst path is unchanged; the new cost exists only when a crash handler chooses to write the black box.

Verification -> `dotnet exec csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.AI.Cognition.rsp` exits 0. Burst job branch scan returns `NO_BRANCH_TOKENS`; scoped AI/Cognition forbidden-token scan returns `NO_FORBIDDEN_TOKENS`; public-struct audit returns `ALL_PUBLIC_STRUCTS_PACK1`; BufferID audit returns `NO_BUFFERID_DUPLICATES`; root `dotnet build` still fails MSB1011 because the folder contains multiple project/solution files.

## 2026-05-16 - Persisted State NaN Vaccine Pass

What was wrong -> The job sanitized steering outputs and telemetry, but persisted DataVault state could still carry non-finite aggression, phase start time, forward, or previous steering vectors. `PhaseStartSeconds` also existed as a state-machine field but was not updated.

What was done -> `LeviathanStalkJob` now sanitizes persisted aggression, phase start time, forward, and previous steering before reuse and before writeback. Active rows mark `Fault` when persisted state corruption is detected. `PhaseStartSeconds` now updates branchlessly when `CurrentPhase` changes, using sanitized `CurrentTimeSeconds`.

Cinematic Cheats used -> None. This is stability work that protects both low-tier interpolation and high-tier VFX intent from poisoned state.

Exact Microseconds saved -> Measured proof absent. Claimed measured savings: 0 us. Static cost is several finite checks and safe normalizations per row.

Verification -> `dotnet exec csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.AI.Cognition.rsp` exits 0. Burst job branch scan returns `NO_BRANCH_TOKENS`; scoped forbidden-token scan returns `NO_FORBIDDEN_TOKENS`; `git diff --check` reports only CRLF normalization warnings.

## 2026-05-16 - Parallel Telemetry Write Safety Pass

What was wrong -> The parallel job writes telemetry into a 300-frame ring at `frame * 64 + slot`, not at the direct `IJobParallelFor` index. Unity safety checks can reject that pattern without an explicit waiver.

What was done -> Added `[NativeDisableParallelForRestriction]` only to `TelemetryRing`. State and steering arrays keep normal restrictions because they write by dense slot index. The schedule-length guard remains the proof that each telemetry worker writes a unique row.

Cinematic Cheats used -> None. This is Unity job-safety integration work.

Exact Microseconds saved -> Measured proof absent. Claimed measured savings: 0 us. Attribute-only safety contract; no new hot-path scheduling.

Verification -> `dotnet exec csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.AI.Cognition.rsp` exits 0. Burst job branch scan returns `NO_BRANCH_TOKENS`; scoped forbidden-token scan returns `NO_FORBIDDEN_TOKENS`; public-struct audit returns `ALL_PUBLIC_STRUCTS_PACK1`; `git diff --check` reports only CRLF normalization warnings. Explicit `dotnet build Hecton8.slnx --no-restore` is blocked by missing Unity-generated project assets files for `Assembly-CSharp-Editor*` and MSB4166 child-node failure.

## 2026-05-17 - Locked Vault Recovery Pass

What was wrong -> The Alpha Leviathan vault bridge treated `IsAllocationLocked` as a total failure condition. That blocks legitimate post-init recovery of already allocated DataVault buffers and pressures owners to keep stale raw `NativeArray` views alive before the allocation sentinel locks.

What was done -> `TryResolve(...)` now resolves existing buffers with `TryGetBuffer(...)` under allocation lock. `TryResolveHandles(...)` now resolves existing handles with `TryGetBufferHandle(...)`, refreshes them through `TryResolveViews(...)`, and verifies required slot capacity. Locked recovery refuses undersized state/sensory/steering buffers and requires the full 300x64 telemetry ring.

Cinematic Cheats used -> None. This is DataVault sovereignty and memory-sentinel compatibility work.

Exact Microseconds saved -> Measured proof absent. Claimed measured savings: 0 us. Hot Burst path is unchanged; the patch adds cold resolve-path metadata checks only.

Verification -> `dotnet exec csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.AI.Cognition.rsp` exits 0. Burst job branch scan returns `NO_BRANCH_TOKENS`; scoped forbidden-token scan returns `NO_FORBIDDEN_TOKENS`; public-struct audit returns `ALL_PUBLIC_STRUCTS_PACK1`; BufferID audit returns `NO_BUFFERID_DUPLICATES`; `git diff --check` reports only CRLF normalization warnings.

## 2026-05-17 - AUP Snap-Fence Telemetry Pass

What was wrong -> `ShiftFenceActive` existed in the runtime flags but the job did not consume it. The black box could show the first frame that reset steering after a shift, but not the full 300-frame snap-fence window required by the AUP mandate.

What was done -> `LeviathanStalkJob` now reads `ShiftFenceActive` and marks telemetry with `ShiftFenceReset` for every fence-active row. Steering reset remains tied only to `shiftChanged`, so the fence marker does not freeze movement for 300 frames. The telemetry contract summary was updated without changing row size.

Cinematic Cheats used -> None. This is AUP crash-evidence work.

Exact Microseconds saved -> Measured proof absent. Claimed measured savings: 0 us. Static cost is one flag test and one OR/select per row.

Verification -> `dotnet exec csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.AI.Cognition.rsp` exits 0. Burst job branch scan returns `NO_BRANCH_TOKENS`; scoped forbidden-token scan returns `NO_FORBIDDEN_TOKENS`; public-struct audit returns `ALL_PUBLIC_STRUCTS_PACK1`; `git diff --check` reports only CRLF normalization warnings.

## 2026-05-17 - Strict Burst Float Mode Pass

What was wrong -> `LeviathanStalkJob` used `FloatMode.Fast`, while the installed Burst package documents that mode as allowing assumptions that values contain no NaNs or infinities. That is incompatible with a kernel whose stability contract depends on finite checks and fault telemetry.

What was done -> Changed only the stalking kernel Burst attribute to `FloatMode.Strict` with `FloatPrecision.Standard`. The package-local enum was checked before the edit; `Strict` is supported and `Default` maps to `Strict`, but the explicit mode keeps the safety contract visible.

Cinematic Cheats used -> Low-tier 5Hz cadence and radial fake remain the cost-control mechanism. High-tier SDF contour and visual-overkill scalar outputs remain unchanged.

Exact Microseconds saved -> Measured proof absent. Claimed measured savings: 0 us. This likely spends a small amount of Burst optimization headroom to preserve NaN/fault correctness; no profiler timing was captured.

Verification -> `dotnet exec csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.AI.Cognition.rsp` exits 0. Burst job branch scan returns `NO_BRANCH_TOKENS`; scoped forbidden-token scan returns `NO_FORBIDDEN_TOKENS`; public-struct audit returns `ALL_PUBLIC_STRUCTS_PACK1`; `git diff --check` reports only CRLF normalization warnings. Explicit `dotnet build Hecton8.slnx --no-restore` still fails outside AI/Cognition with missing Unity-generated `project.assets.json`, unrelated Gameplay/Tether/Interaction compile errors, and missing RealtimeCSG source files.

## 2026-05-17 - Blackbox Cursor Wrap Safety Pass

What was wrong -> `ResolveTelemetryCursor(...)` selected the largest raw `uint Frame`. During the first 299 frames after frame counter wrap, stale pre-wrap rows would look newer than valid post-wrap rows. Cleared default rows could also pollute cursor fallback because they have `Frame == 0`.

What was done -> The cursor fallback scan now ignores rows with `StateHash == 0` and uses unsigned wrap-aware frame comparison. The telemetry row stays 64 bytes and the Burst job stays unchanged.

Cinematic Cheats used -> None. This is black-box survival work, not presentation.

Exact Microseconds saved -> Measured proof absent. Claimed measured savings: 0 us. Hot path is unchanged; this adds only cold dump-scan checks.

Verification -> `dotnet exec csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.AI.Cognition.rsp` exits 0. Burst job branch scan remains `NO_BRANCH_TOKENS`; scoped forbidden-token scan returns `NO_FORBIDDEN_TOKENS`; public-struct audit returns `ALL_PUBLIC_STRUCTS_PACK1`; `git diff --check` reports only CRLF normalization warnings.

## 2026-05-17 - Aggression Delta Hitch Clamp Pass

What was wrong -> The aggression integrator accepted any finite positive `DeltaTime`. A hitch-sized producer value could saturate aggression in one tick and trigger charge behavior from a scheduling artifact.

What was done -> Added `MaxDeltaTimeSeconds = 0.25f` and clamp the job delta before applying `NoiseAggressionGainPerSecond`. The low-tier 5Hz cadence remains valid because 0.2s is below the cap.

Cinematic Cheats used -> The low-tier Dear Lie cadence remains the intended performance fake. The clamp prevents hitch recovery from becoming fake aggression.

Exact Microseconds saved -> Measured proof absent. Claimed measured savings: 0 us. Static cost is one `math.min` per row.

Verification -> `dotnet exec csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.AI.Cognition.rsp` exits 0. Burst job branch scan remains `NO_BRANCH_TOKENS`; scoped forbidden-token scan returns `NO_FORBIDDEN_TOKENS`; public-struct audit returns `ALL_PUBLIC_STRUCTS_PACK1`; `git diff --check` reports only CRLF normalization warnings.

## 2026-05-17 - Acoustic Lure Input Vaccine Pass

What was wrong -> The sonar lure gate consumed raw ping age and intensity. Negative age could keep a lure alive, and NaN handling depended on comparison behavior instead of an explicit sensory contract.

What was done -> Sanitized sonar ping age to finite non-negative input and saturated sonar intensity before `sonarActive` is computed. The 10s acoustic lure rule and DataVault row contract remain unchanged.

Cinematic Cheats used -> None. This protects the existing acoustic lure illusion from bad producer data.

Exact Microseconds saved -> Measured proof absent. Claimed measured savings: 0 us. Static cost is two scalar sanitization paths per row.

Verification -> `dotnet exec csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.AI.Cognition.rsp` exits 0. Burst job branch scan remains `NO_BRANCH_TOKENS`; scoped forbidden-token scan returns `NO_FORBIDDEN_TOKENS`; public-struct audit returns `ALL_PUBLIC_STRUCTS_PACK1`; `git diff --check` reports only CRLF normalization warnings.

## 2026-05-17 - Final Validation Re-Run

What was wrong -> Full solution validation is still blocked outside AI/Cognition. The latest explicit build reports missing Unity-generated editor `project.assets.json` files and missing RealtimeCSG source files.

What was done -> Re-ran the owned AI/Cognition compile and all domain static gates after the last code patches. Re-ran `dotnet build Hecton8.slnx --no-restore -v:minimal /clp:ErrorsOnly` and recorded the dependency wall instead of faking completion.

Cinematic Cheats used -> None. This is verification evidence.

Exact Microseconds saved -> 0 us runtime. Build triage only.

Verification -> Owned AI/Cognition Bee/Csc response file exits 0. Branch scan: `NO_BRANCH_TOKENS`. Forbidden-token scan: `NO_FORBIDDEN_TOKENS`. Struct audit: `ALL_PUBLIC_STRUCTS_PACK1`. `git diff --check` has only CRLF normalization warnings. Full solution build fails with 218 errors and 15 warnings from missing editor restore assets and RealtimeCSG source files outside this domain.

## 2026-05-17 - Phase Timestamp Sanitation Pass

What was wrong -> Phase start and current time were finite-checked but could still be negative. That lets bad producer time become persisted state-machine metadata.

What was done -> Clamped sanitized `PhaseStartSeconds` and `CurrentTimeSeconds` to non-negative values inside `LeviathanStalkJob` before phase transition writeback.

Cinematic Cheats used -> None. This is state recovery and black-box clarity.

Exact Microseconds saved -> Measured proof absent. Claimed measured savings: 0 us. Static cost is two `math.max` operations per row.

Verification -> `dotnet exec csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.AI.Cognition.rsp` exits 0. Burst job branch scan remains `NO_BRANCH_TOKENS`; scoped forbidden-token scan returns `NO_FORBIDDEN_TOKENS`; public-struct audit returns `ALL_PUBLIC_STRUCTS_PACK1`; `git diff --check` reports only CRLF normalization warnings.

## 2026-05-17 - Fog Distance Range Vaccine Pass

What was wrong -> The fog distance scalar was finite and positive but unbounded. A bad sensory row could produce a huge ring distance and target offset while still passing NaN checks.

What was done -> Added `MaxFogDistanceMeters = 2048f` and capped sanitized fog distance before ring-distance calculation in `LeviathanStalkJob`.

Cinematic Cheats used -> The normal fog-edge silhouette behavior remains. The cap only prevents pathological data from buying impossible scale.

Exact Microseconds saved -> Measured proof absent. Claimed measured savings: 0 us. Static cost is one `math.min` per row.

Verification -> `dotnet exec csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.AI.Cognition.rsp` exits 0. Burst job branch scan remains `NO_BRANCH_TOKENS`; scoped forbidden-token scan returns `NO_FORBIDDEN_TOKENS`; public-struct audit returns `ALL_PUBLIC_STRUCTS_PACK1`; `git diff --check` reports only CRLF normalization warnings.

## 2026-05-17 - AUP Overlap And Local Poison Vaccine Pass

What was wrong -> Same-position overlap was treated as invalid delta and raised `Fault`, despite the task requiring an `Up` fallback for that exact case. Selected AUP anchors could also carry non-finite local offsets into persisted `TargetAnchorAup`.

What was done -> Split finite-delta checks from separated-distance checks. Finite overlap now reports zero distance and branchlessly selects the `Up` fallback steering vector without marking a fault. Selected anchor and persisted Leviathan AUP locals are finite-checked and sanitized before double-distance math and DataVault writeback; non-finite AUP locals still raise active-row fault telemetry.

Cinematic Cheats used -> The overlap fallback is a deterministic motion fake: preserve a readable upward escape vector instead of simulating a collision solver inside cognition. Low-tier 5Hz behavior remains cheap; high-tier SDF/VFX scalar output remains available for valid stalking states.

Exact Microseconds saved -> Measured proof absent. Claimed measured savings: 0 us. Static cost is two float4 finite checks, two float4 selects, and one steering select per row; this prevents false dump work and NaN propagation rather than claiming speed.

Verification -> `dotnet exec csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.AI.Cognition.rsp` exits 0. Burst job branch scan returns `NO_BRANCH_TOKENS`; split scoped scans return `NO_PATHFINDING_TOKENS`, `NO_LOCAL_NATIVE_OR_DELEGATE_TOKENS`, and `NO_HOTPATH_UNITY_TOKENS`; public-struct audit reports no unpacked public structs and `STRUCT_AUDIT_DONE`; `git diff --check` reports only CRLF normalization warnings. No `dotnet build` solution rebuild was run in this pass.

## 2026-05-17 - Gaze Fallback Decontamination Pass

What was wrong -> Invalid or zero `PlayerForward` fell back to `awayFromAnchor`, making the dot-product gaze check read as full exposure. Missing sensory data could therefore raise `PlayerGazeBreak` and feed false fear/VFX intent.

What was done -> Changed the safe-normalize fallback for `PlayerForward` to `toAnchor`. Valid forward vectors are unchanged; invalid rows now produce zero gaze exposure after saturation.

Cinematic Cheats used -> The dot-product vision fake remains the intended cheap perception model. This patch keeps the fake honest by making missing data invisible instead of dramatic.

Exact Microseconds saved -> Measured proof absent. Claimed measured savings: 0 us. No extra ALU was added; the existing fallback vector changed.

Verification -> `dotnet exec csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.AI.Cognition.rsp` exits 0. Burst job branch scan returns `NO_BRANCH_TOKENS`; split scoped scans return `NO_PATHFINDING_TOKENS`, `NO_LOCAL_NATIVE_OR_DELEGATE_TOKENS`, and `NO_HOTPATH_UNITY_TOKENS`. No `dotnet build` solution rebuild was run in this pass.

## 2026-05-17 - Faulted Row Output Silence Pass

What was wrong -> Faulted active rows still produced steering, cadence, SDF, acoustic lure, gaze, and VFX output. That is not stable enough: a row can be bad enough to dump a black box and still drive downstream movement.

What was done -> Added a branchless `safeToAct` gate. `Fault` still records when active data is bad, but movement/output/VFX intent now require clean AUP and clean persisted state. The job still writes sanitized state back into the DataVault so a bad row can recover next frame.

Cinematic Cheats used -> No new simulation. The patch spends the existing cheap mask/select path to prevent corrupt rows from buying false silt, salt, SSS, silhouette, or particle overkill.

Exact Microseconds saved -> Measured proof absent. Claimed measured savings: 0 us. Static cost is one boolean mask and reused selects; any downstream saved work is unmeasured.

Verification -> `dotnet exec csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.AI.Cognition.rsp` exits 0. Burst job branch scan returns `NO_BRANCH_TOKENS`; split scoped scans return `NO_PATHFINDING_TOKENS`, `NO_LOCAL_NATIVE_OR_DELEGATE_TOKENS`, and `NO_HOTPATH_UNITY_TOKENS`; public-struct audit reports `STRUCT_AUDIT_DONE`; `git diff --check` reports only CRLF normalization warnings. No `dotnet build` solution rebuild was run in this pass.

## 2026-05-17 - Telemetry Bit Collision Purge Pass

What was wrong -> AI/Cognition wrote `AcousticLure` to telemetry bit 5 while nearby Fauna telemetry already used bit 5 as `AlphaLeviathanTelemetryNoPlayerTarget`. That made the shared black-box byte ambiguous.

What was done -> Stopped writing acoustic lure into the legacy telemetry byte, renamed the shared bit 5 constant to `LegacyNoPlayerTarget`, and added `AlphaLeviathanSteeringIntentFlags` plus byte-sized `AlphaLeviathanSteeringOutput.IntentFlags`. The output field uses existing tail padding in the 88-byte steering row. Also hardened `AlphaLeviathanAup.ToAbsoluteDouble3()` so raw non-finite local offsets are zeroed before double3 conversion.

Cinematic Cheats used -> Acoustic lure remains a cheap DataVault-driven steer fake. The new intent flags let render/VFX consumers see sonar, SDF, gaze, light, shift, low-tier, and fault intent without abusing the black-box byte.

Exact Microseconds saved -> Measured proof absent. Claimed measured savings: 0 us. Static cost is seven branchless byte intent selects per row and one float4 finite select per AUP conversion; one colliding telemetry OR was removed.

Verification -> `dotnet exec csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.AI.Cognition.rsp` exits 0. Burst job branch scan returns `NO_BRANCH_TOKENS`; scoped AI/Cognition forbidden-token scan returns `NO_FORBIDDEN_TOKENS`; public-struct audit returns `PUBLIC_STRUCT_PACK_SCAN_DONE`; acoustic scan shows no `AlphaLeviathanTelemetryFlags.AcousticLure` references and Fauna's bit 5 remains read-only. `git diff --check` reports only CRLF normalization warnings. No solution rebuild was run.

## 2026-05-17 - Packed Intent ABI Repair Pass

What was wrong -> The intent side-channel was first added as `uint IntentFlags` while preserving `AlphaLeviathanSteeringOutput` at `Size = 88`. Under `Pack = 1`, the row only had 3 tail bytes after `Flags`; a uint field would not fit the declared ABI.

What was done -> Changed steering intent constants and the output field to byte. Seven intent bits still fit, the job remains branchless, and the steering output stays at 88 bytes without forcing DataVault stride churn.

Cinematic Cheats used -> None new. This preserves the existing cheap intent channel for sonar lure, SDF contouring, gaze, light retreat, shift fence, low-tier fallback, and faulted input.

Exact Microseconds saved -> Measured proof absent. Claimed measured savings: 0 us. Static payload stays 88 bytes instead of expanding to the rejected 92-byte row.

Verification -> `dotnet exec csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.AI.Cognition.rsp` exits 0. Burst job branch scan returns `NO_BRANCH_TOKENS`; scoped forbidden-token scan returns `NO_FORBIDDEN_TOKENS`; public-struct scan returns `PUBLIC_STRUCT_PACK_SCAN_DONE`; source scan shows `IntentFlags` is byte and `AlphaLeviathanSteeringOutput` remains `Size = 88`. `git diff --check` reports only CRLF normalization warnings. No solution rebuild was run.

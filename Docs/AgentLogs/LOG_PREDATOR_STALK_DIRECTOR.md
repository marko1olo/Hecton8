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

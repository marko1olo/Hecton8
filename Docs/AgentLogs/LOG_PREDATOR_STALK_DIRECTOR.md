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

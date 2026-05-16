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

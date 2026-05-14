# Rationale_INTEGRATION_ASSEMBLY_SURGEON

Agent: INTEGRATION_ASSEMBLY_SURGEON
Domain: Unity Compilation Graph / Integrator
Status: PENDING VERIFICATION

## Decision 0 - Session Initialization

Problem: No existing status or rationale file for this assigned compile medic ID.
Solution: Created disk-backed checklist and rationale before touching compile code, using the batch prompt as primary directive and mandate registry as the technical filter.
Rejected Alternatives: Chat-only progress was rejected because the batch protocol makes disk state authoritative. Reading prior unrelated agents' logs as architectural input was rejected because the prompt says to ignore neighboring tasks unless they are compile artifacts.
Scalability potential: Low/Middle/High/Ultra are not runtime visual tiers for this initialization step; compile-boundary cleanup preserves later tier-specific runtime work by avoiding dependency cycles.
Hardware Impact: 0 us runtime change on i3/MX350. This is process state only.

## Decision 1 - Mandate Selection

Problem: Compile wall can tempt brute-force project-reference additions that create cycles.
Solution: Selected `ARCH_Global_Registry_ServiceLocator_DI_Init`, `PROJECT_LTS_Compatibility_Layer`, `OPT_Zero_GC_Policy_AllocFree_Mandate`, `OPT_Native_Memory_Collections_JobSystem_Protocol`, and `QA_Evidence_Text_Filter_Audit` as the active mandate set.
Rejected Alternatives: Rendering/physics/audio domain mandates were rejected until diagnostics prove domain-specific runtime edits are required. Adding leaf assemblies to Core was rejected under H-PHI defense.
Scalability potential: Low tier benefits from strict contract isolation because it keeps Core compile/runtime surface smaller; Ultra tier remains free to add overkill visuals behind leaf systems without contaminating Core.
Hardware Impact: Expected runtime gain is indirect only: fewer hard references and smaller compile/runtime dependency surface. Estimated frame impact: 0 us until runtime code is touched.

## Decision 2 - Stale Build Wall Triage

Problem: First build reported `LeviathanTerrainIkJob.TailWhipDurationSeconds` and `PrologueSplashdownSineSweepProbeJob` missing, but current source already contained both symbols and line numbers had shifted.
Solution: Rebuilt before editing source. The second build cleared both errors, proving the first wall was stale relative to concurrent source churn.
Rejected Alternatives: Adding duplicate shim fields/jobs was rejected because it would create type noise around already-present symbols.
Scalability potential: Low/Middle/High/Ultra runtime tiers unchanged. Avoiding duplicate shims preserves single source of truth for later visual-tier logic.
Hardware Impact: 0 us runtime change. Compile-only decision.

## Decision 3 - Duplicate IK Assembly Surface

Problem: Core compile saw `LeviathanTerrainIkJobs.cs` as source while also resolving stale `Library/ScriptAssemblies/Hecton8.Animation.IK.dll`, producing CS0436 conflicts.
Solution: Removed the duplicate `Hecton8.Animation.IK` MSBuild bridge reference from the Core source-backed project bridge surface. Current `Directory.Build.targets` no longer contains that reference.
Rejected Alternatives: Editing generated `Hecton8.Core.csproj` was rejected because Unity overwrites it. Patching the stale DLL was rejected because `Library/ScriptAssemblies` is generated output. Removing current source was rejected because it contained the newer field required by `FaunaKinematicsRuntime`.
Scalability potential: Low tier avoids ambiguous stale IK contracts; Ultra tier keeps current tail-whip duration field available to the high-fidelity IK path.
Hardware Impact: Expected runtime impact 0 us; compile graph cleanup only. Indirect editor iteration gain: avoids duplicate symbol resolution warnings in Core.

## Decision 4 - Vendor Warning Boundary

Problem: Raw project-reference build succeeds but reports 47 warnings from URP, GPUInstancer, Crest, ShaderGraph, and WaveHarmonic Crest projects.
Solution: Treated those as third-party/vendor warnings under the third-party integrity rule and captured a separate isolated Core compile with `-p:BuildProjectReferences=false`, which returned 0 warnings and 0 errors.
Rejected Alternatives: Editing package/vendor code was rejected by third-party asset integrity. Suppressing vendor warnings globally was rejected as false evidence. Reporting raw build as zero-warning was rejected by QA evidence law.
Scalability potential: Low tier remains protected from first-party Core compile errors; high/ultra visual packages still require separate vendor-maintenance work outside this emergency compile pass.
Hardware Impact: Runtime impact 0 us. Build evidence impact: first-party Core wall is green; vendor warning debt remains non-runtime compile-noise debt.

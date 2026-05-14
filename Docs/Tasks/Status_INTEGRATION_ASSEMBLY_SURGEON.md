# Status_INTEGRATION_ASSEMBLY_SURGEON

Agent: INTEGRATION_ASSEMBLY_SURGEON
Role: SYSTEMS_ARCHITECT
Domain: Unity Compilation Graph / Integrator
Prompt task count: 17
Current state: PENDING VERIFICATION

## Mandates Read

- [x] `.agents-skills/README.md` | Justification: registry authority before selecting task mandates | Alternatives rejected: bulk-loading all mandates as context noise | Estimate: 400 us
- [x] `ARCH_Global_Registry_ServiceLocator_DI_Init.txt` | Justification: compile wall likely includes service/cycle boundaries | Alternatives rejected: direct leaf-to-Core references | Estimate: 600 us
- [x] `PROJECT_LTS_Compatibility_Layer.txt` | Justification: asmdef and compatibility boundaries are the primary failure class | Alternatives rejected: editing generated csproj as source of truth | Estimate: 700 us
- [x] `OPT_Zero_GC_Policy_AllocFree_Mandate.txt` | Justification: any contract extraction must preserve hot-path allocation law | Alternatives rejected: managed wrapper shims in runtime paths | Estimate: 500 us
- [x] `OPT_Native_Memory_Collections_JobSystem_Protocol.txt` | Justification: Core contracts may include NativeQueue/NativeArray/job-facing structs | Alternatives rejected: managed bridge types inside Burst-facing contracts | Estimate: 700 us
- [x] `QA_Evidence_Text_Filter_Audit.txt` | Justification: reports must distinguish CLI compile from Unity runtime proof | Alternatives rejected: claiming green from stale logs | Estimate: 300 us

## Pre-Code Analysis

Target: `Hecton8.Core.csproj` compile wall and first-party asmdef/contract boundaries.
Affected systems: Core contracts, assembly definition references, generated root project bridge, duplicate member cleanup in compile-blocking files only.
Zero GC proof: planned changes are compile-boundary/data-contract fixes; no hot-path allocation may be introduced. Any new shared types must be unmanaged structs where Burst/job-facing.
State check: status/rationale initialized from missing state; no previous INTEGRATION_ASSEMBLY_SURGEON data exists. Shared workspace has active logs from other agents; unrelated edits must not be reverted.
Rule quote: `H-PHI DEFENSE: Do NOT solve missing dependencies by adding Hecton8.Core as a reference to a leaf node. Move the shared data down to Contracts.`

## Checklist

- [ ] Task 1: READ THE WALL | Justification: run `dotnet build Hecton8.Core.csproj --no-restore`; parse first 20 errors | Alternatives rejected: stale log trust | Estimate: pending
- [ ] Task 2: ASMDEF REPAIR | Justification: ensure dependent asmdefs reference contracts without cycles | Alternatives rejected: Core referencing leaf assemblies | Estimate: pending
- [ ] Task 3: STRUCT MIGRATION | Justification: shared data belongs in Contracts when Core needs it | Alternatives rejected: concrete leaf dependency | Estimate: pending
- [ ] Task 4: NAMESPACE ALIGNMENT | Justification: moved contracts must compile from all consumers | Alternatives rejected: duplicate type aliases | Estimate: pending
- [ ] Task 5: DUPLICATE MEMBER PURGE | Justification: stale duplicate methods are direct compile blockers | Alternatives rejected: partial-class masking | Estimate: pending
- [ ] Task 6: PROJECT FILE SYNC | Justification: source tree and generated bridge must allow Bee/Roslyn discovery | Alternatives rejected: hand-editing generated root csproj unless unavoidable | Estimate: pending
- [ ] Task 7: COMPILER DIRECTIVES | Justification: package-absent code must be isolated with explicit guards | Alternatives rejected: adding packages or fake package APIs | Estimate: pending
- [ ] Task 8: H-PHI DEFENSE | Justification: break cycles by contracts/interfaces only | Alternatives rejected: leaf-to-Core back edges | Estimate: pending
- [ ] Task 9: BATCHED COMPILE | Justification: fix small error batches and rebuild repeatedly | Alternatives rejected: one hallucinated mass patch | Estimate: pending
- [ ] Task 10: OMEGA COMPILE CHECK | Justification: final target is CLI build success, zero warnings/errors if reachable | Alternatives rejected: partial success report | Estimate: pending
- [ ] Task 11: RE-READ PROMPT | Justification: anti-amnesia after compile passes | Alternatives rejected: relying on chat memory | Estimate: pending
- [ ] Task 12: RECHECK TASKS | Justification: every item must be verified or blocked | Alternatives rejected: checkbox-only reporting | Estimate: pending
- [ ] Task 13: FLAW SWEEP | Justification: inspect own changes for architectural flaws | Alternatives rejected: compile-only tunnel vision | Estimate: pending
- [ ] Task 14: ILateFrameTickable PATCH IF PRESENT | Justification: emergency compile directive allows empty contract body where required | Alternatives rejected: broad runtime logic invention | Estimate: pending
- [ ] Task 15: FINAL STATUS BUILD SUCCESSFUL OR BLOCKED | Justification: exact evidence class and artifact path required | Alternatives rejected: optimistic status | Estimate: pending
- [ ] Task 16: FINAL LOG APPEND | Justification: CTO reads disk logs, not chat | Alternatives rejected: chat-only report | Estimate: pending
- [ ] Task 17: POLISH MANDATE AFTER CORE STATUS | Justification: only parse/execute after all core tasks checked or blocked | Alternatives rejected: premature polish scope creep | Estimate: pending

## Loop Ledger

- Loop 0: Initialized mandate/status state. Build wall not yet run. Status: PENDING VERIFICATION.

# Rationale_INTEGRATION_ASSEMBLY_SURGEON

Agent: INTEGRATION_ASSEMBLY_SURGEON
Domain: Echelon 9 / The Integrator (Compile Medic)
Status: BUILD SUCCESSFUL / PLATINUM GRADE / CURRENTDISK53 + CURRENTDISKBUDGETGATE22 CURRENT-DISK GREEN

## Decision 1 - RenderGraph Compatibility Wall

Problem: Current source hit compile drift around URP RenderGraph helper availability. The simple `RenderGraph.AddBlitPass` overload was not visible in the active Core compile lane.
Solution: Accept the project-compatible RasterGraph copy path using explicit `_BlitTexture` binding and fullscreen shader copy support where needed. This keeps the visual pass behavior while avoiding package/API-version dependence.
Rejected Alternatives: Editing generated `.csproj` files, widening package references, changing Unity package source, or replacing the dry-volume/noir stack with a behavior-changing stub.
Scalability potential: Low tier keeps a predictable single fullscreen copy/resolve path. Middle keeps the same visual composition. High and Ultra retain the dry-volume/noir visual stack without contaminating Core dependency graph.
Hardware Impact: Runtime frame savings are 0 us measured. This is compile compatibility, not profiler evidence.

## Decision 2 - Evidence Rebuild After AgentLog Churn

Problem: Concurrent conflict-resolution/doc agents removed earlier Integrator status and evidence artifacts from `Docs/AgentLogs` and `Docs/Tasks`; reporting deleted artifacts would be false.
Solution: Rebuilt fresh evidence on current disk: `CurrentDisk53` for compile and `CurrentDiskBudgetGate22` for strict H-Phi. Recreated the Integrator status/rationale/log files with only current artifact references.
Rejected Alternatives: Restoring the entire deleted AgentLogs tree was rejected because those deletions belong to another active workspace change. Claiming previous artifact names was rejected because the files were missing on disk.
Scalability potential: Low/Middle/High/Ultra runtime tiers unchanged. The value is evidence integrity during parallel agent churn.
Hardware Impact: Runtime frame savings are 0 us measured. Compile verification cost: 2,327,083 us. H-Phi verification cost: 116,430,187 us.

## Decision 3 - Tight H-Phi Budget Closure

Problem: Older H-Phi gates allowed looser source-debt ceilings than the current static scan.
Solution: Ran strict full-source H-Phi with exact ceilings: `GlobalRegistrySurface=5060`, `LinqSurface=3`, `ManagedFormatSurface=534`, `PrimaryManagedRuntimeRisk=147`, `FindObjectCalls=0`, `UnityUpdateMethods=0`, `AupPrecisionRisk=0`, plus Core graph budgets at 25/10/14/8/6.
Rejected Alternatives: Keeping looser Gate16-era budgets, lowering the H-Phi score floor, or treating static H-Phi as runtime/profiler proof.
Scalability potential: Low tier gets tighter no-regression guardrails against registry and managed-runtime sprawl. High and Ultra preserve visual-overkill freedom in leaf systems while the Integrator blocks renewed Core coupling.
Hardware Impact: Runtime frame savings are 0 us measured. Static `RuntimeHPhiRisk=0.000636091`; this is static evidence only.

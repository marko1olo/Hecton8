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

## 2026-05-16 Current Batch Re-Entry - Checklist Reset

Problem: Active disk status was stale for the current prompt. `Status_INTEGRATION_ASSEMBLY_SURGEON.md` claimed task count 15 while `CURRENT_BATCH.md` defines 18 tasks for the same id.
Solution: Reset the active checklist to the current XML task list and record the mismatch as a hygiene violation before touching source.
Rejected Alternatives: Reusing stale status, reporting previous build artifacts as current, or reading neighboring batch prompts.
Scalability potential: Low/Middle/High/Ultra runtime tiers unchanged. The value is compile evidence integrity under 20+ parallel agents.
Hardware Impact: Runtime gain is 0 us measured. This is process correction; no frame-time claim.

Problem: The current task is an assembly graph compile wall, not a gameplay feature.
Solution: Restrict edits to assembly definitions, contracts, source guards, duplicate compile blockers, and evidence logs under the authorized domain.
Rejected Alternatives: Adding gameplay code, widening concrete cross-domain references, or changing public APIs without a legacy wrapper.
Scalability potential: Low tier benefits from strict compile-layer isolation and no accidental managed hot-path debt; High/Ultra preserve visual-system freedom behind explicit assembly boundaries.
Hardware Impact: Runtime gain is 0 us measured. Expected benefit is lower build/iteration debt, not profiler-proven frame savings.

## 2026-05-16 Compile Wall Closure - Loop37 Green

Problem: The active compile wall was polluted by stale dumps and parallel `dotnet/csc` processes. `Dump_COMPILE_ERROR.txt` showed 81 `SargassumMicroFaunaBoids`/`RepairTool` errors that no longer matched the source on disk, while concurrent agents kept spawning normal builds against shared `Temp/obj`.
Solution: Ran an isolated Core build with separate `Temp/obj_integrator` and `Temp/bin_integrator` paths, then verified the standard Core build evidence on disk. Current evidence: `Build_INTEGRATION_ASSEMBLY_SURGEON_20260516_110605_Loop37.log` reports `Build succeeded. 0 Warning(s). 0 Error(s).`; `Dump_INTEGRATION_ASSEMBLY_SURGEON_isolated_build.binlog` also captured a green isolated compile.
Rejected Alternatives: Killing other agents' compiler processes, editing against stale CS0103 lines, or claiming green from generated output without a fresh build.
Scalability potential: Low tier gets stable compile gates for Android/Quest source guards; Middle/High/Ultra retain feature code behind explicit contracts instead of emergency stubs.
Hardware Impact: Runtime gain is 0 us measured. Compile verification cost: standard build 89,860,000 us; isolated confirmation 72,330,000 us.

Problem: Quest/Android portability still had unguarded Windows-only memory mapping and ABI-risky contract structs.
Solution: Guarded runtime `System.IO.MemoryMappedFiles` code in `InputDispatcher` and `H8MacroDatabaseService` behind `UNITY_EDITOR || UNITY_STANDALONE`; set `Pack = 1` on `BrineLayerSample` and `AcousticAup`.
Rejected Alternatives: Leaving MMF namespaces visible to mobile compilation, adding platform-specific assembly forks, or relying on default struct packing for ARM64.
Scalability potential: Low/Quest tiers avoid compile/runtime platform traps; High/Ultra keep memory-mapped fast paths on editor/standalone.
Hardware Impact: Runtime gain is 0 us measured. Low-tier value is crash-risk reduction, not profiler-proven frame savings.

Problem: IL2CPP stripping risk remained in `link.xml` because the preserved signal namespace was stale.
Solution: Updated linker preservation to real `Hecton8.Core.Contracts.Signals` types: `SignalBus<T>`, registry, `ISignal`, `IInitializable`, and snapshot transformer.
Rejected Alternatives: Preserving obsolete `Hecton8.Core.Signals` entries only, or broad assembly preserve.
Scalability potential: ARM64/IL2CPP keeps typed signal lanes alive without preserving whole assemblies; High/Ultra avoid managed fallback paths caused by stripped generic buses.
Hardware Impact: Runtime gain is 0 us measured. Build/player survival risk reduced.

Problem: A concurrent DataVault migration left `ToolDurabilitySystem` with missing local NativeArray symbols and a queue-to-vault transition half-applied.
Solution: Resolved vault handles through `GlobalRegistry.DataVault`, cached `NativeArray` aliases without owning disposal, and changed breakdown event flushing to a vault-backed flag buffer.
Rejected Alternatives: Reverting the migration to private persistent NativeArrays, keeping a local `NativeQueue` for breakdown events, or adding gameplay behavior.
Scalability potential: Low tier centralizes memory ownership for deterministic budget tracking; High/Ultra can scale tool telemetry without per-system allocation sprawl.
Hardware Impact: Runtime gain is 0 us measured. Expected benefit is reduced leak risk and better vault observability.

## 2026-05-16 Tail Repair - Loop39 Current Disk Green

Problem: Parallel edits reintroduced a hard Core compile dependency on `Hecton8.AI.Ecosystem` through `EcosystemRuntimeInstaller` and `BinaryLayoutManifest`. Loop38 failed with 2 CS0234 errors and wrote the failure to `Docs/AgentLogs/Dump_COMPILE_ERROR.txt`.
Solution: Replaced the concrete `EcosystemPopulationBalancer` add path with cold-path reflection, converted ecosystem population binary-layout checks to optional reflection, and removed the forced `EcosystemPopulationBalancer.cs` compile include from `Directory.Build.targets`.
Rejected Alternatives: Adding a direct Core reference to the AI ecosystem leaf assembly, compiling the whole AI population balancer into Core, or deleting the binary layout verification outright.
Scalability potential: Low/Quest builds keep Core independent from AI leaf assemblies; Middle/High/Ultra still validate ecosystem population layout when the leaf assembly is loaded.
Hardware Impact: Runtime gain is 0 us measured. Compile recovery evidence: Loop38 failed in 34,430,000 us; Loop39 succeeded in 60,510,000 us.

Problem: Strict asmdef graph enforcement was not stable under concurrent file churn; `Hecton8.Core.Content.Editor.asmdef` reverted to `autoReferenced: true`.
Solution: Set it back to `autoReferenced: false` and reran the asmdef scan. Current result: `ASMDEF_COUNT=94`, `AUTO_REFERENCED_TRUE_COUNT=0`, `ASMDEF_CYCLES=0`, and `MISSING_HECTON8_REFERENCES=0` across all Assets asmdefs.
Rejected Alternatives: Leaving editor assemblies auto-injected, editing generated projects, or removing live Unity/package references from asmdefs.
Scalability potential: Low/Middle/High/Ultra builds get explicit dependency graph behavior instead of hidden auto-reference edges.
Hardware Impact: Runtime gain is 0 us measured. This is compile graph debt removal.

Problem: `_MATH_LOD_LOW` had runtime/shader paths but no robust MSBuild-level explicit low-tier trigger for standalone low builds.
Solution: Added a guarded `Directory.Build.targets` define injection for Android, `HECTON_LOW_TIER`, `HECTON_MATH_LOD_LOW`, `HectonMathLodLow=true`, or `HectonBuildTier=Low`; verified with an MSBuild property probe that `_MATH_LOD_LOW` appears.
Rejected Alternatives: Forcing low-tier defines into all standalone/editor builds, which would downgrade PC high-tier visual paths.
Scalability potential: Toaster mode gets the cheap math path; High/Ultra standalone remains free to use expensive visual-overkill paths.
Hardware Impact: Runtime gain is 0 us measured in this shell. Expected benefit is platform-tier correctness, not profiler proof.

## 2026-05-16 Tail Integration Revalidation

Problem: After commit `d7dd79d3c`, concurrent agents added a new dirty tail. `Docs/AgentLogs/Dump_COMPILE_ERROR.txt` rotated through stale `Hecton8.AI.Ecosystem`, missing `BinaryBlittableSafe`, and missing `BinaryLayoutManifest` helper errors while source was still changing.
Solution: Re-read current source, re-extracted the XML prompt with an attribute-aware CLI regex, ran fresh normal Core compiles, and treated stale dumps as obsolete evidence. Current tail artifact: `Build_INTEGRATION_ASSEMBLY_SURGEON_20260516_tail08.log` reports `Build succeeded. 0 Warning(s). 0 Error(s). EXIT=0` after restoring `BinaryLayoutManifest` Type-based helper methods, preserving the `Hecton8.Core.Memory.Layout` import in `H8BridgeContracts`, and absorbing later bridge/content edits.
Rejected Alternatives: Editing files to fix line numbers that no longer existed, deleting another agent's ecosystem work, or committing against stale failure evidence from tail05/tail06/tail07.
Scalability potential: Low/Quest tiers avoid direct Core-to-AI assembly pressure; High/Ultra retain ecosystem population validation through optional layout probes without forcing a compile-time dependency.
Hardware Impact: Runtime gain is 0 us measured. Latest tail compile verification cost: 74,320,000 us.

## 2026-05-16 ABI Pack Tail Revalidation

Problem: A post-push Core contracts tail added ARM64 ABI hardening to MacroDatabase, PersistencePaging, and Prologue sequence DTOs. Without fresh compile evidence, committing it would satisfy the Pack=1 mandate on paper but leave the generated Core project unverified.
Solution: Kept the change scoped to declared `StructLayout` attributes, verified all touched contract layouts now include `Pack = 1`, checked for conflict markers, and reran `dotnet build Hecton8.Core.csproj --no-restore`.
Rejected Alternatives: Reverting the concurrent Pack=1 additions, broad DTO rewrites, or claiming Quest/Android ABI safety from default sequential layout.
Scalability potential: Low/Quest/Android tiers get deterministic binary contract packing for ARM64; Middle retains the same DTO surface; High/Ultra keep the same visual/gameplay systems without hidden padding drift at contract boundaries.
Hardware Impact: Runtime gain is 0 us measured. Compile verification cost: 34,760,000 us. Crash-risk reduction is ABI stability, not profiler-proven frame time.

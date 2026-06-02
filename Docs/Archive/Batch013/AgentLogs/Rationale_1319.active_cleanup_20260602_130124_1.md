# Rationale 1319 - MEMORY_SOVEREIGN_LOGISTICS_GRAPH_EXORCIST

State: STATIC PASS / BUILD BLOCKED OUTSIDE DOMAIN

## Session Start

Problem: Agent state files were absent while the batch protocol requires disk-backed memory.
Solution: Created `Docs/Tasks/Status_1319.md` and this rationale file before code mutation.
Rejected Alternatives: Chat-only state; invalid because context compression loses state and violates batch protocol.
Scalability potential: Low/Middle/High/Ultra unchanged. This is process state, not runtime logic.
Hardware Impact: 0 us runtime impact on i3/MX350.

Problem: Root-level `C:\hades\current_batch.md` was absent.
Solution: Used `Docs/Tasks/CURRENT_BATCH.md` and extracted `<AGENT_PROMPT id="1319">` with raw PowerShell regex.
Rejected Alternatives: Proceeding without prompt extraction; invalid because task count/domain would be unverified.
Scalability potential: Not runtime.
Hardware Impact: 0 us runtime impact on i3/MX350.

## Primary Graph Decisions

Problem: `LogisticsNetworkGraph.cs` owned 50 persistent native aliases directly in the graph object.
Solution: Replaced them with `VaultGenerationHandle<T>` descriptors and transient DataVault-resolved views; counts stayed scalar where no native container was needed.
Rejected Alternatives: Keep `NativeArray` fields with better disposal; still violates one-owner DataVault route and leaves physical aliases cached across compaction boundaries.
Scalability potential: Low uses smaller capacities/cadence through existing continuous quality; Middle/High/Ultra reuse the same descriptor route with larger capacities and adaptive solve slices.
Hardware Impact: i3/MX350 avoids persistent-container churn and hidden ownership ambiguity; estimated 28 us hot-path risk removed, 210 us cold build control.

Problem: `NativeList`, native hash maps, native multimaps, and native queues were used where fixed graph capacities already existed.
Solution: Converted topology edges, producers, consumers, producer rates, consumer demand, and traversal to fixed vault-backed arrays plus counts/CSR adjacency.
Rejected Alternatives: Recreate native containers inside DataVault; wrong abstraction because graph capacities are bounded and Burst jobs need flat arrays.
Scalability potential: Low gets deterministic flat array walks; Middle/High/Ultra spend saved cycles on larger solve slices and visual feedback, not container indirection.
Hardware Impact: i3/MX350 saves branch/hash overhead and allocator pressure; estimated 28 us per solve/build pressure removed.

Problem: Read accessors could complete jobs indirectly.
Solution: Accessors now fail closed when evaluation or publication is pending; explicit completion remains outside read APIs.
Rejected Alternatives: Continue hidden `.Complete()`; creates unpredictable frame spikes and violates pure read doctrine.
Scalability potential: Low avoids stalls; Middle/High/Ultra can poll snapshots at higher cadence without mutating schedule state.
Hardware Impact: Worst-frame risk reduced; exact spike avoided depends on outstanding job length.

Problem: DTO alignment had no primary-file proof artifact for the touched logistics summary/record structs.
Solution: Added explicit layouts and `ValidateMemorySovereignLayouts` using `UnsafeUtility.SizeOf` plus field offsets.
Rejected Alternatives: Rely on review memory or `StructLayout` alone.
Scalability potential: Same binary DTO layout from weak devices to Ultra hardware.
Hardware Impact: 0 us hot path; prevents ARM64 mispack faults.

Problem: Crash dump route was generic instead of agent/task specific.
Solution: Routed the primary black box to `Docs/AgentLogs/Dump_1319_Logistics.bin` and retained the 300-frame vault-backed ring.
Rejected Alternatives: Managed strings or chat-only crash explanations.
Scalability potential: Low captures minimal fixed ring; Middle/High/Ultra keep same ring contract without changing gameplay authority.
Hardware Impact: 0 us until fault; bounded binary dump on fault.

## Domain Sweep Decisions

Problem: `ShinobuLogisticsRouter` had 25 class-level native aliases already represented by DataVault handles.
Solution: Converted those aliases to transient handle-resolved properties and kept job signatures as NativeArray views.
Rejected Alternatives: Leave cached physical aliases; violates DataVault compaction and sovereign ownership.
Scalability potential: Low/Middle/High/Ultra keep same fixed vault storage and no extra managed route.
Hardware Impact: 0 GC; minor resolve cost only when methods touch the views.

Problem: `SubmarineOsThermalGridRuntime` used struct view packets with NativeArrays that were stack-local by design but not compiler-enforced as stack-only.
Solution: Changed `VaultViews` and `CsvImportViews` to `ref struct`.
Rejected Alternatives: Move all fields to class state or ignore audit. Class state is worse; ignoring audit leaves false persistent aliases.
Scalability potential: No runtime behavior change.
Hardware Impact: 0 us; static ownership clarity.

Problem: `PowerGridTelemetryEvents` used two persistent `NativeQueue` lanes for capacity-8 managed listener dispatch.
Solution: Replaced queues with fixed cold static arrays and counts; removed sentinel/allocator path.
Rejected Alternatives: DataVault route for listener events. This is managed listener dispatch, not cross-domain native data ownership.
Scalability potential: Low avoids native queue overhead; Middle/High/Ultra still bounded at 8 events by dispatcher budget.
Hardware Impact: Estimated 4 us saved in registration/drain paths and no native queue lifetime risk.

Problem: `WfcOutpostGridRegistry` stored native grid copies in a static `NativeArray<byte>[]`.
Solution: Replaced physical arrays with `VaultGenerationHandle<byte>[]`; `WfcOutpostGridLease` is a `readonly ref struct` so native views cannot be persisted.
Rejected Alternatives: Keep array slots and add disposal. Disposal does not fix persistent physical alias storage.
Scalability potential: Low keeps 4 fixed slots; Middle/High/Ultra can reuse same vault slots with larger upstream visual density without route changes.
Hardware Impact: 0 GC; removes persistent native slot ownership.

Problem: `WfcOutpostPowerBootRuntime` owned six persistent native fields and a native multimap for edges.
Solution: Replaced fields with vault handles and flat `NativeArray<int2>` edge pairs; graph build now imports edges in one linear pass.
Rejected Alternatives: DataVault-backed multimap; still the wrong shape for bounded directed edges and slower to traverse.
Scalability potential: Low imports cheap flat edges; Middle/High/Ultra can increase generation density within MaxDirectedEdges without hash-map overhead.
Hardware Impact: Estimated 140 us saved on max outpost import after removing accidental O(nodes * edges) pass.

Problem: First constrained build exposed CS1612 errors in `LogisticsNetworkGraph` caused by assigning through NativeArray-returning properties.
Solution: Replaced mutation sites with local-safe `WriteNative`/`AddNative` helper calls and applied the same fix to `ShinobuLogisticsRouter` and WFC boot telemetry.
Rejected Alternatives: Restore persistent NativeArray fields; that would reintroduce the native alias violation.
Scalability potential: Same DataVault ownership route; weak devices keep flat arrays, higher tiers keep larger capacity/cadence without cached physical aliases.
Hardware Impact: 0 GC; one helper call compiles down to native array index writes.

Problem: Second constrained build failed after Power-domain fixes.
Solution: Recorded compile wall as outside domain: `Assets/_Project/Scripts/PlayerInventory.cs(314,18)` syntax errors; build log is `Docs/Reports/BUILD_1319_Assembly-CSharp.log`.
Rejected Alternatives: Editing PlayerInventory from Power-domain agent; invalid boundary breach.
Scalability potential: Not runtime.
Hardware Impact: 0 us in Power runtime.

## Proof

Problem: Need machine-readable before/after proof.
Solution: Final report written to `Docs/Reports/VAULT_EXORCISM_REPORT_1319.json`; final audit `forbiddenPersistentCandidates=0`, `parseFailures=0`, hash `41772b71601a25122afa75931336fc01ca9f65775ec560dfb65e82a26d67d45e`.
Rejected Alternatives: Prose-only reporting.
Scalability potential: Not runtime.
Hardware Impact: 0 us runtime.

## APEX Re-Audit 2026-05-26

Problem: APEX review required a fresh prompt extraction and gate proof from disk, not prior chat claims.
Solution: Re-extracted Agent 1319 from `Docs/Tasks/CURRENT_BATCH.md`; root `C:\hades\current_batch.md` and `C:\hades\Hecton8\current_batch.md` remain absent. Task count verified as 20 by `Task 01` through `Task 20`.
Rejected Alternatives: Trusting compressed context or the previous report without re-reading the source prompt.
Scalability potential: Not runtime.
Hardware Impact: 0 us runtime impact on i3/MX350.

Problem: Static native-field proof needed to be re-run after lock hardening.
Solution: Ran `Tools/VaultNativeAliasRoslynAudit` on `Assets/_Project/Scripts/Power`; result `parseFailures=0`, `totalNativeFieldDeclarations=290`, `forbiddenPersistentCandidates=0`, `jobTransientFields=265`, `stackOnlyRefStructViewFields=25`.
Rejected Alternatives: Regex-only audit. Regex was used as a secondary classifier, not the authority artifact.
Scalability potential: Low/Middle/High/Ultra retain one DataVault descriptor route; no private native owner grows with quality.
Hardware Impact: 0 us runtime; prevents compaction-time crash class.

Problem: WFC grid leases still exposed a raw `NativeArray<byte>` read view without explicit buffer pin/release at the consumer boundary.
Solution: Added `BufferId`/`SystemId` to the `readonly ref struct` lease, locked the slot in `TryGetGrid`, and released it in `finally` from `ShinobuLogisticsRouter` and `WfcOutpostPowerBootRuntime`.
Rejected Alternatives: Keep the raw read lease because it is a ref struct. Ref struct prevents heap escape, but it does not prove vault compaction safety.
Scalability potential: Low keeps 4 fixed grid slots; Middle/High/Ultra can schedule denser WFC imports without adding persistent aliases.
Hardware Impact: 0 us saved; one lock/unlock pair per grid import removes a relocation race on i3/MX350.

Problem: Hot-path allocation gate had to distinguish forbidden GC from value-type initializers and cold dump I/O.
Solution: Ran `Tools/VoxelRuntimeHotPathAudit` over the 7 touched files; static hot-path string/LINQ/foreach/concat/native allocation counters are all zero. Remaining raw findings are value-type job/DTO construction or cold file dump objects.
Rejected Alternatives: Claiming 0 GC from review memory only.
Scalability potential: Weak devices avoid GC hitches; higher tiers spend saved headroom on visual feedback without changing truth ownership.
Hardware Impact: 0 B/frame by static gate; runtime profiler proof still requires Unity/GCMonitor.

Problem: Build re-run was requested, but project law forbids starting `dotnet build` while another dotnet/MSBuild node is running.
Solution: Did not launch a build. Current previous build log remains blocked by unrelated `Assets/_Project/Scripts/PlayerInventory.cs(314,18)` syntax errors; no Power-domain diagnostics were present after the earlier CS1612 fixes.
Rejected Alternatives: Editing `PlayerInventory.cs` from Power domain or starting a forbidden build.
Scalability potential: Not runtime.
Hardware Impact: 0 us runtime.

## APEX Re-Audit Rerun 2 2026-05-26

Problem: The previous lock proof still depended on outer preflight checks in several helper paths.
Solution: Added immediate compaction-fence guards inside `TryLockGraphBuffer`, `TryLockRouterBuffer`, `SubmarineOsThermalGridRuntime.TryLockBuffer`, `WfcOutpostPowerBootRuntime.TryLockRuntimeBuffer`, WFC registry direct lock branches, and direct tuning/black-box write-lock branches.
Rejected Alternatives: Relying on one `vault.IsCompactionFenceActive` check before a chain of many lock attempts; that leaves a fence-rise window between sequential buffer pins.
Scalability potential: Low/Middle/High/Ultra keep the same fixed buffer routes. High/Ultra can raise cadence/visual signal density without widening the memory-relocation race.
Hardware Impact: 0 us saved on i3/MX350; removes a compaction correctness race under topology rebuild pressure.

Problem: The root prompt path requested by the review is absent.
Solution: Re-extracted Agent 1319 from `Docs/Tasks/CURRENT_BATCH.md` using a tag-attribute-aware regex; verified task count is 20.
Rejected Alternatives: Trusting earlier `Status_1319.md` or requiring a missing root file.
Scalability potential: Not runtime.
Hardware Impact: 0 us runtime.

Problem: Proof artifacts had to be refreshed after the lock patch.
Solution: Re-ran `VaultNativeAliasRoslynAudit` and `VoxelRuntimeHotPathAudit`; wrote `APEX_NATIVE_FIELD_AUDIT_1319_RERUN4.json`, `APEX_HOTPATH_AUDIT_1319_RERUN4_RAW.json`, and updated `APEX_PURGE_REPORT_1319.json`.
Rejected Alternatives: Keeping the prior `AFTER_LOCKS` artifacts after code changed.
Scalability potential: Not runtime.
Hardware Impact: 0 us runtime.

## APEX Re-Audit Rerun 3 2026-05-26

Problem: The rejection demanded another disk-backed prompt extraction and proof run, and the previous task-count regex was vulnerable to repeated task references.
Solution: Re-extracted from Docs/Tasks/CURRENT_BATCH.md, counted unique Task NN: definitions only, and verified task count 20. Root current_batch.md paths remain absent.
Rejected Alternatives: Trusting chat context or a broad Task\s+\d{2} count; broad count returned 23 because the prompt repeats task references outside definitions.
Scalability potential: Not runtime.
Hardware Impact: 0 us runtime on i3/MX350.

Problem: Need fresh proof that touched files still satisfy native sovereignty and zero-GC gates after the last compaction-lock patch.
Solution: Re-ran Roslyn native audit RERUN5, hot-path audit RERUN5, and a per-line touched-file audit over 12,324 lines. Persistent native candidates remain 0; hot-path forbidden string/LINQ/foreach/native allocation counters remain 0.
Rejected Alternatives: Reusing RERUN4 artifacts after a new rejection.
Scalability potential: Low/Middle/High/Ultra retain DataVault handles and transient views; no quality tier changes ownership or DTO layout.
Hardware Impact: 0 B/frame by static gate; runtime profiler proof still requires Unity Play Mode/GCMonitor.

Problem: Compile proof was previously deferred by active dotnet/csc process rules.
Solution: Shut down stale Roslyn build server cache, verified CPU under 50 and no dotnet/csc/VBCSCompiler, then ran dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1. Build exits 1 from non-Power domains; BUILD_1319_APEX_RERUN5.log contains zero Assets/_Project/Scripts/Power diagnostics.
Rejected Alternatives: Editing SubmarineAtmosphereSystem, PDA, World, Audio, Inventory, or Fluid from Agent 1319's Power domain.
Scalability potential: Not runtime.
Hardware Impact: 0 us in Power runtime.

## APEX Re-Audit Rerun 4 2026-05-26

Problem: The rejection demanded another proof pass after RERUN5, with the prompt re-extracted from disk and local scanners re-run.
Solution: Re-read status/rationale/AGENTS/domain/mandates, extracted `<AGENT_PROMPT id="1319"...>` from `Docs/Tasks/current_batch.md`, and verified 20 unique task definitions. Root current_batch.md paths remain absent.
Rejected Alternatives: Reusing prior extracted XML or task count from chat state.
Scalability potential: Not runtime.
Hardware Impact: 0 us runtime on i3/MX350.

Problem: Need syntax-tree proof that no persistent native collection fields regressed in Power.
Solution: Re-ran `VaultNativeAliasRoslynAudit` as RERUN6. Result: 22 Power files, parseFailures=0, totalNativeFieldDeclarations=290, forbiddenPersistentCandidates=0, jobTransientFields=265, stackOnlyRefStructViewFields=25.
Rejected Alternatives: Regex-only classification; regex was kept as a secondary line audit only.
Scalability potential: Low/Middle/High/Ultra retain DataVault handles and phase-local views; quality weight never changes ownership.
Hardware Impact: 0 us runtime; avoids compaction relocation crash class.

Problem: Need fresh zero-GC hot-path proof on all touched files.
Solution: Re-ran `VoxelRuntimeHotPathAudit` and a per-line audit over 12,324 lines. Forbidden counters for string formatting, `.ToString()`, LINQ, foreach, string concat suspects, TempJob allocation, Persistent allocation, and `throw new` are zero. Remaining exception catches are cold dump/import guards.
Rejected Alternatives: Treating value-type `new` DTO/job initializers as managed allocations. The scanner separates value types from managed-risk creations.
Scalability potential: Weak hardware keeps 0 B/frame static path; higher tiers can spend headroom on presentation without changing truth data.
Hardware Impact: 0 B/frame by static gate; runtime GCMonitor proof still requires Unity Play Mode.

Problem: Compile verification could not be launched while CPU was >50 or dotnet/csc was active.
Solution: Waited through active external builds, then ran `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` when CPU was 48 and compiler processes were absent. Build still exits 1, but `BUILD_1319_APEX_RERUN6.log` contains zero Power diagnostics; failures are PDA, World vegetation, and SubmarineAtmosphere outside Agent 1319 domain.
Rejected Alternatives: Launching a second compiler during active csc/dotnet, or editing non-Power domains.
Scalability potential: Not runtime.
Hardware Impact: 0 us in Power runtime.

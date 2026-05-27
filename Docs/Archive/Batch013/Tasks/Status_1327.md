# Status 1327 - MEMORY_SOVEREIGN_FLORA_INTERACTION_EXORCIST

Status: STATIC VERIFIED GREEN FOR 1327 GATES - `Assembly-CSharp.csproj` build was attempted after the latest private-padding fix; it failed in external systems with zero visible `FloraInteractionManager.cs` / validator errors.
Domain: Assets/_Project/Scripts/World/FloraInteractionManager.cs first; broader Assets/_Project/Scripts/World flora scope only after conflict scan.
Task count: 20

Relevant mandates read before coding:
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- DATA_Runtime_Struct_Layout_ARM64.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- ARCH_Execution_Phases.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- ARCH_Signal_Lane_Segregation.txt
- REND_Instanced_Flora_Physics.txt

## Loop 0 - Intake
- [x] Extracted prompt 1327 from Docs/Tasks/CURRENT_BATCH.md | DOD practice: CLI raw extraction with regex, not truncated reader | Rejected alternative: relying on chat/user summary | Estimate: 150 us
- [x] Read AGENTS.md and domain map | DOD practice: authority spine before source mutation | Rejected alternative: coding from batch prompt alone | Estimate: 500 us
- [x] Selected and read 8 relevant registry mandates | DOD practice: task-specific mandate minimum before coding | Rejected alternative: broad registry scan without applying rules | Estimate: 900 us

## Loop 1 - Tasks 01-05
- [x] Task 01 EXHAUSTIVE_PRIMARY_TARGET_INQUISITION | DOD practice: Roslyn AST direct-field scan, source ledger written to Docs/Reports/VAULT_EXORCISM_LEDGER_1327_BEFORE.json | Rejected alternative: regex-only hit list that would include Burst job fields | Estimate: 650 us
- [x] Task 02 OWNERSHIP_PROVENANCE_AND_LIFECYCLE_MAPPING | DOD practice: usage grep grouped aliases into ocean scratch, parasite nodes, template masks, cascade buffers, and spatial query staging | Rejected alternative: one generic buffer bucket for unrelated lifecycles | Estimate: 900 us
- [x] Task 03 DEPENDENCY_GRAPH_IMPACT_ANALYSIS | DOD practice: external reads identified as `TryResolveKelpPushback`, `TryGetCulledFloraVisibleBuffer`, tool/parasite internal methods; transition uses existing `IDataVault` cache and owner-local helper calls | Rejected alternative: new public API or new SignalBus lane for private callers | Estimate: 850 us
- [x] Task 04 DTO_LAYOUT_EXTRACTION_AND_VERIFICATION | DOD practice: verified active DTOs already explicit for `ParasiteNode`, `FloraCascadeEventPayload`, `FloraDisplacementDTO`, `FloraStiffnessRuleDTO`, `FloraSwayFieldTelemetryEntry`; `ModuleParasiteState` remains managed dictionary value outside NativeArray scope | Rejected alternative: editing unrelated managed-only structs | Estimate: 700 us
- [x] Task 05 TELEMETRY_RING_INTEGRATION_PLANNING | DOD practice: reused existing 64-byte `FloraSwayFieldTelemetryEntry` and 300-frame Vault blackbox route for flora sway; native alias failures map to existing Vault-missing/NaN flags plus final report ledger | Rejected alternative: adding a second telemetry lane before source proves a runtime gap | Estimate: 650 us

## Loop 2 - Tasks 06-10
- [x] Task 06 VAULT_DESCRIPTOR_SUBSTITUTION | DOD practice: deleted the 14 persistent native collection fields and replaced them with 16-byte `VaultGenerationHandle<T>` descriptors plus scalar counts for former `NativeList<int>` lanes | Rejected alternative: wrapper object retaining native aliases | Estimate: 700 us
- [x] Task 07 COLD_BOOT_BUFFER_REGISTRATION | DOD practice: added `ResolveFloraAuxiliaryVaultBuffers` in cold enable/awake lanes using `NativeArrayOptions.UninitializedMemory` except explicit clear boot | Rejected alternative: lazy hot-path growth at first query | Estimate: 900 us
- [x] Task 08 PHASE_LOCAL_VIEW_RESOLUTION | DOD practice: converted ocean flow, parasite nodes, cascade masks/events/seeds, and reactive spatial query lanes to local `TryResolve`/`TryAcquireWriteLock` views | Rejected alternative: passing generation handles into jobs | Estimate: 1200 us
- [x] Task 09 IRONCLAD_TRY_FINALLY_LOCKING | DOD practice: every new writer acquisition has a paired release helper in `finally` or an explicit fail-closed release branch | Rejected alternative: multi-frame Vault writer locks | Estimate: release path <1 us, contention skip path unmeasured
- [x] Task 10 BURST_JOB_SIGNATURE_RECONCILIATION | DOD practice: existing Burst jobs still receive `NativeArray<T>` views resolved at schedule time; no job stores Vault handles | Rejected alternative: kernel-side Vault lookup | Estimate: 0 runtime us added to job body

## Loop 3 - Tasks 11-15
- [x] Task 11 READ_ACCESSOR_PURIFICATION | DOD practice: public `TryResolveKelpPushback` now treats the query handle buffer as a write-scratch lane with `TryAcquireWriteLock` and `finally` release; no `GlobalRegistry` polling or allocation added | Rejected alternative: pure read facade over a mutating spatial query scratch buffer | Estimate: contention branch <2 us unmeasured
- [x] Task 12 EXPLICIT_DTO_REFACTORING | DOD practice: added 64-byte explicit `FloraMemoryTelemetryEntry`; existing native DTOs remain explicit and 8-byte aligned | Rejected alternative: extending existing sway telemetry with overloaded semantics | Estimate: 0 hot-path us except failure telemetry
- [x] Task 13 SCALABILITY_WEIGHT_PRESERVATION | DOD practice: preserved existing continuous `HomeostasisBrain.GlobalQualityWeight` math and recorded it in memory telemetry; introduced no binary quality switch | Rejected alternative: raising/lowering fixed capacities by device class | Estimate: 0 us
- [x] Task 14 TELEMETRY_RING_IMPLEMENTATION | DOD practice: allocated Vault-backed `FloraMemoryTelemetryEntry[300]` ring at BufferID 71669 and writes resolution/lock/NaN failures without managed work on normal success paths | Rejected alternative: managed log strings in frame failure branches | Estimate: failure path only, unmeasured
- [x] Task 15 BLACKBOX_DUMP_ROUTING | DOD practice: added `Docs/AgentLogs/Dump_1327_FloraInteraction.bin` export after NaN or 3 hard memory failures; background file write uses copied crash payload, not a live NativeArray crossing threads | Rejected alternative: background thread reading Vault memory directly after phase end | Estimate: catastrophic path only

## Loop 4 - Tasks 16-20
- [x] Task 16 BROAD_DOMAIN_CONFLICT_CHECK | DOD practice: `git status --short -- Assets/_Project/Scripts/World` found active sibling edits and agent 1316 ownership of vegetation memory pool; broad native-field scan recorded residuals | Rejected alternative: folder-wide rewrite during active csc/dotnet work | Estimate: 300 us
- [x] Task 17 UNCONTESTED_FILE_EXORCISM | DOD practice: no sibling production exorcism performed beyond primary target; `FloraRegrowthDirector.cs` residuals documented as outside propwash/sway interaction lifecycle without a safe BufferID/lifecycle ledger | Rejected alternative: blind migration of flora lifecycle/regrowth containers under another ecosystem domain | Estimate: 0 us changed
- [x] Task 18 ARM64_ALIGNMENT_VALIDATOR_INTEGRATION | DOD practice: added `FloraInteractionMemorySovereigntyValidator1327.cs` and public layout guards for displacement, memory telemetry, parasite node, cascade payload, sway telemetry, and consumed wake telemetry DTOs | Rejected alternative: report-only layout proof | Estimate: editor-load only
- [x] Task 19 ZERO_GC_HOT_PATH_VERIFICATION | DOD practice: grep confirms no persistent native collection fields, no new `NativeArray` allocations, and no allocator constants in primary target; managed dump copy exists only on catastrophic path | Rejected alternative: claiming runtime profiler proof without running Unity | Estimate: measured proof absent
- [x] Task 20 AUTOMATED_METRIC_VALIDATOR_REPORT | DOD practice: wrote `Docs/Reports/VAULT_EXORCISM_REPORT_1327.json` with before/after counts, BufferIDs, SHA-256 hashes, and conflict notes | Rejected alternative: chat-only report | Estimate: 250 us static scan

Verification:
- Compile: ATTEMPTED AND FAILED OUTSIDE 1327 SCOPE. `dotnet build .\Assembly-CSharp.csproj --no-restore` reached `Hecton8.Core.csproj` and reported 233 project errors, with visible failures in `SubmarineAtmosphereSystem.cs`, `VegetationMemoryPool.cs`, inventory routing/query code, `HectonFluidEngine.cs`, and audio renderers. No visible error line in the terminal output referenced `FloraInteractionManager.cs` or `FloraInteractionMemorySovereigntyValidator1327.cs`; full capture-log rerun was blocked because seven `dotnet` build-server processes remained active.
- Static native field scan: PASS for touched C# files after T.A.R.S. re-audit. Roslyn subset: 14 native field declarations, 14 transient job parameters, 0 persistent fields.
- Hot-path GC scan: PASS for `Tick`, `SlowTick`, `LateFrameTick`, and all six job `Execute` bodies: 0 managed allocation/string/LINQ/foreach hits in those bodies. Broad scanner still reports cold/editor/fatal object creation outside hot paths.
- AUP scan: PASS. Direct absolute AUP float casts removed; submarine wash route and force jobs use `ResolveAupLocalDelta` double subtraction, clamp, then cast.
- Inner-loop branch audit: PASS for job loops under line 700. Loop starts 265, 473, 635 have 0 `if`, 0 `continue`, 0 ternary hits, 0 prohibited allocation/text hits.
- Compaction-aware locks: PASS. `TryEnsureFloraVaultBuffer`, `TryResolveFloraVaultBuffer`, `TryAcquireFloraVaultWriteBuffer`, and telemetry acquisition all check `IsCompactionFenceActive`; write locks release on fail branches and in caller `finally`.
- Telemetry ring: PASS. `FloraMemoryTelemetryEntry[300]` Vault buffer, numeric failure flags, and crash dump route exist.

## T.A.R.S. Re-audit - 2026-05-26
- [x] Re-extracted `<AGENT_PROMPT id="1327">` from `Docs/Tasks/CURRENT_BATCH.md` | DOD practice: CLI raw regex extraction | Rejected alternative: relying on prior context | Estimate: 150 us
- [x] Re-ran Roslyn native alias audit | DOD practice: syntax-tree field classification filtered to touched C# files | Rejected alternative: broad World false-green claim | Estimate: 1200 us
- [x] Re-ran zero-GC hot path scanner | DOD practice: Roslyn parse plus method-body regex on Tick/SlowTick/LateFrameTick/job Execute | Rejected alternative: raw all-file grep as proof | Estimate: 900 us
- [x] Fixed AUP direct absolute cast defect in submarine wash globals | DOD practice: double origin subtraction through `ResolveAupLocalDelta` before float shader payload | Rejected alternative: leaving unused AUP globals dirty | Estimate: jitter prevention, not measured
- [x] Tightened `ResolveAupLocalDelta` | DOD practice: double subtraction, double clamp, then float cast | Rejected alternative: cast-then-finite-check | Estimate: 0.2 us/job sample unmeasured
- [x] Removed branchy inner-loop culls in flora cascade and sway jobs | DOD practice: mask/select math and continuous `GlobalQualityWeight` solver fake | Rejected alternative: CPU-realistic solver branch maze | Estimate: 3-12 us on dense low-end flora frames, static estimate
- [x] Rechecked compaction fence after write-lock acquisition | DOD practice: post-acquire fail-closed release | Rejected alternative: pre-check only | Estimate: stall prevention, not measured
- [x] Re-audited every touched write-lock caller | DOD practice: direct line-range inspection of all `TryAcquire*`/`Release*` pairs in `FloraInteractionManager.cs` | Rejected alternative: trusting helper-level proof only | Estimate: 900 us
- [x] Fixed pre-mask NaN propagation in wake source accumulation | DOD practice: sanitize source local/radius/intensity/velocity before multiplication and mask application | Rejected alternative: multiplying invalid values by zero masks | Estimate: correctness defect; CPU delta unmeasured
- [x] Re-ran final scanners and report hash | DOD practice: Roslyn scans plus direct regex AUP/alloc/string sweep and SHA-256 over touched C# files | Rejected alternative: stale report hashes from previous pass | Estimate: 1500 us
- [x] Documented external World parse failure without claiming ownership | DOD practice: scoped touched-file proof while naming `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:1254` CS1519 as outside 1327 files | Rejected alternative: false broad green over all World files | Estimate: 300 us

## T.A.R.S. Re-audit 2 - 2026-05-26
- [x] Re-extracted `<AGENT_PROMPT id="1327">` again from `Docs/Tasks/CURRENT_BATCH.md` | DOD practice: raw CLI regex extraction, 22,915 chars, SHA-256 `adbf73df8eb4432da3489a5f88130b053cfcd8381038ea99194f0b48699313aa` | Rejected alternative: relying on previous report | Estimate: 150 us
- [x] Removed hot write-acquire allocation path | DOD practice: `TryAcquireFloraVaultWriteBuffer` and telemetry acquisition are resolve-only; `EnsureGenerationHandle` remains in cold ensure helpers | Rejected alternative: acquire helper silently growing Vault buffers | Estimate: prevents frame spike; microseconds unmeasured
- [x] Preallocated cascade phase seed Vault/GPU buffers in cold setup | DOD practice: fixed `CascadePhaseSeedCapacity` and no Vault/GraphicsBuffer release in visual channel release | Rejected alternative: first cascade refresh allocating in Tick/LateFrame | Estimate: prevents first-touch allocation spike
- [x] Fixed `PopulateCascadePhaseSeedsJob` out-of-bounds fail path | DOD practice: return before writing if `PhaseSeeds` is not created or `index >= Length` | Rejected alternative: writing `PhaseSeeds[index]` inside invalid-buffer branch | Estimate: correctness defect; CPU delta negligible
- [x] Moved template-mask rebuilds out of hot refresh wrappers | DOD practice: force rebuilds call cold helper from setup; Tick/SlowTick refresh methods are read-only/no-grow | Rejected alternative: hidden `new bool[]`, `new int[]`, and Vault ensure under `force:false` mismatch | Estimate: prevents rare SlowTick/Tick allocation spike
- [x] Moved reactive spatial hash creation to cold setup | DOD practice: hot rebuild passes `allowCreate:false`; cold setup creates hashes | Rejected alternative: `new HectonSpatialHash` on first spatial refresh | Estimate: prevents first-refresh managed/native allocation
- [x] Re-ran Roslyn native alias audit | DOD practice: syntax-tree field classification over World with touched-file subset | Rejected alternative: regex-only field count | Estimate: 1200 us
- [x] Re-ran hot-path allocation scanner | DOD practice: Roslyn hotpath scan plus method-bound scanner for Tick/SlowTick/LateFrameTick and called hot wrappers | Rejected alternative: all-file object creation count as hot proof | Estimate: 1200 us

## T.A.R.S. Re-audit 3 - 2026-05-26
- [x] Re-extracted `<AGENT_PROMPT id="1327">` with attribute-tolerant CLI regex | DOD practice: exact tag block extraction from `Docs/Tasks/CURRENT_BATCH.md`, 22,915 chars, 20 tasks, SHA-256 `adbf73df8eb4432da3489a5f88130b053cfcd8381038ea99194f0b48699313aa` | Rejected alternative: stale memory or exact-close-tag regex that fails on `role/chat_name` attrs | Estimate: 150 us
- [x] Re-read 4 mandate files | DOD practice: native memory/jobs, zero-GC, ARM64 layout, and AUP determinism reread before verification | Rejected alternative: relying on prior mandate summary | Estimate: 900 us
- [x] Added missing `FloraStiffnessRuleDTO` layout validator coverage | DOD practice: editor guard now checks size 16, PlantHash offset 0, Flags offset 12 | Rejected alternative: report-only byte offset map without executable validator proof | Estimate: editor-only
- [x] Re-ran native alias Roslyn audit | DOD practice: current disk scan over World with touched-file subset | Rejected alternative: stale `VAULT_NATIVE_ALIAS_LEDGER_1327_REAUDIT_WORLD.json` | Estimate: 1200 us
- [x] Re-ran hot-path Roslyn audit and method-bound scanner | DOD practice: current disk scan after validator edit; hot methods have 0 prohibited hits | Rejected alternative: accepting previous hotpath hash | Estimate: 1200 us
- [x] Re-ran AUP direct absolute cast and job-loop branch scans | DOD practice: direct regex plus loop-body extraction | Rejected alternative: prose-only AUP/SIMD proof | Estimate: 800 us
- [x] Build retry guard checked | DOD practice: CPU/process check before compile | Rejected alternative: launching build while CPU 58% with active `csc` and `dotnet` | Estimate: 200 us

## T.A.R.S. Re-audit 4 - 2026-05-26
- [x] Retried build when guard cleared | DOD practice: CPU 22%, no active `dotnet/csc/VBCSCompiler`, one-process `dotnet build .\Assembly-CSharp.csproj --no-restore` | Rejected alternative: preserving a stale blocked-build statement | Estimate: 98.94 s wall
- [x] Fixed 1327 compile regression | DOD practice: declared cold Vault write views as `out NativeArray<byte>` locals in cascade and defensive spore mask rebuilds | Rejected alternative: restoring persistent native fields or hiding under stale build report | Estimate: compile blocker removal, runtime 0 us
- [x] Rebuilt after fix | DOD practice: second guarded build reached 197 external errors and no visible 1327 target errors | Rejected alternative: claiming project compile green while vegetation/atmosphere/inventory/fluid/PDA/audio remain broken | Estimate: 98.94 s wall
- [x] Re-ran native alias and hot-path scanners after fix | DOD practice: current disk scan confirms 14 touched native declarations, 14 job-transient, 0 persistent, 0 hot-path prohibited hits | Rejected alternative: using pre-fix hashes | Estimate: 1700 us
- [x] Recomputed source hashes and report | DOD practice: updated `VAULT_EXORCISM_REPORT_1327.json` with new FloraInteractionManager SHA-256 and combined touched C# hash | Rejected alternative: leaving stale verification hash after source mutation | Estimate: 250 us
- [x] Tightened `FloraSwayFieldTelemetryEntry` field order | DOD practice: moved 2-byte fields to the 60/62 tail and added executable offset validation | Rejected alternative: accepting size-only proof for a DTO that did not follow 4-byte-before-2-byte ordering | Estimate: runtime 0 us
- [x] Re-ran static scans after DTO layout fix | DOD practice: native audit unchanged; hotpath audit hash updated to `9c372a8f1e7e8e0454ce8459d8f343bc0755f1fe57b486a69d8ae1196ad37cd1`; hot method/AUP/loop scanners 0 hits | Rejected alternative: stale pre-layout-fix report | Estimate: 1700 us
- [x] Rebuild after DTO layout fix | DOD practice: waited until guard cleared, launched one-process build, captured timeout/external failure output | Rejected alternative: claiming compile green or launching while `csc/dotnet` were active | Estimate: 121.49 s wall
- [x] Re-extracted prompt 1327 after rejection | DOD practice: CLI attribute-tolerant XML extraction from `Docs/Tasks/CURRENT_BATCH.md`, 22,915 chars, SHA-256 `adbf73df8eb4432da3489a5f88130b053cfcd8381038ea99194f0b48699313aa`, 20 explicit tasks | Rejected alternative: trusting prior final answer | Estimate: 150 us
- [x] Re-read mandates after rejection | DOD practice: native memory/jobs, zero-GC, ARM64 layout, AUP, cinematic cheat, telemetry mandates | Rejected alternative: relying on previous summary | Estimate: 1200 us
- [x] Replaced public padding fields in touched runtime structs | DOD practice: `ParasiteNode`, `FloraCascadeEventPayload`, and `DefensiveSporeBurstState` now use private `_pad*` fields; cascade initializer no longer writes padding | Rejected alternative: claiming layout perfection while exposing public `Padding*` fields | Estimate: runtime 0 us
- [x] Re-ran scanners after private-padding fix | DOD practice: native audit 14 job-transient / 0 persistent; hot audit hash `63493df7978b98c76a13c8befdb6989ef240697fb805cc006ce8fda34c7e9706`; method/AUP/loop scans 0 hits | Rejected alternative: stale hashes | Estimate: 1700 us
- [x] Rebuild after private-padding fix | DOD practice: CPU/process guard cleared, shared compilation disabled, build captured complete external failure set | Rejected alternative: 120s timeout proof or project-green claim | Estimate: 95.61 s wall, 69 external errors, 0 visible 1327 errors

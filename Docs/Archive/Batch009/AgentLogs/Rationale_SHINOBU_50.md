# Rationale_SHINOBU_50

Status: BABEL_LANE_REVERIFIED / BABEL_CONTRACT_EXTRACTED_TO_CORE_CONTRACTS / CORE_BUILD_RECHECK_PENDING_CPU_BUSY / GLOBAL_BINARY_HYGIENE_HAS_NON_BABEL_FAILURES

## Decision 000 - Scope Lock
Problem: The batch prompt mixes integration language with a concrete Babel binary alignment mandate. Direct runtime-domain references would create dependency pressure and compile-wall risk.
Solution: Scope SHINOBU_50 to the Babel dictionary memory surface, aligned DTOs, mocked request/output structs, typed signal payloads, and GlobalRegistry/DataVault-facing interfaces only.
Rejected Alternatives: A concrete bridge into Terminal OS, Anomaly Director, or Audio systems was rejected because the prompt explicitly says temporal blindness and AGENTS.md forbids direct cross-domain concrete references.
Scalability potential: Low uses capped per-frame lookup budget; Middle drains more requests; High/Ultra can spend saved CPU on richer decrypted lore preview and diagnostics without changing gameplay truth.
Hardware Impact: Expected i3/MX350 gain comes from replacing managed dictionary/string access with contiguous binary search over 16-byte records; estimate pending code audit.

## Decision 001 - Mandate Selection
Problem: The task touches binary layout, zero-GC localization, signal boundaries, service discovery, and post-mortem dumps.
Solution: Loaded DATA_Runtime_Struct_Layout_ARM64, UI_Localization_Babel_RTL_FontSwap_ZeroAlloc, STRM_ModuleDTO_LZ4_Dictionary, ARCH_Global_Registry_ServiceLocator_DI_Init, ARCH_Signal_Lane_Segregation, DBG_Telemetry_Crash_Reporting_PostMortem, and OPT_Zero_GC_Policy_AllocFree_Mandate.
Rejected Alternatives: Physics/AUP mandates were rejected because Task 13 explicitly excludes AUP from dictionary queries.
Scalability potential: The selected mandates cover Low/Middle/High/Ultra lookup throttling and diagnostics without domain creep.
Hardware Impact: Prevents ARM64 unaligned access traps and avoids managed allocations on mobile and MX350-class CPUs.

## Decision 002 - 1295 Byte Repair
Problem: `Data/Balance/Baked/Babel_Dictionary.h8bin` was 1295 bytes, not divisible by 16. Header length and CRC matched the bad size, so appending a byte without contract updates would corrupt validation.
Solution: Fixed `H8DataBaker.WriteBabelDictionary()` to align final `totalBytes`, repaired the current payload to 1296 bytes, recomputed payload CRC `0x199CAC7A`, and updated `H8StaticData.bin` Babel CRC.
Rejected Alternatives: Raw hand-padding without header/CRC repair was rejected because the binary ledger says the baker owns alignment semantics.
Scalability potential: Low/Middle/High/Ultra all read the same stable aligned payload; larger future dictionaries can still use the MMF path.
Hardware Impact: Removes 1-byte tail misalignment risk on ARM64. i3/MX350 gain is not frame-time measurable for 26 records but prevents trap-class failure.

## Decision 003 - Flat Babel Index
Problem: `BabelDictionaryStore` built a `NativeParallelHashMap<uint,long>` lookup, adding persistent native ownership and rejecting the prompt's flat-array/binary-search contract.
Solution: Replaced the lookup map with direct binary search over sorted 16-byte `BabelIndexDTO` rows and a monolithic UTF-8 blob pointer. Added `BabelBinarySearchKernel` for batched Burst lookups.
Rejected Alternatives: Keeping the hash map as "fast enough" was rejected because it adds allocation/state and hides offset/length truth behind a second index.
Scalability potential: Low processes 20 requests per frame through `BabelLookupScalability`; Middle ramps smoothly; High/Ultra can drain full request counts and spend saved cycles on richer diagnostics.
Hardware Impact: O(log N) over contiguous 16-byte rows is cache-predictable. Estimated low-end gain: 2-8 us per 500-entry UI burst versus hash-map hydration, unprofiled.

## Decision 004 - DTO Layout
Problem: Babel and static-data DTOs used `[StructLayout(Pack=1)]`; `ContentLoreBlockIndex` placed a `long` at offset 4.
Solution: Removed `Pack=1` from the touched DTOs, introduced `BabelIndexDTO` layout `uint,uint,uint,uint`, and reordered `ContentLoreBlockIndex` to put `long Offset` first.
Rejected Alternatives: Keeping `Pack=1` with comments was rejected because it is the exact ARM64 failure mode under audit.
Scalability potential: Same data works on weak devices and overkill desktop without per-platform forks.
Hardware Impact: 16-byte Babel rows and aligned lore index prevent split cache-line reads; estimated gain is correctness-first, with sub-microsecond lookup stability.

## Decision 005 - Fallback, Lore, And Signals
Problem: Missing hashes, corrupted lore, and voice links needed proof without direct UI/audio/runtime dependencies.
Solution: Added Vault-backed unmanaged `ERROR` fallback bytes, `BabelLoreXorDecryptJob`, `MockTextRequestSignal`, `MockUIBuffer`, `MockSpanCountJob`, and `PlayVoiceOverSignal` through typed `SignalBus`.
Rejected Alternatives: Returning null/empty spans or directly calling audio was rejected because it either hides authoring errors or violates domain isolation.
Scalability potential: Low can skip expensive text hydration and still display `ERROR`; High/Ultra can run XOR previews and voice synchronization.
Hardware Impact: Missing-hash handling is branch-only after cold Vault buffer acquisition. Voice link is a typed signal push, not a concrete audio call.

## Decision 006 - Verification Boundary
Problem: An earlier `dotnet build Hecton8.Core.csproj` attempt was blocked by `PlayerBuilder.cs` construction DTO and sampler dependencies outside the Babel domain during concurrent agent work.
Solution: Saved the failed build log, refused cross-domain edits, then re-ran compile after the codebase moved; current final compile evidence is Decision 009.
Rejected Alternatives: Editing `PlayerBuilder`, Habitat, or Construction mocks was rejected as cross-domain sabotage for this agent.
Scalability potential: Compile-wall isolation keeps Babel changes from dragging construction/player dependencies into the localization lane.
Hardware Impact: No runtime gain; protects iteration time by refusing unrelated dependency surgery.

## Decision 007 - Pointer Index And Burst Jobs
Problem: The first runtime pass still carried a private persistent `NativeArray<BabelIndexDTO>` copy and Burst attributes weaker than the batch mandate.
Solution: Removed the private native index allocation, binary-searches the mapped/padded index pointer directly, and added exact Burst flags plus NoAlias on search, endianness, XOR lore, and mock span jobs.
Rejected Alternatives: Keeping the copied index was rejected because the Ultra mandate forbids local persistent native arrays when mapped bytes already contain a valid flat index.
Scalability potential: Low/Middle/High/Ultra all share the same pointer truth; weak devices throttle request count through `GlobalQualityWeight`, desktop drains full batches.
Hardware Impact: Avoids one native allocation and one index copy at boot; on i3/MX350 this is cold-path only, but it removes fragmentation and keeps lookup rows L1-predictable.

## Decision 008 - CSV Override Boundary
Problem: Task 19 asks for live Vault byte mutation from `loc_overrides.csv` without rebaking, and the first pass only handled equal-or-shorter replacements.
Solution: `LocRegistry.TryApplyLocOverridesCsv` now reads CSV into a 1 MiB Vault scratch buffer, parses hashes/keys without managed per-line strings, mutates equal/shorter replacements in-place, appends longer replacements at `AlignUp16(_utf8ByteLength)`, and updates both `ByteOffset` and `ByteLength` under `BabelOverrideMutationGuardMask`.
Rejected Alternatives: Managed `Dictionary<uint,string>` and rebake-only typo fixes were rejected by the prompt. Shifting all neighboring slices was rejected because append+index-update is O(1) for the text blob and avoids rewriting unrelated rows.
Scalability potential: Low/Middle/High/Ultra keep the same lookup hot path; editor/dev authoring can patch long strings live, while high-tier presentation can spend saved CPU on decrypted previews and richer UI diagnostics.
Hardware Impact: 0 us/frame outside the 0.5 s dev/editor poll. Ingest cost is O(file bytes + overrides * log N), bounded by a 1 MiB scratch buffer.

## Decision 009 - Final Compile Reality
Problem: Earlier build attempts during concurrent agent work exposed external construction/player compile-wall noise; the codebase changed underneath the lane while SHINOBU_50 was active.
Solution: Re-ran `dotnet build Hecton8.Core.csproj`, `Hecton8.Editor.csproj`, and `Hecton8.PlayModeTests.csproj` with serial no-restore/no-analyzer settings after the final Babel patch. Current result is build success with 0 errors across all three.
Rejected Alternatives: Reporting stale failure state was rejected after fresh build evidence contradicted it.
Scalability potential: Confirms Babel changes do not widen assembly dependencies or force sibling runtime references.
Hardware Impact: No runtime gain; protects integration by proving the Core assembly compiles with the Babel lane changes.

## Decision 010 - Generated Project Linkage
Problem: `Hecton8.PlayModeTests.csproj` referenced `BabelDictionaryStore`, but the generated Core project did not include the source file; adding it exposed another stale UI DTO compile include.
Solution: Added the missing Core compile includes for `BabelDictionaryStore.cs` and existing `WristHologramHudRuntime.cs` DTO ownership. No runtime code dependency was invented; the project file now matches Unity's assembly surface.
Rejected Alternatives: Moving `BabelDictionaryStore` or duplicating `WristHudQuadTransformDTO` was rejected because it would create real code duplication and cross-domain drift.
Scalability potential: Build tooling now sees the same contract surface Unity sees; Low/Middle/High/Ultra behavior is unchanged.
Hardware Impact: 0 runtime impact; compile wall removed for PlayMode test builds.

## Decision 011 - Ultra Vault And Burst Hardening
Problem: The post-user-mandate audit found remaining weak Burst attributes in `LocRegistry` jobs, a branch-based quality cap, a 48-byte Babel telemetry row, and fallback memory paths that could still own local persistent NativeArray/raw memory when the Vault was unavailable.
Solution: Converted all remaining Babel jobs to `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]`, added `[NoAlias]`, changed lookup budgeting to a saturated polynomial `math.lerp` curve, padded `BabelTelemetryEntry` to 64 bytes, and routed Babel buffers through `BufferID.BabelErrorUtf8` / `BabelDictionaryMappedBytes` / existing Babel Vault handles.
Rejected Alternatives: Keeping cold fallback `new NativeArray` or `H8Memory.AllocateRaw` paths was rejected because the Ultra mandate requires DataVault ownership. A binary `qualityWeight < 0.5f` branch was rejected because the project scalability law requires a continuum.
Scalability potential: Low/Thermal remains capped to 20 lookups per frame; Middle ramps through a smooth polynomial; High/Ultra drains all requested entries and can spend saved CPU on richer presentation/decryption.
Hardware Impact: Runtime hot-path allocation remains 0 B/frame. 64-byte telemetry rows avoid false-sharing class stalls if telemetry writes become parallelized. Exact measured frame gain is not claimed without Unity profiler.

## Decision 012 - External Economy Compile Wall Boundary
Problem: The prior vault hardening pass recorded a temporary Core compile wall in untracked Economy code. After concurrent codebase movement, stale blocked status would now be false.
Solution: Re-ran `dotnet build Hecton8.Core.csproj`, `Hecton8.Editor.csproj`, and `Hecton8.PlayModeTests.csproj` after the CSV append hardening. All three now pass with 0 errors and 0 warnings under the serial no-restore/no-analyzer command set.
Rejected Alternatives: Keeping the stale blocked report was rejected because fresh compiler evidence supersedes old logs. Editing unrelated Economy code remains rejected unless a separate Economy owner asks for it.
Scalability potential: Confirms the Babel lane no longer contributes to compile-wall failure and did not require new sibling-runtime assembly references.
Hardware Impact: No runtime gain; protects iteration time by replacing a stale wall note with current compile evidence.

## Decision 013 - SHINOBU_50 Final Static Guard
Problem: The ultra mandate required proof that Babel did not add direct domain links, weak Burst attributes, local native ownership, or hidden runtime string dictionaries.
Solution: Re-ran hot-file scans over `LocRegistry`, `BabelDictionaryStore`, `H8StaticDataContracts`, and `BabelLocalizationManagerWindow`. The SHINOBU hot files contain no `Dictionary<uint,string>`, no `NativeParallelHashMap<uint,long>`, no `Pack=1`, no weak Burst flags, no `string.Format`, no `FindObjectOfType`, and no `GameObject.Find`. `LocalizationManager` retains pre-existing sibling namespace usings and a dev-only legacy `string.Format` branch, but the SHINOBU-added CSV monitor uses only `LocRegistry`, File I/O, dispatcher timing, and Vault-owned mutation.
Rejected Alternatives: Removing unrelated pre-existing `LocalizationManager` domain references in this pass was rejected because it would be broad cross-domain surgery outside the Babel XML and could break unrelated localization features.
Scalability potential: The hot Babel path remains a flat binary lookup service. Low caps request batches through `GlobalQualityWeight`; Middle ramps smoothly; High/Ultra drains full batches and supports richer decrypted diagnostics.
Hardware Impact: SHINOBU-added code adds no gameplay-frame GC path. Dev CSV poll is bounded to 0.5 s cadence and does not run in Burst or lookup jobs.

## Decision 014 - Babel Interface Registry Sidecar
Problem: `GlobalRegistry.BabelLocalization` exposed `IBabelLocalization`, but the registry had no way to register an isolated interface provider. CI/mock-only Babel tests still had to drag the concrete `LocalizationManager` surface, preserving compile-wall pressure and mock chaos.
Solution: Added `_babelLocalizationRuntime` as a lightweight sidecar in `GlobalRegistry`, plus `RegisterBabelLocalizationRuntime(IBabelLocalization)` and `UnregisterBabelLocalizationRuntime(IBabelLocalization)`. `TryGet<IBabelLocalization>` now resolves the sidecar first. `LocalizationManager` registers itself into the interface sidecar during boot and unregisters that sidecar before removing the legacy concrete runtime.
Rejected Alternatives: Replacing `GlobalRegistry.Localization` with an interface was rejected because too many legacy callers still use concrete APIs and would turn SHINOBU_50 into a project-wide migration. Adding a new enum service slot was rejected because it expands registry ABI and service telemetry beyond this lane. Routing the sidecar through generic `UnregisterService` was rejected after review because it would trigger a second memory reap for the same localization lifetime.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged at runtime; the gain is architectural. Babel-only consumers can bind the allocation-free interface and avoid recompiling UI/audio/world concrete surfaces.
Hardware Impact: 0 us/frame. Iteration gain is compile-wall reduction for isolated tests and mock providers, not a runtime micro-optimization.

## Decision 015 - Cross-Domain Compile-Wall Stitch
Problem: A fresh Core build after SHINOBU recheck failed in concurrently modified SaveSystem files, not Babel: missing pager queue/result symbols and two AUP variable typos. A later pass exposed a transient UI glitch job symbol while that file was also moving.
Solution: Applied the smallest SaveSystem stitch needed to restore compile while preserving the other agent's Vault queue direction: fixed `sectorOrigin`/`sectorOriginMeters` typos and kept pager command/result state routed through `GlobalDataVault` buffers. Re-ran Core after concurrent file movement stabilized, then Editor and PlayModeTests.
Rejected Alternatives: Reverting the SaveSystem file was rejected because it would destroy another agent's large WAL/RLE work. Expanding SHINOBU into a SaveSystem refactor was rejected because the domain boundary is Babel; this was compile-wall surgery only.
Scalability potential: No runtime tier behavior changed for Babel. The benefit is integration stability: Babel can be verified without a foreign SaveSystem symbol wall.
Hardware Impact: 0 us/frame in Babel. Iteration-time gain is removal of a current 46-error compiler blocker.

## Decision 016 - Generated Project Contract Linkage
Problem: A fresh serial Core build after the GlobalRegistry sidecar exposed generated-project drift, not a Babel binary fault: `SignalWardenRuntime` preserved `WaterlineBreachSignal` while `Hecton8.Core.csproj` omitted the existing waterline contracts file; `HectonNetworkManager` referenced the existing rollback runtime while the generated project omitted rollback contracts/runtime; rollback contracts also depended on the existing memory-sentinel signal/contracts files.
Solution: Added the existing `ShinobuOceanSurfaceAtmosphereContracts.cs`, `HectonRollbackNetcodeRuntime.cs`, `RollbackNetcodeContracts.cs`, `MemorySentinelSignals.cs`, and `MemorySentinelContracts.cs` compile includes to `Hecton8.Core.csproj`, and added the missing `Unity.Jobs` import to `HectonRollbackNetcodeRuntime.cs` so its `IJob.Run()` calls resolve. No duplicate mock signal was created.
Rejected Alternatives: Creating a second `WaterlineBreachSignal` in Core was rejected because Unity's assembly surface already has the real signal and a duplicate would become a real type conflict. Removing AOT preserve lanes or network manager calls was rejected because that hides the compile wall rather than aligning project linkage with source truth.
Scalability potential: Low/Middle/High/Ultra Babel behavior is unchanged. The gain is build determinism: generated Core project now sees the same contract files that Unity source already expects, so Babel verification does not inherit unrelated missing-symbol noise.
Hardware Impact: 0 us/frame in Babel. Iteration-time gain is the removal of current Core compiler blockers; runtime memory, GC, and lookup cadence are unchanged.

## Decision 017 - FutureCommandEnvelope Project Surface
Problem: A fresh Core compile-wall recheck failed at `HectonAPI.RequestFuture(in FutureCommandEnvelope)` because `HectonAPI.cs` was included in `Hecton8.Core.csproj`, while the existing source file that defines the exact 64-byte unmanaged `FutureCommandEnvelope` (`FutureCommandSandboxValidator.cs`) was not included in the generated project surface.
Solution: Added `Assets\_Project\Scripts\ModdingAPI\FutureCommandSandboxValidator.cs` to `Hecton8.Core.csproj` next to the other ModdingAPI compile includes. The existing DTO is `[StructLayout(LayoutKind.Explicit, Size = 64)]` with offsets 0/4/8/32/48/56, so no duplicate type or new cross-domain contract was created.
Rejected Alternatives: Duplicating `FutureCommandEnvelope` in `HectonAPI.cs` was rejected because it would create ABI drift the moment the validator evolves. Reusing `FutureCommandEnvelope64` from `Hecton8.Global.Contracts` was rejected because it is a reserved seam envelope with a different field schema.
Scalability potential: Low/Middle/High/Ultra Babel behavior is unchanged; this is compile-wall alignment only. The modding validator keeps binary command ingress decoupled from concrete runtime systems.
Hardware Impact: 0 us/frame in Babel. Iteration-time gain is removal of the current missing-symbol blocker once the CPU window permits a serial Core recheck.

## Decision 018 - Compile-Wall Cascade Recheck
Problem: The post-Loop12 compile window reopened and exposed moving project-surface drift outside the Babel lane: Construction signal includes, FutureCommand validator inclusion, rollback vault ID mismatch, fauna flag DTO migration, a Sargassum validity call shape, a Bioluminescence helper scope leak, duplicate AssetLifecycle native-handle helper methods, and a World addressable byte estimator race.
Solution: Applied the smallest stitches needed to align generated project files and existing contracts with current source truth, removed the duplicate AssetLifecycle helper block, kept the World owner estimator as the single implementation, and reran serial Core, Editor, and PlayModeTests builds to 0 errors. Babel binary/runtime code stayed isolated.
Rejected Alternatives: Reverting other agents' large files was rejected because it would destroy concurrent work. Duplicating DTOs, signals, or estimator methods was rejected because it creates ABI drift. Adding direct Babel references to sibling runtime domains was rejected by the compile-wall contract.
Scalability potential: Babel Low/Middle/High/Ultra behavior is unchanged: low-tier request batches remain capped by continuous `GlobalQualityWeight`, high/ultra drains full queues and spends saved CPU on presentation diagnostics. The integration stitches only remove stale compile blockers.
Hardware Impact: 0 us/frame in Babel and 0 B/frame added to gameplay. The measurable gain is iteration-time recovery: Core/Editor/PlayModeTests now compile from the current source surface, while Babel payload alignment and lookup cost remain unchanged.

## Decision 019 - Babel Contract Extraction From Registry Header
Problem: `IBabelLocalization` lived inside `GlobalRegistryContracts.cs`, a large concrete registry contract header already carrying direct sibling-domain usings and legacy `Pack=1` DTOs. That made Babel-only mocks and tests depend on the same compile-wall header that the task is supposed to route around.
Solution: Created `Assets/_Project/Scripts/Core/Contracts/BabelLocalizationContract.cs` in the contract-only namespace `Hecton8.Core.Contracts`, removed the old `IBabelLocalization` declaration from `GlobalRegistryContracts.cs`, added the new file to `Hecton8.Core.csproj`, and made `LocalizationManager` implement the contract-only interface. `GlobalRegistry` already imports `Hecton8.Core.Contracts`, so the sidecar registration API keeps the same call surface.
Rejected Alternatives: Moving the full `LocalizationManager` out of Core was rejected as a broad cross-domain migration. Rewriting all `GlobalRegistryContracts.cs` `Pack=1` DTOs was rejected as a massive core-header operation outside SHINOBU_50 ownership. Leaving `IBabelLocalization` in the heavy header was rejected because it keeps Babel mocks tied to direct sibling-domain references.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. The gain is compile-wall isolation: a Babel-only provider can bind the UTF-8 span contract through Core.Contracts without pulling Audio, World, Gameplay, Input, or UI concrete namespaces through the interface definition.
Hardware Impact: 0 us/frame and 0 B/frame. Fresh Babel verifiers still pass. A fresh dotnet build is pending because CPU samples stayed above the AGENTS.md 50% threshold, so no compiler was launched under load.

## Decision 020 - CPU-Locked Compile Guard And Static Closure
Problem: The user requested another compile-wall pass after the `IBabelLocalization` extraction, but the machine stayed above the AGENTS.md build threshold (`100`, `100`) with no active compiler workers. Launching `dotnet build` under that load would violate the hardware-protection rule.
Solution: Refused to launch the compiler, then completed a static closure pass: verified exactly one project include for each Babel source, verified `IBabelLocalization` is defined only in `Core.Contracts`, verified SHINOBU hot files remain free of `Dictionary<uint,string>`, `NativeParallelHashMap<uint,long>`, `Pack=1`, local native allocations, weak Burst flags, and direct runtime-domain imports, and re-probed the balance payload layout.
Rejected Alternatives: Running `dotnet build` under 100% CPU was rejected by explicit policy. Killing unrelated Python/VS Code/system processes was rejected because those are outside SHINOBU_50 ownership and may belong to the user or other agents. Editing the remaining legacy `Pack=1` DTOs in `GlobalRegistryContracts.cs` was rejected because that is a broad core-header migration outside the Babel XML.
Scalability potential: Runtime behavior is unchanged. Low/Middle/High/Ultra all keep the same contiguous byte-span lookup lane; the compile-wall gain is isolated contract binding for Babel mocks/providers.
Hardware Impact: 0 us/frame and 0 B/frame. The actual compile proof remains pending until CPU <50% and no compiler workers are active.

# SHINOBU_103 Rationale

Agent: SHINOBU_103
Domain: ECHELON 1 / Data Monolith (Static DB)
Status: IMPLEMENTED_BLOCKED_BY_EXTERNAL_COMPILE_WALL_AFTER_SCAVENGING_NATIVE_EDITOR_CSV_GATE

## Decision 000: Batch Memory Initialization

Problem: Agent state files were absent, which would break anti-amnesia and decision journaling on the first implementation loop.
Solution: Created fresh status and rationale files before code changes; all progress will be file-backed.
Rejected Alternatives: Chat-only tracking; rejected because context compression and CTO file review require persistent disk evidence.
Scalability potential: Not runtime-facing; prevents batch drift that would cause wrong-system edits.
Hardware Impact: 0 us/frame; no runtime code path touched.

## Decision 001: Static Data Source Of Truth

Problem: `Data/Balance/Baked/H8StaticData.bin` and `Babel_Dictionary.h8bin` exist, but the task targets the missing StreamingAssets Data Monolith. Keeping both as runtime truth would preserve the Ghost Engine lie.
Solution: Treat `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` as the authoritative boot payload and keep `Data/Balance/Baked/*` as legacy/small-store evidence only.
Rejected Alternatives: Wire the older `H8StaticData.bin` into bootstrap; rejected because the binary payload ledger explicitly says it is not the authoritative StreamingAssets DataMonolith.
Scalability potential: One contiguous monolith read scales from weak devices to high-end without parallel text parsing or scattered payload probes.
Hardware Impact: Low-end i3/MX350 avoids runtime CSV/JSON parse spikes and directory probing; expected boot CPU reduction is dominated by replacing managed file staging with direct native read in later tasks.

## Decision 002: ARM64 DTO Repacking

Problem: Data Monolith DTOs used `[StructLayout(Pack = 1)]`, and `H8ItemRecord` was expanded without updating its declared record size. This could produce unaligned ARM64 loads and section stride corruption.
Solution: Rebuilt monolith DTOs with explicit offsets, 8/16-byte aligned record sizes, 64-byte telemetry entries, and a 16-byte BIOS header plus 64-byte directory. Item rows are now 80-byte records because CSV cost/access data and UTF-8 slice lengths are real fields, not comments.
Rejected Alternatives: Keep `Pack=1` and rely on x86 tolerance; rejected because Quest/ARM64 can pay unaligned-load penalties and Burst cannot safely vectorize unknown packed DTOs.
Scalability potential: Low uses compact fixed-stride pointer reads; Middle/High/Ultra can bulk-upload full sections to GPU/BRG without runtime string parsing or per-record marshaling.
Hardware Impact: Estimated 5-25 us saved on low-end boot/table hydration by avoiding misaligned section walks and defensive copies; frame hot path impact is 0 us because records are static resident data.

## Decision 003: Header/Directory Endianness Contract

Problem: The compiler wrote headers by raw struct copy, while the runtime read the same bytes as native structs. That silently assumes little-endian and hides file corruption behind host ABI behavior.
Solution: Header, directory, and section table are emitted with explicit little-endian byte writers. The editor and runtime fail closed on non-little-endian hosts for record payloads until a per-record byte-swap path exists.
Rejected Alternatives: Generic byte reversal over unmanaged records; rejected because floats, doubles, nested structs, and explicit layouts need per-field handling, not blind word swapping.
Scalability potential: All tiers get identical deterministic boot validation; high-tier can memory-map the same blob without translation.
Hardware Impact: Boot-only cost is negligible; avoiding corrupted binary hydration prevents undefined runtime crashes on i3/MX350-class hardware.

## Decision 004: Vault-Backed Arena With Direct IO

Problem: Runtime load staged `static_data.h8bin` through `File.ReadAllBytes`, allocating one managed byte array as large as the blob before copying to native memory.
Solution: Runtime now requests Data Monolith payload, telemetry ring, and cursor buffers from `GlobalDataVault` using local BufferID constants `71103`, `71104`, and `71105`. File hydration uses memory mapping when available and a direct `FileStream.Read(Span<byte>)` path otherwise; both routes write into Vault-owned bytes.
Rejected Alternatives: Keep private persistent `NativeArray<byte>` as the normal or no-vault path; rejected under Vault Law because persistent buffers must be owned by the boot memory authority.
Scalability potential: Low uses a single sequential read into resident bytes; Middle/High/Ultra can use MMF and direct section spans for zero-copy editor/runtime inspection.
Hardware Impact: Removes a full blob-size managed allocation and copy. For a 10 MB blob on i3/MX350, expected boot GC avoidance is multiple milliseconds and one major managed heap pressure spike; per-frame cost remains 0 us.

## Decision 005: Designer CSV Authority Bridge

Problem: Current `Data/Balance` files are `Items.csv`, `Fauna.csv`, `Economy.csv`, and `Physics.csv`, but the compiler only recognized older aliases and therefore could silently drop rows.
Solution: Added explicit table aliases, Economy and Physics sections, UTF-8 string slice lengths, hash injection from authored IDs, mismatch validation when hash columns are present, and cross-reference fail-fast checks for item-backed recipes/loot.
Rejected Alternatives: Rename designer CSV files or require hash columns in every row; rejected because the compiler must adapt to the current source of truth and inject hashes deterministically.
Scalability potential: Low consumes compact binary sections; Ultra can layer richer records later without changing runtime CSV parsing because designers still author text and the compiler owns conversion.
Hardware Impact: Runtime removes CSV/token parsing entirely for this domain; expected savings are boot/cold-load only and depend on source size, with 50 MB CSV imports kept editor-side.

## Decision 006: Editor Facade Instead Of Runtime Reflection

Problem: Designers need a facade for baking, schema generation, and binary inspection, but reflection or schema text must not leak into runtime assemblies.
Solution: Added a UI Toolkit editor-only compiler window that bakes, generates CSV templates plus a reflection-derived layout manifest, and validates checksum/section layout of the binary.
Rejected Alternatives: Add runtime inspectors or ScriptableObject tuning assets; rejected because runtime must consume only the baked monolith and keep one owner route.
Scalability potential: Low-tier runtime stays binary-only; high-end/editor iteration gets richer inspection without touching gameplay boot code.
Hardware Impact: 0 us/frame; editor-only tooling prevents runtime reflection and managed schema scans.

## Decision 007: Stack Scratch For Record Emission

Problem: The editor baker emitted each unmanaged record through a newly allocated managed `byte[]`, which would scale badly for large 50 MB CSV inputs even though it is editor-only.
Solution: Record emission now uses stack-allocated scratch for the fixed Data Monolith DTO sizes and fails closed if a future record exceeds 256 bytes without a deliberate writer.
Rejected Alternatives: Keep per-record heap scratch; rejected because editor-time tooling should not become the next iteration wall.
Scalability potential: Low hardware authors can bake without thousands of small GC allocations; high-end editor runs spend CPU on parsing and hashing, not allocator churn.
Hardware Impact: Editor-only, but on i3/MX350-class machines this can remove thousands of short-lived allocations during large bakes; runtime remains 0 us/frame.

## Decision 008: Compile Guard Obeyed

Problem: Batch protocol requires compile verification, but the active machine reported 96-100% total CPU load and the user explicitly forbade dotnet builds under >50% CPU load or when compile services are active.
Solution: Deferred `dotnet build` and Unity batch bake until CPU pressure drops; continued static audits instead of forcing a compile wall.
Rejected Alternatives: Launching a build immediately; rejected because it violates the hardware protection rule and risks contaminating other agents' parallel work.
Scalability potential: Not runtime-facing; preserves workstation responsiveness while other agents are active.
Hardware Impact: Avoids a multi-minute compile spike on already saturated hardware.

## Decision 009: Telemetry And Source-Route Hardening

Problem: The first staged telemetry path could clear cached arena/telemetry handles before dumping a failed file read, and recursive source enumeration could pick up generated `Data/Balance/Baked` manifests or schema templates.
Solution: Record and dump telemetry before arena shutdown on read failure, store actual IO ticks and MMF/FileStream flags into the final `Loaded` entry, and exclude `Data/Balance/Baked` plus `Data/Balance/Schemas` from source enumeration and watcher triggers.
Rejected Alternatives: Keep zero-tick success telemetry and broad recursive file ownership; rejected because black-box proof and one-fact/one-route data ownership are more important than preserving broad legacy convenience.
Scalability potential: Low/Middle devices get deterministic boot forensics without runtime cost; High/Ultra editor workflows avoid rebake loops from generated artifacts while keeping the single monolith universal.
Hardware Impact: 0 us/frame. Boot-only work preserves the real IO path in telemetry; editor source filtering prevents pointless rebake work on weak i3/MX350 machines.

## Decision 010: Same-Domain Burst Job Cleanup

Problem: `H8CreatureSoAReconstructJob` and `H8ItemSoAReconstructJob` were still on bare `[BurstCompile]` and lacked `[NoAlias]` field proofs, even though they consume Data Monolith records.
Solution: Added `CompileSynchronously=true`, `FloatMode.Fast`, `FloatPrecision.Standard`, and explicit `[NoAlias]` on input/output arrays.
Rejected Alternatives: Treat those jobs as out of scope; rejected because they are same-domain Data Monolith unpack jobs and would remain the obvious compile/vectorization weak spot.
Scalability potential: Low devices get cheaper monolith-to-SoA reconstruction; Middle/High/Ultra can bulk-expand table sections without unnecessary alias pessimism.
Hardware Impact: Estimated 2-10 us saved per large reconstruction pass on i3/MX350-class hardware; 0 us/frame unless a consumer schedules reconstruction.

## Decision 011: External World-Domain Compile Wall

Problem: The first guarded `dotnet build` failed before reaching SHINOBU_103 code because `Hecton8.Core.csproj` references `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs`, while git reports that tracked source file and its `.meta` as deleted in the working tree.
Solution: Classified the failure as an external World-domain dependency blocker and recorded the exact `CS2001` path. No World file was restored, recreated, or replaced by SHINOBU_103 because that would overwrite another agent/user deletion and violate domain ownership.
Rejected Alternatives: Restoring the file from HEAD to make my build pass; rejected because SHINOBU_103 has no ownership of the MapMagic vegetation bridge and blind restoration could erase an intentional World-domain refactor. Removing the `Compile Include` was initially rejected while evidence still indicated an uncommitted World deletion rather than a stale project file in current HEAD.
Scalability potential: Not runtime-facing; preserves one-owner/one-route discipline so Data Monolith does not mutate World architecture to hide a compile gate.
Hardware Impact: 0 us/frame. The failed build consumed about 68 s wall time once under CPU guard; no further build attempts are justified until the missing World source/project reference conflict is resolved.

Correction: Later git evidence showed the file is absent from HEAD and from the index, so the blocker is not an uncommitted World deletion anymore; it is a stale `Hecton8.Core.csproj` compile include. I removed exactly that single stale include and did not recreate World code.

## Decision 012: Post-Blocker Static Polish

Problem: Static inspection after the external compile blocker found three Data Monolith weaknesses that a blocked build could not expose: `H8StaticLocalizationReference` was 12 bytes, the compiler window could list generated schema/baked CSV files as sources, and schema generation relied too much on hardcoded authoring headers rather than reflection-derived struct templates.
Solution: Padded `H8StaticLocalizationReference` to 16 bytes and added it to layout audit; guarded UTF-8 decode by required char count before writing caller-owned spans; made `H8DataMonolithCompiler.IsSourcePath` absolute/relative-safe and routed compiler-window source display through it; added reflection-generated struct CSV templates for item, creature, economy, and physics records.
Rejected Alternatives: Waiting for the external World compile wall to clear; rejected because these are deterministic same-domain defects. Keeping a 12-byte helper DTO was also rejected because future NativeArray use would inherit a bad ARM64 stride.
Scalability potential: Low devices keep aligned fixed-stride metadata and avoid cold UI decode exceptions; Middle/High/Ultra editor workflows get drift-resistant schema output without runtime reflection.
Hardware Impact: Runtime frame impact is 0 us. Cold-path risk reduction is alignment correctness and exception avoidance; editor schema/source filtering prevents useless authoring churn on i3/MX350-class machines.

## Decision 013: Stale CSProj Include Removal

Problem: A second check showed `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs` no longer exists in HEAD or the git index, but `Hecton8.Core.csproj` still includes it, guaranteeing repeat `CS2001` before any Data Monolith diagnostics.
Solution: Removed the single stale `Compile Include` line from `Hecton8.Core.csproj`. This is a build metadata correction, not a World implementation change.
Rejected Alternatives: Recreating the deleted World file from old history; rejected because it would resurrect stale World behavior outside SHINOBU_103 ownership. Re-running build without removing the stale include was also rejected because it would reproduce the same known compile wall.
Scalability potential: Not runtime-facing; unblocks compile validation so the binary monolith path can be proven without corrupting World ownership.
Hardware Impact: 0 us/frame. Prevents another single-node build from spending about a minute to rediscover the same missing source path.

## Decision 014: Current CSV Surface Verification

Problem: The compiler can be structurally sound and still fail the first bake if the live `Data/Balance` headers drift from parser aliases.
Solution: Read the actual headers and rows for `Items.csv`, `Fauna.csv`, `Economy.csv`, and `Physics.csv`. The supported aliases cover the observed fields: `Id`, `Name`, `Description`, `CategoryId`, `Cost`, `StackMax`, `MassKg`, `AccessFrequency`, `SwimSpeed`, `TurnRate`, `Aggression01`, `FleeDistanceM`, `BiolumIntensity`, `BasePrice`, `Scarcity01`, `Demand01`, `SupplyRefreshSeconds`, `AddedMass`, `LinearDrag`, `Buoyancy`, and `CrushDepthM`.
Rejected Alternatives: Trusting the prompt or previous generated `Data/Balance/Baked` payloads as source evidence; rejected because those binaries are explicitly non-authoritative for the StreamingAssets monolith.
Scalability potential: Low/Middle/High/Ultra all consume the same baked binary; this check prevents editor-time schema drift before runtime boot ever sees the blob.
Hardware Impact: 0 us/frame. Prevents a wasted bake/import loop on weak i3/MX350-class machines.

## Decision 015: Economy Reference Gate Correction

Problem: Task 14 explicitly requires Economy recipe/item cross-reference fail-fast, but the staged validator only covered dedicated recipe rows and loot CDF item rows. Current `Economy.csv` does not contain item-reference columns, yet the baker must fail closed when designers add them later.
Solution: Preserve raw Economy rows in the editor-only `DataSet` and validate optional `item_id`, `item`, `output`, `output_id`, `recipe_output`, `recipe_output_id`, `ingredients`, `ingredient_ids`, `recipe`, and `recipe_items` fields against the Item hash set before any blob bytes are written.
Rejected Alternatives: Expanding `H8EconomyRecord` with unused item reference slots; rejected because current live CSV has no such fields and ABI churn would waste static payload bytes. Leaving recipe/loot-only validation was rejected because it under-implements the XML assignment.
Scalability potential: Low/Middle/High/Ultra runtimes still load one unchanged universal monolith; the editor gate prevents broken designer references from becoming runtime fault branches.
Hardware Impact: 0 us/frame. Editor-only linear validation over Economy rows avoids any runtime foreign-key checking on i3/MX350-class hardware.

## Decision 016: Final Blob Alignment

Problem: Section payload starts were explicitly 16-byte aligned, but the final file length could terminate immediately after an arbitrary-length UTF-8 string pool. The XML requires ARM64-safe payload alignment, and the binary payload ledger treats misaligned product binaries as static-data debt.
Solution: Add one final `Align16(stream)` after all sections are written and before directory/checksum emission. Section counts stay exact; trailing padding is outside all section ranges and included in the XXHash3 seal.
Rejected Alternatives: Accepting an unaligned final byte length because individual section offsets are aligned; rejected because product binary hygiene and future mmap/page diagnostics should see an aligned blob end as well.
Scalability potential: Universal file remains one monolith across Low/Middle/High/Ultra tiers; aligned terminal padding keeps future bulk upload/mmap readers simple.
Hardware Impact: 0 us/frame. Adds 0-15 bytes per bake and avoids future binary hygiene failures on weak authoring hardware.

## Decision 017: Unsigned UTF-8 Offset ABI

Problem: Task 09 requires records to store unsigned string-pool offsets plus byte lengths, but the staged DTOs still used signed `int` offsets to preserve a `-1` missing sentinel. That is a contract mismatch even though the field sizes are both 4 bytes.
Solution: Convert all Data Monolith UTF-8 offset fields to `uint`, use `uint.MaxValue` as the missing sentinel, make `LocalizationPool` emit `uint` offsets, add unsigned overloads in `H8StaticDataArena`, and update the single static LocData alias consumer (`LocRegistry`) with an `int.MaxValue` guard before its legacy packed-index write.
Rejected Alternatives: Keeping signed offsets because it compiled locally; rejected because the binary file contract must be exact. Expanding records for a separate validity flag was rejected because `uint.MaxValue` is unambiguous under the 256MB blob cap.
Scalability potential: Low/Middle/High/Ultra use the same ABI. Unsigned offsets allow direct GPU/native metadata export later without signed-sentinel translation.
Hardware Impact: 0 us/frame. Runtime decoding remains zero-allocation; the only extra guard is a cold LocRegistry alias bounds check.

## Decision 018: Player Vault Hard Fail

Problem: `H8StaticDataArena` still had a private persistent `NativeArray<byte>` fallback when `GlobalDataVault` was absent. That contradicted XML Task 11, which says FileStream fallback memory is allocated via `GlobalDataVault`, and contradicted the latest H-PHI/Vault mandate requiring zero private array allocations.
Solution: Remove the no-vault owned byte arena. `TryAllocateArena` now resolves only Vault BufferID `71103`; if the vault is absent or cannot provide the requested capacity, arena allocation fails and the boot path reports `ReadFailed`/throws through the existing fail-fast player gate.
Rejected Alternatives: Keeping an editor/player private fallback for convenience; rejected because it preserves a second memory owner and lies to telemetry. Creating a separate Data Monolith-local vault was rejected because it would be another global authority surface instead of using the boot-owned `GlobalDataVault`.
Scalability potential: Low/Middle/High/Ultra all use the same resident payload ownership route. MMF remains the high-end read path, FileStream remains the hostile-platform copy path, and neither path owns memory outside the Vault.
Hardware Impact: 0 us/frame. Prevents a blob-sized private native allocation outside the Vault accounting path; on i3/MX350 this avoids hidden native memory pressure during boot and keeps NativeMemorySentinel/Vault forensics single-owner.

## Decision 019: Spec Reconciliation And Mock Boundary

Problem: The active `DATA_MONOLITH_H8BIN_SPEC.md` still carried stale ABI facts: the header was documented as world/app-version fields, `H8ItemRecord` was documented as 64 bytes, section IDs stopped before Economy/PhysicsConstants, and wording said records were "packed" even though Pack=1 was explicitly removed.
Solution: Corrected the spec to match current source: 16-byte magic/version/header/checksum header, section IDs 25 and 26, 80-byte item records, 64-byte economy/physics/telemetry records, 16-byte static localization references, and explicit-layout language. Rechecked targeted static-data mock/parser routes: production boot still hard-fails outside the editor; editor missing-file tolerance is the CI/import fallback, not a runtime emergency monolith.
Rejected Alternatives: Leaving stale spec text for a later documentation pass; rejected because future consumers would bake or reinterpret the wrong stride. Adding a deterministic runtime fallback monolith for CI was rejected because Task 01 explicitly kills Ghost Engine production fallback. Broadly deleting other agents' `GenerateEmergencyMock...` helpers was rejected because the SHINOBU_103 XML says those can remain for unit tests and other domains own them.
Scalability potential: Low/Middle/High/Ultra use one ABI and one source-of-truth route. Correct docs prevent accidental low/high binary forks or stale 64-byte item consumers.
Hardware Impact: 0 us/frame. Prevents a future 80-vs-64 byte stride bug that would corrupt table walks and waste a full boot/bake cycle on low-end i3/MX350-class authoring hardware.

## Decision 020: Complete UTF-8 Slice Metadata

Problem: Item, Economy, and Physics records had unsigned string-pool offsets plus byte lengths, but creature display names, biome names, audio Addressables keys, ghost-module names, and SOP error messages still stored only offsets and relied on null-terminated scans. Task 09 requires offset+length metadata for text slices.
Solution: Reused existing 4-byte reserved slots inside the fixed records to add `DisplayNameUtf8ByteLength`, `AddressableKeyUtf8ByteLength`, and `MessageUtf8ByteLength` fields without increasing any section stride. The baker now emits these lengths for CSV and JSON source rows. Runtime static-localization alias extraction and audio key decoding use bounded span reads before decoding.
Rejected Alternatives: Increasing record sizes; rejected because the existing padding/reserved fields are enough and stride churn would add avoidable integration risk. Keeping null-terminated scans as the only route was rejected because it under-implements the binary text-slice contract and wastes cold lookup cycles.
Scalability potential: Low/Middle devices avoid repeated delimiter scans when LocData aliases are merged. High/Ultra can bulk-export text-slice metadata to native/GPU consumers without a null-terminated walk.
Hardware Impact: Runtime hot path remains 0 us/frame. Cold static text import saves one linear UTF-8 scan per alias/key lookup; on i3/MX350 the gain is small per string but deterministic across large localization batches.

## Decision 021: Android/Quest StreamingAssets URI Staging

Problem: `Application.streamingAssetsPath` is not guaranteed to be a filesystem directory on Android/Quest. The Data Monolith boot path built a path and immediately used `File.Exists`/`FileStream`, which would classify a valid packaged `jar:` StreamingAssets blob as missing before the Vault-backed checksum reader ever ran.
Solution: Added a non-filesystem StreamingAssets route in `H8StaticDataArena`. URI roots are staged into `Application.temporaryCachePath/Hecton8/DataMonolith/static_data.h8bin` through `UnityWebRequest` with `DownloadHandlerFile`, then the existing `TryInitializeFromFile` path performs FileInfo sizing, Vault BufferID `71103` allocation, FileStream-to-Vault copy, XXHash3 validation, directory audit, and telemetry. The staging hop is marked with `PathFlagStreamingUriStaged`, and fatal early failures now call the telemetry/dump path before returning when the Vault is available.
Rejected Alternatives: Using `DownloadHandlerBuffer` was rejected because it would allocate a managed blob-sized byte array. Keeping a pure `FileStream` path was rejected because it is desktop-biased and breaks Android/Quest packaged assets. Adding a runtime mock/fallback blob was rejected by Task 01.
Scalability potential: Low/Quest uses one cold URI-to-cache stage and then the same Vault-backed monolith reader as desktop. Middle/High/Ultra desktop still gets MMF first. All tiers consume the same checksum-sealed binary route, so there is no low/high data fork.
Hardware Impact: Runtime hot path remains 0 us/frame. Boot avoids one managed blob allocation on Android/Quest and preserves the same XXHash3/Vault verification path; the cost is a cold staging copy only when StreamingAssets is not a filesystem path.

## Decision 022: External Compile Wall After Guarded Build

Problem: After CPU/process guard cleared, a single `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` failed with 79 external errors before Data Monolith diagnostics.
Solution: Recorded the failure as an external compile wall. First blockers are missing Gameplay/Visor/Equipment/Fauna/World contracts/types such as `Hecton8.Animation.KineticCharacter`, `UberNoirReconstructionConstantsDTO`, `MockReconstructionInputSignal`, `DynamicDecalFrameStats`, `ActiveEquipmentDTO`, `MesofaunaTuningDTO`, and `MacroEcosystemSectorVaultRecord`.
Rejected Alternatives: Creating placeholder DTOs or changing those domains from SHINOBU_103 was rejected because it would violate one-owner routing and hide real integration debt outside Data Monolith authority.
Scalability potential: Not runtime-facing; protects compile-wall truth by not papering over missing cross-domain contracts.
Hardware Impact: 0 us/frame. The guarded build consumed 97.7 s wall time; no additional compile attempt is justified until external missing contracts are restored by their owners. After the early-failure telemetry micro-patch, static audits were used instead of burning another compile slot against the same external wall.

## Decision 023: StreamingAssets Staging Symbol Qualification

Problem: `H8StaticDataArena` has a public `Directory` property returning `H8DataBlobDirectory`. The Android/Quest staging patch used an unqualified `Directory.CreateDirectory(...)` call inside the same class, which can bind against the property name instead of `System.IO.Directory` once the external compile wall no longer masks Data Monolith diagnostics.
Solution: Qualified the staging directory creation call as `System.IO.Directory.CreateDirectory(...)`. The dump path already used `System.IO.Directory`, so this makes both cold file-system calls explicit and removes a deterministic symbol-resolution trap.
Rejected Alternatives: Renaming the public `Directory` property was rejected because it is a broader API change for external consumers. Leaving the unqualified call was rejected because it waits for the external compile wall to expose a preventable same-domain error.
Scalability potential: Low/Quest still uses the URI-to-cache staging route and then the same Vault-backed reader. Middle/High/Ultra desktop keeps MMF-first behavior. No tier fork or binary quality switch was introduced.
Hardware Impact: Runtime hot path remains 0 us/frame. Boot behavior is unchanged except that the staging directory call is compile-stable; CPU guard blocked a new build with samples `99.03, 92.47, 70, 35.09%`.

## Decision 024: Compile-Wall Boundary Truth

Problem: The user mandate demands assembly isolation, but the current repository places Data Monolith runtime source under `Assets/_Project/Scripts/Data/Monolith` and includes it directly in `Hecton8.Core.csproj`. `GameBootstrapper` also lives in Core and calls `H8StaticDataArena`, so moving Data Monolith into a new runtime asmdef during this patch would create a Core/Data circular dependency unless bootstrap ownership is redesigned.
Solution: Verified the actual route instead of inventing a fake compile-wall claim. Data Monolith runtime files import only `Hecton8.Core` and `Hecton8.Core.Memory`; the broad sibling-reference problem is in `Hecton8.Core.asmdef`, not in a Data Monolith asmdef. I recorded this as an integration boundary and did not create a new assembly surface from SHINOBU_103.
Rejected Alternatives: Adding `Hecton8.Data.Runtime.asmdef` immediately was rejected because Core bootstrap currently depends on Data Monolith symbols while Data Monolith depends on Core Vault/FatalArchitecture contracts. Moving bootstrap contracts or introducing a new registry facade is outside this domain and would risk breaking other agents' active work.
Scalability potential: The runtime data format still scales as one universal binary; this decision prevents a compile-wall refactor from corrupting boot ownership while external compile errors are unresolved.
Hardware Impact: 0 us/frame. It avoids a risky assembly churn that could trigger additional full-domain recompiles without proving the binary payload path.

## Decision 025: Prebuild Artifact Gate And Atomic Promotion

Problem: `H8DataMonolithCompiler.BakeAll` wrote the production `static_data.h8bin` directly and there was no build preprocessor forcing a bake/validate pass before player builds. That allowed two bad states: a half-written blob if an editor write was interrupted, or a release build attempted without the authoritative monolith artifact.
Solution: Replaced direct production overwrite with a temp-write, full binary validation, and same-directory promote route. Added `H8DataMonolithBuildPreprocessor` at callback order `-9100`; it runs `BakeAll(false)` and then `TryValidateOutputBlob` before any player build can continue. The validator checks fixed header bytes, little-endian directory fields, XXHash3 over `[16..end)`, section table offset/byte count, section ID order, expected record size, nonempty range bounds, 16-byte section offsets, localization directory mirroring, and final file alignment.
Rejected Alternatives: Manual bake discipline was rejected because it is not a release gate. Direct `File.WriteAllBytes(OutputAssetPath, blob)` was rejected because it can corrupt the only production static-data artifact on interruption. Creating a runtime fallback monolith was rejected because Task 01 explicitly removes production Ghost Engine fallback behavior.
Scalability potential: Low/Middle/High/Ultra all still consume one universal monolith. Weak authoring machines avoid repeated failed player builds from stale/missing artifacts; high-end build agents get a deterministic fail-fast binary contract before ContentAuthority or player packaging work proceeds.
Hardware Impact: Runtime 0 us/frame. Editor/build-only validation adds a linear cold pass over the blob and prevents a much larger wasted build/import cycle on i3/MX350-class machines.

## Decision 026: Architecture Doc Currentness Correction

Problem: Stable architecture docs still claimed the Data Monolith route used runtime `File.ReadAllBytes`, that `GameBootstrapper` accepted missing monoliths, and that Balance CSVs required authored `id/hash32` pairs. Those statements now contradict source and would push the next owner back toward old behavior.
Solution: Updated `BOOT_SEQUENCE_TOPOLOGY.md`, `HECTON8_P0_FOUNDATION_PROOF_MATRIX.md`, `SUBNAUTICA2_HECTON8_IMPLEMENTATION_HANDOFF.md`, and `SUBNAUTICA2_EA_TO_HECTON8_PRODUCTION_CONTRACTS.md` to reflect MMF/FileStream-to-Vault loading, Android/Quest URI staging, fail-closed player boot, compiler-side FNV-1a hash injection, and the new prebuild bake/validate gate.
Rejected Alternatives: Leaving stale docs for a chronicler pass was rejected because Data Monolith is a boot-critical authority route; stale docs here produce wrong build gates and wrong runtime expectations.
Scalability potential: All hardware tiers keep one binary source of truth and no low/high monolith fork. The docs now direct future platform proof toward Android/Quest packaging and boot memory evidence instead of already-removed managed staging.
Hardware Impact: Runtime 0 us/frame. Documentation correctness prevents build and integration churn; no player code path changed in this decision.

## Decision 027: Data Monolith Editor Import Boundary

Problem: The editor compiler/window files existed under `Assets/_Project/Scripts/Editor/DataMonolith`, but the current generated csproj set did not include `H8DataMonolithCompiler.cs` or `H8DataMonolithCompilerWindow.cs`, and `H8DataMonolithCompilerWindow.cs` had no `.meta`. That means Tasks 18-20 could appear implemented on disk while Unity import/menu discovery remained nondeterministic.
Solution: Added a dedicated `Hecton8.DataMonolith.Editor.asmdef` scoped to Editor and referencing only `Hecton8.Core`, `Unity.Burst`, `Unity.Collections`, and `Unity.Mathematics`. Added stable `.meta` files for the asmdef and compiler window. Updated the Data Monolith spec and binary payload ledger to record this editor import boundary and remove the stale editor `File.WriteAllBytes` claim. Runtime Data Monolith code remains in Core for now because Core bootstrap calls `H8StaticDataArena` and the arena depends on Core Vault/fatal boot contracts.
Rejected Alternatives: Editing ignored/generated `.csproj` files was rejected because Unity will overwrite them and they are not source authority. Adding a Data runtime asmdef was rejected again because it would introduce a Core/Data circular dependency without a planned bootstrap facade. Leaving missing metas was rejected because it would let Unity mint local GUIDs and risk menu/facade drift.
Scalability potential: Low/Middle/High/Ultra runtime payload behavior is unchanged: one universal monolith. Editor compile blast radius is narrower because Data Monolith tooling no longer relies on broad `Hecton8.Editor` inclusion once Unity imports the child asmdef.
Hardware Impact: Runtime 0 us/frame. Editor iteration impact is reduced assembly churn after Unity import; latest CPU samples were `54.6, 60.44, 100, 100%`, so no build/import verification was launched.

## Decision 028: Scavenging Loot Consumer Mock Demotion

Problem: `ScavengingLootOracle` was a static-data consumer that still scheduled `GenerateEmergencyMockLootTablesJob` as the default loot-table dependency before resolving real queued loot. That preserves a downstream Ghost Engine path even though the primary Data Monolith boot already fails closed outside the editor.
Solution: Added a narrow consumer bridge that imports the first contiguous `LootCdf` table from `H8StaticDataArena.TryGetSectionSpan<H8LootCdfRecord>()` into the Scavenging Vault `LootTableEntryDTO` buffer. Player builds with a valid monolith but no `LootCdf` rows now expose zero active loot entries and set table hash/version to zero; forced-item requests still resolve through their explicit forced path. The emergency CDF generator remains available for editor/manual self-audit and tests.
Rejected Alternatives: Deleting the emergency mock was rejected because the XML says mocks may remain for tests. Adding a Scavenging-owned CSV/runtime reader was rejected because it reintroduces text parsing and a second static-data owner. Creating a new runtime Data asmdef or new cross-domain contract was rejected because Data Monolith runtime is still compiled in Core and bootstrap split remains a larger integration migration.
Scalability potential: Low/Middle/High/Ultra all consume one universal monolith; Scavenging no longer forks loot truth for weak hardware. High-end can later use richer loot tables in the same `LootCdf` section without changing runtime parsing.
Hardware Impact: Runtime hot path remains 0 B GC. First-use copy is a bounded cold copy from resident monolith span to Vault entries; it removes fake four-entry CDF production truth rather than adding a per-frame cost. CPU/process guard sampling timed out twice, so no new build was launched.

## Decision 029: Binary Inspector Uses Release Validator

Problem: The Task 20 compiler-window inspector displayed header/checksum/section diagnostics by reading the blob itself, but it did not first call the same `TryValidateOutputBlob` gate used by atomic bake promotion and the player-build preprocessor. That created a weaker editor proof surface than the release artifact gate.
Solution: Route `H8DataMonolithCompilerWindow.InspectBinary()` through `H8DataMonolithCompiler.TryValidateOutputBlob(out error)` before printing local diagnostics, and document that the inspector is diagnostic UI layered on top of the release validator.
Rejected Alternatives: Copying the full validator into the window was rejected because two validator implementations drift. Treating a local checksum PASS label as enough was rejected because the prebuild validator also checks fixed header fields, directory shape, section order, record sizes, range bounds, localization mirror, and final 16-byte alignment.
Scalability potential: Low/Middle/High/Ultra runtime data remains one universal binary. Weak authoring machines get the same fail-fast artifact verdict in UI and builds instead of discovering stricter release validation later.
Hardware Impact: Runtime 0 us/frame. Editor-only validation may read the blob once when the inspector refreshes; this replaces future wasted build/import cycles, not a gameplay cost. No build was launched for this editor-only patch because static checks covered the route and the known external compile wall remains.

## Decision 030: Preserve Baker Validation Errors In Facade Refresh

Problem: `H8DataMonolithCompilerWindow.Bake()` displayed `LastError`, then called `RefreshAll()`. `RefreshAll()` called `InspectBinary()`, and the inspector's shared validator call wrote missing/stale blob errors back into `H8DataMonolithCompiler.LastError`. That could erase the actual cross-reference/bake error state that Task 18 requires the facade to expose.
Solution: Add an optional `updateLastError` flag to `TryValidateOutputBlob`, keep the default `true` for the prebuild gate and build pipeline, and call it with `false` from the inspector. The inspector separately prints `last-baker-error=` when the compiler owns a stored bake failure.
Rejected Alternatives: A second window-local error cache was rejected because the compiler is the validation owner. Letting inspector checks overwrite baker errors was rejected because a missing output blob is a consequence, not the root cross-reference failure. Copying the full validator was rejected again because duplicate validators drift.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged. Weak authoring hardware gets deterministic editor diagnostics without an extra failed build/import loop to discover the original bad CSV reference.
Hardware Impact: Runtime 0 us/frame. Editor-only string UI work on refresh; no gameplay allocation or binary path changes. Build was not launched because static checks covered the editor-only signature/call-site change and external compile blockers are already documented.

## Decision 031: Runtime Directory Validation Parity

Problem: The editor/prebuild validator enforced canonical section order, expected record sizes, zero offset for empty sections, exact data-start alignment, and localization directory mirroring. Runtime `IsDirectoryValid()` accepted any section id/order/record size as long as broad ranges were inside the blob. A checksum-valid but stale or malicious blob could become resident and fail later at consumer access instead of at boot.
Solution: Move expected section stride authority into `H8DataLayoutAudit.GetExpectedRecordSize()`, route the editor compiler's validation through it, and tighten runtime directory validation to require section count 26, ids `1..26` in order, exact record size, exact data start, 16-byte data-start alignment, zero offsets for empty sections, payload offsets after `DataStartOffset`, and localization section/directory mirror.
Rejected Alternatives: Leaving runtime permissive because the prebuild gate is stricter was rejected; production can still encounter stale files, modded files, bad patch artifacts, or copied blobs. Duplicating another record-size switch in runtime was rejected because that recreates editor/runtime validator drift.
Scalability potential: Low/Middle/High/Ultra still load one universal monolith. Boot does 26 fixed iterations; consumers get a stronger Ready guarantee and do not need to defend against malformed static section metadata in hot code.
Hardware Impact: Runtime hot path 0 us/frame. Boot adds O(26) integer checks, well below measurement noise, and prevents later consumer stalls or crashes from malformed section metadata.

## Decision 032: Cross-Reference Provenance Gate

Problem: Task 14 validation rejected some broken item references, but the failure message only reported an owner/hash pair. Recipe validation used baked recipe records, and loot validation used rebuilt `LootCdf` records, so the baker could not identify the exact CSV/JSON row, field, or packed token that caused the failure.
Solution: Carry source provenance in editor-only `CsvRow` records. CSV rows now retain absolute path and physical line number; JSON items and recipes get synthetic provenance rows with source index. The cross-reference gate validates raw item, recipe, loot, and economy rows before blob output and reports file, line/source index, field, token index, authored value, and computed FNV-1a hash.
Rejected Alternatives: Expanding runtime DTOs with debug source fields was rejected because provenance is editor-only and would waste static payload bytes. Keeping anonymous owner/hash diagnostics was rejected because designers would need a failed build/import loop or manual hash reverse lookup to repair data.
Scalability potential: Low/Middle/High/Ultra runtimes still consume one universal monolith. Weak authoring machines get precise fail-fast repair data without launching Unity play or player build; high-end pipelines can run the same prebuild gate at scale.
Hardware Impact: Runtime 0 us/frame. Editor-only validation adds no new runtime allocations or ABI fields; it saves wasted bake/build cycles by reporting the exact bad source token.

## Decision 033: Automated Bake Debounce Gate

Problem: Data Monolith source automation had two bake routes. `AssetPostprocessor.OnPostprocessAllAssets` called `BakeAll()` synchronously inside Unity import, and `FileSystemWatcher` scheduled a bake on the next editor update with no debounce. A multi-write CSV save, move, or import burst could bake a half-written source file and rebuild the monolith repeatedly.
Solution: Route both import callbacks and filesystem events through `H8DataMonolithFileSystemWatcher.RequestBake()`. The watcher stores the latest source-change `Stopwatch` tick, waits 0.75 seconds of stability, skips while `EditorApplication.isCompiling` is true, and uses an interlocked `_bakeInProgress` flag to prevent overlapping `BakeAll()` calls.
Rejected Alternatives: Keeping the direct AssetPostprocessor bake was rejected because it performs heavy source parsing and blob writes inside import. Only using FileSystemWatcher was rejected because Unity import callbacks see asset moves/deletes that filesystem events can miss. Adding a runtime fallback monolith was rejected by Task 01.
Scalability potential: Low/Middle authoring machines avoid redundant bake storms and half-written blob output; High/Ultra machines still get automatic hot source refresh without changing the universal runtime monolith.
Hardware Impact: Runtime 0 us/frame. Editor-only savings are burst-dependent: one CSV save that emits 3-6 filesystem/import events now collapses to one bake after the source is stable.

## Decision 034: Bounded CSV Worker Gate

Problem: `ReadCsvSourcesParallel` created one `Task.Run` worker per CSV file. That is acceptable for the current small source set, but it is a poor architecture for a static database compiler expected to ingest large authored content because source-file fanout can exceed useful CPU parallelism and put avoidable pressure on the editor threadpool.
Solution: Bound CSV import workers to `min(fileCount, max(1, Environment.ProcessorCount - 1))` and distribute source files with an interlocked index. Empty CSV sets return immediately.
Rejected Alternatives: Serializing all CSV reads was rejected because Task 06 requires multi-threaded ingestion. Keeping one task per file was rejected because it makes authoring file count control worker count instead of CPU capacity.
Scalability potential: Low/Middle authoring machines keep one core free for the editor and OS while still reading CSVs in parallel; High/Ultra machines scale up to useful core count without launching hundreds of workers.
Hardware Impact: Runtime 0 us/frame. Editor-only improvement reduces threadpool wakeups from O(file count) tasks to O(cpu count) tasks and avoids redundant `Task.WaitAll` work when no CSV sources exist.

## Decision 035: Facade Literal Bake Button Gate

Problem: Task 18 literally requires a giant `BAKE MONOLITH` button. The window had the command, but it was styled as a normal 160 px toolbar button, which weakens the human-control facade and makes the release-critical bake action visually equivalent to secondary commands.
Solution: Make the primary bake command 260 x 42 px, bold, and vertically centered in the toolbar. Secondary schema/inspect/refresh commands remain standard toolbar controls.
Rejected Alternatives: Keeping the 160 px button was rejected because this is the primary source-of-truth action. Moving bake only to the menu was rejected earlier because designers need an obvious facade command.
Scalability potential: Runtime data remains universal. Weak authoring machines benefit indirectly by making the safe bake route obvious and reducing accidental reliance on stale/missing `static_data.h8bin`.
Hardware Impact: Runtime 0 us/frame. Editor UI style-only; no player code path or runtime allocation changed.

## Decision 036: Hot Reload Locality Gate

Problem: `H8DataMonolithHotReloadSocket.NotifyBake()` connected to the editor's own loopback listener after every play-mode bake, encoded a TCP payload, and trusted any loopback `RELOAD` path. A port collision with another Unity editor could also make the current editor skip its own reload because `TrySendReload()` returned true for the wrong receiver.
Solution: Queue same-process bake reloads directly through the existing main-thread pending slot. Keep the loopback listener only as an external packet bridge, cap packet length to 1024 chars, accept only the canonical `static_data.h8bin` output path, start the listener when a domain reload lands during play mode, and stop it on play-mode exit, assembly reload, and editor quit.
Rejected Alternatives: Keeping self-TCP was rejected because it is IPC theater inside the same editor process and can route to another editor instance. Removing the listener entirely was rejected because external/editor tooling may still use the loopback bridge. Accepting arbitrary paths was rejected because runtime reload authority must stay bound to the one monolith output route.
Scalability potential: Low/Middle authoring machines avoid unnecessary socket work during play-mode rebakes. High/Ultra editor workflows keep external hot-reload hooks while using the direct owner-local queue for normal bakes. Runtime/player data remains one universal monolith.
Hardware Impact: Runtime 0 us/frame. Editor-only savings are one TCP connect, one UTF-8 payload allocation, and socket write per play-mode bake; the larger impact is avoiding wrong-instance reload loss under multiple Unity editors.

## Decision 037: Scavenging Native Editor CSV Gate

Problem: The Scavenging loot oracle's editor/manual CSV audit path still used `File.ReadAllBytes`, then copied the managed `byte[]` into a Temp `NativeArray<byte>` before invoking the native byte parser. This was editor-only, but it preserved the exact whole-file managed staging pattern that the Data Monolith route is eradicating from static-data consumers.
Solution: Replace the editor facade read with a `FileInfo` length gate, a Temp `NativeArray<byte>` allocated with `UninitializedMemory`, and a `FileStream.Read(Span<byte>)` loop into the native buffer. The path rejects zero/oversized files and incomplete reads before calling `ScavengingLootOracleRuntime.TryIngestLootDistributionCsvBytes`.
Rejected Alternatives: Keeping `File.ReadAllBytes` because the code is behind `#if UNITY_EDITOR` was rejected; static-data consumer tooling should exercise the same native-byte parser shape as runtime bridges. Adding a new Scavenging runtime CSV route was rejected because production loot must come from Data Monolith `LootCdf` rows.
Scalability potential: Low/Middle authoring machines avoid a whole-file managed allocation and copy during manual loot CSV audits. High/Ultra editor workflows still parse from native bytes and can scale to larger loot authoring files without training the codebase back toward managed text staging. Runtime/player data remains one universal monolith.
Hardware Impact: Runtime 0 us/frame. Editor-only saving is one file-sized managed allocation plus one byte-copy loop per manual loot CSV import; exact time depends on CSV size and disk cache state.

# SHINOBU_103 Rationale

Agent: SHINOBU_103
Domain: ECHELON 1 / Data Monolith (Static DB)
Status: IMPLEMENTED_PENDING_COMPILE_CPU_GUARD_AFTER_TEXT_SLICE_COMPLETION

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

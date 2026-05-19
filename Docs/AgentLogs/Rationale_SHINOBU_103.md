# SHINOBU_103 Rationale

Agent: SHINOBU_103
Domain: ECHELON 1 / Data Monolith (Static DB)
Status: IMPLEMENTED_BLOCKED_BY_EXTERNAL_WORLD_DELETE

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
Solution: Runtime now requests Data Monolith payload, telemetry ring, and cursor buffers from `GlobalDataVault` using local BufferID constants `71103`, `71104`, and `71105`; only when the vault is absent does it allocate a fallback NativeArray. File hydration uses memory mapping when available and a direct `FileStream.Read(Span<byte>)` path otherwise.
Rejected Alternatives: Keep private persistent `NativeArray<byte>` as the normal path; rejected under Vault Law because persistent buffers must be owned by the boot memory authority.
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
Rejected Alternatives: Restoring the file from HEAD to make my build pass; rejected because SHINOBU_103 has no ownership of the MapMagic vegetation bridge and blind restoration could erase an intentional World-domain refactor. Removing the `Compile Include` from `Hecton8.Core.csproj` was also rejected because the project file may be Unity-generated and the authoritative fix belongs to the World owner/integrator.
Scalability potential: Not runtime-facing; preserves one-owner/one-route discipline so Data Monolith does not mutate World architecture to hide a compile gate.
Hardware Impact: 0 us/frame. The failed build consumed about 68 s wall time once under CPU guard; no further build attempts are justified until the missing World source/project reference conflict is resolved.

# Rationale_CORE_DATA_MONOLITH

## Intake
Problem: Static gameplay data is scattered across managed MonoBehaviour and ScriptableObject surfaces, creating cache misses and runtime balance drift.
Solution: Build a binary Data Monolith surface with Persistent NativeArray-backed arena, blittable records, checksum gate, editor-only source compiler, and zero-GC runtime accessors.
Rejected Alternatives: ScriptableObject hot-path reads, JSON/CSV runtime parsing, managed dictionaries as runtime truth.
Scalability potential: Low uses compact LUTs and direct hashes; Middle uses broader tables; High keeps richer sections resident; Ultra can afford denser authored tables while preserving the same binary API.
Hardware Impact: Target removes managed parse/load work and random object graph reads on i3/MX350; exact gain pending build and profiler evidence.

STATUS: PENDING VERIFICATION

## Loop 1 Decisions
Problem: Existing arena was exact blob size, so the monolith did not satisfy the fixed reserve requirement and spare capacity could corrupt checksum if counted.
Solution: Allocate at least `H8DataLayoutConstants.DefaultArenaCapacityBytes` as Persistent native memory while tracking `_residentBlobBytes` for checksum, directory validation, and public byte length.
Rejected Alternatives: Treating `_arena.Length` as payload length; this breaks 10MB reserve. Keeping exact-file allocation; this fails the monolith reserve objective.
Scalability potential: Low/Middle retain 10MB reserve; High/Ultra can store richer static sections in the same API without reallocating callers.
Hardware Impact: i3/MX350 avoids runtime object graph fetches and keeps static data contiguous; estimated hot lookup savings ~2-40 us depending former call site.

Problem: Disk load path copied in stream windows, not a single visible guarded blit into unmanaged arena.
Solution: Read file once during boot into a managed staging byte array, then use `UnsafeMemoryCopyGuard.TryMemCpy`, which wraps `UnsafeUtility.MemCpy`, into the Persistent arena.
Rejected Alternatives: BinaryReader/read-loop deserialization; runtime CSV/JSON parse; per-record copy.
Scalability potential: Low spends boot-only allocation to buy zero-GC runtime; Ultra can ship larger baked tables without changing consumer code.
Hardware Impact: Boot-only managed staging is acceptable; no gameplay frame cost on MX350.

Problem: Core build was blocked by unrelated `HectonHardwareProfile` constructor drift after another agent added `hardwareScore`.
Solution: Patched two call sites to pass `0` for the new field and preserved existing math precision routing.
Rejected Alternatives: Reverting unrelated files; changing public API; ignoring failed compile.
Scalability potential: Preserves the new hardware-score slot for future quality routing.
Hardware Impact: No frame cost; compile restored.

Problem: Loop 2 compile check is blocked by missing tracked file `Assets/_Project/Scripts/SaveBinaryStorageNativeArrayExtensions.cs`, referenced by `Hecton8.Core.csproj`.
Solution: Treat as external dependency blocker. Do not restore or recreate another agent/user deletion without authority.
Rejected Alternatives: `git checkout --` of a user/agent deletion; removing the compile include from generated project; writing a fake save-system stub outside the Data Monolith domain.
Scalability potential: None; dependency hygiene issue.
Hardware Impact: No runtime impact from this agent. Build verification is blocked until the owning agent restores or intentionally removes the save extension file and its project reference.

## Loop 2-4 Decisions
Problem: Creature ecology needed a compact genome surface, but the existing creature record stored loose floats.
Solution: Embed `H8CreatureGenomeTraitBlock` as a 32-byte blittable subrecord inside the 64-byte creature record and update the Burst SoA job to read through that block.
Rejected Alternatives: Separate variable genome section; managed class trait bundle; ScriptableObject genome reads.
Scalability potential: Low reads only core genome floats; Middle/High/Ultra can add richer downstream visual behavior from the same compact species record.
Hardware Impact: i3/MX350 receives fewer cache lines per species scan; estimated ~2-8 us saved per 1K trait reads.

Problem: Recipe masks were four `uint` lanes, while the assignment required `ulong` bitmasks.
Solution: Change item/recipe ABI to two `ulong` lanes and add a dedicated FNV-hash-to-128-bit mask helper.
Rejected Alternatives: String ingredient sets, `List<uint>` recipes, keeping four `uint` lanes as primary API.
Scalability potential: Low/Middle perform O(1) mask tests; High/Ultra can layer richer UI/FX after the same authoritative result.
Hardware Impact: Fewer comparisons and less branch work on craft checks; estimated ~4-20 us saved per craft batch.

Problem: Biome heatmap needed O(1) reads without MapMagic.
Solution: Normalize authoring rows into a dense 256x256 baked section and add direct-index runtime lookup.
Rejected Alternatives: Runtime MapMagic query, sparse dictionary lookup, procedural height/biome recomputation.
Scalability potential: Low uses coarse stable LUT; Ultra can bake richer biome variants while preserving direct reads.
Hardware Impact: Avoids terrain subsystem query on MX350/i3; estimated ~5-50 us saved per lookup depending former path.

Problem: Loot CDF used floats, conflicting with deterministic slot-machine mandate.
Solution: Sort loot rows by table/item hash and bake integer cumulative weights; runtime resolves with binary search and caller-supplied ranged threshold.
Rejected Alternatives: UnityEngine.Random, `System.Random`, float threshold CDF, unsorted authoring-order authority.
Scalability potential: Low uses compact integer tables; Ultra can increase table richness without changing deterministic replay semantics.
Hardware Impact: Avoids float drift and full-table scans; estimated ~2-15 us saved per roll on medium tables.

Problem: Static data consumers needed direct LUTs for voxel, audio, pressure, hull, and physics material data.
Solution: Add hash-keyed binary search accessors and pressure nearest-sample LUT lookup on the resident native blob.
Rejected Alternatives: per-consumer duplicated tables, string Addressables event IDs, runtime pressure formulas.
Scalability potential: Low uses minimal sections; High/Ultra can ship denser tables and visual/audio overkill through same binary section contract.
Hardware Impact: Removes SO/managed dictionary pressure; estimated ~1-12 us saved per lookup family.

Problem: Hot reload requirement explicitly called for `FileSystemWatcher`; existing path was AssetPostprocessor/socket-based.
Solution: Add editor-only `FileSystemWatcher` over `Assets/_SourceData`, queueing changes via `Interlocked` and draining on `EditorApplication.update` before bake/hot-reload.
Rejected Alternatives: touching Unity API from watcher thread, manual menu-only bake, runtime source parsing.
Scalability potential: No runtime scalability impact; authoring iteration improves across all tiers.
Hardware Impact: Editor-only. Player builds unaffected.

Problem: Core compile has repeatedly surfaced unrelated blockers after Data Monolith edits compile far enough to reach external systems.
Solution: Mark Omega compile as `[BLOCKED BY DEPENDENCY]` and continue Data-domain documentation/reporting. Earlier blockers were `PlayerFootstepAudio` and `SubmarineFluidDynamics`; the latest blocker is `HectonFloatingOrigin.PublishAupShiftSignal`.
Rejected Alternatives: fake cross-domain stubs, blind gameplay/vehicle/floating-origin patches, reverting unrelated user/agent changes.
Scalability potential: None.
Hardware Impact: None from this agent; build proof remains pending external owners.

## OMEGA POLISH CHANGES
Problem: The biome heatmap accessor still carried a sparse-table fallback scan after the compiler normalized a dense 256x256 table.
Solution: Removed the fallback scan and made the runtime path direct-only through `(clampedY << 8) + clampedX`, with coordinate validation to fail fast on corrupt or stale blobs.
Rejected Alternatives: Allowing old sparse blobs to scan 65,536 cells; querying MapMagic from gameplay; rebuilding biome math at runtime.
Scalability potential: Low/Middle get one predictable cache-line read; High/Ultra can bake richer biome hashes into the same dense table without increasing lookup complexity.
Hardware Impact: i3/MX350 avoids worst-case 65,536-record scan. Estimated saved cost on bad/mismatched blobs: ~20-100 us per failed lookup; normal dense path remains ~sub-1 us.

Problem: Loot table range discovery found one matching CDF row and then expanded linearly to the start/end of the table.
Solution: Replaced edge expansion with binary lower/upper-bound searches, then kept the CDF item search binary. Table lookup is now O(log N) without a linear table-width tail.
Rejected Alternatives: Managed dictionary of table ranges; per-table arrays; accepting O(k) expansion because typical tables are small.
Scalability potential: Low keeps compact deterministic CDF rows; Ultra can ship larger loot tables without changing caller cost class.
Hardware Impact: i3/MX350 avoids branchy range walks on large tables. Estimated saved cost: ~1-8 us on medium tables, higher on oversized authoring errors.

Problem: Omega audit required identifying honest calculations that can become cinematic cheats or LUTs.
Solution: Confirmed Data Monolith owns static data, not visual simulation. Cheats used here are data-domain substitutes: depth pressure is a 256-sample LUT instead of per-query `math.pow`; biome lookup is a 1D dense LUT instead of terrain/MapMagic query; recipes are two `ulong` bitmasks instead of set/string checks; loot randomness is integer CDF over a caller-supplied deterministic threshold instead of float random math.
Rejected Alternatives: runtime pressure formula, terrain biome query, string recipe lists, Unity random.
Scalability potential: Low uses direct hash/LUT reads only; Middle increases authored row count; High/Ultra can spend saved CPU on richer downstream visuals/audio while static lookup APIs remain fixed.
Hardware Impact: Core data lookup work stays under the 0.1 ms suspicion threshold when called in ordinary batches. Exact profiler-backed microseconds are unavailable because Core build remains dependency-blocked.

Problem: Omega zero-GC purge required a final scan for managed hot-path debt.
Solution: Scanned runtime Data Monolith files for `foreach`, LINQ, `string.Format`, interpolated strings, `.ToString()`, `math.pow`, `sqrt`, `normalize`, and `Vector3.magnitude`; no matches remain in owned runtime files. Managed `new` usage remains boot-only native arena allocation, file staging, or struct initialization; editor compiler allocations are isolated under the editor assembly path.
Rejected Alternatives: moving editor compiler scratch into runtime; keeping managed strings as resident data.
Scalability potential: Same runtime memory behavior across Low through Ultra; higher tiers only carry larger resident blobs.
Hardware Impact: Hot path remains 0 B/frame by code inspection.

Problem: Omega silo audit found shared Core/Bootstrap files dirty in the workspace.
Solution: Documented the only justified cross-domain touch: constructor compatibility for `HectonHardwareProfile` after another agent's API drift blocked compile. Current tracked diff in `GameBootstrapper.cs` and `GlobalRegistryContracts.cs` contains other agents' wider changes and is not claimed as Data Monolith ownership.
Rejected Alternatives: reverting other agents' dirty work; patching unrelated AUP/floating-origin compile blocker; creating fake cross-domain stubs.
Scalability potential: Hardware profile field remains available for quality routing.
Hardware Impact: No Data Monolith frame cost.

Problem: Omega build health check is still blocked after the polish patch.
Solution: Ran `dotnet build Hecton8.Core.csproj` on 2026-05-11. Build restored dependencies far enough to expose the latest external blocker: `Assets/_Project/Scripts/HectonFloatingOrigin.cs(620,17): CS0103 The name 'PublishAupShiftSignal' does not exist in the current context`.
Rejected Alternatives: editing floating-origin/AUP code outside assigned domain; removing project references; reporting success.
Scalability potential: None until owner resolves dependency.
Hardware Impact: None from this agent. Status remains PENDING VERIFICATION despite the Polish tag requesting VERIFIED MASTER GRADE, because the agent assignment explicitly requires PENDING VERIFICATION and the build is not clean.

Final Git Diff / Workspace Evidence:
- `?? Assets/_Project/Scripts/Data/Monolith/` created/updated: runtime binary arena, data ABI, FNV hashing, Burst SoA jobs.
- `?? Assets/_Project/Scripts/Editor/DataMonolith/` created/updated: editor CSV/JSON to `.h8bin` compiler and FileSystemWatcher hot reload.
- `?? Docs/Tasks/Status_CORE_DATA_MONOLITH.md`
- `?? Docs/AgentLogs/Rationale_CORE_DATA_MONOLITH.md`
- `M Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs` contains shared dirty diff; Data Surgeon only justified compile-compatibility touch.
- `M Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` contains shared dirty diff from multiple agents; not owned as Data Monolith implementation.
- Runtime audit command found no banned hot-path patterns in `Assets/_Project/Scripts/Data/Monolith/*.cs`.

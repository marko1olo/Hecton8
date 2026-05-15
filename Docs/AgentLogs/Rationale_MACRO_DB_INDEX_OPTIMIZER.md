# Rationale_MACRO_DB_INDEX_OPTIMIZER

## Session Initialization

Problem: Wired storage prompt requires `.h8db` index/RLE work but the authoritative `C:\hades\Hecton8\AGENTS.md` path is absent in this checkout.  
Solution: Use local `C:\Hecton8\AGENTS.md`, the extracted XML prompt, `.agents-skills`, and stable `Docs` as authority. Treat missing external path as an environment fact, not permission to guess.  
Rejected Alternatives: Blocking immediately on the missing `C:\hades` copy would leave the batch task unworked; using chat-pasted neighboring data as authority would violate strict parsing.  
Scalability potential: Low uses smaller resident index/cache and larger hydration hysteresis; Middle/High/Ultra expand cache radius and RLE audit detail without changing file compatibility.  
Hardware Impact: Prevents direct runtime guesswork; expected gain on i3/MX350 is hitch avoidance by keeping storage changes in cold tools/spec or existing service code only.

Problem: Macro DB task crosses save, voxel delta, AUP hashing, streaming cache, and telemetry concerns.  
Solution: Load 2-8+ relevant mandates, prioritizing persistence, voxel delta schema, zero-GC, AUP, deterministic hashing, streaming residency, and telemetry.  
Rejected Alternatives: Reading every mandate would waste context and increase chance of cross-domain contamination. Reading none would violate batch protocol.  
Scalability potential: Mandates force tiered cache and quantization decisions instead of one middle-ground setting.  
Hardware Impact: On low-end silicon, mandated page/RLE layout reduces MicroSD seek count and decompression bytes; on high-end, saved cycles fund larger hydration radius and richer voxel AO payloads.

## Implementation Decisions

Problem: `.h8db` node payload arrays are not all 16-byte array-start aligned because `NodeMaxKeys=169`; blindly changing to 168 would corrupt the current B-tree split invariant.  
Solution: Keep v1 node geometry intact, define explicit node/payload alignment constants, document the 16-byte tail padding, and optimize lookup with lower-bound binary search inside nodes.  
Rejected Alternatives: Format bump to v2, changing `NodeMaxKeys`, or raw layout reshuffle. Those would invalidate existing files and need migration proof outside this task.  
Scalability potential: Low tier keeps the same compact 4KB node reads; High/Ultra benefit from the same ABI with fewer CPU comparisons during broader prefetch.  
Hardware Impact: Worst-case per-node search drops from 169 comparisons to 8, saving up to 161 key comparisons per node visit; on i3/MX350 this reduces main-thread hydration stalls during MicroSD misses.

Problem: Macro payload cache returned failure when full, which can cause repeated disk hydration misses instead of shedding old clean payloads.  
Solution: Add zero-managed-GC LRU metadata in `GlobalDataVault` using `NativeParallelHashMap<ulong,uint>` access ticks. On full store, evict the oldest clean payload and protect dirty payloads.  
Rejected Alternatives: Managed `LinkedList`/`Dictionary` LRU, expanding the public cache interface, or evicting dirty payloads. Managed containers violate hot-path policy; dirty eviction risks data loss.  
Scalability potential: Low uses small capacity with clean LRU churn; Middle/High/Ultra widen capacity and hydration radius without changing contract.  
Hardware Impact: Expected low-end gain is fewer failed cache stores and fewer duplicate MicroSD reads. CPU cost is O(cache entries) only on full-cache store, bounded by configured cache capacity.

Problem: The prompt requires a 10M sector hash collision simulation, but pure Python set simulation exceeded the tool timeout.  
Solution: Keep the exact same 64-bit hash formula and use NumPy vectorized generation, sort, and adjacent-equality collision count. The run completed with `observed_collisions=0`.  
Rejected Alternatives: Reporting only the birthday bound or running a smaller sample. That would not satisfy the prompt's simulation requirement.  
Scalability potential: Low tooling path can use smaller samples; CI/high-end can run the full 10M sample quickly with NumPy.  
Hardware Impact: Offline only. Runtime impact is zero; evidence quality improves without touching Unity frame time.

Problem: RLE task asks for a 4-bit-style 50% saving, but current `SaveVoxelDeltaRun8` spends only one byte on SDF density.  
Solution: Quantify the real math: 4-bit saves 50% of the density lane but only 6.25% of an unchanged 8-byte run. Keep 8-bit for LOD0/LOD1; allow 4-bit narrow-band only for LOD2+ visual RLE after hysteresis.  
Rejected Alternatives: 4-bit full-range SDF for all chunks. It creates ~0.533m max error across +/-8m and is not "0% visual loss" for player-facing mesh or collision truth.  
Scalability potential: Low/MX350 can use 4-bit far visual RLE for distant impostors; Ultra keeps 8-bit near-field and spends saved far bandwidth on denser lighting/AO.  
Hardware Impact: Low-end disk/CPU relief applies only to far visual payloads. Near-field correctness remains stable.

Problem: Page fault feedback objective was initially document-only.  
Solution: Add a rolling page-fault window in `H8MacroDatabaseService`; if the previous one-second window exceeds two faults, the active tier hydration radius grows by one sector size and dehydrate radius follows.  
Rejected Alternatives: Increasing native cache capacity first or forcing synchronous broad hydration. Cache growth spends RAM; sync hydration creates the MicroSD hitch this task is eliminating.  
Scalability potential: Low gets conservative one-sector increments; High/Ultra can absorb larger radii through existing tier radii and cache capacity.  
Hardware Impact: On i3/MX350, the likely gain is fewer visible stalls after repeated page faults, paid for by bounded extra prefetch.

Problem: A flat RLE world file is tempting because VoxelDelta payloads compress well locally.  
Solution: Keep B-tree as the sector locator and RLE as the payload codec. B-tree bounds sparse sector lookup for a 100km world; RLE is efficient only after the sector is found.  
Rejected Alternatives: Flat append-only RLE with linear scan, or one monolithic RLE stream. Both force O(n) search or require a second index that recreates the B-tree problem.  
Scalability potential: Low reads a few indexed sectors around the player; Ultra widens prefetch and can retain more sectors without changing lookup semantics.  
Hardware Impact: B-tree lookup prevents MicroSD scan hitches; flat RLE would trade smaller bytes for worse seek/search behavior at world scale.

## Continuation Hardening

Problem: The first pass had no C# compile proof because no local Unity/.NET project build surface was found.  
Solution: Search exact Unity/Visual Studio/.NET paths. Roslyn and .NET Framework `csc.exe` exist, but both hung with no diagnostics on source probes; Unity editor, generated Unity assemblies, `.sln`, `.csproj`, and `dotnet` remain absent. Add explicit static guards instead of claiming compile success.  
Rejected Alternatives: Calling the hung compiler a pass, or generating fake project files with incomplete Unity stubs. Both would create false evidence.  
Scalability potential: Compile verification should be run in the real Unity project environment; static guards remain cheap on low-end machines and CI.  
Hardware Impact: No runtime cost. Evidence quality improves because the build wall is precise rather than vague.

Problem: Static text checks can miss syntax faults if they are too shallow.  
Solution: Add a custom source guard over owned files: delimiter scanning with comment/string handling, B-tree lower-bound token checks, page-fault counter uniqueness, LRU lifecycle token checks, and managed-container/LINQ hot-path rejection.  
Rejected Alternatives: Only using `rg` grep. That catches policy smells but not structural delimiter errors.  
Scalability potential: Guard is offline and can be rerun on any hardware; High/CI can still run Unity compile afterward.  
Hardware Impact: Zero runtime impact; catches editor-blocking mistakes before Unity import.

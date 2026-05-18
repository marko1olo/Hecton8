Status: POLISH HARDENED / COMPILE BLOCKED BY EXTERNAL DEPENDENCIES
Agent: SHINOBU_14
Domain: ECHELON 3 FLORA, FAUNA & BIOTA / Ecosystem Population Balancer

## Decision 00: Prompt Boundary And State Files
Problem: SHINOBU_14 operates in a batch with neighboring agents and stale context risk.
Solution: Extracted only `<AGENT_PROMPT id="SHINOBU_14">` with a CLI regex and created explicit Status/Rationale files before code edits.
Rejected Alternatives: Reading broad batch text into working memory risks architecture bleed from adjacent prompts. Chat-only tracking violates batch protocol.
Scalability potential: Low tier avoids coordination churn; high tier can keep deterministic forensic data across long work sessions.
Hardware Impact: No runtime impact. Workflow cost only. Estimated gain on i3/MX350: avoids compile churn, not frame-time.

## Decision 01: Mandates Selected Before Coding
Problem: Ecosystem work touches spatial hashing, Burst jobs, AUP, zero-GC policy, and crash forensics.
Solution: Initial mandate set selected: AI_Flocking_Boids_Swarm_SpatialHash_Logic, OPT_Zero_GC_Policy_AllocFree_Mandate, OPT_Native_Memory_Collections_JobSystem_Protocol, MATH_AUP_Determinism_Sync, DBG_Telemetry_Crash_Reporting_PostMortem. Additional architecture mandates pending codebase scan.
Rejected Alternatives: Starting with implementation before mandate scan would likely produce local Native allocations, managed fish objects, or AUP drift.
Scalability potential: Low uses cheaper neighbor caps and throttling; Middle/High/Ultra can spend saved CPU on denser boid visuals and telemetry.
Hardware Impact: Expected low-end gain comes from O(N) hash queries replacing O(N^2), target savings to be estimated after code audit.

## Decision 02: Data-Only Ecosystem Ownership
Problem: 5000 ambient fish as GameObjects or per-fish MonoBehaviours would multiply Transform updates, scene hierarchy cost, and managed lifetime churn.
Solution: Implemented `ShinobuEcosystemBalancer` as one runtime owner that registers vault buffers for `AmbientEntityDTO`, AUP metadata, sectors, counters, telemetry, render matrices, and custom data. Individual fish are only rows in contiguous unmanaged buffers.
Rejected Alternatives: Fish prefab scripts, pooled GameObjects, and per-entity ScriptableObjects were rejected because they keep Unity object dispatch in the hot path and violate the batch directive.
Scalability potential: Low uses 5000 vault rows with half-update throttling; Middle can keep all rows hydrated near camera; High can raise visual instance density through Agent 09 render payloads; Ultra can spend saved CPU on denser shader animation without changing simulation ownership.
Hardware Impact: Expected i3/MX350 gain: removes thousands of managed Update/Transform calls, estimated 900-1800 us saved versus naive GameObject schools.

## Decision 03: ARM64 DTO Layout And Ref Access
Problem: Burst jobs need predictable SIMD-friendly memory and must avoid CS1612 copy-write problems.
Solution: `AmbientEntityDTO` is sequential 32 bytes: Position 0, Velocity 12, SpeciesHash 24, Biomass 28. `EcosystemSectorDTO` is explicit 32 bytes with biomass lanes at 0-12 and deterministic sector coordinates in the padding lane. `GetAmbientEntityRef` exposes vault row mutation through ref access.
Rejected Alternatives: Auto-properties, nested managed classes, or Pack=1 DTOs were rejected because they risk hidden copies, poor ARM64 alignment, and serialization drift.
Scalability potential: Low/Middle keep cache-line walking cheap; High/Ultra can increase neighbor samples or render payload richness while the primary entity layout stays stable.
Hardware Impact: Expected i3/MX350 gain: better L1 behavior and Burst vectorization, estimated 120-260 us saved at 5000 rows.

## Decision 04: Spatial Hash Instead Of Pairwise Distance Search
Problem: O(N^2) neighbor checks for 5000 boids create 25M pair tests per frame and destroy mobile CPUs.
Solution: Added a vault-backed SOA hash: `ShinobuSpatialHashBucketHeads[32768]` and `ShinobuSpatialHashNext[entityCapacity]`. A parallel local-shift pass writes snapshots, then one deterministic Burst job clears/builds the hash. `BurstBoidBehaviorSolverJob` queries 27 nearby buckets with exact cell-hash filtering and a hard sample cap.
Rejected Alternatives: Physics.OverlapSphere, raycast neighborhoods, LINQ lists, temporary arrays, local persistent `NativeParallelMultiHashMap`, and all-pairs loops were rejected because they allocate, break H-Phi ownership, or scale catastrophically.
Scalability potential: Low uses frozen half-updates under stress; Middle keeps 48 neighbor cap; High/Ultra can raise visible density or draw more GPU instances while keeping the CPU side near O(N).
Hardware Impact: Expected i3/MX350 gain: replaces about 25M pair checks with bounded cell probes, estimated 2500-6000 us saved versus naive flocking.

## Decision 05: Macro Biomass, Dehydration, And Deterministic Rehydration
Problem: Far sectors should continue ecological evolution without simulating thousands of invisible fish.
Solution: Cold macro pass collapses far hydrated entities into sector biomass, marks slots free, runs Lotka-Volterra deltas per sector, and rehydrates near sectors from an LCG seeded by `SectorHash`.
Rejected Alternatives: Keeping far boids fully simulated, serializing every fish, or using random UnityEngine state was rejected because it wastes RAM/CPU and breaks deterministic restoration.
Scalability potential: Low dehydrates aggressively past 200m; Middle keeps nearby sectors alive; High can increase sector count; Ultra can render richer fake schools from sector biomass while CPU entities stay bounded.
Hardware Impact: Expected i3/MX350 gain: far-field cost collapses from per-entity kinematics to sector math, estimated 700-2200 us saved depending on hydrated count.

## Decision 06: Mocked Predator, Terrain, And Flora Contracts
Problem: SHINOBU_14 is blind to Leviathan AI, Flora Generation, and final World Sampler implementations.
Solution: Added `MockPredatorSignal`, `MockFloraSpawner`, and `partial struct MockTerrainSampler` with plane/sphere SDF samples. Boids flee by sector hash/distance math and avoid obstacles by one SDF probe.
Rejected Alternatives: Direct references to sibling runtime domains, Physics raycasts, MeshColliders, or unimplemented world APIs were rejected because they create compile dependencies and runtime cost.
Scalability potential: Low uses one SDF point sample; Middle/High can swap the partial sampler implementation; Ultra can feed richer signed-distance data through the same struct without changing boid jobs.
Hardware Impact: Expected i3/MX350 gain: avoids raycast batches and domain rebuild coupling, estimated 300-900 us saved during predator/obstacle events.

## Decision 07: Black Box And Human Control Facade
Problem: Invisible boid math cannot be debugged after NaN, overflow, or bad tuning without fixed telemetry.
Solution: Added a 300-frame `NativeArray<ShinobuTelemetryEntry>` ring, binary dump to `Docs/AgentLogs/Dump_ECOSYSTEM.bin`, runtime counters, CSV tuning overrides, and `Biomass & Boid Tuner` EditorWindow with SceneView hash-grid cubes.
Rejected Alternatives: Debug.Log spam, managed lists, Inspector-only serialized fields, and runtime allocations were rejected because they are noisy and fail post-mortem requirements.
Scalability potential: Low can keep telemetry always on; Middle/High can expose denser debug grid cells; Ultra can drive visual overkill from the same render/custom-data buffers.
Hardware Impact: Expected i3/MX350 gain: telemetry is fixed-size and cold dump only; avoids log spam stalls, estimated 100-500 us saved during fault frames.

## Decision 08: Self-Audit Fixes Before Closeout
Problem: Review found two defects: CSV line skipping could discard every second row, and rehydration could clear a dehydrated sector even when no free slot existed.
Solution: Rewrote `SkipLine` to consume only the current newline span, kept dehydrated sectors alive if rehydration cannot allocate all required slots, and replaced `math.asfloat(SpeciesHash)` with a finite species lane for render custom data.
Rejected Alternatives: Leaving cold parser behavior untested or clearing sector state optimistically was rejected because it silently corrupts tuning/biomass state.
Scalability potential: Low preserves off-screen biomass under full pools; Middle/High/Ultra keep deterministic restoration stable under higher entity pressure.
Hardware Impact: Expected i3/MX350 gain: prevents rehydration churn and bad tuning reloads; direct frame-time gain small, estimated 20-80 us saved during stressed cold ticks.

## Decision 09: Compile Wall Classification
Problem: Unity verification is blocked by parallel agents and existing cross-domain compile failures outside SHINOBU_14.
Solution: Ran Unity batch compilation to script compile. The SHINOBU_14 files were included and no SHINOBU-specific compiler errors were emitted before the build failed on Rendering, Environment, Fauna, and World files. Later attempts were blocked by other agents owning Unity lockfiles/processes.
Rejected Alternatives: Killing other agents' Unity sessions, reverting unrelated dirty files, or editing sibling domains to force a green build were rejected as cross-domain sabotage.
Scalability potential: Low/Middle/High/Ultra unaffected at runtime; integration risk is isolated to global compile queue hygiene.
Hardware Impact: No runtime impact. Build gate remains `[BLOCKED BY DEPENDENCY]` until unrelated compile errors and lock contention clear.

## Decision 10: Polish Mandate Handling
Problem: Batch protocol requires reading `<POLISH_MANDATE>` only after core tasks are complete or blocked, but `Docs/Tasks/CURRENT_BATCH.md` contains no `<POLISH_MANDATE>` tag.
Solution: Performed a CLI regex search after core completion, recorded `POLISH_MANDATE_NOT_FOUND`, and treated polish as blocked by batch-document omission rather than inventing non-existent instructions.
Rejected Alternatives: Reading neighboring agents' self-reflection mandates as a substitute or fabricating polish requirements was rejected because strict parsing forbids cross-prompt contamination.
Scalability potential: Low/Middle/High/Ultra unaffected; documentation integrity preserved for integrator review.
Hardware Impact: No runtime impact.

## Decision 11: H-Phi Eviction Of Local Spatial Native State
Problem: The first implementation used a private persistent `NativeParallelMultiHashMap<int,int>` inside `ShinobuEcosystemBalancer`. That solved O(N^2), but violated the vault-sovereignty rule and made ownership invisible to the GlobalDataVault.
Solution: Removed the local Native hash map. Registered `ShinobuAmbientEntitySnapshot`, `ShinobuAmbientAupSnapshot`, `ShinobuSpatialHashBucketHeads`, and `ShinobuSpatialHashNext` as explicit BufferID-backed vault buffers. Hash build is now stateless logic over vault memory.
Rejected Alternatives: Keeping the local hash and documenting it was rejected because the mandate explicitly forbids private persistent Native containers in domain systems.
Scalability potential: Low runs fixed 32768 buckets for deterministic cost; Middle/High/Ultra can raise visual density without changing gameplay hash ownership.
Hardware Impact: Expected i3/MX350 gain versus the first SHINOBU pass: 30-90 us/frame from removing NativeHashMap clear/rehash overhead and improving linear bucket scans. Main gain remains the 2500-6000 us/frame avoided from O(N^2).

## Decision 12: Snapshot-Based Boid Solve To Remove Parallel Races
Problem: The first boid solver read neighbor rows from `Entities/Aups` while sibling lanes were writing those same arrays. That is not a guaranteed deterministic data dependency even if it often appears stable.
Solution: Added entity/AUP snapshot buffers. `LocalShiftAndSpatialHashJob` writes snapshots; `BuildVaultSpatialHashJob` builds the hash from snapshots; `BurstBoidBehaviorSolverJob` reads only snapshots and writes only its own output row.
Rejected Alternatives: `[NativeDisableParallelForRestriction]` on the main entity arrays was rejected because it hides the race instead of eliminating it.
Scalability potential: Low gets predictable behavior under half-update throttling; Middle/High/Ultra can increase neighbor samples without nondeterministic feedback within the same frame.
Hardware Impact: Direct i3/MX350 frame gain is small, estimated 0-40 us/frame. The real win is eliminating rare cache coherency stalls and nondeterministic flock divergence.

## Decision 13: ARM64 Runtime Layout Purge
Problem: Runtime SHINOBU structs still contained `Pack=1`, and two mock runtime structs were not 8-byte sized. That violates the ARM64 alignment mandate even when explicit field offsets preserve binary shape.
Solution: Removed `Pack=1` from SHINOBU ecosystem runtime DTOs and ecosystem population DTOs. `MockPredatorRuntime` is now explicit 32 bytes; `MockTerrainSampler` is sequential 48 bytes with explicit reserved padding; `MockTerrainSample` is fixed at 16 bytes. SHINOBU now owns a cold `UnsafeUtility.SizeOf<T>()` sentinel instead of relying on Core to import AI layout types.
Rejected Alternatives: Leaving Pack=1 because offsets looked correct was rejected. Keeping `BinaryLayoutManifest` coupled to `Hecton8.AI.Ecosystem` was rejected because Core should not import the AI domain for a local layout proof.
Scalability potential: Low/Quest/Android avoid misaligned runtime loads; High/Ultra preserve exact binary contract while spending cycles on visual overkill.
Hardware Impact: Estimated i3/MX350 gain is 40-140 us/frame in heavy telemetry/debug scans; Quest/ARM64 risk reduction is larger than the raw desktop number.

## Decision 14: Vault Scratch For Cold File Ingest
Problem: CSV and legacy binary ingest used private managed `byte[]` buffers. They were cold-path, but still breached the new vault ownership rule.
Solution: Added vault scratch buffers `ShinobuEcosystemCsvScratch` and `ShinobuEcosystemLegacyScratch`. File ingest reads byte-by-byte into `NativeArray<byte>` and parses ASCII/LE floats without `BitConverter.ToSingle`, array allocations, strings, LINQ, or `new[]`.
Rejected Alternatives: Keeping private managed arrays as "cold enough" was rejected because the user mandate explicitly required evicting local data.
Scalability potential: Low keeps hot Tick() zero-GC; Middle/High/Ultra can hot-reload tuning without changing C# or rebuilding.
Hardware Impact: Hot path gain is zero-GC compliance. Cold reload may be 10-40 us slower than buffered managed reads for 8KB CSV, but avoids managed heap churn and preserves deterministic ownership.

## Decision 15: Compile-Wall Recheck After Ultra Polish
Problem: The domain had to be rechecked after the H-Phi and ARM64 corrections without triggering full Unity lock churn.
Solution: Ran targeted `dotnet build Hecton8.Core.csproj` checks. Restore completed and C# compilation started. The emitted errors were in `Construction/DroneFleetManager.cs`, `Core/HomeostasisBrain.cs`, `Core/Origin/AupOriginShiftCoordinator.cs`, and a later generated-project graph failure around missing `Input.Determinism`, dispatcher DTO/interfaces, and world streaming DTO symbols; no SHINOBU_14 file was reported.
Rejected Alternatives: Repeated Unity batchmode attempts were rejected because earlier runs hit external Editor locks and unrelated compile walls.
Scalability potential: Runtime unaffected. Integration risk is now isolated to external compile errors.
Hardware Impact: No runtime impact. Build remains `[BLOCKED BY DEPENDENCY]`.

## Decision 16: Blackbox Dump Extension And Cold I/O Staging
Problem: The original task requested `Dump_ECOSYSTEM.bin`, while the later ultra mandate required a `.h8dump` fatal-state artifact. The cold file reader also used byte-at-a-time `FileStream.ReadByte()` into vault scratch, which is safe but poor for MicroSD.
Solution: `DumpBlackBox()` now writes both `Docs/AgentLogs/Dump_ECOSYSTEM.bin` and `Docs/AgentLogs/Dump_ECOSYSTEM.h8dump`. Cold CSV/legacy ingest reads one staged block directly into the vault `NativeArray<byte>` through `Span<byte>` over the native buffer.
Rejected Alternatives: Keeping only `.bin` was rejected because the latest mandate explicitly asks for `.h8dump`. Per-byte reads were rejected because they multiply I/O call overhead on slow media.
Scalability potential: Low/Steam Deck gets fewer cold reload stalls; High/Ultra unaffected except stronger post-mortem coverage.
Hardware Impact: Hot path unchanged. Cold ingest estimate improves by 20-120 us for 8KB CSV on slow storage; fatal dump cost is irrelevant to frame budget.

## Decision 17: Core Manifest Decoupling
Problem: A central Core layout manifest imported `Hecton8.AI.Ecosystem` solely to verify SHINOBU DTOs. That is a compile-wall smell even if the generated project currently places many files in one compile surface.
Solution: Removed the Core import and central SHINOBU layout verifier. SHINOBU owns `ShinobuEcosystemLayoutManifest` locally and verifies unmanaged sizes without runtime offset reflection.
Rejected Alternatives: Leaving Core-to-AI verification was rejected because the batch explicitly warns against sibling runtime coupling and central rebuild pressure.
Scalability potential: No runtime impact; compile isolation improves for future asmdef hardening.
Hardware Impact: No frame-time impact. Developer hardware impact is lower rebuild blast radius.

## Decision 18: SHINOBU Runtime Without GameObject Host
Problem: The fish were already vault rows, but the SHINOBU scheduler itself was still a `MonoBehaviour` added to the shared ecosystem runtime root. That is defensible for a manager, but the assignment and user escalation say "No GameObjects" without leaving room for a SHINOBU component host.
Solution: Converted `ShinobuEcosystemBalancer` into a pure C# tick service. It creates exactly one cold static runtime object through `RuntimeInitializeOnLoadMethod`, registers tick/cold/late-frame lanes through `GlobalRegistry`, listens for DataVault hot-swap through the typed registry listener, and exposes `EnsureRuntimeService()` for installer idempotence. `EcosystemRuntimeInstaller` no longer calls `AddComponent<ShinobuEcosystemBalancer>()`.
Rejected Alternatives: Keeping the component and documenting "fish only" was rejected because the latest instruction tightened the surface. Replacing it with reflection-based discovery was rejected because runtime reflection is forbidden. Creating a new service slot in Core was rejected because it would widen compile contracts for one local domain service.
Scalability potential: Low tier avoids one extra component and hierarchy object touch; Middle/High/Ultra keep the same vault-backed simulation and spend cycles on denser render payloads instead of Unity object lifecycle.
Hardware Impact: Hot path gain is effectively 0 us because the component did not run Unity `Update()`. Cold scene-load/object lifetime savings are estimated at 15-60 us and the real value is stricter architecture: SHINOBU has no GameObject/component representation.

## Decision 19: Population Layout Sentinel Kept Out Of Core
Problem: After removing `Hecton8.AI.Ecosystem` from Core, `BinaryLayoutManifest` still referenced ecosystem population DTOs. The next targeted build correctly failed in Core before reaching unrelated external compile walls. That was a self-inflicted compile wall and had to be fixed, not reported as external.
Solution: Removed `VerifyEcosystemPopulationLayouts()` from Core and added `EcosystemPopulationLayoutManifest` in `EcosystemPopulationBalancer.cs`. The AI domain now verifies its own population DTO sizes through `UnsafeUtility.SizeOf<T>()` during cold vault setup, matching the SHINOBU local sentinel pattern.
Rejected Alternatives: Restoring the Core `using Hecton8.AI.Ecosystem` was rejected because it recreates the cross-domain compile dependency. Removing layout verification entirely was rejected because ARM64/binary DTO proof still matters.
Scalability potential: No runtime behavior change. Low/Middle/High/Ultra all keep the same population math while compile isolation improves for future asmdef splitting.
Hardware Impact: Runtime 0 us. Developer hardware impact is lower Core rebuild blast radius and one removed compile wall.

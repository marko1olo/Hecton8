# Rationale_SHINOBU_42

Agent: SHINOBU_42
Domain: BIOMIMETIC_ARCHITECTURE_DIRECTOR / POI Sculptor
Status: POLISH PASS 18 TEMP COMPILE PASS / UNITY IMPORT PENDING

## Decision 000 - Matrix-Only POI Authority
Problem: The batch requests bases, factories, and POIs without runtime GameObject instantiation. The known failure is floating bases or bases embedded in terrain.
Solution: Author the system as unmanaged DTO streams and Burst jobs that write placement matrices, stilt matrices, HLOD records, masks, and telemetry. Runtime hydration remains outside this domain.
Rejected Alternatives: Direct `GameObject.Instantiate`, `NetworkServer.Spawn`, prefab sampling, and terrain mesh reads were rejected because they violate the matrix-only prompt, zero-GC mandate, and cross-agent streaming boundary.
Scalability potential: Low uses sparse main silhouettes and cheap gradient sampling; middle adds debris and masks; high increases debris density and anchor samples; ultra increases localized detail while keeping authority DTOs compact.
Hardware Impact: Expected low-end i3/MX350 gain is elimination of per-POI GameObject churn and prefab construction stalls; exact microseconds require Unity profiler evidence.

## Decision 001 - Mock-First Geology Boundary
Problem: Agent 41 terrain SDF and base prefabs are not finalized, but SHINOBU_42 must compile and prove gradient adaptation.
Solution: Add `MockGradientSampler`, `MockGeologySignal`, and `MockPrefabBounds` data that feed the same unmanaged placement jobs used by real geology data later.
Rejected Alternatives: Waiting for Agent 41 or adding concrete direct references to geology classes was rejected because 20+ agents run in parallel and AGENTS.md requires GlobalRegistry/interfaces/EventBus boundaries only.
Scalability potential: Mock data validates low-to-ultra math paths without committing to final terrain implementation.
Hardware Impact: Avoids blocked integration and lets compiler/static verification catch DTO/job defects before real terrain lands.

## Decision 002 - DTO Layout and CS1612 Avoidance
Problem: Mutable POI array elements must be updated by jobs without C# property copy traps or ARM64 misalignment.
Solution: `PoiTransformDTO` is explicit size 64: offset 0 `double3 AUP` 24b, offset 24 `quaternion Rotation` 16b, offset 40 `float3 Scale` 12b, offset 52 `uint PrefabHash` 4b, offset 56 `uint BiomeID` 4b, offset 60 `uint QuestNodeHash` 4b. `StructuralBoundsDTO` is explicit size 32: offset 0 `float3 Extents`, offset 12 `float3 CenterOffset`, offset 24 `float ClearanceRadius`, offset 28 `uint _pad0`. `PoiTransformBufferRef.ElementAt()` returns a mutable ref into unmanaged storage.
Rejected Alternatives: Auto-layout structs, `Pack=1`, DTO properties, and per-element copy/assign facades were rejected because they either violate the ARM64 mandate or preserve CS1612-style mutation hazards.
Scalability potential: Low through ultra all stream the same 64-byte authority record; high and ultra add visual payloads in adjacent lanes instead of bloating base truth.
Hardware Impact: Expected low-end i3/MX350 benefit is sequential 64-byte reads for placement records and no heap churn; exact microseconds require Burst/Profiler evidence.

## Decision 003 - Loop 1 Compile Wall Classification
Problem: After Tasks 01-05, `dotnet build Hecton8.Core.csproj` failed before a clean project compile could be claimed.
Solution: Filtered compiler output showed errors in pre-existing `Assets/_Project/Scripts/World/GlobalWorldSampler.cs` (`_forceLow`, `TryResolveProbeBuffersExtended`) and full `Assembly-CSharp.csproj` additionally failed on missing RealtimeCSG vendor files. No SHINOBU-owned file errors appeared in the filtered output.
Rejected Alternatives: Editing `GlobalWorldSampler` or RealtimeCSG was rejected because those are outside SHINOBU_42 domain and not required for the POI architecture slice.
Scalability potential: Continue in isolated SHINOBU files while preserving compile-wall evidence for the Integrator.
Hardware Impact: No runtime impact from the compile wall classification; build verification remains PENDING VERIFICATION until unrelated dependencies are repaired.

## Decision 004 - Spatial Syntax Before Scatter
Problem: Random POI placement creates visual noise and fails the prompt requirement for deliberate NASA-Punk architectural composition.
Solution: `PoiPlacementJob` scores each candidate with a 3x3 mock terrain-gradient grid. It rewards flat center samples adjacent to higher-gradient edge samples, records `VisualAnchorSampleDTO`, and emits only accepted matrix rows.
Rejected Alternatives: Pure RNG scatter, manual artist anchors, managed scene markers, and renderer-time placement were rejected because they fail determinism, zero-GC, or offline-bake requirements.
Scalability potential: Low lowers the acceptance threshold and keeps main silhouettes sparse; middle keeps stricter visual anchors; high and ultra can increase candidate density and anchor-sample density without changing the DTO contract.
Hardware Impact: Expected i3/MX350 gain is zero runtime placement work after bake; exact cold-pass microseconds require Unity Profiler/Burst Inspector.

## Decision 005 - Level Floors With Projected Stilts
Problem: The known failure mode is bases floating over terrain or intersecting rock, especially on steep slopes.
Solution: Base center height is terrain height plus structural half-height plus clearance. Rotation keeps the floor up-vector aligned to gravity, derives yaw from the terrain-normal cross product, and emits four titanium stilt DTOs when slope exceeds the rule cosine.
Rejected Alternatives: Tilting the whole floor into the slope, Physics.Raycast, MeshCollider sampling, and runtime support-object spawning were rejected because they hurt gameplay readability and violate matrix-only generation.
Scalability potential: Low keeps four coarse stilts; middle can add debris/contact masks; high and ultra spend saved cycles on denser support silhouettes while keeping the same base matrix truth.
Hardware Impact: Prevents visual correction work in streaming/hydration and avoids physics queries on low-end i3/MX350; exact savings remain profiler-pending.

## Decision 006 - Dear-Lie HLOD, Debris, And Negative Space
Problem: Distant complex POI clusters and dense debris can crush rendering, while overly dense POIs destroy isolation pacing.
Solution: Added `PoiDearLieHlodClusterJob` for 50m far-cluster impostors, `DebrisScatterJob` for deterministic curl-current ruin fields, and `NegativeSpacePoiCullJob` for 2000m major-POI alive masks.
Rejected Alternatives: Drawing every child matrix at distance, physics-settled debris, random culling, and runtime collider overlap tests were rejected as slower and less controllable.
Scalability potential: Low quality emits few or zero debris rows while retaining silhouettes; middle adds readable ruin trails; high increases debris density; ultra can use dense localized visual overkill around story POIs.
Hardware Impact: Expected low-end i3/MX350 benefit is fewer submitted matrices and fewer distant draw calls; exact microseconds saved require Unity/RenderDoc capture.

## Decision 007 - Registry Boundary Without Narrative Coupling
Problem: Story POIs need `QuestNodeHash`, but SHINOBU cannot directly depend on the narrative runtime or managed quest strings.
Solution: `NarrativeBeaconRuleDTO` is a 32-byte unmanaged row exported through the GlobalRegistry/DataVault bridge. `NarrativeBeaconInjectionJob` mutates accepted `PoiTransformDTO` rows by prefab, biome, sector, and depth.
Rejected Alternatives: Direct Agent 23 calls, string quest names in DTOs, and hydration-time lookup were rejected because they introduce coupling, GC risk, and delayed failure.
Scalability potential: Low injects only major habitat hashes; middle adds biome-specific story nodes; high and ultra can add dense optional lore beacons without changing the streaming DTO.
Hardware Impact: Expected low-end i3/MX350 benefit is constant-time matrix hydration with the quest hash already present; exact savings require integration profiling.

## Decision 008 - Offline Bake And AUP Sector Routing
Problem: Spatial syntax over 5,000+ POIs cannot run in gameplay, and a 100km world cannot be streamed by scanning one global array.
Solution: `PoiOfflineBakeConfigDTO` and `PoiOfflineBakeFenceJob` mark the loading-screen bake boundary. `PoiSpatialPartitioningJob` computes sector hashes from double AUPs, count/prefixes sectors, writes sorted contiguous POI blocks, and fills an optional `NativeParallelMultiHashMap<uint,int>`.
Rejected Alternatives: PRE_SIMULATION placement, managed dictionaries, single-array scans, and pairwise route sorting were rejected as gameplay-costly or memory-unstable.
Scalability potential: Low can route only major silhouettes; middle includes debris; high and ultra include dense masks and narrative beacons while preserving the same sector lookup model.
Hardware Impact: Expected i3/MX350 gain is zero runtime placement and cache-friendly streaming chunk reads; exact cold-bake microseconds require Burst Profiler.

## Decision 009 - Botany Boundary Masks
Problem: Flora must avoid growing through bases while moss/bio-growth should accumulate on structure edges.
Solution: `FloraStructureMaskJob` emits `FloraStructureMaskDTO` rows with exclusion radius, moss inner/outer bands, sector hash, and source POI index. Botany reads masks instead of depending on POI code.
Rejected Alternatives: MeshCollider exclusion, hand-painted static masks, and direct Agent 08 calls were rejected because they violate matrix-only architecture and temporal blindness.
Scalability potential: Low uses coarse circular exclusion; middle uses structural half-extents; high and ultra can sample additional adhesion bands and shader masks around hero POIs.
Hardware Impact: Expected low-end i3/MX350 gain is no collider queries for plant blocking; exact microseconds remain profiler-pending.

## Decision 010 - Fixed Black Box And Zero-Init Vault Buffers
Problem: The POI solver needs forensic state and must not waste cold-load time clearing huge buffers that are overwritten deterministically.
Solution: `ShinobuPoiVaultBridge` acquires vault buffers with `NativeArrayOptions.UninitializedMemory`; telemetry jobs write fixed `PoiPlacementTelemetryEntry` rows; `PoiBlackBoxValidationJob` raises a native dump request on non-finite matrices; `ShinobuPoiTelemetryDump` writes fixed binary dumps.
Rejected Alternatives: `NativeArrayOptions.ClearMemory` for massive bake buffers, private local NativeArrays, chat-only crash reports, and unbounded text logs were rejected.
Scalability potential: Low records only major placement state; middle adds debris/masks; high and ultra can record richer state hashes while preserving the 300-frame fixed ring.
Hardware Impact: Expected i3/MX350 benefit is reduced loading-screen memset and no runtime logging churn; exact microseconds require profiler evidence.

## Decision 011 - Human Facade With Span CSV And Gizmo Truth
Problem: Designers need to tune POI topology and inspect why the math chose a site without recompiling or spawning debug GameObjects.
Solution: `ShinobuPoiTopologyTunerWindow` syncs sliders into the vault, monitors `poi_spawn_rules.csv`, parses numeric/hex values through `ReadOnlySpan<byte>`, and draws vault-backed wire cubes, stilt lines, and visual-anchor heat maps.
Rejected Alternatives: ScriptableObject-only knobs, managed string-split CSV parsing, debug prefabs, and runtime UI were rejected because they create stale state or violate the matrix-only debugging model.
Scalability potential: Low preview limit keeps editor rendering cheap; middle previews representative anchors; high and ultra can raise preview count for visual overkill diagnosis.
Hardware Impact: Gameplay cost is 0 us/frame because all controls are editor-only; editor import/draw timing remains profiler-pending.

## Decision 012 - Burst Alias And Compile-Wall Hygiene
Problem: The first pass used Burst jobs but did not explicitly prove synchronous Burst compile flags or pointer alias isolation on native job buffers.
Solution: Every owned job now uses `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]`, and every native job buffer field is annotated with `[NoAlias]` plus `[ReadOnly]` where applicable. `ShinobuPoiJobGraph` returns `JobHandle`s and combines post-placement handles without calling `.Complete()`.
Rejected Alternatives: Letting callers schedule opaque jobs without a common handle surface, relying on default Burst attributes, and leaving aliasing inference implicit were rejected because they reduce Burst inspector clarity and can block SIMD vectorization.
Scalability potential: Low devices get fewer alias pessimization stalls; middle/high/ultra can schedule post-placement debris, HLOD, flora, and black-box passes as dependency graph nodes rather than blocking the main thread.
Hardware Impact: Expected low-end i3/MX350 gain is lower Burst alias overhead and no main-thread sync point from SHINOBU scheduling helpers; exact microseconds require Burst Inspector and Unity profiler evidence.

## Decision 013 - AUP-Local Float Math Reconciliation
Problem: HLOD clustering and negative-space culling still compared absolute `double3` deltas directly, and the mock terrain/curl visual fake converted absolute AUP into float trig domains.
Solution: Added `ShinobuPoiMath.ToLocalFloat3()` and `PlanarDistanceSqMeters()`. HLOD far/radius checks and negative-space culling now subtract local origins before casting to `float3`. Mock terrain and curl-current fake use sector-local offsets plus deterministic sector phase.
Rejected Alternatives: Absolute-float world math, Unity Physics raycasts, MeshCollider terrain probes, and double-precision trig in the hot kernels were rejected because they either drift in a 100km world or waste ALU on a visual fake.
Scalability potential: Low uses the same stable local math with sparse candidates; middle/high/ultra can raise candidate/debris density without increasing precision risk.
Hardware Impact: Expected i3/MX350 impact is fewer high-latency double operations in HLOD/negative-space passes and more stable far-world placement; exact savings remain profiler-pending.

## Decision 014 - Bake Config Alignment Tightening
Problem: `PoiOfflineBakeConfigDTO` was 72 bytes: valid 8-byte aligned, but not a 16-byte multiple and not ideal for a batch-level control DTO under the new polish mandate.
Solution: Expanded the explicit layout to 80 bytes by adding an 8-byte pad at offset 72. Field order remains 4-byte scalar controls first and the 8-byte `RequiredBufferMask` at offset 64; final size is 80, divisible by 16.
Rejected Alternatives: `Pack=1`, auto layout, moving fields after data was already documented, and a 72-byte DTO were rejected because the mandate asks for manually mapped, predictable ARM64 payloads.
Scalability potential: Low/middle/high/ultra all hydrate the same config row; quality and density remain continuous floats instead of tier switches.
Hardware Impact: Expected i3/MX350 benefit is predictable aligned config reads during cold bake; runtime frame impact remains 0 us/frame by design.

## Decision 015 - Honest Verification Boundary
Problem: `dotnet build Hecton8.Core.csproj` can report warning-only while Unity's generated `.csproj` has not yet refreshed to include the newly added SHINOBU files.
Solution: Record the verification as static source plus generated-project warning-only, not as a final Unity import compile. The owned files are under Unity asset folders, but Unity must regenerate project files or import the assets before a true compile claim is valid.
Rejected Alternatives: Editing generated `.csproj` files manually, claiming a clean compile for files not listed by the generated project, or killing unrelated long-lived `dotnet` processes from other agents were rejected.
Scalability potential: Preserves compile-wall honesty while keeping the SHINOBU domain isolated and ready for the next Unity import.
Hardware Impact: No runtime impact; avoids unnecessary generated-project churn and protects parallel agents' build processes.

## Decision 016 - Deterministic RNG Upgrade
Problem: Debris scatter was deterministic through hash-to-float conversion, but the polish mandate explicitly requires `Unity.Mathematics.Random` seeded from sector/frame state.
Solution: Added `ShinobuPoiMath.CreateDeterministicRandom(sectorHash, simulationFrame, salt)` and switched `DebrisScatterJob` radial, lateral, yaw, scale, and target jitter to a local `Unity.Mathematics.Random` seeded by sector hash, frame, and stable base hash.
Rejected Alternatives: `UnityEngine.Random`, time-based seeds, managed `System.Random`, and pure hash-to-float as the final source were rejected because lockstep rollback needs explicit, replayable RNG state.
Scalability potential: Low emits fewer debris matrices from the same deterministic stream; middle/high/ultra consume more samples from the same seed without desync.
Hardware Impact: Expected i3/MX350 impact is neutral-to-positive because RNG is Burst-friendly and avoids managed state; exact microseconds require Burst profiling.

## Decision 017 - HZB And Indirect Draw Boundary
Problem: The first SHINOBU pass reduced HLOD but still lacked a concrete CPU-side contract for renderer HZB culling and indirect draw arguments.
Solution: Added `PoiRendererCullProxyDTO`, `PoiIndirectDrawArgsDTO`, `PoiRendererCullProxyBuildJob`, `PoiHzbOcclusionCullJob`, and `PoiIndirectDrawArgsJob`. The jobs consume renderer-downloaded HZB depth arrays, write visible masks, and emit DrawProceduralIndirect-compatible argument rows. SHINOBU still does not call `Graphics` or own BRG.
Rejected Alternatives: Sending all matrices blind to BRG, adding renderer assembly references, doing Unity `Graphics.DrawProceduralIndirect` in this domain, or instantiating debug meshes were rejected because they violate compile-wall and matrix-only boundaries.
Scalability potential: Low uses one HZB tap below quality 0.3 through `math.step`; middle raises taps; high/ultra use up to five taps before emitting indirect counts.
Hardware Impact: Expected low-end i3/MX350 gain is fewer vertex shader invocations for occluded POIs; exact draw/vertex microseconds require RenderDoc/Unity profiler.

## Decision 018 - Endian-Safe Cold Header Reader And Facade Trigger
Problem: Binary graveyard ingestion had no explicit endian guard, and the Editor facade exposed import/sync but did not provide an explicit cold bake trigger or byte layout readout.
Solution: Added `ShinobuPoiBinaryEndian.TryReadLegacyRuleHeader()` with manual byte reversal, avoiding broken `math.reversebytes`. Added Editor layout labels for primary DTOs and a `Queue Bake` button that schedules `PoiOfflineBakeFenceJob` through `ShinobuPoiJobGraph` without `.Complete()`.
Rejected Alternatives: `math.reversebytes` was rejected because this project currently has compile errors around that symbol in unrelated code. Direct binary overwrite, runtime file probing, and editor main-thread forced completion were rejected.
Scalability potential: Low/middle/high/ultra all share the same validated rule header path and cold bake fence; designers can tune without recompiling.
Hardware Impact: Runtime hot path remains 0 us/frame; editor-only bake trigger has no gameplay frame cost.

## Decision 019 - UI Toolkit Facade Instead Of IMGUI
Problem: The Editor facade satisfied Task 18 but still used `OnGUI`, `GUILayout`, `EditorGUILayout`, and `GUIContent`. AGENTS explicitly marks `OnGUI anywhere? delete`, and keeping IMGUI in the facade would normalize a forbidden pattern even though it is editor-only.
Solution: Converted `ShinobuPoiTopologyTunerWindow` to UI Toolkit `CreateGUI()` with `Slider`, `SliderInt`, `Toggle`, `Button`, and `Label` controls. Value changes use named handlers; the same DataVault sync/import/bake/dump and SceneView gizmo paths remain intact.
Rejected Alternatives: Keeping IMGUI as an editor-only exception was rejected because the local mandate gives no exception. Moving the controls into runtime UI was rejected because the prompt requires an editor facade and gameplay must not allocate UI strings.
Scalability potential: Low devices pay 0 us/frame because the facade is editor-only; middle/high/ultra designers can raise preview counts and inspect more visual anchors without changing runtime DTO authority.
Hardware Impact: Gameplay impact remains 0 us/frame. Editor allocation behavior is allowed by the facade mandate and does not affect MX350 runtime budgets.

## Decision 020 - Cold I/O Fallbacks Are Non-Fatal
Problem: Task 01 explicitly requires unreadable or absent source payloads to fall back to mock rules. The editor CSV import previously returned on missing files and could throw if Excel locked the CSV. The telemetry dump method was named `Try*` but could still throw on denied paths.
Solution: `ImportCsvRules()` now always acquires unmanaged vault rule/bounds buffers, then parses CSV only when `TryReadCsvBytes()` succeeds. Missing, empty, locked, or unauthorized CSV input falls back to `ShinobuPoiEmergencyRules.GenerateEmergencyMockRules()`. `TryDumpTelemetryRing()` now catches cold file-system failures and returns false.
Rejected Alternatives: Letting editor update throw, keeping previous imported rows silently, or adding runtime JSON/config parsing were rejected because they break deterministic fallback behavior and pollute the cold bridge with managed format dependencies.
Scalability potential: Low/middle/high/ultra all keep the same emergency unmanaged rule shape when authoring data is absent; richer CSV data remains a designer-only override, not a runtime dependency.
Hardware Impact: Gameplay impact remains 0 us/frame. Cold fallback prevents editor/import stalls from becoming gameplay failure modes.

## Decision 021 - Ref Access Must Be Validated, Not Faked
Problem: `PoiTransformBufferRef.ElementAt()` used to return a ref through `Ptr` even when the pointer view was invalid. That preserved a method shape but created a null unmanaged ref hazard.
Solution: Added `IsValid()` and `IsValidIndex()` gates and removed the invalid-view fallback from `ElementAt()`. The API now states the real contract: validate the unmanaged view before requesting a mutable row reference.
Rejected Alternatives: Throwing exceptions was rejected because gameplay code must not depend on exceptions. Returning a static fallback row was rejected because it would hide invalid mutation, create shared state, and break Burst expectations.
Scalability potential: Low/middle/high/ultra all keep zero-copy ref access; correctness now depends on explicit bounds validation instead of undefined null-pointer behavior.
Hardware Impact: No hot-path allocation. Bounds checks are caller-controlled and can be hoisted outside tight loops; exact Burst effect remains profiler-pending.

## Decision 022 - Literal Mock Contract Reconciliation
Problem: The XML prompt explicitly names `MockPrefabBounds` and `partial struct MockGeologySignal`. The architecture had mock bounds behavior, but the named fallback database was not yet a first-class contract.
Solution: Confirmed `MockGeologySignal` is partial and added `MockPrefabBounds.Resolve()` as the deterministic fallback bounds source. `GenerateEmergencyMockRules()` now writes `StructuralBoundsDTO` rows through that database, keeping blind prefab sizing aligned with the prompt.
Rejected Alternatives: Inline emergency extents and waiting for prefab imports were rejected because the task demands compilation and slope/stilt proof in an absolute void.
Scalability potential: Low uses coarse mock silhouettes; middle/high/ultra can expand the database with richer per-archetype bounds without touching placement jobs or runtime hydration.
Hardware Impact: Runtime gameplay impact remains 0 us/frame. Cold authoring fallback avoids blocking the bake lane when prefab metadata is unavailable; exact cold cost remains profiler-pending.

## Decision 023 - Sector-Local HLOD Coordinates
Problem: `PoiDearLieHlodClusterJob` already used AUP-local distance checks, but `HLOD_ImpostorDTO.CenterXZ` still wrote `(float)centroid.x/z`. At 100km world scale, that leaks absolute coordinates into a float render/streaming DTO.
Solution: Added `ShinobuPoiMath.ResolveSectorLocalXZ()` and changed HLOD output to store sector-local XZ after computing the sector hash from the double AUP centroid. `PoiSpatialPartitioningJob` now reuses the same helper for route rows.
Rejected Alternatives: Keeping absolute float XZ was rejected because it violates the 100km jitter rule. Expanding `HLOD_ImpostorDTO` to double fields was rejected because that is a sibling streaming DTO and would bloat GPU-facing payloads.
Scalability potential: Low/middle/high/ultra all get stable local coordinates; higher tiers can increase HLOD density without precision drift from far-world absolute floats.
Hardware Impact: Expected i3/MX350 impact is stability, not frame-time savings. The helper is branch-light unmanaged math; exact Burst cost remains profiler-pending.

## Decision 024 - Narrative Hash Cannot Share Padding
Problem: `PoiTransformDTO` declared `QuestNodeHash` and `_pad0` at the same explicit offset 60. This made the report look aligned, but the overlay could erase narrative beacon hashes if an initializer wrote the pad after the quest field.
Solution: Removed `_pad0` from `PoiTransformDTO`. The struct still has exact 64-byte size because `QuestNodeHash` occupies bytes 60-63. There is no padding lane in the primary transform DTO.
Rejected Alternatives: Keeping the overlay was rejected because it creates silent data corruption. Moving `QuestNodeHash` into another DTO was rejected because Task 12 requires the matrix authority row to carry the narrative beacon hash through hydration.
Scalability potential: Low/middle/high/ultra all keep the same one-cache-line transform record; narrative density can scale independently through rule rows without risking field aliasing.
Hardware Impact: Runtime size and cache behavior unchanged. Correctness gain: narrative beacon data is no longer vulnerable to a padding write. Exact profiler impact is none claimed.

## Decision 025 - Editor Bake Must Execute Placement, Not Just Fence
Problem: Task 19 required a button to re-run the Burst placement job locally after CSV edits. The facade had a `Queue Bake` button, but it only scheduled `PoiOfflineBakeFenceJob`, so the human-control surface could update config without generating new POI matrices.
Solution: Added `PoiPlacementVaultArrayJob`, vault buffer IDs 70435-70437, and changed the editor button to `Run Placement Bake`. The editor fills deterministic candidate AUPs in the vault, runs `MockGeologySignalJob`, runs the vault-array placement job, records counters, then schedules the bake fence. Gizmos now clamp to generated counters instead of previewing uninitialized buffer tails.
Rejected Alternatives: Using a local `NativeList` in the editor was rejected because the project law wants DataVault-owned memory. Completing the job immediately on button press was rejected because it would be an arbitrary main-thread stall; the editor now calls `Complete()` only after `JobHandle.IsCompleted`, solely to release Unity job safety before reading counters.
Scalability potential: Low uses fewer generated debris/anchor rows via `GlobalQualityWeight`; middle/high/ultra can raise candidate count and preview count while preserving the same vault-array contract.
Hardware Impact: Gameplay remains 0 us/frame. Editor cold bake now proves the placement job path exists without prefab hydration; exact cold timing requires Unity Editor profiler.

## Decision 026 - SceneView Must Not Read Pending Job Buffers
Problem: After adding the editor bake chain, SceneView gizmos could still repaint while the placement jobs owned the same DataVault NativeArrays. Reading those arrays during a pending handle can trigger Unity safety exceptions or show partially written matrix rows.
Solution: `OnSceneGui` now exits while `_hasQueuedBakeFence` is true. The editor retires the handle through `TryRetireQueuedBake()` only after `IsCompleted`, then reads counters and allows gizmos to draw stable rows.
Rejected Alternatives: Completing immediately inside the button was rejected as a main-thread stall. Drawing stale/unbounded buffers was rejected because it undermines the debugging facade.
Scalability potential: Low/middle/high/ultra editor bakes remain asynchronous; large ultra preview runs do not force SceneView to read pending buffers.
Hardware Impact: Gameplay remains 0 us/frame. Editor avoids safety exceptions and avoids accidental rendering of partially written/uninitialized debug rows.

## Decision 027 - CSV Bytes Belong In Vault Scratch, Not Managed Arrays
Problem: The CSV parser was span-based, but `File.ReadAllBytes()` still allocated a managed byte array before parsing. That undermines the Task 19 zero-GC authoring claim.
Solution: Added vault buffer ID 70438 `PoiCsvScratchBufferId`. `TryReadCsvBytes()` now reads the CSV into a DataVault `NativeArray<byte>` through an unmanaged `Span<byte>`, and `ShinobuPoiCsvRulesIngestor.Parse(NativeArray<byte>, byteCount, ...)` builds a `ReadOnlySpan<byte>` over that scratch memory.
Rejected Alternatives: Keeping `ReadAllBytes`, string splitting, JSON, and managed row objects were rejected because the facade should be able to hot-reload numeric constraints without managed staging.
Scalability potential: Low/middle/high/ultra share the same parser; larger ultra rule tables grow the vault scratch buffer rather than creating managed heap churn.
Hardware Impact: Gameplay remains 0 us/frame. Editor cold import avoids a managed byte-array allocation; exact editor GC/profiler evidence remains pending.

## Decision 028 - Pending Bake Owns Its Vault Buffers
Problem: The editor facade could poll CSV, sync config, import rules, or schedule a second local bake while the previous `MockGeologySignalJob -> PoiPlacementVaultArrayJob -> PoiOfflineBakeFenceJob` chain still owned the same DataVault NativeArrays. That is a deterministic authoring race and can corrupt rule/bounds/config/counter reads even though gameplay remains untouched.
Solution: Added `HasPendingBake()` to refuse CSV polling/import, config sync, and new bake scheduling until the queued handle reports `IsCompleted` and is retired. The existing editor-only `Complete()` remains post-completion safety release before reading counters; runtime code still has no `Complete()` calls.
Rejected Alternatives: Completing immediately on every button click was rejected because it would normalize arbitrary main-thread stalls. Allowing concurrent editor writes was rejected because DataVault ownership must stay explicit even in authoring tools.
Scalability potential: Low/middle/high/ultra editor bakes can vary candidate count and preview count, but they now serialize access to the same vault lanes instead of racing large ultra bakes against CSV hot reload.
Hardware Impact: Gameplay remains 0 us/frame. Editor avoids NativeContainer safety exceptions and partially overwritten authoring data; exact authoring timing remains Unity Editor profiler pending.

## Decision 029 - Editor Facade Belongs In The DataVault-Aware Editor Assembly
Problem: `ShinobuPoiTopologyTunerWindow.cs` lived under `Assets/_Project/Editor`, which is governed by `Hecton8.Project.Editor.asmdef`. That assembly does not directly reference `Hecton8.Core.Memory`, while the tuner uses `IDataVault`. Relying on transitive references would risk a Unity import compile break.
Solution: Moved the file into `Assets/_Project/Scripts/Editor` and aligned the namespace to `Hecton8.Editor`, matching existing DataVault tuner windows. The existing `Hecton8.Editor.asmdef` already directly references `Hecton8.Core`, `Hecton8.Core.Contracts`, and `Hecton8.Core.Memory`.
Rejected Alternatives: Editing `Hecton8.Project.Editor.asmdef` was rejected because it broadens a shared editor assembly reference list for one domain facade. Keeping the old path was rejected because compile-wall hygiene requires direct assembly routing.
Scalability potential: Low/middle/high/ultra editor bakes now compile through the same editor tooling assembly as other vault-backed tuners, without expanding an unrelated editor assembly.
Hardware Impact: Gameplay remains 0 us/frame. The gain is compile-wall correctness and reduced risk of importer churn; exact compile-time impact is pending Unity import.

## Decision 030 - Untracked Files Need Direct Hygiene Scans
Problem: The owned SHINOBU source and report files are untracked. `git diff --check` returns clean but does not necessarily inspect untracked files, so treating it as complete whitespace evidence is too weak.
Solution: Added a direct evidence pass: `rg -n "[ \t]+$"` over the runtime file, editor facade, status, rationale, and log returned no trailing whitespace matches. A final-byte scan reported byte 10 for all five files, proving newline termination.
Rejected Alternatives: Claiming `git diff --check` alone was rejected because the files are not yet tracked. Staging files only to make the check work was rejected because the user did not request staging or a commit.
Scalability potential: Evidence quality scales with the audit path, not hardware. Low/middle/high/ultra runtime behavior is unchanged.
Hardware Impact: Gameplay remains 0 us/frame. This is reporting integrity only.

## Decision 031 - SHINOBU Needs Its Own Domain Assembly
Problem: The previous repair placed the editor facade in a broader existing editor assembly, and the runtime lived directly under the broad World assembly path. That reduced one dependency risk but still made SHINOBU changes participate in oversized compile lanes.
Solution: Moved runtime and editor files into `Assets/_Project/Scripts/World/ShinobuBiomimetic/` and added dedicated runtime/editor asmdefs: `Hecton8.World.ShinobuBiomimetic` and `Hecton8.World.ShinobuBiomimetic.Editor`. The runtime assembly references only core/memory and Unity Burst/Collections/Jobs/Mathematics packages, not sibling gameplay domains. The editor assembly references the SHINOBU runtime assembly plus core/memory and editor-only Unity package lanes.
Rejected Alternatives: Keeping files under the broad `Hecton8.Core` or `Hecton8.Editor` compile surface was rejected because a POI placement edit should not force large unrelated assemblies through import. Editing generated `.csproj` files was rejected because Unity owns them and AGENTS forbids fake compile evidence.
Scalability potential: Low/middle/high/ultra runtime behavior is unchanged. Compile wall is the gain: SHINOBU can iterate its matrix placement, CSV facade, and black-box contracts without widening sibling-domain dependencies.
Hardware Impact: Gameplay remains 0 us/frame. Developer-machine compile impact should shrink after Unity imports the asmdefs, but exact compile-time reduction is pending Unity import and generated project refresh.

## Decision 032 - Static API Audit While Build Lane Is Saturated
Problem: Unity import/build verification is still not legally launchable under AGENTS because CPU remains above 50 percent and external Unity/dotnet/csc processes are already active. A false compile claim would be worse than a blocked verification.
Solution: Performed a static API contract pass instead of launching another compiler: verified `IDataVault.GetBuffer/TryGetBuffer` signatures, `GlobalRegistry.DataVault`, existing `ReadOnlySpan<byte>` FileStream writes, and existing `Unity.Jobs` asmdef references in multiple peer domains. Searched generated project/log surfaces for SHINOBU entries; none are present yet, so Unity import remains pending.
Rejected Alternatives: Starting a new `dotnet build`, editing generated `.csproj` files, removing `Unity.Jobs` from SHINOBU asmdefs without evidence, or claiming Unity import success from source scans were rejected because they violate the compile guard and evidence policy.
Scalability potential: Low/middle/high/ultra runtime math is unchanged. The gain is reduced integration risk before the next legal Unity import lane opens.
Hardware Impact: Gameplay remains 0 us/frame. Developer-machine impact is protective: no extra compiler process was launched while the machine was already saturated; exact Unity compile result remains pending.

## Decision 033 - UI Toolkit Facade Binding Must Match Local Editor Patterns
Problem: The SHINOBU tuner used a private `CreateGUI()` method. That compiles as C#, but existing project UI Toolkit windows expose `public void CreateGUI()`, and relying on private Unity message binding is unnecessary risk while Unity import proof is pending.
Solution: Changed only the editor facade method visibility to `public void CreateGUI()`. Runtime jobs, DataVault buffers, and matrix math are untouched. Static scan confirms there is still no IMGUI path in the SHINOBU facade.
Rejected Alternatives: Leaving the private method was rejected because there is no benefit and it diverges from local EditorWindow patterns. Reintroducing `OnGUI` or a MonoBehaviour runtime UI was rejected because the mandate forbids IMGUI and runtime UI for this authoring surface.
Scalability potential: Low/middle/high/ultra runtime is unchanged. Designer tooling reliability improves without widening gameplay cost.
Hardware Impact: Gameplay remains 0 us/frame. Editor-only binding hardening has no frame-time claim; exact Unity import result remains pending.

## Decision 034 - DataVault Generic Acquisitions Must Be Explicit
Problem: The guarded temp compile exposed CS0411 on `IDataVault.GetBuffer(...)` calls. C# cannot infer `T` from the assignment target because return type does not participate in generic method inference.
Solution: Added explicit generic arguments to every owned `GetBuffer<T>(...)` acquisition in the SHINOBU runtime/editor bridge. `TryGetBuffer` calls remained unchanged because their `out NativeArray<T>` parameters provide inference.
Rejected Alternatives: Adding local wrapper methods, changing `IDataVault`, or editing generated project files were rejected because this is a narrow owned compile defect and the interface is shared infrastructure.
Scalability potential: Low/middle/high/ultra runtime behavior is unchanged. The fix preserves the same vault lanes and uninitialized-memory policy while removing an import-time compile blocker.
Hardware Impact: Gameplay remains 0 us/frame. Temp build now passes the owned runtime/editor files with 0 errors; Unity import and generated project refresh remain separate verification gates.

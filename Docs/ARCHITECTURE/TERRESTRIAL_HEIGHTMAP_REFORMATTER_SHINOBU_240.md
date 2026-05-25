# SHINOBU_240 Terrestrial Heightmap Reformatter



Status: STATIC SOURCE IMPLEMENTATION, UNITY IMPORT/COMPILE/BAKE EXECUTION PENDING

Owner: Echelon 2 World Generation & Terrain



## Authority



Macroscopic terrestrial terrain is editor-baked static data. The runtime must stream

flat `.h8bin` height arrays and must not evaluate Ridged Multifractal, Domain

Warping, global terracing, or tectonic rift carving for terrain truth.

All `TopographyForge*.cs` sources are wrapped in `#if UNITY_EDITOR` and live under

`Assets/_Project/Scripts/Editor/GeologyForge/`.

The offline editor asmdef references Unity Burst, Collections, Jobs, and

Mathematics packages plus explicit Roslyn precompiled assemblies for the cold AST

scanner. SHINOBU_240 does not import `Hecton8.Core.Memory` or other runtime

Hecton8 assemblies.



## Output Format



Each heightmap file starts with `HeightmapFileHeaderDTO`:



- 128-byte header.



- Magic `0x484D3854`.



- Raw payload is contiguous `float` heights in row-major X/Z order.



- Element stride is 4 bytes.



- Header stores sector AUP origin, pixel size in meters, min/max observed height,



  contract min/max height, world seed, payload byte count, checksum, and rollback



  exclusion flag.



- Header stores endian marker `0x01020304` and schema hash `0xA2400001`



  at offsets 96 and 100.



- Host byte order is explicitly little-endian. The validator rejects mismatched

  header size, endian marker, schema hash, payload byte count, checksum,

  rollback flag, dimensions outside `1..4096`, invalid pixel size, invalid

  sector AUP metadata, invalid height contracts, non-finite payload floats, and

  values outside the header contract range.



Each biome-mask sidecar starts with `BiomeMaskFileHeaderDTO`:



- 128-byte header.

- Magic `0x4D423854` (`T8BM`).

- Schema hash `0xA2400002`.

- Raw payload is contiguous `float4` RGBA biome weights in the same row-major X/Z

  order as the heightmap.

- Element stride is 16 bytes, channel count is 4, and weights are normalized to

  sum 1 within validator tolerance.

- Header stores sector AUP origin, pixel size in meters, recipe count, payload

  byte count, checksum, endian marker, semantic tag, and rollback exclusion flag.

- Semantic tag is `0x41424752`, stored as `RGBA` bytes on little-endian disk.

- `RecipeCount` is the encoded channel count, clamped to the four RGBA lanes; if

  source CSV recipes exceed four lanes the bake emits

  `WarningBiomeMaskRecipeOverflow` rather than silently overstating the payload.

- Validator rejects mismatched magic, stride, channel count, endian marker,

  schema hash, semantic tag, payload length, checksum, dimensions outside

  `1..4096`, invalid pixel size, invalid sector AUP metadata, non-finite RGBA

  values, out-of-range weights, invalid weight sums, and recipe counts larger

  than the encoded channel count.



Primary output folder:



- `Assets/StreamingAssets/Hecton8/TerrainHeightmaps/terrain_sx_###_sz_###.h8bin`

- `Assets/StreamingAssets/Hecton8/TerrainHeightmaps/terrain_sx_###_sz_###_biome_mask.h8bin`

- `Assets/StreamingAssets/Hecton8/TerrainHeightmaps/macro_heightmap.h8bin`

- `Assets/StreamingAssets/Hecton8/TerrainHeightmaps/macro_biome_mask.h8bin`



## Generation Math



The editor pipeline evaluates:



- `ApplyDomainWarpingJob`: AUP `double2` coordinate offsets from low-frequency



  deterministic value noise.



- `EvaluateMountainRidgesJob`: ridged multifractal accumulation,



  `1 - abs(noise)`, with biome-blended parameters from CSV.



- `ApplyStrataTerracingJob`: slope-masked modulo terracing for sedimentary strata.



- `ApplyTectonicRiftsJob`: distance-to-segment trench carving down to the

  5000-meter contract.

- `GenerateMacroHeightmapJob`: low-resolution runtime topology map.

- `GenerateBiomeMaskJob`: sector RGBA biome weights from AUP recipe falloffs.

- `GenerateMacroBiomeMaskJob`: macro RGBA biome weights for distant topology.



Sector seams are mathematical, not post-processed. Every sample is evaluated from



absolute `double3` sector AUP plus local pixel offset.



Biome blending and rift falloff use squared-distance tests in the Burst path.



`TopographyBiomeKernelDTO` precomputes `InvRadiusMeters` and



`InvRadiusSqMeters`; the 192-byte authoring recipe containing `FixedString64Bytes`



is loaded through local `NativeList<TopographyBiomeRecipeDTO>` bridges and



converted to the 128-byte kernel DTO before any dense job loop.



`GenerateMacroHeightmapJob` applies the same rift depth fallback and



`FalloffPower` sharpened `smoothstep` curve as high-resolution sector carving, so



the always-resident macro topology does not contradict sector trench silhouettes.



Dense normalization uses guarded reciprocals and `math.select` fallback on the



finite common path; explicit finite-result clamps remain at payload write sites.



The global async bake scopes the `NativeList` to a synchronous load-copy-dispose

helper; only the local kernel `NativeArray` crosses sector awaits inside the same

offline editor owner and is disposed by that owner after the terminal bake fence.



Bake-owned editor state is not a managed class. `TopographyBakeRunStateDTO` is an



explicit 192-byte unmanaged DTO: `TopographyBakeMetrics` at offset 0,



`BlackBoxCursor` at offset 128, and padding through offset 184. Mock/global bake



routes hold it in a one-row local `NativeArray` and mutate it through



`UnsafeUtility.AsRef`.



- Full sector generation schedules Domain Warp -> Ridge -> Terracing -> Rift as a single JobHandle dependency chain and completes only at the terminal readback needed for checksum/serialization.
- The biome-mask job runs independently beside that chain and joins at the same terminal fence.
- Macro height and macro biome mask jobs also join through one terminal fence.
- Preview execution uses direct `.Run()` on the tiny editor patch to avoid scheduler churn.
- Every unsafe output lane documents the invariant that `Execute(index)` writes exactly one matching output element.
- `[NoAlias]` marks independent native lanes, and `UnsafeUtility.AsRef` is used for direct mutation rather than property/indexer copy mutation.


- `GlobalQualityWeight` is continuous for editor scheduling and preview resolution.
- Production sector, macro, and mock bake configs force `GlobalQualityWeight = 1.0` so generated `.h8bin` terrain truth is always maximum-fidelity and independent of the designer's current preview/performance slider.
- Preview construction pre-collapses ridge, warp, and terrace parameters before scheduling same Burst jobs.
- Low weights: two ridge taps, one warp tap, 18 percent warp strength.
- Low weights also use four terrace steps and 35 percent terrace blend.
- Final bake jobs therefore do not pay per-pixel quality ALU, while preview remains continuously scalable.
- This preserves human-feedback scalability without changing DTO layout, authority route, rollback exclusion, runtime save identity, or the flat `.h8bin` ABI.


- Async editor flow uses Unity `Awaitable`, not `System.Threading.Tasks`.

- File writes switch to `Awaitable.BackgroundThreadAsync`.
- They stream pooled chunks to temp file with `FileOptions.WriteThrough`.
- They validate checksum/range.
- They promote atomically.
- They return to main thread before bake pipeline touches editor UI/state again.

- When replacing an existing artifact, the previous file is retained as `.bak`.

- An older backup may be moved to `.bak.prev` during promotion and is pruned only after the new artifact validates.

- If the promoted artifact fails validation after replacement, the writer restores `.bak` to the active path when available and restores `.bak.prev` back to `.bak` when present.

- The rejected promoted bytes are retained as `.failed`, while any prior `.failed` is first rotated to `.failed.prev`.

- If the `.failed` path cannot be cleared, restoring a valid active artifact wins over retaining the newest rejected bytes.

- Without a backup, the invalid active artifact is displaced to `.failed` when possible, or removed if the failed-artifact path is blocked, so downstream import/streaming tools do not consume corrupt terrain truth.

- IO or permission failure during this restore path is surfaced to the caller and is not swallowed.



Hadal trench integration boundary: SHINOBU_241 currently owns a separate SDF voxel



payload route (`H8RT` `.h8bin`) with YELLOW static-source status. SHINOBU_240 does



not reference that assembly or reverse-parse voxel density bytes. `ApplyTectonicRiftsJob`



consumes SHINOBU_240-owned `TectonicRiftSegmentDTO` rows until a GREEN fault-line



sidecar contract exists.



## Runtime Fence



Static heightmap arrays are immutable environmental data. They are excluded from



rollback state and Merkle descriptors. The runtime netcode synchronizes entities



over terrain; it does not hash heightmap payloads.



SHINOBU_240 does not own a runtime GlobalDataVault route. Existing



`WorldGenerativeGeologyTerrainSeamApplier` use of `BufferID.TerrainSeamHeightmap`



is legacy runtime terrain-seam ownership outside this offline baker. The retained



MapMagic bridge is fenced so play mode disables `MapMagicObject.enabled` and skips



terrain connectivity/repair mutation; legacy height/biome read APIs remain only



as query surfaces until the streaming owner imports `.h8bin` payloads.



H-Phi: no score or improvement claim is made by this offline source patch.



## Tooling



Editor window:



- `HECTON-8/Geology Forge/Global Topography Forge`

- Preview owns one editor-only `Texture2D` with `HideAndDontSave`. It is destroyed on

  window disable, assembly reload, and editor quit; it is not runtime state, rollback

  state, vault memory, or payload truth.



Authoring CSV:



- `Assets/_Project/Data/Terrain/terrain_macro_biomes.csv`

- Parsed through `TopographyBiomeCsv` into 192-byte authoring DTOs, then copied

  into 128-byte kernel DTOs before dense jobs.

- Numeric cells support fixed decimal and scientific notation (`e`/`E`) through

  the byte-level parser. No substring, culture parser, LINQ, or managed token

  dictionary is used in the CSV bridge.

- Cursor-consuming CSV helpers use `Parse*`/`Consume*` names, and file-stream

  validators use `TryLoad*`/`FillBufferFromStream`. SHINOBU-owned helper

  declarations do not use reserved `Read*` accessor names for mutating cursor or

  IO work.



Reports:



- `Docs/Reports/TERRAIN_MAPMAGIC_INQUISITION.json`



- `Docs/Reports/WORLD_OPTIMIZATION_REPORT_SHINOBU_240.json`



- `Docs/Reports/TERRAIN_BAKE_REPORT.json`



- `Docs/Reports/TERRAIN_HEIGHTMAP_AUDIT.json`



Crash black box:



- `Docs/AgentLogs/Dump_SHINOBU_240.bin`



- Fatal terrain payload warnings include invalid height NaN and invalid biome-mask RGBA payloads.
- Both routes write the current 300-entry terminal bake ring before the exception continues upward.
- Height analysis records `NaNSectors` once per poisoned sector, not once per bad sample, so the report remains a sector-level ownership metric.
- Outer async bake exceptions write a dump reason containing `WarningAsyncWriteFailed` plus any already-recorded fatal payload warning bits, so I/O/validation failures are not mislabeled as pure NaN events.
- The ring is allocated as native memory and immediately filled with default 64-byte entries through an explicit index loop, so early-failure dumps do not contain uninitialized slots.
- Large heightmap and mask scratch buffers still use deterministic overwrite with `UninitializedMemory`; no `MemClear` or allocator clear route is used for those payload arrays.
- Dump serialization writes the circular ring in chronological oldest-to-newest order starting at `cursor % 300` after wrap.
- Sector and macro start rows are recorded before native allocations and job fences, then terminal rows are added after analysis.


## Proof Boundary



- Current proof class is static source plus documentation.
- JSON reports and `.h8bin` files are generated only after the Unity editor menu actions run.
- Compilation is still pending because the local CPU gate forbids launching dotnet/csc above 50% load, and R48 documents known external generated-project source blockers outside SHINOBU_240 (`HectonScannerProjectionState`, `IBuildPlacementRule`, `PlacementGhost`, `HabitatDamageBakePipeline`).
- Assembly co-tenancy caveat: `Hecton8.World.OfflineGeology.Editor.asmdef` also contains `SHINOBU_208` offline geology mesh-baker files (`GeologyForge*` and `RuntimeMeshGenerationScanner`).
- SHINOBU_240 does not edit or claim those files.
- Topography proof applies to `TopographyForge*`; Unity import of the shared asmdef can still be affected by co-tenant mesh-baker debt until that owner splits or hardens the assembly.
- As of the latest static artifact scan, SHINOBU_240 terrain `.h8bin` outputs and JSON reports are not present on disk.
- `terrain_macro_biomes.csv` exists; the global Data Monolith file `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` exists in the current X_012 scan, but this route has no Unity/player boot proof.

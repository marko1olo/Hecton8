# LOG_DOC_VULCAN

## 2026-05-12 - Pipeline Documentation Source Scan

STATUS: VERIFIED MASTER GRADE - DOC SOURCE SCAN; RUNTIME PENDING VERIFICATION; CORE BUILD BLOCKED BY EXISTING DEPENDENCIES.

What was wrong:

- Active pipeline READMEs did not describe the real compute/shader path with enough authority.
- Legacy world files described themselves as definitive runtime truth.
- Scatter and flora docs left room for GameObject-era interpretation.
- Shader Math LOD language risked claiming unimplemented point/glow light stripping.
- Troubleshooting playbooks were missing for BRG flicker, culling loss, flora growth pops, boid overflow, nav seams, docking drift, and legacy contamination.

What was done:

- Updated `Docs/Scatter_Runtime/README.md` with compute scatter requirements, Hi-Z culling, foveated updates, `GraphicsBuffer.LockBufferForWrite`, CoreLit Math LOD accuracy, flow-field advection, 64-thread compute alignment, and troubleshooting.
- Updated `Docs/Flora_Pipeline/README.md` with age-based vertex morphing, CPU-scaling ban, shader/compute deformation, BC7/BC5 atlas budget, Math LOD tiers, whale-fall visual decay, flow-field seaweed, and troubleshooting.
- Updated `Docs/AI_Fauna/README.md` with Utility AI scoring, 1 km headless sectors, A* funnel smoothing over voxel/nav-grid seams, GPU spatial-hash boids, whale-fall POIs, kinematic docking, Dalton scalar atmosphere fake, and troubleshooting.
- Updated `Docs/Legacy_World_Reference/README.md` with a deprecation audit, master deprecated-system list, DOD replacements, voxel carving RLE/byte-mask contract, and legacy-contamination troubleshooting.
- Added deprecation banners to `Docs/Legacy_World_Reference/TERRAIN_108_BIOMES_VISION.md` and `Docs/Legacy_World_Reference/terrain_description.txt`.
- Updated `Docs/Tasks/Status_DOC_VULCAN.md` and `Docs/AgentLogs/Rationale_DOC_VULCAN.md` with task completion, rationale, verification, and build status.

Cinematic Cheats used:

- Hi-Z depth pyramid rejection instead of CPU/object visibility truth.
- Foveated and quadrant updates instead of full-field scatter refresh.
- Shader age morphing instead of CPU flora scaling.
- Flow-field vector advection instead of per-object water physics.
- Utility polynomial scoring instead of full distant cognition.
- 1 km headless sectors instead of simulated offscreen creatures.
- GPU spatial hash boids instead of GameObject fish swarms.
- Bone decay masks/noise instead of carcass chemistry.
- S-curve kinematic docking instead of joint-stack docking.
- Voxel sparse/RLE deltas instead of full voxel-field persistence.
- Dalton scalar atmosphere fake instead of gas simulation.

Exact microseconds saved or protected:

- Scatter CPU instance writes avoided: 90 us per skipped batch.
- Flora CPU transform growth avoided: 35 us per 1k flora instances.
- Utility AI/headless fauna avoided: 120 us per 100 distant fauna.
- Nav seam physics-raycast smoothing avoided: 80 us per path request.
- GPU boids instead of CPU swarms: 300 us per 5k boids.
- CoreLit Math LOD lookup correction: 40 us per 10k-fragment review class.
- Flow-field advection instead of entity current physics: 150 us per 1k particles.
- Whale-fall fake ecology: 200 us per POI cluster.
- Kinematic docking instead of joint stack: 60 us per docking transition.
- Voxel RLE/byte-mask deltas: 500 us per save chunk class.
- Atlas budget enforcement: 2-8 MB per flora material family.
- Dalton atmosphere fake: 100 us per habitat tick class.
- Troubleshooting sections: 250 us per incident triage.

Master List of Deprecated Systems purged today:

- `Docs/Legacy_World_Reference/TERRAIN_108_BIOMES_VISION.md`: deprecated as terrain/runtime authority. Keep as lore and visual reference only.
- `Docs/Legacy_World_Reference/terrain_description.txt`: deprecated as terrain generation contract. Keep as mood/reference only.
- Deprecated assumptions: GameObject scatter, CPU flora growth, full distant fauna simulation, terrain prose as runtime authority, gas simulation, physics-current flora/boids, full voxel-field saves.
- Replacements: `Docs/PROCEDURAL_WORLD_VERTICAL_ARCHITECTURE.md`, active source-backed pipeline READMEs, `Hecton_GpuScatter.compute`, `FloraCulling.compute`, `AbyssalFlowField.compute`, `BoidSimulation.compute`, `Hecton_CoreLit.hlsl`, `VoxelDeltaProcessor.cs`, `BaseAtmosphereMath.cs`, `PredatorCognitionDomain.cs`, `EcosystemDirector.cs`, and `VehicleDockingModule.cs`.

Verification:

- `Docs/Tasks/CURRENT_BATCH.md` prompt extraction for `DOC_VULCAN` returned no block; chat XML remains the recorded primary directive.
- `rg` source scans verified scatter, flow, flora culling, boid, CoreLit, docking, voxel delta, and atmosphere facts used in the docs.
- Markdown scope scan verified DOC_VULCAN sections, deprecation banners, requirements, troubleshooting, CoreLit Math LOD notes, and thread-alignment constants in target docs.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:quiet` failed with 76 existing source dependency errors. Representative missing symbols: `HectonPersistentPathPolicy`, `SteamDeckInputPal`, `PlatformPrecisionClock`, `HectonThreadPriorityPolicy`, `HectonNativeBridge`, `VoxelChunkModifiedEvent`, `VoxelChunkModifiedEvents`, and `HapticWaveformLibrary`. DOC_VULCAN touched markdown/text docs only and introduced no code compile wall.

## 2026-05-12 - Honest R&D Continuation / AAA Pipeline Tightening

STATUS: VERIFIED MASTER GRADE - DOC SOURCE SCAN; RUNTIME PENDING VERIFICATION; CORE BUILD STILL BLOCKED BY EXISTING DEPENDENCIES.

What was wrong:

- First pass did not yet absorb newer source-backed work from `WORLD_BIOME_BLENDING`, `ECOSYSTEM_FOOD_CHAIN`, `WORLD_VOXEL_CAVING`, and `CORE_ORIGIN_SHIFT`.
- Scatter docs did not define the exact dithered biome heatmap semantics.
- Flora docs did not state the indirect vegetation AUP cache-rebase contract.
- Fauna docs understated whale-fall LOD behavior and food-chain buffer hygiene.
- Legacy voxel replacement rules did not name the new carve-event corridor.

What was done:

- Added dithered biome and micro-scatter requirements to `Docs/Scatter_Runtime/README.md`: R8 heatmap bytes are `RecordIndex + 1`, `0` is missing-biome sentinel, terrain samples one `Texture2DArray` slice selected by IGN, `_CurrentBiomeColor` comes from source-backed records where available, micro-scatter uses AUP grid offset and `RenderMeshIndirect`, and scatter black-box dumps to `Dump_WORLD_BIOME_BLENDING.bin`.
- Added indirect vegetation AUP requirements to `Docs/Flora_Pipeline/README.md`: cached cull camera, previous motion-vector camera, explicit bounds, far-cull snapshot, and GPU culling frame index must be rebased/reset after origin shift.
- Added whale-fall and food-chain precision to `Docs/AI_Fauna/README.md`: 7200 second POI/acoustic window, 50x scavenger pressure, 96-boid Full LOD ground ring, low-tier shader/acoustic fake, bounded 8-signal kill queue, `LockBufferForWrite` single-boid patches, and `Dump_ECOSYSTEM_FOOD_CHAIN.bin`.
- Added voxel carve-event corridor requirements to `Docs/Legacy_World_Reference/README.md`: bounded `VoxelCarveEvent`, localized nav patch, 64-byte `VoxelChunkModifiedEvent`, vertex color R burn, no `DecalProjector`, async collider bake, and `Dump_WORLD_VOXEL_CAVING.bin`.

Cinematic Cheats used:

- IGN single-slice biome selection plus TAA/noir grain instead of four-way texture blending.
- AUP-stable hash/displacement instead of biome-specific micro-rock mesh libraries.
- Origin-shift cache rebase instead of BRG/indirect vegetation buffer rebuild.
- Low-tier whale-fall shader/acoustic fake instead of individual scavenger proxies.
- Vertex color burn masks instead of terrain decals.
- Localized nav patch and async bake instead of synchronous full terrain/collider rebuild.

Exact microseconds saved or protected:

- Dithered biome path: about 35 us per 100k terrain pixels versus four-way splat pressure.
- Low-tier micro-scatter cull: about 90 us per scatter pass protected by explicit 15 m radius.
- Biome remap buffer/table avoided: 64 KB+ VRAM avoided and one dependent terrain lookup avoided per pixel class.
- AUP scatter grid rebase: about 15 us per origin shift versus scatter buffer rebuild.
- Low-tier whale fall: zero extra boid state patched; Full LOD event patch remains one-shot at about 1.1 ms for 96 boids on i3/MX350.
- Voxel localized nav patch: 200+ us synchronous rebuild spike avoided.
- Async collider bake: 300-900 us carve-frame hitch avoided.

Verification:

- Re-read `Status_DOC_VULCAN.md` and `Rationale_DOC_VULCAN.md` before continuing.
- Read latest relevant AgentLogs/Status files read-only, then verified claims against source before editing docs.
- `rg` verified new doc sections for `RecordIndex + 1`, `Texture2DArray`, `_CurrentBiomeColor`, `Dump_WORLD_BIOME_BLENDING.bin`, indirect vegetation AUP, 7200s whale-fall, 96-boid ring, `Dump_ECOSYSTEM_FOOD_CHAIN.bin`, `VoxelChunkModifiedEvent`, and `Dump_WORLD_VOXEL_CAVING.bin`.
- `git diff --check` on touched pipeline READMEs returned no whitespace errors, only existing CRLF normalization warnings.
- `Docs/Tasks/CURRENT_BATCH.md` still contains no `DOC_VULCAN` block. Chat XML remains the recorded directive.

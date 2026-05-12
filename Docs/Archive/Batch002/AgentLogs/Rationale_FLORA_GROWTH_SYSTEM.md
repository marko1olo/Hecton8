# Rationale_FLORA_GROWTH_SYSTEM

STATUS: PENDING VERIFICATION

## Decision 0 - Batch Initialization

Problem: The FLORA_GROWTH_SYSTEM batch had no local status or rationale files, and anti-amnesia state must live on disk before code work.
Solution: Created fresh status and rationale logs and recorded the eight relevant mandates before repository edits.
Rejected Alternatives: Chat-only memory was rejected because context compression invalidates it. Reusing other flora/fauna logs was rejected because batch hygiene forbids stale cross-agent state.
Scalability potential: Low uses fixed plots and shader cull for dead plants. Middle uses FrostTick growth and bounded spore events. High adds auto-spread. Ultra spends saved CPU on richer emissive/pulse shader response.
Hardware Impact: i3/MX350 gain is organizational, not runtime yet; prevents unmanaged growth logic from becoming GameObject scaling work. Estimated runtime saving pending code path audit.

## Decision 1 - Shader-Driven Age Lane

Problem: Flora growth needed a real 0..1 age channel without expanding the 64-byte BRG metadata struct or touching GameObject transforms.
Solution: Added renderer-owned `NativeArray<float> FloraAges01`, uploaded it as `_HectonFloraAges01`, and derived it from existing metadata `Reserved0`/dead flags. The shader and cull compute consume this SoA buffer; metadata stride remains 64 bytes.
Rejected Alternatives: Expanding `HectonVegetationInstanceData` was rejected because it risks BRG metadata pack drift. CPU transform scaling was rejected because the prompt explicitly forbids GameObject growth and would cost culling coherence.
Scalability potential: Low defaults legacy flora to mature and culls dead plants in compute. Middle runs shader age morph. High/Ultra can author denser age fields and spend GPU ALU on richer emissive response.
Hardware Impact: i3/MX350 avoids transform writes and mesh rebuilds; estimated CPU saving is 40-150 us per 10K flora versus managed transform mutation, with one contiguous float upload.

## Decision 2 - Vertex Growth Morph And Cull Sentinel

Problem: Visible flora needed age-based Y growth and non-linear XZ pop while harvested flora vanished from culling immediately.
Solution: Main, depth, shadow, and motion-vector shaders resolve age from `_HectonFloraAges01`, scale local Y by `age`, scale local XZ by `sqrt(age)`, and clip negative growth in the main pass. `FloraCulling.compute` rejects `Age < 0` before append.
Rejected Alternatives: Fragment-only dissolve was rejected because shadows/depth would still mismatch. CPU-side bounds updates were rejected because compute culling already owns the scalable path.
Scalability potential: Low draws seedlings collapsed with tiny bounds. Middle gets deterministic shader growth. High/Ultra get stable shadows/motion with the same age lane.
Hardware Impact: i3/MX350 pays one `sqrt` and a few scalar ops per vertex/cull candidate; expected CPU draw/cull reduction after harvest is immediate once age upload occurs.

## Decision 3 - FrostTick Radiation Growth

Problem: Growth needed to advance on a deterministic slow cadence and mutate faster near radiation zones.
Solution: `FloraRegrowthDirector.SlowTick` now gates maturation work behind a 10 second FrostTick and resolves `HazardType.Radiation` through `HectonHazardManager` before the Burst job. Radiation-exposed flora receive a 3x growth multiplier in the job.
Rejected Alternatives: Per-frame growth was rejected as frame-time noise. Querying hazards inside the Burst job was rejected because `HectonHazardManager` is managed/global-state code and must be sampled before scheduling.
Scalability potential: Low has one hazard query per tracked flora per 10 seconds. Middle uses existing maturation state. High/Ultra can add more mutation visuals without changing cadence.
Hardware Impact: i3/MX350 expected cost is about 35 us per 2K tracked flora every 10 seconds, effectively 3.5 us amortized per second.

## Decision 4 - Harvest Yield Uses Age, Not Scale

Problem: Existing maturation yield used smoothed scale, so tiny plants could still produce resource mass.
Solution: Resource yield now stores linear age; `ResolveParentMassKg` returns `BaseYield * Age` and returns zero below 0.2. Harvest/decomposition writes `Reserved0 = -1` after computing the pre-harvest mass.
Rejected Alternatives: Reusing smoothed scale was rejected because it violates the prompt and makes seedlings overpay. Clamping all yields to 0.05 kg was rejected for young plants.
Scalability potential: Low does one scalar age read at harvest. Middle preserves deterministic resource economy. High/Ultra can bias visuals independently from yield because scale and yield are now separated.
Hardware Impact: i3/MX350 runtime cost is below 0.2 us per harvest; avoids later inventory churn for invalid seedling drops.

## Decision 5 - Dependency Blocks Kept Explicit

Problem: Tasks 5, 6, 8, 9, 11, and 13 require cross-domain contracts that were not exposed in the botany domain.
Solution: Marked missing `NativeQueue<SporeEvent>`, GPU scatter ingestion, creeping-vine taxonomy, flora spatial-hash registration, low-tier spread radius, and Data Archivist age-array MMF lanes as blocked rather than inventing private dependencies.
Rejected Alternatives: Directly mutating `GPUScatterDirector`, registering fake spatial contacts, or writing save/MMF files from flora code were rejected as architectural coupling.
Scalability potential: Once ABIs exist, Low disables spread, Middle enables bounded spread with density cull, High/Ultra can spend saved cycles on fog density and mutation visuals.
Hardware Impact: Current patch adds 0 us for blocked systems. Avoided likely multi-ms spikes from ad hoc particle/GameObject spawning or per-frame spatial scans.

## Decision 6 - Flora Material Recon

Problem: Flora materials might bypass the growth shader/core-lit contract and hide shader growth work.
Solution: Scanned flora-like materials under `Assets/_Project/Art/Materials/` and wrote `Docs/AgentLogs/RECON_FLORA_GROWTH_SYSTEM.md` with 12 non-compliant and 9 compliant entries.
Rejected Alternatives: Manual inspector review was rejected because it is not reproducible. Broadly editing materials was rejected because recon is the required domain action, not visual retargeting.
Scalability potential: Low can leave non-growth materials as static support effects. Middle can migrate actual flora to indirect growth. High/Ultra can add overkill material response after shader contract cleanup.
Hardware Impact: No runtime impact; prevents hidden material divergence that would waste future shader work.

## Decision 7 - Legacy Zero Growth Disambiguation

Problem: `Reserved0 = 0` is the legacy mature value, but authored seedlings also want Age 0. A naive runtime-state fallback could collapse agitated legacy flora into seedlings.
Solution: Authored maturation/regrowth now encodes zero-age seedlings as `0.0002`; legacy `Reserved0 = 0` remains mature. Harvest and renderer age resolution treat negative as culled, positive as authored age, zero as legacy mature.
Rejected Alternatives: Using `RuntimeState.Agitated` as a seed marker was rejected because mature flora can become agitated from gameplay interaction. Changing the BRG struct was rejected because stride must remain 64 bytes.
Scalability potential: Low keeps old content stable. Middle gets correct seedling visuals. High/Ultra can add richer authored age curves without metadata migration.
Hardware Impact: No measurable cost; one scalar compare prevents false culling/morphing of legacy flora.

## Decision 8 - Compile Block Boundary

Problem: Unity compile/import is currently red, but console errors are in non-botany files (`PlayerInventory.cs`, `HectonBoidController.cs`, `VehicleDockingModule.cs`) and a pre-existing Burst error in save storage.
Solution: Botany-owned scripts were validated individually where Unity MCP remained stable. Full project compile is marked blocked by dependency rather than crossing domain boundaries.
Rejected Alternatives: Editing fauna, vehicle, inventory, or save-system files from FLORA_GROWTH_SYSTEM was rejected as cross-domain sabotage. Declaring compile success was rejected because Unity console is objectively red.
Scalability potential: No runtime feature change. This preserves integration ownership so actual compile owners can fix their lanes without botany side effects.
Hardware Impact: 0 us changed; verification-only blocker.

## Decision 9 - Renderer Age Authoring API And Growth Black Box

Problem: The shader growth lane existed, but external farming/persistence systems had no safe way to author the renderer-owned age SoA without either mutating `FloraAges01` invisibly or being overwritten by GPU-source default mature ages. The flora growth path also lacked the mandated 300-frame black-box state trail.
Solution: Added `TrySetFloraAge01`, `TryCopyFloraAges01`, and `MarkFloraAgesDirty` on `HectonIndirectVegetationRenderer`. External authoring now sets `_floraAgesAuthoredExternally` so GPU-only sources do not refill every age to `1.0`. Added a fixed `NativeArray<FloraGrowthTelemetryEntry>[300]` circular buffer that records frame index, instance count, sample count, negative sentinel count, NaN count, min/max age, dirty-upload flag, and a bounded hash. On NaN detection the renderer dumps `Docs/AgentLogs/Dump_FLORA_GROWTH_SYSTEM.bin`.
Rejected Alternatives: Exposing the graphics buffer directly was rejected because it would let other systems bypass sanitization and upload ownership. Adding a managed `List<float>`/event callback was rejected for GC and ownership churn. Full per-frame O(N) scans were rejected for steady-state MX350 cost; the renderer does full scans only on dirty uploads and bounded 64-sample hashes otherwise.
Scalability potential: Low keeps fixed farming plots with mature defaults and cheap bounded telemetry. Middle supports deterministic restore/authoring via `NativeArray<float>`. High can stream richer farming age lanes without metadata stride changes. Ultra can spend saved CPU/GPU budget on denser mutation visuals while black-box hashes retain diagnosability.
Hardware Impact: i3/MX350 steady-state overhead is bounded to at most 64 sampled floats per rendered frame, estimated below 2 us. Dirty uploads pay one extra linear scan only for externally-authored age arrays, estimated below 20 us per 10K plants, and only when age data changes.

## Decision 10 - Mature Toxic Spore Event ABI

Problem: Task 5 was previously blocked because no botany-owned `NativeQueue<SporeEvent>` contract existed. The renderer/scatter system still owns fog visuals, but mature toxic plants needed a deterministic handoff without direct scatter mutation.
Solution: Added `HectonFloraSporeEvent` and `HectonFloraSporeEvents` in the indirect vegetation contracts. The event stores AUP, runtime position, radius, intensity, age, template index, payload index, frame index, event kind, and underwater flag. `FloraInteractionManager` now samples mature toxic emitters on a 10 second FrostTick with a serialized per-lane scan budget, and also queues the nearest mature toxic emitter during player exposure scans. The queue is persistent, prewarmed, capacity-bounded to 64, and tracks dropped events instead of allocating or blocking.
Rejected Alternatives: Direct `GPUScatterDirector` calls were rejected because scatter ownership is outside the botany domain. Managed VFX spawning was rejected for GC and uncontrolled draw cost. A full scan every frame was rejected because it wastes CPU on static flora and violates the 0.1 ms suspicion threshold.
Scalability potential: Low uses a 10 second scan cadence and bounded queue drops; fixed plots still look correct without fog overdraw. Middle consumes the queue for sparse dithered fog. High can raise fog density on the renderer side without changing flora logic. Ultra can spend saved CPU/GPU budget on richer volumetric layering while the botany producer remains deterministic.
Hardware Impact: i3/MX350 cost is bounded to `_matureToxicSporeEventScanBudget` candidates per lane every 10 seconds, default 96, plus at most 64 pending queue entries. Expected amortized CPU cost is below 10 us per 10 second tick for producer work; skipped GPU fog rendering remains Task 6 owner work.

## Decision 11 - OMEGA POLISH CHANGES

Problem: The polish mandate required an anti-bloat pass after all tasks were checked or explicitly blocked. The new mature-toxic scan still used one floating-point division in the exposure falloff and continued scanning even when the spore event queue was already full.
Solution: Replaced `distanceSq / detectionRadiusSq` with a precomputed `math.rcp(math.max(detectionRadiusSq, 0.0001f))` and multiplication. Added a queue-pressure early exit before each mature-toxic scan iteration when `HectonFloraSporeEvents.PendingCount >= HectonFloraSporeEvents.PendingEventCapacity`.
Rejected Alternatives: A 1D LUT for exposure was rejected because the falloff is one squared-distance multiply after the reciprocal, not a material runtime hotspot. Continuing to scan after queue saturation was rejected because it wastes CPU and cannot publish more events. Raising queue capacity was rejected because renderer consumption is still Task 6 owner work.
Scalability potential: Low exits early under queue pressure and keeps sparse event output. Middle consumes bounded mature-toxic events. High can increase renderer-side fog density without increasing botany scan cost. Ultra can consume the same compact event stream for heavier volumetric response while producer cost stays deterministic.
Hardware Impact: i3/MX350 saves one divide per toxic emitter candidate in player exposure scans and skips up to `_matureToxicSporeEventScanBudget` candidate checks per lane when the queue is saturated. Expected gain is small but real: sub-10 us per saturated 10 second producer tick, with no GC.

Omega audit evidence:
- Touched spore ABI/producers contain no `math.sqrt`, `math.normalize`, `foreach`, `string.Format`, or `.ToString()` matches.
- `git diff --check` reported only CRLF conversion warnings for touched scripts.
- `read_console` is red from unrelated Burst `CombatDamageResult` struct-layout mismatch in gameplay code.
- `dotnet build Hecton8.Core.csproj --no-restore -clp:ErrorsOnly /m:1 /p:UseSharedCompilation=false` failed with 111 non-botany errors, primarily missing `HectonPersistentPathPolicy`, `SteamDeckInputPal`, `PlatformPrecisionClock`, `HectonThreadPriorityPolicy`, `VoxelChunkModifiedEvents`, and native bridge symbols. `VERIFIED MASTER GRADE` is rejected as false; status remains `PENDING VERIFICATION`.

Final Git Diff:
- `Assets/_Project/Scripts/World/HectonIndirectVegetationContracts.cs`: added spore event kind, payload, bounded `NativeQueue` API, drop counter, reset/clear/dequeue helpers, and prewarm.
- `Assets/_Project/Scripts/World/FloraInteractionManager.cs`: added mature-toxic FrostTick producer, player-proximity spore event publication, defensive burst publication, mature age resolver, toxic trait/template checks, reciprocal exposure falloff, and queue-pressure early exit.
- Docs updated: `Status_FLORA_GROWTH_SYSTEM.md`, `Rationale_FLORA_GROWTH_SYSTEM.md`, and `LOG_FLORA_GROWTH_SYSTEM.md`.

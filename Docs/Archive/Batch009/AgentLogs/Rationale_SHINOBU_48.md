# SHINOBU_48 Rationale

Status: PENDING VERIFICATION

## Intake

Problem: Build the Seed Ship anomaly without TriggerColliders, FindObjectsOfType, boss-script coupling, or per-entity hot-path garbage while supporting a 50,000-entity scene.
Solution: Use a single Vault-owned scalar field and Burst distance math; route optional domain effects through aligned DTOs, mock signals, and DataVault-style buffers.
Rejected Alternatives: Unity TriggerCollider volume, GameObject search, custom boss minion scripts, and direct renderer/HUD/AI concrete references; each violates cache locality, decoupling, or zero-GC rules.
Scalability potential: Low uses player/global scalar only; Middle adds bounded predator-page batches; High adds denser mock AI corruption and shader heat/radar pulses; Ultra spends saved CPU on visual corruption intensity, not simulation breadth.
Hardware Impact: i3/MX350 target expects entity-specific processing to be quality-weight gated; replacing trigger callbacks over 50,000 objects with one scalar plus bounded NativeArray pass avoids physics broadphase churn and managed callback pressure.

## Relevant Mandates

- ARCH_Global_Registry_ServiceLocator_DI_Init: GlobalRegistry is cold discovery only; hot paths cache dependencies and use typed DataVault/signal contracts.
- ARCH_Execution_Phases: anomaly math belongs in PRE_SIMULATION/SIMULATION; telemetry and signal publication belong in POST_SIMULATION; shader globals/tuner visuals belong in VISUAL_SYNC or Editor-only.
- OPT_Zero_GC_Policy_AllocFree_Mandate: no LINQ, strings, Find, TriggerCollider reliance, or allocations in gameplay paths.
- OPT_Native_Memory_Collections_JobSystem_Protocol: persistent native state must be Vault-owned or explicitly wrapped with owner/system id and disposal path.
- MATH_Coordinate_Precision_AUP_FloatingOrigin: subtract double3 AUP values before casting to float3 for distance math.
- MATH_Rsqrt_i3_SIMD: use rsqrt/rcp style math with finite checks for cheap radial falloff.
- DBG_Telemetry_Crash_Reporting_PostMortem: 300-frame blackbox ring and binary dump on budget/NaN breach.
- AI_Creature_Cognition_States: frenzy is scalar utility/weight injection, not a bespoke boss AI tree.

## Loop 1 Decisions

Problem: Legacy Batch005-007 binary anomaly tables may or may not exist, and blocking on them would stall the endgame runtime.
Solution: Add cold-path archaeology across Docs/Archive and StreamingAssets for `seed_ship_emission_rates.h8bin` and `glitch_zones_007.bin`; if absent or unreadable, initialize deterministic emergency mock anomaly rows in Vault.
Rejected Alternatives: Hard failing boot on missing historical files, or parsing legacy blobs in a frame tick. Both would make current runtime hostage to archived content.
Scalability potential: Low/Middle/High/Ultra all get the same one-field scalar truth; legacy content only tunes constants.
Hardware Impact: i3/MX350 avoids startup retry loops and hot-path file IO; cold scan cost is outside frame budget.

Problem: A 3km Seed Ship zone implemented as a Unity trigger would push 50,000 fauna/particle collider callbacks through PhysX and managed MonoBehaviour dispatch.
Solution: Store `AnomalyFieldDTO` as one 48-byte Vault row and evaluate AUP distance in Burst using subtract-before-cast math.
Rejected Alternatives: `SphereCollider`, `IsTrigger`, `OnTriggerEnter/Stay`, scene search, and overlap queries.
Scalability potential: Low keeps global/player scalars; Middle/High/Ultra spend extra budget on mock predator rows and visual overkill scalars.
Hardware Impact: Replaces broadphase/callback churn with one scalar field plus a bounded NativeArray job.

Problem: Struct-property mutation would reintroduce CS1612 copies and hide the hot field behind accessors.
Solution: DTOs expose raw fields, and `SeedShipAnomalyRuntime.GetAnomalyFieldRef()` returns a ref directly into Vault memory through `VaultBufferHandle<T>.GetElementAsRef`/UnsafeUtility.
Rejected Alternatives: `{ get; private set; }`, wrapper classes, or editor-facing managed mirrors as runtime authority.
Scalability potential: Low-to-Ultra uses identical contiguous data with no object graph.
Hardware Impact: L1-friendly 48-byte field row; no stack-copy mutation hazard.

Problem: ARM64 consumers need deterministic payload alignment for UI/radar/mock boundaries.
Solution: `AnomalyFieldDTO` is explicit 48 bytes and `GlitchCommandDTO` is explicit 16 bytes; tests assert size and offsets.
Rejected Alternatives: Pack=1 or compiler-default sequential layout without offset tests.
Scalability potential: Same ABI works across weak ARM64 and desktop; higher tiers only read richer scalar values.
Hardware Impact: Avoids unaligned trap risk and keeps UI command read to one 16-byte payload.

Problem: Origin-shift, HUD, and predator systems are concurrent-agent domains not available as direct dependencies.
Solution: Add `MockAupRebaseSignal`, `MockHudSignal`, `MockLeviathanState`, and typed signal lanes; the runtime proves rebase handling and external scrambling without concrete references.
Rejected Alternatives: Direct Agent 30/07/24 class calls or waiting for their code.
Scalability potential: Low may emit only globals; Middle/High/Ultra increase mock row budgets continuously.
Hardware Impact: Signal/Vault handoff avoids managed event fanout under 50,000 entities.

## Loop 2 Decisions

Problem: Corruption must be the single endgame horror truth while staying under 0.1ms.
Solution: `SeedShipAnomalyFieldJob` computes `CorruptionLevel` from player AUP to `EpicenterAUP` using subtract-before-cast, smooth radial falloff, and rsqrt damping. The result is written to `AnomalyFieldDTO` and `AnomalyGlobalScalarsDTO`.
Rejected Alternatives: Per-object MonoBehaviour infection, terrain deformation, or many local zone checks.
Scalability potential: Low reads one player/global scalar; Middle adds bounded AI rows; High/Ultra can spend more budget on mock predators, radar pulses, shader corruption and thermodynamics scalars.
Hardware Impact: i3/MX350 pays one Burst singleton job plus a continuous entity budget instead of 50,000 trigger callbacks.

Problem: Gravity inversion must terrify the player without rewriting every physics owner.
Solution: `GravityY` is a Vault scalar generated by a sine oscillator gated through smoothstep around corruption 0.5, moving continuously between normal 9.80665 and -2.0.
Rejected Alternatives: Direct Rigidbody force injection or a binary `if corrupted then flip gravity` switch.
Scalability potential: Low/Middle/High/Ultra all read the same scalar; richer devices can use it for more visual/physical secondary effects.
Hardware Impact: One float write lets Exosuit/debris consumers react without extra lookups.

Problem: Visual corruption must not instantiate corrupted world objects or deform collision meshes.
Solution: `SeedShipAnomalyShaderBridge` publishes a Vault shader slot plus shader globals for corruption, universe-offset noise, heat and radar intensity. Physics collision remains stable.
Rejected Alternatives: Mesh edits, spawned corrupted props, or renderer-specific direct references.
Scalability potential: Low uses cheap global UV/noise scalar; High/Ultra can interpret the same scalar as heavier material overkill.
Hardware Impact: CPU cost is a single slot write and two shader global uploads after job completion.

Problem: Predator frenzy needs to affect AI without a boss-minion script or fauna-domain dependency.
Solution: `SeedShipLeviathanFrenzyJob` mutates `MockLeviathanState` rows inside a quality/corruption-derived budget, raising aggression and suppressing light aversion.
Rejected Alternatives: Direct PredatorCognitionDomain calls or custom boss guardian behaviours.
Scalability potential: Budget scales from a few rows to 50,000 via continuous `GlobalQualityWeight`.
Hardware Impact: On MX350, entity work collapses with `quality^4`; on high-end, saved cycles buy more frenzy rows.

Problem: Technology jamming must cross HUD/scanner boundaries without managed event fanout.
Solution: The field job pushes `RadarJamSignal` only near oscillator peaks and mirrors HUD glitch commands through `MockHudSignal`.
Rejected Alternatives: Polling scanner/HUD objects or allocating managed events.
Scalability potential: Low emits sparse jam pulses; High/Ultra can raise pulse interpretation density downstream.
Hardware Impact: NativeQueue signal write is bounded and load-shed by the typed SignalBus.

## Loop 3 Decisions

Problem: The prompt's low-tier guidance could become a forbidden binary switch if implemented literally as `Weight < 0.5`.
Solution: Entity-specific corruption budget is `quality^4 * corruptionGate`; at weak weights it continuously collapses toward zero while player/global/shader scalars remain active.
Rejected Alternatives: Hard `if low then skip all entities`, quality enum tiers, or all-entities iteration regardless of thermal pressure.
Scalability potential: Low processes only core/global horror; Middle ramps partial predator rows; High/Ultra can evaluate full 50,000 mock rows if budget allows.
Hardware Impact: i3/MX350 load drops smoothly with `GlobalQualityWeight`, preventing 50,000-entity spikes.

Problem: Babel text corruption must not allocate strings while scrambling logs, and the polish mandate rejected even cold private table complacency.
Solution: `SeedShipAnomalyMath.ScrambleUtf8Bytes` mutates caller-owned UTF-8 spans in place using `Unity.Mathematics.Random` and resolves default glyph bytes through a switch, not a private `byte[]`.
Rejected Alternatives: managed string replacement, regex, TMP string rebuilds, per-call byte-array creation, handwritten LCG drift, or a static managed glyph table hidden behind `ReadOnlySpan<byte>`.
Scalability potential: Low corrupts fewer bytes via lower corruption scalar; Ultra can use denser glyph replacement without changing API.
Hardware Impact: Span mutation is O(n) over the requested text only and creates no hot-path GC.

Problem: 5km/deep/endgame AUP positions can jitter if absolute doubles are cast to floats too early.
Solution: All distance solvers subtract `double3 actorAup - epicenterAup` first, then cast the local delta to `float3`.
Rejected Alternatives: float absolute world positions or runtime `Vector3.Distance` at huge coordinates.
Scalability potential: Same math remains stable at map edge for Low through Ultra.
Hardware Impact: Double subtract plus float dot product keeps precision without expensive all-double distance loops.

Problem: Thermodynamics and radiation need anomaly heat without direct ownership of Agent 16.
Solution: Runtime mirrors `AnomalyThermoSourceDTO` and emits `RadiationSourceSignal`/`RadiationDoseSignal` with the Seed Ship epicenter and pulsing heat/radiation scalars.
Rejected Alternatives: Concrete thermodynamics grid calls or per-entity damage triggers.
Scalability potential: Low still gets global heat/radiation source; higher tiers can render/diffuse richer fields downstream.
Hardware Impact: One source signal replaces thousands of overlap/damage checks.

Problem: Narrative hacking must heal the world mathematically.
Solution: `CoreHackedSignal` with the accepted code starts a 10-second scalar decay; gravity, shaders, predator frenzy and radiation converge as corruption falls.
Rejected Alternatives: scene animation timelines, toggling systems off, or direct quest/HUD dependencies.
Scalability potential: All tiers share the same scalar decay; high-end only changes visual interpretation.
Hardware Impact: One timer and one subtractive scalar path, no object traversal.

## Loop 4 Decisions

Problem: Booting the anomaly must not zero large buffers repeatedly during the endgame transition.
Solution: Vault handles are allocated with `NativeArrayOptions.UninitializedMemory`; initialization writes exact singleton, telemetry, CSV and mock rows once in `GenerateEmergencyMockAnomalies`.
Rejected Alternatives: per-frame DTO allocation, ClearMemory for 50,000 mock rows, or lazy managed objects.
Scalability potential: Low/Middle/High/Ultra share the same resident buffers; only scheduled row count changes.
Hardware Impact: Avoids OS/allocator clear cost during the 5km activation path.

Problem: A >0.1ms anomaly solve needs postmortem proof, not excuses.
Solution: 300 `AnomalyTelemetryEntry` rows track corruption, entities, compute ms, gravity/radar/heat/quality and state hash; budget/NaN breach dumps `Docs/AgentLogs/Dump_SEED_SHIP_ANOMALY.bin`.
Rejected Alternatives: log-only diagnostics, exceptions without state, or managed list telemetry.
Scalability potential: Low keeps the same blackbox; High/Ultra can breach-dump richer state without changing format.
Hardware Impact: Ring write is fixed-size; binary dump is cold error path.

Problem: Human tuning must edit actual runtime data instead of serialized shadows.
Solution: `SeedShipAnomalyTunerWindow` reads/writes Vault handles for field/tuning/global rows in Play Mode and can inject `CoreHackedSignal`.
Rejected Alternatives: ScriptableObject mirror, scene singleton lookup, or inspector-only dead values.
Scalability potential: Designers can tune Low/Middle/High/Ultra constants against one live scalar model.
Hardware Impact: Editor-only, 0us player runtime.

Problem: Architects need live profile edits without a managed CSV parser or private persistent scratch arrays.
Solution: SlowTick monitors `anomaly_profiles.csv`, reads into `BufferID.ShinobuSeedShipAnomalyIoScratch`, hashes ASCII keys, parses floats from spans and writes Vault tuning fields.
Rejected Alternatives: `string.Split`, LINQ, regex, per-frame file polling, or a private managed read buffer.
Scalability potential: Same CSV keys can lower heat/radar/entity budgets on weak devices or raise overkill on high-end.
Hardware Impact: Cold/slow IO only; gameplay Tick remains allocation-free.

Problem: The 3km corruption sphere is invisible and too dangerous to debug by guesswork.
Solution: Editor SceneView gizmo draws red outer radius and yellow gravity inversion radius directly from Vault field/global rows.
Rejected Alternatives: runtime debug GameObjects or trigger-volume visualizers.
Scalability potential: Editor-only visualization works for all quality levels.
Hardware Impact: 0us player build cost.

<SELF_AUDIT id="SHINOBU_48">
  <no_triggers_findobjects status="PASS">Static scan of SHINOBU_48 files returned no `SphereCollider`, `IsTrigger`, `OnTrigger`, `FindObjectsOfType`, `FindObjectOfType`, `OverlapSphere`, LINQ, or managed list hot-path matches.</no_triggers_findobjects>
  <arm64_layout status="PASS">`AnomalyFieldDTO`: double3 EpicenterAUP offset 0 size 24; float Radius offset 24; float CorruptionLevel offset 28; uint GlitchHash offset 32; uint _pad0 offset 36; ulong _pad1 offset 40; total 48 bytes. `GlitchCommandDTO`: 4+4+4+4 total 16 bytes.</arm64_layout>
  <cs1612 status="PASS">DTOs expose fields, not properties. Runtime direct field mutation uses `VaultBufferHandle<T>` and `GetAnomalyFieldRef()` for ref access into unmanaged Vault memory.</cs1612>
  <global_quality_weight status="PASS">Entity corruption budget consumes continuous `GlobalQualityWeight` via `quality^4` and corruption smoothstep; global player/shader/radiation/gravity scalars remain active as budgets approach zero.</global_quality_weight>
  <editor_facade status="PASS">`SeedShipAnomalyTunerWindow` reads and writes Vault field/tuning rows in Play Mode and draws red/yellow SceneView radius gizmos.</editor_facade>
</SELF_AUDIT>

## Loop 6 Ultra-Polish Decisions

Problem: The first pass still had two private managed arrays (`_ioReadBuffer`, `_dumpScratch`) and a static glyph byte table. Even if cold, that violated the literal Vault sovereignty audit.
Solution: Added `BufferID.ShinobuSeedShipAnomalyIoScratch` and `BufferID.ShinobuSeedShipAnomalyDumpScratch`; all CSV, legacy binary and dump staging now resolves through `VaultBufferHandle<byte>`. Replaced default glitch table with switch-resolved glyph bytes.
Rejected Alternatives: Keeping cold `byte[]` fields with comments, pooling managed arrays, or loading `GlitchTable.bytes` into a private runtime cache.
Scalability potential: Low has zero additional persistent managed memory; Middle/High/Ultra reuse the same Vault scratch while spending saved memory discipline on shader/global scalar richness.
Hardware Impact: i3/MX350 avoids hidden managed heap retention and GC root pressure; byte scratch remains explicit Vault-owned native memory.

Problem: Burst alias analysis could not prove SHINOBU_48 Vault arrays were disjoint.
Solution: Added `[NoAlias]` to every `NativeArray<T>` field in `SeedShipMockAupRebaseJob`, `SeedShipAnomalyFieldJob`, and `SeedShipLeviathanFrenzyJob`, with `[ReadOnly, NoAlias]` on read-only inputs.
Rejected Alternatives: Trusting Burst to infer non-overlap from caller intent, or hiding arrays behind interface dispatch.
Scalability potential: Low still pays singleton math; High/Ultra can get cleaner NEON/AVX vectorization on entity frenzy rows.
Hardware Impact: Potential false-alias stalls removed for ARM64 and desktop SIMD; exact gain requires Unity profiler after project lock clears.

Problem: Runtime frame stamping still used Unity frame count, which is not rollback authority.
Solution: Added `_simulationFrameCounter`, incremented from dispatcher `Tick`, and fed it to Burst jobs, CSV override records and core-hack runtime signals. The only remaining `Time.frameCount` in SHINOBU_48 is editor-only tuner signal stamping.
Rejected Alternatives: `Time.frameCount` as simulation state, or Unity `Time.deltaTime` reads inside jobs.
Scalability potential: Deterministic scalar fields remain identical across Low/Middle/High/Ultra for the same dispatcher frame stream.
Hardware Impact: No CPU gain; removes rollback/desync risk.

Problem: Domain code was still conceptually inside the root project assembly until Unity import regenerated, increasing compile-wall blast radius.
Solution: Added `Hecton8.SeedShipAnomaly.Runtime.asmdef` and `Hecton8.SeedShipAnomaly.Editor.asmdef`. Runtime references only Core, Core.Contracts, Core.Memory and Unity Burst/Collections/Jobs/Mathematics.
Rejected Alternatives: Direct references to AI, HUD, Rendering, Physics, Vehicles, Audio, Inventory, Logistics or Terrain assemblies.
Scalability potential: Domain changes recompile the anomaly assembly and its explicit consumers instead of the full root monolith after Unity import.
Hardware Impact: Developer iteration time reduction, not frame-time impact.

Problem: SHINOBU-specific signal DTOs originally lived in the anomaly runtime file, which would force HUD/scanner/AI consumers to reference the SeedShip runtime assembly to receive signals.
Solution: Moved `GlitchCommandDTO`, `MockAupRebaseSignal`, `RadarJamSignal`, `CoreHackedSignal`, and `MockHudSignal` into `Hecton8.Core.Contracts.Signals` via `Assets/_Project/Scripts/Core/Contracts/SeedShipAnomalySignals.cs`.
Rejected Alternatives: Keeping signal contracts in the producer runtime, or adding sibling HUD/AI references back into SHINOBU_48.
Scalability potential: Low/Middle/High/Ultra consumers can subscribe to contract signals without pulling anomaly implementation code.
Hardware Impact: No frame-time effect; reduces C# rebuild fanout and future domain dependency cycles.

Problem: A nonzero designer `MinEntityBudget` could override the low-tier mathematical collapse and keep hundreds or thousands of rows active on weak hardware.
Solution: `ResolveEntityBudget` now smooth-ramps the minimum floor itself with `SmoothStep(0.35, 0.75, quality)`, then applies `quality^4`, corruption smoothstep and a `math.step` zero guard.
Rejected Alternatives: trusting CSV authors not to set a floor, or hard-binary low-end overrides.
Scalability potential: Low collapses below 100 rows even with a 1000-row designer floor; Middle ramps gradually; High/Ultra restores the intended floor and full overkill budget.
Hardware Impact: i3/MX350 avoids accidental forced 1000-row passes during thermal collapse.

Problem: The first Burst pass used `FloatMode.Fast`, but the anomaly writes authoritative global state that must survive deterministic rollback across x86 and ARM64.
Solution: Switched SHINOBU_48 Burst jobs to `FloatMode.Deterministic` with `CompileSynchronously = true` and `FloatPrecision.Standard`.
Rejected Alternatives: Keeping fast math for critical simulation truth and hoping shader-only differences stayed cosmetic.
Scalability potential: Low/Middle/High/Ultra share identical scalar truth; visual overkill remains downstream in shaders.
Hardware Impact: Minor ALU cost on weak devices, traded for desync prevention.

Problem: Wall-clock compute time is nondeterministic and must not mutate rollback-visible global flags.
Solution: `CompleteFrameJob` now writes elapsed milliseconds and budget breach only into telemetry/dump diagnostics; `AnomalyGlobalScalarsDTO.Flags` remains the deterministic job-authored simulation flag set.
Rejected Alternatives: Embedding `Stopwatch`-derived budget flags in global state consumed by other systems.
Scalability potential: All quality levels can profile locally without desyncing co-op simulation truth.
Hardware Impact: No frame-time gain; removes rollback divergence.

## Loop 8 Resume Verification Decisions

Problem: Context resume created a risk of stale prompt memory and false completion claims.
Solution: Re-read `Status_SHINOBU_48.md`, `Rationale_SHINOBU_48.md`, `AGENTS.md`, the domain boundary document, and re-extracted the exact `<AGENT_PROMPT id="SHINOBU_48">` block from `CURRENT_BATCH.md` by CLI. The initial regex was wrongly escaped and failed; the corrected regex produced the full prompt.
Rejected Alternatives: Relying on compacted chat state or IDE-open tabs.
Scalability potential: No runtime behavior change; protects the task boundary under concurrent agent work.
Hardware Impact: 0 us runtime.

Problem: Build process hygiene was contaminated by lingering compiler workers from earlier probes and a concurrent external/editor build.
Solution: Stopped only the confirmed own `dotnet build C:\hades\Hecton8\Hecton8.Core.csproj --no-restore` chain and its Roslyn worker. Did not kill later `Hecton8.Editor.csproj`/Unity-associated build processes because those are outside SHINOBU ownership.
Rejected Alternatives: Killing every `dotnet.exe`, launching another build while a compiler is active, or editing unrelated Volcanic/Editor domains to force green.
Scalability potential: No runtime behavior change; preserves developer machine safety and concurrent-agent isolation.
Hardware Impact: Avoids CPU contention during verification; 0 us gameplay.

Problem: Final source proof needed objective scans after the ultra-polish changes, not a narrative audit.
Solution: Re-ran targeted `rg` checks. Results: no trigger/search/overlap/random/LCG/Pack=1/LINQ/string.Format matches in SHINOBU_48 paths; no direct sibling-domain namespace matches; no DTO `{ get; set; }` matches; all job `NativeArray<T>` fields carry `[NoAlias]`; all SHINOBU_48 jobs use Burst `FloatMode.Deterministic`.
Rejected Alternatives: Broad repo scans polluted by other agents or manual inspection only.
Scalability potential: Low/Middle/High/Ultra paths stay bound to the same scalar/Vault model without hidden Unity trigger fallback.
Hardware Impact: Confirms the implementation remains O(1) plus bounded O(B), not 50,000-trigger O(N) callback load.

Problem: `git diff --check` could expose whitespace corruption in touched files.
Solution: Ran scoped `git diff --check`; it reported only line-ending normalization warnings for pre-existing modified files `H8Memory.cs` and `Hecton8.EditModeTests.asmdef`, no whitespace errors.
Rejected Alternatives: Repo-wide diff check that would include unrelated dirty worktree edits.
Scalability potential: No runtime change.
Hardware Impact: 0 us runtime.

<SELF_AUDIT id="SHINOBU_48" pass="ULTRA_POLISH_RECHECK">
  <task_reconciliation>
    <task id="01" name="BINARY_GRAVEYARD_RECONNAISSANCE" status="PASS">Cold archaeology searches Docs/Archive and StreamingAssets for legacy emission/glitch blobs; absent data falls back to `GenerateEmergencyMockAnomalies()`.</task>
    <task id="02" name="TRIGGER_COLLIDER_ERADICATION_PASS" status="PASS">No trigger/collider/overlap query exists; scalar AUP distance math replaces the zone.</task>
    <task id="03" name="CS1612_ENCAPSULATION_PURGE" status="PASS">Hot DTOs are fields, not properties; field ref access goes through Vault memory.</task>
    <task id="04" name="ARM64_PADDING_RECONSTRUCTION" status="PASS">`GlitchCommandDTO` is explicit 16 bytes: 0 intensity, 4 frequency, 8 glyph hash, 12 pad.</task>
    <task id="05" name="BLIND_DEPENDENCY_MOCKING" status="PASS">Mock HUD, AUP rebase, radar and leviathan paths prove math without concrete external domains.</task>
    <task id="06" name="BURST_ANOMALY_FIELD_KERNEL" status="PASS">`SeedShipAnomalyFieldJob` computes the Vault corruption scalar from player AUP to epicenter.</task>
    <task id="07" name="GRAVITY_INVERSION_INJECTION" status="PASS">`GravityY` is a continuous scalar oscillator gated by corruption, not per-rigidbody traversal.</task>
    <task id="08" name="THE_DEAR_LIE_SHADER_CORRUPTION" status="PASS">Shader/Vault global payload carries corruption/noise/heat/radar; collision remains stable.</task>
    <task id="09" name="LEVIATHAN_FRENZY_ROUTER" status="PASS">Mock leviathan rows receive aggression/light-aversion scalars inside a continuous budget.</task>
    <task id="10" name="RADAR_JAMMING_PULSES" status="PASS">Oscillator peaks enqueue `RadarJamSignal` and mirror `MockHudSignal` without managed fanout.</task>
    <task id="11" name="CONTINUOUS_SCALABILITY_ANOMALY_LOD" status="PASS">Entity budget is `quality^4 * smooth corruption gate`; global scalars never pop off.</task>
    <task id="12" name="BABEL_CRYPTOGRAPHY_SCRAMBLER" status="PASS">UTF-8 spans mutate in place with Unity.Mathematics.Random and switch-resolved glyph bytes.</task>
    <task id="13" name="AUP_PRECISION_EPICENTER_MATH" status="PASS">Every distance solve subtracts double3 AUP first, then casts local delta to float3.</task>
    <task id="14" name="THERMO_TOXIC_VENTING" status="PASS">Heat/radiation are Vault thermo rows and radiation signals, not trigger damage.</task>
    <task id="15" name="NARRATIVE_HACKING_STATE_LINK" status="PASS">Accepted `CoreHackedSignal` starts deterministic 10s corruption decay.</task>
    <task id="16" name="ZERO_INIT_OVERHEAD_BYPASS" status="PASS">Singleton/mock/telemetry/scratch buffers allocate via Vault with uninitialized boot handles.</task>
    <task id="17" name="TELEMETRY_ANOMALY_RECORDER" status="PASS">300-frame 64-byte telemetry ring dumps through Vault-owned scratch on budget/NaN flags.</task>
    <task id="18" name="ANOMALY_TUNER_EDITOR_WINDOW" status="PASS">EditorWindow edits Vault field/tuning rows directly in Play Mode.</task>
    <task id="19" name="CSV_OVERRIDE_INGESTOR" status="PASS">CSV parser hashes byte spans from Vault IO scratch and overwrites unmanaged tuning.</task>
    <task id="20" name="GIZMO_CORRUPTION_VISUALIZER" status="PASS">SceneView red/yellow radius gizmos read Vault field/global rows.</task>
  </task_reconciliation>
  <struct_layout status="PASS">
    AnomalyFieldDTO = 48 bytes. Offsets: EpicenterAUP double3 0..23, Radius float 24..27, CorruptionLevel float 28..31, GlitchHash uint 32..35, _pad0 uint 36..39, _pad1 ulong 40..47. 24+4+4+4+4+8=48, divisible by 16 and 8. GlitchCommandDTO = 16 bytes. Telemetry and mock leviathan rows are 64 bytes to avoid cache-line false sharing in parallel entity writes.
  </struct_layout>
  <scalability_curve status="PASS">Below GlobalQualityWeight 0.3, entity frenzy budget collapses through `quality^4` and the minimum floor also smooth-collapses, while singleton corruption, gravity, shader, radar, heat and radiation scalars remain active. At weight 1.0 the same scalar source can drive full 50,000 mock rows and richer shader interpretation.</scalability_curve>
  <h_phi_vault status="PASS">No private array allocations remain in SHINOBU_48 runtime. Boot handles: 70700 field, 70701 tuning, 70702 globals, 70703 glitch command, 70704 HUD mock, 70705 leviathans, 70706 AUP rebase, 70707 thermo, 70708 telemetry ring, 70709 CSV overrides, 70710 IO scratch, 70711 dump scratch.</h_phi_vault>
  <pointer_aliasing_dependency_graph status="PASS">Jobs consume the prior scheduled anomaly handle chain and output one `JobHandle` registered through `H8Memory.RegisterActiveJob`. No arbitrary main-thread complete occurs; LateFrame completes only when `IsCompleted` unless disabling. All NativeArray job fields are `[NoAlias]`; all SHINOBU_48 jobs use Burst `FloatMode.Deterministic`; Stopwatch diagnostics are quarantined to telemetry/dump.</pointer_aliasing_dependency_graph>
  <compile_guard status="PASS_WITH_ENV_BLOCKER">`Hecton8.SeedShipAnomaly.Runtime` references Core/Core.Contracts/Core.Memory and Unity only; no direct sibling AI/HUD/rendering/physics asmdef reference. Cross-domain signal DTOs are in Core.Contracts. Unity import/editmode verification is blocked by an open Unity instance.</compile_guard>
  <dear_lie status="PASS">Before: 50,000 collider/entity checks plus physical deformation, O(N) physics callbacks and renderer churn. After: O(1) global scalar field plus O(B) optional entity budget where B = ceil(maxEntities * quality^4 * corruptionGate). Visual infection is shader/global scalar noise, not terrain mutation.</dear_lie>
</SELF_AUDIT>

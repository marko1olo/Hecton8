# Status_SHINOBU_260

Agent: SHINOBU_260
Domain: CREST_VERSION_QUARANTINE_DIRECTOR / Echelon 9 Meta & Integration
Task Count: 20
Status: POLISH PASS LOOP 21 STATIC VERIFIED / TASK 12 BLOCKED BY DEPENDENCY / NO BUILD DUE ACTIVE csc+dotnet CPU GATE
Batch Source: Docs/Tasks/CURRENT_BATCH.md `<AGENT_PROMPT id="SHINOBU_260" role="CREST_VERSION_QUARANTINE_DIRECTOR">`

## Hygiene

- [x] Duplicate `SHINOBU_260` prompt detected.
  - DOD practice: selected the XML block matching both ID and role, not the stale first SHINOBU_260 vocal block.
  - Rejected alternative: acting on the first ID-only match and touching the wrong domain.
  - Estimated saving: prevents unbounded compile-wall and asset-move damage from wrong-role execution.
- [x] Previous vocal status/rationale/log copied to `Docs/Archive/Batch_SHINOBU_260_VOCAL_STALE_20260521/`.
  - DOD practice: preserve forensic trail before replacing active memory files.
  - Rejected alternative: mixing Crest decisions into old vocal task log.
  - Estimated saving: prevents human/agent misrouting during later context compression.

## Selected Mandates

- STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- ARCH_Signal_Lane_Segregation.txt
- DATA_Runtime_Struct_Layout_ARM64.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- MATH_AUP_Determinism_Sync.txt
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt

## Analysis

Target: Crest 4/5 asset quarantine, backup, asmdef wall, and zero-GC ocean boundary.
Affected systems: third-party Crest asset roots, Packages/com.waveharmonic.crest, Assets/Crest, any Assets/Plugins/Crest4/Crest5 roots, Hecton8 ocean/fluids contracts, editor quarantine tooling, reports/logs.
Zero GC proof: runtime bridge contracts must use unmanaged DTOs, `NativeArray`/raw pointer surfaces, no strings, no `IEnumerable`, no managed event delegates in hot paths. Editor archiving may allocate because it is cold tooling.
State check: stale SHINOBU_260 files were vocal-domain; copied to archive and replaced. Crest 5 package and Crest 5 first-party migration tools were moved outside Unity visibility. No dotnet build launched.
Rule quote: `Hecton8.Core` and `Hecton8.Physics` must have zero direct assembly references to Crest; communication flows through `Hecton8.Environment.Fluids.Contracts` interfaces injected during cold boot.

## Loop 1: Tasks 01-05

- [x] Task 01 THIRD_PARTY_ASSET_BACKUP_AUTOMATION
  - DOD practice: `Tools/Crest_Baseline_Archiver.py --execute` zipped actual donor roots before moving anything.
  - Rejected alternative: Unity-visible `Assets/~Quarantine_*` storage was rejected because the user required archive outside Unity visibility.
  - Estimated saving: prevents minutes to hours of Crest re-import recovery after shader/API graft failure; runtime frame cost 0 microseconds.
- [x] Task 02 NAMESPACE_AND_GUID_COLLISION_PURGE
  - DOD practice: moved `Packages/com.waveharmonic.crest` plus Crest 5 first-party adapter/migration/parity `.cs` and `.meta` files to `Docs/Archive/Crest_Version_Quarantine/`, then removed stale lock entry from `Packages/packages-lock.json`.
  - Rejected alternative: leaving Crest 5 package visible with asmdef references removed was rejected because Unity would still scan package shaders/metas and preserve namespace collision risk.
  - Estimated saving: avoids Crest 4/5 duplicate assembly import and package lock churn; expected editor domain/import savings measured in seconds, runtime frame cost 0 microseconds.
- [x] Task 03 ASMDEF_ISOLATION_AND_DEFENSE
  - DOD practice: set Crest 4 runtime/editor asmdefs to `autoReferenced=false`, moved Crest-only editor validation into `Hecton8.Crest.Bridge.Editor`, removed all Crest/WaveHarmonic refs from shared first-party asmdefs, and verified `Crest_Dependency_Scanner.py` `breach_count=0`.
  - Rejected alternative: keeping Crest in `Hecton8.Plugins` was rejected because it lets every unrelated plugin change inherit Crest import volatility.
  - Estimated saving: avoids whole-sibling compile fanout; expected editor reload saving is seconds per Crest API churn, runtime frame cost 0 microseconds.
- [x] Task 04 CS1612_ADAPTER_CONTRACT_ANNIHILATION
  - DOD practice: added `Hecton8.Environment.Fluids` unmanaged `IHectonOceanKinematics` contract with explicit DTO fields only; `OceanSampleRequestDTO` is `[StructLayout(LayoutKind.Explicit, Size=32)]`, `double3 RequestAUP` at 0, `uint CallerHashID` at 24, private pad at 28.
  - Rejected alternative: mutating legacy `Hecton8.Physics.IHectonOceanKinematics` in-place was rejected because parallel agents already compile against that compatibility surface; the strict contract is now the forward route through Environment.Fluids.Contracts.
  - Estimated saving: prevents hidden struct property copies in Burst request lanes; expected hot-path saving 50-200 microseconds at thousands of samples.
- [x] Task 05 EMERGENCY_MOCK_OCEAN_GENERATOR
  - DOD practice: added Burst `EmergencyMockOceanKinematicsAdapter.GenerateEmergencyMockOceanAdapter()` with `NativeArray` request/result buffers, no main-thread `Complete`, no LINQ/foreach, and deterministic sine-based Dear Lie fallback.
  - Rejected alternative: using Crest CPU queries as fallback was rejected because the fallback must survive a broken Crest assembly.
  - Estimated saving: avoids synchronous Crest sampling stalls; expected low-end saving 200-1000 microseconds under heavy buoyancy probes.

## Loop 2: Tasks 06-10

- [x] Task 06 ADAPTER_PATTERN_IMPLEMENTATION_KERNEL
  - DOD practice: added `CrestOceanRuntimeAdapter.cs` inside `Hecton8.Crest.Bridge`; it implements the new Environment.Fluids contract and acquires `OceanRenderer` only during cold bind/Awake/TryGetComponent.
  - Rejected alternative: polling `OceanRenderer.Instance` from hot requests was rejected because it hides singleton lookup and third-party lifetime coupling in sample submission.
  - Estimated saving: avoids hot singleton path and blocks Crest leakage; 10-50 microseconds under high request churn.
- [x] Task 07 ASYNCHRONOUS_READBACK_QUEUE_ORCHESTRATION
  - DOD practice: request submission returns a chained `JobHandle` and never calls `Complete`; outputs are marked delayed through status flags.
  - Rejected alternative: same-frame Crest GPU/CPU readback was rejected because it hard-stalls the 16.67 ms frame.
  - Estimated saving: removes main-thread readback stalls; 500-2000 microseconds avoided during water-heavy frames.
- [x] Task 08 THE_DEAR_LIE_DEFERRED_BUOYANCY
  - DOD practice: both bridge and mock results carry `DelayedOneToThreeFrames`; simplified samples use deterministic sine approximations instead of forcing Crest wave truth.
  - Rejected alternative: treating visual wave displacement as authoritative gameplay truth was rejected because rollback/netcode and GPU timing cannot own physics authority.
  - Estimated saving: prevents GPU synchronization; 500+ microseconds in worst-case buoyancy bursts.
- [x] Task 09 AUP_PRECISION_TRANSLATION_LAYER
  - DOD practice: jobs subtract `double3` ocean/root AUP from request AUP before casting to `float3`, keeping math local and ARM-friendly.
  - Rejected alternative: casting absolute 100km coordinates to `float3` was rejected because it creates map-edge wave jitter and probe drift.
  - Estimated saving: correctness gain; prevents catastrophic physics/visual retries rather than raw frame time.
- [x] Task 10 CONTINUOUS_SCALABILITY_SAMPLE_BUDGET
  - DOD practice: quality budget uses `math.smoothstep`/`math.lerp` from `GlobalQualityWeight`, degrading ambient samples to cheap single-sine approximations without binary hardware switches.
  - Rejected alternative: `IsLowEndHardware` branches were rejected because they create visible LOD cliffs and violate the continuous quality law.
  - Estimated saving: 100-900 microseconds depending on sample count and quality pressure.

## Loop 3: Tasks 11-15

- [x] Task 11 WATER_LEVEL_GLOBAL_SCALAR_BROADCAST
  - DOD practice: added `OceanAdapterVaultRoute.TryPublishWaterLevel()` writing `OceanGlobalWaterLevelDTO` to SHINOBU_260 local `(BufferID)72964`.
  - Rejected alternative: forcing every consumer through per-point wave queries was rejected because most systems need only O(1) sea-level checks.
  - Estimated saving: 10-100 microseconds per consumer cluster.
- [ ] Task 12 CREST_INITIALIZATION_PHASE_FENCE [BLOCKED BY DEPENDENCY]
  - DOD practice: bridge adapter exposes cold `Bind()` and avoids hot singleton polling, but full disabling of Crest `OceanRenderer.OnEnable/Start` requires direct third-party Crest source lifecycle surgery.
  - Rejected alternative: editing Crest internals during quarantine was rejected because the donor package must remain restorable and leaf-isolated before later ocean agents modify internals.
  - Estimated saving: not claimed; integrator must schedule the Crest source lifecycle patch if runtime startup hitches remain.
- [x] Task 13 ROLLBACK_NETCODE_EXCLUSION_FENCE
  - DOD practice: all bridge request/result buffers are presentation-path SHINOBU_260 local vault lanes `(BufferID)72960..72961`, not state ring authoritative DTOs.
  - Rejected alternative: hashing wave readback buffers was rejected because frame-rate-dependent visual water would desync rollback.
  - Estimated saving: prevents rollback mismatch; runtime saving is avoided resim churn, not steady-state microseconds.
- [x] Task 14 ZERO_INIT_OVERHEAD_BYPASS
  - DOD practice: vault handles for requests/results/telemetry/profiles/csv scratch use `NativeArrayOptions.UninitializedMemory`.
  - Rejected alternative: clearing overwritten sample queues was rejected as redundant memory bandwidth.
  - Estimated saving: 50-300 microseconds per large buffer refresh.
- [x] Task 15 TELEMETRY_ADAPTER_RECORDER
  - DOD practice: added 64-byte `OceanAdapterTelemetryEntry` and `OceanAdapterVaultRoute.TryRecordTelemetry()` into 300-entry SHINOBU_260 local `(BufferID)72962`.
  - Rejected alternative: editor-only logs were rejected because endurance failures need last-frame native evidence.
  - Estimated saving: no steady-state speed gain; failure diagnosis avoids hours of replay blindness.

## Loop 4: Tasks 16-20

- [x] Task 16 QUARANTINE_CONTROL_XRAY_WINDOW
  - DOD practice: added UI Toolkit `CrestQuarantineXRayWindow` in the Crest bridge editor assembly; it reads the dependency report and diagnostic vault telemetry when available.
  - Rejected alternative: keeping Crest diagnostics in the shared editor assembly was rejected because it reopens the compile wall.
  - Estimated saving: editor-only visibility, runtime cost 0 microseconds.
- [x] Task 17 CSV_OCEAN_PROFILES_INGESTOR
  - DOD practice: added `OceanPerformanceProfileCsv.Parse(ReadOnlySpan<byte>, NativeArray<OceanPerformanceProfileDTO>)` with FNV-1a hashes and caller-owned output.
  - Rejected alternative: `string.Split`/managed CSV objects were rejected because cold boot still must not leak managed configuration habits into hot systems.
  - Estimated saving: avoids managed parsing churn during boot profile ingestion.
- [x] Task 18 LIVE_AUP_SAMPLING_GIZMO
  - DOD practice: added `CrestAupSamplingGizmo` in bridge editor assembly; diagnostic-only reads `GlobalRegistry.DataVault`, resolves SHINOBU_260 route handles, localizes AUP, and draws request/result states from the ocean vault buffers.
  - Rejected alternative: drawing from scene object guesses only was rejected because it does not prove AUP request placement.
  - Estimated saving: editor-only; runtime cost 0 microseconds.
- [x] Task 19 ARCHITECTURAL_METRIC_VALIDATOR
  - DOD practice: added and ran `Tools/Crest_Dependency_Scanner.py`; current JSON report has `breach_count=0`, `allowed_hit_count=23`, and `reflection_string_hit_count=0` after removing runtime/presentation Crest reflection strings outside the bridge.
  - Rejected alternative: manual grep report was rejected because it is non-repeatable and cannot gate later regressions.
  - Estimated saving: prevents future compile-wall regressions; runtime cost 0 microseconds.
- [x] Task 20 SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION
  - DOD practice: wrote `Docs/Reports/SHINOBU_260_SELF_AUDIT.xml`, architecture route card, binary ledger addendum, and appended final proof to `Docs/AgentLogs/LOG_SHINOBU_260.md`.
  - Rejected alternative: chat-only reporting was rejected because CTO/integrators read disk artifacts.
  - Estimated saving: prevents repeated rediscovery of quarantine state; runtime cost 0 microseconds.

## Loop 5: Strict Iteration

- [x] Re-read correct Crest XML every 3-5 tasks.
- [x] Re-read changed files for hidden Crest direct references, managed collections, DTO properties, and asmdef leaks.
- [x] Run static validators and compile gate only if CPU/build constraints allow.
- [x] Append `Docs/AgentLogs/LOG_SHINOBU_260.md` with final evidence and `<SELF_AUDIT>`.

## Loop 6: Sub-Agent Polish Findings

- [x] Managed fallback class removed.
  - DOD practice: `EmergencyMockOceanKinematicsAdapter` is now a `readonly struct`; the factory no longer creates a heap object for a value-only fallback.
  - Rejected alternative: keeping a sealed class because creation is cold was rejected because the task explicitly requires struct-first unmanaged boundaries.
  - Estimated saving: 0 steady-state microseconds; prevents cold allocation habit from entering hot ocean profiling harnesses.
- [x] Atmosphere-owned Vault lane collision removed.
  - DOD practice: `OceanAdapterVaultRoute` now uses SHINOBU_260 local numeric `BufferID` casts `72960..72965`; old `ShinobuOcean*` lanes remain owned by `ShinobuOceanSurfaceAtmosphereRuntime`.
  - Rejected alternative: sharing existing `ShinobuOceanTelemetryRing`, `ShinobuOceanLodState`, readback, profile, and CSV lanes was rejected because `GlobalDataVault` type guards can throw on mismatched element types depending on boot order.
  - Estimated saving: prevents fatal boot/runtime vault type mismatch; steady-state frame saving not claimed.
- [x] Hot dependency repair removed from the new Crest runtime adapter.
  - DOD practice: `ScheduleWaveHeightRequests` uses cold cached authoritative root AUP or caller active-origin fallback; it does not call `TryGetComponent`, mutate binding state, or reconstruct AUP from `Transform.position`.
  - Rejected alternative: auto-repairing missing Crest references during sample submission was rejected because it hides scene lookup in a read-looking hot path.
  - Estimated saving: 5-50 microseconds avoided during binding failures; correctness gain is stronger than timing.
- [x] Editor AUP gizmo localized before float cast.
  - DOD practice: diagnostic drawing subtracts `HectonFloatingOrigin.CurrentTotalOffsetDouble` in double precision before constructing `Vector3`.
  - Rejected alternative: direct absolute AUP cast was rejected because the debug view lies at 100km scale.
  - Estimated saving: 0 runtime microseconds; editor-only correctness proof.
- [x] Forbidden editor assembly edge removed.
  - DOD practice: `EasySave3` reference was removed from `Hecton8.Crest.Bridge.Editor.asmdef`.
  - Rejected alternative: keeping a dormant forbidden vendor reference was rejected because it widens the Crest bridge compile wall.
  - Estimated saving: editor compile/import hygiene only.
- [x] Static polish audit added.
  - DOD practice: `Tools/Crest_Quarantine_Polish_Audit.py` writes `Docs/Reports/CREST_QUARANTINE_POLISH_AUDIT.json` and currently reports `failed_count=0`.
  - Rejected alternative: chat-only subagent findings were rejected because future context compression needs a disk artifact.
  - Estimated saving: prevents repeated manual rediscovery; runtime cost 0 microseconds.
- [x] BufferID sovereignty audit rerun.
  - DOD practice: `Tools/BufferIDSovereigntyAudit.py` wrote `Docs/Reports/SHINOBU_260_BufferIDSovereigntyAudit.*`; global duplicate numeric values are `3` in unrelated `H8Memory.cs` ranges `70534..70536`, while `72960..72965` appear as local casts only in `OceanAdapterVaultRoute.cs`.
  - Rejected alternative: trusting the local exact scan alone was rejected because the prior defect was a cross-domain lane collision.
  - Estimated saving: prevents boot/runtime alias faults; no frame microseconds claimed.

## Loop 7: Legacy Bridge Read-Accessor Polish

- [x] Legacy `Crest4KinematicsAdapter` hidden binding repair removed.
  - DOD practice: `ResolveOceanRenderer()` no longer calls `TryResolveLocalOceanRendererBinding()` or `TryGetComponent`; binding discovery remains in `Awake`, while `IsAvailable`/`SeaLevel` read only the cached field through `TryReadBoundOceanRenderer()`.
  - Rejected alternative: rewriting the whole legacy `Hecton8.Physics` bridge was rejected because the forward strict route is `Hecton8.Environment.Fluids.Contracts` and legacy consumers belong to parallel physics/ocean agents.
  - Estimated saving: 5-50 microseconds only during missing-binding failure paths; main gain is preventing read-looking calls from searching scene state.
- [x] Polish audit widened.
  - DOD practice: `Tools/Crest_Quarantine_Polish_Audit.py` now includes `legacy_crest4_adapter_no_hot_component_repair`; latest run reports `failed_count=0`.
  - Rejected alternative: relying on memory of the sub-agent finding was rejected because future context compaction needs disk evidence.
  - Estimated saving: runtime cost 0 microseconds; prevents regression of hidden component repair.

## Loop 8: Base Bridge Singleton And Read Purity Polish

- [x] Base `CrestBridge` stopped polling `Crest.OceanRenderer.Instance`.
  - DOD practice: added `ReadBoundOceanRenderer()` as a concrete-adapter hook and routed `OceanMaterial`, `IsOceanCameraOwnedBy`, and `AssignOceanCamera` through the cold-bound renderer.
  - Rejected alternative: leaving singleton access in the base class was rejected because any visual bridge consumer could accidentally reintroduce hidden third-party global lookup outside the strict adapter route.
  - Estimated saving: steady-state 0-10 microseconds depending on caller cadence; the main gain is removing a copied singleton-poll pattern from the bridge base.
- [x] Legacy weather, sea-level, flow, and collision reads use cached fields without logging or registry fallback.
  - DOD practice: `TryGetSurfaceWeatherState`, `GetSurfaceFlow`, and collision provider reads now use `TryReadBoundOceanRenderer()`. `SeaLevel` uses `ResolveSeaLevel(..., allowRegistryFallback: false)` so a read accessor cannot poll `GlobalRegistry`.
  - Rejected alternative: keeping one-shot diagnostic logging in read paths was rejected because read accessors must not mutate diagnostic flags.
  - Estimated saving: 5-50 microseconds only on missing-binding paths; stronger value is read-purity proof.
- [x] Static audit expanded and rerun.
  - DOD practice: `Tools/Crest_Quarantine_Polish_Audit.py` now gates `base_bridge_no_ocean_singleton_polling` and `legacy_crest4_read_accessors_do_not_log_or_poll_registry`; latest run reports `failed_count=0`.
  - Rejected alternative: trusting manual inspection was rejected because future bridge regressions need machine-readable proof.
  - Estimated saving: runtime cost 0 microseconds; prevents singleton/read-purity regression.
- [x] Verification gate rerun without rebuild.
  - DOD practice: `python Tools\Crest_Quarantine_Polish_Audit.py`, `python Tools\Crest_Dependency_Scanner.py`, Python bytecode compilation, and `git diff --check` passed. Dependency scanner reports `breach_count=0`, `allowed_hit_count=28`.
  - Rejected alternative: launching `dotnet build` was rejected because CPU sampled at 100 percent under the explicit AGENTS gate.
  - Estimated saving: avoided a forbidden compile-wall run under load; build proof remains gated, not silently claimed.

## Loop 9: Sub-Agent Findings Integration

- [x] Base underwater read accessors changed to cache-only.
  - DOD practice: `HasUnderwaterInstance`, `HasUnderwaterRenderer`, and `TryGetUnderwaterRenderer` no longer call `Crest.UnderwaterRenderer.Instance` or `GetComponent`; only the imperative `EnsureUnderwaterRenderer` command path may resolve/create the component and update the cache.
  - Rejected alternative: keeping a singleton read for visual convenience was rejected because read accessors must not poll global third-party state.
  - Estimated saving: 0-10 microseconds in normal visual setup paths; main gain is read-purity proof.
- [x] Legacy tuning path made cached-read only.
  - DOD practice: `TryBuildBurstTuning` now uses `TryReadBoundOceanRenderer()` and `ResolveSeaLevel(oceanRenderer)` without `GlobalRegistry.Fluid` fallback or logging resolver. `ResolveOceanRenderer()` no longer logs.
  - Rejected alternative: leaving one-shot diagnostic logging inside the resolver was rejected because scheduling paths call tuning repeatedly and read-like failure paths must not mutate log flags.
  - Estimated saving: 5-50 microseconds on missing-binding failure paths; prevents registry/log side effects.
- [x] Depth-cache singleton fallback removed.
  - DOD practice: `HectonCrestOceanDepthCacheBootstrap` no longer falls back to `Crest.OceanRenderer.Instance`, and `ResolveFallbackWaterLevel` no longer mutates `mapMagicBridge` through a resolver.
  - Rejected alternative: wider celestial/global dependency rewrite was rejected because it crosses into World/Celestial ownership beyond the Crest quarantine bridge patch.
  - Estimated saving: small runtime saving; main gain is removing global Crest singleton recovery from slow tick.
- [x] Reflection coupling scanner bucket added.
  - DOD practice: `Tools/Crest_Dependency_Scanner.py` still fails only compile-wall breaches, but now reports non-failing `reflection_string_hits` for Crest type-name strings outside `Assets/_Project/Scripts/Plugins/Crest`.
  - Rejected alternative: treating strings as compile-wall breaches was rejected because they do not create asmdef dependencies; hiding them was rejected because they are still migration debt.
  - Estimated saving: runtime cost 0 microseconds; prevents scanner overclaim.
- [x] Static validation rerun.
  - DOD practice: `python Tools\Crest_Quarantine_Polish_Audit.py` reports `failed_count=0`; `python Tools\Crest_Dependency_Scanner.py` reports `breach_count=0`, `allowed_hit_count=23`, `reflection_string_hit_count=4`; Python bytecode compilation passed.
  - Rejected alternative: Unity/dotnet rebuild was rejected under the CPU gate.
  - Estimated saving: no runtime claim; build proof remains gated.

## Loop 10: Runtime Reflection Coupling Purge

- [x] `HectonUnderwaterVisuals` no longer carries Crest type-name reflection fallbacks.
  - DOD practice: removed `"Crest.OceanRenderer"` / `"Crest.UnderwaterRenderer"` string fallback routes and the editor-only material/underwater renderer reflection helpers; visual access now depends on `IOceanVisualBridge`.
  - Rejected alternative: preserving editor convenience reflection was rejected because it bypasses the asmdef wall pattern and lets a non-bridge runtime/presentation file know Crest type names.
  - Estimated saving: runtime 0 microseconds in normal play; editor fallback scene scans avoided when bridge material is absent.
- [x] Reflection scanner tightened.
  - DOD practice: `Tools/Crest_Dependency_Scanner.py` now reports runtime/presentation Crest reflection strings outside bridge and ignores editor compliance denylist strings that do not create a runtime coupling route.
  - Rejected alternative: counting compliance denylist strings as coupling was rejected because it creates false positives and hides real reflection debt.
  - Estimated saving: runtime cost 0 microseconds; improves proof quality.
- [x] Static proof rerun.
  - DOD practice: `python Tools\Crest_Quarantine_Polish_Audit.py` reports `failed_count=0`; `python Tools\Crest_Dependency_Scanner.py` reports `breach_count=0`, `allowed_hit_count=23`, `reflection_string_hit_count=0`; py_compile passed; `git diff --check` passed with only CRLF warnings.
  - Rejected alternative: dotnet/Unity rebuild was rejected because CPU sampled at 90.9 percent under the AGENTS gate.
  - Estimated saving: avoids forbidden compile-wall run under load; compile proof remains gated.

## Loop 11: Visual Contract And Prefab Quarantine Polish

- [x] Crest forensic debug MonoBehaviours moved under the Crest bridge folder.
  - DOD practice: `CrestFoamDebugger.cs(.meta)` and `CrestDepthCacheDebugger.cs(.meta)` now live under `Assets/_Project/Scripts/Plugins/Crest/`; `Assets/_Project/Scripts/World/` no longer owns Crest forensic scripts.
  - Rejected alternative: leaving Crest-specific debug components in World was rejected because it makes a gameplay domain look like a Crest API owner.
  - Estimated saving: runtime 0 microseconds; compile-wall ownership proof improves.
- [x] Core and visual interface vocabulary neutralized.
  - DOD practice: core diagnostics now say ocean/third-party adapter, `IOceanVisualBridge` exposes `UnderwaterPass` verbs and `CameraColorTextureId`, and `HectonUnderwaterVisuals` uses pass vocabulary through `OceanVisualBridgeRegistry`.
  - Rejected alternative: keeping `UnderwaterRenderer` and hard-coded `_Crest_CameraColorTexture` names in non-bridge code was rejected because it leaks donor vocabulary across the wall even without an asmdef edge.
  - Estimated saving: runtime 0 microseconds in normal play; removes a copied vendor-global lookup pattern.
- [x] Serialized field migration preserved while removing non-bridge Crest field naming.
  - DOD practice: `crestSkyBaseFogLink` became `oceanSkyBaseFogLink` with `[FormerlySerializedAs("crestSkyBaseFogLink")]` so scene/prefab values survive Unity deserialization.
  - Rejected alternative: raw rename without `FormerlySerializedAs` was rejected because it silently resets authored visual tuning.
  - Estimated saving: runtime 0 microseconds; avoids designer re-tuning loss.
- [x] Crest5 prefab component reference removed.
  - DOD practice: raw YAML removed component fileID `4153056372701123456` and script GUID `51fcb9de0aa92b842be404fec8bf21d4` from `Assets/_Project/Prefabs/Ocean_Crest.prefab`. Exact scans over prefabs/scenes/assets found no remaining `Crest5KinematicsAdapter`/GUID/fileID hits. `m_RootGameObject` is absent in this prefab format, so alignment proof uses the root `GameObject` component list and exact ID/GUID absence.
  - Rejected alternative: leaving the missing Crest5 component on the active Crest4 prefab was rejected because Unity would preserve a broken/quarantined script edge.
  - Estimated saving: editor import/console noise only; runtime cost 0 microseconds.
- [x] Shared first-party ocean kinematics base renamed away from Crest.
  - DOD practice: `HectonCrestOceanKinematics.cs(.meta)` became `HectonOceanKinematicsBridgeBase.cs(.meta)` with the GUID preserved; `CrestBridge` inherits the neutral base and the editor scanner skip path was updated.
  - Rejected alternative: renaming serialized gameplay components such as `SargassumCrestDampingController` or `HectonPlayerMovement.useCrestOceanHeight` was rejected in this loop because those names are prefab/scene ABI and player/world ownership, not direct Crest assembly references.
  - Estimated saving: runtime 0 microseconds; reduces first-party type-name contamination without unsafe prefab remap.
- [x] Static proof rerun.
  - DOD practice: `python Tools\Crest_Quarantine_Polish_Audit.py` reports `failed_count=0` with new checks for prefab removal, vendor-neutral visual contract, dry-volume bridge texture ID, and neutral kinematics base. `python Tools\Crest_Dependency_Scanner.py` reports `breach_count=0`, `allowed_hit_count=23`, `reflection_string_hit_count=0`; py_compile passed; `git diff --check` passed with only CRLF warnings.
  - Rejected alternative: launching dotnet/Unity rebuild was rejected because the gate found active `dotnet`/`csc` processes and CPU load at 88 percent.
  - Estimated saving: no runtime claim; build proof remains gated.

## Loop 12: Vocabulary Debt Scanner And Low-Risk Donor Text Polish

- [x] Low-risk non-bridge donor wording neutralized.
  - DOD practice: comments/tooltips in Visor, Atmosphere, Environment, Fluid, and Sargassum authoring code now say ocean/ocean-donor/ocean shader instead of naming Crest where no serialized ABI or direct API route required the name.
  - Rejected alternative: renaming `HectonPlayerMovement` and `SargassumCrestDampingController` symbols was rejected because those are Player/World serialized ABI and private gameplay routes outside this quarantine wall.
  - Estimated saving: runtime 0 microseconds; compile-wall proof improves by reducing copyable donor terminology in non-bridge code.
- [x] Dependency scanner now tracks vocabulary debt as non-failing evidence.
  - DOD practice: `Tools/Crest_Dependency_Scanner.py` separates `vocabulary_debt_hits` from `breaches` and `reflection_string_hits`; current report has `breach_count=0`, `reflection_string_hit_count=0`, and `vocabulary_debt_hit_count=111`.
  - Rejected alternative: failing the compile-wall scanner on serialized text debt was rejected because it would block on Player/World ownership; hiding the debt was rejected because future agents need a list.
  - Estimated saving: runtime 0 microseconds; prevents false green reports and false red compile-wall failures.
- [x] Static proof rerun.
  - DOD practice: `python Tools\Crest_Dependency_Scanner.py`, `python Tools\Crest_Quarantine_Polish_Audit.py`, `python -m py_compile ...`, active Crest5 GUID scan, and `git diff --check` passed. `git diff --check` emitted only CRLF conversion warnings.
  - Rejected alternative: launching dotnet/Unity rebuild was rejected because the explicit gate still forbids builds while active compiler processes or CPU load are present.
  - Estimated saving: no runtime claim; build proof remains gated.

## Loop 13: Active Asset, Shader, Prefab, And Scene Hard Breach Containment

- [x] Crest5 WaveHarmonic serialized settings assets moved outside Unity visibility.
  - DOD practice: `Crest5_WaveSpectrum.asset(.meta)` and `Crest5_FoamSettings.asset(.meta)` moved from `Assets/_Project/Data/CrestMigration/` to `Docs/Archive/Crest_Version_Quarantine/Assets/_Project/Data/CrestMigration/` with metas preserved.
  - Rejected alternative: leaving inactive Crest5 ScriptableObject references under `Assets/_Project/Data` was rejected because Unity can still import serialized `WaveHarmonic.Crest` type identities.
  - Estimated saving: editor/import hygiene only; runtime frame cost 0 microseconds.
- [x] Crest-specific sargassum input shaders moved under the Crest bridge folder.
  - DOD practice: `Crest_SargassumWaveDamping.shader`, `Crest_SargassumFoamDamping.shader`, and `Crest_SargassumOilFilm.shader` plus metas now live under `Assets/_Project/Scripts/Plugins/Crest/Shaders/`; material links are preserved by keeping shader GUID metas.
  - Rejected alternative: leaving Crest HLSL includes in shared `Assets/_Project/Art/Shaders` was rejected because a shared art folder then becomes a hidden Crest donor owner.
  - Estimated saving: runtime 0 microseconds; compile/import ownership proof improves.
- [x] Player prefab direct Crest underwater component removed.
  - DOD practice: raw YAML removed the `Crest::Crest.UnderwaterRenderer` component block, fileID `9079297290110143596`, and script GUID `1b0c0a69611596146aceb2f60532940c` from `Assets/_Project/Prefabs/Player.prefab`; exact scan confirms absence.
  - Rejected alternative: leaving the player prefab to own a direct vendor component was rejected because underwater pass ownership must route through `IOceanVisualBridge` and the bridge command path.
  - Estimated saving: small editor/import and startup hygiene; no frame microseconds claimed.
- [x] Binary Crest5 sandbox scene moved outside Unity visibility.
  - DOD practice: `03_HECTON_WORLD_CREST5.unity(.meta)` moved from `Assets/_Project/Scenes/` to `Docs/Archive/Crest_Version_Quarantine/Assets/_Project/Scenes/`; exact scan confirms no active build/settings/scene reference.
  - Rejected alternative: keeping a binary WaveHarmonic Crest5 scene under active `Assets` was rejected because it cannot be safely patched as YAML and preserves inactive donor import debt.
  - Estimated saving: editor import hygiene only; runtime frame cost 0 microseconds.
- [x] Dependency scanner and polish audit widened to active asset/shader/prefab/scene surfaces.
  - DOD practice: `Tools/Crest_Dependency_Scanner.py` now scans active `.asset`, `.prefab`, `.unity`, `.mat`, `.shader`, `.hlsl`, and `.compute` surfaces for Crest5/WaveHarmonic and direct `Crest.UnderwaterRenderer` breaches; `Tools/Crest_Quarantine_Polish_Audit.py` gates the new containment state.
  - Rejected alternative: C# and asmdef-only proof was rejected because sub-agent audit found hard serialized/shader breaches outside code files.
  - Estimated saving: runtime 0 microseconds; prevents hidden import/compile-wall regressions.
- [x] Static proof rerun.
  - DOD practice: `python Tools\Crest_Dependency_Scanner.py` reports `breach_count=0`, `allowed_hit_count=40`, `reflection_string_hit_count=0`, `vocabulary_debt_hit_count=111`; `python Tools\Crest_Quarantine_Polish_Audit.py` reports `failed_count=0`; `python -m py_compile ...` passed; exact scans found no active Crest5/WaveHarmonic/UnderwaterRenderer serialized hits and only bridge-owned Crest shader references.
  - Rejected alternative: launching dotnet/Unity rebuild was rejected under the explicit no-rebuild-until-needed instruction and build gate discipline.
  - Estimated saving: no runtime claim; build proof remains gated.

## Loop 14: Root Asset And Recovery Scene Quarantine Widening

- [x] Easy Save global assembly defaults purged of Crest assemblies.
  - DOD practice: removed bare `Crest` and `WaveHarmonic.Crest*` entries from `Assets/Plugins/Easy Save 3/Resources/ES3/ES3Defaults.asset` so a global serializer config cannot scan donor assemblies outside the bridge.
  - Rejected alternative: leaving serializer reflection defaults untouched was rejected because it preserves a non-bridge managed reflection route into third-party ocean assemblies.
  - Estimated saving: runtime 0 microseconds; editor/reflection import hygiene only.
- [x] Root InitTestScene TestRunner assembly lists purged of WaveHarmonic assemblies.
  - DOD practice: removed `WaveHarmonic.Crest`, `WaveHarmonic.Crest.Samples`, and `WaveHarmonic.Crest.Scripting` from five root `Assets/InitTestScene*.unity` files while preserving the scenes and metas.
  - Rejected alternative: moving/deleting TestRunner scenes was rejected because exact string removal is sufficient and keeps the test harness structure intact.
  - Estimated saving: editor/test import hygiene only; runtime frame cost 0 microseconds.
- [x] Active Unity recovery payload moved outside Unity visibility.
  - DOD practice: moved `Assets/_Recovery/` and `Assets/_Recovery.meta` to `Docs/Archive/Crest_Version_Quarantine/Assets/_Recovery/` after static scan found binary recovery scenes with `Crest::Crest.UnderwaterRenderer` and `Crest5KinematicsAdapter` strings.
  - Rejected alternative: byte-editing 1.2 GB of binary recovery scenes was rejected because the folder is Unity recovery payload, not authoritative source; archive movement preserves evidence without import cost.
  - Estimated saving: avoids scanning/importing 102 recovery payload files totaling about 1.2 GB; runtime frame cost 0 microseconds.
- [x] Dependency scanner widened to root/Plugins/ProjectSettings/Packages serialized surfaces.
  - DOD practice: `Tools/Crest_Dependency_Scanner.py` now scans active serialized text in `Assets`, `ProjectSettings`, and `Packages`, catches bare `- Crest` assembly-list entries, and hard-fails active `Packages/com.waveharmonic.crest` visibility instead of skipping it.
  - Rejected alternative: retaining `_Project`-only serialized scans was rejected because Easy Save, root TestRunner scenes, and recovery payloads are Unity-visible outside `_Project`.
  - Estimated saving: runtime 0 microseconds; prevents hidden import and serializer reflection regressions.
- [x] Static proof rerun.
  - DOD practice: `python Tools\Crest_Dependency_Scanner.py` reports `breach_count=0`, `allowed_hit_count=40`, `reflection_string_hit_count=0`, `vocabulary_debt_hit_count=111`; broad serialized exact scan found no active Crest5/WaveHarmonic/direct UnderwaterRenderer/bare Crest assembly hits; polish audit reports `failed_count=0`; py_compile passed.
  - Rejected alternative: launching dotnet/Unity rebuild was rejected under explicit instruction and the compile-wall gate.
  - Estimated saving: no runtime claim; build proof remains gated.

## Loop 15: Scanner Throughput Repair

- [x] Active serialized breach scan moved to `rg --json`.
  - DOD practice: `Tools/Crest_Dependency_Scanner.py` now uses ripgrep for active serialized/shader breach search across `Assets`, `ProjectSettings`, and `Packages`, then parses JSON matches into the same report schema; Python file-walk fallback remains for environments without `rg`.
  - Rejected alternative: reading every Unity asset through Python was rejected because it made a proof tool take about 262 seconds after widening the scan surface.
  - Estimated saving: scanner wall time dropped from about 262 seconds to about 35.5 seconds; runtime frame cost 0 microseconds.
- [x] Large-file read fallback bounded.
  - DOD practice: Python fallback reads only the first `MAX_ACTIVE_ASSET_SCAN_BYTES` bytes instead of loading whole files before slicing.
  - Rejected alternative: full `read_bytes()` on large Unity/binary assets was rejected because it wastes IO and memory for a fixed-prefix static scanner.
  - Estimated saving: fallback avoids multi-gigabyte transient reads on recovery-scale scenes.
- [x] Static proof rerun.
  - DOD practice: `python -m py_compile Tools\Crest_Dependency_Scanner.py` passed; `Measure-Command { python Tools\Crest_Dependency_Scanner.py | Out-Null }` reported `SCANNER_SECONDS=35.51`; latest scanner report remains `breach_count=0`.
  - Rejected alternative: Unity/dotnet rebuild was rejected because this loop only changes Python proof tooling.
  - Estimated saving: about 226 seconds per full scanner run on this workspace.

## Loop 16: Assembly Sidecar And GUID Reference Wall

- [x] `.asmref` Crest route scanning added.
  - DOD practice: `Tools/Crest_Dependency_Scanner.py` now reads both `.asmdef` and `.asmref` JSON, classifies `asmref_reference` hits, and allows them only inside `Assets/_Project/Scripts/Plugins/Crest`.
  - Rejected alternative: named `.asmdef` scanning alone was rejected because Unity can route assembly references through `.asmref` sidecars.
  - Estimated saving: runtime 0 microseconds; prevents a future hidden compile-wall edge from bypassing the quarantine scanner.
- [x] Unity GUID-form Crest asmdef references added to the wall.
  - DOD practice: scanner treats `GUID:5b35af79ebbe89647a157055d52c59d3` and `GUID:59cd48da98d9e4a80917b613abe9416e` as Crest assembly references, equal to `"Crest"` / `"Crest.Helpers.Editor"`.
  - Rejected alternative: trusting assembly names only was rejected because Unity asmdefs may store references as GUID strings.
  - Estimated saving: runtime 0 microseconds; compile-wall proof is stronger.
- [x] Static proof rerun.
  - DOD practice: `python -m py_compile Tools\Crest_Dependency_Scanner.py Tools\Crest_Quarantine_Polish_Audit.py`, exact `rg` scans for non-bridge Crest asmdef GUID refs and Crest `.asmref` refs, full dependency scanner, and polish audit passed.
  - Rejected alternative: Unity/dotnet rebuild was rejected because this loop changes proof tooling and no C# compilation was needed.
  - Estimated saving: avoids an unnecessary rebuild; full scanner remains `breach_count=0`, `allowed_hit_count=40`.

## Loop 17: Archived Asset GUID Backreference Wall

- [x] Archived Crest5/recovery GUIDs extracted and scanned.
  - DOD practice: read archived metas for `Crest5_WaveSpectrum.asset`, `Crest5_FoamSettings.asset`, `03_HECTON_WORLD_CREST5.unity`, and `_Recovery.meta`, then ran an active Assets/ProjectSettings/Packages scan for those GUIDs.
  - Rejected alternative: relying only on asset names/type strings was rejected because Unity YAML can preserve dead links purely by GUID.
  - Estimated saving: runtime 0 microseconds; prevents silent missing-reference import/editor churn.
- [x] Dependency scanner now hard-fails active backreferences to those archived GUIDs.
  - DOD practice: `Tools/Crest_Dependency_Scanner.py` owns `QUARANTINED_ASSET_GUIDS` and includes them in the active serialized breach patterns used by both `rg` and Python fallback.
  - Rejected alternative: keeping the GUID check as a one-off command was rejected because future regressions need the normal scanner gate.
  - Estimated saving: runtime 0 microseconds; future proof pass catches dead archive links automatically.
- [x] Static proof rerun.
  - DOD practice: py_compile passed; exact archived-GUID scan reports no active refs; dependency scanner reports `breach_count=0`; polish audit reports `failed_count=0`.
  - Rejected alternative: Unity/dotnet rebuild was rejected because this loop changes proof tooling/docs only.
  - Estimated saving: no frame claim; avoids needless build wall.

## Loop 18: AutoReferenced Donor And Bridge Fence

- [x] Active Crest donor and bridge asmdefs verified as opt-in only.
  - DOD practice: exact checks confirmed `autoReferenced=false` in Crest donor runtime/editor asmdefs and Crest bridge runtime/editor asmdefs.
  - Rejected alternative: relying on direct-reference scans alone was rejected because an auto-referenced donor assembly can widen compile scope without a first-party reference line.
  - Estimated saving: runtime 0 microseconds; protects editor compile fanout.
- [x] Dependency scanner now hard-fails autoReferenced regressions.
  - DOD practice: scanner fails `crest_donor_asmdef_auto_referenced` for active Crest donor asmdefs and `bridge_crest_asmdef_auto_referenced` for allowed bridge asmdefs that reference Crest while auto-referenced.
  - Rejected alternative: audit-only proof was rejected because compile-wall regressions must fail the normal dependency scanner.
  - Estimated saving: runtime 0 microseconds; prevents future seconds-scale editor recompile fanout.
- [x] Static proof rerun.
  - DOD practice: py_compile passed; dependency scanner reports `breach_count=0`; polish audit reports `failed_count=0`.
  - Rejected alternative: Unity/dotnet rebuild was rejected because this loop changes proof tooling/docs only.
  - Estimated saving: no frame claim; avoids needless build wall.

## Loop 19: Global Crest Scripting Define Evidence Wall

- [x] Attribute-aware Crest XML extraction revalidated.
  - DOD practice: extracted `<AGENT_PROMPT id="SHINOBU_260" role="CREST_VERSION_QUARANTINE_DIRECTOR" ...>` with an attribute-tolerant CLI regex and recounted 20 tasks.
  - Rejected alternative: trusting the earlier exact-tag false negative was rejected because the current batch still contains the correct Crest block at line 4605.
  - Estimated saving: prevents wrong-domain stop/restart churn; runtime cost 0 microseconds.
- [x] Global Crest scripting defines classified as evidence, not hidden first-party routes.
  - DOD practice: `Tools/Crest_Dependency_Scanner.py` now reports `global_scripting_define_hits` for PlayerSettings `CREST_OCEAN`/`CREST_URP` and hard-fails first-party `#if CREST_OCEAN` / `#if CREST_URP` branches outside the Crest bridge.
  - Rejected alternative: deleting `CREST_URP` from `ProjectSettings` was rejected because the selected active Crest 4 donor uses that symbol internally; leaving the symbols invisible in reports was rejected because global defines are contamination evidence.
  - Estimated saving: runtime 0 microseconds; prevents future non-bridge compile branches from silently depending on donor scripting symbols.
- [x] Static proof rerun.
  - DOD practice: py_compile passed; dependency scanner reports `breach_count=0`, `global_scripting_define_hit_count=1`, `reflection_string_hit_count=0`, `vocabulary_debt_hit_count=111`; polish audit reports `failed_count=0`.
  - Rejected alternative: Unity/dotnet rebuild was rejected because the gate found active `VBCSCompiler` even though sampled CPU was 45.3 percent.
  - Estimated saving: no frame claim; avoids violating the compile gate while preserving scanner proof.

## Loop 20: Project-Side Crest4 Binding Backup

- [x] Active Crest4 project bindings explicitly archived.
  - DOD practice: widened `Tools/Crest_Baseline_Archiver.py` to back up `Assets/_Project/Data/Ocean`, `Assets/_Project/crest`, `Ocean_Crest.prefab(.meta)`, and `02_HECTON_WORLD.unity(.meta)` because these Unity-visible project assets contain selected Crest4 donor bindings.
  - Rejected alternative: assuming `Assets/Crest` zip alone was enough was rejected because restore after an ocean shader/prefab graft also needs project-side Crest settings and scene bindings.
  - Estimated saving: runtime 0 microseconds; prevents minutes to hours of manual scene/prefab/settings reconstruction after donor restore.
- [x] New baseline archive run executed.
  - DOD practice: `python Tools\Crest_Baseline_Archiver.py --execute` produced project-side donor zips under `Docs/Archive/Crest_Baseline_Backup/`; latest report lists 10 ocean settings files, 6 legacy `_Project/crest` files, `Ocean_Crest.prefab`, `Ocean_Crest.prefab.meta`, `02_HECTON_WORLD.unity`, and its meta.
  - Rejected alternative: copying into `Assets/~Quarantine` was rejected because the archive must remain outside Unity visibility.
  - Estimated saving: editor/runtime 0 microseconds; recovery speed is the gain.
- [x] Static proof rerun.
  - DOD practice: py_compile passed; polish audit reports `crest4_project_bindings_have_baseline_archives` and `failed_count=0`; dependency scanner remains `breach_count=0`.
  - Rejected alternative: Unity/dotnet rebuild was rejected because this loop changes Python tooling, docs, and archive payloads only.
  - Estimated saving: no frame claim; avoids needless compile-wall load.

## Loop 21: Compliance, Donor Reference, And Generated Report Wall

- [x] Editor compliance denylist Crest strings surfaced in scanner output.
  - DOD practice: `Tools/Crest_Dependency_Scanner.py` now reports `compliance_denylist_hits` from `Assets/_Project/Scripts/Editor/HectonComplianceValidator.cs` as non-failing policy evidence.
  - Rejected alternative: leaving `WaveHarmonic.Crest*` denylist strings visible only to ad hoc `rg` was rejected because future auditors would see unexplained non-bridge Crest text.
  - Estimated saving: runtime 0 microseconds; prevents false breach triage and keeps policy-only strings separate from runtime coupling.
- [x] Polish audit widened to gate the new scanner bucket.
  - DOD practice: `Tools/Crest_Quarantine_Polish_Audit.py` now checks `dependency_scanner_tracks_compliance_denylist_strings`; latest audit reports `failed_count=0`.
  - Rejected alternative: documenting the denylist exception without a machine gate was rejected because scanner schema regressions would hide the evidence again.
  - Estimated saving: runtime 0 microseconds; reduces repeated manual proof work.
- [x] Static proof rerun.
  - DOD practice: py_compile passed; dependency scanner reports `breach_count=0`, `global_scripting_define_hit_count=1`, `compliance_denylist_hit_count=6`, and `vocabulary_debt_hit_count=111`; polish audit reports `failed_count=0`.
  - Rejected alternative: Unity/dotnet rebuild remains rejected until compiler-process and CPU gates are clear.
  - Estimated saving: no frame claim; avoids forbidden rebuild load while strengthening static proof.
- [x] Active Crest4 donor asmdef absent-package references removed.
  - DOD practice: removed `Unity.RenderPipelines.HighDefinition.Runtime` and `Unity.Postprocessing.Runtime` from `Assets/Crest/Crest/Scripts/Crest.asmdef` because neither backing package exists in `Packages/manifest.json`, `packages-lock.json`, or physical `Packages/`.
  - Rejected alternative: adding HDRP/PostProcessing packages was rejected because the selected donor route is URP and package installation would widen compile/import scope.
  - Estimated saving: editor compile/import correctness; no runtime frame claim.
- [x] Stale Unity-visible profiler marker CSV quarantined.
  - DOD practice: moved `Assets/profilermarkers.csv(.meta)` to `Docs/Archive/Crest_Version_Quarantine/Assets/` after static scan found stale Crest assembly/method profiler rows.
  - Rejected alternative: editing the 2.7 MB generated CSV in place was rejected because it is a stale generated report, not authoritative runtime source.
  - Estimated saving: runtime 0 microseconds; removes stale donor evidence from active Unity import visibility.
- [x] Scanner and polish audit now gate both regressions.
  - DOD practice: dependency scanner hard-fails reintroduced absent optional donor assembly references and Unity-visible generated profiler reports containing Crest rows; polish audit checks `crest_donor_no_absent_hdrp_postprocessing_references`, `stale_profiler_markers_outside_unity_visibility`, `dependency_scanner_blocks_absent_optional_donor_references`, and `dependency_scanner_blocks_stale_generated_report_crest_rows`.
  - Rejected alternative: relying on sub-agent notes only was rejected because future regressions require machine-readable gates.
  - Estimated saving: editor proof time; no runtime frame claim.
- [x] Loop 21 post-compaction verification gate rerun.
  - DOD practice: py_compile passed for the three Crest tools; dependency scanner reports `breach_count=0`, `global_scripting_define_hit_count=1`, `compliance_denylist_hit_count=6`, `vocabulary_debt_hit_count=111`; polish audit reports `failed_count=0`; exact file gates prove `Assets/profilermarkers.csv` absent, archived profiler CSV present, and no HDRP/PostProcessing donor references remain; domain hot-path rg scan found no Pack=1, hot auto-properties, foreach, hidden `.Complete()`, LINQ, `UnityEngine.Random`, or private native collection allocation hits in the checked Crest/Environment surfaces.
  - Rejected alternative: launching Unity/dotnet rebuild was rejected because the build gate sampled active `csc` and `dotnet` processes and CPU at 98.6 percent.
  - Estimated saving: avoids a forbidden compile-wall hit; runtime frame claim remains 0 microseconds for this proof loop.

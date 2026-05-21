# Rationale_SHINOBU_260

Status: PENDING VERIFICATION / POLISH PASS LOOP 19 WITH TASK 12 BLOCKED BY DEPENDENCY / NO BUILD DUE COMPILER PROCESS GATE

## Decision 001: Resolve Duplicate SHINOBU_260 Prompt By ID And Role

Problem: `Docs/Tasks/CURRENT_BATCH.md` contains a stale first `<AGENT_PROMPT id="SHINOBU_260">` for vocal synthesis and a later correct `<AGENT_PROMPT id="SHINOBU_260" role="CREST_VERSION_QUARANTINE_DIRECTOR">`.
Solution: Select the block matching both the user-stated ID and role. Archive the stale vocal status/rationale/log to `Docs/Archive/Batch_SHINOBU_260_VOCAL_STALE_20260521/` before replacing active memory files.
Rejected Alternatives: ID-only first-match extraction was rejected because it routes work into the wrong domain. Ignoring the duplicate was rejected because context compression would re-poison future iterations.
Scalability potential: Low/Middle/High/Ultra unaffected at runtime; this is compile-wall and agent-memory hygiene.
Hardware Impact: Prevents unnecessary C# recompiles and third-party asset churn on developer hardware. Estimated saving: seconds to minutes of avoided wrong-domain rebuild/import work.

## Decision 002: Treat Crest As Toxic Third-Party Leaf Until Proven Otherwise

Problem: Crest 4/5 coexistence can create namespace collisions, duplicated shader properties, GUID drift, and direct first-party references into third-party assemblies.
Solution: All investigation and future edits route toward a physical archive, Unity-invisible quarantine for inactive Crest versions, strict asmdef leaf isolation, and first-party access only through unmanaged ocean kinematics contracts.
Rejected Alternatives: Leaving both Crest versions visible to Unity was rejected because it preserves compile collisions and shader import ambiguity. Direct references from Hecton8.Core or Hecton8.Physics were rejected because they make third-party API churn a core compile-wall trigger.
Scalability potential: Low devices use mock/deferred/cached ocean sampling when Crest is unavailable or over budget. Middle uses bounded delayed query batches. High/Ultra can allow richer internal Crest shader/compute work behind the same contract without changing core dependencies.
Hardware Impact: Expected low-end i3/MX350 gain is primarily iteration and import stability; runtime savings come from deferred/capped ocean query batches, estimated 100-1000 microseconds avoided when synchronous Crest sampling would otherwise stall physics.

## Decision 003: Quarantine Crest 5 Outside Unity Visibility

Problem: The active repository had Crest 4 in `Assets/Crest` and Crest 5 in `Packages/com.waveharmonic.crest`, with `Packages/packages-lock.json` still pinning the embedded Crest 5 package even though `manifest.json` did not list it.
Solution: Created `Tools/Crest_Baseline_Archiver.py`, generated compressed backups under `.gitignore`-protected `Docs/Archive/Crest_Baseline_Backup/`, moved Crest 5 and Crest 5 first-party bridge/migration/parity scripts with their `.meta` files under `Docs/Archive/Crest_Version_Quarantine/`, and removed the stale `com.waveharmonic.crest` lock entry through JSON parsing.
Rejected Alternatives: `Assets/~Quarantine_Crest5` was rejected because it remains under Unity's asset tree and violates the user's "outside Unity visibility" constraint. Deleting Crest 5 outright was rejected because restore must remain possible from a bit-for-bit backup.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged; this removes compile/import volatility so later hardware-specific ocean LOD work happens behind one Crest 4 donor boundary.
Hardware Impact: Runtime frame gain is 0 microseconds because this is editor/build hygiene. Developer hardware gain is expected in seconds of avoided package import/domain reload work and lower chance of forced full recompiles on i3/MX350-class machines.

## Decision 004: Confine Crest To Dedicated Bridge Assemblies

Problem: `Hecton8.Plugins`, `Hecton8.Editor`, and `Hecton8.Project.Editor` referenced Crest and WaveHarmonic assemblies directly, so unrelated first-party changes inherited third-party ocean churn.
Solution: Created `Hecton8.Crest.Bridge` and `Hecton8.Crest.Bridge.Editor`, moved the Crest-dependent render validator under the bridge editor folder, removed Crest/WaveHarmonic references from shared asmdefs, and set Crest 4 asmdefs to `autoReferenced=false`.
Rejected Alternatives: Keeping the direct references in broad assemblies was rejected because it defeats the compile wall. Moving active Crest 4 outside Unity was rejected because Crest 4 is the selected donor version.
Scalability potential: Low/Middle/High/Ultra runtime unchanged; compile stability scales developer throughput.
Hardware Impact: Runtime frame gain is 0 microseconds. Editor compile/import savings are expected in seconds per API/import churn event on low-end developer hardware.

## Decision 005: Add Strict Environment.Fluids Ocean Contract Instead Of Breaking Legacy Physics Interface

Problem: Existing `Hecton8.Physics.IHectonOceanKinematics` is already consumed by legacy gameplay and contains managed compatibility methods. Replacing it in-place would break parallel agents and widen the compile blast radius.
Solution: Added the strict forward contract in `Hecton8.Environment.Fluids.Contracts`: explicit-layout DTOs, `NativeArray` request/result submission, `JobHandle` dependency chaining, and no managed collections in the hot path.
Rejected Alternatives: In-place legacy interface surgery was rejected because it would turn a quarantine task into a project-wide physics migration. Managed array return paths were rejected for all new contracts.
Scalability potential: Low uses mock/simplified samples, Middle uses bounded delayed samples, High/Ultra can feed richer Crest-backed approximations behind the same DTO layout.
Hardware Impact: Expected i3/MX350 savings are 50-1000 microseconds under high query pressure by avoiding hidden managed array and synchronous readback paths.

## Decision 006: Superseded - Vault IDs Are Existing ShinobuOcean Lanes

Problem: The prompt requires request, result, water-level, CSV, and telemetry buffers to live in `GlobalDataVault`; adding new BufferID enum values would touch a contested core file.
Solution: Reused existing `H8Memory.BufferID` lanes: `ShinobuOceanWaveReadbackQueries`, `ShinobuOceanWaveReadbackResults`, `ShinobuOceanTelemetryRing`, `ShinobuOceanBeaufortProfiles`, `ShinobuOceanLodState`, and `ShinobuOceanCsvScratch`.
Rejected Alternatives: Editing `H8Memory.cs` was rejected because the IDs already exist and core enum churn risks merge conflicts across 20+ agents.
Scalability potential: Low/Middle/High/Ultra share the same DTO identity; only sample budget and shader detail change with continuous quality.
Hardware Impact: `NativeArrayOptions.UninitializedMemory` on large queues avoids redundant clears, expected 50-300 microseconds saved when buffers are refreshed at high capacity.
Supersession: Decision 008 invalidates this lane choice. The current implementation uses SHINOBU_260 local numeric lanes `(BufferID)72960..72965`.

## Decision 007: Do Not Patch Crest OnEnable/Start In The Quarantine Pass

Problem: Task 12 asks for disabling Crest automatic initialization, but that requires direct donor-source lifecycle changes in `Assets/Crest`. This pass is the containment wall and baseline backup pass; changing Crest internals before quarantine proof increases restore ambiguity.
Solution: Expose cold binding in `CrestOceanRuntimeAdapter`, keep all hot request submission independent of `OceanRenderer.Instance`, and mark the full Crest source lifecycle fence as dependency work for the later Crest-internal ocean agent.
Rejected Alternatives: Editing Crest `OnEnable`/`Start` now was rejected because it mixes quarantine with invasive vendor-source surgery. Ignoring the issue was rejected; it is recorded as `Task 12 [BLOCKED BY DEPENDENCY]`.
Scalability potential: Low/Middle/High/Ultra will benefit only after the future Crest-internal lifecycle patch moves material/ComputeBuffer creation behind a loading-screen boot phase.
Hardware Impact: No microsecond saving is claimed in this pass for Task 12. The expected future saving is avoided first-frame material/command-buffer hitching.

## Decision 008: Supersede Shared ShinobuOcean Vault Lane Reuse

Problem: Sub-agent static audit proved Decision 006 reused Atmosphere-owned `ShinobuOcean*` Vault lanes with incompatible element types: SHINOBU_260 wanted `OceanSampleRequestDTO`, `OceanSampleResultDTO`, `OceanAdapterTelemetryEntry`, `OceanPerformanceProfileDTO`, and `OceanGlobalWaterLevelDTO`, while `ShinobuOceanSurfaceAtmosphereRuntime` already owns those IDs as `float4`, `OceanSurfaceTelemetryEntry`, `BeaufortProfileDTO`, and `OceanSurfaceLodDTO`.
Solution: Keep the central enum untouched and move the Crest quarantine adapter to local numeric `BufferID` casts `72960..72965`, documented in status, audit JSON, ledger, and route card. `OceanAdapterVaultRoute` exposes named constants so editor tools do not hard-code contested IDs.
Rejected Alternatives: Keeping shared IDs was rejected because `GlobalDataVault` type guards can fail with `FatalMemoryException.ThrowVaultTypeMismatch()` depending on allocation order. Editing the central `H8Memory.cs` enum was rejected because a local collision-free numeric lane is enough for this quarantine boundary and avoids a core-header compile-wall touch.
Scalability potential: Low/Middle/High/Ultra share the same lane identity and DTO layout. `GlobalQualityWeight` still scales only sample capacity and approximation detail; it does not change Vault ID ownership.
Hardware Impact: Runtime microsecond gain is not claimed. The fix prevents a boot/runtime hard fault and avoids cross-domain scratch corruption. Static exact-number scan found `72960..72965` only in SHINOBU_260 source before documentation updates.

## Decision 009: Remove Residual OOP And Diagnostic AUP Weaknesses From The Strict Route

Problem: The strict fallback adapter was a managed class despite carrying only a scalar, the new runtime adapter repaired Crest binding during sample submission, the editor AUP gizmo cast absolute doubles directly to `Vector3`, and the bridge editor asmdef referenced forbidden `EasySave3`.
Solution: Converted the fallback to a `readonly struct`, made `ScheduleWaveHeightRequests` consume cold cached authoritative AUP or caller active-origin fallback without component lookup, localized editor AUP draws before float conversion, and removed `EasySave3` from the bridge editor assembly.
Rejected Alternatives: Treating all four as harmless because they were cold/editor-only was rejected; these files define the boundary pattern future agents will copy. Leaving `Transform.position` as AUP source was rejected because AUP authority must come from owner-phase inputs, not presentation transforms.
Scalability potential: Low uses the one-sine fallback and smaller quality budget; Middle expands delayed samples; High/Ultra preserve the same DTOs while increasing detail terms. The polish does not create binary hardware switches.
Hardware Impact: Expected steady-state gain is small, 5-50 microseconds only during missing-binding failure paths. Main impact is preventing GC/presentation-coordinate habits from entering the profiling and diagnostic route.

## Decision 010: Fence Legacy Crest4 Read Accessors Without Owning The Full Physics Migration

Problem: The old `Crest4KinematicsAdapter` still exists for legacy `Hecton8.Physics` consumers. Its `ResolveOceanRenderer()` repaired bindings by calling `TryGetComponent`, and `IsAvailable`/`SeaLevel` routed through that resolver, making read accessors capable of scene lookup and state mutation.
Solution: Keep the legacy API intact, but make binding discovery cold-only in `Awake`. `ResolveOceanRenderer()` now only returns the serialized/cold-bound renderer or logs the missing binding once, while `IsAvailable` and `SeaLevel` use a pure cached-field reader. `Tools/Crest_Quarantine_Polish_Audit.py` now gates this with `legacy_crest4_adapter_no_hot_component_repair`.
Rejected Alternatives: Rewriting the entire legacy `Hecton8.Physics` bridge to the new Environment.Fluids contract was rejected because it crosses into parallel physics/ocean ownership. Leaving the hidden repair untouched was rejected because future agents would copy a read-looking hot path that searches scene state.
Scalability potential: Low/Middle/High/Ultra unchanged at the truth boundary. The strict forward route remains `Hecton8.Environment.Fluids.Contracts`; the legacy route is only made less dangerous until the owning agent retires it.
Hardware Impact: Expected steady-state frame gain is small, 5-50 microseconds only on missing-binding failure paths. Main impact is preventing scene lookup and logging side effects from recurring inside read accessors.

## Decision 011: Remove Base Bridge Singleton Polling And Residual Read-Side Effects

Problem: `CrestBridge` still read `Crest.OceanRenderer.Instance` in visual bridge helpers, and legacy `Crest4KinematicsAdapter` still let some read-like paths log missing providers or fall back through `GlobalRegistry.Fluid`.
Solution: Added a protected cold-bound renderer hook in `CrestBridge`, overridden by `Crest4KinematicsAdapter`. Routed base visual ocean material/camera helpers through the hook. Moved legacy weather, flow, and collision reads to cached-field access and made `SeaLevel` pass `allowRegistryFallback: false`. Expanded `Tools/Crest_Quarantine_Polish_Audit.py` with singleton/read-purity gates.
Rejected Alternatives: Leaving `OceanRenderer.Instance` in the base bridge was rejected because it preserves a global third-party lookup pattern outside concrete adapter ownership. Keeping one-shot `Debug.LogError` in read paths was rejected because read accessors must not mutate diagnostic flags. Fully migrating legacy `Hecton8.Physics` consumers was rejected because the strict forward route is already `Hecton8.Environment.Fluids.Contracts` and broad physics migration is outside this quarantine domain.
Scalability potential: Low devices avoid accidental singleton/global fallback work when visual helpers are queried under missing bindings. Middle/High/Ultra retain the same DTO/Vault route; richer Crest visuals remain hidden behind the bridge and do not change authority identity.
Hardware Impact: Steady-state savings are expected to be small, 0-10 microseconds for base helper calls and 5-50 microseconds on missing-binding legacy reads. The larger impact is architectural: no base bridge code teaches future agents to poll Crest singletons or mutate diagnostics from read paths.

## Decision 012: Integrate Side-Agent Audit Without Expanding Into Foreign Ownership

Problem: Side-agent audit found remaining read-purity and scanner-proof gaps: `CrestBridge` still used `Crest.UnderwaterRenderer.Instance`/`GetComponent` in read helpers, `TryBuildBurstTuning` still used the logging resolver and `ResolveSeaLevel` still carried `GlobalRegistry.Fluid` fallback, the depth-cache bootstrap still had Crest singleton fallback, and the dependency scanner hid Crest reflection strings outside the bridge.
Solution: Made underwater read helpers cache-only and left component lookup only in the imperative `EnsureUnderwaterRenderer` command path. Made `TryBuildBurstTuning` use `TryReadBoundOceanRenderer()` and removed `GlobalRegistry.Fluid` from sea-level resolution. Removed logging from `ResolveOceanRenderer`. Removed `Crest.OceanRenderer.Instance` fallback from depth-cache bootstrap and stopped `ResolveFallbackWaterLevel` from mutating `mapMagicBridge`. Added non-failing `reflection_string_hits` to the dependency scanner.
Rejected Alternatives: Fully migrating `HectonUnderwaterVisuals` reflection fallback was rejected because that is visual/UI ownership and not a compile-time Crest dependency. Renaming legacy duplicate `OceanSampleRequestDTO` was rejected because it is a separate migration with possible consumers. Rewriting celestial/depth-cache ownership was rejected beyond singleton/fallback cleanup because that crosses into World/Celestial owner phase design.
Scalability potential: Low devices avoid missing-binding singleton/log/registry churn. Middle/High/Ultra keep the same strict Environment.Fluids DTO/Vault route; the added reflection bucket gives integrators visibility without changing runtime authority.
Hardware Impact: Small steady-state gain, roughly 0-10 microseconds for underwater read helpers and 5-50 microseconds under missing Crest bindings. Stronger value is forensic: scanner now distinguishes compile breaches from string/reflection migration debt.

## Decision 013: Remove Runtime Crest Reflection Strings Outside Bridge

Problem: `HectonUnderwaterVisuals` did not compile against Crest, but it still carried editor fallback code that searched for `"Crest.OceanRenderer"` and `"Crest.UnderwaterRenderer"` by string. That bypassed the intended `IOceanVisualBridge` route and weakened the claim that non-bridge runtime/presentation code is Crest-ignorant.
Solution: Removed the material and underwater renderer reflection fallback helpers from `HectonUnderwaterVisuals`; the file now gets Crest material/underwater renderer access only through `OceanVisualBridgeRegistry.Active`. Tightened `Tools/Crest_Quarantine_Polish_Audit.py` to gate this and refined `Tools/Crest_Dependency_Scanner.py` so runtime/presentation reflection strings are reported, while editor compliance denylist strings are not misclassified as coupling.
Rejected Alternatives: Leaving editor reflection fallback was rejected because it keeps Crest type identity in a non-bridge file. Moving the fallback into a new bridge-side editor API was rejected for this loop because current bridge commands already provide the needed visual access; adding API surface without a proven consumer would widen the boundary.
Scalability potential: Low/Middle/High/Ultra runtime authority is unchanged. Editor and runtime visuals depend on the active bridge, so Crest complexity stays behind one route regardless of hardware tier.
Hardware Impact: Runtime steady-state gain is effectively 0 microseconds unless the old editor fallback was scanned. The measurable value is compile-wall proof: scanner now reports `reflection_string_hit_count=0` for runtime/presentation files outside the bridge.

## Decision 014: Neutralize Non-Bridge Visual Vocabulary Without Breaking Serialized ABI

Problem: Static side-audit still found donor vocabulary in first-party visual contracts and active prefab data: `IOceanVisualBridge` exposed `UnderwaterRenderer` verbs, `HectonDryVolumeFeature` hard-coded `_Crest_CameraColorTexture`, `HectonUnderwaterVisuals` retained a Crest-named serialized field, `Ocean_Crest.prefab` still had a quarantined Crest5 adapter component reference, and the shared first-party kinematics base was named `HectonCrestOceanKinematics`.
Solution: Rename the core visual contract to `UnderwaterPass` verbs and add `CameraColorTextureId` so only the bridge knows the vendor shader global. Route `HectonDryVolumeFeature` through `OceanVisualBridgeRegistry.Active`. Rename `crestSkyBaseFogLink` to `oceanSkyBaseFogLink` with `[FormerlySerializedAs("crestSkyBaseFogLink")]`. Remove the Crest5 adapter component block and fileID from `Ocean_Crest.prefab`. Rename `HectonCrestOceanKinematics.cs(.meta)` to `HectonOceanKinematicsBridgeBase.cs(.meta)` and update `CrestBridge` plus the editor scanner skip path.
Rejected Alternatives: Leaving hard-coded vendor names in non-bridge code was rejected because future agents copy symbols as architecture. Raw serialized-field rename without `FormerlySerializedAs` was rejected because it resets authored underwater tuning. Renaming `SargassumCrestDampingController` and `HectonPlayerMovement.useCrestOceanHeight` in this loop was rejected because they are serialized Player/World ABI and outside the Crest quarantine ownership boundary; they are vocabulary debt, not direct assembly references.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. The benefit is route stability: all hardware tiers consume the same bridge-owned visual texture ID and underwater pass commands, while the donor can change behind the bridge.
Hardware Impact: Runtime steady-state saving is 0 microseconds. Editor/import gain is avoided missing-script/Crest5 prefab noise and reduced risk of accidental full assembly fanout. Static proof now gates `crest5_prefab_adapter_reference_removed`, `visual_bridge_contract_vendor_neutral`, `dry_volume_reads_vendor_texture_id_through_bridge`, `underwater_visuals_vendor_neutral_pass_vocabulary`, and `ocean_kinematics_base_vendor_neutral`.

## Decision 015: Track Serialized Vocabulary Debt Instead Of Falsifying The Compile-Wall Gate

Problem: After Loop 11, remaining `Crest` text outside the bridge was mixed: some was harmless generic wave-crest language, some was low-risk authoring text, and some was serialized or gameplay ABI in Player/World ownership. Treating all text as a compile breach would be false; ignoring it would overstate donor isolation.
Solution: Neutralized low-risk non-serialized authoring wording in Visor, Atmosphere, Environment, Fluid, and Sargassum comments/tooltips. Expanded `Crest_Dependency_Scanner.py` with non-failing `vocabulary_debt_hits`, while keeping hard failures limited to asmdef/direct API breaches. Fixed the scanner console output to UTF-8 because legacy mojibake lines can break Windows code-page printing even when JSON writing succeeds.
Rejected Alternatives: Renaming `HectonPlayerMovement.useCrestOceanHeight`, `_crestQuery*`, `SargassumCrestDampingController`, or `SyncCrestPrimaryLight` from this quarantine pass was rejected because Player/World/Celestial owners must validate serialized scene/prefab remaps and private gameplay route names. Failing the scanner on those names was rejected because there is no direct Crest assembly edge.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged. The benefit is process quality: donor implementation terms no longer spread through safe authoring text, and remaining ownership-bound names are visible to the proper agents without weakening the compile wall.
Hardware Impact: Runtime steady-state saving is 0 microseconds. Editor/process impact is reduced false-positive scanner work and a cleaner audit trail for future remap passes.

## Decision 016: Treat Active Serialized Assets And Shaders As Part Of The Crest Wall

Problem: Side-agent static audit found hard non-code breaches that the earlier asmdef/C# scanner could not see: Crest5 `WaveHarmonic.Crest` serialized settings assets under `Assets/_Project/Data/CrestMigration`, Crest HLSL includes under shared `Assets/_Project/Art/Shaders`, a direct `Crest::Crest.UnderwaterRenderer` component on `Assets/_Project/Prefabs/Player.prefab`, and a binary Crest5 sandbox scene under `Assets/_Project/Scenes`.
Solution: Move the Crest5 settings assets and binary Crest5 scene, with metas, to `Docs/Archive/Crest_Version_Quarantine/Assets/_Project/...`. Move the Crest-specific sargassum input shaders and metas to `Assets/_Project/Scripts/Plugins/Crest/Shaders/`, preserving shader GUIDs so existing materials keep links. Remove the Player prefab `Crest.UnderwaterRenderer` component block, fileID `9079297290110143596`, and script GUID `1b0c0a69611596146aceb2f60532940c` by exact YAML surgery. Expand `Crest_Dependency_Scanner.py` and `Crest_Quarantine_Polish_Audit.py` to cover active asset/shader/prefab/scene surfaces, not only C# and asmdefs.
Rejected Alternatives: Leaving Crest5 ScriptableObjects or the binary Crest5 scene under active `Assets` was rejected because Unity can still import inactive donor type identities. Moving materials was rejected because shader GUID-preserving moves are enough and avoid needless art remapping. Editing donor Crest source for lifecycle suppression was rejected again because Task 12 remains a separate vendor-source patch dependency. Treating the Player prefab direct component as acceptable because the bridge can create one was rejected because prefab ownership bypasses the command path.
Scalability potential: Low/Middle/High/Ultra runtime authority is unchanged. The bridge remains the only donor owner; higher tiers can still use the Crest-backed visual route, while low tiers can keep the mock/deferred ocean route without importing inactive Crest5 assets or direct prefab vendor components.
Hardware Impact: Runtime steady-state saving is 0 microseconds. Editor/import impact is reduced Unity asset scanning and fewer inactive Crest5/import errors; on i3/MX350-class developer hardware this prevents seconds of avoidable importer/domain churn when opening scenes or refreshing assets.

## Decision 017: Expand Quarantine Proof Beyond `_Project`

Problem: A widened active-filesystem scan found Unity-visible Crest edges outside `_Project`: Easy Save global serializer defaults listed bare `Crest` and `WaveHarmonic.Crest*` assemblies; five root `Assets/InitTestScene*.unity` TestRunner scenes listed `WaveHarmonic.Crest*`; and `Assets/_Recovery` held about 1.2 GB of binary recovery scenes containing direct `Crest::Crest.UnderwaterRenderer` and `Crest5KinematicsAdapter` strings.
Solution: Remove the serializer/test assembly-list entries as exact YAML list deletions, preserving the assets. Move `Assets/_Recovery/` plus `_Recovery.meta` to `Docs/Archive/Crest_Version_Quarantine/Assets/_Recovery/` because recovery payloads are not authoritative source and should not be imported by Unity. Update `Crest_Dependency_Scanner.py` to scan active serialized text in `Assets`, `ProjectSettings`, and `Packages`, catch bare `- Crest` assembly-list entries, and hard-fail an active `Packages/com.waveharmonic.crest` directory.
Rejected Alternatives: Leaving root/plugin assets outside the scanner was rejected because Unity visibility, not folder naming, is the actual contamination boundary. Byte-editing binary recovery scenes was rejected because archival movement is safer and preserves forensic evidence. Moving the root InitTestScene files was rejected because removing only the stale WaveHarmonic TestRunner assembly names keeps test scene structure intact.
Scalability potential: Low/Middle/High/Ultra runtime ocean behavior is unchanged. The benefit is iteration stability: no hardware tier should pay import/reflection overhead for inactive Crest5 or direct donor components in recovery/test/serializer payloads.
Hardware Impact: Runtime steady-state saving is 0 microseconds. Developer hardware gain is import and scan hygiene: `_Recovery` removes 102 Unity-visible files totaling about 1.2 GB from active import scope, which matters on i3/MX350-class machines and slow disks.

## Decision 018: Make The Dependency Wall Scanner Fast Enough To Use Repeatedly

Problem: After widening serialized scan coverage to `Assets`, `ProjectSettings`, and `Packages`, the Python active-asset scanner took about 212 seconds in `scan_active_assets` and about 262 seconds for a full scanner run. The tool was correct but too slow for repeated quarantine proof.
Solution: Use `rg --json -n -a` for active serialized/shader hard-breach search and parse JSON matches back into the existing report schema. Keep the Python path as a fallback when `rg` is unavailable. Bound fallback binary reads to `MAX_ACTIVE_ASSET_SCAN_BYTES` instead of loading entire Unity assets.
Rejected Alternatives: Returning to `_Project`-only scanning was rejected because it already missed Easy Save, root TestRunner scenes, and recovery payloads. Keeping full Python asset reads was rejected because proof tooling should not create minutes of IO pressure on every pass. Failing closed when `rg` is absent was rejected because CI or developer machines may still need a pure-Python fallback.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged. The gain is developer-iteration scalability: broader static proof can now be rerun during future Crest edits without turning every check into a multi-minute IO event.
Hardware Impact: On this workspace, full scanner wall time dropped from about 262 seconds to about 35.5 seconds, saving roughly 226 seconds per full proof pass. Runtime frame saving remains 0 microseconds.

## Decision 019: Close Unity Assembly Sidecar And GUID Reference Gaps

Problem: The dependency wall scanner proved named `.asmdef` references to Crest, but Unity can also store assembly references as `GUID:<asmdef-guid>` and route folders through `.asmref` files. Active Crest 4 asmdef GUIDs are `5b35af79ebbe89647a157055d52c59d3` for `Crest` and `59cd48da98d9e4a80917b613abe9416e` for `Crest.Helpers.Editor`.
Solution: Expand `Tools/Crest_Dependency_Scanner.py` to collect both `.asmdef` and `.asmref` files, classify `.asmref` references, and treat the Crest asmdef GUID strings as hard Crest references outside `Assets/_Project/Scripts/Plugins/Crest`. Add a polish audit gate proving this scanner behavior is present.
Rejected Alternatives: Relying on assembly names was rejected because a future Unity inspector change can rewrite references to GUID form without changing compile behavior. Treating `.asmref` as harmless was rejected because it can attach a folder to an assembly and bypass the named asmdef reference list.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged. Developer scalability improves because the compile-wall proof now covers the Unity sidecar formats that can silently widen recompile fanout.
Hardware Impact: Runtime frame saving is 0 microseconds. The hardware benefit is avoided future editor/compiler churn: exact scans now prove no non-bridge Crest asmdef GUID or Crest `.asmref` route is active.

## Decision 020: Block Active Backreferences To Archived Crest Assets By GUID

Problem: Moving Crest5 settings, the Crest5 sandbox scene, and `_Recovery` outside Unity visibility removes the source files, but active YAML could still hold pure GUID references to those archived objects without containing `WaveHarmonic.Crest` or file-name text.
Solution: Extract the archived GUIDs and add them to `Tools/Crest_Dependency_Scanner.py` as `QUARANTINED_ASSET_GUIDS`: `ed12880d16f3f2f4e80ceee64594101d`, `149ebcba5c729ad49911b1ea4b8456fd`, `0ef7bde4d259c9d4abcc93f41b0903a0`, and `a73ab923bdc811242bdca5f288eb3877`. Include the GUIDs in both ripgrep and Python active serialized breach patterns, and add a polish audit gate for this behavior.
Rejected Alternatives: Keeping the GUID search as a one-off manual command was rejected because future agents could reintroduce an active reference and still pass the normal scanner. Failing on all archived GUIDs under `Docs/Archive` was rejected; the breach scope is active Unity-visible `Assets`, `ProjectSettings`, and `Packages`.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged. The benefit is asset lifecycle scalability: inactive donor assets stay restorable in archive without becoming hidden active dependencies.
Hardware Impact: Runtime frame saving is 0 microseconds. Developer hardware impact is avoided missing-reference/import churn when Unity refreshes scenes or serialized assets.

## Decision 021: Fail The Scanner If Crest Donor Or Bridge Becomes Auto-Referenced

Problem: A direct-reference scanner can report clean while `autoReferenced=true` silently lets Unity inject Crest or the bridge into broader compilation. The current files are guarded, but the proof was audit-local and not part of the hard dependency scanner.
Solution: Add scanner checks for active Crest donor asmdefs and bridge asmdefs that reference Crest. `crest_donor_asmdef_auto_referenced` fails if Crest donor runtime/editor asmdefs are not explicitly `autoReferenced=false`. `bridge_crest_asmdef_auto_referenced` fails if an allowed bridge asmdef references Crest while auto-referenced. Polish audit now gates both source behavior and current runtime/editor bridge/donor asmdef state.
Rejected Alternatives: Trusting status documentation was rejected because a future Unity inspector toggle can alter `autoReferenced` without adding any C# or asmdef reference line. Audit-only checking was rejected because the dependency scanner is the primary repeatable wall gate.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged. Developer scalability improves because Crest donor changes stay leaf-scoped instead of forcing unrelated assemblies into the compile wall.
Hardware Impact: Runtime frame saving is 0 microseconds. Editor hardware impact is avoided seconds-scale domain reload/recompile fanout if an asmdef toggle regresses.

## Decision 022: Track Global Crest Scripting Defines Without Breaking The Active Donor

Problem: `ProjectSettings/ProjectSettings.asset` still contains Standalone scripting defines `CREST_OCEAN` and `CREST_URP`. These symbols do not create an asmdef reference by themselves, but they are global compile switches and can become a hidden donor route if first-party code outside the bridge starts using `#if CREST_OCEAN` or `#if CREST_URP`.
Solution: Extend `Tools/Crest_Dependency_Scanner.py` with `scan_first_party_scripting_define_usage()` and `scan_global_scripting_defines()`. Non-bridge first-party Crest preprocessor branches are hard breaches. The current global PlayerSettings symbols are reported as `global_scripting_define_hits` so integrators can see the contamination without breaking the selected Crest 4 donor.
Rejected Alternatives: Deleting `CREST_URP` immediately was rejected because active Crest 4 donor C# and HLSL under `Assets/Crest` uses the symbol for URP-specific code. Treating the PlayerSettings line as a hard breach was rejected because it would fail the wall while the selected donor still requires it. Ignoring the line was rejected because global defines can activate future non-bridge conditional code silently.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged. Developer scalability improves because future Crest-symbol branches outside `Assets/_Project/Scripts/Plugins/Crest` fail in the scanner instead of becoming hidden compile-wall dependencies.
Hardware Impact: Runtime frame saving is 0 microseconds. Editor/build impact is avoided future compile fanout from donor preprocessor symbols leaking into first-party assemblies; no steady-state frame claim.

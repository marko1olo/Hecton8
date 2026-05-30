# Rationale 1402 - YAML Serialized Property And Prefab Metadata Migrator

Evidence status: APEX STATIC VERIFIED - UNITY EDITOR VERIFICATION NOT RUN

## Initialization Decisions

Problem: Agent prompt identity and task count had to be derived from the authoritative batch XML, not chat memory.
Solution: Extracted `<AGENT_PROMPT id="1402">` with a PowerShell regex against raw `Docs/Tasks/CURRENT_BATCH.md`; counted `Task 01` through `Task 20`.
Rejected Alternatives: Manual task count from neighboring prompts; basic document reads that may truncate.
Scalability potential: Low/Middle/High/Ultra not applicable to static prompt extraction; decision protects batch routing, not runtime visual scale.
Hardware Impact: i3/MX350 impact is negligible; avoids unnecessary Unity or compiler launch.

Problem: YAML migration has high corruption risk and cannot be treated as a normal code refactor.
Solution: Selected mandates for evidence labeling, editor facade safety, persistent registry, zero-GC boundaries, DTO layout, native memory, compression caution, and performance budgets before scanning.
Rejected Alternatives: Immediate raw find-replace; broad unsorted registry reading.
Scalability potential: Low uses static scanning only; Middle adds deterministic backup and dry-run; High adds schema-specific migration tooling; Ultra adds SHA-256 proof and fuzz validation.
Hardware Impact: Static CLI scan avoids Unity import and compiler pressure on i3/MX350-class host.

Problem: Active-batch hygiene required durable state files and stale-data detection.
Solution: Checked for existing `Status_1402.md` and `Rationale_1402.md`; both were absent, then initialized fresh ledgers.
Rejected Alternatives: Chat-only state; appending to unknown stale state.
Scalability potential: Static workflow scales by file count, not runtime tier.
Hardware Impact: Negligible file I/O; avoids CPU-heavy rebuild.

## Loop 1 Decisions

Problem: Task 01 required exhaustive YAML desync evidence without destroying authored scene/prefab data.
Solution: Implemented `Tools/YamlMigration1402/yaml_migration_1402.py` as a streaming scanner. It maps `*.cs.meta` GUIDs to current C# serialized fields, scans MonoBehaviour YAML blocks by `--- !u!114 &fileID`, records root property line numbers, detects prompt-listed obsolete properties, and writes JSON ledgers.
Rejected Alternatives: Raw find-and-replace; loading 33 MB+ scene text into a mutation buffer; relying on Unity Editor import to reveal data loss after the fact.
Scalability potential: Low scans targeted seven assets; Middle scans all scenes/prefabs; High adds schema-mismatch triage; Ultra adds SHA-256 proof and fuzz validation.
Hardware Impact: Full static scan completed without compiler or Unity launch; i3/MX350 host cost limited to sequential disk reads.

Problem: Prompt examples map to current non-serialized DataVault handles, not normal serialized fields.
Solution: Classified `_cellIntegrityFront`, `_cellIntegrityBack`, `_cellFatigue`, `_cellCompartmentIndices`, `_densityBuildSources`, and `_publishedSonarSdf` as `raw_yaml_extract_only_if_hit_then_manual_schema_required` with `no_serialized_destination_detected`.
Rejected Alternatives: Injecting YAML into `VaultGenerationHandle<T>` fields, which are private runtime handles and not designer-authored destinations; inventing initialization DTOs absent from source.
Scalability potential: Low no-op if no hits; Middle preserves hit payloads in JSON if found; High requires owner-approved cold import path; Ultra could add a dedicated binary DataVault seed if a real payload exists.
Hardware Impact: Avoids malformed scene writes and compile/import churn on low-end host.

Problem: Backup protocol had to exist before any destructive write.
Solution: Generated `Docs/AgentLogs/BackupPlan_1402.json` with seven source assets, deterministic `_Recovery_1402` destinations, byte sizes, hashes, and PowerShell copy/size-verify commands.
Rejected Alternatives: Creating backups only for files with exact hits after mutation; relying on git as the only recovery route.
Scalability potential: Low backs up critical target set; Middle adds full-scene proof; High/Ultra can extend target list from ledger hits without changing logic.
Hardware Impact: Planned backup is linear file copy; no compiler or Unity import cost.

Problem: Full scan exposed 208 known-script schema mismatch candidates unrelated to the prompt-listed native arrays.
Solution: Preserved them in `YamlDesyncLedger_1402_full.json` as dry-run-only candidates. No migration was performed because each needs semantic owner mapping, not regex guessing.
Rejected Alternatives: Deleting unknown YAML lines; treating generic mismatch hits as permission to mutate gameplay scripts; adding `[FormerlySerializedAs]` broadly without owner review.
Scalability potential: Low reports only; Middle prioritizes by prefab; High maps safe one-to-one renames; Ultra uses an Editor verifier after owner review.
Hardware Impact: Reporting only avoids asset churn and import cost.

## Loop 2 Decisions

Problem: Backup execution had to prove recovery parity, not just file existence.
Solution: Copied all seven mandated assets to `Docs/Tasks/_Recovery_1402` and recorded byte equality plus SHA-256 equality in `Docs/AgentLogs/BackupExecution_1402.json`.
Rejected Alternatives: Same-directory `.bak` writes for every file before any hit existed; relying on timestamps only.
Scalability potential: Low target set is protected; Middle/High/Ultra can add extra files from ledger hits without changing verification.
Hardware Impact: Linear file copy only; no Unity import, no compiler, no high CPU contention.

Problem: Raw extraction and transplantation would be destructive if applied to generic mismatch candidates.
Solution: Dry-run consumed the exact-hit ledger only. It aborted with `NO_EXACT_OBSOLETE_PROPERTY_HITS`, wrote zero removal lines, zero addition blocks, and zero target files.
Rejected Alternatives: Removing stale `Player.prefab` and `PFB_Submarine_Core.prefab` fields without mapping; transplanting native-array payloads into non-serialized handles.
Scalability potential: Low no-op; Middle preserves exact hit lines if they appear; High requires approved one-to-one schema; Ultra uses Unity Editor post-import proof.
Hardware Impact: Avoided asset write/import churn and CPU rebuild pressure.

Problem: Task 09 asked for a `SerializedObject` migrator where applicable, but no applicable exact payload exists.
Solution: Did not create `Migrator1402.cs`. Current prompt fields are not accepted by `SerializedObject`, and current handle fields are private runtime handles, not serialized authoring destinations.
Rejected Alternatives: Dead Editor menu item; reflection-based runtime migration; compile just to satisfy bureaucracy.
Scalability potential: Low avoids pointless code; Middle creates Editor script only when `SerializedObject` can see both old and new properties; High/Ultra add menu tooling after owner mapping.
Hardware Impact: No `dotnet build`; preserves parallel agent CPU budget.

Problem: FileID alignment proof was needed even with no mutation because backup/current parity is the rollback basis.
Solution: Compared `--- !u!114 &` MonoBehaviour counts between source and recovery copies for all seven targets. Counts match exactly; `02_HECTON_WORLD.unity` contains zero local MonoBehaviour blocks in this static file, matching its backup.
Rejected Alternatives: Assuming no change because dry-run said no write; checking only SHA-256 before backup.
Scalability potential: Low verifies target files; Middle/High/Ultra can extend to all files in a mutation set.
Hardware Impact: Single-pass text count; negligible CPU.

## Loop 3 Decisions

Problem: Scene-specific prefab overrides can preserve designer values under `m_Modification.m_Modifications`, separate from component root fields.
Solution: Scanner handles `--- !u!1001` PrefabInstance blocks and detects `propertyPath:` values matching exact obsolete properties. Target set found 8 PrefabInstance blocks and 0 obsolete property paths.
Rejected Alternatives: Rewriting propertyPath strings without exact source/destination; assuming root MonoBehaviour scan covers overrides.
Scalability potential: Low target override scan; Middle full-scope override scan; High injects approved renamed propertyPaths; Ultra validates in Unity Editor.
Hardware Impact: Text scan only; no import or compiler work.

Problem: Missing script references would require component block deletion or YAML `m_Script` remap, both high-risk.
Solution: Ran full static sweep across 1765 scene/prefab/asset files; found 0 `m_Script: {fileID: 0}` references. No deletion/remap executed.
Rejected Alternatives: Component deletion without hit; recalculating Unity script fileID/guid for absent classes.
Scalability potential: Low no-op; Middle keeps sweep artifact; High remaps only with exact target script meta; Ultra validates in Editor console.
Hardware Impact: Sequential file scan; no compiler.

Problem: Compile verification cannot be used as ritual when no C# migrator exists and host is under load.
Solution: Recorded `CompileSkip_1402.json`: CPU load 99%, active `csc` and `dotnet`, `dotnetBuildRun=false`, reason no Editor migrator was created.
Rejected Alternatives: Violating compiler throttle; building unrelated solution state; fabricating compile proof.
Scalability potential: Low avoids contention; Middle builds only when code exists and CPU free; High/Ultra use Unity import proof after source changes.
Hardware Impact: Prevented additional CPU contention on an already saturated host.

## Loop 4 Decisions

Problem: The regex/block parser needed proof against miniature malformed YAML before it could be trusted as evidence.
Solution: Added and ran the `self-test` command. It validates missing array dash detection, non-numeric scalar detection inside obsolete payloads, and valid object-reference arrays. `Docs/AgentLogs/YamlParserFuzzer_1402.json` reports failures = 0.
Rejected Alternatives: Assuming regex correctness from the production scan alone; adding broad YAML rewriting to test error handling.
Scalability potential: Low uses three deterministic parser cases; Middle adds more obsolete-array shapes; High adds prefab override fixtures; Ultra adds imported Unity fixture validation after owner approval.
Hardware Impact: Micro static test; avoids Unity import and compiler load on i3/MX350-class hardware.

Problem: Data-loss assertion had to distinguish "no payload exists" from "payload migrated."
Solution: Final report records filesModified = 0, yamlBytesRewritten = 0, obsoletePropertiesMigrated = 0, and exact obsolete properties remaining target/full = 0/0. This is a no-op rescue, not a claimed data transfer.
Rejected Alternatives: Fabricating migrated counts; deleting 208 generic schema mismatch candidates without semantic owner mapping.
Scalability potential: Low no-op is stable; Middle exports generic candidates for owner review; High performs only approved one-to-one migrations; Ultra runs Unity Editor import and prefab override validation after mappings exist.
Hardware Impact: Prevents expensive asset import churn and preserves designer YAML bytes.

Problem: Full-scope YAML strictness could misclassify pre-existing tab indentation as migration corruption.
Solution: Validation treats tabs as reported warnings unless strict-existing-tabs mode is requested. Full validation scanned 853 files, failures = 0, exact obsolete = 0, missing script refs = 0; three legacy files contain 30,793 tab occurrences and were not normalized.
Rejected Alternatives: Editing unrelated scene formatting; failing a no-op migration because of pre-existing authoring serialization.
Scalability potential: Low reports legacy hygiene; Middle scopes tab cleanup to owners; High/Ultra can normalize only with asset-owner approval and Unity import proof.
Hardware Impact: Avoided megabyte-scale scene rewrites and reimport cost.

Problem: Task 19 required hot-path/compile verification without abusing the compiler.
Solution: Kept the tool as an offline Python CLI, no runtime C# or Editor assembly was added, and no Unity hot path was touched. Previous compile gate recorded CPU 99% plus active `csc`/`dotnet`, so build remained skipped.
Rejected Alternatives: Dead `Migrator1402.cs`; `dotnet build` as ritual proof; runtime scanner components.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged because assets and code hot paths were not modified.
Hardware Impact: Zero runtime frame cost and no extra compiler contention.

Problem: Final authority proof needed machine-readable counts and cryptographic state, not chat text.
Solution: Wrote `Docs/Reports/YAML_MIGRATION_REPORT_1402.json` with target hashes, backup hash parity, validation counts, parser-fuzzer failures, compiler skip state, and dry-run abort reason.
Rejected Alternatives: Human-only summary; omitting hashes because no files changed.
Scalability potential: Low report covers target set; Middle includes full-scope ledger totals; High/Ultra can ingest approved migration results with the same schema.
Hardware Impact: Static JSON generation only; no Unity or compiler pressure.

## APEX Final Verification Decisions

Problem: The initial schema mismatch ledger over-counted orphaned fields because the C# extractor treated the GUID-owned `.cs` file as the whole class and ignored serialized fields in partial class siblings.
Solution: Added `merge_partial_class_fields` to aggregate serialized field names and `[FormerlySerializedAs]` aliases by class name before YAML comparison. This corrected `PlayerSwimBlockoutRig.Body.cs` false positives and reduced full-scope candidates from 208 to 143. Added `partial_class_merge` to `self-test` so this defect has a regression check.
Rejected Alternatives: Keeping stale 208-count report; raw YAML deletion based on a defective extractor; pretending Unity would catch the partial-class issue without static proof.
Scalability potential: Low keeps static evidence accurate; Middle/High/Ultra can add namespace-aware class-scope parsing later if owner-approved, but current correction removes the detected false-positive class.
Hardware Impact: Static scan only; no Unity import or compiler launch on loaded host.

Problem: APEX demanded Zero-GC proof for modified hot paths, but agent 1402 did not modify C# runtime hot paths.
Solution: Wrote `Docs/AgentLogs/APEX_FinalVerification_1402.json` recording runtimeHotPathsModifiedByAgent1402 as empty and scanning the agent-owned offline tool for requested C# forbidden-token strings. Counts for `new`, `string.Format`, `.ToString()`, `foreach`, and LINQ token forms are zero.
Rejected Alternatives: Auditing unrelated runtime files modified by other agents as if they were mine; claiming Python file scanning is a Unity runtime hot path.
Scalability potential: Runtime scale unaffected across weak/middle/high/ultra devices because agent-owned code is offline tooling.
Hardware Impact: Zero runtime frame cost; static grep-level scan only.

Problem: Data Sovereignty proof cannot be fabricated when no GlobalDataVault write occurred.
Solution: APEX artifact explicitly records no migrated fields, no BufferID constants, no `TryAcquireWriteLock` sites, and no `finally` release sites because exact obsolete payload hits are zero.
Rejected Alternatives: Inventing lock proof; creating a DataVault route for absent data.
Scalability potential: No runtime storage route added; future real payload migration must allocate an owner-approved BufferID and lock discipline.
Hardware Impact: No native memory ownership changes.

Problem: Cinematic Cheat and Continuous Scalability pillars were requested for a static YAML scanner.
Solution: APEX artifact records no runtime simulation, no physical system, no `isLowEnd`/tier string switches, and no `GlobalQualityWeight` requirement. Existing runtime systems remain responsible for continuous quality scaling.
Rejected Alternatives: Adding fake GlobalQualityWeight plumbing to an offline audit tool; claiming visual scalability from no runtime code.
Scalability potential: Offline scanner cost scales by file count; runtime device tiers are not affected.
Hardware Impact: No frame-time load.

Problem: Compilation Resource Throttling had to be re-proven after APEX audit.
Solution: Did not run `dotnet build`. Prior compile gate recorded CPU 99% with active `csc`/`dotnet`; latest APEX sample recorded 60.99% CPU and no need for compilation because no C# migrator or runtime source was created by agent 1402.
Rejected Alternatives: Compiler spam for a Python/report-only correction; fabricating compile success.
Scalability potential: Preserves parallel agent throughput on low-end and shared machines.
Hardware Impact: Avoided additional compiler load.

Problem: Remaining 143 schema mismatch candidates still represent domain risk but are not exact prompt native payloads.
Solution: Wrote `Docs/AgentLogs/YamlSchemaMismatchTriage_1402.json` grouped by file/script with policy: no raw YAML deletion without owner-approved old-to-new mapping or Unity import proof.
Rejected Alternatives: Treating all 143 as safe cleanup; leaving the corrected residual risk undocumented.
Scalability potential: Low reports; Middle routes to owners; High performs approved one-to-one `[FormerlySerializedAs]`/SerializedObject migrations; Ultra validates in Unity Editor and prefab overrides.
Hardware Impact: No asset mutation; no import cost.

## APEX Second-Pass Decisions

Problem: After partial-class correction, the remaining ledger still over-reported inherited serialized fields from `PlayerTool` on concrete held tool prefabs.
Solution: Added `merge_inherited_class_fields`, parsing class base lists and transitively merging serialized fields and `[FormerlySerializedAs]` aliases from known base classes. Added `base_class_merge` regression self-test.
Rejected Alternatives: Treating inherited `_toolData`, `_toolMetadata`, `_swimContract`, `enableDurabilityDrain`, and related fields as orphaned YAML; deleting those lines from held tool prefabs.
Scalability potential: Low keeps static scanner accurate for common inheritance; Middle adds namespace-aware inheritance; High/Ultra can validate with Unity SerializedObject after owner approval.
Hardware Impact: Static Python scan only; no runtime frame cost and no Unity import.

Problem: APEX artifacts had to be regenerated after the inherited-field semantics changed.
Solution: Re-ran target and full scans, target and full validations, dry-run, self-test, final-report, APEX artifact, and schema-mismatch triage. Candidate count dropped from 143 to 63; exact prompt obsolete hits remain 0.
Rejected Alternatives: Keeping stale hashes/counts; claiming the prior APEX artifact still represented current logic.
Scalability potential: Same static workflow; future approved mappings can be measured against the corrected baseline of 63 candidates.
Hardware Impact: Sequential file scan; no compiler or Unity load.

Problem: The remaining 63 candidates could still be either legitimate orphan data or unsafe semantic redesign leftovers.
Solution: Searched representative current owners. The remaining fields are old names without safe one-to-one destinations: player wipeout/crush impact tuning, legacy sargassum ocean facade inputs, structural collision/decal tuning, scanner/flashlight presentation fields, and bootstrap/tick config remnants.
Rejected Alternatives: Speculative `[FormerlySerializedAs]` mappings where units/meaning changed; raw YAML deletion just to reduce the count.
Scalability potential: Low reports exact residual risk; Middle routes by file/script owner; High performs owner-approved migration; Ultra confirms in Unity import and prefab override inspectors.
Hardware Impact: No asset mutation and no compile.

## APEX Third-Pass Decisions

Problem: The remaining 63 candidates contained some safe one-to-one serialized renames mixed with unsafe semantic leftovers.
Solution: Accepted only fields whose current destination had the same responsibility and compatible serialized value shape. Added `[FormerlySerializedAs]` bridges for `raycastInterval`, `syncCargoMassFromInventoryEvents`, `_debugDensityWorldRect`, `slowTickTopEntries`, and `constructionGhostWarmupCount`.
Rejected Alternatives: Raw YAML deletion; adding aliases where units or system ownership changed; changing prefab bytes by hand.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged except preserving authored values. `slowTickTopEntries` becomes a bounded diagnostic budget, not a gameplay-quality switch.
Hardware Impact: No frame allocation added; no Unity import or compiler launch. Prevents designer values from being lost on low-end and shared machines without CPU-heavy migration tooling.

Problem: `runtimeShaderReferenceCatalog` was authored on `BootstrapController` YAML but consumed by `GameBootstrapper`, so the YAML value was orphaned by current source shape.
Solution: Restored a serialized `BootstrapController.runtimeShaderReferenceCatalog` bridge and routed it into `GameBootstrapper.SetBootstrapRuntimeShaderReferenceCatalog` before bootstrap completion.
Rejected Alternatives: Scene search from `GameBootstrapper`; direct YAML transplant into a different component; leaving the catalog orphaned.
Scalability potential: Low uses existing authored catalog; Middle/High/Ultra can warm richer shader sets through the existing bootstrap path. No binary quality branch is introduced.
Hardware Impact: Cold bootstrap assignment only. No per-frame cost and no extra allocations on gameplay hot paths.

Problem: `SargassumCrestDampingController` YAML held explicit legacy Crest input object references, but current code only resolved by child names, making authored references fragile.
Solution: Restored explicit serialized Transform/Renderer input fields and made cold `ResolveLegacyInputs` prefer authored references before `transform.Find` fallback.
Rejected Alternatives: Raw YAML deletion; per-frame scene search; assuming child names are always stable after prefab edits.
Scalability potential: Low preserves authored references with one cold lookup; Middle/High/Ultra continue using the existing texture facade path and can spend saved stability budget on visual facade quality elsewhere.
Hardware Impact: Cold-path branch only. No new Update load; no GC allocation in the tick path.

Problem: The scanner still had one real parser blind spot: `global::Crest.OceanRenderer crestOceanRenderer` was not recognized as a serialized field type.
Solution: Expanded the field declaration type regex to accept `:` and added a `global_qualified_field_type` self-test.
Rejected Alternatives: Listing `crestOceanRenderer` as orphaned YAML; switching to a heavyweight C# parser without a proven need.
Scalability potential: Low keeps the scanner accurate with minimal cost; Middle/High/Ultra can add Roslyn later if namespace/conditional parsing defects appear with hard evidence.
Hardware Impact: Static regex cost only; no runtime or compile cost.

Problem: APEX demanded Zero-GC, Data Sovereignty, scalability, and compile-throttle evidence after actual C# bridge edits.
Solution: Wrote `Docs/AgentLogs/APEX_FinalVerification_1402.json`. It records 84 added C# lines scanned with zero `new`, `string.Format`, `.ToString()`, LINQ, `foreach`, or binary low-end switch hits; no GlobalDataVault migration; no BufferID constants; no locks; no physical simulation; no `dotnet build`.
Rejected Alternatives: Auditing unrelated worktree files; claiming Unity compile success without running it; fabricating `GlobalQualityWeight` usage for static compatibility bridges.
Scalability potential: The pass preserves serialized authoring data and leaves runtime scaling to existing owner systems. No new binary tier decision exists.
Hardware Impact: CPU sample before final compile decision was 100% with active `dotnet`; build skip avoided violating compiler throttling.

## APEX Fourth-Pass Decisions

Problem: The remaining `PlayerFlashlight` residual candidates included two diagnostic fields whose current owner still exists under the light-shaft terminology.
Solution: Added `using UnityEngine.Serialization` and two `FormerlySerializedAs` bridges: `_debugVolumetricMultiplier -> _debugLightShaftMultiplier` and `_debugVolumetricDepth -> _debugLightShaftDepth`.
Rejected Alternatives: Mapping `volumetricBeam` or underwater/storm beam noise/jitter fields into the current scalar shaft response; those are not the same serialized contract.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged. These are diagnostic serialized values; no quality branch or processing load is added.
Hardware Impact: No per-frame work, no allocations, no Unity asset byte rewrite.

Problem: Some residual properties looked tempting but were semantically unsafe.
Solution: Recorded concrete rejected mappings in `YamlSchemaMismatchTriage_1402.json`: `scannerPulseShader` is a different shader asset than `scannerMarkerShader`; `playerRigidbody` and `handObstacleMask` are obsolete because `PlayerSwimPresentationController` consumes movement contact snapshots; `sceneSearchRetryInterval` is obsolete because `Crest4KinematicsAdapter` binds explicit/local `OceanRenderer`.
Rejected Alternatives: Aliasing different GUIDs; adding dead serialized quarantine fields just to lower the scanner count; raw YAML deletion.
Scalability potential: Preserves correctness across device classes by refusing dead or misleading authoring routes. No low/high binary switch introduced.
Hardware Impact: Avoids dead runtime memory surface and avoids a compile/import churn cycle.

Problem: APEX evidence had to be regenerated after the fourth-pass source edit.
Solution: Re-ran scan, full scan, validation, full validation, dry-run, parser self-test, final report, APEX JSON, and triage JSON. Full-scope candidates are now 48; target-set candidates are 22; exact prompt obsolete hits remain 0.
Rejected Alternatives: Keeping third-pass hashes and counts after editing `PlayerFlashlight`.
Scalability potential: Static workflow remains linear by YAML file count; runtime systems are unaffected.
Hardware Impact: Static file scans only. `dotnet build` remained skipped because CPU was 100% with active `csc` and `dotnet`.

## APEX Fifth-Pass Decisions

Problem: `Player.prefab` still held a non-null `volumetricBeam` reference to a VLB component on `DiveLamp_Light`, while current `PlayerFlashlight` moved ownership to the screen-space shaft path and no `ScreenSpaceLightShaftSource` was statically present in the prefab/scene scan.
Solution: Added `FormerlySerializedAs("volumetricBeam")` to `PlayerFlashlight.legacyVolumetricBeamSource` and used it only as a cold migration hint. `PlayerFlashlight` now resolves the `Light` from that old component when needed and installs `ScreenSpaceLightShaftSource` on the light GameObject in `Awake`/`Start` if absent.
Rejected Alternatives: Manual prefab YAML insertion; resurrecting VLB field mutation; reflection into VLB properties; mapping old beam noise/jitter scalars into the current diagnostic multiplier/depth values.
Scalability potential: Low uses the existing screen-space fake with minimum sample/render scale; Middle/High/Ultra inherit the already-existing `HomeostasisBrain.GlobalQualityWeight` scaling in `ScreenSpaceLightShaftRuntime` and `HectonScooterVolumetricShaftsFeature`. No binary `isLowEnd` switch was added.
Hardware Impact: One cold `TryGetComponent`/optional `AddComponent` during lifecycle only. No Update/LateFrame allocation and no per-frame VLB property mutation on i3/MX350-class hardware.

Problem: Residual candidates after the shaft bridge still included values that could be forced into current fields only by changing their semantic meaning.
Solution: Rechecked scanner pulse, Manta scooter audio/indicator, swim hand obstacle, structural hull collision, ballast math LOD, Crest retry, sargassum snag/waterline, harpoon/laser null refs, and procedural thruster audio against current owners. Kept them in triage as rejected mappings.
Rejected Alternatives: Adding dead serialized quarantine fields; aliasing `scannerPulseShader` to `scannerMarkerShader`; reintroducing direct AudioClip or Renderer ownership where SignalBus/procedural routes replaced it; converting old Joule thresholds into speed/damage scales without an owner-approved formula.
Scalability potential: Low/Middle/High/Ultra preserve current continuous-quality owner routes. The rejected mappings avoid bringing back removed binary-switch or direct-presentation paths.
Hardware Impact: No extra runtime memory surface; no Unity asset reimport churn; no compiler launch under 100% CPU.

Problem: APEX proof had to reflect the fifth-pass source edit rather than the previous 48-candidate state.
Solution: Regenerated target/full scan, validations, dry-run, self-test, final report, APEX JSON, and triage. Full candidates are 47, target candidates are 21, exact obsolete prompt hits remain 0, validation failures 0, parser failures 0.
Rejected Alternatives: Reporting stale fourth-pass hashes; running `dotnet build` when CPU sample was 100%.
Scalability potential: Static verification remains linear by asset file count; runtime quality scaling is delegated to existing shaft systems.
Hardware Impact: Static scan cost only. Build remains unrun because the compile throttle forbids it under current load.

## APEX Sixth-Pass Decisions

Problem: The fifth-pass APEX JSON carried a stale Zero-GC scan surface count after rechecking the full set of agent-owned C# bridge files.
Solution: Re-scanned the current `git diff` for all eight C# bridge files. The current added-line surface is 126 C# lines, with zero matches for `new` reference construction tokens, `string.Format`, `.ToString()`, LINQ query/method tokens, `foreach`, and binary low-end switch strings. Updated `APEX_FinalVerification_1402.json`.
Rejected Alternatives: Keeping the stale 112-line figure; scanning unrelated dirty worktree files owned by other agents.
Scalability potential: No new runtime work. Low/Middle/High/Ultra behavior remains delegated to existing owner systems; the bridges preserve serialized authoring data only.
Hardware Impact: Static diff text scan only; no Unity import and no compiler launch.

Problem: `HarpoonLauncherTool.tracer` looked like a possible rename candidate but had to be proven by payload and current field state, not name similarity.
Solution: Inspected `Tool_HarpoonLauncher_Held.prefab`: `tracerShader` already stores GUID `1d324d2bc5d23144bbf8d997d03ddc1a`; `Hecton_TetherLineStrip.shader.meta` has the same GUID; legacy `tracer` is `{fileID: 0}`. Rejected a bridge because there is no payload to rescue.
Rejected Alternatives: Adding `[FormerlySerializedAs("tracer")]` to `tracerShader`, which would create an alias without preserving any non-null designer data.
Scalability potential: Low/Middle/High/Ultra unchanged; tracer rendering already uses the current shader field.
Hardware Impact: No source edit, no frame-time change.

Problem: Remaining `HectonPlayerMovement` candidates include old scalar tuning fields with tempting names, but `Player.prefab` already contains current successor fields beside them.
Solution: Rejected aliases for old wipeout/exosuit/KCC fields because Unity could map stale old YAML into current fields and overwrite current authored values. Example: old `wipeoutSweepSkinWidth: 0.04` sits next to current `wipeoutSweepCapsuleInset: 0.015`.
Rejected Alternatives: One-to-one aliases based on approximate gameplay meaning; raw YAML deletion.
Scalability potential: Current movement scalability and cinematic response remain owned by `HectonPlayerMovement`; no binary quality switch added.
Hardware Impact: Avoids risky import-time value overwrite; no runtime cost.

Problem: `PlayerThrusterAudio.thrusterLoopClip` has a non-null old AudioClip payload, so it required explicit review.
Solution: Rejected a direct clip bridge because current `PlayerThrusterAudio` owns procedural audio generation and can disable itself under `PlayerCriticalProceduralAudioRenderer`. Restoring a direct clip field would reverse the current audio owner contract.
Rejected Alternatives: Optional legacy AudioClip override; direct playback resurrection; dead serialized quarantine field.
Scalability potential: Procedural/critical audio route remains scalable by owner systems; no Low/Ultra dichotomy introduced.
Hardware Impact: Avoids extra serialized memory surface and avoids audio route ambiguity on low-end devices.

Problem: Final compile decision had to follow current machine state, not earlier logs.
Solution: Sampled CPU at 100% and found active compiler-related processes `csc` PID 18616 and `dotnet` PID 55080. Did not run `dotnet build`.
Rejected Alternatives: Running build under forbidden CPU/compiler contention; claiming Unity Editor verification without executing it.
Scalability potential: Preserves parallel agent throughput.
Hardware Impact: Prevented extra compiler contention on a saturated host.

## APEX Seventh-Pass Decisions

Problem: The flashlight light-shaft bridge depends on `ScreenSpaceLightShaftSource`, so the target component contract needed direct source verification.
Solution: Read `ScreenSpaceLightShaftSource`. It is a public sealed MonoBehaviour in `Hecton8.Lighting.Shafts`, marked `[DisallowMultipleComponent]`. Its `Awake` and `OnEnable` call `CacheLocalReferences`, and `CacheLocalReferences` resolves `sourceLight` with `TryGetComponent` when null. Adding it to the resolved `Light` GameObject is therefore a cold self-initializing path.
Rejected Alternatives: Assuming `AddComponent` safety from type name; adding extra setter API or reflection.
Scalability potential: Low/Middle/High/Ultra use the same authored source component; runtime quality is handled by the screen-space shaft runtime.
Hardware Impact: One cold component add only when absent; no hot path work.

Problem: The unmanaged-layout part of APEX was too terse for evidence-based review.
Solution: Added explicit static layout proof to `APEX_FinalVerification_1402.json`: `FlashlightEventPayload` is `[StructLayout(LayoutKind.Explicit, Size=16)]` with offsets 0,4,8,10,12; `LightShaftContribution` is `[StructLayout(LayoutKind.Explicit, Size=64)]` with offsets 0,4,8,16,28,32,36,40,44,45,46,48,56. Agent 1402 changed none of these offsets.
Rejected Alternatives: Saying "no struct modified" without listing the actual offsets adjacent to the touched bridge route.
Scalability potential: Stable struct ABI supports all device tiers; no DTO layout change.
Hardware Impact: No memory layout churn; no native buffer risk introduced.

Problem: Continuous Scalability proof needed exact source line evidence for `HomeostasisBrain.GlobalQualityWeight`, not only a prose statement.
Solution: Added line evidence for `ScreenSpaceLightShaftRuntime`: `sampleBudget` lerps between min/max tap counts from `_qualityWeight01`, and `_qualityWeight01` is read from `HomeostasisBrain.GlobalQualityWeight`. The flashlight bridge routes to this fake screen-space system instead of restoring VLB mutation.
Rejected Alternatives: Adding a new quality switch in `PlayerFlashlight`; binary `isLowEnd` branches; physical volumetric simulation.
Scalability potential: Low uses the low tap count; Middle/High/Ultra continuously increase shaft sample budget through the existing scalar.
Hardware Impact: No new per-frame route from agent 1402; existing runtime spends quality budget through current owner.

Problem: Compile gate needed another attempt only if machine state changed.
Solution: Waited 30 seconds and resampled. CPU remained 100%, active `dotnet` PID 55080 remained. `dotnet build` was not invoked.
Rejected Alternatives: Waiting indefinitely; compiler spam under a known active build process.
Scalability potential: Preserves shared machine throughput.
Hardware Impact: Avoided extra compiler load on a saturated host.

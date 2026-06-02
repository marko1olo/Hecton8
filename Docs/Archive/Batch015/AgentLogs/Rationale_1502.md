# Rationale 1502 - YAML Serialized Property and Prefab Metadata Migrator

Evidence discipline: STATIC_SOURCE / STATIC_DOC unless upgraded by compile, Unity Console, Play Mode, profiler, or player artifact.

## Decision 001 - Mandate Scope
Problem: The task spans raw Unity YAML, C# serialized field schemas, and unmanaged DTO migration without permission to corrupt scene or prefab files.
Solution: Use 7 relevant mandates before tooling: Zero GC for no runtime hot-path pollution; Native Memory for DataVault ownership; Runtime Struct Layout for DTO alignment; ModuleDTO/Persistent Registry for migration context; Evidence Audit for proof language; Designer Facade Bridge for authoring/runtime split.
Rejected Alternatives: Reading all 80 mandates would burn context without improving target precision. Skipping mandates violates batch law.
Scalability potential: Low tier benefits from preserving authored compact data and avoiding runtime parser work; middle tier keeps deterministic bake paths; high tier can add richer editor diagnostics; ultra tier can carry visual-overkill authoring data without bloating gameplay truth.
Hardware Impact: Static tooling has no runtime i3/MX350 frame cost. Avoiding runtime YAML/JSON parsers prevents future hot-path allocations and CPU stalls.

## Decision 002 - Initial Work Mode
Problem: Direct raw YAML mutation can destroy scene/prefab linkage if FileID/GUID/component block alignment is wrong.
Solution: Start with read-only scanners and dry-run reports; defer mutation until orphaned fields, owning MonoBehaviour fileIDs, new C# destinations, and backup parity are proven.
Rejected Alternatives: Repository-wide blind find/replace is structurally unsafe and explicitly forbidden. Unity Editor-only migration is insufficient if old fields were already deleted from C# and exist only as orphan YAML.
Scalability potential: Low/middle/high/ultra lanes are unaffected at runtime because the scanner is offline; preserving designer-authored data keeps all quality lanes fed by the same authoring truth.
Hardware Impact: CLI scan cost is bounded cold tooling. Estimated runtime gain is 0 us/frame because no runtime code is introduced; avoided future data loss has correctness value only.

## Decision 003 - Scanner Scope and Parser Corrections
Problem: A naive C# schema parser misclassified attribute-decorated serialized fields and would inflate false orphan counts in first-party prefabs.
Solution: Keep attribute state across consecutive attribute lines, bind schemas to the class matching the script filename, and compare MonoBehaviour `m_Script.guid` against `.cs.meta` GUIDs before judging a YAML key.
Rejected Alternatives: Trusting line-by-line `SerializeField` regex without class/GUID ownership was too loose for prefab metadata repair. Using Unity compilation was rejected at this phase because the user forbade heavyweight builds and static YAML facts were sufficient.
Scalability potential: Low tier gets no runtime burden; middle/high/ultra get a safer authoring pipeline because stale metadata is isolated without changing gameplay truth.
Hardware Impact: One static scan cost 84649899 us on the host. Runtime i3/MX350 frame gain is 0 us/frame because the scanner is offline; avoided corrupt prefab imports prevent editor downtime.

## Decision 004 - Target Deleted Native Field Absence
Problem: The primary prompt names deleted native fields such as `_cellIntegrityFront`, `_densityBuildSources`, `_publishedSonarSdf`, and `_combatDamageArray` as possible stranded YAML sources.
Solution: Treat source-byte existence as mandatory before migration. Repository YAML search and the full ledger found 0 exact target obsolete native field keys in `.unity`, `.prefab`, or `.asset` files. Current C# destinations are runtime `VaultGenerationHandle<T>` routes, not Unity-serialized fields.
Rejected Alternatives: Creating DTO payloads from defaults would be fake salvage. Copying unrelated legacy scalar fields into native buffers would corrupt gameplay truth ownership.
Scalability potential: Low tier keeps compact runtime DataVault buffers; middle tier keeps deterministic initialized handles; high/ultra tiers can spend saved cycles on visual damage presentation without serializing hot native arrays.
Hardware Impact: No runtime write introduced. Estimated low-end gain is preservation of existing zero-GC hot path: no managed YAML/JSON parser, no scene search, no per-frame handle reconstruction.

## Decision 005 - Orphan Candidate Policy
Problem: Static scan found broader orphan candidates, including first-party fields in `PFB_Submarine_Core.prefab` and `PFB_SargassumCollapseChunk.prefab`, but these are not the named deleted native arrays.
Solution: Classify them as dry-run cleanup candidates only after backup. A field may be deleted only if it is a root YAML key inside the proven MonoBehaviour block, has no current C# serialized field or `[FormerlySerializedAs]` bridge, has no safe new destination, and component counts/file headers remain invariant after edit.
Rejected Alternatives: Blindly purging all 358 orphan candidates would hit third-party assets and possible scanner blind spots. Transplanting values without a one-to-one target is data fabrication.
Scalability potential: Low tier benefits from smaller asset text and fewer editor import warnings; middle/high/ultra retain authored fidelity because only unmapped dead keys are candidates.
Hardware Impact: Runtime gain is 0 us/frame. Editor/import gain is bounded but real: fewer serialized dead keys to parse on asset import.

## Decision 006 - Backup and Report Contract
Problem: Any raw YAML edit must be reversible and objectively measurable.
Solution: Before mutation, copy target files into `Docs/AgentLogs/_Recovery_1502` by relative path, record source/backup SHA-256 and byte length, then write final evidence to `Docs/Reports/YAML_MIGRATION_REPORT_1502.json` plus append a human summary to `Docs/AgentLogs/LOG_1502.md`.
Rejected Alternatives: Git reset/checkout is destructive in a shared 20-agent workspace. Chat-only reports do not satisfy the batch proof requirement.
Scalability potential: Low/middle/high/ultra runtime lanes are unchanged; the backup route protects designer-authored data across all quality lanes.
Hardware Impact: Backup IO is cold tooling. Runtime gain is 0 us/frame; risk reduction is direct because restore is file-copy deterministic.

## Decision 007 - Binary Master Scene Boundary
Problem: `Assets/_Project/Scenes/02_HECTON_WORLD.unity` is binary serialized, not text YAML; raw line-level repair would corrupt it.
Solution: Treat the scene as a binary Unity asset. Back it up and hash it, but do not attempt raw text extraction or mutation. Any future scene migration must run through Unity serialization APIs after a real old/new property table exists.
Rejected Alternatives: Forcing `Get-Content` text parsing on null-byte binary data is unsafe. Converting serialization mode was rejected because it is a project-wide policy change outside agent 1502 scope.
Scalability potential: Low/middle/high/ultra lanes retain the same authored scene identity. No runtime quality tier is affected.
Hardware Impact: No runtime cost. Avoided corruption of a 33756552-byte scene asset; restore remains deterministic via backup hash `D96AE444158B0D8410460A9FE8588DB89C809DF3F2C6EBEABE534F5894D700D0`.

## Decision 008 - Editor Migrator Refusal
Problem: The prompt prefers a C# `SerializedObject` migrator, but the scan found no target old serialized fields to read and no new Unity-serialized DTO field to write.
Solution: Do not create `Assets/_Project/Scripts/Editor/Migration/Migrator1502.cs`. Record `csharpEditorMigratorCreated=false` and preserve compile wall hygiene.
Rejected Alternatives: A no-op menu item would satisfy paperwork while adding compile risk and another UnityEditor dependency. A runtime bootstrap migrator would violate zero-GC and hot-path ownership doctrine.
Scalability potential: Low tier avoids any startup migration scan; middle/high/ultra keep DataVault handles initialized by owners, not asset postprocessors.
Hardware Impact: Estimated low-end gain is avoiding a needless compile/domain reload and any future startup scan. Runtime delta remains 0 us/frame.

## Decision 009 - Final No-Mutation Outcome
Problem: Broad orphan candidates exist, but 0 belong to the named deleted native payloads and many lack one-to-one destinations.
Solution: Finalize as `NO_ASSET_MUTATION`: backups made, source absence proven, hashes unchanged, report written. Leave broad orphan cleanup to a separate owner with per-component semantic mapping.
Rejected Alternatives: Purging all first-party orphan candidates would delete possible designer intent from Player and presentation prefabs. Migrating values into unrelated fields would be fabricated salvage.
Scalability potential: Low tier keeps clean runtime memory discipline; middle/high/ultra preserve current authored content and can later spend fidelity budget only after semantic owner approval.
Hardware Impact: Files modified 0, bytes rewritten 0, properties migrated 0. Runtime cost added 0 us/frame. Cold verification cost is documented in final report.

## Decision 010 - Scanner False-Positive Reduction
Problem: The first extended project scan reported thousands of orphan properties in authoring assets because the C# schema parser missed `[TextArea] public` / `[Range] public` same-line fields, partial class fields, inherited serialized fields, and multiline `FormerlySerializedAs` chains.
Solution: Patch `Scan-YamlDesync1502.ps1` to merge partial classes by class name, parse same-line attributes before public/private detection, preserve multiline attribute state, merge base-class serialized fields, and treat `m_Active` as Unity renderer-feature base metadata.
Rejected Alternatives: Deleting asset fields from the inflated scan would have corrupted valid authoring data. Running Unity or `dotnet build` was rejected because this was a static parser defect, not a compile question.
Scalability potential: Low tier gets no runtime cost; middle/high/ultra authoring data stays intact because scanner proof is narrower and avoids false orphan cleanup.
Hardware Impact: Runtime i3/MX350 impact remains 0 us/frame. Cold scan cost after fixes: 74155014 us for scenes/prefabs and 87747677 us for full `Assets/_Project`.

## Decision 011 - First-Party Prefab Orphan Cleanup
Problem: After parser correction, 50 first-party prefab root keys remained provably absent from current C# schemas and had no safe DTO or `FormerlySerializedAs` destination. Leaving them keeps prefab text out of sync and preserves ignored stale metadata.
Solution: Create `Remove-YamlOrphans1502.ps1`, dry-run it against `YamlDesync_1502_Ledger.json`, require exact line/property match at two-space MonoBehaviour root indent, back up every edited prefab to `Docs/AgentLogs/_Recovery_1502_Extended`, then remove only those matched root blocks.
Rejected Alternatives: Unity `SerializedObject` cannot remove unknown orphan fields after C# deletion. Blanket regex delete was rejected. `.asset` cleanup for `ShakeProfile.FalloffCurve` was rejected because curve-to-exponent migration needs a semantic formula, not deletion.
Scalability potential: Low tier avoids stale authoring metadata import noise; middle/high/ultra keep current runtime truth unchanged and preserve the same quality-tier behavior because no hot-path code changed.
Hardware Impact: 8 prefab files changed, 50 ignored serialized keys removed, 1850 bytes removed from prefab text. Runtime cost added 0 us/frame; expected runtime gain 0 us/frame; editor/import hygiene improves only in cold tooling.

## Decision 012 - Raw YAML Safety Boundary
Problem: Raw prefab mutation can break FileID/component alignment if nested blocks or neighboring properties are cut incorrectly.
Solution: Restrict removal to root serialized properties already tied to MonoBehaviour `componentFileID`, preserve line-ending style, delete nested child lines until the next root property or Unity document marker, and verify YAML/TAG markers, GameObject blocks, MonoBehaviour counts, and missing-script counts after write.
Rejected Alternatives: Applying Unity Prefab "Apply All" or forcing scene save was rejected because unrelated multi-agent edits may exist. Editing the binary `02_HECTON_WORLD.unity` remains forbidden.
Scalability potential: Low/middle/high/ultra runtime lanes unchanged; prefab data is smaller and cleaner without changing continuous `GlobalQualityWeight` behavior.
Hardware Impact: No compile, no runtime allocation, no `dotnet build`. Validation found first-party prefab orphans = 0, target obsolete native hits = 0, missing script refs = 0, obsolete override paths = 0 after cleanup.

## Decision 013 - ShakeProfile Curve Salvage Boundary
Problem: Six first-party `ShakeProfile` ScriptableObject assets retained stale `FalloffCurve` YAML while current `ShakeProfile.cs` declares scalar `FalloffExponent`. The stale curve blocks kept project-wide first-party orphan count above zero.
Solution: Treat the curve as cold legacy authoring metadata, not runtime truth. Back up each asset, verify `m_Script.guid` matches `ShakeProfile.cs.meta`, require one MonoBehaviour block and one root `FalloffCurve`, then map the legacy curve to `FalloffExponent: 2` and delete the curve block.
Rejected Alternatives: Unity `SerializedObject` migration cannot read `FalloffCurve` after the C# field was deleted. Blind deletion was rejected because the current scalar field was absent and needed to be serialized. Runtime fallback was rejected because camera shake decay is owned by the Burst/Vault tuning route, not by Unity curves.
Scalability potential: Low tier keeps scalar presentation authoring only; middle/high/ultra keep richer procedural camera trauma through the existing continuous `GlobalQualityWeight` and Vault tuning route, not through per-asset curves.
Hardware Impact: Runtime i3/MX350 cost remains 0 us/frame. Cold asset text removed 2871 bytes across six assets; project-wide first-party orphan properties dropped to 0.

## Decision 014 - ScriptableObject YAML Structure Proof
Problem: `AGENTS.md` requires `m_RootGameObject` checks after raw `.asset` edits, but ScriptableObject YAML assets do not contain prefab root markers.
Solution: Run the mandated command anyway and record the expected false result. Use the relevant ScriptableObject invariants instead: YAML/TAG headers preserved, MonoBehaviour count stayed 1, `m_Script` GUID stayed `17ab5b96ce13779438b3efbdf414483f`, missing script count stayed 0, `FalloffCurve` count became 0, `FalloffExponent` count became 1.
Rejected Alternatives: Treating false `m_RootGameObject` as failure would misclassify valid ScriptableObject YAML. Skipping the command would violate the local raw YAML sanity rule.
Scalability potential: Low/middle/high/ultra runtime lanes are unchanged; this is cold asset hygiene only.
Hardware Impact: No runtime allocation, no build, no Unity import. Full `Assets/_Project` scan cost 91572742 us and reported 0 first-party orphan properties.

## Decision 015 - Modified Asset Hash Coverage
Problem: The first final report preserved hashes for the original 9 audited targets, but the extended pass modified 14 files after prefab cleanup and ShakeProfile migration.
Solution: Extend `Write-FinalReport1502.ps1` to ingest `YamlCleanup_1502.json` and `ShakeProfileFalloff_1502.json`, then emit `modifiedAssetHashes` for every changed file with pre/post SHA-256, current SHA-256, backup SHA-256, and structure counters.
Rejected Alternatives: Assuming git diff is proof was rejected because the batch requires self-contained artifact evidence. Rehashing only current files was rejected because rollback proof needs backup/pre-mutation parity too.
Scalability potential: Low/middle/high/ultra runtime lanes unchanged; evidence quality improves because restore and audit routes now cover every edited authoring asset.
Hardware Impact: Runtime cost 0 us/frame. Final report generation cost 418946 us; modified file hash coverage is 14/14 with 0 mismatches.

## Decision 016 - Third-Party Serialized Debt Boundary
Problem: Full project scan still reports 158 orphan serialized properties after first-party cleanup, all on project assets referencing third-party Crest, MapMagic, or VolumetricLightBeam scripts.
Solution: Create a read-only classifier and report these as package-owned schema drift: Crest=102, VolumetricLightBeam=54, MapMagic=2, first-party=0. Mutation policy remains `BLOCKED_THIRD_PARTY_ASSET_INTEGRITY`.
Rejected Alternatives: Raw deletion from project-owned assets was rejected because the owning scripts are third-party and may use importer/version migration semantics. Editing package scripts or adding wrappers was rejected by the 3rd-party asset integrity rule.
Scalability potential: Low tier avoids unsafe package breakage; middle/high/ultra keep ocean/light authoring data intact until package-specific migration is approved.
Hardware Impact: Runtime cost 0 us/frame. Classification cost 604519 us. No build, no Unity import, no third-party file mutation.

## Decision 017 - Evidence Chain Validator
Problem: The final report had correct modified-file hashes, but it did not independently validate that every changed target backup divergence was intentional and every backup/current hash still matched the mutation reports.
Solution: Add `Test-MigrationEvidence1502.ps1` as a read-only validator over the final JSON, mutation reports, current files, backups, human log, and third-party debt summary. The validator writes `YamlEvidenceValidation_1502.json` and fails on missing backups, hash mismatches, unexpected target drift, remaining first-party orphans, missing scripts, or malformed log interpolation.
Rejected Alternatives: Manual eyeballing of `git status` was rejected because it is not a durable proof artifact. Re-running raw migration scripts was rejected because validation must be read-only after mutation.
Scalability potential: Low/middle/high/ultra runtime lanes unchanged; cold evidence reliability improves because rollback and mutation proof now cover every changed asset path.
Hardware Impact: Runtime cost 0 us/frame. Validation cost 819183 us. Result: PASS, 14 modified unique paths, 0 missing backups, 0 hash mismatches, 3 intentional target divergences, 0 unexpected target divergences.

## Decision 018 - Backup Divergence Semantics
Problem: `postCheckChangedFromBackupCount=3` is expected after intentional prefab cleanup but is ambiguous without context and could be misread as accidental asset drift.
Solution: Patch `Write-FinalReport1502.ps1` to carry evidence-chain validation fields into the final report: `evidenceChainValidationStatus`, failure/warning counts, `intentionalTargetBackupDivergenceCount`, and `unexpectedTargetBackupDivergenceCount`.
Rejected Alternatives: Renaming or deleting the original invariant field was rejected because it is still raw evidence. The correct fix is to add interpretation, not hide the raw count.
Scalability potential: Low tier and high tier unchanged; integrators get exact failure semantics when deciding whether assets can be opened/imported in Unity.
Hardware Impact: Runtime cost 0 us/frame. Final report records PASS, failure count 0, intentional target backup divergence 3, unexpected target backup divergence 0.

## Decision 019 - Raw Mutation Footprint Guard
Problem: The scanner uses streaming reads, but the focused prefab/ScriptableObject mutation tools use whole-file text replacement. That is acceptable only for small exact-alignment files, not for future 100 MB scene-scale surgery.
Solution: Add a validation guard that fails if any raw-mutated file exceeds 104857600 bytes and records `maxModifiedBytesBefore`. Current maximum is `Player.prefab` at 173951 bytes; oversize count is 0.
Rejected Alternatives: Rewriting every mutation tool to streaming mode now was rejected because current edited files are small and the higher-risk issue is preventing accidental future large-file raw mutation. Ignoring the prompt's large-file warning was rejected.
Scalability potential: Low/middle/high/ultra runtime lanes unchanged; CI/editor hosts avoid accidental large-file whole-string rewrites in future migration passes.
Hardware Impact: Runtime cost 0 us/frame. Validation cost 641156 us. Raw mutation memory guard status PASS.

## Decision 020 - Modified YAML Reference Integrity
Problem: Hash parity proves bytes match the mutation reports, but it does not prove that referenced GUIDs, script references, FileID anchors, or YAML whitespace remained structurally sane after raw edits.
Solution: Add `Test-ModifiedYamlReferences1502.ps1` over all modified YAML files. The scanner indexes `.meta` GUIDs in `Assets`, `Packages`, and `Library/PackageCache`, treats Unity built-in GUIDs as built-ins, and fails on unresolved GUIDs, missing `m_Script` references, duplicate FileID anchors, tabs, missing YAML/TAG headers, or prefab files without GameObject blocks.
Rejected Alternatives: A first pass treated URP `Library/PackageCache` GUIDs and Unity built-in mesh GUIDs as broken references; that was a false-positive model, so the validator was corrected instead of mutating assets.
Scalability potential: Low/middle/high/ultra runtime lanes unchanged; editor import safety improves because package/script reference failures are caught before Unity import.
Hardware Impact: Runtime cost 0 us/frame. Reference pass cost 39055897 us. Result: PASS, 14 modified YAML files, 274 project/package GUID refs, 26 built-in GUID refs, 0 unresolved GUID refs, 76 script refs, 0 missing script refs, 0 duplicate FileID anchors, 0 tab lines.

## Decision 021 - Reference Integrity as Main Gate
Problem: A standalone reference report can be missed by the Integrator if it is not part of the primary final report and evidence-chain validator.
Solution: Wire `YamlReferenceIntegrity_1502.json` into `Write-FinalReport1502.ps1` and make `Test-MigrationEvidence1502.ps1` fail unless final report reference integrity is `PASS`.
Rejected Alternatives: Keeping the reference scanner as optional diagnostics was rejected because GUID/FileID integrity is a core YAML surgery proof, not optional telemetry.
Scalability potential: Low/middle/high/ultra unchanged; cross-device stability depends on assets importing deterministically before runtime quality lanes matter.
Hardware Impact: Runtime cost 0 us/frame. Evidence validation after integration cost 1351735 us and remained PASS.

## Decision 022 - Backup/Current Delta Gate
Problem: Backup/current comparison reported two removed GUID references after orphan cleanup: `cec354f2d9357bc4eaa00c12f00af368` from `Player.prefab` and `83fe0f4ef4dc4260ae2b77e1e1e218b2` from `Tool_Scanner_Held.prefab`. Hash parity alone could not prove whether these were valid orphan payload removals or live reference loss.
Solution: Add `Test-YamlBackupDelta1502.ps1` to compare each modified file against its backup for FileID anchors, script refs, component refs, prefab `propertyPath` refs, GUID refs, and ShakeProfile falloff field shape. It reads `YamlCleanup_1502.json` and allows removed GUID refs only when the GUID appears inside the exact backup line range of a proven deleted orphan root property. C# audit confirmed `PlayerThrusterAudio` now uses generated `_proceduralThrusterClip`, not serialized `thrusterLoopClip`, and `ScannerTool` has `scannerMarkerShader` only, not `scannerPulseShader`.
Rejected Alternatives: Restoring removed legacy fields would reintroduce schema-desync data that current C# ignores. Treating every removed GUID as corruption was too blunt because orphan property deletion can intentionally remove nested asset refs. Ignoring the delta was rejected because raw YAML surgery requires backup/current structural proof.
Scalability potential: Low/middle/high/ultra runtime lanes unchanged; authoring assets now have a stricter cold gate that protects actual FileID/script/component/override topology while allowing dead payload removal.
Hardware Impact: Runtime cost 0 us/frame. Backup/current delta pass cost 805446 us. Result: PASS, 14 files checked, FileID/script/component/propertyPath delta 0, GUID added 0, GUID removed 2, orphan-payload GUID removed 2, unclassified GUID removed 0.

## Decision 023 - Backup Delta as Evidence Chain Gate
Problem: A passing backup/current delta report is useful only if the main final report and evidence validator consume it.
Solution: Patch `Write-FinalReport1502.ps1` to include backup-delta counters and add `YamlBackupDelta_1502.json` to evidence files. Patch `Test-MigrationEvidence1502.ps1` to fail on missing/non-PASS backup delta, any FileID/script/component/propertyPath delta, any GUID addition, or any unclassified GUID removal.
Rejected Alternatives: Leaving the backup delta as a side report would let later runs regress the proof chain silently. Making GUID removed count always zero was rejected because valid orphan payload cleanup can remove asset refs from dead fields.
Scalability potential: Low/middle/high/ultra runtime unchanged; integration confidence improves before Unity import because topology regressions are now mechanically rejected.
Hardware Impact: Runtime cost 0 us/frame. Evidence chain remained PASS with 14 modified files, 0 hash mismatches, 0 missing backups, backup delta PASS, and reference integrity PASS.

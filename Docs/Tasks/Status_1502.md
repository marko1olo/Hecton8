# Status 1502 - YAML Serialized Property and Prefab Metadata Migrator

Batch prompt: `Docs/Tasks/CURRENT_BATCH.md` / `AGENT_PROMPT id="1502"`
Domain: Echelon 9 Data Integrity / Serialized Asset Migration
Evidence class baseline: STATIC_SOURCE / STATIC_DOC until Unity or compile proof exists.

## Hygiene
- [x] Prompt extracted by CLI regex from `CURRENT_BATCH.md`.
  - DOD: cover-to-cover XML block extraction using `Get-Content -Raw` and `<AGENT_PROMPT id="1502">` regex.
  - Rejected: manual recall or neighboring prompt context.
  - Estimate: 1800 us.
- [x] Status/Rationale clean-start check.
  - DOD: both files were missing before creation; no old batch state observed.
  - Rejected: reading archive logs from previous batches.
  - Estimate: 600 us.
- [x] Mandates selected and read.
  - DOD: read registry plus YAML/data relevant mandates: zero-GC, native memory, runtime DTO layout, module DTO, persistent registry, evidence audit, designer bridge.
  - Rejected: bulk-loading all mandates.
  - Estimate: 2200 us.

## Phase 0 - Systems Archaeology
- [x] Task 01: EXHAUSTIVE_YAML_DESYNC_INQUISITION.
  - DOD: wrote and executed `Tools/Migration1502/Scan-YamlDesync1502.ps1`; scanned `Assets/_Project/Scenes` and `Assets/_Project/Prefabs`; parsed 4315 script schemas, 853 Unity asset files, and 3252 MonoBehaviour records.
  - Proof: `Docs/AgentLogs/YamlDesync_1502_Ledger.json` reports 0 target obsolete native field hits, 0 missing script references, and 0 obsolete prefab override paths. It reports 358 broader orphan candidates for later triage.
  - Rejected: direct raw YAML mutation before proving GUID/FileID ownership and deleted-field presence.
  - Estimate: 40852961 us.
- [x] Task 02: CSHARP_TO_YAML_SCHEMA_MAPPING.
  - DOD: mapped the prompt target names to current C# owners. Structural grid runtime arrays now resolve through `VaultGenerationHandle<T>` fields in `SubmarineStructuralGrid`; sargassum density sources resolve through `VaultGenerationHandle<DensitySourceData>` in `SargassumGlobalDragManager`.
  - Proof: repository YAML search for `_cellIntegrityFront:`, `_densityBuildSources:`, `_publishedSonarSdf:`, and `_combatDamageArray:` returned 0 hits; C# search found only handle/runtime references, not serialized YAML source fields.
  - Rejected: fabricating a YAML-to-DTO transplant when the serialized source bytes do not exist on disk.
  - Estimate: 6200 us.
- [x] Task 03: MIGRATION_TOOL_ARCHITECTURE_PLANNING.
  - DOD: selected a two-lane migrator architecture: `SerializedObject` lane only when old and new fields both exist; raw YAML lane only for deleted orphan properties proven by component FileID/GUID.
  - Proof: current target deleted native fields require no transplant because their YAML source count is 0. First-party orphan cleanup candidates remain dry-run only until backup parity and component-count invariants are recorded.
  - Rejected: repository-wide regex replace and editor script generation with empty migration tables.
  - Estimate: 4100 us.
- [x] Task 04: BACKUP_AND_RESTORE_PROTOCOL_DESIGN.
  - DOD: backup route will copy target `.unity`/`.prefab` files into `Docs/AgentLogs/_Recovery_1502` before mutation; restore route will copy backed-up files over originals by relative path and verify SHA-256/byte length.
  - Proof commands: `Copy-Item -LiteralPath <source> -Destination <backup> -Force`; `Copy-Item -LiteralPath <backup> -Destination <source> -Force`; `Get-FileHash -Algorithm SHA256`.
  - Rejected: relying on git checkout or Unity auto-reimport as rollback.
  - Estimate: 2100 us.
- [x] Task 05: TELEMETRY_AND_REPORTING_PLANNING.
  - DOD: final report path fixed as `Docs/Reports/YAML_MIGRATION_REPORT_1502.json` with files scanned, files modified, properties migrated/deleted, bytes rewritten, backup hashes, post-write hashes, and scan/mutation microseconds.
  - Proof: scan ledger already uses the planned fields for static proof; final report remains pending because no asset mutation has occurred.
  - Rejected: chat-only success claims and binary dump artifacts.
  - Estimate: 1800 us.

## Phase 1 - Migration and Repair
- [x] Task 06: SURGICAL_BACKUP_EXECUTION.
  - DOD: wrote `Tools/Migration1502/Backup-YamlTargets1502.ps1` and `Restore-YamlTargets1502.ps1`; backed up 9 target scene/prefab files into `Docs/AgentLogs/_Recovery_1502`.
  - Proof: `Docs/AgentLogs/Backup_1502_Manifest.json` records 34084867 bytes and SHA-256 parity `MATCH` for every backup.
  - Rejected: git checkout/reset rollback in a shared multi-agent workspace.
  - Estimate: 392190 us.
- [x] Task 07: RAW_YAML_EXTRACTION_PASS.
  - DOD: wrote and executed `Tools/Migration1502/Extract-YamlOrphans1502.ps1`.
  - Proof: `Docs/AgentLogs/YamlExtraction_1502.json` reports 0 target deleted field payloads extracted, 104 first-party dry-run cleanup candidates, and `02_HECTON_WORLD.unity` as `BINARY_UNITY_ASSET`.
  - Rejected: raw text extraction from the binary master scene.
  - Estimate: 274097 us.
- [x] Task 08: YAML_DATA_TRANSPLANTATION_PASS.
  - DOD: no transplantation executed because source payload count was 0 and no one-to-one DTO destination existed for broad cleanup candidates.
  - Proof: final report records `propertiesMigrated=0`, `bytesRewritten=0`, `filesModified=0`.
  - Rejected: synthesizing DTO payloads from runtime defaults.
  - Estimate: 700 us.
- [x] Task 09: CSHARP_SERIALIZED_OBJECT_MIGRATOR_CONSTRUCTION.
  - DOD: C# editor migrator intentionally not created; the `SerializedObject` lane requires an oldProperty/newProperty table, and the scan found no old serialized target properties.
  - Proof: `Docs/Reports/YAML_MIGRATION_REPORT_1502.json` records `csharpEditorMigratorCreated=false` with reason.
  - Rejected: adding a no-op UnityEditor script that would create compile churn without data rescue.
  - Estimate: 900 us.
- [x] Task 10: AUTOMATED_FILE_ID_ALIGNMENT_CHECK.
  - DOD: wrote and executed `Tools/Migration1502/Test-YamlMigration1502.ps1`.
  - Proof: `Docs/AgentLogs/YamlInvariant_1502.json` records 9 audited targets, 0 changed from backup, 0 missing scripts, 0 target obsolete keys, and 0 target obsolete override paths.
  - Rejected: relying on visual prefab inspection only.
  - Estimate: 485358 us.
- [x] Task 11: PREFAB_NESTING_AND_OVERRIDES_GUARD.
  - DOD: scanned target prefabs for obsolete `propertyPath` override references.
  - Proof: invariant report and scan ledger both record 0 target obsolete override paths.
  - Rejected: editing prefab instances without override path proof.
  - Estimate: 1200 us.
- [x] Task 12: MISSING_REFERENCE_ANNIHILATION.
  - DOD: scanned targets and scene/prefab roots for `m_Script: {fileID: 0}`.
  - Proof: scan ledger and invariant report both record 0 missing script references.
  - Rejected: opening Unity solely to confirm a zero static count.
  - Estimate: 1300 us.
- [x] Task 13: COMPILE_WALL_AND_NAMESPACE_HYGIENE.
  - DOD: no C# runtime or Editor code was added; compile wall unchanged.
  - Proof: final report records `dotnetBuildExecuted=false` and `csharpEditorMigratorCreated=false`.
  - Rejected: `dotnet build`, because no C# change required verification and user prohibited CPU-heavy builds.
  - Estimate: 600 us.
- [x] Task 14: DRY_RUN_VERIFICATION_EXECUTION.
  - DOD: dry-run extraction plus invariant verification executed against backed-up targets.
  - Proof: `changedFromBackupCount=0`, `targetDeletedFieldHits=0`, `targetObsoleteKeyCount=0`.
  - Rejected: dry-run mutation simulator on binary scene.
  - Estimate: 759455 us.
- [x] Task 15: BATCHED_COMPILATION_AND_EXECUTION_CHECK.
  - DOD: compile/execution check resolved as not applicable because no C# migrator/runtime code was generated.
  - Proof: no `dotnet build` was launched; final report states why.
  - Rejected: build abuse under explicit user prohibition and no C# delta.
  - Estimate: 400 us.

## Phase 2 - Stress Testing and Forensic Proof
- [x] Task 16: YAML_CORRUPTION_FUZZER_TEST.
  - DOD: wrote and executed `Tools/Migration1502/Test-YamlScannerFuzz1502.ps1`.
  - Proof: `Docs/AgentLogs/YamlFuzz_1502.json` records 6/6 regex detector cases passed, 0 failures.
  - Rejected: temp asset creation in `Assets`.
  - Estimate: 83077 us.
- [x] Task 17: MIGRATION_DATA_LOSS_ASSERTION.
  - DOD: asserted all 9 target files remained byte/hash identical to backups after all scans and dry-runs.
  - Proof: invariant report records `changedFromBackupCount=0`.
  - Rejected: claiming data safety without SHA-256 parity.
  - Estimate: 1100 us.
- [x] Task 18: YAML_SYNTAX_STRICTNESS_AUDIT.
  - DOD: audited 8 text prefabs for YAML/TAG headers and prefab GameObject blocks; classified the master scene as binary and excluded it from raw YAML surgery.
  - Proof: invariant report records `textYamlCount=8`, `binaryUnityAssetCount=1`, all text targets with Unity YAML headers.
  - Rejected: treating a binary `.unity` file as editable text.
  - Estimate: 900 us.
- [x] Task 19: ZERO_COMPILATION_HOT_PATH_VERIFICATION.
  - DOD: no runtime code, no Editor C# code, no managed hot-path parser, no new Unity API dependency.
  - Proof: final report records files modified 0, C# migrator false, dotnet build false.
  - Rejected: runtime migration bridge or startup scene scan.
  - Estimate: 600 us.
- [x] Task 20: AUTOMATED_METRIC_VALIDATOR_REPORT.
  - DOD: wrote and executed `Tools/Migration1502/Write-FinalReport1502.ps1`.
  - Proof: final proof artifact at `Docs/Reports/YAML_MIGRATION_REPORT_1502.json`; human log appended to `Docs/AgentLogs/LOG_1502.md`.
  - Rejected: chat-only completion report.
  - Estimate: 1400 us.

## Strict Iterative Loops
- [x] Loop 1: initial scanner failed on old PowerShell path API; replaced with portable relative path logic.
- [x] Loop 2: generic list JSON output mismatch; converted collections to arrays before report emission.
- [x] Loop 3: schema parser false positives on filename class and attribute handling; patched class binding and multiline attributes.
- [x] Loop 4: master scene binary detection; prohibited raw text mutation for `02_HECTON_WORLD.unity`.
- [x] Loop 5: invariant/fuzzer pass; proved unchanged backup hashes and exact obsolete-key detector behavior.

## Current Gate
Extended STATIC_SOURCE pass complete. Target deleted native field source bytes still do not exist on disk, so DTO transplantation remains `0`. Proven first-party prefab orphan metadata cleanup applied to 8 text prefabs with extended backups. `ShakeProfile` stale `FalloffCurve` ScriptableObject assets were mapped to scalar `FalloffExponent` and cleaned. Project-wide first-party orphan count is now `0`.

## Extended Pass - 2026-05-31
- [x] Parser hardening: partial classes, inherited serialized fields, same-line attributes, multiline `FormerlySerializedAs`, `m_Active` renderer-feature base metadata.
  - DOD: reran scenes/prefabs scan and full `Assets/_Project` scan after patch; biome matrix false positives dropped to 0 in scoped check, first-party prefab noise from `PlayerTool` base fields removed.
  - Rejected: deleting fields from the inflated scanner output; `dotnet build` because no C# runtime/editor code was changed.
  - Estimate: 149041480 us for prefabs/scene validation pass before cleanup; 87747677 us for final project scan.
- [x] First-party prefab orphan cleanup dry-run.
  - DOD: `Remove-YamlOrphans1502.ps1` dry-run matched 50 root orphan keys in 8 `.prefab` files, rejected 0, validated YAML/TAG/GameObject/MonoBehaviour/missing-script invariants in memory.
  - Rejected: `.asset` deletion of `ShakeProfile.FalloffCurve`, because curve-to-exponent migration requires semantic conversion.
  - Estimate: 1010156 us.
- [x] First-party prefab orphan cleanup apply.
  - DOD: backed up all 8 edited prefabs to `Docs/AgentLogs/_Recovery_1502_Extended`; deleted 50 orphan root keys; removed 1850 bytes; no missing scripts introduced.
  - Rejected: Unity Apply All, scene autosave, binary scene edits, and raw deletion outside exact ledger/FileID/property alignment.
  - Estimate: 1864778 us.
- [x] Post-cleanup verification.
  - DOD: reran `Scan-YamlDesync1502.ps1` on scenes/prefabs and full `Assets/_Project`; scenes/prefabs now report 0 first-party prefab orphans, 0 target obsolete native hits, 0 missing scripts, 0 obsolete prefab override paths. Fuzzer remains 6/6.
  - Rejected: `dotnet build`; the mutation is prefab YAML only and user forbade build abuse.
  - Estimate: 74155014 us for scenes/prefabs scan, 87747677 us project scan, 28444 us fuzzer.
- [x] Final evidence update.
  - DOD: reran `Test-YamlMigration1502.ps1` and `Write-FinalReport1502.ps1`; final report now records `PREFAB_ORPHAN_METADATA_CLEANUP_APPLIED`, filesModified=8, propertiesDeleted=50, bytesRewritten=1850, dotnetBuildExecuted=false.
  - Rejected: leaving the old `NO_ASSET_MUTATION` report after actual prefab cleanup.
  - Estimate: 3017842 us invariant, 2000000 us final report.

## ShakeProfile Asset Pass - 2026-05-31
- [x] Re-extracted active `AGENT_PROMPT id="1502"` from `CURRENT_BATCH.md`.
  - DOD: corrected regex fallback for prompted tag attributes; prompt found at line 2637.
  - Rejected: relying only on compressed chat state after extraction failure.
  - Estimate: 2400 us.
- [x] Audited `ShakeProfile` C# and asset contract.
  - DOD: confirmed `Assets/_Project/Scripts/VFX/ShakeProfile.cs.meta` guid `17ab5b96ce13779438b3efbdf414483f` matches all six assets; each asset has one MonoBehaviour, zero missing scripts, one stale `FalloffCurve`, and no serialized `FalloffExponent`.
  - Rejected: raw deletion before proving the C# field and script GUID.
  - Estimate: 91286459 us for VFX-root scan before/after proof.
- [x] Migrated stale curve blocks to scalar falloff.
  - DOD: created `Tools/Migration1502/Migrate-ShakeProfileFalloff1502.ps1`; dry-run matched 6 blocks with 0 rejects; apply backed up all six assets to `Docs/AgentLogs/_Recovery_1502_ShakeProfile`, inserted `FalloffExponent: 2`, deleted six stale curve blocks, removed 2871 bytes, and normalized empty `m_EditorClassIdentifier` metadata.
  - Rejected: C# Editor migrator because the old `FalloffCurve` field no longer exists for `SerializedObject` access; runtime migration because camera shake is presentation-only and already uses SignalBus/Vault scalar route.
  - Estimate: 1150913 us.
- [x] Verified asset and project-wide serialization state.
  - DOD: required `Get-Content | Select-String "m_RootGameObject" -Quiet` command run for all six `.asset` files; result false as expected for ScriptableObject assets. VFX scan reports 0 orphans. Full `Assets/_Project` scan reports 3242 YAML files, 4597 MonoBehaviours, 0 first-party orphan properties, 0 target obsolete native hits, 0 missing scripts, 0 obsolete override paths.
  - Rejected: `dotnet build`; no C# runtime/editor code changed and another `dotnet` process was already running.
  - Estimate: 91572742 us project scan.
- [x] Updated final proof artifacts.
  - DOD: `Docs/Reports/YAML_MIGRATION_REPORT_1502.json` now records `PREFAB_AND_SHAKEPROFILE_ASSET_ORPHAN_METADATA_CLEANUP_APPLIED`, filesModified=14, propertiesMigrated=6, propertiesDeleted=56, bytesRewritten=4721, projectWideFirstPartyOrphanProperties=0.
  - Rejected: leaving report at prefab-only metrics.
  - Estimate: 39621 us final report.

## Evidence Hardening - 2026-05-31
- [x] Modified-file hash coverage hardened.
  - DOD: patched `Write-FinalReport1502.ps1` to load both prefab cleanup and ShakeProfile migration reports; final JSON now records all 14 modified files in `modifiedAssetHashes`, verifies current SHA-256 equals reported post-mutation SHA, and verifies backup SHA-256 equals pre-mutation SHA.
  - Proof: `modifiedFileHashCoverageCount=14`, `modifiedFileHashMismatchCount=0`.
  - Rejected: relying on the original 9 target `assetHashes`, because they do not cover all extended-pass mutations.
  - Estimate: 418946 us final report generation.
- [x] Remaining orphan metadata classified by ownership.
  - DOD: created and executed `Tools/Migration1502/Classify-ThirdPartySerializedDebt1502.ps1`; report written to `Docs/AgentLogs/YamlThirdPartySerializedDebt_1502.json`.
  - Proof: total remaining orphan properties=158, first-party=0, known third-party=158; split is Crest=102, VolumetricLightBeam=54, MapMagic=2; mutation policy is read-only / blocked by third-party asset integrity.
  - Rejected: raw deletion of Crest/MapMagic/VolumetricLightBeam serialized metadata from project assets under agent 1502, because package-specific upgrade/import policy owns that risk.
  - Estimate: 604519 us classification pass.

## Evidence Chain Pass - 2026-05-31
- [x] Migration evidence chain validator added.
  - DOD: created and executed `Tools/Migration1502/Test-MigrationEvidence1502.ps1 -FailOnError`; validation report written to `Docs/AgentLogs/YamlEvidenceValidation_1502.json`.
  - Proof: status=`PASS`, modified files reported=14, unique modified paths=14, missing modified files=0, missing modified backups=0, actual modified hash mismatches=0, bytes removed from modified records=4721.
  - Rejected: treating `postCheckChangedFromBackupCount=3` as either failure or success without classifying whether each changed target was an intentional mutation.
  - Estimate: 819183 us validation pass.
- [x] Intentional vs unexpected backup divergence separated in final report.
  - DOD: patched `Write-FinalReport1502.ps1` to include `evidenceChainValidationStatus`, failure/warning counts, `intentionalTargetBackupDivergenceCount`, and `unexpectedTargetBackupDivergenceCount`.
  - Proof: final report now records validation status `PASS`, failure count 0, intentional target backup divergence=3, unexpected target backup divergence=0.
  - Rejected: leaving backup parity as an ambiguous raw count after approved prefab metadata cleanup.
  - Estimate: 400000 us final report update.
- [x] Raw mutation memory-footprint guard added.
  - DOD: patched `Test-MigrationEvidence1502.ps1` to fail if any raw-mutated file exceeds 100 MB, because the current mutation scripts intentionally use whole-file text replacement only for small, exact-alignment assets.
  - Proof: latest validation records `rawMutationMemoryGuardStatus=PASS`, `maxModifiedBytesBefore=173951`, `rawMutationOversizeCount=0`.
  - Rejected: claiming Task 19 streaming compliance from prose alone; scanner uses `StreamReader`, while raw mutation is bounded by explicit file-size guard.
  - Estimate: 641156 us validation pass.
- [x] Modified YAML reference integrity scanner added.
  - DOD: created and executed `Tools/Migration1502/Test-ModifiedYamlReferences1502.ps1 -FailOnError`; report written to `Docs/AgentLogs/YamlReferenceIntegrity_1502.json`.
  - Proof: status=`PASS`, modified YAML files checked=14, meta GUIDs indexed=29401 including `Library/PackageCache`, project/package GUID refs=274, Unity built-in GUID refs=26, unresolved GUID refs=0, script refs=76, missing script refs=0, duplicate FileID anchors=0, tab lines=0.
  - Rejected: treating URP PackageCache GUIDs and Unity built-in mesh GUIDs as broken references; the validator now resolves package cache and accounts for built-in GUIDs explicitly.
  - Estimate: 39055897 us reference integrity pass.
- [x] Reference integrity wired into final evidence chain.
  - DOD: patched `Write-FinalReport1502.ps1` and `Test-MigrationEvidence1502.ps1`; final report now carries reference integrity status/counters and evidence validation fails if reference integrity is not `PASS`.
  - Proof: final report records `referenceIntegrityStatus=PASS`, failure count=0, unresolved GUID refs=0, missing script refs=0, duplicate FileID anchors=0, tab lines=0; evidence chain remains `PASS`.
  - Rejected: leaving `YamlReferenceIntegrity_1502.json` as an unattached side report.
  - Estimate: 1351735 us evidence validation pass after integration.
- [x] Backup/current YAML structural delta validator added.
  - DOD: created and executed `Tools/Migration1502/Test-YamlBackupDelta1502.ps1 -FailOnError`; validator compares modified current files against their backups for FileID anchors, script refs, component refs, prefab `propertyPath` refs, GUID refs, and expected `ShakeProfile` FalloffCurve -> FalloffExponent field shape.
  - Proof: `Docs/AgentLogs/YamlBackupDelta_1502.json` records status=`PASS`, filesChecked=14, FileID/script/component/propertyPath delta=0/0, GUID added=0, GUID removed=2, orphan-payload GUID removed=2, unclassified GUID removed=0.
  - Rejected: treating all GUID removals as corruption; C# audit proved `PlayerThrusterAudio` now generates `_proceduralThrusterClip` and `ScannerTool` uses `scannerMarkerShader`, so removed `thrusterLoopClip` and `scannerPulseShader` GUIDs were inside deleted orphan fields with no current serialized destination.
  - Estimate: 805446 us backup/current delta pass.
- [x] Backup delta wired into final evidence chain.
  - DOD: patched `Write-FinalReport1502.ps1` and `Test-MigrationEvidence1502.ps1`; final report now carries backup delta status/counters and evidence validation fails if structural deltas or unclassified GUID removals appear.
  - Proof: final report records `backupDeltaStatus=PASS`, `backupDeltaFailureCount=0`, `backupDeltaUnclassifiedGuidReferenceRemovedCount=0`; evidence chain remains `PASS`.
  - Rejected: leaving backup/current comparison as an unattached diagnostic report.
  - Estimate: 1100000 us evidence validation/report refresh.

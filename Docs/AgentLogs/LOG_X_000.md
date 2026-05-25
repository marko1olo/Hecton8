# LOG_X_000

## 2026-05-23 - Scoped Vault Exorcism: AudioLogSystem

What was wrong:
- `AudioLogSystem` owned one persistent `NativeQueue<uint>` and two persistent `NativeArray<uint>` fields inside a MonoBehaviour.
- These aliases blocked DataVault relocation/defragmentation safety because they preserved direct native collection views across dispatcher phases.
- Initial full Roslyn audit found 2270 forbidden persistent native alias candidates across `Assets/_Project/Scripts`.

What was done:
- Added `Tools/VaultNativeAliasRoslynAudit` and generated a machine-readable Roslyn AST ledger.
- Added AudioLog vault BufferIDs:
  - `AudioLogPlaybackQueue = 70672`
  - `AudioLogEncryptedFragmentHashes = 70673`
  - `AudioLogEncryptedFragmentRecoveredBits = 70674`
  - `AudioLogTelemetryRing = 70675`
  - `AudioLogTelemetryCursor = 70676`
- Replaced `AudioLogSystem` persistent native aliases with `VaultGenerationHandle<T>` descriptors.
- Rewrote playback queue enqueue/dequeue/clear paths to resolve DataVault views only inside method scope.
- Rewrote encrypted fragment save/load/read/write paths to use transient DataVault read-only views and bounded writer locks.
- Added `AudioLogVaultTelemetryEntry`, explicit layout, 64 bytes, 300-row ring.
- Corrected telemetry writes to acquire/release DataVault writer fences for ring and cursor.
- Wrote proof artifacts:
  - `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_X_000.json`
  - `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_X_000_AudioLog_after.json`
  - `Docs/Reports/VAULT_EXORCISM_REPORT_X_000.json`

Cinematic cheats used:
- Replaced queue object behavior with a fixed 16-slot ring buffer. No simulation fidelity lost; narrative queue ordering is deterministic and bounded.
- Encrypted fragment state stayed as two flat `uint` lanes, not object state. This preserves cheap save serialization and cache-linear reads.
- Telemetry records failure counters and generations only. No managed stack traces, no string formatting, no hot-path exception payload.

Exact microseconds saved:
- Runtime GC saved: unmeasured in profiler, structurally 0 managed allocations added on migrated hot paths.
- Persistent native alias count saved in `AudioLogSystem`: 3 fields removed.
- Full-project forbidden candidate delta: 2270 -> 2267, net -3.
- Expected normal-frame telemetry overhead: 0 us because telemetry recording is only on fallback/error paths.
- Error-path telemetry cost: two bounded DataVault writer-fence attempts plus one 64-byte row write; no profiler microsecond sample available in this shell session.

Verification:
- `dotnet build Hecton8.Editor.csproj --no-restore`: succeeded, 0 warnings, 0 errors, 00:01:26.34.
- Full Roslyn audit after migration: 2373 files, 0 parse failures, 2267 forbidden persistent candidates, hash `a0e80f2152a4712f729c3d6e867c21a0b199b26bff7764e2d31b4fb808ef04a7`.
- Scoped AudioLog audit after migration: 5 files, 0 parse failures, 2 remaining static queue findings in `AudioLogEvents.cs`, 0 MonoBehaviour candidates, hash `c4af968a29c0bf6f24172fbd986896e5234a00f1ebc4e2007d01c5d80bde7474`.

Residual violations:
- Project-wide purge is not complete. Full audit still reports 2267 forbidden persistent native alias candidates.
- `AudioLogEvents.cs` still owns `_pendingEvents` and `_nextFrameEvents` static `NativeQueue<AudioLogEventPayload>` lanes. They require a SignalBus route decision before removal.

## 2026-05-23 - Scoped Vault Exorcism: AwaitableDropSequenceDirector

What was wrong:
- `AwaitableDropSequenceDirector` owned `NativeArray<PrologueSequenceTelemetryEntry> _blackBox` as a persistent MonoBehaviour field.
- That ring is exactly the class of stale alias that breaks DataVault relocation guarantees.

What was done:
- Added `SystemID.PrologueSequence = 350`.
- Added `BufferID.PrologueSequenceTelemetryRing = 74009`.
- Replaced `_blackBox` with `VaultGenerationHandle<PrologueSequenceTelemetryEntry> _blackBoxHandle`.
- Rewrote `RecordStage` to acquire a DataVault writer fence, validate capacity, write one fixed telemetry row, and release in `finally`.
- Rewrote `DumpBlackBox` to resolve a read-only DataVault view only inside dump scope.
- Updated `Docs/Reports/VAULT_EXORCISM_REPORT_X_000.json` with the second scoped migration.

Cinematic cheats used:
- No physical simulation added. The prologue director remains contract-only.
- Telemetry stays a 300-row fixed ring. No managed event history, no stack strings, no per-frame allocation.

Exact microseconds saved:
- Runtime GC saved: structurally 0 managed allocations added in record/dump paths.
- Persistent native alias count saved in `AwaitableDropSequenceDirector`: 1 field removed.
- Full-project forbidden candidate delta after both migrations: 2270 -> 2266, net -4.
- Expected record-path overhead: one DataVault writer-fence attempt plus one 32-byte row write. No profiler microsecond sample available in shell.

Verification:
- `dotnet build Hecton8.Editor.csproj --no-restore`: succeeded, 0 warnings, 0 errors, 00:01:02.16.
- Scoped prologue Roslyn audit: 1 file, 0 parse failures, 0 forbidden persistent candidates, hash `254906112e60fba00917c34dafe995f2cc66cd70ff89c10a0df3faa68edf7087`.
- Full Roslyn audit after second migration: 2373 files, 0 parse failures, 2266 forbidden persistent candidates, hash `47f5a2eada2de257cd80f01cfcfb17e1f08c691fac42102c127f6cc73cde5a44`.

Residual violations:
- Project-wide purge is still not complete. The next work unit should target another isolated one-field MonoBehaviour or route static event queues through SignalBus where ownership is clear.

## 2026-05-23 - Scoped Vault Exorcism: QAEnduranceWatchdogBot

What was wrong:
- `QAEnduranceWatchdogBot` owned `NativeArray<QAEnduranceBlackBoxEntry> _blackBox` as a persistent MonoBehaviour field.
- QA runtime state can still produce stale native aliases during relocation testing; leaving it dirty invalidates the memory-sovereignty audit.

What was done:
- Added `SystemID.QAEndurance = 351`.
- Added `BufferID.QAEnduranceBlackBoxRing = 74200`.
- Replaced `_blackBox` with `VaultGenerationHandle<QAEnduranceBlackBoxEntry> _blackBoxHandle`.
- Rewrote `WriteBlackBox` to acquire a DataVault writer fence, validate capacity, write one fixed telemetry row, and release in `finally`.
- Rewrote `DumpBlackBox` to resolve a read-only DataVault view only inside dump scope.
- Updated `Docs/Reports/VAULT_EXORCISM_REPORT_X_000.json` with the third scoped migration.

Cinematic cheats used:
- None. This is a QA harness. The correct optimization is bounded forensic capture, not richer simulation.
- The CSV writer remains managed/cold and separate; the hot native alias target was the 300-row black-box ring.

Exact microseconds saved:
- Runtime GC saved: structurally 0 managed allocations added in black-box record/dump paths.
- Persistent native alias count saved in `QAEnduranceWatchdogBot`: 1 field removed.
- Full-project forbidden candidate delta after three migrations: 2270 -> 2265, net -5.
- Active QA frame cost: one DataVault writer-fence attempt plus one 128-byte row write. No profiler microsecond sample available in shell.

Verification:
- `dotnet build Hecton8.Editor.csproj --no-restore`: succeeded, 0 warnings, 0 errors, 00:00:11.79.
- Scoped QA audit: 10 files, 0 parse failures; `QAEnduranceWatchdogBot.cs` has no findings. QA root still has 29 forbidden candidates in other QA/headless files.
- Full Roslyn audit after third migration: 2373 files, 0 parse failures, 2265 forbidden persistent candidates, hash `36d58d55b031d7e0580c61a0136a728dd8ce037e6905bf33c8de06d90784d3c6`.

Residual violations:
- Project-wide purge remains incomplete. Next safe isolated targets include other black-box-only MonoBehaviours; broad physics/world managers still require separate dependency maps before mutation.

## 2026-05-23 - T.A.R.S. Override Deep Audit

What was wrong:
- The exact prompt extractor used earlier was too strict for the active attributed tag shape. Attribute-aware CLI extraction confirms the X_000 prompt is still present in `Docs/Tasks/CURRENT_BATCH.md`.
- The scoped AudioLog migration still had a real read-path purity defect: `TryReadVaultBuffer<T>` mutated counters and wrote telemetry on read failure.
- The project is not clean. Full Roslyn audit still reports 2265 forbidden persistent native alias candidates, including 694 in MonoBehaviour classes across 78 files.

What was done:
- Reran full Roslyn native-field scan over `Assets/_Project/Scripts`: 2373 files, 0 parse failures, 7769 native fields, 2265 forbidden persistent candidates, hash `6e79e18d585c096f82a45bcf5546b7c34866323dee03582cceb751d6ef5ebc2d`.
- Wrote complete MonoBehaviour residual map to `Docs/Reports/VAULT_MONOBEHAVIOUR_NATIVE_FIELD_AUDIT_X_000.json`.
- Removed read-failure telemetry/counter mutation from `AudioLogSystem.TryReadVaultBuffer<T>` and removed fallback/buffer-id parameters from the read call chain.
- Renamed prologue/QA cold DataVault bootstrap methods from `ResolveDataVaultCold` to `CacheDataVaultCold` because they can cache `GlobalRegistry.DataVault` and are not pure `Resolve*` accessors.
- Extended that private-method rename to KineticCharacterAnimatorRuntime, VocalWarningSystem, SpectrumSystem, and TopographicalSonarSynthesizer. `rg "ResolveDataVaultCold" Assets/_Project/Scripts -g "*.cs"` now returns no matches.
- Fixed compile blockers exposed by final builds: qualified `Hecton8.Core.Contracts.AcousticEchoTap` in AcousticEchoLocationRuntime and added the missing scalability listener implementation in ProceduralWreckGenerator.
- Wrote read purity report to `Docs/Reports/VAULT_READ_ACCESSOR_PURITY_X_000.md`.
- Audited ARM64 explicit layouts for `AudioLogVaultTelemetryEntry`, `PrologueSequenceTelemetryEntry`, `QAEnduranceBlackBoxEntry`, and nested `AbsoluteUniversePosition`; wrote `Docs/Reports/VAULT_ARM64_LAYOUT_REPORT_X_000.md`.

Cinematic cheats used:
- None added. This pass is memory ownership and forensic DTO hygiene.
- Existing bounded rings remain fixed-size: AudioLog 300 x 64 bytes, Prologue 300 x 32 bytes, QA 300 x 128 bytes.

Exact microseconds saved:
- AudioLog read miss path: removes telemetry write attempts and counter mutation from `TryReadVaultBuffer<T>`. No profiler sample available in shell; structural work removed is two integer mutations plus potential DataVault telemetry writer-fence path on each failed read.
- Native alias count saved by this override patch: 0 additional fields; this was a purity correction, not another field migration.
- Residual project-wide count remains 2265 forbidden candidates. MonoBehaviour residual count is 694.

Verification:
- Build gate: CPU 35.6%, active dotnet/csc processes 0.
- `dotnet build Hecton8.Editor.csproj --no-restore`: succeeded, 0 warnings, 0 errors, 00:01:25.54.
- Second build gate after `CacheDataVaultCold` rename: first gate CPU 3.4% with 7 active dotnet processes, build correctly skipped; next gate cleared and `dotnet build Hecton8.Editor.csproj --no-restore` succeeded, 0 warnings, 0 errors, 00:01:30.67.
- Rebuild after the wider mechanical rename was gated off once: CPU 96.2%, active `csc` 1, active `dotnet` 1. No compiler was launched under the project rule.
- Final compile after compile-blocker fixes: `dotnet build Hecton8.Editor.csproj --no-restore /nr:false` succeeded, 0 warnings, 0 errors, 00:01:06.97.
- Top MonoBehaviour residual files: `DestructibleOrganicManager.cs` 50, `PlayerInventory.cs` 49, `EcosystemDirector.cs` 41, `HectonFluidEngine.cs` 39, `GasDynamicsSolver.cs` 34.
- ARM64 layout proof: all migrated DTO sizes are divisible by 8; all double/long/ulong fields are at offsets divisible by 8.

Residual violations:
- Project-wide purge is still incomplete. The complete residual map is the JSON artifact above; reporting otherwise would be false.
- Next safe work should target one owner slice at a time, not broad world/physics managers without dependency maps and phase ownership proof.

## 2026-05-23 - T.A.R.S. Override Slice 2: Orbital VFX And Sargassum Cut Commands

What was wrong:
- `OrbitalDropReentryVfxController` still stored `NativeArray<ReentryVfxTelemetryEntry> _telemetry` as a persistent MonoBehaviour field.
- `SargassumCutManager` still stored `NativeArray<StampCommand>` and `NativeArray<DamageVolumeStampCommand>` as persistent MonoBehaviour fields.
- `VAULT_EXORCISM_REPORT_X_000.json` still reflected only the first three migrations and was stale after the new audit.

What was done:
- Added `BufferID.OrbitalDropReentryVfxTelemetryRing = 74010`.
- Replaced Orbital reentry VFX telemetry storage with `VaultGenerationHandle<ReentryVfxTelemetryEntry>` and scoped DataVault writer/read-only views.
- Added `BufferID.SargassumCutStampCommands = 74300` and `BufferID.SargassumCutDamageVolumeStampCommands = 74301`.
- Replaced Sargassum cut command staging fields with `VaultGenerationHandle<StampCommand>` and `VaultGenerationHandle<DamageVolumeStampCommand>`.
- Converted Sargassum command DTOs to explicit layouts: `StampCommand` = 16 bytes, `DamageVolumeStampCommand` = 32 bytes.
- Moved the new high-valued Sargassum `BufferID` declarations into the high-ID vault block for audit readability.
- Updated ARM64 layout and read-accessor purity reports to include Reentry VFX and Sargassum DTOs.

Cinematic cheats used:
- Reentry VFX keeps a fixed 300-row forensic ring; no additional physical simulation was added.
- Sargassum cutting keeps bounded command staging at 16 rows per buffer; no expanded plant physics or per-blade simulation was introduced.

Exact microseconds saved:
- Persistent native alias count removed in this slice: 3 fields.
- Full-project forbidden candidate delta for this slice: 2265 -> 2262, net -3.
- MonoBehaviour residual delta for this slice: 694 -> 691, net -3.
- Orbital record path: one DataVault writer-fence attempt plus one 48-byte row write. No profiler microsecond sample available in shell.
- Sargassum upload path: scoped writer-fenced native view over 16 command rows max. No profiler microsecond sample available in shell.

Verification:
- `dotnet build Hecton8.Editor.csproj --no-restore /nr:false`: succeeded, 0 warnings, 0 errors, 00:01:37.65.
- Final rebuild after moving Sargassum high-valued `BufferID` declarations into the high-ID vault block: succeeded, 0 warnings, 0 errors, 00:00:29.16.
- Full Roslyn audit: 2373 files, 0 parse failures, 7766 native fields, 2262 forbidden persistent candidates, 691 MonoBehaviour candidates, hash `52d105de83557b78a42e3f76f71dc9aba903bf8cca77353ea3f52d4deb2f2d1a`.
- Migrated file zero-findings set now includes `AudioLogSystem.cs`, `AwaitableDropSequenceDirector.cs`, `QAEnduranceWatchdogBot.cs`, `OrbitalDropReentryVfxController.cs`, and `SargassumCutManager.cs`.
- ARM64 DTO report covers `AudioLogVaultTelemetryEntry`, `PrologueSequenceTelemetryEntry`, `QAEnduranceBlackBoxEntry`, `ReentryVfxTelemetryEntry`, `StampCommand`, and `DamageVolumeStampCommand`; all row sizes are divisible by 8 and no 8-byte field is unaligned.

Residual violations:
- Project-wide purge is still incomplete: 2262 forbidden persistent candidates remain, including 691 MonoBehaviour candidates across 76 files.
- Top MonoBehaviour residual owners remain `DestructibleOrganicManager`, `PlayerInventory`, `EcosystemDirector`, `HectonFluidEngine`, and `GasDynamicsSolver`.
- Broad world/physics owners require separate dependency maps and phase ownership proof before mutation.

## 2026-05-23 - T.A.R.S. Override Slice 3: Debris Front/Back State Buffers

What was wrong:
- `DebrisManager` still stored two persistent MonoBehaviour fields: `NativeArray<DebrisChunkState> _frontStates` and `NativeArray<DebrisChunkState> _backStates`.
- The old owner route allowed origin-shift and pending-burst mutation to touch state while `DebrisSimulationJob` could still be reading/writing front/back buffers.
- `DebrisChunkState` had no explicit ARM64 row proof in the X_000 layout report.

What was done:
- Added `SystemID.GameplayDebris = 352`.
- Added `BufferID.GameplayDebrisFrontStates = 74310` and `BufferID.GameplayDebrisBackStates = 74311`.
- Replaced both persistent native fields with `VaultGenerationHandle<DebrisChunkState>` descriptors.
- Scoped all front/back state access to method-local DataVault views and writer/read locks.
- Locked both front/back DataVault buffer IDs while `DebrisSimulationJob` is scheduled, then unlocked after `LateFrameTick` completion or forced release.
- Deferred origin-shift and pending burst writes while simulation buffers are locked, closing the write-while-job-read hazard.
- Converted `DebrisChunkState` to `[StructLayout(LayoutKind.Explicit, Size = 120)]` and added its padding table to `VAULT_ARM64_LAYOUT_REPORT_X_000.md`.
- Updated read-purity, MonoBehaviour residual, and exorcism reports.
- Fixed compile blockers revealed by the final gate: duplicate `SdfSqueezeJob` include, missing Kinematic contract/CCD compile includes, missing physics namespaces/aliases, ambiguous `AudioEvent` routes, and Win-only P/Invoke attribute qualification.

Cinematic cheats used:
- No new physical simulation. Debris remains a bounded 192-slot visual state buffer with existing cheap per-row math.
- Low/Middle/High/Ultra can scale debris density/cadence elsewhere; this slice keeps DTO shape and authority route fixed.

Exact microseconds saved:
- Persistent native alias count removed in this slice: 2 fields.
- Full-project forbidden candidate delta for this slice: 2262 -> 2260, net -2.
- MonoBehaviour residual delta for this slice: 691 -> 689, net -2.
- Added vault payload: 2 * 192 * 120 = 46,080 bytes plus vault metadata.
- No profiler sample available in shell. Structural gain is relocation safety and removal of stale front/back array aliases; job lock/unlock cost is bounded to one simulation schedule window.

Verification:
- Final gated compile: `dotnet build Hecton8.Editor.csproj --no-restore /nr:false` succeeded, 0 warnings, 0 errors, 00:02:07.61.
- Full Roslyn audit: 2374 files, 0 parse failures, 7764 native fields, 2260 forbidden persistent candidates, 689 MonoBehaviour candidates across 75 files, hash `3cb9b5bcd75166d8552bb2fc10f3c906ae6aae9199f47cdc772e9072bd07040b`.
- Migrated file zero-findings set now includes `AudioLogSystem.cs`, `AwaitableDropSequenceDirector.cs`, `QAEnduranceWatchdogBot.cs`, `OrbitalDropReentryVfxController.cs`, `SargassumCutManager.cs`, and `DebrisManager.cs`.
- ARM64 DTO report now covers `DebrisChunkState` 120 bytes; 120 % 8 = 0 and the DTO contains no double, long, or ulong fields.

Residual violations:
- Project-wide purge is still incomplete: 2260 forbidden persistent candidates remain, including 689 MonoBehaviour candidates across 75 files.
- Top MonoBehaviour residual owners remain `DestructibleOrganicManager`, `PlayerInventory`, `EcosystemDirector`, `HectonFluidEngine`, and `GasDynamicsSolver`.
- Broad world/physics owners require separate dependency maps and phase ownership proof before mutation.

## 2026-05-23 - T.A.R.S. Override Slice 4: UI And Visor Black-Box Rings

What was wrong:
- `InternalFloodWaterlineRuntime` still stored `NativeArray<WaterlineTelemetryEntry> _telemetry` as a persistent MonoBehaviour field.
- `DiegeticVisorHudMesh` still stored `NativeArray<DiegeticHudTelemetryEntry> _blackBox` as a persistent MonoBehaviour field.
- `DiegeticTooltipSystem` still stored `NativeArray<TooltipBlackBoxEntry> _blackBox` as a persistent MonoBehaviour field.

What was done:
- Added `BufferID.InternalFloodWaterlineTelemetryRing = 74312`.
- Added `BufferID.DiegeticVisorHudBlackBox = 74313`.
- Added `BufferID.DiegeticTooltipBlackBox = 74314`.
- Replaced all three persistent arrays with `VaultGenerationHandle<T>` descriptors under `SystemID.UI`.
- Converted record paths to scoped DataVault writer fences with `finally` release.
- Converted dump paths to scoped read-only DataVault views; no dump path regenerates or allocates DataVault buffers.
- Updated ARM64 layout report for `WaterlineTelemetryEntry`, `DiegeticHudTelemetryEntry`, and `TooltipBlackBoxEntry`.
- Updated read-purity, MonoBehaviour residual, and exorcism reports.

Cinematic cheats used:
- No new simulation. All three systems keep bounded 300-row forensic rings.
- UI/visor quality remains driven by continuous `GlobalQualityWeight`; DTO shape does not change by tier.

Exact microseconds saved:
- Persistent native alias count removed in this slice: 3 fields.
- Full-project forbidden candidate delta for this slice: 2260 -> 2257, net -3.
- MonoBehaviour residual delta for this slice: 689 -> 686, net -3.
- Added vault payload: 300 * 40 + 300 * 40 + 300 * 32 = 33,600 bytes plus metadata.
- No profiler sample available in shell. Structural gain is relocation-safe black-box ownership and removal of three phase-spanning NativeArray aliases.

Verification:
- Gated compile: `dotnet build Hecton8.Editor.csproj --no-restore /nr:false` succeeded, 0 warnings, 0 errors, 00:01:25.42.
- Full Roslyn audit: 2375 files, 0 parse failures, 7761 native fields, 2257 forbidden persistent candidates, 686 MonoBehaviour candidates across 72 files, hash `2907da94e9edd41e2bca5547d6ad941271bf22126cb0f94b5973d4bcb0e1b058`.
- Migrated file zero-findings set now includes `AudioLogSystem.cs`, `AwaitableDropSequenceDirector.cs`, `QAEnduranceWatchdogBot.cs`, `OrbitalDropReentryVfxController.cs`, `SargassumCutManager.cs`, `DebrisManager.cs`, `InternalFloodWaterlineRuntime.cs`, `DiegeticVisorHudMesh.cs`, and `DiegeticTooltipSystem.cs`.
- ARM64 DTO report now covers ten migrated DTO rows; all row sizes are divisible by 8 and no checked 8-byte field is misaligned.

Residual violations:
- Project-wide purge is still incomplete: 2257 forbidden persistent candidates remain, including 686 MonoBehaviour candidates across 72 files.
- Top MonoBehaviour residual owners remain broad world/physics/inventory managers and require owner maps before safe mutation.

## 2026-05-23 - T.A.R.S. Override Slice 5: HUD Notification Queue

What was wrong:
- `HUDNotification` still stored `NativeArray<NotificationRequest> _queue` as a persistent MonoBehaviour field.
- The queue row had no explicit ARM64 padding map in the X_000 report.
- The safe one-field candidate `PhysicalToolGripOffsets` was not migrated because it is prefab-instance-owned state; one global BufferID would create instance collisions without a dedicated route card.

What was done:
- Added `BufferID.HudNotificationQueue = 74315` under `SystemID.UI`.
- Replaced `_queue` with `VaultGenerationHandle<NotificationRequest>` and cached `IDataVault`.
- Converted `NotificationRequest` to `[StructLayout(LayoutKind.Explicit, Size = 8)]`.
- Queue mutations now acquire a DataVault writer fence and release it in `finally`.
- `ResolveQueueCapacity`, active-instance checks, and text write helpers do not allocate/grow/regenerate DataVault buffers.
- Regenerated the Roslyn ledger, MonoBehaviour residual report, exorcism report, ARM64 layout report, read-purity report, status, and rationale.

Cinematic cheats used:
- No new simulation. HUD notifications stay an 8-row hash/severity queue.
- Low/Middle/High/Ultra can scale presentation polish and cadence elsewhere; this queue DTO remains fixed.

Exact microseconds saved:
- Persistent native alias count removed in this slice: 1 field.
- Full-project forbidden candidate delta for this slice: 2257 -> 2256, net -1.
- MonoBehaviour residual delta for this slice: 686 -> 685, net -1.
- Added vault payload: 8 * 8 = 64 bytes plus metadata.
- No profiler sample available in shell. Structural gain is relocation-safe queue ownership and removal of one phase-spanning NativeArray alias.

Verification:
- Gated compile: CPU 38.2%, no active compiler processes; `dotnet build Hecton8.Editor.csproj --no-restore /nr:false` succeeded, 0 warnings, 0 errors, 00:01:41.35.
- Full Roslyn audit: 2375 files, 0 parse failures, 7760 native fields, 2256 forbidden persistent candidates, 685 MonoBehaviour candidates across 71 files, hash `fde89fba5e53852cceccdf46d017ae7e37c571f7abfefb63a0c9985ad19297dc`.
- Scoped `HUDNotification.cs` verification: 0 forbidden persistent findings.
- ARM64 DTO report now includes `NotificationRequest`: 8 bytes, 8 % 8 = 0, no double/long/ulong fields.

Residual violations:
- Project-wide purge is still incomplete: 2256 forbidden persistent candidates remain, including 685 MonoBehaviour candidates across 71 files.
- Top residual MonoBehaviour owners remain `DestructibleOrganicManager`, `PlayerInventory`, `EcosystemDirector`, `HectonFluidEngine`, and `GasDynamicsSolver`.
- Multi-instance prefab state and broad job-fan-out owners need route cards before migration.

## 2026-05-23 - T.A.R.S. Override Slice 6: Voxel Mesh Pipeline Black-Box

What was wrong:
- `HectonVoxelEngine` still stored static `NativeArray<VoxelMeshPipelineTelemetryEntry> _voxelMeshPipelineBlackBox` in a MonoBehaviour owner.
- The voxel mesh telemetry DTO was explicit in source but missing from X_000 ARM64 padding proof.
- The file also contains non-MonoBehaviour native candidates in `MCTables` and voxel scratch structs; those are separate owner routes and are not claimed clean.

What was done:
- Added `BufferID.VoxelMeshPipelineBlackBox = 74316` under `SystemID.WorldStreaming`.
- Replaced `_voxelMeshPipelineBlackBox` with `VaultGenerationHandle<VoxelMeshPipelineTelemetryEntry>` and cached `IDataVault`.
- `WriteVoxelMeshPipelineBlackBoxSample` now acquires a DataVault writer fence, writes one 32-byte row, and releases in `finally`.
- `DumpVoxelMeshPipelineBlackBox` uses a scoped read-only DataVault view and does not allocate/grow/regenerate the ring.
- Added ARM64 map for `VoxelMeshPipelineTelemetryEntry`: 32 bytes, explicit offsets, 8-byte-clean size, no double/long/ulong fields.
- Regenerated Roslyn ledger, MonoBehaviour residual report, exorcism report, ARM64 layout report, read-purity report, status, and rationale.

Cinematic cheats used:
- No new voxel simulation. The ring remains 300 compact rows; mesh telemetry is forensic, not visual truth.
- Low/Middle/High/Ultra can scale voxel mesh cadence and presentation outside this fixed DTO route.

Exact microseconds saved:
- Persistent native alias count removed in this slice: 1 field.
- Full-project forbidden candidate delta for this slice: 2256 -> 2255, net -1.
- MonoBehaviour residual delta for this slice: 685 -> 684, net -1.
- Added vault payload: 300 * 32 = 9600 bytes plus metadata.
- No profiler sample available in shell. Structural gain is relocation-safe voxel black-box ownership and removal of one phase-spanning NativeArray alias.

Verification:
- Gated compile: CPU 35.1%, no active compiler processes; `dotnet build Hecton8.Editor.csproj --no-restore /nr:false` succeeded, 0 warnings, 0 errors, 00:03:15.75.
- Full Roslyn audit: 2375 files, 0 parse failures, 7759 native fields, 2255 forbidden persistent candidates, 684 MonoBehaviour candidates across 70 files, hash `62f12414eb680d5f1b20a08d525612529ab16fd0aa104a080aa664ad2df3a603`.
- Scoped `HectonVoxelEngine.cs` MonoBehaviour verification: 0 MonoBehaviour forbidden persistent findings.
- Residual file truth: `HectonVoxelEngine.cs` still has 50 non-MonoBehaviour forbidden persistent candidates in `MCTables` and voxel scratch owner structs.

Residual violations:
- Project-wide purge is still incomplete: 2255 forbidden persistent candidates remain, including 684 MonoBehaviour candidates across 70 files.
- Top residual MonoBehaviour owners remain `DestructibleOrganicManager`, `PlayerInventory`, `EcosystemDirector`, `HectonFluidEngine`, and `GasDynamicsSolver`.
- Voxel table/scratch candidates require separate owner route cards; they were not bulk-migrated in this slice.
## T.A.R.S. Override Slice 7: Lore Unlock Words

What was wrong:
- `LoreDatabaseManager` stored persistent `NativeArray<uint> _unlockedWords` directly in a MonoBehaviour.
- `TryGetPackedUnlockWords` was a read-looking route that called `EnsureUnlockStorage`, creating/growing native storage lazily.
- `TryGetRecordIndex` lazily rebuilt the lore hash lookup table from a `TryGet*` route.
- The follow-up gated build exposed an unrelated compile blocker in `PDADecryptionSpectrogramPanel.cs`: unqualified `ToolHapticsRuntime` did not resolve in the generated project context.

What was done:
- Added `SystemID.LoreDatabase = 353`.
- Added `BufferID.LoreDatabaseUnlockedWords = 74317`.
- Replaced `NativeArray<uint> _unlockedWords` with `VaultGenerationHandle<uint> _unlockedWordsHandle` and cached `IDataVault`.
- Moved mutable unlock/load paths behind `TryAcquireWriteLock` / `ReleaseWriteLock` with `finally`.
- Made `TryGetPackedUnlockWords` a pure read-only handle validation route.
- Moved lookup-table build out of `TryGetRecordIndex` into `Awake` / write-side cold setup.
- Fully qualified `Hecton8.Tools.ToolHapticsRuntime.EnqueueSinusoidalCommand` to restore compiler resolution.

Cinematic Cheats used:
- None. This slice is memory ownership and read-purity only; no simulation or visual approximation changed.

Exact Microseconds saved:
- Profiler proof not available from shell. Static effect is removal of one persistent native alias and removal of hidden DataVault allocation/regeneration from `TryGetPackedUnlockWords`.

Verification:
- `dotnet build Hecton8.Editor.csproj --no-restore /nr:false` succeeded in 00:01:45.38 with 0 warnings and 0 errors.
- Full Roslyn audit: 2378 files, 0 parse failures, 7766 native fields, 2252 forbidden persistent candidates, 683 MonoBehaviour candidates across 69 files.
- Proof hash: `1923e614ac7170e17cdc137caf69ca6f6b68ae6386a84c1cb24b13a4f13eacdd`.
- Project-wide purge remains incomplete.

## T.A.R.S. Override Slice 8: Headless Stress Fracture QA

What was wrong:
- `HeadlessStressFractureBot` stored persistent `NativeArray<FractureTelemetryEntry> _blackbox` and `NativeArray<byte> _scratchBlock` directly in a MonoBehaviour.
- The scratch block used local `H8Memory.Allocate<byte>` ownership, so the QA memory fracture harness itself violated the DataVault descriptor rule.

What was done:
- Added `SystemID.QAHeadless = 354`.
- Added `BufferID.QAHeadlessStressFractureBlackBoxRing = 74318` and `BufferID.QAHeadlessStressFractureScratchBlock = 74319`.
- Replaced both native fields with `VaultGenerationHandle` descriptors.
- `RecordBlackbox` now acquires a DataVault writer fence, writes one 64-byte row, and releases in `finally`.
- `TryReadBlackbox`, `DumpBlackbox`, and manifest generation use scoped read-only views only.
- Scratch pressure remains explicit in `PulseScratchMemory` / `AcquireScratchBlock`; no raw `NativeArray<byte>` survives between phases.

Cinematic Cheats used:
- None. This is QA memory ownership and forensic telemetry, not visual simulation.

Exact Microseconds saved:
- Profiler proof not available from shell. Static effect is removal of two persistent native aliases and removal of local H8Memory ownership from the QA scratch block.
- Added vault payload: 300 * 64 = 19,200 bytes for the black-box ring plus an explicit 8..256 MB QA scratch pressure payload.

ARM64 proof:
- `FractureTelemetryEntry` is 64 bytes. `NativeBytes` sits at offset 16 and `H8Bytes` at offset 24; both offsets are divisible by 8.
- Scratch payload is `scratchMegabytes * 1048576`; every allowed capacity is divisible by 8 and contains no 8-byte scalar field.

Verification:
- Build gate launched at CPU 39.2% with 0 active `dotnet`/`csc` processes.
- `dotnet build Hecton8.Editor.csproj --no-restore /nr:false` succeeded in 00:01:23.68 with 0 warnings and 0 errors.
- Full Roslyn audit: 2379 files, 0 parse failures, 7755 native fields, 2248 forbidden persistent candidates, 679 MonoBehaviour candidates across 68 files.
- Proof hash: `360438e0a6efe9d5fa73b1c6755cf31805a5bf20a9048f8f397cffbd45bf82d8`.
- Scoped `HeadlessStressFractureBot.cs`: 0 forbidden persistent findings.
- Project-wide purge remains incomplete.

## T.A.R.S. Override Slice 9: Instance Culling Readback And Telemetry

What was wrong:
- `InstanceCullingService` stored persistent `NativeArray<uint> _indirectArgsReadback` and `NativeArray<InstanceCullingTelemetryEntry> _telemetryRing` directly in a MonoBehaviour.
- The delayed `AsyncGPUReadback` path could retain a native pointer across callback timing without DataVault ownership proof.
- A full audit during this loop found one real parse failure in `HeadlessStressFractureBot.cs` from an extra brace.
- A gated build also exposed unqualified `SanitizeFinite` calls in audio signal DTO constructors.

What was done:
- Added `BufferID.InstanceCullingIndirectArgsReadback = 74320` and `BufferID.InstanceCullingTelemetryRing = 74321`.
- Replaced both culling native fields with `VaultGenerationHandle` descriptors under existing `SystemID.GraphicsScalability`.
- `TryRequestTelemetryReadback` now acquires a DataVault writer fence for the GPU readback scratch and releases it in the callback or teardown guard.
- `TryReadIndirectArgsReadback`, `TryReadTelemetryRing`, `TryConsumeTelemetry`, and `DumpBlackBox` use read-only DataVault views only and do not allocate or regenerate buffers.
- Removed the extra brace in `HeadlessStressFractureBot.cs`; full Roslyn audit is back to 0 parse failures.
- Qualified the audio DTO finite sanitization calls through `SignalPayloadSanitizer`; final build is clean.

Cinematic Cheats used:
- Culling remains a visual GPU culling/telemetry route; no gameplay truth changed. The low-tier survival path keeps the same readback cadence and bounded forensic ring, while high/ultra can spend budget on higher instance density through existing quality-weight paths.

Exact Microseconds saved:
- Profiler proof not available from shell. Static effect is removal of two persistent native aliases and relocation-safe GPU readback ownership.
- Added vault payload: 20 bytes primitive indirect-args scratch plus 300 * 64 = 19,200 bytes telemetry ring, excluding metadata.

ARM64 proof:
- `InstanceCullingTelemetryEntry` is explicit 64 bytes. `Padding1`, `Padding2`, and `Padding3` are `ulong` lanes at offsets 40, 48, and 56; all offsets are divisible by 8.
- The indirect args readback payload is primitive `uint[5]` with no double, long, or ulong fields. It is GPU ABI scratch, not a structured DTO row.

Verification:
- Full Roslyn audit: 2383 files, 0 parse failures, 7737 native fields, 2241 forbidden persistent candidates, 675 MonoBehaviour candidates.
- Proof hash: `ef7e3db1164bdfa36b350658c8e843cc2b3d7e92c6b59d49ab4bf6b90f950d79`.
- Scoped `InstanceCullingService.cs`: 0 forbidden persistent findings. Remaining native finding is allowed transient job parameter `ApplyAupShiftJob.Matrices`.
- Build gate launched at CPU 23.5% with 0 active `dotnet`/`csc` processes.
- `dotnet build Hecton8.Editor.csproj --no-restore /nr:false` succeeded in 00:01:38.06 with 0 warnings and 0 errors.
- Project-wide purge remains incomplete: 2241 forbidden persistent candidates remain, including 675 MonoBehaviour candidates.

## T.A.R.S. Override Slice 10: Trauma Dispatcher Parasite LOS

What was wrong:
- `TraumaDispatcher` stored persistent `NativeArray<RaycastCommand> _parasiteSporeLosCommands` and `NativeArray<RaycastHit> _parasiteSporeLosHits` directly in a MonoBehaviour.
- The old `LateFrameTick` could call the allocation/ensure path if the buffers were missing.

What was done:
- Added `BufferID.TraumaDispatcherParasiteSporeLosCommands = 74322` and `BufferID.TraumaDispatcherParasiteSporeLosHits = 74323`.
- Replaced both native fields with `VaultGenerationHandle` descriptors under existing `SystemID.GameplayPlayer`.
- Cold setup in `Awake` / `OnEnable` creates the one-row command/hit buffers.
- `LateFrameTick` now acquires method-local DataVault writer views only immediately before `RaycastCommand.ScheduleBatch`; it no longer regenerates buffers.
- Writer fences stay held while the physics job owns the views, then `CompleteParasiteSporeLosQuery` releases them and reads the hit through `TryReadParasiteSporeLosHits` read-only view.

Cinematic Cheats used:
- None. This slice preserves the existing one-ray LOS gameplay truth. No visual simulation changed.

Exact Microseconds saved:
- Profiler proof not available from shell. Static effect is removal of two persistent native aliases and removal of hot-path allocation/regeneration risk from the parasite LOS route.
- Added vault payload: two one-row Unity Physics buffers, excluding metadata.

ARM64 proof:
- No new X_000 custom DTO was introduced. The payloads are `UnityEngine.RaycastCommand` and `UnityEngine.RaycastHit`, consumed by Unity's own `RaycastCommand.ScheduleBatch` ABI.
- The ARM64 layout report records this as a Unity Physics ABI boundary, not a fabricated X_000 padding table.

Verification:
- Build gate waited once at CPU 73.6%, then launched at CPU 24.2% with 0 active `dotnet`/`csc` processes.
- `dotnet build Hecton8.Editor.csproj --no-restore /nr:false` succeeded in 00:01:22.97 with 0 warnings and 0 errors.
- Full Roslyn audit: 2390 files, 0 parse failures, 7747 native fields, 2248 forbidden persistent candidates, 682 MonoBehaviour candidates across 68 files.
- Proof hash: `2b80ff4bffa6b59e63a1d41222cad450f8d98f6792bbe36d1db2fe440bedb98f`.
- Scoped `TraumaDispatcher.cs`: 0 forbidden persistent findings.
- Project-wide purge remains incomplete.

## T.A.R.S. Override Slice 11 - Raycast Batch Helper

What was wrong: `RaycastBatchHelper` retained `NativeArray<RaycastCommand> _commands` and `NativeArray<RaycastHit> _hits` as MonoBehaviour fields. That left Unity Physics batch buffers as persistent local aliases outside DataVault relocation/defragmentation control.

What was done: Replaced both fields with `VaultGenerationHandle` descriptors under `SystemID.Physics` and BufferIDs 74324..74325. `AddQuery` writes one command through a short writer fence. `ExecuteBatch` locks command/hit buffers for the scheduled `RaycastCommand.ScheduleBatch` window and late-frame completion releases locks before read-only hit consumption.

Cinematic cheats used: none. This is physics-query infrastructure, not a visual simulation; the cheat is preserving batch scheduling and failing closed instead of forcing synchronous raycasts.

Exact microseconds saved: no profiler sample from shell. Expected GC delta: 0 B/frame. Main verified gain is correctness: 2 fewer persistent MonoBehaviour native aliases and no lazy allocation from result/read helpers.

Proof: gated build `dotnet build Hecton8.Editor.csproj --no-restore /nr:false` completed in 00:00:19.56 with 0 warnings / 0 errors. Roslyn ledger hash `baf4734bc9c976e6a331deb5e1b7b3d5ea1bad3d696bc62992b1713aa15bb345`: 2390 files, 0 parse failures, 2173 forbidden persistent candidates, 680 MonoBehaviour candidates across 65 files. `RaycastBatchHelper.cs` scoped findings: 0.

## T.A.R.S. Override Slice 12 - Physical Tool Grip And Diegetic HUD Layout

What was wrong:
- `PhysicalToolGripOffsets` retained `NativeArray<float4x4> _gripOffsets` for two authored per-instance offsets.
- `DiegeticHudManualLayout` retained `NativeArray<DiegeticHudLayoutInput> _inputs` and `NativeArray<float3> _outputs` for tiny local UI layout scratch.
- DataVault would have been wrong for the physical tool offsets because a single global `BufferID` would collide across prefab instances.

What was done:
- Replaced physical grip native storage with `_leftGripOffset`, `_rightGripOffset`, and `_offsetsCached` unmanaged value state.
- Rewrote diegetic HUD manual layout to compute target positions through stack/value DTOs and direct `Transform.localPosition` writes.
- Removed native dispose/sentinel paths from both files because no persistent native owner remains.
- Kept `DiegeticHudLayoutJob` only as a transient job contract type; the MonoBehaviour no longer owns or schedules persistent `_inputs` / `_outputs` buffers.
- Fixed compile-only blockers exposed by verification: `VoxelStreamingScratchLease` owner/slot accessibility, `FaunaBrain` prey-brain locals, and unused catch variables in SettingsManager/SpatialAudioManager.

Cinematic cheats used:
- Physical tool grip uses authored transform values instead of any runtime native buffer or simulation.
- HUD layout uses direct value math; the rejected native job path was too small to justify scheduling or retained native scratch.

Exact microseconds saved:
- Profiler proof not available from shell. Static effect is removal of three persistent MonoBehaviour native aliases and removal of native allocation/disposal pressure from two small UI/interaction paths.
- Expected GC delta: 0 B/frame. No new DataVault payload was added for this slice.

ARM64 proof:
- `PhysicalToolGripOffsets` stores two `float4x4` value fields; each is 64 bytes, 64 % 8 = 0, with no double/long/ulong fields.
- `DiegeticHudLayoutInput` is explicit 16 bytes: floats at offsets 0/4/8 and `_pad0` at 12.
- `DiegeticHudLayoutSettings` is explicit 16 bytes: `Axis` at 0, reserves at 1/2, floats at 4/8/12.

Verification:
- Final build command `dotnet build Hecton8.Editor.csproj --no-restore /nr:false` succeeded in 00:01:39.10 with 0 warnings and 0 errors.
- Full Roslyn audit: 2390 files, 0 parse failures, 7721 native fields, 2233 forbidden persistent candidates, 677 MonoBehaviour candidates across 63 files.
- Proof hash: `c7156675265f9b616938d414b92e95419fb09ebfc6199c06b12d9a1a9eb5e76d`.
- Scoped findings: `PhysicalToolGripOffsets.cs` 0, `DiegeticHudManualLayout.cs` 0, `RaycastBatchHelper.cs` 0.
- X_000 targeted removals now total 27 persistent native aliases.
- Project-wide purge remains incomplete.

## T.A.R.S. Override Slice 13 - Font Streaming Prefetch

What was wrong:
- `FontStreamingManager` retained `NativeArray<uint> _visibleHashPrefetch` and `NativeArray<int2> _visibleSlicePrefetch` as MonoBehaviour fields.
- The buffers were UI prefetch scratch, but they survived across phases as raw native aliases instead of relocatable DataVault ownership.

What was done:
- Added `BufferID.FontStreamingVisibleHashPrefetch = 74326` and `BufferID.FontStreamingVisibleSlicePrefetch = 74327` under `SystemID.UI`.
- Replaced both native fields with `VaultGenerationHandle` descriptors, scalar capacity state, and explicit writer-lock state.
- `CollectSwapQueue` now writes visible hashes through a method-local DataVault writer view.
- Visible text offset prefetch scheduling passes method-local `NativeArray<uint>` and `NativeArray<int2>` views to `LocRegistry.TryScheduleVisibleTextOffsetPrefetch`, then releases writer locks after completion/teardown.
- `LabelSwapScheduler.ApplyPrefetchSlices` now accepts `NativeArray<int2>.ReadOnly`.
- Fixed compile proof-route blockers without claiming memory ownership: current input contracts/source are used for the local generated-project build, stale `Hecton8.Input.dll` identity conflict is avoided, `InputManager` reference warnings were removed, and unrelated compiler errors exposed by verification were corrected.

Cinematic cheats used:
- None. This is UI/font prefetch infrastructure. The preserved cheat is bounded prefetch capacity and fail-closed reads instead of hot-path regeneration.

Exact microseconds saved:
- Profiler proof not available from shell. Static effect is removal of two persistent MonoBehaviour native aliases and removal of retained raw prefetch buffers.
- Expected GC delta: 0 B/frame. Added vault payload is one primitive `uint` buffer plus one `int2` buffer at visible prefetch capacity, excluding metadata.

ARM64 proof:
- `FontStreamingVisibleHashPrefetch` is a primitive `uint` payload: 4-byte lanes, no double/long/ulong fields.
- `FontStreamingVisibleSlicePrefetch` is an `int2` payload: 8 bytes per row, 8 % 8 = 0, two 4-byte int lanes, no double/long/ulong fields.

Verification:
- Final gated build command `dotnet build Hecton8.Editor.csproj --no-restore /nr:false -p:UseSharedCompilation=false -v:minimal` succeeded in 00:02:21.34 with 0 warnings and 0 errors.
- Full Roslyn audit: 2398 files, 0 parse failures, 7740 native fields, 2234 forbidden persistent candidates, 671 MonoBehaviour candidates across 62 files.
- Proof hash: `b2a2d0e9af041616dbcba8004e5e81476593942c8417c9c8d69b6943956eb99d`.
- Scoped `FontStreamingManager.cs`: 0 forbidden persistent findings.
- X_000 targeted removals now total 29 persistent native aliases.
- Project-wide purge remains incomplete.

## T.A.R.S. Override Slice 14 - Vehicle Sub OS Cockpit

What was wrong:
- `VehicleSubOsCockpitRuntime` retained seven persistent `NativeArray` fields in a MonoBehaviour: button states, targets, progress, offsets, base positions, matrices, and cockpit telemetry.
- The base-position payload used raw `float3` rows, which are 12-byte stride and not acceptable for the requested 8-byte row proof.

What was done:
- Added UI DataVault BufferIDs `74328..74334`.
- Replaced the seven raw native fields with `VaultGenerationHandle` descriptors and scalar lock state.
- Added explicit `CockpitButtonBasePosition` as a 16-byte row and kept `CockpitTelemetryEntry` as explicit 64-byte telemetry row.
- Reworked button job scheduling to use method-local writer views held only for the scheduled job window.
- Reworked upload, transform application, telemetry dump, and damage mirror dump paths to use read-only views only.

Cinematic cheats used:
- None for gameplay truth. The button animation remains kinematic/value-driven; no added simulation.

Exact microseconds saved:
- Profiler proof not available from shell. Static effect is removal of seven persistent MonoBehaviour native aliases and removal of raw `float3` retained stride from cockpit button bases.
- Expected GC delta: 0 B/frame.

ARM64 proof:
- `CockpitButtonBasePosition`: 16 bytes, `float3` at offset 0, `_pad0` at offset 12, 16 % 8 = 0.
- `CockpitTelemetryEntry`: 64 bytes, all fields 4-byte lanes, 64 % 8 = 0, no double/long/ulong fields.

Verification:
- Scoped `VehicleSubOsCockpitRuntime.cs`: 0 forbidden persistent findings.
- Build before the next slice succeeded in 00:02:23.01 with 0 warnings and 0 errors.

## T.A.R.S. Override Slice 15 - Fake Radar Blip Controller

What was wrong:
- `FakeRadarBlipController` retained `NativeArray<RadarCullCandidate> _radarCullCandidates`, `NativeArray<RadarCullResult> _radarCullResults`, and `NativeList<Matrix4x4> _visibleBlipMatrices`.
- The native cull job operated over at most 64 HUD blips and fed a same-frame render handoff. That is a tiny-job pattern with persistent native scratch.

What was done:
- Removed the Burst cull job, both native arrays, the native list, native allocators, `JobHandle`, and native dispose/sentinel paths from the file.
- Replaced the job with direct value culling over the existing fixed `SpatialQueryHit[64]` query buffer.
- Writes now go directly into the existing fixed managed `Matrix4x4[64]` draw buffer.
- Fixed compile-only blockers exposed by verification: `RadiationHazardGrid` uint-to-int status source boundary and `GlobalRegistryContracts` missing `Hecton8.Items` namespace.

Cinematic cheats used:
- Rejected a job for 64 HUD blips. The cheap visual fake is direct bounded 2D radar projection with thermal ghost hashing, scaled by continuous quality weight.

Exact microseconds saved:
- Profiler proof not available from shell. Static effect is removal of three persistent MonoBehaviour native aliases and one tiny scheduled job path.
- Expected GC delta: 0 B/frame because the retained buffers are fixed managed arrays already owned by the MonoBehaviour.

ARM64 proof:
- No new DTO was introduced. Former native rows `RadarCullCandidate` and `RadarCullResult` were deleted.
- Retained draw matrices are `Matrix4x4` values, 64 bytes, 64 % 8 = 0.

Verification:
- Final gated build `dotnet build Hecton8.Editor.csproj --no-restore /nr:false -p:UseSharedCompilation=false -v:minimal` succeeded in 00:01:34.75 with 0 warnings and 0 errors.
- Full Roslyn audit: 2398 files, 0 parse failures, 7736 native fields, 2224 forbidden persistent candidates, 661 MonoBehaviour candidates across 60 files.
- Proof hash: `aeca727724ffa3d082660c7c28726bd038689766a6e25f42ab9fc5e9d335638e`.
- Scoped `VehicleSubOsCockpitRuntime.cs`: 0 forbidden persistent findings.
- Scoped `FakeRadarBlipController.cs`: 0 forbidden persistent findings.
- X_000 targeted removals now total 39 persistent native aliases.
- Project-wide purge remains incomplete.

## T.A.R.S. Override Slice 16 - Ecosystem Wrapper And World Procedural Sampler

What was wrong:
- `EcosystemDirector` hid a persistent `NativeArray<T>` view inside its nested `VaultNativeArray<T>` helper. That let the outer MonoBehaviour look descriptor-based while the helper still retained a raw native alias.
- `WorldProceduralFieldSampler` retained six persistent native arrays in a MonoBehaviour: zone rows, biome matrix rows, matrix-index lookup, biome family rows, cave entrance hints, and a 512x512 noise LUT.
- The sampler buffers feed Burst jobs and scalar biome read helpers, so stale raw views could survive between preparation, sampling, and read phases.

What was done:
- Rewrote `EcosystemDirector.VaultNativeArray<T>` to store only `IDataVault` and `VaultGenerationHandle<T>`. All native views are resolved method-locally.
- Added `SystemID.WorldProceduralFieldSampler` and BufferIDs `74363..74368`.
- Replaced six `WorldProceduralFieldSampler` native fields with DataVault generation handles plus scalar lock state.
- `PrepareBurstData` now fills DataVault payloads explicitly.
- `ScheduleCellSamplingJob` now acquires writer fences for the six sampler buffers before the Burst job and releases them after completion, teardown, or schedule failure.
- `TryGetZoneData`, `TryGetBiomeMatrixData`, `TryGetBiomeFamilyData`, and secondary-biome fallback now read through `TryReadOnlyHandle` only.

Cinematic cheats used:
- The sampler keeps the existing 512x512 ushort LUT rather than generating arbitrary procedural noise per read. That is a bounded lookup-cheat with predictable memory and CPU behavior.
- No new realism simulation was added. Saved ownership complexity can be spent later on denser biome presentation through continuous quality scaling, not on retained raw native views.

Exact microseconds saved:
- Profiler proof is not available from shell. Static effect is removal of six persistent MonoBehaviour native aliases plus one hidden wrapper alias carrier.
- Expected GC delta: 0 B/frame. DataVault payloads are bounded by zone/profile counts and a fixed 524288-byte noise LUT payload.

ARM64 proof:
- `ZoneData`: explicit 64 bytes, 64 % 8 = 0, no double/long/ulong fields.
- `BiomeMatrixData`: explicit 64 bytes, 64 % 8 = 0, no double/long/ulong fields.
- `BiomeFamilyData`: explicit 16 bytes, `BiomeFamilyFlags : ulong` at offset 8, 16 % 8 = 0.
- `CaveEntranceHintData`: explicit 32 bytes, 32 % 8 = 0, no double/long/ulong fields.
- Noise LUT: 262144 `ushort` entries = 524288 bytes, 524288 % 8 = 0.

Verification:
- Scoped regex for direct private native collection fields in `WorldProceduralFieldSampler.cs` and `World/EcosystemDirector.cs`: 0 findings.
- Obsolete-call scan after the previous failed build found no producer call sites using the old APIs; only legacy wrapper definitions and smoke-test strings remain.
- Clean build and full Roslyn ledger are pending because the local CPU-gate is currently above the project threshold.
- X_000 targeted removals now total 46 persistent native aliases/alias carriers.
- Project-wide purge remains incomplete.

## T.A.R.S. Override Slice 17 - Radiation Hazard Grid Cached View Purge

What was wrong:
- `RadiationHazardGrid` already used DataVault handles for its radiation payloads but retained twelve raw `NativeArray` fields as cached views.
- Those fields covered grid read/write/source buffers, radiation state, sources, source count lane, telemetry ring/cursor, profiles, csv scratch, tuning, and status signal lane.

What was done:
- Replaced the twelve raw fields with `VaultNativeArray<T>` descriptors that store only `IDataVault` plus `VaultGenerationHandle<T>`.
- Existing radiation jobs and save/load code still receive `NativeArray<T>` views, but only through method-local wrapper resolution.
- Fixed the wrapper-to-unsafe-pointer compile issue by using explicit generic `GetUnsafePtr<T>` and `GetUnsafeReadOnlyPtr<T>` calls.
- Fixed a namespace-only compiler blocker in `FaunaBrain.CombatDamageReceiver.cs` by importing `Hecton8.World` for the existing vegetation bridge call.

Cinematic cheats used:
- None added. Existing low-tier behavior remains the fixed 32^3 grid plus capped 64 source lane; higher visual response can scale outside the DTO ownership route.

Exact microseconds saved:
- Profiler proof not available from shell. Static effect is removal of twelve persistent MonoBehaviour native aliases.
- Expected GC delta: 0 B/frame. No new DataVault payload was added; the slice uses existing radiation handles.

ARM64 proof:
- No new DTO row was introduced.
- Existing rows checked in the ARM64 report: `RadiationStateDTO` 32 bytes, `RadiationStatusSignal` 32 bytes, `RadiationTelemetryEntry` 64 bytes, `RadiationSource` 64 bytes, `RadiationProfileDTO` 64 bytes, `RadiationTuningDTO` 32 bytes.

Verification:
- Scoped regex for direct private native collection fields in `RadiationHazardGrid.cs`, `WorldProceduralFieldSampler.cs`, and `World/EcosystemDirector.cs`: 0 findings.
- Clean build and full Roslyn ledger are pending because Unity/Bee currently keeps active compiler processes and CPU-gate remains above the allowed threshold.
- X_000 targeted removals now total at least 58 persistent native aliases/alias carriers.
- Project-wide purge remains incomplete.

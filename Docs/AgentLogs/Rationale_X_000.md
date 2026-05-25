# Rationale_X_000

Status: BUILD CLEAN / PHYSICAL TOOL GRIP AND DIEGETIC HUD MANUAL LAYOUT NATIVE ALIAS PURGE COMPLETE / PROJECT-WIDE PURGE INCOMPLETE

## Decision 000 - Scope Gate

Problem: The assignment demands eradication of persistent native collection aliases across Assets/_Project/Scripts without corrupting parallel agent work.
Solution: Start with a Roslyn AST audit ledger, then migrate only targets with proven owner, phase, and DataVault API compatibility.
Rejected Alternatives: Regex-only grep was rejected because it cannot distinguish fields from locals. Blind bulk replacement was rejected because it can break Burst job signatures and owner routes without proof.
Scalability potential: Low uses static/offline proof with no runtime cost. Middle/High/Ultra get the same correctness route; saved runtime risk can be spent later in VISUAL_SYNC, not on uncontrolled simulation.
Hardware Impact: 0 us runtime impact for audit. Preventing stale NativeArray aliases avoids relocation crashes and cache-fragmentation failures on i3/MX350.

## Decision 001 - Mandate Selection

Problem: The task crosses native collection ownership, job scheduling, telemetry, ARM64 struct layout, registry boundaries, and frame budgets.
Solution: Load eight mandates: native memory/jobs, arena allocator, ARM64 layout, crash telemetry, execution phases, registry DI, zero GC, and performance budgets.
Rejected Alternatives: Reading unrelated rendering/AI mandates was rejected because it adds noise before target systems are known.
Scalability potential: The selected mandates preserve Low/Middle/High/Ultra behavior through continuous GlobalQualityWeight and phase ownership, not binary tier switches.
Hardware Impact: Reduces risk of hot-path allocations, hidden Complete calls, and stale pointer crashes on low-end silicon.

## Decision 002 - Roslyn Ledger Over Regex Ledger

Problem: Existing DataVaultSovereigntyAudit produced useful counts but is regex/heuristic based and exceeded the first two-minute execution ceiling.
Solution: Added a narrow Roslyn console scanner that walks FieldDeclarationSyntax, classifies Core/Memory authority fields, transient job fields, and forbidden persistent candidates, then writes a SHA-256-stamped JSON ledger.
Rejected Alternatives: Expanding the existing Python audit was rejected because Task 01 explicitly requires Roslyn AST parsing. Running Unity Editor menu scanners was rejected because this shell session has no Unity runtime proof channel.
Scalability potential: Offline tool has no runtime cost. Low/Middle/High/Ultra all benefit from deterministic memory-owner evidence before migration work.
Hardware Impact: 0 us runtime impact; compile/run cost is editor/offline only.

## Decision 003 - AudioLogSystem First Migration

Problem: The global ledger contains 2270 forbidden persistent candidates; bulk migration would break unrelated systems owned by other active agents. A first cut needed a small, self-contained MonoBehaviour with no Burst job signature fan-out.
Solution: Migrated AudioLogSystem's playback queue and encrypted fragment state to DataVault handles, added BufferID entries 70672..70676, and recorded failures in a 64-byte AudioLogVaultTelemetryEntry ring.
Rejected Alternatives: PlayerInventory and world/physics managers were rejected for this pass because they have broad save/UI/job dependencies. AudioLogEvents queues were rejected for this pass because they are a separate static event lane and require a SignalBus route decision, not a local buffer swap.
Scalability potential: Low uses the same 16-slot playback queue and 32 encrypted fragment records. Middle/High/Ultra can add richer presentation/audio behavior through existing audio routes without changing save identity or DTO layout.
Hardware Impact: Removes three persistent native aliases from a MonoBehaviour. Added vault memory is 16 uints + 64 uints + 300 telemetry rows + cursor, approximately 19.6 KB plus vault metadata; no per-frame GC introduced.

## Decision 004 - Telemetry Writer Fence Correction

Problem: The first AudioLog telemetry pass resolved the black-box ring and cursor as mutable views without explicit writer fences.
Solution: Rewrote RecordVaultTelemetry to acquire and release DataVault write locks for the telemetry ring and cursor in a bounded try/finally block.
Rejected Alternatives: Leaving telemetry as TryResolveHandle was rejected because black-box writes are still writes. Logging failures through Debug.Log was rejected because it allocates/serializes managed state and does not produce fixed-row forensic data.
Scalability potential: Low records the same 300-row ring with no heap allocation. Middle/High/Ultra can increase downstream visual/audio fidelity without changing telemetry DTO layout or ownership.
Hardware Impact: Adds two short-lived writer-fence attempts only on fallback/error telemetry paths; expected normal-frame cost is 0 us because RecordVaultTelemetry is not called on successful steady-state reads/writes.

## Decision 005 - Prologue Black-Box Ring Migration

Problem: AwaitableDropSequenceDirector held a persistent NativeArray<PrologueSequenceTelemetryEntry> black-box ring as a MonoBehaviour field.
Solution: Added SystemID.PrologueSequence and BufferID.PrologueSequenceTelemetryRing, then replaced the field with a VaultGenerationHandle descriptor. RecordStage now writes through a bounded DataVault writer fence; DumpBlackBox resolves a read-only view only inside dump scope.
Rejected Alternatives: Keeping the private NativeArray was rejected because it survives across phases and blocks relocation safety. Replacing the ring with managed arrays was rejected because the black-box mandate requires unmanaged fixed-row forensic state.
Scalability potential: Low keeps the same 300 rows at 32 bytes per entry. Middle/High/Ultra gain no extra simulation burden; presentation overkill remains outside this contract-only prologue director.
Hardware Impact: Removes one persistent native alias from a MonoBehaviour. Vault payload is 9600 bytes plus vault metadata; steady-state recording remains one fixed row write behind a short-lived writer fence.

## Decision 006 - QA Endurance Black-Box Ring Migration

Problem: QAEnduranceWatchdogBot held NativeArray<QAEnduranceBlackBoxEntry> as a persistent MonoBehaviour field for crash/endurance forensic state.
Solution: Added SystemID.QAEndurance and BufferID.QAEnduranceBlackBoxRing, then replaced the field with a VaultGenerationHandle descriptor. WriteBlackBox now writes through a bounded DataVault writer fence; DumpBlackBox resolves a read-only view only during binary export.
Rejected Alternatives: Leaving the QA-only NativeArray was rejected because QA systems still participate in relocation safety and can mask architecture failures. Moving the data to managed lists was rejected because the dump requires fixed-size unmanaged rows.
Scalability potential: Low keeps the 300-row, 128-byte forensic window. Middle/High/Ultra do not change gameplay truth; QA endurance fidelity remains bounded by tier intervals already present in the bot.
Hardware Impact: Removes one persistent native alias from a MonoBehaviour. Vault payload is 38400 bytes plus metadata; hot-path cost is one writer-fence attempt on active QA endurance frames only, not normal gameplay.

## Decision 007 - Read Accessor Purification

Problem: The T.A.R.S. override exposed a real defect in the scoped migration: `AudioLogSystem.TryReadVaultBuffer<T>` was a read helper but still incremented counters and wrote telemetry rows on read failure.
Solution: Removed all telemetry/counter mutation from `TryReadVaultBuffer<T>` and removed the unused fallback/buffer-id parameters from its read call chain. `TryGetEncryptedFragmentBits` now reaches DataVault through a read-only, fail-closed path only. Renamed prologue/QA cold DataVault bootstrap methods from `ResolveDataVaultCold` to `CacheDataVaultCold` because those methods cache `_dataVault` and are not pure Resolve accessors.
Rejected Alternatives: Keeping side effects because the helper is private was rejected. Renaming only the AudioLog helper was rejected because the doctrine also treats `Resolve*` names as pure read accessors.
Scalability potential: Low tier avoids extra work on read misses. Middle/High/Ultra keep the same DTO layout and can spend saved hot-path work on presentation systems without changing gameplay truth ownership.
Hardware Impact: Removes telemetry write attempts and counter mutation from encrypted-fragment read miss paths. Expected gain is small per call, but it closes a defragmentation safety hole on i3/MX350-class hardware.

## Decision 008 - ARM64 DTO Layout Proof

Problem: The migrated DataVault rows must not hide ARM64 alignment faults behind C# struct declarations.
Solution: Audited FieldOffset maps for AudioLogVaultTelemetryEntry, PrologueSequenceTelemetryEntry, QAEnduranceBlackBoxEntry, and nested AbsoluteUniversePosition. Wrote `Docs/Reports/VAULT_ARM64_LAYOUT_REPORT_X_000.md`.
Rejected Alternatives: Trusting `[StructLayout]` without an offset table was rejected. Using sequential layout was rejected for forensic DTO rows because explicit layout is cheaper to verify and safer for binary dumps.
Scalability potential: Low/Middle/High/Ultra all share the same binary DTOs; quality scaling does not change row shape or save identity.
Hardware Impact: All rows are 8-byte-size multiples. All long/double/ulong fields sit on offsets divisible by 8, avoiding unaligned 8-byte access risk on ARM64/mobile-class processors.

## Decision 009 - MonoBehaviour Residual Truth

Problem: The codebase is not project-wide clean even after the scoped migrations.
Solution: Reran the Roslyn scanner across all `Assets/_Project/Scripts` and wrote the full MonoBehaviour residual map to `Docs/Reports/VAULT_MONOBEHAVIOUR_NATIVE_FIELD_AUDIT_X_000.json`.
Rejected Alternatives: Reporting only the three fixed files was rejected because the user's override specifically requested a paranoid project-wide scan.
Scalability potential: Low hardware still carries risk in the 694 remaining MonoBehaviour native aliases until those owners migrate. Middle/High/Ultra do not remove the risk because stale native aliases are correctness faults, not quality settings.
Hardware Impact: Current scoped pass removed five persistent aliases total. Residual ledger still records 2265 forbidden persistent candidates project-wide, including 694 MonoBehaviour candidates, so full memory-sovereignty work remains incomplete.

## Decision 010 - Project-Wide DataVault Cold Cache Rename

Problem: A follow-up name-purity scan found additional private `ResolveDataVaultCold` methods outside the three migrated files. The name implies a pure resolver, but the implementation caches `GlobalRegistry.DataVault` into `_dataVault`.
Solution: Mechanically renamed the remaining private methods and local call sites to `CacheDataVaultCold` in KineticCharacterAnimatorRuntime, VocalWarningSystem, SpectrumSystem, and TopographicalSonarSynthesizer. A project-wide `rg` scan now finds no `ResolveDataVaultCold` under `Assets/_Project/Scripts`.
Rejected Alternatives: Rewriting broader `TryResolve*` mutable-view APIs in this pass was rejected because those routes are owner-specific DataVault view acquisition contracts, not safe mechanical renames.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged; this is route-name hygiene that prevents future agents from treating cold mutable cache paths as pure accessors.
Hardware Impact: 0 us runtime gain expected. The benefit is architectural: fewer misclassified accessor paths during future defragmentation audits.

## Decision 011 - Compile Blocker Fixes Exposed By Final Build

Problem: Final compile attempts surfaced unrelated current-worktree errors: ambiguous `AcousticEchoTap` in AcousticEchoLocationRuntime and missing scalability listener methods in ProceduralWreckGenerator.
Solution: Fully qualified `Hecton8.Core.Contracts.AcousticEchoTap` in the sensory hydration route because the method reads the core 144-byte contract fields. Added the missing `IScalabilityChangedEventListener` interface, registration guard, helpers, and `OnScalabilityChanged` hook to ProceduralWreckGenerator using the established `ScalabilityEvents` pattern.
Rejected Alternatives: Reporting success with compiler errors was rejected. Reverting unrelated work was rejected because these files were dirty from the shared worktree and the required fixes were minimal and local.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged except ProceduralWreckGenerator now receives the scalability change event that its existing calls already expected.
Hardware Impact: 0 us expected steady-state gain. The fixes remove build failure and preserve continuous quality-weight driven generation scaling.

## Decision 012 - Orbital Reentry VFX Ring Migration

Problem: `OrbitalDropReentryVfxController` still held `NativeArray<ReentryVfxTelemetryEntry> _telemetry` as a persistent MonoBehaviour field after the first scoped report.
Solution: Added `BufferID.OrbitalDropReentryVfxTelemetryRing = 74010` and replaced the persistent array with `VaultGenerationHandle<ReentryVfxTelemetryEntry>`. Recording now acquires one DataVault writer fence, writes one 48-byte row, and releases in `finally`; dump reads a scoped read-only view only during binary export.
Rejected Alternatives: Leaving the VFX telemetry ring as a private native field was rejected because black-box data still crosses frame phases and must tolerate relocation. Replacing it with managed lists was rejected because the crash dump route needs fixed-size unmanaged rows.
Scalability potential: Low keeps the same 300-row forensic window with no visual simulation expansion. Middle/High/Ultra can raise VFX fidelity through existing quality-weight paths without changing telemetry ownership or DTO layout.
Hardware Impact: Removes one persistent native alias from a MonoBehaviour. Vault payload is 14400 bytes plus metadata. Hot record cost is one writer-fence attempt plus one 48-byte row write; no profiler microsecond sample is available from shell.

## Decision 013 - Sargassum Cut Command Buffer Migration

Problem: `SargassumCutManager` held two persistent MonoBehaviour arrays for GPU stamp command staging: `NativeArray<StampCommand>` and `NativeArray<DamageVolumeStampCommand>`.
Solution: Added `BufferID.SargassumCutStampCommands = 74300` and `BufferID.SargassumCutDamageVolumeStampCommands = 74301`, replaced the fields with `VaultGenerationHandle` descriptors, and scoped mutable views to enqueue/upload methods. `StampCommand` and `DamageVolumeStampCommand` now use explicit 16-byte and 32-byte layouts.
Rejected Alternatives: Keeping local native staging fields was rejected because the manager survives across phases and defeats DataVault relocation checks. Moving upload staging to managed arrays was rejected because `GraphicsBufferUploadUtility.UploadNativeArray` requires native views and the GPU path must remain alloc-free.
Scalability potential: Low keeps capacities at 16 command rows and avoids new simulation. Middle/High/Ultra can spend saved ownership certainty on denser visual cutting and damage-volume presentation without changing command DTO shape.
Hardware Impact: Removes two persistent native aliases from a MonoBehaviour. Vault payload is 256 bytes for stamp commands plus 512 bytes for damage-volume commands, excluding metadata. Upload paths now hold short writer-fenced native views only during method scope.

## Decision 014 - Final Residual Truth After Five Migrated Files

Problem: The project still contains many hidden native collection fields after five scoped migrations; declaring global cleanliness would be false.
Solution: Reran the full Roslyn audit and regenerated the MonoBehaviour residual report. Final scoped state is 2373 files, 0 parse failures, 7766 native fields, 2262 forbidden persistent candidates, and 691 MonoBehaviour candidates across 76 files.
Rejected Alternatives: Reporting only the newly clean files was rejected because the user requested a paranoid scan of every `Assets/_Project/Scripts` file. Bulk-editing the largest world/physics owners without dependency maps was rejected because those systems have job fan-out and owner-route risk.
Scalability potential: Low still carries correctness risk in residual owners until migrated. Middle/High/Ultra do not mask this because stale native aliases are architecture faults, not quality-tier tradeoffs.
Hardware Impact: The current pass removed three more persistent native aliases and verified five migrated files at zero findings. Residual project-wide count remains too high for a memory-sovereignty completion claim.

## Decision 015 - DebrisManager Front/Back State Vault Migration

Problem: `DebrisManager` still held two persistent `NativeArray<DebrisChunkState>` fields and could mutate front state during an active simulation job through origin-shift or burst flush paths.
Solution: Added `SystemID.GameplayDebris` with front/back `BufferID` rows, replaced both arrays with `VaultGenerationHandle<DebrisChunkState>`, resolved method-local native views for simulation/render/write scopes, and locked both DataVault buffers while `DebrisSimulationJob` owns them.
Rejected Alternatives: Keeping local front/back arrays was rejected because they survive across phase boundaries. Blindly resolving mutable views without buffer locks was rejected because it would preserve the old write-while-job-read hazard. Passing vault handles into Burst jobs was rejected because jobs must receive native views, not owner abstractions.
Scalability potential: Low keeps the same 192-row debris pool and cheapest math path. Middle/High/Ultra can raise debris visual density through existing authored capacities/quality weights without changing DTO layout, owner route, or gameplay truth.
Hardware Impact: Removes two more persistent native aliases from a MonoBehaviour. Vault payload is 2 * 192 * 120 = 46,080 bytes plus metadata. The race fix trades a possible one-frame origin-shift/burst delay for deterministic buffer ownership on i3/MX350-class hardware.

## Decision 016 - DebrisChunkState ARM64 Layout

Problem: Moving debris state behind DataVault requires a binary row whose layout is stable on ARM64 and safe for relocation/dump tooling.
Solution: Converted `DebrisChunkState` to `[StructLayout(LayoutKind.Explicit, Size = 120)]` with all float/vector/byte fields at fixed offsets and a 4-byte `_pad0` reserve at offset 116. The row size is divisible by 8 and contains no double, long, or ulong fields.
Rejected Alternatives: Sequential layout was rejected because field reordering or compiler/runtime differences would make the report unverifiable. Shrinking the row without an explicit reserve was rejected because future tail fields would risk silent ABI drift.
Scalability potential: Low/Middle/High/Ultra share one row shape. Quality scaling can adjust spawn density/cadence, not DTO layout or authority ownership.
Hardware Impact: 120 % 8 = 0, so buffer stride is 8-byte clean. No 8-byte scalar field can be misaligned because none exist in this DTO.

## Decision 017 - Compile Gate Dependency Repairs

Problem: After the Debris migration and duplicate `SdfSqueezeJob` cleanup, the gated build exposed current-worktree namespace/include errors outside the Debris file.
Solution: Added the missing explicit project include for `KinematicStateContract.cs` and `KinematicCcdMath.cs`, removed the duplicate `SdfSqueezeJob.cs` include already supplied by `Directory.Build.targets`, added missing physics namespaces/aliases, and fully qualified ambiguous signal/physics types where the compiler could not infer the route.
Rejected Alternatives: Reporting a successful memory migration with compiler errors was rejected. Reverting other agents' files was rejected because the worktree is shared and the fixes were narrow compile-route repairs.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged. The value is build integrity: the project reaches DLL output with 0 warnings and 0 errors.
Hardware Impact: 0 us steady-state performance gain. The fixes preserve deterministic build validation so future memory-owner migrations are not hidden behind unrelated compile failures.

## Decision 018 - UI/Visor Black-Box Ring Owner Slice

Problem: Three small presentation MonoBehaviours still held persistent black-box `NativeArray` fields: internal flood waterline, diegetic visor HUD mesh, and diegetic tooltip renderer.
Solution: Added three UI-owned BufferIDs and replaced each persistent array with a `VaultGenerationHandle`. Record paths now acquire scoped DataVault writer fences; dump paths resolve read-only views only for cold binary export.
Rejected Alternatives: Targeting `SpatialAudioManager`, `PlayerInventory`, or large world/physics owners was rejected for this loop because they have broad job fan-out and authority routes. Leaving UI black boxes local was rejected because black-box rings still cross frame phases and must be relocatable.
Scalability potential: Low keeps bounded 300-row rings and current visual cost. Middle/High/Ultra can raise presentation quality through existing `GlobalQualityWeight` paths without changing DTO layout or ownership.
Hardware Impact: Removes three persistent native aliases from MonoBehaviours. Vault payload is 300 * 40 + 300 * 40 + 300 * 32 = 33,600 bytes plus metadata. No per-frame GC introduced.

## Decision 019 - UI/Visor DTO Layout Proof

Problem: The new UI/visor DataVault rows must prove ARM64-safe byte layout instead of relying on existing declarations.
Solution: Audited `WaterlineTelemetryEntry` 40 bytes, `DiegeticHudTelemetryEntry` 40 bytes, and `TooltipBlackBoxEntry` 32 bytes. Added full offset maps to `VAULT_ARM64_LAYOUT_REPORT_X_000.md`.
Rejected Alternatives: Treating these as harmless UI telemetry was rejected because they are still NativeArray/DataVault rows and participate in relocation and dump paths.
Scalability potential: Low/Middle/High/Ultra share identical DTO rows. Quality changes affect cadence/visual presentation, not binary row shape.
Hardware Impact: All three row sizes are divisible by 8. None contains double, long, or ulong fields, so no 8-byte scalar can be misaligned.

## Decision 020 - UI/Visor Read-Purity Gate

Problem: Moving UI black boxes to DataVault could accidentally place lazy allocation behind read or dump paths.
Solution: Kept allocation in `EnsureNativeTelemetry` / `EnsureBlackBox` cold setup only. `DumpBlackBox*` paths use read-only views and never call ensure/regenerate. `Record*` paths acquire writer fences and release in `finally`.
Rejected Alternatives: Using `TryResolve*` mutable views without writer fences was rejected. Letting dump paths lazily create missing rings was rejected because read/fault export must not mutate DataVault ownership.
Scalability potential: Low avoids hidden work during UI dump/read failures. Middle/High/Ultra can spend cycles on presentation overkill without changing the memory route.
Hardware Impact: Removes three long-lived native aliases from presentation MonoBehaviours and bounds DataVault interaction to one row write per active frame for each owner.

## Decision 021 - HUDNotification Queue Vault Migration

Problem: `HUDNotification` still held `NativeArray<NotificationRequest> _queue` as a persistent MonoBehaviour field after the UI/visor black-box slice.
Solution: Added `BufferID.HudNotificationQueue = 74315`, replaced the persistent array with `VaultGenerationHandle<NotificationRequest>`, and scoped all queue mutations through `IDataVault.TryAcquireWriteLock` with `finally` release.
Rejected Alternatives: Migrating `PhysicalToolGripOffsets` first was rejected because that prefab-owned state can have multiple instances and one shared BufferID would collide without a per-instance route card. Keeping the HUD queue as a local array was rejected because the component is active runtime UI state and participates in relocation safety.
Scalability potential: Low keeps an 8-row notification queue with no visual expansion. Middle/High/Ultra can spend UI budget on richer presentation/fade styling without changing queue DTO layout or ownership.
Hardware Impact: Removes one persistent native alias from a MonoBehaviour. Vault payload is 8 * 8 = 64 bytes plus metadata. Queue mutation adds a short writer-fence attempt only when notifications are enqueued/dequeued.

## Decision 022 - NotificationRequest ARM64 Layout

Problem: Moving the notification queue into DataVault requires proving the queue row is stride-safe on ARM64 rather than relying on implicit struct padding.
Solution: Converted `NotificationRequest` to `[StructLayout(LayoutKind.Explicit, Size = 8)]` with `MessageHash` at offset 0, `Severity` at offset 4, `_pad0` at offset 5, and `_pad1` at offset 6. Added the offset table to `VAULT_ARM64_LAYOUT_REPORT_X_000.md`.
Rejected Alternatives: Leaving sequential layout was rejected because the audit request requires a visible padding map. Shrinking to a 5-byte packed row was rejected because runtime packed DTO rows are forbidden and would produce poor stride alignment.
Scalability potential: Low/Middle/High/Ultra share the same 8-byte row. Quality scaling may change notification cadence or presentation cost, not binary row shape.
Hardware Impact: 8 % 8 = 0. The row contains no double, long, or ulong fields, so no 8-byte scalar can be misaligned on ARM64/mobile silicon.

## Decision 023 - Residual Truth After Ten Migrated Files

Problem: The codebase is still not globally clean after the HUD queue migration; overstating completion would hide hundreds of persistent MonoBehaviour aliases.
Solution: Reran the full Roslyn audit and regenerated the MonoBehaviour residual report and exorcism report. Current proof state is 2375 files, 0 parse failures, 7760 native fields, 2256 forbidden persistent candidates, and 685 MonoBehaviour candidates across 71 files.
Rejected Alternatives: Declaring project-wide purity because the current target file is clean was rejected. Editing multi-instance or broad job-fan-out owners without route cards was rejected because it could introduce ownership collisions or Burst dependency regressions.
Scalability potential: Low still carries correctness risk in residual owners until migrated. Middle/High/Ultra do not mask stale native aliases because this is memory ownership correctness, not a quality tier.
Hardware Impact: The current pass removes one additional persistent native alias and verifies ten migrated files at zero scoped findings. Residual project-wide count remains too high for a completion claim.

## Decision 024 - HectonVoxelEngine Black-Box Vault Migration

Problem: `HectonVoxelEngine` still held static `NativeArray<VoxelMeshPipelineTelemetryEntry> _voxelMeshPipelineBlackBox` inside a MonoBehaviour owner, creating a persistent native alias across frame phases.
Solution: Added `BufferID.VoxelMeshPipelineBlackBox = 74316` under `SystemID.WorldStreaming`, replaced the static native array with `VaultGenerationHandle<VoxelMeshPipelineTelemetryEntry>`, and scoped writer/read-only views to black-box write/dump methods.
Rejected Alternatives: Migrating all voxel scratch state in the same pass was rejected because `MCTables` and `VoxelPipelineData` own table/scratch allocations with job fan-out and require separate route cards. Leaving the black-box local was rejected because it is a 300-frame forensic ring and must survive relocation safely.
Scalability potential: Low keeps the same 300-row forensic window. Middle/High/Ultra can spend saved certainty on richer voxel presentation, not on changing telemetry row layout or gameplay truth.
Hardware Impact: Removes one MonoBehaviour persistent native alias. Vault payload is 300 * 32 = 9600 bytes plus metadata; steady-state cost is one short writer-fence attempt per voxel mesh telemetry publication.

## Decision 025 - VoxelMeshPipelineTelemetryEntry ARM64 And Read Purity

Problem: Moving the voxel mesh ring into DataVault requires proving the DTO layout and preventing cold allocation from leaking into read/fault dump paths.
Solution: Verified `VoxelMeshPipelineTelemetryEntry` as explicit 32-byte layout with offsets 0,4,8,10,12,14,16,18,20,24,28. `DumpVoxelMeshPipelineBlackBox` now uses `TryReadOnlyHandle` and never calls the ensure path; `WriteVoxelMeshPipelineBlackBoxSample` acquires/release a writer fence in `finally`.
Rejected Alternatives: Sequential layout trust was rejected because the audit requires a byte map. Calling `EnsureVoxelMeshPipelineBlackBox` from dump was rejected because fault export must not regenerate buffers.
Scalability potential: Low/Middle/High/Ultra share the same 32-byte row. Quality scaling can change voxel mesh cadence or visual density, not the telemetry ABI.
Hardware Impact: 32 % 8 = 0. No 8-byte scalar fields exist, so there is no ARM64 unaligned double/long/ulong risk.

## Decision 026 - Residual Truth After Eleven Persistent Alias Removals

Problem: The project remains unclean after the voxel black-box migration; overstating completion would hide 684 MonoBehaviour persistent aliases.
Solution: Reran the full Roslyn audit and regenerated the MonoBehaviour residual report and exorcism report. Current proof state is 2375 files, 0 parse failures, 7759 native fields, 2255 forbidden persistent candidates, and 684 MonoBehaviour candidates across 70 files.
Rejected Alternatives: Marking `HectonVoxelEngine.cs` file-clean was rejected because the file still has 50 non-MonoBehaviour persistent candidates in `MCTables` and voxel scratch owner structs. Bulk-editing those in this pass was rejected because they are not the direct MonoBehaviour field just migrated.
Scalability potential: Low still carries correctness risk in residual owners until migrated. Middle/High/Ultra do not mask stale native aliases because this is memory ownership correctness, not quality scaling.
Hardware Impact: This pass removes one additional persistent native alias and verifies compiler/audit cleanliness for the touched owner slice. Residual project-wide count remains too high for a completion claim.

## Decision 027 - LoreDatabase Unlock Word Vault Migration

Problem: `LoreDatabaseManager` held persistent `NativeArray<uint> _unlockedWords` as a MonoBehaviour field and exposed it through a read-looking `TryGetPackedUnlockWords` route.
Solution: Added `SystemID.LoreDatabase` and `BufferID.LoreDatabaseUnlockedWords`, replaced the field with `VaultGenerationHandle<uint>`, and scoped all mutable access to writer-fenced unlock/load paths. `OnDestroy` and DataVault hot-swap release the descriptor through the vault.
Rejected Alternatives: Leaving a two-word array local was rejected because small persistent native aliases still break relocation sovereignty. Moving unlock words into managed arrays was rejected because the UI read model expects a native read-only view and the save path needs fixed bit packing.
Scalability potential: Low keeps the 64-bit unlock mask with no UI expansion. Middle/High/Ultra can present richer lore surfaces without changing save identity, DTO layout, or memory ownership.
Hardware Impact: Removes one persistent native alias from a MonoBehaviour. Vault payload is 2 * 4 = 8 bytes plus metadata; no per-frame GC introduced.

## Decision 028 - LoreDatabase Read Purity And Primitive Layout

Problem: `TryGetPackedUnlockWords` lazily allocated native storage and `TryGetRecordIndex` lazily rebuilt the lookup dictionary, violating the read accessor doctrine under the user's override.
Solution: `TryGetPackedUnlockWords` now performs read-only handle validation only; it fails closed when the vault/handle is unavailable. `TryGetRecordIndex` returns false if the cold lookup has not been built. `BuildRecordLookupCold` runs in `Awake` and write paths, not in read accessors. ARM64 proof records the primitive unlock payload as two uint words, 8 bytes total.
Rejected Alternatives: Keeping lazy creation because the data is small was rejected. Padding the two uint words into a new DTO was rejected because it would change the existing API and save packing without adding alignment value.
Scalability potential: Low/Middle/High/Ultra share one two-word payload. Quality scaling can affect lore presentation cadence, not save identity or unlock truth.
Hardware Impact: Removes hidden allocation/regeneration from UI read paths. Payload total is 8-byte clean and contains no double, long, or ulong field.

## Decision 029 - Compile Blocker Repair And Residual Truth After Twelve Alias Removals

Problem: The gated build after the Lore migration exposed an unrelated compile blocker in `PDADecryptionSpectrogramPanel.cs`: unqualified `ToolHapticsRuntime` did not resolve in the current generated project context.
Solution: Fully qualified the existing call as `Hecton8.Tools.ToolHapticsRuntime.EnqueueSinusoidalCommand`. Final gated build completed with 0 warnings and 0 errors. Full Roslyn audit now reports 2378 files, 0 parse failures, 2252 forbidden persistent candidates, and 683 MonoBehaviour candidates across 69 files, hash `1923e614ac7170e17cdc137caf69ca6f6b68ae6386a84c1cb24b13a4f13eacdd`.
Rejected Alternatives: Ignoring the compile error was rejected. Rewriting haptic routing was rejected because the compile blocker was namespace resolution, not the X_000 memory-ownership target.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged for the haptic route. Lore read purity improves all tiers equally by avoiding hidden work in UI read paths.
Hardware Impact: Compiler repair has 0 us expected steady-state gain. The memory slice removes one persistent native alias and one lazy read allocation route; residual project-wide count remains too high for a completion claim.

## Decision 030 - Headless Stress Fracture QA Vault Migration

Problem: `HeadlessStressFractureBot` held two persistent MonoBehaviour native aliases: `NativeArray<FractureTelemetryEntry> _blackbox` and `NativeArray<byte> _scratchBlock`. The scratch block also used `H8Memory.Allocate<byte>` as a local persistent owner.
Solution: Added `SystemID.QAHeadless`, `BufferID.QAHeadlessStressFractureBlackBoxRing`, and `BufferID.QAHeadlessStressFractureScratchBlock`. Replaced both fields with `VaultGenerationHandle` descriptors. `RecordBlackbox` now writes through `IDataVault.TryAcquireWriteLock`; dump paths use a scoped read-only view.
Rejected Alternatives: Leaving QA-only native arrays local was rejected because the override asks for every MonoBehaviour, including test harnesses. Moving the scratch block to managed `byte[]` was rejected because the bot intentionally stresses native allocation accounting, not managed heap behavior.
Scalability potential: Low keeps the 300-frame forensic ring and default 50 MB scratch pressure. Middle/High/Ultra can raise QA pressure through the existing command-line/env scratch MB knob up to 256 MB without changing DTO layout or authority route.
Hardware Impact: Removes two persistent native aliases from a MonoBehaviour. Vault payload is 19,200 bytes for the black-box ring plus an explicit 8..256 MB scratch pressure buffer. No profiler microsecond claim is made from shell-only verification.

## Decision 031 - FractureTelemetryEntry ARM64 Layout

Problem: Moving the stress-fracture black-box ring into DataVault requires proving that its 64-byte row is ARM64-safe, especially the two 8-byte `long` counters.
Solution: Verified `[StructLayout(LayoutKind.Explicit, Size = 64)]`: `NativeBytes` is at offset 16 and `H8Bytes` is at offset 24, both divisible by 8. The remaining fields are 4-byte scalar lanes or a `float3` at offset 48. Added the full map to `VAULT_ARM64_LAYOUT_REPORT_X_000.md`.
Rejected Alternatives: Trusting the existing explicit declaration without a map was rejected because the user requested padding evidence. Repacking the row was rejected because the current binary dump format already matches the constants and is 8-byte clean.
Scalability potential: Low/Middle/High/Ultra share the same row. Quality or QA intensity changes may alter sampling cadence or scratch pressure, not row shape.
Hardware Impact: 64 % 8 = 0. The only 8-byte fields are aligned at 16 and 24; no double or ulong fields exist. The byte scratch payload is always integer megabytes, therefore total bytes are divisible by 8.

## Decision 032 - Residual Truth After Headless Stress Slice

Problem: The codebase remains globally unclean after the headless QA slice; claiming completion would hide 679 residual MonoBehaviour native aliases.
Solution: Reran the full Roslyn audit and regenerated the MonoBehaviour residual and exorcism reports. Current proof state is 2379 files, 0 parse failures, 7755 native fields, 2248 forbidden persistent candidates, and 679 MonoBehaviour candidates across 68 files, hash `360438e0a6efe9d5fa73b1c6755cf31805a5bf20a9048f8f397cffbd45bf82d8`.
Rejected Alternatives: Bulk-editing `PlayerInventory`, `HectonFluidEngine`, or `GasDynamicsSolver` in the same loop was rejected because those owners have large job fan-out and require route cards. Declaring the project clean because the current target is clean was rejected.
Scalability potential: Low still carries residual correctness risk until the large owners are migrated. Middle/High/Ultra do not hide this; stale native aliases are memory ownership defects, not visual quality decisions.
Hardware Impact: This pass removes two additional persistent aliases and keeps the build at 0 warnings / 0 errors. Residual cleanup remains required before any project-wide memory-sovereignty claim.

Status: BUILD CLEAN / HEADLESS STRESS FRACTURE VAULT MIGRATION COMPLETE / PROJECT-WIDE PURGE INCOMPLETE

## Decision 033 - InstanceCullingService GPU Readback Vault Migration

Problem: `InstanceCullingService` still held two persistent MonoBehaviour native aliases: `NativeArray<uint> _indirectArgsReadback` for delayed GPU indirect-args readback and `NativeArray<InstanceCullingTelemetryEntry> _telemetryRing` for the black-box ring.
Solution: Added `BufferID.InstanceCullingIndirectArgsReadback` and `BufferID.InstanceCullingTelemetryRing` under existing `SystemID.GraphicsScalability`, replaced both fields with `VaultGenerationHandle` descriptors, and scoped native views to setup, callback, write, dump, and read-only methods. The readback path holds a DataVault writer fence while `AsyncGPUReadback` owns the native pointer and releases it in callback/teardown.
Rejected Alternatives: Leaving the readback array local was rejected because GPU callbacks can outlive the frame that acquired the native view. Moving telemetry to a managed queue was rejected because the black-box mandate requires fixed-size unmanaged rows. Using a new SystemID was rejected because graphics scalability already owns this culling service route.
Scalability potential: Low keeps the same readback cadence and 300-row forensic window. Middle/High/Ultra can increase visual density/culling fidelity through existing quality-weight and GPU budgets without changing DTO layout or authority route.
Hardware Impact: Removes two persistent native aliases from a MonoBehaviour. Vault payload is 20 bytes of primitive indirect-args scratch plus 300 * 64 = 19,200 bytes telemetry, excluding metadata. No profiler microsecond sample is claimed from shell; expected GC delta is zero.

## Decision 034 - InstanceCulling ARM64 Layout And Read Purity

Problem: The new culling telemetry DTO needed explicit ARM64 padding proof, and the GPU readback route could not hide allocation/regeneration behind `TryRead*` helpers.
Solution: Verified `InstanceCullingTelemetryEntry` as explicit 64-byte layout with `ulong` reserve lanes at offsets 40, 48, and 56. Added read-purity proof: `TryReadIndirectArgsReadback` and `TryReadTelemetryRing` use read-only handle resolution only; `TryRequestTelemetryReadback` is explicitly a mutation/request path, not a read accessor.
Rejected Alternatives: Treating a graphics telemetry row as exempt UI/debug data was rejected because the DataVault relocation rules apply to every persistent native owner. Padding the primitive `uint[5]` indirect-args payload into a fake DTO was rejected because it would misrepresent the GPU indirect args ABI; it has no 8-byte scalar alignment risk.
Scalability potential: Low/Middle/High/Ultra share the same 64-byte telemetry row. Quality scaling affects instance count/cull cadence, not row shape or save identity.
Hardware Impact: `InstanceCullingTelemetryEntry` size is 64 and divisible by 8. The only 8-byte fields are explicit `ulong` padding at aligned offsets. The primitive readback payload contains no double, long, or ulong fields.

## Decision 035 - Parse Failure And Signal Sanitizer Compile Repairs

Problem: The post-migration audit found a real Roslyn parse failure in `HeadlessStressFractureBot.cs` from an extra brace, and the build exposed unqualified `SanitizeFinite` calls in `GlobalSignalPayloads.CoreFoundation.cs`.
Solution: Removed the stray brace and qualified the audio DTO constructor finite sanitization calls through `SignalPayloadSanitizer`. Final audit reports 0 parse failures and final gated build reports 0 warnings / 0 errors.
Rejected Alternatives: Ignoring parser failure because the previous build was green was rejected; Roslyn audit is a required proof channel. Adding duplicate local sanitizer methods to the DTOs was rejected because a namespace-local helper already exists.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. This is build and audit integrity repair, not runtime feature work.
Hardware Impact: 0 us expected runtime gain. The benefit is evidence quality: the parser and compiler now agree on a clean source state.

## Decision 036 - Residual Truth After Instance Culling Slice

Problem: The project still contains hundreds of MonoBehaviour native aliases after the culling slice; declaring completion would be false.
Solution: Reran the full Roslyn audit and regenerated the residual reports. Current proof state is 2383 files, 0 parse failures, 7737 native fields, 2241 forbidden persistent candidates, and 675 MonoBehaviour candidates, hash `ef7e3db1164bdfa36b350658c8e843cc2b3d7e92c6b59d49ab4bf6b90f950d79`.
Rejected Alternatives: Bulk-editing `PlayerInventory`, `HectonFluidEngine`, or `WorldChunkResidencyManager` in the same loop was rejected because those owners have large job fan-out and route-card risk. Reporting only the clean `InstanceCullingService` file was rejected because the override requires project-wide residual truth.
Scalability potential: Low still carries residual correctness risk until large owners are migrated. Middle/High/Ultra do not hide stale native aliases because this is memory ownership correctness, not quality scaling.
Hardware Impact: This pass removes two additional persistent aliases and keeps build green. Residual cleanup remains required before any project-wide memory-sovereignty claim.

Status: BUILD CLEAN / INSTANCE CULLING VAULT MIGRATION COMPLETE / PROJECT-WIDE PURGE INCOMPLETE

## Decision 037 - TraumaDispatcher Parasite LOS Vault Migration

Problem: `TraumaDispatcher` held two persistent MonoBehaviour native aliases, `NativeArray<RaycastCommand> _parasiteSporeLosCommands` and `NativeArray<RaycastHit> _parasiteSporeLosHits`, for the parasite-spore line-of-sight raycast batch.
Solution: Added `BufferID.TraumaDispatcherParasiteSporeLosCommands` and `BufferID.TraumaDispatcherParasiteSporeLosHits` under existing `SystemID.GameplayPlayer`, replaced both fields with `VaultGenerationHandle` descriptors, and scoped native views to the frame-local raycast scheduling/completion window.
Rejected Alternatives: Leaving the capacity-one arrays local was rejected because small persistent native aliases still violate relocation sovereignty. Replacing the raycast batch with `Physics.Raycast` was rejected because the existing dispatcher-owned batch route avoids direct synchronous physics calls in the trauma tick path.
Scalability potential: Low keeps one cheap LOS query and fails closed if the vault is unavailable. Middle/High/Ultra can increase parasite presentation/audio intensity through existing quality routes, but gameplay truth and buffer shape remain unchanged.
Hardware Impact: Removes two persistent native aliases from a player-side MonoBehaviour. Vault payload is two one-row Unity Physics buffers, excluding metadata. No profiler microsecond sample is claimed from shell-only verification.

## Decision 038 - TraumaDispatcher Read Purity And Job Lock Lifetime

Problem: The old `LateFrameTick` could lazily allocate native buffers if they were missing, and the scheduled raycast job needed a valid native pointer until completion.
Solution: Removed hot-path ensure from `LateFrameTick`; creation now happens only in cold `Awake`/`OnEnable` setup. `TryAcquireParasiteSporeLosWriteBuffers` acquires writer fences for command and hit buffers immediately before scheduling. `CompleteParasiteSporeLosQuery` releases writer fences after job completion and then reads the hit through `TryReadParasiteSporeLosHits`, which is read-only and fail-closed.
Rejected Alternatives: Holding a raw `NativeArray` field between frames was rejected. Completing the job from a read helper was rejected; completion remains in the explicit late-frame/teardown route.
Scalability potential: Low avoids hidden allocation/regeneration under spore hazard frames. Middle/High/Ultra retain deterministic player damage truth and can spend visual budget on parasite room effects without changing the raycast authority route.
Hardware Impact: Removes hot-path buffer regeneration risk and stale pointer retention between simulation phases. Expected GC delta is 0 B; actual frame microseconds require Unity profiler proof.

## Decision 039 - Trauma Residual Truth And Unity Physics ABI Boundary

Problem: The user requested ARM64 padding maps for new DTOs; the Trauma slice moved Unity Physics payloads but did not introduce a custom DTO row with X_000-owned field offsets.
Solution: Recorded the boundary in `VAULT_ARM64_LAYOUT_REPORT_X_000.md`: the two new buffers store `UnityEngine.RaycastCommand` and `UnityEngine.RaycastHit`, consumed by Unity's own `RaycastCommand.ScheduleBatch` ABI. X_000 does not claim a fake field-offset map for Unity-owned structs.
Rejected Alternatives: Wrapping Unity raycast structs in a custom padded DTO was rejected because `RaycastCommand.ScheduleBatch` requires `NativeArray<RaycastCommand>` and `NativeArray<RaycastHit>`. Faking a padding table from memory was rejected because the report must be objective.
Scalability potential: Low/Middle/High/Ultra share the same LOS ABI. GlobalQualityWeight can affect parasite VFX/audio cadence elsewhere, not the raycast result truth.
Hardware Impact: No custom double/long/ulong fields were introduced. Full Roslyn audit after this slice reports 2390 files, 0 parse failures, 2248 forbidden persistent candidates, and 682 MonoBehaviour candidates; project-wide purge remains incomplete.

Status: BUILD CLEAN / RAYCAST BATCH HELPER VAULT MIGRATION COMPLETE / PROJECT-WIDE PURGE INCOMPLETE

## Decision 040 - RaycastBatchHelper Batch Buffer Vault Migration

Problem: `RaycastBatchHelper` held two persistent MonoBehaviour native aliases, `NativeArray<RaycastCommand> _commands` and `NativeArray<RaycastHit> _hits`, for a shared 512-query batch raycast service.
Solution: Added `BufferID.RaycastBatchHelperCommands` and `BufferID.RaycastBatchHelperHits` under existing `SystemID.Physics`, replaced both fields with `VaultGenerationHandle` descriptors, and scoped native views to command write, scheduled job ownership, and read-only result/gizmo inspection.
Rejected Alternatives: Leaving the arrays local was rejected because this singleton survives across phases and is exactly the kind of stale pointer carrier the override targets. Replacing the batch with synchronous `Physics.Raycast` was rejected because it would move work back to gameplay hot paths and discard the existing job-batched route.
Scalability potential: Low keeps the same 512 hard cap and fails closed when the vault is unavailable. Middle/High/Ultra can raise caller-side query density only through explicit capacities and budgets; buffer ownership and gameplay truth stay unchanged.
Hardware Impact: Removes two persistent native aliases from a physics-side MonoBehaviour. Vault payload is two 512-row Unity Physics buffers plus metadata. No profiler microsecond sample is claimed from shell-only verification.

## Decision 041 - RaycastBatchHelper Read Purity And Job Lock Lifetime

Problem: A scheduled raycast job needs valid command/hit native pointers until completion, while read accessors must not allocate, regenerate buffers, or complete jobs.
Solution: `AddQuery` writes a single command through a short writer fence. `ExecuteBatch` acquires command/hit writer fences immediately before `RaycastCommand.ScheduleBatch` and keeps them locked until `TryConsumeScheduledBatch` or teardown completes the job. `GetResult`, `QueryCount`, `WasExecuted`, `HeartbeatState`, `TryReadRaycastCommands`, and `TryReadRaycastHits` are pure/scalar/read-only paths and never call `EnsureRaycastBuffer`.
Rejected Alternatives: Holding raw `NativeArray` fields between frames was rejected. Completing jobs inside `GetResult` was rejected because it would hide synchronization behind a read accessor.
Scalability potential: Low avoids hidden stalls and lazy allocation in query result reads. Middle/High/Ultra preserve deterministic physics query publication while spending visual budget elsewhere.
Hardware Impact: Removes stale pointer retention between simulation phases and blocks hidden same-frame readback stalls in result accessors. Expected GC delta is 0 B; actual frame microseconds require Unity profiler proof.

## Decision 042 - Raycast Residual Truth And Unity Physics ABI Boundary

Problem: The user requested ARM64 padding maps for new DTOs, but the RaycastBatchHelper slice moves Unity Physics payloads rather than creating an X_000-owned DTO row.
Solution: Recorded the boundary in `VAULT_ARM64_LAYOUT_REPORT_X_000.md`: the buffers store `UnityEngine.RaycastCommand` and `UnityEngine.RaycastHit`, required by `RaycastCommand.ScheduleBatch`. X_000 does not claim a fake field-offset map for Unity-owned structs.
Rejected Alternatives: Wrapping Unity raycast structs in custom padded DTOs was rejected because the Unity API requires exact `NativeArray<RaycastCommand>` and `NativeArray<RaycastHit>`. Faking a padding table from memory was rejected because reports must be objective.
Scalability potential: Low/Middle/High/Ultra share the same Unity Physics ABI. GlobalQualityWeight can affect who requests raycasts or visual response fidelity, not the DataVault payload type.
Hardware Impact: No custom double/long/ulong field was introduced. Full Roslyn audit after this slice reports 2390 files, 0 parse failures, 2173 forbidden persistent candidates, and 680 MonoBehaviour candidates; project-wide purge remains incomplete.

## Decision 043 - Compile Blockers Exposed During Raycast Verification

Problem: The Raycast build verification exposed unrelated current-worktree compile blockers: a preprocessor directive attached to a closing brace in `GlobalSignals.RuntimeLifecycle.cs`, and editor signal injection still writing removed `MockPlayerFootstepSignal.SurfaceName`.
Solution: Split the preprocessor directive onto its own line and changed the editor mock injector to write `SurfaceHash` with `Animator.StringToHash`. Final gated build reports 0 warnings and 0 errors.
Rejected Alternatives: Ignoring editor-only compile failure was rejected because the build is the proof channel. Reintroducing a fixed string field into the signal DTO was rejected because the current 128-byte explicit layout already carries `SurfaceHash` and validated size.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged; the editor injector now targets the current signal contract.
Hardware Impact: 0 us expected runtime gain. The benefit is proof integrity: compiler and Roslyn scanner are both green after the memory slice.

Status: BUILD CLEAN / PHYSICAL TOOL GRIP AND DIEGETIC HUD MANUAL LAYOUT NATIVE ALIAS PURGE COMPLETE / PROJECT-WIDE PURGE INCOMPLETE

## Decision 044 - PhysicalToolGripOffsets Value-Field Cleanup

Problem: `PhysicalToolGripOffsets` held `NativeArray<float4x4> _gripOffsets` as persistent MonoBehaviour state for two authored grip offsets.
Solution: Removed the native collection and replaced it with two unmanaged `float4x4` value fields plus a scalar cache flag. `TryReadGripOffset` now returns one value field and never creates/grows a native buffer or retains a view.
Rejected Alternatives: DataVault migration was rejected because this is per-prefab-instance authored state; one global `BufferID` would merge independent tool instances. A managed array was rejected because two fixed value fields are simpler and avoid heap allocation.
Scalability potential: Low keeps the same two authored transforms with no native allocation. Middle/High/Ultra can use richer physical tool presentation elsewhere without changing this per-instance value route.
Hardware Impact: Removes one persistent native alias from a MonoBehaviour. Steady-state expected GC delta is 0 B; native allocation/disposal for the two-offset cache is eliminated.

## Decision 045 - DiegeticHudManualLayout Stack Value Rewrite

Problem: `DiegeticHudManualLayout` held persistent `_inputs` and `_outputs` native arrays for a small transform-layout task that did not justify retained native ownership.
Solution: Removed both arrays and rewrote `RebuildLayout` to compute each target position with stack/value `DiegeticHudLayoutInput` and `DiegeticHudLayoutSettings` rows. The MonoBehaviour now writes transforms directly and retains no native view between phases.
Rejected Alternatives: Moving the arrays into DataVault was rejected because this is local UI layout scratch, not cross-domain native ownership. Keeping a scheduled job was rejected because the work is tiny and the mandate rejects tiny jobs without profiler proof.
Scalability potential: Low avoids native allocation and scheduling overhead for simple HUD layout. Middle/High/Ultra can add richer HUD visuals through rendering/material routes without changing layout truth or DTO shape.
Hardware Impact: Removes two persistent native aliases from a MonoBehaviour. Eliminates native allocate/dispose pressure and one potential tiny-job/same-frame readback trap.

## Decision 046 - Latest ARM64 Layout Proof

Problem: The latest cleanup touched value/DTO data paths and the user required a padding map proving ARM64-safe layout.
Solution: Added `PhysicalToolGripOffsets` value cache proof and explicit maps for `DiegeticHudLayoutInput` and `DiegeticHudLayoutSettings` to `VAULT_ARM64_LAYOUT_REPORT_X_000.md`. Both Diegetic DTOs are 16 bytes; the physical value cache uses 64-byte `float4x4` fields.
Rejected Alternatives: Faking DataVault DTO rows for `float4x4` value fields was rejected because no DataVault row exists. Trusting sequential layout for Diegetic DTOs was rejected; explicit 16-byte layout remains verifiable.
Scalability potential: Low/Middle/High/Ultra share one layout shape. Quality settings can change HUD density or visibility, not DTO layout or authority route.
Hardware Impact: Every checked row/value size is divisible by 8. No double, long, or ulong field exists in the new value/DTO paths, so there is no misaligned 8-byte scalar risk.

## Decision 047 - Compile Repairs And Residual Truth After Twenty-Seven Alias Removals

Problem: Verification exposed current-worktree compile blockers and warnings outside the two cleanup files, and the project remains globally unclean.
Solution: Made minimal compile repairs only: `VoxelStreamingScratchLease` owner/slot fields were made accessible to the enclosing engine methods, `FaunaBrain` restored prey-brain locals in the attack branch, and unused catch variables were removed where no exception value is consumed. Final build is 0 warnings / 0 errors. Full Roslyn audit now reports 2390 files, 0 parse failures, 2233 forbidden persistent candidates, and 677 MonoBehaviour candidates across 63 files, hash `c7156675265f9b616938d414b92e95419fb09ebfc6199c06b12d9a1a9eb5e76d`.
Rejected Alternatives: Stopping at compile errors was rejected. Claiming project-wide completion was rejected because 677 MonoBehaviour native aliases remain. Reverting unrelated shared-worktree edits was rejected because the fixes were narrow and compatible with concurrent agents.
Scalability potential: Low still carries residual correctness risk until remaining owners are migrated. Middle/High/Ultra cannot mask stale native aliases because this is memory ownership correctness, not visual quality scaling.
Hardware Impact: The latest slice removes three more persistent aliases, bringing the X_000 targeted removal count to 27. Build proof is clean; residual cleanup must continue.

Status: BUILD CLEAN / FONT STREAMING PREFETCH VAULT MIGRATION COMPLETE / PROJECT-WIDE PURGE INCOMPLETE

## Decision 048 - FontStreamingManager Prefetch Vault Migration

Problem: `FontStreamingManager` held `NativeArray<uint> _visibleHashPrefetch` and `NativeArray<int2> _visibleSlicePrefetch` as persistent MonoBehaviour fields for visible-text prefetch scratch.
Solution: Added `BufferID.FontStreamingVisibleHashPrefetch = 74326` and `BufferID.FontStreamingVisibleSlicePrefetch = 74327` under existing `SystemID.UI`, replaced both arrays with `VaultGenerationHandle` descriptors, and scoped native views to collection/job ownership windows only.
Rejected Alternatives: Leaving the arrays local was rejected because font streaming survives across UI phases and the aliases can stale under DataVault relocation. Moving the scratch to managed arrays was rejected because `LocRegistry.TryScheduleVisibleTextOffsetPrefetch` consumes native views for the prefetch job path.
Scalability potential: Low can keep visible prefetch capacity small and fail closed when the vault is unavailable. Middle/High/Ultra can raise visible text density or prefetch cadence through existing capacity/quality controls without changing DTO layout or authority route.
Hardware Impact: Removes two persistent native aliases from a UI MonoBehaviour. Vault payload is one primitive `uint` hash buffer plus one `int2` slice buffer at visible prefetch capacity, excluding metadata. Expected GC delta is 0 B/frame; profiler microseconds are not available from shell.

## Decision 049 - Font Streaming Read Purity And Job Lock Lifetime

Problem: Visible prefetch result reads must not hide lazy buffer creation or job completion, and scheduled prefetch jobs must not outlive their native views.
Solution: `TryReadVisibleHashPrefetch` and `TryReadVisibleSlicePrefetch` validate exact handles and return read-only views only. `TryAcquireVisibleHashPrefetchWriteBuffer` and `TryAcquireVisiblePrefetchJobBuffers` are explicit mutation/job paths. Writer locks are released by `ReleaseVisiblePrefetchJobBufferLocks` after the prefetch job window closes.
Rejected Alternatives: Completing the prefetch job inside a `TryRead*` helper was rejected because read accessors must not synchronize hidden work. Reacquiring/growing buffers from read helpers was rejected because it breaks defragmentation purity.
Scalability potential: Low avoids allocation and hidden synchronization in UI read paths. Middle/High/Ultra retain deterministic localization/prefetch ownership while using saved budget for richer text density or animation.
Hardware Impact: Blocks stale native view retention between UI phases and removes hidden regeneration risk from read paths on i3/MX350-class hardware.

## Decision 050 - Compile Route Repair And Residual Truth After Twenty-Nine Alias Removals

Problem: Verification exposed a local dotnet proof-route conflict: stale `Library/ScriptAssemblies/Hecton8.Input.dll` carried an incompatible input interface identity after current bootstrap contracts were included.
Solution: For the local generated-project proof route, `Directory.Build.targets` now compiles current `InputBindingServiceContracts.cs`, `InputManager.cs`, and `UserOptionsPersistence.cs` into `Hecton8.Core` and references only `Hecton8.Input.Generated.dll` for generated input actions. Warning cleanup in `InputManager` uses `ReferenceEquals`. Final build is 0 warnings / 0 errors.
Rejected Alternatives: Using `-p:HectonBuildProjectReferences=true` was rejected for this worktree because it pulled a different construction/combat graph and produced 15 errors plus thousands of warnings. Claiming success with the stale input DLL conflict was rejected.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged; this is build proof integrity, not gameplay logic.
Hardware Impact: 0 us expected runtime gain. Full Roslyn audit now reports 2398 files, 0 parse failures, 2234 forbidden persistent candidates, and 671 MonoBehaviour candidates across 62 files, hash `b2a2d0e9af041616dbcba8004e5e81476593942c8417c9c8d69b6943956eb99d`. X_000 targeted removals now total 29 persistent native aliases; project-wide purge remains incomplete.

Status: BUILD CLEAN / VEHICLE SUB OS AND FAKE RADAR NATIVE PURGE COMPLETE / PROJECT-WIDE PURGE INCOMPLETE

## Decision 051 - VehicleSubOsCockpitRuntime Vault Migration

Problem: `VehicleSubOsCockpitRuntime` retained seven persistent MonoBehaviour native arrays for cockpit button state, kinematic button job input/output, base positions, matrices, and black-box telemetry.
Solution: Added UI-owned BufferIDs 74328..74334, replaced all seven fields with `VaultGenerationHandle` descriptors, and scoped native views to cold setup, explicit button command writes, telemetry writes, scheduled job ownership, and read-only upload/dump routes.
Rejected Alternatives: Leaving `float3` base positions as a raw `NativeArray<float3>` was rejected because its 12-byte stride is not 8-byte-clean for the requested ARM64 proof. Moving only telemetry to DataVault was rejected because button job buffers were still retained native aliases.
Scalability potential: Low uses the same button count and fails closed if vault views are unavailable. Middle/High/Ultra can spend saved safety margin on richer cockpit presentation without changing DTO layout, authority route, or button truth.
Hardware Impact: Removes seven persistent native aliases from a UI MonoBehaviour. Added vault payloads are bounded by `MaxButtons` plus a fixed telemetry ring. Expected GC delta is 0 B/frame; profiler microseconds are not available from shell.

## Decision 052 - VehicleSubOs ARM64 Layout And Read Purity

Problem: The cockpit migration needed proof that DTO rows are 8-byte-clean and that `TryRead*` accessors do not allocate, regenerate, complete jobs, or mutate state.
Solution: Added `CockpitButtonBasePosition` as explicit 16-byte row and documented `CockpitTelemetryEntry` as explicit 64-byte row. `TryReadButtonStates`, `TryReadButtonTargets`, `TryReadButtonProgress`, `TryReadButtonOffsets`, `TryReadButtonBaseLocalPositions`, `TryReadButtonMatrices`, and `TryReadTelemetryRing` now resolve read-only handles only. Job writer locks are acquired by `TryAcquireButtonJobBuffers` and released by late-frame/teardown paths.
Rejected Alternatives: Completing the button job inside a read helper was rejected because read accessors must not hide synchronization. Reacquiring or growing buffers from read helpers was rejected because it breaks defragmentation purity.
Scalability potential: Low/Middle/High/Ultra share one row layout. GlobalQualityWeight can affect presentation cadence elsewhere, not button DTO shape or telemetry authority.
Hardware Impact: `CockpitButtonBasePosition` is 16 bytes and `CockpitTelemetryEntry` is 64 bytes; both are divisible by 8. No double, long, or ulong field exists in either row.

## Decision 053 - FakeRadarBlipController Tiny Job Removal

Problem: `FakeRadarBlipController` retained two native arrays and one native list for a 64-entry HUD radar cull, plus a tiny Burst job and same-frame handoff that did not justify persistent native ownership.
Solution: Removed `RadarBlip2DCullJob`, `_radarCullCandidates`, `_radarCullResults`, and `_visibleBlipMatrices`. The controller now culls directly over the fixed `SpatialQueryHit[64]` buffer and writes matrices into the existing fixed managed `Matrix4x4[64]` draw buffer.
Rejected Alternatives: Moving the three buffers into DataVault was rejected because this is per-frame HUD-local scratch and a tiny job, not cross-domain native ownership. Keeping the NativeList handoff was rejected because it remained a persistent native alias in a MonoBehaviour.
Scalability potential: Low avoids native allocation and job overhead for 64 HUD blips. Middle/High/Ultra retain continuous quality scaling through `_qualityBlipCapacity` and `_qualityThermalGhostCapacity`; higher tiers spend cycles on more visible/ghost blips, not on retained native memory.
Hardware Impact: Removes three persistent native aliases and a same-frame job completion risk. Static expected GC delta remains 0 B/frame because existing managed arrays are fixed-size fields.

## Decision 054 - Compile Repairs Exposed During Vehicle/Radar Verification

Problem: Verification exposed moving-worktree compile blockers outside X_000's direct memory targets: Regex namespace, `SignalBeacon` gameplay route, sonar local shadowing, obsolete editor overloads, seismic double-to-float angle call, radiation status source `uint`/`int` boundary, and missing `Hecton8.Items` namespace in `GlobalRegistryContracts`.
Solution: Made minimal compile-only repairs at the exact failing boundaries and reran gated builds until the proof route was clean. No unrelated edits were reverted.
Rejected Alternatives: Stopping at compile errors was rejected. Broadly refactoring radiation, seismic, or registry systems was rejected because the build errors required narrow type/namespace fixes only.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged except that compile proof is restored. These fixes do not alter quality decisions.
Hardware Impact: 0 us runtime gain claimed. The value is evidence quality: final build reports 0 warnings and 0 errors.

## Decision 055 - Residual Truth After Thirty-Nine Alias Removals

Problem: The latest scoped files are clean, but the project remains globally dirty. Claiming full purge would be false.
Solution: Reran full Roslyn audit after the clean build. Current proof state: 2398 files, 0 parse failures, 7736 native fields, 2224 forbidden persistent candidates, and 661 MonoBehaviour candidates across 60 files, hash `aeca727724ffa3d082660c7c28726bd038689766a6e25f42ab9fc5e9d335638e`.
Rejected Alternatives: Bulk-editing `PlayerInventory`, `HectonFluidEngine`, `GasDynamicsSolver`, or `DestructibleOrganicManager` in the same pass was rejected because each is a large authority owner requiring a route card and phased verification.
Scalability potential: Low still carries residual memory ownership risk until remaining owners are migrated. Middle/High/Ultra cannot hide stale native aliases because this is correctness, not visual fidelity.
Hardware Impact: Latest accepted work removes ten more targeted persistent aliases, bringing X_000 targeted removals to 39. Project-wide cleanup remains required.

Status: BUILD PENDING / ECOSYSTEM WRAPPER AND WORLD PROCEDURAL FIELD SAMPLER VAULT MIGRATION COMPLETE / PROJECT-WIDE PURGE INCOMPLETE

## Decision 056 - EcosystemDirector Wrapper Descriptor Correction

Problem: `EcosystemDirector` used a nested `VaultNativeArray<T>` wrapper that retained a `NativeArray<T>` field internally. The outer MonoBehaviour did not show a direct private native array, but the wrapper still kept a native view across phase boundaries.
Solution: Rewrote the wrapper to store only `IDataVault` and `VaultGenerationHandle<T>`. `IsCreated`, `Length`, indexer, `GetSubArray`, and `Resolve` now resolve a method-local native view and discard it immediately.
Rejected Alternatives: Leaving the cached native view hidden inside the wrapper was rejected because it defeats relocation safety while evading simple field scans. Replacing every wrapper call site with raw handle code was rejected for this pass because it would increase churn in a broad ecosystem owner.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged; this is memory ownership hygiene. Quality settings must scale ecosystem fidelity, not DTO ownership or cached pointer lifetime.
Hardware Impact: Removes one hidden persistent native alias carrier. Runtime cost is one DataVault handle validation on wrapper access; no heap allocation is introduced.

## Decision 057 - WorldProceduralFieldSampler DataVault Migration

Problem: `WorldProceduralFieldSampler` retained six persistent MonoBehaviour native arrays for sampler DTOs and a 512x512 noise LUT. These buffers feed Burst sampling jobs and scalar biome read helpers, so stale aliases would cross simulation phases.
Solution: Added `SystemID.WorldProceduralFieldSampler` plus BufferIDs 74363..74368, replaced the six fields with `VaultGenerationHandle` descriptors, and resolved mutable views only during explicit data preparation or scheduled job ownership. Job buffers are writer-locked until completion/teardown and released on schedule failure.
Rejected Alternatives: Keeping local arrays was rejected because the sampler survives as a scene service. Passing handles into Burst jobs was rejected because jobs require native views. Moving the noise LUT to a managed array was rejected because sampler jobs consume a native lookup table.
Scalability potential: Low can keep the same lookup stride and sampler counts. Middle/High/Ultra can spend budget on denser procedural placement or richer biome visuals without changing DTO layout, owner route, or sampling truth.
Hardware Impact: Removes six persistent native aliases from a MonoBehaviour plus the hidden Ecosystem wrapper alias in the same loop. Vault payloads are bounded by zone/profile counts plus a 262144-entry ushort LUT. Expected GC delta is 0 B/frame; profiler microseconds are not available from shell.

## Decision 058 - WorldProcedural Read Purity And Job Lock Lifetime

Problem: The sampler exposes `TryGet*` helpers that previously resolved mutable native views, and the scheduled job path could leak writer locks if schedule validation threw after locks were acquired.
Solution: `TryGetZoneData`, `TryGetBiomeMatrixData`, `TryGetBiomeFamilyData`, and secondary-biome fallback now use `TryReadOnlyHandle`. `ScheduleCellSamplingJob` marks locks held before scheduling and releases them in a `catch` path if scheduling fails.
Rejected Alternatives: Treating private `TryGet*` helpers as exempt was rejected because the doctrine applies to route semantics, not public visibility. Completing or regenerating buffers inside read helpers was rejected because it would hide synchronization/allocation in read paths.
Scalability potential: Low avoids hidden stalls and allocator pressure in scalar biome reads. Middle/High/Ultra preserve deterministic sampler data ownership while raising visual density through existing quality controls.
Hardware Impact: Blocks stale view retention and closes a lock-leak edge case. Compile proof remains pending because the no-restore build found a missing `project.assets.json` and the restore-capable build is currently blocked by active compiler processes.

## Decision 059 - RadiationHazardGrid Cached Native View Purge

Problem: `RadiationHazardGrid` already owned DataVault handles for radiation grids, source lanes, profile/tuning lanes, status signal lane, and telemetry, but still cached twelve raw `NativeArray` views as MonoBehaviour fields.
Solution: Replaced the twelve raw native fields with a local `VaultNativeArray<T>` descriptor wrapper that stores only `IDataVault` plus `VaultGenerationHandle<T>`. Existing jobs and save/load code receive method-local `NativeArray` views through implicit resolution.
Rejected Alternatives: Rewriting the entire radiation kernel into writer-fenced helper calls was rejected for this pass because the file is under active parallel gameplay edits and already has the DataVault owner route. Keeping cached raw views was rejected because it violates relocation safety and the X_000 task scope.
Scalability potential: Low keeps fixed 32^3 grid and 64 source capacity. Middle/High/Ultra can raise sampling fidelity or visual response elsewhere without changing radiation DTO layout or owner route.
Hardware Impact: Removes twelve persistent native aliases from a gameplay MonoBehaviour. Scoped regex now reports 0 direct private native collection fields in `RadiationHazardGrid.cs`; compile proof is pending Unity/Bee compiler drain.

## Decision 060 - Migratory Sargassum Vault Migration

Problem: `WorldProceduralScatterDirectorMigratorySargassum` retained six persistent `NativeArray` fields inside a MonoBehaviour partial for island state, scratch state, selected sources, flow samples, and AUP spatial handles. These buffers cross scatter slow-tick, Burst drift, spatial publication, and DataVault replacement phases.
Solution: Added six `SystemID.WorldSargassum` BufferIDs and replaced the raw fields with `MigratoryVaultArray<T>` descriptors holding only `IDataVault` plus `VaultGenerationHandle<T>`. The drift job now receives method-local native views only after explicit DataVault writer locks are acquired.
Rejected Alternatives: Leaving these arrays local because the island cap is only 24 was rejected; small persistent native aliases still stale under relocation. Moving the spatial hash itself into DataVault was rejected because `HectonSpatialHash` owns its own internal native tables and requires a separate route card.
Scalability potential: Low keeps the same 24-island cap and fails closed if DataVault allocation is locked. Middle/High/Ultra can increase visual density around migratory canopies elsewhere, but island DTO layout, buffer ownership, and spatial signal truth stay unchanged.
Hardware Impact: Removes six additional persistent native aliases from a world-generation MonoBehaviour. No heap allocation is introduced; descriptor access is generation-checked and method-local.

## Decision 061 - Migratory Sargassum Job Lock And Phase Purity

Problem: The old slow-tick path could refresh island/source state while a previous migratory drift job was still marked running, and a DataVault-backed job would need relocation protection until completion.
Solution: `TickMigratorySargassumLane` now returns while the migratory job is running. `TryAcquireMigratorySargassumJobBuffers` locks the island and flow-sample buffers before sampling flow and scheduling the Burst job; `ReleaseMigratorySargassumJobBufferLocks` runs after normal completion, forced teardown, schedule failure, and DataVault hot-swap.
Rejected Alternatives: Resolving DataVault views without writer locks was rejected because defragmentation could move memory while Burst owns the pointer. Completing the job from read helpers was rejected because reads must not hide synchronization.
Scalability potential: Low avoids hidden same-phase mutation and relocation hazards. Middle/High/Ultra preserve deterministic sargassum spatial publication while visual overkill scales through presentation lanes, not by changing memory ownership.
Hardware Impact: Scoped regex reports 0 direct private native collection fields in `WorldProceduralScatterDirectorMigratorySargassum.cs`. Build proof is pending because CPU was 50.56% and `dotnet exec ... VBCSCompiler.dll` process 52216 was active.

## Decision 062 - MarauderOutpostGenerationService Vault Migration

Problem: `MarauderOutpostGenerationService` retained seven persistent MonoBehaviour native arrays for WFC solve state, shell extraction, interactable spawns, counters, mutable WFC persistence, and the 300-frame black-box ring.
Solution: Added `SystemID.WorldOutposts` plus BufferIDs 74375..74381, replaced all seven arrays with `VaultGenerationHandle<T>` descriptors, and kept native views method-local through DataVault read-only resolution or writer locks.
Rejected Alternatives: Leaving the WFC grid as a public `NativeArray<byte>` was rejected because power-grid publication would retain a stale pointer carrier. Moving the registry handoff to managed bytes was rejected because the existing logistics grid registry uses native fixed-slot copies and would add heap churn.
Scalability potential: Low uses the same low-tier 5x5x3 solve shape and fails closed if vault buffers are unavailable. Middle/High/Ultra can spend saved safety margin on denser shell visuals and interactable presentation while keeping WFC truth, DTO layout, and authority route unchanged.
Hardware Impact: Removes seven persistent native aliases from a world-generation MonoBehaviour. Expected GC delta remains 0 B/frame; static proof only, no profiler microseconds available from shell.

## Decision 063 - Marauder Outpost Job Fences And Read Purity

Problem: The outpost solve, matrix extraction, and AUP-shift jobs need stable native pointers until completion, while `TryGet*` and `TryRead*` routes must not allocate, regenerate buffers, complete jobs, or publish state.
Solution: Added explicit `_solveJobBufferLocked`, `_extractionJobBuffersLocked`, and `_shiftJobBufferLocked` state. Solve, extraction, and shift paths acquire DataVault writer locks immediately before scheduling and release them on late-frame completion, schedule failure, forced teardown, and DataVault hot-swap. `TryGetWfcGrid`, `TryGetShellMatrices`, and private `TryRead*` helpers now resolve read-only handles only.
Rejected Alternatives: Completing jobs from read accessors was rejected because it hides synchronization in consumer reads. Resolving writable views without DataVault locks was rejected because relocation/defragmentation could move memory while Burst owns the pointer.
Scalability potential: Low avoids hidden stalls and allocator pressure under outpost hydration. Middle/High/Ultra keep deterministic WFC state ownership and scale visual overkill through matrices/material response, not through a different buffer route.
Hardware Impact: Blocks stale pointer retention between solve/extract/shift phases. Lock/unlock overhead is bounded by generation events, not per-frame reads; profiler microseconds are pending build/profiler access.

## Decision 064 - Marauder Outpost ARM64 Layout Proof And Registry Boundary

Problem: The new outpost DataVault payloads needed explicit proof that custom DTO rows are 8-byte-clean and that the registry handoff does not force raw native field retention in the MonoBehaviour.
Solution: Documented `OutpostTelemetryEntry` as explicit 128 bytes with `SectorHash` at offset 8 and padding ulongs at offsets 72,80,88,96,104,112,120. Documented `OutpostInteractableSpawn` as explicit 32 bytes with `_pad1` at offset 24. Added a `WfcOutpostGridRegistry.RegisterGrid` overload for `NativeArray<byte>.ReadOnly` so publication copies from a scoped view.
Rejected Alternatives: Faking byte maps for Unity `float4x4` internals was rejected; the report states it as a 64-byte Unity.Mathematics row with no double/long scalar. Reusing the old public `WfcGrid` field for compatibility was rejected because it was the exact stale alias being removed.
Scalability potential: Low/Middle/High/Ultra share one DTO layout. GlobalQualityWeight affects quality tier and presentation, not telemetry row size or buffer authority.
Hardware Impact: `OutpostTelemetryEntry` and `OutpostInteractableSpawn` sizes are divisible by 8. All 8-byte scalar/padding lanes start on 8-byte offsets. Scoped regex reports zero direct native collection fields in `MarauderOutpostGenerationService.cs`; final build reports 0 warnings and 0 errors.

## Decision 065 - Marauder Build And Residual Audit Truth

Problem: Static proof was not enough; the migration needed compiler and Roslyn evidence after the DataVault descriptor rewrite.
Solution: Waited until the build gate cleared at CPU 21.93% with no active compiler processes, then ran `dotnet build Hecton8.Editor.csproj /nr:false -p:UseSharedCompilation=false -v:minimal`. Build completed in 00:02:08.87 with 0 warnings and 0 errors. Reran the Roslyn audit and regenerated the mono residual and exorcism reports.
Rejected Alternatives: Launching a build while CPU was 100% or `csc` was active was rejected by project rule. Reporting the Marauder slice from scoped regex only was rejected because the compiler and AST scanner are the proof channels.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged by proof generation. Residual risk remains memory ownership correctness, not a quality-level trade.
Hardware Impact: Latest full ledger: 2406 files, 0 parse failures, 7710 native fields, 2138 forbidden persistent candidates, 581 MonoBehaviour candidates across 58 files, hash `1a2db4092081840dfc0366bb82ed12aaa304e226b1fc5b3b1ee858e37456c58a`. Project-wide purge is still incomplete.

Status: BUILD CLEAN / MARAUDER OUTPOST VAULT MIGRATION COMPLETE / PROJECT-WIDE PURGE INCOMPLETE

## Decision 066 - CrashTelemetryBuffer Vault Migration

Problem: `CrashTelemetryBuffer` retained three persistent MonoBehaviour native arrays for the live crash ring, export snapshot, and export scratch bytes. This is a critical black-box owner, so stale native aliases here are unacceptable.
Solution: Added CoreDiagnostics-owned BufferIDs 74382..74384 and replaced the three fields with a `VaultArray<T>` descriptor wrapper that stores `IDataVault` plus `VaultGenerationHandle<T>`. Initialization acquires fixed DataVault payloads; disposal releases handles through the cached vault.
Rejected Alternatives: Leaving the ring local because it is a diagnostic singleton was rejected; diagnostics still obey memory sovereignty. Moving crash export scratch entirely to managed memory was rejected because the existing binary exporter already uses native snapshot/scratch for bounded unsafe copies.
Scalability potential: Low/Middle/High/Ultra share the same black-box payload shape. Quality settings must not alter crash telemetry authority, DTO layout, or export identity.
Hardware Impact: Removes three persistent native aliases from a critical MonoBehaviour. Expected GC delta remains 0 B/frame because existing managed file scratch buffers were already allocated cold.

## Decision 067 - Crash Export Thread DataVault Boundary

Problem: The background crash export worker must not resolve DataVault handles from a non-owner thread.
Solution: `TryExportSnapshot` and the unhandled-exception path now build the native export scratch and mirror it into `_crashExportFileScratch` before signaling the worker. `WritePreparedExportToDisk` writes the managed scratch only and no longer touches `_exportScratch` or `_exportSnapshot`.
Rejected Alternatives: Letting the worker call `BuildExportScratch` was rejected because it would resolve DataVault-backed views off the owner thread. Removing the worker was rejected because crash export I/O should remain isolated from the game tick.
Scalability potential: Low keeps export bounded to a 1000-row snapshot. Middle/High/Ultra receive the same deterministic crash payload; visual quality has no authority over the black-box route.
Hardware Impact: Prevents cross-thread DataVault handle resolution while retaining the existing fixed 64016-byte export payload. Build proof is pending because CPU remains above the allowed threshold.

Status: BUILD PENDING / CRASH TELEMETRY BUFFER VAULT MIGRATION IN PROGRESS / PROJECT-WIDE PURGE INCOMPLETE

## Decision 068 - Crash Telemetry Layout And Read Purity Static Proof

Problem: The crash telemetry migration needed ARM64 layout evidence and read-purity evidence before the compiler gate was available. Reporting only the field replacement would leave the black-box owner under-proven.
Solution: Audited the explicit structs in `CrashTelemetryBuffer`: `CrashExportHeader` is 16 bytes with `Magic` at offset 0, `TelemetryEntry` is 64 bytes with only 4-byte scalar/vector lanes, and `LiveTelemetryRecord` is 32 bytes. The fixed byte export scratch is 64016 bytes. `TryExportSnapshot` and `TryExportSnapshotFromUnhandledException` build native scratch on the owner thread, then the background worker writes the fixed managed scratch only.
Rejected Alternatives: Running `dotnet build` while CPU was 100% and `dotnet`/`csc` were active was rejected by build-gate law. Claiming runtime ARM64 proof without editor/player verification was rejected; this is static source proof until the compiler/profiler gate clears.
Scalability potential: Low/Middle/High/Ultra all share one crash DTO/export contract. Visual quality must not change black-box row size, export identity, or crash authority route.
Hardware Impact: Confirms the three CrashTelemetryBuffer payloads removed from MonoBehaviour fields have 8-byte-clean DTO or byte-lane storage. Targeted removals remain at least 74 persistent aliases/alias carriers pending clean build and full Roslyn refresh.

Status: BUILD PENDING / CRASH TELEMETRY BUFFER VAULT MIGRATION STATIC-PROOFED / PROJECT-WIDE PURGE INCOMPLETE

## Decision 069 - HectonWorldGenerator LUT Handle Migration

Problem: `HectonWorldGenerator` retained three persistent `NativeArray<float>` fields for west slope, east slope, and biome remap LUTs. These LUTs cross chunk job scheduling and public terrain read helpers, so retaining raw MonoBehaviour fields violated the native collection purge.
Solution: Added WorldStreaming BufferIDs 74385..74387, replaced the three fields with `VaultGenerationHandle<float>` descriptors plus cached `IDataVault`, and fill the fixed LUT buffers under DataVault writer locks in `EnsureLUTs`. `HectonVertexJob` receives method-local LUT views at schedule time.
Rejected Alternatives: Keeping the LUTs local because they are only 1024 floats each was rejected; small persistent native aliases still become stale under relocation. Converting jobs to call `AnimationCurve.Evaluate` was rejected because Burst jobs cannot use managed curve objects.
Scalability potential: Low keeps the same 1024-sample LUTs and bounded chunk cadence. Middle/High/Ultra can increase chunk density or visual terrain detail without changing LUT ownership or terrain authority.
Hardware Impact: Removes three persistent native aliases from a world-streaming MonoBehaviour. LUT payload total is 3 * 1024 * 4 = 12288 bytes, byte count divisible by 8, and contains no 8-byte scalar lane.

## Decision 070 - HectonWorldGenerator Read Accessor De-Lazification

Problem: `GetBiomeAt` and `GetWorldHeight` called `EnsureLUTs`, so read accessors could lazily allocate/grow native buffers.
Solution: Removed `EnsureLUTs` from both read helpers. They now use `TryReadOnlyHandle` for existing LUTs and fall back to direct `AnimationCurve.Evaluate` when no LUT handle exists. Scheduling and preview generation remain explicit mutation/setup paths.
Rejected Alternatives: Returning a hard zero when LUTs are unavailable was rejected because it would corrupt terrain queries before streaming setup. Keeping lazy `EnsureLUTs` was rejected because `Get*` paths must not allocate or mutate global state.
Scalability potential: Low gets deterministic fallback without allocation. Middle/High/Ultra still use the fixed LUTs once owner setup has run; quality scaling is not tied to read accessor side effects.
Hardware Impact: Removes hidden native allocation/regeneration from public terrain reads. Build proof remains pending under the CPU/compiler gate.

Status: BUILD PENDING / HECTON WORLD GENERATOR LUT VAULT MIGRATION STATIC-PROOFED / PROJECT-WIDE PURGE INCOMPLETE

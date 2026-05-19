# LOG_HFI_AUDIT

Agent: HFI_AUDIT
Domain: Cross-domain static integration audit
Status: PENDING VERIFICATION
Date: 2026-05-19

## 2026-05-19 Dirty Integration Snapshot R1

Scope: read-only static audit of current dirty tree after a large multi-agent batch. No runtime code was edited. No Unity import, Play Mode, Profiler, GCMonitor, player build, or dotnet build was launched in this slice.

Evidence class: STATIC_SOURCE / STATIC_DOC / GIT_DIFF only.

### Snapshot Counts

- `git status --short -uall`: observed moving target between 392 and 422 entries while agents continued writing. Latest observed count in this slice: 422 entries: 354 modified, 66 untracked, 3 deleted.
- Interpretation: current tree is source-heavy construction state, not verification-grade integration state.

### Highest-Signal Findings

- `Docs/Tasks/CURRENT_BATCH.md` fails `git diff --check` on trailing whitespace and final blank-line errors. Static process impact: strict prompt extraction can become noisy or brittle.
- `CURRENT_BATCH.md` contains parsing hazards reported by read-only docs subagent: split or malformed `SHINOBU_141` prompt tag around line 2430, inline closing tags around line 2627, and mojibake near the same area. Evidence remains STATIC_DOC until re-extracted with each agent ID.
- Subtitle cue traffic has split-brain semantics. `Assets/_Project/Scripts/UI/BabelSubtitleSyncRuntime.cs` defines `Hecton8.Core.Contracts.Signals.SubtitleCueSignal` with `StartAudioFrame`, `DurationMilliseconds`, `Priority`, `Flags`, and `SourceHash`. `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs` defines `Hecton8.Modding.SubtitleCueSignal` with `TokenHash`, `Duration`, `Priority`, `_pad0`. This is not a direct namespace duplicate, but it is a duplicate signal-name/meaning route and conflicts with the batch text that expects the 16-byte `TokenHash + float Duration + uint Priority + uint _pad0` layout.
- Unity metadata is incomplete for new C# assets. Static scan found 17 untracked `.cs` files; 14 lacked matching `.meta` at scan time. Examples: `BallisticsRuntime.cs`, `BallisticsEditorFacade.cs`, `BabelSubtitleSyncRuntime.cs`, `BiomeTransitionManagerRuntime.cs`, `BiomeTransitionTunerWindow.cs`, `ProceduralCoralContracts.cs`, `EquipmentThermalBatteryContracts.cs`, `VRSomaticProvider.Comfort.cs`, `DynamicDecalVaultRuntime.cs`.
- Data Monolith boot path can fail if the binary blob is absent. Runtime expects `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`; observed `Assets/StreamingAssets` did not contain that blob in the static scan. `GameBootstrapper` sets fail-if-missing behavior for the static data arena.
- Data Monolith ABI appears changed while `H8DataLayoutConstants.FormatVersion` remains `1`. Static concern: stale v1 blobs may be accepted by version gate and decoded under a changed ABI.
- Rollback Merkle/AUP schema is inconsistent by static read. `EntityAUPs` resolves as `AbsoluteUniversePosition`, while snapshot descriptors and hash byte-length paths use `UnsafeUtility.SizeOf<double3>()` for some AUP hashing routes. If intentional projection hashing, the contract needs an explicit note; otherwise byte offsets/lengths do not describe the backing buffer.
- `RigidbodyAUPs` consumer drift exists: physics ownership appears to use `double3`, while `ArchitectEyeVisualizer` still requests `AbsoluteUniversePosition` for `BufferID.RigidbodyAUPs`, forcing silent fallback behavior.
- Mock network jitter path appears scheduled every fixed tick and marks state active in the job without an obvious mode/debug gate in the inspected path.
- BufferID governance is drifting through local hard-cast ranges instead of central enum/ledger names. Examples: Ballistics `(BufferID)71270..71279`, DroneFleet `(BufferID)70265..70275`, ProceduralCoral `(BufferID)71390..71408`.
- Save telemetry dump format changed in `VoxelDeltaCompressionArchitecture` by prepending a dump header. Tools expecting raw telemetry rings need a reader update or explicit version handling.
- Save docs are duplicated/noisy: `SAVE_PAGING_PROTOCOL.md` repeats active-version blocks; `SAVE_V8_BINARY_SPEC.md` repeats the heading and retains superseded `CurrentHeaderSize = 52` text under older sections.

### What Looks Like Real Work

- Large source-backed changes exist in Ballistics, Biome Transition, Hydrodynamic KCC, Seismic/Celestial, DroneFleet, Rollback Netcode, Save/Voxel, Structural Integrity, Thermodynamics, Audio/Virtualization, UI/Babel, and Procedural Coral/Wreckage.
- Strongest narrow verification claim found in agent status is `SHINOBU_127`, which reports narrow `Assembly-CSharp` compile passes for owned Ballistics scope. This was not independently rerun in this slice.
- Most other status files still say PENDING, BLOCKED BY DEPENDENCY, COMPILE PENDING, SOURCE IMPLEMENTED, or STATIC PASS only.

### Immediate Integration Order

1. Repair `CURRENT_BATCH.md` formatting and XML prompt extraction hazards.
2. Generate/track Unity `.meta` files for new C# assets before any commit or merge.
3. Unify subtitle cue contract and decide whether modding subtitle payload is a wrapper/adapter or the canonical signal.
4. Validate Data Monolith blob generation, `StreamingAssets` placement, and ABI `FormatVersion`.
5. Normalize rollback AUP byte schema and document projection hashing if intentional.
6. Add central BufferID enum/ledger entries or route cards for hard-cast ranges.
7. Run compile only after CPU/compiler guard opens; runtime proof remains pending.

Runtime microseconds saved: 0 claimed. This slice only prevents integration churn and false verification.

## 2026-05-19 Dirty Integration Snapshot R2

Scope: deeper static contract audit after R1. No runtime/source fixes applied. No build launched.

Evidence class: STATIC_SOURCE / STATIC_DOC / GIT_DIFF only.

### New High-Severity Findings

- `BufferID.BabelSubtitleCueState` and `BufferID.BabelSubtitleCueTelemetryRing` are referenced by `Assets/_Project/Scripts/UI/BabelSubtitleSyncRuntime.cs`, but the current parsed `H8Memory.BufferID` enum does not define either symbol. Static impact: this is a likely compile error in the current dirty tree.
- `H8Memory.BufferID` contains duplicate numeric values inside the enum itself:
  - `70142..70149`: `ShinobuVRSomatic*` overlap with `ShinobuInventory*`.
  - `70200`: `SaveWorldPagerWriteArena` overlaps with `ConstructionBuilderOccupancy`.
  - `70800..70807`: `AudioStem*` overlap with `ShinobuActiveEquipment*`.
  C# permits duplicate enum values, but `GlobalDataVault` does not get separate storage identities from separate names with the same integer key.
- `DroneFleetManager` hard-casts local BufferIDs that collide with existing Save Merkle IDs:
  - `70270` DroneFleet service cursor collides with `SaveMerkleNodeFront`.
  - `70271` DroneFleet pending snapshots collides with `SaveMerkleNodeBack`.
  - `70272` DroneFleet next-frame snapshots collides with `SaveMerkleLeafDescriptors`.
  - `70273` DroneFleet spatial bucket heads collides with `SaveMerkleDeltaRecords`.
  - `70274` DroneFleet spatial next indices collides with `SaveMerkleDeltaBytes`.
  - `70275` DroneFleet spatial keys collides with `SaveMerkleCompressedBytes`.
  Static impact: potential cross-system Vault buffer corruption between DroneFleet and Save/Merkle.
- Additional hard-cast overlap exists in active source:
  - `RollbackNetcodeContracts` uses `(BufferID)70750..70752` for rollback state buffers.
  - `VolcanicUpdraftDirector` uses `(BufferID)70750..70752` for volcanic buffers.
  - `H8Memory` defines `ShinobuHydroKccCsvScratch/DebugOutputs/ResolvedHits` at `70750..70752`.
  Static impact: three unrelated systems claim the same numeric Vault keys.
- `ToxicOutgassingChemistryRuntime` uses `(BufferID)70800..70807`; current `H8Memory` also maps `AudioStem*` and `ShinobuActiveEquipment*` to `70800..70807`. Static impact: atmosphere, audio, and equipment can alias the same Vault keys if active together.
- `DiegeticGlitchSurgeonRuntime` hard-casts `(BufferID)70520` as a Terminal OS bridge while `H8Memory` defines `ShinobuInputCurrentDto = 70520`. Static impact: UI glitch/terminal bridge can alias input state.
- `DiegeticGlitchSurgeonRuntime` hard-casts `(BufferID)70900` while `H8Memory` defines `ShinobuModSandboxBlackboxMemory = 70900`. Static impact: UI glitch state can alias mod sandbox blackbox memory.

### Data Monolith Details

- `Assets/StreamingAssets` currently contains only `signal_tuning_profiles.csv` and `.meta`; no `Hecton8/DataMonolith/static_data.h8bin` was found.
- `H8DataLayoutConstants.DefaultStreamingAssetsRelativePath` is `Hecton8/DataMonolith/static_data.h8bin`.
- `GameBootstrapper.InitializeBootstrapDataMonolith()` sets `failIfMissing = true` outside `UNITY_EDITOR` and throws `FatalArchitectureException` when load fails.
- `H8DataMonolithTypes.cs` changed multiple UTF-8 offsets from signed `int` to unsigned `uint` and expanded `H8StaticLocalizationReference` from 12 bytes to 16 bytes, while `FormatVersion` remains `1`.
- Loader validation still accepts only `H8DataLayoutConstants.FormatVersion`, so a stale v1 blob with old field semantics can pass version validation if present. Checksum may still reject mismatched bytes, but versioning no longer communicates ABI change.

### Subtitle/Babel Details

- `CURRENT_BATCH.md` has contradictory subtitle expectations:
  - Early task text expects `SubtitleCueSignal` = 16 bytes: `TokenHash`, `float Duration`, `uint Priority`, `_pad0`.
  - Later SHINOBU_150 text expects a 16-byte signal containing `TokenHash` and `StartAudioFrame`.
  - `BabelSubtitleSyncRuntime.cs` implements `TokenHash`, `StartAudioFrame`, `DurationMilliseconds`, `Priority`, `Flags`, `SourceHash`.
  This is still 16 bytes, but the wire contract meaning is not singular.

### Batch Parsing Details

- `SHINOBU_141` opening tag spans lines and starts inline with body text rather than a clean single-line tag block. Several later closing tags are inline with body text. This may not break every extractor, but it violates the strict "extract own XML tag" assumption used by agents.

### Meta Hygiene Update

- The untracked C# set changed while auditing. Latest static scan found 17 untracked `.cs` and 15 without a matching `.meta` at that moment, including `KineticCharacterAnimatorJobs.cs`, `KineticCharacterAnimatorTypes.cs`, `MesofaunaBehavioralStateMachine.cs`, `BallisticsRuntime.cs`, `BallisticsEditorFacade.cs`, `FabricationAssemblerRuntime.cs`, `VRSomaticProvider.Comfort.cs`, `TetherAupVerletJobs.cs`, `EquipmentThermalBatteryContracts.cs`, `DynamicDecalVaultRuntime.cs`, `BiomeTransitionManagerRuntime.cs`, `BiomeTransitionTunerWindow.cs`, `ProceduralCoralContracts.cs`, and `ProceduralCoralJobs.cs`.

### Updated Immediate Integration Order

1. Stop adding hard-cast `BufferID` ranges. First fix numeric collisions and missing enum symbols.
2. Restore `BabelSubtitleCueState` / `BabelSubtitleCueTelemetryRing` or rename `BabelSubtitleSyncRuntime` to an existing canonical BufferID route.
3. Move DroneFleet temporary IDs away from `SaveMerkle*` immediately before any save/drone runtime test.
4. Reconcile `70750..70752`, `70800..70807`, `70520`, and `70900` ownership before treating DataVault as safe.
5. Then fix batch XML/whitespace and Unity `.meta`.
6. Then perform guarded compile.

Runtime microseconds saved: 0 claimed. These are static contract defects; no profiler evidence exists.

## 2026-05-19 Dirty Integration Snapshot R3

Scope: static generated-project and untracked-source audit. No build launched.

Evidence class: STATIC_SOURCE / GIT_STATUS / CSPROJ_TEXT only.

### Moving Dirty Tree

- Latest observed `git status --short -uall`: 448 entries: 365 modified, 80 untracked, 3 deleted.
- Latest observed untracked C# count: 26 at first count, then 27 by generated-project inclusion scan. The tree is still moving while this audit runs.

### Generated Project Inclusion Gap

- Current scan of all root `*.csproj` files found every currently untracked `.cs` file absent from generated project text.
- Examples absent from all `*.csproj`: `BallisticsRuntime.cs`, `BallisticsEditorFacade.cs`, `BabelSubtitleSyncRuntime.cs`, `EquipmentThermalBatteryContracts.cs`, `BiomeTransitionManagerRuntime.cs`, `ProceduralCoralContracts.cs`, `ProceduralCoralJobs.cs`, `ProceduralCoralVault.cs`, `DynamicDecalVaultRuntime.cs`, `VRSomaticProvider.Comfort.cs`, `InventoryRoutingNetwork.cs`, `FabricationAssemblerRuntime.cs`, `TopographicalSonarSynthesizer.cs`, `MesofaunaBehavioralStateMachine.cs`, and KineticCharacter animator files.
- Static implication: local `dotnet build` results can be false negatives for this batch. Until Unity imports/regenerates projects or the files become included in the generated compile graph, narrow build claims do not prove these new source files compile.

### Missing World Source Wall Rechecked

- `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs` and `.meta` are absent on disk.
- Current direct scan of `Hecton8.Core.csproj`, `Assembly-CSharp.csproj`, `Directory.Build.targets`, and `Directory.Build.props` did not find a current reference to that source file.
- Static implication: older agent logs that cite this as the current hard compile wall may now be stale. The source is still absent, but the generated-project include wall was not present in the currently scanned project files.

### Current Hard Compile Mines By Static Source

- Missing BufferID enum symbols for `BabelSubtitleCueState` and `BabelSubtitleCueTelemetryRing` remain the clearest direct C# compile mine because `BabelSubtitleSyncRuntime.cs` references them and current `H8Memory.BufferID` lacks them.
- Untracked files not included in generated projects mean this compile mine may remain hidden until project regeneration/import.

Runtime microseconds saved: 0 claimed. No compile/runtime proof.

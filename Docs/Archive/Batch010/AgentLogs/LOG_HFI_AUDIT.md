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

## 2026-05-19 Global Direction Snapshot R4

Scope: global product/architecture direction audit after user requested to ignore small defects. No runtime/source fixes applied. No build launched.

Evidence class: STATIC_SOURCE / STATIC_DOC / GIT_DIFF only.

### Current Direction

- The project direction is technically coherent: deep-sea systemic survival/engineering sim, NASA-punk/noir identity, deterministic/data-oriented runtime doctrine, and a large authored content/control surface.
- Current worktree pressure is broad, not narrow: tracked diff shows 112 changed files with 5412 insertions and 1474 deletions, plus active changes across atmosphere/ocean, animation, inventory, equipment, combat, tethers, asset lifecycle, procedural coral, sonar, UI/Babel, marketing, docs, and authority governance.
- This is no longer "toy prototype" movement. It is infrastructure-heavy AA pre-alpha / vertical-slice-candidate movement.

### Global Concern

- The current vector still leans engine/platform-first more than game-loop-first.
- The likely failure mode is not "cannot invent systems". The failure mode is "beautiful project OS, no proven first 20 minutes".
- Static docs and H-Phi movement show architecture discipline. They do not prove moment-to-moment player value, scene wiring, content density, memory budget, save/load recovery, frame stability, or visual readability.
- More global instruments, more ledgers, and more isolated subsystem depth will not raise product truth unless they shorten the path to a playable route.

### What Is Strategically Good

- Identity is strong enough to steer art, audio, UI, mechanics, and marketing.
- Runtime doctrine is not random: dispatcher phases, typed signal lanes, DataVault ambition, AUP, telemetry, black-box, and quality scaling are aligned.
- Large systems are not empty prose. World streaming, save, player, scatter, sonar, equipment, ecology, and rendering all have real source surfaces.
- Documentation has become more honest: most active docs now mark static evidence separately from runtime proof.

### What Is Strategically Bad

- The project is still overproducing horizontal systems before proving the core loop.
- A lot of current work improves "capability surface" rather than "one playable chain".
- Marketing docs are moving while demo proof remains pending. That is acceptable for preparation, but not for external push.
- Global authority surfaces are being strengthened at the same time as many domain systems are expanding. Without a vertical-slice gate, this can become architecture inflation.

### Direction Correction

Default product target should be:

```text
First 20 Minutes Vertical Slice
boot -> world load -> swim -> find resource -> tool interaction -> craft/repair/build
-> hazard response -> save -> load -> return to same state
```

All new agent work should answer one question:

```text
Which moment of the First 20 Minutes does this make playable, visible, faster, safer, or more testable?
```

If the answer is "none", the work is parked unless it fixes an integration blocker.

### Recommended Operating Order

1. Freeze net-new domain expansion except blockers for the first 20-minute route.
2. Pick one biome, one resource chain, one tool, one hazard, one base/repair action, one save/load roundtrip, and one return path.
3. Make boot/world/scatter/asset lifecycle prove that route in Unity, not just in docs.
4. Make profiling artifacts part of the route definition: frame time, GC, memory, VRAM, and hitch notes.
5. Keep global instruments serving the route. Do not add registry/signal/vault surface just to improve static metrics.
6. Marketing output should be fed by captured route proof, not by architecture claims.

### Current Verdict

- Global direction: correct ambition, correct identity, strong technical doctrine.
- Global execution risk: too much breadth before runtime proof.
- Required correction: less new system surface, more forced playable-route proof.
- H-Phi growth remains useful only if it reduces friction to the vertical slice. If it grows while the slice stays unproven, it is architecture hygiene without product progress.

Runtime microseconds saved: 0 claimed. This is product-direction governance, not runtime optimization.

## 2026-05-19 Global Direction Snapshot R5

Scope: product-direction hardening after repeated user request to ignore minor defects and judge global direction.

Evidence class: STATIC_DOC edits only. No runtime/source fixes applied. No build launched.

### Action Taken

- Added `Docs/ARCHITECTURE/FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md`.
- Linked the route gate from `AGENTS.md`, `.codexrules/AGENTS.md`, `Docs/ARCHITECTURE/README.md`, `Docs/README.md`, `Docs/QUALITY_GATES.md`, `Docs/HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md`, `MASTER_RELEASE_WORK_PLAN.md`, `BUILD_PLAYTEST_ISSUES.md`, and creator outreach workflow.

### Global Direction Rule

The current governing route is:

```text
boot -> world load -> swim -> find resource -> tool interaction -> craft/repair/build
-> hazard response -> save -> load -> return to same state
```

Every new batch must state which route moment it improves or which route blocker it removes. Work outside this path is parked unless it is compile/import/save/load/scene/payload/global-authority corruption work that blocks the route.

### Product Verdict

- HECTON-8 should stop measuring near-term progress by breadth of systems.
- The correct next product proof is a narrow playable chain with profiler, GC, memory, save/load, screenshot, and clip evidence.
- Marketing verification can continue, but public outreach remains blocked until assets come from the proven route.

Runtime microseconds saved: 0 claimed. This is product governance only.

## 2026-05-19 Global Direction Snapshot R6

Scope: selected-route correction after user asked to ignore minor issues and judge direction.

Evidence class: STATIC_SOURCE / STATIC_DOC edits only. No runtime/source fixes applied. No build launched.

### Action Taken

- Added `Docs/ARCHITECTURE/FIRST_20_MINUTES_ROUTE_BRIEF.md`.
- Selected Copper Wire as V0 route: boot -> world load -> safe exit -> swim -> oxygen/depth pressure -> find copper -> collect `Data_Copper` -> `quest_copper_sample` -> craft `Recipe_CopperWire` -> save/load.
- Linked the selected route from architecture/docs indexes, quality gates, roadmap, playtest ledger, creator outreach workflow, static x-ray, and the HFI report.

### Direction Verdict

The project is not globally broken by ambition. It is globally at risk if the next proof target stays broad. The correct near-term direction is not "more systems"; it is one route that forces existing systems to interlock under runtime evidence.

### Why Copper Wire First

- `Recipe_CopperWire` is not scan-locked.
- `Quest_CopperSample` completes on `Data_Copper`.
- Current static docs already identify copper -> item collected -> inventory -> quest -> craft -> save/load as the bounded gameplay proof.
- Scanner and Repair Tool are deferred because their recipes rely on scan gates without proven production scene/prefab/data unlock routes.

### Current Blockers

- Prove the player has a real starter interaction that can acquire copper, or treat the copper access seam as the route blocker.
- Prove `quest_copper_sample` activation/completion in the real route.
- Prove a reachable powered fabricator can craft Copper Wire.
- Keep legacy root `Data_Copper` out of the route.
- Prove save/load restores the same route state.

Runtime microseconds saved: 0 claimed. This is route governance only.

## 2026-05-19 Global Authority And Platform Portability Snapshot R7

Scope: static architecture/platform audit after user asked whether current
SignalBus/GlobalRegistry/GlobalDataVault/data-bus direction is globally correct
and how ready the project is for PC tiers, macOS, standalone Quest/PICO, PCVR,
Steam Deck, and possible consoles.

Evidence class: STATIC_SOURCE / STATIC_DOC / PROJECT_SETTINGS / PACKAGE_SETTINGS
/ FILESYSTEM / OFFICIAL_PLATFORM_DOC. No Unity import, Play Mode, profiler,
GCMonitor, player build, Android/Quest/PICO build, Steam Deck run, macOS Metal
run, or console SDK validation was launched.

Report written:

- `Docs/Reports/2026-05-19_GLOBAL_AUTHORITY_AND_PLATFORM_PORTABILITY_AUDIT.md`

### Verdict

- Global authority direction is correct, high-risk, and not yet failed.
- `GlobalRegistry` is valid as cold identity/DI, but 6137 raw dot hits make it a
  danger-zone service catalog if agents keep adding slots.
- `SignalBus<T>` is the right first-party hot broadcast spine. `GlobalSignals`
  still has 259 publish call sites and must remain a bridge, not the default new
  traffic route.
- `HectonEventBus` is acceptable only as mod/API/cold boundary. Current 52 call
  sites require classification; any first-party hot use must migrate.
- `GlobalDataVault` has strong mechanics, but BufferID governance remains the
  corruption risk. Hard-cast ranges and numeric aliasing are platform/runtime
  blockers, not cosmetic issues.
- Windows flat PC is the only sensible first proof platform.
- Quest/PICO/PCVR are blocked by missing XR Management/OpenXR/provider settings.
- Steam Deck/Linux and macOS are plausible later but lack build, shader, storage,
  input, and native plugin proof.
- Consoles are future strategy only; current serialized ProjectSettings console
  fields are not readiness.

### Current Platform Blockers

- `Packages/manifest.json` lacks `com.unity.xr.management`,
  `com.unity.xr.openxr`, and Meta/PICO provider packages.
- `ProjectSettings/ProjectSettings.asset` still has empty
  `m_BuildTargetVRSettings`, Unity template Android application identifier,
  automatic Android target SDK, and custom Android manifest disabled.
- `Assets/AddressableAssetsData` and `Assets/_SourceData` exist but are empty.
- `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` is absent.
- `Assets/Plugins/x86_64/HectonAudioKernel.dll` has no observed Linux/macOS/
  Android equivalent in this static scan.

### Proof Ladder Recorded

1. Windows import/player route proof.
2. Copper Wire V0 route with profiler/GC/memory/VRAM evidence.
3. Content payload gate for DataMonolith and Addressables.
4. Linux/Steam Deck.
5. macOS Metal.
6. XR packages/provider setup.
7. PCVR.
8. Android ARM64 non-XR.
9. Quest 3 then Quest 2.
10. PICO.
11. Consoles only after platform constraints are real.

Runtime microseconds saved: 0 claimed. This is audit/governance only.

## 2026-05-19 Quest OpenXR Package Bootstrap R8

Scope: Quest package/bootstrap setup after user confirmed Android SDK/JDK/NDK were installed through Unity Hub and asked what to download/connect for Quest.

Evidence class: PACKAGE_SETTINGS / PROJECT_SETTINGS / STATIC_DOC / OFFICIAL_UNITY_PACKAGE_REGISTRY. No Unity import, Package Manager resolve, Play Mode, Android build, Quest install, or headset run was launched.

### Changed

- Added Unity XR packages to `Packages/manifest.json`:
  - `com.unity.xr.management` `4.6.0`
  - `com.unity.xr.openxr` `1.17.0`
  - `com.unity.xr.meta-openxr` `2.5.0`
- Enabled existing Android custom files in `ProjectSettings/ProjectSettings.asset`:
  - `useCustomMainManifest: 1`
  - `useCustomMainGradleTemplate: 1`
- Replaced Unity template Android id with `com.danatgames.hecton8`.
- Set explicit Android target SDK to `35` for Android 15 API-level policy alignment.

### Current Remaining Work

- Open Unity so Package Manager resolves and downloads the new packages, then let it regenerate `Packages/packages-lock.json`.
- In Project Settings -> XR Plug-in Management, enable OpenXR for Android and Standalone.
- In OpenXR feature groups, enable Meta Quest / Meta OpenXR features required by the current Unity 6.4 package set.
- Confirm `ProjectSettings/ProjectSettings.asset` no longer has empty `m_BuildTargetVRSettings` after XR settings are written.
- Do not add PICO yet. PICO is not in Unity registry by the names probed; PICO docs require importing the SDK package from disk and enabling PicoXR under XR Plug-in Management.

Runtime microseconds saved: 0 claimed. This is package/project bootstrap only.

## 2026-05-19 Documentation Gap Scanner And Platform Ladder Promotion R9

Scope: stable-doc scan for GlobalRegistry, EventBus, SignalBus, GlobalDataVault,
and platform proof ladder after the user asked whether direction and portability
readiness are globally correct.

Evidence class: STATIC_SOURCE / STATIC_DOC / PROJECT_SETTINGS / PACKAGE_SETTINGS
/ FILESYSTEM / PY_TOOL. No Unity import, Package Manager resolve, build,
profiler, GCMonitor, player run, or device proof was launched.

### Findings

- Stable global-authority docs are strong enough: the boundaries, operating
  model, setup playbook, route-card template, review checklist, and migration
  ledger already tell agents how not to turn registry/signal/vault/event tools
  into a global monolith.
- Stable platform ladder was the real gap. The proof order existed in the dated
  report but not as a durable architecture rule.
- Current source pressure remains high: `GlobalRegistry.` 6075 lines / 687
  files, `GlobalSignals.Publish` 259 lines / 91 files, `SignalBus` push/try-push
  285 lines / 101 files, direct `new NativeArray<...>` 1064 lines / 162 files.
- DataVault sovereignty gate currently fails closed:
  `direct=1064`, `allowed=6`, `forbidden=1058`, baseline missing.
- XR package bootstrap is present in `manifest.json`, but `packages-lock.json`
  has no XR lock entries, `m_BuildTargetVRSettings` is empty, and `Assets/XR` is
  absent. Quest/PCVR remain provider/settings/device-proof blocked.

### Done

- Added `Docs/ARCHITECTURE/PLATFORM_PORTABILITY_PROOF_LADDER.md`.
- Linked the ladder from `AGENTS.md`, `.codexrules/AGENTS.md`,
  `Docs/README.md`, `Docs/ARCHITECTURE/README.md`, and `Docs/QUALITY_GATES.md`.
- Appended R9 recapture to
  `Docs/Reports/2026-05-19_GLOBAL_AUTHORITY_AND_PLATFORM_PORTABILITY_AUDIT.md`.

### Verdict

Global direction remains correct and high-risk. The project is not globally
failing yet. The measurable product path is still Windows/Copper Wire first;
Steam Deck, macOS, PCVR, Quest/PICO, and consoles are scaffolded targets, not
readiness claims.

Runtime microseconds saved: 0 claimed. This is documentation/governance only.

## 2026-05-19 Current Global Direction And Platform Gate Refresh R9

Scope: follow-up static audit after the Quest/OpenXR bootstrap. User asked
again whether the current global-registry/signal-bus/data-vault direction is
globally correct and how ready the project is for PC tiers, macOS, Quest/PICO,
PCVR, Steam Deck, and consoles.

Evidence class: STATIC_SOURCE / STATIC_DOC / PROJECT_SETTINGS /
PACKAGE_SETTINGS. No Unity import, Package Manager resolve, build, Play Mode,
profiler, GCMonitor, Android/Quest install, PICO run, Steam Deck run, macOS
Metal run, or console SDK validation was launched.

### Current Static Counters

- `GlobalRegistry.` dot hits under first-party scripts: `6147`.
- `GlobalRegistry.Get<T>` hits: `0`.
- `HectonEventBus.Publish/Subscribe` hits: `48`.
- `GlobalSignals.Publish` hits: `259`.
- `SignalBus<...>` refs: `1303`.
- Direct `.Push(...)` / `.TryPush(...)` text hits: `302`.
- `GlobalDataVault` / `IDataVault` / `VaultBufferHandle` / `DataVault` refs:
  `4752`.
- Native collection refs: `15468`.
- XR/OpenXR refs: `127`.
- Android/Quest/PICO refs: `2836`.
- Mac/Metal refs: `2606`.
- Linux/Steam Deck refs: `217`.

### Verdict

- Global architecture direction remains correct and high-risk yellow, not a
  terminal failure.
- The correct interpretation is runtime government, not global plumbing:
  `GlobalRegistry` for cold identity, `SystemDispatcher` for phase/cadence,
  typed `SignalBus<T>` for first-party fan-out, `GlobalDataVault` for shared
  native state, `HectonEventBus` for mod/API/cold managed isolation.
- The risk is still global-object drift: registry breadth, `GlobalSignals`
  bridge growth, unclassified `HectonEventBus` calls, and DataVault BufferID
  ownership.
- Quest/PCVR improved from package-blocked to provider/settings/build-proof
  blocked. Manifest has XR package ids now; Unity lock/import and XR loader
  settings are still absent.

### Changed This Pass

- Updated `Docs/Reports/2026-05-19_GLOBAL_AUTHORITY_AND_PLATFORM_PORTABILITY_AUDIT.md`
  with current XR package/bootstrap state, refreshed counters, and corrected
  PCVR/Quest blocker wording.
- Created `Docs/Tasks/Status_HFI_AUDIT.md` so active HFI work has a local
  status checkpoint again.
- Tightened `XrPlatformReadinessValidator` to check Meta OpenXR package
  presence, custom Android manifest usage, custom Gradle usage, and ARM64-only
  target architecture.
- Tightened `PlatformCompatibilityAudit` to report Meta OpenXR, custom Android
  manifest, custom Gradle, and ARM64-only target rows.

### Platform Direction

- Windows flat PC: first proof target. Correct next move is Copper Wire V0 route
  plus profiler/GC/memory/VRAM proof.
- Low-end PC/MX350: architecture acknowledges the budget, but asset/shader/vendor
  weight still needs real capture.
- High-end PC: visual-overkill model is sound only after low baseline is stable.
- PCVR: OpenXR package ids are present; provider settings and headset smoke are
  still missing.
- Quest 2/3: Android package id/target SDK/custom manifest/custom Gradle and XR
  package ids are now in place; XR Plug-in Management/OpenXR/Meta feature
  groups, package lock, build/install/thermal/comfort proof remain missing.
- PICO: still separate SDK/provider path; do not assume Quest setup is PICO
  readiness.
- Steam Deck/Linux: plausible after Windows route; native plugin parity,
  storage, controller/UI, and frame pacing remain unproven.
- macOS: viable later; Metal shader compile and native plugin parity are
  unproven.
- Consoles: zero production readiness without SDK/devkit/certification path.

Runtime microseconds saved: 0 claimed. Validator changes affect editor/build
preprocess only; no player-frame code path was changed.

## 2026-05-19 Global Authority Scanner And Portability Deep Scan R10

Scope: current static deep scan for GlobalRegistry, SignalBus, GlobalSignals,
HectonEventBus, GlobalDataVault, system phases, and platform portability after
large concurrent project changes.

Evidence class: STATIC_SOURCE / PROJECT_SETTINGS / PACKAGE_SETTINGS /
FILESYSTEM. No Unity import, Package Manager resolve, build, Play Mode,
profiler, Android/Quest install, PICO run, PCVR headset smoke, Steam Deck run,
macOS Metal run, or console SDK validation was launched.

### Current Direction

The project is not globally failing right now. It is in high-risk yellow:
architecture direction is correct, runtime proof is missing, and global surface
pressure is now large enough to require strict governance.

Correct direction:

- `GlobalRegistry` remains a cold identity/registration spine, not visibly a
  generic hot `Get<T>` service locator in current grep.
- `SignalBus<T>` lanes are real typed unmanaged first-party traffic, with broad
  configuration and snapshot usage.
- `GlobalSignals` is acting as a bridge into typed lanes, not as a purely
  untyped event swamp.
- `HectonEventBus` explicitly declares itself as a managed mod bridge where
  first-party systems should consume SignalBus snapshots directly.
- `GlobalDataVault` has owner IDs, handles, generation checks, phase-gated
  defrag, and dump/black-box surfaces.
- `SystemDispatcher` has explicit PreSimulation, Simulation, PostSimulation,
  and VisualSync paths.

Architectural risks:

- `GlobalRegistry.` appears in 688 first-party files with 6169 dot hits. The
  registry is a pressure point even if generic `Get<T>` is not showing up.
- `GlobalSignals.Publish` has 259 hits. It must stay a migration bridge, not a
  convenience API for new first-party hot routes.
- `HectonEventBus.Publish/Subscribe` has 48 hits. Each site needs
  `MOD_API_COLD`, `FIRST_PARTY_COLD`, or `FIRST_PARTY_HOT` classification.
  `FIRST_PARTY_HOT` is wrong and must move to typed SignalBus or owner
  interface.
- 9 producer-touching SignalBus payloads lack obvious static config proof:
  `EncyclopediaUnlockSignal`, `EntityDepletedSignal`,
  `Hecton8.Core.Contracts.Signals.CameraFrustumSignal`,
  `Hecton8.Core.Contracts.Signals.CameraPositionSignal`,
  `Hecton8.Core.Contracts.Signals.CombatDamageSignal`, `PlayVoiceOverSignal`,
  `ResidencySectorDehydratedSignal`, `ResidencySectorHydratedSignal`,
  `ThermalUpdraftSignal`.
- 959 persistent native constructor text hits exist. Many may be legitimate
  cold or owner-local allocations, but DataVault/H8Memory sovereignty is not
  globally proven.
- 135 exact `Pack = 1` layouts exist. These are ARM64/Metal/Vulkan/Quest risk
  until each boundary is audited.

Hard blockers:

- `ProjectSettings/ProjectSettings.asset` still has
  `m_BuildTargetVRSettings: []`.
- `Packages/manifest.json` contains XR Management/OpenXR/Meta OpenXR ids, but
  `Packages/packages-lock.json` has zero matching lock entries. Unity has not
  resolved/imported packages in this evidence pass.
- No current platform runtime proof exists for Quest 2/3, PICO, PCVR, Steam
  Deck, macOS, or consoles.

Missing proof:

- Unity import and package resolve.
- Clean compile for current dirty tree.
- Windows Copper Wire V0 route.
- Profiler/GC/memory/VRAM proof.
- Dispatcher/signal/DataVault stress proof.
- XR provider loader, OpenXR feature groups, headset smoke, and standalone
  Android device captures.

### Platform Assessment

- Windows flat PC: correct first proof target.
- Low-end PC/MX350: direction correct, asset/shader/vendor pressure unproven.
- High-end PC: visual-overkill model is correct only after low baseline proof.
- PCVR: scaffolding exists; provider/settings/package-lock/headset proof missing.
- Quest 3: plausible first standalone XR target after Windows/PCVR proof.
- Quest 2: stress target; current URP Quest render scale 1 and MSAA 4 may be
  heavy until device profiling proves otherwise.
- PICO: not covered by Quest; separate SDK/provider/input/haptics proof needed.
- Steam Deck: plausible after Windows route; Linux/Proton/native plugin/storage
  and controller/UI proof missing.
- macOS: viable later; Metal shader/native plugin/signing proof missing.
- Consoles: zero production readiness without SDK/devkit/certification path.

### Action

Do not refactor code blindly during this pass. The correct autonomous
improvement was to update the report with the current blockers and exact
classification. Next code work should be narrow: signal-lane config proof,
EventBus site classification, Pack=1 boundary audit, and persistent native
allocation ownership ledger.

Runtime microseconds saved: 0 claimed. This pass changed audit/reporting only.

## 2026-05-19 BufferID Sovereignty Gate R11

Scope: DataVault `BufferID` identity governance after the global/platform audit
found DataVault ownership as the biggest remaining architectural risk.

Evidence class: STATIC_SOURCE / PY_TOOL / PY_UNIT_TEST. No Unity import,
compile, Play Mode, profiler, player build, save/load route, or device proof was
executed.

### What Was Wrong

- DataVault core mechanics are useful, but BufferID identity was still partly
  informal.
- Central `H8Memory.BufferID` initially had one duplicate numeric value:
  `70200`, shared by `SaveWorldPagerWriteArena` and
  `ConstructionBuilderOccupancy`.
- First-party scripts currently contain 579 local numeric `(BufferID)N` casts
  outside `H8Memory.cs` across 48 files.
- Some local casts collide with existing central enum names, so the risk is not
  theoretical: two domains can address the same Vault key even when C# compiles.

### What Was Done

- Added `Tools/BufferIDSovereigntyAudit.py`.
- Added `Tools/test_buffer_id_sovereignty_audit.py`.
- Generated:
  - `Docs/AgentLogs/BufferIDSovereigntyAudit_HFI_AUDIT.md`
  - `Docs/AgentLogs/BufferIDSovereigntyAudit_HFI_AUDIT.json`
- Promoted the gate into `QUALITY_GATES.md`,
  `GLOBAL_AUTHORITY_BOUNDARIES.md`, and
  `GLOBAL_AUTHORITY_MIGRATION_LEDGER.md`.

### Current Commands

```powershell
python Tools/test_buffer_id_sovereignty_audit.py
python Tools/BufferIDSovereigntyAudit.py
python Tools/BufferIDSovereigntyAudit.py --fail-on-duplicates
```

`--fail-on-duplicates` is a hard gate. `--fail-on-local-casts` is the final
migration gate after current debt is owned or removed.

Verification:

- `python Tools/test_buffer_id_sovereignty_audit.py`: PASS, 2 tests.
- `python Tools/BufferIDSovereigntyAudit.py`: PASS as report-only command,
  `duplicates=1`, `localCasts=579`, `castFiles=48` before R12 repair.
- `python Tools/BufferIDSovereigntyAudit.py --fail-on-duplicates`: expected
  FAIL before R12 repair because value `70200` was duplicated.

### Cinematic Cheats Used

None. This is identity/governance tooling, not simulation or visual work.

### Microseconds Saved

Runtime microseconds saved: 0 claimed. The value is preventing hidden
cross-domain Vault aliasing before it becomes runtime corruption on low-end,
Quest, Deck, macOS, or PCVR builds.

## 2026-05-19 BufferID Duplicate Repair R12

Scope: minimal source repair for the hard DataVault identity blocker found by
R11.

Evidence class: STATIC_SOURCE / PY_TOOL. No Unity import, compile, Play Mode,
profiler, player build, save/load route, or device proof was executed.

### What Was Wrong

`SaveWorldPagerWriteArena` and `ConstructionBuilderOccupancy` both used
`BufferID` value `70200`. That made two unrelated Vault buffers address the same
integer identity.

### What Was Done

- Preserved `SaveWorldPagerWriteArena = 70200`.
- Moved `ConstructionBuilderOccupancy` to free construction-adjacent value
  `70319`, directly before `ConstructionPreviewWrite = 70320`.
- Re-ran BufferID sovereignty audit.

### Verification

```powershell
python Tools/BufferIDSovereigntyAudit.py
python Tools/BufferIDSovereigntyAudit.py --fail-on-duplicates
```

Both commands exit `0` after R12. Latest R15 verification result:
`duplicates=0`, `localCasts=604`, `castFiles=50`.

### Remaining Debt

Local numeric `(BufferID)N` casts still exist across 48 files. That is migration
debt and must not be sold as full DataVault sovereignty.

### Microseconds Saved

Runtime microseconds saved: 0 claimed. This fixes identity aliasing; profiler
savings are not asserted.

## 2026-05-19 Global Direction And Portability Recap R13

Scope: current broad audit after R12 BufferID duplicate repair.

Evidence class: STATIC_SOURCE / STATIC_DOC / PROJECT_SETTINGS /
PACKAGE_SETTINGS / PY_TOOL. No Unity import, Package Manager resolve, dotnet
build, Unity Console, Play Mode, profiler, GCMonitor, player build, headset
smoke, Steam Deck run, macOS run, PICO run, or console SDK validation was
executed.

### What Was Found

- Dirty tree pressure is high: `999` `git status --short` entries, including
  `792` modified, `199` untracked, and `8` deleted entries.
- `GlobalRegistry.` appears `6174` times across `689` first-party script files.
- `GlobalRegistry.Get<T>` appears `0` times in the static scan.
- `SignalBus<...>` appears `1321` times across `198` files.
- `GlobalSignals.Publish` appears `259` times across `91` files.
- `HectonEventBus.Publish/Subscribe` appears `48` times across `20` files.
- DataVault refs are broad: `4808` matches across `272` files.
- Raw `new NativeArray<...>` remains `1064` matches across `162` files.
- `Pack = 1` remains `154` matches across `50` files.
- `GlobalQualityWeight` appears `1680` times across `217` files.
- XR/Quest scaffolding is broad in source, but package resolve/provider/device
  proof is absent.

### Verdict

Global direction is correct. Status remains high-risk yellow, not green.

Keep `GlobalRegistry`, `SignalBus<T>`, `GlobalDataVault`, and dispatcher. The
spine is coherent. The problem is proof and governance:

- Registry breadth can become a god catalog if new slots are added for
  convenience.
- `GlobalSignals.Publish` must keep shrinking toward owned bridge lanes or typed
  SignalBus routes.
- `HectonEventBus` needs classification so it stays mod/API/cold.
- DataVault duplicate identity is repaired, but local numeric `BufferID` casts
  and raw native ownership still block full sovereignty.
- Platform references are not platform proof.

### Platform Bands

- Windows flat PC: 50-60%.
- Low PC / MX350: 40-50%.
- Steam Deck / Linux / Proton: 30-40%.
- macOS / Metal: 25-35%.
- PCVR: 30-40%.
- Quest 3 standalone: 25-35%.
- Quest 2 standalone: 20-30%.
- PICO standalone: 10-15%.
- Consoles: 5-10%.

These are static confidence bands only. They are not build, runtime, profiler,
or certification proof.

### Next Hard Order

1. Unity package resolve and `packages-lock.json`.
2. XR Plug-in Management/OpenXR provider settings through Unity.
3. Windows Copper Wire V0 proof.
4. EventBus classification.
5. SignalBus config proof for the 9 suspect producer payloads.
6. BufferID local-cast burn-down or ledger.
7. Pack=1 ARM64 boundary audit.
8. Low-end PC capture, then Deck/macOS/PCVR/Quest/PICO/console ladder.

Runtime microseconds saved: 0 claimed. R13 is audit/governance only; R12 removed
a real BufferID alias but no profiler result exists.

## 2026-05-19 Unified Global Authority Gate R14

Scope: safe tooling improvement requested by current architecture review and
subagent recommendation.

Evidence class: STATIC_SOURCE / PY_TOOL / PY_UNIT_TEST. No Unity import, dotnet
build, Play Mode, profiler, player build, or device proof was executed.

### What Was Wrong

Global-authority review had several separate scans: registry, bus, DataVault,
BufferID, native collections, `Pack = 1`, and SignalBus producer/config proof.
That made it too easy for future agents to cite one good number while ignoring
another pressure surface.

### What Was Done

- Added `Tools/GlobalAuthorityGate.py`.
- Added `Tools/test_global_authority_gate.py`.
- Promoted the command into `QUALITY_GATES.md` and
  `GLOBAL_AUTHORITY_MIGRATION_LEDGER.md`.

Default hard failures:

- generic `GlobalRegistry.Get<T>` / `TryGet<T>` usage above threshold 0.
- duplicate central `BufferID` numeric values.

Default warnings:

- broad `GlobalRegistry.` surface.
- `GlobalSignals.Publish` bridge pressure.
- `HectonEventBus.Publish/Subscribe` classification debt.
- raw native collection / `new NativeArray<...>` pressure.
- local numeric `(BufferID)N` cast debt.
- `Pack = 1` ARM64 layout debt.
- SignalBus producer types without obvious config proof.

### Verification

```powershell
python Tools/test_global_authority_gate.py
python Tools/GlobalAuthorityGate.py
```

Results:

- Unit tests: PASS, 2 tests.
- Gate: PASS_WITH_WARNINGS.
- Current hard checks: `globalRegistryGenericGet=0`,
  `bufferId duplicates=0`.
- Current warning pressure: `GlobalRegistry.` 6185, `GlobalSignals.Publish`
  259, `HectonEventBus` pub/sub 46, SignalBus suspects 9, local BufferID casts
  604, `new NativeArray<...>` 1057, `Pack = 1` 154.

### Microseconds Saved

Runtime microseconds saved: 0 claimed. This is offline governance tooling.

## 2026-05-19 Platform Proof-Adjusted Correction R15

Scope: integrate the dedicated platform portability review into the active HFI
audit state.

Evidence class: STATIC_DOC / STATIC_SOURCE / PROJECT_SETTINGS /
PACKAGE_SETTINGS / FILESYSTEM. No Unity import, dotnet build, Play Mode,
profiler, GCMonitor, player build, headset smoke, Steam Deck run, macOS run,
PICO run, or console SDK validation was executed.

### Correction

R13 static bands were useful for scaffolding direction. R15 records the stricter
proof-adjusted bands:

- Windows low / MX350: static 25-35%, proven 0-5%.
- Windows mid PC: static 35-45%, proven 0-5%.
- Windows high PC: static 40-50%, proven 0-5%.
- Steam Deck / Linux native: static 15-25%, proven 0%.
- Proton: static 10-20%, proven 0%.
- macOS / Metal: static 10-20%, proven 0-2%.
- PCVR: static 15-25%, proven 0%.
- Quest 3 standalone: static 15-25%, proven 0%.
- Quest 2 standalone: static 10-20%, proven 0%.
- PICO standalone: static 3-8%, proven 0%.
- Consoles: static 0-3%, proven 0%.

### Confirmed Blockers

- XR package ids are present only in `Packages/manifest.json`; lock/import proof
  is absent from `Packages/packages-lock.json`.
- `m_BuildTargetVRSettings: []` remains.
- `ProjectSettings/XRSettings.asset` has only legacy VR keys, not OpenXR
  provider proof.
- Windows-only native plugins were found: `HectonAudioKernel.dll` and
  `liblz4.dll`; no Linux/macOS/Android equivalents were found in checked plugin
  paths.
- `Assets/AddressableAssetsData` exists but has 0 entries.
- `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` is absent.

### Verdict

Platform direction is sane, but proven readiness is near zero outside static
scaffolding. Windows Copper Wire proof stays first. Quest/PCVR/Deck/macOS/PICO
claims remain blocked until artifacts exist.

Runtime microseconds saved: 0 claimed. This is proof classification only.

## 2026-05-19 Current Gate Recapture R16

Scope: repeated user request for current global direction and platform posture
after more concurrent workspace churn.

Evidence class: PY_TOOL / STATIC_SOURCE / STATIC_DOC. No Unity import, dotnet
build, Play Mode, profiler, player build, or device proof was executed.

### Current Gate Results

- `python Tools/GlobalAuthorityGate.py`: PASS_WITH_WARNINGS.
- `python Tools/BufferIDSovereigntyAudit.py --fail-on-duplicates`: PASS.
- `python Tools/DataVaultSovereigntyAudit.py --fail-on-regression`: FAIL,
  baseline missing; `direct=1057`, `allowed=6`, `forbidden=1051`,
  `files=160`, `declarations=4700`, `forbiddenDeclarations=4694`,
  `declarationFiles=327`.

### Current Counts

- `GlobalRegistry.`: 6185 hits / 691 files.
- `GlobalRegistry.Get/TryGet`: 0 hits / 0 files.
- `SignalBus<...>` refs: 1323 hits / 199 files.
- `SignalBus<...>.Push/TryPush`: 288 hits / 104 files.
- `SignalBus<...>.Configure`: 221 hits / 44 files.
- `SignalBus<...>.EnsureInitialized`: 264 hits / 56 files.
- `GlobalSignals.Publish`: 259 hits / 91 files.
- `HectonEventBus.Publish/Subscribe`: 46 hits / 20 files.
- DataVault refs: 4896 hits / 280 files.
- `new NativeArray<...>`: 1057 hits / 161 files.
- Native collection refs: 15599 hits / 626 files.
- `Pack = 1`: 156 hits / 51 files.
- local numeric `(BufferID)N`: 609 hits / 50 files.
- SignalBus producer/config suspects: 9.
- central BufferID duplicates: 0.

### Verdict

No terminal architecture failure is visible. The direction remains correct, but
the warning surface is not decreasing. Keep the runtime-government spine; stop
selling scaffold as readiness; use Windows/Copper Wire plus Unity/profiler proof
as the next real acceptance boundary.

Runtime microseconds saved: 0 claimed. This is current-state audit only.

## 2026-05-19 Final Recapture For Repeated Request R17

Scope: final current-state recapture after repeated user request.

Evidence class: PY_TOOL / STATIC_SOURCE. No Unity import, dotnet build, Play
Mode, profiler, player build, or device proof was executed.

### Latest Gates

- `GlobalAuthorityGate.py`: PASS_WITH_WARNINGS.
- `BufferIDSovereigntyAudit.py --fail-on-duplicates`: PASS.
- `DataVaultSovereigntyAudit.py --fail-on-regression`: FAIL, baseline missing.

### Latest Counts

- `GlobalRegistry.`: 6185 / 691 files.
- `GlobalRegistry.Get/TryGet`: 0 / 0 files.
- `SignalBus<...>` refs: 1323 / 199 files.
- `SignalBus<...>.Push/TryPush`: 288 / 104 files.
- `SignalBus<...>.Configure`: 221 / 44 files.
- `SignalBus<...>.EnsureInitialized`: 264 / 56 files.
- `GlobalSignals.Publish`: 259 / 91 files.
- `HectonEventBus.Publish/Subscribe`: 46 / 20 files.
- DataVault refs: 4897 / 280 files.
- `new NativeArray<...>`: 1057 / 161 files.
- Native collection refs: 15603 / 626 files.
- `Pack = 1`: 156 / 51 files.
- local numeric `(BufferID)N`: 609 / 50 files.
- SignalBus producer/config suspects: 9.
- central BufferID duplicates: 0.

### Verdict

No change from R16. Global direction is correct. The hard identity/service
tripwires are clean. The project is not globally failing yet, but warning
pressure remains high and platform readiness remains proof-blocked.

Runtime microseconds saved: 0 claimed. This is audit state only.

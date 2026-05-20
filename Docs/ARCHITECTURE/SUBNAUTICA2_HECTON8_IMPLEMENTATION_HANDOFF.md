# Subnautica 2 To HECTON-8 Implementation Handoff

Date: 2026-05-17
Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.

R45 root/architecture R43/R44 residue/proof-artifact/source-counter correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md`) is the latest local static DOC_GLOBAL boundary for architecture/root documentation. R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41 remains the prior global-authority/internal-residue correction; R40 remains the prior R38-residue/source-counter correction; R39 remains the prior authority-counter/proof-wording correction; R38/R37/R36/R35/R34 remain prior static correction layers. Runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## 2026-05-20 DOC_GLOBAL R38 Current Boundary Note

This handoff remains clean-room source/docs orientation, not implementation, platform, player-build, profiler, or Steam Deck proof. Current DOC_GLOBAL boundary is `Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md`; R44 remains prior at `Docs/Reports/2026-05-20_DOCUMENTATION_R44_ROOT_ARCHITECTURE_INTERNAL_RESIDUE_EXACT_ROUTE_FIELDS_LOCAL.md`; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current static gates: `Tools/AtlasCheck.py` remains red on `ATLAS_CHECK_FAIL references=6741 missing=59` (one Dynamic Decals missing vendor asset ref, RealtimeCSG vendor icon/readme image refs, and missing HabitatDamageBakePipeline source ref in the current atlas); `Docs/Modding/Validate_Mod_API_Static.ps1` passes (`Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`) as static-tool orientation only. Runtime proof remains absent.

Owner lane: SUBNAUTICA_RESEARCHER
Scope: clean-room external reference research mapped to current HECTON-8 source files.

This is not a request to clone Subnautica or Subnautica 2. Do not extract, decompile, copy, or structurally imitate proprietary assets, binaries, levels, scripts, save data, creature logic, or UI text. The useful material is tactical: content packaging shape, route proof, platform proof, visual fakes, player trust gaps, and build gates.

## Current Verdict

Subnautica 2's screenshot surface is catchable with HECTON-8's fog, silt, cockpit framing, impostor density, and Overkill tier effects. The real threat is operational: stable content payloads, first-hour route proof, creature stimulus contracts, platform presets, comfort settings, save/schema discipline, and live-update trust.

HECTON-8 already has many runtime primitives, but too many contracts are not yet backed by populated payloads or build-blocking proof. The next work should harden the foundation before adding more spectacle.

## Source Map

| Area | Current File Evidence | What Is Real | Gap | Next Gate |
|---|---|---|---|---|
| Static data monolith | `Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithCompiler.cs`, `Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs`, `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`, `Data/Balance/*.csv` | Compiler targets `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`, writes via temp-validate-promote, and registers a prebuild bake/validate gate. Arena loads StreamingAssets through MMF/FileStream-to-Vault, with Android/Quest URI staging. | `static_data.h8bin` is absent until the baker runs under Unity. Player/non-editor boot fails fatal when the monolith is missing/invalid. Balance CSVs may omit `hash32`; the compiler derives FNV-1a hashes and fails only mismatches. | Capture guarded Unity import/build proof and keep the prebuild gate red when monolith is absent, stale, corrupt, misaligned, or schema-incompatible. |
| Small balance bake | `Assets/_Project/Scripts/Core/Data/H8DataBaker.cs`, `Assets/_Project/Scripts/Core/Data/StaticDataStore.cs`, `Data/Balance/Baked/H8StaticData.bin` | A small balance binary exists and can serve dev/static data reads. | It is not the StreamingAssets monolith. Treating it as the world/content monolith is a false positive. | Decide one source-of-truth path, then make the other dev-only or a section producer. |
| ContentAuthority object assets | `Assets/_Project/Scripts/Core/Content/Editor/ContentAuthorityBuildValidators.cs`, `Assets/_Project/Scripts/Core/Content/ContentRuntimeServices.cs`, `Assets/AddressableAssetsData` | Validators are build-failing and expect `Core`, `High_Res`, and `Overkill` Unity object/visual groups. Runtime has hash-map lookup, refcounting, VRAM ledger, VFX prewarm, and tier policy. This is separate from DataMonolith/world-static truth. | `Assets/AddressableAssetsData` is empty. No populated `ContentAssetHashMap` or VFX prewarm manifest assets were found. | Generate minimal object-asset settings/groups, hash maps, and VFX manifests. Then run ContentAuthority as a real prebuild gate. |
| Static world dressing batches | `Assets/_Project/Scripts/Core/Content/ObjectBatchBase.cs`, `Assets/_Project/Scripts/Core/Content/VisibilityProxyBase.cs`, `ContentAuthorityBuildValidators.ValidateObjectBatchPayloads` | ABI exists for static object batches, with packed batch instances and visual-hash validation. | No concrete `ObjectBatchBase` or `VisibilityProxyBase` implementations/assets were found in the current scan. | Add a sector object-batch generator for wrecks, debris, flora clusters, and landmark silhouettes. No runtime GameObject swarm. |
| First-hour route | `Assets/_Project/Scripts/Gameplay/FirstHourDirector.cs`, `Assets/_Project/Scripts/Editor/ContentSanityValidator.cs`, `Assets/_Project/Scripts/Editor/ScanIntelValidator.cs`, `Assets/_Project/Scripts/ScanLogSystem.cs`, `Assets/_Project/Data/Lore/Quests`, `Assets/_Project/Data/Crafting/Recipes` | There is a first-hour director, scan log, quest/craft assets, and validation surface. | `ContentSanityValidator` and `ScanIntelValidator` are mostly menu/scene proof. Scan-gate warnings are not a deterministic build route proof. | Add a build-blocking route verifier: spawn state -> collect item -> scan -> unlock recipe -> craft -> save/load -> quest advance. |
| Biome visual authority | `Assets/_Project/Data/Biomes/RuntimeVisualProfiles`, `Assets/_Project/Scripts/BiomeMatrixDirector.cs`, `Assets/_Project/Scripts/Rendering/HectonUberNoirRuntimeBridge.cs` | 216 runtime visual profile assets exist. Biome matrix and noir runtime hooks exist. | Need proof that required fog, silt, palette, silhouette, caustic, audio, and reward fields are populated and connected to content residency. | Add biome visual schema validator and require low/mid/high/ultra budget fields per profile. |
| Visual Overkill tiers | `Assets/_Project/Scripts/Core/Contracts/HectonVisualOverkillContract.cs`, `Assets/_Project/Scripts/Core/Content/ContentRuntimeServices.cs`, `Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs`, visor/silt/hull systems | Low tier has Dear Lie flags. High/Ultra define raymarch/POM/SSS/silt/salt/hull budgets. Thermal adapter drives Dear Lie and Overkill shader flags. | The Overkill payload group is not populated. Visual budgets exist as code, not as validated content packs. | Bind salt crystals, volumetric silt wakes, hull dents, POM/raymarch assets, and flora density to `Overkill` group with fallback content in `Core`. |
| Creature stimuli and acoustic threat | `Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs`, `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs`, `Assets/_Project/Scripts/World/WorldSpatialHashGrid.cs`, `Assets/_Project/Scripts/Core/GlobalSignals.cs` | Acoustic runtime consumes typed `SignalBus<MovementAcousticSignal>` and `SignalBus<AcousticPingSignal>` via `ReadOnlySpan<T>`. Sargassum has sensory black box and typed swarm signals. | Legacy `GlobalSignals.Publish`, `PhysicsEventBus`, `HectonEventBus`, delegates, and managed `Action` surfaces still exist. | Create a signal inventory. Promote critical creature/route/world stimuli to typed lanes. Leave legacy only as documented bridge or remove it. |
| Black-box telemetry | `Assets/_Project/Scripts/Core/GlobalTelemetryBus.cs`, `Assets/_Project/Scripts/CrashTelemetryBuffer.cs`, `Assets/_Project/Scripts/Core/BlackBoxHeartbeatThread.cs`, acoustic/sargassum black boxes | Central telemetry, crash export, heartbeat monitor, and several 300-frame rings exist. | Coverage is not uniform across every P0 foundation system. Content payload generation, first-hour route verification, world paging, and platform gates need explicit last-state records. | Add black-box checklist per P0 system and dump path proof. No "unknown crash" for content/route/pager failures. |
| Platform proof | `Assets/_Project/Scripts/Editor/Build/PlatformCompatibilityAudit.cs`, `Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs`, `ContentAuthorityBuildValidators.ValidateComputeShaderThreadGroups` | Editor audit classifies Android/Quest, macOS/Metal, Linux/Steam Deck, XR, Addressables, plugin, and storage risks. Thermal adapter has Low/Mx350, Mid, High, Ultra policy. | Audit is diagnostic. It does not equal player build/device proof. | Add CI/device matrix gates: Windows, Linux/Steam Deck storage, macOS Metal shader import, Android/Quest IL2CPP/ARM64, XR comfort, and high-end Overkill pack. |

## P0 Work Orders

1. **Monolith Build Gate**
   - Files: `H8DataMonolithCompiler.cs`, `H8StaticDataArena.cs`, `GameBootstrapper.cs`, `Data/Balance/*.csv`.
   - Required proof: `static_data.h8bin` generated in `Assets/StreamingAssets/Hecton8/DataMonolith`, schema hash recorded, app-version hash recorded, boot fails in non-dev builds when missing.
   - Low tier: smallest required static tables only.
   - High/Ultra: optional visual/content sections allowed only if ABI and fallback sections stay identical.

2. **ContentAuthority Object-Payload Bootstrap**
   - Files: `ContentAuthorityBuildValidators.cs`, `ContentRuntimeServices.cs`, `ContentAssetHashMap.cs`, `ContentVfxPrewarmManifest`.
   - Required proof: `Core`, `High_Res`, `Overkill` Unity object asset groups exist where deliberately chosen; at least one valid hash-map asset is bound; VFX prewarm manifest validates; no first-party `Resources.Load` leak. `static_data.h8bin` and sector payload manifests remain separate static/world-data authority.
   - Low tier: `Core` group must cover all mandatory gameplay.
   - Ultra: `Overkill` group must be optional, downloadable-safe, XR-disabled when policy says so, and never required for route completion.

3. **First-Hour Route Verifier**
   - Files: `FirstHourDirector.cs`, `ContentSanityValidator.cs`, `ScanIntelValidator.cs`, `ScanLogSystem.cs`, quest and recipe assets.
   - Required proof: deterministic route from first salvage to scanner/repair/craft/save/load. Validator must fail build for missing route-critical scan entries, recipe gates, quest IDs, or item prefabs.
   - Low tier: route works with no high-density VFX.
   - Ultra: route can add PDA, VO, hologram, and cinematic visor presentation without changing progression facts.

4. **ObjectBatch World Dressing**
   - Files: `ObjectBatchBase.cs`, `VisibilityProxyBase.cs`, world/page payload systems.
   - Required proof: static wreck/debris/flora dressing exists as packed batches with hash-map visual references. No tactical world density should depend on spawning thousands of GameObjects.
   - Low tier: impostor and sparse silhouette batches.
   - Ultra: dense debris, local decals, material variation, volumetric silt anchors.

5. **Biome Visual Authority Gate**
   - Files: runtime visual profiles, `BiomeMatrixDirector.cs`, `HectonUberNoirRuntimeBridge.cs`, `ContentTieredGroupPolicy`.
   - Required proof: every shipped biome profile has fog, palette, silt, visibility, audio/material, landmark, reward hook, and tier budgets.
   - Low tier: 1D LUTs, triangle noise, billboard clusters, projected caustics.
   - Ultra: visor salt, volumetric silt wakes, hull dents, raymarched fog, 16-tap POM, SSS.

6. **Stimulus Lane Cleanup**
   - Files: `AcousticEchoLocationRuntime.cs`, `SargassumMicroFaunaBoids.cs`, `GlobalSignals.cs`, legacy event buses.
   - Required proof: creature threat reacts through typed light/sound/movement/action lanes. No duplicate signal contract for the same gameplay fact.
   - Low tier: scalar stimulus scores and animation state fakes.
   - Ultra: secondary motion, tentacle/appendage presentation, silt/particle reaction layers.

7. **Platform Matrix Proof**
   - Files: `PlatformCompatibilityAudit.cs`, `ThermalDynamicResolutionAdapter.cs`, compute shaders, StreamingAssets readers, native plugin metadata.
   - Required proof: separate logs for Windows PC, Linux/Steam Deck, macOS Metal import, Android/Quest IL2CPP, and high-end PC Overkill.
   - Low tier: no 1024+ thread group abuse, no unbounded synchronous I/O spikes, no heavy preload audio.
   - Ultra: 4090-class high tier is not punished by mobile defaults.

## P1 Work Orders

1. **Save And Schema Cadence**
   - Make content versions, route versions, and monolith schema versions explicit.
   - Subnautica 2 is Early Access. HECTON-8 must assume repeated content updates and old saves.

2. **Comfort And Trust**
   - Verify FOV, motion, input, accessibility, and permissions settings. Player trust failures are competitive damage, not polish.

3. **Feedback Ingestion**
   - Extend telemetry to answer: where first-hour players stop, which route item fails, which platform hitches, and which content hash is missing.

4. **Co-op Ready State Boundaries**
   - Do not build co-op now unless commanded, but stop writing systems that would make future shared state impossible.
   - Separate player inventory, world mutation, base edits, route flags, and content unlocks.

## Explicit Non-Goals

- Do not move HECTON-8 back to standard Unity Addressables as the world-paging architecture.
- Do not use Subnautica or Subnautica 2 proprietary assets, binary data, decompiled scripts, creature setups, or world cache payloads.
- Do not chase screenshot parity before `static_data.h8bin`, ContentAuthority payloads, first-hour route proof, and platform audit gates are real.
- Do not make Overkill visuals route-critical.
- Do not accept menu-only validators as shipping proof.

## Proof Limits

This handoff is documentation and source audit only. No runtime code was changed. No Unity Editor import, Play Mode, player build, Android/Quest, macOS/Metal, Steam Deck, or profiler validation was run during this pass. Microsecond savings are 0us measured. All future performance claims require profiler evidence on target hardware.

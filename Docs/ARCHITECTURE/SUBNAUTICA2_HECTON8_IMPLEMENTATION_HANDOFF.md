# Subnautica 2 To HECTON-8 Implementation Handoff

Date: 2026-05-17

Status: PENDING VERIFICATION

## Current Verdict

Subnautica 2's screenshot surface is catchable with HECTON-8 fog, silt, cockpit framing, impostor density, and Overkill tier effects.

Real threat is operational: content payloads, first-hour route proof, creature stimulus contracts, platform presets, comfort settings, save/schema discipline, live-update trust.

HECTON-8 has runtime primitives, but many contracts lack populated payloads or build-blocking proof. Next work should harden foundation before adding spectacle.

## Source Map

Small balance bake:

- Evidence: `H8DataBaker.cs`, `StaticDataStore.cs`, `Data/Balance/Baked/H8StaticData.bin`.
- Real: small balance binary can serve dev/static reads.
- Gap: not the StreamingAssets monolith.
- Gate: choose one source-of-truth path; make the other dev-only or a section producer.

ContentAuthority object assets:

- Evidence: `ContentAuthorityBuildValidators.cs`, `ContentRuntimeServices.cs`, `Assets/AddressableAssetsData`.
- Real: validators expect `Core`, `High_Res`, `Overkill`; runtime has hash-map lookup, refcounting, VRAM ledger, VFX prewarm, tier policy.
- Gap: `Assets/AddressableAssetsData` is empty; no populated `ContentAssetHashMap` or VFX prewarm manifest assets found.
- Gate: generate settings/groups, hash maps, VFX manifests; run ContentAuthority as prebuild gate.

Static world dressing batches:

- Evidence: `ObjectBatchBase.cs`, `VisibilityProxyBase.cs`, `ContentAuthorityBuildValidators.ValidateObjectBatchPayloads`.
- Real: packed batch ABI and visual-hash validation exist.
- Gap: no concrete `ObjectBatchBase` or `VisibilityProxyBase` implementations/assets found.
- Gate: add sector object-batch generator for wrecks, debris, flora clusters, landmark silhouettes.

First-hour route:

- Evidence: `FirstHourDirector.cs`, `ContentSanityValidator.cs`, `ScanIntelValidator.cs`, `ScanLogSystem.cs`, quest/craft assets.
- Real: first-hour director, scan log, quest/craft assets, validation surface exist.
- Gap: menu/scene proof is not deterministic build route proof.
- Gate: build-blocking route verifier for spawn -> collect -> scan -> unlock -> craft -> save/load -> quest advance.

Biome visual authority:

- Evidence: runtime visual profiles, `BiomeMatrixDirector.cs`, `HectonUberNoirRuntimeBridge.cs`.
- Real: 216 runtime visual profile assets plus runtime hooks.
- Gap: field population and content-residency links unproven.
- Gate: biome visual schema validator with minimum/intermediate/high/maximum budget fields.

Visual Overkill budgets:

- Evidence: `HectonVisualOverkillContract.cs`, `ContentRuntimeServices.cs`, `ThermalDynamicResolutionAdapter.cs`, visor/silt/hull systems.
- Real: minimum Dear Lie flags; high/max raymarch, POM, SSS, silt, salt, hull budgets.
- Gap: `Overkill` payload group not populated.
- Gate: bind salt crystals, silt wakes, hull dents, POM/raymarch assets, flora density to `Overkill`; keep `Core` fallback.

Creature stimuli and acoustic threat:

- Evidence: `AcousticEchoLocationRuntime.cs`, `SargassumMicroFaunaBoids.cs`, `WorldSpatialHashGrid.cs`, `GlobalSignals.cs`.
- Real: acoustic runtime consumes typed `SignalBus<MovementAcousticSignal>` / `SignalBus<AcousticPingSignal>`; Sargassum has black box and typed swarm signals.
- Gap: legacy `GlobalSignals.Publish`, `PhysicsEventBus`, `HectonEventBus`, delegates, managed `Action` surfaces remain.
- Gate: inventory signals; promote critical stimuli to typed lanes; leave legacy only as documented bridge.

Black-box telemetry:

- Evidence: `GlobalTelemetryBus.cs`, `CrashTelemetryBuffer.cs`, `BlackBoxHeartbeatThread.cs`, acoustic/sargassum rings.
- Real: central telemetry, crash export, heartbeat monitor, several 300-frame rings.
- Gap: P0 coverage not uniform.
- Gate: black-box checklist per P0 system and dump path proof.

Platform proof:

- Evidence: `PlatformCompatibilityAudit.cs`, `ThermalDynamicResolutionAdapter.cs`, `ContentAuthorityBuildValidators.ValidateComputeShaderThreadGroups`.
- Real: editor audit classifies Android/Quest, macOS/Metal, Linux/Steam Deck, XR, Addressables, plugin, storage risks.
- Gap: diagnostic audit is not player/device proof.
- Gate: CI/device matrix for Windows, Linux/Steam Deck, macOS Metal, Android/Quest IL2CPP/ARM64, XR comfort, high-end Overkill pack.

### Static Data Monolith

| Field | Fact |
|---|---|
| Source files | `H8DataMonolithCompiler.cs`; `H8StaticDataArena.cs`; `GameBootstrapper.cs`; `Data/Balance/*.csv` |
| Output target | `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` |
| Compiler behavior | temp-write, validate, promote, prebuild bake/validate gate |
| Runtime load | StreamingAssets via MMF/FileStream-to-Vault; Android/Quest URI staging |
| Current artifact | exists in X_012 scan; Unity/player boot proof pending |
| Fatal gate | non-editor player boot rejects missing or invalid monolith |
| CSV hashes | missing `hash32` is derived by FNV-1a; mismatches fail |
| Next gate | guarded Unity import/build proof; keep prebuild red for absent, stale, corrupt, misaligned, or schema-incompatible payload |

## P0 Work Orders

1. **Monolith Build Gate**

   - Files: `H8DataMonolithCompiler.cs`, `H8StaticDataArena.cs`, `GameBootstrapper.cs`, `Data/Balance/*.csv`.

   - Required proof: `static_data.h8bin` generated in `Assets/StreamingAssets/Hecton8/DataMonolith`, schema hash recorded, app-version hash recorded, boot fails in non-dev builds when missing.

   - Minimum quality: smallest required static tables only.
   - High and maximum quality: optional visual/content sections allowed only if ABI and fallback sections stay identical.

2. **ContentAuthority Object-Payload Bootstrap**

   - Files: `ContentAuthorityBuildValidators.cs`, `ContentRuntimeServices.cs`, `ContentAssetHashMap.cs`, `ContentVfxPrewarmManifest`.

   - Required proof: `Core`, `High_Res`, `Overkill` Unity object asset groups exist where chosen.
   - Required proof: at least one valid hash-map asset is bound.
   - Required proof: VFX prewarm manifest validates.
   - Required proof: no first-party `Resources.Load` leak.
   - Boundary: `static_data.h8bin` and sector manifests remain separate static/world-data authority.

   - Minimum quality: `Core` group must cover all mandatory gameplay.
   - Maximum quality: `Overkill` group must be optional, downloadable-safe, XR-disabled when policy says so, and never required for route completion.

3. **First-Hour Route Verifier**

   - Files: `FirstHourDirector.cs`, `ContentSanityValidator.cs`, `ScanIntelValidator.cs`, `ScanLogSystem.cs`, quest and recipe assets.

   - Required proof: deterministic route from first salvage to scanner/repair/craft/save/load. Validator must fail build for missing route-critical scan entries, recipe gates, quest IDs, or item prefabs.

   - Minimum quality: route works with no high-density VFX.
   - Maximum quality: route can add PDA, VO, hologram, and cinematic visor presentation without changing progression facts.

4. **ObjectBatch World Dressing**

   - Files: `ObjectBatchBase.cs`, `VisibilityProxyBase.cs`, world/page payload systems.

   - Required proof: static wreck/debris/flora dressing exists as packed batches with hash-map visual references. No tactical world density should depend on spawning thousands of GameObjects.

   - Minimum quality: impostor and sparse silhouette batches.
   - Maximum quality: dense debris, local decals, material variation, volumetric silt anchors.

5. **Biome Visual Authority Gate**

   - Files: runtime visual profiles, `BiomeMatrixDirector.cs`, `HectonUberNoirRuntimeBridge.cs`, `ContentTieredGroupPolicy`.

   - Required proof: every shipped biome profile has fog, palette, silt, visibility, audio/material, landmark, reward hook, and tier budgets.

   - Minimum quality: 1D LUTs, triangle noise, billboard clusters, projected caustics.
   - Maximum quality: visor salt, volumetric silt wakes, hull dents, raymarched fog, 16-tap POM, SSS.

6. **Stimulus Lane Cleanup**

   - Files: `AcousticEchoLocationRuntime.cs`, `SargassumMicroFaunaBoids.cs`, `GlobalSignals.cs`, legacy event buses.

   - Required proof: creature threat reacts through typed light/sound/movement/action lanes. No duplicate signal contract for the same gameplay fact.

   - Minimum quality: scalar stimulus scores and animation state fakes.
   - Maximum quality: secondary motion, tentacle/appendage presentation, silt/particle reaction layers.

7. **Platform Matrix Proof**

   - Files: `PlatformCompatibilityAudit.cs`, `ThermalDynamicResolutionAdapter.cs`, compute shaders, StreamingAssets readers, native plugin metadata.

   - Required proof: separate logs for Windows PC, Linux/Steam Deck, macOS Metal import, Android/Quest IL2CPP, and high-end PC Overkill.

   - Minimum quality: no 1024+ thread group abuse, no unbounded synchronous I/O spikes, no heavy preload audio.
   - Maximum quality: 4090-class quality is not punished by mobile defaults.

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

This handoff is documentation and source audit only.

- Runtime code changed: no.
- Unity Editor import / Play Mode / player build: not run.
- Platform validation: no Android/Quest, macOS/Metal, or Steam Deck run.
- Profiler validation: not run.
- Measured savings: `0us`.
- Future performance claims require profiler evidence on target hardware.

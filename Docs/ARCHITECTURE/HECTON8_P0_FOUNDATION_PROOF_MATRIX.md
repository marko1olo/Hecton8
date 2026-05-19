# HECTON-8 P0 Foundation Proof Matrix

Date: 2026-05-17
Status: ACTIVE REFERENCE / RUNTIME PENDING

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## 2026-05-19 DOC_GLOBAL R28 Interior Note

R28 reread confirmed this matrix remains static proof-orientation only. No row becomes Unity import, Console, Play Mode, profiler, GCMonitor, Memory Profiler, player-build, or scene-wiring proof unless it links a fresh artifact. Current root/architecture boundary is `Docs/Reports/2026-05-19_DOCUMENTATION_R28_ROOT_ARCHITECTURE_INTERIOR_BOUNDARY_LOCAL.md`, with R27 source counters retained until a newer counter pass reruns them. Current static gates: `Tools/AtlasCheck.py` remains red on `57` RealtimeCSG vendor references; `Docs/Modding/Validate_Mod_API_Static.ps1` now passes (`Status=PASS`, `SchemaRevision=14`, `SourceSignals=160`, `ModCommandSizeBytes=64`).

Lane: SUBNAUTICA_RESEARCHER
Scope: clean-room comparison against local HECTON-8 source, local Subnautica file taxonomy, and public Subnautica ecosystem sources.

## Legal Boundary

This document does not authorize copying Subnautica or Subnautica 2 assets, binary payloads, decompiled code, private data, or proprietary schemas.

Allowed use:

- Public source/repository metadata.
- Clean-room file taxonomy and directory-shape comparison.
- Architectural patterns that are generic enough to reimplement independently.
- HECTON-8 source and documentation facts.

Forbidden use:

- Extracting or reusing commercial game assets.
- Parsing proprietary cache payloads for content.
- Copying GPL/AGPL mod code into HECTON-8 unless the project deliberately accepts the license consequences.
- Treating Subnautica's Unity Addressables stack as an automatic fit for HECTON-8 DOD runtime data.

## Verdict

The foundation is not empty. It is also not payload-ready.

HECTON-8 already has serious scaffolding: ContentAuthority build validators, a DataMonolith compiler, a native static data arena, object-batch interfaces, visual tier contracts, scan/quest systems, modding APIs, and platform audit code.

The gap is proof. Multiple systems have strong source-level contracts but no populated production payloads, no generated artifacts, or only menu-time validation. The next batch should not rewrite the architecture. It should convert scaffolds into hard artifacts and build gates.

## Current Static Evidence Matrix

| Area | What is real | What is not proven | P0 action |
| --- | --- | --- | --- |
| DataMonolith | `Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithCompiler.cs` exists. It watches `Assets/_SourceData` and `Data/Balance`, and targets `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`. `GameBootstrapper` calls `H8StaticDataArena.TryInitializeFromStreamingAssets`. | `Assets/_SourceData` has 0 files. `Assets/StreamingAssets` has 0 files. The boot path tolerates `Missing`. Current `Data/Balance/*.csv` files have `Id` but no `hash32`, while the monolith compiler requires hash pairs for Balance CSV rows. | Make `static_data.h8bin` a required release artifact. Reconcile `H8DataBaker` and `H8DataMonolithCompiler` schemas. Add generated `hash32` or a single shared schema manifest. |
| Addressables / ContentAuthority | `ContentAuthorityBuildPreprocessor` calls `ContentAuthorityBuildValidators.RunAllBuildValidators()` at build callback order -9000. Validators expect `Core`, `High_Res`, and `Overkill` Unity object/visual payload groups, binary layout checks, hash maps, object batches, VFX manifests, lore budgets, prefab bindings, and compute threadgroup limits. | `Assets/AddressableAssetsData` has 0 files. No actual Addressables settings/groups were found. No `ContentAssetHashMap` asset payloads were found. Texture/item helpers can create targeted entries, but not a complete project bootstrap. This is separate from DataMonolith/world-static truth. | Add a deterministic ContentAuthority bootstrap for Unity object assets: settings, required groups, minimal Core entries, generated hash map, VFX prewarm manifest, and a build-report freshness check. Keep immutable world/static data on DataMonolith and sector payloads. |
| Object batches | `ObjectBatchBase` exists. `ObjectBatchInstance` uses `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 80)]`; `ObjectBatchChunk` uses Pack=1/Size=40. ContentAuthority validates object batch payloads when present. | No concrete `ObjectBatchBase` asset payloads were found in `Assets/_Project`. This is a contract, not populated world density. | Generate one minimal object-batch payload per test sector and integrate it into the world residency/payload map. Do not spawn static dressing as individual GameObjects. |
| Visibility/physics proxies | `VisibilityProxyBase` exists as a payload concept and appears in ContentAuthority scans. | No concrete visibility proxy payloads were found. No proof that a sector can load baked collision/visibility data without scene objects. | Add `VisibilityPhysicsProxyBase` as a named sector payload family and validate it in build, not only as an abstract asset type. |
| VFX prewarm | `ContentVfxPrewarmManifest` exists with capped entries and ContentAuthority validation logic. | No `ContentVfxPrewarmManifest` asset payloads were found. Validator does not prove runtime warmup coverage if no manifest exists. | Require one Core manifest and one Overkill manifest. Tie them to platform tier and budget gates. |
| First-hour scan/recipe route | Recipe assets exist. First-hour quest assets exist: exit lifepod, collect titanium, craft scanner. Scanner and scan log systems exist. | Recipe scan gates exist, but authored route proof is weak. Search found 41 recipes, with many empty scan gates and a small set of gates such as `scan.expedition_contact`, `scan.structure_relay`, `scan.resource_node`, and `scan.resource_cache`. Current scan validators are menu-only or warning-based. The scanner route is not a release build gate. | Promote missing required scan route from warning to build failure after route assets are represented. Add a deterministic first-hour route fixture. |
| Biome visual authority | `Assets/_Project/Data/Biomes/RuntimeVisualProfiles` contains 216 profile assets. Visual tier policy includes Overkill bits such as salt crystals, volumetric silt wake, and procedural hull dents. | Large profile count is not semantic proof. There is no confirmed authored mapping from biome profile to sector payload, scan route, audio bank, object batch, and platform tier. | Add a biome authority matrix: biome hash, profile, object batch, audio bank, discovery route, low/high/overkill feature budget. |
| Modding API | `HectonAPI` exposes events, commands, item/recipe registration, resources, localization, UI settings, save state, and world readiness. Direct Unity prefab/instance access is blocked in key paths. | `Mods` has 0 files. `ModLoader.CurrentAPIVersion = 2`; runtime rejects `RequiredAPIVersion <= 0`; `ModBuilderWindow.ModManifestData` emits no `RequiredAPIVersion` or `ModPriority`. SDK-built mods can be rejected unless hand-edited. Missing Nautilus-like data overlay handlers for PDA, scan/known-tech, loot, databank, and world-entity distribution. | Fix manifest v2 emission. Add data-only overlay handlers before accepting arbitrary managed-code mod scope. Keep hot runtime contracts typed and bounded. |
| Platform storage | Static arena and world pager contracts exist. ContentAuthority checks compute threadgroups against 1024. | `H8StaticDataArena` uses `File.ReadAllBytes` as a whole-file staging path. That is acceptable as a boot-only desktop prototype, but not proof for Android/Quest StreamingAssets/JAR, Steam Deck MicroSD pressure, or huge future blobs. | Add platform-aware monolith loading: chunked file read, Android StreamingAssets path handling, hash/freshness proof, and boot memory ceiling test. |
| High-end visuals | Tier contract already contains Overkill feature flags. HECTON-8 visual direction is distinct: NASA-punk / deep-sea noir, not bright coral imitation. | No populated Overkill content pack exists. No proof that Overkill payloads are isolated from low-tier builds. | Keep Overkill optional and content-addressed: visor salt, volumetric silt wake, hull dents, high-tier POM/raymarch/SSS/particles must not be required by Core. |

## Concrete Local Evidence

- `Assets/_SourceData`: 0 files.
- `Assets/StreamingAssets`: 0 files.
- `Assets/AddressableAssetsData`: 0 files.
- `ContentAssetHashMap` authored payloads found in `Assets/_Project`: 0.
- `ContentVfxPrewarmManifest` authored payloads found in `Assets/_Project`: 0.
- `ObjectBatchBase` authored payloads found in `Assets/_Project`: 0.
- `VisibilityProxyBase` authored payloads found in `Assets/_Project`: 0.
- `Assets/_Project/Data/Biomes/RuntimeVisualProfiles`: 216 `.asset` files.
- `Data/Balance/Items.csv`, `Fauna.csv`, `Economy.csv`, and `Physics.csv` expose `Id` columns but no `hash32` columns.
- `ModLoader.cs` exposes `CurrentAPIVersion = 2` and rejects missing/invalid required API versions.
- `ModBuilderWindow.cs` manifest struct emits only Id/Name/Version/Author/Dependencies/EntryAssembly/EntryType.

## Tactical Interpretation From Subnautica

Subnautica's useful lesson is not "use Unity Addressables because they did."

The useful lesson is separation of concerns:

- Baked base world data is not the same as player save deltas.
- Static world cache payloads deserve their own directory and manifest vocabulary.
- Object batches, terrain/cell caches, visibility/physics proxies, biome sidecars, and save-slot deltas must not be collapsed into one anonymous blob.
- Mod APIs become usable when they provide high-level handlers for common content operations, not raw Unity object access.

For HECTON-8, the correct direction is:

- `static_data.h8bin` for immutable static authority.
- Sector payload directories for authored base-world caches.
- Save deltas for player changes only.
- Typed lanes/commands for mod operations.
- ContentAuthority build gates for every generated artifact.

## Required P0 Work Orders

1. `DataMonolithReleaseGate`
   - Target files: `H8DataMonolithCompiler.cs`, `H8StaticDataArena.cs`, `GameBootstrapper.cs`, Balance CSV schema.
   - Done means: `static_data.h8bin` exists, is fresh, hash-validated, and missing blob fails release builds.

2. `ContentAuthorityBootstrap`
   - Target files: `ContentAuthorityBuildValidators.cs`, Addressables settings, generated hash maps.
   - Done means: `Core`, `High_Res`, `Overkill` groups exist and a minimal Core payload builds cleanly.

3. `FirstHourRouteBuildGate`
   - Target files: `ContentSanityValidator.cs`, `ScanIntelValidator.cs`, recipe/quest/scannable assets.
   - Done means: every required scan gate has an authored route or generated route asset; missing route fails build.

4. `ObjectBatchSectorPayload`
   - Target files: `ObjectBatchBase.cs`, world page payload contracts, sector authoring pipeline.
   - Done means: one streamed sector can load static dressing without per-object GameObject spawning.

5. `ModManifestV2AndOverlayHandlers`
   - Target files: `ModBuilderWindow.cs`, `ModLoader.cs`, `HectonAPI.cs`, mod overlay registries.
   - Done means: SDK emits runtime-loadable manifest v2 and exposes data-only handlers for PDA/databank, scan/known-tech, loot, audio, and world entity distribution.

6. `PlatformBlobLoader`
   - Target files: `H8StaticDataArena.cs`, platform audit/build pipeline.
   - Done means: no whole-file managed peak for large blobs on constrained targets; Android/Quest/Steam Deck storage paths have proof tests.

## What Not To Do

- Do not replace HECTON-8's DOD monolith with stock Addressables for world data.
- Do not copy Nautilus/Nitrox/TerrainPatcher GPL/AGPL code into runtime.
- Do not expose raw Unity objects to mods as the primary API.
- Do not treat menu validators as release gates.
- Do not claim Subnautica 2 visual parity from screenshots alone; content density and update cadence are the real pressure.

## Sources

- Steam Subnautica 2 page: https://store.steampowered.com/app/1962700/Subnautica_2/
- Nautilus repository: https://github.com/SubnauticaModding/Nautilus
- Nitrox repository: https://github.com/SubnauticaNitrox/Nitrox
- BepInEx.Subnautica repository: https://github.com/toebeann/BepInEx.Subnautica
- TerrainPatcher repository: https://github.com/Esper89/Subnautica-TerrainPatcher
- QModManager repository: https://github.com/SubnauticaModding/QModManager

## Proof Limits

This is a source/documentation audit. It did not run Unity import, Addressables build, Android/Quest IL2CPP build, Metal shader compile, Steam Deck storage test, or Memory Profiler capture.

Exact runtime microseconds saved: 0us. The value is preventing false readiness claims and generating implementation targets that can later be profiled.

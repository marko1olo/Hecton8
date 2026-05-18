# HECTON-8 Documentation Actuality Ledger

Date: 2026-05-19
Owner lane: SUBNAUTICA_RESEARCHER
Status: ACTIVE SOURCE-OF-TRUTH OVERLAY / R24 DOC_GLOBAL ROOT-ARCH BOUNDARY / STATIC_DOC + STATIC_SOURCE + WEB_REFERENCE

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

## Purpose

This ledger exists because the documentation set is larger than the current architecture truth.
It does not rewrite archive history. It marks which live documents own current facts, which
documents are legacy snapshots, and which claims require implementation or runtime proof.

Use this file before trusting older save, content-pipeline, Subnautica, Subnautica 2, modding,
or co-op reports.

Current DOC_GLOBAL boundary:

- `Docs/Reports/2026-05-19_DOCUMENTATION_R24_ROOT_ARCHITECTURE_ACTUALITY_LOCAL.md`
- `Docs/Reports/2026-05-18_DOCUMENTATION_R23_SUBAGENT_RESIDUE_AND_STATUS_JSON_LOCAL.md`
- `Docs/Reports/2026-05-18_DOCUMENTATION_R22_COUNTER_DRIFT_AND_VALIDATION_LOCAL.md`

Historical machine-readable R4 companion:

- `Docs/Reports/2026-05-17_ACTIVE_DOCUMENTATION_ACTUALITY_MANIFEST.json`

## Inventory Boundary

Historical 2026-05-17 PowerShell inventory of `Docs` with `*.md`, `*.txt`, and `*.json`; do not use these numbers as current R24 counts unless recaptured:

- Total scanned docs: 3032.
- Live docs under active/reference locations: 414.
- Historical or agent/process docs: 2618.

Historical/process locations excluded from live authority by default:

- `Docs/Archive`
- `Docs/_Archive`
- `Docs/AgentLogs`
- `Docs/Tasks`
- `Docs/ARCHIVARIUS REPORTS`
- `Docs/DEPRECATED`
- `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit`

Active broad reference locations remain:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`
- `Docs/HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md`
- `Docs/SYSTEMS_CONTRACTS.md`
- `Docs/QUALITY_GATES.md`
- `Docs/PROJECT_STATE_STATIC_XRAY.md`
- `Docs/ARCHITECTURE/*`
- `Docs/Design/*`
- `Docs/Modding/*`
- `Docs/Lore/*`
- `Docs/AI_Fauna/*`
- `Docs/Flora_Pipeline/*`
- `Docs/Scatter_Runtime/*`
- `Docs/SPACE_ENGINE_RESEARCH/*`

Reports remain evidence snapshots unless promoted into the stable authority spine.

## Current Save Truth

Evidence class: `STATIC_SOURCE`.

Source files:

- `Assets/_Project/Scripts/SaveBinaryStorage.cs`
- `Assets/_Project/Scripts/SaveSystem/SaveMasterHashV10.cs`
- `Docs/Design/Save_Binary_Header.md`
- `Docs/ARCHITECTURE/SUBNAUTICA2_EA_TO_HECTON8_PRODUCTION_CONTRACTS.md`

Current facts:

- `SaveBinaryStorage.CurrentVersion = 0x0009`.
- `SaveBinaryStorage.CurrentHeaderSize = 56`.
- `SaveBinaryStorage.TryValidateHeader(...)` accepts versions from the supported minimum through current, not future versions beyond `0x0009`.
- `SaveMasterHashV10.HeaderVersion = 0x000A`.
- `SaveMasterHashV10` uses a staged 72-byte v10 header/hash contract.

Legacy/drift documents:

- `Docs/ARCHITECTURE/SAVE_V8_BINARY_SPEC.md`
- `Docs/ARCHITECTURE/SAVE_PAGING_PROTOCOL.md`

These two files remain useful for indexed-sector design history, but their `0x0008` and `52-byte`
claims are no longer the current runtime truth. They are superseded for version authority by this
ledger plus `SUBNAUTICA2_EA_TO_HECTON8_PRODUCTION_CONTRACTS.md`.

Required next action:

- Create a generated `SAVE_LIVE_VERSION_LEDGER` or equivalent CI/doc gate that fails when
  `CurrentVersion`, header size, header flags, or staged hash header version drift without a
  stable-doc update.

## Current Static-Data And Content-Authority Truth

Evidence class: `STATIC_SOURCE` + `FILESYSTEM`.

Source files:

- `Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithCompiler.cs`
- `Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs`
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`
- `Assets/_Project/Scripts/Core/Content/ContentRuntimeServices.cs`
- `Assets/_Project/Scripts/Core/Content/ContentAssetHashMap.cs`

Current facts:

- `H8DataMonolithCompiler` reads `Assets/_SourceData` and `Data/Balance`.
- It targets `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`.
- `H8StaticDataArena` can initialize from `Application.streamingAssetsPath/Hecton8/DataMonolith/static_data.h8bin`.
- Current filesystem scan found `Assets/_SourceData` empty and `Assets/StreamingAssets` empty.
- `Assets/AddressableAssetsData` is empty.
- Authored `ContentAssetHashMap`, `ContentVfxPrewarmManifest`, `ObjectBatchBase`, and `VisibilityProxyBase` payload scans in the latest research pass found zero concrete production assets.
- `Data/Balance/*.csv` uses `Id` columns; the monolith compiler's Balance CSV path requires hash-pair compatibility such as `hash32`.
- `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` is the current generated-binary payload authority. Its 2026-05-18 recheck found 47 product/generated target binary files, while the broad hygiene verifier sees 65 `.bin` / `.h8bin` files because it also scans Bakery editor/plugin fixtures.
- `Docs/Reports/2026-05-18_DOCUMENTATION_BATCH008_BINARY_HYGIENE_R14_LOCAL.md` records the DOC_GLOBAL R14 documentation pass that demoted pre-Batch008 binary-hygiene PASS rows in active docs.
- Statically proven main-runtime payloads are only `Data/Audio/Acoustic_LUT.bin` and `Data/Visuals/Water_Extinction_Matrix.bin`.
- `Data/Balance/Baked/Babel_Dictionary.h8bin` is the only misaligned product payload in the current recheck: 1295 bytes, 16-byte remainder 15.
- `Data/Balance/Baked/H8StaticData.bin` and `Data/Balance/Baked/Babel_Dictionary.h8bin` are small balance-store artifacts. They are not the absent StreamingAssets DataMonolith `static_data.h8bin`.
- `Data/Lore/Encyclopedia.h8bin` is an `H8LR` raw UTF-8 lore blob and is not read by current `LoreMmfEncyclopedia`, which expects an `H8LE` index plus separate payload stream.

Current architecture split:

- `DataMonolith` owns immutable static DB / table truth.
- World/sector payload pages own baked terrain/object/visibility/audio/discovery cache families.
- Save slots own player/world deltas only.
- `ContentAssetHashMap` and Addressables-style groups are for Unity object/visual/audio asset bindings where the project chooses Unity asset delivery. They are not the world truth store.

Required next action:

- Make `static_data.h8bin` mandatory for production builds.
- Rebake `Data/Balance/Baked/Babel_Dictionary.h8bin` through its owning baker before any runtime wiring.
- Generate or author minimal `Core`, `High_Res`, and `Overkill` Unity object asset groups only where needed.
- Generate hash maps, VFX prewarm manifests, object batches, visibility/physics proxies, and freshness reports.
- Keep stock Unity Addressables out of the immutable world/static-data truth path.

## Current Modding Truth

Evidence class: `STATIC_SOURCE`.

Source files:

- `Assets/_Project/Scripts/ModdingAPI/ModLoader.cs`
- `Assets/_Project/Scripts/Editor/ModdingSDK/ModBuilderWindow.cs`
- `Docs/Modding/Mod_API_Specification.md`
- `Docs/Modding/Runtime_Verification_Playbook.md`

Current facts:

- `ModLoader.CurrentAPIVersion = 2`.
- `ModLoader` rejects `RequiredAPIVersion <= 0`.
- `ModLoader` rejects manifest API versions above current.
- `ModLoader` consumes `ModPriority`.
- `ModBuilderWindow.ModManifestData` currently emits `Id`, `Name`, `Version`, `Author`,
  `Dependencies`, `EntryAssembly`, and `EntryType`, but not `RequiredAPIVersion` or `ModPriority`.

Required next action:

- Builder must emit `RequiredAPIVersion = 2` and `ModPriority`.
- Add a runtime smoke fixture proving a builder-created mod loads without manual manifest editing.
- Favor data-only overlay handlers for PDA/databank, scan/known-tech, loot, audio, and world
  distribution before broad managed-code mod authority.

## Current Co-Op Truth

Evidence class: `STATIC_SOURCE` + `STATIC_DOC`.

Source files:

- `Assets/_Project/Scripts/Networking/HectonNetworkManager.cs`
- `Docs/ARCHITECTURE/COOP_MERKLE_STATE_DELTA_PROTOCOL.md`
- `Docs/ARCHITECTURE/SUBNAUTICA2_PLAYER_LOOP_TO_HECTON8_GAP_MATRIX.md`

Current facts:

- `HectonNetworkManager` is a MonoBehaviour placeholder with TODOs and development-build logs.
- The Merkle protocol document is a strong static design contract.
- No local transport loopback, packet harness, co-op save ownership test, or player-route runtime proof was run in this pass.

Required next action:

- Add a local loopback harness or explicitly remove the placeholder from runtime scope until real
  transport ownership exists.
- Keep state authority in typed unmanaged packets and DataVault snapshots, not Unity object graphs.

## Current Subnautica 2 Reference Truth

Evidence class: `WEB_REFERENCE` + `STATIC_DOC`.

Verified 2026-05-17:

- Unknown Worlds announced Subnautica 2 Early Access on 2026-05-14.
- Steam lists release and Early Access release date as 14 May 2026.
- Steam lists single-player, online co-op, cross-platform multiplayer, Steam Cloud, and up to
  three friends in co-op.
- Unknown Worlds' 2026-05-15 roadmap image lists: Biomods System, Blight Encounters, Wrecks
  Gameplay, Vehicle Docking & Fabrication, PDA Databank, Voicelogs Priority System, more passive
  Biomod slots, Storage Cache, Sprint, HUD signals, Base Builder Tool, Pinned Recipes System,
  Voice Chat Emotes, Player Trading, Player Revive, additional customizations, and future major
  expansions with new biomes, creatures, resources, tools, vehicle, and story.

Clean-room rule:

- Do not inspect, decompile, copy, extract, or structurally imitate proprietary Subnautica or
  Subnautica 2 assets, cache payloads, binaries, art, UI, names, story, or code.
- Borrow only public product-contract pressure, file taxonomy lessons, update-process lessons,
  and generic architecture patterns that HECTON-8 can implement independently.

Primary public sources:

- https://unknownworlds.com/en/news/subnautica-2-early-access-released
- https://unknownworlds.com/en/news/subnautica-2-early-access-roadmap
- https://store.steampowered.com/app/1962700/Subnautica_2/

## Active Subnautica Research Docs

Stable docs created or refreshed by the research lane:

- `Docs/Reports/SUBNAUTICA_2_UE5_REFERENCE_DOSSIER.md`
- `Docs/Reports/SUBNAUTICA_PUBLIC_MOD_ECOSYSTEM_DEEPDIVE.md`
- `Docs/ARCHITECTURE/HECTON8_P0_FOUNDATION_PROOF_MATRIX.md`
- `Docs/ARCHITECTURE/SUBNAUTICA2_TO_HECTON8_TACTICAL_BACKLOG.md`
- `Docs/ARCHITECTURE/SUBNAUTICA2_HECTON8_IMPLEMENTATION_HANDOFF.md`
- `Docs/ARCHITECTURE/SUBNAUTICA2_EA_TO_HECTON8_PRODUCTION_CONTRACTS.md`
- `Docs/ARCHITECTURE/SUBNAUTICA2_PLAYER_LOOP_TO_HECTON8_GAP_MATRIX.md`
- `Docs/Design/HECTON8_DREAM_VS_SUBNAUTICA2_COUNTERPOSITION.md`
- `Docs/Design/SUBNAUTICA2_SCREENSHOT_VISUAL_CHEATS.md`

Current priority reading order:

1. `SUBNAUTICA2_EA_TO_HECTON8_PRODUCTION_CONTRACTS.md`
2. `SUBNAUTICA2_PLAYER_LOOP_TO_HECTON8_GAP_MATRIX.md`
3. `HECTON8_P0_FOUNDATION_PROOF_MATRIX.md`
4. `SUBNAUTICA2_HECTON8_IMPLEMENTATION_HANDOFF.md`
5. `HECTON8_DREAM_VS_SUBNAUTICA2_COUNTERPOSITION.md`
6. `SUBNAUTICA2_SCREENSHOT_VISUAL_CHEATS.md`

## Proof Limits

- This ledger is not compile proof.
- This ledger is not Unity import proof.
- This ledger is not Play Mode proof.
- This ledger is not profiler, GCMonitor, player-build, scene-wiring, save/load, Android/Quest,
  Metal, Steam Deck, or visual-quality proof.
- Runtime microseconds saved by this pass: 0us. The value is preventing false authority and
  converting stale docs into explicit implementation gates.

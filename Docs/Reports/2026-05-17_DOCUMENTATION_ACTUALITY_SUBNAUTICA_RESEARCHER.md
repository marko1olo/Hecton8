<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R22 Static Actuality Boundary

This document is active only where it agrees with `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, current source files, and fresh verification artifacts.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, platform run, campaign telemetry, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older `PASS` / `VERIFIED` labels inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
# 2026-05-17 Documentation Actuality Sweep - SUBNAUTICA_RESEARCHER

Date: 2026-05-17
Owner lane: SUBNAUTICA_RESEARCHER
Status: DOC ACTUALITY PATCH / RUNTIME PENDING

## Scope

The user requested broad documentation actualization after the Subnautica/Subnautica 2 research
passes. This sweep does not rewrite historical archives. It updates active source-of-truth docs and
records stale clusters that future agents must not treat as current truth.

## Evidence Classes

- `STATIC_SOURCE`: source text inspected with `rg`/PowerShell.
- `STATIC_DOC`: active docs inspected.
- `FILESYSTEM`: local file/directory existence counted.
- `WEB_REFERENCE`: public pages opened on 2026-05-17.

No Unity import, Play Mode, profiler, GCMonitor, player build, platform build, save/load runtime, or
visual route proof was collected.

## Live Inventory

PowerShell scan over `Docs` for `*.md`, `*.txt`, and `*.json` found:

- Total: 3036 docs.
- Live/reference: 547 docs.
- Historical/process: 2489 docs.

Excluded from live authority by default: `Archive`, `_Archive`, `AgentLogs`, `Tasks`,
`ARCHIVARIUS REPORTS`, `DEPRECATED`, and the April forensic audit bundle.

## Updated Active Docs

- Created `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.
- Updated `Docs/README.md` to point at the 2026-05-17 actuality overlay.
- Updated `Docs/ARCHITECTURE/README.md` to put the ledger before legacy save docs.
- Generated `Docs/Reports/2026-05-17_ACTIVE_DOCUMENTATION_ACTUALITY_MANIFEST.json`.
- Marked `SAVE_V8_BINARY_SPEC.md` and `SAVE_PAGING_PROTOCOL.md` as legacy v8 snapshots for
  current version authority.
- Updated `CONTENT_SAVE_SLOT_TOPOLOGY.md` to split DataMonolith/static data, sector payloads,
  save deltas, and Unity object asset bindings.
- Corrected DataMonolith path and ContentAuthority wording in Subnautica research docs.
- Updated the Subnautica 2 reference dossier and dream/backlog docs so Addressables is not
  described as the world/static-data truth path.
- Updated the co-op Merkle doc's save-reference boundary to point at the live ledger.

## Contradiction Clusters

### Header / Manifest State

Current generated manifest:

- `Docs/Reports/2026-05-17_ACTIVE_DOCUMENTATION_ACTUALITY_MANIFEST.json`

Manifest snapshot after this pass:

- Live/reference docs scanned: 547.
- Live markdown: 429.
- Stable markdown excluding reports: 249.
- Stable markdown missing `Date:` or `Status:`: 0.
- Live markdown header debt is now mostly report history under `Docs/Reports`, not stable docs.
- Stable docs still calling the old `2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json` "Current": 0.
- Stable docs preserving that old file as a historical manifest snapshot: 35.
- Stable docs still using `## 2026-05-11 Current-State Override`: 0.
- Stable docs with explicit `2026-05-11 Historical Override + 2026-05-17 Actuality Pointer`: 35.
- Stable docs still calling the May 11 data boundary or visual-fake audit "Current": 0.

Old `Docs/Reports/*ACTIVE_DOCUMENTATION_MANIFEST.json` files remain historical generated snapshots.
Use the new actuality manifest for this lane's current doc-state evidence.

### Save Versions

Current source facts:

- `SaveBinaryStorage.CurrentVersion = 0x0009`.
- `SaveBinaryStorage.CurrentHeaderSize = 56`.
- `SaveMasterHashV10.HeaderVersion = 0x000A`.

Drift:

- `SAVE_V8_BINARY_SPEC.md` and `SAVE_PAGING_PROTOCOL.md` still described `0x0008` as the current
  save container.

Patch:

- Added supersession banners and moved active version truth to
  `HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.
- Reworded the remaining body text in `SAVE_V8_BINARY_SPEC.md` and `SAVE_PAGING_PROTOCOL.md`
  so `0x0008` is framed as a historical snapshot, not `SaveBinaryStorage.CurrentVersion`.

### DataMonolith vs Addressables

Current source facts:

- `H8DataMonolithCompiler` path is `Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithCompiler.cs`.
- Output path is `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`.
- 2026-05-19 supersession: `Assets/_SourceData` and `Assets/AddressableAssetsData` are empty; `Assets/StreamingAssets` contains `signal_tuning_profiles.csv` plus its Unity `.meta`, but `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` is absent.
- Current ContentAuthority payload scans found no concrete hash map, VFX prewarm, object batch, or
  visibility proxy production assets.

Drift:

- Several docs blurred immutable world/static-data authority with Unity Addressables object
  delivery.

Patch:

- Clarified that `DataMonolith` and sector payloads own immutable world/static data.
- Addressables-style groups remain valid only for Unity object/visual/audio asset binding where
  deliberately chosen.

### Modding

Current source facts:

- `ModLoader.CurrentAPIVersion = 2`.
- Loader rejects `RequiredAPIVersion <= 0`.
- Loader consumes `ModPriority`.
- `ModBuilderWindow.ModManifestData` does not emit `RequiredAPIVersion` or `ModPriority`.

Patch:

- Ledger records SDK/runtime mismatch as active P0.

### Co-Op

Current source facts:

- `HectonNetworkManager.cs` is still a placeholder.
- `COOP_MERKLE_STATE_DELTA_PROTOCOL.md` is a static design contract, not runtime proof.

Patch:

- Ledger and co-op doc now point at live save-version authority instead of relying on legacy v8
  save-doc naming.

## Subnautica 2 Current Public Facts

Verified public sources:

- Unknown Worlds Early Access release page dated 2026-05-14 states Early Access started that day
  and links Steam, Epic, and Xbox/Microsoft Store.
- Steam lists release and Early Access release date as 14 May 2026, plus single-player, online
  co-op, cross-platform multiplayer, Steam Cloud, and 2-3 year Early Access expectation.
- Unknown Worlds roadmap page dated 2026-05-15 states regular updates, QoL update first, co-op
  improvements second, future large expansions, and continuing bug/balance/optimization work.
- The official roadmap image lists the specific tactical pressure points: Biomods System, Blight
  Encounters, Wrecks Gameplay, Vehicle Docking & Fabrication, PDA Databank, Voicelogs Priority
  System, Storage Cache, Pinned Recipes System, HUD Signals, Base Builder Tool, Voice Chat Emotes,
  Player Trading, Player Revive, and future world/biome/creature/resource/tool/vehicle/story
  expansion.

Sources:

- https://unknownworlds.com/en/news/subnautica-2-early-access-released
- https://unknownworlds.com/en/news/subnautica-2-early-access-roadmap
- https://store.steampowered.com/app/1962700/Subnautica_2/

## Proof Limits

- No proprietary Subnautica/Subnautica 2 files, assets, binaries, Unreal internals, save payloads,
  shader code, UI, names, story, or decompiled code were inspected or copied.
- No GPL/AGPL mod implementation code was copied.
- Runtime microseconds saved: 0us.
- Actual performance value requires later implementation and profiler evidence.

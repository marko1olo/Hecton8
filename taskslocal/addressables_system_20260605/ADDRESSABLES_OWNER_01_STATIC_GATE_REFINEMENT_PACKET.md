# ADDRESSABLES_OWNER_01 Static Gate Refinement Packet

Status: STATIC_GATE_PACKET / PENDING UNITY OWNER PROOF.
Evidence class: STATIC_DOC / STATIC_SOURCE only.
Owner prompt: ADDRESSABLES_OWNER_01_STATIC_GATE_REFINER.
Write scope: this packet and `taskslocal/addressables_system_20260605/README.md`.

No Unity launch, import, Play Mode, build, Addressables settings mutation, project settings mutation, `Assets/` mutation, catalog build, label creation, entry creation, prefab edit, material edit, scene edit, or raw YAML mutation was performed.

## Mandates Followed

- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `.agents-skills/STRM_World_Streaming_Residency_Chunk_Management.txt`
- `.agents-skills/STRM_Async_Standard.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/DATA_Runtime_Struct_Layout_ARM64.txt`
- `streaming.md`
- `performance.md`
- `data.md`

## Static Facts

- `Assets/AddressableAssetsData` is reported as existing with recursive file count `0` and non-meta file count `0`.
- No static Addressables settings, group, profile, schema, catalog, label, key, entry, loaded handle count, release ledger, memory residency, or pressure behavior proof exists.
- `Docs/AssetAudit/ADDRESSABLES_ASSET_GROUP_PLAN_20260605.csv` contains `11` proposed rows: `6` P0 and `5` P1.
- `Docs/AssetAudit/ASSET_GUID_ACTIVE_ROUTE_TRIAGE_20260605.csv` contains `655` P0 active-route review rows and `145` P1 scene-route review rows.
- P0 active-route rows are dominated by scriptable/native assets, prefabs, materials, and audio. This blocks broad automatic grouping until scoped Unity readback proves actual route ownership.

## Clean Unity Gate Dependency

No future owner may create or mutate Addressables settings, groups, catalogs, labels, entries, schemas, profiles, or keys until a clean Unity gate is recorded.

Clean Unity gate means all of the following are true:

- Unity is closed or confirmed idle by the assigned Unity owner.
- No compile/import/package/shader/build work is active.
- No dirty scene or dirty prefab risk is present.
- The latest `ASSET_OWNER_06` or equivalent readback packet has no stop artifact blocking Addressables work.
- Console/import/readback proof exists for the active scene, Player prefab audio refs, sky/Aegir, Crest/ocean/contact, terrain, flora/proxy materials, UI sprites, and Addressables window state.
- CPU/build process policy is satisfied before any build-like validation is attempted by a later owner.

If any item fails, stop at the checkpoint. Do not create settings, groups, catalogs, labels, entries, or keys.

## First-20 Route And P0 Row Binding

| P0 row | First-20 route moment protected | Current blocker |
|---|---|---|
| `group_audio_music` / `audio.music.musicdirector` | boot, first exit, route tension, silence windows | no group proof; null music/stinger mixer refs; long beds need cadence proof |
| `group_audio_amb_long` / `audio.ambient.long` | bright surface exit, shallow swim orientation, route sound bed | import policy conflict; direct `Player.prefab` ambience refs; no group proof |
| `group_core_player_audio_loops` / `audio.player.loop` | breath, swimming, survival feedback, first-control continuity | direct `Player.prefab` refs; loop policy unresolved; no entries |
| `group_core_ui_sfx_short` / `audio.ui_sfx.short` | HUD feedback, oxygen/warning interaction, early tool use | missing owner exceptions; direct player refs for some clips; long UI-class file contamination |
| `group_surface_water_contact` / `visual.surface.water_contact` | ocean surface, shoreline contact, waterline credibility | rejected `foam.png` is serialized-reachable; Crest clone/wrapper forbidden; no group proof |
| `group_photic_flora_materials` / `visual.photic.flora_materials` | photic shallows, route silhouettes, premium near-field dressing | active-world `WorldProceduralProxy` material contamination; streaming mip risk; no group proof |

P1 rows stay behind P0: `group_surface_sky_celestial`, `group_photic_terrain_pbr`, `group_photic_flora_prefabs`, `group_geology_prefabs`, and `group_core_ui_sprites`.

## Required Proof Artifacts

- Addressables settings readback: settings asset path, profile path, schemas, group window state, and source-control diff scope.
- Group/key plan: one row, one owner, one proposed group, one label, one stable string key grammar, one hashed runtime key route, one release route.
- Handle load/release ledger: acquire, duplicate acquire, release, duplicate-release guard, zero-ref pending release, stale-handle scan, leak scan.
- Memory and residency proof: loaded handles, resident memory, committed memory, graphics memory, texture mip residency, audio memory, driver reserve, compact pressure response.
- Scene transition proof: load screen/activation gate, first-control asset readiness, route return safety, and no gameplay start before critical assets are owned.
- Unload release queue proof: pending release queue drain, release ordering, zero orphan handles, pressure recalc, and per-frame drain budget.
- `Resources.UnloadUnusedAssets` exclusion proof: source scan plus runtime owner statement that pressure relief uses release queues, mip downgrade, LOD residency reduction, and approved GC safe windows only.
- No broad bundle proof: heavy rows use `RequestedAssetAndDependencies`; any `AllPackedAssetsAndDependencies` exception is tiny, always-hot, capped, and Memory Profiler-proved.

## Execution Tasks

1. Reconfirm the static source boundary: current packet may read docs and CSVs only; it must not mutate `Assets`, Addressables data, project settings, scenes, prefabs, materials, or catalogs.
2. Record the clean Unity gate as a hard precondition for any future settings, group, catalog, label, entry, schema, or profile work.
3. Read the latest scoped Unity readback artifacts for active-route sky/Aegir, Crest/ocean/contact, terrain, flora/proxy materials, UI sprites, audio config, Player prefab refs, and Addressables window state.
4. Check for any `ASSET_OWNER_06_STOP_*` or equivalent stop artifact. If present, stop this route and write a blocker note in the future owner's packet.
5. Read back Addressables settings state before mutation. Required fields: settings asset path, profiles, groups, schemas, labels, entries, build/load paths, and group load modes.
6. Freeze the initial P0 row queue from `ADDRESSABLES_ASSET_GROUP_PLAN_20260605.csv`. Do not let P1 rows advance before P0 blockers have owner proof.

Checkpoint 1 after task 6: Gate/readback checkpoint. If Unity gate is dirty, Addressables settings are absent/unreadable, or readback artifacts are missing, block execution. No settings, groups, catalogs, labels, entries, or keys may be created.

7. For each P0 row, assign one future owner, one route, one proof artifact, one release policy, and one failure disposition. Do not merge audio, water, or flora rows into a broad convenience group.
8. Build the group/key plan as data only. Required columns: source row, owner, proposed group, label, key grammar, runtime hash route, priority tier, load mode, release owner, proof artifact, rejection condition.
9. Enforce `RequestedAssetAndDependencies` for heavy music, ambience, player loops, water/contact, flora materials, sky, terrain, prefab, and HLOD groups. Reject packed residency for these rows without measured Memory Profiler proof.
10. Create an exception table for tiny always-hot core assets only. Each exception needs byte cap, import settings, owner lifetime, release or retain policy, and Memory Profiler artifact.
11. Split direct audio refs from Addressables-owned audio. `Player.prefab` direct `AudioClip` refs remain blockers until a Unity owner proves a documented lifetime exception or reroutes them through owned handles.
12. Split rejected visible source from support/source-only use. `foam.png` and `WorldProceduralProxy` materials cannot become acceptance evidence by being placed into a group.

Checkpoint 2 after task 12: Row-planning checkpoint. If any P0 row lacks owner, key grammar, load mode, release route, visual/audio proof path, or rejection condition, it remains blocked. Do not start Unity Addressables creation.

13. After the clean Unity gate and row-planning checkpoint pass, create or update Addressables settings only through scoped Unity API work by the assigned future owner. Raw YAML edits are rejected.
14. Immediately read back settings after any future settings work: settings asset, profile, build/load path, schema list, group list, group load modes, labels, entries, and dirty asset paths.
15. Create labels and entries row-by-row only after the row owner approves the exact source list. No glob expansion may silently include proxy, rejected, demo, placeholder, or source-only assets.
16. Build catalogs only after settings, groups, labels, entries, load modes, and source lists have readback proof. Catalog presence alone is not readiness.
17. Run the handle lifecycle proof for each P0 row: acquire, duplicate acquire, release, duplicate-release guard, zero-ref pending release, pending release queue drain, stale-handle scan, and leak scan.
18. Prove no `Resources.UnloadUnusedAssets` route is introduced. Pressure relief must use mip downgrade, LOD residency reduction, release queues, bank limits, and approved safe-window GC only where allowed by project law.

Checkpoint 3 after task 18: Lifecycle checkpoint. If lifecycle proof is absent, contradictory, or uses `Resources.UnloadUnusedAssets`, revert scoped Addressables work and keep status PENDING_VERIFICATION.

19. Run memory/residency proof for compact lane before readiness language: loaded handles, RAM, VRAM, texture budget, mip residency, audio memory, resident/committed split, graphics memory, and driver reserve.
20. Run scene transition proof: bootstrap or main-menu load, world load gate, first-control readiness, route return safety, unload release queue, and no scene activation before player/HUD/audio/proxy essentials are owned.
21. Run compact pressure proof: mip downgrade before broad unload, far/decorative rows shed first, player survival assets pinned, waterline/sky/route silhouettes retained, and no flat visual fallback.
22. Run route visual/audio proof for P0 rows: surface water/contact screenshot, photic flora screenshot, Player prefab audio readback, music/ambience cue proof, UI/SFX playback proof, Frame Debugger/Stats where visual.
23. Produce a regression model packet: CPU, GC, memory, VRAM, upload, cadence, correctness, failure modes, and rollback path. Label exact evidence class for every claim.
24. Only after all checkpoints pass, draft a future readiness candidate. The wording must remain bounded to the artifacts. If any artifact is missing, final status stays PENDING_VERIFICATION.

Checkpoint 4 after task 24: Acceptance checkpoint. Readiness is rejected unless settings readback, group/key plan, handle ledger, memory/residency proof, scene transition proof, unload release queue proof, no `Resources.UnloadUnusedAssets` proof, and no broad bundle proof all exist.

## Rejection Gates

- Reject any claim that package presence, editor helper code, serialized scene refs, material refs, audio profile refs, or static CSV rows prove Addressables readiness.
- Reject any group work before the clean Unity gate.
- Reject broad biome/audio/visual bundles that hide per-row ownership, load mode, release route, or memory proof.
- Reject direct prefab audio refs as lifecycle proof unless an owner exception is documented and measured.
- Reject `foam.png` or `WorldProceduralProxy` as visible-route acceptance.
- Reject runtime material clone/wrapper/override work for Crest. Assign asset materials only.
- Reject any report that omits loaded handle count, ref-count transition, release ledger, memory/residency, pressure response, and route proof.
- Reject static text evidence if it is presented as Unity/runtime/player proof.

## GlobalQualityWeight Residency Breadth

`GlobalQualityWeight` scales residency breadth. It does not change asset owner, stable key, DTO layout, save identity, release order, gameplay truth, collision truth, or warning facts.

Continuous knobs:

- `prefetch_radius = lerp(compactRadius, ultraRadius, saturate(GlobalQualityWeight))`, then reduced by pressure and route hysteresis.
- `speculative_slots = floor(lerp(0, maxSpeculativeSlots, GlobalQualityWeight * GlobalQualityWeight))`.
- `mip_bias = lerp(compactMipBias, ultraMipBias, saturate(GlobalQualityWeight))`, clamped by VRAM pressure.
- `audio_bank_breadth = lerp(minRequiredBanks, maxProfileBanks, saturate(GlobalQualityWeight))`, with player-critical cues pinned.
- `decorative_residency = baseResidency * smoothstep(0.0, 1.0, GlobalQualityWeight)`, with return-route silhouettes pinned.
- `diagnostic_depth = lerp(minLedgerDepth, maxLedgerDepth, saturate(GlobalQualityWeight))`, never replacing required proof.

## Low / Middle / High / Ultra Consequences

Low:

- Keep player HUD, breath/swim loops, collision proxies, warning cues, sky/Aegir readability, waterline contact, route silhouettes, and minimal photic flora identity resident before decorative content.
- Restrict speculative prefetch, far mip residency, decorative flora breadth, secondary ambience, and far LOD residency.
- Compact still needs premium readability. No flat water, muddy sky, proxy-looking flora, or silent survival feedback.

Middle:

- Permit route-owned PBR, contact masks, UI/SFX ownership, music/ambience ownership, and candidate prefab pools only after readback and lifecycle proof.
- Keep active banks and groups bounded. Do not widen residency to hide unresolved direct refs.

High:

- Spend spare budget on longer mip/LOD residency, richer Aegir/cloud detail, stronger water/contact breakup, denser near-field geology/flora, smoother audio transitions, and wider cue variation after proof.
- High-tier breadth still obeys release queues, memory pressure, and owner keys.

Ultra:

- Extend visual/audio density through richer cloud/Aegir layers, wet material layering, dense route dressing, wider music/ambient prefetch, and stronger audio spatial richness only while proof stays within budget.
- Ultra does not bypass ref-count, release, key ownership, compact fallback, or pressure behavior.

## Regression Model

CPU: Static packet only. Future risk is load dispatch spikes, catalog lookup churn, async instantiate cost, release queue stalls, audio route contention, and group overpacking. Proof requires profiler markers and scene transition captures.

GC: Static packet only. Future risk is string key creation, coroutine load loops, direct clip events, managed UI/audio churn, and cached raw `handle.Result` routes. Proof requires GCMonitor or Profiler across asset acquire/release, HUD, player loops, warnings, ambience swaps, and cue changes.

Memory/VRAM: Static packet only. Future risk is broad resident bundles, unreleased handles, overpacked audio banks, mip pressure, UI atlas bloat, and driver reserve undercount. Proof requires Memory Profiler, VRAM/texture ledger, resident/committed split, audio memory, mip residency, and pressure response.

Cadence: Static packet only. Future risk is same-frame load/release loops, immediate LOD/bank flipping, unbounded prefetch, missing hysteresis, and audio start jitter. Proof requires owner phase, preload window, release window, pressure audit cadence, hysteresis, and active bank limits.

Correctness: This packet blocks false promotion from static plans. Correctness remains blocked until one fact, one owner, one route, and one proof artifact exist per row.

Final status: PENDING UNITY OWNER PROOF. No readiness claim.

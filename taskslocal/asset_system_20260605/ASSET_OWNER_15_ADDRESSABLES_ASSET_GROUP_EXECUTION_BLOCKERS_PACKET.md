# ASSET_OWNER_15 Addressables Asset Group Execution Blockers Packet

Status: PENDING_VERIFICATION
Evidence class: STATIC_DOC / STATIC_SOURCE only.
Write scope: this file only.
First-20 route moment: blocks false promotion of surface sky/Aegir/moons, ocean contact, photic terrain/flora, player audio loops, HUD oxygen sprites, and route prefab candidates before Addressables owner proof exists.

No Unity run, import, build, Play Mode, Addressables window work, catalog build, prefab edit, material edit, scene edit, project setting edit, raw YAML mutation, or `Assets/` mutation was performed.

## Mandates Used

- `AGENTS.md`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `streaming.md`
- `performance.md`
- `Docs/AssetAudit/README.md`
- `Docs/AssetAudit/ADDRESSABLES_ASSET_GROUP_PLAN_20260605.md`
- `Docs/AssetAudit/ADDRESSABLES_ASSET_GROUP_PLAN_20260605.csv`
- `Docs/Reports/Batch32/CONTROLLER_MANDATORY_VISUAL_REFERENCE_READ_20260605.md`
- `Docs/Reports/AssetSystem_20260605/ADDRESSABLES_STATIC_COVERAGE_GAP_3218_20260605.md`
- `Docs/Reports/AssetSystem_20260605/ADDRESSABLES_GROUP_PLAN_3220_20260605.md`
- `Docs/Reports/AssetSystem_20260605/ASSET_PLANNING_CONSOLIDATION_3222_20260605.md`

## Exact Blockers

## 2026-06-06 Current Addressables Static Blocker Refresh

Fresh static audio-route evidence narrows the immediate scene-route blocker but does not promote anything to runtime proof:

- `ValidateAudioSceneStaticRoute.py --no-fail` currently reports `AUDIO_SCENE_STATIC_ROUTE_REJECTED blockers=1`; the only hard scene-route blocker is `addressables-absent`.
- `02_HECTON_WORLD` statically contains exactly one active `[MUSIC_SYSTEM] / HectonMusicDirectorAnchor` bound to `MusicDirectorConfig_Global.asset` GUID `3fe2e07be4fdac24cb6b2f12b438dcc3`. This is static YAML evidence only, not Play Mode, mixer, memory, GC, or audible proof.
- `ValidateAudioAddressablesP0Synthesis.py` currently reports `AUDIO_ADDRESSABLES_P0_SYNTHESIS_OK blockers=1 direct_refs=24 p0=0 footsteps=20 ui=4 fallback_required=1`.
- `Assets/AddressableAssetsData` remains effectively empty for owner-promotion purposes: no settings, groups, profiles, schemas, labels, entries, or catalog ownership evidence exists.
- Future Addressables repair must create or restore settings, groups, profiles, schemas, labels, and entries only through scoped Unity owner/API work after the process gate is green. Raw YAML edits remain forbidden.

| Blocker | Static fact | Required future evidence |
|---|---|---|
| No Addressables source data | `Assets/AddressableAssetsData` exists with recursive file count `0` and non-meta file count `0` in prior static evidence. No settings, group, profile, schema, catalog, key, label, or entry evidence exists. | Addressables settings/groups/schemas/profiles/labels/entries on disk, group window readback, settings readback, and catalog evidence after scoped owner work. |
| Static plans only | AssetAudit plan and 3220 report provide dispatch aliases, future naming grammar, load-mode law, and proof lists. They do not create data or prove lifecycle. | One future owner per row, one route, one proof artifact, and no duplicated group taxonomy. |
| Direct player audio refs | `Player.prefab` direct `AudioClip` refs remain open for player loops and some SFX/UI classes. | Prefab component-path readback, clip/import table, owner lifetime exception or reroute, handle/ref-count route where Addressables-owned, playback allocation proof, and mixer/DSP route proof. |
| MusicDirector mixer nulls | Static evidence says `MusicDirectorConfig_Global.asset` has null music and stinger mixer group refs. | Config readback, mixer or native/DSP route, cue table, key ownership, active/preload bank memory evidence, cadence evidence, and release ledger. |
| Rejected foam/contact source | `foam.png` is rejected as visible waterline art but serialized-reachable through active world/ocean routes. | Crest/ocean material slot readback, replacement/contact mask proof, bright shoreline screenshot, Frame Debugger/Stats artifact, mip/VRAM evidence, and handle lifecycle proof. |
| Proxy flora contamination | Four `WorldProceduralProxy` coral/kelp materials are referenced in `02_HECTON_WORLD.unity` static evidence. | Object/material/visibility table, replacement material route, LOD/material binding proof, photic route screenshots, mip/VRAM evidence, and handle lifecycle proof. |
| Source-only texture/PBR candidates | Generated or cleaned wet basalt, sand, foam, cloud, Aegir, and flora stacks are source/candidate material only. | Texture import role proof, material receiver readback, visual capture, mip residency evidence, and owner route before any group assignment. |
| Missing UI atlas proof | Static scope found no SpriteAtlas proof under `Assets/_Project`; oxygen/HUD sprite binding is not proved. | HUD prefab/readback artifact, atlas/import table, compact HUD screenshot, owner key route, and hot UI allocation proof. |
| Heavy group load mode unproved | Heavy sky, ocean, terrain, flora, audio, and prefab groups have no `AssetLoadMode` evidence. | `RequestedAssetAndDependencies` evidence unless a tiny always-hot exception has a written cap and Memory Profiler artifact. |
| No load/release evidence | No loaded handle count, owner list, ref-count sample, release ledger, leak audit, or pressure behavior exists for current candidates. | Runtime owner ledger with acquire/release records, ref-count transitions, pending-release drain evidence, and stale-handle/leak report fields. |
| No memory/residency evidence | Current docs do not prove RAM, VRAM, texture mip residency, resident/committed split, audio memory, or upload behavior. | Memory Profiler, VRAM/texture ledger, mip target table, audio memory table, upload budget/range evidence, and pressure response artifacts. |

Mandatory visual-reference consequence: Addressables grouping, mip residency, load mode, unload pressure, or compact-lane decisions cannot be accepted if future captures fail `CONTROLLER_MANDATORY_VISUAL_REFERENCE_READ_20260605.md` for surface sky/Aegir, ocean/shoreline, photic terrain/flora, UI oxygen, product-face, or medium-depth route contexts.

## No-Mutation Boundaries

- Current packet: no `Assets/` edits, no Addressables settings creation, no labels, no keys, no groups, no catalogs, no SpriteAtlas assets, no import settings, no material/prefab/scene mutation, no Unity/build/import/Play Mode.
- Future readback owner: may inspect only under the process gate in `Docs/AssetAudit/README.md`; if Unity auto-creates or dirties assets, stop, record path, do not save.
- Future implementation owner: do not raw-edit `.mat`, `.prefab`, `.unity`, `.asset`, Addressables settings, or catalogs. Use Unity APIs after gate clears and after row owner proof exists.
- Crest boundary: assign asset materials only. No runtime Crest material clone, wrapper, or override.
- Audio boundary: direct prefab refs are blockers until owner exception or reroute proof exists. Do not treat serialized refs as release ownership.
- Static documentation cannot elevate any row from blocker mapping to runtime state.

## Safe Execution Route After Gate Clears

1. Confirm process gate: CPU under the documented threshold, no active compiler/build/package/shader work, Unity idle or closed, and no dirty-scene risk.
2. Read ASSET_OWNER_06 readback artifacts or a stop artifact. If a stop artifact exists, do not create Addressables data.
3. Use `Docs/AssetAudit/ADDRESSABLES_ASSET_GROUP_PLAN_20260605.csv` as the queue. Use 3220 naming/lifecycle law for future real group/key/label grammar.
4. Resolve each row to one owner, one group route, one stable string key grammar, one hashed runtime key route, one release policy, and one proof artifact.
5. Keep heavy groups on `RequestedAssetAndDependencies`. Only tiny core always-hot groups may request packed residency after a written memory cap and Memory Profiler artifact.
6. Create settings/groups/labels/entries only through scoped Unity owner work. Record group load mode, source paths, owner, labels, key convention, and release route.
7. Run readback immediately after creation: settings asset, group asset, schema, profile, labels, entries, and group window screenshot or report.
8. Run lifecycle proof: acquire, duplicate acquire, release, duplicate-release guard, zero-ref pending release, release queue drain, and stale handle/leak scan.
9. Run memory proof: loaded handle count, RAM, VRAM, texture mip residency, audio memory, resident/committed split, upload budget/range behavior, and compact pressure response.
10. Only then draft any higher-level route claim, still bounded by actual artifacts and evidence class.

## Owner Proof Artifacts

Required before any group/key/catalog work:

- `Docs/Screenshots/HectonProofPackets/ASSET_OWNER_06_*` screenshots for sky/Aegir, Crest/ocean/foam, terrain/geology, flora/proxy, UI oxygen, audio config, Player prefab refs, and Addressables groups window.
- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_06_UNITY_READBACK_*` reports for the same domains.
- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_06_FRAME_DEBUGGER_*` reports where visual routing matters.
- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_06_VISUAL_REFERENCE_COMPARISON_*` reports for visual routes covered by the group plan.
- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_06_CONSOLE_*.txt` covering scene load, prefab readback, asset readback, and Addressables inspection.
- If present, any `ASSET_OWNER_06_STOP_*.md` blocks group creation until corrected by a later scoped owner artifact.

Required from future Addressables owner:

- Settings/group/schema/profile/label/entry inventory.
- Owner/key/group matrix by CSV row.
- Load mode table and exception table.
- Acquire/release/ref-count ledger.
- Pending release drain sample.
- Leak/stale-handle scan output.
- Memory/VRAM/mip/audio/upload budget evidence.
- Rollback note for every rejected row or removed group.

## Load, Release, And Ref-Count Proof Gates

- Every loaded handle has an owner, stable key, priority, ref count, last access tick, estimated RAM/VRAM size, and release phase.
- Duplicate acquire increments ref count without issuing a second uncontrolled load.
- Release decrements ref count; zero ref count enters the pending-release path; negative ref count is clamped and recorded as a defect.
- Release order remains: unregister phase users, unsubscribe signals, disable renderer/audio/particles, return/destroy approved runtime object, release Addressables handle, remove registry record, update pressure ledger.
- Callers do not persist raw `handle.Result` without ownership.
- No load starts in physics callbacks or gameplay hot loops.
- No scene activation or first-control moment occurs before required player/HUD/audio/collision-proxy assets have their owner route.

## Memory, VRAM, And Upload Proof Gates

- Compact lane honors the 1800 MB VRAM ceiling and 900 MB texture budget before visual route claims.
- Reports distinguish loaded handles, resident memory, committed memory, graphics memory, texture mip residency, audio memory, and driver reserve.
- Heavy group overpacking is rejected without resident/committed memory evidence.
- Mip downgrade and LOD residency reduction occur before broad unload of route-readable assets.
- Upload proof names per-frame byte budget, dirty page/range behavior, and avoids full-buffer updates unless all-dirty evidence exists.
- No synchronous GPU readback is used as runtime pressure logic.
- Pressure response preserves survival UI, breath/player loops, collision proxies, route silhouettes, sky/Aegir readability, and waterline readability before decorative content.

## Rollback And Rejection Gates

- If Unity auto-creates Addressables data during inspection, stop and record the created object path; do not save.
- If group creation uses a heavy packed mode without memory proof, revert scoped Addressables changes.
- If an asset row lacks owner, key, load mode, release route, or proof artifact, reject the row from group creation.
- If direct prefab audio refs remain open, reject audio group promotion for those refs.
- If `foam.png` or `WorldProceduralProxy` material remains visible-route support, reject water/contact or flora group promotion for that path.
- If SpriteAtlas/import/HUD binding is absent, reject core UI sprite group promotion.
- If load/release/ref-count evidence is missing or contradictory, revert scoped lifecycle changes.
- If memory/VRAM/upload pressure exceeds compact budget or harms route readability, revert scoped group additions or split the group.
- If load mode, mip residency, pressure response, or group split harms mandatory digest readability for user-visible water, terrain, sky/Aegir, flora, UI/cockpit, product-face, or medium-depth route views, revert or split the route.
- If a future report uses static text hits as runtime evidence, downgrade that claim to PENDING_VERIFICATION.

## Continuous GlobalQualityWeight Consequences

`GlobalQualityWeight` is one scalar curve from `0.0` to `1.0`. Low, Middle, High, and Ultra are review checkpoints on that curve, not binary branches.

Continuous knobs:

- `prefetch_radius = lerp(compact_radius, ultra_radius, saturate(GlobalQualityWeight))`, then multiplied by pressure and route hysteresis.
- `speculative_slots = floor(lerp(0, max_slots, GlobalQualityWeight * GlobalQualityWeight))`.
- `mip_bias = lerp(compact_bias, ultra_bias, saturate(GlobalQualityWeight))`, clamped by VRAM pressure.
- `ambient_bank_breadth = lerp(min_required_banks, max_profile_banks, saturate(GlobalQualityWeight))`, with player-critical cues pinned.
- `decorative_density = base_density * smoothstep(0.0, 1.0, GlobalQualityWeight)`, with return-route silhouettes pinned.
- `diagnostic_depth = lerp(min_ledger_depth, max_ledger_depth, saturate(GlobalQualityWeight))`, never replacing required owner proof.

| Checkpoint | Consequence |
|---|---|
| Low | Preserve bright sky/Aegir silhouette, waterline contact readability, photic terrain identity, route flora silhouettes, oxygen HUD clarity, breath/player loops, warning cues, collision proxies, and return route. Smoothly reduce speculative preload, far mip residency, decorative flora/prefab density, secondary ambience, and far LOD residency. |
| Middle | Permit route-owned PBR stacks, clean contact masks, UI atlas ownership, MusicDirector/ambient ownership, and candidate prefab pools only after readback and owner proof. Keep active banks and groups bounded. |
| High | Spend spare budget on longer mip/LOD residency, richer Aegir/cloud detail, stronger shoreline/contact breakup, denser near-field geology/flora, smoother audio transitions, and wider cue variation after frame/memory/mix evidence. |
| Ultra | Extend visual/audio density through richer cloud/Aegir layers, wet material layering, dense route dressing, wider music/ambient prefetch, and stronger reverb/occlusion only while proof artifacts stay within budget and release discipline remains intact. |

The curve must not change owner route, gameplay truth, cue IDs, Addressables keys, DTO layout, save identity, warning facts, collision truth, or release order.

## Regression Model

| Axis | Current packet impact | Future risk | Required evidence |
|---|---|---|---|
| CPU | Static documentation only; no runtime or Unity setting changed. | Load dispatch spikes, async instantiate cost, catalog lookup churn, release queue drain stalls, audio routing contention. | Profiler markers for dispatch, release, pressure eval, and audio/music route. |
| GC | No runtime code changed; no hot allocation claim is made. | String key creation in hot paths, coroutine load loops, direct clip event paths, UI/audio managed churn, persistent raw handle-result caching. | GCMonitor or Profiler artifact across asset acquire/release, HUD updates, player loops, warnings, ambience swaps, and cue changes. |
| Memory/VRAM | No import, residency, group, or catalog data changed. | Heavy group overpacking, broad resident music/ambient banks, texture mip pressure, unreleased handles, UI atlas bloat, driver reserve undercount. | Memory Profiler, VRAM/texture ledger, loaded handle count, resident/committed split, audio memory, mip residency, release ledger, pressure response. |
| Upload | No GPU upload route changed. | Full-buffer uploads, unchanged data uploads, missing dirty ranges, synchronous readback, upload spikes during route reveal. | Upload byte budget, dirty-page/range artifact, async readback boundary where needed, and visual route screenshot under pressure. |
| Cadence | No load/release cadence changed. | Same-frame load/release loops, immediate LOD/bank flipping, unbounded prefetch, missing hysteresis, audio start jitter. | Owner phase, preload window, release window, pressure audit cadence, hysteresis, active bank limit, and lifecycle telemetry. |
| Correctness | Blocks false promotion from static plans. | Duplicate group taxonomy, open audio policy conflict, source-only textures promoted by mistake, direct refs treated as lifecycle ownership. | One fact, one owner, one route, one proof artifact per row. |

Final status: PENDING_VERIFICATION

# Asset Planning Consolidation - Asset Worker 3222 - 2026-06-05

Status: `PENDING VERIFICATION`.
Evidence boundary: `STATIC_DOC` / `STATIC_SOURCE` only.
Write scope: this report only.
First-20 route moment: bright first surface exit, Aegir/sky readability, shoreline/ocean contact, photic shallows, player audio continuity, HUD oxygen readability, and candidate route dressing.

This report reconciles duplicated planning artifacts. It is not Unity proof, runtime proof, import proof, Addressables proof, Memory Profiler proof, Frame Debugger proof, audio mix proof, SpriteAtlas proof, GC proof, or visual proof.

## Mandates Followed

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `.agents-skills/STRM_Async_Asset_Upload_Texture_Settings.txt`
- `.agents-skills/AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

## Evidence Boundary

Evidence used:

- `STATIC_DOC`: worker reports, planning reports, readback packet, asset index.
- `STATIC_SOURCE`: CSV rows, static path references, summarized serialized refs from prior reports.

Evidence absent:

- No Unity run.
- No import readback.
- No Play Mode.
- No Addressables Groups window readback.
- No Addressables build or catalog proof.
- No prefab, material, scene, project setting, or import mutation.
- No Memory Profiler, Profiler, Frame Debugger runtime capture, GCMonitor, player build, listening pass, or visual route capture.

Every Unity-facing, runtime-facing, memory-facing, mix-facing, and visual-facing claim remains `PENDING VERIFICATION`.

## Planning Precedence

| Planning area | Use first | Use second | Controller disposition |
|---|---|---|---|
| Addressables row queue | `Docs/AssetAudit/ADDRESSABLES_ASSET_GROUP_PLAN_20260605.md` and `.csv` | `Docs/Reports/AssetSystem_20260605/ADDRESSABLES_GROUP_PLAN_3220_20260605.md` | Use the AssetAudit CSV rows for priority, source scope, owner lane, blocker text, and row-level dispatch. Use 3220 for lifecycle law, H8-style naming direction, load mode discipline, proof artifact list, and future residency questions. Do not create two parallel group taxonomies. |
| Addressables naming | 3220 | AssetAudit CSV | 3220 gives stricter future group/key/label grammar. AssetAudit group names are dispatch aliases until a future Unity owner creates real settings intentionally. |
| Addressables load mode | Both | None | Both plans reject heavy packed residency without memory proof and prefer `RequestedAssetAndDependencies` for heavy sky, ocean, terrain, flora, audio, and prefab groups. No conflict. |
| Audio policy direction | `Docs/Reports/AssetSystem_20260605/AUDIO_POLICY_ADOPTION_DRAFT_3221_20260605.md` | `Docs/AssetAudit/AUDIO_IMPORT_POLICY_EXCEPTION_TABLE_20260605.md` and `.csv` | Use 3221 only as patch-basis text for a future authority decision. Use the exception table as the current per-class blocker ledger. No import edits from either file. |
| Audio exception rows | Exception table CSV | 3221 | Exception CSV owns row-level class, owner route, proof needed, blockers, and disposition. 3221 owns proposed policy clause wording and default classifier rationale. |
| Texture import roles | `Docs/AssetAudit/TEXTURE_IMPORT_ROLE_MATRIX_20260605.md` and `.csv` | Addressables plans and asset index | Texture matrix governs sRGB, texture type, mipmaps, streaming mips, compression target, source-only status, and role proof. Addressables plans govern lifecycle only after texture/material role proof exists. |

## Conflict And Overlap Table

| Area | Artifact A | Artifact B | Conflict or overlap | Controller disposition |
|---|---|---|---|---|
| Addressables group names | 3220 uses `H8_SKY_SURFACE_CELESTIAL`, `H8_OCEAN_CREST_CONTACT`, `H8_AUDIO_MUSIC_DIRECTOR`, and similar H8 names. | AssetAudit plan uses `group_surface_sky_celestial`, `group_surface_water_contact`, `group_audio_music`, and similar aliases. | Two naming systems exist for the same future groups. | Treat AssetAudit names as dispatch aliases. Use 3220 grammar for future real group/key naming after ASSET_OWNER_06 proof. |
| Addressables priority | 3220 groups are structured by route/category and proof needs. | AssetAudit CSV has explicit P0/P1/P2 row priority. | 3220 has no equivalent queue priority field. | Use AssetAudit priority for owner scheduling. Keep 3220 proof and lifecycle clauses attached to each matching row. |
| Addressables source scope | 3220 gives broad route groups and resident budget placeholders. | AssetAudit CSV gives exact source scopes and blocker summaries. | 3220 is broader; CSV is more actionable for row owners. | Dispatch from CSV; consult 3220 before writing group/key/load-mode proposals. |
| Addressables proof artifacts | 3220 lists ASSET_OWNER_06 artifact paths for sky, Crest, terrain, flora, UI, audio, and Addressables. | AssetAudit plan lists proof classes but less artifact naming. | 3220 is more exact. | Future Unity owner must use 3220 artifact list plus the ASSET_OWNER_06 packet. |
| Heavy group load mode | 3220 rejects `AllPackedAssetsAndDependencies` for heavy mixed groups without memory proof. | AssetAudit plan requires `RequestedAssetAndDependencies` unless bounded exception proof exists. | No conflict. | Heavy groups stay proposed-only and use requested dependencies unless future memory proof supports a tiny always-hot exception. |
| Audio policy authority | 3221 proposes hybrid duration-based policy text. | Exception table keeps conflict at `PENDING_AUTHORITY_DECISION`. | 3221 is closer to stable-doc wording; exception table is more conservative about authority state. | Authority conflict remains open. Use hybrid text only as proposal input. No import mutation. |
| Music handling | 3221 says long music may stream through MusicDirector with owned prefetch/release if adopted. | Exception table says music profile row remains `PENDING_AUTHORITY_DECISION` with mixer nulls and no Addressables proof. | Same direction, different gate strictness. | Keep music streaming as static route evidence only. Mixer nulls and owner/key/release proof block future promotion. |
| Long ambience | 3221 allows long non-critical ambience streaming through owned active-bank route if adopted. | Exception table keeps long ambience at `PENDING_AUTHORITY_DECISION`, with Q0.45 and warning-mask risks. | Same direction, but quality and warning proof gaps remain. | No broad ambient import changes. Future owner must prove Q70 target, bank budget, warning ducking, and memory. |
| Player loops | 3221 separates `player_loop` from generic SFX and blocks long loops until owner exception proof. | Exception table names `player_loop_breath_suit_swim` as P0 authority decision risk. | No conflict. | Treat player loops as first-person continuity assets, not generic SFX. Direct prefab refs remain blockers. |
| UI/SFX | 3221 says generic SFX never stream and short UI/SFX are low-latency. | Exception table rows cite direct Player.prefab refs and mixed import state. | No conflict; CSV has more blockers. | Exception rows govern owner actions. No generic streaming SFX exception. |
| Texture source status | Texture matrix marks foam/contact, Aegir/cloud, generated PBR, and flora stacks as source/candidate only. | Addressables plans include corresponding visual groups. | Lifecycle plans may look like promotion if read alone. | Texture matrix blocks import/material promotion until Unity slot/import/visual proof exists. |
| UI oxygen | Texture matrix splits `ui/OXYGEN.png` icon source from `oxygen-tank.png` mask. | Addressables CSV has `group_core_ui_sprites` and notes no SpriteAtlas proof. | No conflict. | UI owner must prove binding, import role, atlas, and HUD visual state before using either asset as route proof. |

## Actionable Next Steps

### Before ASSET_OWNER_06 Unity Readback

1. Do not create Addressables settings, groups, labels, keys, catalogs, SpriteAtlas assets, or import setting changes.
2. Use `Docs/AssetAudit/ADDRESSABLES_ASSET_GROUP_PLAN_20260605.csv` as the owner queue and attach 3220 lifecycle/proof clauses to each matched row.
3. Use `Docs/AssetAudit/AUDIO_IMPORT_POLICY_EXCEPTION_TABLE_20260605.csv` as the audio exception queue and attach 3221 proposed policy text only as a patch-basis input.
4. Use `Docs/AssetAudit/TEXTURE_IMPORT_ROLE_MATRIX_20260605.csv` before any future texture import/meta/material decision.
5. Keep all hard blocks visible in handoff: foam, proxy flora materials, MusicDirector mixer nulls, Player direct clips, missing Addressables settings, missing SpriteAtlas proof, and no runtime/GC/memory proof.
6. Do not use static reachability, CSV rows, or planning tables as residency, visual, import, or mix proof.

### During ASSET_OWNER_06 Readback

1. Follow `taskslocal/asset_system_20260605/ASSET_OWNER_06_UNITY_READBACK_EXECUTION_PACKET.md` exactly.
2. Stop if process, CPU, import, compiler, package, dirty-scene, auto-create, save-prompt, missing material, Console error, or Frame Debugger gate fails.
3. Record read-only proof for sky/Aegir/moons, Crest/ocean/foam, terrain/geology, flora proxy materials, UI oxygen sprites, audio config/prefab refs, and Addressables settings/data.
4. Write artifacts only under `Docs/Screenshots/HectonProofPackets/` and `Docs/Reports/AssetSystem_20260605/`.
5. Do not press Apply, save scenes, save project, mutate materials, mutate prefabs, mutate import settings, create SpriteAtlas assets, create Addressables settings, or build bundles.
6. If Unity auto-creates or dirties Addressables assets, stop and record the object path. Do not save.

### After ASSET_OWNER_06 Readback

1. Compare Unity readback tables against the Addressables CSV row queue, audio exception table, and texture role matrix.
2. Split each item into one owner, one route, and one proof artifact. Do not merge route ownership across audio, texture/material, UI, prefab, and streaming owners.
3. Draft stable authority patch text only after controller/human direction for the audio policy conflict.
4. For any future Addressables implementation, require settings asset, group, schema, label, key, load mode, handle owner, ref-count route, release ledger, memory proof, and pressure behavior.
5. For any future texture/material implementation, require import role proof, material slot readback, visual capture, mip/residency proof, and Frame Debugger or Stats where visual path matters.
6. For any future audio implementation, require import readback, mixer or native/DSP route proof, active bank budget, runtime mix/listening pass, audio-thread safety proof, and `0 B/frame` proof.

## Hard Blocks That Remain

| Block | Static fact | Required future proof |
|---|---|---|
| Rejected foam | `foam.png` is rejected as visible waterline art but serialized-reachable through active world/ocean users. | Crest/ocean slot readback, replacement/contact mask proof, bright waterline screenshot, Frame Debugger/Stats, memory/residency proof. |
| Proxy flora materials | Four `WorldProceduralProxy` coral/kelp materials are referenced in `02_HECTON_WORLD.unity`. | Object/material/visibility table, route screenshots, candidate prefab/material proof, LOD/silhouette proof, no proxy visible route use. |
| MusicDirector mixer nulls | `MusicDirectorConfig_Global.asset` has null `_musicMixerGroup` and `_stingerMixerGroup` in static evidence. | Unity config readback, owned mixer or native/DSP bypass route, runtime MusicDirector capture, mix/listening proof. |
| Player prefab direct clips | `Player.prefab` has direct `AudioClip` refs without owner/load/release proof. | Prefab readback by component path, owner exception or reroute, Addressables key/group or lifetime policy, runtime playback proof, `0 B/frame` proof. |
| No Addressables settings | `Assets/AddressableAssetsData` exists with zero static settings/group/catalog evidence. | Addressables settings, groups, schemas, profiles, labels, entries, load modes, handle/ref-count/release ledger, memory and pressure proof. |
| No SpriteAtlas proof | Static scope found no `.spriteatlas` or `.spriteatlasv2` under `Assets/_Project`. | Unity Project/Inspector readback, atlas/import proof, HUD binding proof, compact HUD screenshot. |
| No runtime/GC/memory proof | All current artifacts are static plans or static source summaries. | Unity Console, Play Mode or player capture, Profiler/GCMonitor, Memory Profiler, VRAM/texture residency, audio mix, and visual route captures. |

## Continuous GlobalQualityWeight Consequences

`GlobalQualityWeight` is a continuous scalar. It can scale preload radius, active-bank breadth, mip target, decorative density, diagnostic depth, upload budget, and release cadence. It must not alter owner route, gameplay truth, cue IDs, Addressables keys, DTO layout, save identity, warning facts, or release order.

| Lane | Consequence |
|---|---|
| Low / compact | Preserve sky, Aegir silhouette, waterline readability, photic terrain identity, route flora silhouettes, oxygen HUD clarity, breath/player loops, warnings, and route cues. Reduce speculative preload, mip residency, decorative flora/prefab density, secondary music/ambient breadth, and far LOD residency smoothly. No flat or dark fallback art. |
| Middle | Admit route-owned PBR stacks, clean foam/contact masks, UI atlas ownership, MusicDirector/ambient bank ownership, and candidate prefab pools only after readback and owner proof. Keep active banks and streaming groups bounded. |
| High | Spend spare budget on longer mip/LOD residency, richer Aegir/cloud detail, stronger shoreline/contact response, denser near-field geology/flora, smoother audio transitions, and wider MusicDirector variation after memory/frame/mix proof. |
| Ultra | Extend visual and audio detail with richer cloud/Aegir layers, shoreline breakup, wet material layering, denser route dressing, wider music/ambient prefetch, and stronger reverb/occlusion only while proof stays within budget. Do not bypass owner, key, ref-count, release, or compact-lane constraints. |

## Regression Model

| Axis | Current report impact | Future regression risk | Required proof |
|---|---|---|---|
| CPU | No runtime code or Unity setting changed. | Load dispatch spikes, Addressables lookup churn, async instantiation cost, mixer/DSP contention, import-driven editor stalls. | Profiler markers, load/release timing, MusicDirector runtime capture, no main-thread stall claim without artifact. |
| GC | No runtime code changed. No `0 B/frame` claim. | String key construction, coroutine load loops, direct clip event paths, UI sprite/text churn, managed audio callbacks. | GCMonitor/Profiler through cue changes, HUD updates, warnings, player loops, ambient swaps, and streaming operations. |
| Memory/VRAM | No import, residency, or group data changed. | Heavy group overpacking, broad resident music/ambient conversion, texture mip pressure, UI atlas bloat, unreleased handles. | Memory Profiler, loaded handle count, resident/committed memory split, VRAM/texture residency, release ledger, pressure response. |
| Cadence | No runtime cadence changed. | Same-frame load/release loops, immediate LOD/bank flipping, unbounded prefetch, audio start jitter, warning delay. | Owner phase, preload window, release window, hysteresis, active bank limit, ASSET_OWNER_06 and runtime cadence artifacts. |
| Correctness | Consolidates static planning only. | Duplicate group taxonomies, unresolved audio authority conflict, texture source-only assets promoted by mistake, direct refs treated as lifecycle ownership. | One fact, one owner, one route, one proof artifact per row before future mutation. |

## Final Controller Disposition

- Addressables: use AssetAudit CSV as the queue, 3220 as lifecycle/proof/naming guidance. Do not create groups before ASSET_OWNER_06 proof.
- Audio: use exception table CSV as the queue, 3221 as patch-basis text only. Authority conflict remains open.
- Texture: use texture import role matrix as the import-role and source-status guard. It blocks source-only texture promotion until Unity/material proof exists.
- Unity readback: use ASSET_OWNER_06 packet. No save, no apply, no asset mutation.

Final status: `PENDING VERIFICATION`.

# 1804 Applied Lore DataMonolith Reconciliation

ID: 1804  
Role: APPLIED_LORE_DATAMONOLITH_RECONCILER  
Evidence class: STATIC_DOC / STATIC_SOURCE / STATIC_BINARY only.  
Runtime state: PENDING UNITY/DATAMONOLITH BAKE.  

## Executive State

The AppliedLore packet CSV shape is coherent: 6,900 rows, 460 packet IDs, exactly 15 locale rows per packet, 454 route cards, and the expected 15-locale roster.

The current `static_data.h8bin` is not cleared for full DataMonolith readiness. A direct static AppliedLore packet parity check against the binary passes, but the normal audit fails first on generated publication-page/frontmatter drift for `P151_BLACK_KEEL_CONTRACT_APPROACH/ru_RU`. Unity bake/import/boot/runtime proof was not attempted because CPU/editor state was busy.

The public-site route has an active content blocker. `P456_SITE_HOME_LONGFORM_BRIEF` has production-brief residue in current packet source and the generated `ru_RU` external-site page while still carrying `flags=0` / `source_ready` semantics for `en_US` and `ru_RU`. This is not a one-line schema fix. It needs writer/source repair before generated publication outputs can be trusted.

## Static Evidence

Commands run from `C:\hades\Hecton8`:

```text
python Tools/AppliedLoreRuntimeAudit.py --root . --source-only
```

Result:

```text
AppliedLore audit FAILED: Publication page C:\hades\Hecton8\Docs\Lore\AppliedContent\in_game_wiki\ru_RU\P151_BLACK_KEEL_CONTRACT_APPROACH.md missing frontmatter line: localization_status: draft_native_pass_pending
```

```text
python Tools/AppliedLoreRuntimeAudit.py --root .
```

Result:

```text
AppliedLore audit FAILED: Publication page C:\hades\Hecton8\Docs\Lore\AppliedContent\in_game_wiki\ru_RU\P151_BLACK_KEEL_CONTRACT_APPROACH.md missing frontmatter line: localization_status: draft_native_pass_pending
```

Direct static packet-record parity through `Tools/AppliedLoreRuntimeAudit.py` module functions:

```text
APPLIED_LORE_BLOB_PARITY_OK rows=6900 blob_records=6900 blob_bytes=3270784 localization_bytes=1265914
```

Binary metadata:

```text
Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin
Length: 3270784
LastWriteTime: 2026-06-04 03:09:53
```

Editor contention gate:

```text
CpuLoadPercent : 74
Unity:26320
Unity:40660
Unity:58160
```

No Unity bake, editor validation, PlayMode validation, TMP layout proof, or runtime UI proof was produced by 1804.

## Source Shape

`Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv`

```text
header=packet_id,locale,release_set_id,article_id,unlock_id,surface_mask,title,scanner,terminal,audio,in_game_wiki,external_site,field_note,poi_tags,biome_tags,flags
rows=6900
packet_ids=460
locales=ar_SA,de_DE,en_US,es_ES,fr_FR,he_IL,id_ID,ja_JP,ko_KR,nl_NL,pl_PL,pt_BR,ru_RU,uk_UA,zh_CN
per_packet_counts=15rows=460packets
flags=0=1715;1=5185
surface_masks=127=6900
```

`Assets/_SourceData/DataMonolith/Narrative/applied_lore_route_cards.csv`

```text
header=route_card_id,route_card_hash_hex,route_card_hash_uint,phase_id,phase_hash_hex,phase_hash_uint,depth_min_m,depth_max_m,primary_surface,primary_surface_mask,ending_pressure,ending_pressure_hash_hex,ending_pressure_hash_uint,packet_ids,packet_hashes_hex,packet_hashes_uint,required_packet_ids,required_packet_hashes_hex,required_packet_hashes_uint
rows=454
unique_route_cards=454
```

`Docs/Lore/AppliedContent/Publication_Surface_Index.csv`

```text
header=surface,locale,direction,packet_id,release_set_id,article_id,unlock_id,localization_status,localization_flags,poi_tags,biome_tags,page_path,title
rows=13800
status=draft_native_pass_pending=10360;source_ready=3440
surfaces=external_site=6900;in_game_wiki=6900
locales=ar_SA=920;de_DE=920;en_US=920;es_ES=920;fr_FR=920;he_IL=920;id_ID=920;ja_JP=920;ko_KR=920;nl_NL=920;pl_PL=920;pt_BR=920;ru_RU=920;uk_UA=920;zh_CN=920
```

## Content-Type Matrix

| Content Type | Current Source Files | LocID / Hash Strategy | 15-Locale Status | Runtime Layer Target | Evidence Object / Unlock | Proof State | Blockers |
|---|---|---|---|---|---|---|---|
| External wiki / public site | `applied_lore_packets.csv`, generated `Docs/Lore/AppliedContent/external_site/<locale>/*.md`, `Publication_Surface_Index.csv`, packet JSON bundles | `packet_id + locale + external_site`; packet hashes in route CSV (`packet_hashes_hex`, `packet_hashes_uint`) | 15 locale rows exist per packet; 10,360 publication rows still `draft_native_pass_pending`; `source_ready` is not native review | Static site / reader route only until publication proof | `unlock_id`, `poi_tags`, `biome_tags`, route-card prerequisites | CANDIDATE / PENDING PUBLICATION VERIFICATION | `P456_SITE_HOME_LONGFORM_BRIEF` source/page still exposes production-brief text and player-invisible instructions; current `source_ready` semantics are unsafe |
| In-game PDA encyclopedia / codex | `applied_lore_packets.csv` fields `title`, `in_game_wiki`; generated `in_game_wiki/<locale>/*.md`; baked binary section 27 | `packet_id + locale`; FNV packet/locale keys in `AppliedLoreRuntimeAudit.py`; runtime packet hash fields | 15 locale rows exist; 5,185 packet rows flagged draft; no native-final proof | `PDAEncyclopediaStreamer`, AppliedLore binary records | `unlock_id`, `article_id`, `release_set_id` | STATIC_BINARY packet parity only; PENDING UNITY/TMP proof | Full audit stops on `P151_BLACK_KEEL_CONTRACT_APPROACH/ru_RU` page/index/source-status drift |
| Scanner field notes | CSV fields `scanner`, `field_note`; generated packet JSON; scene/prefab binding maps from Batch 17 | `packet_id + locale + scanner/field_note`; scannable components use packet hash fields | 15 locale rows exist; draft flags remain majority | `ScannableTarget`, `H8AppliedLoreRuntime.TryWriteTitleUtf16`, field-note UI surfaces | `poi_tags`, `biome_tags`, route-card primary surfaces | STATIC_SOURCE / STATIC_BINARY packet parity only | 1777 reports 61,060 static text-bound/status-risk findings; no scanner UI/TMP proof; placement coverage remains weak |
| Terminal / survivor / corporate notes | CSV fields `terminal`; route-card primary surface `terminal`; generated packet JSON | `packet_id + locale + terminal`; terminal anchor fields use packet hash constants | 15 locale rows exist; draft status must follow `flags` and generated status | `MessageTerminal`, `TerminalOS`, AppliedLore runtime | `unlock_id`, terminal route cards, prerequisites | STATIC_SOURCE / PENDING UNITY proof | `P151` status drift blocks audit; route-card placement coverage from 1778 is not enough for game integration |
| Audio blackbox transcripts | CSV field `audio`; Batch 17 subtitle/audio artifacts | `packet_id + locale + audio`; future subtitle keys must stay hash-backed | 15 locale text rows exist; no VO/native/timing proof | Audio log/subtitle presentation, `BabelSubtitleSyncRuntime` if wired | Audio route cards and `unlock_id` | CANDIDATE / PENDING AUDIO RUNTIME PROOF | No subtitle timing, font, VO, or localized audio proof; draft rows cannot be treated as finished copy |
| Player notes / facts | Batch 17 player-note candidates and packet metadata; no runtime schema ownership found in 1804 scope | Requires approved LocID and owner route before bake; do not invent fields | Not established as native/localized runtime rows by 1804 | PENDING schema owner | Needs explicit evidence object and fact owner per writing/narrative bibles | CANDIDATE | 1776 schema drift: packet-level related/crosslink arrays absent; relationships live in generated publication/cluster indexes |
| Localization QA | `Localization_Status_Index.md`, packet CSV `flags`, publication index status, 1777 audit artifacts | Locale roster fixed to 15; status must be source/draft/native-review proof-backed | 1,715 packet rows `flags=0`; 5,185 packet rows `flags=1`; 10,360 generated publication rows draft | Runtime localization manager / font resolver / reader/TMP surfaces | Locale-specific text records | STATIC_SOURCE ONLY | No native review proof; no RTL/CJK reader visual proof; no TMP overflow/font atlas proof |
| Lore reader prototype / site agent | `Docs/Lore/AppliedContent/reader.html`, generated Markdown/CSV/JSON | Reads packet/page/index data; no Unity runtime route | 15 locale definitions and RTL flags reported by 1779; HTTP smoke only | Static local reader/protosite | Publication index, packet JSON, sample pages | STATIC_SOURCE plus local HTTP smoke from 1779 | Browser QA absent; P456 source residue and P151 status drift can surface bad public copy unless quarantined |

## Current Blockers

### BLOCKER-1804-001: P151 Publication Frontmatter Drift

The normal source-only and full audit both fail before reaching route/binary route validation:

```text
Publication page C:\hades\Hecton8\Docs\Lore\AppliedContent\in_game_wiki\ru_RU\P151_BLACK_KEEL_CONTRACT_APPROACH.md missing frontmatter line: localization_status: draft_native_pass_pending
```

Verified mismatch:

- `applied_lore_packets.csv` row `P151_BLACK_KEEL_CONTRACT_APPROACH/ru_RU` has `flags=1`.
- `RS031_FIRST_HOUR_PLAYABLE_SPINE.packets.json` carries `Draft RU localization pending native pass.` on the localized RU packet fields.
- Generated page `Docs/Lore/AppliedContent/in_game_wiki/ru_RU/P151_BLACK_KEEL_CONTRACT_APPROACH.md` currently has `localization_status: source_ready`.

Required fix: run the owning source/page export route after P456 source repair, or apply a targeted generated-page/index correction only if the exporter owner confirms it will not be overwritten. Do not claim full audit proof until this is fixed and the audit reruns clean.

### BLOCKER-1804-002: P456 Public-Site Content Is Still a Production Brief in Source

Current hits in generated `ru_RU` public page:

```text
Docs/Lore/AppliedContent/external_site/ru_RU/P456_SITE_HOME_LONGFORM_BRIEF.md:19:Longform spine: ...
Docs/Lore/AppliedContent/external_site/ru_RU/P456_SITE_HOME_LONGFORM_BRIEF.md:23:Public brief: ...
Docs/Lore/AppliedContent/external_site/ru_RU/P456_SITE_HOME_LONGFORM_BRIEF.md:27:SITE HOME: ...
Docs/Lore/AppliedContent/external_site/ru_RU/P456_SITE_HOME_LONGFORM_BRIEF.md:35:Assemble for website: ...
```

Current packet source has the same problem:

```text
Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv:6827:P456_SITE_HOME_LONGFORM_BRIEF,en_US,...Longform spine...
Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv:6828:P456_SITE_HOME_LONGFORM_BRIEF,ru_RU,...Longform spine...
```

This is a content/source blocker, not generated-page-only drift. A blanket page export will preserve the residue because the source row still contains it. The writer/content owner must rewrite the source packet fields and re-export.

### BLOCKER-1804-003: Legacy Single-Packet JSON Drift

1776 verified 9 packet IDs referenced by `Publication_Surface_Index.csv` outside `.packets.json` bundle scope, all present as legacy single-packet `H8.APPLIED_CONTENT_PACKET.V0` JSON. This is schema/exporter ownership drift. It is not dead content and not a safe 1804 hand edit.

Required fix: schema/exporter owner decides whether to fold legacy single-packet JSON into bundle files or preserve/document the legacy path as a first-class ingestion route.

### BLOCKER-1804-004: Scene / Prefab Placement Coverage Is Weak

1778 reported:

```text
scene_bindings=7
prefab_bindings=42
authoring_bindings=49
scene_placement_covered_rows=34
```

This is inadequate against 460 packet IDs. 1804 did not run Unity placement repair because Unity/editor state was busy. Treat world placement as PENDING UNITY AUTHORING PROOF.

### BLOCKER-1804-005: Localization Is Not Release-Clean

1777 reported:

```text
LoreTextBoundsVerifier.py: packets=460 surfaces=48300 issues=61060 collisions=0 rewrites=0
```

1777 also reported literal marker hits for draft/native-pass phrases, placeholders, TODO, and machine-localization phrases. Current source still has 5,185 packet rows with draft flags. There is no native-final, native-reviewed, RTL visual, CJK wrapping, TMP overflow, or font-atlas proof.

### BLOCKER-1804-006: Full DataMonolith Readiness Not Proven

The direct static packet parity check passes for AppliedLore packet records:

```text
APPLIED_LORE_BLOB_PARITY_OK rows=6900 blob_records=6900 blob_bytes=3270784 localization_bytes=1265914
```

This downgrades the older P288 stale-packet mismatch as not currently reproduced for packet records. It does not clear:

- generated publication page/index validation;
- route-record validation after the P151 gate;
- Unity import/bake ownership;
- runtime boot;
- scene placement;
- reader/TMP rendering;
- localized UX behavior.

## Safe Fix Decision

No source content edits were applied by 1804.

Reasons:

- `P151` is generated publication/index drift and needs the owning export route or a confirmed targeted generated-output correction.
- `P456` requires editorial rewrite of source packet fields across 15 locales/statuses. That exceeds a small schema-safe fix.
- `static_data.h8bin` inspection/parity was read-only. Baking was blocked by CPU/editor contention.

## Scalability Consequences

Low: only short, evidence-backed surfaces should be exposed. Scanner/title/field-note copy must avoid draft markers and production-brief residue because cheap devices still need premium readability.

Middle: PDA/terminal text can stream fuller packet bodies, but source truth and unlock gating must remain the same as low tier.

High: audio, route-card crosslinks, and richer reader views can be enabled after native review and runtime UI proof.

Ultra: extended archive/site views can expose more longform context, but they must not add new facts outside the same packet/unlock route.

GlobalQualityWeight may scale presentation density, cadence, and optional telemetry. It must not change packet truth ownership, DTO layout, locale identity, or unlock authority.

## Follow-Up Prompt: Writer / Content Agent

```text
ID: NEXT_WRITER_APPLIED_LORE_PUBLIC_REPAIR
Role: APPLIED_LORE_WRITER_CONTENT_REPAIR

Read AGENTS.md, PROJECT_BIBLES.md, VISION_LOCKS.md, TASTE.md, quality.md, writing.md, narrative.md, localization.md, and Docs/Lore/WriterScenarioAgentPrompt.md. Load relevant writing/localization/data mandates only.

Scope:
- Repair source packet content, not just generated pages.
- Start with P456_SITE_HOME_LONGFORM_BRIEF and P151_BLACK_KEEL_CONTRACT_APPROACH.
- Rewrite P456 fields in Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv so public/site copy is a real player-facing article spine, not production brief instructions.
- Preserve article_id, unlock_id, packet_id, route-card identity, poi_tags, biome_tags unless a data owner explicitly approves a schema change.
- For non-English rows, keep honest draft/native-review-pending status unless native review proof exists. Do not claim native-final.
- Produce a concise issue list for any other rows that contain "Longform spine", "Public brief", "SITE HOME", "Assemble for website", TODO, placeholder, or draft marker leakage.

Output:
- Exact rows changed.
- Before/after excerpts.
- Honest locale status table.
- No Unity/runtime claims.
```

## Follow-Up Prompt: Data / Bake Agent

```text
ID: NEXT_DATA_APPLIED_LORE_BAKE
Role: APPLIED_LORE_DATAMONOLITH_BAKE_VALIDATOR

Read AGENTS.md, PROJECT_BIBLES.md, VISION_LOCKS.md, quality.md, data.md, authoring.md, localization.md, and the AppliedLore tooling docs/source. Load CSV/binary bridge, runtime struct layout, localization, and zero-GC mandates.

Preflight:
- Do not run Unity or dotnet if CPU is above 50 percent or Unity/dotnet/csc is already busy.
- Start from current source after writer/content repair.

Tasks:
- Rerun source-only audit: python Tools/AppliedLoreRuntimeAudit.py --root . --source-only
- Fix generated publication status drift through the owning exporter path, not blind hand edits, unless a targeted generated-output correction is explicitly justified.
- Run the full offline audit: python Tools/AppliedLoreRuntimeAudit.py --root .
- If Unity is free, bake static data through Hecton8/Data Monolith/Bake Static Data.
- Rerun full audit after bake and record static_data.h8bin metadata.
- If runtime proof is requested, run boot/PlayMode checks separately and label them distinctly.

Output:
- Command outputs.
- static_data.h8bin size/timestamp.
- AppliedLore packet/route counts.
- Remaining blockers, if any.
- No DataMonolith readiness claim without clean full audit plus bake/import/boot proof.
```

## Follow-Up Prompt: Reader / Site Agent

```text
ID: NEXT_READER_APPLIED_LORE_QUARANTINE
Role: APPLIED_LORE_READER_SITE_QUARANTINE

Read AGENTS.md, PROJECT_BIBLES.md, VISION_LOCKS.md, TASTE.md, quality.md, writing.md, localization.md, and the AppliedContent reader docs. Load relevant reader/localization mandates.

Tasks:
- Update reader/site presentation so draft rows, status drift, and production-brief residue are surfaced to controllers and hidden or quarantined from public/player-like views.
- Add explicit filters/warnings for "Longform spine", "Public brief", "SITE HOME", "Assemble for website", TODO, placeholder, and draft/native-pass markers.
- Prove P456 public route after source repair; do not call the current P456 route publication-ready.
- Run local HTTP smoke only; do not claim browser visual proof without Playwright/manual screenshot evidence.

Output:
- Reader files changed.
- Smoke command outputs.
- Remaining public-site blockers.
- No native-review or Unity-runtime claims.
```

## Final Classification

- AppliedLore packet CSV shape: STATIC_SOURCE PASS.
- AppliedLore packet static binary parity: STATIC_BINARY PASS for packet records only.
- Full AppliedLore audit: FAIL at P151 publication drift.
- Public-site home route: BLOCKED by P456 source residue.
- 15-locale completeness: row coverage exists; native quality does not.
- DataMonolith runtime readiness: NOT PROVEN.
- Unity/editor proof: NOT RUN.

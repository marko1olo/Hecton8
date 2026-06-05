# 1825 Terminal Scanner Audiolog Placement Manifest

Evidence class: STATIC_SOURCE / STATIC_DOC. No Unity, build, exporter, bake, PlayMode, profiler, scene placement, AppliedLore source edit, generated page edit, audio implementation edit, UI implementation edit, or native-final localization proof was produced.

## Scope

Agent 1825 produced a static placement manifest and queue for first-route and early-game player-facing content surfaces. The owned queue is:

- `Docs/Reports/Batch18/1825_CONTENT_PLACEMENT_QUEUE.csv`

The queue converts existing AppliedLore rows and previous static handoffs into route placement work. It does not change packet text, pages, indexes, source data, scenes, binaries, audio, UI, or reader code.

## Inputs Used

- Root authority: `AGENTS.md`, `PROJECT_BIBLES.md`, `VISION_LOCKS.md`, `TASTE.md`, `quality.md`, `writing.md`, `narrative.md`, `localization.md`, `ui.md`, `audio.md`, `gameplay.md`.
- Relevant mandates: QA evidence text filtering, UI localization/RTL/font swap, diegetic UI, audio DSP/thread-safe SPSC, designer facade/CSV-binary bridge.
- Scenario/context: `Docs/Lore/WriterScenarioAgentPrompt.md`.
- Batch 18 evidence: 1804, 1805, 1811, 1816, and 1820 reports.
- Applied source: `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv`.
- Applied content structure under `Docs/Lore/AppliedContent/`.
- Earlier placement handoffs/audits: 1773 scanner/field-note inventory, 1774 terminal inventory/handoff, 1775 audio blackbox inventory/handoff, 1778 binding/runtime matrix and handoff.

`Docs/Actual Domains of Project.txt` was not present at task start.

## Static Findings

- AppliedLore source contains 460 packet IDs and 6900 packet-locale rows. The 15-locale roster is intact per 1778/1820.
- English `en_US` is the only full source-candidate locale set per 1820. Non-English rows remain draft/native-review pending unless separately proven.
- Game scanner/terminal/wiki/field-note rows are static source candidates only. They still need runtime UI, string-pool/DataMonolith, placement, and layout proof.
- Audio rows are script/subtitle candidates only. They still need VO/source, subtitle timing, narrow-UI segmentation, loudness/mix, and implementation proof.
- Public/site rows are editorial/spoiler/site-integration candidates only. P456 English was repaired by 1811; P457-P460 remain blocked by production-residue text.
- P151-P155 have ru_RU generated wiki/site status drift per 1820. That blocks localized publication claims; it does not invalidate the English first-hour source candidates.
- 1778 proves binding/hash knowledge is available for many packets, but also proves scene placement is incomplete and `static_data.h8bin` is stale after source generation. No runtime readiness claim is valid from this task.

## Surface Rules

Scanner facts:
- Must be concise instrument output: observed object, confidence/risk/action, route consequence.
- Must not carry article instructions, public-brief wording, omniscient narrator prose, or fake scientific filler.
- Best first-route scanner placements: P001, P004, P005, P007 scanner-only, P011, P017-P019, P022, P032, P152, P155, P250.

Terminal notes:
- Must read as old systems, operators, work orders, claim ledgers, diagnostics, and procedural/field records.
- Must not become villain monologues, lore essays, or detached encyclopedia prose.
- Best first-route terminal placements: P001, P002, P008, P012, P014-P015, P021, P023, P151, P153-P154, P247-P249, P287.

Encyclopedia entries:
- Must unlock after physical observation, scan, repair, or document recovery.
- Must be English prototype candidates only until runtime PDA proof and native review exist.
- Best early entries: P001, P003, P016, P020, P031. P007 wiki is blocked by residue.

Survivor/player notes:
- Must attach to physical evidence such as lockers, nameplates, correction notes, claim caches, and route tags.
- Must not tell the player what to think before the object proves it.
- Best early notes: P003, P006, P288, P289, P438.

Audiolog scripts:
- Must be written/localized/timed as subtitle-safe transcript beats before voice/audio implementation.
- English text is not VO proof. Non-English text is not native-final proof.
- First-hour safe audio candidates: P001, P002, P151, P246, P286, P436. Early/mid candidates: P249, P250, P290, P437-P439. P440 is ending-gated.

Public/site/wiki content:
- Must pass editorial, spoiler, site integration, and localization status gates.
- P456 English is the only repaired public-home seed in this slice.
- P457-P460 are blocked and must be source-rewritten before placement.

Authoring/specification packets:
- P316, P320, P432, P433, and P446 are useful as planning constraints and owner handoff rows.
- They must not ship as player-facing prose.

## Priority Placement Shape

First-hour physical anchors come first:

- Black Keel claim console: P151/P246/P436.
- Damaged bathy-drop/capsule diagnostic panel: P001/P152/P247/P286.
- P-63 pump room and work order: P153/P248/P287.
- Sanitized accident contradiction: P154/P249.
- First Atlas useful-wrong repair trace: P005/P155/P250/P439.
- Shallow/world awe and route context: P016/P017/P020/P031, gated behind observation rather than darkness-as-cover.

Do not spend first-route terminal slots on broad article/card rows before these anchors exist.

## Handoff Gates

- DataMonolith/game integration: future owner must use stable packet IDs/hash/string-pool paths, then perform serialized importer/exporter/bake only after P151/P457-P460 source drift is resolved. This task did not run any bake/exporter.
- Runtime/UI proof: future owner must prove scanner, terminal, PDA encyclopedia, field note, and NarrativeDiscovery placement with layout and no runtime allocation claims from actual runtime evidence.
- Audio proof: future owner must prove audio source, subtitle segmentation, timing, and localized layout before calling any row VO-ready.
- Localization proof: all non-English rows in this queue remain draft/native-review pending unless a separate native review artifact exists.
- Public proof: site/public rows require editorial/spoiler pass and publish integration proof.

## Scalability Consequences

- Low: place the high-priority English scanner/terminal rows as short instrument/terminal strings only after string-pool proof; skip optional audio playback and broad public/wiki surfaces.
- Middle: add English PDA encyclopedia and field-note unlocks after placement and layout proof.
- High: add subtitle-safe English audiolog playback and more route-conditional surface entries after timing proof.
- Ultra: add public-site polish and multilingual presentation only after native review, RTL/CJK layout proof, and site/editorial gates pass.

## Completion

`PLACEMENT_MANIFEST_COMPLETE`.


# HECTON-8 Lore Localization Model

Status: working content contract.
Evidence class: STATIC_DOC / CONTENT_CORPUS.
Purpose: every lore unit must be usable by the game codex, terminal records, scanner notes, website articles, wiki pages, and localization pipeline.

## Authority And Proof Boundary

Use `narrative.md` for canon truth, evidence order, unlock route, and mission/text placement. Use `writing.md` for artifact prose and multilingual packet shape. Use `localization.md` for accepted localization statuses, locale rows, font/RTL/CJK/fallback rules, and runtime text proof gates. Use `textes.md` for real-world public copy, store copy, social posts, creator outreach, captions, and publication-facing claims.

This model is STATIC_DOC / CONTENT_CORPUS evidence only. It does not prove Unity placement, player-visible runtime delivery, h8bin/Data Monolith bake readiness, native review, public website publication, profiler/GC status, or platform readiness.

## Principle

Lore is not stored as loose prose.

One canon fact has one owner, one stable article ID, and many delivery voices. Translation changes only text. It must not change Article ID, LocID, hash, category, spoiler level, unlock route, or runtime layer.

Canonical meaning lives in `Canon_Locks.md`, `Lore_Bible.md`, and approved encyclopedia articles. `en_US` is the current source locale for the binary localization pipeline, but Russian control text from the user can remain the writer-side semantic source until converted into production English.

## Runtime Locale Roster

Current source data exists for `en_US`. The full target roster is:

| User Label | Runtime Locale | Script / Direction | Production Note |
|---|---:|---|---|
| en | `en_US` | Latin / LTR | Source locale for current JSON and binary bake. |
| ru | `ru_RU` | Cyrillic / LTR | High priority because user control language is Russian. |
| ja | `ja_JP` | Japanese / LTR | CJK font subset and line-break QA required. |
| cn | `zh_CN` | Simplified Chinese / LTR | `cn` is only a user alias. Runtime uses `zh_CN`. |
| fr | `fr_FR` | Latin / LTR | Text expansion QA. |
| es | `es_ES` | Latin / LTR | `es_419` can be added later if needed. |
| de | `de_DE` | Latin / LTR | Long compound-word overflow QA. |
| pl | `pl_PL` | Latin Extended / LTR | Diacritics and text expansion QA. |
| ua | `uk_UA` | Cyrillic / LTR | `ua` is only a user alias. Runtime uses `uk_UA`. |
| ar | `ar_SA` | Arabic / RTL | Modern Standard Arabic baseline; RTL QA required. |
| in | `id_ID` | Latin / LTR | `in` is treated as Indonesian, not India. |
| kr | `ko_KR` | Hangul / LTR | `kr` is only a user alias. Runtime uses `ko_KR`. |
| jewish | `he_IL` | Hebrew / RTL | `jewish` is only a user alias. Runtime uses `he_IL`. |
| portuguese | `pt_BR` | Latin / LTR | Default Portuguese target; `pt_PT` can be added later. |
| nl | `nl_NL` | Latin / LTR | Text expansion QA. |

Open production question: whether Spanish should add `es_419`, Portuguese should add `pt_PT`, Arabic should use a different regional default, and Chinese should later add `zh_TW`.

## Content Unit Contract

Every mature article must define:

- Article ID: stable, ASCII, Pascal or snake title form, for writer/index use.
- Loc Namespace: stable uppercase prefix for all localized strings in the article.
- Canon Owner: the doc that owns the truth.
- Runtime Layer: `Core`, `World`, or `Narrative`.
- Content Targets: website public, website archive, player codex, terminal document, scanner note, audio transcript, or dossier.
- Source Voices: neutral reference, marauder field note, Deep Reach internal, Atlas fragment, public domain.
- Spoiler Level: 0 to 4.
- First Unlock Route: event, depth band, scan, POI, contract, or ending.
- Localization Status: `source_authority`, `draft_machine_or_llm`, `BLOCKED_TRANSLATION_DRAFT`, `fluent_reviewed`, `native_reviewed`, or `runtime_ready`.
- Related Articles: article IDs only, not prose names.

## LocID Shape

LocIDs are stable across every locale:

`LORE_<DOMAIN>_<ARTICLE>_<VOICE>_<FIELD>`

Examples:

- `LORE_AEGIR_MOON_CATALOG_PUBLIC_TITLE`
- `LORE_AEGIR_MOON_CATALOG_PUBLIC_BODY`
- `LORE_AEGIR_MOON_CATALOG_MARAUDER_NOTE`
- `LORE_BLACK_KEEL_DEEP_REACH_BODY`
- `LORE_BLUE_DEBT_SCANNER_SHORT`

Rules:

- Do not translate LocIDs.
- Do not rename LocIDs after a content unit ships unless a migration table exists.
- One LocID maps to one text purpose.
- Long article bodies belong in `Narrative`.
- Scanner names, resource names, fauna names, and short object labels belong in `World`.
- UI labels, category names, and menu text belong in `Core`.

## Localization Entry Shape

Current localization files use the `H8.LOCALIZATION.V1` model. Lore entries should remain compatible with the existing pattern:

```json
{
  "LocID": "LORE_BLUE_DEBT_SCANNER_SHORT",
  "Hash": "0x00000000",
  "Layer": "Narrative",
  "Category": "codex_article",
  "Text": "Pressure-kept Xenon-Omega residue. Do not vent it into a warm cabin."
}
```

The hash is derived from LocID, not localized text. The same LocID should resolve to the same hash in every locale file.

## Website And Wiki Export

Website and wiki pages must not invent separate canon.

They export from the same article packet:

- Website Public: spoiler level 0 or controlled level 1.
- Website Archive: spoiler-enabled after release, or hidden before release.
- Wiki Summary: neutral reference, short.
- In-Game Codex: player-readable, may be incomplete or biased by unlock route.
- Terminal / Deep Reach / Atlas variants: in-world source voices, allowed to lie only when source-labeled.

The website can have richer prose, images, and chronology, but it must point back to the same Article ID and canon owner.

Website, wiki, or marketing-support markdown is a source candidate only. External publication requires `textes.md` routing and the applicable proof gates for the exact public claim, asset, channel, owner approval, and readiness language.

## Localization QA Notes

RTL:

- `ar_SA` and `he_IL` need explicit RTL layout QA.
- Keep Latin technical identifiers such as `HECTON-8`, `Atlas-6`, `Aegir-VIII`, `Xenon-Omega`, and ship registry marks stable unless a locale style guide says otherwise.
- Do not manually reverse source strings in content data.

CJK:

- `ja_JP`, `zh_CN`, and `ko_KR` need font subset, line-break, and no-space wrapping QA.
- Short scanner strings need separate localized short forms, not automatic truncation.

Expansion:

- `de_DE`, `ru_RU`, `pl_PL`, `fr_FR`, `es_ES`, `pt_BR`, and `nl_NL` need text-length QA for terminal columns, PDA cards, and scan popups.
- If a translated short string does not fit at 0.8 scale, rewrite the localized short form instead of widening gameplay UI.

## Maturity States

Source Authority:
`source_authority`. English authority text exists with stable Article ID, canon owner, spoiler level, source voice, unlock route, and surface intent.

Draft Machine Or LLM:
`draft_machine_or_llm`. Draft translation exists, but it is not native-approved and cannot be claimed final.

Blocked Translation Draft:
`BLOCKED_TRANSLATION_DRAFT`. Required locale row exists, but usable draft text is blocked and the blocker is named.

Fluent Reviewed:
`fluent_reviewed`. Fluent review passed, but native review and assigned-surface runtime proof may still be missing.

Native Reviewed:
`native_reviewed`. Native speaker reviewed meaning, voice, idiom, and forbidden fact drift. This is not runtime proof.

Runtime Ready:
`runtime_ready`. Native-reviewed text has been tested in the assigned UI, audio, site, or export surface with required proof artifacts. Lore markdown, locale JSON, and generated pages alone do not prove this state.

Publication Ready:
Not a localization status. Use only after `textes.md` public-copy review and the applicable proof gate for the exact external channel and claim.

## Current Application

All new lore work should now be written as content packets first:

- article metadata;
- canon summary;
- player-facing sections;
- source-voice sections;
- localization keys;
- delivery hooks;
- unresolved questions.

This keeps the same material useful for encyclopedia, future website, wiki, terminal documents, scanner records, and multi-language production.

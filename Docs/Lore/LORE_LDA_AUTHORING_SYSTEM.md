# H8-LDA: Lore Authoring Route

Status: **ROUTING DOC / STATIC_DOC**
Evidence class: **STATIC_DOC**

## What this file used to claim, and why it changed

This file declared itself `AUTHORING LAW` and required all lore generation, validation and export to run
through `h8_lda_pipeline.py`, producing `MANIFEST.json`, `15_LOCALE_ROWS.csv`, `TERMINOLOGY_LOCK_TABLE.csv`,
`ROUTE_CARDS.csv` and `QA_MATRIX.csv`. It instructed writers: "Run `python h8_lda_pipeline.py`... Do not
bypass the script."

None of those six artifacts exist. Checked by basename against the entire repository rather than the
expected directory: zero hits for any of them. The pipeline was never built, while the corpus was in fact
authored and exported by a different set of tools that do exist. A routing document pointing at absent
machinery is worse than no routing document, because it sends every writer to a dead end and makes the real
tools look unofficial.

The intent — mechanical anti-fluff enforcement across 15 locales — is now genuinely implemented. It lives
elsewhere, and this file's job is to say where.

Nothing was dropped in the reduction. Every rule this file carried was migrated to a live document first:
the English forbidden-phrase list was already present in `writing.md`; the Russian tone markers went to the
`ru_RU` rejection list in `localization.md`, which was missing `в мире, где`, `тонкий баланс` and
`по-своему прекрасен и ужасен`; and the per-locale register targets from the old section 3 — the Soviet-era
bureaucratic register for Deep Reach Russian, the industrial-bureaucratic compound nouns for German, the
RTL/CJK glyph and wrapping requirements — are now a `Locale register targets` block in `localization.md`.

## Where the enforcement actually is

| Job this file used to claim | What really does it |
|---|---|
| forbidden-phrase scan, English | `writing.md` Anti-AI Prose Ban and AI Phrase Family Quarantine, enforced by `Tools/ValidateAppliedLoreArtifactVoice.py` |
| forbidden-tone scan, per locale | `localization.md` Multilingual AI-Style Localization Firewall, per-locale rejection lists and register targets |
| terminology lock | `Docs/Lore/Canon_Locks.md` plus the stable-ID rules in `localization.md` |
| 15-locale row structure | the `localized` block in `Docs/Lore/AppliedContent/packets/*.json` |
| packet export to markdown | `Tools/AppliedLorePageExporter.py` |
| packet import to the runtime table | `Tools/AppliedLoreImporter.py` → `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv` |
| route cards | `Tools/AppliedLoreRouteCardExporter.py` |
| quality gate | `Docs/QUALITY_GATES.md` AppliedLore Content Gate |

`Tools/ValidateAppliedLoreArtifactVoice.py` covers the old phrase families mechanically through its
`at_its_core`, `essay_framing`, `museum_label` and `thesis_contrast` detectors, and adds three shape-level
checks a phrase list cannot express: a heading addressing the player, three or more consecutive sentences
sharing one clause shape, and a long body carrying no document ID, quantity or date anywhere in it.

## The one law worth keeping verbatim

Detection triages. It does not accept.

`writing.md` LLM Style Suppression Law is explicit that current AI-text detection is not reliable enough to
approve prose, and that acceptance is manual redline. The validator prints exactly that in its own output
header. A clean run means no known pattern matched; it does not mean the prose is good.

Two failure classes measured in this corpus were invisible to the validator while it reported zero findings
on the files carrying them: seventeen of twenty-five audio logs ending on the identical em-dash cut, and the
"leave one unexplained detail" instruction hardening into a schema field filled thirteen times, five of them
with the literal words "Nobody has explained". Both were found by reading. Plan for the reading.

## Authoring route

1. `writing.md` for prose law, `narrative.md` for evidence order, `localization.md` for locale rules.
2. `Docs/Lore/Lore_Bible.md` and `Docs/Lore/Canon_Locks.md` for canon facts. A canon claim needs a canon
   source; when the fact is missing the deliverable is the blocker, not an invention.
3. Write or edit the packet JSON under `Docs/Lore/AppliedContent/packets/`. A packet must declare
   `content_class` as `in_world_artifact` or `production_metadata`. The exporter is default-deny: an
   undeclared packet does not get a new page.
4. A new release set needs a manifest under `Docs/Lore/AppliedContent/release_sets/` listing its
   `packet_sources`, or the importer never sees the packets at all — `collect_packets` iterates manifests,
   not packet files. Authored content with no manifest is invisible to the whole pipeline.
5. `python -B Tools/ValidateAppliedLoreArtifactVoice.py --glob "<your files>"` and drive it to zero.
6. `python -B Tools/AppliedLorePageExporter.py` to publish; `python -B Tools/AppliedLoreImporter.py` to bake.
7. Read the result yourself. Step 5 cannot do step 7.

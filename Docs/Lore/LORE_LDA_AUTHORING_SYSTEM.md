# H8-LDA: Lore, Dialogue & Adaptability Authoring System

Status: **AUTHORING LAW**
Evidence class: **STATIC_DOC / SYSTEM_ARCHITECTURE**

This document defines the H8-LDA (Lore, Dialogue & Adaptability) system. It replaces scattered ad-hoc python generation scripts and provides a mathematically rigid pipeline that guarantees deep-sea noir tone, prevents "AI fluff", and scales to 15 locales with zero manual CSV layout errors.

## 1. The Core Philosophy
Lore in HECTON-8 is an artifact of a brutal, hyper-corporate, and procedural reality. It is not an essay, a poem, or a philosophical reflection. 

If a piece of text contains generic "AI fluff" (нейроговно), it is a fatal build error. H8-LDA enforces this mechanically.

## 2. The Anti-Fluff Tone Enforcer (QA Layer)
All text passing through the H8-LDA pipeline is strictly scanned against a forbidden phrase dictionary. If any packet contains these strings, the build **fails immediately**.

**Forbidden Phrases (English):**
- "serves as a reminder"
- "a testament to"
- "in a world where"
- "a delicate balance"
- "a unique blend"
- "this entry explores"
- "both beautiful and terrifying"
- "the real horror is"

**Forbidden Tone Markers (Russian):**
- "Это служит напоминанием"
- "является свидетельством"
- "уникальное сочетание"
- "в мире, где"
- "тонкий баланс"
- "по-своему прекрасен и ужасен"

*If an AI subagent or writer attempts to commit text containing these markers, the pipeline will reject the commit.*

## 3. The 15-Locale Translation Matrix
Localization is not just word-substitution. It requires structural mapping to the socio-linguistic constraints of the setting:

- **English (en_US):** The source authority. Tone must be clipped, procedural, or deeply evasive (Corporate Legal).
- **Russian (ru_RU):** Must adopt a cold, Soviet-era bureaucratic tone for Deep Reach ("Удержание активов", "Каскадный сбой"). Marauder text must use gritty industrial slang, avoiding direct translation of English idioms.
- **German (de_DE):** Deep Reach text relies heavily on massive compound nouns representing industrial bureaucracy.
- **RTL (ar_SA, he_IL) & CJK (ja_JP, ko_KR, zh_CN):** The pipeline auto-tags these locales for font-glyph and wrapping checks. They must be stringently tested for zero-allocation rendering in the HUD.

## 4. The Unified Pipeline (`h8_lda_pipeline.py`)
All lore generation, validation, and export to CSV must be executed via `h8_lda_pipeline.py`. 

**Pipeline Workflow:**
1. **Ingest**: Read markdown source files.
2. **Scan**: Run the Anti-Fluff Regex checks. Fail if dirty.
3. **Map**: Apply the Terminology Lock Table. Fail if a locked term is translated improperly.
4. **Generate**: Expand to the 15-locale grid. Inject `[LOCALE]` draft prefixes where native translations are unavailable.
5. **Export**: Write `MANIFEST.json`, `15_LOCALE_ROWS.csv`, `TERMINOLOGY_LOCK_TABLE.csv`, `ROUTE_CARDS.csv`, and `QA_MATRIX.csv`.

## 5. Usage for Agents and Authors
When a task requests a massive lore drop (e.g., "RS120 Mega Wave"):
1. Write the source material cleanly, adhering to Marauder or Deep Reach voices.
2. Run `python h8_lda_pipeline.py --source <folder> --out <folder>`.
3. If it fails the tone check, fix your writing. Do not bypass the script.
4. Commit the pipeline-generated CSVs to AppliedContent.

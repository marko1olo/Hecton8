# HECTON-8 Localization, Text Runtime, And Subtitle Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Scope: localization, subtitles, font atlases, RTL/CJK readiness, text expansion, zero-GC UI text, warnings, lore display, and localization proof gates.

## Prime Law

Text must survive pressure, localization, and runtime budgets.

HECTON-8 text appears on failing instruments, subtitles, warnings, terminals, logs, menus, evidence screens, and public-facing UI. It must be short, readable, localizable, and allocation-safe. English-only layout, tiny flavor labels, runtime string churn, and untested long text are rejected.

## Truth Ownership

Localization owns language resources, font coverage, string ids, plural/gender rules where needed, subtitle timing data, text expansion proof, and allocation-safe formatting routes. It does not own narrative truth, UI state, survival facts, mission logic, or public claims.

UI, narrative, accessibility, audio, and settings consume localized text by stable ids. They must not build gameplay truth through ad hoc strings.

## Runtime Text Law

Required:

- stable string ids;
- no runtime key construction in hot paths;
- preloaded font atlases for core UI/HUD/subtitles;
- allocation-free numeric/state formatting in HUD;
- char-buffer or `SetCharArray` style update route for hot readouts;
- explicit fallback language and missing-string display policy;
- subtitle speaker/source metadata;
- text category ownership: UI, warning, lore, subtitle, public copy, debug.

Forbidden:

- `TMP_Text.text = value` in hot HUD paths;
- string concatenation/formatting in gameplay update paths;
- English-only layout proof;
- hidden missing-key fallback that looks like valid text;
- all-caps paragraphs in emergency UI.

## Subtitle And Warning Rules

Subtitles and warnings are survival instruments:

- warnings are concise and state what changed;
- subtitles identify source when useful;
- critical alarms pair text with audio/icon/color/position;
- captions do not expose secret threat truth unless the game has a sensor/source reason;
- repeated alarms must throttle and prioritize.

## GlobalQualityWeight Scaling

`GlobalQualityWeight` may scale text animation, subtitle background material, scanline/glitch richness, optional speaker metadata, and debug overlay density. It must not change string ids, language selection, gameplay truth, warning priority, or accessibility availability.

Compact keeps readable text, high contrast, stable fonts, and no animation dependency. High tiers add richer screen material and transition polish only around the same text truth.

## Production Packet

Any localization, subtitle, warning text, font atlas, or runtime text change must declare:

- string id namespace and owner;
- supported language/fallback list;
- font atlas coverage and missing glyph behavior;
- expansion, CJK/RTL/fallback status if relevant;
- hot-path text update route;
- subtitle/warning priority and lifetime;
- Compact UI proof for long strings;
- profiler/GC proof when runtime text code changes.

Localized text that allocates per update, clips critical information, or changes gameplay meaning is rejected.

## Proof Artifacts

Localization work must provide:

- string id list or changed table;
- font atlas coverage note;
- long-string expansion capture;
- RTL/CJK/fallback note where applicable;
- zero-GC hot text proof if runtime UI changed;
- subtitle timing/source proof if audio/text changed;
- missing-key behavior proof;
- accessibility/readability capture.

## Rejection Gates

Reject:

- text layout tested only in English;
- runtime formatting allocations in HUD;
- tiny lore labels carrying critical facts;
- missing keys hidden as normal copy;
- warning copy that does not tell the player what to do or what failed;
- public/store text routed here instead of `textes.md`.

## Acceptance Sentence

Localization is accepted only when every text route uses stable ids, remains readable after expansion, supports fallback and accessibility, avoids hot-path allocation, and proves the language/font behavior it claims.

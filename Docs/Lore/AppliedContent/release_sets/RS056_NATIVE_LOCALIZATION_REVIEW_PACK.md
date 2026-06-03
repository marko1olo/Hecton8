# Native Localization Review Pack

Status: production-facing draft pending native localization.

Define release-review gates for Russian, CJK, RTL, European and subtitle/audio localization surfaces.

## Packets

- `P276_RU_NATIVE_REVIEW_LOCK` - Ru Native Review Lock: Russian Native Review Lock describes why HECTON-8 Russian localization must stay operational and evidence-led.
- `P277_CJK_REVIEW_LOCK` - Cjk Review Lock: CJK Review Lock explains the font and layout proof required before HECTON-8 CJK publication.
- `P278_RTL_REVIEW_LOCK` - Rtl Review Lock: RTL Review Lock describes the bidirectional layout proof required before HECTON-8 RTL publication.
- `P279_EUROPEAN_LANGUAGE_REVIEW_LOCK` - European Language Review Lock: European Language Review Lock explains text expansion proof for HECTON-8 contract and dossier UI.
- `P280_SUBTITLE_AUDIO_REVIEW_LOCK` - Subtitle Audio Review Lock: Subtitle Audio Review Lock explains how HECTON-8 keeps translated audio useful without turning it into chatter.

## Runtime Rule

Authoring/export source only. Runtime consumes baked static-data rows, packet hashes and string-pool offsets.
No runtime markdown parsing, JSON parsing, live translation or scene search.

# Rationale 2022

Agent ID: 2022
Date: 2026-06-04
Evidence class: STATIC_DOC / STATIC_SOURCE_REVIEW

## Decisions

1. Wet basalt stays in the queue at rank 1.
   - Reason: `TX_H8_WetBasaltShoreline_1429_MANIFEST.md` and the 2017 adversarial review statically reject current 1429 for broad terrain. Surface/coast terrain, triplanar rock, and wetness blockers remain open. A new prompt is justified, but no current basalt source is accepted.

2. Foam/contact mask ranks 2 instead of waiting for a full water simulation.
   - Reason: waterline and foam are visible first-route blockers, and `water.md` plus cinematic-cheat mandates prefer bounded visual masks over fluid simulation.

3. Photic seabed and shallow coral rank ahead of scanner/resource product-face textures.
   - Reason: `VISION_LOCKS.md`, `TASTE.md`, `world.md`, and Batch20 material debt make 0-100 m photic beauty/readability a first-route floor. Scanner/resource debt is real but lower immediate surface/photic impact.

4. Aegir cloud bands rank 5 and cloud deck ranks 6.
   - Reason: sky/Aegir refs are top blockers, but Aegir cloud-band source has stronger immediate identity impact than generic cloud coverage. Both remain source-only until route owner import/proof.

5. `GeminiTextureIntakeAudit.py` is required after download but not treated as production acceptance.
   - Reason: the script creates useful static metrics/previews, but `QA_Evidence_Text_Filter_Audit.txt`, `quality.md`, and the 2017 review forbid upgrading static QA into Unity/runtime proof.

6. `TextureSeamPeriodicRefiner.py` is diagnostic only.
   - Reason: the 2017 review shows exact edge pinning can fake seam metrics while inner bands remain worse than raw source.

7. Retry policy is strict.
   - Reason: daily image budget is limited. One retry per texture is the default; only ranks 1-3 can receive a second retry, and only for a clear failure reason.

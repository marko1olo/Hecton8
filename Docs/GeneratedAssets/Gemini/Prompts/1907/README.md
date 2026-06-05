# 1907 Terrain Coastline Gemini Prompt Pack

Evidence class: STATIC_DOC
Status: SOURCE REQUEST PACK ONLY

This is not an import manifest, not a Unity material manifest, not a texture acceptance report, and not visual proof. It is a prompt/source request packet for future source operators and PBR QA.

Every prompt must produce square tileable source, orthographic top-down material view, diffuse even light, no perspective, no horizon, no object render, no cast shadows, no text, no logo, no UI, no watermark, no baked directional highlight, no muddy/noir hiding, and surface/photic readability at or above the Subnautica-level floor.

## Rejection Checklist

Reject source outputs if:

- seams show in a 2x2 tile test;
- albedo contains baked shadows, cast highlights, text, logo, perspective, horizon, or object silhouette;
- material truth is false: glossy dirt, metallic rust, glowing sand, uniform wet plastic, or flat procedural noise;
- source is blurry, muddy, crayon-like, low-resolution mush, or generic grunge;
- foam reads as opaque paint rather than lace/contact breakup;
- basalt lacks fracture, pore, mineral, salt, or wet/dry history;
- black sand loses traversal readability or becomes a dark smear;
- caustic source has no physically motivated shallow-light role;
- reference-only Aegir/ocean panels are mistaken for imported texture families.

## Suitability Tags

- `ALBEDO_SOURCE`
- `HEIGHT_SOURCE`
- `ROUGHNESS_AO_DERIVATION`
- `MASK_SOURCE`
- `REFERENCE_ONLY`

See `prompts.csv` for the 12 source rows.

# Rationale 2009

Evidence class: STATIC_DOC / STATIC_SOURCE.

## Decisions

- Kept all work under 2009 docs/log outputs. Task forbids Unity and active `Assets` edits.
- Used root visual locks over older dark/noir interpretation. Surface, sky, Aegir, moons, coastline, ocean surface, and photic shallows are bright/readable by default.
- Wrote prompts as source-material requests, not final texture or screenshot prompts. This follows the texture playbook and prevents baked-light/object-render candidates.
- Declared channel contracts explicitly. Undefined packed channels are a reject condition.
- Treated Crest textures and Aegir source files as existing static source routes only. No visual acceptance claimed.
- Included ProductFace rows because ProductFace primitive/default material debt overlaps the requested source categories, but kept sky/ocean/celestial route-owned and not ProductFace donor material.
- Kept `GlobalQualityWeight` consequences continuous across low/middle/high/ultra instead of binary switches.

## Rejected Alternatives

- Rejected image-generation claims. No image generation tool was used and no new image files were created.
- Rejected albedo-only promotion for wet basalt, moon, coral, kelp, and ProductFace rows. Derivation path and QA are mandatory.
- Rejected storm/darkness as a default surface art route.
- Rejected direct prefab/material binding from generated candidates.
- Rejected packed-channel guessing from filenames such as ARM/MRAO/ORM without shader route discovery.

## Residual Risk

- Static docs cannot prove graphics, optimization, or gameplay acceptance.
- Runtime route may differ from static file scans.
- Future candidate sources may still fail seam, baked-light, histogram, mip, compression, or channel independence QA.

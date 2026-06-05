# Rationale 1858

Evidence class: STATIC_SOURCE

## Decisions

- Did not run `Tools/GeneratedAssetProductionAudit.py`: its default behavior writes 1851 JSON/Markdown outputs outside the 1858 owned write set. Used existing 1851 JSON/Markdown and read-only scans instead.
- Prioritized surface/shallow flora, reef carriers, and geology route anchors ahead of alphabetical order because `VISION_LOCKS.md`, `TASTE.md`, `world.md`, `terrain.md`, and `3dmodel.md` make photic/surface beauty and route readability product-critical.
- Treated manifests as debt-clearing for package metadata only. Manifests cannot clear `SURFACE_SHALLOW_VISUAL_PROOF_PENDING`; that requires render/screenshot proof.
- Treated BioForge shallow mesh roots as source/library candidates even when matching prefabs exist elsewhere, because the 1851 audit explicitly marks that family `SOURCE_ONLY_PACKAGE`.
- Kept all proof requirements static-executable for future agents: manifest naming, named proof naming, screenshot requirements, and no-Unity static checks.

## Residual Risk

- Static scans cannot prove mesh silhouette, PBR quality, texture import correctness inside Unity, LOD switching behavior, collider correctness, SRP batching, GPU Resident Drawer behavior, or compact-tier readability.
- Existing audit counts can drift if another agent edits assets or reruns the audit after this packet.

# SHINOBU_149 Rationale

## Decision 01 - Replace object decals with Vault ring

Problem: `SubmarineStructuralGrid` still references `DecalProjector` prefabs and ObjectPool spawn/despawn for hull impact scratches. That path creates component traffic and keeps URP decal renderer dependency alive.
Solution: Replace hit-time projector spawn with a presentation-only native request into a Vault-backed 80B decal ring. Fullscreen screen-space pass consumes matrices.
Rejected Alternatives: Pooling `DecalProjector` is still GameObject/component state; drawing impact quads still creates mesh/overdraw ownership and cannot batch all decals through one pass.
Scalability potential: Low=128 newest decals with faster decay; Middle=384; High=768; Ultra=1024 plus slower decay and full normal/depth projection.
Hardware Impact: MX350/i3 avoids projector component updates and renderer feature object traversal; estimated 300-1400 us saved during clustered impacts, with GPU loop capped by continuous `GlobalQualityWeight`.

## Decision 02 - Use existing normals, no physics re-query

Problem: Reconstructing decal orientation by `Physics.Raycast` at impact time would stall the main thread and duplicate combat/ballistics work.
Solution: Use ballistics `BallisticHitResultDTO.Normal` and submarine collision/contact normals. AUP position is localized in Burst by subtracting camera AUP before float downcast.
Rejected Alternatives: `Physics.Raycast`, `RaycastNonAlloc`, or mesh normal lookup during visual sync. Standard Unity surface lookup is not deterministic enough and spends CPU for presentation-only state.
Scalability potential: Low uses dominant-axis fallback if normal is invalid; Middle/High/Ultra use normalized contact normal and deterministic roll.
Hardware Impact: Avoids 40-250 us per clustered hit on low-end silicon; preserves shader budget for visible scorch density.

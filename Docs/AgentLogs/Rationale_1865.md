# Rationale 1865

Evidence class: STATIC_SOURCE

## Decisions

1. `ocean.md` was requested but missing. Used `water.md` because `PROJECT_BIBLES.md` names it as the standing water/ocean authority.

2. `Sky_System.prefab` is not accepted even though `02_HECTON_WORLD` overrides the scene instance to `SkyDome_Inverted.asset`. Source prefab still has an enabled built-in sphere renderer. Scene override lowers immediate scene risk, not prefab-source risk.

3. `Ocean_Crest.prefab` is not accepted even though `02_HECTON_WORLD` disables the three Sargassum input MeshRenderers. Source prefab still has enabled primitive plane renderers, and `SargassumMicroFaunaBoids.boidMesh` still points at a built-in primitive plane.

4. Hidden/input-only interpretation is marked `PENDING RUNTIME PROOF`, not accepted. Crest `_disableRenderer: 1` and scene `m_Enabled=0` are static intent, not frame-zero proof.

5. Visual proof plan rejects darkness/storm/fog excuses. Normal surface, shore, photic shallow, Aegir/moon, waterline, foam, refraction, and micro-fauna captures are mandatory before acceptance.

## Scaling Consequence

Compact must preserve bright/readable ocean, sky, Aegir, moons, waterline, and shore cues. Middle adds water/shore/cloud richness. High adds stronger reflection, atmosphere, and micro-fauna density. Ultra adds sensory overkill only; it cannot own navigation truth or basic readability.


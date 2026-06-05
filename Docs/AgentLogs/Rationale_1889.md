# Rationale 1889

Date: 2026-06-04
Evidence class: STATIC_DOC / STATIC_SOURCE

## Decision

Environment route assets are visual references, not product-face source material.

## Reason

Sky, Aegir, moons, ocean, waterline, photic shallows, coastline, terrain, flora, sargassum, storm, fog, and depth materials are pillar or route-owned systems. Direct product-face reuse would steal identity from the route owner, bypass PBR/channel/import proof, and allow weak product-face work to be hidden behind environment art.

## Boundary

Allowed:

- static visual reference;
- category-specific derivative authoring with owner approval;
- project-owned albedo/normal/packed-mask outputs;
- declared shader channel layout and import manifest;
- proof state label.

Forbidden:

- cloning or mutating Crest package assets;
- direct relink of environment materials/textures into tools, pickups, vehicles, or player suit;
- using noir, fog, storm, dirty foam, or depth materials to conceal weak normal-surface or product-face art;
- promoting proof swatches or hidden input masks into production materials.

## Residual Risk

No validator was implemented because task is report-only. Future relink agents can still violate this boundary unless their manifests and validators enforce it.

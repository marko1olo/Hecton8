# Foam Contact Source Role Decision Queue - 2026-06-05

Status: `PENDING_VERIFICATION / STATIC_IMAGE_QA_ONLY`.
Evidence class: `STATIC_SOURCE + STATIC_IMAGE_QA`.
Unity import: absent.
Material binding: absent.
Visual acceptance: absent.
Runtime proof: absent.

CSV companion: `Docs/AssetAudit/FOAM_CONTACT_SOURCE_ROLE_DECISION_QUEUE_20260605.csv`.

## Scope

This queue classifies existing water/foam/contact source artifacts before any future water, Crest, shoreline, or terrain material owner imports or binds them.

Reviewed static image artifacts:

- `Docs/AssetAudit/ContactSheets/water_foam_caustic_contact_sheet.png`
- `Docs/GeneratedAssets/AssetSystem_20260605/FoamContactPrototype_20260605/`
- `Docs/GeneratedAssets/AssetSystem_20260605/CleanupPass_20260605/FoamContact_CleanupPass_ContactSheet_SOURCE_ONLY_20260605.png`
- `Docs/AssetAudit/SOURCE_PROTOTYPE_REVIEW_20260605.md`
- `Docs/AssetAudit/SOURCE_PROTOTYPE_CLEANUP_REVIEW_20260605.md`
- `taskslocal/asset_system_20260605/ASSET_OWNER_11_WATER_FOAM_CONTACT_AUTHORING_PACKET.md`
- `taskslocal/asset_system_20260605/ASSET_OWNER_20_OCEAN_CREST_CONTACT_PROOF_PACKET.md`

## Decision

- The old turquoise `foam.png` remains rejected as final visible waterline or shoreline art.
- Cleanup albedo and normal are useful source direction only. They need role-correct map authoring, tile proof, import proof, material readback, and route screenshots.
- Cleanup MRAO/RGBA/contact channels remain too harsh, false-color, broad, or blocky for direct material binding.
- Visor droplet and `unnormal.png` assets visible in the water/foam contact sheet are not water-contact material candidates without a separate owner route.

## Required Future Outputs

- role-correct albedo, normal/detail, packed mask, and RGBA contact mask maps;
- 2x2 and 4x4 tile sheets;
- per-channel grayscale debug sheet, not false-color-only previews;
- import-role row match from `TEXTURE_IMPORT_ROLE_MATRIX_20260605.csv`;
- Crest/ocean slot readback and route screenshots after a clean Unity gate;
- mandatory visual-reference comparison against bright surface, shoreline, and photic shallow rows.

## Low / Middle / High / Ultra Consequences

- Low/compact: use fewer proven contact layers, not rejected flat foam or broad false-color masks.
- Middle: admit route-owned contact maps only after slot, import, tile, and screenshot proof.
- High: spend budget on wet-edge detail, micro-bubble breakup, and shoreline response after proof.
- Ultra: layered contact response is allowed only after material route, memory, Frame Debugger, and visual-reference proof are stable.

## Regression Model

- CPU: static image/source classification only; no runtime CPU change.
- GPU: no shader, material, sampler, or render pass changed.
- GC: no runtime code touched.
- Memory/VRAM: no texture imported or resident.
- Correctness: queue blocks direct promotion of rejected or source-only foam/contact images.
- Visual: no final waterline result is claimed. Surface/ocean/shoreline remains blocked until Unity screenshots pass mandatory references.

Final status: `PENDING_VERIFICATION`.

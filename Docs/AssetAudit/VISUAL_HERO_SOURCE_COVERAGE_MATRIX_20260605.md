# Visual Hero Source Coverage Matrix - 2026-06-05

Status: `PENDING_VERIFICATION / STATIC_IMAGE_QA_ONLY`.
Evidence class: `STATIC_IMAGE_QA + STATIC_SOURCE + STATIC_DOC`.
Unity readback: absent.
Runtime visual proof: absent.
Asset mutation: none.

CSV companion: `Docs/AssetAudit/VISUAL_HERO_SOURCE_COVERAGE_MATRIX_20260605.csv`.

## Scope

This matrix links the mandatory visual reference set to current texture/source-pack coverage for first surface exit, shoreline contact, photic shallows, kelp/flora density, cockpit/HUD readability, deep bioluminescent routes, and medium-depth hero routes.

Contact sheets manually inspected in this pass:

- `Docs/AssetAudit/ContactSheets/mandatory_visual_references_current_20260605.png`
- `Docs/GeneratedAssets/Batch31_LocalPBR/Batch31_LocalPBR_contact_sheet.png`
- `Docs/GeneratedAssets/AssetSystem_20260605/CleanupPass_20260605/FoamContact_CleanupPass_ContactSheet_SOURCE_ONLY_20260605.png`
- `Docs/GeneratedAssets/AssetSystem_20260605/CleanupPass_20260605/AegirCloud_CleanupPass_ContactSheet_SOURCE_ONLY_20260605.png`
- `Docs/AssetAudit/ContactSheets/flora_coral_fauna_contact_sheet.png`
- `Docs/AssetAudit/ContactSheets/ui_textures_contact_sheet.png`
- `Docs/AssetAudit/ContactSheets/terrain_geology_contact_sheet.png`

## Static Findings

- Mandatory references require bright surface, readable Aegir/clouds, real waterline contact, photic terrain clarity, dense organic silhouettes, and cockpit/HUD readability.
- Batch31 terrain sources provide usable albedo/normal direction, but MRAO/channel semantics and route material proof remain blocked.
- Foam cleanup albedo/normal sources are better than the rejected old foam sheet; cleanup MRAO/RGBA masks remain broad/blocky and source-only.
- Aegir cleanup band/detail sources improve direction; storm masks remain source-only and not sky-slot proof.
- Flora/coral source stacks have candidate material identity, but `WorldProceduralProxy` contamination and import/mip proof block promotion.
- UI source icons include detailed `OXYGEN.png`; `oxygen-tank.png` remains a black mask/silhouette risk without atlas/binding proof.

## Low / Middle / High / Ultra Consequences

- Low/compact: preserve bright water/sky readability, material identity, silhouette hierarchy, and HUD clarity with compressed role-correct maps only.
- Middle: admit route-owned PBR stacks and source packs only after material slot, import role, and screenshot proof.
- High: spend saved budget on detail normals, wet contact masks, longer LOD/mip residency, and denser near-field dressing after proof.
- Ultra: use layered Aegir/cloud, shoreline breakup, flora density, and cockpit material detail only after route, memory, and Frame Debugger proof.

## Regression Model

- CPU: static image QA only. Future work risks shader/material churn and overdraw.
- GC: static docs only. Future runtime material/UI systems need 0 B/frame proof.
- Memory/VRAM: source packs are not resident proof. Future import must respect texture budgets, streaming mips, and Addressables ownership.
- Cadence: no runtime cadence changed. Future visual density must scale through continuous `GlobalQualityWeight`.
- Correctness: this matrix narrows source fit and gaps only. It is not visual acceptance.

Final status: `PENDING_VERIFICATION`.

# Material File Technical Properties - 2026-06-05

Status: `PENDING VERIFICATION`.
Evidence class: `STATIC_YAML_SCAN` + `STATIC_DOC`.
Scope: `.mat` files under `Assets/_Project`.

This file is not Unity material readback, shader validity proof, material binding proof, SRP Batcher proof, visual acceptance, or runtime proof. It is a static serialized-material route map only.

CSV companion: `Docs/AssetAudit/MATERIAL_FILE_TECHNICAL_PROPERTIES_20260605.csv`.

## Summary

- Material files scanned: `392`.
- Materials with no static texture GUIDs: `290`.
- Materials with empty texture slot tokens: `314`.
- Materials with `WorldProceduralProxy` token: `41`.
- Materials with proxy/placeholder tokens: `42`.
- Materials with Crest route tokens: `3`.
- Materials with unresolved shader GUID by static map: `260`.
- Materials with custom render queue token: `52`.

## Required Future Unity Gates

- Read back shader object, shader keywords, render queue, texture slots, and material users through Unity APIs.
- Confirm SRP Batcher compatibility and reject per-object material clones for standard geometry.
- For Crest/ocean rows, assign asset materials only; no runtime wrapper or material clone route.
- For `WorldProceduralProxy` and placeholder rows, reject visible-route promotion until final route-owned materials exist.
- Capture route screenshots, Frame Debugger, Stats, memory, and Console only after scoped Unity readback.

## Rollback Conditions

- Shader/material readback points to unresolved slots, unauthorized clone materials, broken shader variants, or wrong texture channel contracts.
- Proxy or placeholder material remains in visible surface, photic shallows, first-exit, or medium-depth route.
- Material slot changes increase SetPass/material uniqueness without proof.
- Screenshot evidence is flat, muddy, blurry, primitive, or hidden by darkness/fog/post.

## Continuous GlobalQualityWeight Consequences

- Low/compact: preserve material identity, baked AO/channel packing, silhouette readability, and water/sky route clarity; reduce residency and density smoothly only after readback proof.
- Middle: keep stable route-owned PBR stacks and dithered LOD/material transitions.
- High: spend saved cost on richer detail normals, wetness/contact response, sky/cloud detail, and organic material breakup.
- Ultra: extend material layering, reflection/lighting response, and near-field dressing after measured proof. Gameplay truth and ownership route do not change.

## Regression Model

- CPU: static scan only; future risk is material count, shader variants, renderer state changes, and SetPass growth.
- GC: no runtime code touched; no allocation claim.
- Memory/VRAM: texture slots and source refs are static tokens; resident memory is unproven.
- Cadence: no runtime cadence changed.
- Correctness: static material tokens reduce owner guessing only. Unity binding and visual floor remain `PENDING VERIFICATION`.

Final status: `PENDING VERIFICATION`.

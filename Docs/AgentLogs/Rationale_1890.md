# Rationale 1890

## Decision

Implemented a strict read-only editor validator instead of a repair/relink tool.

## Reason

The batch route needs a pre-relink gate. Current static evidence shows product-face material debt: unresolved/default GUID, package `Lit.mat`, tool placeholders, player blockout, flat resource materials, and environment/noir/deep materials that must not be reused as generic product-face body materials.

## Boundaries

- No Unity execution.
- No build.
- No import.
- No prefab/material/texture/asset mutation.
- No scene access or mutation.
- No package/material cloning.
- No runtime claims.

## Validator Truth

The validator treats missing albedo, normal, packed mask, and packed-channel declarations as source failure when a role target requires them. It treats placeholder, package/default, diagnostics, blockout, and out-of-scope environment routes as hard failures.

## Proof State

STATIC_SOURCE only. Compile/import/menu behavior remains PENDING UNITY PROOF.

## Orchestrator Follow-Up: Current Assets Must Drive The Gate

The live validator now fails current prefab YAML/default-material asset debt, but only warns on historical Batch18 markdown report mentions. Reports preserve evidence and may legitimately mention old contamination after assets are fixed. The Unity gate must judge current asset state.

## Orchestrator Follow-Up: Exact MRAO Property Casing

`Hecton_MraoAtlasLit` uses `_MraoMap`. The validator now checks `_MraoMap` in addition to `_MRAOMap`, so valid material candidates are not falsely rejected by casing mismatch.

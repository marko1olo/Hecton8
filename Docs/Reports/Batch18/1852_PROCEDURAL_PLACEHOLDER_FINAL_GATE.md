# 1852 Procedural Placeholder Final Gate

Date: 2026-06-04
Evidence class: STATIC_SOURCE
Unity compile: PENDING VERIFICATION
Runtime proof: PENDING VERIFICATION

## Scope

Tightened procedural family final validators so dev placeholder prefabs and Unity built-in primitive meshes cannot pass as production `finalReady && !proxyOnly` content.

## Changed

- `Assets/_Project/Scripts/Editor/WorldProceduralFamilyContractValidator.cs`
  - final-ready placeholder variants are now errors;
  - final-ready variants using Unity built-in primitive mesh ids are now errors;
  - placeholder-only families are now errors.
- `Assets/_Project/Scripts/Editor/WorldProceduralFinalPrefabQualityGate.cs`
  - shared static prefab gate for detecting Unity built-in primitive mesh ids in final prefabs;
  - shared authoring stop switch for legacy primitive-composite production final rebuild menus.
- `Tools/GeneratedAssetProductionAudit.py`
  - procedural family link scan rejects final-ready/non-proxy variants pointing at placeholder or Unity primitive prefabs;
  - direct production `Final` prefab-root scan rejects unlinked primitive finals so they cannot be hidden for later relinking.
- `Assets/_Project/Scripts/Editor/WorldProceduralFinalVariantAuthoring.cs`
  - first-wave final variant linking now rejects prefabs that use Unity built-in primitive mesh ids.
- `Assets/_Project/Scripts/Editor/WorldProceduralSupportFinalAuthoring.cs`
  - legacy primitive-composite world-support final rebuild menu is blocked.
- `Assets/_Project/Scripts/Editor/WorldProceduralOrganicMiscFinalAuthoring.cs`
  - legacy primitive-composite organic misc final rebuild menu is blocked.
- `Assets/_Project/Scripts/Editor/ConstructionBootstrapAuthoring.cs`
  - legacy starter construction rebuild is blocked from rewriting production construction finals from Unity primitives.
- `Assets/_Project/Scripts/Editor/WorldProceduralSupportFinalValidator.cs`
  - placeholder-only support finals are now errors;
  - final-ready support variants pointing at placeholder prefabs are now errors;
  - final-ready support variants using Unity built-in primitive mesh ids are now errors.
- `Assets/_Project/Scripts/Editor/WorldProceduralGeologyFinalValidator.cs`
  - placeholder-only geology finals are now errors;
  - final-ready geology variants pointing at placeholder prefabs are now errors;
  - final-ready geology variants using Unity built-in primitive mesh ids are now errors.
- `Assets/_Project/Scripts/Editor/WorldProceduralOrganicMiscFinalValidator.cs`
  - placeholder-only organic misc finals are now errors;
  - final-ready organic misc variants pointing at placeholder prefabs are now errors;
  - final-ready organic misc variants using Unity built-in primitive mesh ids are now errors.
- `Assets/_Project/Scripts/Editor/WorldProceduralStructuralFinalValidator.cs`
  - placeholder-only structural finals are now errors;
  - final-ready structural variants pointing at placeholder prefabs are now errors;
  - final-ready structural variants using Unity built-in primitive mesh ids are now errors.

## Rationale

`WorldProceduralPlaceholderAuthoring` can still create temporary placeholder final variants for authoring continuity, but final validation must reject them as production content. Legacy authoring menus that rebuild production finals out of Unity primitives are now blocked so future agent passes cannot silently recreate the same weak assets. A cube/primitive placeholder or a prefab built from Unity primitive mesh ids cannot satisfy the surface, shallow, geology, flora, support, or structural visual floor.

## Verification

- `git diff --check -- Assets/_Project/Scripts/Editor/WorldProceduralFamilyContractValidator.cs Assets/_Project/Scripts/Editor/WorldProceduralFinalPrefabQualityGate.cs Assets/_Project/Scripts/Editor/WorldProceduralSupportFinalValidator.cs Assets/_Project/Scripts/Editor/WorldProceduralGeologyFinalValidator.cs Assets/_Project/Scripts/Editor/WorldProceduralOrganicMiscFinalValidator.cs Assets/_Project/Scripts/Editor/WorldProceduralStructuralFinalValidator.cs`
  - Result: only CRLF working-copy warnings.
- `git diff --check -- Assets/_Project/Scripts/Editor/WorldProceduralSupportFinalAuthoring.cs Assets/_Project/Scripts/Editor/WorldProceduralOrganicMiscFinalAuthoring.cs Assets/_Project/Scripts/Editor/ConstructionBootstrapAuthoring.cs Assets/_Project/Scripts/Editor/WorldProceduralFinalVariantAuthoring.cs`
  - Result: only CRLF working-copy warnings.
- Static brace count checked on all six edited validator/gate files.
  - Result: balanced braces in each file.
- Static brace count checked on all four edited authoring files.
  - Result: balanced braces in each file.
- `python -m py_compile Tools/GeneratedAssetProductionAudit.py`
  - Result: OK.
- `python Tools/GeneratedAssetProductionAudit.py --root .`
  - Result: `generated_asset_packages=392 fatal=0 error=41 warn=1281`.
  - The 41 current errors are 20 final-ready family links using Unity built-in primitive meshes plus 21 direct production `Final` prefabs using Unity built-in primitive meshes.
  - `PFB_SargassumCollapseChunk.prefab` is additionally caught by direct root scan even though it is not currently a flagged family link.
- `python Tools/GeneratedAssetProductionAudit.py --root . --fail-on-error`
  - Result: expected exit code `3` while the 41 production errors remain.
  - Use this opt-in mode for CI/controller gating when production `ERROR` findings must fail the pass.

## Residual Risk

- Unity Editor compile and menu validator runs are pending because Unity and UnityShaderCompiler processes are active.
- This change may intentionally turn existing placeholder-only or primitive-final families red. That is desired: those families need real authored/generated final prefabs or must remain dev-only/proxy-only.
- Existing primitive final assets are not deleted or rewritten in this pass. The next content pass must replace them with production meshes/material packages, then rerun the audit.
- Replacement plan is recorded in `Docs/Reports/Batch18/1853_PRIMITIVE_FINAL_REPLACEMENT_PLAN.md`.

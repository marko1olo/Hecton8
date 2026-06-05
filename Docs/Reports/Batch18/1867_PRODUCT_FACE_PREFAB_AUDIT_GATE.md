# 1867 Product-Face Prefab Audit Gate

Date: 2026-06-04
Owner: local orchestrator
Evidence class: STATIC_SOURCE / STATIC_AUDIT
Unity/build/runtime: NOT RUN

## Scope

This pass records the new product-face primitive prefab gate added to `Tools/GeneratedAssetProductionAudit.py` and the regenerated audit artifacts:

- `Docs/Reports/Batch18/1851_GENERATED_ASSET_PRODUCTION_AUDIT.json`
- `Docs/Reports/Batch18/1851_GENERATED_ASSET_PRODUCTION_AUDIT.md`

No prefab, scene, `.asset`, `.meta`, import, bake, Unity menu, PlayMode, screenshot, profiler, or build action was run.

## Gate Purpose

The previous generated-asset audit caught procedural family/final prefab debt, but product-facing blockout prefabs could still sit outside those roots.

The audit now scans first-minute/product-face prefab classes:

- `Assets/_Project/Prefabs/Player.prefab`
- `Assets/_Project/Prefabs/Sky_System.prefab`
- `Assets/_Project/Prefabs/Ocean_Crest.prefab`
- `Assets/_Project/Prefabs/Tools/Held/*.prefab`
- `Assets/_Project/Prefabs/Items/Tools/*.prefab`
- `Assets/_Project/Prefabs/Resources/Pickups/*.prefab`
- `Assets/_Project/Prefabs/Transport/*.prefab`
- `Assets/_Project/Prefabs/Item_Titanium.prefab`
- `Assets/_Project/Prefabs/STRUCTURES.prefab`
- `Assets/_Project/Prefabs/Buildings/Cube.prefab`

Any scanned product-face prefab that uses Unity built-in primitive mesh ids now emits:

`PRODUCT_FACE_PREFAB_BUILTIN_PRIMITIVE_MESH`

This does not make the visuals better by itself. It prevents product-facing primitive art from being hidden by folder boundaries.

## Audit Result

Command:

```powershell
python Tools/GeneratedAssetProductionAudit.py --root .
```

Result:

```text
generated_asset_packages=434 fatal=0 error=83 warn=1281
```

Summary from regenerated audit:

- `product_face_prefabs`: packages=42, error=42
- `final_prefab_roots`: packages=21, error=21
- `procedural_family_links`: packages=33, error=20, warn=18
- `baked_flora_prefabs`: packages=89, warn=267
- `bioforge_shallow_source_meshes`: packages=200, warn=800
- `world_procedural_geology_meshes`: packages=49, warn=196

Issue-code distribution:

- `PRODUCT_FACE_PREFAB_BUILTIN_PRIMITIVE_MESH`: 42
- `FINAL_PREFAB_BUILTIN_PRIMITIVE_MESH`: 21
- `FAMILY_FINAL_READY_BUILTIN_PRIMITIVE_MESH`: 20
- `FAMILY_NO_REAL_FINAL_LINKS`: 18
- `MISSING_MANIFEST`: 338
- `MISSING_NAMED_PROOF`: 338
- `SURFACE_SHALLOW_VISUAL_PROOF_PENDING`: 338
- `SOURCE_ONLY_PACKAGE`: 249

## Product-Face Blocker Set

The 42 product-face primitive errors are intentionally broad. These are player-facing surfaces, held items, pickups, vehicles, or visual systems; they cannot be waved away as internal procedural proxy debt.

High exposure:

- `Assets/_Project/Prefabs/Player.prefab`
- `Assets/_Project/Prefabs/Sky_System.prefab`
- `Assets/_Project/Prefabs/Ocean_Crest.prefab`

Held tools:

- `Assets/_Project/Prefabs/Tools/Held/Tool_BeaconDeployer_Held.prefab`
- `Assets/_Project/Prefabs/Tools/Held/Tool_Builder_Held.prefab`
- `Assets/_Project/Prefabs/Tools/Held/Tool_EnvAnalyzer_Held.prefab`
- `Assets/_Project/Prefabs/Tools/Held/Tool_Flashlight_Held.prefab`
- `Assets/_Project/Prefabs/Tools/Held/Tool_HarpoonLauncher_Held.prefab`
- `Assets/_Project/Prefabs/Tools/Held/Tool_Knife_Held.prefab`
- `Assets/_Project/Prefabs/Tools/Held/Tool_LaserCutter_Held.prefab`
- `Assets/_Project/Prefabs/Tools/Held/Tool_Propulsion_Held.prefab`
- `Assets/_Project/Prefabs/Tools/Held/Tool_Repair_Held.prefab`
- `Assets/_Project/Prefabs/Tools/Held/Tool_SalvageSampler_Held.prefab`
- `Assets/_Project/Prefabs/Tools/Held/Tool_Scanner_Held.prefab`
- `Assets/_Project/Prefabs/Tools/Held/Tool_StunPistol_Held.prefab`

World tool pickups:

- `Assets/_Project/Prefabs/Items/Tools/Item_Tool_BeaconDeployer_World.prefab`
- `Assets/_Project/Prefabs/Items/Tools/Item_Tool_Builder_World.prefab`
- `Assets/_Project/Prefabs/Items/Tools/Item_Tool_EnvAnalyzer_World.prefab`
- `Assets/_Project/Prefabs/Items/Tools/Item_Tool_Flashlight_World.prefab`
- `Assets/_Project/Prefabs/Items/Tools/Item_Tool_HarpoonLauncher_World.prefab`
- `Assets/_Project/Prefabs/Items/Tools/Item_Tool_Knife_World.prefab`
- `Assets/_Project/Prefabs/Items/Tools/Item_Tool_LaserCutter_World.prefab`
- `Assets/_Project/Prefabs/Items/Tools/Item_Tool_Propulsion_World.prefab`
- `Assets/_Project/Prefabs/Items/Tools/Item_Tool_Repair_World.prefab`
- `Assets/_Project/Prefabs/Items/Tools/Item_Tool_SalvageSampler_World.prefab`
- `Assets/_Project/Prefabs/Items/Tools/Item_Tool_Scanner_World.prefab`
- `Assets/_Project/Prefabs/Items/Tools/Item_Tool_StunPistol_World.prefab`

Resource pickups:

- `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_CopperOre.prefab`
- `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_FiberKelp.prefab`
- `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_HydrocarbonResin.prefab`
- `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_MembraneTissue.prefab`
- `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_SilicaShards.prefab`
- `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_SilverOre.prefab`
- `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_SulfurClumps.prefab`
- `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_TitaniumScrap.prefab`

Transport:

- `Assets/_Project/Prefabs/Transport/PFB_CargoSled_Transport.prefab`
- `Assets/_Project/Prefabs/Transport/PFB_Exosuit_Frame_Transport.prefab`
- `Assets/_Project/Prefabs/Transport/PFB_MicroSub_Transport.prefab`
- `Assets/_Project/Prefabs/Transport/PFB_ScoutGlider_Transport.prefab`

Loose or legacy-facing prefabs:

- `Assets/_Project/Prefabs/Item_Titanium.prefab`
- `Assets/_Project/Prefabs/STRUCTURES.prefab`
- `Assets/_Project/Prefabs/Buildings/Cube.prefab`

## Verification

Claim: the audit now reports product-face primitive prefab errors.
Evidence class: STATIC_AUDIT.
Command:

```powershell
Select-String -Path Docs/Reports/Batch18/1851_GENERATED_ASSET_PRODUCTION_AUDIT.md -Pattern PRODUCT_FACE_PREFAB_BUILTIN_PRIMITIVE_MESH
```

Result: the regenerated audit lists issue-code count `42` and per-prefab error rows.

Claim: the audit script compiles as Python.
Evidence class: STATIC_SOURCE.
Command:

```powershell
python -m py_compile Tools/GeneratedAssetProductionAudit.py
```

Result: OK.

Claim: `--fail-on-error` fails while the current 83 errors remain.
Evidence class: STATIC_AUDIT.
Command:

```powershell
python Tools/GeneratedAssetProductionAudit.py --root . --fail-on-error; $code = $LASTEXITCODE; Write-Output "AUDIT_EXIT=$code"; exit 0
```

Result:

```text
generated_asset_packages=434 fatal=0 error=83 warn=1281
AUDIT_EXIT=3
```

Do not treat this as a broken audit; it is the audit doing its job.

## Acceptance Boundary

This gate proves only text/YAML/static-audit debt. It does not prove:

- actual player GameView composition;
- scene instance override safety;
- mesh import validity;
- material or texture quality;
- LOD/HLOD quality;
- collision proxy correctness;
- screenshot visual floor;
- frame time, GC, or profiler behavior.

Product-face prefab debt is closed only when each replacement has:

- non-primitive production mesh references or hidden-input proof;
- material/texture role proof;
- collider/proxy split proof where interactive;
- LOD/HLOD proof where visible beyond close range;
- scene/player screenshot or render proof against `TASTE.md`;
- runtime/profiler proof if behavior or hot-path rendering changes.

## Next Work

Use `Docs/Reports/Batch18/1864_PRODUCT_FACE_REPLACEMENT_MATRIX.csv` for player/tools/resources/transport replacement ordering.

Use `Docs/Reports/Batch18/1865_SKY_OCEAN_PRIMITIVE_RISK_PROOF_PACKET.md` for sky/ocean runtime proof ordering.

Highest next source/asset work:

1. Held tool and world tool visual source package.
2. Resource pickup source package and material identity pass.
3. Player suit/body visual replacement path.
4. Transport body source package.
5. Sky/Ocean active scene proof and source prefab cleanup once Unity slot is safe.

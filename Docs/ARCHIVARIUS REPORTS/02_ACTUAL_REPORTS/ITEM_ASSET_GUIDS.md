# HECTON-8 â€” ITEM ASSET GUID AUDIT
Date: 2026-05-07
Status: PENDING VERIFICATION

**Status:** ETA SURGERY_PREPPED  
**Purpose:** Hardcoded fallback table for Agent Gamma Addressables pre-warm queue.  
**Scope:** `Assets/_Project/Prefabs/Items/Tools/*.prefab`  
**Date:** 2026-04-28

---

## GUID TABLE â€” Dropped-Item World Prefabs

| Prefab Name | GUID | Asset Path |
|---|---|---|
| `Item_Tool_BeaconDeployer_World` | `d174d546f879a4742bc018eb043e67b7` | `Assets/_Project/Prefabs/Items/Tools/Item_Tool_BeaconDeployer_World.prefab` |
| `Item_Tool_Builder_World` | `a9d920f69f572794da38a80172350742` | `Assets/_Project/Prefabs/Items/Tools/Item_Tool_Builder_World.prefab` |
| `Item_Tool_EnvAnalyzer_World` | `f31fbadc22133c74a9c4e0dafbec547e` | `Assets/_Project/Prefabs/Items/Tools/Item_Tool_EnvAnalyzer_World.prefab` |
| `Item_Tool_Flashlight_World` | `40a67b632626b2b4ca1b22462448c725` | `Assets/_Project/Prefabs/Items/Tools/Item_Tool_Flashlight_World.prefab` |
| `Item_Tool_HarpoonLauncher_World` | `2f2aaf08a7039d74ab54a9f41530b73c` | `Assets/_Project/Prefabs/Items/Tools/Item_Tool_HarpoonLauncher_World.prefab` |
| `Item_Tool_Knife_World` | `774f5752cc67c7f49916466b60350a64` | `Assets/_Project/Prefabs/Items/Tools/Item_Tool_Knife_World.prefab` |
| `Item_Tool_LaserCutter_World` | `5d6d90d471f7ea44291faf2907d11145` | `Assets/_Project/Prefabs/Items/Tools/Item_Tool_LaserCutter_World.prefab` |
| `Item_Tool_Propulsion_World` | `f9ee01257418ed74696850470ef62d20` | `Assets/_Project/Prefabs/Items/Tools/Item_Tool_Propulsion_World.prefab` |
| `Item_Tool_Repair_World` | `fd6fc0a78e6568b4e972561e8b888d34` | `Assets/_Project/Prefabs/Items/Tools/Item_Tool_Repair_World.prefab` |
| `Item_Tool_SalvageSampler_World` | `fa20e563eef211a4daf00fe5b0ca6412` | `Assets/_Project/Prefabs/Items/Tools/Item_Tool_SalvageSampler_World.prefab` |
| `Item_Tool_Scanner_World` | `48435f04343913447adc3ca4573951fc` | `Assets/_Project/Prefabs/Items/Tools/Item_Tool_Scanner_World.prefab` |
| `Item_Tool_StunPistol_World` | `1cedfa8d3d2816f48afce0afcdbdc9c0` | `Assets/_Project/Prefabs/Items/Tools/Item_Tool_StunPistol_World.prefab` |

---

## USAGE â€” Addressables Fallback

```csharp
// Gamma Addressables pre-warm fallback â€” inject into PersistentWorldRegistry
// or ItemCatalog world-prefab resolution path.

internal static class ItemAssetGuidFallbacks
{
    internal static readonly (int hashId, string guid)[] ToolPrefabs = new (int, string)[]
    {
        (LocHash.Compute("Item_Tool_BeaconDeployer"),  "d174d546f879a4742bc018eb043e67b7"),
        (LocHash.Compute("Item_Tool_Builder"),         "a9d920f69f572794da38a80172350742"),
        (LocHash.Compute("Item_Tool_EnvAnalyzer"),     "f31fbadc22133c74a9c4e0dafbec547e"),
        (LocHash.Compute("Item_Tool_Flashlight"),      "40a67b632626b2b4ca1b22462448c725"),
        (LocHash.Compute("Item_Tool_HarpoonLauncher"), "2f2aaf08a7039d74ab54a9f41530b73c"),
        (LocHash.Compute("Item_Tool_Knife"),           "774f5752cc67c7f49916466b60350a64"),
        (LocHash.Compute("Item_Tool_LaserCutter"),     "5d6d90d471f7ea44291faf2907d11145"),
        (LocHash.Compute("Item_Tool_Propulsion"),      "f9ee01257418ed74696850470ef62d20"),
        (LocHash.Compute("Item_Tool_Repair"),          "fd6fc0a78e6568b4e972561e8b888d34"),
        (LocHash.Compute("Item_Tool_SalvageSampler"),  "fa20e563eef211a4daf00fe5b0ca6412"),
        (LocHash.Compute("Item_Tool_Scanner"),         "48435f04343913447adc3ca4573951fc"),
        (LocHash.Compute("Item_Tool_StunPistol"),      "1cedfa8d3d2816f48afce0afcdbdc9c0"),
    };
}
```

> **Note:** use the authored `ItemData.stableId` / `PersistentId` strings (`Item_Tool_*`), not the held-tool prefab names (`Tool_*`). The hash must match `ItemCatalog.FindByHash()` and the dropped-item registry path.

---

## AUDIT NOTES
- All `.meta` files parsed successfully. No GUID collisions detected.
- All prefabs are under `Assets/_Project/` (first-party). No third-party tool prefabs exist in the dropped-item catalog.
- Total scanned: **12 prefabs**.
- Missing categories: Consumables, Materials, DataShards â€” no world prefabs found in current directory tree.

**STATUS:** ETA SURGERY_PREPPED

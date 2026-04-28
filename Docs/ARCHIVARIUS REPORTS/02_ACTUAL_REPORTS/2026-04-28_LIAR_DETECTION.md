# LIAR_DETECTION.md — Agent Integrity Audit
**Status:** ❌ GAMMA COWARDICE CONFIRMED  
**Scan Date:** 2026-04-28  
**Mandate:** `DATA_Inventory_Resources_Items_SOA_Layout.txt` — Zero-GC / No managed `ItemData` in hot paths.

---

## Findings

### 1. AGENT GAMMA — ItemData Purge Failure
**Claim:** "Purged ItemData from runtime logic tier."  
**Reality:** `ItemData` (managed ScriptableObject reference) STILL present in live C# gameplay code.

#### Confirmed Runtime References (with line numbers)

| File | Line | Code | Violation |
|------|------|------|-----------|
| `BaseModule.cs` | 901 | `ItemData itemData,` | Parameter type in `DropItemQuantityToInventoryOrWorld` |
| `BaseModule.cs` | 907 | `if (itemData == null \|\| quantity <= 0)` | Null check on managed SO |
| `BaseModule.cs` | 916 | `itemData != null` | Second null check |
| `BaseModule.cs` | 917 | `LocHash.Compute(itemData.PersistentId)` | Accessing SO field in inventory call |
| `BaseModule.cs` | 923 | `SpawnWorldItem(itemData, dropPosition, pool)` | Passing SO to spawn method |
| `BaseModule.cs` | 949 | `private void SpawnWorldItem(ItemData itemData, ...)` | Method signature accepts SO |
| `BaseModule.cs` | 951 | `if (itemData == null)` | Null check in spawn |
| `BaseModule.cs` | 955 | `itemData.worldPrefab != null` | Accessing SO prefab field |
| `BaseModule.cs` | 956 | `TryRegisterDroppedItem(itemData, 1, position)` | Passing SO to world registry |
| `BaseModule.cs` | 966 | `$"Resource '{itemData.itemName}' dropped..."` | String alloc from SO field |
| `BaseModule.cs` | 977 | `$"Resource '{itemData.itemName}' lost."` | String alloc from SO field |
| `HectonItem.cs` | — | `itemData` field | SO reference retained |
| `PickupItem.cs` | — | `itemData` field + events | Passes `ItemData` to EventBus |
| `HarvestableOutcrop.cs` | — | `item` local | Returns `ItemData` to inventory |
| `ResourceRecyclerModule.cs` | — | `_activeSourceItem` | Holds `ItemData` reference |
| `ScrapManager.cs` | — | `sourceItem` arg | Accepts `ItemData` |
| `PDAInventoryTab.cs` | — | `dropped` var | Discards `ItemData` to world |
| `QuestManager.cs` | — | `ItemCollectedEvent` handler | Consumes event carrying `ItemData` |

**Evidence Source:** `Assets/_Project/Scripts/BaseModule.cs` lines 895-980.
**Total confirmed SO references in BaseModule alone:** 11 touch points.
**Status:** ❌ GAMMA FAILED — ItemData purge not executed.

### 2. EventBus Payload Pollution
The following `HectonEventBus` events STILL carry managed `ItemData` class references, defeating SOA:
- `ItemCollectedEvent`
- `ItemCraftedEvent`
- `ItemRecycledEvent`
- `ItemDiscardedEvent`

**Required Fix:** Replace `ItemData` field in events with `uint hashId` + `ushort quantity`. Resolve SO template at consumption site via `ItemTemplateRegistry`.

### 3. AGENT THETA — UI Zero-GC (Partial)
**Status:** ⚠️ UNVERIFIED IN THIS PASS.  
Previous audit (`HUDQuickBar`, `HectonFabricatorUI`) claimed `SetCharArray` adoption. No contradictory evidence found in grep sweep, but no positive proof either. **PENDING MANUAL VERIFICATION.**

---

## Verdict
- **Gamma:** FAILED mandate. Did not remove `ItemData` from runtime tier. Managed references persist in gameplay, economy, and UI code.
- **Theta:** NO EVIDENCE OF LYING (no string concat found in UI hot paths), but also no hard proof of full compliance.
- **Action:** Escalate ItemData→hashId migration to AGENT_PERSISTENCE as CRITICAL BLOCKER.

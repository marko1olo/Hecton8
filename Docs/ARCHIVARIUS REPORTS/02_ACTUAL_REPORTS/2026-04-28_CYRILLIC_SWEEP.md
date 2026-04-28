# CYRILLIC_SWEEP.md — Non-ASCII Character Audit
**Status:** ⚠️ VIOLATIONS DETECTED  
**Scan Date:** 2026-04-28  
**Scope:** `Assets/_Project/` + root `.shader` / `.hlsl`

---

## Shader / HLSL Files — FIRST-PARTY
| File | Cyrillic? | Verdict |
|------|-----------|---------|
| `Assets/_Project/Shaders/Hecton_Item_Highlight.shader` | ❌ No | ✅ COMPLIANT |
| `Assets/_Project/Shaders/UI/Hecton_DiegeticPanelDepthFade.shader` | ❌ No | ✅ COMPLIANT |

## C# Source Files — Russian Comments
| File | Location | Content |
|------|----------|---------|
| `HectonBoidController.cs` | Header comments | Russian architecture notes |
| `BuoyancyObject.cs` | Header / inline | Russian lifecycle comments |
| `InteractionHighlighter.cs` | Header / inline | Russian state-machine comments |
| `HectonItem.cs` | Header | Russian pooling notes |
| `ObjectPoolManager.cs` | Header / inline | Russian timer comments |
| `HectonUnderwaterVisuals.cs` | Inline | Mangled/corrupted unicode lines |
| `Items/PickupItem.cs` | Header | Russian SlowTick notes |
| `HectonFabricatorUI.cs` | Inline | Russian hologram comments |
| `HectonPlayerMovement.cs` | Inline | Russian Sargassum notes |

## Folder Names — Russian
| Path | Issue |
|------|-------|
| `Assets/_Project/Art/Models/Rocks/Rock 4 - УНИВЕРСАЛЬНЫЙ ВЫБОР/` | Russian folder name + subfolder `УНИВЕРСАЛЬНЫЙ ВЫБОР (ТЕКСТУРЫ)` |

## Root / Lore — Russian Text Files
| File | Verdict |
|------|---------|
| `Lore/лор1.txt`, `Lore/лор2.txt`, `Lore/лор3.txt` | Expected (lore docs) |
| `Assets/_Project/Data/текст.txt` | ⚠️ Russian design doc inside runtime asset tree |

## Verdict
- **Shaders:** ✅ CLEAN — no Cyrillic in first-party `.shader`/`.hlsl`.
- **C# Comments:** ⚠️ ARCHITECTURAL DEBT — does not affect runtime, but violates "English-only codebase" standard for AA commercial product.
- **Folder Names:** ❌ VIOLATION — Russian path may break CI/build tools on some locales.
- **Action:** Rename rock folder to ASCII transliteration. Migrate Russian comments to English in next hygiene pass.

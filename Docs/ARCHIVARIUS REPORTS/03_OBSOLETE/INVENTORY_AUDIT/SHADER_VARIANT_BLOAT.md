# SHADER VARIANT BLOAT — HECTON-8 Static Audit

**Generated:** 2026-04-27 | **Auditor:** Static Compliance Officer  
**Mandates:** REND_URP_Graphics_HotPath_Optimization_HLOD.txt (§3), AGENTS.md (Shader Variant rules)

---

## I. Materials with > 5 Shader Keywords (in `_Project/Art/Materials/`)

| Material | Shader Keywords Count | Keywords | Severity |
|---|---|---|---|
| *(none found with >5 keywords)* | — | — | ✅ **CLEAN** |

**Analysis:** All first-party materials in `Assets/_Project/Art/Materials/` have 0–2 shader keywords. No bloat detected in first-party materials.

---

## II. Third-Party Materials with > 5 Keywords

| Material | Shader Keywords Count | Keywords | Severity |
|---|---|---|---|
| `MMBRP_BlueSkybox.mat` (Feel/MMTools) | 2 | `_METALLIC_SETUP _SUNDISK_HIGH_QUALITY` | Low — demo-only |
| `EmojiOne.asset` (TMP) | 1 | `UNITY_UI_CLIP_RECT` | Low — UI font |

**Third-party materials are not production-critical. No action required.**

---

## III. Materials Using `Universal Render Pipeline/Lit` Instead of `Hecton8/CoreLit`

### First-Party `.mat` Files

**Scan Method:** `m_Shader` reference in `.mat` YAML + `Shader.Find("Universal Render Pipeline/Lit")` in scripts

| Material | Path | Current Shader | Required Shader | Severity |
|---|---|---|---|---|
| `Mat_RuinSeepSheen.mat` | `Assets/_Project/Art/Materials/Construction/` | Custom (guid: e388b6e8...) | Verify vs CoreLit | ⚠️ **Needs Verification** |
| `Mat_LeakWetSheen.mat` | `Assets/_Project/Art/Materials/Construction/` | Custom (guid: 05bf6ca5...) | Verify vs CoreLit | ⚠️ **Needs Verification** |

**Note:** Most `_Project/Art/Materials/` files reference custom shader GUIDs (Hecton8-specific), not raw `Universal Render Pipeline/Lit`. The `.mat` YAML does not store shader names directly — only GUIDs. A full GUID→shader name resolution requires Unity Editor or asset database scan.

### Runtime `Shader.Find("Universal Render Pipeline/Lit")` in First-Party Scripts

| File | Line | Context | Severity |
|---|---|---|---|
| `BeaconRuntime.cs` | L116 | Fallback material for beacons | 🔴 **HIGH** — runtime fallback |
| `SargassumGlobalDragManager.cs` | L1843, L1862 | Fallback crate/scrap materials | 🔴 **HIGH** — runtime fallback |
| `HectonSphereGenerator.cs` | L385 | Absolute fallback in editor tool | 🟡 **Medium** — editor-only context |
| `ImpostorSystem.cs` | L728 | Fallback if Unlit not found | 🟡 **Medium** — rare path |

### Editor-Only `Shader.Find("Universal Render Pipeline/Lit")` (Acceptable)

| File | Line | Context |
|---|---|---|
| `ConstructionBootstrapAuthoring.cs` | L631 | Editor authoring script |
| `CreatureProxyPrefabAuthoring.cs` | L177 | Editor authoring script |
| `HectonPrefabIntegrityScanner.cs` | L518 | Editor validation tool |
| `HectonProjectAuditor.cs` | L91 | Editor audit tool (checks for banned shader) |
| `ResourceWorldBootstrapAuthoring.cs` | L169 | Editor authoring script |
| `WorldProceduralPlaceholderAuthoring.cs` | L149 | Editor authoring script |
| `WorldProceduralProxyAuthoring.cs` | L916 | Editor authoring script |
| `WorldProceduralOrganicMiscContract.cs` | L12 | Editor constant definition |
| `WorldProceduralStructuralContract.cs` | L14 | Editor constant definition |
| `WorldProceduralSupportContract.cs` | L12 | Editor constant definition |

---

## IV. Summary

- **No materials with >5 shader keywords** found in first-party assets.
- **2 runtime scripts** use `Shader.Find("Universal Render Pipeline/Lit")` as fallback — violates AGENTS.md mandate to use `Hecton8/CoreLit`.
- **10 editor scripts** reference URP/Lit — acceptable per [EXCEPT] rules.
- `HectonProjectAuditor.cs` already contains a **banned shader check** that flags URP/Lit at edit time. This is correctly implemented.

**STATUS:** PENDING VERIFICATION — full GUID→shader resolution requires Unity Editor.

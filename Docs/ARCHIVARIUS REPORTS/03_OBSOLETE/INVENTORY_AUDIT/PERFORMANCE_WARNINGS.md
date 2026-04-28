# HECTON-8 PERFORMANCE WARNINGS
## Scribe-Compliance Audit | Status: CONTINUOUS

---

### RULE REMINDER (from AGENTS.md)
- Max texture resolution: hero ≤ 2048 · world ≤ 2048 · small props ≤ 512.
- VRAM HARD CEILING: 1800MB (MX350). Texture budget: 900MB.
- Materials should use `Hecton8_CoreLit` or specialized shaders, NOT `Universal Render Pipeline/Lit`.
- FBX > 50k triangles without LOD Group = VIOLATION.

---

### 🔴 TEXTURES > 2048 IN `_Project/`

No first-party textures with `maxTextureSize > 2048` were found in `Assets/_Project/`.

**VERDICT**: First-party texture max sizes are within budget. **COMPLIANT.**

---

### 🟡 TEXTURES > 2048 IN THIRD-PARTY / OTHER

| Path | Max Size | Category |
|------|----------|----------|
| `Assets/ScifiFacility/Textures/DetailSheet_normal.png` | 4096 | Third-party demo asset |
| `Assets/ScifiFacility/Textures/plane_2x2_DefaultMaterial_Normal.png` | 4096 | Third-party demo asset |
| `Assets/ScifiFacility/Textures/sphere_basecolor.png` | 4096 | Third-party demo asset |
| `Assets/ScifiFacility/Textures/DetailSheet_mask.png` | 4096 | Third-party demo asset |
| `Assets/ScifiFacility/Textures/BrushedMetal_dirt_roughness.png` | 4096 | Third-party demo asset |
| `Assets/ScifiFacility/Textures/Base_normal.png` | 4096 | Third-party demo asset |
| `Assets/ScifiFacility/Textures/Base_dirt_roughness.png` | 4096 | Third-party demo asset |
| `Assets/ScifiFacility/Textures/Base_02_dirt_roughness.png` | 4096 | Third-party demo asset |
| `Assets/ScifiFacility/Textures/Transparent_basecolor.png` | 4096 | Third-party demo asset |
| `Assets/ScifiFacility/Textures/Transparent_normal.png` | 4096 | Third-party demo asset |
| `Assets/MapMagic/Map_Graph/New Gen/heightmap.png` | 4096 | Third-party (MapMagic) |
| `Assets/Feel/MMTools/Tools/MMVFX/MMBloomDirt/MMBloomDirt1.png` | 4096 | Third-party (Feel) |
| `Assets/Feel/MMTools/Tools/MMVFX/MMBloomDirt/MMBloomDirt2.png` | 4096 | Third-party (Feel) |
| `Assets/Feel/MMTools/Tools/MMVFX/MMBloomDirt/MMBloomDirt3.png` | 4096 | Third-party (Feel) |
| `Assets/Feel/MMTools/Tools/MMVFX/MMBloomDirt/MMBloomDirt4.png` | 4096 | Third-party (Feel) |
| `Assets/Plugins/Sirenix/Odin Inspector/Assets/Editor/SdfIconAtlas.png` | 16384 | Third-party (Odin, Editor-only) |

**RECOMMENDATION**: `ScifiFacility` textures are demo assets — downgrade to 2048 or remove from build. Odin atlas is editor-only (not in build). Feel/MMBloomDirt — downgrade to 2048 if included in build; Bloom is [FORBID] on MX350 anyway.

---

### 🔴 MATERIALS USING `Universal Render Pipeline/Lit` IN FIRST-PARTY

The following first-party `.mat` assets reference the standard URP/Lit shader instead of `Hecton8_CoreLit` or specialized shaders:

| Path | Current Shader | Required Shader |
|------|---------------|-----------------|
| `Assets/_Project/Art/Materials/red.mat` | URP/Lit (guid 933532…) | `Hecton8/Environment/Hecton_DryZoneLit` |
| `Assets/_Project/Art/Materials/Sand.mat` | URP/Lit (guid 933532…) | `Hecton8/Environment/Hecton_DryZoneLit` |
| `Assets/_Project/Art/Materials/Snow.mat` | URP/Lit (guid 933532…) | `Hecton8/Environment/Hecton_DryZoneLit` |
| `Assets/_Project/Art/Materials/Skybox.mat` | URP/Lit or custom | Verify — should be `Hecton8/Atmosphere/HectonSkybox` |
| `Assets/_Project/Art/Materials/terrain.mat` | URP/Lit (guid 58f923…) | `Hecton8/Environment/Hecton_TerrainLit` |
| `Assets/_Project/Art/Materials/terrain 1.mat` | URP/Lit (guid 933532…) | `Hecton8/Environment/Hecton_DryZoneLit` |
| `Assets/_Project/Art/Materials/terrain 2.mat` | URP/Lit (guid 933532…) | `Hecton8/Environment/Hecton_DryZoneLit` |
| `Assets/_Project/Art/Materials/Meshy_AI_Alien_barnacles_clust_0301230506_texture.mat` | URP/Lit (guid 933532…) | Custom organic shader |

*(Note: Many `Mat_`-prefixed materials in subfolders like `Construction/`, `Resources/`, `Tools/`, `Diagnostics/` also reference guid 933532 (URP/Lit). These are placeholder/blockout materials — acceptable for prototyping but must be replaced before production.)*

**TOTAL MATERIALS ON URP/Lit (first-party): ~60+**

**RECOMMENDATION**: Run `HectonProjectAuditor` (which already checks for banned URP/Lit). Replace all production-facing materials with `Hecton_DryZoneLit` or specialized shaders. Placeholder materials (Diagnostics, ToolTrial) are low priority.

---

### 🔴 RUNTIME `Shader.Find("Universal Render Pipeline/Lit")` IN FIRST-PARTY SCRIPTS

| File | Line | Context | Risk |
|------|------|---------|------|
| `Assets/_Project/Scripts/BeaconRuntime.cs` | 116 | Fallback beacon material at runtime | 🔴 HIGH — `Shader.Find` at runtime allocates + SRP Batcher incompatible |
| `Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs` | 1843, 1862 | Fallback crate/scrap materials at runtime | 🔴 HIGH — same issue |
| `Assets/_Project/Scripts/World/ImpostorSystem.cs` | 726-728 | Fallback for impostor render | 🔴 HIGH — `Shader.Find` in render path |

**RECOMMENDATION**: Pre-cache all fallback materials in Awake or via Addressables. `Shader.Find` at runtime is a string-allocating operation that breaks SRP Batcher compatibility.

---

### 🟢 EDITOR-ONLY `Shader.Find("Universal Render Pipeline/Lit")` (Acceptable)

| File | Line | Context |
|------|------|---------|
| `Assets/_Project/Editor/HectonSphereGenerator.cs` | 385 | Editor generator |
| `Assets/_Project/Scripts/Editor/ConstructionBootstrapAuthoring.cs` | 631 | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/CreatureProxyPrefabAuthoring.cs` | 177 | Editor authoring |
| `Assets/_Project/Scripts/Editor/HectonPrefabIntegrityScanner.cs` | 518 | Editor scanner |
| `Assets/_Project/Scripts/Editor/ResourceWorldBootstrapAuthoring.cs` | 169 | Editor bootstrap |
| `Assets/_Project/Scripts/Editor/WorldProceduralPlaceholderAuthoring.cs` | 149 | Editor authoring |
| `Assets/_Project/Scripts/Editor/WorldProceduralProxyAuthoring.cs` | 916 | Editor authoring |

---

### 🟡 FBX > 50K TRIANGLES WITHOUT LOD GROUP

No `.fbx` files with vertex counts exceeding 50K were identified via `.meta` analysis. However, `.meta` files do not always contain `vertexCount`. A Unity Editor scan via `HectonPrefabIntegrityScanner` is recommended for definitive results.

**STATUS**: PENDING EDITOR VERIFICATION.

---

### SUMMARY

| Category | Count | Severity |
|----------|-------|----------|
| Textures > 2048 (first-party) | 0 | 🟢 COMPLIANT |
| Textures > 2048 (third-party) | 16 | 🟡 LOW (most are demo or editor-only) |
| Materials on URP/Lit (first-party) | ~60+ | 🔴 HIGH — violates shader standard |
| Runtime `Shader.Find("URP/Lit")` | 3 files | 🔴 HIGH — runtime alloc + SRP Batcher break |
| FBX > 50k without LOD | TBD | 🟡 PENDING EDITOR SCAN |

**PRIMARY ACTION ITEMS**:
1. Replace all production `.mat` files using URP/Lit with `Hecton_DryZoneLit` or project-specific shaders.
2. Pre-cache fallback materials in `BeaconRuntime.cs`, `SargassumGlobalDragManager.cs`, `ImpostorSystem.cs`.
3. Downgrade/remove `ScifiFacility` demo textures (4096 → 2048 or delete).

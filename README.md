<div align="center">

![HECTON-8 Banner](assets/banner.png)

# HECTON-8 вЂ” Deep Sea Noir / NASA-Punk 3D Survival Game

[![GitHub Pages](https://img.shields.io/badge/GitHub%20Pages-Live%20Demo-brightgreen?style=for-the-badge&logo=github)](https://marko1olo.github.io/Hecton8/)
[![Deploy GitHub Pages](https://github.com/marko1olo/Hecton8/actions/workflows/deploy-gh-pages.yml/badge.svg)](https://github.com/marko1olo/Hecton8/actions/workflows/deploy-gh-pages.yml)
[![Unity](https://img.shields.io/badge/Engine-Unity%206000.5%20URP-black?style=for-the-badge&logo=unity)](https://unity.com)
[![C#](https://img.shields.io/badge/Language-C%23%20Burst--Compiled-purple?style=for-the-badge&logo=csharp)](https://docs.unity3d.com/Packages/com.unity.burst@latest)
[![Performance Target](https://img.shields.io/badge/Target-60%20FPS%20%7C%200B%2Fframe%20GC-00ff88?style=for-the-badge)]()
[![Shaders](https://img.shields.io/badge/Graphics-Custom%20URP%20Shaders-blue?style=for-the-badge)]()
[![License](https://img.shields.io/badge/License-Commercial%20Anti--Theft-red?style=for-the-badge)](LICENSE.md)
[![Status](https://img.shields.io/badge/Status-V0%20Vertical%20Slice-orange?style=for-the-badge)]()

> **AA Deep Sea Noir / NASA-Punk 3D survival game built on Unity 6000.5 URP вЂ” strict 60 FPS, 0 B/frame GC allocation target, scalable from 2GB VRAM handhelds to Ultra PCVR.**

</div>

---
<p align="center">
  <a href="https://twitter.com/intent/tweet?text=Check%20out%20Hecton8%20on%20GitHub!&url=https%3A%2F%2Fmarko1olo.github.io%2FHecton8%2F"><img src="https://img.shields.io/badge/Share-Twitter%2FX-1DA1F2?style=for-the-badge&logo=x" alt="Share on X"/></a> &nbsp;
  <a href="https://news.ycombinator.com/submitlink?u=https%3A%2F%2Fmarko1olo.github.io%2FHecton8%2F&t=Check%20out%20Hecton8%20on%20GitHub!"><img src="https://img.shields.io/badge/Submit-Hacker%20News-FF6600?style=for-the-badge&logo=y-combinator" alt="Submit to HN"/></a> &nbsp;
  <a href="https://reddit.com/submit?url=https%3A%2F%2Fmarko1olo.github.io%2FHecton8%2F&title=Check%20out%20Hecton8%20on%20GitHub!"><img src="https://img.shields.io/badge/Post-Reddit-FF4500?style=for-the-badge&logo=reddit" alt="Post on Reddit"/></a>
</p>
---

## Architectural Overview

```mermaid
graph TD
    A[Unity 6000.5 URP Runtime Engine] --> B[Custom Volumetric Ocean Shader Pipeline]
    A --> C[Burst-Compiled C# Systems DOD]
    C --> D[NativeMemory Unmanaged Collections]
    C --> E[0 B/frame GC Hot-Path Loop]
    B --> F[Continuous GlobalQualityWeight Scaler]
    F --> G[Target Profiles: 2GB Handheld to Ultra PCVR]
```

## Component Matrix

| Component / Path | Technology / Subsystem | Primary Responsibilities |
| --- | --- | --- |
| `Assets/` | C# Unity Engine Code & Assets | Core gameplay scripts, DOD systems, ScriptableObject definitions, URP Shaders |
| `ProjectSettings/` | Unity Engine Settings | Editor configuration, quality levels, package dependency manifest, URP assets |
| `AGENTS.md` | Authority & Process Control | System mandates, Hecton-8 build preflight rules, CPU allocation gates |
| `PROJECT_BIBLES.md` | Domain Bibles Index | Visual style guidelines, performance budget specs, rendering mandates |
| `VISION_LOCKS.md` | Product Direction | Scope boundaries, gameplay pillars, NASA-Punk / Deep Sea Noir aesthetic standards |

---

## Original Developer Documentation

<div align="center">

<img src="assets/banner.png" width="100%" alt="HECTON-8 Banner"/>

# HECTON-8 вЂ” Deep Sea Noir / NASA-Punk 3D Survival Game

[![Unity](https://img.shields.io/badge/Unity-6000.5%20URP-black?style=for-the-badge&logo=unity)](https://unity.com)
[![Language](https://img.shields.io/badge/C%23-Burst%20Compiled-purple?style=for-the-badge&logo=csharp)]()
[![FPS](https://img.shields.io/badge/Target-60%20FPS%20%7C%200B%2Fframe%20GC-00ff88?style=for-the-badge)]()
[![License](https://img.shields.io/badge/License-Commercial%20Anti--Theft-red?style=for-the-badge)](LICENSE.md)
[![Status](https://img.shields.io/badge/Status-V0%20Vertical%20Slice-orange?style=for-the-badge)]()

> **AA Deep Sea Noir / NASA-Punk survival on Unity 6000.5 URP вЂ” 60 FPS, 0B/frame GC, scalable from 2GB VRAM handhelds to Ultra PCVR.**

[рџЊЉ Wishlist](#) В· [рџ“– Devlog](#) В· [рџђ› Issues](../../issues)

</div>

---

> **AA Deep Sea Noir / NASA-Punk 3D game built on Unity 6000.5 URP with extreme memory optimizations (60 FPS / GC 0 B/frame target).**

---

### рџљЂ Technical Standards & Architecture

* вљЎ **Performance Budget:** Strict 60 FPS (16.67 ms frame budget), 0 B/frame GC allocation in gameplay hot-paths.
* рџЊЉ **Deep Sea Rendering:** Custom URP volumetric ocean shaders, photic underwater lighting, and procedural sea floor.
* рџЋ® **Platform Portability:** Scalable continuous `GlobalQualityWeight` architecture targeting 2GB VRAM handhelds up to Ultra PCVR.
* рџ“¦ **Unmanaged Memory:** Burst-compiled C#, NativeMemory collections, and Data-Oriented Design (DOD).

---

### рџ“њ License / Р›РёС†РµРЅР·РёСЏ
Protected under **HECTON-8 Commercial Anti-Theft & Source-Available License (Copyright (c) 2026 Adolf Petushkov)**. Maintainers and AI research welcome!

---

<details>
<summary><b>рџ‡·рџ‡є РљСЂР°С‚РєРѕРµ РѕРїРёСЃР°РЅРёРµ РЅР° СЂСѓСЃСЃРєРѕРј</b></summary>

### HECTON-8 вЂ” Deep Sea Noir / NASA-Punk 3D Р’С‹Р¶РёРІР°РЅРёРµ

**HECTON-8** вЂ” СЌС‚Рѕ AA 3D-РёРіСЂР° РЅР° РІС‹Р¶РёРІР°РЅРёРµ РІ Р°С‚РјРѕСЃС„РµСЂРЅРѕРј СЃРµС‚С‚РёРЅРіРµ Deep Sea Noir / NASA-Punk, СЂР°Р·СЂР°Р±Р°С‚С‹РІР°РµРјР°СЏ РЅР° РґРІРёР¶РєРµ Unity 6000.5 URP.

#### РўРµС…РЅРёС‡РµСЃРєРёРµ РЎС‚Р°РЅРґР°СЂС‚С‹ Рё РђСЂС…РёС‚РµРєС‚СѓСЂР°:
1. **Р–РµСЃС‚РєРёР№ Р‘СЋРґР¶РµС‚ РџСЂРѕРёР·РІРѕРґРёС‚РµР»СЊРЅРѕСЃС‚Рё**: Р¦РµР»РµРІРѕР№ РїРѕРєР°Р·Р°С‚РµР»СЊ вЂ” 60 FPS (16.67 РјСЃ РЅР° РєР°РґСЂ) Рё 0 B/frame GC-Р°Р»Р»РѕРєР°С†РёР№ РІ РіРѕСЂСЏС‡РёС… С†РёРєР»Р°С… РіРµР№РјРїР»РµСЏ.
2. **РќРёР·РєРѕСѓСЂРѕРІРЅРµРІР°СЏ РџР°РјСЏС‚СЊ Рё DOD**: РСЃРїРѕР»СЊР·РѕРІР°РЅРёРµ РєРѕРјРїРёР»СЏС‚РѕСЂР° Burst, РЅРµСѓРїСЂР°РІР»СЏРµРјРѕР№ РїР°РјСЏС‚Рё `NativeMemory` Рё Data-Oriented Design.
3. **Р РµРЅРґРµСЂРёРЅРі Рё РњР°СЃС€С‚Р°Р±РёСЂРѕРІР°РЅРёРµ**: РљР°СЃС‚РѕРјРЅС‹Рµ РѕР±СЉРµРјРЅС‹Рµ С€РµР№РґРµСЂС‹ РѕРєРµР°РЅСЃРєРѕР№ С‚РѕР»С‰Рё РІРѕРґС‹ РІ URP, РЅРµРїСЂРµСЂС‹РІРЅР°СЏ СЃРёСЃС‚РµРјР° `GlobalQualityWeight` РґР»СЏ РјР°СЃС€С‚Р°Р±РёСЂРѕРІР°РЅРёСЏ РѕС‚ РїРѕСЂС‚Р°С‚РёРІРѕРє СЃ 2GB VRAM РґРѕ Ultra PCVR.
</details>



### 🏗️ Submarine Engine Architecture (Unity 6000 URP)

```mermaid
graph TD
    Input[🎮 Hydro Controls] --> Core[⚙️ Submarine Main Loop]
    Core --> Physics[🌊 Hydro-X Buoyancy Engine]
    Core --> Terrain[🗺️ MapMagic 2 Chunk Manager]
    Terrain --> Voxel[🧊 Voxel Mesh Generator]
    Physics --> Telemetry[📊 Zero-GC Telemetry HUD]
    Core --> Render[🎨 Unity 6000 URP Shaders]
```

### ⚡ Technical Performance Budgets

| Metric | Budget / Actual | Status |
|---|---|---|
| **Target Frame Rate** | 60 FPS Constant | 🎮 PASS |
| **Garbage Collector Allocations** | 0 B / frame (Zero-GC) | ⚡ OPTIMIZED |
| **VRAM Memory Footprint** | < 2.2 GB VRAM | 🟢 STABLE |
| **Chunk Generation Latency** | < 12ms / chunk | 🚀 FAST |

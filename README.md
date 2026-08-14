<div align="center">

![HECTON-8 Banner](Assets/banner.png)

# HECTON-8 — Deep Sea Noir / NASA-Punk 3D Survival Game

[![GitHub Pages](https://img.shields.io/badge/GitHub%20Pages-Live%20Demo-brightgreen?style=for-the-badge&logo=github)](https://marko1olo.github.io/Hecton8/)
[![Deploy GitHub Pages](https://github.com/marko1olo/Hecton8/actions/workflows/deploy-gh-pages.yml/badge.svg)](https://github.com/marko1olo/Hecton8/actions/workflows/deploy-gh-pages.yml)
[![Unity](https://img.shields.io/badge/Engine-Unity%206000.5%20URP-black?style=for-the-badge&logo=unity)](https://unity.com)
[![C#](https://img.shields.io/badge/Language-C%23%20Burst--Compiled-purple?style=for-the-badge&logo=csharp)](https://docs.unity3d.com/Packages/com.unity.burst@latest)
[![Performance Target](https://img.shields.io/badge/Target-60%20FPS%20%7C%200B%2Fframe%20GC-00ff88?style=for-the-badge)]()
[![Shaders](https://img.shields.io/badge/Graphics-Custom%20URP%20Shaders-blue?style=for-the-badge)]()
[![License](https://img.shields.io/badge/License-Commercial%20Anti--Theft-red?style=for-the-badge)](LICENSE.md)
[![Status](https://img.shields.io/badge/Status-V0%20Vertical%20Slice-orange?style=for-the-badge)]()

> **AA Deep Sea Noir / NASA-Punk 3D survival game built on Unity 6000.5 URP — strict 60 FPS, 0 B/frame GC allocation target, scalable from 2GB VRAM handhelds to Ultra PCVR.**

</div>

## Start here

The public surface has two purposes: it introduces the visual premise and gives contributors a dependable route into the source tree. The [interactive project page](https://marko1olo.github.io/Hecton8/) is a presentation of the project; it is **not** a browser build or runtime-performance benchmark.

| Goal | Start with | Continue with |
| --- | --- | --- |
| Explore the intended atmosphere | [Interactive project page](https://marko1olo.github.io/Hecton8/) | The visual references and concept illustrations below |
| Understand product boundaries | [Vision locks](VISION_LOCKS.md) | [Project bibles](PROJECT_BIBLES.md) for the relevant discipline |
| Navigate the technical corpus | [Documentation index](Docs/README.md) | The source-backed maps and current route documents it names |
| Make a source contribution | [Contributing guide](CONTRIBUTING.md) | The authority chain and verification route before changing code or assets |
| Check current build or playtest constraints | [Build and playtest issues](BUILD_PLAYTEST_ISSUES.md) | Fresh Unity, player, profiler, or device evidence before making readiness claims |

> **Evidence boundary.** Concept art, source review, and documentation establish direction and constraints; they do not prove a runtime feature, player build, frame-time result, or device compatibility. Those claims require fresh evidence from the corresponding verification route.

---
<p align="center">
  <a href="https://twitter.com/intent/tweet?text=Check%20out%20Hecton8%20on%20GitHub!&url=https%3A%2F%2Fmarko1olo.github.io%2FHecton8%2F"><img src="https://img.shields.io/badge/Share-Twitter%2FX-1DA1F2?style=for-the-badge&logo=x" alt="Share on X"/></a> &nbsp;
  <a href="https://news.ycombinator.com/submitlink?u=https%3A%2F%2Fmarko1olo.github.io%2FHecton8%2F&t=Check%20out%20Hecton8%20on%20GitHub!"><img src="https://img.shields.io/badge/Submit-Hacker%20News-FF6600?style=for-the-badge&logo=y-combinator" alt="Submit to HN"/></a> &nbsp;
  <a href="https://reddit.com/submit?url=https%3A%2F%2Fmarko1olo.github.io%2FHecton8%2F&title=Check%20out%20Hecton8%20on%20GitHub!"><img src="https://img.shields.io/badge/Post-Reddit-FF4500?style=for-the-badge&logo=reddit" alt="Post on Reddit"/></a>
</p>

---

## 🌊 Visual References — The World of HECTON-8

> *These images capture the visual target — the underwater world HECTON-8 is being built toward.*

<div align="center">

<img src="Assets/illustrations/ref_best_surface.png" width="100%" alt="Surface vista — alien ocean with gas giant, NASA research station and exotic flora"/>

*Surface vista: alien coastline, NASA-punk research outpost, gas giant on the horizon*

</div>

---

<div align="center">
<table>
<tr>
<td width="50%"><img src="Assets/illustrations/ref_beauty.webp" width="100%" alt="Shallow underwater coral reef with ancient ruins"/></td>
<td width="50%"><img src="Assets/illustrations/ref_deep_bioluminescence.jpg" width="100%" alt="Deep bioluminescent zone with alien flora"/></td>
</tr>
<tr>
<td align="center"><i>Shallow zone — coral reefs, warm light, ancient structures</i></td>
<td align="center"><i>Deep zone — bioluminescent alien flora, darkness, danger</i></td>
</tr>
</table>
</div>

---

## 🎨 Concept Illustrations

<div align="center">

<img src="Assets/illustrations/illust_bioluminescent_base.jpg" width="100%" alt="Bioluminescent underwater research base"/>

*The deep sea NASA research station — bioluminescent flora surrounds the abandoned complex*

</div>

---

<div align="center">
<table>
<tr>
<td width="50%"><img src="Assets/illustrations/illust_diver_encounter.jpg" width="100%" alt="Player in NASA-punk suit facing a deep sea leviathan"/></td>
<td width="50%"><img src="Assets/illustrations/illust_surface_gaze.jpg" width="100%" alt="Looking up from the deep — gas giant through ocean surface"/></td>
</tr>
<tr>
<td align="center"><i>Player encounter — NASA suit vs deep sea leviathan</i></td>
<td align="center"><i>Looking up from the abyss — the gas giant through water</i></td>
</tr>
</table>
</div>

---

<div align="center">
<table>
<tr>
<td width="50%"><img src="Assets/illustrations/illust_abyssal_trench.jpg" width="100%" alt="Abyssal trench descent — submarine in underwater canyon"/></td>
<td width="50%"><img src="Assets/illustrations/illust_nasa_hud.jpg" width="100%" alt="NASA-punk submarine cockpit HUD interface"/></td>
</tr>
<tr>
<td align="center"><i>Abyssal trench — descending into the unknown canyon</i></td>
<td align="center"><i>Submarine cockpit — telemetry HUD, depth gauge, oxygen</i></td>
</tr>
</table>
</div>

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

### ⚡ Technical Performance Guardrails

> **These are V0 development targets, not claimed player-build measurements.** Runtime, profiler, and device captures remain the source of truth for performance verification.

| Metric | Development guardrail | Verification state |
|---|---|---|
| **Frame time** | 60 FPS target / 16.67 ms frame budget | Target — requires fresh runtime evidence |
| **Main thread** | ≤ 12 ms budget | Target — requires profiler evidence |
| **GC allocation** | 0 B per frame in gameplay hot paths | Target — requires profiler evidence |
| **Compact VRAM** | ≤ 1.8 GB hard ceiling | Target — requires device or player-build evidence |
| **Texture budget** | ≤ 900 MB on compact tier | Target — requires memory evidence |
| **Render targets + depth** | ≤ 320 MB on compact tier | Target — requires memory evidence |

---

### 🚀 Technical Standards & Architecture

* **Performance budget:** A 60 FPS target with a 16.67 ms frame budget and zero per-frame allocations in gameplay hot paths.
* **Deep-sea rendering:** Custom URP volumetric-ocean shaders, photic underwater lighting, and procedural seafloor systems.
* **Platform portability:** Continuous `GlobalQualityWeight` scaling from compact hardware through high-end PCVR.
* **Memory discipline:** Burst-compiled C#, unmanaged collections, and data-oriented systems for budgeted runtime paths.

---

### 📜 License / Лицензия
Protected under **HECTON-8 Commercial Anti-Theft & Source-Available License (Copyright (c) 2026 Adolf Petushkov)**. Maintainers and AI research welcome!

---

<details>
<summary><b>🇷🇺 Краткое описание на русском</b></summary>

### HECTON-8 — Deep Sea Noir / NASA-Punk 3D Выживание

**HECTON-8** — это AA 3D-игра на выживание в атмосферном сеттинге Deep Sea Noir / NASA-Punk, разрабатываемая на движке Unity 6000.5 URP.

#### Технические Стандарты и Архитектура:
1. **Жёсткий Бюджет Производительности**: Целевой показатель — 60 FPS (16.67 мс на кадр) и 0 B/frame GC-аллокаций в горячих циклах геймплея.
2. **Низкоуровневая Память и DOD**: Использование компилятора Burst, неуправляемой памяти `NativeMemory` и Data-Oriented Design.
3. **Рендеринг и Масштабирование**: Кастомные объёмные шейдеры океанской толщи воды в URP, непрерывная система `GlobalQualityWeight` для масштабирования от портативок с 2GB VRAM до Ultra PCVR.
</details>

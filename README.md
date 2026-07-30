<div align="center">

![HECTON-8 Banner](assets/banner.png)

# HECTON-8 — Deep Sea Noir / NASA-Punk 3D Survival Game

[![GitHub Pages](https://img.shields.io/badge/GitHub%20Pages-Live%20Demo-brightgreen?style=for-the-badge&logo=github)](https://barsukdana.github.io/Hecton8/)
[![Deploy GitHub Pages](https://github.com/barsukdana/Hecton8/actions/workflows/deploy-gh-pages.yml/badge.svg)](https://github.com/barsukdana/Hecton8/actions/workflows/deploy-gh-pages.yml)
[![Unity](https://img.shields.io/badge/Engine-Unity%206000.4%20URP-black?style=for-the-badge&logo=unity)](https://unity.com)
[![C#](https://img.shields.io/badge/Language-C%23%20Burst--Compiled-purple?style=for-the-badge&logo=csharp)](https://docs.unity3d.com/Packages/com.unity.burst@latest)
[![Performance Target](https://img.shields.io/badge/Target-60%20FPS%20%7C%200B%2Fframe%20GC-00ff88?style=for-the-badge)]()
[![Shaders](https://img.shields.io/badge/Graphics-Custom%20URP%20Shaders-blue?style=for-the-badge)]()
[![License](https://img.shields.io/badge/License-Commercial%20Anti--Theft-red?style=for-the-badge)](LICENSE.md)
[![Status](https://img.shields.io/badge/Status-V0%20Vertical%20Slice-orange?style=for-the-badge)]()

> **AA Deep Sea Noir / NASA-Punk 3D survival game built on Unity 6000.4 URP — strict 60 FPS, 0 B/frame GC allocation target, scalable from 2GB VRAM handhelds to Ultra PCVR.**

</div>

---

## Architectural Overview

```mermaid
graph TD
    A[Unity 6000.4 URP Runtime Engine] --> B[Custom Volumetric Ocean Shader Pipeline]
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

# HECTON-8 — Deep Sea Noir / NASA-Punk 3D Survival Game

[![Unity](https://img.shields.io/badge/Unity-6000.4%20URP-black?style=for-the-badge&logo=unity)](https://unity.com)
[![Language](https://img.shields.io/badge/C%23-Burst%20Compiled-purple?style=for-the-badge&logo=csharp)]()
[![FPS](https://img.shields.io/badge/Target-60%20FPS%20%7C%200B%2Fframe%20GC-00ff88?style=for-the-badge)]()
[![License](https://img.shields.io/badge/License-Commercial%20Anti--Theft-red?style=for-the-badge)](LICENSE.md)
[![Status](https://img.shields.io/badge/Status-V0%20Vertical%20Slice-orange?style=for-the-badge)]()

> **AA Deep Sea Noir / NASA-Punk survival on Unity 6000.4 URP — 60 FPS, 0B/frame GC, scalable from 2GB VRAM handhelds to Ultra PCVR.**

[🌊 Wishlist](#) · [📖 Devlog](#) · [🐛 Issues](../../issues)

</div>

---

> **AA Deep Sea Noir / NASA-Punk 3D game built on Unity 6000.4 URP with extreme memory optimizations (60 FPS / GC 0 B/frame target).**

---

### 🚀 Technical Standards & Architecture

* ⚡ **Performance Budget:** Strict 60 FPS (16.67 ms frame budget), 0 B/frame GC allocation in gameplay hot-paths.
* 🌊 **Deep Sea Rendering:** Custom URP volumetric ocean shaders, photic underwater lighting, and procedural sea floor.
* 🎮 **Platform Portability:** Scalable continuous `GlobalQualityWeight` architecture targeting 2GB VRAM handhelds up to Ultra PCVR.
* 📦 **Unmanaged Memory:** Burst-compiled C#, NativeMemory collections, and Data-Oriented Design (DOD).

---

### 📜 License / Лицензия
Protected under **HECTON-8 Commercial Anti-Theft & Source-Available License (Copyright (c) 2026 Adolf Petushkov)**. Maintainers and AI research welcome!

---

<details>
<summary><b>🇷🇺 Краткое описание на русском</b></summary>

### HECTON-8 — Deep Sea Noir / NASA-Punk 3D Выживание

**HECTON-8** — это AA 3D-игра на выживание в атмосферном сеттинге Deep Sea Noir / NASA-Punk, разрабатываемая на движке Unity 6000.4 URP.

#### Технические Стандарты и Архитектура:
1. **Жесткий Бюджет Производительности**: Целевой показатель — 60 FPS (16.67 мс на кадр) и 0 B/frame GC-аллокаций в горячих циклах геймплея.
2. **Низкоуровневая Память и DOD**: Использование компилятора Burst, неуправляемой памяти `NativeMemory` и Data-Oriented Design.
3. **Рендеринг и Масштабирование**: Кастомные объемные шейдеры океанской толщи воды в URP, непрерывная система `GlobalQualityWeight` для масштабирования от портативок с 2GB VRAM до Ultra PCVR.
</details>

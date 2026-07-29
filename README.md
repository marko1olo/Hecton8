<div align="center">

![Banner](https://raw.githubusercontent.com/marko1olo/gigahrush/main/docs/hecton8_banner.jpg)

# HECTON-8 — Deep Sea Noir / NASA-Punk 3D Survival Game

[![Unity](https://img.shields.io/badge/Unity-6000.4%20URP-black?style=for-the-badge&logo=unity)](https://unity.com)
[![Language](https://img.shields.io/badge/C%23-Burst%20Compiled-purple?style=for-the-badge&logo=csharp)]()
[![FPS Target](https://img.shields.io/badge/Target-60%20FPS%20%7C%200B%2Fframe%20GC-00ff88?style=for-the-badge)]()
[![License](https://img.shields.io/badge/License-Commercial%20Anti--Theft-red?style=for-the-badge)](LICENSE.md)
[![Status](https://img.shields.io/badge/Status-In%20Development%20(V0%20Vertical%20Slice)-orange?style=for-the-badge)]()

> **AA Deep Sea Noir / NASA-Punk 3D survival game on Unity 6000.4 URP — targeting 60 FPS with zero GC allocation across 2GB VRAM handhelds to Ultra PCVR.**

[🌊 Wishlist](#) · [📖 Devlog](#) · [🐛 Report Bug](../../issues)

</div>

---

## 📖 About

**HECTON-8** is an AA single-player survival game set in the abyssal depths of an alien ocean on a NASA-punk research platform descending into the unknown. Think *Subnautica*-level environmental fidelity meets deep-sea noir mystery and Soviet-era engineering brutalism.

The game is built on a radical engineering philosophy: **Subnautica-level visual quality with zero GC allocation in gameplay hot paths**, running on hardware as lean as 2GB VRAM.

---

## ⚙️ Technical Architecture

```
Performance Budget (hard ceiling):
├── Frame Budget:     16.67 ms (60 FPS)
├── Main Thread:      ≤ 12 ms
├── GC Allocation:    0 B/frame (gameplay hot paths)
├── SetPass Calls:    ≤ 600
├── Draw Batches:     ≤ 1800
├── VRAM (Compact):   ≤ 1800 MB
└── Texture Budget:   ≤ 900 MB

Scalability:
└── Continuous GlobalQualityWeight [0.0 → 1.0]
    ├── 0.0  — 2GB VRAM handheld survival mode
    ├── 0.5  — Mid-tier 1080p balanced
    └── 1.0  — Ultra 4K PCVR overkill mode
```

---

## 🌊 Core Systems

| System | Technology |
|---|---|
| Terrain & World | MapMagic 2 + custom bridge, procedural biomes |
| Ocean Rendering | Crest Ocean System + custom URP shaders |
| Memory | `NativeArray`, `GlobalDataVault`, zero managed heap in hot paths |
| Compute | Unity Burst Compiler, Job System, BRG indirect rendering |
| Save System | Binary checksummed delta saves, `ISaveable` registration |
| Audio | Spatial audio pools, ADPCM SFX, Vorbis Q70 ambient |
| Streaming | Addressables tracked handles, no fire-and-forget |
| Event Bus | Unmanaged `SignalBus<T>`, NativeQueue bridge lanes |

---

## 🎮 Current Milestone: V0 Vertical Slice (First 20 Minutes)

The active development target is a self-contained vertical slice covering the player's first 20 minutes: awakening, orientation, first dive, first discovery, and first threat encounter.

---

## 📜 License

Protected under **HECTON-8 Commercial Anti-Theft & Source-Available License**.
Maintainers, contributors, and AI research tools are welcome. Commercial use requires a written agreement.

See [LICENSE.md](LICENSE.md) · Copyright (c) 2026 Adolf Petushkov.

---

<details>
<summary>🇷🇺 Русская Версия</summary>

**HECTON-8** — AA игра выживания в жанре Deep Sea Noir / NASA-Punk на Unity 6000.4 URP. Цели: 60 FPS, ноль GC-аллокаций в горячих путях, масштабирование от 2GB VRAM до Ultra PCVR.

Текущий этап: вертикальный срез (первые 20 минут игрового опыта).

</details>

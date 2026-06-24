# HECTON-8 — Deep Sea Sci-Fi Survival Game (NASA-Punk / Deep Sea Noir)

[![Unity](https://img.shields.io/badge/Unity-6000.4_URP-black.svg?style=flat&logo=unity)](https://unity.com/)
[![C#](https://img.shields.io/badge/Language-C%23-blue.svg?style=flat&logo=c-sharp)](https://learn.microsoft.com/en-us/dotnet/csharp/)
[![Platform](https://img.shields.io/badge/Platform-Windows_/_Steam_Deck_/_XR-green.svg?style=flat)](https://github.com/marko1olo/Hecton8)
[![License](https://img.shields.io/badge/License-Proprietary-red.svg?style=flat)](LICENSE)

**HECTON-8** is an immersive, atmospheric **Deep Sea Sci-Fi Survival & Salvage Game** built using **Unity 6000.4 (Universal Render Pipeline)**. Set in the cold, twilight depths of the ocean planet Aegir, the game combines hard-science industrial machinery (NASA-Punk) with a tense, atmospheric corporate-noir mystery.

---

## 🕹️ Core Game Mechanics & Experience

### 🌊 1. Twilight & Abyss Survival
- **Depth-Based Gameplay**: 
  - **0–100m**: Bright, colourful alien shallows filled with dense bioluminescent flora and fauna (inspired by the beauty floor of Subnautica).
  - **200–400m**: Twilight zone where light fades and silhouettes, navigation instruments, and sonar cues become vital.
  - **400m+**: Complete abyss where player-visible details rely entirely on headlights, sonar, and active sensor suites.
- **Physical Survival**: Manage strict, real-time oxygen reserves, hydrostatic pressure limits, temperature gradients, and environmental contaminants.

### 🛠️ 2. Industrial Salvage (Marauder Operations)
- **NASA-Punk Aesthetic**: Heavy, pressure-rated, functional, and bolted industrial machinery. Operating retro-futuristic submersibles and maintenance rigs.
- **Grid-Based Inventory**: A tactical inventory layout that requires spatial management of salvaged materials and equipment.
- **Wreck Exploration**: Board and salvage abandoned deep-sea research facilities, pipeline channels, and cargo hulls.

### 🧭 3. Sonar & Navigation
- Navigate pitch-black trenches and complex cave networks using active sonar maps, depth metrics, and acoustic telemetry.

---

## 📐 Project Architecture & Systems

HECTON-8 is engineered for performance, moddability, and cross-platform scalability:
- **Clean Logic Separation**: Gameplay state (oxygen, inventory, pressure, construction) is strictly separated from presentation-only systems (lighting, post-effects, haptics).
- **Zero-GC Hot Paths**: Update loops, input handling, and event dispatching (`SignalBus<T>`) allocate **0 Bytes/frame** to guarantee a constant framerate.
- **Continuous Scalability**: The engine dynamically reads a unified `GlobalQualityWeight` value to scale visuals and simulation density seamlessly from low-end handhelds (Steam Deck) to high-end PCVR rigs.
- **Modular Assembly Structure**:
  - **Hecton8.Core**: Base systems, event dispatchers, save state, and global registry.
  - **Hecton8.World**: Terrain mesh generation, spline tools, and bioluminescent pulse systems.
  - **Hecton8.Input**: Input mappings and device routing.

---

## 📂 Repository Structure

- **/Assets**: Core Unity project files, scenes, shaders, materials, and prefabs.
- **/ProjectSettings**: Unity project configurations.
- **/Docs**: Comprehensive system bibles, specifications, and development rules:
  - `water.md`: Crest Ocean integration and rendering standards.
  - `terrain.md`: MapMagic spline, mesh generation, and splatmap rules.
  - `persistence.md`: Checksummed binary save game layout.
  - `audio.md`: Spatial audio acoustic DSP pipeline rules.

---

## 🚀 Getting Started

### 📋 Prerequisites
- **Unity Hub** and **Unity Editor 6000.4.x URP** installed.
- **.NET SDK 8.0/9.0** for C# compilation checks.

### ⚙️ Setup
1. Clone the repository:
   ```bash
   git clone https://github.com/marko1olo/Hecton8.git
   ```
2. Open the project root folder in the Unity Hub.
3. Select Unity Editor version **6000.4** and launch.
4. Load the bootstrap scene: `Assets/_Project/Scenes/00_BOOTSTRAP.unity`.

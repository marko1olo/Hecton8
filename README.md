<div align="center">

# 🌊 HECTON-8 — NASA-Punk Deep Sea Noir 3D Submarine Simulation

[![Live Showcase](https://img.shields.io/badge/Live_Showcase-GitHub_Pages-38bdf8?style=for-the-badge&logo=github)](https://marko1olo.github.io/Hecton8/)
[![Unity](https://img.shields.io/badge/Unity-6000.0-black?style=for-the-badge&logo=unity)](https://unity.com/)
[![C#](https://img.shields.io/badge/C%23-13.0_.NET_9-purple?style=for-the-badge&logo=csharp)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-Proprietary-red?style=for-the-badge)](LICENSE.md)

**A high-fidelity immersive submarine simulation exploring the hadal abyssal trenches of Europa. Built with bespoke hydrodynamic physics, acoustic sonar raytracing, and diegetic analog instrumentation.**

</div>

---

## 🎮 Vision & Gameplay Pillars

1. **Diegetic NASA-Punk Cockpit:** Every gauge, cathode-ray tube display, toggle switch, and circuit breaker exists physically in the 3D cockpit. Zero floating 2D UI fluff.
2. **True Hydrodynamic Simulation:** Six-degrees-of-freedom submarine kinematics with ballast tank buoyancy physics, ocean thermal stratification, and hull compression stress.
3. **Acoustic Raytracing Sonar:** Active and passive bathymetric sonar simulating acoustic wave propagation, Doppler shifts, thermocline refraction, and benthic reverberation.
4. **Benthic Ecosystem & Pressure Hazards:** Dynamic hydrothermal vent fauna, bioluminescent abyssal predators, and crushing 1,100-atmosphere depths.

---

## 🏗️ System Architecture

```mermaid
graph TD
    A[Unity 6 Engine Core] --> B[Presentation Layer: Diegetic Cockpit Canvas]
    A --> C[Simulation Domain: ECS & Native Burst Jobs]
    A --> D[Audio DSP: Sonar & Hydrophone Synthesis]
    
    C --> C1[Hydrodynamic Kinematics Solver]
    C --> C2[Acoustic Sonar Raytracer]
    C --> C3[Submarine Reactor & Power Grid]
    C --> C4[Hadal Biome Procedural Streamer]
    
    D --> D1[Active Ping Transceiver Node]
    D --> D2[Passive Cavitation FFT Analyzer]
```

---

### 👥 Engineering Syndicate
Developed and maintained by **Жирняк** & **Адольф Петушков**.

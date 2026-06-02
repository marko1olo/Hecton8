# Static Runtime Risk Category Matrix

Status: CATEGORY COUNTS ONLY - NOT CONFIRMED DEFECTS
Date: 2026-06-02

These counts are derived from `_scans/*_runtime_risks.txt`. A single line can match more than one category. Every non-zero count is a review target, not automatic guilt.

| System | Runtime Suspects | Top Categories |
|---|---:|---|
| Mandate Registry Currency and Routing | 0 | none in configured scan |
| Project Routes, Taste, Quality, Agent Entry | 0 | none in configured scan |
| Generated Meshes, Textures, Materials, LOD, Collision | 249 | Native allocation or persistent lifetime: 177; Runtime debug logging: 104; Runtime mesh/material mutation: 13; Unity scene lookup: 10; Hot Unity phase method: 2 |
| UI, Menus, HUD, Terminals, Localization, Settings | 241 | Runtime debug logging: 121; Runtime mesh/material mutation: 59; Native allocation or persistent lifetime: 49; Unity scene lookup: 31 |
| Rendering, Shaders, Lighting, VFX, Water Presentation | 109 | Native allocation or persistent lifetime: 92; Runtime debug logging: 51; Runtime mesh/material mutation: 1 |
| Runtime Architecture, Data, Bootstrap, Telemetry, Performance | 275 | Runtime debug logging: 178; Native allocation or persistent lifetime: 98; Unity scene lookup: 10; Job fence / sync wait: 3; Hot Unity phase method: 1 |
| Physics, Vehicles, Pressure, Water Truth, Survival Physiology | 281 | Native allocation or persistent lifetime: 181; Runtime debug logging: 95; Runtime mesh/material mutation: 39; Unity scene lookup: 8; Hot Unity phase method: 3 |
| World, Terrain, Voxels, Geology, Ecosystem, Celestial | 254 | Native allocation or persistent lifetime: 170; Runtime debug logging: 102; Unity scene lookup: 14; Runtime mesh/material mutation: 13; Hot Unity phase method: 2 |
| Gameplay, Tools, Construction, Inventory, Combat, Economy | 193 | Runtime debug logging: 99; Native allocation or persistent lifetime: 82; Unity scene lookup: 22; Coroutine / managed timing: 2; Runtime mesh/material mutation: 1 |
| AI, Creatures, Sonar, Drones, Navigation | 70 | Runtime debug logging: 43; Native allocation or persistent lifetime: 23; Unity scene lookup: 9; Runtime mesh/material mutation: 3 |
| Audio, Narrative, PDA, Cinematics, Public Text | 149 | Runtime debug logging: 75; Native allocation or persistent lifetime: 70; Runtime mesh/material mutation: 23; Unity scene lookup: 7 |
| Persistence, Streaming, Release, Platform, Modding, Testing | 126 | Native allocation or persistent lifetime: 85; Runtime debug logging: 76 |

## Review Method

1. Open the system `REPORT.md` and full `_scans/*_runtime_risks.txt` list.
2. Mark each line as legal cold path, legal editor/dev-only path, guarded diagnostic path, or runtime violation.
3. Runtime violations must be fixed before Unity/profiler proof can upgrade the system beyond yellow.
4. Legal editor/tool paths should be moved under `Editor/`, wrapped in `#if UNITY_EDITOR`, or documented in the system report if the path name is ambiguous.

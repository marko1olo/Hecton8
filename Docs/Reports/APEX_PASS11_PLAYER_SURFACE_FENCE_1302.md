# APEX Pass 11 - Player Preprocessor Surface Fence 1302

## Scope

- Prompt: Docs/Reports/PROMPT_1302_REEXTRACTED_PASS11.txt
- Task count: 20
- Domain: Assets/_Project/Scripts/Physics, excluding Tether/Cable/Harpoon tension ownership lanes.
- Build/dotnet: not launched. Last CPU probe: 50%; user ordered rare builds and this pass used static player-preprocessor proof.

## Source Changes

- Guarded editor-only CSV scratch constants and scratch BufferID constants behind UNITY_EDITOR in:
  - Buoyancy/AnalyticalGerstnerWaveContracts.cs
  - Buoyancy/AsyncReadback/AsyncBuoyancyReadbackContracts.cs
  - Buoyancy/BuoyancyDisplacementContracts.cs
  - Cavitation/AbyssalCavitationContracts.cs
  - Seaglide/SeaglideHydrodynamicsContracts.cs
  - Vehicles/SubmarineBallastBuoyancyContracts.cs
  - Vehicles/VehicleComponentDamageContracts.cs
- Guarded PhysicsCulling editor-only CSV/legacy binary file probing and scratch vault buffers behind UNITY_EDITOR in GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs.
- Player PhysicsCulling now initializes tuning from deterministic emergency/mock defaults instead of probing Docs/Archive or StreamingAssets with managed Directory/FileStream APIs.

## Static Evidence

- Touched player-preprocessor scan: Docs/Reports/PLAYER_PREPROCESSOR_SURFACE_SCAN_1302_PASS11.json
  - files scanned: 32
  - blocking player file/path/CSV scratch hits: 0
  - bridge residual hits: 2
- All-domain player-preprocessor scan: Docs/Reports/PLAYER_PREPROCESSOR_SURFACE_SCAN_1302_PASS11_DOMAIN.json
  - files scanned: 48
  - residual hits: 13
  - residual ownership: GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:2 uses System.IO for existing root BinaryWriter blackbox bridge; HarpoonTensionSolver328.cs is Harpoon/tension ownership excluded from 1302.
- Added-line player-active forbidden token scan: Docs/Reports/PATCH_ADDED_LINES_TOKEN_SCAN_1302_PASS11.json
  - raw hits: 1
  - player-active hits: 0

## Release Honesty

- Fixed in 1302 active surface: player file/path probing and CSV scratch vault registration are gone from touched Physics player surface.
- Not fixed in this pass: root/global managed blackbox dump bridge still depends on managed BinaryWriter ownership outside the local Physics scratch route. Harpoon tension dump writer remains excluded by current 1302 boundary.
- DTO layout changed: no. Pass 10 DTO map remains authoritative: Docs/Reports/DTO_OFFSET_MAP_1302_PASS10_TARGETS.json.
- AUP arithmetic changed: no. Pass 10 AUP cast scan remains authoritative: Docs/Reports/AUP_CAST_SCAN_1302_PASS10.json.

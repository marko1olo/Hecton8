# APEX Pass 12 - Bridge Relocation / Player Surface

Agent: 1302  
Domain: `Assets/_Project/Scripts/Physics` excluding Tether/Cable lanes  
Prompt: `Docs/Tasks/CURRENT_BATCH.md` / `<AGENT_PROMPT id="1302">`  
Task count: 20

## Source Changes

- `Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs`
  - `System.IO` is editor-only.
  - The player-compiled Physics partial no longer contains the `BinaryWriter` culling dump helper.
- `Assets/_Project/Scripts/GlobalPhysicsStateManager.cs`
  - Root/global owner now contains `WriteShinobu37PhysicsCullingFrameDump(BinaryWriter writer)` at line 3340.
  - Existing root dump bridge remains managed `FileStream`/`BinaryWriter` at lines 3316 and 3340. This is outside the strict Physics folder player-preprocessor surface and still needs a Core/native dump bridge to be literal native-only.
- `Assets/_Project/Scripts/Physics/HarpoonTensionSolver328.cs`
  - `System.IO`, `System.Reflection`, `System.Text`, layout reflection, XML audit builder, and dump writer are editor-only.
  - Player `TryDumpTelemetryIfFault(...)` is fail-closed and returns `false` without file IO or managed diagnostics.
- `Assets/_Project/Scripts/Physics/HabitatFluidIncursionContracts.cs`
  - Restored compile-critical contract file because DTOs/constants were still referenced by `HabitatFluidIncursionJobs.cs` and `HabitatFluidIncursionDirector.cs`.
  - Layout validator reflection narrowed from `UNITY_EDITOR || DEVELOPMENT_BUILD` to `UNITY_EDITOR`.
- `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime_Gyroscopes.cs`
  - `MaxGyroProfileCsvBytes` is editor-only; player release scan no longer sees CSV scratch capacity.

## Static Proof

- `Docs/Reports/PLAYER_PREPROCESSOR_SURFACE_SCAN_1302_PASS12.json`
  - 34 touched Physics files.
  - Blocking IO/path/CSV scratch hits: 0.
  - Bridge hits: 0.
- `Docs/Reports/PLAYER_PREPROCESSOR_SURFACE_SCAN_1302_PASS12_DOMAIN.json`
  - 50 domain files.
  - Blocking IO/path/CSV scratch hits: 0.
  - Bridge hits: 0.
- `Docs/Reports/MANAGED_RISK_PLAYER_SURFACE_SCAN_1302_PASS12.json`
  - 34 touched files, 50 domain files.
  - `System.Text`, `System.Reflection`, `StringBuilder`, `ToString()`, `string.Format`, `System.Linq`, `Enumerable`, string concat, `catch`, `throw new`, `Activator`, `BindingFlags`, likely managed array allocation hits: 0 in release player surface.
- `Docs/Reports/DTO_OFFSET_MAP_1302_PASS12_TARGETS.json`
  - 50 files, 100 explicit-layout structs.
  - Size multiple-of-8 violations: 0.
  - Bool fields in DTO layouts: 0.
- `Docs/Reports/AUP_CAST_SCAN_1302_PASS12.json`
  - Candidate AUP/double-to-float casts: 8.
  - Possible absolute AUP float casts: 0.
- `Docs/Reports/DEPENDENCY_USING_AUDIT_1302_PASS12.json`
  - `System.Linq`: 0.
  - Modified `.asmdef`: 0.
  - Direct `Hecton8.World` using hits: 12 existing AUP/value-type contract surfaces, not new gameplay-domain links.
- `Docs/Reports/PREPROCESSOR_BALANCE_SCAN_1302_PASS12.json`
  - 35 touched source files including root global bridge file.
  - Bad preprocessor balances: 0.

## Fail-Closed / Boundary

- Harpoon player dump path is now closed by construction: `return false` under non-editor compilation.
- PhysicsCulling player tuning path uses deterministic generated fallback; editor keeps authored file probes.
- No local Physics P/Invoke or native file writer was introduced.
- Residual native-only dump gap is still Core/global: root `GlobalPhysicsStateManager.cs` and Core `GlobalTelemetryBus` must own the final unmanaged writer route.

## Build

No dotnet, Unity compile, or rebuild was launched in this pass. Reason: user explicitly ordered rare build/dotnet usage, and this pass is covered by static/preprocessor/source-level evidence.

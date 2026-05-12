# HECTON8_BIOS_REGISTRY Log

## 2026-05-11 - Bootstrap Contract Gateway / Math LOD Compile Recovery

Status: PENDING VERIFICATION

What was wrong:
- BIOS/Core compile evidence was unreliable because an incremental project-reference build emitted DLLs while a stricter Core-only pass initially exposed transient source errors from concurrent edits.
- The BIOS scalability path needed one registry-owned Math LOD decision instead of per-system hardware guessing.
- Missing `.meta` files were a known Unity assembly discovery risk for first-party scripts.

What was done:
- Confirmed local strict compile gates:
  - `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false /p:BuildProjectReferences=false /m:1 /nr:false -v:minimal` -> 0 warnings, 0 errors.
  - `dotnet build Assembly-CSharp.csproj --no-restore /p:UseSharedCompilation=false /p:BuildProjectReferences=false /m:1 /nr:false -v:minimal` -> 0 warnings, 0 errors.
  - `dotnet build Hecton8.Editor.csproj --no-restore /p:UseSharedCompilation=false /p:BuildProjectReferences=false /m:1 /nr:false -v:minimal` -> 0 warnings, 0 errors.
- Confirmed `SceneBootstrap.cs` is absent; only unrelated `WorldLODSceneBootstrap.cs` remains.
- Confirmed `GlobalRegistryServiceSlot.ToString()` is absent from Core/Bootstrap. Ghost-service reporting resolves deterministic slot names through `ResolveServiceSlotName(index)`.
- Added/validated BIOS hardware profile Math LOD:
  - `GameBootstrapper.CaptureHardwareProfile()` reads `SystemInfo.graphicsMemorySize`, `SystemInfo.systemMemorySize`, `SystemInfo.processorCount`, and BIOS physics benchmark.
  - `MathPrecisionLevel` is stored in `HectonHardwareProfile`.
  - `GlobalRegistry` exposes current/target Math LOD and a 60-frame degradation transition.
  - `SystemDispatcher.Update()` advances Math LOD transition once per frame.
  - shader keywords `_MATH_LOD_LOW` and `_MATH_LOD_HIGH` are warmed/applied by BIOS/registry.
- Added editor-only `MetaFileGenerator.cs` under `Assets/_Project/Scripts/Editor/Build/`.
  - Scans first-party `Assets/_Project/Scripts`.
  - Skips `Assets/_Project/Scripts/Plugins/`, `Assets/_ThirdParty/`, and `Assets/Plugins/`.
  - Imports scripts first and writes a minimal MonoImporter `.meta` only when Unity does not create one.

Cinematic cheats used:
- No new physical simulation was replaced in this pass. This was compile recovery plus BIOS Math LOD control-plane work.
- Installed cheat selector:
  - Low/MX350 path: global `_MATH_LOD_LOW`, intended for dominant-axis/approximate math consumers.
  - High/Ultra path: global `_MATH_LOD_HIGH`, intended for true-normal/visual-overkill consumers.

Estimated microseconds saved:
- Direct compile/meta work: 0 us runtime.
- Fatal boot crash log builder reuse: estimated 2-8 us and one managed allocation avoided on fatal cold path only.
- Math LOD global switch: estimated 8-40 us/frame on i3/MX350 after shader/simulation consumers honor `_MATH_LOD_LOW`.
- SceneBootstrap split-brain removal: exact bytes/loop savings still require Unity Profiler evidence; static scan confirms target file absence only.

Final Git Diff:
- Modified:
  - `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`
  - `Assets/_Project/Scripts/Core/GlobalRegistry.cs`
  - `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs`
  - `Assets/_Project/Scripts/Core/SystemDispatcher.cs`
- Added:
  - `Assets/_Project/Scripts/Editor/Build/MetaFileGenerator.cs`
  - `Assets/_Project/Scripts/Editor/Build/MetaFileGenerator.cs.meta`
  - `Docs/Tasks/Status_HECTON8_BIOS_REGISTRY.md`
  - `Docs/AgentLogs/Rationale_HECTON8_BIOS_REGISTRY.md`
  - `Docs/AgentLogs/LOG_HECTON8_BIOS_REGISTRY.md`
- Diff stat for tracked BIOS/Core files:
  - `GameBootstrapper.cs`: +140/-1 approximate local diff.
  - `GlobalRegistry.cs`: +54/-0 approximate local diff.
  - `GlobalRegistryContracts.cs`: +8/-1 approximate local diff.
  - `SystemDispatcher.cs`: +2/-0 approximate local diff.

Open verification debt:
- Unity Editor Console, Play Mode, scene wiring, GC/frame-time, and profiler evidence were not available through callable MCP tools in this session.
- Status remains PENDING VERIFICATION by AGENTS.md.

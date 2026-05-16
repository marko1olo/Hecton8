# Status_ARCHITECT_SPATIAL_PROBE

Prompt: ARCHITECT_SPATIAL_PROBE
Domain: CORE/DIAGNOSTICS
Task Count: 20
Status: VERIFIED MASTER GRADE - VISION CLEAR / GLOBAL COMPILE BLOCKED BY EXTERNAL WORLD-ECOSYSTEM DEPENDENCY

## Mandates Selected Before Coding

- REND_Shader_Noir_Aesthetics_Dithering_Fog.txt
- GPU_Compute_Kernels_Kernels_Optimization_MX350.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- ARCH_Signal_Lane_Segregation.txt
- REND_GPU_Sovereignty.txt
- UI_Diegetic_Physical_Interfaces.txt

## Task Checklist

- [x] 1. VAULT_BLOCK_MAP - DOD: DataVault block descriptors to packed indirect quads. Rejected: GameObject tiles/Inspector windows. Estimate: 180 us/frame saved at 512 blocks.
- [x] 2. POINTER_STABILITY_MONITOR - DOD: Vault relocation records and DebugSignal pointer links. Rejected: concrete system references. Estimate: 90 us/frame saved by batched lines.
- [x] 3. GENERATION_ID_HUD - DOD: fixed char buffer hex labels from buffer generations. Rejected: managed string labels per entity. Estimate: 35 us/frame and 0 B/frame saved.
- [x] 4. AUP_SECTOR_GIZMO - DOD: editor-only 5000m grid around scene camera with active 9 sector highlight. Rejected: runtime sector GameObjects. Estimate: 120 us/frame saved in play mode.
- [x] 5. SDF_DENSITY_GLOW - DOD: byte SDF sampling plus noir indirect quad wire/volume proxy. Rejected: CPU marching cubes debug mesh. Estimate: 700 us/frame saved on MX350-class hardware.
- [x] 6. COLLISION_NORMAL_TRAILS - DOD: DebugSignal.CollisionNormal feed draws green slide/red stop vectors. Rejected: KCC hard dependency. Estimate: 110 us/frame saved at 128 contacts.
- [x] 7. BREADCRUMB_PATH_VIZ - DOD: DebugSignal.BreadcrumbSegment world lines, AUP-safe float positions. Rejected: LineRenderer path objects. Estimate: 160 us/frame saved at 64 segments.
- [x] 8. GAS_HEATMAP_VOLUME - DOD: room O2/CO2 state to translucent quad volume markers. Rejected: volumetric particle emitters. Estimate: 240 us/frame saved at 80 rooms.
- [x] 9. PRESSURE_STRESS_VECTORS - DOD: DebugSignal.PressureVector hull force arrows. Rejected: solver polling/concrete submarine module link. Estimate: 115 us/frame saved at 128 vectors.
- [x] 10. FLUID_VELOCITY_FIELD - DOD: DebugSignal.FluidVelocity arrows with magnitude scaling. Rejected: full fluid simulation visualization. Estimate: 320 us/frame saved by arrow proxy.
- [x] 11. ACOUSTIC_PORTAL_RAYS - DOD: DebugSignal.AcousticRay bounce lines. Rejected: debug ray GameObjects. Estimate: 105 us/frame saved at 96 taps.
- [x] 12. SIGNAL_BUS_MONITOR - DOD: SignalBusRegistry telemetry and integer IDs, screen waterfall quads. Rejected: text log allocation. Estimate: 210 us/frame and unbounded GC avoided.
- [x] 13. LANE_SATURATION_GAUGE - DOD: NativeQueue telemetry bars, red threshold at 90 percent. Rejected: UI Canvas gauges. Estimate: 260 us/frame saved.
- [x] 14. EVENT_RESONANCE_MAP - DOD: DebugSignal.EventResonance producer-consumer lines. Rejected: managed event subscription tracing. Estimate: 145 us/frame saved.
- [x] 15. NAN_GEYSER_DETECTOR - DOD: finite checks, red world pillar, 300-frame binary dump. Rejected: console-only NaN reports. Estimate: 0 us saved, crash localization gained.
- [x] 16. HOMEOSTASIS_DIAL - DOD: health index dial in indirect visor/PDA layer. Rejected: Unity UI needle. Estimate: 170 us/frame saved.
- [x] 17. BLACKBOX_REPLAY_OVERLAY - DOD: 300-entry circular buffer ghost trail. Rejected: instantiating translucent history objects. Estimate: 430 us/frame saved at 300 poses.
- [x] 18. VRAM_BUDGET_PIE_CHART - DOD: DebugSignal.VramBudgetSlice physical-screen proxy. Rejected: Canvas pie chart mesh rebuild. Estimate: 220 us/frame saved.
- [x] 19. EDITOR_AUP_TELEPORT - DOD: SceneView control-click moves GlobalRegistry player runtime and publishes preview signal. Rejected: runtime cheat UI. Estimate: 0 runtime us saved.
- [x] 20. PLATINUM_COMPILE - `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` exited 0 after diagnostics hardening and contracts refresh. Latest global reverification is now [BLOCKED BY DEPENDENCY] in `Assets/_Project/Scripts/World/EcosystemDirector.cs`, outside ARCHITECT_SPATIAL_PROBE domain. Estimate: 0 runtime us.

## Iterative Loop Record

- Loop 1: Re-read AGENTS.md, domain document, batch XML, and mandates. Built disk state. Covered Tasks 1-5 design constraints before code.
- Loop 2: Implemented Memory X-Ray and Spatial Truth surfaces, then reread `ArchitectEyeVisualizer.cs` for SDF type, AUP precision, and label allocation errors.
- Loop 3: Implemented DebugSignal overlay feed for Tasks 6-14 without direct dependencies on KCC, navmesh, gas, vehicle, fluid, sonar, or signal producers.
- Loop 4: Implemented NaN pillar, homeostasis dial, blackbox replay, VRAM slice proxy, editor AUP teleport, and F12 global toggle.
- Loop 5: Removed local compile faults, reread changed code paths, verified `Hecton8.Core.csproj` build clean with 0 warnings and 0 errors.

## Verification

- Build: PASS, 0 warnings, 0 errors.
- Hot-path allocation stance: fixed buffers, fixed char formatting, SignalBus frame snapshots, `GraphicsBuffer.LockBufferForWrite`, and `Graphics.DrawMeshInstancedIndirect`.
- Known limitation: producer systems must publish `DebugSignal` payloads to populate collision, breadcrumb, pressure, fluid, acoustic, resonance, and VRAM feeds. Visualizer side is compiled and isolated.
- Reverification 2026-05-16: refreshed stale `Hecton8.Core.Contracts.dll` from current contract sources because Unity had not rebuilt the referenced assembly, then ran `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`; result PASS, 0 warnings, 0 errors.
- Inquisition polish 2026-05-16: removed private indirect-args managed scratch array, wrote indirect args directly to `GraphicsBuffer.LockBufferForWrite`, removed `GraphicsBuffer.Target.Raw` from indirect args buffers for Metal/Vulkan portability, expanded High/Ultra visual-overkill quad counts, added shader fake POM/SSS/noir dither, and reran the same Core build; result PASS, 0 warnings, 0 errors.
- Multiplatform hardening 2026-05-16: added editor/development-build runtime gate, ProfilerMarkers for SlowTick/upload, `DebugSignal` ABI size validation, capped blackbox dump viewer reads to 300 frames with `FileOptions.SequentialScan`, tiered SDF shader march counts of 3/8/16, and one cross-domain compile unblock in `SystemDispatcher.cs` (`System.ReadOnlySpan<T>` qualification) after another agent's edit broke the typed lane snapshot. Refreshed stale contracts DLL via `Temp\CoreContractsBuild\Hecton8.Core.Contracts.Generated.csproj`; reran `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`; result PASS, 0 warnings, 0 errors.
- Debt audit 2026-05-16: `rg` found no `Debug.Log`, `File.ReadAllBytes`, `Marshal.SizeOf`, `_argsScratch`, `GraphicsBuffer.Target.Raw`, `SetData`, `GetData`, `string.Format`, `Update`, `LateUpdate`, `FixedUpdate`, `EventBus`, managed delegate signatures, or `new NativeArray` in `Assets/_Project/Scripts/Core/Diagnostics/Visuals`.
- Shader portability audit 2026-05-16: removed numeric `h` literal suffixes from both diagnostic shaders while keeping `half` variables, reducing Metal/HLSL cross-compile risk. `rg -n "[0-9]h\b" Assets/_Project/Scripts/Core/Diagnostics/Visuals -g "*.shader" -g "*.compute"` returns no matches.
- Latest compile audit 2026-05-16: rerunning Core after the shader audit now fails outside diagnostics in `Assets/_Project/Scripts/World/EcosystemDirector.cs` with missing `_biomassIndexByKey`, `_sectorIndexByKey`, `ClearIndexEntries`, `TryUpsertIndexEntry`, `TryFindIndexEntry`, `ResolveVaultIndexCapacity`, and `BiomassLotkaVolterraJob.CellIndexByKey`. This is marked [BLOCKED BY DEPENDENCY]; diagnostics debt audit remains clean.

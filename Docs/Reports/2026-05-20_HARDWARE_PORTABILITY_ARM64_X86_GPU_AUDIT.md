# 2026-05-20 Hardware Portability Audit - ARM64, x86, GPU Matrix

Agent: HFI_AUDIT  
Domain: Architecture / Platform Portability / Hardware Readiness  
Scope: static source, settings, docs, and audit-tool review only. No Unity
import, dotnet build, player build, profiler capture, RenderDoc capture,
headset run, Steam Deck run, macOS run, or console SDK proof was launched.

## Executive Verdict

The project is moving in the correct architectural direction, but it is not yet
proven adapted to ARM64, x86, Quest, Steam Deck, Mac, PICO, consoles, or the
full GPU matrix.

Current state is best described as: strong portability scaffold, weak runtime
proof. The codebase has real platform-aware foundations: Android ARM64 IL2CPP,
Vulkan configuration, Burst, `Pack=1` hard gate at zero, hardware detection,
GlobalQualityWeight, dynamic render scale pressure, VRAM pressure, foveation
state, GraphicsBuffer upload paths, indirect rendering, and HZB/depth-pyramid
culling. That is not fake work.

The hard problem is that these foundations are ahead of the evidence. There are
no player-build artifacts, no headset logs, no profiler traces, no shader
warmup proof, no Steam Deck/Linux proof, no Mac/Metal proof, no PICO package,
and no console integration proof. On mobile/standalone VR, the risk is not
syntax or compilation; the risk is frame budget, memory ownership, shader
stutter, compute dispatch shape, and quality-profile wiring.

## Platform Scores

These are static-readiness scores, not runtime-performance scores.

| Target | Score | Verdict |
| --- | ---: | --- |
| Windows x86_64 / mid-high PC | 5 / 10 | Best-covered target conceptually; still lacks current player/profiler proof. |
| Weak x86 / MX350 class | 4 / 10 | VRAM/render-scale policy exists; no capture proving stable frame budget. |
| Quest 3 ARM64 / standalone VR | 2.5 / 10 | Android ARM64 IL2CPP scaffold exists; XR/provider/runtime proof absent. |
| Quest 2 ARM64 / standalone VR | 2 / 10 | Needs stricter survival path than current low quality profile appears to provide. |
| Steam Deck / Linux Vulkan | 2 / 10 | Detection exists; Linux/Vulkan build, shader, and controller proof absent. |
| macOS / Apple Silicon / Metal | 2 / 10 | Metal detection exists; macOS player and compute/shader proof absent. |
| PICO standalone VR | 1 / 10 | No PICO package candidate found. |
| Consoles | 1 / 10 | Architecture may be compatible later; there is no platform integration proof. |
| High-end RTX visual overkill | 4 / 10 | GPU-driven direction exists; high-tier capture and distinct overkill budget absent. |

## Static Evidence

- `ProjectSettings/ProjectSettings.asset`: Android min SDK `25`, target SDK
  `35`, `AndroidTargetArchitectures: 2`, and Android scripting backend `1`.
  This indicates ARM64-only IL2CPP scaffold.
- `ProjectSettings/ProjectSettings.asset`: `m_BuildTargetVRSettings: []`.
  Serialized XR provider proof is absent.
- `ProjectSettings/ProjectSettings.asset`: Android graphics API is serialized
  as Vulkan-only and automatic API selection is off. This is directionally good
  for Quest, but risky until headset and Vulkan device logs exist.
- `ProjectSettings/ProjectSettings.asset`: `AndroidEnableSustainedPerformanceMode: 0`.
  This is weak for standalone VR thermal behavior.
- `Packages/manifest.json` and `Packages/packages-lock.json`: XR Management,
  OpenXR, Meta OpenXR, URP, Burst, Collections, and Android JNI packages are
  present.
- `Tools/PlatformPortabilityProofAudit.py`: PASS_WITH_WARNINGS. XR packages,
  Android ARM64, and IL2CPP are present. XR provider serialized proof, build
  logs, Addressables payload, Data Monolith, and PICO package are absent.
- `Tools/PolishMandateStaticAudit.py`: PASS_WITH_WARNINGS. Current warning
  surface includes `burstMissingCompileSynchronously=346`,
  `burstMissingFloatMode=41`, `burstMissingFloatPrecision=43`,
  `jobHandleComplete=104`, `privateNativeCollectionField=1353`,
  `binaryHardwareSwitch=103`, and `Pack=1=0`.
- `Tools/DataVaultSovereigntyAudit.py` candidate no-regression gate:
  FAIL_REGRESSION. Runtime file-level gross native ownership growth is `+38`;
  editor/offline-baker growth is `+12`.
- `ProjectSettings/QualitySettings.asset`: Android default quality resolves to
  the low tier, but that low tier points at `URP_Low (PC_RPAsset)`, not the
  existing Quest-specific URP asset.
- `Assets/_Project/Data/URP_Quest_VR.asset`: exists and is better shaped for
  VR, but current static search found no wiring from QualitySettings,
  GraphicsSettings, scenes, or prefabs.

## ARM64 / Quest / Mobile CPU

What is already correct:

- Android is configured for ARM64-only IL2CPP.
- Exact runtime `StructLayout(Pack=1)` is currently zero by static audit.
- AUP and local-space math standards exist in the architecture set.
- `HomeostasisBrain.ScalabilityDictator` and hardware-policy code consume
  device model, system memory, graphics memory, shared-memory pressure, Quest
  class signatures, Steam Deck signatures, and GlobalQualityWeight.
- `HectonXRRuntimeState` tracks XR state, refresh rate, frame interval,
  foveation state, and shader-global publication.
- `OculusFfrEnforcer` contains a Quest/Vulkan candidate policy and TBDR path
  flags.

What is not proven:

- No Quest 2 or Quest 3 player build/install/run artifact exists.
- XR provider serialization is absent in project settings.
- PICO package candidate count is zero.
- Android sustained-performance mode is off.
- Quest-specific URP asset exists but does not appear wired into Android
  quality selection.
- DataVault regression still shows runtime native ownership growth. This is
  dangerous for ARM64 memory pressure, rollback state clarity, and frame-time
  stability.
- Several Burst jobs still miss strict flags. That does not mean they are slow,
  but it means the policy is not enforced uniformly.
- Some compute kernels need mobile/TBDR proof. A 512-thread group in a compute
  shader is especially suspicious for standalone VR until measured.

ARM64 verdict:

The ARM64 direction is real, but the current project is not Quest-ready. It is
at the "configured and architecturally prepared" stage, not the "runs at locked
VR frame budget" stage.

## x86 / Windows / Weak PC

What is already correct:

- Burst package is present and modern enough for the current Unity stack.
- x86 intrinsics are guarded rather than blindly assumed. Static review found
  explicit support checks before SSE/SSE2 paths and fallback paths nearby.
- Hardware detection distinguishes D3D11, D3D12, Vulkan, Metal, Steam Deck-like
  devices, Quest-like devices, shared-memory machines, and graphics memory
  budgets.
- MX350/weak-dGPU pressure is explicitly represented in the hardware policy and
  VRAM/render-scale governor.

What is not proven:

- No current Windows player profiler proof is present.
- No i3/MX350 frame capture or memory capture is present.
- Direct `.Complete()` pressure and private native collection pressure are still
  too high to claim weak-CPU readiness.
- Native plugins are Windows x86_64-centered. Linux/macOS/plugin importer proof
  is missing or weak.
- High-end x86 does not automatically prove low-end x86. The project needs a
  weak-machine capture with fixed scenes and fixed frame budgets.

x86 verdict:

x86 is the best-covered CPU family conceptually. Windows PC is likely the
lowest-friction first runtime target. Weak x86 readiness remains unproven until
the `.Complete()`/native ownership hot surfaces are separated from editor/test
noise and measured on a weak device.

## GPU Matrix

What is already correct:

- GlobalQualityWeight is used widely enough to be a real project pillar, not a
  token variable.
- PlatformAdaptiveBudgetGovernor and dynamic-resolution code react to thermal,
  frame, shared-memory, and VRAM pressure.
- GraphicsBuffer upload utility includes `LockBufferForWrite` paths.
- Indirect rendering and GPU culling exist in vegetation and ecosystem-style
  systems.
- HectonIndirectVegetationRenderer has depth-pyramid/HZB-style occlusion,
  append buffers, indirect args, and GPU readback telemetry.
- Volumetric fog resolves ray steps from quality, with a 4 to 64 step ladder and
  low-quality proxy blend.
- GraphicsSettings keeps SRP batcher, instancing variants, and BRG variants from
  being stripped.

What is not proven:

- Shader warmup is weak relative to project shader surface. Current preloaded
  shader evidence is too small for a project with many shader-feature and
  multi-compile surfaces.
- Vulkan-only Android path has no current headset log or deny/allow-list proof.
- Metal/macOS shader and compute compatibility is not proven.
- Steam Deck/Linux Vulkan shader stutter is not proven.
- Low/Medium/High URP quality assets are not enough as static scalability proof:
  render scale is still `1`, and the low tier appears PC-low rather than
  standalone-VR-low.
- ComputeBuffer and SetData surfaces still exist. Some are cold or acceptable,
  but they need cadence and byte-budget proof before weak GPU/mobile claims.
- AsyncGPUReadback surfaces require cadence proof. Telemetry readback is useful,
  but readback cadence can still damage mobile/Deck frame pacing.

GPU verdict:

The GPU direction is good: GPU-driven, scalable, and increasingly indirect. The
runtime proof is weak: no RenderDoc, Frame Debugger, profiler, shader warmup, or
device capture evidence exists for the target GPU classes.

## Native Plugin Portability

Current critical native plugin candidates:

- `Assets/_Project/Plugins/Windows/x86_64/liblz4.dll`
- `Assets/Plugins/x86_64/HectonAudioKernel.dll`

The code has fallback behavior:

- Save binary storage can fall back when native LZ4 is unavailable.
- The audio native bridge is excluded from Android and has plugin-unavailable
  paths.

The unresolved issue is platform parity:

- Windows x86_64 native plugin presence does not prove Linux, macOS, Android,
  Quest, PICO, or Steam Deck behavior.
- Minimal or missing PluginImporter metadata means the platform inclusion matrix
  is not trustworthy enough.
- Fallback paths must be benchmarked. A fallback that works functionally may
  still break frame or load-time budgets.

## Global Direction

The project is not globally wrong. The decision to introduce global registry,
signals, data vault, quality weight, platform governor, and GPU-driven buffers
is directionally correct for this hardware target matrix.

The current failure mode is different: readiness language is ahead of proof.
The system is building the right kind of skeleton, but the skeleton is not yet
validated under ARM64 thermal limits, weak x86 cache/memory limits, Vulkan
driver behavior, Metal shader behavior, or standalone-VR frame cadence.

## Priority Fixes

1. Wire Android/Quest quality to the Quest-specific URP asset or create a
   dedicated `URP_StandaloneVR_Low` asset and prove it is selected for Android
   Quest builds.
2. Serialize OpenXR provider configuration so project settings contain real XR
   provider proof.
3. Produce a minimal Quest 3 proof ladder: build log, install/run log, player
   log, profiler frame timing, GC alloc trace, memory trace, shader compile log.
4. Produce a Steam Deck/Linux Vulkan proof ladder: build log, player log,
   shader warmup/stutter evidence, controller/input proof, frame timing.
5. Produce a macOS/Metal proof ladder before claiming Apple Silicon readiness.
6. Add or explicitly reject PICO integration. Today PICO readiness is
   essentially absent.
7. Turn high-risk compute kernels into platform-aware dispatch contracts. Large
   thread groups need desktop-only proof or mobile-safe variants.
8. Expand shader warmup and variant stripping into a real platform gate.
9. Reduce runtime DataVault regression before mobile/XR claims. The current
   runtime gross growth `+38` is the wrong trend.
10. Separate runtime `.Complete()` and private native collection debt from
    editor/test/cold paths and burn down the runtime slice first.

## Bottom Line

The current project is architecturally pointed at cross-hardware scalability,
but it is not yet adapted in the only sense that matters: measured execution on
the target devices. Keep the global systems. Stop treating settings/package
presence as readiness. The next milestone must be proof artifacts per hardware
class, not more broad claims.

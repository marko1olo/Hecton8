# Status_SHADER_OVERKILL_ARCHITECT

Agent: SHADER_OVERKILL_ARCHITECT
Domain: Rendering / Presentation & UX
Task count: 20
Status: CORE IMPLEMENTED / COMPILE BLOCKED BY EXTERNAL WORLD-GPR DEPENDENCY

## Hygiene
- [x] Session status file initialized | Justification: state-machine checklist required before code edits | Alternative rejected: chat-only progress report | Estimate: 35 us
- [x] Active rationale file initialized | Justification: decision journaling required before marking tasks done | Alternative rejected: final-only rationale dump | Estimate: 35 us
- [x] Registry mandates selected/read | Justification: shader task touches SRP batching, AUP, graphics buffers, fake-first rendering, zero-GC IDs | Alternative rejected: coding from prompt alone | Estimate: 250 us
- [!] Mandatory active logs missing | `Docs/AgentLogs/Rationale_CAUSTICS_PROJECTION.md` and `Docs/AgentLogs/Rationale_MATERIAL_DECAY.md` were not present; implementation will inspect source files and record this as dependency evidence gap.
- [!] Batch prompt extraction gap | `Docs/Tasks/CURRENT_BATCH.md` exists but does not contain `<AGENT_PROMPT id="SHADER_OVERKILL_ARCHITECT">`; this chat XML remains the only exact prompt source.

## Core Checklist
- [x] Task 01: SRP Batcher compatibility via single `UnityPerMaterial` CBUFFER | Justification: all per-material values in one CBUFFER in `Hecton8_UberNoir.hlsl` | Alternative rejected: MaterialPropertyBlock/material clone fragmentation | Estimate: 30-120 us CPU SetPass avoided pending Frame Debugger proof
- [x] Task 02: Native AUP vertex offset before world position math | Justification: instance matrix translation subtracts `_TotalUniverseOffset.xyz` before `mul()` world position math | Alternative rejected: post-transform offset that leaves 100 km float jitter in matrix multiply | Estimate: 0-5 us GPU cost, precision defect removed
- [x] Task 03: GraphicsBuffer instance data binding | Justification: `StructuredBuffer<H8UberNoirInstanceData>` provides matrices and seed/fade/flags under `H8_UBERNOIR_USE_INSTANCE_BUFFER` | Alternative rejected: per-renderer property writes | Estimate: 20-80 us CPU avoided at high instance counts pending RenderDoc/Profiler
- [x] Task 04: Analytical caustics integration | Justification: caustic ALU and optional map sample are folded into lighting, low tier bypasses it | Alternative rejected: separate caustics pass/shader increasing SetPass pressure | Estimate: 30-120 us CPU pass overhead avoided; GPU cost tier-gated
- [x] Task 05: Dynamic hull bending logic | Justification: crush/habitat stress deformation is shader-side vertex bowing using global pressure inputs | Alternative rejected: CPU mesh mutation/skinning path | Estimate: 60-300 us CPU mesh work avoided on deformed hull batches
- [x] Task 06: Rust/corrosion 16-tap POM | Justification: GOD mode uses 16 unrolled rust-detail depth taps with UV refinement and pit tint | Alternative rejected: extra decal/rust shader pass | Estimate: CPU SetPass avoided; GPU cost accepted only above low LOD
- [x] Task 07: Bioluminescent spectral pulse | Justification: `_BiolumMasterPhase` drives spectral lerp emission with material/instance seed variation | Alternative rejected: script-animated material color updates | Estimate: 10-50 us CPU property churn avoided across many materials
- [x] Task 08: Branchless attenuation math | Justification: main light gates use `step()` products and lerp-style attenuation composition | Alternative rejected: fragment `if` ladder on light distance/fog gates | Estimate: 1-10 us GPU divergence avoided, scene-dependent
- [x] Task 09: Blue-noise dithered transparency | Justification: blue-noise cutout clips noir fog alpha without transparent sorting path | Alternative rejected: full alpha blend fog material | Estimate: 40-200 us overdraw/sorting pressure avoided in fog-heavy views
- [x] Task 10: Low-tier stripping block | Justification: `_MATH_LOD_LOW` bypasses POM, caustics, bending, and biolum overkill | Alternative rejected: one balanced middle shader | Estimate: 80-500 us GPU avoided on low-tier material clusters
- [x] Task 11: XR late-latching compatibility | Justification: vertex/fragment paths preserve Unity stereo instance macros | Alternative rejected: custom matrices outside Unity XR macros | Estimate: stability/prediction correctness; microsecond gain not claimed
- [x] Task 12: GPU Resident Drawer compatibility | Justification: immutable shader property IDs, instance buffer path, no per-renderer material mutation in owned code | Alternative rejected: runtime MaterialPropertyBlock edits | Estimate: 20-100 us CPU avoided in resident batches
- [x] Task 13: Zero-GC `H8ShaderIDs` property cache | Justification: static readonly `Shader.PropertyToID` cache created for material/runtime globals | Alternative rejected: string property lookup at runtime hot paths | Estimate: 5-40 us CPU avoided per heavy update burst, zero managed allocs
- [x] Task 14: NaN vaccination for `pow()` and `rsqrt()` | Justification: only `pow()` and `rsqrt()` calls are wrapped by safe helpers with epsilon/abs clamps | Alternative rejected: raw math intrinsics in high contrast lighting | Estimate: correctness guard; cost below measurable threshold
- [!] Task 15: Vulkan/Metal/DX12 compile hygiene [BLOCKED BY DEPENDENCY] | Justification: Unity 6000.4.1f1 batchmode exits on unrelated `GroundPenetratingRadarRuntime.cs` errors before complete import verification | Alternative rejected: editing World/GPR outside domain | Estimate: 0 us owned-code gain; integration blocker documented
- [x] Task 16: Prompt re-read after core tasks | Justification: `CURRENT_BATCH.md` rechecked; this agent XML absent, chat XML retained as source | Alternative rejected: letting neighboring batch prompts drive architecture | Estimate: 35 us process overhead
- [x] Task 17: Texture-stall audit | Justification: ORM `_MaskMap` is sampled once into a struct-equivalent value; rust POM samples only rust detail in GOD path | Alternative rejected: separate metallic/roughness/occlusion texture fetches | Estimate: 10-60 us GPU texture pressure avoided by packed ORM
- [x] Task 18: Five-loop self-review pass | Justification: loops covered mandate read, static shader scan, rust UV fix, abyss-floor parameter fix, Unity compile block triage, and texture-stall audit | Alternative rejected: single-pass unchecked implementation | Estimate: 0 us runtime, prevents integration churn
- [ ] Task 19: Polish mandate parse/execute after core completion | Justification pending | Alternative rejected pending | Estimate pending
- [ ] Task 20: Final log appended | Justification pending | Alternative rejected pending | Estimate pending

## Verification Ledger
- Compile: BLOCKED BY DEPENDENCY - Unity batchmode reports only existing `Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs` missing `Hecton8.World.GPR`, `GroundRadarTelemetryEntry`, and `GroundRadarConstants`.
- Shader static audit: PASS - one `UnityPerMaterial` CBUFFER, one `_MaskMap` sample, guarded `pow()`/`rsqrt()`, balanced braces.
- Unity import/Console: BLOCKED BY DEPENDENCY - owned shader/C# names absent from error scan.
- Frame Debugger/RenderDoc/Profiler: NOT RUN - no runtime scene/clean compile available; microsecond figures are estimates until capture.

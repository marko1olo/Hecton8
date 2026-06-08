# HECTON-8 Mandate Registry

Date: 2026-05-14
Status: ENFORCED REGISTRY / PENDING RUNTIME VERIFICATION

Purpose: stable index for `.agents-skills`. This folder contains technical mandates, not brainstorming notes.

Current inventory: `80` `.txt` mandates plus this `README.md` registry index.

## Authority

- `AGENTS.md` defines global rejection rules and the current authority spine.
- This file defines how to read the mandate registry.
- Task-relevant mandate files define local rules.
- Dated reports are evidence snapshots only. If a report changes policy, the policy must be promoted into `AGENTS.md`, this registry, or a stable `Docs/*.md` authority file.
- Current source and fresh logs still outrank prose claims.

## Read Rule

Before coding or writing a technical report, read `2-8` mandates that match the task domain. Do not bulk-load the whole registry as context noise.

If the selected route bible has no obvious mandate bucket, treat that as a routing gap: update this registry or the missing mandate before production implementation. Do not proceed from memory or from stale reports.

After editing mandate files or this registry, run `python -B Tools/Docs/TestMandateRegistry.py`. It checks inventory count, command-language discipline, weak wording, false readiness labels, truncated mandate bodies, and proof/evidence language. After editing the registry lint tool itself, also run `python -B Tools/Docs/TestMandateRegistry.py --self-test`. `--strict-format` additionally turns exported top-level markdown fences into blocking format errors.

Minimum examples:

- physics, movement, vehicles, collision: `PHYS_Physics_Integrity_Determinism_ForceMode.txt`, `CORE_Submarine_Vehicles_Kinematics_AUP.txt`, `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`, `OPT_Premium_Approximation_Protocol.txt`
- rendering, fog, light, particles: `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`, `REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`, `REND_Abyssal_Lighting_Voxel_Occlusion_Shadows.txt`, `REND_VFX_Fluid_Aesthetics_Compute_Particles.txt`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- save, streaming, persistence: `DATA_Save_Persistence_Binary_Delta_Checksum.txt`, `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`, `STRM_Persistent_Object_Registry.txt`
- runtime DTOs, binary payloads, signal payloads: `DATA_Runtime_Struct_Layout_ARM64.txt`, `QA_Evidence_Text_Filter_Audit.txt`, `ARCH_Signal_Lane_Segregation.txt`
- global authority, registry, event/signal split, DataVault ownership: `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`, `ARCH_Signal_Lane_Segregation.txt`, `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`, `QA_Evidence_Text_Filter_Audit.txt`
- UI/audio/presentation: `UI_Data_Streaming_ZeroGC_Optimization.txt`, `UI_Diegetic_Physical_Interfaces.txt`, `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt`, `AUDIO_Hrtf_Binaural_Spatialization.txt`
- tooling/procedural/world/designer bridges: `TOOL_Procedural_Wreckage_Generator.txt`, `TOOL_Designer_Facades_CSV_Binary_Bridge.txt`, `VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt`, `VOX_Voxel_World_Logic_Carving_Persistence.txt`

## Current Doctrine

- Premium approximation first. Simulate only gameplay truth.
- Player-visible visual mandates inherit the Visual Reference Parity Gate from `Docs/QUALITY_GATES.md`: mandatory references plus best-known internal baseline/current rejection matrix beat agent taste, raw diagnostic captures are reject-only, and `VISUAL_ROUTE_INVALID` triggers owner-stack recovery before polish.
- Any runtime system over `0.1ms` is suspicious until profiler proof and load-shed behavior exist.
- No per-proton, per-droplet, per-bubble, per-cable-segment, or per-flora-blade truth by default.
- Zero GC in hot paths remains non-negotiable.
- Native runtime buffers are DataVault-owned. Local persistent NativeArray ownership is banned outside the vault owner.
- Global authority is bounded: `GlobalRegistry` is cold service discovery, `SignalBus<T>` is first-party runtime broadcast, `HectonEventBus` is mod/API/cold isolation, `GlobalSignals` direct queues are legacy/bridge only, and `GlobalDataVault` is not a mutable global heap. New subsystem setup starts owner-local and follows `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_SETUP_PLAYBOOK.md`; new global routes require the route-card/lifecycle model in `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_OPERATING_MODEL.md`, the copy/paste template in `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md`, and a `GREEN` review disposition from `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md`.
- Read-looking APIs (`Get*`, `TryGet*`, `Resolve*`, `Read*`) are pure. They must not publish, sync scene state, allocate/grow buffers, complete jobs, mutate global state, or search the scene.
- `GlobalDataVault.TryGetLatestCreated()` is bootstrap/editor/diagnostic/crash-only unless a core fallback route card says otherwise. Domain runtime code uses injected `IDataVault` plus cached generation handles.
- Burst/Jobs require amortized, data-local batches and dispatcher-owned completion windows. Tiny same-frame schedule/readback loops are rejected without profiler proof.
- Data Monolith readiness requires the active `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` payload and import/bake/boot proof.
- Runtime DTOs, SignalBus payloads, telemetry entries, save staging records, and GPU upload records must be ARM64-safe: no runtime `Pack=1`, no runtime `bool`, 8-byte fields first, explicit padding, and total size multiple of 8.
- Systems execute through named phases: PRE_SIMULATION, SIMULATION, POST_SIMULATION, VISUAL_SYNC.
- New gameplay broadcasts use typed SignalBus lanes and ReadOnlySpan-style snapshots. A monolithic EventBus is not a gameplay transport.
- AUP authority uses a 300-frame Sync-Fence and millimeter quantization after every origin shift.
- Designer-tunable unmanaged data requires a human-readable bridge: CSV/SO/Editor facade to validated binary, with runtime parsing kept out of hot paths.
- Mandate language is command language. "Consider", "maybe", "should", "recommended", "best effort", "if possible", "when possible", "assume", "stub", and "placeholder" are rejected in new mandate text unless quoted as a banned/rejected/legacy/diagnostic/template pattern.
- Legacy or illustrative mandate snippets must not show dangerous runtime APIs as active routes. `Camera.main`, `FindObjectOfType`, `GameObject.Find`, `DontDestroyOnLoad`, `Resources.Load`, `StartCoroutine`, `BinaryFormatter`, `JsonUtility.FromJson`, `File.ReadAllText`, and `File.ReadAllBytes` must be marked as `[FORBID]`, banned, legacy, historical, injected/cached replacement context, or rejected by `python -B Tools/Docs/TestMandateRegistry.py`.
- Unity import, Console, Play Mode, profiler, GCMonitor, player-build, memory, frame-time, scene wiring, and visual quality are `PENDING VERIFICATION` unless fresh artifacts prove them.
- Verification has a budget. One scoped static scan and one scoped triage pass may route action; repeated checks over unchanged source/assets/proof are bureaucracy theater.
- After `PENDING VERIFICATION` is known, the next useful step is proof run, source/asset fix, or concrete blocker report. More boards, CSVs, task packets, and validation summaries are rejected unless they name a new command/file/proof action.

## Engineering Data

| Fact | Current Value | Enforcement |
|---|---:|---|
| Mandate files | 80 | `python -B Tools/Docs/TestMandateRegistry.py` must pass when files are added, removed, or edited. |
| Runtime coroutine tolerance | 0 | `IEnumerator`, `yield return`, and `StartCoroutine` are rejected in gameplay hot paths. |
| Unity 6000 render path | RenderGraph | New URP renderer features use `RecordRenderGraph`; Compatibility Mode is legacy debt. |
| Async Unity object path | `UnityEngine.Awaitable` | `Task` is reserved for owned persistent workers or non-Unity background work. |

## Conflict Resolution

When mandate text conflicts:

1. `AGENTS.md` wins.
2. A dated `2026-05-11` override in a mandate wins over older body text.
3. `OPT_Premium_Approximation_Protocol.txt` wins over simulate-first wording for water, light, deformation, pressure, flow, ambience, cable sag, particles, flora motion, and distant motion.
4. `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt` wins on frame, VRAM, RAM, quality-tier, and load-shed budgets.
5. `OPT_Zero_GC_Policy_AllocFree_Mandate.txt` wins on allocation policy.
6. `ARCH_Execution_Phases.txt` wins on runtime phase ownership.
7. `ARCH_Signal_Lane_Segregation.txt` wins on broadcast topology.
8. `ARCH_Global_Registry_ServiceLocator_DI_Init.txt` plus `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_BOUNDARIES.md`, `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_OPERATING_MODEL.md`, `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_SETUP_PLAYBOOK.md`, `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md`, and `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md` wins on registry/service/global-authority ownership, setup order, route-card practice, and review disposition.
9. `OPT_Native_Memory_Collections_JobSystem_Protocol.txt` wins on native allocation ownership and DataVault boundaries.
10. `MATH_AUP_Determinism_Sync.txt` wins on rebase Sync-Fence and AUP drift proof.
11. `DATA_Runtime_Struct_Layout_ARM64.txt` wins on runtime struct layout, runtime padding, and ARM64 DTO alignment.
12. `TOOL_Designer_Facades_CSV_Binary_Bridge.txt` wins on designer-facing bridges for unmanaged/binary data.
13. `QA_Evidence_Text_Filter_Audit.txt` wins on verification language and proof labels.
14. Current source/log evidence wins over undocumented assumptions.

## Registry Buckets

- `ARCH_*`: bootstrap, registry, service ownership, execution phases, signal lane segregation.
- `OPT_*`: performance, zero-GC, native memory, premium approximation doctrine.
- `PHYS_*`: physics truth, contacts, kinematics, tether, fluid/incursion.
- `REND_*`, `GPU_*`, `VOX_*`: rendering, VFX, shader, compute, voxel, MapMagic.
- `CORE_*`: player-facing systems, submarine, tools, weather, damage, survival.
- `AI_*`, `ANIM_*`: navigation, cognition, boids, IK, animation.
- `DATA_*`, `STRM_*`, `LOGI_*`, `NET_*`: data layout, runtime struct alignment, save, streaming, logistics, reconciliation.
- `AUD_*`, `AUDIO_*`, `UI_*`, `CTRL_*`: audio, UI, haptics, presentation.
- `TOOL_*`, `DBG_*`, `PROJECT_*`, `QA_*`: tooling, telemetry, compatibility, evidence discipline.

## Batch 007 Additions

- `ARCH_Execution_Phases.txt`: fixed phase order and phase ownership record.
- `ARCH_Signal_Lane_Segregation.txt`: typed SignalBus lanes, ReadOnlySpan snapshots, duplicate lane prevention.
- `MATH_AUP_Determinism_Sync.txt`: AUP Sync-Fence, millimeter quantization, drift probes, fault dump law.

## 2026-05-17 Prompt Audit Additions

- `DATA_Runtime_Struct_Layout_ARM64.txt`: centralized runtime DTO alignment law. It converts the prompt-level ARM64 warning into enforceable mandate text.
- `TOOL_Designer_Facades_CSV_Binary_Bridge.txt`: designer bridge law for CSV/SO/Editor facades that bake into binary runtime data without hot-path parsers.

## Inquisition Prevention

[RULE] A report claiming `VERIFIED`, `0 GC`, microsecond savings, compile success, platform readiness, IL2CPP safety, or data sovereignty must name artifact path, command/tool, timestamp, and evidence class.
[RULE] Binary/native DTOs and signal payloads crossing native, Burst, persistence, or platform boundaries require explicit layout proof or documented unmanaged field order.
[RULE] Contract expansion during a batch is API debt unless legacy wrappers are preserved and compile evidence exists.
[RULE] Duplicate signal names across assemblies are build-blocking architecture debt.
[RULE] Global authority changes must cite `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_BOUNDARIES.md`; new or changed global routes must satisfy `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_OPERATING_MODEL.md`, `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md`, and `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md`; changes to review queues or stop conditions must update `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_MIGRATION_LEDGER.md`.

[FORBID] False verification language.
[FORBID] Microsecond tables without profiler context.
[FORBID] Claiming platform readiness from `link.xml` text alone.
[FORBID] Claiming data sovereignty while NativeArray references bypass GlobalDataVault.
[FORBID] Using H-Phi score movement as proof that new global Registry/Signal/Event/Vault surface is architecturally correct.
[FORBID] Repeating static checks over unchanged source/assets/proof after the missing Unity/player/profiler/device proof is already known.
[FORBID] Treating report volume, CSV count, task packet count, or controller-board churn as progress.

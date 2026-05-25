# SHINOBU_331 Terminal Projection Route Card

Review disposition: `YELLOW`.

Reason: static source route is present, but compile, Unity import, Play Mode, profiler/GCMonitor, Burst Inspector, Frame Debugger, shader import, player build, and device proof are absent.

`GREEN` requires those artifacts.

Owner: `TerminalOsRuntime`.

Domain: Core Interaction Layers / Diegetic Terminal UV Input Projection.

Scope: diegetic submarine/base terminal input projection. This route replaces terminal Canvas/GraphicRaycaster/PhysX UI authority with AUP-localized Burst ray-plane math and shader cursor rendering.

## Runtime Route

| Fact | Owner | Phase | Route | Consumer | Proof |
|---|---|---|---|---|---|
| Gaze ray | `TerminalOsRuntime` cached input/player context | owner schedule before terminal projection | Vault buffer `71374` via `GenerateMockGazeVectorsJob` until live bridge takes over | `EvaluateTerminalGazeJob` | telemetry `71381` |
| Terminal screen plane | `TerminalOsRuntime` | cold boot/CSV refresh | Vault buffer `71373` `TerminalPlaneDTO` | `EvaluateTerminalGazeJob`, gizmo | layout validator |
| Button rects | `TerminalOsRuntime` | cold boot/CSV refresh | Vault button AABB buffer | `EvaluateTerminalGazeJob` | static CSV parser/source audit |
| Shader cursor state | `TerminalOsRuntime` | `LateFrameTick` finalization / visual sync; PRE_SIM migration required | Vault `71380`, `TerminalInputStateDTO`, `TerminalInputGpuStateDTO`, `_TerminalInputStates` | `Hecton_DiegeticTerminal.shader` | source audit, Frame Debugger pending |
| Shader cursor dirty rows | `TerminalOsRuntime` | audit before upload | Vault `71383`; `uint[terminalCount]`; high bit dirty; low 31 bits row hash | `_TerminalInputStates` contiguous lock runs | source audit; profiler pending |
| Tuning | `TerminalOsRuntime` | cold/editor facade | Vault buffer `71382` `TerminalInputTuningDTO` | owner schedule, X-Ray window | layout validator |
| Commands | `EvaluateTerminalGazeJob` | current owner projection schedule under `TerminalOsRuntime`; PRE_SIM dispatcher proof pending | `SignalBus<TerminalCommandSignal>` ParallelWriter | terminal command consumers | SignalBus route, runtime proof pending |
| UI hints | `EvaluateTerminalGazeJob` | current owner projection schedule under `TerminalOsRuntime`; PRE_SIM dispatcher proof pending | `SignalBus<InteractionUiSignal>` ParallelWriter | interaction UI hint consumers | SignalBus route, runtime proof pending |
| Black-box telemetry | `TerminalOsRuntime` | POST_SIMULATION owner telemetry | Vault buffer `71381` `TerminalInputTelemetryEntry[300]` | crash dump/report/X-Ray | `Dump_SHINOBU_331.bin` on fault |

## Vault Buffers

- `71380` `TerminalInputStateDTO[terminalCount]`, 64 bytes per row, `NativeArrayOptions.UninitializedMemory`.
- Presentation-only.
- Opened during native resource setup.
- Generation handle cleared on owner shutdown/hot-swap.
- Owner write/finalize paths fail closed through `TryOpenVaultBuffer`.
- Public snapshot accessors use `TryReadVaultBuffer`.
- `71381` `TerminalInputTelemetryEntry[300]`, 64 bytes per row, `NativeArrayOptions.ClearMemory`. Black-box ring. Dumped as fixed 64-byte header plus chronological raw rows on non-finite/layout/budget fault.
- `71382` `TerminalInputTuningDTO[1]`, 64 bytes, `NativeArrayOptions.UninitializedMemory`. Editor facade writes through `UnsafeUtility.AsRef`; owner mirrors sanitized scalars before scheduling.
- `71383` `uint[terminalCount]`, 4 bytes per row, `NativeArrayOptions.ClearMemory`.
- Per-row dirty hash lane.
- High bit marks GPU upload dirty; low 31 bits store FNV row hash of terminal hash, flags, and UV.
- Recreated GPU buffers force full first upload before hash-gated runs resume.

No persistent private `NativeArray`, `NativeList`, or `NativeHashMap` ownership is introduced by SHINOBU_331. GraphicsBuffers are presentation upload surfaces and are double-buffered for `_TerminalInputStates`.

## DTO ABI

- `TerminalInputStateDTO=64`: `TerminalAUP@0 double3`, `ForwardNormal@24 float3`, `UpVector@36 float3`, `ProjectedUV@48 float2`, `TerminalHashID@56 uint`, `InputFlags@60 uint`.
- `TerminalInputGpuStateDTO=32`.
- Layout: `ProjectedUV@0 float2`, `TerminalHashID@8 uint`, `InputFlags@12 uint`, `Reserved0@16 float4`.
- Shader ABI target: `_TerminalInputStates`.
- The 64-byte CPU AUP DTO is not uploaded verbatim.
- `TerminalInputTelemetryEntry=64`: frame/counts/timing/quality/fault flags through byte 55, `_pad0@56 ulong`.
- `TerminalInputTuningDTO=64`: distance/tolerance/thickness/quality/radius scalars through byte 27, explicit padding through byte 63.
- `TerminalInputBlackBoxHeader=64`: magic/version/fault/cursor/entry metadata through byte 31, explicit padding through byte 63.

No `Pack=1`, no hot DTO properties, no managed references.

## Rollback Fence

`ProjectedUV` and `InputFlags` are transient presentation data. They are not gameplay truth, save identity, DTO authority, or Merkle/state-ring material. Authority is `TerminalCommandSignal`.

## Scalability

`EvaluateTerminalGazeJob` scaling:

- Vault defaults: `LowRadiusMeters=5.0`, `UltraRadiusMeters=25.0`.
- Quality curve: `GlobalQualityWeight` through `QualityCurvePower`.
- Continuous lerps: schedule batch size, mock sway, UV edge tolerance, shader cursor detail.
- Tier delta: distance, tolerance, presentation detail.
- Invariant: terminal command truth and DTO layout.

## Dear Lie

Cursor presentation is shader-only in `Hecton_DiegeticTerminal.shader` via `_TerminalInputStates` and `H8ApplyTerminalCursor`.

CPU uploads only dirty 32-byte UV/flag rows. It does not move GameObjects, rebuild Canvas mesh, run GraphicRaycaster, or use PhysX UI.

Shader render-state exception:

- Shader: `Hecton_DiegeticTerminal.shader`.
- Queue/type: `Geometry` / `Opaque`.
- Depth/culling: `ZWrite On`, `Cull Back`.
- Reason: physical front-facing terminal surface participates in world depth and occlusion.
- Rejected classification: transparent HUD pane.
- Rejected visibility: backside surface without a separate art-owned backside material.

## Failure Evidence

NaN/non-finite, layout mismatch, or >0.2 ms projection faults dump `TerminalInputTelemetryEntry` history to `Docs/AgentLogs/Dump_SHINOBU_331.bin` with a fixed 64-byte header and raw 64-byte rows.

## Shutdown

- Owner clears Vault generation handles on shutdown/hot-swap.
- It sets `_TerminalInputStateCount=0`.
- It releases both terminal input GraphicsBuffers.
- Teardown force-complete is only for owner disposal.
- Normal frame finalization polls completion and uses dispatcher fence.

## Proof Required Before GREEN

- Guarded compile with no SHINOBU_331 diagnostics.
- Unity import and Console clean for SHINOBU_331 assets.
- Play Mode GCMonitor proof: 0 B/frame on terminal projection hot path.
- Burst Inspector proof for `GenerateMockGazeVectorsJob` and `EvaluateTerminalGazeJob`.
- Frame Debugger proof that `_TerminalInputStates` is bound and cursor is shader-side.
- Runtime profiler proof under terminal spam and no Canvas/GraphicRaycaster/PhysX terminal authority path.

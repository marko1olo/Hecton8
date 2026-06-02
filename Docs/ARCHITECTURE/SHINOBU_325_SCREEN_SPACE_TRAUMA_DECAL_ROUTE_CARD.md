# SHINOBU_325 Screen-Space Trauma Decal Route Card

Route ID: `SHINOBU_325_SCREEN_SPACE_TRAUMA_DECAL_RESOLVER`

Date: 2026-05-22

Owner: `SHINOBU_325`

Owner domain: Echelon 8 Presentation & UX / Screen-Space Trauma Decals

Owning files: `DynamicDecalVaultRuntime.cs`, `DeferredDecalPass.cs`, `Hecton_VisorTrauma.shader`

Status: `YELLOW / STATIC_SOURCE_PRESENT / UNITY RUNTIME PROOF PENDING`

Problem: blood, acid, burns, scorch marks, hull dents, and visor glass cracks must not spawn `DecalProjector`, Canvas, quad, particle, or per-trauma GameObject routes.

## Authority Route

- Producers: combat and high-speed impact owners publish unmanaged damage/impact signals in their owner phases.
- Consumer: `DeferredDecalPass` captures the active game camera, publishes only the last complete GPU buffer, and performs visual sync through dispatcher late-frame ownership.
- Hot route: `SignalBus<CombatDamageSignal>` and `SignalBus<HighSpeedImpactSignal>` snapshots feed Vault-backed fixed ring `DecalRequestSignal[1024]`.
- Memory route: `GlobalDataVault` owns all persistent presentation payloads. No rollback, save, Merkle, or gameplay authority data is owned here.
- AUP route: incoming AUP stays double precision until camera-local matrix construction. Shader reconstructs world position from the depth buffer, subtracts `_GlobalVisorTraumaCameraWS`, and evaluates camera-relative trauma matrices.

## Payload Contract

- BufferIDs: `73190..73198`, owned by `SystemID.Vfx`.
- `73190` `TraumaDecalDTO[128]`
- `73191` upload scratch `TraumaDecalDTO[128]`
- `73192` `DecalRuntimeStateDTO[1]`
- `73193` `TraumaWoundTelemetryEntry[300]`
- `73194` `DecalTuningDTO[1]`
- `73195` `DecalMaterialProfileDTO[256]`
- `73196` CSV scratch `byte[16384]`
- `73197` ingress request ring `DecalRequestSignal[1024]`
- `73198` ingress request state `DecalRequestQueueStateDTO[1]`
- `TraumaDecalDTO` is explicit 80 bytes: `LocalToWorld@0`, `DecalTypeHash@64`, `Opacity01@68`, `BirthTime@72`, `Flags@76`.
- `DecalRequestSignal` is explicit 64 bytes: `ImpactAup@0`, `Normal@24`, scalar/material/flag lane `36..60`.
- `DecalRequestQueueStateDTO` is explicit 64 bytes and isolates queue counters from trauma DTO rows.
- `TraumaWoundTelemetryEntry` is explicit 64 bytes and dumps to `Docs/AgentLogs/Dump_SHINOBU_325.bin` with fixed rows.

The old local `71490..71496` candidate is rejected for SHINOBU_325 because `H8Memory` already owns it for auxiliary equipment and propwash GPU lanes.

## Render Route

- Active shader: `Assets/_Project/Art/Shaders/Hecton_VisorTrauma.shader`, `Hidden/Hecton8/VisorTrauma`.
- Shader ABI: `_GlobalVisorTrauma`, `_GlobalVisorTraumaCount`, `_GlobalVisorTraumaAtlas`, `_GlobalVisorTraumaParams`, `_GlobalVisorTraumaRefractionParams`, `_GlobalVisorTraumaTint`, `_GlobalVisorTraumaCameraWS`.
- RenderGraph pass: `Hecton Visor Trauma Composite`.
- Draw submission: one fullscreen `CoreUtils.DrawFullScreen` call for the published trauma buffer.
- Depth path: `SampleSceneDepth` + `ComputeWorldSpacePosition` reconstructs scene position; local trauma space decides projection coverage and depth fade.
- RenderGraph ABI guard: graph-owned source/depth textures bind as `TextureHandle`s; the optional static `Texture2DArray` trauma atlas is material-local and must not be bound through `RasterCommandBuffer.SetGlobalTexture(int, Texture)`.
- Legacy shaders `Hecton_VisorWounds.shader` and `Hecton_DeferredDecal.shader` are compatibility shims and must not be treated as new active ABI owners.

## Quality Scaling

- `GlobalQualityWeight` continuously scales active trauma count from 8 to 128.
- Low: newest small trauma window, fast decay pressure, procedural samples only.
- Middle: moderate active window and normal perturbation.
- High: stronger crack refraction and larger active window.
- Ultra: maximum active buffer and richer procedural/atlas sampling.
- Quality changes cost and visual richness only. It does not change DTO layout, authority, save identity, or rollback state.

## Fault Route

- Last 300 high-level frames are retained in `TraumaWoundTelemetryEntry[300]`.
- Layout faults, non-finite matrix/opacity, or upload stalls mark runtime flags and dump fixed binary rows to `Docs/AgentLogs/Dump_SHINOBU_325.bin`.
- Public runtime ingress fails closed if cold storage is absent or a visual-sync job owns the Vault request ring.

## H-Phi Correction

- There is no private persistent `NativeQueue`, `NativeArray`, `NativeList`, or `NativeHashMap` in the active trauma route.
- Runtime ingress uses Vault-backed `73197`/`73198`; cold/mock writers reserve fixed slots, Burst fills rows by index, and the visual-sync job drains by `ReadIndex`/`PendingCount`.
- This keeps bounded queue semantics without owning allocator memory outside `GlobalDataVault`.

## Proof

- Scanner: `Tools/Trauma_Projector_Inquisition.py`.
- Current static status: PASS, no active trauma GameObject/Canvas/DecalProjector spawn route, inactive URP decal renderer features reported separately.
- Runtime proof still required: Unity import, shader import, Frame Debugger one fullscreen pass, profiler timing, and GCMonitor 0 B/frame under damage spam.

Rejected alternatives: runtime `DecalProjector`, Canvas blood, spawned quads, particle splats, fracture mesh truth, material clones, hot `GlobalRegistry` polling, private persistent `NativeQueue`, `GraphicsBuffer.SetData`, absolute float world upload.

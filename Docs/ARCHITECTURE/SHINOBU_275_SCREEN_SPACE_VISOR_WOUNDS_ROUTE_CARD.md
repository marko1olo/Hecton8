# SHINOBU_275 Screen-Space Visor Wounds Route Card

Supersession note, 2026-05-22:

- Active trauma decal ownership moved to `SHINOBU_325_SCREEN_SPACE_TRAUMA_DECAL_RESOLVER`.
- Use route card `SHINOBU_325_SCREEN_SPACE_TRAUMA_DECAL_ROUTE_CARD.md`, BufferIDs `73190..73198`, shader `Hecton_VisorTrauma.shader`, `_GlobalVisorTrauma`.
- Old `71490..71496` range collides with auxiliary/propwash lanes.

Route ID: `SHINOBU_275_SCREEN_SPACE_VISOR_WOUNDS`
Date: 2026-05-21
Owner: `SHINOBU_275`
Owner domain: Echelon 8 Presentation & UX / Screen-Space Wounds & Decals
Owning file/system: `DynamicDecalVaultRuntime`, `DeferredDecalPass`, `Hecton_VisorWounds.shader`
Status: `YELLOW / PENDING UNITY RUNTIME PROOF`

Problem: helmet glass cracks, blood, burns, and acid splats must not spawn `DecalProjector`, quads, particles, Canvas overlays, or material clones.

Why owner-local data is insufficient: damage impacts originate in combat/physics lanes and must cross into presentation without concrete sibling-domain references.

Why direct caller/owner interface is insufficient: multiple damage producers can broadcast impacts; the presentation route must consume immutable unmanaged snapshots without owning combat truth.

Instrument:
- `SignalBus<CombatDamageSignal>` first-party broadcast.
- `SignalBus<HighSpeedImpactSignal>` compatibility visual ingress.
- Read-only AUP bridge:
  - APIs: `GlobalSignals.CurrentRuntimeOriginAup()`, `GlobalSignals.TryRuntimePositionToAup()`.
  - Scope: camera/runtime-position localization only.
  - Owner: Core origin/AUP lane.
  - Phase: dispatcher visual sync/camera staging.
  - Cadence: once per staged camera context or request localization.
  - Fallback: cached `IPlayerRuntimeContext` snapshot, then current origin.
  - Telemetry: non-finite AUP/matrix faults recorded in the wound ring.
  - Forbidden: direct GlobalSignals queue publish.
  - Does not replace SignalBus damage ingress.
- `GlobalDataVault / IDataVault` for persistent visor wound DTO, upload scratch, tuning, profiles, CSV scratch, and black-box telemetry.
- Black-box telemetry route.

Producer/consumer phase:
- Producers: combat/impact owners publish unmanaged signals in their owner phases.
- Consumer: `DeferredDecalPass` registers as dispatcher `ILateFrameTickable`.
- Renderer enqueue captures camera context and publishes only prior staged GPU buffer.
- `LateFrameTick()` performs visor wound visual sync for the next frame.
- `RecordRenderGraph()` reads only last published GPU buffer snapshot.

Cadence/capacity:
- Signal snapshot read once per Unity frame.
- Active GPU-evaluated wounds scale 8..128.
- Request ingress queue is fixed and prewarmed; active ring insertion is O(1).
- Runtime damage ingress fails closed unless cold storage is initialized.
- `TryEnqueueRuntimeImpact()` and `TryEnqueueAupImpact()` do not call `EnsureInitialized()`.
- Gameplay producers cannot trigger registry polling, queue allocation/prewarm, Vault handle acquisition, or default tuning seed from a hot impact.
- While a visual-sync job is pending, public/mock ingress fails closed and increments dropped-ingress telemetry instead of touching the `NativeQueue` that the scheduled dequeue job owns.

Expected max events/reads per frame:
- Matrix job processes at most current `MaxActiveDecals`.
- Shader reads at most `_GlobalVisorWoundCount`, clamped by the quality-scaled upload count.
- Signal ingress resolves material profile rows and live tuning once per visual-sync snapshot; accepted signal count no longer multiplies profile descriptor resolves.

## GlobalQualityWeight behavior

- `ResolveMaxActiveDecals()` uses smoothed `GlobalQualityWeight` to lerp 8..128.
- Thermal pressure increases decay pressure.
- Shader crack/refraction detail scales by quality and `NormalRefractionIntensity`; DTO layout and authority route do not change.
- Active Noir integration is pre-tonemap only; URP Volume Tonemapping owns final ACES.
- `Hecton_VisorGlitchACES.shader` must not apply local fragment tonemap or clamp HDR color with `saturate(color)`.
- Noir timing follows the dispatcher route: `TimeSliceScheduler.CurrentFrameId` supplies frame/profile cadence and finite `SystemDispatcher.CurrentFrameDeltaTime` advances wrapped visual phase. Unity `Time.*` is not part of the owned wound/noir route.
- Active Noir CBuffer publication is owned by `HectonVisorUberPostFeature.LateFrameTick`.
- `AddRenderPasses()` consumes last valid buffer and enqueues RenderGraph pass. One-record mock/parameter math is direct scalar code.
- Shared host player-context path consumes cached `IPlayerRuntimeContext` snapshots; render enqueue does not call `PlayerRuntimeContextService.TryGetActiveRuntimeContext()`.
- Touched host file no longer imports `Hecton8.Gameplay`; survival status and hull stress come from owner-published snapshot DTOs.
- Wet-lens stays presentation-only from cached movement owner.
- Shared host no longer imports concrete `Hecton8.Physics`, caches `HectonFluidEngine`, or handles `GlobalRegistryServiceSlot.FluidRuntime`.
- Maelstrom pressure is not sampled from fluid owner in this presentation route; current pressure/stress trauma uses owner-local screen-space surge scalar until a contracts-only model is approved.
- Reconstruction constants use A/B mapped constant buffers.
- One active buffer is published for RenderGraph.
- AB split is bound in the reconstruction raster function.
- Enqueue path does not mutate reconstruction material.
- Reconstruction aesthetic CSV/profile data is cold-loaded into a fixed 32-row snapshot cache. `AddRenderPasses()` selects profiles without Vault locks or file IO retries.
- Legacy shader low-tier behavior is continuous for heat haze, VR comfort, light shafts, water refraction, and droplets.
- Quality may scale ALU/intensity, not feature ownership or DTO layout.

Accessor purity:
- Public read accessors now fail closed unless cold initialization already created handles.
- No read accessor publishes signals, syncs scene state, grows buffers, completes jobs, or searches the scene.
- Public `TryGetTuning`, `TryGetRuntimeState`, and `TryGetLatestTelemetry` return owner-phase immutable snapshots and do not lock/unlock Vault buffers.
- `TryAcquireDecalBufferRead` / `ReleaseDecalBufferRead` is compiled only under `UNITY_EDITOR`; it is an explicit acquire/release debug surface for SceneView matrix gizmos, not a runtime `TryGet*` read accessor.

Payload/data shape:
- Managed fields present: no.
- UnityEngine.Object fields present: no.
- Layout proof: `VisorDecalDTO` is explicit 80 bytes.
- Offsets: `LocalToWorld@0` 64B, `DecalTypeHash@64` 4B, `Opacity01@68` 4B, `BirthTime@72` 4B, `Flags@76` 4B.
- Offset 72 matches original XML shader ABI; lifetime is packed into bits 8..23 of `DecalTypeHash`; wound type remains bits 0..3, atlas slice bits 4..7.
- Telemetry proof: `VisorWoundTelemetryEntry` is explicit 64 bytes.
- Fault dump proof: `Dump_SHINOBU_275.bin` writes a fixed 16-byte little-endian header plus fixed 64-byte telemetry rows via stack spans. The crash proof lane does not use `BinaryWriter`.

Overflow/failure:
- Active ring overwrites `TotalWritten % capacity`.
- Pending visual-sync ingress is dropped with telemetry rather than blocking the frame or racing the dequeue job.
- Non-finite matrices mark fault and dump telemetry.
- Upload stalls above local threshold mark fault and dump telemetry.

Telemetry fields:
- frame, active, new, upload count, GPU upload microseconds, CPU microseconds, quality, thermal, flags, state hash, dropped, total written, max active, last ballistic frame.

Black-box fields:
- same fixed telemetry rows, dumped to `Docs/AgentLogs/Dump_SHINOBU_275.bin` with explicit little-endian header/row bytes.

Profiler marker:
- `H8.VisorWounds.VisualSync`
- `H8.VisorWounds.Enqueue`
- `Hecton Visor Wound Composite`

Loop 20 render ownership addendum:
- `DeferredDecalPass` binds wound atlas state inside the RenderGraph raster function; no material atlas mutation remains in setup.
- `HectonVisorUberPostFeature.AddRenderPasses()` stages reconstruction camera/runtime input and consumes the last active reconstruction CBuffer only. `LateFrameTick()` owns reconstruction constant upload, Vault mirror write, telemetry write, and dump emission.
- Visor post trauma scalars/textures pass through pass data and bind with command-buffer globals inside raster function.
- Legacy visor post shader no longer uses `UnityPerMaterial` for those fields.
- Owned visor post/reconstruction shaders consume dispatcher-published visual time globals, not shader `_Time`.
- Noir/reconstruction CSV profile selection reads fixed cold snapshots after parse; hot paths do not resolve profile Vault NativeArrays.

Loop 22 render/fault addendum:
- Wound atlas and visor post texture bindings now use `RasterCommandBuffer.SetGlobalTexture` inside RenderGraph raster functions. No owned wound/noir raster binding path mutates `Material.SetTexture`.
- `DynamicDecalVaultRuntime` no longer throws from the runtime state ref helper; invalid Vault state access marks the existing layout fault route and fails closed.

Loop 23 cold-state addendum:
- Cold storage seeds `DecalRuntimeStateDTO` before visual sync with `RuntimeInitializedFlag`, continuous quality, thermal pressure, max-active count, and normal refraction intensity.
- Instance/upload/profile Vault buffers are requested with clear memory. The former first-frame main-thread `ClearDecalsJob.Execute(i)` loop is removed from normal visual sync; cold fallback clearing uses bounded `UnsafeUtility.MemClear`.

Loop 24 editor-facade addendum:
- `ScreenSpaceDecalTunerWindow` now surfaces source CSV, schema id/hash, runtime Vault route, DataMonolith bake caveat, last validation state, row count, selected header hash, and explicit DTO byte-layout summaries.
- CSV load rejects schema-header hash mismatches before calling the cold `TryLoadMaterialProfilesCsv` Vault path. This validation does not claim `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` exists or changes runtime payload authority.

Loop 26 disk-state render-binding correction:
- `DeferredDecalPass` binds `_GlobalVisorWoundAtlas` with `RasterCommandBuffer.SetGlobalTexture`.
- `HectonVisorUberPostFeature` binds crack, lens dirt, blue-noise, and VR comfort textures with `RasterCommandBuffer.SetGlobalTexture`.
- The stale string-name texture constants are removed from those owned sources.
- Focused scan confirms no `Material.Set*` / `.SetTexture(` / `.SetBuffer(` / stale texture-name constant remains in those owned RenderGraph sources.

Loop 28 disk-state render-binding and constants clear-ownership correction:
- Fresh source proof supersedes stale Loop 26/27 evidence. The active disk sources now bind wound atlas, crack, lens dirt, blue-noise, and VR comfort textures with `RasterCommandBuffer.SetGlobalTexture`.
- `ReconstructionConstantsVaultId` requests `NativeArrayOptions.ClearMemory`; editor diagnostics cannot read undefined constants before the first dispatcher publish.
- `ReconstructionCsvScratchVaultId` remains `UninitializedMemory` by design because it is cold scratch and the parser consumes only the file-read byte count.
- Scanner PASS timestamp is 2026-05-21T23:30:34Z with 0 active GameObject/URP decal violations.

Loop 29 VR comfort smoothstep correction:
- `HectonVisorUberPost.shader` no longer uses `step(0.42, edge01)` for low-tier comfort edge masks; both paths use `smoothstep(0.36, 0.48, edge01)`.
- The concurrent C# render-binding overwrite was reasserted to command-buffer `SetGlobalTexture`.
- Scanner PASS timestamp is 2026-05-21T23:33:01Z with 0 active GameObject/URP decal violations.

Loop 30 mobile waterline smoothstep correction:
- `HectonVisorUberPost.shader` mobile internal-waterline path no longer uses `cameraSubmerged = step(cameraPosition.y, waterlineY - 0.03)`.
- It now uses the existing softness scalar as `smoothstep(-softness, softness, (waterlineY - 0.03) - cameraPosition.y)`.
- Scanner PASS timestamp is 2026-05-21T23:37:35Z with 0 active GameObject/URP decal violations.

Loop 31 crack reveal smoothstep correction:
- `HectonVisorUberPost.shader` procedural crack reveal no longer uses `step(threshold, damage01)`.
- Texture-driven crack reveal no longer uses `step(crackSample.a, damage01)`.
- Both paths use narrow `smoothstep` reveal bands; shader property IDs, DTOs, and BufferIDs are unchanged.
- Scanner PASS timestamp is 2026-05-21T23:40:23Z with 0 active GameObject/URP decal violations.

Loop 32 radial falloff smoothstep correction:
- `FastRadialFalloff01()` no longer uses `step(2.0, e)` to switch exponent approximation families.
- It now blends low/high polynomial falloff approximations through `smoothstep(1.85, 2.15, e)`.
- Scanner PASS timestamp is 2026-05-21T23:42:38Z with 0 active GameObject/URP decal violations.

GC proof required:
- Unity Profiler / GCMonitor capture in Play Mode. Static source proof only exists now.

Shutdown/disposal:
- `DeferredDecalPass.Dispose()` releases double-buffered `GraphicsBuffer`s.
- subsystem registration and rebind reset force-complete pending visual-sync work, unlock runtime buffers, then reset/dispose the native request queue.

Scene unload behavior:
- runtime static reset disposes request queue and releases pending locks.

Stale-handle behavior:
- read accessors return false when handles are not already initialized.
- write/init paths reacquire generation handles through `IDataVault`.
- public runtime ingress returns false when storage is not ready; cold creation remains owned by feature `Create()`, DataVault replacement handling, editor/mock tooling, and explicit bootstrap calls.

Rejected alternatives:
- `DecalProjector` GameObjects.
- Canvas blood overlays.
- spawned quads / particles.
- per-instance material clones or MPBs.
- direct combat concrete references.
- absolute world float coordinates.

Why this does not increase global monolith risk:
- no new `GlobalRegistry` service slot.
- no new signal type.
- no gameplay truth ownership.
- no save/Merkle/rollback state.
- existing domain-local Vault buffer IDs are documented as presentation/proof lanes.

H-Phi impact expected:
- lower object hierarchy/render submission pressure; local numeric BufferID debt remains documented until central enum migration is authorized.

Proof required before GREEN:
- guarded C# compile.
- Unity import and Console clear.
- Play Mode GCMonitor 0 B/frame capture under damage spam.
- Frame Debugger proof of one fullscreen wound pass and inactive URP object decal path.
- profiler CPU/GPU timing under low/mid/high quality.

Reviewer: integrator/graphics authority required.
Review disposition: `YELLOW`

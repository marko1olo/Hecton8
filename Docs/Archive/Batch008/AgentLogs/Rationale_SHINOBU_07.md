# Rationale_SHINOBU_07

Date: 2026-05-18
Status: PENDING VERIFICATION

## Decision 00: Execution Boundary

Problem: SHINOBU_07 owns diegetic spatial UI/HUD. Existing project contains many legacy Canvas/TMP UI files and active diegetic UI fragments. Directly deleting legacy UI without scene/prefab evidence risks cross-domain sabotage and compile walls.
Solution: Build the wrist hologram runtime as an isolated first-party UI subsystem under `Assets/_Project/Scripts/UI`, using unmanaged DTOs, mock signals, fixed font metrics, instanced quads, and editor-only tooling. Existing Canvas systems will be audited and bypassed, not deleted blindly.
Rejected Alternatives: Raw prefab/scene YAML deletion of Canvas components was rejected; Unity prefab mutation without live scene evidence violates the project YAML guard and can corrupt handoff scenes.
Scalability potential: Low uses static unlit SDF quads and culls at 45 degrees; Middle enables bar shiver and basic glitch; High enables chromatic glitch; Ultra increases radar/threat density and visual overkill while preserving the same DTO contract.
Hardware Impact: Expected gain on i3/MX350 comes from avoiding Canvas rebuilds and text string churn. Numeric proof is absent until Unity Profiler/GCMonitor capture.

## Decision 01: Vault Slots and Unmanaged HUD State

Problem: Wrist-HUD needs stable cross-system memory without direct compile dependency on survival, inventory, or SignalBus implementations still being edited by parallel agents.
Solution: Added UI-owned `BufferID.WristHudState`, `WristHudQuads`, `WristHudFontAtlas`, `WristHudTelemetryRing`, `WristHudCounters`, and `WristHudAcousticTaps`; `WristHologramHudRuntime` now stores only `VaultBufferHandle<T>` fields and resolves transient NativeArray views from `GlobalDataVault` at use sites. If the vault is absent, the HUD fails closed instead of allocating local NativeArrays.
Rejected Alternatives: Direct references to PlayerMetabolism, inventory controllers, and concrete SignalBus classes were rejected because those owners are moving and would create a compile wall outside SHINOBU_07 domain.
Scalability potential: Low keeps the same DTO contract with fewer active quads and radar taps; Middle/High/Ultra increase visual density without reallocating or changing consumers.
Hardware Impact: i3/MX350 avoids managed UI object churn and Canvas rebuilds; expected savings are 80-500 us on text-heavy HUD frames, pending profiler proof.

## Decision 02: 112-Byte Quad Contract

Problem: The prompt demanded `float4x4 + float4 + float4 + uint + float + padding` equal to 112 bytes; one `uint` pad only totals 108 bytes, which is not enough for the stated stride.
Solution: Used `[StructLayout(LayoutKind.Sequential, Size = 112)]` and two explicit `uint` pads in `WristHudQuadTransformDTO`. Runtime asserts `UnsafeUtility.SizeOf<WristHudQuadTransformDTO>() == 112` on cold enable.
Rejected Alternatives: `[StructLayout(Pack=1)]`, bool flags, and squeezed 108-byte layouts were rejected; they trade false compactness for ARM64 alignment risk and shader stride mismatch.
Scalability potential: All tiers use the same stride; low tier culls and emits fewer records, high tier spends the same record format on richer shader presentation.
Hardware Impact: Fixed stride prevents misaligned structured-buffer reads on mobile/VR hardware; estimated failure prevention is correctness-critical, not a measured microsecond gain.

## Decision 02B: State DTO Alignment Correction

Problem: Ultra-polish self-audit caught `WristHudStateDTO` declared as 224 bytes while its actual fields total 248 bytes: twelve `float4` lanes (192 bytes) plus fourteen `int` lanes (56 bytes). The stale size was architectural rot, not a harmless annotation.
Solution: Corrected `[StructLayout(LayoutKind.Sequential, Size = 248)]` and expanded cold layout asserts to cover `WristHudStateDTO`, glyph, telemetry, mock signal DTOs, and acoustic taps. `248 % 8 == 0`, so the state record remains ARM64-safe.
Rejected Alternatives: Leaving the stale size because the compiler might tolerate it was rejected. `[StructLayout(Pack=1)]` was also rejected; padding and field order stay explicit and 8-byte aligned.
Scalability potential: All tiers read the same state contract; Low sheds quad count and shader overkill, Ultra spends the stable contract on richer glitch/radar density.
Hardware Impact: Prevents wrong-stride vault interpretation and ARM64 cache penalties. Microsecond gain is not claimed; this is correctness and crash-prevention work.

## Decision 03: FixedString Text and Fragment Glitch

Problem: Updating `O2: 98%` through managed strings or TMP allocates and wakes layout rebuilds.
Solution: Built HUD labels with `FixedString64Bytes`, converted them into one quad per glyph in `TextToQuadsJob`, and moved radiation/toxemia glitch to `Hecton_WristHudSDF.shader` using per-fragment noise, UV jitter, RGB split, and flicker.
Rejected Alternatives: TextMeshPro, CanvasRenderer, mesh-per-glyph text geometry, and CPU random glitch mutation were rejected because they produce layout/GC pressure or unnecessary vertex work.
Scalability potential: Low disables heavy glitch with `_LowTierMode`; Middle keeps flicker; High/Ultra keep chromatic split and denser radar/PDA quads.
Hardware Impact: i3/MX350 avoids managed text formatting and layout rebuilds entirely; expected CPU savings are 40-250 us per changing HUD label group, pending profiler proof.

## Decision 04: Wrist-Relative AUP Safety

Problem: Global AUP/double coordinates cast down to GPU floats make wrist text shake at map edges.
Solution: The job receives already rebased runtime float transforms and builds quads from wrist/head local basis vectors only; PDA grid and visor vignette are local offsets from wrist/camera matrices.
Rejected Alternatives: Direct global double3 to shader, scene searches, and physics raycasts for HUD placement were rejected as jitter-prone and outside UI ownership.
Scalability potential: Low removes smoothing to keep pose direct; Middle/High/Ultra add nlerp catch-up and visual shiver while staying local-space.
Hardware Impact: Local float math is branch-light and under 10 us expected for 512 quads on i3/MX350; main gain is no visible precision shimmer.

## Decision 05: Black Box and Human Tuning

Problem: HUD failures must leave evidence, and art direction needs control without recompiling binary DTOs.
Solution: Added 300-entry `WristHudTelemetryEntry` ring and `Dump_SHINOBU_07.h8dump` on NaN with a fixed 32-byte raw header plus contiguous telemetry payload; added `HudHologramTunerWindow`, SceneView/Gizmos PDA grid visualization, and span-based `font_metrics_override.csv` ingestion.
Rejected Alternatives: Console-only reports, in-game debug Canvas, and `string.Split` CSV parsing were rejected; they either vanish after crash, reintroduce Canvas, or allocate avoidable garbage.
Scalability potential: Low can tune distance/text size for readability; Ultra can push stronger palette/glitch overkill through the same vault state.
Hardware Impact: Telemetry writes are fixed-size native stores, estimated below 5 us/frame; editor and CSV IO are cold/editor-side, not runtime UI hot path.

## Decision 06: Signal Corridor and Player I/O Protection

Problem: The original blind mock lane proved isolation but did not consume existing signal corridors, and the CSV poller touched File APIs from the runtime Tick path.
Solution: Added snapshot drains for `SignalBus<SurvivalVitalsChangedSignal>`, `RadiationDoseSignal`, `SystemHealthIndexSignal`, and `PdaExchangeStateChangedSignal` through `Hecton8.Core.Contracts.Signals` only. CSV polling is now compiled/called under `UNITY_EDITOR`; the public reload remains a manual editor facade action. Mock signal generation writes to a `NativeQueue<PlayerVitalsSignal>.ParallelWriter` without a local NativeArray scratch buffer.
Rejected Alternatives: Direct concrete survival/inventory/audio dependencies were rejected. Player-build polling of `font_metrics_override.csv` was rejected because Steam Deck MicroSD and main-thread File I/O can create hitches. Runtime reflection or string EventIDs were rejected.
Scalability potential: Low/MX350 and critical `SystemHealthIndexSignal` hold a 300-frame low-tier mode to avoid oscillation. High/Ultra keep chromatic glitch and denser radar visuals when pressure is low.
Hardware Impact: Removes a player hot-loop File I/O hazard and local NativeArray ownership. Estimated savings are hitch-risk removal rather than a deterministic microsecond count.

## Decision 07: SRP Payload Discipline

Problem: Runtime material instance scalar writes (`Material.SetFloat/SetBuffer/SetTexture`) are easy to accidentally move into hot paths and can fragment batching discipline.
Solution: Retained a single `MaterialPropertyBlock` for the UI instanced draw and moved quad buffer, atlas, and scalar shader payload into that block. Per-glyph data still travels through `GraphicsBuffer.LockBufferForWrite` and `UnsafeUtility.MemCpy`.
Rejected Alternatives: Per-instance material mutations and per-renderer material clones were rejected. Full BatchRendererGroup was not introduced in this pass because the prompt specifically demanded `DrawMeshInstanced` and the current shader contract is already a single structured-buffer path.
Scalability potential: Low uses the same buffer with `_LowTierMode`; Ultra keeps shader visual overkill without changing CPU layout.
Hardware Impact: Avoids material mutation drift; no measured microsecond claim without Frame Debugger/profiler.

## Decision 08: Unity Asset GUID Hygiene

Problem: New `.cs` and `.shader` assets without `.meta` files let Unity generate random GUIDs at import, creating unstable references across machines.
Solution: Added fixed `.meta` files for `WristHologramHudRuntime.cs`, `HudHologramTunerWindow.cs`, and `Hecton_WristHudSDF.shader`.
Rejected Alternatives: Letting Unity generate GUIDs on first import was rejected because this is a multi-agent workspace and asset references need deterministic IDs.
Scalability potential: None; asset hygiene only.
Hardware Impact: None; asset hygiene only.

## Decision 09: Raw H8Dump Blackbox Contract

Problem: The fatal blackbox path still used a `.bin` filename and a `BinaryWriter` field loop, which contradicted the latest `.h8dump` requirement and made dump structure less auditable.
Solution: Added `WristHudBlackBoxDumpHeader` as a 32-byte aligned DTO, writes `Dump_SHINOBU_07.h8dump`, and streams the header plus raw `WristHudTelemetryEntry` vault memory through `ReadOnlySpan<byte>` in the fatal path. No hot-path File I/O was introduced.
Rejected Alternatives: Keeping the older `.bin` path was rejected because the latest user mandate explicitly requires `.h8dump`. Rebuilding the dump through per-field `BinaryWriter.Write` was rejected because it duplicates the struct contract and hides stride mistakes.
Scalability potential: None for visuals; this is survival evidence. Low and Ultra tiers share identical dump ABI.
Hardware Impact: Fatal-only IO. No frame-time claim; main value is deterministic postmortem payload and fewer moving parts during NaN failure.

## Decision 10: Double-Buffered GPU Upload and CSV Byte Parser

Problem: Static self-audit found two local rot points. First, `UploadQuads()` skipped structured-buffer uploads when the quad count stayed unchanged, even though wrist matrices, glyph payload, colors, and glitch intensity can change every frame at identical count. Second, the CSV parser used `File.ReadAllText`, so the parser was span-based only after allocating a managed string.
Solution: Replaced the single HUD structured buffer with double-buffered `GraphicsBuffer` A/B. Upload now writes the non-active buffer via `LockBufferForWrite`, promotes it to the active MaterialPropertyBlock buffer, and gates repeated uploads by `state.FrameIndex + count`, not count alone. Removed runtime `Shader.Find`; player builds now require a serialized shader reference while editor can load the asset by path. Replaced CSV text loading with a fixed 8192-byte scratch buffer and byte-span ASCII parser for uint/float tokens.
Rejected Alternatives: Keeping the count-only skip was rejected because it can freeze GPU DTO data while the CPU thinks the HUD updated. Keeping `File.ReadAllText` was rejected because it contradicts the CSV zero-GC bridge claim. Runtime `Shader.Find` was rejected by the diegetic UI mandate; failing closed is better than a forbidden scene-wide shader search.
Scalability potential: Low/MX350 avoids stale GPU buffer hazards and only pays one bounded memcpy for active quads. High/Ultra keep the same ABI and spend the saved correctness margin on denser glitch/radar visuals without changing gameplay truth.
Hardware Impact: Expected impact is correctness and stall-risk reduction, not a measured microsecond claim. Double-buffering avoids locking the same buffer the GPU may still be consuming; CSV changes remove a cold/editor managed string allocation from the typography bridge.

## Decision 11: Special-Quad Payload Integrity

Problem: Shader self-audit found that special quads called `SpecialAlpha()` with `float4(input.uv, 0, 0)`. That replaced the CPU-authored `UVRect` payload with interpolated atlas UV. Depth fill, O2 vignette intensity, compass phase, and radar payload could therefore drift from the vault DTO while the CPU telemetry still looked correct.
Solution: Added a dedicated `payload : TEXCOORD5` varying and assigned it from `data.uvRect` in the vertex shader. Special quads now call `SpecialAlpha(input.code, input.localUv, input.payload)`. The shader render queue is `Transparent+10` to keep the wrist hologram above standard transparent glass without touching gameplay materials.
Rejected Alternatives: Repacking the DTO or adding extra CPU-side per-special fields was rejected because the existing 112-byte stride already contains the payload. Recomputing these effects on CPU was rejected as a violation of the Dear Lie rule.
Scalability potential: Low/MX350 still gets the cheapest alpha fakes with correct scalar payload. Middle/High/Ultra can push stronger depth shimmer, compass ticks, and radiation flicker without changing the vault ABI.
Hardware Impact: No measured microsecond claim. The fix removes a visual correctness bug at zero CPU cost and keeps shader-side fake physics intact.

## Compile Wall 01: External Missing Types

Problem: `dotnet build Hecton8.Core.csproj` fails before SHINOBU_07 verification due missing external types in `BinaryLayoutManifest`, `EcosystemRuntimeInstaller`, `VRSomaticRuntimeBootstrap`, `HectonSeismicTideDirector`, and a readonly assignment in `GlobalWorldSampler`.
Solution: Per 3-strikes protocol, SHINOBU_07 code was not reverted because build output contains no errors in `WristHologramHudRuntime.cs`, `HudHologramTunerWindow.cs`, `Hecton_WristHudSDF.shader`, or the `WristHud*` BufferID additions. Verification is marked dependency-blocked until the integrator resolves those files.
Rejected Alternatives: Editing unrelated ecosystem/seismic/somatic/world sampler code was rejected as cross-domain sabotage.
Scalability potential: None; compile-wall note only.
Hardware Impact: None; compile-wall note only.

## Compile Wall 02: External Construction DTOs

Problem: Third compile pass failed on construction drone types (`DroneFleetTuningConstants`, `MockSdfGrid`, `PathWaypointDTO`, `DroneNativeMinHeapNode`, `DroneAStarTelemetry`, `DroneFleetAutomationStats`, `DroneFleetDebugRoute`) after parallel workspace changes shifted the visible error front.
Solution: Maintained SHINOBU_07 files; compiler output still contains no SHINOBU_07 path errors. This confirms a dependency wall, not a wrist-HUD regression.
Rejected Alternatives: Stubbing construction drone DTOs from UI domain was rejected; that would hide another agent's ownership problem and pollute the interface surface.
Scalability potential: None; compile-wall note only.
Hardware Impact: None; compile-wall note only.

## Compile Wall 03: External Drone/Ecosystem Helpers

Problem: Fourth compile pass after the Vault-handle refactor failed on `Construction/DroneFleetManager.cs` missing `ResolveDroneVaultBuffer` / `RegisterNativeArrayIfFallback` and `AI/Ecosystem/ShinobuEcosystemBalancer.cs` readonly field assignments.
Solution: Stopped rebuild spam after verifying the output contains no SHINOBU_07 paths. Static scans were used for the final DTO-size correction and dependency audit.
Rejected Alternatives: Adding helper stubs from UI domain was rejected as cross-domain sabotage.
Scalability potential: None; compile-wall note only.
Hardware Impact: None; compile-wall note only.

## Compile Wall 04: External WakeRequestSignal

Problem: Fifth focused compile after SHINOBU GPU/CSV polish failed in `GlobalPhysicsStateManager.cs` because `WakeRequestSignal` is missing at lines 119 and 1343.
Solution: Stopped at the UI domain boundary. The compile output contains no SHINOBU_07 paths, so this remains an external physics/contracts dependency wall.
Rejected Alternatives: Defining or stubbing `WakeRequestSignal` from SHINOBU UI was rejected as signal fragmentation and cross-domain sabotage.
Scalability potential: None; compile-wall note only.
Hardware Impact: None; compile-wall note only.

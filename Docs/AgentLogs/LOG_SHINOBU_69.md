# LOG_SHINOBU_69

## 2026-05-19 - VFX Reentry Started

What was wrong: `SHINOBU_69` status/rationale files contained stale SaveSystem WAL/RLE state while the current user request and second batch prompt assign Volumetric Plasma / Beam VFX.

What was done: Stale SaveSystem files were archived. A fresh PlasmaBeam status/rationale/log set was created.

Cinematic Cheats used: Beam plasma is treated as a procedural scrolling tube, not particles or volumetric raymarching.

Exact Microseconds saved: Not measured yet. Static estimate: low path writes 720 vertices for 20 beams; ultra writes 19,200 vertices. Unity Profiler pending.

## 2026-05-19 - Procedural Indirect Beam Path

What was wrong: Existing first-party tools still contain legacy line-renderer beam/tracer visuals, and the existing ToolKinematics beam DTO does not match the required SHINOBU_69 `BeamVertexDTO` layout. No `beam_visual_profiles.h8bin` payload was present.

What was done: Added `Hecton8.VFX.PlasmaBeam.ShinobuPlasmaBeamRuntime`, explicit 32B beam vertices, 128B beam states, 8B trig LUT entries, mock laser signals, acoustic echo taps, 300-frame telemetry, CSV tuning, `Plasma Beam Tuner`, scene wireframe inspection, Unity `.meta` files, and `Hecton8/VFX/PlasmaBeamIndirect` shader. Added vault IDs `71120..71128` and documented the path in `Docs/ARCHITECTURE/SHINOBU_69_VOLUMETRIC_PLASMA_BEAM.md`.

Cinematic Cheats used: The beam is not plasma simulation. It is a deterministic tube with Simplex radius crackle, UV scrolling, shader bands, silt tint, and a white end-ring flare.

Exact Microseconds saved: Static estimate only. Low quality: 20 beams * 2 length * 3 radial * 6 = 720 vertices, noise bypassed. Ultra: 20 * 20 * 8 * 6 = 19,200 vertices. Avoided per-beam renderer/mesh rebuild and separate spark spawn; Unity Profiler pending. Build was not launched because CPU load was 100%.

## 2026-05-19 - Ultra-Polish Reconciliation

What was wrong: Smooth-only length scaling could round q=0.1 to 3 length segments, so thermal low was not the exact 2-segment collapse promised in the audit. Mock beam RNG was deterministic per frame, but not explicitly sector-seeded.

What was done: Added `SectorHash` to the 64B runtime scalar DTO, mixed it into mock RNG, changed length density to `math.step(0.30, q) * smoothstep((q - 0.30) / 0.70)`, and gated Simplex crackle with the same `math.step`.

Cinematic Cheats used: The beam remains a UV-scrolled emissive tube; no particle plasma, raymarching, or LineRenderer fallback was introduced.

Exact Microseconds saved: Static geometry delta at q=0.1 is 120 vertices per beam compared with prior rounded 3-segment path. Simplex saves one `noise.snoise` evaluation per emitted vertex below q=0.3. Compile still pending CPU guard.

## 2026-05-19 - Dispatch Devirtualization Pass

What was wrong: Local phase adapters used an abstract base and virtual overrides before entering the project dispatcher interface.

What was done: Replaced the abstract base with four sealed `IDispatcherSystem` adapters. Each adapter declares one phase and directly forwards to the runtime.

Cinematic Cheats used: None; this is architectural hot-path hygiene.

Exact Microseconds saved: Sub-us expected. Removes one avoidable local virtual layer; dispatcher interface dispatch remains the core contract boundary.

## 2026-05-19 - Compile Wall Attempt

What was wrong: Static scans cannot prove Unity/Burst import. CPU initially stayed above the project build guard threshold.

What was done: Waited until CPU dropped below 50% with no `dotnet`/`csc`, then launched one constrained `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies /p:UseSharedCompilation=false /nr:false /m:1 -v:q /clp:ErrorsOnly`.

Cinematic Cheats used: None.

Exact Microseconds saved: No runtime savings. Build failed after 42.5s on 6 non-VFX errors: `ShinobuFloraFaunaSymbiosisSolver.cs` missing `math.reversebytes`, `HomeostasisBrain.ScalabilityDictator.cs` unassigned `sanitizedWeight`, `SaveBinaryPayloadCodec.cs` missing `IndustrialLoreBitMask`, and two Visor files missing `HectonDrsRenderFeatureGate`. Generated `Hecton8.Core.csproj` does not yet include new `Assets/_Project/Scripts/VFX/PlasmaBeam` files, so Unity/Burst validation remains pending.

## 2026-05-19 - VisualSync Allocation Firewall

What was wrong: GPU resource creation was cold-path intended but still reachable from `VisualSyncTick` if a resource became invalid. Shader motion used Unity `_Time.y`, separate from the CPU Simplex phase.

What was done: `VisualSyncTick` now calls `EnsureGraphicsResources(false)` and draws only when buffers/material are already resident. Boot remains the only path allowed to allocate GPU buffers/material. Shader scroll/flicker now uses `_H8PlasmaFrameTime` bound from dispatcher frame * fixed tick/fallback 1/60.

Cinematic Cheats used: The beam remains a deterministic UV-scrolled tube; only the time source changed.

Exact Microseconds saved: No normal-frame saving claimed. Prevents worst-case multi-ms VisualSync allocation hitch and removes CPU/GPU visual clock drift.

## 2026-05-19 - PlasmaBeam Compile-Wall Isolation

What was wrong: PlasmaBeam files were still under the parent source compile surface, and `EnsureVaultState` reacquired all vault handles plus layout sizes on every dispatcher phase after boot.

What was done: Added `Hecton8.VFX.PlasmaBeam.Runtime.asmdef` and `Hecton8.VFX.PlasmaBeam.Editor.asmdef`. Runtime references Core/Core.Contracts/Core.Memory plus Unity packages only. Added `_layoutChecked/_layoutValid` and a fast path so initialized vault handles are reused through generation-checked `Resolve`.

Cinematic Cheats used: None; this is compile-wall and hot-path hygiene. The visual cheat remains the UV-scrolled procedural tube.

Exact Microseconds saved: Not measured. Static work removed from steady phases: 9 `GetBufferHandle` calls and 8 `UnsafeUtility.SizeOf` layout probes per phase after initialization. Unity asmdef import/build proof is still pending.

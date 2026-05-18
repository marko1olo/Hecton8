# LOG_SHINOBU_18

## 2026-05-17 - Volumetric Silt Compute Pass

What was wrong:
- Ambient marine snow/silt risked CPU position ownership and Shuriken-style mental model.
- Primary DTO alignment was not proven for the 16-byte GPU ABI mandate.
- Wake/flow/submarine/acoustic producers are parallel-agent domains, so direct dependencies would create compile-wall coupling.
- Telemetry did not literally track `DispatchedParticleCount` or write the prompt-required `Dump_SILT_VFX.bin`.
- Low tier lacked a literal `_MATH_LOD_LOW` compute variant for wake-loop removal.

What was done:
- Added `VolumetricSiltContracts.cs` with 32-byte `ParticleDataDTO`, `ParticleRenderMetaDTO`, `DynamicWakeDTO`, `MockWakeSignal`, `MockAcousticSignal`, `MockFlowField`, `VfxConfigurationDTO`, ref-return DTO access, and zero-split CSV parser.
- Reworked `HectonMarineSnowRenderer` to use GPU ping-pong particle/meta buffers, Vault tuning/wake/flow handles, mock wake/flow/acoustic signals, 300-frame telemetry ring, and mock GPU microsecond blackbox threshold.
- Updated `Hecton_MarineSnow.compute` to consume `ParticleDataDTO`/`DynamicWakeDTO`, perform GPU advection, AUP-stable curl fake, SDF collision, frustum respawn, low-tier wake bypass, sonar/acoustic impulse, and GPU initialization.
- Updated `Hecton_MarineSnow.shader` to consume the split 32-byte DTO buffers and apply URP main-light plus water-extinction LUT noir scattering.
- Added `VolumetricSiltTunerWindow` editor facade for live Vault tuning and SceneView wake visualization.
- Added Vault BufferIDs `MarineSnowTuningConstants`, `MarineSnowDynamicWakes`, and `MarineSnowMockFlowField`.

Cinematic cheats used:
- No Navier-Stokes. Silt motion is curl/hash fake + global flow + wake vectors + simple Euler integration.
- Density is faked by camera-forward respawn instead of simulating invisible far-field particles.
- Low tier compiles out dynamic wake flow through `_MATH_LOD_LOW`.
- Acoustic blast is a radial shader impulse, not physical pressure propagation.
- Deep-sea noir scatter is a shared shader tint/LUT, not per-particle lighting truth.

Microseconds saved:
- Exact measured microseconds saved: 0 proven. Unity Profiler/GCMonitor/RenderDoc were not available in this terminal pass.
- Estimate only: avoiding a 100k CPU particle loop would save roughly 3000-8000 us/frame on MX350-class CPU.
- Estimate only: one procedural indirect path avoids roughly 500-1500 us/frame versus CPU-built quads/objects.
- Estimate only: low-tier 100k-to-10k dispatch and wake-loop bypass saves roughly 200-300 us GPU/frame plus wake-loop pressure.
- Estimate only: GPU init avoids roughly 1000-4000 us gameplay allocation/seed spikes.

Verification:
- Static scan of touched runtime files found no `[StructLayout(Pack=1)]`, `ParticleSystem`, `new NativeArray`, `Allocator.Persistent`, `Material.SetFloat`, LINQ, or `.ToString()` in runtime hot files. Editor-only `.ToString()` remains in the tuner window labels.
- `git diff --check` passed for touched files with only CRLF warnings.
- Core build remains blocked by external compile-wall errors outside SHINOBU_18: missing ambient/ecosystem/seismic/somatic DTOs/jobs/signals and a world-sampler readonly assignment. After fixing SHINOBU_18's `uint` telemetry field, filtered build output showed no remaining SHINOBU_18 C# errors before the external errors.
- Runtime/Unity import/Play Mode/Profiler proof is absent. Status remains PENDING VERIFICATION.

## 2026-05-17 - Polish Delta

What was wrong:
- CSV hot reload still performed main-thread file existence, timestamp, and stream reads. That violates the Steam Deck/MicroSD stall mandate even if the cadence was low.
- Blackbox used the XML task `.bin` path only; the polish mandate demanded `.h8dump`.
- Editor tuner contained manual `.ToString()` labels, editor-only but noisy for static zero-GC scans.

What was done:
- Added `H8_SiltCsvReader` background thread with 4096-byte staged buffers. Main thread now only consumes staged bytes and writes unmanaged Vault tuning.
- Changed primary blackbox path to `Docs/AgentLogs/Dump_SILT_VFX.h8dump` and kept `Docs/AgentLogs/Dump_SILT_VFX.bin` as a prompt-compatible legacy mirror.
- Removed manual `.ToString()` calls from `VolumetricSiltTunerWindow`.

Cinematic cheats used:
- No change to gameplay truth: CSV, dump, and editor polish only.

Microseconds saved:
- Exact measured microseconds saved: 0 proven.
- Estimate only: moving CSV file polling off the main thread removes possible 50-5000 us MicroSD stalls during profile edits or antivirus/file-lock contention.

Verification:
- `git diff --check` passed for changed files with CRLF warnings only.
- Filtered Core build still reports external compile-wall errors before any SHINOBU_18 C# errors.
- Static scan now has no `.ToString()` in touched runtime/editor files except none reported by the targeted scan.

## 2026-05-17 - Forensic Follow-Up

What was wrong:
- CSV hot reload had already been moved off the main thread, but the private refresh method still accepted an unused `dt` argument, which made the hot path look time-polled.

What was done:
- Removed the unused `dt` parameter and call-site clamp.
- Repeated static forbidden-pattern checks.
- Confirmed `git diff --check` reports only CRLF warnings.
- Confirmed SHINOBU BufferIDs 622-624 are present once in `H8Memory.cs`.

Cinematic cheats used:
- Unchanged. Low tier still compiles out the dynamic wake loop with `_MATH_LOD_LOW`; high tier keeps DynamicWakeDTO and acoustic fake impulse for noir turbulence.

Exact Microseconds saved:
- No measured runtime delta from the signature cleanup.
- Main protected gain remains the previous CSV main-thread file-IO removal: hot frame file-IO cost is 0 us by design, pending profiler validation.

Verification:
- A final `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies --no-incremental /p:UseSharedCompilation=false /nr:false /m:1` exceeded 120 seconds and left a `dotnet` process, which was stopped.
- Status remains PENDING VERIFICATION / BLOCKED BY DEPENDENCY, not green.

## 2026-05-17 - Bandwidth Discipline Polish

What was wrong:
- Frame constants, empty fallback buffers, and mock wake pages still used tiny managed upload arrays before mapping into GraphicsBuffers. Cheap, but not titanium under the bandwidth mandate.

What was done:
- Removed managed upload arrays for frame constants, empty flow/abyssal sentinels, and mock wake DTO/vector payloads.
- Added direct `GraphicsBuffer.LockBufferForWrite` helpers inside `HectonMarineSnowRenderer`.
- Added wake DTO sanitization before upload; inactive wake pages are now cleared directly in mapped GPU memory.

Cinematic Cheats used:
- Unchanged. Wake turbulence is still vector falloff plus hash/curl fake, not a fluid solve.

Exact Microseconds saved:
- Measured proof absent.
- Expected runtime gain is small; the material improvement is auditability and lower managed-memory surface on the GPU upload path.

Verification:
- Targeted forbidden-pattern scan found no `ParticleSystem`, `Pack=1`, `UploadArray(`, LINQ, `.ToString()`, `Instantiate`, or runtime `GetComponent` in touched SHINOBU files.
- `git diff --check` reports only CRLF warnings.
- Remaining managed arrays in SHINOBU runtime are CSV staging bytes and immutable quad mesh topology, not particle simulation authority.

## 2026-05-18 - Hot-Path Stall Polish

What was wrong:
- CSV hot reload no longer performed file IO on the main thread, but staged-byte consumption still used a blocking `lock` in `Tick()`.

What was done:
- Replaced the main-thread staged-buffer lock with `Monitor.TryEnter`.
- If the background CSV reader is copying staged bytes, `Tick()` now skips consumption and retries next frame.
- Background-side staging still uses a normal lock because it is off the gameplay frame path.

Cinematic Cheats used:
- Unchanged. The fake remains curl/hash silt advection, wake vectors, and camera-forward respawn.

Exact Microseconds saved:
- Measured proof absent.
- Normal-frame cost is expected unchanged; worst-case lock-wait spike is removed from the hot path.

Verification:
- Targeted static scan remains clean for forbidden SHINOBU patterns.
- `git diff --check` reports only CRLF warnings.
- Unity Profiler/GCMonitor proof remains absent; status stays PENDING VERIFICATION.

## 2026-05-18 - Shader NaN Vaccination

What was wrong:
- Marine-snow compute/shader code still had local reciprocal/root sites that depended on upstream clamps instead of guarding the operation at the call site.

What was done:
- Guarded remaining local `rcp()` denominators with `max(..., EPSILON)` or `max(..., 0.0001)`.
- Guarded respawn radial `sqrt()` with `max(hash, 0.0)`.

Cinematic Cheats used:
- Unchanged. The visuals remain fake-current and wake-vector driven, not physical fluid truth.

Exact Microseconds saved:
- None claimed. This is a safety hardening pass, not a performance win.

Verification:
- PCRE scan now finds no `rcp(`, `rsqrt(`, or `sqrt(` call in touched SHINOBU shader files without a local `max(...)` guard.
- `git diff --check` reports only CRLF warnings.

## 2026-05-18 - Cache-Line and Visual-Fake Polish

What was wrong:
- `FrameConstantsData` was 112 bytes. Legal, but not a clean two-line payload for the final cache-line audit.
- Private `VehicleWakeJobResult` was 40 bytes. Legal 8-byte multiple, but not a clean 16-byte lane layout.
- Mock wake/flow proof jobs still used `math.sin` / `math.cos`.
- Unbound camera fallback could probe components every tick.

What was done:
- Padded `FrameConstantsData` to 128 bytes and mirrored `float4 Pad0` in both marine-snow compute and render shader frame structs.
- Padded `VehicleWakeJobResult` to 48 bytes with explicit `uint Pad0` / `uint Pad1`.
- Replaced mock wake/flow trig with signed triangle-wave fakery.
- Added a 30-frame cold retry gate for fallback camera component resolution.

Cinematic Cheats used:
- Triangle waves replace trigonometric proof turbulence. It is controlled visual motion, not fluid truth.

Exact Microseconds saved:
- 0 measured. No profiler/RenderDoc capture was run.
- Estimate only: normal-frame gain is negligible when correctly bound; worst-case unbound component probing drops from every tick to about twice per second at 60 fps.

Verification:
- Attribute-aware extraction of `<AGENT_PROMPT id="SHINOBU_18" ...>` succeeded and still contains 20 tasks.
- Static scans found no `math.sin`, `math.cos`, unsafe local runtime `math.rcp`, shader `rcp/rsqrt/sqrt` without local `max`, `ParticleSystem`, `Pack=1`, `SetData`, `GetData`, `UploadArray`, LINQ, `.ToString()`, or CPU particle loops in touched SHINOBU files.
- `git diff --check` reports only CRLF warnings.
- Compile/runtime/profiler proof remains absent; status stays PENDING VERIFICATION.

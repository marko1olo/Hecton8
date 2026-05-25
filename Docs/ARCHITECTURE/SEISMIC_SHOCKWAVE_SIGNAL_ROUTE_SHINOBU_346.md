# Seismic Shockwave Signal Route - SHINOBU_346

Owner: `HectonSeismicTideDirector` (`SystemID.HabitatAtmosphere`).

Runtime truth:
- `SeismicEventDTO`: 32B, `double3 EpicenterAUP@0`, `float MagnitudeRichter@24`, `uint EventTypeHash@28`.
- `SeismicStateDTO`: 64B vault row for birth time, P/S radii, frequency, decay, frame, flags, sequence.
- `SeismicTelemetryEntry`: 64B blackbox row; dump header is `SeismicTelemetryDumpHeader=32`.
- `SeismicSignal`: 96B SignalBus packet. Legacy presentation fields remain at offsets `0..31`; AUP/radius/magnitude fields occupy offsets `32..95`.
- `SeismicSignal.Flags`: `FlagRadialWave=0x80` marks AUP/radius stress truth; `FlagPresentationOnly=0x40` marks legacy camera/audio/turbidity presentation packets. Legacy quality remains masked to the lower nibble.

## Route

- The owner schedules deterministic Burst `EvaluateSeismicPropagationJob` from vault-owned arrays.
- The job advances P and S radii from `H8TimeSeconds
- BirthTimeSeconds`.
- Job finite-gates payloads with `TryFinalizeSeismicSignal` and `TryFinalizeShockwaveSignal` before `ParallelWriter.Enqueue`.
- It clamps scalars, normalizes direction, sanitizes `Reserved0`, enforces `FlagRadialWave`, and drops invalid epicenter/magnitude packets.
- The job emits unmanaged `SeismicSignal` and legacy `SeismicShockwaveSignal` lanes only after that producer-side vaccine.
- Core `SignalPayloadFiniteGuards` also owns fixed `SeismicSignal` and `SeismicShockwaveSignal` cases, so `TryPush` and frame flush no longer fall through to `GuardNone` for seismic payloads.
- Legacy `GlobalSignals.Publish(in SeismicSignal)` sanitizes before updating `_latestSeismicSignal`; `TryGetLatestSeismicSignal` can no longer expose a malformed legacy publish before SignalBus flush.
- Structural, vehicle, KCC, camera, and VFX consumers subtract their own `double3` AUP from `SeismicSignal.EpicenterAUP` before casting the local delta to `float3`.
- Structural/base/boat consumers must treat `SeismicSignal.FlagRadialWave` plus finite `MagnitudeRichter`/P/S radii as the stress-truth route. `FlagPresentationOnly` packets are visual/audio tremor outputs and do not own structural stress.
- `SeismicWaveMath.CalculateSeismicDisplacement` returns zero early when `FlagRadialWave` is absent.
- It performs AUP delta in double before local float math.
- It sanitizes radius/magnitude/amplitude/intensity fields.
- Any non-finite output returns zero.

Forbidden route:
- No `Physics.OverlapSphere`.
- No `Rigidbody.AddExplosionForce`.
- No camera transform coroutine shake.
- No direct base or boat stress mutation from the environment owner.
- No seismic `CombatDamageSignal` fan-out; base/boat/habitat owners compute stress from `SeismicSignal` snapshots.

Tide lie:
- Celestial tide writes one double scalar to `WaterSurfaceAupYBuffer`.
- Ocean rendering and buoyancy consumers can read that scalar; CPU terrain/water meshes are not deformed.

Cold data:
- `tectonic_fault_profiles.csv`, when present, hydrates `SeismicFaultProfileDTO[16]` through a `ReadOnlySpan<byte>` cursor parser. Missing CSV falls back to one deterministic emergency profile.
- `GenerateMockSeismicEventsJob` is the cold/editor synthetic cataclysm injector for the tuner and writes event/state Vault rows directly through unmanaged pointers.
- Fault dumps write a 32B header plus raw oldest-to-newest `SeismicTelemetryEntry[300]` bytes through `ReadOnlySpan<byte>`.
- Final Data Monolith readiness is not claimed by this route.

Static proof:
- `Tools/OOP_Explosion_Scanner.py` scans Environment/Events C# files and records namespace/type/member context for forbidden explosion API sites.
- `OOP_Explosion_Scanner.cs` is the scoped Roslyn `CSharpSyntaxTree` scanner for member-access and unqualified `OverlapSphere` calls.
- Findings include namespace/type/member context.
- Isolation: `Hecton8.Environment.Editor.asmdef`.
- Assembly is editor-only, has Roslyn precompiled refs, and zero runtime assembly refs.
- When run from Unity it writes both `PHYSICS_OPTIMIZATION_REPORT_SHINOBU_346_ROSLYN.json` and the shared `PHYSICS_OPTIMIZATION_REPORT.json` section `SHINOBU_346_OOP_Explosion_Scanner_Roslyn`.
- Unity menu execution is pending until a legal Editor/compile window exists.
- Guarded `Hecton8.Core.csproj` compile attempt reached an external Construction/Habitat namespace wall (`CS0234 Hecton8.Habitat` in untracked hatch files); no SHINOBU_346 green compile claim is made.
- Latest SHINOBU_346 scan result: `OOP Seismic Forces Eradicated`, `seismicExplosionApiSites=0`; latest build command was blocked before launch by in-command guard at `cpu=100`, `compilerProcesses=8`.

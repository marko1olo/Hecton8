# SHINOBU_234 Surface Storm Abyssal Propagation

Status: SUPERSEDED STATIC NOTE / PENDING VERIFICATION

Current route card:

`Docs/ARCHITECTURE/SURFACE_STORM_ABYSSAL_PROPAGATION_SHINOBU_234.md`

Supersession:

This older note is retained only to prevent stale cross-links from reviving the rejected direct-mutation route. The current implementation does not mutate `FogConstantsDTO`, `BiolumPulseStateDTO`, ocean swell DTOs, or audio DSP objects.

Current owner:

ECHELON 7 Atmosphere & Celestial / Weather & Wind Director.

Current route:

- Surface weather truth remains `BufferID.ShinobuOceanWeatherState` when present; SHINOBU does not create or mutate that upstream row.

- If upstream weather row is absent/invalid and emergency mock toggle is enabled, SHINOBU uses `MockHurricaneStateDTO`.
- That row feeds the same attenuation job for CI/dev stress scenes.

- Storm attenuation writes a hidden 96-byte `StormPropagationWriteSnapshotDTO` containing the 32-byte `StormPropagationDTO` plus four scalar `float4` snapshots.

- Stable read lane is `BufferID.ShinobuStormPropagationState = 71712`; hidden write lane is `BufferID.ShinobuStormPropagationWriteState = 71713`.

- Producer-only scalar lanes are `ShinobuStormPropagationFlowScalar = 71721`, `ShinobuStormPropagationAudioScalar = 71722`, `ShinobuStormPropagationBiolumScalar = 71723`, and `ShinobuStormPropagationFogScalar = 71724`.

- Public scalar lanes are not locked or written by worker job.
- Late-frame publication locks four scalar rows only for all-or-nothing owner publication window.
- It copies stable state/scalar rows after `DispatcherJobFence` finalization.

- Downstream fog, biolum, audio, and flow owners must consume those scalar lanes in their own owner phases; no downstream consumer is claimed in SHINOBU_234 proof.

Phase:

- Active route runs through `ShinobuStormPropagationRuntime`.

- The runtime is scene-local auto-installed after scene load. It does not use `DontDestroyOnLoad`.

- Scheduled work finalizes through `DispatcherJobFence`; no raw `JobHandle.Complete()` is present in the SHINOBU runtime route.

Proof Required:

- Unity import and Console compile.

- Play Mode validation of DataVault buffers and zero GC.

- Profiler sample proving propagation under 5 microseconds on the target scene.

- Downstream owner-phase consumers for the four scalar lanes.

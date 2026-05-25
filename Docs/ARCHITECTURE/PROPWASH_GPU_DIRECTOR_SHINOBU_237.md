# SHINOBU_237 Propwash GPU Director

Domain: presentation-only silt and propwash rendering.

Authority route:

- CPU owns only compact thrust DTO harvest into `GlobalDataVault` buffers `PropwashGpuEventRing`, `PropwashGpuRingCursor`, `PropwashGpuTelemetryRing`, `PropwashGpuTuning`, and `PropwashGpuWakeProfiles`.

- `PropwashEventDTO` is the GPU wire payload: 32 bytes, `float3 LocalPosition`, `float3 ThrustVector`, `float Intensity`, `float Radius`.

- `PropwashWakeProfileDTO` is a cold editor/source-data tuning payload: 64 bytes, FNV-1a engine hash plus emission, lifetime, turbulence, radius, intensity, lift, tint, curl, and jitter scalars from `Assets/_SourceData/VFX/Propwash/vehicle_wake_profiles.csv`.

- CSV parser is byte-span based and fail-closed for numeric tokens.
- Trailing bytes after a parsed number reject the field/row instead of hydrating partial values.
- Optional wake-profile columns may be absent or empty.
- Malformed present optional values reject the row.

- Player builds do not read propwash CSV from `StreamingAssets`.
- CSV staging buffers, background reader, file IO, and parser refresh compile only under `UNITY_EDITOR`.
- Non-editor/player lifecycle calls are no-ops.
- Until `.h8bin`/Data Monolith hydrates `PropwashGpuWakeProfiles`, runtime uses deterministic default rows.

- CPU upload to `_PropwashEvents` is double-buffered (`_propwashEventBufferA/B`) through `LockBufferForWrite`; the inactive buffer receives the next frame before becoming the compute read buffer.

- Upload consumes `PropwashRingCursorDTO.WriteCursor` and `EventCount`, computes the wrapped oldest slot, and writes a GPU snapshot. The Vault ring remains circular; the shader sees a dense linear buffer.

- `Hecton_MarineSnow.compute` owns SDF/depth proximity, particle injection, propwash advection, AUP rebase, and indirect-visible count mutation.

- `Hecton_MarineSnow.compute` tags propwash silt with particle flag bit 3. Shader consumes the bit and `_PropwashBiomeTint.rgb`.
- Biome color reaches the visible material pass without widening particle stride.

- `HectonMarineSnowRenderer` submits through non-indexed `Graphics.DrawProceduralIndirect`; the indirect args buffer is 16 bytes and the CPU never reads the GPU visible particle count.

Rollback/netcode exclusion:

- Propwash particles, event cursor, telemetry, biome tint, and tuner values are visual presentation state only.

- Gameplay authority remains vehicle kinematics, submarine physics, SDF collision truth, and save/network state owned by their existing domains.

- No `PropwashGpu*` buffer is a rollback/Merkle descriptor. GlobalQualityWeight may scale sample budget and particle budget, but never DTO layout, gameplay truth, save identity, or authority route.

Scalability:

- Low: 4 propwash event samples, low marine-snow particle budget, cheap radial/lift approximation.

- Middle: continuous increase of event sampling and curl response.

- High: denser SDF/depth-reactive silt injection and stronger biome tint variation.

- Ultra: consumes the full 500-event mock/harvest stress payload and spends GPU budget on visual overkill density.

- Wake proximity SDF/height dispatch uses the same continuous event sample budget as propwash flow sampling.
- Low quality reduces advection event reads and near-floor silt injection work.
- Vault ring shape does not change.

Black box:

- `PropwashTelemetryEntry` stores 300 frames in `PropwashGpuTelemetryRing`.

- On black-box dump, raw telemetry bytes are written to `Docs/AgentLogs/Dump_SHINOBU_237.bin`.

Rejected:

- Unity `ParticleSystem.Emit()` for propwash/silt.

- `Physics.Raycast` or `RaycastNonAlloc` for cosmetic seabed proximity.

- CPU visible-particle counts or managed vehicle lists in the hot path.

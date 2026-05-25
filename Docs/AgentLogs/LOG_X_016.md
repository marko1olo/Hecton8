# LOG X_016

## 2026-05-25 - Spatial Audio DSP and Portal Graph Scout

What was wrong:
- Native audio ring descriptor likely rejects `WriteIndex` alignment: bridge requires 8-byte pointer alignment, but `WriteIndex` points to int shared-state slot 1 (`base + 4`).
- Acoustic portal pathfinding is bounded but synchronous through `pathJob.Execute()`.
- Virtual voice lifecycle spans 1000 request capacity, 64 physical selections, and Unity `AudioSource` hydration; these are separate limits.
- Several explicit-layout DTOs are ARM64-size-aligned but contain unnamed tail padding.
- Adjacent audio synthesis modules still expose `OnAudioFilterRead`; player-critical renderer does not.

What was done:
- Read active `X_016` batch prompt from `Docs/Tasks/CURRENT_BATCH.md`.
- Read AGENTS authority, domain map, and eight audio/DTO/native-memory mandates.
- Audited `SpatialAudioManager.cs`, portal propagation DTOs, virtualization contracts/jobs, HullStress granular DSP kernel, player-critical renderer, SPSC ring, native bridge, and signal-lane contracts.
- Wrote `Docs/Reports/AUDIO_DSP_SCOUT_REPORT_X_016.json`.
- Wrote `Docs/Reports/AUDIO_DSP_SCOUT_REPORT_X_016.md`.
- Updated `Docs/Tasks/Status_X_016.md` and `Docs/AgentLogs/Rationale_X_016.md`.

Cinematic cheats used:
- Portal graph uses bounded habitat/voxel macro routes instead of full wave propagation.
- Occlusion uses dear-lie/SDF taps scaled by continuous `GlobalQualityWeight`, not full acoustic simulation.
- Depth low-pass, Sabine approximation, ITD pan, and low-pass presentation are perceptual fakes, not physical acoustics.

Exact microseconds saved:
- 0 us measured and 0 us claimed. This was a read-only audit with no runtime code changes.

Compile:
- Not run. C# source was not modified.

## 2026-05-25 - APEX Override Addendum

What was wrong:
- Initial report was too broad for overload prevention; it lacked exact formulas for virtual voice culling and exact proof that `AcousticPortalNode` is compact but not cache-line-isolated.

What was done:
- Re-read `VirtualVoiceSortJob.Execute`, `ResolveSdfLineOcclusion`, `SortKeysDescending`, `ResolveVirtualVoicePriority`, `FastTick` sort scheduling, `PublishAudioParameterSnapshot`, `ProduceAudioBlock`, `RenderHullStressBlock`, `RenderStructuralGranularVoices`, `ResolveGranularVoiceSlot`, and `TryWriteInterleaved`.
- Appended APEX formulas and exact offsets to `Docs/Reports/AUDIO_DSP_SCOUT_REPORT_X_016.md`.

Cinematic cheats used:
- Acoustic portal routing remains macro graph propagation, not full wave simulation.
- SDF occlusion uses 1..8 taps from continuous quality, not continuous volumetric acoustic integration.
- Hull stress uses stochastic granular grains, pressure-bed noise, and soft clipping rather than physical shell resonance simulation.

Exact microseconds saved:
- 0 us measured and 0 us claimed. This was documentation and audit refinement only.

Compile:
- Not run. C# source was not modified.

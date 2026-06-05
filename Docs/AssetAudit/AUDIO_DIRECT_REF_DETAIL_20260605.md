# Audio Direct Ref Detail - 2026-06-05

Status: `PENDING VERIFICATION`.
Evidence class: `STATIC_SOURCE`.
Scope: direct `AudioClip` references statically found in `Assets/_Project/Prefabs/Player.prefab` through `Docs/Audio/audio_profile_usage_20260605.csv`.

No Unity run, prefab edit, import edit, listening pass, build, profiler, Addressables operation, or asset mutation was performed. This file proves serialized direct refs only.

CSV companion: `Docs/AssetAudit/AUDIO_DIRECT_REF_DETAIL_20260605.csv`.

## Summary

| Priority | Disposition | Rows | Static Meaning |
|---|---|---:|---|
| P0 | `P0_AMBIENT_DIRECT_REF_BLOCKED` | 2 | `Underwater Ambient.wav` is directly serialized in `Player.prefab`; owner/release/ducking proof is absent. |
| P0 | `P0_SPLASH_DIRECT_REF_CLASSIFICATION_BLOCKED` | 2 | `dive_splash.wav` duplicate direct refs need lifecycle classification. |
| P1 | `P1_FOOTSTEP_DIRECT_REF_OWNER_BLOCKED` | 20 | Footstep direct refs need owner, import readback, playback route, and no-allocation proof. |
| P1 | `P1_UI_DIRECT_REF_AUDIBILITY_BLOCKED` | 4 | UI direct refs need HUD/UI audibility, route ownership, and import readback proof. |

## Owner Rules

- Do not treat direct prefab serialization as Addressables ownership.
- Do not mutate import settings from this table alone.
- Do not remove or reroute refs without Unity prefab readback and owner route decision.
- Future owner must prove load/release path, playback path, mixer/DSP route, and no-allocation behavior before acceptance.

## Regression Model

- CPU: static CSV derivation only.
- GC: no runtime code touched; no no-allocation claim.
- Memory/residency: direct refs only; no handle/release proof.
- Cadence: no playback cadence changed.
- Correctness: direct-ref blockers are row-addressable for future audio owner.

Final status: `PENDING VERIFICATION`.

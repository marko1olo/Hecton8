# Audio Direct Ref Detail - 2026-06-05

Status: `PENDING VERIFICATION`.
Evidence class: `STATIC_SOURCE`.
Scope: direct `AudioClip` references statically found in the current `Assets/_Project/Prefabs/Player.prefab` source state.

No Unity run, prefab edit, import edit, listening pass, build, profiler, Addressables operation, or asset mutation was performed. This file proves serialized direct refs only.

CSV companion: `Docs/AssetAudit/AUDIO_DIRECT_REF_DETAIL_20260605.csv`.

## Summary

| Priority | Disposition | Rows | Static Meaning |
|---|---|---:|---|
| P1 | `P1_FOOTSTEP_DIRECT_REF_OWNER_BLOCKED` | 20 | Footstep direct refs need owner, import readback, playback route, and no-allocation proof. |
| P1 | `P1_UI_DIRECT_REF_AUDIBILITY_BLOCKED` | 4 | UI direct refs need HUD/UI audibility, route ownership, and import readback proof. |

Current static prefab scan reports `0` direct `Underwater Ambient.wav` refs. Current static prefab scan reports `0` direct `dive_splash.wav` refs. The working-tree prefab diff shows prior `m_Resource` and `_driverClip` refs set to `{fileID: 0}` and the waterline splash cue moved off prefab `AudioClip` refs. This is not runtime acceptance: Unity prefab readback, import readback, route ownership, playback proof, listening proof, memory/residency proof, and 0 B/frame proof remain absent.

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
- Correctness: remaining footstep/UI direct-ref blockers are row-addressable for future audio owner; cleared ambient and splash refs are still pending Unity prefab readback and route proof.

Final status: `PENDING VERIFICATION`.

# Status 2301 - ATMOSPHERE_FOG_WRITER_LIVE_ROUTE_AUDITOR

Status: COMPLETED_STATIC_AUDIT / PENDING_UNITY_OWNER_PROOF

## Relevant Mandates
- Surface, sky, coastline, ocean surface, and 0-100 m photic water must be bright, readable, and Subnautica-level or better. Darkness/fog cannot hide weak art.
- Visual fake first: fog/haze/water presentation should use deterministic shader/LUT/particle fakes unless gameplay truth requires physical simulation.
- Fog is a visibility and route-readability tool; generic blue fog, green swamp cast, transparent empty underwater, and pure black void are rejected.
- `GlobalQualityWeight` must scale fog/haze fidelity continuously; no binary low/high quality switches.
- Any runtime fog/water presentation route adding >0.1 ms needs profiler proof, quality gate, and load-shed behavior.
- Runtime acceptance requires fresh Unity/capture/profiler evidence. Static serialized values are not live proof.

## Work Done
- Inventoried static writers for `RenderSettings.fog`, `fogColor`, `fogDensity`, ambient settings, and shader fog/water globals.
- Inventoried underwater force/detection routes from source and scene YAML.
- Checked required 1473 screenshots as visual evidence.
- Wrote Batch23 matrix report and CSV.
- Appended concise log facts.

## Result
- Likely live fault: surface/underwater ownership mismatch plus green-biased serialized/profile sources; `_useAutoUnderwaterDetection: 0` allows false surface atmosphere state while camera-local underwater visuals may still render.
- Hard reject gates remain: green swamp, transparent empty underwater, darkness hiding weak terrain, false underwater label.

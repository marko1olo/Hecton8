# Rationale 1805

Status: COMPLETE
Evidence mode: STATIC ONLY.

## Authority Rules Recorded

- Static text/source inspection is not Unity import, compile, Play Mode, profiler, GC, Frame Debugger, Memory Profiler, player-build, device, or screenshot proof.
- Claims must be labeled by evidence class. A higher claim without artifact is downgraded to PENDING_VERIFICATION.
- "Done", "complete", "verified", "0 GC", timing, platform, release, or visual quality claims require artifact path, command/tool, timestamp, target scene/hardware where applicable, and unresolved failures.
- Dated reports and old batch logs are evidence snapshots only. Root authority wins over stale controller prompts and stale doctrine.
- Visual acceptance for surface, sky, Aegir, moons, coastline, ocean surface, photic shallows, and medium-depth hero routes requires current captures. Static reports cannot prove the Subnautica-level floor.
- No code/assets/scenes/Unity operations are in scope for 1805. Controller output must produce triage, rejection criteria, and next-wave recommendations only.
- Unity-slot tasks must not conflict with the active verifier. Without current proof, mark them PENDING UNITY SLOT.

## Non-Trivial Decisions

- Decision: Use evidence-class downgrading as the dashboard spine.
  Reason: AGENTS.md, quality.md, testing.md, release.md, and QA_Evidence_Text_Filter_Audit all reject runtime acceptance from static reports.

- Decision: Treat static source blocker verification as STATIC_SOURCE only.
  Reason: The task explicitly forbids Unity control and asks not to fabricate runtime proof.

- Decision: Downgrade the reported ProceduralWreckGenerator runtime mesh fallback blocker to STALE/OVERSTATED.
  Reason: Current source keeps `BuildMergedMesh*` under `#if UNITY_EDITOR`, `ShouldBuildMergedMeshFallback()` returns `!Application.isPlaying`, and the merged-mesh methods return `null` while playing. A player-runtime fallback was not proven.

- Decision: Downgrade the reported MissionMarkerSystem runtime marker mesh/material fallback blocker to STALE/OVERSTATED.
  Reason: Current source has no `CreateMarkerMesh()` hit. `EnsureRuntimeResources()` only validates assigned `markerMesh` and `markerMaterial`; invalid resources disable markers rather than fabricating fallback assets.

- Decision: Keep DynamicMusic and VocalBank audio routes as CONFIRMED STATIC_SOURCE blockers.
  Reason: Both files still declare `OnAudioFilterRead(float[] data, int channels)`. DynamicMusic copies native buffers into the managed callback array; VocalBank pins the callback array, locks runtime views, decodes, and records callback timing with `Stopwatch`.

- Decision: Reword world-truth blockers from "mock SDF" to "SDF/substrate runtime proof missing."
  Reason: GPR and Drone source paths use `IVoxelSonarSdfReadLeaseModel` leases, and Foundation reads `VoxelSdfTexture3D` from the WorldStreaming owner. Foundation explicitly fails closed and warns when substrate is missing. Static source does not prove the real substrate is present in the current Unity route.

- Decision: Use latest 1804 report as current AppliedLore/DataMonolith state.
  Reason: A newer 1804 report appeared during 1805. It shows direct AppliedLore packet binary parity passes on current `static_data.h8bin`, while normal source/full audit fails on `P151_BLACK_KEEL_CONTRACT_APPROACH/ru_RU` generated status drift. It also verifies P456 source/public residue. Therefore older P288 stale-binary mismatch is historical unless rerun reproduces it.

- Decision: Mark 1771 P456 clean claim UNSAFE for future publication work.
  Reason: Current `external_site/ru_RU/P456_SITE_HOME_LONGFORM_BRIEF.md` still has production-brief markers and mojibake markers despite 1771's final clean scan claim.

- Decision: Treat 1428 as scoped old PlayMode evidence, not current first-20 acceptance.
  Reason: 1428 contains useful Play Mode screenshots and console proof for its route, but later agents changed source/content/scene-relevant systems and no current first-20 profiler/player proof was found by 1805.

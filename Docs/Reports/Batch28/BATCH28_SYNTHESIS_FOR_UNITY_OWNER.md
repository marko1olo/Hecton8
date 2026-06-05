# Batch28 Synthesis For Unity Owner

Date: 2026-06-04 21:58 +04:00.
Controller: local portfolio orchestrator.
Evidence class: `STATIC_SOURCE` / `STATIC_DOC` / `STATIC_FILESYSTEM`.
Runtime proof status: `PENDING UNITY VERIFICATION`.

## Current Verdict

`1474` remains `REJECTED`.

The later `Docs/Screenshots/MCP/h8_1908_surface_runtime_ui_on.png` is a single raw surface screenshot. It is not a proof packet, does not contain manifest/checksums/camera/depth/quality/toggles/log path, and does not change the reject state.

Unity is active with ILPP and shader compiler processes in the latest controller sample. No build was launched by the controller.

## Direct Controller Patches Pending Unity Proof

Files touched before this synthesis:

- `Assets/_Project/Scripts/SeamGapDitherRenderer.cs`
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`
- `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs`

Patch intents:

- `SeamGapDitherRenderer` now clears pending draw/debug state on disable and treats all `GraphicsBuffer` instances as one coherent set during recreate/release.
- `GameBootstrapper` now publishes `GlobalRegistry.UnderwaterVisuals` from the scene runtime publication gate during scene activation reference resolution.
- `HectonUnderwaterVisuals` no longer self-publishes the underwater visual service from arbitrary runtime `OnEnable()`.

These are source changes only. They are not proof. Required Unity verification:

- fresh import/compile without new `error CS` or script compilation failure;
- fresh reload/play-exit log;
- no `SeamGapDitherRenderer.EnsureBuffers`, `GraphicsBuffer:.ctor`, or `Persistent allocates` stack from this renderer;
- no ready-lock rejection for `HectonUnderwaterVisuals`;
- one active enabled underwater owner;
- `GlobalRegistry.UnderwaterVisuals` bound to `H8_UNDERWATER_VISUALS_RUNTIME_OWNER_1474` or its accepted replacement scene owner.

Important ownership note: `HectonUnderwaterVisuals.cs` already had unrelated uncommitted edits before the controller patch. The controller-owned change there is only the removal of direct `GlobalRegistry.RegisterUnderwaterVisualsRuntime(this)` from runtime `OnEnable()`.

## Batch28 Findings

### 2801 Proof Harness

No first-party HECTON proof harness exists for `1475`.

Current capture routes can emit PNG files, but they do not bind view id, route id, camera pose, water depth, underwater owner state, UI state, continuous quality, material/post-stack toggles, checksums, or a clean post-capture log window.

Required route:

- add side-effect-free proof snapshots from actual runtime owners;
- add an authored `1475` route capture rig with fixed view ids and predicates;
- add an editor-only owned harness under `Docs/Screenshots/HectonProofPackets/h8_1475_{session_id}/`;
- write `manifest.json`, `manifest.sha256`, copied log, screenshot SHA256/dimensions/timestamps, and derived gate results.

### 2802 False Underwater Route

The rejected `underwater_0_5m` and `underwater_20_50m_route` images failed because current capture accepts filename intent as truth.

Future underwater proof must reject before or at manifest gate unless:

- `underwater_0_5m` camera depth is `>= 0.25m` and `<= 5.0m`;
- `underwater_20_50m_route` camera depth is `>= 20.0m` and `<= 50.0m`;
- `HectonUnderwaterVisuals` reports underwater active for the exact capture camera;
- the depth zone owner reports exact depth, zone min/max/hash, and contains-depth result;
- the route owner confirms the authored route segment and camera anchor;
- the image itself shows premium water-column/route evidence, not a surface/coast/Aegir horizon with a false filename.

### 2803 Shoreline / Foam / Photic Terrain

The shoreline route remains blocked.

Static blockers:

- active broad photic terrain still binds `MAT_H8_PhoticRouteTerrain_1464`, which uses rejected wet-basalt lineage as its only texture input;
- no accepted normal/MRAO/wetness/shell/sand/contact mask stack is bound;
- active close foam is a transparent ribbon using generic `foam.png`, not accepted contact-owned foam/salt/wetness mask proof;
- floor caustics are a transparent sine/additive fake with no depth/light/receiver ownership;
- `Ocean-Underwater.mat` now has nonzero caustics, but clip/transparency keywords remain a slab/cut risk until runtime proof;
- no current Docs-generated shoreline source is `READY_FOR_UNITY_IMPORT`.

Do not darken, haze, overdrive caustics, or raw-enable curtains/slabs to hide this.

### 2804 Aegir / Sky Route

Primary sun route remains:

- `PrimarySunDiscOwner=SkyMaterial`;
- owner driver: `HectonCelestialEngine`;
- primary material: `Assets/_Project/Art/Materials/Mat_HectonSky.mat`;
- primary shader: `Assets/_Project/Art/Shaders/Hecton_AlienSky_Master.shader`;
- Aegir material: `Assets/_Project/Art/Materials/Celestial/MAT_AegirGasGiant_Impostor_1428.mat`.

Rejected quick fix:

- do not activate `SURFACE_LOW_SUN_DISC_1428`;
- do not wire it as the primary sun proof route;
- do not treat its inactive/renderer-disabled flat material as accepted debt.

Required source follow-up:

- make `PrimarySunDiscOwner=SkyMaterial` a first-class route/proof field;
- make `HectonUnderwaterVisuals` sun visual warnings route-aware so missing mesh sun is not logged as a blocker when sky material owns the sun;
- require fresh Aegir crop proof for rim, veil, seam, sticker, stripe, dirty-noir hiding, and texture residency.

### 2805 Proof Gate Tool

A local static proof packet validator has been implemented:

- `Tools/ProofGate/validate_proof_packet.py`
- `Tools/ProofGate/test_validate_proof_packet.py`

Verification run:

```text
python -m unittest discover -s Tools/ProofGate -p test_*.py
```

Result:

```text
Ran 17 tests
OK
```

Control run against the raw MCP folder:

```text
python Tools/ProofGate/validate_proof_packet.py --packet-root Docs/Screenshots/MCP --packet-id h8_1474 --session-id mcp --strict
```

Result:

```text
REJECTED_STATIC_GATE
RAW_PNG_SET
```

Reports:

- `Docs/Reports/Batch28/ProofPacketGate_h8_1474_mcp.json`
- `Docs/Reports/Batch28/ProofPacketGate_h8_1474_mcp.md`

The validator is a static gate only. It cannot accept visual quality, runtime correctness, profiler state, or player capture truth. It can block raw PNG sets, missing manifests, missing production views, bad hashes/dimensions, binary quality labels, false route predicates, invalid underwater depth bands, dirty/stale/short logs, dirty tokens inside a declared log-offset window, screenshot-under-Assets contamination, `.meta` sibling contamination, and diagnostic substitution.

## Required Unity Owner Next Packet

Next acceptable packet must be `1475` or newer, produced through the owned harness route, not raw MCP filenames.

Required production views:

- `01_surface_coast_aegir_ui_off.png`
- `02_shoreline_close_1m.png`
- `03_underwater_0_5m.png`
- `04_underwater_20_50m_route.png`
- `05_aegir_celestial_long.png`
- `06_regression_low_oblique.png`

Required packet files:

- `manifest.json`
- `manifest.sha256`
- copied `UnityEditor_{packet_id}_{session_id}.log`
- screenshots under `screenshots/`

Required manifest proof:

- camera position/rotation/FOV/source per view;
- route id, route anchor, route predicate pass/fail;
- exact depth, water level, underwater active state, depth zone min/max/hash;
- UI state;
- continuous `GlobalQualityWeight` and `qNNN` label, not binary low/high;
- render scale current/target;
- material/post-stack toggle hashes;
- sky/Aegir route metadata;
- screenshot SHA256, dimensions, byte size, timestamps;
- clean post-capture log window newer than the final screenshot.

## Reject Conditions

Reject immediately if:

- screenshots are raw MCP names without manifest;
- underwater views are surface-looking again;
- shoreline close view does not show real 1 m wet-contact/foam/material proof;
- there is no foam, no caustic receiver proof, no water-column depth, or no route cue;
- Aegir/sun proof uses `SURFACE_LOW_SUN_DISC_1428` as the primary route;
- Aegir crop shows rim, veil, seam, sticker, stripe, or dirty-noir hiding;
- log contains compile/import/domain reload/ILPP/MCP transport/leak/ready-lock noise inside the proof window;
- any source patch is claimed fixed before fresh Unity reload/play-exit proof.

## Scalability Consequences

Low: same six route truths and manifest schema, lower resolution/cadence only. No haze/darkness cover-up.

Middle: baseline 1475 harness packet, clean log, and route predicates.

High: spend extra budget on foam breakup, wet-contact materials, caustic receiver detail, water volume, sky/Aegir polish, and route landmarks.

Ultra: add richer diagnostics and visual-overkill toggles, but do not change route authority, gameplay truth, DTO identity, save identity, or manifest schema.

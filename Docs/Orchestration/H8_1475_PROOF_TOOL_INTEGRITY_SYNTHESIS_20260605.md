# H8 1475 Proof Tool Integrity Synthesis - 2026-06-05

Status: `PENDING VERIFICATION`.
Evidence class: `STATIC_SOURCE`, `STATIC_DOC`.
Subagent source: Godel, proof-tool integrity audit.

No Unity run, Play Mode, scene save, prefab save, material save, import, profiler, Frame Debugger, project-setting mutation, Addressables build, or runtime code mutation was performed.

## Current Verdict

`Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs` is rejected for canonical `h8_1475` acceptance. It can produce diagnostic rejection screenshots only. It must not be used to promote the game state, because current capture paths either lack the `h8_1475` packet contract or mutate editor scene/render state before capture.

First-20 route impact: this blocks false promotion of scenic water/shore/sky screenshots while active player, HUD, movement, tool route, no-mutation readback, and visual floor are still unproved.

## Exact Static Anchors

| Anchor | Finding |
|---|---|
| `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs:20` | `CaptureRoot` writes to raw `Docs/Screenshots/MCP`, not `Docs/Screenshots/HectonProofPackets/h8_1475_*`. |
| `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs:28` | Public methods emit `h8_1912`, `h8_1913`, and `h8_1914`; no current `h8_1475` output path exists. |
| `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs:189` | `ApplySurfaceCrestRecoveryProbe` begins editor-state mutation before capture. |
| `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs:203` | Assigns a temp Crest material and serialized `OceanRenderer` fields. |
| `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs:216` | Calls `ApplyModifiedPropertiesWithoutUndo`. |
| `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs:227` | Creates temporary `HideAndDontSave` Crest material and writes probe colors/floats. |
| `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs:249` | Mutates MapMagic graph/settings, pins tile, refreshes, and starts generation. |
| `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs:315` | Creates temp horizon haze material, activates object, moves/scales it, assigns `sharedMaterial`. |
| `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs:413` | `QuarantineSurfaceRejectsAndExit` begins destructive quarantine utility. |
| `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs:442` | Disables renderers. |
| `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs:457` | Marks scene dirty and saves scene. |
| `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs:488` | Uses raw `Camera.Render`, `ReadPixels`, `EncodeToPNG`; not a proof packet manifest path. |
| `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs:531` | Writes text metadata, not a ProofGate manifest. |
| `Tools/ProofGate/validate_proof_packet.py:46` | ProofGate requires canonical view set, not arbitrary MCP images. |
| `Tools/ProofGate/validate_proof_packet.py:55` | ProofGate requires manifest fields and strict screenshot metadata. |
| `Tools/ProofGate/validate_proof_packet.py:456` | ProofGate rejects diagnostic substitution. |

Current source no longer references the old deleted `H8_SurfaceWaterReadability_1428.shader` path. Current diagnostic temp haze path is `Assets/_Project/Art/Shaders/H8_SurfaceHorizonHaze_1428.shader`. Stale references to the deleted water-readability probe remain historical rejection context only when explicitly tied to old `h8_1914_surface_water_recovery_probe` artifacts.

## Rejection-Only Methods

- `CaptureSurfaceAndExit`: raw diagnostic screenshot/text only.
- `CaptureSurfacePatchAAndExit`: raw diagnostic screenshot/text only.
- `CaptureWithPoseAndExit` and underwater patch wrappers: rejection-only because they move the camera in memory and do not prove player route ownership.
- `CaptureSurfaceCrestRecoveryProbeAndExit`: rejection-only at best; it applies temp Crest, terrain, and haze mutations before capture.
- `QuarantineSurfaceRejectsAndExit`: not proof tooling. It disables renderers and saves the scene.

## Canonical h8_1475 Blockers

- No `h8_1475` output path.
- Raw MCP output, not proof-packet output.
- No `manifest.json`, no `manifest.sha256`, no copied Unity log, no canonical six production screenshots.
- Editor-only visual probes mutate Crest, terrain generation, haze, GameObject activation, transforms, and materials.
- Scene quarantine path can alter `02_HECTON_WORLD.unity`.
- Diagnostic predicate logic is name/token-based, not route-owner-state based.
- Diagnostic images can reject failures but cannot substitute production views.

## Required Repair Route

1. Do not extend `H8VisualProofCapture1912` for canonical acceptance.
2. Create a new no-mutation harness under `Assets/_Project/Scripts/Editor/Proof/`.
3. Output only to `Docs/Screenshots/HectonProofPackets/h8_1475_{session}/`.
4. Hard-ban `SaveScene`, `MarkSceneDirty`, `ApplyModifiedPropertiesWithoutUndo`, renderer disabling, temp material assignment, MapMagic generation, hidden haze probes, and any scene state mutation.
5. Use route-owned production cameras/anchors and read-only serialized readback. No editor cheat camera for canonical proof.
6. Read Crest, terrain, sky, HUD, player, and route predicates without mutating them. Write readback JSON into the packet.
7. Capture the six ProofGate views, compute hashes/sizes/dimensions, write manifest and `manifest.sha256`.
8. Copy Unity log and include offsets plus at least 60 clean post-capture seconds.
9. Run `Tools/ProofGate/validate_proof_packet.py` in strict mode after a clean process gate.

## Low / Middle / High / Ultra Consequences

- Low: proof still uses the same route predicates and production state; lower visual density is allowed only if the surface/shallow floor remains beautiful and readable.
- Middle: same truth route with normal production density and clean UI policy.
- High: longer sightline and richer material/lighting proof, no changed route truth.
- Ultra: extra polish captures may exist, but canonical acceptance remains the same six-view no-mutation packet plus route/HUD/player proof.

Final status: `PENDING VERIFICATION / PROOF_TOOL_RISK`.

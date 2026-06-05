# Rationale 3101 - UNITY_SCENE_DIFF_OWNER

Date: 2026-06-05
Status: STATIC VERIFIED / PENDING EDITOR VERIFICATION

## Analysis

Target: `Assets/_Project/Scenes/02_HECTON_WORLD.unity` dirty diff after 1912 quarantine/capture work.

Affected systems: scene visual composition, surface/photic water readability, shoreline foam, caustic presentation, sky/Aegir/sun objects, camera/light proof state.

Zero GC proof: not applicable to this static triage. No runtime code changed. No runtime proof claimed.

State check: current scene diff is dirty; Unity/editor action blocked by CPU 100 percent plus active `dotnet` and `UnityShaderCompiler`; no scene save or Unity mutation performed.

Rule quote: static text search and YAML parsing are source evidence only; no runtime or visual acceptance claim is allowed without Unity/editor/profiler/capture proof.

## Decision

The scene diff must remain under per-object Unity owner review. Static evidence is enough to reject blind acceptance, blind restore, and blind deletion. It is not enough to certify cleanup.

## Reasoning

`H8VisualProofCapture1912.cs` contains a quarantine route that disables renderers and saves the scene. The quarantine text reports `disabledCount=3`, while the scene diff contains broad active-state, renderer, transform, prefab/fileID, material, camera, and light churn. Therefore only three active renderer disables can be tied with high confidence to the 1912 quarantine run.

The surface/photic route cannot be cleaned by hiding ugly objects if no replacement exists. Foam, water mass, caustics, terrain, sky, and Aegir objects may be visually failed but still represent route-critical functions. The correct queue is restore/replace/delete/keep-disabled per group after Unity readback and valid proof packet.

## First-20-Minutes Route Effect

This removes a proof-hygiene blocker for the bright semi-open surface/photic exit route. It does not improve visuals or gameplay by itself.

## Low / Middle / High / Ultra Consequences

Low: keep route silhouettes, water clarity, sky/Aegir readability, and shoreline cues. Do not accept black slabs, green water sheets, or missing water masses.

Middle: require richer material response, semantic foam/contact masks, and readable underwater density before cleanup is accepted.

High: spend recovered cost on better waterline, terrain material breakup, Aegir atmosphere, particles, fauna, and route landmarks.

Ultra: visual overkill only after compact readability and route truth hold. No quality tier changes gameplay truth.

## Residual Risk

Static YAML cannot prove visual outcome, scene hierarchy behavior, prefab override intent, hidden child renderers, or runtime camera/sky/sun state. Unity owner readback is required.

# Flow Field Visualizer Hardening

Date: 2026-04-02
Area: `Assets/_Project/Scripts/FlowFieldVisualizer.cs`

## Context

After compile blockers were removed, the next senior-pass targeted the editor
flow-field tooling used to tune water/current gameplay. The goal was not
cosmetic cleanup, but behavioural correctness and resource hygiene.

## Fixed Issues

1. Job path ignored `ShowGlobalCurrent`

- `FlowSamplingJob` always sampled `CurrentManager.SampleCurrent(...)`.
- Result: the visualizer still displayed phantom/global current even when
  `ShowGlobalCurrent` was disabled.
- Fix: added explicit `ShowGlobalCurrent` gating inside the job and passed
  engine parameters only when the global source is actually enabled.

2. Local-only job sampling incorrectly depended on `HectonFluidEngine`

- Async/job recalculation previously downgraded or changed behaviour when the
  fluid engine instance was absent, even if the user only wanted authored local
  `CurrentVolume` data.
- Fix: job path now supports local-only mode without `HectonFluidEngine`.

3. Preview particle pool left hidden editor objects alive

- Temporary preview particles were released back into an internal queue, but the
  queue itself was never torn down on component disable.
- Result: hidden editor objects could outlive the active visualization session.
- Fix: added pool disposal and full preview cleanup on disable.

4. Sample buffer hardening

- Buffer allocation logic assumed `_samplePositions` implied matching
  `_flowVectors` and `_flowMagnitudes`.
- Fix: allocation guards now validate all three arrays together in both sync and
  async completion paths.

5. Programmatic draw safety

- `DrawFlowField()` could be called from tests or tooling outside a valid GUI
  drawing context, while debug labels still reached `Handles.Label`.
- Fix: debug panel now exits early when no current GUI event exists.

## Verification

- Added regression coverage in
  `Assets/_Project/Scripts/Editor/FlowFieldVisualizerTests.cs` for the
  `local-only + job-path + no fluid engine` scenario.
- Updated `Assets/_Project/Scripts/FLOW_FIELD_VISUALIZER_README.md` to document
  the corrected source-selection contract and preview cleanup behaviour.

## Gameplay / Tooling Impact

- Designers now get truthful visualization when disabling global phantom flow.
- Local current volumes remain inspectable even when the runtime fluid engine is
  unavailable in the current editor context.
- Editor preview no longer leaves stale hidden particle objects behind between
  tool sessions.

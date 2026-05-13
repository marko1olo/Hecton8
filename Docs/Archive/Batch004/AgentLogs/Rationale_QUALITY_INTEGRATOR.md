# Rationale_QUALITY_INTEGRATOR

Problem: User requested continued AAA-quality improvement without a specific subsystem.
Solution: Treat this as a meta-quality integration pass. Start with evidence: git state, Unity console, compile/test output, then fix the narrowest confirmed defects.
Rejected Alternatives: No broad refactor, no speculative optimization, no edits outside confirmed defect scope, no overwrite of other agents' concurrent work.
Scalability potential: Low/Middle/High/Ultra runtime tiers are protected by refusing blind bloat. Actual scalability changes require profiler evidence.
Hardware Impact: 0 us/frame until runtime code changes are made and measured.

Problem: Hecton8.Vehicles.VFX failed standalone Unity Bee Roslyn compile: HullDentShaderController called SystemDispatcher.CurrentFrameUnscaledDeltaTime from a separate asmdef. The property is internal to Hecton8.Core, so the assembly boundary made the call invalid.
Solution: Cache the public ITickDispatcher dependency during enable/registration, then use its TimeSnapshot.UnscaledDeltaTime in LateFrameTick with sanitized Time.unscaledDeltaTime fallback. This keeps dispatcher-owned timing when available, obeys hot-path registry discipline, and avoids widening Core API surface.
Rejected Alternatives: Making SystemDispatcher.CurrentFrameUnscaledDeltaTime public would expand Core API for one VFX consumer. Polling GlobalRegistry.TickDispatcher from every LateFrameTick would violate the hot-path service cache mandate. Copying generated DLLs into Library/ScriptAssemblies would mask Unity postprocess state instead of fixing source. Using scaled Time.deltaTime would make repair fade pause-sensitive and break unscaled late-frame intent.
Scalability potential: Low uses the same O(1) scalar read and existing dent cap. Middle/High/Ultra do not gain extra simulation cost; visual overkill remains controlled by existing hull dent count and shader upload path.
Hardware Impact: Expected 0 us/frame regression. One cached-interface read and scalar read in LateFrameTick, no allocation, no collection walk, no registry lookup.

Problem: Fresh Core validation failed after concurrent fauna edits: PredatorCognitionJob called ResolveRuntimePosition after the outer helper was renamed to ResolveTelemetryRuntimePosition.
Solution: Add a private static ResolveRuntimePosition helper inside PredatorCognitionJob, matching the SwarmAnalysisJob AUP-to-runtime conversion shape. The Burst job keeps its own deterministic math helper and no longer depends on telemetry naming.
Rejected Alternatives: Calling ResolveTelemetryRuntimePosition from PredatorCognitionJob would compile but couples gameplay job math to telemetry helper semantics. Reverting the whole fauna file would discard other agent work. Refactoring shared AUP conversion across jobs would be outside the compile-wall scope.
Scalability potential: Low/Middle/High/Ultra unchanged; the retinal light path uses the same O(light count) loop and scalar AUP conversion. No added simulation fidelity and no removed visual fidelity.
Hardware Impact: Expected 0 us/frame regression; duplicate static math compiles into the same style of arithmetic already used by SwarmAnalysisJob.

Problem: New Power/RTG asmdefs and RTG editmode test were absent from the stale Bee response inventory, making earlier SaveData missing-field errors ambiguous.
Solution: Build temporary validation response files for Power.Generators.Contracts, Power.Generators, RTG editmode test, and optional World.Dots. Refresh Core ref first, then compile those sources against the current Core ref.
Rejected Alternatives: Editing SaveData RTG fields was rejected because current SaveData source and fresh Core ref already expose rtgDecayCount, rtgDecaySourceIds, rtgStartTimesSeconds, rtgDecayFlags, and MaxRtgDecayRecords. Enabling optional DOTS globally was rejected because its asmdef define constraints mark it as conditional.
Scalability potential: Validation-only. RTG decay math and DOTS placeholders are not changed. Low/Middle/High/Ultra runtime behavior unchanged.
Hardware Impact: 0 us/frame; no runtime code modified in Power/RTG or World.Dots.

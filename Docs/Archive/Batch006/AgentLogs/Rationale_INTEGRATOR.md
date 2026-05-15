# Rationale_INTEGRATOR

Agent: GRAND_INTEGRATOR_KRASAVCHIK
Domain: Echelon 9 / The Integrator (Compile Medic)

## Decision 0 - Assignment Source and Hygiene

Problem: The batch protocol requires extracting the agent prompt from `Docs/Tasks/CURRENT_BATCH.md`, but the current batch file and archived batch files do not contain `GRAND_INTEGRATOR_KRASAVCHIK`.
Solution: Treat the explicit user-supplied XML prompt as the operative assignment, log the source violation, and continue because the user supplied the full prompt cover-to-cover in the active session.
Rejected Alternatives: Waiting for a new batch file was rejected because the project compile wall is the assigned emergency. Guessing neighboring batch tasks was rejected because the strict parser forbids cross-agent contamination.
Scalability potential: Low = compile medic can proceed without importing unrelated prompts; Middle = status/rationale files survive context compression; High = future batch hygiene can be corrected without losing this integration trace; Ultra = compile graph stabilization lets high-end visual systems re-enable normal verification.
Hardware Impact: 0 us runtime impact. This is process containment only.

## Decision 1 - Initial Mandate Selection

Problem: The integration task crosses Core asmdefs, registry lookup discipline, signal lanes, save alignment, bootstrap, world streaming, and native disposal.
Solution: Read the registry, bootstrap, zero-GC, native-memory, debug telemetry, AUP, save, and world-streaming mandates before coding.
Rejected Alternatives: Reading only AGENTS.md was rejected because the task touches specialized technical mandates. Reading all skill files was rejected because it wastes context and risks unrelated-domain drift.
Scalability potential: Low = avoids hot-path registry and allocation regressions on MX350/i3; Middle = keeps signal/registry contracts coherent; High = preserves native job throughput; Ultra = keeps saved cycles available for visual overkill rather than compile debt.
Hardware Impact: Prevents compile-fix regressions that could add hot-path lookup/allocation overhead; expected direct runtime gain depends on found defects and is not claimed yet.

## Decision 2 - Log Scan Scope

Problem: The prompt requires reading all agent logs, but the log directory is large and includes many domains with stale compile-wall notes.
Solution: Process all `Docs/AgentLogs/*.md` through CLI keyword extraction for compile, asmdef, signal, registry, duplicate, save, leak, AUP, and residency evidence, then read specific files fully when a fix target is identified.
Rejected Alternatives: Dumping every log verbatim into chat context was rejected because it truncates and loses actionable signal. Ignoring logs was rejected because previous agents documented broken dependencies and duplicate hazards.
Scalability potential: Low = fastest route to red-build root causes; Middle = avoids reintroducing known signal/registry mistakes; High = preserves cross-domain boundaries; Ultra = broad compile graph repair restores verification for all visual systems.
Hardware Impact: 0 us runtime impact. Reduces integration thrash.

## Decision 3 - AI Cognition Contract Drift in Dotnet Build

Problem: Full `dotnet build Hecton8.slnx` failed because `PredatorCognitionDomain.cs` referenced `AlphaLeviathanTelemetryFlags.NoPlayerTarget`, while `Hecton8.Core.csproj` was binding the stale `Library/ScriptAssemblies/Hecton8.AI.Cognition.dll` that predates the source contract update.
Solution: Stop injecting the stale AI cognition binary into the Core MSBuild graph and include `AlphaLeviathanCognitionContracts.cs` directly for `Hecton8.Core.csproj` dotnet compatibility. Unity asmdef import remains governed by `Hecton8.AI.Cognition.asmdef`.
Rejected Alternatives: Editing the public telemetry flag use site was rejected because the source contract already contains the field. Hand-copying a binary into `Library/ScriptAssemblies` was rejected because it is generated state and would rot on the next import. Creating a broad AI cognition csproj was rejected as larger build-system churn than needed for the compile wall.
Scalability potential: Low = Core dotnet build no longer depends on stale generated binary state; Middle = AI telemetry flag additions stay source-authoritative; High = future Core compile probes are deterministic; Ultra = clean generated builds unlock domain verification for high-tier visual and AI systems.
Hardware Impact: 0 us runtime impact. Compile-time compatibility only.

## Decision 4 - Audio Virtualization Contract Drift in Dotnet Build

Problem: After the AI cognition fix, `Hecton8.Editor.csproj` failed through Core because `SpatialAudioManager.cs` called the source `VirtualVoiceUtility.ComputeStableKey(eventID, clipHash, stationaryCacheKey, sourceAup)` overload, but Core was still binding stale `Library/ScriptAssemblies/Hecton8.Audio.Virtualization.Contracts.dll`.
Solution: Replace the stale virtualizer binary references in the Core MSBuild compatibility target with source includes for `AudioVirtualizationContracts.cs` and `AudioVirtualizationJobs.cs`.
Rejected Alternatives: Editing `SpatialAudioManager` to match stale binary state was rejected because it would throw away the stationary-cache key and regress voice stability. Rebuilding/copying generated binaries was rejected because Library state is disposable and not source authority.
Scalability potential: Low = virtual voice compile probes now use the same source as Unity import; Middle = stable keys include stationary cache identity; High = virtualizer top-K job remains source-authoritative; Ultra = selected-voice budget can still buy richer acoustic presentation.
Hardware Impact: 0 us runtime impact from this patch. It preserves the existing optimized virtual voice path instead of downgrading it.

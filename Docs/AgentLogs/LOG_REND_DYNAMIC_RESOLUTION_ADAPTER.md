# LOG_REND_DYNAMIC_RESOLUTION_ADAPTER

## 2026-05-14 - Thermal DRS Adapter
What was wrong:
Thermal and frame pressure could reduce performance budgets, but graphics render-scale policy needed a signal-owned DRS adapter that reacts to Homeostasis frame pressure and thermal severity. The old scaler could also overwrite a new adapter if it remained a later dispatcher writer.

What was done:
Implemented/verified the DRS path around IDynamicResolutionRuntime, FrameTimeSignal/SystemHealthSignal consumption, URP DynamicResolutionHandler system scaler, STP/FSR cold-path selection, 0.5-1.0 scale clamp, 0.7 thermal cap, HUD hash notification below 0.6, Android-only XRDisplaySubsystem bridge, and 300-frame fixed NativeArray blackbox dump. OMEGA polish removed duplicate URP/ScalableBufferManager writes when the registry runtime exists and converted fallback percentage divisions to multiply/rcp.

Cinematic Cheats used:
EWMA frame-time scalar instead of per-camera GPU timing. Hard thermal cap instead of heat simulation. STP/FSR upscaling instead of native pixels. Slow +0.01 recovery instead of continuous smoothing curves.

Exact Microseconds saved:
Estimated 2500-8000 GPU microseconds on fill-rate-bound Quest 3/Steam Deck/MX350 frames when render scale drops from 1.0 to 0.7/0.5. OMEGA duplicate-write removal saves an estimated 2-15 CPU microseconds on scale-change frames. Hot-path adapter cost target remains 6-20 CPU microseconds, 0 B/frame.

Verification:
STATUS: PENDING VERIFICATION. Unity MCP has no active session. Unity batchmode aborts because another Unity instance has the project open. dotnet build Hecton8.Core.csproj is not authoritative here: it fails on generated-project/reference state unrelated to this adapter, most recently missing Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs.

## 2026-05-14 - Post-Polish Hardening Pass
What was wrong:
Re-read found that adapter disable could leave the Unity system DRS slot installed, no-runtime fallback could leave URP renderScale downscaled after disable, notification flags were added after runtime snapshot commit, and recovery telemetry could publish every +0.01 upscale step.

What was done:
Added idempotent system DRS release, fallback default render-scale restore, notification flag calculation before commit, 30-frame recovery telemetry throttle with immediate drop telemetry, and disabled save-load target scale restoration in DynamicResolutionScaler.

Cinematic Cheats used:
Kept the same EWMA scalar and hard thermal cap. Chose a coarse telemetry cadence during recovery because blackbox telemetry already holds exact per-frame state.

Exact Microseconds saved:
Estimated 1-8 CPU microseconds saved on recovery frames by suppressing non-critical telemetry publishes. Disable/re-enable now returns the DRS slot and fallback render scale to default state with 0 B/frame.

Verification:
STATUS: PENDING VERIFICATION. Prompt re-extracted from Docs/Tasks/CURRENT_BATCH.md. Unity 6 package source confirms the DRS and URP upscaling APIs used. dotnet build Hecton8.Core.csproj rerun after this pass timed out after 124s and remains non-authoritative until the active Unity session releases the project lock and refreshes generated csproj files.

## 2026-05-14 - Fault Containment Recheck
What was wrong:
The invalid-scale path dumped after detection but could miss the corrupt current frame, and a runtime hot-unbind could leave the no-runtime URP fallback stale until the next scale change.

What was done:
Moved invalid-state protection before target calculation, made blackbox telemetry write the corrupt frame before dumping, committed native scale immediately after invalid recovery, added direct fallback render-scale application on null runtime rebind, and made DynamicResolutionScaler clear system override back to its default render scale.

Cinematic Cheats used:
No new simulation. The adapter still uses the scalar EWMA/hard thermal cap policy and treats invalid state as a deterministic native-scale visual reset.

Exact Microseconds saved:
Normal path cost remains effectively unchanged: two finite checks and one health saturate, estimated below 1 CPU microsecond. Fault path avoids a potential NaN cascade and stale fallback frame state; recovery is immediate instead of waiting for a later scale transition.

Verification:
STATUS: PENDING VERIFICATION. Unity MCP console and telemetry calls failed because 127.0.0.1:8088 was unreachable. Hecton8.Core.csproj build is still non-authoritative for Hecton8.Graphics.DRS; the first post-check build exited 1 without actionable captured diagnostics, the diagnostic rerun timed out after 94s, dotnet build-server shutdown also timed out, and taskkill cleared the remaining spawned dotnet.exe processes.

## 2026-05-14 - Pressure Merge Recheck
What was wrong:
FrameTimeSignal and SystemHealthSignal pressure values were consumed by assignment order, so a lower later signal could erase a higher same-frame escalation before the DRS target was calculated.

What was done:
Merged pressure with max-byte logic across same-frame signal snapshots, while still allowing recovery when both lanes report lower pressure on a later frame.

Cinematic Cheats used:
No simulation. This keeps the scalar pressure policy but makes escalation deterministic across signal ordering.

Exact Microseconds saved:
Adds two byte comparisons per signal, below 1 CPU microsecond. Prevents missed emergency downscale opportunities worth an estimated 2500-8000 GPU microseconds on fill-rate-bound frames.

Verification:
STATUS: PENDING VERIFICATION. Static hot-path scan found no foreach, LINQ, string formatting, ToString, Enumerable, or Unity Update in the touched runtime files. The only List allocation hit is the Android-only XR scratch list behind UNITY_ANDROID && !UNITY_EDITOR.

## 2026-05-14 - Ownership And Telemetry Recheck
What was wrong:
Duplicate adapter lifecycle paths could touch registration, dispatcher registration could be missed if OnEnable ran before the dispatcher was ready, hot-swap ref and compatibility callbacks could apply the same runtime twice, and default-scale startup commits could publish false performance warnings.

What was done:
Added active-owner guards, Start retry registration, same-runtime rebind no-op, SubsystemRegistration DRS slot reset, and observed-scale seeding/native-scale telemetry suppression.

Cinematic Cheats used:
No visual simulation changes. This is ownership hygiene around the scalar DRS policy.

Exact Microseconds saved:
Avoids duplicate hot-swap commits and false telemetry events, estimated 2-10 CPU microseconds on lifecycle/hot-swap frames. Normal runtime cost is one ReferenceEquals guard and one integer scale comparison, below 1 CPU microsecond.

Verification:
STATUS: PENDING VERIFICATION. Static dispatcher inspection confirms HomeostasisBrain.PreSimulationTick runs before IUpdatable lanes, so Core-lane DRS consumes same-frame FrameTimeSignal/SystemHealthSignal data. git diff --check reports no whitespace errors, only CRLF conversion warnings. Stray dotnet.exe processes were cleared with taskkill. Unity Editor verification remains blocked.

## 2026-05-14 - Conservative Signal Merge Recheck
What was wrong:
FrameTimeSignal/SystemHealthSignal merging still let producer order decide EWMA frame time, health index, and foveation tier when more than one same-frame signal was present. Pressure was already max-merged, but the rest of the decision context could still be softened by a later signal.

What was done:
Merged current-frame snapshots conservatively: maximum EWMA frame time, minimum system health index, maximum pressure level, and maximum foveation pressure tier. Cached values are replaced once per signal family so later frames can still recover normally.

Cinematic Cheats used:
Still no per-camera GPU timing or thermal simulation. The adapter uses scalar EWMA and signal tiers as cheap presentation knobs.

Exact Microseconds saved:
Adds only scalar comparisons, estimated below 1 CPU microsecond per frame, while preserving emergency downscale opportunities worth an estimated 2500-8000 GPU microseconds on fill-rate-bound frames.

Verification:
STATUS: PENDING VERIFICATION. Static hot-path scan found no foreach, LINQ, string formatting, ToString, Enumerable, or Unity Update in the touched runtime files. git diff --check is clean apart from CRLF conversion warnings. Unity Editor/MCP verification remains blocked.

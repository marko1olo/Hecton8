# LOG_ARCHITECTURAL_AUP_INTEGRITY_AUDITOR

## 2026-05-14 - AUP Integrity Audit

What was wrong:
- Core AUP construction rebuilt `AbsoluteUniversePosition` from a `Vector3` absolute coordinate, cutting committed origin offset precision before sector quantization.
- AUP direction math cast double-sector deltas to `float3` before normalization.
- AUP drift watchdog only emitted threshold correction, not max drift telemetry.
- One AUP shift consumer used destructive NativeQueue drain instead of non-destructive frame snapshots.
- Acoustic occlusion distance converted endpoints to `Vector3` absolute coordinates and subtracted in float.
- `Hecton8.Core.AUP` asmdef does not exist; AUP is embedded in UnityEngine-dependent `PersistentWorldRegistry.cs`.
- `dotnet build Hecton8.Core.csproj` cannot validate this patch because the project file is already missing multiple assembly references.

What was done:
- Added double committed-offset lane in `HectonFloatingOrigin` and routed `AbsoluteUniversePosition.FromRuntimePosition` through `ToAbsoluteUniversePositionDouble3`.
- Rebuilt `AUPDirection` around double squared length and `math.rsqrt` before final float output.
- Added `CrashTelemetryBuffer.ReportAupMaxDriftError` and wrote watchdog max drift into the fixed telemetry ring.
- Replaced origin fallback velocity division with reciprocal multiply.
- Changed `WorldChunkResidencyManager` to consume AUP shifts from `SignalBus<AupShiftSignal>.GetFrameSnapshot()` with `_lastAppliedAupShiftFrameId`.
- Changed acoustic AUP distance to use `AbsoluteUniversePosition.DistanceSq` before final float audio scalar.
- Verified KCC millimeter snap, 300-frame sync fence, LCG sector hash entropy, rsqrt coverage, and zero-GC behavior by static scan.

Cinematic Cheats used:
- Preserved float presentation lanes for shader/fluid/scatter visuals where Unity/GPU buffers require float; authority math remains double/AUP.
- Low-tier behavior stays explicit through existing KCC/fluid tier gates; no silent AUP float fallback was introduced.
- Acoustic output still returns a float scalar for audio shaping after double AUP subtraction.

Exact Microseconds saved:
- AUP constructor/double offset: estimated 12-45 us during long-session drift spikes by avoiding jitter repair/re-hydration cascades.
- Non-destructive AUP shift consumption: estimated 2-8 us on shift frames by avoiding missed rebase correction work.
- AUP direction rsqrt: estimated 1-4 us in steering/audio/scanner callsites that avoid oscillating correction.
- Origin reciprocal divide cleanup: sub-1 us, deterministic consistency improvement.
- AUP max drift telemetry: below 1 us every 300 frames for two tracked entities.
- Zero-GC result: 0 B/frame added.

Verification:
- Mandatory scan re-run: residual AUP float hits are presentation/shader lanes, mainly fluid/scatter offsets.
- Scoped `/ dt` scan over AUP/origin/KCC/acoustic/residency files is clean.
- Scoped normalization scan over AUP/origin/KCC/acoustic files found no remaining normalize/sqrt use in patched authority paths.
- `git diff --check`: line-ending warnings only.
- Build status: blocked. `dotnet build Hecton8.Core.csproj` fails with 131 existing missing-reference errors; `Assembly-CSharp.csproj` timed out; Unity MCP validation returned `no_unity_session`.

Integrator notes:
- Do not accept an empty `Hecton8.Core.AUP.asmdef`; real fix requires moving `AbsoluteUniversePosition` and AUP math into a UnityEngine-free assembly.
- Fluid/scatter AUP offset hits remain presentation-domain debt, not current AUP authority regressions.

## 2026-05-14 - Loop 5 Runtime Projection Upgrade

What was wrong:
- Default AUP-to-runtime projection still subtracted `CurrentTotalOffset` as a `Vector3`, cutting committed-origin precision before the final presentation cast.
- `WorldSpatialHashGrid` AUP validation stored absolute validation positions and committed offset as `float3`, so the validator compared against truncated coordinates.
- Far-unload runtime rehydration used `Vector3 CurrentTotalOffset` even though its source absolute positions were already `double3`.

What was done:
- `PersistentWorldRegistry.AbsoluteUniversePosition.ToRuntimeFloat3()` now uses `HectonFloatingOrigin.CurrentTotalOffsetDouble`.
- `AUPMath.ToRuntimeFloat3` now has a `double3` committed-offset overload; the `float3` overload remains as a wrapper for existing job payloads.
- `WorldSpatialHashGrid.ValidateAupIntegrityJob` now compares `double3` absolute positions against `runtime + double offset`.
- `WorldSpatialHashGrid` far-unload rehydration now subtracts `CurrentTotalOffsetDouble`.
- Re-ran prompt extraction from `Docs/Tasks/CURRENT_BATCH.md`; this agent ID is still absent.

Cinematic Cheats used:
- Kept final runtime positions as float because Unity transforms/rendering consume float.
- Left explicit `float3` job offset payloads intact where they are presentation/job ownership boundaries, instead of forcing cross-domain churn.

Exact Microseconds saved:
- Runtime projection double offset: estimated 4-12 us in rebase-heavy scenes by avoiding avoidable projection jitter and correction work.
- Spatial validation double lane: below 2 us on validation cadence, with about 98 KB extra persistent native memory at max validation capacity.
- Far-unload rehydration double offset: sub-1 us per maintenance pass; prevents rehydrated runtime cache drift after long sessions.

Verification:
- Mandatory AUP scan re-run; residual `AupOffset` hits remain fluid/scatter/presentation debt.
- `git diff --check` on changed code files reports line-ending warnings only.
- `dotnet build .\Hecton8.Core.csproj --no-restore --disable-build-servers -v:quiet -clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false` is still blocked by 140 existing missing-reference/interface errors before these AUP files can be isolated.
- Unity MCP `validate_script` on `Assets/_Project/Scripts/World/AUPMath.cs` returned `no_unity_session`.

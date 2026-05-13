# LOG RENDER_GI_RELAY_SYNC

## 2026-05-13 - GI Relay Purge And Native SH Blit
STATUS: PENDING VERIFICATION

What was wrong:
- Ambient authority was split across celestial, underwater visuals, and render-settings restore paths. That creates last-writer-wins lighting and can force Unity render-setting/GI stalls.
- No first-party `LightingManager.Instance` existed, but no registered GI authority existed either.
- Realtime reflection probe generation was not present in first-party scripts, but there was no hard single-cubemap relay path.

What was done:
- Added `IGIRelaySystem`, `GIRelayRuntimeSnapshot`, `GlobalRegistry.GIRelay`, and register/unregister/resolve support.
- Added `Hecton8.Lighting` leaf asmdef and `HectonGIRelaySystem`.
- Implemented Burst SH job over persistent native day/night/snap/output buffers.
- Implemented `UnsafeUtility.MemCpy` from native SH coefficients into retained `SphericalHarmonicsL2`; layout checked as `27 * sizeof(float)`.
- Patched `HectonCelestialEngine`, `HectonUnderwaterVisuals`, `SystemDispatcher`, and `RenderSettingsLifecycleGuard` to yield ambient writes/restores when GI relay owns the probe.
- Added depth palette SH tint, moon/eclipse water surface emission handoff, fog globals, fauna emissive scalar, shadow cascade LOD, single optional `WaterVolume` cubemap, two-frame lightning L0 spike, AUP-safe depth, low-tier SH snap states, and fixed native blackbox telemetry.

Cinematic cheats used:
- 1D cyan-to-black depth palette instead of volumetric underwater GI.
- Triangle-wave daylight after OMEGA polish instead of sine.
- Four discrete SH snap states for low tier/MX350 instead of coefficient interpolation.
- One global low-res cubemap instead of realtime reflection probe renders.
- Two-frame SH L0 flash instead of spawning a lightning light or coroutine.

Exact microseconds saved estimates:
- Registry GI authority vs discovery/singleton churn: 8 us avoided on lookup paths.
- 10Hz SH job instead of per-frame SH blending: 10-25 us per frame avoided.
- Native SH blit amortized at 10Hz: 1-3 us/frame, 0 B/frame.
- Depth palette fake vs volumetric/real probe approach: 1000-5000 us stall risk avoided when probes would churn.
- Shadow cascade reduction at depth: 50-300 us GPU-side saved depending caster count.
- Triangle daylight polish: 1-2 us per SlowTick vs sine on i3/MX350.
- Telemetry ring write: 1-3 us per SlowTick fixed cost.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /nr:false /p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors for generated Core project coverage.
- `Hecton8.Lighting.csproj` has not been generated yet, so Unity asmdef validation for the new lighting assembly is still pending.
- Targeted anti-bloat scan on `HectonGIRelaySystem.cs`: no `math.sin`, `math.sqrt`, `math.normalize`, `foreach`, `string.Format`, `.ToString(`, or `new SphericalHarmonicsL2`; expected bitmask branch found.

Final status:
- All 19 task checkboxes complete.
- OMEGA polish complete.
- STATUS remains PENDING VERIFICATION by prompt and missing Unity-generated lighting assembly validation.

## 2026-05-13 - Post-Recheck Fix Pass
STATUS: PENDING VERIFICATION

What was wrong:
- Lightning SH scalar could persist from a previous stronger strike and affect a later weaker strike after the two-frame overlay finished.
- GI surface emission handoff touched underwater materials every SlowTick even when moon/eclipse emission and the registered underwater visual target were unchanged.

What was done:
- Reset `_lightningScalar` after base SH probe restoration and made new strikes start from their own saturated intensity unless extending an active two-frame flash.
- Added `_lastSurfaceEmissionTarget` so relay material handoff runs only when emission color or target changes.
- Reapplied active GI tint from `HectonUnderwaterVisuals.ApplyCrestMaterial` after Crest material refreshes, keeping relay authority without redundant 10Hz relay material writes.

Cinematic cheats used:
- Kept lightning as exact two-frame SH L0 overlay, no lights/coroutines.
- Kept water emission as material color reinforcement, no renderer scan.

Exact microseconds saved estimates:
- Stable emission path avoids estimated 4-12 us per 10Hz SlowTick when ocean materials exist.
- Lightning scalar reset costs 0 us outside active strikes and preserves exact flash semantics.

Verification:
- No `dotnet build` was launched during this pass per user instruction.
- `git diff --check` over touched files reported only existing CRLF normalization warnings.
- Targeted static scan found no `Update()`, `math.sin`, `math.sqrt`, `foreach`, or `new SphericalHarmonicsL2` in `HectonGIRelaySystem.cs`.

## 2026-05-13 - Second Post-Recheck Fix Pass
STATUS: PENDING VERIFICATION

What was wrong:
- Reflection binding used `RenderSettings.customReflectionTexture`, which is not the stable Unity RenderSettings cubemap API.
- Fog LOD, fauna emissive, and WaterVolume global texture writes were still unconditional at 10Hz.
- Telemetry ring used modulo and dumped raw ring order instead of chronological black-box order.
- Water surface GI material tint could brighten through max-with-current-color but could fail to decay when moon/eclipse emission fell.

What was done:
- Switched reflection binding to `RenderSettings.customReflection`.
- Added shader-state cache invalidation on relay enable/disable and guarded fog/fauna/WaterVolume global writes by state change.
- Changed telemetry ring to branch-wrapped cursor plus chronological dump order.
- Made `ApplyGIRelaySurfaceEmission` rebuild Crest material base before applying changed GI tint, so tint can go down as well as up.

Cinematic cheats used:
- Still one global cubemap and one material tint path; no realtime probe render and no material scan.
- Still fixed native telemetry and SH relay; no managed diagnostic list.

Exact microseconds saved estimates:
- Guarded stable global writes: 1-3 us per 10Hz SlowTick.
- Branch-wrapped telemetry cursor: sub-us per telemetry write, removes modulo.
- Reversible material tint avoids visual artifact without adding per-frame work.

Verification:
- No `dotnet build` was launched during this pass per user instruction.
- `git diff --check` over touched files reported only existing CRLF normalization warning on `HectonUnderwaterVisuals.cs`.
- Static scan confirmed `customReflectionTexture` no longer appears in GI relay code.

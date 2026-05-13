# TRAUMA_GLITCH_SYSTEM
Date: 2026-05-07

Status: PENDING VERIFICATION
Verification: PENDING VERIFICATION

## 2026-05-11 Current-State Override

- Current data boundary: `Docs/Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md`.
- Current manifest: `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`.
- Current visual-realistic-fake doctrine: `Docs/Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md`.
- May 13 DOC_AUDIT override: the cited May 11 compile artifact is absent from the current filesystem; treat the May 11 compile-success line as stale report text until restored or replaced. Runtime, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, import, scene wiring, and visual quality remain `PENDING VERIFICATION`.
- Existing May 4 boundary sections in this file are historical unless they describe local system intent not contradicted by newer reports.
- Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, and visual quality remain `PENDING VERIFICATION`.
## Historical 2026-05-04 Boundary

- Read `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md`, `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`, `Docs/Reports/2026-05-04_WARNING_CLEANUP.md`, `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`, and `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` before using this trauma/glitch map as current runtime truth.
- This document is a gameplay/presentation contract, not proof that all damage channels, mutation unlocks, visor shader paths, or survival grace states are runtime-validated.
- Re-open gameplay health, trauma, visor, and shader owners before surgery.

## Scope

Canonical runtime architecture for player trauma, visor glitch propagation, BIOS recovery mode, survival grace, radiation fatigue, and mutation unlock state.

This document supersedes ad-hoc prompt dumps and audit notes as the live architecture reference.
No older trauma architecture document existed under `Docs/ARCHITECTURE/`, so no in-scope replacement delete was required.
Archived chat dumps and audits remain archival evidence, not runtime authority.

## Runtime Owners

- `Assets/_Project/Scripts/Gameplay/TraumaDispatcher.cs`
  Receives damage-channel events, accumulates transient trauma channels, accumulates sustained radiation exposure, and publishes HUD/audio-facing signal packets.
- `Assets/_Project/Scripts/Gameplay/HectonPlayerHealth.cs`
  Owns lethal-damage interception, grace lockout, runtime max-health cap, permanent mutation bitmask, and mutation-side gameplay hooks.
- `Assets/_Project/Scripts/Gameplay/HazardMutationProfile.cs`
  Authoring owner for mutation thresholds and bit IDs.
- `Assets/_Project/Scripts/Visor/VisorHUDController.cs`
  Resolves trauma state into shader uniforms for visor-space glitch and BIOS recovery gating.
- `Assets/_Project/Art/Shaders/SuitVisor.shader`
  Executes the final visual failure mode: VHS tearing, RGB split, and 1-bit phosphor BIOS path.
- `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs`
  Consumes `PlayerSignalEvents` and mirrors BIOS mode on the diegetic canvas HUD.

## Channel Model

Three channels exist and must remain separate:

- `Integrity`
  Physical survivability and hull/body reliability.
- `Power`
  Transport/system power state that can collapse HUD fidelity.
- `Clarity`
  Sensor/UI truthfulness and visor coherence.

`TraumaDispatcher` decays transient channel amplitudes over time, but radiation-fatigue exposure does not decay. That exposure is handed off to `HectonPlayerHealth` as permanent degradation state.

## BIOS Recovery Mode

### Entry

BIOS recovery mode is entered when either condition is true:

- `ClarityRemaining01 < 0.1`
- effective integrity collapse drives the visor into emergency state

`ClarityRemaining01` is resolved as:

```csharp
float clarityRemaining01 = 1f - Mathf.Clamp01(_clarityChannel01);
```

### Visor-Space Glitch Stack

Normal trauma path in `SuitVisor.shader` blends:

- hazard-driven glitch intensity
- horizontal UV tearing
- RGB channel splitting
- scanline/noise modulation

Core HUD split logic:

```hlsl
float2 hudDecaySplit = float2(
    hazardRadiation * 0.015 + hazardGlitch * 0.008,
    hazardThermal * 0.0025);

float4 hudSampleR = SAMPLE_TEXTURE2D(_HUD_RenderTexture, sampler_HUD_RenderTexture, hudDistortedUV + hudHypoxiaOffset + hudDecaySplit);
float4 hudSampleB = SAMPLE_TEXTURE2D(_HUD_RenderTexture, sampler_HUD_RenderTexture, hudDistortedUV - hudHypoxiaOffset - hudDecaySplit);
hudSample.rgb = float3(hudSampleR.r, hudSample.g, hudSampleB.b);
```

This is the live VHS-tear branch, not a conceptual sketch.

### BIOS 1-Bit Phosphor Path

When BIOS recovery mode is active, RGB HUD color is discarded and the visor uses luminance-only thresholding with a green-only output.

Core branch:

```hlsl
float biosRecoverySwitch = step(0.5, biosRecoveryMode);
float2 phosphorTrailOffset = float2(-(0.0015 + hazardRadiation * 0.004 + hazardGlitch * 0.002), 0.0);
float rawHudLuminance = dot(hudBaseSample.rgb, float3(0.2126, 0.7152, 0.0722));
float trailLuminanceA = dot(SAMPLE_TEXTURE2D(_HUD_RenderTexture, sampler_HUD_RenderTexture, hudDistortedUV + phosphorTrailOffset).rgb, float3(0.2126, 0.7152, 0.0722));
float trailLuminanceB = dot(SAMPLE_TEXTURE2D(_HUD_RenderTexture, sampler_HUD_RenderTexture, hudDistortedUV + phosphorTrailOffset * 2.5).rgb, float3(0.2126, 0.7152, 0.0722));
float trailLuminance = max(rawHudLuminance, max(trailLuminanceA * 0.72, trailLuminanceB * 0.46));
float biosPrimaryBit = step(biosThreshold, trailLuminance);
float phosphorLevel = saturate(biosPrimaryBit + phosphorScanGlow);
float3 biosColor = float3(0.0, phosphorLevel * phosphorPulse, 0.0) * _HUD_Intensity;
```

Rules:

- no normal RGB HUD color survives the BIOS branch
- thresholding is pure ALU
- the trail is simulated by offset re-sampling, not a history buffer
- output is green-only phosphor on black

## Survival Grace Period

`HectonPlayerHealth` owns the anti-one-shot gate.

Activation rules:

- incoming damage must be lethal
- player must be above `10%` normalized health
- grace lockout must be inactive
- ignored when the caller explicitly bypasses invulnerability

Runtime logic:

```csharp
if (_survivalGraceLockoutTimer > 0f ||
    incomingDamage < currentHealth ||
    HealthPercent <= 0.10f)
{
    return false;
}

clampedDamage = Mathf.Max(0f, currentHealth - 0.01f);
_invulnerabilityTimer = Mathf.Max(_invulnerabilityTimer, 0.5f);
_survivalGraceLockoutTimer = 8f;
PlaySurvivalGraceHeartbeatPulse();
NotificationEvents.PushCritical("CARDIAC OVERRIDE // SURVIVAL GRACE");
```

Effects:

- health is clamped to `0.01`
- invulnerability runs for `0.5s`
- lockout runs for `8.0s`
- heartbeat audio is triggered through `GlobalRegistry.Audio`

## Radiation Fatigue

`TraumaDispatcher` accumulates only when radiation signal exceeds the floor threshold.

Accumulator:

```csharp
_radiationExposureSeconds += Mathf.Max(0f, deltaTime) * radiationSignal;
_playerHealth.ApplyRadiationExposure(_radiationExposureSeconds);
```

Permanent health-cap formula in `HectonPlayerHealth`:

```csharp
float fatigueScale = Mathf.Max(0.65f, 1f - (_radiationExposureSeconds * 0.005f));
maxHealth = BaseMaxHealth * fatigueScale;
```

Behavior:

- exposure is monotonic
- no passive recovery exists
- only `ClearRadiationFatigue()` resets the cap
- the intended recovery owner is a future RadAway consumable, not passive time

## Mutation Tracker

Mutation state is a persistent runtime bitmask:

- `Bit0` `BioluminescentSkinBit`
- `Bit1` `GillsBit`
- `Bit2` `RadiationLatticeBit`
- `Bit3` `NeuralEchoBit`

Threshold data is authored in `HazardMutationProfile`.

Unlock flow:

```csharp
if ((_mutationFlags & threshold.MutationBit) == 0u &&
    _radiationExposureSeconds >= threshold.ExposureThresholdSeconds)
{
    _mutationFlags |= threshold.MutationBit;
    ApplyMutationRuntimeEffects();
    OnMutationFlagsChanged?.Invoke(_mutationFlags);
    NotificationEvents.PushWarning("MUTATION DETECTED // " + ResolveMutationDisplayName(threshold));
}
```

Current gameplay hooks:

- `Bioluminescent Skin`
  `PredatorVisibilityScale = 2f`
  `FlashlightBypassActive = true`
- `Gills`
  `HectonSurvivalSystem.SetRuntimeOxygenCapacityMultiplier(1.25f)`

UI wiring:

- immediate user-facing mutation discovery goes through `NotificationEvents`
- stable runtime consumers can subscribe to `OnMutationFlagsChanged`
- HUD trauma rendering remains event-driven through `PlayerSignalEvents`

## Event Flow

1. Hazard or structural owner emits damage-channel event.
2. `TraumaDispatcher` receives `OnIntegrityChanged`, `OnPowerChanged`, `OnClarityChanged`, or threshold events.
3. Dispatcher updates transient trauma channels and hazard-specific signal channels.
4. Dispatcher publishes `TraumaHudSignal` and `InteractionSignal`.
5. `VisorHUDController` and `SuitHUDV4CanvasOverlay` consume those packets and render either glitch or BIOS fallback.
6. Sustained radiation additionally advances permanent exposure in `HectonPlayerHealth`.
7. `HectonPlayerHealth` updates the max-health cap and mutation flags.

## GC and Hot-Path Notes

- No new managed collections are created in tick paths.
- No coroutine-based trauma logic is used.
- No `GC.Collect()` call exists in live runtime hot paths after the sweep.
- The only `GC.Collect()` hit in the repository scope was a commented line in a third-party editor script, which is not a runtime hot-path owner.

## Verification Boundary

Code-level validation can prove:

- compile cleanliness for touched scripts
- console cleanliness at validation time
- formula ownership and event ownership

It cannot prove:

- final gameplay feel
- mutation balance
- BIOS readability under live combat conditions

Those remain runtime verification tasks.

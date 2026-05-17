# VR Comfort HLSL Integration

Owner: `VR_JERK_THRESHOLD_AUDIT`
Status: `COMFORT TUNED / PENDING RUNTIME VERIFICATION`
Evidence Class: `STATIC_DOC`

## Contract

Use `Data/UX/VR_Comfort_Profiles.json` only during bootstrap, build-time bake, or editor tooling. Do not parse JSON in `Tick`, `FixedTick`, `LateUpdate`, Burst jobs, or shader update loops.

Runtime owner must max-combine comfort contributors:

```text
comfort01 = max(speedOpacity, angularVelocityOpacity, angularAccelerationOpacity, angularJerkOpacity, frameSafetyOpacity)
```

Do not sum contributors. Summing creates sudden darkness during combined motion and is a comfort regression.

## C# Mapping

Copy baked values into fixed fields or preallocated arrays before gameplay:

```csharp
// Pseudocode only. Do not allocate or parse JSON in VISUAL_SYNC.
_vrComfortVignette01 = math.max(_speedOpacity, _somaticAngularOpacity);
Shader.SetGlobalFloat(_VRComfortVignette01Id, _vrComfortVignette01);
Shader.SetGlobalFloat(_VRComfortInnerRadiusId, math.lerp(0.86f, 0.38f, _vrComfortVignette01));
Shader.SetGlobalFloat(_VRComfortEdgeSoftnessId, math.lerp(0.18f, 0.34f, _vrComfortVignette01));
```

## HLSL Edge Mask

Use the existing URP/XR vignette pass or a stencil-gated visor pass. Keep the mask cheap.

```hlsl
float2 centeredUv = input.texcoord.xy * 2.0 - 1.0;
float radial = length(centeredUv);
float edgeMask = smoothstep(_VRComfortInnerRadius, _VRComfortInnerRadius + _VRComfortEdgeSoftness, radial);
float comfortMask = saturate(edgeMask * _VRComfortVignette01);
color.rgb = lerp(color.rgb, 0.0.xxx, comfortMask);
```

Quest 2 uses earlier opacity rise and slower release. PC VR 120 Hz can retain wider FOV longer, but `50 rad/s3` angular jerk still trips the nausea guard.

## Teleport Fake

When a profile's fade-black velocity, acceleration, or jerk threshold is crossed, fade out, execute the snap/teleport, hold black briefly, then fade in. Showing the high-speed camera motion is rejected.

## Binary Ingest

The SHINOBU-facing blob is `Data/UX/VR_Comfort_Profiles.h8bin`. It uses explicit little-endian Python struct formats, 16-byte aligned sections, a CRC32 payload guard, and FNV-1a IDs. Layout details live in `Data/UX/VR_Comfort_Binary_Layout.md`.

The blob is cold data. Load or bake it before gameplay; do not allocate private mutable comfort profiles during `VISUAL_SYNC`.

## Verification Boundary

This document is not Unity runtime proof. Required future proof: Unity Console, Play Mode, XR headset route, GCMonitor, and profiler capture for the consuming runtime owner.

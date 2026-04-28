# CIRCULAR_DEPS.md — Assembly Dependency Audit
**Status:** ✅ NO CIRCULAR DEPENDENCIES DETECTED  
**Scan Date:** 2026-04-28  
**Scope:** All `.asmdef` under `Assets/_Project/`

---

## Assembly Dependency Map

```
Hecton8.Bootstrap.Contracts (leaf)
Hecton8.World.Contracts (leaf)
  └─ Unity.Collections, Unity.Mathematics

Hecton8.Input.Generated (leaf)
  └─ Unity.InputSystem

Hecton8.Input
  ├─ Hecton8.Input.Generated
  ├─ Unity.InputSystem
  └─ Unity.TextMeshPro

Hecton8.Core
  ├─ Hecton8.Bootstrap.Contracts
  ├─ Hecton8.World.Contracts
  ├─ Hecton8.Input
  ├─ Hecton8.Input.Generated
  ├─ Unity.InputSystem
  ├─ Unity.Mathematics
  ├─ Unity.Burst
  ├─ Unity.Collections
  ├─ Unity.Profiling.Core
  ├─ Unity.TextMeshPro
  ├─ UnityEngine.UI
  ├─ Unity.RenderPipelines.Core.Runtime
  ├─ Unity.RenderPipelines.Universal.Runtime
  ├─ GPUInstancer
  ├─ Den.Tools
  ├─ MapMagic
  ├─ Crest
  ├─ WaveHarmonic.Crest
  ├─ WaveHarmonic.Crest.Shared
  ├─ ShapesRuntime
  ├─ EasySave3
  └─ VolumetricLightBeam

Hecton8.World.Dots
  ├─ Hecton8.World.Contracts
  ├─ Unity.Entities
  ├─ Unity.Collections
  ├─ Unity.Mathematics
  └─ Unity.Burst

Hecton8.Editor
  ├─ Hecton8.Core
  ├─ UnityEngine.TestRunner
  ├─ UnityEditor.TestRunner
  ├─ Unity.InputSystem
  └─ Unity.TextMeshPro

Hecton8.Optimization.Editor
  └─ Hecton8.Core

Hecton8.UI.Editor
  └─ Hecton8.Core

Hecton8.EditModeTests
  ├─ Hecton8.Core
  ├─ Hecton8.Editor
  ├─ UnityEngine.TestRunner
  └─ UnityEditor.TestRunner

Hecton8.PlayModeTests (leaf)
  └─ UnityEngine.TestRunner, UnityEditor.TestRunner
```

## Critical Finding: ACL Violation in Hecton8.Core

`Hecton8.Core.asmdef` contains **direct references to 8 third-party assemblies**:

| Assembly | Type | ACL Status |
|----------|------|------------|
| `GPUInstancer` | Third-party rendering | ❌ DIRECT — must be behind RENDER bridge |
| `Den.Tools` | MapMagic dependency | ❌ DIRECT — must be behind WORLD bridge |
| `MapMagic` | World generation | ❌ DIRECT — must be behind `Hecton8.World` bridge |
| `Crest` | Ocean system (legacy) | ❌ DIRECT — must be behind `IHectonOceanKinematics` |
| `WaveHarmonic.Crest` | Ocean system (package) | ❌ DIRECT — must be behind `IHectonOceanKinematics` |
| `WaveHarmonic.Crest.Shared` | Ocean system (shared) | ❌ DIRECT — must be behind `IHectonOceanKinematics` |
| `ShapesRuntime` | Third-party primitives | ❌ DIRECT — must be behind UI/VFX bridge |
| `EasySave3` | Save system (FORBIDDEN) | ❌ DIRECT — AGENTS.md forbids Easy Save 3 |
| `VolumetricLightBeam` | VFX third-party | ❌ DIRECT — must be behind VFX bridge |

**Evidence:** `Assets/_Project/Scripts/Hecton8.Core.asmdef` lines 18-26.

**Required Fix:** Extract all third-party references into dedicated bridge assemblies:
- `Hecton8.CrestBridge.asmdef` (references Crest, implements `IHectonOceanKinematics`)
- `Hecton8.MapMagicBridge.asmdef` (references MapMagic/Den.Tools)
- `Hecton8.SaveBridge.asmdef` — **REMOVE EasySave3 entirely** per AGENTS.md mandate
- `Hecton8.VFXBridge.asmdef` (references GPUInstancer, ShapesRuntime, VolumetricLightBeam)

## Verdict
- **Cycles:** None.
- **ACL Compliance:** ❌ FAILED — Hecton8.Core is contaminated with 9 third-party direct references.
- **Compile Order:** Safe.
- **Action:** Bridge extraction mandatory before production milestone.

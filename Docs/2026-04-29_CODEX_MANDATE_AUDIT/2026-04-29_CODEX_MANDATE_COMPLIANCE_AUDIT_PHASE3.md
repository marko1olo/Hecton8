# 2026-04-29 - CODEX Mandate Compliance Audit Phase 3

Status: PENDING VERIFICATION
Author: Codex
Scope: static audit only

## Mandates Followed

- `AGENTS.md`
- `.agents-skills/AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt`
- `.agents-skills/UI_Localization_Babel_RTL_FontSwap_ZeroAlloc.txt`
- `.agents-skills/PROJECT_LTS_Compatibility_Layer.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`

## Method

- Cross-cutting scan for forbidden APIs and compatibility patterns across shipping scripts.
- This phase intentionally checks both negative and positive signals.
- No runtime execution or profiler validation was performed.

## What Is Actually Aligned

### 1. No `AudioSource.PlayOneShot` footprint was found in shipping scripts

Evidence:

- Shipping-script `PlayOneShot(...)` count: `0`

Assessment:

- This does not prove the full DSPGraph contract is implemented correctly.
- It does show the most obvious forbidden hot-path audio fallback is not present in first-party shipping scripts.

### 2. Localization registry architecture exists and appears materially aligned

Evidence:

- `Assets/_Project/Scripts/UI/TMP_TextRegistry.cs` exists and is actively used.
- `Assets/_Project/Scripts/LocRegistry.cs` exists and includes RTL processing hooks.
- `Assets/_Project/Scripts/RTLProcessor.cs` exists.
- `SuitHUDV4CanvasOverlay.cs`, `PDAShellChrome.cs`, `LabelSwapScheduler.cs`, `LocalizedTMPAutoSizer.cs`, and `FontStreamingManager.cs` all show registry/RTL integration.

Additional positive signal:

- No shipping hits were found for:
  - `FindObjectsOfType<TMP_Text>`
  - `FindObjectsOfTypeAll<TMP_Text>`
  - `Resources.FindObjectsOfTypeAll<TMP_Text>`
- No shipping hits were found for direct string-key lookup pattern:
  - `LocRegistry.Resolve("...")`

Assessment:

- Localization and RTL infrastructure appears substantially more disciplined than several other subsystems.
- This area still needs runtime validation, but the architecture is directionally correct.

### 3. No `SendMessage` / `BroadcastMessage` runtime usage was confirmed in active shipping paths

Evidence:

- Shipping-script scan for `SendMessage(...)`, `BroadcastMessage(...)`, and `SendMessageUpwards(...)` produced no direct active examples.

Assessment:

- This is one of the few clean cross-cutting bans that appears to be holding.

## Confirmed Findings

### 1. Compatibility and modding boundaries still use reflection-based runtime coupling

Mandate conflict:

- `PROJECT_LTS_Compatibility_Layer.txt`: reflection-free modding boundary
- `AGENTS.md`: reflection is forbidden in gameplay architecture

Evidence:

- Cross-cutting scan returned `11` shipping matches across `async`/`Task`/reflection/type activation patterns.

Direct source evidence:

- `Assets/_Project/Scripts/ModdingAPI/ModLoader.cs`
  - `using System.Reflection;`
  - `Activator.CreateInstance(entryType)`
- `Assets/_Project/Scripts/World/HectonCrestOceanDepthCacheRuntimeBridge.cs`
  - `using System.Reflection;`
  - `GetMethod(...)`
  - `Invoke(...)`
- `Assets/_Project/Scripts/World/ImpostorSystem.cs`
  - `Type.GetType($"{typeName}, AmplifyImpostors.Runtime")`
  - `Type.GetType($"{typeName}, Assembly-CSharp")`

Assessment:

- This is not a narrow editor-only exception.
- Reflection is being used in runtime compatibility paths and in mod-loading.

What is objectively missing:

- A reflection-free compatibility and modding boundary consistent with the project mandate.

Impact:

- IL2CPP and vendor API drift risk remain.
- Modding boundary is heavier and looser than the declared contract.

### 2. Async policy is still inconsistent in shipping runtime code

Mandate conflict:

- `AGENTS.md`: `async void` forbidden
- `AGENTS.md`: `async Task` forbidden, use `Awaitable`

Direct source evidence:

- `Assets/_Project/Scripts/SceneBootstrap.cs`
  - `private async void Start()`
- `Assets/_Project/Scripts/UI/PauseMenuController.cs`
  - `private async System.Threading.Tasks.Task SaveSlotAsync(string slotName)`
- `Assets/_Project/Scripts/SaveThumbnailSystem.cs`
  - `File.WriteAllBytesAsync(...).ContinueWith(...)`

Assessment:

- Some files explicitly claim compliance while using the forbidden model anyway.
- This is contract drift, not just technical debt wording.

What is objectively missing:

- A finished migration of bootstrap/save-side async orchestration onto `Awaitable`.

### 3. Runtime search fallback is still present in compatibility and integration code

Mandate conflict:

- Runtime search APIs are meant to be minimized and ownership-driven.

Evidence:

- Shipping-script `FindObjectsOfTypeAll` / `Resources.FindObjectsOfTypeAll` count: `3`

Direct source evidence:

- `Assets/_Project/Scripts/MapMagicBridge.cs`
  - `Resources.FindObjectsOfTypeAll<MapMagicObject>()`
- `Assets/_Project/Scripts/World/HectonCrestOceanDepthCacheBootstrap.cs`
  - `Resources.FindObjectsOfTypeAll<Terrain>()`
- `Assets/_Project/Scripts/Tools/VerificationRuntimeProbe.cs`
  - `Resources.FindObjectsOfTypeAll<T>()`

Assessment:

- Some of these are recovery or verification tools.
- The omission is that these recovery/search paths still live inside the shipping tree and runtime integration surface.

### 4. Deprecated and inactive runtime files remain inside the shipping script tree

Evidence:

- `Camera.main` scan only returned the following active code hits:
  - `Assets/_Project/Scripts/PlayerController - Old - deprecated - do not use or open.cs`

Assessment:

- The immediate `Camera.main` problem is not active modern gameplay code.
- The broader issue is source-tree contamination:
  - deprecated runtime files remain in the active first-party scripts tree
  - they pollute audits and can re-enter references accidentally

What is objectively missing:

- A stricter separation between active shipping code and deprecated retained files.

### 5. Localization is one of the stronger areas, but it still sits inside a generally mixed runtime tree

Positive evidence:

- `TMP_TextRegistry`
- `RTLProcessor`
- `LocRegistry`
- staged registration and `isRightToLeftText` handling

Residual concern:

- Several localization-support files still coexist beside broader UI files that use direct `.text = ...` mutation from Phase 1.

Assessment:

- Localization infrastructure itself looks stronger than generic UI mutation policy.
- The missing work is not registry design.
- The missing work is completing zero-GC text discipline across all UI layers that consume localization.

## System-Level Assessment

Audio:

- The most obvious forbidden `PlayOneShot` fallback is absent.
- Full DSP compliance still requires runtime verification and deeper implementation audit.

Localization:

- Architecture is comparatively mature.
- Registry and RTL discipline appear real, not cosmetic.

Compatibility / Modding:

- This is a weak spot.
- Reflection-based runtime coupling still exists where the compatibility mandate wanted a harder boundary.

Source hygiene:

- Deprecated and verification-oriented files still live in the shipping script tree.
- That does not automatically break runtime, but it weakens architectural hygiene.

## What The Project Objectively Missed In This Phase

- Reflection-free runtime compatibility and modding boundaries.
- Full `Awaitable` compliance outside the main save core.
- Clearer separation of shipping code from deprecated and verification-only runtime files.
- End-to-end alignment between strong localization infrastructure and weaker generic UI text mutation behavior.

## Regression Model

CPU:

- Risk source: reflection and type-resolution paths in runtime compatibility code.

GC:

- Risk source: `Task`/`ContinueWith(...)` usage and runtime search fallback.

Memory:

- Risk source: compatibility helpers and deprecated runtime files increase retained code surface and maintenance burden more than direct memory cost.

Cadence:

- Risk source: inconsistent async models and compatibility fallbacks.

Correctness:

- Risk source: reflection-driven runtime coupling under IL2CPP and vendor/package evolution.

## Verification Status

Static verification only.

Not performed:

- DSPGraph runtime inspection
- localization live language swap test
- IL2CPP build verification
- mod loading runtime verification
- profiler-backed hot-path validation

Final status: PENDING VERIFICATION

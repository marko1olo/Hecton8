# System Scorecard

Date: 2026-04-29  
Status: PENDING VERIFICATION

Mandates followed:
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `UI_Data_Streaming_ZeroGC_Optimization.txt`

## Good

- Core dispatch architecture is real and technically competent.
  Evidence: `Assets/_Project/Scripts/Core/SystemDispatcher.cs` uses fixed registry lanes, native raycast queues, job scheduling, and central update ownership instead of spraying `Update()` everywhere.

- Interaction packet contracts are clean.
  Evidence: `Assets/_Project/Scripts/Interaction/EquipmentInteractionContracts.cs` uses blittable packet/signal structs and clear tool capability contracts instead of string-driven dispatch.

- Runtime interaction routing is directionally correct.
  Evidence: `Assets/_Project/Scripts/Interaction/EquipmentInteractionHandler.cs` uses a persistent `NativeQueue<InteractionSignal>`, scheduled `RaycastCommand` buffers, and explicit service registration.

- Legacy async asset loading was intentionally neutered instead of silently violating policy.
  Evidence: `Assets/_Project/Scripts/AsyncLoadHelper.cs` fails fast and documents that runtime Resources loading is disabled.

- Loading screen architecture is better than average.
  Evidence: `Assets/_Project/Scripts/UI/LoadingScreenController.cs` uses `CanvasGroup`, tick registration, cached percent strings, and avoids native coroutine spam in the main controller path.

## Acceptable

- Bootstrap has real structure, but there are too many startup owners.
  Evidence: `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` handles service/bootstrap layers, while `Assets/_Project/Scripts/SceneBootstrap.cs` separately owns async scene startup, pool warmup, save restore, and world-readiness gates.
  Verdict: not chaos, but not a single sovereign boot authority either.

- Save system design is ambitious and mostly aligned with project rules, but confidence is blocked by compile failure.
  Evidence: `Assets/_Project/Scripts/SaveManager.cs` uses persistent native buffers, hash/integrity jobs, explicit registry saveables, and backup logic.
  Limiter: the current editor session reports first-party compile errors inside save code paths.

- UI architecture is mixed.
  Evidence for positives: `Assets/_Project/Scripts/Interaction/InteractionUI.cs` already prefers `CanvasGroup` after one-time initialization.
  Evidence for drift: PDA / pause / tooltip flows still use many `SetActive()` toggles across runtime UI.

- Addressables discipline exists in isolated pockets, not as a system.
  Evidence: `Assets/_Project/Scripts/ItemCatalog.cs` has an actual `LoadAssetAsync<GameObject>()` / `Addressables.Release(...)` pair.
  Limiter: broad mandated asset lifecycle ownership is not visible across the rest of first-party runtime code.

- World readability fail-safes are practical, but they also expose missing authored ownership.
  Evidence: `Assets/_Project/Scripts/World/WorldReadabilityRuntimeBootstrap.cs` spawns missing runtime directors if scenes omit them.
  Verdict: useful for survival, bad as a permanent architectural habit.

## Bad

- The project is not presently in a clean compilable first-party state.
  Evidence: live Unity console errors in quest, save, fluid, UI, and world systems. See `FINDINGS_AND_EVIDENCE.md`.

- `IUIService` ownership is fragmented.
  Evidence: `HectonFabricatorUI`, `HectonSuitHUD_v4`, and `UI/SuitHUDV4CanvasOverlay` all implement `IUIService`, while `Bootstrap/GameBootstrapper.cs` explicitly says no unified UI-layer adapter exists yet.

- `IAudioService` is still a ghost contract.
  Evidence: `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs` defines it, `Assets/_Project/Scripts/Core/GlobalRegistry.cs` exposes it, but no first-party implementor was found in `Assets/_Project/Scripts`.

- Runtime memory lifecycle still violates an explicit project ban.
  Evidence: `Assets/_Project/Scripts/UI/PauseMenuController.cs:1004` calls `Resources.UnloadUnusedAssets()`.

- The VFX camera stack contradicts the stated package policy.
  Evidence: `Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs` imports `DG.Tweening` and uses `DOTween.To(...)`.

- God-object concentration is severe.
  Evidence:
  - `World/HectonMapMagicVegetationBridge.cs`: `12793` lines
  - `WorldProceduralScatterDirector.cs`: `10316` lines
  - `HectonPlayerMovement.cs`: `7839` lines
  - `HectonUnderwaterVisuals.cs`: `4826` lines
  - `UI/SuitHUDV4CanvasOverlay.cs`: `4257` lines
  These files are too large to be low-risk owners.

- First-party runtime ownership is still badly scattered at the folder level.
  Evidence: `306` scripts live directly under `Assets/_Project/Scripts` root instead of subsystem folders.

## Summary Judgement

- Good foundation exists in `Core`, parts of `Interaction`, and some bootstrap/runtime service patterns.
- The middle layer is fragmented, oversized, and inconsistent.
- The bad news is not stylistic. It is operational: compile breakage and explicit rule violations are still live.

## Regression Model

CPU: no runtime mutation in this report  
GC: no runtime mutation in this report  
Memory: no runtime mutation in this report  
Cadence: classification only  
Correctness: higher than prior flattering narratives because compile state and mandate drift are stated directly

## Hot Path Impact

None. Documentation-only pass.

## Failure Modes

- A subsystem marked "acceptable" may still hide deeper runtime faults in unreached paths.
- A subsystem marked "bad" can still contain good local engineering inside a broken owner.
- This scorecard is system-level, not a guarantee that every file under the subsystem behaves the same.

## Why Kept

Kept because it separates foundation quality from project health.
The project is neither "all trash" nor "basically ready". It is a technically capable codebase carrying live structural damage.


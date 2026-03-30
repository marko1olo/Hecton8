# Unity Reload Audit

## Goal

Speed up script reload and Play Mode entry without breaking:
- sandbox gameplay
- world stack
- live visual iteration for sky / atmosphere / water / celestial systems

## Protected In Wave 1

Do not change these in the first cleanup pass:
- `Assets/_Project/Scripts/HectonCelestialEngine.cs`
- `Assets/_Project/Scripts/HectonAtmosphereManager.cs`
- `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs`
- `Assets/_Project/Scripts/SkySystemFollowCamera.cs`
- visor / HUD preview layers that are still useful for visual iteration

Reason:
- these are likely part of the art-direction workflow in edit mode
- speeding up reload is not worth breaking live look-dev

## Safe To Defer / Reduce

### `Assets/_Project/Scripts/HectonWorldGenerator.cs`

Current status:
- likely legacy / side-path after MapMagic integration
- not referenced by the main world scene as an active runtime authority
- had `ExecuteAlways`
- had edit-mode lifecycle churn through `OnEnable / OnDisable / OnDestroy`

Applied:
- removed `ExecuteAlways`
- made lifecycle do work only in play mode
- in edit mode `OnDestroy` now only clears preview/LUT state instead of doing full runtime cleanup

Why safe:
- manual preview buttons remain
- custom inspector remains
- no protected visual workflow depends on it being always alive

### `Assets/_Project/Scripts/HectonVoxelEngine.cs`

Current status:
- still a real subsystem, not trash
- likely under-tested rather than deprecated
- had `ExecuteAlways`
- had edit-mode lifecycle churn through `OnEnable / OnDisable / OnDestroy`

Applied:
- removed `ExecuteAlways`
- made lifecycle do work only in play mode

Why safe:
- editor inspector remains
- runtime generation API remains
- we removed editor churn, not the subsystem itself

Additional optimization:
- removed the eager global `InitializeOnLoadMethod` registration for `MCTables`
- editor shutdown hooks are now registered lazily only when tables are actually initialized

Why this matters:
- reduces one more always-on editor reload hook
- targets the exact area highlighted by `Editor.log`:
  - `BeforeProcessingInitializeOnLoad`
  - `ProcessInitializeOnLoadAttributes`

### `Assets/_Project/Scripts/HectonSocketHelper.cs`

Current status:
- editor gizmo helper
- used `ExecuteInEditMode`
- actual features are:
  - gizmo drawing
  - context menu snap

Applied:
- removed `ExecuteInEditMode`

Why safe:
- gizmos still work through `OnDrawGizmos`
- context menu still works
- no always-on edit lifecycle is needed

### `Assets/_Project/Scripts/ToolStagingSpawner.cs`

Current status:
- editor authoring helper
- rebuilds tool staging after reset/validate via `EditorApplication.delayCall`
- could queue duplicate rebuilds during inspector churn

Applied:
- added `_rebuildQueued`
- deduplicated pending `delayCall` rebuild requests
- delayed rebuild while Unity is:
  - compiling
  - updating assets
  - entering/leaving play mode

Why safe:
- staging rebuild still works
- editor spam / repeated queued rebuilds are reduced
- rebuild no longer tries to fire inside the most expensive reload window

### `Assets/_Project/Editor/HectonMeshCleaner.cs`

Current status:
- editor-only window
- only matters if the window is open
- previously did a full reset plus global temporary-object cleanup inside `OnDisable`

Applied:
- `OnDisable` now skips heavy cleanup while Unity is:
  - compiling
  - updating
  - entering/leaving play mode
- `OnPlayMode` now only resets on exit transitions instead of every state change

Why safe:
- normal tool behavior remains
- preview and cleanup still happen in stable editor state
- avoids an unnecessary whole-scene temp-object sweep during the worst reload window

### `Assets/_Project/Editor/HectonPhysicsSkinGenerator.cs`

Current status:
- editor-only window
- only matters if the window is open
- previously nulled preview state on every `OnDisable`, including reload/playmode transitions

Applied:
- `OnDisable` now bails out during:
  - compile
  - asset update
  - playmode transition

Why safe:
- normal editor behavior stays intact
- the tool still unsubscribes from `SceneView.duringSceneGui`
- avoids another little bit of unnecessary editor churn during the most expensive reload window

### `Assets/AmplifyImpostors/Plugins/Editor/AIStartScreen.cs`

Applied:
- moved the startup open-check off the hot reload path from immediate `EditorApplication.update`
  to deferred `EditorApplication.delayCall`
- startup work now bails out during:
  - batch mode
  - compile
  - asset update
  - play mode transition

Why safe:
- manual start screen access still works
- package UI/functionality is untouched
- only the editor startup autoload noise was reduced

### Mass `OnValidate` guard pass

Applied safe reload guards to these non-visual gameplay/world scripts:
- `Assets/_Project/Scripts/ScavengePopulator.cs`
- `Assets/_Project/Scripts/BaseModule.cs`
- `Assets/_Project/Scripts/FaunaDirector.cs`
- `Assets/_Project/Scripts/HectonDirectorAI.cs`
- `Assets/_Project/Scripts/HectonBaseAI.cs`
- `Assets/_Project/Scripts/SpatialAudioManager.cs`
- `Assets/_Project/Scripts/ProximityColliderSystem.cs`
- `Assets/_Project/Scripts/PlayerBuilder.cs`
- `Assets/_Project/Scripts/PowerNode.cs`
- `Assets/_Project/Scripts/ResourceNode.cs`
- `Assets/_Project/Scripts/HectonFabricatorUI.cs`
- `Assets/_Project/Scripts/PlayerPDA.cs`
- `Assets/_Project/Scripts/ItemData.cs`
- `Assets/_Project/Scripts/RecipeData.cs`
- `Assets/_Project/Scripts/BuildableData.cs`
- `Assets/_Project/Scripts/ModuleCatalog.cs`
- `Assets/_Project/Scripts/WorldZoneAnchor.cs`
- `Assets/_Project/Scripts/WorldSliceAnchor.cs`
- `Assets/_Project/Scripts/WorldContentSocket.cs`
- `Assets/_Project/Scripts/BuilderTool.cs`
- `Assets/_Project/Scripts/Fabricator.cs`
- `Assets/_Project/Scripts/ItemCatalog.cs`
- `Assets/_Project/Scripts/ModuleMarker.cs`
- `Assets/_Project/Scripts/PlayerFlashlight.cs`
- `Assets/_Project/Scripts/PlayerFootstepAudio.cs`
- `Assets/_Project/Scripts/ScanRuntimeSmokeTester.cs`
- `Assets/_Project/Scripts/ToolTrialRangeRuntimeSmokeTester.cs`
- `Assets/_Project/Scripts/UIRuntimeSmokeTester.cs`
- `Assets/_Project/Scripts/ScannableTarget.cs`
- `Assets/_Project/Scripts/SurvivalStats.cs`
- `Assets/_Project/Scripts/HUDNotification.cs`
- `Assets/_Project/Scripts/HectonItem.cs`
- `Assets/_Project/Scripts/HectonPlayerMovement.cs`
- `Assets/_Project/Scripts/HectonPlayerSpawner.cs`
- `Assets/_Project/Scripts/InteractionHighlighter.cs`
- `Assets/_Project/Scripts/WorldInterestAnchor.cs`
- `Assets/_Project/Scripts/WorldFidelityRoot.cs`

What changed:
- each `OnValidate()` now exits early while Unity is:
  - compiling
  - updating assets
  - entering/leaving play mode

Why safe:
- runtime behavior is unchanged
- authored values are still clamped in normal editor use
- we stop spending validation work in the worst reload window

## Risky / Needs Confirmation Later

### `Assets/_Project/Scripts/Editor/SceneViewSkyboxEnforcer.cs`

Status:
- very likely expensive
- runs on every editor update
- manipulates Scene View skybox/cloud/fog/image-effects state

Why not touched yet:
- this directly affects the look of Scene View
- too close to protected visual workflow

### `Assets/_Project/Editor/VisorOpaqueTextureEnsurer.cs`

Status:
- very cheap
- one-time warning on editor load

Why not touched yet:
- no meaningful reload win
- not worth churn for now

## Findings So Far

- biggest safe wins are in our non-visual world-generation side scripts
- the protected visual systems still need to stay alive for look-dev
- current `Editor.log` evidence says the main pain is not compile itself
- main pain is post-compile reload/finalization:
  - `FinalizeReload`
  - `SetupLoadedEditorAssemblies`
  - `BeforeProcessingInitializeOnLoad`
  - `ProcessInitializeOnLoadAttributes`
  - `AwakeInstancesAfterBackupRestoration`
- recent measured reload samples are in the `105s - 153s` range
- recent measured `Asset Pipeline Refresh` spikes reach:
  - `118s`
  - `136s`
  - `145s`
  - `338s`
- Unity MCP `validate_script` is unreliable on some large old files and produces false
  duplicate-signature reports
- live console reads are more trustworthy than those false duplicate diagnostics
- `_Project` currently has no `.asmdef` files, so our code still compiles into the large
  default assemblies

## Third-Party Hotspots Seen In Hook Scans

These are not touched in the safe first wave, but they do show up as likely reload weight:

- Bakery
- GPU Instancer
- Astar Pathfinding Project
- MapMagic
- Amplify Impostors
- MoreMountains / MMPlaylist

Meaning:
- `_Project` is not the only source of reload pain
- our local cleanup still matters, but package/editor overhead is also real

## Safe Vendor Cleanup Applied

These are editor-only startup helpers or welcome/update windows.
We touched them because they are lower-risk than runtime/core package code:

### `Assets/AstarPathfindingProject/Editor/AstarUpdateChecker.cs`

Applied:
- no longer schedules its startup update loop blindly on every reload
- now skips startup scheduling in:
  - batch mode
  - playmode transition
- now only schedules a startup check when:
  - there is no cached server message
  - or an update check is actually due

Why safe:
- manual `CheckForUpdatesNow()` still works
- documentation URL hookup remains
- update checking still exists, but reload no longer always pays for it

### `Assets/Candice AI for Games/Scripts/Editor/CandiceAutorun.cs`

Applied:
- replaced unconditional `EditorApplication.update` startup hook with `delayCall`
- now skips startup window logic in:
  - batch mode
  - playmode transition

Why safe:
- startup window still works in normal editor use
- one-shot startup behavior remains
- it no longer adds an unnecessary editor update loop on every reload

### `Assets/Plugins/Editor/DarkTonic/MasterAudio/MasterAudioWelcomeWindow.cs`

Applied:
- replaced startup `EditorApplication.update` hook with `delayCall`
- startup check now skips in:
  - batch mode
  - playmode transition
- startup hook is no longer registered when `showOnStartPrefs` is already disabled

Why safe:
- the welcome window still opens manually from menu
- auto-open still works when enabled by the user
- reload no longer pays for a pointless startup check when the feature is disabled

### `Assets/AmplifyImpostors/Plugins/Editor/AIPackageManagerHelper.cs`

Applied:
- no longer requests package info blindly on every reload
- now skips startup scheduling in:
  - batch mode
  - playmode transition
- now skips startup package probing when `Preferences.GlobalAutoSRP` is disabled
- startup request now goes through `delayCall` instead of immediate static-constructor work

Why safe:
- manual `RequestInfo()` still works
- SRP auto-import logic still exists when the feature is enabled
- reload no longer pays for package-manager work when the feature is not in use

### `Assets/GPUInstancer/Scripts/Editor/GPUInstancerDefines.cs`

Applied:
- startup settings generation is no longer registered directly in the static constructor
- now skips startup scheduling in:
  - batch mode
  - playmode transition
- startup settings work is first deferred through `delayCall`, then moved into the existing update loop only when needed

Why safe:
- `GPU_INSTANCER` define initialization remains
- version/package setup still runs
- we only reduced how aggressively it injects itself into the editor startup path

## Additional Safe Vendor Cleanup

### `ftUpdater` (Bakery)

File:
- `Assets/Editor/x64/Bakery/scripts/ftUpdater.cs`

Changes:
- kept the patch workflow intact
- moved startup patch prompting off the static constructor fast path
- replaced immediate `EditorApplication.update += PatchAsk` with deferred startup registration
- added guards to skip startup work during:
  - batch mode
  - compile
  - asset update
  - play mode transition

Why safe:
- patch UI still exists
- manual "Check for patches" still works
- downloaded patch apply flow still exists
- we only stopped Bakery from trying to wake patch-apply logic at the worst possible reload moments

### `ftFixResettingGlobalsOnSave` (Bakery)

File:
- `Assets/Editor/x64/Bakery/scripts/ftFixResettingGlobalsOnSave.cs`

Changes:
- kept the global shader reset workaround intact
- replaced raw `EditorApplication.update` scheduling with a one-shot deferred call
- added guards for:
  - batch mode
  - play mode transition
  - compile
  - asset update
- made the post-save callback queue idempotent

Why safe:
- Bakery still restores global volume shader state after save
- the workaround no longer spams editor update or runs during bad reload windows

### `MasterAudioHierIcon`

File:
- `Assets/Plugins/Editor/DarkTonic/MasterAudio/MasterAudioHierIcon.cs`

Changes:
- moved hierarchy icon registration off the static constructor fast path
- deferred icon asset loading and callback registration through `EditorApplication.delayCall`
- added guards for:
  - batch mode
  - play mode transition
- made the hierarchy callback registration idempotent

Why safe:
- hierarchy icons still work
- no runtime audio behavior changed
- only the editor-only visual decoration was made less aggressive during reload

### `ES3Postprocessor` (Easy Save 3)

File:
- `Assets/Plugins/Easy Save 3/Editor/ES3Postprocessor.cs`

Changes:
- moved editor callback registration off the static constructor fast path
- deferred registration through `EditorApplication.delayCall`
- added guards for:
  - batch mode
  - compile
  - asset update
  - play mode transition
- made callback registration idempotent

Why safe:
- Easy Save reference manager refresh flow stays intact
- scene/object hooks still register in edit mode
- we only stopped the package from doing all of that directly inside reload-time static initialization

### `ftDefine` (Bakery)

File:
- `Assets/Editor/x64/Bakery/scripts/ftDefine.cs`

Changes:
- moved `BAKERY_INCLUDED` define setup off the static constructor fast path
- deferred it through `EditorApplication.delayCall`
- added guards for:
  - batch mode
  - compile
  - asset update
  - play mode transition

Why safe:
- the define still gets ensured
- active build target changes still re-apply the define
- we only removed the eager reload-time define mutation

### `AudioScriptOrderManager` (Master Audio)

File:
- `Assets/Plugins/Editor/DarkTonic/MasterAudio/AudioScriptOrderManager.cs`

Changes:
- moved runtime script execution-order scan off the static constructor fast path
- deferred it through `EditorApplication.delayCall`
- added guards for:
  - batch mode
  - compile
  - asset update
  - play mode transition

Why safe:
- execution order enforcement still happens in edit mode
- no runtime audio behavior changed
- the expensive scan across runtime mono scripts no longer runs directly during the hottest reload stage

## External Audit Helpers

These tools do not depend on Unity being responsive:

- `Tools/ReloadAudit/Analyze-EditorLog.ps1`
  - parses `Editor.log`
  - writes `UNITY_RELOAD_FINDINGS.md`
- `Tools/ReloadAudit/Scan-ReloadHooks.ps1`
  - scans `Assets + Packages`
  - writes `UNITY_RELOAD_HOOKS_REPORT.md`
- `Tools/ReloadAudit/Analyze-ProjectSplit.ps1`
  - scans `Assets/_Project`
  - counts runtime vs editor scripts
  - lists runtime files still carrying editor-coupling signals
  - writes `UNITY_PROJECT_SPLIT_REPORT.md`

## `_Project` Split Snapshot

From `UNITY_PROJECT_SPLIT_REPORT.md`:

- total `_Project` C# files: `246`
- runtime-side files: `207`
- editor-side files: `39`
- runtime files with editor-coupling signals: `77`

Meaning:
- a blind `_Project asmdef` split is not the right next move yet
- we first need to reduce editor-coupling in the biggest runtime-side offenders
- the top runtime-side coupling cluster is currently:
  - `HectonUnderwaterVisuals`
  - `HectonVoxelEngine`
  - `SkySystemFollowCamera`
  - `HectonWorldGenerator`
  - `ToolStagingSpawner`

### Runtime/Editor Partialization Progress

Safe `_Project` split prep already applied:
- moved `ObjectSpawner` into `Assets/_Project/Editor`
- restored earlier runtime/editor partial extraction attempt back into stable inline `#if UNITY_EDITOR`
  blocks for:
  - `ToolLoadoutProvisioner`
  - `ToolRuntimeSmokeTester`
  - `FieldToolRuntimeSmokeTester`
  - `PDALoadoutTab`

Why it matters:
- we removed a future compile risk before any real asmdef split
- gameplay/runtime classes are back to a safe single-file shape
- editor-only helper files that would cross runtime/editor assembly boundaries were deleted

## Additional `_Project` Editor Startup Cleanup

### `VisorOpaqueTextureEnsurer`

File:
- `Assets/_Project/Editor/VisorOpaqueTextureEnsurer.cs`

Changes:
- kept the URP opaque texture warning intact
- moved the warning check off the static initialization path into `EditorApplication.delayCall`
- added guards to skip or defer the check during:
  - batch mode
  - play mode transition

Why safe:
- visor refraction warnings still appear when relevant
- no runtime behavior changed
- this no longer spends startup work directly inside an `InitializeOnLoad` constructor

### `ObjectSpawner`

File:
- `Assets/_Project/Scripts/ObjectSpawner.cs`

Changes:
- marked the file as editor-only with `#if UNITY_EDITOR`
- kept the menu tool behavior unchanged

Why safe:
- this script is a pure editor debris spawning utility
- it has no gameplay/runtime responsibility
- removing it from runtime compilation reduces `_Project` editor coupling without changing the tool itself

## Next Safe Candidates

- build a fuller hook map for `_Project` scripts into:
  - `Protected`
  - `Safe To Defer`
  - `Safe To Disable In Editor`
  - `Risky`
- use:
  - `Hecton/Validation/Generate Unity Reload Audit Report`
  to generate a current markdown report once Unity finishes recompiling
- use the external PowerShell helpers when Unity is hanging
- current generated outputs:
  - `UNITY_RELOAD_FINDINGS.md`
  - `UNITY_RELOAD_HOOKS_REPORT.md`
- re-measure:
  - reload scripts
  - enter play
  - return from play
  - editor session stability

## Additional Safe `OnValidate` Guard Wave

Applied another non-visual authored/UI/data guard wave so these components do not waste
reload time during compile/update/playmode-transition windows:

- `AcousticZoneController`
- `BarterRuntimeSmokeTester`
- `BuilderRuntimeSmokeTester`
- `FaunaBiomeData`
- `HectonBoidController`
- `PlayerToolManager`
- `ToolLoadoutProvisioner`
- `ToolRuntimeSmokeTester`
- `PauseMenuController`
- `PDAControlsRebindUI`
- `PDAConstructionTab`
- `PDADataLogTab`

Why safe:

- these are not protected sky/water/atmosphere preview systems
- changes only short-circuit `OnValidate` during the exact editor states that already stall reload
- play-mode/runtime behavior is unchanged

Current `_Project` split snapshot after this wave:

- total `_Project` C# files: `243`
- runtime-side files: `207`
- editor-side files: `36`
- runtime files with editor coupling signals: `80`

Reading:

- this wave further reduces pointless edit-time churn even though it does not magically change
  every coupling count
- the remaining top coupling offenders still confirm the current strategy:
  keep shaving safe non-visual/runtime editor coupling first, do not rush `_Project asmdef`
  split, and keep protected visual workflow untouched

## Additional Safe Vendor Startup Cleanup

### `ES3ScriptingDefineSymbols`

File:
- `Assets/Plugins/Easy Save 3/Editor/ES3ScriptingDefineSymbols.cs`

Changes:
- removed direct define-setup execution from the static initialization path
- switched startup work to an idempotent deferred `delayCall`
- added guards to skip define work during:
  - batch mode
  - compile
  - asset update
  - playmode transition

Why safe:
- this is editor-only package startup work
- runtime save/load behavior is untouched
- define setup still happens, but no longer tries to mutate editor state in the hottest part of reload

### `NiceVibrationsDefineSymbols`

File:
- `Assets/Feel/NiceVibrations/Define/NiceVibrationsDefineSymbols.cs`

Changes:
- moved automatic define setup off the raw static initialization path
- switched it to a deferred idempotent `delayCall`
- added guards for:
  - batch mode
  - compile
  - asset update
  - playmode transition

Why safe:
- this is editor-only define maintenance
- runtime haptics/gameplay behavior is untouched
- it no longer mutates scripting defines in the hottest part of domain reload

### `PhysicsCreatorUpdater`

File:
- `Assets/Technie/PhysicsCreator/Updater/PhysicsCreatorUpdater.cs`

Changes:
- moved orphaned-file search work out of the raw static constructor
- switched it to a deferred idempotent `delayCall`
- added guards for:
  - batch mode
  - compile
  - asset update
  - playmode transition

Why safe:
- this is editor-only maintenance logic
- runtime collider/physics behavior is untouched
- package cleanup scanning no longer runs in the worst possible reload window

## Additional Safe Runtime Hook Cleanup

### `MMEventManager`

File:
- `Assets/Feel/MMTools/Tools/MMEvents/MMEventManager.cs`

Changes:
- removed `[ExecuteAlways]` from the static event manager type

Why safe:
- this class is static and does not need MonoBehaviour-style edit-mode execution semantics
- runtime event behavior is still driven by normal static storage and `RuntimeInitializeOnLoadMethod`
- this removes a misleading editor-hook signal without changing gameplay event flow

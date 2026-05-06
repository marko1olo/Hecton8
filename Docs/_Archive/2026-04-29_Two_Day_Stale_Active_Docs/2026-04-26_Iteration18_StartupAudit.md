# Iteration 18 Startup Audit
Date: 2026-04-29

Mandates followed:
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `DATA_Save_Persistence_Binary_Delta_Checksum.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `STRM_Persistent_Object_Registry.txt`

## Evidence A — Lore Deadlock Purge

File: `Assets/_Project/Scripts/Bootstrap/HectonLoreSystemsRoot.cs`

Exact diff excerpt:

```diff
-        private void TryApplyRuntimeLoreRecovery()
-        {
-            if (!Application.isPlaying || _runtimeLoreRecoveryAttempted)
-                return;
-
-            if (_narrativeDiscoveryCount > 0 && _audioLogPickupCount > 0)
-                return;
-
-            if (runtimeRecoveryRegistry == null)
-            {
-#if UNITY_EDITOR || DEVELOPMENT_BUILD
-                Debug.LogWarning(
-                    "[LoreSystemsRoot] Runtime lore recovery skipped. ColonistLoreRegistry is not assigned on LoreSystems.",
-                    this);
-#endif
-                return;
-            }
-
-            _runtimeLoreRecoveryAttempted = true;
-
-            int createdOrUpdatedCount = 0;
-            for (int i = 0; i < _runtimeRecoveryPlacements.Length; i++)
-            {
-                if (TryEnsureRuntimeRecoveryPlacement(_runtimeRecoveryPlacements[i]))
-                    createdOrUpdatedCount++;
-            }
-
-#if UNITY_EDITOR || DEVELOPMENT_BUILD
-            if (createdOrUpdatedCount > 0)
-            {
-                Debug.LogWarning(
-                    "[LoreSystemsRoot] Applied runtime lore recovery because the production scene had no placed player-facing lore. " +
-                    "This is a fail-safe, not a substitute for authored placement.",
-                    this);
-            }
-#endif
-        }
+        private void Awake()
+        {
+            // Runtime bootstrap must stay self-owned. Manual setup and validation remain
+            // available through inspector actions, but play-mode startup does not mutate scene state.
+            RefreshSystemStatus(false);
+        }
```

Verification:
- `rg -n "TryApplyRuntimeLoreRecovery|Applied runtime lore recovery|Runtime lore recovery" Assets Packages -g "*.cs"` returns no matches.
- `Awake()` now lives at line 45 and `OnEnable()` at line 52.
- `RefreshLoreContentStatus()` still contains `FindObjectsByType<...>` calls, but only in explicit validation/editor paths, not startup runtime mutation.

## Evidence B — `[InitializeOnLoad]` Scan

Command:

```text
rg -l "\[InitializeOnLoad\]|\[InitializeOnLoadMethod\]" Assets Packages -g "*.cs"
```

Before cleanup:

```text
Assets/_Project/Editor/UnityReloadAuditReport.cs
Assets/AmplifyImpostors/Plugins/Editor/AIPackageManagerHelper.cs
Assets/AmplifyImpostors/Plugins/Editor/AIStartScreen.cs
Assets/AstarPathfindingProject/Editor/AstarUpdateChecker.cs
Assets/Bakery/ftLightmaps.cs
Assets/Candice AI for Games/Scripts/Editor/CandiceAutorun.cs
Assets/Crest/Crest/Scripts/Editor/ScriptingDefineSymbols.cs
Assets/Feel/MMTools/Tools/MMAttributes/MMExecutionOrderAttribute.cs
Assets/Feel/NiceVibrations/Define/NiceVibrationsDefineSymbols.cs
Assets/GPUInstancer/Scripts/Editor/GPUInstancerDefines.cs
Assets/MapMagic/Nodes/Editor/GraphInspector.cs
Assets/Plugins/Demigiant/DOTweenPro/Editor/DOTweenAnimationInspector.cs
Assets/Plugins/Easy Save 3/Editor/ES3Postprocessor.cs
Assets/Plugins/Easy Save 3/Editor/ES3ScriptingDefineSymbols.cs
Assets/Plugins/Editor/DarkTonic/MasterAudio/AudioScriptOrderManager.cs
Assets/Plugins/Editor/DarkTonic/MasterAudio/MasterAudioHierIcon.cs
Assets/Plugins/Editor/DarkTonic/MasterAudio/MasterAudioWelcomeWindow.cs
Assets/RealtimeCSG/RealtimeCSG/Plugins/Editor/Scripts/Control/Managers/UpdateLoop.cs
Assets/Technie/PhysicsCreator/Updater/PhysicsCreatorUpdater.cs
Assets/VolumetricLightBeam/Scripts/Config.cs
Packages/com.coplaydev.unity-mcp/Editor/Helpers/ProjectIdentityUtility.cs
Packages/com.coplaydev.unity-mcp/Editor/Migrations/LegacyServerSrcMigration.cs
Packages/com.coplaydev.unity-mcp/Editor/Migrations/StdIoVersionMigration.cs
Packages/com.coplaydev.unity-mcp/Editor/Resources/MenuItems/GetMenuItems.cs
Packages/com.coplaydev.unity-mcp/Editor/Services/EditorStateCache.cs
Packages/com.coplaydev.unity-mcp/Editor/Services/HttpAutoStartHandler.cs
Packages/com.coplaydev.unity-mcp/Editor/Services/HttpBridgeReloadHandler.cs
Packages/com.coplaydev.unity-mcp/Editor/Services/McpEditorShutdownCleanup.cs
Packages/com.coplaydev.unity-mcp/Editor/Services/StdioBridgeReloadHandler.cs
Packages/com.coplaydev.unity-mcp/Editor/Services/Transport/TransportCommandDispatcher.cs
Packages/com.coplaydev.unity-mcp/Editor/Services/Transport/Transports/StdioBridgeHost.cs
Packages/com.coplaydev.unity-mcp/Editor/Setup/SetupWindowService.cs
Packages/com.coplaydev.unity-mcp/Editor/Tools/UnityReflect.cs
Packages/com.jbooth.microsplat.core/Scripts/Editor/MicroSplatBaseFeatures.cs
Packages/com.jbooth.microsplat.core/Scripts/Editor/MicroSplatDefines.cs
Packages/com.jbooth.microsplat.core/Scripts/Editor/MicroSplatGenerator.cs
Packages/com.jbooth.microsplat.core/Scripts/Editor/TextureArrayPreprocessor.cs
Packages/com.jbooth.microsplat.core/Scripts/VegetationStudio/Editor/MicroSplatVegetationStudio.cs
Packages/com.unity.shadergraph/Editor/Data/Nodes/NodeClassCache.cs
Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/AssetPostProcessors/MaterialPostprocessor.cs
Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/AssetPostProcessors/ShaderGraphMaterialsUpdater.cs
Packages/com.unity.shadergraph/Editor/Importers/RenderPipelineChangedCallback.cs
Packages/com.waveharmonic.crest/Editor/Scripts/Utility/Shared/DecoratedDrawer.cs
Packages/com.waveharmonic.crest/Runtime/Scripts/Utility/Shared/Component/EditorBehaviour.cs
```

After cleanup:

```text
Packages/com.unity.shadergraph/Editor/Data/Nodes/NodeClassCache.cs
Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/AssetPostProcessors/MaterialPostprocessor.cs
Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/AssetPostProcessors/ShaderGraphMaterialsUpdater.cs
Packages/com.unity.shadergraph/Editor/Importers/RenderPipelineChangedCallback.cs
```

Result:
- Third-party and first-party autorun source hooks reduced to zero.
- Remaining live source hooks are Unity ShaderGraph package code only.

## Evidence C — Exact asmdef Constraint

Constraint applied:

```text
HECTON8_ENABLE_OPTIONAL_ASSEMBLIES
```

Examples:

```json
"defineConstraints": [
  "HECTON8_ENABLE_EDITMODE_TESTS",
  "HECTON8_ENABLE_OPTIONAL_ASSEMBLIES"
]
```

```json
"defineConstraints": [
  "HECTON8_ENABLE_PLAYMODE_TESTS",
  "HECTON8_ENABLE_OPTIONAL_ASSEMBLIES"
]
```

```json
"defineConstraints": [
  "HECTON8_ENABLE_ENTITIES_DOTS",
  "HECTON8_HAS_ENTITIES_PACKAGE",
  "HECTON8_ENABLE_OPTIONAL_ASSEMBLIES"
]
```

```json
"defineConstraints": [
  "UNITY_TESTS_FRAMEWORK",
  "HECTON8_ENABLE_OPTIONAL_ASSEMBLIES"
]
```

Files explicitly updated include:
- `Assets/_Project/Tests/Editor/Hecton8.EditModeTests.asmdef`
- `Assets/_Project/Tests/PlayMode/Hecton8.PlayModeTests.asmdef`
- `Assets/_Project/Scripts/World/Dots/Hecton8.World.Dots.asmdef`
- `Packages/com.unity.shadergraph/Tests/Editor/Unity.ShaderGraph.Editor.Tests.asmdef`
- `Library/PackageCache/com.unity.inputsystem@21a28c3a6c83/DocCodeSamples.Tests/DocCodeSamples.asmdef`
- `Library/PackageCache/com.unity.visualscripting@8bed5ad90189/DocCodeExamples/Unity.VisualScripting.DocCodeExamples.asmdef`
- `Library/PackageCache/com.unity.test-framework@76560ee600cb/Tests/TestNewCustomAssembly/TestNewCustomAssembly.asmdef`
- `Library/PackageCache/com.unity.test-framework@76560ee600cb/UnityEngine.TestRunner/UnityEngine.TestRunner.asmdef`
- `Library/PackageCache/com.unity.test-framework.performance@0840f58e4562/Runtime/Unity.PerformanceTesting.asmdef`

Proof that the gate is absent by default:
- `ProjectSettings/ProjectSettings.asset` current `Standalone` define list does not contain `HECTON8_ENABLE_OPTIONAL_ASSEMBLIES`.

## Batch Verification

Logs produced:
- `.iter18_startup_audit.log`
- `.iter18_startup_audit_pass2.log`
- `.iter18_startup_audit_cold.log`
- `.iter18_startup_audit_pass3.log`

Measured domain reload facts:
- Previous logs:
  - `.iter17a_unity_batch.log` → `ProcessInitializeOnLoadAttributes (24851ms)`
  - `.iter17a_unity_batch.log` → `ProcessInitializeOnLoadAttributes (41775ms)`
- Latest pass:
  - `.iter18_startup_audit_pass3.log` → `ProcessInitializeOnLoadAttributes (15837ms)`
  - `.iter18_startup_audit_pass3.log` → `ProcessInitializeOnLoadMethodAttributes (2400ms)`

Current blocker that remains:
- Even after asmdef gating, deleting `Library/Bee`, and deleting `Library/ScriptAssemblies`, Unity still logs skipped package/test/doc assemblies on reload.
- `Packages/packages-lock.json` shows `com.unity.collections` transitively pulls:
  - `com.unity.test-framework`
  - `com.unity.test-framework.performance`
- The remaining skip spam therefore appears to be driven by Unity/package dependency reload behavior, not by the stripped source autorun hooks and not by live `Library/ScriptAssemblies` outputs.

Other log fact:
- `.iter18_startup_audit_pass3.log` still reports `UnassignedReferenceException: The variable m_AtlasTextures of TMP_FontAsset has not been assigned.` This is separate from the lore root purge.

Status:
- Lore startup deadlock path: source-fixed, `PENDING VERIFICATION` in live Play Mode.
- Third-party source autorun hooks: reduced to zero, verified by `rg`.
- Skipped assembly spam: source gating applied, but Unity/package reload still emits the invalid assembly list. `PENDING VERIFICATION`.

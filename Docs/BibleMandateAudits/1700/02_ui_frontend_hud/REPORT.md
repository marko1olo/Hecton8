# UI, Menus, HUD, Terminals, Localization, Settings

Status: STATIC BIBLE/MANDATE/CODEBASE AUDIT - RUNTIME PROOF NOT RUN
Date: 2026-06-02
Verdict: YELLOW_RUNTIME_STATIC_RISK_REVIEW_REQUIRED

## Scope

This report compares the current root bible routes and selected mandate registry files against static codebase evidence. It does not prove Unity import health, Play Mode behavior, profiler cost, memory use, visual quality, or device performance.

## Bibles Checked

- OK ui.md - 275 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK UI_MENU_SCREEN_STANDARDS.md - 169 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK UI_DIEGETIC_HUD_STANDARDS.md - 196 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK input.md - 98 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK settings.md - 99 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK localization.md - 97 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK accessibility.md - 129 lines; GlobalQualityWeight, proof, acceptance, rejection.

## Mandates Matched

- .agents-skills\CTRL_Device_Abstraction_Haptics.txt
- .agents-skills\OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- .agents-skills\OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- .agents-skills\UI_Data_Streaming_ZeroGC_Optimization.txt
- .agents-skills\UI_Diegetic_Physical_Interfaces.txt
- .agents-skills\UI_Localization_Babel_RTL_FontSwap_ZeroAlloc.txt

## Code/Asset Roots

- OK Assets\_Project\Scripts\UI
- OK Assets\_Project\Scripts\Visor
- OK Assets\_Project\Scripts\Input
- OK Assets\_Project\Scripts\Player
- OK Assets\_Project\Editor\HectonUIBuilder.cs
- OK Assets\_Project\Scripts\LocalizedWorldSign.cs
- OK Assets\_Project\Scripts\LocRegistry.cs
- OK Assets\_Project\Scripts\LocNumericBuffer.cs

## Static Evidence Found

Total matching files: 207. Showing first 80. Full list: _scans/02_ui_frontend_hud_evidence_files.txt.

- Assets\_Project\Editor\HectonUIBuilder.cs
- Assets\_Project\Scripts\Input\AccessibilitySettings.cs
- Assets\_Project\Scripts\Input\ControlRemapper.cs
- Assets\_Project\Scripts\Input\Determinism\DeterministicInputContracts.cs
- Assets\_Project\Scripts\Input\INPUT_MIGRATION_GUIDE.md
- Assets\_Project\Scripts\Input\InputBindingContracts.cs
- Assets\_Project\Scripts\Input\InputManager.cs
- Assets\_Project\Scripts\Input\UserOptionsPersistence.cs
- Assets\_Project\Scripts\LocalizedWorldSign.cs
- Assets\_Project\Scripts\LocNumericBuffer.cs
- Assets\_Project\Scripts\LocRegistry.cs
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs
- Assets\_Project\Scripts\Player\Movement\ZeroGMovementContracts.cs
- Assets\_Project\Scripts\Player\Movement\ZeroGMovementJobs.cs
- Assets\_Project\Scripts\Player\Movement\ZeroGMovementRuntime.cs
- Assets\_Project\Scripts\UI\AcousticEcholocationTranslator.cs
- Assets\_Project\Scripts\UI\AcousticRadarSphereRenderer.cs
- Assets\_Project\Scripts\UI\ActionProgressHUD.cs
- Assets\_Project\Scripts\UI\AnalogGaugeNeedle3D.cs
- Assets\_Project\Scripts\UI\ARWaypointOverlay.cs
- Assets\_Project\Scripts\UI\AudioWaveformAnimator.cs
- Assets\_Project\Scripts\UI\BabelSubtitleSyncRuntime.cs
- Assets\_Project\Scripts\UI\BaseIntegrityHUD.cs
- Assets\_Project\Scripts\UI\BeaconHUDElement.cs
- Assets\_Project\Scripts\UI\BIOSMessageStreamer.cs
- Assets\_Project\Scripts\UI\BlackBoxMetricDashboard.cs
- Assets\_Project\Scripts\UI\BuilderStatusOverlay.cs
- Assets\_Project\Scripts\UI\CharBufferPool.cs
- Assets\_Project\Scripts\UI\Diegetic\Hecton8.UI.Diegetic.asmdef
- Assets\_Project\Scripts\UI\DiegeticGlitchSurgeonRuntime.cs
- Assets\_Project\Scripts\UI\DiegeticHudManualLayout.cs
- Assets\_Project\Scripts\UI\DiegeticHudTextNode.cs
- Assets\_Project\Scripts\UI\DiegeticMenuCanvasUtility.cs
- Assets\_Project\Scripts\UI\DiegeticMenuRaycastReceiver.cs
- Assets\_Project\Scripts\UI\DiegeticPanelController.cs
- Assets\_Project\Scripts\UI\DiegeticPDAController.cs
- Assets\_Project\Scripts\UI\DiegeticTooltipSystem.cs
- Assets\_Project\Scripts\UI\DiegeticVisorHudMesh.cs
- Assets\_Project\Scripts\UI\Editor\BabelLocalizationManagerWindow.cs
- Assets\_Project\Scripts\UI\Editor\BabelSyncTunerWindow.cs
- Assets\_Project\Scripts\UI\Editor\DiegeticGlitchTunerWindow.cs
- Assets\_Project\Scripts\UI\Editor\DiegeticUiTunerWindow.cs
- Assets\_Project\Scripts\UI\Editor\HUDCanvasInquisition.cs
- Assets\_Project\Scripts\UI\Editor\HudHologramTunerWindow.cs
- Assets\_Project\Scripts\UI\Editor\MenuVisualVariantContractValidator15MM.cs
- Assets\_Project\Scripts\UI\Editor\Minigame_Canvas_Inquisition.cs
- Assets\_Project\Scripts\UI\Editor\OOP_Canvas_Scanner_SHINOBU_348.cs
- Assets\_Project\Scripts\UI\Editor\PDAEncyclopediaTunerWindow.cs
- Assets\_Project\Scripts\UI\Editor\PdaProjectionTunerWindow.cs
- Assets\_Project\Scripts\UI\Editor\SettingsPanelAnimatorEditor.cs
- Assets\_Project\Scripts\UI\Editor\TerminalOsDesignerWindow.cs
- Assets\_Project\Scripts\UI\Editor\UIAudioPlaceholderGenerator.cs
- Assets\_Project\Scripts\UI\EngineHealthOverlay.cs
- Assets\_Project\Scripts\UI\FakeRadarBlipController.cs
- Assets\_Project\Scripts\UI\FontAssetRecovery.cs
- Assets\_Project\Scripts\UI\FontStreamingManager.cs
- Assets\_Project\Scripts\UI\GlitchEncoder.cs
- Assets\_Project\Scripts\UI\GlitchTable.cs
- Assets\_Project\Scripts\UI\HectonOSBootManager.cs
- Assets\_Project\Scripts\UI\HectonSubmarineOsDisplay.cs
- Assets\_Project\Scripts\UI\HectonSystemsDebugUI.cs
- Assets\_Project\Scripts\UI\HectonTextNode.cs
- Assets\_Project\Scripts\UI\HectonUIScaler.cs
- Assets\_Project\Scripts\UI\HphiReactiveUiTelemetry.cs
- Assets\_Project\Scripts\UI\HudNumericStringCache.cs
- Assets\_Project\Scripts\UI\HUDSaveNotificationLink.cs
- Assets\_Project\Scripts\UI\InteractionUI.cs
- Assets\_Project\Scripts\UI\LabelSwapScheduler.cs
- Assets\_Project\Scripts\UI\LoadingScreenController.cs
- Assets\_Project\Scripts\UI\LoadingTipsDisplay.cs
- Assets\_Project\Scripts\UI\Localization\BabelLocalizationAssemblyMarker.cs
- Assets\_Project\Scripts\UI\Localization\H8LocHashes.cs
- Assets\_Project\Scripts\UI\Localization\Hecton8.UI.Localization.asmdef
- Assets\_Project\Scripts\UI\LocalizedFontResolver.cs
- Assets\_Project\Scripts\UI\LocalizedLayoutMirror.cs
- Assets\_Project\Scripts\UI\LocalizedTextMadnessFx.cs
- Assets\_Project\Scripts\UI\LocalizedTMPAutoSizer.cs
- Assets\_Project\Scripts\UI\LocOverflowHandler.cs
- Assets\_Project\Scripts\UI\MainMenuAtmosphereController.cs
- Assets\_Project\Scripts\UI\MainMenuAudioIntegration.cs

## Static Risk Suspects

These are suspects, not confirmed defects. Runtime suspects need code review. Editor/tool suspects are legal only if they cannot execute in gameplay/player hot paths.

Runtime suspects:
Total runtime suspects: 241. Showing first 80. Full list: _scans/02_ui_frontend_hud_runtime_risks.txt.

- Assets\_Project\Scripts\UI\BaseIntegrityHUD.cs:476:            Hecton8.Core.H8Debug.LogError("[BaseIntegrityEvents] Listener destroyed while still registered.");
- Assets\_Project\Scripts\UI\BaseIntegrityHUD.cs:701:            Hecton8.Core.H8Debug.LogException(exception);
- Assets\_Project\Scripts\UI\AcousticRadarSphereRenderer.cs:531:            mesh.RecalculateNormals();
- Assets\_Project\Scripts\UI\DiegeticMenuCanvasUtility.cs:74:            return Camera.main;
- Assets\_Project\Scripts\UI\DiegeticMenuCanvasUtility.cs:97:            root.GetComponentsInChildren(true, s_readableTextScratch); // COLD SCAN: main-menu setup only, never per-frame.
- Assets\_Project\Scripts\UI\DiegeticTooltipSystem.cs:1572:                payload = new NativeArray<byte>(
- Assets\_Project\Scripts\UI\DiegeticVisorHudMesh.cs:438:                _runtimeMesh = new Mesh(); // COLD ALLOC: Mesh[1] - visor physical projection surface - owner: DiegeticVisorHudMesh
- Assets\_Project\Scripts\UI\DiegeticVisorHudMesh.cs:802:                payload = new NativeArray<byte>(
- Assets\_Project\Scripts\UI\DiegeticGlitchSurgeonRuntime.cs:1221:                Hecton8.Core.H8Debug.LogError("SHINOBU_49 glitch DTO layout mismatch.");
- Assets\_Project\Scripts\UI\DiegeticGlitchSurgeonRuntime.cs:1301:                Allocator.Persistent,
- Assets\_Project\Scripts\UI\DiegeticGlitchSurgeonRuntime.cs:1331:            H8Memory.FreeRaw(buffer, Allocator.Persistent, SystemID.UI);
- Assets\_Project\Scripts\UI\DiegeticGlitchSurgeonRuntime.cs:2430:                NativeArray<byte> payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\UI\MainMenuAtmosphereController.cs:189:            renderer = quad.GetComponent<MeshRenderer>();
- Assets\_Project\Scripts\UI\DiegeticPDAController.cs:652:            tabletRoot.GetComponentsInChildren(true, _tabletVisibilityRenderers);
- Assets\_Project\Scripts\UI\DiegeticPDAController.cs:653:            tabletRoot.GetComponentsInChildren(true, _tabletVisibilityColliders);
- Assets\_Project\Scripts\UI\DiegeticPDAController.cs:654:            tabletRoot.GetComponentsInChildren(true, _tabletVisibilityCanvasGroups);
- Assets\_Project\Scripts\UI\PauseMenuController.cs:1428:            Hecton8.Core.H8Debug.LogError("[PauseMenuController] Save failed.");
- Assets\_Project\Scripts\UI\PauseMenuController.cs:1637:            Hecton8.Core.H8Debug.LogError("[PauseMenuController] Fatal pause-menu state.");
- Assets\_Project\Scripts\UI\HectonSystemsDebugUI.cs:225:                        Hecton8.Core.H8Debug.Log("[HectonSystemsDebugUI] Destroying duplicate runtime owner.", this);
- Assets\_Project\Scripts\UI\HectonSystemsDebugUI.cs:242:                Hecton8.Core.H8Debug.Log("[HectonSystemsDebugUI] Awake.", this);
- Assets\_Project\Scripts\UI\HectonSystemsDebugUI.cs:253:                Hecton8.Core.H8Debug.Log("[HectonSystemsDebugUI] OnEnable.", this);
- Assets\_Project\Scripts\UI\HectonSystemsDebugUI.cs:280:                Hecton8.Core.H8Debug.Log("[HectonSystemsDebugUI] OnDestroy.", this);
- Assets\_Project\Scripts\UI\HectonSystemsDebugUI.cs:1274:            Hecton8.Core.H8Debug.Log("[HectonSystemsDebugUI] runtime snapshot captured.", this);
- Assets\_Project\Scripts\UI\LocalizedFontResolver.cs:189:            if (font == null || font.material == null)
- Assets\_Project\Scripts\UI\LocalizedFontResolver.cs:353:                if (font.material != null && font.material.GetTexture(ShaderUtilities.ID_MainTex) != null)
- Assets\_Project\Scripts\UI\NotificationEvents.cs:667:            Hecton8.Core.H8Debug.LogException(exception);
- Assets\_Project\Scripts\UI\Navigation\DiegeticGyroCompassRuntime.cs:1796:                    payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\UI\FontStreamingManager.cs:633:            return font != null ? font.material : null;
- Assets\_Project\Scripts\UI\FontAssetRecovery.cs:131:            bool hasAtlasBinding = ResolveAtlasTexture(fontAsset) != null && fontAsset.material != null;
- Assets\_Project\Scripts\UI\FontAssetRecovery.cs:168:            Material fontMaterial = fontAsset.material;
- Assets\_Project\Scripts\UI\FontAssetRecovery.cs:209:                    return fontAsset.material != null
- Assets\_Project\Scripts\UI\FontAssetRecovery.cs:210:                        ? fontAsset.material.GetTexture(ShaderUtilities.ID_MainTex)
- Assets\_Project\Scripts\UI\FontAssetRecovery.cs:217:                return fontAsset.material != null
- Assets\_Project\Scripts\UI\FontAssetRecovery.cs:218:                    ? fontAsset.material.GetTexture(ShaderUtilities.ID_MainTex)
- Assets\_Project\Scripts\UI\FontAssetRecovery.cs:223:                return fontAsset.material != null
- Assets\_Project\Scripts\UI\FontAssetRecovery.cs:224:                    ? fontAsset.material.GetTexture(ShaderUtilities.ID_MainTex)
- Assets\_Project\Scripts\UI\FontAssetRecovery.cs:240:            if (fontAsset.material != null)
- Assets\_Project\Scripts\UI\FontAssetRecovery.cs:241:                textComponent.fontSharedMaterial = fontAsset.material;
- Assets\_Project\Scripts\UI\FontAssetRecovery.cs:347:                if (fontAsset.material != null)
- Assets\_Project\Scripts\UI\FontAssetRecovery.cs:348:                    EditorUtility.SetDirty(fontAsset.material);
- Assets\_Project\Scripts\UI\FontAssetRecovery.cs:423:            if (fontAsset == null || fontAsset.material != null)
- Assets\_Project\Scripts\UI\FontAssetRecovery.cs:448:            fontAsset.material = material;
- Assets\_Project\Scripts\UI\FontAssetRecovery.cs:508:            if (fontAsset.material != null &&
- Assets\_Project\Scripts\UI\FontAssetRecovery.cs:511:                fontAsset.material.SetTexture(ShaderUtilities.ID_MainTex, atlasTexture);
- Assets\_Project\Scripts\UI\FontAssetRecovery.cs:512:                EditorUtility.SetDirty(fontAsset.material);
- Assets\_Project\Scripts\UI\MenuVisualConceptDecorApplier.cs:80:                _root = rootObject.GetComponent<RectTransform>();
- Assets\_Project\Scripts\UI\MenuVisualConceptDecorApplier.cs:81:                _group = rootObject.GetComponent<CanvasGroup>();
- Assets\_Project\Scripts\UI\MenuVisualConceptDecorApplier.cs:90:                _group = _root.gameObject.GetComponent<CanvasGroup>();
- Assets\_Project\Scripts\UI\MenuVisualConceptDecorApplier.cs:111:            RectTransform rect = slotObject.GetComponent<RectTransform>();
- Assets\_Project\Scripts\UI\MenuVisualConceptDecorApplier.cs:112:            Image image = slotObject.GetComponent<Image>();
- Assets\_Project\Scripts\UI\SettingsComparisonView.cs:69:                Hecton8.Core.H8Debug.LogWarning("[SettingsComparisonView] Settings runtime is not registered. Comparison panel disabled.");
- Assets\_Project\Scripts\UI\LoadingScreenController.cs:136:                Hecton8.Core.H8Debug.LogError("[LoadingScreenController] Missing CanvasGroup component!");
- Assets\_Project\Scripts\UI\LoadingScreenController.cs:150:                Hecton8.Core.H8Debug.LogError("[LoadingScreenController] Loading panel not assigned!");
- Assets\_Project\Scripts\UI\RelayHUDRuntimeBootstrap.cs:42:            Hecton8.Core.H8Debug.LogWarning("[RelayHUDRuntimeBootstrap] Spawned RelayRouteMarker at runtime because the active HUD had none. This is a fail-safe, not a substitute for authored HUD setup.");
- Assets\_Project\Scripts\UI\PDAIntrusionManager.cs:246:            Hecton8.Core.H8Debug.LogError("[PDAIntrusionEvents] Listener destroyed while still registered.");
- Assets\_Project\Scripts\UI\PDAIntrusionManager.cs:430:            Hecton8.Core.H8Debug.LogException(exception);
- Assets\_Project\Scripts\UI\PDAEncyclopediaStreamer.cs:3027:            NativeArray<byte> payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\UI\PDADecryptionSpectrogramPanel.cs:899:                payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\UI\PDAShellChrome.cs:1712:            graphic.material = material;
- Assets\_Project\Scripts\UI\PDAMapTab.cs:483:                mapImage.material = null;
- Assets\_Project\Scripts\UI\UIAudioFeedback.cs:616:                    Hecton8.Core.H8Debug.Log("[UIAudioFeedback] Playback event.", this);
- Assets\_Project\Scripts\UI\WristHologramHudRuntime_PdaScreenProjector.cs:1370:                Hecton8.Core.H8Debug.LogError("Agent1335 PDA projection dump failed.");
- Assets\_Project\Scripts\UI\WristHologramHudRuntime_PdaScreenProjector.cs:1374:                Hecton8.Core.H8Debug.LogError("Agent1335 PDA projection dump failed.");
- Assets\_Project\Scripts\UI\WristHologramHudRuntime_PdaScreenProjector.cs:1378:                Hecton8.Core.H8Debug.LogError("Agent1335 PDA projection dump failed.");
- Assets\_Project\Scripts\UI\WristHologramHudRuntime_PdaScreenProjector.cs:1382:                Hecton8.Core.H8Debug.LogError("Agent1335 PDA projection dump failed.");
- Assets\_Project\Scripts\UI\WristHologramHudRuntime_PdaScreenProjector.cs:1386:                Hecton8.Core.H8Debug.LogError("Agent1335 PDA projection dump failed.");
- Assets\_Project\Scripts\UI\WristHologramHudRuntime_PdaScreenProjector.cs:1390:                Hecton8.Core.H8Debug.LogError("Agent1335 PDA projection dump failed.");
- Assets\_Project\Scripts\UI\ShaderCompassRibbon.cs:178:                _ribbonImage.material = _runtimeMaterial;
- Assets\_Project\Scripts\UI\TerminalOS\TerminalOsRuntime_TerminalProjection.cs:514:                Hecton8.Core.H8Debug.LogException(exception);
- Assets\_Project\Scripts\UI\TerminalOS\TerminalOsRuntime_TerminalProjection.cs:518:                Hecton8.Core.H8Debug.LogException(exception);
- Assets\_Project\Scripts\UI\TerminalOS\TerminalOsRuntime_TerminalProjection.cs:522:                Hecton8.Core.H8Debug.LogException(exception);
- Assets\_Project\Scripts\UI\TerminalOS\TerminalOsRuntime_TerminalProjection.cs:526:                Hecton8.Core.H8Debug.LogException(exception);
- Assets\_Project\Scripts\UI\TerminalOS\TerminalOsRuntime_TerminalProjection.cs:530:                Hecton8.Core.H8Debug.LogException(exception);
- Assets\_Project\Scripts\UI\TerminalOS\TerminalOsRuntime_TerminalProjection.cs:534:                Hecton8.Core.H8Debug.LogException(exception);
- Assets\_Project\Scripts\UI\SettingsPanelProfiler.cs:129:            Hecton8.Core.H8Debug.Log("[SettingsPanelProfiler] Apply metrics captured.");
- Assets\_Project\Scripts\UI\SettingsPanelProfiler.cs:133:                Hecton8.Core.H8Debug.LogWarning("[SettingsPanelProfiler] Apply time exceeded target.");
- Assets\_Project\Scripts\UI\SettingsPanelProfiler.cs:138:                Hecton8.Core.H8Debug.LogWarning("[SettingsPanelProfiler] GC allocation detected.");
- Assets\_Project\Scripts\UI\TopographicalSonar\TopographicalSonarSynthesizer.cs:542:                        Allocator.Persistent,
- Assets\_Project\Scripts\UI\TopographicalSonar\TopographicalSonarSynthesizer.cs:548:                        Allocator.Persistent,
- Assets\_Project\Scripts\UI\TopographicalSonar\TopographicalSonarSynthesizer.cs:554:                        Allocator.Persistent,

Editor/tool/static suspects:
Total editor/tool/static suspects: 128. Showing first 80. Full list: _scans/02_ui_frontend_hud_editor_tool_risks.txt.

- Assets\_Project\Scripts\UI\Editor\DiegeticUiTunerWindow.cs:111:                _statusLabel.text = "No runtime selected.";
- Assets\_Project\Scripts\UI\Editor\DiegeticUiTunerWindow.cs:115:            _statusLabel.text =
- Assets\_Project\Scripts\UI\Editor\BabelSyncTunerWindow.cs:110:            _stateLabel.text =
- Assets\_Project\Scripts\UI\Editor\BabelSyncTunerWindow.cs:130:                _decodedLabel.text = "Hash parse failed.";
- Assets\_Project\Scripts\UI\Editor\BabelSyncTunerWindow.cs:131:                _hexLabel.text = "Raw UTF-8 hex unavailable.";
- Assets\_Project\Scripts\UI\Editor\BabelSyncTunerWindow.cs:140:            _decodedLabel.text = found && decodedLength > 0
- Assets\_Project\Scripts\UI\Editor\BabelSyncTunerWindow.cs:146:                _hexLabel.text = "Raw UTF-8 hex unavailable for 0x" + hash.ToString("X8") + ".";
- Assets\_Project\Scripts\UI\Editor\BabelSyncTunerWindow.cs:151:            _hexLabel.text = hexLength > 0
- Assets\_Project\Scripts\UI\Editor\UIAudioPlaceholderGenerator.cs:43:            Debug.Log($"[UIAudioPlaceholderGenerator] Generated placeholder audio clips in {folderPath}");
- Assets\_Project\Scripts\UI\Editor\SettingsPanelAnimatorEditor.cs:60:                Debug.Log($"[SettingsPanelAnimatorEditor] Header CanvasGroup assigned: {header.name}");
- Assets\_Project\Scripts\UI\Editor\SettingsPanelAnimatorEditor.cs:82:                Debug.Log($"[SettingsPanelAnimatorEditor] Preset button CanvasGroups assigned: {presetGroups.Length}");
- Assets\_Project\Scripts\UI\Editor\SettingsPanelAnimatorEditor.cs:104:                Debug.Log($"[SettingsPanelAnimatorEditor] Settings row CanvasGroups assigned: {settingsGroups.Length}");
- Assets\_Project\Scripts\UI\Editor\SettingsPanelAnimatorEditor.cs:115:                Debug.Log($"[SettingsPanelAnimatorEditor] Action buttons CanvasGroup assigned: {actionsContainer.name}");
- Assets\_Project\Scripts\UI\Editor\SettingsPanelAnimatorEditor.cs:119:            Debug.Log("[SettingsPanelAnimatorEditor] Auto-setup complete!");
- Assets\_Project\Scripts\UI\Editor\SettingsPanelAnimatorEditor.cs:136:            Debug.Log("[SettingsPanelAnimatorEditor] CanvasGroups cleared.");
- Assets\_Project\Scripts\UI\Editor\PDAEncyclopediaTunerWindow.cs:76:                _stateLabel.text = "No PDAEncyclopediaStreamer in the loaded scene.";
- Assets\_Project\Scripts\UI\Editor\PDAEncyclopediaTunerWindow.cs:82:                _stateLabel.text = "Streamer found. Vault buffers are not available.";
- Assets\_Project\Scripts\UI\Editor\PDAEncyclopediaTunerWindow.cs:101:            _stateLabel.text =
- Assets\_Project\Scripts\UI\Editor\PDAEncyclopediaTunerWindow.cs:159:                _rawLabel.text = new string(_rawBuffer, 0, written);
- Assets\_Project\Scripts\UI\Editor\PDAEncyclopediaTunerWindow.cs:161:                _rawLabel.text = "Raw UTF-8 x-ray unavailable for this hash.";
- Assets\_Project\Scripts\UI\Editor\HUDCanvasInquisition.cs:56:                hudCanvasCount += prefab.GetComponentsInChildren<Canvas>(true).Length;
- Assets\_Project\Scripts\UI\Editor\HUDCanvasInquisition.cs:75:            Debug.Log($"HUD Canvas Inquisition wrote {reportPath}");
- Assets\_Project\Scripts\UI\Editor\HUDCanvasInquisition.cs:111:            builder.AppendLine("  \"stencilWaypointOcclusionDearLie\": \"Stencil waypoint occlusion is a bounded cone/distance fake over at most 16 AUP-local rows; no HZB readback, MeshCollider, raycast, or per-waypoint GameObject renderer is used.\",");
- Assets\_Project\Scripts\UI\Editor\HUDCanvasInquisition.cs:349:            Component[] components = root.GetComponentsInChildren<Component>(true);
- Assets\_Project\Scripts\UI\Editor\Minigame_Canvas_Inquisition.cs:44:            Debug.Log("Terminal minigame canvas inquisition wrote " + reportPath);
- Assets\_Project\Scripts\UI\Editor\MenuVisualVariantContractValidator15MM.cs:20:            Debug.Log("15MM menu visual variants validated: 15 styles, 12 concepts, 180 combinations.");
- Assets\_Project\Scripts\UI\TerminalOS\Editor\TerminalOsLayoutValidator.cs:80:                Debug.Log("[SHINOBU_137/273/331] Terminal OS DTO layout validated.");
- Assets\_Project\Scripts\UI\TerminalOS\Editor\TerminalOsLayoutValidator.cs:91:            Debug.LogError("[SHINOBU_137/273/331] DTO size mismatch: " + typeof(T).Name + " expected " + expected + " observed " + observed);
- Assets\_Project\Scripts\UI\TerminalOS\Editor\TerminalOsLayoutValidator.cs:102:            Debug.LogError("[SHINOBU_137/273/331] DTO offset mismatch: " + typeof(T).Name + "." + fieldName + " expected " + expected + " observed " + observed);
- Assets\_Project\Scripts\UI\TerminalOS\Editor\OscilloscopeDecryptionTunerWindow.cs:225:                _status.text = "No runtime selected.";
- Assets\_Project\Scripts\UI\TerminalOS\Editor\OscilloscopeDecryptionTunerWindow.cs:232:                _status.text = "Puzzle DTO unavailable or job in flight.";
- Assets\_Project\Scripts\UI\TerminalOS\Editor\OscilloscopeDecryptionTunerWindow.cs:251:            _status.text = "Telemetry readout active.";
- Assets\_Project\Scripts\UI\TerminalOS\Editor\OOP_Canvas_Scanner.cs:41:            Debug.Log("OOP Canvas Scanner wrote " + reportPath);
- Assets\_Project\Scripts\UI\TerminalOS\Editor\DiegeticTerminalXRayWindow.cs:166:                _status.text = "No runtime selected.";
- Assets\_Project\Scripts\UI\TerminalOS\Editor\DiegeticTerminalXRayWindow.cs:172:                _status.text = "Telemetry unavailable. Enter Play Mode or select an initialized TerminalOsRuntime.";
- Assets\_Project\Scripts\UI\TerminalOS\Editor\DiegeticTerminalXRayWindow.cs:185:            _status.text = "Vault buffer 71381, projection DTO stride 64, rollback excluded.";
- Assets\_Project\Scripts\Visor\Editor\VisorHudArTunerWindow.cs:71:                    _status.text = "Telemetry unavailable";
- Assets\_Project\Scripts\Visor\Editor\VisorHudArTunerWindow.cs:81:                _status.text = $"Targets {entry.TargetCount:0} | CPU {entry.ProjectionMicroseconds:0.00} us | GPU est {entry.EstimatedGpuMicroseconds:0.00} us | Q {entry.QualityWeight:0.00}";
- Assets\_Project\Scripts\Visor\Editor\ScreenSpaceDecalTunerWindow.cs:214:            _csvLabel.text = loaded
- Assets\_Project\Scripts\Visor\Editor\ScreenSpaceDecalTunerWindow.cs:241:            _statsLabel.text = string.Concat(
- Assets\_Project\Scripts\Visor\Editor\ScreenSpaceDecalTunerWindow.cs:269:                _bridgeLabel.text = string.Concat(
- Assets\_Project\Scripts\Visor\Editor\ScreenSpaceDecalTunerWindow.cs:285:                _layoutLabel.text =
- Assets\_Project\Scripts\Visor\Editor\ScreenSpaceDecalTunerWindow.cs:298:                _validationLabel.text = string.Concat(
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs:33:            NativeArray<ZeroGTestResultDTO> result = new NativeArray<ZeroGTestResultDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs:59:            NativeArray<ZeroGTestResultDTO> result = new NativeArray<ZeroGTestResultDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs:83:            NativeArray<ZeroGMovementStateDTO> state = new NativeArray<ZeroGMovementStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs:84:            NativeArray<ZeroGInputStateDTO> input = new NativeArray<ZeroGInputStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs:85:            NativeArray<ZeroGTuningDTO> tuning = new NativeArray<ZeroGTuningDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs:86:            NativeArray<ZeroGSurfaceHitDTO> surface = new NativeArray<ZeroGSurfaceHitDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs:87:            NativeArray<ZeroGSolverOutputDTO> output = new NativeArray<ZeroGSolverOutputDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs:88:            NativeArray<ZeroGTelemetryEntry> telemetry = new NativeArray<ZeroGTelemetryEntry>(300, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs:89:            NativeArray<int> cursor = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs:164:            NativeArray<ZeroGMovementStateDTO> state = new NativeArray<ZeroGMovementStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs:165:            NativeArray<ZeroGInputStateDTO> input = new NativeArray<ZeroGInputStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs:166:            NativeArray<ZeroGTuningDTO> tuning = new NativeArray<ZeroGTuningDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs:167:            NativeArray<ZeroGSurfaceHitDTO> surface = new NativeArray<ZeroGSurfaceHitDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs:168:            NativeArray<ZeroGSolverOutputDTO> output = new NativeArray<ZeroGSolverOutputDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs:169:            NativeArray<ZeroGTelemetryEntry> telemetry = new NativeArray<ZeroGTelemetryEntry>(300, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs:170:            NativeArray<int> cursor = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs:242:            NativeArray<ZeroGMovementStateDTO> state = new NativeArray<ZeroGMovementStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs:243:            NativeArray<ZeroGInputStateDTO> input = new NativeArray<ZeroGInputStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs:244:            NativeArray<ZeroGTuningDTO> tuning = new NativeArray<ZeroGTuningDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs:245:            NativeArray<ZeroGSurfaceHitDTO> surface = new NativeArray<ZeroGSurfaceHitDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs:246:            NativeArray<ZeroGSolverOutputDTO> output = new NativeArray<ZeroGSolverOutputDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs:247:            NativeArray<ZeroGTelemetryEntry> telemetry = new NativeArray<ZeroGTelemetryEntry>(300, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs:248:            NativeArray<int> cursor = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs:325:            NativeArray<ZeroGMovementStateDTO> state = new NativeArray<ZeroGMovementStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs:326:            NativeArray<ZeroGInputStateDTO> input = new NativeArray<ZeroGInputStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs:327:            NativeArray<ZeroGTuningDTO> tuning = new NativeArray<ZeroGTuningDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs:328:            NativeArray<ZeroGSurfaceHitDTO> surface = new NativeArray<ZeroGSurfaceHitDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs:329:            NativeArray<ZeroGSolverOutputDTO> output = new NativeArray<ZeroGSolverOutputDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs:330:            NativeArray<ZeroGTelemetryEntry> telemetry = new NativeArray<ZeroGTelemetryEntry>(300, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs:331:            NativeArray<int> cursor = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs:409:            NativeArray<ZeroGMovementStateDTO> state = new NativeArray<ZeroGMovementStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs:410:            NativeArray<ZeroGInputStateDTO> input = new NativeArray<ZeroGInputStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs:411:            NativeArray<ZeroGTuningDTO> tuning = new NativeArray<ZeroGTuningDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs:412:            NativeArray<ZeroGSurfaceHitDTO> surface = new NativeArray<ZeroGSurfaceHitDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs:413:            NativeArray<ZeroGSolverOutputDTO> output = new NativeArray<ZeroGSolverOutputDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs:414:            NativeArray<ZeroGTelemetryEntry> telemetry = new NativeArray<ZeroGTelemetryEntry>(300, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Player\Movement\Editor\ZeroGMovementEditTests1600.cs:415:            NativeArray<int> cursor = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);

## Exists / Missing / Required Proof

- Exists: bible routes exist and static implementation evidence was found.
- Partial: runtime static risk suspects need manual code review.
- Editor/tool: static suspects exist but may be legal if editor-only or cold-path.
- Required proof: Desktop/mobile screenshots, controller navigation proof, localization expansion capture, 0 B UI text update proof, and menu/HUD state-truth trace.

## Next Audit Action

Classify each runtime suspect as cold-path/legal or runtime violation. Fix runtime violations before profiler proof.

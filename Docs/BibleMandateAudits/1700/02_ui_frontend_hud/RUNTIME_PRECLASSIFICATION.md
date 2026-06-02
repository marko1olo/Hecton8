# Runtime Preclassification - UI, Menus, HUD, Terminals, Localization, Settings

Status: HEURISTIC FIRST PASS - MANUAL REVIEW STILL REQUIRED
Date: 2026-06-02

This file groups static runtime suspects by a conservative heuristic. It can reduce review time, but it cannot prove a line is legal or illegal without reading the containing method and owner phase.

Total runtime suspects: 241.

## Summary

- LEGAL_EDITOR_OR_DEV_GUARDED: 123
- REVIEW_RUNTIME_MESH_MATERIAL_PATH: 55
- REVIEW_CACHE_OR_INJECTION_REQUIRED: 30
- REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED: 18
- LIKELY_LEGAL_COLD_OR_OWNER_LIFETIME_PATH: 13
- LIKELY_LEGAL_COLD_LOOKUP: 1
- LIKELY_LEGAL_COLD_PATH: 1

## LEGAL_EDITOR_OR_DEV_GUARDED (123)

- Runtime debug logging | Assets\_Project\Scripts\UI\BaseIntegrityHUD.cs:476:            Hecton8.Core.H8Debug.LogError("[BaseIntegrityEvents] Listener destroyed while still registered.");
- Runtime debug logging | Assets\_Project\Scripts\UI\BaseIntegrityHUD.cs:701:            Hecton8.Core.H8Debug.LogException(exception);
- Runtime debug logging | Assets\_Project\Scripts\UI\DiegeticGlitchSurgeonRuntime.cs:1221:                Hecton8.Core.H8Debug.LogError("SHINOBU_49 glitch DTO layout mismatch.");
- Runtime debug logging | Assets\_Project\Scripts\UI\PauseMenuController.cs:1428:            Hecton8.Core.H8Debug.LogError("[PauseMenuController] Save failed.");
- Runtime debug logging | Assets\_Project\Scripts\UI\PauseMenuController.cs:1637:            Hecton8.Core.H8Debug.LogError("[PauseMenuController] Fatal pause-menu state.");
- Runtime debug logging | Assets\_Project\Scripts\UI\HectonSystemsDebugUI.cs:225:                        Hecton8.Core.H8Debug.Log("[HectonSystemsDebugUI] Destroying duplicate runtime owner.", this);
- Runtime debug logging | Assets\_Project\Scripts\UI\HectonSystemsDebugUI.cs:242:                Hecton8.Core.H8Debug.Log("[HectonSystemsDebugUI] Awake.", this);
- Runtime debug logging | Assets\_Project\Scripts\UI\HectonSystemsDebugUI.cs:253:                Hecton8.Core.H8Debug.Log("[HectonSystemsDebugUI] OnEnable.", this);
- Runtime debug logging | Assets\_Project\Scripts\UI\HectonSystemsDebugUI.cs:280:                Hecton8.Core.H8Debug.Log("[HectonSystemsDebugUI] OnDestroy.", this);
- Runtime debug logging | Assets\_Project\Scripts\UI\HectonSystemsDebugUI.cs:1274:            Hecton8.Core.H8Debug.Log("[HectonSystemsDebugUI] runtime snapshot captured.", this);
- Runtime debug logging | Assets\_Project\Scripts\UI\NotificationEvents.cs:667:            Hecton8.Core.H8Debug.LogException(exception);
- Runtime mesh/material mutation | Assets\_Project\Scripts\UI\FontAssetRecovery.cs:348:                    EditorUtility.SetDirty(fontAsset.material);
- Runtime mesh/material mutation | Assets\_Project\Scripts\UI\FontAssetRecovery.cs:512:                EditorUtility.SetDirty(fontAsset.material);
- Runtime debug logging | Assets\_Project\Scripts\UI\SettingsComparisonView.cs:69:                Hecton8.Core.H8Debug.LogWarning("[SettingsComparisonView] Settings runtime is not registered. Comparison panel disabled.");
- Runtime debug logging | Assets\_Project\Scripts\UI\LoadingScreenController.cs:136:                Hecton8.Core.H8Debug.LogError("[LoadingScreenController] Missing CanvasGroup component!");
- Runtime debug logging | Assets\_Project\Scripts\UI\LoadingScreenController.cs:150:                Hecton8.Core.H8Debug.LogError("[LoadingScreenController] Loading panel not assigned!");
- Runtime debug logging | Assets\_Project\Scripts\UI\RelayHUDRuntimeBootstrap.cs:42:            Hecton8.Core.H8Debug.LogWarning("[RelayHUDRuntimeBootstrap] Spawned RelayRouteMarker at runtime because the active HUD had none. This is a fail-safe, not a substitute for authored HUD setup.");
- Runtime debug logging | Assets\_Project\Scripts\UI\PDAIntrusionManager.cs:246:            Hecton8.Core.H8Debug.LogError("[PDAIntrusionEvents] Listener destroyed while still registered.");
- Runtime debug logging | Assets\_Project\Scripts\UI\PDAIntrusionManager.cs:430:            Hecton8.Core.H8Debug.LogException(exception);
- Runtime debug logging | Assets\_Project\Scripts\UI\UIAudioFeedback.cs:616:                    Hecton8.Core.H8Debug.Log("[UIAudioFeedback] Playback event.", this);
- Runtime debug logging | Assets\_Project\Scripts\UI\WristHologramHudRuntime_PdaScreenProjector.cs:1370:                Hecton8.Core.H8Debug.LogError("Agent1335 PDA projection dump failed.");
- Runtime debug logging | Assets\_Project\Scripts\UI\WristHologramHudRuntime_PdaScreenProjector.cs:1374:                Hecton8.Core.H8Debug.LogError("Agent1335 PDA projection dump failed.");
- Runtime debug logging | Assets\_Project\Scripts\UI\WristHologramHudRuntime_PdaScreenProjector.cs:1378:                Hecton8.Core.H8Debug.LogError("Agent1335 PDA projection dump failed.");
- Runtime debug logging | Assets\_Project\Scripts\UI\WristHologramHudRuntime_PdaScreenProjector.cs:1382:                Hecton8.Core.H8Debug.LogError("Agent1335 PDA projection dump failed.");
- Runtime debug logging | Assets\_Project\Scripts\UI\WristHologramHudRuntime_PdaScreenProjector.cs:1386:                Hecton8.Core.H8Debug.LogError("Agent1335 PDA projection dump failed.");
- Runtime debug logging | Assets\_Project\Scripts\UI\WristHologramHudRuntime_PdaScreenProjector.cs:1390:                Hecton8.Core.H8Debug.LogError("Agent1335 PDA projection dump failed.");
- Runtime debug logging | Assets\_Project\Scripts\UI\TerminalOS\TerminalOsRuntime_TerminalProjection.cs:514:                Hecton8.Core.H8Debug.LogException(exception);
- Runtime debug logging | Assets\_Project\Scripts\UI\TerminalOS\TerminalOsRuntime_TerminalProjection.cs:518:                Hecton8.Core.H8Debug.LogException(exception);
- Runtime debug logging | Assets\_Project\Scripts\UI\TerminalOS\TerminalOsRuntime_TerminalProjection.cs:522:                Hecton8.Core.H8Debug.LogException(exception);
- Runtime debug logging | Assets\_Project\Scripts\UI\TerminalOS\TerminalOsRuntime_TerminalProjection.cs:526:                Hecton8.Core.H8Debug.LogException(exception);
- Runtime debug logging | Assets\_Project\Scripts\UI\TerminalOS\TerminalOsRuntime_TerminalProjection.cs:530:                Hecton8.Core.H8Debug.LogException(exception);
- Runtime debug logging | Assets\_Project\Scripts\UI\TerminalOS\TerminalOsRuntime_TerminalProjection.cs:534:                Hecton8.Core.H8Debug.LogException(exception);
- Runtime debug logging | Assets\_Project\Scripts\UI\SettingsPanelProfiler.cs:129:            Hecton8.Core.H8Debug.Log("[SettingsPanelProfiler] Apply metrics captured.");
- Runtime debug logging | Assets\_Project\Scripts\UI\SettingsPanelProfiler.cs:133:                Hecton8.Core.H8Debug.LogWarning("[SettingsPanelProfiler] Apply time exceeded target.");
- Runtime debug logging | Assets\_Project\Scripts\UI\SettingsPanelProfiler.cs:138:                Hecton8.Core.H8Debug.LogWarning("[SettingsPanelProfiler] GC allocation detected.");
- Runtime debug logging | Assets\_Project\Scripts\UI\SettingsPanel.cs:367:                Hecton8.Core.H8Debug.LogWarning("[SettingsPanel] SettingsManager runtime is null. Settings unavailable.");
- Runtime debug logging | Assets\_Project\Scripts\UI\SettingsPanel.cs:2004:                Hecton8.Core.H8Debug.LogWarning("[SettingsPanel] Apply button on cooldown. Please wait.");
- Runtime debug logging | Assets\_Project\Scripts\UI\WristHologramHudRuntime.cs:1730:            Hecton8.Core.H8Debug.LogWarning("[Agent1335] Legacy font atlas discovery failed.");
- Runtime debug logging | Assets\_Project\Scripts\UI\WristHologramHudRuntime.cs:2156:                Hecton8.Core.H8Debug.LogError("Agent1335 wrist HUD blackbox dump failed.");
- Runtime debug logging | Assets\_Project\Scripts\UI\WristHologramHudRuntime.cs:2160:                Hecton8.Core.H8Debug.LogError("Agent1335 wrist HUD blackbox dump failed.");
- Additional lines omitted here: 83. Use `../_scans/02_ui_frontend_hud_runtime_risks.txt` for the full list.

## REVIEW_RUNTIME_MESH_MATERIAL_PATH (55)

- Runtime mesh/material mutation | Assets\_Project\Scripts\UI\AcousticRadarSphereRenderer.cs:531:            mesh.RecalculateNormals();
- Runtime mesh/material mutation | Assets\_Project\Scripts\UI\LocalizedFontResolver.cs:189:            if (font == null || font.material == null)
- Runtime mesh/material mutation | Assets\_Project\Scripts\UI\LocalizedFontResolver.cs:353:                if (font.material != null && font.material.GetTexture(ShaderUtilities.ID_MainTex) != null)
- Runtime mesh/material mutation | Assets\_Project\Scripts\UI\FontStreamingManager.cs:633:            return font != null ? font.material : null;
- Runtime mesh/material mutation | Assets\_Project\Scripts\UI\FontAssetRecovery.cs:131:            bool hasAtlasBinding = ResolveAtlasTexture(fontAsset) != null && fontAsset.material != null;
- Runtime mesh/material mutation | Assets\_Project\Scripts\UI\FontAssetRecovery.cs:168:            Material fontMaterial = fontAsset.material;
- Runtime mesh/material mutation | Assets\_Project\Scripts\UI\FontAssetRecovery.cs:209:                    return fontAsset.material != null
- Runtime mesh/material mutation | Assets\_Project\Scripts\UI\FontAssetRecovery.cs:210:                        ? fontAsset.material.GetTexture(ShaderUtilities.ID_MainTex)
- Runtime mesh/material mutation | Assets\_Project\Scripts\UI\FontAssetRecovery.cs:217:                return fontAsset.material != null
- Runtime mesh/material mutation | Assets\_Project\Scripts\UI\FontAssetRecovery.cs:218:                    ? fontAsset.material.GetTexture(ShaderUtilities.ID_MainTex)
- Runtime mesh/material mutation | Assets\_Project\Scripts\UI\FontAssetRecovery.cs:223:                return fontAsset.material != null
- Runtime mesh/material mutation | Assets\_Project\Scripts\UI\FontAssetRecovery.cs:224:                    ? fontAsset.material.GetTexture(ShaderUtilities.ID_MainTex)
- Runtime mesh/material mutation | Assets\_Project\Scripts\UI\FontAssetRecovery.cs:240:            if (fontAsset.material != null)
- Runtime mesh/material mutation | Assets\_Project\Scripts\UI\FontAssetRecovery.cs:241:                textComponent.fontSharedMaterial = fontAsset.material;
- Runtime mesh/material mutation | Assets\_Project\Scripts\UI\FontAssetRecovery.cs:347:                if (fontAsset.material != null)
- Runtime mesh/material mutation | Assets\_Project\Scripts\UI\FontAssetRecovery.cs:423:            if (fontAsset == null || fontAsset.material != null)
- Runtime mesh/material mutation | Assets\_Project\Scripts\UI\FontAssetRecovery.cs:448:            fontAsset.material = material;
- Runtime mesh/material mutation | Assets\_Project\Scripts\UI\FontAssetRecovery.cs:508:            if (fontAsset.material != null &&
- Runtime mesh/material mutation | Assets\_Project\Scripts\UI\FontAssetRecovery.cs:511:                fontAsset.material.SetTexture(ShaderUtilities.ID_MainTex, atlasTexture);
- Runtime mesh/material mutation | Assets\_Project\Scripts\UI\PDAShellChrome.cs:1712:            graphic.material = material;
- Runtime mesh/material mutation | Assets\_Project\Scripts\UI\PDAMapTab.cs:483:                mapImage.material = null;
- Runtime mesh/material mutation | Assets\_Project\Scripts\UI\ShaderCompassRibbon.cs:178:                _ribbonImage.material = _runtimeMaterial;
- Runtime mesh/material mutation | Assets\_Project\Scripts\UI\VehicleSubOsCockpitRuntime.cs:2601:            mesh.RecalculateNormals();
- Runtime mesh/material mutation | Assets\_Project\Scripts\UI\SuitHUDV4CanvasOverlay.cs:2725:                image.material = _ditheredUiBackgroundMaterial;
- Runtime mesh/material mutation | Assets\_Project\Scripts\UI\SuitHUDV4CanvasOverlay.cs:2756:                _savingProgressDataLamp.material = null;
- Runtime mesh/material mutation | Assets\_Project\Scripts\UI\SuitHUDV4CanvasOverlay.cs:2760:                _savingProgressDataNeedle.material = null;
- Runtime mesh/material mutation | Assets\_Project\Scripts\UI\SuitHUDV4CanvasOverlay.cs:2777:                _savingProgressDataLamp.material = _savingProgressDataPulseMaterial;
- Runtime mesh/material mutation | Assets\_Project\Scripts\UI\SuitHUDV4CanvasOverlay.cs:2783:                _savingProgressDataNeedle.material = _savingProgressDataPulseMaterial;
- Runtime mesh/material mutation | Assets\_Project\Scripts\UI\SuitHUDV4CanvasOverlay.cs:2813:                _acousticRadarOverlay.material = null;
- Runtime mesh/material mutation | Assets\_Project\Scripts\UI\SuitHUDV4CanvasOverlay.cs:3594:            _acousticRadarOverlay.material = _acousticRadarMaterial;
- Runtime mesh/material mutation | Assets\_Project\Scripts\UI\SuitHUDV4CanvasOverlay.cs:5491:                refs.Icon.material = null;
- Runtime mesh/material mutation | Assets\_Project\Scripts\UI\SuitHUDV4CanvasOverlay.cs:5812:            image.material = null;
- Runtime mesh/material mutation | Assets\_Project\Scripts\UI\SuitHUDV4CanvasOverlay.cs:5853:            Material material = fontAsset.material;
- Runtime mesh/material mutation | Assets\_Project\Scripts\UI\SuitHUDV4CanvasOverlay.cs:7347:                image.material = null;
- Runtime mesh/material mutation | Assets\_Project\Scripts\Visor\HectonBiolumSSGIFeature.cs:353:                    passData.material = _compositeMaterial;
- Runtime mesh/material mutation | Assets\_Project\Scripts\Visor\HectonBiolumSSGIFeature.cs:365:                        if (data.material == null)
- Runtime mesh/material mutation | Assets\_Project\Scripts\Visor\HectonBiolumSSGIFeature.cs:370:                        CoreUtils.DrawFullScreen(context.cmd, data.material, null, data.shaderPassIndex);
- Runtime mesh/material mutation | Assets\_Project\Scripts\Visor\HectonBiolumSSGIFeature.cs:398:                    passData.material = _compositeMaterial;
- Runtime mesh/material mutation | Assets\_Project\Scripts\Visor\HectonBiolumSSGIFeature.cs:415:                        if (data.material == null)
- Runtime mesh/material mutation | Assets\_Project\Scripts\Visor\HectonBiolumSSGIFeature.cs:427:                        CoreUtils.DrawFullScreen(cmd, data.material, null, 1);
- Additional lines omitted here: 15. Use `../_scans/02_ui_frontend_hud_runtime_risks.txt` for the full list.

## REVIEW_CACHE_OR_INJECTION_REQUIRED (30)

- Unity scene lookup | Assets\_Project\Scripts\UI\DiegeticMenuCanvasUtility.cs:74:            return Camera.main;
- Unity scene lookup | Assets\_Project\Scripts\UI\MainMenuAtmosphereController.cs:189:            renderer = quad.GetComponent<MeshRenderer>();
- Unity scene lookup | Assets\_Project\Scripts\UI\DiegeticPDAController.cs:652:            tabletRoot.GetComponentsInChildren(true, _tabletVisibilityRenderers);
- Unity scene lookup | Assets\_Project\Scripts\UI\DiegeticPDAController.cs:653:            tabletRoot.GetComponentsInChildren(true, _tabletVisibilityColliders);
- Unity scene lookup | Assets\_Project\Scripts\UI\DiegeticPDAController.cs:654:            tabletRoot.GetComponentsInChildren(true, _tabletVisibilityCanvasGroups);
- Unity scene lookup | Assets\_Project\Scripts\UI\MenuVisualConceptDecorApplier.cs:80:                _root = rootObject.GetComponent<RectTransform>();
- Unity scene lookup | Assets\_Project\Scripts\UI\MenuVisualConceptDecorApplier.cs:81:                _group = rootObject.GetComponent<CanvasGroup>();
- Unity scene lookup | Assets\_Project\Scripts\UI\MenuVisualConceptDecorApplier.cs:90:                _group = _root.gameObject.GetComponent<CanvasGroup>();
- Unity scene lookup | Assets\_Project\Scripts\UI\MenuVisualConceptDecorApplier.cs:111:            RectTransform rect = slotObject.GetComponent<RectTransform>();
- Unity scene lookup | Assets\_Project\Scripts\UI\MenuVisualConceptDecorApplier.cs:112:            Image image = slotObject.GetComponent<Image>();
- Unity scene lookup | Assets\_Project\Scripts\UI\SettingsPanel.cs:604:            RectTransform rowRect = rowObject.GetComponent<RectTransform>();
- Unity scene lookup | Assets\_Project\Scripts\UI\SettingsPanel.cs:607:            LayoutElement rowLayout = rowObject.GetComponent<LayoutElement>();
- Unity scene lookup | Assets\_Project\Scripts\UI\SettingsPanel.cs:611:            HorizontalLayoutGroup rowGroup = rowObject.GetComponent<HorizontalLayoutGroup>();
- Unity scene lookup | Assets\_Project\Scripts\UI\SettingsPanel.cs:671:            RectTransform rowRect = rowObject.GetComponent<RectTransform>();
- Unity scene lookup | Assets\_Project\Scripts\UI\SettingsPanel.cs:674:            LayoutElement rowLayout = rowObject.GetComponent<LayoutElement>();
- Unity scene lookup | Assets\_Project\Scripts\UI\SettingsPanel.cs:678:            HorizontalLayoutGroup rowGroup = rowObject.GetComponent<HorizontalLayoutGroup>();
- Unity scene lookup | Assets\_Project\Scripts\UI\SettingsPanel.cs:738:            RectTransform rowRect = rowObject.GetComponent<RectTransform>();
- Unity scene lookup | Assets\_Project\Scripts\UI\SettingsPanel.cs:741:            LayoutElement rowLayout = rowObject.GetComponent<LayoutElement>();
- Unity scene lookup | Assets\_Project\Scripts\UI\SettingsPanel.cs:745:            HorizontalLayoutGroup rowGroup = rowObject.GetComponent<HorizontalLayoutGroup>();
- Unity scene lookup | Assets\_Project\Scripts\UI\SettingsPanel.cs:800:            RectTransform rowRect = rowObject.GetComponent<RectTransform>();
- Unity scene lookup | Assets\_Project\Scripts\UI\SettingsPanel.cs:803:            LayoutElement rowLayout = rowObject.GetComponent<LayoutElement>();
- Unity scene lookup | Assets\_Project\Scripts\UI\SettingsPanel.cs:807:            HorizontalLayoutGroup rowGroup = rowObject.GetComponent<HorizontalLayoutGroup>();
- Unity scene lookup | Assets\_Project\Scripts\UI\SettingsPanel.cs:863:            Slider slider = sliderObject.GetComponent<Slider>();
- Unity scene lookup | Assets\_Project\Scripts\UI\SettingsPanel.cs:880:            RectTransform fillAreaRect = fillAreaObject.GetComponent<RectTransform>();
- Unity scene lookup | Assets\_Project\Scripts\UI\SettingsPanel.cs:896:            RectTransform handleAreaRect = handleAreaObject.GetComponent<RectTransform>();
- Unity scene lookup | Assets\_Project\Scripts\UI\SettingsPanel.cs:922:            Image image = imageObject.GetComponent<Image>();
- Unity scene lookup | Assets\_Project\Scripts\UI\SettingsPanel.cs:941:            Image image = buttonObject.GetComponent<Image>();
- Unity scene lookup | Assets\_Project\Scripts\UI\SettingsPanel.cs:944:            Button button = buttonObject.GetComponent<Button>();
- Unity scene lookup | Assets\_Project\Scripts\UI\SettingsPanel.cs:969:            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
- Unity scene lookup | Assets\_Project\Scripts\UI\SettingsPanel.cs:1001:            LayoutElement layout = target.GetComponent<LayoutElement>();

## REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED (18)

- Native allocation or persistent lifetime | Assets\_Project\Scripts\UI\DiegeticTooltipSystem.cs:1572:                payload = new NativeArray<byte>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\UI\DiegeticVisorHudMesh.cs:802:                payload = new NativeArray<byte>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\UI\DiegeticGlitchSurgeonRuntime.cs:1301:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\UI\DiegeticGlitchSurgeonRuntime.cs:1331:            H8Memory.FreeRaw(buffer, Allocator.Persistent, SystemID.UI);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\UI\DiegeticGlitchSurgeonRuntime.cs:2430:                NativeArray<byte> payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\UI\PDAEncyclopediaStreamer.cs:3027:            NativeArray<byte> payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\UI\TopographicalSonar\TopographicalSonarSynthesizer.cs:542:                        Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\UI\TopographicalSonar\TopographicalSonarSynthesizer.cs:548:                        Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\UI\TopographicalSonar\TopographicalSonarSynthesizer.cs:554:                        Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\UI\TopographicalSonar\TopographicalSonarSynthesizer.cs:560:                        Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\UI\TopographicalSonar\TopographicalSonarSynthesizer.cs:566:                        Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\UI\TopographicalSonar\TopographicalSonarSynthesizer.cs:572:                        Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\UI\TopographicalSonar\TopographicalSonarSynthesizer.cs:2008:                payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\UI\VR\OpenXRManualOverrideLever.cs:685:                payload = new NativeArray<byte>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Visor\DynamicDecalVaultRuntime.cs:2339:                NativeArray<byte> payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Input\ControlRemapper.cs:356:                records = new NativeArray<InputActionStateDTO>(MaxBindingRecords, Allocator.Temp, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\LocRegistry.cs:650:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\LocRegistry.cs:1989:                Allocator.Persistent,

## LIKELY_LEGAL_COLD_OR_OWNER_LIFETIME_PATH (13)

- Native allocation or persistent lifetime | Assets\_Project\Scripts\UI\Navigation\DiegeticGyroCompassRuntime.cs:1796:                    payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\UI\PDADecryptionSpectrogramPanel.cs:899:                payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\UI\VehicleSubOsCockpitRuntime.cs:3008:            NativeArray<byte> dump = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Visor\HectonVisorARStencilRendererFeature.cs:1378:                payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Visor\HectonVisorFluidDistortionFeature.cs:1731:                payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Visor\HectonVisorUberPostFeature.Noir.cs:1076:                payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Visor\HectonVisorUberPostFeature.cs:1797:                payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Visor\HectonVolumetricParticulateFogFeature.cs:1963:                    payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Visor\InternalFloodWaterlineRuntime.cs:789:                payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Visor\SpectrumSystem.cs:3762:                payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Input\ControlRemapper.cs:132:                snapshot = new NativeArray<byte>(maxByteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Input\ControlRemapper.cs:220:                buffer = new NativeArray<byte>(MaxControlsJsonBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Input\ControlRemapper.cs:346:                fileBytes = new NativeArray<byte>(MaxControlsJsonBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

## LIKELY_LEGAL_COLD_LOOKUP (1)

- Unity scene lookup | Assets\_Project\Scripts\UI\DiegeticMenuCanvasUtility.cs:97:            root.GetComponentsInChildren(true, s_readableTextScratch); // COLD SCAN: main-menu setup only, never per-frame.

## LIKELY_LEGAL_COLD_PATH (1)

- Runtime mesh/material mutation | Assets\_Project\Scripts\UI\DiegeticVisorHudMesh.cs:438:                _runtimeMesh = new Mesh(); // COLD ALLOC: Mesh[1] - visor physical projection surface - owner: DiegeticVisorHudMesh

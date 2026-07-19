  4494	        private AtmosphericLightingState BuildSurfaceAtmosphericLightingState()
  4495	        {
  4496	            EvaluateCelestialAtmosphereProfileWeights(
  4497	                _currentSunAngle,
  4498	                out float dayWeight,
  4499	                out float sunsetWeight,
  4500	                out float nightWeight);
  4501
  4502	            Color skyHorizonColor = _resolvedSkyHorizon;
  4503	            skyHorizonColor.a = 1f;
  4504	            Color skyZenithColor = _resolvedSkyZenith;
  4505	            skyZenithColor.a = 1f;
  4506	            Color skyNadirColor = _resolvedSkyNadir;
  4507	            skyNadirColor.a = 1f;
  4508
  4509	            Color skyFogAnchor = Color.Lerp(skyHorizonColor, skyZenithColor, 0.18f);
  4510	            skyFogAnchor = Color.Lerp(skyFogAnchor, skyNadirColor, 0.08f);
  4511
  4512	            Color atmosphereFogColor = _atmosphereManager != null
  4513	                ? _atmosphereManager.CurrentFogColor
  4514	                : skyFogAnchor;
  4515	            if (!HasUsableSurfaceColor(atmosphereFogColor))
  4516	                atmosphereFogColor = skyFogAnchor;
  4517	            atmosphereFogColor.a = 1f;
  4518
  4519	            float horizonTransmittance = EvaluateAtmosphereTransmittance(
  4520	                0f,
  4521	                dayWeight,
  4522	                sunsetWeight,
  4523	                nightWeight);
  4524	            float horizonHaze = 1f - Mathf.Clamp01(horizonTransmittance);
  4525	            float lowSunFactor = 1f - Mathf.Clamp01((_currentSunAngle + 8f) * Inv88);
  4526	            float hazeResponse = Mathf.Clamp01(horizonHaze * 0.72f + lowSunFactor * 0.28f);
  4527	            Color ambientBaseColor = ResolveSurfaceAmbientBaseColor();
  4528	            Color horizonSkyTint = Color.Lerp(skyHorizonColor, skyZenithColor, 0.14f);
  4529	            horizonSkyTint = Color.Lerp(horizonSkyTint, skyNadirColor, 0.05f);
  4530	            if (_celestialAtmosphereLutSamples.Length > 0)
  4531	            {
  4532	                horizonSkyTint = Color.Lerp(horizonSkyTint, _celestialAtmosphereLutSamples[0], 0.14f);
  4533	                horizonTransmittance = Mathf.Clamp01(_celestialAtmosphereLutSamples[0].a);
  4534	                horizonHaze = 1f - Mathf.Clamp01(horizonTransmittance);
  4535	                hazeResponse = Mathf.Clamp01(horizonHaze * 0.72f + lowSunFactor * 0.28f);
  4536	            }
  4537	            horizonSkyTint.a = 1f;
  4538
  4539	            float baseFogDensity = ResolveSurfaceBaseFogDensity();
  4540	            float dayVisibility = Mathf.Clamp01((_currentSunAngle + 2f) * Inv64);
  4541	            float middayFogReduction = Mathf.Lerp(1.08f, 0.82f, dayVisibility);
  4542	            float fogDensity = Mathf.Max(
  4543	                0.0001f,
  4544	                baseFogDensity *
  4545	                Mathf.Lerp(0.82f, 1.28f, hazeResponse) *
  4546	                middayFogReduction *
  4547	                Mathf.Max(0.25f, _surfaceFogDensityMultiplier));
  4548
  4549	            Color fogOwnerColor = _surfaceWeatherFogOverrideActive
  4550	                ? _surfaceWeatherFogColor
  4551	                : Color.Lerp(
  4552	                    atmosphereFogColor,
  4553	                    _surfaceFogManualColor,
  4554	                    Mathf.Clamp01(_surfaceFogManualColorBlend));
  4555	            fogOwnerColor.a = 1f;
  4556
  4557	            float skyTintWeight =
  4558	                Mathf.Lerp(0.06f, 0.18f, hazeResponse) * Mathf.Clamp01(_surfaceFogSkyColorInfluence) +
  4559	                Mathf.Lerp(0.02f, 0.18f, hazeResponse) * _surfaceHazeSkyTintInfluence;
  4560	            skyTintWeight = Mathf.Lerp(skyTintWeight, skyTintWeight * 1.22f, sunsetWeight);
  4561	            skyTintWeight = Mathf.Lerp(skyTintWeight, skyTintWeight * 0.82f, nightWeight);
  4562	            skyTintWeight = Mathf.Clamp01(skyTintWeight);
  4563
  4564	            float ambientWeight = Mathf.Lerp(0.08f, 0.24f, 1f - dayVisibility) *
  4565	                                  Mathf.Clamp01(_surfaceFogAmbientColorInfluence);
  4566
  4567	            Color horizonFogColor = Color.Lerp(fogOwnerColor, horizonSkyTint, skyTintWeight);
  4568	            horizonFogColor = Color.Lerp(horizonFogColor, ambientBaseColor, ambientWeight);
  4569	            float atmosphereRestoreWeight =
  4570	                (1f - Mathf.Clamp01(_surfaceFogManualColorBlend)) *
  4571	                Mathf.Lerp(0.18f, 0.36f, hazeResponse);
  4572	            horizonFogColor = Color.Lerp(horizonFogColor, atmosphereFogColor, atmosphereRestoreWeight);
  4573	            float fogTargetLuminance = Mathf.Max(
  4574	                ComputePerceivedLuminance(fogOwnerColor) * Mathf.Lerp(1f, 0.88f, hazeResponse),
  4575	                ComputePerceivedLuminance(horizonSkyTint),
  4576	                ComputePerceivedLuminance(skyZenithColor) * Mathf.Lerp(0.42f, 0.58f, dayVisibility));
  4577	            horizonFogColor = LiftColorTowardsLuminance(
  4578	                horizonFogColor,
  4579	                fogTargetLuminance,
  4580	                Mathf.Lerp(0.22f, 0.38f, dayVisibility));
  4581	            horizonFogColor = DesaturateColor(
  4582	                horizonFogColor,
  4583	                Mathf.Lerp(0.14f, 0.22f, dayWeight) + hazeResponse * 0.04f);
  4584	            horizonFogColor.a = 1f;
  4585
  4586	            float hazeSpread = Mathf.Max(0.5f, _surfaceHazeHorizonSpread);
  4587	            float hazeIntensity = Mathf.Lerp(0.12f, 0.34f, hazeResponse) *
  4588	                                  Mathf.Max(0.25f, _surfaceSkyHazeIntensityMultiplier);
  4589	            hazeIntensity *= Mathf.Lerp(1f, 1f + (hazeSpread - 1f) * 0.35f, hazeResponse);
  4590	            hazeIntensity = Mathf.Lerp(hazeIntensity, hazeIntensity * 1.18f, sunsetWeight);
  4591	            hazeIntensity = Mathf.Lerp(hazeIntensity, hazeIntensity * 0.42f, nightWeight);
  4592
  4593	            float hazeFalloff = Mathf.Lerp(6.1f, 3.8f, hazeResponse) * math.rcp(hazeSpread);
  4594	            hazeFalloff = Mathf.Lerp(hazeFalloff, hazeFalloff * 0.9f, sunsetWeight);

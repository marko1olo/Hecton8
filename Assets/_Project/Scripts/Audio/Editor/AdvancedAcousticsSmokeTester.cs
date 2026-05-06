#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Audio.Editor
{
    /// <summary>
    /// Editor-only source smoke test for advanced acoustic propagation and DSP producer features.
    /// </summary>
    public static class AdvancedAcousticsSmokeTester
    {
        private const string RendererPath = "Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs";
        private const string SpatialAudioPath = "Assets/_Project/Scripts/SpatialAudioManager.cs";
        private const string PhysicsApplyPath = "Assets/_Project/Scripts/PhysicsApplySystem.cs";
        private const string SpectrumSystemPath = "Assets/_Project/Scripts/Visor/SpectrumSystem.cs";
        private const string ResourceNodePath = "Assets/_Project/Scripts/ResourceNode.cs";
        private const string ResourceNodeTemplatePath = "Assets/_Project/Scripts/Scavenging/ResourceNodeTemplate.cs";
        private const string ToolHapticsPath = "Assets/_Project/Scripts/Tools/ToolHapticsRuntime.cs";
        private const string FakeRadarPath = "Assets/_Project/Scripts/UI/FakeRadarBlipController.cs";
        private const string EcholocationTranslatorPath = "Assets/_Project/Scripts/UI/AcousticEcholocationTranslator.cs";
        private const string GlobalRegistryPath = "Assets/_Project/Scripts/Core/GlobalRegistry.cs";
        private const string GlobalRegistryContractsPath = "Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs";
        private const string OcclusionPath = "Assets/_Project/Scripts/World/AcousticOcclusionUtility.cs";
        private const string RingBufferPath = "Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs";
        private const string TelemetryPath = "Assets/_Project/Scripts/CrashTelemetryBuffer.cs";
        private const string EventsPath = "Assets/_Project/Scripts/Audio/ProceduralAudioEvents.cs";
        private const string PlayerPdaPath = "Assets/_Project/Scripts/PlayerPDA.cs";
        private const string PlayerStressVfxPath = "Assets/_Project/Scripts/Visor/PlayerStressVFX.cs";

        [MenuItem("Hecton8/Audio/Run Advanced Acoustics Smoke Test")]
        public static void RunMenuItem()
        {
            bool passed = Run(out string report);
            if (passed)
                Debug.Log(report);
            else
                Debug.LogError(report);
        }

        public static bool Run(out string report)
        {
            int failureCount = 0;
            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine("[AdvancedAcousticsSmokeTester]");

            string renderer = ReadAssetText(RendererPath, builder, ref failureCount);
            string spatial = ReadAssetText(SpatialAudioPath, builder, ref failureCount);
            string physicsApply = ReadAssetText(PhysicsApplyPath, builder, ref failureCount);
            string spectrumSystem = ReadAssetText(SpectrumSystemPath, builder, ref failureCount);
            string resourceNode = ReadAssetText(ResourceNodePath, builder, ref failureCount);
            string resourceNodeTemplate = ReadAssetText(ResourceNodeTemplatePath, builder, ref failureCount);
            string toolHaptics = ReadAssetText(ToolHapticsPath, builder, ref failureCount);
            string fakeRadar = ReadAssetText(FakeRadarPath, builder, ref failureCount);
            string echolocationTranslator = ReadAssetText(EcholocationTranslatorPath, builder, ref failureCount);
            string globalRegistry = ReadAssetText(GlobalRegistryPath, builder, ref failureCount);
            string globalRegistryContracts = ReadAssetText(GlobalRegistryContractsPath, builder, ref failureCount);
            string occlusion = ReadAssetText(OcclusionPath, builder, ref failureCount);
            string ringBuffer = ReadAssetText(RingBufferPath, builder, ref failureCount);
            string telemetry = ReadAssetText(TelemetryPath, builder, ref failureCount);
            string eventsSource = ReadAssetText(EventsPath, builder, ref failureCount);
            string playerPda = ReadAssetText(PlayerPdaPath, builder, ref failureCount);
            string playerStressVfx = ReadAssetText(PlayerStressVfxPath, builder, ref failureCount);

            if (spatial.Length > 0)
            {
                AssertContains(spatial, "TryTraceVoxelDensityOcclusion", "Delayed world events apply voxel density occlusion", builder, ref failureCount);
                AssertContains(spatial, "PlayAtPointWithLowPass", "Delayed events route resolved low-pass cutoff into source filter", builder, ref failureCount);
                AssertContains(spatial, "ThermalShimmerMaximumPitchRatio", "Thermal plume shimmer pitch modulation exists", builder, ref failureCount);
                AssertContains(spatial, "RefreshListenerCaveState", "Listener cave state refresh exists", builder, ref failureCount);
                AssertContains(spatial, "HectonVoxelVolume", "Cave state uses authored voxel-volume records", builder, ref failureCount);
                AssertContains(spatial, "localBounds.Contains", "Cave interior check is a local AABB contains test", builder, ref failureCount);
                AssertContains(spatial, "IsListenerInsideCaveVolume", "Spatial manager exposes listener cave membership for fake reverb", builder, ref failureCount);
                AssertContains(spatial, "IPhysicsAcousticImpulseEventListener", "Spatial manager receives acoustic impulses", builder, ref failureCount);
                AssertContains(spatial, "PhysicsEventBus.Register(this)", "Spatial manager subscribes to sensory bus", builder, ref failureCount);
                AssertContains(spatial, "math.dot((float3)listener.right, sourceDirection)", "Binaural ITD uses one ear-axis dot product", builder, ref failureCount);
                AssertContains(spatial, "TryQueueImpactRadarEmitter(impulseEvent.RuntimePosition", "Acoustic impulses feed passive HUD emitters", builder, ref failureCount);
                AssertContains(spatial, "ResolveAupDelta", "Long-range spatial audio direction uses AUP delta helpers", builder, ref failureCount);
                AssertContains(spatial, "AbsoluteUniversePosition.DistanceSq(in listenerAup, in sourceAup)", "Spatial audio distance uses int64-sector AUP distance math", builder, ref failureCount);
                AssertContains(spatial, "AbsoluteUniversePosition.ToCameraRelativeFloat3(in sourceAup, in listenerAup)", "Doppler/radar direction uses AUP camera-relative math", builder, ref failureCount);
            }

            if (occlusion.Length > 0)
            {
                AssertContains(occlusion, "internal struct AcousticVoxelOcclusionResult", "Voxel occlusion payload exists", builder, ref failureCount);
                AssertContains(occlusion, "TryTraceVoxelDensityOcclusion", "Voxel density trace API exists", builder, ref failureCount);
                AssertContains(occlusion, "OpenLowPassCutoffHertz / (1f +", "Accumulated density drives heavy low-pass cutoff", builder, ref failureCount);
                AssertContains(occlusion, "VoxelDensityHardLowPassCutoffHertz = 300f", "Solid SDF occlusion applies 300 Hz LPF floor", builder, ref failureCount);
            }

            if (renderer.Length > 0)
            {
                string onAudioFilterRead = ExtractMethodBody(renderer, "private void OnAudioFilterRead(float[] data, int channels)");
                string updateCaveReverb = ExtractMethodBody(renderer, "private void UpdateCaveReverb(float deltaTime)");
                string handleSonarPingSent = ExtractMethodBody(renderer, "private void HandleSonarPingSent(float intensity)");
                string renderBubbleBlock = ExtractMethodBody(renderer, "private void RenderBubbleBlock(");
                string renderTinnitusSample = ExtractMethodBody(renderer, "private static float RenderTinnitusSample(");
                string renderHullStressBlock = ExtractMethodBody(renderer, "private void RenderHullStressBlock(");
                string renderSonarBlock = ExtractMethodBody(renderer, "private void RenderSonarBlock(int frameCount, long blockStartFrame, double invSampleRate)");
                AssertContains(renderer, "RenderLeviathanGranularRoarSample", "Leviathan granular synthesis kernel exists", builder, ref failureCount);
                AssertContains(renderer, "NativeArray<float> baseRoarClip", "Granular kernel consumes native base roar data", builder, ref failureCount);
                AssertContains(renderer, "LeviathanRoarAggro", "Aggro is synchronized through audio parameter snapshot", builder, ref failureCount);
                AssertContains(renderer, "LeviathanRoarPitchScale", "Leviathan roar pitch is driven by Doppler snapshot state", builder, ref failureCount);
                AssertContains(renderer, "ResolveLeviathanDopplerPitchScale", "Leviathan Doppler pitch resolver exists", builder, ref failureCount);
                AssertContains(renderer, "AbsoluteUniversePosition.ToCameraRelativeFloat3(predatorAup, playerAup)", "Doppler distance delta uses AUP camera-relative math", builder, ref failureCount);
                AssertContains(renderer, "RenderInteriorFdnReverbSample", "Dry interior FDN reverb exists", builder, ref failureCount);
                AssertContains(renderer, "AbyssalLowPassCutoffHertz = 380f", "Abyssal LPF reaches 380 Hz at full depth", builder, ref failureCount);
                AssertContains(renderer, "AbyssalLowPassFadeDepthMeters = 4500f", "5000 m depth maps to full abyssal LPF after 500 m start", builder, ref failureCount);
                AssertContains(renderer, "TinnitusCarrierHertz = 8000f", "O2 deprivation tinnitus carrier is 8000 Hz", builder, ref failureCount);
                AssertContains(renderer, "TinnitusLowPassCutoffHertz", "O2 deprivation lowers master LPF cutoff", builder, ref failureCount);
                AssertContains(renderTinnitusSample, "1f - math.exp(-TinnitusPlayerStressExponentialSharpness * playerStress)", "O2 deprivation tinnitus scales exponentially with player stress", builder, ref failureCount);
                AssertContains(renderer, "PanicHeartbeatStressThreshold01 = 0.8f", "Panic heartbeat engages above 80 percent stress", builder, ref failureCount);
                AssertContains(renderer, "PanicHeartbeatAmbientHighCutMinimumGain = 0.38f", "Panic heartbeat dulls high-frequency ambient bed", builder, ref failureCount);
                AssertContains(updateCaveReverb, "targetWetMix = insideCaveVolume ? FakeCaveReverbMix01 : FakeOpenWaterReverbMix01", "Cave reverb uses fixed 0.8/0.2 fake volume mix", builder, ref failureCount);
                AssertNotContains(updateCaveReverb, "TryGetCachedEnclosureSample", "Critical cave reverb does not use enclosure ray fallback", builder, ref failureCount);
                AssertContains(renderer, "SonarGhostEchoTapCount = 3", "Sonar ghost echo is a three-tap synthetic echo", builder, ref failureCount);
                AssertNotContains(handleSonarPingSent, "Raycast", "Sonar ghost echo trigger has no raycast", builder, ref failureCount);
                AssertContains(renderSonarBlock, "tap.LeftPanDeltaGain", "Sonar ghost echoes use hash-derived stereo panning deltas", builder, ref failureCount);
                AssertContains(renderer, "HullGroanLoopPitchMinimum = 0.8f", "Hull authored loop pitch minimum is 0.8", builder, ref failureCount);
                AssertContains(renderer, "HullGroanLoopPitchMaximum = 1.2f", "Hull authored loop pitch maximum is 1.2", builder, ref failureCount);
                AssertNotContains(renderHullStressBlock, "CarrierAPhase", "Hull DSP block has no FM carrier chain", builder, ref failureCount);
                AssertContains(renderBubbleBlock, "ToolCavitationMaximumGain", "Tool overheat cavitation writes high-frequency bursts into DSP scratch", builder, ref failureCount);
                AssertContains(renderBubbleBlock, "HashSigned(sampleIndex ^ 0x7E5A3C91u)", "Tool cavitation noise is deterministic hash noise", builder, ref failureCount);
                AssertContains(renderer, "VehicleCavitationScreechStartMetersPerSecond = 20f", "Vehicle cavitation screech gates at 20 m/s", builder, ref failureCount);
                AssertContains(renderer, "VehicleCavitationHighPassAlpha", "Vehicle cavitation uses high-pass hash noise", builder, ref failureCount);
                AssertNotContains(renderer, "ResolveMinnaertFrequency", "Minnaert bubble formula is absent from critical renderer", builder, ref failureCount);
                AssertNotContains(renderer, "UnityEngine.Random", "Critical renderer has no UnityEngine.Random call", builder, ref failureCount);
                AssertContains(renderer, "PhysicsImpactMinimumAudibleMassVelocity = 5f", "Impact thuds gate at 5 m/s mass velocity", builder, ref failureCount);
                AssertContains(renderer, "ResolveImpactMaterialBlend", "Impact synthesis blends both AudioMaterialID values", builder, ref failureCount);
                AssertContains(renderer, "ResolveSonarMaterialPitchScale", "Sonar echo pitch uses AudioMaterialID", builder, ref failureCount);
                AssertContains(renderer, "ResolveSonarMaterialDecayMultiplier", "Sonar echo decay uses AudioMaterialID", builder, ref failureCount);
                AssertContains(renderer, "RenderPressureScrubberHumSample", "Pressure scrubber hum harmonic saturation exists", builder, ref failureCount);
                AssertContains(renderer, "math.tanh((fundamental + second + third)", "Pressure hum distortion uses tanh saturation", builder, ref failureCount);
                AssertContains(renderer, "case ItemAudioMaterialId.Metal:", "Metal impacts route to clang multiplier", builder, ref failureCount);
                AssertContains(renderer, "return 1.1f;", "Metal impact clang multiplier is boosted", builder, ref failureCount);
                AssertContains(renderer, "return 0.4f;", "Rock/default impact clang multiplier remains dull", builder, ref failureCount);
                AssertNotContains(onAudioFilterRead, "RenderLeviathanGranularRoarSample", "Leviathan synth is not in OnAudioFilterRead", builder, ref failureCount);
                AssertNotContains(onAudioFilterRead, "TryTraceVoxelDensityOcclusion", "Voxel trace is not in OnAudioFilterRead", builder, ref failureCount);
                AssertNotContains(onAudioFilterRead, "new ", "OnAudioFilterRead has no explicit allocation", builder, ref failureCount);
            }

            if (physicsApply.Length > 0)
            {
                AssertContains(physicsApply, "public readonly struct AcousticImpulseEvent", "Acoustic impulse event payload exists", builder, ref failureCount);
                AssertContains(physicsApply, "ForcePacketPriority.Critical", "Critical force packets are checked for acoustic routing", builder, ref failureCount);
                AssertContains(physicsApply, "ResolveKineticEnergyJoules", "Physics impulse energy resolver exists", builder, ref failureCount);
                AssertContains(physicsApply, "0.5f * math.max(0.0001f, massKg) * math.lengthsq(velocity)", "Kinetic energy uses 0.5*m*v^2", builder, ref failureCount);
                AssertContains(physicsApply, "ResolveAcousticImpulseVolume01", "Kinetic energy maps to audio volume", builder, ref failureCount);
                AssertContains(physicsApply, "PhysicsEventBus.NotifyAcousticImpulse", "Critical force packets publish acoustic impulses", builder, ref failureCount);
                AssertContains(physicsApply, "ProxyLightRegistry.RegisterOrUpdate", "Critical collisions spawn transient proxy light sparks", builder, ref failureCount);
                AssertContains(physicsApply, "return Instance;", "Physics apply runtime resolves through GlobalRegistry instead of self-spawn", builder, ref failureCount);
                AssertNotContains(physicsApply, "new GameObject(\"[PhysicsApplySystem]\")", "Physics apply runtime does not self-spawn", builder, ref failureCount);
            }

            if (spectrumSystem.Length > 0)
                AssertContains(spectrumSystem, "public byte AudioMaterialId", "Sonar echo events carry AudioMaterialID", builder, ref failureCount);

            if (resourceNode.Length > 0)
            {
                AssertContains(resourceNode, "resourceTemplate.AudioMaterialID", "Resource sonar echoes publish template AudioMaterialID", builder, ref failureCount);
                AssertContains(resourceNode, "AbsoluteUniversePosition.DistanceSq(in nodeAup, in playerAup)", "Resource sonar echo range uses AUP distance math", builder, ref failureCount);
            }

            if (resourceNodeTemplate.Length > 0)
                AssertContains(resourceNodeTemplate, "public byte AudioMaterialID", "Resource templates expose sonar AudioMaterialID", builder, ref failureCount);

            if (toolHaptics.Length > 0)
            {
                string hapticsTick = ExtractMethodBody(toolHaptics, "public void Tick(float deltaTime)");
                AssertContains(toolHaptics, "IPhysicsAcousticImpulseEventListener", "Tool haptics receive acoustic impulses", builder, ref failureCount);
                AssertContains(toolHaptics, "LeftMotorMask", "Left-side collision haptics route to left motor", builder, ref failureCount);
                AssertContains(toolHaptics, "GlobalRegistry.ToolHaptics", "Tool haptics resolve through GlobalRegistry", builder, ref failureCount);
                AssertContains(toolHaptics, "ResolveHapticDecayFactor", "Tool haptics use Padé decay approximation", builder, ref failureCount);
                AssertNotContains(toolHaptics, "_instance", "Tool haptics has no classic singleton field", builder, ref failureCount);
                AssertNotContains(toolHaptics, "new GameObject(\"[ToolHapticsRuntime]\")", "Tool haptics does not self-spawn", builder, ref failureCount);
                AssertNotContains(hapticsTick, "math.exp", "Tool haptics Tick avoids libm exp", builder, ref failureCount);
            }

            if (globalRegistry.Length > 0 && globalRegistryContracts.Length > 0)
            {
                AssertContains(globalRegistry, "public static ToolHapticsRuntime ToolHaptics", "GlobalRegistry exposes authoritative ToolHaptics slot", builder, ref failureCount);
                AssertContains(globalRegistry, "RegisterToolHapticsRuntime", "GlobalRegistry registers tool haptics runtime", builder, ref failureCount);
                AssertContains(globalRegistryContracts, "ToolHapticsRuntime = 118", "GlobalRegistry service enum includes ToolHaptics slot", builder, ref failureCount);
            }

            if (fakeRadar.Length > 0)
            {
                AssertContains(fakeRadar, "ThermalNoiseStartDepthMeters = 4000f", "Pressure ghost blips begin below 4000 m", builder, ref failureCount);
                AssertContains(fakeRadar, "HashThermalNoiseGhost", "Pressure ghost blips use deterministic hash noise", builder, ref failureCount);
            }

            if (echolocationTranslator.Length > 0)
            {
                AssertContains(echolocationTranslator, "IPhysicsAcousticImpulseEventListener", "Echolocation HUD receives acoustic impulses", builder, ref failureCount);
                AssertContains(echolocationTranslator, "DefaultVisualSoundWaveText", "Leviathan acoustic impulses render visual sound wave text", builder, ref failureCount);
                AssertContains(echolocationTranslator, "CurrentFogAttenuationDistance <= HeavyFogAttenuationDistanceMeters", "Visual sound waves require blindness or heavy fog", builder, ref failureCount);
            }

            if (playerPda.Length > 0)
            {
                string playSound = ExtractMethodBody(playerPda, "private void PlaySound(AudioClip clip, float volume, float pitch)");
                AssertContains(playSound, "audioManager.PlayAtPoint(clip, ResolvePdaAudioPosition(), volume, pitch, audioManager.InterfaceGroup)", "PDA clicks route through SpatialAudioManager at the PDA hand AUP", builder, ref failureCount);
                AssertNotContains(playSound, "PlayStatic2D", "PDA click helper does not route through 2D UI audio", builder, ref failureCount);
            }

            if (playerStressVfx.Length > 0)
            {
                AssertContains(playerStressVfx, "PlayHeartbeat(audioStress01)", "Heartbeat audio is driven from stress VFX update", builder, ref failureCount);
                AssertContains(playerStressVfx, "ApplyStressPulse(stress01, beat01, fog01, frost01)", "Heartbeat pulse is synchronized with visual UI distortion", builder, ref failureCount);
            }

            if (eventsSource.Length > 0)
                AssertContains(eventsSource, "LeviathanRoar", "Procedural audio event kind routes Leviathan roar", builder, ref failureCount);

            if (ringBuffer.Length > 0)
            {
                AssertContains(ringBuffer, "CrashTelemetryBuffer.ReportAudioOverflowDropWarning", "SPSC overflow drop emits crash telemetry", builder, ref failureCount);
                AssertContains(ringBuffer, "_lastTelemetryOverflowDropCount", "SPSC overflow telemetry is rate-gated", builder, ref failureCount);
            }

            if (telemetry.Length > 0)
            {
                AssertContains(telemetry, "AudioOverflowDropWarning", "Crash telemetry stores audio overflow fault bit", builder, ref failureCount);
                AssertContains(telemetry, "WriteAudioOverflowDropTelemetry", "Crash telemetry writes audio overflow ring entry", builder, ref failureCount);
                AssertContains(telemetry, "SystemBits.Audio", "Crash telemetry tags audio subsystem rows", builder, ref failureCount);
            }

            builder.Append("STATUS: ");
            builder.AppendLine(failureCount == 0 ? "PASS" : "FAIL");
            report = builder.ToString();
            return failureCount == 0;
        }

        private static string ReadAssetText(string assetPath, StringBuilder builder, ref int failureCount)
        {
            string root = Directory.GetParent(Application.dataPath)?.FullName;
            string absolutePath = root == null
                ? assetPath
                : Path.Combine(root, assetPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absolutePath))
            {
                AppendFailure(builder, ref failureCount, "Missing asset: " + assetPath);
                return string.Empty;
            }

            return File.ReadAllText(absolutePath);
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            if (signatureIndex < 0)
                return string.Empty;

            int braceStart = source.IndexOf('{', signatureIndex);
            if (braceStart < 0)
                return string.Empty;

            int depth = 0;
            for (int i = braceStart; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                    depth++;
                else if (c == '}')
                    depth--;

                if (depth == 0)
                    return source.Substring(braceStart, i - braceStart + 1);
            }

            return string.Empty;
        }

        private static void AssertContains(string source, string needle, string message, StringBuilder builder, ref int failureCount)
        {
            if (source.IndexOf(needle, StringComparison.Ordinal) >= 0)
            {
                builder.Append("[PASS] ").AppendLine(message);
                return;
            }

            AppendFailure(builder, ref failureCount, message + " :: missing `" + needle + "`");
        }

        private static void AssertNotContains(string source, string needle, string message, StringBuilder builder, ref int failureCount)
        {
            if (source.IndexOf(needle, StringComparison.Ordinal) < 0)
            {
                builder.Append("[PASS] ").AppendLine(message);
                return;
            }

            AppendFailure(builder, ref failureCount, message + " :: found forbidden `" + needle + "`");
        }

        private static void AppendFailure(StringBuilder builder, ref int failureCount, string message)
        {
            failureCount++;
            builder.Append("[FAIL] ").AppendLine(message);
        }
    }
}
#endif

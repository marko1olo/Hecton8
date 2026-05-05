// ============================================================================
// HECTON-8 - CelestialCataclysmSmokeTester.cs
// Dev-only smoke coverage for astrodynamics cataclysm contracts.
// ============================================================================

using System;
using System.IO;
using Hecton8.Caves;
using Hecton8.Celestial;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Items;
using Hecton8.Physics;
using Hecton8.World;
using NASAPunk.Visor;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Dev
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Celestial Cataclysm Smoke Tester")]
    public sealed class CelestialCataclysmSmokeTester : MonoBehaviour
    {
        private const string TitaniumScrapAssetPath = "Assets/_Project/Data/Items/Resources/Raw/Data_TitaniumScrap.asset";
        private const string VolumetricComputePath = "Assets/_Project/Art/Shaders/Hecton_VolumetricLight.compute";
        private const string CataclysmSystemPath = "Assets/_Project/Scripts/Gameplay/CelestialCataclysmSystem.cs";
        private const string EclipseGameplaySystemPath = "Assets/_Project/Scripts/Gameplay/EclipseGameplaySystem.cs";
        private const string RandomEventSystemPath = "Assets/_Project/Scripts/Gameplay/RandomEventSystem.cs";
        private const string RandomEventMeteorMathPath = "Assets/_Project/Scripts/Gameplay/RandomEventMeteorMath.cs";
        private const string CelestialSyncSmokeTesterPath = "Assets/_Project/Scripts/Dev/CelestialSyncSmokeTester.cs";
        private const string CelestialEnginePath = "Assets/_Project/Scripts/HectonCelestialEngine.cs";
        private const string AtmosphereManagerPath = "Assets/_Project/Scripts/HectonAtmosphereManager.cs";
        private const string FirmamentComputePath = "Assets/_Project/Art/Shaders/HectonFirmamentBake.compute";
        private const string AlienSkyShaderPath = "Assets/_Project/Art/Shaders/Hecton_AlienSky_Master.shader";
        private const string CelestialAtmosphereIncludePath = "Assets/_Project/Art/Shaders/Hecton_CelestialAtmosphere.hlsl";
        private const string CoreLitIncludePath = "Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl";
        private const string AlienSkyShaderName = "HECTON/Sky/Hecton_AlienSky_Master";
        private const string LegacyFluidInstanceToken = "HectonFluidEngine" + ".Instance";
        private const string LegacyEclipseInstanceToken = "EclipseGameplaySystem" + ".Instance";
        private const string LegacyEclipsePublicInstanceToken = "public " + "static EclipseGameplaySystem " + "Instance";
        private const string LegacyRandomEventInstanceToken = "RandomEventSystem" + ".Instance";
        private const string LegacyRandomEventPublicInstanceToken = "public " + "static RandomEventSystem " + "Instance";
        private const string JobCompleteToken = ".Complete" + "(";
        private const string JobRunToken = ".Run" + "(";
        private const string StringFormatToken = "string" + ".Format";
        private const string ToStringToken = "ToString" + "(";
        private const char InterpolationDollar = (char)36;
        private const char QuoteChar = (char)34;
        private const string BatchJsonStatusPrefix = "{\"tester\":\"CelestialCataclysmSmokeTester\",\"status\":\"";
        private const string BatchJsonIssueSegment = "\",\"issue\":\"";
        private const string BatchJsonSourceChecksSegment = "\",\"sourceChecks\":";
        private const string BatchJsonTelemetryContractsSegment = ",\"telemetryContracts\":";
        private const string BatchJsonSuffix = "}";

        [Header("Execution")]
        [SerializeField] private bool runOnStart = false;
        [SerializeField] private bool verboseLogging = false;

#pragma warning disable CS0414
        [Header("Debug")]
        [SerializeField] private int _debugRunCount;
        [SerializeField] private bool _debugLastPass;
        [SerializeField] private string _debugLastIssue = string.Empty;
        [SerializeField] private float _debugAtmosphereDensity;
        [SerializeField] private float _debugLunarMultiplier;
        [SerializeField] private int _debugSolarFlareIndex;
        [SerializeField] private int _debugMeteorFogShadowParamsId;
        [SerializeField] private int _debugOmegaSourceChecks;
        [SerializeField] private int _debugOmegaTelemetryContracts;
#pragma warning restore CS0414

        private static readonly int _AtmosphereDensityId = Shader.PropertyToID("_AtmosphereDensity");
        private static readonly int _FinalGiantAbyssLightId = Shader.PropertyToID("_FinalGiantAbyssLight");
        private static readonly int _MeteorFogShadowPositionsId = Shader.PropertyToID("_MeteorFogShadowPositions");
        private static readonly int _MeteorFogShadowParamsId = Shader.PropertyToID("_MeteorFogShadowParams");
        private static readonly int _SolarEmpGlitchParamsId = Shader.PropertyToID("_SolarEmpGlitchParams");
        private static readonly int _BakedStarCubemapReadyId = Shader.PropertyToID("_BakedStarCubemapReady");
        private static readonly int _HectonAtmosphereScatteringLutReadyId = Shader.PropertyToID("_HectonAtmosphereScatteringLUTReady");
        private static readonly int _HectonEclipseWaterShadowParamsId = Shader.PropertyToID("_HectonEclipseWaterShadowParams");
        private static readonly int _HectonRingCausticsParamsId = Shader.PropertyToID("_HectonRingCausticsParams");

#if UNITY_EDITOR
        public static void RunBatchModeSmokeTest()
        {
            GameObject smokeRoot = new GameObject("CelestialCataclysmSmokeTester_Batch")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            CelestialCataclysmSmokeTester tester = smokeRoot.AddComponent<CelestialCataclysmSmokeTester>();
            bool pass = tester.RunSmokePass();
            string issue = SanitizeJsonField(tester._debugLastIssue);
            Debug.Log(BuildBatchJson(pass, issue, tester._debugOmegaSourceChecks, tester._debugOmegaTelemetryContracts));
            DestroyImmediate(smokeRoot);

            if (Application.isBatchMode)
                EditorApplication.Exit(pass ? 0 : 1);
        }
#endif

        private void Start()
        {
            if (runOnStart)
                RunSmokePass();
        }

        [ContextMenu("Run Celestial Cataclysm Smoke Pass")]
        public void RunFromContextMenu()
        {
            RunSmokePass();
        }

        public bool RunSmokePass()
        {
            _debugRunCount++;
            _debugLastPass = false;
            _debugLastIssue = string.Empty;
            _debugOmegaSourceChecks = 0;
            _debugOmegaTelemetryContracts = 0;

            if (!ValidateRandomEventContracts())
                return false;

            if (!ValidateTypeContracts())
                return false;

            if (!ValidateShaderContracts())
                return false;

            if (!ValidateFirmamentContracts())
                return false;

            if (!ValidateOmegaContracts())
                return false;

            if (!ValidateRuntimeCelestialValues())
                return false;

            if (!ValidateTitaniumScrapContract())
                return false;

            _debugLastPass = true;
            _debugLastIssue = string.Empty;
            LogVerbose("[CelestialCataclysmSmoke] COMPLETE pass=True");
            return true;
        }

        private bool ValidateRandomEventContracts()
        {
            _debugSolarFlareIndex = (int)RandomEventType.SolarFlare;
            if (_debugSolarFlareIndex <= (int)RandomEventType.MeteorShower)
                return Fail("SolarFlare must remain after MeteorShower in RandomEventType.");

            if (RandomEventSystem.EventTypeCount <= _debugSolarFlareIndex)
                return Fail("RandomEventSystem.EventTypeCount excludes SolarFlare.");

            return true;
        }

        private bool ValidateTypeContracts()
        {
            if (!typeof(IRandomEventListener).IsAssignableFrom(typeof(CelestialCataclysmSystem)))
                return Fail("CelestialCataclysmSystem is not wired as an IRandomEventListener.");

            if (!typeof(ISlowTickable).IsAssignableFrom(typeof(CelestialCataclysmSystem)))
                return Fail("CelestialCataclysmSystem is not wired to slow tick.");

            if (!typeof(IElectromagneticPulseEventListener).IsAssignableFrom(typeof(BaseModule)))
                return Fail("BaseModule does not receive EMP events.");

            if (typeof(HectonVoxelVolume).GetMethod(nameof(HectonVoxelVolume.TryApplyExtraterrestrialImpactCrater)) == null)
                return Fail("HectonVoxelVolume lacks the meteor impact crater entry point.");

            if (typeof(GlobalRegistry).GetProperty(nameof(GlobalRegistry.Fluid)) == null)
                return Fail("GlobalRegistry.Fluid runtime slot missing.");

            if (typeof(GlobalRegistry).GetProperty(nameof(GlobalRegistry.EclipseGameplay)) == null)
                return Fail("GlobalRegistry.EclipseGameplay runtime slot missing.");

            if (typeof(GlobalRegistry).GetProperty(nameof(GlobalRegistry.RandomEvents)) == null)
                return Fail("GlobalRegistry.RandomEvents runtime slot missing.");

            if (typeof(GlobalTelemetryBus).GetMethod(
                    nameof(GlobalTelemetryBus.PublishPerformanceWarning),
                    new[] { typeof(uint), typeof(uint), typeof(float) }) == null)
            {
                return Fail("GlobalTelemetryBus.PublishPerformanceWarning(uint,uint,float) missing.");
            }

            _debugOmegaTelemetryContracts++;

            if (typeof(VisorHUDController).GetMethod(nameof(VisorHUDController.GlitchPulse), new[] { typeof(float) }) == null)
                return Fail("VisorHUDController.GlitchPulse(float) missing.");

            return true;
        }

        private bool ValidateShaderContracts()
        {
            _debugMeteorFogShadowParamsId = _MeteorFogShadowParamsId;
            if (_AtmosphereDensityId == 0 || _FinalGiantAbyssLightId == 0 ||
                _MeteorFogShadowPositionsId == 0 || _MeteorFogShadowParamsId == 0 ||
                _SolarEmpGlitchParamsId == 0 ||
                _BakedStarCubemapReadyId == 0 || _HectonAtmosphereScatteringLutReadyId == 0 ||
                _HectonEclipseWaterShadowParamsId == 0 || _HectonRingCausticsParamsId == 0)
            {
                return Fail("One or more cataclysm shader property IDs resolved to zero.");
            }

#if UNITY_EDITOR
            if (Shader.Find(AlienSkyShaderName) == null)
                return Fail("Alien sky shader contract missing.");

            if (AssetDatabase.LoadAssetAtPath<ComputeShader>(VolumetricComputePath) == null)
                return Fail("Volumetric fog compute shader contract missing.");
#endif
            return true;
        }

        private bool ValidateFirmamentContracts()
        {
#if UNITY_EDITOR
            ComputeShader firmamentCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(FirmamentComputePath);
            if (firmamentCompute == null)
                return Fail("HectonFirmamentBake.compute missing.");

            string computeSource = ReadAssetText(FirmamentComputePath);
            if (!computeSource.Contains("#pragma kernel BakeSpectralStars") ||
                !computeSource.Contains("#pragma kernel BakeAtmosphereLut") ||
                !computeSource.Contains("HectonSpectralColor") ||
                !computeSource.Contains("_BakedStarCubemap"))
            {
                return Fail("Firmament compute shader lacks bake kernels or spectral contract.");
            }
            _debugOmegaSourceChecks++;

            string celestialSource = ReadAssetText(CelestialEnginePath);
            if (!celestialSource.Contains("FirmamentStartupStarCount = 100000") ||
                !celestialSource.Contains("TryBakeFirmamentOnce") ||
                !celestialSource.Contains("_BakedStarCubemapReady") ||
                !celestialSource.Contains("_HectonAtmosphereScatteringLUTReady"))
            {
                return Fail("HectonCelestialEngine lacks startup firmament bake contract.");
            }
            _debugOmegaSourceChecks++;

            string alienSkySource = ReadAssetText(AlienSkyShaderPath);
            if (!alienSkySource.Contains("_BakedStarCubemap") ||
                !alienSkySource.Contains("SAMPLE_TEXTURECUBE") ||
                !alienSkySource.Contains("_HectonAtmosphereScatteringLUT"))
            {
                return Fail("Alien sky shader does not sample baked stars and scattering LUT.");
            }
            _debugOmegaSourceChecks++;

            string atmosphereInclude = ReadAssetText(CelestialAtmosphereIncludePath);
            if (!atmosphereInclude.Contains("sunView01") ||
                !atmosphereInclude.Contains("_HectonAtmosphereScatteringLUTReady"))
            {
                return Fail("Celestial atmosphere include lacks sun-direction LUT sampling.");
            }
            _debugOmegaSourceChecks++;

            string coreLitSource = ReadAssetText(CoreLitIncludePath);
            if (!coreLitSource.Contains("_HectonEclipseWaterShadowParams") ||
                !coreLitSource.Contains("HectonCoreLitEvaluateRingCausticShadow"))
            {
                return Fail("CoreLit shader lacks eclipse water shadow or ring caustic contract.");
            }
            _debugOmegaSourceChecks++;

            string randomEventSource = ReadAssetText(RandomEventSystemPath);
            if (!randomEventSource.Contains("_MeteorWaterImpactParams") ||
                !randomEventSource.Contains("RegisterMassiveDisplacement"))
            {
                return Fail("RandomEventSystem lacks meteor water impact displacement contract.");
            }
            _debugOmegaSourceChecks++;

            string atmosphereManagerSource = ReadAssetText(AtmosphereManagerPath);
            if (!atmosphereManagerSource.Contains("Matrix4x4.Rotate") ||
                !atmosphereManagerSource.Contains("MultiplyVector(Vector3.forward)"))
            {
                return Fail("HectonAtmosphereManager lacks Matrix4x4 sun tracking contract.");
            }
            _debugOmegaSourceChecks++;
#endif
            return true;
        }

        private bool ValidateOmegaContracts()
        {
#if UNITY_EDITOR
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(CataclysmSystemPath);
            if (script == null)
                return Fail("CelestialCataclysmSystem source script missing.");

            string source = script.text;
            if (source.Contains(LegacyFluidInstanceToken))
                return Fail("CelestialCataclysmSystem still bypasses GlobalRegistry.Fluid.");
            _debugOmegaSourceChecks++;

            if (source.Contains(JobCompleteToken) || source.Contains(JobRunToken))
                return Fail("CelestialCataclysmSystem has a direct job barrier token.");
            _debugOmegaSourceChecks++;

            if (ContainsInterpolationToken(source) || source.Contains(StringFormatToken) || source.Contains(ToStringToken))
                return Fail("CelestialCataclysmSystem has managed string formatting tokens.");
            _debugOmegaSourceChecks++;

            if (!source.Contains("GlobalTelemetryBus.PublishPerformanceWarning"))
                return Fail("CelestialCataclysmSystem lacks GlobalTelemetryBus warning path.");
            _debugOmegaTelemetryContracts++;

            if (!source.Contains("SolarEmpNoVisorController"))
                return Fail("CelestialCataclysmSystem lacks Solar EMP no-visor telemetry.");
            _debugOmegaTelemetryContracts++;

            if (!source.Contains("_SolarEmpGlitchParams"))
                return Fail("CelestialCataclysmSystem lacks Solar EMP glitch global.");

            if (!source.Contains("s_visorControllers.Clear()"))
                return Fail("CelestialCataclysmSystem leaves static visor scratch references uncleared.");
            _debugOmegaSourceChecks++;

            MonoScript eclipseGameplay = AssetDatabase.LoadAssetAtPath<MonoScript>(EclipseGameplaySystemPath);
            if (eclipseGameplay == null)
                return Fail("EclipseGameplaySystem source script missing.");

            string eclipseGameplaySource = eclipseGameplay.text;
            if (eclipseGameplaySource.Contains(LegacyEclipseInstanceToken) ||
                eclipseGameplaySource.Contains(LegacyEclipsePublicInstanceToken))
            {
                return Fail("EclipseGameplaySystem still exposes a singleton Instance.");
            }
            _debugOmegaSourceChecks++;

            if (ContainsInterpolationToken(eclipseGameplaySource) ||
                eclipseGameplaySource.Contains(StringFormatToken) ||
                eclipseGameplaySource.Contains(ToStringToken))
            {
                return Fail("EclipseGameplaySystem has managed string formatting tokens.");
            }
            _debugOmegaSourceChecks++;

            if (!ValidateNativeQueueSentinelPair(eclipseGameplaySource, "_pendingEvents") ||
                !ValidateNativeQueueSentinelPair(eclipseGameplaySource, "_nextFrameEvents"))
            {
                return Fail("EclipseGameplayEvents has an unregistered NativeQueue.");
            }
            _debugOmegaSourceChecks++;

            if (!eclipseGameplaySource.Contains("GlobalTelemetryBus.PublishPerformanceWarning") ||
                !eclipseGameplaySource.Contains("EclipseNoEcosystemDirector"))
            {
                return Fail("EclipseGameplaySystem lacks missing-ecosystem telemetry.");
            }
            _debugOmegaTelemetryContracts++;

            MonoScript randomEvents = AssetDatabase.LoadAssetAtPath<MonoScript>(RandomEventSystemPath);
            if (randomEvents == null)
                return Fail("RandomEventSystem source script missing.");

            string randomEventSource = randomEvents.text;
            if (randomEventSource.Contains(LegacyRandomEventInstanceToken) ||
                randomEventSource.Contains(LegacyRandomEventPublicInstanceToken))
            {
                return Fail("RandomEventSystem still exposes a singleton Instance.");
            }
            _debugOmegaSourceChecks++;

            if (ContainsInterpolationToken(randomEventSource) ||
                randomEventSource.Contains(StringFormatToken) ||
                randomEventSource.Contains(ToStringToken))
            {
                return Fail("RandomEventSystem has managed string formatting tokens.");
            }
            _debugOmegaSourceChecks++;

            if (!ValidateNativeQueueSentinelPair(randomEventSource, "_pendingStarted") ||
                !ValidateNativeQueueSentinelPair(randomEventSource, "_nextFrameStarted") ||
                !ValidateNativeQueueSentinelPair(randomEventSource, "_pendingEnded") ||
                !ValidateNativeQueueSentinelPair(randomEventSource, "_nextFrameEnded") ||
                !ValidateNativeQueueSentinelPair(randomEventSource, "_pendingSeismicShockwaves") ||
                !ValidateNativeQueueSentinelPair(randomEventSource, "_nextFrameSeismicShockwaves"))
            {
                return Fail("RandomEventEvents has an unregistered NativeQueue.");
            }
            _debugOmegaSourceChecks++;

            if (!randomEventSource.Contains("RandomEventMeteorMath.EvaluateMeteorFlash") ||
                !randomEventSource.Contains("RandomEventMeteorMath.Hash01"))
            {
                return Fail("RandomEventSystem still owns meteor math instead of RandomEventMeteorMath.");
            }
            _debugOmegaSourceChecks++;

            MonoScript meteorMath = AssetDatabase.LoadAssetAtPath<MonoScript>(RandomEventMeteorMathPath);
            if (meteorMath == null)
                return Fail("RandomEventMeteorMath source script missing.");

            string meteorMathSource = meteorMath.text;
            if (!meteorMathSource.Contains("EvaluateMeteorFlash") ||
                !meteorMathSource.Contains("Hash01"))
            {
                return Fail("RandomEventMeteorMath lacks meteor flash/hash contracts.");
            }
            _debugOmegaSourceChecks++;

            if (ContainsInterpolationToken(meteorMathSource) ||
                meteorMathSource.Contains(StringFormatToken) ||
                meteorMathSource.Contains(ToStringToken))
            {
                return Fail("RandomEventMeteorMath has managed string formatting tokens.");
            }
            _debugOmegaSourceChecks++;

            MonoScript syncSmoke = AssetDatabase.LoadAssetAtPath<MonoScript>(CelestialSyncSmokeTesterPath);
            if (syncSmoke == null)
                return Fail("CelestialSyncSmokeTester source script missing.");

            string syncSmokeSource = syncSmoke.text;
            if (syncSmokeSource.Contains(LegacyEclipseInstanceToken) ||
                syncSmokeSource.Contains(LegacyRandomEventInstanceToken))
            {
                return Fail("CelestialSyncSmokeTester still reads a legacy celestial singleton.");
            }
            _debugOmegaSourceChecks++;
#endif
            return true;
        }

        private bool ValidateRuntimeCelestialValues()
        {
            HectonCelestialEngine celestialEngine = HectonCelestialEngine.ActiveRuntimeInstance;
            if (celestialEngine == null)
                return true;

            _debugAtmosphereDensity = celestialEngine.AtmosphereDensity;
            _debugLunarMultiplier = celestialEngine.LunarResonanceBiolumMultiplier;

            if (!float.IsFinite(_debugAtmosphereDensity) || _debugAtmosphereDensity < 0f || _debugAtmosphereDensity > 1f)
                return Fail("Atmosphere density runtime value is outside [0,1].");

            if (!float.IsFinite(_debugLunarMultiplier) || _debugLunarMultiplier < 1f)
                return Fail("Lunar resonance multiplier runtime value is invalid.");

            return true;
        }

        private bool ValidateTitaniumScrapContract()
        {
#if UNITY_EDITOR
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(TitaniumScrapAssetPath);
            if (item == null)
                return Fail("Data_TitaniumScrap asset missing.");

            if (item.worldPrefab == null)
                return Fail("Data_TitaniumScrap.worldPrefab is missing.");
#endif
            return true;
        }

        private bool Fail(string issue)
        {
            _debugLastPass = false;
            _debugLastIssue = issue;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[CelestialCataclysmSmoke] FAIL " + issue, this);
#endif
            return false;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogVerbose(string message)
        {
            if (verboseLogging)
                Debug.Log(message, this);
        }

#if UNITY_EDITOR
        private static string SanitizeJsonField(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace('"', '\'');
        }

        private static bool ContainsInterpolationToken(string source)
        {
            if (string.IsNullOrEmpty(source))
                return false;

            for (int i = 1; i < source.Length; i++)
            {
                if (source[i - 1] == InterpolationDollar && source[i] == QuoteChar)
                    return true;
            }

            return false;
        }

        private static bool ValidateNativeQueueSentinelPair(string source, string fieldName)
        {
            return source.Contains("NativeMemorySentinel.RegisterNativeQueue") &&
                   source.Contains("NativeMemorySentinel.UnregisterNativeQueue") &&
                   source.Contains("nameof(" + fieldName + ")");
        }

        private static string ReadAssetText(string assetPath)
        {
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            return File.Exists(fullPath) ? File.ReadAllText(fullPath) : string.Empty;
        }

        private static string BuildBatchJson(bool pass, string issue, int sourceChecks, int telemetryContracts)
        {
            return BatchJsonStatusPrefix +
                   (pass ? "PASS" : "FAIL") +
                   BatchJsonIssueSegment +
                   issue +
                   BatchJsonSourceChecksSegment +
                   sourceChecks +
                   BatchJsonTelemetryContractsSegment +
                   telemetryContracts +
                   BatchJsonSuffix;
        }
#endif
    }
}

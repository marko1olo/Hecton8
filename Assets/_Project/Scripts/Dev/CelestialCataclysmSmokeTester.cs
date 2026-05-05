// ============================================================================
// HECTON-8 - CelestialCataclysmSmokeTester.cs
// Dev-only smoke coverage for astrodynamics cataclysm contracts.
// ============================================================================

using System;
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
        private const string AlienSkyShaderName = "HECTON/Sky/Hecton_AlienSky_Master";
        private const string BatchJsonPass = "{\"tester\":\"CelestialCataclysmSmokeTester\",\"status\":\"PASS\",\"issue\":\"\"}";
        private const string BatchJsonFailPrefix = "{\"tester\":\"CelestialCataclysmSmokeTester\",\"status\":\"FAIL\",\"issue\":\"";
        private const string BatchJsonFailSuffix = "\"}";

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
#pragma warning restore CS0414

        private static readonly int _AtmosphereDensityId = Shader.PropertyToID("_AtmosphereDensity");
        private static readonly int _FinalGiantAbyssLightId = Shader.PropertyToID("_FinalGiantAbyssLight");
        private static readonly int _MeteorFogShadowPositionsId = Shader.PropertyToID("_MeteorFogShadowPositions");
        private static readonly int _MeteorFogShadowParamsId = Shader.PropertyToID("_MeteorFogShadowParams");
        private static readonly int _SolarEmpGlitchParamsId = Shader.PropertyToID("_SolarEmpGlitchParams");

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
            Debug.Log(pass ? BatchJsonPass : BatchJsonFailPrefix + issue + BatchJsonFailSuffix);
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

            if (!ValidateRandomEventContracts())
                return false;

            if (!ValidateTypeContracts())
                return false;

            if (!ValidateShaderContracts())
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

            if (typeof(GlobalTelemetryBus).GetMethod(
                    nameof(GlobalTelemetryBus.PublishPerformanceWarning),
                    new[] { typeof(uint), typeof(uint), typeof(float) }) == null)
            {
                return Fail("GlobalTelemetryBus.PublishPerformanceWarning(uint,uint,float) missing.");
            }

            if (typeof(VisorHUDController).GetMethod(nameof(VisorHUDController.GlitchPulse), new[] { typeof(float) }) == null)
                return Fail("VisorHUDController.GlitchPulse(float) missing.");

            return true;
        }

        private bool ValidateShaderContracts()
        {
            _debugMeteorFogShadowParamsId = _MeteorFogShadowParamsId;
            if (_AtmosphereDensityId == 0 || _FinalGiantAbyssLightId == 0 ||
                _MeteorFogShadowPositionsId == 0 || _MeteorFogShadowParamsId == 0 ||
                _SolarEmpGlitchParamsId == 0)
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

        private bool ValidateOmegaContracts()
        {
#if UNITY_EDITOR
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(CataclysmSystemPath);
            if (script == null)
                return Fail("CelestialCataclysmSystem source script missing.");

            string source = script.text;
            if (source.Contains("HectonFluidEngine.Instance"))
                return Fail("CelestialCataclysmSystem still bypasses GlobalRegistry.Fluid.");

            if (!source.Contains("GlobalTelemetryBus.PublishPerformanceWarning"))
                return Fail("CelestialCataclysmSystem lacks GlobalTelemetryBus warning path.");

            if (!source.Contains("_SolarEmpGlitchParams"))
                return Fail("CelestialCataclysmSystem lacks Solar EMP glitch global.");
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
#endif
    }
}

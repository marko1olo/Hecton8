// ============================================================================
// HECTON-8 - CelestialSyncSmokeTester.cs
// Dev-only smoke coverage for orbital/environment sync.
// Verifies eclipse penumbra math, Aegir fixed sky lock, star seed path,
// depth-cache presence, biolum bridge presence, and eclipse audio pitch scalar.
// ============================================================================

using Hecton8.Atmosphere;
using Hecton8.Audio;
using Hecton8.Celestial;
using Hecton8.Gameplay;
using Hecton8.World;
using UnityEngine;

namespace Hecton8.Dev
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Celestial Sync Smoke Tester")]
    public sealed class CelestialSyncSmokeTester : MonoBehaviour
    {
        private const float PenumbraEpsilon = 0.0001f;
        private const float PitchRatioTolerance = 0.0005f;

        [Header("References")]
        [SerializeField] private HectonCelestialEngine celestialEngine;
        [SerializeField] private EclipseGameplaySystem eclipseGameplaySystem;
        [SerializeField] private HectonCrestOceanDepthCacheBootstrap depthCacheBootstrap;
        [SerializeField] private EcosystemDirector ecosystemDirector;
        [SerializeField] private HectonBiolumController biolumController;
        [SerializeField] private SpatialAudioManager spatialAudioManager;

        [Header("Execution")]
        [SerializeField] private bool runOnStart = false;
        [SerializeField] private bool verboseLogging = false;

#pragma warning disable CS0414
        [Header("Debug")]
        [SerializeField] private int _debugRunCount;
        [SerializeField] private bool _debugLastPass;
        [SerializeField] private string _debugLastIssue = string.Empty;
        [SerializeField] private float _debugAegirDirectionMagnitude;
        [SerializeField] private float _debugStarSeed;
        [SerializeField] private float _debugPenumbraPartial;
        [SerializeField] private float _debugEclipsePitchRatio;
#pragma warning restore CS0414

        private void Awake()
        {
            AutoResolve();
        }

        private void Start()
        {
            if (runOnStart)
                RunSmokePass();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            AutoResolve();
        }
#endif

        [ContextMenu("Run Celestial Sync Smoke Pass")]
        public void RunFromContextMenu()
        {
            RunSmokePass();
        }

        public bool RunSmokePass()
        {
            AutoResolve();
            _debugRunCount++;
            _debugLastPass = false;
            _debugLastIssue = string.Empty;

            if (!ValidateReferences())
                return false;

            if (!ValidateAegirLock())
                return false;

            if (!ValidatePenumbraMath())
                return false;

            if (!ValidateStarSeed())
                return false;

            if (!ValidateAtmosphereFacade())
                return false;

            if (!ValidateAudioPitchScalar())
                return false;

            _debugLastPass = true;
            _debugLastIssue = string.Empty;
            LogVerbose("[CelestialSyncSmoke] COMPLETE pass=True");
            return true;
        }

        private void AutoResolve()
        {
            if (celestialEngine == null)
                celestialEngine = HectonCelestialEngine.ActiveRuntimeInstance != null
                    ? HectonCelestialEngine.ActiveRuntimeInstance
                    : UnityEngine.Object.FindAnyObjectByType<HectonCelestialEngine>(FindObjectsInactive.Include);

            if (eclipseGameplaySystem == null)
                eclipseGameplaySystem = EclipseGameplaySystem.Instance != null
                    ? EclipseGameplaySystem.Instance
                    : UnityEngine.Object.FindAnyObjectByType<EclipseGameplaySystem>(FindObjectsInactive.Include);

            if (depthCacheBootstrap == null)
                depthCacheBootstrap = UnityEngine.Object.FindAnyObjectByType<HectonCrestOceanDepthCacheBootstrap>(FindObjectsInactive.Include);

            if (ecosystemDirector == null)
                ecosystemDirector = UnityEngine.Object.FindAnyObjectByType<EcosystemDirector>(FindObjectsInactive.Include);

            if (biolumController == null)
                biolumController = UnityEngine.Object.FindAnyObjectByType<HectonBiolumController>(FindObjectsInactive.Include);

            if (spatialAudioManager == null)
                spatialAudioManager = SpatialAudioManager.ActiveRuntimeInstance != null
                    ? SpatialAudioManager.ActiveRuntimeInstance
                    : UnityEngine.Object.FindAnyObjectByType<SpatialAudioManager>(FindObjectsInactive.Include);
        }

        private bool ValidateReferences()
        {
            if (celestialEngine == null)
                return Fail("Missing HectonCelestialEngine.");
            if (eclipseGameplaySystem == null)
                return Fail("Missing EclipseGameplaySystem.");
            if (depthCacheBootstrap == null)
                return Fail("Missing HectonCrestOceanDepthCacheBootstrap.");
            if (ecosystemDirector == null)
                return Fail("Missing EcosystemDirector.");
            if (biolumController == null)
                return Fail("Missing HectonBiolumController.");
            if (spatialAudioManager == null)
                return Fail("Missing SpatialAudioManager.");

            return true;
        }

        private bool ValidateAegirLock()
        {
            if (!celestialEngine.TryGetAegirSkyDirection(out Vector3 aegirDirection))
                return Fail("Aegir sky direction unresolved.");

            _debugAegirDirectionMagnitude = aegirDirection.magnitude;
            if (_debugAegirDirectionMagnitude < 0.99f || _debugAegirDirectionMagnitude > 1.01f)
                return Fail("Aegir sky direction is not normalized.");

            if (!celestialEngine.IsAegirFixedDirectionLocked)
                return Fail("Aegir observer body is not FixedDirection locked.");

            return true;
        }

        private bool ValidatePenumbraMath()
        {
            float full = HectonCelestialEngine.EvaluatePenumbraOverlapForSmoke(0.27f, 0.6f, 0f);
            if (full < 1f - PenumbraEpsilon)
                return Fail("Penumbra full-overlap case failed.");

            float partial = HectonCelestialEngine.EvaluatePenumbraOverlapForSmoke(0.27f, 0.27f, 0.2f);
            _debugPenumbraPartial = partial;
            if (partial <= PenumbraEpsilon || partial >= 1f - PenumbraEpsilon)
                return Fail("Penumbra partial-overlap case failed.");

            float separated = HectonCelestialEngine.EvaluatePenumbraOverlapForSmoke(0.27f, 0.27f, 1f);
            if (separated > PenumbraEpsilon)
                return Fail("Penumbra separated-disc case failed.");

            return true;
        }

        private bool ValidateStarSeed()
        {
            _debugStarSeed = celestialEngine.ResolvedStarMapSeed;
            if (!(_debugStarSeed >= 0f))
                return Fail("Resolved star seed is invalid.");

            return true;
        }

        private bool ValidateAtmosphereFacade()
        {
            Material skybox = AtmosphereDirector.Skybox;
            if (skybox != null && !AtmosphereDirector.IsSkybox(skybox))
                return Fail("AtmosphereDirector skybox facade mismatch.");

            return true;
        }

        private bool ValidateAudioPitchScalar()
        {
            float previousCents = spatialAudioManager.EclipseAcousticPitchShiftCents;
            spatialAudioManager.SetEclipseAcousticPitchShiftCents(-150f);

            float expectedRatio = Mathf.Pow(2f, -150f / 1200f);
            _debugEclipsePitchRatio = spatialAudioManager.EclipseAcousticPitchRatio;
            bool valid = Mathf.Abs(_debugEclipsePitchRatio - expectedRatio) <= PitchRatioTolerance;

            spatialAudioManager.SetEclipseAcousticPitchShiftCents(previousCents);

            if (!valid)
                return Fail("Eclipse acoustic pitch ratio mismatch.");

            return true;
        }

        private bool Fail(string issue)
        {
            _debugLastPass = false;
            _debugLastIssue = issue;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[CelestialSyncSmoke] FAIL " + issue, this);
#endif
            return false;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogVerbose(string message)
        {
            if (verboseLogging)
                Debug.Log(message, this);
        }
    }
}

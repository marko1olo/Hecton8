using System;
using System.Globalization;
using System.Reflection;
using System.Text;
using Hecton8.Core;
using Hecton8.Physics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Runs a multi-frame Crest 4 vs Crest 5 runtime height parity test in the migration scene.
    /// </summary>
    internal static class CrestParityRunner
    {
        private const string MenuRunPath = "Tools/Hecton8/Crest/Run Height Parity Test";
        private const string MenuShowLastPath = "Tools/Hecton8/Crest/Show Last Height Parity Result";
        private const string ArmedKey = "Hecton8.CrestParity.Armed";
        private const string FramesKey = "Hecton8.CrestParity.Frames";
        private const string ResultKey = "Hecton8.CrestParity.Result";
        private const string RestoreLegacyActiveKey = "Hecton8.CrestParity.RestoreLegacyActive";
        private const string WarmupFramesKey = "Hecton8.CrestParity.WarmupFrames";
        private const string MaxFramesKey = "Hecton8.CrestParity.MaxFrames";
        private const int SampleCount = 5;
        private const int DefaultWarmupFrames = 90;
        private const int DefaultMaxFrames = 240;
        private const float MinimumSpatialLength = 1f;
        private const string Crest4AdapterTypeName = "Hecton8.Physics.Crest4KinematicsAdapter";
        private const string Crest5AdapterTypeName = "Hecton8.Physics.Crest5KinematicsAdapter";

        private static GameObject s_Probe;
        private static IHectonOceanKinematics s_Crest4Adapter;
        private static IHectonOceanKinematics s_Crest5Adapter;
        private static Vector3[] s_SamplePositions;
        private static float[] s_Crest4Heights;
        private static float[] s_Crest5Heights;

        [MenuItem(MenuRunPath)]
        private static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[CrestParity] Editor is already entering or running play mode.");
                return;
            }

            Crest.OceanRenderer legacyOcean = FindLegacyOcean(includeInactive: true);
            WaveHarmonic.Crest.WaterRenderer crest5Water = FindCrest5Water(includeInactive: true);

            if (legacyOcean == null || crest5Water == null)
            {
                Debug.LogError("[CrestParity] Missing legacy Crest 4 or Crest 5 water renderer in the active scene.");
                return;
            }

            SessionState.SetBool(ArmedKey, true);
            SessionState.SetInt(FramesKey, 0);
            SessionState.SetInt(WarmupFramesKey, DefaultWarmupFrames);
            SessionState.SetInt(MaxFramesKey, DefaultMaxFrames);
            SessionState.EraseString(ResultKey);
            SessionState.SetBool(RestoreLegacyActiveKey, legacyOcean.gameObject.activeSelf);

            legacyOcean.gameObject.SetActive(true);
            EditorUtility.SetDirty(legacyOcean.gameObject);

            RegisterCallbacks();
            DestroyRuntimeHarness();

            Debug.Log("[CrestParity] Armed. Entering play mode.");
            EditorApplication.isPlaying = true;
        }

        [MenuItem(MenuShowLastPath)]
        private static void ShowLastResult()
        {
            string result = SessionState.GetString(ResultKey, string.Empty);
            if (string.IsNullOrWhiteSpace(result))
            {
                Debug.LogWarning("[CrestParity] No stored parity result.");
                return;
            }

            Debug.Log(result);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ArmedKey, false))
                return;

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetInt(FramesKey, 0);
                EditorApplication.update -= OnEditorUpdate;
                EditorApplication.update += OnEditorUpdate;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= OnEditorUpdate;
                RestoreLegacyOceanActiveState();
                DestroyRuntimeHarness();

                string result = SessionState.GetString(ResultKey, string.Empty);
                if (!string.IsNullOrWhiteSpace(result))
                    Debug.Log(result);

                SessionState.SetBool(ArmedKey, false);
                UnregisterCallbacks();
            }
        }

        private static void OnEditorUpdate()
        {
            if (!Application.isPlaying)
                return;

            int frameCount = SessionState.GetInt(FramesKey, 0) + 1;
            SessionState.SetInt(FramesKey, frameCount);

            Crest.OceanRenderer legacyOcean = FindLegacyOcean(includeInactive: false);
            WaveHarmonic.Crest.WaterRenderer crest5Water = FindCrest5Water(includeInactive: false);
            if (legacyOcean == null || crest5Water == null)
            {
                FinalizeRun("[CrestParity] missing legacy Crest 4 or Crest 5 water renderer in play mode.");
                return;
            }

            EnsureRuntimeHarness();
            if (s_Crest4Adapter == null || s_Crest5Adapter == null)
            {
                FinalizeRun("[CrestParity] missing Crest kinematics adapter types.");
                return;
            }

            PrimeLegacyProviders(legacyOcean);
            PrimeCrest5Providers(crest5Water);

            if (frameCount < SessionState.GetInt(WarmupFramesKey, DefaultWarmupFrames))
                return;

            bool ok4 = s_Crest4Adapter.GetWaterHeight(s_SamplePositions, SampleCount, MinimumSpatialLength, s_Crest4Heights);
            bool ok5 = s_Crest5Adapter.GetWaterHeight(s_SamplePositions, SampleCount, MinimumSpatialLength, s_Crest5Heights);

            string report = BuildParityReport(frameCount, legacyOcean, crest5Water, ok4, ok5);
            SessionState.SetString(ResultKey, report);

            if (ok4 && ok5)
            {
                FinalizeRun(report);
                return;
            }

            if (frameCount >= SessionState.GetInt(MaxFramesKey, DefaultMaxFrames))
                FinalizeRun(report);
        }

        private static void FinalizeRun(string result)
        {
            SessionState.SetString(ResultKey, result);
            EditorApplication.update -= OnEditorUpdate;
            DestroyRuntimeHarness();
            EditorApplication.isPlaying = false;
        }

        private static void RegisterCallbacks()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= OnEditorUpdate;
        }

        private static void UnregisterCallbacks()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.update -= OnEditorUpdate;
        }

        private static string BuildParityReport(
            int frameCount,
            Crest.OceanRenderer legacyOcean,
            WaveHarmonic.Crest.WaterRenderer crest5Water,
            bool ok4,
            bool ok5)
        {
            Vector3 anchor = ResolveAnchor(crest5Water);
            PopulateSamplePositions(anchor);

            StringBuilder report = new StringBuilder(768);
            report.AppendLine("[CrestParity] frames=" + frameCount);
            report.AppendLine("[CrestParity] ok4=" + ok4 + " ok5=" + ok5 + " anchor=" + anchor);
            report.AppendLine("[CrestParity] providers crest4Collision=" + (legacyOcean.CollisionProvider != null) +
                              " crest4Flow=" + (legacyOcean.FlowProvider != null) +
                              " crest5Collision=" + (crest5Water.CollisionProvider != null) +
                              " crest5Flow=" + (crest5Water.FlowProvider != null));

            float maxDelta = 0f;
            if (ok4 && ok5)
            {
                for (int i = 0; i < SampleCount; i++)
                {
                    float delta = Mathf.Abs(s_Crest4Heights[i] - s_Crest5Heights[i]);
                    if (delta > maxDelta)
                        maxDelta = delta;

                    report.AppendLine(
                        "[CrestParity] sample=" + i +
                        " point=" + s_SamplePositions[i] +
                        " crest4=" + s_Crest4Heights[i].ToString("F6", CultureInfo.InvariantCulture) +
                        " crest5=" + s_Crest5Heights[i].ToString("F6", CultureInfo.InvariantCulture) +
                        " delta=" + delta.ToString("F6", CultureInfo.InvariantCulture));
                }

                report.AppendLine("[CrestParity] maxDelta=" + maxDelta.ToString("F6", CultureInfo.InvariantCulture));
                report.AppendLine("[CrestParity] status=" + (maxDelta > 0.01f ? "FFT_CALIBRATION_REQUIRED" : "WITHIN_THRESHOLD"));
            }
            else
            {
                report.AppendLine("[CrestParity] status=QUERY_NOT_READY");
            }

            return report.ToString();
        }

        private static void EnsureRuntimeHarness()
        {
            if (s_Probe == null)
            {
                // COLD ALLOC: GameObject[1] - one-shot parity probe owner for editor migration validation - owner: CrestParityRunner
                s_Probe = new GameObject("__CrestParityProbe")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            if (s_Crest4Adapter == null)
                s_Crest4Adapter = ResolveOrAddAdapter(Crest4AdapterTypeName);

            if (s_Crest5Adapter == null)
                s_Crest5Adapter = ResolveOrAddAdapter(Crest5AdapterTypeName);

            if (s_SamplePositions == null)
            {
                // COLD ALLOC: Vector3[5] - one-shot parity sample positions for editor migration validation - owner: CrestParityRunner
                s_SamplePositions = new Vector3[SampleCount];
            }

            if (s_Crest4Heights == null)
            {
                // COLD ALLOC: float[5] - one-shot Crest 4 parity heights for editor migration validation - owner: CrestParityRunner
                s_Crest4Heights = new float[SampleCount];
            }

            if (s_Crest5Heights == null)
            {
                // COLD ALLOC: float[5] - one-shot Crest 5 parity heights for editor migration validation - owner: CrestParityRunner
                s_Crest5Heights = new float[SampleCount];
            }

            PopulateSamplePositions(ResolveAnchor(FindCrest5Water(includeInactive: false)));
        }

        private static IHectonOceanKinematics ResolveOrAddAdapter(string adapterTypeName)
        {
            Type adapterType = ResolveType(adapterTypeName);
            if (adapterType == null || !typeof(Component).IsAssignableFrom(adapterType))
            {
                Debug.LogError("[CrestParity] Adapter type unavailable: " + adapterTypeName);
                return null;
            }

            Component component = s_Probe.GetComponent(adapterType);
            if (component == null)
                component = s_Probe.AddComponent(adapterType);

            IHectonOceanKinematics adapter = component as IHectonOceanKinematics;
            if (adapter == null)
                Debug.LogError("[CrestParity] Adapter does not implement IHectonOceanKinematics: " + adapterTypeName);

            return adapter;
        }

        private static Type ResolveType(string typeName)
        {
            Type type = Type.GetType(typeName);
            if (type != null)
                return type;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(typeName);
                if (type != null)
                    return type;
            }

            return null;
        }

        private static void DestroyRuntimeHarness()
        {
            s_Crest4Adapter = null;
            s_Crest5Adapter = null;
            if (s_Probe != null)
            {
                UnityEngine.Object.DestroyImmediate(s_Probe);
                s_Probe = null;
            }
        }

        private static Vector3 ResolveAnchor(WaveHarmonic.Crest.WaterRenderer crest5Water)
        {
            Camera contextCamera = GlobalRenderContext.CurrentCamera;
            Vector3 anchor = contextCamera != null ? contextCamera.transform.position : crest5Water.transform.position;
            anchor.y = crest5Water != null ? crest5Water.SeaLevel : anchor.y;
            return anchor;
        }

        private static void PopulateSamplePositions(Vector3 anchor)
        {
            if (s_SamplePositions == null)
                return;

            s_SamplePositions[0] = new Vector3(anchor.x, anchor.y, anchor.z);
            s_SamplePositions[1] = new Vector3(anchor.x + 25f, anchor.y, anchor.z);
            s_SamplePositions[2] = new Vector3(anchor.x - 25f, anchor.y, anchor.z + 15f);
            s_SamplePositions[3] = new Vector3(anchor.x + 40f, anchor.y, anchor.z - 20f);
            s_SamplePositions[4] = new Vector3(anchor.x - 60f, anchor.y, anchor.z - 35f);
        }

        private static void PrimeLegacyProviders(Crest.OceanRenderer legacyOcean)
        {
            MethodInfo createDestroySubSystems = typeof(Crest.OceanRenderer).GetMethod(
                "CreateDestroySubSystems",
                BindingFlags.Instance | BindingFlags.NonPublic);
            createDestroySubSystems?.Invoke(legacyOcean, null);

            if (legacyOcean.CollisionProvider == null)
            {
                Crest.SimSettingsAnimatedWaves settings =
                    legacyOcean._lodDataAnimWaves != null ? legacyOcean._lodDataAnimWaves.Settings : legacyOcean._simSettingsAnimatedWaves;

                if (settings != null)
                {
                    PropertyInfo collisionProviderProperty = typeof(Crest.OceanRenderer).GetProperty(
                        nameof(Crest.OceanRenderer.CollisionProvider),
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    collisionProviderProperty?.SetValue(legacyOcean, settings.CreateCollisionProvider(), null);

                    PropertyInfo flowProviderProperty = typeof(Crest.OceanRenderer).GetProperty(
                        nameof(Crest.OceanRenderer.FlowProvider),
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    flowProviderProperty?.SetValue(legacyOcean, settings.CreateFlowProvider(legacyOcean), null);
                }
            }
        }

        private static void PrimeCrest5Providers(WaveHarmonic.Crest.WaterRenderer crest5Water)
        {
            InvokeInitializeProvider(crest5Water.AnimatedWavesLod);
            InvokeInitializeProvider(crest5Water.FlowLod);
            InvokeInitializeProvider(crest5Water.DepthLod);
        }

        private static void InvokeInitializeProvider(object lod)
        {
            if (lod == null)
                return;

            MethodInfo method = null;
            System.Type type = lod.GetType();
            while (type != null && method == null)
            {
                method = type.GetMethod("InitializeProvider", BindingFlags.Instance | BindingFlags.NonPublic);
                type = type.BaseType;
            }

            method?.Invoke(lod, new object[] { true });
        }

        private static Crest.OceanRenderer FindLegacyOcean(bool includeInactive)
        {
            return UnityEngine.Object.FindAnyObjectByType<Crest.OceanRenderer>(
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
        }

        private static WaveHarmonic.Crest.WaterRenderer FindCrest5Water(bool includeInactive)
        {
            return UnityEngine.Object.FindAnyObjectByType<WaveHarmonic.Crest.WaterRenderer>(
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
        }

        private static void RestoreLegacyOceanActiveState()
        {
            Crest.OceanRenderer legacyOcean = FindLegacyOcean(includeInactive: true);
            if (legacyOcean == null)
                return;

            legacyOcean.gameObject.SetActive(SessionState.GetBool(RestoreLegacyActiveKey, false));
            EditorUtility.SetDirty(legacyOcean.gameObject);
        }
    }
}

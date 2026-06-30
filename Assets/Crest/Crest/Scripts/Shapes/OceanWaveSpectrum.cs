// Crest Ocean System

// Copyright 2020 Wave Harmonic Ltd

using UnityEngine;
using UnityEditor;

namespace Crest
{
    /// <summary>
    /// Ocean shape representation - power values for each octave of wave components.
    /// </summary>
    [CreateAssetMenu(fileName = "OceanWaves", menuName = "Crest/Ocean Wave Spectrum", order = 10000)]
    [HelpURL(Internal.Constants.HELP_URL_BASE_USER + "waves.html" + Internal.Constants.HELP_URL_RP)]
    public class OceanWaveSpectrum : ScriptableObject
    {
        /// <summary>
        /// The version of this asset. Can be used to migrate across versions. This value should
        /// only be changed when the editor upgrades the version.
        /// </summary>
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _version = 1;
#pragma warning restore 414

        // These must match corresponding constants in FFTSpectrum.compute
        public const int NUM_OCTAVES = 14;
        public static readonly float SMALLEST_WL_POW_2 = -4f;

        [HideInInspector]
        public float _fetch = 500000f;

        public static readonly float MIN_POWER_LOG = -8f;
        public static readonly float MAX_POWER_LOG = 5f;

        [Tooltip("Variance of wave directions, in degrees."), Range(0f, 180f), HideInInspector]
        public float _waveDirectionVariance = 90f;

        [Tooltip("More gravity means faster waves."), Range(0f, 25f), HideInInspector]
        public float _gravityScale = 1f;

        [Range(0f, 2f), HideInInspector]
        public float _smallWavelengthMultiplier = 1f;

        [Tooltip("Multiplier which scales waves"), Range(0f, 10f)]
        public float _multiplier = 1f;

        [HideInInspector, SerializeField]
        internal float[] _powerLog = new float[NUM_OCTAVES]
            { -5.71f, -5.03f, -4.54f, -3.88f, -3.28f, -2.32f, -1.78f, -1.21f, -0.54f, 0.28f, 0.54f, 1.03f, 1.44f, -8f };

        [HideInInspector, SerializeField]
        internal bool[] _powerDisabled = new bool[NUM_OCTAVES];

        [HideInInspector]
        public float[] _chopScales = new float[NUM_OCTAVES]
            { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };

        [HideInInspector]
        public float[] _gravityScales = new float[NUM_OCTAVES]
            { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };

        [Tooltip("Scales horizontal displacement"), Range(0f, 2f)]
        public float _chop = 1.6f;


        static void Upgrade(SerializedObject soSpectrum)
        {
            var spVer = soSpectrum.FindProperty("_version");

            // Future: Upgrade to version 2: ...

            soSpectrum.ApplyModifiedProperties();
        }

        public override void OnInspectorGUI()
        {
            // Display a notice if its being edited as a standalone asset (not embedded in a component) because
            // it displays the FFT-interface.
            if (_hostComponentType == null)
            {
                EditorGUILayout.HelpBox("This editor is displaying the FFT spectrum settings. " +
                    "To edit settings specific to the ShapeGerstner component, assign this asset to a ShapeGerstner component " +
                    "and edit it there by expanding the Spectrum field.", MessageType.Info);
                EditorGUILayout.Space();
            }

            base.OnInspectorGUI();

            bool beingEditedOnGerstnerComponent = _hostComponentType == typeof(ShapeGerstner) || _hostComponentType == typeof(ShapeGerstnerBatched);

            bool showAdvancedControls = false;
            if (beingEditedOnGerstnerComponent)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_gravityScale"));

                EditorGUILayout.PropertyField(serializedObject.FindProperty("_waveDirectionVariance"));

                EditorGUILayout.PropertyField(serializedObject.FindProperty("_showAdvancedControls"));
                showAdvancedControls = serializedObject.FindProperty("_showAdvancedControls").boolValue;
            }
            else
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_gravityScale"), s_timeScaleLabel);
            }

            var spSpectrumModel = serializedObject.FindProperty("_model");
            var spectraIndex = serializedObject.FindProperty("_model").enumValueIndex;
            var spectrumModel = (OceanWaveSpectrum.SpectrumModel)Mathf.Clamp(spectraIndex, 0, 1);

            EditorGUILayout.Space();

            var spDisabled = serializedObject.FindProperty("_powerDisabled");
            EditorGUILayout.BeginHorizontal();
            bool allEnabled = true;
            for (int i = 0; i < spDisabled.arraySize; i++)
            {
                if (spDisabled.GetArrayElementAtIndex(i).boolValue) allEnabled = false;
            }
            bool toggle = allEnabled;
            if (toggle != EditorGUILayout.Toggle(toggle, GUILayout.Width(13f)))
            {
                for (int i = 0; i < spDisabled.arraySize; i++)
                {
                    spDisabled.GetArrayElementAtIndex(i).boolValue = toggle;
                }
            }
            EditorGUILayout.LabelField("Spectrum", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            var spec = target as OceanWaveSpectrum;

            var spPower = serializedObject.FindProperty("_powerLog");
            var spChopScales = serializedObject.FindProperty("_chopScales");
            var spGravScales = serializedObject.FindProperty("_gravityScales");

            // Disable sliders if authoring with model.
            var canEditSpectrum = spectrumModel != OceanWaveSpectrum.SpectrumModel.None;

            for (int i = 0; i < spPower.arraySize; i++)
            {
                EditorGUILayout.BeginHorizontal();

                var spDisabled_i = spDisabled.GetArrayElementAtIndex(i);
                spDisabled_i.boolValue = !EditorGUILayout.Toggle(!spDisabled_i.boolValue, GUILayout.Width(15f));

                float smallWL = OceanWaveSpectrum.SmallWavelength(i);
                var spPower_i = spPower.GetArrayElementAtIndex(i);

                var isPowerDisabled = spDisabled_i.boolValue;
                var powerValue = isPowerDisabled ? OceanWaveSpectrum.MIN_POWER_LOG : spPower_i.floatValue;

                if (showAdvancedControls)
                {
                    EditorGUILayout.LabelField(string.Format("{0}", smallWL), EditorStyles.boldLabel);
                    EditorGUILayout.EndHorizontal();
                    // Disable slider if authoring with model.
                    GUI.enabled = !canEditSpectrum && !spDisabled_i.boolValue;
                    powerValue = EditorGUILayout.Slider("    Power", powerValue, OceanWaveSpectrum.MIN_POWER_LOG, OceanWaveSpectrum.MAX_POWER_LOG);
                    GUI.enabled = true;
                }
                else
                {
                    EditorGUILayout.LabelField(string.Format("{0}", smallWL), GUILayout.Width(50f));
                    // Disable slider if authoring with model.
                    GUI.enabled = !canEditSpectrum && !spDisabled_i.boolValue;
                    powerValue = EditorGUILayout.Slider(powerValue, OceanWaveSpectrum.MIN_POWER_LOG, OceanWaveSpectrum.MAX_POWER_LOG);
                    GUI.enabled = true;
                    EditorGUILayout.EndHorizontal();
                    // This will create a tooltip for slider.
                    GUI.Label(GUILayoutUtility.GetLastRect(), new GUIContent("", powerValue.ToString()));
                }

                // If the power is disabled, we are using the MIN_POWER_LOG value so we don't want to store it.
                if (!isPowerDisabled)
                {
                    spPower_i.floatValue = powerValue;
                }

                if (showAdvancedControls)
                {
                    EditorGUILayout.Slider(spChopScales.GetArrayElementAtIndex(i), 0f, 4f, "    Chop Scale");
                    EditorGUILayout.Slider(spGravScales.GetArrayElementAtIndex(i), 0f, 4f, "    Grav Scale");
                }
            }


            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Empirical Spectra", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            spectrumModel = (OceanWaveSpectrum.SpectrumModel)EditorGUILayout.EnumPopup(spectrumModel);
            spSpectrumModel.enumValueIndex = (int)spectrumModel;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(s_modelDescriptions[(int)spectrumModel], MessageType.Info);
            EditorGUILayout.Space();

            if (spectrumModel == OceanWaveSpectrum.SpectrumModel.None)
            {
                Undo.RecordObject(spec, "Change Spectrum");
            }
            else
            {
                // It doesn't seem to matter where this is called.
                Undo.RecordObject(spec, $"Apply {ObjectNames.NicifyVariableName(spectrumModel.ToString())} Spectrum");


                // Descriptions from this very useful paper:
                // https://hal.archives-ouvertes.fr/file/index/docid/307938/filename/frechot_realistic_simulation_of_ocean_surface_using_wave_spectra.pdf

                switch (spectrumModel)
                {
                    case OceanWaveSpectrum.SpectrumModel.PiersonMoskowitz:
                        spec.ApplyPiersonMoskowitzSpectrum();
                        break;
                }
            }

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
            {
                // We need to call this otherwise any property which has HideInInspector won't save.
                EditorUtility.SetDirty(spec);
            }
        }
    }
#endif
}

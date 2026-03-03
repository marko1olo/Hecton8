using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(HectonWaterPhysics))]
[CanEditMultipleObjects]
public class HectonWaterPhysicsEditor : Editor
{
    // ================================================================
    // SERIALIZED PROPERTY REFERENCES
    // ================================================================

    // Material
    SerializedProperty propOceanMaterial;

    // Wave globals
    SerializedProperty propWaveHeight;
    SerializedProperty propWaveSpeed;
    SerializedProperty propWaveChoppiness;

    // Octave 0
    SerializedProperty propWave0Direction;
    SerializedProperty propWave0Amplitude;
    SerializedProperty propWave0Wavelength;
    SerializedProperty propWave0Steepness;

    // Octave 1
    SerializedProperty propWave1Direction;
    SerializedProperty propWave1Amplitude;
    SerializedProperty propWave1Wavelength;
    SerializedProperty propWave1Steepness;

    // Octave 2
    SerializedProperty propWave2Direction;
    SerializedProperty propWave2Amplitude;
    SerializedProperty propWave2Wavelength;
    SerializedProperty propWave2Steepness;

    // Color & depth
    SerializedProperty propShallowColor;
    SerializedProperty propDeepColor;
    SerializedProperty propAbsorptionCoeff;
    SerializedProperty propDepthMaxDistance;
    SerializedProperty propDepthFadeDistance;

    // Foam
    SerializedProperty propFoamColor;
    SerializedProperty propFoamDepthThreshold;
    SerializedProperty propFoamCrestThreshold;
    SerializedProperty propFoamIntensity;
    SerializedProperty propFoamScale;

    // SSS
    SerializedProperty propSSSColor;
    SerializedProperty propSSSIntensity;
    SerializedProperty propSSSPower;
    SerializedProperty propSSSDistortion;

    // Normal maps
    SerializedProperty propNormalStrength;
    SerializedProperty propNormalLayer0Scale;
    SerializedProperty propNormalLayer0SpeedX;
    SerializedProperty propNormalLayer0SpeedY;
    SerializedProperty propNormalLayer0Rotation;
    SerializedProperty propNormalLayer1Scale;
    SerializedProperty propNormalLayer1SpeedX;
    SerializedProperty propNormalLayer1SpeedY;
    SerializedProperty propNormalLayer1Rotation;
    SerializedProperty propNormalLayer2Scale;
    SerializedProperty propNormalLayer2SpeedX;
    SerializedProperty propNormalLayer2SpeedY;
    SerializedProperty propNormalLayer2Rotation;

    // PBR
    SerializedProperty propSmoothness;
    SerializedProperty propMetallic;
    SerializedProperty propFresnelPower;

    // Debug
    SerializedProperty propShowDebugGizmos;
    SerializedProperty propDebugGridSize;
    SerializedProperty propDebugGridSpacing;

    // Foldout states
    private bool foldWaveGlobals  = true;
    private bool foldOctave0      = true;
    private bool foldOctave1      = true;
    private bool foldOctave2      = true;
    private bool foldColor        = true;
    private bool foldFoam         = true;
    private bool foldSSS          = true;
    private bool foldNormals      = true;
    private bool foldNormalLayer0 = true;
    private bool foldNormalLayer1 = true;
    private bool foldNormalLayer2 = true;
    private bool foldPBR          = true;
    private bool foldDebug        = false;

    // ================================================================
    // STYLES
    // ================================================================
    private static GUIStyle _headerStyle;
    private static GUIStyle HeaderStyle
    {
        get
        {
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 13,
                    richText = true
                };
            }
            return _headerStyle;
        }
    }

    private static GUIStyle _sectionStyle;
    private static GUIStyle SectionStyle
    {
        get
        {
            if (_sectionStyle == null)
            {
                _sectionStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    padding = new RectOffset(10, 10, 8, 8),
                    margin  = new RectOffset(0, 0, 4, 4)
                };
            }
            return _sectionStyle;
        }
    }

    private static GUIStyle _syncButtonStyle;
    private static GUIStyle SyncButtonStyle
    {
        get
        {
            if (_syncButtonStyle == null)
            {
                _syncButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize  = 14,
                    fontStyle = FontStyle.Bold,
                    fixedHeight = 40,
                    richText = true
                };
            }
            return _syncButtonStyle;
        }
    }

    // ================================================================
    // ON ENABLE — find all serialized properties
    // ================================================================
    private void OnEnable()
    {
        propOceanMaterial = serializedObject.FindProperty("oceanMaterial");

        propWaveHeight     = serializedObject.FindProperty("waveHeight");
        propWaveSpeed      = serializedObject.FindProperty("waveSpeed");
        propWaveChoppiness = serializedObject.FindProperty("waveChoppiness");

        propWave0Direction = serializedObject.FindProperty("wave0Direction");
        propWave0Amplitude = serializedObject.FindProperty("wave0Amplitude");
        propWave0Wavelength = serializedObject.FindProperty("wave0Wavelength");
        propWave0Steepness = serializedObject.FindProperty("wave0Steepness");

        propWave1Direction = serializedObject.FindProperty("wave1Direction");
        propWave1Amplitude = serializedObject.FindProperty("wave1Amplitude");
        propWave1Wavelength = serializedObject.FindProperty("wave1Wavelength");
        propWave1Steepness = serializedObject.FindProperty("wave1Steepness");

        propWave2Direction = serializedObject.FindProperty("wave2Direction");
        propWave2Amplitude = serializedObject.FindProperty("wave2Amplitude");
        propWave2Wavelength = serializedObject.FindProperty("wave2Wavelength");
        propWave2Steepness = serializedObject.FindProperty("wave2Steepness");

        propShallowColor    = serializedObject.FindProperty("shallowColor");
        propDeepColor       = serializedObject.FindProperty("deepColor");
        propAbsorptionCoeff = serializedObject.FindProperty("absorptionCoeff");
        propDepthMaxDistance = serializedObject.FindProperty("depthMaxDistance");
        propDepthFadeDistance = serializedObject.FindProperty("depthFadeDistance");

        propFoamColor          = serializedObject.FindProperty("foamColor");
        propFoamDepthThreshold = serializedObject.FindProperty("foamDepthThreshold");
        propFoamCrestThreshold = serializedObject.FindProperty("foamCrestThreshold");
        propFoamIntensity      = serializedObject.FindProperty("foamIntensity");
        propFoamScale          = serializedObject.FindProperty("foamScale");

        propSSSColor      = serializedObject.FindProperty("sssColor");
        propSSSIntensity  = serializedObject.FindProperty("sssIntensity");
        propSSSPower      = serializedObject.FindProperty("sssPower");
        propSSSDistortion = serializedObject.FindProperty("sssDistortion");

        propNormalStrength       = serializedObject.FindProperty("normalStrength");
        propNormalLayer0Scale    = serializedObject.FindProperty("normalLayer0Scale");
        propNormalLayer0SpeedX   = serializedObject.FindProperty("normalLayer0SpeedX");
        propNormalLayer0SpeedY   = serializedObject.FindProperty("normalLayer0SpeedY");
        propNormalLayer0Rotation = serializedObject.FindProperty("normalLayer0Rotation");
        propNormalLayer1Scale    = serializedObject.FindProperty("normalLayer1Scale");
        propNormalLayer1SpeedX   = serializedObject.FindProperty("normalLayer1SpeedX");
        propNormalLayer1SpeedY   = serializedObject.FindProperty("normalLayer1SpeedY");
        propNormalLayer1Rotation = serializedObject.FindProperty("normalLayer1Rotation");
        propNormalLayer2Scale    = serializedObject.FindProperty("normalLayer2Scale");
        propNormalLayer2SpeedX   = serializedObject.FindProperty("normalLayer2SpeedX");
        propNormalLayer2SpeedY   = serializedObject.FindProperty("normalLayer2SpeedY");
        propNormalLayer2Rotation = serializedObject.FindProperty("normalLayer2Rotation");

        propSmoothness  = serializedObject.FindProperty("smoothness");
        propMetallic    = serializedObject.FindProperty("metallic");
        propFresnelPower = serializedObject.FindProperty("fresnelPower");

        propShowDebugGizmos = serializedObject.FindProperty("showDebugGizmos");
        propDebugGridSize   = serializedObject.FindProperty("debugGridSize");
        propDebugGridSpacing = serializedObject.FindProperty("debugGridSpacing");
    }

    // ================================================================
    // INSPECTOR GUI
    // ================================================================
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        HectonWaterPhysics script = (HectonWaterPhysics)target;

        // ---- Title ----
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField(
            "<color=#4FC3F7>🌊 HYDRO-X 2.0</color>  <color=#B0BEC5>Ocean System</color>",
            HeaderStyle);
        EditorGUILayout.LabelField(
            "Single Source of Truth — All parameters synced to material",
            EditorStyles.centeredGreyMiniLabel);
        EditorGUILayout.Space(6);

        // ---- Material ----
        DrawSeparator();
        EditorGUILayout.PropertyField(propOceanMaterial,
            new GUIContent("🎨 Ocean Material", "The material using Hecton/HectonOcean_v2 shader."));

        if (propOceanMaterial.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox(
                "Assign a Material using the Hecton/HectonOcean_v2 shader. " +
                "All parameters below will be pushed to it every frame.",
                MessageType.Warning);
        }

        EditorGUILayout.Space(4);

        // ---- WAVE GLOBALS ----
        foldWaveGlobals = DrawFoldoutSection("🌊 Wave Parameters", foldWaveGlobals, () =>
        {
            EditorGUILayout.Slider(propWaveHeight, 0f, 5f,
                new GUIContent("Height", "Global wave height multiplier"));
            EditorGUILayout.Slider(propWaveSpeed, 0f, 5f,
                new GUIContent("Speed", "Global wave animation speed"));
            EditorGUILayout.Slider(propWaveChoppiness, 0f, 2f,
                new GUIContent("Choppiness", "Horizontal displacement intensity (Gerstner Q)"));
        });

        // ---- OCTAVE 0 ----
        foldOctave0 = DrawFoldoutSection("〰️ Wave Octave 0 (Primary)", foldOctave0, () =>
        {
            EditorGUILayout.PropertyField(propWave0Direction, new GUIContent("Direction"));
            EditorGUILayout.PropertyField(propWave0Amplitude, new GUIContent("Amplitude"));
            EditorGUILayout.PropertyField(propWave0Wavelength, new GUIContent("Wavelength"));
            EditorGUILayout.Slider(propWave0Steepness, 0f, 1f, new GUIContent("Steepness"));
        });

        // ---- OCTAVE 1 ----
        foldOctave1 = DrawFoldoutSection("〰️ Wave Octave 1 (Secondary)", foldOctave1, () =>
        {
            EditorGUILayout.PropertyField(propWave1Direction, new GUIContent("Direction"));
            EditorGUILayout.PropertyField(propWave1Amplitude, new GUIContent("Amplitude"));
            EditorGUILayout.PropertyField(propWave1Wavelength, new GUIContent("Wavelength"));
            EditorGUILayout.Slider(propWave1Steepness, 0f, 1f, new GUIContent("Steepness"));
        });

        // ---- OCTAVE 2 ----
        foldOctave2 = DrawFoldoutSection("〰️ Wave Octave 2 (Detail)", foldOctave2, () =>
        {
            EditorGUILayout.PropertyField(propWave2Direction, new GUIContent("Direction"));
            EditorGUILayout.PropertyField(propWave2Amplitude, new GUIContent("Amplitude"));
            EditorGUILayout.PropertyField(propWave2Wavelength, new GUIContent("Wavelength"));
            EditorGUILayout.Slider(propWave2Steepness, 0f, 1f, new GUIContent("Steepness"));
        });

        // ---- COLOR & DEPTH ----
        foldColor = DrawFoldoutSection("🎨 Color & Depth", foldColor, () =>
        {
            EditorGUILayout.PropertyField(propShallowColor, new GUIContent("Shallow Color"));
            EditorGUILayout.PropertyField(propDeepColor, new GUIContent("Deep Color"));
            EditorGUILayout.Slider(propAbsorptionCoeff, 0.01f, 2f,
                new GUIContent("Absorption", "Beer's law absorption coefficient"));
            EditorGUILayout.Slider(propDepthMaxDistance, 0.1f, 50f,
                new GUIContent("Max Depth Distance"));
            EditorGUILayout.Slider(propDepthFadeDistance, 0.01f, 5f,
                new GUIContent("Shoreline Softness", "Depth fade for soft intersection"));
        });

        // ---- FOAM ----
        foldFoam = DrawFoldoutSection("🫧 Foam", foldFoam, () =>
        {
            EditorGUILayout.PropertyField(propFoamColor, new GUIContent("Color"));
            EditorGUILayout.Slider(propFoamDepthThreshold, 0f, 3f,
                new GUIContent("Shore Threshold", "Depth threshold for intersection foam"));
            EditorGUILayout.Slider(propFoamCrestThreshold, 0f, 2f,
                new GUIContent("Crest Threshold", "Height threshold for crest foam"));
            EditorGUILayout.Slider(propFoamIntensity, 0f, 3f,
                new GUIContent("Intensity"));
            EditorGUILayout.Slider(propFoamScale, 0.1f, 20f,
                new GUIContent("UV Scale", "Foam texture tiling scale"));
        });

        // ---- SSS ----
        foldSSS = DrawFoldoutSection("💡 Subsurface Scattering", foldSSS, () =>
        {
            EditorGUILayout.PropertyField(propSSSColor, new GUIContent("Color"));
            EditorGUILayout.Slider(propSSSIntensity, 0f, 5f, new GUIContent("Intensity"));
            EditorGUILayout.Slider(propSSSPower, 1f, 16f,
                new GUIContent("Falloff Power", "Higher = tighter SSS highlight"));
            EditorGUILayout.Slider(propSSSDistortion, 0f, 1f,
                new GUIContent("Normal Distortion", "How much the surface normal bends the SSS direction"));
        });

        // ---- NORMAL MAPS ----
        foldNormals = DrawFoldoutSection("🗺️ Normal Maps — Anti-Tiling", foldNormals, () =>
        {
            EditorGUILayout.Slider(propNormalStrength, 0f, 2f,
                new GUIContent("Global Strength"));
            EditorGUILayout.Space(2);

            // Layer 0
            foldNormalLayer0 = EditorGUILayout.Foldout(foldNormalLayer0, "Layer 0 (Large / Slow)", true);
            if (foldNormalLayer0)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(propNormalLayer0Scale, new GUIContent("Scale"));
                EditorGUILayout.PropertyField(propNormalLayer0SpeedX, new GUIContent("Speed X"));
                EditorGUILayout.PropertyField(propNormalLayer0SpeedY, new GUIContent("Speed Y"));
                EditorGUILayout.PropertyField(propNormalLayer0Rotation, new GUIContent("Rotation °"));
                EditorGUI.indentLevel--;
            }

            // Layer 1
            foldNormalLayer1 = EditorGUILayout.Foldout(foldNormalLayer1, "Layer 1 (Medium)", true);
            if (foldNormalLayer1)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(propNormalLayer1Scale, new GUIContent("Scale"));
                EditorGUILayout.PropertyField(propNormalLayer1SpeedX, new GUIContent("Speed X"));
                EditorGUILayout.PropertyField(propNormalLayer1SpeedY, new GUIContent("Speed Y"));
                EditorGUILayout.PropertyField(propNormalLayer1Rotation, new GUIContent("Rotation °"));
                EditorGUI.indentLevel--;
            }

            // Layer 2
            foldNormalLayer2 = EditorGUILayout.Foldout(foldNormalLayer2, "Layer 2 (Micro Detail)", true);
            if (foldNormalLayer2)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(propNormalLayer2Scale, new GUIContent("Scale"));
                EditorGUILayout.PropertyField(propNormalLayer2SpeedX, new GUIContent("Speed X"));
                EditorGUILayout.PropertyField(propNormalLayer2SpeedY, new GUIContent("Speed Y"));
                EditorGUILayout.PropertyField(propNormalLayer2Rotation, new GUIContent("Rotation °"));
                EditorGUI.indentLevel--;
            }
        });

        // ---- PBR ----
        foldPBR = DrawFoldoutSection("✨ PBR Surface", foldPBR, () =>
        {
            EditorGUILayout.Slider(propSmoothness, 0f, 1f, new GUIContent("Smoothness"));
            EditorGUILayout.Slider(propMetallic, 0f, 1f, new GUIContent("Metallic"));
            EditorGUILayout.Slider(propFresnelPower, 1f, 10f, new GUIContent("Fresnel Power"));
        });

        // ---- DEBUG ----
        foldDebug = DrawFoldoutSection("🐛 Debug", foldDebug, () =>
        {
            EditorGUILayout.PropertyField(propShowDebugGizmos, new GUIContent("Show Gizmos"));
            if (propShowDebugGizmos.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.IntSlider(propDebugGridSize, 5, 50, new GUIContent("Grid Size"));
                EditorGUILayout.Slider(propDebugGridSpacing, 0.5f, 5f, new GUIContent("Grid Spacing"));
                EditorGUI.indentLevel--;
            }
        });

        // ---- SYNC BUTTON ----
        EditorGUILayout.Space(8);
        DrawSeparator();
        EditorGUILayout.Space(4);

        Color prevBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.2f, 0.7f, 1f, 1f);

        if (GUILayout.Button("⚡  Apply & Sync to Material  ⚡", SyncButtonStyle))
        {
            serializedObject.ApplyModifiedProperties();
            foreach (Object t in targets)
            {
                HectonWaterPhysics hwp = t as HectonWaterPhysics;
                if (hwp != null)
                {
                    hwp.SyncAllToMaterial();
                    EditorUtility.SetDirty(hwp);
                    if (hwp.OceanMaterial != null)
                        EditorUtility.SetDirty(hwp.OceanMaterial);
                }
            }
            Debug.Log("[Hydro-X 2.0] ✅ All parameters synced to material.");
        }

        GUI.backgroundColor = prevBg;

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField(
            "Sync happens automatically in Update(). Button forces an immediate push.",
            EditorStyles.centeredGreyMiniLabel);
        EditorGUILayout.Space(4);

        serializedObject.ApplyModifiedProperties();
    }

    // ================================================================
    // HELPER: Foldout Section
    // ================================================================
    private bool DrawFoldoutSection(string title, bool foldState, System.Action drawContent)
    {
        EditorGUILayout.Space(2);
        foldState = EditorGUILayout.Foldout(foldState, title, true, EditorStyles.foldoutHeader);

        if (foldState)
        {
            EditorGUILayout.BeginVertical(SectionStyle);
            drawContent?.Invoke();
            EditorGUILayout.EndVertical();
        }

        return foldState;
    }

    // ================================================================
    // HELPER: Separator Line
    // ================================================================
    private void DrawSeparator()
    {
        EditorGUILayout.Space(2);
        Rect rect = EditorGUILayout.GetControlRect(false, 1);
        rect.height = 1;
        EditorGUI.DrawRect(rect, new Color(0.35f, 0.35f, 0.35f, 1f));
        EditorGUILayout.Space(2);
    }
}
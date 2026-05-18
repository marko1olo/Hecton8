#if UNITY_EDITOR
using Hecton8.Core.Memory;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Core.Editor
{
    public sealed class InputCurveHapticsTunerWindow : EditorWindow
    {
        private const float GridSize = 180f;

        [MenuItem("HECTON-8/Input Curve & Haptics Tuner")]
        private static void Open()
        {
            GetWindow<InputCurveHapticsTunerWindow>("Input Curve & Haptics Tuner");
        }

        private void OnEnable()
        {
            EditorApplication.update += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
        }

        private void OnGUI()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (!Application.isPlaying || vault == null)
            {
                EditorGUILayout.LabelField("Play Mode and GlobalDataVault required.");
                return;
            }

            VaultBufferHandle<InputProfileDTO> profileHandle = vault.GetBufferHandle<InputProfileDTO>(
                BufferID.ShinobuInputProfile,
                1,
                SystemID.CoreDeterminism,
                NativeArrayOptions.UninitializedMemory);
            VaultBufferHandle<InputStateDTO> inputHandle = vault.GetBufferHandle<InputStateDTO>(
                BufferID.ShinobuInputCurrentDto,
                1,
                SystemID.CoreDeterminism,
                NativeArrayOptions.UninitializedMemory);

            if (!profileHandle.IsCreated || !inputHandle.IsCreated)
            {
                EditorGUILayout.LabelField("Input deterministic buffers are not ready.");
                return;
            }

            InputProfileDTO profile = profileHandle.GetElementAsReadOnlyRef(vault, 0);
            EditorGUI.BeginChangeCheck();
            profile.InnerDeadzone = EditorGUILayout.Slider("Analog Inner Deadzone", profile.InnerDeadzone, 0f, 0.95f);
            profile.OuterDeadzone = EditorGUILayout.Slider("Analog Outer Deadzone", profile.OuterDeadzone, profile.InnerDeadzone + 0.0001f, 1f);
            profile.MoveExponent = EditorGUILayout.Slider("Analog Exponent", profile.MoveExponent, 0.25f, 4f);
            profile.MouseSensitivity = EditorGUILayout.Slider("Mouse Sensitivity", profile.MouseSensitivity, 0.01f, 20f);
            profile.MouseAcceleration = EditorGUILayout.Slider("Mouse Acceleration", profile.MouseAcceleration, 0f, 8f);
            profile.HapticPowerScale = EditorGUILayout.Slider("Haptic Power", profile.HapticPowerScale, 0f, 2f);
            profile.HapticThermalAmplitudeScale = EditorGUILayout.Slider("Thermal Haptic Scale", profile.HapticThermalAmplitudeScale, 0f, 1f);
            bool mockCollision = (profile.Flags & 1u) != 0u;
            mockCollision = EditorGUILayout.Toggle("Mock Collision Pulse", mockCollision);
            profile.Flags = mockCollision ? profile.Flags | 1u : profile.Flags & ~1u;
            if (EditorGUI.EndChangeCheck())
                profileHandle.GetElementAsRef(vault, 0) = profile;

            Rect curveRect = GUILayoutUtility.GetRect(position.width - 20f, 120f);
            DrawCurvePreview(curveRect, profile);

            InputStateDTO state = inputHandle.GetElementAsReadOnlyRef(vault, 0);
            Rect oscilloscopeRect = GUILayoutUtility.GetRect(GridSize, GridSize);
            DrawOscilloscope(oscilloscopeRect, profile, state);
        }

        private static void DrawCurvePreview(Rect rect, InputProfileDTO profile)
        {
            EditorGUI.DrawRect(rect, new Color(0.08f, 0.08f, 0.08f, 1f));
            Handles.BeginGUI();
            Handles.color = Color.yellow;
            Vector3 previous = default;
            for (int i = 0; i <= 64; i++)
            {
                float x = i / 64f;
                float normalized = Mathf.Clamp01((x - profile.InnerDeadzone) / Mathf.Max(profile.OuterDeadzone - profile.InnerDeadzone, 0.0001f));
                float y = Mathf.Pow(normalized, Mathf.Clamp(profile.MoveExponent, 0.25f, 4f));
                Vector3 point = new Vector3(rect.xMin + (x * rect.width), rect.yMax - (y * rect.height), 0f);
                if (i > 0)
                    Handles.DrawLine(previous, point);
                previous = point;
            }

            Handles.color = Color.cyan;
            float hapticY = rect.yMax - Mathf.Clamp01(profile.HapticPowerScale * 0.5f) * rect.height;
            Handles.DrawLine(new Vector3(rect.xMin, hapticY, 0f), new Vector3(rect.xMax, hapticY, 0f));
            Handles.EndGUI();
        }

        private static void DrawOscilloscope(Rect rect, InputProfileDTO profile, InputStateDTO state)
        {
            EditorGUI.DrawRect(rect, new Color(0.04f, 0.04f, 0.04f, 1f));
            Handles.BeginGUI();
            Vector2 center = rect.center;
            Handles.color = new Color(0.25f, 0.25f, 0.25f, 1f);
            Handles.DrawLine(new Vector3(rect.xMin, center.y, 0f), new Vector3(rect.xMax, center.y, 0f));
            Handles.DrawLine(new Vector3(center.x, rect.yMin, 0f), new Vector3(center.x, rect.yMax, 0f));
            Handles.color = Color.yellow;
            float radius = Mathf.Clamp01(profile.InnerDeadzone) * rect.width * 0.5f;
            Handles.DrawWireDisc(center, Vector3.forward, radius);
            Handles.color = Color.red;
            Vector2 dot = center + new Vector2(state.MoveAxis.x, -state.MoveAxis.y) * (rect.width * 0.5f);
            Handles.DrawSolidDisc(dot, Vector3.forward, 4f);
            Handles.EndGUI();
        }
    }
}
#endif

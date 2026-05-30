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

            VaultGenerationHandle<InputProfileDTO> profileHandle = vault.EnsureGenerationHandle<InputProfileDTO>(
                BufferID.ShinobuInputProfile,
                1,
                SystemID.CoreDeterminism,
                NativeArrayOptions.UninitializedMemory);
            VaultGenerationHandle<InputStateDTO> inputHandle = vault.EnsureGenerationHandle<InputStateDTO>(
                BufferID.ShinobuInputCurrentDto,
                1,
                SystemID.CoreDeterminism,
                NativeArrayOptions.UninitializedMemory);

            if (profileHandle.BufferID == 0u ||
                inputHandle.BufferID == 0u ||
                !vault.TryReadOnlyHandle(in profileHandle, out NativeArray<InputProfileDTO>.ReadOnly profileBuffer) ||
                !vault.TryReadOnlyHandle(in inputHandle, out NativeArray<InputStateDTO>.ReadOnly inputBuffer) ||
                profileBuffer.Length <= 0 ||
                inputBuffer.Length <= 0)
            {
                EditorGUILayout.LabelField("Input deterministic buffers are not ready.");
                return;
            }

            InputProfileDTO profile = profileBuffer[0];
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
                WriteProfile(vault, in profileHandle, in profile);

            Rect curveRect = GUILayoutUtility.GetRect(position.width - 20f, 120f);
            DrawCurvePreview(curveRect, profile);

            InputStateDTO state = inputBuffer[0];
            Rect oscilloscopeRect = GUILayoutUtility.GetRect(GridSize, GridSize);
            DrawOscilloscope(oscilloscopeRect, profile, state);
        }

        private static void WriteProfile(
            IDataVault vault,
            in VaultGenerationHandle<InputProfileDTO> profileHandle,
            in InputProfileDTO profile)
        {
            if (vault == null ||
                !vault.TryAcquireWriteLock(in profileHandle, SystemID.CoreDeterminism, out NativeArray<InputProfileDTO> profileBuffer))
            {
                return;
            }

            try
            {
                if (profileBuffer.IsCreated && profileBuffer.Length > 0)
                    profileBuffer[0] = profile;
            }
            finally
            {
                vault.ReleaseWriteLock(in profileHandle, SystemID.CoreDeterminism);
            }
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
                float y = PreviewPower01(normalized, Mathf.Clamp(profile.MoveExponent, 0.25f, 4f));
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

        private static float PreviewPower01(float value, float exponent)
        {
            float x = Mathf.Clamp01(value);
            float x2 = x * x;
            float x4 = x2 * x2;
            float sqrt = x / Mathf.Sqrt(Mathf.Max(x, 0.000001f));
            float low = Mathf.Lerp(sqrt, x, Mathf.Clamp01((exponent - 0.25f) / 0.75f));
            float high = Mathf.Lerp(x2, x4, Mathf.Clamp01((exponent - 2f) * 0.5f));
            return exponent < 1f
                ? Mathf.Clamp01(low)
                : Mathf.Clamp01(Mathf.Lerp(x, high, Mathf.Clamp01((exponent - 1f) / 3f)));
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

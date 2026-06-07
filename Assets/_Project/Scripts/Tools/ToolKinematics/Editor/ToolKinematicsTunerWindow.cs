#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Tools.ToolKinematics;
using Hecton8.Tools.ToolKinematics.Contracts;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Tools.ToolKinematics.Editor
{
    public sealed class ToolKinematicsTunerWindow : EditorWindow
    {
        private IDataVault _dataVault;
        private VaultGenerationHandle<ToolKinematicsTuningDTO> _tuningHandle;
        private VaultGenerationHandle<ToolStateDTO> _statesHandle;
        private VaultGenerationHandle<ToolKinematicsFrameInputDTO> _frameInputsHandle;
        private VaultGenerationHandle<ToolHitResultDTO> _hitResultsHandle;
        private VaultGenerationHandle<ToolPoseOutputDTO> _poseOutputsHandle;
        private VaultGenerationHandle<ToolBeamVertexDTO> _beamVerticesHandle;
        private VaultGenerationHandle<int> _beamVertexCountsHandle;

        [MenuItem("Hecton8/Tools/Tool Kinematics Tuner")]
        private static void Open()
        {
            GetWindow<ToolKinematicsTunerWindow>("Tool Kinematics");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= DrawRaymarchGizmos;
            SceneView.duringSceneGui += DrawRaymarchGizmos;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawRaymarchGizmos;
            ReleaseVaultHandles();
            ClearHandles();
        }

        private void OnGUI()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
            {
                EditorGUILayout.HelpBox("GlobalDataVault is not registered.", MessageType.Warning);
                return;
            }

            BindVault(vault);
            if (!TryResolveEditorVaultView(vault, ref _tuningHandle, BufferID.ToolKinematicsTuning, 1, out NativeArray<ToolKinematicsTuningDTO> tuning))
            {
                EditorGUILayout.HelpBox("Tool kinematics tuning buffer is unavailable.", MessageType.Error);
                return;
            }

            ToolKinematicsTuningDTO dto = tuning[0];
            if (dto.MaxHeat <= 0f)
                dto = ToolKinematicsMath.DefaultTuning();

            EditorGUI.BeginChangeCheck();
            dto.LaserRange = EditorGUILayout.Slider("Laser Range", dto.LaserRange, 0.1f, 60f);
            dto.HeatRampRate = EditorGUILayout.Slider("Heat Ramp Rate", dto.HeatRampRate, 0f, 8f);
            dto.CoolingRate = EditorGUILayout.Slider("Cooling Rate", dto.CoolingRate, 0f, 8f);
            dto.MaxHeat = EditorGUILayout.Slider("Max Heat", dto.MaxHeat, 0.1f, 4f);
            dto.EnergyDrainRate = EditorGUILayout.Slider("Energy Drain Rate", dto.EnergyDrainRate, 0f, 4f);
            dto.RecoilStrength = EditorGUILayout.Slider("Recoil Strength", dto.RecoilStrength, 0f, 2f);
            dto.SpringDamping = EditorGUILayout.Slider("Spring Damping", dto.SpringDamping, 0f, 64f);
            dto.CollisionSpring = EditorGUILayout.Slider("Collision Spring", dto.CollisionSpring, 0f, 8f);
            dto.BeamRadius = EditorGUILayout.Slider("Beam Radius", dto.BeamRadius, 0.002f, 0.12f);
            dto.SystemHealthIndex = EditorGUILayout.Slider("System Health Index", dto.SystemHealthIndex, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                dto.Flags = 0u;
                dto._pad0 = 0u;
                tuning[0] = dto;
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space();
            DrawReadOnlyRuntimeState(vault);
        }

        private void DrawReadOnlyRuntimeState(IDataVault vault)
        {
            if (!TryResolveEditorVaultView(vault, ref _statesHandle, BufferID.ToolKinematicsStates, ToolKinematicsRuntime.MaxToolCapacity, out NativeArray<ToolStateDTO> states) ||
                !TryResolveEditorVaultView(vault, ref _hitResultsHandle, BufferID.ToolKinematicsHitResults, ToolKinematicsRuntime.MaxToolCapacity, out NativeArray<ToolHitResultDTO> hits))
            {
                EditorGUILayout.HelpBox("Runtime state buffers are not seeded yet.", MessageType.Info);
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                for (int i = 0; i < math.min(states.Length, ToolKinematicsRuntime.MaxToolCapacity); i++)
                {
                    ToolStateDTO state = states[i];
                    if (state.ToolTypeHash == 0u)
                        continue;

                    ToolHitResultDTO hit = hits[i];
                    EditorGUILayout.LabelField(
                        "Slot " + i,
                        "tool=0x" + state.ToolTypeHash.ToString("X8") +
                        " heat=" + state.HeatLevel.ToString("0.000") +
                        " energy=" + state.EnergyRemaining.ToString("0.000") +
                        " hit=" + hit.Distance.ToString("0.00"));
                }
            }
        }

        private void DrawRaymarchGizmos(SceneView sceneView)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return;

            BindVault(vault);
            if (!TryResolveEditorVaultView(vault, ref _statesHandle, BufferID.ToolKinematicsStates, ToolKinematicsRuntime.MaxToolCapacity, out NativeArray<ToolStateDTO> states) ||
                !TryResolveEditorVaultView(vault, ref _frameInputsHandle, BufferID.ToolKinematicsFrameInputs, ToolKinematicsRuntime.MaxToolCapacity, out NativeArray<ToolKinematicsFrameInputDTO> frameInputs) ||
                !TryResolveEditorVaultView(vault, ref _hitResultsHandle, BufferID.ToolKinematicsHitResults, ToolKinematicsRuntime.MaxToolCapacity, out NativeArray<ToolHitResultDTO> hits) ||
                !TryResolveEditorVaultView(vault, ref _poseOutputsHandle, BufferID.ToolKinematicsPoseOutputs, ToolKinematicsRuntime.MaxToolCapacity, out NativeArray<ToolPoseOutputDTO> poseOutputs) ||
                !TryResolveEditorVaultView(vault, ref _beamVerticesHandle, BufferID.ToolKinematicsBeamVertices, ToolKinematicsRuntime.MaxToolCapacity * ToolKinematicsRuntime.BeamVerticesPerTool, out NativeArray<ToolBeamVertexDTO> beamVertices) ||
                !TryResolveEditorVaultView(vault, ref _beamVertexCountsHandle, BufferID.ToolKinematicsBeamVertexCounts, ToolKinematicsRuntime.MaxToolCapacity, out NativeArray<int> beamCounts))
            {
                return;
            }

            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            for (int i = 0; i < math.min(states.Length, ToolKinematicsRuntime.MaxToolCapacity); i++)
            {
                ToolStateDTO state = states[i];
                if (state.ToolTypeHash == 0u)
                    continue;

                float3 start = ResolveGizmoStart(i, state, frameInputs, poseOutputs);
                ToolHitResultDTO hit = hits[i];
                DrawToolRay(start, hit);
                DrawBeamTube(i, beamVertices, beamCounts);
            }
        }

        private static float3 ResolveGizmoStart(
            int slot,
            in ToolStateDTO state,
            NativeArray<ToolKinematicsFrameInputDTO> frameInputs,
            NativeArray<ToolPoseOutputDTO> poseOutputs)
        {
            if ((uint)slot < (uint)poseOutputs.Length)
            {
                ToolPoseOutputDTO pose = poseOutputs[slot];
                float3 local = new float3(pose.MatrixColumn3.x, pose.MatrixColumn3.y, pose.MatrixColumn3.z);
                if (math.all(math.isfinite(local)))
                    return local + state.Forward * 0.28f;
            }

            if ((uint)slot < (uint)frameInputs.Length)
                return frameInputs[slot].ControllerLocalPosition + state.Forward * 0.28f;

            return state.Forward * 0.28f;
        }

        private static void DrawToolRay(float3 start, in ToolHitResultDTO hit)
        {
            Vector3 startVector = ToVector3(start);
            Vector3 hitVector = ToVector3(hit.HitPoint);
            Handles.color = hit.MaterialHash != 0u ? Color.cyan : Color.gray;
            Handles.DrawLine(startVector, hitVector, 2f);
            if (hit.MaterialHash == 0u)
                return;

            Handles.color = Color.red;
            Handles.DrawLine(hitVector, hitVector + ToVector3(hit.Normal) * 0.25f, 2f);
            Handles.SphereHandleCap(0, hitVector, Quaternion.identity, 0.045f, EventType.Repaint);
        }

        private static void DrawBeamTube(int slot, NativeArray<ToolBeamVertexDTO> vertices, NativeArray<int> counts)
        {
            if ((uint)slot >= (uint)counts.Length)
                return;

            int count = math.clamp(counts[slot], 0, ToolKinematicsRuntime.BeamVerticesPerTool);
            if (count <= 1)
                return;

            int start = slot * ToolKinematicsRuntime.BeamVerticesPerTool;
            if (start < 0 || start + count > vertices.Length)
                return;

            Handles.color = new Color(0.15f, 0.85f, 1f, 0.55f);
            for (int i = 1; i < count; i++)
                Handles.DrawLine(ToVector3(vertices[start + i - 1].Position), ToVector3(vertices[start + i].Position), 1f);
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private void BindVault(IDataVault vault)
        {
            if (ReferenceEquals(_dataVault, vault))
                return;

            ReleaseVaultHandles();
            ClearHandles();
            _dataVault = vault;
        }

        private static bool IsHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private static bool TryResolveEditorVaultView<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0)
                return false;

            if (IsHandleCreated(in handle) &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            VaultGenerationHandle<T> acquired = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, SystemID.GameplayTools, NativeArrayOptions.ClearMemory);
            if (!IsHandleCreated(in acquired) ||
                !vault.TryResolveHandle(in acquired, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                return false;
            }

            handle = acquired;
            return true;
        }

        private void ReleaseVaultHandles()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            ReleaseVaultHandle(vault, ref _tuningHandle);
            ReleaseVaultHandle(vault, ref _statesHandle);
            ReleaseVaultHandle(vault, ref _frameInputsHandle);
            ReleaseVaultHandle(vault, ref _hitResultsHandle);
            ReleaseVaultHandle(vault, ref _poseOutputsHandle);
            ReleaseVaultHandle(vault, ref _beamVerticesHandle);
            ReleaseVaultHandle(vault, ref _beamVertexCountsHandle);
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (!IsHandleCreated(in handle))
                return;

            vault.ReleaseBuffer(in handle);
            handle = default;
        }

        private void ClearHandles()
        {
            _tuningHandle = default;
            _statesHandle = default;
            _frameInputsHandle = default;
            _hitResultsHandle = default;
            _poseOutputsHandle = default;
            _beamVerticesHandle = default;
            _beamVertexCountsHandle = default;
            _dataVault = null;
        }
    }
}
#endif

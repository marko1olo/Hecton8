using Hecton8.Core;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Construction
{
    [DisallowMultipleComponent]
    public sealed class HectonBlueprintPreviewBatch : MonoBehaviour, IRenderable, ILateFrameTickable
    {
        private const string NativeMemoryOwner = nameof(HectonBlueprintPreviewBatch);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;
        private const string WireShaderPath = "Assets/_Project/Art/Shaders/Hecton_BlueprintWireInstanced.shader";

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct BuildPreviewMatricesJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<BlueprintPreviewInstance> Instances;
            public NativeArray<Matrix4x4> Matrices;
            public float TimeSeconds;
            public int Count;

            public void Execute(int index)
            {
                if ((uint)index >= (uint)Count)
                    return;

                BlueprintPreviewInstance instance = Instances[index];
                float requirement01 = (instance.OwnedMask & instance.RequirementMask) == instance.RequirementMask ? 1f : 0f;
                float flickerPhase = math.frac((TimeSeconds * 17.0f) + (index * 0.38196602f));
                float flicker = 1f - math.abs((flickerPhase * 2f) - 1f);
                float scaleMul = math.lerp(0.88f, 1.0f, requirement01) + ((flicker - 0.5f) * 0.018f * instance.FlickerAmplitude);
                float bob = (math.frac((TimeSeconds * instance.BobFrequency) + (index * 0.173f)) * 2f) - 1f;
                float3 position = instance.Position + new float3(0f, bob * instance.BobAmplitude, 0f);
                quaternion yaw = quaternion.AxisAngle(new float3(0f, 1f, 0f), TimeSeconds * instance.SpinRadiansPerSecond);
                float4x4 trs = float4x4.TRS(position, math.mul(instance.Rotation, yaw), instance.Scale * math.max(0.001f, scaleMul));
                Matrices[index] = ToMatrix4x4(in trs);
            }

            private static Matrix4x4 ToMatrix4x4(in float4x4 matrix)
            {
                Matrix4x4 result;
                result.m00 = matrix.c0.x;
                result.m10 = matrix.c0.y;
                result.m20 = matrix.c0.z;
                result.m30 = matrix.c0.w;
                result.m01 = matrix.c1.x;
                result.m11 = matrix.c1.y;
                result.m21 = matrix.c1.z;
                result.m31 = matrix.c1.w;
                result.m02 = matrix.c2.x;
                result.m12 = matrix.c2.y;
                result.m22 = matrix.c2.z;
                result.m32 = matrix.c2.w;
                result.m03 = matrix.c3.x;
                result.m13 = matrix.c3.y;
                result.m23 = matrix.c3.z;
                result.m33 = matrix.c3.w;
                return result;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BlueprintPreviewInstance
        {
            public float3 Position;
            public quaternion Rotation;
            public float3 Scale;
            public uint RequirementMask;
            public uint OwnedMask;
            public float BobAmplitude;
            public float BobFrequency;
            public float SpinRadiansPerSecond;
            public float FlickerAmplitude;
        }

        [SerializeField] private Mesh previewMesh;
        [SerializeField] private Material previewMaterial;
        [SerializeField] private Shader previewShader;
        [SerializeField] private Camera targetCamera;
        [SerializeField, Min(1)] private int capacity = 128;
        [SerializeField] private Color validColor = new Color(0.08f, 1f, 0.72f, 0.72f);

        private NativeArray<BlueprintPreviewInstance> _instances;
        private NativeArray<Matrix4x4> _matrices;
        private Matrix4x4[] _matrixMirror;
        private JobHandle _buildHandle;
        private JobHandle _disposeHandle;
        private bool _buildScheduled;
        private bool _registeredRenderable;
        private bool _registeredLateFrame;
        private int _activeCount;
        private int _scheduledCount;
        private int _drawCount;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            EnsureBuffers();
            EnsureMaterial();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            GlobalRegistry.Renderables.Register(this);
            _registeredRenderable = GlobalRegistry.Renderables.Contains(this);
            GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.UI);
            _registeredLateFrame = true;
        }

        private void OnDisable()
        {
            if (_registeredRenderable)
            {
                GlobalRegistry.Renderables.Unregister(this);
                _registeredRenderable = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _registeredLateFrame = false;
            }
        }

        private void OnDestroy()
        {
            JobHandle disposeDependency = _buildScheduled ? _buildHandle : default;
            _buildScheduled = false;
            _scheduledCount = 0;
            _drawCount = 0;

            if (_instances.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_instances);
                _disposeHandle = JobHandle.CombineDependencies(_disposeHandle, _instances.Dispose(disposeDependency));
                _instances = default;
            }

            if (_matrices.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_matrices);
                _disposeHandle = JobHandle.CombineDependencies(_disposeHandle, _matrices.Dispose(disposeDependency));
                _matrices = default;
            }

            _disposeHandle.Complete(); // COLD SYNC: teardown must own scheduled NativeArray disposal before sentinel handoff ends.

            if (previewMaterial != null && previewMaterial.hideFlags == HideFlags.DontSave)
                Destroy(previewMaterial);
        }

        public void Render(float deltaTime)
        {
            DrawPreparedBatch();
            ScheduleNextBuild();
        }

        public void LateFrameTick()
        {
            CompleteReadyBuild();
        }

        public bool SetPreview(int index, Vector3 position, Quaternion rotation, Vector3 scale, uint requirementMask, uint ownedMask)
        {
            EnsureBuffers();
            if ((uint)index >= (uint)_instances.Length)
                return false;

            _instances[index] = new BlueprintPreviewInstance
            {
                Position = (float3)position,
                Rotation = new quaternion(rotation.x, rotation.y, rotation.z, rotation.w),
                Scale = (float3)scale,
                RequirementMask = requirementMask,
                OwnedMask = ownedMask,
                BobAmplitude = 0.025f,
                BobFrequency = 1.35f,
                SpinRadiansPerSecond = 0.22f,
                FlickerAmplitude = 1f
            };
            _activeCount = math.max(_activeCount, index + 1);
            return true;
        }

        public void SetActivePreviewCount(int count)
        {
            EnsureBuffers();
            _activeCount = math.clamp(count, 0, _instances.Length);
            if (_drawCount > _activeCount)
                _drawCount = _activeCount;
        }

        public void ClearPreviews()
        {
            _activeCount = 0;
            _drawCount = 0;
        }

        private void CompleteReadyBuild()
        {
            if (!_buildScheduled || !_buildHandle.IsCompleted)
                return;

            _buildHandle.Complete();
            _buildScheduled = false;
            _drawCount = math.min(_scheduledCount, _matrixMirror != null ? _matrixMirror.Length : 0);
            for (int i = 0; i < _drawCount; i++)
                _matrixMirror[i] = _matrices[i];
        }

        private void DrawPreparedBatch()
        {
            if (_drawCount <= 0 || previewMesh == null || previewMaterial == null || _matrixMirror == null)
                return;

            if (previewMaterial.HasProperty(BaseColorId))
                previewMaterial.SetColor(BaseColorId, validColor);

            Graphics.DrawMeshInstanced(
                previewMesh,
                0,
                previewMaterial,
                _matrixMirror,
                _drawCount,
                null,
                ShadowCastingMode.Off,
                false,
                0,
                targetCamera,
                LightProbeUsage.Off,
                null);
        }

        private void ScheduleNextBuild()
        {
            if (_buildScheduled || _activeCount <= 0 || !_instances.IsCreated || !_matrices.IsCreated)
                return;

            _scheduledCount = math.min(_activeCount, _instances.Length);
            BuildPreviewMatricesJob job = new BuildPreviewMatricesJob
            {
                Instances = _instances,
                Matrices = _matrices,
                TimeSeconds = Time.time,
                Count = _scheduledCount
            };
            _buildHandle = job.Schedule(_scheduledCount, 32);
            _buildScheduled = true;
        }

        private void EnsureBuffers()
        {
            if (_instances.IsCreated && _matrices.IsCreated && _matrixMirror != null)
                return;

            int resolvedCapacity = math.max(1, capacity);
            if (!_instances.IsCreated)
            {
                _instances = new NativeArray<BlueprintPreviewInstance>(resolvedCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<BlueprintPreviewInstance>[capacity] - blueprint data bitmask buffer - owner: HectonBlueprintPreviewBatch
                NativeMemorySentinel.RegisterNativeArray(_instances, NativeMemoryOwner, nameof(_instances), NativeMemoryLifetime);
            }

            if (!_matrices.IsCreated)
            {
                _matrices = new NativeArray<Matrix4x4>(resolvedCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<Matrix4x4>[capacity] - Burst-built preview matrices - owner: HectonBlueprintPreviewBatch
                NativeMemorySentinel.RegisterNativeArray(_matrices, NativeMemoryOwner, nameof(_matrices), NativeMemoryLifetime);
            }

            if (_matrixMirror == null)
                _matrixMirror = new Matrix4x4[resolvedCapacity]; // COLD ALLOC: Matrix4x4[capacity] - DrawMeshInstanced managed mirror - owner: HectonBlueprintPreviewBatch
        }

        private void EnsureMaterial()
        {
            if (previewMaterial != null)
                return;

#if UNITY_EDITOR
            if (previewShader == null)
                previewShader = AssetDatabase.LoadAssetAtPath<Shader>(WireShaderPath);
#endif

            if (previewShader == null)
                return;

            previewMaterial = new Material(previewShader)
            {
                enableInstancing = true,
                hideFlags = HideFlags.DontSave
            };
        }
    }
}

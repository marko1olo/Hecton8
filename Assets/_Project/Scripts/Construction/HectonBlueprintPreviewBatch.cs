using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Core.Memory.Layout;
using Hecton8.World;
using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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
        private const string WireShaderPath = "Assets/_Project/Art/Shaders/Hecton_BlueprintWireInstanced.shader";
        private const int MaxDrawMeshInstancedBatch = 1023;

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct BuildPreviewMatricesJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<BlueprintPreviewInstance> Instances;
            [NoAlias] public NativeArray<Matrix4x4> Matrices;
            public float TimeSeconds;
            public int Count;

            public void Execute(int index)
            {
                if ((uint)index >= (uint)Count)
                    return;

                BlueprintPreviewInstance instance = Instances[index];
                float requirement01 = (instance.OwnedMask & instance.RequirementMask) == instance.RequirementMask ? 1f : 0f;
                float flickerPhase = (TimeSeconds * 17.0f) + (index * 0.38196602f);
                float flicker = CinematicMath.FastTriangleWave01(flickerPhase);
                float scaleMul = math.lerp(0.88f, 1.0f, requirement01) + ((flicker - 0.5f) * 0.018f * instance.FlickerAmplitude);
                float bobPhase = math.frac((TimeSeconds * instance.BobFrequency) + (index * 0.173f));
                float bob = CinematicMath.FastTriangleWaveSigned(bobPhase);
                float3 position = instance.Position + new float3(0f, bob * instance.BobAmplitude, 0f);
                quaternion yaw = CinematicMath.FastYawQuaternion(TimeSeconds * instance.SpinRadiansPerSecond);
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

        [BinaryBlittableSafe]
        [StructLayout(LayoutKind.Explicit, Size = 64)]
        public struct BlueprintPreviewInstance
        {
            [FieldOffset(0)] public quaternion Rotation;
            [FieldOffset(16)] public float3 Position;
            [FieldOffset(28)] public float3 Scale;
            [FieldOffset(40)] public uint RequirementMask;
            [FieldOffset(44)] public uint OwnedMask;
            [FieldOffset(48)] public float BobAmplitude;
            [FieldOffset(52)] public float BobFrequency;
            [FieldOffset(56)] public float SpinRadiansPerSecond;
            [FieldOffset(60)] public float FlickerAmplitude;
        }

        [SerializeField] private Mesh previewMesh;
        [SerializeField] private Material previewMaterial;
        [SerializeField] private Shader previewShader;
        [SerializeField] private Camera targetCamera;
        [SerializeField, Min(1)] private int capacity = 128;
        [SerializeField] private Color validColor = new Color(0.08f, 1f, 0.72f, 0.72f);
        [SerializeField] private Color invalidColor = new Color(1f, 0.18f, 0.12f, 0.78f);

        private VaultBufferHandle<BlueprintPreviewInstance> _writeInstancesHandle;
        private VaultBufferHandle<BlueprintPreviewInstance> _buildInstancesHandle;
        private VaultBufferHandle<Matrix4x4> _matricesHandle;
        private IDataVault _vault;
        private Matrix4x4[] _matrixMirror;
        private JobHandle _buildHandle;
        private bool _buildScheduled;
        private bool _previewVaultLocksHeld;
        private bool _registeredRenderable;
        private bool _registeredLateFrame;
        private bool _hasBaseColorProperty;
        private bool _baseColorApplied;
        private bool _instancesDirty = true;
        private int _activeCount;
        private int _scheduledCount;
        private int _drawCount;
        private Color _appliedBaseColor;
        private Material _cachedMaterialForProperties;
        private int _lastPreviewSignalFrame = -1;
        private bool _lastPreviewAllowed = true;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            ConfigureSignalLane();
            EnsureBuffers();
            EnsureMaterial();
        }

        private void OnEnable()
        {
            ConfigureSignalLane();
            if (!Application.isPlaying)
                return;

            _registeredRenderable = GlobalRegistry.Renderables.TryRegister(this);
            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
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

            CompleteOutstandingBuildForTeardown();
            _drawCount = 0;
        }

        private void OnDestroy()
        {
            CompleteOutstandingBuildForTeardown();
            _writeInstancesHandle = default;
            _buildInstancesHandle = default;
            _matricesHandle = default;
            _vault = null;

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
            ConsumeConstructionPreviewSignals();
            CompleteReadyBuild();
        }

        public bool SetPreview(int index, Vector3 position, Quaternion rotation, Vector3 scale, uint requirementMask, uint ownedMask)
        {
            if (!TryEnsureAndResolveBuffers(
                    out NativeArray<BlueprintPreviewInstance> writeInstances,
                    out _,
                    out _))
                return false;

            if ((uint)index >= (uint)writeInstances.Length)
                return false;

            writeInstances[index] = new BlueprintPreviewInstance
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
            _instancesDirty = true;
            return true;
        }

        public void SetActivePreviewCount(int count)
        {
            if (!TryEnsureAndResolveBuffers(
                    out NativeArray<BlueprintPreviewInstance> writeInstances,
                    out _,
                    out _))
                return;

            int previousCount = _activeCount;
            _activeCount = math.clamp(count, 0, writeInstances.Length);
            if (_drawCount > _activeCount)
                _drawCount = _activeCount;
            if (_scheduledCount > _activeCount)
                _scheduledCount = _activeCount;
            if (_activeCount != previousCount)
                _instancesDirty = true;
        }

        public void ClearPreviews()
        {
            _activeCount = 0;
            _scheduledCount = 0;
            _drawCount = 0;
            _instancesDirty = true;
        }

        private void CompleteReadyBuild()
        {
            if (!_buildScheduled)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _buildHandle, forceComplete: false))
                return;

            _buildScheduled = false;
            if (!TryResolvePreviewBuffers(
                    out _,
                    out _,
                    out NativeArray<Matrix4x4> matrices))
            {
                ReleasePreviewVaultLocks();
                _drawCount = 0;
                return;
            }

            int completedCount = math.min(_scheduledCount, _activeCount);
            _drawCount = math.min(completedCount, _matrixMirror != null ? _matrixMirror.Length : 0);
            for (int i = 0; i < _drawCount; i++)
                _matrixMirror[i] = matrices[i];

            ReleasePreviewVaultLocks();
        }

        private void DrawPreparedBatch()
        {
            if (_drawCount <= 0 || previewMesh == null || previewMaterial == null || _matrixMirror == null)
                return;

            if (_cachedMaterialForProperties != previewMaterial)
                CacheMaterialProperties();

            Color targetColor = _lastPreviewAllowed ? validColor : invalidColor;
            if (_hasBaseColorProperty && (!_baseColorApplied || _appliedBaseColor != targetColor))
            {
                previewMaterial.SetColor(BaseColorId, targetColor);
                _appliedBaseColor = targetColor;
                _baseColorApplied = true;
            }

            UnityEngine.Graphics.DrawMeshInstanced(
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
            if (_buildScheduled || _activeCount <= 0)
                return;

            if (!TryEnsureAndResolveBuffers(
                    out NativeArray<BlueprintPreviewInstance> writeInstances,
                    out NativeArray<BlueprintPreviewInstance> buildInstances,
                    out NativeArray<Matrix4x4> matrices))
                return;

            _scheduledCount = math.min(_activeCount, writeInstances.Length);
            if (_scheduledCount <= 0 || !TryLockPreviewVaultBuffers())
                return;

            if (_instancesDirty)
            {
                MemoryInquisitor.Blit(writeInstances, 0, buildInstances, 0, _scheduledCount);
                _instancesDirty = false;
            }

            BuildPreviewMatricesJob job = new BuildPreviewMatricesJob
            {
                Instances = buildInstances,
                Matrices = matrices,
                TimeSeconds = Time.time,
                Count = _scheduledCount
            };
            _buildHandle = job.Schedule(_scheduledCount, 32);
            _buildScheduled = true;
        }

        private static void ConfigureSignalLane()
        {
            SignalBus<ConstructionPreviewSignal>.Configure(
                expectedCapacity: 4,
                maxFrameSignals: 8,
                lowTierFrameSignals: 1,
                laneHash: ConstructionPreviewSignal.LaneHash);
        }

        private void ConsumeConstructionPreviewSignals()
        {
            ReadOnlySpan<ConstructionPreviewSignal> signals = SignalBus<ConstructionPreviewSignal>.GetFrameSnapshot();
            if (signals.Length <= 0)
            {
                if (_lastPreviewSignalFrame >= 0 && Time.frameCount - _lastPreviewSignalFrame > 1)
                    ClearPreviews();
                return;
            }

            EnsureBuffers();
            if (!TryResolvePreviewBuffers(
                    out NativeArray<BlueprintPreviewInstance> writeInstances,
                    out _,
                    out _))
                return;

            int capacityLimit = writeInstances.Length;
            int writeCount = 0;
            for (int i = 0; i < signals.Length && writeCount < capacityLimit; i++)
            {
                ConstructionPreviewSignal signal = signals[i];
                if ((signal.Flags & ConstructionPreviewSignal.FlagActive) == 0)
                    continue;

                float3 runtimePosition = signal.CenterAup.ToRuntimeFloat3();
                float3 safeScale = math.max(signal.Scale, new float3(0.001f));
                Quaternion rotation = new Quaternion(signal.Rotation.x, signal.Rotation.y, signal.Rotation.z, signal.Rotation.w);
                uint ownedMask = signal.IsValid != 0 ? 1u : 0u;
                writeInstances[writeCount] = new BlueprintPreviewInstance
                {
                    Position = runtimePosition,
                    Rotation = new quaternion(rotation.x, rotation.y, rotation.z, rotation.w),
                    Scale = safeScale,
                    RequirementMask = 1u,
                    OwnedMask = ownedMask,
                    BobAmplitude = 0.025f,
                    BobFrequency = 1.35f,
                    SpinRadiansPerSecond = 0.22f,
                    FlickerAmplitude = 1f
                };
                _lastPreviewAllowed = signal.IsValid != 0;
                _lastPreviewSignalFrame = Time.frameCount;
                writeCount++;
            }

            SetActivePreviewCount(writeCount);
        }

        private void EnsureBuffers()
        {
            int resolvedCapacity = math.clamp(capacity, 1, MaxDrawMeshInstancedBatch);
            if (!TryResolveVault(out IDataVault vault))
                return;

            _vault = vault;
            if (_writeInstancesHandle.IsCreated &&
                _buildInstancesHandle.IsCreated &&
                _matricesHandle.IsCreated &&
                _writeInstancesHandle.Length >= resolvedCapacity &&
                _buildInstancesHandle.Length >= resolvedCapacity &&
                _matricesHandle.Length >= resolvedCapacity &&
                _matrixMirror != null &&
                _matrixMirror.Length >= resolvedCapacity &&
                vault.ResolveBuffer(ref _writeInstancesHandle) &&
                vault.ResolveBuffer(ref _buildInstancesHandle) &&
                vault.ResolveBuffer(ref _matricesHandle))
            {
                return;
            }

            if (_buildScheduled)
                return;

            _writeInstancesHandle = vault.GetBufferHandle<BlueprintPreviewInstance>(
                BufferID.ConstructionPreviewWrite,
                resolvedCapacity,
                SystemID.Construction,
                NativeArrayOptions.ClearMemory);
            _buildInstancesHandle = vault.GetBufferHandle<BlueprintPreviewInstance>(
                BufferID.ConstructionPreviewBuild,
                resolvedCapacity,
                SystemID.Construction,
                NativeArrayOptions.ClearMemory);
            _matricesHandle = vault.GetBufferHandle<Matrix4x4>(
                BufferID.ConstructionPreviewMatrices,
                resolvedCapacity,
                SystemID.Construction,
                NativeArrayOptions.UninitializedMemory);

            if (_matrixMirror == null || _matrixMirror.Length < resolvedCapacity)
            {
                _matrixMirror = new Matrix4x4[resolvedCapacity]; // COLD ALLOC: Matrix4x4[capacity] - DrawMeshInstanced managed mirror - owner: HectonBlueprintPreviewBatch
                _instancesDirty = true;
            }
        }

        private bool TryEnsureAndResolveBuffers(
            out NativeArray<BlueprintPreviewInstance> writeInstances,
            out NativeArray<BlueprintPreviewInstance> buildInstances,
            out NativeArray<Matrix4x4> matrices)
        {
            EnsureBuffers();
            return TryResolvePreviewBuffers(out writeInstances, out buildInstances, out matrices);
        }

        private bool TryResolvePreviewBuffers(
            out NativeArray<BlueprintPreviewInstance> writeInstances,
            out NativeArray<BlueprintPreviewInstance> buildInstances,
            out NativeArray<Matrix4x4> matrices)
        {
            writeInstances = default;
            buildInstances = default;
            matrices = default;

            IDataVault vault = _vault;
            if (vault == null && !TryResolveVault(out vault))
                return false;

            _vault = vault;
            writeInstances = _writeInstancesHandle.Resolve(vault);
            buildInstances = _buildInstancesHandle.Resolve(vault);
            matrices = _matricesHandle.Resolve(vault);
            return writeInstances.IsCreated && buildInstances.IsCreated && matrices.IsCreated;
        }

        private bool TryLockPreviewVaultBuffers()
        {
            if (_previewVaultLocksHeld)
                return true;

            if (_vault == null)
                return false;

            bool lockedWrite = _vault.TryLockBuffer(BufferID.ConstructionPreviewWrite, SystemID.Construction);
            bool lockedBuild = lockedWrite &&
                               _vault.TryLockBuffer(BufferID.ConstructionPreviewBuild, SystemID.Construction);
            bool lockedMatrices = lockedBuild &&
                                  _vault.TryLockBuffer(BufferID.ConstructionPreviewMatrices, SystemID.Construction);
            if (lockedWrite && lockedBuild && lockedMatrices)
            {
                _previewVaultLocksHeld = true;
                return true;
            }

            if (lockedMatrices)
                _vault.TryUnlockBuffer(BufferID.ConstructionPreviewMatrices, SystemID.Construction);
            if (lockedBuild)
                _vault.TryUnlockBuffer(BufferID.ConstructionPreviewBuild, SystemID.Construction);
            if (lockedWrite)
                _vault.TryUnlockBuffer(BufferID.ConstructionPreviewWrite, SystemID.Construction);
            return false;
        }

        private void ReleasePreviewVaultLocks()
        {
            if (!_previewVaultLocksHeld || _vault == null)
            {
                _previewVaultLocksHeld = false;
                return;
            }

            _vault.TryUnlockBuffer(BufferID.ConstructionPreviewMatrices, SystemID.Construction);
            _vault.TryUnlockBuffer(BufferID.ConstructionPreviewBuild, SystemID.Construction);
            _vault.TryUnlockBuffer(BufferID.ConstructionPreviewWrite, SystemID.Construction);
            _previewVaultLocksHeld = false;
        }

        private void CompleteOutstandingBuildForTeardown()
        {
            if (_buildScheduled)
            {
                DispatcherJobSwap.TryComplete(ref _buildHandle, forceComplete: true);
                _buildScheduled = false;
            }

            ReleasePreviewVaultLocks();
            _scheduledCount = 0;
            _drawCount = 0;
        }

        private static bool TryResolveVault(out IDataVault vault)
        {
            vault = GlobalRegistry.DataVault;
            if (vault != null)
                return true;

            if (GlobalDataVault.TryGetLatestCreated(out GlobalDataVault latest))
            {
                vault = latest;
                return true;
            }

            return false;
        }

        private void EnsureMaterial()
        {
            if (previewMaterial != null)
            {
                CacheMaterialProperties();
                return;
            }

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
            CacheMaterialProperties();
        }

        private void CacheMaterialProperties()
        {
            _cachedMaterialForProperties = previewMaterial;
            if (previewMaterial != null && !previewMaterial.enableInstancing)
                previewMaterial.enableInstancing = true;
            _hasBaseColorProperty = previewMaterial != null && previewMaterial.HasProperty(BaseColorId);
            _baseColorApplied = false;
        }
    }
}

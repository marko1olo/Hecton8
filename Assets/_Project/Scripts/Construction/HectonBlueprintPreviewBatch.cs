using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Core.Memory.Layout;
using Hecton8.World;
using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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
        private const string HologramShaderPath = "Assets/_Project/Shaders/Hecton_ConstructionDearLieHologram.shader";
        private const float DefaultDearLieWiggleSpeed = 18f;

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

        private VaultBufferHandle<BuilderGhostStateDTO> _stateHandle;
        private VaultBufferHandle<BuilderGhostVisualDTO> _visualHandle;
        private VaultBufferHandle<HolographyTelemetryEntry> _telemetryHandle;
        private IDataVault _vault;
        private GraphicsBuffer _stateBufferA;
        private GraphicsBuffer _stateBufferB;
        private GraphicsBuffer _visualBufferA;
        private GraphicsBuffer _visualBufferB;
        private GraphicsBuffer _argsBufferA;
        private GraphicsBuffer _argsBufferB;
        private Bounds _drawBounds = new Bounds(Vector3.zero, new Vector3(256f, 256f, 256f));
        private bool _registeredRenderable;
        private bool _registeredLateFrame;
        private bool _buffersDirty;
        private bool _lastPreviewAllowed = true;
        private bool _lastDearLieActive;
        private int _activeCount;
        private int _uploadedCount;
        private int _capacityResolved;
        private int _writeBufferIndex;
        private int _lastPreviewSignalFrame = -1;
        private float _dearLieStartTime;
        private float _lastDearLieDampen;
        private float _lastDearLieQuality = 1f;
        private float _lastDearLieWiggleSpeed = DefaultDearLieWiggleSpeed;
        private uint _lastDearLieResultHash;
        private uint _lastDearLieModuleHash;

        private static readonly int BuilderGhostStatesId = Shader.PropertyToID("_H8BuilderGhostStates");
        private static readonly int BuilderGhostVisualsId = Shader.PropertyToID("_H8BuilderGhostVisuals");
        private static readonly int BuilderGhostCountId = Shader.PropertyToID("_H8BuilderGhostCount");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int DearLieDampenId = Shader.PropertyToID("_H8SnapDampen");
        private static readonly int DearLieWiggleSpeedId = Shader.PropertyToID("_H8SnapWiggleSpeed");
        private static readonly int DearLieQualityId = Shader.PropertyToID("_H8GlobalQualityWeight");

        private void Awake()
        {
            ConfigureSignalLane();
            EnsureBuffers();
            EnsureMaterial();
            EnsureGraphicsBuffers();
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

            _uploadedCount = 0;
        }

        private void OnDestroy()
        {
            ReleaseGraphicsBuffer(ref _stateBufferA);
            ReleaseGraphicsBuffer(ref _stateBufferB);
            ReleaseGraphicsBuffer(ref _visualBufferA);
            ReleaseGraphicsBuffer(ref _visualBufferB);
            ReleaseGraphicsBuffer(ref _argsBufferA);
            ReleaseGraphicsBuffer(ref _argsBufferB);
            _stateHandle = default;
            _visualHandle = default;
            _telemetryHandle = default;
            _vault = null;

            if (previewMaterial != null && previewMaterial.hideFlags == HideFlags.DontSave)
                Destroy(previewMaterial);
        }

        public void Render(float deltaTime)
        {
            DrawPreparedBatch();
        }

        public void LateFrameTick()
        {
            ConsumeConstructionPreviewSignals();
            UploadDirtyBuffers();
        }

        public bool SetPreview(int index, Vector3 position, Quaternion rotation, Vector3 scale, uint requirementMask, uint ownedMask)
        {
            if (!TryEnsureAndResolveBuffers(
                    out NativeArray<BuilderGhostStateDTO> states,
                    out NativeArray<BuilderGhostVisualDTO> visuals,
                    out NativeArray<HolographyTelemetryEntry> telemetry))
            {
                return false;
            }

            if ((uint)index >= (uint)states.Length || (uint)index >= (uint)visuals.Length)
                return false;

            uint flags = BuilderGhostValidationFlags.Active |
                         BuilderGhostValidationFlags.PresentationOnly |
                         BuilderGhostValidationFlags.RollbackExcluded |
                         BuilderGhostValidationFlags.GridSnapped;
            if ((ownedMask & requirementMask) == requirementMask)
                flags |= BuilderGhostValidationFlags.Valid;
            else
                flags |= BuilderGhostValidationFlags.BoundsBlocked;

            double3 centerAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(position);
            WriteStateRow(
                states,
                visuals,
                index,
                centerAup,
                new quaternion(rotation.x, rotation.y, rotation.z, rotation.w),
                (float3)scale,
                0u,
                flags,
                0u,
                0u,
                0f,
                ShinobuSocketConstructionRuntime.ResolveGlobalQualityWeight(),
                DefaultDearLieWiggleSpeed);

            WriteTelemetry(telemetry, states[index], 8u, 0f, 0f);
            _activeCount = math.max(_activeCount, index + 1);
            _buffersDirty = true;
            return true;
        }

        public void SetActivePreviewCount(int count)
        {
            _activeCount = math.clamp(count, 0, ResolveCapacity());
            if (_uploadedCount > _activeCount)
                _uploadedCount = _activeCount;
            _buffersDirty = true;
        }

        public void ClearPreviews()
        {
            _activeCount = 0;
            _uploadedCount = 0;
            _buffersDirty = true;
        }

        private void DrawPreparedBatch()
        {
            if (_uploadedCount <= 0 || previewMaterial == null)
                return;

            GraphicsBuffer stateBuffer = _writeBufferIndex == 0 ? _stateBufferB : _stateBufferA;
            GraphicsBuffer visualBuffer = _writeBufferIndex == 0 ? _visualBufferB : _visualBufferA;
            GraphicsBuffer argsBuffer = _writeBufferIndex == 0 ? _argsBufferB : _argsBufferA;
            if (stateBuffer == null || visualBuffer == null || argsBuffer == null)
                return;

            previewMaterial.SetBuffer(BuilderGhostStatesId, stateBuffer);
            previewMaterial.SetBuffer(BuilderGhostVisualsId, visualBuffer);
            previewMaterial.SetInt(BuilderGhostCountId, _uploadedCount);
            ApplyDearLieMaterialProperties();

            Graphics.DrawProceduralIndirect(
                previewMaterial,
                _drawBounds,
                MeshTopology.Triangles,
                argsBuffer,
                0,
                null,
                null,
                ShadowCastingMode.Off,
                false,
                0);
        }

        private void UploadDirtyBuffers()
        {
            if (!_buffersDirty)
                return;

            _buffersDirty = false;
            if (_activeCount <= 0)
            {
                UploadArgs(ResolveWriteArgsBuffer(), 0);
                _uploadedCount = 0;
                _writeBufferIndex ^= 1;
                return;
            }

            if (!TryEnsureAndResolveBuffers(
                    out NativeArray<BuilderGhostStateDTO> states,
                    out NativeArray<BuilderGhostVisualDTO> visuals,
                    out _))
            {
                _uploadedCount = 0;
                return;
            }

            EnsureGraphicsBuffers();
            int writeCount = math.min(_activeCount, math.min(states.Length, visuals.Length));
            GraphicsBuffer stateTarget = ResolveWriteStateBuffer();
            GraphicsBuffer visualTarget = ResolveWriteVisualBuffer();
            GraphicsBuffer argsTarget = ResolveWriteArgsBuffer();
            GraphicsBufferUploadUtility.UploadNativeArray(stateTarget, states, writeCount);
            GraphicsBufferUploadUtility.UploadNativeArray(visualTarget, visuals, writeCount);
            UploadArgs(argsTarget, writeCount);
            _uploadedCount = writeCount;
            _writeBufferIndex ^= 1;
        }

        private void UploadArgs(GraphicsBuffer argsTarget, int instanceCount)
        {
            if (argsTarget == null)
                return;

            NativeArray<BuilderGhostIndirectArgsDTO> mapped = argsTarget.LockBufferForWrite<BuilderGhostIndirectArgsDTO>(0, 1);
            BuilderGhostIndirectArgsDTO args;
            args.VertexCountPerInstance = ShinobuSocketConstructionRuntime.BuilderGhostProceduralVertexCount;
            args.InstanceCount = (uint)math.max(0, instanceCount);
            args.StartVertex = 0u;
            args.StartInstance = 0u;
            mapped[0] = args;
            argsTarget.UnlockBufferAfterWrite<BuilderGhostIndirectArgsDTO>(1);
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

            if (!TryEnsureAndResolveBuffers(
                    out NativeArray<BuilderGhostStateDTO> states,
                    out NativeArray<BuilderGhostVisualDTO> visuals,
                    out NativeArray<HolographyTelemetryEntry> telemetry))
            {
                return;
            }

            int capacityLimit = math.min(states.Length, visuals.Length);
            int writeCount = 0;
            for (int i = 0; i < signals.Length && writeCount < capacityLimit; i++)
            {
                ConstructionPreviewSignal signal = signals[i];
                if ((signal.Flags & ConstructionPreviewSignal.FlagActive) == 0)
                    continue;

                quaternion rotation = new quaternion(signal.Rotation.x, signal.Rotation.y, signal.Rotation.z, signal.Rotation.w);
                float3 safeScale = math.max(signal.Scale, new float3(0.001f));
                uint flags = BuilderGhostValidationFlags.Active |
                             BuilderGhostValidationFlags.PresentationOnly |
                             BuilderGhostValidationFlags.RollbackExcluded |
                             BuilderGhostValidationFlags.GridSnapped;
                if (signal.IsValid != 0)
                    flags |= BuilderGhostValidationFlags.Valid;
                if ((signal.Flags & ConstructionPreviewSignal.FlagSocketSnap) != 0)
                    flags |= BuilderGhostValidationFlags.SocketSnap;
                if ((signal.Flags & ConstructionPreviewSignal.FlagDearLieActive) != 0)
                    flags |= BuilderGhostValidationFlags.DearLieActive;
                if (signal.IsValid == 0 && signal.FailureFlags == 0u)
                    flags |= BuilderGhostValidationFlags.BoundsBlocked;
                if (signal.IsValid == 0 && signal.FailureFlags != 0u)
                    flags |= BuilderGhostValidationFlags.SdfBlocked;

                WriteStateRow(
                    states,
                    visuals,
                    writeCount,
                    signal.CenterAup.ToAbsoluteDouble3(),
                    rotation,
                    safeScale,
                    signal.ModuleHash,
                    flags,
                    signal.FailureFlags,
                    signal.ResultHash,
                    signal.DearLieDampen,
                    signal.GlobalQualityWeight,
                    signal.DearLieWiggleSpeed);
                WriteTelemetry(telemetry, states[writeCount], 8u, 0f, ResolveTelemetrySdfDistance(flags));
                ConsumeDearLieSignal(in signal);
                _lastPreviewAllowed = signal.IsValid != 0;
                _lastPreviewSignalFrame = Time.frameCount;
                writeCount++;
            }

            SetActivePreviewCount(writeCount);
        }

        private void WriteStateRow(
            NativeArray<BuilderGhostStateDTO> states,
            NativeArray<BuilderGhostVisualDTO> visuals,
            int index,
            double3 centerAup,
            quaternion rotation,
            float3 scale,
            uint moduleHash,
            uint validationFlags,
            uint failureFlags,
            uint resultHash,
            float dearLieDampen,
            float globalQualityWeight,
            float dearLieWiggleSpeed)
        {
            double3 runtimeOrigin = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(Vector3.zero);
            double3 runtimeDouble = centerAup - runtimeOrigin;
            float3 runtimePosition = new float3((float)runtimeDouble.x, (float)runtimeDouble.y, (float)runtimeDouble.z);
            bool finite = math.all(math.isfinite(centerAup)) &&
                          math.all(math.isfinite(runtimeDouble)) &&
                          math.all(math.isfinite(runtimePosition)) &&
                          math.all(math.isfinite(rotation.value)) &&
                          math.all(math.isfinite(scale)) &&
                          math.any(scale > 0f);
            uint flags = validationFlags;
            if (!finite || math.any(math.abs(runtimeDouble) > (double)float.MaxValue))
            {
                flags &= ~BuilderGhostValidationFlags.Valid;
                flags |= BuilderGhostValidationFlags.NonFinite;
                runtimePosition = float3.zero;
                rotation = quaternion.identity;
                scale = new float3(0.001f);
            }

            float phase = math.frac(Time.unscaledTime * 0.5f);
            BuilderGhostStateDTO state;
            state.LocalToWorld = float4x4.TRS(runtimePosition, rotation, math.max(scale, new float3(0.001f)));
            state.AUP_TargetPosition = centerAup;
            state.PrefabHashID = moduleHash;
            state.ValidationFlags = flags;
            state.AnimationPhase = phase;
            state.ValidationStateHash = MakeStateHash(moduleHash, flags, failureFlags, resultHash, phase);
            state._pad0 = failureFlags;
            state._pad1 = resultHash;
            state._pad2 = unchecked((uint)Time.frameCount);
            state._pad3 = 0u;
            state._pad4 = 0u;
            state._pad5 = 0u;
            states[index] = state;

            BuilderGhostVisualDTO visual;
            visual.GlobalQualityWeight = ShinobuSocketConstructionRuntime.SanitizeQuality(globalQualityWeight);
            visual.DearLieDampen = math.clamp(math.isfinite(dearLieDampen) ? dearLieDampen : 0f, 0f, 1f);
            visual.DearLieWiggleSpeed = math.isfinite(dearLieWiggleSpeed) && dearLieWiggleSpeed > 0.0001f ? dearLieWiggleSpeed : DefaultDearLieWiggleSpeed;
            visual.Alpha = _lastPreviewAllowed ? validColor.a : invalidColor.a;
            visual.ValidColor = new float4(validColor.r, validColor.g, validColor.b, validColor.a);
            visual.InvalidColor = new float4(invalidColor.r, invalidColor.g, invalidColor.b, invalidColor.a);
            visual.Flags = flags;
            visual.Frame = unchecked((uint)Time.frameCount);
            visual._pad0 = 0u;
            visual._pad1 = 0u;
            visuals[index] = visual;
        }

        private void WriteTelemetry(NativeArray<HolographyTelemetryEntry> telemetry, BuilderGhostStateDTO state, uint sdfCornerChecks, float solverMicroseconds, float minSdfDistance)
        {
            ShinobuSocketConstructionRuntime.WriteHolographyTelemetry(
                telemetry,
                unchecked((uint)Time.frameCount),
                state.AUP_TargetPosition,
                state.PrefabHashID,
                sdfCornerChecks,
                state.ValidationFlags,
                solverMicroseconds,
                minSdfDistance,
                state.ValidationStateHash,
                _lastDearLieQuality);
        }

        private void EnsureBuffers()
        {
            int resolvedCapacity = ResolveCapacity();
            if (!TryResolveVault(out IDataVault vault))
                return;

            _vault = vault;
            if (_stateHandle.IsCreated &&
                _visualHandle.IsCreated &&
                _telemetryHandle.IsCreated &&
                _stateHandle.Length >= resolvedCapacity &&
                _visualHandle.Length >= resolvedCapacity &&
                _telemetryHandle.Length >= ShinobuSocketConstructionRuntime.TelemetryCapacity &&
                vault.ResolveBuffer(ref _stateHandle) &&
                vault.ResolveBuffer(ref _visualHandle) &&
                vault.ResolveBuffer(ref _telemetryHandle))
            {
                return;
            }

            _stateHandle = vault.GetBufferHandle<BuilderGhostStateDTO>(
                ShinobuSocketConstructionRuntime.BuilderGhostStateBufferId,
                resolvedCapacity,
                SystemID.Construction,
                NativeArrayOptions.UninitializedMemory);
            _visualHandle = vault.GetBufferHandle<BuilderGhostVisualDTO>(
                ShinobuSocketConstructionRuntime.BuilderGhostVisualBufferId,
                resolvedCapacity,
                SystemID.Construction,
                NativeArrayOptions.UninitializedMemory);
            _telemetryHandle = vault.GetBufferHandle<HolographyTelemetryEntry>(
                ShinobuSocketConstructionRuntime.BuilderGhostTelemetryBufferId,
                ShinobuSocketConstructionRuntime.TelemetryCapacity,
                SystemID.Construction,
                NativeArrayOptions.UninitializedMemory);
        }

        private bool TryEnsureAndResolveBuffers(
            out NativeArray<BuilderGhostStateDTO> states,
            out NativeArray<BuilderGhostVisualDTO> visuals,
            out NativeArray<HolographyTelemetryEntry> telemetry)
        {
            EnsureBuffers();
            return TryResolveBuffers(out states, out visuals, out telemetry);
        }

        private bool TryResolveBuffers(
            out NativeArray<BuilderGhostStateDTO> states,
            out NativeArray<BuilderGhostVisualDTO> visuals,
            out NativeArray<HolographyTelemetryEntry> telemetry)
        {
            states = default;
            visuals = default;
            telemetry = default;

            IDataVault vault = _vault;
            if (vault == null && !TryResolveVault(out vault))
                return false;

            _vault = vault;
            states = _stateHandle.Resolve(vault);
            visuals = _visualHandle.Resolve(vault);
            telemetry = _telemetryHandle.Resolve(vault);
            return states.IsCreated && visuals.IsCreated && telemetry.IsCreated;
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

        private void EnsureGraphicsBuffers()
        {
            int resolvedCapacity = ResolveCapacity();
            if (_stateBufferA != null &&
                _stateBufferB != null &&
                _visualBufferA != null &&
                _visualBufferB != null &&
                _argsBufferA != null &&
                _argsBufferB != null &&
                _capacityResolved >= resolvedCapacity)
            {
                return;
            }

            ReleaseGraphicsBuffer(ref _stateBufferA);
            ReleaseGraphicsBuffer(ref _stateBufferB);
            ReleaseGraphicsBuffer(ref _visualBufferA);
            ReleaseGraphicsBuffer(ref _visualBufferB);
            ReleaseGraphicsBuffer(ref _argsBufferA);
            ReleaseGraphicsBuffer(ref _argsBufferB);
            _capacityResolved = resolvedCapacity;
            _stateBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<BuilderGhostStateDTO>(resolvedCapacity);
            _stateBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<BuilderGhostStateDTO>(resolvedCapacity);
            _visualBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<BuilderGhostVisualDTO>(resolvedCapacity);
            _visualBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<BuilderGhostVisualDTO>(resolvedCapacity);
            _argsBufferA = CreateIndirectArgsBuffer();
            _argsBufferB = CreateIndirectArgsBuffer();
            UploadArgs(_argsBufferA, 0);
            UploadArgs(_argsBufferB, 0);
        }

        private GraphicsBuffer ResolveWriteStateBuffer()
        {
            return _writeBufferIndex == 0 ? _stateBufferA : _stateBufferB;
        }

        private GraphicsBuffer ResolveWriteVisualBuffer()
        {
            return _writeBufferIndex == 0 ? _visualBufferA : _visualBufferB;
        }

        private GraphicsBuffer ResolveWriteArgsBuffer()
        {
            return _writeBufferIndex == 0 ? _argsBufferA : _argsBufferB;
        }

        private static GraphicsBuffer CreateIndirectArgsBuffer()
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1,
                UnsafeUtility.SizeOf<BuilderGhostIndirectArgsDTO>());
        }

        private int ResolveCapacity()
        {
            return math.clamp(capacity, 1, ShinobuSocketConstructionRuntime.BuilderGhostStateCapacity);
        }

        private void EnsureMaterial()
        {
            if (previewMaterial != null)
                return;

#if UNITY_EDITOR
            if (previewShader == null)
                previewShader = AssetDatabase.LoadAssetAtPath<Shader>(HologramShaderPath);
#endif

            if (previewShader == null)
                return;

            previewMaterial = new Material(previewShader)
            {
                enableInstancing = false,
                hideFlags = HideFlags.DontSave
            };
        }

        private void ConsumeDearLieSignal(in ConstructionPreviewSignal signal)
        {
            bool active = (signal.Flags & ConstructionPreviewSignal.FlagDearLieActive) != 0 &&
                          math.isfinite(signal.DearLieDampen) &&
                          signal.DearLieDampen > 0.0001f;
            if (!active)
            {
                _lastDearLieActive = false;
                _lastDearLieDampen = 0f;
                _lastDearLieQuality = SanitizeUnit(signal.GlobalQualityWeight, 1f);
                _lastDearLieWiggleSpeed = SanitizePositive(signal.DearLieWiggleSpeed, DefaultDearLieWiggleSpeed);
                return;
            }

            bool resetEnvelope = !_lastDearLieActive ||
                                 signal.ResultHash != _lastDearLieResultHash ||
                                 signal.ModuleHash != _lastDearLieModuleHash;
            if (resetEnvelope)
                _dearLieStartTime = Time.time;

            _lastDearLieActive = true;
            _lastDearLieResultHash = signal.ResultHash;
            _lastDearLieModuleHash = signal.ModuleHash;
            _lastDearLieDampen = math.clamp(signal.DearLieDampen, 0f, 1f);
            _lastDearLieQuality = SanitizeUnit(signal.GlobalQualityWeight, 1f);
            _lastDearLieWiggleSpeed = SanitizePositive(signal.DearLieWiggleSpeed, DefaultDearLieWiggleSpeed);
        }

        private void ApplyDearLieMaterialProperties()
        {
            if (previewMaterial == null)
                return;

            float quality = SanitizeUnit(_lastDearLieQuality, 1f);
            float smoothQuality = quality * quality * (3f - (2f * quality));
            float decaySeconds = math.lerp(0.08f, 0.22f, smoothQuality);
            float elapsed = math.max(0f, Time.time - _dearLieStartTime);
            float decay01 = _lastDearLieActive
                ? math.saturate(1f - (elapsed / math.max(0.001f, decaySeconds)))
                : 0f;
            float dampen = _lastDearLieDampen * decay01 * decay01;
            float wiggle = SanitizePositive(_lastDearLieWiggleSpeed, DefaultDearLieWiggleSpeed);
            Color targetColor = _lastPreviewAllowed ? validColor : invalidColor;
            previewMaterial.SetColor(BaseColorId, targetColor);
            previewMaterial.SetFloat(DearLieDampenId, dampen);
            previewMaterial.SetFloat(DearLieWiggleSpeedId, wiggle);
            previewMaterial.SetFloat(DearLieQualityId, quality);
        }

        private static uint MakeStateHash(uint moduleHash, uint flags, uint failureFlags, uint resultHash, float phase)
        {
            uint hash = ShinobuSocketConstructionRuntime.FoldHash(2166136261u, moduleHash);
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, flags);
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, failureFlags);
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, resultHash);
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, math.asuint(phase));
            return hash;
        }

        private static float ResolveTelemetrySdfDistance(uint flags)
        {
            return (flags & BuilderGhostValidationFlags.SdfBlocked) != 0u ? -1f : 1f;
        }

        private static float SanitizeUnit(float value, float fallback)
        {
            return math.saturate(math.isfinite(value) ? value : fallback);
        }

        private static float SanitizePositive(float value, float fallback)
        {
            return math.isfinite(value) && value > 0.0001f ? value : fallback;
        }

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }
    }
}

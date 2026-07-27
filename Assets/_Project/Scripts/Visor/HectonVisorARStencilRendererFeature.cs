using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.UI;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
#endif

namespace Hecton8.Visor
{
    [StructLayout(LayoutKind.Explicit, Size = VisorARStencilContracts.HudParamsStrideBytes)]
    public struct VisorHudParamsDTO
    {
        [FieldOffset(0)] public float4 TargetCoordinates;
        [FieldOffset(16)] public float4 VitalStats;
        [FieldOffset(32)] public float4 VisorGlitchParams;
        [FieldOffset(48)] public float4 QualityAndTime;
    }

    [StructLayout(LayoutKind.Explicit, Size = VisorARStencilContracts.TargetStrideBytes)]
    public struct VisorArTargetDTO
    {
        [FieldOffset(0)] public float4 ScreenAndFlags;
        [FieldOffset(16)] public float4 ColorAndPulse;
        [FieldOffset(32)] public float4 LocalMetersAndDistance;
        [FieldOffset(48)] public float4 ShapeParams;
    }

    [StructLayout(LayoutKind.Explicit, Size = VisorARStencilContracts.DigitParamsStrideBytes)]
    public struct VisorHudDigitParamsDTO
    {
        [FieldOffset(0)] public float4 OxygenDigits;
        [FieldOffset(16)] public float4 DepthDigits;
        [FieldOffset(32)] public float4 PressureDigits;
        [FieldOffset(48)] public float4 WarningDigits;
    }

    [StructLayout(LayoutKind.Explicit, Size = VisorARStencilContracts.TelemetryEntryStrideBytes)]
    public struct VisorTelemetryEntry
    {
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public float TargetCount;
        [FieldOffset(12)] public float QualityWeight;
        [FieldOffset(16)] public float ProjectionMicroseconds;
        [FieldOffset(20)] public float EstimatedGpuMicroseconds;
        [FieldOffset(24)] public float FirstTargetDepthMeters;
        [FieldOffset(28)] public uint StateHash;
        [FieldOffset(32)] public float Oxygen01;
        [FieldOffset(36)] public float Co201;
        [FieldOffset(40)] public float FogIntensity01;
        [FieldOffset(44)] public float StencilScale;
        [FieldOffset(48)] public uint LayoutHash;
        [FieldOffset(52)] public uint VaultGeneration;
        [FieldOffset(56)] public uint CameraPixelWidth;
        [FieldOffset(60)] public uint CameraPixelHeight;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VisorHudProfileDTO
    {
        [FieldOffset(0)] public uint NameHash;
        [FieldOffset(4)] public float FontAtlasScale;
        [FieldOffset(8)] public float Curvature;
        [FieldOffset(12)] public float FogEdgeStrength;
        [FieldOffset(16)] public float4 PrimaryColor;
        [FieldOffset(32)] public float4 WarningColor;
        [FieldOffset(48)] public float4 LayoutOffsetScale;
    }

    public static class VisorARStencilContracts
    {
        public const int HudParamsStrideBytes = 64;
        public const int TargetStrideBytes = 64;
        public const int DigitParamsStrideBytes = 64;
        public const int TelemetryEntryStrideBytes = 64;
        public const int MaxTargets = 16;
        public const int TelemetryFrameCount = 300;
        public const int ProfileCapacity = 16;
        public const int CsvScratchBytes = 16 * 1024;
        public const BufferID HudParamsBufferId = BufferID.HectonVisorARStencilRendererFeature_HudParamsBufferId;
        public const BufferID TargetSourceBufferId = BufferID.HectonVisorARStencilRendererFeature_TargetSourceBufferId;
        public const BufferID ProjectedTargetBufferId = BufferID.HectonVisorARStencilRendererFeature_ProjectedTargetBufferId;
        public const BufferID DigitParamsBufferId = BufferID.HectonVisorARStencilRendererFeature_DigitParamsBufferId;
        public const BufferID TelemetryRingBufferId = BufferID.HectonVisorARStencilRendererFeature_TelemetryRingBufferId;
        public const BufferID ProfileBufferId = BufferID.HectonVisorARStencilRendererFeature_ProfileBufferId;
        public const BufferID CsvScratchBufferId = BufferID.HectonVisorARStencilRendererFeature_CsvScratchBufferId;

        public static bool ValidateLayouts()
        {
            bool sizeMatch =
                UnsafeUtility.SizeOf<VisorHudParamsDTO>() == HudParamsStrideBytes &&
                UnsafeUtility.SizeOf<VisorArTargetDTO>() == TargetStrideBytes &&
                UnsafeUtility.SizeOf<VisorHudDigitParamsDTO>() == DigitParamsStrideBytes &&
                UnsafeUtility.SizeOf<VisorTelemetryEntry>() == TelemetryEntryStrideBytes &&
                UnsafeUtility.SizeOf<ARWaypointOverlay.StencilTargetSourceDTO>() == 80 &&
                UnsafeUtility.SizeOf<VisorHudProfileDTO>() == 64;
#if UNITY_EDITOR
            return sizeMatch &&
                   ValidateHudParamsOffsetsEditor() &&
                   ValidateArTargetOffsetsEditor() &&
                   ValidateDigitParamsOffsetsEditor() &&
                   ValidateTelemetryOffsetsEditor() &&
                   ValidateProfileOffsetsEditor() &&
                   ValidateTargetSourceOffsetsEditor();
#else
            return sizeMatch;
#endif
        }

#if UNITY_EDITOR
        private static bool ValidateHudParamsOffsetsEditor()
        {
            return OffsetOf<VisorHudParamsDTO>(nameof(VisorHudParamsDTO.TargetCoordinates)) == 0 &&
                   OffsetOf<VisorHudParamsDTO>(nameof(VisorHudParamsDTO.VitalStats)) == 16 &&
                   OffsetOf<VisorHudParamsDTO>(nameof(VisorHudParamsDTO.VisorGlitchParams)) == 32 &&
                   OffsetOf<VisorHudParamsDTO>(nameof(VisorHudParamsDTO.QualityAndTime)) == 48;
        }

        private static bool ValidateArTargetOffsetsEditor()
        {
            return OffsetOf<VisorArTargetDTO>(nameof(VisorArTargetDTO.ScreenAndFlags)) == 0 &&
                   OffsetOf<VisorArTargetDTO>(nameof(VisorArTargetDTO.ColorAndPulse)) == 16 &&
                   OffsetOf<VisorArTargetDTO>(nameof(VisorArTargetDTO.LocalMetersAndDistance)) == 32 &&
                   OffsetOf<VisorArTargetDTO>(nameof(VisorArTargetDTO.ShapeParams)) == 48;
        }

        private static bool ValidateDigitParamsOffsetsEditor()
        {
            return OffsetOf<VisorHudDigitParamsDTO>(nameof(VisorHudDigitParamsDTO.OxygenDigits)) == 0 &&
                   OffsetOf<VisorHudDigitParamsDTO>(nameof(VisorHudDigitParamsDTO.DepthDigits)) == 16 &&
                   OffsetOf<VisorHudDigitParamsDTO>(nameof(VisorHudDigitParamsDTO.PressureDigits)) == 32 &&
                   OffsetOf<VisorHudDigitParamsDTO>(nameof(VisorHudDigitParamsDTO.WarningDigits)) == 48;
        }

        private static bool ValidateTelemetryOffsetsEditor()
        {
            return OffsetOf<VisorTelemetryEntry>(nameof(VisorTelemetryEntry.FrameIndex)) == 0 &&
                   OffsetOf<VisorTelemetryEntry>(nameof(VisorTelemetryEntry.Flags)) == 4 &&
                   OffsetOf<VisorTelemetryEntry>(nameof(VisorTelemetryEntry.TargetCount)) == 8 &&
                   OffsetOf<VisorTelemetryEntry>(nameof(VisorTelemetryEntry.QualityWeight)) == 12 &&
                   OffsetOf<VisorTelemetryEntry>(nameof(VisorTelemetryEntry.ProjectionMicroseconds)) == 16 &&
                   OffsetOf<VisorTelemetryEntry>(nameof(VisorTelemetryEntry.EstimatedGpuMicroseconds)) == 20 &&
                   OffsetOf<VisorTelemetryEntry>(nameof(VisorTelemetryEntry.FirstTargetDepthMeters)) == 24 &&
                   OffsetOf<VisorTelemetryEntry>(nameof(VisorTelemetryEntry.StateHash)) == 28 &&
                   OffsetOf<VisorTelemetryEntry>(nameof(VisorTelemetryEntry.Oxygen01)) == 32 &&
                   OffsetOf<VisorTelemetryEntry>(nameof(VisorTelemetryEntry.Co201)) == 36 &&
                   OffsetOf<VisorTelemetryEntry>(nameof(VisorTelemetryEntry.FogIntensity01)) == 40 &&
                   OffsetOf<VisorTelemetryEntry>(nameof(VisorTelemetryEntry.StencilScale)) == 44 &&
                   OffsetOf<VisorTelemetryEntry>(nameof(VisorTelemetryEntry.LayoutHash)) == 48 &&
                   OffsetOf<VisorTelemetryEntry>(nameof(VisorTelemetryEntry.VaultGeneration)) == 52 &&
                   OffsetOf<VisorTelemetryEntry>(nameof(VisorTelemetryEntry.CameraPixelWidth)) == 56 &&
                   OffsetOf<VisorTelemetryEntry>(nameof(VisorTelemetryEntry.CameraPixelHeight)) == 60;
        }

        private static bool ValidateProfileOffsetsEditor()
        {
            return OffsetOf<VisorHudProfileDTO>(nameof(VisorHudProfileDTO.NameHash)) == 0 &&
                   OffsetOf<VisorHudProfileDTO>(nameof(VisorHudProfileDTO.FontAtlasScale)) == 4 &&
                   OffsetOf<VisorHudProfileDTO>(nameof(VisorHudProfileDTO.Curvature)) == 8 &&
                   OffsetOf<VisorHudProfileDTO>(nameof(VisorHudProfileDTO.FogEdgeStrength)) == 12 &&
                   OffsetOf<VisorHudProfileDTO>(nameof(VisorHudProfileDTO.PrimaryColor)) == 16 &&
                   OffsetOf<VisorHudProfileDTO>(nameof(VisorHudProfileDTO.WarningColor)) == 32 &&
                   OffsetOf<VisorHudProfileDTO>(nameof(VisorHudProfileDTO.LayoutOffsetScale)) == 48;
        }

        private static bool ValidateTargetSourceOffsetsEditor()
        {
            return OffsetOf<ARWaypointOverlay.StencilTargetSourceDTO>(nameof(ARWaypointOverlay.StencilTargetSourceDTO.PositionAup)) == 0 &&
                   OffsetOf<ARWaypointOverlay.StencilTargetSourceDTO>(nameof(ARWaypointOverlay.StencilTargetSourceDTO.Color)) == 48 &&
                   OffsetOf<ARWaypointOverlay.StencilTargetSourceDTO>(nameof(ARWaypointOverlay.StencilTargetSourceDTO.Flags)) == 64 &&
                   OffsetOf<ARWaypointOverlay.StencilTargetSourceDTO>(nameof(ARWaypointOverlay.StencilTargetSourceDTO.StableId)) == 68 &&
                   OffsetOf<ARWaypointOverlay.StencilTargetSourceDTO>(nameof(ARWaypointOverlay.StencilTargetSourceDTO.Reserved0)) == 72 &&
                   OffsetOf<ARWaypointOverlay.StencilTargetSourceDTO>(nameof(ARWaypointOverlay.StencilTargetSourceDTO.Reserved1)) == 76;
        }

        private static int OffsetOf<T>(string fieldName) where T : unmanaged
        {
            FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }

        [MenuItem("Hecton8/Visor/Validate AR Stencil DTO Layouts")]
        private static void ValidateLayoutsMenu()
        {
            if (!ValidateLayouts())
            {
                Hecton8.Core.H8Debug.LogError("VISOR_AR_STENCIL DTO layout validation failed.");
                return;
            }

            Hecton8.Core.H8Debug.Log("VISOR_AR_STENCIL DTO layouts valid: Hud=64, Source=80, Target=64, Digits=64, Telemetry=64, Profile=64.");
        }
#endif
    }

    public sealed class HectonVisorARStencilRendererFeature : ScriptableRendererFeature, IGlobalRegistryHotSwapListener
    {
        private const float ProjectionDepthEpsilon = 0.0001f;
        private const double MaxProjectionLocalMeters = 1000000d;
        private const float ProjectionBudgetMicroseconds = 100f;
        private const float DefaultEstimatedGpuMicroseconds = 55f;
        private const uint TelemetryFlagActive = 1u << 0;
        private const uint TelemetryFlagNoPlayerAup = 1u << 1;
        private const uint TelemetryFlagNonFiniteProjection = 1u << 2;
        private const uint TelemetryFlagProjectionOverBudget = 1u << 3;
        private const uint TelemetryFlagMockData = 1u << 4;
        private const uint DumpMagic = 0x56534152u; // VSAR
        private const uint DumpVersion = 1u;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_13KRA.bin";
        private const string DumpPayloadLabel = "visorArStencilDumpPayload";
#if UNITY_EDITOR
        private const string ArShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_VisorAR.shader";
        private const string StencilShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_VisorStencilMask.shader";
        private const string CsvProfileRelativePath = "Assets/_SourceData/Visor/visor_hud_profiles.csv";
#endif

        [Serializable]
        private sealed class FeatureSettings
        {
            [Tooltip("Fullscreen stencil-gated visor AR shader.")]
            public Shader arShader;

            [Tooltip("ColorMask 0 stencil writer shader used for the helmet glass mask.")]
            public Shader stencilShader;

            [Tooltip("Optional physical visor mesh. When absent a cold fallback low-poly glass mesh is generated.")]
            public Mesh visorMaskMesh;

            [Tooltip("Local mask position relative to the render camera.")]
            public Vector3 maskLocalPosition = new Vector3(0f, 0f, 0.38f);

            [Tooltip("Local mask rotation relative to the render camera.")]
            public Vector3 maskLocalEuler = Vector3.zero;

            [Tooltip("Local mask scale relative to the render camera.")]
            public Vector3 maskLocalScale = new Vector3(0.92f, 0.58f, 1f);

            [Tooltip("Stencil injection. After opaques keeps helmet glass depth available.")]
            public RenderPassEvent stencilInjectionPoint = RenderPassEvent.AfterRenderingOpaques;

            [Tooltip("AR injection. After transparents replaces Canvas HUD overdraw with one fullscreen resolve.")]
            public RenderPassEvent arInjectionPoint = RenderPassEvent.AfterRenderingTransparents;

            [Tooltip("Continuous visor curvature scalar consumed by the shader.")]
            [Range(0f, 1f)] public float visorCurvature = 0.48f;

            [Tooltip("Continuous edge-fog strength. Stress and CO2 multiply this value.")]
            [Range(0f, 1f)] public float fogEdgeStrength = 0.42f;

            [Tooltip("Digit scale for the shader-side Dear Lie numeric renderer.")]
            [Range(0.5f, 2f)] public float fontAtlasScale = 1f;

            [Tooltip("Cold test lane: generates violent synthetic vitals without waiting for gameplay damage.")]
            public bool enableMockHudData;

            [Tooltip("Editor/source-data visor_hud_profiles.csv hydration bridge. Player builds require binary/Vault profile data.")]
            public bool loadCsvProfiles = true;
        }

        private static readonly ulong CsvProfileMutationGuardMask =
            VisorMutationGuardBit(VisorARStencilContracts.CsvScratchBufferId) |
            VisorMutationGuardBit(VisorARStencilContracts.ProfileBufferId);

        private sealed class StencilPass : ScriptableRenderPass
        {
            private sealed class PassData
            {
                internal Mesh Mesh;
                internal Matrix4x4 Matrix;
                internal Material Material;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Visor AR Stencil");
            private readonly HectonVisorARStencilRendererFeature _owner;
            private Mesh _mesh;
            private Matrix4x4 _matrix;
            private Material _material;
            private RenderPassEvent _injectionPoint;

            public StencilPass(HectonVisorARStencilRendererFeature owner)
            {
                _owner = owner;
                profilingSampler = _profilingSampler;
            }

            public void Setup(FeatureSettings settings, Material material, Mesh mesh, Matrix4x4 matrix)
            {
                _mesh = mesh;
                _matrix = matrix;
                _material = material;
                _injectionPoint = settings != null ? settings.stencilInjectionPoint : RenderPassEvent.AfterRenderingOpaques;
                renderPassEvent = _injectionPoint;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_mesh == null || _material == null)
                {
                    _owner.ClearStencilPresentationForRenderGraphAbort();
                    return;
                }

                if (IsUnsupportedCamera(frameData))
                    return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                {
                    _owner.ClearStencilPresentationForRenderGraphAbort();
                    return;
                }

                TextureHandle depthTexture = resourceData.activeDepthTexture;
                if (!depthTexture.IsValid())
                {
                    _owner.ClearStencilPresentationForRenderGraphAbort();
                    return;
                }

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>(
                           "Hecton Visor AR Stencil",
                           out PassData passData,
                           _profilingSampler))
                {
                    passData.Mesh = _mesh;
                    passData.Matrix = _matrix;
                    passData.Material = _material;

                    builder.SetRenderAttachmentDepth(depthTexture, AccessFlags.ReadWrite);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                    {
                        context.cmd.DrawMesh(data.Mesh, data.Matrix, data.Material, 0, 0);
                    });
                }
            }
        }

        private sealed class ArPass : ScriptableRenderPass
        {
            private sealed class PassData
            {
                internal TextureHandle Source;
                internal Material Material;
                internal BufferHandle HudBuffer;
                internal BufferHandle DigitBuffer;
                internal BufferHandle TargetBuffer;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Visor AR Resolve");
            private readonly HectonVisorARStencilRendererFeature _owner;
            private FeatureSettings _settings;
            private Material _material;
            private GraphicsBuffer _hudBufferA;
            private GraphicsBuffer _hudBufferB;
            private GraphicsBuffer _digitBufferA;
            private GraphicsBuffer _digitBufferB;
            private GraphicsBuffer _targetBufferA;
            private GraphicsBuffer _targetBufferB;
            private GraphicsBuffer _activeHudBuffer;
            private GraphicsBuffer _activeDigitBuffer;
            private GraphicsBuffer _activeTargetBuffer;
            private int _bufferWriteIndex;
            private bool _supportsSetConstantBuffer;

            public ArPass(HectonVisorARStencilRendererFeature owner)
            {
                _owner = owner;
                profilingSampler = _profilingSampler;
                requiresIntermediateTexture = true;
            }

            public void Setup(FeatureSettings settings, Material material)
            {
                _settings = settings;
                _material = material;
                renderPassEvent = settings != null ? settings.arInjectionPoint : RenderPassEvent.AfterRenderingTransparents;
                ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
                requiresIntermediateTexture = true;
            }

            public bool PrewarmBuffers()
            {
                return EnsureBuffers();
            }

            public void SetGraphicsCapabilitiesCold(bool supportsSetConstantBuffer)
            {
                _supportsSetConstantBuffer = supportsSetConstantBuffer;
            }

            public bool UpdateGpuPayload(
                in VisorHudParamsDTO hudParamsSource,
                in VisorHudDigitParamsDTO digitParamsSource,
                ReadOnlySpan<VisorArTargetDTO> targets)
            {
                if (!HasBuffers() ||
                    targets.Length < VisorARStencilContracts.MaxTargets)
                {
                    return false;
                }

                _bufferWriteIndex ^= 1;
                GraphicsBuffer hudWrite = _bufferWriteIndex == 0 ? _hudBufferA : _hudBufferB;
                GraphicsBuffer digitWrite = _bufferWriteIndex == 0 ? _digitBufferA : _digitBufferB;
                GraphicsBuffer targetWrite = _bufferWriteIndex == 0 ? _targetBufferA : _targetBufferB;

                try
                {
                    NativeArray<VisorHudParamsDTO> mappedHud = hudWrite.LockBufferForWrite<VisorHudParamsDTO>(0, 1);
                    try
                    {
                        CopyHudParamsToMappedBuffer(in hudParamsSource, mappedHud);
                    }
                    finally
                    {
                        hudWrite.UnlockBufferAfterWrite<VisorHudParamsDTO>(1);
                    }

                    NativeArray<VisorHudDigitParamsDTO> mappedDigits = digitWrite.LockBufferForWrite<VisorHudDigitParamsDTO>(0, 1);
                    try
                    {
                        CopyDigitParamsToMappedBuffer(in digitParamsSource, mappedDigits);
                    }
                    finally
                    {
                        digitWrite.UnlockBufferAfterWrite<VisorHudDigitParamsDTO>(1);
                    }

                    NativeArray<VisorArTargetDTO> mappedTargets = targetWrite.LockBufferForWrite<VisorArTargetDTO>(0, VisorARStencilContracts.MaxTargets);
                    try
                    {
                        CopyTargetsToMappedBuffer(targets, mappedTargets);
                    }
                    finally
                    {
                        targetWrite.UnlockBufferAfterWrite<VisorArTargetDTO>(VisorARStencilContracts.MaxTargets);
                    }

                    _activeHudBuffer = hudWrite;
                    _activeDigitBuffer = digitWrite;
                    _activeTargetBuffer = targetWrite;
                    return true;
                }
                catch (ObjectDisposedException)
                {
                    ClearActivePayload();
                    return false;
                }
                catch (InvalidOperationException)
                {
                    ClearActivePayload();
                    return false;
                }
                catch (ArgumentException)
                {
                    ClearActivePayload();
                    return false;
                }
                catch (NotSupportedException)
                {
                    ClearActivePayload();
                    return false;
                }
            }

            private void ClearActivePayload()
            {
                _activeHudBuffer = null;
                _activeDigitBuffer = null;
                _activeTargetBuffer = null;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                bool unsupportedCamera = IsUnsupportedCamera(frameData);
                if (_settings == null ||
                    _material == null ||
                    _activeHudBuffer == null ||
                    !_activeHudBuffer.IsValid() ||
                    _activeDigitBuffer == null ||
                    !_activeDigitBuffer.IsValid() ||
                    _activeTargetBuffer == null ||
                    !_activeTargetBuffer.IsValid() ||
                    unsupportedCamera)
                {
                    if (!unsupportedCamera)
                        _owner.ClearStencilPresentationForRenderGraphAbort();
                    return;
                }

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                {
                    _owner.ClearStencilPresentationForRenderGraphAbort();
                    return;
                }

                TextureHandle sourceTexture = resourceData.activeColorTexture;
                TextureHandle depthTexture = resourceData.activeDepthTexture;
                if (!sourceTexture.IsValid() || !depthTexture.IsValid())
                {
                    _owner.ClearStencilPresentationForRenderGraphAbort();
                    return;
                }

                TextureDesc sourceDesc = renderGraph.GetTextureDesc(sourceTexture);
                TextureDesc destinationDesc = sourceDesc;
                destinationDesc.name = "_HectonVisorARStencilResolve";
                destinationDesc.clearBuffer = false;
                destinationDesc.depthBufferBits = DepthBits.None;
                destinationDesc.msaaSamples = MSAASamples.None;
                TextureHandle destinationTexture = renderGraph.CreateTexture(destinationDesc);
                BufferHandle hudBufferHandle = renderGraph.ImportBuffer(_activeHudBuffer);
                BufferHandle digitBufferHandle = renderGraph.ImportBuffer(_activeDigitBuffer);
                BufferHandle targetBufferHandle = renderGraph.ImportBuffer(_activeTargetBuffer);

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>(
                           "Hecton Visor AR Resolve",
                           out PassData passData,
                           _profilingSampler))
                {
                    passData.Source = sourceTexture;
                    passData.Material = _material;
                    passData.HudBuffer = hudBufferHandle;
                    passData.DigitBuffer = digitBufferHandle;
                    passData.TargetBuffer = targetBufferHandle;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseBuffer(hudBufferHandle, AccessFlags.Read);
                    builder.UseBuffer(digitBufferHandle, AccessFlags.Read);
                    builder.UseBuffer(targetBufferHandle, AccessFlags.Read);
                    builder.SetRenderAttachment(destinationTexture, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(depthTexture, AccessFlags.Read);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                    {
                        GraphicsBuffer hudBuffer = data.HudBuffer;
                        GraphicsBuffer digitBuffer = data.DigitBuffer;
                        GraphicsBuffer targetBuffer = data.TargetBuffer;
                        if (hudBuffer == null || digitBuffer == null || targetBuffer == null)
                            return;

                        context.cmd.SetGlobalTexture(ShaderConstants.BlitTextureId, data.Source);
                        context.cmd.SetGlobalConstantBuffer(hudBuffer, ShaderConstants.HudParamsBufferId, 0, VisorARStencilContracts.HudParamsStrideBytes);
                        context.cmd.SetGlobalConstantBuffer(digitBuffer, ShaderConstants.DigitParamsBufferId, 0, VisorARStencilContracts.DigitParamsStrideBytes);
                        context.cmd.SetGlobalBuffer(ShaderConstants.ArTargetsBufferId, targetBuffer);
                        CoreUtils.DrawFullScreen(context.cmd, data.Material, null, 0);
                        CoreUtils.DrawFullScreen(context.cmd, data.Material, null, 1);
                    });
                }

                resourceData.cameraColor = destinationTexture;
                _owner.MarkStencilResolveRecorded();
            }

            public void Dispose()
            {
                _hudBufferA?.Release();
                _hudBufferB?.Release();
                _digitBufferA?.Release();
                _digitBufferB?.Release();
                _targetBufferA?.Release();
                _targetBufferB?.Release();
                _hudBufferA = null;
                _hudBufferB = null;
                _digitBufferA = null;
                _digitBufferB = null;
                _targetBufferA = null;
                _targetBufferB = null;
                _activeHudBuffer = null;
                _activeDigitBuffer = null;
                _activeTargetBuffer = null;
            }

            private bool HasBuffers()
            {
                if (!_supportsSetConstantBuffer)
                    return false;

                return _hudBufferA != null && _hudBufferA.IsValid() &&
                       _hudBufferB != null && _hudBufferB.IsValid() &&
                       _digitBufferA != null && _digitBufferA.IsValid() &&
                       _digitBufferB != null && _digitBufferB.IsValid() &&
                       _targetBufferA != null && _targetBufferA.IsValid() &&
                       _targetBufferB != null && _targetBufferB.IsValid();
            }

            private bool EnsureBuffers()
            {
                if (HasBuffers())
                    return true;

                if (!_supportsSetConstantBuffer)
                    return false;

                Dispose();
                _hudBufferA = new GraphicsBuffer(GraphicsBuffer.Target.Constant, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, VisorARStencilContracts.HudParamsStrideBytes);
                _hudBufferB = new GraphicsBuffer(GraphicsBuffer.Target.Constant, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, VisorARStencilContracts.HudParamsStrideBytes);
                _digitBufferA = new GraphicsBuffer(GraphicsBuffer.Target.Constant, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, VisorARStencilContracts.DigitParamsStrideBytes);
                _digitBufferB = new GraphicsBuffer(GraphicsBuffer.Target.Constant, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, VisorARStencilContracts.DigitParamsStrideBytes);
                _targetBufferA = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, VisorARStencilContracts.MaxTargets, VisorARStencilContracts.TargetStrideBytes);
                _targetBufferB = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, VisorARStencilContracts.MaxTargets, VisorARStencilContracts.TargetStrideBytes);
                return _hudBufferA.IsValid() && _hudBufferB.IsValid() &&
                       _digitBufferA.IsValid() && _digitBufferB.IsValid() &&
                       _targetBufferA.IsValid() && _targetBufferB.IsValid();
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
            internal static readonly int HudParamsBufferId = Shader.PropertyToID("HectonVisorHudParams");
            internal static readonly int DigitParamsBufferId = Shader.PropertyToID("HectonVisorDigitParams");
            internal static readonly int ArTargetsBufferId = Shader.PropertyToID("_HectonVisorArTargets");
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private StencilPass _stencilPass;
        private ArPass _arPass;
        private Material _stencilMaterial;
        private Material _arMaterial;
        private IPlayerRuntimeContext _playerContext;
        private IDataVault _dataVault;
        private VaultGenerationHandle<VisorHudParamsDTO> _hudParamsHandle;
        private VaultGenerationHandle<ARWaypointOverlay.StencilTargetSourceDTO> _targetSourceHandle;
        private VaultGenerationHandle<VisorArTargetDTO> _projectedTargetHandle;
        private VaultGenerationHandle<VisorHudDigitParamsDTO> _digitParamsHandle;
        private VaultGenerationHandle<VisorTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<VisorHudProfileDTO> _profileHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;
        private bool _hotSwapRegistered;
        private bool _telemetryDumped;
        private uint _telemetryDescriptorGeneration;
        private int _lastStencilPresentationFrame = -1;
        private int _pendingStencilPresentationFrame = -1;
        private bool _renderWatchdogRegistered;
        private bool _supportsSetConstantBuffer;

        private void OnDisable()
        {
            _pendingStencilPresentationFrame = -1;
            _lastStencilPresentationFrame = -1;
            SetStencilPresentationActive(false);
            TryUnregisterHotSwapListener();
            TryUnregisterRenderWatchdog();
        }

        public override void Create()
        {
#if UNITY_EDITOR
            if (settings != null && settings.arShader == null)
                settings.arShader = AssetDatabase.LoadAssetAtPath<Shader>(ArShaderAssetPath);
            if (settings != null && settings.stencilShader == null)
                settings.stencilShader = AssetDatabase.LoadAssetAtPath<Shader>(StencilShaderAssetPath);
#endif
            if (!VisorARStencilContracts.ValidateLayouts())
                Hecton8.Core.H8Debug.LogError("VISOR_AR_STENCIL DTO layout validation failed.");

            _stencilPass ??= new StencilPass(this);
            _arPass ??= new ArPass(this);
            CacheGraphicsCapabilitiesCold();
            RecreateMaterial(ref _stencilMaterial, settings != null ? settings.stencilShader : null);
            RecreateMaterial(ref _arMaterial, settings != null ? settings.arShader : null);
            if (!Application.isPlaying)
                _arPass.Dispose();
            TryRegisterHotSwapListener();
            TryRegisterRenderWatchdog();
            CacheColdServices(GlobalRegistry.Player, GlobalRegistry.DataVault);
#if UNITY_EDITOR
            LoadCsvProfilesCold();
#endif
        }

        private void CacheGraphicsCapabilitiesCold()
        {
            _supportsSetConstantBuffer = SystemInfo.supportsSetConstantBuffer;
            _arPass?.SetGraphicsCapabilitiesCold(_supportsSetConstantBuffer);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null || _stencilPass == null || _arPass == null || _stencilMaterial == null || _arMaterial == null)
            {
                SetStencilPresentationActive(false);
                return;
            }

            Camera renderCamera = renderingData.cameraData.camera;
            CameraType cameraType = renderingData.cameraData.cameraType;
            if (renderCamera == null ||
                cameraType != CameraType.Game ||
                renderingData.cameraData.renderType != CameraRenderType.Base ||
                !IsAuthorizedPlayerRenderCamera(renderCamera))
            {
                ClearStencilPresentationIfFrameUnclaimed();
                return;
            }

            if (!HasRequiredVaultHandles() && !TryEnsureVaultBuffers())
            {
                SetStencilPresentationActive(false);
                return;
            }

            Mesh maskMesh = settings.visorMaskMesh;
            if (maskMesh == null)
            {
                SetStencilPresentationActive(false);
                return;
            }

            Matrix4x4 maskMatrix = ResolveMaskMatrix(renderCamera, settings);

            if (!_arPass.PrewarmBuffers())
            {
                SetStencilPresentationActive(false);
                return;
            }

            if (!BuildAndUploadFrame(renderCamera, out uint telemetryFlags))
            {
                SetStencilPresentationActive(false);
                return;
            }

            _stencilPass.Setup(settings, _stencilMaterial, maskMesh, maskMatrix);
            _arPass.Setup(settings, _arMaterial);
            _pendingStencilPresentationFrame = SystemDispatcher.CurrentFrameIndex;
            renderer.EnqueuePass(_stencilPass);
            renderer.EnqueuePass(_arPass);

            if ((telemetryFlags & TelemetryFlagNonFiniteProjection) != 0u)
                DumpTelemetryOnce(telemetryFlags);
        }

        protected override void Dispose(bool disposing)
        {
            _arPass?.Dispose();
            CoreUtils.Destroy(_stencilMaterial);
            CoreUtils.Destroy(_arMaterial);
            _stencilMaterial = null;
            _arMaterial = null;
            ReleaseVaultHandles(_dataVault);
            _dataVault = null;
            TryUnregisterHotSwapListener();
            TryUnregisterRenderWatchdog();
            SetStencilPresentationActive(false);
        }

        private static void SetStencilPresentationActive(bool active)
        {
            ARWaypointOverlay.SetStencilRenderGraphActive(active);
            SuitHUDV4CanvasOverlay.SetStencilRenderGraphRuntimeActive(active);
        }

        private void ClearStencilPresentationForRenderGraphAbort()
        {
            _pendingStencilPresentationFrame = -1;
            _lastStencilPresentationFrame = -1;
            SetStencilPresentationActive(false);
        }

        private void ClearStencilPresentationIfFrameUnclaimed()
        {
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastStencilPresentationFrame != frame && _pendingStencilPresentationFrame != frame)
                SetStencilPresentationActive(false);
        }

        private void MarkStencilResolveRecorded()
        {
            int frame = SystemDispatcher.CurrentFrameIndex;
            _lastStencilPresentationFrame = frame;
            _pendingStencilPresentationFrame = -1;
            SetStencilPresentationActive(true);
        }

        private void TryRegisterRenderWatchdog()
        {
            if (_renderWatchdogRegistered)
                return;

            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
            _renderWatchdogRegistered = true;
        }

        private void TryUnregisterRenderWatchdog()
        {
            if (!_renderWatchdogRegistered)
                return;

            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            _renderWatchdogRegistered = false;
        }

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (!IsAuthorizedPlayerRenderCamera(camera))
                return;

            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastStencilPresentationFrame != frame)
                ClearStencilPresentationForRenderGraphAbort();
        }

        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _playerContext = currentService as IPlayerRuntimeContext;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                if (_dataVault == null && previousService is IDataVault previousVault)
                    ReleaseVaultHandles(previousVault);

                IDataVault nextVault = currentService is IDataVault dataVault ? dataVault : null;
                BindDataVaultForLifecycle(nextVault);
                TryEnsureVaultBuffers();
#if UNITY_EDITOR
                LoadCsvProfilesCold();
#endif
            }
        }

        private bool BuildAndUploadFrame(
            Camera renderCamera,
            out uint telemetryFlags)
        {
            telemetryFlags = TelemetryFlagActive;
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
            {
                return false;
            }

            Span<ARWaypointOverlay.StencilTargetSourceDTO> targetSources =
                stackalloc ARWaypointOverlay.StencilTargetSourceDTO[VisorARStencilContracts.MaxTargets];
            Span<VisorArTargetDTO> projectedTargets =
                stackalloc VisorArTargetDTO[VisorARStencilContracts.MaxTargets];

            int sourceCount = ARWaypointOverlay.CopyStencilTargetSources(targetSources, VisorARStencilContracts.MaxTargets);
            float quality = ResolveGlobalQualityWeight01();
            float oxygen01 = ReadUIValue(UIValueSlotId.Oxygen01, 1f);
            float co201 = math.saturate(ReadUIValue(UIValueSlotId.RoomCarbonDioxidePartialKPa, 0f) * 0.1f);
            float toxicity01 = math.saturate(ReadUIValue(UIValueSlotId.RoomNarcosis01, 0f));
            float temperature01 = 1f - math.saturate(ReadUIValue(UIValueSlotId.FrostIntensity01, 0f));
            float depthMeters = math.max(0f, ReadUIValue(UIValueSlotId.DepthMeters, 0f));
            float pressureAtm = math.max(1f, ReadUIValue(UIValueSlotId.PressureAtm, 1f));
            float health01 = math.saturate(ReadUIValue(UIValueSlotId.Health01, 1f));
            float stress01 = ResolvePlayerStress01(oxygen01, co201);
            float fog01 = math.saturate(settings.fogEdgeStrength * (0.35f + stress01 * 0.65f));
            float timeSeconds = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;

            VisorHudParamsDTO hudParams = new VisorHudParamsDTO
            {
                VitalStats = new float4(oxygen01, co201, toxicity01, temperature01),
                VisorGlitchParams = new float4(stress01, fog01, math.saturate(settings.visorCurvature), health01),
                QualityAndTime = new float4(quality, timeSeconds, sourceCount, math.max(0.01f, settings.fontAtlasScale))
            };

            if (settings.enableMockHudData)
            {
                hudParams = GenerateMockHudData(timeSeconds, in hudParams);
                telemetryFlags |= TelemetryFlagMockData;
            }

            bool hasCameraAup = TryResolveCameraAup(out AbsoluteUniversePosition cameraAup);
            if (!hasCameraAup)
            {
                telemetryFlags |= TelemetryFlagNoPlayerAup;
                sourceCount = 0;
            }

            long projectionStart = System.Diagnostics.Stopwatch.GetTimestamp();
            int projectedCount = hasCameraAup
                ? ProjectArTargets(
                    targetSources,
                    projectedTargets,
                    sourceCount,
                    cameraAup.ToAbsoluteDouble3(),
                    (float3)(renderCamera.transform.right),
                    (float3)(renderCamera.transform.up),
                    (float3)(renderCamera.transform.forward),
                    global::Hecton8.Core.MathLodApproximation.ApproxTanClamped(math.radians(math.max(1f, renderCamera.fieldOfView)) * 0.5f, 4096f),
                    math.max(0.01f, renderCamera.aspect),
                    math.max(ProjectionDepthEpsilon, renderCamera.nearClipPlane),
                    math.max(renderCamera.farClipPlane, renderCamera.nearClipPlane + 1f),
                    quality,
                    ref telemetryFlags)
                : ClearProjectedTargets(projectedTargets);
            long projectionTicks = System.Diagnostics.Stopwatch.GetTimestamp() - projectionStart;
            float projectionUs = (float)(projectionTicks * 1000000.0d / System.Diagnostics.Stopwatch.Frequency);
            if (projectionUs > ProjectionBudgetMicroseconds)
                telemetryFlags |= TelemetryFlagProjectionOverBudget;

            if (projectedCount > 0)
                hudParams.TargetCoordinates = projectedTargets[0].ScreenAndFlags;
            hudParams.QualityAndTime.z = projectedCount;

            VisorHudDigitParamsDTO digitParams = BuildDigitParams(
                hudParams.VitalStats.x,
                depthMeters,
                pressureAtm,
                hudParams.VisorGlitchParams.x);

            if (!TryCommitSpanToVault(
                    vault,
                    in _targetSourceHandle,
                    targetSources,
                    VisorARStencilContracts.MaxTargets) ||
                !TryCommitSpanToVault(
                    vault,
                    in _projectedTargetHandle,
                    projectedTargets,
                    VisorARStencilContracts.MaxTargets) ||
                !TryCommitSingleToVault(vault, in _digitParamsHandle, in digitParams) ||
                !TryCommitSingleToVault(vault, in _hudParamsHandle, in hudParams))
            {
                return false;
            }

            if (!_arPass.UpdateGpuPayload(in hudParams, in digitParams, projectedTargets))
                return false;

            return TryCommitTelemetryFrame(
                vault,
                renderCamera,
                in hudParams,
                projectedTargets,
                projectedCount,
                projectionUs,
                telemetryFlags);
        }

        private static bool TryCommitSingleToVault<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            in T value) where T : struct
        {
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (!vault.TryAcquireWriteLock(in handle, SystemID.UI, out NativeArray<T> buffer))
                return false;

            try
            {
                if (!buffer.IsCreated || buffer.Length <= 0)
                    return false;

                T copy = value;
                buffer[0] = copy;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, SystemID.UI);
            }
        }

        private static bool TryCommitSpanToVault<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            ReadOnlySpan<T> source,
            int count) where T : struct
        {
            if (vault == null || vault.IsCompactionFenceActive || count < 0 || source.Length < count)
                return false;

            if (!vault.TryAcquireWriteLock(in handle, SystemID.UI, out NativeArray<T> destination))
                return false;

            try
            {
                if (!destination.IsCreated || destination.Length < count)
                    return false;

                for (int i = 0; i < count; i++)
                    destination[i] = source[i];

                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, SystemID.UI);
            }
        }

        private bool TryCommitTelemetryFrame(
            IDataVault vault,
            Camera renderCamera,
            in VisorHudParamsDTO hudParams,
            ReadOnlySpan<VisorArTargetDTO> projectedTargets,
            int projectedCount,
            float projectionUs,
            uint telemetryFlags)
        {
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (!vault.TryAcquireWriteLock(in _telemetryHandle, SystemID.UI, out NativeArray<VisorTelemetryEntry> telemetry))
                return false;

            try
            {
                int telemetryLength = telemetry.IsCreated
                    ? math.min(telemetry.Length, VisorARStencilContracts.TelemetryFrameCount)
                    : 0;
                if (telemetryLength <= 0)
                    return false;

                WriteTelemetryFrame(
                    renderCamera,
                    telemetry,
                    telemetryLength,
                    in hudParams,
                    projectedTargets,
                    projectedCount,
                    projectionUs,
                    telemetryFlags);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _telemetryHandle, SystemID.UI);
            }
        }

        private bool TryEnsureVaultBuffers()
        {
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (!IsOwnedHandle(in _hudParamsHandle, VisorARStencilContracts.HudParamsBufferId))
                _hudParamsHandle = vault.EnsureGenerationHandle<VisorHudParamsDTO>(VisorARStencilContracts.HudParamsBufferId, 1, SystemID.UI, NativeArrayOptions.ClearMemory);
            if (!IsOwnedHandle(in _targetSourceHandle, VisorARStencilContracts.TargetSourceBufferId))
                _targetSourceHandle = vault.EnsureGenerationHandle<ARWaypointOverlay.StencilTargetSourceDTO>(VisorARStencilContracts.TargetSourceBufferId, VisorARStencilContracts.MaxTargets, SystemID.UI, NativeArrayOptions.ClearMemory);
            if (!IsOwnedHandle(in _projectedTargetHandle, VisorARStencilContracts.ProjectedTargetBufferId))
                _projectedTargetHandle = vault.EnsureGenerationHandle<VisorArTargetDTO>(VisorARStencilContracts.ProjectedTargetBufferId, VisorARStencilContracts.MaxTargets, SystemID.UI, NativeArrayOptions.ClearMemory);
            if (!IsOwnedHandle(in _digitParamsHandle, VisorARStencilContracts.DigitParamsBufferId))
                _digitParamsHandle = vault.EnsureGenerationHandle<VisorHudDigitParamsDTO>(VisorARStencilContracts.DigitParamsBufferId, 1, SystemID.UI, NativeArrayOptions.ClearMemory);
            if (!IsOwnedHandle(in _telemetryHandle, VisorARStencilContracts.TelemetryRingBufferId))
                _telemetryHandle = vault.EnsureGenerationHandle<VisorTelemetryEntry>(VisorARStencilContracts.TelemetryRingBufferId, VisorARStencilContracts.TelemetryFrameCount, SystemID.UI, NativeArrayOptions.ClearMemory);
            if (!IsOwnedHandle(in _profileHandle, VisorARStencilContracts.ProfileBufferId))
                _profileHandle = vault.EnsureGenerationHandle<VisorHudProfileDTO>(VisorARStencilContracts.ProfileBufferId, VisorARStencilContracts.ProfileCapacity, SystemID.UI, NativeArrayOptions.ClearMemory);
            if (!IsOwnedHandle(in _csvScratchHandle, VisorARStencilContracts.CsvScratchBufferId))
                _csvScratchHandle = vault.EnsureGenerationHandle<byte>(VisorARStencilContracts.CsvScratchBufferId, VisorARStencilContracts.CsvScratchBytes, SystemID.UI, NativeArrayOptions.UninitializedMemory);

            _telemetryDescriptorGeneration = _telemetryHandle.Generation;
            return IsOwnedHandle(in _hudParamsHandle, VisorARStencilContracts.HudParamsBufferId) &&
                   IsOwnedHandle(in _targetSourceHandle, VisorARStencilContracts.TargetSourceBufferId) &&
                   IsOwnedHandle(in _projectedTargetHandle, VisorARStencilContracts.ProjectedTargetBufferId) &&
                   IsOwnedHandle(in _digitParamsHandle, VisorARStencilContracts.DigitParamsBufferId) &&
                   IsOwnedHandle(in _telemetryHandle, VisorARStencilContracts.TelemetryRingBufferId);
        }

        private bool HasRequiredVaultHandles()
        {
            return _dataVault != null &&
                   !_dataVault.IsCompactionFenceActive &&
                   IsOwnedHandle(in _hudParamsHandle, VisorARStencilContracts.HudParamsBufferId) &&
                   IsOwnedHandle(in _targetSourceHandle, VisorARStencilContracts.TargetSourceBufferId) &&
                   IsOwnedHandle(in _projectedTargetHandle, VisorARStencilContracts.ProjectedTargetBufferId) &&
                   IsOwnedHandle(in _digitParamsHandle, VisorARStencilContracts.DigitParamsBufferId) &&
                   IsOwnedHandle(in _telemetryHandle, VisorARStencilContracts.TelemetryRingBufferId);
        }

        // Pure buffer-id bit computation. It is consumed by the CsvProfileMutationGuardMask static
        // field initializer near the top of the file, which is NOT preprocessor-guarded, so this
        // helper must exist in a player build too. Same shape and placement as
        // HectonVisorUberPostFeature.UberVisorMutationGuardBit, which is likewise unguarded.
        private static ulong VisorMutationGuardBit(BufferID bufferId)
        {
            return 1UL << ((int)bufferId & 31);
        }

#if UNITY_EDITOR
        private void LoadCsvProfilesCold()
        {
            IDataVault vault = _dataVault;
            if (settings == null || !settings.loadCsvProfiles || vault == null || vault.IsCompactionFenceActive)
                return;

            if (!TryEnsureVaultBuffers())
            {
                return;
            }

            string path = Path.Combine(Application.dataPath, "..", CsvProfileRelativePath);
            if (!File.Exists(path))
                return;

            try
            {
                Span<byte> csvBytes = stackalloc byte[VisorARStencilContracts.CsvScratchBytes];
                int read = ReadCsvFileIntoSpan(path, csvBytes);
                if (read <= 0)
                    return;

                Span<VisorHudProfileDTO> parsedProfiles = stackalloc VisorHudProfileDTO[VisorARStencilContracts.ProfileCapacity];
                int parsed = ParseProfilesCsv(csvBytes.Slice(0, read), parsedProfiles);

                if (!vault.TryAcquireMutationGuard(CsvProfileMutationGuardMask))
                {
                    return;
                }

                try
                {
                    if (!vault.TryResolveHandle(in _csvScratchHandle, out NativeArray<byte> scratch) ||
                        !vault.TryResolveHandle(in _profileHandle, out NativeArray<VisorHudProfileDTO> profiles) ||
                        !scratch.IsCreated ||
                        !profiles.IsCreated)
                    {
                        return;
                    }

                    CopyBytesToNativeArray(csvBytes.Slice(0, read), scratch);
                    CopyProfilesToNativeArray(parsedProfiles, parsed, profiles);
                }
                finally
                {
                    vault.ReleaseMutationGuard(CsvProfileMutationGuardMask);
                }
            }
            catch (IOException)
            {
                // Cold editor/dev configuration path. Runtime rendering must not depend on CSV success.
            }
            catch (UnauthorizedAccessException)
            {
                // Cold editor/dev configuration path. Runtime rendering must not depend on CSV success.
            }
            catch (ArgumentException)
            {
                // Cold editor/dev configuration path. Runtime rendering must not depend on CSV success.
            }
            catch (NotSupportedException)
            {
                // Cold editor/dev configuration path. Runtime rendering must not depend on CSV success.
            }
            catch (ObjectDisposedException)
            {
                // Cold editor/dev configuration path. Runtime rendering must not depend on CSV success.
            }
        }

        private static int ReadCsvFileIntoSpan(string path, Span<byte> destination)
        {
            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            int length = (int)math.min(stream.Length, destination.Length);
            return length > 0 ? stream.Read(destination.Slice(0, length)) : 0;
        }

        private static void CopyBytesToNativeArray(ReadOnlySpan<byte> source, NativeArray<byte> destination)
        {
            int count = math.min(source.Length, destination.IsCreated ? destination.Length : 0);
            for (int i = 0; i < count; i++)
                destination[i] = source[i];
            for (int i = count; i < destination.Length; i++)
                destination[i] = 0;
        }

        private static void CopyProfilesToNativeArray(
            ReadOnlySpan<VisorHudProfileDTO> source,
            int count,
            NativeArray<VisorHudProfileDTO> destination)
        {
            int safeCount = math.min(math.max(0, count), math.min(source.Length, destination.IsCreated ? destination.Length : 0));
            for (int i = 0; i < safeCount; i++)
                destination[i] = source[i];
            for (int i = safeCount; i < destination.Length; i++)
                destination[i] = default;
        }

        private static int ParseProfilesCsv(ReadOnlySpan<byte> bytes, Span<VisorHudProfileDTO> profiles)
        {
            int count = 0;
            int cursor = 0;
            int length = bytes.Length;
            SkipLine(bytes, length, ref cursor);
            while (cursor < length && count < profiles.Length)
            {
                uint nameHash = ParseHash(bytes, length, ref cursor);
                float fontScale = ParseFloat(bytes, length, ref cursor, 1f);
                float curvature = ParseFloat(bytes, length, ref cursor, 0.48f);
                float fog = ParseFloat(bytes, length, ref cursor, 0.42f);
                float r = ParseFloat(bytes, length, ref cursor, 0.42f);
                float g = ParseFloat(bytes, length, ref cursor, 0.94f);
                float b = ParseFloat(bytes, length, ref cursor, 0.98f);
                float a = ParseFloat(bytes, length, ref cursor, 1f);
                SkipLine(bytes, length, ref cursor);
                profiles[count] = new VisorHudProfileDTO
                {
                    NameHash = nameHash,
                    FontAtlasScale = math.saturate(fontScale),
                    Curvature = math.saturate(curvature),
                    FogEdgeStrength = math.saturate(fog),
                    PrimaryColor = new float4(r, g, b, a),
                    WarningColor = new float4(1f, 0.34f, 0.2f, 1f),
                    LayoutOffsetScale = new float4(0f, 0f, 1f, 1f)
                };
                count++;
            }

            return count;
        }
#endif

        private static uint ParseHash(ReadOnlySpan<byte> bytes, int length, ref int cursor)
        {
            uint hash = 2166136261u;
            while (cursor < length)
            {
                byte value = bytes[cursor++];
                if (value == (byte)',' || value == (byte)'\n' || value == (byte)'\r')
                    break;
                hash = (hash ^ value) * 16777619u;
            }

            return hash;
        }

        private static float ParseFloat(ReadOnlySpan<byte> bytes, int length, ref int cursor, float fallback)
        {
            while (cursor < length && (bytes[cursor] == (byte)' ' || bytes[cursor] == (byte)'\t'))
                cursor++;

            float sign = 1f;
            if (cursor < length && bytes[cursor] == (byte)'-')
            {
                sign = -1f;
                cursor++;
            }

            float value = 0f;
            bool hasDigit = false;
            while (cursor < length)
            {
                byte c = bytes[cursor];
                if (c < (byte)'0' || c > (byte)'9')
                    break;
                hasDigit = true;
                value = value * 10f + (c - (byte)'0');
                cursor++;
            }

            if (cursor < length && bytes[cursor] == (byte)'.')
            {
                cursor++;
                float place = 0.1f;
                while (cursor < length)
                {
                    byte c = bytes[cursor];
                    if (c < (byte)'0' || c > (byte)'9')
                        break;
                    hasDigit = true;
                    value += (c - (byte)'0') * place;
                    place *= 0.1f;
                    cursor++;
                }
            }

            while (cursor < length && bytes[cursor] != (byte)',' && bytes[cursor] != (byte)'\n' && bytes[cursor] != (byte)'\r')
                cursor++;
            if (cursor < length && bytes[cursor] == (byte)',')
                cursor++;

            return hasDigit ? sign * value : fallback;
        }

        private static void SkipLine(ReadOnlySpan<byte> bytes, int length, ref int cursor)
        {
            while (cursor < length)
            {
                byte c = bytes[cursor++];
                if (c == (byte)'\n')
                    break;
            }
        }

        private void WriteTelemetryFrame(
            Camera renderCamera,
            NativeArray<VisorTelemetryEntry> telemetry,
            int telemetryLength,
            in VisorHudParamsDTO hudParams,
            ReadOnlySpan<VisorArTargetDTO> targets,
            int targetCount,
            float projectionUs,
            uint flags)
        {
            if (!telemetry.IsCreated || telemetryLength <= 0)
                return;

            int frame = SystemDispatcher.CurrentFrameIndex;
            int index = frame % telemetryLength;
            if (index < 0)
                index += telemetryLength;

            float firstDepth = targetCount > 0 && targets.Length > 0 ? targets[0].ScreenAndFlags.z : 0f;
            telemetry[index] = new VisorTelemetryEntry
            {
                FrameIndex = frame >= 0 ? (uint)frame : 0u,
                Flags = flags,
                TargetCount = targetCount,
                QualityWeight = hudParams.QualityAndTime.x,
                ProjectionMicroseconds = math.max(0f, projectionUs),
                EstimatedGpuMicroseconds = DefaultEstimatedGpuMicroseconds * math.lerp(0.55f, 1.85f, math.saturate(hudParams.QualityAndTime.x)),
                FirstTargetDepthMeters = firstDepth,
                StateHash = HashHudState(in hudParams, targetCount, flags),
                Oxygen01 = hudParams.VitalStats.x,
                Co201 = hudParams.VitalStats.y,
                FogIntensity01 = hudParams.VisorGlitchParams.y,
                StencilScale = settings != null ? math.max(settings.maskLocalScale.x, settings.maskLocalScale.y) : 1f,
                LayoutHash = 0x53484E42u,
                VaultGeneration = _telemetryDescriptorGeneration,
                CameraPixelWidth = renderCamera != null ? (uint)math.max(0, renderCamera.pixelWidth) : 0u,
                CameraPixelHeight = renderCamera != null ? (uint)math.max(0, renderCamera.pixelHeight) : 0u
            };
        }

        private unsafe void DumpTelemetryOnce(uint reasonFlags, int telemetryLength, int cursor, uint frameIndex, uint descriptorGeneration)
        {
            if (_telemetryDumped || telemetryLength <= 0)
                return;

            NativeArray<byte> payload = default;
            try
            {
                string path = Path.Combine(Application.dataPath, "..", DumpRelativePath);
                int count = math.min(telemetryLength, VisorARStencilContracts.TelemetryFrameCount);
                int stride = VisorARStencilContracts.TelemetryEntryStrideBytes;
                int totalBytes = 32 + count * stride;
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    totalBytes,
                    nameof(HectonVisorARStencilRendererFeature),
                    DumpPayloadLabel,
                    NativeArrayOptions.UninitializedMemory);
                byte* payloadPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);

                Span<byte> header = new Span<byte>(payloadPtr, 32);
                WriteUInt32LittleEndian(header, 0, DumpMagic);
                WriteUInt32LittleEndian(header, 4, DumpVersion);
                WriteUInt32LittleEndian(header, 8, reasonFlags);
                WriteUInt32LittleEndian(header, 12, VisorARStencilContracts.TelemetryEntryStrideBytes);
                WriteUInt32LittleEndian(header, 16, (uint)count);
                WriteUInt32LittleEndian(header, 20, (uint)cursor);
                WriteUInt32LittleEndian(header, 24, frameIndex);
                WriteUInt32LittleEndian(header, 28, descriptorGeneration);

                int offset = 32;
                int start = count == VisorARStencilContracts.TelemetryFrameCount ? (cursor + 1) % count : 0;
                for (int i = 0; i < count; i++)
                {
                    int row = (start + i) % count;
                    if (!TryCopyTelemetryDumpRow(row, payloadPtr + offset, stride))
                        return;

                    offset += stride;
                }

                _telemetryDumped = NativeFaultDumpWriter.TryWriteAll(path, payload, totalBytes);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (ArgumentException)
            {
            }
            catch (NotSupportedException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(HectonVisorARStencilRendererFeature),
                    DumpPayloadLabel);
            }
        }

        private void DumpTelemetryOnce(uint reasonFlags)
        {
            if (_telemetryDumped)
                return;

            IDataVault vault = _dataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !TryReadTelemetryDumpShape(vault, out int telemetryLength, out int cursor, out uint frameIndex, out uint descriptorGeneration))
            {
                return;
            }

            DumpTelemetryOnce(reasonFlags, telemetryLength, cursor, frameIndex, descriptorGeneration);
        }

        private bool TryReadTelemetryDumpShape(
            IDataVault vault,
            out int telemetryLength,
            out int cursor,
            out uint frameIndex,
            out uint descriptorGeneration)
        {
            telemetryLength = 0;
            cursor = 0;
            frameIndex = 0u;
            descriptorGeneration = 0u;
            if (vault == null ||
                vault.IsCompactionFenceActive)
            {
                return false;
            }

            if (!IsOwnedHandle(in _telemetryHandle, VisorARStencilContracts.TelemetryRingBufferId) ||
                !vault.TryReadOnlyHandle(in _telemetryHandle, out NativeArray<VisorTelemetryEntry>.ReadOnly telemetry) ||
                vault.IsCompactionFenceActive ||
                !telemetry.IsCreated)
            {
                return false;
            }

            telemetryLength = math.min(telemetry.Length, VisorARStencilContracts.TelemetryFrameCount);
            int frame = SystemDispatcher.CurrentFrameIndex;
            frameIndex = frame >= 0 ? (uint)frame : 0u;
            cursor = telemetryLength > 0 ? frame % telemetryLength : 0;
            if (cursor < 0)
                cursor += telemetryLength;
            descriptorGeneration = _telemetryDescriptorGeneration;
            return telemetryLength > 0;
        }

        private unsafe bool TryCopyTelemetryDumpRow(int row, byte* destination, int stride)
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                destination == null ||
                row < 0 ||
                stride != VisorARStencilContracts.TelemetryEntryStrideBytes ||
                vault.IsCompactionFenceActive)
            {
                return false;
            }

            if (!IsOwnedHandle(in _telemetryHandle, VisorARStencilContracts.TelemetryRingBufferId) ||
                !vault.TryReadOnlyHandle(in _telemetryHandle, out NativeArray<VisorTelemetryEntry>.ReadOnly telemetry) ||
                vault.IsCompactionFenceActive ||
                !telemetry.IsCreated ||
                row >= telemetry.Length)
            {
                return false;
            }

            VisorTelemetryEntry entry = telemetry[row];
            UnsafeUtility.MemCpy(destination, &entry, stride);
            return !vault.IsCompactionFenceActive;
        }

        private static void WriteUInt32LittleEndian(Span<byte> bytes, int offset, uint value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }

        private static uint HashHudState(in VisorHudParamsDTO hudParams, int targetCount, uint flags)
        {
            uint hash = 2166136261u;
            hash = Mix(hash, flags);
            hash = Mix(hash, (uint)targetCount);
            hash = Mix(hash, math.asuint(hudParams.VitalStats.x));
            hash = Mix(hash, math.asuint(hudParams.VitalStats.y));
            hash = Mix(hash, math.asuint(hudParams.VisorGlitchParams.x));
            hash = Mix(hash, math.asuint(hudParams.QualityAndTime.x));
            return hash;
        }

        private static uint Mix(uint hash, uint value)
        {
            return (hash ^ value) * 16777619u;
        }

        private static int ProjectArTargets(
            ReadOnlySpan<ARWaypointOverlay.StencilTargetSourceDTO> sourceTargets,
            Span<VisorArTargetDTO> outputTargets,
            int sourceCount,
            double3 cameraAup,
            float3 cameraRight,
            float3 cameraUp,
            float3 cameraForward,
            float tanHalfVerticalFov,
            float aspect,
            float nearClip,
            float farClip,
            float qualityWeight,
            ref uint telemetryFlags)
        {
            int maxCount = math.min(outputTargets.Length, VisorARStencilContracts.MaxTargets);
            int sourceLimit = math.min(sourceTargets.Length, math.max(0, sourceCount));
            float tanY = math.max(ProjectionDepthEpsilon, tanHalfVerticalFov);
            float tanX = math.max(ProjectionDepthEpsilon, tanY * math.max(0.01f, aspect));
            float safeNear = math.max(ProjectionDepthEpsilon, nearClip);
            float safeFar = math.max(safeNear + ProjectionDepthEpsilon, farClip);
            int outputCount = 0;

            for (int i = 0; i < sourceLimit && outputCount < maxCount; i++)
            {
                ARWaypointOverlay.StencilTargetSourceDTO source = sourceTargets[i];
                if ((source.Flags & 1u) == 0u || !source.PositionAup.IsFinite())
                    continue;

                double3 localDouble = source.PositionAup.ToAbsoluteDouble3() - cameraAup;
                if (!math.all(math.isfinite(localDouble)))
                {
                    telemetryFlags |= TelemetryFlagNonFiniteProjection;
                    continue;
                }

                localDouble = math.clamp(
                    localDouble,
                    new double3(-MaxProjectionLocalMeters),
                    new double3(MaxProjectionLocalMeters));
                float3 local = new float3((float)localDouble.x, (float)localDouble.y, (float)localDouble.z);
                float viewX = math.dot(cameraRight, local);
                float viewY = math.dot(cameraUp, local);
                float viewZ = math.dot(cameraForward, local);
                bool depthActive = math.isfinite(viewZ) && viewZ > safeNear && viewZ <= safeFar;
                float safeViewZ = math.max(ProjectionDepthEpsilon, math.abs(viewZ));
                float ndcX = viewX / math.max(ProjectionDepthEpsilon, safeViewZ * tanX);
                float ndcY = viewY / math.max(ProjectionDepthEpsilon, safeViewZ * tanY);
                float distance = math.length(local);
                if (!depthActive ||
                    !math.isfinite(ndcX) ||
                    !math.isfinite(ndcY) ||
                    !math.isfinite(distance))
                {
                    if (!math.isfinite(ndcX) || !math.isfinite(ndcY) || !math.isfinite(distance))
                        telemetryFlags |= TelemetryFlagNonFiniteProjection;
                    continue;
                }

                float inside = Step01(math.abs(ndcX), 1.35f) * Step01(math.abs(ndcY), 1.35f);
                if (inside <= 0f)
                    continue;

                float edge = math.saturate(math.max(math.abs(ndcX), math.abs(ndcY)));
                float uvX = ndcX * 0.5f + 0.5f;
                float uvY = ndcY * 0.5f + 0.5f;
                float shapeScale = math.lerp(0.72f, 1.35f, math.saturate(qualityWeight)) *
                                   math.rsqrt(math.max(1f, distance * 0.035f));
                outputTargets[outputCount] = new VisorArTargetDTO
                {
                    ScreenAndFlags = new float4(uvX, uvY, distance, 1f),
                    ColorAndPulse = source.Color,
                    LocalMetersAndDistance = new float4(local.x, local.y, local.z, distance),
                    ShapeParams = new float4(edge, (source.Flags & 2u) != 0u ? 1f : 0f, shapeScale, source.StableId)
                };
                outputCount++;
            }

            for (int i = outputCount; i < maxCount; i++)
                outputTargets[i] = default;

            return outputCount;
        }

        private static int ClearProjectedTargets(Span<VisorArTargetDTO> outputTargets)
        {
            int count = math.min(outputTargets.Length, VisorARStencilContracts.MaxTargets);
            for (int i = 0; i < count; i++)
                outputTargets[i] = default;
            return 0;
        }

        private bool TryResolveCameraAup(out AbsoluteUniversePosition cameraAup)
        {
            cameraAup = default;
            IPlayerRuntimeContext player = _playerContext;
            if (player != null && player.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
            {
                cameraAup = snapshot.Aup;
                return cameraAup.IsFinite();
            }

            return false;
        }

        private bool IsAuthorizedPlayerRenderCamera(Camera renderCamera)
        {
            IPlayerRuntimeContext player = _playerContext;
            if (renderCamera == null || player == null || !player.IsInitialized)
                return false;

            Camera playerCamera = player.PlayerCamera;
            return playerCamera != null &&
                   ReferenceEquals(renderCamera, playerCamera) &&
                   renderCamera.isActiveAndEnabled;
        }

        private static float ResolvePlayerStress01(float oxygen01, float co201)
        {
            return math.saturate(math.max(1f - math.saturate(oxygen01), co201));
        }

        private static float ResolveGlobalQualityWeight01()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : 1f);
        }

        private static float ReadUIValue(UIValueSlotId slotId, float fallback)
        {
            if (!UIStateStore.TryReadValue(slotId, out UIValueSlot slot))
                return fallback;

            float value = slot.Value;
            return math.isfinite(value) ? value : fallback;
        }

        private static Matrix4x4 ResolveMaskMatrix(Camera renderCamera, FeatureSettings featureSettings)
        {
            Transform cameraTransform = renderCamera.transform;
            Vector3 position = featureSettings != null ? featureSettings.maskLocalPosition : new Vector3(0f, 0f, 0.38f);
            Vector3 euler = featureSettings != null ? featureSettings.maskLocalEuler : Vector3.zero;
            Vector3 scale = featureSettings != null ? featureSettings.maskLocalScale : new Vector3(0.92f, 0.58f, 1f);
            Matrix4x4 local = Matrix4x4.TRS(position, Quaternion.Euler(euler), scale);
            return cameraTransform.localToWorldMatrix * local;
        }

        private static bool IsUnsupportedCamera(ContextContainer frameData)
        {
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            return cameraData.cameraType == CameraType.Preview ||
                   cameraData.cameraType == CameraType.Reflection ||
                   cameraData.cameraType == CameraType.SceneView;
        }

        private void CacheColdServices(IPlayerRuntimeContext playerContext, IDataVault vault)
        {
            _playerContext = playerContext;
            BindDataVaultForLifecycle(vault);
        }

        private void ReleaseVaultHandles(IDataVault vault)
        {
            // Renderer features are secondary DataVault consumers. URP disposal can run while the
            // vault arena is resetting, so this lifecycle path detaches handles without freeing
            // vault-owned storage from inside RenderPipeline cleanup.
            _hudParamsHandle = default;
            _targetSourceHandle = default;
            _projectedTargetHandle = default;
            _digitParamsHandle = default;
            _telemetryHandle = default;
            _profileHandle = default;
            _csvScratchHandle = default;
            _telemetryDescriptorGeneration = 0u;
            _telemetryDumped = false;
        }

        private void BindDataVaultForLifecycle(IDataVault vault)
        {
            if (ReferenceEquals(_dataVault, vault))
                return;

            ReleaseVaultHandles(_dataVault);
            _dataVault = vault;
        }

        private static void ReleaseVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID expectedBufferId) where T : unmanaged
        {
            if (vault != null && IsOwnedHandle(in handle, expectedBufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private static bool IsOwnedHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId) where T : unmanaged
        {
            return handle.BufferID == unchecked((uint)(int)expectedBufferId) &&
                   handle.SystemID == (uint)SystemID.UI &&
                   handle.Generation != 0u;
        }

        private static void RecreateMaterial(ref Material material, Shader shader)
        {
            if (shader == null)
            {
                CoreUtils.Destroy(material);
                material = null;
                return;
            }

            if (material != null && material.shader == shader)
                return;

            CoreUtils.Destroy(material);
            material = CoreUtils.CreateEngineMaterial(shader);
        }

        private static void CopyHudParamsToMappedBuffer(
            in VisorHudParamsDTO source,
            NativeArray<VisorHudParamsDTO> destinationBuffer)
        {
            if (!destinationBuffer.IsCreated || destinationBuffer.Length <= 0)
                return;

            destinationBuffer[0] = source;
        }

        private static void CopyDigitParamsToMappedBuffer(
            in VisorHudDigitParamsDTO source,
            NativeArray<VisorHudDigitParamsDTO> destinationBuffer)
        {
            if (!destinationBuffer.IsCreated || destinationBuffer.Length <= 0)
                return;

            destinationBuffer[0] = source;
        }

        private static void CopyTargetsToMappedBuffer(
            ReadOnlySpan<VisorArTargetDTO> sourceBuffer,
            NativeArray<VisorArTargetDTO> destinationBuffer)
        {
            if (!destinationBuffer.IsCreated || destinationBuffer.Length <= 0)
                return;

            int targetCapacity = math.min(destinationBuffer.Length, VisorARStencilContracts.MaxTargets);
            int copyCount = math.min(sourceBuffer.Length, targetCapacity);
            for (int i = 0; i < copyCount; i++)
                destinationBuffer[i] = sourceBuffer[i];

            for (int i = copyCount; i < targetCapacity; i++)
                destinationBuffer[i] = default;
        }

        private static VisorHudParamsDTO GenerateMockHudData(float timeSeconds, in VisorHudParamsDTO input)
        {
            VisorHudParamsDTO value = input;
            float oxygen = math.saturate(0.01f + Triangle01(timeSeconds * 0.588f) * 0.98f);
            float co2 = math.saturate(0.02f + Triangle01(timeSeconds * 0.334f + 0.27f) * 0.96f);
            float toxicity = Triangle01(timeSeconds * 0.843f + 0.13f);
            float temperature = Triangle01(timeSeconds * 0.223f + 0.44f);
            float stress = math.saturate(math.max(1f - oxygen, co2) + toxicity * 0.35f);
            value.VitalStats = new float4(oxygen, co2, toxicity, temperature);
            value.VisorGlitchParams.x = stress;
            value.VisorGlitchParams.y = math.saturate(value.VisorGlitchParams.y + stress * 0.45f);
            return value;
        }

        private static VisorHudDigitParamsDTO BuildDigitParams(
            float oxygen01,
            float depthMeters,
            float pressureAtm,
            float stress01)
        {
            int oxygenPercent = ClampInt((int)math.round(math.saturate(oxygen01) * 100f), 0, 999);
            int depth = ClampInt((int)math.round(math.max(0f, depthMeters)), 0, 9999);
            int pressure = ClampInt((int)math.round(math.max(0f, pressureAtm) * 10f), 0, 999);
            int warning = ClampInt((int)math.round(math.saturate(stress01) * 999f), 0, 999);
            return new VisorHudDigitParamsDTO
            {
                OxygenDigits = Pack4Digits(oxygenPercent, 3),
                DepthDigits = Pack4Digits(depth, 4),
                PressureDigits = Pack4Digits(pressure, 3),
                WarningDigits = Pack4Digits(warning, 3)
            };
        }

        private static float4 Pack4Digits(int value, int digits)
        {
            int ones = value % 10;
            int tens = (value / 10) % 10;
            int hundreds = (value / 100) % 10;
            int thousands = (value / 1000) % 10;
            float blank = -1f;
            return new float4(
                digits >= 4 || thousands > 0 ? thousands : blank,
                digits >= 3 || hundreds > 0 ? hundreds : blank,
                digits >= 2 || tens > 0 ? tens : blank,
                ones);
        }

        private static int ClampInt(int value, int min, int max)
        {
            return value < min ? min : value > max ? max : value;
        }

        private static float Step01(float value, float threshold)
        {
            return value <= threshold ? 1f : 0f;
        }

        private static float Triangle01(float phase)
        {
            return math.abs(math.frac(phase) * 2f - 1f);
        }
    }
}

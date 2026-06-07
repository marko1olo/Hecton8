#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Hecton8.Editor
{
    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct HlodImpostorBakeSettings
    {
        [FieldOffset(0)]
        public FixedString64Bytes ProfileName;

        [FieldOffset(64)]
        public int ViewCount;

        [FieldOffset(68)]
        public int AtlasResolution;

        [FieldOffset(72)]
        public int DilationRadiusPixels;

        [FieldOffset(76)]
        public float ExtraPaddingMeters;

        [FieldOffset(80)]
        public float RealGeometryDistanceMeters;

        [FieldOffset(84)]
        public byte HemisphereOnly;

        [FieldOffset(85)]
        private byte _pad0;

        [FieldOffset(86)]
        private ushort _pad1;

        [FieldOffset(88)]
        private ulong _pad2;

        public static HlodImpostorBakeSettings CreateDefault()
        {
            return new HlodImpostorBakeSettings
            {
                ProfileName = new FixedString64Bytes("Massive_Wreck"),
                ViewCount = HectonOctahedralImpostorData.ViewCount,
                AtlasResolution = HectonOctahedralImpostorData.DefaultAtlasSize,
                DilationRadiusPixels = 4,
                ExtraPaddingMeters = 0.75f,
                RealGeometryDistanceMeters = HectonChunkImpostorResidency.DefaultImpostorEnterDistanceMeters,
                HemisphereOnly = 0
            };
        }
    }

    /// <summary>
    /// Offline HLOD impostor baker. Runtime never captures render textures; it consumes the generated atlas/material/quad only.
    /// </summary>
    public static unsafe class HectonOctahedralImpostorBaker
    {
        private const int BakeLayer = 31;
        private const int MaxViewCount = 64;
        private const uint PortableMaxComputeThreadsPerGroup = 256u;
        private const string OutputRoot = "Assets/_Project/BakedGeometry/Impostors";
        private const string ShaderPath = "Assets/_Project/Art/Shaders/Hecton_HLOD_Impostor.shader";
        private const string LegacyShaderPath = "Assets/_Project/Art/Shaders/Hecton_OctahedralImpostor.shader";
        private const string AlbedoAlphaShaderPath = "Assets/_Project/Art/Shaders/Hecton_EditorOctaImpostorAlbedoAlpha.shader";
        private const string NormalDepthShaderPath = "Assets/_Project/Art/Shaders/Hecton_EditorOctaImpostorNormalDepth.shader";
        private const string PackComputePath = "Assets/_Project/Art/Shaders/PackImpostorAtlas.compute";
        private const string DilateComputePath = "Assets/_Project/Art/Shaders/DilateImpostorEdges.compute";
        private const string BakeReportPath = "Docs/Reports/IMPOSTOR_BAKE_REPORT.json";
        private const string NativeMemoryOwner = nameof(HectonOctahedralImpostorBaker);

        private static readonly int SourceAlbedoId = Shader.PropertyToID("_SourceAlbedo");
        private static readonly int SourceNormalDepthId = Shader.PropertyToID("_SourceNormalDepth");
        private static readonly int AtlasAlbedoDepthId = Shader.PropertyToID("_AtlasAlbedoDepth");
        private static readonly int AtlasNormalXYId = Shader.PropertyToID("_AtlasNormalXY");
        private static readonly int ViewIndexId = Shader.PropertyToID("_ViewIndex");
        private static readonly int TileSizeId = Shader.PropertyToID("_TileSize");
        private static readonly int AtlasGridId = Shader.PropertyToID("_AtlasGrid");
        private static readonly int SourceAtlasId = Shader.PropertyToID("_SourceAtlas");
        private static readonly int SourceMaskAtlasId = Shader.PropertyToID("_SourceMaskAtlas");
        private static readonly int OutputAtlasId = Shader.PropertyToID("_OutputAtlas");
        private static readonly int AtlasSizeId = Shader.PropertyToID("_AtlasSize");
        private static readonly int DilationRadiusId = Shader.PropertyToID("_DilationRadius");
        private static readonly int ImpostorAtlasGridId = Shader.PropertyToID("_HectonImpostorAtlasGrid");
        private static readonly int ImpostorDepthScaleId = Shader.PropertyToID("_HectonImpostorDepthScaleMeters");
        private static readonly int GlobalQualityWeightId = Shader.PropertyToID("_HectonGlobalQualityWeight");

        [MenuItem("HECTON-8/Rendering/HLOD Impostor/Bake Selected", false, 2500)]
        public static void BakeSelected()
        {
            GameObject source = Selection.activeGameObject;
            if (source == null)
            {
                EditorUtility.DisplayDialog("HLOD Impostor Baker", "Select a GameObject with renderers.", "OK");
                return;
            }

            BakeGameObject(source, HlodImpostorBakeSettings.CreateDefault(), null);
        }

        [MenuItem("HECTON-8/Rendering/HLOD Impostor/Bake Selected", true)]
        private static bool ValidateBakeSelected()
        {
            return Selection.activeGameObject != null;
        }

        [MenuItem("HECTON-8/Rendering/HLOD Impostor/Mock Capture Benchmark", false, 2501)]
        public static void RunMockCaptureBenchmark()
        {
            const int pointCount = 65536;
            NativeArray<HlodImpostorMockPoint> points = AllocateTrackedNativeArray<HlodImpostorMockPoint>(
                pointCount,
                Allocator.TempJob,
                NativeArrayOptions.UninitializedMemory,
                nameof(points));
            NativeArray<HlodImpostorCaptureAngleRecord> records = AllocateTrackedNativeArray<HlodImpostorCaptureAngleRecord>(
                HectonOctahedralImpostorData.ViewCount,
                Allocator.TempJob,
                NativeArrayOptions.UninitializedMemory,
                nameof(records));

            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                new GenerateMockCaptureTargetJob
                {
                    Points = points,
                    Center = float3.zero,
                    Extents = new float3(75f, 25f, 220f),
                    StableSeed = 0x5348494Eu,
                    GlobalQualityWeight = HomeostasisBrain.GlobalQualityWeight
                }.Schedule(pointCount, 128).Complete();

                new CalculateCaptureAnglesJob
                {
                    OutputRecords = records,
                    BoundsCenter = float3.zero,
                    BoundsExtents = new float3(75f, 25f, 220f),
                    ViewCount = records.Length,
                    HemisphereOnly = 0,
                    ExtraPaddingMeters = 0.75f,
                    NearClipMeters = 0.01f
                }.Schedule().Complete();
            }
            finally
            {
                stopwatch.Stop();
                DisposeTrackedNativeArray(ref records);
                DisposeTrackedNativeArray(ref points);
            }

            Debug.Log("SHINOBU_212 mock capture benchmark: " + pointCount.ToString(CultureInfo.InvariantCulture) +
                      " points + 16 angles in " + stopwatch.Elapsed.TotalMilliseconds.ToString("0.000", CultureInfo.InvariantCulture) + " ms");
        }

        public static bool BakeGameObject(GameObject source, HlodImpostorBakeSettings settings, Action<string, float> progress)
        {
            HlodImpostorStaticValidators.ValidateLayouts(false);
            HlodImpostorStaticValidators.RunStaticArchaeology(false);

            if (source == null || !TryCalculateRendererBounds(source, out Bounds sourceBounds))
            {
                EditorUtility.DisplayDialog("HLOD Impostor Baker", "Source has no renderer bounds.", "OK");
                return false;
            }

            Shader albedoAlphaShader = AssetDatabase.LoadAssetAtPath<Shader>(AlbedoAlphaShaderPath);
            Shader normalDepthShader = AssetDatabase.LoadAssetAtPath<Shader>(NormalDepthShaderPath);
            Shader impostorShader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (impostorShader == null)
                impostorShader = AssetDatabase.LoadAssetAtPath<Shader>(LegacyShaderPath);
            ComputeShader packCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(PackComputePath);
            ComputeShader dilateCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(DilateComputePath);
            if (albedoAlphaShader == null || normalDepthShader == null || impostorShader == null || packCompute == null || dilateCompute == null)
            {
                EditorUtility.DisplayDialog("HLOD Impostor Baker", "Required impostor shaders or compute kernels are missing.", "OK");
                return false;
            }

            settings.ViewCount = math.clamp(settings.ViewCount <= 0 ? HectonOctahedralImpostorData.ViewCount : settings.ViewCount, 1, MaxViewCount);
            settings.AtlasResolution = AlignAtlasResolution(settings.AtlasResolution);
            settings.DilationRadiusPixels = math.clamp(settings.DilationRadiusPixels, 0, 32);
            settings.RealGeometryDistanceMeters = math.max(1f, settings.RealGeometryDistanceMeters);
            if (settings.ProfileName.Length == 0)
                settings.ProfileName = new FixedString64Bytes("Massive_Wreck");

            Vector2Int grid = ResolveGrid(settings.ViewCount);
            int tileWidth = math.max(16, settings.AtlasResolution / grid.x);
            int tileHeight = math.max(16, settings.AtlasResolution / grid.y);
            string safeName = SanitizeAssetName(source.name);
            string folder = EnsureOutputFolder(safeName);
            string albedoPath = folder + "/TX_" + safeName + "_ImpostorAlbedoDepth.png";
            string normalPath = folder + "/TX_" + safeName + "_ImpostorNormalXY.png";
            string dataPath = folder + "/ImpostorData_" + safeName + ".asset";
            string materialPath = folder + "/MAT_" + safeName + "_HLODImpostor.mat";
            string meshPath = folder + "/MSH_" + safeName + "_ImpostorQuad.asset";

            RenderTexture captureAlbedo = null;
            RenderTexture captureNormalDepth = null;
            RenderTexture atlasAlbedoDepth = null;
            RenderTexture atlasNormalXY = null;
            RenderTexture dilatedAlbedoDepth = null;
            RenderTexture dilatedNormalXY = null;
            GameObject clone = null;
            Camera bakeCamera = null;
            NativeArray<HlodImpostorCaptureAngleRecord> records = default;
            Stopwatch stopwatch = Stopwatch.StartNew();
            long packTicks = 0L;

            try
            {
                clone = Object.Instantiate(source);
                clone.name = source.name + "_SHINOBU_212_BakeClone";
                clone.hideFlags = HideFlags.HideAndDontSave;
                StripBehaviours(clone);
                ForceHighestLod(clone);
                SetHideFlagsAndLayer(clone.transform, BakeLayer);

                if (!TryCalculateRendererBounds(clone, out Bounds cloneBounds))
                    return false;

                clone.transform.position -= cloneBounds.center;
                if (!TryCalculateRendererBounds(clone, out Bounds bakeBounds))
                    return false;

                records = AllocateTrackedNativeArray<HlodImpostorCaptureAngleRecord>(
                    settings.ViewCount,
                    Allocator.TempJob,
                    NativeArrayOptions.UninitializedMemory,
                    nameof(records));
                new CalculateCaptureAnglesJob
                {
                    OutputRecords = records,
                    BoundsCenter = new float3(bakeBounds.center.x, bakeBounds.center.y, bakeBounds.center.z),
                    BoundsExtents = new float3(bakeBounds.extents.x, bakeBounds.extents.y, bakeBounds.extents.z),
                    ViewCount = settings.ViewCount,
                    HemisphereOnly = settings.HemisphereOnly,
                    ExtraPaddingMeters = settings.ExtraPaddingMeters,
                    NearClipMeters = 0.01f
                }.Schedule().Complete();

                bakeCamera = CreateBakeCamera();
                captureAlbedo = CreateCaptureTexture(tileWidth, tileHeight, GraphicsFormat.R8G8B8A8_UNorm, "H8 Capture AlbedoDepth");
                captureNormalDepth = CreateCaptureTexture(tileWidth, tileHeight, GraphicsFormat.R16G16B16A16_SFloat, "H8 Capture NormalDepth");
                atlasAlbedoDepth = CreateAtlasTexture(settings.AtlasResolution, settings.AtlasResolution, "H8 Atlas AlbedoDepth");
                atlasNormalXY = CreateAtlasTexture(settings.AtlasResolution, settings.AtlasResolution, "H8 Atlas NormalXY");
                dilatedAlbedoDepth = CreateAtlasTexture(settings.AtlasResolution, settings.AtlasResolution, "H8 Dilated AlbedoDepth");
                dilatedNormalXY = CreateAtlasTexture(settings.AtlasResolution, settings.AtlasResolution, "H8 Dilated NormalXY");
                ClearAtlas(atlasAlbedoDepth);
                ClearAtlas(atlasNormalXY);
                progress?.Invoke("Capturing", 0.05f);

                if (!SystemInfo.supportsComputeShaders)
                    return false;

                if (!TryFindKernel(packCompute, "CSMain", out int packKernel))
                    return false;

                ResolveKernelThreadGroupSizes(packCompute, packKernel, out int packThreadGroupSizeX, out int packThreadGroupSizeY);
                if (packThreadGroupSizeX <= 0 || packThreadGroupSizeY <= 0)
                    return false;

                for (int i = 0; i < settings.ViewCount; i++)
                {
                    HlodImpostorCaptureAngleRecord record = records[i];
                    ApplyCameraRecord(bakeCamera, in record, bakeBounds);
                    RenderReplacementTo(bakeCamera, captureAlbedo, albedoAlphaShader);
                    RenderReplacementTo(bakeCamera, captureNormalDepth, normalDepthShader);

                    Stopwatch packWatch = Stopwatch.StartNew();
                    PackCapture(packCompute, packKernel, captureAlbedo, captureNormalDepth, atlasAlbedoDepth, atlasNormalXY, i, tileWidth, tileHeight, grid, packThreadGroupSizeX, packThreadGroupSizeY);
                    packWatch.Stop();
                    packTicks += packWatch.ElapsedTicks;
                    progress?.Invoke("Packing view " + (i + 1).ToString(CultureInfo.InvariantCulture), 0.05f + 0.65f * ((i + 1f) / settings.ViewCount));
                }

                if (!TryFindKernel(dilateCompute, "CSMain", out int dilateKernel))
                    return false;

                ResolveKernelThreadGroupSizes(dilateCompute, dilateKernel, out int dilateThreadGroupSizeX, out int dilateThreadGroupSizeY);
                if (dilateThreadGroupSizeX <= 0 || dilateThreadGroupSizeY <= 0)
                    return false;

                DilateAtlas(dilateCompute, dilateKernel, atlasAlbedoDepth, atlasAlbedoDepth, dilatedAlbedoDepth, settings.AtlasResolution, settings.DilationRadiusPixels, dilateThreadGroupSizeX, dilateThreadGroupSizeY);
                DilateAtlas(dilateCompute, dilateKernel, atlasNormalXY, atlasNormalXY, dilatedNormalXY, settings.AtlasResolution, settings.DilationRadiusPixels, dilateThreadGroupSizeX, dilateThreadGroupSizeY);
                progress?.Invoke("Async readback queued", 0.78f);

                float radius = Mathf.Max(0.5f, bakeBounds.extents.magnitude);
                float farClip = Mathf.Max(8f, radius * 6f);
                long sourceMeshBytes = EstimateMeshBytes(source);
                CreateOrUpdateQuadMesh(meshPath, sourceBounds);
                PendingBake pending = new PendingBake
                {
                    SourceName = safeName,
                    Folder = folder,
                    AlbedoAssetPath = albedoPath,
                    NormalAssetPath = normalPath,
                    DataAssetPath = dataPath,
                    MaterialAssetPath = materialPath,
                    MeshAssetPath = meshPath,
                    ImpostorShader = impostorShader,
                    SourceBounds = sourceBounds,
                    PivotOffset = sourceBounds.center - source.transform.position,
                    AtlasSize = settings.AtlasResolution,
                    Grid = grid,
                    ViewCount = settings.ViewCount,
                    CaptureOrthoSize = radius,
                    CaptureDepthMeters = farClip,
                    DilationRadiusPixels = settings.DilationRadiusPixels,
                    RealGeometryDistanceMeters = settings.RealGeometryDistanceMeters,
                    ProfileName = settings.ProfileName,
                    SourceMeshBytes = sourceMeshBytes,
                    PackMicroseconds = TicksToMicroseconds(packTicks),
                    TotalStopwatch = stopwatch,
                    Progress = progress,
                    AlbedoReadbackSource = dilatedAlbedoDepth,
                    NormalReadbackSource = dilatedNormalXY,
                    OwnedCaptureAlbedo = captureAlbedo,
                    OwnedCaptureNormalDepth = captureNormalDepth,
                    OwnedAtlasAlbedoDepth = atlasAlbedoDepth,
                    OwnedAtlasNormalXY = atlasNormalXY,
                    OwnedDilatedAlbedoDepth = dilatedAlbedoDepth,
                    OwnedDilatedNormalXY = dilatedNormalXY
                };

                captureAlbedo = null;
                captureNormalDepth = null;
                atlasAlbedoDepth = null;
                atlasNormalXY = null;
                dilatedAlbedoDepth = null;
                dilatedNormalXY = null;
                RequestAsyncWritebacks(pending);
                progress?.Invoke("Waiting for GPU readback", 0.82f);
                return true;
            }
            finally
            {
                DisposeTrackedNativeArray(ref records);
                if (bakeCamera != null)
                    Object.DestroyImmediate(bakeCamera.gameObject);
                if (clone != null)
                    Object.DestroyImmediate(clone);
                ReleaseRenderTexture(captureAlbedo);
                ReleaseRenderTexture(captureNormalDepth);
                ReleaseRenderTexture(atlasAlbedoDepth);
                ReleaseRenderTexture(atlasNormalXY);
                ReleaseRenderTexture(dilatedAlbedoDepth);
                ReleaseRenderTexture(dilatedNormalXY);
            }
        }

        public static int BakePrefabFolder(string folderAssetPath, HlodImpostorBakeSettings settings, Action<string, float> progress)
        {
            if (string.IsNullOrEmpty(folderAssetPath) || !AssetDatabase.IsValidFolder(folderAssetPath))
                return 0;

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderAssetPath });
            int launched = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                if (BakeGameObject(prefab, settings, progress))
                    launched++;
            }

            return launched;
        }

        public static bool TryBuildPreviewAngles(GameObject source, int viewCount, byte hemisphereOnly, NativeArray<HlodImpostorCaptureAngleRecord> records)
        {
            if (source == null || !records.IsCreated || !TryCalculateRendererBounds(source, out Bounds bounds))
                return false;

            new CalculateCaptureAnglesJob
            {
                OutputRecords = records,
                BoundsCenter = new float3(bounds.center.x, bounds.center.y, bounds.center.z),
                BoundsExtents = new float3(bounds.extents.x, bounds.extents.y, bounds.extents.z),
                ViewCount = math.min(viewCount, records.Length),
                HemisphereOnly = hemisphereOnly,
                ExtraPaddingMeters = 0.75f,
                NearClipMeters = 0.01f
            }.Schedule().Complete();
            return true;
        }

        private static void RequestAsyncWritebacks(PendingBake pending)
        {
            AsyncGPUReadback.Request(pending.AlbedoReadbackSource, 0, TextureFormat.RGBA32, request =>
            {
                pending.AlbedoDone = true;
                pending.AlbedoError = request.hasError;
                if (!request.hasError)
                    WritePngFromReadback(request, pending.AlbedoAssetPath, pending.AtlasSize, pending.AtlasSize);
                TryFinalizePending(pending);
            });

            AsyncGPUReadback.Request(pending.NormalReadbackSource, 0, TextureFormat.RGBA32, request =>
            {
                pending.NormalDone = true;
                pending.NormalError = request.hasError;
                if (!request.hasError)
                    WritePngFromReadback(request, pending.NormalAssetPath, pending.AtlasSize, pending.AtlasSize);
                TryFinalizePending(pending);
            });
        }

        private static void TryFinalizePending(PendingBake pending)
        {
            if (!pending.AlbedoDone || !pending.NormalDone)
                return;

            pending.TotalStopwatch.Stop();
            try
            {
                if (pending.AlbedoError || pending.NormalError)
                {
                    WriteBakeReport(pending, "CRITICAL_WARNING", "AsyncGPUReadback failed.");
                    pending.Progress?.Invoke("Readback failed", 1f);
                    return;
                }

                AssetDatabase.ImportAsset(pending.AlbedoAssetPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(pending.NormalAssetPath, ImportAssetOptions.ForceUpdate);
                ConfigureTextureImporter(pending.AlbedoAssetPath, pending.AtlasSize, true);
                ConfigureTextureImporter(pending.NormalAssetPath, pending.AtlasSize, false);

                Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(pending.AlbedoAssetPath);
                Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(pending.NormalAssetPath);
                HectonOctahedralImpostorData data = AssetDatabase.LoadAssetAtPath<HectonOctahedralImpostorData>(pending.DataAssetPath);
                if (data == null)
                {
                    data = ScriptableObject.CreateInstance<HectonOctahedralImpostorData>();
                    AssetDatabase.CreateAsset(data, pending.DataAssetPath);
                }

                data.Configure(
                    albedo,
                    normal,
                    pending.SourceBounds,
                    pending.PivotOffset,
                    pending.AtlasSize,
                    pending.CaptureOrthoSize,
                    pending.CaptureDepthMeters,
                    pending.RealGeometryDistanceMeters,
                    pending.DilationRadiusPixels,
                    pending.CaptureDepthMeters,
                    pending.ProfileName.ToString(),
                    pending.Grid,
                    pending.ViewCount);
                EditorUtility.SetDirty(data);
                CreateOrUpdateMaterial(pending.MaterialAssetPath, pending.ImpostorShader, albedo, normal, pending.Grid, pending.CaptureDepthMeters);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                WriteBakeReport(pending, ResolveBudgetStatus(pending), string.Empty);
                Selection.activeObject = data;
                pending.Progress?.Invoke("Complete", 1f);
            }
            finally
            {
                pending.Release();
            }
        }

        private static void WritePngFromReadback(AsyncGPUReadbackRequest request, string assetPath, int width, int height)
        {
            NativeArray<byte> raw = request.GetData<byte>();
            NativeArray<byte> png = ImageConversion.EncodeNativeArrayToPNG(
                raw,
                GraphicsFormat.R8G8B8A8_UNorm,
                (uint)width,
                (uint)height);
            try
            {
                RegisterTrackedNativeArray(ref png, NativeAllocationLifetime.TempJob, nameof(png));
                WriteNativeBytes(ToFullPath(assetPath), png);
            }
            finally
            {
                DisposeTrackedNativeArray(ref png);
            }
        }

        private static void PackCapture(
            ComputeShader compute,
            int kernel,
            RenderTexture sourceAlbedo,
            RenderTexture sourceNormalDepth,
            RenderTexture atlasAlbedoDepth,
            RenderTexture atlasNormalXY,
            int viewIndex,
            int tileWidth,
            int tileHeight,
            Vector2Int grid,
            int threadGroupSizeX,
            int threadGroupSizeY)
        {
            CommandBuffer cmd = new CommandBuffer { name = "SHINOBU_212 Pack Impostor Atlas" };
            try
            {
                if (!SystemInfo.supportsComputeShaders)
                    return;

                int groupCountX = CeilDividePositive(tileWidth, threadGroupSizeX);
                int groupCountY = CeilDividePositive(tileHeight, threadGroupSizeY);
                if (groupCountX <= 0 || groupCountY <= 0)
                    return;

                cmd.SetComputeTextureParam(compute, kernel, SourceAlbedoId, sourceAlbedo);
                cmd.SetComputeTextureParam(compute, kernel, SourceNormalDepthId, sourceNormalDepth);
                cmd.SetComputeTextureParam(compute, kernel, AtlasAlbedoDepthId, atlasAlbedoDepth);
                cmd.SetComputeTextureParam(compute, kernel, AtlasNormalXYId, atlasNormalXY);
                cmd.SetComputeIntParam(compute, ViewIndexId, viewIndex);
                cmd.SetComputeVectorParam(compute, TileSizeId, new Vector4(tileWidth, tileHeight, 0f, 0f));
                cmd.SetComputeVectorParam(compute, AtlasGridId, new Vector4(grid.x, grid.y, 0f, 0f));
                cmd.DispatchCompute(compute, kernel, groupCountX, groupCountY, 1);
                UnityEngine.Graphics.ExecuteCommandBuffer(cmd);
            }
            finally
            {
                cmd.Release();
            }
        }

        private static void DilateAtlas(
            ComputeShader compute,
            int kernel,
            RenderTexture source,
            RenderTexture mask,
            RenderTexture output,
            int atlasSize,
            int radius,
            int threadGroupSizeX,
            int threadGroupSizeY)
        {
            CommandBuffer cmd = new CommandBuffer { name = "SHINOBU_212 Dilate Impostor Atlas" };
            try
            {
                if (!SystemInfo.supportsComputeShaders)
                    return;

                int groupCountX = CeilDividePositive(atlasSize, threadGroupSizeX);
                int groupCountY = CeilDividePositive(atlasSize, threadGroupSizeY);
                if (groupCountX <= 0 || groupCountY <= 0)
                    return;

                cmd.SetComputeTextureParam(compute, kernel, SourceAtlasId, source);
                cmd.SetComputeTextureParam(compute, kernel, SourceMaskAtlasId, mask);
                cmd.SetComputeTextureParam(compute, kernel, OutputAtlasId, output);
                cmd.SetComputeVectorParam(compute, AtlasSizeId, new Vector4(atlasSize, atlasSize, 0f, 0f));
                cmd.SetComputeIntParam(compute, DilationRadiusId, radius);
                cmd.DispatchCompute(compute, kernel, groupCountX, groupCountY, 1);
                UnityEngine.Graphics.ExecuteCommandBuffer(cmd);
            }
            finally
            {
                cmd.Release();
            }
        }

        private static void ResolveKernelThreadGroupSizes(
            ComputeShader compute,
            int kernel,
            out int threadGroupSizeX,
            out int threadGroupSizeY)
        {
            threadGroupSizeX = 0;
            threadGroupSizeY = 0;
            if (compute == null || kernel < 0 || !SystemInfo.supportsComputeShaders)
                return;

            uint queryX;
            uint queryY;
            uint queryZ;
            try
            {
                if (!compute.IsSupported(kernel))
                    return;

                compute.GetKernelThreadGroupSizes(kernel, out queryX, out queryY, out queryZ);
            }
            catch (System.ObjectDisposedException)
            {
                return;
            }
            catch (System.InvalidOperationException)
            {
                return;
            }
            catch (System.ArgumentException)
            {
                return;
            }
            catch (UnityEngine.MissingReferenceException)
            {
                return;
            }
            catch (UnityEngine.UnityException)
            {
                return;
            }
            if (queryX == 0u || queryY == 0u || queryZ != 1u || queryX > int.MaxValue || queryY > int.MaxValue)
                return;

            ulong totalThreads = queryX * (ulong)queryY * queryZ;
            if (totalThreads > PortableMaxComputeThreadsPerGroup)
                return;

            threadGroupSizeX = (int)queryX;
            threadGroupSizeY = (int)queryY;
        }

        private static bool TryFindKernel(ComputeShader compute, string kernelName, out int kernel)
        {
            kernel = -1;
            if (compute == null)
                return false;

            try
            {
                if (!compute.HasKernel(kernelName))
                    return false;

                kernel = compute.FindKernel(kernelName);
                return kernel >= 0;
            }
            catch (ObjectDisposedException)
            {
                kernel = -1;
                return false;
            }
            catch (InvalidOperationException)
            {
                kernel = -1;
                return false;
            }
            catch (ArgumentException)
            {
                kernel = -1;
                return false;
            }
            catch (UnityEngine.MissingReferenceException)
            {
                kernel = -1;
                return false;
            }
            catch (UnityEngine.UnityException)
            {
                kernel = -1;
                return false;
            }
        }

        private static int CeilDividePositive(int value, int divisor)
        {
            const int MaxDispatchGroupsPerDimension = 65535;
            if (value <= 0 || divisor <= 0)
                return 0;

            long groups = ((long)value + divisor - 1L) / divisor;
            return groups <= MaxDispatchGroupsPerDimension ? (int)groups : 0;
        }

        private static void RenderReplacementTo(Camera camera, RenderTexture target, Shader replacementShader)
        {
            CommandBuffer cmd = new CommandBuffer { name = "SHINOBU_212 Clear Capture RT" };
            try
            {
                cmd.SetRenderTarget(target);
                cmd.ClearRenderTarget(true, true, Color.clear);
                UnityEngine.Graphics.ExecuteCommandBuffer(cmd);
            }
            finally
            {
                cmd.Release();
            }

            camera.targetTexture = target;
            camera.RenderWithShader(replacementShader, string.Empty);
        }

        private static void ClearAtlas(RenderTexture target)
        {
            CommandBuffer cmd = new CommandBuffer { name = "SHINOBU_212 Clear Impostor Atlas" };
            try
            {
                cmd.SetRenderTarget(target);
                cmd.ClearRenderTarget(false, true, Color.clear);
                UnityEngine.Graphics.ExecuteCommandBuffer(cmd);
            }
            finally
            {
                cmd.Release();
            }
        }

        private static Camera CreateBakeCamera()
        {
            GameObject cameraObject = new GameObject("SHINOBU_212 Impostor Bake Camera")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.cameraType = CameraType.Preview;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.orthographic = true;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.cullingMask = 1 << BakeLayer;
            return camera;
        }

        private static void ApplyCameraRecord(Camera camera, in HlodImpostorCaptureAngleRecord record, Bounds bounds)
        {
            Vector3 direction = new Vector3(record.Direction.x, record.Direction.y, record.Direction.z);
            Vector3 position = new Vector3(record.CameraPosition.x, record.CameraPosition.y, record.CameraPosition.z);
            Vector3 up = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.96f ? Vector3.forward : Vector3.up;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = Mathf.Max(record.CameraDistance + bounds.extents.magnitude * 3.5f, 8f);
            camera.orthographicSize = Mathf.Max(0.5f, record.OrthoSize);
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(bounds.center - position, up));
        }

        private static RenderTexture CreateCaptureTexture(int width, int height, GraphicsFormat format, string name)
        {
            RenderTextureDescriptor descriptor = new RenderTextureDescriptor(width, height)
            {
                graphicsFormat = format,
                depthBufferBits = 24,
                msaaSamples = 1,
                mipCount = 1,
                sRGB = format == GraphicsFormat.R8G8B8A8_UNorm,
                useMipMap = false,
                autoGenerateMips = false,
                enableRandomWrite = false
            };
            RenderTexture texture = new RenderTexture(descriptor)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.Create();
            return texture;
        }

        private static RenderTexture CreateAtlasTexture(int width, int height, string name)
        {
            RenderTextureDescriptor descriptor = new RenderTextureDescriptor(width, height)
            {
                graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm,
                depthBufferBits = 0,
                msaaSamples = 1,
                mipCount = 1,
                sRGB = false,
                useMipMap = false,
                autoGenerateMips = false,
                enableRandomWrite = true
            };
            RenderTexture texture = new RenderTexture(descriptor)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.Create();
            return texture;
        }

        private static void ConfigureTextureImporter(string assetPath, int atlasSize, bool sRgb)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return;

            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = true;
            importer.sRGBTexture = sRgb;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = atlasSize;
            TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
            standalone.overridden = true;
            standalone.maxTextureSize = atlasSize;
            standalone.format = TextureImporterFormat.BC7;
            standalone.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SetPlatformTextureSettings(standalone);
            importer.SaveAndReimport();
        }

        private static void CreateOrUpdateMaterial(
            string materialPath,
            Shader shader,
            Texture2D albedo,
            Texture2D normal,
            Vector2Int grid,
            float depthScaleMeters)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.SetTexture("_ImpostorAlbedoDepthAtlas", albedo);
            material.SetTexture("_ImpostorNormalDepthAtlas", normal);
            material.SetVector(ImpostorAtlasGridId, new Vector4(grid.x, grid.y, 1f / Mathf.Max(1, grid.x), 1f / Mathf.Max(1, grid.y)));
            material.SetFloat(ImpostorDepthScaleId, Mathf.Max(0.01f, depthScaleMeters));
            material.SetFloat(GlobalQualityWeightId, 1f);
            material.SetFloat("_AlphaClipThreshold", 0.003f);
            EditorUtility.SetDirty(material);
        }

        private static void CreateOrUpdateQuadMesh(string meshPath, Bounds sourceBounds)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = Path.GetFileNameWithoutExtension(meshPath) };
                AssetDatabase.CreateAsset(mesh, meshPath);
            }

            mesh.Clear();
            float width = Mathf.Max(0.5f, Mathf.Max(sourceBounds.size.x, sourceBounds.size.z));
            float height = Mathf.Max(0.5f, sourceBounds.size.y);
            mesh.vertices = new[]
            {
                new Vector3(-0.5f * width, -0.5f * height, 0f),
                new Vector3(0.5f * width, -0.5f * height, 0f),
                new Vector3(0.5f * width, 0.5f * height, 0f),
                new Vector3(-0.5f * width, 0.5f * height, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
        }

        private static bool TryCalculateRendererBounds(GameObject root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bounds = default;
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                if (hasBounds)
                    bounds.Encapsulate(renderer.bounds);
                else
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
            }

            return hasBounds;
        }

        private static long EstimateMeshBytes(GameObject root)
        {
            long bytes = 0L;
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                Mesh mesh = filters[i] != null ? filters[i].sharedMesh : null;
                if (mesh == null)
                    continue;

                bytes += (long)mesh.vertexCount * 48L;
                for (int s = 0; s < mesh.subMeshCount; s++)
                    bytes += (long)mesh.GetIndexCount(s) * 4L;
            }

            return bytes;
        }

        private static void StripBehaviours(GameObject root)
        {
            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != null)
                    Object.DestroyImmediate(behaviours[i]);
            }
        }

        private static void ForceHighestLod(GameObject root)
        {
            LODGroup[] lodGroups = root.GetComponentsInChildren<LODGroup>(true);
            for (int i = 0; i < lodGroups.Length; i++)
            {
                if (lodGroups[i] != null)
                    lodGroups[i].ForceLOD(0);
            }
        }

        private static void SetHideFlagsAndLayer(Transform root, int layer)
        {
            root.gameObject.hideFlags = HideFlags.HideAndDontSave;
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
                SetHideFlagsAndLayer(root.GetChild(i), layer);
        }

        private static int AlignAtlasResolution(int resolution)
        {
            int safe = math.max(512, resolution);
            if (safe <= 1024)
                return 1024;
            if (safe <= 2048)
                return 2048;
            if (safe <= 4096)
                return 4096;
            return 8192;
        }

        private static Vector2Int ResolveGrid(int viewCount)
        {
            if (viewCount <= 16)
                return new Vector2Int(4, 4);

            int columns = Mathf.CeilToInt(Mathf.Sqrt(viewCount));
            int rows = Mathf.CeilToInt(viewCount / (float)columns);
            return new Vector2Int(Mathf.Max(1, columns), Mathf.Max(1, rows));
        }

        private static string EnsureOutputFolder(string safeName)
        {
            EnsureFolder("Assets/_Project", "BakedGeometry");
            EnsureFolder("Assets/_Project/BakedGeometry", "Impostors");
            EnsureFolder(OutputRoot, safeName);
            return OutputRoot + "/" + safeName;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }

        private static string SanitizeAssetName(string source)
        {
            char[] chars = source.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                bool valid =
                    (c >= 'a' && c <= 'z') ||
                    (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') ||
                    c == '_' ||
                    c == '-';
                if (!valid)
                    chars[i] = '_';
            }

            return chars.Length > 0 ? new string(chars) : "Selection";
        }

        private static string ToFullPath(string assetOrProjectPath)
        {
            return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), assetOrProjectPath));
        }

        private static NativeArray<T> AllocateTrackedNativeArray<T>(int length, Allocator allocator, NativeArrayOptions options, string label) where T : struct
        {
            if (length <= 0)
                return default;

            NativeArray<T> array = new NativeArray<T>(length, allocator, options);
            if (!array.IsCreated)
                throw new InvalidOperationException("[HectonOctahedralImpostorBaker] NativeArray allocation failed for " + label + ".");

            RegisterTrackedNativeArray(ref array, ResolveNativeAllocationLifetime(allocator), label);
            return array;
        }

        private static void RegisterTrackedNativeArray<T>(ref NativeArray<T> array, NativeAllocationLifetime lifetime, string label) where T : struct
        {
            if (!array.IsCreated)
                return;

            try
            {
                int sentinelId = NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, lifetime);
                if (sentinelId <= 0)
                    throw new InvalidOperationException("[HectonOctahedralImpostorBaker] NativeMemorySentinel rejected NativeArray registration for " + label + ".");
            }
            catch
            {
                array.Dispose();
                array = default;
                throw;
            }
        }

        private static void DisposeTrackedNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            try
            {
                NativeMemorySentinel.UnregisterNativeArray(array);
            }
            finally
            {
                array.Dispose();
                array = default;
            }
        }

        private static NativeAllocationLifetime ResolveNativeAllocationLifetime(Allocator allocator)
        {
            switch (allocator)
            {
                case Allocator.Temp:
                    return NativeAllocationLifetime.Temp;
                case Allocator.TempJob:
                    return NativeAllocationLifetime.TempJob;
                case Allocator.Persistent:
                    return NativeAllocationLifetime.Session;
                default:
                    return NativeAllocationLifetime.Session;
            }
        }

        private static void WriteNativeBytes(string fullPath, NativeArray<byte> bytes)
        {
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            void* ptr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(bytes);
            using (FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024))
            {
                stream.Write(new ReadOnlySpan<byte>(ptr, bytes.Length));
            }
        }

        private static void WriteBakeReport(PendingBake pending, string status, string message)
        {
            string fullPath = ToFullPath(BakeReportPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            long atlasBytesBc7 = (long)pending.AtlasSize * pending.AtlasSize * 2L;
            long savedBytes = Math.Max(0L, pending.SourceMeshBytes - atlasBytesBc7);
            string warning = atlasBytesBc7 > 16L * 1024L * 1024L ? "CRITICAL_WARNING" : status;
            string report = string.Concat(
                "{\n",
                "  \"agent\": \"SHINOBU_212\",\n",
                "  \"status\": \"", EscapeJson(warning), "\",\n",
                "  \"message\": \"", EscapeJson(message), "\",\n",
                "  \"objects_processed\": 1,\n",
                "  \"source\": \"", EscapeJson(pending.SourceName), "\",\n",
                "  \"profile\": \"", EscapeJson(pending.ProfileName.ToString()), "\",\n",
                "  \"view_count\": ", pending.ViewCount.ToString(CultureInfo.InvariantCulture), ",\n",
                "  \"atlas_resolution\": ", pending.AtlasSize.ToString(CultureInfo.InvariantCulture), ",\n",
                "  \"atlas_grid\": [", pending.Grid.x.ToString(CultureInfo.InvariantCulture), ", ", pending.Grid.y.ToString(CultureInfo.InvariantCulture), "],\n",
                "  \"albedo_depth_path\": \"", EscapeJson(pending.AlbedoAssetPath), "\",\n",
                "  \"normal_xy_path\": \"", EscapeJson(pending.NormalAssetPath), "\",\n",
                "  \"source_mesh_estimated_bytes\": ", pending.SourceMeshBytes.ToString(CultureInfo.InvariantCulture), ",\n",
                "  \"impostor_bc7_estimated_bytes\": ", atlasBytesBc7.ToString(CultureInfo.InvariantCulture), ",\n",
                "  \"memory_footprint_saved_bytes\": ", savedBytes.ToString(CultureInfo.InvariantCulture), ",\n",
                "  \"gpu_pack_microseconds\": ", pending.PackMicroseconds.ToString("0.00", CultureInfo.InvariantCulture), ",\n",
                "  \"total_elapsed_microseconds\": ", (pending.TotalStopwatch.Elapsed.TotalMilliseconds * 1000.0).ToString("0.00", CultureInfo.InvariantCulture), "\n",
                "}\n");
            File.WriteAllText(fullPath, report);
        }

        private static string ResolveBudgetStatus(PendingBake pending)
        {
            long atlasBytesBc7 = (long)pending.AtlasSize * pending.AtlasSize * 2L;
            return atlasBytesBc7 > 16L * 1024L * 1024L ? "CRITICAL_WARNING" : "OK";
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static double TicksToMicroseconds(long ticks)
        {
            return ticks * 1000000.0 / Stopwatch.Frequency;
        }

        private static void ReleaseRenderTexture(RenderTexture texture)
        {
            if (texture == null)
                return;

            texture.Release();
            Object.DestroyImmediate(texture);
        }

        private sealed class PendingBake
        {
            public string SourceName;
            public string Folder;
            public string AlbedoAssetPath;
            public string NormalAssetPath;
            public string DataAssetPath;
            public string MaterialAssetPath;
            public string MeshAssetPath;
            public Shader ImpostorShader;
            public Bounds SourceBounds;
            public Vector3 PivotOffset;
            public int AtlasSize;
            public Vector2Int Grid;
            public int ViewCount;
            public float CaptureOrthoSize;
            public float CaptureDepthMeters;
            public float DilationRadiusPixels;
            public float RealGeometryDistanceMeters;
            public FixedString64Bytes ProfileName;
            public long SourceMeshBytes;
            public double PackMicroseconds;
            public Stopwatch TotalStopwatch;
            public Action<string, float> Progress;
            public RenderTexture AlbedoReadbackSource;
            public RenderTexture NormalReadbackSource;
            public RenderTexture OwnedCaptureAlbedo;
            public RenderTexture OwnedCaptureNormalDepth;
            public RenderTexture OwnedAtlasAlbedoDepth;
            public RenderTexture OwnedAtlasNormalXY;
            public RenderTexture OwnedDilatedAlbedoDepth;
            public RenderTexture OwnedDilatedNormalXY;
            public bool AlbedoDone;
            public bool NormalDone;
            public bool AlbedoError;
            public bool NormalError;

            public void Release()
            {
                ReleaseRenderTexture(OwnedCaptureAlbedo);
                ReleaseRenderTexture(OwnedCaptureNormalDepth);
                ReleaseRenderTexture(OwnedAtlasAlbedoDepth);
                ReleaseRenderTexture(OwnedAtlasNormalXY);
                ReleaseRenderTexture(OwnedDilatedAlbedoDepth);
                ReleaseRenderTexture(OwnedDilatedNormalXY);
                OwnedCaptureAlbedo = null;
                OwnedCaptureNormalDepth = null;
                OwnedAtlasAlbedoDepth = null;
                OwnedAtlasNormalXY = null;
                OwnedDilatedAlbedoDepth = null;
                OwnedDilatedNormalXY = null;
            }
        }
    }
}
#endif

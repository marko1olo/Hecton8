#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Hecton8.Editor.AITextureControlMaps
{
    internal static unsafe class AITextureControlMapBaker
    {
        private const uint SourceHash = 0x53483236u;
        private const uint WarningReadback = 1u << 0;
        private const uint WarningBlackMap = 1u << 1;
        private const uint WarningEncode = 1u << 2;
        private const uint WarningWrite = 1u << 3;
        private const uint WarningUnsupportedFormat = 1u << 4;
        private const string NativeMemoryOwner = nameof(AITextureControlMapBaker);

        private const string NormalShaderPath = "Assets/_Project/Shaders/Editor/AITextureControlMapBaker/Hecton_BakeWorldNormal.shader";
        private const string DepthShaderPath = "Assets/_Project/Shaders/Editor/AITextureControlMapBaker/Hecton_BakeDepth.shader";
        private const string ColorIdShaderPath = "Assets/_Project/Shaders/Editor/AITextureControlMapBaker/Hecton_BakeColorID.shader";
        private const string CurvatureShaderPath = "Assets/_Project/Shaders/Editor/AITextureControlMapBaker/Hecton_BakeCurvature.shader";

        private static readonly object WriteGate = new object();
        private static readonly List<ReadbackCompletion> PendingReadbackCompletions = new List<ReadbackCompletion>(128); // COLD ALLOC: List<ReadbackCompletion>[128] - editor async GPU readback completion queue - owner: AITextureControlMapBaker
        private static readonly List<WriteCompletion> PendingWriteCompletions = new List<WriteCompletion>(128); // COLD ALLOC: List<WriteCompletion>[128] - editor async PNG write completion queue - owner: AITextureControlMapBaker

        private static readonly int BakeBoundsMinId = Shader.PropertyToID("_BakeBoundsMin");
        private static readonly int BakeBoundsInvSizeId = Shader.PropertyToID("_BakeBoundsInvSize");
        private static readonly int BakeColorIdId = Shader.PropertyToID("_BakeColorId");
        private static readonly int CurvatureScaleId = Shader.PropertyToID("_CurvatureScale");
        private static readonly int CurvatureEdgeGainId = Shader.PropertyToID("_CurvatureEdgeGain");
        private static int _activeReadbacks;
        private static int _activeWrites;
        private static bool _writeDrainRegistered;
        private static bool _reloadLocked;

        [InitializeOnLoadMethod]
        private static void RegisterDomainReloadGuards()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= ForceUnlockReloadGuard;
            AssemblyReloadEvents.beforeAssemblyReload += ForceUnlockReloadGuard;
            EditorApplication.quitting -= ForceUnlockReloadGuard;
            EditorApplication.quitting += ForceUnlockReloadGuard;
        }

        [MenuItem("Hecton8/AI Texture Control Maps/Bake Selected Meshes", false, 2671)]
        internal static void BakeSelectedMeshesFromMenu()
        {
            AITextureBakeSettings settings = DefaultSettings();
            List<Mesh> meshes = new List<Mesh>(32); // COLD ALLOC: List<Mesh>[32] - editor selected mesh batch - owner: AITextureControlMapBaker
            Object[] selected = Selection.objects;
            for (int i = 0; i < selected.Length; i++)
            {
                Mesh mesh = selected[i] as Mesh;
                if (mesh != null)
                    meshes.Add(mesh);
            }

            BakeMeshes(meshes, settings, null);
        }

        internal static AITextureBakeSettings DefaultSettings()
        {
            AITextureBakeSettings settings;
            settings.ProfileName = new FixedString64Bytes("Hero_Prop");
            settings.PassMask = AITexturePassMask.All;
            settings.Resolution = AITextureControlMapConstants.DefaultBakeResolution;
            settings.GlobalQualityWeight = 1.0f;
            settings.AntiAliasing = 4;
            settings.ForceOverwrite = 1;
            settings._pad0 = 0;
            return settings;
        }

        internal static void BakeFolder(string folderAssetPath, AITextureBakeSettings settings, Action<string, float> progress)
        {
            if (string.IsNullOrEmpty(folderAssetPath) || !AssetDatabase.IsValidFolder(folderAssetPath))
            {
                Hecton8.Core.H8Debug.LogError("[AITextureControlMapBaker] Invalid mesh folder: " + folderAssetPath);
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Mesh", new[] { folderAssetPath });
            List<Mesh> meshes = new List<Mesh>(guids.Length); // COLD ALLOC: List<Mesh>[guids.Length] - editor folder mesh batch - owner: AITextureControlMapBaker
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh != null)
                    meshes.Add(mesh);
            }

            BakeMeshes(meshes, settings, progress);
        }

        internal static void BakeMeshes(List<Mesh> meshes, AITextureBakeSettings settings, Action<string, float> progress)
        {
            if (meshes == null || meshes.Count == 0)
            {
                Hecton8.Core.H8Debug.LogWarning("[AITextureControlMapBaker] No meshes supplied.");
                return;
            }

            EnsureDirectory(AITextureControlMapConstants.TemplateOutputFolder);
            int requestedPasses = CountPasses(settings.PassMask);
            if (requestedPasses == 0)
            {
                Hecton8.Core.H8Debug.LogWarning("[AITextureControlMapBaker] No bake passes enabled.");
                return;
            }

            int totalRequests = meshes.Count * requestedPasses;
            int resolution = NormalizeBakeResolution(settings.Resolution <= 0 ? AITextureControlMapConstants.DefaultBakeResolution : settings.Resolution);
            BakeBatchState state = new BakeBatchState(totalRequests, progress, meshes.Count, resolution);
            EnsureWriteDrainRegistered();
            UvCaptureRig rig = default;
            try
            {
                rig = UvCaptureRig.Create();
                for (int i = 0; i < meshes.Count; i++)
                {
                    Mesh mesh = meshes[i];
                    if (mesh == null)
                        continue;

                    BakeMesh(mesh, settings, state, ref rig);
                }
            }
            finally
            {
                rig.Dispose();
            }
        }

        private static void BakeMesh(Mesh mesh, AITextureBakeSettings settings, BakeBatchState state, ref UvCaptureRig rig)
        {
            if (mesh == null)
                return;

            Bounds bounds = mesh.bounds;
            Vector3 size = bounds.size;
            Vector4 min = bounds.min;
            Vector4 invSize = new Vector4(
                size.x > 1e-5f ? 1.0f / size.x : 0.0f,
                size.y > 1e-5f ? 1.0f / size.y : 0.0f,
                size.z > 1e-5f ? 1.0f / size.z : 0.0f,
                0.0f);
            int resolution = NormalizeBakeResolution(settings.Resolution <= 0 ? AITextureControlMapConstants.DefaultBakeResolution : settings.Resolution);
            string safeName = Sanitize(mesh.name);
            uint meshHash = BuildMeshHash(safeName, mesh.vertexCount, mesh.subMeshCount);
            Vector3 extents = bounds.extents;
            float quality = Mathf.Clamp01(settings.GlobalQualityWeight);
            int superSampleMultiplier = SelectSupersampleMultiplier(settings.AntiAliasing, quality, resolution);

            if ((settings.PassMask & AITexturePassMask.Normal) != (AITexturePassMask)0)
                BakePass(mesh, AITextureControlPass.Normal, resolution, superSampleMultiplier, min, invSize, extents, safeName, meshHash, quality, state, ref rig);
            if ((settings.PassMask & AITexturePassMask.Depth) != (AITexturePassMask)0)
                BakePass(mesh, AITextureControlPass.Depth, resolution, superSampleMultiplier, min, invSize, extents, safeName, meshHash, quality, state, ref rig);
            if ((settings.PassMask & AITexturePassMask.ColorId) != (AITexturePassMask)0)
                BakePass(mesh, AITextureControlPass.ColorId, resolution, superSampleMultiplier, min, invSize, extents, safeName, meshHash, quality, state, ref rig);
            if ((settings.PassMask & AITexturePassMask.Curvature) != (AITexturePassMask)0)
                BakePass(mesh, AITextureControlPass.Curvature, resolution, superSampleMultiplier, min, invSize, extents, safeName, meshHash, quality, state, ref rig);
        }

        private static void BakePass(Mesh mesh, AITextureControlPass pass, int resolution, int superSampleMultiplier, Vector4 boundsMin, Vector4 boundsInvSize, Vector3 boundsExtents, string safeName, uint meshHash, float quality, BakeBatchState state, ref UvCaptureRig rig)
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(SelectShaderPath(pass));
            if (shader == null)
            {
                Hecton8.Core.H8Debug.LogError("[AITextureControlMapBaker] Missing shader for pass " + pass + ".");
                state.AddCriticalWarning();
                AITextureBakeBlackBox.Record(BuildTelemetry(meshHash, resolution, pass, 0.0, 0.0, 0.0, mesh.vertexCount, mesh.subMeshCount, WarningReadback, boundsExtents, quality));
                AITextureBakeBlackBox.Dump(AITextureControlMapConstants.BakeBlackBoxDumpPath);
                state.MarkComplete(safeName + "_" + pass);
                return;
            }

            Material material = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            RenderTexture readbackTexture = null;
            RenderTexture drawTexture = null;
            CommandBuffer commandBuffer = null;
            Stopwatch renderStopwatch = Stopwatch.StartNew();
            try
            {
                RenderTextureDescriptor descriptor = new RenderTextureDescriptor(resolution, resolution, GraphicsFormat.R8G8B8A8_UNorm, 0)
                {
                    msaaSamples = 1,
                    sRGB = false,
                    useMipMap = false,
                    autoGenerateMips = false
                };
                readbackTexture = new RenderTexture(descriptor)
                {
                    name = "SHINOBU_269_" + safeName + "_" + pass,
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear
                };
                readbackTexture.Create();
                if (superSampleMultiplier > 1)
                {
                    RenderTextureDescriptor supersampleDescriptor = descriptor;
                    supersampleDescriptor.width = resolution * superSampleMultiplier;
                    supersampleDescriptor.height = resolution * superSampleMultiplier;
                    drawTexture = new RenderTexture(supersampleDescriptor)
                    {
                        name = "SHINOBU_269_" + safeName + "_" + pass + "_SS" + superSampleMultiplier.ToString(CultureInfo.InvariantCulture),
                        hideFlags = HideFlags.HideAndDontSave,
                        filterMode = FilterMode.Bilinear
                    };
                    drawTexture.Create();
                }
                else
                {
                    drawTexture = readbackTexture;
                }

                rig.Bind(drawTexture);

                commandBuffer = new CommandBuffer
                {
                    name = "SHINOBU_269_AITexture_" + pass
                };
                commandBuffer.SetRenderTarget(drawTexture);
                commandBuffer.ClearRenderTarget(false, true, SelectClearColor(pass));
                rig.Configure(commandBuffer);
                commandBuffer.SetGlobalVector(BakeBoundsMinId, boundsMin);
                commandBuffer.SetGlobalVector(BakeBoundsInvSizeId, boundsInvSize);
                commandBuffer.SetGlobalFloat(CurvatureScaleId, SelectCurvatureScale(quality));
                commandBuffer.SetGlobalFloat(CurvatureEdgeGainId, SelectCurvatureEdgeGain(quality));
                int subMeshCount = Mathf.Max(1, mesh.subMeshCount);
                for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
                {
                    if (pass == AITextureControlPass.ColorId)
                        commandBuffer.SetGlobalVector(BakeColorIdId, BuildColorId(subMesh));

                    commandBuffer.DrawMesh(mesh, Matrix4x4.identity, material, subMesh, 0);
                }
                if (superSampleMultiplier > 1)
                    commandBuffer.Blit(drawTexture, readbackTexture);

                UnityEngine.Graphics.ExecuteCommandBuffer(commandBuffer);
                renderStopwatch.Stop();
                string outputPath = AITextureControlMapConstants.TemplateOutputFolder + "/" + safeName + "_" + SelectPassToken(pass) + ".png";
                ReadbackContext context = new ReadbackContext(readbackTexture, drawTexture != readbackTexture ? drawTexture : null, material, outputPath, resolution, pass, state, renderStopwatch.Elapsed.TotalMilliseconds, meshHash, mesh.vertexCount, mesh.subMeshCount, boundsExtents, quality);
                if (!SystemInfo.IsFormatSupported(GraphicsFormat.R8G8B8A8_UNorm, GraphicsFormatUsage.ReadPixels))
                {
                    context.WarningFlags |= WarningUnsupportedFormat;
                    context.State.AddCriticalWarning();
                    CompleteWithoutWrite(context, 0.0);
                    rig.Bind(null);
                    readbackTexture = null;
                    drawTexture = null;
                    material = null;
                    return;
                }

                context.ReadbackData = AITextureNativeMemory.AllocateArray<byte>(
                    resolution * resolution * 4,
                    Allocator.TempJob,
                    NativeArrayOptions.UninitializedMemory,
                    NativeMemoryOwner,
                    nameof(ReadbackContext.ReadbackData));
                try
                {
                    RegisterActiveReadback();
                    AsyncGPUReadback.RequestIntoNativeArray(ref context.ReadbackData, context.ReadbackTexture, 0, OnReadbackComplete(context));
                    rig.Bind(null);
                    readbackTexture = null;
                    drawTexture = null;
                    material = null;
                }
                catch
                {
                    ReleaseActiveReadback();
                    TryReleaseReloadGuardWhenIdle();
                    rig.Bind(null);
                    AITextureNativeMemory.DisposeArray(ref context.ReadbackData);

                    throw;
                }
            }
            catch (Exception ex)
            {
                Hecton8.Core.H8Debug.LogError("[AITextureControlMapBaker] Bake pass failed for " + safeName + "_" + pass + ": " + ex.Message);
                state.AddCriticalWarning();
                AITextureBakeBlackBox.Record(BuildTelemetry(meshHash, resolution, pass, renderStopwatch.Elapsed.TotalMilliseconds, 0.0, 0.0, mesh.vertexCount, mesh.subMeshCount, WarningReadback, boundsExtents, quality));
                AITextureBakeBlackBox.Dump(AITextureControlMapConstants.BakeBlackBoxDumpPath);
                state.MarkComplete(safeName + "_" + pass);
            }
            finally
            {
                rig.Bind(null);
                if (commandBuffer != null)
                    commandBuffer.Release();
                if (drawTexture != null && drawTexture != readbackTexture)
                {
                    drawTexture.Release();
                    Object.DestroyImmediate(drawTexture);
                }
                if (readbackTexture != null)
                {
                    readbackTexture.Release();
                    Object.DestroyImmediate(readbackTexture);
                }
                if (material != null)
                    Object.DestroyImmediate(material);
            }
        }

        private static Action<AsyncGPUReadbackRequest> OnReadbackComplete(ReadbackContext context)
        {
            return request => CompleteReadback(context, request);
        }

        private static void CompleteReadback(ReadbackContext context, AsyncGPUReadbackRequest request)
        {
            EnqueueReadbackCompletion(new ReadbackCompletion(context, request.hasError));
        }

        private static void ProcessReadbackCompletion(ReadbackCompletion completion)
        {
            Stopwatch encodeStopwatch = Stopwatch.StartNew();
            NativeArray<byte> pngBytes = default;
            bool completedWithoutWrite = false;
            ReadbackContext context = completion.Context;
            try
            {
                if (completion.HasError)
                {
                    Hecton8.Core.H8Debug.LogError("[AITextureControlMapBaker] AsyncGPUReadback failed for " + context.OutputPath);
                    context.WarningFlags |= WarningReadback;
                    context.State.AddCriticalWarning();
                    completedWithoutWrite = true;
                }
                else
                {
                    NativeArray<byte> data = context.ReadbackData;
                    if (IsMostlyBlack(data, context.Pass, context.GlobalQualityWeight))
                    {
                        context.WarningFlags |= WarningBlackMap;
                        context.State.AddCriticalWarning();
                    }

                    pngBytes = ImageConversion.EncodeNativeArrayToPNG(data, GraphicsFormat.R8G8B8A8_UNorm, (uint)context.Resolution, (uint)context.Resolution, 0u);
                    AITextureNativeMemory.RegisterArray(ref pngBytes, NativeMemoryOwner, nameof(pngBytes), Hecton8.Core.NativeAllocationLifetime.TempJob);
                    if (!pngBytes.IsCreated || pngBytes.Length == 0)
                    {
                        context.WarningFlags |= WarningEncode;
                        context.State.AddCriticalWarning();
                        completedWithoutWrite = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Hecton8.Core.H8Debug.LogError("[AITextureControlMapBaker] PNG encode failed: " + ex.Message);
                context.WarningFlags |= WarningEncode;
                context.State.AddCriticalWarning();
                completedWithoutWrite = true;
            }
            finally
            {
                encodeStopwatch.Stop();
                ReleaseContextResources(context);

                AITextureNativeMemory.DisposeArray(ref context.ReadbackData);
            }

            if (!pngBytes.IsCreated || pngBytes.Length == 0)
            {
                AITextureNativeMemory.DisposeArray(ref pngBytes);

                if (completedWithoutWrite)
                    CompleteWithoutWrite(context, encodeStopwatch.Elapsed.TotalMilliseconds);

                return;
            }

            WritePngAsync(context, pngBytes, encodeStopwatch.Elapsed.TotalMilliseconds);
        }

        private static void WritePngAsync(ReadbackContext context, NativeArray<byte> pngBytes, double encodeMilliseconds)
        {
            EnsureDirectory(Path.GetDirectoryName(context.OutputPath));
            Stopwatch writeStopwatch = Stopwatch.StartNew();
            RegisterActiveWrite();
            try
            {
                IntPtr pointer = (IntPtr)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(pngBytes);
                int byteCount = pngBytes.Length;
                bool queued = ThreadPool.QueueUserWorkItem(_ =>
                {
                    string error = null;
                    string tempPath = context.OutputPath + ".tmp";
                    try
                    {
                        if (File.Exists(tempPath))
                            File.Delete(tempPath);

                        using (FileStream stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan))
                        {
                            byte* bytes = (byte*)pointer.ToPointer();
                            stream.Write(new ReadOnlySpan<byte>(bytes, byteCount));
                        }

                        PromoteTempFileAtomic(tempPath, context.OutputPath);
                    }
                    catch (Exception ex)
                    {
                        TryDeleteFileNoThrow(tempPath);
                        error = ex.Message;
                    }
                    finally
                    {
                        writeStopwatch.Stop();
                        EnqueueWriteCompletion(new WriteCompletion(context, pngBytes, encodeMilliseconds, writeStopwatch.Elapsed.TotalMilliseconds, error));
                    }
                });
                if (!queued)
                {
                    writeStopwatch.Stop();
                    EnqueueWriteCompletion(new WriteCompletion(context, pngBytes, encodeMilliseconds, writeStopwatch.Elapsed.TotalMilliseconds, "ThreadPool queue rejected native PNG write."));
                }
            }
            catch (Exception ex)
            {
                writeStopwatch.Stop();
                EnqueueWriteCompletion(new WriteCompletion(context, pngBytes, encodeMilliseconds, writeStopwatch.Elapsed.TotalMilliseconds, ex.Message));
            }
        }

        private static void PromoteTempFileAtomic(string tempPath, string path)
        {
            if (File.Exists(path))
                File.Replace(tempPath, path, null, true);
            else
                File.Move(tempPath, path);
        }

        private static void TryDeleteFileNoThrow(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }

        private static string SelectShaderPath(AITextureControlPass pass)
        {
            switch (pass)
            {
                case AITextureControlPass.Normal:
                    return NormalShaderPath;
                case AITextureControlPass.Depth:
                    return DepthShaderPath;
                case AITextureControlPass.ColorId:
                    return ColorIdShaderPath;
                case AITextureControlPass.Curvature:
                    return CurvatureShaderPath;
                default:
                    return NormalShaderPath;
            }
        }

        private static string SelectPassToken(AITextureControlPass pass)
        {
            switch (pass)
            {
                case AITextureControlPass.Normal:
                    return "Normal";
                case AITextureControlPass.Depth:
                    return "Depth";
                case AITextureControlPass.ColorId:
                    return "ColorID";
                case AITextureControlPass.Curvature:
                    return "Curvature";
                default:
                    return "Unknown";
            }
        }

        private static Color SelectClearColor(AITextureControlPass pass)
        {
            return pass == AITextureControlPass.Curvature
                ? new Color(0.5f, 0.5f, 0.5f, 1.0f)
                : new Color(0.0f, 0.0f, 0.0f, 1.0f);
        }

        private static Vector4 BuildColorId(int subMesh)
        {
            uint hash = (uint)(subMesh + 1) * 747796405u + 2891336453u;
            hash ^= hash >> 16;
            hash *= 2246822519u;
            float r = ((hash >> 0) & 255u) * (1.0f / 255.0f);
            float g = ((hash >> 8) & 255u) * (1.0f / 255.0f);
            float b = ((hash >> 16) & 255u) * (1.0f / 255.0f);
            return new Vector4(r, g, b, 1.0f);
        }

        private static void CompleteWithoutWrite(ReadbackContext context, double encodeMilliseconds)
        {
            context.State.AddTiming(context.RenderMilliseconds, encodeMilliseconds, 0.0);
            AITextureBakeBlackBox.Record(BuildTelemetry(context, encodeMilliseconds, 0.0));
            if (context.WarningFlags != 0u)
                AITextureBakeBlackBox.Dump(AITextureControlMapConstants.BakeBlackBoxDumpPath);

            ReleaseContextResources(context);
            context.State.MarkComplete(context.OutputPath);
        }

        private static void ReleaseContextResources(ReadbackContext context)
        {
            if (context == null)
                return;

            if (context.SupersampleTexture != null)
            {
                context.SupersampleTexture.Release();
                Object.DestroyImmediate(context.SupersampleTexture);
                context.SupersampleTexture = null;
            }

            if (context.ReadbackTexture != null)
            {
                context.ReadbackTexture.Release();
                Object.DestroyImmediate(context.ReadbackTexture);
                context.ReadbackTexture = null;
            }

            if (context.Material != null)
            {
                Object.DestroyImmediate(context.Material);
                context.Material = null;
            }
        }

        private static void RegisterActiveReadback()
        {
            bool lockReload = false;
            lock (WriteGate)
            {
                _activeReadbacks++;
                if (!_reloadLocked)
                {
                    _reloadLocked = true;
                    lockReload = true;
                }
            }

            if (lockReload)
                EditorApplication.LockReloadAssemblies();
        }

        private static void ReleaseActiveReadback()
        {
            lock (WriteGate)
                _activeReadbacks = Mathf.Max(0, _activeReadbacks - 1);
        }

        private static void RegisterActiveWrite()
        {
            bool lockReload = false;
            lock (WriteGate)
            {
                _activeWrites++;
                if (!_reloadLocked)
                {
                    _reloadLocked = true;
                    lockReload = true;
                }
            }

            if (lockReload)
                EditorApplication.LockReloadAssemblies();
        }

        private static void EnqueueReadbackCompletion(ReadbackCompletion completion)
        {
            lock (WriteGate)
            {
                PendingReadbackCompletions.Add(completion);
                _activeReadbacks = Mathf.Max(0, _activeReadbacks - 1);
            }
        }

        private static void EnqueueWriteCompletion(WriteCompletion completion)
        {
            lock (WriteGate)
            {
                PendingWriteCompletions.Add(completion);
                _activeWrites = Mathf.Max(0, _activeWrites - 1);
            }
        }

        private static void EnsureWriteDrainRegistered()
        {
            if (_writeDrainRegistered)
                return;

            EditorApplication.update -= DrainWriteCompletions;
            EditorApplication.update += DrainWriteCompletions;
            _writeDrainRegistered = true;
        }

        private static void DrainWriteCompletions()
        {
            while (true)
            {
                ReadbackCompletion readbackCompletion;
                bool hasReadbackCompletion = false;
                lock (WriteGate)
                {
                    int readbackCount = PendingReadbackCompletions.Count;
                    if (readbackCount > 0)
                    {
                        int lastReadback = readbackCount - 1;
                        readbackCompletion = PendingReadbackCompletions[lastReadback];
                        PendingReadbackCompletions.RemoveAt(lastReadback);
                        hasReadbackCompletion = true;
                    }
                    else
                    {
                        readbackCompletion = default;
                    }
                }

                if (hasReadbackCompletion)
                {
                    ProcessReadbackCompletion(readbackCompletion);
                    continue;
                }

                WriteCompletion completion;
                bool unlockReload = false;
                bool exitAfterIdle = false;
                lock (WriteGate)
                {
                    int count = PendingWriteCompletions.Count;
                    if (count == 0)
                    {
                        if (_activeReadbacks == 0 && _activeWrites == 0 && _writeDrainRegistered)
                        {
                            EditorApplication.update -= DrainWriteCompletions;
                            _writeDrainRegistered = false;
                        }

                        if (_activeReadbacks == 0 && _activeWrites == 0 && PendingReadbackCompletions.Count == 0 && _reloadLocked)
                        {
                            _reloadLocked = false;
                            unlockReload = true;
                        }

                        completion = default;
                        exitAfterIdle = true;
                    }
                    else
                    {
                        int last = count - 1;
                        completion = PendingWriteCompletions[last];
                        PendingWriteCompletions.RemoveAt(last);
                    }
                }

                if (unlockReload)
                    EditorApplication.UnlockReloadAssemblies();
                if (exitAfterIdle)
                    return;

                ProcessWriteCompletion(completion);
            }
        }

        private static void TryReleaseReloadGuardWhenIdle()
        {
            bool unlockReload = false;
            lock (WriteGate)
            {
                if (_activeReadbacks == 0 &&
                    _activeWrites == 0 &&
                    PendingReadbackCompletions.Count == 0 &&
                    PendingWriteCompletions.Count == 0 &&
                    _reloadLocked)
                {
                    _reloadLocked = false;
                    unlockReload = true;
                }
            }

            if (unlockReload)
                EditorApplication.UnlockReloadAssemblies();
        }

        private static void ForceUnlockReloadGuard()
        {
            bool unlockReload = false;
            lock (WriteGate)
            {
                EditorApplication.update -= DrainWriteCompletions;
                _writeDrainRegistered = false;

                for (int i = 0; i < PendingReadbackCompletions.Count; i++)
                {
                    ReadbackContext context = PendingReadbackCompletions[i].Context;
                    ReleaseContextResources(context);
                    AITextureNativeMemory.DisposeArray(ref context.ReadbackData);
                }

                for (int i = 0; i < PendingWriteCompletions.Count; i++)
                {
                    WriteCompletion completion = PendingWriteCompletions[i];
                    AITextureNativeMemory.DisposeArray(ref completion.PngBytes);
                }

                PendingReadbackCompletions.Clear();
                PendingWriteCompletions.Clear();
                _activeReadbacks = 0;
                _activeWrites = 0;
                if (_reloadLocked)
                {
                    _reloadLocked = false;
                    unlockReload = true;
                }
            }

            if (unlockReload)
                EditorApplication.UnlockReloadAssemblies();
        }

        private static void ProcessWriteCompletion(WriteCompletion completion)
        {
            try
            {
                ReadbackContext context = completion.Context;
                if (!string.IsNullOrEmpty(completion.Error))
                {
                    Hecton8.Core.H8Debug.LogError("[AITextureControlMapBaker] PNG write failed: " + completion.Error);
                    context.WarningFlags |= WarningWrite;
                    context.State.AddCriticalWarning();
                }

                context.State.AddTiming(context.RenderMilliseconds, completion.EncodeMilliseconds, completion.WriteMilliseconds);
                AITextureBakeBlackBox.Record(BuildTelemetry(context, completion.EncodeMilliseconds, completion.WriteMilliseconds));
                if (context.WarningFlags != 0u)
                    AITextureBakeBlackBox.Dump(AITextureControlMapConstants.BakeBlackBoxDumpPath);

                context.State.MarkComplete(context.OutputPath);
            }
            finally
            {
                AITextureNativeMemory.DisposeArray(ref completion.PngBytes);
            }
        }

        private static uint BuildMeshHash(string safeName, int vertexCount, int subMeshCount)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < safeName.Length; i++)
                {
                    hash ^= safeName[i];
                    hash *= 16777619u;
                }

                hash ^= (uint)vertexCount;
                hash *= 16777619u;
                hash ^= (uint)subMeshCount;
                hash *= 16777619u;
                return hash;
            }
        }

        private static AITextureBakeTelemetryEntry BuildTelemetry(ReadbackContext context, double encodeMilliseconds, double writeMilliseconds)
        {
            return BuildTelemetry(
                context.MeshHash,
                context.Resolution,
                context.Pass,
                context.RenderMilliseconds,
                encodeMilliseconds,
                writeMilliseconds,
                context.VertexCount,
                context.SubMeshCount,
                context.WarningFlags,
                context.BoundsExtents,
                context.GlobalQualityWeight);
        }

        private static AITextureBakeTelemetryEntry BuildTelemetry(uint meshHash, int resolution, AITextureControlPass pass, double renderMilliseconds, double encodeMilliseconds, double writeMilliseconds, int vertexCount, int subMeshCount, uint warningFlags, Vector3 boundsExtents, float globalQualityWeight)
        {
            AITextureBakeTelemetryEntry entry;
            entry.SourceHash = SourceHash;
            entry.MeshHash = meshHash;
            entry.Resolution = resolution;
            entry.PassMask = 1 << (int)pass;
            entry.RenderMicroseconds = ToMicroseconds(renderMilliseconds);
            entry.EncodeMicroseconds = ToMicroseconds(encodeMilliseconds);
            entry.WriteMicroseconds = ToMicroseconds(writeMilliseconds);
            entry.VertexCount = vertexCount;
            entry.SubMeshCount = subMeshCount;
            entry.WarningFlags = warningFlags;
            entry.BoundsExtentX = boundsExtents.x;
            entry.BoundsExtentY = boundsExtents.y;
            entry.BoundsExtentZ = boundsExtents.z;
            entry.GlobalQualityWeight = Mathf.Clamp01(globalQualityWeight);
            entry.StateHash = BuildStateHash(meshHash, resolution, pass, warningFlags);
            entry._pad0 = 0u;
            return entry;
        }

        private static int ToMicroseconds(double milliseconds)
        {
            double value = milliseconds * 1000.0;
            if (value <= 0.0)
                return 0;

            return value >= int.MaxValue ? int.MaxValue : (int)value;
        }

        private static uint BuildStateHash(uint meshHash, int resolution, AITextureControlPass pass, uint warningFlags)
        {
            unchecked
            {
                uint hash = SourceHash;
                hash ^= meshHash + 0x9E3779B9u + (hash << 6) + (hash >> 2);
                hash ^= (uint)resolution + 0x9E3779B9u + (hash << 6) + (hash >> 2);
                hash ^= ((uint)pass << 24) + 0x9E3779B9u + (hash << 6) + (hash >> 2);
                hash ^= warningFlags + 0x9E3779B9u + (hash << 6) + (hash >> 2);
                return hash;
            }
        }

        private static bool IsMostlyBlack(NativeArray<byte> data, AITextureControlPass pass, float globalQualityWeight)
        {
            if (!data.IsCreated || (pass != AITextureControlPass.Normal && pass != AITextureControlPass.ColorId))
                return false;

            int pixelCount = data.Length >> 2;
            if (pixelCount <= 0)
                return true;

            int sampleBudget = SelectValidationSampleBudget(globalQualityWeight);
            int step = Mathf.Max(1, pixelCount / sampleBudget);
            int samples = 0;
            int black = 0;
            for (int pixel = 0; pixel < pixelCount; pixel += step)
            {
                int index = pixel << 2;
                if (data[index] <= 2 && data[index + 1] <= 2 && data[index + 2] <= 2)
                    black++;

                samples++;
            }

            return samples > 0 && black * 1000 >= samples * 995;
        }

        private static int NormalizeBakeResolution(int requestedResolution)
        {
            int safeRequested = Mathf.Max(64, requestedResolution);
            int aligned = Mathf.CeilToInt(safeRequested * (1.0f / 64.0f)) * 64;
            return Mathf.Clamp(aligned, 64, AITextureControlMapConstants.HeroBakeResolution);
        }

        private static int SelectSupersampleMultiplier(byte requestedAntiAliasing, float globalQualityWeight, int outputResolution)
        {
            int requested = Mathf.Clamp(requestedAntiAliasing <= 0 ? 2 : requestedAntiAliasing, 1, 4);
            float weighted = math.lerp(1.0f, requested, BuildQualityCurve(globalQualityWeight));
            int multiplier = Mathf.Clamp(Mathf.RoundToInt(weighted), 1, 4);
            int maxTextureSize = Mathf.Min(SystemInfo.maxTextureSize, 8192);
            while (multiplier > 1 && outputResolution * multiplier > maxTextureSize)
                multiplier--;
            return multiplier;
        }

        private static float SelectCurvatureScale(float globalQualityWeight)
        {
            return math.lerp(0.35f, 1.25f, BuildQualityCurve(globalQualityWeight));
        }

        private static float SelectCurvatureEdgeGain(float globalQualityWeight)
        {
            return math.lerp(4.0f, 18.0f, BuildQualityCurve(globalQualityWeight));
        }

        private static int SelectValidationSampleBudget(float globalQualityWeight)
        {
            return Mathf.RoundToInt(math.lerp(512.0f, 4096.0f, BuildQualityCurve(globalQualityWeight)));
        }

        private static float BuildQualityCurve(float globalQualityWeight)
        {
            float q = math.saturate(globalQualityWeight);
            return q * q * (3.0f - 2.0f * q);
        }

        private static int CountPasses(AITexturePassMask mask)
        {
            int count = 0;
            if ((mask & AITexturePassMask.Normal) != (AITexturePassMask)0)
                count++;
            if ((mask & AITexturePassMask.Depth) != (AITexturePassMask)0)
                count++;
            if ((mask & AITexturePassMask.ColorId) != (AITexturePassMask)0)
                count++;
            if ((mask & AITexturePassMask.Curvature) != (AITexturePassMask)0)
                count++;

            return count;
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "UnnamedMesh";

            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
                    chars[i] = '_';
            }

            return new string(chars);
        }

        private static void EnsureDirectory(string directory)
        {
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }

        private sealed class ReadbackContext
        {
            public ReadbackContext(RenderTexture readbackTexture, RenderTexture supersampleTexture, Material material, string outputPath, int resolution, AITextureControlPass pass, BakeBatchState state, double renderMilliseconds, uint meshHash, int vertexCount, int subMeshCount, Vector3 boundsExtents, float globalQualityWeight)
            {
                ReadbackTexture = readbackTexture;
                SupersampleTexture = supersampleTexture;
                Material = material;
                OutputPath = outputPath;
                Resolution = resolution;
                Pass = pass;
                State = state;
                RenderMilliseconds = renderMilliseconds;
                MeshHash = meshHash;
                VertexCount = vertexCount;
                SubMeshCount = subMeshCount;
                BoundsExtents = boundsExtents;
                GlobalQualityWeight = Mathf.Clamp01(globalQualityWeight);
                WarningFlags = 0u;
            }

            public RenderTexture ReadbackTexture;
            public RenderTexture SupersampleTexture;
            public Material Material;
            public string OutputPath;
            public int Resolution;
            public AITextureControlPass Pass;
            public BakeBatchState State;
            public double RenderMilliseconds;
            public uint MeshHash;
            public int VertexCount;
            public int SubMeshCount;
            public Vector3 BoundsExtents;
            public float GlobalQualityWeight;
            public uint WarningFlags;
            public NativeArray<byte> ReadbackData;
        }

        private struct WriteCompletion
        {
            public WriteCompletion(ReadbackContext context, NativeArray<byte> pngBytes, double encodeMilliseconds, double writeMilliseconds, string error)
            {
                Context = context;
                PngBytes = pngBytes;
                EncodeMilliseconds = encodeMilliseconds;
                WriteMilliseconds = writeMilliseconds;
                Error = error;
            }

            public ReadbackContext Context;
            public NativeArray<byte> PngBytes;
            public double EncodeMilliseconds;
            public double WriteMilliseconds;
            public string Error;
        }

        private struct ReadbackCompletion
        {
            public ReadbackCompletion(ReadbackContext context, bool hasError)
            {
                Context = context;
                HasError = hasError;
            }

            public ReadbackContext Context;
            public bool HasError;
        }

        private struct UvCaptureRig
        {
            public GameObject CameraObject;
            public Camera Camera;

            public static UvCaptureRig Create()
            {
                GameObject cameraObject = new GameObject("SHINOBU_269_AITextureCaptureRig")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                cameraObject.transform.position = new Vector3(0.0f, 0.0f, -1.0f);
                cameraObject.transform.rotation = Quaternion.identity;

                Camera camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = 1.0f;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 10.0f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.cullingMask = 0;
                camera.allowHDR = false;
                camera.allowMSAA = false;

                UvCaptureRig rig;
                rig.CameraObject = cameraObject;
                rig.Camera = camera;
                return rig;
            }

            public void Bind(RenderTexture renderTexture)
            {
                if (Camera == null)
                    return;

                Camera.targetTexture = renderTexture;
            }

            public void Configure(CommandBuffer commandBuffer)
            {
                if (Camera == null || commandBuffer == null)
                    return;

                commandBuffer.SetViewProjectionMatrices(Camera.worldToCameraMatrix, Camera.projectionMatrix);
            }

            public void Dispose()
            {
                if (Camera != null)
                    Camera.targetTexture = null;
                if (CameraObject != null)
                    Object.DestroyImmediate(CameraObject);

                Camera = null;
                CameraObject = null;
            }
        }

        internal sealed class BakeBatchState
        {
            private readonly object _gate = new object();
            private readonly Action<string, float> _progress;
            private readonly int _total;
            private readonly int _modelCount;
            private readonly int _resolution;
            private int _completed;
            private int _criticalWarnings;
            private double _renderMilliseconds;
            private double _encodeMilliseconds;
            private double _writeMilliseconds;
            private bool _reported;

            public BakeBatchState(int total, Action<string, float> progress, int modelCount, int resolution)
            {
                _total = Mathf.Max(1, total);
                _progress = progress;
                _modelCount = modelCount;
                _resolution = resolution;
            }

            public void AddTiming(double renderMilliseconds, double encodeMilliseconds, double writeMilliseconds)
            {
                lock (_gate)
                {
                    _renderMilliseconds += renderMilliseconds;
                    _encodeMilliseconds += encodeMilliseconds;
                    _writeMilliseconds += writeMilliseconds;
                }
            }

            public void AddCriticalWarning()
            {
                lock (_gate)
                    _criticalWarnings++;
            }

            public void MarkComplete(string label)
            {
                int completed;
                int warnings;
                double render;
                double encode;
                double write;
                bool shouldReport;
                lock (_gate)
                {
                    _completed++;
                    completed = _completed;
                    warnings = _criticalWarnings;
                    render = _renderMilliseconds;
                    encode = _encodeMilliseconds;
                    write = _writeMilliseconds;
                    shouldReport = !_reported && completed >= _total;
                    if (shouldReport)
                        _reported = true;
                }

                float value = Mathf.Clamp01(completed / (float)_total);
                _progress?.Invoke(label, value);
                if (shouldReport)
                {
                    AssetDatabase.Refresh();
                    AITexturePipelineReport.WriteBakeReport(_modelCount, _resolution, completed, warnings, render, encode, write);
                    Hecton8.Core.H8Debug.Log("[AITextureControlMapBaker] Batch report written. RenderMs=" + render.ToString("0.000", CultureInfo.InvariantCulture) +
                              " EncodeMs=" + encode.ToString("0.000", CultureInfo.InvariantCulture) +
                              " WriteMs=" + write.ToString("0.000", CultureInfo.InvariantCulture) +
                              " CriticalWarnings=" + warnings.ToString(CultureInfo.InvariantCulture) + ".");
                }
            }
        }
    }
}
#endif

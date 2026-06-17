#if UNITY_EDITOR
using System;
using System.IO;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Hecton8.Core;

namespace Hecton8.Editor.AITextureControlMaps
{
    [InitializeOnLoad]
    internal static class AITextureBakeBlackBox
    {
        private const string NativeMemoryOwner = nameof(AITextureBakeBlackBox);
        private const string RingLabel = "ring";
        private static NativeArray<AITextureBakeTelemetryEntry> _ring;
        private static int _cursor;

        static AITextureBakeBlackBox()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= Release;
            AssemblyReloadEvents.beforeAssemblyReload += Release;
            EditorApplication.quitting -= Release;
            EditorApplication.quitting += Release;
        }

        [MenuItem("Hecton8/AI Texture Control Maps/Dump Bake Black Box", false, 2692)]
        internal static void DumpMenu()
        {
            Dump(AITextureControlMapConstants.BakeBlackBoxDumpPath);
        }

        internal static void Record(in AITextureBakeTelemetryEntry entry)
        {
            Ensure();
            _ring[_cursor] = entry;
            _cursor++;
            if (_cursor >= AITextureControlMapConstants.BakeBlackBoxCapacity)
                _cursor = 0;
        }

        internal static void Dump(string path)
        {
            try
            {
                Ensure();
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(0x32425848u);
                    writer.Write(AITextureControlMapConstants.BakeBlackBoxCapacity);
                    writer.Write(_cursor);
                    writer.Write(64);
                    for (int i = 0; i < _ring.Length; i++)
                        WriteEntry(writer, _ring[i]);
                }

                H8Debug.Log("[AITextureBakeBlackBox] Dumped " + path + ".");
            }
            catch (Exception ex)
            {
                H8Debug.LogError("[AITextureBakeBlackBox] Dump failed: " + ex.Message);
            }
        }

        private static void Ensure()
        {
            if (_ring.IsCreated)
                return;

            _ring = AITextureNativeMemory.AllocateArray<AITextureBakeTelemetryEntry>(
                AITextureControlMapConstants.BakeBlackBoxCapacity,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory,
                NativeMemoryOwner,
                RingLabel); // COLD ALLOC: NativeArray<AITextureBakeTelemetryEntry>[300] - editor bake forensic blackbox - owner: AITextureBakeBlackBox

            AITextureBakeTelemetryEntry sentinel = default;
            sentinel.SourceHash = 0x5348494Eu;
            sentinel.MeshHash = 0u;
            sentinel.Resolution = 0;
            sentinel.PassMask = 0;
            sentinel.RenderMicroseconds = 0;
            sentinel.EncodeMicroseconds = 0;
            sentinel.WriteMicroseconds = 0;
            sentinel.VertexCount = 0;
            sentinel.SubMeshCount = 0;
            sentinel.WarningFlags = 0u;
            sentinel.BoundsExtentX = 0.0f;
            sentinel.BoundsExtentY = 0.0f;
            sentinel.BoundsExtentZ = 0.0f;
            sentinel.GlobalQualityWeight = 0.0f;
            sentinel.StateHash = 0u;
            sentinel._pad0 = 0u;
            for (int i = 0; i < _ring.Length; i++)
                _ring[i] = sentinel;
        }

        private static void Release()
        {
            if (!_ring.IsCreated)
                return;

            AITextureNativeMemory.DisposeArray(ref _ring);
            _cursor = 0;
        }

        private static void WriteEntry(BinaryWriter writer, AITextureBakeTelemetryEntry entry)
        {
            writer.Write(entry.SourceHash);
            writer.Write(entry.MeshHash);
            writer.Write(entry.Resolution);
            writer.Write(entry.PassMask);
            writer.Write(entry.RenderMicroseconds);
            writer.Write(entry.EncodeMicroseconds);
            writer.Write(entry.WriteMicroseconds);
            writer.Write(entry.VertexCount);
            writer.Write(entry.SubMeshCount);
            writer.Write(entry.WarningFlags);
            writer.Write(entry.BoundsExtentX);
            writer.Write(entry.BoundsExtentY);
            writer.Write(entry.BoundsExtentZ);
            writer.Write(entry.GlobalQualityWeight);
            writer.Write(entry.StateHash);
            writer.Write(entry._pad0);
        }
    }
}
#endif

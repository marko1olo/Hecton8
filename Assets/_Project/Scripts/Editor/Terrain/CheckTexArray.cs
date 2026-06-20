using UnityEditor;
using UnityEngine;
using System.IO;

namespace Hecton8.Editor.Terrain {
    public static class CheckTexArray {
        public static void Run() {
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/Terrain_AlbedoArray.asset");
            if (albedo == null) {
                Debug.LogError("Albedo array not found!");
                return;
            }
            Debug.Log($"[CheckTexArray] AlbedoArray: {albedo.width}x{albedo.height}, depth: {albedo.depth}, format: {albedo.format}");
            
            // Extract slice 0 to see what it is
            Texture2D slice = new Texture2D(albedo.width, albedo.height, TextureFormat.RGBA32, false, false);
            UnityEngine.Graphics.CopyTexture(albedo, 0, 0, slice, 0, 0);
            File.WriteAllBytes("C:/hades/Hecton8/Logs/AlbedoArray_Slice0.png", slice.EncodeToPNG());
            Debug.Log("[CheckTexArray] Dumped AlbedoArray_Slice0.png");
        }
    }
}

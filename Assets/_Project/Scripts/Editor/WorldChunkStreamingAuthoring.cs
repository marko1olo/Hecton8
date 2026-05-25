using Hecton8.World;
using UnityEditor;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.EditorTools
{
    public static class WorldChunkStreamingAuthoring
    {
        private const string ProfileFolder = "Assets/_Project/Data/World/Streaming";
        private const string ProfilePath = ProfileFolder + "/WorldChunkStreamingProfile.asset";

        [MenuItem("Hecton/Authoring/Build World Chunk Streaming Profile", priority = 43)]
        public static void BuildWorldChunkStreamingProfile()
        {
            EnsureFolder("Assets/_Project/Data");
            EnsureFolder("Assets/_Project/Data/World");
            EnsureFolder(ProfileFolder);

            WorldChunkStreamingProfile profile = AssetDatabase.LoadAssetAtPath<WorldChunkStreamingProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<WorldChunkStreamingProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            profile.worldSizeMeters = 15000f;
            profile.chunkSizeMeters = 192f;
            profile.chunkCellSizeMeters = 64f;
            profile.macroZoneSizeMeters = 768f;
            profile.fullSimulationRadius = 180f;
            profile.midSimulationRadius = 420f;
            profile.visualResidencyRadius = 900f;
            profile.dataResidencyRadius = 1800f;
            profile.layers = new[]
            {
                WorldChunkStreamingProfile.CreateDefaultLayerProfile(WorldStreamingLayer.TerrainLod),
                WorldChunkStreamingProfile.CreateDefaultLayerProfile(WorldStreamingLayer.Flora),
                WorldChunkStreamingProfile.CreateDefaultLayerProfile(WorldStreamingLayer.Debris),
                WorldChunkStreamingProfile.CreateDefaultLayerProfile(WorldStreamingLayer.Resources),
                WorldChunkStreamingProfile.CreateDefaultLayerProfile(WorldStreamingLayer.Fauna),
                WorldChunkStreamingProfile.CreateDefaultLayerProfile(WorldStreamingLayer.Construction),
                WorldChunkStreamingProfile.CreateDefaultLayerProfile(WorldStreamingLayer.LargeThreats)
            };

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            H8Debug.Log($"[WorldChunkStreamingAuthoring] Built chunk streaming profile: {ProfilePath}");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int separatorIndex = path.LastIndexOf('/');
            if (separatorIndex <= 0)
                return;

            string parent = path.Substring(0, separatorIndex);
            string folderName = path.Substring(separatorIndex + 1);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}

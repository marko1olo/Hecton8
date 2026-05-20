using System;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Editor-baked octahedral impostor atlas and bounds metadata consumed by far-field renderers.
    /// </summary>
    [CreateAssetMenu(fileName = "ImpostorData_", menuName = "HECTON-8/Rendering/Octahedral Impostor Data")]
    public sealed class HectonOctahedralImpostorData : ScriptableObject
    {
        public const int ViewCount = 16;
        public const int DefaultAtlasSize = 4096;
        public static readonly Vector2Int DefaultAtlasGrid = new Vector2Int(4, 4);

        [SerializeField] private Texture2D _albedoDepthAtlas;
        [SerializeField] private Texture2D _normalDepthAtlas;
        [SerializeField] private Bounds _sourceBounds = new Bounds(Vector3.zero, Vector3.one);
        [SerializeField] private Vector3 _pivotOffset;
        [SerializeField] private Vector2Int _atlasGrid = DefaultAtlasGrid;
        [SerializeField, Min(1)] private int _atlasSize = DefaultAtlasSize;
        [SerializeField, Min(1)] private int _viewCount = ViewCount;
        [SerializeField, Min(0.01f)] private float _captureOrthoSize = 1f;
        [SerializeField, Min(0.01f)] private float _captureDepthMeters = 1f;
        [SerializeField, Min(0.01f)] private float _realGeometryDistanceMeters = HectonChunkImpostorResidency.DefaultImpostorEnterDistanceMeters;
        [SerializeField, Min(0f)] private float _dilationRadiusPixels = 4f;
        [SerializeField, Min(0f)] private float _depthScaleMeters = 1f;
        [SerializeField] private string _profileName = "Massive_Wreck";

        public Texture2D AlbedoDepthAtlas => _albedoDepthAtlas;
        public Texture2D NormalDepthAtlas => _normalDepthAtlas;
        public Bounds SourceBounds => _sourceBounds;
        public Vector3 PivotOffset => _pivotOffset;
        public Vector2Int AtlasGrid => _atlasGrid;
        public int AtlasSize => _atlasSize;
        public int BakedViewCount => _viewCount;
        public float CaptureOrthoSize => _captureOrthoSize;
        public float CaptureDepthMeters => _captureDepthMeters;
        public float RealGeometryDistanceMeters => _realGeometryDistanceMeters;
        public float DilationRadiusPixels => _dilationRadiusPixels;
        public float DepthScaleMeters => _depthScaleMeters;
        public string ProfileName => _profileName;

        public void Configure(
            Texture2D albedoDepthAtlas,
            Texture2D normalDepthAtlas,
            Bounds sourceBounds,
            Vector3 pivotOffset,
            int atlasSize,
            float captureOrthoSize,
            float captureDepthMeters,
            float realGeometryDistanceMeters)
        {
            Configure(
                albedoDepthAtlas,
                normalDepthAtlas,
                sourceBounds,
                pivotOffset,
                atlasSize,
                captureOrthoSize,
                captureDepthMeters,
                realGeometryDistanceMeters,
                4f,
                captureDepthMeters,
                "Massive_Wreck");
        }

        public void Configure(
            Texture2D albedoDepthAtlas,
            Texture2D normalDepthAtlas,
            Bounds sourceBounds,
            Vector3 pivotOffset,
            int atlasSize,
            float captureOrthoSize,
            float captureDepthMeters,
            float realGeometryDistanceMeters,
            float dilationRadiusPixels,
            float depthScaleMeters,
            string profileName)
        {
            Configure(
                albedoDepthAtlas,
                normalDepthAtlas,
                sourceBounds,
                pivotOffset,
                atlasSize,
                captureOrthoSize,
                captureDepthMeters,
                realGeometryDistanceMeters,
                dilationRadiusPixels,
                depthScaleMeters,
                profileName,
                DefaultAtlasGrid);
        }

        public void Configure(
            Texture2D albedoDepthAtlas,
            Texture2D normalDepthAtlas,
            Bounds sourceBounds,
            Vector3 pivotOffset,
            int atlasSize,
            float captureOrthoSize,
            float captureDepthMeters,
            float realGeometryDistanceMeters,
            float dilationRadiusPixels,
            float depthScaleMeters,
            string profileName,
            Vector2Int atlasGrid)
        {
            Configure(
                albedoDepthAtlas,
                normalDepthAtlas,
                sourceBounds,
                pivotOffset,
                atlasSize,
                captureOrthoSize,
                captureDepthMeters,
                realGeometryDistanceMeters,
                dilationRadiusPixels,
                depthScaleMeters,
                profileName,
                atlasGrid,
                atlasGrid.x * atlasGrid.y);
        }

        public void Configure(
            Texture2D albedoDepthAtlas,
            Texture2D normalDepthAtlas,
            Bounds sourceBounds,
            Vector3 pivotOffset,
            int atlasSize,
            float captureOrthoSize,
            float captureDepthMeters,
            float realGeometryDistanceMeters,
            float dilationRadiusPixels,
            float depthScaleMeters,
            string profileName,
            Vector2Int atlasGrid,
            int viewCount)
        {
            _albedoDepthAtlas = albedoDepthAtlas;
            _normalDepthAtlas = normalDepthAtlas;
            _sourceBounds = sourceBounds.size.sqrMagnitude > 0.0001f
                ? sourceBounds
                : new Bounds(Vector3.zero, Vector3.one);
            _pivotOffset = pivotOffset;
            _atlasGrid = new Vector2Int(Mathf.Max(1, atlasGrid.x), Mathf.Max(1, atlasGrid.y));
            _atlasSize = Mathf.Max(256, atlasSize);
            _viewCount = Mathf.Clamp(viewCount, 1, 64);
            _captureOrthoSize = Mathf.Max(0.01f, captureOrthoSize);
            _captureDepthMeters = Mathf.Max(0.01f, captureDepthMeters);
            _realGeometryDistanceMeters = Mathf.Max(1f, realGeometryDistanceMeters);
            _dilationRadiusPixels = Mathf.Max(0f, dilationRadiusPixels);
            _depthScaleMeters = Mathf.Max(0.01f, depthScaleMeters);
            _profileName = string.IsNullOrEmpty(profileName) ? "Massive_Wreck" : profileName;
        }

        public OctahedralImpostorInstance CreateInstance(Vector3 universeCenter, Vector3 size, float fade01, uint flags)
        {
            Vector3 resolvedSize = size.sqrMagnitude > 0.0001f ? size : _sourceBounds.size;
            return OctahedralImpostorInstance.Create(universeCenter, resolvedSize, fade01, 0f, flags);
        }
    }
}

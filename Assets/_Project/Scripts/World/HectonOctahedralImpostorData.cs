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
        public const int ViewCount = 8;
        public const int DefaultAtlasSize = 2048;
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
            _albedoDepthAtlas = albedoDepthAtlas;
            _normalDepthAtlas = normalDepthAtlas;
            _sourceBounds = sourceBounds.size.sqrMagnitude > 0.0001f
                ? sourceBounds
                : new Bounds(Vector3.zero, Vector3.one);
            _pivotOffset = pivotOffset;
            _atlasGrid = DefaultAtlasGrid;
            _atlasSize = Mathf.Max(256, atlasSize);
            _viewCount = ViewCount;
            _captureOrthoSize = Mathf.Max(0.01f, captureOrthoSize);
            _captureDepthMeters = Mathf.Max(0.01f, captureDepthMeters);
            _realGeometryDistanceMeters = Mathf.Max(1f, realGeometryDistanceMeters);
        }

        public OctahedralImpostorInstance CreateInstance(Vector3 universeCenter, Vector3 size, float fade01, uint flags)
        {
            Vector3 resolvedSize = size.sqrMagnitude > 0.0001f ? size : _sourceBounds.size;
            return OctahedralImpostorInstance.Create(universeCenter, resolvedSize, fade01, 0f, flags);
        }
    }
}

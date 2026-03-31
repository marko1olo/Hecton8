using UnityEngine;

namespace Hecton8.World
{
    [System.Serializable]
    public struct WorldChunkCoordinate
    {
        public int x;
        public int z;

        public WorldChunkCoordinate(int x, int z)
        {
            this.x = x;
            this.z = z;
        }

        public Vector2Int ToVector2Int()
        {
            return new Vector2Int(x, z);
        }

        public static WorldChunkCoordinate FromWorldPosition(Vector3 worldPosition, float chunkSize)
        {
            float safeChunkSize = Mathf.Max(1f, chunkSize);
            int chunkX = Mathf.FloorToInt(worldPosition.x / safeChunkSize);
            int chunkZ = Mathf.FloorToInt(worldPosition.z / safeChunkSize);
            return new WorldChunkCoordinate(chunkX, chunkZ);
        }

        public static Vector3 ToWorldCenter(WorldChunkCoordinate coord, float chunkSize, float y = 0f)
        {
            float safeChunkSize = Mathf.Max(1f, chunkSize);
            return new Vector3(
                (coord.x + 0.5f) * safeChunkSize,
                y,
                (coord.z + 0.5f) * safeChunkSize);
        }

        public override string ToString()
        {
            return $"({x},{z})";
        }
    }
}

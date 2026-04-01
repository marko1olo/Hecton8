using UnityEngine;

namespace Hecton8.World
{
    [System.Serializable]
    public readonly struct WorldMacroZoneCoordinate
    {
        public readonly int x;
        public readonly int z;

        public WorldMacroZoneCoordinate(int x, int z)
        {
            this.x = x;
            this.z = z;
        }

        public static WorldMacroZoneCoordinate FromWorldPosition(Vector3 worldPosition, float macroZoneSize)
        {
            float safeSize = Mathf.Max(1f, macroZoneSize);
            int zoneX = Mathf.FloorToInt(worldPosition.x / safeSize);
            int zoneZ = Mathf.FloorToInt(worldPosition.z / safeSize);
            return new WorldMacroZoneCoordinate(zoneX, zoneZ);
        }

        public static Vector3 ToWorldCenter(WorldMacroZoneCoordinate coord, float macroZoneSize, float y = 0f)
        {
            float safeSize = Mathf.Max(1f, macroZoneSize);
            return new Vector3(
                (coord.x + 0.5f) * safeSize,
                y,
                (coord.z + 0.5f) * safeSize);
        }

        public int ChebyshevDistanceTo(WorldMacroZoneCoordinate other)
        {
            int dx = Mathf.Abs(x - other.x);
            int dz = Mathf.Abs(z - other.z);
            return Mathf.Max(dx, dz);
        }

        public override string ToString()
        {
            return $"({x},{z})";
        }
    }
}

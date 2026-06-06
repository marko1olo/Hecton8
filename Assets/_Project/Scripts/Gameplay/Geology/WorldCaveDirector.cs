using UnityEngine;

namespace Hecton8.Gameplay.Geology
{
    public static class WorldCaveDirector
    {
        private const float MinimumEntranceRadius = 0.25f;
        private const float TorusMinorRadiusScale = 0.42f;
        private const float TorusNoiseAmplitude = 0.18f;

        public static float[,,] CarveCaveEntranceOverhang(float[,,] primaryWallSDF, Vector3 entranceCenter, float radius)
        {
            if (primaryWallSDF == null)
                return null;

            int sizeX = primaryWallSDF.GetLength(0);
            int sizeY = primaryWallSDF.GetLength(1);
            int sizeZ = primaryWallSDF.GetLength(2);
            float[,,] carvedSdf = new float[sizeX, sizeY, sizeZ];
            float safeRadius = Mathf.Max(MinimumEntranceRadius, radius);
            float minorRadius = Mathf.Max(MinimumEntranceRadius * 0.5f, safeRadius * TorusMinorRadiusScale);

            for (int x = 0; x < sizeX; x++)
            {
                for (int y = 0; y < sizeY; y++)
                {
                    for (int z = 0; z < sizeZ; z++)
                    {
                        Vector3 sample = new Vector3(x, y, z);
                        float torusDistance = CalculateTorusSDF(sample, entranceCenter, safeRadius, minorRadius);
                        carvedSdf[x, y, z] = Mathf.Max(primaryWallSDF[x, y, z], -torusDistance);
                    }
                }
            }

            return carvedSdf;
        }

        public static void VerifyAndFixInteriorWindingOrder(Mesh shaftMesh)
        {
            if (shaftMesh == null)
                return;

            Vector3[] vertices = shaftMesh.vertices;
            int[] triangles = shaftMesh.triangles;
            if (vertices == null || vertices.Length == 0 || triangles == null || triangles.Length < 3)
                return;

            Vector3 centroid = CalculateMeshCentroid(vertices);
            bool changed = false;
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                int ia = triangles[i];
                int ib = triangles[i + 1];
                int ic = triangles[i + 2];
                if ((uint)ia >= (uint)vertices.Length || (uint)ib >= (uint)vertices.Length || (uint)ic >= (uint)vertices.Length)
                    continue;

                Vector3 v0 = vertices[ia];
                Vector3 v1 = vertices[ib];
                Vector3 v2 = vertices[ic];
                Vector3 faceCenter = (v0 + v1 + v2) * (1f / 3f);
                Vector3 outwardVector = (faceCenter - centroid).normalized;
                Vector3 currentNormal = Vector3.Cross(v1 - v0, v2 - v0).normalized;
                if (Vector3.Dot(outwardVector, currentNormal) <= 0f)
                    continue;

                triangles[i + 1] = ic;
                triangles[i + 2] = ib;
                changed = true;
            }

            if (!changed)
                return;

            shaftMesh.triangles = triangles;
            shaftMesh.RecalculateNormals();
            shaftMesh.RecalculateBounds();
        }

        private static float CalculateTorusSDF(Vector3 sample, Vector3 center, float majorRadius, float minorRadius)
        {
            Vector3 local = sample - center;
            float polar = Mathf.Atan2(local.z, local.x);
            float noise = Mathf.Sin(polar * 3.1f + local.y * 0.17f) *
                          Mathf.Cos(polar * 1.7f - local.y * 0.11f) *
                          TorusNoiseAmplitude;
            float localMinorRadius = Mathf.Max(MinimumEntranceRadius * 0.5f, minorRadius * (1f + noise));
            Vector2 q = new Vector2(new Vector2(local.x, local.z).magnitude - majorRadius, local.y);
            return q.magnitude - localMinorRadius;
        }

        private static Vector3 CalculateMeshCentroid(Vector3[] vertices)
        {
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < vertices.Length; i++)
                sum += vertices[i];
            return sum / Mathf.Max(1, vertices.Length);
        }
    }
}

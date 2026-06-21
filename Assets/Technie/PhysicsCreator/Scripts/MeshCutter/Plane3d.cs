using UnityEngine;

namespace Technie.PhysicsCreator
{
    public struct Plane3d
    {
        public Vector3 normal;
        public double distance;

        public Plane3d(Vector3 inNormal, Vector3 inPoint)
        {
            normal = Vector3.Normalize(inNormal);
            distance = -((double)normal.x * inPoint.x + (double)normal.y * inPoint.y + (double)normal.z * inPoint.z);
        }

        public Plane3d(Plane plane)
        {
            normal = plane.normal;
            distance = plane.distance;
        }

        public double GetDistanceToPoint(Vector3 point)
        {
            return (double)normal.x * point.x + (double)normal.y * point.y + (double)normal.z * point.z + distance;
        }

        public bool Raycast(Ray ray, out float enter)
        {
            double a = Vector3.Dot(ray.direction, normal);
            double num = -GetDistanceToPoint(ray.origin);

            // Using double precision internally for check and computation
            if (System.Math.Abs(a) < 1E-05f)
            {
                enter = 0f;
                return false;
            }

            enter = (float)(num / a);
            return enter > 0f;
        }
    }
}

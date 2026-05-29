using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CandiceAIforGames.AI.Pathfinding
{
    public class Path
    {
        public readonly Vector3[] lookPoints;
        public readonly Line[] turnBoundaries;
        public int finishLineIndex;
        public int slowDownIndex;
        public int lookPointCount;
        public Path(Vector3[] waypoints, Vector3 startPos, float turnDist, float stoppingDist)
        {
            int capacity = waypoints == null || waypoints.Length == 0 ? 1 : waypoints.Length;
            lookPoints = new Vector3[capacity];
            turnBoundaries = new Line[capacity];
            Set(waypoints, waypoints == null ? 0 : waypoints.Length, startPos, turnDist, stoppingDist);
        }

        public Path(int capacity)
        {
            int safeCapacity = Mathf.Max(1, capacity);
            lookPoints = new Vector3[safeCapacity];
            turnBoundaries = new Line[safeCapacity];
            finishLineIndex = 0;
            slowDownIndex = 0;
            lookPointCount = 0;
        }

        public void Set(Vector3[] waypoints, int waypointCount, Vector3 startPos, float turnDist, float stoppingDist)
        {
            lookPointCount = 0;
            finishLineIndex = 0;
            slowDownIndex = 0;
            if (waypoints == null || waypointCount <= 0)
            {
                return;
            }

            lookPointCount = Mathf.Min(waypointCount, lookPoints.Length);
            for (int i = 0; i < lookPointCount; i++)
            {
                lookPoints[i] = waypoints[i];
            }

            finishLineIndex = lookPointCount - 1;
            Vector2 previousPoint = V3ToV2(startPos);

            for (int i = 0; i < lookPointCount; i++)
            {
                Vector2 currentPoint = V3ToV2(lookPoints[i]);
                Vector2 directionToCurrentPoint = (currentPoint - previousPoint).normalized;
                Vector2 turnBoundaryPoint = (i == finishLineIndex) ? currentPoint : currentPoint - directionToCurrentPoint * turnDist;
                turnBoundaries[i] = new Line(turnBoundaryPoint, previousPoint - directionToCurrentPoint * turnDist);
                previousPoint = turnBoundaryPoint;

            }
            float distFromEndPoint = 0;
            for (int i = lookPointCount - 1; i > 0; i--)
            {
                distFromEndPoint += Vector3.Distance(lookPoints[i], lookPoints[i - 1]);
                if (distFromEndPoint > stoppingDist)
                {
                    slowDownIndex = i;
                    break;
                }
            }

        }

        Vector2 V3ToV2(Vector3 v3)
        {
            return new Vector2(v3.x, v3.z);
        }
        public void DrawWithGizmos()
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < lookPointCount; i++)
            {
                Vector3 p = lookPoints[i];
                Gizmos.DrawSphere(p + Vector3.up, .5f);
            }

            Gizmos.color = Color.white;
            for (int i = 0; i < lookPointCount; i++)
            {
                Line l = turnBoundaries[i];
                l.DrawWithGizmos(10);
            }
        }
    }

}

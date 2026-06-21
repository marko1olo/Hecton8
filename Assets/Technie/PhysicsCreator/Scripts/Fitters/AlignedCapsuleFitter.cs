using UnityEngine;
using System.Collections.Generic;
using Technie.PhysicsCreator.Rigid;

namespace Technie.PhysicsCreator
{
	public class AlignedCapsuleFitter
	{
		public CapsuleDef Fit(Hull hull, Vector3[] meshVertices, int[] meshIndices)
		{
			Vector3[] hullVertices;
			int[] hullIndices;
			hull.GenerateMathHull(meshVertices, meshIndices, out hullVertices, out hullIndices);

			return Fit(hullVertices, hullIndices);
		}

		public CapsuleDef Fit(Vector3[] hullVertices, int[] hullIndices)
		{
			if (hullVertices == null || hullVertices.Length == 0 || hullIndices == null || hullIndices.Length == 0)
				return new CapsuleDef();

			// Have to align the capsule along one of the three primary axies
			// Fit a box to the points to get a rough first approximation

			ConstructionPlane basePlane = new ConstructionPlane(Vector3.zero);
			RotatedBox tightestBox = RotatedBoxFitter.FindTightestBox(basePlane, hullVertices);

			if (tightestBox == null)
				return new CapsuleDef();

			// Find the longest axis to put the primary capsule axis along

			ConstructionPlane capsulePlane;
			CapsuleAxis axis;
			if (tightestBox.size.x > tightestBox.size.y && tightestBox.size.x > tightestBox.size.z)
			{
				// Longest along X axis
				capsulePlane = new ConstructionPlane(tightestBox.center, Vector3.right, Vector3.forward);
				axis = CapsuleAxis.X;
			}
			else if (tightestBox.size.y > tightestBox.size.z)
			{
				// Longest along Y axis
				capsulePlane = new ConstructionPlane(tightestBox.center, Vector3.up, Vector3.right);
				axis = CapsuleAxis.Y;
			}
			else
			{
				// Longest along Z axis
				capsulePlane = new ConstructionPlane(tightestBox.center, Vector3.forward, Vector3.right);
				axis = CapsuleAxis.Z;
			}

			// Fit a capsule 
			RotatedCapsule capsule = RotatedCapsuleFitter.FitCapsule(capsulePlane, hullVertices);

			// Refine it for a tighter fit
			RotatedCapsule bestCapsule;
			ConstructionPlane bestPlane;
			RotatedCapsuleFitter.Refine(capsule, capsulePlane, hullVertices, out bestCapsule, out bestPlane);

			CapsuleDef result = new CapsuleDef();
			result.capsuleDirection = axis;
			result.capsuleRadius = bestCapsule.radius;
			result.capsuleHeight = bestCapsule.height;
			result.capsuleCenter = bestCapsule.center;
			result.capsulePosition = Vector3.zero;
			result.capsuleRotation = Quaternion.identity;
		
			return result;
		}
	}
}

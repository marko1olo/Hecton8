using UnityEngine;
using System.Collections.Generic;

namespace Technie.PhysicsCreator.Skinned
{
	[System.Serializable]
	public class BoneHullData : IHull
	{
		public string Name
		{
			get { return targetBoneName; }
		}

		public float MinThreshold { get { return this.minThreshold; } }
		public float MaxThreshold { get { return this.maxThreshold; } }

		public int NumSelectedTriangles
		{
			get { return selectedFaces.Count; }
		}

		public Vector3[] CachedTriangleVertices
		{
			get { return cachedTriangleVertices.ToArray(); }
			set
			{
				cachedTriangleVertices.Clear();
				cachedTriangleVertices.AddRange(value);
			}
		}

		public string targetBoneName;
		public HullType type = HullType.Auto;
		public ColliderType colliderType = ColliderType.Convex;

		// Common properties
		public Color previewColour;
		public Mesh hullMesh; // the generated convex hull
		public PhysicsMaterial material;
		public bool isTrigger;

		// Auto properties
		[SerializeField]
		private float minThreshold;

		[SerializeField]
		private float maxThreshold;

		// Manual properties
		[SerializeField]
		private List<int> selectedFaces = new List<int>();  // selected triangle indices
		public List<Vector3> cachedTriangleVertices = new List<Vector3>();  // TODO Implement this

		// Cache of the faces indices for triangles that are fully between the min/max thresholds
		//private List<int> thresholdSelectedFaces = new List<int>();


		public bool IsTrianglePainted(int triIndex)
		{
			if (type == HullType.Manual)
			{
				return selectedFaces.Contains(triIndex);
			}
			return false;	
		}

		public int[] GetSelectedFaces()
		{
			return selectedFaces.ToArray();
		}

		public void AddToSelection(int newTriangleIndex, Mesh srcMesh)
		{
			if (selectedFaces.Contains(newTriangleIndex))
				return;

			this.selectedFaces.Add(newTriangleIndex);

			Utils.UpdateCachedVertices(this, srcMesh);
		}

		public void RemoveFromSelection(int existingTriangleIndex, Mesh srcMesh)
		{
			this.selectedFaces.Remove(existingTriangleIndex);

			Utils.UpdateCachedVertices(this, srcMesh);
		}

		public void SetMinThreshold(float newMinThreshold)
		{
			this.minThreshold = newMinThreshold;
		}

		public void SetMaxThreshold(float newMaxThreshold)
		{
			this.maxThreshold = newMaxThreshold;
		}
		
		public void SetThresholds(float newMinThreshold, float newMaxThreshold, SkinnedMeshRenderer renderer, Mesh targetMesh)
		{
			this.minThreshold = newMinThreshold;
			this.maxThreshold = newMaxThreshold;

			SkinnedMeshRenderer skinnedRenderer = renderer as SkinnedMeshRenderer;
			BoneWeight[] weights = targetMesh.boneWeights;
			int[] triangles = targetMesh.triangles;
			int numTris = triangles.Length / 3;

			selectedFaces.Clear();

			Transform bone = SkinnedColliderCreator.FindBone(skinnedRenderer, targetBoneName);
			int ownBoneIndex = Utils.FindBoneIndex(skinnedRenderer, bone);

			for (int i = 0; i< numTris; i++)
			{
				int triIndex = i;
				int i0 = triangles[triIndex * 3];
				int i1 = triangles[triIndex * 3 + 1];
				int i2 = triangles[triIndex * 3 + 2];

				BoneWeight w0 = weights[i0];
				BoneWeight w1 = weights[i1];
				BoneWeight w2 = weights[i2];

				if (Utils.IsWeightAboveThreshold(w0, ownBoneIndex, minThreshold, maxThreshold)
					&& Utils.IsWeightAboveThreshold(w1, ownBoneIndex, minThreshold, maxThreshold)
					&& Utils.IsWeightAboveThreshold(w2, ownBoneIndex, minThreshold, maxThreshold))
				{
					selectedFaces.Add(triIndex);
				}
			}

			Utils.UpdateCachedVertices(this, targetMesh);
		}

		public void ClearSelectedFaces()
		{
			if (type == HullType.Manual)
			{
				selectedFaces.Clear();
				cachedTriangleVertices.Clear();
			}
		}

		public void SetSelectedFaces(List<int> newSelectedFaceIndices, Mesh srcMesh)
		{
			if (type == HullType.Manual)
			{
				selectedFaces.Clear();
				selectedFaces.AddRange(newSelectedFaceIndices);

				Utils.UpdateCachedVertices(this, srcMesh);
			}
		}








		public float CalculateVolume()
		{
			if (this.cachedTriangleVertices == null || this.cachedTriangleVertices.Count == 0)
				return 0.001f;

			Vector3 min = this.cachedTriangleVertices[0];
			Vector3 max = this.cachedTriangleVertices[0];

			for (int i = 1; i < this.cachedTriangleVertices.Count; i++)
			{
				Vector3 v = this.cachedTriangleVertices[i];
				min = Vector3.Min(min, v);
				max = Vector3.Max(max, v);
			}

			Vector3 size = max - min;

			if (colliderType == ColliderType.Box)
			{
				return size.x * size.y * size.z;
			}
			else if (colliderType == ColliderType.Sphere)
			{
				float radius = Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * 0.5f;
				return (4.0f / 3.0f) * Mathf.PI * radius * radius * radius;
			}
			else if (colliderType == ColliderType.Capsule)
			{
				float radius = Mathf.Max(size.x, size.z) * 0.5f;
				float height = size.y;
				float internalLength = Mathf.Max(height - (radius * 2), 0.0f);
				return Mathf.PI * (radius * radius) * internalLength + (4.0f / 3.0f) * Mathf.PI * radius * radius * radius;
			}
			else if (colliderType == ColliderType.Convex)
			{
				// Approximation of convex hull volume via bounding box
				return size.x * size.y * size.z * 0.5f;
			}

			return 0.001f;
		}

		public Vector3[] GetCachedTriangleVertices()
		{
			return cachedTriangleVertices.ToArray();
		}
	}

} // namespace Technie.PhysicsCreator.Skinned

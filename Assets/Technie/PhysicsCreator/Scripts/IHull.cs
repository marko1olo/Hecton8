using System.Collections.Generic;
using UnityEngine;

namespace Technie.PhysicsCreator
{
	
	public interface IHull
	{
		string Name
		{
			get;
		}

		Vector3[] CachedTriangleVertices
		{
			get;
			set;
		}

		int NumSelectedTriangles
		{
			get;
		}

		bool IsTrianglePainted(int triIndex);

		int[] GetSelectedFaces();

		void ClearSelectedFaces();

		void SetSelectedFaces(List<int> newSelectedFaceIndices, Mesh srcMesh);
	}
	
} // namespace Technie.PhysicsCreator

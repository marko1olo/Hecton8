using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Technie.PhysicsCreator
{
	public class CuttableSubMesh
	{
		private List<Vector3> vertices;
		private List<Vector3> normals;
		private List<Vector4> tangents;
		private List<Color32> colours;
		private List<Vector2> uvs;
		private List<Vector2> uv1s;



		public CuttableSubMesh(bool hasNormals, bool hasTangents, bool hasColours, bool hasUvs, bool hasUv1)

		{
			vertices = new List<Vector3>();

			if (hasNormals)
				normals = new List<Vector3>();

			if (hasTangents)
				tangents = new List<Vector4>();

			if (hasColours)
				colours = new List<Color32>();

			if (hasUvs)
				uvs = new List<Vector2>();

			if (hasUv1)
				uv1s = new List<Vector2>();


		}


		public CuttableSubMesh(int[] indices, Vector3[] inputVertices, Vector3[] inputNormals, Vector4[] inputTangents, Color32[] inputColours, Vector2[] inputUvs, Vector2[] inputUv1)

		{
			vertices = new List<Vector3>();

			if (inputNormals != null && inputNormals.Length > 0)
				normals = new List<Vector3>();

			if (inputTangents != null && inputTangents.Length > 0)
				tangents = new List<Vector4>();

			if (inputColours != null && inputColours.Length > 0)
				colours = new List<Color32>();

			if (inputUvs != null && inputUvs.Length > 0)
				uvs = new List<Vector2>();

			if (inputUv1 != null && inputUv1.Length > 0)
				uv1s = new List<Vector2>();



			for (int i = 0; i < indices.Length; i++)
			{
				int nextIndex = indices[i];

				this.vertices.Add(inputVertices[nextIndex]);

				if (normals != null)
					this.normals.Add(inputNormals[nextIndex]);

				if (tangents != null)
					this.tangents.Add(inputTangents[nextIndex]);

				if (colours != null)
					colours.Add(inputColours[nextIndex]);

				if (uvs != null)
					uvs.Add(inputUvs[nextIndex]);

				if (uv1s != null)
					uv1s.Add(inputUv1[nextIndex]);


			}

		}

		public void Add(CuttableSubMesh other)
		{
			for (int i = 0; i < other.vertices.Count; i++)
			{
				CopyVertex(i, other);
			}
		}

		public int NumVertices()
		{
			return vertices.Count;
		}

		public Vector3 GetVertex(int index)
		{
			return vertices[index];
		}

		public bool HasNormals()
		{
			return normals != null;
		}

		public bool HasTangents()
		{
			return tangents != null;
		}

		public bool HasColours()
		{
			return colours != null;
		}

		public bool HasUvs()
		{
			return uvs != null;
		}

		public bool HasUv1()
		{
			return uv1s != null;
		}

		public bool HasTangents()
		{
			return tangents != null;
		}

		public void CopyVertex(int srcIndex, CuttableSubMesh srcMesh)
		{
			vertices.Add(srcMesh.vertices[srcIndex]);

			if (normals != null)
				normals.Add(srcMesh.normals[srcIndex]);

			if (tangents != null)
				tangents.Add(srcMesh.tangents[srcIndex]);

			if (colours != null)
				colours.Add(srcMesh.colours[srcIndex]);

			if (uvs != null)
				uvs.Add(srcMesh.uvs[srcIndex]);

			if (uv1s != null)
				uv1s.Add(srcMesh.uv1s[srcIndex]);


		}

		public void AddInterpolatedVertex(int i0, int i1, float weight, CuttableSubMesh srcMesh)
		{
			Vector3 v0 = srcMesh.GetVertex(i0);
			Vector3 v1 = srcMesh.GetVertex(i1);

			vertices.Add(Vector3.Lerp(v0, v1, weight));

			if (normals != null)
				normals.Add(Vector3.Lerp(srcMesh.normals[i0], srcMesh.normals[i1], weight).normalized);

			if (tangents != null)
			{
				Vector4 t0 = srcMesh.tangents[i0];
				Vector4 t1 = srcMesh.tangents[i1];
				Vector3 interpolatedTangent = Vector3.Lerp(t0, t1, weight).normalized;
				tangents.Add(new Vector4(interpolatedTangent.x, interpolatedTangent.y, interpolatedTangent.z, t0.w));
			}

			if (colours != null)
				colours.Add(Color32.Lerp(srcMesh.colours[i0], srcMesh.colours[i1], weight));

			if (uvs != null)
				uvs.Add(Vector2.Lerp(srcMesh.uvs[i0], srcMesh.uvs[i1], weight));

			if (uv1s != null)
				uv1s.Add(Vector2.Lerp(srcMesh.uv1s[i0], srcMesh.uv1s[i1], weight));

		}

		public void AddTo(List<Vector3> destVertices, List<Vector3> destNormals, List<Vector4> destTangents, List<Color32> destColours, List<Vector2> destUvs, List<Vector2> destUv1s)

		{
			destVertices.AddRange(this.vertices);

			if (normals != null)
				destNormals.AddRange(this.normals);

			if (tangents != null)
				destTangents.AddRange(this.tangents);

			if (colours != null)
				destColours.AddRange(colours);

			if (uvs != null)
				destUvs.AddRange(uvs);

			if (uv1s != null)
				destUv1s.AddRange(uv1s);


		}

		public int NumIndices()
		{
			return vertices.Count;
		}

		public int[] GenIndices()
		{
			int[] indices = new int[vertices.Count];
			for (int i = 0; i < indices.Length; i++)
			{
				indices[i] = i;
			}
			return indices;
		}
	}
} // namespace Technie.PhysicsCreator

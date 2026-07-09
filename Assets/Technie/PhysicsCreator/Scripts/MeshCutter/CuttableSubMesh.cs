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
		private List<Vector2> uv2s;
		private List<Vector2> uv3s;
		private List<Vector2> uv4s;
		private List<Vector2> uv5s;
		private List<Vector2> uv6s;
		private List<Vector2> uv7s;

		public CuttableSubMesh(bool hasNormals, bool hasColours, bool hasUvs, bool hasUv1, bool hasUv2, bool hasUv3, bool hasUv4, bool hasUv5, bool hasUv6, bool hasUv7, bool hasTangents)
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

			if (hasUv2)
				uv2s = new List<Vector2>();

			if (hasUv3)
				uv3s = new List<Vector2>();

			if (hasUv4)
				uv4s = new List<Vector2>();

			if (hasUv5)
				uv5s = new List<Vector2>();

			if (hasUv6)
				uv6s = new List<Vector2>();

			if (hasUv7)
				uv7s = new List<Vector2>();

			if (hasTangents)
				tangents = new List<Vector4>();
		}

		public CuttableSubMesh(int[] indices, Vector3[] inputVertices, Vector3[] inputNormals, Color32[] inputColours, Vector2[] inputUvs, Vector2[] inputUv1, Vector2[] inputUv2, Vector2[] inputUv3, Vector2[] inputUv4, Vector2[] inputUv5, Vector2[] inputUv6, Vector2[] inputUv7, Vector4[] inputTangents)
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

			if (inputUv2 != null && inputUv2.Length > 0)
				uv2s = new List<Vector2>();

			if (inputUv3 != null && inputUv3.Length > 0)
				uv3s = new List<Vector2>();

			if (inputUv4 != null && inputUv4.Length > 0)
				uv4s = new List<Vector2>();

			if (inputUv5 != null && inputUv5.Length > 0)
				uv5s = new List<Vector2>();

			if (inputUv6 != null && inputUv6.Length > 0)
				uv6s = new List<Vector2>();

			if (inputUv7 != null && inputUv7.Length > 0)
				uv7s = new List<Vector2>();

			if (inputTangents != null && inputTangents.Length > 0)
				tangents = new List<Vector4>();

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

				if (uv2s != null)
					uv2s.Add(inputUv2[nextIndex]);

				if (uv3s != null)
					uv3s.Add(inputUv3[nextIndex]);

				if (uv4s != null)
					uv4s.Add(inputUv4[nextIndex]);

				if (uv5s != null)
					uv5s.Add(inputUv5[nextIndex]);

				if (uv6s != null)
					uv6s.Add(inputUv6[nextIndex]);

				if (uv7s != null)
					uv7s.Add(inputUv7[nextIndex]);

				if (tangents != null)
					tangents.Add(inputTangents[nextIndex]);
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

		public bool HasUv2()
		{
			return uv2s != null;
		}

		public bool HasUv3()
		{
			return uv3s != null;
		}

		public bool HasUv4()
		{
			return uv4s != null;
		}

		public bool HasUv5()
		{
			return uv5s != null;
		}

		public bool HasUv6()
		{
			return uv6s != null;
		}

		public bool HasUv7()
		{
			return uv7s != null;
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

			if (uv2s != null)
				uv2s.Add(srcMesh.uv2s[srcIndex]);

			if (uv3s != null)
				uv3s.Add(srcMesh.uv3s[srcIndex]);

			if (uv4s != null)
				uv4s.Add(srcMesh.uv4s[srcIndex]);

			if (uv5s != null)
				uv5s.Add(srcMesh.uv5s[srcIndex]);

			if (uv6s != null)
				uv6s.Add(srcMesh.uv6s[srcIndex]);

			if (uv7s != null)
				uv7s.Add(srcMesh.uv7s[srcIndex]);

			if (tangents != null)
				tangents.Add(srcMesh.tangents[srcIndex]);
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

			if (uv2s != null)
				uv2s.Add(Vector2.Lerp(srcMesh.uv2s[i0], srcMesh.uv2s[i1], weight));

			if (uv3s != null)
				uv3s.Add(Vector2.Lerp(srcMesh.uv3s[i0], srcMesh.uv3s[i1], weight));

			if (uv4s != null)
				uv4s.Add(Vector2.Lerp(srcMesh.uv4s[i0], srcMesh.uv4s[i1], weight));

			if (uv5s != null)
				uv5s.Add(Vector2.Lerp(srcMesh.uv5s[i0], srcMesh.uv5s[i1], weight));

			if (uv6s != null)
				uv6s.Add(Vector2.Lerp(srcMesh.uv6s[i0], srcMesh.uv6s[i1], weight));

			if (uv7s != null)
				uv7s.Add(Vector2.Lerp(srcMesh.uv7s[i0], srcMesh.uv7s[i1], weight));

			if (tangents != null)
			{
				Vector4 t0 = srcMesh.tangents[i0];
				Vector4 t1 = srcMesh.tangents[i1];
				Vector3 t = Vector3.Lerp(new Vector3(t0.x, t0.y, t0.z), new Vector3(t1.x, t1.y, t1.z), weight).normalized;
				tangents.Add(new Vector4(t.x, t.y, t.z, t0.w));
			}
		}

		public void AddTo(List<Vector3> destVertices, List<Vector3> destNormals, List<Color32> destColours, List<Vector2> destUvs, List<Vector2> destUv1s, List<Vector2> destUv2s, List<Vector2> destUv3s, List<Vector2> destUv4s, List<Vector2> destUv5s, List<Vector2> destUv6s, List<Vector2> destUv7s, List<Vector4> destTangents)
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

			if (uv2s != null)
				destUv2s.AddRange(uv2s);

			if (uv3s != null)
				destUv3s.AddRange(uv3s);

			if (uv4s != null)
				destUv4s.AddRange(uv4s);

			if (uv5s != null)
				destUv5s.AddRange(uv5s);

			if (uv6s != null)
				destUv6s.AddRange(uv6s);

			if (uv7s != null)
				destUv7s.AddRange(uv7s);

			if (tangents != null)
				destTangents.AddRange(tangents);
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

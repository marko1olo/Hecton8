using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Technie.PhysicsCreator
{
	// Normalised, high-precision mesh stored in a format suitable to actually apply the cutting algorithm to
	//
	// Todo: need to implement other optional vertex attributes: tangents, uv2, uv3, etc.
	// 
	public class CuttableMesh
	{
		private MeshRenderer inputMeshRenderer;

		private bool hasUvs;
		private bool hasUv1s;
		private bool hasUv3s;
		private bool hasUv4s;
#if UNITY_2018_2_OR_NEWER
		private bool hasUv5s;
		private bool hasUv6s;
		private bool hasUv7s;
		private bool hasUv8s;
#endif
		private bool hasColours;
		private bool hasTangents;

		private List<CuttableSubMesh> subMeshes;

		public CuttableMesh(Mesh inputMesh)
		{
			Init(inputMesh, inputMesh.name);
		}

		public CuttableMesh(MeshRenderer input)
		{
			this.inputMeshRenderer = input;

			MeshFilter inputFilter = input.GetComponent<MeshFilter>();
			Mesh inputMesh = inputFilter.sharedMesh;

			Init(inputMesh, input.name);
		}

		private void Init(Mesh inputMesh, string debugName)
		{
			this.subMeshes = new List<CuttableSubMesh>();

			if (inputMesh.isReadable)
			{
				Vector3[] vertices = inputMesh.vertices;
				Vector3[] normals = inputMesh.normals;
				Vector2[] uvs = inputMesh.uv;
				Vector2[] uv1 = inputMesh.uv2;
				Vector2[] uv3 = inputMesh.uv3;
				Vector2[] uv4 = inputMesh.uv4;
#if UNITY_2018_2_OR_NEWER
				Vector2[] uv5 = inputMesh.uv5;
				Vector2[] uv6 = inputMesh.uv6;
				Vector2[] uv7 = inputMesh.uv7;
				Vector2[] uv8 = inputMesh.uv8;
#endif
				Color32[] colours = inputMesh.colors32;
				Vector4[] tangents = inputMesh.tangents;

				this.hasUvs = uvs != null && uvs.Length > 0;
				this.hasUv1s = uv1 != null && uv1.Length > 0;
				this.hasUv3s = uv3 != null && uv3.Length > 0;
				this.hasUv4s = uv4 != null && uv4.Length > 0;
#if UNITY_2018_2_OR_NEWER
				this.hasUv5s = uv5 != null && uv5.Length > 0;
				this.hasUv6s = uv6 != null && uv6.Length > 0;
				this.hasUv7s = uv7 != null && uv7.Length > 0;
				this.hasUv8s = uv8 != null && uv8.Length > 0;
#endif
				this.hasColours = colours != null && colours.Length > 0;
				this.hasTangents = tangents != null && tangents.Length > 0;

				for (int i = 0; i < inputMesh.subMeshCount; i++)
				{
					int[] indices = inputMesh.GetIndices(i);

					CuttableSubMesh subMesh = new CuttableSubMesh(indices, vertices, normals, colours, uvs, uv1, uv3, uv4,
#if UNITY_2018_2_OR_NEWER
					uv5, uv6, uv7, uv8,
#endif
					tangents);
					this.subMeshes.Add(subMesh);
				}
			}
			else
			{
				Debug.LogError("CuttableMesh's input mesh is not readable: " + debugName, inputMesh);
			}
		}

		public CuttableMesh(CuttableMesh inputMesh, List<CuttableSubMesh> newSubMeshes)
		{
			this.inputMeshRenderer = inputMesh.inputMeshRenderer;

			this.hasUvs = inputMesh.hasUvs;
			this.hasUv1s = inputMesh.hasUv1s;
			this.hasUv3s = inputMesh.hasUv3s;
			this.hasUv4s = inputMesh.hasUv4s;
#if UNITY_2018_2_OR_NEWER
			this.hasUv5s = inputMesh.hasUv5s;
			this.hasUv6s = inputMesh.hasUv6s;
			this.hasUv7s = inputMesh.hasUv7s;
			this.hasUv8s = inputMesh.hasUv8s;
#endif
			this.hasColours = inputMesh.hasColours;
			this.hasTangents = inputMesh.hasTangents;

			this.subMeshes = new List<CuttableSubMesh>();
			this.subMeshes.AddRange(newSubMeshes);
		}

		public void Add(CuttableMesh other)
		{
			if (this.subMeshes.Count != other.subMeshes.Count)
				throw new System.Exception("Mismatched submesh count");

			for (int i = 0; i < subMeshes.Count; i++)
			{
				subMeshes[i].Add(other.subMeshes[i]);
			}
		}

		public int NumSubMeshes()
		{
			return subMeshes.Count;
		}

		public bool HasUvs()
		{
			return hasUvs;
		}

		public bool HasColours()
		{
			return hasColours;
		}

		public List<CuttableSubMesh> GetSubMeshes()
		{
			return subMeshes;
		}

		public CuttableSubMesh GetSubMesh(int index)
		{
			return subMeshes[index];
		}

		public Transform GetTransform()
		{
			if (inputMeshRenderer != null)
				return inputMeshRenderer.transform;
			else
				return null;
		}

		public MeshRenderer ConvertToRenderer(string newObjectName)
		{
			Mesh newMesh = CreateMesh();
			if (newMesh.vertexCount == 0)
				return null;

			GameObject newObj = new GameObject(newObjectName);
			newObj.transform.SetParent(inputMeshRenderer.transform);
			newObj.transform.localPosition = Vector3.zero;
			newObj.transform.localRotation = Quaternion.identity;
			newObj.transform.localScale = Vector3.one;

			MeshFilter newFilter = newObj.AddComponent<MeshFilter>();
			newFilter.mesh = newMesh;

			MeshRenderer newRenderer = newObj.AddComponent<MeshRenderer>();
			// Copy properties
			newRenderer.shadowCastingMode = inputMeshRenderer.shadowCastingMode;
			newRenderer.reflectionProbeUsage = inputMeshRenderer.reflectionProbeUsage;
			//	newRenderer.renderingLayerMask = inputMeshRenderer.renderingLayerMask;
			newRenderer.lightProbeUsage = inputMeshRenderer.lightProbeUsage;
			// Copy materials
			newRenderer.sharedMaterials = inputMeshRenderer.sharedMaterials;

			return newRenderer;
		}

		public Mesh CreateMesh()
		{
			Mesh newMesh = new Mesh();

			int totalIndices = 0;
			for (int i = 0; i < subMeshes.Count; i++)
				totalIndices += subMeshes[i].NumIndices();
#if UNITY_2017_1_OR_NEWER
			newMesh.indexFormat = totalIndices > System.UInt16.MaxValue ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
#endif

			List<Vector3> outputVertices = new List<Vector3>();
			List<Vector3> outputNormals = new List<Vector3>();
			List<Color32> outputColours = hasColours ? new List<Color32>() : null;
			List<Vector2> outputUvs = hasUvs ? new List<Vector2>() : null;
			List<Vector2> outputUv1s = hasUv1s ? new List<Vector2>() : null;
			List<Vector2> outputUv3s = hasUv3s ? new List<Vector2>() : null;
			List<Vector2> outputUv4s = hasUv4s ? new List<Vector2>() : null;
#if UNITY_2018_2_OR_NEWER
			List<Vector2> outputUv5s = hasUv5s ? new List<Vector2>() : null;
			List<Vector2> outputUv6s = hasUv6s ? new List<Vector2>() : null;
			List<Vector2> outputUv7s = hasUv7s ? new List<Vector2>() : null;
			List<Vector2> outputUv8s = hasUv8s ? new List<Vector2>() : null;
#endif
			List<Vector4> outputTangents = hasTangents ? new List<Vector4>() : null;

			List<int> baseSubMeshVertex = new List<int>();

			foreach (CuttableSubMesh sub in subMeshes)
			{
				baseSubMeshVertex.Add(outputVertices.Count);

								sub.AddTo(outputVertices, outputNormals, outputColours, outputUvs, outputUv1s, outputUv3s, outputUv4s,
#if UNITY_2018_2_OR_NEWER
					outputUv5s, outputUv6s, outputUv7s, outputUv8s,
#endif
					outputTangents);
			}

			newMesh.vertices = outputVertices.ToArray();
			newMesh.normals = outputNormals.ToArray();
			newMesh.colors32 = hasColours ? outputColours.ToArray() : null;
			newMesh.uv = hasUvs ? outputUvs.ToArray() : null;
			newMesh.uv2 = hasUv1s ? outputUv1s.ToArray() : null;
			newMesh.uv3 = hasUv3s ? outputUv3s.ToArray() : null;
			newMesh.uv4 = hasUv4s ? outputUv4s.ToArray() : null;
#if UNITY_2018_2_OR_NEWER
			newMesh.uv5 = hasUv5s ? outputUv5s.ToArray() : null;
			newMesh.uv6 = hasUv6s ? outputUv6s.ToArray() : null;
			newMesh.uv7 = hasUv7s ? outputUv7s.ToArray() : null;
			newMesh.uv8 = hasUv8s ? outputUv8s.ToArray() : null;
#endif
			newMesh.tangents = hasTangents ? outputTangents.ToArray() : null;

			newMesh.subMeshCount = subMeshes.Count;

			for (int i = 0; i < subMeshes.Count; i++)
			{
				CuttableSubMesh sub = subMeshes[i];
				int baseVertex = baseSubMeshVertex[i];

				int[] indices = sub.GenIndices();
#if UNITY_2017_1_OR_NEWER
				newMesh.SetTriangles(indices, i, true, baseVertex);
#else
				for (int j = 0; j < indices.Length; j++)
					indices[j] += baseVertex;
				newMesh.SetTriangles(indices, i, true);
#endif
			}

			return newMesh;
		}
	}

} // namespace Technie.PhysicsCreator

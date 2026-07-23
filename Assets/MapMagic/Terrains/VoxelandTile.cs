using UnityEngine;
using Den.Tools;
using MapMagic.Core;

namespace MapMagic.Terrains
{
	public class VoxelandTile : MonoBehaviour, ITile
	{
		public MapMagicObject mapMagic;
		public Coord coord = new Coord(int.MaxValue, int.MaxValue);
		public float distance = -1;

		public bool IsNull { get { return this == (UnityEngine.Object)null || this.Equals(null) || gameObject == null || gameObject.Equals(null); } }

		public static VoxelandTile Construct (MapMagicObject mapMagic)
		{
			GameObject go = new GameObject();
			go.transform.parent = mapMagic.transform;
			VoxelandTile tile = go.AddComponent<VoxelandTile>();
			tile.mapMagic = mapMagic;
			return tile;
		}

		public void Move (Coord newCoord, float newRemoteness)
		{
			coord = newCoord;
			distance = newRemoteness;

			Vector3 size = (Vector3)mapMagic.tileSize;
			Vector3 position = new Vector3(coord.x*size.x, 0, coord.z*size.z);

			transform.localPosition = position;
			gameObject.name = "Voxeland Tile " + coord.x + "," + coord.z;
		}

		public void Dist (float newRemoteness)
		{
			distance = newRemoteness;
		}

		public void Remove ()
		{
			#if UNITY_EDITOR
			if (!MapMagicObject.isPlaying)
				GameObject.DestroyImmediate(gameObject);
			else
			#endif
				GameObject.Destroy(gameObject);
		}
	}
}

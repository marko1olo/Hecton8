using UnityEngine;
using System;
using System.Threading;
using System.Collections;
using System.Collections.Generic;

using Den.Tools;

using MapMagic.Nodes;
using MapMagic.Terrains;

namespace MapMagic.Core
{
	[Serializable]
	public class TerrainTileManager : TileManager<TerrainTile>, ISerializationCallbackReceiver
	{
		[SerializeField] public TerrainTile[] customTiles = new TerrainTile[0];
		public Dictionary<Coord,TerrainTile> pinned = new Dictionary<Coord,TerrainTile>();
		[NonSerialized] private int customTilesCount = -1;

	
		protected override TerrainTile ConstructTile (MonoBehaviour holder)
		{
			return TerrainTile.Construct((MapMagicObject)holder);
		}

		public void Pin (Coord coord, bool asDraft, MonoBehaviour holder=null)
		/// Creates new tile at the coord if it's empty and pin it
		{
			TerrainTile tile;
			bool created = false;
			lock (gridLocker)
			{
				if (!grid.TryGetValue(coord, out tile))
					tile = null;
			}

			if (tile == null)
			{
				TerrainTile constructedTile = ConstructTile(holder);

				lock (gridLocker)
				{
					if (!grid.TryGetValue(coord, out tile))
					{
						Dictionary<Coord,TerrainTile> newGrid = new Dictionary<Coord,TerrainTile>(grid);
						newGrid.Add(coord, constructedTile);
						grid = newGrid;
						tile = constructedTile;
						created = true;
					}
				}

				if (!created && constructedTile != null)
					constructedTile.Remove();
			}

			if (tile == null)
				return;

			tile.Pin(asDraft);
			tile.Move(coord, camCoords != null ? GetRemoteness(coord, camCoords, camCoordsCount) : 0);

			if (!pinned.ContainsKey(coord))
				pinned.Add(coord, tile);
		}


		public void Unpin (Coord coord)
		/// Clears pin flag for tile at the coord and re-deploys grid to remove it if needed
		{
			if (!pinned.ContainsKey(coord)) return;

			pinned.Remove(coord);

			//re-deploying to find out if this tile should be removed or left as unpinned
			//if (camCoords != null)
			//	Deploy(camCoords, pinned, holder:null); //deploying without holder since it shouldn't create new tiles anyways

			//no deploy was performed - removing pinned
			//else
			{
				TerrainTile tile;
				lock (gridLocker)
				{
					if (!grid.TryGetValue(coord, out tile))
						return;

					Dictionary<Coord,TerrainTile> newGrid = new Dictionary<Coord,TerrainTile>(grid);
					newGrid.Remove(coord);
					grid = newGrid;
				}

				if (tile != null)
					tile.Remove();
			}
		}


		public void Deploy (Coord[] camCoords, MonoBehaviour holder=null)
			{ Deploy(camCoords, pinned, holder); }

		public IEnumerable<TerrainTile> All () 
		{ 
			foreach (TerrainTile tile in base.Tiles()) 
				yield return tile;

			int count = customTilesCount >= 0 && customTilesCount <= customTiles.Length ? customTilesCount : customTiles.Length;
			for (int i=0; i<count; i++)
			{
				TerrainTile tile = customTiles[i];
				if (tile != null && !tile.IsNull)
					yield return tile;
			}
		}

		/*public TerrainTile PreviewTile 
		{
			get
			{
				foreach (TerrainTile tile in All()) 
					if (tile.preview) return tile;
				return null;
			}
			set
			{
				foreach (TerrainTile tile in All())
				{
					if (tile == value) tile.preview = true;
					else tile.preview = false;
				}
			}
		}*/

		public IEnumerable<Rect> AllWorldRects ()
		/// Map-Magic relative rects actually (in MM coordsys)
		{
			foreach (TerrainTile tile in All()) 
				yield return tile.WorldRect;
		}

		public IEnumerable<Terrain> AllActiveTerrains ()
		{
			foreach (TerrainTile tile in All()) 
				yield return tile.ActiveTerrain;
		}

		public void PinCustom (TerrainTile tile)
		{
			if (!customTiles.Contains(tile))
			{
				ArrayTools.Add(ref customTiles, tile);
				customTilesCount = -1;
			}
		}

		public void UnpinCustom (TerrainTile tile)
		{
			if (customTiles.Contains(tile))
			{
				ArrayTools.Remove(ref customTiles, tile);
				customTilesCount = -1;
			}
		}


		public override void RemoveNulls () 
		{
			base.RemoveNulls();

			int writeIndex = 0;
			for (int i=0; i<customTiles.Length; i++)
			{
				TerrainTile tile = customTiles[i];
				if (tile == null || tile.IsNull)
					continue;

				if (writeIndex != i)
					customTiles[writeIndex] = tile;

				writeIndex++;
			}

			for (int i=writeIndex; i<customTiles.Length; i++)
				customTiles[i] = null;

			customTilesCount = writeIndex;
		}

		public override TerrainTile Closest () 
		/// Using cached distances instead of re-calculating hem
		/// If mainOnly enabled then checking only tiles containing main data
		{
			float minDist = int.MaxValue;
			TerrainTile minTile = default;

			foreach (var kvp in grid)
			{
				TerrainTile tile = kvp.Value;
				if (tile.distance < minDist) { minDist=tile.distance; minTile=kvp.Value; }
			}

			return minTile;
		}

		public TerrainTile ClosestMain () 
		/// Same as above, but iterates only in tiles with main data
		{
			float minDist = int.MaxValue;
			TerrainTile minTile = default;

			foreach (var kvp in grid)
			{
				TerrainTile tile = kvp.Value;
				if (tile.main==null) continue;
				if (tile.distance < minDist) { minDist=tile.distance; minTile=kvp.Value; }
			}

			return minTile;
		}

		public TerrainTile FindByWorldPosition (float x, float z)
		{
			foreach (TerrainTile tile in All())
			{
				if (tile.ContainsWorldPosition(x,z))
					return tile;
			}

			return null;
		}

		public TerrainTile FindByTerrain (Terrain terrain)
		{
			foreach (TerrainTile tile in All())
			{
				if (tile.main?.terrain == terrain)
					return tile;
				if (tile.draft?.terrain == terrain)
					return tile;
			}

			return null;
		}


		#region Serialization

		public Coord[] serializedPinnedCoords = new Coord[0];

			public override void OnBeforeSerialize () 
			{
				base.OnBeforeSerialize();

				if (serializedPinnedCoords.Length != pinned.Count)
					serializedPinnedCoords = new Coord[pinned.Count];

				int i=0;
				foreach (var kvp in pinned)
				{
					serializedPinnedCoords[i] = kvp.Key;
					i++;
				}
			}

			public override void OnAfterDeserialize () 
			{
				base.OnAfterDeserialize();

				for (int i=0; i<serializedPinnedCoords.Length; i++)
				{
					Coord coord = serializedPinnedCoords[i];
					if (grid.TryGetValue(coord, out TerrainTile tile))
					{
						if (!pinned.ContainsKey(coord))
							pinned.Add(coord, tile);
					}
				}
			}

		#endregion
	}
}

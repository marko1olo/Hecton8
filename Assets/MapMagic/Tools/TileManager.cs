using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace Den.Tools
{

	public interface ITile
	{		
		//Coord Coord { set; }
		//bool Pinned { set; }
		//int Distance { get; set; } //from deploy rects centers, in chunks
		bool IsNull { get; } //if main object was removed externally. Checking on ad, remove and deploy
		
		//static ITile Construct (object holder);
		void Move (Coord coord, float dist);
		void Dist (float dist);
		void Remove ();
	}

	public class TileManager<T> : ISerializationCallbackReceiver where T: ITile//, IEquatable<T>
	{
		public Dictionary<Coord,T> grid = new Dictionary<Coord,T>();
		public object gridLocker = new object();

		public bool allowMove = false;

		public bool generateLimited = true;
		public Vector2D generateCenter;

		public bool generateInfinite = true;
		public int generateRange = 2;
		public int retainMargin = 1;

		public bool genAroundMainCam = true;
		public bool genAroundObjsTag = false;
		public string genAroundTag = null;
		public bool genAroundTfms = false;
		public Transform[] genAroundTfmsList = new Transform[0];
		public bool genAroundCoordinates = false;
		public Coord[] genCoordinates = new Coord[0];


		[System.NonSerialized] protected Coord[] camCoords = null;
		[System.NonSerialized] protected int camCoordsCount = 0;
		[System.NonSerialized] private static readonly Coord[] emptyCamCoords = Array.Empty<Coord>();
		[System.NonSerialized] private readonly Coord[] editorSingleCamCoords = new Coord[1];
		[System.NonSerialized] private Camera cachedMainCamera = null;
		[System.NonSerialized] private Transform cachedMainCameraTransform = null;
		[System.NonSerialized] private Transform[] cachedTaggedTransforms = Array.Empty<Transform>();
		[System.NonSerialized] private int cachedTaggedTransformCount = 0;
		[System.NonSerialized] private bool camCoordsStorageDirty = false;
		// COLD ALLOC: Dictionary<Coord,T>[256] - reusable source snapshot for tile-ring deployment - owner: TileManager
		[System.NonSerialized] private readonly Dictionary<Coord,T> deploySrcGrid = new Dictionary<Coord,T>(256);
		// COLD ALLOC: List<T>[256] - reusable removed-tile pool for tile-ring movement - owner: TileManager
		[System.NonSerialized] private readonly List<T> deployPool = new List<T>(256);
		// COLD ALLOC: List<(T,Coord,float)>[256] - reusable move ordering buffer for tile-ring deployment - owner: TileManager
		[System.NonSerialized] private readonly List<(T tile, Coord coord, float dist)> deployMoved = new List<(T,Coord,float)>(256);
		[System.NonSerialized] private CoordRect[] deployRectsScratch = Array.Empty<CoordRect>();
		[System.NonSerialized] private readonly List<Coord> removedCoordsScratch = new List<Coord>(256);
		private static readonly Comparison<(T tile, Coord coord, float dist)> CompareMovedTilesByDistance = CompareMovedTiles;
		//[System.NonSerialized] protected CoordRect[] deployRects;  //used to find chunks difference and for Unpin


		public T this[Coord coord] 
		{get{ 
			if (grid.TryGetValue(coord, out T t)) return t; 
			else return default(T); 
		}}

		public T this[int x, int z] {get{ return this[ new Coord(x,z) ]; }}


		public bool Contains (Coord coord)
		/// Checks if tile is contained in hash dictionary. 
		{
			return grid.ContainsKey(coord);
		}


		protected virtual T ConstructTile (MonoBehaviour holder)
		{
			return (T)typeof(T).GetMethod("Construct", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public).Invoke(null,new object[]{holder});
		}


		public IEnumerable<T> Tiles ()
		{
			foreach (KeyValuePair<Coord,T> kvp in grid)
				yield return kvp.Value;
		}


		public virtual T Closest ()
		{
			float minDist = int.MaxValue;
			T minTile = default;

			Dictionary<Coord,T>.Enumerator closestEnumerator = grid.GetEnumerator();
			while (closestEnumerator.MoveNext())
			{
				KeyValuePair<Coord,T> kvp = closestEnumerator.Current;
				if (camCoords == null) return kvp.Value;

				Coord coord = kvp.Key;
				float dist = GetRemoteness(coord, camCoords, camCoordsCount);
				if (dist<minDist) { minDist=dist; minTile=kvp.Value; }
			}

			return minTile;
		}




		#region Per-frame/Update

			public void Update (Vector3 tileSize, Dictionary<Coord,T> pinned=null, MonoBehaviour holder=null, bool distsOnly=false)
			/// Holder is an object who's coordsys is used. Not necessary parent
			/// DistsOnly will just refresh coords without deploy. In MM case distsOnly = !playmode
			{
				Profiler.BeginSample("Remove Nulls");
				RemoveNulls(); //excluding removed objects
				Profiler.EndSample();
				
				Profiler.BeginSample("RefreshCamCoords");
				bool camCoordsChanged = RefreshCamCoords(tileSize.x, holder);
				if (!camCoordsChanged || camCoordsCount==0) { Profiler.EndSample(); return; }
				Profiler.EndSample();

				Profiler.BeginSample("Deploy");
				if (!distsOnly && generateInfinite) Deploy(camCoords, pinned:pinned, holder:holder);

				Profiler.EndSample();

				Profiler.BeginSample("ChangeDists");
				ChangeDists(camCoords);
				Profiler.EndSample();
			}


			public void ReDeploy (Vector3 tileSize, Dictionary<Coord,T> pinned=null, MonoBehaviour holder=null)
			{
				RemoveNulls(); //excluding removed objects
				PrepareCamCoordsStorage();
				RefreshCamCoords(tileSize.x);
				if (camCoordsCount == 0) return;
				Deploy(camCoords, pinned:pinned, holder:holder);
				ChangeDists(camCoords);
			}


			private bool RefreshCamCoords (float tileSize, MonoBehaviour holder=null)
			/// Gets a list of camera (or tagged objects) positions. Uses a cached camPoses array. Returns true if camera positions changed.
			{
				bool coordsChanged = false;

				#if UNITY_EDITOR
				if (!UnityEditor.EditorApplication.isPlaying) 
				{
					if (UnityEditor.SceneView.lastActiveSceneView?.camera==null || UnityEditor.SceneView.lastActiveSceneView.camera==null) //this happens right after script compile 
					{
						camCoords = emptyCamCoords;
						camCoordsCount = 0;
					}

					else
					{
						Vector3 sceneCamPos = UnityEditor.SceneView.lastActiveSceneView.camera.transform.position;
						Coord sceneCamCoord = Coord.Floor(sceneCamPos.x/tileSize, sceneCamPos.z/tileSize);

						if (!ReferenceEquals(camCoords, editorSingleCamCoords)) { camCoords = editorSingleCamCoords; coordsChanged = true; }
						if (camCoordsCount != 1) { camCoordsCount = 1; coordsChanged = true; }
						if (camCoords[0] != sceneCamCoord) { camCoords[0] = sceneCamCoord; coordsChanged = true; }
					}
				}

				else
				#endif
				{
					coordsChanged = camCoordsStorageDirty;
					camCoordsStorageDirty = false;

					//finding objects with tag
					Transform[] taggedObjects = genAroundObjsTag ? cachedTaggedTransforms : null;
					int taggedObjectsCount = genAroundObjsTag ? cachedTaggedTransformCount : 0;

					Transform mainCamTransform = GetCachedMainCameraTransform();

					//calculating cams array length
					int camsLength = CountRuntimeCamCoords(mainCamTransform);

					if (camCoords == null || camsLength > camCoords.Length)
					{
						camCoords = emptyCamCoords;
						camCoordsCount = 0;
						camCoordsStorageDirty = camsLength != 0;
						coordsChanged = true;
						if (camsLength != 0)
							return true;
					}

					if (camCoordsCount != camsLength)
					{
						camCoordsCount = camsLength;
						coordsChanged = true;
					}
				
					if (camsLength == 0) 
						return coordsChanged;

					//filling cams array
					int counter = 0;
					if (genAroundMainCam && mainCamTransform != null)
					{
						Vector3 camPos = mainCamTransform.position;
						if (holder != null) camPos = holder.transform.InverseTransformPoint(camPos);
						
						Coord camCoord = Coord.Floor(camPos.x/tileSize, camPos.z/tileSize);
						if (camCoords[counter] != camCoord) { camCoords[counter] = camCoord; coordsChanged = true; }
						counter++;
					}

					if (taggedObjects != null)
						for (int i=0; i<taggedObjectsCount; i++)
						{
							Transform taggedTransform = taggedObjects[i];
							if (taggedTransform == null) continue;

							Vector3 objPos = taggedTransform.position;
							if (holder != null) objPos = holder.transform.InverseTransformPoint(objPos);

							Coord objCoord = Coord.Floor(objPos.x/tileSize, objPos.z/tileSize);
							if (camCoords[counter] != objCoord) { camCoords[counter] = objCoord; coordsChanged = true; }
							counter++;
						}

					if (genAroundTfms && genAroundTfmsList != null)
						for (int i=0; i<genAroundTfmsList.Length; i++)
						{
							Transform tfm = genAroundTfmsList[i];
							if (tfm == null) continue;

							Vector3 objPos = tfm.position;
							if (holder != null) objPos = holder.transform.InverseTransformPoint(objPos);

							Coord objCoord = Coord.Floor(objPos.x/tileSize, objPos.z/tileSize);
							if (camCoords[counter] != objCoord)
								{ camCoords[counter] = objCoord; coordsChanged = true; }
							counter++;
						}

					if (genAroundCoordinates && genCoordinates != null)
						for (int i=0; i<genCoordinates.Length; i++) 
						{
							Coord objCoord = genCoordinates[i];
							if (camCoords[counter] != objCoord)
								{ camCoords[counter] = objCoord; coordsChanged = true; }
							counter++;
						}

					if (counter != camCoordsCount)
					{
						camCoordsCount = counter;
						coordsChanged = true;
					}
				}

				return coordsChanged;
			}


			public void SetMainCamera (Camera camera, bool prepareStorage=true)
			{
				cachedMainCamera = camera;
				cachedMainCameraTransform = camera == null ? null : camera.transform;
				if (prepareStorage)
					PrepareCamCoordsStorage();
			}


			public void SetTaggedObjects (GameObject[] taggedObjects)
			{
				if (taggedObjects == null || taggedObjects.Length == 0)
				{
					ClearCachedTaggedTransforms();
					PrepareCamCoordsStorage();
					return;
				}

				if (cachedTaggedTransforms == null || cachedTaggedTransforms.Length < taggedObjects.Length)
					cachedTaggedTransforms = new Transform[taggedObjects.Length];

				cachedTaggedTransformCount = 0;
				for (int i=0; i<taggedObjects.Length; i++)
				{
					GameObject taggedObject = taggedObjects[i];
					if (taggedObject == null) continue;

					cachedTaggedTransforms[cachedTaggedTransformCount] = taggedObject.transform;
					cachedTaggedTransformCount++;
				}

				for (int i=cachedTaggedTransformCount; i<cachedTaggedTransforms.Length; i++)
					cachedTaggedTransforms[i] = null;

				PrepareCamCoordsStorage();
			}


			private void PrepareCamCoordsStorage ()
			{
				#if UNITY_EDITOR
				if (!UnityEditor.EditorApplication.isPlaying)
					return;
				#endif

				Transform mainCamTransform = GetCachedMainCameraTransform();
				int camsLength = CountRuntimeCamCoords(mainCamTransform);
				int camsCapacity = CountRuntimeCamCoordCapacity();
				if (camCoords == null || camCoords.Length < camsCapacity || (camsCapacity == 0 && !ReferenceEquals(camCoords, emptyCamCoords)))
				{
					camCoords = camsCapacity == 0 ? emptyCamCoords : new Coord[camsCapacity];
					camCoordsStorageDirty = true;
				}
				EnsureDeployRectCapacity(camsCapacity);

				if (camCoordsCount != camsLength)
				{
					camCoordsCount = camsLength;
					camCoordsStorageDirty = true;
				}
			}


			private Transform GetCachedMainCameraTransform ()
			{
				if (cachedMainCamera == null)
				{
					cachedMainCameraTransform = null;
					return null;
				}

				if (!cachedMainCamera.isActiveAndEnabled)
					return null;

				return cachedMainCameraTransform;
			}


			private static int CountNonNullTransforms (Transform[] transforms)
			{
				if (transforms == null) return 0;

				int count = 0;
				for (int i=0; i<transforms.Length; i++)
					if (transforms[i] != null) count++;

				return count;
			}


			private int CountCachedTaggedTransforms ()
			{
				if (cachedTaggedTransforms == null) return 0;

				int count = 0;
				for (int i=0; i<cachedTaggedTransformCount; i++)
					if (cachedTaggedTransforms[i] != null) count++;

				return count;
			}


			private void ClearCachedTaggedTransforms ()
			{
				if (cachedTaggedTransforms != null)
				{
					for (int i=0; i<cachedTaggedTransforms.Length; i++)
						cachedTaggedTransforms[i] = null;
				}

				cachedTaggedTransformCount = 0;
			}


			private int CountRuntimeCamCoords (Transform mainCamTransform)
			{
				int camsLength = 0;
				if (genAroundMainCam && mainCamTransform != null) camsLength++;
				if (genAroundObjsTag) camsLength += CountCachedTaggedTransforms();
				camsLength += CountNonNullTransforms(genAroundTfms ? genAroundTfmsList : null);
				if (genAroundCoordinates && genCoordinates != null) camsLength += genCoordinates.Length;
				return camsLength;
			}


			private int CountRuntimeCamCoordCapacity ()
			{
				int camsLength = 0;
				if (genAroundMainCam) camsLength++;
				if (genAroundObjsTag && cachedTaggedTransforms != null) camsLength += cachedTaggedTransformCount;
				camsLength += CountNonNullTransforms(genAroundTfms ? genAroundTfmsList : null);
				if (genAroundCoordinates && genCoordinates != null) camsLength += genCoordinates.Length;
				return camsLength;
			}

		#endregion


		#region Deploy

			public virtual void ChangeDists (Coord[] camCoords)
			/// Fast deploy that changes distances only
			{
				int activeCamCoordCount = ActiveCamCoordCount(camCoords);
				Dictionary<Coord,T>.Enumerator enumerator = grid.GetEnumerator();
				while (enumerator.MoveNext())
				{
					KeyValuePair<Coord,T> kvp = enumerator.Current;
					Coord coord = kvp.Key;
					T tile = kvp.Value;

					tile.Dist( GetRemoteness(coord, camCoords, activeCamCoordCount) );
				}
			}	


			public virtual void Deploy (Coord[] camCoords, Dictionary<Coord,T> pinned=null, MonoBehaviour holder=null)
			/// Creates all tiles within createRect, removes tiles outside removeRect. Tries to move tiles instead of creating new (if allowed). 
			/// Note that all rects contain chunks, not world units
			/// Holder is a parent object that called refresh, to parent created tiles
			{
				int activeCamCoordCount = ActiveCamCoordCount(camCoords);
				if (activeCamCoordCount == 0) return;

				EnsureDeployRectCapacity(activeCamCoordCount);
				FillDeployRects(camCoords, activeCamCoordCount, generateRange);

				int rectSide = generateRange*2 + 1;
				Dictionary<Coord,T> currentGrid;
				int expectedGridCapacity;

				Dictionary<Coord,T> srcGrid = deploySrcGrid;
				srcGrid.Clear();

				lock (gridLocker)
				{
					currentGrid = grid;
					expectedGridCapacity = Math.Max(currentGrid.Count, activeCamCoordCount*rectSide*rectSide + (pinned != null ? pinned.Count : 0));

					Dictionary<Coord,T>.Enumerator gridEnumerator = currentGrid.GetEnumerator();
					while (gridEnumerator.MoveNext())
					{
						KeyValuePair<Coord,T> kvp = gridEnumerator.Current;
						srcGrid.Add(kvp.Key, kvp.Value);
					}
				}

				// A replacement grid is intentionally allocated per deploy. The previous public grid may
				// still be observed by generation/progress readers and must not be recycled as scratch.
				Dictionary<Coord,T> dstGrid = new Dictionary<Coord,T>(expectedGridCapacity);

				//transferring pinned tiles to new grid
				Profiler.BeginSample("Transf Pin To New");
				if (pinned != null)
				{
					Dictionary<Coord,T>.Enumerator pinnedEnumerator = pinned.GetEnumerator();
					while (pinnedEnumerator.MoveNext())
					{
						KeyValuePair<Coord,T> kvp = pinnedEnumerator.Current;
						Coord coord = kvp.Key;
						T tile = kvp.Value;
					
						srcGrid.Remove(coord);
						dstGrid.Add(coord, tile);

						tile.Dist(GetRemoteness(coord, camCoords, activeCamCoordCount)); //calculating dist to every tile added to dstGrid
					}
				}
				Profiler.EndSample();


				//adding objects within create range + margin (on their respective coordinates)
				Profiler.BeginSample("Adding Objs");
				for (int r=0; r<activeCamCoordCount; r++)
				{
					CoordRect rect = deployRectsScratch[r];
					//rect.Expand(retainMargin);
					Coord min = rect.Min-retainMargin; Coord max = rect.Max+retainMargin;

					for (int x=min.x; x<max.x; x++)
						for (int z=min.z; z<max.z; z++)
						{
							Coord coord = new Coord(x,z);

							if (srcGrid.TryGetValue(coord, out T tile))
							{
								srcGrid.Remove(coord);
								dstGrid.Add(coord,tile);

								tile.Dist(GetRemoteness(coord, camCoords, activeCamCoordCount));
							}
						}
				}
				Profiler.EndSample();

				//filling create rects empty areas with unused (or new) objects and moving them
				Profiler.BeginSample("Fillin Empty");
				deployPool.Clear();
				Dictionary<Coord,T>.Enumerator srcEnumerator = srcGrid.GetEnumerator();
				while (srcEnumerator.MoveNext())
					deployPool.Add(srcEnumerator.Current.Value);

				int poolIndex = 0;
				deployMoved.Clear();
				for (int r=0; r<activeCamCoordCount; r++)
				{
					CoordRect rect = deployRectsScratch[r];
					Coord min = rect.Min; Coord max = rect.Max;

					for (int x=min.x; x<max.x; x++)
						for (int z=min.z; z<max.z; z++)
					{
						Coord newCoord = new Coord(x,z);

						if (dstGrid.ContainsKey(newCoord)) continue;

						T tile;

						//moving
						if (poolIndex < deployPool.Count  &&  allowMove)
						{
							//Coord oldCoord = srcGrid.AnyKey();
							//T tile = srcGrid[oldCoord];
							tile = deployPool[poolIndex];
							poolIndex++;
						}

						//creating
						else 
						{
							//Debug.Log("No tiles left. Creating. Coord:" + newCoord);
							Profiler.BeginSample("Construct Tile");
							tile = ConstructTile(holder);
							Profiler.EndSample();
						}	

						dstGrid.Add(newCoord, tile);

						//tile.Move(newCoord, GetRemoteness(newCoord, camCoords)); //moving after according to their distance
						deployMoved.Add( (tile, newCoord, GetRemoteness(newCoord, camCoords, activeCamCoordCount)) );
					}

					//HashSet<T> curChangedTiles = RelocateTiles(dstGrid, rect, pool, holder);
					//changedTiles.UnionWith(curChangedTiles);
				}
				Profiler.EndSample();

				//calling remove fn on all other objs left (no need to remove from srcDict - just not including them in dst)
				Profiler.BeginSample("Callin Remove");
				for (int p=poolIndex; p<deployPool.Count; p++)
				{
					T tile = deployPool[p];
					tile.Remove();
				}
				Profiler.EndSample();

				//calling Move function in order depending on remoteness
				Profiler.BeginSample("Callin Move");
				deployMoved.Sort(CompareMovedTilesByDistance);

				//assigning new grid and deployed rects
				//this should be done before calling Move (moves calls MM welding, and welding reads grid)
				lock (gridLocker)
					grid = dstGrid;

				int movedCount = deployMoved.Count;
				for (int m=0; m<movedCount; m++)
					deployMoved[m].tile.Move(deployMoved[m].coord, deployMoved[m].dist);

				srcGrid.Clear();
				deployPool.Clear();
				deployMoved.Clear();
				Profiler.EndSample();
			}

		#endregion

		#region Helpers

			private int ActiveCamCoordCount (Coord[] coords)
			{
				if (coords == null) return 0;
				if (ReferenceEquals(coords, camCoords)) return camCoordsCount;
				return coords.Length;
			}


			private void EnsureDeployRectCapacity (int requiredCapacity)
			{
				if (deployRectsScratch.Length >= requiredCapacity)
					return;

				Array.Resize(ref deployRectsScratch, requiredCapacity);
			}


			private void FillDeployRects (Coord[] camCoords, int camCoordsCount, int range)
			/// Converts each cam coord to chunk rect using the generate range
			{
				for (int r=0; r<camCoordsCount; r++)
					deployRectsScratch[r] = new CoordRect(camCoords[r].x - range, camCoords[r].z - range, range*2 +1, range*2 +1);
			}


			private static int CompareMovedTiles ((T tile, Coord coord, float dist) x, (T tile, Coord coord, float dist) y)
			{
				float delta = x.dist-y.dist;
				if (delta > 0.00001f) return 1;
				if (delta < -0.000001f) return -1;
				return 0;
			}


			protected static float GetRemoteness (Coord coord, Coord[] camCoords, int camCoordsCount=-1)
			/// Returns an axis/priority distance to the closest cam
			{
				if (camCoords == null || camCoords.Length == 0 || camCoordsCount == 0) return 0; // HECTON headless-gen fix: priority underflow when no camera

				float minDist = float.MaxValue;

				if (camCoordsCount < 0 || camCoordsCount > camCoords.Length) camCoordsCount = camCoords.Length;

				for (int r=0; r<camCoordsCount; r++)
				{
					float dist = Coord.DistanceAxisPriority(camCoords[r], coord);
					if (dist < minDist) minDist = dist;
				}

				return minDist;
			}


			public virtual void RemoveNulls ()
			/// Removes tiles that were deleted externally from the collection
			{
				removedCoordsScratch.Clear();

				try
				{
					lock (gridLocker)
					{
						Dictionary<Coord,T> currentGrid = grid;
						Dictionary<Coord,T>.Enumerator enumerator = currentGrid.GetEnumerator();
						while (enumerator.MoveNext())
						{
							KeyValuePair<Coord,T> kvp = enumerator.Current;
							T tile = kvp.Value;

							if (tile == null || tile.IsNull)
								removedCoordsScratch.Add(kvp.Key);
						}

						if (removedCoordsScratch.Count == 0)
							return;
			
						Dictionary<Coord,T> dstGrid = new Dictionary<Coord,T>(currentGrid.Count);

						enumerator = currentGrid.GetEnumerator();
						while (enumerator.MoveNext())
						{
							KeyValuePair<Coord,T> kvp = enumerator.Current;
							T tile = kvp.Value;

							if (tile != null && !tile.IsNull)
								dstGrid.Add(kvp.Key, tile);
						}

						grid = dstGrid;
					}
				}
				finally
				{
					removedCoordsScratch.Clear();
				}
			}


			[Obsolete] private CoordRect WorldToChunksRect (CoordRect wrect, int size)
			/// Not used anywhere, but contains a tested code just in case
			{
				Coord cMin = new Coord(
					wrect.offset.x>=0 ? wrect.Min.x/size : (wrect.Min.x+1)/size-1,
					wrect.offset.z>=0 ? wrect.Min.z/size : (wrect.Min.z+1)/size-1 );
			
				Coord cMax = new Coord(
					wrect.offset.x+wrect.size.x>0 ? (wrect.offset.x+wrect.size.x-1)/size + 1 :  (wrect.offset.x+wrect.size.x)/size,
					wrect.offset.z+wrect.size.z>0 ? (wrect.offset.z+wrect.size.z-1)/size + 1 :  (wrect.offset.z+wrect.size.z)/size );
				//tested

				return new CoordRect(cMin, cMax-cMin);
			}




		#endregion


		#region Serialization 
		//generics do not serialize. Derive to use it.

			public T[] serializedTiles;
			public Coord[] serializedCoords;

			public virtual void OnBeforeSerialize ()
			{
				if (serializedTiles == null || serializedTiles.Length != grid.Count) serializedTiles = new T[grid.Count];
				if (serializedCoords == null || serializedCoords.Length != grid.Count) serializedCoords = new Coord[grid.Count];

				int counter = 0;
				foreach (var kvp in grid)
				{
					serializedTiles[counter] = kvp.Value;
					serializedCoords[counter] = kvp.Key;
					counter++;
				}
			}

			public virtual void OnAfterDeserialize ()
			{
				Dictionary<Coord,T> newTiles = new Dictionary<Coord,T>();
				int serializedCount = serializedTiles != null && serializedCoords != null ? Math.Min(serializedTiles.Length, serializedCoords.Length) : 0;
				for (int i=0; i<serializedCount; i++)
				{
					if (serializedTiles[i] != null)
						newTiles.Add(serializedCoords[i], serializedTiles[i]);
				}
				lock (gridLocker) { grid = newTiles; }
			}

		#endregion
	}


	public interface IPinTile : ITile { void Pin(); }

	public class TilePinManager<T> : TileManager<T> where T: IPinTile, IEquatable<T>
	{	
		private Dictionary<Coord,T> pinned = new Dictionary<Coord,T>();

	
		public void Pin (Coord coord, MonoBehaviour holder=null)
		/// Creates new tile at the coord if it's empty and pin it
		{
			T tile;
			bool created = false;
			lock (gridLocker)
			{
				if (!grid.TryGetValue(coord, out tile))
					tile = default;
			}

			if (tile == null)
			{
				T constructedTile = ConstructTile(holder);

				lock (gridLocker)
				{
					if (!grid.TryGetValue(coord, out tile))
					{
						Dictionary<Coord,T> newGrid = new Dictionary<Coord,T>(grid);
						newGrid.Add(coord, constructedTile);
						grid = newGrid;
						tile = constructedTile;
						created = true;
					}
				}

				if (!created && constructedTile != null)
					constructedTile.Remove();
			}

			if (created)
			{
				tile.Pin();
				tile.Move(coord, camCoords != null ? GetRemoteness(coord, camCoords, camCoordsCount) : 0);
			}

			else
				tile.Pin(); 

			if (!pinned.ContainsKey(coord))
				pinned.Add(coord, tile);
		}


		public void Unpin (Coord coord)
		/// Clears pin flag for tile at the coord and re-deploys grid to remove it if needed
		{
			if (!pinned.ContainsKey(coord)) return;

			pinned.Remove(coord);

			//re-deploying to find out if this tile should be removed or left as unpinned
			if (camCoords != null)
				Deploy(camCoords, pinned, holder:null); //deploying without holder since it shouldn't create new tiles anyways

			//no deploy was performed - removing pinned
			else
			{
				T tile;
				lock (gridLocker)
				{
					if (!grid.TryGetValue(coord, out tile))
						return;

					Dictionary<Coord,T> newGrid = new Dictionary<Coord,T>(grid);
					newGrid.Remove(coord);
					grid = newGrid;
				}

				tile.Remove();
			}
		}

		public void Deploy (Coord[] camCoords, MonoBehaviour holder=null)
			{ Deploy(camCoords, pinned, holder); }
	}
}

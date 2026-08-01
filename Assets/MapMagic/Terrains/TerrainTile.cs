using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Profiling;

using Den.Tools;
using Den.Tools.Tasks;
using MapMagic.Core;
using MapMagic.Products;
using MapMagic.Nodes;

namespace MapMagic.Terrains
{

	//[System.Serializable]
	public class TerrainTile : MonoBehaviour, ITile, ISerializationCallbackReceiver
	{
		public MapMagicObject mapMagic;  //each tile belongs to only one mm object, it could not be changed or copied, both monobehs so no problem with serialization

		public Coord coord = new Coord(int.MaxValue, int.MaxValue);
		public float distance = -1;  //distance in chunks from the center of the deploy rects
		public int Priority => (int)(-distance*100);

		public bool preview = true;

		public TerrainData defaultTerrainData;

		public Rect WorldRect => new Rect(coord.x*mapMagic.tileSize.x, coord.z*mapMagic.tileSize.z, mapMagic.tileSize.x, mapMagic.tileSize.z);
		public Vector2D Min => new Vector2D(coord.x*mapMagic.tileSize.x, coord.z*mapMagic.tileSize.z);
		public Vector2D Max => new Vector2D((coord.x+1)*mapMagic.tileSize.x, (coord.z+1)*mapMagic.tileSize.z);
		public bool ContainsWorldPosition(float x, float z)
		{
			Vector2D worldPos = new Vector2D(coord.x*mapMagic.tileSize.x, coord.z*mapMagic.tileSize.z);
			return  x > worldPos.x  &&   x < worldPos.x+mapMagic.tileSize.x  &&
					z > worldPos.z  &&   z < worldPos.z+mapMagic.tileSize.z;
		}

		public static Action<TerrainTile, TileData> OnBeforeTileStart;
		public static Action<TerrainTile, TileData> OnBeforeTilePrepare;
		public static Action<TerrainTile, TileData, StopToken> OnBeforeTileGenerate;
		public static Action<TerrainTile, TileData, StopToken> OnTileFinalized; //tile event
		public static Action<TerrainTile, TileData, StopToken> OnTileComplete;
		public static Action<TerrainTile, TileData, StopToken> OnTileApplied;
		public static Action<MapMagicObject> OnAllComplete;
		public static Action<TerrainTile, bool, bool> OnLodSwitched;
		public static Action<TileData> OnPreviewAssigned; //preview tile changed
		public static Action<TerrainTile> OnTileMoved;

		public static Action<TerrainTile> OnBeforeResetTerrain; //mainly for Lock, to save and return stored data
		public static Action<TerrainTile> OnAfterResetTerrain;


		[System.Serializable]
		public class DetailLevel
		{
			[NonSerialized] public TileData data; //also assigned on before serialize
			public Terrain terrain;
			public EdgesSet edges = new EdgesSet(); //edges are serializable, while data is not

			public bool generateStarted = true;	//to avoid starting generate for the second time
			public bool generateReady = false;	//used to control progress bar and lod switch, does not affect task planning
			public bool applyReady = false;		//practice shows two bools better than stage enum

			[NonSerialized] public StopToken stop;  //a tag to stop last assigned task
			[NonSerialized] public ThreadManager.Task task;
			[NonSerialized] public CoroutineManager.Task coroutine;
			[NonSerialized] public Stack<CoroutineManager.Task> applyMainCoroutines;
			[NonSerialized] public CoroutineManager.Task applyDraftCoroutine;
			[NonSerialized] public CoroutineManager.Task switchLodCoroutine; //should be cancelled somehow, but shouldn't be added to coroutines list (otherwise IsGenerating return true)

			public DetailLevel (TerrainTile tile, bool isDraft) { data=new TileData(); terrain = tile.CreateTerrain(isDraft); }
			public void Remove () { data?.Clear(inSubs:true); if (terrain!=null) GameObject.DestroyImmediate(terrain.gameObject); }

			public void SetPriority (int priority)
			{
				if (task != null) task.priority = priority;
				if (coroutine != null) coroutine.priority = priority;
				if (applyDraftCoroutine != null) applyDraftCoroutine.priority = priority;
				if (switchLodCoroutine != null) switchLodCoroutine.priority = priority;
				if (applyMainCoroutines != null)
				{
					foreach (CoroutineManager.Task c in applyMainCoroutines)
						if (c != null) c.priority = priority;
				}
			}
		}

		[NonSerialized] public DetailLevel main;
		[NonSerialized] public DetailLevel draft;
		//serializing on onbeforeserialize

		public ObjectsPool objectsPool;

		public bool guiMain;
		public bool guiDraft;


		public Terrain GetTerrain (bool isDraft)  =>  isDraft ? draft?.terrain : main?.terrain;
		public bool ContainsTerrain (Terrain terrain)  =>  terrain==draft?.terrain  || terrain==main?.terrain;

		public Terrain ActiveTerrain 
		/// Setting null will disable both terrains
		{
			get{
				if (main!=null && IsLiveTerrain(main.terrain)  &&  main.terrain.isActiveAndEnabled) 
					return main.terrain;
				if (draft!=null && IsLiveTerrain(draft.terrain)  &&  draft.terrain.isActiveAndEnabled) 
					return draft.terrain;
				return null;
			}

			set{
				// L19: destroyed/dangling Terrain wrappers reach SetActive and native-crash
				// (L18 LIVE: set_ActiveTerrain → GameObject.SetActive_Injected during SwitchLod/ApplyRoutine).
				if (main!=null && value==main.terrain)
				{ 
					SafeSetTerrainActive(main.terrain, true);
					if (draft != null) SafeSetTerrainActive(draft.terrain, false);
				}
				else if (draft!=null && value==draft.terrain)
				{
					if (main != null) SafeSetTerrainActive(main.terrain, false);
					SafeSetTerrainActive(draft.terrain, true);
				}
				else
				{
					if (main != null) SafeSetTerrainActive(main.terrain, false);
					if (draft != null) SafeSetTerrainActive(draft.terrain, false);
				}
			}
		}

		/// <summary>
		/// L19 product guard: Unity fake-null and destroyed native Terrain/GameObject must not call SetActive.
		/// </summary>
		private static bool IsLiveTerrain(Terrain terrain)
		{
			// UnityEngine.Object overloaded == handles destroyed wrappers.
			return terrain != null && terrain.gameObject != null;
		}

		private static void SafeSetTerrainActive(Terrain terrain, bool active)
		{
			if (!IsLiveTerrain(terrain))
				return;
			GameObject go = terrain.gameObject;
			if (go == null)
				return;
			if (go.activeSelf == active)
				return;
			go.SetActive(active);
		}



		public void SwitchLod ()
		/// Changes detail level based on main and draft avaialability and readyness
		/// Doesn't start generate (it's done by Dist), only welds drafts (not mains)
		{
			if (this == null) return; //happens after scene switch

			// L19: ApplyRoutine/Update can call SwitchLod after tile teardown or with non-finite remoteness.
			if (mapMagic == null)
				return;
			if (!float.IsFinite(distance))
				distance = float.MaxValue;

			Profiler.BeginSample("Switch Lod");

			// Only consider detail levels whose Terrain native object is still alive.
			bool useMain = main!=null && IsLiveTerrain(main.terrain);
			bool useDraft = draft!=null && IsLiveTerrain(draft.terrain);
			//if both using main
			//if none disabling terrain

			//in editor
			#if UNITY_EDITOR
			if (!MapMagicObject.isPlaying)
			{
				//if both detail levels are used - choosing the one should be displayed
				if (useMain && useDraft) 
				{
					//if generating Draft in DraftData - switching to draft
					if (draft.data!=null  &&  mapMagic?.graph!=null  &&  !draft.data.AllOutputsReady(mapMagic.graph, OutputLevel.Draft, inSubs:true))
						useMain = false;

					//if generating Both in MainData - switching to draft too
					if (main.data!=null  &&  mapMagic?.graph!=null  &&  !main.data.AllOutputsReady(mapMagic.graph, OutputLevel.Draft | OutputLevel.Main, inSubs:true))
						useMain = false;

					//if dragging graph dragfield - do not switch from draft back to main
					if (mapMagic.guiDraggingField  &&  ActiveTerrain == draft.terrain)
						useMain = false; 
				}
			}
			else
			#endif

			//if playmode
			{
				//default case with drafts
				if (mapMagic.draftsInPlaymode)
				{
					if ((int)distance > mapMagic.mainRange)  useMain = false;
					if ((int)distance > mapMagic.tiles.generateRange  &&  mapMagic.hideFarTerrains)  useDraft = false;
				}

				//case no drafts at all
				else
				{
					if ((int)distance > mapMagic.tiles.generateRange  &&  mapMagic.hideFarTerrains)  useMain = false;
					useDraft = false;
				}

				//hiding just moved terrains
				if (main!=null  &&  !main.applyReady) useMain = false; 
				if (draft!=null  &&  !draft.applyReady) useDraft = false; 

				//if main is not ready and using drafts
				if (useMain  &&  useDraft  &&  !main.applyReady) useMain = false;
			}

			//debugging
			//string was = ActiveTerrain==main.terrain ? "main" : (ActiveTerrain==draft.terrain ? "draft" : "null");
			//string replaced = useMain ? "main" : (useDraft ? "draft" : "null");
			//Debug.Log("Switching lod. Was " + was + ", replaced with " + replaced);
			//if (was == "draft" && replaced == "main")
			//	Debug.Log("Test");

			//finding if lod switch is for real and switching active terrain
			Terrain newActiveTerrain;
			if (useMain) newActiveTerrain = main.terrain;
			else if (useDraft) newActiveTerrain = draft.terrain;
			else newActiveTerrain = null;

			// Drop selection if wrapper went dead between flag compute and assign.
			if (newActiveTerrain != null && !IsLiveTerrain(newActiveTerrain))
			{
				newActiveTerrain = null;
				useMain = false;
				useDraft = false;
			}

			bool lodSwitched = false;
			if (ActiveTerrain != newActiveTerrain) 
			{
				lodSwitched = true;
				ActiveTerrain = newActiveTerrain;
			}

			//disabling objects
			if (objectsPool != null)
			{
				bool objsEnabled = useMain; // || (useDraft && mapMagic.draftsIfObjectsChanged);
				bool currentObjsEnabled = objectsPool.isActiveAndEnabled;
				if (!objsEnabled && currentObjsEnabled && objectsPool.gameObject != null)
					objectsPool.gameObject.SetActive(false);
				if (objsEnabled && !currentObjsEnabled && objectsPool.gameObject != null)
					objectsPool.gameObject.SetActive(true);
			}
			

			//welding
			bool isTerrainActive = IsLiveTerrain(newActiveTerrain) && newActiveTerrain.isActiveAndEnabled;
			if (lodSwitched && isTerrainActive &&
				mapMagic.tiles != null && mapMagic.tiles.Contains(coord) ) //otherwise error on SwitchLod called from Generate (when tile has been moved)
			{
				if (useMain)
				{
					Weld.WeldSurroundingDraftsToThisMain(mapMagic.tiles, coord);
					Weld.WeldCorners(mapMagic.tiles, coord);

					//Weld.SetNeighbors(mapMagic.tiles, coord); 
					//Unity calls Terrain.SetConnectivityDirty on each terrain enable or disable that resets neighbors
					//using autoConnect instead. AutoConnect is a crap but neighbors are broken
				}
				else if (useDraft  &&  draft.applyReady) 
					Weld.WeldThisDraftWithSurroundings(mapMagic.tiles, coord);
			}

			if (lodSwitched) OnLodSwitched?.Invoke(this, useMain, useDraft);

			//CoroutineManager.Enqueue( ()=>Weld.SetNeighbors(mapMagic.tiles, coord) );
			//CoroutineManager.Enqueue( mapMagic.Tmp );
			//mapMagic.Tmp();

			Profiler.EndSample();
		}



		public void ResetTerrain ()
		/// Removes terrain and children, re-constructing tile. Used to clear some output
		{
			OnBeforeResetTerrain?.Invoke(this);

			bool hasMain = main!=null;
			bool hasDraft = draft!=null;

			//removing all children
			for (int i=transform.childCount-1; i>-0; i--)
				GameObject.DestroyImmediate(transform.GetChild(i).gameObject);

			//creating new
			if (hasMain) main = new DetailLevel(this, isDraft:false);
			if (hasDraft) draft = new DetailLevel(this, isDraft:true);
			CreateObjectsPool();

			OnAfterResetTerrain?.Invoke(this);
		}


		#region ITile

			public static TerrainTile Construct (MapMagicObject mapMagic)
			{
				Profiler.BeginSample("Construct Internal");

				GameObject go = new GameObject();
				go.transform.parent = mapMagic.transform;
				TerrainTile tile = go.AddComponent<TerrainTile>();
				tile.mapMagic = mapMagic;
				
				//tile.Resize(mapMagic.tileSize, (int)mapMagic.tileResolution, mapMagic.tileMargins, (int)mapMagic.lodResolution, mapMagic.lodMargins);
				
				//creating detail levels in playmode (for editor Pin us used)
				if (MapMagicObject.isPlaying) //if (MapMagicObject.isPlayingOrWillChangePlaymode)
				{
					tile.main = new DetailLevel(tile, isDraft:false); //tile created in any case and generated at the background

					if (mapMagic.draftsInPlaymode)
						tile.draft = new DetailLevel(tile, isDraft:true);
				}

				//creating objects pool
				tile.CreateObjectsPool();

				Profiler.EndSample();

				return tile;
			}


			public void Pin (bool asDraftOnly)
			{
				if (mapMagic.draftsInEditor && draft==null)
					draft = new DetailLevel(this, isDraft:true);

				if (!asDraftOnly && main==null)
					main = new DetailLevel(this, isDraft:false);

				if (asDraftOnly && main!=null)
					{ main.Remove(); main=null; }
			}


			public void Move (Coord newCoord, float newRemoteness)
			{
				coord = newCoord;

				//if (IsGenerating) //stopping anyway just in case
					Stop();

				//clearing
				main?.data?.Clear(inSubs:true);
				draft?.data?.Clear(inSubs:true);

				if (main!=null) { main.applyReady = false;	main.generateReady = false;		main.generateStarted = false; }
				if (draft != null) { draft.applyReady = false;	draft.generateReady = false;	draft.generateStarted = false; }

				ActiveTerrain = null; //disabling terrains

				//resizing (if needed)
				Vector3 size = (Vector3)mapMagic.tileSize;
				Vector3 position = new Vector3(coord.x*size.x, 0, coord.z*size.z);

				if (main!=null  &&  main.terrain != null  &&  main.terrain.terrainData.size != new Vector3 (size.x, main.terrain.terrainData.size.y, size.z)) 
					main.terrain.terrainData.size = new Vector3(size.x, main.terrain.terrainData.size.y, size.z);

				if (draft!=null && draft.terrain != null  &&  draft.terrain.terrainData.size != new Vector3 (size.x, draft.terrain.terrainData.size.y, size.z)) 
					draft.terrain.terrainData.size = new Vector3(size.x, draft.terrain.terrainData.size.y, size.z);

				//moving
				transform.localPosition = position;
				gameObject.name = "Tile " + coord.x + "," + coord.z;

				//switch Dist (on each move)
				Dist(newRemoteness);

				OnTileMoved?.Invoke(this);
			}


			public void Dist (float newRemoteness)
			{
				distance = newRemoteness;

				if (MapMagicObject.isPlaying) 	
				{
					if (main != null  &&
						!main.generateStarted  &&
						(int)distance <= mapMagic.mainRange) 
								StartGenerate(mapMagic.graph, generateMain:true, generateLod:false);

					if (draft != null  &&
						!draft.generateStarted  &&
						(int)distance <= mapMagic.tiles.generateRange)
								StartGenerate(mapMagic.graph, generateMain:false, generateLod:true);

					//switching lod in playmode
					if (coord != new Coord(int.MaxValue, int.MaxValue))  //skipping tiles that were just created to avoid showing blank terrain and error on weld
						SwitchLod();
				}

				else //editor mode
				{
					if (draft != null  &&  !draft.generateStarted) StartGenerate(mapMagic.graph, generateMain:false, generateLod:true);
					if (main != null  &&  !main.generateStarted) StartGenerate(mapMagic.graph, generateMain:true, generateLod:false);
				}

				main?.SetPriority(Priority);
				draft?.SetPriority(Priority + 1000);
			}


			public void Remove ()
			{
				Stop();

				#if UNITY_EDITOR
				if (!MapMagicObject.isPlaying)
					GameObject.DestroyImmediate(gameObject);
				else
				#endif
					GameObject.Destroy(gameObject);
			}


			public bool IsNull {get{ return this==(UnityEngine.Object)null || this.Equals(null) || gameObject==null || gameObject.Equals(null); } }
			
			//public bool Equals(TerrainTile tile) { return (object)this == (object)tile; }

			
			public void Resize ()
			{
				Move(coord, distance);
				//yep, it will change the tile size, including the height
			}


			public Terrain CreateTerrain (bool isDraft)
			{
				GameObject go = new GameObject();
				go.transform.parent = transform;
				go.transform.localPosition = new Vector3(0,0,0);
				go.name = isDraft ? "Draft Terrain" : "Main Terrain";

				Terrain terrain = go.AddComponent<Terrain>();
				TerrainCollider terrainCollider = go.AddComponent<TerrainCollider>();

				TerrainData terrainData;
				TerrainData template = Resources.Load<TerrainData>("MapMagicDefaultTerrainData");
				if (template != null)	
					terrainData = GameObject.Instantiate(template); 
				else
					terrainData = new TerrainData(); 

				terrain.terrainData = terrainData;
				terrainCollider.terrainData = terrainData;
				terrainData.size = (Vector3)mapMagic.tileSize;

				mapMagic.terrainSettings.ApplyAll(terrain);
				terrain.groupingID = isDraft ? -2 : -1;

				return terrain;
			}

			public void CreateObjectsPool ()
			{
				GameObject poolGo = new GameObject();
				poolGo.transform.parent = transform;
				poolGo.transform.localPosition = new Vector3();
				poolGo.name = "Objects";
				objectsPool = poolGo.AddComponent<ObjectsPool>();
			}

		#endregion




		#region Threaded

			public void Refresh (Graph graph, bool clearAll=false) 
			/// Stops ongoing tasks, clears change, starts again - in that order.
			/// Clear change between stop and start - stopping it after clearing change might result in some output ready - with outdated data
			{
				if (main != null)
					StopTask(main); //stopping only main tiles - drafts update one by one until the end

				ClearChanged(graph, clearAll);

				StartGenerate(graph, generateMain:true, generateLod:true);
			}


			public void ClearChanged (Graph graph, bool clearAll=false)
			{
				if (clearAll)
				{
					Stop(); //this will reset tile tasks

					main?.data?.Clear(inSubs:true);
					draft?.data?.Clear(inSubs:true); 
				}

				if (main?.data!=null) 
					graph.ClearChanged(main.data, clearAll);
					
				if (draft?.data!=null) 
					graph.ClearChanged(draft.data, clearAll);
			}


			public void StartGenerate (Graph graph, bool generateMain=true, bool generateLod=true)
			/// Starts generating tile in a separate thread (or just enqueues it if `launch` is set to false)
			{
				if (graph==null) return;

				//starting draft
				if (generateLod  &&  draft != null)
				{
					if (draft.data == null) draft.data = new TileData();
					draft.data.area = new Area(coord, (int)mapMagic.draftResolution, mapMagic.draftMargins, mapMagic.tileSize);
					draft.data.globals = mapMagic.globals;
					draft.data.random = graph.random;
					draft.data.isPreview = false; //don't preview draft in any case
					draft.data.isDraft = true;

					//if (draft.coroutines == null) draft.coroutines = new Stack<CoroutineManager.Task>();
					//while (draft.coroutines.Count != 0)
					//	CoroutineManager.Stop(draft.coroutines.Pop());

					draft.generateStarted = true;
					draft.applyReady = false;
					draft.generateReady = false;

					EnqueueTask(draft, graph, Priority+1000, "Draft");
				}

				//starting main
				if (generateMain  &&  main != null)
				{
					if (main.data == null) main.data = new TileData();
					main.data.area = new Area(coord, (int)mapMagic.tileResolution, mapMagic.tileMargins, mapMagic.tileSize);
					main.data.globals = mapMagic.globals;
					main.data.random = graph.random;
					main.data.isPreview = mapMagic.PreviewData==main.data;
					main.data.isDraft = false;

					main.generateStarted = true;
					main.applyReady = false;
					main.generateReady = false;

					EnqueueTask(main, graph, Priority, "Main");
					//EnqueueTask(main, graph, Priority, "Main");
				}

				SwitchLod(); //switching to draft if needed
			}


			private void EnqueueTask (DetailLevel det, Graph graph, int priority=0, string name="Task")
			{
				if (det.task == null  ||  !det.task.Enqueued)
				{
					Prepare(graph, this, det);

					det.stop = new StopToken();
					StopToken stop = det.stop; //closure var
					det.task = new ThreadManager.Task() { 
						action = ()=>Generate(graph, this, det, stop), 
						priority = priority, 
						name = name + " " + coord };
					ThreadManager.Enqueue(det.task);
				}

				det.task.priority = priority;
				
				if (det.task.Active) det.stop.restart = true;
				else
				{
					if (!det.task.Enqueued) 
					{
						Prepare(graph, this, det);
						ThreadManager.Enqueue(det.task);
					}
				}
			}


			private void StopTask (DetailLevel det, bool dequeue=true)
			/// Will stop previous task before running
			{
				//stopping coroutines
				if (det.applyMainCoroutines == null) det.applyMainCoroutines = new Stack<CoroutineManager.Task>();
				while (det.applyMainCoroutines.Count != 0)
					CoroutineManager.Stop(det.applyMainCoroutines.Pop());

				if (det.switchLodCoroutine != null)
					CoroutineManager.Stop(det.switchLodCoroutine);

				if (det.coroutine != null)
					CoroutineManager.Stop(det.coroutine);

				//dequeue
				if (dequeue && det.task!=null)
				{
					#if MM_DEBUG
					if (det.task.Enqueued)
						Log.AddThreaded("TerrainTile.StopEnqueueTask Dequeuening", ("coord:",det.data?.area?.Coord), ("draft:",det.data?.isDraft)); 
					#endif

					ThreadManager.Dequeue(det.task);
				}

				//active
				if (det.task != null  &&  det.task.Active) 
				{
					if (det.stop != null)
					{
						det.stop.stop = true;
						det.stop.restart = false;

						#if MM_DEBUG
						Log.AddThreaded("TerrainTile.StopEnqueueTask ActiveStopped", ("coord:",det.data?.area?.Coord), ("draft:",det.data?.isDraft)); 
						#endif
					}
				}

				//forgetting task if it was dequeued
				if (dequeue)
					det.task = null;
			}


			public void Stop ()
			{
				if (main != null) StopTask(main, dequeue:true);
				if (draft != null) StopTask(draft, dequeue:true);
			}


			[Obsolete] private void StopEnqueueTask (DetailLevel det, Graph graph, int priority=0, string name="Task")
			/// Will stop previous task before running
			{
				if (det.applyMainCoroutines == null) det.applyMainCoroutines = new Stack<CoroutineManager.Task>();
				while (det.applyMainCoroutines.Count != 0)
					CoroutineManager.Stop(det.applyMainCoroutines.Pop());

				if (det.switchLodCoroutine != null)
					CoroutineManager.Stop(det.switchLodCoroutine);

				if (det.coroutine != null)
					CoroutineManager.Stop(det.coroutine);

				if (det.task != null) 
				{
					#if MM_DEBUG
					Log.AddThreaded("TerrainTile.StopEnqueueTask Test", ("coord:",det.data.area.Coord), ("active:",det.task.Active), ("cl graph ver:",graph.IdsVersionsHash()));
					#endif
				}

				if (det.task != null  &&  det.task.Active) 
				{
					det.stop.stop = true;
					//and forget about this task

					#if MM_DEBUG
					Log.AddThreaded("TerrainTile.StopEnqueueTask Stopped", ("coord:",det.data.area.Coord), ("cl graph ver:",graph.IdsVersionsHash()));
					#endif
				}

				if (det.task == null  ||  !det.task.Enqueued)
				{
					Prepare(graph, this, main);

					det.stop = new StopToken();
					StopToken stop = det.stop; //closure var
					det.task = new ThreadManager.Task() { 
						action = ()=>Generate(graph, this, det, stop), 
						priority = priority, 
						name = name + " " + coord };
					ThreadManager.Enqueue(det.task);
				}
				//do nothing if task enqueued

				det.task.priority = priority;
			}


			private void Prepare (Graph graph, TerrainTile tile, DetailLevel det)
			{
				det.edges.ready = false;

				OnBeforeTilePrepare?.Invoke(tile, det.data);

				graph.Prepare(det.data, det.terrain);
				//was using data's parent graph
			}


			private void Generate (Graph graph, TerrainTile tile, DetailLevel det, StopToken stop)
			/// Note that referencing det.task is illegal since task could be changed
			{
				OnBeforeTileGenerate?.Invoke(tile, det.data, stop);

				//do not return (for draft) until the end (apply)
//				if (!stop.stop) graph.CheckClear(det.data, stop);
				if (!stop.stop) graph.Generate(det.data, stop);
				if (!stop.stop) graph.Finalize(det.data, stop);

				//finalize event
				OnTileFinalized?.Invoke(tile, det.data, stop);
					
				//flushing products for playmode (all except apply)
				if (MapMagicObject.isPlaying)
					det.data.Clear(clearApply:false, inSubs:true);

				//welding (before apply since apply will flush 2d array)
				if (!stop.stop) Weld.ReadEdges(det.data, det.edges);
				if (!stop.stop) Weld.WeldEdgesInThread(det.edges, tile.mapMagic.tiles, tile.coord, det.data.isDraft);
				if (!stop.stop) Weld.WriteEdges(det.data, det.edges);

				//enqueue apply 
				//was: while the playmode is applied on SwitchLod to avoid unnecessary lags for main

				if (det.data.isDraft)
					det.coroutine = CoroutineManager.Enqueue(()=>ApplyNow(det,stop), Priority+1000, "ApplyNow " + coord);

				else //main
				{
					IEnumerator coroutine = ApplyRoutine(det, stop);
					det.coroutine = CoroutineManager.Enqueue(coroutine, Priority, "ApplyRoutine " + coord);
				}		
				
				det.generateReady = true;
			}


			private void ApplyNow (DetailLevel det, StopToken stop)
			{
				if (this == null) return;

				if (stop==null || !stop.stop)
				{
					while (det.data.ApplyMarksCount != 0)
					{
						var appDat = det.data.DequeueApply();
						appDat.Apply(det.terrain);
					}

					//MapMagicObject.OnTileComplete?.Invoke(this, det.data, stop);

					det.applyReady = true; //enabling ready before switching lod (otherwise will leave draft)

					OnTileApplied?.Invoke(this, det.data, stop);

					SwitchLod();

					OnTileComplete?.Invoke(this, det.data, stop);

					//if (!mapMagic.IsGenerating()) //won't be called since this couroutine still left
					if (!ThreadManager.IsWorking && CoroutineManager.IsQueueEmpty)
						OnAllComplete?.Invoke(mapMagic);
				}

				if (stop.restart) 
				{ 
					stop.restart=false; 
					//Prepare(graph, this, det);
					if (!det.task.Enqueued) ThreadManager.Enqueue(det.task); 
				}
			}


			private IEnumerator ApplyRoutine (DetailLevel det, StopToken stop)
			{
				if (this == null) yield break;

				if (stop==null || !stop.stop)
				{
					while (det.data.ApplyMarksCount != 0)
					{
						if (stop!=null && stop.stop) yield break;

						IApplyData apply = det.data.DequeueApply();	//this will remove apply from the list
																	//coroutines guarantee FIFO
						if (apply is IApplyDataRoutine)
						{
							IEnumerator routine = (apply as IApplyDataRoutine).ApplyRoutine(det.terrain);
							while (true) 
							{
								if (stop!=null && stop.stop) yield break;

								bool move = routine.MoveNext();
								yield return null;

								if (!move) break;
							}
						}
						else
						{
							apply.Apply(det.terrain);
							yield return null;
						}
					}
				}

				if (stop==null || !(stop.stop || stop.restart)) //can't set ready when restart enqueued
				{
					det.applyReady = true; //enabling ready before switching lod (otherwise will leave draft)

					OnTileApplied?.Invoke(this, det.data, stop);

					SwitchLod();

					OnTileComplete?.Invoke(this, det.data, stop);
					
					//if (!mapMagic.IsGenerating()) //won't be called since this couroutine still left
					if (!ThreadManager.IsWorking && CoroutineManager.IsQueueEmpty)
						OnAllComplete?.Invoke(mapMagic);
				}

				if (stop!=null && stop.restart) 
				{ 
					stop.restart=false; 
					//Prepare(graph, this, det);
					if (!det.task.Enqueued) ThreadManager.Enqueue(det.task); 
				}
			}


			public (float progress, float max) GetProgress (Graph graph, float generateComplexity, float applyComplexity)
			{
				float progress = 0;
				float max = 0;

				if (main != null  &&  main.generateStarted)
				{
					max += generateComplexity + applyComplexity;

					if (main.generateReady) progress += generateComplexity;
					else if (main.data != null)  progress += graph.GetGenerateProgress(main.data);

					if (main.applyReady) progress += applyComplexity;
					else if (main.data != null) progress += graph.GetApplyProgress(main.data);
				}

				if (draft != null  &&  draft.generateStarted)
				{
					max += 2;
					if (draft.generateReady) progress ++;
					if (draft.applyReady) progress ++;
				}

				return (progress, max); 
			}


			public bool IsGenerating 
			{get{
				if (main != null  &&  main.generateStarted  &&  !main.applyReady) return true;
				if (draft != null  &&  draft.generateStarted  &&  !draft.applyReady) return true;
				return false;
			}}

			public bool Ready
			{get{
				if (main != null  &&  (!main.applyReady || !main.generateReady)) return false;
				if (draft != null  &&  (!draft.applyReady || !draft.generateReady)) return false;
				return true;
			}}
				
			//public bool ReadyDraft
			//	{get{ return draft!=null && draft.stage != DetailLevel.Stage.Blank && draft.stage != DetailLevel.Stage.Ready; }}

		#endregion


		#region Serialization

			[SerializeField] private DetailLevel serialized_main;
			[SerializeField] private bool serialized_mainNull;

			[SerializeField] private DetailLevel serialized_draft;
			[SerializeField] private bool serialized_draftNull;

			public void OnBeforeSerialize () 
			{
				serialized_main = main;
				serialized_mainNull = main==null;

				serialized_draft = draft;
				serialized_draftNull = draft==null; 
			}


			public void OnAfterDeserialize () 
			{
				if (!serialized_mainNull)  
				{ 
					main = serialized_main;  
					//main.data = new TileData(); //data is not serialized, so it will be null

					if (!main.applyReady || !main.generateReady) //resetting ready state if it's not completely generated
						{ main.applyReady = false; main.generateReady = false; }
				}

				if (!serialized_draftNull) 
				{ 
					draft = serialized_draft;  
					//draft.data = new TileData();

					if (!draft.applyReady || !draft.generateReady) //resetting ready state if it's not completely generated
						{ draft.applyReady = false; draft.generateReady = false; }
				}
			}

		#endregion


		public void OnDrawGizmos_Tmp ()
		{
			Gizmos.color = Color.blue;
			Vector3 center = (Vector3)(coord.vector2d * mapMagic.tileSize.x + mapMagic.tileSize/2);
			Gizmos.DrawWireCube(center, (Vector3)mapMagic.tileSize);

			center.y += 150;

			//active terrain
			Gizmos.color = Color.red;
			if (draft != null && ActiveTerrain == draft.terrain) Gizmos.color = Color.yellow;
			if (main != null && ActiveTerrain == main.terrain) Gizmos.color = Color.green;
			Gizmos.DrawCube(center + new Vector3(-150,0,0), new Vector3(60,60,60));

			//main state
			Gizmos.color = Color.black;
			if (main != null)
			{
				Gizmos.color = Color.green;
				if (!main.applyReady) 
				{
					if (main.task.Enqueued) Gizmos.color = Color.red;
					if (main.task.Active) Gizmos.color = new Color(0.8f, 0.3f, 0, 1);
					
					if (main.applyMainCoroutines != null)
						foreach (CoroutineManager.Task coroutine in main.applyMainCoroutines)
							if (coroutine.Active || coroutine.Enqueued) Gizmos.color = Color.yellow;
				}
			}
			Gizmos.DrawSphere(center + new Vector3(-30,0,0), 60);

			//draft state
			Gizmos.color = Color.black;
			if (draft != null)
			{
				Gizmos.color = Color.green;
				if (!draft.applyReady) 
				{
					if (draft.task.Enqueued) Gizmos.color = Color.red;
					if (draft.task.Active) Gizmos.color = new Color(0.8f, 0.3f, 0, 1);
					
					if (draft.applyMainCoroutines != null)
						foreach (CoroutineManager.Task coroutine in draft.applyMainCoroutines)
							if (coroutine.Active || coroutine.Enqueued) Gizmos.color = Color.yellow;
				}
			}
			Gizmos.DrawSphere(center + new Vector3(90,0,0), 40);

			//lod switch enqueued
			if (CoroutineManager.IsNameEnqueued("LodSwitch " + coord)) Gizmos.color = Color.red;
			else if (CoroutineManager.IsNameActive("LodSwitch " + coord)) Gizmos.color = Color.yellow;
			else Gizmos.color = Color.green;
			Gizmos.DrawSphere(center + new Vector3(180,0,0), 30);

			//data size
			/*Gizmos.color = Color.gray;
			int dataSize = 0;
			if (main!=null) dataSize += main.data.Count();
			if (draft!=null) dataSize += draft.data.Count();
			dataSize *= 10;
			Gizmos.DrawCube(center + new Vector3(0,0,-120), new Vector3(dataSize,30,30));*/
		}
	}

}

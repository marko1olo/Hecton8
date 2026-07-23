using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

using Den.Tools;
using MapMagic.Products;

namespace MapMagic.Nodes.MatrixGenerators
{

	/*[System.Serializable]
	[GeneratorMenu (menu="Map/Input", name ="Textures In", section=1, disengageable = true)]
	public class TexturesInput : Generator, IOutlet<MatrixWorld>, ITerrainReader
	{
		[Val(name="Channel")] public int channel = 0;

		public void CheckReadTerrain (Terrain terrain, Results results)
		{
			if (results.terrainReads.ContainsKey(typeof(SplatData))) return; //already read

			SplatData data = new SplatData();
			data.ReadFromTerrain(terrain);
			results.terrainReads.Add(typeof(SplatData), data);
		}

		public override void Generate (Results results, Area area, int seed, StopCallback stop)
		{
			if (!enabled) { results.SetProduct(this, null); return; }  //should set anything to mark as generated

			SplatData data = null;
			if (results.terrainReads.ContainsKey(typeof(SplatData))) data = (SplatData)results.terrainReads[typeof(SplatData)];
			if (data==null) { results.SetProduct(this, null); return; }

			if (stop!=null && stop(0)) return; 

			MatrixWorld matrix = new MatrixWorld(area.full.resolution, area.full.position, area.full.size);
			Floats3DtoMatrix(data.splats3D, channel, matrix, area);

			if (stop!=null && stop(0)) return;
			results.SetProduct(this, matrix);
		}

		public void Floats3DtoMatrix (float[,,] splats3D, int channel, Matrix matrix, Area area)
		{
			int splatsResolution = splats3D.GetLength(0);
			int margins = area.Margins;
			
			//simple case if resolution match
			if (area.active.resolution == splatsResolution)
			{
				for (int x=0; x<matrix.rect.size.x; x++)
					for (int z=0; z<matrix.rect.size.z; z++)
					{
						int ax = x - margins;
						int az = z - margins;

						if (ax<0) ax = 0; else if (ax>=splatsResolution) ax = splatsResolution-1;
						if (az<0) az = 0; else if (az>=splatsResolution) az = splatsResolution-1;

						float val = splats3D[az,ax, channel];
						matrix.array[z*matrix.rect.size.x + x] = val; //do not use matrix[x,z] since x/z are 0-based
					}
			}
		
			//interpolated if resolution doesn't match
			else
			{
				Matrix tmpMatrix = new Matrix( new CoordRect(0, 0, splatsResolution, splatsResolution) );

				for (int x=0; x<splatsResolution; x++)
					for (int z=0; z<splatsResolution; z++)
						tmpMatrix.array[z*splatsResolution + x] = splats3D[z,x, channel];
				
				Den.Tools.Matrices.MatrixOps.Resize(tmpMatrix, matrix);
			}
		}
	}*/

}

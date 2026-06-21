
using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;

using Den.Tools;
using Den.Tools.GUI;


namespace Den.Tools.Matrices
{
	[CustomEditor(typeof(MatrixObject))]
	public class MatrixObjectInspector : Editor
	{
		MatrixObject matrixObject;
		UI ui = new UI(); 

		bool colorize = false;
		bool relief = false;
	
		public override void OnInspectorGUI () => ui.Draw(DrawGUI, inInspector:true);
		private void DrawGUI ()
		{
			matrixObject = (MatrixObject)target;

			MatrixAssetInspector.DrawPreviewAndImport(
				matrixObject, matrixObject.matrix, matrixObject.preview, () => matrixObject.RefreshPreview(),
				ref matrixObject.source, ref matrixObject.rawPath, ref matrixObject.textureSource, ref matrixObject.channelSource,
				() => matrixObject.Reload(), ref colorize, ref relief);

			if (matrixObject.source == MatrixAsset.Source.New)
			{
				using (Cell.LinePx(0)) 
				{
					using (Cell.LineStd) Draw.Field(ref matrixObject.newRes, "Resolution");
					using (Cell.LineStd) Draw.Field(ref matrixObject.newOffset, "Offset");

					if (Cell.current.valChanged)
						matrixObject.Reload();
				}
			}

			MatrixAssetInspector.DrawReloadButton(
				matrixObject, matrixObject.source, matrixObject.rawPath, matrixObject.textureSource, () => matrixObject.Reload());

			Cell.EmptyLinePx(5);
			using (Cell.LineStd) Draw.Field(ref matrixObject.worldPosition, "World Pos");
			using (Cell.LineStd) Draw.Field(ref matrixObject.worldSize, "World Size");
			using (Cell.LineStd) Draw.Field(ref matrixObject.worldHeight, "World Height");

			Cell.EmptyLinePx(5);
			using (Cell.LineStd) Draw.Field(ref matrixObject.displayGizmo, "Gizmo");
			using (Cell.LineStd) Draw.ToggleLeft(ref matrixObject.centerCell, "Center Cell");
			using (Cell.LineStd) Draw.Field(ref matrixObject.filterMode, "Filter Mode");
		}


		public virtual void OnSceneGUI()
		{
			if (matrixObject.displayGizmo == MatrixObject.DisplayGizmo.Texture)
			{
				if (matrixObject.textureGizmo == null) matrixObject.Reload();
				matrixObject.textureGizmo.SetOffsetSize(matrixObject.worldPosition, matrixObject.worldSize);
				matrixObject.textureGizmo.Draw();
			}

			if (matrixObject.displayGizmo == MatrixObject.DisplayGizmo.Height  ||  matrixObject.displayGizmo == MatrixObject.DisplayGizmo.FacetedHeight)
			{
				if (matrixObject.heightGizmo == null) matrixObject.Reload();
				matrixObject.heightGizmo.SetOffsetSize(
					(Vector3)matrixObject.worldPosition, 
					new Vector3(matrixObject.worldSize.x, matrixObject.worldHeight, matrixObject.worldSize.z));
				matrixObject.heightGizmo.Draw();
			}
		}
	}
}
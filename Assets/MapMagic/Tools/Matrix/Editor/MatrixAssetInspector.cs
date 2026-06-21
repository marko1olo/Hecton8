
using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;

using Den.Tools;
using Den.Tools.Matrices;
using Den.Tools.GUI;


//namespace MapMagic.Core 
namespace Den.Tools
{
	[CustomEditor(typeof(MatrixAsset))]
	public class MatrixAssetInspector : Editor
	{
		MatrixAsset matrixAsset;
		UI ui = new UI(); 

		bool colorize = false;
		bool relief = false;


		public override void OnInspectorGUI ()
		{
			ui.Draw(DrawGUI, inInspector:true);
		}


		private void DrawGUI ()
		{
			matrixAsset = (MatrixAsset)target;

			using (Cell.LinePx(32))
				Draw.Label("WARNING: Serializing this asset when selected in \nInspector can slow down editor GUI performance.", style:UI.current.styles.helpBox);

			Cell.EmptyLinePx(5);

			DrawPreviewAndImport(
				matrixAsset, matrixAsset.matrix, matrixAsset.preview, () => matrixAsset.RefreshPreview(),
				ref matrixAsset.source, ref matrixAsset.rawPath, ref matrixAsset.textureSource, ref matrixAsset.channelSource,
				() => matrixAsset.Reload(), ref colorize, ref relief);

			DrawReloadButton(
				matrixAsset, matrixAsset.source, matrixAsset.rawPath, matrixAsset.textureSource, () => matrixAsset.Reload());
		}

		public static void DrawPreviewAndImport (
			UnityEngine.Object targetObj, Matrix matrix, Texture2D preview, Action refreshPreview,
			ref MatrixAsset.Source source, ref string rawPath, ref Texture2D textureSource, ref MatrixAsset.Channel channelSource,
			Action reload, ref bool colorize, ref bool relief)
		{
			if (matrix != null  &&  preview == null)
				refreshPreview();

			if (preview != null)
			{
				using (Cell.LinePx(256))
				{
					Cell.EmptyRowRel(1);
					using (Cell.RowPx(256)) 
					{
						Draw.MatrixPreviewTexture(preview, colorize:colorize, relief:relief, min:0, max:1);
						Draw.MatrixPreviewReliefSwitch(ref colorize, ref relief);
					}
					Cell.EmptyRowRel(1);
				}

				if (rawPath != null)
					using (Cell.LineStd) Draw.Label(rawPath);

				if (matrix != null)
					using (Cell.LineStd) Draw.Label(matrix.rect.size.x + ", " + matrix.rect.size.z);
			}

			Cell.EmptyLinePx(5);

			using (Cell.LineStd) Draw.Field(ref source, "Map Source");

			if (source == MatrixAsset.Source.Raw)
			{
				using (Cell.LinePx(22)) Draw.Label("Square gray 16bit RAW, PC byte order", style:UI.current.styles.helpBox);

				using (Cell.LineStd) if (Draw.Button("Load RAW"))
				{
					string newPath = EditorUtility.OpenFilePanel("Import Texture File", "", "raw,r16");

					if (newPath!=null && newPath.Length!=0)
					{
						UnityEditor.Undo.RecordObject(targetObj, "Import RAW");
						rawPath = newPath;

						reload();

						EditorUtility.SetDirty(targetObj);
					}
				}
			}

			else if (source == MatrixAsset.Source.Texture)
				using (Cell.LinePx(0))
				{
					using (Cell.LineStd) Draw.ObjectField(ref textureSource, "Texture"); //
					using (Cell.LineStd) Draw.Field(ref channelSource, "Channel"); //

					if (Cell.current.valChanged)
						reload();
				}
		}

		public static void DrawReloadButton (
			UnityEngine.Object targetObj, MatrixAsset.Source source, string rawPath, Texture2D textureSource, Action reload)
		{
			using (Cell.LineStd)
			{
				Cell.current.disabled = 
					(source == MatrixAsset.Source.Raw && rawPath == null) ||
					(source == MatrixAsset.Source.Texture && textureSource == null);
				if (Draw.Button("Reload"))
				{
					reload();
					EditorUtility.SetDirty(targetObj);
				}
			}
		}
	}

	class MatrixAssetTexturePostprocessor : AssetPostprocessor 
	{
		//public static WeakEvent<Texture2D> OnTextureImported = new WeakEvent<Texture2D>();
		//In MatrixAsset since it should work in non-editors

		//public void OnPostprocessTexture(Texture2D tex)  //using OnPostprocessAllAssets because tex here is not the same tex as the changed one

		static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
		{
			for (int a=0; a<importedAssets.Length; a++)
			{
				if (AssetDatabase.GetMainAssetTypeAtPath(importedAssets[a]) != typeof(Texture2D)) continue;
				Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(importedAssets[a]);

				if (MatrixAsset.OnTextureImported != null)
					MatrixAsset.OnTextureImported(tex);
			}
		}
	}
}

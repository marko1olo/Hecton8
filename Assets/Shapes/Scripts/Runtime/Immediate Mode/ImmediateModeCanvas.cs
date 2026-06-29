// Shapes © Freya Holmér - https://twitter.com/FreyaHolmer/
// Website & Documentation - https://acegikmo.com/shapes/

namespace Shapes {

	using System;
	using System.Collections.Generic;
	using UnityEngine;
	using UnityEngine.Rendering;

	[ExecuteAlways] [RequireComponent( typeof(Canvas) )]
	public class ImmediateModeCanvas : ImmediateModeShapeDrawer {

		static ImCanvasContext canvasContext = new ImCanvasContext();

		Canvas canvas;
		Canvas Canvas => canvas = canvas != null ? canvas : GetComponent<Canvas>();
		RectTransform canvasRectTf;
		RectTransform CanvasRectTf => canvasRectTf = canvasRectTf != null ? canvasRectTf : GetComponent<RectTransform>();
		Camera camUI;
		Camera CamUI => camUI = camUI != null ? camUI : Canvas.worldCamera;
		List<ImmediateModePanel> panels = new List<ImmediateModePanel>();

		bool IsCameraBasedUI => Canvas.worldCamera != null && Canvas.renderMode is RenderMode.WorldSpace;

		public void Add( ImmediateModePanel panel ) => panels.Add( panel );
		public void Remove( ImmediateModePanel panel ) => panels.Remove( panel );

		protected void DrawPanels() {
			using( Draw.Scope ) {
				foreach( ImmediateModePanel panel in panels ) {
					#if UNITY_EDITOR
					if( canvasContext.camera.cameraType is CameraType.SceneView && UnityEditor.SceneVisibilityManager.instance.IsHidden( panel.gameObject ) )
						continue; // don't draw hidden panels in the scene view
					#endif
					using( Draw.Scope ) {
						Draw.Matrix = panel.transform.localToWorldMatrix;
						panel.DrawPanel( canvasContext );
					}
				}
			}
		}

		bool CameraShouldRenderUI( Camera cam ) {
			#if UNITY_EDITOR
			// always display UI in the scene view, unless the game object is invisible
			if( cam.cameraType is CameraType.SceneView )
				return UnityEditor.SceneVisibilityManager.instance.IsHidden( gameObject ) == false;
			#endif
			if( cam.cameraType is CameraType.Game ) {
				// game cameras should only draw overlay UI if the camera display matches the canvas target display
				if( canvas.renderMode == RenderMode.ScreenSpaceOverlay )
					return cam.targetDisplay == canvas.targetDisplay;
				return cam == CamUI; // for world space UI, we only render in the UI camera, if matching
			}
			return false; // don't render in any other camera types
		}

		public override void DrawShapes( Camera cam ) {
			if( Canvas.enabled == false )
				return;
			if( CameraShouldRenderUI( cam ) == false )
				return;

			bool isOverlay = Canvas.renderMode == RenderMode.ScreenSpaceOverlay && !DisplayAsWorldSpacePanel( cam );

			RectTransform cnvTf = CanvasRectTf;

			DrawCommand drawCmd;
			if( isOverlay ) {
				Rect rect = cnvTf.rect;
				Matrix4x4 projMatrix = Matrix4x4.Ortho( rect.xMin, rect.xMax, rect.yMin, rect.yMax, -1000f, 1000f );

				// Unity's projection matrices handle the platform differences (e.g., upside down on D3D)
				// It handles view space differently too when setting manually
				// However, standard GL.GetGPUProjectionMatrix is typically used for this
				projMatrix = GL.GetGPUProjectionMatrix( projMatrix, true );

				// The view matrix must transform from world space into the canvas's local space
				Matrix4x4 viewMatrix = cnvTf.worldToLocalMatrix;
				drawCmd = Draw.Command( cam, viewMatrix, projMatrix );
			} else {
				drawCmd = Draw.Command( cam );
			}

			using( drawCmd ) {
				Draw.ZTest = CompareFunction.Always;
				canvasContext.UpdateParams( Canvas, cam, cnvTf, cnvTf.localToWorldMatrix );

				Draw.Matrix = canvasContext.canvasToWorldNet;
				DrawCanvasShapes( canvasContext );
			}
		}

		bool DisplayAsWorldSpacePanel( Camera cam ) => cam.cameraType == CameraType.SceneView || ( IsCameraBasedUI && cam == Canvas.worldCamera );

		/// <summary>The method to override in order to draw immediate mode shapes.
		/// Note: This is called from an existing Draw.Command context</summary>
		public virtual void DrawCanvasShapes( ImCanvasContext ctx ) {}

	}

}
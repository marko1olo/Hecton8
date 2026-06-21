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

			using( DrawCommand cmd = Draw.Command( cam ) ) {
				Draw.ZTest = CompareFunction.Always;
				RectTransform cnvTf = CanvasRectTf;

				// Apply the screen space overlay canvas matrix explicitly
				canvasContext.UpdateParams( Canvas, cam, cnvTf, cnvTf.localToWorldMatrix );

				if( isOverlay ) {
					// Set up orthographic projection matrix for UI screen space
					Matrix4x4 proj = Matrix4x4.Ortho( 0, cam.pixelWidth, 0, cam.pixelHeight, -1000f, 1000f );
					cmd.UseCustomViewProjection( Matrix4x4.identity, proj );
				}

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
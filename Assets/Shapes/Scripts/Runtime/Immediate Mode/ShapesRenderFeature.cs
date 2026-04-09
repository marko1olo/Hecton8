#if SHAPES_URP
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
#endif

#endif

// Shapes © Freya Holmér - https://twitter.com/FreyaHolmer/
// Website & Documentation - https://acegikmo.com/shapes/
namespace Shapes {

	#if SHAPES_URP
	public class ShapesRenderFeature : ScriptableRendererFeature {

		public override void Create() => _ = 0; // called once per camera

		public override void AddRenderPasses( ScriptableRenderer renderer, ref RenderingData renderingData ) { // on pre render, called once per render
			Camera cam = renderingData.cameraData.camera;
#if UNITY_EDITOR
			if( ShouldSkipUnfocusedSceneView( cam ) )
				return;
#endif
			if( DrawCommand.cBuffersRendering.TryGetValue( cam, out List<DrawCommand> cmds ) ) {
				foreach( DrawCommand cmd in cmds ) {
					renderer.EnqueuePass( ObjectPool<ShapesRenderPass>.Alloc().Init( cmd ) );
				}
			}
		}

#if UNITY_EDITOR
		private static bool ShouldSkipUnfocusedSceneView( Camera cam ) {
			return !Application.isPlaying &&
			       cam != null &&
			       cam.cameraType != CameraType.Game &&
			       EditorWindow.focusedWindow == null;
		}
#endif

	}
	#endif

}

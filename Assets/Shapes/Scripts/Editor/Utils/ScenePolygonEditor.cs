using System;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

// Shapes © Freya Holmér - https://twitter.com/FreyaHolmer/
// Website & Documentation - https://acegikmo.com/shapes/
namespace Shapes {

	public class ScenePolygonEditor : SceneEditGizmos {

		static bool isEditing;

		ArcHandle arcHandleRadius = ShapesHandles.InitRadialHandle();
		ArcHandle arcHandleThicknessOuter = ShapesHandles.InitRadialHandle();
		ArcHandle arcHandleThicknessInner = ShapesHandles.InitRadialHandle();
		ArcHandle arcHandleAngle = ShapesHandles.InitAngularHandle();

		public ScenePolygonEditor( Editor parentEditor ) => this.parentEditor = parentEditor;

		protected override bool IsEditing {
			get => isEditing;
			set => isEditing = value;
		}

		Color GetHandleColor( RegularPolygon shape ) {
			return ShapesHandles.GetHandleColor( shape.Color );
		}

		static RegularPolygon currentSceneShape;

		static ThicknessSpace RadiusSpace => currentSceneShape.RadiusSpace;

		static ThicknessSpace ThicknessSpace => currentSceneShape.ThicknessSpace;

		static bool HasThickness => currentSceneShape.Border;

		static float Thickness {
			get => currentSceneShape.Thickness;
			set => currentSceneShape.Thickness = value;
		}

		static float Radius {
			get => currentSceneShape.Radius;
			set => currentSceneShape.Radius = value;
		}

		void DrawPolygon( float radius, int sides, float angleOffset ) {
			Vector3[] pts = new Vector3[sides + 1];
			float angStep = ShapesMath.TAU / sides;
			for( int i = 0; i <= sides; i++ ) {
				float ang = angleOffset + angStep * i;
				pts[i] = new Vector3( Mathf.Cos( ang ), Mathf.Sin( ang ), 0 ) * radius;
			}
			Handles.DrawPolyLine( pts );
		}

		public bool DoSceneHandles( RegularPolygon shape ) {
			if( IsEditing == false )
				return false;
			currentSceneShape = shape;
			if( RadiusSpace != ThicknessSpace.Meters )
				return false;

			bool holdingShift = ( Event.current.modifiers & EventModifiers.Shift ) != 0;
			bool editInnerOuterRadius = holdingShift;

			// set up matrix
			Vector3 rootDir = shape.transform.right;
			Vector3 discNormal = shape.transform.forward;
			Quaternion rot = Quaternion.LookRotation( rootDir, discNormal );
			Matrix4x4 mtx = Matrix4x4.TRS( shape.transform.position, rot, Vector3.one ); // todo: scale?

			bool changed = false;

			using( new Handles.DrawingScope( GetHandleColor( shape ), mtx ) ) {
				// thickness handles
				if( HasThickness && ThicknessSpace == ThicknessSpace.Meters ) {
					using( var chchk = new EditorGUI.ChangeCheckScope() ) {
						arcHandleThicknessOuter.radius = Radius + Thickness * 0.5f;
						arcHandleThicknessOuter.wireframeColor = Color.clear;
						DrawPolygon( arcHandleThicknessOuter.radius, shape.Sides, shape.Angle );
						arcHandleThicknessOuter.DrawHandle();
						if( chchk.changed ) {
							changed = true;
							Undo.RecordObject( shape, "edit polygon" );
							if( editInnerOuterRadius ) {
								float prevInnerRadius = Radius - Thickness * 0.5f;
								float newOuterRadius = arcHandleThicknessOuter.radius;
								Radius = ( prevInnerRadius + newOuterRadius ) / 2;
								Thickness = newOuterRadius - prevInnerRadius;
							} else {
								Thickness = ( arcHandleThicknessOuter.radius - Radius ) * 2;
							}
						}
					}

					// inner radius
					if( editInnerOuterRadius ) {
						using( var chchk = new EditorGUI.ChangeCheckScope() ) {
							arcHandleThicknessInner.radius = Radius - Thickness * 0.5f;
							arcHandleThicknessInner.wireframeColor = Color.clear;
							DrawPolygon( arcHandleThicknessInner.radius, shape.Sides, shape.Angle );
							arcHandleThicknessInner.DrawHandle();
							if( chchk.changed ) {
								changed = true;
								Undo.RecordObject( shape, "edit polygon" );
								float prevOuterRadius = Radius + Thickness * 0.5f;
								float newInnerRadius = arcHandleThicknessInner.radius;
								Radius = ( newInnerRadius + prevOuterRadius ) / 2;
								Thickness = prevOuterRadius - newInnerRadius;
							}
						}
					}
				}

				// radius handle
				using( var chchk = new EditorGUI.ChangeCheckScope() ) {
					arcHandleRadius.radius = Radius;
					arcHandleRadius.wireframeColor = Color.clear;
					DrawPolygon( arcHandleRadius.radius, shape.Sides, shape.Angle );
					arcHandleRadius.DrawHandle();
					if( chchk.changed ) {
						changed = true;
						Undo.RecordObject( shape, "edit polygon radius" );
						Radius = arcHandleRadius.radius;
					}
				}

				// angle handle
				using( var chchk = new EditorGUI.ChangeCheckScope() ) {
					arcHandleAngle.radius = Radius;
					arcHandleAngle.angle = shape.Angle * Mathf.Rad2Deg;
					if( HasThickness && ThicknessSpace == ThicknessSpace.Meters )
						arcHandleAngle.radius += Thickness / 2f;
					arcHandleAngle.wireframeColor = Color.clear;
					arcHandleAngle.radiusHandleSizeFunction = pos => 0f;
					arcHandleAngle.DrawHandle();
					if( chchk.changed ) {
						changed = true;
						Undo.RecordObject( shape, "edit polygon angle" );
						shape.Angle = arcHandleAngle.angle * Mathf.Deg2Rad;
					}
				}

			}

			return changed;
		}

	}

}

using System.Runtime.CompilerServices;
﻿using UnityEngine;

// Shapes © Freya Holmér - https://twitter.com/FreyaHolmer/
// Website & Documentation - https://acegikmo.com/shapes/
namespace Shapes {

	public class PolygonPath : PointPath<Vector2> {

		const MethodImplOptions INLINE = MethodImplOptions.AggressiveInlining;

		PolygonTriangulation lastUsedTriangulationMode = PolygonTriangulation.EarClipping;

		public PolygonPath() => _ = 0;
		
		public void AddPoint( float x, float y ) => AddPoint( new Vector2( x, y ) );


		#region BezierTo, ArcTo

		/// <summary>A cubic bezier curve, using the previous point as the starting point</summary>
		[MethodImpl( INLINE )] public void BezierTo( Vector2 startTangent, Vector2 endTangent, Vector2 end ) => BezierTo( startTangent, endTangent, end, ShapesConfig.Instance.polylineDefaultPointsPerTurn );

		/// <summary>A cubic bezier curve, using the previous point as the starting point. Number of points is given by density in number of points per full 360° turn</summary>
		public void BezierTo( Vector2 startTangent, Vector2 endTangent, Vector2 end, float pointsPerTurn ) {
			if( CheckCanAddContinuePoint() ) return;
			int pointCount = ShapesMath.GetBezierPointCount( LastPoint, startTangent, endTangent, end, pointsPerTurn );
			BezierTo( startTangent, endTangent, end, pointCount );
		}

		/// <summary>Adds points of a cubic bezier curve, using the previous point as the starting point</summary>
		public void BezierTo( Vector2 startTangent, Vector2 endTangent, Vector2 end, int pointCount ) {
			if( CheckCanAddContinuePoint() ) return;
			AddPoints( ShapesMath.CubicBezierPointsSkipFirst( LastPoint, startTangent, endTangent, end, pointCount ) );
		}

		/// <summary>Adds points of an arc wedged into the corner defined by the previous point, corner, and next, with the given point count</summary>
		[MethodImpl( INLINE )] public void ArcTo( Vector2 corner, Vector2 next, float radius, int pointCount ) => AddArcPoints( corner, next, radius, useDensity: false, pointCount, 0 );

		/// <summary>Adds points of an arc wedged into the corner defined by the previous point, corner, and next</summary>
		[MethodImpl( INLINE )] public void ArcTo( Vector2 corner, Vector2 next, float radius ) => AddArcPoints( corner, next, radius, useDensity: true, 0, ShapesConfig.Instance.polylineDefaultPointsPerTurn );

		/// <summary>Adds points of an arc wedged into the corner defined by the previous point, corner, and next, with the given point density in number of points per full 360° turn</summary>
		[MethodImpl( INLINE )] public void ArcTo( Vector2 corner, Vector2 next, float radius, float pointsPerTurn ) => AddArcPoints( corner, next, radius, useDensity: true, 0, pointsPerTurn );

		void AddArcPoints( Vector2 corner, Vector2 next, float radius, bool useDensity, int targetPointCount, float pointsPerTurn ) {
			if( radius <= 0.0001f ) {
				// radius is super small, just add the corner point
				AddPoint( corner );
				return; // pretty much just a straight line. only add the corner point
			}

			Vector2 tangentA = ( corner - LastPoint ).normalized;
			Vector2 tangentB = ( next - corner ).normalized;
			float dot = Vector2.Dot( tangentA, tangentB );

			if( dot > 0.999f ) {
				AddPoint( corner );
				return; // pretty much just a straight line. only add the corner point
			}

			Vector2 normA = ShapesMath.Rotate90CW( tangentA ); // normalized
			Vector2 normB = ShapesMath.Rotate90CW( tangentB );
			Vector2 cornerDir = ( normA + normB ).normalized;
			float cornerBDot = Vector2.Dot( cornerDir, normB );
			Vector2 center = corner + cornerDir * ( ( radius / cornerBDot ) );
			// calc count here if density based
			if( useDensity ) {
				float angTurn = Vector2.Angle( normA, normB ) / 360f;
				targetPointCount = Mathf.RoundToInt( angTurn * pointsPerTurn );
			}

			AddPoints( ShapesMath.GetArcPoints( -normA, -normB, center, radius, targetPointCount ) );
		}

		#endregion

		public bool EnsureMeshIsReadyToRender( PolygonTriangulation triangulation, out Mesh outMesh ) {
			if( meshDirty == false ) {
				// polygon itself didn't change, but the render state might force us to update
				if( triangulation != lastUsedTriangulationMode )
					meshDirty = true;
			}

			return base.EnsureMeshIsReadyToRender( out outMesh, () => { TryUpdateMesh( triangulation ); } );
		}

		void TryUpdateMesh( PolygonTriangulation triangulation ) {
			lastUsedTriangulationMode = triangulation;
			// todo: be smarter about this, maybe don't mesh.clear but check point count and whatnot
			ShapesMeshGen.GenPolygonMesh( base.mesh, path, triangulation );
		}


	}


}
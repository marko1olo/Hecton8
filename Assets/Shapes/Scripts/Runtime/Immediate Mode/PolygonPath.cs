using UnityEngine;

// Shapes © Freya Holmér - https://twitter.com/FreyaHolmer/
// Website & Documentation - https://acegikmo.com/shapes/
namespace Shapes {

	public class PolygonPath : PointPath<Vector2> {

		PolygonTriangulation lastUsedTriangulationMode = PolygonTriangulation.EarClipping;

		public PolygonPath() => _ = 0;
		
		public void AddPoint( float x, float y ) => AddPoint( new Vector2( x, y ) );


		// todo: don't make this duplicate code pls
		// unify these functions for PolygonPath and PolylinePath

		#region BezierTo, ArcTo

		/// <summary>
		/// Adds points of a cubic bezier curve, using the previous point as the starting point
		/// </summary>
		public void BezierTo( Vector2 startTangent, Vector2 endTangent, Vector2 end, int pointCount ) {
			if( CheckCanAddContinuePoint() ) return;
			AddPoints( ShapesMath.CubicBezierPointsSkipFirst( LastPoint, startTangent, endTangent, end, pointCount ) );
		}

		/// <summary>
		/// A cubic bezier curve, using the previous point as the starting point. Number of points is given by density in number of points per full 360° turn
		/// </summary>
		public void BezierTo( Vector2 startTangent, Vector2 endTangent, Vector2 end, float pointsPerTurn ) {
			if( CheckCanAddContinuePoint() ) return;
			int pointCount = ShapesMath.CalcBezierPointCount( LastPoint, startTangent, endTangent, end, pointsPerTurn );
			BezierTo( startTangent, endTangent, end, pointCount );
		}

		/// <summary>
		/// Adds points of an arc wedged into the corner defined by the previous point, corner, and next, with the given point density in number of points per full 360° turn
		/// </summary>
		public void ArcTo( Vector2 corner, Vector2 next, float radius, float pointsPerTurn ) {
			if( CheckCanAddContinuePoint() ) return;
			AddArcPoints( corner, next, radius, useDensity: true, 0, pointsPerTurn );
		}

		/// <summary>
		/// Adds points of an arc wedged into the corner defined by the previous point, corner, and next, with the given point count
		/// </summary>
		public void ArcTo( Vector2 corner, Vector2 next, float radius, int pointCount ) {
			if( CheckCanAddContinuePoint() ) return;
			AddArcPoints( corner, next, radius, useDensity: false, pointCount, 0 );
		}

		/// <summary>
		/// Adds points of an arc wedged into the corner defined by the previous point, corner, and next
		/// </summary>
		public void ArcTo( Vector2 corner, Vector2 next, float radius ) {
			if( CheckCanAddContinuePoint() ) return;
			AddArcPoints( corner, next, radius, useDensity: true, 0, ShapesConfig.Instance.polylineDefaultPointsPerTurn );
		}

		/// <summary>
		/// Adds points of an arc wedged into the corner defined by the previous point, corner, and next, with the given point density in number of points per full 360° turn
		/// </summary>
		public void ArcTo( Vector2 corner, Vector2 next, float radius, float pointsPerTurn, Color color ) {
			if( CheckCanAddContinuePoint() ) return;
			AddArcPoints( corner, next, radius, useDensity: true, 0, pointsPerTurn );
		}

		void AddArcPoints( Vector2 corner, Vector2 next, float radius, bool useDensity, int targetPointCount, float pointsPerTurn ) {
			if( ShapesMath.GetArcParameters( LastPoint, corner, next, radius, out Vector2 center, out Vector2 normA, out Vector2 normB, out float angTurn ) ) {
				if( useDensity )
					targetPointCount = Mathf.RoundToInt( angTurn * pointsPerTurn );
				AddPoints( ShapesMath.GetArcPoints( -normA, -normB, center, radius, targetPointCount ) );
			} else {
				AddPoint( corner );
			}
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
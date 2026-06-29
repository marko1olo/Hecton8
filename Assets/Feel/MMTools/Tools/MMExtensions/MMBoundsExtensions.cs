using UnityEngine;
using System.Collections;

namespace MoreMountains.Tools
{	
	/// <summary>
	/// Bounds helpers
	/// </summary>
	public class MMBoundsExtensions : MonoBehaviour 
	{
		/// <summary>
		/// Returns a random point within the bounds set as parameter
		/// </summary>
		/// <param name="bounds"></param>
		/// <returns></returns>
		public static Vector3 MMRandomPointInBounds(Bounds bounds)
		{
			return new Vector3(
				Random.Range(bounds.min.x, bounds.max.x),
				Random.Range(bounds.min.y, bounds.max.y),
				Random.Range(bounds.min.z, bounds.max.z)
			);
		}

		/// <summary>
		/// Gets collider bounds for an object (from Collider2D)
		/// </summary>
		/// <param name="theObject"></param>
		/// <returns></returns>
		public static Bounds GetColliderBounds(GameObject theObject)
		{
			// if the object has a collider at root level, we base our calculations on that
			if (theObject.TryGetComponent(out Collider collider))
			{
				return collider.bounds;
			}

			// if the object has a collider2D at root level, we base our calculations on that
			if (theObject.TryGetComponent(out Collider2D collider2D))
			{
				return collider2D.bounds;
			}

			// if the object contains at least one Collider we'll add all its children's Colliders bounds
			Collider[] colliders = theObject.GetComponentsInChildren<Collider>();
			if (colliders.Length > 0)
			{
				Bounds totalBounds = colliders[0].bounds;
				foreach (Collider col in colliders) 
				{
					totalBounds.Encapsulate(col.bounds);
				}
				return totalBounds;
			}

			// if the object contains at least one Collider2D we'll add all its children's Collider2Ds bounds
			Collider2D[] colliders2D = theObject.GetComponentsInChildren<Collider2D>();
			if (colliders2D.Length > 0)
			{
				Bounds totalBounds = colliders2D[0].bounds;
				foreach (Collider2D col in colliders2D)
				{
					totalBounds.Encapsulate(col.bounds);
				}
				return totalBounds;
			}

			return new Bounds(Vector3.zero, Vector3.zero);
		}

		/// <summary>
		/// Gets bounds of a renderer
		/// </summary>
		/// <param name="theObject"></param>
		/// <returns></returns>
		public static Bounds GetRendererBounds(GameObject theObject)
		{
			// if the object has a renderer at root level, we base our calculations on that
			if (theObject.TryGetComponent(out Renderer renderer))
			{
				return renderer.bounds;
			}

			// if the object contains at least one renderer we'll add all its children's renderer bounds
			Renderer[] renderers = theObject.GetComponentsInChildren<Renderer>();
			if (renderers.Length > 0)
			{
				Bounds totalBounds = renderers[0].bounds;
				foreach (Renderer rnd in renderers)
				{
					totalBounds.Encapsulate(rnd.bounds);
				}
				return totalBounds;
			}

			return new Bounds(Vector3.zero, Vector3.zero);
		}
	}
}
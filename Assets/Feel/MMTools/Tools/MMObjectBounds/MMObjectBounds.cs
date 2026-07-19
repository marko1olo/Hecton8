using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;

namespace MoreMountains.Tools
{
	[AddComponentMenu("More Mountains/Tools/Object Bounds/MMObjectBounds")]
	public class MMObjectBounds : MonoBehaviour
	{
		public enum WaysToDetermineBounds { Collider, Collider2D, Renderer, Undefined }

		[Header("Bounds")]
		public WaysToDetermineBounds BoundsBasedOn;  


		public virtual Vector3 Size { get; set; }

		/// <summary>
		protected Renderer _cachedRenderer;
		protected Collider _cachedCollider;
		protected Collider2D _cachedCollider2D;

		protected virtual void Awake()
		{
			TryGetComponent(out _cachedRenderer);
			TryGetComponent(out _cachedCollider);
			TryGetComponent(out _cachedCollider2D);
		}

		/// <summary>
		/// When this component is added we define its bounds.
		/// </summary>
		protected virtual void Reset() 
		{
			DefineBoundsChoice();
		}

		/// <summary>
		/// Tries to determine automatically what the bounds should be based on.
		/// In this order, it'll keep the last found of these : Collider2D, Collider or Renderer.
		/// If none of these is found, it'll be set as Undefined.
		/// </summary>
		protected virtual void DefineBoundsChoice()
		{
			BoundsBasedOn = WaysToDetermineBounds.Undefined;
			if (_cachedRenderer != null || TryGetComponent(out _cachedRenderer))
			{
				BoundsBasedOn = WaysToDetermineBounds.Renderer;
			}
			if (_cachedCollider != null || TryGetComponent(out _cachedCollider))
			{
				BoundsBasedOn = WaysToDetermineBounds.Collider;
			}
			if (_cachedCollider2D != null || TryGetComponent(out _cachedCollider2D))
			{
				BoundsBasedOn = WaysToDetermineBounds.Collider2D;
			}
		}

		/// <summary>
		/// Returns the bounds of the object, based on what has been defined
		/// </summary>
		public virtual Bounds GetBounds()
		{
			if (BoundsBasedOn==WaysToDetermineBounds.Renderer)
			{
				if (_cachedRenderer == null && !TryGetComponent(out _cachedRenderer))
				{
					throw new Exception("The PoolableObject "+gameObject.name+" is set as having Renderer based bounds but no Renderer component can be found.");
				}
				return _cachedRenderer.bounds;
			}

			if (BoundsBasedOn==WaysToDetermineBounds.Collider)
			{
				if (_cachedCollider == null && !TryGetComponent(out _cachedCollider))
				{
					throw new Exception("The PoolableObject "+gameObject.name+" is set as having Collider based bounds but no Collider component can be found.");
				}
				return _cachedCollider.bounds;
			}

			if (BoundsBasedOn==WaysToDetermineBounds.Collider2D)
			{
				if (_cachedCollider2D == null && !TryGetComponent(out _cachedCollider2D))
				{
					throw new Exception("The PoolableObject "+gameObject.name+" is set as having Collider2D based bounds but no Collider2D component can be found.");
				}
				return _cachedCollider2D.bounds;
			}

			return new Bounds(Vector3.zero,Vector3.zero);
		}



	}
}
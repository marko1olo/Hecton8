
using UnityEngine;

namespace Technie.PhysicsCreator
{
	/*
	 *	Unified logging point for all of Technie Collider Creator
	 *	Turn on/off logging via IS_DEBUG_OUTPUT_ENABLED
	 * 
	 *  Example usage:
	 *		Console.output.Log("Hello world");
	 *		Console.output.LogWarning(Console.Technie, "Something not right");
	 */
	public static class Console
	{
		// Turn on/off console logging
		public const bool IS_DEBUG_OUTPUT_ENABLED = false;

		// Make the shadow hierarchy (which draws the hull colour overlays) visible in the regular Unity hierachy
		public const bool SHOW_SHADOW_HIERARCHY = false;

		// Enable experimental joint support
		public const bool ENABLE_JOINT_SUPPORT = false;

		// Tag for LogWarning / LogError
		public static string Technie = "Technie.PhysicsCreator";

		public static Logger output = new Logger(Debug.unityLogger.logHandler);

		static Console()
		{
			output.logEnabled = IS_DEBUG_OUTPUT_ENABLED;
		}
	}
}


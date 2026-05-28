// Amplify Impostors
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

using System;
using UnityEngine;

namespace AmplifyImpostors
{
	[Serializable]
	public class VersionInfo
	{
		public const byte Major = 0;
		public const byte Minor = 9;
		public const byte Release = 9;
		public static byte Revision = 3;
		private const string BaseVersionLabel = "0.9.9";

		public static string StaticToString()
		{
			return Revision > 0 ? BaseVersionLabel + "." + Revision : BaseVersionLabel;
		}

		public static int FullNumber { get { return Major * 10000 + Minor * 1000 + Release * 100 + Revision; } }
		public static string FullLabel { get { return "Version=" + FullNumber; } }
	}
}

using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using UnityEngine.Profiling;
using UnityEditor;

using Den.Tools;
using Den.Tools.GUI;
using Den.Tools.GUI.Popup;
using MapMagic.Core;
using MapMagic.Nodes;
using MapMagic.Nodes.GUI;

namespace MapMagic.Nodes.GUI
{
	public static class PortalSelectorPopup
	{
		private static GUIStyle itemTextStyle;

		public static void DrawPortalSelector (Graph graph, IPortalExit<object> portalExit)
		{
			Type exitType = null;
			foreach (Type i in portalExit.GetType().GetInterfaces())
				if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPortalExit<>))
					{ exitType = i.GetGenericArguments()[0]; break; }

			if (itemTextStyle == null)
			{
				itemTextStyle = new GUIStyle(UnityEditor.EditorStyles.label); 
				itemTextStyle.normal.textColor = itemTextStyle.focused.textColor = itemTextStyle.active.textColor = Color.black;
			}
			
			List<Item> enterPortalsItems = new List<Item>();

			for (int g=0; g<graph.generators.Length; g++)
			{
				IPortalEnter<object> portalEnter = graph.generators[g] as IPortalEnter<object>;
				if (portalEnter == null) continue;
				Type enterType = null;
				foreach (Type i in portalEnter.GetType().GetInterfaces())
					if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPortalEnter<>))
						{ enterType = i.GetGenericArguments()[0]; break; }

				if (enterType != exitType) continue;

				Item item = new Item( portalEnter.Name, 
					onDraw: (i, r) => EditorGUI.LabelField(r, i.name, itemTextStyle),
					onClick: ()=>
					{ 
						if (graph.AreDependent((Generator)portalExit, (Generator)portalEnter))   
							{ EditorUtility.DisplayDialog("MapMagic", "Linking portals this way will create a dependency loop.", "Cancel"); return; }
					
						portalExit.AssignEnter(portalEnter, graph);

						GraphWindow.current?.RefreshMapMagic();
					} );

				enterPortalsItems.Add(item);
			}

			PopupMenu menu = new PopupMenu() { items=enterPortalsItems };
			menu.Show(Event.current.mousePosition);

		}

	}
}
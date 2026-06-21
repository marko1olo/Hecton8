using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Runtime.InteropServices;

namespace Den.Tools.GUI
{
	public class Undo
	{
    	public UnityEngine.Object undoObject;
		public string undoName;
		public Action undoAction;
		public string lastUndoName;  //the last undo group name (kept to know it on UndoRedoPerformed)

		public Undo ()
		{
			UnityEditor.Undo.undoRedoPerformed -= OnUndoRedoPerformed;
			UnityEditor.Undo.undoRedoPerformed += OnUndoRedoPerformed;
		}


		public void OnUndoRedoPerformed ()
		{
			bool undoMatched = false;

			if (undoObject != null)
			{
				Type type = undoObject.GetType();
				System.Reflection.FieldInfo curField = type.GetField("curUndoId");
				System.Reflection.FieldInfo prevField = type.GetField("prevUndoId");

				if (curField != null && prevField != null)
				{
					int cur = (int)curField.GetValue(undoObject);
					int prev = (int)prevField.GetValue(undoObject);

					if (cur != prev)
					{
						prevField.SetValue(undoObject, cur);
						undoMatched = true;
					}
				}
			}

			if (!undoMatched)
			{
				string currGroupName = UnityEditor.Undo.GetCurrentGroupName();

				if (currGroupName == undoName || currGroupName == lastUndoName)
				// a bit hacky here. On undoRedoPerformed there is already no current group in stack, and no way to get current group name
				// so we store previous (before mm change) name and performing undo if this name is first in stack
				{
					undoMatched = true;

					if (currGroupName == lastUndoName)
						lastUndoName = null;
				}
			}

			if (undoMatched)
			{
				EditorWindow.focusedWindow?.Repaint();
				undoAction?.Invoke();
			}
		}

		public void Record (bool completeUndo=false)
		{
			if (undoObject==null) return;

			if (completeUndo) UnityEditor.Undo.RegisterCompleteObjectUndo(undoObject, undoName);
			else UnityEditor.Undo.RecordObject(undoObject, undoName);

			Type type = undoObject.GetType();
			System.Reflection.FieldInfo curField = type.GetField("curUndoId");
			if (curField != null)
			{
				int cur = (int)curField.GetValue(undoObject);
				cur++;
				curField.SetValue(undoObject, cur);
			}

			string currGroupName = UnityEditor.Undo.GetCurrentGroupName();
			if (currGroupName != undoName)
				lastUndoName = currGroupName;

			EditorUtility.SetDirty(undoObject);
		}

		public void SetDirty ()
		{
			EditorUtility.SetDirty(undoObject);
		}
	}
}

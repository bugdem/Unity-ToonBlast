using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameEngine.Core
{
	[CustomEditor(typeof(TileManager))]
    public class TileManagerEditor : Editor
    {
		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			var tileManager = target as TileManager;

			if (GUILayout.Button("Level Editor"))
			{
				LevelWindow.OpenWindow(tileManager);
			}
		}
	}
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameEngine.Util
{
    public static class GEExtensions
    {
		public static void RemoveAllChild(this GameObject go, bool immediate = false)
		{
			for (var i = go.transform.childCount - 1; i >= 0; i--)
			{
				if (immediate) GameObject.DestroyImmediate(go.transform.GetChild(i).gameObject);
				else GameObject.Destroy(go.transform.GetChild(i).gameObject);
			}
		}
	}
}
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
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

		public static void SafeAdd<TKey, TValue>(this NativeHashMap<TKey, TValue> hashMap, TKey key, TValue value) 
																						where TKey : unmanaged, IEquatable<TKey>
																						where TValue : unmanaged, IEquatable<TValue>
		{
			if (hashMap.ContainsKey(key)) hashMap[key] = value;
			else hashMap.Add(key, value);
		}
	}
}
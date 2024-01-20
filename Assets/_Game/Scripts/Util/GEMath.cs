using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameEngine.Util
{
    public static class GEMath
    {        
		public readonly struct int2
        {
			public static readonly Unity.Mathematics.int2 up = new Unity.Mathematics.int2(0, 1);
			public static readonly Unity.Mathematics.int2 down = new Unity.Mathematics.int2(0, -1);
			public static readonly Unity.Mathematics.int2 left = new Unity.Mathematics.int2(-1, 0);
			public static readonly Unity.Mathematics.int2 right = new Unity.Mathematics.int2(1, 0);
		}

		public readonly struct float3
		{
			public static readonly Unity.Mathematics.float3 up = new Unity.Mathematics.float3(0f, 1f, 0f);
			public static readonly Unity.Mathematics.float3 down = new Unity.Mathematics.float3(0f, -1f, 0f);
			public static readonly Unity.Mathematics.float3 left = new Unity.Mathematics.float3(-1f, 0f, 0f);
			public static readonly Unity.Mathematics.float3 right = new Unity.Mathematics.float3(1f, 0f, 0f);
			public static readonly Unity.Mathematics.float3 forward = new Unity.Mathematics.float3(0f, 0f, 1f);
			public static readonly Unity.Mathematics.float3 back = new Unity.Mathematics.float3(0f, 0f, -1f);
		}

		// Usable for 2D grid neighbourhoods.
		// This is used for grids starting from top-left.
		// Grid increases left to right (0,0), (0, 1), (0,2) ...
		// Grid increases top to bottom (0,0), (1, 0), (2,0) ...
		public readonly struct grid2n
		{
			public static readonly Unity.Mathematics.int2 up = new Unity.Mathematics.int2(-1, 0);
			public static readonly Unity.Mathematics.int2 down = new Unity.Mathematics.int2(1, 0);
			public static readonly Unity.Mathematics.int2 left = new Unity.Mathematics.int2(0, -1);
			public static readonly Unity.Mathematics.int2 right = new Unity.Mathematics.int2(0, 1);
		}
	}

	public static class MathExtension
	{
		public static T Clamp<T>(this T val, T min, T max) where T : IComparable<T>
		{
			if (val.CompareTo(min) < 0) return min;
			else if (val.CompareTo(max) > 0) return max;
			else return val;
		}

		public static T ClampMin<T>(this T val, T min) where T : IComparable<T>
		{
			if (val.CompareTo(min) < 0) return min;
			else return val;
		}

		public static T ClampMax<T>(this T val, T max) where T : IComparable<T>
		{
			if (val.CompareTo(max) > 0) return max;
			else return val;
		}
	}
}
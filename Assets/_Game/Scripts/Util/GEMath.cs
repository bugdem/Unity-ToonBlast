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
}
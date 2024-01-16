using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameEngine.Core
{
	[Serializable]
	public class TileData
	{
		public BlockType BlockType;
		public CubeColor CubeColor;
		public int Iteration;
	}

	[Serializable]
	public class TileColumn
	{
		public List<TileData> Rows;
	}

	[CreateAssetMenu(fileName = "Level", menuName = "Game Engine/Level")]
	public class LevelData : ScriptableObject
    {
		public LevelAssetPack AssetPack;
		public int Row = 10;
		public int Col = 10;
		public List<TileColumn> Tiles;
    }
}
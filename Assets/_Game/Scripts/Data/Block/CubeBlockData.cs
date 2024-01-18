using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Entities.Serialization;
using UnityEngine;

namespace GameEngine.Core
{
	[Serializable]
	public enum CubeColor : ushort
	{
		Random = 0,
		Blue,
		Green,
		Pink,
		Purple,
		Red,
		Yellow
	}

	[Serializable]
	public class CubeBlockAsset : BlockAsset { }

	[CreateAssetMenu(fileName = "Cube", menuName = "Game Engine/Blocks/Cube")]
    public class CubeBlockData : BlockDataGen<CubeBlockAsset>
    {
		[Header("Cube Block")]
		[SerializeField] private CubeColor _color;

		public override BlockType Type => BlockType.Cube;
		public CubeColor Color => _color;
	}
}
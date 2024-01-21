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

	public static partial class Extensions
	{
		public static Color GetColor(this CubeColor type)
		{
			switch (type)
			{
				case CubeColor.Blue: return new Color(102f / 255f, 178f / 255f, 255f / 255f, 1f);
				case CubeColor.Green: return Color.green;
				case CubeColor.Pink: return new Color(255f / 255f, 105f / 255f, 180f / 255f, 1f);
				case CubeColor.Purple: return new Color(143f / 255f, 45f / 255f, 194f / 255f, 1f);
				case CubeColor.Red: return Color.red;
				case CubeColor.Yellow: return Color.yellow;
			}

			return Color.white;
		}
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
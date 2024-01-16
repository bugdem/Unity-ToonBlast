using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
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

	[CreateAssetMenu(fileName = "Cube", menuName = "Game Engine/Blocks/Cube")]
    public class CubeBlockData : BlockData
    {
		[SerializeField] private CubeColor _color;
		[SerializeField] private Sprite _defaultIcon;
		[SerializeField] private List<Sprite> _conditionalIcons;

		public override BlockType Type => BlockType.Cube;
		public CubeColor Color => _color;
		public int ConditionCount => _conditionalIcons.Count;

		public Sprite GetDefaultIcon() => _defaultIcon;
		public Sprite GetConditionalIcon(int conditionIndex)
		{
			if (conditionIndex < 0) return _defaultIcon;
			return _conditionalIcons[Mathf.Clamp(conditionIndex, 0, _conditionalIcons.Count - 1)];
		}
	}
}
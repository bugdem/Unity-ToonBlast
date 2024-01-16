using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameEngine.Core
{
	[CreateAssetMenu(fileName = "Layered", menuName = "Game Engine/Blocks/Layered")]
	public class LayeredBlockData : BlockData
	{
		[SerializeField] private Sprite _defaultIcon;
		[SerializeField] private List<Sprite> _layeredIcons;

		public override BlockType Type => BlockType.Layered;
		public int IterationCount => _layeredIcons.Count + 1;

		public Sprite GetDefaultIcon() => _defaultIcon;
		public Sprite GetLayeredIcon(int iteration)
		{
			if (iteration <= 0 || _layeredIcons.Count == 0)
				return _defaultIcon;

			return _layeredIcons[Mathf.Clamp(iteration, 1, _layeredIcons.Count - 1)];
		}
	}
}
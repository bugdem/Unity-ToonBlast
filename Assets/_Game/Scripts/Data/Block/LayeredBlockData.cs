using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameEngine.Core
{
	[Serializable]
	public class LayeredBlockAsset : BlockAsset { }

	[CreateAssetMenu(fileName = "Layered", menuName = "Game Engine/Blocks/Layered")]
	public class LayeredBlockData : BlockDataGen<LayeredBlockAsset>
	{
		public override BlockType Type => BlockType.Layered;
	}
}
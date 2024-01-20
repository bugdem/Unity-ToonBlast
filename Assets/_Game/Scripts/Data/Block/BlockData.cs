using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameEngine.Core
{
    [Serializable]
    public enum BlockType : ushort
	{
        Cube = 0,
        Layered
    }

    public static partial class Extensions
    {
        public static bool CanBeTouchedByDefault(this BlockType type)
        {
            return type != BlockType.Layered;
        }

        public static bool CanBeDamaged(this BlockType type)
        {
            return type == BlockType.Layered;
        }

        public static bool IsMovable (this BlockType type)
        {
            return type != BlockType.Layered;
        }
    }

    [Serializable]
    public class BlockAsset
    {
        public GameObject Prefab;
        public Sprite Icon;
    }

    public abstract class BlockData : ScriptableObject
    {
        [Header("Block")]
        [SerializeField] private string _title;

		public abstract BlockType Type { get; }
		public string Title => _title;
	}

	public abstract class BlockDataGen<T> : BlockData where T : BlockAsset
	{
		[SerializeField] private List<T> _assets;
		public ushort AssetCount => (ushort)_assets.Count;

		public T GetAsset(ushort index)
		{
			return _assets[Mathf.Clamp(index, 0, _assets.Count - 1)];
		}
	}
}
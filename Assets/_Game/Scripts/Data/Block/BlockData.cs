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

    public abstract class BlockData : ScriptableObject
    {
        [SerializeField] private string _title;

		public abstract BlockType Type { get; }
		public string Title => _title;
    }
}
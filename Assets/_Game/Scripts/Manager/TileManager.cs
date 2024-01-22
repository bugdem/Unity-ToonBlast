using GameEngine.Util;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameEngine.Core
{
    public class TileManager : Singleton<TileManager>
    {
        [SerializeField] private Transform _tileContainer;
        [SerializeField] private Transform _borderBackground;

#if UNITY_EDITOR
        [SerializeField] private BlockEditor _blockEditorPrefab;
        public BlockEditor BlockEditorPrefab => _blockEditorPrefab;
#endif
        public Transform BorderBackground => _borderBackground;
        public Transform TileContainer => _tileContainer;
        public const float TILE_BLOCK_SIZE = .4f;
	}
}
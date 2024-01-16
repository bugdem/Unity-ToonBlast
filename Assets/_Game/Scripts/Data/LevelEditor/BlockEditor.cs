using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameEngine.Core
{
    public class BlockEditor : MonoBehaviour
    {
        public SpriteRenderer IconRenderer;
        [HideInInspector] public TileData Data;
        [HideInInspector] public Vector2Int GridIndex;
    }
}
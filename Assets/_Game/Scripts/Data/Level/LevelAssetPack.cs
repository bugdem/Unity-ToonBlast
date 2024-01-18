using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameEngine.Core
{
    [CreateAssetMenu(fileName = "AssetPack", menuName = "Game Engine/Asset Pack")]
    public class LevelAssetPack : ScriptableObject
    {
        public int Id;
        public List<BlockData> BlockAssets;
    }
}
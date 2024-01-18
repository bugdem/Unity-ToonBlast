using GameEngine.Util;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameEngine.Core
{
    public class ProgressManager : Singleton<ProgressManager>
    {
        public int GetLevelIndex()
        {
            return PlayerPrefs.GetInt("CurrentLevelIndex", 0);
        }
    }
}
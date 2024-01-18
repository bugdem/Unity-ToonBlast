using GameEngine.Util;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameEngine.Core
{
    public class LevelManager : Singleton<LevelManager>
    {
        [SerializeField] private List<LevelData> _levels;

		public int CurrentLevelIndex { get; private set; }
		public LevelData CurrentLevelData => _levels[CurrentLevelIndex];

		private void Start()
		{
			// Get level index from player progress.
			CurrentLevelIndex = ProgressManager.Instance.GetLevelIndex() % _levels.Count;

			// Notify entity bridge to start entity systems.
			EntityBridge.Instance.PrepareLevelData();
		}
	}
}
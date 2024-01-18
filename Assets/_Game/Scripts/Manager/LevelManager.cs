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

			TileManager.Instance.BorderBackground.gameObject.SetActive(true);
			var borderTransform = TileManager.Instance.BorderBackground;
			borderTransform.transform.position = TileManager.Instance.TileContainer.transform.position + Vector3.up * TileManager.TILE_BLOCK_SIZE;
			borderTransform.transform.localScale = new Vector3(CurrentLevelData.Col * TileManager.TILE_BLOCK_SIZE + 0.05f, CurrentLevelData.Row * TileManager.TILE_BLOCK_SIZE + 0.15f, 1f);

			// Notify entity bridge to start entity systems.
			EntityBridge.Instance.PrepareLevelData();
		}
	}
}
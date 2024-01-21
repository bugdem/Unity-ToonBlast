using GameEngine.Util;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GameEngine.Core
{
	// Used for initializing current level details into systems to be used on DOTS ECS side.
    public class EntityBridge : Singleton<EntityBridge>
    {
		public bool IsLevelDataPrepared { get; private set; }
		public LevelData CurrentLevelData => LevelManager.Instance.CurrentLevelData;

		public void PrepareLevelData()
        {
            if (IsLevelDataPrepared) return;

            IsLevelDataPrepared = true;

			var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
			var commandBuffer = new EntityCommandBuffer(Allocator.Temp);

			// Create level config entity with current level data.
			var spawnerEntity = commandBuffer.CreateEntity();
			commandBuffer.SetName(spawnerEntity, "_LevelConfig");
			commandBuffer.AddComponent<LevelConfig>(spawnerEntity);
			commandBuffer.SetComponent<LevelConfig>(spawnerEntity, new LevelConfig
			{
				AssetPackId = CurrentLevelData.AssetPack.Id,
				LevelIndex = LevelManager.Instance.CurrentLevelIndex,
				GridCenter = TileManager.Instance.TileContainer.transform.position,
				GridSize = new int2(CurrentLevelData.Col, CurrentLevelData.Row),
				GridForward = TileManager.Instance.TileContainer.transform.forward
			});
			var gridTiles = commandBuffer.AddBuffer<LevelTileData>(spawnerEntity);
			for (int y = 0; y < CurrentLevelData.Col; y++)
			{
				for (int x = 0; x < CurrentLevelData.Row; x++)
				{
					var tileData = CurrentLevelData.Tiles[y].Rows[x];
					gridTiles.Add(new LevelTileData
					{
						AssetTitle = tileData.AssetTitle ?? string.Empty,
						BlockType = tileData.BlockType,
						CubeColor = tileData.CubeColor,
						AssetIndex = tileData.AssetIndex,
						GridIndex = new int2(x, y)
					});
				}
			}

			// Create available list that will be used by tile spawner system.
			var availableColors = commandBuffer.AddBuffer<LevelConfigCubeAvailableColor>(spawnerEntity);
			foreach (var color in CurrentLevelData.AvailableColors)
				availableColors.Add(new LevelConfigCubeAvailableColor { CubeColor = color });

			commandBuffer.Playback(entityManager);
			commandBuffer.Dispose();
		}
	}

	public struct LevelConfig: IComponentData
	{
		public int LevelIndex;
		public int AssetPackId;
		public float3 GridCenter;
		public float3 GridForward;
		public int2 GridSize;
	}

	public struct LevelTileData : IBufferElementData
	{
		public FixedString64Bytes AssetTitle;
		public int2 GridIndex;
		public BlockType BlockType;
		public CubeColor CubeColor;
		public int AssetIndex;
	}

	public struct LevelConfigCubeAvailableColor : IBufferElementData
	{
		public CubeColor CubeColor;
	}

	public struct LevelTile : IComponentData
	{
		public FixedString64Bytes AssetTitle;
		public int2 GridIndex;
		public BlockType BlockType;
		public CubeColor CubeColor;
		public int AssetIndex;

		public LevelTile Clone()
		{
			return new LevelTile
			{
				AssetIndex = AssetIndex,
				GridIndex = GridIndex,
				BlockType = BlockType,
				CubeColor = CubeColor,
				AssetTitle = AssetTitle
			};
		}
	}

	public struct CanBeTouched : IComponentData, IEnableableComponent { }
	public struct IsMoving : IComponentData, IEnableableComponent 
	{
		public int2 Target;
	}
}


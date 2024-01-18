using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace GameEngine.Core
{
    public partial struct LevelInitializeSystem : ISystem
    {
		private static LevelAssetPackConfigHash _levelAssetPackConfigHash;
		private static NativeArray<CubeColor> _availableCubeColors;

		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<LevelConfig>();
			state.RequireForUpdate<LevelTileData>();
			state.RequireForUpdate<LevelAssetPackConfig>();
		}

		public void OnUpdate(ref SystemState state)
		{
			state.Enabled = false;

			var levelConfig = SystemAPI.GetSingleton<LevelConfig>();
			var levelTiles = SystemAPI.GetSingletonBuffer<LevelTileData>();
			var availableColors = SystemAPI.GetSingletonBuffer<LevelConfigCubeAvailableColor>();

			// To fast access for future references to assets, cache current level's assets to native hash.
			foreach (var (assetPackConfig, cubeBlocks, layeredBlocks) in
						SystemAPI.Query<RefRO<LevelAssetPackConfig>, DynamicBuffer<LevelAssetCubeBlockData>, DynamicBuffer<LevelAssetLayeredBlockData>>())
			{
				if (assetPackConfig.ValueRO.Id == assetPackConfig.ValueRO.Id)
				{
					_levelAssetPackConfigHash = new LevelAssetPackConfigHash
					{
						Id = assetPackConfig.ValueRO.Id,
						CubeAssets = new NativeHashMap<LevelAssetCubeBlockKey, Entity>(cubeBlocks.Length, Allocator.Persistent),
						LayeredAssets = new NativeHashMap<LevelAssetLayeredBlockKey, Entity>(layeredBlocks.Length, Allocator.Persistent)
					};

					foreach (var cubeBlockAsset in cubeBlocks)
						_levelAssetPackConfigHash.CubeAssets.Add(cubeBlockAsset.Key, cubeBlockAsset.Prefab);

					foreach (var layeredBlockAsset in layeredBlocks)
						_levelAssetPackConfigHash.LayeredAssets.Add(layeredBlockAsset.Key, layeredBlockAsset.Prefab);

					break;
				}
			}

			_availableCubeColors = new NativeArray<CubeColor>(availableColors.Length, Allocator.Persistent);
			for (int index = 0; index < availableColors.Length; index++)
				_availableCubeColors[index] = availableColors[index].CubeColor;

			var random = new Random((uint)DateTime.Now.Millisecond);
			var ecb = new EntityCommandBuffer(Allocator.Temp);

			for (int column = 0; column < levelConfig.GridSize.x; column++)
			{
				for (int row = 0; row < levelConfig.GridSize.y; row++)
				{
					var tileData = levelTiles[column * levelConfig.GridSize.x + row];
					if (tileData.BlockType == BlockType.Cube && tileData.CubeColor == CubeColor.Random)
					{
						tileData.CubeColor = _availableCubeColors[random.NextInt(0, _availableCubeColors.Length)];
					}

					var entityTileData = new LevelTile
					{
						AssetTitle = tileData.AssetTitle,
						BlockType = tileData.BlockType,
						CubeColor = tileData.CubeColor,
						GridIndex = tileData.GridIndex,
						AssetIndex = tileData.AssetIndex
					};
					var entityLocalTransform = new LocalTransform
					{
						Position = GetTileWorldPosition(tileData.GridIndex, levelConfig.GridSize.y, levelConfig.GridSize.x) + levelConfig.GridCenter,
						Scale = TileSize,
						Rotation = quaternion.identity
					};

					var tileEntity = ecb.Instantiate(GetBlockAssetPrefab(entityTileData));
					ecb.SetName(tileEntity, $"Tile ({tileData.GridIndex.x},{tileData.GridIndex.y})");
					ecb.AddComponent<LevelTile>(tileEntity, entityTileData);
					ecb.SetComponent<LocalTransform>(tileEntity, entityLocalTransform);

					UnityEngine.Debug.Log($"System Cube : {tileData.GridIndex}");
				}
			}

			ecb.Playback(state.EntityManager);
		}

		public float3 GetTileWorldPosition(int2 gridIndex, int rowCount, int columnCount)
		{
			Vector3 localPosition = new Vector2(gridIndex.y * TileSize + TileSize * .5f,
						 -gridIndex.x * TileSize + TileSize * .5f
						);
			Vector3 worldPosition = localPosition - new Vector3(columnCount * TileSize, -rowCount * TileSize, 0f) * .5f;
			worldPosition += Vector3.forward * gridIndex.x * 0.01f;
			return worldPosition;
		}

		public float TileSize => TileManager.TILE_BLOCK_SIZE;

		private Entity GetBlockAssetPrefab(LevelTile tile)
		{
			if (tile.BlockType == BlockType.Cube)
				return _levelAssetPackConfigHash.CubeAssets[new LevelAssetCubeBlockKey { AssetIndex = tile.AssetIndex, Color = tile.CubeColor }];
			else if (tile.BlockType == BlockType.Layered)
				return _levelAssetPackConfigHash.LayeredAssets[new LevelAssetLayeredBlockKey { AssetIndex = tile.AssetIndex, Title = tile.AssetTitle }];

			return Entity.Null;
		}
	}
}
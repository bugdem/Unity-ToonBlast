using GameEngine.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace GameEngine.Core
{
	public struct LevelInitializeEvent : IComponentData { }

	public partial struct LevelInitializeSystem : ISystem
	{
		// Immutable, set only once on start of this system.
		private static LevelConfig _levelConfig;
		private static LevelAssetPackConfigHash _levelAssetPackConfigHash;
		private static NativeArray<CubeColor> _availableCubeColors;

		// Mutable, may change later by other system logics.
		private static NativeHashMap<int2, Entity> _gridEntities;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<LevelConfig>();
			state.RequireForUpdate<LevelTileData>();
			state.RequireForUpdate<LevelAssetPackConfig>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			state.Enabled = false;

			_levelConfig = SystemAPI.GetSingleton<LevelConfig>();

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

			// Cache available cube colors for random cubes.
			_availableCubeColors = new NativeArray<CubeColor>(availableColors.Length, Allocator.Persistent);
			for (int index = 0; index < availableColors.Length; index++)
				_availableCubeColors[index] = availableColors[index].CubeColor;

			var random = new Random((uint)DateTime.Now.Millisecond);
			var ecb = new EntityCommandBuffer(Allocator.Temp);

			_gridEntities = new NativeHashMap<int2, Entity>(_levelConfig.GridSize.x * _levelConfig.GridSize.y, Allocator.Persistent);

			// Create cube entities with level data.
			for (int column = 0; column < _levelConfig.GridSize.x; column++)
			{
				for (int row = 0; row < _levelConfig.GridSize.y; row++)
				{
					var tileData = levelTiles[column * _levelConfig.GridSize.y + row];

					// Replace random cubes with cubes from available colors.
					if (tileData.BlockType == BlockType.Cube && tileData.CubeColor == CubeColor.Random)
						tileData.CubeColor = _availableCubeColors[random.NextInt(0, _availableCubeColors.Length)];

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
						Position = GetTileWorldPosition(tileData.GridIndex, _levelConfig.GridSize.y, _levelConfig.GridSize.x) + _levelConfig.GridCenter,
						Scale = TileSize,
						Rotation = quaternion.identity
					};

					var tileEntity = ecb.Instantiate(GetBlockAssetPrefab(entityTileData));
					ecb.SetName(tileEntity, $"Tile ({tileData.GridIndex.x},{tileData.GridIndex.y})");
					ecb.AddComponent<LevelTile>(tileEntity, entityTileData);
					ecb.SetComponent<LocalTransform>(tileEntity, entityLocalTransform);
					ecb.AddComponent<CanBeTouched>(tileEntity);
					ecb.SetComponentEnabled<CanBeTouched>(tileEntity, entityTileData.BlockType.CanBeTouchedByDefault());

					// Cannot add before Entity Command Buffer is not played back.
					// _gridEntities.Add(entityTileData.GridIndex, tileEntity);
				}
			}

			var initCompletedEntity = ecb.CreateEntity();
			ecb.AddComponent<LevelInitializeEvent>(initCompletedEntity);

			ecb.Playback(state.EntityManager);

			// Cache entities with grid index.
			foreach (var (tileData, entity) in
						SystemAPI.Query<RefRO<LevelTile>>().WithEntityAccess())
			{
				_gridEntities.Add(tileData.ValueRO.GridIndex, entity);
			}
		}

		public static float3 GetTileWorldPosition(int2 gridIndex, int rowCount, int columnCount)
		{	// (1,1) => localPos : (1.5, -.5), worldPos = (.5, .5)
			Vector3 localPosition = new Vector2(gridIndex.y * TileSize + TileSize * .5f,
						 -gridIndex.x * TileSize + TileSize * .5f
						);
			Vector3 worldPosition = localPosition - new Vector3(columnCount * TileSize, -rowCount * TileSize, 0f) * .5f;
			worldPosition += Vector3.forward * gridIndex.x * 0.01f;
			return worldPosition;
		}

		public static int2 GetTileGridIndexFromWorldPosition(float3 worldPosition)
		{
			float3 gridTopLeftPoint = _levelConfig.GridCenter + new float3(-_levelConfig.GridSize.x * TileSize, _levelConfig.GridSize.y * TileSize, 0f) * .5f + new float3(0f, TileSize, 0f);

			// GameEngine.Util.GEDebug.DrawPoint(worldPosition, Color.black, .25f, 2f);
			// GameEngine.Util.GEDebug.DrawPoint(gridTopLeftPoint, Color.red, .25f, 5f);

			float3 distanceFromTouch = worldPosition - gridTopLeftPoint;
			return new int2(Mathf.FloorToInt((-distanceFromTouch.y) / TileSize), Mathf.FloorToInt((distanceFromTouch.x) / TileSize));
		}

		public static float TileSize => TileManager.TILE_BLOCK_SIZE;

		public static Entity GetBlockAssetPrefab(LevelTile tile)
		{
			if (tile.BlockType == BlockType.Cube)
				return _levelAssetPackConfigHash.CubeAssets[new LevelAssetCubeBlockKey { AssetIndex = tile.AssetIndex, Color = tile.CubeColor }];
			else if (tile.BlockType == BlockType.Layered)
				return _levelAssetPackConfigHash.LayeredAssets[new LevelAssetLayeredBlockKey { AssetIndex = tile.AssetIndex, Title = tile.AssetTitle }];

			return Entity.Null;
		}

		public static Entity GetEntityFromRay(Ray ray)
		{
			new Plane(_levelConfig.GridForward, _levelConfig.GridCenter).Raycast(ray, out float enter);
			float3 rayWorldPosition = ray.GetPoint(enter);
			int2 gridIndex = GetTileGridIndexFromWorldPosition(rayWorldPosition);

			_gridEntities.TryGetValue(gridIndex, out Entity entity);

			return entity;
		}

		// Returns indexes by checking borders. It does not check if they are available for matching or other things.
		public static void GetNeighbours(int2 gridIndex, ref NativeList<int2> neighbours)
		{
			neighbours.Clear();

			if ((gridIndex + GEMath.grid2n.left).y >= 0) neighbours.Add(gridIndex + GEMath.grid2n.left);
			if ((gridIndex + GEMath.grid2n.right).y < _levelConfig.GridSize.x) neighbours.Add(gridIndex + GEMath.grid2n.right);
			if ((gridIndex + GEMath.grid2n.down).x < _levelConfig.GridSize.y) neighbours.Add(gridIndex + GEMath.grid2n.down);
			if ((gridIndex + GEMath.grid2n.up).x >= 0) neighbours.Add(gridIndex + GEMath.grid2n.up);
		}

		public static Entity GetEntity(int2 gridIndex)
		{
			if (_gridEntities.TryGetValue(gridIndex, out Entity entity)) 
				return entity;

			return Entity.Null;
		}

		public static int2 GridSize => _levelConfig.GridSize;

		public static void RemoveEntityFromCache(int2 gridIndex)
		{
			if (_gridEntities.ContainsKey(gridIndex))
				_gridEntities.Remove(gridIndex);
		}
	}

	public struct EntityTile : IEquatable<EntityTile>
	{
		public int2 GridIndex;
		public Entity Entity;

		public bool Equals(EntityTile other)
		{
			return GridIndex.Equals(other.GridIndex);
		}
	}
}
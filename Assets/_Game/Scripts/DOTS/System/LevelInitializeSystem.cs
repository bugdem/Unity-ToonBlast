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
	public struct UpdateMatchEvent : IComponentData, IEnableableComponent { }

	public abstract class BurstLevelConfig
	{
		public static readonly SharedStatic<LevelConfig> LevelConfigField =
			SharedStatic<LevelConfig>.GetOrCreate<BurstLevelConfig, LevelConfigFieldKey>();
		private class LevelConfigFieldKey { }
	}

	public abstract class BurstLevelAssetPackConfigHash
	{
		public static readonly SharedStatic<LevelAssetPackConfigHash> Field =
			SharedStatic<LevelAssetPackConfigHash>.GetOrCreate<BurstLevelAssetPackConfigHash, LevelAssetPackConfigHashFieldKey>();
		private class LevelAssetPackConfigHashFieldKey { }
	}

	public abstract class BurstAvailableColors
	{
		public static readonly SharedStatic<NativeArray<CubeColor>> Field =
			SharedStatic<NativeArray<CubeColor>>.GetOrCreate<BurstAvailableColors, AvailableColorsFieldKey>();
		private class AvailableColorsFieldKey { }
	}

	public abstract class BurstRandom
	{
		public static readonly SharedStatic<Random> Field =
			SharedStatic<Random>.GetOrCreate<BurstRandom, RandomFieldKey>();
		private class RandomFieldKey { }
	}

	[UpdateBefore(typeof(TileMoveSystem))]
	public partial struct LevelInitializeSystem : ISystem
	{
		// Immutable, set only once on start of this system.
		private static LevelConfig _levelConfig { get => BurstLevelConfig.LevelConfigField.Data; set => BurstLevelConfig.LevelConfigField.Data = value; }
		private static LevelAssetPackConfigHash _levelAssetPackConfigHash { get => BurstLevelAssetPackConfigHash.Field.Data; set => BurstLevelAssetPackConfigHash.Field.Data = value; }


		// These values defines match group rules. Lowest tile count to consider it as a match is first index of this list.
		public static readonly FixedList32Bytes<byte> CubeGroupConditions = new FixedList32Bytes<byte> { 2, 3, 5, 7 };
		public static readonly FixedList32Bytes<MatchCount> MatchSpawnCountForShuffle = new FixedList32Bytes<MatchCount> { 
																							new MatchCount { GridTileCount = 10, SpawnCount = 1 },
																							new MatchCount { GridTileCount = 20, SpawnCount = 2 },
																							new MatchCount { GridTileCount = 50, SpawnCount = 3 },
																							new MatchCount { GridTileCount = 70, SpawnCount = 4 },
																							new MatchCount { GridTileCount = 100, SpawnCount = 5 },
																						};
		public static int2 GridSize => _levelConfig.GridSize;

		public struct MatchCount
		{
			public ushort GridTileCount;
			public byte SpawnCount;
		}

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
			BurstAvailableColors.Field.Data = new NativeArray<CubeColor>(availableColors.Length, Allocator.Persistent);
			for (int index = 0; index < availableColors.Length; index++)
				BurstAvailableColors.Field.Data[index] = availableColors[index].CubeColor;

			BurstRandom.Field.Data = new Random((uint)(SystemAPI.Time.ElapsedTime * 100 + 1).ClampMin(1));

			var ecb = new EntityCommandBuffer(Allocator.Temp);

			// Create cube entities with level data.
			for (int column = 0; column < _levelConfig.GridSize.x; column++)
			{
				for (int row = 0; row < _levelConfig.GridSize.y; row++)
				{
					var tileData = levelTiles[column * _levelConfig.GridSize.y + row];

					var entityTileData = new LevelTile
					{
						AssetTitle = tileData.AssetTitle,
						BlockType = tileData.BlockType,
						CubeColor = tileData.CubeColor,
						GridIndex = tileData.GridIndex,
						AssetIndex = tileData.AssetIndex
					};

					CreateTileEntity(entityTileData, ref ecb);

					// Cannot add before Entity Command Buffer is not played back.
					// _gridEntities.Add(entityTileData.GridIndex, tileEntity);
				}
			}

			var initCompletedEntity = ecb.CreateEntity();
			ecb.SetName(initCompletedEntity, "_LevelInitialize");
			ecb.AddComponent<LevelInitializeEvent>(initCompletedEntity);
			ecb.AddComponent<UpdateMatchEvent>(initCompletedEntity);
			ecb.SetComponentEnabled<UpdateMatchEvent>(initCompletedEntity, true);

			ecb.Playback(state.EntityManager);
		}

		public static Entity CreateTileEntity(LevelTile entityTileData, ref EntityCommandBuffer ecb)
		{
			// Replace random cubes with cubes from available colors.
			if (entityTileData.BlockType == BlockType.Cube && entityTileData.CubeColor == CubeColor.Random)
				entityTileData.CubeColor = BurstAvailableColors.Field.Data[BurstRandom.Field.Data.NextInt(0, BurstAvailableColors.Field.Data.Length)];

			var entityLocalTransform = new LocalTransform
			{
				Position = GetTileWorldPosition(entityTileData.GridIndex),
				Scale = TileSize,
				Rotation = quaternion.identity
			};

			var tileEntity = ecb.Instantiate(GetBlockAssetPrefab(entityTileData));
			ecb.SetName(tileEntity, $"Tile ({entityTileData.GridIndex.x},{entityTileData.GridIndex.y})");
			ecb.AddComponent<LevelTile>(tileEntity, entityTileData);
			ecb.SetComponent<LocalTransform>(tileEntity, entityLocalTransform);
			ecb.AddComponent<CanBeTouched>(tileEntity);
			ecb.SetComponentEnabled<CanBeTouched>(tileEntity, entityTileData.BlockType.CanBeTouchedByDefault());
			ecb.AddComponent<IsMoving>(tileEntity);
			ecb.SetComponentEnabled<IsMoving>(tileEntity, false);
			return tileEntity;
		}

		public static float3 GetTileWorldPosition(int2 gridIndex)
		{
			return GetTileWorldPosition(gridIndex, GridSize.y, GridSize.x);
		}

		public static float3 GetTileWorldPosition(int2 gridIndex, int rowCount, int columnCount)
		{	// (1,1) => localPos : (1.5, -.5), worldPos = (.5, .5)
			Vector3 localPosition = new Vector2(gridIndex.y * TileSize + TileSize * .5f,
						 -gridIndex.x * TileSize + TileSize * .5f
						);
			Vector3 worldPosition = localPosition - new Vector3(columnCount * TileSize, -rowCount * TileSize, 0f) * .5f;
			worldPosition += new Vector3(_levelConfig.GridCenter.x, _levelConfig.GridCenter.y, _levelConfig.GridCenter.z);
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

		public static bool HasBlockAssetPrefab(LevelTile tile)
		{
			if (tile.BlockType == BlockType.Cube) return _levelAssetPackConfigHash.CubeAssets.ContainsKey(new LevelAssetCubeBlockKey { AssetIndex = tile.AssetIndex, Color = tile.CubeColor });
			else if (tile.BlockType == BlockType.Layered) return _levelAssetPackConfigHash.LayeredAssets.ContainsKey(new LevelAssetLayeredBlockKey { AssetIndex = tile.AssetIndex, Title = tile.AssetTitle });

			return false;
		}

		public static int2 GetGridIndexFromRay(Ray ray)
		{
			new Plane(_levelConfig.GridForward, _levelConfig.GridCenter).Raycast(ray, out float enter);
			float3 rayWorldPosition = ray.GetPoint(enter);
			int2 gridIndex = GetTileGridIndexFromWorldPosition(rayWorldPosition);

			return gridIndex;
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

		public static int FallPointGridOffsetY => -3;
	}

	public struct EntityTile : IEquatable<EntityTile>
	{
		public LevelTile TileData;
		public Entity Entity;

		public bool Equals(EntityTile other)
		{
			return TileData.GridIndex.Equals(other.TileData.GridIndex);
		}
	}
}
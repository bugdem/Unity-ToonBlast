using GameEngine.Util;
using System;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace GameEngine.Core
{
	[UpdateBefore(typeof(TransformSystemGroup))]
	[UpdateAfter(typeof(TileMoveSystem))]
	public partial struct TileMatchGroupSystem : ISystem, ISystemStartStop
	{
		private static MatchAllGroup _matchAllGroup;

		private static bool _hasUpdatedTilesThisFrame;
		private static NativeHashMap<int2, Entity> _tiles;

		private NativeList<int2> _frontierTmp;
		private NativeList<int2> _reachedTmp;
		private NativeList<int2> _neighboursTmp;
		private NativeList<EntityTile> _foundTilesTmp;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<LevelInitializeEvent>();
		}

		[BurstCompile]
		public void OnStartRunning(ref SystemState state)
		{
			_matchAllGroup = new MatchAllGroup
			{
				GroupIds = new NativeHashMap<int2, int>(1, Allocator.Persistent),
				Groups = new NativeList<MatchGroup>(1, Allocator.Persistent)
			};

			_tiles = new NativeHashMap<int2, Entity>(1, Allocator.Persistent);

			_frontierTmp = new NativeList<int2>(Allocator.Persistent);
			_reachedTmp = new NativeList<int2>(Allocator.Persistent);
			_neighboursTmp = new NativeList<int2>(Allocator.Persistent);
			_foundTilesTmp = new NativeList<EntityTile>(Allocator.Persistent);
		}

		[BurstCompile]
		public void OnStopRunning(ref SystemState state)
		{
			DisposeAll();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			_hasUpdatedTilesThisFrame = false;

			Entity touchedEntity = Entity.Null;

			foreach (var touchInput in SystemAPI.Query<RefRO<InputTouch>>())
			{
				// Get grid index from touch to find entity.
				var touchGridIndex = LevelInitializeSystem.GetGridIndexFromRay(touchInput.ValueRO.Ray);
				touchedEntity = GetGridTile(ref state, touchGridIndex);
			}

			bool needsUpdateMatchGroups = false;
			if (touchedEntity != Entity.Null && SystemAPI.IsComponentEnabled<CanBeTouched>(touchedEntity))
			{
				var tileData = SystemAPI.GetComponent<LevelTile>(touchedEntity);
				Debug.Log("Touched Entity!: " + tileData.GridIndex);

				if (tileData.BlockType == BlockType.Cube)
				{
					if (_matchAllGroup.GroupIds.TryGetValue(tileData.GridIndex, out int matchGroupIndex))
					{
						var matchGroup = _matchAllGroup.Groups[matchGroupIndex];
						BlastTiles(ref state, ref matchGroup.Matches);

						needsUpdateMatchGroups = true;
					}
				}
			}

			foreach (var (updateMatchGroup, entity) in
				SystemAPI.Query<EnabledRefRW<UpdateMatchEvent>>().WithEntityAccess())
			{
				updateMatchGroup.ValueRW = false;
				needsUpdateMatchGroups = true;

				// Cannot call UpdateAllMatches here as it causes structural changes while iterating in SystemAPI.Query
			}

			if (needsUpdateMatchGroups)
			{
				UpdateAllMatches(ref state);
			}
		}

		private void DisposeAll()
		{
			DisposeChildNatives();

			_matchAllGroup.GroupIds.Dispose();
			_matchAllGroup.Groups.Dispose();
			_matchAllGroup = default;

			_tiles.Dispose();
			_frontierTmp.Dispose();
			_reachedTmp.Dispose();
			_neighboursTmp.Dispose();
			_foundTilesTmp.Dispose();
		}

		private void DisposeChildNatives()
		{
			_matchAllGroup.GroupIds.Clear();
			for (int index = _matchAllGroup.Groups.Length - 1; index >= 0; index--)
			{
				_matchAllGroup.Groups[index].Matches.Dispose();
				_matchAllGroup.Groups.RemoveAt(index);
			}
		}

		private void UpdateAllMatches(ref SystemState state)
		{
			// Dispose and clear data.
			DisposeChildNatives();

			int matchGroupIndex = 0;

			var tilesToReplace = new NativeList<TileReplace>(Allocator.Temp);

			// var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.DefaultGameObjectInjectionWorld.Unmanaged);
			// var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

			foreach (var (levelTile, canBeTouched, tileEntity) in
						SystemAPI.Query<RefRO<LevelTile>, EnabledRefRO<CanBeTouched>>().WithAll<CanBeTouched>().WithEntityAccess())
			{
				if (levelTile.ValueRO.BlockType == BlockType.Cube && !_matchAllGroup.GroupIds.ContainsKey(levelTile.ValueRO.GridIndex))
				{
					FindMatch(ref state, levelTile.ValueRO, tileEntity, ref _frontierTmp, ref _reachedTmp, ref _neighboursTmp, ref _foundTilesTmp);

					if (_foundTilesTmp.Length >= LevelInitializeSystem.CubeGroupConditions[0])
					{
						// We have match group.
						var matchGroup = new MatchGroup
						{
							Matches = new NativeList<int2>(_foundTilesTmp.Length, Allocator.Persistent)
						};

						int newAssetIndex = 0;
						// Find new block asset index with conditional group type.
						for (int index = LevelInitializeSystem.CubeGroupConditions.Length - 1; index >= 1; index--)
						{
							if (_foundTilesTmp.Length >= LevelInitializeSystem.CubeGroupConditions[index])
							{
								// Asset to replace is found.
								newAssetIndex = index;
								break;
							}
						}

						foreach (var tileInMatchGroup in _foundTilesTmp)
						{
							matchGroup.Matches.Add(tileInMatchGroup.TileData.GridIndex);
							_matchAllGroup.GroupIds.Add(tileInMatchGroup.TileData.GridIndex, matchGroupIndex);

							// If current asset index is different than new one, add list to replace assets.
							if (tileInMatchGroup.TileData.AssetIndex != newAssetIndex)
							{
								var newTileData = tileInMatchGroup.TileData.Clone();
								newTileData.AssetIndex = newAssetIndex;

								tilesToReplace.Add(new TileReplace
								{
									NewTileData = newTileData,
									EntityToDestroy = tileInMatchGroup.Entity
								});
							}
						}

						_matchAllGroup.Groups.Add(matchGroup);
						matchGroupIndex++;
					}
					// There is no match, revert asset to standart one.
					else if (levelTile.ValueRO.AssetIndex != 0)
					{
						var newTileData = levelTile.ValueRO.Clone();
						newTileData.AssetIndex = 0;

						tilesToReplace.Add(new TileReplace
						{
							NewTileData = newTileData,
							EntityToDestroy = tileEntity
						});
					}
				}
			}

			// var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.DefaultGameObjectInjectionWorld.Unmanaged);
			var ecb = new EntityCommandBuffer(Allocator.Temp);
			foreach (var tileToReplace in tilesToReplace)
			{
				// Replace entity with new asset and copy components.
				ReplaceGridTile(ref state, tileToReplace, ref ecb);
			}
			ecb.Playback(state.EntityManager);
		}

		// 1. Destroy match group
		// 2. Find lowest row on modified columns
		// 3. Find upper tiles to move down
		// 4. Generate new tiles
		private void BlastTiles(ref SystemState state, ref NativeList<int2> tiles)
		{
			// Holds bottom row index for each column
			// Key: Column, Value: Lowest row
			var blastXY = new NativeHashMap<int, int>(1, Allocator.Temp);

			// To prevent multiple generation of same damagable tiles, cache replaced ones.
			var replacedDamagableTiles = new NativeHashSet<int2>(1, Allocator.Temp);

			// Destroy match group and find lowest rows on modified columns.
			var ecb = new EntityCommandBuffer(Allocator.Temp);
			for (int index = tiles.Length - 1; index >= 0; index--)
			{
				var tileIndex = tiles[index];
				int lowestRow = -1;
				if (blastXY.TryGetValue(tileIndex.y, out int previousLowestRow))
					lowestRow = previousLowestRow;

				if (tileIndex.x > lowestRow)
					blastXY.SafeAdd(tileIndex.y, tileIndex.x);

				// Look for neighbour damagable tiles around blasting group.
				FindDamagable(ref state, tileIndex, ref _neighboursTmp, ref _foundTilesTmp);
				for (int damagableIndex = _foundTilesTmp.Length - 1;  damagableIndex >= 0; damagableIndex--)
				{
					var damagable = _foundTilesTmp[damagableIndex];
					damagable.TileData.AssetIndex++;
					// If there is next damaged asset layer, replace it.
					if (LevelInitializeSystem.HasBlockAssetPrefab(damagable.TileData) && !replacedDamagableTiles.Contains(damagable.TileData.GridIndex))
					{
						ReplaceGridTile(ref state, new TileReplace
						{
							EntityToDestroy = damagable.Entity,
							NewTileData = damagable.TileData
						}, ref ecb);

						replacedDamagableTiles.Add(damagable.TileData.GridIndex);
					}
					// This was last layer for damagable, remove from grid.
					else
					{
						lowestRow = -1;
						if (blastXY.TryGetValue(damagable.TileData.GridIndex.y, out int damagablePreviousLowestRow))
							lowestRow = damagablePreviousLowestRow;

						if (damagable.TileData.GridIndex.x > lowestRow)
							lowestRow = damagable.TileData.GridIndex.x;

						// There is a case: when a damagable is destroyed and there are already empty cells below it
						// , above tiles would only fall until damagable's level.
						// So we need to check if there is any empty cells below this damagable object.
						while (lowestRow + 1 < LevelInitializeSystem.GridSize.y)
						{
							if (GetGridTile(ref state, new int2(lowestRow + 1, damagable.TileData.GridIndex.y)) == Entity.Null)
								lowestRow++;
							else break;
						}
						
						blastXY.SafeAdd(damagable.TileData.GridIndex.y, lowestRow);

						DestroyGridTile(ref state, damagable.TileData.GridIndex, ref ecb);
					}
				}

				DestroyGridTile(ref state, tileIndex, ref ecb);
			}
			ecb.Playback(state.EntityManager);

			var ecbNewTile = new EntityCommandBuffer(Allocator.Temp);

			// Move upwards from lowest row on column.
			foreach (var blastKVPair in blastXY)
			{
				int newTileCountToGenerate = 0;
				int lastRowToMove = blastKVPair.Value;
				int column = blastKVPair.Key;
				int bottomMostRow = blastKVPair.Value;

				for (int row = bottomMostRow; row >= 0; row --)
				{
					var tileAtRow = GetGridTile(ref state, new int2(row, column));
					if (tileAtRow == Entity.Null)
					{
						newTileCountToGenerate++;
						continue;
					}

					var tileData = SystemAPI.GetComponent<LevelTile>(tileAtRow);
					// Current tile may already in target position, so check it.
					if (tileData.BlockType.IsMovable() && !(tileData.GridIndex.x == column && tileData.GridIndex.y == lastRowToMove))
					{
						tileData.GridIndex = new int2(lastRowToMove, column);

						// Mark this tile as moving to make it movable by TileMoveSystem.
						SystemAPI.SetComponentEnabled<IsMoving>(tileAtRow, true);
						SystemAPI.SetComponent<IsMoving>(tileAtRow, new IsMoving { Target = tileData.GridIndex });
						SystemAPI.SetComponentEnabled<CanBeTouched>(tileAtRow, false);
						SystemAPI.SetComponent<LevelTile>(tileAtRow, tileData);
;
						lastRowToMove--;
					}
					else
					{
						newTileCountToGenerate = 0;
						lastRowToMove = row - 1;
					}
				}

				if (newTileCountToGenerate > 0)
				{
					for (int index = newTileCountToGenerate - 1; index >= 0; index --)
					{
						int2 spawnIndex = new int2(LevelInitializeSystem.FallPointGridOffsetY - (newTileCountToGenerate - index - 1), column);
						int2 targetIndex = new int2(index, column);
						var newTileData = new LevelTile
						{
							AssetIndex = 0,
							BlockType = BlockType.Cube,
							CubeColor = CubeColor.Random,
							GridIndex = targetIndex
						};
						var newEntity = LevelInitializeSystem.CreateTileEntity(newTileData, ref ecbNewTile);
						ecbNewTile.SetComponentEnabled<CanBeTouched>(newEntity, false);
						ecbNewTile.SetComponentEnabled<IsMoving>(newEntity, true);
						ecbNewTile.SetComponent<IsMoving>(newEntity, new IsMoving { Target = targetIndex });
						ecbNewTile.SetComponent<LocalTransform>(newEntity, new LocalTransform
						{
							Position = LevelInitializeSystem.GetTileWorldPosition(spawnIndex),
							Scale = LevelInitializeSystem.TileSize,
							Rotation = quaternion.identity
						});
					}
				}
			}

			ecbNewTile.Playback(state.EntityManager);
		}

		#region Tile Methods
		private void UpdateGridTiles(ref SystemState state)
		{
			_tiles.Clear();
			// Cache entities with grid index.
			foreach (var (tileData, entity) in
						SystemAPI.Query<RefRO<LevelTile>>().WithEntityAccess())
			{
				_tiles.Add(tileData.ValueRO.GridIndex, entity);
			}
		}

		private Entity GetGridTile(ref SystemState state, int2 gridIndex)
		{
			if (!_hasUpdatedTilesThisFrame) UpdateGridTiles(ref state);

			if (_tiles.TryGetValue(gridIndex, out Entity tileEntity))
				return tileEntity;

			return Entity.Null;
		}

		private void DestroyGridTile(ref SystemState state, int2 gridIndex)
		{
			var ecb = new EntityCommandBuffer(Allocator.Temp);
			DestroyGridTile(ref state, gridIndex, ref ecb);
		}

		private void DestroyGridTile(ref SystemState state, int2 gridIndex, ref EntityCommandBuffer ecb)
		{
			ecb.DestroyEntity(GetGridTile(ref state, gridIndex));

			_tiles.Remove(gridIndex);
		}

		private void ReplaceGridTile(ref SystemState state, TileReplace tileToReplace, ref EntityCommandBuffer ecb)
		{
			var canBeTouched = SystemAPI.IsComponentEnabled<CanBeTouched>(tileToReplace.EntityToDestroy);
			var isMoving = SystemAPI.IsComponentEnabled<IsMoving>(tileToReplace.EntityToDestroy);
			var moveTarget = SystemAPI.GetComponent<IsMoving>(tileToReplace.EntityToDestroy).Target;

			// ecb.DestroyEntity(tileToReplace.EntityToDestroy);
			ecb.DestroyEntity(GetGridTile(ref state, tileToReplace.NewTileData.GridIndex));
			// DestroyGridTile(ref state, tileToReplace.NewTileData.GridIndex, ref ecb);

			var newEntity = LevelInitializeSystem.CreateTileEntity(tileToReplace.NewTileData, ref ecb);
			ecb.SetComponentEnabled<CanBeTouched>(newEntity, canBeTouched);
			ecb.SetComponentEnabled<IsMoving>(newEntity, isMoving);
			ecb.SetComponent<IsMoving>(newEntity, new IsMoving { Target = moveTarget });
		}
		#endregion

		#region Find Tiles
		private void FindMatch(ref SystemState state, int2 gridIndex, ref NativeList<EntityTile> foundTiles)
		{
			var frontier = new NativeList<int2>(Allocator.Temp);
			var reached = new NativeList<int2>(Allocator.Temp);
			var neighbours = new NativeList<int2>(Allocator.Temp);

			FindMatch(ref state, gridIndex, ref frontier, ref reached, ref neighbours, ref foundTiles);
		}

		private void FindMatch(ref SystemState state, int2 gridIndex
						, ref NativeList<int2> frontier, ref NativeList<int2> reached
						, ref NativeList<int2> neighbours, ref NativeList<EntityTile> foundTiles)
		{
			var entity = GetGridTile(ref state, gridIndex);
			var tileData = SystemAPI.GetComponent<LevelTile>(entity);

			FindMatch(ref state, tileData, entity, ref frontier, ref reached, ref neighbours, ref foundTiles);
		}

		private void FindMatch(ref SystemState state, LevelTile startEntityTileData, Entity startEntity, ref NativeList<EntityTile> foundTiles)
		{
			var frontier = new NativeList<int2>(Allocator.Temp);
			var reached = new NativeList<int2>(Allocator.Temp);
			var neighbours = new NativeList<int2>(Allocator.Temp);

			FindMatch(ref state, startEntityTileData, startEntity, ref frontier, ref reached, ref neighbours, ref foundTiles);
		}

		private void FindMatch(ref SystemState state, LevelTile startEntityTileData, Entity startEntity
								, ref NativeList<int2> frontier, ref NativeList<int2> reached
								, ref NativeList<int2> neighbours, ref NativeList<EntityTile> foundTiles)
		{
			frontier.Clear();
			reached.Clear();
			neighbours.Clear();
			foundTiles.Clear();

			frontier.Add(startEntityTileData.GridIndex);
			reached.Add(startEntityTileData.GridIndex);
			foundTiles.Add(new EntityTile
			{
				Entity = startEntity,
				TileData = startEntityTileData
			});

			var searchingCubeColor = startEntityTileData.CubeColor;

			while (frontier.Length > 0)
			{
				var current = frontier[0];
				frontier.RemoveAt(0);

				LevelInitializeSystem.GetNeighbours(current, ref neighbours);
				foreach (var nextNeighbour in neighbours)
				{
					if (reached.Contains(nextNeighbour)) continue;

					var neighbourEntity = GetGridTile(ref state, nextNeighbour);
					if (neighbourEntity != Entity.Null)
					{
						var neighbourTileData = SystemAPI.GetComponent<LevelTile>(neighbourEntity);
						if (SystemAPI.IsComponentEnabled<CanBeTouched>(neighbourEntity)
							&& neighbourTileData.BlockType == BlockType.Cube
							&& neighbourTileData.CubeColor == searchingCubeColor)
						{
							foundTiles.Add(new EntityTile
							{
								TileData = neighbourTileData,
								Entity = neighbourEntity
							});
							frontier.Add(nextNeighbour);
						}
					}

					reached.Add(nextNeighbour);
				}
			}
		}

		private void FindDamagable(ref SystemState state, int2 gridIndex,
									ref NativeList<int2> neighbours, ref NativeList<EntityTile> foundTiles)
		{
			neighbours.Clear();
			foundTiles.Clear();

			LevelInitializeSystem.GetNeighbours(gridIndex, ref neighbours);
			foreach (var nextNeighbour in neighbours)
			{
				var neighbourEntity = GetGridTile(ref state, nextNeighbour);
				if (neighbourEntity != Entity.Null)
				{
					var neighbourTileData = SystemAPI.GetComponent<LevelTile>(neighbourEntity);
					if (neighbourTileData.BlockType.CanBeDamaged())
					{
						var entityTile = new EntityTile
						{
							TileData = neighbourTileData,
							Entity = neighbourEntity
						};
						foundTiles.Add(entityTile);
					}
				}
			}
		}
		#endregion
	}

	public struct TileReplace
	{
		public LevelTile NewTileData;
		public Entity EntityToDestroy;
	}

	public struct MatchAllGroup
	{
		public NativeList<MatchGroup> Groups;
		public NativeHashMap<int2, int> GroupIds;
	}

	public struct MatchGroup
	{
		public NativeList<int2> Matches;
	}
}
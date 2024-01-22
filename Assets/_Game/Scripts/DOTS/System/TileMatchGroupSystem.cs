using GameEngine.Util;
using System;
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
	[UpdateBefore(typeof(TransformSystemGroup))]
	[UpdateAfter(typeof(TileMoveSystem))]
	public partial struct TileMatchGroupSystem : ISystem, ISystemStartStop
	{
		private bool _hasUpdatedTilesThisFrame;
		private MatchAllGroup _matchAllGroup;
		private NativeHashMap<int2, Entity> _tiles;

		private NativeHashSet<int2> _cubeTilesForShuffle;
		private bool _shuffleNextFrame;

		// Temporary cache for finding match gropus.
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
			_cubeTilesForShuffle = new NativeHashSet<int2>(1, Allocator.Persistent);

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

			if (_shuffleNextFrame)
			{
				Shuffle(ref state);
				return;
			}

			// Get players input touch and blast if touched on any match group.
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
				// Get touched entity info.
				var tileData = SystemAPI.GetComponent<LevelTile>(touchedEntity);
				Debug.Log($"Touched Entity: {tileData.GridIndex}, Color: {tileData.CubeColor}");

				if (tileData.BlockType == BlockType.Cube)
				{
					// If there is any match group with touch tile, blast it.
					if (_matchAllGroup.GroupIds.TryGetValue(tileData.GridIndex, out int matchGroupIndex))
					{
						var matchGroup = _matchAllGroup.Groups[matchGroupIndex];
						int matchCount = matchGroup.Matches.Length;
						BlastTiles(ref state, ref matchGroup.Matches);

						needsUpdateMatchGroups = true;

						GEDebug.LogColored($"Blast({matchCount})! Condition: {tileData.AssetIndex}, Color: {tileData.CubeColor}", tileData.CubeColor.GetColor());
					}
				}
			}
			//

			// Check if there is any match group update event triggered by other systems.
			foreach (var (updateMatchGroup, entity) in
				SystemAPI.Query<EnabledRefRW<UpdateMatchEvent>>().WithEntityAccess())
			{
				updateMatchGroup.ValueRW = false;
				needsUpdateMatchGroups = true;
				// Cannot call UpdateAllMatches here as it causes structural changes and throws exception while iterating in SystemAPI.Query.
			}

			if (needsUpdateMatchGroups)
			{
				UpdateMatchGroups(ref state);
				ShuffleIfNeeded(ref state);				
			}
			//
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

		private void ShuffleIfNeeded(ref SystemState state)
		{
			// Check if there is any match on grid.
			if (_matchAllGroup.Groups.Length <= 0)
			{
				// No match found, check if there are tiles still moving.
				EntityQueryBuilder queryBuilder = new EntityQueryBuilder(Allocator.Temp)
					.WithAll<LevelTile, IsMoving>();
				EntityQuery entityQuery = state.EntityManager.CreateEntityQuery(queryBuilder);
				if (entityQuery.CalculateEntityCount() <= 0)
				{
					_shuffleNextFrame = true;
					Debug.Log("Shuffling!");
				}
			}
		}

		private void Shuffle(ref SystemState state)
		{
			_shuffleNextFrame = false;

			UpdateGridTiles(ref state);

			// var random = new Random(0x6E624EB7u);
			var random = new Random((uint)(BursDateTimeNow.Field.Data + 1).ClampMin(1));
			// Memcopy hash to array to modify. We can only swap cube blocks.
			var cubeTiles = new NativeList<int2>(_cubeTilesForShuffle.Count, Allocator.Temp);
			cubeTiles.CopyFrom(_cubeTilesForShuffle.ToNativeArray(Allocator.Temp));
			// Shuffle cube tile array in place.
			for (int index = cubeTiles.Length - 1; index >= 0; index--)
			{
				int randomArrayIndex = random.NextInt(0, index);
				var swapValue = cubeTiles[index];
				cubeTiles[index] = cubeTiles[randomArrayIndex];
				cubeTiles[randomArrayIndex] = swapValue;

				SwapTiles(ref state, cubeTiles[randomArrayIndex], cubeTiles[index]);
			}

			// Get guaranteed match count from settings.
			int matchToGenerate = LevelInitializeSystem.MatchSpawnCountForShuffle[LevelInitializeSystem.MatchSpawnCountForShuffle.Length - 1].SpawnCount;
			int gridTileCount = LevelInitializeSystem.GridSize.x * LevelInitializeSystem.GridSize.y;
			for (int index = 0; index < LevelInitializeSystem.MatchSpawnCountForShuffle.Length; index ++)
			{
				var setting = LevelInitializeSystem.MatchSpawnCountForShuffle[index];
				if (gridTileCount <= setting.GridTileCount)
				{
					matchToGenerate = setting.SpawnCount;
					break;
				}
			}

			_neighboursTmp.Clear();

			var lowestMatchRequirement = LevelInitializeSystem.CubeGroupConditions[0];
			var ecb = new EntityCommandBuffer(Allocator.Temp);
			// Spawn guaranteed matches.
			for (int index = 0; index < matchToGenerate; index ++)
			{
				bool matchCreated = false;
				byte matchCount = (byte)(lowestMatchRequirement - 1);

				// Randomly select a tile from cube list.
				// There may be a slight chance that selected cube to be surrounded by none blocks, like box.
				// This while loop ensures that we create a match with possible cube blocks.
				while (!matchCreated && cubeTiles.Length > 0)
				{					
					// var randomGrid = _cubeTilesForShuffle.ElementAt(random.NextInt(0, _cubeTilesForShuffle.Count));
					var randomIndex = random.NextInt(0, cubeTiles.Length);
					var randomGrid = cubeTiles[randomIndex];
					var currentEntity = GetGridTile(ref state, randomGrid);
					var currentEntityData = SystemAPI.GetComponent<LevelTile>(currentEntity);

					cubeTiles.RemoveAt(randomIndex);

					LevelInitializeSystem.GetNeighbours(randomGrid, ref _neighboursTmp);
					foreach (var nextNeighbour in _neighboursTmp)
					{
						// Remove checked tiles.
						// WARNING(GE): Normally, we should use hashset (_cubeTilesForShuffle) to check or remove for performance.
						// But at current state of NativeContainers like NativeHashSet, almost none of enumerator functions are implemented(Like ElementAt()).
						// So change this in future.
						cubeTiles.RemoveAt(cubeTiles.IndexOf(nextNeighbour));

						var neighbourEntity = GetGridTile(ref state, nextNeighbour);
						var neighbourTileData = SystemAPI.GetComponent<LevelTile>(neighbourEntity);

						// This neigbour can be changed to same the color with started cube color.
						// If they have already same color, we do not need to create a new tile.
						if (currentEntityData.CubeColor != neighbourTileData.CubeColor)
						{
							DestroyGridTile(ref state, nextNeighbour, ref ecb);
							var newTileData = new LevelTile
							{
								AssetIndex = 0,
								BlockType = BlockType.Cube,
								CubeColor = currentEntityData.CubeColor,
								GridIndex = nextNeighbour
							};
							var newEntity = LevelInitializeSystem.CreateTileEntity(newTileData, ref ecb);
						}

						matchCount = (byte)(matchCount - 1);
						if (matchCount <= 0)
						{
							Debug.Log($"Match group created: {randomGrid}, CubeColor: {currentEntityData.CubeColor}");
							matchCreated = true;
							break;
						}
					}
				}

			}
			ecb.Playback(state.EntityManager);

			// Register an update for next frame to reduce load on current frame.
			foreach (var updateMatchGroup in SystemAPI.Query<EnabledRefRW<UpdateMatchEvent>>().WithDisabled<UpdateMatchEvent>())
			{
				updateMatchGroup.ValueRW = true;
			}
		}

		private void UpdateMatchGroups(ref SystemState state)
		{
			// Dispose and clear temporary cache.
			DisposeChildNatives();

			int matchGroupIndex = 0;
			var tilesToReplace = new NativeList<TileReplace>(Allocator.Temp);

			// Iterate over each touchable tiles to find match groups and create replace tile list with new assets (Conditional icons) if needed.
			foreach (var (levelTile, canBeTouched, tileEntity) in
						SystemAPI.Query<RefRO<LevelTile>, EnabledRefRO<CanBeTouched>>().WithAll<CanBeTouched>().WithEntityAccess())
			{
				if (levelTile.ValueRO.BlockType == BlockType.Cube && !_matchAllGroup.GroupIds.ContainsKey(levelTile.ValueRO.GridIndex))
				{
					FindMatch(ref state, levelTile.ValueRO, tileEntity, ref _frontierTmp, ref _reachedTmp, ref _neighboursTmp, ref _foundTilesTmp);

					// Check if found tile count satisfies minimum match group condition.
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

						// If tiles in match group needs different kind of assets, add them to the list to change them when iteration is over.
						// Ex: Match group with 7 Tiles has different icon from group with 2 tiles.
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

					// There is no match, revert asset to standart one if needed.
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
			// var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

			var ecb = new EntityCommandBuffer(Allocator.Temp);
			foreach (var tileToReplace in tilesToReplace)
			{
				// Replace entity with new asset and copy components.
				ReplaceGridTile(ref state, tileToReplace, ref ecb);
			}
			ecb.Playback(state.EntityManager);
		}

		// 1. Destroy tiles in match group.
		// 2. Find lowest row on modified columns.
		// 3. Find upper tiles to move down.
		// 4. Generate new tiles that will fall down.
		private void BlastTiles(ref SystemState state, ref NativeList<int2> tiles)
		{
			// Holds bottom row index for each column.
			// Key: Column, Value: Lowest row.
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

				// Look for neighbouring damagable tiles around blasting group.
				FindDamagable(ref state, tileIndex, ref _neighboursTmp, ref _foundTilesTmp);
				for (int damagableIndex = _foundTilesTmp.Length - 1;  damagableIndex >= 0; damagableIndex--)
				{
					var damagable = _foundTilesTmp[damagableIndex];
					damagable.TileData.AssetIndex++;

					// If there is next damaged asset layer, replace it (Ex: Icon Box2 -> Box1) .
					if (LevelInitializeSystem.HasBlockAssetPrefab(damagable.TileData) && !replacedDamagableTiles.Contains(damagable.TileData.GridIndex))
					{
						ReplaceGridTile(ref state, new TileReplace
						{
							EntityToDestroy = damagable.Entity,
							NewTileData = damagable.TileData
						}, ref ecb);

						replacedDamagableTiles.Add(damagable.TileData.GridIndex);

						Debug.Log($"{damagable.TileData.AssetTitle} damaged: {damagable.TileData.GridIndex}");
					}
					// This was last layer for damagable, remove it from grid.
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
						// IF there is, change lowest row on column.
						while (lowestRow + 1 < LevelInitializeSystem.GridSize.y)
						{
							if (GetGridTile(ref state, new int2(lowestRow + 1, damagable.TileData.GridIndex.y)) == Entity.Null)
								lowestRow++;
							else break;
						}
						
						blastXY.SafeAdd(damagable.TileData.GridIndex.y, lowestRow);

						Debug.Log($"{damagable.TileData.AssetTitle} destroyed: {damagable.TileData.GridIndex}");

						// Destroy damagable tile found around match group.
						DestroyGridTile(ref state, damagable.TileData.GridIndex, ref ecb);
					}
				}

				// Destroy match group tile.
				DestroyGridTile(ref state, tileIndex, ref ecb);
			}
			ecb.Playback(state.EntityManager);

			var ecbNewTile = new EntityCommandBuffer(Allocator.Temp);

			// Move upwards from lowest row on column.
			foreach (var blastKVPair in blastXY)
			{
				int column = blastKVPair.Key;
				int newTileCountToGenerate = 0;
				int lastRowToMove = blastKVPair.Value;
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
					// Current tile may already in target position, if so, no need to move it.
					if (tileData.BlockType.IsMovable() && !(tileData.GridIndex.x == column && tileData.GridIndex.y == lastRowToMove))
					{
						tileData.GridIndex = new int2(lastRowToMove, column);

						// Mark this tile as movable to make it moved by TileMoveSystem.
						SystemAPI.SetComponentEnabled<IsMoving>(tileAtRow, true);
						SystemAPI.SetComponent<IsMoving>(tileAtRow, new IsMoving { Target = tileData.GridIndex });
						SystemAPI.SetComponentEnabled<CanBeTouched>(tileAtRow, false);
						SystemAPI.SetComponent<LevelTile>(tileAtRow, tileData);
;
						lastRowToMove--;
					}
					else
					{
						// There may be unmovable block like Box or there is no need to move the tile.
						newTileCountToGenerate = 0;
						lastRowToMove = row - 1;
					}
				}

				// Create new tiles for this column if needed.
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
			_cubeTilesForShuffle.Clear();
			// Cache entities with grid index.
			foreach (var (tileData, entity) in
						SystemAPI.Query<RefRO<LevelTile>>().WithEntityAccess())
			{
				_tiles.Add(tileData.ValueRO.GridIndex, entity);
				if (tileData.ValueRO.BlockType == BlockType.Cube)
					_cubeTilesForShuffle.Add(tileData.ValueRO.GridIndex);
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
			_cubeTilesForShuffle.Remove(gridIndex);
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

		private void SwapTiles(ref SystemState state, int2 tile1, int2 tile2)
		{
			var tileEntity1 = GetGridTile(ref state, tile1);
			var tileEntity2 = GetGridTile(ref state, tile2);
			var tileEntityLT1 = SystemAPI.GetComponent<LocalTransform>(tileEntity1);
			var tileEntityLT2 = SystemAPI.GetComponent<LocalTransform>(tileEntity2);
			var tileEntityData1 = SystemAPI.GetComponent<LevelTile>(tileEntity1);
			var tileEntityData2 = SystemAPI.GetComponent<LevelTile>(tileEntity2);

			SystemAPI.SetComponent<LocalTransform>(tileEntity1, tileEntityLT2);
			SystemAPI.SetComponent<LocalTransform>(tileEntity2, tileEntityLT1);

			tileEntityData1.GridIndex = tile2;
			tileEntityData2.GridIndex = tile1;
			
			SystemAPI.SetComponent<LevelTile>(tileEntity1, tileEntityData1);			
			SystemAPI.SetComponent<LevelTile>(tileEntity2, tileEntityData2);

			_tiles[tile1] = tileEntity2;
			_tiles[tile2] = tileEntity1;
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

		/// <summary>
		/// Finds matches for tile using Breadth First Search algorithm.
		/// BFS is useful to provide wave like explosions as it searches by increasing radius.
		/// </summary>
		/// <param name="state"></param>
		/// <param name="startEntityTileData"></param>
		/// <param name="startEntity"></param>
		/// <param name="frontier"></param>
		/// <param name="reached"></param>
		/// <param name="neighbours"></param>
		/// <param name="foundTiles"></param>
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
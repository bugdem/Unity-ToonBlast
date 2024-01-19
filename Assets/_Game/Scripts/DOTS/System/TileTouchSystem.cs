using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using static UnityEngine.EventSystems.EventTrigger;

namespace GameEngine.Core
{
	public partial struct TileTouchSystem : ISystem
	{
		private NativeList<NativeHashSet<EntityTile>> _matchGroups;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			_matchGroups = new NativeList<NativeHashSet<EntityTile>>(Allocator.Persistent);

			state.RequireForUpdate<LevelInitializeEvent>();
		}

		[BurstCompile]
		public void OnDestroy(ref SystemState state)
		{
			DisposeChildMatchHash();
			_matchGroups.Dispose();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			Entity touchedEntity = Entity.Null;

			foreach (var touchInput in
				SystemAPI.Query<RefRO<InputTouch>>())
			{
				// Debug.Log("Touched!" + touchInput.ValueRO.Value);

				// System does not allow us to destroy entity while idiomatic iteration.
				touchedEntity = LevelInitializeSystem.GetEntityFromRay(touchInput.ValueRO.Ray);
			}


			if (touchedEntity != Entity.Null && SystemAPI.IsComponentEnabled<CanBeTouched>(touchedEntity))
			{
				var tileData = SystemAPI.GetComponent<LevelTile>(touchedEntity);
				Debug.Log("Touched Entity!: " + tileData.GridIndex);

				// Find neighbours of tapped cube.
				if (tileData.BlockType == BlockType.Cube)
				{
					var foundTiles = new NativeList<EntityTile>(Allocator.Temp);
					FindMatch(ref state, tileData, touchedEntity, ref foundTiles);
					
					if (foundTiles.Length >= 2)
					{
						var ecb = new EntityCommandBuffer(Allocator.Temp);

						for (int index = foundTiles.Length - 1; index >= 0; index--)
						{
							var tile = foundTiles[index];

							LevelInitializeSystem.RemoveEntityFromCache(tile.GridIndex);
							ecb.DestroyEntity(tile.Entity);
						}

						ecb.Playback(state.EntityManager);
					}
				}
			}			
		}

		private void DisposeChildMatchHash()
		{
			for (int index = _matchGroups.Length - 1; index >= 0; index--)
			{
				var match = _matchGroups[index];
				_matchGroups.RemoveAt(index);
				match.Dispose();
			}
		}

		private void FindAllMatches(ref SystemState state)
		{
			DisposeChildMatchHash();

			var gridSize = LevelInitializeSystem.GridSize;
			var groupedTiles = new NativeHashMap<int2, int>(1, Allocator.Temp);

			foreach (var (levelTile, tileEntity) in
						SystemAPI.Query<RefRO<LevelTile>>().WithAll<CanBeTouched>().WithEntityAccess())
			{
				if (levelTile.ValueRO.BlockType == BlockType.Cube && !groupedTiles.ContainsKey(levelTile.ValueRO.GridIndex))
				{

				}
			}

			/*
			var gridSize = LevelInitializeSystem.GridSize;
			for (int column = gridSize.x - 1; column >= 0; column --)
			{
				for (int row = gridSize.y - 1; row >= 0; row --)
				{
					var entity = LevelInitializeSystem.GetEntity(new int2(row, column));

				}
			}
			*/
		}

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
			var entity = LevelInitializeSystem.GetEntity(gridIndex);
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
				GridIndex = startEntityTileData.GridIndex
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

					var neighbourEntity = LevelInitializeSystem.GetEntity(nextNeighbour);
					if (neighbourEntity != Entity.Null)
					{
						var neighbourTileData = SystemAPI.GetComponent<LevelTile>(neighbourEntity);
						if (SystemAPI.IsComponentEnabled<CanBeTouched>(neighbourEntity)
							&& neighbourTileData.BlockType == BlockType.Cube
							&& neighbourTileData.CubeColor == searchingCubeColor)
						{
							foundTiles.Add(new EntityTile
							{
								GridIndex = neighbourTileData.GridIndex,
								Entity = neighbourEntity
							});
							frontier.Add(nextNeighbour);
						}
					}

					reached.Add(nextNeighbour);
				}
			}
		}
	}
}
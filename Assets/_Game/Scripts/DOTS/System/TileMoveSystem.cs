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
	public partial struct TileMoveSystem : ISystem
	{
		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<LevelInitializeEvent>();
		}

		[BurstCompile]
		public void OnDestroy(ref SystemState state)
		{

		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			foreach (var (isMovingEnabled, localTransform, entity) 
				in SystemAPI.Query<EnabledRefRW<IsMoving>, RefRW<LocalTransform>>().WithAll<LevelTile>().WithDisabled<CanBeTouched>().WithEntityAccess())
			{
				var isMoving = SystemAPI.GetComponent<IsMoving>(entity);
				float3 targetPosition = LevelInitializeSystem.GetTileWorldPosition(isMoving.Target);
				localTransform.ValueRW.Position = Vector3.MoveTowards(localTransform.ValueRO.Position, targetPosition, SystemAPI.Time.DeltaTime * 2f); ;

				
				if (math.distancesq(localTransform.ValueRO.Position, targetPosition) < 0.001f)
				{
					localTransform.ValueRW.Position = targetPosition;
					SystemAPI.SetComponentEnabled<CanBeTouched>(entity, true);

					isMovingEnabled.ValueRW = false;

					foreach (var updateMatchGroup in SystemAPI.Query<EnabledRefRW<UpdateMatchEvent>>().WithDisabled<UpdateMatchEvent>())
					{
						updateMatchGroup.ValueRW = true;
					}
				}
			}
		}
	}
}
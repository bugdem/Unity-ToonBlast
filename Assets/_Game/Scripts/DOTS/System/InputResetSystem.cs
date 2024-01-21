using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace GameEngine.Core
{
	[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
	[UpdateAfter(typeof(EndSimulationEntityCommandBufferSystem))]
	public partial struct InputResetSystem : ISystem
	{
		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			var ecb = new EntityCommandBuffer(Allocator.Temp);

			foreach (var (inputTouch, entity) in SystemAPI.Query<InputTouch>().WithEntityAccess())
			{
				ecb.SetComponentEnabled<InputTouch>(entity, false);
			}

			ecb.Playback(state.EntityManager);
			ecb.Dispose();
		}
	}
}
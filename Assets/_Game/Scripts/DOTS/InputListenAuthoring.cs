using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GameEngine.Core
{
    public class InputListenAuthoring : MonoBehaviour
    {
		public class Baker : Baker<InputListenAuthoring>
		{
			public override void Bake(InputListenAuthoring authoring)
			{
				var playerEntity = GetEntity(TransformUsageFlags.Dynamic);

				AddComponent<InputTag>(playerEntity);
				AddComponent<InputTouchPosition>(playerEntity);
				AddComponent<InputTouch>(playerEntity);
				SetComponentEnabled<InputTouch>(playerEntity, false);
			}
		}
	}

	public struct InputTouchPosition : IComponentData
	{
		public float2 Value;
	}

	public struct InputTouch : IComponentData, IEnableableComponent { }

	public struct InputTag : IComponentData { }
}
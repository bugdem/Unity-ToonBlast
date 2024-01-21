using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GameEngine.Core
{
	// Bakes assets like cube or box blocks that will be used to create tiles by systems.
	public class AssetBridgeAuthoring : MonoBehaviour
	{
		[SerializeField] private List<LevelAssetPack> _assetPacks;

		public List<LevelAssetPack> AssetPacks => _assetPacks;

		public class Baker : Baker<AssetBridgeAuthoring>
		{
			public override void Bake(AssetBridgeAuthoring authoring)
			{
				// Bake assets that will be used in game.
				// WARNING(GE): Because of baking limitations, we cannot bake only the current level's assets as baking function
				// should know everything in build time, not runtime.
				// For now, it is not a problem because we have one asset pack. But in future, for memory reasons
				// , this may needs to be changed in future.
				var playerEntity = GetEntity(TransformUsageFlags.None);
				foreach (var assetPack in authoring.AssetPacks)
				{
					var assetPackEntity = CreateAdditionalEntity(TransformUsageFlags.None, entityName: $"AssetPack-{assetPack.Id}");

					AddComponent(assetPackEntity, new LevelAssetPackConfig { Id = assetPack.Id });

					var cubeAssets = AddBuffer<LevelAssetCubeBlockData>(assetPackEntity);
					var layeredAssets = AddBuffer<LevelAssetLayeredBlockData>(assetPackEntity);

					foreach (var blockData in assetPack.BlockAssets)
					{
						if (blockData is CubeBlockData cubeBlockData)
						{
							for (int index = 0; index < cubeBlockData.AssetCount; index++)
							{
								cubeAssets.Add(new LevelAssetCubeBlockData
								{
									Key = new LevelAssetCubeBlockKey { AssetIndex = index, Color = cubeBlockData.Color },
									Prefab = GetEntity(cubeBlockData.GetAsset((ushort)index).Prefab, TransformUsageFlags.Dynamic)
								});
							}
						}
						else if (blockData is LayeredBlockData layeredBlockData)
						{
							for (int index = 0; index < layeredBlockData.AssetCount; index++)
							{
								layeredAssets.Add(new LevelAssetLayeredBlockData
								{
									Key = new LevelAssetLayeredBlockKey { Title =  layeredBlockData.Title, AssetIndex = index },
									Prefab = GetEntity(layeredBlockData.GetAsset((ushort)index).Prefab, TransformUsageFlags.Dynamic)
								});
							}
						}
					}
				}
			}
		}
	}

	public struct LevelAssetPackConfig : IComponentData
	{
		public int Id;
	}

	public struct LevelAssetCubeBlockData : IBufferElementData
	{
		public LevelAssetCubeBlockKey Key;
		public Entity Prefab;
	}

	public struct LevelAssetLayeredBlockData : IBufferElementData
	{
		public LevelAssetLayeredBlockKey Key;
		public Entity Prefab;
	}

	public struct LevelAssetCubeBlockKey : IEquatable<LevelAssetCubeBlockKey>
	{
		public CubeColor Color;
		public int AssetIndex;

		public bool Equals(LevelAssetCubeBlockKey other)
		{
			return Color == other.Color && AssetIndex == other.AssetIndex;
		}

		public override int GetHashCode()
		{
			return ((FixedString64Bytes)($"{Color}&{AssetIndex}")).GetHashCode();
		}
	}

	public struct LevelAssetLayeredBlockKey : IEquatable<LevelAssetLayeredBlockKey>
	{
		public FixedString64Bytes Title;
		public int AssetIndex;

		public bool Equals(LevelAssetLayeredBlockKey other)
		{
			return Title.Equals(other.Title) && AssetIndex == other.AssetIndex;
		}

		public override int GetHashCode()
		{
			return ((FixedString64Bytes)($"{Title}&{AssetIndex}")).GetHashCode();
		}
	}

	public struct LevelAssetPackConfigHash
	{
		[ReadOnly] public int Id;
		[ReadOnly] public NativeHashMap<LevelAssetCubeBlockKey, Entity> CubeAssets;
		[ReadOnly] public NativeHashMap<LevelAssetLayeredBlockKey, Entity> LayeredAssets;
	}
}
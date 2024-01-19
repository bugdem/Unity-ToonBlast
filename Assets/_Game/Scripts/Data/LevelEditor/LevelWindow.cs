
#if UNITY_EDITOR

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using GameEngine.Util;
using System.Linq;

namespace GameEngine.Core
{
	public class LevelWindow : EditorWindow
	{
		private const string LEVELDATA_FILE_PATH = "Assets/_Game/Datas/Levels";
		
		private static TileManager _tileManager;
		private static LevelAssetPack _assetPack;
		private static TileData _selectedBrush;
		private static HashSet<Vector2Int> _brushedTiles = new();
		private static bool _isMouseDown = false;
		private static Dictionary<CubeColor, bool> _cubeAvailableColors;

		private int _row = 11;
		private int _col = 10;
		private List<TileColumn> _tiles;
		private bool _levelDataExists;
		private LevelData _loadedLevelData;
		

		public static void OpenWindow(TileManager tileManager)
		{
			_tileManager = tileManager;
			_cubeAvailableColors = new();
			string[] guids = AssetDatabase.FindAssets("t:LevelAssetPack", null);
			if (guids.Length > 0) _assetPack = AssetDatabase.LoadAssetAtPath<LevelAssetPack>(AssetDatabase.GUIDToAssetPath(guids[0]));

			var window = GetWindow<LevelWindow>("Level Editor");
			window.Show();
		}

		// Window has been selected
		private void OnFocus()
		{
			// Remove delegate listener if it has previously
			// been assigned.
			SceneView.duringSceneGui -= this.OnSceneGUI;

			// Add (or re-add) the delegate.
			SceneView.duringSceneGui += this.OnSceneGUI;
		}

		private void OnDestroy()
		{
			_tileManager.TileContainer.gameObject.RemoveAllChild(true);

			// When the window is destroyed, remove the delegate
			// so that it will no longer do any drawing.
			SceneView.duringSceneGui -= this.OnSceneGUI;
		}

		private void OnGUI()
		{
			GUILayout.Space(20f);

			GUILayout.BeginHorizontal();
			EditorGUI.BeginChangeCheck();
			_loadedLevelData = (LevelData)EditorGUILayout.ObjectField("Level Data", _loadedLevelData, typeof(LevelData), false);
			if (EditorGUI.EndChangeCheck())
			{
				if (_loadedLevelData == null)
				{
					_levelDataExists = false;
				}
				else
				{
					LoadLevelData(_loadedLevelData);
				}
			}
			GUILayout.EndHorizontal();

			GUILayout.Space(20f);

			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Create New Level"))
			{
				Debug.Log("New Level Creating...");

				_loadedLevelData = null;
				CreateNewLevelData();

				Debug.Log("Level Created!");
			}
			GUILayout.EndHorizontal();

			GUILayout.Space(20f);

			GUILayout.BeginHorizontal();
			_row = Mathf.Clamp(EditorGUILayout.IntField("Row (M):", _row), 2, 10);
			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			_col = Mathf.Clamp(EditorGUILayout.IntField("Column (N):", _col), 2, 10);
			GUILayout.EndHorizontal();

			GUILayout.Space(20f);

			GUILayout.BeginHorizontal();
			_assetPack = (LevelAssetPack)EditorGUILayout.ObjectField("Asset Pack", _assetPack, typeof(LevelAssetPack), false);
			GUILayout.EndHorizontal();

			if (_levelDataExists)
			{
				if (_assetPack != null)
				{
					GUILayout.Space(10f);

					GUILayout.BeginHorizontal();
					GUILayout.Label("Available Colors: ");
					GUILayout.BeginVertical();
					var values = Enum.GetValues(typeof(CubeColor)).Cast<CubeColor>();
					foreach (var value in values)
					{
						if (value != CubeColor.Random)
						{
							GUILayout.BeginHorizontal();
							var isSelected = EditorGUILayout.Toggle(value.ToString(), _cubeAvailableColors.GetValueOrDefault(value, true));
							if (_cubeAvailableColors.ContainsKey(value)) _cubeAvailableColors[value] = isSelected;
							else _cubeAvailableColors.Add(value, isSelected);
							GUILayout.EndHorizontal();
						}
					}
					GUILayout.EndVertical();
					GUILayout.EndHorizontal();

					GUILayout.Space(10f);

					GUILayout.BeginHorizontal();
					GUILayout.Label("Block Brush");
					GUILayout.EndHorizontal();

					GUILayout.BeginHorizontal();
					GUILayout.BeginVertical();

					var grid = new GridEditor(50, 50);
					for (int blockAssetIndex = 0; blockAssetIndex < _assetPack.BlockAssets.Count; blockAssetIndex++)
					{
						var blockAsset = _assetPack.BlockAssets[blockAssetIndex];
						if (blockAsset.Type == BlockType.Cube)
						{
							var cubeData = blockAsset as CubeBlockData;

							// Draw default icon.
							if (GUI.Button(grid.GetRect(), cubeData.GetAsset(0).Icon.texture) || _selectedBrush == null)
							{
								_selectedBrush = new TileData
								{
									AssetTitle = blockAsset.Title,
									BlockType = blockAsset.Type,
									CubeColor = cubeData.Color,
									AssetIndex = 0
								};
							}
						}
						else if (blockAsset.Type == BlockType.Layered)
						{
							var layeredData = blockAsset as LayeredBlockData;

							// Draw default icon.
							for (int layeredIterationIndex = 0; layeredIterationIndex < layeredData.AssetCount; layeredIterationIndex++)
							{
								var sprite = layeredData.GetAsset((ushort)(layeredIterationIndex)).Icon;
								if (GUI.Button(grid.GetRect(), sprite.texture) || _selectedBrush == null)
								{
									_selectedBrush = new TileData
									{
										AssetTitle = blockAsset.Title,
										BlockType = blockAsset.Type,
										AssetIndex = layeredIterationIndex
									};
								}
							}
						}

						// Texture banner = (Texture)AssetDatabase.LoadAssetAtPath("Assets/_Game/Sprites/MatchItems/Box0.png", typeof(Texture));
						// GUILayout.Box(banner, GUILayout.Height(64), GUILayout.Width(64));
						// GUILayout.Button(banner);
					}

					GUILayout.EndVertical();

					GUILayout.EndHorizontal();
				}

				GUILayout.Space(20f);
				GUILayout.BeginHorizontal();
				if (GUILayout.Button("Save Level", GUILayout.Width(100)))
				{
					SaveLevel();
				}
				GUILayout.EndHorizontal();
			}
		}

		private void OnSceneGUI(SceneView sceneView)
		{
			_tileManager = FindObjectOfType<TileManager>();

			Event currentEvent = Event.current;
			if (currentEvent != null && currentEvent.button == 0)
			{
				int controlID = GUIUtility.GetControlID(FocusType.Passive);

				switch (currentEvent.GetTypeForControl(controlID))
				{
					case EventType.MouseDown:
						{
							GUIUtility.hotControl = controlID;

							_isMouseDown = true;
							_brushedTiles.Clear();

							currentEvent.Use();
						}
						break;
					case EventType.MouseUp:
						{
							_isMouseDown = false;

							GUIUtility.hotControl = 0;
							currentEvent.Use();
						}
						break;
				}
			}

			if (_isMouseDown)
			{
				Ray ray = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition);
				Plane tilePlane = new Plane(_tileManager.TileContainer.transform.forward, _tileManager.TileContainer.transform.position);
				if (tilePlane.Raycast(ray, out float enter))
				{
					Vector3 point = ray.GetPoint(enter);
					var collider2D = Physics2D.OverlapPoint(point);
					if (collider2D != null)
					{
						var blockEditor = collider2D.GetComponent<BlockEditor>();
						if (blockEditor != null)
						{
							Debug.Log("Col:" + collider2D.gameObject.name);

							Vector2Int gridIndex = blockEditor.GridIndex;
							DestroyImmediate(blockEditor.gameObject);

							CreateBlockEditor(_selectedBrush, gridIndex.x, gridIndex.y);

							_tiles[gridIndex.y].Rows[gridIndex.x] = _selectedBrush;
						}
					}
				}
			}

			// Handles.BeginGUI();
			// Handles.EndGUI();
		}

		private void LoadLevelData(LevelData levelData)
		{
			_loadedLevelData = levelData;

			_row = levelData.Row;
			_col = levelData.Col;
			_assetPack = levelData.AssetPack;

			_cubeAvailableColors.Clear();
			var values = Enum.GetValues(typeof(CubeColor)).Cast<CubeColor>();
			foreach (var value in values)
			{
				_cubeAvailableColors.Add(value, levelData.AvailableColors.Contains(value));
			}

			CreateNewLevelData();
		}

		private void CreateNewLevelData()
		{
			_tileManager.TileContainer.gameObject.RemoveAllChild(true);

			_tiles = new List<TileColumn>(_col);
			for (int column = 0; column < _col; column++)
			{
				_tiles.Add(new TileColumn { Rows = new List<TileData>(_row) });
				for (int row = 0; row < _row; row++)
				{
					var tileData = _loadedLevelData == null ? new TileData { BlockType = BlockType.Cube, AssetIndex = 0, CubeColor = CubeColor.Random }
															: new TileData { BlockType = _loadedLevelData.Tiles[column].Rows[row].BlockType, CubeColor = _loadedLevelData.Tiles[column].Rows[row].CubeColor, AssetIndex = _loadedLevelData.Tiles[column].Rows[row].AssetIndex, AssetTitle = _loadedLevelData.Tiles[column].Rows[row].AssetTitle };
					_tiles[column].Rows.Add(tileData);

					CreateBlockEditor(tileData, row, column);
				}
			}

			_levelDataExists = true;
		}

		private BlockEditor CreateBlockEditor(TileData tileData, int row, int column)
		{
			Vector3 localPosition = new Vector2(column * TileManager.TILE_BLOCK_SIZE + TileManager.TILE_BLOCK_SIZE * .5f,
									 -row * TileManager.TILE_BLOCK_SIZE + TileManager.TILE_BLOCK_SIZE * .5f
									);
			localPosition += -new Vector3(_col * TileManager.TILE_BLOCK_SIZE, -_row * TileManager.TILE_BLOCK_SIZE, 0f) * .5f;
			localPosition += _tileManager.TileContainer.transform.position;

			var blockEditor = PrefabUtility.InstantiatePrefab(_tileManager.BlockEditorPrefab, _tileManager.TileContainer.transform) as BlockEditor;
			blockEditor.Data = tileData;
			blockEditor.name = $"Block-{row},{column}";
			blockEditor.transform.position = localPosition;
			blockEditor.IconRenderer.sortingOrder = -row;
			blockEditor.transform.localScale = Vector2.one * TileManager.TILE_BLOCK_SIZE;
			blockEditor.GridIndex = new Vector2Int(row, column);

			foreach (var asset in _assetPack.BlockAssets)
			{
				if (asset.Type == tileData.BlockType)
				{
					if (asset is CubeBlockData cubeBlockData && cubeBlockData.Color == tileData.CubeColor)
					{
						blockEditor.IconRenderer.sprite = cubeBlockData.GetAsset(0).Icon;
						break;
					}
					else if (asset is LayeredBlockData layeredBlockData)
					{
						blockEditor.IconRenderer.sprite = layeredBlockData.GetAsset((ushort)tileData.AssetIndex).Icon;
						break;
					}
				}
			}

			return blockEditor;
		}

		private void SaveLevel()
		{
			// Create new level data if there is not any level selected.
			if (_loadedLevelData == null)
			{
				string fileName = $"{System.Guid.NewGuid().ToString()}";

				var newLevelData = ScriptableObject.CreateInstance<LevelData>();
				newLevelData.name = fileName;
				AssetDatabase.CreateAsset(newLevelData, $"{LEVELDATA_FILE_PATH}/{fileName}.asset");

				_loadedLevelData =  newLevelData as LevelData;
			}

			// Update current level data
			_loadedLevelData.Row = _row;
			_loadedLevelData.Col = _col;
			_loadedLevelData.AssetPack = _assetPack;
			// Deep copy window variables to save as scriptable object.
			_loadedLevelData.Tiles = CloneTileList(_tiles);

			_loadedLevelData.AvailableColors = new();
			foreach (var availableColor in _cubeAvailableColors)
			{
				if (availableColor.Value)
					_loadedLevelData.AvailableColors.Add(availableColor.Key);
			}
		}

		private List<TileColumn> CloneTileList( List<TileColumn> fromTiles)
		{
			var newTiles = new List<TileColumn>(fromTiles.Count);
			for (int column = 0; column < fromTiles.Count; column++)
			{
				var newColumn = new TileColumn { Rows = new List<TileData>(fromTiles[column].Rows.Count) };
				for (int row = 0; row < fromTiles[column].Rows.Count; row++)
				{
					var rowData = fromTiles[column].Rows[row];
					var tileData = new TileData
					{
						BlockType = rowData.BlockType,
						CubeColor = rowData.CubeColor,
						AssetIndex = rowData.AssetIndex,
						AssetTitle = rowData.AssetTitle
					};
					newColumn.Rows.Add(tileData);
				}
				newTiles.Add(newColumn);
			}
			return newTiles;
		}
	}

	public class GridEditor
	{
		private readonly int _height;
		private readonly int _width;
		private Rect _currentRow;
		private int _lastX = 0;

		public GridEditor(int width, int height)
		{
			this._width = width;
			this._height = height;
		}

		public Rect GetRect()
		{
			if (!EnoughSpaceInCurrentRow())
			{
				_currentRow = GUILayoutUtility.GetRect(_width, _height);
				_lastX = 0;
			}

			return GetNextRectInCurrentRow();
		}

		private bool EnoughSpaceInCurrentRow()
		{
			return _currentRow.width >= _lastX + _width;
		}

		private Rect GetNextRectInCurrentRow()
		{
			var ret = new Rect(_currentRow) { x = _lastX, width = _width };
			_lastX += _width;
			return ret;
		}
	}
}

#endif
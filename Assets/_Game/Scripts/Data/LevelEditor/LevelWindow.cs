
#if UNITY_EDITOR

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using GameEngine.Util;

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

		private int _row = 10;
		private int _col = 10;
		private List<TileColumn> _tiles;
		private bool _levelDataExists;
		private LevelData _loadedLevelData;

		public static void OpenWindow(TileManager tileManager)
		{
			_tileManager = tileManager;
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
							if (GUI.Button(grid.GetRect(), cubeData.GetDefaultIcon().texture))
							{
								_selectedBrush = new TileData
								{
									BlockType = blockAsset.Type,
									CubeColor = cubeData.Color,
									Iteration = 0
								};
							}
						}
						else if (blockAsset.Type == BlockType.Layered)
						{
							var layeredData = blockAsset as LayeredBlockData;

							// Draw default icon.
							for (int layeredIterationIndex = 0; layeredIterationIndex < layeredData.IterationCount; layeredIterationIndex++)
							{
								var sprite = layeredData.GetLayeredIcon(layeredIterationIndex);
								if (GUI.Button(grid.GetRect(), sprite.texture))
								{
									_selectedBrush = new TileData
									{
										BlockType = blockAsset.Type,
										Iteration = layeredIterationIndex
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
					var tileData = _loadedLevelData == null ? new TileData { BlockType = BlockType.Cube, Iteration = 0, CubeColor = CubeColor.Random }
															: new TileData { BlockType = _loadedLevelData.Tiles[column].Rows[row].BlockType, CubeColor = _loadedLevelData.Tiles[column].Rows[row].CubeColor, Iteration = _loadedLevelData.Tiles[column].Rows[row].Iteration };
					_tiles[column].Rows.Add(tileData);

					CreateBlockEditor(tileData, row, column);
				}
			}

			_tileManager.TileContainer.transform.localPosition = - new Vector2(_col * TileManager.TILE_BLOCK_SIZE, -_row * TileManager.TILE_BLOCK_SIZE) * .5f;

			_levelDataExists = true;
		}

		private BlockEditor CreateBlockEditor(TileData tileData, int row, int column)
		{
			Vector2 localPosition = new Vector2(column * TileManager.TILE_BLOCK_SIZE + TileManager.TILE_BLOCK_SIZE * .5f,
									 -row * TileManager.TILE_BLOCK_SIZE + TileManager.TILE_BLOCK_SIZE * .5f
									);

			var blockEditor = PrefabUtility.InstantiatePrefab(_tileManager.BlockEditorPrefab, _tileManager.TileContainer.transform) as BlockEditor;
			blockEditor.Data = tileData;
			blockEditor.name = $"Block-{row},{column}";
			blockEditor.transform.localPosition = localPosition;
			blockEditor.IconRenderer.sortingOrder = -row;
			blockEditor.transform.localScale = Vector2.one * TileManager.TILE_BLOCK_SIZE;
			blockEditor.GridIndex = new Vector2Int(row, column);

			foreach (var asset in _assetPack.BlockAssets)
			{
				if (asset.Type == tileData.BlockType)
				{
					if (asset is CubeBlockData cubeBlockData && cubeBlockData.Color == tileData.CubeColor)
					{
						blockEditor.IconRenderer.sprite = cubeBlockData.GetDefaultIcon();
						break;
					}
					else if (asset is LayeredBlockData layeredBlockData)
					{
						blockEditor.IconRenderer.sprite = layeredBlockData.GetLayeredIcon(tileData.Iteration);
						break;
					}
				}
			}

			return blockEditor;
		}

		private void SaveLevel()
		{
			// Create new level data.
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
						Iteration = rowData.Iteration
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
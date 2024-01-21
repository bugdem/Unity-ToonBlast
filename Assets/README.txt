
Toon Blast (Implementation as Data oriented approach)

- Game is developed with DOTS ECS and fully unmanaged types, which makes it possible to use Burst and Jobs.

- A Level Editor Window is added to create levels with specific requirements like grid size, colors, asset packs, etc.
	You can open up window from Game(Scene) -> TileManager -> Editor Window(Button).
	A new level can be created with "Create New Level" Button(To save level, click on "Save Level" Button).
	If level data is selected, it is automatically loaded into level editor.
	A different asset pack can be loaded to paint each level with different assets.
	Colors that will be used in level can be limited by disabling specific colors.
	You can paint tiles by selecting a block brush and click inside Scene directly.
	To test specific level created, you need to select Game(Scene) -> Managers -> Level Manager and add it to levels as first.

- Stationary blocks are interactable and can be blasted while other blocks are falling.
- 
- 
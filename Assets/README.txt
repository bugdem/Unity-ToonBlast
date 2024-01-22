
Toon Blast (Implementation as Data oriented approach)

Features: 

- Game is developed with DOTS ECS and fully unmanaged types, which makes it possible to use Burst and Jobs.
- Burst is enabled by default. It can be turned off to test performance from Jobs(Menu) -> Burst -> Enable Compilation
- New Input System is used for possible porting.

- A Level Editor Window is added to create levels with specific requirements like grid size, colors, asset packs, etc.
	You can open up window from Game(Scene) -> TileManager -> Editor Window(Button).
	A new level can be created with "Create New Level" Button(To save level, click on "Save Level" Button).
	If level data is selected, it is automatically loaded into level editor.
	A different asset pack can be loaded to paint each level with different assets.
	Colors that will be used in level can be limited by disabling specific colors.
	You can paint tiles by selecting a block brush and click inside Scene directly.
	To test specific level created, you need to select Game(Scene) -> Managers -> Level Manager and add it to levels as first.

- Stationary blocks are interactable and can be blasted while other blocks are falling.
- There are currently 6 different cube block colors with 1 layered block (Box Obstacle).
- Blocks are defined as scriptable objects.
	INFO(GE): Because of some limitations about Burst and Jobs using unmanaged types, multiple prefabs for each block is created.
	We cannot use a single prefab and change its sprite and sorting layer for each condition, because SpriteRenderer is managed type and 
	managed types cannot be used in Burst and Jobs. Instead, multiple prefabs for each block condition is created and feed as entity to Burst Systems.
- Deadlocks are solved by swapping tiles and adding guaranteed matches, not blindly shuffling.

---------------------------------------------------

Execution's short description:

1. LevelManager loads player's current level data and notifies EntityBridge to prepare DOTS System side.
2. LevelInitializeSystem starts working just once when current level config and required assets are loaded by EntityBridge.
3. When initialization is done, other systems like TileMoveSystem and TileMatchGroup system starts working.

---------------------------------------------------

Future improvements:

1. Jobs are not used right now so systems are not working multithreaded. But it is easy to add Jobs because system is designed 
for Burst and Jobs in mind and used fully unmanaged types for heavy operations.
2. Some methods are not implemented by Unity(NativeContainers like NativeHashSet's Enumerator or ElementAt). This limitations causes some 
workarounds that uses unnecessary memory allocations for time being, but they do not effect game noticibly.
3. Some effects like particles may be generated on Monobehaviour side with events fired from Systems.
4. Level Editor improvement.
# DunGen
A simple unity dungeon generating script

To use:

1. Make an empty that will be used as the root of the dungeon
2. Attach a new DunGen script to the empty
3. Set a material to the "Mat" field of the script, this will be the mat for the entire dungeon
4. Specify settings for the dungeon in the inspector
5. Press play. If "Generate On Spawn" is enabled in the script, the dungeon will be generated when the game starts. If "Generate On Value Change" is enabled, every time a value is changed in the inspector, the dungeon will be regenerated. Otherwise, the dungeon can be generated by pressing the "Generate" button. The Generate() method can also be called on the script to generate the dungeon.

Settings:

- Mat: The material that will be applied to the dungeon
- DungeonWidth and Height: The size of the dungeon in tiles
- TileSize: The size of the tiles
- WallThickness: The thickness of the walls
- WallHeight: The height of the walls
- RoomDensity: Then number of rooms we will attempt to spawn within the given limits
- Max/MinRoomWidth: Maximum and minimum room width
- Max/MinRoomHeight: Maximum and minimum room height
- Windiness: How windy the corridors will be. 0 means that when deciding which direction to grow the corridor in, we will try and use the same as previous growth. 1 means choose a completely random direction within the ones available
- UndoOpenChance: When we undo the corridors, this is the chance we have to create an additional opening in an adjacent wall
- RemovePillars: There is a chance of lone corners remaining when creating additional openings with UndoOpenChance. When this is enabled, it removes them.
- Seed: The seed with which we generate the dungeon
- GenerateOnSpawn: If enabled, dungeon will be generated on spawn
- GenerateOnValueChange: If enabled, dungeon will be generated every time a value is changed in the inspector

An Example:

![Example](https://i.imgur.com/TeeTSI6.png)

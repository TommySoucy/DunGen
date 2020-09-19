using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DunGen : MonoBehaviour
{
    private class Tile
    {
        public Vector2Int coords { get; }

        public byte walls { get; set; }
        public byte corners { get; set; }

        public int region { get; set; }

        public Tile() { }

        public Tile(Vector2Int coords, int region)
        {
            this.coords = coords;
        }

        public Tile(Vector2Int coords, int region, byte walls, byte corners)
        {
            this.coords = coords;
            this.region = region;
            this.walls = walls;
            this.corners = corners;
        }

        public Tile(int x,int y, int region, byte walls, byte corners)
        {
            coords=new Vector2Int(x,y);
            this.region = region;
            this.walls = walls;
            this.corners = corners;
        }
    }

    private class Room
    {
        public Vector2Int dimensions { get; set; }
        public Vector2Int coords { get; set; } // Bot left
        public int region { get; }

        public Room(int region)
        {
            this.region = region;
        }

        public Room(Vector2Int dimensions, Vector2Int coords, int region)
        {
            this.dimensions = dimensions;
            this.coords = coords;
            this.region = region;
        }

        public Room(int dimensionX, int dimensionY, int coordX, int coordY, int region)
        {
            dimensions = new Vector2Int(dimensionX, dimensionY);
            coords = new Vector2Int(coordX, coordY);
            this.region = region;
        }
    }

    private class Connection
    {
        public Tile src { get; }
        public Tile dest { get; }
        public byte direction { get; }

        public Connection(Tile src, Tile dest, byte direction)
        {
            this.src = src;
            this.dest = dest;
            this.direction = direction;
        }
    }

    public Material Mat;

    private const int maxInt = 2147399999; // because actual max int overflows when used with range attribute for some reason

    [Range(2, maxInt)]
    public int dungeonWidth, dungeonHeight;

    public float tileSize, wallThickness, wallHeight;
    
    [Range(1, maxInt)]
    public int roomDensity;

    [Range(2, maxInt)]
    public int maxRoomWidth;
    [Range(2, maxInt)]
    public int minRoomWidth;

    [Range(2, maxInt)]
    public int maxRoomHeight;
    [Range(2, maxInt)]
    public int minRoomHeight;

    [Range(0,1)]
    public float windiness;

    [Range(0,1)] 
    public float undoOpenChance;

    public bool removePillars;

    public int seed;

    public bool generateOnSpawn = true;

    private Dictionary<Vector2Int, Tile> tiles;
    private List<List<Vector2Int>> regionTiles;
    private List<Vector2Int> deadEnds;
    private List<Room> rooms;

    private int region;
    
    void Start()
    {
        Random.InitState(seed);
        region = 0;
        tiles=new Dictionary<Vector2Int, Tile>();
        rooms = new List<Room>();
        regionTiles=new List<List<Vector2Int>>();
        regionTiles.Add(new List<Vector2Int>());
        deadEnds=new List<Vector2Int>();

        if (generateOnSpawn)
            Generate();
    }

    public void Generate()
    {
        ResetStructs();


        //long startTime = System.DateTimeOffset.Now.ToUnixTimeMilliseconds();

        //Debug.Log("DEBUG: Making rooms...");

        MakeRooms();

        //Debug.Log("DEBUG: Made rooms, making corridors...");

        MakeCorridors();

        //Debug.Log("DEBUG: Made corridors, making connections...");

        MakeConnections();

        //Debug.Log("DEBUG: Made connections, undoing corridors...");

        UndoCorridors();

        //Debug.Log("DEBUG: Undone corridors, building...");

        BuildDungeon();
        
        //Debug.Log("DEBUG: Dungeon done in: " + (System.DateTimeOffset.Now.ToUnixTimeMilliseconds() - startTime) + "ms");
    }
    
    private void ResetStructs()
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
        Random.InitState(seed);
        region = 0;
        tiles.Clear();
        rooms.Clear();
        regionTiles.Clear();
        regionTiles.Add(new List<Vector2Int>());
        deadEnds.Clear();
    }
   


    private void MakeRooms()
    {
        for(int i=0; i < roomDensity; ++i)
        {
            Vector2Int dimensions = new Vector2Int(Random.Range(minRoomWidth,maxRoomWidth+1),Random.Range(minRoomHeight,maxRoomHeight+1));

            //Debug.Log("DEBUG: \tMaking new room with dimensions " + dimensions);

            //check if dimensions are valid
            if (dimensions.x <= dungeonWidth && dimensions.y <= dungeonHeight) {
                Vector2Int coords = new Vector2Int(Random.Range(0, (dungeonWidth - dimensions.x) + 1), Random.Range(0, (dungeonHeight - dimensions.y) + 1));
                
                //Debug.Log("DEBUG: \tMaking new room with coords " + coords);

                //check if a new room with these dimensions and coords overlaps with any preexisting room
                Vector2Int topRight = new Vector2Int(coords.x+(dimensions.x-1),coords.y+(dimensions.y-1));

                if (!RoomOverlaps(coords, topRight))
                {
                    //add the room to the rooms list
                    rooms.Add(new Room(dimensions, coords, region));

                    //Debug.Log("DEBUG: \t\tAdded room at " + coords + " (with top right "+topRight+") with dimensions " + dimensions+" and region "+region);

                    //add the room tiles to the tiles list
                    for (int x = coords.x; x < coords.x+dimensions.x; ++x)
                    {
                        for (int y = coords.y; y < coords.y + dimensions.y; ++y)
                        {
                            byte walls = 0;
                            byte corners = 0;

                            if (x == coords.x)
                            {
                                walls = (byte)(walls | 8);
                                corners = (byte)(corners | 12);
                            }

                            if (x == coords.x + dimensions.x - 1)
                            {
                                walls = (byte)(walls | 2);
                                corners = (byte)(corners | 3);
                            }

                            if (y == coords.y)
                            {
                                walls = (byte)(walls|4);
                                corners = (byte)(corners|6);
                            }

                            if (y == coords.y + dimensions.y - 1)
                            {
                                walls = (byte)(walls | 1);
                                corners = (byte)(corners | 9);
                            }

                            //Debug.Log("DEBUG: \tAdded tile at ("+x+","+y+") with walls " + walls);

                            tiles[new Vector2Int(x,y)] = new Tile(new Vector2Int(x, y), region, walls, corners);
                            regionTiles[region].Add(new Vector2Int(x, y));
                        }
                    }

                    ++region;
                    regionTiles.Add(new List<Vector2Int>());
                }
            }
        }

        // Remove the last list because iif we dont make corridors, we will not need this last one
        //If we do however make corridors we would want to keep the last list but the creation of corridors is not necessary
        //So we delete it here and add it again before creating the corridors if we do
        regionTiles.RemoveAt(regionTiles.Count - 1); 
    }

    private bool RoomOverlaps(Vector2Int botLeft, Vector2Int topRight)
    {
        bool overlaps = false;
        for (int j = 0; j < rooms.Count; ++j)
        {
            Vector2Int currentTopRight = new Vector2Int(rooms[j].coords.x + (rooms[j].dimensions.x - 1), rooms[j].coords.y + (rooms[j].dimensions.y - 1));
            Vector2Int currentBotLeft = rooms[j].coords;
            if (!(botLeft.y > currentTopRight.y || rooms[j].coords.y > topRight.y || botLeft.x > currentTopRight.x || rooms[j].coords.x > topRight.x)) // if not (not overlapping)
            {
                overlaps = true;
                break;
            }
        }
        return overlaps;
    }



    private void MakeCorridors()
    {
        regionTiles.Add(new List<Vector2Int>());

        for (int x = 0; x < dungeonWidth; ++x)
        {
            for (int y = 0; y < dungeonHeight; ++y)
            {
                if(!tiles.ContainsKey(new Vector2Int(x, y)))
                {
                    //Debug.Log("DEBUG: Found empty tile for new corridor at ("+x+","+y+") with region "+region+", starting corridor...");

                    BuildCorridor(x,y);

                    //inc. region once were done generating corridor of this region
                    ++region;
                    regionTiles.Add(new List<Vector2Int>());
                }
            }
        }

        regionTiles.RemoveAt(regionTiles.Count - 1); // Remove the last list because it will not be used
    }

    private void BuildCorridor(int x,int y)
    {
        //Debug.Log("DEBUG: Build corridor called for (" + x + "," + y + ")");

        Stack<Vector2Int> coordStack=new Stack<Vector2Int>();

        Vector2Int startingCoords = new Vector2Int(x, y);
        tiles[startingCoords] = new Tile(startingCoords, region, 15, 15);
        regionTiles[region].Add(startingCoords);

        coordStack.Push(startingCoords);
        deadEnds.Add(startingCoords); // Because the start of a corridor is always a deadend

        bool previousAvailable=false;
        byte previousDirection = 0;
        bool previousDidGrowth = false;

        while (coordStack.Count > 0)
        {
            //Debug.Log("DEBUG: Building corridor from " + coordStack.Peek());

            Vector2Int currentCoord = coordStack.Peek();
            Vector2Int upCoords = new Vector2Int(currentCoord.x, currentCoord.y + 1);
            Vector2Int rightCoords = new Vector2Int(currentCoord.x+1, currentCoord.y);
            Vector2Int downCoords = new Vector2Int(currentCoord.x, currentCoord.y-1);
            Vector2Int leftCoords = new Vector2Int(currentCoord.x-1, currentCoord.y);

            List<byte> availableDirections = new List<byte>();
            Vector2Int[] availableCoords = new Vector2Int[4];
            previousAvailable = false;

            //check if individual adjacent coords are valid and available. If they are add them to the list
            if (CoordsValid(upCoords) && !tiles.ContainsKey(upCoords))
            {
                //Debug.Log("DEBUG: \tUpCoords "+ upCoords + " added to possible growth tile");

                availableDirections.Add(0);
                availableCoords[0] = upCoords;

                if (previousDirection == 0)
                    previousAvailable = true;
            }
            
            if (CoordsValid(rightCoords) && !tiles.ContainsKey(rightCoords))
            {
                //Debug.Log("DEBUG: \tRightCoords " + rightCoords + " added to possible growth tile");

                availableDirections.Add(1);
                availableCoords[1] = rightCoords;

                if (previousDirection == 1)
                    previousAvailable = true;
            }
            
            if (CoordsValid(downCoords) && !tiles.ContainsKey(downCoords))
            {
                //Debug.Log("DEBUG: \tDownCoords " + downCoords + " added to possible growth tile");

                availableDirections.Add(2);
                availableCoords[2] = downCoords;

                if (previousDirection == 2)
                    previousAvailable = true;
            }
            
            if (CoordsValid(leftCoords) && !tiles.ContainsKey(leftCoords))
            {
                //Debug.Log("DEBUG: \tLeftCoords " + leftCoords + " added to possible growth tile");

                availableDirections.Add(3);
                availableCoords[3] = leftCoords;

                if (previousDirection == 3)
                    previousAvailable = true;
            }

            //decide which direction to grow if there is at least 1 available
            if (availableDirections.Count > 0)
            {
                //Debug.Log("DEBUG: \t\tGrowth directions are available");

                previousDidGrowth = true;

                //if the previous is available and we want the previous direction
                if (previousAvailable && Random.value > windiness)
                {
                    //Debug.Log("DEBUG: \t\t\tUsing previous growth direction");

                    //grow in previous direction
                    switch (previousDirection)
                    {
                        case 0:
                            //grow up
                            tiles[currentCoord].walls = (byte)(tiles[currentCoord].walls & 14);

                            tiles[availableCoords[0]] = new Tile(availableCoords[0], region, 11, 15);
                            regionTiles[region].Add(availableCoords[0]);
                            coordStack.Push(availableCoords[0]);
                            break;
                        case 1:
                            //grow right
                            tiles[currentCoord].walls = (byte)(tiles[currentCoord].walls & 13);

                            tiles[availableCoords[1]] = new Tile(availableCoords[1], region, 7, 15);
                            regionTiles[region].Add(availableCoords[1]);
                            coordStack.Push(availableCoords[1]);
                            break;
                        case 2:
                            //grow down
                            tiles[currentCoord].walls = (byte)(tiles[currentCoord].walls & 11);

                            tiles[availableCoords[2]] = new Tile(availableCoords[2], region, 14, 15);
                            regionTiles[region].Add(availableCoords[2]);
                            coordStack.Push(availableCoords[2]);
                            break;
                        case 3:
                            //grow left
                            tiles[currentCoord].walls = (byte)(tiles[currentCoord].walls & 7);

                            tiles[availableCoords[3]] = new Tile(availableCoords[3], region, 13, 15);
                            regionTiles[region].Add(availableCoords[3]);
                            coordStack.Push(availableCoords[3]);
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    //Debug.Log("DEBUG: \t\t\tUsing random growth direction in available ones");

                    //choose a random direction in the ones available
                    int chosenAvailableDirection = availableDirections[Random.Range(0, availableDirections.Count - 1)];
                    Vector2Int chosenAvailableCoords = availableCoords[chosenAvailableDirection];

                    switch (chosenAvailableDirection)
                    {
                        case 0:
                            //Debug.Log("DEBUG: \t\t\t\tGrowing up");
                            //grow up, open the top wall of the current tile and make sure the bottom of the new tile is open
                            //once we change the walls of a preexisting tile we need to check the adjacent corners to make sure they are still needed and remove them if they arent
                            tiles[currentCoord].walls = (byte)(tiles[currentCoord].walls & 14);

                            tiles[chosenAvailableCoords] = new Tile(chosenAvailableCoords, region, 11, 15);
                            regionTiles[region].Add(chosenAvailableCoords);
                            coordStack.Push(chosenAvailableCoords);

                            previousDirection = 0;
                            break;
                        case 1:
                            //Debug.Log("DEBUG: \t\t\t\tGrowing right");
                            //grow right
                            tiles[currentCoord].walls = (byte)(tiles[currentCoord].walls & 13);

                            tiles[chosenAvailableCoords] = new Tile(chosenAvailableCoords, region, 7, 15);
                            regionTiles[region].Add(chosenAvailableCoords);
                            coordStack.Push(chosenAvailableCoords);

                            previousDirection = 1;
                            break;
                        case 2:
                            //Debug.Log("DEBUG: \t\t\t\tGrowing down");
                            //grow down
                            tiles[currentCoord].walls = (byte)(tiles[currentCoord].walls & 11);

                            tiles[chosenAvailableCoords] = new Tile(chosenAvailableCoords, region, 14, 15);
                            regionTiles[region].Add(chosenAvailableCoords);
                            coordStack.Push(chosenAvailableCoords);

                            previousDirection = 2;
                            break;
                        case 3:
                            //Debug.Log("DEBUG: \t\t\t\tGrowing left");
                            //grow left
                            tiles[currentCoord].walls = (byte)(tiles[currentCoord].walls & 7);

                            tiles[chosenAvailableCoords] = new Tile(chosenAvailableCoords, region, 13, 15);
                            regionTiles[region].Add(chosenAvailableCoords);
                            coordStack.Push(chosenAvailableCoords);

                            previousDirection = 3;
                            break;
                        default:
                            break;
                    }
                }
            }
            else // we hit a dead-end, add the coords to the dead-end list and pop it from the stack
            {
                //Debug.Log("DEBUG: \t\tGrowth directions are NOT available");

                if (previousDidGrowth)
                {
                    deadEnds.Add(currentCoord);

                    //Debug.Log("DEBUG: \t\t\tAdded "+currentCoord+" to deadends");
                }

                previousDidGrowth = false;
                coordStack.Pop();
            }
        }
    }



    private void MakeConnections()
    {
        //take the first room in the rooms list as the starting region (0)
        //find all possible connections (adjacent tiles that have a wall between them and are in different regions)
        //  for each tile, find all adj. tiles seperated by a wall and in a diff. region. If there are no adj. in a different region, remove this tile from the region-wise tiles list so we dont iterate over it again since its useless
        //choose a random connection out of all the possible one and create it
        //merge the two regions

        // Debug.Log("DEBUG: Creating regionConnectionsArray with size region: " + region);

        List<Connection>[] regionConnections = new List<Connection>[region];
        for(int i=0; i < regionConnections.Length; ++i)
        {
            regionConnections[i] = new List<Connection>();
        }

        //for every region
        for (int i = 0; i < regionTiles.Count; ++i)
        {
            //Debug.Log("DEBUG: Finding connections from region "+i);

            //for every tile in that region
            for (int j = 0; j < regionTiles[i].Count; ++j)
            {
                //Debug.Log("DEBUG: Checking if there are possible connections from source tile at "+ regionTiles[i][j]);

                Tile currentTile = tiles[regionTiles[i][j]];

                Vector2Int upTileCoords = new Vector2Int(regionTiles[i][j].x, regionTiles[i][j].y + 1);
                Vector2Int rightTileCoords = new Vector2Int(regionTiles[i][j].x + 1, regionTiles[i][j].y);
                Vector2Int downTileCoords = new Vector2Int(regionTiles[i][j].x, regionTiles[i][j].y - 1);
                Vector2Int leftTileCoords = new Vector2Int(regionTiles[i][j].x - 1, regionTiles[i][j].y);

                if (CoordsValid(upTileCoords) && tiles.ContainsKey(upTileCoords))
                {
                    Tile upTile = tiles[upTileCoords];

                    if ((currentTile.walls & 1) == 1 && upTile.region != currentTile.region)
                    {
                        //Debug.Log("DEBUG: \tAdding connection between " + currentTile.coords + " and " + upTile.coords + " at region " + currentTile.region);

                        regionConnections[currentTile.region].Add(new Connection(currentTile, upTile, 0));
                    }
                    /*
                    else
                    {
                        Debug.Log("DEBUG: \tUp is NOT a possible connection between " + currentTile.coords + " and " + upTile.coords+": walls: "+ currentTile.walls);
                    }
                    */
                }

                if (CoordsValid(rightTileCoords) && tiles.ContainsKey(rightTileCoords))
                {
                    Tile rightTile = tiles[rightTileCoords];

                    if (((currentTile.walls >> 1) & 1) == 1 && rightTile.region != currentTile.region)
                    {
                        //Debug.Log("DEBUG: \tAdding connection between " + currentTile.coords+" and "+rightTile.coords+" at region "+currentTile.region);

                        regionConnections[currentTile.region].Add(new Connection(currentTile, rightTile, 1));
                    }
                }

                if (CoordsValid(downTileCoords) && tiles.ContainsKey(downTileCoords))
                {
                    Tile downTile = tiles[downTileCoords];

                    if (((currentTile.walls >> 2) & 1) == 1 && downTile.region != currentTile.region)
                    {
                        //Debug.Log("DEBUG: \tAdding connection between " + currentTile.coords + " and " + downTile.coords + " at region " + currentTile.region);

                        regionConnections[currentTile.region].Add(new Connection(currentTile, downTile, 2));
                    }
                }

                if (CoordsValid(leftTileCoords) && tiles.ContainsKey(leftTileCoords))
                {
                    Tile leftTile = tiles[leftTileCoords];

                    if (((currentTile.walls >> 3) & 1) == 1 && leftTile.region != currentTile.region)
                    {
                        //Debug.Log("DEBUG: \tAdding connection between " + currentTile.coords + " and " + leftTile.coords + " at region " + currentTile.region);

                        regionConnections[currentTile.region].Add(new Connection(currentTile, leftTile, 3));
                    }
                }
            }
        }

        //populate a list of all region that still have possible connections. At the beginning, this is all the regions unless there is only one region
        List<int> nonEmptyRegionIndices = new List<int>();
        for (int i = 0; i < region; ++i)
        {
            if (regionConnections[i].Count != 0)
            {
                nonEmptyRegionIndices.Add(i);
            }
            /*
            else if (region != 1)
            {
                Debug.LogError("DEBUG: ERROR: Found region " + i + " without possible connections but there are more than 1 regions.");
            }
            */
        }

        //choose a random region in the nonEmptyRegionIndices list
        //choose a random connection from that region in the regionConnections list
        //make the connection
        //in the src and dest regions in regionConnections list, remove all connections that are in between those two region
        //  if that region is now empty of possible connections, remove its index from nonEmptyRegionIndices list

        while (nonEmptyRegionIndices.Count > 0)
        {
            //Debug.Log("DEBUG: Still non empty regions of connections, count: "+ nonEmptyRegionIndices.Count);

            /*
            for(int i=0;i<nonEmptyRegionIndices.Count; ++i)
            {
                Debug.Log("DEBUG: \tNon empty region index: " + nonEmptyRegionIndices[i]);
            }
            */

            int chosenRegion = nonEmptyRegionIndices[Random.Range(0, nonEmptyRegionIndices.Count)];

            //Debug.Log("DEBUG: Chosen region for connection: " + chosenRegion);

            //Debug.Log("DEBUG: Choosing connection out of region connections with count: "+regionConnections.Length);

            Connection chosenConnection = regionConnections[chosenRegion][Random.Range(0, regionConnections[chosenRegion].Count)];

            int srcRegion = chosenConnection.src.region;
            int destRegion = chosenConnection.dest.region;

            //Debug.Log("DEBUG: Chosen connection (direction: " + chosenConnection.direction + ") from tile " + chosenConnection.src.coords+" with region "+ srcRegion + ", to tile "+chosenConnection.dest.coords+" with region "+ destRegion);

            switch (chosenConnection.direction)
            {

                case 0:
                    chosenConnection.src.walls = (byte)(chosenConnection.src.walls & 14);
                    chosenConnection.dest.walls = (byte)(chosenConnection.dest.walls & 11);
                    break;
                case 1:
                    chosenConnection.src.walls = (byte)(chosenConnection.src.walls & 13);
                    chosenConnection.dest.walls = (byte)(chosenConnection.dest.walls & 7);
                    break;
                case 2:
                    chosenConnection.src.walls = (byte)(chosenConnection.src.walls & 11);
                    chosenConnection.dest.walls = (byte)(chosenConnection.dest.walls & 14);
                    break;
                case 3:
                    chosenConnection.src.walls = (byte)(chosenConnection.src.walls & 7);
                    chosenConnection.dest.walls = (byte)(chosenConnection.dest.walls & 13);
                    break;
                default:
                    break;
            }

            //Debug.Log("DEBUG: Removing all possible connections between those regions...");

            //remove all connections between those two regions from src region
            for (int i = 0; i < regionConnections[srcRegion].Count; ++i)
            {
                //Debug.Log("DEBUG: \tChecking possible connection between regions "+ srcRegion+" and "+ regionConnections[srcRegion][i].dest.region);

                if (regionConnections[srcRegion][i].dest.region == destRegion)
                {
                    regionConnections[srcRegion].RemoveAt(i--);//i-- because if we remove one, the next one will then also have index i so we want to reiterate over index i

                    //Debug.Log("DEBUG: \t\tPossible connection has corresponding regions, removed and new possible connections count from this src region is: " + regionConnections[srcRegion].Count);
                }
            }
            if (regionConnections[srcRegion].Count == 0)
            {
                //Debug.Log("DEBUG: \tPossible connections count from this src region is now 0, deleting index from non empty region indices list...");

                nonEmptyRegionIndices.Remove(srcRegion);
            }

            //remove all connections between those two regions from dest region
            for (int i = 0; i < regionConnections[destRegion].Count; ++i)
            {
                //Debug.Log("DEBUG: \tChecking possible connection between regions " + destRegion + " and " + regionConnections[destRegion][i].dest.region);

                if (regionConnections[destRegion][i].dest.region == srcRegion)
                {
                    regionConnections[destRegion].RemoveAt(i--);//i-- because if we remove one, the next one will then also have index i so we want to reiterate over index i

                    //Debug.Log("DEBUG: \t\tPossible connection has corresponding regions, removed and new possible connections count from this dest region is: " + regionConnections[destRegion].Count);
                }
            }
            if (regionConnections[destRegion].Count == 0)
            {
                //Debug.Log("DEBUG: \tPossible connections count from this dest region is now 0, deleting index from non empty region indices list...");

                nonEmptyRegionIndices.Remove(destRegion);
            }
            
            //merge the regions by setting the regions of the connections to the src or dest region of the connection we just made, given the boolean, and delete the connection if it has src==dest
            //go through all possible connections
            bool src = Random.Range(0, 2) == 0;

            //Debug.Log("DEBUG: Merging regions with src: "+src);

            for (int i=0; i < regionConnections.Length; ++i)
            {
                for(int j=0; j < regionConnections[i].Count; ++j)
                {
                    //Debug.Log("DEBUG: \tChecking connection (" + regionConnections[i][j].src.region + " -> " + regionConnections[i][j].dest.region + ") to see if its src or dest == "+ srcRegion + " or "+ destRegion);

                    //for each connection
                    if (regionConnections[i][j].src.region == srcRegion || regionConnections[i][j].src.region == destRegion)
                    {
                        //if src region of connection is == to either src or dest will be set to src or dest of chosen connection given the random boolean
                        regionConnections[i][j].src.region = (src ? srcRegion : destRegion);

                        //Debug.Log("DEBUG: \thas same src region as src or dest of what we just connected. Merged connection: ("+ regionConnections[i][j].src.region + " -> "+ regionConnections[i][j].dest.region + ")");
                    }

                    if (regionConnections[i][j].dest.region == srcRegion || regionConnections[i][j].dest.region == destRegion)
                    {
                        //if dest region of connection is == to either src or dest will be set to src or dest of chosen connection given the random boolean
                        regionConnections[i][j].dest.region = (src ? srcRegion : destRegion);

                        //Debug.Log("DEBUG: \thas same dest region as src or dest of what we just connected. Merged connection: (" + regionConnections[i][j].src.region + " -> " + regionConnections[i][j].dest.region + ")");
                    }

                    //if the src and dest regions of this connection are now the same, delete the connection
                    if(regionConnections[i][j].src.region == regionConnections[i][j].dest.region)
                    {
                        //Debug.Log("DEBUG: \tConnection now has same src and dest regions, removing...");

                        regionConnections[i].RemoveAt(j--);
                    }
                }

                //if there are no more possible connections from that region, remove the region from the nonEmptyRegionIndices list
                if (regionConnections[i].Count == 0)
                {
                    //Debug.Log("DEBUG: Possible connections count from region "+i+" is now 0, deleting index from non empty region indices list...");

                    nonEmptyRegionIndices.Remove(i);
                }
            }
        }
    }



    private void UndoCorridors()
    {
        for (int i=0; i < deadEnds.Count; ++i)
        {
            //Multiple deadends can be the same corridor, a deadend could have already been dealt with at the same time as a previous one
            //if thats the case, it will have been deleted from tiles already, so skip it.
            if (!tiles.ContainsKey(deadEnds[i])) 
                continue;

            Tile currentTile = tiles[deadEnds[i]];
            
            //Debug.Log("DEBUG: Undoing from deadend: " + deadEnds[i]);

            int openingCount=0;
            do
            {
                if (Random.value < undoOpenChance)
                {
                    List<int> possibleOpenDirections = new List<int>();
                    Vector2Int[] otherTileCoords = new Vector2Int[4];
                    
                    //Debug.Log("DEBUG: Will possibly make an opening from tile: " + currentTile.coords);

                    //check in all directions around the current tile
                    for (int j = 0; j < 4; ++j)
                    {
                        switch (j)
                        {
                            case 0:
                                otherTileCoords[j] = new Vector2Int(currentTile.coords.x, currentTile.coords.y + 1);
                                break;
                            case 1:
                                otherTileCoords[j] = new Vector2Int(currentTile.coords.x + 1, currentTile.coords.y);
                                break;
                            case 2:
                                otherTileCoords[j] = new Vector2Int(currentTile.coords.x, currentTile.coords.y - 1);
                                break;
                            case 3:
                                otherTileCoords[j] = new Vector2Int(currentTile.coords.x - 1, currentTile.coords.y);
                                break;
                            default:
                                break;
                        }

                        //if there is a wall and a tile in that direction
                        if (((currentTile.walls >> j) & 1) == 1 && tiles.ContainsKey(otherTileCoords[j])) 
                        {
                            //Debug.Log("DEBUG: \tOther tile added in direction: "+j+": " + otherTileCoords[j]);

                            possibleOpenDirections.Add(j);
                        }
                    }

                    //Debug.Log("DEBUG: Possible opening count: " + possibleOpenDirections.Count);

                    //if there are directions we can open in
                    if (possibleOpenDirections.Count > 0)
                    {
                        //get a random direction
                        int chosenDirection = possibleOpenDirections[Random.Range(0, possibleOpenDirections.Count)];

                        //Debug.Log("DEBUG: \t\tOpening to tile in direction: "+chosenDirection+": " + otherTileCoords[chosenDirection]);

                        //get the corresponding tile
                        Tile otherTile = tiles[otherTileCoords[chosenDirection]];

                        //make the opening
                        switch (chosenDirection)
                        {
                            case 0:
                                currentTile.walls = (byte)(currentTile.walls & 14);
                                otherTile.walls = (byte)(otherTile.walls & 11);
                                if (removePillars)
                                {
                                    UpdateCorners(currentTile.coords, 9);
                                }
                                break;
                            case 1:
                                currentTile.walls = (byte)(currentTile.walls & 13);
                                otherTile.walls = (byte)(otherTile.walls & 7);
                                if (removePillars)
                                {
                                    UpdateCorners(currentTile.coords, 3);
                                }
                                break;
                            case 2:
                                currentTile.walls = (byte)(currentTile.walls & 11);
                                otherTile.walls = (byte)(otherTile.walls & 14);
                                if (removePillars)
                                {
                                    UpdateCorners(currentTile.coords, 6);
                                }
                                break;
                            case 3:
                                currentTile.walls = (byte)(currentTile.walls & 7);
                                otherTile.walls = (byte)(otherTile.walls & 13);
                                if (removePillars)
                                {
                                    UpdateCorners(currentTile.coords, 12);
                                }
                                break;
                            default:
                                break;
                        }
                    }
                }

                //check the number of openings on this tile and store the last one. is there is only one, its direction will be stored
                openingCount = 0;
                int direction = -1;
                for(int j=0; j < 4; ++j)
                {
                    if (((currentTile.walls >> j) & 1)==0)
                    {
                        ++openingCount;
                        direction = j;
                    }
                }

                //if its a deadend, remove the tile
                if (openingCount <= 1)
                {
                    tiles.Remove(currentTile.coords);
                    switch (direction)
                    {
                        case 0:
                            currentTile = tiles[new Vector2Int(currentTile.coords.x, currentTile.coords.y + 1)];
                            currentTile.walls = (byte)(currentTile.walls | 4);
                            break;
                        case 1:
                            currentTile = tiles[new Vector2Int(currentTile.coords.x+1, currentTile.coords.y)];
                            currentTile.walls = (byte)(currentTile.walls | 8);
                            break;
                        case 2:
                            currentTile = tiles[new Vector2Int(currentTile.coords.x, currentTile.coords.y-1)];
                            currentTile.walls = (byte)(currentTile.walls | 1);
                            break;
                        case 3:
                            currentTile = tiles[new Vector2Int(currentTile.coords.x-1, currentTile.coords.y)];
                            currentTile.walls = (byte)(currentTile.walls | 2);
                            break;
                        default:
                            break;
                    }
                }
            } while (openingCount == 1);
        }
    }



    private void BuildDungeon()
    {
        GameObject emptyTileElems = new GameObject("EmptyTileElems");
        emptyTileElems.transform.parent = transform;

        for (int y=0; y < dungeonHeight; ++y)
        {
            for (int x = 0; x < dungeonWidth; ++x)
            {
                Vector2Int currentCoords = new Vector2Int(x, y);

                Vector2Int upCoords = new Vector2Int(currentCoords.x, currentCoords.y + 1);
                Vector2Int rightCoords = new Vector2Int(currentCoords.x + 1, currentCoords.y);
                Vector2Int topRightCoords = new Vector2Int(currentCoords.x + 1, currentCoords.y + 1);
                Vector2Int topLeftCoords = new Vector2Int(currentCoords.x - 1, currentCoords.y + 1);
                Vector2Int botCoords = new Vector2Int(currentCoords.x, currentCoords.y - 1);
                Vector2Int botLeftCoords = new Vector2Int(currentCoords.x - 1, currentCoords.y - 1);
                Vector2Int leftCoords = new Vector2Int(currentCoords.x - 1, currentCoords.y);

                Vector3 origin = new Vector3((wallThickness + tileSize) * x, 0, (wallThickness + tileSize) * y);



                //These are all the verts that will be used
                List<Vector3> vertList = new List<Vector3>();

                //base
                vertList.Add(origin);
                vertList.Add(new Vector3(origin.x + wallThickness, 0, origin.z));
                vertList.Add(new Vector3(origin.x + wallThickness + tileSize, 0, origin.z));
                vertList.Add(new Vector3(origin.x, 0, origin.z + wallThickness));
                vertList.Add(new Vector3(origin.x + wallThickness, 0, origin.z + wallThickness));
                vertList.Add(new Vector3(origin.x + wallThickness + tileSize, 0, origin.z + wallThickness));
                vertList.Add(new Vector3(origin.x, 0, origin.z + wallThickness + tileSize));
                vertList.Add(new Vector3(origin.x + wallThickness, 0, origin.z + wallThickness + tileSize));

                //base at wallheight
                vertList.Add(new Vector3(origin.x, wallHeight, origin.z));
                vertList.Add(new Vector3(origin.x + wallThickness, wallHeight, origin.z));
                vertList.Add(new Vector3(origin.x + wallThickness + tileSize, wallHeight, origin.z));
                vertList.Add(new Vector3(origin.x, wallHeight, origin.z + wallThickness));
                vertList.Add(new Vector3(origin.x + wallThickness, wallHeight, origin.z + wallThickness));
                vertList.Add(new Vector3(origin.x + wallThickness + tileSize, wallHeight, origin.z + wallThickness));
                vertList.Add(new Vector3(origin.x, wallHeight, origin.z + wallThickness + tileSize));
                vertList.Add(new Vector3(origin.x + wallThickness, wallHeight, origin.z + wallThickness + tileSize));

                //other
                vertList.Add(new Vector3(origin.x + wallThickness + tileSize, 0, origin.z));
                vertList.Add(new Vector3(origin.x + wallThickness * 2 + tileSize, 0, origin.z));
                vertList.Add(new Vector3(origin.x + wallThickness + tileSize, 0, origin.z + wallThickness));
                vertList.Add(new Vector3(origin.x + wallThickness * 2 + tileSize, 0, origin.z + wallThickness));
                vertList.Add(new Vector3(origin.x, 0, origin.z + wallThickness + tileSize));
                vertList.Add(new Vector3(origin.x + wallThickness, 0, origin.z + wallThickness + tileSize));
                vertList.Add(new Vector3(origin.x + wallThickness + tileSize, 0, origin.z + wallThickness + tileSize));
                vertList.Add(new Vector3(origin.x + wallThickness * 2 + tileSize, 0, origin.z + wallThickness + tileSize));
                vertList.Add(new Vector3(origin.x, 0, origin.z + wallThickness * 2 + tileSize));
                vertList.Add(new Vector3(origin.x + wallThickness, 0, origin.z + wallThickness * 2 + tileSize));
                vertList.Add(new Vector3(origin.x + wallThickness + tileSize, 0, origin.z + wallThickness * 2 + tileSize));
                vertList.Add(new Vector3(origin.x + wallThickness * 2 + tileSize, 0, origin.z + wallThickness * 2 + tileSize));

                //other at wallheight
                vertList.Add(new Vector3(origin.x + wallThickness + tileSize, wallHeight, origin.z));
                vertList.Add(new Vector3(origin.x + wallThickness * 2 + tileSize, wallHeight, origin.z));
                vertList.Add(new Vector3(origin.x + wallThickness + tileSize, wallHeight, origin.z + wallThickness));
                vertList.Add(new Vector3(origin.x + wallThickness * 2 + tileSize, wallHeight, origin.z + wallThickness));
                vertList.Add(new Vector3(origin.x, wallHeight, origin.z + wallThickness + tileSize));
                vertList.Add(new Vector3(origin.x + wallThickness, wallHeight, origin.z + wallThickness + tileSize));
                vertList.Add(new Vector3(origin.x + wallThickness + tileSize, wallHeight, origin.z + wallThickness + tileSize));
                vertList.Add(new Vector3(origin.x + wallThickness * 2 + tileSize, wallHeight, origin.z + wallThickness + tileSize));
                vertList.Add(new Vector3(origin.x, wallHeight, origin.z + wallThickness * 2 + tileSize));
                vertList.Add(new Vector3(origin.x + wallThickness, wallHeight, origin.z + wallThickness * 2 + tileSize));
                vertList.Add(new Vector3(origin.x + wallThickness + tileSize, wallHeight, origin.z + wallThickness * 2 + tileSize));
                vertList.Add(new Vector3(origin.x + wallThickness * 2 + tileSize, wallHeight, origin.z + wallThickness * 2 + tileSize));

                if (tiles.ContainsKey(currentCoords))
                {
                    //Debug.Log("DEBUG: Building tile " + currentCoords);

                    GameObject tile = new GameObject("Tile (" + x + "," + y + ")");
                    tile.transform.parent = transform;

                    //Debug.Log("DEBUG: \tTile has origin " + origin);

                    Vector3[] floorVertices = new Vector3[4];
                    floorVertices[0] = origin;
                    floorVertices[1] = new Vector3(origin.x, 0, origin.z + wallThickness + tileSize);
                    floorVertices[2] = new Vector3(origin.x + wallThickness + tileSize, 0, origin.z + wallThickness + tileSize);
                    floorVertices[3] = new Vector3(origin.x + wallThickness + tileSize, 0, origin.z);

                    //Debug.Log("DEBUG: \t\tVert 0: " + floorVertices[0]);
                    //Debug.Log("DEBUG: \t\tVert 1: " + floorVertices[1]);
                    //Debug.Log("DEBUG: \t\tVert 2: " + floorVertices[2]);
                    //Debug.Log("DEBUG: \t\tVert 3: " + floorVertices[3]);

                    int[] floorTriangles = { 0, 1, 2, 0, 2, 3 };

                    GameObject floor = new GameObject("Floor");
                    floor.transform.parent = tile.transform;
                    floor.AddComponent<MeshRenderer>().material= Mat;
                    MeshFilter floorMF = floor.AddComponent<MeshFilter>();
                    floorMF.mesh.vertices = floorVertices;
                    floorMF.mesh.triangles = floorTriangles;
                    floorMF.mesh.Optimize();
                    floorMF.mesh.RecalculateNormals();
                    BoxCollider floorCol = floor.AddComponent<BoxCollider>();
                    floorCol.center = new Vector3(origin.x + wallThickness / 2 + tileSize / 2, -0.05f, origin.z + wallThickness / 2 + tileSize / 2);
                    floorCol.size = new Vector3(tileSize + wallThickness, 0.1f, tileSize + wallThickness);

                    //if there is a wall to the left of this tile
                    if (((tiles[currentCoords].walls >> 3) & 1) == 1)
                    {
                        //Debug.Log("DEBUG: \tHas left wall");

                        List<Vector3> leftWallVerticesList = new List<Vector3>();

                        //in face
                        leftWallVerticesList.Add(vertList[4]);
                        leftWallVerticesList.Add(vertList[12]);
                        leftWallVerticesList.Add(vertList[15]);
                        leftWallVerticesList.Add(vertList[7]);

                        //top face
                        leftWallVerticesList.Add(vertList[12]);
                        leftWallVerticesList.Add(vertList[11]);
                        leftWallVerticesList.Add(vertList[14]);
                        leftWallVerticesList.Add(vertList[15]);

                        //out face
                        leftWallVerticesList.Add(vertList[6]);
                        leftWallVerticesList.Add(vertList[14]);
                        leftWallVerticesList.Add(vertList[11]);
                        leftWallVerticesList.Add(vertList[3]);

                        List<int> leftWallTrianglesList = new List<int>();
                        leftWallTrianglesList.Add(0);
                        leftWallTrianglesList.Add(1);
                        leftWallTrianglesList.Add(2);
                        leftWallTrianglesList.Add(0);
                        leftWallTrianglesList.Add(2);
                        leftWallTrianglesList.Add(3);
                        leftWallTrianglesList.Add(4);
                        leftWallTrianglesList.Add(5);
                        leftWallTrianglesList.Add(6);
                        leftWallTrianglesList.Add(4);
                        leftWallTrianglesList.Add(6);
                        leftWallTrianglesList.Add(7);
                        leftWallTrianglesList.Add(8);
                        leftWallTrianglesList.Add(9);
                        leftWallTrianglesList.Add(10);
                        leftWallTrianglesList.Add(8);
                        leftWallTrianglesList.Add(10);
                        leftWallTrianglesList.Add(11);
                        
                        //if the current tile has not bot left corner
                        if (((tiles[currentCoords].corners >> 2) & 1) == 0)
                        {
                            leftWallVerticesList.Add(vertList[3]); //-4
                            leftWallVerticesList.Add(vertList[11]); //-3
                            leftWallVerticesList.Add(vertList[12]); //-2
                            leftWallVerticesList.Add(vertList[4]); //-1

                            leftWallTrianglesList.Add(leftWallVerticesList.Count - 4);
                            leftWallTrianglesList.Add(leftWallVerticesList.Count - 3);
                            leftWallTrianglesList.Add(leftWallVerticesList.Count - 2);
                            leftWallTrianglesList.Add(leftWallVerticesList.Count - 4);
                            leftWallTrianglesList.Add(leftWallVerticesList.Count - 2);
                            leftWallTrianglesList.Add(leftWallVerticesList.Count - 1);
                        }

                        //if the tile above has no botleft corner
                        if (CoordsValid(upCoords) && tiles.ContainsKey(upCoords) && ((tiles[upCoords].corners >> 2) & 1) == 0)
                        {
                            leftWallVerticesList.Add(vertList[7]);
                            leftWallVerticesList.Add(vertList[15]);
                            leftWallVerticesList.Add(vertList[14]);
                            leftWallVerticesList.Add(vertList[6]);

                            leftWallTrianglesList.Add(leftWallVerticesList.Count - 4);
                            leftWallTrianglesList.Add(leftWallVerticesList.Count - 3);
                            leftWallTrianglesList.Add(leftWallVerticesList.Count - 2);
                            leftWallTrianglesList.Add(leftWallVerticesList.Count - 4);
                            leftWallTrianglesList.Add(leftWallVerticesList.Count - 2);
                            leftWallTrianglesList.Add(leftWallVerticesList.Count - 1);
                        }

                        GameObject leftWall = new GameObject("Left Wall");
                        leftWall.transform.parent = tile.transform;
                        leftWall.AddComponent<MeshRenderer>().material = Mat;
                        MeshFilter leftWallMF = leftWall.AddComponent<MeshFilter>();
                        leftWallMF.mesh.vertices = leftWallVerticesList.ToArray();
                        leftWallMF.mesh.triangles = leftWallTrianglesList.ToArray();
                        leftWallMF.mesh.Optimize();
                        leftWallMF.mesh.RecalculateNormals();
                        BoxCollider leftWallCol = leftWall.AddComponent<BoxCollider>();
                        leftWallCol.center = new Vector3(origin.x + wallThickness / 2, wallHeight / 2, origin.z + wallThickness + tileSize / 2);
                        leftWallCol.size = new Vector3(wallThickness, wallHeight, tileSize);
                    }
                    
                    //if there is a wall to the bottom of this tile
                    if (((tiles[currentCoords].walls >> 2) & 1) == 1)
                    {
                        //Debug.Log("DEBUG: \tHas bottom wall");

                        List<Vector3> bottomWallVerticesList = new List<Vector3>();
                        bottomWallVerticesList.Add(vertList[1]);
                        bottomWallVerticesList.Add(vertList[9]);
                        bottomWallVerticesList.Add(vertList[10]);
                        bottomWallVerticesList.Add(vertList[2]);
                        bottomWallVerticesList.Add(vertList[9]);
                        bottomWallVerticesList.Add(vertList[12]);
                        bottomWallVerticesList.Add(vertList[13]);
                        bottomWallVerticesList.Add(vertList[10]);
                        bottomWallVerticesList.Add(vertList[5]);
                        bottomWallVerticesList.Add(vertList[13]);
                        bottomWallVerticesList.Add(vertList[12]);
                        bottomWallVerticesList.Add(vertList[4]);

                        List<int> bottomWallTrianglesList = new List<int>();
                        bottomWallTrianglesList.Add(0);
                        bottomWallTrianglesList.Add(1);
                        bottomWallTrianglesList.Add(2);
                        bottomWallTrianglesList.Add(0);
                        bottomWallTrianglesList.Add(2);
                        bottomWallTrianglesList.Add(3);
                        bottomWallTrianglesList.Add(4);
                        bottomWallTrianglesList.Add(5);
                        bottomWallTrianglesList.Add(6);
                        bottomWallTrianglesList.Add(4);
                        bottomWallTrianglesList.Add(6);
                        bottomWallTrianglesList.Add(7);
                        bottomWallTrianglesList.Add(8);
                        bottomWallTrianglesList.Add(9);
                        bottomWallTrianglesList.Add(10);
                        bottomWallTrianglesList.Add(8);
                        bottomWallTrianglesList.Add(10);
                        bottomWallTrianglesList.Add(11);

                        //if the tile to the right has no botleft corner
                        if (CoordsValid(rightCoords) && tiles.ContainsKey(rightCoords) && ((tiles[rightCoords].corners >> 2) & 1) == 0)
                        {
                            bottomWallVerticesList.Add(vertList[2]);
                            bottomWallVerticesList.Add(vertList[10]);
                            bottomWallVerticesList.Add(vertList[13]);
                            bottomWallVerticesList.Add(vertList[5]);

                            bottomWallTrianglesList.Add(bottomWallVerticesList.Count - 4);
                            bottomWallTrianglesList.Add(bottomWallVerticesList.Count - 3);
                            bottomWallTrianglesList.Add(bottomWallVerticesList.Count - 2);
                            bottomWallTrianglesList.Add(bottomWallVerticesList.Count - 4);
                            bottomWallTrianglesList.Add(bottomWallVerticesList.Count - 2);
                            bottomWallTrianglesList.Add(bottomWallVerticesList.Count - 1);
                        }
                        
                        //if the current tile has not bot left corner
                        if (((tiles[currentCoords].corners >> 2) & 1) == 0)
                        {
                            bottomWallVerticesList.Add(vertList[4]);
                            bottomWallVerticesList.Add(vertList[12]);
                            bottomWallVerticesList.Add(vertList[9]);
                            bottomWallVerticesList.Add(vertList[1]);

                            bottomWallTrianglesList.Add(bottomWallVerticesList.Count - 4);
                            bottomWallTrianglesList.Add(bottomWallVerticesList.Count - 3);
                            bottomWallTrianglesList.Add(bottomWallVerticesList.Count - 2);
                            bottomWallTrianglesList.Add(bottomWallVerticesList.Count - 4);
                            bottomWallTrianglesList.Add(bottomWallVerticesList.Count - 2);
                            bottomWallTrianglesList.Add(bottomWallVerticesList.Count - 1);
                        }

                        GameObject bottomWall = new GameObject("Bottom Wall");
                        bottomWall.transform.parent = tile.transform;
                        bottomWall.AddComponent<MeshRenderer>().material = Mat;
                        MeshFilter bottomWallMF = bottomWall.AddComponent<MeshFilter>();
                        bottomWallMF.mesh.vertices = bottomWallVerticesList.ToArray();
                        bottomWallMF.mesh.triangles = bottomWallTrianglesList.ToArray();
                        bottomWallMF.mesh.Optimize();
                        bottomWallMF.mesh.RecalculateNormals();
                        BoxCollider bottomWallCol = bottomWall.AddComponent<BoxCollider>();
                        bottomWallCol.center = new Vector3(origin.x + wallThickness + tileSize / 2, wallHeight / 2, origin.z + wallThickness / 2);
                        bottomWallCol.size = new Vector3(tileSize, wallHeight, wallThickness);
                    }
                    
                    //if there is a bot left corner to this tile
                    if (((tiles[currentCoords].corners >> 2) & 1) == 1)
                    {
                        //Debug.Log("DEBUG: \tHas corner");

                        bool hasUpFace = ((tiles[currentCoords].walls >> 3) & 1) == 0;
                        bool hasRightFace = ((tiles[currentCoords].walls >> 2) & 1) == 0;
                        bool hasBotFace = (!tiles.ContainsKey(botCoords) || ((tiles[botCoords].walls >> 3) & 1) == 0)&&(tiles.ContainsKey(botCoords)||!tiles.ContainsKey(botLeftCoords));
                        bool hasLeftFace = !tiles.ContainsKey(leftCoords) || ((tiles[leftCoords].walls >> 2) & 1) == 0;

                        List<Vector3> cornerVerticesList = new List<Vector3>();
                        cornerVerticesList.Add(vertList[8]);
                        cornerVerticesList.Add(vertList[11]);
                        cornerVerticesList.Add(vertList[12]);
                        cornerVerticesList.Add(vertList[9]);

                        List<int> cornerTrianglesList = new List<int>();
                        cornerTrianglesList.Add(0);
                        cornerTrianglesList.Add(1);
                        cornerTrianglesList.Add(2);
                        cornerTrianglesList.Add(0);
                        cornerTrianglesList.Add(2);
                        cornerTrianglesList.Add(3);
                        
                        if (hasUpFace)
                        {
                            cornerVerticesList.Add(vertList[4]);
                            cornerVerticesList.Add(vertList[12]);
                            cornerVerticesList.Add(vertList[11]);
                            cornerVerticesList.Add(vertList[3]);

                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 3);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 1);
                        }
                        
                        if (hasRightFace)
                        {
                            cornerVerticesList.Add(vertList[1]);
                            cornerVerticesList.Add(vertList[9]);
                            cornerVerticesList.Add(vertList[12]);
                            cornerVerticesList.Add(vertList[4]);

                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 3);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 1);
                        }
                        
                        if (hasBotFace)
                        {
                            cornerVerticesList.Add(vertList[0]);
                            cornerVerticesList.Add(vertList[8]);
                            cornerVerticesList.Add(vertList[9]);
                            cornerVerticesList.Add(vertList[1]);

                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 3);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 1);
                        }
                        
                        if (hasLeftFace)
                        {
                            cornerVerticesList.Add(vertList[3]);
                            cornerVerticesList.Add(vertList[11]);
                            cornerVerticesList.Add(vertList[8]);
                            cornerVerticesList.Add(vertList[0]);

                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 3);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 1);
                        }

                        GameObject corner = new GameObject("Corner");
                        corner.transform.parent = tile.transform;
                        corner.AddComponent<MeshRenderer>().material = Mat;
                        MeshFilter cornerMF = corner.AddComponent<MeshFilter>();
                        cornerMF.mesh.vertices = cornerVerticesList.ToArray();
                        cornerMF.mesh.triangles = cornerTrianglesList.ToArray();
                        cornerMF.mesh.Optimize();
                        cornerMF.mesh.RecalculateNormals();
                        BoxCollider cornerCol = corner.AddComponent<BoxCollider>();
                        cornerCol.center = new Vector3(origin.x + wallThickness / 2, wallHeight / 2, origin.z + wallThickness / 2);
                        cornerCol.size = new Vector3(wallThickness, wallHeight, wallThickness);
                    }

                    //if the tile is at the very top of the dungeon we need to have top wall and top left corner
                    if (currentCoords.y == dungeonHeight - 1)
                    {
                        List<Vector3> topWallVerticesList = new List<Vector3>();
                        topWallVerticesList.Add(vertList[21]);
                        topWallVerticesList.Add(vertList[33]);
                        topWallVerticesList.Add(vertList[34]);
                        topWallVerticesList.Add(vertList[22]);
                        topWallVerticesList.Add(vertList[33]);
                        topWallVerticesList.Add(vertList[37]);
                        topWallVerticesList.Add(vertList[38]);
                        topWallVerticesList.Add(vertList[34]);
                        topWallVerticesList.Add(vertList[26]);
                        topWallVerticesList.Add(vertList[38]);
                        topWallVerticesList.Add(vertList[37]);
                        topWallVerticesList.Add(vertList[25]);

                        List<int> topWallTrianglesList = new List<int>();
                        topWallTrianglesList.Add(0);
                        topWallTrianglesList.Add(1);
                        topWallTrianglesList.Add(2);
                        topWallTrianglesList.Add(0);
                        topWallTrianglesList.Add(2);
                        topWallTrianglesList.Add(3);
                        topWallTrianglesList.Add(4);
                        topWallTrianglesList.Add(5);
                        topWallTrianglesList.Add(6);
                        topWallTrianglesList.Add(4);
                        topWallTrianglesList.Add(6);
                        topWallTrianglesList.Add(7);
                        topWallTrianglesList.Add(8);
                        topWallTrianglesList.Add(9);
                        topWallTrianglesList.Add(10);
                        topWallTrianglesList.Add(8);
                        topWallTrianglesList.Add(10);
                        topWallTrianglesList.Add(11);

                        //if the tile to the top right has no botleft corner
                        if (CoordsValid(topRightCoords) && tiles.ContainsKey(topRightCoords) && ((tiles[topRightCoords].corners >> 2) & 1) == 0)
                        {
                            topWallVerticesList.Add(vertList[23]);
                            topWallVerticesList.Add(vertList[35]);
                            topWallVerticesList.Add(vertList[39]);
                            topWallVerticesList.Add(vertList[27]);

                            topWallTrianglesList.Add(topWallVerticesList.Count - 4);
                            topWallTrianglesList.Add(topWallVerticesList.Count - 3);
                            topWallTrianglesList.Add(topWallVerticesList.Count - 2);
                            topWallTrianglesList.Add(topWallVerticesList.Count - 4);
                            topWallTrianglesList.Add(topWallVerticesList.Count - 2);
                            topWallTrianglesList.Add(topWallVerticesList.Count - 1);
                        }

                        //if the up tile has no bot left corner
                        if (CoordsValid(upCoords) && tiles.ContainsKey(upCoords) && ((tiles[upCoords].corners >> 2) & 1) == 0)
                        {
                            topWallVerticesList.Add(vertList[25]);
                            topWallVerticesList.Add(vertList[37]);
                            topWallVerticesList.Add(vertList[33]);
                            topWallVerticesList.Add(vertList[21]);

                            topWallTrianglesList.Add(topWallVerticesList.Count - 4);
                            topWallTrianglesList.Add(topWallVerticesList.Count - 3);
                            topWallTrianglesList.Add(topWallVerticesList.Count - 2);
                            topWallTrianglesList.Add(topWallVerticesList.Count - 4);
                            topWallTrianglesList.Add(topWallVerticesList.Count - 2);
                            topWallTrianglesList.Add(topWallVerticesList.Count - 1);
                        }

                        GameObject topWall = new GameObject("Top Wall");
                        topWall.transform.parent = tile.transform;
                        topWall.AddComponent<MeshRenderer>().material = Mat;
                        MeshFilter topWallMF = topWall.AddComponent<MeshFilter>();
                        topWallMF.mesh.vertices = topWallVerticesList.ToArray();
                        topWallMF.mesh.triangles = topWallTrianglesList.ToArray();
                        topWallMF.mesh.Optimize();
                        topWallMF.mesh.RecalculateNormals();
                        BoxCollider topWallCol = topWall.AddComponent<BoxCollider>();
                        topWallCol.center = new Vector3(origin.x + wallThickness + tileSize / 2, wallHeight / 2, origin.z + wallThickness + wallThickness / 2 + tileSize);
                        topWallCol.size = new Vector3(tileSize, wallHeight, wallThickness);



                        bool hasUpFace = true;
                        bool hasRightFace = false;
                        bool hasBotFace = ((tiles[currentCoords].walls >> 3) & 1) == 0;
                        bool hasLeftFace = !tiles.ContainsKey(leftCoords);

                        List<Vector3> cornerVerticesList = new List<Vector3>();
                        cornerVerticesList.Add(vertList[32]);
                        cornerVerticesList.Add(vertList[36]);
                        cornerVerticesList.Add(vertList[37]);
                        cornerVerticesList.Add(vertList[33]);

                        List<int> cornerTrianglesList = new List<int>();
                        cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 3);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 1);

                        if (hasUpFace)
                        {
                            cornerVerticesList.Add(vertList[25]);
                            cornerVerticesList.Add(vertList[37]);
                            cornerVerticesList.Add(vertList[36]);
                            cornerVerticesList.Add(vertList[24]);

                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 3);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 1);
                        }

                        if (hasRightFace)
                        {
                            cornerVerticesList.Add(vertList[21]);
                            cornerVerticesList.Add(vertList[33]);
                            cornerVerticesList.Add(vertList[37]);
                            cornerVerticesList.Add(vertList[25]);

                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 3);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 1);
                        }

                        if (hasBotFace)
                        {
                            cornerVerticesList.Add(vertList[20]);
                            cornerVerticesList.Add(vertList[32]);
                            cornerVerticesList.Add(vertList[33]);
                            cornerVerticesList.Add(vertList[21]);

                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 3);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 1);
                        }

                        if (hasLeftFace)
                        {
                            cornerVerticesList.Add(vertList[24]);
                            cornerVerticesList.Add(vertList[36]);
                            cornerVerticesList.Add(vertList[32]);
                            cornerVerticesList.Add(vertList[20]);

                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 3);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 1);
                        }

                        GameObject corner = new GameObject("Corner");
                        corner.transform.parent = emptyTileElems.transform;
                        corner.AddComponent<MeshRenderer>().material = Mat;
                        MeshFilter cornerMF = corner.AddComponent<MeshFilter>();
                        cornerMF.mesh.vertices = cornerVerticesList.ToArray();
                        cornerMF.mesh.triangles = cornerTrianglesList.ToArray();
                        cornerMF.mesh.Optimize();
                        cornerMF.mesh.RecalculateNormals();
                        BoxCollider cornerCol = corner.AddComponent<BoxCollider>();
                        cornerCol.center = new Vector3(origin.x + wallThickness / 2, wallHeight / 2, origin.z + wallThickness + wallThickness / 2 + tileSize);
                        cornerCol.size = new Vector3(wallThickness, wallHeight, wallThickness);
                    }

                    //if the tile is at the very right of the grid we need to have right wall and bot right corner
                    if(currentCoords.x == dungeonWidth - 1)
                    {
                        List<Vector3> rightWallVerticesList = new List<Vector3>();
                        rightWallVerticesList.Add(vertList[22]);
                        rightWallVerticesList.Add(vertList[34]);
                        rightWallVerticesList.Add(vertList[30]);
                        rightWallVerticesList.Add(vertList[18]);
                        rightWallVerticesList.Add(vertList[34]);
                        rightWallVerticesList.Add(vertList[35]);
                        rightWallVerticesList.Add(vertList[31]);
                        rightWallVerticesList.Add(vertList[30]);
                        rightWallVerticesList.Add(vertList[19]);
                        rightWallVerticesList.Add(vertList[31]);
                        rightWallVerticesList.Add(vertList[35]);
                        rightWallVerticesList.Add(vertList[23]);

                        List<int> rightWallTrianglesList = new List<int>();
                        rightWallTrianglesList.Add(0);
                        rightWallTrianglesList.Add(1);
                        rightWallTrianglesList.Add(2);
                        rightWallTrianglesList.Add(0);
                        rightWallTrianglesList.Add(2);
                        rightWallTrianglesList.Add(3);
                        rightWallTrianglesList.Add(4);
                        rightWallTrianglesList.Add(5);
                        rightWallTrianglesList.Add(6);
                        rightWallTrianglesList.Add(4);
                        rightWallTrianglesList.Add(6);
                        rightWallTrianglesList.Add(7);
                        rightWallTrianglesList.Add(8);
                        rightWallTrianglesList.Add(9);
                        rightWallTrianglesList.Add(10);
                        rightWallTrianglesList.Add(8);
                        rightWallTrianglesList.Add(10);
                        rightWallTrianglesList.Add(11);

                        GameObject rightWall = new GameObject("Right Wall");
                        rightWall.transform.parent = tile.transform;
                        rightWall.AddComponent<MeshRenderer>().material = Mat;
                        MeshFilter rightWallMF = rightWall.AddComponent<MeshFilter>();
                        rightWallMF.mesh.vertices = rightWallVerticesList.ToArray();
                        rightWallMF.mesh.triangles = rightWallTrianglesList.ToArray();
                        rightWallMF.mesh.Optimize();
                        rightWallMF.mesh.RecalculateNormals();
                        BoxCollider rightWallCol = rightWall.AddComponent<BoxCollider>();
                        rightWallCol.center = new Vector3(origin.x + wallThickness + wallThickness / 2 + tileSize, wallHeight / 2, origin.z + wallThickness + tileSize / 2);
                        rightWallCol.size = new Vector3(wallThickness, wallHeight, tileSize);


                        //corner
                        bool hasUpFace = false;
                        bool hasRightFace = true;
                        bool hasBotFace = !tiles.ContainsKey(botCoords);
                        bool hasLeftFace = ((tiles[currentCoords].walls >> 2) & 1) == 0;

                        List<Vector3> cornerVerticesList = new List<Vector3>();
                        cornerVerticesList.Add(vertList[28]);
                        cornerVerticesList.Add(vertList[30]);
                        cornerVerticesList.Add(vertList[31]);
                        cornerVerticesList.Add(vertList[29]);

                        List<int> cornerTrianglesList = new List<int>();

                        cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 3);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 1);

                        if (hasUpFace)
                        {
                            cornerVerticesList.Add(vertList[19]);
                            cornerVerticesList.Add(vertList[31]);
                            cornerVerticesList.Add(vertList[30]);
                            cornerVerticesList.Add(vertList[18]);

                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 3);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 1);
                        }

                        if (hasRightFace)
                        {
                            cornerVerticesList.Add(vertList[17]);
                            cornerVerticesList.Add(vertList[29]);
                            cornerVerticesList.Add(vertList[31]);
                            cornerVerticesList.Add(vertList[19]);

                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 3);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 1);
                        }

                        if (hasBotFace)
                        {
                            cornerVerticesList.Add(vertList[16]);
                            cornerVerticesList.Add(vertList[28]);
                            cornerVerticesList.Add(vertList[29]);
                            cornerVerticesList.Add(vertList[17]);

                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 3);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 1);
                        }

                        if (hasLeftFace)
                        {
                            cornerVerticesList.Add(vertList[18]);
                            cornerVerticesList.Add(vertList[30]);
                            cornerVerticesList.Add(vertList[28]);
                            cornerVerticesList.Add(vertList[16]);

                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 3);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 1);
                        }

                        GameObject corner = new GameObject("Corner");
                        corner.transform.parent = tile.transform;
                        corner.AddComponent<MeshRenderer>().material = Mat;
                        MeshFilter cornerMF = corner.AddComponent<MeshFilter>();
                        cornerMF.mesh.vertices = cornerVerticesList.ToArray();
                        cornerMF.mesh.triangles = cornerTrianglesList.ToArray();
                        cornerMF.mesh.Optimize();
                        cornerMF.mesh.RecalculateNormals();
                        BoxCollider cornerCol = corner.AddComponent<BoxCollider>();
                        cornerCol.center = new Vector3(origin.x + wallThickness + wallThickness / 2 + tileSize, wallHeight / 2, origin.z + wallThickness / 2);
                        cornerCol.size = new Vector3(wallThickness, wallHeight, wallThickness);
                    }

                    //If the tile if at topright of dun we need to make the top right corner
                    if (currentCoords.y == dungeonHeight - 1 && currentCoords.x == dungeonWidth - 1)
                    {
                        List<Vector3> cornerVerticesList = new List<Vector3>();
                        cornerVerticesList.Add(vertList[34]);
                        cornerVerticesList.Add(vertList[38]);
                        cornerVerticesList.Add(vertList[39]);
                        cornerVerticesList.Add(vertList[35]);

                        List<int> cornerTrianglesList = new List<int>();
                        cornerTrianglesList.Add(0);
                        cornerTrianglesList.Add(1);
                        cornerTrianglesList.Add(2);
                        cornerTrianglesList.Add(0);
                        cornerTrianglesList.Add(2);
                        cornerTrianglesList.Add(3);

                        cornerVerticesList.Add(vertList[27]);
                        cornerVerticesList.Add(vertList[39]);
                        cornerVerticesList.Add(vertList[38]);
                        cornerVerticesList.Add(vertList[26]);

                        cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 3);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 1);

                        cornerVerticesList.Add(vertList[23]);
                        cornerVerticesList.Add(vertList[35]);
                        cornerVerticesList.Add(vertList[39]);
                        cornerVerticesList.Add(vertList[27]);

                        cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 3);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 1);

                        GameObject corner = new GameObject("Corner");
                        corner.transform.parent = emptyTileElems.transform;
                        corner.AddComponent<MeshRenderer>().material = Mat;
                        MeshFilter cornerMF = corner.AddComponent<MeshFilter>();
                        cornerMF.mesh.vertices = cornerVerticesList.ToArray();
                        cornerMF.mesh.triangles = cornerTrianglesList.ToArray();
                        cornerMF.mesh.Optimize();
                        cornerMF.mesh.RecalculateNormals();
                        BoxCollider cornerCol = corner.AddComponent<BoxCollider>();
                        cornerCol.center = new Vector3(origin.x + wallThickness + wallThickness / 2 + tileSize, wallHeight / 2, origin.z + wallThickness / 2);
                        cornerCol.size = new Vector3(wallThickness, wallHeight, wallThickness);
                    }
                }
                else // The tile does not exist, this means that the bot wall, left wall and corner MUST be built if the tiles in the corresponding directions exist.
                {
                    //Left wall
                    if (tiles.ContainsKey(leftCoords))
                    {
                        List<Vector3> leftWallVerticesList = new List<Vector3>();
                        //in face
                        leftWallVerticesList.Add(vertList[4]);
                        leftWallVerticesList.Add(vertList[12]);
                        leftWallVerticesList.Add(vertList[15]);
                        leftWallVerticesList.Add(vertList[7]);

                        //top face
                        leftWallVerticesList.Add(vertList[12]);
                        leftWallVerticesList.Add(vertList[11]);
                        leftWallVerticesList.Add(vertList[14]);
                        leftWallVerticesList.Add(vertList[15]);

                        //out face
                        leftWallVerticesList.Add(vertList[6]);
                        leftWallVerticesList.Add(vertList[14]);
                        leftWallVerticesList.Add(vertList[11]);
                        leftWallVerticesList.Add(vertList[3]);

                        List<int> leftWallTrianglesList = new List<int>();
                        leftWallTrianglesList.Add(0);
                        leftWallTrianglesList.Add(1);
                        leftWallTrianglesList.Add(2);
                        leftWallTrianglesList.Add(0);
                        leftWallTrianglesList.Add(2);
                        leftWallTrianglesList.Add(3);
                        leftWallTrianglesList.Add(4);
                        leftWallTrianglesList.Add(5);
                        leftWallTrianglesList.Add(6);
                        leftWallTrianglesList.Add(4);
                        leftWallTrianglesList.Add(6);
                        leftWallTrianglesList.Add(7);
                        leftWallTrianglesList.Add(8);
                        leftWallTrianglesList.Add(9);
                        leftWallTrianglesList.Add(10);
                        leftWallTrianglesList.Add(8);
                        leftWallTrianglesList.Add(10);
                        leftWallTrianglesList.Add(11);

                        //if the tile above has no botleft corner
                        if (CoordsValid(upCoords) && tiles.ContainsKey(upCoords) && ((tiles[upCoords].corners >> 2) & 1) == 0)
                        {
                            leftWallVerticesList.Add(vertList[7]);
                            leftWallVerticesList.Add(vertList[15]);
                            leftWallVerticesList.Add(vertList[14]);
                            leftWallVerticesList.Add(vertList[6]);

                            leftWallTrianglesList.Add(leftWallVerticesList.Count - 4);
                            leftWallTrianglesList.Add(leftWallVerticesList.Count - 3);
                            leftWallTrianglesList.Add(leftWallVerticesList.Count - 2);
                            leftWallTrianglesList.Add(leftWallVerticesList.Count - 4);
                            leftWallTrianglesList.Add(leftWallVerticesList.Count - 2);
                            leftWallTrianglesList.Add(leftWallVerticesList.Count - 1);
                        }

                        GameObject leftWall = new GameObject("Left Wall");
                        leftWall.transform.parent = emptyTileElems.transform;
                        leftWall.AddComponent<MeshRenderer>().material = Mat;
                        MeshFilter leftWallMF = leftWall.AddComponent<MeshFilter>();
                        leftWallMF.mesh.vertices = leftWallVerticesList.ToArray();
                        leftWallMF.mesh.triangles = leftWallTrianglesList.ToArray();
                        leftWallMF.mesh.Optimize();
                        leftWallMF.mesh.RecalculateNormals();
                        BoxCollider leftWallCol = leftWall.AddComponent<BoxCollider>();
                        leftWallCol.center = new Vector3(origin.x + wallThickness / 2, wallHeight / 2, origin.z + wallThickness + tileSize / 2);
                        leftWallCol.size = new Vector3(wallThickness, wallHeight, tileSize);
                    }

                    //Bottom wall
                    if (tiles.ContainsKey(botCoords))
                    {
                        List<Vector3> bottomWallVerticesList = new List<Vector3>();
                        bottomWallVerticesList.Add(vertList[1]);
                        bottomWallVerticesList.Add(vertList[9]);
                        bottomWallVerticesList.Add(vertList[10]);
                        bottomWallVerticesList.Add(vertList[2]);
                        bottomWallVerticesList.Add(vertList[9]);
                        bottomWallVerticesList.Add(vertList[12]);
                        bottomWallVerticesList.Add(vertList[13]);
                        bottomWallVerticesList.Add(vertList[10]);
                        bottomWallVerticesList.Add(vertList[5]);
                        bottomWallVerticesList.Add(vertList[13]);
                        bottomWallVerticesList.Add(vertList[12]);
                        bottomWallVerticesList.Add(vertList[4]);

                        List<int> bottomWallTrianglesList = new List<int>();
                        bottomWallTrianglesList.Add(0);
                        bottomWallTrianglesList.Add(1);
                        bottomWallTrianglesList.Add(2);
                        bottomWallTrianglesList.Add(0);
                        bottomWallTrianglesList.Add(2);
                        bottomWallTrianglesList.Add(3);
                        bottomWallTrianglesList.Add(4);
                        bottomWallTrianglesList.Add(5);
                        bottomWallTrianglesList.Add(6);
                        bottomWallTrianglesList.Add(4);
                        bottomWallTrianglesList.Add(6);
                        bottomWallTrianglesList.Add(7);
                        bottomWallTrianglesList.Add(8);
                        bottomWallTrianglesList.Add(9);
                        bottomWallTrianglesList.Add(10);
                        bottomWallTrianglesList.Add(8);
                        bottomWallTrianglesList.Add(10);
                        bottomWallTrianglesList.Add(11);

                        //if the tile to the right has no botleft corner
                        if (CoordsValid(rightCoords) && tiles.ContainsKey(rightCoords) && ((tiles[rightCoords].corners >> 2) & 1) == 0)
                        {
                            bottomWallVerticesList.Add(vertList[2]);
                            bottomWallVerticesList.Add(vertList[10]);
                            bottomWallVerticesList.Add(vertList[13]);
                            bottomWallVerticesList.Add(vertList[5]);

                            bottomWallTrianglesList.Add(bottomWallVerticesList.Count - 4);
                            bottomWallTrianglesList.Add(bottomWallVerticesList.Count - 3);
                            bottomWallTrianglesList.Add(bottomWallVerticesList.Count - 2);
                            bottomWallTrianglesList.Add(bottomWallVerticesList.Count - 4);
                            bottomWallTrianglesList.Add(bottomWallVerticesList.Count - 2);
                            bottomWallTrianglesList.Add(bottomWallVerticesList.Count - 1);
                        }

                        GameObject bottomWall = new GameObject("Bottom Wall");
                        bottomWall.transform.parent = emptyTileElems.transform;
                        bottomWall.AddComponent<MeshRenderer>().material = Mat;
                        MeshFilter bottomWallMF = bottomWall.AddComponent<MeshFilter>();
                        bottomWallMF.mesh.vertices = bottomWallVerticesList.ToArray();
                        bottomWallMF.mesh.triangles = bottomWallTrianglesList.ToArray();
                        bottomWallMF.mesh.Optimize();
                        bottomWallMF.mesh.RecalculateNormals();
                        BoxCollider bottomWallCol = bottomWall.AddComponent<BoxCollider>();
                        bottomWallCol.center = new Vector3(origin.x + wallThickness + tileSize / 2, wallHeight / 2, origin.z + wallThickness / 2);
                        bottomWallCol.size = new Vector3(tileSize, wallHeight, wallThickness);
                    }

                    //Corner
                    if (tiles.ContainsKey(botLeftCoords)||tiles.ContainsKey(leftCoords)||tiles.ContainsKey(botCoords))
                    {
                        bool hasUpFace = !tiles.ContainsKey(leftCoords);
                        bool hasRightFace = !tiles.ContainsKey(botCoords);
                        bool hasBotFace = (!tiles.ContainsKey(botCoords) || ((tiles[botCoords].walls >> 3) & 1) == 0)&&(tiles.ContainsKey(botCoords)||!tiles.ContainsKey(botLeftCoords));
                        bool hasLeftFace = (!tiles.ContainsKey(leftCoords) || ((tiles[leftCoords].walls >> 2) & 1) == 0)&&(tiles.ContainsKey(leftCoords)||!tiles.ContainsKey(botLeftCoords));

                        List<Vector3> cornerVerticesList = new List<Vector3>();
                        cornerVerticesList.Add(vertList[8]);
                        cornerVerticesList.Add(vertList[11]);
                        cornerVerticesList.Add(vertList[12]);
                        cornerVerticesList.Add(vertList[9]);

                        List<int> cornerTrianglesList = new List<int>();
                        cornerTrianglesList.Add(0);
                        cornerTrianglesList.Add(1);
                        cornerTrianglesList.Add(2);
                        cornerTrianglesList.Add(0);
                        cornerTrianglesList.Add(2);
                        cornerTrianglesList.Add(3);

                        if (hasUpFace)
                        {
                            cornerVerticesList.Add(vertList[4]);
                            cornerVerticesList.Add(vertList[12]);
                            cornerVerticesList.Add(vertList[11]);
                            cornerVerticesList.Add(vertList[3]);

                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 3);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 1);
                        }

                        if (hasRightFace)
                        {
                            cornerVerticesList.Add(vertList[1]);
                            cornerVerticesList.Add(vertList[9]);
                            cornerVerticesList.Add(vertList[12]);
                            cornerVerticesList.Add(vertList[4]);

                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 3);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 1);
                        }

                        if (hasBotFace)
                        {
                            cornerVerticesList.Add(vertList[0]);
                            cornerVerticesList.Add(vertList[8]);
                            cornerVerticesList.Add(vertList[9]);
                            cornerVerticesList.Add(vertList[1]);

                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 3);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 1);
                        }

                        if (hasLeftFace)
                        {
                            cornerVerticesList.Add(vertList[3]);
                            cornerVerticesList.Add(vertList[11]);
                            cornerVerticesList.Add(vertList[8]);
                            cornerVerticesList.Add(vertList[0]);

                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 3);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 1);
                        }

                        GameObject corner = new GameObject("Corner");
                        corner.transform.parent = emptyTileElems.transform;
                        corner.AddComponent<MeshRenderer>().material = Mat;
                        MeshFilter cornerMF = corner.AddComponent<MeshFilter>();
                        cornerMF.mesh.vertices = cornerVerticesList.ToArray();
                        cornerMF.mesh.triangles = cornerTrianglesList.ToArray();
                        cornerMF.mesh.Optimize();
                        cornerMF.mesh.RecalculateNormals();
                        BoxCollider cornerCol = corner.AddComponent<BoxCollider>();
                        cornerCol.center = new Vector3(origin.x + wallThickness / 2, wallHeight / 2, origin.z + wallThickness / 2);
                        cornerCol.size = new Vector3(wallThickness, wallHeight, wallThickness);
                    }


                    //If the empty tile is at top of dungeon and leftCoords exist we need to make a topleft corner
                    if (currentCoords.y == dungeonHeight - 1 && tiles.ContainsKey(leftCoords))
                    {
                        bool hasUpFace = true;
                        bool hasRightFace = true;
                        bool hasBotFace = false;
                        bool hasLeftFace = false;

                        List<Vector3> cornerVerticesList = new List<Vector3>();
                        cornerVerticesList.Add(vertList[32]);
                        cornerVerticesList.Add(vertList[36]);
                        cornerVerticesList.Add(vertList[37]);
                        cornerVerticesList.Add(vertList[33]);

                        List<int> cornerTrianglesList = new List<int>();
                        cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 3);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 1);

                        if (hasUpFace)
                        {
                            cornerVerticesList.Add(vertList[25]);
                            cornerVerticesList.Add(vertList[37]);
                            cornerVerticesList.Add(vertList[36]);
                            cornerVerticesList.Add(vertList[24]);

                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 3);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 1);
                        }

                        if (hasRightFace)
                        {
                            cornerVerticesList.Add(vertList[21]);
                            cornerVerticesList.Add(vertList[33]);
                            cornerVerticesList.Add(vertList[37]);
                            cornerVerticesList.Add(vertList[25]);

                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 3);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 1);
                        }

                        if (hasBotFace)
                        {
                            cornerVerticesList.Add(vertList[20]);
                            cornerVerticesList.Add(vertList[32]);
                            cornerVerticesList.Add(vertList[33]);
                            cornerVerticesList.Add(vertList[21]);

                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 3);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 1);
                        }

                        if (hasLeftFace)
                        {
                            cornerVerticesList.Add(vertList[24]);
                            cornerVerticesList.Add(vertList[36]);
                            cornerVerticesList.Add(vertList[32]);
                            cornerVerticesList.Add(vertList[20]);

                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 3);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                            cornerTrianglesList.Add(cornerVerticesList.Count - 1);
                        }

                        GameObject corner = new GameObject("Corner");
                        corner.transform.parent = emptyTileElems.transform;
                        corner.AddComponent<MeshRenderer>().material = Mat;
                        MeshFilter cornerMF = corner.AddComponent<MeshFilter>();
                        cornerMF.mesh.vertices = cornerVerticesList.ToArray();
                        cornerMF.mesh.triangles = cornerTrianglesList.ToArray();
                        cornerMF.mesh.Optimize();
                        cornerMF.mesh.RecalculateNormals();
                        BoxCollider cornerCol = corner.AddComponent<BoxCollider>();
                        cornerCol.center = new Vector3(origin.x + wallThickness / 2, wallHeight / 2, origin.z + wallThickness + wallThickness / 2 + tileSize);
                        cornerCol.size = new Vector3(wallThickness, wallHeight, wallThickness);
                    }


                    //If the empty tile is at right of dungeon and botCoords exist we need to make a botRight corner
                    if (currentCoords.x == dungeonWidth - 1 && tiles.ContainsKey(botCoords))
                    {
                        List<Vector3> cornerVerticesList = new List<Vector3>();
                        cornerVerticesList.Add(vertList[28]);
                        cornerVerticesList.Add(vertList[30]);
                        cornerVerticesList.Add(vertList[31]);
                        cornerVerticesList.Add(vertList[29]);

                        List<int> cornerTrianglesList = new List<int>();

                        cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 3);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 1);
                        
                        cornerVerticesList.Add(vertList[19]);
                        cornerVerticesList.Add(vertList[31]);
                        cornerVerticesList.Add(vertList[30]);
                        cornerVerticesList.Add(vertList[18]);

                        cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 3);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 1);
                        
                        cornerVerticesList.Add(vertList[17]);
                        cornerVerticesList.Add(vertList[29]);
                        cornerVerticesList.Add(vertList[31]);
                        cornerVerticesList.Add(vertList[19]);

                        cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 3);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 1);

                        GameObject corner = new GameObject("Corner");
                        corner.transform.parent = emptyTileElems.transform;
                        corner.AddComponent<MeshRenderer>().material = Mat;
                        MeshFilter cornerMF = corner.AddComponent<MeshFilter>();
                        cornerMF.mesh.vertices = cornerVerticesList.ToArray();
                        cornerMF.mesh.triangles = cornerTrianglesList.ToArray();
                        cornerMF.mesh.Optimize();
                        cornerMF.mesh.RecalculateNormals();
                        BoxCollider cornerCol = corner.AddComponent<BoxCollider>();
                        cornerCol.center = new Vector3(origin.x + wallThickness + wallThickness / 2 + tileSize, wallHeight / 2, origin.z + wallThickness / 2);
                        cornerCol.size = new Vector3(wallThickness, wallHeight, wallThickness);
                    }

                    //If the empty tile is a bottom of dun and left coords exists, we need to do botLeft corner
                    if (currentCoords.y == 0 && tiles.ContainsKey(leftCoords))
                    {
                        List<Vector3> cornerVerticesList = new List<Vector3>();
                        cornerVerticesList.Add(vertList[8]);
                        cornerVerticesList.Add(vertList[11]);
                        cornerVerticesList.Add(vertList[12]);
                        cornerVerticesList.Add(vertList[9]);

                        List<int> cornerTrianglesList = new List<int>();
                        cornerTrianglesList.Add(0);
                        cornerTrianglesList.Add(1);
                        cornerTrianglesList.Add(2);
                        cornerTrianglesList.Add(0);
                        cornerTrianglesList.Add(2);
                        cornerTrianglesList.Add(3);
                        
                        cornerVerticesList.Add(vertList[1]);
                        cornerVerticesList.Add(vertList[9]);
                        cornerVerticesList.Add(vertList[12]);
                        cornerVerticesList.Add(vertList[4]);

                        cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 3);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 1);

                        cornerVerticesList.Add(vertList[0]);
                        cornerVerticesList.Add(vertList[8]);
                        cornerVerticesList.Add(vertList[9]);
                        cornerVerticesList.Add(vertList[1]);

                        cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 3);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 4);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 2);
                        cornerTrianglesList.Add(cornerVerticesList.Count - 1);
                        
                        GameObject corner = new GameObject("Corner");
                        corner.transform.parent = emptyTileElems.transform;
                        corner.AddComponent<MeshRenderer>().material = Mat;
                        MeshFilter cornerMF = corner.AddComponent<MeshFilter>();
                        cornerMF.mesh.vertices = cornerVerticesList.ToArray();
                        cornerMF.mesh.triangles = cornerTrianglesList.ToArray();
                        cornerMF.mesh.Optimize();
                        cornerMF.mesh.RecalculateNormals();
                        BoxCollider cornerCol = corner.AddComponent<BoxCollider>();
                        cornerCol.center = new Vector3(origin.x + wallThickness / 2, wallHeight / 2, origin.z + wallThickness / 2);
                        cornerCol.size = new Vector3(wallThickness, wallHeight, wallThickness);
                    }
                }
            }
        }
    }

    private bool CoordsValid(Vector2Int coords)
    {
        return coords.x >= 0 && coords.x < dungeonWidth && coords.y >= 0 && coords.y < dungeonHeight;
    }

    private void UpdateCorners(Vector2Int coords, byte corners)
    {
        //Debug.Log("DEBUG: Updating corners " + corners + " at coords " + coords);

        if (CoordsValid(coords) && tiles.ContainsKey(coords))
        {
            //if the first corner is one that was specified to be updated or if we want to update all of them
            if ((1 & corners) > 0 || corners == 0)
            {
                bool cornerNecessary = false;

                Vector2Int upCoords = new Vector2Int(coords.x, coords.y + 1);
                Vector2Int upRightCoords = new Vector2Int(coords.x + 1, coords.y + 1);
                Vector2Int rightCoords = new Vector2Int(coords.x + 1, coords.y);

                //need to check if corner is necessary, if it is, set the bit and continue to next corner
                //check if coords' adjacent walls to the given corner is set, if it is, the corner is necessary
                if (((tiles[coords].walls & 1) == 1) || (((tiles[coords].walls >> 1) & 1) == 1))
                {
                    //first corner is necessary for the current coords
                    cornerNecessary = true;
                }

                if (!cornerNecessary && tiles.ContainsKey(upCoords) && ((((tiles[upCoords].walls >> 1) & 1) == 1) || (((tiles[upCoords].walls >> 2) & 1) == 1)))
                {
                    cornerNecessary = true;
                }

                if (!cornerNecessary && tiles.ContainsKey(upRightCoords) && ((((tiles[upRightCoords].walls >> 2) & 1) == 1) || (((tiles[upRightCoords].walls >> 3) & 1) == 1)))
                {
                    cornerNecessary = true;
                }

                if (!cornerNecessary && tiles.ContainsKey(rightCoords) && ((((tiles[rightCoords].walls >> 3) & 1) == 1) || ((tiles[rightCoords].walls & 1) == 1)))
                {
                    cornerNecessary = true;
                }

                if (cornerNecessary)
                {
                    tiles[coords].corners = (byte)(tiles[coords].corners | 1);
                    if(tiles.ContainsKey(upCoords))
                        tiles[upCoords].corners = (byte)(tiles[upCoords].corners | 2);
                    if(tiles.ContainsKey(upRightCoords))
                        tiles[upRightCoords].corners = (byte)(tiles[upRightCoords].corners | 4);
                    if(tiles.ContainsKey(rightCoords))
                        tiles[rightCoords].corners = (byte)(tiles[rightCoords].corners | 8);
                }
                else
                {
                    tiles[coords].corners = (byte)(tiles[coords].corners & 14);
                    if (tiles.ContainsKey(upCoords))
                        tiles[upCoords].corners = (byte)(tiles[upCoords].corners & 13);
                    if (tiles.ContainsKey(upRightCoords))
                        tiles[upRightCoords].corners = (byte)(tiles[upRightCoords].corners & 11);
                    if (tiles.ContainsKey(rightCoords))
                        tiles[rightCoords].corners = (byte)(tiles[rightCoords].corners & 7);
                }
            }

            //if the second corner is one that was specified to be updated or if we want to update all of them
            if ((2 & corners) > 0 || corners == 0)
            {
                bool cornerNecessary = false;

                Vector2Int rightCoords = new Vector2Int(coords.x + 1, coords.y);
                Vector2Int downRightCoords = new Vector2Int(coords.x + 1, coords.y - 1);
                Vector2Int downCoords = new Vector2Int(coords.x, coords.y - 1);

                if ((((tiles[coords].walls >> 1) & 1) == 1) || (((tiles[coords].walls >> 2) & 1) == 1))
                {
                    cornerNecessary = true;
                }

                if (!cornerNecessary && CoordsValid(rightCoords) && tiles.ContainsKey(rightCoords) && ((((tiles[rightCoords].walls >> 2) & 1) == 1) || (((tiles[rightCoords].walls >> 3) & 1) == 1)))
                {
                    cornerNecessary = true;
                }

                if (!cornerNecessary && CoordsValid(downRightCoords) && tiles.ContainsKey(downRightCoords) && (((tiles[downRightCoords].walls & 1) == 1) || (((tiles[downRightCoords].walls >> 3) & 1) == 1)))
                {
                    cornerNecessary = true;
                }

                if (!cornerNecessary && CoordsValid(downCoords) && tiles.ContainsKey(downCoords) && (((tiles[downCoords].walls & 1) == 1) || (((tiles[downCoords].walls >> 1) & 1) == 1)))
                {
                    cornerNecessary = true;
                }

                if (cornerNecessary)
                {
                    tiles[coords].corners = (byte)(tiles[coords].corners | 2);
                    if (tiles.ContainsKey(rightCoords))
                        tiles[rightCoords].corners = (byte)(tiles[rightCoords].corners | 4);
                    if (tiles.ContainsKey(downRightCoords))
                        tiles[downRightCoords].corners = (byte)(tiles[downRightCoords].corners | 8);
                    if (tiles.ContainsKey(downCoords))
                        tiles[downCoords].corners = (byte)(tiles[downCoords].corners | 1);
                }
                else
                {
                    tiles[coords].corners = (byte)(tiles[coords].corners & 13);
                    if (tiles.ContainsKey(rightCoords))
                        tiles[rightCoords].corners = (byte)(tiles[rightCoords].corners & 11);
                    if (tiles.ContainsKey(downRightCoords))
                        tiles[downRightCoords].corners = (byte)(tiles[downRightCoords].corners & 7);
                    if (tiles.ContainsKey(downCoords))
                        tiles[downCoords].corners = (byte)(tiles[downCoords].corners & 14);
                }
            }

            //if the third corner is one that was specified to be updated or if we want to update all of them
            if ((4 & corners) > 0 || corners == 0)
            {
                bool cornerNecessary = false;

                Vector2Int downCoords = new Vector2Int(coords.x, coords.y - 1);
                Vector2Int downLeftCoords = new Vector2Int(coords.x - 1, coords.y-1);
                Vector2Int leftCoords = new Vector2Int(coords.x - 1, coords.y);

                if ((((tiles[coords].walls >> 2) & 1) == 1) || (((tiles[coords].walls >> 3) & 1) == 1))
                {
                    cornerNecessary = true;
                }

                if (!cornerNecessary && CoordsValid(downCoords) && tiles.ContainsKey(downCoords) && (((tiles[downCoords].walls & 1) == 1) || (((tiles[downCoords].walls >> 3) & 1) == 1)))
                {
                    cornerNecessary = true;
                }

                if (!cornerNecessary && CoordsValid(downLeftCoords) && tiles.ContainsKey(downLeftCoords) && (((tiles[downLeftCoords].walls & 1) == 1) || (((tiles[downLeftCoords].walls >> 1) & 1) == 1)))
                {
                    cornerNecessary = true;
                }

                if (!cornerNecessary && CoordsValid(leftCoords) && tiles.ContainsKey(leftCoords) && ((((tiles[leftCoords].walls>>1) & 1) == 1) || (((tiles[leftCoords].walls >> 2) & 1) == 1)))
                {
                    cornerNecessary = true;
                }

                if (cornerNecessary)
                {
                    tiles[coords].corners = (byte)(tiles[coords].corners | 4);
                    if (tiles.ContainsKey(downCoords))
                        tiles[downCoords].corners = (byte)(tiles[downCoords].corners | 8);
                    if (tiles.ContainsKey(downLeftCoords))
                        tiles[downLeftCoords].corners = (byte)(tiles[downLeftCoords].corners | 1);
                    if (tiles.ContainsKey(leftCoords))
                        tiles[leftCoords].corners = (byte)(tiles[leftCoords].corners | 2);
                }
                else
                {
                    tiles[coords].corners = (byte)(tiles[coords].corners & 11);
                    if (tiles.ContainsKey(downCoords))
                        tiles[downCoords].corners = (byte)(tiles[downCoords].corners & 7);
                    if (tiles.ContainsKey(downLeftCoords))
                        tiles[downLeftCoords].corners = (byte)(tiles[downLeftCoords].corners & 14);
                    if (tiles.ContainsKey(leftCoords))
                        tiles[leftCoords].corners = (byte)(tiles[leftCoords].corners & 13);
                }
            }

            //if the fourth corner is one that was specified to be updated or if we want to update all of them
            if ((8 & corners) > 0 || corners == 0)
            {
                bool cornerNecessary = false;

                Vector2Int leftCoords = new Vector2Int(coords.x - 1, coords.y);
                Vector2Int upLeftCoords = new Vector2Int(coords.x - 1, coords.y + 1);
                Vector2Int upCoords = new Vector2Int(coords.x, coords.y + 1);

                if ((((tiles[coords].walls >> 3) & 1) == 1) || ((tiles[coords].walls & 1) == 1))
                {
                    cornerNecessary = true;
                }

                if (!cornerNecessary && CoordsValid(leftCoords) && tiles.ContainsKey(leftCoords) && (((tiles[leftCoords].walls & 1) == 1) || (((tiles[leftCoords].walls >> 1) & 1) == 1)))
                {
                    cornerNecessary = true;
                }

                if (!cornerNecessary && CoordsValid(upLeftCoords) && tiles.ContainsKey(upLeftCoords) && ((((tiles[upLeftCoords].walls>>1) & 1) == 1) || (((tiles[upLeftCoords].walls >> 2) & 1) == 1)))
                {
                    cornerNecessary = true;
                }

                if (!cornerNecessary && CoordsValid(upCoords) && tiles.ContainsKey(upCoords) && ((((tiles[upCoords].walls>>2) & 1) == 1) || (((tiles[upCoords].walls >> 3) & 1) == 1)))
                {
                    cornerNecessary = true;
                }

                if (cornerNecessary)
                {
                    tiles[coords].corners = (byte)(tiles[coords].corners | 8);
                    if (tiles.ContainsKey(leftCoords))
                        tiles[leftCoords].corners = (byte)(tiles[leftCoords].corners | 1);
                    if (tiles.ContainsKey(upLeftCoords))
                        tiles[upLeftCoords].corners = (byte)(tiles[upLeftCoords].corners | 2);
                    if (tiles.ContainsKey(upCoords))
                        tiles[upCoords].corners = (byte)(tiles[upCoords].corners | 4);
                }
                else
                {
                    tiles[coords].corners = (byte)(tiles[coords].corners & 7);
                    if (tiles.ContainsKey(leftCoords))
                        tiles[leftCoords].corners = (byte)(tiles[leftCoords].corners & 14);
                    if (tiles.ContainsKey(upLeftCoords))
                        tiles[upLeftCoords].corners = (byte)(tiles[upLeftCoords].corners & 13);
                    if (tiles.ContainsKey(upCoords))
                        tiles[upCoords].corners = (byte)(tiles[upCoords].corners & 11);
                }
            }
        }
    }
}

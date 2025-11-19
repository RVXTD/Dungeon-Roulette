using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;
using Graphs;

public class Generator3D : MonoBehaviour
{
    enum CellType
    {
        None,
        Room,
        Hallway,
        Stairs
    }

    class Room
    {
        public BoundsInt bounds;

        public Room(Vector3Int location, Vector3Int size)
        {
            bounds = new BoundsInt(location, size);
        }

        public static bool Intersect(Room a, Room b)
        {
            return !((a.bounds.position.x >= (b.bounds.position.x + b.bounds.size.x)) || ((a.bounds.position.x + a.bounds.size.x) <= b.bounds.position.x)
                  || (a.bounds.position.y >= (b.bounds.position.y + b.bounds.size.y)) || ((a.bounds.position.y + a.bounds.size.y) <= b.bounds.position.y)
                  || (a.bounds.position.z >= (b.bounds.position.z + b.bounds.size.z)) || ((a.bounds.position.z + a.bounds.size.z) <= b.bounds.position.z));
        }
    }

    [Header("Map Settings")]
    [SerializeField] Vector3Int size;
    [SerializeField] int roomCount;
    [SerializeField] Vector3Int roomMaxSize;

    [Header("Debug Cube Settings")]
    [SerializeField] bool useDebugCubes = false;
    [SerializeField] GameObject cubePrefab;
    [SerializeField] Material redMaterial;
    [SerializeField] Material blueMaterial;

    [Header("Dungeon Prefabs")]
    [SerializeField] GameObject roomTilePrefab;     
    [SerializeField] GameObject hallwayTilePrefab;  
    [SerializeField] bool tilesPivotAtCenter = true;
    [SerializeField] GameObject wallPrefab;
    [SerializeField] GameObject doorFramePrefab;



    [Header("Optional")]
    [SerializeField] GameObject StartingRoom;

    Random random;

    Grid3D<CellType> grid;
    List<Room> rooms;
    Delaunay3D delaunay;
    HashSet<Prim.Edge> selectedEdges;
    List<Vertex> _roomVertices;
    public List<Vector3> roomCenters = new List<Vector3>();

    void Start()
    {
        random = new Random(0);
        grid = new Grid3D<CellType>(size, Vector3Int.zero);
        rooms = new List<Room>();

        PlaceRooms();
        Triangulate();
        CreateHallways();
        PathfindHallways();
        RecordRoomCenters();

        FindObjectOfType<SpawnManager>()?.Spawn();
    }

    void PlaceRooms()
    {
        for (int i = 0; i < roomCount; i++)
        {
            Vector3Int location = new Vector3Int(
                random.Next(0, size.x),
                0,
                random.Next(0, size.z)
            );

            Vector3Int roomSize = new Vector3Int(
                random.Next(1, roomMaxSize.x + 1),
                1,
                random.Next(1, roomMaxSize.z + 1)
            );

            bool add = true;
            Room newRoom = new Room(location, roomSize);
            Room buffer = new Room(location + new Vector3Int(-1, 0, -1), roomSize + new Vector3Int(2, 0, 2));

            foreach (var room in rooms)
            {
                if (Room.Intersect(room, buffer))
                {
                    add = false;
                    break;
                }
            }

            if (newRoom.bounds.xMin < 0 || newRoom.bounds.xMax >= size.x
             || newRoom.bounds.yMin < 0 || newRoom.bounds.yMax >= size.y
             || newRoom.bounds.zMin < 0 || newRoom.bounds.zMax >= size.z)
            {
                add = false;
            }

            if (add)
            {
                rooms.Add(newRoom);

                // visually place the room
                PlaceRoom(newRoom.bounds.position, newRoom.bounds.size);

                // mark grid cells as room
                foreach (var pos in newRoom.bounds.allPositionsWithin)
                {
                    grid[pos] = CellType.Room;
                }
            }
        }
    }

    void Triangulate()
    {
        _roomVertices = new List<Vertex>();
        foreach (var room in rooms)
        {
            _roomVertices.Add(new Vertex<Room>(
                (Vector3)room.bounds.position + ((Vector3)room.bounds.size) / 2, room));
        }
        delaunay = Delaunay3D.Triangulate(_roomVertices);
    }

    void CreateHallways()
    {
        List<Prim.Edge> edges = new List<Prim.Edge>();
        foreach (var e in delaunay.Edges)
        {
            edges.Add(new Prim.Edge(e.U, e.V));
        }

        // Fallback if no 3D edges were created (rare)
        if (edges.Count == 0)
        {
            const int k = 3;

            for (int i = 0; i < _roomVertices.Count; i++)
            {
                var vi = _roomVertices[i];
                var pi = vi.Position;

                var nn = new List<(float dist2, int j)>();
                for (int j = 0; j < _roomVertices.Count; j++)
                {
                    if (j == i) continue;
                    var pj = _roomVertices[j].Position;
                    float d2 = (new Vector2(pi.x, pi.z) - new Vector2(pj.x, pj.z)).sqrMagnitude;
                    nn.Add((d2, j));
                }
                nn.Sort((a, b) => a.dist2.CompareTo(b.dist2));

                int limit = Mathf.Min(k, nn.Count);
                for (int m = 0; m < limit; m++)
                {
                    edges.Add(new Prim.Edge(vi, _roomVertices[nn[m].j]));
                }
            }
        }

        if (edges.Count == 0)
        {
            Debug.LogError("No edges were generated — skipping hallway creation.");
            return;
        }

        var minimumSpanningTree = Prim.MinimumSpanningTree(edges, edges[0].U);
        selectedEdges = new HashSet<Prim.Edge>(minimumSpanningTree);

        var remainingEdges = new HashSet<Prim.Edge>(edges);
        remainingEdges.ExceptWith(selectedEdges);

        foreach (var edge in remainingEdges)
        {
            if (random.NextDouble() < 0.125)
            {
                selectedEdges.Add(edge);
            }
        }
    }

    void PathfindHallways()
    {
        DungeonPathfinder3D aStar = new DungeonPathfinder3D(size);

        foreach (var edge in selectedEdges)
        {
            var startRoom = (edge.U as Vertex<Room>).Item;
            var endRoom = (edge.V as Vertex<Room>).Item;

            var startPosf = startRoom.bounds.center;
            var endPosf = endRoom.bounds.center;

            var startPos = new Vector3Int(Mathf.RoundToInt(startPosf.x), 0, Mathf.RoundToInt(startPosf.z));
            var endPos = new Vector3Int(Mathf.RoundToInt(endPosf.x), 0, Mathf.RoundToInt(endPosf.z));

            var path = aStar.FindPath(startPos, endPos, (DungeonPathfinder3D.Node a, DungeonPathfinder3D.Node b) =>
            {
                var pathCost = new DungeonPathfinder3D.PathCost
                {
                    traversable = true,
                    isStairs = false
                };
                float stepCost = 1f;

                if (!grid.InBounds(b.Position))
                {
                    pathCost.traversable = false;
                    return pathCost;
                }

                if (b.Position == endPos)
                {
                    stepCost += 0f;
                }
                else if (grid[b.Position] == CellType.Room)
                {
                    stepCost += 1f;
                }
                else if (grid[b.Position] == CellType.None)
                {
                    stepCost += 1f;
                }
                else if (grid[b.Position] == CellType.Hallway)
                {
                    stepCost += 0.1f;
                }

                pathCost.cost = stepCost + Vector3Int.Distance(b.Position, endPos);

                return pathCost;
            });

            if (path != null)
            {
                for (int i = 0; i < path.Count; i++)
                {
                    var current = path[i];

                    if (grid[current] == CellType.None)
                    {
                        grid[current] = CellType.Hallway;
                    }

                    if (i > 0)
                    {
                        var prev = path[i - 1];
                        Debug.DrawLine(prev + new Vector3(0.5f, 0.5f, 0.5f), current + new Vector3(0.5f, 0.5f, 0.5f), Color.blue, 100, false);
                    }
                }

                foreach (var pos in path)
                {
                    if (grid[pos] == CellType.Hallway)
                    {
                        PlaceHallway(pos);
                    }
                }
            }
            else
            {
                Debug.LogError($"Pathfinding failed between room at {startRoom.bounds.center} and {endRoom.bounds.center}");
            }
        }
    }

    // ---------- VISUAL PLACEMENT HELPERS ----------

    void PlaceCube(Vector3Int location, Vector3Int size, Material material)
    {
        GameObject go = Instantiate(cubePrefab, location, Quaternion.identity, transform);
        go.transform.localScale = size;

        var mr = go.GetComponent<MeshRenderer>();
        if (mr) mr.material = material;

        var bc = go.GetComponent<BoxCollider>();
        if (bc == null) bc = go.AddComponent<BoxCollider>();
        bc.size = Vector3.one;
        bc.center = new Vector3(0.5f, 0.5f, 0.5f);

        go.isStatic = true;
    }
   

    void PlaceRoom(Vector3Int location, Vector3Int size)
    {
        // Record center for spawns
        Vector3 center = (Vector3)location + (Vector3)size / 2f;
        center.y = 0f;
        roomCenters.Add(center);

        if (useDebugCubes || roomTilePrefab == null)
        {
            // Old behaviour: one big red cube
            PlaceCube(location, size, redMaterial);
            return;
        }

        // New behaviour: fill the room area with 1x1 tiles
        var bounds = new BoundsInt(location, size);
        Vector3 offset = tilesPivotAtCenter ? new Vector3(0.5f, 0f, 0.5f) : Vector3.zero;

        foreach (var pos in bounds.allPositionsWithin)
        {
            Vector3 worldPos = (Vector3)pos + offset;
            Instantiate(roomTilePrefab, worldPos, Quaternion.identity, transform);
        }

        BuildWallsForRoom(bounds);
    }
    void BuildWallsForRoom(BoundsInt roomBounds)
    {
        if (wallPrefab == null) return;

        Vector3 offset = tilesPivotAtCenter ? new Vector3(0.5f, 0f, 0.5f) : Vector3.zero;

        foreach (var pos in roomBounds.allPositionsWithin)
        {
            // For each tile, check the 4 neighbor directions
            Vector3Int[] directions = {
            new Vector3Int(1,0,0),
            new Vector3Int(-1,0,0),
            new Vector3Int(0,0,1),
            new Vector3Int(0,0,-1)
        };

            foreach (var dir in directions)
            {
                Vector3Int neighbor = pos + dir;
                bool neighborIsEmpty =!grid.InBounds(neighbor) || grid[neighbor] == CellType.None;


                
                if (!roomBounds.Contains(neighbor))
                {
                    Vector3 wallPos = (Vector3)pos + offset + new Vector3(dir.x * 0.5f, 1.5f, dir.z * 0.5f);

                    Quaternion rot = Quaternion.identity;
                    if (dir.x != 0) rot = Quaternion.Euler(0, 90, 0);

                    Instantiate(wallPrefab, wallPos, rot, transform);
                }
            }
        }
    }
    void BuildWallsForHallways()
    {
        if (wallPrefab == null) return;

        Vector3 offset = tilesPivotAtCenter ? new Vector3(0.5f, 0f, 0.5f) : Vector3.zero;
        HashSet<Vector3> placedWalls = new HashSet<Vector3>();

        for (int x = 0; x < size.x; x++)
            for (int z = 0; z < size.z; z++)
            {
                Vector3Int pos = new Vector3Int(x, 0, z);
                if (grid[pos] != CellType.Hallway) continue;

                Vector3Int[] directions = {
            new Vector3Int(1,0,0),
            new Vector3Int(-1,0,0),
            new Vector3Int(0,0,1),
            new Vector3Int(0,0,-1)
        };

                foreach (var dir in directions)
                {
                    var neighbor = pos + dir;

                    // Only place a wall if neighbor is out of bounds or empty
                    bool neighborIsEmpty =
                        !grid.InBounds(neighbor) || grid[neighbor] == CellType.None;

                    if (!neighborIsEmpty) continue;

                    Vector3 wallPos = (Vector3)pos + offset +
                                      new Vector3(dir.x * 0.5f, 1.5f, dir.z * 0.5f);

                    // avoid double walls on shared edges
                    if (placedWalls.Contains(wallPos)) continue;
                    placedWalls.Add(wallPos);

                    Quaternion rot = Quaternion.identity;
                    if (dir.x != 0) rot = Quaternion.Euler(0, 90, 0);

                    Instantiate(wallPrefab, wallPos, rot, transform);
                }
            }
    }

    void PlaceHallway(Vector3Int location)
    {
        if (useDebugCubes || hallwayTilePrefab == null)
        {
            PlaceCube(location, new Vector3Int(1, 1, 1), blueMaterial);
            return;
        }

        Vector3 offset = tilesPivotAtCenter ? new Vector3(0.5f, 0f, 0.5f) : Vector3.zero;
        Vector3 worldPos = (Vector3)location + offset;
        Instantiate(hallwayTilePrefab, worldPos, Quaternion.identity, transform);
        BuildWallsForHallways();

    }

    public void RecordRoomCenters()
    {
        roomCenters.Clear();

        foreach (var room in rooms)
        {
            Vector3 c = (Vector3)room.bounds.position + (Vector3)room.bounds.size / 2f;
            c.y = 0f;
            roomCenters.Add(c);
        }

        Debug.Log($"Recorded {roomCenters.Count} room centers.");
        
    }
}

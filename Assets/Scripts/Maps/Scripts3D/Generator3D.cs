using System.Collections;
using System.Collections.Generic;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEngine;
using Random = System.Random;
using Graphs;

public class Generator3D : MonoBehaviour
{
    [Header("NavMesh")]
    [SerializeField] private NavMeshSurface navSurface;

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

    [Header("Trap Settings")]
    [SerializeField] GameObject trapTilePrefab;
    [Range(0f, 1f)]
    [SerializeField] float trapChancePerTile = 0.15f;

    [Header("Door Settings")]
    [SerializeField] float doorCenterHeight = 1.5f;

    [Header("Ceiling Settings")]
    [SerializeField] GameObject ceilingTilePrefab;
    [SerializeField] float ceilingHeight = 3f;
    [SerializeField] bool addCeilingsToRooms = true;
    [SerializeField] bool addCeilingsToHallways = true;

    [SerializeField] GameObject torchPrefab;
    [SerializeField] GameObject columnPrefab;


    Random random;

    Grid3D<CellType> grid;
    List<Room> rooms;
    Delaunay3D delaunay;
    HashSet<Prim.Edge> selectedEdges;
    List<Vertex> _roomVertices;
    List<GameObject> spawnedWalls = new List<GameObject>();

    public List<Vector3> roomCenters = new List<Vector3>();

    void Start()
    {
        GenerateDungeon();
    }

    /// <summary>
    /// Public method you can call from RoundManager to make a new dungeon.
    /// </summary>
    public void GenerateDungeon()
    {
        // New random seed every generation (so the dungeon changes each round)
        random = new Random();

        grid = new Grid3D<CellType>(size, Vector3Int.zero);
        rooms = new List<Room>();

        // --- generate geometry ---
        PlaceRooms();
        Triangulate();
        CreateHallways();
        PathfindHallways();

        BuildWallsForHallways();
        PlaceDoorsAtRoomHallwayEdges();
        BuildCeilings();
        PlaceColumnsInCorners();

        // --- navmesh setup ---
        if (navSurface == null)
            navSurface = GetComponent<NavMeshSurface>();

        if (navSurface != null)
            StartCoroutine(RebuildNavmeshNextFrame());

        // --- room centers + enemy spawns ---
        RecordRoomCenters();
        FindObjectOfType<SpawnManager>()?.Spawn();
    }

    /// <summary>
    /// Clears the generated dungeon from under this Generator3D object.
    /// Call this before GenerateDungeon() when starting a new round.
    /// </summary>
    public void ClearDungeon()
    {
        // Destroy everything spawned under this generator
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }

        // Clear runtime lists
        spawnedWalls.Clear();
        roomCenters.Clear();
        selectedEdges = null;
        delaunay = null;
        _roomVertices = null;

        // Reset references
        grid = null;
        rooms = null;
    }

    // Wait one frame so all instantiated tiles/walls/doors exist,
    // then build the NavMesh over the final dungeon.
    private IEnumerator RebuildNavmeshNextFrame()
    {
        yield return null;
        navSurface.BuildNavMesh();
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

    // Marks a grid cell as hallway and instantiates the hallway floor there
    void TryMakeHallwayCell(Vector3Int pos)
    {
        if (!grid.InBounds(pos)) return;

        if (grid[pos] == CellType.None)
        {
            grid[pos] = CellType.Hallway;
            PlaceHallway(pos);
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

                    // always make the center hallway cell
                    TryMakeHallwayCell(current);

                    // if we know the direction we came from, widen perpendicular to that
                    if (i > 0)
                    {
                        var prev = path[i - 1];
                        Vector3Int dir = current - prev;

                        // hallway going along X → widen along Z
                        if (Mathf.Abs(dir.x) > 0)
                        {
                            TryMakeHallwayCell(current + new Vector3Int(0, 0, 1));
                            TryMakeHallwayCell(current + new Vector3Int(0, 0, -1));
                        }
                        // hallway going along Z → widen along X
                        else if (Mathf.Abs(dir.z) > 0)
                        {
                            TryMakeHallwayCell(current + new Vector3Int(1, 0, 0));
                            TryMakeHallwayCell(current + new Vector3Int(-1, 0, 0));
                        }

                        Debug.DrawLine(prev + new Vector3(0.5f, 0.5f, 0.5f),
                                       current + new Vector3(0.5f, 0.5f, 0.5f),
                                       Color.blue, 100, false);
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
        Vector3 center = (Vector3)location + (Vector3)size / 2f;
        center.y = 0f;
        roomCenters.Add(center);

        if (useDebugCubes || roomTilePrefab == null)
        {
            PlaceCube(location, size, redMaterial);
            return;
        }

        var bounds = new BoundsInt(location, size);
        Vector3 offset = tilesPivotAtCenter ? new Vector3(0.5f, 0f, 0.5f) : Vector3.zero;

        foreach (var pos in bounds.allPositionsWithin)
        {
            Vector3 worldPos = (Vector3)pos + offset;

            GameObject floor = Instantiate(roomTilePrefab, worldPos, Quaternion.identity, transform);

            bool placeTrap = trapTilePrefab != null && random.NextDouble() < trapChancePerTile;

            if (placeTrap)
            {
                Instantiate(trapTilePrefab, worldPos, Quaternion.identity, transform);

                var renderers = floor.GetComponentsInChildren<MeshRenderer>();
                foreach (var mr in renderers)
                {
                    mr.enabled = false;
                }
            }
        }

        BuildWallsForRoom(bounds);
    }

    void BuildWallsForRoom(BoundsInt roomBounds)
    {
        if (wallPrefab == null) return;

        Vector3 offset = tilesPivotAtCenter ? new Vector3(0.5f, 0f, 0.5f) : Vector3.zero;

        foreach (var pos in roomBounds.allPositionsWithin)
        {
            Vector3Int[] directions = {
                new Vector3Int(1,0,0),
                new Vector3Int(-1,0,0),
                new Vector3Int(0,0,1),
                new Vector3Int(0,0,-1)
            };

            foreach (var dir in directions)
            {
                Vector3Int neighbor = pos + dir;
                bool neighborIsEmpty = !grid.InBounds(neighbor) || grid[neighbor] == CellType.None;

                if (!roomBounds.Contains(neighbor))
                {
                    Vector3 wallPos = (Vector3)pos + offset + new Vector3(dir.x * 0.5f, 1.5f, dir.z * 0.5f);

                    Quaternion rot = Quaternion.identity;
                    if (dir.x != 0) rot = Quaternion.Euler(0, 90, 0);

                    var wall = Instantiate(wallPrefab, wallPos, rot, transform);
                    spawnedWalls.Add(wall);

                    // --- TORCH LOGIC ADDED HERE ---
                    if (torchPrefab != null && random.NextDouble() < 0.2f)
                    {
                        GameObject torch = Instantiate(torchPrefab, wallPos, rot, transform);
                        torch.transform.SetParent(wall.transform);

                        // OFFSET FIXES (Adjust these if your model is weird)
                        torch.transform.Translate(Vector3.up * 0.5f);
                        torch.transform.Translate(Vector3.forward * 0.1f);
                        // If bracket is backwards, uncomment this:
                        // torch.transform.Rotate(0, 180f, 0);
                    }
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
        {
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

                    bool neighborIsEmpty =
                        !grid.InBounds(neighbor) || grid[neighbor] == CellType.None;

                    if (!neighborIsEmpty) continue;

                    Vector3 wallPos = (Vector3)pos + offset +
                                      new Vector3(dir.x * 0.5f, 1.5f, dir.z * 0.5f);

                    if (placedWalls.Contains(wallPos)) continue;
                    placedWalls.Add(wallPos);

                    Quaternion rot = Quaternion.identity;
                    if (dir.x != 0) rot = Quaternion.Euler(0, 90, 0);

                    var wall = Instantiate(wallPrefab, wallPos, rot, transform);
                    spawnedWalls.Add(wall);
                }
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

    void PlaceDoorsAtRoomHallwayEdges()
    {
        if (doorFramePrefab == null) return;

        Vector3 tileOffset = tilesPivotAtCenter ? new Vector3(0.5f, 0f, 0.5f) : Vector3.zero;

        for (int x = 0; x < size.x; x++)
        {
            for (int z = 0; z < size.z; z++)
            {
                Vector3Int pos = new Vector3Int(x, 0, z);
                if (grid[pos] != CellType.Room) continue;

                Vector3Int[] dirs =
                {
                    new Vector3Int(1,0,0),
                    new Vector3Int(-1,0,0),
                    new Vector3Int(0,0,1),
                    new Vector3Int(0,0,-1)
                };

                foreach (var dir in dirs)
                {
                    Vector3Int neighbor = pos + dir;
                    if (!grid.InBounds(neighbor)) continue;

                    if (grid[neighbor] == CellType.Hallway)
                    {
                        Vector3 basePos = (Vector3)pos + tileOffset;
                        Vector3 doorPos = basePos + new Vector3(dir.x * 0.5f, doorCenterHeight, dir.z * 0.5f);

                        Quaternion rot = Quaternion.identity;
                        if (dir.x != 0)
                            rot = Quaternion.Euler(0f, 90f, 0f);

                        RemoveWallAtPosition(doorPos);

                        Instantiate(doorFramePrefab, doorPos, rot, transform);
                    }
                }
            }
        }
    }

    void RemoveWallAtPosition(Vector3 position)
    {
        float radius = 0.6f;

        for (int i = spawnedWalls.Count - 1; i >= 0; i--)
        {
            var wall = spawnedWalls[i];
            if (wall == null)
            {
                spawnedWalls.RemoveAt(i);
                continue;
            }

            Vector3 wallPos = wall.transform.position;

            float distXZ = Vector2.Distance(
                new Vector2(wallPos.x, wallPos.z),
                new Vector2(position.x, position.z)
            );

            if (distXZ < radius)
            {
                Destroy(wall);
                spawnedWalls.RemoveAt(i);
            }
        }
    }

    void BuildCeilings()
    {
        if (ceilingTilePrefab == null) return;

        Vector3 tileOffset = tilesPivotAtCenter ? new Vector3(0.5f, 0f, 0.5f) : Vector3.zero;

        for (int x = 0; x < size.x; x++)
        {
            for (int z = 0; z < size.z; z++)
            {
                Vector3Int pos = new Vector3Int(x, 0, z);
                CellType type = grid[pos];

                bool shouldPlace =
                    (type == CellType.Room && addCeilingsToRooms) ||
                    (type == CellType.Hallway && addCeilingsToHallways);

                if (!shouldPlace) continue;

                Vector3 worldPos = (Vector3)pos + tileOffset + new Vector3(0f, ceilingHeight, 0f);
                Instantiate(ceilingTilePrefab, worldPos, Quaternion.identity, transform);
            }
        }
    }
    void PlaceColumnsInCorners()
    {
        if (columnPrefab == null) return;

        foreach (var room in rooms)
        {
            // Get 4 corner positions (integers)
            int minX = room.bounds.xMin;
            int maxX = room.bounds.xMax;
            int minZ = room.bounds.zMin;
            int maxZ = room.bounds.zMax;

            Vector3[] corners = new Vector3[] {
                new Vector3(minX, 0, minZ),
                new Vector3(maxX, 0, minZ),
                new Vector3(minX, 0, maxZ),
                new Vector3(maxX, 0, maxZ)
            };

            foreach (var pos in corners)
            {
                // PHYSICS CHECK:
                // Check a 1-meter radius around the corner for any objects.
                // If we find a "Door" or "DoorFrame", we SKIP this column.

                Collider[] hitColliders = Physics.OverlapSphere(pos, 1.0f);
                bool isNearDoor = false;

                foreach (var hit in hitColliders)
                {
                    // CHANGE "Door" IF YOUR PREFAB IS NAMED DIFFERENTLY (e.g. "Archway")
                    if (hit.gameObject.name.Contains("Door") || hit.gameObject.name.Contains("Frame"))
                    {
                        isNearDoor = true;
                        break;
                    }
                }

                if (isNearDoor) continue; // Found a door? Skip!

                // Otherwise, spawn the column
                Instantiate(columnPrefab, pos, Quaternion.identity, transform);
            }
        }
    }
}

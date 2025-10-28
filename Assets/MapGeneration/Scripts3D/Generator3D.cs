using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;
using Graphs;

public class Generator3D : MonoBehaviour {
    enum CellType {
        None,
        Room,
        Hallway,
        Stairs
    }

    class Room {
        public BoundsInt bounds;

        public Room(Vector3Int location, Vector3Int size) {
            bounds = new BoundsInt(location, size);
        }

        public static bool Intersect(Room a, Room b) {
            return !((a.bounds.position.x >= (b.bounds.position.x + b.bounds.size.x)) || ((a.bounds.position.x + a.bounds.size.x) <= b.bounds.position.x)
                || (a.bounds.position.y >= (b.bounds.position.y + b.bounds.size.y)) || ((a.bounds.position.y + a.bounds.size.y) <= b.bounds.position.y)
                || (a.bounds.position.z >= (b.bounds.position.z + b.bounds.size.z)) || ((a.bounds.position.z + a.bounds.size.z) <= b.bounds.position.z));
        }
    }

    [SerializeField]
    GameObject StartingRoom;
    [SerializeField]
    Vector3Int size;
    [SerializeField]
    int roomCount;
    [SerializeField]
    Vector3Int roomMaxSize;
    [SerializeField]
    GameObject cubePrefab;
    [SerializeField]
    Material redMaterial;
    [SerializeField]
    Material blueMaterial;
    [SerializeField]
    Material greenMaterial;

    Random random;
    Grid3D<CellType> grid;
    List<Room> rooms;
    Delaunay3D delaunay;
    HashSet<Prim.Edge> selectedEdges;
    List<Vertex> _roomVertices;


    void Start() {
        random = new Random(0);
        grid = new Grid3D<CellType>(size, Vector3Int.zero);
        rooms = new List<Room>();

        PlaceRooms();
        Triangulate();
        CreateHallways();
        PathfindHallways();
    }

    void PlaceRooms() {
        for (int i = 0; i < roomCount; i++) {
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

            foreach (var room in rooms) {
                if (Room.Intersect(room, buffer)) {
                    add = false;
                    break;
                }
            }

            if (newRoom.bounds.xMin < 0 || newRoom.bounds.xMax >= size.x
                || newRoom.bounds.yMin < 0 || newRoom.bounds.yMax >= size.y
                || newRoom.bounds.zMin < 0 || newRoom.bounds.zMax >= size.z) {
                add = false;
            }

            if (add) {
                rooms.Add(newRoom);
                PlaceRoom(newRoom.bounds.position, newRoom.bounds.size);

                foreach (var pos in newRoom.bounds.allPositionsWithin) {
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

    // 3) in CreateHallways(), add a 2D fallback when Delaunay3D returns 0 edges
    void CreateHallways()
    {
        List<Prim.Edge> edges = new List<Prim.Edge>();
        foreach (var e in delaunay.Edges)
        {
            edges.Add(new Prim.Edge(e.U, e.V));
        }

        if (edges.Count == 0)
        {
            
            const int k = 3; // try 2–4; tweak to taste

            for (int i = 0; i < _roomVertices.Count; i++)
            {
                var vi = _roomVertices[i];
                var pi = vi.Position;

                // collect neighbors by XZ distance
                var nn = new List<(float dist2, int j)>();
                for (int j = 0; j < _roomVertices.Count; j++)
                {
                    if (j == i) continue;
                    var pj = _roomVertices[j].Position;
                    float d2 = (new Vector2(pi.x, pi.z) - new Vector2(pj.x, pj.z)).sqrMagnitude;
                    nn.Add((d2, j));
                }
                nn.Sort((a, b) => a.dist2.CompareTo(b.dist2));

                // connect to k nearest (undirected; Prim will dedupe anyway)
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

    void PathfindHallways() {

        DungeonPathfinder3D aStar = new DungeonPathfinder3D(size);

        foreach (var edge in selectedEdges) {
            var startRoom = (edge.U as Vertex<Room>).Item;
            var endRoom = (edge.V as Vertex<Room>).Item;


            var startPosf = startRoom.bounds.center;
            var endPosf = endRoom.bounds.center;

            var startPos = new Vector3Int(Mathf.RoundToInt(startPosf.x), 0, Mathf.RoundToInt(startPosf.z));
            var endPos = new Vector3Int(Mathf.RoundToInt(endPosf.x), 0, Mathf.RoundToInt(endPosf.z));


            var path = aStar.FindPath(startPos, endPos, (DungeonPathfinder3D.Node a, DungeonPathfinder3D.Node b) => {
                var pathCost = new DungeonPathfinder3D.PathCost
                {
                    traversable = true, // It's traversable if it's a valid neighbor (already guaranteed to be delta.y == 0)
                    isStairs = false    // Not stairs
                };
                float stepCost = 1f;

                if (!grid.InBounds(b.Position))
                {
                    pathCost.traversable = false;
                    return pathCost;
                }

                if (b.Position == endPos)
                {
                    // No penalty to step into the goal room (makes connection reliable)
                    stepCost += 0f;
                }
                else if (grid[b.Position] == CellType.Room)
                {
                    stepCost += 1f; // small penalty for being inside a room (was +5 — that was too large)
                }
                else if (grid[b.Position] == CellType.None)
                {
                    stepCost += 1f;
                }
                else if (grid[b.Position] == CellType.Hallway)
                {
                    stepCost += 0.1f; // prefer corridors slightly
                }


                pathCost.cost = stepCost + Vector3Int.Distance(b.Position, endPos);

                return pathCost;
            });

            if (path != null) {
                for (int i = 0; i < path.Count; i++) {
                    var current = path[i];

                    if (grid[current] == CellType.None) {
                        grid[current] = CellType.Hallway;
                    }

                    if (i > 0) {
                        var prev = path[i - 1];                        

                        Debug.DrawLine(prev + new Vector3(0.5f, 0.5f, 0.5f), current + new Vector3(0.5f, 0.5f, 0.5f), Color.blue, 100, false);
                    }
                }

                foreach (var pos in path) {
                    if (grid[pos] == CellType.Hallway) {
                        PlaceHallway(pos);
                    }

                }
            }
            else
            {
                // Add a debug message to see if any path is failing
                Debug.LogError($"Pathfinding failed between room at {startRoom.bounds.center} and {endRoom.bounds.center}");
            }
        }


    }

    void PlaceCube(Vector3Int location, Vector3Int size, Material material) {
        GameObject go = Instantiate(cubePrefab, location, Quaternion.identity);
        go.GetComponent<Transform>().localScale = size;
        go.GetComponent<MeshRenderer>().material = material;
    }

    void PlaceRoom(Vector3Int location, Vector3Int size) {
        PlaceCube(location, size, redMaterial);
    }

    void PlaceHallway(Vector3Int location) {
        PlaceCube(location, new Vector3Int(1, 1, 1), blueMaterial);
    }

    void PlaceStairs(Vector3Int location) {
        PlaceCube(location, new Vector3Int(1, 1, 1), greenMaterial);
    }
}

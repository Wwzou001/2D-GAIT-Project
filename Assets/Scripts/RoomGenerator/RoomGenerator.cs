using UnityEngine;
using UnityEngine.Tilemaps;
 
// One room's worth of settings. Each technique's demo scene builds one of
// these with whatever it needs, and hands it to RoomGenerator.GenerateRoom().
// This is what makes the generator reusable across scenes instead of having
// one fixed set of toggles - an A* scene ticks Include A* Agent, a Steering
// scene ticks Include Spiders, an FSM scene ticks Include FSM Agent, and so
// on, all through the same generator. A scene is free to tick more than
// one if it wants several techniques in the same room at once.
//
// A RoomSpec's own coordinates (playerStart, aStarAgentStart, width,
// height, and so on) are always LOCAL to that one room, starting at (0,0).
// When WorldGenerator places several rooms together, it supplies a world
// offset so each room's local coordinates land in a different,
// non-overlapping part of the shared GridSystem.
[System.Serializable]
public class RoomSpec
{
    [Header("Player")]
    public bool includePlayer = true;
    public GameObject playerPrefab;
    public Vector2Int playerStart = new Vector2Int(0, 0);
 
    [Header("Moving Obstacle")]
    public bool includeMovingObstacle = false;
    public GameObject movingObstaclePrefab;
    public Vector2Int movingObstacleStart = new Vector2Int(5, 5);
 
    [Header("A*")]
    public bool includeAStarAgent = false;
    public GameObject aStarAgentPrefab;
    public Vector2Int aStarAgentStart = new Vector2Int(9, 9);
 
    [Header("FSM")]
    public bool includeFSMAgent = false;
    public GameObject fsmAgentPrefab;
    public Vector2Int fsmAgentStart = new Vector2Int(9, 9);
 
    [Header("Decision Tree / Behaviour Tree")]
    public bool includeBehaviourTreeAgent = false;
    public GameObject behaviourTreeAgentPrefab;
    public Vector2Int behaviourTreeAgentStart = new Vector2Int(9, 9);
 
    [Header("Steering / Flocking")]
    [UnityEngine.Serialization.FormerlySerializedAs("includeFlies")]
    public bool includeSpiders = false;
    [UnityEngine.Serialization.FormerlySerializedAs("flyPrefab")]
    public GameObject spiderPrefab;
    [UnityEngine.Serialization.FormerlySerializedAs("flyCount")]
    public int spiderCount = 5;
 
    [Header("Room size")]
    public int width = 10;
    public int height = 10;
 
    [Header("Tilemap (painted to match the room size above)")]
    public bool paintTilemap = true;
    public TileBase floorTile;
    [Tooltip("Used for any side below that has no tile of its own.")]
    public TileBase wallTile;
    [Tooltip("Optional. The four corners stay empty unless a tile is set here.")]
    public TileBase wallTileCorner;
    public TileBase wallTileLeft;
    public TileBase wallTileRight;
    public TileBase wallTileAbove;
    [Tooltip("Second tile for the top wall, placed one row above (north of) Wall Tile Above. Needs the Overlay Tilemap to be assigned.")]
    public TileBase wallTileAboveOverlay;
    public TileBase wallTileBelow;
 
    [Header("Random layout")]
    public int randomSeed = 0;   // same seed gives the same layout every time, 0 picks a new one
}
 
// Builds a single room from a RoomSpec. Used three ways:
//   1. In the Editor: fill in Default Spec below, click Generate Room
//      (via the custom editor button) to build it right there in the scene.
//   2. From code: any other script can call GenerateRoom(mySpec) with its
//      own settings, so each technique's demo scene can request exactly
//      the room it needs without touching this component's Inspector fields.
//   3. From WorldGenerator: GenerateRoomAt(spec, offset, name) builds a room
//      positioned at a world offset, without clearing any other rooms that
//      already exist - this is what lets several rooms coexist as one world.
public class RoomGenerator : MonoBehaviour
{
    [Header("Used when Generate Room is clicked in the Inspector")]
    public RoomSpec defaultSpec = new RoomSpec();
 
    [Header("Tilemaps that get painted to fit the room (use new, empty ones)")]
    public Tilemap floorTilemap;
    public Tilemap wallTilemap;   // optional, walls go on the floor tilemap if empty
 
    [Header("Extra tilemap layer (optional, draws on top of the walls)")]
    [Tooltip("A third tilemap. Whatever is set in Wall Tile Above Overlay is painted here on the same cells as the top wall, so the top wall can use two tiles.")]
    public Tilemap overlayTilemap;
 
    [Header("Door (placed from the room size)")]
    public bool placeDoor = true;
    [Tooltip("The Door object in the scene. It is moved into the top wall above the door cell.")]
    public Transform doorObject;
 
    [Header("Camera")]
    [Tooltip("After Generate Room, centre the main camera on the room and zoom to fit it.")]
    public bool fitCamera = true;
    public float cameraPadding = 1.5f;
 
    // Everything generated is placed under this so a re-generate can clean up cleanly.
    private const string GeneratedRootName = "RoomGenerator_Generated";
 
    // Called by the custom editor's Generate Room button. Builds defaultSpec.
    public void GenerateRoom()
    {
        GenerateRoom(defaultSpec);
    }
 
    // The reusable single-room entry point. Clears whatever this
    // RoomGenerator built last time, then builds spec at local (0,0).
    public void GenerateRoom(RoomSpec spec)
    {
        ClearPreviousRoom();
        ClearTiles();
 
        Transform root = new GameObject(GeneratedRootName).transform;
        root.SetParent(transform);
 
        BuildRoom(spec, Vector2Int.zero, root);
        PlaceDoor(spec);
        FitCameraToRoom(spec);
    }
 
    // Puts the win cell in the top right inside corner of the room and the
    // Door object in the wall right above it, so the door follows the room size.
    private void PlaceDoor(RoomSpec spec)
    {
        if (!placeDoor) return;
 
        Vector2Int doorCell = new Vector2Int(spec.width - 1, spec.height - 1);
 
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetDoorPosition(doorCell);
        }
        else
        {
            // In edit mode Instance is not set yet, so find it directly.
            GameManager gm = FindFirstObjectByType<GameManager>();
            if (gm != null)
            {
                gm.SetDoorPosition(doorCell);
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(gm);
#endif
            }
            else
            {
                Debug.LogWarning("RoomGenerator: Place Door is ticked but there is no GameManager in the scene.");
            }
        }
 
        if (doorObject != null)
        {
            doorObject.position = new Vector3(doorCell.x, doorCell.y + 1, doorObject.position.z);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(doorObject);
#endif
        }
    }
 
    // Centres the main camera on the room and zooms so the whole room
    // (plus the wall ring and a little padding) is visible.
    private void FitCameraToRoom(RoomSpec spec)
    {
        if (!fitCamera) return;
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return;
 
        // Objects sit on whole numbers, so the room's middle cell is at (w-1)/2.
        float cx = (spec.width - 1) / 2f;
        float cy = (spec.height - 1) / 2f;
        cam.transform.position = new Vector3(cx, cy, cam.transform.position.z);
 
        float halfHeight = spec.height / 2f + 1f + cameraPadding;
        float halfWidth = (spec.width / 2f + 1f + cameraPadding) / cam.aspect;
        cam.orthographicSize = Mathf.Max(halfHeight, halfWidth);
    }
 
    // Builds one room at a world offset, as a new standalone object that
    // does NOT clear anything else. Used by WorldGenerator so multiple
    // rooms can exist together. Returns the room's root transform so the
    // caller can parent/track it.
    public Transform GenerateRoomAt(RoomSpec spec, Vector2Int offset, string roomName)
    {
        Transform root = new GameObject(roomName).transform;
        BuildRoom(spec, offset, root);
        return root;
    }
 
    // The actual build logic, shared by both entry points above. Every
    // position in spec is local to the room; offset shifts all of them
    // into their place in the wider world (Vector2Int.zero for a single
    // standalone room).
    private void BuildRoom(RoomSpec spec, Vector2Int offset, Transform root)
    {
        PaintTiles(spec, offset);
 
        if (spec.includeMovingObstacle)
        {
            Spawn(spec.movingObstaclePrefab, spec.movingObstacleStart + offset, root, "MovingObstacle");
        }
 
        GameObject player = null;
        if (spec.includePlayer)
        {
            player = Spawn(spec.playerPrefab, spec.playerStart + offset, root, "Player");
        }
 
        if (spec.includeAStarAgent)
        {
            Spawn(spec.aStarAgentPrefab, spec.aStarAgentStart + offset, root, "AStarAgent");
        }
 
        if (spec.includeFSMAgent)
        {
            GameObject fsmAgent = Spawn(spec.fsmAgentPrefab, spec.fsmAgentStart + offset, root, "FSMAgent");
            WirePlayerReference(fsmAgent, player);
        }
 
        if (spec.includeBehaviourTreeAgent)
        {
            Spawn(spec.behaviourTreeAgentPrefab, spec.behaviourTreeAgentStart + offset, root, "BehaviourTreeAgent");
        }
 
        if (spec.includeSpiders)
        {
            SpawnSpiders(spec, offset, root, player);
        }
    }
 
    // Wipes every tile on the assigned tilemaps. WorldGenerator calls this
    // once before building all its rooms.
    public void ClearTiles()
    {
        if (floorTilemap != null) floorTilemap.ClearAllTiles();
        if (wallTilemap != null) wallTilemap.ClearAllTiles();
        if (overlayTilemap != null) overlayTilemap.ClearAllTiles();
    }
 
    // Paints floor tiles over the room's width x height, plus a one tile
    // wall ring just outside it, so the map always matches the room size.
    private void PaintTiles(RoomSpec spec, Vector2Int offset)
    {
        if (!spec.paintTilemap || floorTilemap == null) return;
 
        Tilemap walls = wallTilemap != null ? wallTilemap : floorTilemap;
 
        // Say why the overlay is not showing instead of silently skipping it.
        if (spec.wallTileAboveOverlay != null && overlayTilemap == null)
            Debug.LogWarning("RoomGenerator: Wall Tile Above Overlay is set but no Overlay Tilemap is assigned on the RoomGenerator.");
        if (overlayTilemap != null && spec.wallTileAboveOverlay == null)
            Debug.LogWarning("RoomGenerator: Overlay Tilemap is assigned but Wall Tile Above Overlay is empty in the room spec.");
 
        // By default a tile is centred in the middle of its cell, which sits
        // half a cell away from objects placed on whole numbers. Anchor at
        // the cell corner so tiles line up exactly with the grid cells.
        floorTilemap.tileAnchor = Vector3.zero;
        walls.tileAnchor = Vector3.zero;
 
        for (int x = -1; x <= spec.width; x++)
        {
            for (int y = -1; y <= spec.height; y++)
            {
                bool isBorder = x == -1 || y == -1 || x == spec.width || y == spec.height;
                Vector3Int cell = new Vector3Int(x + offset.x, y + offset.y, 0);
 
                if (isBorder)
                {
                    // Corners only get a tile if Wall Tile Corner is set,
                    // edges use their own side tile.
                    bool onX = x == -1 || x == spec.width;
                    bool onY = y == -1 || y == spec.height;
                    TileBase tile = (onX && onY) ? spec.wallTileCorner : spec.wallTile;
 
                    if (!(onX && onY))
                    {
                        if (x == -1 && spec.wallTileLeft != null) tile = spec.wallTileLeft;
                        else if (x == spec.width && spec.wallTileRight != null) tile = spec.wallTileRight;
                        else if (y == -1 && spec.wallTileBelow != null) tile = spec.wallTileBelow;
                        else if (y == spec.height && spec.wallTileAbove != null) tile = spec.wallTileAbove;
                    }
 
                    if (tile != null) walls.SetTile(cell, tile);
 
                    // Second tile for the top wall: placed one row ABOVE the
                    // top wall (north of it), so the two tiles stack instead
                    // of the overlay covering the wall tile.
                    if (y == spec.height && !onX && overlayTilemap != null && spec.wallTileAboveOverlay != null)
                    {
                        overlayTilemap.tileAnchor = Vector3.zero;
                        Vector3Int northCell = new Vector3Int(cell.x, cell.y + 1, 0);
                        overlayTilemap.SetTile(northCell, spec.wallTileAboveOverlay);
                    }
                }
                else if (spec.floorTile != null)
                {
                    floorTilemap.SetTile(cell, spec.floorTile);
                }
            }
        }
    }
 
    // Removes whatever the last single-room GenerateRoom() call created.
    // Rooms built through GenerateRoomAt() (by WorldGenerator) are
    // untouched by this, since they are not tracked under GeneratedRootName.
    private void ClearPreviousRoom()
    {
        Transform existing = transform.Find(GeneratedRootName);
        if (existing != null)
        {
            DestroyImmediate(existing.gameObject);
        }
    }
 
    // Checks a LOCAL position against only the start cells that are
    // actually in use this room (so an untouched default start on a
    // technique that isn't ticked doesn't needlessly block a cell). This
    // check stays entirely in local coordinates, before offset is applied.
    private bool IsAdjacentToAnyActiveStart(RoomSpec spec, Vector2Int localPos)
    {
        Vector2Int[] starts =
        {
            spec.playerStart, spec.aStarAgentStart, spec.fsmAgentStart,
            spec.behaviourTreeAgentStart,
            spec.movingObstacleStart
        };
        bool[] active =
        {
            spec.includePlayer, spec.includeAStarAgent, spec.includeFSMAgent,
            spec.includeBehaviourTreeAgent,
            spec.includeMovingObstacle
        };
 
        for (int i = 0; i < starts.Length; i++)
        {
            if (active[i] && IsAdjacentOrEqual(localPos, starts[i]))
                return true;
        }
        return false;
    }
 
    // Spiders are different from the single-instance agents above: there are
    // several of them, they are not grid-locked (they fly freely), and
    // SpiderFSM needs a couple of things wired up to actually work - a
    // reference to the player to flee from, and boundary limits so they
    // stay inside this room instead of wandering off. RoomGenerator sets
    // both of these automatically here, based on the room's own size and
    // world offset, so nothing needs fixing by hand afterward.
    private void SpawnSpiders(RoomSpec spec, Vector2Int offset, Transform root, GameObject player)
    {
        if (spec.spiderPrefab == null)
        {
            Debug.LogWarning("RoomGenerator: Include Spiders is ticked but no Spider Prefab is assigned.");
            return;
        }
 
        System.Random rng = spec.randomSeed != 0 ? new System.Random(spec.randomSeed) : new System.Random();
 
        // Keep the flock roughly centred in the room (in world space, so
        // offset is included here), with room to move before hitting the
        // edges.
        Vector2 boundaryCenter = new Vector2(spec.width / 2f + offset.x, spec.height / 2f + offset.y);
        Vector2 boundaryDims = new Vector2(spec.width / 2f - 1f, spec.height / 2f - 1f);
 
        for (int i = 0; i < spec.spiderCount; i++)
        {
            // Spawn somewhere near the middle of the room, not right on the
            // edge, so they start inside their own boundary.
            float x = boundaryCenter.x + (float)(rng.NextDouble() * 2 - 1) * (boundaryDims.x * 0.5f);
            float y = boundaryCenter.y + (float)(rng.NextDouble() * 2 - 1) * (boundaryDims.y * 0.5f);
 
            GameObject go = Instantiate(spec.spiderPrefab, new Vector3(x, y, 0f), Quaternion.identity, root);
            go.name = $"Spider_{i}";
 
            SpiderFSM spider = go.GetComponent<SpiderFSM>();
            if (spider == null)
            {
                Debug.LogWarning("RoomGenerator: Spider Prefab has no SpiderFSM component, it will not flock or flee.");
                continue;
            }
 
            spider.boundaryCenter = boundaryCenter;
            spider.boundaryDims = boundaryDims;
 
            // Only wire up fleeing if a player was actually spawned this room.
            if (player != null)
            {
                spider.player = player.transform;
            }
        }
    }
 
    private bool IsAdjacentOrEqual(Vector2Int pos, Vector2Int start)
    {
        if (pos == start) return true;
        int dx = Mathf.Abs(pos.x - start.x);
        int dy = Mathf.Abs(pos.y - start.y);
        return (dx + dy) == 1;
    }
 
    // cell is already in world/GridSystem coordinates (offset already applied).
    private GameObject Spawn(GameObject prefab, Vector2Int cell, Transform root, string name)
    {
        if (prefab == null)
        {
            Debug.LogWarning($"RoomGenerator: no prefab assigned for {name}, skipping.");
            return null;
        }
 
        GameObject go = Instantiate(prefab, new Vector3(cell.x, cell.y, 0f), Quaternion.identity, root);
        go.name = name;
 
        // GridMover's own Start() overrides position based on its own
        // startPosition field, so without this, every agent would end up
        // wherever its prefab's startPosition happens to be - usually
        // (0,0) - regardless of where RoomGenerator actually placed it.
        GridMover mover = go.GetComponent<GridMover>();
        if (mover != null)
        {
            mover.SetStartPosition(cell);
        }
 
        return go;
    }
 
    // Several agent types (FSM, and likely others) need a reference to the
    // player's GridMover to actually do anything. This wires that up
    // automatically after spawning, the same way SpawnSpiders() already does
    // for spider.player, so nothing needs fixing by hand afterward.
    private void WirePlayerReference(GameObject agent, GameObject player)
    {
        if (agent == null || player == null)
            return;
 
        FSMEnemyController fsm = agent.GetComponent<FSMEnemyController>();
        if (fsm != null)
        {
            GridMover playerMover = player.GetComponent<GridMover>();
            if (playerMover != null)
            {
                fsm.SetPlayer(playerMover);
            }
        }
    }
}
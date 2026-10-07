using UnityEngine;

// One room's worth of settings. Each technique's demo scene builds one of
// these with whatever it needs, and hands it to RoomGenerator.GenerateRoom().
// This is what makes the generator reusable across scenes instead of having
// one fixed set of toggles - an A* scene ticks Include A* Agent, a Steering
// scene ticks Include Flies, an FSM scene ticks Include FSM Agent, and so
// on, all through the same generator. A scene is free to tick more than
// one if it wants several techniques in the same room at once.
[System.Serializable]
public class RoomSpec
{
    [Header("Player and obstacles")]
    public bool includePlayer = true;
    public bool includeObstacles = true;
    public GameObject playerPrefab;
    public GameObject obstaclePrefab;
    public Vector2Int playerStart = new Vector2Int(0, 0);
    public int obstacleCount = 1;

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
    public bool includeFlies = false;
    public GameObject flyPrefab;
    public int flyCount = 5;

    [Header("Room size")]
    public int width = 10;
    public int height = 10;

    [Header("Random layout")]
    public int randomSeed = 0;   // same seed gives the same layout every time, 0 picks a new one
}

// Builds a single room from a RoomSpec. Used two ways:
//   1. In the Editor: fill in Default Spec below, click Generate Room
//      (via the custom editor button) to build it right there in the scene.
//   2. From code: any other script can call GenerateRoom(mySpec) with its
//      own settings, so each technique's demo scene can request exactly
//      the room it needs without touching this component's Inspector fields.
// This only builds ONE room. Combining several rooms into a connected map
// is the world generator's job, a separate piece that calls this one
// once per room it needs.
public class RoomGenerator : MonoBehaviour
{
    [Header("Used when Generate Room is clicked in the Inspector")]
    public RoomSpec defaultSpec = new RoomSpec();

    // Everything generated is placed under this so a re-generate can clean up cleanly.
    private const string GeneratedRootName = "RoomGenerator_Generated";

    // Called by the custom editor's Generate Room button. Builds defaultSpec.
    public void GenerateRoom()
    {
        GenerateRoom(defaultSpec);
    }

    // The reusable entry point. Any script, including a technique-specific
    // demo scene setup script, can call this directly with its own spec.
    public void GenerateRoom(RoomSpec spec)
    {
        ClearPreviousRoom();

        Transform root = new GameObject(GeneratedRootName).transform;
        root.SetParent(transform);

        if (spec.includeObstacles)
        {
            SpawnObstacles(spec, root);
        }

        GameObject player = null;
        if (spec.includePlayer)
        {
            player = Spawn(spec.playerPrefab, spec.playerStart, root, "Player");
        }

        if (spec.includeAStarAgent)
        {
            Spawn(spec.aStarAgentPrefab, spec.aStarAgentStart, root, "AStarAgent");
        }

        if (spec.includeFSMAgent)
        {
            GameObject fsmAgent = Spawn(spec.fsmAgentPrefab, spec.fsmAgentStart, root, "FSMAgent");
            WirePlayerReference(fsmAgent, player);
        }

        if (spec.includeBehaviourTreeAgent)
        {
            Spawn(spec.behaviourTreeAgentPrefab, spec.behaviourTreeAgentStart, root, "BehaviourTreeAgent");
        }

        if (spec.includeFlies)
        {
            SpawnFlies(spec, root, player);
        }
    }

    // Removes whatever the last GenerateRoom() call created, so calling it
    // again does not just keep adding more copies on top.
    private void ClearPreviousRoom()
    {
        Transform existing = transform.Find(GeneratedRootName);
        if (existing != null)
        {
            DestroyImmediate(existing.gameObject);
        }
    }

    private void SpawnObstacles(RoomSpec spec, Transform root)
    {
        if (spec.obstaclePrefab == null)
        {
            Debug.LogWarning("RoomGenerator: Include Obstacles is ticked but no Obstacle Prefab is assigned.");
            return;
        }

        System.Random rng = spec.randomSeed != 0 ? new System.Random(spec.randomSeed) : new System.Random();

        // Every single-instance agent's start cell gets kept clear, whichever
        // of them happen to be ticked for this room.
        Vector2Int[] startsToAvoid =
        {
            spec.playerStart, spec.aStarAgentStart, spec.fsmAgentStart,
            spec.behaviourTreeAgentStart
        };

        int placed = 0;
        int safetyLimit = 200;

        while (placed < spec.obstacleCount && safetyLimit-- > 0)
        {
            Vector2Int pos = new Vector2Int(rng.Next(0, spec.width), rng.Next(0, spec.height));

            if (IsAdjacentToAnyActiveStart(spec, pos, startsToAvoid)) continue;

            Spawn(spec.obstaclePrefab, pos, root, $"Obstacle_{pos.x}_{pos.y}");
            placed++;
        }
    }

    // Checks pos against only the start cells that are actually in use this
    // room (so an untouched default start on a technique that isn't ticked
    // doesn't needlessly block an obstacle cell).
    private bool IsAdjacentToAnyActiveStart(RoomSpec spec, Vector2Int pos, Vector2Int[] starts)
    {
        bool[] active =
        {
            spec.includePlayer, spec.includeAStarAgent, spec.includeFSMAgent,
            spec.includeBehaviourTreeAgent
        };

        for (int i = 0; i < starts.Length; i++)
        {
            if (active[i] && IsAdjacentOrEqual(pos, starts[i]))
                return true;
        }
        return false;
    }

    // Flies are different from the single-instance agents above: there are
    // several of them, they are not grid-locked (they fly freely), and
    // FlyFSM needs a couple of things wired up to actually work - a
    // reference to the player to flee from, and boundary limits so they
    // stay inside this room instead of wandering off. RoomGenerator sets
    // both of these automatically here, based on the room's own size, so
    // nothing needs fixing by hand afterward.
    private void SpawnFlies(RoomSpec spec, Transform root, GameObject player)
    {
        if (spec.flyPrefab == null)
        {
            Debug.LogWarning("RoomGenerator: Include Flies is ticked but no Fly Prefab is assigned.");
            return;
        }

        System.Random rng = spec.randomSeed != 0 ? new System.Random(spec.randomSeed) : new System.Random();

        // Keep the flock roughly centred in the room, with room to move
        // before hitting the edges.
        Vector2 boundaryCenter = new Vector2(spec.width / 2f, spec.height / 2f);
        Vector2 boundaryDims = new Vector2(spec.width / 2f - 1f, spec.height / 2f - 1f);

        for (int i = 0; i < spec.flyCount; i++)
        {
            // Spawn somewhere near the middle of the room, not right on the
            // edge, so they start inside their own boundary.
            float x = boundaryCenter.x + (float)(rng.NextDouble() * 2 - 1) * (boundaryDims.x * 0.5f);
            float y = boundaryCenter.y + (float)(rng.NextDouble() * 2 - 1) * (boundaryDims.y * 0.5f);

            GameObject go = Instantiate(spec.flyPrefab, new Vector3(x, y, 0f), Quaternion.identity, root);
            go.name = $"Fly_{i}";

            FlyFSM fly = go.GetComponent<FlyFSM>();
            if (fly == null)
            {
                Debug.LogWarning("RoomGenerator: Fly Prefab has no FlyFSM component, it will not flock or flee.");
                continue;
            }

            fly.boundaryCenter = boundaryCenter;
            fly.boundaryDims = boundaryDims;

            // Only wire up fleeing if a player was actually spawned this room.
            if (player != null)
            {
                fly.player = player.transform;
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
        // wherever its prefab's startPosition happens to be - usually (0,0) -
        // regardless of where RoomGenerator actually placed it.
        GridMover mover = go.GetComponent<GridMover>();
        if (mover != null)
        {
            mover.SetStartPosition(cell);
        }

        return go;
    }
    // Several agent types (FSM, and likely others) need a reference to the
    // player's GridMover to actually do anything. This wires that up
    // automatically after spawning, the same way SpawnFlies() already does
    // for fly.player, so nothing needs fixing by hand afterward.
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

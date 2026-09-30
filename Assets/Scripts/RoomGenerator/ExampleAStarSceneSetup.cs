using UnityEngine;

// EXAMPLE ONLY - shows the pattern for how a specific technique's demo
// scene calls the shared RoomGenerator with its own parameters, per the
// client's request in Meeting 7 (item 8): "each technique's demo scene
// calls the generator with its own parameters."
//
// This is not meant to be used as-is - copy the pattern into a real setup
// script for each technique (an AStarSceneSetup, a SteeringSceneSetup,
// etc.), each building its own RoomSpec with whatever that technique needs.
public class ExampleAStarSceneSetup : MonoBehaviour
{
    [SerializeField] private RoomGenerator roomGenerator;

    [Header("This scene's own prefabs")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject aStarAgentPrefab;
    [SerializeField] private GameObject obstaclePrefab;

    private void Start()
    {
        // Build the spec this A* demo scene wants: a bigger grid with more
        // obstacles than the default, since A* needs a more interesting
        // path to actually demonstrate the algorithm.
        RoomSpec aStarDemoSpec = new RoomSpec
        {
            includePlayer = true,
            includeAStarAgent = true,
            includeObstacles = true,

            playerPrefab = playerPrefab,
            aStarAgentPrefab = aStarAgentPrefab,
            obstaclePrefab = obstaclePrefab,

            width = 15,
            height = 15,
            obstacleCount = 12,

            playerStart = new Vector2Int(0, 0),
            aStarAgentStart = new Vector2Int(14, 14),
        };

        roomGenerator.GenerateRoom(aStarDemoSpec);
    }
}

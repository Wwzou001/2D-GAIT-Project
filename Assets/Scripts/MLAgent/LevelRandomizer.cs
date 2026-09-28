using UnityEngine;
using System.Collections.Generic;

// Randomise start position, goal position, and a level specifix set of obstacle position each episode
public class LevelRandomizer : MonoBehaviour
{
    // Reference
    [SerializeField] private Transform startPosition;
    [SerializeField] private Transform goalPosition;

    // Ordered near to far from start in teaching order
    [SerializeField] private List<Transform> obstacleTransforms;

    // Randomisation range
    [SerializeField] private float minSeparation = 8f; // minimum distance between start and goal

    [SerializeField] private float maxSeparation = 10f;
    [SerializeField] private bool allowGoalOnEitherSide = true; // if true, goal can end up left or right of start, so agent can learn direction
    [SerializeField] private float obstacleMargin = 1.5f; // keep obstacles at least this far from start/goal themselves

    // Map bounds (world X)
    [SerializeField] private float mapMinX = -14f;
    [SerializeField] private float mapMaxX = 14f;

    // Spacing safety
    [SerializeField] private float minObstacleGap = 2f; // minimum gap between consecutive obstacles, must be at least the fixed jump distance, or a gap can be un-crossable
    [SerializeField] private int maxSeparationRetries = 10;

    // Max space between obstacles
    [SerializeField] private float maxObstacleGap = 2.8f;

    [SerializeField] private float minSpikeGap = 4f;
    
    // Manual test override
    [SerializeField] private bool overrideSpikeDifficultyForTesting = false;
    [SerializeField] private float testSpikeDifficulty = 1f;

    // Curriculum -- enemy
    [SerializeField] private float minEnemyGap = 8f;

    [SerializeField] private bool overrideEnemyDifficultyForTesting = false;
    [SerializeField] private float testEnemyDifficulty = 1f;

    [SerializeField] private List<Transform> enemyTransforms;
    private float enemyDifficulty = 0f;

    public float EffectiveEnemyDifficulty => overrideEnemyDifficultyForTesting ? testEnemyDifficulty : enemyDifficulty;
    public void SetEnemyDifficulty(float difficulty) => enemyDifficulty = Mathf.Clamp01(difficulty);

    // Curriculum -- hazards spikes
    [SerializeField] private List<Transform> spikeTransforms;
    private float spikeDifficulty = 0f;

    public float EffectiveSpikeDifficulty => overrideSpikeDifficultyForTesting ? testSpikeDifficulty : spikeDifficulty;

    public void SetSpikeDifficulty(float difficulty) => spikeDifficulty = Mathf.Clamp01(difficulty);

    private List<float> enemyInitialYs = new List<float>();

    private void Start()
    {
        foreach (var enemy in enemyTransforms)
        {
            enemyInitialYs.Add(enemy != null ? enemy.position.y : 0f);
        }
    }

    // Fixed y position, only x is randomised
    public void RandomiseLevel()
    {
        if (startPosition == null || goalPosition == null) return;

        float direction = (!allowGoalOnEitherSide || Random.value < 0.5f) ? 1f : -1f;

        // Start stays where it was placed in the scene, goal placed separation unit away in chosen direction, both keep own y postion
        Vector3 startPos = startPosition.position;

        float separation = 0f;
        float clampedGoalX = startPos.x;
        int attempts = 0;
        int obstacleCount = Mathf.Max(1, obstacleTransforms != null ? obstacleTransforms.Count : 1);

        do
        {
            separation = Random.Range(minSeparation, maxSeparation);
            clampedGoalX = Mathf.Clamp(startPos.x + direction * separation, mapMinX, mapMaxX);
            attempts++;
        }
        while (attempts < maxSeparationRetries && !SlotsWideEnough(startPos.x, clampedGoalX, obstacleCount));

        Vector3 goalPos = goalPosition.position;
        goalPos.x = clampedGoalX;
        goalPosition.position = goalPos;

        PlaceObstaclesBetween(startPos.x, goalPos.x);
        PlaceSpikes(startPos.x, goalPos.x);
        PlaceEnemiesBetween(startPos.x, goalPos.x);
    }

    // Divide the space between start and goal into one slot per obstacle, and place each obstacle at random x within its own slot
    private void PlaceObstaclesBetween(float startX, float goalX)
    {
        if (obstacleTransforms == null || obstacleTransforms.Count == 0) return;

        float low = Mathf.Max(Mathf.Min(startX, goalX) + obstacleMargin, mapMinX);
        float high = Mathf.Min(Mathf.Max(startX, goalX) - obstacleMargin, mapMaxX);

        int count = obstacleTransforms.Count;

        if (high <= low)
        {
            // Separation end up too small for margin - place everything at midpoint rather than produce an inverted range
            float mid = (startX + goalX) / 2;
            foreach (Transform obstacle in obstacleTransforms)
            {
                if (obstacle == null) continue;
                Vector3 pos = obstacle.position;
                pos.x = mid;
                obstacle.position = pos;
            }
            return;
        }

        float walkDir = goalX >= startX ? 1f : -1f;

        // The side of low, high the agent enters from, walk from start to goal
        float entryX = walkDir > 0f ? low : high;
        float totalSpan = high - low;

        float lastPlacedDist = 0f;

        for (int i = 0; i < count; i++)
        {
            Transform obstacle = obstacleTransforms[i];
            if (obstacle == null) continue;

            int remainingAfter = count - 1 - i;

            float distMin = lastPlacedDist + minObstacleGap;

            // Leave enough room for whatever obstacles still need to be placed after this one
            float requiredForRest = remainingAfter * minObstacleGap;
            float distMax = totalSpan - requiredForRest;

            // maxObstacleGap only cap the hop from previous obstacle to this one
            if (i > 0)
            {
                distMax = Mathf.Min(distMax, lastPlacedDist + maxObstacleGap);
            }

            float dist;
            if (distMin <= distMax)
            {
                dist = Random.Range(distMin, distMax);
            }
            else
            {
                dist = distMin;
            }
            
            dist = Mathf.Clamp(dist, 0f, totalSpan);
            float x = entryX + walkDir * dist;

            Vector3 pos = obstacle.position;
            pos.x = x;
            obstacle.position = pos;

            lastPlacedDist = dist;

            Debug.Log($"Obstacle {i} ({obstacle.name}) placed at X={x}, dist from entry={dist}");
        }
    }

    private void PlaceSpikes(float startX, float goalX)
    {
        if (spikeTransforms == null || spikeTransforms.Count == 0) return;

        int activeCount = Mathf.RoundToInt(EffectiveSpikeDifficulty * spikeTransforms.Count);

        float low = Mathf.Max(Mathf.Min(startX, goalX) + obstacleMargin, mapMinX);
        float high = Mathf.Min(Mathf.Max(startX, goalX) - obstacleMargin, mapMaxX);

        float lastPlacedX = float.NegativeInfinity;
        int placedSoFar = 0;

        for (int i = 0; i < spikeTransforms.Count; i++)
        {
            Transform spike = spikeTransforms[i];
            if (spike == null) continue;

            bool active = i < activeCount;
            spike.gameObject.SetActive(active);

            if (!active || high <= low) continue;

            int remainingAfter = activeCount - 1 - placedSoFar;

            float rangeMin = Mathf.Max(low, lastPlacedX + minSpikeGap);
            float rangeMax = high - remainingAfter * minSpikeGap;

            float x;
            if (rangeMin <= rangeMax)
            {
                x = Random.Range(rangeMin, rangeMax);
            }
            else
            {
                x = Mathf.Clamp(rangeMin, low, high);
            }

            Vector3 pos = spike.position;
            pos.x = x;
            spike.position = pos;

            lastPlacedX = x;
            placedSoFar++;
        }
    }

    private void PlaceEnemiesBetween(float startX, float goalX)
    {
        if (enemyTransforms == null || enemyTransforms.Count == 0) return;

        int activeCount = Mathf.RoundToInt(EffectiveEnemyDifficulty * enemyTransforms.Count);

        float low = Mathf.Max(Mathf.Min(startX, goalX) + obstacleMargin, mapMinX);
        float high = Mathf.Min(Mathf.Max(startX, goalX) - obstacleMargin, mapMaxX);

        float lastPlacedX = float.NegativeInfinity;
        int placedSoFar = 0;

        for (int i = 0; i < enemyTransforms.Count; i++)
        {
            Transform enemy = enemyTransforms[i];
            if (enemy == null) continue;

            bool active = i < activeCount;
            enemy.gameObject.SetActive(active);
            if (!active || high <= low) continue;

            int remainingAfter = activeCount - 1 - placedSoFar;
            float rangeMin = Mathf.Max(low, lastPlacedX + minEnemyGap);
            float rangeMax = high - remainingAfter * minEnemyGap;

            float x = (rangeMin <= rangeMax) ? Random.Range(rangeMin, rangeMax) : Mathf.Clamp(rangeMin, low, high);

            Vector3 pos = enemy.position;
            pos.x = x;
            pos.y = enemyInitialYs[i];
            enemy.position = pos;

            lastPlacedX = x;
            placedSoFar++;

            enemy.GetComponent<GroundEnemy>()?.ResetForEpisode();
            enemy.GetComponent<FlyingEnemy>()?.ResetForEpisode();
            enemy.GetComponent<CrossMoveFlyingEnemy>()?.ResetForEpisode();
        }
    }

    private bool SlotsWideEnough(float startX, float goalX, int obstacleCount)
    {
        float low = Mathf.Max(Mathf.Min(startX, goalX) + obstacleMargin, mapMinX);
        float high = Mathf.Min(Mathf.Max(startX, goalX) - obstacleMargin, mapMaxX);
        if (high <= low) return false;

        // If spike level
        int activeSpikes = (spikeTransforms != null && spikeTransforms.Count > 0)
            ? Mathf.RoundToInt(spikeDifficulty * spikeTransforms.Count) : 0;

        // If enemy level
        int activeEnemies = (enemyTransforms != null && enemyTransforms.Count > 0)
            ? Mathf.RoundToInt(enemyDifficulty * enemyTransforms.Count) : 0;

        // If level have spike, box, enemy
        float requiredForObstacles = obstacleCount > 0 && (obstacleTransforms?.Count ?? 0) > 0
            ? obstacleCount * minObstacleGap : 0f;
        float requiredForSpikes = activeSpikes * minSpikeGap;
        float requiredForEnemies = activeEnemies * minEnemyGap;

        float requiredSpan = Mathf.Max(requiredForObstacles, Mathf.Max(requiredForSpikes, requiredForEnemies));
        return (high - low) >= requiredSpan;
    }
}

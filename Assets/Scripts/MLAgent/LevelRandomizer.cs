using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Tilemaps;

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

    // Curriculum -- coins
    [SerializeField] private List<Transform> coinTransforms;
    [SerializeField] private float coinMargin = 1f; // coin stay away from start/goal for at least 1 grid
    [SerializeField] private float coinStartMargin = 2f; // coin stay away form start at least 2 grid
    private float coinDifficulty = 1f;

    public void SetCoinDifficulty(float difficulty) => coinDifficulty = Mathf.Clamp01(difficulty);

    public float EffectiveEnemyDifficulty => overrideEnemyDifficultyForTesting ? testEnemyDifficulty : enemyDifficulty;
    public void SetEnemyDifficulty(float difficulty) => enemyDifficulty = Mathf.Clamp01(difficulty);

    // Curriculum -- hazards spikes
    [SerializeField] private List<Transform> spikeTransforms;
    private float spikeDifficulty = 0f;

    public float EffectiveSpikeDifficulty => overrideSpikeDifficultyForTesting ? testSpikeDifficulty : spikeDifficulty;

    public void SetSpikeDifficulty(float difficulty) => spikeDifficulty = Mathf.Clamp01(difficulty);

    // Curriculum -- hazards pits
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private TileBase groundTile;
    [SerializeField] private TileBase groundTileLower;
    [SerializeField] private List<Transform> pitTransforms;
    private float pitDifficulty = 1f;
    private int pitColumnMinY; // record initial lowest y

    public void SetPitDifficulty(float difficulty) => pitDifficulty = Mathf.Clamp01(difficulty);

    private List<Vector3Int> pitCandidatePositions = new List<Vector3Int>();

    private List<float> enemyInitialYs = new List<float>();

    private void Start()
    {
        foreach (var enemy in enemyTransforms)
        {
            enemyInitialYs.Add(enemy != null ? enemy.position.y : 0f);
        }

        GeneratePitCandidates();
    }

    private void GeneratePitCandidates()
    {
        pitCandidatePositions.Clear();

        if (startPosition == null || groundTilemap == null) return;

        int startTileX = Mathf.RoundToInt(startPosition.position.x);
        int excludeRange = 1;

        groundTilemap.CompressBounds();
        BoundsInt bounds = groundTilemap.cellBounds;

        pitColumnMinY = bounds.yMin;

        for (int x = Mathf.CeilToInt(mapMinX); x <= Mathf.FloorToInt(mapMaxX); x++)
        {
            if (Mathf.Abs(x - startTileX) <= excludeRange) continue;

            // Start from top to down find first
            for (int y = bounds.yMax - 1; y >= bounds.yMin; y--)
            {
                if (groundTilemap.HasTile(new Vector3Int(x, y, 0)))
                {
                    pitCandidatePositions.Add(new Vector3Int(x, y, 0));
                    break;
                }
            }
        }

        Debug.Log($"[LevelRandomizer] Generated {pitCandidatePositions.Count} pit candidates");
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
        PlaceCoins(startPos.x, goalPos.x);
        PlacePits();
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

    private void PlaceCoins(float startX, float goalX)
    {
        if (coinTransforms == null || coinTransforms.Count == 0) return;

        // Base on difficulty to choose coins active amount
        int totalCount = coinTransforms.Count;
        int activeCount = Mathf.RoundToInt(coinDifficulty * totalCount);
        activeCount = Mathf.Clamp(activeCount, 0, totalCount);

        foreach (var coin in coinTransforms)
        {
            if (coin != null) coin.gameObject.SetActive(false);
        }

        int count = activeCount;

        if (count == 0) return;

        float leftLow, leftHigh, rightLow, rightHigh;

        if (goalX >= startX)
        {
            leftLow = mapMinX + coinMargin;
            leftHigh = startX - coinStartMargin;
            rightLow = startX + coinStartMargin;
            rightHigh = goalX - coinMargin;
        }
        else
        {
            leftLow = goalX + coinMargin;
            leftHigh = startX - coinStartMargin;
            rightLow = startX + coinStartMargin;
            rightHigh = mapMaxX - coinMargin;
        }

        // One coin
        if (count == 1) 
        {
            float x;
            bool leftAvailable = leftHigh > leftLow;
            bool rightAvailable = rightHigh > rightLow;

            if (leftAvailable && rightAvailable)
            {
                x = Random.value < 0.5f ? Random.Range(leftLow, leftHigh) : Random.Range(rightLow, rightHigh);
            }
            else if (leftAvailable)
            {
                x = Random.Range(leftLow, leftHigh);
            }
            else if (rightAvailable)
            {
                x = Random.Range(rightLow, rightHigh);
            }
            else
            {
                return;
            }

            SetCoinPosition(coinTransforms[0], x);
        }
        // Three coins
        else if (count == 3)
        {
            if (leftHigh > leftLow)
            {
                SetCoinPosition(coinTransforms[0], Random.Range(leftLow, leftHigh));
            }
            else if (rightHigh > rightLow) 
            {
                float rightSlotWidth = (rightHigh - rightLow) / 3f;
                SetCoinPosition(coinTransforms[0], Random.Range(rightLow, rightLow + rightSlotWidth));
            }
            else
            {
                return;
            }

            if (rightHigh > rightLow)
            {
                float rightMid = (rightLow + rightHigh) / 2;
                SetCoinPosition(coinTransforms[1], Random.Range(rightLow, rightMid));
                SetCoinPosition(coinTransforms[2], Random.Range(rightMid, rightHigh));
            }
        }
        // Other amount coins
        else
        {
            float low = Mathf.Min(startX, goalX) + coinMargin;
            float high = Mathf.Max(startX, goalX) - coinMargin;
            if (high <= low) return;
            float slotWidth = (high - low) / count;

            for (int i = 0; i < count; i++)
            {
                float slotLow = low + slotWidth * i;
                float slotHigh = slotLow + slotWidth;
                SetCoinPosition(coinTransforms[i], Random.Range(slotLow, slotHigh));
            }
        }
    }

    private void SetCoinPosition(Transform coin, float x)
    {
        if (coin == null) return;
        coin.gameObject.SetActive(true);
        Vector3 pos = coin.position;
        pos.x = x;
        coin.position = pos;
    }

    private void PlacePits()
    {
        if (groundTilemap == null || groundTile == null) return;
        if (pitTransforms == null || pitTransforms.Count == 0) return;
        if (pitCandidatePositions.Count == 0) return;

        int maxPits = Mathf.Min(pitTransforms.Count, pitCandidatePositions.Count);
        int activeCount = Mathf.RoundToInt(pitDifficulty * pitTransforms.Count);
        activeCount = Mathf.Clamp(activeCount, 0, maxPits);

        float startX = startPosition != null ? startPosition.position.x : 0f;
        float goalX = goalPosition != null ? goalPosition.position.x : 0f;
        float lowX = Mathf.Min(startX, goalX);
        float highX = Mathf.Max(startX, goalX);

        int startTileX = Mathf.RoundToInt(startX);
        int goalTileX = Mathf.RoundToInt(goalX);
        int dir = goalTileX >= startTileX ? 1 : -1; // which side goal at

        // No pit near start position for 1 gap
        int startForbidMin = startTileX - 1;
        int startForbidMax = startTileX + 1;

        // No pit near goal position for 2 gaps
        int goalForbidMin = dir > 0 ? goalTileX -2 : goalTileX;
        int goalForbidMax = dir > 0 ? goalTileX : goalTileX + 2;

        List<Vector3Int> validCandidates = new List<Vector3Int>();

        foreach (var pos in pitCandidatePositions)
        {
            float cellLeftX = groundTilemap.CellToWorld(pos).x;
            float cellRightX = cellLeftX + groundTilemap.cellSize.x * 2f;
            float worldX = (cellLeftX + cellRightX) * 0.5f;

            // Only keep candidate between start and goal
            if (worldX <= lowX || worldX >= highX) continue;

            // Not over right wall
            if (pos.x + 1 > Mathf.FloorToInt(mapMaxX)) continue;

            // Pit cover range and skip when in start range
            if (pos.x <= startForbidMax && pos.x + 1 >= startForbidMin) continue;

            // Pit cover range and skip when in goal range
            if (pos.x <= goalForbidMax && pos.x + 1 >= goalForbidMin) continue;

            validCandidates.Add(pos);
        }

        // Candidate not enough, return
        if (validCandidates.Count < activeCount)
        {
            activeCount = Mathf.Min(activeCount, validCandidates.Count);
        }

        // Reset whole column
        foreach (var pos in pitCandidatePositions)
        {
            // Reset upper tile
            for (int dx = 0; dx < 2; dx++) 
            {
                int x = pos.x + dx;

                // Exclude wall for both side
                if (x < Mathf.CeilToInt(mapMinX)) continue;
                if (x > Mathf.FloorToInt(mapMaxX)) continue;

                groundTilemap.SetTile(new Vector3Int(x, pos.y, 0), groundTile);

                // Reset all lower tile
                for (int y = pos.y - 1; y >= pitColumnMinY; y--)
                {
                    groundTilemap.SetTile(new Vector3Int(x, y, 0), groundTileLower);
                }
            }          
        }

        foreach (var pit in pitTransforms)
        {
            if (pit != null) pit.gameObject.SetActive(false);
        }

        if (activeCount == 0) return;

        // Randomise candidate
        List<Vector3Int> shuffled = new List<Vector3Int>(validCandidates);
        for (int i = 0; i < shuffled.Count; i++)
        {
            int r = Random.Range(i, shuffled.Count);
            (shuffled[i], shuffled[r]) = (shuffled[r], shuffled[i]);
        }

        int minPitGapBetweenSelected = 3; // gap between selected pit >= 3
        List<Vector3Int> selected = new List<Vector3Int>();

        foreach (var candidate in shuffled)
        {
            if (selected.Count >= activeCount) break;

            bool tooClose = false;
            foreach(var s in selected)
            {
                if (Mathf.Abs(candidate.x - s.x) <= minPitGapBetweenSelected)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose) selected.Add(candidate);
        }

        // If select not enough, increat gap and retry
        if (selected.Count < activeCount)
        {
            int relaxedGap = 3;
            selected.Clear();

            foreach (var candidate in shuffled)
            {
                if (selected.Count >= activeCount) break;

                bool tooClose = false;
                foreach (var s in selected)
                {
                    if (Mathf.Abs(candidate.x - s.x) <= relaxedGap)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose) selected.Add(candidate);
            }
        }

        // Remove select column tile
        for (int i = 0; i < selected.Count; i++)
        {
            Vector3Int tilePos = selected[i];

            // Remove upper tile + all lower tile
            for (int dx = 0; dx < 2; dx++)
            {
                int x = tilePos.x + dx;

                for (int y = tilePos.y; y >= pitColumnMinY; y--)
                {
                    groundTilemap.SetTile(new Vector3Int(x, y, 0), null);
                }
            }

            if (i < pitTransforms.Count && pitTransforms[i] != null)
            {
                Transform pit = pitTransforms[i];
                pit.gameObject.SetActive(true);

                Vector3 worldPos = groundTilemap.CellToWorld(tilePos);
                worldPos.x += groundTilemap.cellSize.x * 1f;
                worldPos.y = pit.position.y;

                pit.position = worldPos;
            }
        }
        Debug.Log($"[PlacePits] startTileX={startTileX}, goalTileX={goalTileX}, dir={dir}, " +
          $"forbidStart=[{startForbidMin},{startForbidMax}], " +
          $"forbidGoal=[{goalForbidMin},{goalForbidMax}], " +
          $"validCandidates={validCandidates.Count}, activeCount={activeCount}, selected={selected.Count}");
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

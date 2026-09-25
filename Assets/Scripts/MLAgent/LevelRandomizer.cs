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
            float distMax = lastPlacedDist + maxObstacleGap;

            // Ensure have enough space for remaining obstacle later
            float requiredForRest = remainingAfter * minObstacleGap;
            float maxAllowedBySpan = totalSpan - requiredForRest;
            distMax = Mathf.Min(distMax, maxAllowedBySpan);

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

    private bool SlotsWideEnough(float startX, float goalX, int obstacleCount)
    {
        float low = Mathf.Max(Mathf.Min(startX, goalX) + obstacleMargin, mapMinX);
        float high = Mathf.Min(Mathf.Max(startX, goalX) - obstacleMargin, mapMaxX);
        if (high <= low) return false;

        float requiredSpan = obstacleCount * minObstacleGap;
        return (high - low) >= requiredSpan;
    }
}
